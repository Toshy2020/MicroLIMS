using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

// SC-4 titration mode: one Postgres round trip covering run creation (null column,
// blank titre stored) and a titration entry recorded against that run.
[Collection("PostgresDatabaseCollection")]
public class StandardComparisonTitrationPostgresIntegrationTests
{
    private readonly PostgresTestFixture _fixture;

    public StandardComparisonTitrationPostgresIntegrationTests(PostgresTestFixture fixture)
    {
        _fixture = fixture;
    }

    [PostgresFact]
    public async Task TitrationRunAndEntry_PostgresRoundTrip_PersistsNullColumnBlankAndReportedValue()
    {
        var methodAbbr = "TT" + Guid.NewGuid().ToString("N")[..4].ToUpperInvariant();
        await using var db = _fixture.CreateDbContext();

        var section = await db.DocumentSections.FirstOrDefaultAsync(s => s.Code == "FP")
            ?? await db.DocumentSections.FirstAsync(s => s.Code == "MICRO");

        var user = await db.Users.FirstAsync(u => u.Id == _fixture.SeededUserId);
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword("IntegrationPassword123!");
        user.IsActive = true;

        var testDef = new TestDefinition
        {
            Code = $"{methodAbbr}_ASSAY",
            DisplayName = $"{methodAbbr} Titration Assay",
            SectionId = section.Id,
            WorkflowType = WorkflowType.StandardComparison,
            EquationType = EquationType.StandardComparison,
            RequiresSystemSuitability = true,
            MethodAbbreviation = methodAbbr,
            ResponseMode = ResponseMode.TitrationVolume,
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
            StandardReplicates = 3,
            SampleReplicates = 1
        };
        db.TestDefinitionStageReplicates.Add(stageReplicate);

        var titrator = new Equipment
        {
            Name = $"Titrator {methodAbbr}",
            Code = $"TTR-{methodAbbr}",
            Type = EquipmentType.Titrator,
            SectionId = section.Id
        };
        db.Equipment.Add(titrator);

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
            Purity = 100m,
            CreatedByUserId = _fixture.SeededUserId,
            LastModifiedByUserId = _fixture.SeededUserId
        };
        db.Materials.Add(stdMat);
        await db.SaveChangesAsync();

        var analyte = new TestAnalyte
        {
            TestDefinitionId = testDef.Id,
            Element = "Ascorbic Acid",
            WavelengthNm = 0m,
            DisplayOrder = 1,
            IsActive = true,
            SstMaxRsdPercent = 2.0m,
            SstMinResolution = 1.5m // configured, must be ignored for titration
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
            ParameterName = "Ascorbic Acid Assay",
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

        // Create the titration suitability run through the real service:
        // no column, blank titre + standard titres supplied per analyte row.
        var sstService = TestServiceFactory.SystemSuitability(db);
        var sstRequest = new CreateSystemSuitabilityRunRequest(
            TestDefinitionId: testDef.Id,
            EquipmentId: titrator.Id,
            ChromatographyColumnId: null,
            Password: "IntegrationPassword123!",
            Comment: "Postgres SC-4 titration run",
            Analytes: new List<CreateSystemSuitabilityRunAnalyteRequest>
            {
                new(
                    TestAnalyteId: analyte.Id,
                    ReferenceStandardMaterialId: stdMat.Id,
                    StandardWeightMg: 50.0m,
                    StandardDilution: 0m,
                    StandardMeanArea: 0m,
                    TheoreticalWeightMg: 50.0m,
                    MoisturePercent: 0m,
                    Responses: new List<decimal> { 10.10m, 10.10m, 10.10m }, // mean = 10.10, RSD = 0%
                    BlankTitreMl: 0.10m)
            });

        var run = await sstService.CreateAsync(sstRequest, _fixture.SeededUserId, "127.0.0.1");

        Assert.NotNull(run);
        Assert.True(run.Passed);
        Assert.Null(run.ChromatographyColumnId);
        var createdRunAnalyte = run.Analytes.Single();
        Assert.Equal(0.10m, createdRunAnalyte.BlankTitreMl);
        Assert.Equal(10.10m, createdRunAnalyte.StandardMeanArea);

        order.SystemSuitabilityRunId = run.Id;
        await db.SaveChangesAsync();

        // Record a titration entry against the run.
        var engine = TestServiceFactory.TestWorkflow(db);
        var payload = new StandardComparisonPayload(
            DateTime.UtcNow,
            titrator.Id,
            new List<StandardComparisonPreparationInput> { new(100.0m, 100.0m, null) },
            new List<StandardComparisonResponseInput> { new(analyte.Id, 1, 9.90m) }, // EP_test
            "IntegrationPassword123!",
            "Titration integration test record.");

        var res = await engine.RecordStandardComparisonResultAsync(order.Id, payload, _fixture.SeededUserId);
        Assert.NotNull(res);
        Assert.Equal("WithinLimits", res.Status);

        // Verify from a fresh DbContext against PostgreSQL.
        await using var verifyDb = _fixture.CreateDbContext();

        var storedRun = await verifyDb.SystemSuitabilityRuns
            .Include(r => r.Analytes)
            .FirstOrDefaultAsync(r => r.Id == run.Id);
        Assert.NotNull(storedRun);
        Assert.Null(storedRun!.ChromatographyColumnId);
        var storedRunAnalyte = storedRun.Analytes.Single();
        Assert.Equal(0.10m, storedRunAnalyte.BlankTitreMl);
        Assert.Equal(10.10m, storedRunAnalyte.StandardMeanArea);

        var savedAnalysis = await verifyDb.TestAnalyses
            .Include(a => a.ParameterResults)
                .ThenInclude(p => p.Readings)
            .FirstOrDefaultAsync(a => a.TestOrderId == order.Id && a.IsActive);
        Assert.NotNull(savedAnalysis);
        Assert.Equal(WorkflowType.StandardComparison, savedAnalysis!.AnalysisType);
        Assert.Equal("SystemSuitabilityRun", savedAnalysis.ValidityRecordType);
        Assert.Equal(run.Id, savedAnalysis.ValidityRecordId);

        var param = savedAnalysis.ParameterResults.Single();
        Assert.Equal("Ascorbic Acid Assay", param.ParameterName);
        Assert.Equal("WithinLimits", param.ComparisonStatus);
        Assert.Single(param.Readings);

        var reading = param.Readings[0];
        Assert.Equal(ReadingKind.Titration, reading.Kind);
        Assert.Equal(9.90m, reading.Value1);

        /*
         HAND CALCULATION:
           blank = 0.10, EP_std (mean) = 10.10 -> EP_std - blank = 10.00
           EP_test = 9.90 -> EP_test - blank = 9.80
           Response ratio = 9.80 / 10.00 = 0.98
           Weight ratios: ActWtStd/ThWtStd = 50.0/50.0 = 1.0; ThWtTest/ActWtTest = 100.0/100.0 = 1.0
           Moisture correction = (100-0)/100 = 1.0; Purity = 100
           %Assay = 0.98 x 1.0 x 1.0 x 1.0 x 100 = 98.0
        */
        Assert.Equal(98.0m, param.ReportedValue);
        Assert.Equal("98.0 %", param.ReportedDisplay);

        var calcData = JsonSerializer.Deserialize<StandardComparisonCalculationData>(
            param.CalculationJson!,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(calcData);
        Assert.Equal("TitrationVolume", calcData!.ResponseMode);
        Assert.Equal(0.10m, calcData.BlankTitreMl);
    }
}
