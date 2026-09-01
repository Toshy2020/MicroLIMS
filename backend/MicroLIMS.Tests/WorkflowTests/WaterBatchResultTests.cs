using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.WorkflowTests;

public class WaterBatchResultTests
{
    private static MicroLimsDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MicroLimsDbContext(options);
    }

    [Fact]
    public async Task SampleLocation_StoresRawReadings()
    {
        await using var db = NewDb();
        var location = new SampleLocation { SampleId = 0, TestOrderId = 0, LocationType = LocationType.WaterSamplingPoint, RawReadings = "12,14,13" };
        db.SampleLocations.Add(location);
        await db.SaveChangesAsync();

        var loaded = await db.SampleLocations.SingleAsync();
        Assert.Equal("12,14,13", loaded.RawReadings);
    }

    [Fact]
    public async Task GetLocationsAsync_LoadsWaterSamplingPointAndSamplingConfiguration()
    {
        await using var db = NewDb();
        var department = new WaterDepartment { Name = "WTU" };
        db.WaterDepartments.Add(department);
        await db.SaveChangesAsync();
        var point = new WaterSamplingPoint { Code = "SP-1", WaterDepartmentId = department.Id, AssignedTestCodes = new() { "TAMC-Water" } };
        db.WaterSamplingPoints.Add(point);
        var config = new SamplingConfiguration { WaterSamplingPointId = point.Id, TestCode = "TAMC-Water", AlertLimit = "10", ActionLimit = "50", SpecLimit = "100" };
        db.SamplingConfigurations.Add(config);
        await db.SaveChangesAsync();

        var sample = new Sample { ReferenceNumber = "WT0817500", Category = SampleCategory.Water, WaterDepartmentId = department.Id, ControlNumber = "CTRL-500", SampledBy = "Analyst" };
        var order = new TestOrder { TestCode = "TAMC-Water", Status = ApprovalStatus.Pending, CurrentStep = WorkflowStep.Waiting };
        sample.TestOrders.Add(order);
        sample.Locations.Add(new SampleLocation { TestOrder = order, LocationType = LocationType.WaterSamplingPoint, WaterSamplingPointId = point.Id, SamplingConfigurationId = config.Id });
        db.Samples.Add(sample);
        await db.SaveChangesAsync();

        var engine = TestServiceFactory.TestWorkflow(db);

        var locations = await engine.GetLocationsAsync(order.Id);

        var loaded = Assert.Single(locations);
        Assert.NotNull(loaded.WaterSamplingPoint);
        Assert.Equal("SP-1", loaded.WaterSamplingPoint!.Code);
        Assert.NotNull(loaded.SamplingConfiguration);
        Assert.Equal("50", loaded.SamplingConfiguration!.ActionLimit);
    }

    private static async Task<(MicroLIMS.Application.Workflows.TestWorkflowEngine engine, int testOrderId, int locationAId, int locationBId)>
        SetupPreparedWaterCountOrderAsync(MicroLimsDbContext db)
    {
        var department = new WaterDepartment { Name = "WTU" };
        db.WaterDepartments.Add(department);
        await db.SaveChangesAsync();

        var pointA = new WaterSamplingPoint { Code = "SP-A", WaterDepartmentId = department.Id, AssignedTestCodes = new() { "TAMC-Water" } };
        var pointB = new WaterSamplingPoint { Code = "SP-B", WaterDepartmentId = department.Id, AssignedTestCodes = new() { "TAMC-Water" } };
        db.WaterSamplingPoints.AddRange(pointA, pointB);
        await db.SaveChangesAsync();
        db.SamplingConfigurations.Add(new SamplingConfiguration { WaterSamplingPointId = pointA.Id, TestCode = "TAMC-Water", AlertLimit = "10", ActionLimit = "50", SpecLimit = "100" });
        db.SamplingConfigurations.Add(new SamplingConfiguration { WaterSamplingPointId = pointB.Id, TestCode = "TAMC-Water", AlertLimit = "10", ActionLimit = "50", SpecLimit = "100" });
        await db.SaveChangesAsync();

        var testDefinition = new TestDefinition { Code = "TAMC-Water", DisplayName = "TAMC-Water", WorkflowType = WorkflowType.CountTest };
        db.TestDefinitions.Add(testDefinition);
        await db.SaveChangesAsync();

        var step = new TestWorkflowStep
        {
            TestDefinitionId = testDefinition.Id, StepOrder = 1, StepName = "CountIncubation", 
            IncubationMinHours = 0, IncubationMaxHours = 24, TemperatureMin = 20, TemperatureMax = 25, IsFinalStep = true
        };
        db.TestWorkflowSteps.Add(step);

        var material = new Material
        {
            MaterialType = MaterialType.DehydratedMedia, MaterialName = "TSA Powder", ManufacturerName = "Himedia",
            BatchNumber = "LOT-W01", ReceivingDate = DateTime.UtcNow.AddDays(-10), Code = "TSA-WATER",
            Location = "Micro Lab", QuantityReceived = 500, QuantityRemaining = 500, Unit = MaterialUnit.Gram
        };
        db.Materials.Add(material);
        await db.SaveChangesAsync();
        db.TestWorkflowStepMedias.Add(new TestWorkflowStepMedia { TestWorkflowStepId = step.Id, MaterialId = material.Id, TempMin = 20, TempMax = 25 });

        var media = new Media
        {
            MaterialId = material.Id, LotNumber = "TSA/WATER", IsReleasedForUse = true,
            Status = MediaStatus.Active, ExpiryDate = DateTime.UtcNow.AddDays(30)
        };
        var equipment = new Equipment { Name = "Incubator Water", Code = "INC-WATER", Type = EquipmentType.Incubator, SetPointTemperature = 22 };
        db.Media.Add(media);
        db.Equipment.Add(equipment);
        await db.SaveChangesAsync();

        var waterEngine = new MicroLIMS.Application.Workflows.WaterWorkflowEngine(db, new ReferenceNumberGenerator(db));
        var sample = await waterEngine.ReceiveAsync(new MicroLIMS.Application.Workflows.WaterReceiveRequest(department.Id, 0, "500ml", "Analyst", "CTRL-600", 1));
        var prepared = await waterEngine.PrepareAsync(sample.Id, new List<int> { pointA.Id, pointB.Id }, 1, "RoomTemperature");
        var order = prepared.TestOrders.Single();

        var engine = TestServiceFactory.TestWorkflow(db);
        await engine.SelectMediaAsync(order.Id, "CountIncubation", media.Id, equipment.Id, 1);

        var locations = await engine.GetLocationsAsync(order.Id);
        var locationA = locations.Single(l => l.WaterSamplingPointId == pointA.Id);
        var locationB = locations.Single(l => l.WaterSamplingPointId == pointB.Id);

        return (engine, order.Id, locationA.Id, locationB.Id);
    }

    [Fact]
    public async Task RecordWaterBatchReadingsAsync_AveragesReadingsPerLocationNoDilution()
    {
        await using var db = NewDb();
        var (engine, testOrderId, locationAId, locationBId) = await SetupPreparedWaterCountOrderAsync(db);

        var result = await engine.RecordWaterBatchReadingsAsync(testOrderId, new List<MicroLIMS.Application.Workflows.WaterBatchLocationReadings>
        {
            new(locationAId, new List<decimal> { 12, 14 }),   // avg 13 -> AlertLimitExceeded
            new(locationBId, new List<decimal> { 5, 5 })      // avg 5  -> WithinLimits
        }, 1);

        Assert.True(result.AllStepsComplete);

        var locationA = await db.SampleLocations.FirstAsync(l => l.Id == locationAId);
        Assert.Equal(13m, locationA.CalculatedResult);
        Assert.Equal("AlertLimitExceeded", locationA.Status);
        Assert.Equal("12,14", locationA.RawReadings);
        Assert.Equal(0m, locationA.DilutionFactor);

        var locationB = await db.SampleLocations.FirstAsync(l => l.Id == locationBId);
        Assert.Equal(5m, locationB.CalculatedResult);
        Assert.Equal("WithinLimits", locationB.Status);
    }

    [Fact]
    public async Task RecordWaterBatchReadingsAsync_TransitionsTestOrderToReady()
    {
        await using var db = NewDb();
        var (engine, testOrderId, locationAId, locationBId) = await SetupPreparedWaterCountOrderAsync(db);

        await engine.RecordWaterBatchReadingsAsync(testOrderId, new List<MicroLIMS.Application.Workflows.WaterBatchLocationReadings>
        {
            new(locationAId, new List<decimal> { 1 }),
            new(locationBId, new List<decimal> { 1 })
        }, 1);

        var order = await db.TestOrders.FirstAsync(o => o.Id == testOrderId);
        Assert.Equal(WorkflowStep.Ready, order.CurrentStep);
    }

    [Fact]
    public async Task RecordWaterBatchReadingsAsync_RejectsMissingLocation()
    {
        await using var db = NewDb();
        var (engine, testOrderId, locationAId, _) = await SetupPreparedWaterCountOrderAsync(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() => engine.RecordWaterBatchReadingsAsync(testOrderId,
            new List<MicroLIMS.Application.Workflows.WaterBatchLocationReadings> { new(locationAId, new List<decimal> { 1 }) }, 1));
    }

    [Fact]
    public async Task RecordWaterBatchReadingsAsync_RejectsEmptyReadingsForALocation()
    {
        await using var db = NewDb();
        var (engine, testOrderId, locationAId, locationBId) = await SetupPreparedWaterCountOrderAsync(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() => engine.RecordWaterBatchReadingsAsync(testOrderId,
            new List<MicroLIMS.Application.Workflows.WaterBatchLocationReadings>
            {
                new(locationAId, new List<decimal>()),
                new(locationBId, new List<decimal> { 1 })
            }, 1));
    }

    [Fact]
    public async Task UpsertFromSampleLocationAsync_SetsSubjectNameFromWaterSamplingPoint()
    {
        await using var db = NewDb();
        var (engine, testOrderId, locationAId, locationBId) = await SetupPreparedWaterCountOrderAsync(db);

        await engine.RecordWaterBatchReadingsAsync(testOrderId, new List<MicroLIMS.Application.Workflows.WaterBatchLocationReadings>
        {
            new(locationAId, new List<decimal> { 1 }),
            new(locationBId, new List<decimal> { 1 })
        }, 1);

        var record = await db.ResultRecords.FirstAsync(r => r.SourceTable == "SampleLocation" && r.SourceId == locationAId);
        Assert.Equal("SP-A", record.SubjectName);
    }
}
