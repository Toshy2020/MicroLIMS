using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MicroLIMS.Application.Abstractions.Email;
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
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

[Collection("PostgresDatabaseCollection")]
public class SsoLocalLoginPostgresTests
{
    private const string Password = "ValidPassword1!";
    private const string FixedKeyBase64 = "MDEyMzQ1Njc4OWFiY2RlZjAxMjM0NTY3ODlhYmNkZWY=";
    private static readonly DateTimeOffset BaseUtc = new(2026, 11, 13, 10, 0, 0, TimeSpan.Zero);

    private readonly PostgresTestFixture _fixture;

    public SsoLocalLoginPostgresTests(PostgresTestFixture fixture) => _fixture = fixture;

    private sealed class StubSystemSettingsReader : ISystemSettingsReader
    {
        private readonly Dictionary<string, string> _strings = new(StringComparer.Ordinal)
        {
            [SystemSettingKeys.MfaPolicy] = nameof(MfaPolicy.RequiredForAll),
            [SystemSettingKeys.MfaRequiredRoles] = "SystemAdministrator,Reviewer",
            [SystemSettingKeys.SsoMode] = nameof(SsoMode.Off),
        };

        private readonly Dictionary<string, int> _ints = new(StringComparer.Ordinal)
        {
            [SystemSettingKeys.MfaTrustedDeviceHours] = 12,
            [SystemSettingKeys.MfaEnrolmentGraceDays] = 7,
            [SystemSettingKeys.LockoutMaxFailedAttempts] = AuthenticationService.MaxFailedAttempts,
            [SystemSettingKeys.LockoutDurationMinutes] = (int)AuthenticationService.LockDuration.TotalMinutes,
            [SystemSettingKeys.RefreshTokenLifetimeDays] = (int)AuthenticationService.RefreshTokenLifetime.TotalDays,
        };

        public StubSystemSettingsReader WithSsoMode(string mode)
        {
            _strings[SystemSettingKeys.SsoMode] = mode;
            return this;
        }

        public StubSystemSettingsReader WithMfaPolicy(string policy)
        {
            _strings[SystemSettingKeys.MfaPolicy] = policy;
            return this;
        }

        public Task<int> GetIntAsync(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult(_ints[key]);

        public Task<bool> GetBoolAsync(string key, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException($"Unexpected bool setting read: {key}");

        public Task<string> GetStringAsync(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult(_strings[key]);
    }

    private sealed class RecordingEmailSender : IEmailSender
    {
        public List<(string To, string Subject, string Body)> Sent { get; } = new();
        public Exception? ExceptionToThrow { get; init; }

        public Task SendAsync(string to, string subject, string body)
        {
            if (ExceptionToThrow is not null)
                throw ExceptionToThrow;

            Sent.Add((to, subject, body));
            return Task.CompletedTask;
        }
    }

    private sealed class MutableFingerprint : ISsoConfigurationFingerprint
    {
        public string Value { get; set; } = "fp-v1";
        public string GetFingerprint(SsoMode mode) => $"{mode}:{Value}";
    }

    private sealed class FakeTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow;
        public FakeTimeProvider(DateTimeOffset initialUtc) => _utcNow = initialUtc;
        public override DateTimeOffset GetUtcNow() => _utcNow;
    }

    private static ISecretProtector CreateProtector() => new AesGcmSecretProtector(new MfaOptions
    {
        ActiveKeyId = "test-k1",
        EncryptionKeys = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["test-k1"] = FixedKeyBase64
        }
    });

    private static (AuthenticationService Auth, MfaService Mfa, UserService Users, SsoModeService SsoMode) CreateServices(
        MicroLimsDbContext db,
        TimeProvider clock,
        ISystemSettingsReader? settings = null,
        IEmailSender? emailSender = null,
        ISsoConfigurationFingerprint? fingerprint = null)
    {
        var effectiveSettings = settings ?? new StubSystemSettingsReader();
        var policy = new MfaPolicyService(effectiveSettings);
        var audit = new SecurityAuditService(db, new SystemSecurityRequestContext(), clock);
        var ssoMode = new SsoModeService(db, effectiveSettings, audit, fingerprint);
        var email = emailSender ?? new EmailSender("", 587, "", "", "no-reply@microlims.local", false);
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

        var mfa = new MfaService(
            db,
            auth,
            CreateProtector(),
            audit,
            NullLogger<MfaService>.Instance,
            policy,
            email,
            new ZXingQrCodeRenderer(),
            clock,
            ssoMode);

        var users = new UserService(db, auth, audit, clock, signatureService: new ElectronicSignatureService(db, clock, effectiveSettings), settings: effectiveSettings, mfaPolicy: policy);

        return (auth, mfa, users, ssoMode);
    }

    private static SystemSettingsService CreateSettingsService(
        MicroLimsDbContext db,
        TimeProvider clock,
        ISsoConfigurationFingerprint? fingerprint = null)
    {
        var cache = new SystemSettingsCache();
        var reader = new SystemSettingsReader(db, cache, null, clock);
        var signatures = new ElectronicSignatureService(db, clock, reader);
        var audit = new SecurityAuditService(db, new SystemSecurityRequestContext(), clock);
        var ssoMode = new SsoModeService(db, reader, audit, fingerprint);
        return new SystemSettingsService(db, cache, signatures, null, clock, null, audit, ssoMode);
    }

    private static async Task<int> GetRoleIdAsync(MicroLimsDbContext db, RoleType roleType)
    {
        var existing = await db.Roles.AsNoTracking()
            .Where(r => r.Type == roleType)
            .OrderBy(r => r.Id)
            .Select(r => r.Id)
            .FirstOrDefaultAsync();
        if (existing != 0)
            return existing;

        var role = new Role
        {
            Name = roleType.ToString(),
            Type = roleType,
            IsSystemRole = true,
            IsActive = true
        };
        db.Roles.Add(role);
        await db.SaveChangesAsync();
        return role.Id;
    }

    private static async Task<(User User, byte[]? TotpSecret)> SeedUserAsync(
        MicroLimsDbContext db,
        RoleType roleType,
        SignInMethod signInMethod = SignInMethod.Local,
        bool withActiveMfa = false,
        bool isEmergency = false,
        string? directoryUsername = null,
        string? email = null,
        bool isActive = true)
    {
        var roleId = await GetRoleIdAsync(db, roleType);
        var tag = Guid.NewGuid().ToString("N");
        var user = new User
        {
            FullName = $"SSO Test {tag[..8]}",
            Username = $"sso_t_{tag}",
            Email = email ?? $"sso_t_{tag}@example.test",
            PasswordHash = TestPasswords.Hash(Password),
            RoleId = roleId,
            IsActive = isActive,
            CreatedAt = BaseUtc.UtcDateTime,
            SignInMethod = signInMethod,
            IsEmergencyAccount = isEmergency,
            DirectoryUsername = User.NormalizeDirectoryUsername(directoryUsername)
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

        return (user, secret);
    }

    [PostgresFact]
    public async Task ModeOff_LocalLoginWorksAsBefore()
    {
        var clock = new FakeTimeProvider(BaseUtc);
        User user;
        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            (user, _) = await SeedUserAsync(arrangeDb, RoleType.Analyst, SignInMethod.Local);
        }

        await using var actDb = _fixture.CreateDbContext();
        var (auth, _, _, _) = CreateServices(actDb, clock, new StubSystemSettingsReader().WithSsoMode("Off").WithMfaPolicy("Off"));
        var outcome = await auth.LoginAsync(user.Username, Password, "127.0.0.1", "TestAgent");

        Assert.True(outcome.Success);
        Assert.NotNull(outcome.Token);
        Assert.NotNull(outcome.RefreshToken);
    }

    [PostgresFact]
    public async Task OidcMode_DirectoryUser_CorrectSigningPassword_IsRefused_GenericMessage_NoTokens_NoLockoutIncrement()
    {
        var clock = new FakeTimeProvider(BaseUtc);
        User user;
        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            (user, _) = await SeedUserAsync(arrangeDb, RoleType.Analyst, SignInMethod.Directory, directoryUsername: $"dir.{Guid.NewGuid():N}@corp.test");
        }

        LoginOutcome outcome;
        await using (var actDb = _fixture.CreateDbContext())
        {
            var (auth, _, _, _) = CreateServices(actDb, clock, new StubSystemSettingsReader().WithSsoMode("OpenIdConnect").WithMfaPolicy("Off"));
            outcome = await auth.LoginAsync(user.Username, Password, "127.0.0.1", "TestAgent");
        }

        Assert.False(outcome.Success);
        Assert.Null(outcome.Token);
        Assert.Null(outcome.RefreshToken);
        Assert.False(outcome.MfaRequired);
        Assert.False(outcome.MfaEnrolmentRequired);
        Assert.Null(outcome.MfaToken);
        Assert.Equal("Invalid username or password.", outcome.FailureReason);

        await using var assertDb = _fixture.CreateDbContext();
        var reloaded = await assertDb.Users.AsNoTracking().SingleAsync(u => u.Id == user.Id);
        Assert.Equal(0, reloaded.FailedLoginAttempts);
        Assert.Null(reloaded.LockedUntil);
        Assert.True(await assertDb.SecurityAuditEvents.AsNoTracking()
            .AnyAsync(e => e.TargetUserId == user.Id && e.EventCode == SecurityEventCodes.LocalLoginRefusedSsoOn && e.Outcome == SecurityEventOutcome.Failure));
        Assert.False(await assertDb.RefreshTokens.AsNoTracking().AnyAsync(r => r.UserId == user.Id));
    }

    [PostgresFact]
    public async Task OidcMode_DirectoryUser_WrongPassword_IncrementsLockout()
    {
        var clock = new FakeTimeProvider(BaseUtc);
        User user;
        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            (user, _) = await SeedUserAsync(arrangeDb, RoleType.Analyst, SignInMethod.Directory, directoryUsername: $"dir.{Guid.NewGuid():N}@corp.test");
        }

        LoginOutcome outcome;
        await using (var actDb = _fixture.CreateDbContext())
        {
            var (auth, _, _, _) = CreateServices(actDb, clock, new StubSystemSettingsReader().WithSsoMode("OpenIdConnect").WithMfaPolicy("Off"));
            outcome = await auth.LoginAsync(user.Username, "WrongPassword!", "127.0.0.1", "TestAgent");
        }

        Assert.False(outcome.Success);
        Assert.Equal("Invalid username or password.", outcome.FailureReason);

        await using var assertDb = _fixture.CreateDbContext();
        var reloaded = await assertDb.Users.AsNoTracking().SingleAsync(u => u.Id == user.Id);
        Assert.Equal(1, reloaded.FailedLoginAttempts);
        Assert.True(await assertDb.SecurityAuditEvents.AsNoTracking()
            .AnyAsync(e => e.TargetUserId == user.Id && e.EventCode == SecurityEventCodes.LoginFailedBadCredentials));
    }

    [PostgresFact]
    public async Task OidcMode_EmergencyAccount_PasswordThenCode_Succeeds_RecordsEvent_EmailsAdministrators()
    {
        await ScratchPostgresDatabase.RunAsync(async cs =>
        {
            var clock = new FakeTimeProvider(BaseUtc);
            User emergencyAdmin;
            User otherActiveAdmin;
            User inactiveAdmin;
            byte[] emergencySecret;

            await using (var arrangeDb = ScratchPostgresDatabase.NewContext(cs))
            {
                await arrangeDb.Database.MigrateAsync();
                (emergencyAdmin, var s) = await SeedUserAsync(
                    arrangeDb, RoleType.SystemAdministrator, SignInMethod.Local, withActiveMfa: true, isEmergency: true, email: "emerg@corp.test");
                emergencySecret = s!;
                (otherActiveAdmin, _) = await SeedUserAsync(
                    arrangeDb, RoleType.SystemAdministrator, SignInMethod.Directory, email: "admin2@corp.test", directoryUsername: "admin2@corp.test");
                (inactiveAdmin, _) = await SeedUserAsync(
                    arrangeDb, RoleType.SystemAdministrator, SignInMethod.Local, email: "inactive-admin@corp.test", isActive: false);
            }

            var emailSender = new RecordingEmailSender();
            var settings = new StubSystemSettingsReader().WithSsoMode("OpenIdConnect").WithMfaPolicy("RequiredForAll");

            LoginOutcome step1;
            await using (var actDb1 = ScratchPostgresDatabase.NewContext(cs))
            {
                var (auth, _, _, _) = CreateServices(actDb1, clock, settings, emailSender);
                step1 = await auth.LoginAsync(emergencyAdmin.Username, Password, "127.0.0.1", "TestAgent");
            }

            Assert.True(step1.Success);
            Assert.True(step1.MfaRequired);
            Assert.NotNull(step1.MfaToken);
            Assert.Null(step1.Token);

            var code = Totp.Generate(emergencySecret, Totp.StepAt(clock.GetUtcNow().UtcDateTime, 30));
            LoginOutcome step2;
            await using (var actDb2 = ScratchPostgresDatabase.NewContext(cs))
            {
                var (_, mfa, _, _) = CreateServices(actDb2, clock, settings, emailSender);
                step2 = await mfa.VerifyAsync(step1.MfaToken!, code, null, false, "127.0.0.1", "TestAgent");
            }

            Assert.True(step2.Success);
            Assert.NotNull(step2.Token);
            Assert.NotNull(step2.RefreshToken);

            await using var assertDb = ScratchPostgresDatabase.NewContext(cs);
            Assert.True(await assertDb.SecurityAuditEvents.AsNoTracking()
                .AnyAsync(e => e.TargetUserId == emergencyAdmin.Id && e.EventCode == SecurityEventCodes.EmergencyLogin && e.Outcome == SecurityEventOutcome.Success));

            var recipients = emailSender.Sent.Select(s => s.To).ToHashSet(StringComparer.OrdinalIgnoreCase);
            Assert.Contains(emergencyAdmin.Email!, recipients);
            Assert.Contains(otherActiveAdmin.Email!, recipients);
            Assert.DoesNotContain(inactiveAdmin.Email!, recipients);
        });
    }

    [PostgresFact]
    public async Task EmergencyLogin_IgnoresTrustedDeviceToken_AlwaysAsksCode()
    {
        var clock = new FakeTimeProvider(BaseUtc);
        User emergencyAdmin;
        byte[] secret;
        string deviceToken;

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            (emergencyAdmin, var s) = await SeedUserAsync(arrangeDb, RoleType.SystemAdministrator, SignInMethod.Local, withActiveMfa: true, isEmergency: true);
            secret = s!;
        }

        // First obtain a valid trusted device token while SSO is Off.
        var settingsOff = new StubSystemSettingsReader().WithSsoMode("Off").WithMfaPolicy("RequiredForAll");
        string mfaTokenOff;
        await using (var db1 = _fixture.CreateDbContext())
        {
            var (auth, _, _, _) = CreateServices(db1, clock, settingsOff);
            var step1 = await auth.LoginAsync(emergencyAdmin.Username, Password, "127.0.0.1", "TestAgent");
            Assert.True(step1.MfaRequired);
            mfaTokenOff = step1.MfaToken!;
        }

        await using (var db2 = _fixture.CreateDbContext())
        {
            var (_, mfa, _, _) = CreateServices(db2, clock, settingsOff);
            var code = Totp.Generate(secret, Totp.StepAt(clock.GetUtcNow().UtcDateTime, 30));
            var step2 = await mfa.VerifyAsync(mfaTokenOff, code, null, trustDevice: true, "127.0.0.1", "TestAgent");
            Assert.True(step2.Success);
            Assert.NotNull(step2.DeviceToken);
            deviceToken = step2.DeviceToken!;
        }

        // Now with SSO on, supplying that valid deviceToken MUST still require MFA.
        await using var db3 = _fixture.CreateDbContext();
        var settingsOn = new StubSystemSettingsReader().WithSsoMode("OpenIdConnect").WithMfaPolicy("RequiredForAll");
        var (authOn, _, _, _) = CreateServices(db3, clock, settingsOn);
        var ssoLogin = await authOn.LoginAsync(emergencyAdmin.Username, Password, "127.0.0.1", "TestAgent", deviceToken);

        Assert.True(ssoLogin.Success);
        Assert.True(ssoLogin.MfaRequired);
        Assert.NotNull(ssoLogin.MfaToken);
        Assert.Null(ssoLogin.Token);
    }

    [PostgresFact]
    public async Task MarkEmergency_NonAdministrator_Throws()
    {
        var clock = new FakeTimeProvider(BaseUtc);
        User admin;
        User analyst;
        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            (admin, _) = await SeedUserAsync(arrangeDb, RoleType.SystemAdministrator);
            (analyst, _) = await SeedUserAsync(arrangeDb, RoleType.Analyst, withActiveMfa: true);
        }

        await using var actDb = _fixture.CreateDbContext();
        var (_, _, users, _) = CreateServices(actDb, clock);
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            users.SetEmergencyAccountAsync(analyst.Id, true, "Break glass", admin.Id));
    }

    [PostgresFact]
    public async Task MarkEmergency_WithoutSecondFactor_Throws()
    {
        var clock = new FakeTimeProvider(BaseUtc);
        User actingAdmin;
        User targetAdmin;
        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            (actingAdmin, _) = await SeedUserAsync(arrangeDb, RoleType.SystemAdministrator);
            (targetAdmin, _) = await SeedUserAsync(arrangeDb, RoleType.SystemAdministrator, withActiveMfa: false);
        }

        await using var actDb = _fixture.CreateDbContext();
        var (_, _, users, _) = CreateServices(actDb, clock);
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            users.SetEmergencyAccountAsync(targetAdmin.Id, true, "Break glass", actingAdmin.Id));
    }

    [PostgresFact]
    public async Task MarkEmergency_FourthAccount_Throws()
    {
        await ScratchPostgresDatabase.RunAsync(async cs =>
        {
            var clock = new FakeTimeProvider(BaseUtc);
            User actingAdmin;
            User fourthAdmin;
            await using (var arrangeDb = ScratchPostgresDatabase.NewContext(cs))
            {
                await arrangeDb.Database.MigrateAsync();
                (actingAdmin, _) = await SeedUserAsync(arrangeDb, RoleType.SystemAdministrator, withActiveMfa: true, isEmergency: true);
                await SeedUserAsync(arrangeDb, RoleType.SystemAdministrator, withActiveMfa: true, isEmergency: true);
                await SeedUserAsync(arrangeDb, RoleType.SystemAdministrator, withActiveMfa: true, isEmergency: true);
                (fourthAdmin, _) = await SeedUserAsync(arrangeDb, RoleType.SystemAdministrator, withActiveMfa: true, isEmergency: false);
            }

            await using var actDb = ScratchPostgresDatabase.NewContext(cs);
            var (_, _, users, _) = CreateServices(actDb, clock);
            await Assert.ThrowsAsync<BusinessRuleException>(() =>
                users.SetEmergencyAccountAsync(fourthAdmin.Id, true, "Too many break-glass accounts", actingAdmin.Id));
        });
    }

    [PostgresFact]
    public async Task SwitchOn_WithoutConnectionTest_Refused()
    {
        await ScratchPostgresDatabase.RunAsync(async cs =>
        {
            var clock = new FakeTimeProvider(BaseUtc);
            User actingAdmin;
            await using (var arrangeDb = ScratchPostgresDatabase.NewContext(cs))
            {
                await arrangeDb.Database.MigrateAsync();
                (actingAdmin, _) = await SeedUserAsync(
                    arrangeDb, RoleType.SystemAdministrator, SignInMethod.Local, withActiveMfa: true, isEmergency: true, directoryUsername: "admin@corp.test");
            }

            await using var actDb = ScratchPostgresDatabase.NewContext(cs);
            var settings = CreateSettingsService(actDb, clock);
            var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                settings.UpdateAsync(SystemSettingKeys.SsoMode, "OpenIdConnect", "Enable SSO", actingAdmin.Id, Password, "127.0.0.1"));
            Assert.Contains(SsoModeService.BlockerConnectionTestRequired, ex.Message);
        });
    }

    [PostgresFact]
    public async Task SwitchOn_WithoutEmergencyAccount_Refused()
    {
        await ScratchPostgresDatabase.RunAsync(async cs =>
        {
            var clock = new FakeTimeProvider(BaseUtc);
            var fp = new MutableFingerprint();
            User actingAdmin;
            await using (var arrangeDb = ScratchPostgresDatabase.NewContext(cs))
            {
                await arrangeDb.Database.MigrateAsync();
                (actingAdmin, _) = await SeedUserAsync(
                    arrangeDb, RoleType.SystemAdministrator, SignInMethod.Local, withActiveMfa: true, isEmergency: false, directoryUsername: "admin@corp.test");
                var (_, _, _, ssoMode) = CreateServices(arrangeDb, clock, fingerprint: fp);
                await ssoMode.RecordConnectionTestPassedAsync(SsoMode.OpenIdConnect, actingAdmin.Id);
            }

            await using var actDb = ScratchPostgresDatabase.NewContext(cs);
            var settings = CreateSettingsService(actDb, clock, fp);
            var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                settings.UpdateAsync(SystemSettingKeys.SsoMode, "OpenIdConnect", "Enable SSO", actingAdmin.Id, Password, "127.0.0.1"));
            Assert.Contains(SsoModeService.BlockerEmergencyAdminRequired, ex.Message);
        });
    }

    [PostgresFact]
    public async Task SwitchOn_ActingAdminNotLinked_Refused()
    {
        await ScratchPostgresDatabase.RunAsync(async cs =>
        {
            var clock = new FakeTimeProvider(BaseUtc);
            var fp = new MutableFingerprint();
            User actingAdmin;
            await using (var arrangeDb = ScratchPostgresDatabase.NewContext(cs))
            {
                await arrangeDb.Database.MigrateAsync();
                (actingAdmin, _) = await SeedUserAsync(
                    arrangeDb, RoleType.SystemAdministrator, SignInMethod.Local, withActiveMfa: true, isEmergency: true, directoryUsername: null);
                var (_, _, _, ssoMode) = CreateServices(arrangeDb, clock, fingerprint: fp);
                await ssoMode.RecordConnectionTestPassedAsync(SsoMode.OpenIdConnect, actingAdmin.Id);
            }

            await using var actDb = ScratchPostgresDatabase.NewContext(cs);
            var settings = CreateSettingsService(actDb, clock, fp);
            var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                settings.UpdateAsync(SystemSettingKeys.SsoMode, "OpenIdConnect", "Enable SSO", actingAdmin.Id, Password, "127.0.0.1"));
            Assert.Contains(SsoModeService.BlockerActingAdminNotLinked, ex.Message);
        });
    }

    [PostgresFact]
    public async Task SwitchOn_AllConditionsMet_Succeeds_Audited()
    {
        await ScratchPostgresDatabase.RunAsync(async cs =>
        {
            var clock = new FakeTimeProvider(BaseUtc);
            var fp = new MutableFingerprint();
            User actingAdmin;
            await using (var arrangeDb = ScratchPostgresDatabase.NewContext(cs))
            {
                await arrangeDb.Database.MigrateAsync();
                (actingAdmin, _) = await SeedUserAsync(
                    arrangeDb, RoleType.SystemAdministrator, SignInMethod.Local, withActiveMfa: true, isEmergency: true, directoryUsername: "admin@corp.test");
                var (_, _, _, ssoMode) = CreateServices(arrangeDb, clock, fingerprint: fp);
                await ssoMode.RecordConnectionTestPassedAsync(SsoMode.OpenIdConnect, actingAdmin.Id);
            }

            await using (var actDb = ScratchPostgresDatabase.NewContext(cs))
            {
                var settings = CreateSettingsService(actDb, clock, fp);
                var dto = await settings.UpdateAsync(SystemSettingKeys.SsoMode, "OpenIdConnect", "Enable OIDC", actingAdmin.Id, Password, "127.0.0.1");
                Assert.Equal("OpenIdConnect", dto.Value);
            }

            await using var assertDb = ScratchPostgresDatabase.NewContext(cs);
            var stored = await assertDb.SystemSettings.AsNoTracking().SingleAsync(s => s.Key == SystemSettingKeys.SsoMode);
            Assert.Equal("OpenIdConnect", stored.Value);
            Assert.True(await assertDb.AuditLogs.AsNoTracking().AnyAsync(a => a.Action == "SYSTEM_SETTING_CHANGED" && a.Reason == "Enable OIDC"));
        });
    }

    [PostgresFact]
    public async Task SwitchOff_IsAlwaysAllowed()
    {
        await ScratchPostgresDatabase.RunAsync(async cs =>
        {
            var clock = new FakeTimeProvider(BaseUtc);
            var fp = new MutableFingerprint();
            User actingAdmin;
            await using (var arrangeDb = ScratchPostgresDatabase.NewContext(cs))
            {
                await arrangeDb.Database.MigrateAsync();
                (actingAdmin, _) = await SeedUserAsync(
                    arrangeDb, RoleType.SystemAdministrator, SignInMethod.Local, withActiveMfa: true, isEmergency: true, directoryUsername: "admin@corp.test");
                var (_, _, _, ssoMode) = CreateServices(arrangeDb, clock, fingerprint: fp);
                await ssoMode.RecordConnectionTestPassedAsync(SsoMode.OpenIdConnect, actingAdmin.Id);
            }

            await using (var onDb = ScratchPostgresDatabase.NewContext(cs))
            {
                var settings = CreateSettingsService(onDb, clock, fp);
                await settings.UpdateAsync(SystemSettingKeys.SsoMode, "OpenIdConnect", "Enable OIDC", actingAdmin.Id, Password, "127.0.0.1");
            }

            // Invalidate test and unlink acting admin — switching Off must STILL succeed.
            await using (var modDb = ScratchPostgresDatabase.NewContext(cs))
            {
                var u = await modDb.Users.SingleAsync(x => x.Id == actingAdmin.Id);
                u.IsEmergencyAccount = false;
                u.DirectoryUsername = null;
                await modDb.SaveChangesAsync();
            }

            await using (var offDb = ScratchPostgresDatabase.NewContext(cs))
            {
                var settings = CreateSettingsService(offDb, clock, fp);
                var dto = await settings.UpdateAsync(SystemSettingKeys.SsoMode, "Off", "Disable SSO", actingAdmin.Id, Password, "127.0.0.1");
                Assert.Equal("Off", dto.Value);
            }

            await using var assertDb = ScratchPostgresDatabase.NewContext(cs);
            var stored = await assertDb.SystemSettings.AsNoTracking().SingleAsync(s => s.Key == SystemSettingKeys.SsoMode);
            Assert.Equal("Off", stored.Value);
        });
    }

    [PostgresFact]
    public async Task AdMode_DirectoryUser_WithoutDirectoryService_IsRefused_NotLoggedInLocally()
    {
        var clock = new FakeTimeProvider(BaseUtc);
        User user;
        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            (user, _) = await SeedUserAsync(arrangeDb, RoleType.Analyst, SignInMethod.Directory, directoryUsername: $"ad.{Guid.NewGuid():N}@corp.test");
        }

        await using var actDb = _fixture.CreateDbContext();
        var (auth, _, _, _) = CreateServices(actDb, clock, new StubSystemSettingsReader().WithSsoMode("ActiveDirectory").WithMfaPolicy("Off"));
        var outcome = await auth.LoginAsync(user.Username, Password, "127.0.0.1", "TestAgent");

        Assert.False(outcome.Success);
        Assert.Null(outcome.Token);
        Assert.Null(outcome.RefreshToken);
        Assert.Equal("Invalid username or password.", outcome.FailureReason);
    }

    [PostgresFact]
    public async Task EmergencyLogin_PolicyOff_StillAsksCode()
    {
        var clock = new FakeTimeProvider(BaseUtc);
        User emergencyAdmin;
        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            (emergencyAdmin, _) = await SeedUserAsync(arrangeDb, RoleType.SystemAdministrator, SignInMethod.Local, withActiveMfa: true, isEmergency: true);
        }

        await using var actDb = _fixture.CreateDbContext();
        var (auth, _, _, _) = CreateServices(actDb, clock, new StubSystemSettingsReader().WithSsoMode("OpenIdConnect").WithMfaPolicy("Off"));
        var outcome = await auth.LoginAsync(emergencyAdmin.Username, Password, "127.0.0.1", "TestAgent");

        Assert.True(outcome.Success);
        Assert.True(outcome.MfaRequired);
        Assert.NotNull(outcome.MfaToken);
        Assert.Null(outcome.Token);
    }

    [PostgresFact]
    public async Task OidcMode_Refusal_TakesTheSamePathAsWrongPassword_NoMfaChallengeCreated()
    {
        var clock = new FakeTimeProvider(BaseUtc);
        User user;
        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            (user, _) = await SeedUserAsync(arrangeDb, RoleType.Analyst, SignInMethod.Directory, withActiveMfa: true, directoryUsername: $"oidc.{Guid.NewGuid():N}@corp.test");
        }

        LoginOutcome outcome;
        await using (var actDb = _fixture.CreateDbContext())
        {
            var (auth, _, _, _) = CreateServices(actDb, clock, new StubSystemSettingsReader().WithSsoMode("OpenIdConnect").WithMfaPolicy("RequiredForAll"));
            outcome = await auth.LoginAsync(user.Username, Password, "127.0.0.1", "TestAgent");
        }

        Assert.False(outcome.Success);
        Assert.False(outcome.MfaRequired);
        Assert.Null(outcome.MfaToken);
        Assert.Equal("Invalid username or password.", outcome.FailureReason);

        await using var assertDb = _fixture.CreateDbContext();
        Assert.False(await assertDb.MfaChallenges.AsNoTracking().AnyAsync(c => c.UserId == user.Id));
    }

    [PostgresFact]
    public async Task MarkEmergency_DirectoryUser_Throws()
    {
        var clock = new FakeTimeProvider(BaseUtc);
        User actingAdmin;
        User directoryAdmin;
        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            (actingAdmin, _) = await SeedUserAsync(arrangeDb, RoleType.SystemAdministrator);
            (directoryAdmin, _) = await SeedUserAsync(arrangeDb, RoleType.SystemAdministrator, SignInMethod.Directory, withActiveMfa: true, directoryUsername: $"diradmin.{Guid.NewGuid():N}@corp.test");
        }

        await using var actDb = _fixture.CreateDbContext();
        var (_, _, users, _) = CreateServices(actDb, clock);
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            users.SetEmergencyAccountAsync(directoryAdmin.Id, true, "Emergency admin", actingAdmin.Id));
    }

    [PostgresFact]
    public async Task SwitchOn_TestPassedForOtherMode_Refused()
    {
        await ScratchPostgresDatabase.RunAsync(async cs =>
        {
            var clock = new FakeTimeProvider(BaseUtc);
            var fp = new MutableFingerprint();
            User actingAdmin;
            await using (var arrangeDb = ScratchPostgresDatabase.NewContext(cs))
            {
                await arrangeDb.Database.MigrateAsync();
                (actingAdmin, _) = await SeedUserAsync(
                    arrangeDb, RoleType.SystemAdministrator, SignInMethod.Local, withActiveMfa: true, isEmergency: true, directoryUsername: "admin@corp.test");
                var (_, _, _, ssoMode) = CreateServices(arrangeDb, clock, fingerprint: fp);
                await ssoMode.RecordConnectionTestPassedAsync(SsoMode.ActiveDirectory, actingAdmin.Id);
            }

            await using var actDb = ScratchPostgresDatabase.NewContext(cs);
            var settings = CreateSettingsService(actDb, clock, fp);
            var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                settings.UpdateAsync(SystemSettingKeys.SsoMode, "OpenIdConnect", "Enable OIDC", actingAdmin.Id, Password, "127.0.0.1"));
            Assert.Contains(SsoModeService.BlockerConnectionTestRequired, ex.Message);
        });
    }

    [PostgresFact]
    public async Task SwitchOn_TestPassedThenConfigurationChanged_Refused()
    {
        await ScratchPostgresDatabase.RunAsync(async cs =>
        {
            var clock = new FakeTimeProvider(BaseUtc);
            var fp = new MutableFingerprint { Value = "fp-initial" };
            User actingAdmin;
            await using (var arrangeDb = ScratchPostgresDatabase.NewContext(cs))
            {
                await arrangeDb.Database.MigrateAsync();
                (actingAdmin, _) = await SeedUserAsync(
                    arrangeDb, RoleType.SystemAdministrator, SignInMethod.Local, withActiveMfa: true, isEmergency: true, directoryUsername: "admin@corp.test");
                var (_, _, _, ssoMode) = CreateServices(arrangeDb, clock, fingerprint: fp);
                await ssoMode.RecordConnectionTestPassedAsync(SsoMode.OpenIdConnect, actingAdmin.Id);
            }

            fp.Value = "fp-changed";
            await using var actDb = ScratchPostgresDatabase.NewContext(cs);
            var settings = CreateSettingsService(actDb, clock, fp);
            var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                settings.UpdateAsync(SystemSettingKeys.SsoMode, "OpenIdConnect", "Enable OIDC", actingAdmin.Id, Password, "127.0.0.1"));
            Assert.Contains(SsoModeService.BlockerConnectionTestRequired, ex.Message);
        });
    }

    [PostgresFact]
    public async Task SwitchOn_TestPassedThenFailed_Refused()
    {
        await ScratchPostgresDatabase.RunAsync(async cs =>
        {
            var clock = new FakeTimeProvider(BaseUtc);
            var fp = new MutableFingerprint();
            User actingAdmin;
            await using (var arrangeDb = ScratchPostgresDatabase.NewContext(cs))
            {
                await arrangeDb.Database.MigrateAsync();
                (actingAdmin, _) = await SeedUserAsync(
                    arrangeDb, RoleType.SystemAdministrator, SignInMethod.Local, withActiveMfa: true, isEmergency: true, directoryUsername: "admin@corp.test");
                var (_, _, _, ssoMode) = CreateServices(arrangeDb, clock, fingerprint: fp);
                await ssoMode.RecordConnectionTestPassedAsync(SsoMode.OpenIdConnect, actingAdmin.Id);
                await ssoMode.RecordConnectionTestFailedAsync(SsoMode.OpenIdConnect, "Discovery endpoint returned 503", actingAdmin.Id);
            }

            await using var actDb = ScratchPostgresDatabase.NewContext(cs);
            var settings = CreateSettingsService(actDb, clock, fp);
            var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                settings.UpdateAsync(SystemSettingKeys.SsoMode, "OpenIdConnect", "Enable OIDC", actingAdmin.Id, Password, "127.0.0.1"));
            Assert.Contains(SsoModeService.BlockerConnectionTestRequired, ex.Message);
        });
    }

    [PostgresFact]
    public async Task EmergencyLogin_ThroughForcedEnrolment_RecordsEvent_EmailsAdministrators()
    {
        await ScratchPostgresDatabase.RunAsync(async cs =>
        {
            var clock = new FakeTimeProvider(BaseUtc);
            User emergencyAdmin;
            User otherActiveAdmin;
            User inactiveAdmin;

            await using (var arrangeDb = ScratchPostgresDatabase.NewContext(cs))
            {
                await arrangeDb.Database.MigrateAsync();
                (emergencyAdmin, _) = await SeedUserAsync(
                    arrangeDb, RoleType.SystemAdministrator, SignInMethod.Local, withActiveMfa: false, isEmergency: true, email: "emerg-enrol@corp.test");
                (otherActiveAdmin, _) = await SeedUserAsync(
                    arrangeDb, RoleType.SystemAdministrator, SignInMethod.Directory, email: "admin2-enrol@corp.test", directoryUsername: "admin2-enrol@corp.test");
                (inactiveAdmin, _) = await SeedUserAsync(
                    arrangeDb, RoleType.SystemAdministrator, SignInMethod.Local, email: "inactive-enrol@corp.test", isActive: false);
            }

            var emailSender = new RecordingEmailSender();
            var settings = new StubSystemSettingsReader().WithSsoMode("OpenIdConnect").WithMfaPolicy("RequiredForAll");

            LoginOutcome step1;
            await using (var actDb1 = ScratchPostgresDatabase.NewContext(cs))
            {
                var (auth, _, _, _) = CreateServices(actDb1, clock, settings, emailSender);
                step1 = await auth.LoginAsync(emergencyAdmin.Username, Password, "127.0.0.1", "TestAgent");
            }

            Assert.True(step1.Success);
            Assert.True(step1.MfaEnrolmentRequired);
            Assert.NotNull(step1.MfaToken);
            Assert.Null(step1.Token);

            EnrolmentStart started;
            await using (var actDb2 = ScratchPostgresDatabase.NewContext(cs))
            {
                var (_, mfa, _, _) = CreateServices(actDb2, clock, settings, emailSender);
                started = await mfa.StartEnrolmentWithTokenAsync(step1.MfaToken!);
            }

            var secret = Totp.FromBase32(started.ManualKey);
            var code = Totp.Generate(secret, Totp.StepAt(clock.GetUtcNow().UtcDateTime, 30));

            EnrolmentResult step3;
            await using (var actDb3 = ScratchPostgresDatabase.NewContext(cs))
            {
                var (_, mfa, _, _) = CreateServices(actDb3, clock, settings, emailSender);
                step3 = await mfa.ConfirmEnrolmentWithTokenAsync(step1.MfaToken!, code, "127.0.0.1", "TestAgent");
            }

            Assert.NotNull(step3.Login);
            Assert.True(step3.Login!.Success);
            Assert.NotNull(step3.Login.Token);
            Assert.NotNull(step3.Login.RefreshToken);

            await using var assertDb = ScratchPostgresDatabase.NewContext(cs);
            Assert.True(await assertDb.SecurityAuditEvents.AsNoTracking()
                .AnyAsync(e => e.TargetUserId == emergencyAdmin.Id && e.EventCode == SecurityEventCodes.EmergencyLogin && e.Outcome == SecurityEventOutcome.Success));

            var recipients = emailSender.Sent.Select(s => s.To).ToHashSet(StringComparer.OrdinalIgnoreCase);
            Assert.Contains(emergencyAdmin.Email!, recipients);
            Assert.Contains(otherActiveAdmin.Email!, recipients);
            Assert.DoesNotContain(inactiveAdmin.Email!, recipients);
        });
    }
}
