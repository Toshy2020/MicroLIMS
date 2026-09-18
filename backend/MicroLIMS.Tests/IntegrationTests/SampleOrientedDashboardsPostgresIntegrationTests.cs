using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

// The sample-oriented dashboards, workspace drill-downs and notifications are
// unit-tested on the EF InMemory provider, which never translates a query to
// SQL. These run the same entry points against real PostgreSQL, so a LINQ
// shape Npgsql cannot translate (or translates differently) fails CI instead
// of the live deploy.
[Collection("PostgresDatabaseCollection")]
public class SampleOrientedDashboardsPostgresIntegrationTests
{
    private const string SeededUserFullName = "QA Document Admin";

    private readonly PostgresTestFixture _fixture;
    private int _microSectionId;

    public SampleOrientedDashboardsPostgresIntegrationTests(PostgresTestFixture fixture)
    {
        _fixture = fixture;
    }

    [PostgresFact]
    public async Task ReviewerDashboard_OnPostgres_GroupsBySample_WithReviewClock_WorstActiveResult_AndRetests()
    {
        await using var db = _fixture.CreateDbContext();
        var (causeId, itemId) = await ResetAsync(db);
        var now = DateTime.UtcNow;

        // A: in review for 26h (SubmittedForReview event). TAMC's OOS reading
        // was returned (inactive) and re-read within limits; TYMC's active
        // reading is at action level.
        var sampleA = NewSample("PG-REV-A", SampleStatus.UnderReview, now.AddDays(-3), causeId, itemId);
        var tamc = AddOrder(sampleA, "TAMC", ApprovalStatus.ResultEntered, WorkflowStep.Ready);
        var tymc = AddOrder(sampleA, "TYMC", ApprovalStatus.ResultEntered, WorkflowStep.Ready);

        // B: in review with no event - the clock falls back to the latest Ready history (3h).
        var sampleB = NewSample("PG-REV-B", SampleStatus.UnderReview, now.AddDays(-2), causeId, itemId);
        var sampleBOrder = AddOrder(sampleB, "TAMC", ApprovalStatus.ResultEntered, WorkflowStep.Ready);

        var origin = NewSample("PG-REV-ORIG", SampleStatus.RetestRequested, now.AddDays(-5), causeId, itemId);
        AddOrder(origin, "TAMC", ApprovalStatus.Reviewed, WorkflowStep.Reviewed);

        db.Samples.AddRange(sampleA, sampleB, origin);
        await db.SaveChangesAsync();

        var spinOff = NewSample("PG-REV-RET", SampleStatus.InTesting, now.AddDays(-1), causeId, itemId);
        spinOff.OriginSampleId = origin.Id;
        AddOrder(spinOff, "TAMC", ApprovalStatus.InProgress, WorkflowStep.Incubating);
        db.Samples.Add(spinOff);

        var returnedReading = NewReading(tamc.Id, isActive: false);
        var currentTamcReading = NewReading(tamc.Id, isActive: true);
        var tymcReading = NewReading(tymc.Id, isActive: true);
        db.CountTestReadings.AddRange(returnedReading, currentTamcReading, tymcReading);

        db.ReviewWorkflowEvents.Add(NewSubmittedForReviewEvent(sampleA.Id, now.AddHours(-26)));
        db.WorkflowHistories.Add(new WorkflowHistory
        {
            TestOrderId = sampleBOrder.Id,
            FromStep = WorkflowStep.Incubating,
            ToStep = WorkflowStep.Ready,
            PerformedByUserId = _fixture.SeededUserId,
            Timestamp = now.AddHours(-3)
        });
        await db.SaveChangesAsync();

        db.ResultRecords.AddRange(
            NewRecord(sampleA, tamc, returnedReading.Id, ResultLevel.OutOfSpecification, now),
            NewRecord(sampleA, tamc, currentTamcReading.Id, ResultLevel.WithinLimit, now),
            NewRecord(sampleA, tymc, tymcReading.Id, ResultLevel.ActionLevel, now));
        await db.SaveChangesAsync();

        var result = await TestServiceFactory.Dashboard(db).GetReviewerDashboardAsync(_fixture.SeededUserId);

        Assert.Equal(2, result.PendingReviewCount);
        Assert.Equal(1, result.OverdueReviewCount);
        Assert.Equal(1, result.RetestsInProgressCount);

        var rowA = Assert.Single(result.ReviewQueue, r => r.SampleId == sampleA.Id);
        Assert.Equal(2, rowA.Tests.Count);
        Assert.Equal(nameof(ResultLevel.ActionLevel), rowA.WorstResultLevel);
        Assert.Equal(nameof(ResultLevel.WithinLimit), Assert.Single(rowA.Tests, t => t.TestCode == "TAMC").ResultLevel);
        Assert.Equal("High", rowA.Priority);
        Assert.InRange(rowA.SubmittedForReviewAt, now.AddHours(-26).AddSeconds(-1), now.AddHours(-26).AddSeconds(1));

        var rowB = Assert.Single(result.ReviewQueue, r => r.SampleId == sampleB.Id);
        Assert.InRange(rowB.SubmittedForReviewAt, now.AddHours(-3).AddSeconds(-1), now.AddHours(-3).AddSeconds(1));
        Assert.Null(rowB.WorstResultLevel);
        Assert.Equal("Normal", rowB.Priority);
    }

    [PostgresFact]
    public async Task SectionHeadDashboard_AndAdminSummary_OnPostgres_CountSamples_WithApprovalClock_AndFullRetestCount()
    {
        await using var db = _fixture.CreateDbContext();
        var (causeId, itemId) = await ResetAsync(db);
        var now = DateTime.UtcNow;

        var inReview = NewSample("PG-SH-REV", SampleStatus.UnderReview, now.AddDays(-3), causeId, itemId);
        AddOrder(inReview, "TAMC", ApprovalStatus.ResultEntered, WorkflowStep.Ready);
        AddOrder(inReview, "TYMC", ApprovalStatus.ResultEntered, WorkflowStep.Ready);

        var inApproval = NewSample("PG-SH-APP", SampleStatus.UnderApproval, now.AddDays(-3), causeId, itemId);
        inApproval.ReviewedAt = now.AddHours(-30);
        inApproval.ReviewedByUserId = _fixture.SeededUserId;
        AddOrder(inApproval, "TAMC", ApprovalStatus.Reviewed, WorkflowStep.Reviewed);

        var origin = NewSample("PG-SH-ORIG", SampleStatus.RetestRequested, now.AddDays(-5), causeId, itemId);
        AddOrder(origin, "TAMC", ApprovalStatus.Reviewed, WorkflowStep.Reviewed);

        db.Samples.AddRange(inReview, inApproval, origin);
        await db.SaveChangesAsync();

        // Six in-progress retests: more than the five attention items shown.
        for (var i = 1; i <= 6; i++)
        {
            var spinOff = NewSample($"PG-SH-RET-{i}", SampleStatus.InTesting, now.AddHours(-i), causeId, itemId);
            spinOff.OriginSampleId = origin.Id;
            AddOrder(spinOff, "TAMC", ApprovalStatus.InProgress, WorkflowStep.Incubating);
            db.Samples.Add(spinOff);
        }
        db.ReviewWorkflowEvents.Add(NewSubmittedForReviewEvent(inReview.Id, now.AddHours(-2)));
        await db.SaveChangesAsync();

        var dashboard = TestServiceFactory.Dashboard(db);
        var result = await dashboard.GetSectionHeadDashboardAsync();

        Assert.Equal(1, result.PendingReview);
        Assert.Equal(1, result.ReviewBottleneck);
        Assert.Equal(1, result.PendingApproval);
        Assert.Equal(1, result.ApprovalBottleneck);
        Assert.Equal(0, result.ReviewQueueOverdueCount);
        Assert.Equal(1, result.ApprovalQueueOverdueCount);

        var reviewRow = Assert.Single(result.ReviewQueueItems);
        Assert.Equal(new[] { "TAMC", "TYMC" }, reviewRow.TestCodes.OrderBy(c => c));

        var approvalRow = Assert.Single(result.ApprovalQueueItems);
        Assert.Equal(SeededUserFullName, approvalRow.ReviewerName);
        Assert.InRange(approvalRow.AgeHours, 29.9, 30.1);

        Assert.Contains(result.AttentionItems, a => a.ActionType == "DelayedApproval" && a.SampleId == inApproval.Id);
        Assert.Equal(5, result.AttentionItems.Count(a => a.ActionType == "RetestRequired"));
        Assert.All(result.AttentionItems.Where(a => a.ActionType == "RetestRequired"), a => Assert.Contains("PG-SH-ORIG", a.Reason));

        // No TestOrder has an assigned analyst, so the 7-day analyst-stage SLA
        // contributes nothing: 6 retests + 1 delayed approval.
        Assert.Equal(0, result.Overdue);
        Assert.Equal(7, result.AttentionCount);

        var summary = await dashboard.GetSummaryAsync(RoleType.SystemAdministrator, _fixture.SeededUserId);
        Assert.Equal(1, (int)summary.GetType().GetProperty("reviewerQueue")!.GetValue(summary)!);
        Assert.Equal(1, (int)summary.GetType().GetProperty("approvalQueue")!.GetValue(summary)!);
    }

    [PostgresFact]
    public async Task WorkspaceFiltersCountsAndNotifications_OnPostgres_AreSampleOriented()
    {
        await using var db = _fixture.CreateDbContext();
        var (causeId, itemId) = await ResetAsync(db);
        var now = DateTime.UtcNow;
        var analystId = await EnsureAnalystAsync(db);

        var inReview = NewSample("PG-WS-REV", SampleStatus.UnderReview, now.AddDays(-3), causeId, itemId);
        AddOrder(inReview, "TAMC", ApprovalStatus.ResultEntered, WorkflowStep.Ready);

        var inApproval = NewSample("PG-WS-APP", SampleStatus.UnderApproval, now.AddDays(-3), causeId, itemId);
        inApproval.ReviewedAt = now.AddHours(-25);
        inApproval.ReviewedByUserId = _fixture.SeededUserId;
        AddOrder(inApproval, "TAMC", ApprovalStatus.Reviewed, WorkflowStep.Reviewed);

        // Still in testing with one finished test - must not count as awaiting review.
        var stillTesting = NewSample("PG-WS-TEST", SampleStatus.InTesting, now.AddDays(-1), causeId, itemId);
        AddOrder(stillTesting, "TAMC", ApprovalStatus.ResultEntered, WorkflowStep.Ready);
        var incubating = AddOrder(stillTesting, "TYMC", ApprovalStatus.InProgress, WorkflowStep.Incubating);
        incubating.Incubations.Add(new Incubation
        {
            TestOrder = incubating,
            StepNumber = 1,
            StepName = "Stage 1",
            StartedAt = now.AddDays(-1),
            CompletedAt = now.AddMinutes(-5),
            StageNumber = 1
        });

        var origin = NewSample("PG-WS-ORIG", SampleStatus.RetestRequested, now.AddDays(-5), causeId, itemId);
        AddOrder(origin, "TAMC", ApprovalStatus.Reviewed, WorkflowStep.Reviewed);

        db.Samples.AddRange(inReview, inApproval, stillTesting, origin);
        await db.SaveChangesAsync();

        var spinOff = NewSample("PG-WS-RET", SampleStatus.InTesting, now.AddHours(-6), causeId, itemId);
        spinOff.OriginSampleId = origin.Id;
        AddOrder(spinOff, "TAMC", ApprovalStatus.InProgress, WorkflowStep.Running);
        db.Samples.Add(spinOff);
        db.ReviewWorkflowEvents.Add(NewSubmittedForReviewEvent(inReview.Id, now.AddHours(-25)));
        await db.SaveChangesAsync();

        var workspace = new TestingWorkspaceService(db);

        var counts = await workspace.GetWorkloadCountsAsync(analystId);
        Assert.Equal(1, counts.AwaitingReview);

        async Task<List<string>> ReferencesFor(string workload) =>
            (await workspace.GetActiveSamplesAsync(new TestingWorkspaceFilterDto { WorkloadFilter = workload }))
                .Items.Select(s => s.ReferenceNumber).OrderBy(r => r).ToList();

        Assert.Equal(new[] { "PG-WS-REV" }, await ReferencesFor("awaitingReview"));
        Assert.Equal(new[] { "PG-WS-RET" }, await ReferencesFor("retestInProgress"));
        Assert.Equal(new[] { "PG-WS-REV" }, await ReferencesFor("reviewOverdue"));
        Assert.Equal(new[] { "PG-WS-APP" }, await ReferencesFor("approvalOverdue"));

        var notifications = TestServiceFactory.DashboardNotification(db);

        var sectionHeadNotifications = await notifications.GetNotificationsAsync(RoleType.SectionHead, _fixture.SeededControllerUserId);
        Assert.Contains(sectionHeadNotifications, n => n.Type == "ReviewWaiting" && n.Message == "1 sample(s) awaiting review.");
        Assert.Contains(sectionHeadNotifications, n => n.Type == "ApprovalWaiting" && n.Message == "1 sample(s) awaiting approval.");
        Assert.Contains(sectionHeadNotifications, n => n.Type == "IncubationReady"
            && n.Message == "TYMC (Stage 1) for sample PG-WS-TEST is ready."
            && n.SampleId == stillTesting.Id
            && n.TestOrderId == incubating.Id);

        var analystNotifications = await notifications.GetNotificationsAsync(RoleType.Analyst, analystId);
        Assert.DoesNotContain(analystNotifications, n => n.Type == "ReviewWaiting" || n.Type == "ApprovalWaiting");
    }

    // The collection shares one database across test classes, so each test
    // starts from a known state. TRUNCATE ... CASCADE also clears the tables
    // that reference Samples (TestOrders, readings, incubations, ResultRecords,
    // WorkflowHistory). Review events and notification logs hold plain id
    // columns rather than foreign keys, so the rows these tests read are
    // cleared explicitly.
    private async Task<(int CauseId, int ItemId)> ResetAsync(MicroLimsDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync("TRUNCATE TABLE \"Samples\" CASCADE;");
        await db.ReviewWorkflowEvents.Where(e => e.EntityType == ReviewEntityTypes.Sample).ExecuteDeleteAsync();
        await db.NotificationLogs
            .Where(n => n.Type == "ReviewWaiting" || n.Type == "ApprovalWaiting" || n.Type == "IncubationReady")
            .ExecuteDeleteAsync();

        var cause = await db.CausesOfTesting.FirstOrDefaultAsync();
        if (cause is null)
        {
            cause = new CauseOfTesting { Name = "Routine Release" };
            db.CausesOfTesting.Add(cause);
        }

        var item = await db.Items.FirstOrDefaultAsync();
        if (item is null)
        {
            item = new Item { Code = "PG-DASH-ITEM", Name = "Dashboard Integration Item" };
            db.Items.Add(item);
        }

        await db.SaveChangesAsync();
        _microSectionId = (await db.DocumentSections.FirstAsync(s => s.Code == "MICRO")).Id;
        return (cause.Id, item.Id);
    }

    private static async Task<int> EnsureAnalystAsync(MicroLimsDbContext db)
    {
        var existing = await db.Users.FirstOrDefaultAsync(u => u.Username == "pg_dash_analyst");
        if (existing is not null) return existing.Id;

        var analystRole = await db.Roles.FirstAsync(r => r.Type == RoleType.Analyst);
        var analyst = new User
        {
            FullName = "Dashboard Analyst",
            Username = "pg_dash_analyst",
            PasswordHash = "hashed_dummy",
            RoleId = analystRole.Id,
            IsActive = true
        };
        db.Users.Add(analyst);
        await db.SaveChangesAsync();
        return analyst.Id;
    }

    private Sample NewSample(string reference, SampleStatus status, DateTime receivedAt, int causeId, int itemId) => new()
    {
        ReferenceNumber = reference,
        ControlNumber = $"CTRL-{reference}",
        SampledBy = "Postgres Integration",
        Category = SampleCategory.FinishedProduct,
        Status = status,
        PreparationStatus = SamplePreparationStatus.Ready,
        ReceivedByUserId = _fixture.SeededUserId,
        CauseOfTestingId = causeId,
        ItemId = itemId,
        ReceivedAt = receivedAt
    };

    private TestOrder AddOrder(Sample sample, string testCode, ApprovalStatus status, WorkflowStep step)
    {
        var order = new TestOrder { Sample = sample, TestCode = testCode, Status = status, CurrentStep = step, SectionId = _microSectionId };
        sample.TestOrders.Add(order);
        return order;
    }

    private CountTestReading NewReading(int testOrderId, bool isActive) => new()
    {
        TestOrderId = testOrderId,
        StepName = "Count",
        PlateReadings = "10,12",
        DilutionFactor = 1m,
        ReportedResult = "11",
        Status = "WithinLimits",
        IsActive = isActive,
        EnteredByUserId = _fixture.SeededUserId,
        EnteredAt = DateTime.UtcNow
    };

    private ResultRecord NewRecord(Sample sample, TestOrder order, int readingId, ResultLevel level, DateTime now) => new()
    {
        SampleId = sample.Id,
        TestOrderId = order.Id,
        SourceTable = "CountTestReading",
        SourceId = readingId,
        Round = 1,
        ReferenceNumber = sample.ReferenceNumber,
        Category = sample.Category,
        SubjectName = "Dashboard Integration Item",
        TestCode = order.TestCode,
        TestDisplayName = order.TestCode,
        ReportedValue = "11",
        ResultLevel = level,
        ResultEnteredAt = now,
        ResultEnteredByUserId = _fixture.SeededUserId,
        ResultEnteredByName = SeededUserFullName,
        SampleStatus = sample.Status,
        UpdatedAt = now
    };

    private ReviewWorkflowEvent NewSubmittedForReviewEvent(int sampleId, DateTime at) => new()
    {
        EntityType = ReviewEntityTypes.Sample,
        EntityId = sampleId,
        EventType = ReviewWorkflowEventType.SubmittedForReview,
        PerformedByUserId = _fixture.SeededUserId,
        PerformedByNameSnapshot = SeededUserFullName,
        Timestamp = at
    };
}
