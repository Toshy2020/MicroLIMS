using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.WorkflowTests;

public class GroupedTestActionTests
{
    private static MicroLimsDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MicroLimsDbContext(options);
    }

    private static async Task<(TestDefinition tamcDef, Material tsaMaterial, Media tsaLot, Equipment incubator)> SeedTamcSetupAsync(MicroLimsDbContext db)
    {
        var incubator = new Equipment
        {
            Name = "INC-01",
            Code = "INC-01",
            Type = EquipmentType.Incubator,
            SetPointTemperature = 32.5m,
            CalibrationDueDate = DateTime.UtcNow.AddMonths(6)
        };
        db.Equipment.Add(incubator);

        var tsaMaterial = new Material
        {
            MaterialName = "Tryptone Soy Agar",
            Code = "TSA"
        };
        db.Materials.Add(tsaMaterial);
        await db.SaveChangesAsync();

        var tsaLot = new Media
        {
            MaterialId = tsaMaterial.Id,
            LotNumber = "TSA/2026/01",
            ExpiryDate = DateTime.UtcNow.AddMonths(6),
            IsReleasedForUse = true,
            Status = MediaStatus.Prepared,
            ApprovalStatus = ApprovalGateStatus.Approved
        };
        db.Media.Add(tsaLot);

        var tamcDef = new TestDefinition
        {
            Code = "TAMC",
            DisplayName = "Total Aerobic Microbial Count",
            WorkflowType = WorkflowType.CountTest
        };
        db.TestDefinitions.Add(tamcDef);
        await db.SaveChangesAsync();

        var tamcStep = new TestWorkflowStep
        {
            TestDefinitionId = tamcDef.Id,
            StepOrder = 1,
            StepName = "TAMC",
            StepType = StepType.PlateCount,
            TemperatureMin = 30.0m,
            TemperatureMax = 35.0m,
            IncubationMinHours = 48,
            IncubationMaxHours = 72,
            IsFinalStep = true
        };
        db.TestWorkflowSteps.Add(tamcStep);
        await db.SaveChangesAsync();

        var stepMedia = new TestWorkflowStepMedia
        {
            TestWorkflowStepId = tamcStep.Id,
            MaterialId = tsaMaterial.Id,
            TempMin = 30.0m,
            TempMax = 35.0m,
            IncubationMinHours = 48,
            IncubationMaxHours = 72,
            IsRequired = true,
            DisplayOrder = 1
        };
        db.TestWorkflowStepMedias.Add(stepMedia);
        await db.SaveChangesAsync();

        return (tamcDef, tsaMaterial, tsaLot, incubator);
    }

    [Fact]
    public async Task GetActionableGroups_GroupsCompatibleTamcTestsAcrossDifferentSamples()
    {
        await using var db = NewDb();
        var (tamcDef, tsaMat, tsaLot, incubator) = await SeedTamcSetupAsync(db);

        // Seed 2 distinct samples with TAMC test orders
        var sample1 = new Sample { ReferenceNumber = "FP001", Category = SampleCategory.FinishedProduct, ReceivedAt = DateTime.UtcNow };
        var sample2 = new Sample { ReferenceNumber = "FP002", Category = SampleCategory.FinishedProduct, ReceivedAt = DateTime.UtcNow };
        db.Samples.AddRange(sample1, sample2);
        await db.SaveChangesAsync();

        var order1 = new TestOrder { SampleId = sample1.Id, TestCode = "TAMC", Status = ApprovalStatus.Pending, CurrentStep = WorkflowStep.Waiting, AssignedAnalystId = 1 };
        var order2 = new TestOrder { SampleId = sample2.Id, TestCode = "TAMC", Status = ApprovalStatus.Pending, CurrentStep = WorkflowStep.Waiting, AssignedAnalystId = 1 };
        db.TestOrders.AddRange(order1, order2);
        await db.SaveChangesAsync();

        var service = TestServiceFactory.GroupedTestAction(db);
        var response = await service.GetActionableGroupsAsync(1, RoleType.Analyst, scope: "mine");

        Assert.Single(response.Groups);
        var group = response.Groups[0];
        Assert.Equal("SETUP_INCUBATION", group.ActionType);
        Assert.Equal("PlateCount", group.StepType);
        Assert.Equal("TAMC", group.StepName);
        Assert.Equal(2, group.SampleCount);
        Assert.Equal(2, group.TestOrderCount);
        Assert.Contains(tsaMat.Id, group.PermittedMaterialIds);
    }

    [Fact]
    public async Task GetActionableGroups_SplitsTestsWithIncompatibleMedia()
    {
        await using var db = NewDb();
        var (tamcDef, tsaMat, tsaLot, incubator) = await SeedTamcSetupAsync(db);

        // Create a distinct water material and test definition (TAMC-Water using R2A)
        var r2aMaterial = new Material { MaterialName = "R2A Agar", Code = "R2A" };
        db.Materials.Add(r2aMaterial);
        await db.SaveChangesAsync();

        var waterTamcDef = new TestDefinition { Code = "TAMC-Water", DisplayName = "Water TAMC", WorkflowType = WorkflowType.CountTest };
        db.TestDefinitions.Add(waterTamcDef);
        await db.SaveChangesAsync();

        var waterStep = new TestWorkflowStep
        {
            TestDefinitionId = waterTamcDef.Id,
            StepOrder = 1,
            StepName = "TAMC-Water",
            StepType = StepType.PlateCount,
            TemperatureMin = 30.0m,
            TemperatureMax = 35.0m,
            IncubationMinHours = 48,
            IncubationMaxHours = 72
        };
        db.TestWorkflowSteps.Add(waterStep);
        await db.SaveChangesAsync();

        db.TestWorkflowStepMedias.Add(new TestWorkflowStepMedia
        {
            TestWorkflowStepId = waterStep.Id,
            MaterialId = r2aMaterial.Id,
            TempMin = 30.0m,
            TempMax = 35.0m,
            IncubationMinHours = 48,
            IncubationMaxHours = 72
        });
        await db.SaveChangesAsync();

        var s1 = new Sample { ReferenceNumber = "FP001", Category = SampleCategory.FinishedProduct };
        var s2 = new Sample { ReferenceNumber = "WT001", Category = SampleCategory.Water };
        db.Samples.AddRange(s1, s2);
        await db.SaveChangesAsync();

        db.TestOrders.Add(new TestOrder { SampleId = s1.Id, TestCode = "TAMC", Status = ApprovalStatus.Pending, CurrentStep = WorkflowStep.Waiting, AssignedAnalystId = 1 });
        db.TestOrders.Add(new TestOrder { SampleId = s2.Id, TestCode = "TAMC-Water", Status = ApprovalStatus.Pending, CurrentStep = WorkflowStep.Waiting, AssignedAnalystId = 1 });
        await db.SaveChangesAsync();

        var service = TestServiceFactory.GroupedTestAction(db);
        var response = await service.GetActionableGroupsAsync(1, RoleType.Analyst, scope: "mine");

        // Should split into two distinct groups because materials do not overlap
        Assert.Equal(2, response.Groups.Count);
        Assert.Contains(response.Groups, g => g.StepName == "TAMC");
        Assert.Contains(response.Groups, g => g.StepName == "TAMC-Water");
    }

    [Fact]
    public async Task GetActionableGroups_RespectsAnalystOwnership_InMineScope()
    {
        await using var db = NewDb();
        await SeedTamcSetupAsync(db);

        var s1 = new Sample { ReferenceNumber = "S1", Category = SampleCategory.FinishedProduct };
        var s2 = new Sample { ReferenceNumber = "S2", Category = SampleCategory.FinishedProduct };
        db.Samples.AddRange(s1, s2);
        await db.SaveChangesAsync();

        // Order 1 assigned to Analyst 1, Order 2 assigned to Analyst 2
        db.TestOrders.Add(new TestOrder { SampleId = s1.Id, TestCode = "TAMC", Status = ApprovalStatus.Pending, CurrentStep = WorkflowStep.Waiting, AssignedAnalystId = 1 });
        db.TestOrders.Add(new TestOrder { SampleId = s2.Id, TestCode = "TAMC", Status = ApprovalStatus.Pending, CurrentStep = WorkflowStep.Waiting, AssignedAnalystId = 2 });
        await db.SaveChangesAsync();

        var service = TestServiceFactory.GroupedTestAction(db);

        var mineResponse = await service.GetActionableGroupsAsync(1, RoleType.Analyst, scope: "mine");
        Assert.Single(mineResponse.Groups);
        Assert.Equal(1, mineResponse.Groups[0].TestOrderCount);
        Assert.Equal("S1", mineResponse.Groups[0].TestOrders[0].SampleReference);

        var allResponse = await service.GetActionableGroupsAsync(1, RoleType.SectionHead, scope: "all");
        Assert.Single(allResponse.Groups);
        Assert.Equal(2, allResponse.Groups[0].TestOrderCount);
    }

    [Fact]
    public async Task ExecuteBatchSelectMedia_CreatesIndependentIncubationsAndWorkflowHistories()
    {
        await using var db = NewDb();
        var (tamcDef, tsaMat, tsaLot, incubator) = await SeedTamcSetupAsync(db);

        var s1 = new Sample { ReferenceNumber = "S1", Category = SampleCategory.FinishedProduct, Status = SampleStatus.Received };
        var s2 = new Sample { ReferenceNumber = "S2", Category = SampleCategory.FinishedProduct, Status = SampleStatus.Received };
        db.Samples.AddRange(s1, s2);
        await db.SaveChangesAsync();

        var o1 = new TestOrder { SampleId = s1.Id, TestCode = "TAMC", Status = ApprovalStatus.Pending, CurrentStep = WorkflowStep.Waiting, AssignedAnalystId = 5 };
        var o2 = new TestOrder { SampleId = s2.Id, TestCode = "TAMC", Status = ApprovalStatus.Pending, CurrentStep = WorkflowStep.Waiting, AssignedAnalystId = 5 };
        db.TestOrders.AddRange(o1, o2);
        await db.SaveChangesAsync();

        var service = TestServiceFactory.GroupedTestAction(db);
        var request = new BatchSelectMediaRequest(
            TestOrderIds: new List<int> { o1.Id, o2.Id },
            StepName: "TAMC",
            MediaLotId: tsaLot.Id,
            IncubatorEquipmentId: incubator.Id
        );

        var result = await service.ExecuteBatchSelectMediaAsync(request, 5, RoleType.Analyst);

        Assert.Equal(2, result.TotalRequested);
        Assert.Equal(2, result.SucceededCount);
        Assert.Empty(result.Skipped);

        // Verify independent Incubation records
        var incubations = await db.Incubations.Where(i => i.TestOrderId == o1.Id || i.TestOrderId == o2.Id).ToListAsync();
        Assert.Equal(2, incubations.Count);
        Assert.NotEqual(incubations[0].Id, incubations[1].Id);
        Assert.All(incubations, i => Assert.Equal(5, i.StartedByUserId));
        Assert.All(incubations, i => Assert.Equal(tsaLot.Id, i.MediaId));
        Assert.All(incubations, i => Assert.Equal(incubator.Id, i.IncubatorEquipmentId));

        // Verify independent WorkflowHistory records
        var histories = await db.WorkflowHistories.Where(h => h.TestOrderId == o1.Id || h.TestOrderId == o2.Id).ToListAsync();
        Assert.Equal(2, histories.Count);
        Assert.All(histories, h => Assert.Equal(WorkflowStep.Incubating, h.ToStep));
        Assert.All(histories, h => Assert.Equal(5, h.PerformedByUserId));

        // Verify both orders transitioned
        var updatedO1 = await db.TestOrders.FindAsync(o1.Id);
        var updatedO2 = await db.TestOrders.FindAsync(o2.Id);
        Assert.Equal(WorkflowStep.Incubating, updatedO1!.CurrentStep);
        Assert.Equal(WorkflowStep.Incubating, updatedO2!.CurrentStep);
    }

    [Fact]
    public async Task ExecuteBatchSelectMedia_SupportsPartialFailure_WithoutAbortingValidOrders()
    {
        await using var db = NewDb();
        var (tamcDef, tsaMat, tsaLot, incubator) = await SeedTamcSetupAsync(db);

        var s1 = new Sample { ReferenceNumber = "S1", Category = SampleCategory.FinishedProduct };
        var s2 = new Sample { ReferenceNumber = "S2", Category = SampleCategory.FinishedProduct };
        db.Samples.AddRange(s1, s2);
        await db.SaveChangesAsync();

        var validOrder = new TestOrder { SampleId = s1.Id, TestCode = "TAMC", Status = ApprovalStatus.Pending, CurrentStep = WorkflowStep.Waiting, AssignedAnalystId = 5 };
        // Invalid order: already Incubating
        var alreadyRunningOrder = new TestOrder { SampleId = s2.Id, TestCode = "TAMC", Status = ApprovalStatus.InProgress, CurrentStep = WorkflowStep.Incubating, AssignedAnalystId = 5 };
        db.TestOrders.AddRange(validOrder, alreadyRunningOrder);
        await db.SaveChangesAsync();

        // Seed existing open incubation on alreadyRunningOrder
        db.Incubations.Add(new Incubation { TestOrderId = alreadyRunningOrder.Id, StepName = "TAMC", StartedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var service = TestServiceFactory.GroupedTestAction(db);
        var request = new BatchSelectMediaRequest(
            TestOrderIds: new List<int> { validOrder.Id, alreadyRunningOrder.Id },
            StepName: "TAMC",
            MediaLotId: tsaLot.Id,
            IncubatorEquipmentId: incubator.Id
        );

        var result = await service.ExecuteBatchSelectMediaAsync(request, 5, RoleType.Analyst);

        Assert.Equal(2, result.TotalRequested);
        Assert.Equal(1, result.SucceededCount);
        Assert.Equal(1, result.SkippedCount);

        Assert.Equal(validOrder.Id, result.Succeeded[0].TestOrderId);
        Assert.Equal(alreadyRunningOrder.Id, result.Skipped[0].TestOrderId);
        Assert.Contains("already", result.Skipped[0].Reason, StringComparison.OrdinalIgnoreCase);

        // Valid order is now incubating
        var refreshedValid = await db.TestOrders.FindAsync(validOrder.Id);
        Assert.Equal(WorkflowStep.Incubating, refreshedValid!.CurrentStep);
    }

    [Fact]
    public async Task ExecuteBatchSelectMedia_RejectsExpiredMedia()
    {
        await using var db = NewDb();
        var (tamcDef, tsaMat, tsaLot, incubator) = await SeedTamcSetupAsync(db);

        // Expire media lot
        tsaLot.ExpiryDate = DateTime.UtcNow.AddDays(-1);
        await db.SaveChangesAsync();

        var s1 = new Sample { ReferenceNumber = "S1", Category = SampleCategory.FinishedProduct };
        db.Samples.Add(s1);
        await db.SaveChangesAsync();

        var order = new TestOrder { SampleId = s1.Id, TestCode = "TAMC", Status = ApprovalStatus.Pending, CurrentStep = WorkflowStep.Waiting, AssignedAnalystId = 5 };
        db.TestOrders.Add(order);
        await db.SaveChangesAsync();

        var service = TestServiceFactory.GroupedTestAction(db);
        var request = new BatchSelectMediaRequest(
            TestOrderIds: new List<int> { order.Id },
            StepName: "TAMC",
            MediaLotId: tsaLot.Id,
            IncubatorEquipmentId: incubator.Id
        );

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ExecuteBatchSelectMediaAsync(request, 5, RoleType.Analyst));
    }

    [Fact]
    public async Task ExecuteBatchSelectMedia_RejectsIneligibleIncubatorTemperature()
    {
        await using var db = NewDb();
        var (tamcDef, tsaMat, tsaLot, incubator) = await SeedTamcSetupAsync(db);

        // Change incubator set point to 42°C (out of 30–35°C TAMC range)
        incubator.SetPointTemperature = 42.0m;
        await db.SaveChangesAsync();

        var s1 = new Sample { ReferenceNumber = "S1", Category = SampleCategory.FinishedProduct };
        db.Samples.Add(s1);
        await db.SaveChangesAsync();

        var order = new TestOrder { SampleId = s1.Id, TestCode = "TAMC", Status = ApprovalStatus.Pending, CurrentStep = WorkflowStep.Waiting, AssignedAnalystId = 5 };
        db.TestOrders.Add(order);
        await db.SaveChangesAsync();

        var service = TestServiceFactory.GroupedTestAction(db);
        var request = new BatchSelectMediaRequest(
            TestOrderIds: new List<int> { order.Id },
            StepName: "TAMC",
            MediaLotId: tsaLot.Id,
            IncubatorEquipmentId: incubator.Id
        );

        var result = await service.ExecuteBatchSelectMediaAsync(request, 5, RoleType.Analyst);
        Assert.Equal(1, result.TotalRequested);
        Assert.Equal(0, result.SucceededCount);
        Assert.Equal(1, result.SkippedCount);
        Assert.Contains("temperature", result.Skipped[0].Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetActionableGroups_DeduplicatesPathogenSharedTsbOnSameSample()
    {
        await using var db = NewDb();
        var (order1, media, incubator) = await PathogenTestData.SeedFiveStageOrderAsync(db);

        // Add second sibling pathogen order on the SAME sample
        var testDef2 = new TestDefinition { Code = "PATHOGEN_ECOLI", DisplayName = "E. coli", WorkflowType = WorkflowType.Observation };
        db.TestDefinitions.Add(testDef2);
        await db.SaveChangesAsync();

        var step1 = new TestWorkflowStep { TestDefinitionId = testDef2.Id, StepOrder = 1, StepName = "Broth Enrichment", IncubationMinHours = 18, IncubationMaxHours = 24, TemperatureMin = 35, TemperatureMax = 37, StepType = StepType.BrothEnrichment };
        db.TestWorkflowSteps.Add(step1);
        await db.SaveChangesAsync();

        db.TestWorkflowStepMedias.Add(new TestWorkflowStepMedia
        {
            TestWorkflowStepId = step1.Id,
            MaterialId = media.BrothMaterialId,
            TempMin = 35,
            TempMax = 37,
            IncubationMinHours = 18,
            IncubationMaxHours = 24,
            IsRequired = true,
            DisplayOrder = 1
        });
        await db.SaveChangesAsync();

        var order2 = new TestOrder { SampleId = order1.SampleId, TestCode = "PATHOGEN_ECOLI", Status = ApprovalStatus.Pending, CurrentStep = WorkflowStep.Waiting, AssignedAnalystId = 1 };
        order1.AssignedAnalystId = 1;
        db.TestOrders.Add(order2);
        await db.SaveChangesAsync();

        var service = TestServiceFactory.GroupedTestAction(db);
        var response = await service.GetActionableGroupsAsync(1, RoleType.Analyst, scope: "mine");

        // Both belong to the same sample and step is Broth Enrichment (Shared TSB).
        // Should only present ONE candidate to avoid duplicate actions on the same sample TSB!
        var tsbGroup = response.Groups.FirstOrDefault(g => g.StepName == "Broth Enrichment");
        Assert.NotNull(tsbGroup);
        Assert.Equal(1, tsbGroup.SampleCount);
        Assert.Equal(1, tsbGroup.TestOrderCount);
    }
}
