using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Constants;
using Xunit;

namespace MicroLIMS.Tests.WorkflowTests;

public class ConfirmatoryPlatingTests
{
    private static async Task<(int orderId, SeededMedia media, int incubatorId, ITestWorkflowEngine engine, MicroLimsDbContext db)> ReadyForConfirmatoryAsync()
    {
        var db = PathogenTestData.NewDb();
        var (order, media, incubator) = await PathogenTestData.SeedFiveStageOrderAsync(db);
        var engine = TestServiceFactory.TestWorkflow(db);
        var start = DateTime.UtcNow.AddHours(-30);
        var end = DateTime.UtcNow.AddHours(-6);

        await engine.SubmitBrothAsync(order.Id, "Broth Enrichment", media.BrothLotId, incubator.Id, start, end, null, 4);
        await engine.SubmitBrothAsync(order.Id, "Selective Broth", media.SelectiveBrothLotId, media.SelectiveBrothIncubatorId, start, end, null, 4);
        await engine.SubmitSelectivePlatingAsync(order.Id, "Selective Plating", media.SelectivePlatingLotId, incubator.Id,
            start, end, GrowthObservation.GrowthConforming, 4);
        return (order.Id, media, incubator.Id, engine, db);
    }

    private static ConfirmatorySelectionInput[] BothMedia(SeededMedia media, int incubatorId) => new[]
    {
        new ConfirmatorySelectionInput(media.XldStepMediaId, media.XldLotId, incubatorId),
        new ConfirmatorySelectionInput(media.TsiStepMediaId, media.TsiLotId, incubatorId)
    };

    [Fact]
    public async Task Setup_RecordsOneSelectionPerChosenMedium()
    {
        var (orderId, media, incubatorId, engine, db) = await ReadyForConfirmatoryAsync();
        await using var _ = db;

        await engine.SubmitConfirmatorySetupAsync(orderId, "Confirmatory Plating", BothMedia(media, incubatorId),
            DateTime.UtcNow.AddHours(-30), DateTime.UtcNow.AddHours(-6), 4);

        var result = await db.WorkflowStepResults.Include(r => r.Selections).SingleAsync(r => r.StepName == "Confirmatory Plating");
        Assert.Equal(2, result.Selections.Count);
        Assert.All(result.Selections, s => Assert.False(s.WasAnalystAdded));
    }

    [Fact]
    public async Task Setup_WithNoMedia_ThrowsNoMediaSelected()
    {
        var (orderId, _, _, engine, db) = await ReadyForConfirmatoryAsync();
        await using var _db = db;

        var ex = await Assert.ThrowsAsync<WorkflowStepException>(() => engine.SubmitConfirmatorySetupAsync(
            orderId, "Confirmatory Plating", Array.Empty<ConfirmatorySelectionInput>(),
            DateTime.UtcNow.AddHours(-30), DateTime.UtcNow.AddHours(-6), 4));

        Assert.Equal(WorkflowErrorCodes.NoMediaSelected, ex.ErrorCode);
    }

    [Fact]
    public async Task Setup_WithAMediumFromAnotherStep_ThrowsMediaNotInPermittedList()
    {
        var (orderId, media, incubatorId, engine, db) = await ReadyForConfirmatoryAsync();
        await using var _ = db;

        // The selective plating step's own medium is not on the
        // confirmatory step's permitted list.
        var foreign = await db.TestWorkflowStepMedias
            .Include(m => m.TestWorkflowStep)
            .FirstAsync(m => m.TestWorkflowStep!.StepType == StepType.SelectivePlating);

        var ex = await Assert.ThrowsAsync<WorkflowStepException>(() => engine.SubmitConfirmatorySetupAsync(
            orderId, "Confirmatory Plating",
            new[] { new ConfirmatorySelectionInput(foreign.Id, media.SelectivePlatingLotId, incubatorId) },
            DateTime.UtcNow.AddHours(-30), DateTime.UtcNow.AddHours(-6), 4));

        Assert.Equal(WorkflowErrorCodes.MediaNotInPermittedList, ex.ErrorCode);
    }

    [Fact]
    public async Task Observations_AllConforming_RequiresAnAnalystDecision()
    {
        var (orderId, media, incubatorId, engine, db) = await ReadyForConfirmatoryAsync();
        await using var _ = db;
        await engine.SubmitConfirmatorySetupAsync(orderId, "Confirmatory Plating", BothMedia(media, incubatorId),
            DateTime.UtcNow.AddHours(-30), DateTime.UtcNow.AddHours(-6), 4);

        var outcome = await engine.SubmitConfirmatoryObservationsAsync(orderId, "Confirmatory Plating", new[]
        {
            new ConfirmatoryObservationInput(media.XldMaterialId, GrowthObservation.GrowthConforming),
            new ConfirmatoryObservationInput(media.TsiMaterialId, GrowthObservation.GrowthConforming)
        }, 4);

        Assert.Equal("AllConforming", outcome.ConfirmatoryResult);
        Assert.True(outcome.AnalystDecisionRequired);
        Assert.Empty(outcome.Flags);
    }

    [Theory]
    [InlineData(GrowthObservation.NoGrowth)]
    [InlineData(GrowthObservation.GrowthNonConforming)]
    public async Task Observations_MixedResults_AreInconclusiveAndFlagged(GrowthObservation second)
    {
        var (orderId, media, incubatorId, engine, db) = await ReadyForConfirmatoryAsync();
        await using var _ = db;
        await engine.SubmitConfirmatorySetupAsync(orderId, "Confirmatory Plating", BothMedia(media, incubatorId),
            DateTime.UtcNow.AddHours(-30), DateTime.UtcNow.AddHours(-6), 4);

        var outcome = await engine.SubmitConfirmatoryObservationsAsync(orderId, "Confirmatory Plating", new[]
        {
            new ConfirmatoryObservationInput(media.XldMaterialId, GrowthObservation.GrowthConforming),
            new ConfirmatoryObservationInput(media.TsiMaterialId, second)
        }, 4);

        Assert.Equal("Inconclusive", outcome.ConfirmatoryResult);
        Assert.False(outcome.AnalystDecisionRequired);
        Assert.Contains("InconclusiveResult", outcome.Flags);
    }

    [Fact]
    public async Task Observations_SnapshotExpectedAppearancePerMedium()
    {
        var (orderId, media, incubatorId, engine, db) = await ReadyForConfirmatoryAsync();
        await using var _ = db;
        await engine.SubmitConfirmatorySetupAsync(orderId, "Confirmatory Plating", BothMedia(media, incubatorId),
            DateTime.UtcNow.AddHours(-30), DateTime.UtcNow.AddHours(-6), 4);

        await engine.SubmitConfirmatoryObservationsAsync(orderId, "Confirmatory Plating", new[]
        {
            new ConfirmatoryObservationInput(media.XldMaterialId, GrowthObservation.GrowthConforming),
            new ConfirmatoryObservationInput(media.TsiMaterialId, GrowthObservation.GrowthConforming)
        }, 4);

        var observations = await db.ConfirmatoryPlateObservations.ToListAsync();
        Assert.Equal(2, observations.Count);
        Assert.Contains(observations, o => o.ExpectedAppearanceSnapshot == "Red colonies with black centres");
        Assert.Contains(observations, o => o.ExpectedAppearanceSnapshot == "Alkaline slant, acid butt, H2S positive");
    }

    [Fact]
    public async Task Observations_BeforeTheWindowEnds_ThrowIncubationNotComplete()
    {
        var (orderId, media, incubatorId, engine, db) = await ReadyForConfirmatoryAsync();
        await using var _ = db;
        await engine.SubmitConfirmatorySetupAsync(orderId, "Confirmatory Plating", BothMedia(media, incubatorId),
            DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddHours(23), 4);

        var ex = await Assert.ThrowsAsync<WorkflowStepException>(() => engine.SubmitConfirmatoryObservationsAsync(
            orderId, "Confirmatory Plating",
            new[] { new ConfirmatoryObservationInput(media.XldMaterialId, GrowthObservation.GrowthConforming) }, 4));

        Assert.Equal(WorkflowErrorCodes.IncubationNotComplete, ex.ErrorCode);
    }

    [Fact]
    public async Task Observations_MissingAMediumThatWasSetUp_ThrowsIncompleteSetup()
    {
        var (orderId, media, incubatorId, engine, db) = await ReadyForConfirmatoryAsync();
        await using var _ = db;
        await engine.SubmitConfirmatorySetupAsync(orderId, "Confirmatory Plating", BothMedia(media, incubatorId),
            DateTime.UtcNow.AddHours(-30), DateTime.UtcNow.AddHours(-6), 4);

        var ex = await Assert.ThrowsAsync<WorkflowStepException>(() => engine.SubmitConfirmatoryObservationsAsync(
            orderId, "Confirmatory Plating",
            new[] { new ConfirmatoryObservationInput(media.XldMaterialId, GrowthObservation.GrowthConforming) }, 4));

        Assert.Equal(WorkflowErrorCodes.IncompleteConfirmatorySetup, ex.ErrorCode);
    }
}
