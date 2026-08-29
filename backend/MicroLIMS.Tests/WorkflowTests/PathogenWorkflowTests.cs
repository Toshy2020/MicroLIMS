using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Domain.Enums;
using Xunit;

namespace MicroLIMS.Tests.WorkflowTests;

// End-to-end pathogen chain: Broth Enrichment -> Selective Broth ->
// Selective Plating -> Confirmatory Plating -> Biochemical Test, driven
// entirely by the seeded template. No step name is special-cased in the
// engine; this is one template shape among many.
public class PathogenWorkflowTests
{
    private const int AnalystId = 4;

    [Fact]
    public async Task FullChain_AllConformingThroughBiochemical_EndsDetected()
    {
        await using var db = PathogenTestData.NewDb();
        var (order, media, incubator) = await PathogenTestData.SeedFiveStageOrderAsync(db);
        var engine = TestServiceFactory.TestWorkflow(db);
        var start = DateTime.UtcNow.AddHours(-30);
        var end = DateTime.UtcNow.AddHours(-6);

        await engine.SubmitBrothAsync(order.Id, "Broth Enrichment", media.BrothLotId, incubator.Id, start, end, "Turbid.", AnalystId);
        await engine.SubmitBrothAsync(order.Id, "Selective Broth", media.SelectiveBrothLotId, media.SelectiveBrothIncubatorId, start, end, null, AnalystId);
        await engine.SubmitSelectivePlatingAsync(order.Id, "Selective Plating", media.SelectivePlatingLotId, incubator.Id,
            start, end, GrowthObservation.GrowthConforming, AnalystId);
        await engine.SubmitConfirmatorySetupAsync(order.Id, "Confirmatory Plating", new[]
        {
            new ConfirmatorySelectionInput(media.XldStepMediaId, media.XldLotId, incubator.Id),
            new ConfirmatorySelectionInput(media.TsiStepMediaId, media.TsiLotId, incubator.Id)
        }, start, end, AnalystId);
        await engine.SubmitConfirmatoryObservationsAsync(order.Id, "Confirmatory Plating", new[]
        {
            new ConfirmatoryObservationInput(media.XldMaterialId, GrowthObservation.GrowthConforming),
            new ConfirmatoryObservationInput(media.TsiMaterialId, GrowthObservation.GrowthConforming)
        }, AnalystId);
        await engine.RecordAnalystDecisionAsync(order.Id, AnalystDecision.ProceedToBiochemical, AnalystId);
        var final = await engine.SubmitBiochemicalAsync(order.Id, "Biochemical Test", "IMViC: + + - -", null, true, AnalystId);

        Assert.Equal("Detected", final.WorkflowFinalResult);

        var reloaded = await db.TestOrders.SingleAsync(t => t.Id == order.Id);
        Assert.Equal(WorkflowStep.Ready, reloaded.CurrentStep);
        Assert.Single(await db.Results.Where(r => r.TestOrderId == order.Id).ToListAsync());
    }

    [Fact]
    public async Task Chain_StopsAtSelectivePlating_WhenGrowthIsNonConforming()
    {
        await using var db = PathogenTestData.NewDb();
        var (order, media, incubator) = await PathogenTestData.SeedFiveStageOrderAsync(db);
        var engine = TestServiceFactory.TestWorkflow(db);
        var start = DateTime.UtcNow.AddHours(-30);
        var end = DateTime.UtcNow.AddHours(-6);

        await engine.SubmitBrothAsync(order.Id, "Broth Enrichment", media.BrothLotId, incubator.Id, start, end, null, AnalystId);
        await engine.SubmitBrothAsync(order.Id, "Selective Broth", media.SelectiveBrothLotId, media.SelectiveBrothIncubatorId, start, end, null, AnalystId);
        var result = await engine.SubmitSelectivePlatingAsync(order.Id, "Selective Plating", media.SelectivePlatingLotId,
            incubator.Id, start, end, GrowthObservation.GrowthNonConforming, AnalystId);

        Assert.Equal("NotDetected", result.WorkflowFinalResult);
        Assert.Empty(await db.WorkflowStepResults.Where(r => r.StepType == StepType.ConfirmatoryPlating).ToListAsync());
    }

    [Fact]
    public async Task BrothSteps_DoNotBranchOnTheirObservationText()
    {
        await using var db = PathogenTestData.NewDb();
        var (order, media, incubator) = await PathogenTestData.SeedFiveStageOrderAsync(db);
        var engine = TestServiceFactory.TestWorkflow(db);
        var start = DateTime.UtcNow.AddHours(-30);
        var end = DateTime.UtcNow.AddHours(-6);

        // "No turbidity" is recorded verbatim and changes nothing - broth
        // steps carry no result logic (spec 3.4).
        var result = await engine.SubmitBrothAsync(order.Id, "Broth Enrichment", media.BrothLotId, incubator.Id,
            start, end, "No turbidity observed.", AnalystId);

        Assert.Null(result.WorkflowFinalResult);
        Assert.True(result.NextStepUnlocked);
    }

    [Fact]
    public async Task SelectingAnUnreleasedLot_IsRejected()
    {
        await using var db = PathogenTestData.NewDb();
        var (order, media, incubator) = await PathogenTestData.SeedFiveStageOrderAsync(db);

        var lot = await db.Media.SingleAsync(m => m.Id == media.BrothLotId);
        lot.IsReleasedForUse = false;
        await db.SaveChangesAsync();

        var engine = TestServiceFactory.TestWorkflow(db);
        await Assert.ThrowsAsync<InvalidOperationException>(() => engine.SubmitBrothAsync(
            order.Id, "Broth Enrichment", media.BrothLotId, incubator.Id,
            DateTime.UtcNow.AddHours(-30), DateTime.UtcNow.AddHours(-6), null, AnalystId));
    }
}
