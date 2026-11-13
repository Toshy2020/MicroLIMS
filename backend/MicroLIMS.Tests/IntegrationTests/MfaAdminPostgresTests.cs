using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
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
using MicroLIMS.Application.Abstractions.Email;
using MicroLIMS.Application.Abstractions.Security;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Security;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.Mfa;
using MicroLIMS.Application.Services.SystemSettings;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Infrastructure.Security;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

[Collection("PostgresDatabaseCollection")]
public class MfaAdminPostgresTests
{
    private const string Password = "MfaAdminPass1!";
    private static readonly DateTimeOffset BaseUtc = new(2026, 11, 13, 12, 0, 0, TimeSpan.Zero);
    private const string JwtSigningKey =
        "MDEyMzQ1Njc4OUFCQ0RFRjAxMjM0NTY3ODlBQkNERUYwMTIzNDU2Nzg5QUJDREVGMDEyMzQ1Njc4OUFCQ0RFRg==";

    private static readonly string FixedKeyBase64 =
        Convert.ToBase64String(Enumerable.Range(1, 32).Select(i => (byte)i).ToArray());

    private readonly PostgresTestFixture _fixture;

    public MfaAdminPostgresTests(PostgresTestFixture fixture) => _fixture = fixture;

    private sealed class FakeTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow;

        public FakeTimeProvider(DateTimeOffset initialUtc) => _utcNow = initialUtc;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan delta) => _utcNow = _utcNow.Add(delta);
    }

    private sealed class SpyEmailSender : IEmailSender
    {
        public List<(string To, string Subject, string Body)> Sent { get; } = [];

        public Task SendAsync(string toEmail, string subject, string htmlBody)
        {
            Sent.Add((toEmail, subject, htmlBody));
            return Task.CompletedTask;
        }
    }

    private sealed class StubSystemSettingsReader : ISystemSettingsReader
    {
        private readonly Dictionary<string, string> _strings = new(StringComparer.Ordinal)
        {
            [SystemSettingKeys.MfaPolicy] = "RequiredForRoles",
            [SystemSettingKeys.MfaRequiredRoles] = "SystemAdministrator,SectionHead,QcManager",
        };

        private readonly Dictionary<string, int> _ints = new(StringComparer.Ordinal)
        {
            [SystemSettingKeys.MfaTrustedDeviceHours] = 12,
            [SystemSettingKeys.MfaEnrolmentGraceDays] = 7,
            [SystemSettingKeys.LockoutMaxFailedAttempts] = AuthenticationService.MaxFailedAttempts,
            [SystemSettingKeys.LockoutDurationMinutes] = (int)AuthenticationService.LockDuration.TotalMinutes,
            [SystemSettingKeys.RefreshTokenLifetimeDays] = (int)AuthenticationService.RefreshTokenLifetime.TotalDays,
        };

        public Task<int> GetIntAsync(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult(_ints[key]);

        public Task<bool> GetBoolAsync(string key, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException($"Unexpected bool setting read: {key}");

        public Task<string> GetStringAsync(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult(_strings[key]);
    }

    private static ISecretProtector CreateProtector() => new AesGcmSecretProtector(new MfaOptions
    {
        ActiveKeyId = "test-k1",
        EncryptionKeys = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["test-k1"] = FixedKeyBase64
        }
    });

    private static (AuthenticationService Auth, MfaService Mfa, MfaAdminService Admin, SpyEmailSender Emails) CreateServices(
        MicroLimsDbContext db,
        TimeProvider clock,
        ISystemSettingsReader? settings = null,
        ISecretProtector? protector = null)
    {
        var effectiveSettings = settings ?? new StubSystemSettingsReader();
        var policy = new MfaPolicyService(effectiveSettings);
        var audit = new SecurityAuditService(db, new SystemSecurityRequestContext(), clock);
        var emails = new SpyEmailSender();
        var effectiveProtector = protector ?? CreateProtector();
        AccessTokenIssuer issuer = s => $"jwt-{s.UserId}-{Guid.NewGuid():N}";

        var auth = new AuthenticationService(
            db,
            issuer,
            new PermissionService(db),
            emails,
            NullLogger<AuthenticationService>.Instance,
            audit,
            clock,
            effectiveSettings,
            policy);

        var mfa = new MfaService(
            db,
            auth,
            effectiveProtector,
            audit,
            NullLogger<MfaService>.Instance,
            policy,
            emails,
            new ZXingQrCodeRenderer(),
            clock);

        var admin = new MfaAdminService(
            db,
            mfa,
            auth,
            effectiveProtector,
            audit,
            policy,
            emails,
            NullLogger<MfaAdminService>.Instance,
            clock);

        return (auth, mfa, admin, emails);
    }

    private async Task<(WebApplication App, HttpClient Client)> StartHostAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Key"] = JwtSigningKey,
            ["Jwt:Issuer"] = "MicroLIMS",
            ["Jwt:Audience"] = "MicroLIMS.Client",
            ["Mfa:ActiveKeyId"] = "test-k1",
            ["Mfa:EncryptionKeys:test-k1"] = FixedKeyBase64,
            ["ConnectionStrings:DefaultConnection"] = _fixture.TestConnectionString
        });

        var jwtSettings = JwtConfiguration.Resolve(builder.Configuration, builder.Environment);
        builder.Services.AddSingleton(jwtSettings);
        builder.Services.AddMicroLimsDbContext(opt => opt.UseNpgsql(_fixture.TestConnectionString));
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

    private async Task<(int UserId, string Username)> SeedUserAsync(
        RoleType roleType = RoleType.Analyst,
        bool isActive = true,
        DateTime? mfaRequiredSince = null)
    {
        var tag = Guid.NewGuid().ToString("N");
        var username = $"mfa_adm_{tag}";

        await using var arrangeDb = _fixture.CreateDbContext();
        var roleId = await arrangeDb.Roles.AsNoTracking()
            .Where(r => r.Type == roleType)
            .OrderBy(r => r.Id)
            .Select(r => r.Id)
            .FirstAsync();

        var user = new User
        {
            FullName = $"MFA Admin Target {tag[..8]}",
            Username = username,
            Email = $"{username}@example.test",
            PasswordHash = TestPasswords.Hash(Password),
            RoleId = roleId,
            IsActive = isActive,
            CreatedAt = BaseUtc.UtcDateTime,
            MfaRequiredSince = mfaRequiredSince
        };
        arrangeDb.Users.Add(user);
        await arrangeDb.SaveChangesAsync();

        return (user.Id, username);
    }

    private async Task<(int UserId, string Username, byte[] Secret)> SeedEnrolledUserAsync(
        RoleType roleType = RoleType.SectionHead)
    {
        var (userId, username) = await SeedUserAsync(roleType);
        var secret = Totp.NewSecret();
        var protectedSecret = CreateProtector().Protect(secret);

        await using var arrangeDb = _fixture.CreateDbContext();
        arrangeDb.UserMfaCredentials.Add(new UserMfaCredential
        {
            UserId = userId,
            Type = MfaCredentialType.AuthenticatorApp,
            SecretProtected = protectedSecret,
            StepSeconds = 30,
            Status = MfaCredentialStatus.Active,
            CreatedAt = BaseUtc.UtcDateTime.AddDays(-2),
            ConfirmedAt = BaseUtc.UtcDateTime.AddDays(-2)
        });
        arrangeDb.MfaRecoveryCodes.AddRange(
            new MfaRecoveryCode
            {
                UserId = userId,
                CodeHash = AdminPasswordRecoveryService.HashRecoveryCode("ABCDE-23456"),
                CreatedAt = BaseUtc.UtcDateTime.AddDays(-2)
            },
            new MfaRecoveryCode
            {
                UserId = userId,
                CodeHash = AdminPasswordRecoveryService.HashRecoveryCode("FGHIJ-78923"),
                CreatedAt = BaseUtc.UtcDateTime.AddDays(-2),
                UsedAt = BaseUtc.UtcDateTime.AddDays(-1)
            });
        await arrangeDb.SaveChangesAsync();

        return (userId, username, secret);
    }

    [PostgresFact]
    public async Task Reset_RevokesCredentialCodesDevicesSessions_WritesAuditRowWithReason()
    {
        var (adminId, _) = await SeedUserAsync(RoleType.SystemAdministrator);
        var (targetId, targetUsername, secret) = await SeedEnrolledUserAsync(RoleType.SectionHead);
        var clock = new FakeTimeProvider(BaseUtc);

        await using (var loginDb = _fixture.CreateDbContext())
        {
            var (auth, mfa, _, _) = CreateServices(loginDb, clock);
            var login = await auth.LoginAsync(targetUsername, Password, "10.0.0.1", "Browser-A");
            Assert.True(login.MfaRequired);
            var code = Totp.Generate(secret, Totp.StepAt(clock.GetUtcNow().UtcDateTime, 30));
            var verified = await mfa.VerifyAsync(login.MfaToken!, code, null, trustDevice: true, "10.0.0.1", "Browser-A");
            Assert.True(verified.Success);
        }

        clock.Advance(TimeSpan.FromMinutes(10));
        const string reason = "Employee lost corporate phone";

        await using (var actDb = _fixture.CreateDbContext())
        {
            var (_, _, admin, emails) = CreateServices(actDb, clock);
            await admin.ResetAsync(targetId, reason, adminId);
            Assert.Single(emails.Sent);
            Assert.Equal($"{targetUsername}@example.test", emails.Sent[0].To);
        }

        await using var assertDb = _fixture.CreateDbContext();
        var creds = await assertDb.UserMfaCredentials.AsNoTracking().Where(c => c.UserId == targetId).ToListAsync();
        Assert.Single(creds);
        Assert.Equal(MfaCredentialStatus.Revoked, creds[0].Status);
        Assert.Equal(clock.GetUtcNow().UtcDateTime, creds[0].RevokedAt);
        Assert.Equal(adminId, creds[0].RevokedByUserId);
        Assert.Equal(reason, creds[0].RevokeReason);

        var unusedCodes = await assertDb.MfaRecoveryCodes.AsNoTracking()
            .Where(r => r.UserId == targetId && r.UsedAt == null)
            .ToListAsync();
        Assert.Empty(unusedCodes);

        var liveDevices = await assertDb.TrustedDevices.AsNoTracking()
            .Where(d => d.UserId == targetId && d.RevokedAt == null)
            .ToListAsync();
        Assert.Empty(liveDevices);

        var liveRefreshTokens = await assertDb.RefreshTokens.AsNoTracking()
            .Where(r => r.UserId == targetId && r.RevokedAt == null)
            .ToListAsync();
        Assert.Empty(liveRefreshTokens);

        var targetUser = await assertDb.Users.AsNoTracking().SingleAsync(u => u.Id == targetId);
        Assert.NotNull(targetUser.MfaRequiredSince);
        Assert.True(targetUser.MfaRequiredSince!.Value <= clock.GetUtcNow().UtcDateTime.AddDays(-7));

        var auditRow = await assertDb.AuditLogs.AsNoTracking()
            .SingleAsync(a => a.Action == "MFA_RESET_BY_ADMIN" && a.EntityId == targetId.ToString());
        Assert.Equal(adminId, auditRow.UserId);
        Assert.Equal(reason, auditRow.Reason);
        Assert.Contains(reason, auditRow.NewValue);

        var secEvt = await assertDb.SecurityAuditEvents.AsNoTracking()
            .SingleAsync(e => e.EventCode == SecurityEventCodes.MfaResetByAdmin && e.TargetUserId == targetId);
        Assert.Equal(adminId, secEvt.ActorUserId);
        Assert.Equal(SecurityEventOutcome.Success, secEvt.Outcome);
        Assert.Equal(reason, secEvt.Reason);
    }

    [PostgresFact]
    public async Task Reset_OwnAccount_Throws()
    {
        var (adminId, _, _) = await SeedEnrolledUserAsync(RoleType.SystemAdministrator);
        var clock = new FakeTimeProvider(BaseUtc);

        await using var actDb = _fixture.CreateDbContext();
        var (_, _, admin, _) = CreateServices(actDb, clock);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            admin.ResetAsync(adminId, "Trying to reset self", adminId));
    }

    [PostgresFact]
    public async Task Reset_EmptyReason_Throws()
    {
        var (adminId, _) = await SeedUserAsync(RoleType.SystemAdministrator);
        var (targetId, _, _) = await SeedEnrolledUserAsync(RoleType.Analyst);
        var clock = new FakeTimeProvider(BaseUtc);

        await using var actDb = _fixture.CreateDbContext();
        var (_, _, admin, _) = CreateServices(actDb, clock);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            admin.ResetAsync(targetId, "   ", adminId));
    }

    [PostgresFact]
    public async Task Reset_DisabledUser_Throws()
    {
        var (adminId, _) = await SeedUserAsync(RoleType.SystemAdministrator);
        var (disabledId, _) = await SeedUserAsync(RoleType.Analyst, isActive: false);
        var clock = new FakeTimeProvider(BaseUtc);

        await using var actDb = _fixture.CreateDbContext();
        var (_, _, admin, _) = CreateServices(actDb, clock);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            admin.ResetAsync(disabledId, "Disabled user reset attempt", adminId));
    }

    [PostgresFact]
    public async Task Reset_WithoutUsersManage_Returns403()
    {
        var (_, analystUsername) = await SeedUserAsync(RoleType.Analyst);
        var (targetId, _, _) = await SeedEnrolledUserAsync(RoleType.SectionHead);
        var (app, client) = await StartHostAsync();
        await using var _ = app;

        var loginResp = await client.PostAsJsonAsync("/api/auth/login", new { username = analystUsername, password = Password });
        Assert.Equal(HttpStatusCode.OK, loginResp.StatusCode);
        using var loginJson = JsonDocument.Parse(await loginResp.Content.ReadAsStringAsync());
        var token = loginJson.RootElement.GetProperty("data").GetProperty("token").GetString();

        using var req = new HttpRequestMessage(HttpMethod.Post, $"/api/admin/users/{targetId}/mfa/reset");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        req.Content = JsonContent.Create(new { reason = "No permission" });
        var resp = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [PostgresFact]
    public async Task AfterReset_NextLogin_RequiresEnrolmentImmediately()
    {
        var (adminId, _) = await SeedUserAsync(RoleType.SystemAdministrator);
        var (targetId, targetUsername, _) = await SeedEnrolledUserAsync(RoleType.SectionHead);
        var clock = new FakeTimeProvider(BaseUtc);

        await using (var actDb = _fixture.CreateDbContext())
        {
            var (_, _, admin, _) = CreateServices(actDb, clock);
            await admin.ResetAsync(targetId, "Reset device", adminId);
        }

        clock.Advance(TimeSpan.FromMinutes(1));
        await using var loginDb = _fixture.CreateDbContext();
        var (auth, _, _, _) = CreateServices(loginDb, clock);
        var outcome = await auth.LoginAsync(targetUsername, Password);

        Assert.True(outcome.Success);
        Assert.True(outcome.MfaEnrolmentRequired);
        Assert.Null(outcome.Token);
        Assert.False(string.IsNullOrWhiteSpace(outcome.MfaToken));
    }

    [PostgresFact]
    public async Task AssignToken_CreatesPendingCredential_HexAndBase32SeedsGiveSameSecret()
    {
        var (adminId, _) = await SeedUserAsync(RoleType.SystemAdministrator);
        var (userAId, _) = await SeedUserAsync(RoleType.Analyst);
        var (userBId, _) = await SeedUserAsync(RoleType.Analyst);
        var clock = new FakeTimeProvider(BaseUtc);
        var protector = CreateProtector();

        byte[] rawSeed = [0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77, 0x88, 0x99, 0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0xFF, 0x01, 0x02, 0x03, 0x04, 0x05];
        var base32Seed = Totp.ToBase32(rawSeed);
        var hexSeed = Convert.ToHexString(rawSeed);

        await using (var actDb = _fixture.CreateDbContext())
        {
            var (_, _, admin, _) = CreateServices(actDb, clock, protector: protector);
            await admin.AssignHardwareTokenAsync(userAId, "SN-B32-001", base32Seed, seedIsHex: false, stepSeconds: 30, "Issued fob A", adminId);
            await admin.AssignHardwareTokenAsync(userBId, "SN-HEX-001", hexSeed, seedIsHex: true, stepSeconds: 60, "Issued fob B", adminId);
        }

        await using var assertDb = _fixture.CreateDbContext();
        var credA = await assertDb.UserMfaCredentials.AsNoTracking().SingleAsync(c => c.UserId == userAId);
        var credB = await assertDb.UserMfaCredentials.AsNoTracking().SingleAsync(c => c.UserId == userBId);

        Assert.Equal(MfaCredentialType.HardwareToken, credA.Type);
        Assert.Equal(MfaCredentialStatus.Pending, credA.Status);
        Assert.Equal("SN-B32-001", credA.SerialNumber);
        Assert.Equal(30, credA.StepSeconds);
        Assert.Equal(adminId, credA.AssignedByUserId);

        Assert.Equal(MfaCredentialType.HardwareToken, credB.Type);
        Assert.Equal(MfaCredentialStatus.Pending, credB.Status);
        Assert.Equal("SN-HEX-001", credB.SerialNumber);
        Assert.Equal(60, credB.StepSeconds);
        Assert.Equal(adminId, credB.AssignedByUserId);

        Assert.Equal(rawSeed, protector.Unprotect(credA.SecretProtected));
        Assert.Equal(rawSeed, protector.Unprotect(credB.SecretProtected));
    }

    [PostgresFact]
    public async Task AssignToken_ToSelf_Throws()
    {
        var (adminId, _) = await SeedUserAsync(RoleType.SystemAdministrator);
        var clock = new FakeTimeProvider(BaseUtc);
        var seed = Totp.ToBase32(Totp.NewSecret());

        await using var actDb = _fixture.CreateDbContext();
        var (_, _, admin, _) = CreateServices(actDb, clock);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            admin.AssignHardwareTokenAsync(adminId, "SN-SELF-1", seed, seedIsHex: false, 30, "Self assign", adminId));
    }

    [PostgresFact]
    public async Task AssignToken_InvalidSeed_Throws_MessageDoesNotEchoSeed()
    {
        var (adminId, _) = await SeedUserAsync(RoleType.SystemAdministrator);
        var (targetId, _) = await SeedUserAsync(RoleType.Analyst);
        var clock = new FakeTimeProvider(BaseUtc);
        const string badSeed = "NOT-VALID-SECRET-XYZ-987654";

        await using var actDb = _fixture.CreateDbContext();
        var (_, _, admin, _) = CreateServices(actDb, clock);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            admin.AssignHardwareTokenAsync(targetId, "SN-BAD-1", badSeed, seedIsHex: false, 30, "Assign", adminId));
        Assert.DoesNotContain(badSeed, ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [PostgresFact]
    public async Task AssignToken_Step45_Throws()
    {
        var (adminId, _) = await SeedUserAsync(RoleType.SystemAdministrator);
        var (targetId, _) = await SeedUserAsync(RoleType.Analyst);
        var clock = new FakeTimeProvider(BaseUtc);
        var seed = Totp.ToBase32(Totp.NewSecret());

        await using var actDb = _fixture.CreateDbContext();
        var (_, _, admin, _) = CreateServices(actDb, clock);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            admin.AssignHardwareTokenAsync(targetId, "SN-45-1", seed, seedIsHex: false, stepSeconds: 45, "Assign", adminId));
    }

    [PostgresFact]
    public async Task AssignedToken_FirstCorrectCode_Activates_AndReturnsRecoveryCodes()
    {
        var (adminId, _) = await SeedUserAsync(RoleType.SystemAdministrator);
        var (targetId, targetUsername) = await SeedUserAsync(RoleType.Analyst);
        var clock = new FakeTimeProvider(BaseUtc);
        var rawSeed = Totp.NewSecret();
        var base32Seed = Totp.ToBase32(rawSeed);

        await using (var assignDb = _fixture.CreateDbContext())
        {
            var (_, _, admin, _) = CreateServices(assignDb, clock);
            await admin.AssignHardwareTokenAsync(targetId, "SN-ACT-1", base32Seed, seedIsHex: false, 60, "Hardware fob", adminId);
        }

        string mfaToken;
        await using (var loginDb = _fixture.CreateDbContext())
        {
            var (auth, _, _, _) = CreateServices(loginDb, clock);
            var login = await auth.LoginAsync(targetUsername, Password);
            Assert.True(login.Success);
            Assert.True(login.MfaRequired);
            Assert.Null(login.Token);
            Assert.False(string.IsNullOrWhiteSpace(login.MfaToken));
            mfaToken = login.MfaToken!;
        }

        await using (var verifyDb = _fixture.CreateDbContext())
        {
            var (_, mfa, _, _) = CreateServices(verifyDb, clock);
            var code = Totp.Generate(rawSeed, Totp.StepAt(clock.GetUtcNow().UtcDateTime, 60));
            var verified = await mfa.VerifyAsync(mfaToken, code, null, trustDevice: false, "10.0.0.2", "UA");
            Assert.True(verified.Success);
            Assert.False(string.IsNullOrWhiteSpace(verified.Token));
            Assert.NotNull(verified.RecoveryCodes);
            Assert.Equal(10, verified.RecoveryCodes!.Count);
        }

        await using var assertDb = _fixture.CreateDbContext();
        var cred = await assertDb.UserMfaCredentials.AsNoTracking().SingleAsync(c => c.UserId == targetId);
        Assert.Equal(MfaCredentialStatus.Active, cred.Status);
        Assert.Equal(clock.GetUtcNow().UtcDateTime, cred.ConfirmedAt);
        Assert.Equal(Totp.StepAt(clock.GetUtcNow().UtcDateTime, 60), cred.LastAcceptedStep);
        Assert.Equal(10, await assertDb.MfaRecoveryCodes.AsNoTracking().CountAsync(r => r.UserId == targetId && r.UsedAt == null));
    }

    [PostgresFact]
    public async Task PendingAuthenticatorAppCredential_DoesNotTriggerVerifyAtLogin()
    {
        var (userId, username) = await SeedUserAsync(RoleType.Analyst);
        var clock = new FakeTimeProvider(BaseUtc);

        await using (var startDb = _fixture.CreateDbContext())
        {
            var (_, mfa, _, _) = CreateServices(startDb, clock);
            await mfa.StartEnrolmentWithPasswordAsync(userId, Password);
        }

        await using var loginDb = _fixture.CreateDbContext();
        var (auth, _, _, _) = CreateServices(loginDb, clock);
        var login = await auth.LoginAsync(username, Password);

        Assert.True(login.Success);
        Assert.False(login.MfaRequired);
        Assert.False(string.IsNullOrWhiteSpace(login.Token));
    }

    [PostgresFact]
    public async Task AssignToken_AuditRow_DoesNotContainSeed()
    {
        var (adminId, _) = await SeedUserAsync(RoleType.SystemAdministrator);
        var (targetId, _) = await SeedUserAsync(RoleType.Analyst);
        var clock = new FakeTimeProvider(BaseUtc);
        var rawSeed = Totp.NewSecret();
        var base32Seed = Totp.ToBase32(rawSeed);
        var hexSeed = Convert.ToHexString(rawSeed);

        await using (var actDb = _fixture.CreateDbContext())
        {
            var (_, _, admin, _) = CreateServices(actDb, clock);
            await admin.AssignHardwareTokenAsync(targetId, "SN-AUDIT-1", base32Seed, seedIsHex: false, 30, "Token issued for lab bench", adminId);
        }

        await using var assertDb = _fixture.CreateDbContext();
        var cred = await assertDb.UserMfaCredentials.AsNoTracking().SingleAsync(c => c.UserId == targetId);
        var forbiddenValues = new[] { base32Seed, hexSeed, hexSeed.ToLowerInvariant(), cred.SecretProtected };

        var auditRows = await assertDb.AuditLogs.AsNoTracking()
            .Where(a => (a.EntityName == nameof(UserMfaCredential) && a.EntityId == cred.Id.ToString())
                     || (a.Action == "MFA_TOKEN_ASSIGNED" && a.EntityId == targetId.ToString()))
            .ToListAsync();
        Assert.NotEmpty(auditRows);
        foreach (var row in auditRows)
        {
            foreach (var secret in forbiddenValues)
            {
                Assert.DoesNotContain(secret, row.PreviousValue ?? string.Empty, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain(secret, row.NewValue ?? string.Empty, StringComparison.OrdinalIgnoreCase);
            }
        }

        var secEvt = await assertDb.SecurityAuditEvents.AsNoTracking()
            .SingleAsync(e => e.EventCode == SecurityEventCodes.MfaTokenAssigned && e.TargetUserId == targetId);
        foreach (var secret in forbiddenValues)
        {
            Assert.DoesNotContain(secret, secEvt.Metadata ?? string.Empty, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(secret, secEvt.Reason ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }
    }

    [PostgresFact]
    public async Task AdminRevokeTrustedDevice_NextLoginAsksCode()
    {
        var (adminId, _) = await SeedUserAsync(RoleType.SystemAdministrator);
        var (targetId, targetUsername, secret) = await SeedEnrolledUserAsync(RoleType.SectionHead);
        var clock = new FakeTimeProvider(BaseUtc);
        string deviceToken;

        await using (var loginDb = _fixture.CreateDbContext())
        {
            var (auth, mfa, _, _) = CreateServices(loginDb, clock);
            var login = await auth.LoginAsync(targetUsername, Password);
            var code = Totp.Generate(secret, Totp.StepAt(clock.GetUtcNow().UtcDateTime, 30));
            var verified = await mfa.VerifyAsync(login.MfaToken!, code, null, trustDevice: true, "10.0.0.3", "UA");
            Assert.True(verified.Success);
            deviceToken = verified.DeviceToken!;
        }

        clock.Advance(TimeSpan.FromMinutes(5));
        await using (var revokeDb = _fixture.CreateDbContext())
        {
            var (_, _, admin, _) = CreateServices(revokeDb, clock);
            var devices = await admin.ListTrustedDevicesAsync(targetId);
            Assert.Single(devices);
            await admin.RevokeTrustedDeviceAsync(targetId, devices[0].Id, adminId);
        }

        clock.Advance(TimeSpan.FromMinutes(1));
        await using var nextLoginDb = _fixture.CreateDbContext();
        var (nextAuth, _, _, _) = CreateServices(nextLoginDb, clock);
        var nextLogin = await nextAuth.LoginAsync(targetUsername, Password, deviceToken: deviceToken);
        Assert.True(nextLogin.Success);
        Assert.True(nextLogin.MfaRequired);
        Assert.Null(nextLogin.Token);
    }

    [PostgresFact]
    public async Task AdminRevokeTrustedDevice_OfAnotherUsersDevice_IsNotFound()
    {
        var (adminId, _) = await SeedUserAsync(RoleType.SystemAdministrator);
        var (userAId, _, _) = await SeedEnrolledUserAsync(RoleType.SectionHead);
        var (userBId, _) = await SeedUserAsync(RoleType.Analyst);
        var clock = new FakeTimeProvider(BaseUtc);

        int deviceId;
        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            var device = new TrustedDevice
            {
                UserId = userAId,
                TokenHash = Convert.ToHexString(SHA256.HashData(Guid.NewGuid().ToByteArray())),
                CreatedAt = clock.GetUtcNow().UtcDateTime,
                ExpiresAt = clock.GetUtcNow().UtcDateTime.AddHours(12)
            };
            arrangeDb.TrustedDevices.Add(device);
            await arrangeDb.SaveChangesAsync();
            deviceId = device.Id;
        }

        await using var actDb = _fixture.CreateDbContext();
        var (_, _, admin, _) = CreateServices(actDb, clock);
        await Assert.ThrowsAsync<NotFoundException>(() =>
            admin.RevokeTrustedDeviceAsync(userBId, deviceId, adminId));
    }
}
