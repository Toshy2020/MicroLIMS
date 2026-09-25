using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class WeightVariationWorkflowEngineTests
{
    private static MicroLimsDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MicroLimsDbContext(options);
    }

    private static (TestDefinition testDef, Equipment equip, Item item, Specification spec, Sample sample, TestOrder order, User analyst, User head)
        SetupWeightVariationScenario(MicroLimsDbContext db, DosageForm dosageForm)
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
            Code = "WV-TEST",
            DisplayName = "Weight Variation Test",
            SectionId = fpSec.Id,
            WorkflowType = WorkflowType.WeightVariation,
            EquationType = EquationType.WeightVariation,
            RequiresSystemSuitability = false,
            WvUnitCount = 20,
            WvTabletBand1MaxMg = 130m,
            WvTabletBand1Percent = 10m,
            WvTabletBand2MaxMg = 324m,
            WvTabletBand2Percent = 7.5m,
            WvTabletBand3Percent = 5m,
            WvTabletMaxOutside = 2,
            WvCapsuleInnerPercent = 10m,
            WvCapsuleOuterPercent = 25m,
            WvCapsuleS1MaxOutside = 2,
            WvCapsuleS1MaxForRetest = 6,
            WvCapsuleS2ExtraUnits = 40,
            WvCapsuleS2MaxOutside = 6,
            ConditionFields = "Balance ID, Temperature",
            IsActive = true
        };
        db.TestDefinitions.Add(testDef);

        var equip = new Equipment
        {
            Name = "Analytical Balance 1",
            Code = "EQ-BAL-01",
            Type = EquipmentType.Balance,
            SectionId = fpSec.Id
        };
        db.Equipment.Add(equip);

        var item = new Item
        {
            Code = "PROD-WV",
            Name = "WV Product",
            Category = SampleCategory.FinishedProduct,
            IsActive = true
        };
        db.Items.Add(item);
        db.SaveChanges();

        var spec = new Specification
        {
            ItemId = item.Id,
            TestCode = testDef.Code,
            ParameterName = "Weight Variation",
            LimitType = LimitType.WeightVariation,
            DosageForm = dosageForm,
            Unit = "mg",
            SpecLimit = dosageForm == DosageForm.Tablet
                ? "USP <2091>: tablets, limit by average weight"
                : "USP <2091>: net content 90-110 % of average",
            ConversionFactor = 1.0m
        };
        db.Specifications.Add(spec);

        var cause = new CauseOfTesting { Name = "Routine Testing", IsActive = true };
        db.CausesOfTesting.Add(cause);

        var sample = new Sample
        {
            ReferenceNumber = "SMP-WV-001",
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
        var (_, equip, _, _, _, order, analyst, _) = SetupWeightVariationScenario(db, DosageForm.HardCapsule);
        var engine = TestServiceFactory.TestWorkflow(db);

        // Stage 1: Gross weights fail Step A (one gross 700, one 500, 18 at 600 -> mean 600, 700 dev 16.7% > 10%).
        // Net weights have 4 units outside 10% (between 3 and 6) -> NextStageRequired.
        var units1 = new List<WeightVariationUnitPayload>
        {
            new(GrossMg: 700m, ShellMg: 300m), // net 400
            new(GrossMg: 500m, ShellMg: 100m), // net 400
            new(GrossMg: 600m, ShellMg: 150m), // net 450 (dev 12.5% > 10%)
            new(GrossMg: 600m, ShellMg: 150m), // net 450 (dev 12.5% > 10%)
            new(GrossMg: 600m, ShellMg: 250m), // net 350 (dev 12.5% > 10%)
            new(GrossMg: 600m, ShellMg: 250m)  // net 350 (dev 12.5% > 10%)
        };
        for (int i = 0; i < 14; i++) units1.Add(new(GrossMg: 600m, ShellMg: 200m)); // net 400

        var payload1 = new WeightVariationPayload(
            AnalysedAt: DateTime.UtcNow,
            EquipmentId: equip.Id,
            Conditions: new Dictionary<string, string>
            {
                ["Balance ID"] = "BAL-01",
                ["Temperature"] = "22 °C"
            },
            Units: units1,
            Password: "Password123!",
            Comment: "Stage 1 run");

        var result1 = await engine.RecordWeightVariationResultAsync(order.Id, payload1, analyst.Id);

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

        // Stage 2: 40 more units (all net 400).
        // Total 60 units: 4 outside 10% (<= S2MaxOutside = 6), none outside 25% -> Complies!
        var units2 = new List<WeightVariationUnitPayload>();
        for (int i = 0; i < 40; i++) units2.Add(new(GrossMg: 600m, ShellMg: 200m)); // net 400

        var payload2 = new WeightVariationStagePayload(
            Units: units2,
            Password: "Password123!",
            Comment: "Stage 2 run");

        var result2 = await engine.RecordWeightVariationStageAsync(order.Id, payload2, analyst.Id);

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

        // ParameterResult has 60 readings, stage 2, ReportedValue = 400mg
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
        Assert.Equal(400m, pr.ReportedValue);
        Assert.Equal(60, pr.Readings.Count);

        // Check Stage 1 readings updated against 60-unit mean
        var reading3 = pr.Readings.First(r => r.Index == 3);
        Assert.Equal(1, reading3.Stage);
        Assert.Equal(600m, reading3.Value1); // Gross
        Assert.Equal(150m, reading3.Value2); // Shell
        Assert.Equal(450m, reading3.ComputedValue); // Net
        Assert.Equal(12.5m, reading3.Value3); // Dev%
        Assert.False(reading3.Passed);

        // Check Stage 2 reading
        var reading21 = pr.Readings.First(r => r.Index == 21);
        Assert.Equal(2, reading21.Stage);
        Assert.Equal(600m, reading21.Value1);
        Assert.Equal(200m, reading21.Value2);
        Assert.Equal(400m, reading21.ComputedValue);
        Assert.Equal(0m, reading21.Value3);
        Assert.True(reading21.Passed);
    }

    [Fact]
    public async Task Stage1_Tablet_TwoOutsideBand_CompliesDirectly()
    {
        using var db = NewDb();
        var (_, equip, _, _, _, order, analyst, _) = SetupWeightVariationScenario(db, DosageForm.Tablet);
        var engine = TestServiceFactory.TestWorkflow(db);

        // 2 units at 530mg, 470mg (dev 6% > 5%), 18 units at 500mg (dev 0%)
        // Mean = 500mg, Band 3 (5%). 2 outside <= TabletMaxOutside (2) -> Complies
        var units = new List<WeightVariationUnitPayload>
        {
            new(WeightMg: 530m),
            new(WeightMg: 470m)
        };
        for (int i = 0; i < 18; i++) units.Add(new(WeightMg: 500m));

        var payload = new WeightVariationPayload(
            AnalysedAt: DateTime.UtcNow,
            EquipmentId: equip.Id,
            Conditions: new Dictionary<string, string>
            {
                ["Balance ID"] = "BAL-01",
                ["Temperature"] = "22 °C"
            },
            Units: units,
            Password: "Password123!");

        var result = await engine.RecordWeightVariationResultAsync(order.Id, payload, analyst.Id);

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
        Assert.Equal(500m, pr.ReportedValue);
        Assert.Equal(20, pr.Readings.Count);

        var r1 = pr.Readings.First(r => r.Index == 1);
        Assert.Equal(530m, r1.Value1);
        Assert.Null(r1.Value2);
        Assert.Equal(530m, r1.ComputedValue);
        Assert.Equal(6.0m, r1.Value3);
        Assert.False(r1.Passed);
    }

    [Fact]
    public async Task Stage1_HardCapsule_StepAPasses_CompliesImmediately()
    {
        using var db = NewDb();
        var (_, equip, _, _, _, order, analyst, _) = SetupWeightVariationScenario(db, DosageForm.HardCapsule);
        var engine = TestServiceFactory.TestWorkflow(db);

        // Gross weights all identical (600mg), so Step A passes
        var units = new List<WeightVariationUnitPayload>();
        for (int i = 0; i < 20; i++) units.Add(new(GrossMg: 600m, ShellMg: 200m));

        var payload = new WeightVariationPayload(
            AnalysedAt: DateTime.UtcNow,
            EquipmentId: equip.Id,
            Conditions: new Dictionary<string, string>
            {
                ["Balance ID"] = "BAL-01",
                ["Temperature"] = "22 °C"
            },
            Units: units,
            Password: "Password123!");

        var result = await engine.RecordWeightVariationResultAsync(order.Id, payload, analyst.Id);

        Assert.True(result.AllStepsComplete);
        Assert.Equal("WithinLimits", result.Status);

        var pr = await db.ParameterResults.SingleAsync(p => p.TestOrderId == order.Id);
        Assert.Equal("Complies", pr.ReportedDisplay);
        Assert.Contains("\"stepAPassed\":true", pr.CalculationJson);
    }

    [Fact]
    public async Task Stage1_Tablet_ThreeFailures_DoesNotComplyImmediately()
    {
        using var db = NewDb();
        var (_, equip, _, _, _, order, analyst, _) = SetupWeightVariationScenario(db, DosageForm.Tablet);
        var engine = TestServiceFactory.TestWorkflow(db);

        // 3 units outside 5% band
        var units = new List<WeightVariationUnitPayload>
        {
            new(WeightMg: 530m),
            new(WeightMg: 530m),
            new(WeightMg: 470m),
            new(WeightMg: 490m),
            new(WeightMg: 490m),
            new(WeightMg: 490m)
        };
        for (int i = 0; i < 14; i++) units.Add(new(WeightMg: 500m));

        var payload = new WeightVariationPayload(
            AnalysedAt: DateTime.UtcNow,
            EquipmentId: equip.Id,
            Conditions: new Dictionary<string, string>
            {
                ["Balance ID"] = "BAL-01",
                ["Temperature"] = "22 °C"
            },
            Units: units,
            Password: "Password123!");

        var result = await engine.RecordWeightVariationResultAsync(order.Id, payload, analyst.Id);

        Assert.True(result.AllStepsComplete);
        Assert.True(result.IsDefinitive);
        Assert.Equal("OutOfSpecification", result.Status);

        var reloadedOrder = await db.TestOrders.FindAsync(order.Id);
        Assert.NotNull(reloadedOrder);
        Assert.Equal(WorkflowStep.Ready, reloadedOrder.CurrentStep);

        var pr = await db.ParameterResults.SingleAsync(p => p.TestOrderId == order.Id);
        Assert.Equal("Does not comply", pr.ReportedDisplay);
        Assert.Equal("OutOfSpecification", pr.ComparisonStatus);
    }

    [Fact]
    public async Task Stage2_UsesStage1Snapshot_WhenTestMasterAndSpecEditedBetweenStages()
    {
        using var db = NewDb();
        var (testDef, equip, _, _, _, order, analyst, _) = SetupWeightVariationScenario(db, DosageForm.HardCapsule);
        var engine = TestServiceFactory.TestWorkflow(db);

        // Stage 1 with 4 units outside -> NextStageRequired
        var units1 = new List<WeightVariationUnitPayload>
        {
            new(GrossMg: 700m, ShellMg: 300m),
            new(GrossMg: 500m, ShellMg: 100m),
            new(GrossMg: 600m, ShellMg: 150m),
            new(GrossMg: 600m, ShellMg: 150m),
            new(GrossMg: 600m, ShellMg: 250m),
            new(GrossMg: 600m, ShellMg: 250m)
        };
        for (int i = 0; i < 14; i++) units1.Add(new(GrossMg: 600m, ShellMg: 200m));

        await engine.RecordWeightVariationResultAsync(order.Id, new WeightVariationPayload(
            DateTime.UtcNow, equip.Id,
            new Dictionary<string, string> { ["Balance ID"] = "BAL-01", ["Temperature"] = "22 °C" },
            units1, "Password123!"), analyst.Id);

        // Edits after Stage 1: change S2MaxOutside to 1 (which would fail 4 outside) and extra units to 10
        testDef.WvCapsuleS2MaxOutside = 1;
        testDef.WvCapsuleS2ExtraUnits = 10;
        await db.SaveChangesAsync();

        // Stage 2: submit original 40 units
        var units2 = new List<WeightVariationUnitPayload>();
        for (int i = 0; i < 40; i++) units2.Add(new(GrossMg: 600m, ShellMg: 200m));

        var result2 = await engine.RecordWeightVariationStageAsync(order.Id, new WeightVariationStagePayload(
            units2, "Password123!"), analyst.Id);

        // Still passes because snapshotted S2MaxOutside (6) is used!
        Assert.Equal("WithinLimits", result2.Status);
        var pr = await db.ParameterResults.Include(p => p.Readings).SingleAsync(p => p.TestOrderId == order.Id);
        Assert.Equal(60, pr.Readings.Count);
    }

    [Fact]
    public async Task Stage2_WhenNotPending_Rejected()
    {
        using var db = NewDb();
        var (_, equip, _, _, _, order, analyst, _) = SetupWeightVariationScenario(db, DosageForm.HardCapsule);
        var engine = TestServiceFactory.TestWorkflow(db);

        var stagePayload = new WeightVariationStagePayload(
            Units: Enumerable.Range(0, 40).Select(_ => new WeightVariationUnitPayload(GrossMg: 600m, ShellMg: 200m)).ToList(),
            Password: "Password123!");

        // Try stage 2 without stage 1 -> rejected
        var ex1 = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            engine.RecordWeightVariationStageAsync(order.Id, stagePayload, analyst.Id));
        Assert.Contains("No active weight variation entry found", ex1.Message);

        // Record Stage 1 with Step A pass (Complies)
        var s1PassPayload = new WeightVariationPayload(
            AnalysedAt: DateTime.UtcNow,
            EquipmentId: equip.Id,
            Conditions: new Dictionary<string, string> { ["Balance ID"] = "BAL-01", ["Temperature"] = "22 °C" },
            Units: Enumerable.Range(0, 20).Select(_ => new WeightVariationUnitPayload(GrossMg: 600m, ShellMg: 200m)).ToList(),
            Password: "Password123!");

        await engine.RecordWeightVariationResultAsync(order.Id, s1PassPayload, analyst.Id);

        // Try stage 2 now -> order already finalized to Ready
        var ex2 = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            engine.RecordWeightVariationStageAsync(order.Id, stagePayload, analyst.Id));
        Assert.Contains("already at Ready", ex2.Message);
    }

    [Fact]
    public async Task WrongUnitCounts_RejectedByEngine()
    {
        using var db = NewDb();
        var (_, equip, _, _, _, order, analyst, _) = SetupWeightVariationScenario(db, DosageForm.Tablet);
        var engine = TestServiceFactory.TestWorkflow(db);

        // 19 units at stage 1
        var badS1Payload = new WeightVariationPayload(
            AnalysedAt: DateTime.UtcNow,
            EquipmentId: equip.Id,
            Conditions: new Dictionary<string, string> { ["Balance ID"] = "BAL-01", ["Temperature"] = "22 °C" },
            Units: Enumerable.Range(0, 19).Select(_ => new WeightVariationUnitPayload(WeightMg: 500m)).ToList(),
            Password: "Password123!");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            engine.RecordWeightVariationResultAsync(order.Id, badS1Payload, analyst.Id));
        Assert.Contains("Stage 1 weight variation requires exactly 20 units", ex.Message);
    }

    [Fact]
    public async Task WrongShape_RejectedByEngine()
    {
        using var db = NewDb();
        var (_, equip, _, _, _, order, analyst, _) = SetupWeightVariationScenario(db, DosageForm.Tablet);
        var engine = TestServiceFactory.TestWorkflow(db);

        // Tablet given GrossMg instead of WeightMg
        var badShapePayload = new WeightVariationPayload(
            AnalysedAt: DateTime.UtcNow,
            EquipmentId: equip.Id,
            Conditions: new Dictionary<string, string> { ["Balance ID"] = "BAL-01", ["Temperature"] = "22 °C" },
            Units: Enumerable.Range(0, 20).Select(_ => new WeightVariationUnitPayload(GrossMg: 600m, ShellMg: 200m)).ToList(),
            Password: "Password123!");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            engine.RecordWeightVariationResultAsync(order.Id, badShapePayload, analyst.Id));
        Assert.Contains("Tablet units must only contain WeightMg", ex.Message);
    }
}
