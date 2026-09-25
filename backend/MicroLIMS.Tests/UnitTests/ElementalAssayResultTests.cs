using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class ElementalAssayResultTests
{
    private static MicroLimsDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MicroLimsDbContext(options);
    }

    private static (DocumentSection fpSec, User fpAnalyst, User fpReviewer, TestDefinition testDef, Equipment equip, CalibrationRun run, TestAnalyte znAnalyte, TestAnalyte caAnalyte)
        SeedElementalPrerequisites(MicroLimsDbContext db, DateTime? calibrationAt = null, int maxAgeHours = 24)
    {
        var microSec = TestServiceFactory.EnsureMicroSection(db);

        var fpSec = db.DocumentSections.FirstOrDefault(s => s.Code == "FP")
            ?? db.DocumentSections.Add(new DocumentSection
            {
                Name = "Finished Product Laboratory",
                Code = "FP",
                DepartmentId = microSec.DepartmentId,
                IsActive = true
            }).Entity;
        db.SaveChanges();

        var analystRole = db.Roles.FirstOrDefault(r => r.Type == RoleType.Analyst)
            ?? db.Roles.Add(new Role { Name = "Analyst", Type = RoleType.Analyst, IsActive = true }).Entity;
        var reviewerRole = db.Roles.FirstOrDefault(r => r.Type == RoleType.Reviewer)
            ?? db.Roles.Add(new Role { Name = "Reviewer", Type = RoleType.Reviewer, IsActive = true }).Entity;
        db.SaveChanges();

        var passwordHash = BCrypt.Net.BCrypt.HashPassword("ValidPassword123!");

        var fpAnalyst = new User
        {
            Username = "fp_analyst_" + Guid.NewGuid().ToString("N")[..6],
            FullName = "FP Analyst",
            RoleId = analystRole.Id,
            Role = analystRole,
            PasswordHash = passwordHash,
            IsActive = true
        };
        db.Users.Add(fpAnalyst);

        var fpReviewer = new User
        {
            Username = "fp_reviewer_" + Guid.NewGuid().ToString("N")[..6],
            FullName = "FP Reviewer",
            RoleId = reviewerRole.Id,
            Role = reviewerRole,
            PasswordHash = passwordHash,
            IsActive = true
        };
        db.Users.Add(fpReviewer);
        db.SaveChanges();

        db.UserOrgMemberships.Add(new UserOrgMembership { UserId = fpAnalyst.Id, DepartmentId = fpSec.DepartmentId, SectionId = fpSec.Id });
        db.UserOrgMemberships.Add(new UserOrgMembership { UserId = fpReviewer.Id, DepartmentId = fpSec.DepartmentId, SectionId = fpSec.Id });
        db.SaveChanges();

        var testDef = new TestDefinition
        {
            Code = "ICP-MIN",
            DisplayName = "ICP-OES Minerals Assay",
            SectionId = fpSec.Id,
            WorkflowType = WorkflowType.ElementalAssay,
            EquationType = EquationType.CalibrationCurve,
            MethodAbbreviation = "ICP-MIN",
            CalibrationEntryMode = CalibrationEntryMode.InstrumentReported,
            CalMinCorrelation = 0.999m,
            CalCorrelationType = CorrelationType.RSquared,
            CalMinStandards = 3,
            CalCheckRecoveryLowPercent = 90.0m,
            CalCheckRecoveryHighPercent = 110.0m,
            CalBlankMax = 0.005m,
            ReportedConcentrationBasis = ReportedConcentrationBasis.SamplePpm,
            CalMaxRunAgeHours = maxAgeHours,
            IsActive = true
        };
        db.TestDefinitions.Add(testDef);
        db.SaveChanges();

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
            Name = "PerkinElmer Avio 500 ICP-OES",
            Code = "ICP-01",
            Type = EquipmentType.IcpOes,
            SectionId = fpSec.Id,
            Vendor = "PerkinElmer",
            CdsSoftware = CdsSoftware.PerkinElmerSyngistix
        };
        db.Equipment.Add(equip);

        var standard = new Material
        {
            MaterialName = "Multi-Element Calibration Standard 3",
            MaterialType = MaterialType.ReferenceStandard,
            ManufacturerName = "PerkinElmer",
            BatchNumber = "LOT-PE-STD-01",
            ReceivingDate = DateTime.UtcNow.AddDays(-10),
            ExpiryDate = DateTime.UtcNow.AddYears(1),
            QuantityReceived = 100m,
            QuantityRemaining = 100m,
            Unit = MaterialUnit.Milliliter,
            Location = "Standards Cabinet",
            SectionId = fpSec.Id,
            Purity = 99.9m,
            CreatedByUserId = fpAnalyst.Id,
            LastModifiedByUserId = fpAnalyst.Id
        };
        db.Materials.Add(standard);
        db.SaveChanges();

        var calTime = calibrationAt ?? DateTime.UtcNow.AddHours(-2);
        var sig = new ElectronicSignature
        {
            UserId = fpAnalyst.Id,
            UserFullNameSnapshot = fpAnalyst.FullName,
            UsernameSnapshot = fpAnalyst.Username,
            RoleSnapshot = "Analyst",
            SignedAt = calTime,
            MeaningOfSignature = SignatureMeaning.CalibrationRunPerformed,
            EntityType = nameof(CalibrationRun),
            EntityId = 1
        };
        db.ElectronicSignatures.Add(sig);
        db.SaveChanges();

        var run = new CalibrationRun
        {
            Code = "ICP-MIN CAL 01/092026",
            TestDefinitionId = testDef.Id,
            SectionId = fpSec.Id,
            EquipmentId = equip.Id,
            CalibrationStandardMaterialId = standard.Id,
            CalibrationAt = calTime,
            PerformedByUserId = fpAnalyst.Id,
            PerformedAt = calTime,
            SignatureId = sig.Id,
            Status = CalibrationRunStatus.Active,
            Passed = true,
            AnalytesPassed = 2,
            AnalytesTotal = 2
        };
        db.CalibrationRuns.Add(run);
        db.SaveChanges();

        var doc = new CalibrationRunDocument
        {
            CalibrationRunId = run.Id,
            OriginalFileName = "cal_report.pdf",
            ContentType = "application/pdf",
            SizeBytes = 1024,
            StorageKey = "cal_report.pdf",
            ContentSha256 = "abc",
            UploadedByUserId = fpAnalyst.Id,
            UploadedAt = calTime
        };
        db.CalibrationRunDocuments.Add(doc);
        db.SaveChanges();

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
        db.SaveChanges();

        return (fpSec, fpAnalyst, fpReviewer, testDef, equip, run, znAnalyte, caAnalyte);
    }

    private static (Sample sample, TestOrder order, Item item) SeedSampleAndOrder(
        MicroLimsDbContext db, DocumentSection section, TestDefinition testDef)
    {
        var item = new Item
        {
            Code = "MIN_TAB_" + Guid.NewGuid().ToString("N")[..6],
            Name = "Mineral Tablets",
            IsActive = true
        };
        db.Items.Add(item);
        db.SaveChanges();

        db.SampleTests.Add(new SampleTest
        {
            ItemId = item.Id,
            TestCode = testDef.Code,
            DisplayName = testDef.DisplayName
        });
        db.SaveChanges();

        var cause = db.CausesOfTesting.FirstOrDefault() ?? db.CausesOfTesting.Add(new CauseOfTesting { Name = "Routine", IsActive = true }).Entity;
        db.SaveChanges();

        var sample = new Sample
        {
            CauseOfTesting = cause,
            ReceivedByUserId = 1,
            ReferenceNumber = "SMP-" + Guid.NewGuid().ToString("N")[..6],
            ItemId = item.Id,
            BatchNumber = "BATCH-01",
            ControlNumber = "CTL-01",
            Category = SampleCategory.FinishedProduct,
            Status = SampleStatus.InTesting,
            ReceivedAt = DateTime.UtcNow
        };
        db.Samples.Add(sample);
        db.SaveChanges();

        var order = new TestOrder
        {
            SampleId = sample.Id,
            TestCode = testDef.Code,
            SectionId = section.Id,
            CurrentStep = WorkflowStep.Running,
            Status = ApprovalStatus.InProgress
        };
        db.TestOrders.Add(order);
        db.SaveChanges();

        return (sample, order, item);
    }

    [Fact]
    public async Task TC1_Solid_CalculatesCorrectly_PercentLabelClaim_And_MgPerUnit()
    {
        using var db = NewDb();
        var calTime = DateTime.UtcNow.AddHours(-1);
        var (sec, analyst, _, testDef, _, run, znAnalyte, _) = SeedElementalPrerequisites(db, calTime);
        var (sample, order, item) = SeedSampleAndOrder(db, sec, testDef);

        var runZn = db.CalibrationRunAnalytes.First(a => a.CalibrationRunId == run.Id && a.TestAnalyteId == znAnalyte.Id);

        // Spec for Zn: Solid, ResultBasis = PercentLabelClaim, LC = 10.0 mg, CF = 1.0, Range 90.0-110.0 %
        var znSpec = new Specification
        {
            ItemId = item.Id,
            TestCode = testDef.Code,
            ParameterName = "Zinc Content",
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
        db.Specifications.Add(znSpec);
        db.SaveChanges();

        var engine = TestServiceFactory.TestWorkflow(db);

        // TC1 revised solid: C = 8500 ppm, Wu = 1.2500 g, CF = 1.0, LC = 10.0 mg
        // -> MgPerUnit = 8500 * 1.2500 / 1000 = 10.625 mg/unit
        // -> ResultClaim = 10.625 mg
        // -> %LC = 10.625 / 10.0 * 100 = 106.25 %
        // ReportedValue = 106.25, ReportedDisplay = "106.3 %", ComparisonStatus = "WithinLimits"
        var analysedAt = calTime.AddMinutes(30);
        var payload = new ElementalAssayPayload(
            UnitAmount: 1.2500m,
            AnalysedAt: analysedAt,
            Elements: new List<ElementalAssayElementInput>
            {
                new(znSpec.Id, runZn.Id, ReportedPpm: 8500m, OverRange: false, BelowLoq: false)
            },
            Password: "ValidPassword123!",
            Comment: "TC1 Solid elemental assay");

        var result = await engine.RecordElementalAssayResultAsync(order.Id, payload, analyst.Id);

        Assert.Equal("WithinLimits", result.Status);
        Assert.Contains("106.3 %", result.OutcomeSummary);

        var savedEntry = await db.TestAnalyses
            .Include(e => e.ParameterResults)
            .Include(e => e.Signature)
            .FirstOrDefaultAsync(e => e.TestOrderId == order.Id);

        Assert.NotNull(savedEntry);
        Assert.Equal(SampleMatrix.Solid, savedEntry.SampleMatrix);
        Assert.Equal(1.2500m, savedEntry.UnitAmount);
        Assert.True(savedEntry.IsActive);
        Assert.NotNull(savedEntry.Signature);
        Assert.Equal(SignatureMeaning.ResultRecorded, savedEntry.Signature.MeaningOfSignature);

        var savedResult = Assert.Single(savedEntry.Results);
        Assert.Equal(8500m, savedResult.ReportedPpm);
        Assert.Equal(10.625m, savedResult.MgPerUnit);
        Assert.Equal(10.625m, savedResult.ResultClaim);
        Assert.Equal(106.25m, savedResult.PercentLabelClaim);
        Assert.Equal(106.25m, savedResult.ReportedValue);
        Assert.Equal("106.3 %", savedResult.ReportedDisplay);
        Assert.Equal("WithinLimits", savedResult.ComparisonStatus);

        // Also verify order status moved to Ready/UnderReview
        var reloadedOrder = await db.TestOrders.FindAsync(order.Id);
        Assert.Equal(WorkflowStep.Ready, reloadedOrder!.CurrentStep);
        Assert.Equal(ApprovalStatus.ResultEntered, reloadedOrder.Status);
    }

    [Fact]
    public async Task TC1_Solid_MgPerUnitBasis_CalculatesCorrectly()
    {
        using var db = NewDb();
        var calTime = DateTime.UtcNow.AddHours(-1);
        var (sec, analyst, _, testDef, _, run, znAnalyte, _) = SeedElementalPrerequisites(db, calTime);
        var (sample, order, item) = SeedSampleAndOrder(db, sec, testDef);

        var runZn = db.CalibrationRunAnalytes.First(a => a.CalibrationRunId == run.Id && a.TestAnalyteId == znAnalyte.Id);

        var znSpec = new Specification
        {
            ItemId = item.Id,
            TestCode = testDef.Code,
            ParameterName = "Zinc Content",
            TestAnalyteId = znAnalyte.Id,
            SampleMatrix = SampleMatrix.Solid,
            ResultBasis = ResultBasis.MgPerUnit,
            LabelClaim = 10.0m,
            LabelClaimUnit = "mg",
            ConversionFactor = 1.0m,
            LimitType = LimitType.Range,
            LowerLimit = 9.0m,
            UpperLimit = 11.0m,
            Unit = "mg"
        };
        db.Specifications.Add(znSpec);
        db.SaveChanges();

        var engine = TestServiceFactory.TestWorkflow(db);

        var payload = new ElementalAssayPayload(
            UnitAmount: 1.2500m,
            AnalysedAt: calTime.AddMinutes(20),
            Elements: new List<ElementalAssayElementInput>
            {
                new(znSpec.Id, runZn.Id, ReportedPpm: 8500m, OverRange: false, BelowLoq: false)
            },
            Password: "ValidPassword123!");

        var result = await engine.RecordElementalAssayResultAsync(order.Id, payload, analyst.Id);

        Assert.Equal("WithinLimits", result.Status);
        Assert.Contains("10.6 mg", result.OutcomeSummary);

        var savedResult = await db.ParameterResults.FirstOrDefaultAsync(r => r.TestOrderId == order.Id);
        Assert.NotNull(savedResult);
        Assert.Equal(10.625m, savedResult.MgPerUnit);
        Assert.Equal(10.625m, savedResult.ReportedValue);
        Assert.Equal("10.6 mg", savedResult.ReportedDisplay);
    }

    [Fact]
    public async Task TC1_Liquid_CalculatesCorrectly()
    {
        using var db = NewDb();
        var calTime = DateTime.UtcNow.AddHours(-1);
        var (sec, analyst, _, testDef, _, run, znAnalyte, _) = SeedElementalPrerequisites(db, calTime);
        var (sample, order, item) = SeedSampleAndOrder(db, sec, testDef);

        var runZn = db.CalibrationRunAnalytes.First(a => a.CalibrationRunId == run.Id && a.TestAnalyteId == znAnalyte.Id);

        // Liquid case: C = 200 mg/L, Vd = 5 mL, LC = 1.0 mg -> 1.0 mg/dose, 100 %LC
        var znSpec = new Specification
        {
            ItemId = item.Id,
            TestCode = testDef.Code,
            ParameterName = "Zinc Liquid",
            TestAnalyteId = znAnalyte.Id,
            SampleMatrix = SampleMatrix.Liquid,
            ResultBasis = ResultBasis.PercentLabelClaim,
            LabelClaim = 1.0m,
            LabelClaimUnit = "mg",
            ConversionFactor = 1.0m,
            LimitType = LimitType.Range,
            LowerLimit = 90.0m,
            UpperLimit = 110.0m,
            Unit = "%"
        };
        db.Specifications.Add(znSpec);
        db.SaveChanges();

        var engine = TestServiceFactory.TestWorkflow(db);

        var payload = new ElementalAssayPayload(
            UnitAmount: 5.0m, // 5 mL dose volume
            AnalysedAt: calTime.AddMinutes(15),
            Elements: new List<ElementalAssayElementInput>
            {
                new(znSpec.Id, runZn.Id, ReportedPpm: 200m, OverRange: false, BelowLoq: false)
            },
            Password: "ValidPassword123!");

        var result = await engine.RecordElementalAssayResultAsync(order.Id, payload, analyst.Id);

        Assert.Equal("WithinLimits", result.Status);

        var savedEntry = await db.TestAnalyses.Include(e => e.ParameterResults).FirstAsync(e => e.TestOrderId == order.Id);
        Assert.Equal(SampleMatrix.Liquid, savedEntry.SampleMatrix);
        Assert.Equal(5.0m, savedEntry.UnitAmount);

        var savedResult = Assert.Single(savedEntry.Results);
        Assert.Equal(200m, savedResult.ReportedPpm);
        Assert.Equal(1.0m, savedResult.MgPerUnit); // 200 * 5 / 1000 = 1.0
        Assert.Equal(1.0m, savedResult.ResultClaim);
        Assert.Equal(100.0m, savedResult.PercentLabelClaim);
        Assert.Equal(100.0m, savedResult.ReportedValue);
        Assert.Equal("100.0 %", savedResult.ReportedDisplay);
        Assert.Equal("WithinLimits", savedResult.ComparisonStatus);
    }

    [Fact]
    public async Task TC6_OverRange_SetsRequiresReview_And_NullsNumericValues()
    {
        using var db = NewDb();
        var calTime = DateTime.UtcNow.AddHours(-1);
        var (sec, analyst, _, testDef, _, run, znAnalyte, _) = SeedElementalPrerequisites(db, calTime);
        var (sample, order, item) = SeedSampleAndOrder(db, sec, testDef);

        var runZn = db.CalibrationRunAnalytes.First(a => a.CalibrationRunId == run.Id && a.TestAnalyteId == znAnalyte.Id);

        var znSpec = new Specification
        {
            ItemId = item.Id,
            TestCode = testDef.Code,
            ParameterName = "Zinc Over",
            TestAnalyteId = znAnalyte.Id,
            SampleMatrix = SampleMatrix.Solid,
            ResultBasis = ResultBasis.PercentLabelClaim,
            LabelClaim = 10.0m,
            LabelClaimUnit = "mg",
            LimitType = LimitType.Range,
            LowerLimit = 90.0m,
            UpperLimit = 110.0m,
            Unit = "%"
        };
        db.Specifications.Add(znSpec);
        db.SaveChanges();

        var engine = TestServiceFactory.TestWorkflow(db);

        var payload = new ElementalAssayPayload(
            UnitAmount: 1.0m,
            AnalysedAt: calTime.AddMinutes(10),
            Elements: new List<ElementalAssayElementInput>
            {
                new(znSpec.Id, runZn.Id, ReportedPpm: 25000m, OverRange: true, BelowLoq: false)
            },
            Password: "ValidPassword123!");

        var result = await engine.RecordElementalAssayResultAsync(order.Id, payload, analyst.Id);

        Assert.Equal("RequiresReview", result.Status);
        Assert.Contains("Over range", result.OutcomeSummary);

        var savedResult = await db.ParameterResults.FirstAsync(r => r.TestOrderId == order.Id);
        Assert.True(savedResult.OverRange);
        Assert.Null(savedResult.ReportedValue);
        Assert.Null(savedResult.MgPerUnit);
        Assert.Null(savedResult.ResultClaim);
        Assert.Null(savedResult.PercentLabelClaim);
        Assert.Equal("Over range", savedResult.ReportedDisplay);
        Assert.Equal("RequiresReview", savedResult.ComparisonStatus);
    }

    [Fact]
    public async Task TC7_BelowLoq_AgainstNotMoreThan_IsWithinLimits()
    {
        using var db = NewDb();
        var calTime = DateTime.UtcNow.AddHours(-1);
        var (sec, analyst, _, testDef, _, run, znAnalyte, _) = SeedElementalPrerequisites(db, calTime);
        var (sample, order, item) = SeedSampleAndOrder(db, sec, testDef);

        var runZn = db.CalibrationRunAnalytes.First(a => a.CalibrationRunId == run.Id && a.TestAnalyteId == znAnalyte.Id);

        var znSpec = new Specification
        {
            ItemId = item.Id,
            TestCode = testDef.Code,
            ParameterName = "Heavy Metal Impurity",
            TestAnalyteId = znAnalyte.Id,
            SampleMatrix = SampleMatrix.Solid,
            ResultBasis = ResultBasis.MgPerKg,
            LimitType = LimitType.NotMoreThan,
            UpperLimit = 10.0m,
            Unit = "mg/kg"
        };
        db.Specifications.Add(znSpec);
        db.SaveChanges();

        var engine = TestServiceFactory.TestWorkflow(db);

        var payload = new ElementalAssayPayload(
            UnitAmount: 1.0m,
            AnalysedAt: calTime.AddMinutes(10),
            Elements: new List<ElementalAssayElementInput>
            {
                new(znSpec.Id, runZn.Id, ReportedPpm: 0.001m, OverRange: false, BelowLoq: true)
            },
            Password: "ValidPassword123!");

        var result = await engine.RecordElementalAssayResultAsync(order.Id, payload, analyst.Id);

        Assert.Equal("WithinLimits", result.Status);
        Assert.Contains("<LOQ", result.OutcomeSummary);

        var savedResult = await db.ParameterResults.FirstAsync(r => r.TestOrderId == order.Id);
        Assert.True(savedResult.BelowLoq);
        Assert.Null(savedResult.ReportedValue);
        Assert.Equal("<LOQ", savedResult.ReportedDisplay);
        Assert.Equal("WithinLimits", savedResult.ComparisonStatus);
    }

    [Fact]
    public async Task TC7_BelowLoq_AgainstRange_SetsRequiresReview()
    {
        using var db = NewDb();
        var calTime = DateTime.UtcNow.AddHours(-1);
        var (sec, analyst, _, testDef, _, run, znAnalyte, _) = SeedElementalPrerequisites(db, calTime);
        var (sample, order, item) = SeedSampleAndOrder(db, sec, testDef);

        var runZn = db.CalibrationRunAnalytes.First(a => a.CalibrationRunId == run.Id && a.TestAnalyteId == znAnalyte.Id);

        var znSpec = new Specification
        {
            ItemId = item.Id,
            TestCode = testDef.Code,
            ParameterName = "Zinc Range",
            TestAnalyteId = znAnalyte.Id,
            SampleMatrix = SampleMatrix.Solid,
            ResultBasis = ResultBasis.MgPerUnit,
            LabelClaim = 10.0m,
            LabelClaimUnit = "mg",
            LimitType = LimitType.Range,
            LowerLimit = 9.0m,
            UpperLimit = 11.0m,
            Unit = "mg"
        };
        db.Specifications.Add(znSpec);
        db.SaveChanges();

        var engine = TestServiceFactory.TestWorkflow(db);

        var payload = new ElementalAssayPayload(
            UnitAmount: 1.0m,
            AnalysedAt: calTime.AddMinutes(10),
            Elements: new List<ElementalAssayElementInput>
            {
                new(znSpec.Id, runZn.Id, ReportedPpm: 0.001m, OverRange: false, BelowLoq: true)
            },
            Password: "ValidPassword123!");

        var result = await engine.RecordElementalAssayResultAsync(order.Id, payload, analyst.Id);

        Assert.Equal("RequiresReview", result.Status);
        Assert.Contains("<LOQ", result.OutcomeSummary);

        var savedResult = await db.ParameterResults.FirstAsync(r => r.TestOrderId == order.Id);
        Assert.Equal("RequiresReview", savedResult.ComparisonStatus);
    }

    [Fact]
    public async Task TC8_FailingAnalyte_Refused()
    {
        using var db = NewDb();
        var calTime = DateTime.UtcNow.AddHours(-1);
        var (sec, analyst, _, testDef, _, run, znAnalyte, _) = SeedElementalPrerequisites(db, calTime);
        var (sample, order, item) = SeedSampleAndOrder(db, sec, testDef);

        var runZn = db.CalibrationRunAnalytes.First(a => a.CalibrationRunId == run.Id && a.TestAnalyteId == znAnalyte.Id);
        runZn.Passed = false;
        db.SaveChanges();

        var znSpec = new Specification
        {
            ItemId = item.Id,
            TestCode = testDef.Code,
            ParameterName = "Zinc",
            TestAnalyteId = znAnalyte.Id,
            SampleMatrix = SampleMatrix.Solid,
            ResultBasis = ResultBasis.MgPerKg,
            LimitType = LimitType.NotMoreThan,
            UpperLimit = 100m
        };
        db.Specifications.Add(znSpec);
        db.SaveChanges();

        var engine = TestServiceFactory.TestWorkflow(db);

        var payload = new ElementalAssayPayload(
            UnitAmount: 1.0m,
            AnalysedAt: calTime.AddMinutes(10),
            Elements: new List<ElementalAssayElementInput>
            {
                new(znSpec.Id, runZn.Id, ReportedPpm: 50m, OverRange: false, BelowLoq: false)
            },
            Password: "ValidPassword123!");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            engine.RecordElementalAssayResultAsync(order.Id, payload, analyst.Id));
        Assert.Contains("did not pass calibration", ex.Message);
    }

    [Fact]
    public async Task TC8_WithdrawnRun_Refused()
    {
        using var db = NewDb();
        var calTime = DateTime.UtcNow.AddHours(-1);
        var (sec, analyst, _, testDef, _, run, znAnalyte, _) = SeedElementalPrerequisites(db, calTime);
        var (sample, order, item) = SeedSampleAndOrder(db, sec, testDef);

        var runZn = db.CalibrationRunAnalytes.First(a => a.CalibrationRunId == run.Id && a.TestAnalyteId == znAnalyte.Id);
        run.Status = CalibrationRunStatus.Withdrawn;
        db.SaveChanges();

        var znSpec = new Specification
        {
            ItemId = item.Id,
            TestCode = testDef.Code,
            ParameterName = "Zinc",
            TestAnalyteId = znAnalyte.Id,
            SampleMatrix = SampleMatrix.Solid,
            ResultBasis = ResultBasis.MgPerKg,
            LimitType = LimitType.NotMoreThan,
            UpperLimit = 100m
        };
        db.Specifications.Add(znSpec);
        db.SaveChanges();

        var engine = TestServiceFactory.TestWorkflow(db);

        var payload = new ElementalAssayPayload(
            UnitAmount: 1.0m,
            AnalysedAt: calTime.AddMinutes(10),
            Elements: new List<ElementalAssayElementInput>
            {
                new(znSpec.Id, runZn.Id, ReportedPpm: 50m, OverRange: false, BelowLoq: false)
            },
            Password: "ValidPassword123!");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            engine.RecordElementalAssayResultAsync(order.Id, payload, analyst.Id));
        Assert.Contains("is not active", ex.Message);
    }

    [Fact]
    public async Task RunAgeWindow_BeforeCalibrationAt_And_AfterMaxAge_Refused()
    {
        using var db = NewDb();
        var calTime = DateTime.UtcNow.AddHours(-5);
        var (sec, analyst, _, testDef, _, run, znAnalyte, _) = SeedElementalPrerequisites(db, calTime, maxAgeHours: 4);
        var (sample, order, item) = SeedSampleAndOrder(db, sec, testDef);

        var runZn = db.CalibrationRunAnalytes.First(a => a.CalibrationRunId == run.Id && a.TestAnalyteId == znAnalyte.Id);

        var znSpec = new Specification
        {
            ItemId = item.Id,
            TestCode = testDef.Code,
            ParameterName = "Zinc",
            TestAnalyteId = znAnalyte.Id,
            SampleMatrix = SampleMatrix.Solid,
            ResultBasis = ResultBasis.MgPerKg,
            LimitType = LimitType.NotMoreThan,
            UpperLimit = 100m
        };
        db.Specifications.Add(znSpec);
        db.SaveChanges();

        var engine = TestServiceFactory.TestWorkflow(db);

        // 1. Before calibration time
        var payloadBefore = new ElementalAssayPayload(
            UnitAmount: 1.0m,
            AnalysedAt: calTime.AddMinutes(-10),
            Elements: new List<ElementalAssayElementInput> { new(znSpec.Id, runZn.Id, 50m, false, false) },
            Password: "ValidPassword123!");

        var ex1 = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            engine.RecordElementalAssayResultAsync(order.Id, payloadBefore, analyst.Id));
        Assert.Contains("Analysis time must be within", ex1.Message);

        // 2. After max run age (4 hours, analysed 5 hours after)
        var payloadAfter = new ElementalAssayPayload(
            UnitAmount: 1.0m,
            AnalysedAt: calTime.AddHours(4).AddMinutes(1),
            Elements: new List<ElementalAssayElementInput> { new(znSpec.Id, runZn.Id, 50m, false, false) },
            Password: "ValidPassword123!");

        var ex2 = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            engine.RecordElementalAssayResultAsync(order.Id, payloadAfter, analyst.Id));
        Assert.Contains("Analysis time must be within", ex2.Message);
    }

    [Fact]
    public async Task AnalysedAt_InFuture_Refused()
    {
        using var db = NewDb();
        var calTime = DateTime.UtcNow.AddHours(-1);
        var (sec, analyst, _, testDef, _, run, znAnalyte, _) = SeedElementalPrerequisites(db, calTime);
        var (sample, order, item) = SeedSampleAndOrder(db, sec, testDef);

        var runZn = db.CalibrationRunAnalytes.First(a => a.CalibrationRunId == run.Id && a.TestAnalyteId == znAnalyte.Id);

        var znSpec = new Specification
        {
            ItemId = item.Id,
            TestCode = testDef.Code,
            ParameterName = "Zinc",
            TestAnalyteId = znAnalyte.Id,
            SampleMatrix = SampleMatrix.Solid,
            ResultBasis = ResultBasis.MgPerKg,
            LimitType = LimitType.NotMoreThan,
            UpperLimit = 100m
        };
        db.Specifications.Add(znSpec);
        db.SaveChanges();

        var engine = TestServiceFactory.TestWorkflow(db);

        var payloadFuture = new ElementalAssayPayload(
            UnitAmount: 1.0m,
            AnalysedAt: DateTime.UtcNow.AddMinutes(10), // > 5 min in the future
            Elements: new List<ElementalAssayElementInput> { new(znSpec.Id, runZn.Id, 50m, false, false) },
            Password: "ValidPassword123!");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            engine.RecordElementalAssayResultAsync(order.Id, payloadFuture, analyst.Id));
        Assert.Contains("Analysis time cannot be in the future", ex.Message);
    }

    [Fact]
    public async Task MissingElement_And_DuplicateElement_Refused()
    {
        using var db = NewDb();
        var calTime = DateTime.UtcNow.AddHours(-1);
        var (sec, analyst, _, testDef, _, run, znAnalyte, caAnalyte) = SeedElementalPrerequisites(db, calTime);
        var (sample, order, item) = SeedSampleAndOrder(db, sec, testDef);

        var runZn = db.CalibrationRunAnalytes.First(a => a.CalibrationRunId == run.Id && a.TestAnalyteId == znAnalyte.Id);
        var runCa = db.CalibrationRunAnalytes.First(a => a.CalibrationRunId == run.Id && a.TestAnalyteId == caAnalyte.Id);

        var znSpec = new Specification
        {
            ItemId = item.Id,
            TestCode = testDef.Code,
            ParameterName = "Zinc",
            TestAnalyteId = znAnalyte.Id,
            SampleMatrix = SampleMatrix.Solid,
            ResultBasis = ResultBasis.MgPerKg,
            LimitType = LimitType.NotMoreThan,
            UpperLimit = 100m
        };
        var caSpec = new Specification
        {
            ItemId = item.Id,
            TestCode = testDef.Code,
            ParameterName = "Calcium",
            TestAnalyteId = caAnalyte.Id,
            SampleMatrix = SampleMatrix.Solid,
            ResultBasis = ResultBasis.MgPerKg,
            LimitType = LimitType.NotMoreThan,
            UpperLimit = 500m
        };
        db.Specifications.AddRange(znSpec, caSpec);
        db.SaveChanges();

        var engine = TestServiceFactory.TestWorkflow(db);

        // Missing Ca
        var missingPayload = new ElementalAssayPayload(
            UnitAmount: 1.0m,
            AnalysedAt: calTime.AddMinutes(10),
            Elements: new List<ElementalAssayElementInput> { new(znSpec.Id, runZn.Id, 50m, false, false) },
            Password: "ValidPassword123!");

        var ex1 = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            engine.RecordElementalAssayResultAsync(order.Id, missingPayload, analyst.Id));
        Assert.Contains("Expected 2 element results matching specifications, but received 1", ex1.Message);

        // Duplicate Zn
        var duplicatePayload = new ElementalAssayPayload(
            UnitAmount: 1.0m,
            AnalysedAt: calTime.AddMinutes(10),
            Elements: new List<ElementalAssayElementInput>
            {
                new(znSpec.Id, runZn.Id, 50m, false, false),
                new(znSpec.Id, runZn.Id, 60m, false, false)
            },
            Password: "ValidPassword123!");

        var ex2 = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            engine.RecordElementalAssayResultAsync(order.Id, duplicatePayload, analyst.Id));
        Assert.Contains("Every specification for this test must be supplied exactly once", ex2.Message);
    }

    [Fact]
    public async Task WrongMethodRun_Refused()
    {
        using var db = NewDb();
        var calTime = DateTime.UtcNow.AddHours(-1);
        var (sec, analyst, _, testDef, equip, run, znAnalyte, _) = SeedElementalPrerequisites(db, calTime);
        var (sample, order, item) = SeedSampleAndOrder(db, sec, testDef);

        // Create another test definition
        var otherTest = new TestDefinition
        {
            Code = "ICP-HEAVY",
            DisplayName = "ICP Heavy Metals",
            SectionId = sec.Id,
            WorkflowType = WorkflowType.ElementalAssay,
            EquationType = EquationType.CalibrationCurve,
            MethodAbbreviation = "ICP-HEAVY",
            IsActive = true
        };
        db.TestDefinitions.Add(otherTest);
        db.SaveChanges();

        // Run is for otherTest
        run.TestDefinitionId = otherTest.Id;
        db.SaveChanges();

        var runZn = db.CalibrationRunAnalytes.First(a => a.CalibrationRunId == run.Id && a.TestAnalyteId == znAnalyte.Id);

        var znSpec = new Specification
        {
            ItemId = item.Id,
            TestCode = testDef.Code,
            ParameterName = "Zinc",
            TestAnalyteId = znAnalyte.Id,
            SampleMatrix = SampleMatrix.Solid,
            ResultBasis = ResultBasis.MgPerKg,
            LimitType = LimitType.NotMoreThan,
            UpperLimit = 100m
        };
        db.Specifications.Add(znSpec);
        db.SaveChanges();

        var engine = TestServiceFactory.TestWorkflow(db);

        var payload = new ElementalAssayPayload(
            UnitAmount: 1.0m,
            AnalysedAt: calTime.AddMinutes(10),
            Elements: new List<ElementalAssayElementInput> { new(znSpec.Id, runZn.Id, 50m, false, false) },
            Password: "ValidPassword123!");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            engine.RecordElementalAssayResultAsync(order.Id, payload, analyst.Id));
        Assert.Contains("Linked calibration run is for a different test method", ex.Message);
    }

    [Fact]
    public async Task ConversionFactor_Applied_Correctly()
    {
        using var db = NewDb();
        var calTime = DateTime.UtcNow.AddHours(-1);
        var (sec, analyst, _, testDef, _, run, znAnalyte, _) = SeedElementalPrerequisites(db, calTime);
        var (sample, order, item) = SeedSampleAndOrder(db, sec, testDef);

        var runZn = db.CalibrationRunAnalytes.First(a => a.CalibrationRunId == run.Id && a.TestAnalyteId == znAnalyte.Id);

        // CF = 0.5 (e.g. Zinc Gluconate to Zinc elemental factor)
        // C = 1000, Wu = 1.0 -> MgPerUnit = 1.0
        // ResultClaim = 1.0 * 0.5 = 0.5 mg
        // LC = 0.5 mg -> %LC = 100.0 %
        var znSpec = new Specification
        {
            ItemId = item.Id,
            TestCode = testDef.Code,
            ParameterName = "Zinc Elemental from Salt",
            TestAnalyteId = znAnalyte.Id,
            SampleMatrix = SampleMatrix.Solid,
            ResultBasis = ResultBasis.PercentLabelClaim,
            LabelClaim = 0.5m,
            LabelClaimUnit = "mg",
            ConversionFactor = 0.5m,
            LimitType = LimitType.Range,
            LowerLimit = 90.0m,
            UpperLimit = 110.0m,
            Unit = "%"
        };
        db.Specifications.Add(znSpec);
        db.SaveChanges();

        var engine = TestServiceFactory.TestWorkflow(db);

        var payload = new ElementalAssayPayload(
            UnitAmount: 1.0m,
            AnalysedAt: calTime.AddMinutes(10),
            Elements: new List<ElementalAssayElementInput>
            {
                new(znSpec.Id, runZn.Id, ReportedPpm: 1000m, OverRange: false, BelowLoq: false)
            },
            Password: "ValidPassword123!");

        var result = await engine.RecordElementalAssayResultAsync(order.Id, payload, analyst.Id);

        Assert.Equal("WithinLimits", result.Status);

        var savedResult = await db.ParameterResults.FirstAsync(r => r.TestOrderId == order.Id);
        Assert.Equal(1.0m, savedResult.MgPerUnit);
        Assert.Equal(0.5m, savedResult.ResultClaim);
        Assert.Equal(100.0m, savedResult.PercentLabelClaim);
        Assert.Equal(100.0m, savedResult.ReportedValue);
    }

    [Fact]
    public async Task SpecificationValidationRules_EnforcesAllRules()
    {
        using var db = NewDb();
        var calTime = DateTime.UtcNow.AddHours(-1);
        var (sec, analyst, _, testDef, _, run, znAnalyte, caAnalyte) = SeedElementalPrerequisites(db, calTime);

        var item = new Item { Code = "ITM-01", Name = "Test Item", IsActive = true };
        db.Items.Add(item);
        db.SampleTests.Add(new SampleTest { ItemId = item.Id, TestCode = testDef.Code, DisplayName = testDef.DisplayName });

        // Non-calibration test
        var nonCalTest = new TestDefinition
        {
            Code = "OTHER_TEST",
            DisplayName = "Other Test",
            SectionId = sec.Id,
            WorkflowType = WorkflowType.CountTest,
            EquationType = EquationType.None,
            IsActive = true
        };
        db.TestDefinitions.Add(nonCalTest);
        db.SampleTests.Add(new SampleTest { ItemId = item.Id, TestCode = nonCalTest.Code, DisplayName = nonCalTest.DisplayName });
        db.SaveChanges();

        var specService = new SpecificationService(db);

        // 1. CalibrationCurve missing TestAnalyteId
        var spec1 = new Specification
        {
            ItemId = item.Id,
            TestCode = testDef.Code,
            ParameterName = "P1",
            SampleMatrix = SampleMatrix.Solid,
            ResultBasis = ResultBasis.PercentLabelClaim,
            LimitType = LimitType.Range,
            LowerLimit = 90m,
            UpperLimit = 110m
        };
        var ex1 = await Assert.ThrowsAsync<InvalidOperationException>(() => specService.ValidateAsync(spec1));
        Assert.Contains("Test analyte is required", ex1.Message);

        // 2. CalibrationCurve TestAnalyteId belongs to different test
        var otherTestAnalyte = new TestAnalyte
        {
            TestDefinitionId = 999, // different test
            Element = "Fe",
            WavelengthNm = 238.204m,
            View = AnalyteView.Axial,
            LoqMgPerL = 0.01m,
            DisplayOrder = 1,
            IsActive = true
        };
        db.TestAnalytes.Add(otherTestAnalyte);
        db.SaveChanges();

        var spec2 = new Specification
        {
            ItemId = item.Id,
            TestCode = testDef.Code,
            ParameterName = "P2",
            TestAnalyteId = otherTestAnalyte.Id,
            SampleMatrix = SampleMatrix.Solid,
            ResultBasis = ResultBasis.PercentLabelClaim,
            LimitType = LimitType.Range,
            LowerLimit = 90m,
            UpperLimit = 110m
        };
        var ex2 = await Assert.ThrowsAsync<InvalidOperationException>(() => specService.ValidateAsync(spec2));
        Assert.Contains("Test analyte does not belong to test", ex2.Message);

        // 3. CalibrationCurve missing ResultBasis
        var spec3 = new Specification
        {
            ItemId = item.Id,
            TestCode = testDef.Code,
            ParameterName = "P3",
            TestAnalyteId = znAnalyte.Id,
            SampleMatrix = SampleMatrix.Solid,
            LimitType = LimitType.Range,
            LowerLimit = 90m,
            UpperLimit = 110m
        };
        var ex3 = await Assert.ThrowsAsync<InvalidOperationException>(() => specService.ValidateAsync(spec3));
        Assert.Contains("Result basis is required", ex3.Message);

        // 4. CalibrationCurve missing SampleMatrix
        var spec4 = new Specification
        {
            ItemId = item.Id,
            TestCode = testDef.Code,
            ParameterName = "P4",
            TestAnalyteId = znAnalyte.Id,
            ResultBasis = ResultBasis.MgPerKg,
            LimitType = LimitType.Range,
            LowerLimit = 90m,
            UpperLimit = 110m
        };
        var ex4 = await Assert.ThrowsAsync<InvalidOperationException>(() => specService.ValidateAsync(spec4));
        Assert.Contains("Sample matrix is required", ex4.Message);

        // 5. CalibrationCurve unsupported LimitType (e.g. Qualitative)
        var spec5 = new Specification
        {
            ItemId = item.Id,
            TestCode = testDef.Code,
            ParameterName = "P5",
            TestAnalyteId = znAnalyte.Id,
            SampleMatrix = SampleMatrix.Solid,
            ResultBasis = ResultBasis.MgPerKg,
            LimitType = LimitType.Qualitative,
            ExpectedResultText = "Conforms"
        };
        var ex5 = await Assert.ThrowsAsync<InvalidOperationException>(() => specService.ValidateAsync(spec5));
        Assert.Contains("Limit type 'Qualitative' is not supported", ex5.Message);

        // 6. CalibrationCurve ConversionFactor <= 0
        var spec6 = new Specification
        {
            ItemId = item.Id,
            TestCode = testDef.Code,
            ParameterName = "P6",
            TestAnalyteId = znAnalyte.Id,
            SampleMatrix = SampleMatrix.Solid,
            ResultBasis = ResultBasis.MgPerKg,
            LimitType = LimitType.NotMoreThan,
            UpperLimit = 100m,
            ConversionFactor = 0m
        };
        var ex6 = await Assert.ThrowsAsync<InvalidOperationException>(() => specService.ValidateAsync(spec6));
        Assert.Contains("Conversion factor must be greater than zero", ex6.Message);

        // 7. CalibrationCurve PercentLabelClaim missing LabelClaim
        var spec7 = new Specification
        {
            ItemId = item.Id,
            TestCode = testDef.Code,
            ParameterName = "P7",
            TestAnalyteId = znAnalyte.Id,
            SampleMatrix = SampleMatrix.Solid,
            ResultBasis = ResultBasis.PercentLabelClaim,
            LimitType = LimitType.Range,
            LowerLimit = 90m,
            UpperLimit = 110m,
            LabelClaim = 0m
        };
        var ex7 = await Assert.ThrowsAsync<InvalidOperationException>(() => specService.ValidateAsync(spec7));
        Assert.Contains("Label claim must be greater than zero", ex7.Message);

        // 8. Non-CalibrationCurve setting elemental fields
        var nonCalSpec = new Specification
        {
            ItemId = item.Id,
            TestCode = nonCalTest.Code,
            ParameterName = "NonCal",
            LimitType = LimitType.Range,
            LowerLimit = 10m,
            UpperLimit = 50m,
            TestAnalyteId = znAnalyte.Id
        };
        var ex8 = await Assert.ThrowsAsync<InvalidOperationException>(() => specService.ValidateAsync(nonCalSpec));
        Assert.Contains("Test analyte is only allowed for Calibration Curve specifications", ex8.Message);
    }

    private static User SeedSectionHead(MicroLimsDbContext db, DocumentSection section)
    {
        var headRole = db.Roles.FirstOrDefault(r => r.Type == RoleType.SectionHead)
            ?? db.Roles.Add(new Role { Name = "Section Head", Type = RoleType.SectionHead, IsActive = true }).Entity;
        db.SaveChanges();

        var passwordHash = BCrypt.Net.BCrypt.HashPassword("ValidPassword123!");
        var head = new User
        {
            Username = "fp_head_" + Guid.NewGuid().ToString("N")[..6],
            FullName = "FP Section Head",
            RoleId = headRole.Id,
            Role = headRole,
            PasswordHash = passwordHash,
            IsActive = true
        };
        db.Users.Add(head);
        db.SaveChanges();

        db.UserOrgMemberships.Add(new UserOrgMembership { UserId = head.Id, DepartmentId = section.DepartmentId, SectionId = section.Id });
        db.SaveChanges();

        return head;
    }

    [Fact]
    public async Task S3b_ApprovalGate_BlockedWithoutActiveEntry()
    {
        using var db = NewDb();
        var calTime = DateTime.UtcNow.AddHours(-1);
        var (sec, _, fpReviewer, testDef, _, _, _, _) = SeedElementalPrerequisites(db, calTime);
        var (sample, order, _) = SeedSampleAndOrder(db, sec, testDef);
        var fpHead = SeedSectionHead(db, sec);

        order.CurrentStep = WorkflowStep.Reviewed;
        order.Status = ApprovalStatus.Approved;
        sample.Status = SampleStatus.UnderReview;

        db.SampleSectionSignoffs.Add(new SampleSectionSignoff
        {
            SampleId = sample.Id,
            SectionId = sec.Id,
            Status = SectionSignoffStatus.UnderApproval,
            ReviewedByUserId = fpReviewer.Id
        });
        db.SaveChanges();

        var approvalService = TestServiceFactory.SampleApproval(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => approvalService.DecideAsync(sample.Id, fpHead.Id, "ValidPassword123!", ApprovalDecision.Approve, "approve", "127.0.0.1", sectionId: sec.Id));

        Assert.Contains("lacks an active elemental assay entry", ex.Message);
    }

    [Fact]
    public async Task S3b_ApprovalGate_EnteringSectionHeadCannotApprove()
    {
        using var db = NewDb();
        var calTime = DateTime.UtcNow.AddHours(-1);
        var (sec, _, fpReviewer, testDef, _, run, znAnalyte, caAnalyte) = SeedElementalPrerequisites(db, calTime);
        var (sample, order, item) = SeedSampleAndOrder(db, sec, testDef);
        var fpHead = SeedSectionHead(db, sec);

        var runZn = db.CalibrationRunAnalytes.First(a => a.CalibrationRunId == run.Id && a.TestAnalyteId == znAnalyte.Id);
        var runCa = db.CalibrationRunAnalytes.First(a => a.CalibrationRunId == run.Id && a.TestAnalyteId == caAnalyte.Id);

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
        db.Specifications.AddRange(znSpec, caSpec);
        db.SaveChanges();

        // Record entry as fpHead
        var engine = TestServiceFactory.TestWorkflow(db);
        var payload = new ElementalAssayPayload(
            UnitAmount: 1.2500m,
            AnalysedAt: calTime.AddMinutes(10),
            Elements: new List<ElementalAssayElementInput>
            {
                new(znSpec.Id, runZn.Id, 8500m, false, false),
                new(caSpec.Id, runCa.Id, 80000m, false, false)
            },
            Password: "ValidPassword123!");

        await engine.RecordElementalAssayResultAsync(order.Id, payload, fpHead.Id);

        // Move to UnderApproval
        order.CurrentStep = WorkflowStep.Reviewed;
        var signoff = SampleSectionRollup.GetOrAdd(sample, sec.Id);
        signoff.Status = SectionSignoffStatus.UnderApproval;
        signoff.ReviewedByUserId = fpReviewer.Id;
        sample.Status = SampleStatus.UnderApproval;
        db.SaveChanges();

        var approvalService = TestServiceFactory.SampleApproval(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => approvalService.DecideAsync(sample.Id, fpHead.Id, "ValidPassword123!", ApprovalDecision.Approve, "approve", "127.0.0.1", sectionId: sec.Id));

        Assert.Contains("You cannot approve a sample you tested.", ex.Message);
    }

    [Fact]
    public async Task S3b_ReturnToAnalyst_DeactivatesActiveEntryAndResults_SetsStepToRunning()
    {
        using var db = NewDb();
        var calTime = DateTime.UtcNow.AddHours(-1);
        var (sec, analyst, fpReviewer, testDef, _, run, znAnalyte, caAnalyte) = SeedElementalPrerequisites(db, calTime);
        var (sample, order, item) = SeedSampleAndOrder(db, sec, testDef);

        var runZn = db.CalibrationRunAnalytes.First(a => a.CalibrationRunId == run.Id && a.TestAnalyteId == znAnalyte.Id);
        var runCa = db.CalibrationRunAnalytes.First(a => a.CalibrationRunId == run.Id && a.TestAnalyteId == caAnalyte.Id);

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
        db.Specifications.AddRange(znSpec, caSpec);
        db.SaveChanges();

        var engine = TestServiceFactory.TestWorkflow(db);
        var payload = new ElementalAssayPayload(
            UnitAmount: 1.2500m,
            AnalysedAt: calTime.AddMinutes(10),
            Elements: new List<ElementalAssayElementInput>
            {
                new(znSpec.Id, runZn.Id, 8500m, false, false),
                new(caSpec.Id, runCa.Id, 80000m, false, false)
            },
            Password: "ValidPassword123!");

        await engine.RecordElementalAssayResultAsync(order.Id, payload, analyst.Id);

        var entry = await db.TestAnalyses.Include(e => e.ParameterResults).FirstAsync(e => e.TestOrderId == order.Id);
        Assert.True(entry.IsActive);
        Assert.All(entry.Results, r => Assert.True(r.IsActive));

        var reviewService = TestServiceFactory.Review(db);
        await reviewService.ReturnToAnalystAsync(order.Id, fpReviewer.Id, "Recalibration required");

        var reloadedEntry = await db.TestAnalyses.Include(e => e.ParameterResults).FirstAsync(e => e.TestOrderId == order.Id);
        Assert.False(reloadedEntry.IsActive);
        Assert.All(reloadedEntry.Results, r => Assert.False(r.IsActive));

        var reloadedOrder = await db.TestOrders.FindAsync(order.Id);
        Assert.Equal(WorkflowStep.Running, reloadedOrder!.CurrentStep);
    }

    [Fact]
    public async Task S3b_Projection_CreatesOneResultRecordPerElement()
    {
        using var db = NewDb();
        var calTime = DateTime.UtcNow.AddHours(-1);
        var (sec, analyst, _, testDef, _, run, znAnalyte, caAnalyte) = SeedElementalPrerequisites(db, calTime);
        var (sample, order, item) = SeedSampleAndOrder(db, sec, testDef);

        var runZn = db.CalibrationRunAnalytes.First(a => a.CalibrationRunId == run.Id && a.TestAnalyteId == znAnalyte.Id);
        var runCa = db.CalibrationRunAnalytes.First(a => a.CalibrationRunId == run.Id && a.TestAnalyteId == caAnalyte.Id);

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
        db.Specifications.AddRange(znSpec, caSpec);
        db.SaveChanges();

        var engine = TestServiceFactory.TestWorkflow(db);
        var payload = new ElementalAssayPayload(
            UnitAmount: 1.2500m,
            AnalysedAt: calTime.AddMinutes(10),
            Elements: new List<ElementalAssayElementInput>
            {
                new(znSpec.Id, runZn.Id, 8500m, false, false),
                new(caSpec.Id, runCa.Id, 80000m, false, false)
            },
            Password: "ValidPassword123!");

        await engine.RecordElementalAssayResultAsync(order.Id, payload, analyst.Id);

        var records = await db.ResultRecords.Where(r => r.SourceTable == "ParameterResult" && r.TestOrderId == order.Id).ToListAsync();
        Assert.Equal(2, records.Count);

        var znRecord = records.FirstOrDefault(r => r.ReportedValue == "106.3 %");
        Assert.NotNull(znRecord);
        Assert.Equal(106.25m, znRecord.NumericValue);
        Assert.Equal("%", znRecord.Unit);
        Assert.Equal(ResultKind.Quantitative, znRecord.ResultKind);
        Assert.Equal(ResultLevel.WithinLimit, znRecord.ResultLevel);
        Assert.Equal("FP Analyst", znRecord.ResultEnteredByName);
        Assert.False(znRecord.IsBelowDetectionLimit);

        var caRecord = records.FirstOrDefault(r => r.ReportedValue == "100.0 %");
        Assert.NotNull(caRecord);
        Assert.Equal(100.0m, caRecord.NumericValue);
        Assert.Equal("%", caRecord.Unit);
        Assert.Equal(ResultKind.Quantitative, caRecord.ResultKind);
        Assert.Equal(ResultLevel.WithinLimit, caRecord.ResultLevel);
    }

    [Fact]
    public async Task S3b_Summary_CarriesElementDetails_AndNamesAreNotUnknown()
    {
        using var db = NewDb();
        var calTime = DateTime.UtcNow.AddHours(-1);
        var (sec, analyst, _, testDef, _, run, znAnalyte, caAnalyte) = SeedElementalPrerequisites(db, calTime);
        var (sample, order, item) = SeedSampleAndOrder(db, sec, testDef);

        var runZn = db.CalibrationRunAnalytes.First(a => a.CalibrationRunId == run.Id && a.TestAnalyteId == znAnalyte.Id);
        var runCa = db.CalibrationRunAnalytes.First(a => a.CalibrationRunId == run.Id && a.TestAnalyteId == caAnalyte.Id);

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
        db.Specifications.AddRange(znSpec, caSpec);
        db.SaveChanges();

        var engine = TestServiceFactory.TestWorkflow(db);
        var payload = new ElementalAssayPayload(
            UnitAmount: 1.2500m,
            AnalysedAt: calTime.AddMinutes(10),
            Elements: new List<ElementalAssayElementInput>
            {
                new(znSpec.Id, runZn.Id, 8500m, false, false),
                new(caSpec.Id, runCa.Id, 80000m, false, false)
            },
            Password: "ValidPassword123!");

        await engine.RecordElementalAssayResultAsync(order.Id, payload, analyst.Id);

        var summaryService = TestServiceFactory.SampleSummary(db);
        var summary = await summaryService.GetSummaryAsync(sample.Id);
        Assert.NotNull(summary);

        var orderDto = summary.TestOrders.First(o => o.TestOrderId == order.Id);
        Assert.NotNull(orderDto.ElementalAssay);
        Assert.Equal(SampleMatrix.Solid, orderDto.ElementalAssay.SampleMatrix);
        Assert.Equal(1.2500m, orderDto.ElementalAssay.UnitAmount);
        Assert.Equal("g", orderDto.ElementalAssay.UnitAmountUnit);
        Assert.Equal("FP Analyst", orderDto.ElementalAssay.EnteredByName);
        Assert.NotEqual("Unknown", orderDto.ElementalAssay.EnteredByName);

        Assert.Equal(2, orderDto.ElementalAssay.Elements.Count);
        var znElem = orderDto.ElementalAssay.Elements.First(e => e.Element == "Zn");
        Assert.Equal("Zinc Assay", znElem.ParameterName);
        Assert.Equal(run.Code, znElem.RunCode);
        Assert.True(znElem.RunAnalytePassed);
        Assert.Equal(8500m, znElem.ReportedPpm);
        Assert.Equal(10.625m, znElem.MgPerUnit);
        Assert.Equal(10.625m, znElem.ResultClaim);
        Assert.Equal(106.25m, znElem.PercentLabelClaim);
        Assert.Equal("106.3 %", znElem.ReportedDisplay);
        Assert.Equal("WithinLimits", znElem.Status);

        var reportLines = SampleSummaryService.BuildReportLines(summary);
        var exportText = string.Join("\n", reportLines);
        Assert.Contains("FINAL RESULT (ELEMENTAL ASSAY)", exportText);
        Assert.Contains("Zn: 8500 ppm x 1.25 g / 1000 = 10.625 mg, Claim: 10.625, %LC: 106.25 %, Status: WithinLimits", exportText);
    }

    [Fact]
    public async Task S3b_Withdrawal_FlagsUnapprovedResults_AndListsApprovedOnes()
    {
        using var db = NewDb();
        var calTime = DateTime.UtcNow.AddHours(-1);
        var (sec, analyst, _, testDef, _, run, znAnalyte, caAnalyte) = SeedElementalPrerequisites(db, calTime);
        var fpHead = SeedSectionHead(db, sec);

        var (sample1, order1, item1) = SeedSampleAndOrder(db, sec, testDef);
        var (sample2, order2, item2) = SeedSampleAndOrder(db, sec, testDef);

        var runZn = db.CalibrationRunAnalytes.First(a => a.CalibrationRunId == run.Id && a.TestAnalyteId == znAnalyte.Id);

        var spec1 = new Specification
        {
            ItemId = item1.Id,
            TestCode = testDef.Code,
            ParameterName = "Zinc Assay 1",
            TestAnalyteId = znAnalyte.Id,
            SampleMatrix = SampleMatrix.Solid,
            ResultBasis = ResultBasis.MgPerKg,
            ConversionFactor = 1.0m,
            LimitType = LimitType.Range,
            LowerLimit = 100m,
            UpperLimit = 10000m,
            Unit = "mg/kg"
        };
        var spec2 = new Specification
        {
            ItemId = item2.Id,
            TestCode = testDef.Code,
            ParameterName = "Zinc Assay 2",
            TestAnalyteId = znAnalyte.Id,
            SampleMatrix = SampleMatrix.Solid,
            ResultBasis = ResultBasis.MgPerKg,
            ConversionFactor = 1.0m,
            LimitType = LimitType.Range,
            LowerLimit = 100m,
            UpperLimit = 10000m,
            Unit = "mg/kg"
        };
        db.Specifications.AddRange(spec1, spec2);
        db.SaveChanges();

        var engine = TestServiceFactory.TestWorkflow(db);

        // Record for order 1 (remains unapproved)
        var payload1 = new ElementalAssayPayload(1.0m, calTime.AddMinutes(10), new List<ElementalAssayElementInput> { new(spec1.Id, runZn.Id, 500m, false, false) }, "ValidPassword123!");
        await engine.RecordElementalAssayResultAsync(order1.Id, payload1, analyst.Id);

        // Record for order 2 and approve it
        var payload2 = new ElementalAssayPayload(1.0m, calTime.AddMinutes(10), new List<ElementalAssayElementInput> { new(spec2.Id, runZn.Id, 500m, false, false) }, "ValidPassword123!");
        await engine.RecordElementalAssayResultAsync(order2.Id, payload2, analyst.Id);

        sample2.Status = SampleStatus.Approved;
        order2.Status = ApprovalStatus.Approved;
        order2.CurrentStep = WorkflowStep.Approved;
        db.SaveChanges();

        // Withdraw run
        var calService = TestServiceFactory.CalibrationRun(db);
        var withdrawn = await calService.WithdrawAsync(
            run.Id,
            new WithdrawCalibrationRunRequest("Analytical standard contaminated", "ValidPassword123!"),
            fpHead.Id,
            "127.0.0.1");

        // Order 1 result should be flagged RequiresReview and ReportedDisplay unchanged
        var res1 = await db.ParameterResults.FirstAsync(r => r.TestOrderId == order1.Id);
        Assert.Equal("RequiresReview", res1.ComparisonStatus);
        Assert.Equal("500.0 mg/kg", res1.ReportedDisplay);

        // Order 2 (approved) should be listed in AffectedApprovedOrders
        Assert.Single(withdrawn.AffectedApprovedOrders);
        var affected = withdrawn.AffectedApprovedOrders.First();
        Assert.Equal(sample2.ReferenceNumber, affected.SampleReference);
        Assert.Equal(testDef.Code, affected.TestCode);
        Assert.Equal("Zn", affected.Element);
    }
}
