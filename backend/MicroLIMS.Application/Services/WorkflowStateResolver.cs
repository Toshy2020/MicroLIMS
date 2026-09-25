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
    // A closed test: read-only, no result entry, no next step.
    private static WorkflowStateResult Closed(WorkflowStateResult result, string state, string display, string status)
    {
        result.WorkflowState = state;
        result.WorkflowStateDisplay = display;
        result.WorkflowStatus = status;
        result.IsWorkflowLocked = true;
        result.IsResultEntryAllowed = false;
        return result;
    }

    // An open incubation whose window can't be resolved from the test's own
    // medium in Test Master. The medium is the only source of a window (see
    // IncubationWindowResolver), so the test is locked rather than timed
    // against a guessed number.
    private static WorkflowStateResult WindowNotConfigured(WorkflowStateResult result, string stepName)
    {
        result.WorkflowState = "WINDOW_NOT_CONFIGURED";
        result.WorkflowStateDisplay = "Incubation window not configured";
        result.WorkflowStatus = "Blocked";
        result.IsWorkflowLocked = true;
        result.IsResultEntryAllowed = false;
        result.LockReason = $"The incubation window for step \"{stepName}\" is not configured - set it on the medium in Test Master.";
        return result;
    }

    private static TestWorkflowStep? StepFor(IReadOnlyList<TestWorkflowStep>? steps, Incubation incubation) =>
        steps?.FirstOrDefault(s => s.StepName == incubation.StepName);

    // Stage 2 is the transfer window, not a property of any medium, so it
    // keeps resolving exactly as it did before the step-media rule.
    private static int Stage2MinHours(
        Incubation incubation,
        TestWorkflowStep? matchedStep,
        IReadOnlyList<SessionWorkflowStepDto>? stepDtos,
        DateTime start)
    {
        int minHours = 0;
        if (matchedStep != null)
        {
            var stage2 = matchedStep.IncubationStages?.FirstOrDefault(s => s.StageNumber == 2);
            minHours = stage2?.IncubationMinHours ?? matchedStep.IncubationMinHours;
        }

        if (minHours == 0 && stepDtos != null)
        {
            var matchedDto = stepDtos.FirstOrDefault(s => s.StepName == incubation.StepName);
            if (matchedDto != null && matchedDto.IncubationMinHours > 0) minHours = matchedDto.IncubationMinHours;
        }

        if (minHours == 0 && !string.IsNullOrEmpty(incubation.Duration))
        {
            var match = System.Text.RegularExpressions.Regex.Match(incubation.Duration, @"^(\d+)");
            if (match.Success && int.TryParse(match.Groups[1].Value, out var parsed))
                minHours = parsed;
        }

        if (minHours == 0 && incubation.IncubationEndUtc.HasValue)
        {
            var total = (int)Math.Round((incubation.IncubationEndUtc.Value - start).TotalHours);
            if (total > 0) minHours = total;
        }

        return minHours;
    }

    public static WorkflowStateResult Resolve(
        TestOrder testOrder,
        bool requiresTsb,
        IReadOnlyList<Incubation> testOrderIncubations,
        IReadOnlyList<SessionWorkflowStepDto>? stepDtos,
        DateTime utcNow,
        IEnumerable<TestWorkflowStep>? steps = null,
        SampleStatus? sampleStatus = null,
        IReadOnlyDictionary<int, (int MaterialId, int? MediaProductId)>? mediaLookup = null)
    {
        var result = new WorkflowStateResult();
        var stepList = steps?.ToList();

        // 0. Closed states. Once a test is superseded or voided, or its sample
        // is cancelled, nothing about its step or incubation timing applies -
        // callers pass the sample's own status so a closed sample never shows
        // a next step.
        if (testOrder.IsSuperseded)
            return Closed(result, "SUPERSEDED", "Superseded — retested on a new sample", "Superseded");
        if (testOrder.Status == ApprovalStatus.Voided || sampleStatus == SampleStatus.Voided)
            return Closed(result, "VOIDED", "Voided — struck from the record", "Voided");
        if (sampleStatus == SampleStatus.Cancelled)
            return Closed(result, "CANCELLED", "Cancelled", "Cancelled");
        // This section's own testing was closed (SectionClosureService) after
        // another laboratory rejected the sample - never a judgement about
        // this test's material, so it must never read as Rejected, and it
        // wins ahead of the sample-level Rejected/Approved checks below
        // regardless of how the rest of the sample resolves.
        if (testOrder.Status == ApprovalStatus.Cancelled)
            return Closed(result, "CANCELLED", "Cancelled — testing closed after another lab rejected the sample", "Cancelled");

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

        // 1b. Rejected - a final decision about the material. Judged by the
        // sample too, so a test whose own status predates the sample-level
        // decision still reads as rejected, never as a step to continue.
        if (testOrder.Status == ApprovalStatus.Rejected || sampleStatus == SampleStatus.Rejected)
            return Closed(result, "REJECTED", "Rejected", "Rejected");

        // Same for an approved sample whose test status never caught up.
        if (sampleStatus == SampleStatus.Approved)
        {
            result.WorkflowState = "APPROVED";
            result.WorkflowStateDisplay = "Completed & Approved";
            result.WorkflowStatus = "Completed";
            result.IsWorkflowLocked = false;
            result.IsResultEntryAllowed = false;
            return result;
        }

        // A retest was ordered: the original sample's remaining tests wait for
        // the retest outcome to decide them (SampleApprovalService.PropagateOosOutcomeAsync).
        if (sampleStatus == SampleStatus.RetestRequested)
            return Closed(result, "ON_HOLD", "On hold — awaiting retest outcome", "OnHold");

        // 1c. Reviewed — Pending Approval
        if (testOrder.Status == ApprovalStatus.Reviewed || testOrder.CurrentStep == WorkflowStep.Reviewed)
        {
            result.WorkflowState = "REVIEWED";
            result.WorkflowStateDisplay = "Reviewed — Pending Approval";
            result.WorkflowStatus = "Reviewed";
            result.IsWorkflowLocked = true;
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

        // 3. Tests requiring TSB. Each test is timed by its own TSB incubation
        // and its own medium's window - a shared TSB start only triggers the
        // incubation; it never lends one test's timing to another.
        if (requiresTsb)
        {
            var tsb = TsbDetectionHelper.FindSharedTsbIncubation(testOrderIncubations);
            if (tsb == null)
            {
                result.WorkflowState = "PENDING";
                result.WorkflowStateDisplay = "Pending";
                result.WorkflowStatus = "Pending";
                result.IsWorkflowLocked = true;
                result.IsResultEntryAllowed = false;
                result.LockReason = "TSB broth enrichment setup required";
                return result;
            }

            var tsbStep = stepList?.FirstOrDefault(s => string.Equals(s.StepName, tsb.StepName, StringComparison.OrdinalIgnoreCase))
                ?? stepList?.FirstOrDefault(s => s.StepType == StepType.BrothEnrichment || s.StepName.Contains("TSB", StringComparison.OrdinalIgnoreCase));
            var tsbWindow = tsbStep is null ? null : IncubationWindowResolver.ForIncubation(tsbStep, tsb, mediaLookup);
            if (tsbWindow is null)
                return WindowNotConfigured(result, tsbStep?.StepName ?? tsb.StepName);

            bool tsbIncubating = TsbDetectionHelper.IsTsbIncubating(tsb, tsbWindow.MinHours, utcNow);
            bool tsbCompleted = TsbDetectionHelper.IsTsbComplete(tsb, tsbWindow.MinHours, utcNow);

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
                        var incStep = StepFor(stepList, inc);
                        var window = incStep is null ? null : IncubationWindowResolver.ForIncubation(incStep, inc, mediaLookup);
                        if (window is null)
                            return WindowNotConfigured(result, inc.StepName);

                        bool isOverridden = inc.MinimumDurationOverriddenByUserId.HasValue;
                        if (!isOverridden && utcNow < window.MinReadyAt(start))
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

        // 4. Non-TSB tests (e.g. TAMC-Water, TAMC, TYMC, AC-TAMC, EM-TAMC) -> strictly independent
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
            var matchedStep = StepFor(stepList, openCountIncubation);
            int minHours;

            if (openCountIncubation.StageNumber == 2)
            {
                minHours = Stage2MinHours(openCountIncubation, matchedStep, stepDtos, start);
            }
            else
            {
                var window = matchedStep is null ? null : IncubationWindowResolver.ForIncubation(matchedStep, openCountIncubation, mediaLookup);
                if (window is null)
                    return WindowNotConfigured(result, openCountIncubation.StepName);
                minHours = window.MinHours;
            }

            var minReadyAt = start.AddHours(minHours);
            bool isOverridden = openCountIncubation.MinimumDurationOverriddenByUserId.HasValue;
            bool isReady = isOverridden || utcNow >= minReadyAt;

            if (!isReady)
            {
                result.WorkflowState = "COUNT_INCUBATING";
                result.WorkflowStateDisplay = "Incubation In Progress";
                result.WorkflowStatus = "InProgress";
                result.IsWorkflowLocked = true;
                result.IsResultEntryAllowed = false;
                result.LockReason = $"Count incubation in progress. Available from: {minReadyAt:dd/MM/yyyy HH:mm}";
                return result;
            }
            else
            {
                result.WorkflowState = "AWAITING_RESULTS";
                result.WorkflowStateDisplay = "Ready for Result Entry";
                result.WorkflowStatus = "EnterResult";
                result.IsWorkflowLocked = false;
                result.IsResultEntryAllowed = true;
                return result;
            }
        }
        else if (testOrder.CurrentStep == WorkflowStep.Incubating)
        {
            result.WorkflowState = "INCUBATING";
            result.WorkflowStateDisplay = "Incubation In Progress";
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
