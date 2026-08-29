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
        db.TestDefinitions.Add(testDefinition);
        await db.SaveChangesAsync();

        var countStep = new TestWorkflowStep
        {
            TestDefinitionId = testDefinition.Id, StepOrder = 1, StepName = "CountIncubation", 
            IncubationMinHours = 72, IncubationMaxHours = 120, TemperatureMin = 30, TemperatureMax = 35,
            IsFinalStep = true, StepType = StepType.PlateCount
        };
        db.TestWorkflowSteps.Add(countStep);

        var material = new Material
        {
            MaterialType = MaterialType.DehydratedMedia, MaterialName = "TSA Powder", ManufacturerName = "Himedia",
            BatchNumber = "LOT-001", ReceivingDate = DateTime.UtcNow.AddDays(-10), Code = "TSA",
            Location = "Micro Lab", QuantityReceived = 500, QuantityRemaining = 500, Unit = MaterialUnit.Gram
        };
        // A second, distinct product - not permitted on countStep - so
        // SelectMediaAsync_WrongMediaClass_Throws still has a genuinely
        // wrong medium to reject under the new product-level check (see
        // the Media Configuration Migration plan §3): the old class-level
        // mismatch this test named itself after no longer exists as a
        // concept, but "wrong medium for this step" still does.
        var otherMaterial = new Material
        {
            MaterialType = MaterialType.DehydratedMedia, MaterialName = "Cetrimide Agar Powder", ManufacturerName = "Himedia",
            BatchNumber = "LOT-002", ReceivingDate = DateTime.UtcNow.AddDays(-10), Code = "CAM",
            Location = "Micro Lab", QuantityReceived = 500, QuantityRemaining = 500, Unit = MaterialUnit.Gram
        };
        db.Materials.AddRange(material, otherMaterial);
        await db.SaveChangesAsync();

        // SelectMediaAsync now requires the picked lot's MaterialId to be
        // among a step's configured StepMedia (see the Media Configuration
        // Migration plan §3).
        db.TestWorkflowStepMedias.Add(new TestWorkflowStepMedia { TestWorkflowStepId = countStep.Id, MaterialId = material.Id, TempMin = 30, TempMax = 35, IncubationMinHours = 72, IncubationMaxHours = 120 });

        var generalAgarMedia = new Media { MaterialId = material.Id, LotNumber = "TSA/1/26", IsReleasedForUse = true, Status = MediaStatus.Active, ExpiryDate = DateTime.UtcNow.AddDays(30) };
        var selectiveAgarMedia = new Media { MaterialId = otherMaterial.Id, LotNumber = "CAM/1/26", IsReleasedForUse = true, Status = MediaStatus.Active, ExpiryDate = DateTime.UtcNow.AddDays(30) };
        db.Media.AddRange(generalAgarMedia, selectiveAgarMedia);
        // First Equipment row added to this fresh in-memory DB, so it gets
        // Id 1 - matching the hardcoded incubatorEquipmentId: 1 the tests
        // below pass to SelectMediaAsync, which now enforces incubator
        // eligibility (temperature must fall within the step medium's range).
        db.Equipment.Add(new Equipment { Name = "Incubator", Code = "INC-1", Type = EquipmentType.Incubator, SetPointTemperature = 32 });

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

        Assert.Equal("<1 CFU/mL", result.OutcomeSummary);
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
        Assert.Contains("107", result.OutcomeSummary); // average (90+110+120)/3 = 106.67, rounded to a whole CFU count
        Assert.True(result.AllStepsComplete);

        var incubation = await db.Incubations.FirstAsync(i => i.TestOrderId == order.Id && i.StepName == "CountIncubation");
        Assert.NotNull(incubation.CompletedAt);
        Assert.Equal(result.OutcomeSummary, incubation.Outcome);

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
        var transferStep = new TestWorkflowStep
        {
            TestDefinitionId = testDefinition.Id, StepOrder = 2, StepName = "transfer", 
            IncubationMinHours = 48, IncubationMaxHours = 72, TemperatureMin = 20, TemperatureMax = 25,
            IsFinalStep = true, StepType = StepType.PlateCount
        };
        db.TestWorkflowSteps.Add(transferStep);
        await db.SaveChangesAsync();
        db.TestWorkflowStepMedias.Add(new TestWorkflowStepMedia { TestWorkflowStepId = transferStep.Id, MaterialId = generalAgarMedia.MaterialId, TempMin = 20, TempMax = 25, IncubationMinHours = 48, IncubationMaxHours = 72 });
        // Transfer step's 20-25 window is disjoint from the seeded 30-35
        // incubator (Id 1) - a real incubator can't serve both, same as
        // every other two-window fixture in this suite.
        var transferEquipment = new Equipment { Name = "Incubator Transfer", Code = "INC-TRANSFER", Type = EquipmentType.Incubator, SetPointTemperature = 22 };
        db.Equipment.Add(transferEquipment);
        await db.SaveChangesAsync();

        var engine = TestServiceFactory.TestWorkflow(db);

        await engine.SelectMediaAsync(order.Id, "CountIncubation", generalAgarMedia.Id, incubatorEquipmentId: 1, userId: 1);
        var firstResult = await engine.RecordResultAsync(order.Id, "CountIncubation", new CountTestPayload(new List<decimal> { 10 }, 1), userId: 1);
        Assert.False(firstResult.AllStepsComplete);

        var current = await engine.GetCurrentStepAsync(order.Id);
        Assert.False(current.AllStepsComplete);
        Assert.Equal("transfer", current.Step?.StepName);

        await engine.SelectMediaAsync(order.Id, "transfer", generalAgarMedia.Id, transferEquipment.Id, userId: 1);
        var secondResult = await engine.RecordResultAsync(order.Id, "transfer", new CountTestPayload(new List<decimal> { 12 }, 1), userId: 1);
        Assert.True(secondResult.AllStepsComplete);

        var readings = await db.CountTestReadings.Where(r => r.TestOrderId == order.Id).ToListAsync();
        Assert.Equal(2, readings.Count);
        Assert.Contains(readings, r => r.StepName == "CountIncubation");
        Assert.Contains(readings, r => r.StepName == "transfer");
    }

    [Fact]
    public async Task CountTest_ZeroPlates_DF1_ShowsLessThan1()
    {
        await using var db = NewDb();
        var (order, generalAgarMedia, _) = await SeedTamcOrderAsync(db);
        var engine = TestServiceFactory.TestWorkflow(db);
        await engine.SelectMediaAsync(order.Id, "CountIncubation", generalAgarMedia.Id, incubatorEquipmentId: 1, userId: 1);

        var result = await engine.RecordResultAsync(order.Id, "CountIncubation", new CountTestPayload(new List<string> { "0", "0" }, 1), userId: 1);
        Assert.Equal("<1 CFU/mL", result.OutcomeSummary);
    }

    [Fact]
    public async Task CountTest_ZeroPlates_DF10_ShowsLessThan10()
    {
        await using var db = NewDb();
        var (order, generalAgarMedia, _) = await SeedTamcOrderAsync(db);
        var sample = await db.Samples.FirstAsync(s => s.Id == order.SampleId);
        sample.Category = SampleCategory.FinishedProduct;
        db.SamplePreparations.Add(new SamplePreparation { SampleId = sample.Id, Amount = 10, Unit = "gm", Technique = "PourPlate", DiluentTypeId = 1, NeutralizerId = 1 });
        await db.SaveChangesAsync();

        var engine = TestServiceFactory.TestWorkflow(db);
        await engine.SelectMediaAsync(order.Id, "CountIncubation", generalAgarMedia.Id, incubatorEquipmentId: 1, userId: 1);

        var result = await engine.RecordResultAsync(order.Id, "CountIncubation", new CountTestPayload(new List<string> { "0", "0" }, 10), userId: 1);
        Assert.Equal("<10 CFU/g", result.OutcomeSummary);
    }

    [Fact]
    public async Task CountTest_NumericResult_DF10_Calculates()
    {
        await using var db = NewDb();
        var (order, generalAgarMedia, _) = await SeedTamcOrderAsync(db);
        var sample = await db.Samples.FirstAsync(s => s.Id == order.SampleId);
        sample.Category = SampleCategory.FinishedProduct;
        db.SamplePreparations.Add(new SamplePreparation { SampleId = sample.Id, Amount = 10, Unit = "gm", Technique = "PourPlate", DiluentTypeId = 1, NeutralizerId = 1 });
        await db.SaveChangesAsync();

        var engine = TestServiceFactory.TestWorkflow(db);
        await engine.SelectMediaAsync(order.Id, "CountIncubation", generalAgarMedia.Id, incubatorEquipmentId: 1, userId: 1);

        var result = await engine.RecordResultAsync(order.Id, "CountIncubation", new CountTestPayload(new List<string> { "3", "5" }, 10), userId: 1);
        Assert.Equal("40 CFU/g", result.OutcomeSummary);
        Assert.Equal(4m, result.Average);
        Assert.Equal(40m, result.CalculatedResult);
    }

    [Fact]
    public async Task CountTest_LowCount_BelowLowerLimit()
    {
        await using var db = NewDb();
        var (order, generalAgarMedia, _) = await SeedTamcOrderAsync(db);
        var sample = await db.Samples.FirstAsync(s => s.Id == order.SampleId);
        sample.Category = SampleCategory.FinishedProduct;
        db.SamplePreparations.Add(new SamplePreparation { SampleId = sample.Id, Amount = 10, Unit = "gm", Technique = "PourPlate", DiluentTypeId = 1, NeutralizerId = 1 });
        await db.SaveChangesAsync();

        var engine = TestServiceFactory.TestWorkflow(db);
        await engine.SelectMediaAsync(order.Id, "CountIncubation", generalAgarMedia.Id, incubatorEquipmentId: 1, userId: 1);

        var result = await engine.RecordResultAsync(order.Id, "CountIncubation", new CountTestPayload(new List<string> { "0", "1" }, 10), userId: 1);
        Assert.Equal("<10 CFU/g", result.OutcomeSummary);
        Assert.Equal(0.5m, result.Average);
        Assert.Equal(5m, result.CalculatedResult);
    }

    [Fact]
    public async Task CountTest_Water_DF1_DirectResult()
    {
        await using var db = NewDb();
        var (order, generalAgarMedia, _) = await SeedTamcOrderAsync(db);
        var engine = TestServiceFactory.TestWorkflow(db);
        await engine.SelectMediaAsync(order.Id, "CountIncubation", generalAgarMedia.Id, incubatorEquipmentId: 1, userId: 1);

        var result = await engine.RecordResultAsync(order.Id, "CountIncubation", new CountTestPayload(new List<string> { "2", "3" }, 1), userId: 1);
        Assert.Equal("3 CFU/mL", result.OutcomeSummary); // average 2.5, rounded away-from-zero to 3 for display
        Assert.Equal(2.5m, result.Average); // full-precision average still stored, unrounded
        Assert.Equal(2.5m, result.CalculatedResult);
    }

    [Fact]
    public async Task CountTest_TNTC_SetsRequiresReview()
    {
        await using var db = NewDb();
        var (order, generalAgarMedia, _) = await SeedTamcOrderAsync(db);
        var engine = TestServiceFactory.TestWorkflow(db);
        await engine.SelectMediaAsync(order.Id, "CountIncubation", generalAgarMedia.Id, incubatorEquipmentId: 1, userId: 1);

        var result = await engine.RecordResultAsync(order.Id, "CountIncubation", new CountTestPayload(new List<string> { "TNTC", "15" }, 1), userId: 1);
        Assert.Equal("TNTC", result.OutcomeSummary);
        Assert.Equal("RequiresReview", result.Status);
        Assert.Null(result.Average);
        Assert.Null(result.CalculatedResult);

        var reading = await db.CountTestReadings.FirstAsync(r => r.TestOrderId == order.Id);
        Assert.True(reading.HasNonNumericReading);
        Assert.Equal("TNTC", reading.NonNumericValue);
        Assert.True(reading.RequiresReview);
        Assert.Equal("RequiresReview", reading.Status);
    }

    [Fact]
    public async Task CountTest_Uncountable_SetsRequiresReview()
    {
        await using var db = NewDb();
        var (order, generalAgarMedia, _) = await SeedTamcOrderAsync(db);
        var engine = TestServiceFactory.TestWorkflow(db);
        await engine.SelectMediaAsync(order.Id, "CountIncubation", generalAgarMedia.Id, incubatorEquipmentId: 1, userId: 1);

        var result = await engine.RecordResultAsync(order.Id, "CountIncubation", new CountTestPayload(new List<string> { "Uncountable", "10" }, 1), userId: 1);
        Assert.Equal("Uncountable", result.OutcomeSummary);
        Assert.Equal("RequiresReview", result.Status);

        var reading = await db.CountTestReadings.FirstAsync(r => r.TestOrderId == order.Id);
        Assert.True(reading.HasNonNumericReading);
        Assert.Equal("Uncountable", reading.NonNumericValue);
        Assert.True(reading.RequiresReview);
    }

    [Fact]
    public async Task CountTest_BothPlatesTNTC_SetsRequiresReview()
    {
        await using var db = NewDb();
        var (order, generalAgarMedia, _) = await SeedTamcOrderAsync(db);
        var engine = TestServiceFactory.TestWorkflow(db);
        await engine.SelectMediaAsync(order.Id, "CountIncubation", generalAgarMedia.Id, incubatorEquipmentId: 1, userId: 1);

        var result = await engine.RecordResultAsync(order.Id, "CountIncubation", new CountTestPayload(new List<string> { "TNTC", "TNTC" }, 1), userId: 1);
        Assert.Equal("TNTC", result.OutcomeSummary);
        Assert.Equal("RequiresReview", result.Status);
    }

    [Fact]
    public async Task CountTest_Water_DilutionForcedTo1()
    {
        await using var db = NewDb();
        var (order, generalAgarMedia, _) = await SeedTamcOrderAsync(db);
        var engine = TestServiceFactory.TestWorkflow(db);
        await engine.SelectMediaAsync(order.Id, "CountIncubation", generalAgarMedia.Id, incubatorEquipmentId: 1, userId: 1);

        // Water sample - client sends DF=10, should be overridden to 1
        var result = await engine.RecordResultAsync(order.Id, "CountIncubation", new CountTestPayload(new List<string> { "2", "4" }, 10), userId: 1);
        Assert.Equal("3 CFU/mL", result.OutcomeSummary);
        Assert.Equal(3m, result.CalculatedResult);

        var reading = await db.CountTestReadings.FirstAsync(r => r.TestOrderId == order.Id);
        Assert.Equal(1m, reading.DilutionFactor);
    }

    [Fact]
    public void CountTest_Unit_ProductGm_ReturnsCfuPerGram()
    {
        var unit = TestWorkflowEngine.GetCfuUnit(SampleCategory.FinishedProduct, "gm");
        Assert.Equal("CFU/g", unit);
    }

    [Fact]
    public void CountTest_Unit_Water_ReturnsCfuPerMl()
    {
        var unit = TestWorkflowEngine.GetCfuUnit(SampleCategory.Water, null);
        Assert.Equal("CFU/mL", unit);
    }

    [Fact]
    public void CountTest_Unit_EM_Surface_ReturnsCfuPer25cm2()
    {
        var unit = TestWorkflowEngine.GetCfuUnit(SampleCategory.EnvironmentalMonitoring, "25cm2");
        Assert.Equal("CFU/25cm²", unit);
    }

    [Fact]
    public void CountTest_Unit_EM_PassiveAir_ReturnsCfuPerPlate()
    {
        var unit = TestWorkflowEngine.GetCfuUnit(SampleCategory.EnvironmentalMonitoring, "plate");
        Assert.Equal("CFU/plate/4h", unit);
    }
}
