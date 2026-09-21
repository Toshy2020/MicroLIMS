using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

[Collection("PostgresDatabaseCollection")]
public class WeightVariationResultPostgresIntegrationTests
{
    private readonly PostgresTestFixture _fixture;

    public WeightVariationResultPostgresIntegrationTests(PostgresTestFixture fixture)
    {
        _fixture = fixture;
    }

    [PostgresFact]
    public async Task WeightVariation_PostgresEndToEnd_Stage1NextStageRequired_Stage2CompliesAndFinalized()
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
            Code = $"WV_{uid}",
            DisplayName = $"Weight Variation Test {uid}",
            SectionId = section.Id,
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
        await db.SaveChangesAsync();

        var equip = new Equipment
        {
            Name = $"Analytical Balance {uid}",
            Code = $"EQ-BAL-{uid}",
            Type = EquipmentType.Balance,
            SectionId = section.Id
        };
        db.Equipment.Add(equip);

        var item = new Item
        {
            Code = $"ITM-WV-{uid}",
            Name = $"Weight Variation Item {uid}",
            Category = SampleCategory.FinishedProduct,
            IsActive = true
        };
        db.Items.Add(item);
        await db.SaveChangesAsync();

        var spec = new Specification
        {
            ItemId = item.Id,
            TestCode = testDef.Code,
            ParameterName = "Weight Variation",
            LimitType = LimitType.WeightVariation,
            DosageForm = DosageForm.HardCapsule,
            Unit = "mg",
            SpecLimit = "USP <2091>: net content 90-110 % of average",
            ConversionFactor = 1.0m
        };
        db.Specifications.Add(spec);

        var cause = await db.CausesOfTesting.FirstOrDefaultAsync()
            ?? db.CausesOfTesting.Add(new CauseOfTesting { Name = "Routine Testing", IsActive = true }).Entity;

        var sample = new Sample
        {
            ReferenceNumber = $"SMP-WV-{uid}",
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
            Status = ApprovalStatus.InProgress
        };
        db.TestOrders.Add(order);
        await db.SaveChangesAsync();

        var engine = TestServiceFactory.TestWorkflow(db);

        // Stage 1: 20 units. Fails Step A, 4 units outside 10% (dev 12.5% > 10%) -> NextStageRequired
        var units1 = new List<WeightVariationUnitPayload>
        {
            new(GrossMg: 700m, ShellMg: 300m), // net 400
            new(GrossMg: 500m, ShellMg: 100m), // net 400
            new(GrossMg: 600m, ShellMg: 150m), // net 450 (dev 12.5%)
            new(GrossMg: 600m, ShellMg: 150m), // net 450 (dev 12.5%)
            new(GrossMg: 600m, ShellMg: 250m), // net 350 (dev 12.5%)
            new(GrossMg: 600m, ShellMg: 250m)  // net 350 (dev 12.5%)
        };
        for (int i = 0; i < 14; i++) units1.Add(new(GrossMg: 600m, ShellMg: 200m)); // net 400

        var payloadStage1 = new WeightVariationPayload(
            AnalysedAt: DateTime.UtcNow,
            EquipmentId: equip.Id,
            Conditions: new Dictionary<string, string>
            {
                ["Balance ID"] = "BAL-01",
                ["Temperature"] = "22 °C"
            },
            Units: units1,
            Password: "IntegrationPassword123!",
            Comment: "Postgres e2e Stage 1");

        var result1 = await engine.RecordWeightVariationResultAsync(order.Id, payloadStage1, _fixture.SeededUserId, "127.0.0.1");

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
        Assert.Equal(WorkflowType.WeightVariation, savedAnalysis1.AnalysisType);
        Assert.True(savedAnalysis1.IsActive);
        Assert.Single(savedAnalysis1.ParameterResults);
        var pr1 = savedAnalysis1.ParameterResults[0];
        Assert.Equal(1, pr1.StageReached);
        Assert.Equal("NextStageRequired", pr1.ComparisonStatus);
        Assert.Equal(20, pr1.Readings.Count);

        // Stage 2: append 40 units (all net 400) -> 4 outside 10% (<= 6) -> Complies!
        var units2 = new List<WeightVariationUnitPayload>();
        for (int i = 0; i < 40; i++) units2.Add(new(GrossMg: 600m, ShellMg: 200m));

        var payloadStage2 = new WeightVariationStagePayload(
            Units: units2,
            Password: "IntegrationPassword123!",
            Comment: "Postgres e2e Stage 2");

        var result2 = await engine.RecordWeightVariationStageAsync(order.Id, payloadStage2, _fixture.SeededUserId, "127.0.0.1");

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
        Assert.Contains("Weight Variation=", genericResult.RawValue);
        Assert.Contains("Complies (WithinLimits)", genericResult.InterpretedValue);
        Assert.Equal(ResultType.Numeric, genericResult.Type);

        // Projected ResultRecord exists
        var projected = await verifyDb2.ResultRecords.FirstOrDefaultAsync(r => r.TestOrderId == order.Id && r.SourceTable == "ParameterResult");
        Assert.NotNull(projected);
        Assert.Equal("Complies", projected.ReportedValue);

        // Verify Analysis & CalculationJson via JsonDocument
        var savedAnalysis2 = await verifyDb2.TestAnalyses
            .Include(a => a.ParameterResults)
                .ThenInclude(pr => pr.Readings)
            .FirstOrDefaultAsync(a => a.TestOrderId == order.Id);
        Assert.NotNull(savedAnalysis2);
        var pr2 = savedAnalysis2.ParameterResults[0];
        Assert.Equal(2, pr2.StageReached);
        Assert.Equal("WithinLimits", pr2.ComparisonStatus);
        Assert.Equal("Complies", pr2.ReportedDisplay);
        Assert.Equal(400m, pr2.ReportedValue);
        Assert.Equal(60, pr2.Readings.Count);

        using var calcDoc = JsonDocument.Parse(pr2.CalculationJson!);
        Assert.Equal("Complies", calcDoc.RootElement.GetProperty("outcome").GetString());
        Assert.Equal(400m, calcDoc.RootElement.GetProperty("meanWeightMg").GetDecimal());
    }
}
