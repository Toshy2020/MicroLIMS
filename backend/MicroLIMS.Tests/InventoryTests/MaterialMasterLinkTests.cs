using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.InventoryTests;

// Chemical, Indicator and ReferenceStandard stock lots must reference a
// MaterialMasterEntry (HPLC chain S1, spec 3.1). Existing (legacy) lots
// stay unlinked and must keep saving without one.
public class MaterialMasterLinkTests
{
    private static MicroLimsDbContext NewDb() =>
        new(new DbContextOptionsBuilder<MicroLimsDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static (MaterialService svc, MaterialMasterService masterSvc, int fpUserId, DocumentSection fp, DocumentSection other) Seed(MicroLimsDbContext db)
    {
        var micro = TestServiceFactory.EnsureMicroSection(db);
        var fp = new DocumentSection { Name = "Physicochemical Laboratory", Code = "FP", DepartmentId = micro.DepartmentId, IsActive = true };
        var other = new DocumentSection { Name = "Other Lab", Code = "OTHER", DepartmentId = micro.DepartmentId, IsActive = true };
        db.DocumentSections.AddRange(fp, other);
        var role = new Role { Name = "Analyst", Type = RoleType.Analyst, IsActive = true };
        db.Roles.Add(role);
        db.SaveChanges();
        var fpUser = new User { Username = "fp_" + Guid.NewGuid().ToString("N")[..6], FullName = "Physico", RoleId = role.Id, IsActive = true };
        db.Users.Add(fpUser);
        db.SaveChanges();
        db.UserOrgMemberships.Add(new UserOrgMembership { UserId = fpUser.Id, DepartmentId = fp.DepartmentId, SectionId = fp.Id });
        db.SaveChanges();
        return (new MaterialService(db, new UserSectionScopeService(db)), new MaterialMasterService(db, new UserSectionScopeService(db)), fpUser.Id, fp, other);
    }

    private static async Task<MicroLIMS.Application.DTOs.Responses.MaterialMasterEntryResponse> SeedEntryAsync(
        MaterialMasterService masterSvc, int userId, MaterialMasterCategory category, string code = "NAOH-01", int? sectionId = null) =>
        await masterSvc.CreateAsync(new SaveMaterialMasterEntryRequest(
            code, "Sodium Hydroxide", category, "AR", "Merck", MaterialUnit.Gram, sectionId), userId);

    private static SaveMaterialRequest Req(
        MaterialType type, int? masterEntryId = null, decimal? moisture = null, string materialName = "Typed Name") =>
        new(type, materialName, "Merck", "LOT-" + Guid.NewGuid().ToString("N")[..6],
            DateTime.UtcNow, DateTime.UtcNow.AddYears(1), null, "Shelf 2", 10m, MaterialUnit.Gram, null, null, null,
            MaterialMasterEntryId: masterEntryId, MoisturePercent: moisture);

    [Fact]
    public async Task Create_Chemical_WithoutEntry_Throws()
    {
        await using var db = NewDb();
        var (svc, _, fpUserId, _, _) = Seed(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => svc.CreateAsync(Req(MaterialType.Chemical), fpUserId));
        Assert.Contains("Choose the material master entry", ex.Message);
    }

    [Fact]
    public async Task Create_Chemical_WithIndicatorEntry_Throws()
    {
        await using var db = NewDb();
        var (svc, masterSvc, fpUserId, _, _) = Seed(db);
        var entry = await SeedEntryAsync(masterSvc, fpUserId, MaterialMasterCategory.Indicator);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => svc.CreateAsync(Req(MaterialType.Chemical, entry.Id), fpUserId));
        Assert.Contains("Indicator", ex.Message);
    }

    [Fact]
    public async Task Create_Chemical_WithInactiveEntry_Throws()
    {
        await using var db = NewDb();
        var (svc, masterSvc, fpUserId, _, _) = Seed(db);
        var entry = await SeedEntryAsync(masterSvc, fpUserId, MaterialMasterCategory.Reagent);
        await masterSvc.SetActiveAsync(entry.Id, false, fpUserId);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => svc.CreateAsync(Req(MaterialType.Chemical, entry.Id), fpUserId));
        Assert.Contains("inactive", ex.Message);
    }

    [Fact]
    public async Task Create_Chemical_WithOtherSectionEntry_Throws()
    {
        await using var db = NewDb();
        var (svc, _, fpUserId, _, other) = Seed(db);

        // Seeded directly (not through MaterialMasterService) - the FP user
        // isn't a member of "other", so this is a different lab's entry.
        var entry = new MaterialMasterEntry
        {
            SectionId = other.Id, Code = "NAOH-01", Name = "Sodium Hydroxide", Category = MaterialMasterCategory.Reagent,
            BaseUnit = MaterialUnit.Gram, IsActive = true
        };
        db.MaterialMasterEntries.Add(entry);
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => svc.CreateAsync(Req(MaterialType.Chemical, entry.Id), fpUserId));
        Assert.Contains("another laboratory", ex.Message);
    }

    [Fact]
    public async Task Create_Chemical_TakesNameAndCodeFromEntry()
    {
        await using var db = NewDb();
        var (svc, masterSvc, fpUserId, _, _) = Seed(db);
        var entry = await SeedEntryAsync(masterSvc, fpUserId, MaterialMasterCategory.Reagent);

        var material = await svc.CreateAsync(Req(MaterialType.Chemical, entry.Id, materialName: "Typed Wrong Name"), fpUserId);

        Assert.Equal(entry.Name, material.MaterialName);
        Assert.Equal(entry.Code, material.Code);
        Assert.Equal(entry.Id, material.MaterialMasterEntryId);
    }

    [Fact]
    public async Task Create_DehydratedMedia_NoEntryNeeded()
    {
        await using var db = NewDb();
        var (svc, _, fpUserId, fp, _) = Seed(db);

        // DehydratedMedia isn't offered by the FP lab's type list, so seed
        // a micro user instead for this one - the point is only that
        // RequiresMasterEntry is false for it, no master entry is demanded.
        var micro = TestServiceFactory.EnsureMicroSection(db);
        var role = db.Roles.First(r => r.Type == RoleType.Analyst);
        var microUser = new User { Username = "m_" + Guid.NewGuid().ToString("N")[..6], FullName = "Micro", RoleId = role.Id, IsActive = true };
        db.Users.Add(microUser);
        await db.SaveChangesAsync();
        TestServiceFactory.AssignUserToMicroSection(db, microUser.Id);

        var product = new MicroLIMS.Domain.Entities.MediaProduct { Name = "TSA", Code = "TSA" };
        db.MediaProducts.Add(product);
        await db.SaveChangesAsync();

        var req = new SaveMaterialRequest(MaterialType.DehydratedMedia, "TSA", "Himedia", "B1",
            DateTime.UtcNow, DateTime.UtcNow.AddYears(1), null, "Shelf", 10m, MaterialUnit.Gram, null, null, null,
            MediaProductId: product.Id);

        var material = await svc.CreateAsync(req, microUser.Id);
        Assert.Null(material.MaterialMasterEntryId);
    }

    [Fact]
    public async Task Create_ReferenceStandard_MoistureOutOfRange_Throws()
    {
        await using var db = NewDb();
        var (svc, masterSvc, fpUserId, _, _) = Seed(db);
        var entry = await SeedEntryAsync(masterSvc, fpUserId, MaterialMasterCategory.ReferenceStandard);

        var req = Req(MaterialType.ReferenceStandard, entry.Id, moisture: 100m) with { Purity = 99.5m };
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => svc.CreateAsync(req, fpUserId));
        Assert.Contains("Moisture", ex.Message);
    }

    [Fact]
    public async Task Create_Chemical_WithMoisture_Throws()
    {
        await using var db = NewDb();
        var (svc, masterSvc, fpUserId, _, _) = Seed(db);
        var entry = await SeedEntryAsync(masterSvc, fpUserId, MaterialMasterCategory.Reagent);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => svc.CreateAsync(Req(MaterialType.Chemical, entry.Id, moisture: 2m), fpUserId));
        Assert.Contains("only allowed for reference standards", ex.Message);
    }

    [Fact]
    public async Task Update_LegacyUnlinkedChemical_SavesWithoutEntry()
    {
        await using var db = NewDb();
        var (svc, _, fpUserId, fp, _) = Seed(db);

        var legacy = new Material
        {
            SectionId = fp.Id, MaterialType = MaterialType.Chemical, MaterialName = "Legacy Chem", ManufacturerName = "X",
            BatchNumber = "B1", ReceivingDate = DateTime.UtcNow, Location = "L", QuantityReceived = 1, QuantityRemaining = 1,
            Unit = MaterialUnit.Gram
        };
        db.Materials.Add(legacy);
        await db.SaveChangesAsync();

        await svc.UpdateAsync(legacy.Id, Req(MaterialType.Chemical, materialName: "Legacy Chem Renamed"), fpUserId);

        var reloaded = await db.Materials.FindAsync(legacy.Id);
        Assert.Null(reloaded!.MaterialMasterEntryId);
        Assert.Equal("Legacy Chem Renamed", reloaded.MaterialName);
    }

    [Fact]
    public async Task Update_LinkedLot_WhoseEntryWasDeactivated_StillSaves()
    {
        await using var db = NewDb();
        var (svc, masterSvc, fpUserId, fp, _) = Seed(db);
        var entry = await SeedEntryAsync(masterSvc, fpUserId, MaterialMasterCategory.Reagent);

        var lot = await svc.CreateAsync(Req(MaterialType.Chemical, entry.Id), fpUserId);
        await masterSvc.SetActiveAsync(entry.Id, false, fpUserId);

        await svc.UpdateAsync(lot.Id, Req(MaterialType.Chemical, entry.Id, materialName: "Doesn't matter"), fpUserId);

        var reloaded = await db.Materials.FindAsync(lot.Id);
        Assert.Equal(entry.Id, reloaded!.MaterialMasterEntryId);
        Assert.Equal(entry.Name, reloaded.MaterialName);
    }
}
