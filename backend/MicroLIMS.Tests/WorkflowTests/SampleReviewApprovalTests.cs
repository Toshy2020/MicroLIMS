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
        db.TestDefinitions.Add(testDefinition);
        await db.SaveChangesAsync();

        var step = new TestWorkflowStep
        {
            TestDefinitionId = testDefinition.Id, StepOrder = 1, StepName = "CountIncubation", 
            IncubationMinHours = 72, IncubationMaxHours = 120, TemperatureMin = 30, TemperatureMax = 35,
            IsFinalStep = true
        };
        db.TestWorkflowSteps.Add(step);

        var material = new Material
        {
            MaterialType = MaterialType.DehydratedMedia, MaterialName = "TSA Powder", ManufacturerName = "Himedia",
            BatchNumber = "LOT-001", ReceivingDate = DateTime.UtcNow.AddDays(-10), Code = "TSA",
            Location = "Micro Lab", QuantityReceived = 500, QuantityRemaining = 500, Unit = MaterialUnit.Gram
        };
        db.Materials.Add(material);
        await db.SaveChangesAsync();
        db.TestWorkflowStepMedias.Add(new TestWorkflowStepMedia { TestWorkflowStepId = step.Id, MaterialId = material.Id, TempMin = 30, TempMax = 35 });

        var media = new Media { MaterialId = material.Id, LotNumber = "TSA/1/26", IsReleasedForUse = true, Status = MediaStatus.Active, ExpiryDate = DateTime.UtcNow.AddDays(30) };
        db.Media.Add(media);
        // First Equipment row added to this fresh in-memory DB, so it gets
        // Id 1 - matching the hardcoded incubatorEquipmentId: 1 the tests
        // below pass to SelectMediaAsync, which now enforces incubator
        // eligibility (temperature must fall within the step medium's range).
        db.Equipment.Add(new Equipment { Name = "Incubator", Code = "INC-1", Type = EquipmentType.Incubator, SetPointTemperature = 32 });

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

    // CertificateRemarks is the Approver-only, customer-facing field added
    // for the Product/RM/PM Certificate of Analysis - distinct from the
    // internal Comment above, and must round-trip exactly as typed.
    [Fact]
    public async Task DecideAsync_Approve_WithCertificateRemarks_PersistsRemarks()
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
        await approvalService.DecideAsync(sample.Id, sectionHeadUserId: 3, Password, ApprovalDecision.Approve, null, null, "  Released per protocol XYZ.  ");

        var reloadedSample = await db.Samples.FirstAsync(s => s.Id == sample.Id);
        Assert.Equal("Released per protocol XYZ.", reloadedSample.CertificateRemarks);
    }

    [Fact]
    public async Task DecideAsync_Approve_WithoutCertificateRemarks_LeavesNull()
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
        Assert.Null(reloadedSample.CertificateRemarks);
    }

    private static async Task<CauseOfTesting> SeedRetestCauseAsync(MicroLimsDbContext db)
    {
        var cause = new CauseOfTesting { Name = "Retest", IsActive = true };
        db.CausesOfTesting.Add(cause);
        await db.SaveChangesAsync();
        return cause;
    }

    // A Sample already sitting at UnderApproval with two current
    // TestOrders, both assigned to analystId - built directly (no
    // TestWorkflowEngine plumbing needed) since these OOS-retest tests
    // only exercise SampleApprovalService's own branching logic.
    private static async Task<(Sample sample, TestOrder tamc, TestOrder tymc)> SeedTwoTestSampleUnderApprovalAsync(
        MicroLimsDbContext db, int analystId, int reviewedByUserId)
    {
        var sample = new Sample
        {
            Category = SampleCategory.Water,
            ControlNumber = "CTRL-OOS-1",
            Status = SampleStatus.UnderApproval,
            ReviewedByUserId = reviewedByUserId
        };
        var tamc = new TestOrder { TestCode = "TAMC", Status = ApprovalStatus.Reviewed, CurrentStep = WorkflowStep.Waiting, AssignedAnalystId = analystId };
        var tymc = new TestOrder { TestCode = "TYMC", Status = ApprovalStatus.Reviewed, CurrentStep = WorkflowStep.Waiting, AssignedAnalystId = analystId };
        sample.TestOrders.Add(tamc);
        sample.TestOrders.Add(tymc);
        db.Samples.Add(sample);
        await db.SaveChangesAsync();
        return (sample, tamc, tymc);
    }

    [Fact]
    public async Task DecideAsync_RetestRetainedSample_SpinsOffNewSampleWithOnlySelectedTestAndHoldsOriginal()
    {
        await using var db = NewDb();
        await SeedUser(db, 1); // analyst
        await SeedUser(db, 2); // reviewer
        await SeedUser(db, 3); // section head
        await SeedRetestCauseAsync(db);
        var (sample, tamc, tymc) = await SeedTwoTestSampleUnderApprovalAsync(db, analystId: 1, reviewedByUserId: 2);

        var approvalService = NewApprovalService(db);
        await approvalService.DecideAsync(sample.Id, sectionHeadUserId: 3, Password, ApprovalDecision.RetestRetainedSample,
            null, null, selectedTestOrderIds: new List<int> { tamc.Id });

        var reloadedSample = await db.Samples.FirstAsync(s => s.Id == sample.Id);
        Assert.Equal(SampleStatus.RetestRequested, reloadedSample.Status);
        Assert.Equal(ApprovalDecision.RetestRetainedSample, reloadedSample.ApprovalDecision);

        var reloadedTamc = await db.TestOrders.FirstAsync(t => t.Id == tamc.Id);
        Assert.True(reloadedTamc.IsSuperseded);

        // The non-selected TestOrder must be left completely untouched -
        // this is the exact bug being fixed: the old code superseded and
        // re-queued every TestOrder on the sample regardless of whether
        // it actually failed.
        var reloadedTymc = await db.TestOrders.FirstAsync(t => t.Id == tymc.Id);
        Assert.False(reloadedTymc.IsSuperseded);
        Assert.Equal(ApprovalStatus.Reviewed, reloadedTymc.Status);

        // No replacement TestOrder is created on the original sample any more.
        Assert.Equal(2, await db.TestOrders.CountAsync(t => t.SampleId == sample.Id));

        var newSample = await db.Samples.SingleAsync(s => s.OriginSampleId == sample.Id);
        Assert.NotEqual(sample.ReferenceNumber, newSample.ReferenceNumber);
        Assert.Equal(sample.Category, newSample.Category);
        Assert.Equal(SampleStatus.Received, newSample.Status);
        Assert.Equal(SamplePreparationStatus.NeedsPreparation, newSample.PreparationStatus);

        var newOrder = await db.TestOrders.SingleAsync(t => t.SampleId == newSample.Id);
        Assert.Equal("TAMC", newOrder.TestCode);
        Assert.Equal(1, newOrder.AssignedAnalystId);
        Assert.False(newOrder.IsSuperseded);
        Assert.Equal(WorkflowStep.Waiting, newOrder.CurrentStep);
    }

    [Fact]
    public async Task DecideAsync_RetestRetainedSample_SetsOosGroupCodeOnOriginAndSpinoff()
    {
        await using var db = NewDb();
        await SeedUser(db, 1);
        await SeedUser(db, 2);
        await SeedUser(db, 3);
        await SeedRetestCauseAsync(db);
        var (sample, tamc, _) = await SeedTwoTestSampleUnderApprovalAsync(db, analystId: 1, reviewedByUserId: 2);

        var approvalService = NewApprovalService(db);
        await approvalService.DecideAsync(sample.Id, sectionHeadUserId: 3, Password, ApprovalDecision.RetestRetainedSample,
            null, null, selectedTestOrderIds: new List<int> { tamc.Id });

        var reloadedSample = await db.Samples.FirstAsync(s => s.Id == sample.Id);
        Assert.NotNull(reloadedSample.OosGroupCode);
        Assert.StartsWith("OOS", reloadedSample.OosGroupCode);

        var spinoff = await db.Samples.SingleAsync(s => s.OriginSampleId == sample.Id);
        Assert.Equal(reloadedSample.OosGroupCode, spinoff.OosGroupCode);
    }

    [Fact]
    public async Task DecideAsync_RetestRetainedSample_WithoutSelectedTests_Throws()
    {
        await using var db = NewDb();
        await SeedUser(db, 1);
        await SeedUser(db, 2);
        await SeedUser(db, 3);
        await SeedRetestCauseAsync(db);
        var (sample, _, _) = await SeedTwoTestSampleUnderApprovalAsync(db, analystId: 1, reviewedByUserId: 2);

        var approvalService = NewApprovalService(db);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            approvalService.DecideAsync(sample.Id, sectionHeadUserId: 3, Password, ApprovalDecision.RetestRetainedSample, null, null));
        Assert.Contains("At least one test must be selected", ex.Message);
    }

    [Fact]
    public async Task DecideAsync_NewSampleRequest_CreatesTwoSamplesWithTwoDifferentAnalysts()
    {
        await using var db = NewDb();
        await SeedUser(db, 1); // original analyst
        await SeedUser(db, 2); // reviewer
        await SeedUser(db, 3); // section head
        await SeedUser(db, 4, RoleType.Analyst); // new analyst one
        await SeedUser(db, 5, RoleType.Analyst); // new analyst two
        await SeedRetestCauseAsync(db);
        var (sample, tamc, tymc) = await SeedTwoTestSampleUnderApprovalAsync(db, analystId: 1, reviewedByUserId: 2);

        var approvalService = NewApprovalService(db);
        await approvalService.DecideAsync(sample.Id, sectionHeadUserId: 3, Password, ApprovalDecision.NewSampleRequest,
            null, null, selectedTestOrderIds: new List<int> { tamc.Id }, newSampleAnalystOneId: 4, newSampleAnalystTwoId: 5);

        var reloadedSample = await db.Samples.FirstAsync(s => s.Id == sample.Id);
        Assert.Equal(SampleStatus.RetestRequested, reloadedSample.Status);
        Assert.Equal(ApprovalDecision.NewSampleRequest, reloadedSample.ApprovalDecision);

        var reloadedTamc = await db.TestOrders.FirstAsync(t => t.Id == tamc.Id);
        Assert.True(reloadedTamc.IsSuperseded);
        var reloadedTymc = await db.TestOrders.FirstAsync(t => t.Id == tymc.Id);
        Assert.False(reloadedTymc.IsSuperseded);

        var newSamples = await db.Samples.Where(s => s.OriginSampleId == sample.Id).ToListAsync();
        Assert.Equal(2, newSamples.Count);
        Assert.NotEqual(newSamples[0].ReferenceNumber, newSamples[1].ReferenceNumber);

        var assignedAnalysts = new List<int?>();
        foreach (var s in newSamples)
        {
            var order = await db.TestOrders.SingleAsync(t => t.SampleId == s.Id);
            Assert.Equal("TAMC", order.TestCode);
            assignedAnalysts.Add(order.AssignedAnalystId);
        }
        Assert.Equal(new List<int?> { 4, 5 }, assignedAnalysts.OrderBy(a => a).ToList());
    }

    [Fact]
    public async Task DecideAsync_NewSampleRequest_SameAnalystForBothNewSamples_Throws()
    {
        await using var db = NewDb();
        await SeedUser(db, 1);
        await SeedUser(db, 2);
        await SeedUser(db, 3);
        await SeedUser(db, 4, RoleType.Analyst);
        await SeedRetestCauseAsync(db);
        var (sample, tamc, _) = await SeedTwoTestSampleUnderApprovalAsync(db, analystId: 1, reviewedByUserId: 2);

        var approvalService = NewApprovalService(db);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            approvalService.DecideAsync(sample.Id, sectionHeadUserId: 3, Password, ApprovalDecision.NewSampleRequest,
                null, null, selectedTestOrderIds: new List<int> { tamc.Id }, newSampleAnalystOneId: 4, newSampleAnalystTwoId: 4));
        Assert.Contains("two different analysts", ex.Message);
    }

    [Fact]
    public async Task DecideAsync_NewSampleRequest_AnalystSameAsOriginalTester_Throws()
    {
        await using var db = NewDb();
        await SeedUser(db, 1, RoleType.Analyst); // original analyst - also Analyst-role, so it clears eligibility and must be caught by the segregation check instead
        await SeedUser(db, 2);
        await SeedUser(db, 3);
        await SeedUser(db, 5, RoleType.Analyst);
        await SeedRetestCauseAsync(db);
        var (sample, tamc, _) = await SeedTwoTestSampleUnderApprovalAsync(db, analystId: 1, reviewedByUserId: 2);

        var approvalService = NewApprovalService(db);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            approvalService.DecideAsync(sample.Id, sectionHeadUserId: 3, Password, ApprovalDecision.NewSampleRequest,
                null, null, selectedTestOrderIds: new List<int> { tamc.Id }, newSampleAnalystOneId: 1, newSampleAnalystTwoId: 5));
        Assert.Contains("may be the analyst who tested the original sample", ex.Message);
    }

    [Fact]
    public async Task DecideAsync_NewSampleRequest_NonAnalystRoleChosen_Throws()
    {
        await using var db = NewDb();
        await SeedUser(db, 1);
        await SeedUser(db, 2);
        await SeedUser(db, 3);
        await SeedUser(db, 4, RoleType.Analyst);
        await SeedUser(db, 6, RoleType.Reviewer); // not an Analyst - ineligible
        await SeedRetestCauseAsync(db);
        var (sample, tamc, _) = await SeedTwoTestSampleUnderApprovalAsync(db, analystId: 1, reviewedByUserId: 2);

        var approvalService = NewApprovalService(db);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            approvalService.DecideAsync(sample.Id, sectionHeadUserId: 3, Password, ApprovalDecision.NewSampleRequest,
                null, null, selectedTestOrderIds: new List<int> { tamc.Id }, newSampleAnalystOneId: 4, newSampleAnalystTwoId: 6));
        Assert.Contains("eligible Analyst-role users", ex.Message);
    }

    [Fact]
    public async Task DecideAsync_RetestRetainedSample_SelectedTestNotOnSample_Throws()
    {
        await using var db = NewDb();
        await SeedUser(db, 1);
        await SeedUser(db, 2);
        await SeedUser(db, 3);
        await SeedRetestCauseAsync(db);
        var (sample, _, _) = await SeedTwoTestSampleUnderApprovalAsync(db, analystId: 1, reviewedByUserId: 2);

        var approvalService = NewApprovalService(db);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            approvalService.DecideAsync(sample.Id, sectionHeadUserId: 3, Password, ApprovalDecision.RetestRetainedSample,
                null, null, selectedTestOrderIds: new List<int> { 999999 }));
        Assert.Contains("do not belong to this sample", ex.Message);
    }
}
