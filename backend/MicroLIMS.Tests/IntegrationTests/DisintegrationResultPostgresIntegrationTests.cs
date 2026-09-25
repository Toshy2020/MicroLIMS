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
public class DisintegrationResultPostgresIntegrationTests
{
    private readonly PostgresTestFixture _fixture;

    public DisintegrationResultPostgresIntegrationTests(PostgresTestFixture fixture)
    {
        _fixture = fixture;
    }

    [PostgresFact]
    public async Task Disintegration_PostgresEndToEnd_Stage1NextStageRequired_Stage2CompliesAndFinalized()
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
            Code = $"DISINT_{uid}",
            DisplayName = $"Disintegration Test {uid}",
            SectionId = section.Id,
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
        await db.SaveChangesAsync();

        var equip = new Equipment
        {
            Name = $"Disintegration Tester {uid}",
            Code = $"EQ-DT-{uid}",
            Type = EquipmentType.DisintegrationTester,
            SectionId = section.Id
        };
        db.Equipment.Add(equip);

        var item = new Item
        {
            Code = $"ITM-DT-{uid}",
            Name = $"Disintegration Item {uid}",
            Category = SampleCategory.FinishedProduct,
            IsActive = true
        };
        db.Items.Add(item);
        await db.SaveChangesAsync();

        var spec = new Specification
        {
            ItemId = item.Id,
            TestCode = testDef.Code,
            ParameterName = "Disintegration",
            LimitType = LimitType.DisintegrationTime,
            UpperLimit = 30m,
            Unit = "min",
            SpecLimit = "NMT 30 min",
            ConversionFactor = 1.0m
        };
        db.Specifications.Add(spec);

        var cause = await db.CausesOfTesting.FirstOrDefaultAsync()
            ?? db.CausesOfTesting.Add(new CauseOfTesting { Name = "Routine Testing", IsActive = true }).Entity;

        var sample = new Sample
        {
            ReferenceNumber = $"SMP-DT-{uid}",
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

        // Stage 1: 5 pass (<= 30), 1 fail (32 > 30) -> 1 failure -> NextStageRequired
        var payloadStage1 = new DisintegrationPayload(
            AnalysedAt: DateTime.UtcNow,
            EquipmentId: equip.Id,
            Conditions: new Dictionary<string, string>
            {
                ["Medium"] = "Purified Water",
                ["Temperature"] = "37 ± 2 °C"
            },
            UnitMinutes: new List<decimal?> { 12m, 14m, 15m, 13m, 16m, 32m },
            Password: "IntegrationPassword123!",
            Comment: "Postgres e2e Stage 1");

        var result1 = await engine.RecordDisintegrationResultAsync(order.Id, payloadStage1, _fixture.SeededUserId, "127.0.0.1");

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
        Assert.Equal(WorkflowType.Disintegration, savedAnalysis1.AnalysisType);
        Assert.True(savedAnalysis1.IsActive);
        Assert.Single(savedAnalysis1.ParameterResults);
        var pr1 = savedAnalysis1.ParameterResults[0];
        Assert.Equal(1, pr1.StageReached);
        Assert.Equal("NextStageRequired", pr1.ComparisonStatus);
        Assert.Equal(6, pr1.Readings.Count);

        // Stage 2: append 12 units (all <= 30 min) -> 17 pass, 1 fail -> 17 >= 16 -> Complies!
        var payloadStage2 = new DisintegrationStagePayload(
            UnitMinutes: new List<decimal?> { 15m, 16m, 14m, 18m, 20m, 22m, 19m, 17m, 16m, 15m, 14m, 18m },
            Password: "IntegrationPassword123!",
            Comment: "Postgres e2e Stage 2");

        var result2 = await engine.RecordDisintegrationStageAsync(order.Id, payloadStage2, _fixture.SeededUserId, "127.0.0.1");

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
        Assert.Contains("Disintegration=", genericResult.RawValue);
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
        Assert.Equal(32m, pr2.ReportedValue);
        Assert.Equal(18, pr2.Readings.Count);

        using var calcDoc = JsonDocument.Parse(pr2.CalculationJson!);
        Assert.Equal("Complies", calcDoc.RootElement.GetProperty("outcome").GetString());
        Assert.Equal(30m, calcDoc.RootElement.GetProperty("limitMinutes").GetDecimal());
        Assert.Equal(17, calcDoc.RootElement.GetProperty("passedCount").GetInt32());
        Assert.Equal(32m, calcDoc.RootElement.GetProperty("longestMinutes").GetDecimal());
    }
}
