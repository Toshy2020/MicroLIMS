using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MicroLIMS.API.Authorization;
using MicroLIMS.API.Controllers;
using MicroLIMS.API.Extensions;
using MicroLIMS.API.Middleware;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Abstractions.Storage;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Infrastructure.Authentication;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Persistence.Helpers;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

// The ingest endpoint through the real pipeline (authentication, pre-auth
// throttle, authorization, rate limiter, exception middleware, MVC binding).
public class InstrumentIngestControllerTests
{
    private const string KeyA = "lims_agent_AGT-HPLC-01_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string KeyB = "lims_agent_AGT-HPLC-02_bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    private static readonly DateTimeOffset Now = new(2026, 10, 23, 12, 0, 0, TimeSpan.Zero);

    private sealed class NoCapture : IErrorCaptureService
    {
        public Task CaptureAsync(ErrorCaptureRequest request, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class RetryableStorage : IFileStorageService
    {
        public Task<string> SaveAsync(string fileName, byte[] content) => throw new IngestRetryableException("collision");
        public Task<byte[]> ReadAsync(string path) => throw new NotSupportedException();
    }

    private static MicroLimsDbContext NewDb(string name) =>
        new(new DbContextOptionsBuilder<MicroLimsDbContext>().UseInMemoryDatabase(name).Options);

    private static string SeedAgents(bool revokeA = false)
    {
        var name = Guid.NewGuid().ToString();
        using var db = NewDb(name);
        var section = TestServiceFactory.EnsureMicroSection(db);
        db.Equipment.AddRange(
            new Equipment { Id = 11, Name = "HPLC 1", Code = "HPLC-01", Type = EquipmentType.Hplc, SectionId = section.Id },
            new Equipment { Id = 12, Name = "HPLC 2", Code = "HPLC-02", Type = EquipmentType.Hplc, SectionId = section.Id });
        db.InstrumentAgents.AddRange(
            new InstrumentAgent { Id = 1, Code = "AGT-HPLC-01", Name = "PC 1", EquipmentId = 11, SectionId = section.Id, TimeZoneId = "UTC", KeyHash = InstrumentAgentService.HashKey(KeyA), KeyPrefix = "aaaaaaaa", IsActive = !revokeA },
            new InstrumentAgent { Id = 2, Code = "AGT-HPLC-02", Name = "PC 2", EquipmentId = 12, SectionId = section.Id, TimeZoneId = "UTC", KeyHash = InstrumentAgentService.HashKey(KeyB), KeyPrefix = "bbbbbbbb", IsActive = true });
        db.SaveChanges();
        return name;
    }

    // TestServer has no body-size enforcement, so this stands in for Kestrel:
    // it exposes the feature [RequestSizeLimit] sets and refuses reads past it
    // with the same 413 BadHttpRequestException.
    private sealed class KestrelLikeBodyLimit : Microsoft.AspNetCore.Http.Features.IHttpMaxRequestBodySizeFeature
    {
        public bool IsReadOnly => false;
        public long? MaxRequestBodySize { get; set; } = 30_000_000;
    }

    private sealed class LimitedReadStream(Stream inner, Microsoft.AspNetCore.Http.Features.IHttpMaxRequestBodySizeFeature limit) : Stream
    {
        private long _read;
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
        {
            var n = await inner.ReadAsync(buffer, ct);
            _read += n;
            if (limit.MaxRequestBodySize is { } max && _read > max)
                throw new BadHttpRequestException("Request body too large.", StatusCodes.Status413PayloadTooLarge);
            return n;
        }
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken ct) =>
            ReadAsync(buffer.AsMemory(offset, count), ct).AsTask();
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    // Test-only: lets a request choose the client address the way a trusted
    // proxy's forwarded header would.
    private static async Task<(WebApplication App, HttpClient Client)> StartAsync(
        string dbName, IFileStorageService? storage = null, params (string Key, string Value)[] settings)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(settings.Select(s => new KeyValuePair<string, string?>(s.Key, s.Value)));
        builder.Services.AddMicroLimsDbContext(o => o.UseInMemoryDatabase(dbName));
        builder.Services.AddSingleton<TimeProvider>(new FixedTimeProvider(Now));
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<ISecurityRequestContext, HttpSecurityRequestContext>();
        builder.Services.AddScoped<ISecurityAuditService, SecurityAuditService>();
        builder.Services.AddScoped<IDatabaseSequenceHelper, DatabaseSequenceHelper>();
        builder.Services.AddScoped<IAuditEventService, AuditEventService>();
        builder.Services.AddSingleton<IErrorCaptureService, NoCapture>();
        builder.Services.AddSingleton<ILabClock>(sp => new LabClock(sp.GetRequiredService<TimeProvider>(), TimeZoneInfo.Utc));
        builder.Services.AddSingleton<IFileStorageService>(storage ?? new InMemoryFileStorageService());
        builder.Services.AddScoped<InstrumentIngestService>();
        builder.Services.AddMicroLimsJwtAuthentication(new JwtSettings(
            JwtTokenServiceTests.TestSigningKey, "MicroLIMS", "MicroLIMS.Client", TimeSpan.FromMinutes(15)));
        builder.Services.AddMicroLimsAgentAuthentication();
        builder.Services.AddMicroLimsAuthorization();
        builder.Services.AddMicroLimsRateLimiting(builder.Configuration);
        builder.Services.AddControllers().AddApplicationPart(typeof(InstrumentIngestController).Assembly);

        var app = builder.Build();
        app.Use((ctx, next) =>
        {
            if (ctx.Request.Headers.TryGetValue("X-Test-Ip", out var ip)) ctx.Connection.RemoteIpAddress = IPAddress.Parse(ip.ToString());
            return next();
        });
        app.Use((ctx, next) =>
        {
            var limit = new KestrelLikeBodyLimit();
            ctx.Features.Set<Microsoft.AspNetCore.Http.Features.IHttpMaxRequestBodySizeFeature>(limit);
            ctx.Request.Body = new LimitedReadStream(ctx.Request.Body, limit);
            return next();
        });
        app.UseMiddleware<ExceptionMiddleware>();
        app.UseAuthentication();
        app.UseMicroLimsAgentPreAuthThrottle();
        app.UseAuthorization();
        app.UseRateLimiter();
        app.MapControllers();
        await app.StartAsync();
        return (app, app.GetTestClient());
    }

    private static byte[] Report(string text = "report body") => Encoding.UTF8.GetBytes(text);

    private static MultipartFormDataContent Form(byte[]? report = null, string? manifest = null, string sequence = "SEQ-1", string injection = "1")
    {
        report ??= Report();
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(report);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(file, "report", "report.pdf");
        form.Add(new StringContent(manifest ?? InstrumentIngestReceiveTests.ManifestFor(report)), "manifest");
        form.Add(new StringContent(sequence), "sequenceName");
        form.Add(new StringContent(injection), "injectionNumber");
        form.Add(new StringContent("2026-10-01 10:00:00"), "acquiredAtLocalText");
        form.Add(new StringContent(InstrumentIngestReceiveTests.Sha(report)), "reportSha256");
        form.Add(new StringContent("2026-10-23T12:00:00Z"), "agentClockUtc");
        form.Add(new StringContent("analyst1"), "operatorAsReported");
        return form;
    }

    private static Task<HttpResponseMessage> Send(HttpClient client, HttpMethod method, string path,
        string? key = null, string? bearer = null, HttpContent? content = null, string? ip = null)
    {
        var request = new HttpRequestMessage(method, path) { Content = content };
        if (key is not null) request.Headers.TryAddWithoutValidation(AgentKeyDefaults.HeaderName, key);
        if (bearer is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        if (ip is not null) request.Headers.TryAddWithoutValidation("X-Test-Ip", ip);
        return client.SendAsync(request);
    }

    private static Task<HttpResponseMessage> Post(HttpClient client, HttpContent content, string? key = KeyA, string? bearer = null) =>
        Send(client, HttpMethod.Post, "/api/instrument-ingest/receive", key, bearer, content);

    private static JsonElement Data(string body) => JsonDocument.Parse(body).RootElement.GetProperty("data").Clone();

    private static int FailureRows(string dbName)
    {
        using var db = NewDb(dbName);
        return db.SecurityAuditEvents.Count(e => e.EventCode == SecurityEventCodes.AgentAuthFailed);
    }

    [Fact]
    public async Task MultipartRoundTrip_CreatesAVersion_AndReturnsTheReceipt()
    {
        var dbName = SeedAgents();
        var storage = new InMemoryFileStorageService();
        var (app, client) = await StartAsync(dbName, storage);
        await using var _ = app;

        var response = await Post(client, Form());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = Data(await response.Content.ReadAsStringAsync());
        Assert.False(data.GetProperty("alreadyReceived").GetBoolean());
        Assert.Equal(1, data.GetProperty("versionNo").GetInt32());
        using var db = NewDb(dbName);
        var ingest = Assert.Single(db.InstrumentIngests);
        Assert.Equal(11, ingest.EquipmentId);
        Assert.Equal(1, ingest.InstrumentAgentId);
        var version = Assert.Single(db.InstrumentIngestVersions);
        Assert.Equal(InstrumentIngestReceiveTests.Sha(Report()), version.ReportSha256);
        Assert.Equal(Report(), storage.Files[version.ReportStorageKey]);
    }

    [Fact]
    public async Task DuplicatePost_Is200_AlreadyReceived_AndCreatesNothingNew()
    {
        var dbName = SeedAgents();
        var (app, client) = await StartAsync(dbName);
        await using var _ = app;

        Assert.Equal(HttpStatusCode.OK, (await Post(client, Form())).StatusCode);
        var second = await Post(client, Form());

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.True(Data(await second.Content.ReadAsStringAsync()).GetProperty("alreadyReceived").GetBoolean());
        using var db = NewDb(dbName);
        Assert.Single(db.InstrumentIngestVersions);
    }

    [Fact]
    public async Task OversizeBody_Is413()
    {
        var dbName = SeedAgents();
        var (app, client) = await StartAsync(dbName);
        await using var _ = app;

        var response = await Post(client, Form(new byte[InstrumentIngestController.MaxRequestBytes + 1024]));

        Assert.True(HttpStatusCode.RequestEntityTooLarge == response.StatusCode, await response.Content.ReadAsStringAsync());
        using var db = NewDb(dbName);
        Assert.Empty(db.InstrumentIngestVersions);
    }

    [Fact]
    public async Task ManifestOverTwoMegabytes_Is400_AndStoresNothing()
    {
        var dbName = SeedAgents();
        var (app, client) = await StartAsync(dbName);
        await using var _ = app;

        var response = await Post(client, Form(manifest: new string('x', InstrumentIngestService.MaxManifestChars + 1)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var db = NewDb(dbName);
        Assert.Empty(db.InstrumentIngests);
    }

    [Fact]
    public async Task ReportOverTheServiceLimit_Is400()
    {
        var dbName = SeedAgents();
        var (app, client) = await StartAsync(dbName);
        await using var _ = app;

        var response = await Post(client, Form(new byte[InstrumentIngestService.MaxReportBytes + 1]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task BadInput_Is400_WithTheEnvelope()
    {
        var dbName = SeedAgents();
        var (app, client) = await StartAsync(dbName);
        await using var _ = app;
        var noReport = new MultipartFormDataContent { { new StringContent("SEQ"), "sequenceName" } };

        var response = await Post(client, noReport);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("success").GetBoolean());
    }

    [Fact]
    public async Task HumanJwt_NoKey_AndRevokedKey_Are401()
    {
        var dbName = SeedAgents(revokeA: true);
        var (app, client) = await StartAsync(dbName);
        await using var _ = app;
        var token = new JwtTokenService(JwtTokenServiceTests.TestSigningKey, "MicroLIMS", "MicroLIMS.Client", TimeSpan.FromMinutes(15))
            .IssueToken("7", "SystemAdministrator");

        Assert.Equal(HttpStatusCode.Unauthorized, (await Post(client, Form(), key: null, bearer: token)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Post(client, Form(), key: null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Post(client, Form(), key: KeyA)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await Send(client, HttpMethod.Get, "/api/instrument-ingest/receipt/" + new string('a', 64), bearer: token)).StatusCode);
        using var db = NewDb(dbName);
        Assert.Empty(db.InstrumentIngests);
    }

    [Fact]
    public async Task BadKeyFlood_IsThrottledPerIp_AndAuditRowsAreCapped()
    {
        var dbName = SeedAgents();
        var (app, client) = await StartAsync(dbName);
        await using var _ = app;
        const string bad = "lims_agent_AGT-HPLC-01_notTheRealKeyXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX";

        for (var i = 1; i <= 30; i++)
            Assert.Equal(HttpStatusCode.Unauthorized, (await Send(client, HttpMethod.Get, "/api/instrument-ingest/receipt/x", bad, ip: "203.0.113.9")).StatusCode);
        var thirtyFirst = await Send(client, HttpMethod.Get, "/api/instrument-ingest/receipt/x", bad, ip: "203.0.113.9");

        Assert.Equal(HttpStatusCode.TooManyRequests, thirtyFirst.StatusCode);
        Assert.True(thirtyFirst.Headers.Contains("Retry-After"));
        Assert.Equal(AgentAuthFailureAuditLimiter.MaxPerWindow, FailureRows(dbName));

        // Another address has its own allowance, and a valid key from it still works.
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await Send(client, HttpMethod.Get, "/api/instrument-ingest/receipt/x", bad, ip: "203.0.113.10")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Post(client, Form())).StatusCode);
    }

    [Fact]
    public async Task PreAuthThrottle_IsConfigurable_AndIgnoresRequestsWithoutAKey()
    {
        var dbName = SeedAgents();
        var (app, client) = await StartAsync(dbName, null, ("RateLimiting:AgentPreAuth:PermitLimit", "2"));
        await using var _ = app;

        for (var i = 0; i < 5; i++)
            Assert.Equal(HttpStatusCode.Unauthorized, (await Send(client, HttpMethod.Get, "/api/instrument-ingest/receipt/x")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Send(client, HttpMethod.Get, "/api/instrument-ingest/receipt/" + new string('a', 64), KeyA)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Send(client, HttpMethod.Get, "/api/instrument-ingest/receipt/" + new string('a', 64), KeyA)).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await Send(client, HttpMethod.Get, "/api/instrument-ingest/receipt/" + new string('a', 64), KeyA)).StatusCode);
    }

    [Fact]
    public void AuthFailureAuditCap_ResetsWhenTheWindowPasses()
    {
        var time = new FixedTimeProvider(Now);
        var limiter = new AgentAuthFailureAuditLimiter(time);

        for (var i = 0; i < AgentAuthFailureAuditLimiter.MaxPerWindow; i++) Assert.True(limiter.TryAcquire("1.1.1.1"));
        Assert.False(limiter.TryAcquire("1.1.1.1"));
        Assert.True(limiter.TryAcquire("2.2.2.2"));

        time.SetUtcNow(Now.AddSeconds(61));
        Assert.True(limiter.TryAcquire("1.1.1.1"));
    }

    [Fact]
    public async Task Receipt_IsVisibleToTheOwningAgentOnly()
    {
        var dbName = SeedAgents();
        var (app, client) = await StartAsync(dbName);
        await using var _ = app;
        var sha = InstrumentIngestReceiveTests.Sha(Report());
        Assert.Equal(HttpStatusCode.OK, (await Post(client, Form())).StatusCode);

        var mine = await Send(client, HttpMethod.Get, "/api/instrument-ingest/receipt/" + sha.ToUpperInvariant(), KeyA);
        var theirs = await Send(client, HttpMethod.Get, "/api/instrument-ingest/receipt/" + sha, KeyB);
        var unknown = await Send(client, HttpMethod.Get, "/api/instrument-ingest/receipt/" + new string('0', 64), KeyA);
        var malformed = await Send(client, HttpMethod.Get, "/api/instrument-ingest/receipt/not-a-hash", KeyA);

        Assert.Equal(HttpStatusCode.OK, mine.StatusCode);
        var receipts = Data(await mine.Content.ReadAsStringAsync());
        Assert.Equal(1, receipts.GetArrayLength());
        Assert.Equal(1, receipts[0].GetProperty("versionNo").GetInt32());
        Assert.Equal(HttpStatusCode.NotFound, theirs.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);
    }

    [Fact]
    public async Task RetryableCollision_Is503_WithRetryAfter()
    {
        var dbName = SeedAgents();
        var (app, client) = await StartAsync(dbName, new RetryableStorage());
        await using var _ = app;

        var response = await Post(client, Form());

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.True(response.Headers.RetryAfter is not null);
        Assert.Contains("Temporary conflict, retry", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ResolveAgent_RefusesAnAgentThatIsNoLongerActive()
    {
        var dbName = SeedAgents(revokeA: true);
        using var db = NewDb(dbName);
        var service = new InstrumentIngestService(db, new InMemoryFileStorageService(),
            new AuditEventService(db, new DatabaseSequenceHelper(db)), new LabClock(new FixedTimeProvider(Now), TimeZoneInfo.Utc));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ResolveAgentAsync(1, default));
        var ok = await service.ResolveAgentAsync(2, default);
        Assert.Equal(("AGT-HPLC-02", 12, "UTC"), (ok.Code, ok.EquipmentId, ok.TimeZoneId));
    }
}
