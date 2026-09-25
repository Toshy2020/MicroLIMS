using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class DisintegrationWorkflowEngineTests
{
    private static MicroLimsDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MicroLimsDbContext(options);
    }

    private static (TestDefinition testDef, Equipment equip, Item item, Specification spec, Sample sample, TestOrder order, User analyst, User head)
        SetupDisintegrationScenario(MicroLimsDbContext db)
    {
        var microSec = TestServiceFactory.EnsureMicroSection(db);
        var fpSec = db.DocumentSections.FirstOrDefault(s => s.Code == "FP");
        if (fpSec == null)
        {
            fpSec = new DocumentSection
            {
                Name = "Finished Product Laboratory",
                Code = "FP",
                DepartmentId = microSec.DepartmentId,
                IsActive = true
            };
            db.DocumentSections.Add(fpSec);
            db.SaveChanges();
        }

        var analystRole = db.Roles.FirstOrDefault(r => r.Type == RoleType.Analyst)
            ?? db.Roles.Add(new Role { Name = "Analyst", Type = RoleType.Analyst, IsActive = true }).Entity;
        var headRole = db.Roles.FirstOrDefault(r => r.Type == RoleType.SectionHead)
            ?? db.Roles.Add(new Role { Name = "Section Head", Type = RoleType.SectionHead, IsActive = true }).Entity;
        db.SaveChanges();

        var analyst = new User
        {
            Username = "analyst_" + Guid.NewGuid().ToString("N")[..6],
            FullName = "FP Analyst",
            RoleId = analystRole.Id,
            Role = analystRole,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123!"),
            IsActive = true
        };
        var head = new User
        {
            Username = "head_" + Guid.NewGuid().ToString("N")[..6],
            FullName = "FP Head",
            RoleId = headRole.Id,
            Role = headRole,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123!"),
            IsActive = true
        };
        db.Users.AddRange(analyst, head);
        db.SaveChanges();

        db.UserOrgMemberships.AddRange(
            new UserOrgMembership { UserId = analyst.Id, DepartmentId = fpSec.DepartmentId, SectionId = fpSec.Id },
            new UserOrgMembership { UserId = head.Id, DepartmentId = fpSec.DepartmentId, SectionId = fpSec.Id });
        db.SaveChanges();

        var testDef = new TestDefinition
        {
            Code = "DISINT-VIT",
            DisplayName = "Multivitamin Disintegration",
            SectionId = fpSec.Id,
            WorkflowType = WorkflowType.Disintegration,
            EquationType = EquationType.Disintegration,
            RequiresSystemSuitability = false,
            DisintegrationStage1Units = 6,
            DisintegrationStage2Units = 12,
            DisintegrationMaxStage1Failures = 2,
            DisintegrationMinPassTotal = 16,
            ConditionFields = "Medium, Temperature",
            IsActive = true
        };
        db.TestDefinitions.Add(testDef);

        var equip = new Equipment
        {
            Name = "Disintegration Tester 1",
            Code = "EQ-DT-01",
            Type = EquipmentType.DisintegrationTester,
            SectionId = fpSec.Id
        };
        db.Equipment.Add(equip);

        var item = new Item
        {
            Code = "VIT-TAB",
            Name = "Multivitamin Tablets",
            Category = SampleCategory.FinishedProduct,
            IsActive = true
        };
        db.Items.Add(item);
        db.SaveChanges();

        var spec = new Specification
        {
            ItemId = item.Id,
            TestCode = testDef.Code,
            ParameterName = "Disintegration Time",
            LimitType = LimitType.DisintegrationTime,
            UpperLimit = 30m,
            Unit = "min",
            SpecLimit = "NMT 30 min",
            ConversionFactor = 1.0m
        };
        db.Specifications.Add(spec);

        var cause = new CauseOfTesting { Name = "Routine Release", IsActive = true };
        db.CausesOfTesting.Add(cause);

        var sample = new Sample
        {
            ReferenceNumber = "SMP-DT-001",
            Category = SampleCategory.FinishedProduct,
            ItemId = item.Id,
            ReceivedAt = DateTime.UtcNow,
            CauseOfTesting = cause,
            ReceivedByUserId = analyst.Id,
            Status = SampleStatus.InTesting
        };
        db.Samples.Add(sample);
        db.SaveChanges();

        var order = new TestOrder
        {
            SampleId = sample.Id,
            TestCode = testDef.Code,
            SectionId = fpSec.Id,
            CurrentStep = WorkflowStep.Running,
            Status = ApprovalStatus.InProgress
        };
        db.TestOrders.Add(order);
        db.SaveChanges();

        return (testDef, equip, item, spec, sample, order, analyst, head);
    }

    [Fact]
    public async Task Stage1_NextStageRequired_OrderNotFinalized_ApprovalBlocked_Stage2_Finalized()
    {
        using var db = NewDb();
        var (_, equip, _, _, _, order, analyst, _) = SetupDisintegrationScenario(db);
        var engine = TestServiceFactory.TestWorkflow(db);

        // Stage 1: 5 pass (<= 30), 1 fail (32 > 30) -> 1 failure -> NextStageRequired
        var payload1 = new DisintegrationPayload(
            AnalysedAt: DateTime.UtcNow,
            EquipmentId: equip.Id,
            Conditions: new Dictionary<string, string>
            {
                ["Medium"] = "Purified Water",
                ["Temperature"] = "37 ± 2 °C"
            },
            UnitMinutes: new List<decimal?> { 12m, 14m, 15m, 13m, 16m, 32m },
            Password: "Password123!",
            Comment: "Stage 1 run");

        var result1 = await engine.RecordDisintegrationResultAsync(order.Id, payload1, analyst.Id);

        Assert.False(result1.AllStepsComplete);
        Assert.False(result1.IsDefinitive);
        Assert.Equal("NextStageRequired", result1.Status);

        // Order remains Running
        var reloadedOrder = await db.TestOrders.FindAsync(order.Id);
        Assert.NotNull(reloadedOrder);
        Assert.Equal(WorkflowStep.Running, reloadedOrder.CurrentStep);

        // StepDetails shows HasActiveAnalysis = false because of pending stage
        var stepDetails = await engine.GetCurrentStepDetailsAsync(order.Id);
        Assert.False(stepDetails.Result.AllStepsComplete);
        Assert.False(stepDetails.Facts.HasActiveAnalysis);

        // Stage 2: 12 more units (all <= 30 min, e.g. 15 min each).
        // Total = 17 pass, 1 fail -> 17 >= 16 -> Complies!
        var payload2 = new DisintegrationStagePayload(
            UnitMinutes: new List<decimal?> { 15m, 16m, 14m, 18m, 20m, 22m, 19m, 17m, 16m, 15m, 14m, 18m },
            Password: "Password123!",
            Comment: "Stage 2 run");

        var result2 = await engine.RecordDisintegrationStageAsync(order.Id, payload2, analyst.Id);

        Assert.True(result2.AllStepsComplete);
        Assert.True(result2.IsDefinitive);
        Assert.Equal("WithinLimits", result2.Status);

        // Order finalized to Ready
        reloadedOrder = await db.TestOrders.FindAsync(order.Id);
        Assert.NotNull(reloadedOrder);
        Assert.Equal(WorkflowStep.Ready, reloadedOrder.CurrentStep);

        // StepDetails now shows complete
        stepDetails = await engine.GetCurrentStepDetailsAsync(order.Id);
        Assert.True(stepDetails.Result.AllStepsComplete);
        Assert.True(stepDetails.Facts.HasActiveAnalysis);

        // ParameterResult has 18 readings, stage 2, longest time is 32m
        var analysis = await db.TestAnalyses
            .Include(a => a.ParameterResults)
                .ThenInclude(pr => pr.Readings)
            .FirstOrDefaultAsync(a => a.TestOrderId == order.Id);

        Assert.NotNull(analysis);
        Assert.Single(analysis.ParameterResults);
        var pr = analysis.ParameterResults[0];
        Assert.Equal(2, pr.StageReached);
        Assert.Equal("WithinLimits", pr.ComparisonStatus);
        Assert.Equal("Complies", pr.ReportedDisplay);
        Assert.Equal(32m, pr.ReportedValue);
        Assert.Equal(18, pr.Readings.Count);

        // Check readings
        var reading6 = pr.Readings.First(r => r.Index == 6);
        Assert.Equal(1, reading6.Stage);
        Assert.Equal(32m, reading6.Value1);
        Assert.False(reading6.Passed);

        var reading7 = pr.Readings.First(r => r.Index == 7);
        Assert.Equal(2, reading7.Stage);
        Assert.Equal(15m, reading7.Value1);
        Assert.True(reading7.Passed);
    }

    [Fact]
    public async Task Stage2_UsesStage1Snapshot_WhenTestMasterAndSpecEditedBetweenStages()
    {
        using var db = NewDb();
        var (testDef, equip, _, spec, _, order, analyst, _) = SetupDisintegrationScenario(db);
        var engine = TestServiceFactory.TestWorkflow(db);

        await engine.RecordDisintegrationResultAsync(order.Id, new DisintegrationPayload(
            DateTime.UtcNow, equip.Id,
            new Dictionary<string, string> { ["Medium"] = "Purified Water", ["Temperature"] = "37 °C" },
            new List<decimal?> { 12m, 14m, 15m, 13m, 16m, 32m }, "Password123!"), analyst.Id);

        // Edits after stage 1 that would change the verdict if they were applied:
        // 18 of 18 required, 10 more units, and a 60 min limit.
        testDef.DisintegrationMinPassTotal = 18;
        testDef.DisintegrationStage2Units = 10;
        spec.UpperLimit = 60m;
        await db.SaveChangesAsync();

        var result = await engine.RecordDisintegrationStageAsync(order.Id, new DisintegrationStagePayload(
            new List<decimal?> { 15m, 16m, 14m, 18m, 20m, 22m, 19m, 17m, 16m, 15m, 14m, 18m }, "Password123!"), analyst.Id);

        Assert.Equal("WithinLimits", result.Status);
        var pr = await db.ParameterResults.Include(p => p.Readings).SingleAsync(p => p.TestOrderId == order.Id);
        Assert.Equal(18, pr.Readings.Count);
        Assert.False(pr.Readings.Single(r => r.Index == 6).Passed);
    }

    [Fact]
    public async Task Stage1_AllPass_CompliesAndFinalizedImmediately()
    {
        using var db = NewDb();
        var (_, equip, _, _, _, order, analyst, _) = SetupDisintegrationScenario(db);
        var engine = TestServiceFactory.TestWorkflow(db);

        var payload = new DisintegrationPayload(
            AnalysedAt: DateTime.UtcNow,
            EquipmentId: equip.Id,
            Conditions: new Dictionary<string, string>
            {
                ["Medium"] = "Purified Water",
                ["Temperature"] = "37 ± 2 °C"
            },
            UnitMinutes: new List<decimal?> { 12m, 14m, 15m, 13m, 16m, 18m },
            Password: "Password123!");

        var result = await engine.RecordDisintegrationResultAsync(order.Id, payload, analyst.Id);

        Assert.True(result.AllStepsComplete);
        Assert.True(result.IsDefinitive);
        Assert.Equal("WithinLimits", result.Status);

        var reloadedOrder = await db.TestOrders.FindAsync(order.Id);
        Assert.NotNull(reloadedOrder);
        Assert.Equal(WorkflowStep.Ready, reloadedOrder.CurrentStep);

        var analysis = await db.TestAnalyses
            .Include(a => a.ParameterResults)
                .ThenInclude(pr => pr.Readings)
            .FirstOrDefaultAsync(a => a.TestOrderId == order.Id);

        Assert.NotNull(analysis);
        var pr = analysis.ParameterResults[0];
        Assert.Equal(1, pr.StageReached);
        Assert.Equal("WithinLimits", pr.ComparisonStatus);
        Assert.Equal("Complies", pr.ReportedDisplay);
        Assert.Equal(18m, pr.ReportedValue);
        Assert.Equal(6, pr.Readings.Count);
        Assert.All(pr.Readings, r => Assert.True(r.Passed));
    }

    [Fact]
    public async Task Stage1_ThreeFailures_DoesNotComplyImmediately()
    {
        using var db = NewDb();
        var (_, equip, _, _, _, order, analyst, _) = SetupDisintegrationScenario(db);
        var engine = TestServiceFactory.TestWorkflow(db);

        // 3 failures at S1 -> DoesNotComply (no stage 2)
        var payload = new DisintegrationPayload(
            AnalysedAt: DateTime.UtcNow,
            EquipmentId: equip.Id,
            Conditions: new Dictionary<string, string>
            {
                ["Medium"] = "Purified Water",
                ["Temperature"] = "37 ± 2 °C"
            },
            UnitMinutes: new List<decimal?> { 12m, 14m, 15m, 31m, 32m, 35m },
            Password: "Password123!");

        var result = await engine.RecordDisintegrationResultAsync(order.Id, payload, analyst.Id);

        Assert.True(result.AllStepsComplete);
        Assert.True(result.IsDefinitive);
        Assert.Equal("OutOfSpecification", result.Status);

        var reloadedOrder = await db.TestOrders.FindAsync(order.Id);
        Assert.NotNull(reloadedOrder);
        Assert.Equal(WorkflowStep.Ready, reloadedOrder.CurrentStep);

        var analysis = await db.TestAnalyses
            .Include(a => a.ParameterResults)
                .ThenInclude(pr => pr.Readings)
            .FirstOrDefaultAsync(a => a.TestOrderId == order.Id);

        Assert.NotNull(analysis);
        var pr = analysis.ParameterResults[0];
        Assert.Equal(1, pr.StageReached);
        Assert.Equal("OutOfSpecification", pr.ComparisonStatus);
        Assert.Equal("Does not comply", pr.ReportedDisplay);
        Assert.Equal(35m, pr.ReportedValue);
    }

    [Fact]
    public async Task Stage1_WithNotDisintegratedUnit_RecordedCorrectly()
    {
        using var db = NewDb();
        var (_, equip, _, _, _, order, analyst, _) = SetupDisintegrationScenario(db);
        var engine = TestServiceFactory.TestWorkflow(db);

        // 1 not-disintegrated (null) -> 1 failure -> NextStageRequired
        var payload = new DisintegrationPayload(
            AnalysedAt: DateTime.UtcNow,
            EquipmentId: equip.Id,
            Conditions: new Dictionary<string, string>
            {
                ["Medium"] = "Purified Water",
                ["Temperature"] = "37 ± 2 °C"
            },
            UnitMinutes: new List<decimal?> { 12m, 14m, 15m, 13m, 16m, null },
            Password: "Password123!");

        var result = await engine.RecordDisintegrationResultAsync(order.Id, payload, analyst.Id);
        Assert.Equal("NextStageRequired", result.Status);

        var analysis = await db.TestAnalyses
            .Include(a => a.ParameterResults)
                .ThenInclude(pr => pr.Readings)
            .FirstOrDefaultAsync(a => a.TestOrderId == order.Id);

        Assert.NotNull(analysis);
        var pr = analysis.ParameterResults[0];
        var nullReading = pr.Readings.First(r => r.Index == 6);
        Assert.Null(nullReading.Value1);
        Assert.Equal("Not disintegrated", nullReading.Text);
        Assert.False(nullReading.Passed);
        Assert.Equal(16m, pr.ReportedValue); // longest among disintegrated
    }

    [Fact]
    public async Task Stage2_WhenNotPending_Rejected()
    {
        using var db = NewDb();
        var (_, equip, _, _, _, order, analyst, _) = SetupDisintegrationScenario(db);
        var engine = TestServiceFactory.TestWorkflow(db);

        // Try stage 2 without any stage 1 -> rejected
        var stagePayload = new DisintegrationStagePayload(
            UnitMinutes: new List<decimal?> { 15m, 16m, 14m, 18m, 20m, 22m, 19m, 17m, 16m, 15m, 14m, 18m },
            Password: "Password123!");

        var ex1 = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            engine.RecordDisintegrationStageAsync(order.Id, stagePayload, analyst.Id));
        Assert.Contains("No active disintegration entry found", ex1.Message);

        // Record Stage 1 with all passing (Complies)
        var s1PassPayload = new DisintegrationPayload(
            AnalysedAt: DateTime.UtcNow,
            EquipmentId: equip.Id,
            Conditions: new Dictionary<string, string>
            {
                ["Medium"] = "Purified Water",
                ["Temperature"] = "37 ± 2 °C"
            },
            UnitMinutes: new List<decimal?> { 12m, 14m, 15m, 13m, 16m, 18m },
            Password: "Password123!");

        await engine.RecordDisintegrationResultAsync(order.Id, s1PassPayload, analyst.Id);

        // Try stage 2 now -> order already finalized
        var ex2 = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            engine.RecordDisintegrationStageAsync(order.Id, stagePayload, analyst.Id));
        Assert.Contains("already at Ready", ex2.Message);
    }

    [Fact]
    public async Task WrongUnitCounts_RejectedByEngine()
    {
        using var db = NewDb();
        var (_, equip, _, _, _, order, analyst, _) = SetupDisintegrationScenario(db);
        var engine = TestServiceFactory.TestWorkflow(db);

        // 5 units at stage 1
        var badS1Payload = new DisintegrationPayload(
            AnalysedAt: DateTime.UtcNow,
            EquipmentId: equip.Id,
            Conditions: new Dictionary<string, string>
            {
                ["Medium"] = "Purified Water",
                ["Temperature"] = "37 ± 2 °C"
            },
            UnitMinutes: new List<decimal?> { 12m, 14m, 15m, 13m, 16m },
            Password: "Password123!");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            engine.RecordDisintegrationResultAsync(order.Id, badS1Payload, analyst.Id));
        Assert.Contains("Stage 1 disintegration requires exactly 6 units", ex.Message);
    }
}
