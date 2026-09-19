using Microsoft.EntityFrameworkCore;
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
public class ElementalAssayResultPostgresIntegrationTests
{
    private readonly PostgresTestFixture _fixture;

    public ElementalAssayResultPostgresIntegrationTests(PostgresTestFixture fixture)
    {
        _fixture = fixture;
    }

    [PostgresFact]
    public async Task RecordElementalAssayResult_PostgresEndToEnd_CreatesSignedEntryAndResults()
    {
        var methodAbbr = "E" + Guid.NewGuid().ToString("N")[..4].ToUpperInvariant();
        await using var db = _fixture.CreateDbContext();

        var section = await db.DocumentSections.FirstOrDefaultAsync(s => s.Code == "FP")
            ?? await db.DocumentSections.FirstAsync(s => s.Code == "MICRO");

        var user = await db.Users.FirstAsync(u => u.Id == _fixture.SeededUserId);
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword("IntegrationPassword123!");
        user.IsActive = true;

        var testDef = new TestDefinition
        {
            Code = $"{methodAbbr}_TEST",
            DisplayName = $"{methodAbbr} ICP-OES Minerals Test",
            SectionId = section.Id,
            WorkflowType = WorkflowType.ElementalAssay,
            EquationType = EquationType.CalibrationCurve,
            MethodAbbreviation = methodAbbr,
            CalibrationEntryMode = CalibrationEntryMode.InstrumentReported,
            CalMinCorrelation = 0.999m,
            CalCorrelationType = CorrelationType.RSquared,
            CalMinStandards = 3,
            CalCheckRecoveryLowPercent = 90.0m,
            CalCheckRecoveryHighPercent = 110.0m,
            CalBlankMax = 0.005m,
            ReportedConcentrationBasis = ReportedConcentrationBasis.SamplePpm,
            CalMaxRunAgeHours = 24,
            IsActive = true
        };
        db.TestDefinitions.Add(testDef);
        await db.SaveChangesAsync();

        var znAnalyte = new TestAnalyte
        {
            TestDefinitionId = testDef.Id,
            Element = "Zn",
            WavelengthNm = 213.857m,
            View = AnalyteView.Axial,
            LoqMgPerL = 0.005m,
            DisplayOrder = 1,
            IsActive = true
        };
        var caAnalyte = new TestAnalyte
        {
            TestDefinitionId = testDef.Id,
            Element = "Ca",
            WavelengthNm = 317.933m,
            View = AnalyteView.Radial,
            LoqMgPerL = 0.010m,
            DisplayOrder = 2,
            IsActive = true
        };
        db.TestAnalytes.AddRange(znAnalyte, caAnalyte);

        var equip = new Equipment
        {
            Name = $"ICP-OES {methodAbbr}",
            Code = $"EQ-ICP-{methodAbbr}",
            Type = EquipmentType.IcpOes,
            SectionId = section.Id,
            Vendor = "PerkinElmer",
            CdsSoftware = CdsSoftware.PerkinElmerSyngistix
        };
        db.Equipment.Add(equip);

        var standard = new Material
        {
            MaterialName = $"ICP Standard {methodAbbr}",
            MaterialType = MaterialType.ReferenceStandard,
            ManufacturerName = "PerkinElmer",
            BatchNumber = $"LOT-{methodAbbr}",
            ReceivingDate = DateTime.UtcNow.AddDays(-10),
            ExpiryDate = DateTime.UtcNow.AddYears(1),
            QuantityReceived = 100m,
            QuantityRemaining = 100m,
            Unit = MaterialUnit.Milliliter,
            Location = "Standard Storage",
            SectionId = section.Id,
            Purity = 99.9m,
            CreatedByUserId = _fixture.SeededUserId,
            LastModifiedByUserId = _fixture.SeededUserId
        };
        db.Materials.Add(standard);
        await db.SaveChangesAsync();

        // Seed calibration run
        var calTime = DateTime.UtcNow.AddHours(-1);
        var run = new CalibrationRun
        {
            Code = $"{methodAbbr} CAL 01/092026",
            TestDefinitionId = testDef.Id,
            SectionId = section.Id,
            EquipmentId = equip.Id,
            CalibrationStandardMaterialId = standard.Id,
            CalibrationAt = calTime,
            PerformedByUserId = _fixture.SeededUserId,
            PerformedAt = calTime,
            Status = CalibrationRunStatus.Active,
            // Runs are always signed (FK to the append-only signatures table).
            Signature = new ElectronicSignature
            {
                UserId = _fixture.SeededUserId,
                UserFullNameSnapshot = "Integration Analyst",
                UsernameSnapshot = "integration",
                RoleSnapshot = "Analyst",
                MeaningOfSignature = SignatureMeaning.CalibrationRunPerformed,
                EntityType = "TestDefinition",
                EntityId = testDef.Id
            },
            Passed = true,
            AnalytesPassed = 2,
            AnalytesTotal = 2
        };
        db.CalibrationRuns.Add(run);
        await db.SaveChangesAsync();

        var runZn = new CalibrationRunAnalyte
        {
            CalibrationRunId = run.Id,
            TestAnalyteId = znAnalyte.Id,
            Element = "Zn",
            WavelengthNm = 213.857m,
            View = AnalyteView.Axial,
            CorrelationValue = 0.9999m,
            CorrelationType = CorrelationType.RSquared,
            NumberOfStandards = 4,
            LowestStandardMgPerL = 0.01m,
            HighestStandardMgPerL = 10.0m,
            Passed = true
        };
        var runCa = new CalibrationRunAnalyte
        {
            CalibrationRunId = run.Id,
            TestAnalyteId = caAnalyte.Id,
            Element = "Ca",
            WavelengthNm = 317.933m,
            View = AnalyteView.Radial,
            CorrelationValue = 0.9998m,
            CorrelationType = CorrelationType.RSquared,
            NumberOfStandards = 4,
            LowestStandardMgPerL = 0.05m,
            HighestStandardMgPerL = 20.0m,
            Passed = true
        };
        db.CalibrationRunAnalytes.AddRange(runZn, runCa);
        await db.SaveChangesAsync();

        // Seed Item with 2 elemental specifications
        var item = new Item
        {
            Code = $"ITEM_{methodAbbr}",
            Name = $"Item {methodAbbr}",
            IsActive = true
        };
        db.Items.Add(item);
        await db.SaveChangesAsync();

        db.SampleTests.Add(new SampleTest
        {
            ItemId = item.Id,
            TestCode = testDef.Code,
            DisplayName = testDef.DisplayName
        });

        var znSpec = new Specification
        {
            ItemId = item.Id,
            TestCode = testDef.Code,
            ParameterName = "Zinc Assay",
            TestAnalyteId = znAnalyte.Id,
            SampleMatrix = SampleMatrix.Solid,
            ResultBasis = ResultBasis.PercentLabelClaim,
            LabelClaim = 10.0m,
            LabelClaimUnit = "mg",
            ConversionFactor = 1.0m,
            LimitType = LimitType.Range,
            LowerLimit = 90.0m,
            UpperLimit = 110.0m,
            Unit = "%"
        };
        SpecificationService.ApplyCanonicalSpecLimit(znSpec);

        var caSpec = new Specification
        {
            ItemId = item.Id,
            TestCode = testDef.Code,
            ParameterName = "Calcium Assay",
            TestAnalyteId = caAnalyte.Id,
            SampleMatrix = SampleMatrix.Solid,
            ResultBasis = ResultBasis.PercentLabelClaim,
            LabelClaim = 100.0m,
            LabelClaimUnit = "mg",
            ConversionFactor = 1.0m,
            LimitType = LimitType.Range,
            LowerLimit = 90.0m,
            UpperLimit = 110.0m,
            Unit = "%"
        };
        SpecificationService.ApplyCanonicalSpecLimit(caSpec);

        db.Specifications.AddRange(znSpec, caSpec);
        await db.SaveChangesAsync();

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

        // Record Elemental Assay Result on Postgres
        var engine = TestServiceFactory.TestWorkflow(db);

        // Zn: 8500 ppm, Wu = 1.2500 g, LC = 10.0 mg -> 10.625 mg/unit, 106.25 %
        // Ca: 80000 ppm, Wu = 1.2500 g, LC = 100.0 mg -> 100.0 mg/unit, 100.0 %
        var payload = new ElementalAssayPayload(
            UnitAmount: 1.2500m,
            AnalysedAt: calTime.AddMinutes(20),
            Elements: new List<ElementalAssayElementInput>
            {
                new(znSpec.Id, runZn.Id, ReportedPpm: 8500m, OverRange: false, BelowLoq: false),
                new(caSpec.Id, runCa.Id, ReportedPpm: 80000m, OverRange: false, BelowLoq: false)
            },
            Password: "IntegrationPassword123!",
            Comment: "Postgres e2e Elemental Assay");

        var result = await engine.RecordElementalAssayResultAsync(order.Id, payload, _fixture.SeededUserId, "127.0.0.1");

        Assert.Equal("WithinLimits", result.Status);
        Assert.Contains("106.3 %", result.OutcomeSummary);
        Assert.Contains("100.0 %", result.OutcomeSummary);

        // Verify persistence in Postgres
        await using var verifyDb = _fixture.CreateDbContext();
        var savedEntry = await verifyDb.ElementalAssayEntries
            .Include(e => e.Signature)
            .Include(e => e.Results)
            .FirstOrDefaultAsync(e => e.TestOrderId == order.Id);

        Assert.NotNull(savedEntry);
        Assert.Equal(SampleMatrix.Solid, savedEntry.SampleMatrix);
        Assert.Equal(1.2500m, savedEntry.UnitAmount);
        Assert.True(savedEntry.IsActive);
        Assert.NotNull(savedEntry.Signature);
        Assert.Equal(SignatureMeaning.ResultRecorded, savedEntry.Signature.MeaningOfSignature);
        Assert.Equal("TestOrder", savedEntry.Signature.EntityType);
        Assert.Equal(order.Id, savedEntry.Signature.EntityId);

        Assert.Equal(2, savedEntry.Results.Count);
        var savedZn = savedEntry.Results.First(r => r.SpecificationId == znSpec.Id);
        Assert.Equal(8500m, savedZn.ReportedPpm);
        Assert.Equal(10.625m, savedZn.MgPerUnit);
        Assert.Equal(106.25m, savedZn.PercentLabelClaim);
        Assert.Equal("106.3 %", savedZn.ReportedDisplay);
        Assert.Equal("WithinLimits", savedZn.ComparisonStatus);

        var savedCa = savedEntry.Results.First(r => r.SpecificationId == caSpec.Id);
        Assert.Equal(80000m, savedCa.ReportedPpm);
        Assert.Equal(100.0m, savedCa.MgPerUnit);
        Assert.Equal(100.0m, savedCa.PercentLabelClaim);
        Assert.Equal("100.0 %", savedCa.ReportedDisplay);
        Assert.Equal("WithinLimits", savedCa.ComparisonStatus);

        // Verify generic Result row
        var genericResult = await verifyDb.Results.FirstOrDefaultAsync(r => r.TestOrderId == order.Id);
        Assert.NotNull(genericResult);
        Assert.Contains("Zn=8500", genericResult.RawValue);
        Assert.Contains("Ca=80000", genericResult.RawValue);

        // Verify TestOrder status and step
        var verifiedOrder = await verifyDb.TestOrders.FindAsync(order.Id);
        Assert.NotNull(verifiedOrder);
        Assert.Equal(WorkflowStep.Ready, verifiedOrder.CurrentStep);
        Assert.Equal(ApprovalStatus.ResultEntered, verifiedOrder.Status);
    }
}
