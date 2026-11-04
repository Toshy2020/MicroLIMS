using Microsoft.EntityFrameworkCore;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Persistence.Seed;
using MicroLIMS.Shared.Constants;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

// QC Manager signs the final approval of a "not applicable" test and otherwise
// only looks: it holds Reviewer's view/read permissions and nothing that acts.
public class QcManagerRoleTests
{
    private static MicroLimsDbContext Seeded()
    {
        var db = new MicroLimsDbContext(new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        foreach (var type in Enum.GetValues<RoleType>())
            db.Roles.Add(new Role { Type = type, Name = type.ToString(), IsSystemRole = true, IsActive = true });
        db.SaveChanges();
        DbSeeder.SeedPermissionsAndGrants(db);
        return db;
    }

    private static async Task<List<string>> CodesOf(MicroLimsDbContext db, RoleType type) =>
        await db.RolePermissions.Where(rp => rp.Role!.Type == type).Select(rp => rp.Permission!.Code).ToListAsync();

    private static bool IsViewOrRead(string c) =>
        c.EndsWith(".view", StringComparison.OrdinalIgnoreCase) || c.EndsWith(".read", StringComparison.OrdinalIgnoreCase);

    [Fact]
    public async Task QcManager_HoldsFinalApproveAndReadOnly()
    {
        var db = Seeded();
        var codes = await CodesOf(db, RoleType.QcManager);

        Assert.Contains(PermissionConstants.NotApplicableTestsFinalApprove, codes);
        var acting = codes.Where(c => c != PermissionConstants.NotApplicableTestsFinalApprove
            && new[] { ".manage", ".record", ".sign", ".approve" }.Any(s => c.EndsWith(s, StringComparison.OrdinalIgnoreCase)));
        Assert.Empty(acting);

        // Everything else it holds is something Reviewer also holds under a view/read code.
        var reviewerViews = (await CodesOf(db, RoleType.Reviewer)).Where(IsViewOrRead).OrderBy(c => c).ToList();
        Assert.Equal(reviewerViews, codes.Where(c => c != PermissionConstants.NotApplicableTestsFinalApprove).OrderBy(c => c));
    }

    [Fact]
    public async Task SectionHead_HoldsNotApplicableManageAndOutsource()
    {
        var db = Seeded();
        var head = await CodesOf(db, RoleType.SectionHead);
        Assert.Contains(PermissionConstants.NotApplicableTestsManage, head);
        Assert.Contains(PermissionConstants.TestsOutsource, head);
        // Final approval is the QC Manager's alone.
        Assert.DoesNotContain(PermissionConstants.NotApplicableTestsFinalApprove, head);
        // Administrators never execute or approve GxP work.
        var admin = await CodesOf(db, RoleType.SystemAdministrator);
        Assert.DoesNotContain(PermissionConstants.NotApplicableTestsManage, admin);
        Assert.DoesNotContain(PermissionConstants.TestsOutsource, admin);
        Assert.DoesNotContain(PermissionConstants.NotApplicableTestsFinalApprove, admin);
    }

    [Fact]
    public async Task Reviewer_DoesNotHoldTheNewPermissions()
    {
        var db = Seeded();
        var codes = await CodesOf(db, RoleType.Reviewer);
        Assert.DoesNotContain(PermissionConstants.NotApplicableTestsManage, codes);
        Assert.DoesNotContain(PermissionConstants.NotApplicableTestsFinalApprove, codes);
        Assert.DoesNotContain(PermissionConstants.TestsOutsource, codes);
    }

    [Fact]
    public async Task SamplesPrepare_IsHeldByEveryRoleButQcManager()
    {
        var db = Seeded();
        foreach (var role in new[] { RoleType.SectionHead, RoleType.Reviewer, RoleType.Analyst })
            Assert.Contains(PermissionConstants.SamplesPrepare, await CodesOf(db, role));
        Assert.DoesNotContain(PermissionConstants.SamplesPrepare, await CodesOf(db, RoleType.QcManager));
        Assert.DoesNotContain(PermissionConstants.SamplesPrepare, await CodesOf(db, RoleType.SystemAdministrator));
    }
}
