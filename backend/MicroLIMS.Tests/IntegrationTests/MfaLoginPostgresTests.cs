using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
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
public class MfaLoginPostgresTests
{
    private const string Password = "MfaLoginPass1!";
    private const string GenericMfaFailure = "The code is not correct.";
    private const string GenericLoginFailure = "Invalid username or password.";

    private static readonly DateTimeOffset BaseUtc = new(2026, 11, 13, 10, 0, 0, TimeSpan.Zero);

    private static readonly string FixedKeyBase64 =
        Convert.ToBase64String(Enumerable.Range(1, 32).Select(i => (byte)i).ToArray());

    private readonly PostgresTestFixture _fixture;

    public MfaLoginPostgresTests(PostgresTestFixture fixture) => _fixture = fixture;

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

        public StubSystemSettingsReader WithPolicy(string policy)
        {
            _strings[SystemSettingKeys.MfaPolicy] = policy;
            return this;
        }

        public StubSystemSettingsReader WithLockoutDurationMinutes(int minutes)
        {
            _ints[SystemSettingKeys.LockoutDurationMinutes] = minutes;
            return this;
        }

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

    private static (AuthenticationService Auth, MfaService Mfa) CreateServices(
        MicroLimsDbContext db,
        TimeProvider clock,
        ISystemSettingsReader? settings = null,
        ISecretProtector? protector = null)
    {
        var effectiveSettings = settings ?? new StubSystemSettingsReader();
        var policy = new MfaPolicyService(effectiveSettings);
        var audit = new SecurityAuditService(db, new SystemSecurityRequestContext(), clock);
        IEmailSender email = new EmailSender("", 587, "", "", "no-reply@microlims.local", false);
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
            policy);

        var mfa = new MfaService(
            db,
            auth,
            protector ?? CreateProtector(),
            audit,
            NullLogger<MfaService>.Instance,
            clock);

        return (auth, mfa);
    }

    private static string Sha256Hex(string raw) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));

    private async Task<(int UserId, string Username, byte[] Secret)> SeedUserWithCredentialAsync(
        RoleType roleType = RoleType.Analyst,
        DateTime? mfaRequiredSince = null,
        int stepSeconds = 30)
    {
        var protector = CreateProtector();
        var secret = Totp.NewSecret();
        var protectedSecret = protector.Protect(secret);
        var tag = Guid.NewGuid().ToString("N");
        var username = $"mfa_login_{tag}";

        await using var arrangeDb = _fixture.CreateDbContext();
        var roleId = await arrangeDb.Roles.AsNoTracking()
            .Where(r => r.Type == roleType)
            .OrderBy(r => r.Id)
            .Select(r => r.Id)
            .FirstAsync();

        var user = new User
        {
            FullName = $"MFA User {tag[..8]}",
            Username = username,
            PasswordHash = TestPasswords.Hash(Password),
            RoleId = roleId,
            IsActive = true,
            CreatedAt = BaseUtc.UtcDateTime,
            MfaRequiredSince = mfaRequiredSince
        };
        arrangeDb.Users.Add(user);
        await arrangeDb.SaveChangesAsync();

        arrangeDb.UserMfaCredentials.Add(new UserMfaCredential
        {
            UserId = user.Id,
            Type = MfaCredentialType.AuthenticatorApp,
            SecretProtected = protectedSecret,
            StepSeconds = stepSeconds,
            Status = MfaCredentialStatus.Active,
            CreatedAt = BaseUtc.UtcDateTime,
            ConfirmedAt = BaseUtc.UtcDateTime
        });
        await arrangeDb.SaveChangesAsync();

        return (user.Id, username, secret);
    }

    private async Task<(int UserId, string Username)> SeedUserWithoutCredentialAsync(
        RoleType roleType,
        DateTime? mfaRequiredSince = null)
    {
        var tag = Guid.NewGuid().ToString("N");
        var username = $"mfa_nocred_{tag}";

        await using var arrangeDb = _fixture.CreateDbContext();
        var roleId = await arrangeDb.Roles.AsNoTracking()
            .Where(r => r.Type == roleType)
            .OrderBy(r => r.Id)
            .Select(r => r.Id)
            .FirstAsync();

        var user = new User
        {
            FullName = $"MFA NoCred {tag[..8]}",
            Username = username,
            PasswordHash = TestPasswords.Hash(Password),
            RoleId = roleId,
            IsActive = true,
            CreatedAt = BaseUtc.UtcDateTime,
            MfaRequiredSince = mfaRequiredSince
        };
        arrangeDb.Users.Add(user);
        await arrangeDb.SaveChangesAsync();

        return (user.Id, username);
    }

    private static string CurrentCode(byte[] secret, DateTime utcNow, int stepSeconds = 30) =>
        Totp.Generate(secret, Totp.StepAt(utcNow, stepSeconds));

    private static string DifferentCode(byte[] secret, DateTime utcNow, int stepSeconds = 30)
    {
        var valid = CurrentCode(secret, utcNow, stepSeconds);
        return valid == "000000" ? "111111" : "000000";
    }

    [PostgresFact]
    public async Task Login_UserWithActiveCredential_ReturnsChallenge_NoTokens_NoRefreshTokenRow()
    {
        var (userId, username, _) = await SeedUserWithCredentialAsync();
        var clock = new FakeTimeProvider(BaseUtc);

        LoginOutcome outcome;
        await using (var actDb = _fixture.CreateDbContext(clock))
        {
            var (auth, _) = CreateServices(actDb, clock);
            outcome = await auth.LoginAsync(username, Password);
        }

        Assert.True(outcome.MfaRequired);
        Assert.False(outcome.MfaEnrolmentRequired);
        Assert.NotNull(outcome.MfaToken);
        Assert.Null(outcome.Token);
        Assert.Null(outcome.RefreshToken);

        await using var assertDb = _fixture.CreateDbContext();
        Assert.False(await assertDb.RefreshTokens.AsNoTracking().AnyAsync(r => r.UserId == userId));

        var challenge = await assertDb.MfaChallenges.AsNoTracking().SingleAsync(c => c.UserId == userId);
        Assert.Equal(MfaChallengePurpose.Verify, challenge.Purpose);
        Assert.Equal(Sha256Hex(outcome.MfaToken!), challenge.TokenHash);
        Assert.NotEqual(outcome.MfaToken, challenge.TokenHash);
        Assert.Equal(BaseUtc.UtcDateTime.AddMinutes(5), challenge.ExpiresAt);
        Assert.Null(challenge.ConsumedAt);
        Assert.Equal(0, challenge.FailedAttempts);

        Assert.True(await assertDb.SecurityAuditEvents.AsNoTracking().AnyAsync(e =>
            e.TargetUserId == userId && e.EventCode == SecurityEventCodes.MfaChallengeIssued));
        Assert.False(await assertDb.SecurityAuditEvents.AsNoTracking().AnyAsync(e =>
            e.TargetUserId == userId && e.EventCode == SecurityEventCodes.LoginSuccess));
    }

    [PostgresFact]
    public async Task Verify_CorrectCode_IssuesTokens_SetsMfaVerified_UpdatesLastAcceptedStep_RecordsMfaVerifiedEvent()
    {
        var (userId, username, secret) = await SeedUserWithCredentialAsync();
        var clock = new FakeTimeProvider(BaseUtc);

        LoginOutcome loginOutcome;
        await using (var actDb1 = _fixture.CreateDbContext(clock))
        {
            var (auth, _) = CreateServices(actDb1, clock);
            loginOutcome = await auth.LoginAsync(username, Password);
        }

        var code = CurrentCode(secret, BaseUtc.UtcDateTime);
        LoginOutcome verifyOutcome;
        await using (var actDb2 = _fixture.CreateDbContext(clock))
        {
            var (_, mfa) = CreateServices(actDb2, clock);
            verifyOutcome = await mfa.VerifyAsync(loginOutcome.MfaToken!, code, null, false, "10.1.2.3", "TestAgent/1.0");
        }

        Assert.True(verifyOutcome.Success);
        Assert.NotNull(verifyOutcome.Token);
        Assert.NotNull(verifyOutcome.RefreshToken);
        Assert.Null(verifyOutcome.FailureReason);

        await using var assertDb = _fixture.CreateDbContext();
        var refreshToken = await assertDb.RefreshTokens.AsNoTracking().SingleAsync(r => r.UserId == userId);
        Assert.True(refreshToken.MfaVerified);
        Assert.Equal("10.1.2.3", refreshToken.CreatedIp);

        var credential = await assertDb.UserMfaCredentials.AsNoTracking().SingleAsync(c => c.UserId == userId);
        Assert.Equal(Totp.StepAt(BaseUtc.UtcDateTime, 30), credential.LastAcceptedStep);

        var challenge = await assertDb.MfaChallenges.AsNoTracking().SingleAsync(c => c.UserId == userId);
        Assert.Equal(BaseUtc.UtcDateTime, challenge.ConsumedAt);

        Assert.True(await assertDb.SecurityAuditEvents.AsNoTracking().AnyAsync(e =>
            e.TargetUserId == userId
            && e.EventCode == SecurityEventCodes.MfaVerified
            && e.Outcome == SecurityEventOutcome.Success));
        Assert.True(await assertDb.SecurityAuditEvents.AsNoTracking().AnyAsync(e =>
            e.TargetUserId == userId
            && e.EventCode == SecurityEventCodes.LoginSuccess));
    }

    [PostgresFact]
    public async Task Verify_SameCodeTwice_SecondFails()
    {
        var (userId, username, secret) = await SeedUserWithCredentialAsync();
        var clock = new FakeTimeProvider(BaseUtc);
        var code = CurrentCode(secret, BaseUtc.UtcDateTime);

        LoginOutcome login1;
        await using (var actDb1 = _fixture.CreateDbContext(clock))
        {
            var (auth, _) = CreateServices(actDb1, clock);
            login1 = await auth.LoginAsync(username, Password);
        }

        await using (var actDb2 = _fixture.CreateDbContext(clock))
        {
            var (_, mfa) = CreateServices(actDb2, clock);
            var first = await mfa.VerifyAsync(login1.MfaToken!, code, null, false, null, null);
            Assert.True(first.Success);
        }

        // Second login inside the same 30-second step gets a fresh challenge,
        // then attempts to reuse the same valid 6-digit code.
        LoginOutcome login2;
        await using (var actDb3 = _fixture.CreateDbContext(clock))
        {
            var (auth, _) = CreateServices(actDb3, clock);
            login2 = await auth.LoginAsync(username, Password);
        }

        LoginOutcome second;
        await using (var actDb4 = _fixture.CreateDbContext(clock))
        {
            var (_, mfa) = CreateServices(actDb4, clock);
            second = await mfa.VerifyAsync(login2.MfaToken!, code, null, false, null, null);
        }

        Assert.False(second.Success);
        Assert.Null(second.Token);
        Assert.Null(second.RefreshToken);
        Assert.Equal(GenericMfaFailure, second.FailureReason);

        await using var assertDb = _fixture.CreateDbContext();
        Assert.Equal(1, await assertDb.RefreshTokens.AsNoTracking().CountAsync(r => r.UserId == userId));
        Assert.True(await assertDb.SecurityAuditEvents.AsNoTracking().AnyAsync(e =>
            e.TargetUserId == userId
            && e.EventCode == SecurityEventCodes.MfaFailed
            && e.Outcome == SecurityEventOutcome.Failure));
    }

    [PostgresFact]
    public async Task Verify_WrongCode_IncrementsFailedLoginAttempts_RecordsMfaFailed()
    {
        var (userId, username, secret) = await SeedUserWithCredentialAsync();
        var clock = new FakeTimeProvider(BaseUtc);

        LoginOutcome login;
        await using (var actDb1 = _fixture.CreateDbContext(clock))
        {
            var (auth, _) = CreateServices(actDb1, clock);
            login = await auth.LoginAsync(username, Password);
        }

        var wrongCode = DifferentCode(secret, BaseUtc.UtcDateTime);
        LoginOutcome verify;
        await using (var actDb2 = _fixture.CreateDbContext(clock))
        {
            var (_, mfa) = CreateServices(actDb2, clock);
            verify = await mfa.VerifyAsync(login.MfaToken!, wrongCode, null, false, null, null);
        }

        Assert.False(verify.Success);
        Assert.Null(verify.Token);
        Assert.Null(verify.RefreshToken);
        Assert.Equal(GenericMfaFailure, verify.FailureReason);

        await using var assertDb = _fixture.CreateDbContext();
        var user = await assertDb.Users.AsNoTracking().SingleAsync(u => u.Id == userId);
        Assert.Equal(1, user.FailedLoginAttempts);
        Assert.Null(user.LockedUntil);

        var challenge = await assertDb.MfaChallenges.AsNoTracking().SingleAsync(c => c.UserId == userId);
        Assert.Equal(1, challenge.FailedAttempts);
        Assert.Null(challenge.ConsumedAt);

        Assert.True(await assertDb.SecurityAuditEvents.AsNoTracking().AnyAsync(e =>
            e.TargetUserId == userId
            && e.EventCode == SecurityEventCodes.MfaFailed
            && e.Outcome == SecurityEventOutcome.Failure));
        Assert.False(await assertDb.RefreshTokens.AsNoTracking().AnyAsync(r => r.UserId == userId));
    }

    [PostgresFact]
    public async Task Verify_FifthWrongCode_LocksAccount_AndChallengeIsDead()
    {
        var (userId, username, secret) = await SeedUserWithCredentialAsync();
        var clock = new FakeTimeProvider(BaseUtc);
        var settings = new StubSystemSettingsReader().WithLockoutDurationMinutes(2);

        LoginOutcome login;
        await using (var actDb1 = _fixture.CreateDbContext(clock))
        {
            var (auth, _) = CreateServices(actDb1, clock, settings);
            login = await auth.LoginAsync(username, Password);
        }

        var wrongCode = DifferentCode(secret, BaseUtc.UtcDateTime);
        for (var i = 0; i < 5; i++)
        {
            await using var actDb = _fixture.CreateDbContext(clock);
            var (_, mfa) = CreateServices(actDb, clock, settings);
            var failed = await mfa.VerifyAsync(login.MfaToken!, wrongCode, null, false, null, null);
            Assert.False(failed.Success);
            Assert.Equal(GenericMfaFailure, failed.FailureReason);
        }

        await using (var assertLockedDb = _fixture.CreateDbContext())
        {
            var lockedUser = await assertLockedDb.Users.AsNoTracking().SingleAsync(u => u.Id == userId);
            Assert.Equal(5, lockedUser.FailedLoginAttempts);
            Assert.Equal(BaseUtc.UtcDateTime.AddMinutes(2), lockedUser.LockedUntil);

            var challenge = await assertLockedDb.MfaChallenges.AsNoTracking().SingleAsync(c => c.UserId == userId);
            Assert.Equal(5, challenge.FailedAttempts);

            Assert.True(await assertLockedDb.SecurityAuditEvents.AsNoTracking().AnyAsync(e =>
                e.TargetUserId == userId && e.EventCode == SecurityEventCodes.AccountLocked));
        }

        // Advance past the 2-minute lockout (challenge is still within its 5-minute window):
        // even with the right code, the exhausted challenge must still fail.
        clock.Advance(TimeSpan.FromMinutes(3));
        var validCode = CurrentCode(secret, clock.GetUtcNow().UtcDateTime);

        LoginOutcome afterLockOutcome;
        await using (var actDbAfter = _fixture.CreateDbContext(clock))
        {
            var (_, mfa) = CreateServices(actDbAfter, clock, settings);
            afterLockOutcome = await mfa.VerifyAsync(login.MfaToken!, validCode, null, false, null, null);
        }

        Assert.False(afterLockOutcome.Success);
        Assert.Null(afterLockOutcome.Token);
        Assert.Equal(GenericMfaFailure, afterLockOutcome.FailureReason);
    }

    [PostgresFact]
    public async Task Verify_ExpiredChallenge_Fails()
    {
        var (userId, username, secret) = await SeedUserWithCredentialAsync();
        var clock = new FakeTimeProvider(BaseUtc);

        LoginOutcome login;
        await using (var actDb1 = _fixture.CreateDbContext(clock))
        {
            var (auth, _) = CreateServices(actDb1, clock);
            login = await auth.LoginAsync(username, Password);
        }

        clock.Advance(TimeSpan.FromMinutes(5).Add(TimeSpan.FromSeconds(1)));
        var validCode = CurrentCode(secret, clock.GetUtcNow().UtcDateTime);

        LoginOutcome verify;
        await using (var actDb2 = _fixture.CreateDbContext(clock))
        {
            var (_, mfa) = CreateServices(actDb2, clock);
            verify = await mfa.VerifyAsync(login.MfaToken!, validCode, null, false, null, null);
        }

        Assert.False(verify.Success);
        Assert.Null(verify.Token);
        Assert.Null(verify.RefreshToken);
        Assert.Equal(GenericMfaFailure, verify.FailureReason);

        await using var assertDb = _fixture.CreateDbContext();
        Assert.False(await assertDb.RefreshTokens.AsNoTracking().AnyAsync(r => r.UserId == userId));
    }

    [PostgresFact]
    public async Task Verify_ConsumedChallenge_Fails()
    {
        var (userId, username, secret) = await SeedUserWithCredentialAsync();
        var clock = new FakeTimeProvider(BaseUtc);

        LoginOutcome login;
        await using (var actDb1 = _fixture.CreateDbContext(clock))
        {
            var (auth, _) = CreateServices(actDb1, clock);
            login = await auth.LoginAsync(username, Password);
        }

        var firstCode = CurrentCode(secret, clock.GetUtcNow().UtcDateTime);
        await using (var actDb2 = _fixture.CreateDbContext(clock))
        {
            var (_, mfa) = CreateServices(actDb2, clock);
            var first = await mfa.VerifyAsync(login.MfaToken!, firstCode, null, false, null, null);
            Assert.True(first.Success);
        }

        // Advance into the next TOTP step so the code itself is fresh, isolating the consumed check.
        clock.Advance(TimeSpan.FromSeconds(31));
        var nextStepCode = CurrentCode(secret, clock.GetUtcNow().UtcDateTime);

        LoginOutcome replay;
        await using (var actDb3 = _fixture.CreateDbContext(clock))
        {
            var (_, mfa) = CreateServices(actDb3, clock);
            replay = await mfa.VerifyAsync(login.MfaToken!, nextStepCode, null, false, null, null);
        }

        Assert.False(replay.Success);
        Assert.Null(replay.Token);
        Assert.Null(replay.RefreshToken);
        Assert.Equal(GenericMfaFailure, replay.FailureReason);

        await using var assertDb = _fixture.CreateDbContext();
        Assert.Equal(1, await assertDb.RefreshTokens.AsNoTracking().CountAsync(r => r.UserId == userId));
    }

    [PostgresFact]
    public async Task Login_PolicyOff_UserWithCredential_NoChallenge()
    {
        var (userId, username, _) = await SeedUserWithCredentialAsync();
        var clock = new FakeTimeProvider(BaseUtc);
        var settings = new StubSystemSettingsReader().WithPolicy("Off");

        LoginOutcome outcome;
        await using (var actDb = _fixture.CreateDbContext(clock))
        {
            var (auth, _) = CreateServices(actDb, clock, settings);
            outcome = await auth.LoginAsync(username, Password);
        }

        Assert.True(outcome.Success);
        Assert.NotNull(outcome.Token);
        Assert.NotNull(outcome.RefreshToken);
        Assert.False(outcome.MfaRequired);
        Assert.False(outcome.MfaEnrolmentRequired);
        Assert.Null(outcome.MfaToken);

        await using var assertDb = _fixture.CreateDbContext();
        Assert.False(await assertDb.MfaChallenges.AsNoTracking().AnyAsync(c => c.UserId == userId));
        var refreshToken = await assertDb.RefreshTokens.AsNoTracking().SingleAsync(r => r.UserId == userId);
        Assert.False(refreshToken.MfaVerified);
    }

    [PostgresFact]
    public async Task Login_RequiredRole_NotEnrolled_InGrace_IssuesTokens_WithDueDate_AndSetsMfaRequiredSince()
    {
        var (userId, username) = await SeedUserWithoutCredentialAsync(RoleType.SectionHead, mfaRequiredSince: null);
        var clock = new FakeTimeProvider(BaseUtc);

        LoginOutcome outcome;
        await using (var actDb = _fixture.CreateDbContext(clock))
        {
            var (auth, _) = CreateServices(actDb, clock);
            outcome = await auth.LoginAsync(username, Password);
        }

        Assert.True(outcome.Success);
        Assert.NotNull(outcome.Token);
        Assert.NotNull(outcome.RefreshToken);
        Assert.False(outcome.MfaRequired);
        Assert.False(outcome.MfaEnrolmentRequired);
        Assert.Equal(BaseUtc.UtcDateTime.AddDays(7), outcome.MfaEnrolmentDueAt);

        await using var assertDb = _fixture.CreateDbContext();
        var user = await assertDb.Users.AsNoTracking().SingleAsync(u => u.Id == userId);
        Assert.Equal(BaseUtc.UtcDateTime, user.MfaRequiredSince);

        var refreshToken = await assertDb.RefreshTokens.AsNoTracking().SingleAsync(r => r.UserId == userId);
        Assert.False(refreshToken.MfaVerified);
        Assert.False(await assertDb.MfaChallenges.AsNoTracking().AnyAsync(c => c.UserId == userId));
    }

    [PostgresFact]
    public async Task Login_RequiredRole_NotEnrolled_AfterGrace_ReturnsEnrolmentRequired_NoTokens()
    {
        var expiredSince = BaseUtc.UtcDateTime.AddDays(-8);
        var (userId, username) = await SeedUserWithoutCredentialAsync(RoleType.SectionHead, mfaRequiredSince: expiredSince);
        var clock = new FakeTimeProvider(BaseUtc);

        LoginOutcome outcome;
        await using (var actDb = _fixture.CreateDbContext(clock))
        {
            var (auth, _) = CreateServices(actDb, clock);
            outcome = await auth.LoginAsync(username, Password);
        }

        Assert.True(outcome.MfaEnrolmentRequired);
        Assert.False(outcome.MfaRequired);
        Assert.NotNull(outcome.MfaToken);
        Assert.Null(outcome.Token);
        Assert.Null(outcome.RefreshToken);

        await using var assertDb = _fixture.CreateDbContext();
        Assert.False(await assertDb.RefreshTokens.AsNoTracking().AnyAsync(r => r.UserId == userId));
        var challenge = await assertDb.MfaChallenges.AsNoTracking().SingleAsync(c => c.UserId == userId);
        Assert.Equal(MfaChallengePurpose.Enrol, challenge.Purpose);
        Assert.Equal(Sha256Hex(outcome.MfaToken!), challenge.TokenHash);
    }

    [PostgresFact]
    public async Task Login_WrongPassword_WithActiveCredential_ReturnsNoChallenge()
    {
        var (userId, username, _) = await SeedUserWithCredentialAsync();
        var clock = new FakeTimeProvider(BaseUtc);

        LoginOutcome wrongPasswordOutcome;
        LoginOutcome unknownUserOutcome;
        await using (var actDb = _fixture.CreateDbContext(clock))
        {
            var (auth, _) = CreateServices(actDb, clock);
            wrongPasswordOutcome = await auth.LoginAsync(username, "WrongPassword9!");
            unknownUserOutcome = await auth.LoginAsync($"unknown_{Guid.NewGuid():N}", "WrongPassword9!");
        }

        Assert.False(wrongPasswordOutcome.Success);
        Assert.Null(wrongPasswordOutcome.Token);
        Assert.Null(wrongPasswordOutcome.RefreshToken);
        Assert.False(wrongPasswordOutcome.MfaRequired);
        Assert.False(wrongPasswordOutcome.MfaEnrolmentRequired);
        Assert.Null(wrongPasswordOutcome.MfaToken);
        Assert.Equal(GenericLoginFailure, wrongPasswordOutcome.FailureReason);
        Assert.Equal(unknownUserOutcome.FailureReason, wrongPasswordOutcome.FailureReason);

        await using var assertDb = _fixture.CreateDbContext();
        Assert.False(await assertDb.MfaChallenges.AsNoTracking().AnyAsync(c => c.UserId == userId));
        var user = await assertDb.Users.AsNoTracking().SingleAsync(u => u.Id == userId);
        Assert.Equal(1, user.FailedLoginAttempts);
    }

    [PostgresFact]
    public async Task Verify_ParallelSameCode_OnlyOneSucceeds()
    {
        var (userId, username, secret) = await SeedUserWithCredentialAsync();
        var clock = new FakeTimeProvider(BaseUtc);
        var code = CurrentCode(secret, BaseUtc.UtcDateTime);

        LoginOutcome login1;
        LoginOutcome login2;
        await using (var actDb1 = _fixture.CreateDbContext(clock))
        {
            var (auth, _) = CreateServices(actDb1, clock);
            login1 = await auth.LoginAsync(username, Password);
        }
        await using (var actDb2 = _fixture.CreateDbContext(clock))
        {
            var (auth, _) = CreateServices(actDb2, clock);
            login2 = await auth.LoginAsync(username, Password);
        }

        using var startGate = new Barrier(2);
        var task1 = Task.Run(async () =>
        {
            await using var db = _fixture.CreateDbContext(clock);
            var (_, mfa) = CreateServices(db, clock);
            startGate.SignalAndWait();
            return await mfa.VerifyAsync(login1.MfaToken!, code, null, false, null, null);
        });
        var task2 = Task.Run(async () =>
        {
            await using var db = _fixture.CreateDbContext(clock);
            var (_, mfa) = CreateServices(db, clock);
            startGate.SignalAndWait();
            return await mfa.VerifyAsync(login2.MfaToken!, code, null, false, null, null);
        });

        var results = await Task.WhenAll(task1, task2);
        Assert.Equal(1, results.Count(r => r.Success));
        Assert.Equal(1, results.Count(r => !r.Success && r.FailureReason == GenericMfaFailure));

        await using var assertDb = _fixture.CreateDbContext();
        Assert.Equal(1, await assertDb.RefreshTokens.AsNoTracking().CountAsync(r => r.UserId == userId));
    }

    [PostgresFact]
    public async Task Verify_RecoveryCodeWithoutCode_FailsWithGenericMessage_AndStubsReturnPartBDefaults()
    {
        var (userId, username, _) = await SeedUserWithCredentialAsync();
        var clock = new FakeTimeProvider(BaseUtc);

        LoginOutcome login;
        await using (var actDb1 = _fixture.CreateDbContext(clock))
        {
            var (auth, _) = CreateServices(actDb1, clock);
            login = await auth.LoginAsync(username, Password);
        }

        await using var actDb2 = _fixture.CreateDbContext(clock);
        var (_, mfa) = CreateServices(actDb2, clock);
        var verify = await mfa.VerifyAsync(login.MfaToken!, null, "ABCDE-FGHIJ", false, null, null);

        Assert.False(verify.Success);
        Assert.Equal(GenericMfaFailure, verify.FailureReason);
        Assert.False(await mfa.IsTrustedDeviceAsync(userId, "some-device-token"));
        Assert.Equal(0, await mfa.RevokeTrustedDevicesAsync(userId, userId));
    }
}
