using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.WorkflowTests;

public class SegregationOfDutiesTests
{
    private static MicroLimsDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MicroLimsDbContext(options);
    }

    // Seeds a TestOrder at ResultEntered with one Result entered by
    // analystId, and (optionally) AssignedAnalystId set too.
    private static async Task<TestOrder> SeedResultEnteredOrder(MicroLimsDbContext db, int analystId, int? assignedAnalystId = null)
    {
        var sample = new Sample { Category = SampleCategory.FinishedProduct, ControlNumber = $"CTRL-{Guid.NewGuid():N}", Status = SampleStatus.Received };
        var order = new TestOrder
        {
            TestCode = "TAMC",
            Status = ApprovalStatus.ResultEntered,
            CurrentStep = WorkflowStep.Ready,
            AssignedAnalystId = assignedAnalystId
        };
        sample.TestOrders.Add(order);
        db.Samples.Add(sample);
        await db.SaveChangesAsync();

        db.Results.Add(new Result { TestOrderId = order.Id, RawValue = "10", Type = ResultType.Numeric, EnteredByUserId = analystId });
        await db.SaveChangesAsync();

        return order;
    }

    [Fact]
    public async Task DidUserPerformTest_IsTrue_ForTheAnalystWhoEnteredTheResult()
    {
        await using var db = NewDb();
        var order = await SeedResultEnteredOrder(db, analystId: 1);
        var guard = new SegregationOfDutiesGuard(db);

        Assert.True(await guard.DidUserPerformTestAsync(order.Id, 1));
        Assert.False(await guard.DidUserPerformTestAsync(order.Id, 2));
    }

    [Fact]
    public async Task DidUserPerformTest_IsTrue_ForTheAssignedAnalystEvenWithoutEnteringTheResult()
    {
        await using var db = NewDb();
        var order = await SeedResultEnteredOrder(db, analystId: 1, assignedAnalystId: 2);

        Assert.True(await new SegregationOfDutiesGuard(db).DidUserPerformTestAsync(order.Id, 2));
    }

    // Confirmatory plate readings are appended to a result row submitted
    // by whoever ran the SETUP, so a second analyst who only read the
    // plates was invisible to the guard and could review and approve
    // their own reading.
    [Fact]
    public async Task DidUserPerformTest_IsTrue_ForTheAnalystWhoOnlyReadTheConfirmatoryPlates()
    {
        await using var db = PathogenTestData.NewDb();
        var (order, media, incubator) = await PathogenTestData.SeedFiveStageOrderAsync(db);
        var engine = TestServiceFactory.TestWorkflow(db);
        var start = DateTime.UtcNow.AddHours(-30);
        var end = DateTime.UtcNow.AddHours(-6);
        const int setupAnalystId = 4;   // also the order's AssignedAnalystId
        const int plateReaderId = 77;

        await engine.SubmitBrothAsync(order.Id, "Broth Enrichment", media.BrothLotId, incubator.Id, start, end, null, setupAnalystId);
        await engine.SubmitBrothAsync(order.Id, "Selective Broth", media.SelectiveBrothLotId, media.SelectiveBrothIncubatorId, start, end, null, setupAnalystId);
        await engine.SubmitSelectivePlatingAsync(order.Id, "Selective Plating", media.SelectivePlatingLotId, incubator.Id,
            start, end, GrowthObservation.GrowthConforming, setupAnalystId);
        await engine.SubmitConfirmatorySetupAsync(order.Id, "Confirmatory Plating", new[]
        {
            new ConfirmatorySelectionInput(media.XldStepMediaId, media.XldLotId, incubator.Id),
            new ConfirmatorySelectionInput(media.TsiStepMediaId, media.TsiLotId, incubator.Id)
        }, start, end, setupAnalystId);

        // Only this user's contribution is the plate reading itself.
        await engine.SubmitConfirmatoryObservationsAsync(order.Id, "Confirmatory Plating", new[]
        {
            new ConfirmatoryObservationInput(media.XldMaterialId, GrowthObservation.GrowthConforming),
            new ConfirmatoryObservationInput(media.TsiMaterialId, GrowthObservation.GrowthConforming)
        }, plateReaderId);

        var guard = new SegregationOfDutiesGuard(db);
        Assert.True(await guard.DidUserPerformTestAsync(order.Id, plateReaderId));

        // An analyst with no involvement at all is still clear.
        Assert.False(await guard.DidUserPerformTestAsync(order.Id, 88));
    }
}
