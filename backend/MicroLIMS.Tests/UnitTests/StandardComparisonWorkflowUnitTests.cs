using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using System.Text.Json;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class StandardComparisonWorkflowUnitTests
{
    private static MicroLimsDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MicroLimsDbContext(options);
    }

    private static (
        DocumentSection fpSec,
        User fpUser,
        TestDefinition testDef,
        TestAnalyte analyte1,
        TestAnalyte analyte2,
        Item item,
        ProductionStage stage,
        Sample sample,
        TestOrder order,
        Specification spec1,
        Specification spec2,
        SystemSuitabilityRun sstRun)
        SetupScenario(
            MicroLimsDbContext db,
            int sampleReplicates = 2,
            decimal? maxPreparationRsd = 2.0m,
            bool isMultiAnalyte = false)
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
            ?? new Role { Name = "Analyst", Type = RoleType.Analyst, IsActive = true };
        if (analystRole.Id == 0) { db.Roles.Add(analystRole); db.SaveChanges(); }

        var passwordHash = BCrypt.Net.BCrypt.HashPassword("ValidPassword123!");

        var fpUser = new User
        {
            Username = "fp_analyst_" + Guid.NewGuid().ToString("N")[..6],
            FullName = "FP Analyst",
            RoleId = analystRole.Id,
            Role = analystRole,
            PasswordHash = passwordHash,
            IsActive = true
        };
        db.Users.Add(fpUser);
        db.SaveChanges();

        db.UserOrgMemberships.Add(new UserOrgMembership
        {
            UserId = fpUser.Id,
            DepartmentId = fpSec.DepartmentId,
            SectionId = fpSec.Id
        });
        db.SaveChanges();

        var methodAbbr = "SC" + Guid.NewGuid().ToString("N")[..4].ToUpperInvariant();
        var testDef = new TestDefinition
        {
            Code = $"{methodAbbr}_ASSAY",
            DisplayName = $"{methodAbbr} Standard Comparison Assay",
            SectionId = fpSec.Id,
            WorkflowType = WorkflowType.StandardComparison,
            EquationType = EquationType.StandardComparison,
            RequiresSystemSuitability = true,
            MethodAbbreviation = methodAbbr,
            HplcMaxPreparationRsdPercent = maxPreparationRsd,
            IsActive = true
        };
        db.TestDefinitions.Add(testDef);
        db.SaveChanges();

        var stage = new ProductionStage
        {
            Name = "Finished Product Stage",
            Role = ProductionStageRole.Finished,
            IsActive = true
        };
        db.ProductionStages.Add(stage);
        db.SaveChanges();

        var stageReplicate = new TestDefinitionStageReplicate
        {
            TestDefinitionId = testDef.Id,
            Role = ProductionStageRole.Finished,
            StandardReplicates = 5,
            SampleReplicates = sampleReplicates
        };
        db.TestDefinitionStageReplicates.Add(stageReplicate);
        db.SaveChanges();

        var equip = new Equipment
        {
            Name = "HPLC Waters Alliance",
            Code = $"HPLC-{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
            Type = EquipmentType.Hplc,
            SectionId = fpSec.Id,
            CdsSoftware = CdsSoftware.WatersEmpower3
        };
        db.Equipment.Add(equip);

        var col = new ChromatographyColumn
        {
            Name = "C18 250x4.6mm",
            Code = $"COL-{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
            SerialNumber = "SN-12345",
            SectionId = fpSec.Id,
            IsActive = true,
            CreatedByUserId = fpUser.Id,
            LastModifiedByUserId = fpUser.Id
        };
        db.ChromatographyColumns.Add(col);

        var stdMat1 = new Material
        {
            MaterialName = "Paracetamol RS",
            MaterialType = MaterialType.ReferenceStandard,
            ManufacturerName = "USP",
            BatchNumber = "LOT-PCM",
            ReceivingDate = DateTime.UtcNow.AddDays(-10),
            ExpiryDate = DateTime.UtcNow.AddYears(1),
            QuantityReceived = 100m,
            QuantityRemaining = 100m,
            Unit = MaterialUnit.Gram,
            Location = "Standard Storage",
            SectionId = fpSec.Id,
            Purity = 99.5m,
            CreatedByUserId = fpUser.Id,
            LastModifiedByUserId = fpUser.Id
        };
        var stdMat2 = new Material
        {
            MaterialName = "Caffeine RS",
            MaterialType = MaterialType.ReferenceStandard,
            ManufacturerName = "USP",
            BatchNumber = "LOT-CAF",
            ReceivingDate = DateTime.UtcNow.AddDays(-10),
            ExpiryDate = DateTime.UtcNow.AddYears(1),
            QuantityReceived = 100m,
            QuantityRemaining = 100m,
            Unit = MaterialUnit.Gram,
            Location = "Standard Storage",
            SectionId = fpSec.Id,
            Purity = 99.8m,
            CreatedByUserId = fpUser.Id,
            LastModifiedByUserId = fpUser.Id
        };
        db.Materials.AddRange(stdMat1, stdMat2);
        db.SaveChanges();

        var analyte1 = new TestAnalyte
        {
            TestDefinitionId = testDef.Id,
            Element = "Paracetamol",
            WavelengthNm = 243.0m,
            DisplayOrder = 1,
            IsActive = true
        };
        var analyte2 = new TestAnalyte
        {
            TestDefinitionId = testDef.Id,
            Element = "Caffeine",
            WavelengthNm = 275.0m,
            DisplayOrder = 2,
            IsActive = true
        };
        db.TestAnalytes.AddRange(analyte1, analyte2);
        db.SaveChanges();

        var item = new Item
        {
            Code = $"ITEM-{Guid.NewGuid():N}"[..8].ToUpperInvariant(),
            Name = "Paracetamol Extra Tablets",
            Category = SampleCategory.FinishedProduct,
            IsActive = true
        };
        db.Items.Add(item);
        db.SaveChanges();

        var spec1 = new Specification
        {
            ItemId = item.Id,
            TestCode = testDef.Code,
            ParameterName = "Paracetamol Assay",
            TestAnalyteId = analyte1.Id,
            LimitType = LimitType.Range,
            LowerLimit = 95.0m,
            UpperLimit = 105.0m,
            LowerInclusive = true,
            UpperInclusive = true,
            DisplayOrder = 1
        };
        db.Specifications.Add(spec1);

        Specification spec2 = null!;
        if (isMultiAnalyte)
        {
            spec2 = new Specification
            {
                ItemId = item.Id,
                TestCode = testDef.Code,
                ParameterName = "Caffeine Assay",
                TestAnalyteId = analyte2.Id,
                LimitType = LimitType.Range,
                LowerLimit = 90.0m,
                UpperLimit = 110.0m,
                LowerInclusive = true,
                UpperInclusive = true,
                DisplayOrder = 2
            };
            db.Specifications.Add(spec2);
        }
        db.SaveChanges();

        var cause = new CauseOfTesting { Name = "Commercial Release", IsActive = true };
        db.CausesOfTesting.Add(cause);

        var sample = new Sample
        {
            ReferenceNumber = $"SMP-{Guid.NewGuid():N}"[..10].ToUpperInvariant(),
            ItemId = item.Id,
            Category = SampleCategory.FinishedProduct,
            ReceivedAt = DateTime.UtcNow.AddDays(-1),
            CauseOfTesting = cause,
            ReceivedByUserId = fpUser.Id,
            ProductionStageId = stage.Id,
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

        var signature = new ElectronicSignature
        {
            UserId = fpUser.Id,
            UserFullNameSnapshot = fpUser.FullName,
            UsernameSnapshot = fpUser.Username,
            RoleSnapshot = "Analyst",
            MeaningOfSignature = SignatureMeaning.SuitabilityRunPerformed,
            SignedAt = DateTime.UtcNow,
            EntityType = "SystemSuitabilityRun",
            EntityId = 0
        };
        db.ElectronicSignatures.Add(signature);
        db.SaveChanges();

        var sstRun = new SystemSuitabilityRun
        {
            Code = $"{methodAbbr} S.S 01/092026",
            TestDefinitionId = testDef.Id,
            SectionId = fpSec.Id,
            EquipmentId = equip.Id,
            ChromatographyColumnId = col.Id,
            ReferenceStandardMaterialId = stdMat1.Id,
            StandardWeightMg = 50.1m,
            TheoreticalWeightMg = 50.0m,
            MoisturePercent = 0.2m,
            StandardDilution = 100.0m,
            StandardPurityPercent = 99.5m,
            StandardMeanArea = 2000000m,
            Passed = true,
            PerformedByUserId = fpUser.Id,
            PerformedAt = DateTime.UtcNow,
            SignatureId = signature.Id
        };
        db.SystemSuitabilityRuns.Add(sstRun);
        db.SaveChanges();

        var runA1 = new SystemSuitabilityRunAnalyte
        {
            SystemSuitabilityRunId = sstRun.Id,
            TestAnalyteId = analyte1.Id,
            AnalyteName = analyte1.Element,
            WavelengthNm = analyte1.WavelengthNm,
            ReferenceStandardMaterialId = stdMat1.Id,
            StandardWeightMg = 50.1m,
            TheoreticalWeightMg = 50.0m,
            MoisturePercent = 0.2m,
            StandardDilution = 100.0m,
            StandardPurityPercent = 99.5m,
            StandardMeanArea = 2000000m,
            Passed = true
        };
        db.SystemSuitabilityRunAnalytes.Add(runA1);

        if (isMultiAnalyte)
        {
            var runA2 = new SystemSuitabilityRunAnalyte
            {
                SystemSuitabilityRunId = sstRun.Id,
                TestAnalyteId = analyte2.Id,
                AnalyteName = analyte2.Element,
                WavelengthNm = analyte2.WavelengthNm,
                ReferenceStandardMaterialId = stdMat2.Id,
                StandardWeightMg = 25.0m,
                TheoreticalWeightMg = 25.0m,
                MoisturePercent = 0.1m,
                StandardDilution = 100.0m,
                StandardPurityPercent = 99.8m,
                StandardMeanArea = 1500000m,
                Passed = true
            };
            db.SystemSuitabilityRunAnalytes.Add(runA2);
        }
        db.SaveChanges();

        order.SystemSuitabilityRunId = sstRun.Id;
        db.SaveChanges();

        return (fpSec, fpUser, testDef, analyte1, analyte2, item, stage, sample, order, spec1, spec2, sstRun);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task RecordResult_1_2_3_Preparations_SucceedsWhenMatchingStageConfig(int prepCount)
    {
        using var db = NewDb();
        var (fpSec, fpUser, testDef, a1, _, _, _, _, order, _, _, sstRun) = SetupScenario(db, sampleReplicates: prepCount);
        var engine = TestServiceFactory.TestWorkflow(db);

        var preps = Enumerable.Range(1, prepCount)
            .Select(i => new StandardComparisonPreparationInput(100.0m, 100.2m, null))
            .ToList();

        var responses = Enumerable.Range(1, prepCount)
            .Select(i => new StandardComparisonResponseInput(a1.Id, i, 2005000m))
            .ToList();

        var payload = new StandardComparisonPayload(
            DateTime.UtcNow,
            sstRun.EquipmentId,
            preps,
            responses,
            "ValidPassword123!");

        var result = await engine.RecordStandardComparisonResultAsync(order.Id, payload, fpUser.Id);

        Assert.NotNull(result);

        var savedAnalysis = await db.TestAnalyses
            .Include(a => a.ParameterResults)
                .ThenInclude(p => p.Readings)
            .FirstOrDefaultAsync(a => a.TestOrderId == order.Id && a.IsActive);

        Assert.NotNull(savedAnalysis);
        Assert.Equal(WorkflowType.StandardComparison, savedAnalysis.AnalysisType);
        Assert.Single(savedAnalysis.ParameterResults);

        var paramResult = savedAnalysis.ParameterResults[0];
        Assert.Equal(prepCount, paramResult.Readings.Count);
        Assert.NotNull(paramResult.ReportedValue);
        Assert.Equal("WithinLimits", paramResult.ComparisonStatus);

        // CalculationJson check
        var calcData = JsonSerializer.Deserialize<StandardComparisonCalculationData>(
            paramResult.CalculationJson!,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(calcData);
        Assert.Equal(prepCount, calcData.Preparations.Count);
    }

    [Fact]
    public async Task RecordResult_PreparationCountMismatch_IsRejected()
    {
        using var db = NewDb();
        var (fpSec, fpUser, testDef, a1, _, _, _, _, order, _, _, sstRun) = SetupScenario(db, sampleReplicates: 2);
        var engine = TestServiceFactory.TestWorkflow(db);

        // Send 3 preparations when stage requires 2
        var preps = new List<StandardComparisonPreparationInput>
        {
            new(100.0m, 100.0m, null),
            new(100.0m, 100.0m, null),
            new(100.0m, 100.0m, null)
        };
        var responses = new List<StandardComparisonResponseInput>
        {
            new(a1.Id, 1, 2000000m),
            new(a1.Id, 2, 2000000m),
            new(a1.Id, 3, 2000000m)
        };

        var payload = new StandardComparisonPayload(
            DateTime.UtcNow,
            sstRun.EquipmentId,
            preps,
            responses,
            "ValidPassword123!");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            engine.RecordStandardComparisonResultAsync(order.Id, payload, fpUser.Id));

        Assert.Contains("Expected exactly 2 sample preparations", ex.Message);
    }

    [Fact]
    public async Task RecordResult_UnreconciledStage_IsRejectedWithClearMessage()
    {
        using var db = NewDb();
        var (fpSec, fpUser, testDef, a1, _, _, _, sample, order, _, _, sstRun) = SetupScenario(db, sampleReplicates: 2);
        var engine = TestServiceFactory.TestWorkflow(db);

        // Remove reconciled stage from sample
        sample.ProductionStageId = null;
        await db.SaveChangesAsync();

        var payload = new StandardComparisonPayload(
            DateTime.UtcNow,
            sstRun.EquipmentId,
            new List<StandardComparisonPreparationInput> { new(100m, 100m, null), new(100m, 100m, null) },
            new List<StandardComparisonResponseInput> { new(a1.Id, 1, 2000000m), new(a1.Id, 2, 2000000m) },
            "ValidPassword123!");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            engine.RecordStandardComparisonResultAsync(order.Id, payload, fpUser.Id));

        Assert.Contains("Sample has no reconciled production stage", ex.Message);
    }

    [Fact]
    public async Task RecordResult_StageRoleNotConfiguredInTestMaster_IsRejectedWithClearMessage()
    {
        using var db = NewDb();
        var (fpSec, fpUser, testDef, a1, _, _, _, sample, order, _, _, sstRun) = SetupScenario(db, sampleReplicates: 2);
        var engine = TestServiceFactory.TestWorkflow(db);

        // Remove replicate row for this stage role
        var stageReps = await db.TestDefinitionStageReplicates.Where(r => r.TestDefinitionId == testDef.Id).ToListAsync();
        db.TestDefinitionStageReplicates.RemoveRange(stageReps);
        await db.SaveChangesAsync();

        var payload = new StandardComparisonPayload(
            DateTime.UtcNow,
            sstRun.EquipmentId,
            new List<StandardComparisonPreparationInput> { new(100m, 100m, null), new(100m, 100m, null) },
            new List<StandardComparisonResponseInput> { new(a1.Id, 1, 2000000m), new(a1.Id, 2, 2000000m) },
            "ValidPassword123!");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            engine.RecordStandardComparisonResultAsync(order.Id, payload, fpUser.Id));

        Assert.Contains("No replicate configuration is set for stage role", ex.Message);
    }

    [Fact]
    public async Task RecordResult_SampleWeighIn_OutsideWindow_RequiresJustification()
    {
        using var db = NewDb();
        var (fpSec, fpUser, testDef, a1, _, _, _, _, order, _, _, sstRun) = SetupScenario(db, sampleReplicates: 2);
        var engine = TestServiceFactory.TestWorkflow(db);

        // Actual weight 115mg vs theoretical 100mg -> +15% (> 10%) without justification
        var payloadNoJustification = new StandardComparisonPayload(
            DateTime.UtcNow,
            sstRun.EquipmentId,
            new List<StandardComparisonPreparationInput>
            {
                new(100.0m, 115.0m, null),
                new(100.0m, 100.0m, null)
            },
            new List<StandardComparisonResponseInput>
            {
                new(a1.Id, 1, 2300000m),
                new(a1.Id, 2, 2000000m)
            },
            "ValidPassword123!");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            engine.RecordStandardComparisonResultAsync(order.Id, payloadNoJustification, fpUser.Id));

        Assert.Contains("Weigh-in justification is required for sample preparation 1", ex.Message);

        // With justification -> succeeds and records out of window flag and justification
        var payloadWithJustification = new StandardComparisonPayload(
            DateTime.UtcNow,
            sstRun.EquipmentId,
            new List<StandardComparisonPreparationInput>
            {
                new(100.0m, 115.0m, "Sample hygroscopic absorption during weighing."),
                new(100.0m, 100.0m, null)
            },
            new List<StandardComparisonResponseInput>
            {
                new(a1.Id, 1, 2300000m),
                new(a1.Id, 2, 2000000m)
            },
            "ValidPassword123!");

        var result = await engine.RecordStandardComparisonResultAsync(order.Id, payloadWithJustification, fpUser.Id);
        Assert.NotNull(result);

        var savedAnalysis = await db.TestAnalyses
            .Include(a => a.ParameterResults)
            .FirstOrDefaultAsync(a => a.TestOrderId == order.Id && a.IsActive);
        Assert.NotNull(savedAnalysis);

        var calc = JsonSerializer.Deserialize<StandardComparisonCalculationData>(
            savedAnalysis.ParameterResults[0].CalculationJson!,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(calc);
        Assert.True(calc.Preparations[0].WeighInOutOfWindow);
        Assert.Equal(15.0m, calc.Preparations[0].WeighInDeviationPercent);
        Assert.Equal("Sample hygroscopic absorption during weighing.", calc.Preparations[0].WeighInJustification);
        Assert.False(calc.Preparations[1].WeighInOutOfWindow);
    }

    [Fact]
    public async Task RecordResult_MissingMoistureOnRun_IsRejected()
    {
        using var db = NewDb();
        var (fpSec, fpUser, testDef, a1, _, _, _, _, order, _, _, sstRun) = SetupScenario(db, sampleReplicates: 2);
        var engine = TestServiceFactory.TestWorkflow(db);

        var runAnalyte = await db.SystemSuitabilityRunAnalytes.FirstAsync(a => a.SystemSuitabilityRunId == sstRun.Id);
        runAnalyte.MoisturePercent = null;
        await db.SaveChangesAsync();

        var payload = new StandardComparisonPayload(
            DateTime.UtcNow,
            sstRun.EquipmentId,
            new List<StandardComparisonPreparationInput> { new(100m, 100m, null), new(100m, 100m, null) },
            new List<StandardComparisonResponseInput> { new(a1.Id, 1, 2000000m), new(a1.Id, 2, 2000000m) },
            "ValidPassword123!");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            engine.RecordStandardComparisonResultAsync(order.Id, payload, fpUser.Id));

        Assert.Contains("missing or invalid moisture percent", ex.Message);
    }

    [Fact]
    public async Task RecordResult_HighPreparationRsd_SetsRequiresReview()
    {
        using var db = NewDb();
        // maxPreparationRsd = 2.0%
        var (fpSec, fpUser, testDef, a1, _, _, _, _, order, _, _, sstRun) = SetupScenario(db, sampleReplicates: 2, maxPreparationRsd: 2.0m);
        var engine = TestServiceFactory.TestWorkflow(db);

        var payload = new StandardComparisonPayload(
            DateTime.UtcNow,
            sstRun.EquipmentId,
            new List<StandardComparisonPreparationInput>
            {
                new(100.0m, 100.0m, null),
                new(100.0m, 100.0m, null)
            },
            new List<StandardComparisonResponseInput>
            {
                new(a1.Id, 1, 1800000m), // yields ~89.6%
                new(a1.Id, 2, 2200000m)  // yields ~109.5%
            },
            "ValidPassword123!");

        var result = await engine.RecordStandardComparisonResultAsync(order.Id, payload, fpUser.Id);
        Assert.NotNull(result);

        var savedAnalysis = await db.TestAnalyses
            .Include(a => a.ParameterResults)
            .FirstOrDefaultAsync(a => a.TestOrderId == order.Id && a.IsActive);
        Assert.NotNull(savedAnalysis);

        var param = savedAnalysis.ParameterResults[0];
        Assert.Equal("RequiresReview", param.ComparisonStatus);

        var calc = JsonSerializer.Deserialize<StandardComparisonCalculationData>(
            param.CalculationJson!,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(calc);
        Assert.True(calc.RsdExceeded);
        Assert.NotNull(calc.ReviewReason);
        Assert.Contains("exceeds maximum allowed 2.00%", calc.ReviewReason);
    }

    [Fact]
    public async Task RecordResult_InvalidSuitabilityRun_IsRejected()
    {
        using var db = NewDb();
        var (fpSec, fpUser, testDef, a1, _, _, _, _, order, _, _, sstRun) = SetupScenario(db, sampleReplicates: 2);
        var engine = TestServiceFactory.TestWorkflow(db);

        // 1. Unlinked run
        order.SystemSuitabilityRunId = null;
        await db.SaveChangesAsync();

        var payload = new StandardComparisonPayload(
            DateTime.UtcNow,
            sstRun.EquipmentId,
            new List<StandardComparisonPreparationInput> { new(100m, 100m, null), new(100m, 100m, null) },
            new List<StandardComparisonResponseInput> { new(a1.Id, 1, 2000000m), new(a1.Id, 2, 2000000m) },
            "ValidPassword123!");

        var ex1 = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            engine.RecordStandardComparisonResultAsync(order.Id, payload, fpUser.Id));
        Assert.Contains("must be linked to a system suitability run", ex1.Message);

        // 2. Failed run
        order.SystemSuitabilityRunId = sstRun.Id;
        sstRun.Passed = false;
        await db.SaveChangesAsync();

        var ex2 = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            engine.RecordStandardComparisonResultAsync(order.Id, payload, fpUser.Id));
        Assert.Contains("did not pass", ex2.Message);

        // 3. Different section
        sstRun.Passed = true;
        sstRun.SectionId = 9999;
        await db.SaveChangesAsync();

        var ex3 = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            engine.RecordStandardComparisonResultAsync(order.Id, payload, fpUser.Id));
        Assert.Contains("different laboratory section", ex3.Message);
    }

    [Fact]
    public async Task RecordResult_RelinkIsBlockedOnceActiveResultExists()
    {
        using var db = NewDb();
        var (fpSec, fpUser, testDef, a1, _, _, _, _, order, _, _, sstRun) = SetupScenario(db, sampleReplicates: 2);
        var engine = TestServiceFactory.TestWorkflow(db);
        var sstService = TestServiceFactory.SystemSuitability(db);

        var payload = new StandardComparisonPayload(
            DateTime.UtcNow,
            sstRun.EquipmentId,
            new List<StandardComparisonPreparationInput> { new(100m, 100m, null), new(100m, 100m, null) },
            new List<StandardComparisonResponseInput> { new(a1.Id, 1, 2000000m), new(a1.Id, 2, 2000000m) },
            "ValidPassword123!");

        await engine.RecordStandardComparisonResultAsync(order.Id, payload, fpUser.Id);

        // Create a second passed SST run
        var sstRun2 = new SystemSuitabilityRun
        {
            Code = "SC S.S 02/092026",
            TestDefinitionId = testDef.Id,
            SectionId = fpSec.Id,
            EquipmentId = sstRun.EquipmentId,
            ChromatographyColumnId = sstRun.ChromatographyColumnId,
            ReferenceStandardMaterialId = sstRun.ReferenceStandardMaterialId,
            StandardWeightMg = 50.0m,
            TheoreticalWeightMg = 50.0m,
            MoisturePercent = 0.2m,
            StandardDilution = 100.0m,
            StandardPurityPercent = 99.5m,
            StandardMeanArea = 2000000m,
            Passed = true,
            PerformedByUserId = fpUser.Id,
            PerformedAt = DateTime.UtcNow,
            SignatureId = sstRun.SignatureId
        };
        db.SystemSuitabilityRuns.Add(sstRun2);
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sstService.LinkTestOrdersAsync(sstRun2.Id, new[] { order.Id }, fpUser.Id));

        Assert.Contains("active result already exists", ex.Message);
    }

    [Fact]
    public async Task RecordResult_MultiAnalyte_RecordsOneParameterResultPerAnalyte()
    {
        using var db = NewDb();
        var (fpSec, fpUser, testDef, a1, a2, _, _, _, order, _, _, sstRun) = SetupScenario(db, sampleReplicates: 2, isMultiAnalyte: true);
        var engine = TestServiceFactory.TestWorkflow(db);

        var payload = new StandardComparisonPayload(
            DateTime.UtcNow,
            sstRun.EquipmentId,
            new List<StandardComparisonPreparationInput>
            {
                new(100.0m, 100.0m, null),
                new(100.0m, 100.0m, null)
            },
            new List<StandardComparisonResponseInput>
            {
                new(a1.Id, 1, 2000000m),
                new(a1.Id, 2, 2000000m),
                new(a2.Id, 1, 1500000m),
                new(a2.Id, 2, 1500000m)
            },
            "ValidPassword123!");

        var result = await engine.RecordStandardComparisonResultAsync(order.Id, payload, fpUser.Id);
        Assert.NotNull(result);

        var savedAnalysis = await db.TestAnalyses
            .Include(a => a.ParameterResults)
                .ThenInclude(p => p.Readings)
            .FirstOrDefaultAsync(a => a.TestOrderId == order.Id && a.IsActive);

        Assert.NotNull(savedAnalysis);
        Assert.Equal(2, savedAnalysis.ParameterResults.Count);

        var p1 = savedAnalysis.ParameterResults.First(p => p.ParameterName == "Paracetamol Assay");
        var p2 = savedAnalysis.ParameterResults.First(p => p.ParameterName == "Caffeine Assay");

        Assert.Equal(2, p1.Readings.Count);
        Assert.Equal(2, p2.Readings.Count);
        Assert.Null(p1.ValidityRecordItemId);
        Assert.Null(p2.ValidityRecordItemId);
    }
}
