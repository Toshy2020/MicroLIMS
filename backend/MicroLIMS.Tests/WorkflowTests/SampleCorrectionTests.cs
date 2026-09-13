using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.WorkflowTests;

public class SampleCorrectionTests
{
    private static MicroLimsDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MicroLimsDbContext(options);
    }

    private static async Task<Sample> SeedSample(MicroLimsDbContext db)
    {
        var cause = new CauseOfTesting { Name = "Routine" };
        db.CausesOfTesting.Add(cause);
        await db.SaveChangesAsync();

        var sample = new Sample { Category = SampleCategory.FinishedProduct, BatchNumber = "OLD-BATCH", ControlNumber = "OLD-CTRL", Status = SampleStatus.Received, CauseOfTestingId = cause.Id };
        var order = new TestOrder { TestCode = "TAMC", Status = ApprovalStatus.Pending, CurrentStep = WorkflowStep.Waiting };
        sample.TestOrders.Add(order);
        db.Samples.Add(sample);
        await db.SaveChangesAsync();
        return sample;
    }

    [Fact]
    public async Task VoidAsync_SetsSampleAndCurrentTestsVoided_AndRecordsNoApprovalDecision()
    {
        await using var db = NewDb();
        var sample = await SeedSample(db);
        var liveOrder = sample.TestOrders.Single();
        var supersededOrder = new TestOrder { SampleId = sample.Id, TestCode = "TYMC", Status = ApprovalStatus.Reviewed, CurrentStep = WorkflowStep.Reviewed, IsSuperseded = true };
        db.TestOrders.Add(supersededOrder);
        db.ResultRecords.Add(new ResultRecord { SampleId = sample.Id, TestOrderId = liveOrder.Id, TestCode = "TAMC", SampleStatus = SampleStatus.InTesting });
        await db.SaveChangesAsync();
        var service = new SampleCorrectionService(db);

        var dto = await service.VoidAsync(sample.Id, "  Received against the wrong batch  ", actingUserId: 7);

        var reloaded = await db.Samples.Include(s => s.TestOrders).FirstAsync(s => s.Id == sample.Id);
        Assert.Equal(SampleStatus.Voided, reloaded.Status);
        Assert.Equal("Voided", dto.Status);

        // A void is not a decision about the material.
        Assert.Null(reloaded.ApprovalDecision);
        Assert.Null(reloaded.ApprovedByUserId);
        Assert.Null(reloaded.ApprovedAt);
        Assert.Equal("Voided: Received against the wrong batch", reloaded.CertificateRemarks);

        Assert.Equal(ApprovalStatus.Voided, reloaded.TestOrders.Single(t => t.Id == liveOrder.Id).Status);
        Assert.Equal(ApprovalStatus.Reviewed, reloaded.TestOrders.Single(t => t.Id == supersededOrder.Id).Status);

        Assert.Equal(SampleStatus.Voided, (await db.ResultRecords.SingleAsync()).SampleStatus);

        var voidEvent = Assert.Single(await db.ReviewWorkflowEvents
            .Where(e => e.EntityType == ReviewEntityTypes.Sample && e.EntityId == sample.Id)
            .ToListAsync());
        Assert.Equal(ReviewWorkflowEventType.SampleVoided, voidEvent.EventType);
        Assert.Equal(7, voidEvent.PerformedByUserId);
        Assert.Equal("Received against the wrong batch", voidEvent.Comment);
    }

    [Fact]
    public async Task VoidAsync_AlreadyVoided_IsRefused()
    {
        await using var db = NewDb();
        var sample = await SeedSample(db);
        var service = new SampleCorrectionService(db);
        await service.VoidAsync(sample.Id, "Duplicate registration", actingUserId: 7);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.VoidAsync(sample.Id, "Again", actingUserId: 7));

        Assert.Equal("Sample is already voided.", ex.Message);
    }

    [Fact]
    public async Task CorrectAsync_BeforeIncubation_UpdatesBatchAndControlNumber()
    {
        await using var db = NewDb();
        var sample = await SeedSample(db);
        var service = new SampleCorrectionService(db);

        var dto = await service.CorrectAsync(sample.Id, "NEW-BATCH", "NEW-CTRL");

        Assert.Equal("NEW-BATCH", dto.BatchNumber);
        Assert.Equal("NEW-CTRL", dto.ControlNumber);

        var reloaded = await db.Samples.FirstAsync(s => s.Id == sample.Id);
        Assert.Equal("NEW-BATCH", reloaded.BatchNumber);
        Assert.Equal("NEW-CTRL", reloaded.ControlNumber);
    }

    [Fact]
    public async Task CorrectAsync_OnlyControlNumberProvided_LeavesBatchNumberUnchanged()
    {
        await using var db = NewDb();
        var sample = await SeedSample(db);
        var service = new SampleCorrectionService(db);

        var dto = await service.CorrectAsync(sample.Id, null, "NEW-CTRL-ONLY");

        Assert.Equal("OLD-BATCH", dto.BatchNumber);
        Assert.Equal("NEW-CTRL-ONLY", dto.ControlNumber);
    }

    [Fact]
    public async Task CorrectAsync_AfterIncubationStarted_Throws()
    {
        await using var db = NewDb();
        var sample = await SeedSample(db);
        var order = sample.TestOrders.First();
        db.Incubations.Add(new Incubation { TestOrderId = order.Id, StepName = "CountIncubation" });
        await db.SaveChangesAsync();

        var service = new SampleCorrectionService(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CorrectAsync(sample.Id, "NEW-BATCH", null));
        Assert.Equal("This sample cannot be corrected because incubation has already started.", ex.Message);

        var reloaded = await db.Samples.FirstAsync(s => s.Id == sample.Id);
        Assert.Equal("OLD-BATCH", reloaded.BatchNumber); // unchanged
    }
}
