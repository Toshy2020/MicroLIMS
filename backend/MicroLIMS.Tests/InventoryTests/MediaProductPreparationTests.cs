using System.Globalization;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.InventoryTests;

public class MediaProductPreparationTests
{
    private static readonly string Yy = DateTime.UtcNow.ToString("yy", CultureInfo.InvariantCulture);

    private static MicroLimsDbContext NewDb(string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
            .Options;
        var db = new MicroLimsDbContext(options);
        db.CurrentUserId = 1;
        return db;
    }

    private static async Task<Equipment> SeedAutoclaveAsync(MicroLimsDbContext db)
    {
        var autoclave = new Equipment { Name = "Autoclave 1", Code = "AUT-01", Type = EquipmentType.Autoclave };
        db.Equipment.Add(autoclave);
        await db.SaveChangesAsync();
        return autoclave;
    }

    private static async Task SeedCurrentCoaAsync(MicroLimsDbContext db, int materialId)
    {
        db.MaterialDocuments.Add(new MaterialDocument
        {
            MaterialId = materialId,
            DocumentType = MaterialDocumentType.COA,
            OriginalFileName = "COA.pdf",
            StorageKey = $"material-documents/{materialId}/test.pdf",
            FileExtension = ".pdf",
            ContentType = "application/pdf",
            FileSizeBytes = 1024,
            ContentSha256 = "COAHASH",
            UploadedByUserId = 1,
            UploadedAt = DateTime.UtcNow,
            Status = MaterialDocumentStatus.Current
        });
        await db.SaveChangesAsync();
    }

    private static PrepareMediaRequest MediaRequest(Material material, Equipment autoclave) => new(
        MaterialId: material.Id,
        TotalWeight: 50m,
        TotalVolume: "500 ml",
        AutoclaveEquipmentId: autoclave.Id,
        AutoclaveProgram: "Program A",
        LoadType: "agar",
        Temperature: 121m,
        CycleTime: 15,
        CycleNumber: 1,
        Ph: 7.2m,
        ExpiryDate: DateTime.UtcNow.AddMonths(1),
        UserId: 1);

    [Fact]
    public async Task PrepareAsync_UnlinkedBatch_ThrowsAndLeavesStockUnchanged()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var db = NewDb(dbName);
        var autoclave = await SeedAutoclaveAsync(db);

        var material = new Material
        {
            MaterialType = MaterialType.DehydratedMedia,
            MaterialName = "Unlinked TSA",
            ManufacturerName = "Himedia",
            BatchNumber = "BATCH-UNLINKED",
            ReceivingDate = DateTime.UtcNow.AddDays(-5),
            ExpiryDate = DateTime.UtcNow.AddYears(1),
            Code = "TSA",
            Location = "Lab",
            QuantityReceived = 500,
            QuantityRemaining = 500,
            Unit = MaterialUnit.Gram,
            MediaProductId = null
        };
        db.Materials.Add(material);
        await db.SaveChangesAsync();
        await SeedCurrentCoaAsync(db, material.Id);

        var service = TestServiceFactory.MediaPreparation(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.PrepareAsync(MediaRequest(material, autoclave)));
        Assert.Contains("isn't linked to a configured media product", ex.Message);

        await using var checkDb = NewDb(dbName);
        var reloaded = await checkDb.Materials.FindAsync(material.Id);
        Assert.Equal(500m, reloaded!.QuantityRemaining);
        Assert.Empty(await checkDb.Media.ToListAsync());
    }

    [Fact]
    public async Task PrepareAsync_LotPrefixComesFromProductCode_EvenWhenMaterialCodeDiffers()
    {
        await using var db = NewDb();
        var autoclave = await SeedAutoclaveAsync(db);
        var product = await MediaProductTestData.CreateOrGetAsync(db, "Tryptic Soy Agar", "TSAPROD");

        var material = new Material
        {
            MaterialType = MaterialType.DehydratedMedia,
            MaterialName = product.Name,
            ManufacturerName = "Himedia",
            BatchNumber = "BATCH-01",
            ReceivingDate = DateTime.UtcNow.AddDays(-5),
            ExpiryDate = DateTime.UtcNow.AddYears(1),
            Code = "DRIFTED",
            Location = "Lab",
            QuantityReceived = 500,
            QuantityRemaining = 500,
            Unit = MaterialUnit.Gram,
            MediaProductId = product.Id
        };
        db.Materials.Add(material);

        db.MediaConfigurations.Add(new MediaConfiguration
        {
            MediaProductId = product.Id,
            Name = product.Name,
            EvaluationType = EvaluationType.GrowthPromotion,
            IncubationCondition = MediaProductTestData.Condition(product)
        });
        await db.SaveChangesAsync();
        await SeedCurrentCoaAsync(db, material.Id);

        var service = TestServiceFactory.MediaPreparation(db);
        var media = await service.PrepareAsync(MediaRequest(material, autoclave));

        Assert.StartsWith("TSAPROD/", media.LotNumber);
        Assert.Equal($"TSAPROD/01/{Yy}", media.LotNumber);
    }

    [Fact]
    public async Task PrepareAsync_AfterProductCodeChanged_StartsNewSequenceUnderNewCode()
    {
        await using var db = NewDb();
        var autoclave = await SeedAutoclaveAsync(db);
        var product = await MediaProductTestData.CreateOrGetAsync(db, "Tryptic Soy Agar", "TSA");

        var material = new Material
        {
            MaterialType = MaterialType.DehydratedMedia,
            MaterialName = product.Name,
            ManufacturerName = "Himedia",
            BatchNumber = "BATCH-01",
            ReceivingDate = DateTime.UtcNow.AddDays(-5),
            ExpiryDate = DateTime.UtcNow.AddYears(1),
            Code = product.Code,
            Location = "Lab",
            QuantityReceived = 500,
            QuantityRemaining = 500,
            Unit = MaterialUnit.Gram,
            MediaProductId = product.Id
        };
        db.Materials.Add(material);

        db.MediaConfigurations.Add(new MediaConfiguration
        {
            MediaProductId = product.Id,
            Name = product.Name,
            EvaluationType = EvaluationType.GrowthPromotion,
            IncubationCondition = MediaProductTestData.Condition(product)
        });

        // Existing lot TSA/03/{Yy}
        db.Media.Add(new Media
        {
            MaterialId = material.Id,
            LotNumber = $"TSA/03/{Yy}",
            ExpiryDate = DateTime.UtcNow.AddMonths(1),
            PreparedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        await SeedCurrentCoaAsync(db, material.Id);

        // Product code changes to TSAX
        product.Code = "TSAX";
        await db.SaveChangesAsync();

        var service = TestServiceFactory.MediaPreparation(db);
        var media = await service.PrepareAsync(MediaRequest(material, autoclave));

        Assert.Equal($"TSAX/01/{Yy}", media.LotNumber);
    }

    [Fact]
    public async Task PrepareAsync_ConfigurationFoundThroughProduct_EvenWhenNamesDiffer()
    {
        await using var db = NewDb();
        var autoclave = await SeedAutoclaveAsync(db);
        var product = await MediaProductTestData.CreateOrGetAsync(db, "Tryptic Soy Agar", "TSA");

        var material = new Material
        {
            MaterialType = MaterialType.DehydratedMedia,
            MaterialName = "Old Material Name",
            ManufacturerName = "Himedia",
            BatchNumber = "BATCH-01",
            ReceivingDate = DateTime.UtcNow.AddDays(-5),
            ExpiryDate = DateTime.UtcNow.AddYears(1),
            Code = product.Code,
            Location = "Lab",
            QuantityReceived = 500,
            QuantityRemaining = 500,
            Unit = MaterialUnit.Gram,
            MediaProductId = product.Id
        };
        db.Materials.Add(material);

        var organism = new Organism { ScientificName = "Bacillus subtilis", AtccNumber = "6633" };
        db.Organisms.Add(organism);
        await db.SaveChangesAsync();

        db.MediaConfigurations.Add(new MediaConfiguration
        {
            MediaProductId = product.Id,
            Name = "Different Display Name On Configuration",
            EvaluationType = EvaluationType.GrowthPromotion,
            IncubationCondition = MediaProductTestData.Condition(product),
            RecoveryPercentMin = 70,
            RecoveryPercentMax = 200,
            Challenges = new List<MediaConfigurationChallenge>
            {
                new() { OrganismId = organism.Id, InitialInoculum = "10^2" }
            }
        });
        await db.SaveChangesAsync();
        await SeedCurrentCoaAsync(db, material.Id);

        var service = TestServiceFactory.MediaPreparation(db);
        var media = await service.PrepareAsync(MediaRequest(material, autoclave));

        Assert.NotNull(media);
        Assert.Equal($"TSA/01/{Yy}", media.LotNumber);

        var eval = await db.MediaEvaluations.Include(e => e.Challenges).SingleOrDefaultAsync(e => e.MediaId == media.Id);
        Assert.NotNull(eval);
        Assert.Equal(EvaluationType.GrowthPromotion, eval.EvaluationType);
        Assert.Single(eval.Challenges);
        Assert.Equal(organism.Id, eval.Challenges[0].OrganismId);
    }
}
