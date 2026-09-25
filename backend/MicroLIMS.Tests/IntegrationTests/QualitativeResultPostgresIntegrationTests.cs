using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

[Collection("PostgresDatabaseCollection")]
public class QualitativeResultPostgresIntegrationTests
{
    private readonly PostgresTestFixture _fixture;

    public QualitativeResultPostgresIntegrationTests(PostgresTestFixture fixture)
    {
        _fixture = fixture;
    }

    [PostgresFact]
    public async Task RecordQualitativeResult_PostgresEndToEnd_CreatesSignedAnalysisAndResults()
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
            Code = $"QUAL_TEST_{uid}",
            DisplayName = $"Appearance Test {uid}",
            SectionId = section.Id,
            WorkflowType = WorkflowType.Qualitative,
            EquationType = EquationType.Qualitative,
            IsActive = true
        };
        db.TestDefinitions.Add(testDef);
        await db.SaveChangesAsync();

        var item = new Item
        {
            Name = $"Liquid Formulation {uid}",
            Code = $"ITEM-Q-{uid}",
            Category = SampleCategory.FinishedProduct,
            IsActive = true
        };
        db.Items.Add(item);
        await db.SaveChangesAsync();

        var spec = new Specification
        {
            ItemId = item.Id,
            TestCode = testDef.Code,
            ParameterName = "Appearance",
            LimitType = LimitType.Qualitative,
            SpecLimit = "Clear colourless liquid",
            DisplayOrder = 1
        };
        db.Specifications.Add(spec);

        var cause = await db.CausesOfTesting.FirstOrDefaultAsync()
            ?? db.CausesOfTesting.Add(new CauseOfTesting { Name = "Routine Testing", IsActive = true }).Entity;

        var sample = new Sample
        {
            ReferenceNumber = $"SMP-Q-{uid}",
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

        var payload = new QualitativePayload(
            AnalysedAt: DateTime.UtcNow,
            EquipmentId: null,
            Parameters: new List<QualitativeParameterInput>
            {
                new(spec.Id, Conforms: true, Observation: "Clear colourless liquid, odourless")
            },
            Password: "IntegrationPassword123!",
            Comment: "Postgres e2e Qualitative");

        var result = await engine.RecordQualitativeResultAsync(order.Id, payload, _fixture.SeededUserId, "127.0.0.1");

        Assert.Equal("WithinLimits", result.Status);
        Assert.Contains("Complies", result.OutcomeSummary);

        // Verify persistence in Postgres
        await using var verifyDb = _fixture.CreateDbContext();
        var savedAnalysis = await verifyDb.TestAnalyses
            .Include(e => e.Signature)
            .Include(e => e.ParameterResults)
                .ThenInclude(pr => pr.Readings)
            .FirstOrDefaultAsync(e => e.TestOrderId == order.Id);

        Assert.NotNull(savedAnalysis);
        Assert.Equal(WorkflowType.Qualitative, savedAnalysis.AnalysisType);
        Assert.Null(savedAnalysis.EquipmentId);
        Assert.True(savedAnalysis.IsActive);
        Assert.Null(savedAnalysis.ConditionsJson);

        Assert.NotNull(savedAnalysis.Signature);
        Assert.Equal(SignatureMeaning.ResultRecorded, savedAnalysis.Signature.MeaningOfSignature);
        Assert.Equal("TestOrder", savedAnalysis.Signature.EntityType);
        Assert.Equal(order.Id, savedAnalysis.Signature.EntityId);

        Assert.Single(savedAnalysis.ParameterResults);
        var savedParam = savedAnalysis.ParameterResults[0];
        Assert.Equal(spec.Id, savedParam.SpecificationId);
        Assert.Null(savedParam.ReportedValue);
        Assert.Equal("Complies", savedParam.ReportedDisplay);
        Assert.Equal("WithinLimits", savedParam.ComparisonStatus);
        Assert.Equal("Clear colourless liquid", savedParam.SpecLimit);
        Assert.True(savedParam.IsActive);
        Assert.NotNull(savedParam.CalculationJson);

        using var calc = JsonDocument.Parse(savedParam.CalculationJson!);
        Assert.True(calc.RootElement.GetProperty("conforms").GetBoolean());
        Assert.Equal("Clear colourless liquid", calc.RootElement.GetProperty("expected").GetString());

        Assert.Single(savedParam.Readings);
        var reading = savedParam.Readings[0];
        Assert.Equal(ReadingKind.Replicate, reading.Kind);
        Assert.Equal(1, reading.Index);
        Assert.Equal("Clear colourless liquid, odourless", reading.Text);
        Assert.True(reading.Passed);

        // Verify generic Result row
        var genericResult = await verifyDb.Results.FirstOrDefaultAsync(r => r.TestOrderId == order.Id);
        Assert.NotNull(genericResult);
        Assert.Contains("Appearance=Complies", genericResult.RawValue);
        Assert.Contains("Complies (WithinLimits)", genericResult.InterpretedValue);
        Assert.Equal(ResultType.Interpretive, genericResult.Type);

        // Verify TestOrder status
        var verifiedOrder = await verifyDb.TestOrders.FindAsync(order.Id);
        Assert.NotNull(verifiedOrder);
        Assert.Equal(ApprovalStatus.ResultEntered, verifiedOrder.Status);

        // Verify projected ResultRecord
        var projected = await verifyDb.ResultRecords.FirstOrDefaultAsync(r => r.TestOrderId == order.Id && r.SourceTable == "ParameterResult" && r.SourceId == savedParam.Id);
        Assert.NotNull(projected);
        Assert.Equal("Complies", projected.ReportedValue);
    }
}
