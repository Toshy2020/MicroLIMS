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
        var waitingOrder1 = new TestOrder { TestCode = "TAMC", Status = ApprovalStatus.Pending, CurrentStep = WorkflowStep.Waiting };
        var waitingOrder2 = new TestOrder { TestCode = "TYMC", Status = ApprovalStatus.Pending, CurrentStep = WorkflowStep.Waiting };
        sample.TestOrders.Add(waitingOrder1);
        sample.TestOrders.Add(waitingOrder2);
        db.Samples.Add(sample);
        await db.SaveChangesAsync();

        var service = new SamplePreparationService(db);
        await service.PrepareAsync(new PrepareSampleRequest(
            sample.Id, 10m, "ml", "PourPlate", null, null, diluent.Id, null, neutralizer.Id, UserId: 5, null, null));

        var reloadedWaiting1 = await db.TestOrders.FirstAsync(t => t.Id == waitingOrder1.Id);
        var reloadedWaiting2 = await db.TestOrders.FirstAsync(t => t.Id == waitingOrder2.Id);

        Assert.Equal(5, reloadedWaiting1.AssignedAnalystId);
        Assert.Equal(5, reloadedWaiting2.AssignedAnalystId);
    }

    [Fact]
    public async Task PrepareAsync_WhenSampleAssignedToDifferentAnalyst_ThrowsInvalidOperationException()
    {
        await using var db = NewDb();
        var userX = new User { Id = 10, Username = "analystX", FullName = "Analyst X", PasswordHash = "hash", IsActive = true };
        var userY = new User { Id = 20, Username = "analystY", FullName = "Analyst Y", PasswordHash = "hash", IsActive = true };
        db.Users.AddRange(userX, userY);

        var diluent = new DiluentType { Name = "Buffer", RequiresBatchTracking = false };
        var neutralizer = new Neutralizer { Name = "Tween" };
        db.DiluentTypes.Add(diluent);
        db.Neutralizers.Add(neutralizer);

        var sample = new Sample { Category = SampleCategory.FinishedProduct, ControlNumber = "CTRL-2", Status = SampleStatus.Received };
        var order = new TestOrder { TestCode = "TAMC", Status = ApprovalStatus.Pending, CurrentStep = WorkflowStep.Waiting, AssignedAnalystId = 10 };
        sample.TestOrders.Add(order);
        db.Samples.Add(sample);
        await db.SaveChangesAsync();

        var service = new SamplePreparationService(db);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.PrepareAsync(new PrepareSampleRequest(
            sample.Id, 10m, "ml", "PourPlate", null, null, diluent.Id, null, neutralizer.Id, UserId: 20, null, null)));

        Assert.Contains("Analyst X", ex.Message);
        Assert.Contains("Only the assigned analyst may perform sample preparation", ex.Message);
    }

    [Fact]
    public async Task PrepareAsync_WhenSampleAssignedToSameAnalyst_SucceedsAndSetsPreparedBy()
    {
        await using var db = NewDb();
        var userX = new User { Id = 10, Username = "analystX", FullName = "Analyst X", PasswordHash = "hash", IsActive = true };
        db.Users.Add(userX);

        var diluent = new DiluentType { Name = "Buffer", RequiresBatchTracking = false };
        var neutralizer = new Neutralizer { Name = "Tween" };
        db.DiluentTypes.Add(diluent);
        db.Neutralizers.Add(neutralizer);

        var sample = new Sample { Category = SampleCategory.FinishedProduct, ControlNumber = "CTRL-3", Status = SampleStatus.Received };
        var order = new TestOrder { TestCode = "TAMC", Status = ApprovalStatus.Pending, CurrentStep = WorkflowStep.Waiting, AssignedAnalystId = 10 };
        sample.TestOrders.Add(order);
        db.Samples.Add(sample);
        await db.SaveChangesAsync();

        var service = new SamplePreparationService(db);
        var prep = await service.PrepareAsync(new PrepareSampleRequest(
            sample.Id, 10m, "ml", "PourPlate", null, null, diluent.Id, null, neutralizer.Id, UserId: 10, null, null));

        Assert.NotNull(prep);
        Assert.Equal(10, prep.PreparedByUserId);
        Assert.Equal(10, order.AssignedAnalystId);
    }
}
