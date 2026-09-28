using MicroLIMS.Domain.Constants;
using MicroLIMS.Shared.Exceptions;
using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Application.Abstractions.Notifications;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Shared.Constants;

namespace MicroLIMS.Application.Workflows;

// The pathogen confirmation chain after enrichment: broth, selective
// plating, confirmatory media and observations, the analyst decision and
// the biochemical review.
public sealed class PathogenConfirmationRecorder : TestWorkflowSupport
{
    public PathogenConfirmationRecorder(TestWorkflowDependencies deps) : base(deps) { }

    // Resolves the step template by name and guards workflow order,
    // reusing the existing order-violation message.
    //
    // Every pathogen Submit* method comes through here, so the two
    // invariants that span the whole chain live here rather than being
    // re-derived per step: the order must not already be finalized, and
    // the step being submitted must be the chain's first incomplete one.
    // Without the latter, each Submit* method only ever validated its own
    // caller-supplied inputs and a chain could be entered anywhere (and
    // any step re-submitted on top of itself). Mirrors the guard
    // SelectMediaAsync has always applied to the legacy path.
    private async Task<TestWorkflowStep> LoadStepAsync(int testOrderId, string stepName)
    {
        var (_, step) = await LoadOrderAndStepAsync(testOrderId, stepName);
        return step;
    }

    private async Task<(TestOrder order, TestWorkflowStep step)> LoadOrderAndStepAsync(int testOrderId, string stepName)
    {
        var order = await _db.TestOrders.FirstOrDefaultAsync(t => t.Id == testOrderId)
            ?? throw new NotFoundException($"Test order {testOrderId} not found.");
        var test = await _db.TestDefinitions
            .Include(t => t.Steps).ThenInclude(s => s.StepMedia)
            .FirstOrDefaultAsync(t => t.Code == order.TestCode)
            ?? throw new InvalidOperationException($"No test definition for {order.TestCode}.");
        var step = test.Steps.FirstOrDefault(s => s.StepName == stepName)
            ?? throw new InvalidOperationException($"Step '{stepName}' is not part of {order.TestCode}.");

        RequireOrderNotFinalized(order);

        var currentStep = await FindFirstIncompleteStepAsync(testOrderId, test);
        if (currentStep is null)
            throw new InvalidOperationException($"All workflow steps for \"{order.TestCode}\" are already complete.");

        await CheckPredecessorStepsReadyAsync(order, test, step);

        if (currentStep.StepName != stepName)
            throw new InvalidOperationException(
                $"Workflow order violation: step \"{currentStep.StepName}\" must be completed before \"{stepName}\".");

        return (order, step);
    }

    // A migrated template can carry a pathogen StepType without the
    // target organism the appearance snapshot needs (the remap in
    // AddPathogenWorkflowRefactor types legacy steps but cannot invent
    // master data for them). Fail with a clear instruction rather than
    // letting step.TargetOrganismId!.Value throw an unhandled
    // NullReferenceException and surface as a 500.
    private static int RequireTargetOrganism(TestWorkflowStep step) =>
        step.TargetOrganismId
        ?? throw new InvalidOperationException(
            $"Step \"{step.StepName}\" has no target organism configured - complete this step's template in Test Master before recording results.");

    // Same reasoning as RequireTargetOrganism, for the step's permitted
    // media list.
    private async Task<TestWorkflowStepMedia> RequireSingleStepMediumAsync(TestWorkflowStep step)
    {
        var medium = step.StepMedia.FirstOrDefault()
            ?? await _db.TestWorkflowStepMedias
                .Include(m => m.IncubationCondition)
                .Include(m => m.Material)
                .FirstOrDefaultAsync(m => m.TestWorkflowStepId == step.Id)
            ?? throw new InvalidOperationException(
                $"Step \"{step.StepName}\" has no assigned medium - complete this step's template in Test Master before recording results.");

        // StepMediumMatcher reads the product through Material and
        // IncubationCondition. LoadWithTemplateAsync includes them, but a
        // template loaded without them would make LoadReleasedLotAsync
        // silently refuse a lot from another batch of the same product - so
        // this reloads the medium when they're missing (guarded by
        // StepMediumProductMatchingTests, which clears the change tracker).
        if (medium.Material is null || (medium.MediaIncubationConditionId != null && medium.IncubationCondition is null))
        {
            medium = await _db.TestWorkflowStepMedias
                .Include(m => m.IncubationCondition)
                .Include(m => m.Material)
                .FirstAsync(m => m.Id == medium.Id);
        }

        return medium;
    }

    // The lot the analyst picked must be a released lot of the permitted
    // material. The class-level check this used to also carry (MediaTypeId
    // equality) was redundant once the MaterialId check above it existed -
    // deleted rather than translated, per the Media Configuration
    // Migration plan §3.
    private async Task<Media> LoadReleasedLotAsync(int mediaLotId, TestWorkflowStepMedia stepMedium, int testOrderId)
    {
        var lot = await _db.Media.Include(m => m.Material).FirstOrDefaultAsync(m => m.Id == mediaLotId)
            ?? throw new NotFoundException($"Media lot {mediaLotId} not found.");
        if (!lot.IsReleasedForUse || lot.Status == MediaStatus.OutOfStock || lot.Status == MediaStatus.QuarantineFailed)
            throw new InvalidOperationException($"Media lot {lot.LotNumber} is not released for use, out of stock, or rejected.");
        await SectionMediaRule.EnsureLotForTestOrderAsync(_db, lot, testOrderId);
        if (!StepMediumMatcher.Matches(stepMedium, lot.MaterialId, lot.Material?.MediaProductId))
            throw new WorkflowStepException(WorkflowErrorCodes.MediaNotInPermittedList,
                $"Media lot {lot.LotNumber} is not a lot of the permitted medium for this step.");
        return lot;
    }

    // Broth steps carry no result logic - completion is the incubation
    // window elapsing plus the analyst submitting the form. The incubation
    // window is server-controlled and recorded when SelectMediaAsync is
    // called; the analyst cannot override it. This method just records
    // that the window has completed and optionally saves an observation.
    public Task<StepResultDto> SubmitBrothAsync(
        int testOrderId, string stepName, string? observation, int userId) =>
        UnitOfWork.RunAsync(_db, () => SubmitBrothCoreAsync(testOrderId, stepName, observation, userId));

    private async Task<StepResultDto> SubmitBrothCoreAsync(
        int testOrderId, string stepName, string? observation, int userId)
    {
        var step = await LoadStepAsync(testOrderId, stepName);
        if (step.StepType is not (StepType.BrothEnrichment or StepType.SelectiveBroth))
            throw new InvalidOperationException($"Step '{stepName}' is not a broth step.");

        // Load the incubation that was created when media was selected (handling step name casing / TSB aliases).
        var incubations = await _db.Incubations
            .Where(i => i.TestOrderId == testOrderId)
            .OrderByDescending(i => i.StartedAt)
            .ToListAsync();

        var incubation = incubations.FirstOrDefault(i =>
            i.CompletedAt == null && IsIncubationForStep(i, step));

        if (incubation == null)
        {
            // Check if there is an already completed incubation (e.g. from Shared TSB or previous completion)
            var completedInc = incubations.FirstOrDefault(i =>
                i.CompletedAt != null && IsIncubationForStep(i, step));

            if (completedInc != null)
            {
                var existingResult = await _db.WorkflowStepResults
                    .FirstOrDefaultAsync(r => r.TestOrderId == testOrderId && (r.StepName == step.StepName || r.StepName == stepName));

                if (existingResult == null)
                {
                    existingResult = new WorkflowStepResult
                    {
                        IncubationId = completedInc.Id,
                        TestOrderId = testOrderId,
                        StepName = step.StepName,
                        StepType = step.StepType,
                        SubmittedByUserId = userId,
                        SubmittedAtUtc = _clock.UtcNow.UtcDateTime,
                        IsSharedSessionStep = true
                    };
                    _db.WorkflowStepResults.Add(existingResult);
                    await _db.SaveChangesAsync();
                }

                return new StepResultDto(completedInc.Id, step.StepType.ToString(), "Complete",
                    userId, existingResult.SubmittedAtUtc, NextStepUnlocked: true, WorkflowFinalResult: null, Flags: new List<string>());
            }

            throw new InvalidOperationException($"Media must be selected for step \"{stepName}\" before this submission.");
        }

        // Verify that the minimum duration has elapsed, per the medium
        // actually picked at selection time (not the step's own fields -
        // see TestWorkflowStepMedia.IncubationMinHours/MaxHours).
        var brothMedium = await ResolveSelectedStepMediumAsync(step.Id, incubation.MediaId!.Value);
        var brothWindow = IncubationWindowResolver.Require(brothMedium, step.StepName);
        if (!incubation.MinimumDurationOverriddenByUserId.HasValue)
        {
            var minReadyAt = brothWindow.MinReadyAt(incubation.IncubationStartUtc!.Value);
            if (_clock.UtcNow.UtcDateTime < minReadyAt)
                throw new WorkflowStepException(WorkflowErrorCodes.IncubationNotComplete,
                    $"This step requires at least {brothWindow.MinHours} hours of incubation - not ready until {minReadyAt:yyyy-MM-dd HH:mm} UTC.",
                    Math.Max(0, (long)Math.Ceiling((minReadyAt - _clock.UtcNow.UtcDateTime).TotalSeconds)));
        }

        // Record the completion of the incubation.
        incubation.CompletedAt = _clock.UtcNow.UtcDateTime;
        incubation.CompletedByUserId = userId;
        incubation.Outcome = observation;
        await _db.SaveChangesAsync();

        // A shared-TSB session can pre-create this row at incubation start
        // (PathogenSessionService.StartSharedTsbAsync), before this method
        // ever runs - guard against inserting a second one for the same
        // (TestOrderId, StepName) here, same as the no-open-incubation
        // branch above already does. Without this, GetCurrentStep's
        // ToDictionaryAsync(r => r.StepName, ...) throws on the duplicate.
        var result = await _db.WorkflowStepResults
            .FirstOrDefaultAsync(r => r.TestOrderId == testOrderId && r.StepName == step.StepName);
        if (result is null)
        {
            result = new WorkflowStepResult
            {
                IncubationId = incubation.Id,
                TestOrderId = testOrderId,
                StepName = step.StepName,
                StepType = step.StepType,
                SubmittedByUserId = userId,
                SubmittedAtUtc = _clock.UtcNow.UtcDateTime,
                IsSharedSessionStep = true
            };
            _db.WorkflowStepResults.Add(result);
            await _db.SaveChangesAsync();
        }

        if (step.StepType == StepType.BrothEnrichment || step.StepName.Contains("TSB", StringComparison.OrdinalIgnoreCase))
        {
            await PropagateSharedTsbToSiblingOrdersAsync(testOrderId, incubation.Id, userId);
        }

        return new StepResultDto(incubation.Id, step.StepType.ToString(), "Complete",
            userId, result.SubmittedAtUtc, NextStepUnlocked: true, WorkflowFinalResult: null, Flags: new List<string>());
    }

    // Growth that is absent or does not match the expected appearance
    // means the organism being sought is not there - the workflow ends
    public async Task<Incubation> StartSelectivePlatingIncubationAsync(
        int testOrderId, string stepName, int mediaLotId, int equipmentId, DateTime? incubationStartUtc, int userId)
    {
        var (order, definition) = await LoadWithTemplateAsync(testOrderId);
        RequireOrderNotFinalized(order);
        var step = definition.Steps.FirstOrDefault(s => s.StepName == stepName)
            ?? throw new InvalidOperationException($"Step \"{stepName}\" is not part of the workflow template for \"{order.TestCode}\".");

        if (step.StepType != StepType.SelectivePlating)
            throw new InvalidOperationException($"Step '{stepName}' is not a selective plating step.");

        var currentStep = await FindFirstIncompleteStepAsync(testOrderId, definition);
        if (currentStep is null)
            throw new InvalidOperationException($"All workflow steps for \"{order.TestCode}\" are already complete.");

        await CheckPredecessorStepsReadyAsync(order, definition, step);

        if (currentStep.StepName != stepName)
            throw new InvalidOperationException($"Workflow order violation: step \"{currentStep.StepName}\" must be completed before \"{stepName}\".");

        await RequireSamplePreparedAsync(testOrderId, order.SampleId, stepName, userId, order.CurrentStep);

        var alreadyOpen = await _db.Incubations.AnyAsync(i => i.TestOrderId == testOrderId && i.StepName == stepName && i.CompletedAt == null);
        if (alreadyOpen)
            throw new InvalidOperationException($"Incubation has already been started for step \"{stepName}\" - awaiting its result.");

        var stepMedium = await RequireSingleStepMediumAsync(step);
        var lot = await LoadReleasedLotAsync(mediaLotId, stepMedium, testOrderId);
        var window = IncubationWindowResolver.Require(stepMedium, step.StepName);
        await RequireEligibleIncubatorAsync(stepMedium.Id, equipmentId);

        var startedAt = incubationStartUtc ?? _clock.UtcNow.UtcDateTime;
        var incubation = new Incubation
        {
            TestOrderId = testOrderId,
            StepNumber = step.StepOrder,
            StepName = step.StepName,
            StageNumber = 1,
            MediaId = lot.Id,
            IncubatorEquipmentId = equipmentId,
            Temperature = window.TemperatureText,
            Duration = window.DurationText,
            StartedAt = _clock.UtcNow.UtcDateTime,
            IncubationStartUtc = startedAt,
            IncubationEndUtc = window.EndAt(startedAt),
            ExpectedReadingAt = window.EndAt(startedAt),
            WindowReceivedAtUtc = _clock.UtcNow.UtcDateTime,
            StartedByUserId = userId
        };
        _db.Incubations.Add(incubation);

        var sample = await _db.Samples.FirstAsync(s => s.Id == order.SampleId);
        if (sample.Status == SampleStatus.Received)
            sample.Status = SampleStatus.InTesting;

        if (order.CurrentStep == WorkflowStep.Waiting)
            await WorkflowStateMachine.TransitionAsync(_db, order, WorkflowStep.Incubating, userId, $"Started step \"{stepName}\"");
        else
            await _db.SaveChangesAsync();

        return incubation;
    }

    public Task<StepResultDto> SubmitSelectivePlatingObservationAsync(
        int testOrderId, string stepName, GrowthObservation observation, string? observedAppearanceNote, int userId) =>
        UnitOfWork.RunAsync(_db, () => SubmitSelectivePlatingObservationCoreAsync(testOrderId, stepName, observation, observedAppearanceNote, userId));

    private async Task<StepResultDto> SubmitSelectivePlatingObservationCoreAsync(
        int testOrderId, string stepName, GrowthObservation observation, string? observedAppearanceNote, int userId)
    {
        var step = await LoadStepAsync(testOrderId, stepName);
        if (step.StepType != StepType.SelectivePlating)
            throw new InvalidOperationException($"Step '{stepName}' is not a selective plating step.");

        var incubation = await _db.Incubations
            .Where(i => i.TestOrderId == testOrderId && i.StepName == stepName && i.CompletedAt == null)
            .OrderByDescending(i => i.StartedAt)
            .FirstOrDefaultAsync()
            ?? throw new InvalidOperationException($"No active incubation found for step \"{stepName}\". Start incubation before recording observation.");

        var stepMedium = await RequireSingleStepMediumAsync(step);

        // GMP GATE: enforce minimum incubation time server-side, per the
        // medium actually picked (see TestWorkflowStepMedia.IncubationMinHours).
        var window = IncubationWindowResolver.Require(stepMedium, step.StepName);
        if (!incubation.MinimumDurationOverriddenByUserId.HasValue)
        {
            var minReadyAt = window.MinReadyAt(incubation.IncubationStartUtc!.Value);
            if (_clock.UtcNow.UtcDateTime < minReadyAt)
            {
                var remainingSeconds = Math.Max(0, (long)Math.Ceiling((minReadyAt - _clock.UtcNow.UtcDateTime).TotalSeconds));
                throw new WorkflowStepException(WorkflowErrorCodes.IncubationNotComplete,
                    $"Minimum incubation time not elapsed. Available from: {minReadyAt:yyyy-MM-dd HH:mm} UTC.",
                    remainingSeconds);
            }
        }

        incubation.CompletedAt = _clock.UtcNow.UtcDateTime;
        incubation.CompletedByUserId = userId;
        incubation.Outcome = observation.ToString();
        await _db.SaveChangesAsync();

        var targetOrganismId = RequireTargetOrganism(step);

        // Snapshot taken at observation time, never afterwards (ALCOA+).
        var snapshot = await _appearanceSnapshot.GetExpectedAppearanceSnapshotAsync(
            stepMedium.MaterialId, targetOrganismId);

        var result = new WorkflowStepResult
        {
            IncubationId = incubation.Id,
            TestOrderId = testOrderId,
            StepName = step.StepName,
            StepType = step.StepType,
            SelectivePlatingObservation = observation,
            ExpectedAppearanceSnapshot = snapshot,
            SubmittedByUserId = userId,
            SubmittedAtUtc = _clock.UtcNow.UtcDateTime
        };
        _db.WorkflowStepResults.Add(result);

        _db.PathogenObservations.Add(new PathogenObservation
        {
            TestOrderId = testOrderId,
            StepName = step.StepName,
            StepOrder = step.StepOrder,
            Observation = observation,
            ObservedByUserId = userId,
            MediaId = incubation.MediaId!.Value
        });
        await _db.SaveChangesAsync();

        if (observation == GrowthObservation.GrowthConforming)
            return new StepResultDto(incubation.Id, step.StepType.ToString(), "Complete",
                userId, result.SubmittedAtUtc, NextStepUnlocked: true, WorkflowFinalResult: null, Flags: new List<string>());

        await FinalizeWorkflowAsync(testOrderId, ResultStatus.NotDetected, userId);
        return new StepResultDto(incubation.Id, step.StepType.ToString(), "Complete",
            userId, result.SubmittedAtUtc, NextStepUnlocked: false, WorkflowFinalResult: ResultStatus.NotDetected, Flags: new List<string>());
    }

    [Obsolete("Use StartSelectivePlatingIncubationAsync followed by SubmitSelectivePlatingObservationAsync.")]
    public async Task<StepResultDto> SubmitSelectivePlatingAsync(
        int testOrderId, string stepName, int mediaLotId, int equipmentId,
        DateTime incubationStartUtc, DateTime incubationEndUtc, GrowthObservation observation, int userId)
    {
        await StartSelectivePlatingIncubationAsync(testOrderId, stepName, mediaLotId, equipmentId, incubationStartUtc, userId);
        return await SubmitSelectivePlatingObservationAsync(testOrderId, stepName, observation, null, userId);
    }

    // The analyst's media panel for this run. Every chosen medium must be
    // on the step's permitted list, with a released lot and an in-range
    // incubator, before any plate goes into an incubator.
    public Task<StepResultDto> SubmitConfirmatorySetupAsync(
        int testOrderId, string stepName, IReadOnlyList<ConfirmatorySelectionInput> selections,
        DateTime incubationStartUtc, DateTime incubationEndUtc, int userId) =>
        UnitOfWork.RunAsync(_db, () => SubmitConfirmatorySetupCoreAsync(testOrderId, stepName, selections, incubationStartUtc, incubationEndUtc, userId));

    private async Task<StepResultDto> SubmitConfirmatorySetupCoreAsync(
        int testOrderId, string stepName, IReadOnlyList<ConfirmatorySelectionInput> selections,
        DateTime incubationStartUtc, DateTime incubationEndUtc, int userId)
    {
        // A second setup for a step that already has a result row mints a
        // fresh Incubation and WorkflowStepResult, and every downstream
        // reader takes the newest row - so the earlier run is silently
        // replaced with nothing in the audit trail saying it happened.
        // Two shapes of the same hole, both blocked here:
        //   - already read out: an Inconclusive run could be buried under
        //     a re-run and reported as Detected.
        //   - set up but not yet read out: nothing else catches this. The
        //     chain-order guard cannot, because IsStepDoneAsync
        //     deliberately treats an un-read-out setup as "not done" so
        //     the analyst still sees the step as current, and the
        //     ConfirmatoryResult test below is still null at that point.
        //     The abandoned panel's plates would just sit in an incubator
        //     unrecorded.
        // Changing a panel after submission needs a documented,
        // reason-bearing edit path; that is a separate feature.
        //
        // Resolved from one query and one throw so the analyst always
        // gets exactly one message, with the read-out case winning when
        // rows of both shapes somehow exist.
        //
        // Checked ahead of LoadStepAsync purely so these cases get their
        // own error codes and messages: the chain-order guard in there
        // would otherwise reject the read-out case first as a generic
        // order violation, which tells the analyst nothing about why.
        var existingSetups = await _db.WorkflowStepResults
            .Where(r => r.TestOrderId == testOrderId && r.StepName == stepName)
            .Select(r => r.ConfirmatoryResult)
            .ToListAsync();
        if (existingSetups.Count > 0)
            throw existingSetups.Any(r => r != null)
                ? new WorkflowStepException(WorkflowErrorCodes.ConfirmatoryAlreadyRecorded,
                    $"Confirmatory plating for step \"{stepName}\" has already been read out and cannot be set up again.")
                : new WorkflowStepException(WorkflowErrorCodes.ConfirmatorySetupAlreadySubmitted,
                    $"Confirmatory media have already been selected for step \"{stepName}\" - awaiting their plate readings. " +
                    "The submitted panel and its incubation are shown on this test order's current step.");

        var step = await LoadStepAsync(testOrderId, stepName);
        if (step.StepType != StepType.ConfirmatoryPlating)
            throw new InvalidOperationException($"Step '{stepName}' is not a confirmatory plating step.");

        if (selections.Count == 0)
            throw new WorkflowStepException(WorkflowErrorCodes.NoMediaSelected,
                "At least one confirmatory medium must be selected.");

        var permitted = await _db.TestWorkflowStepMedias
            .Include(m => m.IncubationCondition)
            .Include(m => m.Material)
            .Where(m => m.TestWorkflowStepId == step.Id)
            .ToDictionaryAsync(m => m.Id);

        if (permitted.Count == 0)
            throw new InvalidOperationException(
                $"Step \"{step.StepName}\" has no permitted media configured - complete this step's template in Test Master before recording results.");

        var resolved = new List<(TestWorkflowStepMedia Medium, Media Lot, int EquipmentId)>();
        foreach (var selection in selections)
        {
            if (!permitted.TryGetValue(selection.StepMediaId, out var medium))
                throw new WorkflowStepException(WorkflowErrorCodes.MediaNotInPermittedList,
                    "That medium is not on this step's permitted list.");
            if (selection.MediaLotId <= 0 || selection.EquipmentId <= 0)
                throw new WorkflowStepException(WorkflowErrorCodes.IncompleteConfirmatorySetup,
                    "Every selected medium needs a lot and an incubator.");

            var lot = await LoadReleasedLotAsync(selection.MediaLotId, medium, testOrderId);
            await RequireEligibleIncubatorAsync(medium.Id, selection.EquipmentId);
            resolved.Add((medium, lot, selection.EquipmentId));
        }

        // A confirmatory panel can mix media with genuinely different
        // windows on the same step (e.g. XLD at 35-37C/18-24h vs TSI at
        // 40-45C/18-24h) - there is one shared Incubation row for the
        // whole panel, so its window is the widest safe bound across every
        // medium actually picked: never shorter than the strictest
        // medium's own minimum, never shorter than the longest medium's
        // own maximum. Each medium's own eligibility (temperature) is
        // already enforced individually above, per selection - this is
        // only about the panel's one shared start/end window.
        var windows = resolved.Select(r => IncubationWindowResolver.Require(r.Medium, step.StepName)).ToList();
        var durationMax = windows.Max(w => w.MaxHours);
        incubationEndUtc = incubationStartUtc.AddHours((double)durationMax);
        var minHoursRequired = windows.Max(w => w.MinHours);
        RequireValidIncubationWindow(step.StepName, minHoursRequired, incubationStartUtc, incubationEndUtc);

        var distinctRanges = resolved.Select(r => (r.Medium.TempMin, r.Medium.TempMax)).Distinct().ToList();
        var temperatureDisplay = distinctRanges.Count == 1
            ? $"{distinctRanges[0].TempMin}-{distinctRanges[0].TempMax}"
            : string.Join("; ", distinctRanges.Select(r => $"{r.TempMin}-{r.TempMax}"));
        var distinctDurations = resolved.Select(r => (r.Medium.IncubationMinHours, r.Medium.IncubationMaxHours)).Distinct().ToList();
        var durationDisplay = distinctDurations.Count == 1
            ? $"{distinctDurations[0].IncubationMinHours}-{distinctDurations[0].IncubationMaxHours}h"
            : string.Join("; ", distinctDurations.Select(r => $"{r.IncubationMinHours}-{r.IncubationMaxHours}h"));

        var incubation = new Incubation
        {
            TestOrderId = testOrderId, StepNumber = step.StepOrder, StepName = step.StepName,
            MediaId = resolved[0].Lot.Id, IncubatorEquipmentId = resolved[0].EquipmentId,
            Temperature = temperatureDisplay,
            Duration = durationDisplay,
            IncubationStartUtc = incubationStartUtc, IncubationEndUtc = incubationEndUtc,
            WindowReceivedAtUtc = _clock.UtcNow.UtcDateTime,
            ExpectedReadingAt = incubationEndUtc
        };
        _db.Incubations.Add(incubation);
        await _db.SaveChangesAsync();

        var result = new WorkflowStepResult
        {
            IncubationId = incubation.Id, TestOrderId = testOrderId,
            StepName = step.StepName, StepType = step.StepType,
            SubmittedByUserId = userId, SubmittedAtUtc = _clock.UtcNow.UtcDateTime
        };
        foreach (var (medium, lot, equipmentId) in resolved)
            result.Selections.Add(new ConfirmatoryMediaSelection
            {
                MaterialId = medium.MaterialId, MediaId = lot.Id, EquipmentId = equipmentId, WasAnalystAdded = false
            });
        _db.WorkflowStepResults.Add(result);
        await _db.SaveChangesAsync();

        return new StepResultDto(incubation.Id, step.StepType.ToString(), "Incubating",
            userId, result.SubmittedAtUtc, NextStepUnlocked: false, WorkflowFinalResult: null, Flags: new List<string>());
    }

    // Every selected medium must be read, and every reading must be
    // conforming, before the analyst is offered a decision. Anything
    // else is Inconclusive and is flagged for investigation - there is
    // no path from here to Detected.
    public async Task<ConfirmatoryOutcomeDto> SubmitConfirmatoryObservationsAsync(
        int testOrderId, string stepName, IReadOnlyList<ConfirmatoryObservationInput> observations, int userId)
    {
        var step = await LoadStepAsync(testOrderId, stepName);
        if (step.StepType != StepType.ConfirmatoryPlating)
            throw new InvalidOperationException($"Step '{stepName}' is not a confirmatory plating step.");

        // The run still awaiting its plate readings - never simply "the
        // newest row for this step", which would let an already-read-out
        // run be silently overwritten.
        var result = await _db.WorkflowStepResults
            .Include(r => r.Selections)
            .Include(r => r.ConfirmatoryObservations)
            .Where(r => r.TestOrderId == testOrderId && r.StepName == stepName && r.ConfirmatoryResult == null)
            .OrderByDescending(r => r.Id)
            .FirstOrDefaultAsync()
            ?? throw new WorkflowStepException(WorkflowErrorCodes.IncompleteConfirmatorySetup,
                "This step's media selection has not been submitted yet.");

        var targetOrganismId = RequireTargetOrganism(step);

        var incubation = await _db.Incubations.FirstAsync(i => i.Id == result.IncubationId);
        if (incubation.IncubationEndUtc is null)
            throw new WorkflowStepException(WorkflowErrorCodes.IncompleteConfirmatorySetup,
                "This step has no recorded incubation window.");
        RequireIncubationComplete(incubation);

        // Enforced here as well as by the unique index on
        // (WorkflowStepResultId, MaterialId): a duplicate would otherwise
        // reach PostgreSQL as a DbUpdateException/500 instead of a
        // business-rule message, and the SetEquals check below cannot see
        // duplicates at all once both sides are sets.
        var duplicateMaterialIds = observations
            .GroupBy(o => o.MaterialId).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (duplicateMaterialIds.Count > 0)
            throw new WorkflowStepException(WorkflowErrorCodes.IncompleteConfirmatorySetup,
                "Exactly one observation is required for each selected medium - one was submitted more than once.");

        var selectedMaterialIds = result.Selections.Select(s => s.MaterialId).ToHashSet();
        var observedMaterialIds = observations.Select(o => o.MaterialId).ToHashSet();
        if (!selectedMaterialIds.SetEquals(observedMaterialIds))
            throw new WorkflowStepException(WorkflowErrorCodes.IncompleteConfirmatorySetup,
                "Exactly one observation is required for each selected medium.");

        foreach (var observation in observations)
        {
            var snapshot = await _appearanceSnapshot.GetExpectedAppearanceSnapshotAsync(
                observation.MaterialId, targetOrganismId);

            result.ConfirmatoryObservations.Add(new ConfirmatoryPlateObservation
            {
                MaterialId = observation.MaterialId, Observation = observation.Observation,
                ExpectedAppearanceSnapshot = snapshot, RecordedByUserId = userId, RecordedAtUtc = _clock.UtcNow.UtcDateTime
            });
        }

        var allConforming = observations.All(o => o.Observation == GrowthObservation.GrowthConforming);
        result.ConfirmatoryResult = allConforming ? ConfirmatoryResult.AllConforming : ConfirmatoryResult.Inconclusive;
        incubation.CompletedAt = _clock.UtcNow.UtcDateTime;
        incubation.CompletedByUserId = userId;
        incubation.Outcome = result.ConfirmatoryResult.ToString();

        if (!allConforming)
            _db.WorkflowHistories.Add(new WorkflowHistory
            {
                TestOrderId = testOrderId, FromStep = WorkflowStep.Incubating, ToStep = WorkflowStep.Incubating,
                Note = "Confirmatory plating inconclusive - flagged for investigation.", PerformedByUserId = userId
            });

        await _db.SaveChangesAsync();

        return new ConfirmatoryOutcomeDto(
            result.IncubationId,
            result.ConfirmatoryResult.ToString()!,
            AnalystDecisionRequired: allConforming,
            Flags: allConforming ? new List<string>() : new List<string> { "InconclusiveResult" });
    }

    // Offered only once confirmatory plating came back AllConforming.
    // Submitting as Detected is allowed but is permanently flagged so a
    // reviewer sees that no biochemical confirmation was performed.
    public Task<StepResultDto> RecordAnalystDecisionAsync(int testOrderId, AnalystDecision decision, int userId) =>
        UnitOfWork.RunAsync(_db, () => RecordAnalystDecisionCoreAsync(testOrderId, decision, userId));

    private async Task<StepResultDto> RecordAnalystDecisionCoreAsync(int testOrderId, AnalystDecision decision, int userId)
    {
        var order = await _db.TestOrders.FirstOrDefaultAsync(t => t.Id == testOrderId)
            ?? throw new NotFoundException($"Test order {testOrderId} not found.");
        RequireOrderNotFinalized(order);

        var confirmatory = await _db.WorkflowStepResults
            .Where(r => r.TestOrderId == testOrderId && r.StepType == StepType.ConfirmatoryPlating)
            .OrderByDescending(r => r.Id)
            .FirstOrDefaultAsync()
            ?? throw new InvalidOperationException("Confirmatory plating has not been completed for this test order.");

        if (confirmatory.ConfirmatoryResult != ConfirmatoryResult.AllConforming)
            throw new InvalidOperationException("An analyst decision is only available after an all-conforming confirmatory result.");

        // Single-shot. Re-running it used to append a second Result row
        // and a Ready -> Ready history entry, leaving two contradictory
        // "final" results on one test order.
        if (confirmatory.AnalystDecision is not null)
            throw new WorkflowStepException(WorkflowErrorCodes.AnalystDecisionAlreadyRecorded,
                $"An analyst decision ({confirmatory.AnalystDecision}) was already recorded for this confirmatory result.");

        confirmatory.AnalystDecision = decision;
        confirmatory.AnalystDecisionAtUtc = _clock.UtcNow.UtcDateTime;
        confirmatory.AnalystDecisionByUserId = userId;

        if (decision == AnalystDecision.ProceedToBiochemical)
        {
            // The decision point needs a contemporaneous record even when
            // it changes no state - previously this branch persisted
            // nothing at all, so "the analyst chose to confirm
            // biochemically" left no trace anywhere. Same in-place
            // history entry the inconclusive branch above uses; the
            // order's step is genuinely unchanged, so this is not a
            // transition and must not go through the state machine.
            _db.WorkflowHistories.Add(new WorkflowHistory
            {
                TestOrderId = testOrderId, FromStep = order.CurrentStep, ToStep = order.CurrentStep,
                Note = "Analyst decision: proceed to biochemical confirmation.", PerformedByUserId = userId
            });
            await _db.SaveChangesAsync();

            return new StepResultDto(confirmatory.IncubationId, StepType.ConfirmatoryPlating.ToString(), "Complete",
                userId, confirmatory.SubmittedAtUtc, NextStepUnlocked: true, WorkflowFinalResult: null, Flags: new List<string>());
        }

        confirmatory.SkippedBiochemical = true;
        _db.WorkflowHistories.Add(new WorkflowHistory
        {
            TestOrderId = testOrderId, FromStep = order.CurrentStep, ToStep = order.CurrentStep,
            Note = "Analyst decision: submitted as Detected without biochemical confirmation.", PerformedByUserId = userId
        });
        await _db.SaveChangesAsync();

        await FinalizeWorkflowAsync(testOrderId, ResultStatus.Detected, userId);

        return new StepResultDto(confirmatory.IncubationId, StepType.ConfirmatoryPlating.ToString(), "Complete",
            userId, confirmatory.SubmittedAtUtc, NextStepUnlocked: false, WorkflowFinalResult: ResultStatus.Detected,
            Flags: new List<string> { "BiochemicalNotPerformed" });
    }

    // Free-text confirmation with an optional attachment. There is no
    // incubation lock and no media on this step.
    public async Task<StepResultDto> SubmitBiochemicalAsync(
        int testOrderId, string stepName, string biochemicalResultText, int? attachmentId, bool organismDetected, int userId)
    {
        var step = await LoadStepAsync(testOrderId, stepName);
        if (step.StepType != StepType.BiochemicalTest)
            throw new InvalidOperationException($"Step '{stepName}' is not a biochemical test step.");

        if (string.IsNullOrWhiteSpace(biochemicalResultText))
            throw new WorkflowStepException(WorkflowErrorCodes.BiochemicalResultRequired,
                "A biochemical result is required.");

        var siblingSteps = await _db.TestWorkflowSteps
            .Where(s => s.TestDefinitionId == step.TestDefinitionId)
            .ToListAsync();

        // A biochemical step attaches to whichever plate-based step
        // (SelectivePlating or ConfirmatoryPlating) is its nearest
        // preceding step in the configured template - not hardcoded to
        // ConfirmatoryPlating, since some organisms (e.g. Burkholderia
        // cepacia complex) go straight from SelectivePlating to phenotypic
        // confirmatory tests with no ConfirmatoryPlating step at all.
        // Intervening BiochemicalTest steps (e.g. a prior Oxidase step) are
        // skipped so every biochemical step in a chain shares the same
        // underlying plate/incubation.
        var precedingPlateStep = siblingSteps
            .Where(s => s.StepOrder < step.StepOrder && (s.StepType == StepType.SelectivePlating || s.StepType == StepType.ConfirmatoryPlating))
            .OrderByDescending(s => s.StepOrder)
            .FirstOrDefault()
            ?? throw new InvalidOperationException(
                $"Step \"{stepName}\" has no preceding selective or confirmatory plating step configured - check Test Master.");

        var precedingResult = await _db.WorkflowStepResults
            .Where(r => r.TestOrderId == testOrderId && r.StepName == precedingPlateStep.StepName)
            .OrderByDescending(r => r.Id)
            .FirstOrDefaultAsync()
            ?? throw new InvalidOperationException($"Step \"{precedingPlateStep.StepName}\" has not been completed for this test order.");

        // ConfirmatoryPlating: both AllConforming and Inconclusive proceed to
        // biochemical ID - an Inconclusive plate reading doesn't rule out the
        // organism, it just means the analyst couldn't call it from
        // morphology alone, which is exactly what biochemical testing is
        // for. SelectivePlating has no such middle ground (single
        // GrowthObservation call) and keeps its conforming-only gate.
        var precedingConforms = precedingPlateStep.StepType == StepType.ConfirmatoryPlating
            ? precedingResult.ConfirmatoryResult is ConfirmatoryResult.AllConforming or ConfirmatoryResult.Inconclusive
            : precedingResult.SelectivePlatingObservation == GrowthObservation.GrowthConforming;
        if (!precedingConforms)
            throw new InvalidOperationException(
                $"A biochemical test is only available after a conforming result on \"{precedingPlateStep.StepName}\".");

        // Reuses the preceding plate-based step's incubation as the step
        // instance - a biochemical test has no incubation window of its own.
        var result = new WorkflowStepResult
        {
            IncubationId = precedingResult.IncubationId, TestOrderId = testOrderId,
            StepName = step.StepName, StepType = step.StepType,
            BiochemicalResultText = biochemicalResultText, BiochemicalAttachmentId = attachmentId,
            BiochemicalOrganismDetected = organismDetected,
            SkippedBiochemical = false,
            SubmittedByUserId = userId, SubmittedAtUtc = _clock.UtcNow.UtcDateTime
        };
        _db.WorkflowStepResults.Add(result);

        // Clears a reviewer's outstanding send-back, if there was one -
        // only ConfirmatoryPlating ever sets these, so this is a no-op
        // (already false) when the predecessor is SelectivePlating.
        precedingResult.RequiresBiochemical = false;
        precedingResult.SkippedBiochemical = false;
        await _db.SaveChangesAsync();

        // Some organisms chain several BiochemicalTest steps off the same
        // plate predecessor (e.g. Oxidase then Identification kit for
        // Burkholderia cepacia complex) - only the last one finalizes the
        // order. Mirrors the IsFinalStep/max-StepOrder fallback used
        // elsewhere in this file, so a stale/missing Final Step flag
        // doesn't strand the order on a non-final step.
        var isLastStep = step.IsFinalStep || step.StepOrder == siblingSteps.Max(s => s.StepOrder);
        if (!isLastStep)
            return new StepResultDto(result.IncubationId, step.StepType.ToString(), "Complete",
                userId, result.SubmittedAtUtc, NextStepUnlocked: true, WorkflowFinalResult: null, Flags: new List<string>());

        // Never assume Detected - the whole point of this field is that
        // free-text BiochemicalResultText alone must not drive the final
        // result (see BiochemicalOrganismDetected's doc comment on
        // WorkflowStepResult for the incident that made this explicit).
        var finalResult = organismDetected ? ResultStatus.Detected : ResultStatus.NotDetected;
        await FinalizeWorkflowAsync(testOrderId, finalResult, userId);

        return new StepResultDto(result.IncubationId, step.StepType.ToString(), "Complete",
            userId, result.SubmittedAtUtc, NextStepUnlocked: false, WorkflowFinalResult: finalResult, Flags: new List<string>());
    }

    // Reviewer action on a result flagged BiochemicalNotPerformed.
    // Returning re-opens the biochemical step for the analyst; the
    // signature/timeline entry goes through the existing review gate.
    public Task<StepResultDto> RecordBiochemicalReviewDecisionAsync(
        int workflowStepResultId, bool approve, string comment, int reviewerUserId) =>
        UnitOfWork.RunAsync(_db, () => RecordBiochemicalReviewDecisionCoreAsync(workflowStepResultId, approve, comment, reviewerUserId));

    private async Task<StepResultDto> RecordBiochemicalReviewDecisionCoreAsync(
        int workflowStepResultId, bool approve, string comment, int reviewerUserId)
    {
        var result = await _db.WorkflowStepResults.FirstOrDefaultAsync(r => r.Id == workflowStepResultId)
            ?? throw new NotFoundException($"Workflow step result {workflowStepResultId} not found.");

        if (await _sodGuard.DidUserPerformTestAsync(result.TestOrderId, reviewerUserId))
            throw new WorkflowStepException(WorkflowErrorCodes.SegregationOfDutiesViolation,
                "A reviewer cannot decide on a result they performed.");

        // This endpoint decides exactly one thing: whether a confirmatory
        // result submitted as Detected WITHOUT biochemical confirmation
        // stands. Any other row is not a decidable subject, and returning
        // one strands the order - it moves to Incubating while
        // SubmitBiochemicalAsync still refuses it, with no path onwards.
        if (result.StepType != StepType.ConfirmatoryPlating)
            throw new InvalidOperationException(
                $"Workflow step result {workflowStepResultId} is a {result.StepType} result - only a confirmatory plating result carries a biochemical decision.");
        if (!result.SkippedBiochemical)
            throw new InvalidOperationException(
                $"Workflow step result {workflowStepResultId} was not submitted as Detected without biochemical confirmation - there is no biochemical decision to make on it.");

        if (!approve && string.IsNullOrWhiteSpace(comment))
            throw new InvalidOperationException("A reason is required when returning a result for biochemical confirmation.");

        var order = await _db.TestOrders.FirstAsync(t => t.Id == result.TestOrderId);

        if (approve)
        {
            result.RequiresBiochemical = false;
            await _reviewGate.LogEventAsync(ReviewEntityTypes.Sample, order.SampleId, reviewerUserId,
                ReviewWorkflowEventType.ReviewCompleted, comment, ApprovalDecision.Approve);
            await _db.SaveChangesAsync();

            return new StepResultDto(result.IncubationId, result.StepType.ToString(), "Approved",
                result.SubmittedByUserId, result.SubmittedAtUtc, NextStepUnlocked: false,
                WorkflowFinalResult: ResultStatus.Detected, Flags: new List<string>());
        }

        result.RequiresBiochemical = true;
        result.ReturnReason = comment;
        result.ReturnedAtUtc = _clock.UtcNow.UtcDateTime;
        result.ReturnedByUserId = reviewerUserId;

        // Routes through the shared state machine (rather than setting
        // CurrentStep/Status inline) so this transition lands in
        // WorkflowHistory with the order's real prior step, not a
        // hardcoded guess - the order is at Ready at this point (it was
        // finalized by SubmitAsDetected), never Reviewed.
        await WorkflowStateMachine.TransitionAsync(_db, order, WorkflowStep.Incubating, reviewerUserId,
            $"Returned for biochemical confirmation: {comment}");

        // The order has gone back into testing, so the "Detected" the
        // reporting read-model is carrying for it is no longer a
        // reportable result. Re-projecting here (rather than leaving the
        // stale row standing until the biochemical submission happens to
        // refresh it) keeps Reports consistent with the workflow state.
        await _resultProjection.UpsertFromPathogenResultAsync(result.TestOrderId);

        await _reviewGate.LogEventAsync(ReviewEntityTypes.Sample, order.SampleId, reviewerUserId,
            ReviewWorkflowEventType.ReviewCompleted, comment, ApprovalDecision.Investigation);

        // Persisted as well as pushed, so the analyst still finds it in the
        // notification bell after the live push is gone - and staged here so
        // it commits in the same save as the return itself.
        string? returnMessage = null;
        if (order.AssignedAnalystId is int analystId)
        {
            var sampleReference = await _db.Samples
                .Where(s => s.Id == order.SampleId)
                .Select(s => s.ReferenceNumber)
                .FirstOrDefaultAsync();
            returnMessage = $"Test {order.TestCode} for sample {sampleReference ?? $"#{order.SampleId}"} was returned for biochemical confirmation: {comment!.Trim()}";
            _db.NotificationLogs.Add(new NotificationLog
            {
                UserId = analystId,
                Type = "TestReturnedForBiochemical",
                Message = returnMessage,
                Severity = "warning",
                SampleId = order.SampleId,
                TestOrderId = order.Id
            });
        }

        await _db.SaveChangesAsync();

        if (returnMessage is not null)
            await _notifications.NotifyAsync(order.AssignedAnalystId!.Value, returnMessage);

        return new StepResultDto(result.IncubationId, result.StepType.ToString(), "ReturnedForBiochemical",
            result.SubmittedByUserId, result.SubmittedAtUtc, NextStepUnlocked: true,
            WorkflowFinalResult: null, Flags: new List<string> { "ReturnedForBiochemical" });
    }

    // One exit point for a finished pathogen workflow: write the Result
    // row, project it, move the order to Ready, and let the existing
    // sample review service decide whether the sample can now be
    // submitted for review.
    private async Task FinalizeWorkflowAsync(int testOrderId, string finalResult, int userId)
    {
        var order = await _db.TestOrders.FirstOrDefaultAsync(t => t.Id == testOrderId)
            ?? throw new NotFoundException($"Test order {testOrderId} not found.");

        _db.Results.Add(new Result
        {
            TestOrderId = testOrderId, RawValue = finalResult, InterpretedValue = finalResult,
            Type = ResultType.Interpretive, EnteredByUserId = userId
        });

        await _db.SaveChangesAsync();

        // TransitionAsync owns the WorkflowStep -> ApprovalStatus mapping
        // and the WorkflowHistory row. Setting CurrentStep/Status inline
        // here would duplicate that mapping and let the two copies drift.
        await WorkflowStateMachine.TransitionAsync(
            _db, order, WorkflowStep.Ready, userId, $"Workflow complete: {finalResult}");

        await _resultProjection.UpsertFromPathogenResultAsync(testOrderId);
        await _sampleReviewService.AutoSubmitForReviewIfReadyAsync(order.SampleId, userId);

        // Both calls above only stage their changes - the ResultRecord
        // projection and the sample's submit-for-review transition. The
        // sibling finalization paths all flush them here; without this
        // save both are silently discarded.
        await _db.SaveChangesAsync();
    }
}
