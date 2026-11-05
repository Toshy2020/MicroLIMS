using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Tests.WorkflowTests;

// Masters and lots for the Test Preparation diluent / neutralizer tests (all in the Microbiology section).
public static class PreparationTestData
{
    public static readonly PreparationMaterialChoice ContainedInMedium = new(PreparationMaterialSource.ContainedInMedium);

    public static PreparationMaterialChoice MediaChoice(MediaProduct product) => new(PreparationMaterialSource.MediaProduct, MediaProductId: product.Id);
    public static PreparationMaterialChoice SolutionChoice(SolutionMaster master) => new(PreparationMaterialSource.SolutionRecipe, SolutionMasterId: master.Id);
    public static PreparationMaterialChoice EntryChoice(MaterialMasterEntry entry) => new(PreparationMaterialSource.MaterialEntry, MaterialMasterEntryId: entry.Id);

    public static async Task<MediaProduct> ProductAsync(MicroLimsDbContext db, string name = "TSB", string code = "TSB") =>
        await MediaProductTestData.CreateOrGetAsync(db, name, code);

    // A prepared, released, active, unexpired media lot of the product (override to make it unfit).
    public static async Task<Media> MediaLotAsync(
        MicroLimsDbContext db, MediaProduct product, string lotNumber = "TSB/01/26", bool released = true,
        MediaStatus status = MediaStatus.Active, DateTime? expiry = null, int? sectionId = null)
    {
        var section = sectionId ?? TestServiceFactory.EnsureMicroSection(db).Id;
        var material = new Material
        {
            SectionId = section, MaterialType = MaterialType.DehydratedMedia, MaterialName = product.Name, ManufacturerName = "Acme",
            BatchNumber = "B-" + lotNumber, ReceivingDate = DateTime.UtcNow.AddDays(-10), ExpiryDate = DateTime.UtcNow.AddYears(1),
            Code = product.Code, Location = "Lab", QuantityReceived = 100, QuantityRemaining = 50, Unit = MaterialUnit.Gram,
            MediaProductId = product.Id
        };
        db.Materials.Add(material);
        await db.SaveChangesAsync();
        var media = new Media
        {
            MaterialId = material.Id, LotNumber = lotNumber, ExpiryDate = expiry ?? DateTime.UtcNow.AddDays(30),
            Status = status, IsReleasedForUse = released, PreparedAt = DateTime.UtcNow.AddDays(-2)
        };
        db.Media.Add(media);
        await db.SaveChangesAsync();
        return media;
    }

    public static async Task<SolutionMaster> RecipeAsync(
        MicroLimsDbContext db, string name = "Phosphate buffer", SolutionType type = SolutionType.Diluent, bool active = true, int? sectionId = null)
    {
        var master = new SolutionMaster { SectionId = sectionId ?? TestServiceFactory.EnsureMicroSection(db).Id, Name = name, Type = type, IsActive = active };
        db.SolutionMasters.Add(master);
        await db.SaveChangesAsync();
        return master;
    }

    public static async Task<SolutionPreparation> SolutionLotAsync(
        MicroLimsDbContext db, SolutionMaster master, string code = "DL-01/11/2026",
        SolutionPreparationStatus status = SolutionPreparationStatus.Prepared, ApprovalGateStatus? approval = null,
        DateTime? expiresAt = null, int? sectionId = null)
    {
        var prep = new SolutionPreparation
        {
            SectionId = sectionId ?? master.SectionId, SolutionMasterId = master.Id, Type = master.Type, Code = code, Status = status,
            PreparedAt = DateTime.UtcNow.AddHours(-1), ExpiresAt = expiresAt ?? DateTime.UtcNow.AddDays(10), ApprovalStatus = approval
        };
        db.SolutionPreparations.Add(prep);
        await db.SaveChangesAsync();
        return prep;
    }

    public static async Task<MaterialMasterEntry> EntryAsync(MicroLimsDbContext db, string name = "Peptone water", string code = "PW-1", bool active = true)
    {
        var entry = new MaterialMasterEntry
        {
            SectionId = TestServiceFactory.EnsureMicroSection(db).Id, Code = code, Name = name, Category = MaterialMasterCategory.Reagent,
            BaseUnit = MaterialUnit.Gram, IsActive = active
        };
        db.MaterialMasterEntries.Add(entry);
        await db.SaveChangesAsync();
        return entry;
    }

    public static async Task<Material> StockLotAsync(
        MicroLimsDbContext db, MaterialMasterEntry entry, string stockId = "STK-2026-1", decimal remaining = 10,
        DateTime? expiry = null, ApprovalGateStatus? approval = null, int? sectionId = null)
    {
        var lot = new Material
        {
            SectionId = sectionId ?? entry.SectionId, MaterialType = MaterialType.Chemical, MaterialName = entry.Name, ManufacturerName = "Acme",
            BatchNumber = "B-" + stockId, ReceivingDate = DateTime.UtcNow.AddDays(-10), ExpiryDate = expiry ?? DateTime.UtcNow.AddYears(1),
            Code = entry.Code, StockId = stockId, Location = "Lab", QuantityReceived = 10, QuantityRemaining = remaining, Unit = MaterialUnit.Gram,
            MaterialMasterEntryId = entry.Id, ApprovalStatus = approval
        };
        db.Materials.Add(lot);
        await db.SaveChangesAsync();
        return lot;
    }

    // A manual first-entry request: media diluent with its released lot, neutralizer contained in the medium.
    // Also puts the user in the Microbiology section so the lot is readable.
    public static async Task<PrepareSampleRequest> ManualRequestAsync(
        MicroLimsDbContext db, int sampleId, decimal amount, int userId, string password)
    {
        TestServiceFactory.AssignUserToMicroSection(db, userId);
        var product = await ProductAsync(db);
        var lot = await db.Media.FirstOrDefaultAsync(m => m.Material!.MediaProductId == product.Id) ?? await MediaLotAsync(db, product);
        return new PrepareSampleRequest(
            sampleId, amount, "PourPlate", null, null, string.Empty, string.Empty, userId, password,
            MediaChoice(product), ContainedInMedium, DiluentLotId: lot.Id);
    }

    // Parameters for ItemPreparationConfigurationService.UpsertAsync with a media diluent and a contained neutralizer.
    public static async Task<PreparationParameters> UpsertParametersAsync(MicroLimsDbContext db, decimal amount)
    {
        var product = await ProductAsync(db);
        return new PreparationParameters(amount, "PourPlate", null, null, string.Empty, string.Empty, MediaChoice(product), ContainedInMedium);
    }
}
