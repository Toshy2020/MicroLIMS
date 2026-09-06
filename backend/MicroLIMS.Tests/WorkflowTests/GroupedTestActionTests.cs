using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Constants;
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

    [Fact]
    public async Task StartSharedTsb_SetsSiblingPathogenTestsToIncubating_WithActiveIncubation()
    {
        await using var db = NewDb();
        var (order1, media, incubator) = await PathogenTestData.SeedFiveStageOrderAsync(db);

        // Add second sibling pathogen order on the same sample
        var testDef2 = new TestDefinition { Code = "PATHOGEN_ECOLI", DisplayName = "E. coli", WorkflowType = WorkflowType.Observation };
        db.TestDefinitions.Add(testDef2);
        await db.SaveChangesAsync();

        var step1 = new TestWorkflowStep { TestDefinitionId = testDef2.Id, StepOrder = 1, StepName = "Broth Enrichment", IncubationMinHours = 18, IncubationMaxHours = 24, TemperatureMin = 35, TemperatureMax = 37, StepType = StepType.BrothEnrichment };
        var step2 = new TestWorkflowStep { TestDefinitionId = testDef2.Id, StepOrder = 2, StepName = "MacConkey Broth Purple", IncubationMinHours = 24, IncubationMaxHours = 48, TemperatureMin = 35, TemperatureMax = 37, StepType = StepType.SelectiveBroth };
        db.TestWorkflowSteps.AddRange(step1, step2);
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

        var order2 = new TestOrder
        {
            SampleId = order1.SampleId,
            TestCode = "PATHOGEN_ECOLI",
            Status = ApprovalStatus.Pending,
            CurrentStep = WorkflowStep.Waiting,
            AssignedAnalystId = 4
        };
        db.TestOrders.Add(order2);
        await db.SaveChangesAsync();

        var engine = TestServiceFactory.TestWorkflow(db);

        // Start TSB on order1
        await engine.SelectMediaAsync(order1.Id, "Broth Enrichment", media.BrothLotId, incubator.Id, userId: 4);

        // Verify order1 is Incubating
        var refreshedOrder1 = await db.TestOrders.FindAsync(order1.Id);
        Assert.Equal(WorkflowStep.Incubating, refreshedOrder1!.CurrentStep);

        // Verify sibling order2 was transitioned to Incubating as well
        var refreshedOrder2 = await db.TestOrders.FindAsync(order2.Id);
        Assert.Equal(WorkflowStep.Incubating, refreshedOrder2!.CurrentStep);

        // Verify active incubation on sibling order2
        var siblingInc = await db.Incubations.FirstOrDefaultAsync(i => i.TestOrderId == order2.Id && i.StepName == "Broth Enrichment");
        Assert.NotNull(siblingInc);
        Assert.Null(siblingInc.CompletedAt);
        Assert.NotNull(siblingInc.IncubationStartUtc);
        Assert.NotNull(siblingInc.IncubationEndUtc);
        var durationHours = (siblingInc.IncubationEndUtc.Value - siblingInc.IncubationStartUtc.Value).TotalHours;
        Assert.InRange(durationHours, 18, 24);

        // Verify WorkflowStepResult exists for sibling
        var siblingWsr = await db.WorkflowStepResults.FirstOrDefaultAsync(r => r.TestOrderId == order2.Id && r.StepName == "Broth Enrichment");
        Assert.NotNull(siblingWsr);
        Assert.True(siblingWsr.IsSharedSessionStep);
    }

    [Fact]
    public async Task GetActionableGroups_DoesNotReturnStage2Steps_WhilePredecessorTsbIsIncubating()
    {
        await using var db = NewDb();
        var (order1, media, incubator) = await PathogenTestData.SeedFiveStageOrderAsync(db);

        // Add second sibling pathogen order on the same sample
        var testDef2 = new TestDefinition { Code = "PATHOGEN_ECOLI", DisplayName = "E. coli", WorkflowType = WorkflowType.Observation };
        db.TestDefinitions.Add(testDef2);
        await db.SaveChangesAsync();

        var step1 = new TestWorkflowStep { TestDefinitionId = testDef2.Id, StepOrder = 1, StepName = "Broth Enrichment", IncubationMinHours = 18, IncubationMaxHours = 24, TemperatureMin = 35, TemperatureMax = 37, StepType = StepType.BrothEnrichment };
        var step2 = new TestWorkflowStep { TestDefinitionId = testDef2.Id, StepOrder = 2, StepName = "MacConkey Broth Purple", IncubationMinHours = 24, IncubationMaxHours = 48, TemperatureMin = 35, TemperatureMax = 37, StepType = StepType.SelectiveBroth };
        db.TestWorkflowSteps.AddRange(step1, step2);
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

        var order2 = new TestOrder
        {
            SampleId = order1.SampleId,
            TestCode = "PATHOGEN_ECOLI",
            Status = ApprovalStatus.Pending,
            CurrentStep = WorkflowStep.Waiting,
            AssignedAnalystId = 4
        };
        db.TestOrders.Add(order2);
        await db.SaveChangesAsync();

        var engine = TestServiceFactory.TestWorkflow(db);

        // Start TSB on order1 (propagates to sibling order2)
        await engine.SelectMediaAsync(order1.Id, "Broth Enrichment", media.BrothLotId, incubator.Id, userId: 4);

        var service = TestServiceFactory.GroupedTestAction(db);
        var response = await service.GetActionableGroupsAsync(4, RoleType.Analyst, scope: "mine");

        // Neither Broth Enrichment (actively incubating) nor Stage 2 steps (Selective Broth, MacConkey Broth) should be returned
        Assert.DoesNotContain(response.Groups, g => g.StepName == "Broth Enrichment");
        Assert.DoesNotContain(response.Groups, g => g.StepName == "Selective Broth");
        Assert.DoesNotContain(response.Groups, g => g.StepName == "MacConkey Broth Purple");
    }

    [Fact]
    public async Task SelectMediaAsync_OnStage2_ThrowsPredecessorStepIncubationActiveException_WhenTsbIncubating()
    {
        await using var db = NewDb();
        var (order1, media, incubator) = await PathogenTestData.SeedFiveStageOrderAsync(db);

        var engine = TestServiceFactory.TestWorkflow(db);

        // Start TSB on order1
        await engine.SelectMediaAsync(order1.Id, "Broth Enrichment", media.BrothLotId, incubator.Id, userId: 4);

        // Attempting to select media / start Stage 2 (Selective Broth) while TSB incubation is active
        var ex = await Assert.ThrowsAsync<PredecessorStepIncubationActiveException>(() =>
            engine.SelectMediaAsync(order1.Id, "Selective Broth", media.SelectiveBrothLotId, media.SelectiveBrothIncubatorId, userId: 4));

        Assert.Equal(WorkflowErrorCodes.PredecessorStepIncubationActive, ex.ErrorCode);
        Assert.Equal("Broth Enrichment", ex.PredecessorStepName);
        Assert.True(ex.RemainingSeconds > 0);
    }

    [Fact]
    public async Task CompletingTsb_AllowsStage2ToBecomeActionable_AndStartIncubation()
    {
        await using var db = NewDb();
        var (order1, media, incubator) = await PathogenTestData.SeedFiveStageOrderAsync(db);

        var engine = TestServiceFactory.TestWorkflow(db);

        // Start TSB
        var inc = await engine.SelectMediaAsync(order1.Id, "Broth Enrichment", media.BrothLotId, incubator.Id, userId: 4);

        // Fast-forward incubation window to past so minimum duration is satisfied
        inc.StartedAt = DateTime.UtcNow.AddHours(-25);
        inc.IncubationStartUtc = DateTime.UtcNow.AddHours(-25);
        inc.IncubationEndUtc = DateTime.UtcNow.AddHours(-1);
        await db.SaveChangesAsync();

        // Complete Broth Enrichment
        await engine.SubmitBrothAsync(order1.Id, "Broth Enrichment", "Turbidity observed", userId: 4);

        // Check actionable groups - Stage 2 (Selective Broth) should now be actionable!
        var service = TestServiceFactory.GroupedTestAction(db);
        var response = await service.GetActionableGroupsAsync(4, RoleType.Analyst, scope: "mine");

        var stage2Group = response.Groups.FirstOrDefault(g => g.StepName == "Selective Broth");
        Assert.NotNull(stage2Group);
        Assert.Contains(stage2Group.TestOrders, to => to.TestOrderId == order1.Id);

        // Now starting Stage 2 should succeed without throwing predecessor active exception
        var stage2Inc = await engine.SelectMediaAsync(order1.Id, "Selective Broth", media.SelectiveBrothLotId, media.SelectiveBrothIncubatorId, userId: 4);
        Assert.NotNull(stage2Inc);
        Assert.Equal("Selective Broth", stage2Inc.StepName);
        Assert.Null(stage2Inc.CompletedAt);

        var refreshed = await db.TestOrders.FindAsync(order1.Id);
        Assert.Equal(WorkflowStep.Incubating, refreshed!.CurrentStep);
    }

    #region 10 Post-Implementation Grouped Actions Verification Scenarios

    private static async Task<(TestDefinition def, SeededMedia media, Equipment incubator36, Equipment incubator42)> SeedPathogenMasterDataAsync(MicroLimsDbContext db)
    {
        var organism = new Organism { ScientificName = "Salmonella enterica" };
        db.Organisms.Add(organism);

        var incubator36 = new Equipment { Name = "INC-03", Code = "INC-03", Type = EquipmentType.Incubator, SetPointTemperature = 36 };
        var incubator42 = new Equipment { Name = "INC-07", Code = "INC-07", Type = EquipmentType.Incubator, SetPointTemperature = 42 };
        db.Equipment.AddRange(incubator36, incubator42);

        var test = new TestDefinition { Code = "PATHOGEN_SALMONELLA", DisplayName = "Salmonella", WorkflowType = WorkflowType.Observation };
        db.TestDefinitions.Add(test);
        await db.SaveChangesAsync();

        var steps = new[]
        {
            new TestWorkflowStep { TestDefinitionId = test.Id, StepOrder = 1, StepName = "Broth Enrichment", IncubationMinHours = 18, IncubationMaxHours = 24, TemperatureMin = 35, TemperatureMax = 37, StepType = StepType.BrothEnrichment },
            new TestWorkflowStep { TestDefinitionId = test.Id, StepOrder = 2, StepName = "Selective Broth", IncubationMinHours = 18, IncubationMaxHours = 24, TemperatureMin = 41, TemperatureMax = 43, StepType = StepType.SelectiveBroth },
            new TestWorkflowStep { TestDefinitionId = test.Id, StepOrder = 3, StepName = "Selective Plating", IncubationMinHours = 18, IncubationMaxHours = 24, TemperatureMin = 35, TemperatureMax = 37, StepType = StepType.SelectivePlating, TargetOrganismId = organism.Id },
            new TestWorkflowStep { TestDefinitionId = test.Id, StepOrder = 4, StepName = "Confirmatory Plating", IncubationMinHours = 18, IncubationMaxHours = 24, TemperatureMin = 35, TemperatureMax = 37, StepType = StepType.ConfirmatoryPlating, TargetOrganismId = organism.Id },
            new TestWorkflowStep { TestDefinitionId = test.Id, StepOrder = 5, StepName = "Biochemical Test", IncubationMinHours = 0, IncubationMaxHours = 0, TemperatureMin = 35, TemperatureMax = 37, IsFinalStep = true, StepType = StepType.BiochemicalTest }
        };
        db.TestWorkflowSteps.AddRange(steps);
        await db.SaveChangesAsync();

        var (brothMaterial, brothLot) = await AddMaterialAndMediaAsync(db, "Tryptone Soya Broth", "TSB/1/26");
        var (selBrothMaterial, selBrothLot) = await AddMaterialAndMediaAsync(db, "Rappaport Vassiliadis Broth", "RVS/1/26");
        var (platingMaterial, platingLot) = await AddMaterialAndMediaAsync(db, "XLD Agar", "XLD/1/26");
        var (tsiMaterial, tsiLot) = await AddMaterialAndMediaAsync(db, "TSI Agar", "TSI/1/26");

        db.TestWorkflowStepMedias.AddRange(
            new TestWorkflowStepMedia { TestWorkflowStepId = steps[0].Id, MaterialId = brothMaterial.Id, TempMin = 35, TempMax = 37, IncubationMinHours = 18, IncubationMaxHours = 24, IsRequired = true, DisplayOrder = 1 },
            new TestWorkflowStepMedia { TestWorkflowStepId = steps[1].Id, MaterialId = selBrothMaterial.Id, TempMin = 41, TempMax = 43, IncubationMinHours = 18, IncubationMaxHours = 24, IsRequired = true, DisplayOrder = 1 },
            new TestWorkflowStepMedia { TestWorkflowStepId = steps[2].Id, MaterialId = platingMaterial.Id, TempMin = 35, TempMax = 37, IncubationMinHours = 18, IncubationMaxHours = 24, IsRequired = true, DisplayOrder = 1 });
        var xldStepMedia = new TestWorkflowStepMedia { TestWorkflowStepId = steps[3].Id, MaterialId = platingMaterial.Id, TempMin = 35, TempMax = 37, IncubationMinHours = 18, IncubationMaxHours = 24, IsRequired = false, DisplayOrder = 1 };
        var tsiStepMedia = new TestWorkflowStepMedia { TestWorkflowStepId = steps[3].Id, MaterialId = tsiMaterial.Id, TempMin = 35, TempMax = 37, IncubationMinHours = 18, IncubationMaxHours = 24, IsRequired = false, DisplayOrder = 2 };
        db.TestWorkflowStepMedias.AddRange(xldStepMedia, tsiStepMedia);
        await db.SaveChangesAsync();

        var media = new SeededMedia(brothLot.Id, selBrothLot.Id, platingLot.Id, platingLot.Id, tsiLot.Id,
            brothMaterial.Id, selBrothMaterial.Id, platingMaterial.Id, platingMaterial.Id, tsiMaterial.Id,
            xldStepMedia.Id, tsiStepMedia.Id, incubator42.Id);

        return (test, media, incubator36, incubator42);
    }

    private static async Task<(Material material, Media lot)> AddMaterialAndMediaAsync(MicroLimsDbContext db, string materialName, string lotNumber)
    {
        var material = new Material
        {
            MaterialType = MaterialType.DehydratedMedia, MaterialName = materialName, ManufacturerName = "Himedia",
            BatchNumber = $"LOT-{lotNumber}", ReceivingDate = DateTime.UtcNow.AddDays(-10), Location = "Micro Lab",
            QuantityReceived = 500, QuantityRemaining = 500, Unit = MaterialUnit.Gram, Code = lotNumber.Split('/')[0]
        };
        db.Materials.Add(material);
        await db.SaveChangesAsync();

        var lot = new Media { MaterialId = material.Id, LotNumber = lotNumber, IsReleasedForUse = true, Status = MediaStatus.Active, ExpiryDate = DateTime.UtcNow.AddDays(30) };
        db.Media.Add(lot);
        await db.SaveChangesAsync();
        return (material, lot);
    }

    private static async Task<(Sample sample, TestOrder order)> AddPathogenSampleOrderAsync(
        MicroLimsDbContext db, string refNum, string testCode = "PATHOGEN_SALMONELLA", int analystId = 1)
    {
        var sample = new Sample { ReferenceNumber = refNum, ControlNumber = $"CTRL-{refNum}", Category = SampleCategory.FinishedProduct, Status = SampleStatus.Received };
        db.Samples.Add(sample);
        await db.SaveChangesAsync();

        var order = new TestOrder { SampleId = sample.Id, TestCode = testCode, Status = ApprovalStatus.Pending, CurrentStep = WorkflowStep.Waiting, AssignedAnalystId = analystId };
        db.TestOrders.Add(order);
        await db.SaveChangesAsync();
        return (sample, order);
    }

    [Fact]
    public async Task Scenario1_TwoSamples_AtTsbMinIncubationComplete_GroupedActionAvailable_TransferSelective()
    {
        await using var db = NewDb();
        var (def, media, inc36, inc42) = await SeedPathogenMasterDataAsync(db);

        var (s1, o1) = await AddPathogenSampleOrderAsync(db, "OSTEO-01");
        var (s2, o2) = await AddPathogenSampleOrderAsync(db, "OSTEO-02");

        // Both samples started TSB (Broth Enrichment) 20 hours ago (min is 18h)
        var startUtc = DateTime.UtcNow.AddHours(-20);
        var endUtc = DateTime.UtcNow.AddHours(4);

        db.Incubations.AddRange(
            new Incubation { TestOrderId = o1.Id, StepNumber = 1, StepName = "Broth Enrichment", MediaId = media.BrothLotId, IncubatorEquipmentId = inc36.Id, StartedAt = startUtc, IncubationStartUtc = startUtc, IncubationEndUtc = endUtc, StartedByUserId = 1 },
            new Incubation { TestOrderId = o2.Id, StepNumber = 1, StepName = "Broth Enrichment", MediaId = media.BrothLotId, IncubatorEquipmentId = inc36.Id, StartedAt = startUtc, IncubationStartUtc = startUtc, IncubationEndUtc = endUtc, StartedByUserId = 1 }
        );
        o1.CurrentStep = WorkflowStep.Incubating;
        o2.CurrentStep = WorkflowStep.Incubating;
        await db.SaveChangesAsync();

        var service = TestServiceFactory.GroupedTestAction(db);
        var response = await service.GetActionableGroupsAsync(1, RoleType.Analyst, scope: "mine");

        // Assert grouped action is available for TRANSFER_SELECTIVE to Selective Broth
        Assert.Single(response.Groups);
        var group = response.Groups[0];
        Assert.Equal("TRANSFER_SELECTIVE", group.TransitionType);
        Assert.Equal("Selective Broth", group.StepName);
        Assert.Equal("Broth Enrichment", group.PredecessorStepName);
        Assert.Equal(2, group.SampleCount);
        Assert.Equal(2, group.TestOrderCount);
        Assert.Contains(media.SelectiveBrothMaterialId, group.PermittedMaterialIds);
        Assert.Equal(0, response.ExcludedResultEntryCount);
    }

    [Fact]
    public async Task Scenario2_TwoSamples_AtSelectivePlateIncubationComplete_ExcludedResultEntry_NotGroupable()
    {
        await using var db = NewDb();
        var (def, media, inc36, inc42) = await SeedPathogenMasterDataAsync(db);

        var (s1, o1) = await AddPathogenSampleOrderAsync(db, "OSTEO-01");
        var (s2, o2) = await AddPathogenSampleOrderAsync(db, "OSTEO-02");

        // Complete steps 1 and 2
        var brothEnd = DateTime.UtcNow.AddHours(-40);
        db.WorkflowStepResults.AddRange(
            new WorkflowStepResult { TestOrderId = o1.Id, StepName = "Broth Enrichment", StepType = StepType.BrothEnrichment, SubmittedAtUtc = brothEnd, SubmittedByUserId = 1 },
            new WorkflowStepResult { TestOrderId = o1.Id, StepName = "Selective Broth", StepType = StepType.SelectiveBroth, SubmittedAtUtc = brothEnd, SubmittedByUserId = 1 },
            new WorkflowStepResult { TestOrderId = o2.Id, StepName = "Broth Enrichment", StepType = StepType.BrothEnrichment, SubmittedAtUtc = brothEnd, SubmittedByUserId = 1 },
            new WorkflowStepResult { TestOrderId = o2.Id, StepName = "Selective Broth", StepType = StepType.SelectiveBroth, SubmittedAtUtc = brothEnd, SubmittedByUserId = 1 }
        );

        // Step 3 (Selective Plating) incubation has completed minimum duration (25 hours ago, min 18h)
        var startUtc = DateTime.UtcNow.AddHours(-25);
        var endUtc = DateTime.UtcNow.AddHours(-1);
        db.Incubations.AddRange(
            new Incubation { TestOrderId = o1.Id, StepNumber = 3, StepName = "Selective Plating", MediaId = media.SelectivePlatingLotId, IncubatorEquipmentId = inc36.Id, StartedAt = startUtc, IncubationStartUtc = startUtc, IncubationEndUtc = endUtc, StartedByUserId = 1 },
            new Incubation { TestOrderId = o2.Id, StepNumber = 3, StepName = "Selective Plating", MediaId = media.SelectivePlatingLotId, IncubatorEquipmentId = inc36.Id, StartedAt = startUtc, IncubationStartUtc = startUtc, IncubationEndUtc = endUtc, StartedByUserId = 1 }
        );
        o1.CurrentStep = WorkflowStep.Incubating;
        o2.CurrentStep = WorkflowStep.Incubating;
        await db.SaveChangesAsync();

        var service = TestServiceFactory.GroupedTestAction(db);
        var response = await service.GetActionableGroupsAsync(1, RoleType.Analyst, scope: "mine");

        // Must NOT produce actionable grouped action; must report in ExcludedResultEntryCount
        Assert.Empty(response.Groups);
        Assert.Equal(2, response.ExcludedResultEntryCount);
        Assert.NotNull(response.ExcludedResultEntryTestOrders);
        Assert.Equal(2, response.ExcludedResultEntryTestOrders.Count);
        Assert.All(response.ExcludedResultEntryTestOrders, e =>
        {
            Assert.Equal("Selective Plating", e.StepName);
            Assert.Contains("microbiological observation", e.Reason, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public async Task Scenario3_TwoSamples_AtTamcIncubationComplete_ColonyCount_ExcludedResultEntry()
    {
        await using var db = NewDb();
        var (tamcDef, tsaMat, tsaLot, inc) = await SeedTamcSetupAsync(db);

        var s1 = new Sample { ReferenceNumber = "OSTEO-01", Category = SampleCategory.FinishedProduct };
        var s2 = new Sample { ReferenceNumber = "OSTEO-02", Category = SampleCategory.FinishedProduct };
        db.Samples.AddRange(s1, s2);
        await db.SaveChangesAsync();

        var o1 = new TestOrder { SampleId = s1.Id, TestCode = "TAMC", Status = ApprovalStatus.Pending, CurrentStep = WorkflowStep.Incubating, AssignedAnalystId = 1 };
        var o2 = new TestOrder { SampleId = s2.Id, TestCode = "TAMC", Status = ApprovalStatus.Pending, CurrentStep = WorkflowStep.Incubating, AssignedAnalystId = 1 };
        db.TestOrders.AddRange(o1, o2);
        await db.SaveChangesAsync();

        // TAMC incubation started 50 hours ago (min is 48h)
        var startUtc = DateTime.UtcNow.AddHours(-50);
        var endUtc = DateTime.UtcNow.AddHours(22);
        db.Incubations.AddRange(
            new Incubation { TestOrderId = o1.Id, StepNumber = 1, StepName = "TAMC", MediaId = tsaLot.Id, IncubatorEquipmentId = inc.Id, StartedAt = startUtc, IncubationStartUtc = startUtc, IncubationEndUtc = endUtc, StartedByUserId = 1 },
            new Incubation { TestOrderId = o2.Id, StepNumber = 1, StepName = "TAMC", MediaId = tsaLot.Id, IncubatorEquipmentId = inc.Id, StartedAt = startUtc, IncubationStartUtc = startUtc, IncubationEndUtc = endUtc, StartedByUserId = 1 }
        );
        await db.SaveChangesAsync();

        var service = TestServiceFactory.GroupedTestAction(db);
        var response = await service.GetActionableGroupsAsync(1, RoleType.Analyst, scope: "mine");

        Assert.Empty(response.Groups);
        Assert.Equal(2, response.ExcludedResultEntryCount);
        Assert.NotNull(response.ExcludedResultEntryTestOrders);
        Assert.All(response.ExcludedResultEntryTestOrders, e =>
        {
            Assert.Equal("TAMC", e.StepName);
            Assert.Contains("colony count", e.Reason, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public async Task Scenario4_TwoSamples_AtTymcIncubationComplete_ColonyCount_ExcludedResultEntry()
    {
        await using var db = NewDb();

        var inc = new Equipment { Name = "INC-TYMC", Code = "INC-TYMC", Type = EquipmentType.Incubator, SetPointTemperature = 22.5m };
        db.Equipment.Add(inc);

        var sdaMaterial = new Material { MaterialName = "Sabouraud Dextrose Agar", Code = "SDA" };
        db.Materials.Add(sdaMaterial);
        await db.SaveChangesAsync();

        var sdaLot = new Media { MaterialId = sdaMaterial.Id, LotNumber = "SDA/2026/01", ExpiryDate = DateTime.UtcNow.AddMonths(6), IsReleasedForUse = true, Status = MediaStatus.Prepared, ApprovalStatus = ApprovalGateStatus.Approved };
        db.Media.Add(sdaLot);

        var tymcDef = new TestDefinition { Code = "TYMC", DisplayName = "Total Yeast and Mold Count", WorkflowType = WorkflowType.CountTest };
        db.TestDefinitions.Add(tymcDef);
        await db.SaveChangesAsync();

        var tymcStep = new TestWorkflowStep { TestDefinitionId = tymcDef.Id, StepOrder = 1, StepName = "TYMC", StepType = StepType.PlateCount, TemperatureMin = 20.0m, TemperatureMax = 25.0m, IncubationMinHours = 120, IncubationMaxHours = 168, IsFinalStep = true };
        db.TestWorkflowSteps.Add(tymcStep);
        await db.SaveChangesAsync();

        db.TestWorkflowStepMedias.Add(new TestWorkflowStepMedia { TestWorkflowStepId = tymcStep.Id, MaterialId = sdaMaterial.Id, TempMin = 20.0m, TempMax = 25.0m, IncubationMinHours = 120, IncubationMaxHours = 168, IsRequired = true, DisplayOrder = 1 });
        await db.SaveChangesAsync();

        var s1 = new Sample { ReferenceNumber = "OSTEO-01", Category = SampleCategory.FinishedProduct };
        var s2 = new Sample { ReferenceNumber = "OSTEO-02", Category = SampleCategory.FinishedProduct };
        db.Samples.AddRange(s1, s2);
        await db.SaveChangesAsync();

        var o1 = new TestOrder { SampleId = s1.Id, TestCode = "TYMC", Status = ApprovalStatus.Pending, CurrentStep = WorkflowStep.Incubating, AssignedAnalystId = 1 };
        var o2 = new TestOrder { SampleId = s2.Id, TestCode = "TYMC", Status = ApprovalStatus.Pending, CurrentStep = WorkflowStep.Incubating, AssignedAnalystId = 1 };
        db.TestOrders.AddRange(o1, o2);
        await db.SaveChangesAsync();

        // Incubation started 125 hours ago (min is 120h)
        var startUtc = DateTime.UtcNow.AddHours(-125);
        var endUtc = DateTime.UtcNow.AddHours(30);
        db.Incubations.AddRange(
            new Incubation { TestOrderId = o1.Id, StepNumber = 1, StepName = "TYMC", MediaId = sdaLot.Id, IncubatorEquipmentId = inc.Id, StartedAt = startUtc, IncubationStartUtc = startUtc, IncubationEndUtc = endUtc, StartedByUserId = 1 },
            new Incubation { TestOrderId = o2.Id, StepNumber = 1, StepName = "TYMC", MediaId = sdaLot.Id, IncubatorEquipmentId = inc.Id, StartedAt = startUtc, IncubationStartUtc = startUtc, IncubationEndUtc = endUtc, StartedByUserId = 1 }
        );
        await db.SaveChangesAsync();

        var service = TestServiceFactory.GroupedTestAction(db);
        var response = await service.GetActionableGroupsAsync(1, RoleType.Analyst, scope: "mine");

        Assert.Empty(response.Groups);
        Assert.Equal(2, response.ExcludedResultEntryCount);
        Assert.NotNull(response.ExcludedResultEntryTestOrders);
        Assert.All(response.ExcludedResultEntryTestOrders, e =>
        {
            Assert.Equal("TYMC", e.StepName);
            Assert.Contains("colony count", e.Reason, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public async Task Scenario5_MixedSelection_TsbCompleteAndSelectivePlateReading_PartitionsCorrectly()
    {
        await using var db = NewDb();
        var (def, media, inc36, inc42) = await SeedPathogenMasterDataAsync(db);

        // 3 samples at TSB minimum incubation complete
        var (s1, o1) = await AddPathogenSampleOrderAsync(db, "OSTEO-01");
        var (s2, o2) = await AddPathogenSampleOrderAsync(db, "OSTEO-02");
        var (s3, o3) = await AddPathogenSampleOrderAsync(db, "OSTEO-03");

        var tsbStart = DateTime.UtcNow.AddHours(-20);
        var tsbEnd = DateTime.UtcNow.AddHours(4);
        db.Incubations.AddRange(
            new Incubation { TestOrderId = o1.Id, StepNumber = 1, StepName = "Broth Enrichment", MediaId = media.BrothLotId, IncubatorEquipmentId = inc36.Id, StartedAt = tsbStart, IncubationStartUtc = tsbStart, IncubationEndUtc = tsbEnd, StartedByUserId = 1 },
            new Incubation { TestOrderId = o2.Id, StepNumber = 1, StepName = "Broth Enrichment", MediaId = media.BrothLotId, IncubatorEquipmentId = inc36.Id, StartedAt = tsbStart, IncubationStartUtc = tsbStart, IncubationEndUtc = tsbEnd, StartedByUserId = 1 },
            new Incubation { TestOrderId = o3.Id, StepNumber = 1, StepName = "Broth Enrichment", MediaId = media.BrothLotId, IncubatorEquipmentId = inc36.Id, StartedAt = tsbStart, IncubationStartUtc = tsbStart, IncubationEndUtc = tsbEnd, StartedByUserId = 1 }
        );
        o1.CurrentStep = WorkflowStep.Incubating;
        o2.CurrentStep = WorkflowStep.Incubating;
        o3.CurrentStep = WorkflowStep.Incubating;

        // 2 samples at Selective Plating incubation complete (awaiting final reading)
        var (s4, o4) = await AddPathogenSampleOrderAsync(db, "OSTEO-04");
        var (s5, o5) = await AddPathogenSampleOrderAsync(db, "OSTEO-05");

        var brothEnd = DateTime.UtcNow.AddHours(-40);
        db.WorkflowStepResults.AddRange(
            new WorkflowStepResult { TestOrderId = o4.Id, StepName = "Broth Enrichment", StepType = StepType.BrothEnrichment, SubmittedAtUtc = brothEnd, SubmittedByUserId = 1 },
            new WorkflowStepResult { TestOrderId = o4.Id, StepName = "Selective Broth", StepType = StepType.SelectiveBroth, SubmittedAtUtc = brothEnd, SubmittedByUserId = 1 },
            new WorkflowStepResult { TestOrderId = o5.Id, StepName = "Broth Enrichment", StepType = StepType.BrothEnrichment, SubmittedAtUtc = brothEnd, SubmittedByUserId = 1 },
            new WorkflowStepResult { TestOrderId = o5.Id, StepName = "Selective Broth", StepType = StepType.SelectiveBroth, SubmittedAtUtc = brothEnd, SubmittedByUserId = 1 }
        );

        var plateStart = DateTime.UtcNow.AddHours(-25);
        var plateEnd = DateTime.UtcNow.AddHours(-1);
        db.Incubations.AddRange(
            new Incubation { TestOrderId = o4.Id, StepNumber = 3, StepName = "Selective Plating", MediaId = media.SelectivePlatingLotId, IncubatorEquipmentId = inc36.Id, StartedAt = plateStart, IncubationStartUtc = plateStart, IncubationEndUtc = plateEnd, StartedByUserId = 1 },
            new Incubation { TestOrderId = o5.Id, StepNumber = 3, StepName = "Selective Plating", MediaId = media.SelectivePlatingLotId, IncubatorEquipmentId = inc36.Id, StartedAt = plateStart, IncubationStartUtc = plateStart, IncubationEndUtc = plateEnd, StartedByUserId = 1 }
        );
        o4.CurrentStep = WorkflowStep.Incubating;
        o5.CurrentStep = WorkflowStep.Incubating;
        await db.SaveChangesAsync();

        var service = TestServiceFactory.GroupedTestAction(db);
        var sampleIds = new List<int> { s1.Id, s2.Id, s3.Id, s4.Id, s5.Id };
        var response = await service.GetActionableGroupsAsync(1, RoleType.Analyst, scope: "mine", sampleIds: sampleIds);

        // Exactly 1 group for the 3 TSB transferable samples
        Assert.Single(response.Groups);
        var group = response.Groups[0];
        Assert.Equal("TRANSFER_SELECTIVE", group.TransitionType);
        Assert.Equal(3, group.SampleCount);
        Assert.Equal(3, group.TestOrderCount);
        Assert.Contains(group.TestOrders, t => t.TestOrderId == o1.Id);
        Assert.Contains(group.TestOrders, t => t.TestOrderId == o2.Id);
        Assert.Contains(group.TestOrders, t => t.TestOrderId == o3.Id);

        // Exactly 2 excluded tests for the selective plate readout
        Assert.Equal(2, response.ExcludedResultEntryCount);
        Assert.NotNull(response.ExcludedResultEntryTestOrders);
        Assert.Equal(2, response.ExcludedResultEntryTestOrders.Count);
        Assert.Contains(response.ExcludedResultEntryTestOrders, e => e.TestOrderId == o4.Id);
        Assert.Contains(response.ExcludedResultEntryTestOrders, e => e.TestOrderId == o5.Id);
    }

    [Fact]
    public async Task Scenario6_TwoTests_AtStage1Complete_TransferToStage2Incubator_GroupedActionAvailable()
    {
        await using var db = NewDb();

        var inc1 = new Equipment { Name = "INC-S1", Code = "INC-S1", Type = EquipmentType.Incubator, SetPointTemperature = 22.5m };
        var inc2 = new Equipment { Name = "INC-S2", Code = "INC-S2", Type = EquipmentType.Incubator, SetPointTemperature = 32.5m };
        db.Equipment.AddRange(inc1, inc2);

        var mat = new Material { MaterialName = "TwoStage Agar", Code = "TSA2" };
        db.Materials.Add(mat);
        await db.SaveChangesAsync();

        var lot = new Media { MaterialId = mat.Id, LotNumber = "TSA2/01", ExpiryDate = DateTime.UtcNow.AddMonths(6), IsReleasedForUse = true, Status = MediaStatus.Prepared, ApprovalStatus = ApprovalGateStatus.Approved };
        db.Media.Add(lot);

        var def = new TestDefinition { Code = "TEST_2STAGE", DisplayName = "Two Stage Count", WorkflowType = WorkflowType.CountTest };
        db.TestDefinitions.Add(def);
        await db.SaveChangesAsync();

        var step = new TestWorkflowStep
        {
            TestDefinitionId = def.Id,
            StepOrder = 1,
            StepName = "Plate Incubation",
            StepType = StepType.PlateCount,
            RequiresIncubationTransfer = true,
            TemperatureMin = 20.0m,
            TemperatureMax = 25.0m,
            IncubationMinHours = 48,
            IncubationMaxHours = 72,
            IsFinalStep = true
        };
        db.TestWorkflowSteps.Add(step);
        await db.SaveChangesAsync();

        db.TestWorkflowStepMedias.Add(new TestWorkflowStepMedia
        {
            TestWorkflowStepId = step.Id,
            MaterialId = mat.Id,
            TempMin = 20.0m,
            TempMax = 25.0m,
            IncubationMinHours = 48,
            IncubationMaxHours = 72,
            IsRequired = true,
            DisplayOrder = 1
        });

        db.TestWorkflowStepIncubationStages.Add(new TestWorkflowStepIncubationStage
        {
            TestWorkflowStepId = step.Id,
            StageNumber = 2,
            TempMin = 30.0m,
            TempMax = 35.0m,
            IncubationMinHours = 48,
            IncubationMaxHours = 72
        });
        await db.SaveChangesAsync();

        var s1 = new Sample { ReferenceNumber = "S1", Category = SampleCategory.FinishedProduct };
        var s2 = new Sample { ReferenceNumber = "S2", Category = SampleCategory.FinishedProduct };
        db.Samples.AddRange(s1, s2);
        await db.SaveChangesAsync();

        var o1 = new TestOrder { SampleId = s1.Id, TestCode = "TEST_2STAGE", Status = ApprovalStatus.Pending, CurrentStep = WorkflowStep.Incubating, AssignedAnalystId = 1 };
        var o2 = new TestOrder { SampleId = s2.Id, TestCode = "TEST_2STAGE", Status = ApprovalStatus.Pending, CurrentStep = WorkflowStep.Incubating, AssignedAnalystId = 1 };
        db.TestOrders.AddRange(o1, o2);
        await db.SaveChangesAsync();

        // Stage 1 started 50 hours ago (min is 48h)
        var s1Start = DateTime.UtcNow.AddHours(-50);
        var s1End = DateTime.UtcNow.AddHours(22);
        db.Incubations.AddRange(
            new Incubation { TestOrderId = o1.Id, StepNumber = 1, StepName = "Plate Incubation", StageNumber = 1, MediaId = lot.Id, IncubatorEquipmentId = inc1.Id, StartedAt = s1Start, IncubationStartUtc = s1Start, IncubationEndUtc = s1End, StartedByUserId = 1 },
            new Incubation { TestOrderId = o2.Id, StepNumber = 1, StepName = "Plate Incubation", StageNumber = 1, MediaId = lot.Id, IncubatorEquipmentId = inc1.Id, StartedAt = s1Start, IncubationStartUtc = s1Start, IncubationEndUtc = s1End, StartedByUserId = 1 }
        );
        await db.SaveChangesAsync();

        var service = TestServiceFactory.GroupedTestAction(db);
        var response = await service.GetActionableGroupsAsync(1, RoleType.Analyst, scope: "mine");

        // Grouped action available for TRANSFER_INCUBATOR
        Assert.Single(response.Groups);
        var group = response.Groups[0];
        Assert.Equal("TRANSFER_INCUBATOR", group.TransitionType);
        Assert.Equal("Plate Incubation", group.StepName);
        Assert.Equal(2, group.SampleCount);
        Assert.Equal(2, group.TestOrderCount);
        Assert.Equal(30.0m, group.TempMin);
        Assert.Equal(35.0m, group.TempMax);
    }

    [Fact]
    public async Task Scenario7_MixedPathogenWorkflows_WithDifferentNextMedia_NotGroupedTogether()
    {
        await using var db = NewDb();
        var (salmDef, salmMedia, inc36, inc42) = await SeedPathogenMasterDataAsync(db);

        // Add second pathogen definition: E. coli with MacConkey Broth
        var ecoliOrganism = new Organism { ScientificName = "Escherichia coli" };
        db.Organisms.Add(ecoliOrganism);

        var ecoliDef = new TestDefinition { Code = "PATHOGEN_ECOLI", DisplayName = "E. coli", WorkflowType = WorkflowType.Observation };
        db.TestDefinitions.Add(ecoliDef);
        await db.SaveChangesAsync();

        var ecoliStep1 = new TestWorkflowStep { TestDefinitionId = ecoliDef.Id, StepOrder = 1, StepName = "Broth Enrichment", IncubationMinHours = 18, IncubationMaxHours = 24, TemperatureMin = 35, TemperatureMax = 37, StepType = StepType.BrothEnrichment };
        var ecoliStep2 = new TestWorkflowStep { TestDefinitionId = ecoliDef.Id, StepOrder = 2, StepName = "MacConkey Broth Purple", IncubationMinHours = 24, IncubationMaxHours = 48, TemperatureMin = 41, TemperatureMax = 43, StepType = StepType.SelectiveBroth };
        db.TestWorkflowSteps.AddRange(ecoliStep1, ecoliStep2);
        await db.SaveChangesAsync();

        var (macMaterial, macLot) = await AddMaterialAndMediaAsync(db, "MacConkey Broth Purple", "MAC/1/26");
        db.TestWorkflowStepMedias.AddRange(
            new TestWorkflowStepMedia { TestWorkflowStepId = ecoliStep1.Id, MaterialId = salmMedia.BrothMaterialId, TempMin = 35, TempMax = 37, IncubationMinHours = 18, IncubationMaxHours = 24, IsRequired = true, DisplayOrder = 1 },
            new TestWorkflowStepMedia { TestWorkflowStepId = ecoliStep2.Id, MaterialId = macMaterial.Id, TempMin = 41, TempMax = 43, IncubationMinHours = 24, IncubationMaxHours = 48, IsRequired = true, DisplayOrder = 1 }
        );
        await db.SaveChangesAsync();

        // Sample 1: Salmonella
        var (s1, o1) = await AddPathogenSampleOrderAsync(db, "SAMPLE-01", "PATHOGEN_SALMONELLA");
        // Sample 2: E. coli
        var (s2, o2) = await AddPathogenSampleOrderAsync(db, "SAMPLE-02", "PATHOGEN_ECOLI");

        // Both have TSB completed
        var startUtc = DateTime.UtcNow.AddHours(-20);
        var endUtc = DateTime.UtcNow.AddHours(4);
        db.Incubations.AddRange(
            new Incubation { TestOrderId = o1.Id, StepNumber = 1, StepName = "Broth Enrichment", MediaId = salmMedia.BrothLotId, IncubatorEquipmentId = inc36.Id, StartedAt = startUtc, IncubationStartUtc = startUtc, IncubationEndUtc = endUtc, StartedByUserId = 1 },
            new Incubation { TestOrderId = o2.Id, StepNumber = 1, StepName = "Broth Enrichment", MediaId = salmMedia.BrothLotId, IncubatorEquipmentId = inc36.Id, StartedAt = startUtc, IncubationStartUtc = startUtc, IncubationEndUtc = endUtc, StartedByUserId = 1 }
        );
        o1.CurrentStep = WorkflowStep.Incubating;
        o2.CurrentStep = WorkflowStep.Incubating;
        await db.SaveChangesAsync();

        var service = TestServiceFactory.GroupedTestAction(db);
        var response = await service.GetActionableGroupsAsync(1, RoleType.Analyst, scope: "mine");

        // They must NOT be merged into a single group because next step media is completely different (RVS vs MacConkey)!
        Assert.Equal(2, response.Groups.Count);
        Assert.Contains(response.Groups, g => g.StepName == "Selective Broth" && g.PermittedMaterialIds.Contains(salmMedia.SelectiveBrothMaterialId));
        Assert.Contains(response.Groups, g => g.StepName == "MacConkey Broth Purple" && g.PermittedMaterialIds.Contains(macMaterial.Id));
    }

    [Fact]
    public async Task Scenario8_ExecuteGroupedWorkflowAction_CreatesIndependentIncubationAndWorkflowHistoryRecords()
    {
        await using var db = NewDb();
        var (def, media, inc36, inc42) = await SeedPathogenMasterDataAsync(db);

        var (s1, o1) = await AddPathogenSampleOrderAsync(db, "OSTEO-01");
        var (s2, o2) = await AddPathogenSampleOrderAsync(db, "OSTEO-02");

        var startUtc = DateTime.UtcNow.AddHours(-20);
        var endUtc = DateTime.UtcNow.AddHours(4);
        db.Incubations.AddRange(
            new Incubation { TestOrderId = o1.Id, StepNumber = 1, StepName = "Broth Enrichment", MediaId = media.BrothLotId, IncubatorEquipmentId = inc36.Id, StartedAt = startUtc, IncubationStartUtc = startUtc, IncubationEndUtc = endUtc, StartedByUserId = 1 },
            new Incubation { TestOrderId = o2.Id, StepNumber = 1, StepName = "Broth Enrichment", MediaId = media.BrothLotId, IncubatorEquipmentId = inc36.Id, StartedAt = startUtc, IncubationStartUtc = startUtc, IncubationEndUtc = endUtc, StartedByUserId = 1 }
        );
        o1.CurrentStep = WorkflowStep.Incubating;
        o2.CurrentStep = WorkflowStep.Incubating;
        await db.SaveChangesAsync();

        var service = TestServiceFactory.GroupedTestAction(db);
        var request = new BatchSelectMediaRequest(
            TestOrderIds: new List<int> { o1.Id, o2.Id },
            StepName: "Selective Broth",
            MediaLotId: media.SelectiveBrothLotId,
            IncubatorEquipmentId: inc42.Id,
            TransitionType: "TRANSFER_SELECTIVE",
            TargetStepName: "Selective Broth",
            PredecessorStepName: "Broth Enrichment"
        );

        var result = await service.ExecuteBatchSelectMediaAsync(request, 1, RoleType.Analyst);

        Assert.Equal(2, result.TotalRequested);
        Assert.Equal(2, result.SucceededCount);
        Assert.Empty(result.Skipped);

        // Predecessor broth completed for both
        var predInc1 = await db.Incubations.FirstAsync(i => i.TestOrderId == o1.Id && i.StepName == "Broth Enrichment");
        var predInc2 = await db.Incubations.FirstAsync(i => i.TestOrderId == o2.Id && i.StepName == "Broth Enrichment");
        Assert.NotNull(predInc1.CompletedAt);
        Assert.NotNull(predInc2.CompletedAt);

        // Independent new Incubation records for Selective Broth
        var selIncs = await db.Incubations.Where(i => i.StepName == "Selective Broth").ToListAsync();
        Assert.Equal(2, selIncs.Count);
        Assert.NotEqual(selIncs[0].Id, selIncs[1].Id);
        Assert.Contains(selIncs, i => i.TestOrderId == o1.Id);
        Assert.Contains(selIncs, i => i.TestOrderId == o2.Id);
        Assert.All(selIncs, i =>
        {
            Assert.Equal(media.SelectiveBrothLotId, i.MediaId);
            Assert.Equal(inc42.Id, i.IncubatorEquipmentId);
            Assert.Null(i.CompletedAt);
        });

        // Independent WorkflowHistory records
        var histories = await db.WorkflowHistories.Where(h => h.TestOrderId == o1.Id || h.TestOrderId == o2.Id).ToListAsync();
        Assert.True(histories.Count >= 2);
        var o1Histories = histories.Where(h => h.TestOrderId == o1.Id).ToList();
        var o2Histories = histories.Where(h => h.TestOrderId == o2.Id).ToList();
        Assert.NotEmpty(o1Histories);
        Assert.NotEmpty(o2Histories);
        Assert.NotEqual(o1Histories.Last().Id, o2Histories.Last().Id);
    }

    [Fact]
    public async Task Scenario9_ExecuteBatchSelectMedia_RejectsResultEntryStep_ReportsSkipped()
    {
        await using var db = NewDb();
        var (tamcDef, tsaMat, tsaLot, inc) = await SeedTamcSetupAsync(db);

        var s1 = new Sample { ReferenceNumber = "OSTEO-01", Category = SampleCategory.FinishedProduct };
        db.Samples.Add(s1);
        await db.SaveChangesAsync();

        var o1 = new TestOrder { SampleId = s1.Id, TestCode = "TAMC", Status = ApprovalStatus.Pending, CurrentStep = WorkflowStep.Incubating, AssignedAnalystId = 1 };
        db.TestOrders.Add(o1);
        await db.SaveChangesAsync();

        // TAMC incubation complete awaiting colony count
        var startUtc = DateTime.UtcNow.AddHours(-50);
        var endUtc = DateTime.UtcNow.AddHours(22);
        db.Incubations.Add(new Incubation { TestOrderId = o1.Id, StepNumber = 1, StepName = "TAMC", MediaId = tsaLot.Id, IncubatorEquipmentId = inc.Id, StartedAt = startUtc, IncubationStartUtc = startUtc, IncubationEndUtc = endUtc, StartedByUserId = 1 });
        await db.SaveChangesAsync();

        var service = TestServiceFactory.GroupedTestAction(db);
        var request = new BatchSelectMediaRequest(
            TestOrderIds: new List<int> { o1.Id },
            StepName: "TAMC",
            MediaLotId: tsaLot.Id,
            IncubatorEquipmentId: inc.Id
        );

        var result = await service.ExecuteBatchSelectMediaAsync(request, 1, RoleType.Analyst);

        Assert.Equal(1, result.TotalRequested);
        Assert.Equal(0, result.SucceededCount);
        Assert.Equal(1, result.SkippedCount);
        Assert.Contains("colony count", result.Skipped[0].Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Scenario10_ExistingIndividualWorkflowExecution_WorksWithoutRegression()
    {
        await using var db = NewDb();
        var (tamcDef, tsaMat, tsaLot, incubator) = await SeedTamcSetupAsync(db);

        var sample = new Sample { ReferenceNumber = "IND-01", Category = SampleCategory.FinishedProduct };
        db.Samples.Add(sample);
        await db.SaveChangesAsync();

        var order = new TestOrder { SampleId = sample.Id, TestCode = "TAMC", Status = ApprovalStatus.Pending, CurrentStep = WorkflowStep.Waiting, AssignedAnalystId = 1 };
        db.TestOrders.Add(order);
        await db.SaveChangesAsync();

        var engine = TestServiceFactory.TestWorkflow(db);

        // 1. Start incubation individually via SelectMediaAsync
        var inc = await engine.SelectMediaAsync(order.Id, "TAMC", tsaLot.Id, incubator.Id, userId: 1);
        Assert.NotNull(inc);
        Assert.Equal(WorkflowStep.Incubating, (await db.TestOrders.FindAsync(order.Id))!.CurrentStep);

        // Fast-forward incubation window past minimum duration
        inc.StartedAt = DateTime.UtcNow.AddHours(-50);
        inc.IncubationStartUtc = DateTime.UtcNow.AddHours(-50);
        inc.IncubationEndUtc = DateTime.UtcNow.AddHours(-1);
        await db.SaveChangesAsync();

        // 2. Record analytical count result individually
        var countResult = await engine.RecordResultAsync(order.Id, "TAMC", new CountTestPayload(new List<string> { "25", "30" }, 1), userId: 1);
        Assert.NotNull(countResult);
        Assert.True(countResult.AllStepsComplete);

        var reloadedOrder = await db.TestOrders.FindAsync(order.Id);
        Assert.Equal(WorkflowStep.Ready, reloadedOrder!.CurrentStep);
    }

    #endregion
}
