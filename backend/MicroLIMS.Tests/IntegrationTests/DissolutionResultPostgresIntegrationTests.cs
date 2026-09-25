using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

[Collection("PostgresDatabaseCollection")]
public class DissolutionResultPostgresIntegrationTests
{
    private readonly PostgresTestFixture _fixture;

    public DissolutionResultPostgresIntegrationTests(PostgresTestFixture fixture)
    {
        _fixture = fixture;
    }

    [PostgresFact]
    public async Task Dissolution_PostgresEndToEnd_Stage1NextStageRequired_Stage2CompliesAndFinalized()
    {
        var uid = Guid.NewGuid().ToString("N")[..6];
        await using var db = _fixture.CreateDbContext();

        var section = await db.DocumentSections.FirstOrDefaultAsync(s => s.Code == "FP")
            ?? await db.DocumentSections.FirstAsync(s => s.Code == "MICRO");

        var user = await db.Users.FirstAsync(u => u.Id == _fixture.SeededUserId);
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword("IntegrationPassword123!");
        user.IsActive = true;

        var testDef = new TestDefinition
        {
            Code = $"DISS_TEST_{uid}",
            DisplayName = $"Dissolution Test {uid}",
            SectionId = section.Id,
            WorkflowType = WorkflowType.Dissolution,
            EquationType = EquationType.Dissolution,
            RequiresSystemSuitability = true, MethodAbbreviation = "DIS", SstMaxRsdPercent = 2.0m,
            DissolutionS1Offset = 5m,
            DissolutionS2MinOffset = 15m,
            DissolutionS3MinOffset = 25m,
            DissolutionS3MaxBelowS2Min = 2m,
            ConditionFields = "Medium, RPM",
            IsActive = true
        };
        db.TestDefinitions.Add(testDef);
        await db.SaveChangesAsync();

        var equip = new Equipment
        {
            Name = $"Dissolution Tester {uid}",
            Code = $"EQ-DIS-{uid}",
            Type = EquipmentType.DissolutionTester,
            SectionId = section.Id
        };
        db.Equipment.Add(equip);

        // The suitability run is performed on the HPLC; the vessels run in the dissolution tester.
        var hplc = new Equipment
        {
            Name = $"HPLC {uid}",
            Code = $"EQ-HPLC-DIS-{uid}",
            Type = EquipmentType.Hplc,
            SectionId = section.Id
        };
        db.Equipment.Add(hplc);

        var col = new ChromatographyColumn
        {
            Name = $"Dissolution Column {uid}",
            Code = $"COL-DIS-{uid}",
            SerialNumber = $"SN-DIS-{uid}",
            SectionId = section.Id,
            IsActive = true,
            CreatedByUserId = _fixture.SeededUserId,
            LastModifiedByUserId = _fixture.SeededUserId
        };
        db.ChromatographyColumns.Add(col);

        var standard = new Material
        {
            MaterialName = $"Dissolution Standard {uid}",
            MaterialType = MaterialType.ReferenceStandard,
            ManufacturerName = "USP",
            BatchNumber = $"LOT-DIS-{uid}",
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
        db.Materials.Add(standard);
        await db.SaveChangesAsync();

        // Create suitability run
        var sstService = TestServiceFactory.SystemSuitability(db);
        var sstRequest = new CreateSystemSuitabilityRunRequest(
            TestDefinitionId: testDef.Id,
            EquipmentId: hplc.Id,
            ChromatographyColumnId: col.Id,
            ReferenceStandardMaterialId: standard.Id,
            StandardWeightMg: 50.0m,
            StandardDilution: 2500.0m, // C_s = 0.02 mg/mL
            StandardMeanArea: 0.500m,
            RsdPercent: 1.0m,
            Resolution: 2.5m,
            TailingFactor: 1.1m,
            TheoreticalPlates: 5000m,
            Password: "IntegrationPassword123!",
            Comment: "Integration suitability run");

        var run = await sstService.CreateAsync(sstRequest, _fixture.SeededUserId, "127.0.0.1");
        Assert.True(run.Passed);

        var item = new Item
        {
            Name = $"Tablets {uid}",
            Code = $"ITEM-DIS-{uid}",
            Category = SampleCategory.FinishedProduct,
            IsActive = true
        };
        db.Items.Add(item);
        await db.SaveChangesAsync();

        var spec = new Specification
        {
            ItemId = item.Id,
            TestCode = testDef.Code,
            ParameterName = "Dissolution",
            LimitType = LimitType.DissolutionQ,
            LowerLimit = 80.0m, // Q = 80 %
            LabelClaim = 18.0m, // LC = 18 mg
            LabelClaimUnit = "mg",
            Unit = "%",
            SpecLimit = "Q = 80 %",
            DisplayOrder = 1
        };
        db.Specifications.Add(spec);

        var cause = await db.CausesOfTesting.FirstOrDefaultAsync()
            ?? db.CausesOfTesting.Add(new CauseOfTesting { Name = "Routine Testing", IsActive = true }).Entity;

        var sample = new Sample
        {
            ReferenceNumber = $"SMP-DIS-{uid}",
            Category = SampleCategory.FinishedProduct,
            ItemId = item.Id,
            ReceivedAt = DateTime.UtcNow,
            CauseOfTesting = cause,
            ReceivedByUserId = _fixture.SeededUserId,
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
            Status = ApprovalStatus.InProgress,
            SystemSuitabilityRunId = run.Id
        };
        db.TestOrders.Add(order);
        await db.SaveChangesAsync();

        var engine = TestServiceFactory.TestWorkflow(db);

        // Stage 1: 6 vessel areas. One vessel at 0.4200 (84% < Q+5=85%). 5 vessels at 0.4500 (90%).
        // % = A_u * 0.02 * 900 * 1 * 100 / (0.500 * 18) = A_u * 200
        var payloadStage1 = new DissolutionPayload(
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
            Password: "IntegrationPassword123!",
            Comment: "Postgres e2e Stage 1");

        var result1 = await engine.RecordDissolutionResultAsync(order.Id, payloadStage1, _fixture.SeededUserId, "127.0.0.1");

        Assert.Equal("NextStageRequired", result1.Status);
        Assert.False(result1.IsDefinitive);
        Assert.False(result1.AllStepsComplete);

        // Verify in Postgres: order not finalized, stays Running
        await using var verifyDb1 = _fixture.CreateDbContext();
        var verifiedOrder1 = await verifyDb1.TestOrders.FindAsync(order.Id);
        Assert.NotNull(verifiedOrder1);
        Assert.Equal(WorkflowStep.Running, verifiedOrder1.CurrentStep);
        Assert.Equal(ApprovalStatus.InProgress, verifiedOrder1.Status);

        // No generic Result row written yet
        var noResultRow = await verifyDb1.Results.FirstOrDefaultAsync(r => r.TestOrderId == order.Id);
        Assert.Null(noResultRow);

        // TestAnalysis exists with StageReached = 1
        var savedAnalysis1 = await verifyDb1.TestAnalyses
            .Include(a => a.ParameterResults)
                .ThenInclude(pr => pr.Readings)
            .FirstOrDefaultAsync(a => a.TestOrderId == order.Id);
        Assert.NotNull(savedAnalysis1);
        Assert.Equal(WorkflowType.Dissolution, savedAnalysis1.AnalysisType);
        Assert.True(savedAnalysis1.IsActive);
        Assert.Single(savedAnalysis1.ParameterResults);
        var pr1 = savedAnalysis1.ParameterResults[0];
        Assert.Equal(1, pr1.StageReached);
        Assert.Equal("NextStageRequired", pr1.ComparisonStatus);
        Assert.Equal(6, pr1.Readings.Count);

        // Stage 2: append 6 vessel areas (all at 0.4500 -> 90.0%)
        // Mean = (84 + 11*90)/12 = 89.5% >= 80, none < 65 -> Complies!
        var payloadStage2 = new DissolutionStagePayload(
            VesselAreas: new List<decimal> { 0.4500m, 0.4500m, 0.4500m, 0.4500m, 0.4500m, 0.4500m },
            Password: "IntegrationPassword123!",
            Comment: "Postgres e2e Stage 2");

        var result2 = await engine.RecordDissolutionStageAsync(order.Id, payloadStage2, _fixture.SeededUserId, "127.0.0.1");

        Assert.Equal("WithinLimits", result2.Status);
        Assert.True(result2.IsDefinitive);
        Assert.True(result2.AllStepsComplete);

        // Verify in Postgres: order finalized to Ready
        await using var verifyDb2 = _fixture.CreateDbContext();
        var verifiedOrder2 = await verifyDb2.TestOrders.FindAsync(order.Id);
        Assert.NotNull(verifiedOrder2);
        Assert.Equal(WorkflowStep.Ready, verifiedOrder2.CurrentStep);
        Assert.Equal(ApprovalStatus.ResultEntered, verifiedOrder2.Status);

        // Generic Result row exists
        var genericResult = await verifyDb2.Results.FirstOrDefaultAsync(r => r.TestOrderId == order.Id);
        Assert.NotNull(genericResult);
        Assert.Contains("Dissolution=", genericResult.RawValue);
        Assert.Contains("90 % (WithinLimits)", genericResult.InterpretedValue);
        Assert.Equal(ResultType.Numeric, genericResult.Type);

        // Projected ResultRecord exists
        var projected = await verifyDb2.ResultRecords.FirstOrDefaultAsync(r => r.TestOrderId == order.Id && r.SourceTable == "ParameterResult");
        Assert.NotNull(projected);
        Assert.Equal("90 %", projected.ReportedValue);

        // Verify Analysis & CalculationJson via JsonDocument
        var savedAnalysis2 = await verifyDb2.TestAnalyses
            .Include(a => a.ParameterResults)
                .ThenInclude(pr => pr.Readings)
            .FirstOrDefaultAsync(a => a.TestOrderId == order.Id);
        Assert.NotNull(savedAnalysis2);
        var pr2 = savedAnalysis2.ParameterResults[0];
        Assert.Equal(2, pr2.StageReached);
        Assert.Equal("WithinLimits", pr2.ComparisonStatus);
        Assert.Equal(12, pr2.Readings.Count);

        using var calcDoc = JsonDocument.Parse(pr2.CalculationJson!);
        Assert.Equal("Complies", calcDoc.RootElement.GetProperty("outcome").GetString());
        Assert.Equal(89.5m, calcDoc.RootElement.GetProperty("mean").GetDecimal());
        Assert.Equal(0.02m, calcDoc.RootElement.GetProperty("cs").GetDecimal());
        Assert.Equal(80m, calcDoc.RootElement.GetProperty("q").GetDecimal());
        Assert.Equal(900m, calcDoc.RootElement.GetProperty("v").GetDecimal());
        Assert.Equal(18m, calcDoc.RootElement.GetProperty("lc").GetDecimal());
    }
}
