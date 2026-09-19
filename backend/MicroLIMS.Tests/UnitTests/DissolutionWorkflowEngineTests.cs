using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class DissolutionWorkflowEngineTests
{
    private static MicroLimsDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MicroLimsDbContext(options);
    }

    private static (TestDefinition testDef, Equipment equip, SystemSuitabilityRun run, Item item, Specification spec, Sample sample, TestOrder order, User analyst, User head)
        SetupDissolutionScenario(MicroLimsDbContext db)
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
            Code = "DISS-SILD",
            DisplayName = "Sildenafil Dissolution",
            SectionId = fpSec.Id,
            WorkflowType = WorkflowType.Dissolution,
            EquationType = EquationType.Dissolution,
            RequiresSystemSuitability = true,
            DissolutionS1Offset = 5m,
            DissolutionS2MinOffset = 15m,
            DissolutionS3MinOffset = 25m,
            DissolutionS3MaxBelowS2Min = 2m,
            ConditionFields = "Medium, RPM",
            IsActive = true
        };
        db.TestDefinitions.Add(testDef);

        var equip = new Equipment
        {
            Name = "Dissolution Tester 1",
            Code = "EQ-DIS-01",
            Type = EquipmentType.DissolutionTester,
            SectionId = fpSec.Id
        };
        db.Equipment.Add(equip);

        var col = new ChromatographyColumn
        {
            Name = "C18 Column",
            Code = "COL-C18-01",
            SerialNumber = "SN-C18-01",
            SectionId = fpSec.Id,
            IsActive = true,
            CreatedByUserId = analyst.Id,
            LastModifiedByUserId = analyst.Id
        };
        db.ChromatographyColumns.Add(col);

        var standard = new Material
        {
            MaterialName = "Sildenafil Citrate Standard",
            MaterialType = MaterialType.ReferenceStandard,
            ManufacturerName = "USP",
            BatchNumber = "LOT-STD-01",
            ReceivingDate = DateTime.UtcNow.AddDays(-10),
            ExpiryDate = DateTime.UtcNow.AddYears(1),
            QuantityReceived = 100m,
            QuantityRemaining = 100m,
            Unit = MaterialUnit.Gram,
            Location = "Standard Storage",
            SectionId = fpSec.Id,
            Purity = 100m,
            CreatedByUserId = analyst.Id,
            LastModifiedByUserId = analyst.Id
        };
        db.Materials.Add(standard);
        db.SaveChanges();

        var sstRun = new SystemSuitabilityRun
        {
            Code = "SST-DIS-001",
            TestDefinitionId = testDef.Id,
            EquipmentId = equip.Id,
            ChromatographyColumnId = col.Id,
            ReferenceStandardMaterialId = standard.Id,
            StandardWeightMg = 50m,
            StandardDilution = 2500m, // C_s = 50 * 1.0 / 2500 = 0.02 mg/mL
            StandardPurityPercent = 100m,
            StandardMeanArea = 0.500m,
            Passed = true,
            SectionId = fpSec.Id,
            PerformedByUserId = analyst.Id,
            PerformedAt = DateTime.UtcNow
        };
        db.SystemSuitabilityRuns.Add(sstRun);

        var item = new Item
        {
            Code = "SILD-50",
            Name = "Sildenafil 50mg Tablets",
            Category = SampleCategory.FinishedProduct,
            IsActive = true
        };
        db.Items.Add(item);
        db.SaveChanges();

        var spec = new Specification
        {
            ItemId = item.Id,
            TestCode = testDef.Code,
            ParameterName = "Dissolution",
            LimitType = LimitType.DissolutionQ,
            LowerLimit = 80m, // Q = 80 %
            LabelClaim = 18m, // LC = 18 mg
            LabelClaimUnit = "mg",
            Unit = "%",
            SpecLimit = "Q = 80 %"
        };
        db.Specifications.Add(spec);

        var cause = new CauseOfTesting { Name = "Routine Release", IsActive = true };
        db.CausesOfTesting.Add(cause);

        var sample = new Sample
        {
            ReferenceNumber = "SMP-DISS-001",
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
            Status = ApprovalStatus.InProgress,
            SystemSuitabilityRunId = sstRun.Id
        };
        db.TestOrders.Add(order);
        db.SaveChanges();

        return (testDef, equip, sstRun, item, spec, sample, order, analyst, head);
    }

    [Fact]
    public async Task Stage1_NextStageRequired_OrderNotFinalized_ApprovalBlocked_Stage2_Finalized()
    {
        using var db = NewDb();
        var (_, equip, _, _, spec, sample, order, analyst, head) = SetupDissolutionScenario(db);

        var engine = TestServiceFactory.TestWorkflow(db);

        // Stage 1 payload: 6 areas. One vessel below Q+5 (84% -> area 0.4200 when factor is 200).
        // Area to percent: A_u * 0.02 * 900 * 1 * 100 / (0.500 * 18) = A_u * 200
        // 0.4200 * 200 = 84.0% (< 85%). Other 5 at 0.4500 * 200 = 90.0%.
        var payload1 = new DissolutionPayload(
            AnalysedAt: DateTime.UtcNow,
            EquipmentId: equip.Id,
            Conditions: new Dictionary<string, string>
            {
                ["Medium"] = "0.01M HCl 900mL",
                ["RPM"] = "50"
            },
            MediumVolumeMl: 900m,
            DilutionFactor: 1m,
            VesselAreas: new List<decimal> { 0.4200m, 0.4500m, 0.4500m, 0.4500m, 0.4500m, 0.4500m },
            Password: "Password123!",
            Comment: "Stage 1 run");

        var result1 = await engine.RecordDissolutionResultAsync(order.Id, payload1, analyst.Id);

        // 1. Assert result1 indicates NextStageRequired and NOT complete
        Assert.False(result1.AllStepsComplete);
        Assert.False(result1.IsDefinitive);
        Assert.Equal("NextStageRequired", result1.Status);

        // Order remains Running
        var reloadedOrder = await db.TestOrders.FindAsync(order.Id);
        Assert.NotNull(reloadedOrder);
        Assert.Equal(WorkflowStep.Running, reloadedOrder.CurrentStep);

        // Facts should show HasActiveAnalysis = false because pending stage
        var stepDetails = await engine.GetCurrentStepDetailsAsync(order.Id);
        Assert.False(stepDetails.Result.AllStepsComplete);
        Assert.False(stepDetails.Facts.HasActiveAnalysis);

        // The pending stage is not submitted for review, so it cannot reach approval.
        Assert.NotEqual(ApprovalStatus.ResultEntered, reloadedOrder.Status);

        // 2. Stage 2 payload: append 6 more vessels (all at 0.4500 -> 90.0%)
        // Mean of 12 units = (84 + 11*90)/12 = 1074/12 = 89.5% >= 80, all >= 65 -> Complies!
        var payload2 = new DissolutionStagePayload(
            VesselAreas: new List<decimal> { 0.4500m, 0.4500m, 0.4500m, 0.4500m, 0.4500m, 0.4500m },
            Password: "Password123!",
            Comment: "Stage 2 run");

        var result2 = await engine.RecordDissolutionStageAsync(order.Id, payload2, analyst.Id);

        Assert.True(result2.AllStepsComplete);
        Assert.True(result2.IsDefinitive);
        Assert.Equal("WithinLimits", result2.Status);

        // Order is now finalized to Ready!
        reloadedOrder = await db.TestOrders.FindAsync(order.Id);
        Assert.NotNull(reloadedOrder);
        Assert.Equal(WorkflowStep.Ready, reloadedOrder.CurrentStep);

        // StepDetails now shows complete
        stepDetails = await engine.GetCurrentStepDetailsAsync(order.Id);
        Assert.True(stepDetails.Result.AllStepsComplete);
        Assert.True(stepDetails.Facts.HasActiveAnalysis);

        // ParameterResult has 12 readings
        var analysis = await db.TestAnalyses
            .Include(a => a.ParameterResults)
                .ThenInclude(pr => pr.Readings)
            .FirstOrDefaultAsync(a => a.TestOrderId == order.Id);

        Assert.NotNull(analysis);
        Assert.Single(analysis.ParameterResults);
        var pr = analysis.ParameterResults[0];
        Assert.Equal(2, pr.StageReached);
        Assert.Equal("WithinLimits", pr.ComparisonStatus);
        Assert.Equal(12, pr.Readings.Count);
    }
}
