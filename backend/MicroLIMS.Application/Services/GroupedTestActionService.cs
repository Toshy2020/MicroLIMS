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
    TestWorkflowStep CurrentStep,
    TestWorkflowStep TargetStep,
    string ActionType,        // "SETUP_INCUBATION", "TRANSFER_SELECTIVE", "TRANSFER_INCUBATOR"
    string TransitionType,    // "SETUP_INCUBATION", "TRANSFER_SELECTIVE", "TRANSFER_INCUBATOR"
    string TransitionLabel,
    string StepType,
    string StepName,
    string? PredecessorStepName,
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
        var excludedResultEntry = new List<ExcludedResultEntryTestOrderDto>();
        var seenTsbSampleIds = new HashSet<int>();

        foreach (var order in candidateOrders)
        {
            var sample = order.Sample;
            if (sample == null) continue;

            // Test Preparation gate - every category, not just the
            // location-based ones. PreparationStatus turns Ready only once
            // the sample's preparation step has been signed off, so an
            // unprepared sample offers no incubation action at all.
            if (sample.PreparationStatus != SamplePreparationStatus.Ready)
                continue;
            if (sample.ItemId != null && !await _db.SamplePreparations.AnyAsync(p => p.SampleId == sample.Id, ct))
                continue;

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

            var (loadedOrder, definition) = await LoadDefinitionAsync(order.Id, ct);
            var step = stepResult.Step;
            var displayName = sample.Item?.Name ?? sample.WaterSamplingPoint?.Code ?? sample.Department?.Name ?? sample.Machine?.Name ?? sample.ReferenceNumber;
            var analystName = order.AssignedAnalystId.HasValue && analystNames.TryGetValue(order.AssignedAnalystId.Value, out var an) ? an : null;

            var openInc = stepResult.OpenIncubation;
            if (openInc != null && openInc.CompletedAt == null)
            {
                // An incubation is currently open for this step.
                // Check if its minimum incubation duration has elapsed:
                var startUtc = openInc.IncubationStartUtc ?? openInc.StartedAt;
                int minHours = 0;
                if (openInc.StageNumber == 2)
                {
                    var stage2 = step.IncubationStages?.FirstOrDefault(s => s.StageNumber == 2);
                    minHours = stage2?.IncubationMinHours ?? step.IncubationMinHours;
                }
                else
                {
                    if (openInc.MediaId.HasValue && step.StepMedia != null && step.StepMedia.Count > 0)
                    {
                        var mediaRow = await _db.Media.Where(m => m.Id == openInc.MediaId.Value).Select(m => new { m.MaterialId }).FirstOrDefaultAsync(ct);
                        var stepMedia = mediaRow != null ? step.StepMedia.FirstOrDefault(sm => sm.MaterialId == mediaRow.MaterialId) : null;
                        minHours = stepMedia?.IncubationMinHours ?? step.IncubationMinHours;
                    }
                    else
                    {
                        minHours = step.IncubationMinHours;
                    }
                }

                var minReadyAt = startUtc != default ? startUtc.AddHours(minHours) : (DateTime?)null;
                bool isReady = openInc.MinimumDurationOverriddenByUserId.HasValue || (minReadyAt.HasValue && DateTime.UtcNow >= minReadyAt.Value);

                if (!isReady)
                {
                    // Actively incubating - not yet ready for any action
                    continue;
                }

                // Minimum duration has elapsed! Determine the pending executable action:
                if (step.StepType is StepType.BrothEnrichment or StepType.SelectiveBroth)
                {
                    // Broth enrichment completed. The operational transition is:
                    // complete broth and transfer to next selective broth or plating!
                    var nextStep = definition.Steps.Where(s => s.StepOrder > step.StepOrder).OrderBy(s => s.StepOrder).FirstOrDefault();
                    if (nextStep != null && nextStep.StepType is StepType.SelectiveBroth or StepType.SelectivePlating)
                    {
                        var nextStepMedias = await _db.TestWorkflowStepMedias
                            .Include(m => m.Material)
                            .Where(m => m.TestWorkflowStepId == nextStep.Id)
                            .ToListAsync(ct);

                        var permittedMaterialIds = nextStepMedias.Select(m => m.MaterialId).Distinct().ToList();
                        var permittedNames = string.Join(" / ", nextStepMedias.Select(m => m.Material?.MaterialName).Where(n => !string.IsNullOrEmpty(n)).Distinct());

                        var transitionLabel = $"{step.StepName} → {nextStep.StepName}";

                        candidates.Add(new CandidateActionItem(
                            Order: order,
                            Sample: sample,
                            CurrentStep: step,
                            TargetStep: nextStep,
                            ActionType: "TRANSFER_SELECTIVE",
                            TransitionType: "TRANSFER_SELECTIVE",
                            TransitionLabel: transitionLabel,
                            StepType: nextStep.StepType.ToString(),
                            StepName: nextStep.StepName,
                            PredecessorStepName: step.StepName,
                            TestCode: order.TestCode,
                            DisplayName: displayName,
                            PermittedMaterialIds: permittedMaterialIds,
                            PermittedMaterialNames: permittedNames,
                            TempMin: nextStep.TemperatureMin,
                            TempMax: nextStep.TemperatureMax,
                            IncubationMinHours: nextStep.IncubationMinHours,
                            IncubationMaxHours: nextStep.IncubationMaxHours,
                            AssignedAnalystName: analystName
                        ));
                    }
                    continue;
                }
                else if (step.StepType == StepType.PlateCount && step.RequiresIncubationTransfer && openInc.StageNumber == 1)
                {
                    // Stage 1 completed for a two-stage plate count.
                    // Operational transition: transfer to Stage 2 incubator!
                    var stage2Config = await _db.TestWorkflowStepIncubationStages
                        .FirstOrDefaultAsync(s => s.TestWorkflowStepId == step.Id && s.StageNumber == 2, ct);

                    var tempMin = stage2Config?.TempMin ?? step.TemperatureMin;
                    var tempMax = stage2Config?.TempMax ?? step.TemperatureMax;
                    var minH = stage2Config?.IncubationMinHours ?? step.IncubationMinHours;
                    var maxH = stage2Config?.IncubationMaxHours ?? step.IncubationMaxHours;

                    candidates.Add(new CandidateActionItem(
                        Order: order,
                        Sample: sample,
                        CurrentStep: step,
                        TargetStep: step,
                        ActionType: "TRANSFER_INCUBATOR",
                        TransitionType: "TRANSFER_INCUBATOR",
                        TransitionLabel: $"Transfer to Stage 2 Incubator ({step.StepName})",
                        StepType: step.StepType.ToString(),
                        StepName: step.StepName,
                        PredecessorStepName: step.StepName,
                        TestCode: order.TestCode,
                        DisplayName: displayName,
                        PermittedMaterialIds: new List<int>(), // Media remains the same; plate transfers to incubator
                        PermittedMaterialNames: "Existing Plate Media",
                        TempMin: tempMin,
                        TempMax: tempMax,
                        IncubationMinHours: minH,
                        IncubationMaxHours: maxH,
                        AssignedAnalystName: analystName
                    ));
                    continue;
                }
                else if (step.StepType == StepType.PlateCount)
                {
                    // PlateCount incubation complete -> colony count entry
                    // NON-GROUPABLE ANALYTICAL RESULT ENTRY
                    excludedResultEntry.Add(new ExcludedResultEntryTestOrderDto(
                        order.Id, sample.Id, sample.ReferenceNumber, displayName, order.TestCode, step.StepName,
                        "Colony count requires individual analytical result entry"
                    ));
                    continue;
                }
                else if (step.StepType == StepType.SelectivePlating)
                {
                    // Selective plating incubation complete -> observation reading
                    // NON-GROUPABLE ANALYTICAL RESULT ENTRY
                    excludedResultEntry.Add(new ExcludedResultEntryTestOrderDto(
                        order.Id, sample.Id, sample.ReferenceNumber, displayName, order.TestCode, step.StepName,
                        "Selective plate reading requires individual microbiological observation"
                    ));
                    continue;
                }
                else if (step.StepType == StepType.ConfirmatoryPlating)
                {
                    // Confirmatory plating observation readout
                    // NON-GROUPABLE ANALYTICAL RESULT ENTRY
                    excludedResultEntry.Add(new ExcludedResultEntryTestOrderDto(
                        order.Id, sample.Id, sample.ReferenceNumber, displayName, order.TestCode, step.StepName,
                        "Confirmatory plating readout requires individual plate observation"
                    ));
                    continue;
                }
                else
                {
                    continue;
                }
            }

            // No open incubation on current step. Check if predecessor steps are all completed:
            if (step.StepOrder > 1)
            {
                var hasActivePredecessorIncubation = await _db.Incubations.AnyAsync(i =>
                    i.TestOrderId == order.Id &&
                    i.StepNumber < step.StepOrder &&
                    (i.CompletedAt == null || (!i.MinimumDurationOverriddenByUserId.HasValue && i.IncubationEndUtc.HasValue && DateTime.UtcNow < i.IncubationEndUtc.Value)),
                    ct);

                if (hasActivePredecessorIncubation)
                    continue;

                var predecessors = definition.Steps.Where(s => s.StepOrder < step.StepOrder).ToList();
                bool allPredecessorsDone = true;
                foreach (var pred in predecessors)
                {
                    if (!await _workflowEngine.IsStepDoneAsync(order.Id, definition.WorkflowType, pred))
                    {
                        allPredecessorsDone = false;
                        break;
                    }
                }

                if (!allPredecessorsDone)
                    continue;
            }

            if (step.StepType is StepType.PlateCount or StepType.BrothEnrichment or StepType.SelectiveBroth or StepType.SelectivePlating)
            {
                var isSharedTsb = step.StepType == StepType.BrothEnrichment || step.StepName.Contains("TSB", StringComparison.OrdinalIgnoreCase);
                if (isSharedTsb && step.StepOrder == 1)
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

                candidates.Add(new CandidateActionItem(
                    Order: order,
                    Sample: sample,
                    CurrentStep: step,
                    TargetStep: step,
                    ActionType: "SETUP_INCUBATION",
                    TransitionType: "SETUP_INCUBATION",
                    TransitionLabel: step.StepName,
                    StepType: step.StepType.ToString(),
                    StepName: step.StepName,
                    PredecessorStepName: null,
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
            else if (step.StepType == StepType.BiochemicalTest)
            {
                // Biochemical test entry
                // NON-GROUPABLE ANALYTICAL RESULT ENTRY
                excludedResultEntry.Add(new ExcludedResultEntryTestOrderDto(
                    order.Id, sample.Id, sample.ReferenceNumber, displayName, order.TestCode, step.StepName,
                    "Biochemical test interpretation requires individual analytical result entry"
                ));
            }
        }

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

    private static List<List<CandidateActionItem>> ClusterByCompatibility(List<CandidateActionItem> items)
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

        // Domain Guard: Explicitly disallow any request targeting a result-entry operation
        var stepNameLower = request.StepName.ToLowerInvariant();
        if (stepNameLower.Contains("result") || stepNameLower.Contains("reading") ||
            (stepNameLower.Contains("count") && !stepNameLower.Contains("incubation") && !stepNameLower.Contains("tamc") && !stepNameLower.Contains("tymc")))
        {
            throw new InvalidOperationException("Grouped actions cannot be executed against analytical result-entry operations. Result entry must be performed individually.");
        }

        var isTransferIncubator = string.Equals(request.TransitionType, "TRANSFER_INCUBATOR", StringComparison.OrdinalIgnoreCase);

        Media? media = null;
        if (!isTransferIncubator)
        {
            if (!request.MediaLotId.HasValue || request.MediaLotId.Value <= 0)
            {
                throw new InvalidOperationException("Media lot must be selected for this workflow transition.");
            }

            media = await _db.Media.Include(m => m.Material)
                .FirstOrDefaultAsync(m => m.Id == request.MediaLotId.Value, ct);
            if (media == null)
            {
                throw new InvalidOperationException($"Media lot {request.MediaLotId.Value} was not found.");
            }
            if (!media.IsReleasedForUse || media.Status == MediaStatus.OutOfStock || media.Status == MediaStatus.QuarantineFailed)
            {
                throw new InvalidOperationException($"Media lot \"{media.LotNumber}\" is not released for use, out of stock, or rejected.");
            }
            if (media.ExpiryDate <= DateTime.UtcNow)
            {
                throw new InvalidOperationException($"Media lot \"{media.LotNumber}\" is expired (expired on {media.ExpiryDate:yyyy-MM-dd}).");
            }
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

            var isPrepared = order.Sample != null && order.Sample.PreparationStatus == SamplePreparationStatus.Ready;
            if (isPrepared && order.Sample!.ItemId != null)
            {
                isPrepared = await _db.SamplePreparations.AnyAsync(p => p.SampleId == order.Sample.Id);
            }

            if (!isPrepared)
            {
                skipped.Add(new BatchActionSkippedItem(id, sampleRef,
                    order.Sample?.Category is SampleCategory.EnvironmentalMonitoring or SampleCategory.AfterCleaning
                        ? "Sample preparation is not complete (locations/swabs not verified)."
                        : "Test Preparation is not complete for this sample."));
                continue;
            }

            // Backend Domain Rule: Ensure order is NOT at a result-entry step
            CurrentStepResult currentStepResult;
            try
            {
                currentStepResult = await _workflowEngine.GetCurrentStepAsync(id);
            }
            catch (Exception ex)
            {
                skipped.Add(new BatchActionSkippedItem(id, sampleRef, ex.Message));
                continue;
            }

            if (currentStepResult.AllStepsComplete || currentStepResult.Step == null)
            {
                skipped.Add(new BatchActionSkippedItem(id, sampleRef, "Test order has completed all workflow steps."));
                continue;
            }

            var activeStep = currentStepResult.Step;
            var activeInc = currentStepResult.OpenIncubation;

            // Reject if the pending action on this test is analytical result entry
            if (activeInc != null && activeInc.CompletedAt == null)
            {
                var startUtc = activeInc.IncubationStartUtc ?? activeInc.StartedAt;
                var isReady = activeInc.MinimumDurationOverriddenByUserId.HasValue ||
                    (startUtc != default && DateTime.UtcNow >= startUtc.AddHours(activeStep.IncubationMinHours));

                if (isReady)
                {
                    if (activeStep.StepType is StepType.PlateCount && (!activeStep.RequiresIncubationTransfer || activeInc.StageNumber == 2))
                    {
                        skipped.Add(new BatchActionSkippedItem(id, sampleRef, "Test order requires individual colony count entry. Result entry cannot be grouped."));
                        continue;
                    }
                    if (activeStep.StepType is StepType.SelectivePlating)
                    {
                        skipped.Add(new BatchActionSkippedItem(id, sampleRef, "Test order requires individual selective plate observation. Observations cannot be grouped."));
                        continue;
                    }
                    if (activeStep.StepType is StepType.ConfirmatoryPlating)
                    {
                        skipped.Add(new BatchActionSkippedItem(id, sampleRef, "Test order requires individual confirmatory readout. Observations cannot be grouped."));
                        continue;
                    }
                }
            }

            if (activeStep.StepType == StepType.BiochemicalTest)
            {
                skipped.Add(new BatchActionSkippedItem(id, sampleRef, "Biochemical test entry must be completed individually."));
                continue;
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

                Incubation inc;
                var transitionType = request.TransitionType;

                if (string.Equals(transitionType, "TRANSFER_INCUBATOR", StringComparison.OrdinalIgnoreCase))
                {
                    inc = await _workflowEngine.StartStage2IncubationAsync(
                        id, request.StepName, request.IncubatorEquipmentId, currentUserId);

                    _db.WorkflowHistories.Add(new WorkflowHistory
                    {
                        TestOrderId = id,
                        FromStep = WorkflowStep.Incubating,
                        ToStep = WorkflowStep.Incubating,
                        Note = $"Transferred to Stage 2 incubation ({request.StepName}). Incubator: {incubator.Code}.",
                        PerformedByUserId = currentUserId,
                        Timestamp = DateTime.UtcNow
                    });
                    await _db.SaveChangesAsync(ct);

                    succeeded.Add(new BatchActionSuccessItem(
                        id,
                        sampleRef,
                        inc.Id,
                        inc.ExpectedReadingAt ?? DateTime.UtcNow,
                        "Transferred to Stage 2 incubation successfully."
                    ));
                }
                else if (string.Equals(transitionType, "TRANSFER_SELECTIVE", StringComparison.OrdinalIgnoreCase))
                {
                    // 1. Complete the predecessor broth incubation
                    var predStepName = request.PredecessorStepName ?? activeStep.StepName;
                    await _workflowEngine.SubmitBrothAsync(id, predStepName, null, currentUserId);

                    // 2. Start incubation for the target selective step
                    var targetStepName = request.TargetStepName ?? request.StepName;
                    var targetStep = definition.Steps.FirstOrDefault(s => s.StepName == targetStepName);
                    if (targetStep == null)
                    {
                        skipped.Add(new BatchActionSkippedItem(id, sampleRef, $"Target step \"{targetStepName}\" is not part of template for {order.TestCode}."));
                        continue;
                    }

                    if (targetStep.StepType == StepType.SelectivePlating)
                    {
                        inc = await _workflowEngine.StartSelectivePlatingIncubationAsync(
                            id, targetStep.StepName, request.MediaLotId!.Value, request.IncubatorEquipmentId, request.IncubationStartUtc, currentUserId);
                    }
                    else
                    {
                        inc = await _workflowEngine.SelectMediaAsync(
                            id, targetStep.StepName, request.MediaLotId!.Value, request.IncubatorEquipmentId, currentUserId);
                    }

                    _db.WorkflowHistories.Add(new WorkflowHistory
                    {
                        TestOrderId = id,
                        FromStep = WorkflowStep.Incubating,
                        ToStep = WorkflowStep.Incubating,
                        Note = $"Transferred from {predStepName} to {targetStep.StepName} incubation. Media: {media?.LotNumber}, Incubator: {incubator.Code}.",
                        PerformedByUserId = currentUserId,
                        Timestamp = DateTime.UtcNow
                    });
                    await _db.SaveChangesAsync(ct);

                    succeeded.Add(new BatchActionSuccessItem(
                        id,
                        sampleRef,
                        inc.Id,
                        inc.ExpectedReadingAt ?? DateTime.UtcNow,
                        $"Transferred from {predStepName} to {targetStep.StepName} incubation successfully."
                    ));
                }
                else
                {
                    // SETUP_INCUBATION
                    var targetStep = definition.Steps.FirstOrDefault(s => s.StepName == request.StepName);
                    if (targetStep == null)
                    {
                        skipped.Add(new BatchActionSkippedItem(id, sampleRef, $"Step \"{request.StepName}\" is not part of template for {order.TestCode}."));
                        continue;
                    }

                    if (targetStep.StepType == StepType.SelectivePlating)
                    {
                        inc = await _workflowEngine.StartSelectivePlatingIncubationAsync(
                            id, request.StepName, request.MediaLotId!.Value, request.IncubatorEquipmentId, request.IncubationStartUtc, currentUserId);
                    }
                    else
                    {
                        inc = await _workflowEngine.SelectMediaAsync(
                            id, request.StepName, request.MediaLotId!.Value, request.IncubatorEquipmentId, currentUserId);
                    }

                    succeeded.Add(new BatchActionSuccessItem(
                        id,
                        sampleRef,
                        inc.Id,
                        inc.ExpectedReadingAt ?? DateTime.UtcNow,
                        "Incubation started successfully."
                    ));

                    if (targetStep.StepType == StepType.BrothEnrichment || targetStep.StepName.Contains("TSB", StringComparison.OrdinalIgnoreCase))
                    {
                        processedTsbSamples.Add(order.SampleId);
                    }
                }
            }
            catch (WorkflowStepException ex)
            {
                skipped.Add(new BatchActionSkippedItem(id, sampleRef, ex.Message));
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
            .Include(t => t.Steps).ThenInclude(s => s.StepMedia).ThenInclude(m => m.Material)
            .Include(t => t.Steps).ThenInclude(s => s.IncubationStages)
            .FirstOrDefaultAsync(t => t.Code == order.TestCode, ct)
            ?? throw new InvalidOperationException($"Test definition \"{order.TestCode}\" not found.");
        return (order, definition);
    }
}
