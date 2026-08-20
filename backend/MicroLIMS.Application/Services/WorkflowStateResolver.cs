using System;
using System.Collections.Generic;
using System.Linq;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Services;

public class WorkflowStateResult
{
    public string WorkflowState { get; set; } = "Pending";
    public string WorkflowStateDisplay { get; set; } = "Pending";
    public string WorkflowStatus { get; set; } = "Pending";
    public bool IsWorkflowLocked { get; set; } = false;
    public bool IsResultEntryAllowed { get; set; } = false;
    public string? LockReason { get; set; }
}

public class WorkflowStateResolver
{
    public static WorkflowStateResult Resolve(
        TestOrder testOrder,
        bool requiresTsb,
        Incubation? sharedTsb,
        IReadOnlyList<Incubation> testOrderIncubations,
        IReadOnlyList<SessionWorkflowStepDto>? stepDtos,
        DateTime utcNow,
        decimal requiredTsbHoursMin = 24)
    {
        var result = new WorkflowStateResult();

        // 1. Approved
        if (testOrder.Status == ApprovalStatus.Approved)
        {
            result.WorkflowState = "APPROVED";
            result.WorkflowStateDisplay = "Completed & Approved";
            result.WorkflowStatus = "Completed";
            result.IsWorkflowLocked = false;
            result.IsResultEntryAllowed = false;
            return result;
        }

        // 2. Results Recorded — Pending Review
        if (testOrder.CurrentStep == WorkflowStep.Ready)
        {
            result.WorkflowState = "RESULTS_RECORDED";
            result.WorkflowStateDisplay = "Result Recorded — Pending Review";
            result.WorkflowStatus = "PendingReview";
            result.IsWorkflowLocked = false;
            result.IsResultEntryAllowed = true;
            return result;
        }

        // 3. Tests requiring TSB
        if (requiresTsb)
        {
            bool tsbStarted = sharedTsb != null;
            bool tsbIncubating = TsbDetectionHelper.IsTsbIncubating(sharedTsb, requiredTsbHoursMin, utcNow);
            bool tsbCompleted = TsbDetectionHelper.IsTsbComplete(sharedTsb, requiredTsbHoursMin, utcNow);

            if (!tsbStarted)
            {
                result.WorkflowState = "PENDING";
                result.WorkflowStateDisplay = "Pending";
                result.WorkflowStatus = "Pending";
                result.IsWorkflowLocked = true;
                result.IsResultEntryAllowed = false;
                result.LockReason = "TSB broth enrichment setup required";
                return result;
            }
            if (tsbIncubating)
            {
                result.WorkflowState = "TSB_INCUBATING";
                result.WorkflowStateDisplay = "TSB Incubating";
                result.WorkflowStatus = "InProgress";
                result.IsWorkflowLocked = true;
                result.IsResultEntryAllowed = false;
                result.LockReason = "Locked until TSB incubation is complete";
                return result;
            }
            if (tsbCompleted)
            {
                // Check if any non-TSB downstream step is actively incubating
                bool downstreamIncubating = false;
                foreach (var inc in testOrderIncubations)
                {
                    if (string.IsNullOrEmpty(inc.StepName)) continue;
                    if (inc.StepName.Contains("TSB", StringComparison.OrdinalIgnoreCase) ||
                        inc.StepName.Contains("Enrichment", StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (inc.CompletedAt == null && (inc.IncubationStartUtc.HasValue || inc.StartedAt != default))
                    {
                        var start = inc.IncubationStartUtc ?? inc.StartedAt;
                        int minHours = 24;
                        if (stepDtos != null)
                        {
                            var matchedStep = stepDtos.FirstOrDefault(s => s.StepName == inc.StepName);
                            if (matchedStep != null && matchedStep.IncubationMinHours > 0) minHours = matchedStep.IncubationMinHours;
                        }
                        if (utcNow < start.AddHours(minHours))
                        {
                            downstreamIncubating = true;
                            break;
                        }
                    }
                }

                // Check downstream steps completion (steps before ConfirmatoryPlating / BiochemicalTest)
                bool allDownstreamDone = true;
                if (stepDtos != null && stepDtos.Count > 1)
                {
                    var nonFinalSteps = stepDtos.Skip(1).Where(s => s.StepType != "ConfirmatoryPlating" && s.StepType != "BiochemicalTest").ToList();
                    allDownstreamDone = nonFinalSteps.Count == 0 || nonFinalSteps.All(s => s.IsCompleted);
                }

                if (downstreamIncubating)
                {
                    result.WorkflowState = "DOWNSTREAM_INCUBATING";
                    result.WorkflowStateDisplay = "Selective Plating In Progress";
                    result.WorkflowStatus = "InProgress";
                    result.IsWorkflowLocked = false;
                    result.IsResultEntryAllowed = false;
                    return result;
                }
                else if (allDownstreamDone)
                {
                    result.WorkflowState = "AWAITING_RESULTS";
                    result.WorkflowStateDisplay = "Ready — Awaiting Primary Readings";
                    result.WorkflowStatus = "EnterResult";
                    result.IsWorkflowLocked = false;
                    result.IsResultEntryAllowed = true;
                    return result;
                }
                else
                {
                    result.WorkflowState = "READY_FOR_DOWNSTREAM";
                    result.WorkflowStateDisplay = "Ready for Downstream Testing";
                    result.WorkflowStatus = "ReadyToRead";
                    result.IsWorkflowLocked = false;
                    result.IsResultEntryAllowed = false;
                    return result;
                }
            }

            result.WorkflowState = "PENDING";
            result.WorkflowStateDisplay = "Pending";
            result.WorkflowStatus = "Pending";
            result.IsWorkflowLocked = true;
            result.IsResultEntryAllowed = false;
            return result;
        }

        // 4. Non-TSB tests (e.g. TAMC-Water) -> strictly independent
        var openCountIncubation = testOrderIncubations
            .FirstOrDefault(i =>
                i.CompletedAt == null &&
                (i.IncubationStartUtc.HasValue || i.StartedAt != default) &&
                !string.IsNullOrEmpty(i.StepName) &&
                !i.StepName.Contains("TSB", StringComparison.OrdinalIgnoreCase) &&
                !i.StepName.Contains("Enrichment", StringComparison.OrdinalIgnoreCase));

        if (openCountIncubation != null)
        {
            var start = openCountIncubation.IncubationStartUtc ?? openCountIncubation.StartedAt;
            int minHours = 0;
            if (stepDtos != null)
            {
                var matchedStep = stepDtos.FirstOrDefault(s => s.StepName == openCountIncubation.StepName);
                if (matchedStep != null && matchedStep.IncubationMinHours > 0) minHours = matchedStep.IncubationMinHours;
            }
            var minReadyAt = start.AddHours(minHours);

            if (utcNow < minReadyAt)
            {
                result.WorkflowState = "COUNT_INCUBATING";
                result.WorkflowStateDisplay = "Testing / Incubation In Progress";
                result.WorkflowStatus = "InProgress";
                result.IsWorkflowLocked = true;
                result.IsResultEntryAllowed = false;
                result.LockReason = $"Count incubation in progress. Available from: {minReadyAt:dd/MM/yyyy HH:mm}";
                return result;
            }
            else
            {
                result.WorkflowState = "AWAITING_RESULTS";
                result.WorkflowStateDisplay = "Ready — Awaiting Primary Readings";
                result.WorkflowStatus = "EnterResult";
                result.IsWorkflowLocked = false;
                result.IsResultEntryAllowed = true;
                return result;
            }
        }
        else if (testOrder.CurrentStep == WorkflowStep.Incubating)
        {
            result.WorkflowState = "INCUBATING";
            result.WorkflowStateDisplay = "Testing / Incubation In Progress";
            result.WorkflowStatus = "InProgress";
            result.IsWorkflowLocked = true;
            result.IsResultEntryAllowed = false;
            result.LockReason = "Incubation in progress";
            return result;
        }
        else if (testOrder.CurrentStep == WorkflowStep.Running)
        {
            result.WorkflowState = "RUNNING";
            result.WorkflowStateDisplay = "Testing In Progress";
            result.WorkflowStatus = "InProgress";
            result.IsWorkflowLocked = false;
            result.IsResultEntryAllowed = true;
            return result;
        }

        // Default: Pending
        result.WorkflowState = "PENDING";
        result.WorkflowStateDisplay = "Pending";
        result.WorkflowStatus = "Pending";
        result.IsWorkflowLocked = false;
        result.IsResultEntryAllowed = true;
        return result;
    }
}
