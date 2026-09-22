using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

[Collection("PostgresDatabaseCollection")]
public class StandardComparisonPostgresIntegrationTests
{
    private readonly PostgresTestFixture _fixture;

    public StandardComparisonPostgresIntegrationTests(PostgresTestFixture fixture)
    {
        _fixture = fixture;
    }

    [PostgresFact]
    public async Task RecordStandardComparisonResult_PostgresRoundTrip_SavesAnalysisResultsReadingsAndBlocksRelink()
    {
        var methodAbbr = "SC" + Guid.NewGuid().ToString("N")[..4].ToUpperInvariant();
        await using var db = _fixture.CreateDbContext();

        var section = await db.DocumentSections.FirstOrDefaultAsync(s => s.Code == "FP")
            ?? await db.DocumentSections.FirstAsync(s => s.Code == "MICRO");

        var user = await db.Users.FirstAsync(u => u.Id == _fixture.SeededUserId);
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword("IntegrationPassword123!");
        user.IsActive = true;

        var testDef = new TestDefinition
        {
            Code = $"{methodAbbr}_ASSAY",
            DisplayName = $"{methodAbbr} Standard Comparison Assay",
            SectionId = section.Id,
            WorkflowType = WorkflowType.StandardComparison,
            EquationType = EquationType.StandardComparison,
            RequiresSystemSuitability = true,
            MethodAbbreviation = methodAbbr,
            HplcMaxPreparationRsdPercent = 2.0m,
            IsActive = true
        };
        db.TestDefinitions.Add(testDef);

        var stage = new ProductionStage
        {
            Name = $"Stage {methodAbbr}",
            Role = ProductionStageRole.Finished,
            IsActive = true
        };
        db.ProductionStages.Add(stage);
        await db.SaveChangesAsync();

        var stageReplicate = new TestDefinitionStageReplicate
        {
            TestDefinitionId = testDef.Id,
            Role = ProductionStageRole.Finished,
            StandardReplicates = 5,
            SampleReplicates = 2
        };
        db.TestDefinitionStageReplicates.Add(stageReplicate);

        var equip = new Equipment
        {
            Name = $"HPLC {methodAbbr}",
            Code = $"EQ-{methodAbbr}",
            Type = EquipmentType.Hplc,
            SectionId = section.Id,
            CdsSoftware = CdsSoftware.ShimadzuLabSolutions
        };
        db.Equipment.Add(equip);

        var col = new ChromatographyColumn
        {
            Name = $"Column {methodAbbr}",
            Code = $"COL-{methodAbbr}",
            SerialNumber = $"SN-{methodAbbr}",
            SectionId = section.Id,
            IsActive = true,
            CreatedByUserId = _fixture.SeededUserId,
            LastModifiedByUserId = _fixture.SeededUserId
        };
        db.ChromatographyColumns.Add(col);

        var stdMat = new Material
        {
            MaterialName = $"Standard {methodAbbr}",
            MaterialType = MaterialType.ReferenceStandard,
            ManufacturerName = "USP",
            BatchNumber = $"LOT-{methodAbbr}",
            ReceivingDate = DateTime.UtcNow.AddDays(-10),
            ExpiryDate = DateTime.UtcNow.AddYears(1),
            QuantityReceived = 100m,
            QuantityRemaining = 100m,
            Unit = MaterialUnit.Gram,
            Location = "Standard Storage",
            SectionId = section.Id,
            Purity = 99.8m,
            CreatedByUserId = _fixture.SeededUserId,
            LastModifiedByUserId = _fixture.SeededUserId
        };
        db.Materials.Add(stdMat);
        await db.SaveChangesAsync();

        var analyte = new TestAnalyte
        {
            TestDefinitionId = testDef.Id,
            Element = "Ascorbic Acid",
            WavelengthNm = 245.0m,
            DisplayOrder = 1,
            IsActive = true
        };
        db.TestAnalytes.Add(analyte);

        var item = new Item
        {
            Code = $"ITEM_{methodAbbr}",
            Name = $"Vitamin C Tablets {methodAbbr}",
            Category = SampleCategory.FinishedProduct,
            IsActive = true
        };
        db.Items.Add(item);
        await db.SaveChangesAsync();

        var spec = new Specification
        {
            ItemId = item.Id,
            TestCode = testDef.Code,
            ParameterName = "Vitamin C Assay",
            TestAnalyteId = analyte.Id,
            LimitType = LimitType.Range,
            LowerLimit = 90.0m,
            UpperLimit = 110.0m,
            LowerInclusive = true,
            UpperInclusive = true,
            DisplayOrder = 1
        };
        db.Specifications.Add(spec);

        var cause = await db.CausesOfTesting.FirstOrDefaultAsync()
            ?? db.CausesOfTesting.Add(new CauseOfTesting { Name = "Routine", IsActive = true }).Entity;

        var sample = new Sample
        {
            ReferenceNumber = $"SMP_{methodAbbr}",
            ItemId = item.Id,
            Category = SampleCategory.FinishedProduct,
            ReceivedAt = DateTime.UtcNow.AddDays(-1),
            CauseOfTesting = cause,
            ReceivedByUserId = _fixture.SeededUserId,
            ProductionStageId = stage.Id,
            Status = SampleStatus.InTesting
        };
        db.Samples.Add(sample);
        await db.SaveChangesAsync();

        var order = new TestOrder
        {
            SampleId = sample.Id,
            TestCode = testDef.Code,
            SectionId = section.Id,
            CurrentStep = WorkflowStep.Running,
            Status = ApprovalStatus.InProgress
        };
        db.TestOrders.Add(order);
        await db.SaveChangesAsync();

        var signature = new ElectronicSignature
        {
            UserId = _fixture.SeededUserId,
            UserFullNameSnapshot = user.FullName,
            UsernameSnapshot = user.Username,
            RoleSnapshot = "Analyst",
            MeaningOfSignature = SignatureMeaning.SuitabilityRunPerformed,
            SignedAt = DateTime.UtcNow,
            EntityType = "SystemSuitabilityRun",
            EntityId = 0
        };
        db.ElectronicSignatures.Add(signature);
        await db.SaveChangesAsync();

        var sstRun = new SystemSuitabilityRun
        {
            Code = $"{methodAbbr} S.S 01/092026",
            TestDefinitionId = testDef.Id,
            SectionId = section.Id,
            EquipmentId = equip.Id,
            ChromatographyColumnId = col.Id,
            ReferenceStandardMaterialId = stdMat.Id,
            StandardWeightMg = 50.2m,
            TheoreticalWeightMg = 50.0m,
            MoisturePercent = 0.5m,
            StandardDilution = 100.0m,
            StandardPurityPercent = 99.8m,
            StandardMeanArea = 2500000m,
            Passed = true,
            PerformedByUserId = _fixture.SeededUserId,
            PerformedAt = DateTime.UtcNow,
            SignatureId = signature.Id
        };
        db.SystemSuitabilityRuns.Add(sstRun);
        await db.SaveChangesAsync();

        var runAnalyte = new SystemSuitabilityRunAnalyte
        {
            SystemSuitabilityRunId = sstRun.Id,
            TestAnalyteId = analyte.Id,
            AnalyteName = analyte.Element,
            WavelengthNm = analyte.WavelengthNm,
            ReferenceStandardMaterialId = stdMat.Id,
            StandardWeightMg = 50.2m,
            TheoreticalWeightMg = 50.0m,
            MoisturePercent = 0.5m,
            StandardDilution = 100.0m,
            StandardPurityPercent = 99.8m,
            StandardMeanArea = 2500000m,
            Passed = true
        };
        db.SystemSuitabilityRunAnalytes.Add(runAnalyte);

        order.SystemSuitabilityRunId = sstRun.Id;
        await db.SaveChangesAsync();

        // Perform record result via TestWorkflowEngine
        var engine = TestServiceFactory.TestWorkflow(db);

        var payload = new StandardComparisonPayload(
            DateTime.UtcNow,
            equip.Id,
            new List<StandardComparisonPreparationInput>
            {
                new(100.0m, 100.5m, null),
                new(100.0m, 99.8m, null)
            },
            new List<StandardComparisonResponseInput>
            {
                new(analyte.Id, 1, 2490000m),
                new(analyte.Id, 2, 2515000m)
            },
            "IntegrationPassword123!",
            "Standard comparison integration test record.");

        var res = await engine.RecordStandardComparisonResultAsync(order.Id, payload, _fixture.SeededUserId);
        Assert.NotNull(res);
        Assert.Equal("WithinLimits", res.Status);

        // Verification on fresh DbContext from Postgres
        await using var verifyDb = _fixture.CreateDbContext();

        var savedAnalysis = await verifyDb.TestAnalyses
            .Include(a => a.ParameterResults)
                .ThenInclude(p => p.Readings)
            .FirstOrDefaultAsync(a => a.TestOrderId == order.Id && a.IsActive);

        Assert.NotNull(savedAnalysis);
        Assert.Equal(WorkflowType.StandardComparison, savedAnalysis.AnalysisType);
        Assert.Equal(equip.Id, savedAnalysis.EquipmentId);
        Assert.Equal("SystemSuitabilityRun", savedAnalysis.ValidityRecordType);
        Assert.Equal(sstRun.Id, savedAnalysis.ValidityRecordId);
        Assert.Equal("Standard comparison integration test record.", savedAnalysis.Comment);

        Assert.Single(savedAnalysis.ParameterResults);
        var param = savedAnalysis.ParameterResults[0];
        Assert.Equal("Vitamin C Assay", param.ParameterName);
        Assert.Equal("%", param.Unit);
        Assert.Equal("WithinLimits", param.ComparisonStatus);
        Assert.Equal(2, param.Readings.Count);

        // Verify calculation JSON
        var calcData = JsonSerializer.Deserialize<StandardComparisonCalculationData>(
            param.CalculationJson!,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(calcData);
        Assert.Equal(50.0m, calcData.StandardTheoreticalWeightMg);
        Assert.Equal(50.2m, calcData.StandardActualWeightMg);
        Assert.Equal(0.5m, calcData.MoisturePercent);
        Assert.Equal(99.8m, calcData.StandardPurityPercent);
        Assert.Equal(2500000m, calcData.StandardMeanArea);
        Assert.Equal(2, calcData.Preparations.Count);

        // Relink guard check on Postgres
        var sstRun2 = new SystemSuitabilityRun
        {
            Code = $"{methodAbbr} S.S 02/092026",
            TestDefinitionId = testDef.Id,
            SectionId = section.Id,
            EquipmentId = equip.Id,
            ChromatographyColumnId = col.Id,
            ReferenceStandardMaterialId = stdMat.Id,
            StandardWeightMg = 50.0m,
            TheoreticalWeightMg = 50.0m,
            MoisturePercent = 0.5m,
            StandardDilution = 100.0m,
            StandardPurityPercent = 99.8m,
            StandardMeanArea = 2500000m,
            Passed = true,
            PerformedByUserId = _fixture.SeededUserId,
            PerformedAt = DateTime.UtcNow,
            SignatureId = signature.Id
        };
        verifyDb.SystemSuitabilityRuns.Add(sstRun2);
        await verifyDb.SaveChangesAsync();

        var sstService = TestServiceFactory.SystemSuitability(verifyDb);
        var relinkEx = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sstService.LinkTestOrdersAsync(sstRun2.Id, new[] { order.Id }, _fixture.SeededUserId));

        Assert.Contains("active result already exists", relinkEx.Message);
    }
}
