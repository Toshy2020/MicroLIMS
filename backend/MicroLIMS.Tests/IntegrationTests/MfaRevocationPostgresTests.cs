using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using MicroLIMS.API.Extensions;
using MicroLIMS.Application.Abstractions.Email;
using MicroLIMS.Application.Abstractions.Security;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Security;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.Mfa;
using MicroLIMS.Application.Services.SystemSettings;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Infrastructure.Email;
using MicroLIMS.Infrastructure.Security;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

[Collection("PostgresDatabaseCollection")]
public class MfaRevocationPostgresTests
{
    private const string Password = "MfaRevokePass1!";
    private const string NewPassword = "MfaRevokePass2@";
    private static readonly DateTimeOffset BaseUtc = new(2026, 11, 13, 12, 0, 0, TimeSpan.Zero);

    private static readonly string FixedKeyBase64 =
        Convert.ToBase64String(Enumerable.Range(1, 32).Select(i => (byte)i).ToArray());

    private readonly PostgresTestFixture _fixture;

    public MfaRevocationPostgresTests(PostgresTestFixture fixture) => _fixture = fixture;

    private sealed class FakeTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow;

        public FakeTimeProvider(DateTimeOffset initialUtc) => _utcNow = initialUtc;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan delta) => _utcNow = _utcNow.Add(delta);
    }

    private sealed class CapturingEmailSender : IEmailSender
    {
        public string? LastBody { get; private set; }

        public Task SendAsync(string toEmail, string subject, string htmlBody)
        {
            LastBody = htmlBody;
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
            [SystemSettingKeys.PasswordMinimumLength] = 10,
            [SystemSettingKeys.PasswordHistoryLimit] = 5,
            [SystemSettingKeys.PasswordResetLifetimeMinutes] = 30,
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

    private static (AuthenticationService Auth, MfaService Mfa, AdminPasswordRecoveryService Recovery, CapturingEmailSender Emails) CreateServices(
        MicroLimsDbContext db,
        TimeProvider clock)
    {
        var settings = new StubSystemSettingsReader();
        var policy = new MfaPolicyService(settings);
        var audit = new SecurityAuditService(db, new SystemSecurityRequestContext(), clock);
        var emails = new CapturingEmailSender();
        AccessTokenIssuer issuer = s => $"jwt-{s.UserId}-{Guid.NewGuid():N}";

        var auth = new AuthenticationService(
            db,
            issuer,
            new PermissionService(db),
            emails,
            NullLogger<AuthenticationService>.Instance,
            audit,
            clock,
            settings,
            policy);

        var mfa = new MfaService(
            db,
            auth,
            CreateProtector(),
            audit,
            NullLogger<MfaService>.Instance,
            policy,
            emails,
            new ZXingQrCodeRenderer(),
            clock);

        var recovery = new AdminPasswordRecoveryService(db, clock, settings, audit);
        return (auth, mfa, recovery, emails);
    }

    private async Task<(int UserId, string Username, byte[] Secret)> SeedEnrolledUserAsync(
        RoleType roleType = RoleType.SectionHead)
    {
        var tag = Guid.NewGuid().ToString("N");
        var username = $"mfa_rev_{tag}";
        var secret = Totp.NewSecret();
        var protectedSecret = CreateProtector().Protect(secret);

        await using var arrangeDb = _fixture.CreateDbContext();
        var roleId = await arrangeDb.Roles.AsNoTracking()
            .Where(r => r.Type == roleType)
            .OrderBy(r => r.Id)
            .Select(r => r.Id)
            .FirstAsync();

        var user = new User
        {
            FullName = $"MFA Rev {tag[..8]}",
            Username = username,
            Email = $"{username}@example.test",
            PasswordHash = TestPasswords.Hash(Password),
            RoleId = roleId,
            IsActive = true,
            CreatedAt = BaseUtc.UtcDateTime
        };
        arrangeDb.Users.Add(user);
        await arrangeDb.SaveChangesAsync();

        arrangeDb.UserMfaCredentials.Add(new UserMfaCredential
        {
            UserId = user.Id,
            Type = MfaCredentialType.AuthenticatorApp,
            SecretProtected = protectedSecret,
            StepSeconds = 30,
            Status = MfaCredentialStatus.Active,
            CreatedAt = BaseUtc.UtcDateTime.AddDays(-1),
            ConfirmedAt = BaseUtc.UtcDateTime.AddDays(-1)
        });
        await arrangeDb.SaveChangesAsync();

        return (user.Id, username, secret);
    }

    private async Task<string> IssueTrustedDeviceAsync(string username, byte[] secret, FakeTimeProvider clock)
    {
        await using var loginDb = _fixture.CreateDbContext();
        var (auth, mfa, _, _) = CreateServices(loginDb, clock);
        var login = await auth.LoginAsync(username, Password, "10.0.0.1", "UA");
        var code = Totp.Generate(secret, Totp.StepAt(clock.GetUtcNow().UtcDateTime, 30));
        var verified = await mfa.VerifyAsync(login.MfaToken!, code, null, trustDevice: true, "10.0.0.1", "UA");
        Assert.True(verified.Success);
        return verified.DeviceToken!;
    }

    [PostgresFact]
    public async Task ChangePassword_RevokesTrustedDevices()
    {
        var (userId, username, secret) = await SeedEnrolledUserAsync();
        var clock = new FakeTimeProvider(BaseUtc);
        var deviceToken = await IssueTrustedDeviceAsync(username, secret, clock);

        clock.Advance(TimeSpan.FromMinutes(10));
        await using (var changeDb = _fixture.CreateDbContext())
        {
            var (auth, _, _, _) = CreateServices(changeDb, clock);
            Assert.True(await auth.ChangePasswordAsync(userId, Password, NewPassword));
        }

        await using (var assertDb = _fixture.CreateDbContext())
        {
            var device = await assertDb.TrustedDevices.AsNoTracking().SingleAsync(d => d.UserId == userId);
            Assert.Equal(clock.GetUtcNow().UtcDateTime, device.RevokedAt);
            Assert.Equal(userId, device.RevokedByUserId);
        }

        await using var nextDb = _fixture.CreateDbContext();
        var (nextAuth, _, _, _) = CreateServices(nextDb, clock);
        var nextLogin = await nextAuth.LoginAsync(username, NewPassword, deviceToken: deviceToken);
        Assert.True(nextLogin.MfaRequired);
        Assert.Null(nextLogin.Token);
    }

    [PostgresFact]
    public async Task PasswordReset_RevokesTrustedDevices_AndNextLoginStillAsksCode()
    {
        var (userId, username, secret) = await SeedEnrolledUserAsync();
        var clock = new FakeTimeProvider(BaseUtc);
        var deviceToken = await IssueTrustedDeviceAsync(username, secret, clock);

        string resetToken;
        await using (var reqDb = _fixture.CreateDbContext())
        {
            var (auth, _, _, emails) = CreateServices(reqDb, clock);
            await auth.RequestPasswordResetAsync(username);
            var marker = "Reset token: ";
            var line = emails.LastBody!.Split('\n').Single(l => l.StartsWith(marker, StringComparison.Ordinal));
            resetToken = line[marker.Length..].Trim();
        }

        clock.Advance(TimeSpan.FromMinutes(5));
        await using (var confirmDb = _fixture.CreateDbContext())
        {
            var (auth, _, _, _) = CreateServices(confirmDb, clock);
            Assert.True(await auth.ConfirmPasswordResetAsync(resetToken, NewPassword));
        }

        await using (var assertDb = _fixture.CreateDbContext())
        {
            var device = await assertDb.TrustedDevices.AsNoTracking().SingleAsync(d => d.UserId == userId);
            Assert.Equal(clock.GetUtcNow().UtcDateTime, device.RevokedAt);
        }

        clock.Advance(TimeSpan.FromMinutes(1));
        await using var nextDb = _fixture.CreateDbContext();
        var (nextAuth, _, _, _) = CreateServices(nextDb, clock);
        var nextLogin = await nextAuth.LoginAsync(username, NewPassword, deviceToken: deviceToken);
        Assert.True(nextLogin.Success);
        Assert.True(nextLogin.MfaRequired);
        Assert.Null(nextLogin.Token);
    }

    [PostgresFact]
    public async Task AdminPasswordRecovery_DoesNotClearSecondFactor_ButRevokesTrustedDevices()
    {
        var (adminId, _, _) = await SeedEnrolledUserAsync(RoleType.SystemAdministrator);
        var (userId, username, secret) = await SeedEnrolledUserAsync(RoleType.SectionHead);
        var clock = new FakeTimeProvider(BaseUtc);
        var deviceToken = await IssueTrustedDeviceAsync(username, secret, clock);

        string recoveryCode;
        await using (var createDb = _fixture.CreateDbContext())
        {
            var (_, _, recovery, _) = CreateServices(createDb, clock);
            var dto = await recovery.CreateRecoveryRequestAsync(userId, "Forgot password", adminId);
            recoveryCode = dto.RecoveryCode;
        }

        clock.Advance(TimeSpan.FromMinutes(5));
        await using (var confirmDb = _fixture.CreateDbContext())
        {
            var (_, _, recovery, _) = CreateServices(confirmDb, clock);
            await recovery.ConfirmRecoveryAsync(username, recoveryCode, NewPassword);
        }

        await using (var assertDb = _fixture.CreateDbContext())
        {
            var cred = await assertDb.UserMfaCredentials.AsNoTracking().SingleAsync(c => c.UserId == userId);
            Assert.Equal(MfaCredentialStatus.Active, cred.Status);

            var device = await assertDb.TrustedDevices.AsNoTracking().SingleAsync(d => d.UserId == userId);
            Assert.Equal(clock.GetUtcNow().UtcDateTime, device.RevokedAt);
        }

        clock.Advance(TimeSpan.FromMinutes(1));
        await using var nextDb = _fixture.CreateDbContext();
        var (nextAuth, _, _, _) = CreateServices(nextDb, clock);
        var nextLogin = await nextAuth.LoginAsync(username, NewPassword, deviceToken: deviceToken);
        Assert.True(nextLogin.MfaRequired);
        Assert.Null(nextLogin.Token);
    }

    [PostgresFact]
    public async Task Startup_PolicyRequired_NoKey_FailsWithMessageNamingMfaActiveKeyId()
    {
        await ScratchPostgresDatabase.RunAsync(async cs =>
        {
            await using var db = ScratchPostgresDatabase.NewContext(cs);
            await db.Database.MigrateAsync();
            db.SystemSettings.Add(new SystemSetting
            {
                Key = SystemSettingKeys.MfaPolicy,
                Value = "RequiredForRoles",
                ValueType = SystemSettingValueType.String,
                Category = "Security",
                IsGxpRelevant = true,
                Description = "MFA policy",
                CurrentVersion = 1
            });
            await db.SaveChangesAsync();

            var reader = new SystemSettingsReader(db, new SystemSettingsCache(), new ConfigurationBuilder().Build(), TimeProvider.System);
            var policy = new MfaPolicyService(reader);
            var unconfiguredProtector = new AesGcmSecretProtector(new MfaOptions());

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                MfaStartupValidator.EnsureValidAsync(db, policy, unconfiguredProtector));
            Assert.Contains("Mfa:ActiveKeyId", ex.Message, StringComparison.Ordinal);
        });
    }

    [PostgresFact]
    public async Task Startup_PolicyOff_NoKey_Starts()
    {
        await ScratchPostgresDatabase.RunAsync(async cs =>
        {
            await using var db = ScratchPostgresDatabase.NewContext(cs);
            await db.Database.MigrateAsync();
            db.SystemSettings.Add(new SystemSetting
            {
                Key = SystemSettingKeys.MfaPolicy,
                Value = "Off",
                ValueType = SystemSettingValueType.String,
                Category = "Security",
                IsGxpRelevant = true,
                Description = "MFA policy",
                CurrentVersion = 1
            });
            await db.SaveChangesAsync();

            var reader = new SystemSettingsReader(db, new SystemSettingsCache(), new ConfigurationBuilder().Build(), TimeProvider.System);
            var policy = new MfaPolicyService(reader);
            var unconfiguredProtector = new AesGcmSecretProtector(new MfaOptions());

            await MfaStartupValidator.EnsureValidAsync(db, policy, unconfiguredProtector);
        });
    }
}
