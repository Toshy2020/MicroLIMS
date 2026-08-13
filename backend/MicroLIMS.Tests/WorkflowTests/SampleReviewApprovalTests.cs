using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.WorkflowTests;

// Sample-level review/approval: the whole Sample moves through
// UnderReview -> UnderApproval -> Approved/Rejected as one unit, driven
// by TestWorkflowEngine.RecordResultAsync auto-submitting once every
// TestOrder on it is Ready. Seeding mirrors CountTestWorkflowTests'
// single-step TAMC template.
public class SampleReviewApprovalTests
{
    private const string Password = "Correct-Horse-1!";

    private static MicroLimsDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MicroLimsDbContext(options);
    }

    private static async Task<User> SeedUser(MicroLimsDbContext db, int id, RoleType roleType = RoleType.Reviewer)
    {
        var role = new Role { Type = roleType, Name = roleType.ToString() };
        db.Roles.Add(role);
        await db.SaveChangesAsync();

        var user = new User { Id = id, FullName = $"User {id}", Username = $"user{id}", RoleId = role.Id, PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password) };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private static SampleReviewService NewReviewService(MicroLimsDbContext db) => TestServiceFactory.SampleReview(db);

    private static SampleApprovalService NewApprovalService(MicroLimsDbContext db) => TestServiceFactory.SampleApproval(db);

    // One Sample with a single TAMC (CountTest, single-step) TestOrder -
    // recording its result completes the Sample's only test.
    private static async Task<(Sample sample, TestOrder order, Media media)> SeedSingleTestSampleAsync(MicroLimsDbContext db)
    {
        var testDefinition = new TestDefinition { Code = "TAMC", DisplayName = "Total Aerobic Microbial Count", WorkflowType = WorkflowType.CountTest };
        var generalAgar = new MediaType { Class = MediaClass.GeneralAgar, IncubationMinHours = 24, IncubationMaxHours = 48, RequiredTemperatureMin = 30, RequiredTemperatureMax = 35 };
        db.TestDefinitions.Add(testDefinition);
        db.MediaTypes.Add(generalAgar);
        await db.SaveChangesAsync();

        db.TestWorkflowSteps.Add(new TestWorkflowStep
        {
            TestDefinitionId = testDefinition.Id, StepOrder = 1, StepName = "CountIncubation", MediaTypeId = generalAgar.Id,
            IncubationMinHours = 72, IncubationMaxHours = 120, TemperatureMin = 30, TemperatureMax = 35,
            IsFinalStep = true
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

        return (sample, order, media);
    }

    // Drives the seeded TestOrder to Ready, entered by analystId - the
    // point where AutoSubmitForReviewIfReadyAsync fires.
    private static async Task CompleteTestAsync(MicroLimsDbContext db, TestOrder order, Media media, int analystId)
    {
        var engine = TestServiceFactory.TestWorkflow(db);
        await engine.SelectMediaAsync(order.Id, "CountIncubation", media.Id, incubatorEquipmentId: 1, userId: analystId);
        await engine.RecordResultAsync(order.Id, "CountIncubation", new CountTestPayload(new List<decimal> { 10 }, 1), userId: analystId);
    }

    [Fact]
    public async Task RecordResultAsync_LastTestReady_AutoSubmitsSampleForReview()
    {
        await using var db = NewDb();
        var (sample, order, media) = await SeedSingleTestSampleAsync(db);

        await CompleteTestAsync(db, order, media, analystId: 1);

        var reloaded = await db.Samples.FirstAsync(s => s.Id == sample.Id);
        Assert.Equal(SampleStatus.UnderReview, reloaded.Status);

        var events = await db.ReviewWorkflowEvents
            .Where(e => e.EntityType == ReviewEntityTypes.Sample && e.EntityId == sample.Id)
            .ToListAsync();
        Assert.Single(events);
        Assert.Equal(ReviewWorkflowEventType.SubmittedForReview, events[0].EventType);
    }

    [Fact]
    public async Task CompleteReviewAsync_ReviewerTestedTheSample_Throws()
    {
        await using var db = NewDb();
        var (sample, order, media) = await SeedSingleTestSampleAsync(db);
        await SeedUser(db, 1);
        await CompleteTestAsync(db, order, media, analystId: 1);

        var reviewService = NewReviewService(db);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            reviewService.CompleteReviewAsync(sample.Id, reviewerUserId: 1, Password, null, null));
        Assert.Contains("cannot review a sample you tested", ex.Message);
    }

    [Fact]
    public async Task DecideAsync_ApproverWasTheReviewer_Throws()
    {
        await using var db = NewDb();
        var (sample, order, media) = await SeedSingleTestSampleAsync(db);
        await SeedUser(db, 1); // analyst
        await SeedUser(db, 2); // reviewer
        await CompleteTestAsync(db, order, media, analystId: 1);

        var reviewService = NewReviewService(db);
        await reviewService.CompleteReviewAsync(sample.Id, reviewerUserId: 2, Password, null, null);

        var approvalService = NewApprovalService(db);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            approvalService.DecideAsync(sample.Id, sectionHeadUserId: 2, Password, ApprovalDecision.Approve, null, null));
        Assert.Contains("cannot approve a sample you reviewed", ex.Message);
    }

    [Fact]
    public async Task DecideAsync_Approve_SetsSampleAndTestOrdersApproved()
    {
        await using var db = NewDb();
        var (sample, order, media) = await SeedSingleTestSampleAsync(db);
        await SeedUser(db, 1); // analyst
        await SeedUser(db, 2); // reviewer
        await SeedUser(db, 3); // section head
        await CompleteTestAsync(db, order, media, analystId: 1);

        var reviewService = NewReviewService(db);
        await reviewService.CompleteReviewAsync(sample.Id, reviewerUserId: 2, Password, null, null);

        var approvalService = NewApprovalService(db);
        await approvalService.DecideAsync(sample.Id, sectionHeadUserId: 3, Password, ApprovalDecision.Approve, null, null);

        var reloadedSample = await db.Samples.FirstAsync(s => s.Id == sample.Id);
        Assert.Equal(SampleStatus.Approved, reloadedSample.Status);
        Assert.Equal(ApprovalDecision.Approve, reloadedSample.ApprovalDecision);
        Assert.Equal(3, reloadedSample.ApprovedByUserId);

        var reloadedOrder = await db.TestOrders.FirstAsync(t => t.Id == order.Id);
        Assert.Equal(ApprovalStatus.Approved, reloadedOrder.Status);
    }

    [Fact]
    public async Task DecideAsync_RetestRetainedSample_ResetsToInTestingWithFreshTestOrder()
    {
        await using var db = NewDb();
        var (sample, order, media) = await SeedSingleTestSampleAsync(db);
        await SeedUser(db, 1); // analyst
        await SeedUser(db, 2); // reviewer
        await SeedUser(db, 3); // section head
        await CompleteTestAsync(db, order, media, analystId: 1);

        var reviewService = NewReviewService(db);
        await reviewService.CompleteReviewAsync(sample.Id, reviewerUserId: 2, Password, null, null);

        var approvalService = NewApprovalService(db);
        await approvalService.DecideAsync(sample.Id, sectionHeadUserId: 3, Password, ApprovalDecision.RetestRetainedSample, null, null);

        var reloadedSample = await db.Samples.FirstAsync(s => s.Id == sample.Id);
        Assert.Equal(SampleStatus.InTesting, reloadedSample.Status);

        var oldOrder = await db.TestOrders.FirstAsync(t => t.Id == order.Id);
        Assert.True(oldOrder.IsSuperseded);

        var freshOrder = await db.TestOrders.SingleAsync(t => t.SampleId == sample.Id && !t.IsSuperseded);
        Assert.Equal(WorkflowStep.Waiting, freshOrder.CurrentStep);
        Assert.Equal(ApprovalStatus.Pending, freshOrder.Status);
        Assert.Equal("TAMC", freshOrder.TestCode);

        // Old Result/CountTestReading rows must survive, not be deleted.
        Assert.True(await db.CountTestReadings.AnyAsync(r => r.TestOrderId == order.Id));

        // A second round on the fresh TestOrder completes and re-triggers
        // auto-submit for review.
        await CompleteTestAsync(db, freshOrder, media, analystId: 1);
        var reloadedAfterRetest = await db.Samples.FirstAsync(s => s.Id == sample.Id);
        Assert.Equal(SampleStatus.UnderReview, reloadedAfterRetest.Status);
    }
}
