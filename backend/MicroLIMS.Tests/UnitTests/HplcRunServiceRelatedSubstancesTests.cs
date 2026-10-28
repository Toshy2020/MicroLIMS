using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs.Responses;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public partial class HplcRunServiceTests
{
    [Fact]
    public async Task StartRun_RelatedSubstancesMethod_SnapshotCarriesImpurities()
    {
        await using var db = NewDb();
        var (_, clock) = NewClock(SepFirst);
        var s = await SeedScenarioAsync(db, clock);
        // The run snapshot only reads the loaded method, so mark it RS and give it impurities directly.
        s.Method.ResultMode = HplcResultMode.RelatedSubstances;
        s.Method.ReferenceSolutionPercent = 0.1m;
        s.Method.DisregardLimitPercent = 0.05m;
        s.Method.Impurities.Add(new HplcMethodImpurity { DisplayOrder = 1, Name = "Impurity D", RelativeRetention = 0.15m, CorrectionFactor = 0.7m });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear(); // so the run must load the impurities itself
        var service = TestServiceFactory.HplcRun(db, clock: clock);

        var run = await service.StartRunAsync(StartRequest(s), s.UserId);

        var stored = await db.HplcRuns.FirstAsync(r => r.Id == run.Id);
        var snapshot = JsonSerializer.Deserialize<HplcMethodResponse>(stored.MethodSnapshotJson, SnapshotJson.Options)!;
        var impurity = Assert.Single(snapshot.Impurities);
        Assert.Equal("Impurity D", impurity.Name);
        Assert.Equal(0.7m, impurity.CorrectionFactor);
        Assert.Equal(0.1m, snapshot.ReferenceSolutionPercent);
    }

    // ---- Related-substances sample entry, preview and submission (Plan 3 task 3) ----

    private sealed class RsFixture
    {
        public required Scenario S;
        public required HplcRunService Service;
        public required HplcRunDto Run;
        public required TestOrder Order;
        public required HplcMethodAnalyte Analyte;
        public required HplcMethodImpurity ImpurityD;
        public required HplcMethodImpurity ImpurityA;
        public required int RunSampleId;
    }

    private static Specification RsSpec(Item item, TestDefinition def, HplcMethodAnalyte analyte, RelatedSubstanceRow kind, decimal limit,
        HplcMethodImpurity? impurity = null, string name = "") => new()
    {
        ItemId = item.Id, TestCode = def.Code, ParameterName = name, HplcMethodAnalyteId = analyte.Id,
        RelatedSubstanceRow = kind, HplcMethodImpurityId = impurity?.Id,
        ResultBasis = ResultBasis.PercentAsIs, LimitType = LimitType.NotMoreThan, UpperLimit = limit, UpperInclusive = true,
        Unit = "%", DisplayOrder = 1 + (int)kind,
    };

    // A passed run on a related-substances method (reference 0.1 %, disregard 0.05 %, impurities D x0.7 and A) with one
    // assigned sample (one replicate), specs: D <= 0.15, A <= 0.15, any unspecified <= 0.10, total <= 0.5.
    private static async Task<RsFixture> ArrangeRelatedSubstancesAsync(
        MicroLimsDbContext db, ILabClock clock, bool specs = true, decimal sstWeightMg = 0m)
    {
        var s = await SeedScenarioAsync(db, clock);
        s.Method.ResultMode = HplcResultMode.RelatedSubstances;
        s.Method.ReferenceSolutionPercent = 0.1m;
        s.Method.DisregardLimitPercent = 0.05m;
        var d = new HplcMethodImpurity { DisplayOrder = 1, Name = "Impurity D", RelativeRetention = 0.15m, CorrectionFactor = 0.7m };
        var a = new HplcMethodImpurity { DisplayOrder = 2, Name = "Impurity A", RelativeRetention = 1.25m, CorrectionFactor = 1m };
        s.Method.Impurities.Add(d);
        s.Method.Impurities.Add(a);
        await db.SaveChangesAsync();

        var analyte = await db.HplcMethodAnalytes.FirstAsync(x => x.HplcMethodId == s.Method.Id);
        var def = new TestDefinition
        {
            Code = "RS_SILD", DisplayName = "Related substances", SectionId = s.Section.Id,
            WorkflowType = WorkflowType.HplcMethodAssay, EquationType = EquationType.HplcMethodAssay,
            RequiresSystemSuitability = true, HplcMethodId = s.Method.Id, MethodAbbreviation = "VIT-C", IsActive = true,
        };
        db.TestDefinitions.Add(def);
        var item = new Item { Code = "ITM-RS3", Name = "Sildenafil API", Category = SampleCategory.FinishedProduct, IsActive = true };
        db.Items.Add(item);
        await db.SaveChangesAsync();
        if (specs)
        {
            db.Specifications.AddRange(
                RsSpec(item, def, analyte, RelatedSubstanceRow.Named, 0.15m, d, "Impurity D"),
                RsSpec(item, def, analyte, RelatedSubstanceRow.Named, 0.15m, a, "Impurity A"),
                RsSpec(item, def, analyte, RelatedSubstanceRow.UnspecifiedEach, 0.10m),
                RsSpec(item, def, analyte, RelatedSubstanceRow.Total, 0.5m));
            await db.SaveChangesAsync();
        }

        var order = await AddAssayOrderAsync(db, s, new AssayFixture { Item = item, Definition = def, Analyte = analyte, AssaySpec = null! }, sampleReplicates: 1);
        var service = TestServiceFactory.HplcRun(db, clock: clock);
        var run = await service.StartRunAsync(StartRequest(s), s.UserId);
        // The reference standard lot has no purity or moisture and no weight is entered: a related-substances SST needs none.
        var lot = await AddStandardLotAsync(db, s.Section.Id, s.StandardEntry, purity: null, moisturePercent: null);
        await service.SaveSstAsync(run.Id, new SaveSstRequest(new List<SaveSstAnalyteInput> {
            new(run.Sst!.Analytes[0].Id, lot.Id, sstWeightMg, new List<decimal> { 1000m, 1000m, 1000m }, null, null, null, null, null, null, null) }), s.UserId);
        await service.UploadEvidenceAsync(run.Id, null, HplcEvidenceContext.Sst, HplcEvidenceKind.StandardReport, "std.pdf", "application/pdf", PdfBytes(), s.UserId);
        run = await service.ConfirmSstAsync(run.Id, new ConfirmSstRequest(Password, null), s.UserId, null);
        run = await service.AssignSamplesAsync(run.Id, new List<int> { order.Id }, s.UserId);

        return new RsFixture
        {
            S = s, Service = service, Run = run, Order = order, Analyte = analyte, ImpurityD = d, ImpurityA = a,
            RunSampleId = run.Samples.Single().Id,
        };
    }

    private static SaveReplicatesRequest RsReplicates(RsFixture f, decimal referenceArea, params HplcReplicatePeakInput[] peaks) =>
        new(new List<HplcReplicateInput> { new(0m, new List<HplcReplicateResponseInput> { new(f.Analyte.Id, referenceArea) }, peaks.ToList()) });

    // Sildenafil example: D 12000 (x0.7), A 9000, unspecified 6000 and 3000, reference 10000.
    private static HplcReplicatePeakInput[] SildenafilPeaks(RsFixture f) => new HplcReplicatePeakInput[]
    {
        new(f.ImpurityD.Id, null, 12000m), new(f.ImpurityA.Id, null, 9000m), new(null, 0.85m, 6000m), new(null, 1.4m, 3000m),
    };

    [Fact]
    public async Task RsSst_AcceptsALotWithoutWeightPurityOrMoisture()
    {
        await using var db = NewDb();
        var (_, clock) = NewClock(SepFirst);
        var f = await ArrangeRelatedSubstancesAsync(db, clock);

        Assert.Equal(HplcSstStatus.Passed, f.Run.Sst!.Status);
        var sst = await db.HplcSstAnalytes.SingleAsync();
        Assert.Null(sst.StandardWeightMg);
        Assert.Null(sst.StandardPurityPercent);
    }

    [Fact]
    public async Task RsRun_OffersNoWorkingStandardQualifications()
    {
        await using var db = NewDb();
        var (_, clock) = NewClock(SepFirst);
        var f = await ArrangeRelatedSubstancesAsync(db, clock);
        await AddQualificationAsync(db, f.S);

        Assert.Empty(await f.Service.GetEligibleQualificationsAsync(f.Run.Id, f.S.UserId));
    }

    [Fact]
    public async Task SaveReplicates_StoresPeaks_AndPreviewMatchesEvaluator()
    {
        await using var db = NewDb();
        var (_, clock) = NewClock(SepFirst);
        var f = await ArrangeRelatedSubstancesAsync(db, clock);
        var peaks = SildenafilPeaks(f);

        var entry = await f.Service.SaveReplicatesAsync(f.RunSampleId, RsReplicates(f, 10000m, peaks), f.S.UserId);

        Assert.Equal(4, await db.HplcReplicatePeaks.CountAsync());
        Assert.Equal(peaks.ToList(), Assert.Single(entry.Replicates).Peaks);
        Assert.Equal(4, entry.Preview.Count);
        Assert.All(entry.Preview, p => Assert.Equal("Related substance (%)", p.Quantity));
        var expected = RelatedSubstancesEvaluator.Evaluate(f.Analyte.Id, f.Analyte.Name, new RelatedSubstancesInput(
            0.1m, 0.05m,
            new[] { new HplcMethodImpurityResponse(f.ImpurityD.Id, 1, "Impurity D", 0.15m, 0.7m), new HplcMethodImpurityResponse(f.ImpurityA.Id, 2, "Impurity A", 1.25m, 1m) },
            new[] { new RsReplicate(1, 10000m, peaks) },
            await db.Specifications.Where(x => x.TestCode == "RS_SILD").ToListAsync()));
        Assert.Equal(expected.Select(r => (r.AnalyteName, r.Value, r.Display, r.Status)).ToList(),
            entry.Preview.Select(p => (p.ParameterName, p.Value, p.Display, p.Status)).ToList());
        Assert.Equal(new[] { "0.08 %", "0.09 %", "0.06 %", "0.23 %" }, entry.Preview.Select(p => p.Display).ToArray());
        Assert.Equal("Upload the sample report before sending for review.", entry.CanSubmitReason);

        // Saving again replaces the old peaks with the new ones.
        entry = await f.Service.SaveReplicatesAsync(f.RunSampleId, RsReplicates(f, 10000m, new HplcReplicatePeakInput(f.ImpurityA.Id, null, 20000m)), f.S.UserId);
        Assert.Equal(1, await db.HplcReplicatePeaks.CountAsync());
        Assert.Equal(ResultStatus.OutOfSpecification, entry.Preview.Single(p => p.ParameterName == "Impurity A").Status);
    }

    [Fact]
    public async Task EntryProblem_ReferenceAreaZero_Refused()
    {
        await using var db = NewDb();
        var (_, clock) = NewClock(SepFirst);
        var f = await ArrangeRelatedSubstancesAsync(db, clock);

        var entry = await f.Service.SaveReplicatesAsync(f.RunSampleId, RsReplicates(f, 0m, SildenafilPeaks(f)), f.S.UserId);

        Assert.Empty(entry.Preview);
        Assert.False(entry.CanSubmit);
        Assert.Equal($"{f.Analyte.Name}: the reference solution area is required.", entry.CanSubmitReason);
    }

    [Fact]
    public async Task EntryProblem_NoRelatedSubstanceSpecification_Refused()
    {
        await using var db = NewDb();
        var (_, clock) = NewClock(SepFirst);
        var f = await ArrangeRelatedSubstancesAsync(db, clock, specs: false);

        var entry = await f.Service.SaveReplicatesAsync(f.RunSampleId, RsReplicates(f, 10000m, SildenafilPeaks(f)), f.S.UserId);

        Assert.Empty(entry.Preview);
        Assert.Equal("No related-substances specification on this item.", entry.CanSubmitReason);
    }

    [Fact]
    public async Task SaveReplicates_NoPeaksAtAll_PreviewsNotDetected()
    {
        await using var db = NewDb();
        var (_, clock) = NewClock(SepFirst);
        var f = await ArrangeRelatedSubstancesAsync(db, clock);

        var entry = await f.Service.SaveReplicatesAsync(f.RunSampleId, RsReplicates(f, 10000m), f.S.UserId);

        Assert.Equal(4, entry.Preview.Count);
        Assert.All(entry.Preview, p =>
        {
            Assert.Equal("Not detected", p.Display);
            Assert.Equal(ResultStatus.WithinLimits, p.Status);
        });
    }

    [Fact]
    public async Task SaveReplicates_RefusesNegativeArea_AndForeignImpurity()
    {
        await using var db = NewDb();
        var (_, clock) = NewClock(SepFirst);
        var f = await ArrangeRelatedSubstancesAsync(db, clock);
        var foreign = new HplcMethodImpurity { HplcMethodId = 9999, DisplayOrder = 1, Name = "Other method impurity" };
        db.HplcMethodImpurities.Add(foreign);
        await db.SaveChangesAsync();

        var negative = await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.SaveReplicatesAsync(
            f.RunSampleId, RsReplicates(f, 10000m, new HplcReplicatePeakInput(f.ImpurityD.Id, null, -1m)), f.S.UserId));
        Assert.Equal("Peak areas cannot be negative.", negative.Message);

        var other = await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.SaveReplicatesAsync(
            f.RunSampleId, RsReplicates(f, 10000m, new HplcReplicatePeakInput(foreign.Id, null, 100m)), f.S.UserId));
        Assert.Equal("The impurity does not belong to this method.", other.Message);
        Assert.Equal(0, await db.HplcReplicatePeaks.CountAsync());
    }

    [Fact]
    public async Task Submit_WritesOneParameterResultPerSpecRow_WithDisplays()
    {
        await using var db = NewDb();
        var (_, clock) = NewClock(SepFirst);
        var f = await ArrangeRelatedSubstancesAsync(db, clock);
        await f.Service.SaveReplicatesAsync(f.RunSampleId, RsReplicates(f, 10000m, SildenafilPeaks(f)), f.S.UserId);
        await f.Service.UploadEvidenceAsync(f.Run.Id, f.RunSampleId, HplcEvidenceContext.Sample, HplcEvidenceKind.SampleReport, "s.pdf", "application/pdf", PdfBytes(), f.S.UserId);

        await TestServiceFactory.TestWorkflow(db, clock: clock).SubmitHplcMethodAssayAsync(f.RunSampleId, Password, "ok", f.S.UserId, "127.0.0.1");

        var results = await db.ParameterResults.Include(p => p.Readings).Where(p => p.TestOrderId == f.Order.Id && p.IsActive).OrderBy(p => p.Id).ToListAsync();
        Assert.Equal(new[] { "Impurity D", "Impurity A", "Any unspecified impurity", "Total impurities" }, results.Select(r => r.ParameterName).ToArray());
        Assert.Equal(new[] { "0.08 %", "0.09 %", "0.06 %", "0.23 %" }, results.Select(r => r.ReportedDisplay).ToArray());
        Assert.Equal(new decimal?[] { 0.08m, 0.09m, 0.06m, 0.23m }, results.Select(r => r.ReportedValue).ToArray());
        Assert.All(results, r =>
        {
            Assert.Equal("%", r.Unit);
            Assert.Equal(ResultStatus.WithinLimits, r.ComparisonStatus);
            Assert.Contains("\"quantity\":\"RelatedSubstance\"", r.CalculationJson);
        });
        Assert.Contains("\"kind\":\"Named\"", results[0].CalculationJson);
        Assert.Contains($"\"impurityId\":{f.ImpurityD.Id}", results[0].CalculationJson);
        Assert.Contains("\"referenceArea\":10000", results[0].CalculationJson);

        // One reading per contributing peak: D, A and the 6000 unspecified peak make up the total; the 3000 peak was ignored.
        var total = results[3];
        Assert.Equal(3, total.Readings.Count);
        Assert.Contains(total.Readings, r => r.Text != null && r.Text.StartsWith("Impurity D: ") && r.Value1 == 12000m);
        Assert.Contains(total.Readings, r => r.Text != null && r.Text.StartsWith("Unspecified RRT 0.85: "));
        Assert.DoesNotContain(results[2].Readings, r => r.Text!.Contains("1.4"));

        // CalculationJson carries every entered peak per replicate: impurity id or unspecified, RRT, area, pct, and disregarded: true/false.
        var calcDoc = JsonDocument.Parse(results[0].CalculationJson!);
        var repPeaks = calcDoc.RootElement.GetProperty("replicates")[0].GetProperty("peaks");
        Assert.Equal(4, repPeaks.GetArrayLength());
        var belowDisregard = repPeaks.EnumerateArray().Single(p => p.GetProperty("area").GetDecimal() == 3000m);
        Assert.True(belowDisregard.GetProperty("disregarded").GetBoolean());
        Assert.Equal(1.4m, belowDisregard.GetProperty("relativeRetention").GetDecimal());
        Assert.Equal(JsonValueKind.Null, belowDisregard.GetProperty("impurityId").ValueKind);
        var dPeak = repPeaks.EnumerateArray().Single(p => p.GetProperty("area").GetDecimal() == 12000m);
        Assert.False(dPeak.GetProperty("disregarded").GetBoolean());
        Assert.Equal(0.7m, dPeak.GetProperty("correctionFactor").GetDecimal());
        Assert.Equal(1m, repPeaks.EnumerateArray().Single(p => p.GetProperty("area").GetDecimal() == 9000m).GetProperty("correctionFactor").GetDecimal());
        Assert.Equal(1m, belowDisregard.GetProperty("correctionFactor").GetDecimal());
        Assert.Equal(f.ImpurityD.Id, dPeak.GetProperty("impurityId").GetInt32());
    }

    [Fact]
    public async Task PrePlanSnapshot_AssayRun_StillPreviewsAndSubmits()
    {
        await using var db = NewDb();
        var (_, clock) = NewClock(SepFirst);
        var s = await SeedScenarioAsync(db, clock);
        var fixture = await SeedAssayAsync(db, s);
        var order = await AddAssayOrderAsync(db, s, fixture, sampleReplicates: 2);
        var (service, run) = await StartPassedRunAsync(db, clock, s);

        // A snapshot taken before Plan 3: no impurities, reference solution or disregard limit.
        var stored = await db.HplcRuns.FirstAsync(r => r.Id == run.Id);
        var node = System.Text.Json.Nodes.JsonNode.Parse(stored.MethodSnapshotJson)!.AsObject();
        Assert.True(node.Remove("impurities"));
        Assert.True(node.Remove("referenceSolutionPercent"));
        Assert.True(node.Remove("disregardLimitPercent"));
        stored.MethodSnapshotJson = node.ToJsonString();
        await db.SaveChangesAsync();
        Assert.DoesNotContain("impurities", stored.MethodSnapshotJson);

        run = await service.AssignSamplesAsync(run.Id, new List<int> { order.Id }, s.UserId);
        var runSampleId = run.Samples.Single().Id;
        var entry = await service.SaveReplicatesAsync(runSampleId, Replicates(fixture.Analyte.Id, (50m, 1000m), (50m, 1000m)), s.UserId);
        Assert.Equal("Assay %", Assert.Single(entry.Preview).Quantity);
        await service.UploadEvidenceAsync(run.Id, runSampleId, HplcEvidenceContext.Sample, HplcEvidenceKind.SampleReport, "s.pdf", "application/pdf", PdfBytes(), s.UserId);

        await TestServiceFactory.TestWorkflow(db, clock: clock).SubmitHplcMethodAssayAsync(runSampleId, Password, "ok", s.UserId, "127.0.0.1");

        var result = await db.ParameterResults.SingleAsync(p => p.TestOrderId == order.Id && p.IsActive);
        Assert.Contains("\"quantity\":\"AssayPercent\"", result.CalculationJson);
    }

    [Fact]
    public async Task Submit_WhenRowAboveLimit_WritesOutOfSpecificationResult()
    {
        await using var db = NewDb();
        var (_, clock) = NewClock(SepFirst);
        var f = await ArrangeRelatedSubstancesAsync(db, clock);
        // Impurity A area 20000m -> 0.20%, exceeding limit 0.15%
        await f.Service.SaveReplicatesAsync(f.RunSampleId, RsReplicates(f, 10000m, new HplcReplicatePeakInput(f.ImpurityA.Id, null, 20000m)), f.S.UserId);
        await f.Service.UploadEvidenceAsync(f.Run.Id, f.RunSampleId, HplcEvidenceContext.Sample, HplcEvidenceKind.SampleReport, "s.pdf", "application/pdf", PdfBytes(), f.S.UserId);

        await TestServiceFactory.TestWorkflow(db, clock: clock).SubmitHplcMethodAssayAsync(f.RunSampleId, Password, "ok", f.S.UserId, "127.0.0.1");

        var results = await db.ParameterResults.Where(p => p.TestOrderId == f.Order.Id && p.IsActive).ToListAsync();
        var aResult = results.Single(r => r.ParameterName == "Impurity A");
        Assert.Equal(ResultStatus.OutOfSpecification, aResult.ComparisonStatus);
        Assert.Equal("0.20 %", aResult.ReportedDisplay);
        Assert.Equal(0.20m, aResult.ReportedValue);
    }

    [Fact]
    public async Task SaveReplicates_RefusesNegativeReferenceArea()
    {
        await using var db = NewDb();
        var (_, clock) = NewClock(SepFirst);
        var f = await ArrangeRelatedSubstancesAsync(db, clock);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            f.Service.SaveReplicatesAsync(f.RunSampleId, RsReplicates(f, -500m, SildenafilPeaks(f)), f.S.UserId));
        Assert.Equal("Replicate 1: the reference solution area must be zero or more.", ex.Message);
    }

    [Fact]
    public async Task SaveReplicates_RsMode_RefusesNegativeSampleWeight()
    {
        await using var db = NewDb();
        var (_, clock) = NewClock(SepFirst);
        var f = await ArrangeRelatedSubstancesAsync(db, clock);

        var req = new SaveReplicatesRequest(new List<HplcReplicateInput>
        {
            new(-1m, new List<HplcReplicateResponseInput> { new(f.Analyte.Id, 10000m) }, SildenafilPeaks(f).ToList())
        });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            f.Service.SaveReplicatesAsync(f.RunSampleId, req, f.S.UserId));
        Assert.Equal("The sample weight cannot be negative.", ex.Message);
    }

    [Fact]
    public async Task SaveReplicates_RefusesPeakAreaAbove1e12()
    {
        await using var db = NewDb();
        var (_, clock) = NewClock(SepFirst);
        var f = await ArrangeRelatedSubstancesAsync(db, clock);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            f.Service.SaveReplicatesAsync(f.RunSampleId, RsReplicates(f, 10000m, new HplcReplicatePeakInput(f.ImpurityD.Id, null, 1_000_000_000_001m)), f.S.UserId));
        Assert.Equal("Peak area is too large.", ex.Message);
    }

    [Fact]
    public async Task SaveReplicates_RefusesMoreThan50PeaksPerReplicate()
    {
        await using var db = NewDb();
        var (_, clock) = NewClock(SepFirst);
        var f = await ArrangeRelatedSubstancesAsync(db, clock);

        var fiftyOnePeaks = Enumerable.Range(1, 51)
            .Select(i => new HplcReplicatePeakInput(null, (decimal)i, 100m))
            .ToArray();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            f.Service.SaveReplicatesAsync(f.RunSampleId, RsReplicates(f, 10000m, fiftyOnePeaks), f.S.UserId));
        Assert.Equal("At most 50 peaks per replicate.", ex.Message);
    }

    [Fact]
    public async Task Submit_NullDisregardLimit_StoresNullInCalculationJson()
    {
        await using var db = NewDb();
        var (_, clock) = NewClock(SepFirst);
        var f = await ArrangeRelatedSubstancesAsync(db, clock);

        // Update run's snapshot to have disregardLimitPercent: null
        var run = await db.HplcRuns.FirstAsync(r => r.Id == f.Run.Id);
        var node = System.Text.Json.Nodes.JsonNode.Parse(run.MethodSnapshotJson)!.AsObject();
        node["disregardLimitPercent"] = null;
        run.MethodSnapshotJson = node.ToJsonString();
        await db.SaveChangesAsync();

        await f.Service.SaveReplicatesAsync(f.RunSampleId, RsReplicates(f, 10000m, SildenafilPeaks(f)), f.S.UserId);
        await f.Service.UploadEvidenceAsync(f.Run.Id, f.RunSampleId, HplcEvidenceContext.Sample, HplcEvidenceKind.SampleReport, "s.pdf", "application/pdf", PdfBytes(), f.S.UserId);

        await TestServiceFactory.TestWorkflow(db, clock: clock).SubmitHplcMethodAssayAsync(f.RunSampleId, Password, "ok", f.S.UserId, "127.0.0.1");

        var result = await db.ParameterResults.FirstAsync(p => p.TestOrderId == f.Order.Id && p.IsActive);
        var doc = JsonDocument.Parse(result.CalculationJson!);
        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("disregardLimitPercent").ValueKind);
    }

    [Fact]
    public async Task RsSst_StoresNoStandardWeight_EvenWhenOneIsSent()
    {
        await using var db = NewDb();
        var (_, clock) = NewClock(SepFirst);
        var f = await ArrangeRelatedSubstancesAsync(db, clock, sstWeightMg: 25m);

        Assert.Equal(HplcSstStatus.Passed, f.Run.Sst!.Status);
        Assert.Null((await db.HplcSstAnalytes.SingleAsync()).StandardWeightMg);
    }

    [Fact]
    public async Task SampleEntry_CarriesTheRunSnapshotFields_NotTheLiveMethod()
    {
        await using var db = NewDb();
        var (_, clock) = NewClock(SepFirst);
        var f = await ArrangeRelatedSubstancesAsync(db, clock);

        // The live method drifts after the run started; the entry must still describe the snapshot.
        (await db.HplcMethodImpurities.FirstAsync(i => i.Id == f.ImpurityD.Id)).Name = "Renamed live";
        await db.SaveChangesAsync();

        var entry = await f.Service.GetSampleEntryAsync(f.RunSampleId, f.S.UserId);

        Assert.Equal(HplcResultMode.RelatedSubstances, entry.ResultMode);
        Assert.Null(entry.InternalStandardName);
        Assert.Equal(new[] { "Impurity D", "Impurity A" }, entry.Impurities!.Select(i => i.Name).ToArray());
        Assert.Equal(0.7m, entry.Impurities![0].CorrectionFactor);
        Assert.Equal(new HplcPrincipalAnalyteDto(f.Analyte.Id, f.Analyte.Name), entry.PrincipalAnalyte);
        Assert.Equal(0.1m, entry.ReferenceSolutionPercent);
        Assert.Equal(0.05m, entry.DisregardLimitPercent);
    }
}
