using System.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using MicroLIMS.API.Controllers;
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
public class OidcSignInPostgresTests
{
    private const string CallbackUri = "https://api.example.test/api/auth/sso/callback";
    private const string FrontendOrigin = "https://app.example.test";
    private static readonly DateTimeOffset BaseUtc = new(2026, 11, 13, 12, 0, 0, TimeSpan.Zero);

    private readonly PostgresTestFixture _fixture;

    public OidcSignInPostgresTests(PostgresTestFixture fixture) => _fixture = fixture;

    private sealed class MutableTimeProvider : TimeProvider
    {
        private DateTimeOffset _now;
        public MutableTimeProvider(DateTimeOffset now) => _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan delta) => _now = _now.Add(delta);
    }

    private sealed class StubSystemSettingsReader : ISystemSettingsReader
    {
        private readonly Dictionary<string, string> _strings = new(StringComparer.Ordinal)
        {
            [SystemSettingKeys.MfaPolicy] = nameof(MfaPolicy.RequiredForAll),
            [SystemSettingKeys.MfaRequiredRoles] = "SystemAdministrator,Reviewer",
            [SystemSettingKeys.SsoMode] = nameof(SsoMode.OpenIdConnect),
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

        public Task<int> GetIntAsync(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult(_ints[key]);

        public Task<bool> GetBoolAsync(string key, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException($"Unexpected bool setting read: {key}");

        public Task<string> GetStringAsync(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult(_strings[key]);
    }

    private sealed class FakeOidcProviderClient : IOidcProviderClient
    {
        public string? LastNonce { get; private set; }
        public string? LastCodeChallenge { get; private set; }
        public string? LastState { get; private set; }
        public DirectoryIdentity IdentityToReturn { get; set; } = new("sub-default", "alice", true);
        public Exception? ExceptionToThrow { get; set; }

        public Task<Uri> BuildAuthorizeUriAsync(
            string state,
            string nonce,
            string codeChallenge,
            string redirectUri,
            CancellationToken ct = default)
        {
            LastState = state;
            LastNonce = nonce;
            LastCodeChallenge = codeChallenge;
            var uri = $"https://idp.example.test/authorize?state={Uri.EscapeDataString(state)}&nonce={Uri.EscapeDataString(nonce)}&code_challenge={Uri.EscapeDataString(codeChallenge)}&code_challenge_method=S256&redirect_uri={Uri.EscapeDataString(redirectUri)}";
            return Task.FromResult(new Uri(uri));
        }

        public Task<DirectoryIdentity> RedeemAsync(
            string code,
            string codeVerifier,
            string expectedNonce,
            string redirectUri,
            CancellationToken ct = default)
        {
            if (ExceptionToThrow is not null)
                throw ExceptionToThrow;

            return Task.FromResult(IdentityToReturn);
        }

        public Task TestAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private static (OidcSignInService Oidc, SsoModeService SsoMode, AuthenticationService Auth) CreateServices(
        MicroLimsDbContext db,
        TimeProvider clock,
        IOidcProviderClient oidcClient,
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

        var oidc = new OidcSignInService(db, oidcClient, ssoMode, auth, audit, clock);
        return (oidc, ssoMode, auth);
    }

    private async Task<User> SeedUserAsync(
        string prefix,
        string? directoryUsername = null,
        string? directorySubjectId = null,
        bool isActive = true,
        SignInMethod signInMethod = SignInMethod.Directory,
        DateTime? signingPasswordSetAt = null)
    {
        await using var db = _fixture.CreateDbContext();
        var role = await db.Roles.FirstAsync(r => r.Type == RoleType.Analyst);
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var user = new User
        {
            Username = $"{prefix}_{suffix}",
            FullName = $"{prefix} User",
            Email = $"{prefix}_{suffix}@example.test",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("StrongPass1!"),
            RoleId = role.Id,
            IsActive = isActive,
            SignInMethod = signInMethod,
            DirectoryUsername = directoryUsername,
            DirectorySubjectId = directorySubjectId,
            SigningPasswordSetAt = signingPasswordSetAt
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private static string ExtractFragmentParam(Uri redirectUri, string key)
    {
        var fragment = redirectUri.Fragment.TrimStart('#');
        var parsed = HttpUtility.ParseQueryString(fragment);
        return parsed[key] ?? string.Empty;
    }

    [PostgresFact]
    public async Task Start_CreatesPendingRequest_ReturnsAuthorizeUriWithPkceAndNonce()
    {
        var clock = new MutableTimeProvider(BaseUtc);
        var fakeClient = new FakeOidcProviderClient();

        Uri authorizeUri;
        await using (var db = _fixture.CreateDbContext())
        {
            var (oidc, _, _) = CreateServices(db, clock, fakeClient);
            authorizeUri = await oidc.StartAsync(CallbackUri);
        }

        var query = HttpUtility.ParseQueryString(authorizeUri.Query);
        Assert.Equal("S256", query["code_challenge_method"]);
        Assert.False(string.IsNullOrWhiteSpace(query["code_challenge"]));
        Assert.False(string.IsNullOrWhiteSpace(query["nonce"]));
        Assert.False(string.IsNullOrWhiteSpace(query["state"]));

        var stateHash = Sha256Hex(query["state"]!);
        await using var assertDb = _fixture.CreateDbContext();
        var pending = await assertDb.SsoPendingRequests.AsNoTracking()
            .FirstOrDefaultAsync(r => r.StateHash == stateHash);
        Assert.NotNull(pending);
        Assert.Equal(query["nonce"], pending.Nonce);
        Assert.Null(pending.ConsumedAt);
        Assert.Equal(BaseUtc.UtcDateTime.AddMinutes(10), pending.ExpiresAt);
    }

    [PostgresFact]
    public async Task Callback_ValidToken_FirstTime_BindsSubjectId_IssuesExchangeCode()
    {
        var clock = new MutableTimeProvider(BaseUtc);
        var dirUser = $"dir_{Guid.NewGuid():N}"[..16];
        var subjectId = $"sub-{Guid.NewGuid():N}";
        var user = await SeedUserAsync("oidc_first", directoryUsername: dirUser, directorySubjectId: null);
        var fakeClient = new FakeOidcProviderClient
        {
            IdentityToReturn = new DirectoryIdentity(subjectId, dirUser.ToUpperInvariant(), MfaSatisfied: true)
        };

        Uri startUri;
        await using (var startDb = _fixture.CreateDbContext())
        {
            var (oidc, _, _) = CreateServices(startDb, clock, fakeClient);
            startUri = await oidc.StartAsync(CallbackUri);
        }
        var state = HttpUtility.ParseQueryString(startUri.Query)["state"]!;

        Uri callbackRedirect;
        await using (var cbDb = _fixture.CreateDbContext())
        {
            var (oidc, _, _) = CreateServices(cbDb, clock, fakeClient);
            callbackRedirect = await oidc.HandleCallbackAsync(state, "idp-code", null, CallbackUri, FrontendOrigin, "127.0.0.1");
        }

        var exchangeCode = ExtractFragmentParam(callbackRedirect, "code");
        Assert.False(string.IsNullOrWhiteSpace(exchangeCode));

        await using var assertDb = _fixture.CreateDbContext();
        var updatedUser = await assertDb.Users.AsNoTracking().FirstAsync(u => u.Id == user.Id);
        Assert.Equal(subjectId, updatedUser.DirectorySubjectId);

        var codeHash = Sha256Hex(exchangeCode);
        var codeRow = await assertDb.SsoExchangeCodes.AsNoTracking().FirstOrDefaultAsync(c => c.CodeHash == codeHash);
        Assert.NotNull(codeRow);
        Assert.Equal(user.Id, codeRow.UserId);
        Assert.Null(codeRow.ConsumedAt);

        var linkedEvent = await assertDb.SecurityAuditEvents.AsNoTracking()
            .FirstOrDefaultAsync(e => e.TargetUserId == user.Id && e.EventCode == SecurityEventCodes.UserDirectoryLinked);
        Assert.NotNull(linkedEvent);
    }

    [PostgresFact]
    public async Task Callback_UnlinkedUser_Refuses_DoesNotCreateUser()
    {
        var clock = new MutableTimeProvider(BaseUtc);
        var unknownDirUser = $"nobody_{Guid.NewGuid():N}"[..16];
        var fakeClient = new FakeOidcProviderClient
        {
            IdentityToReturn = new DirectoryIdentity($"sub-{Guid.NewGuid():N}", unknownDirUser, MfaSatisfied: true)
        };

        int userCountBefore;
        Uri startUri;
        await using (var setupDb = _fixture.CreateDbContext())
        {
            userCountBefore = await setupDb.Users.AsNoTracking().CountAsync();
            var (oidc, _, _) = CreateServices(setupDb, clock, fakeClient);
            startUri = await oidc.StartAsync(CallbackUri);
        }
        var state = HttpUtility.ParseQueryString(startUri.Query)["state"]!;

        Uri redirect;
        await using (var cbDb = _fixture.CreateDbContext())
        {
            var (oidc, _, _) = CreateServices(cbDb, clock, fakeClient);
            redirect = await oidc.HandleCallbackAsync(state, "idp-code", null, CallbackUri, FrontendOrigin, "127.0.0.1");
        }

        Assert.Equal("signin_failed", ExtractFragmentParam(redirect, "error"));

        await using var assertDb = _fixture.CreateDbContext();
        var userCountAfter = await assertDb.Users.AsNoTracking().CountAsync();
        Assert.Equal(userCountBefore, userCountAfter);

        var evt = await assertDb.SecurityAuditEvents.AsNoTracking()
            .Where(e => e.EventCode == SecurityEventCodes.SsoLoginFailedNotLinked && e.TargetUsername == unknownDirUser)
            .FirstOrDefaultAsync();
        Assert.NotNull(evt);
    }

    [PostgresFact]
    public async Task Callback_SubjectMismatch_Refuses_WritesSecurityEvent()
    {
        var clock = new MutableTimeProvider(BaseUtc);
        var dirUser = $"dir_{Guid.NewGuid():N}"[..16];
        var user = await SeedUserAsync("oidc_mismatch", directoryUsername: dirUser, directorySubjectId: "sub-bound-original");
        var fakeClient = new FakeOidcProviderClient
        {
            IdentityToReturn = new DirectoryIdentity("sub-attacker-different", dirUser, MfaSatisfied: true)
        };

        Uri startUri;
        await using (var startDb = _fixture.CreateDbContext())
        {
            var (oidc, _, _) = CreateServices(startDb, clock, fakeClient);
            startUri = await oidc.StartAsync(CallbackUri);
        }
        var state = HttpUtility.ParseQueryString(startUri.Query)["state"]!;

        Uri redirect;
        await using (var cbDb = _fixture.CreateDbContext())
        {
            var (oidc, _, _) = CreateServices(cbDb, clock, fakeClient);
            redirect = await oidc.HandleCallbackAsync(state, "idp-code", null, CallbackUri, FrontendOrigin, "127.0.0.1");
        }

        Assert.Equal("signin_failed", ExtractFragmentParam(redirect, "error"));

        await using var assertDb = _fixture.CreateDbContext();
        var evt = await assertDb.SecurityAuditEvents.AsNoTracking()
            .FirstOrDefaultAsync(e => e.TargetUserId == user.Id && e.EventCode == SecurityEventCodes.SsoLoginFailedSubjectMismatch);
        Assert.NotNull(evt);

        // The administrator sees "sign-in refused" for the linked user, with a system-attributed audit row.
        var persisted = await assertDb.Users.AsNoTracking().SingleAsync(u => u.Id == user.Id);
        Assert.Equal(BaseUtc.UtcDateTime, persisted.DirectorySignInRefusedAt);
        Assert.Equal("sub-bound-original", persisted.DirectorySubjectId);
        var audit = await assertDb.AuditLogs.AsNoTracking()
            .Where(a => a.EntityName == nameof(User) && a.EntityId == user.Id.ToString() && a.Reason == "Directory subject ID does not match the bound user")
            .SingleAsync();
        Assert.Equal("OidcSignIn", audit.SystemProcessName);
        Assert.Null(audit.UserId);
        Assert.Equal("Update", audit.Action);
    }

    [PostgresFact]
    public async Task Callback_SameSubFromDifferentIssuer_Refused_SubjectMismatch()
    {
        var clock = new MutableTimeProvider(BaseUtc);
        var dirUser = $"dir_{Guid.NewGuid():N}"[..16];
        var user = await SeedUserAsync("oidc_issuer", directoryUsername: dirUser, directorySubjectId: "https://idp-a.example.test|sub-1");
        var fakeClient = new FakeOidcProviderClient
        {
            IdentityToReturn = new DirectoryIdentity("https://idp-b.example.test|sub-1", dirUser, MfaSatisfied: true)
        };

        var redirect = await RunCallbackAsync(clock, fakeClient);

        Assert.Equal("signin_failed", ExtractFragmentParam(redirect, "error"));
        await using var assertDb = _fixture.CreateDbContext();
        Assert.True(await assertDb.SecurityAuditEvents.AsNoTracking()
            .AnyAsync(e => e.TargetUserId == user.Id && e.EventCode == SecurityEventCodes.SsoLoginFailedSubjectMismatch));
        var persisted = await assertDb.Users.AsNoTracking().SingleAsync(u => u.Id == user.Id);
        Assert.Equal("https://idp-a.example.test|sub-1", persisted.DirectorySubjectId);
        Assert.False(await assertDb.SsoExchangeCodes.AsNoTracking().AnyAsync(c => c.UserId == user.Id));
    }

    [PostgresFact]
    public async Task Callback_AfterCompanyUsernameChange_PreviousCompanyAccountNoLongerSignsIn()
    {
        var clock = new MutableTimeProvider(BaseUtc);
        var oldName = $"old_{Guid.NewGuid():N}"[..16];
        var newName = $"new_{Guid.NewGuid():N}"[..16];
        var oldSubject = $"https://idp.example.test|sub-{Guid.NewGuid():N}";
        var user = await SeedUserAsync("oidc_rename", directoryUsername: oldName, directorySubjectId: oldSubject);
        int adminId;
        await using (var seedDb = _fixture.CreateDbContext())
        {
            var role = await seedDb.Roles.FirstAsync(r => r.Type == RoleType.SystemAdministrator);
            var admin = new User
            {
                Username = $"oidc_adm_{Guid.NewGuid():N}"[..20], FullName = "Admin", Email = "adm@example.test",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("StrongPass1!"), RoleId = role.Id, IsActive = true
            };
            seedDb.Users.Add(admin);
            await seedDb.SaveChangesAsync();
            adminId = admin.Id;
        }

        // The previous holder of the old company username still has a valid sign-in at the provider.
        var fakeClient = new FakeOidcProviderClient { IdentityToReturn = new DirectoryIdentity(oldSubject, oldName, MfaSatisfied: true) };
        await using (var renameDb = _fixture.CreateDbContext())
        {
            var audit = new SecurityAuditService(renameDb, new SystemSecurityRequestContext(), clock);
            var (_, _, auth) = CreateServices(renameDb, clock, fakeClient);
            var users = new UserService(renameDb, auth, audit, clock, signatureService: new ElectronicSignatureService(renameDb, clock));
            await users.UpdateSsoLinkAsync(user.Id, SignInMethod.Directory, newName, "Company renamed the account", adminId);
        }

        var redirect = await RunCallbackAsync(clock, fakeClient);
        Assert.Equal("signin_failed", ExtractFragmentParam(redirect, "error"));

        await using var assertDb = _fixture.CreateDbContext();
        var persisted = await assertDb.Users.AsNoTracking().SingleAsync(u => u.Id == user.Id);
        Assert.Equal(newName, persisted.DirectoryUsername);
        Assert.Null(persisted.DirectorySubjectId);
        Assert.False(await assertDb.SsoExchangeCodes.AsNoTracking().AnyAsync(c => c.UserId == user.Id));
        Assert.True(await assertDb.SecurityAuditEvents.AsNoTracking()
            .AnyAsync(e => e.EventCode == SecurityEventCodes.SsoLoginFailedNotLinked && e.TargetUsername == oldName));
    }

    [PostgresFact]
    public async Task Callback_ExpiredPendingRequest_Refuses()
    {
        var clock = new MutableTimeProvider(BaseUtc);
        var fakeClient = new FakeOidcProviderClient();

        Uri startUri;
        await using (var startDb = _fixture.CreateDbContext())
        {
            var (oidc, _, _) = CreateServices(startDb, clock, fakeClient);
            startUri = await oidc.StartAsync(CallbackUri);
        }
        var state = HttpUtility.ParseQueryString(startUri.Query)["state"]!;

        clock.Advance(TimeSpan.FromMinutes(11));

        Uri redirect;
        await using (var cbDb = _fixture.CreateDbContext())
        {
            var (oidc, _, _) = CreateServices(cbDb, clock, fakeClient);
            redirect = await oidc.HandleCallbackAsync(state, "idp-code", null, CallbackUri, FrontendOrigin, "127.0.0.1");
        }

        Assert.Equal("signin_failed", ExtractFragmentParam(redirect, "error"));
    }

    [PostgresFact]
    public async Task Callback_ReusedPendingState_Refuses()
    {
        var clock = new MutableTimeProvider(BaseUtc);
        var dirUser = $"dir_{Guid.NewGuid():N}"[..16];
        var subjectId = $"sub-{Guid.NewGuid():N}";
        await SeedUserAsync("oidc_reuse", directoryUsername: dirUser, directorySubjectId: subjectId);
        var fakeClient = new FakeOidcProviderClient
        {
            IdentityToReturn = new DirectoryIdentity(subjectId, dirUser, MfaSatisfied: true)
        };

        Uri startUri;
        await using (var startDb = _fixture.CreateDbContext())
        {
            var (oidc, _, _) = CreateServices(startDb, clock, fakeClient);
            startUri = await oidc.StartAsync(CallbackUri);
        }
        var state = HttpUtility.ParseQueryString(startUri.Query)["state"]!;

        await using (var cbDb1 = _fixture.CreateDbContext())
        {
            var (oidc, _, _) = CreateServices(cbDb1, clock, fakeClient);
            var firstRedirect = await oidc.HandleCallbackAsync(state, "idp-code", null, CallbackUri, FrontendOrigin, "127.0.0.1");
            Assert.False(string.IsNullOrWhiteSpace(ExtractFragmentParam(firstRedirect, "code")));
        }

        await using (var cbDb2 = _fixture.CreateDbContext())
        {
            var (oidc, _, _) = CreateServices(cbDb2, clock, fakeClient);
            var secondRedirect = await oidc.HandleCallbackAsync(state, "idp-code", null, CallbackUri, FrontendOrigin, "127.0.0.1");
            Assert.Equal("signin_failed", ExtractFragmentParam(secondRedirect, "error"));
        }
    }

    [PostgresFact]
    public async Task Exchange_ValidCode_ReturnsTokens_SkipsB2Mfa_SetsSigningPasswordRequiredWhenUnset()
    {
        var clock = new MutableTimeProvider(BaseUtc);
        var dirUser = $"dir_{Guid.NewGuid():N}"[..16];
        var subjectId = $"sub-{Guid.NewGuid():N}";
        var user = await SeedUserAsync("oidc_exch", directoryUsername: dirUser, directorySubjectId: subjectId, signingPasswordSetAt: null);
        var fakeClient = new FakeOidcProviderClient
        {
            IdentityToReturn = new DirectoryIdentity(subjectId, dirUser, MfaSatisfied: true)
        };

        Uri startUri;
        await using (var startDb = _fixture.CreateDbContext())
        {
            var (oidc, _, _) = CreateServices(startDb, clock, fakeClient);
            startUri = await oidc.StartAsync(CallbackUri);
        }
        var state = HttpUtility.ParseQueryString(startUri.Query)["state"]!;

        Uri callbackRedirect;
        await using (var cbDb = _fixture.CreateDbContext())
        {
            var (oidc, _, _) = CreateServices(cbDb, clock, fakeClient);
            callbackRedirect = await oidc.HandleCallbackAsync(state, "idp-code", null, CallbackUri, FrontendOrigin, "127.0.0.1");
        }
        var rawCode = ExtractFragmentParam(callbackRedirect, "code");

        LoginOutcome outcome;
        await using (var exDb = _fixture.CreateDbContext())
        {
            var (oidc, _, _) = CreateServices(exDb, clock, fakeClient);
            outcome = await oidc.ExchangeAsync(rawCode, "127.0.0.1", "TestAgent");
        }

        Assert.True(outcome.Success);
        Assert.False(string.IsNullOrWhiteSpace(outcome.Token));
        Assert.False(string.IsNullOrWhiteSpace(outcome.RefreshToken));
        Assert.False(outcome.MfaRequired);
        Assert.False(outcome.MfaEnrolmentRequired);
        Assert.True(outcome.SigningPasswordRequired);

        await using var assertDb = _fixture.CreateDbContext();
        var history = await assertDb.LoginHistories.AsNoTracking()
            .Where(h => h.UserId == user.Id && h.Success)
            .OrderByDescending(h => h.Id)
            .FirstOrDefaultAsync();
        Assert.NotNull(history);
        Assert.Equal("Oidc", history.Method);

        var refresh = await assertDb.RefreshTokens.AsNoTracking()
            .Where(t => t.UserId == user.Id)
            .OrderByDescending(t => t.Id)
            .FirstOrDefaultAsync();
        Assert.NotNull(refresh);
        Assert.True(refresh.MfaVerified);

        var ssoEvent = await assertDb.SecurityAuditEvents.AsNoTracking()
            .FirstOrDefaultAsync(e => e.TargetUserId == user.Id && e.EventCode == SecurityEventCodes.SsoLoginSuccess);
        Assert.NotNull(ssoEvent);
    }

    [PostgresFact]
    public async Task Exchange_SameCodeTwice_SecondFails()
    {
        var clock = new MutableTimeProvider(BaseUtc);
        var dirUser = $"dir_{Guid.NewGuid():N}"[..16];
        var subjectId = $"sub-{Guid.NewGuid():N}";
        await SeedUserAsync("oidc_twice", directoryUsername: dirUser, directorySubjectId: subjectId);
        var fakeClient = new FakeOidcProviderClient
        {
            IdentityToReturn = new DirectoryIdentity(subjectId, dirUser, MfaSatisfied: true)
        };

        Uri startUri;
        await using (var startDb = _fixture.CreateDbContext())
        {
            var (oidc, _, _) = CreateServices(startDb, clock, fakeClient);
            startUri = await oidc.StartAsync(CallbackUri);
        }
        var state = HttpUtility.ParseQueryString(startUri.Query)["state"]!;

        Uri callbackRedirect;
        await using (var cbDb = _fixture.CreateDbContext())
        {
            var (oidc, _, _) = CreateServices(cbDb, clock, fakeClient);
            callbackRedirect = await oidc.HandleCallbackAsync(state, "idp-code", null, CallbackUri, FrontendOrigin, "127.0.0.1");
        }
        var rawCode = ExtractFragmentParam(callbackRedirect, "code");

        await using (var exDb1 = _fixture.CreateDbContext())
        {
            var (oidc, _, _) = CreateServices(exDb1, clock, fakeClient);
            var first = await oidc.ExchangeAsync(rawCode, "127.0.0.1", "TestAgent");
            Assert.True(first.Success);
        }

        await using (var exDb2 = _fixture.CreateDbContext())
        {
            var (oidc, _, _) = CreateServices(exDb2, clock, fakeClient);
            var second = await oidc.ExchangeAsync(rawCode, "127.0.0.1", "TestAgent");
            Assert.False(second.Success);
        }
    }

    [PostgresFact]
    public async Task Exchange_ExpiredCode_Fails()
    {
        var clock = new MutableTimeProvider(BaseUtc);
        var dirUser = $"dir_{Guid.NewGuid():N}"[..16];
        var subjectId = $"sub-{Guid.NewGuid():N}";
        await SeedUserAsync("oidc_exp", directoryUsername: dirUser, directorySubjectId: subjectId);
        var fakeClient = new FakeOidcProviderClient
        {
            IdentityToReturn = new DirectoryIdentity(subjectId, dirUser, MfaSatisfied: true)
        };

        Uri startUri;
        await using (var startDb = _fixture.CreateDbContext())
        {
            var (oidc, _, _) = CreateServices(startDb, clock, fakeClient);
            startUri = await oidc.StartAsync(CallbackUri);
        }
        var state = HttpUtility.ParseQueryString(startUri.Query)["state"]!;

        Uri callbackRedirect;
        await using (var cbDb = _fixture.CreateDbContext())
        {
            var (oidc, _, _) = CreateServices(cbDb, clock, fakeClient);
            callbackRedirect = await oidc.HandleCallbackAsync(state, "idp-code", null, CallbackUri, FrontendOrigin, "127.0.0.1");
        }
        var rawCode = ExtractFragmentParam(callbackRedirect, "code");

        clock.Advance(TimeSpan.FromSeconds(61));

        await using var exDb = _fixture.CreateDbContext();
        var (oidcEx, _, _) = CreateServices(exDb, clock, fakeClient);
        var outcome = await oidcEx.ExchangeAsync(rawCode, "127.0.0.1", "TestAgent");
        Assert.False(outcome.Success);
    }

    [PostgresFact]
    public async Task Exchange_ConcurrentDoubleRedeem_OnlyOneSucceeds()
    {
        var clock = new MutableTimeProvider(BaseUtc);
        var dirUser = $"dir_{Guid.NewGuid():N}"[..16];
        var subjectId = $"sub-{Guid.NewGuid():N}";
        await SeedUserAsync("oidc_conc", directoryUsername: dirUser, directorySubjectId: subjectId);
        var fakeClient = new FakeOidcProviderClient
        {
            IdentityToReturn = new DirectoryIdentity(subjectId, dirUser, MfaSatisfied: true)
        };

        Uri startUri;
        await using (var startDb = _fixture.CreateDbContext())
        {
            var (oidc, _, _) = CreateServices(startDb, clock, fakeClient);
            startUri = await oidc.StartAsync(CallbackUri);
        }
        var state = HttpUtility.ParseQueryString(startUri.Query)["state"]!;

        Uri callbackRedirect;
        await using (var cbDb = _fixture.CreateDbContext())
        {
            var (oidc, _, _) = CreateServices(cbDb, clock, fakeClient);
            callbackRedirect = await oidc.HandleCallbackAsync(state, "idp-code", null, CallbackUri, FrontendOrigin, "127.0.0.1");
        }
        var rawCode = ExtractFragmentParam(callbackRedirect, "code");

        using var barrier = new Barrier(2);
        var tasks = Enumerable.Range(0, 2).Select(_ => Task.Run(async () =>
        {
            await using var db = _fixture.CreateDbContext();
            var (oidc, _, _) = CreateServices(db, clock, fakeClient);
            barrier.SignalAndWait();
            return await oidc.ExchangeAsync(rawCode, "127.0.0.1", "TestAgent");
        })).ToArray();

        var results = await Task.WhenAll(tasks);
        Assert.Equal(1, results.Count(r => r.Success));
        Assert.Equal(1, results.Count(r => !r.Success));
    }

    [PostgresFact]
    public async Task SsoEndpoints_WhenModeOff_Return404()
    {
        var clock = new MutableTimeProvider(BaseUtc);
        var fakeClient = new FakeOidcProviderClient();
        var offSettings = new StubSystemSettingsReader().WithSsoMode(nameof(SsoMode.Off));

        await using var db = _fixture.CreateDbContext();
        var (oidc, ssoMode, _) = CreateServices(db, clock, fakeClient, offSettings);
        var config = new ConfigurationBuilder().Build();
        var controller = new SsoController(oidc, ssoMode, config, new SsoOptions())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        Assert.IsType<NotFoundResult>(await controller.Start(CancellationToken.None));
        Assert.IsType<NotFoundResult>(await controller.Callback("s", "c", null, CancellationToken.None));
        Assert.IsType<NotFoundResult>(await controller.Exchange(new SsoExchangeRequest("c"), CancellationToken.None));
    }

    [PostgresFact]
    public async Task OidcMode_MissingClientSecret_FailsNamingTheKey()
    {
        var clock = new MutableTimeProvider(BaseUtc);
        var fakeClient = new FakeOidcProviderClient();
        await using var db = _fixture.CreateDbContext();
        var (_, ssoMode, _) = CreateServices(db, clock, fakeClient);

        var options = new SsoOptions
        {
            Oidc = new OidcOptions
            {
                Authority = "https://idp.example.test",
                ClientId = "microlims-web",
                ClientSecret = null
            }
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => SsoStartupValidator.EnsureValidAsync(ssoMode, options));
        Assert.Contains("Sso:Oidc:ClientSecret", ex.Message);
    }

    private async Task<Uri> RunCallbackAsync(TimeProvider clock, FakeOidcProviderClient client, string? error = null)
    {
        Uri startUri;
        await using (var startDb = _fixture.CreateDbContext())
        {
            var (oidc, _, _) = CreateServices(startDb, clock, client);
            startUri = await oidc.StartAsync(CallbackUri);
        }
        var state = HttpUtility.ParseQueryString(startUri.Query)["state"]!;

        await using var cbDb = _fixture.CreateDbContext();
        var (cbOidc, _, _) = CreateServices(cbDb, clock, client);
        return await cbOidc.HandleCallbackAsync(state, error is null ? "idp-code" : null, error, CallbackUri, FrontendOrigin, "127.0.0.1");
    }

    [PostgresFact]
    public async Task FirstSignIn_StoresSubjectId_AuditedAsUserDirectoryLinked()
    {
        var clock = new MutableTimeProvider(BaseUtc);
        var dirUser = $"dir_{Guid.NewGuid():N}"[..16];
        var subjectId = $"https://idp.example.test|{Guid.NewGuid():N}";
        var user = await SeedUserAsync("oidc_audit", directoryUsername: dirUser);
        var client = new FakeOidcProviderClient { IdentityToReturn = new DirectoryIdentity(subjectId, dirUser, true) };

        await RunCallbackAsync(clock, client);

        await using var assertDb = _fixture.CreateDbContext();
        var audit = await assertDb.AuditLogs.AsNoTracking()
            .Where(a => a.EntityName == nameof(User) && a.EntityId == user.Id.ToString() && a.Action == "Update")
            .OrderByDescending(a => a.Id)
            .FirstOrDefaultAsync();
        Assert.NotNull(audit);
        Assert.Equal(user.Id, audit.UserId);
        Assert.Equal("User directory linked", audit.Reason);
        Assert.Contains(subjectId, audit.NewValue);
        Assert.DoesNotContain(subjectId, audit.PreviousValue ?? string.Empty);
    }

    [PostgresFact]
    public async Task Callback_ProviderError_RedirectsWithGenericError_IssuesNoCode()
    {
        var clock = new MutableTimeProvider(BaseUtc);
        var client = new FakeOidcProviderClient();
        int codesBefore;
        await using (var db = _fixture.CreateDbContext())
            codesBefore = await db.SsoExchangeCodes.AsNoTracking().CountAsync();

        var redirect = await RunCallbackAsync(clock, client, error: "access_denied");

        Assert.Equal($"{FrontendOrigin}/sso-complete#error=signin_failed", redirect.ToString());
        await using var assertDb = _fixture.CreateDbContext();
        Assert.Equal(codesBefore, await assertDb.SsoExchangeCodes.AsNoTracking().CountAsync());
    }

    [PostgresFact]
    public async Task Callback_LocalAccountWithDirectoryUsername_Refused_NoSubjectBound()
    {
        var clock = new MutableTimeProvider(BaseUtc);
        var dirUser = $"dir_{Guid.NewGuid():N}"[..16];
        var user = await SeedUserAsync("oidc_local", directoryUsername: dirUser, signInMethod: SignInMethod.Local);
        var client = new FakeOidcProviderClient { IdentityToReturn = new DirectoryIdentity($"sub-{Guid.NewGuid():N}", dirUser, true) };

        var redirect = await RunCallbackAsync(clock, client);

        Assert.Equal("signin_failed", ExtractFragmentParam(redirect, "error"));
        await using var assertDb = _fixture.CreateDbContext();
        var reloaded = await assertDb.Users.AsNoTracking().SingleAsync(u => u.Id == user.Id);
        Assert.Null(reloaded.DirectorySubjectId);
        Assert.True(await assertDb.SecurityAuditEvents.AsNoTracking()
            .AnyAsync(e => e.TargetUserId == user.Id && e.EventCode == SecurityEventCodes.SsoLoginFailedNotLinked));
        Assert.False(await assertDb.SsoExchangeCodes.AsNoTracking().AnyAsync(c => c.UserId == user.Id));
    }

    [PostgresFact]
    public async Task Exchange_After61Seconds_Fails_NoSessionIssued()
    {
        var clock = new MutableTimeProvider(BaseUtc);
        var dirUser = $"dir_{Guid.NewGuid():N}"[..16];
        var subjectId = $"sub-{Guid.NewGuid():N}";
        var user = await SeedUserAsync("oidc_late", directoryUsername: dirUser, directorySubjectId: subjectId);
        var client = new FakeOidcProviderClient { IdentityToReturn = new DirectoryIdentity(subjectId, dirUser, true) };

        var redirect = await RunCallbackAsync(clock, client);
        var code = ExtractFragmentParam(redirect, "code");
        clock.Advance(TimeSpan.FromSeconds(61));

        LoginOutcome outcome;
        await using (var exDb = _fixture.CreateDbContext())
        {
            var (oidc, _, _) = CreateServices(exDb, clock, client);
            outcome = await oidc.ExchangeAsync(code, "127.0.0.1", "TestAgent");
        }

        Assert.False(outcome.Success);
        Assert.Null(outcome.Token);
        await using var assertDb = _fixture.CreateDbContext();
        Assert.False(await assertDb.RefreshTokens.AsNoTracking().AnyAsync(t => t.UserId == user.Id));
    }

    // Same SHA-256 upper-hex form the service stores for state and exchange codes.
    private static string Sha256Hex(string value) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value)));
}
