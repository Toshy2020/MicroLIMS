using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MicroLIMS.Application.Abstractions.Email;
using MicroLIMS.Application.Abstractions.Sso;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.Mfa;
using MicroLIMS.Application.Services.Sso;
using MicroLIMS.Application.Services.SystemSettings;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

// Session cap on refresh (OpenID Connect mode) and the Active Directory leaver run.
[Collection("PostgresDatabaseCollection")]
public class DirectoryLeaverPostgresTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 13, 6, 0, 0, TimeSpan.Zero);

    private readonly PostgresTestFixture _fixture;

    public DirectoryLeaverPostgresTests(PostgresTestFixture fixture) => _fixture = fixture;

    private sealed class MutableTimeProvider : TimeProvider
    {
        private DateTimeOffset _now;
        public MutableTimeProvider(DateTimeOffset now) => _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Set(DateTimeOffset now) => _now = now;
    }

    private sealed class StubSettings : ISystemSettingsReader
    {
        private readonly string _mode;
        public StubSettings(SsoMode mode) => _mode = mode.ToString();

        public Task<int> GetIntAsync(string key, CancellationToken cancellationToken = default) => Task.FromResult(key switch
        {
            SystemSettingKeys.SsoSessionMaxHours => 12,
            SystemSettingKeys.MfaTrustedDeviceHours => 12,
            SystemSettingKeys.MfaEnrolmentGraceDays => 7,
            SystemSettingKeys.LockoutMaxFailedAttempts => AuthenticationService.MaxFailedAttempts,
            SystemSettingKeys.LockoutDurationMinutes => (int)AuthenticationService.LockDuration.TotalMinutes,
            SystemSettingKeys.RefreshTokenLifetimeDays => 7,
            _ => throw new NotSupportedException($"Unexpected int setting read: {key}")
        });

        public Task<bool> GetBoolAsync(string key, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException($"Unexpected bool setting read: {key}");

        public Task<string> GetStringAsync(string key, CancellationToken cancellationToken = default) => Task.FromResult(key switch
        {
            SystemSettingKeys.SsoMode => _mode,
            SystemSettingKeys.MfaPolicy => nameof(MfaPolicy.Off),
            SystemSettingKeys.MfaRequiredRoles => "SystemAdministrator",
            _ => throw new NotSupportedException($"Unexpected string setting read: {key}")
        });
    }

    private sealed class RecordingEmailSender : IEmailSender
    {
        public List<(string To, string Subject, string Body)> Sent { get; } = new();

        public Task SendAsync(string to, string subject, string body)
        {
            Sent.Add((to, subject, body));
            return Task.CompletedTask;
        }
    }

    // Records, at the moment each email is sent, whether the account was already closed in the database.
    private sealed class CommitCheckingEmailSender : IEmailSender
    {
        private readonly PostgresTestFixture _fixture;
        private readonly int _userId;
        public CommitCheckingEmailSender(PostgresTestFixture fixture, int userId) { _fixture = fixture; _userId = userId; }
        public List<bool> LeaverAlreadyInactiveWhenSent { get; } = new();

        public async Task SendAsync(string to, string subject, string body)
        {
            await using var db = _fixture.CreateDbContext();
            LeaverAlreadyInactiveWhenSent.Add(!await db.Users.AsNoTracking().Where(u => u.Id == _userId).Select(u => u.IsActive).SingleAsync());
        }
    }

    private sealed class Services
    {
        public required AuthenticationService Auth { get; init; }
        public required DirectoryLeaverService Leaver { get; init; }
    }

    private static Services Create(
        MicroLimsDbContext db, TimeProvider clock, SsoMode mode, FakeLdapDirectory? directory = null, IEmailSender? email = null)
    {
        var settings = new StubSettings(mode);
        var audit = new SecurityAuditService(db, new SystemSecurityRequestContext(), clock);
        var ssoMode = new SsoModeService(db, settings, audit);
        var sender = email ?? new RecordingEmailSender();
        AccessTokenIssuer issuer = s => $"jwt-{s.UserId}-{Guid.NewGuid():N}";
        var auth = new AuthenticationService(
            db, issuer, new PermissionService(db), sender, NullLogger<AuthenticationService>.Instance, audit, clock,
            settings, new MfaPolicyService(settings), ssoMode, directorySignIn: null);
        var leaver = new DirectoryLeaverService(
            db, directory ?? new FakeLdapDirectory(), ssoMode, auth, audit, sender, clock,
            NullLogger<DirectoryLeaverService>.Instance);
        return new Services { Auth = auth, Leaver = leaver };
    }

    private async Task<User> SeedUserAsync(
        SignInMethod method = SignInMethod.Directory,
        bool emergency = false,
        string? subject = null,
        bool active = true,
        RoleType role = RoleType.Analyst)
    {
        await using var db = _fixture.CreateDbContext();
        var roleId = await db.Roles.AsNoTracking().Where(r => r.Type == role).OrderBy(r => r.Id).Select(r => r.Id).FirstAsync();
        var tag = Guid.NewGuid().ToString("N");
        var user = new User
        {
            FullName = $"Leaver Test {tag[..8]}",
            Username = $"leaver_{tag}",
            Email = $"leaver_{tag}@example.test",
            PasswordHash = TestPasswords.Hash("ValidPassword1!"),
            RoleId = roleId,
            IsActive = active,
            CreatedAt = T0.UtcDateTime,
            SignInMethod = method,
            IsEmergencyAccount = emergency,
            DirectoryUsername = method == SignInMethod.Directory ? $"jdoe.{tag[..12]}" : null,
            DirectorySubjectId = subject,
            SigningPasswordSetAt = T0.UtcDateTime
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private async Task SeedActiveSessionAsync(int userId, DateTime? sessionStartedAt = null)
    {
        await using var db = _fixture.CreateDbContext();
        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = userId,
            TokenHash = Guid.NewGuid().ToString("N"),
            ExpiresAt = T0.UtcDateTime.AddDays(3),
            CreatedAt = T0.UtcDateTime,
            SessionStartedAt = sessionStartedAt
        });
        await db.SaveChangesAsync();
    }

    // Signs the user in (issues a first refresh token) at the clock's current time.
    private async Task<string> LoginAsync(User user, MutableTimeProvider clock, SsoMode mode)
    {
        await using var db = _fixture.CreateDbContext();
        var svc = Create(db, clock, mode);
        var tracked = await db.Users.Include(u => u.Role).SingleAsync(u => u.Id == user.Id);
        var outcome = await svc.Auth.CompleteLoginAsync(tracked, "127.0.0.1", "TestAgent", mfaVerified: true);
        Assert.True(outcome.Success);
        return outcome.RefreshToken!;
    }

    private async Task<LoginOutcome> RefreshAsync(string token, MutableTimeProvider clock, SsoMode mode)
    {
        await using var db = _fixture.CreateDbContext();
        return await Create(db, clock, mode).Auth.RefreshAsync(token, "127.0.0.1", "TestAgent");
    }

    private async Task<LeaverRunResult> RunLeaverAsync(
        SsoMode mode, FakeLdapDirectory directory, IEmailSender? email = null, TimeProvider? clock = null)
    {
        await using var db = _fixture.CreateDbContext();
        return await Create(db, clock ?? new MutableTimeProvider(T0), mode, directory, email).Leaver.RunAsync();
    }

    private static FakeLdapDirectory NewDirectory() => new() { UnknownIdsAreOmitted = true };

    // ---- session cap -------------------------------------------------------

    [PostgresFact]
    public async Task Oidc_RefreshAt11h59_Succeeds_At12h01_Refused()
    {
        var user = await SeedUserAsync(subject: $"sub-{Guid.NewGuid():N}");
        var clock = new MutableTimeProvider(T0);
        var first = await LoginAsync(user, clock, SsoMode.OpenIdConnect);

        clock.Set(T0.AddHours(6));
        var second = await RefreshAsync(first, clock, SsoMode.OpenIdConnect);
        Assert.True(second.Success);

        clock.Set(T0.AddHours(11).AddMinutes(59));
        var third = await RefreshAsync(second.RefreshToken!, clock, SsoMode.OpenIdConnect);
        Assert.True(third.Success);

        clock.Set(T0.AddHours(12).AddMinutes(1));
        var refused = await RefreshAsync(third.RefreshToken!, clock, SsoMode.OpenIdConnect);
        Assert.False(refused.Success);
        Assert.Null(refused.Token);
        Assert.Null(refused.RefreshToken);
        Assert.Equal("Refresh token is invalid or expired.", refused.FailureReason);

        await using var assertDb = _fixture.CreateDbContext();
        var tokens = await assertDb.RefreshTokens.AsNoTracking().Where(r => r.UserId == user.Id).OrderBy(r => r.Id).ToListAsync();
        Assert.Equal(3, tokens.Count);
        // Rotation never reset the start: every token in the chain carries the login time.
        Assert.All(tokens, t => Assert.Equal(T0.UtcDateTime, t.SessionStartedAt));
        // The refused token is closed, and no new one was issued.
        Assert.All(tokens, t => Assert.NotNull(t.RevokedAt));
        Assert.Equal(T0.AddHours(12).AddMinutes(1).UtcDateTime, tokens[2].RevokedAt);

        var evt = await assertDb.SecurityAuditEvents.AsNoTracking()
            .SingleAsync(e => e.TargetUserId == user.Id && e.EventCode == SecurityEventCodes.RefreshRejectedInvalidToken);
        Assert.Equal("session_cap", evt.Reason);
        Assert.Equal(SecurityEventOutcome.Failure, evt.Outcome);
    }

    [PostgresFact]
    public async Task Oidc_EmergencyAccount_NotCapped()
    {
        var user = await SeedUserAsync(method: SignInMethod.Local, emergency: true, role: RoleType.SystemAdministrator);
        var clock = new MutableTimeProvider(T0);
        var first = await LoginAsync(user, clock, SsoMode.OpenIdConnect);

        clock.Set(T0.AddHours(30));
        var outcome = await RefreshAsync(first, clock, SsoMode.OpenIdConnect);

        Assert.True(outcome.Success);
        await using var assertDb = _fixture.CreateDbContext();
        Assert.False(await assertDb.SecurityAuditEvents.AsNoTracking()
            .AnyAsync(e => e.TargetUserId == user.Id && e.Reason == "session_cap"));
    }

    [PostgresFact]
    public async Task Oidc_LocalUser_NotCapped()
    {
        var user = await SeedUserAsync(method: SignInMethod.Local);
        var clock = new MutableTimeProvider(T0);
        var first = await LoginAsync(user, clock, SsoMode.OpenIdConnect);

        clock.Set(T0.AddHours(30));
        Assert.True((await RefreshAsync(first, clock, SsoMode.OpenIdConnect)).Success);
    }

    [PostgresFact]
    public async Task ModeOff_NotCapped()
    {
        var user = await SeedUserAsync(subject: $"sub-{Guid.NewGuid():N}");
        var clock = new MutableTimeProvider(T0);
        var first = await LoginAsync(user, clock, SsoMode.Off);

        clock.Set(T0.AddHours(30));
        Assert.True((await RefreshAsync(first, clock, SsoMode.Off)).Success);
    }

    [PostgresFact]
    public async Task ActiveDirectoryMode_RefreshNotCapped_TheLeaverWorkerCoversIt()
    {
        var user = await SeedUserAsync(subject: $"sub-{Guid.NewGuid():N}");
        var clock = new MutableTimeProvider(T0);
        var first = await LoginAsync(user, clock, SsoMode.ActiveDirectory);

        clock.Set(T0.AddHours(30));
        Assert.True((await RefreshAsync(first, clock, SsoMode.ActiveDirectory)).Success);
    }

    [PostgresFact]
    public async Task Oidc_LegacyTokenWithoutStart_CountsFromItsCreation_AndCarriesThatStart()
    {
        var user = await SeedUserAsync(subject: $"sub-{Guid.NewGuid():N}");
        var rawToken = $"legacy-{Guid.NewGuid():N}";
        await using (var seedDb = _fixture.CreateDbContext())
        {
            seedDb.RefreshTokens.Add(new RefreshToken
            {
                UserId = user.Id,
                TokenHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(rawToken))),
                ExpiresAt = T0.UtcDateTime.AddDays(7),
                CreatedAt = T0.UtcDateTime,
                SessionStartedAt = null
            });
            await seedDb.SaveChangesAsync();
        }

        var clock = new MutableTimeProvider(T0.AddHours(8));
        var second = await RefreshAsync(rawToken, clock, SsoMode.OpenIdConnect);
        Assert.True(second.Success);

        await using (var assertDb = _fixture.CreateDbContext())
        {
            var rotated = await assertDb.RefreshTokens.AsNoTracking()
                .SingleAsync(r => r.UserId == user.Id && r.RevokedAt == null);
            Assert.Equal(T0.UtcDateTime, rotated.SessionStartedAt);
        }

        clock.Set(T0.AddHours(13));
        Assert.False((await RefreshAsync(second.RefreshToken!, clock, SsoMode.OpenIdConnect)).Success);
    }

    [PostgresFact]
    public async Task Login_SetsSessionStartedAt_RotationCarriesIt()
    {
        var user = await SeedUserAsync(method: SignInMethod.Local);
        var clock = new MutableTimeProvider(T0);
        var first = await LoginAsync(user, clock, SsoMode.Off);

        clock.Set(T0.AddHours(2));
        var second = await RefreshAsync(first, clock, SsoMode.Off);
        Assert.True(second.Success);

        await using var assertDb = _fixture.CreateDbContext();
        var tokens = await assertDb.RefreshTokens.AsNoTracking().Where(r => r.UserId == user.Id).OrderBy(r => r.Id).ToListAsync();
        Assert.Equal(2, tokens.Count);
        Assert.Equal(T0.UtcDateTime, tokens[0].SessionStartedAt);
        Assert.Equal(T0.UtcDateTime, tokens[1].SessionStartedAt);
        Assert.Equal(T0.AddHours(2).UtcDateTime, tokens[1].LastUsedAt);
    }

    // ---- leaver run --------------------------------------------------------

    private async Task AssertDeactivatedAsync(User user, string expectedReasonFragment)
    {
        await using var assertDb = _fixture.CreateDbContext();
        var persisted = await assertDb.Users.AsNoTracking().SingleAsync(u => u.Id == user.Id);
        Assert.False(persisted.IsActive);
        Assert.Equal(T0.UtcDateTime, persisted.DirectorySignInRefusedAt);
        Assert.Equal(user.DirectorySubjectId, persisted.DirectorySubjectId);
        Assert.All(await assertDb.RefreshTokens.AsNoTracking().Where(r => r.UserId == user.Id).ToListAsync(),
            r => Assert.Equal(T0.UtcDateTime, r.RevokedAt));

        var evt = await assertDb.SecurityAuditEvents.AsNoTracking()
            .SingleAsync(e => e.TargetUserId == user.Id && e.EventCode == SecurityEventCodes.UserDeactivatedByDirectory);
        Assert.True(evt.ActorIsSystem);
        Assert.Null(evt.ActorUserId);
        Assert.Contains(expectedReasonFragment, evt.Reason, StringComparison.Ordinal);
        Assert.Equal(2, System.Text.Json.JsonDocument.Parse(evt.Metadata!).RootElement.GetProperty("revokedSessionCount").GetInt32());

        var audit = await assertDb.AuditLogs.AsNoTracking()
            .SingleAsync(a => a.EntityName == nameof(User) && a.EntityId == user.Id.ToString() && a.Action == "Update");
        Assert.Equal(ActorType.System, audit.ActorType);
        Assert.Equal("DirectoryLeaverWorker", audit.SystemProcessName);
        Assert.Null(audit.UserId);
        Assert.Equal("Account disabled or removed in the company directory", audit.Reason);
        Assert.Contains("\"IsActive\":true", audit.PreviousValue, StringComparison.Ordinal);
        Assert.Contains("\"IsActive\":false", audit.NewValue, StringComparison.Ordinal);
        Assert.Contains("DirectorySignInRefusedAt", audit.NewValue, StringComparison.Ordinal);
    }

    [PostgresFact]
    public async Task Worker_DisabledAccount_DeactivatesUser_RevokesSessions_WritesSystemAudit()
    {
        var subject = $"sub-{Guid.NewGuid():N}";
        var leaver = await SeedUserAsync(subject: subject);
        var staying = await SeedUserAsync(subject: $"sub-{Guid.NewGuid():N}");
        await SeedActiveSessionAsync(leaver.Id);
        await SeedActiveSessionAsync(leaver.Id);
        await SeedActiveSessionAsync(staying.Id);
        var directory = NewDirectory()
            .WithAccount("gone", "x", subject, disabled: true)
            .WithAccount("here", "x", staying.DirectorySubjectId!);

        var result = await RunLeaverAsync(SsoMode.ActiveDirectory, directory);

        Assert.False(result.DirectoryUnavailable);
        Assert.True(result.Checked >= 2);
        Assert.Equal(1, result.Deactivated);
        await AssertDeactivatedAsync(leaver, "disabled");

        await using var assertDb = _fixture.CreateDbContext();
        var other = await assertDb.Users.AsNoTracking().SingleAsync(u => u.Id == staying.Id);
        Assert.True(other.IsActive);
        Assert.Null(other.DirectorySignInRefusedAt);
        Assert.All(await assertDb.RefreshTokens.AsNoTracking().Where(r => r.UserId == staying.Id).ToListAsync(),
            r => Assert.Null(r.RevokedAt));
    }

    [PostgresFact]
    public async Task Worker_MissingAccount_Deactivates()
    {
        var subject = $"sub-{Guid.NewGuid():N}";
        var leaver = await SeedUserAsync(subject: subject);
        await SeedActiveSessionAsync(leaver.Id);
        await SeedActiveSessionAsync(leaver.Id);
        var directory = NewDirectory().WithMissingSubject(subject);

        var result = await RunLeaverAsync(SsoMode.ActiveDirectory, directory);

        Assert.Equal(1, result.Deactivated);
        await AssertDeactivatedAsync(leaver, "no longer exists");
    }

    [PostgresFact]
    public async Task Worker_IdTheDirectoryCouldNotHaveIssued_IsUntouched()
    {
        var user = await SeedUserAsync(subject: $"sub-{Guid.NewGuid():N}");

        // The fake omits the id from its answer, as the real directory does for an id it cannot have issued.
        var result = await RunLeaverAsync(SsoMode.ActiveDirectory, NewDirectory());

        Assert.Equal(0, result.Deactivated);
        await using var assertDb = _fixture.CreateDbContext();
        Assert.True((await assertDb.Users.AsNoTracking().SingleAsync(u => u.Id == user.Id)).IsActive);
    }

    [PostgresFact]
    public async Task Worker_DirectoryUnavailable_ChangesNothing_ReportsUnavailable()
    {
        // More than one batch (100 users per query), with a disabled account in an early batch and the
        // directory failing on the last one: a run that wrote batch by batch would already have closed it.
        var users = new List<User>();
        for (var i = 0; i < 105; i++)
            users.Add(await SeedUserAsync(subject: $"sub-{Guid.NewGuid():N}"));
        var disabled = users[0];
        var poison = users[^1];
        await SeedActiveSessionAsync(disabled.Id);

        var directory = NewDirectory().WithAccount("gone", "x", disabled.DirectorySubjectId!, disabled: true);
        directory.ThrowWhenAskedFor.Add(poison.DirectorySubjectId!);

        var emails = new RecordingEmailSender();
        var result = await RunLeaverAsync(SsoMode.ActiveDirectory, directory, emails);

        Assert.True(result.DirectoryUnavailable);
        Assert.Equal(0, result.Deactivated);
        Assert.True(directory.FindManyCalls.Count >= 2, "the failure must happen after an earlier batch was answered");
        var firstCallWithDisabled = directory.FindManyCalls.FindIndex(c => c.Contains(disabled.DirectorySubjectId!));
        var failingCall = directory.FindManyCalls.FindIndex(c => c.Contains(poison.DirectorySubjectId!));
        Assert.True(firstCallWithDisabled >= 0 && firstCallWithDisabled < failingCall);
        Assert.Empty(emails.Sent);

        var ids = users.Select(u => u.Id).ToList();
        await using var assertDb = _fixture.CreateDbContext();
        var persisted = await assertDb.Users.AsNoTracking().Where(u => ids.Contains(u.Id)).ToListAsync();
        Assert.All(persisted, u =>
        {
            Assert.True(u.IsActive);
            Assert.Null(u.DirectorySignInRefusedAt);
        });
        Assert.All(await assertDb.RefreshTokens.AsNoTracking().Where(r => r.UserId == disabled.Id).ToListAsync(),
            r => Assert.Null(r.RevokedAt));
        Assert.False(await assertDb.SecurityAuditEvents.AsNoTracking()
            .AnyAsync(e => e.TargetUserId == disabled.Id && e.EventCode == SecurityEventCodes.UserDeactivatedByDirectory));
        var idStrings = ids.Select(i => i.ToString()).ToList();
        Assert.False(await assertDb.AuditLogs.AsNoTracking()
            .AnyAsync(a => a.EntityName == nameof(User) && idStrings.Contains(a.EntityId)
                           && a.SystemProcessName == "DirectoryLeaverWorker"));
    }

    [PostgresFact]
    public async Task Worker_EnabledAgain_DoesNotReactivate()
    {
        var subject = $"sub-{Guid.NewGuid():N}";
        var leaver = await SeedUserAsync(subject: subject);

        var first = await RunLeaverAsync(SsoMode.ActiveDirectory, NewDirectory().WithAccount("p", "x", subject, disabled: true));
        Assert.Equal(1, first.Deactivated);

        // Enabled again in the directory: the inactive account is not even looked at, and stays closed.
        var directory = NewDirectory().WithAccount("p", "x", subject, disabled: false);
        var second = await RunLeaverAsync(SsoMode.ActiveDirectory, directory);

        Assert.Equal(0, second.Deactivated);
        Assert.DoesNotContain(directory.FindManyCalls, c => c.Contains(subject));
        await using var assertDb = _fixture.CreateDbContext();
        Assert.False((await assertDb.Users.AsNoTracking().SingleAsync(u => u.Id == leaver.Id)).IsActive);
    }

    // [PostgresFact] looping the cases, not a multi-row [PostgresTheory]: skipped without a database a
    // theory counts as one test, and CI's verify_test_results.py then sees different totals for its two runs.
    [PostgresFact]
    public async Task Worker_ModeOidcOrOff_DoesNothing()
    {
        var cases = new[] { SsoMode.Off, SsoMode.OpenIdConnect };

        await PostgresCases.RunAsync(cases, async mode =>
        {
            var subject = $"sub-{Guid.NewGuid():N}";
            var user = await SeedUserAsync(subject: subject);
            var directory = NewDirectory().WithAccount("gone", "x", subject, disabled: true);

            var result = await RunLeaverAsync(mode, directory);

            Assert.Equal(new LeaverRunResult(0, 0, false), result);
            Assert.Empty(directory.FindManyCalls);
            await using var assertDb = _fixture.CreateDbContext();
            Assert.True((await assertDb.Users.AsNoTracking().SingleAsync(u => u.Id == user.Id)).IsActive);
        });
    }

    [PostgresFact]
    public async Task Worker_UserWithoutSubjectId_IsSkipped()
    {
        var unlinked = await SeedUserAsync(subject: null);
        var local = await SeedUserAsync(method: SignInMethod.Local, subject: $"sub-{Guid.NewGuid():N}");
        var directory = NewDirectory();

        var result = await RunLeaverAsync(SsoMode.ActiveDirectory, directory);

        Assert.Equal(0, result.Deactivated);
        await using var assertDb = _fixture.CreateDbContext();
        Assert.True((await assertDb.Users.AsNoTracking().SingleAsync(u => u.Id == unlinked.Id)).IsActive);
        Assert.True((await assertDb.Users.AsNoTracking().SingleAsync(u => u.Id == local.Id)).IsActive);
        Assert.DoesNotContain(directory.FindManyCalls, c => c.Contains(local.DirectorySubjectId!));
    }

    [PostgresFact]
    public async Task Worker_EmailsAdministratorsOnceAfterCommit_OnlyWhenSomeoneDeactivated()
    {
        var admin = await SeedUserAsync(method: SignInMethod.Local, role: RoleType.SystemAdministrator);
        var subject = $"sub-{Guid.NewGuid():N}";
        var leaver = await SeedUserAsync(subject: subject);
        var directory = NewDirectory().WithAccount("gone", "x", subject, disabled: true);
        var emails = new RecordingEmailSender();
        var subject2 = $"sub-{Guid.NewGuid():N}";
        var leaver2 = await SeedUserAsync(subject: subject2);

        // Nobody has left: no email.
        var quiet = await RunLeaverAsync(SsoMode.ActiveDirectory, NewDirectory(), emails);
        Assert.Equal(0, quiet.Deactivated);
        Assert.Empty(emails.Sent);

        var result = await RunLeaverAsync(SsoMode.ActiveDirectory, directory, emails);
        Assert.Equal(1, result.Deactivated);

        await using var assertDb = _fixture.CreateDbContext();
        var expectedRecipients = await assertDb.Users.AsNoTracking()
            .Where(u => u.IsActive && u.Role!.Type == RoleType.SystemAdministrator && u.Email != null && u.Email != "")
            .Select(u => u.Email!).Distinct().ToListAsync();
        Assert.Contains(admin.Email!, expectedRecipients);
        // One message per administrator for the whole run, listing the account.
        Assert.Equal(expectedRecipients.OrderBy(e => e), emails.Sent.Select(s => s.To).OrderBy(e => e));
        Assert.All(emails.Sent, s => Assert.Contains(leaver.Username, s.Body, StringComparison.Ordinal));
        Assert.DoesNotContain(emails.Sent, s => s.To == leaver.Email);

        // After the commit: the account is already closed in the database when the email goes out.
        var checking = new CommitCheckingEmailSender(_fixture, leaver2.Id);
        await RunLeaverAsync(SsoMode.ActiveDirectory, NewDirectory().WithAccount("g2", "x", subject2, disabled: true), checking);
        Assert.NotEmpty(checking.LeaverAlreadyInactiveWhenSent);
        Assert.All(checking.LeaverAlreadyInactiveWhenSent, Assert.True);

        // Already closed: a further run neither deactivates nor emails.
        var before = emails.Sent.Count;
        var again = await RunLeaverAsync(SsoMode.ActiveDirectory, directory, emails);
        Assert.Equal(0, again.Deactivated);
        Assert.Equal(before, emails.Sent.Count);
    }
}
