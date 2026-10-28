using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Persistence.Migrations;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

// Succession for deactivated users: the migration adds Users.DeactivatedAt (backfilled from the audit trail where
// it can be), creates the enforced Documents.Reassign permission and grants it to the built-in Section Head role
// only, with an audit row and a permission-version bump.
[Collection("PostgresDatabaseCollection")]
public class DocumentReassignmentMigrationPostgresTests
{
    private const string Code = PermissionConstants.DocumentsReassign;
    private const string Process = "Migration 20261027133758_DocumentReassignment";

    private readonly PostgresTestFixture _fixture;

    public DocumentReassignmentMigrationPostgresTests(PostgresTestFixture fixture) => _fixture = fixture;

    // The column already exists in the test database (it is built from the current model), so only the SQL steps are run.
    private static async Task RunSqlAsync(MicroLimsDbContext db, IEnumerable<MigrationOperation> operations)
    {
        foreach (var op in operations.OfType<SqlOperation>())
            await db.Database.ExecuteSqlRawAsync(op.Sql.Replace("{", "{{").Replace("}", "}}")); // literal braces (array literals), not format items
    }

    private static Task UpAsync(MicroLimsDbContext db) => RunSqlAsync(db, new DocumentReassignment().UpOperations);
    private static Task DownAsync(MicroLimsDbContext db) => RunSqlAsync(db, new DocumentReassignment().DownOperations);

    // The state of an existing database before this migration: no such permission, nobody holds it.
    private static async Task ArrangePreMigrationAsync(MicroLimsDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync(@"DELETE FROM ""RolePermissions"" WHERE ""PermissionId"" IN (SELECT ""Id"" FROM ""Permissions"" WHERE ""Code"" = 'Documents.Reassign');
            DELETE FROM ""Permissions"" WHERE ""Code"" = 'Documents.Reassign';");
    }

    private static Task<List<string>> HeldAsync(MicroLimsDbContext db, int roleId) =>
        db.RolePermissions.AsNoTracking().Where(rp => rp.RoleId == roleId).Select(rp => rp.Permission!.Code).ToListAsync();

    [PostgresFact]
    public async Task Up_CreatesTheEnforcedPermission_GrantsItToTheBuiltInSectionHead_Audits_AndBumpsTheVersion()
    {
        await using var db = _fixture.CreateDbContext();
        await using var tx = await db.Database.BeginTransactionAsync();
        await ArrangePreMigrationAsync(db);
        var sectionHead = await db.Roles.AsNoTracking().SingleAsync(r => r.Type == RoleType.SectionHead && r.IsSystemRole);
        var custom = new Role { Name = "Custom section head", Type = RoleType.SectionHead, IsSystemRole = false, IsActive = true };
        db.Roles.Add(custom);
        await db.SaveChangesAsync();
        var versionBefore = await db.Roles.AsNoTracking().ToDictionaryAsync(r => r.Id, r => r.PermissionVersion);

        await UpAsync(db);
        db.ChangeTracker.Clear();

        var permission = await db.Permissions.AsNoTracking().SingleAsync(p => p.Code == Code);
        Assert.True(permission.IsEnforced);
        Assert.Contains("deactivated user", permission.Description);

        Assert.Contains(Code, await HeldAsync(db, sectionHead.Id));
        Assert.DoesNotContain(Code, await HeldAsync(db, custom.Id)); // custom roles are the lab's to configure
        Assert.Equal(versionBefore[sectionHead.Id] + 1, (await db.Roles.AsNoTracking().SingleAsync(r => r.Id == sectionHead.Id)).PermissionVersion);
        Assert.Equal(versionBefore[custom.Id], (await db.Roles.AsNoTracking().SingleAsync(r => r.Id == custom.Id)).PermissionVersion);
        // The built-in administrator is not given a document-responsibility code.
        var admin = await db.Roles.AsNoTracking().SingleAsync(r => r.Type == RoleType.SystemAdministrator && r.IsSystemRole);
        Assert.DoesNotContain(Code, await HeldAsync(db, admin.Id));

        var audit = await db.AuditLogs.AsNoTracking().SingleAsync(a => a.SystemProcessName == Process && a.EntityId == sectionHead.Id.ToString());
        Assert.Equal(ActorType.System, audit.ActorType);
        Assert.Equal(AuditActionCategory.Security, audit.ActionCategory);
        Assert.Equal("ROLE_PERMISSIONS_UPDATED", audit.Action);
        Assert.Contains(Code, audit.NewValue);
        Assert.Contains("owner decision", audit.Reason);

        // Running it again changes nothing: one permission row, no second audit row, no further bump.
        await UpAsync(db);
        db.ChangeTracker.Clear();
        Assert.Equal(1, await db.Permissions.CountAsync(p => p.Code == Code));
        Assert.Equal(1, await db.AuditLogs.CountAsync(a => a.SystemProcessName == Process && a.EntityId == sectionHead.Id.ToString()));
        Assert.Equal(versionBefore[sectionHead.Id] + 1, (await db.Roles.AsNoTracking().SingleAsync(r => r.Id == sectionHead.Id)).PermissionVersion);

        // Down revokes what Up granted (audited, version bumped) and removes the permission.
        await DownAsync(db);
        db.ChangeTracker.Clear();
        Assert.False(await db.Permissions.AnyAsync(p => p.Code == Code));
        Assert.DoesNotContain(Code, await HeldAsync(db, sectionHead.Id));
        Assert.Equal(versionBefore[sectionHead.Id] + 2, (await db.Roles.AsNoTracking().SingleAsync(r => r.Id == sectionHead.Id)).PermissionVersion);
        Assert.True(await db.AuditLogs.AnyAsync(a => a.SystemProcessName == Process + " (Down)" && a.EntityId == sectionHead.Id.ToString()));
    }

    [PostgresFact]
    public async Task Up_NeverGrantsASectionHeadRoleThatAlsoManagesUsers()
    {
        await using var db = _fixture.CreateDbContext();
        await using var tx = await db.Database.BeginTransactionAsync();
        await ArrangePreMigrationAsync(db);
        var sectionHead = await db.Roles.AsNoTracking().SingleAsync(r => r.Type == RoleType.SectionHead && r.IsSystemRole);
        var usersManage = await db.Permissions.Where(p => p.Code == PermissionConstants.UsersManage).Select(p => p.Id).SingleAsync();
        db.RolePermissions.Add(new RolePermission { RoleId = sectionHead.Id, PermissionId = usersManage });
        await db.SaveChangesAsync();

        await UpAsync(db);
        db.ChangeTracker.Clear();

        Assert.DoesNotContain(Code, await HeldAsync(db, sectionHead.Id)); // segregation of duties
        Assert.False(await db.AuditLogs.AnyAsync(a => a.SystemProcessName == Process && a.EntityId == sectionHead.Id.ToString()));
    }

    [PostgresFact]
    public async Task Up_BackfillsDeactivatedAt_FromTheLatestDisableInTheAuditTrail_OfCurrentlyInactiveUsersOnly()
    {
        await using var db = _fixture.CreateDbContext();
        await using var tx = await db.Database.BeginTransactionAsync();
        await ArrangePreMigrationAsync(db);
        var role = await db.Roles.AsNoTracking().FirstAsync(r => r.Type == RoleType.Analyst);

        User NewUser(string name, bool active) => new() { Username = $"{name}-{Guid.NewGuid():N}".Substring(0, 24), FullName = name, PasswordHash = "x", RoleId = role.Id, IsActive = active };
        var disabledTwice = NewUser("twice", false);
        var neverAudited = NewUser("legacy", false);
        var reenabled = NewUser("back", true);
        db.Users.AddRange(disabledTwice, neverAudited, reenabled);
        await db.SaveChangesAsync();

        var first = new DateTime(2026, 3, 1, 8, 0, 0, DateTimeKind.Utc);
        var last = new DateTime(2026, 6, 15, 10, 30, 0, DateTimeKind.Utc);
        foreach (var (user, at) in new[] { (disabledTwice, first), (disabledTwice, last), (reenabled, first) })
            db.AuditLogs.Add(new AuditLog { EntityName = "User", EntityId = user.Id.ToString(), Action = "USER_DISABLED", Timestamp = at, UserId = _fixture.SeededUserId });
        await db.SaveChangesAsync();
        // Not yet recorded: the column is new.
        await db.Database.ExecuteSqlRawAsync(@"UPDATE ""Users"" SET ""DeactivatedAt"" = NULL");

        await UpAsync(db);
        db.ChangeTracker.Clear();

        var users = await db.Users.AsNoTracking().Where(u => u.Id == disabledTwice.Id || u.Id == neverAudited.Id || u.Id == reenabled.Id).ToDictionaryAsync(u => u.Id);
        Assert.Equal(last, users[disabledTwice.Id].DeactivatedAt);
        Assert.Null(users[neverAudited.Id].DeactivatedAt); // unknown: never flagged overdue
        Assert.Null(users[reenabled.Id].DeactivatedAt);    // active again
    }
}
