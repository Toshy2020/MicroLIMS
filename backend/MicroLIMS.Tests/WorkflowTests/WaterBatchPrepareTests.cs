using Microsoft.EntityFrameworkCore;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.WorkflowTests;

public class WaterBatchPrepareTests
{
    private static MicroLimsDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MicroLimsDbContext(options);
    }

    [Fact]
    public async Task SampleLocation_CanReferenceWaterSamplingPointAndSamplingConfiguration()
    {
        await using var db = NewDb();
        var department = new WaterDepartment { Name = "WTU" };
        db.WaterDepartments.Add(department);
        await db.SaveChangesAsync();

        var point = new WaterSamplingPoint { Code = "SP-1", WaterDepartmentId = department.Id, AssignedTestCodes = new() { "TAMC" } };
        db.WaterSamplingPoints.Add(point);
        var config = new SamplingConfiguration { WaterSamplingPointId = point.Id, TestCode = "TAMC", AlertLimit = "10", ActionLimit = "50", SpecLimit = "100" };
        db.SamplingConfigurations.Add(config);
        await db.SaveChangesAsync();

        var sample = new Sample
        {
            ReferenceNumber = "WT0817999", Category = SampleCategory.Water, WaterDepartmentId = department.Id,
            ControlNumber = "CTRL-99", SampledBy = "Analyst"
        };
        var order = new TestOrder { TestCode = "TAMC", Status = ApprovalStatus.Pending, CurrentStep = WorkflowStep.Waiting };
        sample.TestOrders.Add(order);
        sample.Locations.Add(new SampleLocation
        {
            TestOrder = order, LocationType = LocationType.WaterSamplingPoint,
            WaterSamplingPointId = point.Id, SamplingConfigurationId = config.Id
        });
        db.Samples.Add(sample);
        await db.SaveChangesAsync();

        var loaded = await db.SampleLocations.SingleAsync();
        Assert.Equal(LocationType.WaterSamplingPoint, loaded.LocationType);
        Assert.Equal(point.Id, loaded.WaterSamplingPointId);
        Assert.Equal(config.Id, loaded.SamplingConfigurationId);

        var loadedSample = await db.Samples.SingleAsync();
        Assert.Equal(department.Id, loadedSample.WaterDepartmentId);
    }

    [Fact]
    public async Task PrepareAsync_CreatesOneTestOrderPerDistinctCodeAndOneLocationPerPointTest()
    {
        await using var db = NewDb();
        var department = new WaterDepartment { Name = "WTU" };
        db.WaterDepartments.Add(department);
        await db.SaveChangesAsync();

        var pointA = new WaterSamplingPoint { Code = "SP-A", Location = "A", WaterDepartmentId = department.Id, AssignedTestCodes = new() { "TAMC", "Salmonella" } };
        var pointB = new WaterSamplingPoint { Code = "SP-B", Location = "B", WaterDepartmentId = department.Id, AssignedTestCodes = new() { "TAMC" } };
        db.WaterSamplingPoints.AddRange(pointA, pointB);
        db.TestDefinitions.Add(new TestDefinition { Code = "TAMC", DisplayName = "TAMC", WorkflowType = WorkflowType.CountTest });
        db.TestDefinitions.Add(new TestDefinition { Code = "Salmonella", DisplayName = "Salmonella", WorkflowType = WorkflowType.Observation });
        db.SamplingConfigurations.Add(new SamplingConfiguration { WaterSamplingPointId = pointA.Id, TestCode = "TAMC", AlertLimit = "10", ActionLimit = "50", SpecLimit = "100" });
        await db.SaveChangesAsync();

        var engine = new MicroLIMS.Application.Workflows.WaterWorkflowEngine(db, new MicroLIMS.Application.Services.ReferenceNumberGenerator(db));
        var sample = await engine.ReceiveAsync(new MicroLIMS.Application.Workflows.WaterReceiveRequest(department.Id, 0, "500ml", "Analyst", "CTRL-3", 1));

        var prepared = await engine.PrepareAsync(sample.Id, new List<int> { pointA.Id, pointB.Id }, 1);

        Assert.Equal(2, prepared.TestOrders.Count); // TAMC, Salmonella
        Assert.Equal(3, prepared.Locations.Count);  // TAMC@A, Salmonella@A, TAMC@B
        Assert.Equal(SamplePreparationStatus.Ready, prepared.PreparationStatus);

        var tamcOrder = prepared.TestOrders.Single(o => o.TestCode == "TAMC");
        var tamcAtPointA = prepared.Locations.Single(l => l.TestOrderId == tamcOrder.Id && l.WaterSamplingPointId == pointA.Id);
        Assert.NotNull(tamcAtPointA.SamplingConfigurationId);

        var salmonellaOrder = prepared.TestOrders.Single(o => o.TestCode == "Salmonella");
        var salmonellaLocation = prepared.Locations.Single(l => l.TestOrderId == salmonellaOrder.Id);
        Assert.Null(salmonellaLocation.SamplingConfigurationId);
    }

    [Fact]
    public async Task PrepareAsync_RejectsPointFromWrongDepartment()
    {
        await using var db = NewDb();
        var deptA = new WaterDepartment { Name = "A" };
        var deptB = new WaterDepartment { Name = "B" };
        db.WaterDepartments.AddRange(deptA, deptB);
        await db.SaveChangesAsync();
        var pointInB = new WaterSamplingPoint { Code = "SP-B", WaterDepartmentId = deptB.Id, AssignedTestCodes = new() { "TAMC" } };
        db.WaterSamplingPoints.Add(pointInB);
        await db.SaveChangesAsync();

        var engine = new MicroLIMS.Application.Workflows.WaterWorkflowEngine(db, new MicroLIMS.Application.Services.ReferenceNumberGenerator(db));
        var sample = await engine.ReceiveAsync(new MicroLIMS.Application.Workflows.WaterReceiveRequest(deptA.Id, 0, "500ml", "Analyst", "CTRL-4", 1));

        await Assert.ThrowsAsync<InvalidOperationException>(() => engine.PrepareAsync(sample.Id, new List<int> { pointInB.Id }, 1));
    }

    [Fact]
    public async Task PrepareAsync_RejectsEmptySelection()
    {
        await using var db = NewDb();
        var department = new WaterDepartment { Name = "WTU" };
        db.WaterDepartments.Add(department);
        await db.SaveChangesAsync();

        var engine = new MicroLIMS.Application.Workflows.WaterWorkflowEngine(db, new MicroLIMS.Application.Services.ReferenceNumberGenerator(db));
        var sample = await engine.ReceiveAsync(new MicroLIMS.Application.Workflows.WaterReceiveRequest(department.Id, 0, "500ml", "Analyst", "CTRL-5", 1));

        await Assert.ThrowsAsync<InvalidOperationException>(() => engine.PrepareAsync(sample.Id, new List<int>(), 1));
    }
}
