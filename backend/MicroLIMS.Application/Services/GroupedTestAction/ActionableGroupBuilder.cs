using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.DTOs.Responses;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Services;

// What one candidate order contributes to the grouped-action list: an
// executable action, an order that needs individual result entry, or nothing.
public readonly record struct CandidateOutcome(CandidateActionItem? Candidate, ExcludedResultEntryTestOrderDto? Excluded)
{
    public static CandidateOutcome None => default;
    public static CandidateOutcome Of(CandidateActionItem candidate) => new(candidate, null);
    public static CandidateOutcome Of(ExcludedResultEntryTestOrderDto excluded) => new(null, excluded);
}

// Pure grouping of candidate actions into the groups the UI offers. No database access.
public static class ActionableGroupBuilder
{
    // The batches a step's media accept: every batch of the media products configured on the step.
    public static (List<int> Ids, string Names) PermittedMedia(
        IReadOnlyCollection<TestWorkflowStepMedia> stepMedias, Dictionary<int, List<int>> materialIdsByProduct)
    {
        var productIds = stepMedias
            .Select(StepMediumMatcher.ProductOf)
            .Where(p => p.HasValue)
            .Select(p => p!.Value)
            .Distinct()
            .ToList();

        var siblingMaterialIds = productIds.SelectMany(p => materialIdsByProduct.TryGetValue(p, out var ids) ? ids : new List<int>());
        var permittedMaterialIds = siblingMaterialIds.Distinct().ToList();

        var permittedNames = string.Join(" / ", stepMedias.Select(m => m.MediaProduct?.Name).Where(n => !string.IsNullOrEmpty(n)).Distinct());
        return (permittedMaterialIds, permittedNames);
    }

    public static ActionableGroupsResponse Build(
        List<CandidateActionItem> candidates, string? actionType, DateTime now, List<ExcludedResultEntryTestOrderDto> excludedResultEntry)
    {
        if (!string.IsNullOrEmpty(actionType))
        {
            candidates = candidates.Where(c => string.Equals(c.ActionType, actionType, StringComparison.OrdinalIgnoreCase) ||
                                               string.Equals(c.TransitionType, actionType, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        var rawGroups = candidates.GroupBy(c => (c.TransitionType, c.StepType, c.StepName)).ToList();
        var finalGroups = new List<ActionableGroupDto>();

        foreach (var rg in rawGroups)
        {
            var subClusters = ClusterByCompatibility(rg.ToList());

            foreach (var cluster in subClusters)
            {
                var isTransferIncubator = cluster[0].TransitionType == "TRANSFER_INCUBATOR";

                List<int> commonMaterials = new();
                if (!isTransferIncubator)
                {
                    commonMaterials = cluster.Select(c => c.PermittedMaterialIds).Aggregate((a, b) => a.Intersect(b).ToList());
                    if (!commonMaterials.Any() && cluster.First().PermittedMaterialIds.Any())
                        continue;
                }

                var effTempMin = cluster.Max(c => c.TempMin);
                var effTempMax = cluster.Min(c => c.TempMax);
                if (effTempMin > effTempMax)
                    continue;

                var effMinHours = cluster.Max(c => c.IncubationMinHours);
                var effMaxHours = cluster.Max(c => c.IncubationMaxHours);

                var distinctSamples = cluster.Select(c => c.Sample.Id).Distinct().Count();
                var groupKey = $"{cluster[0].TransitionType}|{cluster[0].StepType}|{cluster[0].StepName}|{effTempMin}-{effTempMax}|{effMinHours}h";

                var testOrderDtos = cluster.Select(c => new ActionableTestOrderSummaryDto(
                    c.Order.Id,
                    c.Sample.Id,
                    c.Sample.ReferenceNumber,
                    c.DisplayName,
                    c.Order.TestCode,
                    c.Sample.BatchNumber,
                    c.Order.AssignedAnalystId,
                    c.AssignedAnalystName
                )).ToList();

                var urgency = cluster.Any(c => c.Sample.ReceivedAt < now.AddHours(-24)) ? "Overdue" : "DueNow";

                finalGroups.Add(new ActionableGroupDto(
                    GroupKey: groupKey,
                    ActionType: cluster[0].ActionType,
                    StepType: cluster[0].StepType,
                    StepName: cluster[0].StepName,
                    TestCode: cluster[0].TestCode,
                    SampleCount: distinctSamples,
                    TestOrderCount: cluster.Count,
                    TempMin: effTempMin,
                    TempMax: effTempMax,
                    IncubationMinHours: effMinHours,
                    IncubationMaxHours: effMaxHours,
                    PermittedMaterialIds: commonMaterials,
                    PermittedMaterialNames: cluster[0].PermittedMaterialNames,
                    Urgency: urgency,
                    TestOrders: testOrderDtos,
                    TransitionType: cluster[0].TransitionType,
                    TransitionLabel: cluster[0].TransitionLabel,
                    TargetStepName: cluster[0].TargetStep.StepName,
                    PredecessorStepName: cluster[0].PredecessorStepName
                ));
            }
        }

        return new ActionableGroupsResponse(
            Groups: finalGroups.OrderByDescending(g => g.TestOrderCount).ToList(),
            ExcludedResultEntryCount: excludedResultEntry.Count,
            ExcludedResultEntryTestOrders: excludedResultEntry
        );
    }

    public static List<List<CandidateActionItem>> ClusterByCompatibility(List<CandidateActionItem> items)
    {
        var clusters = new List<List<CandidateActionItem>>();

        foreach (var item in items)
        {
            bool placed = false;
            foreach (var cluster in clusters)
            {
                bool isIncTransfer = item.TransitionType == "TRANSFER_INCUBATOR";
                bool canIntersectMats = isIncTransfer;
                if (!isIncTransfer)
                {
                    var commonMats = cluster.Select(c => c.PermittedMaterialIds).Aggregate((a, b) => a.Intersect(b).ToList());
                    canIntersectMats = !item.PermittedMaterialIds.Any() || commonMats.Intersect(item.PermittedMaterialIds).Any();
                }

                var effMin = Math.Max(cluster.Max(c => c.TempMin), item.TempMin);
                var effMax = Math.Min(cluster.Min(c => c.TempMax), item.TempMax);

                if (canIntersectMats && effMin <= effMax)
                {
                    cluster.Add(item);
                    placed = true;
                    break;
                }
            }

            if (!placed)
            {
                clusters.Add(new List<CandidateActionItem> { item });
            }
        }

        return clusters;
    }
}
