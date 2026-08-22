using Microsoft.EntityFrameworkCore;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Application.Services;

public record TodaysWorkTestDto(int TestOrderId, string TestCode, string Status, string? TimeRemaining);
public record TodaysWorkItemDto(int SampleId, string ReferenceNumber, string Category, string DisplayName, DateTime ReceivedAt, string OverallStatus, string NextAction, List<TodaysWorkTestDto> Tests);
public record IncubationOverviewDto(string TestCode, int ReadyToRead, int Incubating);
public record AnalystMetricsDto(int TestsCompletedToday, int MediaLotsPreparedToday, int ActiveAssignedOrders, double OnTimeReadingRate, int Trailing7DayVolume);

public record SectionHeadAttentionItemDto(
    int SampleId,
    int? TestOrderId,
    string ReferenceNumber,
    string SubjectName,
    string TestCode,
    string Urgency,
    string Reason,
    string ActionType,
    DateTime Timestamp
);

public record SectionHeadReviewQueueItemDto(
    int SampleId,
    int TestOrderId,
    string ReferenceNumber,
    string SubjectName,
    string Category,
    string TestCode,
    string? AnalystName,
    DateTime ResultEnteredAt,
    double AgeHours,
    string? ResultLevel,
    string? ReportedValue,
    string? Unit
);

public record SectionHeadApprovalQueueItemDto(
    int SampleId,
    int TestOrderId,
    string ReferenceNumber,
    string SubjectName,
    string Category,
    string TestCode,
    string? ReviewerName,
    DateTime? ReviewedAt,
    double AgeHours
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

public record ReviewerQueueItemDto(
    int SampleId,
    int TestOrderId,
    string ReferenceNumber,
    string SubjectName,
    string Category,
    string TestCode,
    string TestDisplayName,
    string? AnalystName,
    DateTime ResultEnteredAt,
    int AgeMinutes,
    string Priority,
    string? ResultLevel,
    string? ReportedValue,
    string? Unit
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
    int TestOrderId,
    string ReferenceNumber,
    string SubjectName,
    string TestCode,
    string Urgency,
    string Reason,
    DateTime Timestamp
);

public record ReviewerDashboardDto(
    int PendingReviewCount,
    int OverdueReviewCount,
    int DueTodayCount,
    int ReturnedCount,
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
        var reviewerQueue = await _db.TestOrders.CountAsync(t => t.Status == ApprovalStatus.ResultEntered);
        var approvalQueue = await _db.TestOrders.CountAsync(t => t.Status == ApprovalStatus.Reviewed);
        var preparationQueue = await _db.Samples.CountAsync(s => s.PreparationStatus == SamplePreparationStatus.NeedsPreparation);

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

        return samples.Select(s =>
        {
            var tests = s.TestOrders.Select(t =>
            {
                var openIncubation = t.Incubations
                    .Where(i => i.CompletedAt == null && i.ExpectedReadingAt != null)
                    .OrderBy(i => i.ExpectedReadingAt)
                    .FirstOrDefault();
                var timeRemaining = openIncubation?.ExpectedReadingAt is { } readyAt ? FormatTimeRemaining(readyAt, now) : null;
                return new TodaysWorkTestDto(t.Id, t.TestCode, t.Status.ToString(), timeRemaining);
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
            .Where(r => r.EnteredByUserId == userId && r.EnteredAt >= todayStart)
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
            .Where(r => r.EnteredByUserId == userId && r.EnteredAt >= sevenDaysAgo)
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
        var total = await _db.TestOrders.CountAsync();
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

        var pendingReview = await _db.TestOrders.CountAsync(t => !t.IsSuperseded && t.Status == ApprovalStatus.ResultEntered);
        var pendingApproval = await _db.TestOrders.CountAsync(t => !t.IsSuperseded && t.Status == ApprovalStatus.Reviewed);

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
            .Select(t => new { t.SampleId, t.AssignedAnalystId })
            .ToListAsync();

        var overdue = liveOverdueTestOrders.Select(t => t.SampleId).Distinct().Count();
        var liveOverdueCountByAnalyst = liveOverdueTestOrders
            .Where(t => t.AssignedAnalystId.HasValue)
            .GroupBy(t => t.AssignedAnalystId!.Value)
            .ToDictionary(g => g.Key, g => g.Select(x => x.SampleId).Distinct().Count());

        // User lookup map for names
        var users = await _db.Users.AsNoTracking().Include(u => u.Role).ToListAsync();
        var userMap = users.ToDictionary(u => u.Id, u => u.FullName);

        // Review Queue details
        var reviewOrders = await _db.TestOrders
            .Where(t => !t.IsSuperseded && t.Status == ApprovalStatus.ResultEntered)
            .Include(t => t.Sample).ThenInclude(s => s!.Item)
            .Include(t => t.Sample).ThenInclude(s => s!.WaterSamplingPoint)
            .Include(t => t.Sample).ThenInclude(s => s!.Department)
            .Include(t => t.Sample).ThenInclude(s => s!.Machine)
            .Include(t => t.Results)
            .ToListAsync();

        var reviewQueueItems = reviewOrders
            .Select(t =>
            {
                var latestResult = t.Results.OrderByDescending(r => r.EnteredAt).FirstOrDefault();
                var enteredAt = latestResult?.EnteredAt ?? t.Sample?.ReceivedAt ?? now;
                var ageHours = Math.Round((now - enteredAt).TotalHours, 1);
                var analystName = t.AssignedAnalystId.HasValue && userMap.TryGetValue(t.AssignedAnalystId.Value, out var aName)
                    ? aName
                    : (latestResult != null && userMap.TryGetValue(latestResult.EnteredByUserId, out var eName) ? eName : null);
                var displayName = t.Sample?.Item?.Name ?? t.Sample?.WaterSamplingPoint?.Code ?? t.Sample?.Department?.Name ?? t.Sample?.Machine?.Name ?? t.Sample?.ReferenceNumber ?? "Sample";

                return new SectionHeadReviewQueueItemDto(
                    t.SampleId, t.Id, t.Sample?.ReferenceNumber ?? "", displayName,
                    t.Sample?.Category.ToString() ?? "", t.TestCode, analystName, enteredAt,
                    ageHours, latestResult?.Type.ToString(), latestResult?.InterpretedValue ?? latestResult?.RawValue, null
                );
            })
            .OrderByDescending(r => r.AgeHours)
            .ToList();

        var reviewQueueOverdue = reviewQueueItems.Count(r => r.AgeHours >= 24);
        var reviewQueueOldestHours = reviewQueueItems.Count > 0 ? reviewQueueItems.Max(r => r.AgeHours) : 0;

        // Approval Queue details
        var approvalOrders = await _db.TestOrders
            .Where(t => !t.IsSuperseded && t.Status == ApprovalStatus.Reviewed)
            .Include(t => t.Sample).ThenInclude(s => s!.Item)
            .Include(t => t.Sample).ThenInclude(s => s!.WaterSamplingPoint)
            .Include(t => t.Sample).ThenInclude(s => s!.Department)
            .Include(t => t.Sample).ThenInclude(s => s!.Machine)
            .ToListAsync();

        var approvalQueueItems = approvalOrders
            .Select(t =>
            {
                var reviewedAt = t.Sample?.ReviewedAt ?? t.Sample?.ReceivedAt ?? now;
                var ageHours = Math.Round((now - reviewedAt).TotalHours, 1);
                var reviewerName = t.Sample?.ReviewedByUserId.HasValue == true && userMap.TryGetValue(t.Sample.ReviewedByUserId.Value, out var rName) ? rName : null;
                var displayName = t.Sample?.Item?.Name ?? t.Sample?.WaterSamplingPoint?.Code ?? t.Sample?.Department?.Name ?? t.Sample?.Machine?.Name ?? t.Sample?.ReferenceNumber ?? "Sample";

                return new SectionHeadApprovalQueueItemDto(
                    t.SampleId, t.Id, t.Sample?.ReferenceNumber ?? "", displayName,
                    t.Sample?.Category.ToString() ?? "", t.TestCode, reviewerName, reviewedAt, ageHours
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

        // 1. Overdue tests - the same live-filtered, Rule #1 7-day
        // Analyst-stage SLA set as the tile and AnalystWorkloads above,
        // detail-fetched for display (Sample.Item, TestCode) rather than
        // re-derived against Sample.ReceivedAt here.
        var overdueList = (await _db.TestOrders
            .Where(t => !t.IsSuperseded && (t.Status == ApprovalStatus.Pending || t.Status == ApprovalStatus.InProgress) && overdueAssignedAtBySampleId.Keys.Contains(t.SampleId))
            .Include(t => t.Sample).ThenInclude(s => s!.Item)
            .ToListAsync())
            .GroupBy(t => t.SampleId)
            .Select(g => g.First())
            .OrderBy(t => overdueAssignedAtBySampleId[t.SampleId])
            .Take(5)
            .ToList();
        foreach (var ot in overdueList)
        {
            var name = ot.Sample?.Item?.Name ?? ot.Sample?.ReferenceNumber ?? "Sample";
            var assignedAt = overdueAssignedAtBySampleId[ot.SampleId];
            var delayHours = (int)Math.Floor((now - assignedAt).TotalHours);
            attentionItems.Add(new SectionHeadAttentionItemDto(
                ot.SampleId, ot.Id, ot.Sample?.ReferenceNumber ?? "", name, ot.TestCode,
                "High", $"Testing stage pending for {delayHours}h (>168h / 7-day Analyst SLA)", "OverdueTest", assignedAt
            ));
        }

        // 2. Retests requested
        var retests = await _db.TestOrders
            .Where(t => !t.IsSuperseded && t.Status == ApprovalStatus.RetestRequested)
            .Include(t => t.Sample).ThenInclude(s => s!.Item)
            .Take(5)
            .ToListAsync();
        foreach (var rt in retests)
        {
            var name = rt.Sample?.Item?.Name ?? rt.Sample?.ReferenceNumber ?? "Sample";
            attentionItems.Add(new SectionHeadAttentionItemDto(
                rt.SampleId, rt.Id, rt.Sample?.ReferenceNumber ?? "", name, rt.TestCode,
                "High", "Retest requested on sample", "RetestRequired", now
            ));
        }

        // 3. Delayed reviews (>24h in result entered)
        foreach (var ro in reviewQueueItems.Where(r => r.AgeHours >= 24).Take(5))
        {
            attentionItems.Add(new SectionHeadAttentionItemDto(
                ro.SampleId, ro.TestOrderId, ro.ReferenceNumber, ro.SubjectName, ro.TestCode,
                "Medium", $"Scientific review delayed by {ro.AgeHours}h", "DelayedReview", ro.ResultEnteredAt
            ));
        }

        var attentionCount = overdue + retests.Count + reviewQueueOverdue + approvalQueueOverdue;

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

        var users = await _db.Users.AsNoTracking().ToListAsync();
        var userMap = users.ToDictionary(u => u.Id, u => u.FullName);

        var reviewOrders = await _db.TestOrders
            .Where(t => !t.IsSuperseded && t.Status == ApprovalStatus.ResultEntered)
            .Include(t => t.Sample).ThenInclude(s => s!.Item)
            .Include(t => t.Sample).ThenInclude(s => s!.WaterSamplingPoint)
            .Include(t => t.Sample).ThenInclude(s => s!.Department)
            .Include(t => t.Sample).ThenInclude(s => s!.Machine)
            .Include(t => t.Results)
            .ToListAsync();

        var reviewQueue = reviewOrders
            .Select(t =>
            {
                var latestResult = t.Results.OrderByDescending(r => r.EnteredAt).FirstOrDefault();
                var enteredAt = latestResult?.EnteredAt ?? t.Sample?.ReceivedAt ?? now;
                var ageMins = (int)Math.Max(0, (now - enteredAt).TotalMinutes);
                var analystName = t.AssignedAnalystId.HasValue && userMap.TryGetValue(t.AssignedAnalystId.Value, out var aName)
                    ? aName
                    : (latestResult != null && userMap.TryGetValue(latestResult.EnteredByUserId, out var eName) ? eName : "Analyst");
                var displayName = t.Sample?.Item?.Name ?? t.Sample?.WaterSamplingPoint?.Code ?? t.Sample?.Department?.Name ?? t.Sample?.Machine?.Name ?? t.Sample?.ReferenceNumber ?? "Sample";
                var priority = ageMins > 1440 ? "High" : (ageMins > 480 ? "Medium" : "Normal");

                return new ReviewerQueueItemDto(
                    t.SampleId, t.Id, t.Sample?.ReferenceNumber ?? "", displayName,
                    t.Sample?.Category.ToString() ?? "", t.TestCode, t.TestCode,
                    analystName, enteredAt, ageMins, priority,
                    latestResult?.Type.ToString(), latestResult?.InterpretedValue ?? latestResult?.RawValue, null
                );
            })
            .OrderByDescending(r => r.Priority == "High" ? 3 : (r.Priority == "Medium" ? 2 : 1))
            .ThenByDescending(r => r.AgeMinutes)
            .ToList();

        var overdueReviews = reviewQueue.Count(r => r.AgeMinutes >= 1440);
        var dueToday = reviewQueue.Count(r => r.ResultEnteredAt >= todayStart);
        var returnedCount = await _db.TestOrders.CountAsync(t => !t.IsSuperseded && t.Status == ApprovalStatus.RetestRequested);

        // Completed today by this reviewer or all
        var completedTodayCount = await _db.Samples.CountAsync(s => s.ReviewedByUserId == reviewerUserId && s.ReviewedAt >= todayStart);

        // Attention items for reviewer
        var attentionItems = new List<ReviewerAttentionItemDto>();
        foreach (var ro in reviewQueue.Where(r => r.Priority == "High" || r.ResultLevel == "OutOfSpecification").Take(5))
        {
            var reason = ro.ResultLevel == "OutOfSpecification"
                ? "Out of Specification result requires critical review"
                : $"Review pending for {(int)(ro.AgeMinutes / 60)}h (>24h SLA)";
            attentionItems.Add(new ReviewerAttentionItemDto(
                ro.SampleId, ro.TestOrderId, ro.ReferenceNumber, ro.SubjectName, ro.TestCode,
                "High", reason, ro.ResultEnteredAt
            ));
        }

        // Recently reviewed samples
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
            reviewQueue.Count,
            overdueReviews,
            dueToday,
            returnedCount,
            completedTodayCount,
            reviewQueue,
            attentionItems,
            recentlyReviewed
        );
    }
}
