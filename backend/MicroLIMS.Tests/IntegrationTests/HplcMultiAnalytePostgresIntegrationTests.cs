using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.API.Controllers;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Responses;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

[Collection("PostgresDatabaseCollection")]
public class HplcMultiAnalytePostgresIntegrationTests
{
    private static readonly string YearStr = DateTime.UtcNow.ToString("yyyy", CultureInfo.InvariantCulture);
    private static readonly string Mm = DateTime.UtcNow.ToString("MM", CultureInfo.InvariantCulture);
    private readonly PostgresTestFixture _fixture;

    public HplcMultiAnalytePostgresIntegrationTests(PostgresTestFixture fixture)
    {
        _fixture = fixture;
    }

    [PostgresFact]
    public async Task CreateHplcMultiAnalyteSuitabilityRun_PostgresRoundTrip_SavesAnalytesAndPreventsDelete()
    {
        var methodAbbr = "M" + Guid.NewGuid().ToString("N")[..4].ToUpperInvariant();
        await using var db = _fixture.CreateDbContext();

        var section = await db.DocumentSections.FirstOrDefaultAsync(s => s.Code == "FP")
            ?? await db.DocumentSections.FirstAsync(s => s.Code == "MICRO");

        var user = await db.Users.FirstAsync(u => u.Id == _fixture.SeededUserId);
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword("IntegrationPassword123!");
        user.IsActive = true;

        var testDef = new TestDefinition
        {
            Code = $"{methodAbbr}_TEST",
            DisplayName = $"{methodAbbr} Multi-Analyte Test",
            SectionId = section.Id,
            WorkflowType = WorkflowType.HplcMultiAnalyte,
            EquationType = EquationType.HplcMultiAnalyte,
            RequiresSystemSuitability = true,
            MethodAbbreviation = methodAbbr,
            HplcPreparations = 2,
            HplcInjectionsPerPreparation = 2,
            HplcMaxPreparationRsdPercent = 2.0m,
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

        var std1 = new Material
        {
            MaterialName = $"Std1 {methodAbbr}",
            MaterialType = MaterialType.ReferenceStandard,
            ManufacturerName = "USP",
            BatchNumber = $"LOT-1-{methodAbbr}",
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
        var std2 = new Material
        {
            MaterialName = $"Std2 {methodAbbr}",
            MaterialType = MaterialType.ReferenceStandard,
            ManufacturerName = "USP",
            BatchNumber = $"LOT-2-{methodAbbr}",
            ReceivingDate = DateTime.UtcNow.AddDays(-10),
            ExpiryDate = DateTime.UtcNow.AddYears(1),
            QuantityReceived = 100m,
            QuantityRemaining = 100m,
            Unit = MaterialUnit.Gram,
            Location = "Standard Storage",
            SectionId = section.Id,
            Purity = 98.8m,
            CreatedByUserId = _fixture.SeededUserId,
            LastModifiedByUserId = _fixture.SeededUserId
        };
        db.Materials.AddRange(std1, std2);
        await db.SaveChangesAsync();

        var b1 = new TestAnalyte
        {
            TestDefinitionId = testDef.Id,
            Element = "Vitamin B1",
            WavelengthNm = 254.0m,
            DisplayOrder = 1,
            IsActive = true,
            SstMaxRsdPercent = 2.0m,
            SstMaxTailingFactor = 2.0m,
            SstMinTheoreticalPlates = 2000m
        };
        var b2 = new TestAnalyte
        {
            TestDefinitionId = testDef.Id,
            Element = "Vitamin B2",
            WavelengthNm = 267.0m,
            DisplayOrder = 2,
            IsActive = true,
            SstMaxRsdPercent = 2.0m,
            SstMinResolution = 2.0m,
            SstMaxTailingFactor = 2.0m,
            SstMinTheoreticalPlates = 2500m
        };
        db.TestAnalytes.AddRange(b1, b2);
        await db.SaveChangesAsync();

        // Create suitability run through service
        var sstService = TestServiceFactory.SystemSuitability(db);
        var sstRequest = new CreateSystemSuitabilityRunRequest(
            TestDefinitionId: testDef.Id,
            EquipmentId: equip.Id,
            ChromatographyColumnId: col.Id,
            ReferenceStandardMaterialId: std1.Id,
            Password: "IntegrationPassword123!",
            Comment: "Postgres multi-analyte run",
            Analytes: new List<CreateSystemSuitabilityRunAnalyteRequest>
            {
                new(b1.Id, std1.Id, 50.0m, 100.0m, 2500000m, 1.0m, null, 1.1m, 3000m),
                new(b2.Id, std2.Id, 45.0m, 100.0m, 1800000m, 1.2m, 2.5m, 1.2m, 3200m)
            });

        var run = await sstService.CreateAsync(sstRequest, _fixture.SeededUserId, "127.0.0.1");

        Assert.NotNull(run);
        Assert.True(run.Passed);
        Assert.Equal($"{methodAbbr} S.S 01/{Mm}{YearStr}", run.Code);
        Assert.Equal(2, run.Analytes.Count);

        // Verify round trip from fresh DbContext
        await using var verifyDb = _fixture.CreateDbContext();
        var storedRun = await verifyDb.SystemSuitabilityRuns
            .Include(r => r.Analytes)
            .FirstOrDefaultAsync(r => r.Id == run.Id);

        Assert.NotNull(storedRun);
        Assert.True(storedRun.Passed);
        Assert.Equal(2, storedRun.Analytes.Count);

        var storedB1 = storedRun.Analytes.First(a => a.TestAnalyteId == b1.Id);
        Assert.Equal("Vitamin B1", storedB1.AnalyteName);
        Assert.Equal(254.0m, storedB1.WavelengthNm);
        Assert.Equal(99.5m, storedB1.StandardPurityPercent);
        Assert.Equal(50.0m, storedB1.StandardWeightMg);
        Assert.Equal(100.0m, storedB1.StandardDilution);
        Assert.Equal(2500000m, storedB1.StandardMeanArea);
        Assert.True(storedB1.Passed);

        var storedB2 = storedRun.Analytes.First(a => a.TestAnalyteId == b2.Id);
        Assert.Equal("Vitamin B2", storedB2.AnalyteName);
        Assert.Equal(267.0m, storedB2.WavelengthNm);
        Assert.Equal(98.8m, storedB2.StandardPurityPercent);
        Assert.Equal(45.0m, storedB2.StandardWeightMg);
        Assert.Equal(100.0m, storedB2.StandardDilution);
        Assert.Equal(1800000m, storedB2.StandardMeanArea);
        Assert.True(storedB2.Passed);

        // Verify that deleting an analyte referenced by the suitability run deactivates it in Postgres
        var scope = new UserSectionScopeService(verifyDb);
        var colService = new ChromatographyColumnService(verifyDb, scope);
        var masterDataController = new MasterDataController(
            verifyDb,
            new EquipmentConfigurationService(verifyDb),
            TestServiceFactory.MediaProduct(verifyDb),
            TestServiceFactory.MediaIncubationCondition(verifyDb),
            scope,
            colService)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new System.Security.Claims.ClaimsPrincipal(
                        new System.Security.Claims.ClaimsIdentity(
                            new[] { new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, user.Id.ToString()) },
                            "TestAuth"))
                }
            }
        };

        var deleteRes = await masterDataController.DeleteTestAnalyte(testDef.Id, b1.Id);
        Assert.IsType<OkObjectResult>(deleteRes);

        await using var checkDb = _fixture.CreateDbContext();
        var deactivatedB1 = await checkDb.TestAnalytes.FindAsync(b1.Id);
        Assert.NotNull(deactivatedB1);
        Assert.False(deactivatedB1.IsActive);
    }

    [PostgresFact]
    public async Task RecordHplcMultiAnalyteResult_PostgresRoundTrip_SavesResultsReadingsAndBlocksRelink()
    {
        var methodAbbr = "N" + Guid.NewGuid().ToString("N")[..4].ToUpperInvariant();
        await using var db = _fixture.CreateDbContext();

        var section = await db.DocumentSections.FirstOrDefaultAsync(s => s.Code == "FP")
            ?? await db.DocumentSections.FirstAsync(s => s.Code == "MICRO");

        var user = await db.Users.FirstAsync(u => u.Id == _fixture.SeededUserId);
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword("IntegrationPassword123!");
        user.IsActive = true;

        var testDef = new TestDefinition
        {
            Code = $"{methodAbbr}_TEST",
            DisplayName = $"{methodAbbr} Multivitamin Assay",
            SectionId = section.Id,
            WorkflowType = WorkflowType.HplcMultiAnalyte,
            EquationType = EquationType.HplcMultiAnalyte,
            RequiresSystemSuitability = true,
            MethodAbbreviation = methodAbbr,
            HplcPreparations = 2,
            HplcInjectionsPerPreparation = 2,
            HplcMaxPreparationRsdPercent = 2.0m,
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

        Material Standard(string name, decimal purity) => new()
        {
            MaterialName = $"{name} {methodAbbr}",
            MaterialType = MaterialType.ReferenceStandard,
            ManufacturerName = "USP",
            BatchNumber = $"LOT-{name}-{methodAbbr}",
            ReceivingDate = DateTime.UtcNow.AddDays(-10),
            ExpiryDate = DateTime.UtcNow.AddYears(1),
            QuantityReceived = 100m,
            QuantityRemaining = 100m,
            Unit = MaterialUnit.Gram,
            Location = "Standard Storage",
            SectionId = section.Id,
            Purity = purity,
            CreatedByUserId = _fixture.SeededUserId,
            LastModifiedByUserId = _fixture.SeededUserId
        };
        var std1 = Standard("Thiamine", 99.5m);
        var std2 = Standard("Riboflavin", 98.8m);
        db.Materials.AddRange(std1, std2);
        await db.SaveChangesAsync();

        var b1 = new TestAnalyte { TestDefinitionId = testDef.Id, Element = "Vitamin B1", WavelengthNm = 254.0m, DisplayOrder = 1, IsActive = true };
        var b2 = new TestAnalyte { TestDefinitionId = testDef.Id, Element = "Vitamin B2", WavelengthNm = 267.0m, DisplayOrder = 2, IsActive = true };
        db.TestAnalytes.AddRange(b1, b2);

        var item = new Item { Code = $"ITEM_{methodAbbr}", Name = $"Multivitamin {methodAbbr}", Category = SampleCategory.FinishedProduct, IsActive = true };
        db.Items.Add(item);
        await db.SaveChangesAsync();

        db.SampleTests.Add(new SampleTest { ItemId = item.Id, TestCode = testDef.Code, DisplayName = testDef.DisplayName });

        // B1 reported as % label claim; B2 in µg per unit (ConversionFactor 1000 converts mg to µg).
        var specB1 = new Specification
        {
            ItemId = item.Id, TestCode = testDef.Code, ParameterName = "Vitamin B1", TestAnalyteId = b1.Id,
            SampleMatrix = SampleMatrix.Solid, ResultBasis = ResultBasis.PercentLabelClaim,
            LabelClaim = 5.0m, LabelClaimUnit = "mg", ConversionFactor = 1.0m,
            LimitType = LimitType.Range, LowerLimit = 90m, UpperLimit = 110m, Unit = "%", DisplayOrder = 1
        };
        var specB2 = new Specification
        {
            ItemId = item.Id, TestCode = testDef.Code, ParameterName = "Vitamin B2", TestAnalyteId = b2.Id,
            SampleMatrix = SampleMatrix.Solid, ResultBasis = ResultBasis.MgPerUnit,
            LabelClaim = 4500m, LabelClaimUnit = "µg", ConversionFactor = 1000m,
            LimitType = LimitType.Range, LowerLimit = 4000m, UpperLimit = 5000m, Unit = "µg", DisplayOrder = 2
        };
        SpecificationService.ApplyCanonicalSpecLimit(specB1);
        SpecificationService.ApplyCanonicalSpecLimit(specB2);
        db.Specifications.AddRange(specB1, specB2);

        var cause = await db.CausesOfTesting.FirstOrDefaultAsync()
            ?? db.CausesOfTesting.Add(new CauseOfTesting { Name = "Routine", IsActive = true }).Entity;
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
            Status = ApprovalStatus.InProgress
        };
        db.TestOrders.Add(order);
        await db.SaveChangesAsync();

        // C_s(B1) = 50 x 0.995 / 100 = 0.4975 mg/mL; C_s(B2) = 45 x 0.988 / 100 = 0.4446 mg/mL
        var sstService = TestServiceFactory.SystemSuitability(db);
        var run = await sstService.CreateAsync(new CreateSystemSuitabilityRunRequest(
            TestDefinitionId: testDef.Id,
            EquipmentId: equip.Id,
            ChromatographyColumnId: col.Id,
            ReferenceStandardMaterialId: std1.Id,
            Password: "IntegrationPassword123!",
            Comment: "Postgres multi-analyte result run",
            Analytes: new List<CreateSystemSuitabilityRunAnalyteRequest>
            {
                new(b1.Id, std1.Id, 50.0m, 100.0m, 2500000m, null, null, null, null),
                new(b2.Id, std2.Id, 45.0m, 100.0m, 1800000m, null, null, null, null)
            }), _fixture.SeededUserId, "127.0.0.1");
        Assert.True(run.Passed);

        await sstService.LinkTestOrdersAsync(run.Id, new[] { order.Id }, _fixture.SeededUserId);

        // Sample 250 mg in 50 mL, unit weight 500 mg -> factor 100. Area ratio 0.1 for every injection:
        // B1 = 0.1 x 0.4975 x 100 = 4.975 mg -> 99.5 % of 5 mg; B2 = 0.1 x 0.4446 x 100 = 4.446 mg -> 4446 µg.
        var engine = TestServiceFactory.TestWorkflow(db);
        var payload = new HplcMultiAnalytePayload(
            AnalysedAt: DateTime.UtcNow,
            EquipmentId: equip.Id,
            SampleMatrix: SampleMatrix.Solid,
            Preparations: new List<HplcPreparationInput> { new(250m, 50m), new(250m, 50m) },
            UnitAmount: 500m,
            Areas: new List<HplcAreaInput>
            {
                new(b1.Id, 1, 1, 250000m), new(b1.Id, 1, 2, 250000m), new(b1.Id, 2, 1, 250000m), new(b1.Id, 2, 2, 250000m),
                new(b2.Id, 1, 1, 180000m), new(b2.Id, 1, 2, 180000m), new(b2.Id, 2, 1, 180000m), new(b2.Id, 2, 2, 180000m)
            },
            Password: "IntegrationPassword123!",
            Comment: "Postgres e2e multi-analyte");

        var result = await engine.RecordHplcMultiAnalyteResultAsync(order.Id, payload, _fixture.SeededUserId, "127.0.0.1");
        Assert.Equal("WithinLimits", result.Status);

        await using var verifyDb = _fixture.CreateDbContext();
        var saved = await verifyDb.TestAnalyses
            .Include(a => a.Signature)
            .Include(a => a.ParameterResults).ThenInclude(pr => pr.Readings)
            .SingleAsync(a => a.TestOrderId == order.Id && a.IsActive);

        Assert.Equal(WorkflowType.HplcMultiAnalyte, saved.AnalysisType);
        Assert.Equal(SampleMatrix.Solid, saved.SampleMatrix);
        Assert.Equal(500m, saved.UnitAmount);
        Assert.Equal("SystemSuitabilityRun", saved.ValidityRecordType);
        Assert.Equal(run.Id, saved.ValidityRecordId);
        Assert.Equal(SignatureMeaning.ResultRecorded, saved.Signature!.MeaningOfSignature);
        Assert.Equal(2, saved.ParameterResults.Count);

        var runAnalytes = await verifyDb.SystemSuitabilityRunAnalytes.Where(a => a.SystemSuitabilityRunId == run.Id).ToListAsync();

        var prB1 = saved.ParameterResults.Single(p => p.SpecificationId == specB1.Id);
        Assert.Equal(99.5m, prB1.ReportedValue);
        Assert.Equal("99.5 %", prB1.ReportedDisplay);
        Assert.Equal("WithinLimits", prB1.ComparisonStatus);
        Assert.Null(prB1.ValidityRecordItemId);
        Assert.Equal(runAnalytes.Single(a => a.TestAnalyteId == b1.Id).Id, prB1.HplcMultiAnalyteCalculation!.SystemSuitabilityRunAnalyteId);
        Assert.Equal(4.975m, prB1.MgPerUnit);
        Assert.Equal("Typed", prB1.HplcMultiAnalyteCalculation!.UnitAmountSource);
        Assert.Equal(4, prB1.Readings.Count);
        Assert.All(prB1.Readings, r =>
        {
            Assert.Equal(ReadingKind.Replicate, r.Kind);
            Assert.Equal(250000m, r.Value1);
            Assert.Equal(4.975m, r.ComputedValue);
        });

        var prB2 = saved.ParameterResults.Single(p => p.SpecificationId == specB2.Id);
        Assert.Equal(4446m, prB2.ReportedValue);
        Assert.Equal("4446.0 µg", prB2.ReportedDisplay);
        Assert.Equal("WithinLimits", prB2.ComparisonStatus);
        Assert.All(prB2.Readings, r => Assert.Equal(4446m, r.ComputedValue));

        var verifiedOrder = await verifyDb.TestOrders.FindAsync(order.Id);
        Assert.Equal(WorkflowStep.Ready, verifiedOrder!.CurrentStep);
        Assert.Equal(ApprovalStatus.ResultEntered, verifiedOrder.Status);

        var projected = await verifyDb.ResultRecords
            .Where(r => r.SourceTable == "ParameterResult" && r.TestOrderId == order.Id)
            .ToListAsync();
        Assert.Equal(2, projected.Count);

        // The run the results were calculated against cannot be relinked.
        var relinkService = TestServiceFactory.SystemSuitability(verifyDb);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            relinkService.LinkTestOrdersAsync(run.Id, new[] { order.Id }, _fixture.SeededUserId));
        Assert.Contains("active", ex.Message);
    }
}
