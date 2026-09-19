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
        var savedEntry = await verifyDb.TestAnalyses
            .Include(e => e.Signature)
            .Include(e => e.ParameterResults)
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

        // S3b: Verify ResultRecord projection in Postgres
        var projectedRecords = await verifyDb.ResultRecords
            .Where(r => r.SourceTable == "ParameterResult" && r.TestOrderId == order.Id)
            .ToListAsync();
        Assert.Equal(2, projectedRecords.Count);
        var znProj = projectedRecords.First(r => r.ReportedValue == "106.3 %");
        Assert.Equal(106.25m, znProj.NumericValue);
        Assert.Equal("%", znProj.Unit);
        Assert.Equal(ResultKind.Quantitative, znProj.ResultKind);
        Assert.Equal(ResultLevel.WithinLimit, znProj.ResultLevel);

        var caProj = projectedRecords.First(r => r.ReportedValue == "100.0 %");
        Assert.Equal(100.0m, caProj.NumericValue);
        Assert.Equal("%", caProj.Unit);
        Assert.Equal(ResultKind.Quantitative, caProj.ResultKind);
        Assert.Equal(ResultLevel.WithinLimit, caProj.ResultLevel);

        // S3b: Verify SampleSummary in Postgres
        var summaryService = TestServiceFactory.SampleSummary(verifyDb);
        var summary = await summaryService.GetSummaryAsync(sample.Id);
        Assert.NotNull(summary);
        var orderSummary = summary.TestOrders.First(o => o.TestOrderId == order.Id);
        Assert.NotNull(orderSummary.ElementalAssay);
        Assert.Equal(SampleMatrix.Solid, orderSummary.ElementalAssay.SampleMatrix);
        Assert.Equal(1.2500m, orderSummary.ElementalAssay.UnitAmount);
        Assert.Equal("g", orderSummary.ElementalAssay.UnitAmountUnit);
        Assert.NotEqual("Unknown", orderSummary.ElementalAssay.EnteredByName);
        Assert.Equal(2, orderSummary.ElementalAssay.Elements.Count);

        var reportLines = SampleSummaryService.BuildReportLines(summary);
        var exportText = string.Join("\n", reportLines);
        Assert.Contains("FINAL RESULT (ELEMENTAL ASSAY)", exportText);
        Assert.Contains("Zn: 8500 ppm x 1.25 g / 1000 = 10.625 mg, Claim: 10.625, %LC: 106.25 %, Status: WithinLimits", exportText);
    }

    [PostgresFact]
    public async Task S3b_Postgres_ApprovalGate_ReturnToAnalyst_And_Withdrawal()
    {
        var methodAbbr = "F" + Guid.NewGuid().ToString("N")[..4].ToUpperInvariant();
        await using var db = _fixture.CreateDbContext();

        var section = await db.DocumentSections.FirstOrDefaultAsync(s => s.Code == "FP")
            ?? await db.DocumentSections.FirstAsync(s => s.Code == "MICRO");

        var analystUser = await db.Users.FirstAsync(u => u.Id == _fixture.SeededUserId);
        analystUser.PasswordHash = BCrypt.Net.BCrypt.HashPassword("IntegrationPassword123!");
        analystUser.IsActive = true;

        var headRole = await db.Roles.FirstOrDefaultAsync(r => r.Type == RoleType.SectionHead)
            ?? db.Roles.Add(new Role { Name = "Section Head " + methodAbbr, Type = RoleType.SectionHead, IsActive = true }).Entity;
        await db.SaveChangesAsync();

        var headUser = new User
        {
            Username = "pg_head_" + methodAbbr,
            FullName = "PG Section Head",
            RoleId = headRole.Id,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("IntegrationPassword123!"),
            IsActive = true
        };
        db.Users.Add(headUser);
        await db.SaveChangesAsync();

        db.UserOrgMemberships.Add(new UserOrgMembership { UserId = headUser.Id, DepartmentId = section.DepartmentId, SectionId = section.Id });
        await db.SaveChangesAsync();

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
        db.TestAnalytes.Add(znAnalyte);

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
            CreatedByUserId = analystUser.Id,
            LastModifiedByUserId = analystUser.Id
        };
        db.Materials.Add(standard);
        await db.SaveChangesAsync();

        var calTime = DateTime.UtcNow.AddHours(-1);
        var run = new CalibrationRun
        {
            Code = $"{methodAbbr} CAL 01/092026",
            TestDefinitionId = testDef.Id,
            SectionId = section.Id,
            EquipmentId = equip.Id,
            CalibrationStandardMaterialId = standard.Id,
            CalibrationAt = calTime,
            PerformedByUserId = analystUser.Id,
            PerformedAt = calTime,
            Status = CalibrationRunStatus.Active,
            Signature = new ElectronicSignature
            {
                UserId = analystUser.Id,
                UserFullNameSnapshot = analystUser.FullName,
                UsernameSnapshot = analystUser.Username,
                RoleSnapshot = "Analyst",
                MeaningOfSignature = SignatureMeaning.CalibrationRunPerformed,
                EntityType = "TestDefinition",
                EntityId = testDef.Id
            },
            Passed = true,
            AnalytesPassed = 1,
            AnalytesTotal = 1
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
        db.CalibrationRunAnalytes.Add(runZn);
        await db.SaveChangesAsync();

        var item = new Item
        {
            Code = $"ITEM_{methodAbbr}",
            Name = $"Item {methodAbbr}",
            IsActive = true
        };
        db.Items.Add(item);
        await db.SaveChangesAsync();

        var znSpec = new Specification
        {
            ItemId = item.Id,
            TestCode = testDef.Code,
            ParameterName = "Zinc Content",
            TestAnalyteId = znAnalyte.Id,
            SampleMatrix = SampleMatrix.Solid,
            ResultBasis = ResultBasis.MgPerKg,
            ConversionFactor = 1.0m,
            LimitType = LimitType.Range,
            LowerLimit = 100m,
            UpperLimit = 1000m,
            Unit = "mg/kg"
        };
        db.Specifications.Add(znSpec);
        await db.SaveChangesAsync();

        var cause = await db.CausesOfTesting.FirstOrDefaultAsync()
            ?? db.CausesOfTesting.Add(new CauseOfTesting { Name = "Routine", IsActive = true }).Entity;

        var sample1 = new Sample
        {
            CauseOfTesting = cause,
            ReceivedByUserId = analystUser.Id,
            ReferenceNumber = $"SMP1-{methodAbbr}",
            ItemId = item.Id,
            Category = SampleCategory.FinishedProduct,
            Status = SampleStatus.UnderReview,
            ReceivedAt = DateTime.UtcNow
        };
        var sample2 = new Sample
        {
            CauseOfTesting = cause,
            ReceivedByUserId = analystUser.Id,
            ReferenceNumber = $"SMP2-{methodAbbr}",
            ItemId = item.Id,
            Category = SampleCategory.FinishedProduct,
            Status = SampleStatus.InTesting,
            ReceivedAt = DateTime.UtcNow
        };
        db.Samples.AddRange(sample1, sample2);
        await db.SaveChangesAsync();

        var order1 = new TestOrder
        {
            SampleId = sample1.Id,
            TestCode = testDef.Code,
            SectionId = section.Id,
            CurrentStep = WorkflowStep.Reviewed,
            Status = ApprovalStatus.Approved
        };
        var order2 = new TestOrder
        {
            SampleId = sample2.Id,
            TestCode = testDef.Code,
            SectionId = section.Id,
            CurrentStep = WorkflowStep.Running,
            Status = ApprovalStatus.InProgress
        };
        db.TestOrders.AddRange(order1, order2);
        await db.SaveChangesAsync();

        db.SampleSectionSignoffs.Add(new SampleSectionSignoff
        {
            SampleId = sample1.Id,
            SectionId = section.Id,
            Status = SectionSignoffStatus.UnderApproval,
            ReviewedByUserId = analystUser.Id
        });
        await db.SaveChangesAsync();

        // 1. Approval Gate: Blocked without active entry
        var approvalService = TestServiceFactory.SampleApproval(db);
        var exNoEntry = await Assert.ThrowsAsync<InvalidOperationException>(
            () => approvalService.DecideAsync(sample1.Id, headUser.Id, "IntegrationPassword123!", ApprovalDecision.Approve, "approve", "127.0.0.1", sectionId: section.Id));
        Assert.Contains("lacks an active elemental assay entry", exNoEntry.Message);

        // 2. Entering section head cannot approve
        var engine = TestServiceFactory.TestWorkflow(db);
        var payloadHead = new ElementalAssayPayload(
            1.0m,
            calTime.AddMinutes(15),
            new List<ElementalAssayElementInput> { new(znSpec.Id, runZn.Id, 500m, false, false) },
            "IntegrationPassword123!");
        // A result can only be entered on an open order; the order reaches
        // Reviewed afterwards, which is where the approval checks apply.
        order1.CurrentStep = WorkflowStep.Running;
        order1.Status = ApprovalStatus.InProgress;
        await db.SaveChangesAsync();
        await engine.RecordElementalAssayResultAsync(order1.Id, payloadHead, headUser.Id);
        order1.CurrentStep = WorkflowStep.Reviewed;
        await db.SaveChangesAsync();

        var exTestedByHead = await Assert.ThrowsAsync<InvalidOperationException>(
            () => approvalService.DecideAsync(sample1.Id, headUser.Id, "IntegrationPassword123!", ApprovalDecision.Approve, "approve", "127.0.0.1", sectionId: section.Id));
        Assert.Contains("You cannot approve a sample you tested.", exTestedByHead.Message);

        // 3. Return to Analyst (allowed only before the sample is reviewed - sample2 is still
        //    in testing) deactivates the active entry and its results and reopens the step.
        var engine2 = TestServiceFactory.TestWorkflow(db);
        await engine2.RecordElementalAssayResultAsync(order2.Id, new ElementalAssayPayload(
            1.0m,
            calTime.AddMinutes(18),
            new List<ElementalAssayElementInput> { new(znSpec.Id, runZn.Id, 400m, false, false) },
            "IntegrationPassword123!"), analystUser.Id);

        var reviewService = TestServiceFactory.Review(db);
        await reviewService.ReturnToAnalystAsync(order2.Id, headUser.Id, "Return test reason");

        await using var verifyDb = _fixture.CreateDbContext();
        var reloadedEntry = await verifyDb.TestAnalyses.Include(e => e.ParameterResults).FirstAsync(e => e.TestOrderId == order2.Id);
        Assert.False(reloadedEntry.IsActive);
        Assert.All(reloadedEntry.Results, r => Assert.False(r.IsActive));
        var reloadedOrder2 = await verifyDb.TestOrders.FindAsync(order2.Id);
        Assert.Equal(WorkflowStep.Running, reloadedOrder2!.CurrentStep);

        // Re-record order2 (it stays unapproved for the withdrawal check below)
        var engine2b = TestServiceFactory.TestWorkflow(verifyDb);
        var payloadOrder2 = new ElementalAssayPayload(
            1.0m,
            calTime.AddMinutes(20),
            new List<ElementalAssayElementInput> { new(znSpec.Id, runZn.Id, 450m, false, false) },
            "IntegrationPassword123!");
        await engine2b.RecordElementalAssayResultAsync(order2.Id, payloadOrder2, analystUser.Id);

        // Record active result for a fresh approved order on sample1
        var order3 = new TestOrder
        {
            SampleId = sample1.Id,
            TestCode = testDef.Code,
            SectionId = section.Id,
            CurrentStep = WorkflowStep.Running,
            Status = ApprovalStatus.InProgress
        };
        verifyDb.TestOrders.Add(order3);
        await verifyDb.SaveChangesAsync();

        var engine3 = TestServiceFactory.TestWorkflow(verifyDb);
        var payloadOrder3 = new ElementalAssayPayload(
            1.0m,
            calTime.AddMinutes(25),
            new List<ElementalAssayElementInput> { new(znSpec.Id, runZn.Id, 550m, false, false) },
            "IntegrationPassword123!");
        await engine3.RecordElementalAssayResultAsync(order3.Id, payloadOrder3, analystUser.Id);

        // Mark sample1 and order3 Approved
        sample1.Status = SampleStatus.Approved;
        order3.Status = ApprovalStatus.Approved;
        order3.CurrentStep = WorkflowStep.Approved;
        await verifyDb.SaveChangesAsync();

        // 4. Withdrawal: flags unapproved results and lists approved ones
        var calService = TestServiceFactory.CalibrationRun(verifyDb);
        var withdrawn = await calService.WithdrawAsync(
            run.Id,
            new WithdrawCalibrationRunRequest("Postgres withdrawal reason for test run", "IntegrationPassword123!"),
            headUser.Id,
            "127.0.0.1");

        var unapprovedRes = await verifyDb.ParameterResults.FirstAsync(r => r.TestOrderId == order2.Id && r.IsActive);
        Assert.Equal("RequiresReview", unapprovedRes.ComparisonStatus);
        Assert.Equal("450.0 mg/kg", unapprovedRes.ReportedDisplay);

        Assert.Single(withdrawn.AffectedApprovedOrders);
        var aff = withdrawn.AffectedApprovedOrders.First();
        Assert.Equal(sample1.ReferenceNumber, aff.SampleReference);
        Assert.Equal(testDef.Code, aff.TestCode);
        Assert.Equal("Zn", aff.Element);
    }
}
