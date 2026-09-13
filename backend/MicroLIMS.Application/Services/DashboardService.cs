using Microsoft.EntityFrameworkCore;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Application.Services;

public record TodaysWorkTestDto(int TestOrderId, string TestCode, string Status, string? TimeRemaining, bool IsReturned = false, string? ReturnReason = null);
public record TodaysWorkItemDto(int SampleId, string ReferenceNumber, string Category, string DisplayName, DateTime ReceivedAt, string OverallStatus, string NextAction, List<TodaysWorkTestDto> Tests);
public record IncubationOverviewDto(string TestCode, int ReadyToRead, int Incubating);
public record AnalystMetricsDto(int TestsCompletedToday, int MediaLotsPreparedToday, int ActiveAssignedOrders, double OnTimeReadingRate, int Trailing7DayVolume);

public record SectionHeadAttentionItemDto(
    int SampleId,
    string ReferenceNumber,
    string SubjectName,
    List<string> TestCodes,
    string Urgency,
    string Reason,
    string ActionType,
    DateTime Timestamp
);

public record SectionHeadReviewQueueItemDto(
    int SampleId,
    string ReferenceNumber,
    string SubjectName,
    string Category,
    List<string> TestCodes,
    List<string> AnalystNames,
    DateTime SubmittedForReviewAt,
    double AgeHours,
    string? WorstResultLevel
);

public record SectionHeadApprovalQueueItemDto(
    int SampleId,
    string ReferenceNumber,
    string SubjectName,
    string Category,
    List<string> TestCodes,
    string? ReviewerName,
    DateTime ReviewedAt,
    double AgeHours,
    string? WorstResultLevel
);

public record SectionHeadAnalystWorkloadDto(
    int AnalystId,
    string AnalystName,
    string Username,
    int ActiveCount,
    int OverdueCount,
    int CompletedTodayCount
);

public record SectionHeadDashboardDto(
    int ActiveTests,
    int Incubating,
    int ReadyToRead,
    int PendingReview,
    int PendingApproval,
    int Overdue,
    int AttentionCount,
    int TestingBottleneck,
    int IncubationBottleneck,
    int ReadyToReadBottleneck,
    int ReviewBottleneck,
    int ApprovalBottleneck,
    List<SectionHeadAttentionItemDto> AttentionItems,
    int ReviewQueueCount,
    int ReviewQueueOverdueCount,
    double ReviewQueueOldestHours,
    List<SectionHeadReviewQueueItemDto> ReviewQueueItems,
    int ApprovalQueueCount,
    int ApprovalQueueOverdueCount,
    double ApprovalQueueOldestHours,
    List<SectionHeadApprovalQueueItemDto> ApprovalQueueItems,
    List<IncubationOverviewDto> IncubationSummary,
    List<SectionHeadAnalystWorkloadDto> AnalystWorkloads
);

public record ReviewerQueueTestDto(
    int TestOrderId,
    string TestCode,
    string TestDisplayName,
    string? AnalystName,
    string? ResultLevel
);

public record ReviewerQueueItemDto(
    int SampleId,
    string ReferenceNumber,
    string SubjectName,
    string Category,
    DateTime SubmittedForReviewAt,
    int AgeMinutes,
    string Priority,
    string? WorstResultLevel,
    List<string> AnalystNames,
    List<ReviewerQueueTestDto> Tests
);

public record ReviewerRecentlyReviewedDto(
    int SampleId,
    int TestOrderId,
    string ReferenceNumber,
    string SubjectName,
    string Category,
    string TestCode,
    DateTime ReviewedAt,
    string Status,
    string? Comment
);

public record ReviewerAttentionItemDto(
    int SampleId,
    string ReferenceNumber,
    string SubjectName,
    List<string> TestCodes,
    string Urgency,
    string Reason,
    DateTime Timestamp
);

public record ReviewerDashboardDto(
    int PendingReviewCount,
    int OverdueReviewCount,
    int DueTodayCount,
    int RetestsInProgressCount,
    int CompletedTodayCount,
    List<ReviewerQueueItemDto> ReviewQueue,
    List<ReviewerAttentionItemDto> AttentionItems,
    List<ReviewerRecentlyReviewedDto> RecentlyReviewed
);

// Five widgets from the gap analysis, shown to every role (an Analyst's
// "Pending Tests" is their own queue; a Reviewer's is everyone's - see
// the per-role filtering below). Delayed = still not ready 24h+ after
// the sample was received.
public class DashboardService
{
    private static readonly TimeSpan DelayThreshold = TimeSpan.FromHours(24);

    // A sample with no lower bound on when it could have been assigned -
    // used only to call KpiService.GetSampleAssignmentSlaAsync with an
    // effectively unbounded "since the beginning" range, since this
    // dashboard has no date-range concept of its own (a live snapshot,
    // not a filtered report).
    private static readonly DateTime Epoch = new(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly MicroLimsDbContext _db;
    private readonly KpiService _kpiService;

    public DashboardService(MicroLimsDbContext db, KpiService kpiService)
    {
        _db = db;
        _kpiService = kpiService;
    }

    public async Task<object> GetSummaryAsync(RoleType role, int userId)
    {
        var now = DateTime.UtcNow;
        var cutoff = now.Subtract(DelayThreshold);
        var todayStart = now.Date;

        var pendingQuery = _db.TestOrders.Where(t => t.Status == ApprovalStatus.Pending || t.Status == ApprovalStatus.InProgress);
        if (role == RoleType.Analyst)
            pendingQuery = pendingQuery.Where(t => t.AssignedAnalystId == userId);

        var pendingTests = await pendingQuery.CountAsync();

        var delayedTests = await _db.TestOrders
            .Where(t => (t.Status == ApprovalStatus.Pending || t.Status == ApprovalStatus.InProgress))
            .Where(t => _db.Samples.Any(s => s.Id == t.SampleId && s.ReceivedAt < cutoff))
            .CountAsync();

        var samplesToday = await _db.Samples.CountAsync(s => s.ReceivedAt >= todayStart);
        var reviewerQueue = await _db.Samples.CountAsync(s => s.Status == SampleStatus.UnderReview);
        var approvalQueue = await _db.Samples.CountAsync(s => s.Status == SampleStatus.UnderApproval);
        var preparationQueue = await _db.Samples.CountAsync(s => s.PreparationStatus == SamplePreparationStatus.NeedsPreparation);

        // Preparation configs auto-created from an analyst's first manual
        // entry are usable immediately but still owe a Section Head review.
        var pendingPreparationConfigApproval = await _db.ItemPreparationConfigurations
            .CountAsync(c => c.ApprovalStatus == ApprovalGateStatus.PendingReview);

        // Open incubations split ready-vs-still-incubating - feeds the KPI
        // strip's "Incubating" / "Ready to Read" tiles (same open-incubation
        // definition GetIncubationOverviewAsync groups by test code).
        var openIncubationReadings = await _db.Incubations
            .Where(i => i.CompletedAt == null && i.TestOrderId != null)
            .Select(i => i.ExpectedReadingAt)
            .ToListAsync();
        var readyToReadCount = openIncubationReadings.Count(r => r != null && r <= now);
        var incubatingCount = openIncubationReadings.Count(r => r == null || r > now);

        return new
        {
            pendingTests,
            delayedTests,
            samplesToday,
            reviewerQueue,
            approvalQueue,
            preparationQueue,
            pendingPreparationConfigApproval,
            incubatingCount,
            readyToReadCount
        };
    }

    // "Today's Laboratory Work" table - today's samples with a computed
    // NextAction per TestOrder status and TimeRemaining from the nearest
    // open Incubation. Analyst role scopes to their own assigned tests,
    // same filtering rule as GetSummaryAsync's pendingTests above.
    public async Task<List<TodaysWorkItemDto>> GetTodaysWorkAsync(RoleType role, int userId)
    {
        var todayStart = DateTime.UtcNow.Date;
        var now = DateTime.UtcNow;

        var query = _db.Samples
            .Include(s => s.Item)
            .Include(s => s.WaterSamplingPoint)
            .Include(s => s.Department)
            .Include(s => s.Machine)
            .Include(s => s.TestOrders).ThenInclude(t => t.Incubations)
            .Where(s => s.ReceivedAt >= todayStart)
            .AsQueryable();

        if (role == RoleType.Analyst)
            query = query.Where(s => s.TestOrders.Any(t => t.AssignedAnalystId == userId));

        var samples = await query.OrderByDescending(s => s.ReceivedAt).ToListAsync();

        var allTestOrderIds = samples.SelectMany(s => s.TestOrders).Select(t => t.Id).ToList();
        var pendingReturns = await TestReturnHelper.GetPendingReturnsForOrdersAsync(_db, allTestOrderIds);

        return samples.Select(s =>
        {
            var tests = s.TestOrders.Select(t =>
            {
                var openIncubation = t.Incubations
                    .Where(i => i.CompletedAt == null && i.ExpectedReadingAt != null)
                    .OrderBy(i => i.ExpectedReadingAt)
                    .FirstOrDefault();
                var timeRemaining = openIncubation?.ExpectedReadingAt is { } readyAt ? FormatTimeRemaining(readyAt, now) : null;
                var isReturned = pendingReturns.TryGetValue(t.Id, out var returnInfo);
                return new TodaysWorkTestDto(t.Id, t.TestCode, t.Status.ToString(), timeRemaining, isReturned, returnInfo?.Reason);
            }).ToList();

            var worst = s.TestOrders.OrderBy(t => StatusRank(t.Status)).FirstOrDefault();
            var overallStatus = worst?.Status.ToString() ?? s.Status.ToString();
            var nextAction = worst is null ? "View Results" : NextActionFor(worst.Status);
            var displayName = s.Item?.Name ?? s.WaterSamplingPoint?.Code ?? s.Department?.Name ?? s.Machine?.Name ?? string.Empty;

            return new TodaysWorkItemDto(s.Id, s.ReferenceNumber, s.Category.ToString(), displayName, s.ReceivedAt, overallStatus, nextAction, tests);
        }).ToList();
    }

    // Open incubations grouped by TestCode, split into ready-to-read vs
    // still-incubating - powers the Incubation Overview widget.
    public async Task<List<IncubationOverviewDto>> GetIncubationOverviewAsync(bool myIncubationsOnly = false, int? userId = null)
    {
        var now = DateTime.UtcNow;
        var query = _db.Incubations
            .Where(i => i.CompletedAt == null && i.TestOrderId != null)
            .Include(i => i.TestOrder)
            .AsQueryable();

        if (myIncubationsOnly && userId.HasValue)
        {
            query = query.Where(i => i.TestOrder!.AssignedAnalystId == userId.Value || i.StartedByUserId == userId.Value);
        }

        var openIncubations = await query.ToListAsync();

        return openIncubations
            .Where(i => i.TestOrder != null)
            .GroupBy(i => i.TestOrder!.TestCode)
            .Select(g => new IncubationOverviewDto(
                g.Key,
                g.Count(i => i.ExpectedReadingAt != null && i.ExpectedReadingAt <= now),
                g.Count(i => i.ExpectedReadingAt == null || i.ExpectedReadingAt > now)))
            .OrderByDescending(x => x.ReadyToRead + x.Incubating)
            .ToList();
    }

    public async Task<AnalystMetricsDto> GetAnalystMetricsAsync(int userId)
    {
        var todayStart = DateTime.UtcNow.Date;
        var sevenDaysAgo = DateTime.UtcNow.Date.AddDays(-7);

        var completedResultsToday = await _db.Results
            .Where(r => r.EnteredByUserId == userId && r.EnteredAt >= todayStart)
            .Select(r => r.TestOrderId)
            .ToListAsync();

        var completedReadingsToday = await _db.CountTestReadings
            .Where(r => r.EnteredByUserId == userId && r.EnteredAt >= todayStart && r.IsActive)
            .Select(r => r.TestOrderId)
            .ToListAsync();

        var testsCompletedToday = completedResultsToday.Concat(completedReadingsToday).Distinct().Count();

        var mediaLotsPreparedToday = await _db.Media
            .CountAsync(m => m.PreparedByUserId == userId && m.PreparedAt >= todayStart);

        var activeAssignedOrders = await _db.TestOrders
            .CountAsync(t => t.AssignedAnalystId == userId && (t.Status == ApprovalStatus.Pending || t.Status == ApprovalStatus.InProgress || t.Status == ApprovalStatus.RetestRequested));

        var completedResults7d = await _db.Results
            .Where(r => r.EnteredByUserId == userId && r.EnteredAt >= sevenDaysAgo)
            .Select(r => r.TestOrderId)
            .ToListAsync();

        var completedReadings7d = await _db.CountTestReadings
            .Where(r => r.EnteredByUserId == userId && r.EnteredAt >= sevenDaysAgo && r.IsActive)
            .Select(r => r.TestOrderId)
            .ToListAsync();

        var trailing7DayVolume = completedResults7d.Concat(completedReadings7d).Distinct().Count();

        var completedIncubations = await _db.Incubations
            .Where(i => (i.TestOrder!.AssignedAnalystId == userId || i.StartedByUserId == userId)
                        && i.CompletedAt != null && i.ExpectedReadingAt != null)
            .Select(i => new { i.CompletedAt, i.ExpectedReadingAt })
            .ToListAsync();

        double onTimeRate = 100.0;
        if (completedIncubations.Count > 0)
        {
            // Allow up to 4 hours tolerance past ExpectedReadingAt
            var onTimeCount = completedIncubations.Count(i => i.CompletedAt <= i.ExpectedReadingAt!.Value.AddHours(4));
            onTimeRate = Math.Round(onTimeCount * 100.0 / completedIncubations.Count, 1);
        }

        return new AnalystMetricsDto(
            testsCompletedToday,
            mediaLotsPreparedToday,
            activeAssignedOrders,
            onTimeRate,
            trailing7DayVolume);
    }

    private static int StatusRank(ApprovalStatus status) => status switch
    {
        ApprovalStatus.Pending => 0,
        ApprovalStatus.InProgress => 1,
        ApprovalStatus.RetestRequested => 2,
        ApprovalStatus.ResultEntered => 3,
        ApprovalStatus.Reviewed => 4,
        ApprovalStatus.Rejected => 5,
        ApprovalStatus.Approved => 6,
        _ => 7
    };

    private static string NextActionFor(ApprovalStatus status) => status switch
    {
        ApprovalStatus.Pending or ApprovalStatus.InProgress => "Continue Testing",
        ApprovalStatus.RetestRequested => "Retest Required",
        ApprovalStatus.ResultEntered => "Send to Review",
        ApprovalStatus.Reviewed => "Awaiting Approval",
        _ => "View Results"
    };

    // Once ExpectedReadingAt has passed the plate is simply ready to read
    // - that's an expected, routine state, not the same "Overdue" concept
    // delayedTests uses (24h+ since the sample was received at all).
    private static string FormatTimeRemaining(DateTime readyAt, DateTime now)
    {
        var delta = readyAt - now;
        if (delta <= TimeSpan.Zero) return "Ready to read";
        return $"{(int)Math.Ceiling(delta.TotalHours)}h left";
    }

    // Samples lodged vs test requests (TestOrders) lodged, per month,
    // for the last N months - powers the trend bar chart.
    public async Task<List<object>> GetMonthlyTrendAsync(int months = 6)
    {
        var now = DateTime.UtcNow;
        var start = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-(months - 1));

        var samples = await _db.Samples.Where(s => s.ReceivedAt >= start).Select(s => s.ReceivedAt).ToListAsync();
        var testOrders = await _db.TestOrders
            .Where(t => _db.Samples.Any(s => s.Id == t.SampleId && s.ReceivedAt >= start))
            .Join(_db.Samples, t => t.SampleId, s => s.Id, (t, s) => s.ReceivedAt)
            .ToListAsync();

        var result = new List<object>();
        for (var i = 0; i < months; i++)
        {
            var monthStart = start.AddMonths(i);
            var monthEnd = monthStart.AddMonths(1);
            result.Add(new
            {
                month = monthStart.ToString("MMM"),
                samplesLodged = samples.Count(d => d >= monthStart && d < monthEnd),
                testsLodged = testOrders.Count(d => d >= monthStart && d < monthEnd)
            });
        }
        return result;
    }

    // Sample category breakdown (Product/RM/PM/Water/EM/AfterCleaning/GPT) - donut chart.
    public async Task<List<object>> GetCategoryDistributionAsync()
    {
        var total = await _db.Samples.CountAsync();
        if (total == 0) return new List<object>();

        var grouped = await _db.Samples.GroupBy(s => s.Category)
            .Select(g => new { category = g.Key.ToString(), count = g.Count() })
            .ToListAsync();

        return grouped.Select(g => (object)new { g.category, g.count, percent = Math.Round(g.count * 100.0 / total, 1) }).ToList();
    }

    // TestOrder status breakdown (Approved / Pending-or-InProgress / Rejected) - donut chart,
    // mirrors "inspected vs non-inspected" from the reference design.
    public async Task<List<object>> GetStatusDistributionAsync()
    {
        // Voided tests were struck from the record - not pending, not decided.
        var total = await _db.TestOrders.CountAsync(t => t.Status != ApprovalStatus.Voided);
        if (total == 0) return new List<object>();

        var approved = await _db.TestOrders.CountAsync(t => t.Status == ApprovalStatus.Approved);
        var rejected = await _db.TestOrders.CountAsync(t => t.Status == ApprovalStatus.Rejected);
        var pending = total - approved - rejected;

        return new List<object>
        {
            new { status = "Approved", count = approved, percent = Math.Round(approved * 100.0 / total, 1) },
            new { status = "Pending", count = pending, percent = Math.Round(pending * 100.0 / total, 1) },
            new { status = "Rejected", count = rejected, percent = Math.Round(rejected * 100.0 / total, 1) }
        };
    }

    // Month-over-month deltas for the KPI cards.
    public async Task<object> GetKpiDeltasAsync()
    {
        var now = DateTime.UtcNow;
        var thisMonthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var lastMonthStart = thisMonthStart.AddMonths(-1);

        var samplesThisMonth = await _db.Samples.CountAsync(s => s.ReceivedAt >= thisMonthStart);
        var samplesLastMonth = await _db.Samples.CountAsync(s => s.ReceivedAt >= lastMonthStart && s.ReceivedAt < thisMonthStart);

        var testsThisMonth = await _db.TestOrders.CountAsync(t => _db.Samples.Any(s => s.Id == t.SampleId && s.ReceivedAt >= thisMonthStart));
        var testsLastMonth = await _db.TestOrders.CountAsync(t => _db.Samples.Any(s => s.Id == t.SampleId && s.ReceivedAt >= lastMonthStart && s.ReceivedAt < thisMonthStart));

        return new
        {
            samplesThisMonth,
            samplesDeltaPercent = samplesLastMonth == 0 ? 0 : Math.Round((samplesThisMonth - samplesLastMonth) * 100.0 / samplesLastMonth, 1),
            testsThisMonth,
            testsDeltaPercent = testsLastMonth == 0 ? 0 : Math.Round((testsThisMonth - testsLastMonth) * 100.0 / testsLastMonth, 1),
            totalSamples = await _db.Samples.CountAsync(),
            totalTests = await _db.TestOrders.CountAsync()
        };
    }

    public async Task<SectionHeadDashboardDto> GetSectionHeadDashboardAsync()
    {
        var now = DateTime.UtcNow;
        var cutoff = now.Subtract(DelayThreshold);
        var todayStart = now.Date;

        var activeTests = await _db.TestOrders.CountAsync(t => !t.IsSuperseded && (t.Status == ApprovalStatus.Pending || t.Status == ApprovalStatus.InProgress));

        var openIncubations = await _db.Incubations
            .Where(i => i.CompletedAt == null && i.TestOrderId != null)
            .Include(i => i.TestOrder)
            .ToListAsync();
        var incubating = openIncubations.Count(i => i.ExpectedReadingAt == null || i.ExpectedReadingAt > now);
        var readyToRead = openIncubations.Count(i => i.ExpectedReadingAt != null && i.ExpectedReadingAt <= now);

        var pendingReview = await _db.Samples.CountAsync(s => s.Status == SampleStatus.UnderReview);
        var pendingApproval = await _db.Samples.CountAsync(s => s.Status == SampleStatus.UnderApproval);

        // Rule #1's 7-day Analyst-stage SLA (KpiService.
        // GetOverdueAnalystStageSamplesAsync - the SLA determination
        // itself is never re-derived here), restricted to samples still
        // actually stuck in that stage right now (TestOrder still
        // Pending/InProgress). This dashboard is a live "what needs
        // attention now" view, not the historical "did it ever breach"
        // question Reports' Analyst Comparison asks about the same
        // samples - a sample that breached the SLA but has since moved
        // on to Review/Approval/Approved no longer belongs in a
        // currently-actionable count, even though it's still correctly
        // counted as a past SLA breach over on the Reports page.
        var overdueAnalystSamples = await _kpiService.GetOverdueAnalystStageSamplesAsync(Epoch, now);
        var overdueAssignedAtBySampleId = overdueAnalystSamples.ToDictionary(x => x.SampleId, x => x.AssignedAt);

        var liveOverdueTestOrders = await _db.TestOrders
            .Where(t => !t.IsSuperseded && (t.Status == ApprovalStatus.Pending || t.Status == ApprovalStatus.InProgress)
                && overdueAssignedAtBySampleId.Keys.Contains(t.SampleId))
            .Include(t => t.Sample).ThenInclude(s => s!.Item)
            .Include(t => t.Sample).ThenInclude(s => s!.WaterSamplingPoint)
            .Include(t => t.Sample).ThenInclude(s => s!.Department)
            .Include(t => t.Sample).ThenInclude(s => s!.Machine)
            .ToListAsync();

        var overdue = liveOverdueTestOrders.Select(t => t.SampleId).Distinct().Count();
        var liveOverdueCountByAnalyst = liveOverdueTestOrders
            .Where(t => t.AssignedAnalystId.HasValue)
            .GroupBy(t => t.AssignedAnalystId!.Value)
            .ToDictionary(g => g.Key, g => g.Select(x => x.SampleId).Distinct().Count());

        // User lookup map for names
        var users = await _db.Users.AsNoTracking().Include(u => u.Role).ToListAsync();
        var userMap = users.ToDictionary(u => u.Id, u => u.FullName);

        // Review Queue details: one row per UnderReview sample
        var underReviewSamples = await _db.Samples
            .AsNoTracking()
            .Where(s => s.Status == SampleStatus.UnderReview)
            .Include(s => s.Item)
            .Include(s => s.WaterSamplingPoint)
            .Include(s => s.Department)
            .Include(s => s.Machine)
            .Include(s => s.TestOrders).ThenInclude(t => t.Results)
            .ToListAsync();

        var reviewSampleIds = underReviewSamples.Select(s => s.Id).ToList();
        var reviewClockStarts = await SampleWorkflowQueues.GetReviewClockStartsAsync(_db, reviewSampleIds);
        var reviewWorstLevels = await SampleWorkflowQueues.GetWorstResultLevelsAsync(_db, reviewSampleIds);

        var reviewNonSupersededOrders = underReviewSamples
            .SelectMany(s => s.TestOrders.Where(t => !t.IsSuperseded))
            .ToList();
        var reviewTestOrderIds = reviewNonSupersededOrders.Select(t => t.Id).ToList();

        var reviewResultRecords = await _db.ResultRecords
            .AsNoTracking()
            .Where(r => reviewTestOrderIds.Contains(r.TestOrderId))
            .Select(r => new { r.TestOrderId, r.ResultEnteredByUserId, r.ResultEnteredByName, r.ResultEnteredAt })
            .ToListAsync();

        var reviewQueueItems = underReviewSamples
            .Select(s =>
            {
                var submittedAt = reviewClockStarts.GetValueOrDefault(s.Id, s.ReceivedAt);
                var ageHours = Math.Round((now - submittedAt).TotalHours, 1);
                var worstLevel = reviewWorstLevels.GetValueOrDefault(s.Id)?.ToString();

                var nonSuperseded = s.TestOrders.Where(t => !t.IsSuperseded).ToList();
                var testCodes = nonSuperseded.Select(t => t.TestCode).ToList();

                var analystNames = nonSuperseded.Select(t =>
                {
                    if (t.AssignedAnalystId.HasValue && userMap.TryGetValue(t.AssignedAnalystId.Value, out var assignedName))
                    {
                        return assignedName;
                    }
                    var latestResult = t.Results.OrderByDescending(r => r.EnteredAt).FirstOrDefault();
                    var latestRecord = reviewResultRecords.Where(r => r.TestOrderId == t.Id).OrderByDescending(r => r.ResultEnteredAt).FirstOrDefault();

                    if (latestResult != null && (latestRecord == null || latestResult.EnteredAt >= latestRecord.ResultEnteredAt))
                    {
                        return userMap.TryGetValue(latestResult.EnteredByUserId, out var n) ? n : null;
                    }
                    if (latestRecord != null)
                    {
                        return !string.IsNullOrWhiteSpace(latestRecord.ResultEnteredByName)
                            ? latestRecord.ResultEnteredByName
                            : (userMap.TryGetValue(latestRecord.ResultEnteredByUserId, out var n) ? n : null);
                    }
                    return null;
                })
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Select(n => n!)
                .Distinct()
                .ToList();

                var displayName = s.Item?.Name
                    ?? s.WaterSamplingPoint?.Code
                    ?? s.Department?.Name
                    ?? s.Machine?.Name
                    ?? (string.IsNullOrWhiteSpace(s.ReferenceNumber) ? "Sample" : s.ReferenceNumber);

                return new SectionHeadReviewQueueItemDto(
                    s.Id,
                    s.ReferenceNumber ?? "",
                    displayName,
                    s.Category.ToString(),
                    testCodes,
                    analystNames,
                    submittedAt,
                    ageHours,
                    worstLevel
                );
            })
            .OrderByDescending(r => r.AgeHours)
            .ToList();

        var reviewQueueOverdue = reviewQueueItems.Count(r => r.AgeHours >= 24);
        var reviewQueueOldestHours = reviewQueueItems.Count > 0 ? reviewQueueItems.Max(r => r.AgeHours) : 0;

        // Approval Queue details: one row per UnderApproval sample
        var underApprovalSamples = await _db.Samples
            .AsNoTracking()
            .Where(s => s.Status == SampleStatus.UnderApproval)
            .Include(s => s.Item)
            .Include(s => s.WaterSamplingPoint)
            .Include(s => s.Department)
            .Include(s => s.Machine)
            .Include(s => s.TestOrders)
            .ToListAsync();

        var approvalSampleIds = underApprovalSamples.Select(s => s.Id).ToList();
        var approvalWorstLevels = await SampleWorkflowQueues.GetWorstResultLevelsAsync(_db, approvalSampleIds);

        var approvalQueueItems = underApprovalSamples
            .Select(s =>
            {
                var reviewedAt = SampleWorkflowQueues.GetApprovalClockStart(s);
                var ageHours = Math.Round((now - reviewedAt).TotalHours, 1);
                var reviewerName = s.ReviewedByUserId.HasValue && userMap.TryGetValue(s.ReviewedByUserId.Value, out var rName) ? rName : null;
                var displayName = s.Item?.Name
                    ?? s.WaterSamplingPoint?.Code
                    ?? s.Department?.Name
                    ?? s.Machine?.Name
                    ?? (string.IsNullOrWhiteSpace(s.ReferenceNumber) ? "Sample" : s.ReferenceNumber);

                var nonSuperseded = s.TestOrders.Where(t => !t.IsSuperseded).ToList();
                var testCodes = nonSuperseded.Select(t => t.TestCode).ToList();
                var worstLevel = approvalWorstLevels.GetValueOrDefault(s.Id)?.ToString();

                return new SectionHeadApprovalQueueItemDto(
                    s.Id,
                    s.ReferenceNumber ?? "",
                    displayName,
                    s.Category.ToString(),
                    testCodes,
                    reviewerName,
                    reviewedAt,
                    ageHours,
                    worstLevel
                );
            })
            .OrderByDescending(a => a.AgeHours)
            .ToList();

        var approvalQueueOverdue = approvalQueueItems.Count(a => a.AgeHours >= 24);
        var approvalQueueOldestHours = approvalQueueItems.Count > 0 ? approvalQueueItems.Max(a => a.AgeHours) : 0;

        // Incubation summary grouped
        var incubationSummary = await GetIncubationOverviewAsync(false, null);

        // Analyst workloads. Overdue reuses the same live-filtered
        // per-sample set as the top-level Overdue tile above - one shared
        // computation, not a second one re-derived per analyst.
        var analysts = users.Where(u => u.IsActive && u.Role != null && u.Role.Type == RoleType.Analyst).ToList();
        var analystWorkloads = new List<SectionHeadAnalystWorkloadDto>();
        foreach (var a in analysts)
        {
            var aActive = await _db.TestOrders.CountAsync(t => !t.IsSuperseded && t.AssignedAnalystId == a.Id && (t.Status == ApprovalStatus.Pending || t.Status == ApprovalStatus.InProgress));
            var aOverdue = liveOverdueCountByAnalyst.TryGetValue(a.Id, out var cnt) ? cnt : 0;
            var aCompletedToday = await _db.Results.CountAsync(r => r.EnteredByUserId == a.Id && r.EnteredAt >= todayStart);
            analystWorkloads.Add(new SectionHeadAnalystWorkloadDto(a.Id, a.FullName, a.Username, aActive, aOverdue, aCompletedToday));
        }

        // Attention items
        var attentionItems = new List<SectionHeadAttentionItemDto>();

        // 1. Overdue tests - 7-day Analyst-stage SLA samples, one item per sample (not per first order); TestCodes = that sample's non-superseded Pending/InProgress test codes; reason text unchanged.
        var overdueSampleGroups = liveOverdueTestOrders
            .GroupBy(t => t.SampleId)
            .OrderBy(g => overdueAssignedAtBySampleId[g.Key])
            .Take(5)
            .ToList();

        foreach (var g in overdueSampleGroups)
        {
            var sample = g.First().Sample;
            var name = sample?.Item?.Name
                ?? sample?.WaterSamplingPoint?.Code
                ?? sample?.Department?.Name
                ?? sample?.Machine?.Name
                ?? (string.IsNullOrWhiteSpace(sample?.ReferenceNumber) ? "Sample" : sample.ReferenceNumber);
            var assignedAt = overdueAssignedAtBySampleId[g.Key];
            var delayHours = (int)Math.Floor((now - assignedAt).TotalHours);
            var testCodes = g.Select(t => t.TestCode).Distinct().ToList();

            attentionItems.Add(new SectionHeadAttentionItemDto(
                g.Key,
                sample?.ReferenceNumber ?? "",
                name,
                testCodes,
                "High",
                $"Testing stage pending for {delayHours}h (>168h / 7-day Analyst SLA)",
                "OverdueTest",
                assignedAt
            ));
        }

        // 2. Retests requested: retests in progress (D2), top 5 by ReceivedAt ascending;
        // Reason = $"Retest sample in progress (origin {originReferenceNumber})" (fall back to "Retest sample in progress" if the origin can't be loaded);
        // Urgency "High"; Timestamp = the retest sample's ReceivedAt; TestCodes = its non-superseded test codes.
        var totalRetestsInProgress = await _db.Samples.WhereRetestInProgress().CountAsync();
        var retestSamples = await _db.Samples
            .AsNoTracking()
            .WhereRetestInProgress()
            .OrderBy(s => s.ReceivedAt)
            .Take(5)
            .Include(s => s.Item)
            .Include(s => s.WaterSamplingPoint)
            .Include(s => s.Department)
            .Include(s => s.Machine)
            .Include(s => s.TestOrders)
            .ToListAsync();

        var originIds = retestSamples
            .Where(s => s.OriginSampleId.HasValue)
            .Select(s => s.OriginSampleId!.Value)
            .Distinct()
            .ToList();

        var originRefs = originIds.Count > 0
            ? await _db.Samples
                .AsNoTracking()
                .Where(s => originIds.Contains(s.Id))
                .ToDictionaryAsync(s => s.Id, s => s.ReferenceNumber)
            : new Dictionary<int, string>();

        foreach (var rs in retestSamples)
        {
            var name = rs.Item?.Name
                ?? rs.WaterSamplingPoint?.Code
                ?? rs.Department?.Name
                ?? rs.Machine?.Name
                ?? (string.IsNullOrWhiteSpace(rs.ReferenceNumber) ? "Sample" : rs.ReferenceNumber);

            string reason = "Retest sample in progress";
            if (rs.OriginSampleId.HasValue && originRefs.TryGetValue(rs.OriginSampleId.Value, out var originRef) && !string.IsNullOrWhiteSpace(originRef))
            {
                reason = $"Retest sample in progress (origin {originRef})";
            }

            var testCodes = rs.TestOrders.Where(t => !t.IsSuperseded).Select(t => t.TestCode).ToList();

            attentionItems.Add(new SectionHeadAttentionItemDto(
                rs.Id,
                rs.ReferenceNumber ?? "",
                name,
                testCodes,
                "High",
                reason,
                "RetestRequired",
                rs.ReceivedAt
            ));
        }

        // 3. Delayed reviews (>24h in UnderReview)
        foreach (var ro in reviewQueueItems.Where(r => r.AgeHours >= 24).Take(5))
        {
            attentionItems.Add(new SectionHeadAttentionItemDto(
                ro.SampleId,
                ro.ReferenceNumber,
                ro.SubjectName,
                ro.TestCodes,
                "Medium",
                $"Scientific review delayed by {ro.AgeHours}h",
                "DelayedReview",
                ro.SubmittedForReviewAt
            ));
        }

        // 4. Delayed approvals (>24h in UnderApproval)
        foreach (var ao in approvalQueueItems.Where(a => a.AgeHours >= 24).Take(5))
        {
            attentionItems.Add(new SectionHeadAttentionItemDto(
                ao.SampleId,
                ao.ReferenceNumber,
                ao.SubjectName,
                ao.TestCodes,
                "Medium",
                $"Final approval delayed by {ao.AgeHours}h",
                "DelayedApproval",
                ao.ReviewedAt
            ));
        }

        var attentionCount = overdue + totalRetestsInProgress + reviewQueueOverdue + approvalQueueOverdue;

        return new SectionHeadDashboardDto(
            activeTests,
            incubating,
            readyToRead,
            pendingReview,
            pendingApproval,
            overdue,
            attentionCount,
            activeTests,
            incubating,
            readyToRead,
            pendingReview,
            pendingApproval,
            attentionItems,
            reviewQueueItems.Count,
            reviewQueueOverdue,
            reviewQueueOldestHours,
            reviewQueueItems.Take(10).ToList(),
            approvalQueueItems.Count,
            approvalQueueOverdue,
            approvalQueueOldestHours,
            approvalQueueItems.Take(10).ToList(),
            incubationSummary,
            analystWorkloads
        );
    }

    public async Task<ReviewerDashboardDto> GetReviewerDashboardAsync(int reviewerUserId)
    {
        var now = DateTime.UtcNow;
        var todayStart = now.Date;

        // Decision D1 & Semantics: Review queue = one row per sample with Status == SampleStatus.UnderReview.
        // Tests = that sample's non-superseded TestOrders.
        var underReviewSamples = await _db.Samples
            .AsNoTracking()
            .Where(s => s.Status == SampleStatus.UnderReview)
            .Include(s => s.Item)
            .Include(s => s.WaterSamplingPoint)
            .Include(s => s.Department)
            .Include(s => s.Machine)
            .Include(s => s.TestOrders)
                .ThenInclude(t => t.Results)
            .ToListAsync();

        var sampleIds = underReviewSamples.Select(s => s.Id).ToList();

        // Batched review clock starts (Decision D3)
        var clockStarts = await SampleWorkflowQueues.GetReviewClockStartsAsync(_db, sampleIds);

        // Batched worst-of result levels (Decision D4)
        var sampleWorstLevels = await SampleWorkflowQueues.GetWorstResultLevelsAsync(_db, sampleIds);

        // All non-superseded test orders across these samples
        var allNonSupersededOrders = underReviewSamples
            .SelectMany(s => s.TestOrders.Where(t => !t.IsSuperseded))
            .ToList();

        var testOrderIds = allNonSupersededOrders.Select(t => t.Id).ToList();
        var allTestCodes = allNonSupersededOrders.Select(t => t.TestCode).Distinct().ToList();

        // Test display names from TestDefinitions
        var testDefs = await _db.TestDefinitions
            .AsNoTracking()
            .Where(td => allTestCodes.Contains(td.Code))
            .ToDictionaryAsync(td => td.Code, td => td.DisplayName);

        // Test-order level worst ResultLevels
        var testOrderWorstLevels = await SampleWorkflowQueues.GetTestOrderWorstResultLevelsAsync(_db, testOrderIds);

        // Query ResultRecords for test orders to resolve analyst who entered result/reading if assigned analyst is null
        var resultRecords = await _db.ResultRecords
            .AsNoTracking()
            .Where(r => testOrderIds.Contains(r.TestOrderId))
            .Select(r => new { r.TestOrderId, r.ResultEnteredByUserId, r.ResultEnteredByName, r.ResultEnteredAt })
            .ToListAsync();

        var userIds = allNonSupersededOrders
            .Where(t => t.AssignedAnalystId.HasValue)
            .Select(t => t.AssignedAnalystId!.Value)
            .Concat(allNonSupersededOrders.SelectMany(t => t.Results.Select(r => r.EnteredByUserId)))
            .Concat(resultRecords.Select(r => r.ResultEnteredByUserId))
            .Distinct()
            .ToList();

        var users = await _db.Users
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName);

        var reviewQueue = underReviewSamples
            .Select(s =>
            {
                var submittedAt = clockStarts.GetValueOrDefault(s.Id, s.ReceivedAt);
                var ageMins = (int)Math.Max(0, (now - submittedAt).TotalMinutes);

                var worstLevel = sampleWorstLevels.GetValueOrDefault(s.Id);
                var worstLevelStr = worstLevel?.ToString();

                var nonSupersededOrders = s.TestOrders.Where(t => !t.IsSuperseded).ToList();

                var tests = nonSupersededOrders.Select(t =>
                {
                    var testDisplayName = testDefs.TryGetValue(t.TestCode, out var dn) && !string.IsNullOrWhiteSpace(dn)
                        ? dn
                        : t.TestCode;

                    string? analystName = null;
                    if (t.AssignedAnalystId.HasValue && users.TryGetValue(t.AssignedAnalystId.Value, out var assignedName))
                    {
                        analystName = assignedName;
                    }
                    else
                    {
                        var latestResult = t.Results.OrderByDescending(r => r.EnteredAt).FirstOrDefault();
                        var latestRecord = resultRecords.Where(r => r.TestOrderId == t.Id).OrderByDescending(r => r.ResultEnteredAt).FirstOrDefault();

                        if (latestResult != null && (latestRecord == null || latestResult.EnteredAt >= latestRecord.ResultEnteredAt))
                        {
                            analystName = users.TryGetValue(latestResult.EnteredByUserId, out var n) ? n : null;
                        }
                        else if (latestRecord != null)
                        {
                            analystName = !string.IsNullOrWhiteSpace(latestRecord.ResultEnteredByName)
                                ? latestRecord.ResultEnteredByName
                                : (users.TryGetValue(latestRecord.ResultEnteredByUserId, out var n) ? n : null);
                        }
                    }

                    var testLevel = testOrderWorstLevels.GetValueOrDefault(t.Id)?.ToString();

                    return new ReviewerQueueTestDto(t.Id, t.TestCode, testDisplayName, analystName, testLevel);
                }).ToList();

                var analystNames = tests
                    .Select(t => t.AnalystName)
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .Select(n => n!)
                    .Distinct()
                    .ToList();

                // Priority: "High" if WorstResultLevel is OutOfSpecification OR AgeMinutes > 1440;
                // else "Medium" if AgeMinutes > 480; else "Normal".
                var isOos = worstLevel == ResultLevel.OutOfSpecification;
                var priority = (isOos || ageMins > 1440)
                    ? "High"
                    : (ageMins > 480 ? "Medium" : "Normal");

                var displayName = s.Item?.Name
                    ?? s.WaterSamplingPoint?.Code
                    ?? s.Department?.Name
                    ?? s.Machine?.Name
                    ?? (string.IsNullOrWhiteSpace(s.ReferenceNumber) ? "Sample" : s.ReferenceNumber);

                return new ReviewerQueueItemDto(
                    s.Id,
                    s.ReferenceNumber,
                    displayName,
                    s.Category.ToString(),
                    submittedAt,
                    ageMins,
                    priority,
                    worstLevelStr,
                    analystNames,
                    tests
                );
            })
            // Order by priority (High, Medium, Normal) then AgeMinutes desc
            .OrderByDescending(r => r.Priority == "High" ? 3 : (r.Priority == "Medium" ? 2 : 1))
            .ThenByDescending(r => r.AgeMinutes)
            .ToList();

        var pendingReviewCount = reviewQueue.Count;
        var overdueReviewCount = reviewQueue.Count(r => r.AgeMinutes >= 1440);
        var dueTodayCount = reviewQueue.Count(r => r.SubmittedForReviewAt >= todayStart);

        // Retests in progress (Decision D2)
        var retestsInProgressCount = await _db.Samples.WhereRetestInProgress().CountAsync();

        // Completed today unchanged
        var completedTodayCount = await _db.Samples.CountAsync(s => s.ReviewedByUserId == reviewerUserId && s.ReviewedAt >= todayStart);

        // Attention items: rows where Priority == "High", top 5 in queue order
        var attentionItems = new List<ReviewerAttentionItemDto>();
        foreach (var ro in reviewQueue.Where(r => r.Priority == "High").Take(5))
        {
            var oosTestCodes = ro.Tests
                .Where(t => t.ResultLevel == nameof(ResultLevel.OutOfSpecification))
                .Select(t => t.TestCode)
                .ToList();

            var reason = oosTestCodes.Count > 0
                ? $"Out of Specification result requires critical review ({string.Join(", ", oosTestCodes)})"
                : $"Review pending for {ro.AgeMinutes / 60}h (>24h SLA)";

            attentionItems.Add(new ReviewerAttentionItemDto(
                ro.SampleId,
                ro.ReferenceNumber,
                ro.SubjectName,
                ro.Tests.Select(t => t.TestCode).ToList(),
                "High",
                reason,
                ro.SubmittedForReviewAt
            ));
        }

        // Recently reviewed samples (unchanged)
        var recentSamples = await _db.Samples
            .Where(s => s.ReviewedAt != null)
            .Include(s => s.Item)
            .Include(s => s.TestOrders)
            .OrderByDescending(s => s.ReviewedAt)
            .Take(10)
            .ToListAsync();

        var recentlyReviewed = recentSamples.Select(s =>
        {
            var displayName = s.Item?.Name ?? s.ReferenceNumber;
            var testCodes = string.Join(", ", s.TestOrders.Select(t => t.TestCode));
            return new ReviewerRecentlyReviewedDto(
                s.Id, s.TestOrders.FirstOrDefault()?.Id ?? 0, s.ReferenceNumber, displayName,
                s.Category.ToString(), testCodes, s.ReviewedAt!.Value, s.Status.ToString(), s.ApprovalDecision?.ToString()
            );
        }).ToList();

        return new ReviewerDashboardDto(
            pendingReviewCount,
            overdueReviewCount,
            dueTodayCount,
            retestsInProgressCount,
            completedTodayCount,
            reviewQueue,
            attentionItems,
            recentlyReviewed
        );
    }
}
