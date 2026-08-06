using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.WorkflowTests;

public class SamplePreparationTests
{
    private static MicroLimsDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MicroLimsDbContext(options);
    }

    [Fact]
    public async Task PrepareAsync_AssignsPreparerAsAnalyst_ToEveryWaitingTestOrderOnTheSample()
    {
        await using var db = NewDb();
        var diluent = new DiluentType { Name = "Buffer", RequiresBatchTracking = false };
        var neutralizer = new Neutralizer { Name = "Tween" };
        db.DiluentTypes.Add(diluent);
        db.Neutralizers.Add(neutralizer);

        var sample = new Sample { Category = SampleCategory.FinishedProduct, ControlNumber = "CTRL-1", Status = SampleStatus.Received };
        var waitingOrder = new TestOrder { TestCode = "TAMC", Status = ApprovalStatus.Pending, CurrentStep = WorkflowStep.Waiting };
        var runningOrder = new TestOrder { TestCode = "TYMC", Status = ApprovalStatus.InProgress, CurrentStep = WorkflowStep.Running, AssignedAnalystId = 99 };
        sample.TestOrders.Add(waitingOrder);
        sample.TestOrders.Add(runningOrder);
        db.Samples.Add(sample);
        await db.SaveChangesAsync();

        var service = new SamplePreparationService(db);
        await service.PrepareAsync(new PrepareSampleRequest(
            sample.Id, 10m, "ml", "PourPlate", null, null, diluent.Id, null, neutralizer.Id, UserId: 5, null, null));

        var reloadedWaiting = await db.TestOrders.FirstAsync(t => t.Id == waitingOrder.Id);
        var reloadedRunning = await db.TestOrders.FirstAsync(t => t.Id == runningOrder.Id);

        Assert.Equal(5, reloadedWaiting.AssignedAnalystId);
        Assert.Equal(99, reloadedRunning.AssignedAnalystId); // already past Waiting - untouched
    }
}
