using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

// GC residual solvents through the run (spec 2026-10-03 §3.3).
public partial class HplcRunServiceTests
{
    private static async Task<(HplcRunService Service, HplcRunDto Run, TestOrder Order, GcScenario G)> ArrangeRsRunAsync(
        MicroLimsDbContext db, ILabClock clock, bool ppmSpec = true, List<decimal>? stdResponses = null, ProductionStageRole role = ProductionStageRole.Finished)
    {
        var g = await SeedGcScenarioAsync(db);
        var analyte = await db.HplcMethodAnalytes.FirstAsync(a => a.HplcMethodId == g.Method.Id);
        var def = new TestDefinition
        {
            Code = "RS_TEST", DisplayName = "Residual solvents", SectionId = g.Section.Id,
            WorkflowType = WorkflowType.HplcMethodAssay, EquationType = EquationType.HplcMethodAssay,
            RequiresSystemSuitability = true, HplcMethodId = g.Method.Id, MethodAbbreviation = "RS-01", IsActive = true,
        };
        db.TestDefinitions.Add(def);
        var item = new Item { Code = "ITM-RS", Name = "Tablets", Category = SampleCategory.FinishedProduct, IsActive = true };
        db.Items.Add(item);
        await db.SaveChangesAsync();
        if (ppmSpec)
        {
            db.Specifications.Add(new Specification
            {
                ItemId = item.Id, TestCode = def.Code, ParameterName = "Methanol", HplcMethodAnalyteId = analyte.Id,
                ResultBasis = ResultBasis.Ppm, LimitType = LimitType.NotMoreThan, UpperLimit = 3000m, UpperInclusive = true,
                Unit = "ppm", DisplayOrder = 1,
            });
            await db.SaveChangesAsync();
        }

        // Reuse the assay order helper through a minimal Scenario/AssayFixture pair.
        var scenario = new Scenario
        {
            Section = g.Section, UserId = g.UserId, MobilePhase = null!, MobileEntry = null!, Method = g.Method,
            Equipment = g.Equipment, Column = g.Column, StandardEntry = g.StandardEntry, MobilePhasePrep = null!,
        };
        var fixture = new AssayFixture { Item = item, Definition = def, Analyte = analyte, AssaySpec = null! };
        var order = await AddAssayOrderAsync(db, scenario, fixture, role, sampleReplicates: 2);

        var service = TestServiceFactory.HplcRun(db, clock: clock);
        var run = await service.StartRunAsync(GcStart(g), g.UserId);
        var lot = await AddStandardLotAsync(db, g.Section.Id, g.StandardEntry, purity: null, moisturePercent: null);
        await service.SaveSstAsync(run.Id, new SaveSstRequest(new List<SaveSstAnalyteInput> {
            new(run.Sst!.Analytes[0].Id, lot.Id, 0m, stdResponses ?? new List<decimal> { 1000m, 1000m, 1000m }, null, null, null, null, null, null, null) }), g.UserId);
        await service.UploadEvidenceAsync(run.Id, null, HplcEvidenceContext.Sst, HplcEvidenceKind.StandardReport, "std.pdf", "application/pdf", PdfBytes(), g.UserId);
        run = await service.ConfirmSstAsync(run.Id, new ConfirmSstRequest(Password, null), g.UserId, null);
        return (service, run, order, g);
    }

    [Fact]
    public async Task RsSst_NoWeightNoPurity_Passes()
    {
        await using var db = NewDb();
        var (_, clock) = NewClock(SepFirst);
        var (_, run, _, _) = await ArrangeRsRunAsync(db, clock);
        Assert.Equal(HplcSstStatus.Passed, run.Sst!.Status);
    }

    [Fact]
    public async Task RsEntry_PreviewShowsMeanPpmAgainstLimit()
    {
        await using var db = NewDb();
        var (_, clock) = NewClock(SepFirst);
        var (service, run, order, g) = await ArrangeRsRunAsync(db, clock);
        run = await service.AssignSamplesAsync(run.Id, new List<int> { order.Id }, g.UserId);
        var runSampleId = run.Samples.Single().Id;
        var analyteId = (await db.HplcMethodAnalytes.FirstAsync(a => a.HplcMethodId == g.Method.Id)).Id;

        var entry = await service.SaveReplicatesAsync(runSampleId, Replicates(analyteId, (400m, 900m), (400m, 1000m)), g.UserId);

        var row = Assert.Single(entry.Preview);
        Assert.Equal("Residual solvent (ppm)", row.Quantity);
        Assert.Equal(2850m, row.Value);
        Assert.Equal("2850.0 ppm", row.Display);
        Assert.Equal(ResultStatus.WithinLimits, row.Status);
    }

    [Fact]
    public async Task RsEntry_NoPpmSpec_ShowsProblem()
    {
        await using var db = NewDb();
        var (_, clock) = NewClock(SepFirst);
        var (service, run, order, g) = await ArrangeRsRunAsync(db, clock, ppmSpec: false);
        run = await service.AssignSamplesAsync(run.Id, new List<int> { order.Id }, g.UserId);
        var analyteId = (await db.HplcMethodAnalytes.FirstAsync(a => a.HplcMethodId == g.Method.Id)).Id;

        var entry = await service.SaveReplicatesAsync(run.Samples.Single().Id, Replicates(analyteId, (400m, 900m), (400m, 1000m)), g.UserId);

        Assert.Empty(entry.Preview);
        Assert.Equal("No residual-solvent specification for Methanol on this item.", entry.CanSubmitReason);
    }

    [Fact]
    public async Task RsEntry_MissingSstMean_ShowsProblem()
    {
        await using var db = NewDb();
        var (_, clock) = NewClock(SepFirst);
        var (service, run, order, g) = await ArrangeRsRunAsync(db, clock);
        run = await service.AssignSamplesAsync(run.Id, new List<int> { order.Id }, g.UserId);
        var sstRow = await db.HplcSstAnalytes.FirstAsync();
        sstRow.MeanResponse = null;
        await db.SaveChangesAsync();
        var analyteId = (await db.HplcMethodAnalytes.FirstAsync(a => a.HplcMethodId == g.Method.Id)).Id;

        var entry = await service.SaveReplicatesAsync(run.Samples.Single().Id, Replicates(analyteId, (400m, 900m), (400m, 1000m)), g.UserId);

        Assert.Equal("Methanol: the system suitability standard values are incomplete.", entry.CanSubmitReason);
    }

    [Fact]
    public async Task RsSubmit_StoresPpmResultWithCalculationJson()
    {
        await using var db = NewDb();
        var (_, clock) = NewClock(SepFirst);
        var (service, run, order, g) = await ArrangeRsRunAsync(db, clock);
        run = await service.AssignSamplesAsync(run.Id, new List<int> { order.Id }, g.UserId);
        var runSampleId = run.Samples.Single().Id;
        var analyteId = (await db.HplcMethodAnalytes.FirstAsync(a => a.HplcMethodId == g.Method.Id)).Id;
        await service.SaveReplicatesAsync(runSampleId, Replicates(analyteId, (400m, 900m), (400m, 1000m)), g.UserId);
        await service.UploadEvidenceAsync(run.Id, runSampleId, HplcEvidenceContext.Sample, HplcEvidenceKind.SampleReport, "s.pdf", "application/pdf", PdfBytes(), g.UserId);

        await TestServiceFactory.TestWorkflow(db, clock: clock).SubmitHplcMethodAssayAsync(runSampleId, Password, "ok", g.UserId, "127.0.0.1");

        var result = await db.ParameterResults.Include(p => p.Readings).SingleAsync(p => p.TestOrderId == order.Id && p.IsActive);
        Assert.Equal(2850m, result.ReportedValue);
        Assert.Equal("ppm", result.Unit);
        Assert.Equal(ResultStatus.WithinLimits, result.ComparisonStatus);
        Assert.Contains("\"quantity\":\"ResidualSolventPpm\"", result.CalculationJson);
        Assert.Equal(new[] { 2700m, 3000m }, result.Readings.OrderBy(r => r.Stage).Select(r => r.ComputedValue!.Value).ToArray());
    }

    [Fact]
    public async Task RsEntry_BulkStage_StillMeanBasisSingleRow()
    {
        await using var db = NewDb();
        var (_, clock) = NewClock(SepFirst);
        var (service, run, order, g) = await ArrangeRsRunAsync(db, clock, role: ProductionStageRole.Bulk);
        run = await service.AssignSamplesAsync(run.Id, new List<int> { order.Id }, g.UserId);
        var analyteId = (await db.HplcMethodAnalytes.FirstAsync(a => a.HplcMethodId == g.Method.Id)).Id;

        var entry = await service.SaveReplicatesAsync(run.Samples.Single().Id, Replicates(analyteId, (400m, 900m), (400m, 1000m)), g.UserId);

        Assert.Equal("Mean", entry.Basis);
        Assert.Equal(2850m, Assert.Single(entry.Preview).Value);
    }

    [Fact]
    public async Task RsEntry_AllZeroResponses_PreviewsNotDetected()
    {
        await using var db = NewDb();
        var (_, clock) = NewClock(SepFirst);
        var (service, run, order, g) = await ArrangeRsRunAsync(db, clock);
        run = await service.AssignSamplesAsync(run.Id, new List<int> { order.Id }, g.UserId);
        var analyteId = (await db.HplcMethodAnalytes.FirstAsync(a => a.HplcMethodId == g.Method.Id)).Id;

        var entry = await service.SaveReplicatesAsync(run.Samples.Single().Id, Replicates(analyteId, (400m, 0m), (400m, 0m)), g.UserId);

        var row = Assert.Single(entry.Preview);
        Assert.Equal(0m, row.Value);
        Assert.Equal("Not detected", row.Display);
        Assert.Equal(ResultStatus.WithinLimits, row.Status);
    }

    [Fact]
    public async Task RsEntry_OneZeroReplicate_MeanIncludesZero()
    {
        await using var db = NewDb();
        var (_, clock) = NewClock(SepFirst);
        var (service, run, order, g) = await ArrangeRsRunAsync(db, clock);
        run = await service.AssignSamplesAsync(run.Id, new List<int> { order.Id }, g.UserId);
        var analyteId = (await db.HplcMethodAnalytes.FirstAsync(a => a.HplcMethodId == g.Method.Id)).Id;

        var entry = await service.SaveReplicatesAsync(run.Samples.Single().Id, Replicates(analyteId, (400m, 0m), (400m, 1000m)), g.UserId);

        var row = Assert.Single(entry.Preview);
        Assert.Equal(1500m, row.Value);
        Assert.Equal("1500.0 ppm", row.Display);
    }

    [Fact]
    public async Task RsEntry_NegativeResponse_Throws()
    {
        await using var db = NewDb();
        var (_, clock) = NewClock(SepFirst);
        var (service, run, order, g) = await ArrangeRsRunAsync(db, clock);
        run = await service.AssignSamplesAsync(run.Id, new List<int> { order.Id }, g.UserId);
        var analyteId = (await db.HplcMethodAnalytes.FirstAsync(a => a.HplcMethodId == g.Method.Id)).Id;

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SaveReplicatesAsync(run.Samples.Single().Id, Replicates(analyteId, (400m, -1m), (400m, 1000m)), g.UserId));
        Assert.Equal("Replicate 1: the response for Methanol must be zero or more.", ex.Message);
    }

    [Fact]
    public async Task RsSubmit_AllZeroResponses_StoresNotDetected()
    {
        await using var db = NewDb();
        var (_, clock) = NewClock(SepFirst);
        var (service, run, order, g) = await ArrangeRsRunAsync(db, clock);
        run = await service.AssignSamplesAsync(run.Id, new List<int> { order.Id }, g.UserId);
        var runSampleId = run.Samples.Single().Id;
        var analyteId = (await db.HplcMethodAnalytes.FirstAsync(a => a.HplcMethodId == g.Method.Id)).Id;
        await service.SaveReplicatesAsync(runSampleId, Replicates(analyteId, (400m, 0m), (400m, 0m)), g.UserId);
        await service.UploadEvidenceAsync(run.Id, runSampleId, HplcEvidenceContext.Sample, HplcEvidenceKind.SampleReport, "s.pdf", "application/pdf", PdfBytes(), g.UserId);

        await TestServiceFactory.TestWorkflow(db, clock: clock).SubmitHplcMethodAssayAsync(runSampleId, Password, "ok", g.UserId, "127.0.0.1");

        var result = await db.ParameterResults.SingleAsync(p => p.TestOrderId == order.Id && p.IsActive);
        Assert.Equal(0m, result.ReportedValue);
        Assert.Equal("Not detected", result.ReportedDisplay);
        Assert.Equal(ResultStatus.WithinLimits, result.ComparisonStatus);
        Assert.Contains("\"notDetected\":true", result.CalculationJson);
    }
}
