using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.DTOs.Responses;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class SolutionPreparationServiceTests
{
    private const string Password = "ValidPassword123!";

    public class FakeTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow;
        public FakeTimeProvider(DateTimeOffset utcNow) => _utcNow = utcNow;
        public void SetUtcNow(DateTimeOffset utcNow) => _utcNow = utcNow;
        public override DateTimeOffset GetUtcNow() => _utcNow;
    }

    private static MicroLimsDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new MicroLimsDbContext(options);
        db.CurrentUserId = 1;
        return db;
    }

    // A fixed instant on 1 Sep 2026 that lands on the same calendar date in
    // Cairo whether the zone is UTC+2 or UTC+3 - safe for tests that are not
    // themselves testing the year/day boundary.
    private static readonly DateTimeOffset SepFirst = new(2026, 9, 1, 10, 0, 0, TimeSpan.Zero);

    private static (FakeTimeProvider TimeProvider, ILabClock Clock) NewClock(DateTimeOffset utcNow)
    {
        var tz = LabClock.ResolveTimeZone("Africa/Cairo");
        var fake = new FakeTimeProvider(utcNow);
        return (fake, new LabClock(fake, tz));
    }

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
            SectionId = sectionId,
            Code = code,
            Name = code,
            Category = category,
            BaseUnit = MaterialUnit.Milliliter,
            IsActive = true,
            CreatedByUserId = 1,
            CreatedAt = DateTime.UtcNow,
            LastModifiedByUserId = 1,
            LastModifiedAt = DateTime.UtcNow,
        };
        db.MaterialMasterEntries.Add(entry);
        await db.SaveChangesAsync();
        return entry;
    }

    private static async Task<Material> AddLotAsync(
        MicroLimsDbContext db, int sectionId, MaterialMasterEntry entry,
        decimal quantityRemaining = 100m, DateTime? expiryDate = null, string batch = "LOT-01")
    {
        var lot = new Material
        {
            SectionId = sectionId,
            MaterialType = MaterialType.Chemical,
            MaterialMasterEntryId = entry.Id,
            MaterialName = entry.Name,
            ManufacturerName = "Acme",
            BatchNumber = batch,
            ReceivingDate = DateTime.UtcNow.AddDays(-10),
            ExpiryDate = expiryDate ?? DateTime.UtcNow.AddYears(1),
            Location = "Shelf 1",
            QuantityReceived = quantityRemaining,
            QuantityRemaining = quantityRemaining,
            Unit = MaterialUnit.Milliliter,
            CreatedByUserId = 1,
            LastModifiedByUserId = 1,
        };
        db.Materials.Add(lot);
        await db.SaveChangesAsync();
        return lot;
    }

    private static async Task<SolutionMaster> AddSolutionAsync(
        MicroLimsDbContext db, int sectionId, string name, SolutionType type, MaterialMasterEntry componentEntry,
        decimal? phTarget = null, decimal? phTolerance = null, int shelfLifeValue = 7, ShelfLifeUnit shelfLifeUnit = ShelfLifeUnit.Days)
    {
        var service = TestServiceFactory.SolutionMaster(db);
        var created = await service.CreateAsync(new SaveSolutionMasterRequest(
            name, type, shelfLifeValue, shelfLifeUnit, "Room temperature", 1000m, "Mix and filter.",
            new List<SolutionComponentInput> { new(componentEntry.Id, 500m, SolutionComponentUnit.Milliliter) },
            PhTarget: phTarget, PhTolerance: phTolerance, SectionId: sectionId), 1);

        return await db.SolutionMasters.FirstAsync(s => s.Id == created.Id);
    }

    private static async Task<HplcMethod> AddMethodAsync(
        MicroLimsDbContext db, int sectionId, string abbreviation, SolutionMaster mobilePhase, SolutionMaster diluent, MaterialMasterEntry standardEntry)
    {
        var service = TestServiceFactory.HplcMethod(db);
        var created = await service.CreateAsync(new SaveHplcMethodRequest(
            "Vitamin C Assay", abbreviation, DateTime.UtcNow,
            "L1", 150m, 4.6m, 5m, 30m, ElutionMode.Isocratic, 1m,
            HplcDetectorType.UV, 20m, 15m, diluent.Id,
            new List<HplcMobilePhaseInput> { new("A", mobilePhase.Id, null) },
            new List<HplcGradientStepInput>(),
            new List<HplcAnalyteInput> { new(null, "Vitamin C", 254m, standardEntry.Id, 50m, 50m, 5) },
            SectionId: sectionId), 1);

        return await db.HplcMethods.FirstAsync(m => m.Id == created.Id);
    }

    private sealed class Scenario
    {
        public required DocumentSection Section;
        public required int UserId;
        public required SolutionMaster MobilePhase;
        public required SolutionMaster Diluent;
        public required HplcMethod Method;
        public required MaterialMasterEntry Entry;
        public required Material Lot;
    }

    private static async Task<Scenario> SeedScenarioAsync(
        MicroLimsDbContext db, decimal? phTarget = null, decimal? phTolerance = null,
        int shelfLifeValue = 7, ShelfLifeUnit shelfLifeUnit = ShelfLifeUnit.Days, decimal lotQuantity = 100m)
    {
        var (section, userId) = await SeedAsync(db);
        var entry = await AddEntryAsync(db, section.Id, "BUF-01");
        var diluentEntry = await AddEntryAsync(db, section.Id, "DIL-ENTRY");
        var standardEntry = await AddEntryAsync(db, section.Id, "STD-01", MaterialMasterCategory.ReferenceStandard);

        var diluent = await AddSolutionAsync(db, section.Id, "Diluent Water", SolutionType.Diluent, diluentEntry);
        var mobilePhase = await AddSolutionAsync(db, section.Id, "Mobile Phase C", SolutionType.MobilePhase, entry,
            phTarget, phTolerance, shelfLifeValue, shelfLifeUnit);
        var method = await AddMethodAsync(db, section.Id, "VIT-C", mobilePhase, diluent, standardEntry);
        var lot = await AddLotAsync(db, section.Id, entry, lotQuantity);

        return new Scenario { Section = section, UserId = userId, MobilePhase = mobilePhase, Diluent = diluent, Method = method, Entry = entry, Lot = lot };
    }

    // --- Start ---

    [Fact]
    public async Task Start_MobilePhase_WithoutMethod_Throws()
    {
        await using var db = NewDb();
        var s = await SeedScenarioAsync(db);
        var service = TestServiceFactory.SolutionPreparation(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.StartAsync(new StartPreparationRequest(s.MobilePhase.Id, null), s.UserId));

        Assert.Contains("HPLC method is required", ex.Message);
    }

    [Fact]
    public async Task Start_MobilePhase_MethodNotUsingSolution_Throws()
    {
        await using var db = NewDb();
        var s = await SeedScenarioAsync(db);
        var otherEntry = await AddEntryAsync(db, s.Section.Id, "BUF-02");
        var otherMobilePhase = await AddSolutionAsync(db, s.Section.Id, "Mobile Phase D", SolutionType.MobilePhase, otherEntry);
        var service = TestServiceFactory.SolutionPreparation(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.StartAsync(new StartPreparationRequest(otherMobilePhase.Id, s.Method.Id), s.UserId));

        Assert.Contains("does not use this mobile phase", ex.Message);
    }

    [Fact]
    public async Task Start_Diluent_WithMethod_Throws()
    {
        await using var db = NewDb();
        var s = await SeedScenarioAsync(db);
        var service = TestServiceFactory.SolutionPreparation(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.StartAsync(new StartPreparationRequest(s.Diluent.Id, s.Method.Id), s.UserId));

        Assert.Contains("only applies to mobile phase", ex.Message);
    }

    [Fact]
    public async Task Start_CopiesComponentsAndSnapshot()
    {
        await using var db = NewDb();
        var s = await SeedScenarioAsync(db);
        var service = TestServiceFactory.SolutionPreparation(db);

        var result = await service.StartAsync(new StartPreparationRequest(s.MobilePhase.Id, s.Method.Id), s.UserId);

        Assert.Equal(SolutionPreparationStatus.InProgress, result.Status);
        Assert.Single(result.Components);
        Assert.Equal("BUF-01", result.Components[0].EntryCode);
        Assert.Equal(500m, result.Components[0].RecipeQuantity);
        Assert.Contains("Mobile Phase C", result.RecipeSnapshotJson);
        Assert.Equal(s.Section.Id, result.SectionId);
    }

    // --- Save ---

    [Fact]
    public async Task Save_LotOfOtherEntry_Throws()
    {
        await using var db = NewDb();
        var s = await SeedScenarioAsync(db);
        var otherEntry = await AddEntryAsync(db, s.Section.Id, "OTHER-ENTRY");
        var otherLot = await AddLotAsync(db, s.Section.Id, otherEntry, 50m, batch: "OTHER-LOT");
        var service = TestServiceFactory.SolutionPreparation(db);
        var prep = await service.StartAsync(new StartPreparationRequest(s.MobilePhase.Id, s.Method.Id), s.UserId);
        var componentId = prep.Components[0].Id;

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveAsync(
            prep.Id,
            new SavePreparationRequest(new List<PreparationComponentInput> { new(componentId, otherLot.Id, 5m) }, null, null),
            s.UserId));

        Assert.Contains("not linked", ex.Message);
    }

    [Fact]
    public async Task Save_ExpiredLot_Throws()
    {
        await using var db = NewDb();
        var s = await SeedScenarioAsync(db);
        var expiredLot = await AddLotAsync(db, s.Section.Id, s.Entry, 50m, DateTime.UtcNow.AddDays(-1), "EXPIRED-LOT");
        var service = TestServiceFactory.SolutionPreparation(db);
        var prep = await service.StartAsync(new StartPreparationRequest(s.MobilePhase.Id, s.Method.Id), s.UserId);
        var componentId = prep.Components[0].Id;

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveAsync(
            prep.Id,
            new SavePreparationRequest(new List<PreparationComponentInput> { new(componentId, expiredLot.Id, 5m) }, null, null),
            s.UserId));

        Assert.Contains("Expired", ex.Message);
    }

    [Fact]
    public async Task Save_InsufficientLot_Throws()
    {
        await using var db = NewDb();
        var s = await SeedScenarioAsync(db, lotQuantity: 3m);
        var service = TestServiceFactory.SolutionPreparation(db);
        var prep = await service.StartAsync(new StartPreparationRequest(s.MobilePhase.Id, s.Method.Id), s.UserId);
        var componentId = prep.Components[0].Id;

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveAsync(
            prep.Id,
            new SavePreparationRequest(new List<PreparationComponentInput> { new(componentId, s.Lot.Id, 10m) }, null, null),
            s.UserId));

        Assert.Contains("left", ex.Message);
    }

    // --- Complete ---

    [Fact]
    public async Task Complete_DeductsStock_SetsCodeExpiryAndStatus()
    {
        await using var db = NewDb();
        var s = await SeedScenarioAsync(db, shelfLifeValue: 7, shelfLifeUnit: ShelfLifeUnit.Days);
        var (_, clock) = NewClock(SepFirst);
        var service = TestServiceFactory.SolutionPreparation(db, clock: clock);

        var prep = await service.StartAsync(new StartPreparationRequest(s.MobilePhase.Id, s.Method.Id), s.UserId);
        var componentId = prep.Components[0].Id;
        await service.SaveAsync(prep.Id,
            new SavePreparationRequest(new List<PreparationComponentInput> { new(componentId, s.Lot.Id, 5m) }, 1000m, null),
            s.UserId);

        var completed = await service.CompleteAsync(prep.Id, new CompletePreparationRequest(Password, "Prepared for run"), s.UserId, "127.0.0.1");

        Assert.Equal("MP-VIT-C 01/09/2026", completed.Code);
        Assert.Equal(SolutionPreparationStatus.Prepared, completed.Status);
        Assert.NotNull(completed.ExpiresAt);
        Assert.Equal(SepFirst.UtcDateTime.AddDays(7), completed.ExpiresAt!.Value);

        var lot = await db.Materials.AsNoTracking().SingleAsync(m => m.Id == s.Lot.Id);
        Assert.Equal(95m, lot.QuantityRemaining);
    }

    [Fact]
    public async Task Complete_SecondInSeries_Gets02()
    {
        await using var db = NewDb();
        var s = await SeedScenarioAsync(db, lotQuantity: 100m);
        var (_, clock) = NewClock(SepFirst);
        var service = TestServiceFactory.SolutionPreparation(db, clock: clock);

        async Task<string?> CompleteOnce()
        {
            var prep = await service.StartAsync(new StartPreparationRequest(s.MobilePhase.Id, s.Method.Id), s.UserId);
            var componentId = prep.Components[0].Id;
            await service.SaveAsync(prep.Id,
                new SavePreparationRequest(new List<PreparationComponentInput> { new(componentId, s.Lot.Id, 1m) }, 1000m, null),
                s.UserId);
            var completed = await service.CompleteAsync(prep.Id, new CompletePreparationRequest(Password, null), s.UserId, null);
            return completed.Code;
        }

        var first = await CompleteOnce();
        var second = await CompleteOnce();

        Assert.Equal("MP-VIT-C 01/09/2026", first);
        Assert.Equal("MP-VIT-C 02/09/2026", second);
    }

    [Fact]
    public async Task Complete_NewLabYear_RestartsAt01()
    {
        var tz = LabClock.ResolveTimeZone("Africa/Cairo");
        var utcDec31 = new DateTimeOffset(2026, 12, 31, 22, 30, 0, TimeSpan.Zero);
        var localDec31 = TimeZoneInfo.ConvertTime(utcDec31, tz);
        Assert.Equal(new DateTime(2027, 1, 1), localDec31.Date); // sanity: this instant really does cross into the new lab year

        await using var db = NewDb();
        var s = await SeedScenarioAsync(db);
        var (fake, clock) = NewClock(SepFirst);
        var service = TestServiceFactory.SolutionPreparation(db, clock: clock);

        // A prior completion late in 2026, so the series already has "01".
        var prep1 = await service.StartAsync(new StartPreparationRequest(s.MobilePhase.Id, s.Method.Id), s.UserId);
        await service.SaveAsync(prep1.Id,
            new SavePreparationRequest(new List<PreparationComponentInput> { new(prep1.Components[0].Id, s.Lot.Id, 1m) }, 1000m, null),
            s.UserId);
        await service.CompleteAsync(prep1.Id, new CompletePreparationRequest(Password, null), s.UserId, null);

        fake.SetUtcNow(utcDec31);
        var prep2 = await service.StartAsync(new StartPreparationRequest(s.MobilePhase.Id, s.Method.Id), s.UserId);
        await service.SaveAsync(prep2.Id,
            new SavePreparationRequest(new List<PreparationComponentInput> { new(prep2.Components[0].Id, s.Lot.Id, 1m) }, 1000m, null),
            s.UserId);
        var completed2 = await service.CompleteAsync(prep2.Id, new CompletePreparationRequest(Password, null), s.UserId, null);

        Assert.Equal("MP-VIT-C 01/01/2027", completed2.Code);
    }

    [Fact]
    public async Task Complete_DiluentCodeHasNoAbbreviation()
    {
        await using var db = NewDb();
        var s = await SeedScenarioAsync(db);
        var diluentEntry = await AddEntryAsync(db, s.Section.Id, "DIL-COMP");
        var diluentLot = await AddLotAsync(db, s.Section.Id, diluentEntry, 100m, batch: "DIL-LOT");
        var diluentSolution = await AddSolutionAsync(db, s.Section.Id, "Diluent Buffer", SolutionType.Diluent, diluentEntry);
        var (_, clock) = NewClock(SepFirst);
        var service = TestServiceFactory.SolutionPreparation(db, clock: clock);

        var prep = await service.StartAsync(new StartPreparationRequest(diluentSolution.Id, null), s.UserId);
        await service.SaveAsync(prep.Id,
            new SavePreparationRequest(new List<PreparationComponentInput> { new(prep.Components[0].Id, diluentLot.Id, 5m) }, 1000m, null),
            s.UserId);

        var completed = await service.CompleteAsync(prep.Id, new CompletePreparationRequest(Password, null), s.UserId, null);

        Assert.Equal("DL-01/09/2026", completed.Code);
    }

    [Fact]
    public async Task Complete_MissingLot_Throws_NoStockChange()
    {
        await using var db = NewDb();
        var s = await SeedScenarioAsync(db);
        var service = TestServiceFactory.SolutionPreparation(db);
        var prep = await service.StartAsync(new StartPreparationRequest(s.MobilePhase.Id, s.Method.Id), s.UserId);
        // Never saved a lot for the component.

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CompleteAsync(prep.Id, new CompletePreparationRequest(Password, null), s.UserId, null));

        Assert.Contains("lot and a quantity", ex.Message);
        var lot = await db.Materials.AsNoTracking().SingleAsync(m => m.Id == s.Lot.Id);
        Assert.Equal(100m, lot.QuantityRemaining);
    }

    [Fact]
    public async Task Complete_PhOutOfTolerance_Throws()
    {
        await using var db = NewDb();
        var s = await SeedScenarioAsync(db, phTarget: 7.0m, phTolerance: 0.2m);
        var service = TestServiceFactory.SolutionPreparation(db);
        var prep = await service.StartAsync(new StartPreparationRequest(s.MobilePhase.Id, s.Method.Id), s.UserId);
        await service.SaveAsync(prep.Id,
            new SavePreparationRequest(new List<PreparationComponentInput> { new(prep.Components[0].Id, s.Lot.Id, 5m) }, 1000m, 7.5m),
            s.UserId);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CompleteAsync(prep.Id, new CompletePreparationRequest(Password, null), s.UserId, null));

        Assert.Contains("outside", ex.Message);
    }

    [Fact]
    public async Task Complete_LotExpiredSincePicked_Throws_NamesComponent()
    {
        await using var db = NewDb();
        var s = await SeedScenarioAsync(db);
        var service = TestServiceFactory.SolutionPreparation(db);
        var prep = await service.StartAsync(new StartPreparationRequest(s.MobilePhase.Id, s.Method.Id), s.UserId);
        await service.SaveAsync(prep.Id,
            new SavePreparationRequest(new List<PreparationComponentInput> { new(prep.Components[0].Id, s.Lot.Id, 5m) }, 1000m, null),
            s.UserId);

        // The lot expires after it was picked but before signing.
        var trackedLot = await db.Materials.FirstAsync(m => m.Id == s.Lot.Id);
        trackedLot.ExpiryDate = DateTime.UtcNow.AddDays(-1);
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CompleteAsync(prep.Id, new CompletePreparationRequest(Password, null), s.UserId, null));

        Assert.Contains(s.Entry.Name, ex.Message);
        Assert.Contains(s.Lot.BatchNumber, ex.Message);
    }

    [Fact]
    public async Task Complete_WrongPassword_LeavesInProgress_NoStockChange()
    {
        await using var db = NewDb();
        var s = await SeedScenarioAsync(db);
        var service = TestServiceFactory.SolutionPreparation(db);
        var prep = await service.StartAsync(new StartPreparationRequest(s.MobilePhase.Id, s.Method.Id), s.UserId);
        await service.SaveAsync(prep.Id,
            new SavePreparationRequest(new List<PreparationComponentInput> { new(prep.Components[0].Id, s.Lot.Id, 5m) }, 1000m, null),
            s.UserId);

        await Assert.ThrowsAsync<SignatureVerificationException>(
            () => service.CompleteAsync(prep.Id, new CompletePreparationRequest("WrongPassword!", null), s.UserId, null));

        var reloaded = await service.GetByIdAsync(prep.Id, s.UserId);
        Assert.Equal(SolutionPreparationStatus.InProgress, reloaded.Status);
        var lot = await db.Materials.AsNoTracking().SingleAsync(m => m.Id == s.Lot.Id);
        Assert.Equal(100m, lot.QuantityRemaining);
    }

    // --- Cancel / Discard ---

    [Fact]
    public async Task Cancel_RequiresReason_NoStockChange()
    {
        await using var db = NewDb();
        var s = await SeedScenarioAsync(db);
        var service = TestServiceFactory.SolutionPreparation(db);
        var prep = await service.StartAsync(new StartPreparationRequest(s.MobilePhase.Id, s.Method.Id), s.UserId);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CancelAsync(prep.Id, "", s.UserId));

        var cancelled = await service.CancelAsync(prep.Id, "Analyst changed their mind.", s.UserId);
        Assert.Equal(SolutionPreparationStatus.Cancelled, cancelled.Status);

        var lot = await db.Materials.AsNoTracking().SingleAsync(m => m.Id == s.Lot.Id);
        Assert.Equal(100m, lot.QuantityRemaining);
    }

    [Fact]
    public async Task Discard_AfterPrepared_DoesNotRestoreStock()
    {
        await using var db = NewDb();
        var s = await SeedScenarioAsync(db);
        var service = TestServiceFactory.SolutionPreparation(db);
        var prep = await service.StartAsync(new StartPreparationRequest(s.MobilePhase.Id, s.Method.Id), s.UserId);
        await service.SaveAsync(prep.Id,
            new SavePreparationRequest(new List<PreparationComponentInput> { new(prep.Components[0].Id, s.Lot.Id, 5m) }, 1000m, null),
            s.UserId);
        await service.CompleteAsync(prep.Id, new CompletePreparationRequest(Password, null), s.UserId, null);

        var discarded = await service.DiscardAsync(prep.Id, "No longer needed.", s.UserId);

        Assert.Equal(SolutionPreparationStatus.Discarded, discarded.Status);
        var lot = await db.Materials.AsNoTracking().SingleAsync(m => m.Id == s.Lot.Id);
        Assert.Equal(95m, lot.QuantityRemaining);
    }

    [Fact]
    public async Task Closed_CannotBeCancelledOrDiscarded()
    {
        await using var db = NewDb();
        var s = await SeedScenarioAsync(db);
        var service = TestServiceFactory.SolutionPreparation(db);
        var prep = await service.StartAsync(new StartPreparationRequest(s.MobilePhase.Id, s.Method.Id), s.UserId);
        await service.SaveAsync(prep.Id,
            new SavePreparationRequest(new List<PreparationComponentInput> { new(prep.Components[0].Id, s.Lot.Id, 5m) }, 1000m, null),
            s.UserId);
        await service.CompleteAsync(prep.Id, new CompletePreparationRequest(Password, null), s.UserId, null);

        var cancelEx = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CancelAsync(prep.Id, "reason", s.UserId));
        Assert.Contains("closed", cancelEx.Message);

        await service.DiscardAsync(prep.Id, "reason", s.UserId);
        var discardAgainEx = await Assert.ThrowsAsync<InvalidOperationException>(() => service.DiscardAsync(prep.Id, "reason", s.UserId));
        Assert.Contains("closed", discardAgainEx.Message);
    }

    // --- Expiry ---

    [Fact]
    public async Task GetById_PastExpiry_ShowsExpired_AndNotInAvailable()
    {
        await using var db = NewDb();
        var s = await SeedScenarioAsync(db, shelfLifeValue: 1, shelfLifeUnit: ShelfLifeUnit.Hours);
        var (fake, clock) = NewClock(SepFirst);
        var service = TestServiceFactory.SolutionPreparation(db, clock: clock);
        var prep = await service.StartAsync(new StartPreparationRequest(s.MobilePhase.Id, s.Method.Id), s.UserId);
        await service.SaveAsync(prep.Id,
            new SavePreparationRequest(new List<PreparationComponentInput> { new(prep.Components[0].Id, s.Lot.Id, 5m) }, 1000m, null),
            s.UserId);
        await service.CompleteAsync(prep.Id, new CompletePreparationRequest(Password, null), s.UserId, null);

        // The worker has not run yet, but this instant is past the 1-hour shelf life.
        fake.SetUtcNow(SepFirst.AddHours(2));

        var reloaded = await service.GetByIdAsync(prep.Id, s.UserId);
        Assert.Equal(SolutionPreparationStatus.Prepared, reloaded.Status);
        Assert.Equal(SolutionPreparationStatus.Expired, reloaded.EffectiveStatus);

        var available = await service.GetAvailableAsync(s.UserId, s.MobilePhase.Id, s.Method.Id);
        Assert.Empty(available);
    }

    [Fact]
    public async Task ExpireDueAsync_FlipsDueRows_WritesHistory()
    {
        await using var db = NewDb();
        var s = await SeedScenarioAsync(db, shelfLifeValue: 1, shelfLifeUnit: ShelfLifeUnit.Hours);
        var (fake, clock) = NewClock(SepFirst);
        var service = TestServiceFactory.SolutionPreparation(db, clock: clock);
        var prep = await service.StartAsync(new StartPreparationRequest(s.MobilePhase.Id, s.Method.Id), s.UserId);
        await service.SaveAsync(prep.Id,
            new SavePreparationRequest(new List<PreparationComponentInput> { new(prep.Components[0].Id, s.Lot.Id, 5m) }, 1000m, null),
            s.UserId);
        await service.CompleteAsync(prep.Id, new CompletePreparationRequest(Password, null), s.UserId, null);

        fake.SetUtcNow(SepFirst.AddHours(2));

        var expiredCount = await service.ExpireDueAsync();
        Assert.Equal(1, expiredCount);

        var reloaded = await service.GetByIdAsync(prep.Id, s.UserId);
        Assert.Equal(SolutionPreparationStatus.Expired, reloaded.Status);
        var historyRow = Assert.Single(reloaded.StatusHistory, h => h.ToStatus == SolutionPreparationStatus.Expired);
        Assert.Null(historyRow.ChangedByUserId);
        Assert.Equal("Expired automatically", historyRow.Reason);
    }

    // --- Traceability ---

    [Fact]
    public async Task GetByLot_ListsPreparationsThatConsumedIt()
    {
        await using var db = NewDb();
        var s = await SeedScenarioAsync(db);
        var service = TestServiceFactory.SolutionPreparation(db);
        var prep = await service.StartAsync(new StartPreparationRequest(s.MobilePhase.Id, s.Method.Id), s.UserId);
        await service.SaveAsync(prep.Id,
            new SavePreparationRequest(new List<PreparationComponentInput> { new(prep.Components[0].Id, s.Lot.Id, 5m) }, 1000m, null),
            s.UserId);
        var completed = await service.CompleteAsync(prep.Id, new CompletePreparationRequest(Password, null), s.UserId, null);

        var byLot = await service.GetByLotAsync(s.Lot.Id, s.UserId);

        Assert.Single(byLot, i => i.Id == completed.Id);
    }

    // --- Microbiology: general types, prepared-solution components, sterilization, release ---

    private sealed class Micro
    {
        public required DocumentSection Section;
        public required int PreparerId;
        public required int ReleaserId;
        public required MaterialMasterEntry SaltEntry;
        public required Material SaltLot;
        public required MaterialMasterEntry NaclEntry;
        public required Material NaclLot;
        public required SolutionMaster Stock;     // Reagent "Phosphate buffer stock"
        public required SolutionMaster Working;   // Diluent: stock + NaCl + a text line
    }

    private static async Task<int> AddReleaserAsync(MicroLimsDbContext db)
    {
        var role = new Role { Name = "Reviewer", Type = RoleType.Reviewer, IsActive = true };
        db.Roles.Add(role);
        await db.SaveChangesAsync();
        var user = new User
        {
            Username = "r_" + Guid.NewGuid().ToString("N")[..6],
            FullName = "Test Reviewer",
            RoleId = role.Id,
            IsActive = true,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password),
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        TestServiceFactory.AssignUserToMicroSection(db, user.Id);
        return user.Id;
    }

    private static async Task<SolutionMaster> AddGeneralAsync(
        MicroLimsDbContext db, int sectionId, string name, SolutionType type, List<SolutionComponentInput> components,
        bool sterilization = false, bool release = false, int shelfLifeValue = 30, ShelfLifeUnit shelfLifeUnit = ShelfLifeUnit.Days)
    {
        var created = await TestServiceFactory.SolutionMaster(db).CreateAsync(new SaveSolutionMasterRequest(
            name, type, shelfLifeValue, shelfLifeUnit, "2-8 C", 1000m, "Dissolve and make up.", components,
            SectionId: sectionId, SterilizationRequired: sterilization, ReleaseRequired: release), 1);
        return await db.SolutionMasters.FirstAsync(m => m.Id == created.Id);
    }

    private static async Task<Micro> SeedMicroAsync(
        MicroLimsDbContext db, bool stockRelease = true, bool workingRelease = false, bool workingSterilization = false,
        int stockShelfLifeValue = 30, ShelfLifeUnit stockShelfLifeUnit = ShelfLifeUnit.Days)
    {
        var (section, userId) = await SeedAsync(db);
        var releaserId = await AddReleaserAsync(db);
        var salt = await AddEntryAsync(db, section.Id, "KH2PO4");
        var nacl = await AddEntryAsync(db, section.Id, "NACL");
        var saltLot = await AddLotAsync(db, section.Id, salt, 500m, batch: "SALT-01");
        var naclLot = await AddLotAsync(db, section.Id, nacl, 100m, batch: "NACL-01");

        var stock = await AddGeneralAsync(db, section.Id, "Phosphate buffer stock", SolutionType.Reagent,
            new() { new(salt.Id, 34m, SolutionComponentUnit.Gram) }, release: stockRelease,
            shelfLifeValue: stockShelfLifeValue, shelfLifeUnit: stockShelfLifeUnit);
        var working = await AddGeneralAsync(db, section.Id, "Phosphate buffer solution", SolutionType.Diluent,
            new()
            {
                new(null, 1.25m, SolutionComponentUnit.Milliliter, SolutionComponentKind.PreparedSolution, SourceSolutionMasterId: stock.Id),
                new(nacl.Id, 4.3m, SolutionComponentUnit.Gram),
                new(null, null, null, SolutionComponentKind.Text, Text: "Purified water to 1000 mL"),
            }, sterilization: workingSterilization, release: workingRelease);

        return new Micro
        {
            Section = section, PreparerId = userId, ReleaserId = releaserId, SaltEntry = salt, SaltLot = saltLot,
            NaclEntry = nacl, NaclLot = naclLot, Stock = stock, Working = working
        };
    }

    // Prepares one bottle of the stock recipe (it has a single material line).
    private static async Task<SolutionPreparationResponse> PrepareStockAsync(
        SolutionPreparationService service, Micro m, SolutionMaster? recipe = null)
    {
        var prep = await service.StartAsync(new StartPreparationRequest((recipe ?? m.Stock).Id, null), m.PreparerId);
        await service.SaveAsync(prep.Id,
            new SavePreparationRequest(new List<PreparationComponentInput> { new(prep.Components[0].Id, m.SaltLot.Id, 34m) }, 1000m, null),
            m.PreparerId);
        return await service.CompleteAsync(prep.Id, new CompletePreparationRequest(Password, null), m.PreparerId, null);
    }

    private static SavePreparationRequest WorkingSave(
        SolutionPreparationResponse working, Micro m, int? sourcePreparationId, decimal? sourceQuantity = null,
        int? autoclaveEquipmentId = null, string? program = null, string? loadType = null,
        decimal? temperature = null, int? cycleTime = null, int? cycleNumber = null)
    {
        var inputs = new List<PreparationComponentInput>();
        foreach (var c in working.Components)
        {
            inputs.Add(c.Kind switch
            {
                SolutionComponentKind.PreparedSolution => new(c.Id, null, sourceQuantity, sourcePreparationId),
                SolutionComponentKind.Material => new(c.Id, m.NaclLot.Id, 4.3m),
                _ => new(c.Id, null, null),
            });
        }

        return new SavePreparationRequest(inputs, 1000m, null, autoclaveEquipmentId, program, loadType, temperature, cycleTime, cycleNumber);
    }

    private static async Task<Equipment> AddEquipmentAsync(MicroLimsDbContext db, int sectionId, EquipmentType type)
    {
        var uid = Guid.NewGuid().ToString("N")[..6];
        var equipment = new Equipment { Name = $"{type} {uid}", Code = $"EQ-{uid}", Type = type, SectionId = sectionId };
        db.Equipment.Add(equipment);
        await db.SaveChangesAsync();
        return equipment;
    }

    [Theory]
    [InlineData(SolutionType.Reagent, "RE-01/09/2026")]
    [InlineData(SolutionType.Neutralizer, "NT-01/09/2026")]
    [InlineData(SolutionType.Diluent, "DL-01/09/2026")]
    public async Task Complete_GeneralTypes_GetTheirOwnCodePrefix(SolutionType type, string expectedCode)
    {
        await using var db = NewDb();
        var m = await SeedMicroAsync(db);
        var recipe = await AddGeneralAsync(db, m.Section.Id, "General " + type, type,
            new() { new(m.SaltEntry.Id, 1m, SolutionComponentUnit.Gram) });
        var (_, clock) = NewClock(SepFirst);
        var service = TestServiceFactory.SolutionPreparation(db, clock: clock);

        var done = await PrepareStockAsync(service, m, recipe);

        Assert.Equal(expectedCode, done.Code);
        Assert.Null(done.ApprovalStatus);   // no release needed
    }

    [Fact]
    public async Task Start_GeneralType_WithHplcMethod_Throws()
    {
        await using var db = NewDb();
        var m = await SeedMicroAsync(db);
        var service = TestServiceFactory.SolutionPreparation(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.StartAsync(new StartPreparationRequest(m.Stock.Id, 1), m.PreparerId));
    }

    [Fact]
    public async Task Start_CopiesPreparedSolutionAndTextComponents()
    {
        await using var db = NewDb();
        var m = await SeedMicroAsync(db);
        var service = TestServiceFactory.SolutionPreparation(db);

        var prep = await service.StartAsync(new StartPreparationRequest(m.Working.Id, null), m.PreparerId);

        Assert.Equal(3, prep.Components.Count);
        var source = prep.Components[0];
        Assert.Equal(SolutionComponentKind.PreparedSolution, source.Kind);
        Assert.Equal(m.Stock.Id, source.SourceSolutionMasterId);
        Assert.Equal("Phosphate buffer stock", source.EntryName);
        Assert.Null(source.MaterialMasterEntryId);
        Assert.Equal(SolutionComponentKind.Material, prep.Components[1].Kind);
        Assert.Equal("Purified water to 1000 mL", prep.Components[2].Text);
        Assert.Null(prep.Components[2].RecipeQuantity);
    }

    [Fact]
    public async Task WorkingSolution_PicksReleasedStockPreparation_NoStockDeductedForIt()
    {
        await using var db = NewDb();
        var m = await SeedMicroAsync(db);
        var (_, clock) = NewClock(SepFirst);
        var service = TestServiceFactory.SolutionPreparation(db, clock: clock);
        var release = TestServiceFactory.SolutionPreparationRelease(db, clock);

        var stockPrep = await PrepareStockAsync(service, m);
        Assert.Equal(ApprovalGateStatus.PendingReview, stockPrep.ApprovalStatus);
        Assert.Equal(SolutionPreparationStatus.Prepared, stockPrep.Status);   // status does not change
        await release.DecideAsync(stockPrep.Id, new ReleaseSolutionPreparationRequest(true, "OK", Password), m.ReleaserId, null);

        var working = await service.StartAsync(new StartPreparationRequest(m.Working.Id, null), m.PreparerId);
        var options = await service.GetSourcePreparationsAsync(working.Id, working.Components[0].Id, m.PreparerId);
        var option = Assert.Single(options);
        Assert.True(option.Usable);
        Assert.Equal(stockPrep.Code, option.Code);

        await service.SaveAsync(working.Id, WorkingSave(working, m, stockPrep.Id, sourceQuantity: 1.25m), m.PreparerId);
        var done = await service.CompleteAsync(working.Id, new CompletePreparationRequest(Password, null), m.PreparerId, null);

        Assert.Equal(SolutionPreparationStatus.Prepared, done.Status);
        Assert.Equal(stockPrep.Id, done.Components[0].SourcePreparationId);
        Assert.Equal(stockPrep.Code, done.Components[0].SourcePreparationCode);
        Assert.Equal(1.25m, done.Components[0].QuantityUsed);
        Assert.Null(done.Components[0].MaterialId);

        // Only the NaCl lot was deducted (4.3 g); the salt lot went down by the stock preparation's own 34 g.
        Assert.Equal(95.7m, (await db.Materials.AsNoTracking().SingleAsync(x => x.Id == m.NaclLot.Id)).QuantityRemaining);
        Assert.Equal(466m, (await db.Materials.AsNoTracking().SingleAsync(x => x.Id == m.SaltLot.Id)).QuantityRemaining);
    }

    [Fact]
    public async Task WorkingSolution_RejectsPendingRejectedExpiredAndWrongRecipePreparations()
    {
        await using var db = NewDb();
        var m = await SeedMicroAsync(db, stockShelfLifeValue: 1, stockShelfLifeUnit: ShelfLifeUnit.Hours);
        var (fake, clock) = NewClock(SepFirst);
        var service = TestServiceFactory.SolutionPreparation(db, clock: clock);
        var release = TestServiceFactory.SolutionPreparationRelease(db, clock);
        var working = await service.StartAsync(new StartPreparationRequest(m.Working.Id, null), m.PreparerId);

        // Pending release.
        var pending = await PrepareStockAsync(service, m);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SaveAsync(working.Id, WorkingSave(working, m, pending.Id), m.PreparerId));
        Assert.Contains("awaiting release", ex.Message);
        var listed = Assert.Single(await service.GetSourcePreparationsAsync(working.Id, working.Components[0].Id, m.PreparerId));
        Assert.False(listed.Usable);

        // Rejected.
        await release.DecideAsync(pending.Id, new ReleaseSolutionPreparationRequest(false, "Cloudy", Password), m.ReleaserId, null);
        ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SaveAsync(working.Id, WorkingSave(working, m, pending.Id), m.PreparerId));
        Assert.Contains("rejected", ex.Message);

        // Wrong recipe: a preparation of the working recipe itself is not a stock bottle.
        var otherRecipe = await AddGeneralAsync(db, m.Section.Id, "Other reagent", SolutionType.Reagent,
            new() { new(m.SaltEntry.Id, 1m, SolutionComponentUnit.Gram) });
        var wrong = await PrepareStockAsync(service, m, otherRecipe);
        ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SaveAsync(working.Id, WorkingSave(working, m, wrong.Id), m.PreparerId));
        Assert.Contains("not a preparation of", ex.Message);

        // Expired (1-hour shelf life, two hours later).
        var released = await PrepareStockAsync(service, m);
        await release.DecideAsync(released.Id, new ReleaseSolutionPreparationRequest(true, null, Password), m.ReleaserId, null);
        fake.SetUtcNow(SepFirst.AddHours(2));
        ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SaveAsync(working.Id, WorkingSave(working, m, released.Id), m.PreparerId));
        Assert.Contains("Expired", ex.Message);
    }

    [Fact]
    public async Task WorkingSolution_StockPreparationExpiresBeforeComplete_Throws()
    {
        await using var db = NewDb();
        var m = await SeedMicroAsync(db, stockRelease: false, stockShelfLifeValue: 3, stockShelfLifeUnit: ShelfLifeUnit.Hours);
        var (fake, clock) = NewClock(SepFirst);
        var service = TestServiceFactory.SolutionPreparation(db, clock: clock);

        var stockPrep = await PrepareStockAsync(service, m);
        var working = await service.StartAsync(new StartPreparationRequest(m.Working.Id, null), m.PreparerId);
        await service.SaveAsync(working.Id, WorkingSave(working, m, stockPrep.Id), m.PreparerId);

        fake.SetUtcNow(SepFirst.AddHours(4));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CompleteAsync(working.Id, new CompletePreparationRequest(Password, null), m.PreparerId, null));
        Assert.Contains("Expired", ex.Message);
        Assert.Equal(100m, (await db.Materials.AsNoTracking().SingleAsync(x => x.Id == m.NaclLot.Id)).QuantityRemaining);
    }

    [Fact]
    public async Task Complete_PreparedSolutionComponentWithoutPick_Throws()
    {
        await using var db = NewDb();
        var m = await SeedMicroAsync(db);
        var service = TestServiceFactory.SolutionPreparation(db);
        var working = await service.StartAsync(new StartPreparationRequest(m.Working.Id, null), m.PreparerId);
        await service.SaveAsync(working.Id, WorkingSave(working, m, null), m.PreparerId);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CompleteAsync(working.Id, new CompletePreparationRequest(Password, null), m.PreparerId, null));
        Assert.Contains("prepared bottle", ex.Message);
    }

    [Fact]
    public async Task Save_TextLineWithAPick_Throws_AndLotOptionsRefuseNonMaterialLines()
    {
        await using var db = NewDb();
        var m = await SeedMicroAsync(db);
        var service = TestServiceFactory.SolutionPreparation(db);
        var working = await service.StartAsync(new StartPreparationRequest(m.Working.Id, null), m.PreparerId);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveAsync(working.Id,
            new SavePreparationRequest(new List<PreparationComponentInput> { new(working.Components[2].Id, m.NaclLot.Id, 1m) }, null, null),
            m.PreparerId));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetLotOptionsAsync(working.Id, working.Components[0].Id, m.PreparerId));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetSourcePreparationsAsync(working.Id, working.Components[1].Id, m.PreparerId));
    }

    [Fact]
    public async Task Complete_SterilizationRequired_NeedsAWholeAutoclaveRecord()
    {
        await using var db = NewDb();
        var m = await SeedMicroAsync(db, stockRelease: false, workingSterilization: true);
        var (_, clock) = NewClock(SepFirst);
        var service = TestServiceFactory.SolutionPreparation(db, clock: clock);
        var autoclave = await AddEquipmentAsync(db, m.Section.Id, EquipmentType.Autoclave);
        var incubator = await AddEquipmentAsync(db, m.Section.Id, EquipmentType.Incubator);
        var stockPrep = await PrepareStockAsync(service, m);
        var working = await service.StartAsync(new StartPreparationRequest(m.Working.Id, null), m.PreparerId);

        // Nothing recorded.
        await service.SaveAsync(working.Id, WorkingSave(working, m, stockPrep.Id), m.PreparerId);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CompleteAsync(working.Id, new CompletePreparationRequest(Password, null), m.PreparerId, null));
        Assert.Contains("autoclave record", ex.Message);

        // Equipment that is not an autoclave is refused when entered.
        ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveAsync(working.Id,
            WorkingSave(working, m, stockPrep.Id, autoclaveEquipmentId: incubator.Id, program: "P1", loadType: "Liquid", temperature: 121m, cycleTime: 15, cycleNumber: 3),
            m.PreparerId));
        Assert.Equal("Selected equipment is not a valid autoclave.", ex.Message);

        // Zero values are refused.
        ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveAsync(working.Id,
            WorkingSave(working, m, stockPrep.Id, autoclaveEquipmentId: autoclave.Id, program: "P1", loadType: "Liquid", temperature: 0m, cycleTime: 15, cycleNumber: 3),
            m.PreparerId));
        Assert.Contains("temperature", ex.Message);

        // Incomplete record at completion.
        await service.SaveAsync(working.Id,
            WorkingSave(working, m, stockPrep.Id, autoclaveEquipmentId: autoclave.Id, program: "P1", loadType: "Liquid", temperature: 121m),
            m.PreparerId);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CompleteAsync(working.Id, new CompletePreparationRequest(Password, null), m.PreparerId, null));

        // Complete record.
        await service.SaveAsync(working.Id,
            WorkingSave(working, m, stockPrep.Id, autoclaveEquipmentId: autoclave.Id, program: "P1", loadType: "Liquid", temperature: 121m, cycleTime: 15, cycleNumber: 3),
            m.PreparerId);
        var done = await service.CompleteAsync(working.Id, new CompletePreparationRequest(Password, null), m.PreparerId, null);

        Assert.Equal(SolutionPreparationStatus.Prepared, done.Status);
        Assert.Equal(autoclave.Id, done.AutoclaveEquipmentId);
        Assert.Equal("P1", done.AutoclaveProgram);
        Assert.Equal("Liquid", done.LoadType);
        Assert.Equal(121m, done.AutoclaveTemperature);
        Assert.Equal(15, done.AutoclaveCycleTime);
        Assert.Equal(3, done.AutoclaveCycleNumber);
    }

    [Fact]
    public async Task Save_SterilizationNotRequired_RejectsAutoclaveFields()
    {
        await using var db = NewDb();
        var m = await SeedMicroAsync(db, stockRelease: false);
        var service = TestServiceFactory.SolutionPreparation(db);
        var autoclave = await AddEquipmentAsync(db, m.Section.Id, EquipmentType.Autoclave);
        var prep = await service.StartAsync(new StartPreparationRequest(m.Stock.Id, null), m.PreparerId);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveAsync(prep.Id,
            new SavePreparationRequest(new List<PreparationComponentInput> { new(prep.Components[0].Id, m.SaltLot.Id, 34m) }, 1000m, null,
                AutoclaveEquipmentId: autoclave.Id), m.PreparerId));
        Assert.Contains("does not require sterilization", ex.Message);
    }

    [Fact]
    public async Task Release_Approve_MakesPreparationAvailable_AndRecordsSignature()
    {
        await using var db = NewDb();
        var m = await SeedMicroAsync(db);
        var (_, clock) = NewClock(SepFirst);
        var service = TestServiceFactory.SolutionPreparation(db, clock: clock);
        var release = TestServiceFactory.SolutionPreparationRelease(db, clock);
        var stockPrep = await PrepareStockAsync(service, m);

        // Awaiting release: shown in the list, not offered to consumers.
        var listed = Assert.Single(await service.GetAllAsync(m.PreparerId, null, SolutionType.Reagent));
        Assert.Equal(ApprovalGateStatus.PendingReview, listed.ApprovalStatus);
        Assert.Empty(await service.GetAvailableAsync(m.PreparerId, m.Stock.Id, null));

        await release.DecideAsync(stockPrep.Id, new ReleaseSolutionPreparationRequest(true, " Checked ", Password), m.ReleaserId, "10.0.0.2");

        var after = await service.GetByIdAsync(stockPrep.Id, m.PreparerId);
        Assert.Equal(ApprovalGateStatus.Approved, after.ApprovalStatus);
        Assert.Equal(SolutionPreparationStatus.Prepared, after.Status);
        Assert.Equal(m.ReleaserId, after.ReleasedByUserId);
        Assert.Equal("Test Reviewer", after.ReleasedByUserName);
        Assert.Equal("Checked", after.ReleaseComment);
        Assert.Equal(SepFirst.UtcDateTime, after.ReleasedAt);
        Assert.Equal(2, after.StatusHistory.Count(h => h.ToStatus == SolutionPreparationStatus.Prepared));
        Assert.Single(await service.GetAvailableAsync(m.PreparerId, m.Stock.Id, null));

        var signature = await db.ElectronicSignatures.SingleAsync(x => x.MeaningOfSignature == SignatureMeaning.Approved);
        Assert.Equal("SolutionPreparation", signature.EntityType);
        Assert.Equal(stockPrep.Id, signature.EntityId);
        var stored = await db.SolutionPreparations.AsNoTracking().SingleAsync(p => p.Id == stockPrep.Id);
        Assert.Equal(signature.Id, stored.ReleaseSignatureId);
        Assert.Single(await db.ReviewWorkflowEvents.Where(e => e.EntityType == "SolutionPreparation" && e.EntityId == stockPrep.Id).ToListAsync());

        // A decision is final.
        var again = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            release.DecideAsync(stockPrep.Id, new ReleaseSolutionPreparationRequest(false, "Changed my mind", Password), m.ReleaserId, null));
        Assert.Contains("already been decided", again.Message);
    }

    [Fact]
    public async Task Release_Reject_NeedsComment_AndKeepsItOutOfAvailable()
    {
        await using var db = NewDb();
        var m = await SeedMicroAsync(db);
        var (_, clock) = NewClock(SepFirst);
        var service = TestServiceFactory.SolutionPreparation(db, clock: clock);
        var release = TestServiceFactory.SolutionPreparationRelease(db, clock);
        var stockPrep = await PrepareStockAsync(service, m);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            release.DecideAsync(stockPrep.Id, new ReleaseSolutionPreparationRequest(false, "  ", Password), m.ReleaserId, null));
        Assert.Contains("comment is required", ex.Message);
        Assert.Empty(await db.ElectronicSignatures.Where(x => x.MeaningOfSignature == SignatureMeaning.Rejected).ToListAsync());

        await release.DecideAsync(stockPrep.Id, new ReleaseSolutionPreparationRequest(false, "pH off", Password), m.ReleaserId, null);

        var after = await service.GetByIdAsync(stockPrep.Id, m.PreparerId);
        Assert.Equal(ApprovalGateStatus.Rejected, after.ApprovalStatus);
        Assert.Equal("pH off", after.ReleaseComment);
        Assert.Empty(await service.GetAvailableAsync(m.PreparerId, m.Stock.Id, null));
        Assert.Single(await db.ElectronicSignatures.Where(x => x.MeaningOfSignature == SignatureMeaning.Rejected).ToListAsync());
    }

    [Fact]
    public async Task Release_ByThePreparer_Throws_NothingSigned()
    {
        await using var db = NewDb();
        var m = await SeedMicroAsync(db);
        var service = TestServiceFactory.SolutionPreparation(db);
        var release = TestServiceFactory.SolutionPreparationRelease(db);
        var stockPrep = await PrepareStockAsync(service, m);
        var signaturesBefore = await db.ElectronicSignatures.CountAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            release.DecideAsync(stockPrep.Id, new ReleaseSolutionPreparationRequest(true, null, Password), m.PreparerId, null));

        Assert.Contains("you prepared", ex.Message);
        Assert.Equal(signaturesBefore, await db.ElectronicSignatures.CountAsync());
        Assert.Equal(ApprovalGateStatus.PendingReview, (await service.GetByIdAsync(stockPrep.Id, m.PreparerId)).ApprovalStatus);
    }

    [Fact]
    public async Task Release_WrongPassword_LeavesPendingReview()
    {
        await using var db = NewDb();
        var m = await SeedMicroAsync(db);
        var service = TestServiceFactory.SolutionPreparation(db);
        var release = TestServiceFactory.SolutionPreparationRelease(db);
        var stockPrep = await PrepareStockAsync(service, m);

        await Assert.ThrowsAsync<SignatureVerificationException>(() =>
            release.DecideAsync(stockPrep.Id, new ReleaseSolutionPreparationRequest(true, null, "wrong"), m.ReleaserId, null));

        Assert.Equal(ApprovalGateStatus.PendingReview, (await service.GetByIdAsync(stockPrep.Id, m.PreparerId)).ApprovalStatus);
    }

    [Fact]
    public async Task Release_NotRequired_ExpiredOrInProgress_Throws()
    {
        await using var db = NewDb();
        var m = await SeedMicroAsync(db, stockRelease: false, stockShelfLifeValue: 1, stockShelfLifeUnit: ShelfLifeUnit.Hours);
        var (fake, clock) = NewClock(SepFirst);
        var service = TestServiceFactory.SolutionPreparation(db, clock: clock);
        var release = TestServiceFactory.SolutionPreparationRelease(db, clock);
        var request = new ReleaseSolutionPreparationRequest(true, null, Password);

        var inProgress = await service.StartAsync(new StartPreparationRequest(m.Stock.Id, null), m.PreparerId);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => release.DecideAsync(inProgress.Id, request, m.ReleaserId, null));
        Assert.Contains("closed", ex.Message);

        var done = await PrepareStockAsync(service, m);
        ex = await Assert.ThrowsAsync<InvalidOperationException>(() => release.DecideAsync(done.Id, request, m.ReleaserId, null));
        Assert.Contains("does not need a release", ex.Message);

        fake.SetUtcNow(SepFirst.AddHours(2));
        ex = await Assert.ThrowsAsync<InvalidOperationException>(() => release.DecideAsync(done.Id, request, m.ReleaserId, null));
        Assert.Contains("closed", ex.Message);
    }

    [Fact]
    public async Task Release_NoReleaseRequired_PreparationIsAvailableImmediately()
    {
        await using var db = NewDb();
        var m = await SeedMicroAsync(db, stockRelease: false);
        var service = TestServiceFactory.SolutionPreparation(db);

        var done = await PrepareStockAsync(service, m);

        Assert.Null(done.ApprovalStatus);
        Assert.Single(await service.GetAvailableAsync(m.PreparerId, m.Stock.Id, null));
    }
}
