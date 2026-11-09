using MicroLIMS.Shared.Exceptions;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

// ICP run + calibration. The run carries no method: the analyst lists the calibrated elements and their r.
public class IcpRunServiceTests
{
    private const string Password = "ValidPassword123!";
    internal static readonly DateTimeOffset SepFirst = new(2026, 9, 1, 10, 0, 0, TimeSpan.Zero);

    private sealed class FakeTime : TimeProvider
    {
        private readonly DateTimeOffset _now;
        public FakeTime(DateTimeOffset now) => _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
    }

    internal static ILabClock NewClock() => new LabClock(new FakeTime(SepFirst), LabClock.ResolveTimeZone("Africa/Cairo"));

    private static MicroLimsDbContext NewDb()
    {
        var db = new MicroLimsDbContext(new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.CurrentUserId = 1;
        return db;
    }

    internal sealed class Scenario
    {
        public required MicroLimsDbContext Db;
        public required DocumentSection Section;
        public required int UserId;
        public required Equipment Equipment;
        public required Material CalLot;
        public required IcpRunService Service;
    }

    internal static async Task<Material> AddLotAsync(MicroLimsDbContext db, int sectionId, string code = "ICP-CAL", string batch = "LOT-1",
        MaterialType type = MaterialType.ReferenceStandard)
    {
        var entry = new MaterialMasterEntry
        {
            SectionId = sectionId, Code = code, Name = code, Category = MaterialMasterCategory.ReferenceStandard,
            BaseUnit = MaterialUnit.Milliliter, IsActive = true,
            CreatedByUserId = 1, CreatedAt = DateTime.UtcNow, LastModifiedByUserId = 1, LastModifiedAt = DateTime.UtcNow,
        };
        db.MaterialMasterEntries.Add(entry);
        await db.SaveChangesAsync();
        var lot = new Material
        {
            SectionId = sectionId, MaterialType = type, MaterialMasterEntryId = entry.Id,
            MaterialName = entry.Name, ManufacturerName = "Acme", BatchNumber = batch,
            ReceivingDate = DateTime.UtcNow.AddDays(-10), ExpiryDate = DateTime.UtcNow.AddYears(1), Location = "Shelf",
            QuantityReceived = 100m, QuantityRemaining = 100m, Unit = MaterialUnit.Milliliter,
            Purity = 99.5m, MoisturePercent = 0m, CreatedByUserId = 1, LastModifiedByUserId = 1,
        };
        db.Materials.Add(lot);
        await db.SaveChangesAsync();
        return lot;
    }

    internal static async Task<Scenario> SeedAsync()
    {
        var db = NewDb();
        var section = TestServiceFactory.EnsureMicroSection(db);
        var role = new Role { Name = "Analyst", Type = RoleType.Analyst, IsActive = true };
        db.Roles.Add(role);
        await db.SaveChangesAsync();
        var user = new User
        {
            Username = "u_" + Guid.NewGuid().ToString("N")[..6], FullName = "Test Analyst", RoleId = role.Id,
            IsActive = true, PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password),
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        TestServiceFactory.AssignUserToMicroSection(db, user.Id);

        var equipment = new Equipment
        {
            Name = "ICP-OES 01", Code = "ICP-01", Type = EquipmentType.IcpOes, SectionId = section.Id,
            CalibrationDueDate = DateTime.UtcNow.AddYears(1),
        };
        db.Equipment.Add(equipment);
        await db.SaveChangesAsync();

        var calLot = await AddLotAsync(db, section.Id);
        return new Scenario
        {
            Db = db, Section = section, UserId = user.Id, Equipment = equipment,
            CalLot = calLot, Service = TestServiceFactory.IcpRun(db, clock: NewClock()),
        };
    }

    private static byte[] PdfBytes() => new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D };

    internal static Task<IcpRunDto> StartAsync(Scenario s) =>
        s.Service.StartRunAsync(new StartIcpRunRequest(s.Equipment.Id), s.UserId);

    internal static async Task UploadCalibrationReportAsync(Scenario s, int runId) =>
        await s.Service.UploadEvidenceAsync(runId, null, IcpEvidenceContext.Calibration, IcpEvidenceKind.CalibrationReport,
            "cal.pdf", "application/pdf", new MemoryStream(PdfBytes()), s.UserId);

    // Zn fails at r 0.9989 < 0.999 by default, Ca passes.
    internal static SaveIcpCalibrationRequest Save(int? lotId, decimal znR = 0.9989m, decimal caR = 0.9995m) => new(
        lotId, new List<SaveIcpCalibrationElementInput> { new("Zn", znR), new("Ca", caR) });

    internal static SaveIcpCalibrationRequest Save(int? lotId, params (string Symbol, decimal? R)[] elements) => new(
        lotId, elements.Select(e => new SaveIcpCalibrationElementInput(e.Symbol, e.R)).ToList());

    [Fact]
    public async Task StartRun_CreatesPendingCalibrationWithNoElements()
    {
        var s = await SeedAsync();
        var run = await StartAsync(s);

        Assert.Equal(IcpRunStatus.Open, run.Status);
        Assert.Equal("ICP RUN 01/092026", run.Code);
        Assert.Equal("ICP CAL 01/092026", run.Calibration.Code);
        Assert.Equal(IcpCalibrationStatus.Pending, run.Calibration.Status);
        Assert.Empty(run.Calibration.Elements);
        Assert.Equal(0.999m, run.Calibration.MinCorrelation);
        Assert.False(run.CanConfirmCalibration);

        var instruments = await s.Service.GetInstrumentsAsync(s.UserId);
        Assert.Equal("Running", instruments.Single().State);
        Assert.Equal(run.Id, instruments.Single().ActiveRun!.RunId);
    }

    [Fact]
    public async Task StartRun_NonIcpInstrument_Throws()
    {
        var s = await SeedAsync();
        var hplc = new Equipment { Name = "HPLC 01", Code = "HPLC-01", Type = EquipmentType.Hplc, SectionId = s.Section.Id, CalibrationDueDate = DateTime.UtcNow.AddYears(1) };
        s.Db.Equipment.Add(hplc);
        await s.Db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            s.Service.StartRunAsync(new StartIcpRunRequest(hplc.Id), s.UserId));
        Assert.Equal("\"HPLC 01\" is not an ICP-OES instrument.", ex.Message);
    }

    [Fact]
    public async Task StartRun_SecondOpenRun_Throws()
    {
        var s = await SeedAsync();
        await StartAsync(s);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => StartAsync(s));
        Assert.Equal("\"ICP-OES 01\" already has an open run.", ex.Message);
    }

    [Fact]
    public async Task SaveCalibration_AnalystListsElements_ServerComputesVerdicts()
    {
        var s = await SeedAsync();
        var run = await StartAsync(s);

        var saved = await s.Service.SaveCalibrationAsync(run.Id, Save(s.CalLot.Id), s.UserId);

        var zn = saved.Calibration.Elements.Single(e => e.Symbol == "Zn");
        var ca = saved.Calibration.Elements.Single(e => e.Symbol == "Ca");
        Assert.False(zn.Passed);
        Assert.Contains("r 0.9989 is below the minimum 0.999", zn.FailureReasons);
        Assert.True(ca.Passed);
        Assert.Equal(s.CalLot.Id, saved.Calibration.CalibrationStandardMaterialId);
        Assert.Equal(IcpCalibrationStatus.Pending, saved.Calibration.Status);
    }

    [Fact]
    public async Task SaveCalibration_BoundaryIsInclusive_AndSymbolIsNormalised()
    {
        var s = await SeedAsync();
        var run = await StartAsync(s);

        var saved = await s.Service.SaveCalibrationAsync(run.Id, Save(s.CalLot.Id, ("zn", 0.999m)), s.UserId);

        var zn = Assert.Single(saved.Calibration.Elements);
        Assert.Equal("Zn", zn.Symbol);
        Assert.True(zn.Passed);
    }

    [Fact]
    public async Task SaveCalibration_ReplacesTheElementList()
    {
        var s = await SeedAsync();
        var run = await StartAsync(s);
        await s.Service.SaveCalibrationAsync(run.Id, Save(s.CalLot.Id), s.UserId);

        var saved = await s.Service.SaveCalibrationAsync(run.Id, Save(s.CalLot.Id, ("Ca", 0.9996m), ("Mg", 0.9999m)), s.UserId);

        Assert.Equal(new[] { "Ca", "Mg" }, saved.Calibration.Elements.Select(e => e.Symbol).OrderBy(x => x).ToArray());
        Assert.Equal(0.9996m, saved.Calibration.Elements.Single(e => e.Symbol == "Ca").CorrelationR);
    }

    [Fact]
    public async Task SaveCalibration_MissingR_FailsTheElement()
    {
        var s = await SeedAsync();
        var run = await StartAsync(s);

        var saved = await s.Service.SaveCalibrationAsync(run.Id, Save(s.CalLot.Id, ("Zn", null)), s.UserId);

        Assert.False(saved.Calibration.Elements.Single().Passed);
        Assert.Equal("r is required.", saved.Calibration.Elements.Single().FailureReasons);
    }

    [Fact]
    public async Task SaveCalibration_DuplicateElement_Throws()
    {
        var s = await SeedAsync();
        var run = await StartAsync(s);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            s.Service.SaveCalibrationAsync(run.Id, Save(s.CalLot.Id, ("Zn", 0.9995m), ("ZN", 0.9996m)), s.UserId));
        Assert.Equal("Element \"Zn\" is listed more than once.", ex.Message);
    }

    [Fact]
    public async Task SaveCalibration_BadSymbol_Throws()
    {
        var s = await SeedAsync();
        var run = await StartAsync(s);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            s.Service.SaveCalibrationAsync(run.Id, Save(s.CalLot.Id, ("Zn1", 0.9995m)), s.UserId));
        Assert.Equal("Element symbol must be 1-3 letters.", ex.Message);
    }

    [Fact]
    public async Task SaveCalibration_CorrelationOutOfRange_Throws()
    {
        var s = await SeedAsync();
        var run = await StartAsync(s);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            s.Service.SaveCalibrationAsync(run.Id, Save(s.CalLot.Id, ("Zn", 1.2m)), s.UserId));
        Assert.Equal("Zn: r must be between 0 and 1.", ex.Message);
    }

    [Fact]
    public async Task SaveCalibration_RoundsCorrelationToSixDecimalsBeforeJudging()
    {
        var s = await SeedAsync();
        var run = await StartAsync(s);

        // 0.9989996 rounds to 0.999000 (numeric(8,6)), which is the minimum and passes.
        var saved = await s.Service.SaveCalibrationAsync(run.Id, Save(s.CalLot.Id, ("Zn", 0.9989996m)), s.UserId);

        var zn = saved.Calibration.Elements.Single();
        Assert.Equal(0.999m, zn.CorrelationR);
        Assert.True(zn.Passed);
    }

    [Fact]
    public async Task SaveCalibration_LotThatIsNotAReferenceStandard_Throws()
    {
        var s = await SeedAsync();
        var chemical = await AddLotAsync(s.Db, s.Section.Id, "CHEM", "LOT-C", MaterialType.Chemical);
        var run = await StartAsync(s);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            s.Service.SaveCalibrationAsync(run.Id, Save(chemical.Id), s.UserId));
        Assert.Equal("The calibration standard must be a usable reference standard lot of this laboratory.", ex.Message);
    }

    [Fact]
    public async Task ConfirmCalibration_WithoutReport_Throws()
    {
        var s = await SeedAsync();
        var run = await StartAsync(s);
        await s.Service.SaveCalibrationAsync(run.Id, Save(s.CalLot.Id), s.UserId);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            s.Service.ConfirmCalibrationAsync(run.Id, new ConfirmIcpCalibrationRequest(Password, null), s.UserId, null));
        Assert.Equal("Upload the Syngistix calibration report (the calibration curve) before confirming.", ex.Message);
    }

    [Fact]
    public async Task ConfirmCalibration_WithoutStandardLot_Throws()
    {
        var s = await SeedAsync();
        var run = await StartAsync(s);
        await UploadCalibrationReportAsync(s, run.Id);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            s.Service.ConfirmCalibrationAsync(run.Id, new ConfirmIcpCalibrationRequest(Password, null), s.UserId, null));
        Assert.Equal("Choose the calibration standard lot.", ex.Message);
    }

    [Fact]
    public async Task ConfirmCalibration_WithoutElements_Throws()
    {
        var s = await SeedAsync();
        var run = await StartAsync(s);
        await s.Service.SaveCalibrationAsync(run.Id, Save(s.CalLot.Id, Array.Empty<(string, decimal?)>()), s.UserId);
        await UploadCalibrationReportAsync(s, run.Id);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            s.Service.ConfirmCalibrationAsync(run.Id, new ConfirmIcpCalibrationRequest(Password, null), s.UserId, null));
        Assert.Equal("Add the elements calibrated on this run, with their r.", ex.Message);
    }

    [Fact]
    public async Task ConfirmCalibration_NoElementPasses_Throws_InstrumentNotReady()
    {
        var s = await SeedAsync();
        var run = await StartAsync(s);
        await s.Service.SaveCalibrationAsync(run.Id, Save(s.CalLot.Id, znR: 0.9980m, caR: 0.9970m), s.UserId);
        await UploadCalibrationReportAsync(s, run.Id);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            s.Service.ConfirmCalibrationAsync(run.Id, new ConfirmIcpCalibrationRequest(Password, null), s.UserId, null));
        Assert.Equal("No element passes the calibration check (r), so the instrument is not ready for testing.", ex.Message);
    }

    [Fact]
    public async Task ConfirmCalibration_Signs_FreezesVerdicts_AndSetsExpiry()
    {
        var s = await SeedAsync();
        var run = await StartAsync(s);
        await s.Service.SaveCalibrationAsync(run.Id, Save(s.CalLot.Id), s.UserId);
        await UploadCalibrationReportAsync(s, run.Id);

        var confirmed = await s.Service.ConfirmCalibrationAsync(run.Id, new ConfirmIcpCalibrationRequest(Password, "ok"), s.UserId, "1.2.3.4");

        Assert.Equal(IcpCalibrationStatus.Confirmed, confirmed.Calibration.Status);
        Assert.Equal(SepFirst.UtcDateTime, confirmed.Calibration.ConfirmedAt);
        Assert.Equal(SepFirst.UtcDateTime.AddHours(24), confirmed.Calibration.ExpiresAt);
        Assert.Equal(s.UserId, confirmed.Calibration.ConfirmedByUserId);
        Assert.Single(s.Db.ElectronicSignatures.Where(x => x.EntityType == nameof(IcpCalibration)));
        Assert.False(confirmed.Calibration.Elements.Single(e => e.Symbol == "Zn").Passed);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            s.Service.SaveCalibrationAsync(run.Id, Save(s.CalLot.Id), s.UserId));
        Assert.Equal("The calibration has already been confirmed.", ex.Message);
    }

    [Fact]
    public async Task ConfirmCalibration_WrongPassword_LeavesCalibrationPending()
    {
        var s = await SeedAsync();
        var run = await StartAsync(s);
        await s.Service.SaveCalibrationAsync(run.Id, Save(s.CalLot.Id), s.UserId);
        await UploadCalibrationReportAsync(s, run.Id);

        await Assert.ThrowsAnyAsync<InvalidOperationException>(() =>
            s.Service.ConfirmCalibrationAsync(run.Id, new ConfirmIcpCalibrationRequest("wrong", null), s.UserId, null));

        Assert.Equal(IcpCalibrationStatus.Pending, (await s.Service.GetRunAsync(run.Id, s.UserId)).Calibration.Status);
    }

    [Fact]
    public async Task ConfirmCalibration_LotExpiredAfterSave_ThrowsAndStaysPending()
    {
        var s = await SeedAsync();
        var run = await StartAsync(s);
        await s.Service.SaveCalibrationAsync(run.Id, Save(s.CalLot.Id), s.UserId);
        await UploadCalibrationReportAsync(s, run.Id);
        s.CalLot.ExpiryDate = DateTime.UtcNow.AddDays(-400);
        await s.Db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            s.Service.ConfirmCalibrationAsync(run.Id, new ConfirmIcpCalibrationRequest(Password, null), s.UserId, null));
        Assert.Equal("The calibration standard must be a usable reference standard lot of this laboratory.", ex.Message);
        Assert.Equal(IcpCalibrationStatus.Pending, (await s.Service.GetRunAsync(run.Id, s.UserId)).Calibration.Status);
    }

    [Fact]
    public async Task AbandonRun_RequiresReason_AndFreesTheInstrument()
    {
        var s = await SeedAsync();
        var run = await StartAsync(s);

        await Assert.ThrowsAsync<BusinessRuleException>(() => s.Service.AbandonRunAsync(run.Id, "  ", s.UserId));
        var abandoned = await s.Service.AbandonRunAsync(run.Id, "Torch failure", s.UserId);

        Assert.Equal(IcpRunStatus.Abandoned, abandoned.Status);
        Assert.Equal("Torch failure", abandoned.CloseReason);
        Assert.Equal("Available", (await s.Service.GetInstrumentsAsync(s.UserId)).Single().State);
        await StartAsync(s); // a new run can start
    }

    [Fact]
    public async Task Evidence_SupersedeKeepsOld_AndCalibrationReportGateUsesTheCurrentOne()
    {
        var s = await SeedAsync();
        var run = await StartAsync(s);
        await UploadCalibrationReportAsync(s, run.Id);
        var first = (await s.Service.GetRunAsync(run.Id, s.UserId)).Evidence.Single();

        var replacement = await s.Service.SupersedeEvidenceAsync(first.Id, "Wrong curve", "cal2.pdf", "application/pdf",
            new MemoryStream(PdfBytes()), s.UserId);

        var evidence = (await s.Service.GetRunAsync(run.Id, s.UserId)).Evidence;
        Assert.Equal(2, evidence.Count);
        Assert.False(evidence.Single(e => e.Id == first.Id).IsCurrent);
        Assert.True(evidence.Single(e => e.Id == replacement.Id).IsCurrent);
        Assert.Equal("Wrong curve", evidence.Single(e => e.Id == first.Id).SupersedeReason);
    }

    [Fact]
    public async Task Evidence_ContextKindPairs_Enforced()
    {
        var s = await SeedAsync();
        var run = await StartAsync(s);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            s.Service.UploadEvidenceAsync(run.Id, null, IcpEvidenceContext.Calibration, IcpEvidenceKind.SampleReport,
                "x.pdf", "application/pdf", new MemoryStream(PdfBytes()), s.UserId));
        Assert.Equal("SampleReport evidence can't be attached to the Calibration context.", ex.Message);

        var ex2 = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            s.Service.UploadEvidenceAsync(run.Id, null, IcpEvidenceContext.Sample, IcpEvidenceKind.SampleReport,
                "x.pdf", "application/pdf", new MemoryStream(PdfBytes()), s.UserId));
        Assert.Equal("Choose the sample this evidence belongs to.", ex2.Message);
    }
}
