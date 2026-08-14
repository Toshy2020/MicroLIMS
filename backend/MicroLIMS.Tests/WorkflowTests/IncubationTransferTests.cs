using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Constants;
using Xunit;

namespace MicroLIMS.Tests.WorkflowTests;

// Two-stage incubation transfer, opted into per PlateCount step via
// RequiresIncubationTransfer. Mirrors CountTestWorkflowTests' seed shape;
// the step's own TemperatureMin/Max/IncubationMinHours/MaxHours describe
// stage 1, and a TestWorkflowStepIncubationStage row (StageNumber == 2)
// describes stage 2.
public class IncubationTransferTests
{
    private static MicroLimsDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MicroLimsDbContext(options);
    }

    private static async Task<(TestOrder order, Media media, TestWorkflowStep step)> SeedTransferOrderAsync(
        MicroLimsDbContext db, int stage1MinHours = 24, int stage1MaxHours = 48, int stage2MinHours = 24, int stage2MaxHours = 48)
    {
        var testDefinition = new TestDefinition { Code = "TAMC-TRANSFER", DisplayName = "TAMC with transfer", WorkflowType = WorkflowType.CountTest };
        var generalAgar = new MediaType { Class = MediaClass.GeneralAgar, IncubationMinHours = 24, IncubationMaxHours = 48, RequiredTemperatureMin = 30, RequiredTemperatureMax = 35 };
        db.TestDefinitions.Add(testDefinition);
        db.MediaTypes.Add(generalAgar);
        await db.SaveChangesAsync();

        var step = new TestWorkflowStep
        {
            TestDefinitionId = testDefinition.Id, StepOrder = 1, StepName = "CountIncubation", MediaTypeId = generalAgar.Id,
            IncubationMinHours = stage1MinHours, IncubationMaxHours = stage1MaxHours, TemperatureMin = 30, TemperatureMax = 35,
            IsFinalStep = true, StepType = StepType.PlateCount, RequiresIncubationTransfer = true
        };
        step.IncubationStages.Add(new TestWorkflowStepIncubationStage
        {
            StageNumber = 2, TempMin = 20, TempMax = 25, IncubationMinHours = stage2MinHours, IncubationMaxHours = stage2MaxHours
        });
        db.TestWorkflowSteps.Add(step);
        await db.SaveChangesAsync();

        var material = new Material
        {
            MaterialType = MaterialType.DehydratedMedia, MaterialName = "TSA Powder", ManufacturerName = "Himedia",
            BatchNumber = "LOT-001", ReceivingDate = DateTime.UtcNow.AddDays(-10), Code = "TSA",
            Location = "Micro Lab", QuantityReceived = 500, QuantityRemaining = 500, Unit = MaterialUnit.Gram
        };
        db.Materials.Add(material);
        await db.SaveChangesAsync();

        var media = new Media { MediaTypeId = generalAgar.Id, MaterialId = material.Id, LotNumber = "TSA/1/26", IsReleasedForUse = true, Status = MediaStatus.Active, ExpiryDate = DateTime.UtcNow.AddDays(30) };
        db.Media.Add(media);

        var point = new WaterSamplingPoint { Code = "WP-01", Location = "Utility Room", AssignedTestCodes = new() { "TAMC-TRANSFER" } };
        db.WaterSamplingPoints.Add(point);
        await db.SaveChangesAsync();

        var sample = new Sample { Category = SampleCategory.Water, WaterSamplingPointId = point.Id, ControlNumber = "CTRL-1", Status = SampleStatus.Received };
        var order = new TestOrder { TestCode = "TAMC-TRANSFER", Status = ApprovalStatus.Pending, CurrentStep = WorkflowStep.Waiting };
        sample.TestOrders.Add(order);
        db.Samples.Add(sample);
        await db.SaveChangesAsync();

        return (order, media, step);
    }

    // Backdates stage 1's window so it reads as already elapsed, without
    // waiting real wall-clock hours in the test.
    private static async Task BackdateOpenIncubationAsync(MicroLimsDbContext db, int testOrderId, string stepName, TimeSpan elapsedSince)
    {
        var incubation = await db.Incubations.FirstAsync(i => i.TestOrderId == testOrderId && i.StepName == stepName && i.CompletedAt == null);
        incubation.StartedAt -= elapsedSince;
        incubation.IncubationStartUtc -= elapsedSince;
        incubation.IncubationEndUtc -= elapsedSince;
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task StartStage2Async_BeforeStage1WindowElapses_ThrowsStage1NotComplete()
    {
        await using var db = NewDb();
        var (order, media, _) = await SeedTransferOrderAsync(db);
        var engine = TestServiceFactory.TestWorkflow(db);
        await engine.SelectMediaAsync(order.Id, "CountIncubation", media.Id, incubatorEquipmentId: 1, userId: 1);

        var ex = await Assert.ThrowsAsync<WorkflowStepException>(() =>
            engine.StartStage2IncubationAsync(order.Id, "CountIncubation", incubatorEquipmentId: 2, userId: 1));

        Assert.Equal(WorkflowErrorCodes.IncubationStage1NotComplete, ex.ErrorCode);
    }

    [Fact]
    public async Task RecordResultAsync_BeforeStage2Started_ThrowsStage2NotStarted()
    {
        await using var db = NewDb();
        var (order, media, _) = await SeedTransferOrderAsync(db);
        var engine = TestServiceFactory.TestWorkflow(db);
        await engine.SelectMediaAsync(order.Id, "CountIncubation", media.Id, incubatorEquipmentId: 1, userId: 1);
        await BackdateOpenIncubationAsync(db, order.Id, "CountIncubation", TimeSpan.FromHours(49));

        var ex = await Assert.ThrowsAsync<WorkflowStepException>(() =>
            engine.RecordResultAsync(order.Id, "CountIncubation", new CountTestPayload(new List<decimal> { 10 }, 1), userId: 1));

        Assert.Equal(WorkflowErrorCodes.IncubationStage2NotStarted, ex.ErrorCode);
    }

    [Fact]
    public async Task RecordResultAsync_BeforeStage2WindowElapses_ThrowsIncubationNotComplete()
    {
        await using var db = NewDb();
        var (order, media, _) = await SeedTransferOrderAsync(db);
        var engine = TestServiceFactory.TestWorkflow(db);
        await engine.SelectMediaAsync(order.Id, "CountIncubation", media.Id, incubatorEquipmentId: 1, userId: 1);
        await BackdateOpenIncubationAsync(db, order.Id, "CountIncubation", TimeSpan.FromHours(49));
        await engine.StartStage2IncubationAsync(order.Id, "CountIncubation", incubatorEquipmentId: 2, userId: 2);

        var ex = await Assert.ThrowsAsync<WorkflowStepException>(() =>
            engine.RecordResultAsync(order.Id, "CountIncubation", new CountTestPayload(new List<decimal> { 10 }, 1), userId: 1));

        Assert.Equal(WorkflowErrorCodes.IncubationNotComplete, ex.ErrorCode);
    }

    [Fact]
    public async Task RecordResultAsync_AfterBothStagesElapse_Succeeds()
    {
        await using var db = NewDb();
        var (order, media, _) = await SeedTransferOrderAsync(db);
        var engine = TestServiceFactory.TestWorkflow(db);
        await engine.SelectMediaAsync(order.Id, "CountIncubation", media.Id, incubatorEquipmentId: 1, userId: 1);
        await BackdateOpenIncubationAsync(db, order.Id, "CountIncubation", TimeSpan.FromHours(49));
        await engine.StartStage2IncubationAsync(order.Id, "CountIncubation", incubatorEquipmentId: 2, userId: 2);
        await BackdateOpenIncubationAsync(db, order.Id, "CountIncubation", TimeSpan.FromHours(49));

        var result = await engine.RecordResultAsync(order.Id, "CountIncubation", new CountTestPayload(new List<decimal> { 10 }, 1), userId: 3);

        Assert.True(result.AllStepsComplete);
        var reading = await db.CountTestReadings.SingleAsync(r => r.TestOrderId == order.Id);
        Assert.Equal(3, reading.EnteredByUserId);
    }

    [Fact]
    public async Task StartStage2Async_CopiesMediaIdFromStage1()
    {
        await using var db = NewDb();
        var (order, media, _) = await SeedTransferOrderAsync(db);
        var engine = TestServiceFactory.TestWorkflow(db);
        await engine.SelectMediaAsync(order.Id, "CountIncubation", media.Id, incubatorEquipmentId: 1, userId: 1);
        await BackdateOpenIncubationAsync(db, order.Id, "CountIncubation", TimeSpan.FromHours(49));

        var stage2 = await engine.StartStage2IncubationAsync(order.Id, "CountIncubation", incubatorEquipmentId: 2, userId: 2);

        Assert.Equal(media.Id, stage2.MediaId);
        Assert.Equal(2, stage2.StageNumber);
        Assert.Equal(2, stage2.StartedByUserId);

        var stage1 = await db.Incubations.SingleAsync(i => i.TestOrderId == order.Id && i.StageNumber == 1);
        Assert.Equal(stage1.Id, stage2.ParentIncubationId);
        Assert.NotNull(stage1.CompletedAt);
        Assert.Equal(1, stage1.StartedByUserId);
    }

    // Regression: a PlateCount step with RequiresIncubationTransfer = false
    // (the default) behaves exactly as CountTestWorkflowTests already
    // proves - no stage-2 gate is reachable at all.
    [Fact]
    public async Task RecordResultAsync_NonTransferStep_RecordsImmediatelyAfterWindowElapses_NoStage2Required()
    {
        await using var db = NewDb();
        var (order, media, step) = await SeedTransferOrderAsync(db);
        step.RequiresIncubationTransfer = false;
        await db.SaveChangesAsync();
        var engine = TestServiceFactory.TestWorkflow(db);

        await engine.SelectMediaAsync(order.Id, "CountIncubation", media.Id, incubatorEquipmentId: 1, userId: 1);

        // No StartStage2IncubationAsync call at all - record-result must
        // succeed as soon as it's invoked, exactly like today, with no
        // stage-related exception.
        var result = await engine.RecordResultAsync(order.Id, "CountIncubation", new CountTestPayload(new List<decimal> { 10 }, 1), userId: 1);
        Assert.True(result.AllStepsComplete);
    }
}
