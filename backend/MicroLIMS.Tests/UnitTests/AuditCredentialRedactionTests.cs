using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Attributes;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Infrastructure.Email;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

// The automatic audit capture serializes every column of a changed entity
// into the immutable AuditLogs table, which Audit.View holders can read back.
// Credential hashes must therefore be written as "[REDACTED]" - the fact of
// the change stays visible, the value never does.
public class AuditCredentialRedactionTests
{
    private const string CurrentPassword = "CurrentPass1!";
    private const string ReplacementPassword = "Replacement2@";
    private const string Redacted = "\"PasswordHash\":\"[REDACTED]\"";

    private static MicroLimsDbContext CreateDbContext()
    {
        var db = new MicroLimsDbContext(new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Roles.Add(new Role { Id = 4, Type = RoleType.Analyst, Name = "Analyst" });
        db.SaveChanges();
        return db;
    }

    private static AuthenticationService CreateAuth(MicroLimsDbContext db) => new(
        db, subject => $"jwt-for-{subject.UserId}", new PermissionService(db),
        new EmailSender("", 587, "", "", "no-reply@microlims.local", false),
        NullLogger<AuthenticationService>.Instance,
        new SecurityAuditService(db, new SystemSecurityRequestContext()));

    private static User SeedUser(MicroLimsDbContext db)
    {
        var user = new User
        {
            FullName = "Test Analyst",
            Username = "analyst1",
            RoleId = 4,
            PasswordHash = TestPasswords.Hash(CurrentPassword),
            IsActive = true
        };
        db.Users.Add(user);
        db.SaveChanges();
        return user;
    }

    private static void AssertNoHashLeak(AuditLog row)
    {
        // BCrypt hashes begin with $2a$/$2b$/$2y$.
        Assert.DoesNotContain("$2", row.PreviousValue ?? string.Empty);
        Assert.DoesNotContain("$2", row.NewValue ?? string.Empty);
    }

    [Fact]
    public async Task SuccessfulLogin_WritesUserAuditRowWithRedactedPasswordHash()
    {
        using var db = CreateDbContext();
        SeedUser(db);
        var auth = CreateAuth(db);

        var outcome = await auth.LoginAsync("analyst1", CurrentPassword);
        Assert.True(outcome.Success);

        var rows = await db.AuditLogs
            .Where(a => a.EntityName == nameof(User) && a.Action == "Update")
            .ToListAsync();
        Assert.NotEmpty(rows);
        foreach (var row in rows)
        {
            Assert.Contains(Redacted, row.PreviousValue);
            Assert.Contains(Redacted, row.NewValue);
            AssertNoHashLeak(row);
        }
    }

    [Fact]
    public async Task PasswordChange_RedactsPasswordHistoryCreateAndUserUpdateRows()
    {
        using var db = CreateDbContext();
        var user = SeedUser(db);
        var auth = CreateAuth(db);

        Assert.True(await auth.ChangePasswordAsync(user.Id, CurrentPassword, ReplacementPassword));

        var history = Assert.Single(await db.AuditLogs
            .Where(a => a.EntityName == nameof(PasswordHistory) && a.Action == "Create")
            .ToListAsync());
        Assert.Contains(Redacted, history.NewValue);
        AssertNoHashLeak(history);

        var userUpdates = await db.AuditLogs
            .Where(a => a.EntityName == nameof(User) && a.Action == "Update")
            .ToListAsync();
        Assert.NotEmpty(userUpdates);
        foreach (var row in userUpdates)
        {
            Assert.Contains(Redacted, row.PreviousValue);
            Assert.Contains(Redacted, row.NewValue);
            AssertNoHashLeak(row);
        }
    }

    [Fact]
    public async Task OtherColumnsOfTheSameRow_AreStillCaptured()
    {
        using var db = CreateDbContext();
        SeedUser(db);

        var row = Assert.Single(await db.AuditLogs
            .Where(a => a.EntityName == nameof(User) && a.Action == "Create")
            .ToListAsync());

        Assert.Contains(Redacted, row.NewValue);
        Assert.Contains("\"Username\":\"analyst1\"", row.NewValue);
    }

    // ---- Drift protection ----

    // String properties that look like credential material but are not, so
    // they deliberately stay readable in the audit trail. Every entry needs
    // a reason; anything else matching the name rule must be [AuditRedacted].
    private static readonly IReadOnlyList<(Type EntityType, string PropertyName, string Reason)> NonSecretAllowlist = new[]
    {
        (typeof(DocumentAcknowledgementRecord), nameof(DocumentAcknowledgementRecord.ControlledFileHash),
            "SHA-256 of the controlled document file the user acknowledged - evidence of which file was read, not a credential"),
    };

    // Guards against a new credential-like column being added later and
    // flowing into AuditLogs in clear. Matches by name only.
    [Fact]
    public void EveryCredentialLookingStringProperty_IsRedactedOrExplicitlyAllowlisted()
    {
        using var db = CreateDbContext();

        var offenders = db.Model.GetEntityTypes()
            .Select(e => e.ClrType)
            .Distinct()
            .Where(t => !MicroLimsDbContext.AuditCaptureExcludedTypes.Any(x => x.IsAssignableFrom(t)))
            .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            .Where(p => p.PropertyType == typeof(string) && LooksLikeCredential(p.Name))
            .Where(p => !p.IsDefined(typeof(AuditRedactedAttribute), inherit: true))
            .Where(p => !NonSecretAllowlist.Any(a => a.EntityType == p.DeclaringType && a.PropertyName == p.Name))
            .Select(p => $"{p.DeclaringType!.Name}.{p.Name}")
            .ToList();

        Assert.True(offenders.Count == 0,
            "String propert(ies) that look like credentials are neither [AuditRedacted] nor in the allowlist: " +
            string.Join(", ", offenders));
    }

    [Fact]
    public void NonSecretAllowlist_EveryEntryMatchesARealUnredactedProperty()
    {
        foreach (var (entityType, propertyName, reason) in NonSecretAllowlist)
        {
            var property = entityType.GetProperty(propertyName);
            Assert.True(property is not null, $"{entityType.Name}.{propertyName} does not exist.");
            Assert.False(property!.IsDefined(typeof(AuditRedactedAttribute), inherit: true),
                $"{entityType.Name}.{propertyName} is both redacted and allowlisted.");
            Assert.False(string.IsNullOrWhiteSpace(reason));
        }
    }

    [Fact]
    public void TheFiveKnownCredentialHashes_AreMarkedRedacted()
    {
        var expected = new (Type, string)[]
        {
            (typeof(User), nameof(User.PasswordHash)),
            (typeof(PasswordHistory), nameof(PasswordHistory.PasswordHash)),
            (typeof(PasswordResetToken), nameof(PasswordResetToken.TokenHash)),
            (typeof(AdminPasswordRecovery), nameof(AdminPasswordRecovery.CodeHash)),
            (typeof(InstrumentAgent), nameof(InstrumentAgent.KeyHash)),
        };
        foreach (var (type, name) in expected)
            Assert.True(type.GetProperty(name)!.IsDefined(typeof(AuditRedactedAttribute), inherit: true),
                $"{type.Name}.{name} must be [AuditRedacted].");
    }

    private static bool LooksLikeCredential(string name) =>
        name.EndsWith("Hash", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("Secret", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("Token", StringComparison.OrdinalIgnoreCase);
}
