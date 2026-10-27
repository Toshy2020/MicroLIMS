using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Persistence.Seed;
using MicroLIMS.Shared.Constants;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

// Verifies DbSeeder.SeedPermissionsAndGrants reproduces
// rbac-permission-catalog.md exactly. Calls the seeding method directly
// rather than the full DbSeeder.Seed pipeline, which pulls in a large
// amount of unrelated master-data seeding (media, equipment, test
// definitions) that isn't relevant here and isn't guaranteed to behave
// the same way against the EF InMemory provider.
public class RolePermissionSeedDataTests
{
    private static MicroLimsDbContext CreateSeededDbContext()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var db = new MicroLimsDbContext(options);

        db.Roles.AddRange(
            new Role { Type = RoleType.SystemAdministrator, Name = "System Administrator", IsSystemRole = true, IsActive = true },
            new Role { Type = RoleType.SectionHead, Name = "Section Head", IsSystemRole = true, IsActive = true },
            new Role { Type = RoleType.Reviewer, Name = "Reviewer", IsSystemRole = true, IsActive = true },
            new Role { Type = RoleType.Analyst, Name = "Analyst", IsSystemRole = true, IsActive = true }
        );
        db.SaveChanges();

        DbSeeder.SeedPermissionsAndGrants(db);
        return db;
    }

    [Fact]
    public async Task EveryCatalogPermissionIsSeeded()
    {
        var db = CreateSeededDbContext();
        var codes = await db.Permissions.Select(p => p.Code).ToListAsync();

        // Counted from the catalog rather than a literal, so adding a permission
        // does not require editing this assertion - the per-role sets below stay
        // literal on purpose, as a guard against grants changing by accident.
        Assert.Equal(PermissionConstants.All.Count, codes.Count);
        Assert.Equal(PermissionConstants.All.OrderBy(c => c), codes.OrderBy(c => c));
    }

    [Fact]
    public async Task EveryPermissionHasANonEmptyDescription()
    {
        var db = CreateSeededDbContext();
        var permissions = await db.Permissions.ToListAsync();
        Assert.All(permissions, p => Assert.False(string.IsNullOrWhiteSpace(p.Description)));
    }

    [Fact]
    public async Task ExistingStalePermissionRows_AreResyncedFromCatalog()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var db = new MicroLimsDbContext(options);
        db.Roles.Add(new Role { Type = RoleType.SystemAdministrator, Name = "System Administrator", IsSystemRole = true, IsActive = true });
        db.SaveChanges();

        // A row seeded by an older release: wrong description and not yet
        // flagged enforced, although the code is in the Enforced set.
        db.Permissions.Add(new Permission
        {
            Code = PermissionConstants.SamplesReceive,
            Description = "stale description from an older release",
            IsEnforced = false
        });
        db.SaveChanges();

        DbSeeder.SeedPermissionsAndGrants(db);

        var row = await db.Permissions.SingleAsync(p => p.Code == PermissionConstants.SamplesReceive);
        var expected = PermissionConstants.Enforced.Contains(PermissionConstants.SamplesReceive);
        Assert.True(expected);
        Assert.True(row.IsEnforced);
        Assert.NotEqual("stale description from an older release", row.Description);
        Assert.False(string.IsNullOrWhiteSpace(row.Description));
        Assert.Equal(PermissionConstants.All.Count, await db.Permissions.CountAsync());
    }

    private static async Task<List<string>> CodesForRole(MicroLimsDbContext db, RoleType type)
    {
        var permissionService = new PermissionService(db);
        var roleId = await db.Roles.Where(r => r.Type == type).Select(r => r.Id).FirstAsync();
        return await permissionService.GetPermissionCodesForRoleAsync(roleId);
    }

    [Fact]
    public async Task SystemAdministrator_HoldsExactlyTheSystemAdministratorGrants()
    {
        var db = CreateSeededDbContext();
        var codes = await CodesForRole(db, RoleType.SystemAdministrator);
        Assert.Equal(PermissionConstants.SystemAdministratorGrants.Count, codes.Count);
        Assert.Equal(PermissionConstants.SystemAdministratorGrants.OrderBy(c => c), codes.OrderBy(c => c));

        // Segregation of duties: the administrator controls the system but does
        // not execute, correct or supervise GxP work.
        Assert.DoesNotContain(PermissionConstants.SamplesReceive, codes);
        Assert.DoesNotContain(PermissionConstants.SamplesCorrect, codes);
        Assert.DoesNotContain(PermissionConstants.TestWorkflowSupervise, codes);
        Assert.DoesNotContain(PermissionConstants.TestWorkflowExecute, codes);

        // Document Control capabilities the specification denies the
        // administrator are deliberately absent from the catalog entirely, so
        // granting All cannot confer them: voiding a Document Master (FS-1a-111,
        // "not even SystemAdministrator") and overriding a revision number
        // (FRS-1B §3.2:184) remain fixed Document Controller rules.
        Assert.DoesNotContain("Documents.Void", codes);
        Assert.DoesNotContain("Documents.RevisionOverrideNumber", codes);
    }

    [Fact]
    public async Task SectionHead_HoldsExactlyTheCatalogSet()
    {
        var db = CreateSeededDbContext();
        var codes = await CodesForRole(db, RoleType.SectionHead);

        var expected = new[]
        {
            PermissionConstants.AuditView, PermissionConstants.SamplesReview, PermissionConstants.SamplesApprove,
            PermissionConstants.SamplesReceive, PermissionConstants.SamplesTrackAll,
            PermissionConstants.SignaturesManage, PermissionConstants.TestWorkflowExecute, PermissionConstants.TestWorkflowBiochemicalDecision,
            PermissionConstants.CryovialsManage, PermissionConstants.CryovialsApprove,
            PermissionConstants.MaterialsManage, PermissionConstants.MaterialsDocumentControl,
            PermissionConstants.EquipmentManage, PermissionConstants.EquipmentDocumentControl,
            PermissionConstants.ItemsManage, PermissionConstants.ItemsDocumentUpload,
            PermissionConstants.MasterDataManage,
            PermissionConstants.DiscussionsView, PermissionConstants.DiscussionsCreate,
            PermissionConstants.DiscussionsEditAny, PermissionConstants.MessagesUse,
            // Document Control, per the FRS-1A permission matrix.
            PermissionConstants.DocumentsRegister, PermissionConstants.DocumentsDraftEdit,
            PermissionConstants.DocumentsRevisionCreate, PermissionConstants.DocumentsPeriodicReview,
            PermissionConstants.DocumentsApprove,
            PermissionConstants.DocumentsTrainingAssign, PermissionConstants.DocumentsTrainingViewMatrix,
            PermissionConstants.DashboardsLabOverview, PermissionConstants.DashboardsReview, PermissionConstants.KpiView, PermissionConstants.KpiViewTeam, PermissionConstants.KpiConfigureProfiles, PermissionConstants.MediaPrepare, PermissionConstants.MediaRelease, PermissionConstants.OosManage, PermissionConstants.TestWorkflowSupervise, PermissionConstants.SamplesReceiveOwnLab, PermissionConstants.SamplesCorrect, PermissionConstants.SamplesAssignAnalyst,
            // HPLC chain S4 - Solution Preparation area.
            PermissionConstants.SolutionsPrepare,
            // HPLC chain S6 - HPLC Workspace.
            PermissionConstants.HplcOperate,
            // Working standard qualification.
            PermissionConstants.WorkingStandardsQualify,
            // Annual Monitoring Plan.
            PermissionConstants.MonitoringPlanView, PermissionConstants.MonitoringPlanManage, PermissionConstants.MonitoringPlanApprove,
            PermissionConstants.EquipmentPlanManage, PermissionConstants.EquipmentPlanApprove, PermissionConstants.EquipmentWorkRecord, PermissionConstants.CalendarEventManage
        };

        Assert.Equal(50, codes.Count);
        Assert.Equal(expected.OrderBy(c => c), codes.OrderBy(c => c));
        // Not granted to SectionHead per the catalog:
        Assert.DoesNotContain(PermissionConstants.UsersManage, codes);
        Assert.DoesNotContain(PermissionConstants.RolesManage, codes);
        Assert.DoesNotContain(PermissionConstants.ReportingAdmin, codes);
    }

    [Fact]
    public async Task Reviewer_HoldsExactlyTheCatalogSet()
    {
        var db = CreateSeededDbContext();
        var codes = await CodesForRole(db, RoleType.Reviewer);

        var expected = new[]
        {
            PermissionConstants.SamplesReview, PermissionConstants.TestWorkflowExecute,
            PermissionConstants.TestWorkflowBiochemicalDecision, PermissionConstants.CryovialsManage,
            PermissionConstants.DiscussionsView, PermissionConstants.DiscussionsCreate,
            PermissionConstants.MessagesUse,
            // Technical reviewer / QA auditor. Review, periodic review and
            // approval are each still gated on the per-document assignment.
            PermissionConstants.DocumentsReview, PermissionConstants.DocumentsPeriodicReview,
            PermissionConstants.DocumentsApprove, PermissionConstants.DocumentsTrainingViewMatrix,
            PermissionConstants.DashboardsReview, PermissionConstants.MediaPrepare, PermissionConstants.SamplesReceiveOwnLab, PermissionConstants.SamplesCorrect,
            PermissionConstants.MonitoringPlanView,
            // Equipment plan: record calibrations and logbook entries, add calendar events.
            PermissionConstants.EquipmentWorkRecord, PermissionConstants.CalendarEventManage
        };

        Assert.Equal(18, codes.Count);
        Assert.Equal(expected.OrderBy(c => c), codes.OrderBy(c => c));
        Assert.DoesNotContain(PermissionConstants.SamplesApprove, codes);
        // A reviewer does not register documents or edit drafts.
        Assert.DoesNotContain(PermissionConstants.DocumentsRegister, codes);
        Assert.DoesNotContain(PermissionConstants.DocumentsDraftEdit, codes);
    }

    [Fact]
    public async Task Analyst_HoldsExactlyTheCatalogSet()
    {
        var db = CreateSeededDbContext();
        var codes = await CodesForRole(db, RoleType.Analyst);

        var expected = new[]
        {
            PermissionConstants.TestWorkflowExecute, PermissionConstants.CryovialsManage,
            PermissionConstants.MaterialsManage, PermissionConstants.EquipmentManage,
            PermissionConstants.DiscussionsView, PermissionConstants.DiscussionsCreate,
            PermissionConstants.MessagesUse,
            // Document Author capabilities (FRS-1A matrix grants registration to
            // Document Author). RoleType has no Document Author, so these sit on
            // Analyst and a lab can revoke them on the Roles screen.
            PermissionConstants.DocumentsRegister, PermissionConstants.DocumentsDraftEdit,
            PermissionConstants.DocumentsRevisionCreate,
            PermissionConstants.MediaPrepare, PermissionConstants.SamplesReceiveOwnLab,
            // HPLC chain S4 - Solution Preparation area.
            PermissionConstants.SolutionsPrepare,
            // HPLC chain S6 - HPLC Workspace.
            PermissionConstants.HplcOperate,
            // Working standard qualification.
            PermissionConstants.WorkingStandardsQualify,
            PermissionConstants.MonitoringPlanView,
            // Equipment plan: record calibrations and logbook entries.
            PermissionConstants.EquipmentWorkRecord
        };

        Assert.Equal(17, codes.Count);
        Assert.Equal(expected.OrderBy(c => c), codes.OrderBy(c => c));
        Assert.DoesNotContain(PermissionConstants.SamplesReview, codes);
        Assert.DoesNotContain(PermissionConstants.CryovialsApprove, codes);
        // An author neither reviews nor approves - the SoD invariant (BR-013).
        Assert.DoesNotContain(PermissionConstants.DocumentsReview, codes);
        Assert.DoesNotContain(PermissionConstants.DocumentsApprove, codes);
    }

    [Fact]
    public async Task TotalGrantCount_MatchesTheCatalog()
    {
        // The System Administrator now holds only PermissionConstants.SystemAdministratorGrants
        // (25 system codes, no lab/GxP codes). The total is the sum of the four per-role
        // sets that the tests above assert literally:
        //   SystemAdministrator 25 + SectionHead 50 + Reviewer 18 + Analyst 17 = 110.
        // Update this only when a per-role set above changes.
        var db = CreateSeededDbContext();
        var total = await db.RolePermissions.CountAsync();
        Assert.Equal(110, total);
    }

    [Fact]
    public async Task SeedingIsIdempotent_RunningTwiceDoesNotDuplicateRows()
    {
        var db = CreateSeededDbContext();
        DbSeeder.SeedPermissionsAndGrants(db); // second call

        Assert.Equal(PermissionConstants.All.Count, await db.Permissions.CountAsync());
        Assert.Equal(110, await db.RolePermissions.CountAsync()); // 25 + 50 + 18 + 17, see TotalGrantCount_MatchesTheCatalog
    }

    [Fact]
    public async Task SamplesReceive_IsSectionHeadOnly()
    {
        var db = CreateSeededDbContext();
        var code = PermissionConstants.SamplesReceive;

        Assert.Contains(code, await CodesForRole(db, RoleType.SectionHead));
        Assert.DoesNotContain(code, await CodesForRole(db, RoleType.SystemAdministrator));
        Assert.DoesNotContain(code, await CodesForRole(db, RoleType.Reviewer));
        Assert.DoesNotContain(code, await CodesForRole(db, RoleType.Analyst));
    }

    [Fact]
    public async Task SamplesTrackAll_IsSystemAdministratorAndSectionHeadOnly()
    {
        var db = CreateSeededDbContext();
        var code = PermissionConstants.SamplesTrackAll;

        Assert.Contains(code, await CodesForRole(db, RoleType.SystemAdministrator));
        Assert.Contains(code, await CodesForRole(db, RoleType.SectionHead));
        Assert.DoesNotContain(code, await CodesForRole(db, RoleType.Reviewer));
        Assert.DoesNotContain(code, await CodesForRole(db, RoleType.Analyst));
    }
}
