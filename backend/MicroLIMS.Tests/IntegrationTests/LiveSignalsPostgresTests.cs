using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using MicroLIMS.API.Configuration;
using MicroLIMS.API.Controllers;
using MicroLIMS.API.Extensions;
using MicroLIMS.API.Filters;
using MicroLIMS.API.Hubs;
using MicroLIMS.API.Json;
using MicroLIMS.API.Middleware;
using MicroLIMS.Application.Abstractions.Live;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Tests.Fixtures;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

[Collection("PostgresDatabaseCollection")]
public class LiveSignalsPostgresTests
{
    private const string TestPassword = "Live-Pass-123!";
    private static readonly string FixedJwtKeyBase64 =
        Convert.ToBase64String(Enumerable.Range(11, 48).Select(i => (byte)i).ToArray());

    private readonly PostgresTestFixture _fixture;

    public LiveSignalsPostgresTests(PostgresTestFixture fixture)
    {
        _fixture = fixture;
    }

    private sealed class ManualTimeProvider(DateTimeOffset initialUtc) : TimeProvider
    {
        private DateTimeOffset _utcNow = initialUtc;
        private readonly object _lock = new();
        private readonly List<ManualTimer> _timers = new();

        public override DateTimeOffset GetUtcNow()
        {
            lock (_lock) return _utcNow;
        }

        public void Advance(TimeSpan delta)
        {
            List<(TimerCallback Callback, object? State)> due;
            lock (_lock)
            {
                _utcNow = _utcNow.Add(delta);
                due = new List<(TimerCallback, object?)>();
                foreach (var timer in _timers.ToArray())
                    timer.CollectDue(_utcNow, due);
            }

            foreach (var (cb, state) in due)
                cb(state);
        }

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = new ManualTimer(this, callback, state, GetUtcNow(), dueTime, period);
            lock (_lock) _timers.Add(timer);
            return timer;
        }

        private void Remove(ManualTimer timer)
        {
            lock (_lock) _timers.Remove(timer);
        }

        private sealed class ManualTimer : ITimer
        {
            private readonly ManualTimeProvider _owner;
            private readonly TimerCallback _callback;
            private readonly object? _state;
            private DateTimeOffset? _nextDue;
            private TimeSpan _period;
            private bool _disposed;

            public ManualTimer(
                ManualTimeProvider owner,
                TimerCallback callback,
                object? state,
                DateTimeOffset now,
                TimeSpan dueTime,
                TimeSpan period)
            {
                _owner = owner;
                _callback = callback;
                _state = state;
                _period = period;
                _nextDue = dueTime == Timeout.InfiniteTimeSpan ? null : now.Add(dueTime);
            }

            public void CollectDue(DateTimeOffset now, List<(TimerCallback, object?)> due)
            {
                if (_disposed || _nextDue is null || _nextDue.Value > now)
                    return;

                due.Add((_callback, _state));
                _nextDue = _period > TimeSpan.Zero && _period != Timeout.InfiniteTimeSpan
                    ? now.Add(_period)
                    : null;
            }

            public bool Change(TimeSpan dueTime, TimeSpan period)
            {
                if (_disposed) return false;
                _period = period;
                _nextDue = dueTime == Timeout.InfiniteTimeSpan ? null : _owner.GetUtcNow().Add(dueTime);
                return true;
            }

            public void Dispose()
            {
                _disposed = true;
                _owner.Remove(this);
            }

            public ValueTask DisposeAsync()
            {
                Dispose();
                return ValueTask.CompletedTask;
            }
        }
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        public ConcurrentQueue<string> Messages { get; } = new();

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, Messages);
        public void Dispose() { }

        private sealed class CapturingLogger(string category, ConcurrentQueue<string> messages) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                var line = $"[{logLevel}] {category}: {formatter(state, exception)}";
                if (exception is not null)
                    line += $" | {exception.GetType().Name}: {exception.Message}";
                messages.Enqueue(line);
            }
        }
    }

    private sealed class RecordingPublisher(ILiveSignalPublisher inner) : ILiveSignalPublisher
    {
        public ConcurrentQueue<IReadOnlyList<LiveSignal>> Batches { get; } = new();

        public async Task PublishAsync(IReadOnlyCollection<LiveSignal> signals, CancellationToken ct = default)
        {
            Batches.Enqueue(signals.ToArray());
            await inner.PublishAsync(signals, ct);
        }
    }

    private sealed class ThrowingPublisher : ILiveSignalPublisher
    {
        public Task PublishAsync(IReadOnlyCollection<LiveSignal> signals, CancellationToken ct = default) =>
            throw new InvalidOperationException("Simulated publisher outage");
    }

    private async Task<(WebApplication App, HttpClient Client)> StartLiveServerAsync(
        TimeProvider? timeProvider = null,
        ILoggerProvider? loggerProvider = null,
        Func<IServiceProvider, ILiveSignalPublisher>? overridePublisher = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Environment.EnvironmentName = "Testing";

        if (loggerProvider is not null)
            builder.Logging.AddProvider(loggerProvider);

        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Key"] = FixedJwtKeyBase64,
            ["Jwt:Issuer"] = "MicroLIMS",
            ["Jwt:Audience"] = "MicroLIMS.Client",
            ["Mfa:ActiveKeyId"] = "test-k1",
            ["Mfa:EncryptionKeys:test-k1"] = FixedJwtKeyBase64,
            ["Lab:TimeZoneId"] = "Africa/Cairo",
            ["ConnectionStrings:DefaultConnection"] = _fixture.TestConnectionString
        });

        var jwtSettings = JwtConfiguration.Resolve(builder.Configuration, builder.Environment);
        builder.Services.AddSingleton(jwtSettings);
        builder.Services.AddMicroLimsDbContext(opt => opt.UseNpgsql(_fixture.TestConnectionString));
        builder.Services.AddApplicationServices(builder.Configuration);

        if (timeProvider is not null)
        {
            builder.Services.RemoveAll<TimeProvider>();
            builder.Services.AddSingleton(timeProvider);
        }

        if (overridePublisher is not null)
        {
            builder.Services.RemoveAll<ILiveSignalPublisher>();
            builder.Services.AddSingleton(overridePublisher);
        }

        builder.Services.AddMicroLimsJwtAuthentication(jwtSettings);
        builder.Services.AddMicroLimsAuthorization();
        builder.Services.AddMicroLimsRateLimiting(builder.Configuration);
        builder.Services.AddControllers(options =>
            {
                options.Filters.Add<ValidationFilter>();
                options.Filters.Add<MustChangePasswordFilter>();
            })
            .AddApplicationPart(typeof(NotificationsController).Assembly)
            .AddJsonOptions(o => ApiJsonOptions.Configure(o.JsonSerializerOptions));

        var app = builder.Build();
        app.UseMiddleware<RequestLoggingMiddleware>();
        app.UseMiddleware<ExceptionMiddleware>();
        app.UseRouting();
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseMicroLimsAuditPipeline();
        app.MapControllers();
        app.MapHub<LiveHub>("/hubs/live");

        await app.StartAsync();
        return (app, app.GetTestClient());
    }

    private static HubConnection BuildHubConnection(
        TestServer server,
        string? token,
        Action<JsonElement>? onRawSignal = null,
        Action<LiveSignal>? onSignal = null,
        bool passTokenInQuery = false)
    {
        var hubUrl = passTokenInQuery && token is not null
            ? new Uri(server.BaseAddress, $"hubs/live?access_token={Uri.EscapeDataString(token)}")
            : new Uri(server.BaseAddress, "hubs/live");

        var conn = new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                options.Transports = HttpTransportType.ServerSentEvents;
                options.HttpMessageHandlerFactory = _ => server.CreateHandler();
                if (!passTokenInQuery && token is not null)
                    options.AccessTokenProvider = () => Task.FromResult<string?>(token);
            })
            .Build();

        if (onRawSignal is not null)
            conn.On("signal", onRawSignal);

        if (onSignal is not null)
            conn.On("signal", onSignal);

        return conn;
    }

    private static async Task StartAndAwaitConnectedAsync(HubConnection conn)
    {
        await conn.StartAsync();
        await conn.InvokeAsync("UnwatchRecord", "sample", 0);
    }

    private static async Task<string> LoginAsync(HttpClient client, string username)
    {
        using var resp = await client.PostAsync(
            "/api/auth/login",
            new StringContent(JsonSerializer.Serialize(new { username, password = TestPassword }),
                System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("data").GetProperty("token").GetString()!;
    }

    private static async Task<T> WaitUntilAsync<T>(Func<T?> probe, string description, int timeoutMs = 5000) where T : class
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            var result = probe();
            if (result is not null)
                return result;
            await Task.Delay(30);
        }
        throw new TimeoutException($"Timed out waiting for: {description}");
    }

    private static async Task WaitUntilAsync(Func<bool> predicate, string description, int timeoutMs = 5000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            if (predicate())
                return;
            await Task.Delay(30);
        }
        throw new TimeoutException($"Timed out waiting for: {description}");
    }

    private static string UniqueTag(string prefix) =>
        $"{prefix}-{Guid.NewGuid():N}"[..Math.Min(prefix.Length + 9, 24)];

    private async Task<(DocumentDepartment Dept, DocumentSection Sec1, DocumentSection Sec2)> SeedSectionsAsync()
    {
        await using var db = _fixture.CreateDbContext();
        var tag = UniqueTag("LS");
        var dept = new DocumentDepartment { Code = tag, Name = $"Dept {tag}", IsActive = true };
        db.DocumentDepartments.Add(dept);
        await db.SaveChangesAsync();

        var sec1 = new DocumentSection { DepartmentId = dept.Id, Code = $"{tag}-1", Name = $"Section 1 {tag}", IsActive = true };
        var sec2 = new DocumentSection { DepartmentId = dept.Id, Code = $"{tag}-2", Name = $"Section 2 {tag}", IsActive = true };
        db.DocumentSections.AddRange(sec1, sec2);
        await db.SaveChangesAsync();
        return (dept, sec1, sec2);
    }

    private async Task<User> SeedUserInSectionAsync(RoleType roleType, int departmentId, int sectionId)
    {
        await using var db = _fixture.CreateDbContext();
        var roleId = await db.Roles
            .Where(r => r.Type == roleType)
            .Select(r => r.Id)
            .FirstAsync();

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var user = new User
        {
            Username = $"ls_{suffix}",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(TestPassword),
            FullName = $"Live User {suffix}",
            Email = $"ls_{suffix}@example.test",
            RoleId = roleId,
            IsActive = true,
            MustChangePassword = false
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        db.UserOrgMemberships.Add(new UserOrgMembership
        {
            UserId = user.Id,
            DepartmentId = departmentId,
            SectionId = sectionId
        });
        await db.SaveChangesAsync();
        return user;
    }

    private async Task<Sample> SeedSampleInSectionAsync(int sectionId, int receivedByUserId)
    {
        await using var db = _fixture.CreateDbContext();
        var causeId = await db.CausesOfTesting.Select(c => (int?)c.Id).FirstOrDefaultAsync();
        if (causeId is null)
        {
            var cause = new CauseOfTesting { Name = UniqueTag("Cause"), IsActive = true };
            db.CausesOfTesting.Add(cause);
            await db.SaveChangesAsync();
            causeId = cause.Id;
        }

        var tag = UniqueTag("SMP");
        var sample = new Sample
        {
            ReferenceNumber = tag,
            CauseOfTestingId = causeId.Value,
            ReceivedByUserId = receivedByUserId,
            SampledBy = "Sampler",
            ReceivedAt = DateTime.UtcNow,
            Status = SampleStatus.InTesting,
            Category = SampleCategory.FinishedProduct,
            LabSectionId = sectionId
        };
        db.Samples.Add(sample);
        await db.SaveChangesAsync();

        db.TestOrders.Add(new TestOrder
        {
            SampleId = sample.Id,
            TestCode = "TAMC",
            SectionId = sectionId,
            Status = ApprovalStatus.Pending,
            CurrentStep = WorkflowStep.Waiting
        });
        await db.SaveChangesAsync();
        return sample;
    }

    [PostgresFact]
    public async Task Connect_WithoutToken_Rejected()
    {
        var (app, client) = await StartLiveServerAsync();
        await using var _ = app;
        var server = app.GetTestServer();

        using var negResp = await client.PostAsync("/hubs/live/negotiate?negotiateVersion=1", null);
        Assert.Equal(HttpStatusCode.Unauthorized, negResp.StatusCode);

        await using var conn = BuildHubConnection(server, token: null);
        await Assert.ThrowsAnyAsync<Exception>(() => conn.StartAsync());
    }

    [PostgresFact]
    public async Task Connect_WithValidToken_ReceivesOwnUserSignals_NotAnotherUsers()
    {
        var (dept, sec1, _) = await SeedSectionsAsync();
        var userA = await SeedUserInSectionAsync(RoleType.Analyst, dept.Id, sec1.Id);
        var userB = await SeedUserInSectionAsync(RoleType.Analyst, dept.Id, sec1.Id);

        var (app, client) = await StartLiveServerAsync();
        await using var _ = app;
        var server = app.GetTestServer();

        var tokenA = await LoginAsync(client, userA.Username);
        var tokenB = await LoginAsync(client, userB.Username);

        var signalsA = new ConcurrentQueue<LiveSignal>();
        var signalsB = new ConcurrentQueue<LiveSignal>();

        await using var connA = BuildHubConnection(server, tokenA, onSignal: s => signalsA.Enqueue(s));
        await using var connB = BuildHubConnection(server, tokenB, onSignal: s => signalsB.Enqueue(s));
        await StartAndAwaitConnectedAsync(connA);
        await StartAndAwaitConnectedAsync(connB);

        await using (var scope = app.Services.CreateAsyncScope())
        {
            var actDb = scope.ServiceProvider.GetRequiredService<IMicroLimsDbContext>();
            actDb.CurrentUserId = userB.Id;
            actDb.QueueSignal(LiveSignalKinds.Notifications, $"user:{userA.Id}");
            actDb.QueueSignal(LiveSignalKinds.Notifications, $"user:{userB.Id}");
            await actDb.SaveChangesAsync();
        }

        var gotA = await WaitUntilAsync(
            () => signalsA.FirstOrDefault(s => s.Key == $"user:{userA.Id}"),
            "userA receives own signal");
        var gotB = await WaitUntilAsync(
            () => signalsB.FirstOrDefault(s => s.Key == $"user:{userB.Id}"),
            "userB receives own signal");

        Assert.Equal(LiveSignalKinds.Notifications, gotA.Kind);
        Assert.Equal(userB.Id, gotA.ActorUserId);
        Assert.DoesNotContain(signalsA, s => s.Key == $"user:{userB.Id}");
        Assert.DoesNotContain(signalsB, s => s.Key == $"user:{userA.Id}");
        Assert.Equal($"user:{userB.Id}", gotB.Key);
    }

    [PostgresFact]
    public async Task SectionSignal_ReachesMembersOnly()
    {
        var (dept, sec1, sec2) = await SeedSectionsAsync();
        var member = await SeedUserInSectionAsync(RoleType.Analyst, dept.Id, sec1.Id);
        var nonMember = await SeedUserInSectionAsync(RoleType.Analyst, dept.Id, sec2.Id);

        var (app, client) = await StartLiveServerAsync();
        await using var _ = app;
        var server = app.GetTestServer();

        var memberToken = await LoginAsync(client, member.Username);
        var nonMemberToken = await LoginAsync(client, nonMember.Username);

        var memberSignals = new ConcurrentQueue<LiveSignal>();
        var nonMemberSignals = new ConcurrentQueue<LiveSignal>();

        await using var memberConn = BuildHubConnection(server, memberToken, onSignal: s => memberSignals.Enqueue(s));
        await using var nonMemberConn = BuildHubConnection(server, nonMemberToken, onSignal: s => nonMemberSignals.Enqueue(s));
        await StartAndAwaitConnectedAsync(memberConn);
        await StartAndAwaitConnectedAsync(nonMemberConn);

        await using (var scope = app.Services.CreateAsyncScope())
        {
            var actDb = scope.ServiceProvider.GetRequiredService<IMicroLimsDbContext>();
            actDb.QueueSignal(LiveSignalKinds.Counters, $"section:{sec1.Id}");
            actDb.QueueSignal(LiveSignalKinds.Notifications, $"user:{nonMember.Id}");
            await actDb.SaveChangesAsync();
        }

        await WaitUntilAsync(
            () => memberSignals.FirstOrDefault(s => s.Key == $"section:{sec1.Id}"),
            "member receives section signal");
        await WaitUntilAsync(
            () => nonMemberSignals.FirstOrDefault(s => s.Key == $"user:{nonMember.Id}"),
            "nonMember marker received");

        Assert.DoesNotContain(nonMemberSignals, s => s.Key == $"section:{sec1.Id}");
    }

    [PostgresFact]
    public async Task RemovedFromSection_StopsReceivingAfterNextReevaluation()
    {
        var (dept, sec1, sec2) = await SeedSectionsAsync();
        var user = await SeedUserInSectionAsync(RoleType.Analyst, dept.Id, sec1.Id);
        var clock = new ManualTimeProvider(DateTimeOffset.UtcNow);

        var (app, client) = await StartLiveServerAsync(timeProvider: clock);
        await using var _ = app;
        var server = app.GetTestServer();

        var token = await LoginAsync(client, user.Username);
        var signals = new ConcurrentQueue<LiveSignal>();
        await using var conn = BuildHubConnection(server, token, onSignal: s => signals.Enqueue(s));
        await StartAndAwaitConnectedAsync(conn);

        // Confirm initial membership receives section:sec1.
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var actDb = scope.ServiceProvider.GetRequiredService<IMicroLimsDbContext>();
            actDb.QueueSignal(LiveSignalKinds.Counters, $"section:{sec1.Id}");
            await actDb.SaveChangesAsync();
        }
        await WaitUntilAsync(() => signals.FirstOrDefault(s => s.Key == $"section:{sec1.Id}"), "initial section signal");

        // Replace membership in DB: remove sec1, add sec2.
        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            var existing = await arrangeDb.UserOrgMemberships
                .Where(m => m.UserId == user.Id && m.SectionId == sec1.Id)
                .ToListAsync();
            arrangeDb.UserOrgMemberships.RemoveRange(existing);
            arrangeDb.UserOrgMemberships.Add(new UserOrgMembership
            {
                UserId = user.Id,
                DepartmentId = dept.Id,
                SectionId = sec2.Id
            });
            await arrangeDb.SaveChangesAsync();
        }

        signals.Clear();

        // Trigger the 5-minute periodic re-evaluation and wait deterministically for sec2 membership to activate.
        clock.Advance(TimeSpan.FromMinutes(5));
        await WaitUntilAsync(
            () =>
            {
                if (signals.Any(s => s.Key == $"section:{sec2.Id}"))
                    return true;

                using var pingScope = app.Services.CreateScope();
                var pingDb = pingScope.ServiceProvider.GetRequiredService<IMicroLimsDbContext>();
                pingDb.QueueSignal(LiveSignalKinds.Counters, $"section:{sec2.Id}");
                pingDb.SaveChanges();
                return signals.Any(s => s.Key == $"section:{sec2.Id}");
            },
            "sec2 membership joined after periodic reevaluation");

        signals.Clear();

        await using (var scope = app.Services.CreateAsyncScope())
        {
            var actDb = scope.ServiceProvider.GetRequiredService<IMicroLimsDbContext>();
            actDb.QueueSignal(LiveSignalKinds.Counters, $"section:{sec1.Id}");
            actDb.QueueSignal(LiveSignalKinds.Notifications, $"user:{user.Id}");
            await actDb.SaveChangesAsync();
        }

        await WaitUntilAsync(
            () => signals.FirstOrDefault(s => s.Key == $"user:{user.Id}"),
            "user marker received after section removal");

        Assert.DoesNotContain(signals, s => s.Key == $"section:{sec1.Id}");
    }

    [PostgresFact]
    public async Task WatchRecord_WithoutReadAccess_Refused_NoSignals()
    {
        var (dept, sec1, sec2) = await SeedSectionsAsync();
        var user = await SeedUserInSectionAsync(RoleType.Analyst, dept.Id, sec1.Id);
        var allowedSample = await SeedSampleInSectionAsync(sec1.Id, user.Id);
        var forbiddenSample = await SeedSampleInSectionAsync(sec2.Id, user.Id);

        var (app, client) = await StartLiveServerAsync();
        await using var _ = app;
        var server = app.GetTestServer();

        var token = await LoginAsync(client, user.Username);
        var signals = new ConcurrentQueue<LiveSignal>();
        await using var conn = BuildHubConnection(server, token, onSignal: s => signals.Enqueue(s));
        await StartAndAwaitConnectedAsync(conn);

        // Refused for sample outside user's section and unsupported record types.
        await Assert.ThrowsAsync<HubException>(() => conn.InvokeAsync("WatchRecord", "sample", forbiddenSample.Id));
        await Assert.ThrowsAsync<HubException>(() => conn.InvokeAsync("WatchRecord", "auditLog", 1));

        // Allowed for sample inside user's section.
        await conn.InvokeAsync("WatchRecord", "sample", allowedSample.Id);

        await using (var scope = app.Services.CreateAsyncScope())
        {
            var actDb = scope.ServiceProvider.GetRequiredService<IMicroLimsDbContext>();
            actDb.QueueSignal(LiveSignalKinds.Record, $"sample:{forbiddenSample.Id}");
            actDb.QueueSignal(LiveSignalKinds.Record, $"sample:{allowedSample.Id}");
            actDb.QueueSignal(LiveSignalKinds.Notifications, $"user:{user.Id}");
            await actDb.SaveChangesAsync();
        }

        await WaitUntilAsync(() => signals.FirstOrDefault(s => s.Key == $"sample:{allowedSample.Id}"), "allowed record signal");
        await WaitUntilAsync(() => signals.FirstOrDefault(s => s.Key == $"user:{user.Id}"), "marker signal");

        Assert.DoesNotContain(signals, s => s.Key == $"sample:{forbiddenSample.Id}");
    }

    private sealed record ScopedConfigRecords(int SolutionMaster, int Column, int HplcMethod, int KpiSetting);

    private async Task<ScopedConfigRecords> SeedScopedConfigAsync(int sectionId)
    {
        await using var db = _fixture.CreateDbContext();
        var uid = Guid.NewGuid().ToString("N")[..8];
        var solution = new SolutionMaster
        {
            SectionId = sectionId, Name = $"Diluent {uid}", Type = SolutionType.Diluent,
            ShelfLifeValue = 30, ShelfLifeUnit = ShelfLifeUnit.Days, StorageCondition = "Room", FinalVolumeMl = 1000m,
            CreatedByUserId = _fixture.SeededUserId, LastModifiedByUserId = _fixture.SeededUserId
        };
        db.SolutionMasters.Add(solution);
        var column = new ChromatographyColumn
        {
            Code = $"COL-{uid}", Name = $"Column {uid}", SectionId = sectionId,
            CreatedByUserId = _fixture.SeededUserId, LastModifiedByUserId = _fixture.SeededUserId
        };
        db.ChromatographyColumns.Add(column);
        await db.SaveChangesAsync();
        var method = new HplcMethod
        {
            Name = $"HPLC {uid}", Abbreviation = "H" + uid, SectionId = sectionId, DiluentSolutionId = solution.Id,
            ResultMode = HplcResultMode.Assay, Technique = HplcTechnique.Hplc, IsActive = true
        };
        var kpi = new KpiSetting { SectionId = sectionId, UpdatedByUserId = _fixture.SeededUserId };
        db.HplcMethods.Add(method);
        db.KpiSettings.Add(kpi);
        await db.SaveChangesAsync();
        return new ScopedConfigRecords(solution.Id, column.Id, method.Id, kpi.Id);
    }

    [PostgresFact]
    public async Task WatchRecord_SectionScopedAndPermissionGatedConfig_RefusedOutsideReadRule()
    {
        var (dept, sec1, sec2) = await SeedSectionsAsync();
        var analyst = await SeedUserInSectionAsync(RoleType.Analyst, dept.Id, sec1.Id);
        var head = await SeedUserInSectionAsync(RoleType.SectionHead, dept.Id, sec1.Id);
        var own = await SeedScopedConfigAsync(sec1.Id);
        var other = await SeedScopedConfigAsync(sec2.Id);

        var (app, client) = await StartLiveServerAsync();
        await using var _ = app;
        var server = app.GetTestServer();

        await using var analystConn = BuildHubConnection(server, await LoginAsync(client, analyst.Username), onSignal: _ => { });
        await StartAndAwaitConnectedAsync(analystConn);

        // Another section's records are refused; the analyst's own section is allowed.
        await Assert.ThrowsAsync<HubException>(() => analystConn.InvokeAsync("WatchRecord", "solutionMaster", other.SolutionMaster));
        await Assert.ThrowsAsync<HubException>(() => analystConn.InvokeAsync("WatchRecord", "chromatographyColumn", other.Column));
        await Assert.ThrowsAsync<HubException>(() => analystConn.InvokeAsync("WatchRecord", "hplcMethod", other.HplcMethod));
        await analystConn.InvokeAsync("WatchRecord", "solutionMaster", own.SolutionMaster);
        await analystConn.InvokeAsync("WatchRecord", "chromatographyColumn", own.Column);
        await analystConn.InvokeAsync("WatchRecord", "hplcMethod", own.HplcMethod);

        // Permission-gated types: an analyst holds none of these permissions.
        int roleId;
        int monitoringSettingsId;
        await using (var db = _fixture.CreateDbContext())
        {
            roleId = await db.Roles.AsNoTracking().Select(r => r.Id).FirstAsync();
            var settings = await db.MonitoringSettings.AsNoTracking().Select(m => (int?)m.Id).FirstOrDefaultAsync();
            if (settings is null)
            {
                var row = new MonitoringSettings();
                db.MonitoringSettings.Add(row);
                await db.SaveChangesAsync();
                settings = row.Id;
            }
            monitoringSettingsId = settings.Value;
        }
        await Assert.ThrowsAsync<HubException>(() => analystConn.InvokeAsync("WatchRecord", "role", roleId));
        await Assert.ThrowsAsync<HubException>(() => analystConn.InvokeAsync("WatchRecord", "sectionSelfAssignmentSetting", sec1.Id));
        // The seeded analyst role holds MonitoringPlan.View, so the same record is allowed for it ...
        await analystConn.InvokeAsync("WatchRecord", "monitoringSettings", monitoringSettingsId);

        // ... and refused for a user whose role grants no permissions at all.
        User bare;
        await using (var db = _fixture.CreateDbContext())
        {
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var bareRole = new Role { Type = RoleType.Analyst, Name = $"Bare {suffix}", IsActive = true };
            db.Roles.Add(bareRole);
            await db.SaveChangesAsync();
            bare = new User
            {
                Username = $"ls_bare_{suffix}", PasswordHash = BCrypt.Net.BCrypt.HashPassword(TestPassword),
                FullName = $"Bare {suffix}", Email = $"ls_bare_{suffix}@example.test", RoleId = bareRole.Id,
                IsActive = true, MustChangePassword = false
            };
            db.Users.Add(bare);
            await db.SaveChangesAsync();
            db.UserOrgMemberships.Add(new UserOrgMembership { UserId = bare.Id, DepartmentId = dept.Id, SectionId = sec1.Id });
            await db.SaveChangesAsync();
        }
        await using var bareConn = BuildHubConnection(server, await LoginAsync(client, bare.Username), onSignal: _ => { });
        await StartAndAwaitConnectedAsync(bareConn);
        await Assert.ThrowsAsync<HubException>(() => bareConn.InvokeAsync("WatchRecord", "monitoringSettings", monitoringSettingsId));

        // A section head: own section's KPI settings and self-assignment setting, never another section's.
        await using (var db = _fixture.CreateDbContext())
        {
            db.SectionSelfAssignmentSettings.AddRange(
                new SectionSelfAssignmentSetting { SectionId = sec1.Id, Enabled = true, ChangedAt = DateTime.UtcNow },
                new SectionSelfAssignmentSetting { SectionId = sec2.Id, Enabled = true, ChangedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }
        await using var headConn = BuildHubConnection(server, await LoginAsync(client, head.Username), onSignal: _ => { });
        await StartAndAwaitConnectedAsync(headConn);
        await headConn.InvokeAsync("WatchRecord", "kpiSetting", own.KpiSetting);
        await headConn.InvokeAsync("WatchRecord", "sectionSelfAssignmentSetting", sec1.Id);
        await Assert.ThrowsAsync<HubException>(() => headConn.InvokeAsync("WatchRecord", "kpiSetting", other.KpiSetting));
        await Assert.ThrowsAsync<HubException>(() => headConn.InvokeAsync("WatchRecord", "sectionSelfAssignmentSetting", sec2.Id));
        await Assert.ThrowsAsync<HubException>(() => bareConn.InvokeAsync("WatchRecord", "kpiSetting", own.KpiSetting));
        await Assert.ThrowsAsync<HubException>(() => headConn.InvokeAsync("WatchRecord", "role", roleId)); // Roles.Manage is not a section head grant

        // The System Administrator holds Roles.Manage, so the same role record is allowed for them.
        var admin = await SeedUserInSectionAsync(RoleType.SystemAdministrator, dept.Id, sec1.Id);
        await using var adminConn = BuildHubConnection(server, await LoginAsync(client, admin.Username), onSignal: _ => { });
        await StartAndAwaitConnectedAsync(adminConn);
        await adminConn.InvokeAsync("WatchRecord", "role", roleId);
    }

    [PostgresFact]
    public async Task WatchRecord_GroupDroppedWhenSectionMembershipRevoked()
    {
        var (dept, sec1, _) = await SeedSectionsAsync();
        var user = await SeedUserInSectionAsync(RoleType.Analyst, dept.Id, sec1.Id);
        var own = await SeedScopedConfigAsync(sec1.Id);

        var (app, client) = await StartLiveServerAsync();
        await using var _ = app;
        var server = app.GetTestServer();

        var signals = new ConcurrentQueue<LiveSignal>();
        await using var conn = BuildHubConnection(server, await LoginAsync(client, user.Username), onSignal: s => signals.Enqueue(s));
        await StartAndAwaitConnectedAsync(conn);
        await conn.InvokeAsync("WatchRecord", "chromatographyColumn", own.Column);

        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IMicroLimsDbContext>();
            db.QueueSignal(LiveSignalKinds.Record, $"chromatographyColumn:{own.Column}");
            await db.SaveChangesAsync();
        }
        await WaitUntilAsync(() => signals.FirstOrDefault(s => s.Key == $"chromatographyColumn:{own.Column}"), "signal while a member");

        // Revoke membership, then let the connection re-evaluate (every WatchRecord does, even a refused one).
        await using (var db = _fixture.CreateDbContext())
        {
            db.UserOrgMemberships.RemoveRange(db.UserOrgMemberships.Where(m => m.UserId == user.Id));
            await db.SaveChangesAsync();
        }
        await Assert.ThrowsAsync<HubException>(() => conn.InvokeAsync("WatchRecord", "chromatographyColumn", own.Column));

        signals.Clear();
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IMicroLimsDbContext>();
            db.QueueSignal(LiveSignalKinds.Record, $"chromatographyColumn:{own.Column}");
            db.QueueSignal(LiveSignalKinds.Notifications, $"user:{user.Id}");
            await db.SaveChangesAsync();
        }
        await WaitUntilAsync(() => signals.FirstOrDefault(s => s.Key == $"user:{user.Id}"), "marker signal");
        Assert.DoesNotContain(signals, s => s.Key == $"chromatographyColumn:{own.Column}");
    }

    [PostgresFact]
    public async Task Signal_PublishedAfterCommit()
    {
        RecordingPublisher? recorder = null;
        var (app, client) = await StartLiveServerAsync(
            overridePublisher: sp =>
            {
                var real = ActivatorUtilities.CreateInstance<SignalRLiveSignalPublisher>(sp);
                recorder = new RecordingPublisher(real);
                return recorder;
            });
        await using var _ = app;

        var (dept, sec1, _) = await SeedSectionsAsync();
        var user = await SeedUserInSectionAsync(RoleType.Analyst, dept.Id, sec1.Id);
        var token = await LoginAsync(client, user.Username);
        var server = app.GetTestServer();

        var received = new ConcurrentQueue<LiveSignal>();
        await using var conn = BuildHubConnection(server, token, onSignal: s => received.Enqueue(s));
        await StartAndAwaitConnectedAsync(conn);

        await using (var scope = app.Services.CreateAsyncScope())
        {
            var actDb = scope.ServiceProvider.GetRequiredService<MicroLimsDbContext>();
            await using var tx = await actDb.Database.BeginTransactionAsync();

            actDb.CurrentUserId = user.Id;
            actDb.QueueSignal(LiveSignalKinds.Notifications, $"user:{user.Id}");
            await actDb.SaveChangesAsync();

            Assert.Empty(recorder!.Batches);
            Assert.Empty(received);

            await tx.CommitAsync();
        }

        var batch = Assert.Single(recorder!.Batches);
        var signal = Assert.Single(batch);
        Assert.Equal(LiveSignalKinds.Notifications, signal.Kind);
        Assert.Equal($"user:{user.Id}", signal.Key);
        Assert.Equal(user.Id, signal.ActorUserId);

        await WaitUntilAsync(() => received.FirstOrDefault(s => s.Key == $"user:{user.Id}"), "committed signal delivered");
    }

    [PostgresFact]
    public async Task Signal_NotPublishedWhenTransactionRollsBack()
    {
        RecordingPublisher? recorder = null;
        var (app, client) = await StartLiveServerAsync(
            overridePublisher: sp =>
            {
                var real = ActivatorUtilities.CreateInstance<SignalRLiveSignalPublisher>(sp);
                recorder = new RecordingPublisher(real);
                return recorder;
            });
        await using var _ = app;

        var (dept, sec1, _) = await SeedSectionsAsync();
        var user = await SeedUserInSectionAsync(RoleType.Analyst, dept.Id, sec1.Id);
        var token = await LoginAsync(client, user.Username);
        var server = app.GetTestServer();

        var received = new ConcurrentQueue<LiveSignal>();
        await using var conn = BuildHubConnection(server, token, onSignal: s => received.Enqueue(s));
        await StartAndAwaitConnectedAsync(conn);

        await using (var scope = app.Services.CreateAsyncScope())
        {
            var actDb = scope.ServiceProvider.GetRequiredService<IMicroLimsDbContext>();
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                UnitOfWork.RunAsync<int>(actDb, async () =>
                {
                    actDb.QueueSignal(LiveSignalKinds.Counters, $"section:{sec1.Id}");
                    await actDb.SaveChangesAsync();
                    throw new InvalidOperationException("Abort UoW");
                }));

            Assert.Empty(recorder!.Batches);

            // The SAME context commits something else afterwards: the rolled-back work's signal must
            // have been discarded, not left queued to go out with this later commit.
            actDb.QueueSignal(LiveSignalKinds.Notifications, $"user:{user.Id}");
            await actDb.SaveChangesAsync();
        }

        await WaitUntilAsync(() => received.FirstOrDefault(s => s.Key == $"user:{user.Id}"), "marker after rollback");
        Assert.DoesNotContain(received, s => s.Key == $"section:{sec1.Id}");
    }

    [PostgresFact]
    public async Task Signal_InsideUnitOfWork_PublishedOnceOnOuterCommit()
    {
        RecordingPublisher? recorder = null;
        var (app, _) = await StartLiveServerAsync(
            overridePublisher: sp =>
            {
                var real = ActivatorUtilities.CreateInstance<SignalRLiveSignalPublisher>(sp);
                recorder = new RecordingPublisher(real);
                return recorder;
            });
        await using var _ = app;

        var (dept, sec1, _) = await SeedSectionsAsync();
        var user = await SeedUserInSectionAsync(RoleType.Analyst, dept.Id, sec1.Id);

        await using (var scope = app.Services.CreateAsyncScope())
        {
            var actDb = scope.ServiceProvider.GetRequiredService<IMicroLimsDbContext>();
            actDb.CurrentUserId = user.Id;

            await UnitOfWork.RunAsync(actDb, async () =>
            {
                actDb.QueueSignal(LiveSignalKinds.Counters, $"section:{sec1.Id}");
                await actDb.SaveChangesAsync();
                Assert.Empty(recorder!.Batches);

                // Same key queued across a second inner save -> coalesced into one entry on outer commit.
                actDb.QueueSignal(LiveSignalKinds.Counters, $"section:{sec1.Id}");
                actDb.QueueSignal(LiveSignalKinds.MyWork, $"user:{user.Id}");
                await actDb.SaveChangesAsync();
                Assert.Empty(recorder.Batches);
            });
        }

        var batch = Assert.Single(recorder!.Batches);
        Assert.Equal(2, batch.Count);
        Assert.Equal(new LiveSignal(LiveSignalKinds.Counters, $"section:{sec1.Id}", user.Id), batch[0]);
        Assert.Equal(new LiveSignal(LiveSignalKinds.MyWork, $"user:{user.Id}", user.Id), batch[1]);
    }

    [PostgresFact]
    public async Task SignalPayload_HasOnlyKindKeyActor()
    {
        var (dept, sec1, _) = await SeedSectionsAsync();
        var user = await SeedUserInSectionAsync(RoleType.Analyst, dept.Id, sec1.Id);

        var (app, client) = await StartLiveServerAsync();
        await using var _ = app;
        var server = app.GetTestServer();

        var token = await LoginAsync(client, user.Username);
        var rawPayloads = new ConcurrentQueue<JsonElement>();
        await using var conn = BuildHubConnection(server, token, onRawSignal: el => rawPayloads.Enqueue(el.Clone()));
        await StartAndAwaitConnectedAsync(conn);

        await using (var scope = app.Services.CreateAsyncScope())
        {
            var actDb = scope.ServiceProvider.GetRequiredService<IMicroLimsDbContext>();
            actDb.CurrentUserId = user.Id;
            actDb.QueueSignal(LiveSignalKinds.Notifications, $"user:{user.Id}");
            await actDb.SaveChangesAsync();
        }

        var json = await WaitUntilAsync(
            () => rawPayloads.TryDequeue(out var el) ? el.GetRawText() : null,
            "raw signal JSON payload");

        using var doc = JsonDocument.Parse(json);
        Assert.Equal(JsonValueKind.Object, doc.RootElement.ValueKind);
        var propNames = doc.RootElement.EnumerateObject().Select(p => p.Name).OrderBy(n => n).ToArray();
        Assert.Equal(new[] { "actorUserId", "key", "kind" }, propNames);
    }

    [PostgresFact]
    public async Task PublisherThrows_RequestStillSucceeds()
    {
        var logCapture = new CapturingLoggerProvider();
        var (app, client) = await StartLiveServerAsync(
            loggerProvider: logCapture,
            overridePublisher: _ => new ThrowingPublisher());
        await using var _ = app;

        var (dept, sec1, _) = await SeedSectionsAsync();
        var user = await SeedUserInSectionAsync(RoleType.Analyst, dept.Id, sec1.Id);

        int notifId;
        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            var log = new NotificationLog
            {
                UserId = user.Id,
                Type = "ReviewWaiting",
                Title = "Sample awaiting review",
                Message = "Sample awaiting review",
                Severity = "info",
                IsRead = false,
                EmailSent = false,
                CreatedAt = DateTime.UtcNow
            };
            arrangeDb.NotificationLogs.Add(log);
            await arrangeDb.SaveChangesAsync();
            notifId = log.Id;
        }

        var token = await LoginAsync(client, user.Username);
        using var req = new HttpRequestMessage(HttpMethod.Post, $"/api/notifications/{notifId}/read");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var resp = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        await using var assertDb = _fixture.CreateDbContext();
        var persisted = await assertDb.NotificationLogs.AsNoTracking().SingleAsync(n => n.Id == notifId);
        Assert.True(persisted.IsRead);
        Assert.Contains(logCapture.Messages, m => m.Contains("Simulated publisher outage", StringComparison.Ordinal));
    }

    [PostgresFact]
    public async Task RevokedSession_ConnectionClosed()
    {
        var clock = new ManualTimeProvider(DateTimeOffset.UtcNow);
        var (app, client) = await StartLiveServerAsync(timeProvider: clock);
        await using var _ = app;
        var server = app.GetTestServer();

        var (dept, sec1, _) = await SeedSectionsAsync();
        var user = await SeedUserInSectionAsync(RoleType.Analyst, dept.Id, sec1.Id);
        var token = await LoginAsync(client, user.Username);

        var closedTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var conn = BuildHubConnection(server, token);
        conn.Closed += _ =>
        {
            closedTcs.TrySetResult();
            return Task.CompletedTask;
        };
        await conn.StartAsync();

        // Revoke the user's session via AccessTokenRevalidator's checks (deactivation / password change).
        await using (var actDb = _fixture.CreateDbContext())
        {
            var row = await actDb.Users.SingleAsync(u => u.Id == user.Id);
            row.IsActive = false;
            await actDb.SaveChangesAsync();
        }

        clock.Advance(TimeSpan.FromMinutes(5));

        var completed = await Task.WhenAny(closedTcs.Task, Task.Delay(5000));
        Assert.Same(closedTcs.Task, completed);

        // A fresh reconnect with the same token is also rejected on OnTokenValidated.
        await using var freshConn = BuildHubConnection(server, token);
        await Assert.ThrowsAnyAsync<Exception>(() => freshConn.StartAsync());
    }

    [PostgresFact]
    public async Task AccessTokenQueryParameter_IgnoredOutsideHubPath()
    {
        var (app, client) = await StartLiveServerAsync();
        await using var _ = app;

        var (dept, sec1, _) = await SeedSectionsAsync();
        var user = await SeedUserInSectionAsync(RoleType.Analyst, dept.Id, sec1.Id);
        var token = await LoginAsync(client, user.Username);

        using var queryOnlyResp = await client.GetAsync($"/api/notifications/summary?access_token={Uri.EscapeDataString(token)}");
        Assert.Equal(HttpStatusCode.Unauthorized, queryOnlyResp.StatusCode);

        using var headerReq = new HttpRequestMessage(HttpMethod.Get, "/api/notifications/summary");
        headerReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var headerResp = await client.SendAsync(headerReq);
        Assert.Equal(HttpStatusCode.OK, headerResp.StatusCode);
    }

    [PostgresFact]
    public async Task RequestLog_ForHubPath_ContainsNoToken()
    {
        var logCapture = new CapturingLoggerProvider();
        var (app, client) = await StartLiveServerAsync(loggerProvider: logCapture);
        await using var _ = app;
        var server = app.GetTestServer();

        var (dept, sec1, _) = await SeedSectionsAsync();
        var user = await SeedUserInSectionAsync(RoleType.Analyst, dept.Id, sec1.Id);
        var token = await LoginAsync(client, user.Username);

        await using var conn = BuildHubConnection(server, token, passTokenInQuery: true);
        await conn.StartAsync();

        Assert.Contains(logCapture.Messages, m => m.Contains("/hubs/live", StringComparison.Ordinal));
        Assert.DoesNotContain(logCapture.Messages, m =>
            m.Contains(token, StringComparison.Ordinal) ||
            m.Contains("access_token", StringComparison.OrdinalIgnoreCase));
    }
}
