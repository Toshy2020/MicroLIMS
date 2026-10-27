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

// Owner decision Q13: the five Document Control codes became enforced. The migration keeps in-flight work
// working on existing databases by granting each code to the roles whose users hold matching assignments.
[Collection("PostgresDatabaseCollection")]
public class EnforceDocumentControlPermissionsMigrationPostgresTests
{
    private static readonly string[] Codes =
    {
        PermissionConstants.DocumentsDraftEdit, PermissionConstants.DocumentsRevisionCreate, PermissionConstants.DocumentsReview,
        PermissionConstants.DocumentsApprove, PermissionConstants.DocumentsPeriodicReview
    };

    private readonly PostgresTestFixture _fixture;

    public EnforceDocumentControlPermissionsMigrationPostgresTests(PostgresTestFixture fixture) => _fixture = fixture;

    private static async Task RunAsync(MicroLimsDbContext db, IEnumerable<MigrationOperation> operations)
    {
        foreach (var op in operations.Cast<SqlOperation>())
            await db.Database.ExecuteSqlRawAsync(op.Sql);
    }

    private static Task UpAsync(MicroLimsDbContext db) => RunAsync(db, new EnforceDocumentControlPermissions().UpOperations);
    private static Task DownAsync(MicroLimsDbContext db) => RunAsync(db, new EnforceDocumentControlPermissions().DownOperations);

    private static Task<List<string>> HeldAsync(MicroLimsDbContext db, int roleId) =>
        db.RolePermissions.AsNoTracking().Where(rp => rp.RoleId == roleId).Select(rp => rp.Permission!.Code).ToListAsync();

    private static async Task GrantAsync(MicroLimsDbContext db, Role role, params string[] codes)
    {
        foreach (var code in codes)
        {
            var id = await db.Permissions.Where(p => p.Code == code).Select(p => p.Id).SingleAsync();
            db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = id });
        }
        await db.SaveChangesAsync();
    }

    private async Task<(Role Owner, Role Reviewer, Role Approver, Role Periodic, Role Idle, Role AdminLike, Role AlreadyHolds, User OwnerUser)> ArrangeAsync(MicroLimsDbContext db)
    {
        // The state of an existing database before this migration: codes declared, not enforced, nobody holds them.
        await db.Database.ExecuteSqlRawAsync(
            @"UPDATE ""Permissions"" SET ""IsEnforced"" = FALSE WHERE ""Code"" LIKE 'Documents.%' AND ""Code"" IN ('Documents.DraftEdit','Documents.RevisionCreate','Documents.Review','Documents.Approve','Documents.PeriodicReview');
              DELETE FROM ""RolePermissions"" WHERE ""PermissionId"" IN (SELECT ""Id"" FROM ""Permissions"" WHERE ""Code"" IN ('Documents.DraftEdit','Documents.RevisionCreate','Documents.Review','Documents.Approve','Documents.PeriodicReview'));");

        Role NewRole(string name) => new() { Name = name, Type = RoleType.Analyst, IsSystemRole = false, IsActive = true };
        var owner = NewRole("Q13 owner");
        var reviewer = NewRole("Q13 reviewer");
        var approver = NewRole("Q13 approver");
        var periodic = NewRole("Q13 periodic");
        var idle = NewRole("Q13 idle");
        var adminLike = NewRole("Q13 admin-like");
        var alreadyHolds = NewRole("Q13 already holds");
        db.Roles.AddRange(owner, reviewer, approver, periodic, idle, adminLike, alreadyHolds);
        await db.SaveChangesAsync();
        await GrantAsync(db, adminLike, PermissionConstants.UsersManage);
        await GrantAsync(db, alreadyHolds, PermissionConstants.DocumentsReview);

        User NewUser(string n, Role r) => new() { Username = $"q13-{n}-{Guid.NewGuid():N}".Substring(0, 24), FullName = n, Email = $"{n}@q13.local", PasswordHash = "x", RoleId = r.Id, IsActive = true };
        var ownerUser = NewUser("owner", owner);
        var reviewerUser = NewUser("reviewer", reviewer);
        var approverUser = NewUser("approver", approver);
        var periodicUser = NewUser("periodic", periodic);
        var idleUser = NewUser("idle", idle);
        var adminLikeUser = NewUser("adminlike", adminLike);
        var holdsUser = NewUser("holds", alreadyHolds);
        db.Users.AddRange(ownerUser, reviewerUser, approverUser, periodicUser, idleUser, adminLikeUser, holdsUser);
        await db.SaveChangesAsync();

        var master = new DocumentMaster
        {
            MicroLimsDocumentId = $"Q13-{Guid.NewGuid():N}".Substring(0, 16), CompanyDocumentCode = $"Q13-{Guid.NewGuid():N}".Substring(0, 16),
            Title = "Q13 SOP", DocumentTypeId = _fixture.SeededDocTypeId, DepartmentId = _fixture.SeededDepartmentId, SectionId = _fixture.SeededSectionId,
            DocumentOwnerUserId = ownerUser.Id, CreatedByUserId = ownerUser.Id, Confidentiality = DocumentConfidentiality.Internal
        };
        db.DocumentMasters.Add(master);
        await db.SaveChangesAsync();
        var rev = new DocumentRevision
        {
            DocumentMasterId = master.Id, RevisionNumber = "01", RevisionSequence = 1, RevisionStatus = DocumentRevisionStatus.InReview, CreatedByUserId = ownerUser.Id
        };
        db.DocumentRevisions.Add(rev);
        await db.SaveChangesAsync();
        db.DocumentReviewTasks.AddRange(
            new DocumentReviewTask { DocumentRevisionId = rev.Id, AssignedReviewerUserId = reviewerUser.Id, AssignedByUserId = ownerUser.Id, Status = ReviewTaskStatus.Pending },
            new DocumentReviewTask { DocumentRevisionId = rev.Id, AssignedReviewerUserId = adminLikeUser.Id, AssignedByUserId = ownerUser.Id, Status = ReviewTaskStatus.InProgress, ReviewCycleNumber = 2 },
            new DocumentReviewTask { DocumentRevisionId = rev.Id, AssignedReviewerUserId = holdsUser.Id, AssignedByUserId = ownerUser.Id, Status = ReviewTaskStatus.Pending, ReviewCycleNumber = 3 },
            // Finished work does not count: the idle user's review is complete.
            new DocumentReviewTask { DocumentRevisionId = rev.Id, AssignedReviewerUserId = idleUser.Id, AssignedByUserId = ownerUser.Id, Status = ReviewTaskStatus.Completed, ReviewCycleNumber = 4 });
        db.DocumentApprovalTasks.Add(new DocumentApprovalTask
        {
            DocumentRevisionId = rev.Id, AssignedApproverUserId = approverUser.Id, AssignedByUserId = ownerUser.Id, Status = DocumentApprovalTaskStatus.Pending
        });
        db.PeriodicReviewTasks.Add(new PeriodicReviewTask
        {
            DocumentMasterId = master.Id, DocumentRevisionId = rev.Id, ReviewCycleMonths = 24, ScheduledDueDate = DateTime.UtcNow,
            AssignedReviewerUserId = periodicUser.Id, Status = PeriodicReviewTaskStatus.Pending
        });
        await db.SaveChangesAsync();
        return (owner, reviewer, approver, periodic, idle, adminLike, alreadyHolds, ownerUser);
    }

    [PostgresFact]
    public async Task Up_GrantsInFlightAssigneesRoles_AuditsAndBumpsVersion_SkipsSodConflicts_AndDownReverses()
    {
        await using var db = _fixture.CreateDbContext();
        await using var tx = await db.Database.BeginTransactionAsync();
        var (owner, reviewer, approver, periodic, idle, adminLike, alreadyHolds, _) = await ArrangeAsync(db);
        var versionBefore = await db.Roles.AsNoTracking().ToDictionaryAsync(r => r.Id, r => r.PermissionVersion);

        await UpAsync(db);
        db.ChangeTracker.Clear();

        Assert.Equal(5, await db.Permissions.CountAsync(p => Codes.Contains(p.Code) && p.IsEnforced));

        Assert.Equal(new[] { PermissionConstants.DocumentsDraftEdit, PermissionConstants.DocumentsRevisionCreate }, (await HeldAsync(db, owner.Id)).OrderBy(c => c).ToArray());
        Assert.Equal(new[] { PermissionConstants.DocumentsReview }, await HeldAsync(db, reviewer.Id));
        Assert.Equal(new[] { PermissionConstants.DocumentsApprove }, await HeldAsync(db, approver.Id));
        Assert.Equal(new[] { PermissionConstants.DocumentsPeriodicReview }, await HeldAsync(db, periodic.Id));
        Assert.Empty(await HeldAsync(db, idle.Id));

        // SoD: a role administering the system never receives a GxP code; the skip is logged.
        Assert.Equal(new[] { PermissionConstants.UsersManage }, await HeldAsync(db, adminLike.Id));
        var skipped = await db.AuditLogs.AsNoTracking().SingleAsync(a => a.EntityName == "Role" && a.EntityId == adminLike.Id.ToString() && a.Action == "ROLE_PERMISSION_GRANT_SKIPPED");
        Assert.Contains(PermissionConstants.DocumentsReview, skipped.NewValue);
        Assert.Equal(versionBefore[adminLike.Id], (await db.Roles.AsNoTracking().SingleAsync(r => r.Id == adminLike.Id)).PermissionVersion);

        // One System-actor audit row per role changed, and each changed role's tokens are invalidated.
        foreach (var changed in new[] { owner, reviewer, approver, periodic })
        {
            var audit = await db.AuditLogs.AsNoTracking().SingleAsync(a => a.EntityName == "Role" && a.EntityId == changed.Id.ToString() && a.Action == "ROLE_PERMISSIONS_UPDATED");
            Assert.Equal(ActorType.System, audit.ActorType);
            Assert.Contains("EnforceDocumentControlPermissions", audit.SystemProcessName);
            Assert.Contains("Granted", audit.NewValue);
            Assert.Equal(versionBefore[changed.Id] + 1, (await db.Roles.AsNoTracking().SingleAsync(r => r.Id == changed.Id)).PermissionVersion);
        }
        // Roles with nothing to change are untouched: no audit row, same version.
        foreach (var untouched in new[] { idle, alreadyHolds })
        {
            Assert.False(await db.AuditLogs.AnyAsync(a => a.EntityName == "Role" && a.EntityId == untouched.Id.ToString() && a.Action == "ROLE_PERMISSIONS_UPDATED"));
            Assert.Equal(versionBefore[untouched.Id], (await db.Roles.AsNoTracking().SingleAsync(r => r.Id == untouched.Id)).PermissionVersion);
        }
        Assert.Equal(new[] { PermissionConstants.DocumentsReview }, await HeldAsync(db, alreadyHolds.Id));

        // Idempotent: a second run changes nothing.
        var grants = await db.RolePermissions.CountAsync();
        var audits = await db.AuditLogs.CountAsync(a => a.Action == "ROLE_PERMISSIONS_UPDATED");
        await UpAsync(db);
        Assert.Equal(grants, await db.RolePermissions.CountAsync());
        Assert.Equal(audits, await db.AuditLogs.CountAsync(a => a.Action == "ROLE_PERMISSIONS_UPDATED"));

        // Down reverses the flags and revokes exactly what Up granted (a grant the role already had is kept).
        await DownAsync(db);
        db.ChangeTracker.Clear();
        Assert.Equal(0, await db.Permissions.CountAsync(p => Codes.Contains(p.Code) && p.IsEnforced));
        foreach (var changed in new[] { owner, reviewer, approver, periodic, idle })
            Assert.Empty(await HeldAsync(db, changed.Id));
        Assert.Equal(new[] { PermissionConstants.DocumentsReview }, await HeldAsync(db, alreadyHolds.Id));
        Assert.Equal(new[] { PermissionConstants.UsersManage }, await HeldAsync(db, adminLike.Id));
        Assert.Equal(versionBefore[reviewer.Id] + 2, (await db.Roles.AsNoTracking().SingleAsync(r => r.Id == reviewer.Id)).PermissionVersion);

        await tx.RollbackAsync();
    }
}
