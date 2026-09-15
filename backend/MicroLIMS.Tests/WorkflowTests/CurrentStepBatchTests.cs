using MicroLIMS.Application.Workflows;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using Xunit;

namespace MicroLIMS.Tests.WorkflowTests;

// Grouped actions compute every candidate's current step through
// GetCurrentStepDetailsForOrdersAsync. It must give, order for order, what
// GetCurrentStepDetailsAsync gives - including the error for an order whose
// step can't be computed.
public class CurrentStepBatchTests
{
    private const int AnalystId = 4;

    private static DateTime Start => DateTime.UtcNow.AddHours(-30);
    private static DateTime End => DateTime.UtcNow.AddHours(-6);

    private static async Task AssertBatchMatchesSingleAsync(ITestWorkflowEngine engine, params int[] ids)
    {
        var batch = await engine.GetCurrentStepDetailsForOrdersAsync(ids);
        Assert.Equal(ids.Distinct().Count(), batch.Count);

        foreach (var id in ids)
        {
            CurrentStepDetails? single = null;
            string? error = null;
            try
            {
                single = await engine.GetCurrentStepDetailsAsync(id);
            }
            catch (InvalidOperationException ex)
            {
                error = ex.Message;
            }

            var lookup = batch[id];
            Assert.Equal(error, lookup.Error);
            if (single is null)
            {
                Assert.Null(lookup.Details);
                continue;
            }

            var expected = single.Result;
            var actual = Assert.IsType<CurrentStepDetails>(lookup.Details).Result;
            Assert.Equal(expected.Step?.Id, actual.Step?.Id);
            Assert.Equal(expected.WorkflowType, actual.WorkflowType);
            Assert.Equal(expected.OpenIncubation?.Id, actual.OpenIncubation?.Id);
            Assert.Equal(expected.AllStepsComplete, actual.AllStepsComplete);
            Assert.Equal(expected.FinalResult, actual.FinalResult);
            Assert.Equal(expected.TotalSteps, actual.TotalSteps);
            Assert.Equal(expected.CompletedSteps, actual.CompletedSteps);
            Assert.Equal(expected.AllSteps, actual.AllSteps);
            Assert.Equal(single.Definition.Id, lookup.Details!.Definition.Id);
        }
    }

    [Fact]
    public async Task Batch_MatchesSingle_AlongTheChain_AndForOrdersWithoutAUsableTemplate()
    {
        await using var db = PathogenTestData.NewDb();
        var (order, media, incubator) = await PathogenTestData.SeedFiveStageOrderAsync(db);
        var engine = TestServiceFactory.TestWorkflow(db);

        // A second, untouched order on the same template, one whose code has
        // no template, and one whose template has no steps.
        var sibling = new TestOrder { SampleId = order.SampleId, TestCode = order.TestCode, Status = ApprovalStatus.Pending, CurrentStep = WorkflowStep.Waiting };
        var noTemplate = new TestOrder { SampleId = order.SampleId, TestCode = "NO-TEMPLATE", Status = ApprovalStatus.Pending, CurrentStep = WorkflowStep.Waiting };
        db.TestDefinitions.Add(new TestDefinition { Code = "EMPTY-TEMPLATE", DisplayName = "Empty", WorkflowType = WorkflowType.Observation, IsActive = true });
        var emptyTemplate = new TestOrder { SampleId = order.SampleId, TestCode = "EMPTY-TEMPLATE", Status = ApprovalStatus.Pending, CurrentStep = WorkflowStep.Waiting };
        db.TestOrders.AddRange(sibling, noTemplate, emptyTemplate);
        await db.SaveChangesAsync();

        const int missingOrderId = 987654;
        int[] ids = { order.Id, sibling.Id, noTemplate.Id, emptyTemplate.Id, missingOrderId };

        await AssertBatchMatchesSingleAsync(engine, ids);

        await engine.SubmitBrothAsync(order.Id, "Broth Enrichment", media.BrothLotId, incubator.Id, Start, End, null, AnalystId);
        await AssertBatchMatchesSingleAsync(engine, ids);

        await engine.SubmitBrothAsync(order.Id, "Selective Broth", media.SelectiveBrothLotId, media.SelectiveBrothIncubatorId, Start, End, null, AnalystId);
        await engine.SubmitSelectivePlatingAsync(order.Id, "Selective Plating", media.SelectivePlatingLotId, incubator.Id,
            Start, End, GrowthObservation.GrowthNonConforming, AnalystId);
        await AssertBatchMatchesSingleAsync(engine, ids);

        var lookups = await engine.GetCurrentStepDetailsForOrdersAsync(ids);
        Assert.True(lookups[order.Id].Details!.Result.AllStepsComplete);
        Assert.Contains("no workflow template", lookups[noTemplate.Id].Error);
        Assert.Contains("no workflow steps", lookups[emptyTemplate.Id].Error);
        Assert.Equal($"Test order {missingOrderId} not found.", lookups[missingOrderId].Error);
    }

    [Fact]
    public async Task Batch_WithNoOrders_ReturnsEmpty()
    {
        await using var db = PathogenTestData.NewDb();
        var engine = TestServiceFactory.TestWorkflow(db);

        Assert.Empty(await engine.GetCurrentStepDetailsForOrdersAsync(Array.Empty<int>()));
    }
}
