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

    private static TestWorkflowStepMedia? MatchStepMedium(
        TestWorkflowStep step,
        int? mediaLotId,
        Media? incubationMedia,
        IReadOnlyDictionary<int, (int MaterialId, int? MediaProductId)>? mediaLookup)
    {
        if (step.StepMedia == null || step.StepMedia.Count == 0) return null;

        if (mediaLotId.HasValue)
        {
            int? matId = null;
            int? prodId = null;

            if (mediaLookup != null && mediaLookup.TryGetValue(mediaLotId.Value, out var lotInfo))
            {
                matId = lotInfo.MaterialId;
                prodId = lotInfo.MediaProductId;
            }
            else if (incubationMedia != null)
            {
                matId = incubationMedia.MaterialId;
                prodId = incubationMedia.Material?.MediaProductId;
            }

            if (matId.HasValue)
            {
                var matched = step.StepMedia.FirstOrDefault(m => m.MaterialId == matId.Value)
                    ?? step.StepMedia.FirstOrDefault(m => StepMediumMatcher.Matches(m, matId.Value, prodId));
                if (matched != null) return matched;
            }

            // No fallback comparing mediaLotId with MaterialId: a media lot id
            // and a batch id are unrelated, so an equal number would pick the
            // wrong medium (the bug this method replaces). Callers fall back
            // to the step's first medium instead.
        }

        return null;
    }

    public static WorkflowStateResult Resolve(
        TestOrder testOrder,
        bool requiresTsb,
        Incubation? sharedTsb,
        IReadOnlyList<Incubation> testOrderIncubations,
        IReadOnlyList<SessionWorkflowStepDto>? stepDtos,
        DateTime utcNow,
        decimal requiredTsbHoursMin = 24,
        IEnumerable<TestWorkflowStep>? steps = null,
        SampleStatus? sampleStatus = null,
        IReadOnlyDictionary<int, (int MaterialId, int? MediaProductId)>? mediaLookup = null)
    {
        var result = new WorkflowStateResult();

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
                        int minHours = 0;
                        if (steps != null)
                        {
                            var matchedStep = steps.FirstOrDefault(s => s.StepName == inc.StepName);
                            if (matchedStep != null)
                            {
                                var medium = MatchStepMedium(matchedStep, inc.MediaId, inc.Media, mediaLookup);
                                minHours = medium?.IncubationMinHours ?? matchedStep.StepMedia?.FirstOrDefault()?.IncubationMinHours ?? matchedStep.IncubationMinHours;
                            }
                        }
                        if (minHours == 0 && stepDtos != null)
                        {
                            var matchedStep = stepDtos.FirstOrDefault(s => s.StepName == inc.StepName);
                            if (matchedStep != null && matchedStep.IncubationMinHours > 0) minHours = matchedStep.IncubationMinHours;
                        }
                        if (minHours == 0 && !string.IsNullOrEmpty(inc.Duration))
                        {
                            var match = System.Text.RegularExpressions.Regex.Match(inc.Duration, @"^(\d+)");
                            if (match.Success && int.TryParse(match.Groups[1].Value, out var parsed))
                                minHours = parsed;
                        }
                        if (minHours == 0) minHours = 24;

                        bool isOverridden = inc.MinimumDurationOverriddenByUserId.HasValue;
                        if (!isOverridden && utcNow < start.AddHours(minHours))
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
            int minHours = 0;

            // 1. Check TestWorkflowStep entity configuration
            if (steps != null)
            {
                var matchedStep = steps.FirstOrDefault(s => s.StepName == openCountIncubation.StepName);
                if (matchedStep != null)
                {
                    if (openCountIncubation.StageNumber == 2)
                    {
                        var stage2 = matchedStep.IncubationStages?.FirstOrDefault(s => s.StageNumber == 2);
                        minHours = stage2?.IncubationMinHours ?? matchedStep.IncubationMinHours;
                    }
                    else
                    {
                        var medium = MatchStepMedium(matchedStep, openCountIncubation.MediaId, openCountIncubation.Media, mediaLookup);
                        minHours = medium?.IncubationMinHours ?? matchedStep.StepMedia?.FirstOrDefault()?.IncubationMinHours ?? matchedStep.IncubationMinHours;
                    }
                }
            }

            // 2. Check stepDtos
            if (minHours == 0 && stepDtos != null)
            {
                var matchedStep = stepDtos.FirstOrDefault(s => s.StepName == openCountIncubation.StepName);
                if (matchedStep != null && matchedStep.IncubationMinHours > 0) minHours = matchedStep.IncubationMinHours;
            }

            // 3. Fallback: Parse from Incubation.Duration (e.g., "1-2 hours", "72-96 hours", "48-72 hours", "24-48 hours")
            if (minHours == 0 && !string.IsNullOrEmpty(openCountIncubation.Duration))
            {
                var match = System.Text.RegularExpressions.Regex.Match(openCountIncubation.Duration, @"^(\d+)");
                if (match.Success && int.TryParse(match.Groups[1].Value, out var parsed))
                {
                    minHours = parsed;
                }
            }

            // 4. Fallback: Total window duration if IncubationEndUtc is set
            if (minHours == 0 && openCountIncubation.IncubationEndUtc.HasValue)
            {
                var total = (int)Math.Round((openCountIncubation.IncubationEndUtc.Value - start).TotalHours);
                if (total > 0) minHours = total;
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
