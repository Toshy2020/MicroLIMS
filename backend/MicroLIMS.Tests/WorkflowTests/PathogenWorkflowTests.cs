using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.WorkflowTests;

// Especially important per the spec: Pathogen, Water, and EM workflows
// are frozen business rules and must be pinned down with tests.
public class PathogenWorkflowTests
{
    private static MicroLimsDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MicroLimsDbContext(options);
    }

    private static async Task<TestOrder> SeedTestOrder(MicroLimsDbContext db, string testCode)
    {
        var sample = new Sample { BatchNumber = "B1", Status = SampleStatus.Received };
        var order = new TestOrder { TestCode = testCode, Status = ApprovalStatus.Pending, CurrentStep = WorkflowStep.Waiting };
        sample.TestOrders.Add(order);
        db.Samples.Add(sample);
        await db.SaveChangesAsync();
        return order;
    }

    [Fact]
    public async Task SimplePathogen_NoGrowth_InterpretsAsAbsent()
    {
        await using var db = NewDb();
        var order = await SeedTestOrder(db, "PATHOGEN_ECOLI");
        var engine = new PathogenWorkflowEngine(db);

        await engine.RecordObservationAsync(order.Id, "Simple", growthObserved: false, userId: 1);
        var result = await engine.InterpretAsync(order.Id);

        Assert.Equal("Absent", result);
    }

    [Fact]
    public async Task SimplePathogen_Growth_InterpretsAsDetected()
    {
        await using var db = NewDb();
        var order = await SeedTestOrder(db, "PATHOGEN_ECOLI");
        var engine = new PathogenWorkflowEngine(db);

        await engine.RecordObservationAsync(order.Id, "Simple", growthObserved: true, userId: 1);
        var result = await engine.InterpretAsync(order.Id);

        Assert.Equal("Detected", result);
    }

    [Fact]
    public async Task Salmonella_TsbNegative_ShortCircuitsToAbsent()
    {
        await using var db = NewDb();
        var order = await SeedTestOrder(db, "PATHOGEN_SALMONELLA");
        var engine = new PathogenWorkflowEngine(db);

        await engine.RecordObservationAsync(order.Id, "TSB", growthObserved: false, userId: 1);
        var result = await engine.InterpretAsync(order.Id);

        Assert.Equal("Absent", result);

        // Chain is closed - recording RVS after a negative TSB is a violation.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            engine.RecordObservationAsync(order.Id, "RVS", growthObserved: true, userId: 1));
    }

    [Fact]
    public async Task Salmonella_FullChainPositive_InterpretsAsDetected()
    {
        await using var db = NewDb();
        var order = await SeedTestOrder(db, "PATHOGEN_SALMONELLA");
        var engine = new PathogenWorkflowEngine(db);

        await engine.RecordObservationAsync(order.Id, "TSB", growthObserved: true, userId: 1);
        await engine.RecordObservationAsync(order.Id, "RVS", growthObserved: true, userId: 1);
        await engine.RecordObservationAsync(order.Id, "XLD_TSI", growthObserved: true, userId: 1);

        var result = await engine.InterpretAsync(order.Id);
        Assert.Equal("Detected", result);
    }

    [Fact]
    public async Task Salmonella_OutOfOrderStep_ThrowsWorkflowOrderViolation()
    {
        await using var db = NewDb();
        var order = await SeedTestOrder(db, "PATHOGEN_SALMONELLA");
        var engine = new PathogenWorkflowEngine(db);

        // Attempting XLD_TSI before TSB/RVS is a workflow-order violation.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            engine.RecordObservationAsync(order.Id, "XLD_TSI", growthObserved: true, userId: 1));
    }
}
