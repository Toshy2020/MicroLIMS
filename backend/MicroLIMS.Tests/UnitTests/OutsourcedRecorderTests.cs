using Microsoft.EntityFrameworkCore;
using MicroLIMS.API.Controllers;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Tests.ArchitectureTests;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class OutsourcedRecorderTests
{
    private const string Password = "Password123!";

    private static MicroLimsDbContext NewDb() =>
        new(new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private sealed class Scenario
    {
        public DocumentSection Section = null!;
        public User Analyst = null!;
        public User SectionHead = null!;
        public Item Item = null!;
        public Sample Sample = null!;
        public TestDefinition PhyschemTest = null!;
        public Specification PhyschemSpec = null!;
        public TestOrder PhyschemOrder = null!;
    }

    private static Scenario Arrange(MicroLimsDbContext db)
    {
        var uid = Guid.NewGuid().ToString("N")[..6];
        var sc = new Scenario();

        var micro = db.DocumentSections.FirstOrDefault(s => s.Code == "MICRO") ?? TestServiceFactory.EnsureMicroSection(db);
        sc.Section = db.DocumentSections.FirstOrDefault(s => s.Code == "FP")
            ?? db.DocumentSections.Add(new DocumentSection { Name = "Finished Product Laboratory", Code = "FP", DepartmentId = micro.DepartmentId, IsActive = true }).Entity;

        var analystRole = db.Roles.FirstOrDefault(r => r.Type == RoleType.Analyst)
            ?? db.Roles.Add(new Role { Name = "Analyst", Type = RoleType.Analyst, IsActive = true }).Entity;
        var headRole = db.Roles.FirstOrDefault(r => r.Type == RoleType.SectionHead)
            ?? db.Roles.Add(new Role { Name = "Section Head", Type = RoleType.SectionHead, IsActive = true }).Entity;
        db.SaveChanges();

        sc.Analyst = new User { Username = $"analyst_{uid}", FullName = "Analyst User", PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password), RoleId = analystRole.Id, IsActive = true };
        sc.SectionHead = new User { Username = $"head_{uid}", FullName = "Section Head User", PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password), RoleId = headRole.Id, IsActive = true };
        db.Users.AddRange(sc.Analyst, sc.SectionHead);
        db.SaveChanges();

        db.UserOrgMemberships.AddRange(
            new UserOrgMembership { UserId = sc.Analyst.Id, DepartmentId = sc.Section.DepartmentId, SectionId = sc.Section.Id },
            new UserOrgMembership { UserId = sc.SectionHead.Id, DepartmentId = sc.Section.DepartmentId, SectionId = sc.Section.Id }
        );

        sc.Item = new Item { Code = $"ITEM_{uid}", Name = $"Item {uid}", Category = SampleCategory.RawMaterial, IsActive = true };
        db.Items.Add(sc.Item);

        var cause = db.CausesOfTesting.FirstOrDefault() ?? db.CausesOfTesting.Add(new CauseOfTesting { Name = "Routine", IsActive = true }).Entity;
        db.SaveChanges();

        sc.Sample = new Sample
        {
            ReferenceNumber = $"SMP-{uid}",
            Category = SampleCategory.RawMaterial,
            ItemId = sc.Item.Id,
            ReceivedAt = DateTime.UtcNow,
            CauseOfTestingId = cause.Id,
            ReceivedByUserId = sc.SectionHead.Id,
            Status = SampleStatus.InTesting,
            PreparationStatus = SamplePreparationStatus.Ready
        };
        db.Samples.Add(sc.Sample);

        sc.PhyschemTest = new TestDefinition
        {
            Code = $"PC_{uid}",
            DisplayName = $"Physchem Test {uid}",
            SectionId = sc.Section.Id,
            WorkflowType = WorkflowType.Measurement,
            EquationType = EquationType.Measurement,
            ReplicateCount = 1,
            EvaluationBasis = MeasurementEvaluationBasis.Mean,
            IsActive = true
        };
        db.TestDefinitions.Add(sc.PhyschemTest);
        db.SampleTests.Add(new SampleTest { ItemId = sc.Item.Id, TestCode = sc.PhyschemTest.Code, DisplayName = sc.PhyschemTest.DisplayName });

        sc.PhyschemSpec = new Specification
        {
            ItemId = sc.Item.Id,
            TestCode = sc.PhyschemTest.Code,
            ParameterName = "Assay",
            LimitType = LimitType.Range,
            LowerLimit = 95.0m,
            UpperLimit = 105.0m,
            Unit = "%",
            DisplayOrder = 1
        };
        db.Specifications.Add(sc.PhyschemSpec);

        sc.PhyschemOrder = new TestOrder
        {
            SampleId = sc.Sample.Id,
            TestCode = sc.PhyschemTest.Code,
            SectionId = sc.Section.Id,
            CurrentStep = WorkflowStep.Waiting,
            Status = ApprovalStatus.Pending
        };
        db.TestOrders.Add(sc.PhyschemOrder);
        db.SaveChanges();

        return sc;
    }

    private static SampleDocument AddExternalReport(MicroLimsDbContext db, int sampleId, int testOrderId, int userId, SampleDocumentStatus status = SampleDocumentStatus.Current)
    {
        var doc = new SampleDocument
        {
            SampleId = sampleId,
            TestOrderId = testOrderId,
            DocumentType = SampleDocumentType.ExternalLabReport,
            OriginalFileName = "ExternalReport.pdf",
            StorageKey = $"sample-documents/{sampleId}/test_{Guid.NewGuid():N}.pdf",
            FileExtension = ".pdf",
            ContentType = "application/pdf",
            FileSizeBytes = 1024,
            ContentSha256 = "E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855",
            UploadedByUserId = userId,
            UploadedAt = DateTime.UtcNow,
            Status = status,
            VoidReason = status == SampleDocumentStatus.Voided ? "Voided report" : null,
            VoidedAt = status == SampleDocumentStatus.Voided ? DateTime.UtcNow : null,
            VoidedByUserId = status == SampleDocumentStatus.Voided ? userId : null
        };
        db.SampleDocuments.Add(doc);
        db.SaveChanges();
        return doc;
    }

    [Fact]
    public void Outsource_ByAnalyst_IsRefused()
    {
        var roles = AuthorizationMatrixTests.AllowedRoles<TestWorkflowController>(nameof(TestWorkflowController.Outsource))!;
        Assert.DoesNotContain(RoleType.Analyst, roles);
        Assert.Contains(RoleType.SectionHead, roles);
        Assert.Contains(RoleType.SystemAdministrator, roles);
    }

    [Fact]
    public async Task Outsource_MicroTest_IsRefused()
    {
        using var db = NewDb();
        var sc = Arrange(db);

        var microTest = new TestDefinition
        {
            Code = $"MIC_{Guid.NewGuid():N}"[..8],
            DisplayName = "Microbiology Test",
            SectionId = sc.Section.Id,
            WorkflowType = WorkflowType.Observation,
            EquationType = EquationType.Qualitative,
            IsActive = true
        };
        var step = new TestWorkflowStep
        {
            TestDefinition = microTest,
            StepOrder = 1,
            StepName = "Enrichment",
            StepType = StepType.BrothEnrichment
        };
        step.IncubationStages.Add(new TestWorkflowStepIncubationStage { StageNumber = 2, TempMin = 30, TempMax = 35, IncubationMinHours = 24, IncubationMaxHours = 48 });
        microTest.Steps.Add(step);
        db.TestDefinitions.Add(microTest);

        var microOrder = new TestOrder
        {
            SampleId = sc.Sample.Id,
            TestCode = microTest.Code,
            SectionId = sc.Section.Id,
            Status = ApprovalStatus.Pending,
            CurrentStep = WorkflowStep.Waiting
        };
        db.TestOrders.Add(microOrder);
        db.SaveChanges();

        var engine = TestServiceFactory.TestWorkflow(db);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            engine.OutsourceTestAsync(microOrder.Id, new OutsourceTestPayload("Eurofins", "Outsourcing test", Password), sc.SectionHead.Id));
        Assert.Equal("Only physicochemical tests can be outsourced.", ex.Message);
    }

    [Fact]
    public async Task Outsource_SignsAndAudits()
    {
        using var db = NewDb();
        var sc = Arrange(db);
        var engine = TestServiceFactory.TestWorkflow(db);

        await engine.OutsourceTestAsync(sc.PhyschemOrder.Id, new OutsourceTestPayload("Acme Analytical", "Specialized testing required", Password), sc.SectionHead.Id, "127.0.0.1");

        var updatedOrder = await db.TestOrders.FindAsync(sc.PhyschemOrder.Id);
        Assert.NotNull(updatedOrder);
        Assert.Equal("Acme Analytical", updatedOrder.OutsourcedToLab);
        Assert.NotNull(updatedOrder.OutsourcedAt);
        Assert.Equal(sc.SectionHead.Id, updatedOrder.OutsourcedByUserId);

        var sig = await db.ElectronicSignatures
            .FirstOrDefaultAsync(s => s.EntityType == "TestOrder" && s.EntityId == sc.PhyschemOrder.Id && s.MeaningOfSignature == SignatureMeaning.TestOutsourced);
        Assert.NotNull(sig);
        Assert.Equal(sc.SectionHead.Id, sig.UserId);

        var audit = await db.AuditLogs
            .FirstOrDefaultAsync(a => a.ActionCode == OutsourcedRecorder.OutsourcedActionCode && a.TestOrderId == sc.PhyschemOrder.Id);
        Assert.NotNull(audit);
        Assert.Equal(sc.SectionHead.Id, audit.UserId);
        Assert.Equal("Specialized testing required", audit.Reason);
        Assert.Equal("Outsourced to Acme Analytical", audit.NewValue);
    }

    [Fact]
    public async Task Record_NotOutsourced_IsRefused()
    {
        using var db = NewDb();
        var sc = Arrange(db);
        var report = AddExternalReport(db, sc.Sample.Id, sc.PhyschemOrder.Id, sc.Analyst.Id);
        var engine = TestServiceFactory.TestWorkflow(db);

        var payload = new RecordOutsourcedResultPayload(
            DateTime.UtcNow,
            "EXT-1001",
            report.Id,
            new List<OutsourcedParameterInput> { new(sc.PhyschemSpec.Id, 99.5m, null) },
            Password);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            engine.RecordOutsourcedResultAsync(sc.PhyschemOrder.Id, payload, sc.Analyst.Id));
        Assert.Contains("outsourced", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Record_WithoutReport_IsRefused()
    {
        using var db = NewDb();
        var sc = Arrange(db);
        sc.PhyschemOrder.OutsourcedToLab = "Acme Analytical";
        sc.PhyschemOrder.OutsourcedAt = DateTime.UtcNow;
        sc.PhyschemOrder.OutsourcedByUserId = sc.SectionHead.Id;
        db.SaveChanges();

        var engine = TestServiceFactory.TestWorkflow(db);
        var payload = new RecordOutsourcedResultPayload(
            DateTime.UtcNow,
            "EXT-1001",
            ReportDocumentId: 9999, // nonexistent
            new List<OutsourcedParameterInput> { new(sc.PhyschemSpec.Id, 99.5m, null) },
            Password);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            engine.RecordOutsourcedResultAsync(sc.PhyschemOrder.Id, payload, sc.Analyst.Id));
    }

    [Fact]
    public async Task Record_WithVoidedReport_IsRefused()
    {
        using var db = NewDb();
        var sc = Arrange(db);
        sc.PhyschemOrder.OutsourcedToLab = "Acme Analytical";
        sc.PhyschemOrder.OutsourcedAt = DateTime.UtcNow;
        sc.PhyschemOrder.OutsourcedByUserId = sc.SectionHead.Id;
        var voidedReport = AddExternalReport(db, sc.Sample.Id, sc.PhyschemOrder.Id, sc.Analyst.Id, SampleDocumentStatus.Voided);
        db.SaveChanges();

        var engine = TestServiceFactory.TestWorkflow(db);
        var payload = new RecordOutsourcedResultPayload(
            DateTime.UtcNow,
            "EXT-1001",
            voidedReport.Id,
            new List<OutsourcedParameterInput> { new(sc.PhyschemSpec.Id, 99.5m, null) },
            Password);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            engine.RecordOutsourcedResultAsync(sc.PhyschemOrder.Id, payload, sc.Analyst.Id));
        Assert.Contains("voided", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Record_WithAnotherOrdersReport_IsRefused()
    {
        using var db = NewDb();
        var sc = Arrange(db);
        sc.PhyschemOrder.OutsourcedToLab = "Acme Analytical";
        sc.PhyschemOrder.OutsourcedAt = DateTime.UtcNow;
        sc.PhyschemOrder.OutsourcedByUserId = sc.SectionHead.Id;

        // Another test order on same sample
        var anotherOrder = new TestOrder
        {
            SampleId = sc.Sample.Id,
            TestCode = "OTHER_TEST",
            SectionId = sc.Section.Id,
            Status = ApprovalStatus.Pending,
            CurrentStep = WorkflowStep.Waiting
        };
        db.TestOrders.Add(anotherOrder);
        db.SaveChanges();

        var anotherReport = AddExternalReport(db, sc.Sample.Id, anotherOrder.Id, sc.Analyst.Id);

        var engine = TestServiceFactory.TestWorkflow(db);
        var payload = new RecordOutsourcedResultPayload(
            DateTime.UtcNow,
            "EXT-1001",
            anotherReport.Id,
            new List<OutsourcedParameterInput> { new(sc.PhyschemSpec.Id, 99.5m, null) },
            Password);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            engine.RecordOutsourcedResultAsync(sc.PhyschemOrder.Id, payload, sc.Analyst.Id));
    }

    [Fact]
    public async Task Record_RangeAndQualitative_AreJudged()
    {
        using var db = NewDb();
        var sc = Arrange(db);

        // Add an NMT specification
        var nmtSpec = new Specification
        {
            ItemId = sc.Item.Id,
            TestCode = sc.PhyschemTest.Code,
            ParameterName = "Impurity A",
            LimitType = LimitType.NotMoreThan,
            UpperLimit = 1.0m,
            Unit = "%",
            DisplayOrder = 2
        };
        var qualSpec = new Specification
        {
            ItemId = sc.Item.Id,
            TestCode = sc.PhyschemTest.Code,
            ParameterName = "Identification",
            LimitType = LimitType.Qualitative,
            ExpectedResultText = "Conforms",
            DisplayOrder = 3
        };
        db.Specifications.AddRange(nmtSpec, qualSpec);

        sc.PhyschemOrder.OutsourcedToLab = "Acme Analytical";
        sc.PhyschemOrder.OutsourcedAt = DateTime.UtcNow;
        sc.PhyschemOrder.OutsourcedByUserId = sc.SectionHead.Id;
        sc.PhyschemOrder.Status = ApprovalStatus.InProgress;
        sc.PhyschemOrder.CurrentStep = WorkflowStep.Running;
        var report = AddExternalReport(db, sc.Sample.Id, sc.PhyschemOrder.Id, sc.Analyst.Id);
        db.SaveChanges();

        var engine = TestServiceFactory.TestWorkflow(db);

        // 0.5% against NMT 1 conforms
        var conformingPayload = new RecordOutsourcedResultPayload(
            DateTime.UtcNow,
            "EXT-1001",
            report.Id,
            new List<OutsourcedParameterInput>
            {
                new(sc.PhyschemSpec.Id, 100.0m, null),
                new(nmtSpec.Id, 0.5m, null),
                new(qualSpec.Id, null, "Conforms")
            },
            Password);

        var result = await engine.RecordOutsourcedResultAsync(sc.PhyschemOrder.Id, conformingPayload, sc.Analyst.Id);
        Assert.Equal(ResultStatus.WithinLimits, result.Status);

        // 1.2% against NMT 1 is OOS
        // Setup another order to test failing condition
        var order2 = new TestOrder
        {
            SampleId = sc.Sample.Id,
            TestCode = sc.PhyschemTest.Code,
            SectionId = sc.Section.Id,
            Status = ApprovalStatus.InProgress,
            CurrentStep = WorkflowStep.Running,
            OutsourcedToLab = "Acme Analytical",
            OutsourcedAt = DateTime.UtcNow,
            OutsourcedByUserId = sc.SectionHead.Id
        };
        db.TestOrders.Add(order2);
        db.SaveChanges();

        var report2 = AddExternalReport(db, sc.Sample.Id, order2.Id, sc.Analyst.Id);
        var failingPayload = new RecordOutsourcedResultPayload(
            DateTime.UtcNow,
            "EXT-1002",
            report2.Id,
            new List<OutsourcedParameterInput>
            {
                new(sc.PhyschemSpec.Id, 100.0m, null),
                new(nmtSpec.Id, 1.2m, null),
                new(qualSpec.Id, null, "Conforms")
            },
            Password);

        var failResult = await engine.RecordOutsourcedResultAsync(order2.Id, failingPayload, sc.Analyst.Id);
        Assert.Equal(ResultStatus.OutOfSpecification, failResult.Status);
    }

    // An outsourced LOD / water result must look to the moisture lookup like one the lab measured: the
    // dried assay below signs against it and records the LOD order and its parameter result as its source.
    [Fact]
    public async Task Record_OutsourcedLod_FeedsADriedAssay()
    {
        using var db = NewDb();
        var s = TitrationScenario.Seed(db);
        var itemId = db.Samples.Where(x => x.Id == s.Order.SampleId).Select(x => x.ItemId!.Value).First();

        var lodTest = new TestDefinition
        {
            Code = "LOD_EXT", DisplayName = "Loss on Drying", SectionId = s.Section.Id, IsActive = true, PhyschemArea = PhyschemArea.Both,
            WorkflowType = WorkflowType.Gravimetric, EquationType = EquationType.GravimetricLoss,
            MoistureRole = MoistureRole.LossOnDrying, ReplicateCount = 1
        };
        var uvTest = new TestDefinition
        {
            Code = "UV_DRIED", DisplayName = "UV assay", SectionId = s.Section.Id, IsActive = true, PhyschemArea = PhyschemArea.Both,
            WorkflowType = WorkflowType.UvSpectrophotometry, EquationType = EquationType.UvSpectrophotometry,
            ReplicateCount = 1, UvCalculationMode = UvCalculationMode.SpecificAbsorbanceAssay,
            UvSpecificAbsorbance = 328m, UvTotalVolumeMl = 5000m
        };
        db.TestDefinitions.AddRange(lodTest, uvTest);
        db.SampleTests.AddRange(
            new SampleTest { ItemId = itemId, TestCode = lodTest.Code, DisplayName = lodTest.DisplayName },
            new SampleTest { ItemId = itemId, TestCode = uvTest.Code, DisplayName = uvTest.DisplayName });
        var lodSpec = new Specification { ItemId = itemId, TestCode = lodTest.Code, ParameterName = "Loss on Drying", LimitType = LimitType.NotMoreThan, UpperLimit = 10m, Unit = "%", DisplayOrder = 1 };
        var uvSpec = new Specification
        {
            ItemId = itemId, TestCode = uvTest.Code, ParameterName = "Assay", LimitType = LimitType.Range, LowerLimit = 73m, UpperLimit = 79m,
            Unit = "%", DisplayOrder = 1, ResultBasis = ResultBasis.PercentDriedBasis
        };
        db.Specifications.AddRange(lodSpec, uvSpec);
        var spectrophotometer = new Equipment { Name = "UV-Vis", Code = "EQ-UV-EXT", Type = EquipmentType.UvVis, SectionId = s.Section.Id };
        db.Equipment.Add(spectrophotometer);
        var lodOrder = new TestOrder
        {
            SampleId = s.Order.SampleId, TestCode = lodTest.Code, SectionId = s.Section.Id, Status = ApprovalStatus.InProgress, CurrentStep = WorkflowStep.Running,
            OutsourcedToLab = "Moisture Labs", OutsourcedAt = DateTime.UtcNow, OutsourcedByUserId = s.UserId
        };
        var uvOrder = new TestOrder { SampleId = s.Order.SampleId, TestCode = uvTest.Code, SectionId = s.Section.Id, Status = ApprovalStatus.InProgress, CurrentStep = WorkflowStep.Running };
        db.TestOrders.AddRange(lodOrder, uvOrder);
        db.SaveChanges();
        var report = AddExternalReport(db, s.Order.SampleId, lodOrder.Id, s.UserId);
        var engine = TestServiceFactory.TestWorkflow(db);

        // before the LOD is recorded the dried assay is refused
        var uvPayload = new UvPayload(DateTime.UtcNow, spectrophotometer.Id, null, null, null,
            new List<UvParameterInput> { new(uvSpec.Id, 444m, null, new List<UvReplicateInput> { new(0.1m, 0.49m) }) }, Password, "uv");
        var refused = await Assert.ThrowsAsync<InvalidOperationException>(() => engine.RecordUvResultAsync(uvOrder.Id, uvPayload, s.UserId));
        Assert.Equal(SampleMoistureResolver.MissingMessage(MoistureRole.LossOnDrying), refused.Message);

        var lod = await engine.RecordOutsourcedResultAsync(lodOrder.Id, new RecordOutsourcedResultPayload(
            DateTime.UtcNow, "REPORT-LOD-5", report.Id, new List<OutsourcedParameterInput> { new(lodSpec.Id, 5.0m, null) }, Password), s.UserId);
        Assert.Equal(ResultStatus.WithinLimits, lod.Status);

        var moisture = await new SampleMoistureResolver(db).ResolveAsync(s.Order.SampleId, MoistureRole.LossOnDrying);
        Assert.NotNull(moisture);
        Assert.Equal(5.0m, moisture.Percent);
        Assert.Equal(lodOrder.Id, moisture.TestOrderId);

        var uv = await engine.RecordUvResultAsync(uvOrder.Id, uvPayload, s.UserId);
        Assert.Equal(ResultStatus.WithinLimits, uv.Status);
        var assay = await db.TestAnalyses.Include(a => a.ParameterResults).SingleAsync(a => a.TestOrderId == uvOrder.Id);
        Assert.Equal(78.63m, Assert.Single(assay.ParameterResults).ReportedValue);
        using var cond = System.Text.Json.JsonDocument.Parse(assay.ConditionsJson!);
        var used = cond.RootElement.GetProperty("moisture");
        Assert.Equal(5m, used.GetProperty("percent").GetDecimal());
        Assert.Equal(lodOrder.Id, used.GetProperty("testOrderId").GetInt32());
        Assert.Equal(moisture.ParameterResultId, used.GetProperty("parameterResultId").GetInt32());
    }

    [Theory]
    [InlineData("Complies", ResultStatus.WithinLimits)]
    [InlineData("Conforms", ResultStatus.WithinLimits)]
    [InlineData("Does not comply", ResultStatus.OutOfSpecification)]
    [InlineData("Yellow liquid", ResultStatus.OutOfSpecification)]
    public async Task Record_QualitativeText_IsJudgedLikeTheQualitativeRecorder(string text, ResultStatus expected)
    {
        using var db = NewDb();
        var sc = Arrange(db);
        sc.PhyschemSpec.LimitType = LimitType.Qualitative;
        sc.PhyschemSpec.LowerLimit = null;
        sc.PhyschemSpec.UpperLimit = null;
        sc.PhyschemSpec.ExpectedResultText = "Conforms";
        sc.PhyschemOrder.OutsourcedToLab = "Acme Analytical";
        var report = AddExternalReport(db, sc.Sample.Id, sc.PhyschemOrder.Id, sc.Analyst.Id);

        var result = await TestServiceFactory.TestWorkflow(db).RecordOutsourcedResultAsync(
            sc.PhyschemOrder.Id, new RecordOutsourcedResultPayload(DateTime.UtcNow, "EXT-Q", report.Id,
                new List<OutsourcedParameterInput> { new(sc.PhyschemSpec.Id, null, text) }, Password), sc.Analyst.Id);

        Assert.Equal(expected, result.Status);
        var pr = await db.ParameterResults.Include(r => r.Readings).SingleAsync(r => r.TestOrderId == sc.PhyschemOrder.Id);
        Assert.Equal(expected == ResultStatus.WithinLimits ? "Complies" : "Does not comply", pr.ReportedDisplay);
        Assert.Equal(text, Assert.Single(pr.Readings).Text);
        Assert.Contains("\"quantity\":\"Outsourced\"", pr.CalculationJson);
    }

    [Theory]
    [InlineData(ExpectedPresence.Absence, "Absent", true)]
    [InlineData(ExpectedPresence.Absence, "negative", true)]
    [InlineData(ExpectedPresence.Absence, "Present", false)]
    [InlineData(ExpectedPresence.Presence, "Positive", true)]
    [InlineData(ExpectedPresence.Presence, "Absent", false)]
    public void JudgeQualitativeText_PresenceAbsence_FollowsTheExpectedState(ExpectedPresence expected, string text, bool conforms) =>
        Assert.Equal(conforms, SingleResultRecorder.JudgeQualitativeText(
            new Specification { LimitType = LimitType.PresenceAbsence, ExpectedState = expected }, text));

    [Fact]
    public async Task Record_OutsourcedLod_OutOfRange_IsRefused()
    {
        using var db = NewDb();
        var sc = Arrange(db);
        sc.PhyschemTest.MoistureRole = MoistureRole.LossOnDrying;
        sc.PhyschemOrder.OutsourcedToLab = "Moisture Labs";
        sc.PhyschemSpec.LimitType = LimitType.NotMoreThan;
        sc.PhyschemSpec.UpperLimit = 200m;
        var report = AddExternalReport(db, sc.Sample.Id, sc.PhyschemOrder.Id, sc.Analyst.Id);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => TestServiceFactory.TestWorkflow(db).RecordOutsourcedResultAsync(
            sc.PhyschemOrder.Id, new RecordOutsourcedResultPayload(DateTime.UtcNow, "EXT-LOD", report.Id,
                new List<OutsourcedParameterInput> { new(sc.PhyschemSpec.Id, 120m, null) }, Password), sc.Analyst.Id));
        Assert.Contains("loss on drying", ex.Message);
    }

    [Fact]
    public async Task Outsourced_Test_RefusesTheOrdinaryRecorders()
    {
        using var db = NewDb();
        var sc = Arrange(db);
        sc.PhyschemOrder.OutsourcedToLab = "Acme Analytical";
        db.SaveChanges();

        // an ordinary recorder is refused for an outsourced test before it looks at anything else
        var uv = new UvPayload(DateTime.UtcNow, 1, null, null, null,
            new List<UvParameterInput> { new(sc.PhyschemSpec.Id, 444m, null, new List<UvReplicateInput> { new(0.1m, 0.49m) }) }, Password, null);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            TestServiceFactory.TestWorkflow(db).RecordUvResultAsync(sc.PhyschemOrder.Id, uv, sc.Analyst.Id));
        Assert.Contains("outsourced to Acme Analytical", ex.Message);
    }

    [Fact]
    public async Task Outsource_Twice_IsRefused()
    {
        using var db = NewDb();
        var sc = Arrange(db);
        var engine = TestServiceFactory.TestWorkflow(db);
        await engine.OutsourceTestAsync(sc.PhyschemOrder.Id, new OutsourceTestPayload("Acme Analytical", "No HPLC capacity", Password), sc.SectionHead.Id);

        var again = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            engine.OutsourceTestAsync(sc.PhyschemOrder.Id, new OutsourceTestPayload("Other Lab", "Again", Password), sc.SectionHead.Id));
        Assert.Contains("already outsourced to Acme Analytical", again.Message);
        Assert.Single(db.ElectronicSignatures.Where(x => x.MeaningOfSignature == SignatureMeaning.TestOutsourced));
    }

    [Fact]
    public async Task Outsource_WithWrongPassword_ChangesNothing()
    {
        using var db = NewDb();
        var sc = Arrange(db);
        var engine = TestServiceFactory.TestWorkflow(db);

        await Assert.ThrowsAnyAsync<Exception>(() =>
            engine.OutsourceTestAsync(sc.PhyschemOrder.Id, new OutsourceTestPayload("Acme Analytical", "Reason", "wrong"), sc.SectionHead.Id));

        Assert.Null((await db.TestOrders.FindAsync(sc.PhyschemOrder.Id))!.OutsourcedToLab);
        Assert.Empty(db.AuditLogs.Where(a => a.ActionCode == OutsourcedRecorder.OutsourcedActionCode));
    }

    private static SampleDocumentService DocumentService(MicroLimsDbContext db, InMemoryFileStorageService storage) =>
        new(db, storage, new MaterialDocumentFileValidator(26_214_400L),
            new AuditEventService(db, new MicroLIMS.Persistence.Helpers.DatabaseSequenceHelper(db)),
            new UserSectionScopeService(db), Microsoft.Extensions.Logging.Abstractions.NullLogger<SampleDocumentService>.Instance);

    private static readonly byte[] Pdf = { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34 };

    [Fact]
    public async Task Upload_ExternalReport_NeedsAnOutsourcedTestOfThatSample()
    {
        using var db = NewDb();
        var sc = Arrange(db);
        var service = DocumentService(db, new InMemoryFileStorageService());

        // not outsourced yet
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UploadAsync(
            sc.Sample.Id, "r.pdf", "application/pdf", Pdf, SampleDocumentType.ExternalLabReport, sc.SectionHead.Id, sc.PhyschemOrder.Id));
        // a test order id on any other document type
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UploadAsync(
            sc.Sample.Id, "r.pdf", "application/pdf", Pdf, SampleDocumentType.SupplierCoa, sc.SectionHead.Id, sc.PhyschemOrder.Id));

        sc.PhyschemOrder.OutsourcedToLab = "Acme Analytical";
        db.SaveChanges();
        // an external report without a test order
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UploadAsync(
            sc.Sample.Id, "r.pdf", "application/pdf", Pdf, SampleDocumentType.ExternalLabReport, sc.SectionHead.Id));

        var dto = await service.UploadAsync(sc.Sample.Id, "r.pdf", "application/pdf", Pdf, SampleDocumentType.ExternalLabReport, sc.SectionHead.Id, sc.PhyschemOrder.Id);
        Assert.Equal(sc.PhyschemOrder.Id, dto.TestOrderId);
        Assert.Equal(SampleDocumentType.ExternalLabReport, dto.DocumentType);
    }

    [Fact]
    public void Coa_ShowsExternalLabAsAnalyst()
    {
        var order = new TestOrderSummaryDetailDto
        {
            TestOrderId = 1,
            TestCode = "ASSAY",
            TestDisplayName = "Assay Test",
            OutsourcedToLab = "Acme Analytical",
            SpecificationText = "95.0 - 105.0 %",
            Locations = new(),
            Analysis = new AnalysisDetailDto
            {
                EnteredByName = "Internal Analyst",
                EnteredAt = DateTime.UtcNow,
                ParameterResults = new List<ParameterResultDetailDto>
                {
                    new()
                    {
                        ParameterName = "Assay",
                        ReportedDisplay = "99.8 %",
                        SpecLimit = "95.0 - 105.0 %",
                        ComparisonStatus = ResultStatus.WithinLimits
                    }
                }
            }
        };

        var scope = CertificateOfAnalysisBuilder.BuildScope(new[] { order }, "No results");
        Assert.NotNull(scope.Simple);
        var row = Assert.Single(scope.Simple.Rows);
        Assert.Equal("External lab: Acme Analytical", row.AnalystName);
    }
}
