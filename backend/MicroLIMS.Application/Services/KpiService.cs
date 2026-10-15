using Microsoft.EntityFrameworkCore;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Application.Abstractions.Persistence;

namespace MicroLIMS.Application.Services;

// One sample that's overdue on Rule #1's 7-day Analyst-stage SLA -
// backs DashboardService's Section Head "Attention Items" list, which
// needs to name specific samples, not just a count.
public record OverdueAnalystStageSampleDto(int SampleId, DateTime AssignedAt);

// What is left of the pre-remodel KPI service: the 7-day Analyst-stage
// overdue check that DashboardService's live "Attention Items" list still
// reads. Every reported KPI now lives in the registry (Services/Kpi), so do
// not add metrics here.
public class KpiService
{
    // Rule #1's Analyst-stage SLA: assignment -> submitted for review.
    private static readonly TimeSpan AnalystAssignmentSla = TimeSpan.FromDays(7);

    private readonly IMicroLimsDbContext _db;
    private readonly TimeProvider _time;

    public KpiService(IMicroLimsDbContext db, TimeProvider? timeProvider = null)
    {
        _time = timeProvider ?? TimeProvider.System;
        _db = db;
    }

    // Per-sample stage windows - the shared calculation behind the overdue
    // check below, so there's one place that knows how to derive AssignedAt
    // and read the review/approval timeline.
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

    private async Task<List<SampleStageWindow>> BuildSampleStageWindowsAsync(IReadOnlyCollection<int>? sectionIds)
    {
        var assignedOrders = await _db.TestOrders.Where(SectionReviewQueues.OrderIn(sectionIds))
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
    // comparison is made, so GetOverdueAnalystStageSamplesAsync (and
    // DashboardService through it) can never drift apart on what
    // "overdue" means.
    private bool IsAnalystStageOverdue(SampleStageWindow w, DateTime now)
    {
        var deadline = w.AssignedAt.Add(AnalystAssignmentSla);
        return w.SubmittedForReviewAt.HasValue ? w.SubmittedForReviewAt.Value > deadline : now > deadline;
    }

    // The individual samples that are overdue on the 7-day Analyst-stage
    // SLA, oldest-assigned first - what DashboardService's Section Head
    // "Attention Items" list needs to name specific overdue samples
    // instead of just a count. AssignedAt is returned alongside each
    // SampleId so the caller can report real elapsed time against the
    // real clock start, not Sample.ReceivedAt.
    public async Task<List<OverdueAnalystStageSampleDto>> GetOverdueAnalystStageSamplesAsync(DateTime fromDate, DateTime toDate,
        IReadOnlyCollection<int>? sectionIds = null)
    {
        var windows = await BuildSampleStageWindowsAsync(sectionIds);
        var now = _time.GetUtcNow().UtcDateTime;

        return windows
            .Where(w => w.AssignedAt >= fromDate && w.AssignedAt <= toDate && IsAnalystStageOverdue(w, now))
            .OrderBy(w => w.AssignedAt)
            .Select(w => new OverdueAnalystStageSampleDto(w.SampleId, w.AssignedAt))
            .ToList();
    }
}
