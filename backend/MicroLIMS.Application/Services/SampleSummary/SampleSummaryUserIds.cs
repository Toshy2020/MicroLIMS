using MicroLIMS.Application.DTOs;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Services;

public static class SampleSummaryUserIds
{
    // Every user referenced anywhere in the summary, for one batched name lookup.
    public static HashSet<int> Collect(SampleSummaryData d, SamplePreparation? preparation)
    {
        var sample = d.Sample;
        var results = d.Results;
        var countTestReadings = d.CountTestReadings;
        var activeAnalyses = d.ActiveAnalyses;
        var pathogenObservations = d.PathogenObservations;
        var locationPathogenObservations = d.LocationPathogenObservations;
        var workflowHistory = d.WorkflowHistory;
        var sampleLocations = d.SampleLocations;
        var incubations = d.Incubations;
        var effectiveTestOrders = d.EffectiveTestOrders;

        var userIds = new HashSet<int>(results.Select(r => r.EnteredByUserId)
            .Concat(countTestReadings.Select(r => r.EnteredByUserId))
            .Concat(activeAnalyses.Select(e => e.EnteredByUserId))
            .Concat(pathogenObservations.Select(p => p.ObservedByUserId))
            .Concat(locationPathogenObservations.Select(o => o.ObservedByUserId))
            .Concat(workflowHistory.Select(w => w.PerformedByUserId))
            .Concat(sampleLocations.Where(l => l.EnteredByUserId is not null).Select(l => l.EnteredByUserId!.Value))
            .Concat(incubations.Where(i => i.StartedByUserId is not null).Select(i => i.StartedByUserId!.Value))
            .Concat(effectiveTestOrders.Where(o => o.AssignedAnalystId.HasValue).Select(o => o.AssignedAnalystId!.Value))
            .Append(sample.ReceivedByUserId));
        if (preparation is not null) userIds.Add(preparation.PreparedByUserId);
        if (sample.ReviewedByUserId is not null) userIds.Add(sample.ReviewedByUserId.Value);
        if (sample.ApprovedByUserId is not null) userIds.Add(sample.ApprovedByUserId.Value);
        foreach (var signoff in sample.SectionSignoffs)
        {
            if (signoff.ReviewedByUserId is int reviewerId) userIds.Add(reviewerId);
            if (signoff.ApprovedByUserId is int approverId) userIds.Add(approverId);
            if (signoff.ClosedByUserId is int closerId) userIds.Add(closerId);
        }

        return userIds;
    }
}
