using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

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
        public required int MethodId;
        public required Equipment Equipment;
        public required MaterialMasterEntry CalEntry;
        public required Material CalLot;
        public required IcpRunService Service;
    }

    private static async Task<MaterialMasterEntry> AddEntryAsync(MicroLimsDbContext db, int sectionId, string code)
    {
        var entry = new MaterialMasterEntry
        {
            SectionId = sectionId, Code = code, Name = code, Category = MaterialMasterCategory.ReferenceStandard,
            BaseUnit = MaterialUnit.Milliliter, IsActive = true,
            CreatedByUserId = 1, CreatedAt = DateTime.UtcNow, LastModifiedByUserId = 1, LastModifiedAt = DateTime.UtcNow,
        };
        db.MaterialMasterEntries.Add(entry);
        await db.SaveChangesAsync();
        return entry;
    }

    private static async Task<Material> AddLotAsync(MicroLimsDbContext db, int sectionId, MaterialMasterEntry entry, string batch = "LOT-1")
    {
        var lot = new Material
        {
            SectionId = sectionId, MaterialType = MaterialType.ReferenceStandard, MaterialMasterEntryId = entry.Id,
            MaterialName = entry.Name, ManufacturerName = "Acme", BatchNumber = batch,
            ReceivingDate = DateTime.UtcNow.AddDays(-10), ExpiryDate = DateTime.UtcNow.AddYears(1), Location = "Shelf",
            QuantityReceived = 100m, QuantityRemaining = 100m, Unit = MaterialUnit.Milliliter,
            Purity = 99.5m, MoisturePercent = 0m, CreatedByUserId = 1, LastModifiedByUserId = 1,
        };
        db.Materials.Add(lot);
        await db.SaveChangesAsync();
        return lot;
    }

    internal static async Task<Scenario> SeedAsync(bool requireIcv = false, bool requireCcv = false)
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

        var calEntry = await AddEntryAsync(db, section.Id, "ICP-CAL");
        var icvEntry = await AddEntryAsync(db, section.Id, "ICP-ICV");
        var req = new SaveIcpMethodRequest(
            "Minerals by ICP-OES", "MIN-ICP", new DateTime(2026, 8, 1), IcpMethodMode.MineralAssay,
            "0.1, 0.5, 1, 3, 6", calEntry.Id, 0.999m, 50m,
            new() { new IcpElementInput(null, "Zn", 213.857m, AnalyteView.Axial), new IcpElementInput(null, "Ca", 317.933m, AnalyteView.Radial) },
            RequireCcv: requireCcv, CcvNominalMgPerL: requireCcv ? 1m : null, CcvRecoveryLowPercent: requireCcv ? 90m : null, CcvRecoveryHighPercent: requireCcv ? 110m : null,
            RequireIcv: requireIcv, IcvStandardEntryId: requireIcv ? icvEntry.Id : null,
            IcvNominalMgPerL: requireIcv ? 1m : null, IcvRecoveryLowPercent: requireIcv ? 90m : null, IcvRecoveryHighPercent: requireIcv ? 110m : null,
            SectionId: section.Id);
        var method = await TestServiceFactory.IcpMethod(db).CreateAsync(req, user.Id);

        var equipment = new Equipment
        {
            Name = "ICP-OES 01", Code = "ICP-01", Type = EquipmentType.IcpOes, SectionId = section.Id,
            CalibrationDueDate = DateTime.UtcNow.AddYears(1),
        };
        db.Equipment.Add(equipment);
        await db.SaveChangesAsync();

        var calLot = await AddLotAsync(db, section.Id, calEntry);
        return new Scenario
        {
            Db = db, Section = section, UserId = user.Id, MethodId = method.Id, Equipment = equipment,
            CalEntry = calEntry, CalLot = calLot, Service = TestServiceFactory.IcpRun(db, clock: NewClock()),
        };
    }

    private static byte[] PdfBytes() => new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D };

    internal static Task<IcpRunDto> StartAsync(Scenario s) =>
        s.Service.StartRunAsync(new StartIcpRunRequest(s.Equipment.Id, s.MethodId), s.UserId);

    internal static async Task UploadCalibrationReportAsync(Scenario s, int runId) =>
        await s.Service.UploadEvidenceAsync(runId, null, IcpEvidenceContext.Calibration, IcpEvidenceKind.CalibrationReport,
            "cal.pdf", "application/pdf", new MemoryStream(PdfBytes()), s.UserId);

    // Zn fails at r 0.9989 < 0.999, Ca passes.
    internal static SaveIcpCalibrationRequest Save(IcpRunDto run, int? lotId, int? icvLotId = null, decimal znR = 0.9989m, decimal caR = 0.9995m) => new(
        lotId, icvLotId, run.Calibration.Elements.Select(e =>
            new SaveIcpCalibrationElementInput(e.Id, e.Symbol == "Zn" ? znR : caR, null, null)).ToList());

    [Fact]
    public async Task StartRun_CreatesPendingCalibrationWithElements()
    {
        var s = await SeedAsync();
        var run = await StartAsync(s);

        Assert.Equal(IcpRunStatus.Open, run.Status);
        Assert.Equal("MIN-ICP RUN 01/092026", run.Code);
        Assert.Equal("MIN-ICP CAL 01/092026", run.Calibration.Code);
        Assert.Equal(IcpCalibrationStatus.Pending, run.Calibration.Status);
        Assert.Equal(new[] { "Zn", "Ca" }, run.Calibration.Elements.Select(e => e.Symbol).ToArray());
        Assert.Equal("MIN-ICP", run.Method.Abbreviation);
        Assert.Equal("ICP-CAL", run.Method.CalibrationStandardEntryCode);
        Assert.False(run.CanConfirmCalibration);
        Assert.Empty(run.CcvReadings);

        var instruments = await s.Service.GetInstrumentsAsync(s.UserId);
        Assert.Equal("Running", instruments.Single().State);
        Assert.Equal(run.Id, instruments.Single().ActiveRun!.RunId);
        Assert.Single(await s.Service.GetMethodOptionsAsync(s.UserId));
    }

    [Fact]
    public async Task StartRun_NonIcpInstrument_Throws()
    {
        var s = await SeedAsync();
        var hplc = new Equipment { Name = "HPLC 01", Code = "HPLC-01", Type = EquipmentType.Hplc, SectionId = s.Section.Id, CalibrationDueDate = DateTime.UtcNow.AddYears(1) };
        s.Db.Equipment.Add(hplc);
        await s.Db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            s.Service.StartRunAsync(new StartIcpRunRequest(hplc.Id, s.MethodId), s.UserId));
        Assert.Equal("\"HPLC 01\" is not an ICP-OES instrument.", ex.Message);
    }

    [Fact]
    public async Task StartRun_SecondOpenRun_Throws()
    {
        var s = await SeedAsync();
        await StartAsync(s);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => StartAsync(s));
        Assert.Equal("\"ICP-OES 01\" already has an open run.", ex.Message);
    }

    [Fact]
    public async Task SaveCalibration_ComputesVerdicts()
    {
        var s = await SeedAsync();
        var run = await StartAsync(s);

        var saved = await s.Service.SaveCalibrationAsync(run.Id, Save(run, s.CalLot.Id), s.UserId);

        var zn = saved.Calibration.Elements.Single(e => e.Symbol == "Zn");
        var ca = saved.Calibration.Elements.Single(e => e.Symbol == "Ca");
        Assert.False(zn.Passed);
        Assert.Contains("r 0.9989 is below the minimum 0.999", zn.FailureReasons);
        Assert.True(ca.Passed);
        Assert.Equal(s.CalLot.Id, saved.Calibration.CalibrationStandardMaterialId);
        Assert.Equal(IcpCalibrationStatus.Pending, saved.Calibration.Status);
    }

    [Fact]
    public async Task SaveCalibration_LotOfAnotherEntry_Throws()
    {
        var s = await SeedAsync();
        var other = await AddLotAsync(s.Db, s.Section.Id, await AddEntryAsync(s.Db, s.Section.Id, "OTHER"));
        var run = await StartAsync(s);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            s.Service.SaveCalibrationAsync(run.Id, Save(run, other.Id), s.UserId));
        Assert.Equal("The calibration standard lot must be a usable lot of ICP-CAL.", ex.Message);
    }

    [Fact]
    public async Task SaveCalibration_IcvLotWhenMethodDoesNotRequireIcv_Throws()
    {
        var s = await SeedAsync();
        var run = await StartAsync(s);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            s.Service.SaveCalibrationAsync(run.Id, Save(run, s.CalLot.Id, s.CalLot.Id), s.UserId));
        Assert.Equal("The ICV standard lot is only used when the method requires ICV.", ex.Message);
    }

    [Fact]
    public async Task ConfirmCalibration_WithoutReport_Throws()
    {
        var s = await SeedAsync();
        var run = await StartAsync(s);
        await s.Service.SaveCalibrationAsync(run.Id, Save(run, s.CalLot.Id), s.UserId);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            s.Service.ConfirmCalibrationAsync(run.Id, new ConfirmIcpCalibrationRequest(Password, null), s.UserId, null));
        Assert.Equal("Upload the Syngistix calibration report before confirming.", ex.Message);
    }

    [Fact]
    public async Task ConfirmCalibration_WithoutStandardLot_Throws()
    {
        var s = await SeedAsync();
        var run = await StartAsync(s);
        await UploadCalibrationReportAsync(s, run.Id);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            s.Service.ConfirmCalibrationAsync(run.Id, new ConfirmIcpCalibrationRequest(Password, null), s.UserId, null));
        Assert.Equal("Choose the calibration standard lot.", ex.Message);
    }

    [Fact]
    public async Task ConfirmCalibration_IcvRequiredWithoutIcvLot_Throws()
    {
        var s = await SeedAsync(requireIcv: true);
        var run = await StartAsync(s);
        await s.Service.SaveCalibrationAsync(run.Id, Save(run, s.CalLot.Id), s.UserId);
        await UploadCalibrationReportAsync(s, run.Id);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            s.Service.ConfirmCalibrationAsync(run.Id, new ConfirmIcpCalibrationRequest(Password, null), s.UserId, null));
        Assert.Equal("Choose the ICV standard lot.", ex.Message);
    }

    [Fact]
    public async Task ConfirmCalibration_Signs_FreezesVerdicts_AndSetsExpiry()
    {
        var s = await SeedAsync();
        var run = await StartAsync(s);
        await s.Service.SaveCalibrationAsync(run.Id, Save(run, s.CalLot.Id), s.UserId);
        await UploadCalibrationReportAsync(s, run.Id);

        var confirmed = await s.Service.ConfirmCalibrationAsync(run.Id, new ConfirmIcpCalibrationRequest(Password, "ok"), s.UserId, "1.2.3.4");

        Assert.Equal(IcpCalibrationStatus.Confirmed, confirmed.Calibration.Status);
        Assert.Equal(SepFirst.UtcDateTime, confirmed.Calibration.ConfirmedAt);
        Assert.Equal(SepFirst.UtcDateTime.AddHours(24), confirmed.Calibration.ExpiresAt);
        Assert.Equal(s.UserId, confirmed.Calibration.ConfirmedByUserId);
        Assert.Single(s.Db.ElectronicSignatures.Where(x => x.EntityType == nameof(IcpCalibration)));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            s.Service.SaveCalibrationAsync(run.Id, Save(run, s.CalLot.Id), s.UserId));
        Assert.Equal("The calibration has already been confirmed.", ex.Message);
    }

    [Fact]
    public async Task ConfirmCalibration_WrongPassword_LeavesCalibrationPending()
    {
        var s = await SeedAsync();
        var run = await StartAsync(s);
        await s.Service.SaveCalibrationAsync(run.Id, Save(run, s.CalLot.Id), s.UserId);
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
        await s.Service.SaveCalibrationAsync(run.Id, Save(run, s.CalLot.Id), s.UserId);
        await UploadCalibrationReportAsync(s, run.Id);
        s.CalLot.ExpiryDate = DateTime.UtcNow.AddDays(-400);
        await s.Db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            s.Service.ConfirmCalibrationAsync(run.Id, new ConfirmIcpCalibrationRequest(Password, null), s.UserId, null));
        Assert.Equal("The calibration standard lot must be a usable lot of ICP-CAL.", ex.Message);
        Assert.Equal(IcpCalibrationStatus.Pending, (await s.Service.GetRunAsync(run.Id, s.UserId)).Calibration.Status);
        Assert.Empty(s.Db.ElectronicSignatures.Where(x => x.EntityType == nameof(IcpCalibration)));
    }

    [Fact]
    public async Task ConfirmCalibration_AllFail_RunBlocksAssignment()
    {
        var s = await SeedAsync();
        var run = await StartAsync(s);
        await s.Service.SaveCalibrationAsync(run.Id, Save(run, s.CalLot.Id, znR: 0.9980m, caR: 0.9970m), s.UserId);
        await UploadCalibrationReportAsync(s, run.Id);

        var confirmed = await s.Service.ConfirmCalibrationAsync(run.Id, new ConfirmIcpCalibrationRequest(Password, null), s.UserId, null);

        Assert.Equal(IcpCalibrationStatus.Confirmed, confirmed.Calibration.Status);
        Assert.All(confirmed.Calibration.Elements, e => Assert.False(e.Passed));
        Assert.DoesNotContain(confirmed.Calibration.Elements, e => e.Passed); // no valid element: Task 6 assignment gate keys off this
    }

    [Fact]
    public async Task ConfirmCalibration_Twice_Throws()
    {
        var s = await SeedAsync();
        var run = await StartAsync(s);
        await s.Service.SaveCalibrationAsync(run.Id, Save(run, s.CalLot.Id), s.UserId);
        await UploadCalibrationReportAsync(s, run.Id);
        await s.Service.ConfirmCalibrationAsync(run.Id, new ConfirmIcpCalibrationRequest(Password, null), s.UserId, null);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            s.Service.ConfirmCalibrationAsync(run.Id, new ConfirmIcpCalibrationRequest(Password, null), s.UserId, null));
        Assert.Equal("The calibration has already been confirmed.", ex.Message);
    }

    [Fact]
    public async Task AbandonRun_RequiresReason()
    {
        var s = await SeedAsync();
        var run = await StartAsync(s);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => s.Service.AbandonRunAsync(run.Id, "  ", s.UserId));
        Assert.Equal("A reason is required.", ex.Message);

        var abandoned = await s.Service.AbandonRunAsync(run.Id, "Torch out", s.UserId);
        Assert.Equal(IcpRunStatus.Abandoned, abandoned.Status);
        Assert.Equal("Torch out", abandoned.CloseReason);
        Assert.Equal("Available", (await s.Service.GetInstrumentsAsync(s.UserId)).Single().State);
        Assert.Equal(run.Id, (await s.Service.GetRunHistoryAsync(s.Equipment.Id, s.UserId)).Single().Id);
    }

    [Fact]
    public async Task Evidence_SupersedeKeepsOld()
    {
        var s = await SeedAsync();
        var run = await StartAsync(s);
        var first = await s.Service.UploadEvidenceAsync(run.Id, null, IcpEvidenceContext.Calibration, IcpEvidenceKind.CalibrationReport,
            "cal.pdf", "application/pdf", new MemoryStream(PdfBytes()), s.UserId);

        var second = await s.Service.SupersedeEvidenceAsync(first.Id, "wrong file", "cal2.pdf", "application/pdf", new MemoryStream(PdfBytes()), s.UserId);

        var detail = await s.Service.GetRunAsync(run.Id, s.UserId);
        Assert.Equal(2, detail.Evidence.Count);
        Assert.False(detail.Evidence.Single(e => e.Id == first.Id).IsCurrent);
        Assert.Equal("wrong file", detail.Evidence.Single(e => e.Id == first.Id).SupersedeReason);
        Assert.True(detail.Evidence.Single(e => e.Id == second.Id).IsCurrent);
        var (content, _, name) = await s.Service.DownloadEvidenceAsync(first.Id, s.UserId);
        Assert.Equal(PdfBytes(), content);
        Assert.Equal("cal.pdf", name);
    }

    [Fact]
    public async Task SaveCalibration_CorrelationOutOfRange_Throws()
    {
        var s = await SeedAsync();
        var run = await StartAsync(s);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            s.Service.SaveCalibrationAsync(run.Id, Save(run, s.CalLot.Id, znR: 9.99m), s.UserId));
        Assert.Equal("r must be between 0 and 1.", ex.Message);
    }

    [Fact]
    public async Task SaveCalibration_RoundsCorrelationToSixDecimalsBeforeJudging()
    {
        var s = await SeedAsync();
        var run = await StartAsync(s);

        var saved = await s.Service.SaveCalibrationAsync(run.Id, Save(run, s.CalLot.Id, znR: 0.9989996m), s.UserId);

        var zn = saved.Calibration.Elements.Single(e => e.Symbol == "Zn");
        Assert.Equal(0.999000m, zn.CorrelationR);
        Assert.True(zn.Passed); // 0.999000 meets the minimum 0.999; unrounded 0.9989996 would not
    }

    [Fact]
    public async Task Evidence_ContextKindPairs_Enforced()
    {
        var s = await SeedAsync();
        var run = await StartAsync(s);
        Task<IcpEvidenceDto> Up(IcpEvidenceContext c, IcpEvidenceKind k) =>
            s.Service.UploadEvidenceAsync(run.Id, null, c, k, "x.pdf", "application/pdf", new MemoryStream(PdfBytes()), s.UserId);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Up(IcpEvidenceContext.Calibration, IcpEvidenceKind.SampleReport));
        Assert.Equal("SampleReport evidence can't be attached to the Calibration context.", ex.Message);
        ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Up(IcpEvidenceContext.Sample, IcpEvidenceKind.CalibrationReport));
        Assert.Equal("CalibrationReport evidence can't be attached to the Sample context.", ex.Message);
        ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Up(IcpEvidenceContext.Run, IcpEvidenceKind.CalibrationReport));
        Assert.Equal("CalibrationReport evidence can't be attached to the Run context.", ex.Message);

        Assert.Equal(IcpEvidenceKind.Other, (await Up(IcpEvidenceContext.Run, IcpEvidenceKind.Other)).Kind);
    }

    [Fact]
    public async Task ConfirmCalibration_GateRequiresCalibrationReportKind()
    {
        var s = await SeedAsync();
        var run = await StartAsync(s);
        await s.Service.SaveCalibrationAsync(run.Id, Save(run, s.CalLot.Id, znR: 0.9995m), s.UserId);
        // A wrong-kind row (as legacy data could hold) must not satisfy the gate.
        s.Db.IcpEvidences.Add(new IcpEvidence
        {
            IcpRunId = run.Id, Context = IcpEvidenceContext.Calibration, Kind = IcpEvidenceKind.Other, FilePath = "p",
            FileName = "x.pdf", ContentType = "application/pdf", UploadedByUserId = s.UserId, UploadedAt = SepFirst.UtcDateTime,
        });
        await s.Db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            s.Service.ConfirmCalibrationAsync(run.Id, new ConfirmIcpCalibrationRequest(Password, null), s.UserId, null));
        Assert.Equal("Upload the Syngistix calibration report before confirming.", ex.Message);
    }

    [Fact]
    public async Task Evidence_SampleGateRequiresSampleReportKind_AndRemovedSampleRefused()
    {
        var (f, run, rsId) = await IcpRunSampleTests.AssignedAsync();
        MemoryStream Pdf() => new(PdfBytes());
        await f.S.Service.UploadEvidenceAsync(run.Id, rsId, IcpEvidenceContext.Sample, IcpEvidenceKind.Other, "o.pdf", "application/pdf", Pdf(), f.S.UserId);
        await IcpRunSampleTests.SaveAsync(f, rsId, IcpRunSampleTests.Rep(f, 0.5m, 0.8m));
        Assert.Equal("Upload the sample result report before sending for review.",
            (await f.S.Service.GetSampleEntryAsync(rsId, f.S.UserId)).CanSubmitReason);

        await f.S.Service.RemoveSampleAsync(run.Id, rsId, "Wrong sample", f.S.UserId);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            f.S.Service.UploadEvidenceAsync(run.Id, rsId, IcpEvidenceContext.Sample, IcpEvidenceKind.SampleReport, "s.pdf", "application/pdf", Pdf(), f.S.UserId));
        Assert.Equal("Evidence can't be added to a removed sample.", ex.Message);
    }

    [Fact]
    public async Task Evidence_SampleContextNeedsRunSample()
    {
        var s = await SeedAsync();
        var run = await StartAsync(s);

        await Assert.ThrowsAsync<InvalidOperationException>(() => s.Service.UploadEvidenceAsync(
            run.Id, null, IcpEvidenceContext.Sample, IcpEvidenceKind.SampleReport, "s.pdf", "application/pdf", new MemoryStream(PdfBytes()), s.UserId));
        await Assert.ThrowsAsync<MicroLIMS.Shared.Exceptions.NotFoundException>(() => s.Service.UploadEvidenceAsync(
            run.Id, 999, IcpEvidenceContext.Sample, IcpEvidenceKind.SampleReport, "s.pdf", "application/pdf", new MemoryStream(PdfBytes()), s.UserId));
    }
}
