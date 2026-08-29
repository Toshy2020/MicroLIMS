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
    public async Task ExactlyEighteenPermissionsAreSeeded()
    {
        var db = CreateSeededDbContext();
        var codes = await db.Permissions.Select(p => p.Code).ToListAsync();

        Assert.Equal(18, codes.Count);
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
    public async Task SystemAdministrator_HoldsAllEighteenPermissions()
    {
        var db = CreateSeededDbContext();
        var codes = await CodesForRole(db, RoleType.SystemAdministrator);
        Assert.Equal(18, codes.Count);
        Assert.Equal(PermissionConstants.All.OrderBy(c => c), codes.OrderBy(c => c));
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
            PermissionConstants.MasterDataManage
        };

        Assert.Equal(15, codes.Count);
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
            PermissionConstants.TestWorkflowBiochemicalDecision, PermissionConstants.CryovialsManage
        };

        Assert.Equal(4, codes.Count);
        Assert.Equal(expected.OrderBy(c => c), codes.OrderBy(c => c));
        Assert.DoesNotContain(PermissionConstants.SamplesApprove, codes);
    }

    [Fact]
    public async Task Analyst_HoldsExactlyTheCatalogSet()
    {
        var db = CreateSeededDbContext();
        var codes = await CodesForRole(db, RoleType.Analyst);

        var expected = new[]
        {
            PermissionConstants.TestWorkflowExecute, PermissionConstants.CryovialsManage,
            PermissionConstants.MaterialsManage, PermissionConstants.EquipmentManage
        };

        Assert.Equal(4, codes.Count);
        Assert.Equal(expected.OrderBy(c => c), codes.OrderBy(c => c));
        Assert.DoesNotContain(PermissionConstants.SamplesReview, codes);
        Assert.DoesNotContain(PermissionConstants.CryovialsApprove, codes);
    }

    [Fact]
    public async Task TotalGrantCount_IsFortyOne()
    {
        // 18 (SysAdmin) + 15 (SectionHead) + 4 (Reviewer) + 4 (Analyst)
        var db = CreateSeededDbContext();
        var total = await db.RolePermissions.CountAsync();
        Assert.Equal(41, total);
    }

    [Fact]
    public async Task SeedingIsIdempotent_RunningTwiceDoesNotDuplicateRows()
    {
        var db = CreateSeededDbContext();
        DbSeeder.SeedPermissionsAndGrants(db); // second call

        Assert.Equal(18, await db.Permissions.CountAsync());
        Assert.Equal(41, await db.RolePermissions.CountAsync());
    }
}
