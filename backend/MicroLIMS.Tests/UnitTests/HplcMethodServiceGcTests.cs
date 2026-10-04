using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

// GC on the chromatography module (spec 2026-10-03 §3.1). Shares helpers with HplcMethodServiceTests.
public partial class HplcMethodServiceTests
{
    private static HplcAnalyteInput SolventInput(int standardEntryId, string name = "Methanol", int? id = null, decimal? conc = 60m) =>
        new(id, name, null, standardEntryId, 0m, 0m, 6, SstMaxRsdPercent: 15m, StandardConcentrationUgPerMl: conc);

    private static SaveHplcMethodRequest GcReq(
        int diluentId, List<HplcAnalyteInput> analytes,
        HplcResultMode mode = HplcResultMode.ResidualSolvents, List<GcOvenStepInput>? oven = null,
        HplcDetectorType detector = HplcDetectorType.Fid, decimal? sampleVolume = 20m,
        string abbreviation = "RS-01", bool headspace = true, string? reason = null) =>
        new("Residual Solvents", abbreviation, DateTime.UtcNow,
            "G43", 30000m, 0.53m, null, null, ElutionMode.Isocratic, 3.5m,
            detector, 1000m, 40m, diluentId,
            new List<HplcMobilePhaseInput>(), new List<HplcGradientStepInput>(), analytes,
            Reason: reason,
            Technique: HplcTechnique.Gc, ResultMode: mode, FilmThicknessUm: 3m, CarrierGas: CarrierGas.Helium,
            SplitRatio: 5m, InletTemperatureC: 140m, DetectorTemperatureC: 250m,
            OvenSteps: oven ?? new List<GcOvenStepInput> { new(null, 40m, 20m), new(10m, 240m, 20m) },
            HeadspaceEnabled: headspace,
            HeadspaceEquilibrationTemperatureC: headspace ? 80m : null,
            HeadspaceEquilibrationMin: headspace ? 60m : null,
            HeadspaceTransferLineTemperatureC: headspace ? 105m : null,
            SampleSolutionVolumeMl: mode == HplcResultMode.ResidualSolvents ? sampleVolume : null);

    [Fact]
    public async Task CreateGc_ResidualSolvents_SavesGcFieldsAndOvenSteps()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var (diluent, _, standard) = await SeedBasicsAsync(db, section.Id);
        var service = TestServiceFactory.HplcMethod(db);

        var result = await service.CreateAsync(GcReq(diluent.Id, new() { SolventInput(standard.Id) }), userId);

        Assert.Equal(HplcTechnique.Gc, result.Technique);
        Assert.Equal(HplcResultMode.ResidualSolvents, result.ResultMode);
        Assert.Equal(CarrierGas.Helium, result.CarrierGas);
        Assert.Equal(2, result.OvenSteps.Count);
        Assert.Null(result.OvenSteps[0].RateCPerMin);
        Assert.Equal(2, result.OvenSteps[1].StepNo);
        Assert.Empty(result.MobilePhases);
        Assert.Equal(60m, result.Analytes[0].StandardConcentrationUgPerMl);
        Assert.Null(result.Analytes[0].WavelengthNm);
        Assert.Equal(20m, result.SampleSolutionVolumeMl);
    }

    [Fact]
    public async Task CreateGc_Assay_RequiresThWtAndRejectsStandardConcentration()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var (diluent, _, standard) = await SeedBasicsAsync(db, section.Id);
        var service = TestServiceFactory.HplcMethod(db);

        var ok = await service.CreateAsync(GcReq(diluent.Id,
            new() { new(null, "Vitamin E", null, standard.Id, 50m, 50m, 5) }, mode: HplcResultMode.Assay, abbreviation: "VE-GC"), userId);
        Assert.Equal(HplcResultMode.Assay, ok.ResultMode);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(GcReq(diluent.Id,
            new() { new(null, "Vitamin E", null, standard.Id, 50m, 50m, 5, StandardConcentrationUgPerMl: 10m) },
            mode: HplcResultMode.Assay, abbreviation: "VE-GC2"), userId));
        Assert.Equal("Standard concentration and sample solution volume are only used for residual solvents.", ex.Message);
    }

    [Theory]
    [InlineData("film")]
    [InlineData("gas")]
    [InlineData("detector")]
    [InlineData("mobile")]
    [InlineData("oven-empty")]
    [InlineData("oven-first-rate")]
    [InlineData("oven-ramp")]
    [InlineData("headspace")]
    [InlineData("wavelength")]
    [InlineData("volume")]
    [InlineData("conc")]
    public async Task CreateGc_InvalidField_Throws(string field)
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var (diluent, mp, standard) = await SeedBasicsAsync(db, section.Id);
        var service = TestServiceFactory.HplcMethod(db);
        var r = GcReq(diluent.Id, new() { SolventInput(standard.Id) });

        (SaveHplcMethodRequest req, string message) = field switch
        {
            "film" => (r with { FilmThicknessUm = null }, "Film thickness is required for a GC method."),
            "gas" => (r with { CarrierGas = null }, "Carrier gas is required for a GC method."),
            "detector" => (r with { DetectorType = HplcDetectorType.UV }, "Detector UV is not a GC detector."),
            "mobile" => (r with { MobilePhases = new() { new("A", mp.Id, null) } }, "Mobile phases, gradient, particle size, column temperature and equilibration are HPLC-only."),
            "oven-empty" => (r with { OvenSteps = new() }, "A GC method needs at least one oven program step."),
            "oven-first-rate" => (r with { OvenSteps = new() { new(10m, 40m, 5m) } }, "The first oven step is the initial temperature and has no ramp rate."),
            "oven-ramp" => (r with { OvenSteps = new() { new(null, 40m, 5m), new(0m, 200m, 5m) } }, "Oven step 2: the ramp rate must be greater than zero."),
            "headspace" => (r with { HeadspaceEquilibrationMin = null }, "Headspace temperatures and equilibration time are required when headspace is on."),
            "wavelength" => (r with { Analytes = new() { SolventInput(standard.Id) with { WavelengthNm = 254m } } }, "Wavelength is not used on a GC method."),
            "volume" => (r with { SampleSolutionVolumeMl = null }, "Sample solution volume is required for residual solvents."),
            "conc" => (r with { Analytes = new() { SolventInput(standard.Id, conc: null) } }, "Methanol: standard concentration (µg/mL) is required."),
            _ => throw new ArgumentOutOfRangeException(nameof(field)),
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(req, userId));
        Assert.Equal(message, ex.Message);
    }

    [Fact]
    public async Task CreateHplc_WithGcFieldsOrResidualMode_Throws()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var (diluent, mp, standard) = await SeedBasicsAsync(db, section.Id);
        var service = TestServiceFactory.HplcMethod(db);
        var hplc = Req(diluent.Id, new() { new("A", mp.Id, null) }, new() { AnalyteInput(standard.Id) });

        var gcField = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(hplc with { CarrierGas = CarrierGas.Helium }, userId));
        Assert.Equal("GC settings are not used on an HPLC method.", gcField.Message);

        var mode = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(hplc with { ResultMode = HplcResultMode.ResidualSolvents }, userId));
        Assert.Equal("Residual solvents mode is only available for GC methods.", mode.Message);

        var detector = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(hplc with { DetectorType = HplcDetectorType.Fid }, userId));
        Assert.Equal("Detector Fid is not an HPLC detector.", detector.Message);
    }

    [Fact]
    public async Task Update_ChangingTechniqueOrMode_Throws()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var (diluent, _, standard) = await SeedBasicsAsync(db, section.Id);
        var service = TestServiceFactory.HplcMethod(db);
        var created = await service.CreateAsync(GcReq(diluent.Id, new() { SolventInput(standard.Id) }), userId);

        var assay = GcReq(diluent.Id, new() { new(created.Analytes[0].Id, "Methanol", null, standard.Id, 50m, 50m, 6) },
            mode: HplcResultMode.Assay, reason: "switch");
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateAsync(created.Id, assay, userId));
        Assert.Equal("The technique and result mode can't be changed after the method is created.", ex.Message);
    }

    [Fact]
    public async Task Update_Gc_ReplacesOvenSteps()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var (diluent, _, standard) = await SeedBasicsAsync(db, section.Id);
        var service = TestServiceFactory.HplcMethod(db);
        var created = await service.CreateAsync(GcReq(diluent.Id, new() { SolventInput(standard.Id) }), userId);

        var updated = await service.UpdateAsync(created.Id, GcReq(diluent.Id,
            new() { SolventInput(standard.Id, id: created.Analytes[0].Id) },
            oven: new() { new(null, 35m, 5m), new(8m, 120m, 0m), new(20m, 240m, 10m) }, reason: "new oven"), userId);

        Assert.Equal(3, updated.OvenSteps.Count);
        Assert.Equal(3, await db.HplcMethodOvenSteps.CountAsync(s => s.HplcMethodId == created.Id));
        Assert.Equal(created.Analytes[0].Id, updated.Analytes[0].Id);
    }

    [Fact]
    public async Task Update_ExistingHplcMethod_StillValid()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var (diluent, mp, standard) = await SeedBasicsAsync(db, section.Id);
        var service = TestServiceFactory.HplcMethod(db);
        var created = await service.CreateAsync(Req(diluent.Id, new() { new("A", mp.Id, null) }, new() { AnalyteInput(standard.Id) }), userId);

        var updated = await service.UpdateAsync(created.Id,
            Req(diluent.Id, new() { new("A", mp.Id, null) }, new() { AnalyteInput(standard.Id, id: created.Analytes[0].Id) }, name: "Renamed", reason: "rename"),
            userId);

        Assert.Equal(HplcTechnique.Hplc, updated.Technique);
        Assert.Equal("Renamed", updated.Name);
        Assert.Empty(updated.OvenSteps);
    }

    [Fact]
    public async Task GetAll_FiltersByTechnique()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var (diluent, mp, standard) = await SeedBasicsAsync(db, section.Id);
        var service = TestServiceFactory.HplcMethod(db);
        await service.CreateAsync(Req(diluent.Id, new() { new("A", mp.Id, null) }, new() { AnalyteInput(standard.Id) }), userId);
        await service.CreateAsync(GcReq(diluent.Id, new() { SolventInput(standard.Id) }), userId);

        var gc = await service.GetAllAsync(userId, technique: HplcTechnique.Gc);
        var all = await service.GetAllAsync(userId);

        Assert.Single(gc);
        Assert.Equal(HplcTechnique.Gc, gc[0].Technique);
        Assert.Equal(2, all.Count);
    }

    [Fact]
    public async Task CreateHplc_WithStandardConcentration_Throws()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var (diluent, mp, standard) = await SeedBasicsAsync(db, section.Id);
        var service = TestServiceFactory.HplcMethod(db);
        var hplc = Req(diluent.Id, new() { new("A", mp.Id, null) }, new() { AnalyteInput(standard.Id) with { StandardConcentrationUgPerMl = 10m } });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(hplc, userId));
        Assert.Equal("GC settings are not used on an HPLC method.", ex.Message);
    }
}
