using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Constants;
using Xunit;

namespace MicroLIMS.Tests.WorkflowTests;

public class BiochemicalReviewTests
{
    private const int AnalystId = 4;
    private const int ReviewerId = 9;

    private static async Task<(int orderId, ITestWorkflowEngine engine, MicroLimsDbContext db)> AllConformingAsync()
    {
        var db = PathogenTestData.NewDb();
        var (order, media, incubator) = await PathogenTestData.SeedFiveStageOrderAsync(db);
        var engine = TestServiceFactory.TestWorkflow(db);
        var start = DateTime.UtcNow.AddHours(-30);
        var end = DateTime.UtcNow.AddHours(-6);

        await engine.SubmitBrothAsync(order.Id, "Broth Enrichment", media.BrothLotId, incubator.Id, start, end, null, AnalystId);
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
        return (order.Id, engine, db);
    }

    [Fact]
    public async Task SubmitAsDetected_SetsDetectedAndFlagsTheMissingBiochemical()
    {
        var (orderId, engine, db) = await AllConformingAsync();
        await using var _ = db;

        var result = await engine.RecordAnalystDecisionAsync(orderId, AnalystDecision.SubmitAsDetected, AnalystId);

        Assert.Equal("Detected", result.WorkflowFinalResult);
        Assert.Contains("BiochemicalNotPerformed", result.Flags);

        var stored = await db.WorkflowStepResults.SingleAsync(r => r.StepName == "Confirmatory Plating");
        Assert.True(stored.SkippedBiochemical);
    }

    [Fact]
    public async Task ProceedToBiochemical_UnlocksTheStepWithoutSettingAResult()
    {
        var (orderId, engine, db) = await AllConformingAsync();
        await using var _ = db;

        var result = await engine.RecordAnalystDecisionAsync(orderId, AnalystDecision.ProceedToBiochemical, AnalystId);

        Assert.Null(result.WorkflowFinalResult);
        Assert.True(result.NextStepUnlocked);
    }

    [Fact]
    public async Task AnalystDecision_BeforeAllConforming_IsRejected()
    {
        var db = PathogenTestData.NewDb();
        await using var _ = db;
        var (order, _, _) = await PathogenTestData.SeedFiveStageOrderAsync(db);
        var engine = TestServiceFactory.TestWorkflow(db);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => engine.RecordAnalystDecisionAsync(order.Id, AnalystDecision.SubmitAsDetected, AnalystId));
    }

    [Fact]
    public async Task SubmitBiochemical_SetsDetectedAndClearsTheSkippedFlag()
    {
        var (orderId, engine, db) = await AllConformingAsync();
        await using var _ = db;
        await engine.RecordAnalystDecisionAsync(orderId, AnalystDecision.ProceedToBiochemical, AnalystId);

        var result = await engine.SubmitBiochemicalAsync(orderId, "Biochemical Test", "IMViC: + + - -", null, AnalystId);

        Assert.Equal("Detected", result.WorkflowFinalResult);
        Assert.Empty(result.Flags);

        var stored = await db.WorkflowStepResults.SingleAsync(r => r.StepName == "Biochemical Test");
        Assert.False(stored.SkippedBiochemical);
        Assert.Equal("IMViC: + + - -", stored.BiochemicalResultText);
    }

    [Fact]
    public async Task SubmitBiochemical_WithBlankText_ThrowsBiochemicalResultRequired()
    {
        var (orderId, engine, db) = await AllConformingAsync();
        await using var _ = db;
        await engine.RecordAnalystDecisionAsync(orderId, AnalystDecision.ProceedToBiochemical, AnalystId);

        var ex = await Assert.ThrowsAsync<WorkflowStepException>(
            () => engine.SubmitBiochemicalAsync(orderId, "Biochemical Test", "   ", null, AnalystId));

        Assert.Equal(WorkflowErrorCodes.BiochemicalResultRequired, ex.ErrorCode);
    }

    [Fact]
    public async Task ReviewerApprove_ClearsRequiresBiochemical()
    {
        var (orderId, engine, db) = await AllConformingAsync();
        await using var _ = db;
        await engine.RecordAnalystDecisionAsync(orderId, AnalystDecision.SubmitAsDetected, AnalystId);
        var resultId = (await db.WorkflowStepResults.SingleAsync(r => r.StepName == "Confirmatory Plating")).Id;

        await engine.RecordBiochemicalReviewDecisionAsync(resultId, approve: true, "Evidence sufficient.", ReviewerId);

        var stored = await db.WorkflowStepResults.SingleAsync(r => r.Id == resultId);
        Assert.False(stored.RequiresBiochemical);
        Assert.Null(stored.ReturnedAtUtc);
    }

    [Fact]
    public async Task ReviewerReturn_SetsTheReturnFieldsAndNotifiesTheAnalyst()
    {
        var (orderId, engine, db) = await AllConformingAsync();
        await using var _ = db;
        await engine.RecordAnalystDecisionAsync(orderId, AnalystDecision.SubmitAsDetected, AnalystId);
        var resultId = (await db.WorkflowStepResults.SingleAsync(r => r.StepName == "Confirmatory Plating")).Id;

        await engine.RecordBiochemicalReviewDecisionAsync(resultId, approve: false, "Required per SOP-MB-007.", ReviewerId);

        var stored = await db.WorkflowStepResults.SingleAsync(r => r.Id == resultId);
        Assert.True(stored.RequiresBiochemical);
        Assert.Equal("Required per SOP-MB-007.", stored.ReturnReason);
        Assert.Equal(ReviewerId, stored.ReturnedByUserId);
        Assert.NotNull(stored.ReturnedAtUtc);
    }

    [Fact]
    public async Task ReviewerReturn_WithoutAReason_IsRejected()
    {
        var (orderId, engine, db) = await AllConformingAsync();
        await using var _ = db;
        await engine.RecordAnalystDecisionAsync(orderId, AnalystDecision.SubmitAsDetected, AnalystId);
        var resultId = (await db.WorkflowStepResults.SingleAsync(r => r.StepName == "Confirmatory Plating")).Id;

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => engine.RecordBiochemicalReviewDecisionAsync(resultId, approve: false, "  ", ReviewerId));
    }

    [Fact]
    public async Task ReviewerCannotDecideOnTheirOwnResult()
    {
        var (orderId, engine, db) = await AllConformingAsync();
        await using var _ = db;
        await engine.RecordAnalystDecisionAsync(orderId, AnalystDecision.SubmitAsDetected, AnalystId);
        var resultId = (await db.WorkflowStepResults.SingleAsync(r => r.StepName == "Confirmatory Plating")).Id;

        var ex = await Assert.ThrowsAsync<WorkflowStepException>(
            () => engine.RecordBiochemicalReviewDecisionAsync(resultId, approve: true, "Fine.", AnalystId));

        Assert.Equal(WorkflowErrorCodes.SegregationOfDutiesViolation, ex.ErrorCode);
    }
}
