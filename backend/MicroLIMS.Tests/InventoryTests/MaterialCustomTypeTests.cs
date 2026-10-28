using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.InventoryTests;

// Each laboratory's stock register offers its own built-in types. Anything else
// is an Other material received against a master entry; the free-text custom type
// is no longer accepted for new lots (lots that already have one keep it).
public class MaterialCustomTypeTests
{
    private static MicroLimsDbContext NewDb() =>
        new(new DbContextOptionsBuilder<MicroLimsDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static (MaterialService svc, int microUserId, int fpUserId, DocumentSection fp) Seed(MicroLimsDbContext db)
    {
        var micro = TestServiceFactory.EnsureMicroSection(db);
        var fp = new DocumentSection { Name = "Physicochemical Laboratory", Code = "FP", DepartmentId = micro.DepartmentId, IsActive = true };
        db.DocumentSections.Add(fp);
        var role = new Role { Name = "Analyst", Type = RoleType.Analyst, IsActive = true };
        db.Roles.Add(role);
        db.SaveChanges();
        var microUser = new User { Username = "m", FullName = "Micro", RoleId = role.Id, IsActive = true };
        var fpUser = new User { Username = "f", FullName = "Physico", RoleId = role.Id, IsActive = true };
        db.Users.AddRange(microUser, fpUser);
        db.SaveChanges();
        TestServiceFactory.AssignUserToMicroSection(db, microUser.Id);
        db.UserOrgMemberships.Add(new UserOrgMembership { UserId = fpUser.Id, DepartmentId = fp.DepartmentId, SectionId = fp.Id });
        db.SaveChanges();
        return (new MaterialService(db, new UserSectionScopeService(db)), microUser.Id, fpUser.Id, fp);
    }

    private static SaveMaterialRequest Req(MaterialType type, string? customType = null, decimal? purity = null) =>
        new(type, "Acetonitrile HPLC grade", "Merck", "LOT-" + Guid.NewGuid().ToString("N")[..6],
            DateTime.UtcNow, DateTime.UtcNow.AddYears(1), null, "Shelf 2", 10m, MaterialUnit.Liter, null, null, null,
            Purity: purity, CustomType: customType);

    [Fact]
    public async Task TypeOptions_EachLabGetsItsOwnBuiltInTypes()
    {
        await using var db = NewDb();
        var (svc, microUserId, fpUserId, _) = Seed(db);

        var fpOptions = await svc.GetTypeOptionsAsync(fpUserId, null);
        var microOptions = await svc.GetTypeOptionsAsync(microUserId, null);

        Assert.DoesNotContain(MaterialType.DehydratedMedia, fpOptions.BuiltIn);
        Assert.DoesNotContain(MaterialType.LyophilizedMicroorganism, fpOptions.BuiltIn);
        Assert.Contains(MaterialType.ReferenceStandard, fpOptions.BuiltIn);
        Assert.Contains(MaterialType.DehydratedMedia, microOptions.BuiltIn);
        Assert.DoesNotContain(MaterialType.ReferenceStandard, microOptions.BuiltIn);
    }

    [Fact]
    public async Task TypeOptions_NoLongerOfferTheSixLegacyNamedTypes()
    {
        await using var db = NewDb();
        var (svc, microUserId, fpUserId, _) = Seed(db);

        foreach (var userId in new[] { microUserId, fpUserId })
        {
            var options = await svc.GetTypeOptionsAsync(userId, null);
            Assert.Contains(MaterialType.Other, options.BuiltIn);
            Assert.All(MaterialTypeRules.LegacyNamedTypes, t => Assert.DoesNotContain(t, options.BuiltIn));
        }
    }

    [Fact]
    public async Task Create_FreeTextCustomType_IsRefused_ButExistingOnesStayListed()
    {
        await using var db = NewDb();
        var (svc, microUserId, fpUserId, fp) = Seed(db);
        db.Materials.Add(new Material
        {
            SectionId = fp.Id, MaterialType = MaterialType.Other, CustomType = "HPLC Solvent", MaterialName = "Old", ManufacturerName = "X",
            BatchNumber = "B1", ReceivingDate = DateTime.UtcNow, Location = "L", QuantityReceived = 1, QuantityRemaining = 1,
            Unit = MaterialUnit.Liter
        });
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => svc.CreateAsync(Req(MaterialType.Other, "  HPLC   Solvent "), fpUserId));
        Assert.Contains("no longer accepted", ex.Message);
        Assert.Equal(new[] { "HPLC Solvent" }, (await svc.GetTypeOptionsAsync(fpUserId, null)).Custom);
        Assert.Empty((await svc.GetTypeOptionsAsync(microUserId, null)).Custom);
    }

    [Fact]
    public async Task Create_RefusesAnotherLabsTypeAndMisusedCustomNames()
    {
        await using var db = NewDb();
        var (svc, _, fpUserId, _) = Seed(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.CreateAsync(Req(MaterialType.AntibioticDisc), fpUserId));
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.CreateAsync(Req(MaterialType.Chemical, "Solvent"), fpUserId));
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.CreateAsync(Req(MaterialType.Other, "reference standard"), fpUserId));
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.CreateAsync(Req(MaterialType.Other, new string('x', 101)), fpUserId));
    }

    [Fact]
    public async Task Update_KeepsAnOlderTypeButRefusesChangingToAnotherLabsType()
    {
        await using var db = NewDb();
        var (svc, _, fpUserId, fp) = Seed(db);
        var legacy = new Material
        {
            SectionId = fp.Id, MaterialType = MaterialType.Supplement, MaterialName = "Old", ManufacturerName = "X",
            BatchNumber = "B1", ReceivingDate = DateTime.UtcNow, Location = "L", QuantityReceived = 1, QuantityRemaining = 1,
            Unit = MaterialUnit.Milliliter
        };
        db.Materials.Add(legacy);
        await db.SaveChangesAsync();

        await svc.UpdateAsync(legacy.Id, Req(MaterialType.Supplement), fpUserId);
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.UpdateAsync(legacy.Id, Req(MaterialType.AntibioticDisc), fpUserId));

        // No new free-text type, and Other needs a master entry.
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.UpdateAsync(legacy.Id, Req(MaterialType.Other, "Glassware"), fpUserId));
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.UpdateAsync(legacy.Id, Req(MaterialType.Other), fpUserId));
        await svc.UpdateAsync(legacy.Id, Req(MaterialType.Chemical), fpUserId);
        Assert.Null((await db.Materials.FindAsync(legacy.Id))!.CustomType);
    }

    [Fact]
    public async Task Update_KeepsAnOlderCustomTypeOnTheSameType()
    {
        await using var db = NewDb();
        var (svc, _, fpUserId, fp) = Seed(db);
        var legacy = new Material
        {
            SectionId = fp.Id, MaterialType = MaterialType.Other, CustomType = "Glassware", MaterialName = "Old", ManufacturerName = "X",
            BatchNumber = "B1", ReceivingDate = DateTime.UtcNow, Location = "L", QuantityReceived = 1, QuantityRemaining = 1,
            Unit = MaterialUnit.Piece
        };
        db.Materials.Add(legacy);
        await db.SaveChangesAsync();

        await svc.UpdateAsync(legacy.Id, Req(MaterialType.Other, "glassware"), fpUserId);
        await svc.UpdateAsync(legacy.Id, Req(MaterialType.Other), fpUserId);
        Assert.Equal("Glassware", (await db.Materials.FindAsync(legacy.Id))!.CustomType);
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.UpdateAsync(legacy.Id, Req(MaterialType.Other, "Something else"), fpUserId));
    }
}
