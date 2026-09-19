using System.Globalization;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

[Collection("PostgresDatabaseCollection")]
public class HplcAssayResultPostgresIntegrationTests
{
    private readonly PostgresTestFixture _fixture;

    public HplcAssayResultPostgresIntegrationTests(PostgresTestFixture fixture)
    {
        _fixture = fixture;
    }

    [PostgresFact]
    public async Task RecordHplcAssayResult_PostgresEndToEnd_CreatesSignedResultAndProjection()
    {
        var methodAbbr = "H" + Guid.NewGuid().ToString("N")[..4].ToUpperInvariant();
        await using var db = _fixture.CreateDbContext();

        var section = await db.DocumentSections.FirstOrDefaultAsync(s => s.Code == "FP")
            ?? await db.DocumentSections.FirstAsync(s => s.Code == "MICRO");

        var user = await db.Users.FirstAsync(u => u.Id == _fixture.SeededUserId);
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword("IntegrationPassword123!");
        user.IsActive = true;

        var testDef = new TestDefinition
        {
            Code = $"{methodAbbr}_TEST",
            DisplayName = $"{methodAbbr} HPLC Test",
            SectionId = section.Id,
            WorkflowType = WorkflowType.HplcAssay,
            EquationType = EquationType.HplcAssay,
            RequiresSystemSuitability = true,
            MethodAbbreviation = methodAbbr,
            SstMaxRsdPercent = 2.0m,
            IsActive = true
        };
        db.TestDefinitions.Add(testDef);

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

        var standard = new Material
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
            Purity = 99.5m,
            CreatedByUserId = _fixture.SeededUserId,
            LastModifiedByUserId = _fixture.SeededUserId
        };
        db.Materials.Add(standard);

        await db.SaveChangesAsync();

        // Create suitability run through the signed service
        var sstService = TestServiceFactory.SystemSuitability(db);
        var sstRequest = new CreateSystemSuitabilityRunRequest(
            TestDefinitionId: testDef.Id,
            EquipmentId: equip.Id,
            ChromatographyColumnId: col.Id,
            ReferenceStandardMaterialId: standard.Id,
            StandardWeightMg: 50.0m,
            StandardDilution: 100.0m,
            StandardMeanArea: 1000m,
            RsdPercent: 1.0m,
            Resolution: 2.5m,
            TailingFactor: 1.1m,
            TheoreticalPlates: 5000m,
            Password: "IntegrationPassword123!",
            Comment: "Integration suitability run");

        var run = await sstService.CreateAsync(sstRequest, _fixture.SeededUserId, "127.0.0.1");
        Assert.True(run.Passed);

        // Seed Sample, Item with specification, and TestOrder
        var item = new Item
        {
            Code = $"ITEM_{methodAbbr}",
            Name = $"Item {methodAbbr}",
            IsActive = true
        };
        db.Items.Add(item);
        await db.SaveChangesAsync();

        db.Specifications.Add(new Specification
        {
            ItemId = item.Id,
            TestCode = testDef.Code,
            SpecLimit = "90.0-110.0 %",
            Unit = "%"
        });

        var cause = await db.CausesOfTesting.FirstOrDefaultAsync() ?? db.CausesOfTesting.Add(new CauseOfTesting { Name = "Routine", IsActive = true }).Entity;
        var sample = new Sample
        {
            CauseOfTesting = cause,
            ReceivedByUserId = _fixture.SeededUserId,
            ReferenceNumber = $"SMP-{methodAbbr}",
            ItemId = item.Id,
            BatchNumber = $"BATCH-{methodAbbr}",
            ControlNumber = $"CTL-{methodAbbr}",
            Category = SampleCategory.FinishedProduct,
            Status = SampleStatus.InTesting,
            ReceivedAt = DateTime.UtcNow
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

        // Record HPLC result via TestWorkflowEngine on Postgres
        var engine = TestServiceFactory.TestWorkflow(db);
        var payload = new HplcAssayPayload(
            SampleWeightMg: 50.0m,
            SampleDilution: 100.0m,
            SampleAreas: new List<decimal> { 1000m, 1005m, 995m },
            Password: "IntegrationPassword123!",
            Comment: "Postgres e2e HPLC assay");

        var result = await engine.RecordHplcAssayResultAsync(order.Id, payload, _fixture.SeededUserId, "127.0.0.1");

        Assert.Equal("99.5 %", result.OutcomeSummary);
        Assert.Equal("WithinLimits", result.Status);

        // Verify persistence in Postgres
        await using var verifyDb = _fixture.CreateDbContext();
        var savedHplc = await verifyDb.HplcAssayResults
            .Include(h => h.Signature)
            .FirstOrDefaultAsync(h => h.TestOrderId == order.Id);

        Assert.NotNull(savedHplc);
        Assert.Equal(99.5m, savedHplc.MeanAssayPercent);
        Assert.Equal("99.5 %", savedHplc.ReportedResult);
        Assert.True(savedHplc.IsActive);
        Assert.NotNull(savedHplc.Signature);
        Assert.Equal(SignatureMeaning.ResultRecorded, savedHplc.Signature.MeaningOfSignature);
        Assert.Equal("TestOrder", savedHplc.Signature.EntityType);
        Assert.Equal(order.Id, savedHplc.Signature.EntityId);

        // Verify ResultRecord quantitative projection
        var record = await verifyDb.ResultRecords.FirstOrDefaultAsync(r => r.SourceTable == "HplcAssayResult" && r.SourceId == savedHplc.Id);
        Assert.NotNull(record);
        Assert.Equal(ResultKind.Quantitative, record.ResultKind);
        Assert.Equal(99.5m, record.NumericValue);
        Assert.Equal("%", record.Unit);
        Assert.Equal(ResultLevel.WithinLimit, record.ResultLevel);

        // Verify generic Result row
        var genericResult = await verifyDb.Results.FirstOrDefaultAsync(r => r.TestOrderId == order.Id);
        Assert.NotNull(genericResult);
        Assert.Contains("99.5 %", genericResult.InterpretedValue);

        // Sample summary carries the raw data behind the result (translates on Postgres)
        var summary = await TestServiceFactory.SampleSummary(verifyDb).GetSummaryAsync(sample.Id);
        var hplc = Assert.Single(summary!.TestOrders, t => t.TestOrderId == order.Id).HplcAssay;
        Assert.NotNull(hplc);
        Assert.Equal(run.Code, hplc.SuitabilityRunCode);
        Assert.True(hplc.SuitabilityPassed);
        Assert.Equal(1000m, hplc.StandardMeanArea);
        Assert.Equal(50.0m, hplc.StandardWeightMg);
        Assert.Equal(100.0m, hplc.StandardDilution);
        Assert.Equal(equip.Code, hplc.EquipmentCode);
        Assert.Equal(col.Code, hplc.ColumnCode);
        Assert.Equal(1.0m, hplc.RsdPercent);
        Assert.Equal(3, hplc.Replicates.Count);
        Assert.NotEqual("Unknown", hplc.EnteredByName);
        Assert.NotNull(await TestServiceFactory.SampleSummary(verifyDb).GenerateSummaryPdfAsync(sample.Id));
    }
}
