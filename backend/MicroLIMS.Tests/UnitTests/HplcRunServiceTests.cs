using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs.Responses;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

// HPLC Workspace Part A (HPLC chain S6, plan Task A3): instruments, method
// options, StartRunAsync, GetRunAsync gates, SaveSstAsync/ConfirmSstAsync,
// evidence, abandon and history.
public partial class HplcRunServiceTests
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

    private static async Task<Material> AddReagentLotAsync(
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

    private static async Task<Material> AddStandardLotAsync(
        MicroLimsDbContext db, int sectionId, MaterialMasterEntry entry,
        decimal? purity = 99.5m, decimal? moisturePercent = 0.5m,
        decimal quantityRemaining = 100m, DateTime? expiryDate = null, string batch = "STD-LOT-01")
    {
        var lot = new Material
        {
            SectionId = sectionId,
            MaterialType = MaterialType.ReferenceStandard,
            MaterialMasterEntryId = entry.Id,
            MaterialName = entry.Name,
            ManufacturerName = "Acme",
            BatchNumber = batch,
            ReceivingDate = DateTime.UtcNow.AddDays(-10),
            ExpiryDate = expiryDate ?? DateTime.UtcNow.AddYears(1),
            Location = "Shelf 2",
            QuantityReceived = quantityRemaining,
            QuantityRemaining = quantityRemaining,
            Unit = MaterialUnit.Gram,
            Purity = purity,
            MoisturePercent = moisturePercent,
            CreatedByUserId = 1,
            LastModifiedByUserId = 1,
        };
        db.Materials.Add(lot);
        await db.SaveChangesAsync();
        return lot;
    }

    private static async Task<SolutionMaster> AddSolutionAsync(
        MicroLimsDbContext db, int sectionId, string name, SolutionType type, MaterialMasterEntry componentEntry)
    {
        var service = TestServiceFactory.SolutionMaster(db);
        var created = await service.CreateAsync(new SaveSolutionMasterRequest(
            name, type, 7, ShelfLifeUnit.Days, "Room temperature", 1000m, "Mix and filter.",
            new List<SolutionComponentInput> { new(componentEntry.Id, 500m, SolutionComponentUnit.Milliliter) },
            SectionId: sectionId), 1);

        return await db.SolutionMasters.FirstAsync(s => s.Id == created.Id);
    }

    private static async Task<HplcMethod> AddMethodAsync(
        MicroLimsDbContext db, int sectionId, string abbreviation, SolutionMaster mobilePhase, SolutionMaster diluent,
        MaterialMasterEntry standardEntry, int standardInjections = 3,
        decimal? sstMaxRsdPercent = 2.0m, decimal? sstMinResolution = null)
    {
        var service = TestServiceFactory.HplcMethod(db);
        var created = await service.CreateAsync(new SaveHplcMethodRequest(
            "Vitamin C Assay", abbreviation, DateTime.UtcNow,
            "L1", 150m, 4.6m, 5m, 30m, ElutionMode.Isocratic, 1m,
            HplcDetectorType.UV, 20m, 15m, diluent.Id,
            new List<HplcMobilePhaseInput> { new("A", mobilePhase.Id, null) },
            new List<HplcGradientStepInput>(),
            new List<HplcAnalyteInput> { new(null, "Vitamin C", 254m, standardEntry.Id, 50m, 50m, standardInjections, sstMaxRsdPercent, sstMinResolution) },
            SectionId: sectionId), 1);

        return await db.HplcMethods.FirstAsync(m => m.Id == created.Id);
    }

    private static async Task<Equipment> AddHplcEquipmentAsync(
        MicroLimsDbContext db, int sectionId, string code = "HPLC-01", DateTime? calibrationDueDate = null)
    {
        var eq = new Equipment
        {
            Name = "HPLC " + code,
            Code = code,
            Type = EquipmentType.Hplc,
            SectionId = sectionId,
            CalibrationDueDate = calibrationDueDate ?? DateTime.UtcNow.AddYears(1),
        };
        db.Equipment.Add(eq);
        await db.SaveChangesAsync();
        return eq;
    }

    private static async Task<ChromatographyColumn> AddColumnAsync(
        MicroLimsDbContext db, int sectionId, Equipment compatibleEquipment, string code = "COL-01", bool isActive = true, string? uspDesignation = "L1")
    {
        var column = new ChromatographyColumn
        {
            Code = code,
            Name = "C18",
            UspDesignation = uspDesignation,
            SectionId = sectionId,
            IsActive = isActive,
            CreatedByUserId = 1,
            LastModifiedByUserId = 1,
        };
        column.CompatibleEquipment.Add(compatibleEquipment);
        db.ChromatographyColumns.Add(column);
        await db.SaveChangesAsync();
        return column;
    }

    private static async Task<SolutionPreparationResponse> PrepareMobilePhaseAsync(
        MicroLimsDbContext db, ILabClock clock, int userId, SolutionMaster mobilePhase, HplcMethod method, Material lot)
    {
        var service = TestServiceFactory.SolutionPreparation(db, clock: clock);
        var prep = await service.StartAsync(new StartPreparationRequest(mobilePhase.Id, method.Id), userId);
        await service.SaveAsync(prep.Id,
            new SavePreparationRequest(new List<PreparationComponentInput> { new(prep.Components[0].Id, lot.Id, 5m) }, 1000m, null),
            userId);
        return await service.CompleteAsync(prep.Id, new CompletePreparationRequest(Password, null), userId, null);
    }

    private sealed class Scenario
    {
        public required DocumentSection Section;
        public required int UserId;
        public required SolutionMaster MobilePhase;
        public required MaterialMasterEntry MobileEntry;
        public required HplcMethod Method;
        public required Equipment Equipment;
        public required ChromatographyColumn Column;
        public required MaterialMasterEntry StandardEntry;
        public required SolutionPreparationResponse MobilePhasePrep;
    }

    private static async Task<Scenario> SeedScenarioAsync(
        MicroLimsDbContext db, ILabClock clock, int standardInjections = 3,
        decimal? sstMaxRsdPercent = 2.0m, decimal? sstMinResolution = null)
    {
        var (section, userId) = await SeedAsync(db);
        var mobileEntry = await AddEntryAsync(db, section.Id, "BUF-01");
        var diluentEntry = await AddEntryAsync(db, section.Id, "DIL-ENTRY");
        var standardEntry = await AddEntryAsync(db, section.Id, "STD-01", MaterialMasterCategory.ReferenceStandard);

        var diluent = await AddSolutionAsync(db, section.Id, "Diluent Water", SolutionType.Diluent, diluentEntry);
        var mobilePhase = await AddSolutionAsync(db, section.Id, "Mobile Phase C", SolutionType.MobilePhase, mobileEntry);
        var method = await AddMethodAsync(db, section.Id, "VIT-C", mobilePhase, diluent, standardEntry, standardInjections, sstMaxRsdPercent, sstMinResolution);

        var equipment = await AddHplcEquipmentAsync(db, section.Id);
        var column = await AddColumnAsync(db, section.Id, equipment);

        var reagentLot = await AddReagentLotAsync(db, section.Id, mobileEntry);
        var mobilePhasePrep = await PrepareMobilePhaseAsync(db, clock, userId, mobilePhase, method, reagentLot);

        return new Scenario
        {
            Section = section, UserId = userId, MobilePhase = mobilePhase, MobileEntry = mobileEntry, Method = method,
            Equipment = equipment, Column = column, StandardEntry = standardEntry, MobilePhasePrep = mobilePhasePrep
        };
    }

    private static StartHplcRunRequest StartRequest(Scenario s) =>
        new(s.Equipment.Id, s.Method.Id, s.Column.Id, new List<HplcMobilePhaseAssignmentInput> { new("A", s.MobilePhasePrep.Id) });

    // ---- GetInstrumentsAsync ----

    [Fact]
    public async Task Instruments_NoOpenRunNoIssues_ReportsAvailable()
    {
        await using var db = NewDb();
        var (_, clock) = NewClock(SepFirst);
        var s = await SeedScenarioAsync(db, clock);
        var service = TestServiceFactory.HplcRun(db, clock: clock);

        var instruments = await service.GetInstrumentsAsync(s.UserId);

        var instrument = Assert.Single(instruments);
        Assert.Equal("Available", instrument.State);
        Assert.Null(instrument.ActiveRun);
    }

    [Fact]
    public async Task Instruments_WithOpenRun_ReportsRunningWithSummary()
    {
        await using var db = NewDb();
        var (_, clock) = NewClock(SepFirst);
        var s = await SeedScenarioAsync(db, clock);
        var service = TestServiceFactory.HplcRun(db, clock: clock);
        var run = await service.StartRunAsync(StartRequest(s), s.UserId);

        var instruments = await service.GetInstrumentsAsync(s.UserId);

        var instrument = Assert.Single(instruments);
        Assert.Equal("Running", instrument.State);
        Assert.NotNull(instrument.ActiveRun);
        Assert.Equal(run.Code, instrument.ActiveRun!.Code);
        Assert.Equal(0, instrument.ActiveRun.SampleCount);
        Assert.Equal(HplcSstStatus.Pending, instrument.ActiveRun.SstStatus);
    }

    [Fact]
    public async Task Instruments_CalibrationOverdue_ReportsUnavailable()
    {
        await using var db = NewDb();
        var (_, clock) = NewClock(SepFirst);
        var s = await SeedScenarioAsync(db, clock);
        var eq = await db.Equipment.FirstAsync(e => e.Id == s.Equipment.Id);
        eq.CalibrationDueDate = SepFirst.UtcDateTime.AddDays(-1);
        await db.SaveChangesAsync();
        var service = TestServiceFactory.HplcRun(db, clock: clock);

        var instruments = await service.GetInstrumentsAsync(s.UserId);

        var instrument = Assert.Single(instruments);
        Assert.Equal("Unavailable", instrument.State);
        Assert.Contains("Calibration overdue", instrument.Reason);
    }

    [Fact]
    public async Task Instruments_LinkedInventoryOutOfService_ReportsUnavailable()
    {
        await using var db = NewDb();
        var (_, clock) = NewClock(SepFirst);
        var s = await SeedScenarioAsync(db, clock);
        db.EquipmentInventories.Add(new EquipmentInventory
        {
            InstrumentType = "HPLC", ManufacturerName = "Acme", Code = s.Equipment.Code, Location = "Lab",
            Status = EquipmentOperationalStatus.OutOfService, SectionId = s.Section.Id, CreatedByUserId = 1, LastModifiedByUserId = 1
        });
        await db.SaveChangesAsync();
        var service = TestServiceFactory.HplcRun(db, clock: clock);

        var instruments = await service.GetInstrumentsAsync(s.UserId);

        var instrument = Assert.Single(instruments);
        Assert.Equal("Unavailable", instrument.State);
        Assert.Contains("OutOfService", instrument.Reason);
    }

    // ---- GetMethodOptionsAsync ----

    [Fact]
    public async Task MethodOptions_OnlyActiveMethods_AreListed()
    {
        await using var db = NewDb();
        var (_, clock) = NewClock(SepFirst);
        var s = await SeedScenarioAsync(db, clock);
        var methodService = TestServiceFactory.HplcMethod(db);
        await methodService.SetActiveAsync(s.Method.Id, false, "no longer used", s.UserId);
        var service = TestServiceFactory.HplcRun(db, clock: clock);

        var options = await service.GetMethodOptionsAsync(s.UserId);

        Assert.Empty(options);
    }

    // ---- StartRunAsync ----

    [Fact]
    public async Task Start_NonHplcEquipment_Throws()
    {
        await using var db = NewDb();
        var (_, clock) = NewClock(SepFirst);
        var s = await SeedScenarioAsync(db, clock);
        var incubator = new Equipment { Name = "Incubator", Code = "INC-01", Type = EquipmentType.Incubator, SectionId = s.Section.Id };
        db.Equipment.Add(incubator);
        await db.SaveChangesAsync();
        var service = TestServiceFactory.HplcRun(db, clock: clock);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.StartRunAsync(
            new StartHplcRunRequest(incubator.Id, s.Method.Id, s.Column.Id, new List<HplcMobilePhaseAssignmentInput> { new("A", s.MobilePhasePrep.Id) }),
            s.UserId));

        Assert.Contains("not an HPLC instrument", ex.Message);
    }

    [Fact]
    public async Task Start_EquipmentWithOpenRun_Throws()
    {
        await using var db = NewDb();
        var (_, clock) = NewClock(SepFirst);
        var s = await SeedScenarioAsync(db, clock);
        var service = TestServiceFactory.HplcRun(db, clock: clock);
        await service.StartRunAsync(StartRequest(s), s.UserId);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.StartRunAsync(StartRequest(s), s.UserId));

        Assert.Contains("already has an open run", ex.Message);
    }

    [Fact]
    public async Task Start_EquipmentUnavailable_Throws()
    {
        await using var db = NewDb();
        var (_, clock) = NewClock(SepFirst);
        var s = await SeedScenarioAsync(db, clock);
        var eq = await db.Equipment.FirstAsync(e => e.Id == s.Equipment.Id);
        eq.CalibrationDueDate = SepFirst.UtcDateTime.AddDays(-1);
        await db.SaveChangesAsync();
        var service = TestServiceFactory.HplcRun(db, clock: clock);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.StartRunAsync(StartRequest(s), s.UserId));

        Assert.Contains("Calibration overdue", ex.Message);
    }

    [Fact]
    public async Task Start_InactiveMethod_Throws()
    {
        await using var db = NewDb();
        var (_, clock) = NewClock(SepFirst);
        var s = await SeedScenarioAsync(db, clock);
        await TestServiceFactory.HplcMethod(db).SetActiveAsync(s.Method.Id, false, "retired", s.UserId);
        var service = TestServiceFactory.HplcRun(db, clock: clock);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.StartRunAsync(StartRequest(s), s.UserId));

        Assert.Contains("inactive", ex.Message);
    }

    [Fact]
    public async Task Start_InactiveColumn_Throws()
    {
        await using var db = NewDb();
        var (_, clock) = NewClock(SepFirst);
        var s = await SeedScenarioAsync(db, clock);
        var column = await db.ChromatographyColumns.FirstAsync(c => c.Id == s.Column.Id);
        column.IsActive = false;
        await db.SaveChangesAsync();
        var service = TestServiceFactory.HplcRun(db, clock: clock);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.StartRunAsync(StartRequest(s), s.UserId));

        Assert.Contains("inactive", ex.Message);
    }

    [Fact]
    public async Task Start_IncompatibleColumn_Throws()
    {
        await using var db = NewDb();
        var (_, clock) = NewClock(SepFirst);
        var s = await SeedScenarioAsync(db, clock);
        var otherEquipment = await AddHplcEquipmentAsync(db, s.Section.Id, "HPLC-02");
        var incompatibleColumn = await AddColumnAsync(db, s.Section.Id, otherEquipment, "COL-02");
        var service = TestServiceFactory.HplcRun(db, clock: clock);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.StartRunAsync(
            new StartHplcRunRequest(s.Equipment.Id, s.Method.Id, incompatibleColumn.Id, new List<HplcMobilePhaseAssignmentInput> { new("A", s.MobilePhasePrep.Id) }),
            s.UserId));

        Assert.Contains("not compatible", ex.Message);
    }

    [Fact]
    public async Task Start_MissingMobilePhaseForChannel_Throws()
    {
        await using var db = NewDb();
        var (_, clock) = NewClock(SepFirst);
        var s = await SeedScenarioAsync(db, clock);
        var service = TestServiceFactory.HplcRun(db, clock: clock);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.StartRunAsync(
            new StartHplcRunRequest(s.Equipment.Id, s.Method.Id, s.Column.Id, new List<HplcMobilePhaseAssignmentInput>()),
            s.UserId));

        Assert.Contains("every channel", ex.Message);
    }

    [Fact]
    public async Task Start_MobilePhaseWrongType_Throws()
    {
        await using var db = NewDb();
        var (_, clock) = NewClock(SepFirst);
        var s = await SeedScenarioAsync(db, clock);
        var diluentEntry = await AddEntryAsync(db, s.Section.Id, "DIL-COMP");
        var diluentLot = await AddReagentLotAsync(db, s.Section.Id, diluentEntry, batch: "DIL-LOT");
        var diluentSolution = await AddSolutionAsync(db, s.Section.Id, "Diluent Buffer", SolutionType.Diluent, diluentEntry);
        var diluentPrepService = TestServiceFactory.SolutionPreparation(db, clock: clock);
        var diluentPrep = await diluentPrepService.StartAsync(new StartPreparationRequest(diluentSolution.Id, null), s.UserId);
        await diluentPrepService.SaveAsync(diluentPrep.Id,
            new SavePreparationRequest(new List<PreparationComponentInput> { new(diluentPrep.Components[0].Id, diluentLot.Id, 5m) }, 1000m, null), s.UserId);
        var completedDiluent = await diluentPrepService.CompleteAsync(diluentPrep.Id, new CompletePreparationRequest(Password, null), s.UserId, null);
        var service = TestServiceFactory.HplcRun(db, clock: clock);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.StartRunAsync(
            new StartHplcRunRequest(s.Equipment.Id, s.Method.Id, s.Column.Id, new List<HplcMobilePhaseAssignmentInput> { new("A", completedDiluent.Id) }),
            s.UserId));

        Assert.Contains("not a mobile phase", ex.Message);
    }

    [Fact]
    public async Task Start_MobilePhaseWrongMethod_Throws()
    {
        await using var db = NewDb();
        var (_, clock) = NewClock(SepFirst);
        var s = await SeedScenarioAsync(db, clock);
        var otherDiluentEntry = await AddEntryAsync(db, s.Section.Id, "DIL-ENTRY-2");
        var otherDiluent = await AddSolutionAsync(db, s.Section.Id, "Diluent Water 2", SolutionType.Diluent, otherDiluentEntry);
        var otherMethod = await AddMethodAsync(db, s.Section.Id, "VIT-D", s.MobilePhase, otherDiluent, s.StandardEntry);
        var reagentLot2 = await AddReagentLotAsync(db, s.Section.Id, s.MobileEntry, batch: "LOT-02");
        var otherPrep = await PrepareMobilePhaseAsync(db, clock, s.UserId, s.MobilePhase, otherMethod, reagentLot2);
        var service = TestServiceFactory.HplcRun(db, clock: clock);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.StartRunAsync(
            new StartHplcRunRequest(s.Equipment.Id, s.Method.Id, s.Column.Id, new List<HplcMobilePhaseAssignmentInput> { new("A", otherPrep.Id) }),
            s.UserId));

        Assert.Contains("not prepared for this method", ex.Message);
    }

    [Fact]
    public async Task Start_MobilePhaseExpired_Throws()
    {
        await using var db = NewDb();
        var (fake, clock) = NewClock(SepFirst);
        var s = await SeedScenarioAsync(db, clock);
        fake.SetUtcNow(SepFirst.AddDays(30)); // past the 7-day shelf life
        var service = TestServiceFactory.HplcRun(db, clock: clock);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.StartRunAsync(StartRequest(s), s.UserId));

        Assert.Contains("not currently prepared", ex.Message);
    }

    [Fact]
    public async Task Start_Success_SnapshotsMethodAndCreatesPendingSstWithAnalyteRows()
    {
        await using var db = NewDb();
        var (_, clock) = NewClock(SepFirst);
        var s = await SeedScenarioAsync(db, clock);
        var service = TestServiceFactory.HplcRun(db, clock: clock);

        var run = await service.StartRunAsync(StartRequest(s), s.UserId);

        Assert.Equal(HplcRunStatus.Open, run.Status);
        Assert.Equal("VIT-C RUN 01/092026", run.Code);
        Assert.NotNull(run.Sst);
        Assert.Equal("VIT-C S.S 01/092026", run.Sst!.Code);
        Assert.Equal(HplcSstStatus.Pending, run.Sst.Status);
        var analyte = Assert.Single(run.Sst.Analytes);
        Assert.Equal("Vitamin C", analyte.AnalyteName);
        Assert.Empty(analyte.Injections);
        Assert.Single(run.MobilePhases);
        Assert.Equal(s.MobilePhasePrep.Id, run.MobilePhases[0].SolutionPreparationId);
    }

    // ---- GetRunAsync gates ----

    [Fact]
    public async Task GetRun_CanConfirmSst_TrueWhilePending_FalseAfterConfirmed()
    {
        await using var db = NewDb();
        var (_, clock) = NewClock(SepFirst);
        var s = await SeedScenarioAsync(db, clock);
        var service = TestServiceFactory.HplcRun(db, clock: clock);
        var run = await service.StartRunAsync(StartRequest(s), s.UserId);

        Assert.False(run.CanConfirmSst);
        await service.UploadEvidenceAsync(run.Id, null, HplcEvidenceContext.Sst, HplcEvidenceKind.StandardReport, "report.pdf", "application/pdf", PdfBytes(), s.UserId);
        run = await service.GetRunAsync(run.Id, s.UserId);

        Assert.True(run.CanConfirmSst);
        Assert.False(run.CanAssignSamples);
        Assert.Equal("Sample assignment is locked until system suitability passes.", run.CanAssignSamplesReason);
    }

    // ---- SaveSstAsync ----

    [Fact]
    public async Task SaveSst_ExpiredStandardLot_Throws()
    {
        await using var db = NewDb();
        var (_, clock) = NewClock(SepFirst);
        var s = await SeedScenarioAsync(db, clock);
        var expiredLot = await AddStandardLotAsync(db, s.Section.Id, s.StandardEntry, expiryDate: SepFirst.UtcDateTime.AddDays(-1), batch: "EXPIRED");
        var service = TestServiceFactory.HplcRun(db, clock: clock);
        var run = await service.StartRunAsync(StartRequest(s), s.UserId);
        var analyteId = run.Sst!.Analytes[0].Id;

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveSstAsync(run.Id,
            new SaveSstRequest(new List<SaveSstAnalyteInput> { new(analyteId, expiredLot.Id, 50m, new List<decimal> { 1000m, 1001m, 1002m }, null, null, null, null, null, null, null) }),
            s.UserId));

        Assert.Contains("Expired", ex.Message);
    }

    [Fact]
    public async Task SaveSst_LotMissingMoisturePercent_Throws()
    {
        await using var db = NewDb();
        var (_, clock) = NewClock(SepFirst);
        var s = await SeedScenarioAsync(db, clock);
        var lotNoMoisture = await AddStandardLotAsync(db, s.Section.Id, s.StandardEntry, moisturePercent: null, batch: "NO-MC");
        var service = TestServiceFactory.HplcRun(db, clock: clock);
        var run = await service.StartRunAsync(StartRequest(s), s.UserId);
        var analyteId = run.Sst!.Analytes[0].Id;

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveSstAsync(run.Id,
            new SaveSstRequest(new List<SaveSstAnalyteInput> { new(analyteId, lotNoMoisture.Id, 50m, new List<decimal> { 1000m, 1001m, 1002m }, null, null, null, null, null, null, null) }),
            s.UserId));

        Assert.Contains("no moisture content recorded", ex.Message);
    }

    [Fact]
    public async Task SaveSst_WeightNotPositive_Throws()
    {
        await using var db = NewDb();
        var (_, clock) = NewClock(SepFirst);
        var s = await SeedScenarioAsync(db, clock);
        var lot = await AddStandardLotAsync(db, s.Section.Id, s.StandardEntry);
        var service = TestServiceFactory.HplcRun(db, clock: clock);
        var run = await service.StartRunAsync(StartRequest(s), s.UserId);
        var analyteId = run.Sst!.Analytes[0].Id;

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveSstAsync(run.Id,
            new SaveSstRequest(new List<SaveSstAnalyteInput> { new(analyteId, lot.Id, 0m, new List<decimal> { 1000m, 1001m, 1002m }, null, null, null, null, null, null, null) }),
            s.UserId));

        Assert.Contains("greater than zero", ex.Message);
    }

    [Fact]
    public async Task SaveSst_SnapshotsPurityAndMoistureFromLot_AndStaysPending()
    {
        await using var db = NewDb();
        var (_, clock) = NewClock(SepFirst);
        var s = await SeedScenarioAsync(db, clock);
        var lot = await AddStandardLotAsync(db, s.Section.Id, s.StandardEntry, purity: 99.1m, moisturePercent: 0.3m);
        var service = TestServiceFactory.HplcRun(db, clock: clock);
        var run = await service.StartRunAsync(StartRequest(s), s.UserId);
        var analyteId = run.Sst!.Analytes[0].Id;

        var saved = await service.SaveSstAsync(run.Id,
            new SaveSstRequest(new List<SaveSstAnalyteInput> { new(analyteId, lot.Id, 50m, new List<decimal> { 1000m, 1001m, 1002m }, null, null, null, null, null, null, null) }),
            s.UserId);

        var analyte = Assert.Single(saved.Sst!.Analytes);
        Assert.Equal(99.1m, analyte.StandardPurityPercent);
        Assert.Equal(0.3m, analyte.StandardMoisturePercent);
        Assert.Equal(3, analyte.Injections.Count);
        Assert.Equal(HplcSstStatus.Pending, saved.Sst.Status);
    }

    [Fact]
    public async Task SaveSst_AfterConfirmed_Throws()
    {
        await using var db = NewDb();
        var (_, clock) = NewClock(SepFirst);
        var s = await SeedScenarioAsync(db, clock, standardInjections: 3, sstMaxRsdPercent: null);
        var lot = await AddStandardLotAsync(db, s.Section.Id, s.StandardEntry);
        var service = TestServiceFactory.HplcRun(db, clock: clock);
        var run = await service.StartRunAsync(StartRequest(s), s.UserId);
        var analyteId = run.Sst!.Analytes[0].Id;
        await service.SaveSstAsync(run.Id, new SaveSstRequest(new List<SaveSstAnalyteInput> {
            new(analyteId, lot.Id, 50m, new List<decimal> { 1000m, 1000m, 1000m }, null, null, null, null, null, null, null) }), s.UserId);
        await service.UploadEvidenceAsync(run.Id, null, HplcEvidenceContext.Sst, HplcEvidenceKind.StandardReport, "report.pdf", "application/pdf", PdfBytes(), s.UserId);
        await service.ConfirmSstAsync(run.Id, new ConfirmSstRequest(Password, null), s.UserId, null);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveSstAsync(run.Id,
            new SaveSstRequest(new List<SaveSstAnalyteInput> { new(analyteId, lot.Id, 50m, new List<decimal> { 1000m, 1000m, 1000m }, null, null, null, null, null, null, null) }),
            s.UserId));

        Assert.Contains("already been confirmed", ex.Message);
    }

    // ---- Evidence ----

    private static byte[] PdfBytes() => new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34 }; // "%PDF-1.4"

    [Fact]
    public async Task UploadEvidence_DisallowedContentType_Throws()
    {
        await using var db = NewDb();
        var (_, clock) = NewClock(SepFirst);
        var s = await SeedScenarioAsync(db, clock);
        var service = TestServiceFactory.HplcRun(db, clock: clock);
        var run = await service.StartRunAsync(StartRequest(s), s.UserId);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.UploadEvidenceAsync(
            run.Id, null, HplcEvidenceContext.Sst, HplcEvidenceKind.StandardReport, "report.exe", "application/x-msdownload", new byte[] { 1, 2, 3 }, s.UserId));

        Assert.Contains("not supported", ex.Message);
    }

    [Fact]
    public async Task UploadEvidence_TooLarge_Throws()
    {
        await using var db = NewDb();
        var (_, clock) = NewClock(SepFirst);
        var s = await SeedScenarioAsync(db, clock);
        var service = TestServiceFactory.HplcRun(db, clock: clock);
        var run = await service.StartRunAsync(StartRequest(s), s.UserId);
        var oversized = new byte[26_214_401];

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.UploadEvidenceAsync(
            run.Id, null, HplcEvidenceContext.Sst, HplcEvidenceKind.StandardReport, "report.pdf", "application/pdf", oversized, s.UserId));

        Assert.Contains("maximum allowed size", ex.Message);
    }

    [Fact]
    public async Task UploadEvidence_Success_IsRetrievableAndCurrent()
    {
        await using var db = NewDb();
        var (_, clock) = NewClock(SepFirst);
        var s = await SeedScenarioAsync(db, clock);
        var service = TestServiceFactory.HplcRun(db, clock: clock);
        var run = await service.StartRunAsync(StartRequest(s), s.UserId);

        var evidence = await service.UploadEvidenceAsync(
            run.Id, null, HplcEvidenceContext.Sst, HplcEvidenceKind.StandardReport, "report.pdf", "application/pdf", PdfBytes(), s.UserId);

        Assert.True(evidence.IsCurrent);
        var (content, contentType, fileName) = await service.DownloadEvidenceAsync(evidence.Id, s.UserId);
        Assert.Equal(PdfBytes(), content);
        Assert.Equal("application/pdf", contentType);
        Assert.Equal("report.pdf", fileName);
    }

    [Fact]
    public async Task SupersedeEvidence_MarksOldNotCurrent_NewIsCurrent()
    {
        await using var db = NewDb();
        var (_, clock) = NewClock(SepFirst);
        var s = await SeedScenarioAsync(db, clock);
        var service = TestServiceFactory.HplcRun(db, clock: clock);
        var run = await service.StartRunAsync(StartRequest(s), s.UserId);
        var original = await service.UploadEvidenceAsync(
            run.Id, null, HplcEvidenceContext.Sst, HplcEvidenceKind.StandardReport, "report.pdf", "application/pdf", PdfBytes(), s.UserId);

        var replacement = await service.SupersedeEvidenceAsync(original.Id, "Wrong scan", "report2.pdf", "application/pdf", PdfBytes(), s.UserId);

        Assert.True(replacement.IsCurrent);
        var reloadedRun = await service.GetRunAsync(run.Id, s.UserId);
        var oldRow = reloadedRun.Evidence.Single(e => e.Id == original.Id);
        Assert.False(oldRow.IsCurrent);
        Assert.Equal("Wrong scan", oldRow.SupersedeReason);
    }

    // ---- ConfirmSstAsync ----

    [Fact]
    public async Task ConfirmSst_MissingStandardReportEvidence_Throws()
    {
        await using var db = NewDb();
        var (_, clock) = NewClock(SepFirst);
        var s = await SeedScenarioAsync(db, clock, standardInjections: 3, sstMaxRsdPercent: null);
        var lot = await AddStandardLotAsync(db, s.Section.Id, s.StandardEntry);
        var service = TestServiceFactory.HplcRun(db, clock: clock);
        var run = await service.StartRunAsync(StartRequest(s), s.UserId);
        var analyteId = run.Sst!.Analytes[0].Id;
        await service.SaveSstAsync(run.Id, new SaveSstRequest(new List<SaveSstAnalyteInput> {
            new(analyteId, lot.Id, 50m, new List<decimal> { 1000m, 1000m, 1000m }, null, null, null, null, null, null, null) }), s.UserId);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ConfirmSstAsync(run.Id, new ConfirmSstRequest(Password, null), s.UserId, null));

        Assert.Contains("standard report", ex.Message);
    }

    // Review Focus line 2: a mobile-phase preparation that expires after it
    // was selected for the run - ConfirmSstAsync re-checks and refuses.
    [Fact]
    public async Task ConfirmSst_MobilePhaseExpiredSinceSelected_Throws()
    {
        await using var db = NewDb();
        var (fake, clock) = NewClock(SepFirst);
        var s = await SeedScenarioAsync(db, clock, standardInjections: 3, sstMaxRsdPercent: null);
        var lot = await AddStandardLotAsync(db, s.Section.Id, s.StandardEntry);
        var service = TestServiceFactory.HplcRun(db, clock: clock);
        var run = await service.StartRunAsync(StartRequest(s), s.UserId);
        var analyteId = run.Sst!.Analytes[0].Id;
        await service.SaveSstAsync(run.Id, new SaveSstRequest(new List<SaveSstAnalyteInput> {
            new(analyteId, lot.Id, 50m, new List<decimal> { 1000m, 1000m, 1000m }, null, null, null, null, null, null, null) }), s.UserId);
        await service.UploadEvidenceAsync(run.Id, null, HplcEvidenceContext.Sst, HplcEvidenceKind.StandardReport, "report.pdf", "application/pdf", PdfBytes(), s.UserId);

        // The mobile phase preparation (7-day shelf life) expires after selection but before confirmation.
        fake.SetUtcNow(SepFirst.AddDays(10));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ConfirmSstAsync(run.Id, new ConfirmSstRequest(Password, null), s.UserId, null));

        Assert.Contains("expired since it was selected", ex.Message);
    }

    [Fact]
    public async Task ConfirmSst_AllCriteriaMet_PassesAndSigns()
    {
        await using var db = NewDb();
        var (_, clock) = NewClock(SepFirst);
        var s = await SeedScenarioAsync(db, clock, standardInjections: 3, sstMaxRsdPercent: 2.0m);
        var lot = await AddStandardLotAsync(db, s.Section.Id, s.StandardEntry);
        var service = TestServiceFactory.HplcRun(db, clock: clock);
        var run = await service.StartRunAsync(StartRequest(s), s.UserId);
        var analyteId = run.Sst!.Analytes[0].Id;
        await service.SaveSstAsync(run.Id, new SaveSstRequest(new List<SaveSstAnalyteInput> {
            new(analyteId, lot.Id, 50m, new List<decimal> { 1000m, 1001m, 999m }, null, null, null, null, null, null, null) }), s.UserId);
        await service.UploadEvidenceAsync(run.Id, null, HplcEvidenceContext.Sst, HplcEvidenceKind.StandardReport, "report.pdf", "application/pdf", PdfBytes(), s.UserId);

        var confirmed = await service.ConfirmSstAsync(run.Id, new ConfirmSstRequest(Password, "Looks fine"), s.UserId, "127.0.0.1");

        Assert.Equal(HplcSstStatus.Passed, confirmed.Sst!.Status);
        Assert.True(confirmed.Sst.Analytes[0].Passed);
        Assert.NotNull(confirmed.Sst.ConfirmedAt);
        Assert.Equal(s.UserId, confirmed.Sst.ConfirmedByUserId);
        Assert.True(confirmed.CanAssignSamples);
        Assert.False(confirmed.CanConfirmSst);
    }

    [Fact]
    public async Task ConfirmSst_CriterionNotMet_Fails()
    {
        await using var db = NewDb();
        var (_, clock) = NewClock(SepFirst);
        // Very tight RSD gate that the entered responses cannot meet.
        var s = await SeedScenarioAsync(db, clock, standardInjections: 3, sstMaxRsdPercent: 0.01m);
        var lot = await AddStandardLotAsync(db, s.Section.Id, s.StandardEntry);
        var service = TestServiceFactory.HplcRun(db, clock: clock);
        var run = await service.StartRunAsync(StartRequest(s), s.UserId);
        var analyteId = run.Sst!.Analytes[0].Id;
        await service.SaveSstAsync(run.Id, new SaveSstRequest(new List<SaveSstAnalyteInput> {
            new(analyteId, lot.Id, 50m, new List<decimal> { 1000m, 1050m, 950m }, null, null, null, null, null, null, null) }), s.UserId);
        await service.UploadEvidenceAsync(run.Id, null, HplcEvidenceContext.Sst, HplcEvidenceKind.StandardReport, "report.pdf", "application/pdf", PdfBytes(), s.UserId);

        var confirmed = await service.ConfirmSstAsync(run.Id, new ConfirmSstRequest(Password, null), s.UserId, null);

        Assert.Equal(HplcSstStatus.Failed, confirmed.Sst!.Status);
        Assert.False(confirmed.Sst.Analytes[0].Passed);
        Assert.NotNull(confirmed.Sst.FailureReasons);
        Assert.False(confirmed.CanAssignSamples);
    }

    [Fact]
    public async Task ConfirmSst_WrongPassword_LeavesPendingAndUnmutated()
    {
        await using var db = NewDb();
        var (_, clock) = NewClock(SepFirst);
        var s = await SeedScenarioAsync(db, clock, standardInjections: 3, sstMaxRsdPercent: null);
        var lot = await AddStandardLotAsync(db, s.Section.Id, s.StandardEntry);
        var service = TestServiceFactory.HplcRun(db, clock: clock);
        var run = await service.StartRunAsync(StartRequest(s), s.UserId);
        var analyteId = run.Sst!.Analytes[0].Id;
        await service.SaveSstAsync(run.Id, new SaveSstRequest(new List<SaveSstAnalyteInput> {
            new(analyteId, lot.Id, 50m, new List<decimal> { 1000m, 1000m, 1000m }, null, null, null, null, null, null, null) }), s.UserId);
        await service.UploadEvidenceAsync(run.Id, null, HplcEvidenceContext.Sst, HplcEvidenceKind.StandardReport, "report.pdf", "application/pdf", PdfBytes(), s.UserId);

        await Assert.ThrowsAsync<SignatureVerificationException>(
            () => service.ConfirmSstAsync(run.Id, new ConfirmSstRequest("WrongPassword!", null), s.UserId, null));

        var reloaded = await service.GetRunAsync(run.Id, s.UserId);
        Assert.Equal(HplcSstStatus.Pending, reloaded.Sst!.Status);
        Assert.Null(reloaded.Sst.ConfirmedAt);
    }

    // ---- AbandonRunAsync ----

    [Fact]
    public async Task Abandon_RequiresReason_ThenClosesRun()
    {
        await using var db = NewDb();
        var (_, clock) = NewClock(SepFirst);
        var s = await SeedScenarioAsync(db, clock);
        var service = TestServiceFactory.HplcRun(db, clock: clock);
        var run = await service.StartRunAsync(StartRequest(s), s.UserId);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.AbandonRunAsync(run.Id, "", s.UserId));

        var abandoned = await service.AbandonRunAsync(run.Id, "Instrument malfunction.", s.UserId);
        Assert.Equal(HplcRunStatus.Abandoned, abandoned.Status);
        Assert.Equal("Instrument malfunction.", abandoned.CloseReason);
        Assert.NotNull(abandoned.ClosedAt);
    }

    [Fact]
    public async Task Abandon_AlreadyClosed_Throws()
    {
        await using var db = NewDb();
        var (_, clock) = NewClock(SepFirst);
        var s = await SeedScenarioAsync(db, clock);
        var service = TestServiceFactory.HplcRun(db, clock: clock);
        var run = await service.StartRunAsync(StartRequest(s), s.UserId);
        await service.AbandonRunAsync(run.Id, "First reason", s.UserId);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.AbandonRunAsync(run.Id, "Second reason", s.UserId));

        Assert.Contains("closed", ex.Message);
    }

    // ---- GetRunHistoryAsync ----

    [Fact]
    public async Task History_ListsRunsForEquipment_NewestFirst()
    {
        await using var db = NewDb();
        var (fake, clock) = NewClock(SepFirst);
        var s = await SeedScenarioAsync(db, clock);
        var service = TestServiceFactory.HplcRun(db, clock: clock);
        var run1 = await service.StartRunAsync(StartRequest(s), s.UserId);
        await service.AbandonRunAsync(run1.Id, "closing first", s.UserId);

        fake.SetUtcNow(SepFirst.AddHours(1));
        var lot = await AddReagentLotAsync(db, s.Section.Id, s.MobileEntry, batch: "LOT-03");
        var prep2 = await PrepareMobilePhaseAsync(db, clock, s.UserId, s.MobilePhase, s.Method, lot);
        var run2 = await service.StartRunAsync(
            new StartHplcRunRequest(s.Equipment.Id, s.Method.Id, s.Column.Id, new List<HplcMobilePhaseAssignmentInput> { new("A", prep2.Id) }),
            s.UserId);

        var history = await service.GetRunHistoryAsync(s.Equipment.Id, s.UserId);

        Assert.Equal(2, history.Count);
        Assert.Equal(run2.Id, history[0].Id);
        Assert.Equal(run1.Id, history[1].Id);
        Assert.Equal(HplcRunStatus.Abandoned, history[1].Status);
    }
}
