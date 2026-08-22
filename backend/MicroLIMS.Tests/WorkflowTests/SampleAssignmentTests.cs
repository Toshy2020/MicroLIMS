using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.WorkflowTests;

public class SampleAssignmentTests
{
    private static MicroLimsDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MicroLimsDbContext(options);
    }

    [Fact]
    public async Task AssignAnalystAsync_AssignsAnalystToAllActiveOrders()
    {
        await using var db = NewDb();
        var analyst = new User { Id = 101, Username = "analyst1", FullName = "Analyst One", PasswordHash = "hash", IsActive = true };
        db.Users.Add(analyst);

        var sample = new Sample { Category = SampleCategory.FinishedProduct, ControlNumber = "CTRL-10", Status = SampleStatus.Received };
        var order1 = new TestOrder { TestCode = "TAMC", Status = ApprovalStatus.Pending, CurrentStep = WorkflowStep.Waiting };
        var order2 = new TestOrder { TestCode = "TYMC", Status = ApprovalStatus.Pending, CurrentStep = WorkflowStep.Waiting };
        var approvedOrder = new TestOrder { TestCode = "EC", Status = ApprovalStatus.Approved, CurrentStep = WorkflowStep.Approved, AssignedAnalystId = 99 };
        sample.TestOrders.Add(order1);
        sample.TestOrders.Add(order2);
        sample.TestOrders.Add(approvedOrder);
        db.Samples.Add(sample);
        await db.SaveChangesAsync();

        var workspace = new TestingWorkspaceService(db);
        var service = new SampleAssignmentService(db, workspace);

        await service.AssignAnalystAsync(sample.Id, 101, actingUserId: 1);

        var reloaded1 = await db.TestOrders.FirstAsync(t => t.Id == order1.Id);
        var reloaded2 = await db.TestOrders.FirstAsync(t => t.Id == order2.Id);
        var reloadedApproved = await db.TestOrders.FirstAsync(t => t.Id == approvedOrder.Id);

        Assert.Equal(101, reloaded1.AssignedAnalystId);
        Assert.Equal(101, reloaded2.AssignedAnalystId);
        Assert.Equal(99, reloadedApproved.AssignedAnalystId); // Approved order remains unchanged
    }

    [Fact]
    public async Task AssignAnalystAsync_AllowsUnassigningSample()
    {
        await using var db = NewDb();
        var sample = new Sample { Category = SampleCategory.FinishedProduct, ControlNumber = "CTRL-11", Status = SampleStatus.Received };
        var order = new TestOrder { TestCode = "TAMC", Status = ApprovalStatus.Pending, CurrentStep = WorkflowStep.Waiting, AssignedAnalystId = 101 };
        sample.TestOrders.Add(order);
        db.Samples.Add(sample);
        await db.SaveChangesAsync();

        var workspace = new TestingWorkspaceService(db);
        var service = new SampleAssignmentService(db, workspace);

        await service.AssignAnalystAsync(sample.Id, null, actingUserId: 1);

        var reloaded = await db.TestOrders.FirstAsync(t => t.Id == order.Id);
        Assert.Null(reloaded.AssignedAnalystId);
    }

    [Fact]
    public async Task SegregationOfDutiesGuard_BlocksUserWhoPreparedSampleFromReviewing()
    {
        await using var db = NewDb();
        var preparer = new User { Id = 50, Username = "prepUser", FullName = "Prep User", PasswordHash = "hash", IsActive = true };
        db.Users.Add(preparer);

        var sample = new Sample { Category = SampleCategory.FinishedProduct, ControlNumber = "CTRL-12", Status = SampleStatus.UnderReview };
        var order = new TestOrder { TestCode = "TAMC", Status = ApprovalStatus.Pending, CurrentStep = WorkflowStep.Ready, AssignedAnalystId = 99 };
        sample.TestOrders.Add(order);

        var prep = new SamplePreparation
        {
            Sample = sample,
            Amount = 10m,
            Unit = "gm",
            Technique = "PourPlate",
            PreparedByUserId = 50,
            NeutralizerId = 1,
            DiluentTypeId = 1
        };
        db.SamplePreparations.Add(prep);
        db.Samples.Add(sample);
        await db.SaveChangesAsync();

        var guard = new SegregationOfDutiesGuard(db);
        var performedByPreparer = await guard.DidUserPerformTestAsync(order.Id, userId: 50);
        var performedByOther = await guard.DidUserPerformTestAsync(order.Id, userId: 70);

        Assert.True(performedByPreparer);
        Assert.False(performedByOther);
    }
}
