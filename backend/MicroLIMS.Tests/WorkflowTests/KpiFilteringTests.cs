using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.WorkflowTests;

public class KpiFilteringTests
{
    private static MicroLimsDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MicroLimsDbContext(options);
    }

    [Fact]
    public async Task GetCompletionStatsAsync_FiltersCorrectlyByCategoryLocationAndTestCode()
    {
        await using var db = NewDb();

        var itemA = new Item { Id = 101, Name = "Product A", Code = "PA-01" };
        var itemB = new Item { Id = 102, Name = "Product B", Code = "PB-01" };
        db.Items.AddRange(itemA, itemB);

        var sampleA = new Sample
        {
            Id = 1,
            Category = SampleCategory.FinishedProduct,
            Item = itemA,
            ItemId = itemA.Id,
            ControlNumber = "FP-001",
            Status = SampleStatus.Approved
        };
        sampleA.TestOrders.Add(new TestOrder { Id = 1, TestCode = "TAMC", Status = ApprovalStatus.Approved, CurrentStep = WorkflowStep.Approved });
        sampleA.TestOrders.Add(new TestOrder { Id = 2, TestCode = "TAMC", Status = ApprovalStatus.Rejected, CurrentStep = WorkflowStep.Approved });

        var sampleB = new Sample
        {
            Id = 2,
            Category = SampleCategory.RawMaterial,
            Item = itemB,
            ItemId = itemB.Id,
            ControlNumber = "RM-001",
            Status = SampleStatus.InTesting
        };
        sampleB.TestOrders.Add(new TestOrder { Id = 3, TestCode = "TYMC", Status = ApprovalStatus.Approved, CurrentStep = WorkflowStep.Approved });
        sampleB.TestOrders.Add(new TestOrder { Id = 4, TestCode = "TYMC", Status = ApprovalStatus.Pending, CurrentStep = WorkflowStep.Waiting });

        db.Samples.AddRange(sampleA, sampleB);
        await db.SaveChangesAsync();

        var service = new KpiService(db);

        // 1. Unfiltered
        var allStats = await service.GetCompletionStatsAsync();
        Assert.Equal(4, allStats.TotalTestOrders);
        Assert.Equal(2, allStats.Approved);
        Assert.Equal(1, allStats.Rejected);
        Assert.Equal(1, allStats.Pending);

        // 2. Filtered by Category
        var fpStats = await service.GetCompletionStatsAsync(category: SampleCategory.FinishedProduct);
        Assert.Equal(2, fpStats.TotalTestOrders);
        Assert.Equal(1, fpStats.Approved);
        Assert.Equal(1, fpStats.Rejected);
        Assert.Equal(0, fpStats.Pending);

        // 3. Filtered by Location
        var locStats = await service.GetCompletionStatsAsync(location: "Product B");
        Assert.Equal(2, locStats.TotalTestOrders);
        Assert.Equal(1, locStats.Approved);
        Assert.Equal(0, locStats.Rejected);
        Assert.Equal(1, locStats.Pending);

        // 4. Filtered by TestCode
        var testCodeStats = await service.GetCompletionStatsAsync(testCode: "TAMC");
        Assert.Equal(2, testCodeStats.TotalTestOrders);
        Assert.Equal(1, testCodeStats.Approved);
        Assert.Equal(1, testCodeStats.Rejected);
        Assert.Equal(0, testCodeStats.Pending);
    }

    [Fact]
    public async Task GetStageTatSummaryAsync_FiltersCorrectlyByCategoryLocationAndTestCode()
    {
        await using var db = NewDb();

        var now = DateTime.UtcNow;
        var assignedTime = now.AddDays(-10);

        var itemA = new Item { Id = 201, Name = "Batch Alpha", Code = "BA-01" };
        var itemB = new Item { Id = 202, Name = "Batch Beta", Code = "BB-01" };
        db.Items.AddRange(itemA, itemB);

        var sampleA = new Sample
        {
            Id = 10,
            Category = SampleCategory.FinishedProduct,
            Item = itemA,
            ItemId = itemA.Id,
            ControlNumber = "FP-010",
            Status = SampleStatus.Approved
        };
        sampleA.TestOrders.Add(new TestOrder { Id = 10, AssignedAnalystId = 1, TestCode = "TAMC", Status = ApprovalStatus.Approved, CurrentStep = WorkflowStep.Approved });

        var sampleB = new Sample
        {
            Id = 20,
            Category = SampleCategory.RawMaterial,
            Item = itemB,
            ItemId = itemB.Id,
            ControlNumber = "RM-020",
            Status = SampleStatus.Approved
        };
        sampleB.TestOrders.Add(new TestOrder { Id = 20, AssignedAnalystId = 1, TestCode = "TYMC", Status = ApprovalStatus.Approved, CurrentStep = WorkflowStep.Approved });

        db.Samples.AddRange(sampleA, sampleB);

        db.SamplePreparations.Add(new SamplePreparation { SampleId = 10, PreparedAt = assignedTime, PreparedByUserId = 1 });
        db.SamplePreparations.Add(new SamplePreparation { SampleId = 20, PreparedAt = assignedTime, PreparedByUserId = 1 });

        // Sample A Testing TAT: 48h (2.0 days)
        db.ReviewWorkflowEvents.Add(new ReviewWorkflowEvent
        {
            EntityType = ReviewEntityTypes.Sample,
            EntityId = 10,
            EventType = ReviewWorkflowEventType.SubmittedForReview,
            Timestamp = assignedTime.AddHours(48),
            PerformedByUserId = 1
        });

        // Sample B Testing TAT: 96h (4.0 days)
        db.ReviewWorkflowEvents.Add(new ReviewWorkflowEvent
        {
            EntityType = ReviewEntityTypes.Sample,
            EntityId = 20,
            EventType = ReviewWorkflowEventType.SubmittedForReview,
            Timestamp = assignedTime.AddHours(96),
            PerformedByUserId = 1
        });

        await db.SaveChangesAsync();

        var service = new KpiService(db);
        var fromDate = now.AddDays(-20);
        var toDate = now.AddDays(1);

        // 1. Unfiltered: average of 2.0 and 4.0 days = 3.0 days
        var allTat = await service.GetStageTatSummaryAsync(1, fromDate, toDate);
        Assert.Equal(3.0, allTat.TestingAvgDays);

        // 2. Filtered by Category: FinishedProduct only -> 2.0 days
        var fpTat = await service.GetStageTatSummaryAsync(1, fromDate, toDate, category: SampleCategory.FinishedProduct);
        Assert.Equal(2.0, fpTat.TestingAvgDays);

        // 3. Filtered by Location: Batch Beta only -> 4.0 days
        var locTat = await service.GetStageTatSummaryAsync(1, fromDate, toDate, location: "Batch Beta");
        Assert.Equal(4.0, locTat.TestingAvgDays);

        // 4. Filtered by TestCode: TAMC only -> 2.0 days
        var codeTat = await service.GetStageTatSummaryAsync(1, fromDate, toDate, testCode: "TAMC");
        Assert.Equal(2.0, codeTat.TestingAvgDays);
    }

    [Fact]
    public async Task GetSampleQueueCountsAsync_FiltersByCategoryAndLocation()
    {
        await using var db = NewDb();

        var itemA = new Item { Id = 301, Name = "Item A", Code = "IA-01" };
        var itemB = new Item { Id = 302, Name = "Item B", Code = "IB-01" };
        db.Items.AddRange(itemA, itemB);

        var s1 = new Sample { Id = 31, Category = SampleCategory.FinishedProduct, Item = itemA, ItemId = itemA.Id, ControlNumber = "C1", Status = SampleStatus.UnderReview };
        var s2 = new Sample { Id = 32, Category = SampleCategory.FinishedProduct, Item = itemB, ItemId = itemB.Id, ControlNumber = "C2", Status = SampleStatus.UnderApproval };
        var s3 = new Sample { Id = 33, Category = SampleCategory.RawMaterial, Item = itemB, ItemId = itemB.Id, ControlNumber = "C3", Status = SampleStatus.UnderReview };

        db.Samples.AddRange(s1, s2, s3);
        await db.SaveChangesAsync();

        var service = new KpiService(db);

        // Unfiltered: 2 review, 1 approval
        var allQueues = await service.GetSampleQueueCountsAsync();
        Assert.Equal(2, allQueues.ReviewQueueCount);
        Assert.Equal(1, allQueues.ApprovalQueueCount);

        // Filtered by FinishedProduct: 1 review, 1 approval
        var fpQueues = await service.GetSampleQueueCountsAsync(category: SampleCategory.FinishedProduct);
        Assert.Equal(1, fpQueues.ReviewQueueCount);
        Assert.Equal(1, fpQueues.ApprovalQueueCount);

        // Filtered by Location Item A: 1 review, 0 approval
        var locQueues = await service.GetSampleQueueCountsAsync(location: "Item A");
        Assert.Equal(1, locQueues.ReviewQueueCount);
        Assert.Equal(0, locQueues.ApprovalQueueCount);
    }

    [Fact]
    public async Task GetDelayTrackingAsync_FiltersByCategoryLocationAndTestCode()
    {
        await using var db = NewDb();

        var itemA = new Item { Id = 401, Name = "Item A", Code = "IA-02" };
        var itemB = new Item { Id = 402, Name = "Item B", Code = "IB-02" };
        db.Items.AddRange(itemA, itemB);

        // Both received 48h ago (cutoff is 24h ago)
        var receivedOld = DateTime.UtcNow.AddHours(-48);
        var s1 = new Sample { Id = 41, Category = SampleCategory.FinishedProduct, Item = itemA, ItemId = itemA.Id, ReceivedAt = receivedOld, ControlNumber = "C41", Status = SampleStatus.Received };
        var s2 = new Sample { Id = 42, Category = SampleCategory.RawMaterial, Item = itemB, ItemId = itemB.Id, ReceivedAt = receivedOld, ControlNumber = "C42", Status = SampleStatus.Received };

        s1.TestOrders.Add(new TestOrder { Id = 41, TestCode = "TAMC", Status = ApprovalStatus.Pending, CurrentStep = WorkflowStep.Waiting });
        s2.TestOrders.Add(new TestOrder { Id = 42, TestCode = "TYMC", Status = ApprovalStatus.Pending, CurrentStep = WorkflowStep.Waiting });

        db.Samples.AddRange(s1, s2);
        await db.SaveChangesAsync();

        var service = new KpiService(db);

        var allDelay = await service.GetDelayTrackingAsync();
        Assert.Equal(2, allDelay.DelayedCount);

        var fpDelay = await service.GetDelayTrackingAsync(category: SampleCategory.FinishedProduct);
        Assert.Equal(1, fpDelay.DelayedCount);

        var locDelay = await service.GetDelayTrackingAsync(location: "Item B");
        Assert.Equal(1, locDelay.DelayedCount);

        var codeDelay = await service.GetDelayTrackingAsync(testCode: "TAMC");
        Assert.Equal(1, codeDelay.DelayedCount);
    }

    [Fact]
    public async Task GetStepViolationsAsync_FiltersByCategoryLocationAndTestCode()
    {
        await using var db = NewDb();

        var now = DateTime.UtcNow;
        var itemA = new Item { Id = 501, Name = "Item A", Code = "IA-03" };
        var itemB = new Item { Id = 502, Name = "Item B", Code = "IB-03" };
        db.Items.AddRange(itemA, itemB);

        var s1 = new Sample { Id = 51, Category = SampleCategory.FinishedProduct, Item = itemA, ItemId = itemA.Id, ControlNumber = "C51", Status = SampleStatus.InTesting };
        var s2 = new Sample { Id = 52, Category = SampleCategory.RawMaterial, Item = itemB, ItemId = itemB.Id, ControlNumber = "C52", Status = SampleStatus.InTesting };

        var t1 = new TestOrder { Id = 51, SampleId = 51, TestCode = "TAMC", AssignedAnalystId = 1, Status = ApprovalStatus.InProgress, CurrentStep = WorkflowStep.Running };
        var t2 = new TestOrder { Id = 52, SampleId = 52, TestCode = "TYMC", AssignedAnalystId = 1, Status = ApprovalStatus.InProgress, CurrentStep = WorkflowStep.Running };
        s1.TestOrders.Add(t1);
        s2.TestOrders.Add(t2);

        db.Samples.AddRange(s1, s2);

        // Incubation 1: completed 6 hours after ExpectedReadingAt (violates 4h threshold)
        var inc1 = new Incubation
        {
            TestOrderId = 51,
            TestOrder = t1,
            StartedByUserId = 1,
            ExpectedReadingAt = now.AddHours(-10),
            CompletedAt = now.AddHours(-4) // 6 hours past ExpectedReadingAt
        };

        // Incubation 2: completed on time (2 hours after ExpectedReadingAt, within 4h grace)
        var inc2 = new Incubation
        {
            TestOrderId = 52,
            TestOrder = t2,
            StartedByUserId = 1,
            ExpectedReadingAt = now.AddHours(-10),
            CompletedAt = now.AddHours(-8) // 2 hours past ExpectedReadingAt
        };

        db.Incubations.AddRange(inc1, inc2);
        await db.SaveChangesAsync();

        var service = new KpiService(db);
        var fromDate = now.AddDays(-1);
        var toDate = now.AddDays(1);

        var allViolations = await service.GetStepViolationsAsync(1, fromDate, toDate);
        Assert.Equal(2, allViolations.TotalAssignedTests);
        Assert.Equal(1, allViolations.ViolationCount);

        var fpViolations = await service.GetStepViolationsAsync(1, fromDate, toDate, category: SampleCategory.FinishedProduct);
        Assert.Equal(1, fpViolations.TotalAssignedTests);
        Assert.Equal(1, fpViolations.ViolationCount);

        var rmViolations = await service.GetStepViolationsAsync(1, fromDate, toDate, category: SampleCategory.RawMaterial);
        Assert.Equal(1, rmViolations.TotalAssignedTests);
        Assert.Equal(0, rmViolations.ViolationCount);

        var locViolations = await service.GetStepViolationsAsync(1, fromDate, toDate, location: "Item A");
        Assert.Equal(1, locViolations.TotalAssignedTests);
        Assert.Equal(1, locViolations.ViolationCount);

        var codeViolations = await service.GetStepViolationsAsync(1, fromDate, toDate, testCode: "TYMC");
        Assert.Equal(1, codeViolations.TotalAssignedTests);
        Assert.Equal(0, codeViolations.ViolationCount);
    }

    [Fact]
    public async Task GetOverdueAnalystStageSamplesAsync_RegressionCheck_ReturnsAllOverdueWithoutFilters()
    {
        await using var db = NewDb();

        var now = DateTime.UtcNow;
        var overdueAssigned = now.AddDays(-10); // > 7 days ago

        var s1 = new Sample { Id = 61, Category = SampleCategory.FinishedProduct, ControlNumber = "C61", Status = SampleStatus.InTesting };
        var s2 = new Sample { Id = 62, Category = SampleCategory.RawMaterial, ControlNumber = "C62", Status = SampleStatus.InTesting };
        s1.TestOrders.Add(new TestOrder { Id = 61, AssignedAnalystId = 1, TestCode = "TAMC", Status = ApprovalStatus.InProgress, CurrentStep = WorkflowStep.Running });
        s2.TestOrders.Add(new TestOrder { Id = 62, AssignedAnalystId = 2, TestCode = "TYMC", Status = ApprovalStatus.InProgress, CurrentStep = WorkflowStep.Running });

        db.Samples.AddRange(s1, s2);
        db.SamplePreparations.Add(new SamplePreparation { SampleId = 61, PreparedAt = overdueAssigned, PreparedByUserId = 1 });
        db.SamplePreparations.Add(new SamplePreparation { SampleId = 62, PreparedAt = overdueAssigned, PreparedByUserId = 2 });
        await db.SaveChangesAsync();

        var service = new KpiService(db);
        var overdue = await service.GetOverdueAnalystStageSamplesAsync(now.AddDays(-30), now);

        // Must return both overdue samples across all categories/analysts
        Assert.Equal(2, overdue.Count);
        Assert.Contains(overdue, o => o.SampleId == 61);
        Assert.Contains(overdue, o => o.SampleId == 62);
    }

    [Fact]
    public async Task GetReturnToAnalystCountAsync_FiltersCorrectlyByDateRangeAndAnalyst()
    {
        await using var db = NewDb();

        var now = DateTime.UtcNow;
        var inRangeDate = now.AddDays(-5);
        var beforeRangeDate = now.AddDays(-40);
        var afterRangeDate = now.AddDays(5);

        var fromDate = now.AddDays(-30);
        var toDate = now;

        // Analyst 1: 2 events in range, 1 before range, 1 after range
        db.TestReturnEvents.Add(new TestReturnEvent { Id = 1, TestOrderId = 101, AssignedAnalystId = 1, ReviewerUserId = 99, ReturnedAt = inRangeDate, Reason = "Recount 1" });
        db.TestReturnEvents.Add(new TestReturnEvent { Id = 2, TestOrderId = 102, AssignedAnalystId = 1, ReviewerUserId = 99, ReturnedAt = inRangeDate.AddDays(1), Reason = "Recount 2" });
        db.TestReturnEvents.Add(new TestReturnEvent { Id = 3, TestOrderId = 103, AssignedAnalystId = 1, ReviewerUserId = 99, ReturnedAt = beforeRangeDate, Reason = "Old event" });
        db.TestReturnEvents.Add(new TestReturnEvent { Id = 4, TestOrderId = 104, AssignedAnalystId = 1, ReviewerUserId = 99, ReturnedAt = afterRangeDate, Reason = "Future event" });

        // Analyst 2: 1 event in range
        db.TestReturnEvents.Add(new TestReturnEvent { Id = 5, TestOrderId = 105, AssignedAnalystId = 2, ReviewerUserId = 99, ReturnedAt = inRangeDate, Reason = "Typo fix" });

        // Analyst 3: 0 events

        await db.SaveChangesAsync();

        var service = new KpiService(db);

        // 1. Query all analysts (analystId = null) -> returns dictionary mapping analystId -> count
        var allCounts = await service.GetReturnToAnalystCountAsync(null, fromDate, toDate);
        Assert.Equal(2, allCounts[1]);
        Assert.Equal(1, allCounts[2]);
        Assert.False(allCounts.ContainsKey(3));

        // 2. Query scoped to Analyst 1 -> returns single entry dictionary for Analyst 1 with count 2
        var analyst1Counts = await service.GetReturnToAnalystCountAsync(1, fromDate, toDate);
        Assert.Single(analyst1Counts);
        Assert.Equal(2, analyst1Counts[1]);

        // 3. Query scoped to Analyst 2 -> returns count 1
        var analyst2Counts = await service.GetReturnToAnalystCountAsync(2, fromDate, toDate);
        Assert.Single(analyst2Counts);
        Assert.Equal(1, analyst2Counts[2]);

        // 4. Query scoped to Analyst 3 (0 events) -> returns entry with count 0
        var analyst3Counts = await service.GetReturnToAnalystCountAsync(3, fromDate, toDate);
        Assert.Single(analyst3Counts);
        Assert.Equal(0, analyst3Counts[3]);
    }
}
