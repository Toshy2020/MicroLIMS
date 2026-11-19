using System.Data.Common;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MicroLIMS.API.Controllers;
using MicroLIMS.API.Extensions;
using MicroLIMS.API.Filters;
using MicroLIMS.API.Json;
using MicroLIMS.API.Middleware;
using MicroLIMS.Application.Abstractions.Sso;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Security;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.Sso;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Infrastructure.Email;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

[Collection("PostgresDatabaseCollection")]
public class SsoAdminPostgresTests
{
    private const string Password = "SsoAdminPass1!";
    private const string Reason = "Link company accounts for go-live";
    // Later than the fixed clocks other SSO tests use, so this class's connection
    // tests are the most recent ones the "last passed" lookup reads.
    private static readonly DateTimeOffset BaseUtc = new(2030, 1, 15, 9, 0, 0, TimeSpan.Zero);
    private const string JwtSigningKey =
        "MDEyMzQ1Njc4OUFCQ0RFRjAxMjM0NTY3ODlBQkNERUYwMTIzNDU2Nzg5QUJDREVGMDEyMzQ1Njc4OUFCQ0RFRg==";
    private static readonly string FixedKeyBase64 =
        Convert.ToBase64String(Enumerable.Range(1, 32).Select(i => (byte)i).ToArray());

    private static readonly string[] ExpectedConfigurationKeys =
    {
        "oidc.authority", "oidc.clientId", "oidc.usernameClaim", "oidc.requireMfaClaim", "oidc.redirectUri", "oidc.clientSecretConfigured",
        "ldap.host", "ldap.port", "ldap.transport", "ldap.baseDn", "ldap.bindDn", "ldap.usernameAttribute", "ldap.subjectAttribute",
        "ldap.timeoutSeconds", "ldap.serviceAccountPasswordConfigured"
    };

    private readonly PostgresTestFixture _fixture;

    public SsoAdminPostgresTests(PostgresTestFixture fixture) => _fixture = fixture;

    private sealed class FakeTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;
        public FakeTimeProvider(DateTimeOffset utcNow) => _utcNow = utcNow;
        public override DateTimeOffset GetUtcNow() => _utcNow;
    }

    private sealed class FakeOidcProviderClient : IOidcProviderClient
    {
        public Exception? ToThrow { get; init; }
        public int TestCalls { get; private set; }

        public Task<Uri> BuildAuthorizeUriAsync(string state, string nonce, string codeChallenge, string redirectUri, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<DirectoryIdentity> RedeemAsync(string code, string codeVerifier, string expectedNonce, string redirectUri, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task TestAsync(CancellationToken ct = default)
        {
            TestCalls++;
            return ToThrow is null ? Task.CompletedTask : Task.FromException(ToThrow);
        }
    }

    // Lets the first batch (the user rows) reach the database, then fails the
    // next command once, so the import's transaction has something to roll back.
    private sealed class FailOnceAfterUserUpdateInterceptor : DbCommandInterceptor
    {
        private bool _usersUpdated;
        private bool _failed;

        private void Check(DbCommand command)
        {
            if (_usersUpdated && !_failed)
            {
                _failed = true;
                throw new InvalidOperationException("Simulated write failure after the user rows were updated.");
            }
            if (command.CommandText.Contains("UPDATE \"Users\"", StringComparison.Ordinal))
                _usersUpdated = true;
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Check(command);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            Check(command);
            return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
        }
    }

    private static SsoOptions NewOptions(string? clientSecret = null, string? bindPassword = null) => new()
    {
        Ldap = new LdapOptions
        {
            Host = "dc.corp.example.test",
            Port = 636,
            BaseDn = "DC=corp,DC=example,DC=test",
            BindDn = "CN=svc-microlims,OU=Service,DC=corp,DC=example,DC=test",
            BindPassword = bindPassword,
            UsernameAttribute = "sAMAccountName",
            SubjectAttribute = "objectGUID",
            TimeoutSeconds = 10
        },
        Oidc = new OidcOptions
        {
            // A unique authority gives each test its own configuration fingerprint,
            // so connection tests recorded by other tests never count here.
            Authority = $"https://login.example.test/{Guid.NewGuid():N}",
            ClientId = "microlims-client",
            ClientSecret = clientSecret,
            UsernameClaim = "preferred_username",
            RequireMfaClaim = true,
            RedirectUri = "https://lims.example.test/api/auth/sso/callback"
        }
    };

    private static (SsoAdminService Admin, UserService Users) CreateServices(
        MicroLimsDbContext db, SsoOptions? options = null, IOidcProviderClient? oidc = null, ILdapDirectory? ldap = null)
    {
        var clock = new FakeTimeProvider(BaseUtc);
        var effectiveOptions = options ?? NewOptions();
        var audit = new SecurityAuditService(db, new SystemSecurityRequestContext(), clock);
        var ssoMode = new SsoModeService(db, null, audit, new SsoConfigurationFingerprint(effectiveOptions));
        AccessTokenIssuer issuer = s => $"jwt-{s.UserId}";
        var auth = new AuthenticationService(
            db, issuer, new PermissionService(db),
            new EmailSender("", 587, "", "", "no-reply@microlims.local", false),
            NullLogger<AuthenticationService>.Instance, audit, clock, null, null, ssoMode);
        var users = new UserService(db, auth, audit, clock, signatureService: new ElectronicSignatureService(db, clock));
        var admin = new SsoAdminService(db, ssoMode, effectiveOptions, oidc ?? new FakeOidcProviderClient(), ldap ?? new FakeLdapDirectory(), users, clock);
        return (admin, users);
    }

    private async Task<User> SeedUserAsync(
        RoleType roleType = RoleType.Analyst,
        SignInMethod signInMethod = SignInMethod.Local,
        string? directoryUsername = null,
        string? subjectId = null,
        DateTime? refusedAt = null,
        DateTime? lastLoginAt = null,
        bool isEmergency = false,
        bool isActive = true,
        string? username = null)
    {
        await using var arrangeDb = _fixture.CreateDbContext();
        var roleId = await arrangeDb.Roles.AsNoTracking()
            .Where(r => r.Type == roleType).OrderBy(r => r.Id).Select(r => r.Id).FirstAsync();
        var tag = Guid.NewGuid().ToString("N");
        var user = new User
        {
            FullName = $"SSO Admin {tag[..8]}",
            Username = username ?? $"sso_adm_{tag}",
            Email = $"sso_adm_{tag}@example.test",
            PasswordHash = TestPasswords.Hash(Password),
            RoleId = roleId,
            IsActive = isActive,
            CreatedAt = BaseUtc.UtcDateTime,
            SignInMethod = signInMethod,
            DirectoryUsername = User.NormalizeDirectoryUsername(directoryUsername),
            DirectorySubjectId = subjectId,
            DirectorySignInRefusedAt = refusedAt,
            LastLoginAt = lastLoginAt,
            IsEmergencyAccount = isEmergency
        };
        arrangeDb.Users.Add(user);
        await arrangeDb.SaveChangesAsync();
        return user;
    }

    private async Task<uint> VersionOfAsync(int userId)
    {
        await using var readDb = _fixture.CreateDbContext();
        return (await readDb.Users.AsNoTracking().SingleAsync(u => u.Id == userId)).Version;
    }

    private static string Company(string label) => $"{label}.{Guid.NewGuid():N}@corp.test";

    // ---- GET api/admin/sso -------------------------------------------------

    [PostgresFact]
    public async Task GetSso_NeverReturnsClientSecretOrBindPassword()
    {
        var secret = $"Sup3r-Secret-{Guid.NewGuid():N}";
        var bindPassword = $"Bind-Pw-{Guid.NewGuid():N}";
        var options = NewOptions(secret, bindPassword);
        var admin = await SeedUserAsync(RoleType.SystemAdministrator);
        var unlinked = await SeedUserAsync();
        var linked = await SeedUserAsync(directoryUsername: Company("linked"));
        var emergency = await SeedUserAsync(RoleType.SystemAdministrator, isEmergency: true);
        var inactive = await SeedUserAsync(isActive: false);
        // Two names a language-aware collation and character-code order sort differently
        // ("sort_2" first by language rules, "sortA_1" first by character code), so the
        // order check below does not depend on which other tests left users behind.
        var suffix = Guid.NewGuid().ToString("N")[..8];
        await SeedUserAsync(username: $"sortA_1x{suffix}");
        await SeedUserAsync(username: $"sort_2x{suffix}");

        SsoAdminOverviewDto overview;
        await using (var actDb = _fixture.CreateDbContext())
        {
            var (service, _) = CreateServices(actDb, options);
            overview = await service.GetOverviewAsync(admin.Id, requestedMode: null);
        }

        var json = JsonSerializer.Serialize(overview, ApiJsonOptionsForTests());
        Assert.DoesNotContain(secret, json, StringComparison.Ordinal);
        Assert.DoesNotContain(bindPassword, json, StringComparison.Ordinal);
        // Only the "...Configured" flag may mention a password.
        Assert.DoesNotContain("password", json.Replace("serviceAccountPasswordConfigured", string.Empty), StringComparison.OrdinalIgnoreCase);

        // Only the allow-listed keys, and the secret appears only as "configured".
        Assert.Equal(ExpectedConfigurationKeys.OrderBy(k => k, StringComparer.Ordinal),
            overview.Configuration.Keys.OrderBy(k => k, StringComparer.Ordinal));
        Assert.Equal(true, overview.Configuration["oidc.clientSecretConfigured"]);
        Assert.Equal(true, overview.Configuration["ldap.serviceAccountPasswordConfigured"]);
        Assert.Equal("dc.corp.example.test", overview.Configuration["ldap.host"]);
        Assert.Equal("LDAPS", overview.Configuration["ldap.transport"]);
        Assert.Equal(options.Oidc.Authority, overview.Configuration["oidc.authority"]);
        Assert.Equal(true, overview.Configuration["oidc.requireMfaClaim"]);

        using var doc = JsonDocument.Parse(json);
        Assert.Equal("Off", doc.RootElement.GetProperty("mode").GetString());
        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("lastTestPassedAt").ValueKind);
        Assert.Contains(SsoModeService.BlockerConnectionTestRequired, overview.Blockers);
        Assert.Contains(SsoModeService.BlockerActingAdminNotLinked, overview.Blockers);

        var unlinkedIds = overview.UnlinkedUsers.Select(u => u.Id).ToHashSet();
        Assert.Contains(unlinked.Id, unlinkedIds);
        Assert.DoesNotContain(linked.Id, unlinkedIds);
        Assert.DoesNotContain(emergency.Id, unlinkedIds);
        Assert.DoesNotContain(inactive.Id, unlinkedIds);
        var row = overview.UnlinkedUsers.Single(u => u.Id == unlinked.Id);
        Assert.Equal(unlinked.Username, row.Username);
        Assert.False(string.IsNullOrEmpty(row.Role));
        Assert.Equal(overview.UnlinkedUsers.Select(u => u.Username).OrderBy(u => u, StringComparer.Ordinal),
            overview.UnlinkedUsers.Select(u => u.Username), StringComparer.Ordinal);
    }

    [Fact]
    public void ResolveTargetMode_DefaultsToCurrentThenOpenIdConnect()
    {
        Assert.Equal(SsoMode.OpenIdConnect, SsoAdminService.ResolveTargetMode(SsoMode.Off, null));
        Assert.Equal(SsoMode.ActiveDirectory, SsoAdminService.ResolveTargetMode(SsoMode.ActiveDirectory, null));
        Assert.Equal(SsoMode.ActiveDirectory, SsoAdminService.ResolveTargetMode(SsoMode.OpenIdConnect, SsoMode.ActiveDirectory));
    }

    // ---- POST api/admin/sso/test -------------------------------------------

    [PostgresFact]
    public async Task Test_Success_RecordsTimestamp()
    {
        var options = NewOptions("unused-secret");
        var admin = await SeedUserAsync(RoleType.SystemAdministrator);
        var oidc = new FakeOidcProviderClient();

        SsoTestResultDto result;
        await using (var actDb = _fixture.CreateDbContext())
        {
            var (service, _) = CreateServices(actDb, options, oidc);
            result = await service.TestConnectionAsync(admin.Id, requestedMode: null);
        }

        Assert.True(result.Passed);
        Assert.Equal(BaseUtc.UtcDateTime, result.TestedAt);
        Assert.Equal(1, oidc.TestCalls);

        await using (var assertDb = _fixture.CreateDbContext())
        {
            var evt = await assertDb.SecurityAuditEvents.AsNoTracking()
                .Where(e => e.ActorUserId == admin.Id && e.EventCode == SecurityEventCodes.SsoConnectionTestPassed)
                .SingleAsync();
            Assert.Equal(SecurityEventOutcome.Success, evt.Outcome);
            Assert.Contains("OpenIdConnect", evt.Metadata);
            Assert.False(await assertDb.SecurityAuditEvents.AsNoTracking()
                .AnyAsync(e => e.ActorUserId == admin.Id && e.EventCode == SecurityEventCodes.SsoConnectionTestFailed));

            var (service, _) = CreateServices(assertDb, options);
            var overview = await service.GetOverviewAsync(admin.Id, requestedMode: null);
            Assert.Equal(result.TestedAt, overview.LastTestPassedAt);
            Assert.DoesNotContain(SsoModeService.BlockerConnectionTestRequired, overview.Blockers);
        }
    }

    [PostgresFact]
    public async Task Test_Off_Refused_NothingRecorded()
    {
        var admin = await SeedUserAsync(RoleType.SystemAdministrator);
        var oidc = new FakeOidcProviderClient();
        var ldap = new FakeLdapDirectory();

        await using (var actDb = _fixture.CreateDbContext())
        {
            var (service, _) = CreateServices(actDb, oidc: oidc, ldap: ldap);
            await Assert.ThrowsAsync<BusinessRuleException>(() => service.TestConnectionAsync(admin.Id, SsoMode.Off));
        }

        Assert.Equal(0, oidc.TestCalls);
        Assert.Equal(0, ldap.TestCalls);
        await using var assertDb = _fixture.CreateDbContext();
        Assert.False(await assertDb.SecurityAuditEvents.AsNoTracking().AnyAsync(e => e.ActorUserId == admin.Id));
    }

    [PostgresFact]
    public async Task Test_ActiveDirectory_Success_UsesTheDirectoryOnly_RecordsTimestamp()
    {
        var options = NewOptions(bindPassword: "unused-bind-pw");
        var admin = await SeedUserAsync(RoleType.SystemAdministrator);
        var oidc = new FakeOidcProviderClient();
        var ldap = new FakeLdapDirectory();

        SsoTestResultDto result;
        await using (var actDb = _fixture.CreateDbContext())
        {
            var (service, _) = CreateServices(actDb, options, oidc, ldap);
            result = await service.TestConnectionAsync(admin.Id, SsoMode.ActiveDirectory);
        }

        Assert.True(result.Passed);
        Assert.Equal(1, ldap.TestCalls);
        Assert.Equal(0, oidc.TestCalls);

        await using var assertDb = _fixture.CreateDbContext();
        var evt = await assertDb.SecurityAuditEvents.AsNoTracking()
            .Where(e => e.ActorUserId == admin.Id && e.EventCode == SecurityEventCodes.SsoConnectionTestPassed)
            .SingleAsync();
        Assert.Contains("ActiveDirectory", evt.Metadata);
        Assert.DoesNotContain("unused-bind-pw", evt.Metadata ?? string.Empty, StringComparison.Ordinal);

        var (service2, _) = CreateServices(assertDb, options);
        var overview = await service2.GetOverviewAsync(admin.Id, SsoMode.ActiveDirectory);
        Assert.Equal(result.TestedAt, overview.LastTestPassedAt);
    }

    [PostgresFact]
    public async Task Test_ActiveDirectory_Failure_ScrubsBindPassword_RecordsFailureOnly()
    {
        var bindPassword = $"Leaky-Bind-{Guid.NewGuid():N}";
        var options = NewOptions(bindPassword: bindPassword);
        var admin = await SeedUserAsync(RoleType.SystemAdministrator);
        var ldap = new FakeLdapDirectory
        {
            TestThrows = new DirectoryUnavailableException($"The service account could not bind: bad credentials {bindPassword}")
        };

        BusinessRuleException error;
        await using (var actDb = _fixture.CreateDbContext())
        {
            var (service, _) = CreateServices(actDb, options, ldap: ldap);
            error = await Assert.ThrowsAsync<BusinessRuleException>(() => service.TestConnectionAsync(admin.Id, SsoMode.ActiveDirectory));
        }

        Assert.StartsWith("The connection test failed:", error.Message);
        Assert.Contains("service account could not bind", error.Message);
        Assert.DoesNotContain(bindPassword, error.Message, StringComparison.Ordinal);

        await using var assertDb = _fixture.CreateDbContext();
        var evt = await assertDb.SecurityAuditEvents.AsNoTracking()
            .Where(e => e.ActorUserId == admin.Id && e.EventCode == SecurityEventCodes.SsoConnectionTestFailed)
            .SingleAsync();
        Assert.Equal(SecurityEventOutcome.Failure, evt.Outcome);
        Assert.DoesNotContain(bindPassword, evt.Reason ?? string.Empty, StringComparison.Ordinal);
        Assert.DoesNotContain(bindPassword, evt.Metadata ?? string.Empty, StringComparison.Ordinal);
        Assert.False(await assertDb.SecurityAuditEvents.AsNoTracking()
            .AnyAsync(e => e.ActorUserId == admin.Id && e.EventCode == SecurityEventCodes.SsoConnectionTestPassed));
    }

    [PostgresFact]
    public async Task Test_Failure_ReturnsReason_NoTimestamp()
    {
        var secret = $"Leaky-Secret-{Guid.NewGuid():N}";
        var options = NewOptions(secret);
        var admin = await SeedUserAsync(RoleType.SystemAdministrator);
        var oidc = new FakeOidcProviderClient { ToThrow = new HttpRequestException($"Name not resolved while posting client_secret={secret}") };

        BusinessRuleException error;
        await using (var actDb = _fixture.CreateDbContext())
        {
            var (service, _) = CreateServices(actDb, options, oidc);
            error = await Assert.ThrowsAsync<BusinessRuleException>(() => service.TestConnectionAsync(admin.Id, requestedMode: null));
        }

        Assert.StartsWith("The connection test failed:", error.Message);
        Assert.Contains("could not be reached", error.Message);
        Assert.Contains("Name not resolved", error.Message);
        Assert.DoesNotContain(secret, error.Message, StringComparison.Ordinal);

        await using var assertDb = _fixture.CreateDbContext();
        var evt = await assertDb.SecurityAuditEvents.AsNoTracking()
            .Where(e => e.ActorUserId == admin.Id && e.EventCode == SecurityEventCodes.SsoConnectionTestFailed)
            .SingleAsync();
        Assert.Equal(SecurityEventOutcome.Failure, evt.Outcome);
        Assert.Contains("HttpRequestException", evt.Reason);
        Assert.DoesNotContain(secret, evt.Reason ?? string.Empty, StringComparison.Ordinal);
        Assert.False(await assertDb.SecurityAuditEvents.AsNoTracking()
            .AnyAsync(e => e.ActorUserId == admin.Id && e.EventCode == SecurityEventCodes.SsoConnectionTestPassed));

        var (service2, _) = CreateServices(assertDb, options);
        var overview = await service2.GetOverviewAsync(admin.Id, requestedMode: null);
        Assert.Null(overview.LastTestPassedAt);
        Assert.Contains(SsoModeService.BlockerConnectionTestRequired, overview.Blockers);
    }

    // ---- links preview / import --------------------------------------------

    [PostgresFact]
    public async Task ImportPreview_ReportsEachStatus_ChangesNothing()
    {
        var okUser = await SeedUserAsync();
        var dupA = await SeedUserAsync();
        var dupB = await SeedUserAsync();
        var alreadyLinkedUser = await SeedUserAsync(directoryUsername: Company("already"));
        var holderCompany = Company("holder");
        var holder = await SeedUserAsync(directoryUsername: holderCompany);
        var wantsHolders = await SeedUserAsync();
        var blankCompany = await SeedUserAsync();
        var emergency = await SeedUserAsync(RoleType.SystemAdministrator, isEmergency: true);
        var spaced = await SeedUserAsync();
        var disabled = await SeedUserAsync(isActive: false);
        var dupCompany = Company("dup");
        var seededIds = new[] { okUser.Id, dupA.Id, dupB.Id, alreadyLinkedUser.Id, holder.Id, wantsHolders.Id, blankCompany.Id, emergency.Id, spaced.Id, disabled.Id };

        var rows = new List<DirectoryLinkRowRequest>
        {
            new(okUser.Username, Company("ok")),                       // 1 Ok
            new($"nobody_{Guid.NewGuid():N}", Company("ghost")),       // 2 UnknownUser
            new(dupA.Username, dupCompany),                            // 3 Duplicate
            new(dupB.Username, dupCompany.ToUpperInvariant()),         // 4 Duplicate (case-insensitive)
            new(alreadyLinkedUser.Username, Company("new")),           // 5 AlreadyLinked (user has one)
            new(wantsHolders.Username, holderCompany.ToUpperInvariant()), // 6 AlreadyLinked (company taken)
            new(blankCompany.Username, "   "),                         // 7 Invalid (blank)
            new(emergency.Username, Company("em")),                    // 8 Invalid (emergency)
            new(spaced.Username, "a b@corp.test"),                     // 9 Invalid (space)
            new(disabled.Username, Company("off")),                    // 10 Invalid (disabled)
            new("   ", Company("nouser")),                             // 11 Invalid (blank username)
        };

        DirectoryLinkImportResultDto result;
        await using (var actDb = _fixture.CreateDbContext())
        {
            var (service, _) = CreateServices(actDb);
            result = await service.PreviewLinksAsync(rows);
        }

        Assert.Null(result.Applied);
        Assert.Equal(Enumerable.Range(1, rows.Count), result.Rows.Select(r => r.Row));
        Assert.Equal(
            new[] { "Ok", "UnknownUser", "Duplicate", "Duplicate", "AlreadyLinked", "AlreadyLinked", "Invalid", "Invalid", "Invalid", "Invalid", "Invalid" },
            result.Rows.Select(r => r.Status));
        Assert.Equal(okUser.Username, result.Rows[0].Username);
        Assert.All(result.Rows, r => Assert.False(string.IsNullOrWhiteSpace(r.Message)));
        Assert.Contains(holder.Username, result.Rows[5].Message);

        await using var assertDb = _fixture.CreateDbContext();
        var persisted = await assertDb.Users.AsNoTracking().Where(u => seededIds.Contains(u.Id))
            .Select(u => new { u.Id, u.DirectoryUsername, u.SignInMethod }).ToListAsync();
        Assert.Null(persisted.Single(u => u.Id == okUser.Id).DirectoryUsername);
        Assert.Null(persisted.Single(u => u.Id == dupA.Id).DirectoryUsername);
        Assert.Null(persisted.Single(u => u.Id == wantsHolders.Id).DirectoryUsername);
        Assert.All(persisted, u => Assert.Equal(SignInMethod.Local, u.SignInMethod));
        var idStrings = seededIds.Select(i => i.ToString()).ToList();
        Assert.False(await assertDb.AuditLogs.AsNoTracking()
            .AnyAsync(a => a.EntityName == nameof(User) && idStrings.Contains(a.EntityId) && a.Action != "Create"));
        Assert.False(await assertDb.SecurityAuditEvents.AsNoTracking()
            .AnyAsync(e => e.TargetUserId != null && seededIds.Contains(e.TargetUserId.Value)));
    }

    [PostgresFact]
    public async Task Import_DuplicateCompanyUsernameInFile_MarkedDuplicate()
    {
        var admin = await SeedUserAsync(RoleType.SystemAdministrator);
        var first = await SeedUserAsync();
        var second = await SeedUserAsync();
        var third = await SeedUserAsync();
        var shared = Company("shared");

        DirectoryLinkImportResultDto result;
        await using (var actDb = _fixture.CreateDbContext())
        {
            var (service, _) = CreateServices(actDb);
            result = await service.ImportLinksAsync(new List<DirectoryLinkRowRequest>
            {
                new(first.Username, shared),
                new(second.Username, $"  {shared.ToUpperInvariant()} "),
                new(third.Username, Company("third")),
                new(third.Username, Company("third-again")),
            }, Reason, admin.Id);
        }

        Assert.Equal(new[] { "Duplicate", "Duplicate", "Duplicate", "Duplicate" }, result.Rows.Select(r => r.Status));
        Assert.Equal(0, result.Applied);

        await using var assertDb = _fixture.CreateDbContext();
        var ids = new[] { first.Id, second.Id, third.Id };
        Assert.All(await assertDb.Users.AsNoTracking().Where(u => ids.Contains(u.Id)).ToListAsync(),
            u => Assert.Null(u.DirectoryUsername));
    }

    [PostgresFact]
    public async Task Import_AppliesOnlyOkRows_InOneTransaction_AuditsEachUser()
    {
        var admin = await SeedUserAsync(RoleType.SystemAdministrator);
        var a = await SeedUserAsync();
        var b = await SeedUserAsync();
        var existingCompany = Company("existing");
        var alreadyLinked = await SeedUserAsync(directoryUsername: existingCompany);
        var companyA = Company("a");
        var companyB = Company("b");

        DirectoryLinkImportResultDto result;
        await using (var actDb = _fixture.CreateDbContext())
        {
            var (service, _) = CreateServices(actDb);
            result = await service.ImportLinksAsync(new List<DirectoryLinkRowRequest>
            {
                new(a.Username, companyA.ToUpperInvariant()),
                new($"nobody_{Guid.NewGuid():N}", Company("ghost")),
                new(alreadyLinked.Username, Company("replacement")),
                new(b.Username, companyB),
            }, $"  {Reason}  ", admin.Id);
        }

        Assert.Equal(2, result.Applied);
        Assert.Equal(new[] { "Ok", "UnknownUser", "AlreadyLinked", "Ok" }, result.Rows.Select(r => r.Status));

        await using var assertDb = _fixture.CreateDbContext();
        var persistedA = await assertDb.Users.AsNoTracking().SingleAsync(u => u.Id == a.Id);
        var persistedB = await assertDb.Users.AsNoTracking().SingleAsync(u => u.Id == b.Id);
        var persistedLinked = await assertDb.Users.AsNoTracking().SingleAsync(u => u.Id == alreadyLinked.Id);
        Assert.Equal(companyA, persistedA.DirectoryUsername);
        Assert.Equal(companyB, persistedB.DirectoryUsername);
        Assert.Equal(SignInMethod.Local, persistedA.SignInMethod);
        Assert.Equal(SignInMethod.Local, persistedB.SignInMethod);
        Assert.Equal(existingCompany, persistedLinked.DirectoryUsername);

        foreach (var (user, company) in new[] { (a, companyA), (b, companyB) })
        {
            var audit = await assertDb.AuditLogs.AsNoTracking()
                .SingleAsync(x => x.EntityName == nameof(User) && x.EntityId == user.Id.ToString() && x.Action == "USER_SSO_LINK_UPDATED");
            Assert.Equal(admin.Id, audit.UserId);
            Assert.Equal(Reason, audit.Reason);
            Assert.Contains("\"DirectoryUsername\":null", audit.PreviousValue);
            Assert.Contains(company, audit.NewValue);
            Assert.Contains("\"SignInMethod\":\"Local\"", audit.NewValue);

            var captured = await assertDb.AuditLogs.AsNoTracking()
                .SingleAsync(x => x.EntityName == nameof(User) && x.EntityId == user.Id.ToString() && x.Action == "Update");
            Assert.Equal(admin.Id, captured.UserId);

            var evt = await assertDb.SecurityAuditEvents.AsNoTracking()
                .SingleAsync(e => e.TargetUserId == user.Id);
            Assert.Equal(SecurityEventCodes.UserDirectoryLinked, evt.EventCode);
            Assert.Equal(admin.Id, evt.ActorUserId);
            Assert.Equal(Reason, evt.Reason);
        }

        Assert.False(await assertDb.AuditLogs.AsNoTracking()
            .AnyAsync(x => x.EntityName == nameof(User) && x.EntityId == alreadyLinked.Id.ToString() && x.Action != "Create"));
    }

    [PostgresFact]
    public async Task Import_WriteFailsPartWay_NothingApplied()
    {
        var admin = await SeedUserAsync(RoleType.SystemAdministrator);
        var a = await SeedUserAsync();
        var b = await SeedUserAsync();

        await using (var actDb = _fixture.CreateDbContext(new FailOnceAfterUserUpdateInterceptor()))
        {
            var (service, _) = CreateServices(actDb);
            var ex = await Assert.ThrowsAsync<DbUpdateException>(() => service.ImportLinksAsync(new List<DirectoryLinkRowRequest>
            {
                new(a.Username, Company("a")),
                new(b.Username, Company("b")),
            }, Reason, admin.Id));
            Assert.Contains("Simulated write failure", ex.InnerException?.Message);
        }

        await using var assertDb = _fixture.CreateDbContext();
        var ids = new[] { a.Id, b.Id };
        Assert.All(await assertDb.Users.AsNoTracking().Where(u => ids.Contains(u.Id)).ToListAsync(),
            u => Assert.Null(u.DirectoryUsername));
        var idStrings = ids.Select(i => i.ToString()).ToList();
        Assert.False(await assertDb.AuditLogs.AsNoTracking()
            .AnyAsync(x => x.EntityName == nameof(User) && idStrings.Contains(x.EntityId) && x.Action != "Create"));
        Assert.False(await assertDb.SecurityAuditEvents.AsNoTracking()
            .AnyAsync(e => e.TargetUserId != null && ids.Contains(e.TargetUserId.Value)));
    }

    [PostgresFact]
    public async Task Import_WithoutReason_Throws()
    {
        var admin = await SeedUserAsync(RoleType.SystemAdministrator);
        var target = await SeedUserAsync();

        await using (var actDb = _fixture.CreateDbContext())
        {
            var (service, _) = CreateServices(actDb);
            var rows = new List<DirectoryLinkRowRequest> { new(target.Username, Company("t")) };
            var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => service.ImportLinksAsync(rows, null, admin.Id));
            Assert.Contains("reason", ex.Message, StringComparison.OrdinalIgnoreCase);
            await Assert.ThrowsAsync<BusinessRuleException>(() => service.ImportLinksAsync(rows, "   ", admin.Id));
        }

        await using var assertDb = _fixture.CreateDbContext();
        Assert.Null((await assertDb.Users.AsNoTracking().SingleAsync(u => u.Id == target.Id)).DirectoryUsername);
    }

    [PostgresFact]
    public async Task Import_Over2000Rows_Refused()
    {
        var admin = await SeedUserAsync(RoleType.SystemAdministrator);
        var target = await SeedUserAsync();
        var rows = Enumerable.Range(0, SsoAdminService.MaxImportRows)
            .Select(i => new DirectoryLinkRowRequest($"nobody_{i}_{Guid.NewGuid():N}", Company($"r{i}")))
            .Append(new DirectoryLinkRowRequest(target.Username, Company("t")))
            .ToList();

        await using (var actDb = _fixture.CreateDbContext())
        {
            var (service, _) = CreateServices(actDb);
            var import = await Assert.ThrowsAsync<BusinessRuleException>(() => service.ImportLinksAsync(rows, Reason, admin.Id));
            Assert.Contains("2000", import.Message);
            await Assert.ThrowsAsync<BusinessRuleException>(() => service.PreviewLinksAsync(rows));

            // Exactly the limit is accepted.
            var atLimit = await service.PreviewLinksAsync(rows.Take(SsoAdminService.MaxImportRows).ToList());
            Assert.Equal(SsoAdminService.MaxImportRows, atLimit.Rows.Count);
        }

        await using var assertDb = _fixture.CreateDbContext();
        Assert.Null((await assertDb.Users.AsNoTracking().SingleAsync(u => u.Id == target.Id)).DirectoryUsername);
    }

    // ---- directory re-link -------------------------------------------------

    // ---- Part A: changing a company username forgets the old company identity ----

    [PostgresFact]
    public async Task Update_ChangeCompanyUsername_ClearsStoredSubject_Audited()
    {
        var admin = await SeedUserAsync(RoleType.SystemAdministrator);
        var oldCompany = Company("old");
        var newCompany = Company("new");
        var oldSubject = $"sub-{Guid.NewGuid():N}";
        var target = await SeedUserAsync(RoleType.Analyst, SignInMethod.Directory, oldCompany, oldSubject,
            refusedAt: BaseUtc.UtcDateTime.AddDays(-1));
        uint version;
        await using (var readDb = _fixture.CreateDbContext())
            version = (await readDb.Users.AsNoTracking().SingleAsync(u => u.Id == target.Id)).Version;

        await using (var actDb = _fixture.CreateDbContext())
        {
            var (_, users) = CreateServices(actDb);
            actDb.ExpectedVersion = version;
            await users.UpdateProfileAsync(target.Id, target.FullName, target.Username, target.Email, null, admin.Id,
                "Company renamed the account", new UserSignInChange(SignInMethod.Directory, newCompany, false));
        }

        await using var assertDb = _fixture.CreateDbContext();
        var persisted = await assertDb.Users.AsNoTracking().SingleAsync(u => u.Id == target.Id);
        Assert.Equal(newCompany, persisted.DirectoryUsername);
        Assert.Null(persisted.DirectorySubjectId);
        Assert.Null(persisted.DirectorySignInRefusedAt);

        var audit = await assertDb.AuditLogs.AsNoTracking()
            .SingleAsync(a => a.EntityName == nameof(User) && a.EntityId == target.Id.ToString() && a.Action == "USER_SSO_LINK_UPDATED");
        Assert.Equal(admin.Id, audit.UserId);
        Assert.Contains(oldSubject, audit.PreviousValue);
        Assert.Contains(oldCompany, audit.PreviousValue);
        Assert.Contains("\"DirectorySubjectId\":null", audit.NewValue);
        Assert.Contains(newCompany, audit.NewValue);

        var evt = await assertDb.SecurityAuditEvents.AsNoTracking().SingleAsync(e => e.TargetUserId == target.Id);
        Assert.Equal(SecurityEventCodes.UserDirectoryRelinked, evt.EventCode);
        Assert.Equal(admin.Id, evt.ActorUserId);
    }

    [PostgresFact]
    public async Task Update_RemoveCompanyUsername_ClearsStoredSubject_Audited()
    {
        var admin = await SeedUserAsync(RoleType.SystemAdministrator);
        var oldSubject = $"sub-{Guid.NewGuid():N}";
        var target = await SeedUserAsync(RoleType.Analyst, SignInMethod.Local, Company("remove"), oldSubject,
            refusedAt: BaseUtc.UtcDateTime.AddDays(-1));

        await using (var actDb = _fixture.CreateDbContext())
        {
            var (_, users) = CreateServices(actDb);
            await users.UpdateSsoLinkAsync(target.Id, SignInMethod.Local, null, "Unlink the company account", admin.Id);
        }

        await using var assertDb = _fixture.CreateDbContext();
        var persisted = await assertDb.Users.AsNoTracking().SingleAsync(u => u.Id == target.Id);
        Assert.Null(persisted.DirectoryUsername);
        Assert.Null(persisted.DirectorySubjectId);
        Assert.Null(persisted.DirectorySignInRefusedAt);
        var audit = await assertDb.AuditLogs.AsNoTracking()
            .SingleAsync(a => a.EntityName == nameof(User) && a.EntityId == target.Id.ToString() && a.Action == "USER_SSO_LINK_UPDATED");
        Assert.Contains(oldSubject, audit.PreviousValue);
        Assert.Contains("\"DirectorySubjectId\":null", audit.NewValue);
    }

    [PostgresFact]
    public async Task Update_SameCompanyUsername_KeepsStoredSubject()
    {
        var admin = await SeedUserAsync(RoleType.SystemAdministrator);
        var company = Company("same");
        var subject = $"sub-{Guid.NewGuid():N}";
        var target = await SeedUserAsync(RoleType.Analyst, SignInMethod.Directory, company, subject);

        await using (var actDb = _fixture.CreateDbContext())
        {
            var (_, users) = CreateServices(actDb);
            actDb.ExpectedVersion = await VersionOfAsync(target.Id);
            await users.UpdateProfileAsync(target.Id, target.FullName, target.Username, target.Email, null, admin.Id,
                "Re-save a different name", new UserSignInChange(SignInMethod.Directory, company.ToUpperInvariant(), false));
        }

        await using var assertDb = _fixture.CreateDbContext();
        var persisted = await assertDb.Users.AsNoTracking().SingleAsync(u => u.Id == target.Id);
        Assert.Equal(subject, persisted.DirectorySubjectId);
    }

    [PostgresFact]
    public async Task Import_FirstLink_LeavesSubjectNull()
    {
        var admin = await SeedUserAsync(RoleType.SystemAdministrator);
        var target = await SeedUserAsync();
        var company = Company("first");

        await using (var actDb = _fixture.CreateDbContext())
        {
            var (service, _) = CreateServices(actDb);
            await service.ImportLinksAsync(new List<DirectoryLinkRowRequest> { new(target.Username, company) }, Reason, admin.Id);
        }

        await using var assertDb = _fixture.CreateDbContext();
        var persisted = await assertDb.Users.AsNoTracking().SingleAsync(u => u.Id == target.Id);
        Assert.Equal(company, persisted.DirectoryUsername);
        Assert.Null(persisted.DirectorySubjectId);
        var audit = await assertDb.AuditLogs.AsNoTracking()
            .SingleAsync(a => a.EntityName == nameof(User) && a.EntityId == target.Id.ToString() && a.Action == "USER_SSO_LINK_UPDATED");
        Assert.Contains("\"DirectorySubjectId\":null", audit.PreviousValue);
        Assert.Contains("\"DirectorySubjectId\":null", audit.NewValue);
    }

    [PostgresFact]
    public async Task Relink_RequiresReason_ClearsSubject_Audited()
    {
        var admin = await SeedUserAsync(RoleType.SystemAdministrator);
        var company = Company("relink");
        var subject = $"sub-{Guid.NewGuid():N}";
        var target = await SeedUserAsync(RoleType.Analyst,
            SignInMethod.Directory, company, subject, refusedAt: BaseUtc.UtcDateTime.AddDays(-1));
        var unlinked = await SeedUserAsync();

        await using (var actDb = _fixture.CreateDbContext())
        {
            var (_, users) = CreateServices(actDb);
            await Assert.ThrowsAsync<BusinessRuleException>(() => users.RelinkDirectoryAsync(target.Id, "  ", admin.Id));
            await Assert.ThrowsAsync<BusinessRuleException>(() => users.RelinkDirectoryAsync(unlinked.Id, "No link to reset", admin.Id));
        }

        await using (var midDb = _fixture.CreateDbContext())
        {
            var untouched = await midDb.Users.AsNoTracking().SingleAsync(u => u.Id == target.Id);
            Assert.Equal(subject, untouched.DirectorySubjectId);
        }

        await using (var actDb = _fixture.CreateDbContext())
        {
            var (_, users) = CreateServices(actDb);
            await users.RelinkDirectoryAsync(target.Id, "Company re-created the account", admin.Id);
        }

        await using var assertDb = _fixture.CreateDbContext();
        var persisted = await assertDb.Users.AsNoTracking().SingleAsync(u => u.Id == target.Id);
        Assert.Null(persisted.DirectorySubjectId);
        Assert.Null(persisted.DirectorySignInRefusedAt);
        Assert.Equal(company, persisted.DirectoryUsername);
        Assert.Equal(SignInMethod.Directory, persisted.SignInMethod);

        var audit = await assertDb.AuditLogs.AsNoTracking()
            .SingleAsync(a => a.EntityName == nameof(User) && a.EntityId == target.Id.ToString() && a.Action == "USER_DIRECTORY_RELINKED");
        Assert.Equal(admin.Id, audit.UserId);
        Assert.Equal("Company re-created the account", audit.Reason);
        Assert.Contains(subject, audit.PreviousValue);
        Assert.Contains("\"DirectorySubjectId\":null", audit.NewValue);

        var captured = await assertDb.AuditLogs.AsNoTracking()
            .SingleAsync(a => a.EntityName == nameof(User) && a.EntityId == target.Id.ToString() && a.Action == "Update");
        Assert.Equal(admin.Id, captured.UserId);

        var evt = await assertDb.SecurityAuditEvents.AsNoTracking().SingleAsync(e => e.TargetUserId == target.Id);
        Assert.Equal(SecurityEventCodes.UserDirectoryRelinked, evt.EventCode);
        Assert.Equal(admin.Id, evt.ActorUserId);
        Assert.Equal("Company re-created the account", evt.Reason);
    }

    [PostgresFact]
    public async Task Relink_Self_Throws()
    {
        var subject = $"sub-{Guid.NewGuid():N}";
        var admin = await SeedUserAsync(RoleType.SystemAdministrator, SignInMethod.Directory, Company("self"), subject);

        await using (var actDb = _fixture.CreateDbContext())
        {
            var (_, users) = CreateServices(actDb);
            var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => users.RelinkDirectoryAsync(admin.Id, "Resetting my own link", admin.Id));
            Assert.Contains("own", ex.Message);
        }

        await using var assertDb = _fixture.CreateDbContext();
        Assert.Equal(subject, (await assertDb.Users.AsNoTracking().SingleAsync(u => u.Id == admin.Id)).DirectorySubjectId);
        Assert.False(await assertDb.AuditLogs.AsNoTracking()
            .AnyAsync(a => a.EntityName == nameof(User) && a.EntityId == admin.Id.ToString() && a.Action != "Create"));
    }

    // ---- user list, create and update --------------------------------------

    [PostgresFact]
    public async Task UserList_ReturnsLinkStates()
    {
        var t = BaseUtc.UtcDateTime;
        var notLinked = await SeedUserAsync();
        var neverSignedIn = await SeedUserAsync(RoleType.Analyst, SignInMethod.Directory, Company("never"));
        var linked = await SeedUserAsync(RoleType.Analyst, SignInMethod.Directory, Company("linked"), "sub-linked-" + Guid.NewGuid().ToString("N"), lastLoginAt: t);
        var refused = await SeedUserAsync(RoleType.Analyst, SignInMethod.Directory, Company("refused"), "sub-refused-" + Guid.NewGuid().ToString("N"), refusedAt: t.AddHours(1), lastLoginAt: t);
        var emergency = await SeedUserAsync(RoleType.SystemAdministrator, isEmergency: true);

        List<UserDto> list;
        UserDto byId;
        await using (var actDb = _fixture.CreateDbContext())
        {
            var (_, users) = CreateServices(actDb);
            list = await users.GetAllAsync();
            byId = await users.GetByIdAsync(refused.Id);
        }

        UserDto Find(User u) => list.Single(x => x.Id == u.Id);
        Assert.Equal("NotLinked", Find(notLinked).LinkState);
        Assert.Equal("LinkedNeverSignedIn", Find(neverSignedIn).LinkState);
        Assert.Equal("Linked", Find(linked).LinkState);
        Assert.Equal("SignInRefused", Find(refused).LinkState);
        Assert.Equal("SignInRefused", byId.LinkState);
        Assert.Equal("Directory", Find(linked).SignInMethod);
        Assert.Equal("Local", Find(notLinked).SignInMethod);
        Assert.Equal(linked.DirectoryUsername, Find(linked).DirectoryUsername);
        Assert.True(Find(emergency).IsEmergencyAccount);
        Assert.False(Find(notLinked).IsEmergencyAccount);

        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(byId, ApiJsonOptionsForTests()));
        Assert.Equal("SignInRefused", doc.RootElement.GetProperty("linkState").GetString());
        Assert.Equal("Directory", doc.RootElement.GetProperty("signInMethod").GetString());
    }

    [PostgresFact]
    public async Task AccessReviewEntry_IncludesSignInMethodAndEmergencyFlag()
    {
        var directoryUser = await SeedUserAsync(RoleType.Analyst, SignInMethod.Directory, Company("review"));
        var emergency = await SeedUserAsync(RoleType.SystemAdministrator, isEmergency: true);

        int reviewId;
        await using (var actDb = _fixture.CreateDbContext())
        {
            var service = new AccessReviewService(actDb, new ElectronicSignatureService(actDb));
            var review = await service.GenerateAsync($"SSO {Guid.NewGuid():N}"[..20], "SSO field check", _fixture.SeededUserId);
            reviewId = review.Id;
        }

        await using var assertDb = _fixture.CreateDbContext();
        var json = await assertDb.AccessReviews.AsNoTracking().Where(r => r.Id == reviewId).Select(r => r.SnapshotJson).SingleAsync();
        using var doc = JsonDocument.Parse(json);
        var entries = doc.RootElement.GetProperty("users").EnumerateArray().ToList();
        var dirEntry = entries.Single(e => e.GetProperty("userId").GetInt32() == directoryUser.Id);
        var emEntry = entries.Single(e => e.GetProperty("userId").GetInt32() == emergency.Id);
        Assert.Equal("Directory", dirEntry.GetProperty("signInMethod").GetString());
        Assert.False(dirEntry.GetProperty("isEmergencyAccount").GetBoolean());
        Assert.Equal("Local", emEntry.GetProperty("signInMethod").GetString());
        Assert.True(emEntry.GetProperty("isEmergencyAccount").GetBoolean());
    }

    [PostgresFact]
    public async Task Create_DirectoryUser_WithoutPassword_Succeeds_Audited()
    {
        var admin = await SeedUserAsync(RoleType.SystemAdministrator);
        var roleId = await RoleIdAsync(RoleType.Analyst);
        var tag = Guid.NewGuid().ToString("N");
        var company = Company("created");

        UserDto created;
        await using (var actDb = _fixture.CreateDbContext())
        {
            var (_, users) = CreateServices(actDb);
            created = await users.CreateAsync(new User
            {
                FullName = "Directory Created", Username = $"sso_new_{tag}", RoleId = roleId,
                SignInMethod = SignInMethod.Directory, DirectoryUsername = $"  {company.ToUpperInvariant()} "
            }, plainPassword: "", reason: "New starter, company login", actingUserId: admin.Id);
        }

        Assert.Equal("Directory", created.SignInMethod);
        Assert.Equal("LinkedNeverSignedIn", created.LinkState);

        await using var assertDb = _fixture.CreateDbContext();
        var persisted = await assertDb.Users.AsNoTracking().SingleAsync(u => u.Id == created.Id);
        Assert.Equal(SignInMethod.Directory, persisted.SignInMethod);
        Assert.Equal(company, persisted.DirectoryUsername);
        Assert.False(persisted.IsEmergencyAccount);
        Assert.False(persisted.MustChangePassword);
        Assert.Null(persisted.SigningPasswordSetAt);
        Assert.False(string.IsNullOrEmpty(persisted.PasswordHash));
        Assert.False(BCrypt.Net.BCrypt.Verify("", persisted.PasswordHash));

        var audit = await assertDb.AuditLogs.AsNoTracking()
            .SingleAsync(a => a.EntityName == nameof(User) && a.EntityId == created.Id.ToString() && a.Action == "Create");
        Assert.Equal(admin.Id, audit.UserId);
        Assert.Equal("New starter, company login", audit.Reason);
        Assert.Contains(company, audit.NewValue);
        Assert.DoesNotContain(persisted.PasswordHash, audit.NewValue);

        var evt = await assertDb.SecurityAuditEvents.AsNoTracking().SingleAsync(e => e.TargetUserId == created.Id);
        Assert.Equal(SecurityEventCodes.UserDirectoryLinked, evt.EventCode);
        Assert.Equal(admin.Id, evt.ActorUserId);
    }

    [PostgresFact]
    public async Task Create_DirectoryUser_InvalidOrTakenCompanyUsername_Refused()
    {
        var admin = await SeedUserAsync(RoleType.SystemAdministrator);
        var roleId = await RoleIdAsync(RoleType.Analyst);
        var taken = Company("taken");
        await SeedUserAsync(RoleType.Analyst, SignInMethod.Directory, taken);
        var names = new List<string>();

        await using (var actDb = _fixture.CreateDbContext())
        {
            var (_, users) = CreateServices(actDb);
            foreach (var company in new string?[] { null, "   ", "has space@corp.test", taken.ToUpperInvariant() })
            {
                var username = $"sso_bad_{Guid.NewGuid():N}";
                names.Add(username);
                await Assert.ThrowsAsync<BusinessRuleException>(() => users.CreateAsync(new User
                {
                    FullName = "Refused", Username = username, RoleId = roleId,
                    SignInMethod = SignInMethod.Directory, DirectoryUsername = company
                }, "", "Should be refused", actingUserId: admin.Id));
            }
        }

        await using var assertDb = _fixture.CreateDbContext();
        Assert.False(await assertDb.Users.AsNoTracking().AnyAsync(u => names.Contains(u.Username)));
    }

    [PostgresFact]
    public async Task Create_EmergencyRequested_WithoutSecondFactor_Refused()
    {
        var admin = await SeedUserAsync(RoleType.SystemAdministrator);
        var adminRoleId = await RoleIdAsync(RoleType.SystemAdministrator);
        var analystRoleId = await RoleIdAsync(RoleType.Analyst);
        var adminName = $"sso_em_{Guid.NewGuid():N}";
        var analystName = $"sso_em_{Guid.NewGuid():N}";

        await using (var actDb = _fixture.CreateDbContext())
        {
            var (_, users) = CreateServices(actDb);
            var noFactor = await Assert.ThrowsAsync<BusinessRuleException>(() => users.CreateAsync(new User
            {
                FullName = "Emergency Wanted", Username = adminName, RoleId = adminRoleId
            }, Password, "Break-glass account", requestEmergencyAccount: true, actingUserId: admin.Id));
            Assert.Equal("An emergency account must have an active second factor.", noFactor.Message);

            var notAdmin = await Assert.ThrowsAsync<BusinessRuleException>(() => users.CreateAsync(new User
            {
                FullName = "Emergency Wanted", Username = analystName, RoleId = analystRoleId
            }, Password, "Break-glass account", requestEmergencyAccount: true, actingUserId: admin.Id));
            Assert.Equal("Only a System Administrator account can be marked as an emergency account.", notAdmin.Message);
        }

        await using var assertDb = _fixture.CreateDbContext();
        Assert.False(await assertDb.Users.AsNoTracking().AnyAsync(u => u.Username == adminName || u.Username == analystName));
    }

    [PostgresFact]
    public async Task Update_UnchangedSsoFields_WritesNoSsoAudit()
    {
        var admin = await SeedUserAsync(RoleType.SystemAdministrator);
        var company = Company("same");
        var target = await SeedUserAsync(RoleType.Analyst, SignInMethod.Directory, company, "sub-same");
        var emergency = await SeedUserAsync(RoleType.SystemAdministrator, isEmergency: true);

        await using (var actDb = _fixture.CreateDbContext())
        {
            var (_, users) = CreateServices(actDb);
            await users.UpdateProfileAsync(target.Id, "Renamed Directory User", target.Username, target.Email, "Analyst II", admin.Id, "Title change",
                new UserSignInChange(SignInMethod.Directory, company.ToUpperInvariant(), false));
            await users.UpdateProfileAsync(emergency.Id, emergency.FullName, emergency.Username, emergency.Email, "Break-glass", admin.Id, "Title change",
                new UserSignInChange(SignInMethod.Local, null, true));
        }

        await using var assertDb = _fixture.CreateDbContext();
        var persisted = await assertDb.Users.AsNoTracking().SingleAsync(u => u.Id == target.Id);
        Assert.Equal("Renamed Directory User", persisted.FullName);
        Assert.Equal(company, persisted.DirectoryUsername);
        Assert.Equal("sub-same", persisted.DirectorySubjectId);
        Assert.True((await assertDb.Users.AsNoTracking().SingleAsync(u => u.Id == emergency.Id)).IsEmergencyAccount);

        var ids = new[] { target.Id.ToString(), emergency.Id.ToString() };
        var actions = await assertDb.AuditLogs.AsNoTracking()
            .Where(a => a.EntityName == nameof(User) && ids.Contains(a.EntityId))
            .Select(a => a.Action).ToListAsync();
        Assert.Equal(2, actions.Count(a => a == "USER_DATA_UPDATED"));
        Assert.DoesNotContain("USER_SSO_LINK_UPDATED", actions);
        Assert.DoesNotContain("USER_EMERGENCY_ACCOUNT_CHANGED", actions);
        Assert.False(await assertDb.SecurityAuditEvents.AsNoTracking()
            .AnyAsync(e => e.TargetUserId == target.Id || e.TargetUserId == emergency.Id));
    }

    [PostgresFact]
    public async Task Update_SwitchToDirectory_PersistsAndAudits()
    {
        var admin = await SeedUserAsync(RoleType.SystemAdministrator);
        var target = await SeedUserAsync();
        var company = Company("switch");
        uint version;
        await using (var readDb = _fixture.CreateDbContext())
            version = (await readDb.Users.AsNoTracking().SingleAsync(u => u.Id == target.Id)).Version;

        // Stale If-Match, and Directory without a company username: both refused, nothing changes.
        await using (var actDb = _fixture.CreateDbContext())
        {
            var (_, users) = CreateServices(actDb);
            actDb.ExpectedVersion = version + 1;
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => users.UpdateProfileAsync(
                target.Id, target.FullName, target.Username, target.Email, null, admin.Id, "Switch to company login",
                new UserSignInChange(SignInMethod.Directory, company, false)));
        }
        await using (var actDb = _fixture.CreateDbContext())
        {
            var (_, users) = CreateServices(actDb);
            await Assert.ThrowsAsync<BusinessRuleException>(() => users.UpdateProfileAsync(
                target.Id, target.FullName, target.Username, target.Email, null, admin.Id, "Switch to company login",
                new UserSignInChange(SignInMethod.Directory, "  ", false)));
        }
        await using (var midDb = _fixture.CreateDbContext())
        {
            var unchanged = await midDb.Users.AsNoTracking().SingleAsync(u => u.Id == target.Id);
            Assert.Equal(SignInMethod.Local, unchanged.SignInMethod);
            Assert.Null(unchanged.DirectoryUsername);
            Assert.False(await midDb.AuditLogs.AsNoTracking()
                .AnyAsync(a => a.EntityName == nameof(User) && a.EntityId == target.Id.ToString() && a.Action != "Create"));
        }

        UserDto updated;
        await using (var actDb = _fixture.CreateDbContext())
        {
            var (_, users) = CreateServices(actDb);
            actDb.ExpectedVersion = version;
            updated = await users.UpdateProfileAsync(target.Id, target.FullName, target.Username, target.Email, null, admin.Id, "Switch to company login",
                new UserSignInChange(SignInMethod.Directory, company, false));
        }

        Assert.Equal("Directory", updated.SignInMethod);
        Assert.Equal("LinkedNeverSignedIn", updated.LinkState);

        await using var assertDb = _fixture.CreateDbContext();
        var persisted = await assertDb.Users.AsNoTracking().SingleAsync(u => u.Id == target.Id);
        Assert.Equal(SignInMethod.Directory, persisted.SignInMethod);
        Assert.Equal(company, persisted.DirectoryUsername);
        Assert.Equal(BaseUtc.UtcDateTime, persisted.SigningPasswordSetAt);
        Assert.NotEqual(version, persisted.Version);

        var audit = await assertDb.AuditLogs.AsNoTracking()
            .SingleAsync(a => a.EntityName == nameof(User) && a.EntityId == target.Id.ToString() && a.Action == "USER_SSO_LINK_UPDATED");
        Assert.Equal(admin.Id, audit.UserId);
        Assert.Equal("Switch to company login", audit.Reason);
        Assert.Contains("\"SignInMethod\":\"Local\"", audit.PreviousValue);
        Assert.Contains("\"SignInMethod\":\"Directory\"", audit.NewValue);
        Assert.Contains(company, audit.NewValue);

        var evt = await assertDb.SecurityAuditEvents.AsNoTracking().SingleAsync(e => e.TargetUserId == target.Id);
        Assert.Equal(SecurityEventCodes.UserDirectoryLinked, evt.EventCode);
        Assert.Equal(admin.Id, evt.ActorUserId);
    }

    [PostgresFact]
    public async Task Update_EmergencyOffAndSwitchToDirectory_InOneEdit_Succeeds_EmergencyAloneRefused()
    {
        var admin = await SeedUserAsync(RoleType.SystemAdministrator);
        var emergency = await SeedUserAsync(RoleType.SystemAdministrator, isEmergency: true);
        var company = Company("em-switch");

        // Switching method while staying an emergency account is refused.
        await using (var actDb = _fixture.CreateDbContext())
        {
            var (_, users) = CreateServices(actDb);
            var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => users.UpdateProfileAsync(
                emergency.Id, emergency.FullName, emergency.Username, emergency.Email, null, admin.Id, "Move to company login",
                new UserSignInChange(SignInMethod.Directory, company, true)));
            Assert.Equal("Emergency accounts must use Local sign-in.", ex.Message);
        }

        await using (var actDb = _fixture.CreateDbContext())
        {
            var (_, users) = CreateServices(actDb);
            await users.UpdateProfileAsync(emergency.Id, emergency.FullName, emergency.Username, emergency.Email, null, admin.Id, "Move to company login",
                new UserSignInChange(SignInMethod.Directory, company, false));
        }

        await using var assertDb = _fixture.CreateDbContext();
        var persisted = await assertDb.Users.AsNoTracking().SingleAsync(u => u.Id == emergency.Id);
        Assert.False(persisted.IsEmergencyAccount);
        Assert.Equal(SignInMethod.Directory, persisted.SignInMethod);
        var actions = await assertDb.AuditLogs.AsNoTracking()
            .Where(a => a.EntityName == nameof(User) && a.EntityId == emergency.Id.ToString())
            .Select(a => new { a.Action, a.UserId }).ToListAsync();
        Assert.Single(actions, a => a.Action == "USER_EMERGENCY_ACCOUNT_CHANGED" && a.UserId == admin.Id);
        Assert.Single(actions, a => a.Action == "USER_SSO_LINK_UPDATED" && a.UserId == admin.Id);
    }

    // ---- authorization ------------------------------------------------------

    [PostgresFact]
    public async Task Endpoints_WithoutUsersManage_Return403()
    {
        var analyst = await SeedUserAsync(RoleType.Analyst);
        var target = await SeedUserAsync(RoleType.Analyst, SignInMethod.Directory, Company("forbidden"), "sub-forbidden");
        var (app, client) = await StartHostAsync();
        await using var _ = app;

        var loginResp = await client.PostAsJsonAsync("/api/auth/login", new { username = analyst.Username, password = Password });
        Assert.Equal(HttpStatusCode.OK, loginResp.StatusCode);
        using var loginJson = JsonDocument.Parse(await loginResp.Content.ReadAsStringAsync());
        var token = loginJson.RootElement.GetProperty("data").GetProperty("token").GetString();

        var rows = new { rows = new[] { new { username = target.Username, companyUsername = "x@corp.test" } }, reason = "No permission" };
        var requests = new (HttpMethod Method, string Path, object? Body)[]
        {
            (HttpMethod.Get, "/api/admin/sso", null),
            (HttpMethod.Post, "/api/admin/sso/test", null),
            (HttpMethod.Post, "/api/admin/sso/links/preview", rows),
            (HttpMethod.Post, "/api/admin/sso/links/import", rows),
            (HttpMethod.Post, $"/api/admin/users/{target.Id}/directory-relink", new { reason = "No permission" }),
        };

        foreach (var (method, path, body) in requests)
        {
            using var req = new HttpRequestMessage(method, path);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            if (body is not null)
                req.Content = JsonContent.Create(body);
            var resp = await client.SendAsync(req);
            Assert.True(resp.StatusCode == HttpStatusCode.Forbidden, $"{method} {path} returned {(int)resp.StatusCode}");
        }

        await using var assertDb = _fixture.CreateDbContext();
        var persisted = await assertDb.Users.AsNoTracking().SingleAsync(u => u.Id == target.Id);
        Assert.Equal("sub-forbidden", persisted.DirectorySubjectId);
    }

    private async Task<int> RoleIdAsync(RoleType roleType)
    {
        await using var db = _fixture.CreateDbContext();
        return await db.Roles.AsNoTracking().Where(r => r.Type == roleType).OrderBy(r => r.Id).Select(r => r.Id).FirstAsync();
    }

    private static JsonSerializerOptions ApiJsonOptionsForTests()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        ApiJsonOptions.Configure(options);
        return options;
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
}
