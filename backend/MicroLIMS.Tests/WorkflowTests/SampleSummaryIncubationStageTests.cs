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
        var generalAgar = new MediaType { Class = MediaClass.GeneralAgar, IncubationMinHours = 24, IncubationMaxHours = 48, RequiredTemperatureMin = 30, RequiredTemperatureMax = 35 };
        db.TestDefinitions.Add(testDefinition);
        db.MediaTypes.Add(generalAgar);
        await db.SaveChangesAsync();

        var step = new TestWorkflowStep
        {
            TestDefinitionId = testDefinition.Id, StepOrder = 1, StepName = "CountIncubation", MediaTypeId = generalAgar.Id,
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

        db.Users.Add(new User { Id = 1, FullName = "Alice Analyst", Username = "alice", PasswordHash = "x" });
        db.Users.Add(new User { Id = 2, FullName = "Bob Analyst", Username = "bob", PasswordHash = "x" });
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

        var stage1 = summary!.TestOrders[0].Incubations.Single(i => i.StageNumber == 1);
        var stage2 = summary.TestOrders[0].Incubations.Single(i => i.StageNumber == 2);
        Assert.Equal("Alice Analyst", stage1.StartedByName);
        Assert.Equal("Alice Analyst", stage2.StartedByName);
        Assert.Null(stage1.SameAnalystBothStages);
        Assert.True(stage2.SameAnalystBothStages);
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

        var stage2 = summary!.TestOrders[0].Incubations.Single(i => i.StageNumber == 2);
        Assert.Equal("Alice Analyst", summary.TestOrders[0].Incubations.Single(i => i.StageNumber == 1).StartedByName);
        Assert.Equal("Bob Analyst", stage2.StartedByName);
        Assert.False(stage2.SameAnalystBothStages);
    }
}
