using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.InventoryTests;

// Confirms the Inventory consumption guards added to MediaPreparationService
// and ReferenceStrainService actually block on expiry/insufficient
// quantity, and - critically for a GMP system - leave no partial write
// behind when they do (no Media row, no decremented stock, no Cryovial
// row, no decremented discs).
public class MaterialConsumptionTests
{
    private static MicroLimsDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MicroLimsDbContext(options);
    }

    private static async Task<(MediaType mediaType, Equipment autoclave, Material material)> SeedMediaPrepFixtures(
        MicroLimsDbContext db, decimal materialQuantity, DateTime? materialExpiry = null)
    {
        var mediaType = new MediaType
        {
            Name = "Tryptic Soy Agar", Code = "TSA", Class = MediaClass.GeneralAgar,
            IncubationMinHours = 24, IncubationMaxHours = 48,
            RequiredTemperatureMin = 30, RequiredTemperatureMax = 35
        };
        var autoclave = new Equipment { Name = "Autoclave 1", Code = "AUT-01", Type = EquipmentType.Autoclave };
        var material = new Material
        {
            MaterialType = MaterialType.DehydratedMedia, MaterialName = "TSA Powder", ManufacturerName = "Himedia",
            BatchNumber = "LOT-001", ReceivingDate = DateTime.UtcNow.AddDays(-10),
            ExpiryDate = materialExpiry ?? DateTime.UtcNow.AddYears(1),
            Location = "Micro Lab", QuantityReceived = materialQuantity, QuantityRemaining = materialQuantity,
            Unit = MaterialUnit.Gram
        };
        db.MediaTypes.Add(mediaType);
        db.Equipment.Add(autoclave);
        db.Materials.Add(material);
        await db.SaveChangesAsync();
        return (mediaType, autoclave, material);
    }

    [Fact]
    public async Task PrepareMedia_InsufficientStock_ThrowsAndWritesNothing()
    {
        await using var db = NewDb();
        var (mediaType, autoclave, material) = await SeedMediaPrepFixtures(db, materialQuantity: 50m);
        var service = new MediaPreparationService(db, new MaterialService(db));

        var request = new PrepareMediaRequest(
            MediaTypeId: mediaType.Id, MaterialId: material.Id, ManufacturerLot: "MFG-LOT", ManufacturerName: "Himedia",
            TotalWeight: 100m, TotalVolume: "500 ml", AutoclaveEquipmentId: autoclave.Id, AutoclaveProgram: "Program A",
            LoadType: "agar", Temperature: 121m, CycleTime: 15, CycleNumber: 1,
            Ph: 7.2m, ExpiryDate: DateTime.UtcNow.AddMonths(1), UserId: 1);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.PrepareAsync(request));

        Assert.Empty(await db.Media.ToListAsync());
        var reloaded = await db.Materials.FindAsync(material.Id);
        Assert.Equal(50m, reloaded!.QuantityRemaining); // unchanged
    }

    [Fact]
    public async Task PrepareMedia_ExpiredMaterial_ThrowsAndWritesNothing()
    {
        await using var db = NewDb();
        var (mediaType, autoclave, material) = await SeedMediaPrepFixtures(db, materialQuantity: 500m, materialExpiry: DateTime.UtcNow.AddDays(-1));
        var service = new MediaPreparationService(db, new MaterialService(db));

        var request = new PrepareMediaRequest(
            MediaTypeId: mediaType.Id, MaterialId: material.Id, ManufacturerLot: "MFG-LOT", ManufacturerName: "Himedia",
            TotalWeight: 100m, TotalVolume: "500 ml", AutoclaveEquipmentId: autoclave.Id, AutoclaveProgram: "Program A",
            LoadType: "agar", Temperature: 121m, CycleTime: 15, CycleNumber: 1,
            Ph: 7.2m, ExpiryDate: DateTime.UtcNow.AddMonths(1), UserId: 1);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.PrepareAsync(request));

        Assert.Empty(await db.Media.ToListAsync());
        var reloaded = await db.Materials.FindAsync(material.Id);
        Assert.Equal(500m, reloaded!.QuantityRemaining); // unchanged
    }

    [Fact]
    public async Task PrepareMedia_SufficientStock_DecrementsAndCreatesMediaAtomically()
    {
        await using var db = NewDb();
        var (mediaType, autoclave, material) = await SeedMediaPrepFixtures(db, materialQuantity: 500m);
        var service = new MediaPreparationService(db, new MaterialService(db));

        var request = new PrepareMediaRequest(
            MediaTypeId: mediaType.Id, MaterialId: material.Id, ManufacturerLot: "MFG-LOT", ManufacturerName: "Himedia",
            TotalWeight: 100m, TotalVolume: "500 ml", AutoclaveEquipmentId: autoclave.Id, AutoclaveProgram: "Program A",
            LoadType: "agar", Temperature: 121m, CycleTime: 15, CycleNumber: 1,
            Ph: 7.2m, ExpiryDate: DateTime.UtcNow.AddMonths(1), UserId: 1);

        var media = await service.PrepareAsync(request);

        Assert.Single(await db.Media.ToListAsync());
        var reloaded = await db.Materials.FindAsync(material.Id);
        Assert.Equal(400m, reloaded!.QuantityRemaining); // 500 - 100
        Assert.Equal(100m, media.TotalWeight);
    }

    private static async Task<ReferenceStrain> SeedApprovedStrain(MicroLimsDbContext db, int discs, DateTime? expiry = null)
    {
        var strain = new ReferenceStrain
        {
            Code = "RS 01/07/26", OrganismName = "E. coli", AtccNumber = "8739", PassageNumber = 1,
            NumberOfDiscs = discs, DiscsRemaining = discs, ManufacturerName = "Tody laboratories",
            ExpiryDate = expiry ?? DateTime.UtcNow.AddYears(1), StorageCondition = "Micro refrigerator",
            ApprovalStatus = ApprovalGateStatus.Approved
        };
        db.ReferenceStrains.Add(strain);
        await db.SaveChangesAsync();
        return strain;
    }

    [Fact]
    public async Task PrepareCryovials_InsufficientDiscs_ThrowsAndWritesNothing()
    {
        await using var db = NewDb();
        var strain = await SeedApprovedStrain(db, discs: 2);
        var service = new ReferenceStrainService(db);

        var request = new PrepareCryovialsRequest(
            strain.Id, "Tody laboratories", DateTime.UtcNow.AddMonths(6), NumberOfVialsPrepared: 5,
            StorageCondition: "Freezer -15 to -25", PhysicalCheckText: "OK",
            Panel: new List<IdentityConfirmationRow>(), DiscsUsed: 3, UserId: 1);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.PrepareCryovialsAsync(request));

        Assert.Empty(await db.Cryovials.ToListAsync());
        var reloaded = await db.ReferenceStrains.FindAsync(strain.Id);
        Assert.Equal(2, reloaded!.DiscsRemaining); // unchanged
    }

    [Fact]
    public async Task PrepareCryovials_ExpiredStrain_ThrowsAndWritesNothing()
    {
        await using var db = NewDb();
        var strain = await SeedApprovedStrain(db, discs: 10, expiry: DateTime.UtcNow.AddDays(-1));
        var service = new ReferenceStrainService(db);

        var request = new PrepareCryovialsRequest(
            strain.Id, "Tody laboratories", DateTime.UtcNow.AddMonths(6), NumberOfVialsPrepared: 5,
            StorageCondition: "Freezer -15 to -25", PhysicalCheckText: "OK",
            Panel: new List<IdentityConfirmationRow>(), DiscsUsed: 2, UserId: 1);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.PrepareCryovialsAsync(request));

        Assert.Empty(await db.Cryovials.ToListAsync());
        var reloaded = await db.ReferenceStrains.FindAsync(strain.Id);
        Assert.Equal(10, reloaded!.DiscsRemaining); // unchanged
    }

    [Fact]
    public async Task PrepareCryovials_SufficientDiscs_DecrementsAndCreatesCryovial()
    {
        await using var db = NewDb();
        var strain = await SeedApprovedStrain(db, discs: 10);
        var service = new ReferenceStrainService(db);

        var request = new PrepareCryovialsRequest(
            strain.Id, "Tody laboratories", DateTime.UtcNow.AddMonths(6), NumberOfVialsPrepared: 5,
            StorageCondition: "Freezer -15 to -25", PhysicalCheckText: "OK",
            Panel: new List<IdentityConfirmationRow>(), DiscsUsed: 3, UserId: 1);

        var cryovial = await service.PrepareCryovialsAsync(request);

        Assert.Single(await db.Cryovials.ToListAsync());
        var reloaded = await db.ReferenceStrains.FindAsync(strain.Id);
        Assert.Equal(7, reloaded!.DiscsRemaining); // 10 - 3
        Assert.Equal(5, cryovial.NumberOfVialsPrepared);
    }
}
