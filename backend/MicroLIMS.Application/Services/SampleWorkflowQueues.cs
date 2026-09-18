using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Application.Services;

/// <summary>
/// Centralized domain rules and query helpers for sample-level workflows and queues.
/// Serves as the single source of truth for reviewer/approver dashboards, workspace drill-downs, and SLA calculations.
/// </summary>
public static class SampleWorkflowQueues
{
    /// <summary>
    /// Decision D2: Closed sample statuses.
    /// A sample past one of these is finished: it is not outstanding work.
    /// Used across workspace filters, KPI tiles, and dashboard counts so definitions cannot drift.
    /// </summary>
    public static readonly SampleStatus[] ClosedSampleStatuses =
    {
        SampleStatus.Approved,
        SampleStatus.Rejected,
        SampleStatus.RetestRequested,
        SampleStatus.Cancelled,
        SampleStatus.Voided
    };

    // Workspace queues. The register filters and the workload tile counts
    // both use these, so a tile and the list it opens cannot disagree.

    public static readonly Expression<Func<Sample, bool>> NeedsPreparation = s =>
        s.PreparationStatus == SamplePreparationStatus.NeedsPreparation && !ClosedSampleStatuses.Contains(s.Status);

    public static Expression<Func<Sample, bool>> HasTestInSections(IReadOnlyCollection<int> sectionIds) =>
        s => s.TestOrders.Any(t => !t.IsSuperseded && sectionIds.Contains(t.SectionId));

    /// <summary>
    /// A sample with an active (non-superseded, Pending/InProgress) test holding an open incubation that
    /// can be read now. <see cref="IsIncubationReadyToRead(Incubation, DateTime)"/> is the same rule for one incubation.
    /// </summary>
    public static Expression<Func<Sample, bool>> HasTestReadyToRead(DateTime now, IReadOnlyCollection<int>? sectionIds = null)
    {
        if (sectionIds == null)
        {
            return s => s.TestOrders.Any(t => !t.IsSuperseded
                && (t.Status == ApprovalStatus.Pending || t.Status == ApprovalStatus.InProgress)
                && t.Incubations.Any(i => i.CompletedAt == null
                    && ((i.ExpectedReadingAt != null && i.ExpectedReadingAt <= now)
                        || (i.IncubationEndUtc != null && i.IncubationEndUtc <= now)
                        || i.MinimumDurationOverriddenByUserId != null)));
        }

        return s => s.TestOrders.Any(t => !t.IsSuperseded
            && sectionIds.Contains(t.SectionId)
            && (t.Status == ApprovalStatus.Pending || t.Status == ApprovalStatus.InProgress)
            && t.Incubations.Any(i => i.CompletedAt == null
                && ((i.ExpectedReadingAt != null && i.ExpectedReadingAt <= now)
                    || (i.IncubationEndUtc != null && i.IncubationEndUtc <= now)
                    || i.MinimumDurationOverriddenByUserId != null)));
    }

    public static Expression<Func<Sample, bool>> IsOverdue(DateTime now)
    {
        var overdueCutoff = now.AddHours(-24);
        return s => !ClosedSampleStatuses.Contains(s.Status) && s.ReceivedAt < overdueCutoff;
    }

    public static Expression<Func<Sample, bool>> HasTestAssignedTo(int userId, IReadOnlyCollection<int>? sectionIds = null)
    {
        if (sectionIds == null)
        {
            return s => s.TestOrders.Any(t => !t.IsSuperseded && t.AssignedAnalystId == userId);
        }

        return s => s.TestOrders.Any(t => !t.IsSuperseded && sectionIds.Contains(t.SectionId) && t.AssignedAnalystId == userId);
    }

    public static readonly Expression<Func<Sample, bool>> IsUnassigned = s =>
        !ClosedSampleStatuses.Contains(s.Status) && !s.TestOrders.Any(t => !t.IsSuperseded && t.AssignedAnalystId != null);

    public static Expression<Func<Sample, bool>> IsUnassignedIn(IReadOnlyCollection<int>? sectionIds)
    {
        if (sectionIds == null)
        {
            return IsUnassigned;
        }

        return s => !ClosedSampleStatuses.Contains(s.Status)
            && !s.TestOrders.Any(t => !t.IsSuperseded && sectionIds.Contains(t.SectionId) && t.AssignedAnalystId != null);
    }

    // Incubation-level bench counts (dashboards). An open incubation counts
    // only while its test is still active work, and is ready to read by the
    // same rule HasTestReadyToRead applies per sample.

    public static readonly Expression<Func<Incubation, bool>> IsOpenIncubationOnActiveTest = i =>
        i.CompletedAt == null && i.TestOrderId != null
        && !i.TestOrder!.IsSuperseded
        && (i.TestOrder.Status == ApprovalStatus.Pending || i.TestOrder.Status == ApprovalStatus.InProgress);

    public static bool IsIncubationReadyToRead(DateTime? expectedReadingAt, DateTime? incubationEndUtc, int? minimumDurationOverriddenByUserId, DateTime now) =>
        (expectedReadingAt != null && expectedReadingAt <= now)
        || (incubationEndUtc != null && incubationEndUtc <= now)
        || minimumDurationOverriddenByUserId != null;

    public static bool IsIncubationReadyToRead(Incubation incubation, DateTime now) =>
        IsIncubationReadyToRead(incubation.ExpectedReadingAt, incubation.IncubationEndUtc, incubation.MinimumDurationOverriddenByUserId, now);

    /// <summary>
    /// Decision D2: "Retests in progress" = samples with OriginSampleId != null whose Status is not closed.
    /// Composable into an IQueryable&lt;Sample&gt;.
    /// </summary>
    public static IQueryable<Sample> WhereRetestInProgress(this IQueryable<Sample> query)
    {
        return query.Where(s => s.OriginSampleId != null && !ClosedSampleStatuses.Contains(s.Status));
    }

    /// <summary>
    /// Decision D2: Checks whether an in-memory sample is a retest in progress.
    /// </summary>
    public static bool IsRetestInProgress(Sample s)
    {
        return s.OriginSampleId != null && !ClosedSampleStatuses.Contains(s.Status);
    }

    /// <summary>
    /// Decision D4: Explicit severity ranking for ResultLevel.
    /// The enum's numeric values do NOT represent severity.
    /// OutOfSpecification (4) > ActionLevel (3) > AlertLevel (2) > WithinLimit (1).
    /// NotApplicable and LimitsNotConfigured return null (never raise severity).
    /// </summary>
    public static int? GetResultLevelSeverity(ResultLevel level) => level switch
    {
        ResultLevel.OutOfSpecification => 4,
        ResultLevel.ActionLevel => 3,
        ResultLevel.AlertLevel => 2,
        ResultLevel.WithinLimit => 1,
        ResultLevel.NotApplicable => null,
        ResultLevel.LimitsNotConfigured => null,
        _ => null
    };

    /// <summary>
    /// Decision D4: Selects the worst (highest severity) ResultLevel among a collection of levels.
    /// Returns null if the collection contains no ranked levels (e.g. empty or only NotApplicable/LimitsNotConfigured).
    /// </summary>
    public static ResultLevel? GetWorstResultLevel(IEnumerable<ResultLevel> levels)
    {
        ResultLevel? worst = null;
        int maxRank = 0;

        foreach (var level in levels)
        {
            var rank = GetResultLevelSeverity(level);
            if (rank.HasValue && rank.Value > maxRank)
            {
                maxRank = rank.Value;
                worst = level;
            }
        }

        return worst;
    }

    /// <summary>
    /// Decision D5: Approval age clock = Sample.ReviewedAt (set when review completes), fallback Sample.ReceivedAt.
    /// </summary>
    public static DateTime GetApprovalClockStart(Sample s)
    {
        return s.ReviewedAt ?? s.ReceivedAt;
    }

    /// <summary>
    /// Decision D3: Review age clock = when the sample entered UnderReview.
    /// Primary source: latest ReviewWorkflowEvent with EntityType == ReviewEntityTypes.Sample, EntityId == sample.Id,
    /// EventType == ReviewWorkflowEventType.SubmittedForReview (using its Timestamp).
    /// Fallback 1: latest WorkflowHistory.Timestamp with ToStep == WorkflowStep.Ready among the sample's non-superseded TestOrders.
    /// Final fallback: Sample.ReceivedAt.
    /// Uses translation-safe separated queries combined in memory. Never fabricates events.
    /// </summary>
    public static async Task<Dictionary<int, DateTime>> GetReviewClockStartsAsync(
        MicroLimsDbContext db,
        IReadOnlyCollection<int> sampleIds)
    {
        if (sampleIds == null || sampleIds.Count == 0)
        {
            return new Dictionary<int, DateTime>();
        }

        var distinctSampleIds = sampleIds.Distinct().ToList();

        // 1. Primary: Latest SubmittedForReview ReviewWorkflowEvent per sample
        var eventStarts = await db.ReviewWorkflowEvents
            .AsNoTracking()
            .Where(e => e.EntityType == ReviewEntityTypes.Sample
                     && e.EventType == ReviewWorkflowEventType.SubmittedForReview
                     && distinctSampleIds.Contains(e.EntityId))
            .GroupBy(e => e.EntityId)
            .Select(g => new { SampleId = g.Key, MaxTimestamp = g.Max(e => e.Timestamp) })
            .ToDictionaryAsync(x => x.SampleId, x => x.MaxTimestamp);

        // Identify samples needing history fallback
        var missingAfterEvents = distinctSampleIds.Where(id => !eventStarts.ContainsKey(id)).ToList();

        Dictionary<int, DateTime> historyStarts = new();
        if (missingAfterEvents.Count > 0)
        {
            // 2. Fallback 1: Latest Ready WorkflowHistory of non-superseded test orders
            var orderData = await db.TestOrders
                .AsNoTracking()
                .Where(t => missingAfterEvents.Contains(t.SampleId) && !t.IsSuperseded)
                .Select(t => new { t.Id, t.SampleId })
                .ToListAsync();

            if (orderData.Count > 0)
            {
                var orderIds = orderData.Select(o => o.Id).ToList();
                var orderToSample = orderData.ToDictionary(o => o.Id, o => o.SampleId);

                var historyData = await db.WorkflowHistories
                    .AsNoTracking()
                    .Where(h => orderIds.Contains(h.TestOrderId) && h.ToStep == WorkflowStep.Ready)
                    .GroupBy(h => h.TestOrderId)
                    .Select(g => new { TestOrderId = g.Key, MaxTimestamp = g.Max(h => h.Timestamp) })
                    .ToListAsync();

                historyStarts = historyData
                    .GroupBy(h => orderToSample[h.TestOrderId])
                    .ToDictionary(g => g.Key, g => g.Max(x => x.MaxTimestamp));
            }
        }

        // Identify samples needing ReceivedAt fallback
        var missingAfterHistory = distinctSampleIds
            .Where(id => !eventStarts.ContainsKey(id) && !historyStarts.ContainsKey(id))
            .ToList();

        Dictionary<int, DateTime> receivedStarts = new();
        if (missingAfterHistory.Count > 0)
        {
            // 3. Final fallback: Sample.ReceivedAt
            receivedStarts = await db.Samples
                .AsNoTracking()
                .Where(s => missingAfterHistory.Contains(s.Id))
                .Select(s => new { s.Id, s.ReceivedAt })
                .ToDictionaryAsync(x => x.Id, x => x.ReceivedAt);
        }

        // Combine results in memory
        var result = new Dictionary<int, DateTime>(distinctSampleIds.Count);
        foreach (var id in distinctSampleIds)
        {
            if (eventStarts.TryGetValue(id, out var dt))
            {
                result[id] = dt;
            }
            else if (historyStarts.TryGetValue(id, out dt))
            {
                result[id] = dt;
            }
            else if (receivedStarts.TryGetValue(id, out dt))
            {
                result[id] = dt;
            }
        }

        return result;
    }

    /// <summary>
    /// Decision D4: Batched worst-of ResultLevel lookup for samples.
    /// Source: ResultRecord.ResultLevel on non-superseded TestOrders of those samples.
    /// Superseded test orders are ignored.
    /// </summary>
    public static async Task<Dictionary<int, ResultLevel?>> GetWorstResultLevelsAsync(
        MicroLimsDbContext db,
        IReadOnlyCollection<int> sampleIds)
    {
        if (sampleIds == null || sampleIds.Count == 0)
        {
            return new Dictionary<int, ResultLevel?>();
        }

        var distinctSampleIds = sampleIds.Distinct().ToList();

        var orders = await db.TestOrders
            .AsNoTracking()
            .Where(t => distinctSampleIds.Contains(t.SampleId) && !t.IsSuperseded)
            .Select(t => new { t.Id, t.SampleId })
            .ToListAsync();

        var result = distinctSampleIds.ToDictionary(id => id, _ => (ResultLevel?)null);
        if (orders.Count == 0)
        {
            return result;
        }

        var orderIds = orders.Select(o => o.Id).ToList();
        var orderToSample = orders.ToDictionary(o => o.Id, o => o.SampleId);

        var records = await LoadCurrentResultLevelsAsync(db, orderIds);

        foreach (var group in records.GroupBy(r => orderToSample[r.TestOrderId]))
        {
            result[group.Key] = GetWorstResultLevel(group.Select(r => r.ResultLevel));
        }

        return result;
    }

    // ResultRecord rows are never deleted: when a count test is returned to
    // the analyst its CountTestReadings are soft-superseded (IsActive = false)
    // and the re-read adds new readings, but the old readings' projection rows
    // stay. Only records from readings still in force may decide severity -
    // otherwise a result the reviewer already sent back would keep flagging
    // the sample as out of specification.
    private static async Task<List<(int TestOrderId, ResultLevel ResultLevel)>> LoadCurrentResultLevelsAsync(
        MicroLimsDbContext db, List<int> testOrderIds)
    {
        var records = await db.ResultRecords
            .AsNoTracking()
            .Where(r => testOrderIds.Contains(r.TestOrderId))
            .Select(r => new { r.TestOrderId, r.SourceTable, r.SourceId, r.ResultLevel })
            .ToListAsync();

        var inactiveReadingIds = (await db.CountTestReadings
            .AsNoTracking()
            .Where(c => testOrderIds.Contains(c.TestOrderId) && !c.IsActive)
            .Select(c => c.Id)
            .ToListAsync())
            .ToHashSet();

        return records
            .Where(r => !(r.SourceTable == "CountTestReading" && inactiveReadingIds.Contains(r.SourceId)))
            .Select(r => (r.TestOrderId, r.ResultLevel))
            .ToList();
    }

    /// <summary>
    /// Decision D4: Batched worst-of ResultLevel lookup for test orders.
    /// Source: ResultRecord.ResultLevel for the specified TestOrderIds.
    /// </summary>
    public static async Task<Dictionary<int, ResultLevel?>> GetTestOrderWorstResultLevelsAsync(
        MicroLimsDbContext db,
        IReadOnlyCollection<int> testOrderIds)
    {
        if (testOrderIds == null || testOrderIds.Count == 0)
        {
            return new Dictionary<int, ResultLevel?>();
        }

        var distinctOrderIds = testOrderIds.Distinct().ToList();

        var records = await LoadCurrentResultLevelsAsync(db, distinctOrderIds);

        var result = distinctOrderIds.ToDictionary(id => id, _ => (ResultLevel?)null);
        foreach (var group in records.GroupBy(r => r.TestOrderId))
        {
            result[group.Key] = GetWorstResultLevel(group.Select(r => r.ResultLevel));
        }

        return result;
    }

    /// <summary>
    /// Decision D3 &amp; SLA: Returns IDs of UnderReview samples whose review clock start &lt;= now - threshold.
    /// Standard SLA threshold is 24 hours.
    /// </summary>
    public static async Task<List<int>> GetOverdueReviewSampleIdsAsync(
        MicroLimsDbContext db,
        DateTime now,
        TimeSpan threshold)
    {
        var underReviewSampleIds = await db.Samples
            .AsNoTracking()
            .Where(s => s.Status == SampleStatus.UnderReview)
            .Select(s => s.Id)
            .ToListAsync();

        if (underReviewSampleIds.Count == 0)
        {
            return new List<int>();
        }

        var clockStarts = await GetReviewClockStartsAsync(db, underReviewSampleIds);
        var cutoff = now - threshold;

        return underReviewSampleIds
            .Where(id => clockStarts.TryGetValue(id, out var start) && start <= cutoff)
            .ToList();
    }

    /// <summary>
    /// Decision D5 &amp; SLA: Returns IDs of UnderApproval samples whose approval clock start &lt;= now - threshold.
    /// Standard SLA threshold is 24 hours.
    /// </summary>
    public static async Task<List<int>> GetOverdueApprovalSampleIdsAsync(
        MicroLimsDbContext db,
        DateTime now,
        TimeSpan threshold)
    {
        var underApprovalSamples = await db.Samples
            .AsNoTracking()
            .Where(s => s.Status == SampleStatus.UnderApproval)
            .Select(s => new { s.Id, s.ReviewedAt, s.ReceivedAt })
            .ToListAsync();

        if (underApprovalSamples.Count == 0)
        {
            return new List<int>();
        }

        var cutoff = now - threshold;

        return underApprovalSamples
            .Where(s => (s.ReviewedAt ?? s.ReceivedAt) <= cutoff)
            .Select(s => s.Id)
            .ToList();
    }
}
