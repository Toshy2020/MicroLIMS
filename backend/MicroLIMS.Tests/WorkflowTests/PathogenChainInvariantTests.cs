using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Constants;
using Xunit;

namespace MicroLIMS.Tests.WorkflowTests;

// Invariants that span the whole five-stage chain rather than any one
// step: chain order, single submission per step, the confirmatory
// read-out being final, and the analyst decision point. Each step's own
// validation is covered by IncubationLockTests / SelectivePlatingTests /
// ConfirmatoryPlatingTests / BiochemicalReviewTests.
public class PathogenChainInvariantTests
{
    private const int AnalystId = 4;
    private const int ReviewerId = 9;

    private static DateTime Start => DateTime.UtcNow.AddHours(-30);
    private static DateTime End => DateTime.UtcNow.AddHours(-6);

    private static async Task<(TestOrder Order, SeededMedia Media, Equipment Incubator, ITestWorkflowEngine Engine, MicroLimsDbContext Db)>
        NewChainAsync()
    {
        var db = PathogenTestData.NewDb();
        var (order, media, incubator) = await PathogenTestData.SeedFiveStageOrderAsync(db);
        return (order, media, incubator, TestServiceFactory.TestWorkflow(db), db);
    }

    private static async Task BothBrothStepsAsync(ITestWorkflowEngine engine, TestOrder order, SeededMedia media, Equipment incubator)
    {
        await engine.SubmitBrothAsync(order.Id, "Broth Enrichment", media.BrothLotId, incubator.Id, Start, End, null, AnalystId);
        await engine.SubmitBrothAsync(order.Id, "Selective Broth", media.SelectiveBrothLotId, media.SelectiveBrothIncubatorId,
            Start, End, null, AnalystId);
    }

    private static ConfirmatorySelectionInput[] BothMedia(SeededMedia media, int incubatorId) => new[]
    {
        new ConfirmatorySelectionInput(media.XldStepMediaId, media.XldLotId, incubatorId),
        new ConfirmatorySelectionInput(media.TsiStepMediaId, media.TsiLotId, incubatorId)
    };

    // Drives the chain as far as an all-conforming confirmatory read-out,
    // leaving the analyst decision outstanding.
    private static async Task ThroughConfirmatoryAsync(
        ITestWorkflowEngine engine, TestOrder order, SeededMedia media, Equipment incubator,
        GrowthObservation secondConfirmatoryPlate = GrowthObservation.GrowthConforming)
    {
        await BothBrothStepsAsync(engine, order, media, incubator);
        await engine.SubmitSelectivePlatingAsync(order.Id, "Selective Plating", media.SelectivePlatingLotId, incubator.Id,
            Start, End, GrowthObservation.GrowthConforming, AnalystId);
        await engine.SubmitConfirmatorySetupAsync(order.Id, "Confirmatory Plating", BothMedia(media, incubator.Id), Start, End, AnalystId);
        await engine.SubmitConfirmatoryObservationsAsync(order.Id, "Confirmatory Plating", new[]
        {
            new ConfirmatoryObservationInput(media.XldMaterialId, GrowthObservation.GrowthConforming),
            new ConfirmatoryObservationInput(media.TsiMaterialId, secondConfirmatoryPlate)
        }, AnalystId);
    }

    // B3: a step could be submitted on a fresh order with every earlier
    // step never performed, and the chain then ran on normally to a
    // reportable result.
    [Fact]
    public async Task SelectivePlating_WithBothBrothStepsNeverPerformed_IsRejected()
    {
        var (order, media, incubator, engine, db) = await NewChainAsync();
        await using var _ = db;

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => engine.SubmitSelectivePlatingAsync(
            order.Id, "Selective Plating", media.SelectivePlatingLotId, incubator.Id,
            Start, End, GrowthObservation.GrowthConforming, AnalystId));

        Assert.Contains("Broth Enrichment", ex.Message);
        Assert.Empty(await db.WorkflowStepResults.Where(r => r.TestOrderId == order.Id).ToListAsync());
    }

    [Fact]
    public async Task ConfirmatorySetup_BeforeSelectivePlating_IsRejected()
    {
        var (order, media, incubator, engine, db) = await NewChainAsync();
        await using var _ = db;
        await BothBrothStepsAsync(engine, order, media, incubator);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => engine.SubmitConfirmatorySetupAsync(
            order.Id, "Confirmatory Plating", BothMedia(media, incubator.Id), Start, End, AnalystId));

        Assert.Contains("Selective Plating", ex.Message);
    }

    // B3: no test anywhere re-submitted a step. A second submission used
    // to append a second Incubation + WorkflowStepResult for the same step.
    [Fact]
    public async Task Broth_SubmittedTwice_IsRejectedAndLeavesOneResultRow()
    {
        var (order, media, incubator, engine, db) = await NewChainAsync();
        await using var _ = db;

        await engine.SubmitBrothAsync(order.Id, "Broth Enrichment", media.BrothLotId, incubator.Id, Start, End, "Turbid.", AnalystId);

        await Assert.ThrowsAsync<InvalidOperationException>(() => engine.SubmitBrothAsync(
            order.Id, "Broth Enrichment", media.BrothLotId, incubator.Id, Start, End, "Turbid again.", AnalystId));

        Assert.Single(await db.WorkflowStepResults.Where(r => r.StepName == "Broth Enrichment").ToListAsync());
        Assert.Single(await db.Incubations.Where(i => i.StepName == "Broth Enrichment").ToListAsync());
    }

    // A finalized order is done being submitted against - previously a
    // step could be re-submitted onto an order already sitting at Ready
    // and would report NextStepUnlocked = true.
    [Fact]
    public async Task SelectivePlating_ResubmittedAfterTheOrderIsFinalized_IsRejected()
    {
        var (order, media, incubator, engine, db) = await NewChainAsync();
        await using var _ = db;
        await BothBrothStepsAsync(engine, order, media, incubator);
        await engine.SubmitSelectivePlatingAsync(order.Id, "Selective Plating", media.SelectivePlatingLotId, incubator.Id,
            Start, End, GrowthObservation.GrowthNonConforming, AnalystId);

        var finalized = await db.TestOrders.SingleAsync(t => t.Id == order.Id);
        Assert.Equal(WorkflowStep.Ready, finalized.CurrentStep);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => engine.SubmitSelectivePlatingAsync(
            order.Id, "Selective Plating", media.SelectivePlatingLotId, incubator.Id,
            Start, End, GrowthObservation.GrowthConforming, AnalystId));

        Assert.Contains("Ready", ex.Message);
        Assert.Single(await db.Results.Where(r => r.TestOrderId == order.Id).ToListAsync());
    }

    // B5: nothing called GetCurrentStepAsync on a pathogen order. It read
    // "done" purely from PathogenObservations, which only selective
    // plating writes, so the chain never advanced past step 1.
    [Fact]
    public async Task GetCurrentStepAsync_AdvancesAsEachPathogenStepIsSubmitted()
    {
        var (order, media, incubator, engine, db) = await NewChainAsync();
        await using var _ = db;

        var current = await engine.GetCurrentStepAsync(order.Id);
        Assert.Equal("Broth Enrichment", current.Step!.StepName);
        Assert.Empty(current.CompletedSteps);

        await engine.SubmitBrothAsync(order.Id, "Broth Enrichment", media.BrothLotId, incubator.Id, Start, End, null, AnalystId);
        current = await engine.GetCurrentStepAsync(order.Id);
        Assert.Equal("Selective Broth", current.Step!.StepName);
        Assert.Single(current.CompletedSteps);

        await engine.SubmitBrothAsync(order.Id, "Selective Broth", media.SelectiveBrothLotId, media.SelectiveBrothIncubatorId,
            Start, End, null, AnalystId);
        current = await engine.GetCurrentStepAsync(order.Id);
        Assert.Equal("Selective Plating", current.Step!.StepName);
        Assert.Equal(2, current.CompletedSteps.Count);

        await engine.SubmitSelectivePlatingAsync(order.Id, "Selective Plating", media.SelectivePlatingLotId, incubator.Id,
            Start, End, GrowthObservation.GrowthConforming, AnalystId);
        current = await engine.GetCurrentStepAsync(order.Id);
        Assert.Equal("Confirmatory Plating", current.Step!.StepName);
        Assert.Equal(3, current.CompletedSteps.Count);

        // Setup alone does not complete confirmatory plating - the plates
        // still have to be read.
        await engine.SubmitConfirmatorySetupAsync(order.Id, "Confirmatory Plating", BothMedia(media, incubator.Id), Start, End, AnalystId);
        current = await engine.GetCurrentStepAsync(order.Id);
        Assert.Equal("Confirmatory Plating", current.Step!.StepName);

        await engine.SubmitConfirmatoryObservationsAsync(order.Id, "Confirmatory Plating", new[]
        {
            new ConfirmatoryObservationInput(media.XldMaterialId, GrowthObservation.GrowthConforming),
            new ConfirmatoryObservationInput(media.TsiMaterialId, GrowthObservation.GrowthConforming)
        }, AnalystId);
        current = await engine.GetCurrentStepAsync(order.Id);
        Assert.Equal("Biochemical Test", current.Step!.StepName);
        Assert.Equal(4, current.CompletedSteps.Count);

        await engine.RecordAnalystDecisionAsync(order.Id, AnalystDecision.ProceedToBiochemical, AnalystId);
        await engine.SubmitBiochemicalAsync(order.Id, "Biochemical Test", "IMViC: + + - -", null, AnalystId);

        current = await engine.GetCurrentStepAsync(order.Id);
        Assert.True(current.AllStepsComplete);
        Assert.Null(current.Step);
        Assert.Equal("Detected", current.FinalResult);
    }

    // Also proves ValidateAsync/AdvanceAsync, which both read
    // GetCurrentStepAsync, no longer block a completed pathogen chain.
    [Fact]
    public async Task ValidateAsync_OnAnIncompletePathogenChain_ReportsTheOutstandingSteps()
    {
        var (order, media, incubator, engine, db) = await NewChainAsync();
        await using var _ = db;
        await BothBrothStepsAsync(engine, order, media, incubator);

        var errors = await engine.ValidateAsync(order.Id);
        Assert.NotEmpty(errors);
    }

    // B2: re-running setup after an Inconclusive read-out minted a fresh
    // result row that every reader preferred, turning an inconclusive run
    // into a reportable Detected with no record of the re-run.
    [Fact]
    public async Task ConfirmatorySetup_AfterAnInconclusiveReadOut_IsRejected()
    {
        var (order, media, incubator, engine, db) = await NewChainAsync();
        await using var _ = db;
        await ThroughConfirmatoryAsync(engine, order, media, incubator, GrowthObservation.GrowthNonConforming);

        var ex = await Assert.ThrowsAsync<WorkflowStepException>(() => engine.SubmitConfirmatorySetupAsync(
            order.Id, "Confirmatory Plating", BothMedia(media, incubator.Id), Start, End, AnalystId));

        Assert.Equal(WorkflowErrorCodes.ConfirmatoryAlreadyRecorded, ex.ErrorCode);

        var results = await db.WorkflowStepResults.Where(r => r.StepName == "Confirmatory Plating").ToListAsync();
        var only = Assert.Single(results);
        Assert.Equal(ConfirmatoryResult.Inconclusive, only.ConfirmatoryResult);

        // And the inconclusive run still cannot be talked into a decision.
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => engine.RecordAnalystDecisionAsync(order.Id, AnalystDecision.SubmitAsDetected, AnalystId));
    }

    [Fact]
    public async Task ConfirmatorySetup_AfterAnAllConformingReadOut_IsRejected()
    {
        var (order, media, incubator, engine, db) = await NewChainAsync();
        await using var _ = db;
        await ThroughConfirmatoryAsync(engine, order, media, incubator);

        var ex = await Assert.ThrowsAsync<WorkflowStepException>(() => engine.SubmitConfirmatorySetupAsync(
            order.Id, "Confirmatory Plating", BothMedia(media, incubator.Id), Start, End, AnalystId));

        Assert.Equal(WorkflowErrorCodes.ConfirmatoryAlreadyRecorded, ex.ErrorCode);
    }

    // The unique index on (WorkflowStepResultId, MaterialId) is not
    // enforced by the InMemory provider, so this has to be a code rule or
    // it becomes a DbUpdateException/500 on PostgreSQL only.
    [Fact]
    public async Task ConfirmatoryObservations_ReadingTheSameMediumTwice_IsRejected()
    {
        var (order, media, incubator, engine, db) = await NewChainAsync();
        await using var _ = db;
        await BothBrothStepsAsync(engine, order, media, incubator);
        await engine.SubmitSelectivePlatingAsync(order.Id, "Selective Plating", media.SelectivePlatingLotId, incubator.Id,
            Start, End, GrowthObservation.GrowthConforming, AnalystId);
        await engine.SubmitConfirmatorySetupAsync(order.Id, "Confirmatory Plating",
            new[] { new ConfirmatorySelectionInput(media.XldStepMediaId, media.XldLotId, incubator.Id) }, Start, End, AnalystId);

        var ex = await Assert.ThrowsAsync<WorkflowStepException>(() => engine.SubmitConfirmatoryObservationsAsync(
            order.Id, "Confirmatory Plating", new[]
            {
                new ConfirmatoryObservationInput(media.XldMaterialId, GrowthObservation.GrowthConforming),
                new ConfirmatoryObservationInput(media.XldMaterialId, GrowthObservation.NoGrowth)
            }, AnalystId));

        Assert.Equal(WorkflowErrorCodes.IncompleteConfirmatorySetup, ex.ErrorCode);
        Assert.Empty(await db.ConfirmatoryPlateObservations.ToListAsync());
    }

    // The analyst decision was re-runnable: two SubmitAsDetected calls
    // produced two Result rows and a meaningless Ready -> Ready history
    // entry.
    [Fact]
    public async Task AnalystDecision_SubmittedTwice_IsRejectedAndLeavesOneResult()
    {
        var (order, media, incubator, engine, db) = await NewChainAsync();
        await using var _ = db;
        await ThroughConfirmatoryAsync(engine, order, media, incubator);

        await engine.RecordAnalystDecisionAsync(order.Id, AnalystDecision.SubmitAsDetected, AnalystId);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => engine.RecordAnalystDecisionAsync(order.Id, AnalystDecision.SubmitAsDetected, AnalystId));

        Assert.Single(await db.Results.Where(r => r.TestOrderId == order.Id).ToListAsync());
        Assert.Empty(await db.WorkflowHistories
            .Where(h => h.TestOrderId == order.Id && h.FromStep == WorkflowStep.Ready && h.ToStep == WorkflowStep.Ready)
            .ToListAsync());
    }

    // The decision itself is single-shot, independently of the order
    // being finalized: ProceedToBiochemical leaves the order mid-chain,
    // so nothing else stops it being answered twice.
    [Fact]
    public async Task AnalystDecision_ProceedToBiochemicalTwice_IsRejectedAndRecordsOneDecision()
    {
        var (order, media, incubator, engine, db) = await NewChainAsync();
        await using var _ = db;
        await ThroughConfirmatoryAsync(engine, order, media, incubator);

        await engine.RecordAnalystDecisionAsync(order.Id, AnalystDecision.ProceedToBiochemical, AnalystId);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => engine.RecordAnalystDecisionAsync(order.Id, AnalystDecision.ProceedToBiochemical, AnalystId));
        Assert.Contains("already recorded", ex.Message);

        Assert.Single(await db.WorkflowHistories
            .Where(h => h.TestOrderId == order.Id && h.Note != null && h.Note.Contains("proceed to biochemical"))
            .ToListAsync());
    }

    // And a decision cannot be changed after the fact by asking for the
    // other branch.
    [Fact]
    public async Task AnalystDecision_CannotBeSwitchedAfterProceedingToBiochemical()
    {
        var (order, media, incubator, engine, db) = await NewChainAsync();
        await using var _ = db;
        await ThroughConfirmatoryAsync(engine, order, media, incubator);
        await engine.RecordAnalystDecisionAsync(order.Id, AnalystDecision.ProceedToBiochemical, AnalystId);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => engine.RecordAnalystDecisionAsync(order.Id, AnalystDecision.SubmitAsDetected, AnalystId));

        Assert.Empty(await db.Results.Where(r => r.TestOrderId == order.Id).ToListAsync());
        var confirmatory = await db.WorkflowStepResults.SingleAsync(r => r.StepName == "Confirmatory Plating");
        Assert.False(confirmatory.SkippedBiochemical);
    }

    // The ProceedToBiochemical branch used to persist nothing at all - a
    // GMP decision point with no contemporaneous record.
    [Fact]
    public async Task AnalystDecision_ProceedToBiochemical_IsRecorded()
    {
        var (order, media, incubator, engine, db) = await NewChainAsync();
        await using var _ = db;
        await ThroughConfirmatoryAsync(engine, order, media, incubator);

        await engine.RecordAnalystDecisionAsync(order.Id, AnalystDecision.ProceedToBiochemical, AnalystId);

        var confirmatory = await db.WorkflowStepResults.SingleAsync(r => r.StepName == "Confirmatory Plating");
        Assert.Equal(AnalystDecision.ProceedToBiochemical, confirmatory.AnalystDecision);
        Assert.Equal(AnalystId, confirmatory.AnalystDecisionByUserId);
        Assert.NotNull(confirmatory.AnalystDecisionAtUtc);

        var history = await db.WorkflowHistories
            .Where(h => h.TestOrderId == order.Id && h.Note != null && h.Note.Contains("proceed to biochemical"))
            .ToListAsync();
        Assert.Single(history);
        Assert.Equal(AnalystId, history[0].PerformedByUserId);
    }

    // The AddPathogenWorkflowRefactor migration types legacy steps as
    // SelectivePlating/ConfirmatoryPlating without backfilling a target
    // organism, so a migrated template must fail with an instruction, not
    // a null dereference.
    [Fact]
    public async Task SelectivePlating_WithNoTargetOrganismConfigured_FailsWithATemplateMessage()
    {
        var (order, media, incubator, engine, db) = await NewChainAsync();
        await using var _ = db;
        await BothBrothStepsAsync(engine, order, media, incubator);

        var step = await db.TestWorkflowSteps.SingleAsync(s => s.StepName == "Selective Plating");
        step.TargetOrganismId = null;
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => engine.SubmitSelectivePlatingAsync(
            order.Id, "Selective Plating", media.SelectivePlatingLotId, incubator.Id,
            Start, End, GrowthObservation.GrowthConforming, AnalystId));

        Assert.Contains("Test Master", ex.Message);
        Assert.Contains("Selective Plating", ex.Message);
    }

    [Fact]
    public async Task Broth_WithNoAssignedMedium_FailsWithATemplateMessage()
    {
        var (order, media, incubator, engine, db) = await NewChainAsync();
        await using var _ = db;

        var step = await db.TestWorkflowSteps.SingleAsync(s => s.StepName == "Broth Enrichment");
        db.TestWorkflowStepMedias.RemoveRange(
            await db.TestWorkflowStepMedias.Where(m => m.TestWorkflowStepId == step.Id).ToListAsync());
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => engine.SubmitBrothAsync(
            order.Id, "Broth Enrichment", media.BrothLotId, incubator.Id, Start, End, null, AnalystId));

        Assert.Contains("Test Master", ex.Message);
    }
}
