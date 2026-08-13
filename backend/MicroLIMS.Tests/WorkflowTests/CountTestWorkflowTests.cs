using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.WorkflowTests;

// TAMC/TYMC's shape (WorkflowType.CountTest) run through the generic
// TestWorkflowEngine, driven entirely by a seeded TestWorkflowStep
// template - mirrors the seed shape in DbSeeder.SeedWorkflowTemplates.
public class CountTestWorkflowTests
{
    private static MicroLimsDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MicroLimsDbContext(options);
    }

    private static async Task<(TestOrder order, Media generalAgarMedia, Media selectiveAgarMedia)> SeedTamcOrderAsync(MicroLimsDbContext db)
    {
        var testDefinition = new TestDefinition { Code = "TAMC", DisplayName = "Total Aerobic Microbial Count", WorkflowType = WorkflowType.CountTest };
        var generalAgar = new MediaType { Class = MediaClass.GeneralAgar, IncubationMinHours = 24, IncubationMaxHours = 48, RequiredTemperatureMin = 30, RequiredTemperatureMax = 35 };
        var selectiveAgar = new MediaType { Class = MediaClass.SelectiveAgar, IncubationMinHours = 18, IncubationMaxHours = 24, RequiredTemperatureMin = 32, RequiredTemperatureMax = 35 };
        db.TestDefinitions.Add(testDefinition);
        db.MediaTypes.AddRange(generalAgar, selectiveAgar);
        await db.SaveChangesAsync();

        db.TestWorkflowSteps.Add(new TestWorkflowStep
        {
            TestDefinitionId = testDefinition.Id, StepOrder = 1, StepName = "CountIncubation", MediaTypeId = generalAgar.Id,
            IncubationMinHours = 72, IncubationMaxHours = 120, TemperatureMin = 30, TemperatureMax = 35,
            IsFinalStep = true, StepType = StepType.PlateCount
        });

        var material = new Material
        {
            MaterialType = MaterialType.DehydratedMedia, MaterialName = "TSA Powder", ManufacturerName = "Himedia",
            BatchNumber = "LOT-001", ReceivingDate = DateTime.UtcNow.AddDays(-10), Code = "TSA",
            Location = "Micro Lab", QuantityReceived = 500, QuantityRemaining = 500, Unit = MaterialUnit.Gram
        };
        db.Materials.Add(material);
        await db.SaveChangesAsync();

        var generalAgarMedia = new Media { MediaTypeId = generalAgar.Id, MaterialId = material.Id, LotNumber = "TSA/1/26", IsReleasedForUse = true, Status = MediaStatus.Active, ExpiryDate = DateTime.UtcNow.AddDays(30) };
        var selectiveAgarMedia = new Media { MediaTypeId = selectiveAgar.Id, MaterialId = material.Id, LotNumber = "CAM/1/26", IsReleasedForUse = true, Status = MediaStatus.Active, ExpiryDate = DateTime.UtcNow.AddDays(30) };
        db.Media.AddRange(generalAgarMedia, selectiveAgarMedia);

        var point = new WaterSamplingPoint { Code = "WP-01", Location = "Utility Room", AssignedTestCodes = new() { "TAMC" } };
        db.WaterSamplingPoints.Add(point);
        await db.SaveChangesAsync();

        db.SamplingConfigurations.Add(new SamplingConfiguration { WaterSamplingPointId = point.Id, TestCode = "TAMC", AlertLimit = "10", ActionLimit = "50", SpecLimit = "100" });

        var sample = new Sample { Category = SampleCategory.Water, WaterSamplingPointId = point.Id, ControlNumber = "CTRL-1", Status = SampleStatus.Received };
        var order = new TestOrder { TestCode = "TAMC", Status = ApprovalStatus.Pending, CurrentStep = WorkflowStep.Waiting };
        sample.TestOrders.Add(order);
        db.Samples.Add(sample);
        await db.SaveChangesAsync();

        return (order, generalAgarMedia, selectiveAgarMedia);
    }

    [Fact]
    public async Task SelectMediaAsync_WrongMediaClass_Throws()
    {
        await using var db = NewDb();
        var (order, _, selectiveAgarMedia) = await SeedTamcOrderAsync(db);
        var engine = TestServiceFactory.TestWorkflow(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            engine.SelectMediaAsync(order.Id, "CountIncubation", selectiveAgarMedia.Id, incubatorEquipmentId: 1, userId: 1));
        Assert.Contains("requires", ex.Message);
    }

    [Fact]
    public async Task RecordResultAsync_BeforeSelectMediaAsync_Throws()
    {
        await using var db = NewDb();
        var (order, _, _) = await SeedTamcOrderAsync(db);
        var engine = TestServiceFactory.TestWorkflow(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            engine.RecordResultAsync(order.Id, "CountIncubation", new CountTestPayload(new List<decimal> { 50 }, 1), userId: 1));
    }

    [Fact]
    public async Task SelectMediaAsync_ApprovedMedia_LocksTemperatureAndDurationFromStepTemplate()
    {
        await using var db = NewDb();
        var (order, generalAgarMedia, _) = await SeedTamcOrderAsync(db);
        var engine = TestServiceFactory.TestWorkflow(db);

        var incubation = await engine.SelectMediaAsync(order.Id, "CountIncubation", generalAgarMedia.Id, incubatorEquipmentId: 1, userId: 1);

        // Locked from the STEP template (72-120h, 30-35C), not from the MediaType's own values (24-48h).
        Assert.Equal("30-35 °C", incubation.Temperature);
        Assert.Equal("72-120 hours", incubation.Duration);

        var reloaded = await db.TestOrders.FirstAsync(t => t.Id == order.Id);
        Assert.Equal(WorkflowStep.Incubating, reloaded.CurrentStep);
    }

    [Fact]
    public async Task RecordResultAsync_ResultBelowOne_ReportsLessThanOneNotZero()
    {
        await using var db = NewDb();
        var (order, generalAgarMedia, _) = await SeedTamcOrderAsync(db);
        var engine = TestServiceFactory.TestWorkflow(db);
        await engine.SelectMediaAsync(order.Id, "CountIncubation", generalAgarMedia.Id, incubatorEquipmentId: 1, userId: 1);

        var result = await engine.RecordResultAsync(order.Id, "CountIncubation", new CountTestPayload(new List<decimal> { 0, 1 }, 1), userId: 1);

        Assert.Equal("<1", result.OutcomeSummary);
    }

    [Fact]
    public async Task RecordResultAsync_ExceedsSpecLimit_FlagsOutOfSpecificationAndTransitionsToReady()
    {
        await using var db = NewDb();
        var (order, generalAgarMedia, _) = await SeedTamcOrderAsync(db);
        var engine = TestServiceFactory.TestWorkflow(db);
        await engine.SelectMediaAsync(order.Id, "CountIncubation", generalAgarMedia.Id, incubatorEquipmentId: 1, userId: 1);

        var result = await engine.RecordResultAsync(order.Id, "CountIncubation", new CountTestPayload(new List<decimal> { 90, 110, 120 }, 1), userId: 1);

        Assert.Equal("OutOfSpecification", result.Status);
        Assert.Equal("107", result.OutcomeSummary); // average (90+110+120)/3 = 106.67, rounded
        Assert.True(result.AllStepsComplete);

        var incubation = await db.Incubations.FirstAsync(i => i.TestOrderId == order.Id && i.StepName == "CountIncubation");
        Assert.NotNull(incubation.CompletedAt);
        Assert.Equal("107", incubation.Outcome);

        var reloadedOrder = await db.TestOrders.FirstAsync(t => t.Id == order.Id);
        Assert.Equal(WorkflowStep.Ready, reloadedOrder.CurrentStep);

        var savedResult = await db.Results.FirstAsync(r => r.TestOrderId == order.Id);
        Assert.Equal(ResultType.Numeric, savedResult.Type);
        Assert.Contains("107", savedResult.InterpretedValue);
    }

    // Regression test: a CountTest TestDefinition with more than one step
    // (e.g. "TAMC Surface sample"'s CountIncubation -> transfer template)
    // must not be treated as complete after only the first, non-final
    // step's reading is recorded - IsStepDoneAsync has to check per step,
    // not "does any CountTestReading exist for this test order at all".
    [Fact]
    public async Task RecordResultAsync_MultiStepTemplate_DoesNotSkipSecondStep()
    {
        await using var db = NewDb();
        var (order, generalAgarMedia, _) = await SeedTamcOrderAsync(db);

        var testDefinition = await db.TestDefinitions.FirstAsync(t => t.Code == "TAMC");
        var firstStep = await db.TestWorkflowSteps.FirstAsync(s => s.TestDefinitionId == testDefinition.Id && s.StepName == "CountIncubation");
        firstStep.IsFinalStep = false;
        db.TestWorkflowSteps.Add(new TestWorkflowStep
        {
            TestDefinitionId = testDefinition.Id, StepOrder = 2, StepName = "transfer", MediaTypeId = generalAgarMedia.MediaTypeId,
            IncubationMinHours = 48, IncubationMaxHours = 72, TemperatureMin = 20, TemperatureMax = 25,
            IsFinalStep = true, StepType = StepType.PlateCount
        });
        await db.SaveChangesAsync();

        var engine = TestServiceFactory.TestWorkflow(db);

        await engine.SelectMediaAsync(order.Id, "CountIncubation", generalAgarMedia.Id, incubatorEquipmentId: 1, userId: 1);
        var firstResult = await engine.RecordResultAsync(order.Id, "CountIncubation", new CountTestPayload(new List<decimal> { 10 }, 1), userId: 1);
        Assert.False(firstResult.AllStepsComplete);

        var current = await engine.GetCurrentStepAsync(order.Id);
        Assert.False(current.AllStepsComplete);
        Assert.Equal("transfer", current.Step?.StepName);

        await engine.SelectMediaAsync(order.Id, "transfer", generalAgarMedia.Id, incubatorEquipmentId: 1, userId: 1);
        var secondResult = await engine.RecordResultAsync(order.Id, "transfer", new CountTestPayload(new List<decimal> { 12 }, 1), userId: 1);
        Assert.True(secondResult.AllStepsComplete);

        var readings = await db.CountTestReadings.Where(r => r.TestOrderId == order.Id).ToListAsync();
        Assert.Equal(2, readings.Count);
        Assert.Contains(readings, r => r.StepName == "CountIncubation");
        Assert.Contains(readings, r => r.StepName == "transfer");
    }
}
