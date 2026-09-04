using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Application.Services;

internal record CandidateActionItem(
    TestOrder Order,
    Sample Sample,
    TestWorkflowStep Step,
    string ActionType,
    string StepType,
    string StepName,
    string TestCode,
    string DisplayName,
    List<int> PermittedMaterialIds,
    string PermittedMaterialNames,
    decimal TempMin,
    decimal TempMax,
    int IncubationMinHours,
    int IncubationMaxHours,
    string? AssignedAnalystName
);

public class GroupedTestActionService
{
    private readonly MicroLimsDbContext _db;
    private readonly ITestWorkflowEngine _workflowEngine;
    private readonly IncubatorEligibilityService _incubatorEligibility;

    public GroupedTestActionService(
        MicroLimsDbContext db,
        ITestWorkflowEngine workflowEngine,
        IncubatorEligibilityService incubatorEligibility)
    {
        _db = db;
        _workflowEngine = workflowEngine;
        _incubatorEligibility = incubatorEligibility;
    }

    public async Task<ActionableGroupsResponse> GetActionableGroupsAsync(
        int currentUserId,
        RoleType currentRole,
        string? scope = "mine",
        string? actionType = null,
        List<int>? sampleIds = null,
        CancellationToken ct = default)
    {
        var query = _db.TestOrders
            .Include(t => t.Sample!).ThenInclude(s => s.Item)
            .Include(t => t.Sample!).ThenInclude(s => s.WaterSamplingPoint)
            .Include(t => t.Sample!).ThenInclude(s => s.Department)
            .Include(t => t.Sample!).ThenInclude(s => s.Machine)
            .Include(t => t.Incubations)
            .Where(t => !t.IsSuperseded && (t.Status == ApprovalStatus.Pending || t.Status == ApprovalStatus.InProgress))
            .AsQueryable();

        var isMineScope = string.Equals(scope, "mine", StringComparison.OrdinalIgnoreCase) ||
                          (currentRole == RoleType.Analyst && !string.Equals(scope, "all", StringComparison.OrdinalIgnoreCase));

        if (isMineScope)
        {
            query = query.Where(t => t.AssignedAnalystId == currentUserId);
        }

        if (sampleIds != null && sampleIds.Count > 0)
        {
            query = query.Where(t => sampleIds.Contains(t.SampleId));
        }

        var candidateOrders = await query.ToListAsync(ct);

        var analystIds = candidateOrders.Where(t => t.AssignedAnalystId.HasValue).Select(t => t.AssignedAnalystId!.Value).Distinct().ToList();
        var analystNames = await _db.Users
            .Where(u => analystIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName ?? u.Username, ct);

        var candidates = new List<CandidateActionItem>();
        var seenTsbSampleIds = new HashSet<int>();

        foreach (var order in candidateOrders)
        {
            var sample = order.Sample;
            if (sample == null) continue;

            if (sample.Category is SampleCategory.EnvironmentalMonitoring or SampleCategory.AfterCleaning)
            {
                if (sample.PreparationStatus != SamplePreparationStatus.Ready)
                    continue;
            }

            CurrentStepResult stepResult;
            try
            {
                stepResult = await _workflowEngine.GetCurrentStepAsync(order.Id);
            }
            catch
            {
                continue;
            }

            if (stepResult.AllStepsComplete || stepResult.Step == null)
                continue;

            var step = stepResult.Step;

            var isOpenIncubation = stepResult.OpenIncubation != null && stepResult.OpenIncubation.CompletedAt == null;
            if (isOpenIncubation)
                continue;

            var requiresMedia = step.StepType is StepType.PlateCount or StepType.BrothEnrichment or StepType.SelectiveBroth or StepType.SelectivePlating;
            if (!requiresMedia)
                continue;

            var isSharedTsb = step.StepType == StepType.BrothEnrichment || step.StepName.Contains("TSB", StringComparison.OrdinalIgnoreCase);
            if (isSharedTsb)
            {
                if (seenTsbSampleIds.Contains(order.SampleId))
                {
                    continue;
                }
                seenTsbSampleIds.Add(order.SampleId);
            }

            var stepMediaList = await _db.TestWorkflowStepMedias
                .Include(m => m.Material)
                .Where(m => m.TestWorkflowStepId == step.Id)
                .ToListAsync(ct);

            var permittedMaterialIds = stepMediaList.Select(m => m.MaterialId).Distinct().ToList();
            var permittedNames = string.Join(" / ", stepMediaList.Select(m => m.Material?.MaterialName).Where(n => !string.IsNullOrEmpty(n)).Distinct());

            var displayName = sample.Item?.Name ?? sample.WaterSamplingPoint?.Code ?? sample.Department?.Name ?? sample.Machine?.Name ?? sample.ReferenceNumber;
            var analystName = order.AssignedAnalystId.HasValue && analystNames.TryGetValue(order.AssignedAnalystId.Value, out var an) ? an : null;

            candidates.Add(new CandidateActionItem(
                Order: order,
                Sample: sample,
                Step: step,
                ActionType: "SETUP_INCUBATION",
                StepType: step.StepType.ToString(),
                StepName: step.StepName,
                TestCode: order.TestCode,
                DisplayName: displayName,
                PermittedMaterialIds: permittedMaterialIds,
                PermittedMaterialNames: permittedNames,
                TempMin: step.TemperatureMin,
                TempMax: step.TemperatureMax,
                IncubationMinHours: step.IncubationMinHours,
                IncubationMaxHours: step.IncubationMaxHours,
                AssignedAnalystName: analystName
            ));
        }

        if (!string.IsNullOrEmpty(actionType))
        {
            candidates = candidates.Where(c => string.Equals(c.ActionType, actionType, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        var rawGroups = candidates.GroupBy(c => (c.ActionType, c.StepType, c.StepName)).ToList();
        var finalGroups = new List<ActionableGroupDto>();

        foreach (var rg in rawGroups)
        {
            var subClusters = ClusterByCompatibility(rg.ToList());

            foreach (var cluster in subClusters)
            {
                var commonMaterials = cluster.Select(c => c.PermittedMaterialIds).Aggregate((a, b) => a.Intersect(b).ToList());
                if (!commonMaterials.Any() && cluster.First().PermittedMaterialIds.Any())
                    continue;

                var effTempMin = cluster.Max(c => c.TempMin);
                var effTempMax = cluster.Min(c => c.TempMax);
                if (effTempMin > effTempMax)
                    continue;

                var effMinHours = cluster.Max(c => c.IncubationMinHours);
                var effMaxHours = cluster.Max(c => c.IncubationMaxHours);

                var distinctSamples = cluster.Select(c => c.Sample.Id).Distinct().Count();
                var groupKey = $"{cluster[0].ActionType}|{cluster[0].StepType}|{cluster[0].StepName}|{effTempMin}-{effTempMax}|{effMinHours}h";

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

                var urgency = cluster.Any(c => c.Sample.ReceivedAt < DateTime.UtcNow.AddHours(-24)) ? "Overdue" : "DueNow";

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
                    TestOrders: testOrderDtos
                ));
            }
        }

        return new ActionableGroupsResponse(finalGroups.OrderByDescending(g => g.TestOrderCount).ToList());
    }

    private static List<List<CandidateActionItem>> ClusterByCompatibility(List<CandidateActionItem> items)
    {
        var clusters = new List<List<CandidateActionItem>>();

        foreach (var item in items)
        {
            bool placed = false;
            foreach (var cluster in clusters)
            {
                var commonMats = cluster.Select(c => c.PermittedMaterialIds).Aggregate((a, b) => a.Intersect(b).ToList());
                var canIntersectMats = !item.PermittedMaterialIds.Any() || commonMats.Intersect(item.PermittedMaterialIds).Any();
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

    public async Task<BatchSelectMediaResponse> ExecuteBatchSelectMediaAsync(
        BatchSelectMediaRequest request,
        int currentUserId,
        RoleType currentRole,
        CancellationToken ct = default)
    {
        if (request.TestOrderIds == null || request.TestOrderIds.Count == 0)
        {
            return new BatchSelectMediaResponse(0, 0, 0, new(), new());
        }

        var media = await _db.Media.Include(m => m.Material)
            .FirstOrDefaultAsync(m => m.Id == request.MediaLotId, ct);
        if (media == null)
        {
            throw new InvalidOperationException($"Media lot {request.MediaLotId} was not found.");
        }
        if (!media.IsReleasedForUse || media.Status == MediaStatus.OutOfStock || media.Status == MediaStatus.QuarantineFailed)
        {
            throw new InvalidOperationException($"Media lot \"{media.LotNumber}\" is not released for use, out of stock, or rejected.");
        }
        if (media.ExpiryDate <= DateTime.UtcNow)
        {
            throw new InvalidOperationException($"Media lot \"{media.LotNumber}\" is expired (expired on {media.ExpiryDate:yyyy-MM-dd}).");
        }

        var incubator = await _db.Equipment
            .FirstOrDefaultAsync(e => e.Id == request.IncubatorEquipmentId && e.Type == EquipmentType.Incubator, ct);
        if (incubator == null)
        {
            throw new InvalidOperationException($"Incubator equipment {request.IncubatorEquipmentId} was not found or is not an incubator.");
        }
        if (incubator.SetPointTemperature == null)
        {
            throw new InvalidOperationException($"Incubator \"{incubator.Code}\" has no calibrated set point temperature.");
        }
        if (incubator.CalibrationDueDate != null && incubator.CalibrationDueDate < DateTime.UtcNow)
        {
            throw new InvalidOperationException($"Incubator \"{incubator.Code}\" calibration expired on {incubator.CalibrationDueDate:yyyy-MM-dd}.");
        }

        var succeeded = new List<BatchActionSuccessItem>();
        var skipped = new List<BatchActionSkippedItem>();
        var processedTsbSamples = new HashSet<int>();

        var testOrderIds = request.TestOrderIds.Distinct().ToList();

        foreach (var id in testOrderIds)
        {
            var order = await _db.TestOrders.Include(t => t.Sample).FirstOrDefaultAsync(t => t.Id == id, ct);
            if (order == null)
            {
                skipped.Add(new BatchActionSkippedItem(id, "Unknown", $"Test order #{id} not found."));
                continue;
            }

            var sampleRef = order.Sample?.ReferenceNumber ?? $"Sample-{order.SampleId}";

            if (currentRole == RoleType.Analyst && order.AssignedAnalystId.HasValue && order.AssignedAnalystId.Value != currentUserId)
            {
                skipped.Add(new BatchActionSkippedItem(id, sampleRef, "Test order is assigned to another analyst."));
                continue;
            }

            if (order.Sample != null && (order.Sample.Category is SampleCategory.EnvironmentalMonitoring or SampleCategory.AfterCleaning))
            {
                if (order.Sample.PreparationStatus != SamplePreparationStatus.Ready)
                {
                    skipped.Add(new BatchActionSkippedItem(id, sampleRef, "Sample preparation is not complete (locations/swabs not verified)."));
                    continue;
                }
            }

            if (processedTsbSamples.Contains(order.SampleId))
            {
                var existingInc = await _db.Incubations
                    .Where(i => i.TestOrderId == id && (i.StepName == request.StepName || i.StepName == "Broth Enrichment"))
                    .OrderByDescending(i => i.StartedAt)
                    .FirstOrDefaultAsync(ct);

                if (existingInc != null)
                {
                    succeeded.Add(new BatchActionSuccessItem(
                        id,
                        sampleRef,
                        existingInc.Id,
                        existingInc.ExpectedReadingAt ?? DateTime.UtcNow,
                        "Started via shared TSB broth propagation."
                    ));
                    continue;
                }
            }

            try
            {
                var (loadedOrder, definition) = await LoadDefinitionAsync(id, ct);
                var step = definition.Steps.FirstOrDefault(s => s.StepName == request.StepName);
                if (step == null)
                {
                    skipped.Add(new BatchActionSkippedItem(id, sampleRef, $"Step \"{request.StepName}\" is not part of template for {order.TestCode}."));
                    continue;
                }

                Incubation inc;
                if (step.StepType == StepType.SelectivePlating)
                {
                    inc = await _workflowEngine.StartSelectivePlatingIncubationAsync(
                        id, request.StepName, request.MediaLotId, request.IncubatorEquipmentId, request.IncubationStartUtc, currentUserId);
                }
                else
                {
                    inc = await _workflowEngine.SelectMediaAsync(
                        id, request.StepName, request.MediaLotId, request.IncubatorEquipmentId, currentUserId);
                }

                succeeded.Add(new BatchActionSuccessItem(
                    id,
                    sampleRef,
                    inc.Id,
                    inc.ExpectedReadingAt ?? DateTime.UtcNow,
                    "Incubation started successfully."
                ));

                if (step.StepType == StepType.BrothEnrichment || step.StepName.Contains("TSB", StringComparison.OrdinalIgnoreCase))
                {
                    processedTsbSamples.Add(order.SampleId);
                }
            }
            catch (InvalidOperationException ex)
            {
                skipped.Add(new BatchActionSkippedItem(id, sampleRef, ex.Message));
            }
            catch (Exception ex)
            {
                skipped.Add(new BatchActionSkippedItem(id, sampleRef, $"Unexpected error: {ex.Message}"));
            }
        }

        return new BatchSelectMediaResponse(
            TotalRequested: testOrderIds.Count,
            SucceededCount: succeeded.Count,
            SkippedCount: skipped.Count,
            Succeeded: succeeded,
            Skipped: skipped
        );
    }

    private async Task<(TestOrder order, TestDefinition definition)> LoadDefinitionAsync(int testOrderId, CancellationToken ct)
    {
        var order = await _db.TestOrders.FirstOrDefaultAsync(t => t.Id == testOrderId, ct)
            ?? throw new InvalidOperationException($"Test order {testOrderId} not found.");
        var definition = await _db.TestDefinitions
            .Include(t => t.Steps)
            .FirstOrDefaultAsync(t => t.Code == order.TestCode, ct)
            ?? throw new InvalidOperationException($"Test definition \"{order.TestCode}\" not found.");
        return (order, definition);
    }
}
