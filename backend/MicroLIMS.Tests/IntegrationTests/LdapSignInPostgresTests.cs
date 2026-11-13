using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MicroLIMS.API.Extensions;
using MicroLIMS.Application.Abstractions.Security;
using MicroLIMS.Application.Abstractions.Sso;
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
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

[Collection("PostgresDatabaseCollection")]
public class LdapSignInPostgresTests
{
    private const string LocalPassword = "ValidPassword1!";
    private const string FixedKeyBase64 = "MDEyMzQ1Njc4OWFiY2RlZjAxMjM0NTY3ODlhYmNkZWY=";
    private static readonly DateTimeOffset BaseUtc = new(2026, 11, 13, 14, 0, 0, TimeSpan.Zero);

    private readonly PostgresTestFixture _fixture;

    public LdapSignInPostgresTests(PostgresTestFixture fixture) => _fixture = fixture;

    private sealed class FakeTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;
        public FakeTimeProvider(DateTimeOffset utcNow) => _utcNow = utcNow;
        public override DateTimeOffset GetUtcNow() => _utcNow;
    }

    private sealed class StubSettings : ISystemSettingsReader
    {
        private readonly Dictionary<string, string> _strings = new(StringComparer.Ordinal)
        {
            [SystemSettingKeys.MfaPolicy] = nameof(MfaPolicy.Off),
            [SystemSettingKeys.MfaRequiredRoles] = "SystemAdministrator,Reviewer",
            [SystemSettingKeys.SsoMode] = nameof(SsoMode.ActiveDirectory),
        };

        private readonly Dictionary<string, int> _ints = new(StringComparer.Ordinal)
        {
            [SystemSettingKeys.MfaTrustedDeviceHours] = 12,
            [SystemSettingKeys.MfaEnrolmentGraceDays] = 7,
            [SystemSettingKeys.LockoutMaxFailedAttempts] = AuthenticationService.MaxFailedAttempts,
            [SystemSettingKeys.LockoutDurationMinutes] = (int)AuthenticationService.LockDuration.TotalMinutes,
            [SystemSettingKeys.RefreshTokenLifetimeDays] = (int)AuthenticationService.RefreshTokenLifetime.TotalDays,
        };

        public StubSettings WithMfaPolicy(string policy)
        {
            _strings[SystemSettingKeys.MfaPolicy] = policy;
            return this;
        }

        public Task<int> GetIntAsync(string key, CancellationToken cancellationToken = default) => Task.FromResult(_ints[key]);

        public Task<bool> GetBoolAsync(string key, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException($"Unexpected bool setting read: {key}");

        public Task<string> GetStringAsync(string key, CancellationToken cancellationToken = default) => Task.FromResult(_strings[key]);
    }

    private static ISecretProtector CreateProtector() => new AesGcmSecretProtector(new MfaOptions
    {
        ActiveKeyId = "test-k1",
        EncryptionKeys = new Dictionary<string, string>(StringComparer.Ordinal) { ["test-k1"] = FixedKeyBase64 }
    });

    private static (AuthenticationService Auth, MfaService Mfa) CreateServices(
        MicroLimsDbContext db, FakeLdapDirectory directory, TimeProvider clock, StubSettings? settings = null)
    {
        var effective = settings ?? new StubSettings();
        var policy = new MfaPolicyService(effective);
        var audit = new SecurityAuditService(db, new SystemSecurityRequestContext(), clock);
        var ssoMode = new SsoModeService(db, effective, audit);
        var email = new EmailSender("", 587, "", "", "no-reply@microlims.local", false);
        AccessTokenIssuer issuer = s => $"jwt-{s.UserId}-{Guid.NewGuid():N}";

        AuthenticationService auth = null!;
        var ldap = new LdapSignInService(db, directory, ssoMode, () => auth, audit, clock);
        auth = new AuthenticationService(
            db, issuer, new PermissionService(db), email, NullLogger<AuthenticationService>.Instance, audit, clock,
            effective, policy, ssoMode, ldap);
        var mfa = new MfaService(
            db, auth, CreateProtector(), audit, NullLogger<MfaService>.Instance, policy, email,
            new ZXingQrCodeRenderer(), clock, ssoMode);
        return (auth, mfa);
    }

    private sealed record Seeded(User User, string CompanyName, string Subject, byte[]? TotpSecret)
    {
        public string? StoredSubject => User.DirectorySubjectId;
    }

    private async Task<Seeded> SeedAsync(
        RoleType role = RoleType.Analyst,
        SignInMethod method = SignInMethod.Directory,
        string? storedSubject = null,
        bool withActiveMfa = false,
        bool isEmergency = false,
        bool isActive = true,
        int failedAttempts = 0,
        DateTime? lockedUntil = null,
        bool linked = true)
    {
        await using var db = _fixture.CreateDbContext();
        var roleId = await db.Roles.AsNoTracking().Where(r => r.Type == role).OrderBy(r => r.Id).Select(r => r.Id).FirstAsync();
        var tag = Guid.NewGuid().ToString("N");
        var company = $"jdoe.{tag[..12]}";
        var user = new User
        {
            FullName = $"Ldap Test {tag[..8]}",
            Username = $"ldap_t_{tag}",
            Email = $"ldap_t_{tag}@example.test",
            PasswordHash = TestPasswords.Hash(LocalPassword),
            RoleId = roleId,
            IsActive = isActive,
            CreatedAt = BaseUtc.UtcDateTime,
            SignInMethod = method,
            IsEmergencyAccount = isEmergency,
            DirectoryUsername = linked && !isEmergency ? User.NormalizeDirectoryUsername(company) : null,
            DirectorySubjectId = storedSubject is null ? null : $"{storedSubject}-{tag}",
            FailedLoginAttempts = failedAttempts,
            LockedUntil = lockedUntil,
            SigningPasswordSetAt = BaseUtc.UtcDateTime
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        byte[]? secret = null;
        if (withActiveMfa)
        {
            secret = Totp.NewSecret();
            db.UserMfaCredentials.Add(new UserMfaCredential
            {
                UserId = user.Id,
                Type = MfaCredentialType.AuthenticatorApp,
                SecretProtected = CreateProtector().Protect(secret),
                StepSeconds = 30,
                Status = MfaCredentialStatus.Active,
                CreatedAt = BaseUtc.UtcDateTime,
                ConfirmedAt = BaseUtc.UtcDateTime
            });
            await db.SaveChangesAsync();
        }

        return new Seeded(user, company, $"guid-{tag}", secret);
    }

    private async Task<LoginOutcome> LoginAsync(FakeLdapDirectory directory, string typedName, string password, StubSettings? settings = null)
    {
        await using var actDb = _fixture.CreateDbContext();
        var (auth, _) = CreateServices(actDb, directory, new FakeTimeProvider(BaseUtc), settings);
        return await auth.LoginAsync(typedName, password, "127.0.0.1", "TestAgent");
    }

    // ---- the tests ---------------------------------------------------------

    [PostgresFact]
    public async Task ValidBind_LinkedUser_SignsIn_MethodLdap_BindsSubjectFirstTime()
    {
        var s = await SeedAsync();
        var directory = new FakeLdapDirectory().WithAccount(s.CompanyName.ToLowerInvariant(), "Corp-Pass-1", s.Subject);

        var outcome = await LoginAsync(directory, s.CompanyName.ToUpperInvariant(), "Corp-Pass-1");

        Assert.True(outcome.Success);
        Assert.NotNull(outcome.Token);
        Assert.NotNull(outcome.RefreshToken);
        Assert.Equal(new[] { s.CompanyName.ToLowerInvariant() }, directory.BindCalls);

        await using var assertDb = _fixture.CreateDbContext();
        var user = await assertDb.Users.AsNoTracking().SingleAsync(u => u.Id == s.User.Id);
        Assert.Equal(s.Subject, user.DirectorySubjectId);
        Assert.Equal(BaseUtc.UtcDateTime, user.LastLoginAt);
        Assert.Equal(0, user.FailedLoginAttempts);

        var history = await assertDb.LoginHistories.AsNoTracking().SingleAsync(h => h.UserId == s.User.Id);
        Assert.True(history.Success);
        Assert.Equal("Ldap", history.Method);

        var audit = await assertDb.AuditLogs.AsNoTracking()
            .Where(a => a.EntityName == nameof(User) && a.EntityId == s.User.Id.ToString() && a.Reason == "User directory linked")
            .ToListAsync();
        Assert.NotEmpty(audit);
        Assert.All(audit, a => Assert.Equal(s.User.Id, a.UserId));
        Assert.Contains(audit, a => (a.NewValue ?? string.Empty).Contains(s.Subject, StringComparison.Ordinal));

        var codes = await assertDb.SecurityAuditEvents.AsNoTracking()
            .Where(e => e.TargetUserId == s.User.Id).Select(e => e.EventCode).ToListAsync();
        Assert.Contains(SecurityEventCodes.UserDirectoryLinked, codes);
        Assert.Contains(SecurityEventCodes.LoginSuccess, codes);
    }

    [PostgresFact]
    public async Task BadPassword_IncrementsMicroLimsCounter_GenericMessage()
    {
        var s = await SeedAsync(storedSubject: "bound-subject");
        var directory = new FakeLdapDirectory().WithAccount(s.CompanyName.ToLowerInvariant(), "Corp-Pass-1", s.StoredSubject!);

        var outcome = await LoginAsync(directory, s.CompanyName, "not-the-password");

        Assert.False(outcome.Success);
        Assert.Null(outcome.Token);
        Assert.Equal("Invalid username or password.", outcome.FailureReason);
        Assert.Single(directory.BindCalls);

        await using var assertDb = _fixture.CreateDbContext();
        var user = await assertDb.Users.AsNoTracking().SingleAsync(u => u.Id == s.User.Id);
        Assert.Equal(1, user.FailedLoginAttempts);
        Assert.Null(user.LockedUntil);
        Assert.True(await assertDb.SecurityAuditEvents.AsNoTracking()
            .AnyAsync(e => e.TargetUserId == s.User.Id && e.EventCode == SecurityEventCodes.LoginFailedBadCredentials));
        var history = await assertDb.LoginHistories.AsNoTracking().SingleAsync(h => h.UserId == s.User.Id);
        Assert.False(history.Success);
        Assert.Equal("Wrong password", history.FailureReason);
        Assert.False(await assertDb.RefreshTokens.AsNoTracking().AnyAsync(r => r.UserId == s.User.Id));
    }

    [PostgresFact]
    public async Task FifthBadPassword_LocksAccount_ThenCorrectPasswordIsRefusedWithoutContactingDirectory()
    {
        var s = await SeedAsync(storedSubject: "bound-subject", failedAttempts: AuthenticationService.MaxFailedAttempts - 1);
        var directory = new FakeLdapDirectory().WithAccount(s.CompanyName.ToLowerInvariant(), "Corp-Pass-1", s.StoredSubject!);

        await LoginAsync(directory, s.CompanyName, "wrong");
        Assert.Single(directory.BindCalls);

        await using (var midDb = _fixture.CreateDbContext())
        {
            var locked = await midDb.Users.AsNoTracking().SingleAsync(u => u.Id == s.User.Id);
            Assert.NotNull(locked.LockedUntil);
        }

        var outcome = await LoginAsync(directory, s.CompanyName, "Corp-Pass-1");
        Assert.False(outcome.Success);
        Assert.Equal("Invalid username or password.", outcome.FailureReason);
        Assert.Single(directory.BindCalls); // still only the first attempt
    }

    [PostgresFact]
    public async Task LockedInMicroLims_DirectoryIsNotContacted()
    {
        var s = await SeedAsync(storedSubject: "bound-subject", failedAttempts: 5, lockedUntil: BaseUtc.UtcDateTime.AddMinutes(10));
        var directory = new FakeLdapDirectory().WithAccount(s.CompanyName.ToLowerInvariant(), "Corp-Pass-1", s.StoredSubject!);

        var outcome = await LoginAsync(directory, s.CompanyName, "Corp-Pass-1");

        Assert.False(outcome.Success);
        Assert.Equal("Invalid username or password.", outcome.FailureReason);
        Assert.Empty(directory.BindCalls);
        Assert.Empty(directory.FindCalls);

        await using var assertDb = _fixture.CreateDbContext();
        Assert.True(await assertDb.SecurityAuditEvents.AsNoTracking()
            .AnyAsync(e => e.TargetUserId == s.User.Id && e.EventCode == SecurityEventCodes.LoginFailedAccountLocked));
        var user = await assertDb.Users.AsNoTracking().SingleAsync(u => u.Id == s.User.Id);
        Assert.Equal(5, user.FailedLoginAttempts);
    }

    [PostgresFact]
    public async Task DisabledInDirectory_Refused_UserDeactivated_SessionsRevoked_Audited()
    {
        var s = await SeedAsync(storedSubject: "bound-subject");
        await using (var seedDb = _fixture.CreateDbContext())
        {
            seedDb.RefreshTokens.Add(new RefreshToken
            {
                UserId = s.User.Id, TokenHash = Guid.NewGuid().ToString("N"),
                ExpiresAt = BaseUtc.UtcDateTime.AddDays(1), CreatedAt = BaseUtc.UtcDateTime
            });
            await seedDb.SaveChangesAsync();
        }

        var directory = new FakeLdapDirectory().WithAccount(s.CompanyName.ToLowerInvariant(), "Corp-Pass-1", s.StoredSubject!, disabled: true);

        var outcome = await LoginAsync(directory, s.CompanyName, "Corp-Pass-1");

        Assert.False(outcome.Success);
        Assert.Null(outcome.Token);
        Assert.Equal("Invalid username or password.", outcome.FailureReason);

        await using var assertDb = _fixture.CreateDbContext();
        var user = await assertDb.Users.AsNoTracking().SingleAsync(u => u.Id == s.User.Id);
        Assert.False(user.IsActive);
        Assert.Equal(BaseUtc.UtcDateTime, user.DirectorySignInRefusedAt);
        Assert.All(await assertDb.RefreshTokens.AsNoTracking().Where(r => r.UserId == s.User.Id).ToListAsync(),
            r => Assert.NotNull(r.RevokedAt));

        var evt = await assertDb.SecurityAuditEvents.AsNoTracking()
            .SingleAsync(e => e.TargetUserId == s.User.Id && e.EventCode == SecurityEventCodes.UserDeactivatedByDirectory);
        Assert.True(evt.ActorIsSystem);
        Assert.Null(evt.ActorUserId);

        var audit = await assertDb.AuditLogs.AsNoTracking()
            .Where(a => a.EntityName == nameof(User) && a.EntityId == s.User.Id.ToString() && a.Action == "Update")
            .OrderByDescending(a => a.Id).FirstAsync();
        Assert.Equal(ActorType.System, audit.ActorType);
        Assert.Equal("LdapSignIn", audit.SystemProcessName);
        Assert.Equal("Account disabled in the company directory", audit.Reason);
        Assert.Contains("IsActive", audit.NewValue);
    }

    [PostgresFact]
    public async Task SubjectMismatch_Refused()
    {
        var s = await SeedAsync(storedSubject: "bound-subject-original");
        var directory = new FakeLdapDirectory().WithAccount(s.CompanyName.ToLowerInvariant(), "Corp-Pass-1", "someone-else-now");

        var outcome = await LoginAsync(directory, s.CompanyName, "Corp-Pass-1");

        Assert.False(outcome.Success);
        Assert.Equal("Invalid username or password.", outcome.FailureReason);
        await using var assertDb = _fixture.CreateDbContext();
        var user = await assertDb.Users.AsNoTracking().SingleAsync(u => u.Id == s.User.Id);
        Assert.Equal(s.StoredSubject, user.DirectorySubjectId);
        Assert.Equal(0, user.FailedLoginAttempts);
        Assert.True(await assertDb.SecurityAuditEvents.AsNoTracking()
            .AnyAsync(e => e.TargetUserId == s.User.Id && e.EventCode == SecurityEventCodes.SsoLoginFailedSubjectMismatch));
        Assert.False(await assertDb.RefreshTokens.AsNoTracking().AnyAsync(r => r.UserId == s.User.Id));
    }

    [PostgresFact]
    public async Task SubjectHeldByAnotherUser_Refused()
    {
        var holder = await SeedAsync(storedSubject: "shared");
        var typed = await SeedAsync();
        var directory = new FakeLdapDirectory().WithAccount(typed.CompanyName.ToLowerInvariant(), "Corp-Pass-1", holder.User.DirectorySubjectId!);

        var outcome = await LoginAsync(directory, typed.CompanyName, "Corp-Pass-1");

        Assert.False(outcome.Success);
        await using var assertDb = _fixture.CreateDbContext();
        Assert.False(await assertDb.RefreshTokens.AsNoTracking().AnyAsync(r => r.UserId == holder.User.Id || r.UserId == typed.User.Id));
        Assert.Null((await assertDb.Users.AsNoTracking().SingleAsync(u => u.Id == typed.User.Id)).DirectorySubjectId);
        Assert.True(await assertDb.SecurityAuditEvents.AsNoTracking()
            .AnyAsync(e => e.TargetUserId == typed.User.Id && e.EventCode == SecurityEventCodes.SsoLoginFailedSubjectMismatch));
    }

    [PostgresFact]
    public async Task NotLinked_Refused_BindNotAttemptedForUnknownUser()
    {
        var directory = new FakeLdapDirectory().WithAccount("ghost", "Corp-Pass-1", "guid-ghost");
        var typed = $"ghost.{Guid.NewGuid():N}";

        var outcome = await LoginAsync(directory, typed, "Corp-Pass-1");

        Assert.False(outcome.Success);
        Assert.Equal("Invalid username or password.", outcome.FailureReason);
        Assert.Empty(directory.BindCalls);
        Assert.Empty(directory.FindCalls);
        await using var assertDb = _fixture.CreateDbContext();
        Assert.True(await assertDb.SecurityAuditEvents.AsNoTracking()
            .AnyAsync(e => e.TargetUsername == typed && e.EventCode == SecurityEventCodes.SsoLoginFailedNotLinked));
        Assert.True(await assertDb.LoginHistories.AsNoTracking().AnyAsync(h => h.Username == typed && !h.Success));
    }

    [PostgresFact]
    public async Task LocalMethodUser_WithCompanyUsername_Refused_BindNotAttempted()
    {
        var s = await SeedAsync(method: SignInMethod.Local);
        var directory = new FakeLdapDirectory().WithAccount(s.CompanyName.ToLowerInvariant(), "Corp-Pass-1", s.Subject);

        var outcome = await LoginAsync(directory, s.CompanyName, "Corp-Pass-1");

        Assert.False(outcome.Success);
        Assert.Empty(directory.BindCalls);
    }

    [PostgresFact]
    public async Task DirectoryUnavailable_ReturnsUnavailableMessage_RecordsEvent_NoLockoutIncrement()
    {
        var s = await SeedAsync(storedSubject: "bound-subject", failedAttempts: 2);
        var directory = new FakeLdapDirectory { ToThrow = new DirectoryUnavailableException("connection refused") }
            .WithAccount(s.CompanyName.ToLowerInvariant(), "Corp-Pass-1", s.StoredSubject!);

        var outcome = await LoginAsync(directory, s.CompanyName, "Corp-Pass-1");

        Assert.False(outcome.Success);
        Assert.Null(outcome.Token);
        Assert.Equal("Company sign-in is unavailable — contact an administrator.", outcome.FailureReason);

        await using var assertDb = _fixture.CreateDbContext();
        var user = await assertDb.Users.AsNoTracking().SingleAsync(u => u.Id == s.User.Id);
        Assert.Equal(2, user.FailedLoginAttempts);
        Assert.Null(user.LockedUntil);
        var evt = await assertDb.SecurityAuditEvents.AsNoTracking()
            .SingleAsync(e => e.TargetUserId == s.User.Id && e.EventCode == SecurityEventCodes.DirectoryUnavailable);
        Assert.Equal(SecurityEventOutcome.Failure, evt.Outcome);
        Assert.False(await assertDb.SecurityAuditEvents.AsNoTracking()
            .AnyAsync(e => e.TargetUserId == s.User.Id && e.EventCode == SecurityEventCodes.LoginFailedBadCredentials));
        var history = await assertDb.LoginHistories.AsNoTracking().SingleAsync(h => h.UserId == s.User.Id);
        Assert.Equal("Directory unavailable", history.FailureReason);
    }

    [PostgresFact]
    public async Task SecondFactorRequired_ReturnsChallenge_ThenVerifyCompletes()
    {
        var s = await SeedAsync(role: RoleType.SystemAdministrator, storedSubject: "bound-subject", withActiveMfa: true);
        var directory = new FakeLdapDirectory().WithAccount(s.CompanyName.ToLowerInvariant(), "Corp-Pass-1", s.StoredSubject!);
        var settings = new StubSettings().WithMfaPolicy("RequiredForAll");

        var step1 = await LoginAsync(directory, s.CompanyName, "Corp-Pass-1", settings);

        Assert.True(step1.Success);
        Assert.True(step1.MfaRequired);
        Assert.NotNull(step1.MfaToken);
        Assert.Null(step1.Token);
        await using (var midDb = _fixture.CreateDbContext())
        {
            Assert.False(await midDb.RefreshTokens.AsNoTracking().AnyAsync(r => r.UserId == s.User.Id));
            Assert.False(await midDb.LoginHistories.AsNoTracking().AnyAsync(h => h.UserId == s.User.Id && h.Success));
        }

        var code = Totp.Generate(s.TotpSecret!, Totp.StepAt(BaseUtc.UtcDateTime, 30));
        LoginOutcome step2;
        await using (var actDb = _fixture.CreateDbContext())
        {
            var (_, mfa) = CreateServices(actDb, directory, new FakeTimeProvider(BaseUtc), settings);
            step2 = await mfa.VerifyAsync(step1.MfaToken!, code, null, false, "127.0.0.1", "TestAgent");
        }

        Assert.True(step2.Success);
        Assert.NotNull(step2.Token);
        await using var assertDb = _fixture.CreateDbContext();
        var history = await assertDb.LoginHistories.AsNoTracking().SingleAsync(h => h.UserId == s.User.Id && h.Success);
        Assert.Equal("Ldap", history.Method);
        Assert.Single(directory.BindCalls);
    }

    [PostgresFact]
    public async Task PasswordNeverAppearsInSecurityEventsAuditLogsOrLoginHistory()
    {
        var good = $"Good-{Guid.NewGuid():N}!";
        var bad = $"Bad-{Guid.NewGuid():N}!";
        var down = $"Down-{Guid.NewGuid():N}!";
        var off = $"Off-{Guid.NewGuid():N}!";
        var ok = await SeedAsync();
        var wrong = await SeedAsync(storedSubject: "bound");
        var unavailable = await SeedAsync(storedSubject: "bound");
        var disabled = await SeedAsync(storedSubject: "bound");

        var directory = new FakeLdapDirectory()
            .WithAccount(ok.CompanyName.ToLowerInvariant(), good, ok.Subject)
            .WithAccount(wrong.CompanyName.ToLowerInvariant(), "other-secret", wrong.StoredSubject!)
            .WithAccount(disabled.CompanyName.ToLowerInvariant(), off, disabled.StoredSubject!, disabled: true);
        await LoginAsync(directory, ok.CompanyName, good);
        await LoginAsync(directory, wrong.CompanyName, bad);
        await LoginAsync(directory, disabled.CompanyName, off);
        await LoginAsync(new FakeLdapDirectory { ToThrow = new DirectoryUnavailableException("unreachable") }, unavailable.CompanyName, down);
        await LoginAsync(directory, $"nobody.{Guid.NewGuid():N}", bad);

        var ids = new[] { ok.User.Id, wrong.User.Id, unavailable.User.Id, disabled.User.Id };
        var idStrings = ids.Select(i => i.ToString()).ToList();
        var json = new JsonSerializerOptions { ReferenceHandler = ReferenceHandler.IgnoreCycles };

        await using var assertDb = _fixture.CreateDbContext();
        var events = await assertDb.SecurityAuditEvents.AsNoTracking().Where(e => e.TargetUserId != null && ids.Contains(e.TargetUserId.Value)).ToListAsync();
        var audits = await assertDb.AuditLogs.AsNoTracking().Where(a => a.EntityName == nameof(User) && idStrings.Contains(a.EntityId)).ToListAsync();
        var history = await assertDb.LoginHistories.AsNoTracking().Where(h => h.UserId != null && ids.Contains(h.UserId.Value)).ToListAsync();
        var anonymousHistory = await assertDb.LoginHistories.AsNoTracking().Where(h => h.UserId == null && h.Username.StartsWith("nobody.")).ToListAsync();
        var anonymousEvents = await assertDb.SecurityAuditEvents.AsNoTracking().Where(e => e.TargetUserId == null && e.TargetUsername!.StartsWith("nobody.")).ToListAsync();

        Assert.NotEmpty(events);
        Assert.NotEmpty(audits);
        Assert.NotEmpty(history);
        Assert.NotEmpty(anonymousHistory);
        Assert.NotEmpty(anonymousEvents);
        var everything = string.Concat(
            JsonSerializer.Serialize(events, json), JsonSerializer.Serialize(audits, json), JsonSerializer.Serialize(history, json),
            JsonSerializer.Serialize(anonymousHistory, json), JsonSerializer.Serialize(anonymousEvents, json));
        foreach (var secret in new[] { good, bad, down, off, LocalPassword })
            Assert.DoesNotContain(secret, everything, StringComparison.Ordinal);
        foreach (var secret in new[] { good, bad, down, off })
            Assert.DoesNotContain(secret, string.Concat(directory.BindCalls.Concat(directory.FindCalls)), StringComparison.Ordinal);
    }

    [PostgresFact]
    public async Task EmergencyAccount_InAdMode_UsesLocalPath()
    {
        var s = await SeedAsync(role: RoleType.SystemAdministrator, method: SignInMethod.Local, withActiveMfa: true, isEmergency: true);
        var directory = new FakeLdapDirectory();

        var outcome = await LoginAsync(directory, s.User.Username, LocalPassword, new StubSettings().WithMfaPolicy("Off"));

        Assert.True(outcome.Success);
        Assert.True(outcome.MfaRequired); // emergency sign-in always asks the second factor
        Assert.Null(outcome.Token);
        Assert.Empty(directory.BindCalls);
        Assert.Empty(directory.FindCalls);

        var wrong = await LoginAsync(directory, s.User.Username, "wrong-local-password");
        Assert.False(wrong.Success);
        Assert.Empty(directory.BindCalls);
    }

    [PostgresFact]
    public async Task AdMode_DirectoryUser_SigningPassword_Refused()
    {
        var s = await SeedAsync(storedSubject: "bound-subject");
        var directory = new FakeLdapDirectory().WithAccount(s.CompanyName.ToLowerInvariant(), "Corp-Pass-1", s.StoredSubject!);

        // The MicroLIMS username with the signing password: nobody is linked to that name.
        var byUsername = await LoginAsync(directory, s.User.Username, LocalPassword);
        // The company username with the signing password: the directory says no.
        var byCompany = await LoginAsync(directory, s.CompanyName, LocalPassword);

        foreach (var outcome in new[] { byUsername, byCompany })
        {
            Assert.False(outcome.Success);
            Assert.Null(outcome.Token);
            Assert.Null(outcome.RefreshToken);
            Assert.False(outcome.MfaRequired);
            Assert.Equal("Invalid username or password.", outcome.FailureReason);
        }

        await using var assertDb = _fixture.CreateDbContext();
        Assert.False(await assertDb.RefreshTokens.AsNoTracking().AnyAsync(r => r.UserId == s.User.Id));
        Assert.Equal(1, (await assertDb.Users.AsNoTracking().SingleAsync(u => u.Id == s.User.Id)).FailedLoginAttempts);
    }

    [PostgresFact]
    public async Task AdMode_MissingServerBaseDnOrServiceAccount_FailsStartupNamingTheKey()
    {
        await using var db = _fixture.CreateDbContext();
        var clock = new FakeTimeProvider(BaseUtc);
        var audit = new SecurityAuditService(db, new SystemSecurityRequestContext(), clock);
        var ssoMode = new SsoModeService(db, new StubSettings(), audit);

        var complete = new LdapOptions { Host = "dc.corp.test", BaseDn = "DC=corp,DC=test", BindDn = "CN=svc,DC=corp,DC=test", BindPassword = "x" };
        await SsoStartupValidator.EnsureValidAsync(ssoMode, new SsoOptions { Ldap = complete }); // valid: no throw

        foreach (var (mutate, key) in new (Action<LdapOptions>, string)[]
        {
            (o => o.Host = null, "Sso:Ldap:Host"),
            (o => o.BaseDn = " ", "Sso:Ldap:BaseDn"),
            (o => o.BindDn = null, "Sso:Ldap:BindDn"),
            (o => o.BindPassword = null, "Sso:Ldap:BindPassword"),
            (o => o.UseLdaps = false, "LDAPS or StartTLS"),
        })
        {
            var broken = new LdapOptions { Host = complete.Host, BaseDn = complete.BaseDn, BindDn = complete.BindDn, BindPassword = complete.BindPassword };
            mutate(broken);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => SsoStartupValidator.EnsureValidAsync(ssoMode, new SsoOptions { Ldap = broken }));
            Assert.Contains(key, ex.Message);
            Assert.DoesNotContain("x\"", ex.Message);
        }
    }
}
