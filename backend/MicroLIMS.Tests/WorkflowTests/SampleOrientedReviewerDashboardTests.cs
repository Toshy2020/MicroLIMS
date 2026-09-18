using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.WorkflowTests;

public class SampleOrientedReviewerDashboardTests
{
    private static MicroLimsDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MicroLimsDbContext(options);
    }

    [Fact]
    public async Task ReviewerDashboard_SampleWithThreeTests_AppearsAsSingleQueueRowWithThreeTests()
    {
        await using var db = NewDb();
        var dashboardService = TestServiceFactory.Dashboard(db);

        var item = new Item { Name = "Amoxicillin 500mg" };
        db.Items.Add(item);
        db.TestDefinitions.AddRange(
            new TestDefinition { Code = "TAMC", DisplayName = "Total Aerobic Count" },
            new TestDefinition { Code = "TYMC", DisplayName = "Total Yeast/Mold Count" },
            new TestDefinition { Code = "EC", DisplayName = "E. Coli" }
        );
        await db.SaveChangesAsync();

        var sample = new Sample
        {
            ReferenceNumber = "SMP-001",
            Item = item,
            Status = SampleStatus.UnderReview,
            ReceivedAt = DateTime.UtcNow.AddHours(-3)
        };
        sample.TestOrders.Add(new TestOrder { TestCode = "TAMC", Status = ApprovalStatus.ResultEntered, CurrentStep = WorkflowStep.Ready });
        sample.TestOrders.Add(new TestOrder { TestCode = "TYMC", Status = ApprovalStatus.ResultEntered, CurrentStep = WorkflowStep.Ready });
        sample.TestOrders.Add(new TestOrder { TestCode = "EC", Status = ApprovalStatus.ResultEntered, CurrentStep = WorkflowStep.Ready });
        db.Samples.Add(sample);
        await db.SaveChangesAsync();

        var result = await dashboardService.GetReviewerDashboardAsync(reviewerUserId: 99);

        Assert.Equal(1, result.PendingReviewCount);
        var row = Assert.Single(result.ReviewQueue);
        Assert.Equal(sample.Id, row.SampleId);
        Assert.Equal("SMP-001", row.ReferenceNumber);
        Assert.Equal("Amoxicillin 500mg", row.SubjectName);
        Assert.Equal(3, row.Tests.Count);
        Assert.Contains(row.Tests, t => t.TestCode == "TAMC" && t.TestDisplayName == "Total Aerobic Count");
        Assert.Contains(row.Tests, t => t.TestCode == "TYMC" && t.TestDisplayName == "Total Yeast/Mold Count");
        Assert.Contains(row.Tests, t => t.TestCode == "EC" && t.TestDisplayName == "E. Coli");
    }

    [Fact]
    public async Task ReviewerDashboard_InTestingWithSomeReady_AndUnderApproval_AreExcluded()
    {
        await using var db = NewDb();
        var dashboardService = TestServiceFactory.Dashboard(db);

        // 1. InTesting sample with some tests Ready
        var s1 = new Sample { ReferenceNumber = "S1", Status = SampleStatus.InTesting };
        s1.TestOrders.Add(new TestOrder { TestCode = "TAMC", Status = ApprovalStatus.ResultEntered, CurrentStep = WorkflowStep.Ready });
        s1.TestOrders.Add(new TestOrder { TestCode = "TYMC", Status = ApprovalStatus.InProgress, CurrentStep = WorkflowStep.Running });

        // 2. UnderApproval sample
        var s2 = new Sample { ReferenceNumber = "S2", Status = SampleStatus.UnderApproval };
        s2.TestOrders.Add(new TestOrder { TestCode = "TAMC", Status = ApprovalStatus.Reviewed, CurrentStep = WorkflowStep.Reviewed });

        // 3. UnderReview sample
        var s3 = new Sample { ReferenceNumber = "S3", Status = SampleStatus.UnderReview };
        s3.TestOrders.Add(new TestOrder { TestCode = "TAMC", Status = ApprovalStatus.ResultEntered, CurrentStep = WorkflowStep.Ready });

        db.Samples.AddRange(s1, s2, s3);
        await db.SaveChangesAsync();

        var result = await dashboardService.GetReviewerDashboardAsync(reviewerUserId: 99);

        Assert.Equal(1, result.PendingReviewCount);
        var row = Assert.Single(result.ReviewQueue);
        Assert.Equal(s3.Id, row.SampleId);
    }

    [Fact]
    public async Task ReviewerDashboard_SubmittedForReviewAt_ResolvesWithFallbacks_AndIgnoresSuperseded()
    {
        await using var db = NewDb();
        var dashboardService = TestServiceFactory.Dashboard(db);
        var now = DateTime.UtcNow;

        // Sample 1: Has SubmittedForReview event
        var s1 = new Sample { ReferenceNumber = "S1", Status = SampleStatus.UnderReview, ReceivedAt = now.AddHours(-10) };
        var order1 = new TestOrder { TestCode = "T1", Status = ApprovalStatus.ResultEntered };
        s1.TestOrders.Add(order1);
        db.Samples.Add(s1);
        await db.SaveChangesAsync();

        db.ReviewWorkflowEvents.Add(new ReviewWorkflowEvent
        {
            EntityType = ReviewEntityTypes.Sample,
            EntityId = s1.Id,
            EventType = ReviewWorkflowEventType.SubmittedForReview,
            Timestamp = now.AddHours(-2)
        });
        db.WorkflowHistories.Add(new WorkflowHistory
        {
            TestOrderId = order1.Id,
            ToStep = WorkflowStep.Ready,
            Timestamp = now.AddHours(-5)
        });

        // Sample 2: No event; has WorkflowHistory Ready on non-superseded order
        var s2 = new Sample { ReferenceNumber = "S2", Status = SampleStatus.UnderReview, ReceivedAt = now.AddHours(-12) };
        var order2 = new TestOrder { TestCode = "T2", Status = ApprovalStatus.ResultEntered };
        s2.TestOrders.Add(order2);
        db.Samples.Add(s2);
        await db.SaveChangesAsync();

        db.WorkflowHistories.Add(new WorkflowHistory
        {
            TestOrderId = order2.Id,
            ToStep = WorkflowStep.Ready,
            Timestamp = now.AddHours(-4)
        });

        // Sample 3: No event; non-superseded order has NO Ready history; superseded order has Ready history (must be ignored)
        var s3 = new Sample { ReferenceNumber = "S3", Status = SampleStatus.UnderReview, ReceivedAt = now.AddHours(-8) };
        var order3Active = new TestOrder { TestCode = "T3", Status = ApprovalStatus.ResultEntered, IsSuperseded = false };
        var order3Superseded = new TestOrder { TestCode = "T3", Status = ApprovalStatus.RetestRequested, IsSuperseded = true };
        s3.TestOrders.Add(order3Active);
        s3.TestOrders.Add(order3Superseded);
        db.Samples.Add(s3);
        await db.SaveChangesAsync();

        db.WorkflowHistories.Add(new WorkflowHistory
        {
            TestOrderId = order3Superseded.Id,
            ToStep = WorkflowStep.Ready,
            Timestamp = now.AddHours(-1) // Superseded - must NOT be used!
        });

        // Sample 4: No event, no history; falls back to ReceivedAt
        var s4 = new Sample { ReferenceNumber = "S4", Status = SampleStatus.UnderReview, ReceivedAt = now.AddHours(-6) };
        var order4 = new TestOrder { TestCode = "T4", Status = ApprovalStatus.ResultEntered };
        s4.TestOrders.Add(order4);
        db.Samples.Add(s4);

        await db.SaveChangesAsync();

        var result = await dashboardService.GetReviewerDashboardAsync(reviewerUserId: 99);
        var queueMap = result.ReviewQueue.ToDictionary(r => r.SampleId);

        // s1: uses event timestamp (now - 2h)
        Assert.Equal(now.AddHours(-2).Ticks, queueMap[s1.Id].SubmittedForReviewAt.Ticks, tolerance: TimeSpan.FromSeconds(1).Ticks);

        // s2: uses non-superseded WorkflowHistory Ready timestamp (now - 4h)
        Assert.Equal(now.AddHours(-4).Ticks, queueMap[s2.Id].SubmittedForReviewAt.Ticks, tolerance: TimeSpan.FromSeconds(1).Ticks);

        // s3: ignores superseded history, falls back to ReceivedAt (now - 8h)
        Assert.Equal(now.AddHours(-8).Ticks, queueMap[s3.Id].SubmittedForReviewAt.Ticks, tolerance: TimeSpan.FromSeconds(1).Ticks);

        // s4: falls back to ReceivedAt (now - 6h)
        Assert.Equal(now.AddHours(-6).Ticks, queueMap[s4.Id].SubmittedForReviewAt.Ticks, tolerance: TimeSpan.FromSeconds(1).Ticks);
    }

    [Fact]
    public async Task ReviewerDashboard_WorstOf_ResultRecord_FlagsHighPriorityAndIgnoresSuperseded()
    {
        await using var db = NewDb();
        var dashboardService = TestServiceFactory.Dashboard(db);
        var now = DateTime.UtcNow;

        // Sample 1 (Fresh, but OOS -> High priority)
        var s1 = new Sample { ReferenceNumber = "S1", Status = SampleStatus.UnderReview, ReceivedAt = now.AddMinutes(-30) };
        var o1 = new TestOrder { TestCode = "TAMC", Status = ApprovalStatus.ResultEntered };
        var o2 = new TestOrder { TestCode = "TYMC", Status = ApprovalStatus.ResultEntered };
        s1.TestOrders.Add(o1);
        s1.TestOrders.Add(o2);
        db.Samples.Add(s1);
        await db.SaveChangesAsync();

        db.ResultRecords.Add(new ResultRecord { SampleId = s1.Id, TestOrderId = o1.Id, TestCode = "TAMC", ResultLevel = ResultLevel.WithinLimit });
        db.ResultRecords.Add(new ResultRecord { SampleId = s1.Id, TestOrderId = o2.Id, TestCode = "TYMC", ResultLevel = ResultLevel.OutOfSpecification });

        // Sample 2 (NotApplicable does not outrank WithinLimit)
        var s2 = new Sample { ReferenceNumber = "S2", Status = SampleStatus.UnderReview, ReceivedAt = now.AddMinutes(-30) };
        var o3 = new TestOrder { TestCode = "TAMC", Status = ApprovalStatus.ResultEntered };
        var o4 = new TestOrder { TestCode = "TYMC", Status = ApprovalStatus.ResultEntered };
        s2.TestOrders.Add(o3);
        s2.TestOrders.Add(o4);
        db.Samples.Add(s2);
        await db.SaveChangesAsync();

        db.ResultRecords.Add(new ResultRecord { SampleId = s2.Id, TestOrderId = o3.Id, TestCode = "TAMC", ResultLevel = ResultLevel.WithinLimit });
        db.ResultRecords.Add(new ResultRecord { SampleId = s2.Id, TestOrderId = o4.Id, TestCode = "TYMC", ResultLevel = ResultLevel.NotApplicable });

        // Sample 3 (Superseded order's OOS is ignored)
        var s3 = new Sample { ReferenceNumber = "S3", Status = SampleStatus.UnderReview, ReceivedAt = now.AddMinutes(-30) };
        var o5Active = new TestOrder { TestCode = "TAMC", Status = ApprovalStatus.ResultEntered, IsSuperseded = false };
        var o5Superseded = new TestOrder { TestCode = "TAMC", Status = ApprovalStatus.RetestRequested, IsSuperseded = true };
        s3.TestOrders.Add(o5Active);
        s3.TestOrders.Add(o5Superseded);
        db.Samples.Add(s3);
        await db.SaveChangesAsync();

        db.ResultRecords.Add(new ResultRecord { SampleId = s3.Id, TestOrderId = o5Active.Id, TestCode = "TAMC", ResultLevel = ResultLevel.WithinLimit });
        db.ResultRecords.Add(new ResultRecord { SampleId = s3.Id, TestOrderId = o5Superseded.Id, TestCode = "TAMC", ResultLevel = ResultLevel.OutOfSpecification });

        await db.SaveChangesAsync();

        var result = await dashboardService.GetReviewerDashboardAsync(reviewerUserId: 99);
        var queueMap = result.ReviewQueue.ToDictionary(r => r.SampleId);

        // Sample 1: OOS -> WorstResultLevel = OutOfSpecification, Priority = High
        Assert.Equal(nameof(ResultLevel.OutOfSpecification), queueMap[s1.Id].WorstResultLevel);
        Assert.Equal("High", queueMap[s1.Id].Priority);
        var attentionItem = Assert.Single(result.AttentionItems, a => a.SampleId == s1.Id);
        Assert.Contains("TYMC", attentionItem.Reason);
        Assert.Contains("Out of Specification result requires critical review", attentionItem.Reason);

        // Sample 2: WithinLimit (NotApplicable ignored) -> Normal priority
        Assert.Equal(nameof(ResultLevel.WithinLimit), queueMap[s2.Id].WorstResultLevel);
        Assert.Equal("Normal", queueMap[s2.Id].Priority);

        // Sample 3: WithinLimit (superseded OOS ignored) -> Normal priority
        Assert.Equal(nameof(ResultLevel.WithinLimit), queueMap[s3.Id].WorstResultLevel);
        Assert.Equal("Normal", queueMap[s3.Id].Priority);
    }

    [Fact]
    public async Task ReviewerDashboard_OverdueAndDueToday_CalculatedAgainst24HourBoundary()
    {
        await using var db = NewDb();
        var dashboardService = TestServiceFactory.Dashboard(db);
        var now = DateTime.UtcNow;

        var s1 = new Sample { ReferenceNumber = "SMP-FRESH", Status = SampleStatus.UnderReview, ReceivedAt = now.AddHours(-2) };
        s1.TestOrders.Add(new TestOrder { TestCode = "TAMC", Status = ApprovalStatus.ResultEntered });

        var s2 = new Sample { ReferenceNumber = "SMP-OVERDUE", Status = SampleStatus.UnderReview, ReceivedAt = now.AddHours(-25) };
        s2.TestOrders.Add(new TestOrder { TestCode = "TAMC", Status = ApprovalStatus.ResultEntered });

        var s3 = new Sample { ReferenceNumber = "SMP-DUE-TODAY", Status = SampleStatus.UnderReview, ReceivedAt = now.Date.AddHours(1) };
        s3.TestOrders.Add(new TestOrder { TestCode = "TAMC", Status = ApprovalStatus.ResultEntered });

        db.Samples.AddRange(s1, s2, s3);
        await db.SaveChangesAsync();

        var result = await dashboardService.GetReviewerDashboardAsync(reviewerUserId: 99);

        Assert.Equal(3, result.PendingReviewCount);
        Assert.Equal(1, result.OverdueReviewCount); // Only s2 is >= 1440 min
        Assert.Contains(result.ReviewQueue, r => r.SampleId == s2.Id && r.AgeMinutes >= 1440);
        Assert.Contains(result.ReviewQueue, r => r.SampleId == s1.Id && r.AgeMinutes < 1440);
        Assert.True(result.DueTodayCount >= 1);
    }

    [Fact]
    public async Task ReviewerDashboard_RetestsInProgressCount_CountsOpenSpinOffs_ExcludesClosedAndOrigin()
    {
        await using var db = NewDb();
        var dashboardService = TestServiceFactory.Dashboard(db);

        // 1. Origin sample (OriginSampleId = null, open) -> Excluded from retests
        var origin = new Sample { ReferenceNumber = "ORIGIN", Status = SampleStatus.InTesting, OriginSampleId = null };

        // 2. Spin-off in InTesting (open) -> Counted
        var retest1 = new Sample { ReferenceNumber = "RET-1", Status = SampleStatus.InTesting, OriginSampleId = 1 };

        // 3. Spin-off in UnderReview (open) -> Counted
        var retest2 = new Sample { ReferenceNumber = "RET-2", Status = SampleStatus.UnderReview, OriginSampleId = 1 };

        // 4. Spin-offs closed -> Excluded
        var retestApproved = new Sample { ReferenceNumber = "RET-3", Status = SampleStatus.Approved, OriginSampleId = 1 };
        var retestRejected = new Sample { ReferenceNumber = "RET-4", Status = SampleStatus.Rejected, OriginSampleId = 1 };
        var retestReq = new Sample { ReferenceNumber = "RET-5", Status = SampleStatus.RetestRequested, OriginSampleId = 1 };
        var retestCanc = new Sample { ReferenceNumber = "RET-6", Status = SampleStatus.Cancelled, OriginSampleId = 1 };
        var retestVoid = new Sample { ReferenceNumber = "RET-7", Status = SampleStatus.Voided, OriginSampleId = 1 };

        db.Samples.AddRange(origin, retest1, retest2, retestApproved, retestRejected, retestReq, retestCanc, retestVoid);
        await db.SaveChangesAsync();

        var result = await dashboardService.GetReviewerDashboardAsync(reviewerUserId: 99);

        Assert.Equal(2, result.RetestsInProgressCount);
    }

    [Fact]
    public async Task TestingWorkspaceService_WorkloadFilters_ReturnExpectedSamples()
    {
        await using var db = NewDb();
        var workspaceService = new TestingWorkspaceService(db, new UserSectionScopeService(db));
        var now = DateTime.UtcNow;

        var cause = new CauseOfTesting { Name = "Routine Release" };
        db.CausesOfTesting.Add(cause);
        await db.SaveChangesAsync();

        // Origin sample for retests
        var sOrigin = new Sample { ReferenceNumber = "ORIG", Status = SampleStatus.UnderReview, CauseOfTesting = cause, ReceivedAt = now };

        // Sample 1: Retest in progress
        var sRetest = new Sample { ReferenceNumber = "RETEST-IN-PROG", OriginSample = sOrigin, Status = SampleStatus.InTesting, CauseOfTesting = cause, ReceivedAt = now };

        // Sample 2: Retest closed
        var sRetestClosed = new Sample { ReferenceNumber = "RETEST-CLOSED", OriginSample = sOrigin, Status = SampleStatus.Approved, CauseOfTesting = cause, ReceivedAt = now };

        // Sample 3: Regular sample
        var sRegular = new Sample { ReferenceNumber = "REGULAR", OriginSampleId = null, Status = SampleStatus.InTesting, CauseOfTesting = cause, ReceivedAt = now };

        // Sample 4: UnderReview overdue (>24h)
        var sReviewOverdue = new Sample { ReferenceNumber = "REV-OD", Status = SampleStatus.UnderReview, CauseOfTesting = cause, ReceivedAt = now.AddHours(-25) };

        // Sample 5: UnderReview fresh (<24h)
        var sReviewFresh = new Sample { ReferenceNumber = "REV-FRESH", Status = SampleStatus.UnderReview, CauseOfTesting = cause, ReceivedAt = now.AddHours(-2) };

        // Sample 6: UnderApproval overdue (>24h)
        var sApprovalOverdue = new Sample { ReferenceNumber = "APP-OD", Status = SampleStatus.UnderApproval, CauseOfTesting = cause, ReviewedAt = now.AddHours(-26), ReceivedAt = now.AddHours(-30) };

        // Sample 7: UnderApproval fresh (<24h)
        var sApprovalFresh = new Sample { ReferenceNumber = "APP-FRESH", Status = SampleStatus.UnderApproval, CauseOfTesting = cause, ReviewedAt = now.AddHours(-2), ReceivedAt = now.AddHours(-5) };

        db.Samples.AddRange(sOrigin, sRetest, sRetestClosed, sRegular, sReviewOverdue, sReviewFresh, sApprovalOverdue, sApprovalFresh);
        await db.SaveChangesAsync();

        // 1. retestInProgress filter
        var resRetest = await workspaceService.GetActiveSamplesAsync(new TestingWorkspaceFilterDto { WorkloadFilter = "retestInProgress" });
        Assert.Single(resRetest.Items);
        Assert.Equal("RETEST-IN-PROG", resRetest.Items[0].ReferenceNumber);

        // 2. reviewOverdue filter
        var resRevOd = await workspaceService.GetActiveSamplesAsync(new TestingWorkspaceFilterDto { WorkloadFilter = "reviewOverdue" });
        Assert.Single(resRevOd.Items);
        Assert.Equal("REV-OD", resRevOd.Items[0].ReferenceNumber);

        // 3. approvalOverdue filter
        var resAppOd = await workspaceService.GetActiveSamplesAsync(new TestingWorkspaceFilterDto { WorkloadFilter = "approvalOverdue" });
        Assert.Single(resAppOd.Items);
        Assert.Equal("APP-OD", resAppOd.Items[0].ReferenceNumber);
    }

    [Fact]
    public async Task SampleWorkflowQueues_HelperUnitTests()
    {
        // 1. Severity ranking
        Assert.Equal(4, SampleWorkflowQueues.GetResultLevelSeverity(ResultLevel.OutOfSpecification));
        Assert.Equal(3, SampleWorkflowQueues.GetResultLevelSeverity(ResultLevel.ActionLevel));
        Assert.Equal(2, SampleWorkflowQueues.GetResultLevelSeverity(ResultLevel.AlertLevel));
        Assert.Equal(1, SampleWorkflowQueues.GetResultLevelSeverity(ResultLevel.WithinLimit));
        Assert.Null(SampleWorkflowQueues.GetResultLevelSeverity(ResultLevel.NotApplicable));
        Assert.Null(SampleWorkflowQueues.GetResultLevelSeverity(ResultLevel.LimitsNotConfigured));

        // 2. Worst-of ranking
        Assert.Equal(ResultLevel.ActionLevel, SampleWorkflowQueues.GetWorstResultLevel(new[] { ResultLevel.WithinLimit, ResultLevel.ActionLevel }));
        Assert.Equal(ResultLevel.WithinLimit, SampleWorkflowQueues.GetWorstResultLevel(new[] { ResultLevel.WithinLimit, ResultLevel.NotApplicable }));
        Assert.Null(SampleWorkflowQueues.GetWorstResultLevel(new[] { ResultLevel.NotApplicable, ResultLevel.LimitsNotConfigured }));
        Assert.Null(SampleWorkflowQueues.GetWorstResultLevel(Array.Empty<ResultLevel>()));

        // 3. Approval clock start
        var reviewedSample = new Sample { ReviewedAt = new DateTime(2026, 9, 10), ReceivedAt = new DateTime(2026, 9, 8) };
        Assert.Equal(new DateTime(2026, 9, 10), SampleWorkflowQueues.GetApprovalClockStart(reviewedSample));

        var unreviewedSample = new Sample { ReviewedAt = null, ReceivedAt = new DateTime(2026, 9, 8) };
        Assert.Equal(new DateTime(2026, 9, 8), SampleWorkflowQueues.GetApprovalClockStart(unreviewedSample));

        // 4. Review clock starts with empty list
        await using var db = NewDb();
        var emptyStarts = await SampleWorkflowQueues.GetReviewClockStartsAsync(db, Array.Empty<int>());
        Assert.Empty(emptyStarts);
    }

    [Fact]
    public async Task ReviewerDashboard_WorstOf_IgnoresResultRecordsOfInactiveCountReadings()
    {
        await using var db = NewDb();
        var dashboardService = TestServiceFactory.Dashboard(db);

        var sample = new Sample { ReferenceNumber = "S-RETURNED", Status = SampleStatus.UnderReview, ReceivedAt = DateTime.UtcNow.AddMinutes(-30) };
        var order = new TestOrder { TestCode = "TAMC", Status = ApprovalStatus.ResultEntered, CurrentStep = WorkflowStep.Ready };
        sample.TestOrders.Add(order);
        db.Samples.Add(sample);
        await db.SaveChangesAsync();

        // The original OOS reading was returned to the analyst (soft-superseded)
        // and the re-read is within limits - both readings keep projection rows.
        var returnedReading = new CountTestReading { TestOrderId = order.Id, PlateReadings = "900,910", ReportedResult = "905", Status = "OutOfSpecification", IsActive = false };
        var currentReading = new CountTestReading { TestOrderId = order.Id, PlateReadings = "25,25", ReportedResult = "25", Status = "WithinLimits", IsActive = true };
        db.CountTestReadings.AddRange(returnedReading, currentReading);
        await db.SaveChangesAsync();

        db.ResultRecords.AddRange(
            new ResultRecord { SampleId = sample.Id, TestOrderId = order.Id, TestCode = "TAMC", SourceTable = "CountTestReading", SourceId = returnedReading.Id, ResultLevel = ResultLevel.OutOfSpecification },
            new ResultRecord { SampleId = sample.Id, TestOrderId = order.Id, TestCode = "TAMC", SourceTable = "CountTestReading", SourceId = currentReading.Id, ResultLevel = ResultLevel.WithinLimit });
        await db.SaveChangesAsync();

        var result = await dashboardService.GetReviewerDashboardAsync(reviewerUserId: 99);

        var row = Assert.Single(result.ReviewQueue);
        Assert.Equal(nameof(ResultLevel.WithinLimit), row.WorstResultLevel);
        Assert.Equal(nameof(ResultLevel.WithinLimit), Assert.Single(row.Tests).ResultLevel);
        Assert.Equal("Normal", row.Priority);
        Assert.Empty(result.AttentionItems);
    }
}
