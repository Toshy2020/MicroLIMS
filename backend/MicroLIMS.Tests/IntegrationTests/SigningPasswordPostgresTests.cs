using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MicroLIMS.API.Controllers;
using MicroLIMS.API.Extensions;
using MicroLIMS.API.Filters;
using MicroLIMS.API.Json;
using MicroLIMS.API.Middleware;
using MicroLIMS.Application.Abstractions.Security;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Security;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.Mfa;
using MicroLIMS.Application.Services.Sso;
using MicroLIMS.Application.Services.SystemSettings;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Infrastructure.Email;
using MicroLIMS.Infrastructure.Security;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

[Collection("PostgresDatabaseCollection")]
public class SigningPasswordPostgresTests
{
    private const string StrongPassword = "SigningPassword1!";
    private const string SecondStrongPassword = "SigningPassword2!";
    private const string LocalPassword = "LocalUserPass1!";
    private const string JwtSigningKey =
        "MDEyMzQ1Njc4OUFCQ0RFRjAxMjM0NTY3ODlBQkNERUYwMTIzNDU2Nzg5QUJDREVGMDEyMzQ1Njc4OUFCQ0RFRg==";

    private static readonly string FixedKeyBase64 =
        Convert.ToBase64String(Enumerable.Range(1, 32).Select(i => (byte)i).ToArray());

    private static readonly DateTimeOffset BaseUtc = new(2026, 11, 13, 12, 0, 0, TimeSpan.Zero);
    private readonly PostgresTestFixture _fixture;

    public SigningPasswordPostgresTests(PostgresTestFixture fixture) => _fixture = fixture;

    private sealed class FakeTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow;
        public FakeTimeProvider(DateTimeOffset initialUtc) => _utcNow = initialUtc;
        public override DateTimeOffset GetUtcNow() => _utcNow;
        public void Advance(TimeSpan delta) => _utcNow = _utcNow.Add(delta);
    }

    private sealed class StubSystemSettingsReader : ISystemSettingsReader
    {
        private readonly Dictionary<string, string> _strings = new(StringComparer.Ordinal)
        {
            [SystemSettingKeys.MfaPolicy] = nameof(MfaPolicy.Off),
            [SystemSettingKeys.MfaRequiredRoles] = "SystemAdministrator,Reviewer",
            [SystemSettingKeys.SsoMode] = nameof(SsoMode.Off),
        };

        private readonly Dictionary<string, int> _ints = new(StringComparer.Ordinal)
        {
            [SystemSettingKeys.PasswordMinimumLength] = 8,
            [SystemSettingKeys.PasswordHistoryLimit] = 5,
            [SystemSettingKeys.PasswordResetLifetimeMinutes] = 60,
            [SystemSettingKeys.LockoutMaxFailedAttempts] = AuthenticationService.MaxFailedAttempts,
            [SystemSettingKeys.LockoutDurationMinutes] = (int)AuthenticationService.LockDuration.TotalMinutes,
            [SystemSettingKeys.RefreshTokenLifetimeDays] = (int)AuthenticationService.RefreshTokenLifetime.TotalDays,
            [SystemSettingKeys.SignatureMaxFailedAttempts] = 5,
            [SystemSettingKeys.SignatureWindowMinutes] = 15,
            [SystemSettingKeys.MfaTrustedDeviceHours] = 12,
            [SystemSettingKeys.MfaEnrolmentGraceDays] = 7,
        };

        public Task<int> GetIntAsync(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult(_ints[key]);

        public Task<bool> GetBoolAsync(string key, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException($"Unexpected bool setting read: {key}");

        public Task<string> GetStringAsync(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult(_strings[key]);
    }

    private async Task<(WebApplication App, HttpClient Client)> StartHostAsync()
    {
        var cs = _fixture.TestConnectionString;
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Key"] = JwtSigningKey,
            ["Jwt:Issuer"] = "MicroLIMS",
            ["Jwt:Audience"] = "MicroLIMS.Client",
            ["Mfa:ActiveKeyId"] = "test-k1",
            ["Mfa:EncryptionKeys:test-k1"] = FixedKeyBase64,
            ["ConnectionStrings:DefaultConnection"] = cs
        });

        var jwtSettings = JwtConfiguration.Resolve(builder.Configuration, builder.Environment);
        builder.Services.AddSingleton(jwtSettings);
        builder.Services.AddMicroLimsDbContext(opt => opt.UseNpgsql(cs));
        builder.Services.AddApplicationServices(builder.Configuration);
        builder.Services.AddMicroLimsJwtAuthentication(jwtSettings);
        builder.Services.AddMicroLimsAuthorization();
        builder.Services.AddMicroLimsRateLimiting(builder.Configuration);
        builder.Services.AddControllers(options =>
            {
                options.Filters.Add<ValidationFilter>();
                options.Filters.Add<MustChangePasswordFilter>();
            })
            .AddApplicationPart(typeof(AuthenticationController).Assembly)
            .AddJsonOptions(o => ApiJsonOptions.Configure(o.JsonSerializerOptions));

        var app = builder.Build();
        app.UseMiddleware<ExceptionMiddleware>();
        app.UseRouting();
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();

        await app.StartAsync();
        return (app, app.GetTestClient());
    }

    private static (AuthenticationService Auth, UserService Users, ElectronicSignatureService Signatures) CreateServices(
        MicroLimsDbContext db,
        TimeProvider clock,
        ISystemSettingsReader? settings = null)
    {
        var effectiveSettings = settings ?? new StubSystemSettingsReader();
        var policy = new MfaPolicyService(effectiveSettings);
        var audit = new SecurityAuditService(db, new SystemSecurityRequestContext(), clock);
        var ssoMode = new SsoModeService(db, effectiveSettings, audit);
        var email = new EmailSender("", 587, "", "", "no-reply@microlims.local", false);
        AccessTokenIssuer issuer = s => $"jwt-{s.UserId}-{Guid.NewGuid():N}";

        var auth = new AuthenticationService(
            db,
            issuer,
            new PermissionService(db),
            email,
            NullLogger<AuthenticationService>.Instance,
            audit,
            clock,
            effectiveSettings,
            policy,
            ssoMode);

        var signatures = new ElectronicSignatureService(db, clock, effectiveSettings);
        var users = new UserService(db, auth, audit, clock, signatureService: signatures, settings: effectiveSettings, mfaPolicy: policy);
        return (auth, users, signatures);
    }

    private static async Task<int> GetRoleIdAsync(MicroLimsDbContext db, RoleType roleType = RoleType.Analyst) =>
        await db.Roles.AsNoTracking()
            .Where(r => r.Type == roleType)
            .OrderBy(r => r.Id)
            .Select(r => r.Id)
            .FirstAsync();

    private async Task<User> CreateDirectoryUserAsync(
        TimeProvider clock,
        RoleType roleType = RoleType.Analyst,
        string? directoryUsername = null)
    {
        await using var db = _fixture.CreateDbContext();
        var roleId = await GetRoleIdAsync(db, roleType);
        var (_, users, _) = CreateServices(db, clock);
        var tag = Guid.NewGuid().ToString("N");
        var user = new User
        {
            FullName = $"Dir User {tag[..8]}",
            Username = $"dir_u_{tag}",
            Email = $"dir_u_{tag}@corp.test",
            RoleId = roleId,
            IsActive = true,
            SignInMethod = SignInMethod.Directory,
            DirectoryUsername = directoryUsername ?? $"dir.{tag}@corp.test"
        };
        var dto = await users.CreateAsync(user, null, "Provision directory user");
        return await db.Users.AsNoTracking().Include(u => u.Role).SingleAsync(u => u.Id == dto.Id);
    }

    private async Task<User> CreateLocalUserAsync(TimeProvider clock, RoleType roleType = RoleType.Analyst)
    {
        await using var db = _fixture.CreateDbContext();
        var roleId = await GetRoleIdAsync(db, roleType);
        var (_, users, _) = CreateServices(db, clock);
        var tag = Guid.NewGuid().ToString("N");
        var user = new User
        {
            FullName = $"Local User {tag[..8]}",
            Username = $"loc_u_{tag}",
            Email = $"loc_u_{tag}@corp.test",
            RoleId = roleId,
            IsActive = true,
            SignInMethod = SignInMethod.Local
        };
        var dto = await users.CreateAsync(user, LocalPassword, "Provision local user");
        return await db.Users.AsNoTracking().Include(u => u.Role).SingleAsync(u => u.Id == dto.Id);
    }

    [PostgresFact]
    public async Task DirectoryUser_FirstSignIn_ReportsSigningPasswordRequired()
    {
        var clock = new FakeTimeProvider(BaseUtc);
        var dirUser = await CreateDirectoryUserAsync(clock);

        await using var actDb = _fixture.CreateDbContext();
        var (auth, _, _) = CreateServices(actDb, clock);
        var tracked = await actDb.Users.Include(u => u.Role).SingleAsync(u => u.Id == dirUser.Id);
        var outcome = await auth.CompleteLoginAsync(tracked, "127.0.0.1", "TestAgent", mfaVerified: true);

        Assert.True(outcome.Success);
        Assert.True(outcome.SigningPasswordRequired);

        var json = JsonSerializer.Serialize(AuthenticationController.ToLoginResponse(outcome));
        using var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.GetProperty("signingPasswordRequired").GetBoolean());
    }

    [PostgresFact]
    public async Task SetInitial_AppliesPolicy_StoresHash_SetsTimestamp_WritesAuditWithRedactedHash()
    {
        var clock = new FakeTimeProvider(BaseUtc);
        var dirUser = await CreateDirectoryUserAsync(clock);
        var previousHash = dirUser.PasswordHash;

        await using (var actDb = _fixture.CreateDbContext())
        {
            var (auth, _, _) = CreateServices(actDb, clock);
            await auth.SetInitialSigningPasswordAsync(dirUser.Id, StrongPassword);
        }

        await using var assertDb = _fixture.CreateDbContext();
        var reloaded = await assertDb.Users.AsNoTracking().SingleAsync(u => u.Id == dirUser.Id);
        Assert.NotEqual(previousHash, reloaded.PasswordHash);
        Assert.True(BCrypt.Net.BCrypt.Verify(StrongPassword, reloaded.PasswordHash));
        Assert.Equal(BaseUtc.UtcDateTime, reloaded.SigningPasswordSetAt);
        Assert.False(reloaded.MustChangePassword);

        Assert.True(await assertDb.PasswordHistories.AsNoTracking()
            .AnyAsync(h => h.UserId == dirUser.Id && h.PasswordHash == reloaded.PasswordHash));

        Assert.True(await assertDb.SecurityAuditEvents.AsNoTracking()
            .AnyAsync(e => e.TargetUserId == dirUser.Id && e.EventCode == SecurityEventCodes.PasswordChanged && e.Outcome == SecurityEventOutcome.Success));

        var auditRows = await assertDb.AuditLogs.AsNoTracking()
            .Where(a => a.EntityName == nameof(User) && a.EntityId == dirUser.Id.ToString() && a.Action == "Update")
            .OrderByDescending(a => a.Id)
            .ToListAsync();
        Assert.NotEmpty(auditRows);
        var latestAudit = auditRows[0];
        Assert.Contains("[REDACTED]", latestAudit.NewValue);
        Assert.DoesNotContain(StrongPassword, latestAudit.NewValue);
        Assert.DoesNotContain(reloaded.PasswordHash, latestAudit.NewValue);
    }

    [PostgresFact]
    public async Task SetInitial_SecondCall_Throws()
    {
        var clock = new FakeTimeProvider(BaseUtc);
        var dirUser = await CreateDirectoryUserAsync(clock);

        await using (var actDb1 = _fixture.CreateDbContext())
        {
            var (auth, _, _) = CreateServices(actDb1, clock);
            await auth.SetInitialSigningPasswordAsync(dirUser.Id, StrongPassword);
        }

        await using var actDb2 = _fixture.CreateDbContext();
        var (auth2, _, _) = CreateServices(actDb2, clock);
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            auth2.SetInitialSigningPasswordAsync(dirUser.Id, SecondStrongPassword));
    }

    [PostgresFact]
    public async Task SetInitial_LocalUser_Throws()
    {
        var clock = new FakeTimeProvider(BaseUtc);
        var localUser = await CreateLocalUserAsync(clock);

        await using var actDb = _fixture.CreateDbContext();
        var (auth, _, _) = CreateServices(actDb, clock);
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            auth.SetInitialSigningPasswordAsync(localUser.Id, StrongPassword));
    }

    [PostgresFact]
    public async Task Sign_DirectoryUser_WithSigningPassword_Succeeds()
    {
        var clock = new FakeTimeProvider(BaseUtc);
        var dirUser = await CreateDirectoryUserAsync(clock);

        await using (var setupDb = _fixture.CreateDbContext())
        {
            var (auth, _, _) = CreateServices(setupDb, clock);
            await auth.SetInitialSigningPasswordAsync(dirUser.Id, StrongPassword);
        }

        await using (var signDb = _fixture.CreateDbContext())
        {
            var (_, _, signatures) = CreateServices(signDb, clock);
            var sig = await signatures.SignAsync(
                dirUser.Id,
                StrongPassword,
                SignatureMeaning.Reviewed,
                "Sample",
                42,
                "Receiving sample",
                "127.0.0.1");
            await signDb.SaveChangesAsync();
            Assert.True(sig.Id > 0);
        }

        await using var assertDb = _fixture.CreateDbContext();
        Assert.True(await assertDb.ElectronicSignatures.AsNoTracking()
            .AnyAsync(s => s.UserId == dirUser.Id && s.EntityType == "Sample" && s.EntityId == 42));
    }

    [PostgresFact]
    public async Task Sign_DirectoryUser_BeforeSettingSigningPassword_Fails()
    {
        var clock = new FakeTimeProvider(BaseUtc);
        var dirUser = await CreateDirectoryUserAsync(clock);

        await using (var signDb = _fixture.CreateDbContext())
        {
            var (_, _, signatures) = CreateServices(signDb, clock);
            await Assert.ThrowsAsync<SignatureVerificationException>(() =>
                signatures.SignAsync(
                    dirUser.Id,
                    StrongPassword,
                    SignatureMeaning.Reviewed,
                    "Sample",
                    99,
                    "Attempt before signing password set",
                    "127.0.0.1"));
        }

        await using var assertDb = _fixture.CreateDbContext();
        Assert.False(await assertDb.ElectronicSignatures.AsNoTracking().AnyAsync(s => s.UserId == dirUser.Id));
    }

    [PostgresFact]
    public async Task UserMovedFromLocalToDirectory_KeepsPasswordAsSigningPassword_NotAskedAgain()
    {
        var clock = new FakeTimeProvider(BaseUtc);
        var localUser = await CreateLocalUserAsync(clock);

        await using (var actDb = _fixture.CreateDbContext())
        {
            var admin = await CreateLocalUserAsync(clock, RoleType.SystemAdministrator);
            var (_, users, _) = CreateServices(actDb, clock);
            await users.UpdateSsoLinkAsync(
                localUser.Id,
                SignInMethod.Directory,
                $"moved.{Guid.NewGuid():N}@corp.test",
                "Migrate user to SSO",
                admin.Id);
        }

        await using var assertDb = _fixture.CreateDbContext();
        var moved = await assertDb.Users.AsNoTracking().Include(u => u.Role).SingleAsync(u => u.Id == localUser.Id);
        Assert.Equal(SignInMethod.Directory, moved.SignInMethod);
        Assert.NotNull(moved.SigningPasswordSetAt);
        Assert.True(BCrypt.Net.BCrypt.Verify(LocalPassword, moved.PasswordHash));

        var (auth, _, signatures) = CreateServices(assertDb, clock);
        var outcome = await auth.CompleteLoginAsync(moved, "127.0.0.1", "TestAgent", mfaVerified: true);
        Assert.False(outcome.SigningPasswordRequired);

        var sig = await signatures.SignAsync(
            moved.Id,
            LocalPassword,
            SignatureMeaning.Reviewed,
            "Sample",
            77,
            "Signed with kept password",
            "127.0.0.1");
        await assertDb.SaveChangesAsync();
        Assert.True(sig.Id > 0);
    }

    [PostgresFact]
    public async Task SetInitial_KeepsTheCurrentSessionAlive()
    {
        var clock = new FakeTimeProvider(BaseUtc);
        var dirUser = await CreateDirectoryUserAsync(clock);

        string refreshToken;
        await using (var loginDb = _fixture.CreateDbContext())
        {
            var (auth, _, _) = CreateServices(loginDb, clock);
            var tracked = await loginDb.Users.Include(u => u.Role).SingleAsync(u => u.Id == dirUser.Id);
            var outcome = await auth.CompleteLoginAsync(tracked, "127.0.0.1", "TestAgent", mfaVerified: true);
            Assert.True(outcome.SigningPasswordRequired);
            refreshToken = outcome.RefreshToken!;
        }

        await using (var setDb = _fixture.CreateDbContext())
        {
            var (auth, _, _) = CreateServices(setDb, clock);
            await auth.SetInitialSigningPasswordAsync(dirUser.Id, StrongPassword);
        }

        await using (var refreshDb = _fixture.CreateDbContext())
        {
            var (auth, _, _) = CreateServices(refreshDb, clock);
            var refreshed = await auth.RefreshAsync(refreshToken, "127.0.0.1", "TestAgent");
            Assert.True(refreshed.Success);
            Assert.NotNull(refreshed.Token);
            Assert.NotNull(refreshed.RefreshToken);
        }
    }

    [PostgresFact]
    public async Task SetInitial_WeakPassword_IsRejected_NothingStored()
    {
        var clock = new FakeTimeProvider(BaseUtc);
        var dirUser = await CreateDirectoryUserAsync(clock);
        var originalHash = dirUser.PasswordHash;

        await using (var actDb = _fixture.CreateDbContext())
        {
            var (auth, _, _) = CreateServices(actDb, clock);
            await Assert.ThrowsAsync<BusinessRuleException>(() =>
                auth.SetInitialSigningPasswordAsync(dirUser.Id, "weak"));
        }

        await using var assertDb = _fixture.CreateDbContext();
        var reloaded = await assertDb.Users.AsNoTracking().SingleAsync(u => u.Id == dirUser.Id);
        Assert.Equal(originalHash, reloaded.PasswordHash);
        Assert.Null(reloaded.SigningPasswordSetAt);
    }

    [PostgresFact]
    public async Task SigningPasswordEndpoint_WithoutToken_Returns401()
    {
        var (app, client) = await StartHostAsync();
        await using (app)
        {
            var response = await client.PostAsJsonAsync("/api/auth/signing-password", new { newPassword = StrongPassword });
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    [PostgresFact]
    public async Task Me_ReportsSignInMethodAndSigningPasswordSet()
    {
        var clock = new FakeTimeProvider(BaseUtc);
        var dirUser = await CreateDirectoryUserAsync(clock);

        var (app, client) = await StartHostAsync();
        await using (app)
        {
            string accessToken;
            await using (var scope = app.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<MicroLimsDbContext>();
                var auth = scope.ServiceProvider.GetRequiredService<IAuthenticationService>();
                var tracked = await db.Users.Include(u => u.Role).SingleAsync(u => u.Id == dirUser.Id);
                var login = await ((AuthenticationService)auth).CompleteLoginAsync(tracked, "127.0.0.1", "TestAgent", mfaVerified: true);
                accessToken = login.Token!;
            }

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var meBefore = await client.GetAsync("/api/auth/me");
            Assert.Equal(HttpStatusCode.OK, meBefore.StatusCode);
            using (var docBefore = JsonDocument.Parse(await meBefore.Content.ReadAsStringAsync()))
            {
                var data = docBefore.RootElement.GetProperty("data");
                Assert.Equal("Directory", data.GetProperty("signInMethod").GetString());
                Assert.False(data.GetProperty("signingPasswordSet").GetBoolean());
            }

            var setResp = await client.PostAsJsonAsync("/api/auth/signing-password", new { newPassword = StrongPassword });
            Assert.Equal(HttpStatusCode.OK, setResp.StatusCode);

            var meAfter = await client.GetAsync("/api/auth/me");
            Assert.Equal(HttpStatusCode.OK, meAfter.StatusCode);
            using (var docAfter = JsonDocument.Parse(await meAfter.Content.ReadAsStringAsync()))
            {
                var data = docAfter.RootElement.GetProperty("data");
                Assert.Equal("Directory", data.GetProperty("signInMethod").GetString());
                Assert.True(data.GetProperty("signingPasswordSet").GetBoolean());
            }
        }
    }

    [PostgresFact]
    public async Task CreateDirectoryUser_HasUnusablePassword_CannotLogInLocally_EvenWhenModeIsOff()
    {
        var clock = new FakeTimeProvider(BaseUtc);
        var dirUser = await CreateDirectoryUserAsync(clock);
        Assert.False(dirUser.MustChangePassword);
        Assert.False(string.IsNullOrWhiteSpace(dirUser.PasswordHash));
        Assert.Null(dirUser.SigningPasswordSetAt);

        await using var actDb = _fixture.CreateDbContext();
        var (auth, _, _) = CreateServices(actDb, clock, new StubSystemSettingsReader());
        var attempt1 = await auth.LoginAsync(dirUser.Username, string.Empty, "127.0.0.1", "TestAgent");
        var attempt2 = await auth.LoginAsync(dirUser.Username, StrongPassword, "127.0.0.1", "TestAgent");

        Assert.False(attempt1.Success);
        Assert.Null(attempt1.Token);
        Assert.False(attempt2.Success);
        Assert.Null(attempt2.Token);
    }
}
