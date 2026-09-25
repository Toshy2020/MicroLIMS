using System.Globalization;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.InventoryTests;

// Prepared media lot numbers and cryovial codes are {Code}/{seq:D2}/{yy},
// one sequence per code per year, continuing from the highest number
// already issued. Two things broke the old count-per-Material rule:
//   - lots numbered by the earlier per-media-type counter left gaps
//     (RVS/01 then RVS/03), so a count of 2 re-issued RVS/03;
//   - the same Code is received again as a new Material batch, so every
//     batch restarted its own count and reached the same numbers.
public class PreparedLotNumberTests
{
    private static readonly string Yy = DateTime.UtcNow.ToString("yy", CultureInfo.InvariantCulture);
    private static readonly string LastYy = DateTime.UtcNow.AddYears(-1).ToString("yy", CultureInfo.InvariantCulture);

    private static MicroLimsDbContext NewDb()
    {
        var db = new MicroLimsDbContext(new DbContextOptionsBuilder<MicroLimsDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        TestServiceFactory.AssignUserToMicroSection(db, 1);
        return db;
    }

    [Fact]
    public async Task PrepareMedia_ContinuesAfterHighestExistingNumber_NotTheLotCount()
    {
        await using var db = NewDb();
        var autoclave = await SeedAutoclave(db);
        var rvs = await SeedDehydratedMedia(db, "Rappaport Vasiliadis Salmonella enrichment broth", "RVS", "0000682699");
        await SeedMediaLot(db, rvs, $"RVS/01/{Yy}");
        await SeedMediaLot(db, rvs, $"RVS/03/{Yy}");

        var media = await TestServiceFactory.MediaPreparation(db).PrepareAsync(MediaRequest(rvs, autoclave));

        Assert.Equal($"RVS/04/{Yy}", media.LotNumber);
    }

    [Fact]
    public async Task PrepareMedia_SameCodeOnDifferentMaterialBatches_SharesOneSequence()
    {
        await using var db = NewDb();
        var autoclave = await SeedAutoclave(db);
        var batchA = await SeedDehydratedMedia(db, "Macconkey broth Purple", "MBP", "BATCH-A");
        var batchB = await SeedDehydratedMedia(db, "Macconkey broth Purple", "MBP", "BATCH-B");
        var service = TestServiceFactory.MediaPreparation(db);

        var lots = new List<string>();
        foreach (var batch in new[] { batchA, batchB, batchA })
            lots.Add((await service.PrepareAsync(MediaRequest(batch, autoclave))).LotNumber);

        Assert.Equal(new[] { $"MBP/01/{Yy}", $"MBP/02/{Yy}", $"MBP/03/{Yy}" }, lots);
    }

    [Fact]
    public async Task PrepareMedia_IgnoresLastYearLegacyPrefixesAndLongerCodes()
    {
        await using var db = NewDb();
        var autoclave = await SeedAutoclave(db);
        var r2a = await SeedDehydratedMedia(db, "R2A agar", "R2A", "6262818");
        var r2ab = await SeedDehydratedMedia(db, "R2A broth", "R2AB", "R2AB-01");
        await SeedMediaLot(db, r2a, $"R2A/09/{LastYy}", preparedAt: DateTime.UtcNow.AddYears(-1));
        await SeedMediaLot(db, r2a, $"R2AAGAR/07/{Yy}"); // name-derived prefix from before the material had a Code
        await SeedMediaLot(db, r2ab, $"R2AB/05/{Yy}");

        var media = await TestServiceFactory.MediaPreparation(db).PrepareAsync(MediaRequest(r2a, autoclave));

        Assert.Equal($"R2A/01/{Yy}", media.LotNumber);
    }

    [Fact]
    public async Task PrepareCryovials_SameCodeOnDifferentMaterialBatches_ContinuesAfterHighest()
    {
        await using var db = NewDb();
        var (panelMedia, incubator) = await SeedReleasedMediaAndIncubator(db);
        var organism = new Organism { ScientificName = "Pseudomonas aeruginosa", AtccNumber = "9027" };
        db.Organisms.Add(organism);
        await db.SaveChangesAsync();
        var batchA = await SeedLyophilized(db, organism, "Ps.", "04001703");
        var batchB = await SeedLyophilized(db, organism, "Ps.", "04001804");
        db.Cryovials.Add(new Cryovial
        {
            Code = $"Ps./02/{Yy}", MaterialId = batchB.Id, OrganismId = organism.Id, OrganismNameSnapshot = organism.ScientificName,
            ExpiryDate = DateTime.UtcNow.AddMonths(6), NumberOfVialsPrepared = 5, VialsRemaining = 5, PreparedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        var service = TestServiceFactory.Cryovial(db);

        var fromA = await service.PrepareCryovialsAsync(CryovialRequest(batchA, panelMedia, incubator));
        var fromB = await service.PrepareCryovialsAsync(CryovialRequest(batchB, panelMedia, incubator));

        Assert.Equal($"Ps./03/{Yy}", fromA.Code);
        Assert.Equal($"Ps./04/{Yy}", fromB.Code);
    }

    private static async Task<Equipment> SeedAutoclave(MicroLimsDbContext db)
    {
        var autoclave = new Equipment { Name = "Autoclave 1", Code = "AUT-01", Type = EquipmentType.Autoclave };
        db.Equipment.Add(autoclave);
        await db.SaveChangesAsync();
        return autoclave;
    }

    // One Material row per received batch; the MediaConfiguration is per product name.
    private static async Task<Material> SeedDehydratedMedia(MicroLimsDbContext db, string name, string code, string batch)
    {
        var microSec = TestServiceFactory.EnsureMicroSection(db);
        var product = await MediaProductTestData.CreateOrGetAsync(db, name, code);
        var material = new Material
        {
            SectionId = microSec.Id,
            MaterialType = MaterialType.DehydratedMedia, MaterialName = name, ManufacturerName = "Himedia",
            BatchNumber = batch, ReceivingDate = DateTime.UtcNow.AddDays(-10), ExpiryDate = DateTime.UtcNow.AddYears(1),
            Code = code, Location = "Micro Lab", QuantityReceived = 500, QuantityRemaining = 500, Unit = MaterialUnit.Gram,
            MediaProductId = product.Id
        };
        db.Materials.Add(material);
        if (!await db.MediaConfigurations.AnyAsync(c => c.MediaProductId == product.Id))
        {
            db.MediaConfigurations.Add(new MediaConfiguration
            {
                MediaProductId = product.Id,
                Name = name, EvaluationType = EvaluationType.GrowthPromotion,
                IncubationCondition = MediaProductTestData.Condition(product),
                RecoveryPercentMin = 50, RecoveryPercentMax = 200
            });
        }
        await db.SaveChangesAsync();
        await SeedCurrentCoa(db, material.Id);
        return material;
    }

    private static async Task SeedMediaLot(MicroLimsDbContext db, Material material, string lotNumber, DateTime? preparedAt = null)
    {
        db.Media.Add(new Media
        {
            MaterialId = material.Id, LotNumber = lotNumber,
            ExpiryDate = DateTime.UtcNow.AddMonths(1), PreparedAt = preparedAt ?? DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    private static async Task SeedCryovialLot(MicroLimsDbContext db, Material material, Organism organism, string code, DateTime? preparedAt = null)
    {
        db.Cryovials.Add(new Cryovial
        {
            Code = code, MaterialId = material.Id, OrganismId = organism.Id, OrganismNameSnapshot = organism.ScientificName,
            ExpiryDate = DateTime.UtcNow.AddMonths(6), NumberOfVialsPrepared = 5, VialsRemaining = 5,
            PreparedAt = preparedAt ?? DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    private static PrepareMediaRequest MediaRequest(Material material, Equipment autoclave) => new(
        MaterialId: material.Id,
        TotalWeight: 10m, TotalVolume: "500 ml", AutoclaveEquipmentId: autoclave.Id, AutoclaveProgram: "Program A",
        LoadType: "agar", Temperature: 121m, CycleTime: 15, CycleNumber: 1,
        Ph: 7.2m, ExpiryDate: DateTime.UtcNow.AddMonths(1), UserId: 1);

    private static async Task<Material> SeedLyophilized(MicroLimsDbContext db, Organism organism, string code, string batch)
    {
        var microSec = TestServiceFactory.EnsureMicroSection(db);
        var material = new Material
        {
            SectionId = microSec.Id,
            MaterialType = MaterialType.LyophilizedMicroorganism, MaterialName = organism.ScientificName, ManufacturerName = "Tody laboratories",
            BatchNumber = batch, ReceivingDate = DateTime.UtcNow.AddDays(-10), ExpiryDate = DateTime.UtcNow.AddYears(1),
            Code = code, AtccNumber = organism.AtccNumber, OrganismId = organism.Id,
            Location = "Micro refrigerator", QuantityReceived = 10, QuantityRemaining = 10, Unit = MaterialUnit.Disc
        };
        db.Materials.Add(material);
        await db.SaveChangesAsync();
        await SeedCurrentCoa(db, material.Id);
        return material;
    }

    // A released Media lot + Incubator for the identity-confirmation panel
    // row that PrepareCryovialsAsync requires.
    private static async Task<(Media media, Equipment incubator)> SeedReleasedMediaAndIncubator(MicroLimsDbContext db)
    {
        var microSec = TestServiceFactory.EnsureMicroSection(db);
        var product = await MediaProductTestData.CreateOrGetAsync(db, "TSA Powder", "TSA");
        var mediaMaterial = new Material
        {
            SectionId = microSec.Id,
            MaterialType = MaterialType.DehydratedMedia, MaterialName = "TSA Powder", ManufacturerName = "Himedia",
            BatchNumber = "LOT-TSA", ReceivingDate = DateTime.UtcNow.AddDays(-30), ExpiryDate = DateTime.UtcNow.AddYears(1),
            Code = "TSA", Location = "Micro Lab", QuantityReceived = 500, QuantityRemaining = 500, Unit = MaterialUnit.Gram,
            MediaProductId = product.Id
        };
        var incubator = new Equipment { Name = "Incubator 1", Code = "INC-01", Type = EquipmentType.Incubator };
        db.Materials.Add(mediaMaterial);
        db.Equipment.Add(incubator);
        await db.SaveChangesAsync();

        var media = new Media
        {
            MaterialId = mediaMaterial.Id, LotNumber = $"TSA/01/{Yy}",
            IsReleasedForUse = true, Status = MediaStatus.Active, ExpiryDate = DateTime.UtcNow.AddYears(1)
        };
        db.Media.Add(media);
        await db.SaveChangesAsync();
        return (media, incubator);
    }

    private static PrepareCryovialsRequest CryovialRequest(Material material, Media panelMedia, Equipment incubator) => new(
        material.Id, NumberOfVialsPrepared: 5, ExpiryDate: DateTime.UtcNow.AddMonths(6),
        StorageCondition: "Freezer -15 to -25", PhysicalCheckConfirmed: true, PhysicalCheckText: "Conforms to reference description",
        Panel: new List<IdentityConfirmationRow>
        {
            new(panelMedia.Id, incubator.Id, DateTime.UtcNow.AddDays(-2), DateTime.UtcNow.AddDays(-1), "Typical colonies")
        },
        DiscsUsed: 1, UserId: 1);

    private static async Task SeedCurrentCoa(MicroLimsDbContext db, int materialId)
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
            ContentSha256 = "AABBCC",
            UploadedByUserId = 1,
            UploadedAt = DateTime.UtcNow,
            Status = MaterialDocumentStatus.Current
        });
        await db.SaveChangesAsync();
    }
}
