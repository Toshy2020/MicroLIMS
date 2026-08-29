using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.WorkflowTests;

public class SampleSummaryIncubationStageTests
{
    private static MicroLimsDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MicroLimsDbContext(options);
    }

    private static async Task<(int sampleId, TestOrder order, Media media)> SeedTransferOrderAsync(MicroLimsDbContext db)
    {
        var testDefinition = new TestDefinition { Code = "TAMC-TRANSFER", DisplayName = "TAMC with transfer", WorkflowType = WorkflowType.CountTest };
        db.TestDefinitions.Add(testDefinition);
        await db.SaveChangesAsync();

        var step = new TestWorkflowStep
        {
            TestDefinitionId = testDefinition.Id, StepOrder = 1, StepName = "CountIncubation", 
            IncubationMinHours = 1, IncubationMaxHours = 1, TemperatureMin = 30, TemperatureMax = 35,
            IsFinalStep = true, StepType = StepType.PlateCount, RequiresIncubationTransfer = true
        };
        step.IncubationStages.Add(new TestWorkflowStepIncubationStage { StageNumber = 2, TempMin = 20, TempMax = 25, IncubationMinHours = 1, IncubationMaxHours = 1 });
        db.TestWorkflowSteps.Add(step);

        var material = new Material
        {
            MaterialType = MaterialType.DehydratedMedia, MaterialName = "TSA Powder", ManufacturerName = "Himedia",
            BatchNumber = "LOT-001", ReceivingDate = DateTime.UtcNow.AddDays(-10), Code = "TSA",
            Location = "Micro Lab", QuantityReceived = 500, QuantityRemaining = 500, Unit = MaterialUnit.Gram
        };
        db.Materials.Add(material);
        await db.SaveChangesAsync();
        db.TestWorkflowStepMedias.Add(new TestWorkflowStepMedia { TestWorkflowStepId = step.Id, MaterialId = material.Id, TempMin = 30, TempMax = 35 });

        var media = new Media { MaterialId = material.Id, LotNumber = "TSA/1/26", IsReleasedForUse = true, Status = MediaStatus.Active, ExpiryDate = DateTime.UtcNow.AddDays(30) };
        db.Media.Add(media);
        // First Equipment row added to this fresh in-memory DB, so it gets
        // Id 1 - matching the hardcoded incubatorEquipmentId: 1 the tests
        // below pass to SelectMediaAsync, which now enforces incubator
        // eligibility (temperature must fall within the step medium's range).
        db.Equipment.Add(new Equipment { Name = "Incubator", Code = "INC-1", Type = EquipmentType.Incubator, SetPointTemperature = 32 });

        var point = new WaterSamplingPoint { Code = "WP-01", Location = "Utility Room", AssignedTestCodes = new() { "TAMC-TRANSFER" } };
        db.WaterSamplingPoints.Add(point);
        var cause = new CauseOfTesting { Name = "Routine" };
        db.CausesOfTesting.Add(cause);
        await db.SaveChangesAsync();

        var sample = new Sample { Category = SampleCategory.Water, WaterSamplingPointId = point.Id, CauseOfTestingId = cause.Id, ControlNumber = "CTRL-1", Status = SampleStatus.Received };
        var order = new TestOrder { TestCode = "TAMC-TRANSFER", Status = ApprovalStatus.Pending, CurrentStep = WorkflowStep.Waiting };
        sample.TestOrders.Add(order);
        db.Samples.Add(sample);
        await db.SaveChangesAsync();

        db.Users.Add(new User { Id = 1, FullName = "Alice Analyst", Username = "alice", PasswordHash = "x" });
        db.Users.Add(new User { Id = 2, FullName = "Bob Analyst", Username = "bob", PasswordHash = "x" });
        db.Users.Add(new User { Id = 3, FullName = "Charlie Analyst", Username = "charlie", PasswordHash = "x" });
        await db.SaveChangesAsync();

        return (sample.Id, order, media);
    }

    private static async Task BackdateOpenIncubationAsync(MicroLimsDbContext db, int testOrderId, string stepName, TimeSpan elapsedSince)
    {
        var incubation = await db.Incubations.FirstAsync(i => i.TestOrderId == testOrderId && i.StepName == stepName && i.CompletedAt == null);
        incubation.StartedAt -= elapsedSince;
        incubation.IncubationStartUtc -= elapsedSince;
        incubation.IncubationEndUtc -= elapsedSince;
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task GetSummaryAsync_SameAnalystBothStages_ReportsTrue()
    {
        await using var db = NewDb();
        var (sampleId, order, media) = await SeedTransferOrderAsync(db);
        var engine = TestServiceFactory.TestWorkflow(db);
        await engine.SelectMediaAsync(order.Id, "CountIncubation", media.Id, incubatorEquipmentId: 1, userId: 1);
        await BackdateOpenIncubationAsync(db, order.Id, "CountIncubation", TimeSpan.FromHours(2));
        await engine.StartStage2IncubationAsync(order.Id, "CountIncubation", incubatorEquipmentId: 2, userId: 1);

        var summary = await TestServiceFactory.SampleSummary(db).GetSummaryAsync(sampleId);
        Assert.NotNull(summary);
        Assert.NotEmpty(summary.TestOrders);

        var stage1 = summary.TestOrders[0].Incubations.Single(i => i.StageNumber == 1);
        var stage2 = summary.TestOrders[0].Incubations.Single(i => i.StageNumber == 2);
        Assert.Equal("Alice Analyst", stage1.StartedByName);
        Assert.Equal("Alice Analyst", stage2.StartedByName);
        Assert.Equal("Alice Analyst", stage1.TransferredByName);
        Assert.Null(stage1.SameAnalystBothStages);
        Assert.Equal(true, stage2.SameAnalystBothStages);
    }

    [Fact]
    public async Task GetSummaryAsync_DifferentAnalystsPerStage_ReportsFalse()
    {
        await using var db = NewDb();
        var (sampleId, order, media) = await SeedTransferOrderAsync(db);
        var engine = TestServiceFactory.TestWorkflow(db);
        await engine.SelectMediaAsync(order.Id, "CountIncubation", media.Id, incubatorEquipmentId: 1, userId: 1);
        await BackdateOpenIncubationAsync(db, order.Id, "CountIncubation", TimeSpan.FromHours(2));
        await engine.StartStage2IncubationAsync(order.Id, "CountIncubation", incubatorEquipmentId: 2, userId: 2);

        var summary = await TestServiceFactory.SampleSummary(db).GetSummaryAsync(sampleId);

        var stage1 = summary!.TestOrders[0].Incubations.Single(i => i.StageNumber == 1);
        var stage2 = summary.TestOrders[0].Incubations.Single(i => i.StageNumber == 2);
        Assert.Equal("Alice Analyst", stage1.StartedByName);
        Assert.Equal("Bob Analyst", stage1.TransferredByName);
        Assert.Equal("Bob Analyst", stage2.StartedByName);
        Assert.Equal(false, stage2.SameAnalystBothStages);
    }

    [Fact]
    public async Task GetSummaryAsync_TwoStageTransfer_PopulatesTransferAndCompletionPersonnel()
    {
        await using var db = NewDb();
        var (sampleId, order, media) = await SeedTransferOrderAsync(db);
        var engine = TestServiceFactory.TestWorkflow(db);

        // Stage 1 started by Alice (userId 1)
        await engine.SelectMediaAsync(order.Id, "CountIncubation", media.Id, incubatorEquipmentId: 1, userId: 1);
        await BackdateOpenIncubationAsync(db, order.Id, "CountIncubation", TimeSpan.FromHours(2));

        // Stage 1 -> Stage 2 transferred by Bob (userId 2)
        await engine.StartStage2IncubationAsync(order.Id, "CountIncubation", incubatorEquipmentId: 2, userId: 2);
        await BackdateOpenIncubationAsync(db, order.Id, "CountIncubation", TimeSpan.FromHours(2));

        // Stage 2 completed and final count entered by Charlie (userId 3)
        await engine.RecordResultAsync(order.Id, "CountIncubation", new CountTestPayload(new List<decimal> { 15 }, 1), userId: 3);

        var summary = await TestServiceFactory.SampleSummary(db).GetSummaryAsync(sampleId);
        Assert.NotNull(summary);
        Assert.Single(summary.TestOrders);

        var orderDetail = summary.TestOrders[0];
        Assert.Equal(2, orderDetail.Incubations.Count);

        var stage1 = orderDetail.Incubations.Single(i => i.StageNumber == 1);
        var stage2 = orderDetail.Incubations.Single(i => i.StageNumber == 2);

        // Stage 1 checks
        Assert.Equal("Alice Analyst", stage1.StartedByName);
        Assert.Equal("Bob Analyst", stage1.TransferredByName);
        Assert.NotNull(stage1.TransferredAt);
        Assert.Equal(stage1.TransferredAt, stage1.CompletedAt);
        Assert.Equal("Transferred to stage 2 incubation.", stage1.Outcome);

        // Stage 2 checks
        Assert.Equal("Bob Analyst", stage2.StartedByName);
        Assert.Equal("Charlie Analyst", stage2.CompletedByName);
        Assert.NotNull(stage2.CompletedAt);
        Assert.NotNull(stage2.Outcome);

        // Final result checks
        Assert.Single(orderDetail.CountTestReadings);
        var reading = orderDetail.CountTestReadings[0];
        Assert.Equal("Charlie Analyst", reading.EnteredByName);
        Assert.True(Math.Abs((stage2.CompletedAt!.Value - reading.EnteredAt).TotalSeconds) < 2);
    }
}
