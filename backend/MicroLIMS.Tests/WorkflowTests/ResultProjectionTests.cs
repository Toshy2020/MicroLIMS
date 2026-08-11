using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.WorkflowTests;

// Covers the 6 spec'd cases for the ResultRecord flattened reporting
// projection: what gets written (Part 6.1-6.3), what gets refreshed on
// approval (6.4), what the trend endpoint rejects (6.5), and that
// upserts are idempotent (6.6).
public class ResultProjectionTests
{
    private const string Password = "Correct-Horse-1!";

    private static MicroLimsDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MicroLimsDbContext(options);
    }

    // Single-step TAMC (CountTest) TestOrder on a Water sample - mirrors
    // CountTestWorkflowTests.SeedTamcOrderAsync.
    private static async Task<(TestOrder order, Media media)> SeedTamcOrderAsync(MicroLimsDbContext db)
    {
        var testDefinition = new TestDefinition { Code = "TAMC", DisplayName = "Total Aerobic Microbial Count", WorkflowType = WorkflowType.CountTest };
        var generalAgar = new MediaType { Class = MediaClass.GeneralAgar, IncubationMinHours = 24, IncubationMaxHours = 48, RequiredTemperatureMin = 30, RequiredTemperatureMax = 35 };
        db.TestDefinitions.Add(testDefinition);
        db.MediaTypes.Add(generalAgar);
        await db.SaveChangesAsync();

        db.TestWorkflowSteps.Add(new TestWorkflowStep
        {
            TestDefinitionId = testDefinition.Id, StepOrder = 1, StepName = "CountIncubation", MediaTypeId = generalAgar.Id,
            IncubationMinHours = 0, IncubationMaxHours = 120, TemperatureMin = 30, TemperatureMax = 35,
            IsFinalStep = true, StepType = StepType.PlateCount
        });

        var material = new Material
        {
            MaterialType = MaterialType.DehydratedMedia, MaterialName = "TSA Powder", ManufacturerName = "Himedia",
            BatchNumber = "LOT-001", ReceivingDate = DateTime.UtcNow.AddDays(-10), Code = "TSA",
            Location = "Micro Lab", QuantityReceived = 500, QuantityRemaining = 500, Unit = MaterialUnit.Gram
        };
        db.Materials.Add(material);
        await db.SaveChangesAsync();

        var media = new Media { MediaTypeId = generalAgar.Id, MaterialId = material.Id, LotNumber = "TSA/1/26", IsReleasedForUse = true, Status = MediaStatus.Active, ExpiryDate = DateTime.UtcNow.AddDays(30) };
        db.Media.Add(media);

        var point = new WaterSamplingPoint { Code = "WP-01", Location = "Utility Room", AssignedTestCodes = new() { "TAMC" } };
        db.WaterSamplingPoints.Add(point);
        await db.SaveChangesAsync();

        db.SamplingConfigurations.Add(new SamplingConfiguration { WaterSamplingPointId = point.Id, TestCode = "TAMC", AlertLimit = "10", ActionLimit = "50", SpecLimit = "100" });

        var sample = new Sample { Category = SampleCategory.Water, WaterSamplingPointId = point.Id, ControlNumber = "CTRL-1", Status = SampleStatus.InTesting };
        var order = new TestOrder { TestCode = "TAMC", Status = ApprovalStatus.Pending, CurrentStep = WorkflowStep.Waiting };
        sample.TestOrders.Add(order);
        db.Samples.Add(sample);
        await db.SaveChangesAsync();

        return (order, media);
    }

    // 6.1: CountTestReading "<1" -> IsBelowDetectionLimit true, DetectionLimit
    // 1, NumericValue is the actual calculated value (0.6), not the
    // trend-time-imputed one (0.5) - imputation must never happen at
    // projection-write time.
    [Fact]
    public async Task UpsertFromCountTestReadingAsync_ResultBelowDetectionLimit_StoresActualNotImputedValue()
    {
        await using var db = NewDb();
        var (order, media) = await SeedTamcOrderAsync(db);
        var engine = TestServiceFactory.TestWorkflow(db);
        await engine.SelectMediaAsync(order.Id, "CountIncubation", media.Id, incubatorEquipmentId: 1, userId: 1);

        await engine.RecordResultAsync(order.Id, "CountIncubation", new CountTestPayload(new List<decimal> { 0.6m, 0.6m }, 1), userId: 1);

        var reading = await db.CountTestReadings.SingleAsync(r => r.TestOrderId == order.Id);
        var record = await db.ResultRecords.SingleAsync(r => r.SourceTable == "CountTestReading" && r.SourceId == reading.Id);

        Assert.True(record.IsBelowDetectionLimit);
        Assert.Equal(1m, record.DetectionLimit);
        Assert.Equal(0.6m, record.NumericValue); // actual calculated result, not DetectionLimit/2
        Assert.Equal("<1", record.ReportedValue);
        Assert.Equal(ResultKind.Quantitative, record.ResultKind);
    }

    // 6.6: re-running the same upsert twice must not create a duplicate row
    // - the unique (SourceTable, SourceId, Round) index plus GetOrCreateAsync
    // finding the existing row is what guarantees this.
    [Fact]
    public async Task UpsertFromCountTestReadingAsync_CalledTwice_DoesNotDuplicate()
    {
        await using var db = NewDb();
        var (order, media) = await SeedTamcOrderAsync(db);
        var engine = TestServiceFactory.TestWorkflow(db);
        await engine.SelectMediaAsync(order.Id, "CountIncubation", media.Id, incubatorEquipmentId: 1, userId: 1);
        await engine.RecordResultAsync(order.Id, "CountIncubation", new CountTestPayload(new List<decimal> { 20 }, 1), userId: 1);

        var reading = await db.CountTestReadings.SingleAsync(r => r.TestOrderId == order.Id);

        var projection = TestServiceFactory.ResultProjection(db);
        await projection.UpsertFromCountTestReadingAsync(reading.Id);
        await db.SaveChangesAsync();
        await projection.UpsertFromCountTestReadingAsync(reading.Id);
        await db.SaveChangesAsync();

        var records = await db.ResultRecords.Where(r => r.SourceTable == "CountTestReading" && r.SourceId == reading.Id).ToListAsync();
        Assert.Single(records);
    }

    // 6.2: only the final step's outcome is projected for a pathogen chain
    // - the two intermediate broth steps never get their own ResultRecord,
    // even though each is submitted (and saved) separately.
    [Fact]
    public async Task UpsertFromPathogenResultAsync_ProjectsOnlyFinalStep_QualitativeNotApplicable()
    {
        await using var db = PathogenTestData.NewDb();
        var (order, media, incubator) = await PathogenTestData.SeedFiveStageOrderAsync(db);
        var engine = TestServiceFactory.TestWorkflow(db);
        var start = DateTime.UtcNow.AddHours(-30);
        var end = DateTime.UtcNow.AddHours(-6);

        await engine.SubmitBrothAsync(order.Id, "Broth Enrichment", media.BrothLotId, incubator.Id, start, end, null, userId: 1);
        await engine.SubmitBrothAsync(order.Id, "Selective Broth", media.SelectiveBrothLotId, media.SelectiveBrothIncubatorId, start, end, null, userId: 1);
        // NoGrowth at selective plating finalizes the chain as NotDetected
        // without ever reaching confirmatory plating or biochemical - the
        // shortest path to a projected final result.
        await engine.SubmitSelectivePlatingAsync(order.Id, "Selective Plating", media.SelectivePlatingLotId, incubator.Id,
            start, end, GrowthObservation.NoGrowth, userId: 1);

        var records = await db.ResultRecords.Where(r => r.TestOrderId == order.Id).ToListAsync();
        var record = Assert.Single(records); // neither broth step produces its own row

        Assert.Equal("WorkflowStepResult", record.SourceTable);
        Assert.Equal(ResultKind.Qualitative, record.ResultKind);
        Assert.Equal(ResultLevel.NotApplicable, record.ResultLevel);
        Assert.Equal("Not Detected", record.ReportedValue);
        Assert.Null(record.NumericValue);
    }

    // 6.3: an EM batch result on N rooms yields N separate ResultRecord
    // rows, one per location.
    [Fact]
    public async Task UpsertFromSampleLocationAsync_FiveLocationBatch_CreatesFiveResultRecords()
    {
        await using var db = NewDb();
        var dept = new Department { Name = "Filling" };
        var rooms = Enumerable.Range(1, 5).Select(i => new Room { Name = $"Room {i}", Department = dept, GradeClassification = "A" }).ToList();
        db.Departments.Add(dept);
        db.Rooms.AddRange(rooms);
        await db.SaveChangesAsync();

        var configs = rooms.Select(r => new RoomTestConfiguration
        { RoomId = r.Id, TestType = "PassiveAirSample", TestCode = "TAMC", AlertLimit = "1", ActionLimit = "3", SpecLimit = "5" }).ToList();
        db.RoomTestConfigurations.AddRange(configs);
        await db.SaveChangesAsync();

        var mediaType = new MediaType { Class = MediaClass.GeneralAgar, IncubationMinHours = 24, IncubationMaxHours = 48, RequiredTemperatureMin = 30, RequiredTemperatureMax = 35 };
        var testDefinition = new TestDefinition { Code = "TAMC", DisplayName = "TAMC", WorkflowType = WorkflowType.CountTest };
        db.MediaTypes.Add(mediaType);
        db.TestDefinitions.Add(testDefinition);
        await db.SaveChangesAsync();

        db.TestWorkflowSteps.Add(new TestWorkflowStep
        {
            TestDefinitionId = testDefinition.Id, StepOrder = 1, StepName = "CountIncubation", MediaTypeId = mediaType.Id,
            IncubationMinHours = 0, IncubationMaxHours = 24, TemperatureMin = 30, TemperatureMax = 35, IsFinalStep = true
        });

        var material = new Material
        {
            MaterialType = MaterialType.DehydratedMedia, MaterialName = "TSA Powder", ManufacturerName = "Himedia",
            BatchNumber = "LOT-001", ReceivingDate = DateTime.UtcNow.AddDays(-10), Code = "TSA-EM",
            Location = "Micro Lab", QuantityReceived = 500, QuantityRemaining = 500, Unit = MaterialUnit.Gram
        };
        db.Materials.Add(material);
        await db.SaveChangesAsync();

        var media = new Media { MediaTypeId = mediaType.Id, MaterialId = material.Id, LotNumber = "TSA/EM", IsReleasedForUse = true, Status = MediaStatus.Active, ExpiryDate = DateTime.UtcNow.AddDays(30) };
        var equipment = new Equipment { Name = "Incubator EM", Code = "INC-EM", Type = EquipmentType.Incubator };
        db.Media.Add(media);
        db.Equipment.Add(equipment);
        await db.SaveChangesAsync();

        var emEngine = new EMWorkflowEngine(db, new ReferenceNumberGenerator(db));
        var sample = await emEngine.ReceiveAsync(new EMReceiveRequest(dept.Id, 0, "Analyst", "CTRL-EM", 1));
        var prepared = await emEngine.PrepareAsync(sample.Id, configs.Select(c => c.Id).ToList(), 1);
        var order = prepared.TestOrders.Single();

        var workflowEngine = TestServiceFactory.TestWorkflow(db);
        await workflowEngine.SelectMediaAsync(order.Id, "CountIncubation", media.Id, equipment.Id, 1);

        var locations = await workflowEngine.GetLocationsAsync(order.Id);
        Assert.Equal(5, locations.Count);
        await workflowEngine.RecordBatchResultsAsync(order.Id, 1, locations.Select(l => new BatchLocationResult(l.Id, 0)).ToList(), 1);

        var records = await db.ResultRecords.Where(r => r.TestOrderId == order.Id).ToListAsync();
        Assert.Equal(5, records.Count);
        Assert.All(records, r => Assert.Equal("SampleLocation", r.SourceTable));
    }

    // 6.4: approving a Sample fills ApprovedBy/ApprovedAt on all of its
    // ResultRecord rows - mirrors SampleReviewApprovalTests' seeding.
    [Fact]
    public async Task DecideAsync_Approve_FillsApprovalFieldsOnAllProjectionRows()
    {
        await using var db = NewDb();
        var (order, media) = await SeedTamcOrderAsync(db);

        var role = new Role { Type = RoleType.Reviewer, Name = "Reviewer" };
        db.Roles.Add(role);
        await db.SaveChangesAsync();
        db.Users.AddRange(
            new User { Id = 1, FullName = "Analyst", Username = "analyst", RoleId = role.Id, PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password) },
            new User { Id = 2, FullName = "Reviewer", Username = "reviewer", RoleId = role.Id, PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password) },
            new User { Id = 3, FullName = "Section Head", Username = "sectionhead", RoleId = role.Id, PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password) });
        await db.SaveChangesAsync();

        var engine = TestServiceFactory.TestWorkflow(db);
        await engine.SelectMediaAsync(order.Id, "CountIncubation", media.Id, incubatorEquipmentId: 1, userId: 1);
        await engine.RecordResultAsync(order.Id, "CountIncubation", new CountTestPayload(new List<decimal> { 10 }, 1), userId: 1);

        var sampleId = order.SampleId;
        var reviewService = TestServiceFactory.SampleReview(db);
        await reviewService.CompleteReviewAsync(sampleId, reviewerUserId: 2, Password, null, null);

        var approvalService = TestServiceFactory.SampleApproval(db);
        await approvalService.DecideAsync(sampleId, sectionHeadUserId: 3, Password, ApprovalDecision.Approve, null, null);

        var records = await db.ResultRecords.Where(r => r.SampleId == sampleId).ToListAsync();
        Assert.NotEmpty(records);
        Assert.All(records, r =>
        {
            Assert.Equal(3, r.ApprovedByUserId);
            Assert.Equal("Section Head", r.ApprovedByName);
            Assert.NotNull(r.ApprovedAt);
            Assert.Equal(SampleStatus.Approved, r.SampleStatus);
        });
    }

    // 6.5: a trend request for a qualitative (non-CountTest) test code must
    // be rejected server-side with a clear message, not return an empty chart.
    [Fact]
    public async Task GetTrendAsync_PathogenTestCode_ThrowsInsteadOfReturningEmptyChart()
    {
        await using var db = NewDb();
        db.TestDefinitions.Add(new TestDefinition { Code = "PATHOGEN_ECOLI", DisplayName = "E. coli", WorkflowType = WorkflowType.Observation });
        await db.SaveChangesAsync();

        var query = new ReportingQueryService(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            query.GetTrendAsync("PATHOGEN_ECOLI", "Item A", null, null));
        Assert.Contains("qualitative", ex.Message);
    }

    // A pathogen chain has exactly one reportable outcome per round no
    // matter which step concluded it. Keying the projection on the
    // concluding WorkflowStepResult's id meant a send-back followed by a
    // biochemical submission left TWO ResultRecords for the same test
    // order, and the send-back itself left the reviewer-refused
    // "Detected" standing untouched.
    [Fact]
    public async Task PathogenProjection_SurvivesASendBack_AsOneRecordPerTestOrder()
    {
        await using var db = PathogenTestData.NewDb();
        var (order, media, incubator) = await PathogenTestData.SeedFiveStageOrderAsync(db);
        var engine = TestServiceFactory.TestWorkflow(db);
        var start = DateTime.UtcNow.AddHours(-30);
        var end = DateTime.UtcNow.AddHours(-6);
        const int analystId = 4;
        const int reviewerId = 9;

        await engine.SubmitBrothAsync(order.Id, "Broth Enrichment", media.BrothLotId, incubator.Id, start, end, null, analystId);
        await engine.SubmitBrothAsync(order.Id, "Selective Broth", media.SelectiveBrothLotId, media.SelectiveBrothIncubatorId, start, end, null, analystId);
        await engine.SubmitSelectivePlatingAsync(order.Id, "Selective Plating", media.SelectivePlatingLotId, incubator.Id,
            start, end, GrowthObservation.GrowthConforming, analystId);
        await engine.SubmitConfirmatorySetupAsync(order.Id, "Confirmatory Plating", new[]
        {
            new ConfirmatorySelectionInput(media.XldStepMediaId, media.XldLotId, incubator.Id),
            new ConfirmatorySelectionInput(media.TsiStepMediaId, media.TsiLotId, incubator.Id)
        }, start, end, analystId);
        await engine.SubmitConfirmatoryObservationsAsync(order.Id, "Confirmatory Plating", new[]
        {
            new ConfirmatoryObservationInput(media.XldMaterialId, GrowthObservation.GrowthConforming),
            new ConfirmatoryObservationInput(media.TsiMaterialId, GrowthObservation.GrowthConforming)
        }, analystId);
        await engine.RecordAnalystDecisionAsync(order.Id, AnalystDecision.SubmitAsDetected, analystId);

        var detected = Assert.Single(await db.ResultRecords.Where(r => r.TestOrderId == order.Id).ToListAsync());
        Assert.Equal("Detected", detected.ReportedValue);

        var confirmatoryId = (await db.WorkflowStepResults.SingleAsync(r => r.StepName == "Confirmatory Plating")).Id;
        await engine.RecordBiochemicalReviewDecisionAsync(confirmatoryId, approve: false, "Required per SOP-MB-007.", reviewerId);

        // The order is back in testing, so the refused call must not stand.
        var returned = Assert.Single(await db.ResultRecords.Where(r => r.TestOrderId == order.Id).ToListAsync());
        Assert.NotEqual("Detected", returned.ReportedValue);

        // Answering the send-back updates that same row, rather than
        // adding a second reportable result for the order.
        await engine.SubmitBiochemicalAsync(order.Id, "Biochemical Test", "IMViC: + + - -", null, analystId);

        var confirmed = Assert.Single(await db.ResultRecords.Where(r => r.TestOrderId == order.Id).ToListAsync());
        Assert.Equal("Detected", confirmed.ReportedValue);
    }

    [Fact]
    public async Task GetTrendAsync_CountTestCode_ReturnsOrderedPointsWithImputedStatistics()
    {
        await using var db = NewDb();
        var (order, media) = await SeedTamcOrderAsync(db);
        var engine = TestServiceFactory.TestWorkflow(db);
        await engine.SelectMediaAsync(order.Id, "CountIncubation", media.Id, incubatorEquipmentId: 1, userId: 1);
        await engine.RecordResultAsync(order.Id, "CountIncubation", new CountTestPayload(new List<decimal> { 0.6m, 0.6m }, 1), userId: 1);

        var query = new ReportingQueryService(db);
        var trend = await query.GetTrendAsync("TAMC", "WP-01", null, null);

        var point = Assert.Single(trend.Points);
        Assert.True(point.IsBelowDetectionLimit);
        Assert.Equal(0.6m, point.NumericValue); // stored value is the real calculated result
        Assert.Equal(0.5m, trend.Statistics.Latest); // but the statistic is imputed at DetectionLimit/2
        Assert.Equal(1, trend.Statistics.ImputedPointCount);
    }
}
