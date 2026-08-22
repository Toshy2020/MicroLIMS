using Microsoft.EntityFrameworkCore;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Application.Services;

public record AnalystKpiDto(int UserId, string Username, int CompletedTests, int PendingTests, double AverageTurnaroundHours);
public record CompletionStatsDto(int TotalTestOrders, int Approved, int Rejected, int Pending, double ApprovalRatePercent);
public record DelayTrackingDto(int DelayedCount, double AverageDelayHours);

// Sample-level queue depths for the Review and Approval workflow gates -
// deliberately Sample.Status counts, not TestOrder.Status like
// CompletionStatsDto: a sample enters UnderReview/UnderApproval as a
// whole once all its TestOrders finish testing, so "how many samples are
// sitting in each gate" is a Sample-level question, not a per-test one.
public record SampleQueueCountsDto(int ReviewQueueCount, int ApprovalQueueCount);

// Rule #1's 7-day sample-assignment SLA: "assigned" to "submitted for
// review". OnTimeCount + OverdueCount == TotalAssigned - every assigned
// sample in the window is in exactly one bucket, including ones still
// pending (judged against the SLA as of now, not left in limbo).
public record SampleSlaOutcomeDto(int TotalAssigned, int OnTimeCount, int OverdueCount);

// Step-level max-hours violation (Incubation.CompletedAt vs
// ExpectedReadingAt + 4h grace). ViolationCount is the raw count of
// violating incubation windows; TestsWithViolationCount is how many
// distinct TestOrders had at least one - deliberately different numbers,
// since one test can carry several incubation steps.
public record StepViolationOutcomeDto(int TotalAssignedTests, int ViolationCount, int TestsWithViolationCount);

// Rule #1-2's three-stage TAT, in days - Testing (Analyst), Review, and
// Approval each averaged independently over whichever samples in the
// window actually completed that stage. Any of the three can be null
// (no sample in the window has finished that stage yet) - TotalAvgDays
// then treats the missing stage(s) as 0 rather than propagating null,
// matching how a "Total Lifecycle" figure reads most naturally.
public record StageTatSummaryDto(double? TestingAvgDays, double? ReviewAvgDays, double? ApprovalAvgDays, double? TotalAvgDays);

// One calendar month's average Testing-stage (Analyst) TAT, bucketed by
// when that stage concluded (SubmittedForReviewAt) - the same "last N
// calendar months ending this month" window ReportingQueryService.
// GetCompletedByMonthAsync already uses, for consistency between the two
// monthly charts on this page.
public record MonthlyTatPoint(string Month, double? AvgTestingDays);

// Rule #3's "calendar month vs previous calendar month" delta, applied to
// each Workflow Bottleneck queue's inflow (new arrivals this month vs
// last) - the same convention and formula as DashboardService.
// GetKpiDeltasAsync, just applied to the three queue-entry events instead
// of samples/tests overall. A queue's live depth isn't itself
// reconstructable for a past month (no periodic snapshots are stored),
// so "vs prev" here means arrival rate, matching what GetKpiDeltasAsync
// already measures for its own samples/tests figures.
public record WorkflowBottleneckDeltaDto(double TestingQueueDeltaPercent, double ReviewQueueDeltaPercent, double ApprovalQueueDeltaPercent);

// Rule #1's on-time completion, combined across every stage a sample has
// actually reached (Analyst 7d AND Reviewer 24h AND Section-Head 24h) -
// distinct from SampleSlaOutcomeDto, which judges the Analyst/testing
// stage alone. Same TotalAssigned==OnTimeCount+OverdueCount invariant: a
// sample still sitting in whichever stage it's currently in is judged
// against that stage's deadline as of now, not left unclassified.
public record OverallOnTimeOutcomeDto(int TotalAssigned, int OnTimeCount, int OverdueCount);

// One sample that's overdue on Rule #1's 7-day Analyst-stage SLA -
// backs DashboardService's Section Head "Attention Items" list, which
// needs to name specific samples, not just a count.
public record OverdueAnalystStageSampleDto(int SampleId, DateTime AssignedAt);

// Analyst KPIs, delay tracking, completion statistics - gap analysis
// "Missing Laboratory Modules - KPI".
public class KpiService
{
    // Rule #1's Reviewer/Section-Head SLA (24h) - kept as its own named
    // constant, separate from AnalystAssignmentSla (7 days), so no code
    // path can silently apply the wrong stage's threshold. That mix-up
    // (this 24h number being read for Analyst-stage overdue math even
    // though it's really the Reviewer/Section-Head figure) is exactly the
    // gap this batch closes. Only GetDelayTrackingAsync's generic "how
    // many pending test orders are stuck" signal reads this today - that
    // metric is TestOrder-level and category-agnostic by design, not a
    // per-stage SLA breakdown, so it's deliberately left as its own thing
    // rather than folded into the stage-aware methods below.
    private static readonly TimeSpan ReviewerApprovalDelayThreshold = TimeSpan.FromHours(24);

    // Rule #1's Analyst-stage SLA: assignment -> submitted for review.
    private static readonly TimeSpan AnalystAssignmentSla = TimeSpan.FromDays(7);

    private readonly MicroLimsDbContext _db;

    public KpiService(MicroLimsDbContext db)
    {
        _db = db;
    }

    public async Task<List<AnalystKpiDto>> GetAnalystKpisAsync()
    {
        var analysts = await _db.Users.Include(u => u.Role)
            .Where(u => u.Role!.Type == RoleType.Analyst)
            .ToListAsync();

        var result = new List<AnalystKpiDto>();
        foreach (var analyst in analysts)
        {
            var completed = await _db.TestOrders.CountAsync(t => t.AssignedAnalystId == analyst.Id && t.Status == ApprovalStatus.Approved);
            var pending = await _db.TestOrders.CountAsync(t => t.AssignedAnalystId == analyst.Id &&
                (t.Status == ApprovalStatus.Pending || t.Status == ApprovalStatus.InProgress));

            // Average turnaround: time from sample received to result entered,
            // for this analyst's completed test orders.
            var turnaroundHours = await _db.TestOrders
                .Where(t => t.AssignedAnalystId == analyst.Id && t.Status == ApprovalStatus.Approved)
                .Join(_db.Samples, t => t.SampleId, s => s.Id, (t, s) => new { t.Id, s.ReceivedAt })
                .Join(_db.Results, x => x.Id, r => r.TestOrderId, (x, r) => new { x.ReceivedAt, r.EnteredAt })
                .ToListAsync();

            var avgHours = turnaroundHours.Count > 0
                ? turnaroundHours.Average(x => (x.EnteredAt - x.ReceivedAt).TotalHours)
                : 0;

            result.Add(new AnalystKpiDto(analyst.Id, analyst.Username, completed, pending, Math.Round(avgHours, 1)));
        }

        return result;
    }

    public async Task<CompletionStatsDto> GetCompletionStatsAsync()
    {
        var total = await _db.TestOrders.CountAsync();
        var approved = await _db.TestOrders.CountAsync(t => t.Status == ApprovalStatus.Approved);
        var rejected = await _db.TestOrders.CountAsync(t => t.Status == ApprovalStatus.Rejected);
        var pending = await _db.TestOrders.CountAsync(t => t.Status == ApprovalStatus.Pending || t.Status == ApprovalStatus.InProgress);

        var decided = approved + rejected;
        var approvalRate = decided > 0 ? Math.Round(approved * 100.0 / decided, 1) : 0;

        return new CompletionStatsDto(total, approved, rejected, pending, approvalRate);
    }

    public async Task<DelayTrackingDto> GetDelayTrackingAsync()
    {
        var cutoff = DateTime.UtcNow.Subtract(ReviewerApprovalDelayThreshold);

        var delayed = await _db.TestOrders
            .Where(t => t.Status == ApprovalStatus.Pending || t.Status == ApprovalStatus.InProgress)
            .Join(_db.Samples, t => t.SampleId, s => s.Id, (t, s) => s.ReceivedAt)
            .Where(receivedAt => receivedAt < cutoff)
            .ToListAsync();

        var avgDelayHours = delayed.Count > 0
            ? delayed.Average(receivedAt => (DateTime.UtcNow - receivedAt).TotalHours)
            : 0;

        return new DelayTrackingDto(delayed.Count, Math.Round(avgDelayHours, 1));
    }

    public async Task<SampleQueueCountsDto> GetSampleQueueCountsAsync()
    {
        var reviewQueueCount = await _db.Samples.CountAsync(s => s.Status == SampleStatus.UnderReview);
        var approvalQueueCount = await _db.Samples.CountAsync(s => s.Status == SampleStatus.UnderApproval);
        return new SampleQueueCountsDto(reviewQueueCount, approvalQueueCount);
    }

    // Rule #3: each queue's delta is its arrival count this calendar month
    // vs last - Testing queue arrivals are new TestOrders whose Sample was
    // just received (mirrors DashboardService.GetKpiDeltasAsync's own
    // testsThisMonth/testsLastMonth exactly); Review/Approval queue
    // arrivals are SubmittedForReview/ReviewCompleted events, the same
    // entry points BuildSampleStageWindowsAsync reads elsewhere.
    public async Task<WorkflowBottleneckDeltaDto> GetWorkflowBottleneckDeltasAsync()
    {
        var now = DateTime.UtcNow;
        var thisMonthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var lastMonthStart = thisMonthStart.AddMonths(-1);

        var testingThisMonth = await _db.TestOrders.CountAsync(t => _db.Samples.Any(s => s.Id == t.SampleId && s.ReceivedAt >= thisMonthStart));
        var testingLastMonth = await _db.TestOrders.CountAsync(t => _db.Samples.Any(s => s.Id == t.SampleId && s.ReceivedAt >= lastMonthStart && s.ReceivedAt < thisMonthStart));

        var reviewThisMonth = await _db.ReviewWorkflowEvents.CountAsync(e =>
            e.EntityType == ReviewEntityTypes.Sample && e.EventType == ReviewWorkflowEventType.SubmittedForReview && e.Timestamp >= thisMonthStart);
        var reviewLastMonth = await _db.ReviewWorkflowEvents.CountAsync(e =>
            e.EntityType == ReviewEntityTypes.Sample && e.EventType == ReviewWorkflowEventType.SubmittedForReview && e.Timestamp >= lastMonthStart && e.Timestamp < thisMonthStart);

        var approvalThisMonth = await _db.ReviewWorkflowEvents.CountAsync(e =>
            e.EntityType == ReviewEntityTypes.Sample && e.EventType == ReviewWorkflowEventType.ReviewCompleted && e.Timestamp >= thisMonthStart);
        var approvalLastMonth = await _db.ReviewWorkflowEvents.CountAsync(e =>
            e.EntityType == ReviewEntityTypes.Sample && e.EventType == ReviewWorkflowEventType.ReviewCompleted && e.Timestamp >= lastMonthStart && e.Timestamp < thisMonthStart);

        static double DeltaPercent(int thisMonth, int lastMonth) => lastMonth == 0 ? 0 : Math.Round((thisMonth - lastMonth) * 100.0 / lastMonth, 1);

        return new WorkflowBottleneckDeltaDto(
            DeltaPercent(testingThisMonth, testingLastMonth),
            DeltaPercent(reviewThisMonth, reviewLastMonth),
            DeltaPercent(approvalThisMonth, approvalLastMonth));
    }

    // Per-sample stage windows - the single shared calculation every
    // stage-aware method below (GetSampleAssignmentSlaAsync,
    // GetSampleAssignmentSlaByAnalystAsync, GetStageTatSummaryAsync,
    // GetTestingTatByMonthAsync) is built on, so there's one place that
    // knows how to derive AssignedAt and read the review/approval
    // timeline - not four independent re-derivations.
    //
    // AssignedAt: there's no single AssignedAt column anywhere.
    // Assignment happens implicitly at Test Preparation
    // (SamplePreparationService sets TestOrder.AssignedAnalystId with no
    // timestamp of its own; the only nearby timestamp is
    // SamplePreparation.PreparedAt) or explicitly via Section Head
    // reassignment (SampleAssignmentService, logged to AuditLogs with a
    // real Timestamp but likewise no dedicated column). The clock starts
    // at whichever actually happened most recently - a reassignment
    // resets who's accountable, so it resets the SLA clock too. Verified
    // against live data: every sample with an assigned TestOrder has a
    // SamplePreparation row, so this fallback always resolves.
    //
    // Review/Approval timestamps: SampleStatus.RetestRequested is never
    // actually assigned anywhere in this codebase (a retest sends the
    // sample back to InTesting directly - SampleApprovalService.
    // DecideAsync's RetestRetainedSample branch - and a fresh testing
    // round re-triggers a brand new SubmittedForReview event later, once
    // the new TestOrders complete). Taking the LATEST of each event type
    // is what "excluding time spent in a retest loop" actually means
    // given how the state machine really works: it picks out only the
    // final, successful round's stage windows and discards any earlier
    // round's numbers entirely, rather than trying to subtract time
    // against a status value that's never set.
    private record SampleStageWindow(
        int SampleId, HashSet<int> AssignedAnalystIds, DateTime AssignedAt,
        DateTime? SubmittedForReviewAt, DateTime? ReviewCompletedAt, DateTime? ApprovalDecisionAt);

    private async Task<List<SampleStageWindow>> BuildSampleStageWindowsAsync()
    {
        var assignedOrders = await _db.TestOrders
            .Where(t => t.AssignedAnalystId != null)
            .Select(t => new { t.SampleId, AnalystId = t.AssignedAnalystId!.Value })
            .ToListAsync();

        var sampleAnalysts = assignedOrders
            .GroupBy(o => o.SampleId)
            .ToDictionary(g => g.Key, g => g.Select(o => o.AnalystId).ToHashSet());

        var sampleIds = sampleAnalysts.Keys.ToList();
        if (sampleIds.Count == 0) return new List<SampleStageWindow>();

        var preparedAtLookup = await _db.SamplePreparations
            .Where(p => sampleIds.Contains(p.SampleId))
            .ToDictionaryAsync(p => p.SampleId, p => p.PreparedAt);

        var reassignedAtLookup = await _db.AuditLogs
            .Where(a => a.SampleId != null && sampleIds.Contains(a.SampleId.Value)
                && (a.Action == "AssignedAnalyst" || a.Action == "ReassignedAnalyst"))
            .GroupBy(a => a.SampleId!.Value)
            .Select(g => new { SampleId = g.Key, LatestTimestamp = g.Max(a => a.Timestamp) })
            .ToDictionaryAsync(g => g.SampleId, g => g.LatestTimestamp);

        var events = await _db.ReviewWorkflowEvents
            .Where(e => e.EntityType == ReviewEntityTypes.Sample && sampleIds.Contains(e.EntityId))
            .Select(e => new { e.EntityId, e.EventType, e.Timestamp })
            .ToListAsync();

        var submittedAtLookup = events.Where(e => e.EventType == ReviewWorkflowEventType.SubmittedForReview)
            .GroupBy(e => e.EntityId).ToDictionary(g => g.Key, g => g.Max(e => e.Timestamp));
        var reviewCompletedAtLookup = events.Where(e => e.EventType == ReviewWorkflowEventType.ReviewCompleted)
            .GroupBy(e => e.EntityId).ToDictionary(g => g.Key, g => g.Max(e => e.Timestamp));
        var approvalDecisionAtLookup = events.Where(e => e.EventType == ReviewWorkflowEventType.ApprovalDecisionMade)
            .GroupBy(e => e.EntityId).ToDictionary(g => g.Key, g => g.Max(e => e.Timestamp));

        var result = new List<SampleStageWindow>();
        foreach (var sampleId in sampleIds)
        {
            // No SamplePreparation row means this sample was never
            // actually prepared - shouldn't happen for a TestOrder with
            // AssignedAnalystId set, but skip rather than assume a clock
            // start that was never recorded.
            if (!preparedAtLookup.TryGetValue(sampleId, out var preparedAt)) continue;

            var assignedAt = reassignedAtLookup.TryGetValue(sampleId, out var reassignedAt) && reassignedAt > preparedAt
                ? reassignedAt
                : preparedAt;

            DateTime? submittedAt = submittedAtLookup.TryGetValue(sampleId, out var sa) ? sa : null;
            DateTime? reviewCompletedAt = reviewCompletedAtLookup.TryGetValue(sampleId, out var rc) ? rc : null;
            DateTime? approvalDecisionAt = approvalDecisionAtLookup.TryGetValue(sampleId, out var ad) ? ad : null;

            result.Add(new SampleStageWindow(sampleId, sampleAnalysts[sampleId], assignedAt, submittedAt, reviewCompletedAt, approvalDecisionAt));
        }
        return result;
    }

    // Rule #1's Analyst-stage overdue check - the one place this
    // comparison is made, so GetSampleAssignmentSlaAsync,
    // GetSampleAssignmentSlaByAnalystAsync, and
    // GetOverdueAnalystStageSamplesAsync below (and DashboardService,
    // through them) can never drift apart on what "overdue" means.
    private bool IsAnalystStageOverdue(SampleStageWindow w, DateTime now)
    {
        var deadline = w.AssignedAt.Add(AnalystAssignmentSla);
        return w.SubmittedForReviewAt.HasValue ? w.SubmittedForReviewAt.Value > deadline : now > deadline;
    }

    // Rule #1: 7-day SLA from "analyst assigning" to "submit to review".
    public async Task<SampleSlaOutcomeDto> GetSampleAssignmentSlaAsync(int? analystId, DateTime fromDate, DateTime toDate)
    {
        var windows = await BuildSampleStageWindowsAsync();
        var now = DateTime.UtcNow;
        int totalAssigned = 0, onTime = 0, overdue = 0;

        foreach (var w in windows)
        {
            if (analystId.HasValue && !w.AssignedAnalystIds.Contains(analystId.Value)) continue;
            if (w.AssignedAt < fromDate || w.AssignedAt > toDate) continue;

            totalAssigned++;
            if (IsAnalystStageOverdue(w, now)) overdue++; else onTime++;
        }

        return new SampleSlaOutcomeDto(totalAssigned, onTime, overdue);
    }

    // Same 7-day Analyst SLA as above, broken out per analyst - what the
    // Analyst Comparison table's Overdue column needs. A sample with
    // TestOrders split across two analysts counts toward both - the SLA
    // is whole-sample, so both analysts share accountability for it.
    public async Task<Dictionary<int, SampleSlaOutcomeDto>> GetSampleAssignmentSlaByAnalystAsync(DateTime fromDate, DateTime toDate)
    {
        var windows = await BuildSampleStageWindowsAsync();
        var now = DateTime.UtcNow;
        var byAnalyst = new Dictionary<int, (int Total, int OnTime, int Overdue)>();

        foreach (var w in windows)
        {
            if (w.AssignedAt < fromDate || w.AssignedAt > toDate) continue;

            var isOverdue = IsAnalystStageOverdue(w, now);

            foreach (var analystId in w.AssignedAnalystIds)
            {
                byAnalyst.TryGetValue(analystId, out var agg);
                byAnalyst[analystId] = (agg.Total + 1, agg.OnTime + (isOverdue ? 0 : 1), agg.Overdue + (isOverdue ? 1 : 0));
            }
        }

        return byAnalyst.ToDictionary(kv => kv.Key, kv => new SampleSlaOutcomeDto(kv.Value.Total, kv.Value.OnTime, kv.Value.Overdue));
    }

    // The individual samples behind GetSampleAssignmentSlaAsync's overdue
    // count, oldest-assigned first - what DashboardService's Section Head
    // "Attention Items" list needs to name specific overdue samples
    // instead of just a count. AssignedAt is returned alongside each
    // SampleId so the caller can report real elapsed time against the
    // real clock start, not Sample.ReceivedAt.
    public async Task<List<OverdueAnalystStageSampleDto>> GetOverdueAnalystStageSamplesAsync(DateTime fromDate, DateTime toDate)
    {
        var windows = await BuildSampleStageWindowsAsync();
        var now = DateTime.UtcNow;

        return windows
            .Where(w => w.AssignedAt >= fromDate && w.AssignedAt <= toDate && IsAnalystStageOverdue(w, now))
            .OrderBy(w => w.AssignedAt)
            .Select(w => new OverdueAnalystStageSampleDto(w.SampleId, w.AssignedAt))
            .ToList();
    }

    // Rule #1's on-time completion across all three stages a sample has
    // actually reached: each stage it passed through (or is currently
    // sitting in) is checked against its own SLA, short-circuiting to
    // overdue at the first one that fails. A stage the sample hasn't
    // reached yet doesn't count against it - it's only ever judged on
    // the stage(s) it has actually been through.
    private bool IsOverallOnTime(SampleStageWindow w, DateTime now)
    {
        var testingDeadline = w.AssignedAt.Add(AnalystAssignmentSla);
        var testingOnTime = w.SubmittedForReviewAt.HasValue ? w.SubmittedForReviewAt.Value <= testingDeadline : now <= testingDeadline;
        if (!testingOnTime) return false;
        if (!w.SubmittedForReviewAt.HasValue) return true;

        var reviewDeadline = w.SubmittedForReviewAt.Value.Add(ReviewerApprovalDelayThreshold);
        var reviewOnTime = w.ReviewCompletedAt.HasValue ? w.ReviewCompletedAt.Value <= reviewDeadline : now <= reviewDeadline;
        if (!reviewOnTime) return false;
        if (!w.ReviewCompletedAt.HasValue) return true;

        var approvalDeadline = w.ReviewCompletedAt.Value.Add(ReviewerApprovalDelayThreshold);
        return w.ApprovalDecisionAt.HasValue ? w.ApprovalDecisionAt.Value <= approvalDeadline : now <= approvalDeadline;
    }

    public async Task<OverallOnTimeOutcomeDto> GetOverallOnTimeCompletionAsync(DateTime fromDate, DateTime toDate)
    {
        var windows = await BuildSampleStageWindowsAsync();
        var now = DateTime.UtcNow;
        int total = 0, onTime = 0, overdue = 0;

        foreach (var w in windows)
        {
            if (w.AssignedAt < fromDate || w.AssignedAt > toDate) continue;
            total++;
            if (IsOverallOnTime(w, now)) onTime++; else overdue++;
        }

        return new OverallOnTimeOutcomeDto(total, onTime, overdue);
    }

    // Same all-stage on-time definition, broken out per analyst - what
    // the Analyst Comparison table's On-Time % column needs.
    public async Task<Dictionary<int, OverallOnTimeOutcomeDto>> GetOverallOnTimeCompletionByAnalystAsync(DateTime fromDate, DateTime toDate)
    {
        var windows = await BuildSampleStageWindowsAsync();
        var now = DateTime.UtcNow;
        var byAnalyst = new Dictionary<int, (int Total, int OnTime, int Overdue)>();

        foreach (var w in windows)
        {
            if (w.AssignedAt < fromDate || w.AssignedAt > toDate) continue;
            var isOnTime = IsOverallOnTime(w, now);

            foreach (var analystId in w.AssignedAnalystIds)
            {
                byAnalyst.TryGetValue(analystId, out var agg);
                byAnalyst[analystId] = (agg.Total + 1, agg.OnTime + (isOnTime ? 1 : 0), agg.Overdue + (isOnTime ? 0 : 1));
            }
        }

        return byAnalyst.ToDictionary(kv => kv.Key, kv => new OverallOnTimeOutcomeDto(kv.Value.Total, kv.Value.OnTime, kv.Value.Overdue));
    }

    // Rule #1-2's three-stage average TAT (days), scoped by assignment
    // window (same population GetSampleAssignmentSlaAsync uses) and
    // optionally by analyst. Only samples that have actually finished a
    // given stage contribute to that stage's average - a sample still
    // sitting in UnderReview has no Review duration yet to average in.
    public async Task<StageTatSummaryDto> GetStageTatSummaryAsync(int? analystId, DateTime fromDate, DateTime toDate)
    {
        var windows = await BuildSampleStageWindowsAsync();
        var scoped = windows.Where(w => w.AssignedAt >= fromDate && w.AssignedAt <= toDate
            && (!analystId.HasValue || w.AssignedAnalystIds.Contains(analystId.Value))).ToList();

        static double? AvgDays(IEnumerable<double> hours)
        {
            var list = hours.ToList();
            return list.Count > 0 ? Math.Round(list.Average() / 24.0, 1) : null;
        }

        var testingAvg = AvgDays(scoped
            .Where(w => w.SubmittedForReviewAt.HasValue)
            .Select(w => (w.SubmittedForReviewAt!.Value - w.AssignedAt).TotalHours));

        var reviewAvg = AvgDays(scoped
            .Where(w => w.SubmittedForReviewAt.HasValue && w.ReviewCompletedAt.HasValue && w.ReviewCompletedAt > w.SubmittedForReviewAt)
            .Select(w => (w.ReviewCompletedAt!.Value - w.SubmittedForReviewAt!.Value).TotalHours));

        var approvalAvg = AvgDays(scoped
            .Where(w => w.ReviewCompletedAt.HasValue && w.ApprovalDecisionAt.HasValue && w.ApprovalDecisionAt > w.ReviewCompletedAt)
            .Select(w => (w.ApprovalDecisionAt!.Value - w.ReviewCompletedAt!.Value).TotalHours));

        var totalAvg = Math.Round((testingAvg ?? 0) + (reviewAvg ?? 0) + (approvalAvg ?? 0), 1);

        return new StageTatSummaryDto(testingAvg, reviewAvg, approvalAvg, totalAvg);
    }

    // Historical monthly average Testing-stage (Analyst) TAT, bucketed by
    // when that stage concluded - mirrors ReportingQueryService.
    // GetCompletedByMonthAsync's "last N calendar months ending this
    // month" window exactly.
    public async Task<List<MonthlyTatPoint>> GetTestingTatByMonthAsync(int months = 6)
    {
        var windows = await BuildSampleStageWindowsAsync();
        var completed = windows
            .Where(w => w.SubmittedForReviewAt.HasValue)
            .Select(w => new { SubmittedAt = w.SubmittedForReviewAt!.Value, Hours = (w.SubmittedForReviewAt!.Value - w.AssignedAt).TotalHours })
            .ToList();

        var now = DateTime.UtcNow;
        var currentMonthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var points = new List<MonthlyTatPoint>();
        for (var i = 0; i < months; i++)
        {
            var monthStart = currentMonthStart.AddMonths(-(months - 1) + i);
            var monthEnd = monthStart.AddMonths(1);
            var inMonth = completed.Where(c => c.SubmittedAt >= monthStart && c.SubmittedAt < monthEnd).Select(c => c.Hours).ToList();
            var avgDays = inMonth.Count > 0 ? Math.Round(inMonth.Average() / 24.0, 1) : (double?)null;
            points.Add(new MonthlyTatPoint(monthStart.ToString("MMM"), avgDays));
        }
        return points;
    }

    // Step-level max-hours violation. Mirrors DashboardService.
    // GetAnalystMetricsAsync's own on-time-reading-rate calculation
    // exactly - same attribution (assigned analyst OR whoever physically
    // started the window) and same 4-hour grace past ExpectedReadingAt -
    // built as its own query here (not by editing DashboardService)
    // because the KPI page needs an arbitrary date range and an optional
    // analyst filter rather than a fixed "today" snapshot for one caller.
    // "Total Assigned Tests" is scoped by incubation activity in the
    // window (not the Query 1 assignment-clock concept above) since this
    // card is about step-reading violations, not the sample SLA.
    public async Task<StepViolationOutcomeDto> GetStepViolationsAsync(int? analystId, DateTime fromDate, DateTime toDate)
    {
        var incubationsQuery = _db.Incubations
            .Where(i => i.CompletedAt != null && i.ExpectedReadingAt != null
                && i.CompletedAt >= fromDate && i.CompletedAt <= toDate);

        incubationsQuery = analystId.HasValue
            ? incubationsQuery.Where(i => i.TestOrder!.AssignedAnalystId == analystId.Value || i.StartedByUserId == analystId.Value)
            : incubationsQuery.Where(i => i.TestOrder!.AssignedAnalystId != null || i.StartedByUserId != null);

        var incubations = await incubationsQuery
            .Select(i => new { i.TestOrderId, i.CompletedAt, i.ExpectedReadingAt })
            .ToListAsync();

        var totalAssignedTests = incubations.Select(i => i.TestOrderId).Distinct().Count();

        var violatingTestOrderIds = incubations
            .Where(i => i.CompletedAt!.Value > i.ExpectedReadingAt!.Value.AddHours(4))
            .Select(i => i.TestOrderId)
            .ToList();

        return new StepViolationOutcomeDto(totalAssignedTests, violatingTestOrderIds.Count, violatingTestOrderIds.Distinct().Count());
    }
}
