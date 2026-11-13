using System.Text.RegularExpressions;
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
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

[Collection("PostgresDatabaseCollection")]
public class MfaEnrolmentPostgresTests
{
    private const string Password = "MfaEnrolPass1!";
    private static readonly DateTimeOffset BaseUtc = new(2026, 11, 13, 12, 0, 0, TimeSpan.Zero);

    private static readonly string FixedKeyBase64 =
        Convert.ToBase64String(Enumerable.Range(1, 32).Select(i => (byte)i).ToArray());

    private readonly PostgresTestFixture _fixture;

    public MfaEnrolmentPostgresTests(PostgresTestFixture fixture) => _fixture = fixture;

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
            policy,
            email,
            new ZXingQrCodeRenderer(),
            clock);

        return (auth, mfa);
    }

    private async Task<(int UserId, string Username)> SeedUserAsync(
        RoleType roleType = RoleType.Analyst,
        DateTime? mfaRequiredSince = null)
    {
        var tag = Guid.NewGuid().ToString("N");
        var username = $"mfa_enrol_{tag}";

        await using var arrangeDb = _fixture.CreateDbContext();
        var roleId = await arrangeDb.Roles.AsNoTracking()
            .Where(r => r.Type == roleType)
            .OrderBy(r => r.Id)
            .Select(r => r.Id)
            .FirstAsync();

        var user = new User
        {
            FullName = $"MFA Enrol {tag[..8]}",
            Username = username,
            Email = $"{username}@example.test",
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

    private static string CodeFromManualKey(string manualKey, DateTime utcNow, int stepSeconds = 30)
    {
        var secret = Totp.FromBase32(manualKey);
        return Totp.Generate(secret, Totp.StepAt(utcNow, stepSeconds));
    }

    private static string WrongCode(string manualKey, DateTime utcNow, int stepSeconds = 30)
    {
        var real = CodeFromManualKey(manualKey, utcNow, stepSeconds);
        return real == "000000" ? "111111" : "000000";
    }

    [PostgresFact]
    public async Task Start_CreatesPendingCredential_SecretIsEncrypted_NotEqualToManualKey()
    {
        var (userId, username) = await SeedUserAsync();
        var clock = new FakeTimeProvider(BaseUtc);

        EnrolmentStart started;
        await using (var actDb = _fixture.CreateDbContext(clock))
        {
            var (_, mfa) = CreateServices(actDb, clock);
            started = await mfa.StartEnrolmentWithPasswordAsync(userId, Password);
        }

        Assert.False(string.IsNullOrWhiteSpace(started.ManualKey));
        Assert.StartsWith($"otpauth://totp/MicroLIMS:{Uri.EscapeDataString(username)}", started.OtpAuthUri);
        Assert.Contains("<svg", started.QrSvg);
        Assert.Contains("<title>", started.QrSvg);

        await using var assertDb = _fixture.CreateDbContext();
        var cred = await assertDb.UserMfaCredentials.AsNoTracking().SingleAsync(c => c.UserId == userId);
        Assert.Equal(MfaCredentialStatus.Pending, cred.Status);
        Assert.Equal(MfaCredentialType.AuthenticatorApp, cred.Type);
        Assert.Equal(30, cred.StepSeconds);
        Assert.Null(cred.ConfirmedAt);
        Assert.NotEqual(started.ManualKey, cred.SecretProtected);
        Assert.DoesNotContain(started.ManualKey, cred.SecretProtected, StringComparison.Ordinal);

        var roundTripped = Totp.ToBase32(CreateProtector().Unprotect(cred.SecretProtected));
        Assert.Equal(started.ManualKey, roundTripped);

        Assert.True(await assertDb.SecurityAuditEvents.AsNoTracking()
            .AnyAsync(e => e.TargetUserId == userId
                        && e.EventCode == SecurityEventCodes.MfaEnrolmentStarted
                        && e.Outcome == SecurityEventOutcome.Success));
    }

    [PostgresFact]
    public async Task Start_Twice_ReplacesPending_OnlyOnePendingRow()
    {
        var (userId, _) = await SeedUserAsync();
        var clock = new FakeTimeProvider(BaseUtc);

        EnrolmentStart first;
        await using (var actDb1 = _fixture.CreateDbContext(clock))
        {
            var (_, mfa) = CreateServices(actDb1, clock);
            first = await mfa.StartEnrolmentWithPasswordAsync(userId, Password);
        }

        EnrolmentStart second;
        await using (var actDb2 = _fixture.CreateDbContext(clock))
        {
            var (_, mfa) = CreateServices(actDb2, clock);
            second = await mfa.StartEnrolmentWithPasswordAsync(userId, Password);
        }

        Assert.NotEqual(first.ManualKey, second.ManualKey);

        await using var assertDb = _fixture.CreateDbContext();
        var pending = await assertDb.UserMfaCredentials.AsNoTracking()
            .Where(c => c.UserId == userId && c.Status == MfaCredentialStatus.Pending)
            .ToListAsync();
        var only = Assert.Single(pending);
        Assert.Equal(second.ManualKey, Totp.ToBase32(CreateProtector().Unprotect(only.SecretProtected)));
        Assert.False(await assertDb.UserMfaCredentials.AsNoTracking()
            .AnyAsync(c => c.UserId == userId && c.Status == MfaCredentialStatus.Active));
    }

    [PostgresFact]
    public async Task Start_WhileActive_Fails()
    {
        var (userId, _) = await SeedUserAsync();
        var clock = new FakeTimeProvider(BaseUtc);

        await using (var actDb1 = _fixture.CreateDbContext(clock))
        {
            var (_, mfa) = CreateServices(actDb1, clock);
            var started = await mfa.StartEnrolmentWithPasswordAsync(userId, Password);
            await mfa.ConfirmEnrolmentAsync(userId, CodeFromManualKey(started.ManualKey, BaseUtc.UtcDateTime));
        }

        await using (var actDb2 = _fixture.CreateDbContext(clock))
        {
            var (_, mfa) = CreateServices(actDb2, clock);
            await Assert.ThrowsAsync<BusinessRuleException>(() => mfa.StartEnrolmentWithPasswordAsync(userId, Password));
        }

        await using var assertDb = _fixture.CreateDbContext();
        Assert.False(await assertDb.UserMfaCredentials.AsNoTracking()
            .AnyAsync(c => c.UserId == userId && c.Status == MfaCredentialStatus.Pending));
        Assert.Equal(1, await assertDb.UserMfaCredentials.AsNoTracking()
            .CountAsync(c => c.UserId == userId && c.Status == MfaCredentialStatus.Active));
    }

    [PostgresFact]
    public async Task Confirm_CorrectCode_ActivatesCredential_CreatesTenHashedCodes_RecordsMfaEnrolled()
    {
        var (userId, _) = await SeedUserAsync();
        var clock = new FakeTimeProvider(BaseUtc);

        EnrolmentStart started;
        await using (var actDb1 = _fixture.CreateDbContext(clock))
        {
            var (_, mfa) = CreateServices(actDb1, clock);
            started = await mfa.StartEnrolmentWithPasswordAsync(userId, Password);
        }

        var code = CodeFromManualKey(started.ManualKey, BaseUtc.UtcDateTime);
        EnrolmentResult result;
        await using (var actDb2 = _fixture.CreateDbContext(clock))
        {
            var (_, mfa) = CreateServices(actDb2, clock);
            result = await mfa.ConfirmEnrolmentAsync(userId, code);
        }

        Assert.Null(result.Login);
        Assert.Equal(10, result.RecoveryCodes.Count);
        Assert.Equal(10, result.RecoveryCodes.Distinct(StringComparer.Ordinal).Count());
        foreach (var rc in result.RecoveryCodes)
        {
            Assert.Matches(new Regex("^[23456789ABCDEFGHJKLMNPQRSTUVWXYZ]{5}-[23456789ABCDEFGHJKLMNPQRSTUVWXYZ]{5}$"), rc);
        }

        await using var assertDb = _fixture.CreateDbContext();
        var cred = await assertDb.UserMfaCredentials.AsNoTracking().SingleAsync(c => c.UserId == userId);
        Assert.Equal(MfaCredentialStatus.Active, cred.Status);
        Assert.Equal(BaseUtc.UtcDateTime, cred.ConfirmedAt);
        Assert.Equal(Totp.StepAt(BaseUtc.UtcDateTime, 30), cred.LastAcceptedStep);

        var storedCodes = await assertDb.MfaRecoveryCodes.AsNoTracking()
            .Where(r => r.UserId == userId)
            .ToListAsync();
        Assert.Equal(10, storedCodes.Count);
        Assert.All(storedCodes, c => Assert.Null(c.UsedAt));
        var expectedHashes = result.RecoveryCodes.Select(AdminPasswordRecoveryService.HashRecoveryCode).ToHashSet(StringComparer.Ordinal);
        Assert.Equal(expectedHashes, storedCodes.Select(c => c.CodeHash).ToHashSet(StringComparer.Ordinal));
        foreach (var rc in result.RecoveryCodes)
        {
            Assert.DoesNotContain(storedCodes, s => s.CodeHash.Contains(rc, StringComparison.OrdinalIgnoreCase));
        }

        Assert.True(await assertDb.SecurityAuditEvents.AsNoTracking()
            .AnyAsync(e => e.TargetUserId == userId
                        && e.EventCode == SecurityEventCodes.MfaEnrolled
                        && e.Outcome == SecurityEventOutcome.Success));
    }

    [PostgresFact]
    public async Task Confirm_WrongCode_LeavesPending_NoRecoveryCodes()
    {
        var (userId, _) = await SeedUserAsync();
        var clock = new FakeTimeProvider(BaseUtc);

        EnrolmentStart started;
        await using (var actDb1 = _fixture.CreateDbContext(clock))
        {
            var (_, mfa) = CreateServices(actDb1, clock);
            started = await mfa.StartEnrolmentWithPasswordAsync(userId, Password);
        }

        await using (var actDb2 = _fixture.CreateDbContext(clock))
        {
            var (_, mfa) = CreateServices(actDb2, clock);
            await Assert.ThrowsAsync<BusinessRuleException>(() =>
                mfa.ConfirmEnrolmentAsync(userId, WrongCode(started.ManualKey, BaseUtc.UtcDateTime)));
        }

        await using var assertDb = _fixture.CreateDbContext();
        var cred = await assertDb.UserMfaCredentials.AsNoTracking().SingleAsync(c => c.UserId == userId);
        Assert.Equal(MfaCredentialStatus.Pending, cred.Status);
        Assert.Null(cred.ConfirmedAt);
        Assert.False(await assertDb.MfaRecoveryCodes.AsNoTracking().AnyAsync(r => r.UserId == userId));
        Assert.False(await assertDb.SecurityAuditEvents.AsNoTracking()
            .AnyAsync(e => e.TargetUserId == userId && e.EventCode == SecurityEventCodes.MfaEnrolled));
    }

    [PostgresFact]
    public async Task Confirm_LoginTimePath_ConsumesChallenge_IssuesTokensWithMfaVerified()
    {
        var (userId, username) = await SeedUserAsync(RoleType.SectionHead, mfaRequiredSince: BaseUtc.UtcDateTime.AddDays(-8));
        var clock = new FakeTimeProvider(BaseUtc);

        LoginOutcome login;
        await using (var actDb1 = _fixture.CreateDbContext(clock))
        {
            var (auth, _) = CreateServices(actDb1, clock);
            login = await auth.LoginAsync(username, Password, "10.1.0.2", "TestAgent/1.0");
        }

        Assert.True(login.Success);
        Assert.True(login.MfaEnrolmentRequired);
        Assert.NotNull(login.MfaToken);
        Assert.Null(login.Token);
        Assert.Null(login.RefreshToken);

        EnrolmentStart started;
        await using (var actDb2 = _fixture.CreateDbContext(clock))
        {
            var (_, mfa) = CreateServices(actDb2, clock);
            started = await mfa.StartEnrolmentWithTokenAsync(login.MfaToken!);
        }

        EnrolmentResult confirmed;
        await using (var actDb3 = _fixture.CreateDbContext(clock))
        {
            var (_, mfa) = CreateServices(actDb3, clock);
            var code = CodeFromManualKey(started.ManualKey, BaseUtc.UtcDateTime);
            confirmed = await mfa.ConfirmEnrolmentWithTokenAsync(login.MfaToken!, code, "10.1.0.2", "TestAgent/1.0");
        }

        Assert.Equal(10, confirmed.RecoveryCodes.Count);
        Assert.NotNull(confirmed.Login);
        Assert.True(confirmed.Login!.Success);
        Assert.NotNull(confirmed.Login.Token);
        Assert.NotNull(confirmed.Login.RefreshToken);

        await using (var actDb4 = _fixture.CreateDbContext(clock))
        {
            var (_, mfa) = CreateServices(actDb4, clock);
            await Assert.ThrowsAsync<BusinessRuleException>(() => mfa.StartEnrolmentWithTokenAsync(login.MfaToken!));
        }

        await using var assertDb = _fixture.CreateDbContext();
        var cred = await assertDb.UserMfaCredentials.AsNoTracking().SingleAsync(c => c.UserId == userId);
        Assert.Equal(MfaCredentialStatus.Active, cred.Status);

        var challenge = await assertDb.MfaChallenges.AsNoTracking().SingleAsync(c => c.UserId == userId);
        Assert.Equal(MfaChallengePurpose.Enrol, challenge.Purpose);
        Assert.Equal(BaseUtc.UtcDateTime, challenge.ConsumedAt);

        var refresh = await assertDb.RefreshTokens.AsNoTracking().SingleAsync(r => r.UserId == userId);
        Assert.True(refresh.MfaVerified);
        Assert.Equal("10.1.0.2", refresh.CreatedIp);
    }

    [PostgresFact]
    public async Task Start_Authenticated_WrongPassword_Fails()
    {
        var (userId, _) = await SeedUserAsync();
        var clock = new FakeTimeProvider(BaseUtc);

        await using (var actDb = _fixture.CreateDbContext(clock))
        {
            var (_, mfa) = CreateServices(actDb, clock);
            await Assert.ThrowsAsync<BusinessRuleException>(() => mfa.StartEnrolmentWithPasswordAsync(userId, "WrongPassword9!"));
        }

        await using var assertDb = _fixture.CreateDbContext();
        Assert.False(await assertDb.UserMfaCredentials.AsNoTracking().AnyAsync(c => c.UserId == userId));
    }

    [PostgresFact]
    public async Task Regenerate_InvalidatesOldCodes_ReturnsTenNew()
    {
        var (userId, username) = await SeedUserAsync();
        var clock = new FakeTimeProvider(BaseUtc);

        EnrolmentStart started;
        EnrolmentResult first;
        await using (var actDb1 = _fixture.CreateDbContext(clock))
        {
            var (_, mfa) = CreateServices(actDb1, clock);
            started = await mfa.StartEnrolmentWithPasswordAsync(userId, Password);
            first = await mfa.ConfirmEnrolmentAsync(userId, CodeFromManualKey(started.ManualKey, clock.GetUtcNow().UtcDateTime));
        }

        clock.Advance(TimeSpan.FromSeconds(60));

        IReadOnlyList<string> regenerated;
        await using (var actDb2 = _fixture.CreateDbContext(clock))
        {
            var (_, mfa) = CreateServices(actDb2, clock);
            var code = CodeFromManualKey(started.ManualKey, clock.GetUtcNow().UtcDateTime);
            regenerated = await mfa.RegenerateRecoveryCodesAsync(userId, Password, code);
        }

        Assert.Equal(10, regenerated.Count);
        Assert.Empty(first.RecoveryCodes.Intersect(regenerated, StringComparer.Ordinal));

        await using (var assertDb = _fixture.CreateDbContext())
        {
            var storedHashes = await assertDb.MfaRecoveryCodes.AsNoTracking()
                .Where(r => r.UserId == userId)
                .Select(r => r.CodeHash)
                .ToListAsync();
            Assert.Equal(10, storedHashes.Count);
            Assert.Equal(
                regenerated.Select(AdminPasswordRecoveryService.HashRecoveryCode).ToHashSet(StringComparer.Ordinal),
                storedHashes.ToHashSet(StringComparer.Ordinal));
            Assert.True(await assertDb.SecurityAuditEvents.AsNoTracking()
                .AnyAsync(e => e.TargetUserId == userId
                            && e.EventCode == SecurityEventCodes.MfaRecoveryCodesRegenerated
                            && e.Outcome == SecurityEventOutcome.Success));
        }

        LoginOutcome login;
        await using (var actDb3 = _fixture.CreateDbContext(clock))
        {
            var (auth, _) = CreateServices(actDb3, clock);
            login = await auth.LoginAsync(username, Password);
        }

        await using (var actDb4 = _fixture.CreateDbContext(clock))
        {
            var (_, mfa) = CreateServices(actDb4, clock);
            var oldResult = await mfa.VerifyAsync(login.MfaToken!, null, first.RecoveryCodes[0], false, null, null);
            Assert.False(oldResult.Success);
            var newResult = await mfa.VerifyAsync(login.MfaToken!, null, regenerated[0], false, null, null);
            Assert.True(newResult.Success);
            Assert.Equal(9, newResult.RecoveryCodesRemaining);
        }
    }

    [PostgresFact]
    public async Task Regenerate_WrongCode_Fails()
    {
        var (userId, _) = await SeedUserAsync();
        var clock = new FakeTimeProvider(BaseUtc);

        EnrolmentStart started;
        EnrolmentResult first;
        await using (var actDb1 = _fixture.CreateDbContext(clock))
        {
            var (_, mfa) = CreateServices(actDb1, clock);
            started = await mfa.StartEnrolmentWithPasswordAsync(userId, Password);
            first = await mfa.ConfirmEnrolmentAsync(userId, CodeFromManualKey(started.ManualKey, clock.GetUtcNow().UtcDateTime));
        }

        clock.Advance(TimeSpan.FromSeconds(60));

        await using (var actDb2 = _fixture.CreateDbContext(clock))
        {
            var (_, mfa) = CreateServices(actDb2, clock);
            var badCode = WrongCode(started.ManualKey, clock.GetUtcNow().UtcDateTime);
            await Assert.ThrowsAsync<BusinessRuleException>(() =>
                mfa.RegenerateRecoveryCodesAsync(userId, Password, badCode));
        }

        await using var assertDb = _fixture.CreateDbContext();
        var storedHashes = await assertDb.MfaRecoveryCodes.AsNoTracking()
            .Where(r => r.UserId == userId)
            .Select(r => r.CodeHash)
            .ToListAsync();
        Assert.Equal(
            first.RecoveryCodes.Select(AdminPasswordRecoveryService.HashRecoveryCode).ToHashSet(StringComparer.Ordinal),
            storedHashes.ToHashSet(StringComparer.Ordinal));
    }

    [PostgresFact]
    public async Task RemoveOwnFactor_WhenRoleRequired_IsRejected()
    {
        var (userId, _) = await SeedUserAsync(RoleType.SectionHead);
        var clock = new FakeTimeProvider(BaseUtc);

        EnrolmentStart started;
        await using (var actDb1 = _fixture.CreateDbContext(clock))
        {
            var (_, mfa) = CreateServices(actDb1, clock);
            started = await mfa.StartEnrolmentWithPasswordAsync(userId, Password);
            await mfa.ConfirmEnrolmentAsync(userId, CodeFromManualKey(started.ManualKey, clock.GetUtcNow().UtcDateTime));
        }

        clock.Advance(TimeSpan.FromSeconds(60));

        await using (var actDb2 = _fixture.CreateDbContext(clock))
        {
            var (_, mfa) = CreateServices(actDb2, clock);
            var code = CodeFromManualKey(started.ManualKey, clock.GetUtcNow().UtcDateTime);
            await Assert.ThrowsAsync<BusinessRuleException>(() => mfa.RemoveOwnFactorAsync(userId, Password, code));
        }

        await using var assertDb = _fixture.CreateDbContext();
        var cred = await assertDb.UserMfaCredentials.AsNoTracking().SingleAsync(c => c.UserId == userId);
        Assert.Equal(MfaCredentialStatus.Active, cred.Status);
    }

    [PostgresFact]
    public async Task RemoveOwnFactor_WhenOptional_RevokesCredentialCodesAndDevices_RecordsEvent()
    {
        var (userId, username) = await SeedUserAsync(RoleType.Analyst);
        var clock = new FakeTimeProvider(BaseUtc);

        EnrolmentStart started;
        await using (var actDb1 = _fixture.CreateDbContext(clock))
        {
            var (_, mfa) = CreateServices(actDb1, clock);
            started = await mfa.StartEnrolmentWithPasswordAsync(userId, Password);
            await mfa.ConfirmEnrolmentAsync(userId, CodeFromManualKey(started.ManualKey, clock.GetUtcNow().UtcDateTime));
        }

        clock.Advance(TimeSpan.FromSeconds(60));

        await using (var actDb2 = _fixture.CreateDbContext(clock))
        {
            var (auth, mfa) = CreateServices(actDb2, clock);
            var login = await auth.LoginAsync(username, Password);
            var verify = await mfa.VerifyAsync(login.MfaToken!, CodeFromManualKey(started.ManualKey, clock.GetUtcNow().UtcDateTime), null, trustDevice: true, "10.0.0.7", "Agent");
            Assert.True(verify.Success);
            Assert.NotNull(verify.DeviceToken);

            var statusBefore = await mfa.GetStatusAsync(userId);
            Assert.True(statusBefore.Enrolled);
            Assert.Equal("AuthenticatorApp", statusBefore.Type);
            Assert.Equal(10, statusBefore.RecoveryCodesRemaining);
            Assert.False(statusBefore.Required);
            Assert.Single(statusBefore.TrustedDevices);
        }

        clock.Advance(TimeSpan.FromSeconds(60));

        await using (var actDb3 = _fixture.CreateDbContext(clock))
        {
            var (_, mfa) = CreateServices(actDb3, clock);
            var code = CodeFromManualKey(started.ManualKey, clock.GetUtcNow().UtcDateTime);
            await mfa.RemoveOwnFactorAsync(userId, Password, code);

            var statusAfter = await mfa.GetStatusAsync(userId);
            Assert.False(statusAfter.Enrolled);
            Assert.Equal(0, statusAfter.RecoveryCodesRemaining);
            Assert.Empty(statusAfter.TrustedDevices);
        }

        await using var assertDb = _fixture.CreateDbContext();
        var cred = await assertDb.UserMfaCredentials.AsNoTracking().SingleAsync(c => c.UserId == userId);
        Assert.Equal(MfaCredentialStatus.Revoked, cred.Status);
        Assert.Equal(clock.GetUtcNow().UtcDateTime, cred.RevokedAt);
        Assert.Equal(userId, cred.RevokedByUserId);

        Assert.False(await assertDb.MfaRecoveryCodes.AsNoTracking()
            .AnyAsync(r => r.UserId == userId && r.UsedAt == null));

        var device = await assertDb.TrustedDevices.AsNoTracking().SingleAsync(d => d.UserId == userId);
        Assert.Equal(clock.GetUtcNow().UtcDateTime, device.RevokedAt);
        Assert.Equal(userId, device.RevokedByUserId);

        Assert.True(await assertDb.SecurityAuditEvents.AsNoTracking()
            .AnyAsync(e => e.TargetUserId == userId
                        && e.EventCode == SecurityEventCodes.MfaRemovedByUser
                        && e.Outcome == SecurityEventOutcome.Success));
    }

    [PostgresFact]
    public async Task ForgetTrustedDevice_OfAnotherUser_IsRejected()
    {
        var (userAId, _) = await SeedUserAsync();
        var (userBId, _) = await SeedUserAsync();
        var clock = new FakeTimeProvider(BaseUtc);

        int deviceId;
        await using (var arrangeDb = _fixture.CreateDbContext(clock))
        {
            var device = new TrustedDevice
            {
                UserId = userAId,
                TokenHash = new string('A', 64),
                CreatedAt = BaseUtc.UtcDateTime,
                ExpiresAt = BaseUtc.UtcDateTime.AddHours(12)
            };
            arrangeDb.TrustedDevices.Add(device);
            await arrangeDb.SaveChangesAsync();
            deviceId = device.Id;
        }

        await using (var actDb = _fixture.CreateDbContext(clock))
        {
            var (_, mfa) = CreateServices(actDb, clock);
            var exOther = await Assert.ThrowsAsync<NotFoundException>(() => mfa.ForgetOwnTrustedDeviceAsync(userBId, deviceId));
            var exMissing = await Assert.ThrowsAsync<NotFoundException>(() => mfa.ForgetOwnTrustedDeviceAsync(userBId, int.MaxValue));
            Assert.Equal(exMissing.Message, exOther.Message);
        }

        await using (var assertDb1 = _fixture.CreateDbContext())
        {
            var device = await assertDb1.TrustedDevices.AsNoTracking().SingleAsync(d => d.Id == deviceId);
            Assert.Null(device.RevokedAt);
        }

        await using (var actDb2 = _fixture.CreateDbContext(clock))
        {
            var (_, mfa) = CreateServices(actDb2, clock);
            await mfa.ForgetOwnTrustedDeviceAsync(userAId, deviceId);
        }

        await using var assertDb2 = _fixture.CreateDbContext();
        var revoked = await assertDb2.TrustedDevices.AsNoTracking().SingleAsync(d => d.Id == deviceId);
        Assert.Equal(BaseUtc.UtcDateTime, revoked.RevokedAt);
        Assert.Equal(userAId, revoked.RevokedByUserId);
    }

    [PostgresFact]
    public async Task AuditLogs_ForCredentialInsert_ContainRedactedSecret()
    {
        var (userId, _) = await SeedUserAsync();
        var clock = new FakeTimeProvider(BaseUtc);

        EnrolmentStart started;
        EnrolmentResult confirmed;
        await using (var actDb = _fixture.CreateDbContext(clock))
        {
            var (_, mfa) = CreateServices(actDb, clock);
            started = await mfa.StartEnrolmentWithPasswordAsync(userId, Password);
            confirmed = await mfa.ConfirmEnrolmentAsync(userId, CodeFromManualKey(started.ManualKey, BaseUtc.UtcDateTime));
        }

        await using var assertDb = _fixture.CreateDbContext();
        var cred = await assertDb.UserMfaCredentials.AsNoTracking().SingleAsync(c => c.UserId == userId);

        var credLogs = await assertDb.AuditLogs.AsNoTracking()
            .Where(a => a.EntityName == nameof(UserMfaCredential) && a.EntityId == cred.Id.ToString())
            .ToListAsync();
        var insertLog = Assert.Single(credLogs, l => l.Action == "Create");
        Assert.Contains("\"SecretProtected\":\"[REDACTED]\"", insertLog.NewValue);
        Assert.DoesNotContain(cred.SecretProtected, insertLog.NewValue ?? string.Empty, StringComparison.Ordinal);
        Assert.DoesNotContain(started.ManualKey, insertLog.NewValue ?? string.Empty, StringComparison.Ordinal);

        var codeLogs = await assertDb.AuditLogs.AsNoTracking()
            .Where(a => a.EntityName == nameof(MfaRecoveryCode) && a.Action == "Create")
            .ToListAsync();
        Assert.NotEmpty(codeLogs);
        foreach (var rc in confirmed.RecoveryCodes)
        {
            Assert.DoesNotContain(codeLogs, l => (l.NewValue ?? string.Empty).Contains(rc, StringComparison.OrdinalIgnoreCase));
        }
    }

    [PostgresFact]
    public async Task Confirm_SameCodeCannotThenBeUsedToLogin()
    {
        var (userId, username) = await SeedUserAsync();
        var clock = new FakeTimeProvider(BaseUtc);

        EnrolmentStart started;
        var code = string.Empty;
        await using (var actDb1 = _fixture.CreateDbContext(clock))
        {
            var (_, mfa) = CreateServices(actDb1, clock);
            started = await mfa.StartEnrolmentWithPasswordAsync(userId, Password);
            code = CodeFromManualKey(started.ManualKey, BaseUtc.UtcDateTime);
            await mfa.ConfirmEnrolmentAsync(userId, code);
        }

        LoginOutcome login;
        await using (var actDb2 = _fixture.CreateDbContext(clock))
        {
            var (auth, _) = CreateServices(actDb2, clock);
            login = await auth.LoginAsync(username, Password);
        }
        Assert.True(login.MfaRequired);

        await using (var actDb3 = _fixture.CreateDbContext(clock))
        {
            var (_, mfa) = CreateServices(actDb3, clock);
            var replay = await mfa.VerifyAsync(login.MfaToken!, code, null, false, null, null);
            Assert.False(replay.Success);
            Assert.Equal("The code is not correct.", replay.FailureReason);
        }

        clock.Advance(TimeSpan.FromSeconds(30));

        await using (var actDb4 = _fixture.CreateDbContext(clock))
        {
            var (_, mfa) = CreateServices(actDb4, clock);
            var nextStepCode = CodeFromManualKey(started.ManualKey, clock.GetUtcNow().UtcDateTime);
            var valid = await mfa.VerifyAsync(login.MfaToken!, nextStepCode, null, false, null, null);
            Assert.True(valid.Success);
        }
    }

    [PostgresFact]
    public async Task Start_Authenticated_WrongPassword_RecordsFailureEvent()
    {
        var (userId, _) = await SeedUserAsync();
        var clock = new FakeTimeProvider(BaseUtc);

        await using (var actDb = _fixture.CreateDbContext(clock))
        {
            var (_, mfa) = CreateServices(actDb, clock);
            await Assert.ThrowsAsync<BusinessRuleException>(() =>
                mfa.StartEnrolmentWithPasswordAsync(userId, "WrongPassword9!"));
        }

        await using var assertDb = _fixture.CreateDbContext();
        Assert.False(await assertDb.UserMfaCredentials.AsNoTracking().AnyAsync(c => c.UserId == userId));
        var failure = await assertDb.SecurityAuditEvents.AsNoTracking()
            .SingleAsync(e => e.TargetUserId == userId
                           && e.EventCode == SecurityEventCodes.MfaFailed
                           && e.Outcome == SecurityEventOutcome.Failure);
        Assert.Equal(userId, failure.ActorUserId);
        Assert.Equal("Password verification failed.", failure.Reason);
    }

    [PostgresFact]
    public async Task Regenerate_WrongCode_RecordsFailureEvent_AndChangesNothing()
    {
        var (userId, _) = await SeedUserAsync();
        var clock = new FakeTimeProvider(BaseUtc);

        EnrolmentStart started;
        EnrolmentResult first;
        await using (var actDb1 = _fixture.CreateDbContext(clock))
        {
            var (_, mfa) = CreateServices(actDb1, clock);
            started = await mfa.StartEnrolmentWithPasswordAsync(userId, Password);
            first = await mfa.ConfirmEnrolmentAsync(userId, CodeFromManualKey(started.ManualKey, clock.GetUtcNow().UtcDateTime));
        }

        clock.Advance(TimeSpan.FromSeconds(60));
        var badCode = WrongCode(started.ManualKey, clock.GetUtcNow().UtcDateTime);

        await using (var actDb2 = _fixture.CreateDbContext(clock))
        {
            var (_, mfa) = CreateServices(actDb2, clock);
            await Assert.ThrowsAsync<BusinessRuleException>(() =>
                mfa.RegenerateRecoveryCodesAsync(userId, Password, badCode));
        }

        await using var assertDb = _fixture.CreateDbContext();
        var storedHashes = await assertDb.MfaRecoveryCodes.AsNoTracking()
            .Where(r => r.UserId == userId)
            .Select(r => r.CodeHash)
            .ToListAsync();
        Assert.Equal(
            first.RecoveryCodes.Select(AdminPasswordRecoveryService.HashRecoveryCode).ToHashSet(StringComparer.Ordinal),
            storedHashes.ToHashSet(StringComparer.Ordinal));

        var failure = await assertDb.SecurityAuditEvents.AsNoTracking()
            .SingleAsync(e => e.TargetUserId == userId
                           && e.EventCode == SecurityEventCodes.MfaFailed
                           && e.Outcome == SecurityEventOutcome.Failure);
        Assert.Equal(userId, failure.ActorUserId);
        Assert.Equal("Code verification failed.", failure.Reason);
        Assert.DoesNotContain(badCode, failure.Reason ?? string.Empty, StringComparison.Ordinal);
        Assert.DoesNotContain(badCode, failure.Metadata ?? string.Empty, StringComparison.Ordinal);
    }
}
