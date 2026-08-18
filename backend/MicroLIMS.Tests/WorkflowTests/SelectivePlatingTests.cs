using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.API.Controllers;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;
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

    [Fact]
    public async Task StartIncubation_CreatesOpenIncubationRecord()
    {
        var (orderId, media, incubatorId, engine, db) = await ReadyForPlatingAsync();
        await using var _ = db;

        var start = DateTime.UtcNow;
        var incubation = await engine.StartSelectivePlatingIncubationAsync(
            orderId, "Selective Plating", media.SelectivePlatingLotId, incubatorId, start, userId: 4);

        Assert.NotNull(incubation);
        Assert.Null(incubation.CompletedAt);
        Assert.Equal(start, incubation.IncubationStartUtc);
        Assert.NotNull(incubation.IncubationEndUtc);
        Assert.True(incubation.IncubationEndUtc > incubation.IncubationStartUtc);

        var stored = await db.Incubations.SingleAsync(i => i.TestOrderId == orderId && i.StepName == "Selective Plating");
        Assert.Null(stored.CompletedAt);
    }

    [Fact]
    public async Task Observation_BeforeMinHours_ThrowsIncubationNotComplete()
    {
        var (orderId, media, incubatorId, engine, db) = await ReadyForPlatingAsync();
        await using var _ = db;

        // Incubation started just 1 hour ago (minHours is 18 or 24h)
        var start = DateTime.UtcNow.AddHours(-1);
        await engine.StartSelectivePlatingIncubationAsync(
            orderId, "Selective Plating", media.SelectivePlatingLotId, incubatorId, start, userId: 4);

        var ex = await Assert.ThrowsAsync<WorkflowStepException>(() =>
            engine.SubmitSelectivePlatingObservationAsync(orderId, "Selective Plating", GrowthObservation.GrowthConforming, null, userId: 4));

        Assert.Equal(WorkflowErrorCodes.IncubationNotComplete, ex.ErrorCode);
    }

    [Fact]
    public async Task Observation_WithoutStartingIncubation_Throws()
    {
        var (orderId, _, _, engine, db) = await ReadyForPlatingAsync();
        await using var _ = db;

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            engine.SubmitSelectivePlatingObservationAsync(orderId, "Selective Plating", GrowthObservation.GrowthConforming, null, userId: 4));

        Assert.Contains("No active incubation found", ex.Message);
    }

    [Theory]
    [InlineData(GrowthObservation.NoGrowth)]
    [InlineData(GrowthObservation.GrowthNonConforming)]
    public async Task NonConformingGrowth_EndsTheWorkflowAsNotDetected(GrowthObservation observation)
    {
        var (orderId, media, incubatorId, engine, db) = await ReadyForPlatingAsync();
        await using var _ = db;

        var start = DateTime.UtcNow.AddHours(-30);
        await engine.StartSelectivePlatingIncubationAsync(
            orderId, "Selective Plating", media.SelectivePlatingLotId, incubatorId, start, userId: 4);

        var result = await engine.SubmitSelectivePlatingObservationAsync(
            orderId, "Selective Plating", observation, "Test note", userId: 4);

        Assert.Equal("NotDetected", result.WorkflowFinalResult);
        Assert.False(result.NextStepUnlocked);

        var inc = await db.Incubations.SingleAsync(i => i.TestOrderId == orderId && i.StepName == "Selective Plating");
        Assert.NotNull(inc.CompletedAt);
        Assert.Equal(observation.ToString(), inc.Outcome);
    }

    [Fact]
    public async Task ConformingGrowth_UnlocksTheNextStepWithoutSettingAFinalResult()
    {
        var (orderId, media, incubatorId, engine, db) = await ReadyForPlatingAsync();
        await using var _ = db;

        var start = DateTime.UtcNow.AddHours(-30);
        await engine.StartSelectivePlatingIncubationAsync(
            orderId, "Selective Plating", media.SelectivePlatingLotId, incubatorId, start, userId: 4);

        var result = await engine.SubmitSelectivePlatingObservationAsync(
            orderId, "Selective Plating", GrowthObservation.GrowthConforming, null, userId: 4);

        Assert.Null(result.WorkflowFinalResult);
        Assert.True(result.NextStepUnlocked);
    }

    [Fact]
    public async Task Submission_SnapshotsTheExpectedAppearance()
    {
        var (orderId, media, incubatorId, engine, db) = await ReadyForPlatingAsync();
        await using var _ = db;

        var start = DateTime.UtcNow.AddHours(-30);
        await engine.StartSelectivePlatingIncubationAsync(
            orderId, "Selective Plating", media.SelectivePlatingLotId, incubatorId, start, userId: 4);

        await engine.SubmitSelectivePlatingObservationAsync(
            orderId, "Selective Plating", GrowthObservation.GrowthConforming, null, userId: 4);

        var stored = await db.WorkflowStepResults.SingleAsync(r => r.StepName == "Selective Plating");
        Assert.Equal("Red colonies with black centres", stored.ExpectedAppearanceSnapshot);
        Assert.Equal(GrowthObservation.GrowthConforming, stored.SelectivePlatingObservation);
    }

    [Fact]
    public async Task Submission_WritesAPathogenObservationRow()
    {
        var (orderId, media, incubatorId, engine, db) = await ReadyForPlatingAsync();
        await using var _ = db;

        var start = DateTime.UtcNow.AddHours(-30);
        await engine.StartSelectivePlatingIncubationAsync(
            orderId, "Selective Plating", media.SelectivePlatingLotId, incubatorId, start, userId: 4);

        await engine.SubmitSelectivePlatingObservationAsync(
            orderId, "Selective Plating", GrowthObservation.NoGrowth, null, userId: 4);

        var observation = await db.PathogenObservations.SingleAsync(o => o.TestOrderId == orderId);
        Assert.Equal(GrowthObservation.NoGrowth, observation.Observation);
        Assert.Equal(4, observation.ObservedByUserId);
    }

    [Fact]
    public async Task Submission_ProjectsAResultRecordForTheFinalizedWorkflow()
    {
        var (orderId, media, incubatorId, engine, db) = await ReadyForPlatingAsync();
        await using var _ = db;

        var start = DateTime.UtcNow.AddHours(-30);
        await engine.StartSelectivePlatingIncubationAsync(
            orderId, "Selective Plating", media.SelectivePlatingLotId, incubatorId, start, userId: 4);

        await engine.SubmitSelectivePlatingObservationAsync(
            orderId, "Selective Plating", GrowthObservation.NoGrowth, null, userId: 4);

        var record = await db.ResultRecords.SingleAsync(r => r.TestOrderId == orderId);
        Assert.Equal("Not Detected", record.ReportedValue);
    }

    [Fact]
    public void RetiredEndpoint_Returns410Gone()
    {
        var db = PathogenTestData.NewDb();
        var engine = TestServiceFactory.TestWorkflow(db);
        var eligibility = new IncubatorEligibilityService(db);
        var snapshot = new MediaAppearanceSnapshotService(db, Microsoft.Extensions.Logging.Abstractions.NullLogger<MediaAppearanceSnapshotService>.Instance);
        var controller = new TestWorkflowController(engine, db, eligibility, snapshot);

        var result = controller.SubmitSelectivePlating_Retired(99) as ObjectResult;
        Assert.NotNull(result);
        Assert.Equal(410, result.StatusCode);
    }
}
