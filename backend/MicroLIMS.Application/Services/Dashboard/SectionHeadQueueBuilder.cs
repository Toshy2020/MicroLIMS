using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Services;

// The slice of a ResultRecord the Section Head review queue needs to name the analyst.
public sealed record ReviewResultRecordRow(int TestOrderId, int ResultEnteredByUserId, string ResultEnteredByName, DateTime ResultEnteredAt);

public static class SampleDisplayName
{
    public static string For(Sample? s) =>
        s?.Item?.Name
        ?? s?.WaterSamplingPoint?.Code
        ?? s?.Department?.Name
        ?? s?.Machine?.Name
        ?? (string.IsNullOrWhiteSpace(s?.ReferenceNumber) ? "Sample" : s.ReferenceNumber);
}

// Pure builders for the Section Head dashboard: loaded data in, DTOs out, no database access.
public static class SectionHeadQueueBuilder
{
    // Worst current result among one queue entry's tests.
    public static string? WorstLevel(SectionQueueEntry entry, Dictionary<int, ResultLevel?> orderLevels) =>
        SampleWorkflowQueues.GetWorstResultLevel(entry.Orders
            .Select(o => orderLevels.GetValueOrDefault(o.Id))
            .Where(l => l.HasValue)
            .Select(l => l!.Value))?.ToString();

    // Review Queue details: one row per (sample, section) under review
    public static List<SectionHeadReviewQueueItemDto> BuildReviewQueue(
        IEnumerable<SectionQueueEntry> reviewEntries,
        DateTime now,
        Dictionary<int, DateTime> reviewClockStarts,
        Dictionary<int, ResultLevel?> reviewOrderLevels,
        IReadOnlyCollection<ReviewResultRecordRow> reviewResultRecords,
        Dictionary<int, string> userMap,
        Dictionary<int, string> sectionNames) =>
        reviewEntries
            .Select(entry =>
            {
                var s = entry.Sample;
                var submittedAt = entry.Signoff?.SubmittedForReviewAt ?? reviewClockStarts.GetValueOrDefault(s.Id, s.ReceivedAt);
                var ageHours = Math.Round((now - submittedAt).TotalHours, 1);
                var worstLevel = WorstLevel(entry, reviewOrderLevels);

                var nonSuperseded = entry.Orders;
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

                var displayName = SampleDisplayName.For(s);

                return new SectionHeadReviewQueueItemDto(
                    s.Id,
                    s.ReferenceNumber ?? "",
                    displayName,
                    s.Category.ToString(),
                    testCodes,
                    analystNames,
                    submittedAt,
                    ageHours,
                    worstLevel,
                    entry.SectionId,
                    sectionNames.GetValueOrDefault(entry.SectionId)
                );
            })
            .OrderByDescending(r => r.AgeHours)
            .ToList();

    // Approval Queue details: one row per (sample, section) under approval
    public static List<SectionHeadApprovalQueueItemDto> BuildApprovalQueue(
        IEnumerable<SectionQueueEntry> approvalEntries,
        DateTime now,
        Dictionary<int, ResultLevel?> approvalOrderLevels,
        Dictionary<int, string> userMap,
        Dictionary<int, string> sectionNames) =>
        approvalEntries
            .Select(entry =>
            {
                var s = entry.Sample;
                var reviewedAt = entry.Signoff?.ReviewedAt ?? SampleWorkflowQueues.GetApprovalClockStart(s);
                var ageHours = Math.Round((now - reviewedAt).TotalHours, 1);
                var reviewerId = entry.Signoff?.ReviewedByUserId ?? s.ReviewedByUserId;
                var reviewerName = reviewerId.HasValue && userMap.TryGetValue(reviewerId.Value, out var rName) ? rName : null;
                var displayName = SampleDisplayName.For(s);

                var testCodes = entry.Orders.Select(t => t.TestCode).ToList();
                var worstLevel = WorstLevel(entry, approvalOrderLevels);

                return new SectionHeadApprovalQueueItemDto(
                    s.Id,
                    s.ReferenceNumber ?? "",
                    displayName,
                    s.Category.ToString(),
                    testCodes,
                    reviewerName,
                    reviewedAt,
                    ageHours,
                    worstLevel,
                    entry.SectionId,
                    sectionNames.GetValueOrDefault(entry.SectionId)
                );
            })
            .OrderByDescending(a => a.AgeHours)
            .ToList();

    // 1. Overdue tests - 7-day Analyst-stage SLA samples, one item per sample (not per first order); TestCodes = that sample's non-superseded Pending/InProgress test codes; reason text unchanged.
    public static List<SectionHeadAttentionItemDto> BuildOverdueAttentionItems(
        IEnumerable<TestOrder> liveOverdueTestOrders,
        Dictionary<int, DateTime> overdueAssignedAtBySampleId,
        DateTime now)
    {
        var items = new List<SectionHeadAttentionItemDto>();
        var overdueSampleGroups = liveOverdueTestOrders
            .GroupBy(t => t.SampleId)
            .OrderBy(g => overdueAssignedAtBySampleId[g.Key])
            .Take(5)
            .ToList();

        foreach (var g in overdueSampleGroups)
        {
            var sample = g.First().Sample;
            var name = SampleDisplayName.For(sample);
            var assignedAt = overdueAssignedAtBySampleId[g.Key];
            var delayHours = (int)Math.Floor((now - assignedAt).TotalHours);
            var testCodes = g.Select(t => t.TestCode).Distinct().ToList();

            items.Add(new SectionHeadAttentionItemDto(
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
        return items;
    }

    // 2. Retests requested: retests in progress (D2), top 5 by ReceivedAt ascending;
    // Reason = $"Retest sample in progress (origin {originReferenceNumber})" (fall back to "Retest sample in progress" if the origin can't be loaded);
    // Urgency "High"; Timestamp = the retest sample's ReceivedAt; TestCodes = its non-superseded test codes.
    public static List<SectionHeadAttentionItemDto> BuildRetestAttentionItems(
        IEnumerable<Sample> retestSamples,
        Dictionary<int, string> originRefs)
    {
        var items = new List<SectionHeadAttentionItemDto>();
        foreach (var rs in retestSamples)
        {
            var name = SampleDisplayName.For(rs);

            string reason = "Retest sample in progress";
            if (rs.OriginSampleId.HasValue && originRefs.TryGetValue(rs.OriginSampleId.Value, out var originRef) && !string.IsNullOrWhiteSpace(originRef))
            {
                reason = $"Retest sample in progress (origin {originRef})";
            }

            var testCodes = rs.TestOrders.Where(t => !t.IsSuperseded).Select(t => t.TestCode).ToList();

            items.Add(new SectionHeadAttentionItemDto(
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
        return items;
    }

    // 3. Delayed reviews (>24h in UnderReview)
    public static List<SectionHeadAttentionItemDto> BuildDelayedReviewAttentionItems(IEnumerable<SectionHeadReviewQueueItemDto> reviewQueueItems) =>
        reviewQueueItems.Where(r => r.AgeHours >= 24).Take(5)
            .Select(ro => new SectionHeadAttentionItemDto(
                ro.SampleId,
                ro.ReferenceNumber,
                ro.SubjectName,
                ro.TestCodes,
                "Medium",
                $"Scientific review delayed by {ro.AgeHours}h",
                "DelayedReview",
                ro.SubmittedForReviewAt
            )).ToList();

    // 4. Delayed approvals (>24h in UnderApproval)
    public static List<SectionHeadAttentionItemDto> BuildDelayedApprovalAttentionItems(IEnumerable<SectionHeadApprovalQueueItemDto> approvalQueueItems) =>
        approvalQueueItems.Where(a => a.AgeHours >= 24).Take(5)
            .Select(ao => new SectionHeadAttentionItemDto(
                ao.SampleId,
                ao.ReferenceNumber,
                ao.SubjectName,
                ao.TestCodes,
                "Medium",
                $"Final approval delayed by {ao.AgeHours}h",
                "DelayedApproval",
                ao.ReviewedAt
            )).ToList();
}
