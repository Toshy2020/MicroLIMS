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

    private static async Task<List<string>> CodesForRole(MicroLimsDbContext db, RoleType type)
    {
        var permissionService = new PermissionService(db);
        var roleId = await db.Roles.Where(r => r.Type == type).Select(r => r.Id).FirstAsync();
        return await permissionService.GetPermissionCodesForRoleAsync(roleId);
    }

    [Fact]
    public async Task SystemAdministrator_HoldsEveryPermission()
    {
        var db = CreateSeededDbContext();
        var codes = await CodesForRole(db, RoleType.SystemAdministrator);
        Assert.Equal(PermissionConstants.All.Count, codes.Count);
        Assert.Equal(PermissionConstants.All.OrderBy(c => c), codes.OrderBy(c => c));

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
            PermissionConstants.DocumentsTrainingAssign, PermissionConstants.DocumentsTrainingViewMatrix
        };

        Assert.Equal(26, codes.Count);
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
            PermissionConstants.DocumentsApprove, PermissionConstants.DocumentsTrainingViewMatrix
        };

        Assert.Equal(11, codes.Count);
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
            PermissionConstants.DocumentsRevisionCreate
        };

        Assert.Equal(10, codes.Count);
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
        // 33 (SysAdmin) + 26 (SectionHead) + 11 (Reviewer) + 10 (Analyst) = 80
        // System.ViewSecurityAudit is granted to SystemAdministrator only.
        var db = CreateSeededDbContext();
        var total = await db.RolePermissions.CountAsync();
        Assert.Equal(80, total);
    }

    [Fact]
    public async Task SeedingIsIdempotent_RunningTwiceDoesNotDuplicateRows()
    {
        var db = CreateSeededDbContext();
        DbSeeder.SeedPermissionsAndGrants(db); // second call

        Assert.Equal(PermissionConstants.All.Count, await db.Permissions.CountAsync());
        Assert.Equal(80, await db.RolePermissions.CountAsync());
    }
}
