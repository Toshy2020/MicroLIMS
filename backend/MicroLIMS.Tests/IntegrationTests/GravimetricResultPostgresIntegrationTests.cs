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
public class GravimetricResultPostgresIntegrationTests
{
    private readonly PostgresTestFixture _fixture;

    public GravimetricResultPostgresIntegrationTests(PostgresTestFixture fixture)
    {
        _fixture = fixture;
    }

    [PostgresFact]
    public async Task RecordGravimetricResult_PostgresEndToEnd_CreatesSignedAnalysisAndResults()
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
            Code = $"LOD_TEST_{uid}",
            DisplayName = $"Loss on Drying Test {uid}",
            SectionId = section.Id,
            WorkflowType = WorkflowType.Gravimetric,
            EquationType = EquationType.GravimetricLoss,
            ReplicateCount = 1,
            UsesTare = true,
            ConditionFields = "Temperature (°C), Time (h)",
            IsActive = true
        };
        db.TestDefinitions.Add(testDef);
        await db.SaveChangesAsync();

        var equip = new Equipment
        {
            Name = $"Drying Oven {uid}",
            Code = $"EQ-OVN-{uid}",
            Type = EquipmentType.Oven,
            SectionId = section.Id
        };
        db.Equipment.Add(equip);

        var item = new Item
        {
            Name = $"Granule Formulation {uid}",
            Code = $"ITEM-G-{uid}",
            Category = SampleCategory.FinishedProduct,
            IsActive = true
        };
        db.Items.Add(item);
        await db.SaveChangesAsync();

        var spec = new Specification
        {
            ItemId = item.Id,
            TestCode = testDef.Code,
            ParameterName = "Loss on Drying",
            LimitType = LimitType.NotMoreThan,
            UpperLimit = 5.0m,
            Unit = "%",
            SpecLimit = "NMT 5.0%",
            DisplayOrder = 1
        };
        db.Specifications.Add(spec);

        var cause = await db.CausesOfTesting.FirstOrDefaultAsync()
            ?? db.CausesOfTesting.Add(new CauseOfTesting { Name = "Routine Testing", IsActive = true }).Entity;

        var sample = new Sample
        {
            ReferenceNumber = $"SMP-G-{uid}",
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

        var payload = new GravimetricPayload(
            AnalysedAt: DateTime.UtcNow,
            EquipmentId: equip.Id,
            Conditions: new Dictionary<string, string>
            {
                ["Temperature (°C)"] = "105",
                ["Time (h)"] = "3"
            },
            Parameters: new List<GravimetricParameterInput>
            {
                new(spec.Id, new List<GravimetricReplicateInput>
                {
                    new(Container: 25.1234m, Initial: 27.1234m, Final: 27.0334m)
                })
            },
            Password: "IntegrationPassword123!",
            Comment: "Postgres e2e Gravimetric");

        var result = await engine.RecordGravimetricResultAsync(order.Id, payload, _fixture.SeededUserId, "127.0.0.1");

        Assert.Equal("WithinLimits", result.Status);
        Assert.Contains("4.50 %", result.OutcomeSummary);

        // Verify persistence in Postgres
        await using var verifyDb = _fixture.CreateDbContext();
        var savedAnalysis = await verifyDb.TestAnalyses
            .Include(e => e.Signature)
            .Include(e => e.ParameterResults)
                .ThenInclude(pr => pr.Readings)
            .FirstOrDefaultAsync(e => e.TestOrderId == order.Id);

        Assert.NotNull(savedAnalysis);
        Assert.Equal(WorkflowType.Gravimetric, savedAnalysis.AnalysisType);
        Assert.Equal(equip.Id, savedAnalysis.EquipmentId);
        Assert.True(savedAnalysis.IsActive);
        Assert.NotNull(savedAnalysis.ConditionsJson);

        using var condDoc = JsonDocument.Parse(savedAnalysis.ConditionsJson!);
        Assert.Equal("105", condDoc.RootElement.GetProperty("Temperature (°C)").GetString());
        Assert.Equal("3", condDoc.RootElement.GetProperty("Time (h)").GetString());

        Assert.NotNull(savedAnalysis.Signature);
        Assert.Equal(SignatureMeaning.ResultRecorded, savedAnalysis.Signature.MeaningOfSignature);
        Assert.Equal("TestOrder", savedAnalysis.Signature.EntityType);
        Assert.Equal(order.Id, savedAnalysis.Signature.EntityId);

        Assert.Single(savedAnalysis.ParameterResults);
        var savedParam = savedAnalysis.ParameterResults[0];
        Assert.Equal(spec.Id, savedParam.SpecificationId);
        Assert.Equal("4.50 %", savedParam.ReportedDisplay);
        Assert.Equal("WithinLimits", savedParam.ComparisonStatus);
        Assert.True(savedParam.IsActive);
        Assert.NotNull(savedParam.CalculationJson);

        using var calc = JsonDocument.Parse(savedParam.CalculationJson!);
        Assert.Equal("GravimetricLoss", calc.RootElement.GetProperty("mode").GetString());
        Assert.Equal(4.50m, calc.RootElement.GetProperty("mean").GetDecimal());
        Assert.Equal(1, calc.RootElement.GetProperty("n").GetInt32());

        Assert.Single(savedParam.Readings);
        var reading = savedParam.Readings[0];
        Assert.Equal(ReadingKind.Weight, reading.Kind);
        Assert.Equal(1, reading.Index);
        Assert.Equal(25.1234m, reading.Value1);
        Assert.Equal(27.1234m, reading.Value2);
        Assert.Equal(27.0334m, reading.Value3);
        Assert.Equal(4.50m, reading.ComputedValue);

        // Verify generic Result row
        var genericResult = await verifyDb.Results.FirstOrDefaultAsync(r => r.TestOrderId == order.Id);
        Assert.NotNull(genericResult);
        Assert.Contains("Loss on Drying=", genericResult.RawValue);
        Assert.Contains("4.50 % (WithinLimits)", genericResult.InterpretedValue);
        Assert.Equal(ResultType.Numeric, genericResult.Type);

        // Verify TestOrder status
        var verifiedOrder = await verifyDb.TestOrders.FindAsync(order.Id);
        Assert.NotNull(verifiedOrder);
        Assert.Equal(ApprovalStatus.ResultEntered, verifiedOrder.Status);

        // Verify projected ResultRecord
        var projected = await verifyDb.ResultRecords.FirstOrDefaultAsync(r => r.TestOrderId == order.Id && r.SourceTable == "ParameterResult" && r.SourceId == savedParam.Id);
        Assert.NotNull(projected);
        Assert.Equal("4.50 %", projected.ReportedValue);
    }
}
