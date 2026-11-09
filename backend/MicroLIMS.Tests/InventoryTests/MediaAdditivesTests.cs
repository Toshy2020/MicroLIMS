using MicroLIMS.Shared.Exceptions;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.InventoryTests;

// Optional Additives step of Media Preparation (plan 3.2, D3/D9/D11).
public class MediaAdditivesTests
{
    private sealed class Fixture
    {
        public string DbName = null!;
        public MicroLimsDbContext Db = null!;
        public Equipment Autoclave = null!;
        public Material Media = null!;
        public DocumentSection Section = null!;
        public MediaPreparationService Service = null!;
    }

    private static async Task<Fixture> SetUpAsync()
    {
        var dbName = Guid.NewGuid().ToString();
        var db = new MicroLimsDbContext(new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(dbName).Options) { CurrentUserId = 1 };
        TestServiceFactory.AssignUserToMicroSection(db, 1);
        var section = TestServiceFactory.EnsureMicroSection(db);

        var autoclave = new Equipment { Name = "Autoclave 1", Code = "AUT-01", Type = EquipmentType.Autoclave };
        db.Equipment.Add(autoclave);
        var product = await MediaProductTestData.CreateOrGetAsync(db, "Tryptic Soy Agar", "TSA");
        var media = NewMaterial(section.Id, MaterialType.DehydratedMedia, "TSA powder", "BATCH-M", MaterialUnit.Gram, 500);
        media.MediaProductId = product.Id;
        db.Materials.Add(media);
        db.MediaConfigurations.Add(new MediaConfiguration
        {
            MediaProductId = product.Id, Name = product.Name, EvaluationType = EvaluationType.GrowthPromotion,
            IncubationCondition = MediaProductTestData.Condition(product)
        });
        await db.SaveChangesAsync();
        AddCoa(db, media.Id);
        await db.SaveChangesAsync();

        return new Fixture { DbName = dbName, Db = db, Autoclave = autoclave, Media = media, Section = section, Service = TestServiceFactory.MediaPreparation(db) };
    }

    private static Material NewMaterial(int sectionId, MaterialType type, string name, string batch, MaterialUnit unit, decimal qty) => new()
    {
        SectionId = sectionId, MaterialType = type, MaterialName = name, ManufacturerName = "Acme", BatchNumber = batch,
        ReceivingDate = DateTime.UtcNow.AddDays(-5), ExpiryDate = DateTime.UtcNow.AddYears(1), Code = batch, StockId = "STK-" + batch,
        Location = "Lab", QuantityReceived = qty, QuantityRemaining = qty, Unit = unit
    };

    private static void AddCoa(MicroLimsDbContext db, int materialId) => db.MaterialDocuments.Add(new MaterialDocument
    {
        MaterialId = materialId, DocumentType = MaterialDocumentType.COA, OriginalFileName = "COA.pdf",
        StorageKey = $"material-documents/{materialId}/t.pdf", FileExtension = ".pdf", ContentType = "application/pdf",
        FileSizeBytes = 1, ContentSha256 = "H", UploadedByUserId = 1, UploadedAt = DateTime.UtcNow, Status = MaterialDocumentStatus.Current
    });

    private static async Task<Material> AddAdditiveLotAsync(Fixture f, int? sectionId = null, MaterialType type = MaterialType.Chemical,
        MaterialUnit unit = MaterialUnit.Milliliter, decimal qty = 100)
    {
        var lot = NewMaterial(sectionId ?? f.Section.Id, type, "Polysorbate 80", "PS80-" + Guid.NewGuid().ToString("N")[..6], unit, qty);
        f.Db.Materials.Add(lot);
        await f.Db.SaveChangesAsync();
        return lot;
    }

    private static async Task<SolutionPreparation> AddPreparationAsync(Fixture f, int? sectionId = null,
        SolutionPreparationStatus status = SolutionPreparationStatus.Prepared, DateTime? expiresAt = null, ApprovalGateStatus? approval = null)
    {
        var master = new SolutionMaster { SectionId = sectionId ?? f.Section.Id, Name = "Letheen neutralizer", Type = SolutionType.Neutralizer };
        f.Db.SolutionMasters.Add(master);
        await f.Db.SaveChangesAsync();
        var prep = new SolutionPreparation
        {
            SectionId = master.SectionId, SolutionMasterId = master.Id, Type = SolutionType.Neutralizer, Code = "NT-0001/26",
            Status = status, PreparedAt = DateTime.UtcNow.AddHours(-1), ExpiresAt = expiresAt ?? DateTime.UtcNow.AddDays(5), ApprovalStatus = approval
        };
        f.Db.SolutionPreparations.Add(prep);
        await f.Db.SaveChangesAsync();
        return prep;
    }

    private static PrepareMediaRequest Request(Fixture f, params MediaAdditiveInput[] additives) => new(
        f.Media.Id, 50m, "500 ml", f.Autoclave.Id, "Program A", "agar", 121m, 15, 1, 7.2m, DateTime.UtcNow.AddMonths(1), 1,
        additives.Length == 0 ? null : additives.ToList());

    private static MediaAdditiveInput Stock(Material lot, decimal qty = 5m, string? unit = "mL") =>
        new(MediaAdditiveKind.StockLot, lot.Id, null, qty, unit);

    private static MediaAdditiveInput Solution(SolutionPreparation prep, decimal qty = 10m, string? unit = "mL") =>
        new(MediaAdditiveKind.PreparedSolution, null, prep.Id, qty, unit);

    [Fact]
    public async Task NoAdditives_PreparesAsBefore()
    {
        var f = await SetUpAsync();
        var result = await f.Service.PrepareAsync(Request(f));
        Assert.Empty(result.Additives);
        Assert.Empty(await f.Db.MediaAdditives.ToListAsync());

        var withEmptyList = await f.Service.PrepareAsync(Request(f) with { Additives = new() });
        Assert.Empty(withEmptyList.Additives);
    }

    [Fact]
    public async Task StockLotAdditive_DeductsStock_AndIsRecordedInLotsOwnUnit()
    {
        var f = await SetUpAsync();
        var lot = await AddAdditiveLotAsync(f, qty: 100);

        var result = await f.Service.PrepareAsync(Request(f, Stock(lot, 5m, "ignored")));

        var a = Assert.Single(result.Additives);
        Assert.Equal(MediaAdditiveKind.StockLot, a.Kind);
        Assert.Equal("Polysorbate 80", a.Name);
        Assert.Equal(lot.StockId, a.Code);
        Assert.Equal(5m, a.Quantity);
        Assert.Equal("Milliliter", a.Unit);
        Assert.Equal(95m, (await f.Db.Materials.FindAsync(lot.Id))!.QuantityRemaining);
        Assert.Equal(450m, (await f.Db.Materials.FindAsync(f.Media.Id))!.QuantityRemaining);
    }

    [Fact]
    public async Task PreparedSolutionAdditive_IsRecorded_AndNotDeducted()
    {
        var f = await SetUpAsync();
        var prep = await AddPreparationAsync(f, approval: ApprovalGateStatus.Approved);

        var result = await f.Service.PrepareAsync(Request(f, Solution(prep, 12.5m, "mL")));

        var a = Assert.Single(result.Additives);
        Assert.Equal(MediaAdditiveKind.PreparedSolution, a.Kind);
        Assert.Equal("Letheen neutralizer", a.Name);
        Assert.Equal("NT-0001/26", a.Code);
        Assert.Equal(12.5m, a.Quantity);
        Assert.Equal("mL", a.Unit);
        var stored = await f.Db.MediaAdditives.SingleAsync();
        Assert.Equal(prep.Id, stored.SolutionPreparationId);
    }

    [Fact]
    public async Task PreparedSolution_NoReleaseNeeded_IsAccepted()
    {
        var f = await SetUpAsync();
        var prep = await AddPreparationAsync(f, approval: null);
        var result = await f.Service.PrepareAsync(Request(f, Solution(prep)));
        Assert.Single(result.Additives);
    }

    [Fact]
    public async Task PendingReleaseRejectedExpiredOrOtherSectionPreparation_IsRefused_AndNothingIsSaved()
    {
        var f = await SetUpAsync();
        var lot = await AddAdditiveLotAsync(f, qty: 100);
        var otherSection = new DocumentSection { Name = "Other", Code = "OTH", DepartmentId = f.Section.DepartmentId, IsActive = true };
        f.Db.DocumentSections.Add(otherSection);
        await f.Db.SaveChangesAsync();

        var pending = await AddPreparationAsync(f, approval: ApprovalGateStatus.PendingReview);
        var rejected = await AddPreparationAsync(f, approval: ApprovalGateStatus.Rejected);
        var expired = await AddPreparationAsync(f, expiresAt: DateTime.UtcNow.AddMinutes(-5));
        var inProgress = await AddPreparationAsync(f, status: SolutionPreparationStatus.InProgress);
        var foreign = await AddPreparationAsync(f, sectionId: otherSection.Id);

        foreach (var (prep, text) in new[]
        {
            (pending, "awaiting release"), (rejected, "rejected"), (expired, "expired"),
            (inProgress, "InProgress"), (foreign, "another laboratory section")
        })
        {
            // A stock lot ahead of the bad solution proves the whole preparation rolls back.
            var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                f.Service.PrepareAsync(Request(f, Stock(lot), Solution(prep))));
            Assert.Contains(text, ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        await using var check = new MicroLimsDbContext(new DbContextOptionsBuilder<MicroLimsDbContext>().UseInMemoryDatabase(f.DbName).Options);
        Assert.Empty(await check.Media.ToListAsync());
        Assert.Empty(await check.MediaAdditives.ToListAsync());
        Assert.Equal(100m, (await check.Materials.FindAsync(lot.Id))!.QuantityRemaining);
        Assert.Equal(500m, (await check.Materials.FindAsync(f.Media.Id))!.QuantityRemaining);
    }

    [Fact]
    public async Task StockLot_WrongSectionOrMediaTypeOrExpiredOrInsufficient_IsRefused()
    {
        var f = await SetUpAsync();
        var otherSection = new DocumentSection { Name = "Other", Code = "OTH", DepartmentId = f.Section.DepartmentId, IsActive = true };
        f.Db.DocumentSections.Add(otherSection);
        await f.Db.SaveChangesAsync();

        var foreign = await AddAdditiveLotAsync(f, sectionId: otherSection.Id);
        var dehydrated = await AddAdditiveLotAsync(f, type: MaterialType.DehydratedMedia);
        var expired = await AddAdditiveLotAsync(f);
        expired.ExpiryDate = DateTime.UtcNow.AddDays(-1);
        var small = await AddAdditiveLotAsync(f, qty: 2);
        await f.Db.SaveChangesAsync();

        Assert.Contains("another laboratory section", (await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.PrepareAsync(Request(f, Stock(foreign))))).Message);
        Assert.Contains("cannot be added as an additive", (await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.PrepareAsync(Request(f, Stock(dehydrated))))).Message);
        Assert.Contains("expired", (await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.PrepareAsync(Request(f, Stock(expired))))).Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Insufficient stock", (await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.PrepareAsync(Request(f, Stock(small, 5m))))).Message);

        await using var check = new MicroLimsDbContext(new DbContextOptionsBuilder<MicroLimsDbContext>().UseInMemoryDatabase(f.DbName).Options);
        Assert.Empty(await check.Media.ToListAsync());
        Assert.Equal(500m, (await check.Materials.FindAsync(f.Media.Id))!.QuantityRemaining);
    }

    [Fact]
    public async Task QuantityUnitAndDuplicateValidation()
    {
        var f = await SetUpAsync();
        var lot = await AddAdditiveLotAsync(f);
        var prep = await AddPreparationAsync(f);

        Assert.Contains("quantity", (await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.PrepareAsync(Request(f, Stock(lot, 0m))))).Message);
        Assert.Contains("quantity", (await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.PrepareAsync(Request(f, Solution(prep, -1m))))).Message);
        Assert.Contains("unit is required", (await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.PrepareAsync(Request(f, Stock(lot, 1m, " "))))).Message);
        Assert.Contains("unit is required", (await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.PrepareAsync(Request(f, Solution(prep, 1m, null))))).Message);
        Assert.Contains("20 characters", (await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.PrepareAsync(Request(f, Solution(prep, 1m, new string('x', 21)))))).Message);
        Assert.Contains("more than once", (await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.PrepareAsync(Request(f, Stock(lot), Stock(lot))))).Message);
        Assert.Contains("more than once", (await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.PrepareAsync(Request(f, Solution(prep), Solution(prep))))).Message);
        Assert.Contains("choose the stock lot", (await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.PrepareAsync(Request(f, new MediaAdditiveInput(MediaAdditiveKind.StockLot, null, null, 1m, "mL"))))).Message);

        Assert.Empty(await f.Db.Media.ToListAsync());
    }

    [Fact]
    public async Task MixedAdditives_KeepOrder_AndAppearInTheMediaSummary()
    {
        var f = await SetUpAsync();
        var lot = await AddAdditiveLotAsync(f, qty: 100);
        var prep = await AddPreparationAsync(f);

        var result = await f.Service.PrepareAsync(Request(f, Solution(prep, 10m), Stock(lot, 3m)));
        Assert.Equal(new[] { MediaAdditiveKind.PreparedSolution, MediaAdditiveKind.StockLot }, result.Additives.Select(a => a.Kind));

        var summary = await TestServiceFactory.MediaSummary(f.Db).GetSummaryAsync(result.Id);
        Assert.NotNull(summary);
        Assert.Equal(2, summary!.Additives.Count);
        Assert.Equal("PreparedSolution", summary.Additives[0].Kind);
        Assert.Equal("Polysorbate 80", summary.Additives[1].Name);
    }
}
