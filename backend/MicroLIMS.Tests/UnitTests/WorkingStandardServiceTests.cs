using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs.Responses;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

// Working standard qualification Task 4: create/edit, documents, lists.
public class WorkingStandardServiceTests
{
    private const string Password = "ValidPassword123!";
    private static readonly DateTimeOffset SepFirst = new(2026, 9, 1, 10, 0, 0, TimeSpan.Zero);

    private class FakeTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;
        public FakeTimeProvider(DateTimeOffset utcNow) => _utcNow = utcNow;
        public override DateTimeOffset GetUtcNow() => _utcNow;
    }

    private static MicroLimsDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MicroLimsDbContext(options) { CurrentUserId = 1 };
    }

    private static ILabClock NewClock(DateTimeOffset utcNow) =>
        new LabClock(new FakeTimeProvider(utcNow), LabClock.ResolveTimeZone("Africa/Cairo"));

    private static async Task<(DocumentSection section, int userId)> SeedAsync(MicroLimsDbContext db)
    {
        var section = TestServiceFactory.EnsureMicroSection(db);
        var role = new Role { Name = "Analyst", Type = RoleType.Analyst, IsActive = true };
        db.Roles.Add(role);
        await db.SaveChangesAsync();
        var user = new User
        {
            Username = "u_" + Guid.NewGuid().ToString("N")[..6],
            FullName = "Test Analyst",
            RoleId = role.Id,
            IsActive = true,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password),
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        TestServiceFactory.AssignUserToMicroSection(db, user.Id);
        return (section, user.Id);
    }

    private static async Task<MaterialMasterEntry> AddEntryAsync(
        MicroLimsDbContext db, int sectionId, string code,
        MaterialMasterCategory category = MaterialMasterCategory.Reagent)
    {
        var entry = new MaterialMasterEntry
        {
            SectionId = sectionId, Code = code, Name = code, Category = category,
            BaseUnit = MaterialUnit.Milliliter, IsActive = true,
            CreatedByUserId = 1, CreatedAt = DateTime.UtcNow, LastModifiedByUserId = 1, LastModifiedAt = DateTime.UtcNow,
        };
        db.MaterialMasterEntries.Add(entry);
        await db.SaveChangesAsync();
        return entry;
    }

    private static async Task<Material> AddLotAsync(
        MicroLimsDbContext db, int sectionId, MaterialMasterEntry entry, string code,
        DateTime? expiry = null, decimal qty = 10m)
    {
        var lot = new Material
        {
            SectionId = sectionId, MaterialType = MaterialType.WorkingStandard, MaterialMasterEntryId = entry.Id,
            MaterialName = "Paracetamol WS", ManufacturerName = "In-house", BatchNumber = "WS-B1", Code = code,
            ReceivingDate = DateTime.UtcNow.AddDays(-10), ExpiryDate = expiry, Location = "Fridge",
            QuantityReceived = 10m, QuantityRemaining = qty, Unit = MaterialUnit.Gram,
            Purity = 99.1m, MoisturePercent = 0.4m, CreatedByUserId = 1, LastModifiedByUserId = 1,
        };
        db.Materials.Add(lot);
        await db.SaveChangesAsync();
        return lot;
    }

    private static CreateQualificationRequest Manual(int entryId, string name = "Paracetamol API", string batch = "RM-77") =>
        new(WorkingStandardQualificationKind.Initial, null, entryId, null, name, batch, 10m, "Fridge 2", 0.4m);

    private static async Task<(Sample sample, TestOrder order)> AddRmSampleAsync(
        MicroLimsDbContext db, DocumentSection section, WorkflowStep step, string batch = "RM-9")
    {
        var item = new Item { Name = "Paracetamol", Code = "RM-PARA", Category = SampleCategory.RawMaterial };
        db.Items.Add(item);
        db.TestDefinitions.Add(new TestDefinition
        {
            Code = "ASSAY-" + Guid.NewGuid().ToString("N")[..4], DisplayName = "Assay", SectionId = section.Id,
            EquationType = EquationType.StandardComparison,
        });
        await db.SaveChangesAsync();
        var def = await db.TestDefinitions.OrderByDescending(d => d.Id).FirstAsync();
        var sample = new Sample
        {
            ReferenceNumber = "RM" + Guid.NewGuid().ToString("N")[..6], Category = SampleCategory.RawMaterial,
            ItemId = item.Id, BatchNumber = batch, Status = SampleStatus.InTesting,
        };
        db.Samples.Add(sample);
        await db.SaveChangesAsync();
        var order = new TestOrder
        {
            SampleId = sample.Id, TestCode = def.Code, SectionId = section.Id, CurrentStep = step,
        };
        db.TestOrders.Add(order);
        await db.SaveChangesAsync();
        return (sample, order);
    }

    [Fact]
    public async Task CreateInitial_ManualSource_GetsWsqCodeAndDraft()
    {
        await using var db = NewDb();
        var (section, uid) = await SeedAsync(db);
        var entry = await AddEntryAsync(db, section.Id, "STD-01", MaterialMasterCategory.ReferenceStandard);
        var svc = TestServiceFactory.WorkingStandard(db, clock: NewClock(SepFirst));

        var dto = await svc.CreateAsync(Manual(entry.Id), uid);

        Assert.Equal("WSQ-01/09/2026", dto.Code);
        Assert.Equal(WorkingStandardQualificationStatus.Draft, dto.Status);
        Assert.Equal("Paracetamol API", dto.SourceMaterialName);
        Assert.Equal("RM-77", dto.SourceBatchNumber);
    }

    [Fact]
    public async Task CreateInitial_BothSources_Throws()
    {
        await using var db = NewDb();
        var (section, uid) = await SeedAsync(db);
        var entry = await AddEntryAsync(db, section.Id, "STD-01", MaterialMasterCategory.ReferenceStandard);
        var svc = TestServiceFactory.WorkingStandard(db, clock: NewClock(SepFirst));
        var req = Manual(entry.Id) with { SourceSampleId = 5 };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => svc.CreateAsync(req, uid));
        Assert.Equal("Choose a received raw material sample or enter the material name and batch, not both.", ex.Message);
    }

    [Fact]
    public async Task CreateInitial_EntryNotReferenceStandard_Throws()
    {
        await using var db = NewDb();
        var (section, uid) = await SeedAsync(db);
        var entry = await AddEntryAsync(db, section.Id, "RGT-01");
        var svc = TestServiceFactory.WorkingStandard(db, clock: NewClock(SepFirst));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => svc.CreateAsync(Manual(entry.Id), uid));
        Assert.Equal("Master entry \"RGT-01\" is not a reference standard.", ex.Message);
    }

    [Fact]
    public async Task CreateInitial_SampleWithoutApprovedAssay_Throws()
    {
        await using var db = NewDb();
        var (section, uid) = await SeedAsync(db);
        var entry = await AddEntryAsync(db, section.Id, "STD-01", MaterialMasterCategory.ReferenceStandard);
        var (sample, _) = await AddRmSampleAsync(db, section, WorkflowStep.Waiting);
        var svc = TestServiceFactory.WorkingStandard(db, clock: NewClock(SepFirst));
        var req = new CreateQualificationRequest(WorkingStandardQualificationKind.Initial, null, entry.Id, sample.Id, null, null, 10m, "Fridge 2", null);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => svc.CreateAsync(req, uid));
        Assert.Equal("That sample has no approved assay result.", ex.Message);
    }

    [Fact]
    public async Task CreateInitial_SampleWithApprovedAssay_CopiesNameAndBatch()
    {
        await using var db = NewDb();
        var (section, uid) = await SeedAsync(db);
        var entry = await AddEntryAsync(db, section.Id, "STD-01", MaterialMasterCategory.ReferenceStandard);
        var (sample, _) = await AddRmSampleAsync(db, section, WorkflowStep.Approved, "RM-9");
        var svc = TestServiceFactory.WorkingStandard(db, clock: NewClock(SepFirst));
        var req = new CreateQualificationRequest(WorkingStandardQualificationKind.Initial, null, entry.Id, sample.Id, null, null, 10m, "Fridge 2", null);

        var dto = await svc.CreateAsync(req, uid);

        Assert.Equal("Paracetamol", dto.SourceMaterialName);
        Assert.Equal("RM-9", dto.SourceBatchNumber);
        Assert.Equal(sample.Id, dto.SourceSampleId);
        Assert.Single(await svc.GetEligibleSourceSamplesAsync("para", uid));
    }

    [Fact]
    public async Task CreateRequalification_LotWithOpenQualification_Throws()
    {
        await using var db = NewDb();
        var (section, uid) = await SeedAsync(db);
        var entry = await AddEntryAsync(db, section.Id, "STD-01", MaterialMasterCategory.ReferenceStandard);
        var lot = await AddLotAsync(db, section.Id, entry, "WS-01/10/2026", DateTime.UtcNow.AddYears(1));
        var svc = TestServiceFactory.WorkingStandard(db, clock: NewClock(SepFirst));
        var req = new CreateQualificationRequest(WorkingStandardQualificationKind.Requalification, lot.Id, null, null, null, null, null, null, 0.4m);

        var first = await svc.CreateAsync(req, uid);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => svc.CreateAsync(req, uid));
        Assert.Equal($"Lot WS-01/10/2026 already has an open qualification ({first.Code}).", ex.Message);
    }

    [Fact]
    public async Task CreateRequalification_UsesLotEntryNameAndBatch()
    {
        await using var db = NewDb();
        var (section, uid) = await SeedAsync(db);
        var entry = await AddEntryAsync(db, section.Id, "STD-01", MaterialMasterCategory.ReferenceStandard);
        var lot = await AddLotAsync(db, section.Id, entry, "WS-01/10/2026", DateTime.UtcNow.AddYears(1));
        var svc = TestServiceFactory.WorkingStandard(db, clock: NewClock(SepFirst));
        var req = new CreateQualificationRequest(WorkingStandardQualificationKind.Requalification, lot.Id, null, null, null, null, null, null, null);

        var dto = await svc.CreateAsync(req, uid);

        Assert.Equal(entry.Id, dto.MaterialMasterEntryId);
        Assert.Equal("Paracetamol WS", dto.SourceMaterialName);
        Assert.Equal("WS-B1", dto.SourceBatchNumber);
        Assert.Null(dto.QuantityGrams);
        Assert.Equal("WS-01/10/2026", dto.WorkingStandardCode);
    }

    [Fact]
    public async Task SecondQualificationSameMonth_Gets02()
    {
        await using var db = NewDb();
        var (section, uid) = await SeedAsync(db);
        var entry = await AddEntryAsync(db, section.Id, "STD-01", MaterialMasterCategory.ReferenceStandard);
        var svc = TestServiceFactory.WorkingStandard(db, clock: NewClock(SepFirst));

        await svc.CreateAsync(Manual(entry.Id), uid);
        var second = await svc.CreateAsync(Manual(entry.Id, batch: "RM-78"), uid);

        Assert.Equal("WSQ-02/09/2026", second.Code);
    }

    [Fact]
    public async Task UploadDocument_SameKindTwice_SupersedesFirst()
    {
        await using var db = NewDb();
        var (section, uid) = await SeedAsync(db);
        var entry = await AddEntryAsync(db, section.Id, "STD-01", MaterialMasterCategory.ReferenceStandard);
        var svc = TestServiceFactory.WorkingStandard(db, clock: NewClock(SepFirst));
        var q = await svc.CreateAsync(Manual(entry.Id), uid);

        var first = await svc.UploadDocumentAsync(q.Id, WorkingStandardDocumentKind.SourceReport, "a.pdf", "application/pdf", new byte[] { 1 }, uid);
        var second = await svc.UploadDocumentAsync(q.Id, WorkingStandardDocumentKind.SourceReport, "b.pdf", "application/pdf", new byte[] { 2 }, uid);

        var docs = (await svc.GetAsync(q.Id, uid)).Documents;
        Assert.False(docs.Single(d => d.Id == first.Id).IsCurrent);
        Assert.True(docs.Single(d => d.Id == second.Id).IsCurrent);
        var file = await svc.DownloadDocumentAsync(second.Id, uid);
        Assert.Equal(new byte[] { 2 }, file.Content);
    }

    [Fact]
    public async Task AssignProblem_ManualWithoutReport_ReportsMissingReport()
    {
        await using var db = NewDb();
        var (section, uid) = await SeedAsync(db);
        var entry = await AddEntryAsync(db, section.Id, "STD-01", MaterialMasterCategory.ReferenceStandard);
        var svc = TestServiceFactory.WorkingStandard(db, clock: NewClock(SepFirst));
        var dto = await svc.CreateAsync(Manual(entry.Id), uid);
        var q = await db.WorkingStandardQualifications.Include(x => x.Documents).FirstAsync(x => x.Id == dto.Id);

        Assert.Equal("Attach the raw material's first test report first.", WorkingStandardService.AssignProblem(q));

        q.MoisturePercent = null;
        Assert.Equal("Enter the moisture content first.", WorkingStandardService.AssignProblem(q));
        q.Status = WorkingStandardQualificationStatus.Assayed;
        Assert.Equal("Only a draft qualification can be assigned.", WorkingStandardService.AssignProblem(q));
    }

    [Fact]
    public async Task GetLots_StatusDueSoonAndExpired()
    {
        await using var db = NewDb();
        var (section, uid) = await SeedAsync(db);
        var entry = await AddEntryAsync(db, section.Id, "STD-01", MaterialMasterCategory.ReferenceStandard);
        var clock = NewClock(SepFirst);
        var today = clock.LabToday.ToDateTime(TimeOnly.MinValue);
        await AddLotAsync(db, section.Id, entry, "L-DUE", today.AddDays(10));
        await AddLotAsync(db, section.Id, entry, "L-EXP", today.AddDays(-1));
        await AddLotAsync(db, section.Id, entry, "L-OK", today.AddDays(200));
        await AddLotAsync(db, section.Id, entry, "L-ZERO", today.AddDays(200), qty: 0m);
        var svc = TestServiceFactory.WorkingStandard(db, clock: clock);

        var lots = (await svc.GetLotsAsync(uid)).ToDictionary(l => l.Code, l => l.Status);

        Assert.Equal("DueSoon", lots["L-DUE"]);
        Assert.Equal("Expired", lots["L-EXP"]);
        Assert.Equal("Valid", lots["L-OK"]);
        Assert.Equal("Depleted", lots["L-ZERO"]);
    }

    private class ThrowingStorage : MicroLIMS.Application.Abstractions.Storage.IFileStorageService
    {
        public Task<string> SaveAsync(string fileName, byte[] content) => throw new IOException("disk full");
        public Task<byte[]> ReadAsync(string path) => throw new IOException("disk full");
    }

    [Fact]
    public async Task AssignProblem_RequalificationWithMoistureAndNoDocuments_ReturnsNull()
    {
        await using var db = NewDb();
        var (section, uid) = await SeedAsync(db);
        var entry = await AddEntryAsync(db, section.Id, "STD-01", MaterialMasterCategory.ReferenceStandard);
        var lot = await AddLotAsync(db, section.Id, entry, "WS-01/10/2026", DateTime.UtcNow.AddYears(1));
        var svc = TestServiceFactory.WorkingStandard(db, clock: NewClock(SepFirst));
        var dto = await svc.CreateAsync(new CreateQualificationRequest(WorkingStandardQualificationKind.Requalification, lot.Id, null, null, null, null, null, null, 0.4m), uid);
        var q = await db.WorkingStandardQualifications.Include(x => x.Documents).FirstAsync(x => x.Id == dto.Id);

        Assert.Null(WorkingStandardService.AssignProblem(q));
    }

    [Fact]
    public async Task UploadDocument_StorageFails_LeavesPreviousCurrent()
    {
        await using var db = NewDb();
        var (section, uid) = await SeedAsync(db);
        var entry = await AddEntryAsync(db, section.Id, "STD-01", MaterialMasterCategory.ReferenceStandard);
        var clock = NewClock(SepFirst);
        var q = await TestServiceFactory.WorkingStandard(db, clock: clock).CreateAsync(Manual(entry.Id), uid);
        var good = await TestServiceFactory.WorkingStandard(db, clock: clock)
            .UploadDocumentAsync(q.Id, WorkingStandardDocumentKind.SourceReport, "a.pdf", "application/pdf", new byte[] { 1 }, uid);
        var bad = TestServiceFactory.WorkingStandard(db, storage: new ThrowingStorage(), clock: clock);

        await Assert.ThrowsAsync<IOException>(() =>
            bad.UploadDocumentAsync(q.Id, WorkingStandardDocumentKind.SourceReport, "b.pdf", "application/pdf", new byte[] { 2 }, uid));

        var docs = (await bad.GetAsync(q.Id, uid)).Documents;
        var only = Assert.Single(docs);
        Assert.Equal(good.Id, only.Id);
        Assert.True(only.IsCurrent);
    }

    [Fact]
    public async Task CreateRequalification_WithQuantityOrLocation_Throws()
    {
        await using var db = NewDb();
        var (section, uid) = await SeedAsync(db);
        var entry = await AddEntryAsync(db, section.Id, "STD-01", MaterialMasterCategory.ReferenceStandard);
        var lot = await AddLotAsync(db, section.Id, entry, "WS-01/10/2026", DateTime.UtcNow.AddYears(1));
        var svc = TestServiceFactory.WorkingStandard(db, clock: NewClock(SepFirst));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => svc.CreateAsync(
            new CreateQualificationRequest(WorkingStandardQualificationKind.Requalification, lot.Id, null, null, null, null, 5m, "X", null), uid));
        Assert.Equal("Quantity and location belong to the lot for a requalification.", ex.Message);
    }

    [Fact]
    public async Task CreateInitial_SampleWithoutItem_Throws()
    {
        await using var db = NewDb();
        var (section, uid) = await SeedAsync(db);
        var entry = await AddEntryAsync(db, section.Id, "STD-01", MaterialMasterCategory.ReferenceStandard);
        var (sample, _) = await AddRmSampleAsync(db, section, WorkflowStep.Approved);
        sample.ItemId = null;
        await db.SaveChangesAsync();
        var svc = TestServiceFactory.WorkingStandard(db, clock: NewClock(SepFirst));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => svc.CreateAsync(
            new CreateQualificationRequest(WorkingStandardQualificationKind.Initial, null, entry.Id, sample.Id, null, null, 10m, "F", null), uid));
        Assert.Equal("That sample has no item name.", ex.Message);
    }

    [Fact]
    public async Task RejectedRmSample_NotEligible_AndCreateRefuses()
    {
        await using var db = NewDb();
        var (section, uid) = await SeedAsync(db);
        var entry = await AddEntryAsync(db, section.Id, "STD-01", MaterialMasterCategory.ReferenceStandard);
        var (sample, _) = await AddRmSampleAsync(db, section, WorkflowStep.Approved, "RM-9");
        var svc = TestServiceFactory.WorkingStandard(db, clock: NewClock(SepFirst));
        Assert.Single(await svc.GetEligibleSourceSamplesAsync(null, uid));

        foreach (var status in new[] { SampleStatus.Rejected, SampleStatus.RetestRequested })
        {
            (await db.Samples.FindAsync(sample.Id))!.Status = status;
            await db.SaveChangesAsync();
            Assert.Empty(await svc.GetEligibleSourceSamplesAsync(null, uid));
            var req = new CreateQualificationRequest(WorkingStandardQualificationKind.Initial, null, entry.Id, sample.Id, null, null, 10m, "Fridge 2", null);
            await Assert.ThrowsAsync<InvalidOperationException>(() => svc.CreateAsync(req, uid));
        }
    }

    [Fact]
    public void BuiltInTypesFor_UnknownSection_ExcludesWorkingStandard()
    {
        var types = MaterialTypeRules.BuiltInTypesFor("OTHER");
        Assert.DoesNotContain(MaterialType.WorkingStandard, types);
        Assert.Contains(MaterialType.ReferenceStandard, types);
    }
}
