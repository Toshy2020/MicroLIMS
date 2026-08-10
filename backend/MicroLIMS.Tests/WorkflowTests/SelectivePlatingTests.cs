using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Domain.Enums;
using Xunit;

namespace MicroLIMS.Tests.WorkflowTests;

public class SelectivePlatingTests
{
    private static async Task<(int orderId, SeededMedia media, int incubatorId, ITestWorkflowEngine engine, MicroLIMS.Persistence.DbContext.MicroLimsDbContext db)> ReadyForPlatingAsync()
    {
        var db = PathogenTestData.NewDb();
        var (order, media, incubator) = await PathogenTestData.SeedFiveStageOrderAsync(db);
        var engine = TestServiceFactory.TestWorkflow(db);

        var start = DateTime.UtcNow.AddHours(-30);
        var end = DateTime.UtcNow.AddHours(-6);
        await engine.SubmitBrothAsync(order.Id, "Broth Enrichment", media.BrothLotId, incubator.Id, start, end, null, userId: 4);
        await engine.SubmitBrothAsync(order.Id, "Selective Broth", media.SelectiveBrothLotId, media.SelectiveBrothIncubatorId, start, end, null, userId: 4);
        return (order.Id, media, incubator.Id, engine, db);
    }

    [Theory]
    [InlineData(GrowthObservation.NoGrowth)]
    [InlineData(GrowthObservation.GrowthNonConforming)]
    public async Task NonConformingGrowth_EndsTheWorkflowAsNotDetected(GrowthObservation observation)
    {
        var (orderId, media, incubatorId, engine, db) = await ReadyForPlatingAsync();
        await using var _ = db;

        var result = await engine.SubmitSelectivePlatingAsync(orderId, "Selective Plating",
            media.SelectivePlatingLotId, incubatorId, DateTime.UtcNow.AddHours(-30), DateTime.UtcNow.AddHours(-6), observation, userId: 4);

        Assert.Equal("NotDetected", result.WorkflowFinalResult);
        Assert.False(result.NextStepUnlocked);
    }

    [Fact]
    public async Task ConformingGrowth_UnlocksTheNextStepWithoutSettingAFinalResult()
    {
        var (orderId, media, incubatorId, engine, db) = await ReadyForPlatingAsync();
        await using var _ = db;

        var result = await engine.SubmitSelectivePlatingAsync(orderId, "Selective Plating",
            media.SelectivePlatingLotId, incubatorId, DateTime.UtcNow.AddHours(-30), DateTime.UtcNow.AddHours(-6),
            GrowthObservation.GrowthConforming, userId: 4);

        Assert.Null(result.WorkflowFinalResult);
        Assert.True(result.NextStepUnlocked);
    }

    [Fact]
    public async Task Submission_SnapshotsTheExpectedAppearance()
    {
        var (orderId, media, incubatorId, engine, db) = await ReadyForPlatingAsync();
        await using var _ = db;

        await engine.SubmitSelectivePlatingAsync(orderId, "Selective Plating",
            media.SelectivePlatingLotId, incubatorId, DateTime.UtcNow.AddHours(-30), DateTime.UtcNow.AddHours(-6),
            GrowthObservation.GrowthConforming, userId: 4);

        var stored = await db.WorkflowStepResults.SingleAsync(r => r.StepName == "Selective Plating");
        Assert.Equal("Red colonies with black centres", stored.ExpectedAppearanceSnapshot);
        Assert.Equal(GrowthObservation.GrowthConforming, stored.SelectivePlatingObservation);
    }

    [Fact]
    public async Task Submission_IsNotBlockedByAnUnfinishedIncubationWindow()
    {
        var (orderId, media, incubatorId, engine, db) = await ReadyForPlatingAsync();
        await using var _ = db;

        // Selective plating has no incubation lock - a window ending in
        // the future must still be accepted.
        var result = await engine.SubmitSelectivePlatingAsync(orderId, "Selective Plating",
            media.SelectivePlatingLotId, incubatorId, DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddHours(23),
            GrowthObservation.GrowthConforming, userId: 4);

        Assert.True(result.NextStepUnlocked);
    }

    [Fact]
    public async Task Submission_WritesAPathogenObservationRow()
    {
        var (orderId, media, incubatorId, engine, db) = await ReadyForPlatingAsync();
        await using var _ = db;

        await engine.SubmitSelectivePlatingAsync(orderId, "Selective Plating",
            media.SelectivePlatingLotId, incubatorId, DateTime.UtcNow.AddHours(-30), DateTime.UtcNow.AddHours(-6),
            GrowthObservation.NoGrowth, userId: 4);

        var observation = await db.PathogenObservations.SingleAsync(o => o.TestOrderId == orderId);
        Assert.Equal(GrowthObservation.NoGrowth, observation.Observation);
        Assert.Equal(4, observation.ObservedByUserId);
    }
}
