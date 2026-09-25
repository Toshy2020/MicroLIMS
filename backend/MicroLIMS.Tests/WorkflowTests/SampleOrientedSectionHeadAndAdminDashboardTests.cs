using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.WorkflowTests;

// Section Head / Admin dashboards, dashboard notifications and the workspace
// "Awaiting Review" tile count review and approval work per SAMPLE
// (UnderReview / UnderApproval), while bench tiles stay per TestOrder.
public class SampleOrientedSectionHeadAndAdminDashboardTests
{
    private static MicroLimsDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MicroLimsDbContext(options);
    }

    private static Sample NewSample(string reference, SampleStatus status, DateTime receivedAt,
        ApprovalStatus orderStatus, WorkflowStep orderStep, params string[] testCodes)
    {
        var sample = new Sample { ReferenceNumber = reference, Status = status, ReceivedAt = receivedAt };
        foreach (var code in testCodes)
            sample.TestOrders.Add(new TestOrder { TestCode = code, Status = orderStatus, CurrentStep = orderStep });
        return sample;
    }

    private static void AddSubmittedForReviewEvent(MicroLimsDbContext db, int sampleId, DateTime at) =>
        db.ReviewWorkflowEvents.Add(new ReviewWorkflowEvent
        {
            EntityType = ReviewEntityTypes.Sample,
            EntityId = sampleId,
            EventType = ReviewWorkflowEventType.SubmittedForReview,
            Timestamp = at
        });

    private static async Task<User> SeedUserAsync(MicroLimsDbContext db, string fullName, RoleType roleType)
    {
        var role = new Role { Type = roleType, Name = roleType.ToString() };
        db.Roles.Add(role);
        await db.SaveChangesAsync();

        var user = new User { FullName = fullName, Username = fullName.Replace(" ", "").ToLowerInvariant(), RoleId = role.Id, PasswordHash = "not-used" };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        TestServiceFactory.AssignUserToMicroSection(db, user.Id);
        return user;
    }

    [Fact]
    public async Task SectionHead_ReviewQueue_OneRowPerUnderReviewSample_ExcludesInTestingAndUnderApproval()
    {
        await using var db = NewDb();
        var now = DateTime.UtcNow;

        var underReview = NewSample("SMP-REV", SampleStatus.UnderReview, now.AddHours(-3), ApprovalStatus.ResultEntered, WorkflowStep.Ready, "TAMC", "TYMC", "EC");
        var inTesting = new Sample { ReferenceNumber = "SMP-TEST", Status = SampleStatus.InTesting, ReceivedAt = now.AddHours(-3) };
        inTesting.TestOrders.Add(new TestOrder { TestCode = "TAMC", Status = ApprovalStatus.ResultEntered, CurrentStep = WorkflowStep.Ready });
        inTesting.TestOrders.Add(new TestOrder { TestCode = "TYMC", Status = ApprovalStatus.InProgress, CurrentStep = WorkflowStep.Incubating });
        var underApproval = NewSample("SMP-APP", SampleStatus.UnderApproval, now.AddHours(-5), ApprovalStatus.Reviewed, WorkflowStep.Reviewed, "TAMC");
        db.Samples.AddRange(underReview, inTesting, underApproval);
        await db.SaveChangesAsync();

        var result = await TestServiceFactory.Dashboard(db).GetSectionHeadDashboardAsync();

        Assert.Equal(1, result.ReviewQueueCount);
        var row = Assert.Single(result.ReviewQueueItems);
        Assert.Equal(underReview.Id, row.SampleId);
        Assert.Equal(new[] { "EC", "TAMC", "TYMC" }, row.TestCodes.OrderBy(c => c));
    }

    [Fact]
    public async Task SectionHead_ApprovalQueue_OneRowPerUnderApprovalSample_ClockIsReviewedAtWithReceivedAtFallback()
    {
        await using var db = NewDb();
        var now = DateTime.UtcNow;
        var reviewer = await SeedUserAsync(db, "Rita Reviewer", RoleType.Reviewer);

        var reviewed = NewSample("SMP-APP-1", SampleStatus.UnderApproval, now.AddHours(-30), ApprovalStatus.Reviewed, WorkflowStep.Reviewed, "TAMC", "TYMC");
        reviewed.ReviewedAt = now.AddHours(-6);
        reviewed.ReviewedByUserId = reviewer.Id;
        var legacyNoReviewedAt = NewSample("SMP-APP-2", SampleStatus.UnderApproval, now.AddHours(-10), ApprovalStatus.Reviewed, WorkflowStep.Reviewed, "TYMC");
        db.Samples.AddRange(reviewed, legacyNoReviewedAt);
        await db.SaveChangesAsync();

        var result = await TestServiceFactory.Dashboard(db).GetSectionHeadDashboardAsync();

        Assert.Empty(result.ReviewQueueItems);
        Assert.Equal(2, result.ApprovalQueueCount);
        Assert.Equal(2, result.ApprovalQueueItems.Count);

        var reviewedRow = Assert.Single(result.ApprovalQueueItems, r => r.SampleId == reviewed.Id);
        Assert.Equal(reviewed.ReviewedAt!.Value, reviewedRow.ReviewedAt);
        Assert.Equal("Rita Reviewer", reviewedRow.ReviewerName);
        Assert.Equal(2, reviewedRow.TestCodes.Count);

        var legacyRow = Assert.Single(result.ApprovalQueueItems, r => r.SampleId == legacyNoReviewedAt.Id);
        Assert.Equal(legacyNoReviewedAt.ReceivedAt, legacyRow.ReviewedAt);
        Assert.Null(legacyRow.ReviewerName);
    }

    [Fact]
    public async Task SectionHead_PendingCountsAndBottlenecks_CountSamplesNotTests()
    {
        await using var db = NewDb();
        var now = DateTime.UtcNow;

        // Per-test counting would give 4 pending review and 2 pending approval.
        db.Samples.AddRange(
            NewSample("REV-A", SampleStatus.UnderReview, now.AddHours(-2), ApprovalStatus.ResultEntered, WorkflowStep.Ready, "TAMC", "TYMC", "EC"),
            NewSample("REV-B", SampleStatus.UnderReview, now.AddHours(-2), ApprovalStatus.ResultEntered, WorkflowStep.Ready, "TAMC"),
            NewSample("APP-C", SampleStatus.UnderApproval, now.AddHours(-2), ApprovalStatus.Reviewed, WorkflowStep.Reviewed, "TAMC", "TYMC"));
        await db.SaveChangesAsync();

        var result = await TestServiceFactory.Dashboard(db).GetSectionHeadDashboardAsync();

        Assert.Equal(2, result.PendingReview);
        Assert.Equal(2, result.ReviewBottleneck);
        Assert.Equal(1, result.PendingApproval);
        Assert.Equal(1, result.ApprovalBottleneck);
    }

    [Fact]
    public async Task SectionHead_EmptyQueues_ReportZeroOldestHours()
    {
        await using var db = NewDb();

        var result = await TestServiceFactory.Dashboard(db).GetSectionHeadDashboardAsync();

        Assert.Equal(0, result.ReviewQueueOldestHours);
        Assert.Equal(0, result.ApprovalQueueOldestHours);
        Assert.Equal(0, result.ReviewQueueOverdueCount);
        Assert.Equal(0, result.ApprovalQueueOverdueCount);
    }

    [Fact]
    public async Task SectionHead_OverdueCountsAndOldestHours_Use24HourReviewAndApprovalClocks()
    {
        await using var db = NewDb();
        var now = DateTime.UtcNow;

        // Received long ago on purpose: only the review/approval clocks may decide overdue.
        var reviewOld = NewSample("REV-OLD", SampleStatus.UnderReview, now.AddDays(-5), ApprovalStatus.ResultEntered, WorkflowStep.Ready, "TAMC");
        var reviewFresh = NewSample("REV-FRESH", SampleStatus.UnderReview, now.AddDays(-5), ApprovalStatus.ResultEntered, WorkflowStep.Ready, "TAMC");
        var approvalOld = NewSample("APP-OLD", SampleStatus.UnderApproval, now.AddDays(-5), ApprovalStatus.Reviewed, WorkflowStep.Reviewed, "TAMC");
        approvalOld.ReviewedAt = now.AddHours(-25);
        var approvalFresh = NewSample("APP-FRESH", SampleStatus.UnderApproval, now.AddDays(-5), ApprovalStatus.Reviewed, WorkflowStep.Reviewed, "TAMC");
        approvalFresh.ReviewedAt = now.AddHours(-2);
        db.Samples.AddRange(reviewOld, reviewFresh, approvalOld, approvalFresh);
        await db.SaveChangesAsync();

        AddSubmittedForReviewEvent(db, reviewOld.Id, now.AddHours(-25));
        AddSubmittedForReviewEvent(db, reviewFresh.Id, now.AddHours(-2));
        await db.SaveChangesAsync();

        var result = await TestServiceFactory.Dashboard(db).GetSectionHeadDashboardAsync();

        Assert.Equal(1, result.ReviewQueueOverdueCount);
        Assert.Equal(1, result.ApprovalQueueOverdueCount);

        var reviewOldRow = Assert.Single(result.ReviewQueueItems, r => r.SampleId == reviewOld.Id);
        Assert.InRange(reviewOldRow.AgeHours, 24.9, 25.1);
        Assert.Equal(reviewOldRow.AgeHours, result.ReviewQueueOldestHours);
        Assert.InRange(Assert.Single(result.ReviewQueueItems, r => r.SampleId == reviewFresh.Id).AgeHours, 1.9, 2.1);

        var approvalOldRow = Assert.Single(result.ApprovalQueueItems, r => r.SampleId == approvalOld.Id);
        Assert.InRange(approvalOldRow.AgeHours, 24.9, 25.1);
        Assert.Equal(approvalOldRow.AgeHours, result.ApprovalQueueOldestHours);
    }

    [Fact]
    public async Task SectionHead_ActiveTests_StillCountsNonSupersededPendingOrInProgressTestOrders()
    {
        await using var db = NewDb();
        var now = DateTime.UtcNow;

        var inTesting = new Sample { ReferenceNumber = "BENCH-1", Status = SampleStatus.InTesting, ReceivedAt = now.AddHours(-4) };
        inTesting.TestOrders.Add(new TestOrder { TestCode = "TAMC", Status = ApprovalStatus.InProgress, CurrentStep = WorkflowStep.Incubating });
        inTesting.TestOrders.Add(new TestOrder { TestCode = "TYMC", Status = ApprovalStatus.InProgress, CurrentStep = WorkflowStep.Running });
        inTesting.TestOrders.Add(new TestOrder { TestCode = "EC", Status = ApprovalStatus.InProgress, CurrentStep = WorkflowStep.Running, IsSuperseded = true });
        var underReview = NewSample("REV-1", SampleStatus.UnderReview, now.AddHours(-4), ApprovalStatus.ResultEntered, WorkflowStep.Ready, "TAMC");
        db.Samples.AddRange(inTesting, underReview);
        await db.SaveChangesAsync();

        var result = await TestServiceFactory.Dashboard(db).GetSectionHeadDashboardAsync();

        Assert.Equal(2, result.ActiveTests);
        Assert.Equal(2, result.TestingBottleneck);
    }

    [Fact]
    public async Task SectionHead_Attention_DelayedReviewAndDelayedApproval_OnePerOverdueSample()
    {
        await using var db = NewDb();
        var now = DateTime.UtcNow;

        var reviewOld = NewSample("REV-LATE", SampleStatus.UnderReview, now.AddDays(-3), ApprovalStatus.ResultEntered, WorkflowStep.Ready, "TAMC");
        var approvalOld = NewSample("APP-LATE", SampleStatus.UnderApproval, now.AddDays(-3), ApprovalStatus.Reviewed, WorkflowStep.Reviewed, "TAMC", "TYMC");
        approvalOld.ReviewedAt = now.AddHours(-30);
        db.Samples.AddRange(reviewOld, approvalOld);
        await db.SaveChangesAsync();

        AddSubmittedForReviewEvent(db, reviewOld.Id, now.AddHours(-30));
        await db.SaveChangesAsync();

        var result = await TestServiceFactory.Dashboard(db).GetSectionHeadDashboardAsync();

        var delayedReview = Assert.Single(result.AttentionItems, a => a.ActionType == "DelayedReview");
        Assert.Equal(reviewOld.Id, delayedReview.SampleId);
        Assert.Equal("Medium", delayedReview.Urgency);
        Assert.Equal(new[] { "TAMC" }, delayedReview.TestCodes);

        var delayedApproval = Assert.Single(result.AttentionItems, a => a.ActionType == "DelayedApproval");
        Assert.Equal(approvalOld.Id, delayedApproval.SampleId);
        Assert.Equal("Medium", delayedApproval.Urgency);
        Assert.StartsWith("Final approval delayed by", delayedApproval.Reason);
        Assert.Equal(new[] { "TAMC", "TYMC" }, delayedApproval.TestCodes.OrderBy(c => c));

        Assert.Equal(2, result.AttentionCount);
    }

    [Fact]
    public async Task SectionHead_Attention_RetestRequired_OnlyForInProgressSpinOffs_WithOriginReference()
    {
        await using var db = NewDb();
        var now = DateTime.UtcNow;

        var origin = NewSample("ORIG-001", SampleStatus.RetestRequested, now.AddDays(-4), ApprovalStatus.Reviewed, WorkflowStep.Reviewed, "TAMC");
        db.Samples.Add(origin);
        await db.SaveChangesAsync();

        var spinInTesting = NewSample("RET-OPEN", SampleStatus.InTesting, now.AddHours(-6), ApprovalStatus.InProgress, WorkflowStep.Incubating, "TAMC");
        spinInTesting.OriginSampleId = origin.Id;
        var spinApproved = NewSample("RET-DONE", SampleStatus.Approved, now.AddHours(-8), ApprovalStatus.Approved, WorkflowStep.Approved, "TAMC");
        spinApproved.OriginSampleId = origin.Id;
        db.Samples.AddRange(spinInTesting, spinApproved);
        await db.SaveChangesAsync();

        var result = await TestServiceFactory.Dashboard(db).GetSectionHeadDashboardAsync();

        var retestItem = Assert.Single(result.AttentionItems, a => a.ActionType == "RetestRequired");
        Assert.Equal(spinInTesting.Id, retestItem.SampleId);
        Assert.Contains("ORIG-001", retestItem.Reason);
        Assert.Equal(new[] { "TAMC" }, retestItem.TestCodes);
        Assert.Equal(spinInTesting.ReceivedAt, retestItem.Timestamp);
    }

    [Fact]
    public async Task SectionHead_AttentionCount_UsesFullRetestCount_WhileItemsAreCappedAtFive()
    {
        await using var db = NewDb();
        var now = DateTime.UtcNow;

        var origin = NewSample("ORIG-MANY", SampleStatus.RetestRequested, now.AddDays(-4), ApprovalStatus.Reviewed, WorkflowStep.Reviewed, "TAMC");
        db.Samples.Add(origin);
        await db.SaveChangesAsync();

        // No TestOrder has an AssignedAnalystId, so KpiService's 7-day
        // analyst-stage window set is empty and Overdue cannot contribute.
        for (var i = 1; i <= 7; i++)
        {
            var spinOff = NewSample($"RET-{i}", SampleStatus.InTesting, now.AddHours(-i), ApprovalStatus.InProgress, WorkflowStep.Incubating, "TAMC");
            spinOff.OriginSampleId = origin.Id;
            db.Samples.Add(spinOff);
        }
        await db.SaveChangesAsync();

        var result = await TestServiceFactory.Dashboard(db).GetSectionHeadDashboardAsync();

        Assert.Equal(0, result.Overdue);
        Assert.Equal(5, result.AttentionItems.Count(a => a.ActionType == "RetestRequired"));
        Assert.Equal(7, result.AttentionCount);
    }

    [Fact]
    public async Task AdminSummary_ReviewerAndApprovalQueues_CountSamples()
    {
        await using var db = NewDb();
        var now = DateTime.UtcNow;

        var inTestingWithResult = new Sample { ReferenceNumber = "TEST-1", Status = SampleStatus.InTesting, ReceivedAt = now.AddHours(-3) };
        inTestingWithResult.TestOrders.Add(new TestOrder { TestCode = "TAMC", Status = ApprovalStatus.ResultEntered, CurrentStep = WorkflowStep.Ready });
        inTestingWithResult.TestOrders.Add(new TestOrder { TestCode = "TYMC", Status = ApprovalStatus.InProgress, CurrentStep = WorkflowStep.Incubating });
        db.Samples.AddRange(
            NewSample("REV-1", SampleStatus.UnderReview, now.AddHours(-3), ApprovalStatus.ResultEntered, WorkflowStep.Ready, "TAMC", "TYMC", "EC"),
            NewSample("APP-1", SampleStatus.UnderApproval, now.AddHours(-3), ApprovalStatus.Reviewed, WorkflowStep.Reviewed, "TAMC", "TYMC"),
            inTestingWithResult);
        await db.SaveChangesAsync();

        var summary = await TestServiceFactory.Dashboard(db).GetSummaryAsync(RoleType.SystemAdministrator, 1);

        Assert.Equal(1, (int)summary.GetType().GetProperty("reviewerQueue")!.GetValue(summary)!);
        Assert.Equal(1, (int)summary.GetType().GetProperty("approvalQueue")!.GetValue(summary)!);
    }

    [Fact]
    public async Task Notifications_ReviewAndApprovalWaiting_CountSamples_AndAreNotShownToAnalysts()
    {
        await using var db = NewDb();
        var now = DateTime.UtcNow;
        var sectionHead = await SeedUserAsync(db, "Sam Head", RoleType.SectionHead);
        var analyst = await SeedUserAsync(db, "Ana Lyst", RoleType.Analyst);

        db.Samples.AddRange(
            NewSample("REV-1", SampleStatus.UnderReview, now.AddHours(-3), ApprovalStatus.ResultEntered, WorkflowStep.Ready, "TAMC", "TYMC", "EC"),
            NewSample("REV-2", SampleStatus.UnderReview, now.AddHours(-3), ApprovalStatus.ResultEntered, WorkflowStep.Ready, "TAMC"),
            NewSample("APP-1", SampleStatus.UnderApproval, now.AddHours(-3), ApprovalStatus.Reviewed, WorkflowStep.Reviewed, "TAMC", "TYMC"));
        await db.SaveChangesAsync();
        foreach (var order in db.TestOrders)
            order.SectionId = TestServiceFactory.EnsureMicroSection(db).Id;
        await db.SaveChangesAsync();

        var service = TestServiceFactory.DashboardNotification(db);

        var sectionHeadNotifications = await service.GetNotificationsAsync(RoleType.SectionHead, sectionHead.Id);
        Assert.Contains(sectionHeadNotifications, n => n.Type == "ReviewWaiting" && n.Message == "2 sample(s) awaiting review.");
        Assert.Contains(sectionHeadNotifications, n => n.Type == "ApprovalWaiting" && n.Message == "1 sample(s) awaiting approval.");

        var analystNotifications = await service.GetNotificationsAsync(RoleType.Analyst, analyst.Id);
        Assert.DoesNotContain(analystNotifications, n => n.Type == "ReviewWaiting" || n.Type == "ApprovalWaiting");
    }

    [Fact]
    public async Task Workspace_AwaitingReview_TileCountAndFilter_OnlyUnderReviewSamples()
    {
        await using var db = NewDb();
        var now = DateTime.UtcNow;

        var cause = new CauseOfTesting { Name = "Routine Release" };
        db.CausesOfTesting.Add(cause);
        await db.SaveChangesAsync();

        var underReview = NewSample("REV-ONLY", SampleStatus.UnderReview, now.AddHours(-2), ApprovalStatus.ResultEntered, WorkflowStep.Ready, "TAMC");
        underReview.CauseOfTesting = cause;
        var inTestingWithReadyTest = new Sample { ReferenceNumber = "STILL-TESTING", Status = SampleStatus.InTesting, ReceivedAt = now.AddHours(-2), CauseOfTesting = cause };
        inTestingWithReadyTest.TestOrders.Add(new TestOrder { TestCode = "TAMC", Status = ApprovalStatus.ResultEntered, CurrentStep = WorkflowStep.Ready });
        inTestingWithReadyTest.TestOrders.Add(new TestOrder { TestCode = "TYMC", Status = ApprovalStatus.InProgress, CurrentStep = WorkflowStep.Incubating });
        db.Samples.AddRange(underReview, inTestingWithReadyTest);
        await db.SaveChangesAsync();

        var service = new TestingWorkspaceService(db, new UserSectionScopeService(db));

        var counts = await service.GetWorkloadCountsAsync();
        Assert.Equal(1, counts.AwaitingReview);

        var filtered = await service.GetActiveSamplesAsync(new TestingWorkspaceFilterDto { WorkloadFilter = "awaitingReview" });
        var item = Assert.Single(filtered.Items);
        Assert.Equal("REV-ONLY", item.ReferenceNumber);
    }

    [Fact]
    public async Task Notifications_IncubationReady_NamesTestStepAndSample()
    {
        await using var db = NewDb();
        var analyst = await SeedUserAsync(db, "Ana Lyst", RoleType.Analyst);

        var sample = NewSample("FP0107026", SampleStatus.InTesting, DateTime.UtcNow.AddDays(-2), ApprovalStatus.InProgress, WorkflowStep.Incubating, "TAMC");
        db.Samples.Add(sample);
        await db.SaveChangesAsync();
        foreach (var order in db.TestOrders)
            order.SectionId = TestServiceFactory.EnsureMicroSection(db).Id;
        await db.SaveChangesAsync();

        db.Incubations.Add(new Incubation
        {
            TestOrderId = sample.TestOrders.Single().Id,
            StepName = "Stage 1",
            StartedAt = DateTime.UtcNow.AddDays(-2),
            CompletedAt = DateTime.UtcNow.AddMinutes(-5)
        });
        await db.SaveChangesAsync();

        var notifications = await TestServiceFactory.DashboardNotification(db).GetNotificationsAsync(RoleType.Analyst, analyst.Id);

        var ready = Assert.Single(notifications, n => n.Type == "IncubationReady");
        Assert.Equal("TAMC (Stage 1) for sample FP0107026 is ready.", ready.Message);
        Assert.Equal(sample.Id, ready.SampleId);
        Assert.Equal(sample.TestOrders.Single().Id, ready.TestOrderId);
    }
}
