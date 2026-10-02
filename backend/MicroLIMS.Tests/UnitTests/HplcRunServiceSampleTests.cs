using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

// HPLC Workspace Part B (HPLC chain S6, plan Tasks B2/B3): eligible tests,
// sample assignment behind the SST gate, replicate entry with preview,
// submission, run completion and the approval gate. Shares the seeding
// helpers of HplcRunServiceTests (partial class).
public partial class HplcRunServiceTests
{
    private sealed class AssayFixture
    {
        public required Item Item;
        public required TestDefinition Definition;
        public required HplcMethodAnalyte Analyte;
        public required Specification AssaySpec;
        public Specification? AmountSpec;
    }

    private static async Task<AssayFixture> SeedAssayAsync(
        MicroLimsDbContext db, Scenario s, bool amountSpec = false, bool assaySpec = true)
    {
        var analyte = await db.HplcMethodAnalytes.FirstAsync(a => a.HplcMethodId == s.Method.Id);
        var def = new TestDefinition
        {
            Code = "VITC_ASSAY", DisplayName = "Vitamin C assay", SectionId = s.Section.Id,
            WorkflowType = WorkflowType.HplcMethodAssay, EquationType = EquationType.HplcMethodAssay,
            RequiresSystemSuitability = true, HplcMethodId = s.Method.Id, MethodAbbreviation = "VIT-C", IsActive = true,
        };
        db.TestDefinitions.Add(def);
        var item = new Item { Code = "ITM-1", Name = "Vitamin C Tablets", Category = SampleCategory.FinishedProduct, IsActive = true };
        db.Items.Add(item);
        await db.SaveChangesAsync();

        var fixture = new AssayFixture
        {
            Item = item, Definition = def, Analyte = analyte,
            AssaySpec = new Specification
            {
                ItemId = item.Id, TestCode = def.Code, ParameterName = "Vitamin C", HplcMethodAnalyteId = analyte.Id,
                ResultBasis = ResultBasis.PercentLabelClaim, LimitType = LimitType.Range,
                LowerLimit = 95m, UpperLimit = 105m, Unit = "%", DisplayOrder = 1,
            },
        };
        if (assaySpec) db.Specifications.Add(fixture.AssaySpec);
        if (amountSpec)
        {
            fixture.AmountSpec = new Specification
            {
                ItemId = item.Id, TestCode = def.Code, ParameterName = "Vitamin C amount", HplcMethodAnalyteId = analyte.Id,
                ResultBasis = ResultBasis.MgPerUnit, LabelClaim = 500m, LabelClaimUnit = "mg", LimitType = LimitType.Range,
                LowerLimit = 475m, UpperLimit = 525m, Unit = "mg", DisplayOrder = 2,
            };
            db.Specifications.Add(fixture.AmountSpec);
        }
        await db.SaveChangesAsync();
        return fixture;
    }

    private static async Task<TestOrder> AddAssayOrderAsync(
        MicroLimsDbContext db, Scenario s, AssayFixture f, ProductionStageRole role = ProductionStageRole.Finished,
        int sampleReplicates = 2, string batch = "B-001")
    {
        var stage = await db.ProductionStages.FirstOrDefaultAsync(p => p.Role == role);
        if (stage == null)
        {
            stage = new ProductionStage { Name = "Stage " + role, Role = role, IsActive = true };
            db.ProductionStages.Add(stage);
            await db.SaveChangesAsync();
        }
        if (!await db.TestDefinitionStageReplicates.AnyAsync(r => r.TestDefinitionId == f.Definition.Id && r.Role == role))
        {
            db.TestDefinitionStageReplicates.Add(new TestDefinitionStageReplicate
            {
                TestDefinitionId = f.Definition.Id, Role = role, SampleReplicates = sampleReplicates,
            });
            await db.SaveChangesAsync();
        }

        var cause = await db.CausesOfTesting.FirstOrDefaultAsync() ?? new CauseOfTesting { Name = "Release", IsActive = true };
        if (cause.Id == 0) db.CausesOfTesting.Add(cause);

        var sample = new Sample
        {
            ReferenceNumber = "FP-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            ItemId = f.Item.Id, Category = SampleCategory.FinishedProduct, BatchNumber = batch,
            ReceivedAt = SepFirst.UtcDateTime, CauseOfTesting = cause, ReceivedByUserId = s.UserId,
            ProductionStageId = stage.Id, Status = SampleStatus.InTesting,
        };
        db.Samples.Add(sample);
        await db.SaveChangesAsync();

        var order = new TestOrder
        {
            SampleId = sample.Id, TestCode = f.Definition.Code, SectionId = s.Section.Id,
            CurrentStep = WorkflowStep.Running, Status = ApprovalStatus.InProgress,
        };
        db.TestOrders.Add(order);
        await db.SaveChangesAsync();
        return order;
    }

    private static async Task<HplcRunDto> PassSstAsync(
        MicroLimsDbContext db, HplcRunService service, Scenario s, HplcRunDto run,
        List<decimal>? responses = null)
    {
        var lot = await AddStandardLotAsync(db, s.Section.Id, s.StandardEntry);
        await service.SaveSstAsync(run.Id, new SaveSstRequest(new List<SaveSstAnalyteInput> {
            new(run.Sst!.Analytes[0].Id, lot.Id, 50m, responses ?? new List<decimal> { 1000m, 1000m, 1000m }, null, null, null, null, null, null, null) }), s.UserId);
        await service.UploadEvidenceAsync(run.Id, null, HplcEvidenceContext.Sst, HplcEvidenceKind.StandardReport, "std.pdf", "application/pdf", PdfBytes(), s.UserId);
        return await service.ConfirmSstAsync(run.Id, new ConfirmSstRequest(Password, null), s.UserId, null);
    }

    private static async Task<(HplcRunService Service, HplcRunDto Run)> StartPassedRunAsync(MicroLimsDbContext db, ILabClock clock, Scenario s)
    {
        var service = TestServiceFactory.HplcRun(db, clock: clock);
        var run = await service.StartRunAsync(StartRequest(s), s.UserId);
        run = await PassSstAsync(db, service, s, run);
        return (service, run);
    }

    // ---- GetEligibleTestsAsync ----

    [Fact]
    public async Task Eligible_ListsOnlyOpenOrdersOfTheRunsMethod()
    {
        await using var db = NewDb();
        var (_, clock) = NewClock(SepFirst);
        var s = await SeedScenarioAsync(db, clock);
        var f = await SeedAssayAsync(db, s);
        var open = await AddAssayOrderAsync(db, s, f, batch: "B-OPEN");
        var finalized = await AddAssayOrderAsync(db, s, f, batch: "B-DONE");
        finalized.CurrentStep = WorkflowStep.Ready;
        var voided = await AddAssayOrderAsync(db, s, f, batch: "B-VOID");
        (await db.Samples.FirstAsync(x => x.Id == voided.SampleId)).Status = SampleStatus.Voided;
        var superseded = await AddAssayOrderAsync(db, s, f, batch: "B-SUP");
        superseded.IsSuperseded = true;
        await db.SaveChangesAsync();
        var (service, run) = await StartPassedRunAsync(db, clock, s);

        var eligible = await service.GetEligibleTestsAsync(run.Id, null, s.UserId);

        var row = Assert.Single(eligible);
        Assert.Equal(open.Id, row.TestOrderId);
        Assert.Equal("B-OPEN", row.BatchNumber);
        Assert.Equal("Vitamin C Tablets", row.ProductName);
        Assert.Equal("VITC_ASSAY", row.TestCode);
    }

    [Fact]
    public async Task Eligible_SearchFiltersByBatch()
    {
        await using var db = NewDb();
        var (_, clock) = NewClock(SepFirst);
        var s = await SeedScenarioAsync(db, clock);
        var f = await SeedAssayAsync(db, s);
        await AddAssayOrderAsync(db, s, f, batch: "ALPHA-1");
        var beta = await AddAssayOrderAsync(db, s, f, batch: "BETA-2");
        var (service, run) = await StartPassedRunAsync(db, clock, s);

        var eligible = await service.GetEligibleTestsAsync(run.Id, "beta", s.UserId);

        Assert.Equal(beta.Id, Assert.Single(eligible).TestOrderId);
    }

    // ---- Dissolution on the workspace ----

    // Dissolution TestDefinition on the run's method plus one order; the
    // method analyte's standard dilution is set before the run starts so the
    // snapshot carries it (extraAnalyte makes the method two-analyte).
    private static async Task<(Scenario S, TestOrder Order, ILabClock Clock)> SeedDissolutionAsync(
        MicroLimsDbContext db, decimal? dilution, bool extraAnalyte = false)
    {
        var (_, clock) = NewClock(SepFirst);
        var s = await SeedScenarioAsync(db, clock);
        var analyte = await db.HplcMethodAnalytes.FirstAsync(a => a.HplcMethodId == s.Method.Id);
        analyte.StandardDilution = dilution;
        if (extraAnalyte)
        {
            db.HplcMethodAnalytes.Add(new HplcMethodAnalyte
            {
                HplcMethodId = s.Method.Id, DisplayOrder = 2, Name = "Analyte 2", WavelengthNm = 290m,
                StandardEntryId = analyte.StandardEntryId, TheoreticalWeightStdMg = 50m, TheoreticalWeightTestMg = 50m,
                StandardInjections = 3, StandardDilution = dilution,
            });
        }
        db.TestDefinitions.Add(new TestDefinition
        {
            Code = "DISS-1", DisplayName = "Dissolution", SectionId = s.Section.Id,
            WorkflowType = WorkflowType.Dissolution, EquationType = EquationType.Dissolution,
            RequiresSystemSuitability = true, HplcMethodId = s.Method.Id, IsActive = true,
        });
        var item = new Item { Code = "ITM-D", Name = "Dissolution Tablets", Category = SampleCategory.FinishedProduct, IsActive = true };
        db.Items.Add(item);
        var cause = new CauseOfTesting { Name = "Release", IsActive = true };
        db.CausesOfTesting.Add(cause);
        await db.SaveChangesAsync();
        var sample = new Sample
        {
            ReferenceNumber = "FP-DISS01", ItemId = item.Id, Category = SampleCategory.FinishedProduct, BatchNumber = "B-D",
            ReceivedAt = SepFirst.UtcDateTime, CauseOfTesting = cause, ReceivedByUserId = s.UserId, Status = SampleStatus.InTesting,
        };
        db.Samples.Add(sample);
        await db.SaveChangesAsync();
        var order = new TestOrder
        {
            SampleId = sample.Id, TestCode = "DISS-1", SectionId = s.Section.Id,
            CurrentStep = WorkflowStep.Running, Status = ApprovalStatus.InProgress,
        };
        db.TestOrders.Add(order);
        await db.SaveChangesAsync();
        return (s, order, clock);
    }

    [Fact]
    public async Task Eligible_IncludesDissolutionOrderOfSameMethod()
    {
        await using var db = NewDb();
        var (s, _, clock) = await SeedDissolutionAsync(db, 2500m);
        var (service, run) = await StartPassedRunAsync(db, clock, s);

        var eligible = await service.GetEligibleTestsAsync(run.Id, null, s.UserId);

        Assert.Contains(eligible, e => e.TestCode == "DISS-1");
    }

    [Fact]
    public async Task Assign_DissolutionWithoutStandardDilution_Throws()
    {
        await using var db = NewDb();
        var (s, dissOrder, clock) = await SeedDissolutionAsync(db, null);
        var (service, run) = await StartPassedRunAsync(db, clock, s);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.AssignSamplesAsync(run.Id, new List<int> { dissOrder.Id }, s.UserId));

        Assert.Contains("standard dilution", ex.Message);
    }

    [Fact]
    public async Task Assign_DissolutionMultiAnalyteMethod_Throws()
    {
        await using var db = NewDb();
        var (s, dissOrder, clock) = await SeedDissolutionAsync(db, 2500m, extraAnalyte: true);
        var service = TestServiceFactory.HplcRun(db, clock: clock);
        var run = await service.StartRunAsync(StartRequest(s), s.UserId);
        // PassSstAsync records only the first analyte, so mark the SST passed directly.
        (await db.HplcRuns.Include(r => r.Sst).SingleAsync(r => r.Id == run.Id)).Sst!.Status = HplcSstStatus.Passed;
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.AssignSamplesAsync(run.Id, new List<int> { dissOrder.Id }, s.UserId));

        Assert.Contains("exactly one analyte", ex.Message);
    }

    [Fact]
    public async Task Summary_FlagsDissolutionSample()
    {
        await using var db = NewDb();
        var (s, dissOrder, clock) = await SeedDissolutionAsync(db, 2500m);
        var (service, run) = await StartPassedRunAsync(db, clock, s);

        var updated = await service.AssignSamplesAsync(run.Id, new List<int> { dissOrder.Id }, s.UserId);

        Assert.True(updated.Samples.Single(x => x.TestOrderId == dissOrder.Id).IsDissolution);
    }

    private static void AddActiveAnalysis(MicroLimsDbContext db, TestOrder order, int userId)
    {
        db.TestAnalyses.Add(new TestAnalysis { TestOrderId = order.Id, AnalysisType = WorkflowType.Dissolution, EnteredByUserId = userId, IsActive = true });
    }

    [Fact]
    public async Task Eligible_DissolutionWithActiveResult_AfterAbandon_NotEligibleNorAssignable()
    {
        await using var db = NewDb();
        var (s, dissOrder, clock) = await SeedDissolutionAsync(db, 2500m);
        var (service, run) = await StartPassedRunAsync(db, clock, s);
        await service.AssignSamplesAsync(run.Id, new List<int> { dissOrder.Id }, s.UserId);
        AddActiveAnalysis(db, dissOrder, s.UserId);
        await db.SaveChangesAsync();
        await service.AbandonRunAsync(run.Id, "instrument fault", s.UserId);

        var run2 = await service.StartRunAsync(StartRequest(s), s.UserId);
        run2 = await PassSstAsync(db, service, s, run2);

        Assert.DoesNotContain(await service.GetEligibleTestsAsync(run2.Id, null, s.UserId), e => e.TestOrderId == dissOrder.Id);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.AssignSamplesAsync(run2.Id, new List<int> { dissOrder.Id }, s.UserId));
        Assert.Contains("not eligible", ex.Message);
    }

    [Fact]
    public async Task Complete_DissolutionStillRunning_IsNotSubmitted_UntilReady()
    {
        await using var db = NewDb();
        var (s, dissOrder, clock) = await SeedDissolutionAsync(db, 2500m);
        var (service, run) = await StartPassedRunAsync(db, clock, s);
        await service.AssignSamplesAsync(run.Id, new List<int> { dissOrder.Id }, s.UserId);
        AddActiveAnalysis(db, dissOrder, s.UserId);
        await db.SaveChangesAsync();

        var summary = await service.GetRunAsync(run.Id, s.UserId);
        Assert.False(summary.Samples.Single().Submitted);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CompleteRunAsync(run.Id, s.UserId));
        Assert.Contains("not been sent for review", ex.Message);

        dissOrder.CurrentStep = WorkflowStep.Ready;
        await db.SaveChangesAsync();

        Assert.True((await service.GetRunAsync(run.Id, s.UserId)).Samples.Single().Submitted);
        await service.CompleteRunAsync(run.Id, s.UserId);
    }

    [Fact]
    public async Task SaveReplicates_DissolutionOrder_Throws()
    {
        await using var db = NewDb();
        var (s, dissOrder, clock) = await SeedDissolutionAsync(db, 2500m);
        var (service, run) = await StartPassedRunAsync(db, clock, s);
        var assigned = await service.AssignSamplesAsync(run.Id, new List<int> { dissOrder.Id }, s.UserId);
        var runSample = assigned.Samples.Single();
        var analyteId = (await db.HplcMethodAnalytes.FirstAsync(a => a.HplcMethodId == s.Method.Id)).Id;

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.SaveReplicatesAsync(runSample.Id, Replicates(analyteId, (50m, 1000m)), s.UserId));

        Assert.Contains("Testing page", ex.Message);
    }

    // ---- AssignSamplesAsync ----

    // Review Focus: assigning before the SST has passed (even by calling the
    // API directly) is refused.
    [Fact]
    public async Task Assign_BeforeSstPassed_Throws()
    {
        await using var db = NewDb();
        var (_, clock) = NewClock(SepFirst);
        var s = await SeedScenarioAsync(db, clock);
        var f = await SeedAssayAsync(db, s);
        var order = await AddAssayOrderAsync(db, s, f);
        var service = TestServiceFactory.HplcRun(db, clock: clock);
        var run = await service.StartRunAsync(StartRequest(s), s.UserId);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.AssignSamplesAsync(run.Id, new List<int> { order.Id }, s.UserId));

        Assert.Equal("Sample assignment is locked until system suitability passes.", ex.Message);
        Assert.Empty(db.HplcRunSamples);
    }

    [Fact]
    public async Task Assign_AfterSstFailed_Throws()
    {
        await using var db = NewDb();
        var (_, clock) = NewClock(SepFirst);
        var s = await SeedScenarioAsync(db, clock);
        var f = await SeedAssayAsync(db, s);
        var order = await AddAssayOrderAsync(db, s, f);
        var service = TestServiceFactory.HplcRun(db, clock: clock);
        var run = await service.StartRunAsync(StartRequest(s), s.UserId);
        run = await PassSstAsync(db, service, s, run, new List<decimal> { 1000m, 1300m, 700m });
        Assert.Equal(HplcSstStatus.Failed, run.Sst!.Status);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.AssignSamplesAsync(run.Id, new List<int> { order.Id }, s.UserId));
    }

    [Fact]
    public async Task Assign_AfterSstPassed_CreatesAssignedRows()
    {
        await using var db = NewDb();
        var (_, clock) = NewClock(SepFirst);
        var s = await SeedScenarioAsync(db, clock);
        var f = await SeedAssayAsync(db, s);
        var order = await AddAssayOrderAsync(db, s, f, batch: "B-77");
        var (service, run) = await StartPassedRunAsync(db, clock, s);

        var updated = await service.AssignSamplesAsync(run.Id, new List<int> { order.Id }, s.UserId);

        var sample = Assert.Single(updated.Samples);
        Assert.Equal(order.Id, sample.TestOrderId);
        Assert.Equal(HplcRunSampleStatus.Assigned, sample.Status);
        Assert.Equal("B-77", sample.BatchNumber);
        Assert.False(sample.Submitted);
        Assert.Empty(await service.GetEligibleTestsAsync(run.Id, null, s.UserId));
    }

    // Review Focus: the same test order in two open runs is refused.
    [Fact]
    public async Task Assign_TestOrderAlreadyInAnotherOpenRun_Throws()
    {
        await using var db = NewDb();
        var (_, clock) = NewClock(SepFirst);
        var s = await SeedScenarioAsync(db, clock);
        var f = await SeedAssayAsync(db, s);
        var order = await AddAssayOrderAsync(db, s, f);
        var (service, run1) = await StartPassedRunAsync(db, clock, s);
        await service.AssignSamplesAsync(run1.Id, new List<int> { order.Id }, s.UserId);

        var equipment2 = await AddHplcEquipmentAsync(db, s.Section.Id, "HPLC-02");
        var column2 = await AddColumnAsync(db, s.Section.Id, equipment2, "COL-02");
        var run2 = await service.StartRunAsync(
            new StartHplcRunRequest(equipment2.Id, s.Method.Id, column2.Id, new List<HplcMobilePhaseAssignmentInput> { new("A", s.MobilePhasePrep.Id) }), s.UserId);
        run2 = await PassSstAsync(db, service, s, run2);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.AssignSamplesAsync(run2.Id, new List<int> { order.Id }, s.UserId));

        Assert.Contains("already assigned to run", ex.Message);
        Assert.Contains(run1.Code, ex.Message);
    }

    [Fact]
    public async Task Assign_OrderOfAnotherMethod_Throws()
    {
        await using var db = NewDb();
        var (_, clock) = NewClock(SepFirst);
        var s = await SeedScenarioAsync(db, clock);
        var f = await SeedAssayAsync(db, s);
        var order = await AddAssayOrderAsync(db, s, f);
        f.Definition.HplcMethodId = null;
        await db.SaveChangesAsync();
        var (service, run) = await StartPassedRunAsync(db, clock, s);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.AssignSamplesAsync(run.Id, new List<int> { order.Id }, s.UserId));

        Assert.Contains("not eligible", ex.Message);
    }

    [Fact]
    public async Task Assign_ToAbandonedRun_ThenOrderIsEligibleAgain()
    {
        await using var db = NewDb();
        var (_, clock) = NewClock(SepFirst);
        var s = await SeedScenarioAsync(db, clock);
        var f = await SeedAssayAsync(db, s);
        var order = await AddAssayOrderAsync(db, s, f);
        var (service, run) = await StartPassedRunAsync(db, clock, s);
        await service.AssignSamplesAsync(run.Id, new List<int> { order.Id }, s.UserId);
        await service.AbandonRunAsync(run.Id, "instrument fault", s.UserId);

        var run2 = await service.StartRunAsync(StartRequest(s), s.UserId);
        run2 = await PassSstAsync(db, service, s, run2);

        var eligible = await service.GetEligibleTestsAsync(run2.Id, null, s.UserId);
        Assert.Equal(order.Id, Assert.Single(eligible).TestOrderId);
    }

    // ---- RemoveSampleAsync ----

    [Fact]
    public async Task Remove_MarksRemovedWithReason_AndFreesTheOrder()
    {
        await using var db = NewDb();
        var (_, clock) = NewClock(SepFirst);
        var s = await SeedScenarioAsync(db, clock);
        var f = await SeedAssayAsync(db, s);
        var order = await AddAssayOrderAsync(db, s, f);
        var (service, run) = await StartPassedRunAsync(db, clock, s);
        var assigned = await service.AssignSamplesAsync(run.Id, new List<int> { order.Id }, s.UserId);

        var updated = await service.RemoveSampleAsync(run.Id, assigned.Samples[0].Id, "wrong lot", s.UserId);

        Assert.Equal(HplcRunSampleStatus.Removed, Assert.Single(updated.Samples).Status);
        var row = await db.HplcRunSamples.SingleAsync();
        Assert.Equal("wrong lot", row.RemovedReason);
        Assert.NotNull(row.RemovedAt);
        Assert.Equal(s.UserId, row.RemovedByUserId);
        Assert.Single(await service.GetEligibleTestsAsync(run.Id, null, s.UserId));
    }

    [Fact]
    public async Task Remove_WithoutReason_Throws()
    {
        await using var db = NewDb();
        var (_, clock) = NewClock(SepFirst);
        var s = await SeedScenarioAsync(db, clock);
        var f = await SeedAssayAsync(db, s);
        var order = await AddAssayOrderAsync(db, s, f);
        var (service, run) = await StartPassedRunAsync(db, clock, s);
        var assigned = await service.AssignSamplesAsync(run.Id, new List<int> { order.Id }, s.UserId);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RemoveSampleAsync(run.Id, assigned.Samples[0].Id, " ", s.UserId));
    }

    // ---- GetSampleEntryAsync / SaveReplicatesAsync ----

    private static SaveReplicatesRequest Replicates(int analyteId, params (decimal Weight, decimal Response)[] reps) =>
        new(reps.Select(r => new HplcReplicateInput(r.Weight, new List<HplcReplicateResponseInput> { new(analyteId, r.Response) })).ToList());

    [Fact]
    public async Task Entry_BeforeReplicates_ShowsWeightsRequiredCountAndNoPreview()
    {
        await using var db = NewDb();
        var (_, clock) = NewClock(SepFirst);
        var s = await SeedScenarioAsync(db, clock);
        var f = await SeedAssayAsync(db, s);
        var order = await AddAssayOrderAsync(db, s, f, sampleReplicates: 2);
        var (service, run) = await StartPassedRunAsync(db, clock, s);
        var assigned = await service.AssignSamplesAsync(run.Id, new List<int> { order.Id }, s.UserId);

        var entry = await service.GetSampleEntryAsync(assigned.Samples[0].Id, s.UserId);

        Assert.Equal(run.Sst!.Code, entry.SstCode);
        Assert.Equal(HplcSstStatus.Passed, entry.SstStatus);
        Assert.Equal(2, entry.RequiredReplicates);
        Assert.Equal("Mean", entry.Basis);
        var weight = Assert.Single(entry.MethodWeights);
        Assert.Equal(50m, weight.TheoreticalWeightStdMg);
        Assert.Equal(50m, weight.TheoreticalWeightTestMg);
        Assert.Empty(entry.Replicates);
        Assert.Empty(entry.Preview);
        Assert.True(entry.Editable);
        Assert.False(entry.CanSubmit);
        Assert.Contains("Expected exactly 2 replicates", entry.CanSubmitReason);
    }

    [Fact]
    public async Task SaveReplicates_StoresRowsAndPreviewsCalculatedAssay()
    {
        await using var db = NewDb();
        var (_, clock) = NewClock(SepFirst);
        var s = await SeedScenarioAsync(db, clock);
        var f = await SeedAssayAsync(db, s, amountSpec: true);
        var order = await AddAssayOrderAsync(db, s, f, sampleReplicates: 2);
        var (service, run) = await StartPassedRunAsync(db, clock, s);
        var assigned = await service.AssignSamplesAsync(run.Id, new List<int> { order.Id }, s.UserId);

        // R_test 1000, mean std 1000, W_std 50, Th 50/50, W_test 50, MC 0.5, P 99.5 -> 99.0025 %
        var entry = await service.SaveReplicatesAsync(assigned.Samples[0].Id,
            Replicates(f.Analyte.Id, (50m, 1000m), (50m, 1000m)), s.UserId);

        Assert.Equal(2, entry.Replicates.Count);
        Assert.Equal(2, await db.HplcSampleReplicates.CountAsync());
        var assay = Assert.Single(entry.Preview, p => p.Quantity == "Assay %");
        Assert.Equal(99.0025m, Math.Round(assay.Value, 4));
        Assert.Equal(ResultStatus.WithinLimits, assay.Status);
        var amount = Assert.Single(entry.Preview, p => p.Quantity == "Amount per unit");
        Assert.Equal(495.0125m, Math.Round(amount.Value, 4));
        Assert.Equal("mg", amount.Unit);
        Assert.False(entry.CanSubmit);
        Assert.Contains("sample report", entry.CanSubmitReason);
    }

    [Fact]
    public async Task SaveReplicates_ReplacesAllPreviousRows()
    {
        await using var db = NewDb();
        var (_, clock) = NewClock(SepFirst);
        var s = await SeedScenarioAsync(db, clock);
        var f = await SeedAssayAsync(db, s);
        var order = await AddAssayOrderAsync(db, s, f, sampleReplicates: 2);
        var (service, run) = await StartPassedRunAsync(db, clock, s);
        var assigned = await service.AssignSamplesAsync(run.Id, new List<int> { order.Id }, s.UserId);
        await service.SaveReplicatesAsync(assigned.Samples[0].Id, Replicates(f.Analyte.Id, (50m, 1000m), (50m, 1000m)), s.UserId);

        var entry = await service.SaveReplicatesAsync(assigned.Samples[0].Id, Replicates(f.Analyte.Id, (51m, 1010m), (52m, 1020m)), s.UserId);

        Assert.Equal(2, await db.HplcSampleReplicates.CountAsync());
        Assert.Equal(new[] { 51m, 52m }, entry.Replicates.Select(r => r.ActualWeightMg).ToArray());
        Assert.Equal(new[] { 1010m, 1020m }, entry.Replicates.SelectMany(r => r.Responses).Select(x => x.Response).ToArray());
    }

    [Fact]
    public async Task SaveReplicates_NonPositiveWeightOrMissingResponse_Throws()
    {
        await using var db = NewDb();
        var (_, clock) = NewClock(SepFirst);
        var s = await SeedScenarioAsync(db, clock);
        var f = await SeedAssayAsync(db, s);
        var order = await AddAssayOrderAsync(db, s, f);
        var (service, run) = await StartPassedRunAsync(db, clock, s);
        var assigned = await service.AssignSamplesAsync(run.Id, new List<int> { order.Id }, s.UserId);
        var id = assigned.Samples[0].Id;

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveReplicatesAsync(id, Replicates(f.Analyte.Id, (0m, 1000m)), s.UserId));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveReplicatesAsync(id, Replicates(f.Analyte.Id, (50m, 0m)), s.UserId));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveReplicatesAsync(id,
            new SaveReplicatesRequest(new List<HplcReplicateInput> { new(50m, new List<HplcReplicateResponseInput>()) }), s.UserId));
        Assert.Empty(db.HplcSampleReplicates);
    }

    [Fact]
    public async Task SaveReplicates_OnRemovedSample_Throws()
    {
        await using var db = NewDb();
        var (_, clock) = NewClock(SepFirst);
        var s = await SeedScenarioAsync(db, clock);
        var f = await SeedAssayAsync(db, s);
        var order = await AddAssayOrderAsync(db, s, f);
        var (service, run) = await StartPassedRunAsync(db, clock, s);
        var assigned = await service.AssignSamplesAsync(run.Id, new List<int> { order.Id }, s.UserId);
        await service.RemoveSampleAsync(run.Id, assigned.Samples[0].Id, "not needed", s.UserId);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SaveReplicatesAsync(assigned.Samples[0].Id, Replicates(f.Analyte.Id, (50m, 1000m), (50m, 1000m)), s.UserId));
    }

    [Fact]
    public async Task Entry_BulkStage_UsesIndividualBasisAndPreviewsEachReplicate()
    {
        await using var db = NewDb();
        var (_, clock) = NewClock(SepFirst);
        var s = await SeedScenarioAsync(db, clock);
        var f = await SeedAssayAsync(db, s, amountSpec: true);
        var order = await AddAssayOrderAsync(db, s, f, ProductionStageRole.Bulk, sampleReplicates: 2);
        var (service, run) = await StartPassedRunAsync(db, clock, s);
        var assigned = await service.AssignSamplesAsync(run.Id, new List<int> { order.Id }, s.UserId);

        var entry = await service.SaveReplicatesAsync(assigned.Samples[0].Id,
            Replicates(f.Analyte.Id, (50m, 1000m), (50m, 1100m)), s.UserId);

        Assert.Equal("Individual", entry.Basis);
        Assert.Equal(2, entry.Preview.Count);
        Assert.All(entry.Preview, p => Assert.Equal("Assay %", p.Quantity));
        Assert.Equal(ResultStatus.WithinLimits, entry.Preview[0].Status);
        Assert.Equal(ResultStatus.OutOfSpecification, entry.Preview[1].Status);
    }
}
