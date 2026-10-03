using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs.Responses;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

// GC on the chromatography module (spec 2026-10-03 §3.3).
public partial class HplcRunServiceTests
{
    private sealed class GcScenario
    {
        public required DocumentSection Section;
        public required int UserId;
        public required HplcMethod Method;
        public required Equipment Equipment;
        public required ChromatographyColumn Column;
        public required MaterialMasterEntry StandardEntry;
    }

    private static async Task<GcScenario> SeedGcScenarioAsync(MicroLimsDbContext db, HplcResultMode mode = HplcResultMode.ResidualSolvents)
    {
        var (section, userId) = await SeedAsync(db);
        var diluentEntry = await AddEntryAsync(db, section.Id, "DMSO");
        var standardEntry = await AddEntryAsync(db, section.Id, "STD-MEOH", MaterialMasterCategory.ReferenceStandard);
        var diluent = await AddSolutionAsync(db, section.Id, "Diluent DMSO", SolutionType.Diluent, diluentEntry);
        var rs = mode == HplcResultMode.ResidualSolvents;
        var created = await TestServiceFactory.HplcMethod(db).CreateAsync(new SaveHplcMethodRequest(
            "Residual Solvents", "RS-01", DateTime.UtcNow,
            "G43", 30000m, 0.53m, null, null, ElutionMode.Isocratic, 3.5m,
            HplcDetectorType.Fid, 1000m, 40m, diluent.Id,
            new(), new(),
            new() { rs
                ? new(null, "Methanol", null, standardEntry.Id, 0m, 0m, 3, SstMaxRsdPercent: 15m, StandardConcentrationUgPerMl: 60m)
                : new(null, "Vitamin E", null, standardEntry.Id, 50m, 50m, 3, SstMaxRsdPercent: 2m) },
            SectionId: section.Id, Technique: HplcTechnique.Gc, ResultMode: mode,
            FilmThicknessUm: 3m, CarrierGas: CarrierGas.Helium, InletTemperatureC: 140m, DetectorTemperatureC: 250m,
            OvenSteps: new() { new(null, 40m, 20m), new(10m, 240m, 20m) },
            SampleSolutionVolumeMl: rs ? 20m : null), 1);
        var method = await db.HplcMethods.FirstAsync(m => m.Id == created.Id);

        var gc = new Equipment { Name = "GC GC-01", Code = "GC-01", Type = EquipmentType.Gc, SectionId = section.Id, CalibrationDueDate = DateTime.UtcNow.AddYears(1) };
        db.Equipment.Add(gc);
        await db.SaveChangesAsync();
        var column = await AddColumnAsync(db, section.Id, gc, code: "GCOL-01", uspDesignation: "G43");

        return new GcScenario { Section = section, UserId = userId, Method = method, Equipment = gc, Column = column, StandardEntry = standardEntry };
    }

    private static StartHplcRunRequest GcStart(GcScenario g) => new(g.Equipment.Id, g.Method.Id, g.Column.Id, new());

    [Fact]
    public async Task StartRun_GcMethodOnGcInstrument_NoMobilePhases_SnapshotHasOvenSteps()
    {
        await using var db = NewDb();
        var (_, clock) = NewClock(SepFirst);
        var g = await SeedGcScenarioAsync(db);
        var service = TestServiceFactory.HplcRun(db, clock: clock);

        var run = await service.StartRunAsync(GcStart(g), g.UserId);

        Assert.Empty(run.MobilePhases);
        var stored = await db.HplcRuns.FirstAsync(r => r.Id == run.Id);
        var snapshot = JsonSerializer.Deserialize<HplcMethodResponse>(stored.MethodSnapshotJson, SnapshotJson.Options)!;
        Assert.Equal(HplcTechnique.Gc, snapshot.Technique);
        Assert.Equal(2, snapshot.OvenSteps.Count);
    }

    [Fact]
    public async Task StartRun_GcMethodOnHplcInstrument_Throws()
    {
        await using var db = NewDb();
        var (_, clock) = NewClock(SepFirst);
        var g = await SeedGcScenarioAsync(db);
        var hplc = await AddHplcEquipmentAsync(db, g.Section.Id);
        var service = TestServiceFactory.HplcRun(db, clock: clock);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.StartRunAsync(new StartHplcRunRequest(hplc.Id, g.Method.Id, g.Column.Id, new()), g.UserId));
        Assert.Equal("\"HPLC HPLC-01\" is not a GC instrument; method RS-01 needs one.", ex.Message);
    }

    [Fact]
    public async Task StartRun_HplcMethodOnGcInstrument_Throws()
    {
        await using var db = NewDb();
        var (_, clock) = NewClock(SepFirst);
        var s = await SeedScenarioAsync(db, clock);
        var gc = new Equipment { Name = "GC GC-09", Code = "GC-09", Type = EquipmentType.Gc, SectionId = s.Section.Id, CalibrationDueDate = DateTime.UtcNow.AddYears(1) };
        db.Equipment.Add(gc);
        await db.SaveChangesAsync();
        var service = TestServiceFactory.HplcRun(db, clock: clock);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.StartRunAsync(StartRequest(s) with { EquipmentId = gc.Id }, s.UserId));
        Assert.Equal("\"GC GC-09\" is not a HPLC instrument; method VIT-C needs one.", ex.Message);
    }

    [Fact]
    public async Task Instruments_AndMethodOptions_FilterByTechnique()
    {
        await using var db = NewDb();
        var (_, clock) = NewClock(SepFirst);
        var g = await SeedGcScenarioAsync(db);
        await AddHplcEquipmentAsync(db, g.Section.Id);
        var service = TestServiceFactory.HplcRun(db, clock: clock);

        var gcInstruments = await service.GetInstrumentsAsync(g.UserId, HplcTechnique.Gc);
        var hplcInstruments = await service.GetInstrumentsAsync(g.UserId);
        var gcMethods = await service.GetMethodOptionsAsync(g.UserId, HplcTechnique.Gc);
        var hplcMethods = await service.GetMethodOptionsAsync(g.UserId);

        Assert.Equal("GC-01", Assert.Single(gcInstruments).Code);
        Assert.Equal("HPLC-01", Assert.Single(hplcInstruments).Code);
        Assert.Equal(HplcResultMode.ResidualSolvents, Assert.Single(gcMethods).ResultMode);
        Assert.Empty(hplcMethods);
    }

    [Fact]
    public void OldSnapshotWithoutTechnique_ReadsAsHplc()
    {
        var snapshot = JsonSerializer.Deserialize<HplcMethodResponse>("{\"id\":1,\"name\":\"Old\",\"abbreviation\":\"OLD\",\"columnDesignation\":\"L1\"}", SnapshotJson.Options)!;
        Assert.Equal(HplcTechnique.Hplc, snapshot.Technique);
        Assert.Equal(HplcResultMode.Assay, snapshot.ResultMode);
        Assert.Empty(snapshot.OvenSteps);
    }
}
