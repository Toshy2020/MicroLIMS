using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.InventoryTests;

// Confirms the Inventory consumption guards added to MediaPreparationService
// and CryovialService actually block on expiry/insufficient quantity,
// and - critically for a GMP system - leave no partial write behind
// when they do (no Media row, no decremented stock, no Cryovial row,
// no decremented discs).
public class MaterialConsumptionTests
{
    private static MicroLimsDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MicroLimsDbContext(options);
    }

    private static async Task SeedCurrentCoa(MicroLimsDbContext db, int materialId)
    {
        db.MaterialDocuments.Add(new MicroLIMS.Domain.Entities.MaterialDocument
        {
            MaterialId = materialId,
            DocumentType = MicroLIMS.Domain.Enums.MaterialDocumentType.COA,
            OriginalFileName = "COA.pdf",
            StorageKey = $"material-documents/{materialId}/test.pdf",
            FileExtension = ".pdf",
            ContentType = "application/pdf",
            FileSizeBytes = 1024,
            ContentSha256 = "AABBCC",
            UploadedByUserId = 1,
            UploadedAt = DateTime.UtcNow,
            Status = MicroLIMS.Domain.Enums.MaterialDocumentStatus.Current
        });
        await db.SaveChangesAsync();
    }

    private static async Task<(Equipment autoclave, Material material)> SeedMediaPrepFixtures(
        MicroLimsDbContext db, decimal materialQuantity, DateTime? materialExpiry = null)
    {
        var autoclave = new Equipment { Name = "Autoclave 1", Code = "AUT-01", Type = EquipmentType.Autoclave };
        var material = new Material
        {
            MaterialType = MaterialType.DehydratedMedia, MaterialName = "TSA Powder", ManufacturerName = "Himedia",
            BatchNumber = "LOT-001", ReceivingDate = DateTime.UtcNow.AddDays(-10),
            ExpiryDate = materialExpiry ?? DateTime.UtcNow.AddYears(1), Code = "TSA",
            Location = "Micro Lab", QuantityReceived = materialQuantity, QuantityRemaining = materialQuantity,
            Unit = MaterialUnit.Gram
        };
        db.Equipment.Add(autoclave);
        db.Materials.Add(material);
        db.MediaConfigurations.Add(new MediaConfiguration
        {
            Name = material.MaterialName, EvaluationType = EvaluationType.GrowthPromotion,
            IncubationMinHours = 24, IncubationMaxHours = 48, TemperatureMin = 30, TemperatureMax = 35,
            RecoveryPercentMin = 50, RecoveryPercentMax = 200
        });
        await db.SaveChangesAsync();
        return (autoclave, material);
    }

    [Fact]
    public async Task PrepareMedia_InsufficientStock_ThrowsAndWritesNothing()
    {
        await using var db = NewDb();
        var (autoclave, material) = await SeedMediaPrepFixtures(db, materialQuantity: 50m);
        var service = TestServiceFactory.MediaPreparation(db);

        var request = new PrepareMediaRequest(
            MaterialId: material.Id,
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
        var (autoclave, material) = await SeedMediaPrepFixtures(db, materialQuantity: 500m, materialExpiry: DateTime.UtcNow.AddDays(-1));
        var service = TestServiceFactory.MediaPreparation(db);

        var request = new PrepareMediaRequest(
            MaterialId: material.Id,
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
        var (autoclave, material) = await SeedMediaPrepFixtures(db, materialQuantity: 500m);
        await SeedCurrentCoa(db, material.Id); // DehydratedMedia requires a current COA
        var service = TestServiceFactory.MediaPreparation(db);

        var request = new PrepareMediaRequest(
            MaterialId: material.Id,
            TotalWeight: 100m, TotalVolume: "500 ml", AutoclaveEquipmentId: autoclave.Id, AutoclaveProgram: "Program A",
            LoadType: "agar", Temperature: 121m, CycleTime: 15, CycleNumber: 1,
            Ph: 7.2m, ExpiryDate: DateTime.UtcNow.AddMonths(1), UserId: 1);

        var media = await service.PrepareAsync(request);

        Assert.Single(await db.Media.ToListAsync());
        var reloaded = await db.Materials.FindAsync(material.Id);
        Assert.Equal(400m, reloaded!.QuantityRemaining); // 500 - 100
        Assert.Equal(100m, media.TotalWeight);
        Assert.Equal(material.Id, media.MaterialId);
        Assert.Equal(material.BatchNumber, media.ManufacturerLot);
        Assert.Equal(material.ManufacturerName, media.ManufacturerName);
    }

    private static async Task<Material> SeedLyophilizedMaterial(MicroLimsDbContext db, decimal discs, DateTime? expiry = null)
    {
        var organism = new Organism { ScientificName = "E. coli", AtccNumber = "8739" };
        db.Organisms.Add(organism);
        await db.SaveChangesAsync();

        var material = new Material
        {
            MaterialType = MaterialType.LyophilizedMicroorganism, MaterialName = "E. coli", ManufacturerName = "Tody laboratories",
            BatchNumber = "LOT-EC-01", ReceivingDate = DateTime.UtcNow.AddDays(-10),
            ExpiryDate = expiry ?? DateTime.UtcNow.AddYears(1), Code = "ECOLI", AtccNumber = "8739", OrganismId = organism.Id,
            Location = "Micro refrigerator", QuantityReceived = discs, QuantityRemaining = discs,
            Unit = MaterialUnit.Disc
        };
        db.Materials.Add(material);
        await db.SaveChangesAsync();
        return material;
    }

    // A released Media lot + Incubator, needed for the identity-confirmation
    // panel row that PrepareCryovialsAsync now requires (at least one row).
    private static async Task<(Media media, Equipment incubator)> SeedReleasedMediaFixtures(MicroLimsDbContext db)
    {
        var mediaMaterial = new Material
        {
            MaterialType = MaterialType.DehydratedMedia, MaterialName = "TSA Powder", ManufacturerName = "Himedia",
            BatchNumber = "LOT-TSA", ReceivingDate = DateTime.UtcNow.AddDays(-30), ExpiryDate = DateTime.UtcNow.AddYears(1),
            Code = "TSA", Location = "Micro Lab", QuantityReceived = 500, QuantityRemaining = 500, Unit = MaterialUnit.Gram
        };
        var incubator = new Equipment { Name = "Incubator 1", Code = "INC-01", Type = EquipmentType.Incubator };
        db.Materials.Add(mediaMaterial);
        db.Equipment.Add(incubator);
        await db.SaveChangesAsync();

        var media = new Media
        {
            MaterialId = mediaMaterial.Id, LotNumber = "TSA/01/26",
            IsReleasedForUse = true, Status = MediaStatus.Active, ExpiryDate = DateTime.UtcNow.AddYears(1)
        };
        db.Media.Add(media);
        await db.SaveChangesAsync();
        return (media, incubator);
    }

    private static List<IdentityConfirmationRow> OnePanelRow(Media media, Equipment incubator) => new()
    {
        new IdentityConfirmationRow(media.Id, incubator.Id, DateTime.UtcNow.AddDays(-2), DateTime.UtcNow.AddDays(-1), "Typical colonies")
    };

    [Fact]
    public async Task PrepareCryovials_InsufficientDiscs_ThrowsAndWritesNothing()
    {
        await using var db = NewDb();
        var material = await SeedLyophilizedMaterial(db, discs: 2);
        var (media, incubator) = await SeedReleasedMediaFixtures(db);
        var service = TestServiceFactory.Cryovial(db);

        var request = new PrepareCryovialsRequest(
            material.Id, NumberOfVialsPrepared: 5, ExpiryDate: DateTime.UtcNow.AddMonths(6),
            StorageCondition: "Freezer -15 to -25", PhysicalCheckConfirmed: true, PhysicalCheckText: "OK",
            Panel: OnePanelRow(media, incubator), DiscsUsed: 3, UserId: 1);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.PrepareCryovialsAsync(request));

        Assert.Empty(await db.Cryovials.ToListAsync());
        var reloaded = await db.Materials.FindAsync(material.Id);
        Assert.Equal(2m, reloaded!.QuantityRemaining); // unchanged
    }

    [Fact]
    public async Task PrepareCryovials_ExpiredMaterial_ThrowsAndWritesNothing()
    {
        await using var db = NewDb();
        var material = await SeedLyophilizedMaterial(db, discs: 10, expiry: DateTime.UtcNow.AddDays(-1));
        var (media, incubator) = await SeedReleasedMediaFixtures(db);
        var service = TestServiceFactory.Cryovial(db);

        var request = new PrepareCryovialsRequest(
            material.Id, NumberOfVialsPrepared: 5, ExpiryDate: DateTime.UtcNow.AddMonths(6),
            StorageCondition: "Freezer -15 to -25", PhysicalCheckConfirmed: true, PhysicalCheckText: "OK",
            Panel: OnePanelRow(media, incubator), DiscsUsed: 2, UserId: 1);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.PrepareCryovialsAsync(request));

        Assert.Empty(await db.Cryovials.ToListAsync());
        var reloaded = await db.Materials.FindAsync(material.Id);
        Assert.Equal(10m, reloaded!.QuantityRemaining); // unchanged
    }

    [Fact]
    public async Task PrepareCryovials_PhysicalCheckNotConfirmed_ThrowsAndWritesNothing()
    {
        await using var db = NewDb();
        var material = await SeedLyophilizedMaterial(db, discs: 10);
        var (media, incubator) = await SeedReleasedMediaFixtures(db);
        await SeedCurrentCoa(db, material.Id);
        var service = TestServiceFactory.Cryovial(db);

        var request = new PrepareCryovialsRequest(
            material.Id, NumberOfVialsPrepared: 5, ExpiryDate: DateTime.UtcNow.AddMonths(6),
            StorageCondition: "Freezer -15 to -25", PhysicalCheckConfirmed: false, PhysicalCheckText: "Discrepancy noted",
            Panel: OnePanelRow(media, incubator), DiscsUsed: 1, UserId: 1);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.PrepareCryovialsAsync(request));
        Assert.Contains("Physical check confirmation against the organism reference description is required", ex.Message);

        Assert.Empty(await db.Cryovials.ToListAsync());
        var reloaded = await db.Materials.FindAsync(material.Id);
        Assert.Equal(10m, reloaded!.QuantityRemaining); // stock untouched
    }

    [Fact]
    public async Task PrepareCryovials_SufficientDiscs_DecrementsAndCreatesCryovial()
    {
        await using var db = NewDb();
        var material = await SeedLyophilizedMaterial(db, discs: 10);
        var (media, incubator) = await SeedReleasedMediaFixtures(db);
        await SeedCurrentCoa(db, material.Id); // LyophilizedMicroorganism requires a current COA
        var service = TestServiceFactory.Cryovial(db);

        var request = new PrepareCryovialsRequest(
            material.Id, NumberOfVialsPrepared: 5, ExpiryDate: DateTime.UtcNow.AddMonths(6),
            StorageCondition: "Freezer -15 to -25", PhysicalCheckConfirmed: true, PhysicalCheckText: "Conforms to reference description",
            Panel: OnePanelRow(media, incubator), DiscsUsed: 3, UserId: 1);

        var cryovial = await service.PrepareCryovialsAsync(request);

        Assert.Single(await db.Cryovials.ToListAsync());
        var reloaded = await db.Materials.FindAsync(material.Id);
        Assert.Equal(7m, reloaded!.QuantityRemaining); // 10 - 3
        Assert.Equal(5, cryovial.NumberOfVialsPrepared);
        Assert.Equal(5, cryovial.VialsRemaining);
        Assert.Equal(material.Id, cryovial.MaterialId);
        Assert.Equal(material.OrganismId, cryovial.OrganismId);
        Assert.Equal("E. coli", cryovial.OrganismNameSnapshot);
        Assert.True(cryovial.PhysicalCheckConfirmed);
        Assert.Equal("Conforms to reference description", cryovial.PhysicalCheckText);
    }
}
