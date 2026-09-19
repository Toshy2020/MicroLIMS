using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

[Collection("PostgresDatabaseCollection")]
public class MeasurementResultPostgresIntegrationTests
{
    private readonly PostgresTestFixture _fixture;

    public MeasurementResultPostgresIntegrationTests(PostgresTestFixture fixture)
    {
        _fixture = fixture;
    }

    [PostgresFact]
    public async Task RecordMeasurementResult_PostgresEndToEnd_CreatesSignedAnalysisAndResults()
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
            Code = $"PH_TEST_{uid}",
            DisplayName = $"pH Measurement Test {uid}",
            SectionId = section.Id,
            WorkflowType = WorkflowType.Measurement,
            EquationType = EquationType.Measurement,
            ReplicateCount = 3,
            EvaluationBasis = MeasurementEvaluationBasis.Mean,
            IsActive = true
        };
        db.TestDefinitions.Add(testDef);
        await db.SaveChangesAsync();

        var equip = new Equipment
        {
            Name = $"pH Meter {uid}",
            Code = $"EQ-PH-{uid}",
            Type = EquipmentType.PhMeter,
            SectionId = section.Id
        };
        db.Equipment.Add(equip);

        var item = new Item
        {
            Name = $"Tablet Formulation {uid}",
            Code = $"ITEM-M-{uid}",
            Category = SampleCategory.FinishedProduct,
            IsActive = true
        };
        db.Items.Add(item);
        await db.SaveChangesAsync();

        var spec = new Specification
        {
            ItemId = item.Id,
            TestCode = testDef.Code,
            ParameterName = "pH",
            LimitType = LimitType.TargetWithTolerance,
            Target = 6.0m,
            Tolerance = 0.5m,
            ToleranceMode = ToleranceMode.Absolute,
            SpecLimit = "6.0 ± 0.5",
            DisplayOrder = 1
        };
        db.Specifications.Add(spec);

        var cause = await db.CausesOfTesting.FirstOrDefaultAsync()
            ?? db.CausesOfTesting.Add(new CauseOfTesting { Name = "Routine Testing", IsActive = true }).Entity;

        var sample = new Sample
        {
            ReferenceNumber = $"SMP-M-{uid}",
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

        var payload = new MeasurementPayload(
            AnalysedAt: DateTime.UtcNow,
            EquipmentId: equip.Id,
            Parameters: new List<MeasurementParameterInput>
            {
                new(spec.Id, new List<decimal> { 6.02m, 6.05m, 5.98m })
            },
            Password: "IntegrationPassword123!",
            Comment: "Postgres e2e Measurement");

        var result = await engine.RecordMeasurementResultAsync(order.Id, payload, _fixture.SeededUserId, "127.0.0.1");

        Assert.Equal("WithinLimits", result.Status);
        Assert.Contains("6.02", result.OutcomeSummary);

        // Verify persistence in Postgres
        await using var verifyDb = _fixture.CreateDbContext();
        var savedAnalysis = await verifyDb.TestAnalyses
            .Include(e => e.Signature)
            .Include(e => e.ParameterResults)
                .ThenInclude(pr => pr.Readings)
            .FirstOrDefaultAsync(e => e.TestOrderId == order.Id);

        Assert.NotNull(savedAnalysis);
        Assert.Equal(WorkflowType.Measurement, savedAnalysis.AnalysisType);
        Assert.Equal(equip.Id, savedAnalysis.EquipmentId);
        Assert.True(savedAnalysis.IsActive);
        Assert.NotNull(savedAnalysis.Signature);
        Assert.Equal(SignatureMeaning.ResultRecorded, savedAnalysis.Signature.MeaningOfSignature);
        Assert.Equal("TestOrder", savedAnalysis.Signature.EntityType);
        Assert.Equal(order.Id, savedAnalysis.Signature.EntityId);

        Assert.Single(savedAnalysis.ParameterResults);
        var savedParam = savedAnalysis.ParameterResults[0];
        Assert.Equal(spec.Id, savedParam.SpecificationId);
        Assert.Equal("6.02", savedParam.ReportedDisplay);
        Assert.Equal("WithinLimits", savedParam.ComparisonStatus);
        Assert.True(savedParam.IsActive);
        Assert.NotNull(savedParam.CalculationJson);
        // jsonb re-formats the stored text, so compare parsed values.
        using var calc = System.Text.Json.JsonDocument.Parse(savedParam.CalculationJson!);
        Assert.True(calc.RootElement.TryGetProperty("mean", out _));
        Assert.Equal(0.0351188458m, calc.RootElement.GetProperty("sd").GetDecimal());
        Assert.Equal("Mean", calc.RootElement.GetProperty("basis").GetString());

        Assert.Equal(3, savedParam.Readings.Count);
        Assert.All(savedParam.Readings, r => Assert.Equal(ReadingKind.Replicate, r.Kind));
        Assert.Equal(new[] { 1, 2, 3 }, savedParam.Readings.OrderBy(r => r.Index).Select(r => r.Index));
        Assert.Equal(new decimal[] { 6.02m, 6.05m, 5.98m }, savedParam.Readings.OrderBy(r => r.Index).Select(r => r.Value1!.Value));

        // Verify generic Result row
        var genericResult = await verifyDb.Results.FirstOrDefaultAsync(r => r.TestOrderId == order.Id);
        Assert.NotNull(genericResult);
        Assert.Contains("pH=", genericResult.RawValue);
        Assert.Contains("6.02 (WithinLimits)", genericResult.InterpretedValue);

        // Verify TestOrder status
        var verifiedOrder = await verifyDb.TestOrders.FindAsync(order.Id);
        Assert.NotNull(verifiedOrder);
        Assert.Equal(ApprovalStatus.ResultEntered, verifiedOrder.Status);

        // Verify projected ResultRecord
        var projected = await verifyDb.ResultRecords.FirstOrDefaultAsync(r => r.TestOrderId == order.Id && r.SourceTable == "ParameterResult" && r.SourceId == savedParam.Id);
        Assert.NotNull(projected);
        Assert.Equal("6.02", projected.ReportedValue);
    }
}
