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

// Media selection, incubation (including the shared TSB and the second
// stage) and plate-count / observation results, single and batch.
public sealed class IncubationStepRecorder : TestWorkflowSupport
{
    public IncubationStepRecorder(TestWorkflowDependencies deps) : base(deps) { }

    public Task<Incubation> SelectMediaAsync(int testOrderId, string stepName, int mediaLotId, int incubatorEquipmentId, int userId) =>
        UnitOfWork.RunAsync(_db, () => SelectMediaCoreAsync(testOrderId, stepName, mediaLotId, incubatorEquipmentId, userId));

    private async Task<Incubation> SelectMediaCoreAsync(int testOrderId, string stepName, int mediaLotId, int incubatorEquipmentId, int userId)
    {
        var (order, definition) = await LoadWithTemplateAsync(testOrderId);
        var step = definition.Steps.FirstOrDefault(s => s.StepName == stepName)
            ?? throw new InvalidOperationException($"Step \"{stepName}\" is not part of the workflow template for \"{order.TestCode}\".");

        var currentStep = await FindFirstIncompleteStepAsync(testOrderId, definition);
        if (currentStep is null)
            throw new InvalidOperationException($"All workflow steps for \"{order.TestCode}\" are already complete.");

        await CheckPredecessorStepsReadyAsync(order, definition, step);

        if (currentStep.StepName != stepName)
            throw new InvalidOperationException($"Workflow order violation: step \"{currentStep.StepName}\" must be completed before \"{stepName}\".");

        await RequireSamplePreparedAsync(testOrderId, order.SampleId, stepName, userId, order.CurrentStep);

        var alreadyOpen = await _db.Incubations.AnyAsync(i => i.TestOrderId == testOrderId && i.StepName == stepName && i.CompletedAt == null);
        if (alreadyOpen)
            throw new InvalidOperationException($"Media has already been selected for step \"{stepName}\" - awaiting its result.");

        var media = await _db.Media.Include(m => m.Material).FirstOrDefaultAsync(m => m.Id == mediaLotId)
            ?? throw new NotFoundException($"Media lot {mediaLotId} not found.");

        if (!media.IsReleasedForUse || media.Status == MediaStatus.OutOfStock || media.Status == MediaStatus.QuarantineFailed)
            throw new InvalidOperationException($"Media lot \"{media.LotNumber}\" is not released for use, out of stock, or rejected.");
        SectionMediaRule.EnsureLot(media, order.SectionId);

        var stepMedia = await _db.TestWorkflowStepMedias
            .Include(m => m.IncubationCondition)
            .Include(m => m.Material)
            .Where(m => m.TestWorkflowStepId == step.Id)
            .ToListAsync();

        if (step.StepType is StepType.BrothEnrichment or StepType.SelectiveBroth && stepMedia.Count == 0)
            throw new InvalidOperationException($"Step \"{stepName}\" has no media configured in Test Master.");

        // The step template IS the approved specification for this test -
        // this is the product-level check that class-matching alone never
        // gave this method (see the Media Configuration Migration plan
        // §3 - flagged there as a real gap, closed here). Applies to
        // every step type that reaches this method, not just broth ones.
        var permitted = stepMedia.Any(sm => StepMediumMatcher.Matches(sm, media.MaterialId, media.Material?.MediaProductId));
        if (!permitted)
            throw new InvalidOperationException(
                $"This step requires a different medium. The selected lot is {media.Material?.MaterialName ?? "unknown"}.");

        // Temperature eligibility was already enforced server-side for
        // SelectivePlating/Confirmatory (see the other two
        // RequireEligibleIncubatorAsync call sites); this path never had
        // that check at all, a pre-existing gap unrelated to this
        // migration's origin, closed here so every step type is held to
        // the same standard.
        var selectedStepMedium = IncubationWindowResolver.MatchStepMedium(stepMedia, media.MaterialId, media.Material?.MediaProductId)!;
        var window = IncubationWindowResolver.Require(selectedStepMedium, stepName);
        await RequireEligibleIncubatorAsync(selectedStepMedium.Id, incubatorEquipmentId);

        // A TSB lot is shared with the sample's other tests - checked
        // before anything is saved, so a test that can't join blocks the
        // whole start instead of leaving this one started alone.
        var isSharedTsbStep = TsbDetectionHelper.IsSharedTsbStep(step);
        if (isSharedTsbStep)
            await RequireSharedTsbSiblingsCanJoinAsync(order, media, incubatorEquipmentId);

        var startedAt = _clock.UtcNow.UtcDateTime;
        // Incubation window is locked from Test Master: analyst cannot override.
        // The window is IncubationStartUtc to IncubationEndUtc; the analyst can
        // complete once the minimum duration has elapsed AND the end time has passed.
        var incubation = new Incubation
        {
            TestOrderId = testOrderId,
            StepNumber = step.StepOrder,
            StepName = stepName,
            StageNumber = 1,
            MediaId = mediaLotId,
            IncubatorEquipmentId = incubatorEquipmentId,
            Temperature = window.TemperatureText,
            Duration = window.DurationText,
            StartedAt = startedAt,
            IncubationStartUtc = startedAt,
            IncubationEndUtc = window.EndAt(startedAt),
            ExpectedReadingAt = window.EndAt(startedAt),
            WindowReceivedAtUtc = startedAt,
            StartedByUserId = userId
        };
        _db.Incubations.Add(incubation);

        // First lab action on this Sample - nothing else in the codebase
        // ever moves a Sample off Received, so without this
        // SampleReviewService.CanSubmitForReviewAsync's Status == InTesting
        // guard would never be satisfiable and the auto-submit-for-review
        // feature would never fire.
        var sample = await _db.Samples.FirstAsync(s => s.Id == order.SampleId);
        if (sample.Status == SampleStatus.Received)
            sample.Status = SampleStatus.InTesting;

        if (order.CurrentStep == WorkflowStep.Waiting)
            await WorkflowStateMachine.TransitionAsync(_db, order, WorkflowStep.Incubating, userId, $"Started step \"{stepName}\"");
        else
            await _db.SaveChangesAsync();

        if (isSharedTsbStep)
        {
            await PropagateSharedTsbToSiblingOrdersAsync(testOrderId, incubation.Id, userId);
        }

        return incubation;
    }

    // Every test that would newly join the shared TSB must be able to take
    // it: an unconfigured window, or an incubator outside that test's own
    // medium range, refuses the whole start and names the test.
    private async Task RequireSharedTsbSiblingsCanJoinAsync(TestOrder order, Media lot, int incubatorEquipmentId)
    {
        foreach (var sibling in await FindSharedTsbSiblingsAsync(order, lot))
        {
            if (sibling.ExistingIncubation is not null) continue;

            var mediumName = sibling.Medium.Material?.MaterialName ?? lot.Material?.MaterialName ?? "TSB";
            if (IncubationWindowResolver.TryGet(sibling.Medium) is null)
                throw new WorkflowStepException(WorkflowErrorCodes.IncubationWindowNotConfigured,
                    $"The shared TSB can't start: the incubation window for \"{mediumName}\" on test {sibling.Order.TestCode} (step \"{sibling.Step.StepName}\") is not configured - set its incubation hours and temperature in Test Master.");

            if (!await _incubatorEligibility.IsWithinRangeAsync(sibling.Medium.Id, incubatorEquipmentId))
                throw new WorkflowStepException(WorkflowErrorCodes.IncubatorTempOutOfRange,
                    $"The shared TSB can't start: the selected incubator's set point is outside the {sibling.Medium.TempMin}-{sibling.Medium.TempMax} °C range of \"{mediumName}\" on test {sibling.Order.TestCode}.");
        }
    }

    // The transfer IS starting stage 2 - there is no separate
    // confirmation step or timestamp. The physical plate does not change
    // between stages, so MediaId is copied from stage 1 rather than
    // asking the analyst to reselect it; the incubator is new, since
    // that's the whole point of a transfer.
    public async Task<Incubation> StartStage2IncubationAsync(int testOrderId, string stepName, int incubatorEquipmentId, int userId)
    {
        var (order, definition) = await LoadWithTemplateAsync(testOrderId);
        var step = definition.Steps.FirstOrDefault(s => s.StepName == stepName)
            ?? throw new InvalidOperationException($"Step \"{stepName}\" is not part of the workflow template for \"{order.TestCode}\".");

        if (step.StepType != StepType.PlateCount || !step.RequiresIncubationTransfer)
            throw new InvalidOperationException($"Step \"{stepName}\" does not use a two-stage incubation transfer.");

        var openIncubation = await _db.Incubations
            .Where(i => i.TestOrderId == testOrderId && i.StepName == stepName && i.CompletedAt == null)
            .OrderByDescending(i => i.StartedAt)
            .FirstOrDefaultAsync()
            ?? throw new InvalidOperationException($"Media must be selected for step \"{stepName}\" before stage 2 incubation can start.");

        if (openIncubation.StageNumber != 1)
            throw new InvalidOperationException($"Stage 2 incubation has already been started for step \"{stepName}\".");

        var stage1Medium = await ResolveSelectedStepMediumAsync(step.Id, openIncubation.MediaId!.Value);
        var stage1Window = IncubationWindowResolver.Require(stage1Medium, stepName);
        var stage1MinReadyAt = stage1Window.MinReadyAt(openIncubation.IncubationStartUtc!.Value);
        if (_clock.UtcNow.UtcDateTime < stage1MinReadyAt && !openIncubation.MinimumDurationOverriddenByUserId.HasValue)
            throw new WorkflowStepException(WorkflowErrorCodes.IncubationStage1NotComplete,
                $"Stage 1 incubation for step \"{stepName}\" requires at least {stage1Window.MinHours} hours of incubation - not ready until {stage1MinReadyAt:yyyy-MM-dd HH:mm} UTC.",
                Math.Max(0, (long)Math.Ceiling((stage1MinReadyAt - _clock.UtcNow.UtcDateTime).TotalSeconds)));

        var stage2Config = await _db.TestWorkflowStepIncubationStages
            .FirstOrDefaultAsync(s => s.TestWorkflowStepId == step.Id && s.StageNumber == 2)
            ?? throw new InvalidOperationException($"Step \"{stepName}\" has no stage 2 configuration.");

        var startedAt = _clock.UtcNow.UtcDateTime;
        if (startedAt < openIncubation.StartedAt)
            throw new InvalidOperationException($"Stage 2 start time ({startedAt:yyyy-MM-dd HH:mm} UTC) cannot precede Stage 1 start time ({openIncubation.StartedAt:yyyy-MM-dd HH:mm} UTC).");

        var endUtc = startedAt.AddHours(stage2Config.IncubationMaxHours);
        RequireValidIncubationWindow(stepName, stage2Config.IncubationMinHours, startedAt, endUtc);

        openIncubation.CompletedAt = startedAt;
        openIncubation.CompletedByUserId = userId;
        openIncubation.Outcome = "Transferred to stage 2 incubation.";

        var stage2 = new Incubation
        {
            TestOrderId = testOrderId,
            StepNumber = step.StepOrder,
            StepName = stepName,
            MediaId = openIncubation.MediaId,
            IncubatorEquipmentId = incubatorEquipmentId,
            Temperature = $"{stage2Config.TempMin}-{stage2Config.TempMax} °C",
            Duration = $"{stage2Config.IncubationMinHours}-{stage2Config.IncubationMaxHours} hours",
            StartedAt = startedAt,
            IncubationStartUtc = startedAt,
            IncubationEndUtc = endUtc,
            ExpectedReadingAt = endUtc,
            WindowReceivedAtUtc = startedAt,
            StartedByUserId = userId,
            ParentIncubationId = openIncubation.Id,
            StageNumber = 2
        };
        _db.Incubations.Add(stage2);
        await _db.SaveChangesAsync();

        return stage2;
    }

    public Task<TestWorkflowResult> RecordResultAsync(int testOrderId, string stepName, ResultPayload payload, int userId) =>
        UnitOfWork.RunAsync(_db, () => RecordResultCoreAsync(testOrderId, stepName, payload, userId));

    private async Task<TestWorkflowResult> RecordResultCoreAsync(int testOrderId, string stepName, ResultPayload payload, int userId)
    {
        var (order, definition) = await LoadWithTemplateAsync(testOrderId);
        var step = definition.Steps.FirstOrDefault(s => s.StepName == stepName)
            ?? throw new InvalidOperationException($"Step \"{stepName}\" is not part of the workflow template for \"{order.TestCode}\".");

        // EM/After Cleaning batch orders carry per-location results on
        // SampleLocation - the single-value/single-observation path here
        // would silently bypass that, corrupting the batch model. Use
        // RecordBatchResultsAsync/RecordBatchPathogenResultsAsync instead.
        if (await _db.SampleLocations.AnyAsync(l => l.TestOrderId == testOrderId))
            throw new InvalidOperationException("EM/After Cleaning results must be submitted via the batch results endpoint.");

        // This path's ObservationPayload branch below stages a Result but
        // never writes a WorkflowStepResult row - only the dedicated
        // pathogen Submit*Async methods (Tasks 8-11) do that. If it ever
        // ran for a pathogen step, UpsertFromPathogenResultAsync would
        // throw a confusing internal error trying to read a row that was
        // never created. record-result now serves CountTest (PlateCount)
        // steps only, so reject the mismatch here with a clear pointer to
        // the real entry points instead of letting it fail downstream.
        if (step.StepType != StepType.PlateCount)
            throw new InvalidOperationException(
                $"Step \"{stepName}\" is a pathogen workflow step - use the dedicated pathogen endpoints " +
                "(submit-broth, submit-selective-plating, submit-confirmatory-setup, etc.), not record-result.");

        var openIncubation = await _db.Incubations
            .Where(i => i.TestOrderId == testOrderId && i.StepName == stepName && i.CompletedAt == null)
            .OrderByDescending(i => i.StartedAt)
            .FirstOrDefaultAsync()
            ?? throw new InvalidOperationException($"Media must be selected for step \"{stepName}\" before a result can be recorded.");

        // Two-stage incubation transfer: the count cannot be recorded off
        // the stage 1 window at all, and not off stage 2 until its window
        // has elapsed. RequiresIncubationTransfer = false steps skip this
        // block entirely - unchanged from today.
        if (step.RequiresIncubationTransfer)
        {
            if (openIncubation.StageNumber != 2)
                throw new WorkflowStepException(WorkflowErrorCodes.IncubationStage2NotStarted,
                    $"Step \"{stepName}\" requires stage 2 incubation to be started before a count can be recorded.");

            RequireIncubationComplete(openIncubation);
        }

        string outcomeSummary;
        decimal? average = null, calculatedResult = null;
        ResultStatus? status = null;
        CountTestReading? countTestReading = null;

        switch (payload)
        {
            case CountTestPayload countPayload:
                if (definition.WorkflowType != WorkflowType.CountTest)
                    throw new InvalidOperationException($"\"{order.TestCode}\" is not a Count Test workflow.");
                (outcomeSummary, average, calculatedResult, status, countTestReading) = await RecordCountTestAsync(order, step, countPayload, userId);
                break;

            case ObservationPayload obsPayload:
                if (definition.WorkflowType == WorkflowType.CountTest)
                    throw new InvalidOperationException($"Step \"{stepName}\" does not accept a simple growth observation.");
                outcomeSummary = await RecordObservationAsync(testOrderId, step, obsPayload, userId, openIncubation.MediaId);
                break;

            default:
                throw new InvalidOperationException("Unrecognized result payload.");
        }

        openIncubation.CompletedAt = _clock.UtcNow.UtcDateTime;
        openIncubation.CompletedByUserId = userId;
        openIncubation.Outcome = outcomeSummary;
        await _db.SaveChangesAsync();

        if (countTestReading is not null)
        {
            // Needs its own SaveChangesAsync - ResultRecord.SourceId mirrors
            // the reading's real generated Id, which EF only assigns once
            // the insert above has actually gone through. Still written
            // unconditionally right alongside the reading itself, so no
            // code path can persist a CountTestReading without also
            // updating its projection row.
            await _resultProjection.UpsertFromCountTestReadingAsync(countTestReading.Id);
            await _db.SaveChangesAsync();
        }

        var isFinalStep = step.IsFinalStep || step.RequiresIncubationTransfer || (step.StepOrder == definition.Steps.Max(s => s.StepOrder));
        if (!isFinalStep)
            return new TestWorkflowResult(outcomeSummary, true, false, null, average, calculatedResult, status);

        // Final step - CountTest already writes its own Result row;
        // Observation needs one written here with the definitive call.
        if (definition.WorkflowType != WorkflowType.CountTest)
        {
            _db.Results.Add(new Result
            {
                TestOrderId = testOrderId, RawValue = outcomeSummary, InterpretedValue = outcomeSummary,
                Type = ResultType.Interpretive, EnteredByUserId = userId
            });

            // The PathogenObservation rows for this step were already
            // committed by the SaveChangesAsync above, so this can safely
            // query them back and stage a ResultRecord alongside the
            // Result row - both flush together in the SaveChangesAsync below.
            await _resultProjection.UpsertFromPathogenResultAsync(testOrderId);
        }

        // Transitioning this TestOrder to Ready and (if every TestOrder on
        // the Sample is now Ready) auto-submitting the Sample for review
        // are one logical operation with the result saved above. The public
        // RecordResultAsync runs all of it in one transaction (UnitOfWork),
        // so a failure here rolls the result back too.
        await WorkflowStateMachine.TransitionAsync(_db, order, WorkflowStep.Ready, userId, $"Workflow complete: {outcomeSummary}");
        await _sampleReviewService.AutoSubmitForReviewIfReadyAsync(order.SampleId, userId);
        await _db.SaveChangesAsync();

        return new TestWorkflowResult(outcomeSummary, true, true, outcomeSummary, average, calculatedResult, status);
    }

    // Reads every SampleLocation for this TestOrder (with limits already
    // snapshotted live from its config) so the LocationResultGrid can
    // render one row per location before any result is entered.
    public async Task<List<SampleLocation>> GetLocationsAsync(int testOrderId) =>
        await _db.SampleLocations
            .Include(l => l.RoomTestConfiguration!).ThenInclude(c => c.Room)
            .Include(l => l.MachinePartConfiguration!).ThenInclude(c => c.MachinePart)
            .Include(l => l.WaterSamplingPoint)
            .Include(l => l.SamplingConfiguration)
            .Where(l => l.TestOrderId == testOrderId)
            .ToListAsync();

    // Finds the single currently-open incubation for a batch TestOrder
    // (there is never more than one at a time) and the step template it
    // belongs to - shared by CloseCurrentIncubationWindowAsync and
    // RecordBatchResultsAsync, both of which need to know whether that
    // window is the chain's final one and when its minimum duration ends.
    private async Task<(Incubation incubation, TestWorkflowStep step, TestWorkflowStepMedia stepMedium)> LoadOpenBatchWindowAsync(int testOrderId, TestDefinition definition)
    {
        var openIncubation = await _db.Incubations
            .Where(i => i.TestOrderId == testOrderId && i.CompletedAt == null)
            .OrderByDescending(i => i.StartedAt)
            .FirstOrDefaultAsync()
            ?? throw new InvalidOperationException("No incubation window is currently open.");

        var step = definition.Steps.FirstOrDefault(s => s.StepName == openIncubation.StepName)
            ?? throw new InvalidOperationException($"Step \"{openIncubation.StepName}\" is not part of the workflow template for \"{definition.Code}\".");

        var stepMedium = await ResolveSelectedStepMediumAsync(step.Id, openIncubation.MediaId!.Value);

        return (openIncubation, step, stepMedium);
    }

    private void RequireMinimumDurationElapsed(Incubation incubation, TestWorkflowStepMedia stepMedium)
    {
        var window = IncubationWindowResolver.Require(stepMedium);
        if (incubation.MinimumDurationOverriddenByUserId.HasValue) return;
        var minReadyAt = window.MinReadyAt(incubation.StartedAt);
        if (_clock.UtcNow.UtcDateTime < minReadyAt)
            throw new InvalidOperationException(
                $"This incubation window needs at least {window.MinHours} hours - not ready until {minReadyAt:yyyy-MM-dd HH:mm} UTC.");
    }

    // Stage 2 (the PlateCount transfer window) is explicitly out of scope
    // for the medium-derived reversal - it's the transfer window itself,
    // not a property of any medium, and keeps its own independent
    // TestWorkflowStepIncubationStage fields exactly as before.
    private void RequireStage2MinimumDurationElapsed(Incubation incubation, TestWorkflowStep step, TestWorkflowStepMedia stepMedium)
    {
        if (incubation.MinimumDurationOverriddenByUserId.HasValue) return;
        var stage2Config = step.IncubationStages.FirstOrDefault(s => s.StageNumber == 2);
        var minHours = stage2Config?.IncubationMinHours ?? stepMedium.IncubationMinHours;
        var startUtc = incubation.IncubationStartUtc ?? incubation.StartedAt;
        var minReadyAt = startUtc.AddHours(minHours);
        if (_clock.UtcNow.UtcDateTime < minReadyAt)
        {
            var remaining = (long)Math.Ceiling((minReadyAt - _clock.UtcNow.UtcDateTime).TotalSeconds);
            throw new WorkflowStepException(WorkflowErrorCodes.IncubationNotComplete,
                $"Stage 2 incubation for step \"{step.StepName}\" requires at least {minHours} hours of incubation - not ready until {minReadyAt:yyyy-MM-dd HH:mm} UTC.",
                remaining);
        }
    }

    public async Task<Incubation> CloseCurrentIncubationWindowAsync(int testOrderId, int userId)
    {
        var (order, definition) = await LoadWithTemplateAsync(testOrderId);
        var (incubation, step, stepMedium) = await LoadOpenBatchWindowAsync(testOrderId, definition);

        if (step.RequiresIncubationTransfer)
            throw new InvalidOperationException("This step requires incubation transfer - start stage 2 incubation instead of advancing window.");

        if (step.IsFinalStep || (step.StepOrder == definition.Steps.Max(s => s.StepOrder) && !step.RequiresIncubationTransfer))
            throw new InvalidOperationException("This is the final incubation window - record results instead of advancing.");

        RequireMinimumDurationElapsed(incubation, stepMedium);

        incubation.CompletedAt = _clock.UtcNow.UtcDateTime;
        incubation.CompletedByUserId = userId;
        await _db.SaveChangesAsync();
        return incubation;
    }

    // Same "currently open incubation for this order" lookup every gated
    // path already uses (batch and single-step alike - both just query the
    // one open Incubation row per TestOrderId), so this works regardless of
    // step type without branching on it.
    public async Task<Incubation> OverrideMinimumDurationAsync(int testOrderId, int userId)
    {
        var incubation = await _db.Incubations
            .Where(i => i.TestOrderId == testOrderId && i.CompletedAt == null)
            .OrderByDescending(i => i.StartedAt)
            .FirstOrDefaultAsync()
            ?? throw new InvalidOperationException("No incubation window is currently open.");

        incubation.MinimumDurationOverriddenByUserId = userId;
        incubation.MinimumDurationOverriddenAt = _clock.UtcNow.UtcDateTime;
        await _db.SaveChangesAsync();
        return incubation;
    }

    public Task<TestWorkflowResult> RecordBatchResultsAsync(int testOrderId, List<BatchLocationReadings> locations, int userId) =>
        UnitOfWork.RunAsync(_db, () => RecordBatchResultsCoreAsync(testOrderId, locations, userId));

    private async Task<TestWorkflowResult> RecordBatchResultsCoreAsync(int testOrderId, List<BatchLocationReadings> locations, int userId)
    {
        var (order, definition) = await LoadWithTemplateAsync(testOrderId);
        if (order.CurrentStep != WorkflowStep.Incubating)
            throw new InvalidOperationException("Media must be selected for this test before batch results can be recorded.");

        var (openIncubation, step, stepMedium) = await LoadOpenBatchWindowAsync(testOrderId, definition);
        var isFinalIncubation = step.IsFinalStep || step.RequiresIncubationTransfer || (step.StepOrder == definition.Steps.Max(s => s.StepOrder));
        if (!isFinalIncubation)
            throw new InvalidOperationException($"\"{step.StepName}\" is not the final incubation window yet - close it and start the next window first.");

        if (step.RequiresIncubationTransfer)
        {
            if (openIncubation.StageNumber != 2)
                throw new WorkflowStepException(WorkflowErrorCodes.IncubationStage2NotStarted,
                    $"Step \"{step.StepName}\" requires stage 2 incubation to be started before results can be recorded.");

            RequireStage2MinimumDurationElapsed(openIncubation, step, stepMedium);
        }
        else
        {
            RequireMinimumDurationElapsed(openIncubation, stepMedium);
        }

        var sampleLocations = await GetLocationsAsync(testOrderId);
        if (sampleLocations.Count == 0)
            throw new InvalidOperationException("No locations are assigned to this test order.");

        var submitted = locations.ToDictionary(l => l.SampleLocationId);
        var missing = sampleLocations.Where(l => !submitted.ContainsKey(l.Id)).ToList();
        if (missing.Count > 0)
            throw new InvalidOperationException($"Results are missing for: {string.Join(", ", missing.Select(LocationName))}.");

        var emptyReadings = sampleLocations.Where(l => submitted[l.Id].Readings.Count == 0).ToList();
        if (emptyReadings.Count > 0)
            throw new InvalidOperationException($"At least one plate reading is required for: {string.Join(", ", emptyReadings.Select(LocationName))}.");

        var worstStatus = ResultStatus.WithinLimits;
        var conformCount = 0;
        foreach (var location in sampleLocations)
        {
            var readings = submitted[location.Id].Readings;
            var average = readings.Average();
            // Direct-count category - dilution factor is always 1, same
            // rule RecordCountTestAsync already forces for Water/
            // EnvironmentalMonitoring/AfterCleaning sample categories.
            var calculated = average;
            var reported = calculated < 1 ? "<1" : Math.Round(calculated, MidpointRounding.AwayFromZero).ToString("0");

            var alertLimit = location.RoomTestConfiguration?.AlertLimit ?? location.MachinePartConfiguration?.AlertLimit;
            var actionLimit = location.RoomTestConfiguration?.ActionLimit ?? location.MachinePartConfiguration?.ActionLimit;
            var specLimit = location.RoomTestConfiguration?.SpecLimit ?? location.MachinePartConfiguration?.SpecLimit;
            var (status, _) = Compare(calculated, alertLimit, actionLimit, specLimit);

            location.RawReadings = string.Join(",", readings);
            location.DilutionFactor = 1;
            location.CFUResult = average;
            location.CalculatedResult = calculated;
            location.ReportedResult = reported;
            location.AlertLimit = alertLimit;
            location.ActionLimit = actionLimit;
            location.SpecLimit = specLimit;
            location.Status = status;
            var configuredUnit = location.RoomTestConfiguration?.Unit ?? location.MachinePartConfiguration?.Unit;
            location.Unit = !string.IsNullOrWhiteSpace(configuredUnit) ? configuredUnit : DeriveBatchLocationUnit(location);
            location.EnteredAt = _clock.UtcNow.UtcDateTime;
            location.EnteredByUserId = userId;

            if (status == ResultStatus.WithinLimits) conformCount++;
            if (StatusSeverity(status) > StatusSeverity(worstStatus)) worstStatus = status;
        }

        var summary = $"{sampleLocations.Count} locations: {conformCount} conform, {sampleLocations.Count - conformCount} alert/action/spec";

        _db.Results.Add(new Result
        {
            TestOrderId = testOrderId,
            RawValue = summary,
            InterpretedValue = $"{summary} (worst: {worstStatus})",
            Type = ResultType.Numeric,
            EnteredByUserId = userId
        });

        openIncubation.CompletedAt = _clock.UtcNow.UtcDateTime;
        openIncubation.CompletedByUserId = userId;
        openIncubation.Outcome = summary;

        // Locations are existing rows (created at Prepare time) being
        // updated in place, so their Ids are already real - no extra
        // SaveChangesAsync round trip needed before projecting each one.
        foreach (var location in sampleLocations)
            await _resultProjection.UpsertFromSampleLocationAsync(location.Id);

        await WorkflowStateMachine.TransitionAsync(_db, order, WorkflowStep.Ready, userId, $"Batch results recorded: {summary}");
        await _sampleReviewService.AutoSubmitForReviewIfReadyAsync(order.SampleId, userId);
        await _db.SaveChangesAsync();

        return new TestWorkflowResult(summary, true, true, summary, null, null, worstStatus);
    }

    // Water-only batch result entry: each sampling point gets its own
    // set of raw plate readings, averaged directly (no shared dilution
    // factor) and compared to that point's own SamplingConfiguration
    // limits - the multi-reading model water has always used, now
    // applied per-location instead of per-TestOrder. Everything about
    // opening/closing the incubation window and transitioning the
    // TestOrder is identical to RecordBatchResultsAsync; only the
    // result computation differs.
    public Task<TestWorkflowResult> RecordWaterBatchReadingsAsync(int testOrderId, List<WaterBatchLocationReadings> locations, int userId) =>
        UnitOfWork.RunAsync(_db, () => RecordWaterBatchReadingsCoreAsync(testOrderId, locations, userId));

    private async Task<TestWorkflowResult> RecordWaterBatchReadingsCoreAsync(int testOrderId, List<WaterBatchLocationReadings> locations, int userId)
    {
        var (order, definition) = await LoadWithTemplateAsync(testOrderId);
        if (order.CurrentStep != WorkflowStep.Incubating)
            throw new InvalidOperationException("Media must be selected for this test before batch results can be recorded.");

        var (openIncubation, step, stepMedium) = await LoadOpenBatchWindowAsync(testOrderId, definition);
        var isFinalIncubation = step.IsFinalStep || step.RequiresIncubationTransfer || (step.StepOrder == definition.Steps.Max(s => s.StepOrder));
        if (!isFinalIncubation)
            throw new InvalidOperationException($"\"{step.StepName}\" is not the final incubation window yet - close it and start the next window first.");

        if (step.RequiresIncubationTransfer)
        {
            if (openIncubation.StageNumber != 2)
                throw new WorkflowStepException(WorkflowErrorCodes.IncubationStage2NotStarted,
                    $"Step \"{step.StepName}\" requires stage 2 incubation to be started before results can be recorded.");

            RequireStage2MinimumDurationElapsed(openIncubation, step, stepMedium);
        }
        else
        {
            RequireMinimumDurationElapsed(openIncubation, stepMedium);
        }

        var sampleLocations = await GetLocationsAsync(testOrderId);
        if (sampleLocations.Count == 0)
            throw new InvalidOperationException("No locations are assigned to this test order.");

        var submitted = locations.ToDictionary(l => l.SampleLocationId);
        var missing = sampleLocations.Where(l => !submitted.ContainsKey(l.Id)).ToList();
        if (missing.Count > 0)
            throw new InvalidOperationException($"Results are missing for: {string.Join(", ", missing.Select(LocationName))}.");

        var emptyReadings = sampleLocations.Where(l => submitted[l.Id].Readings.Count == 0).ToList();
        if (emptyReadings.Count > 0)
            throw new InvalidOperationException($"At least one plate reading is required for: {string.Join(", ", emptyReadings.Select(LocationName))}.");

        var worstStatus = ResultStatus.WithinLimits;
        var conformCount = 0;
        foreach (var location in sampleLocations)
        {
            var readings = submitted[location.Id].Readings;
            var average = readings.Average();

            var alertLimit = location.SamplingConfiguration?.AlertLimit;
            var actionLimit = location.SamplingConfiguration?.ActionLimit;
            var specLimit = location.SamplingConfiguration?.SpecLimit;
            var (status, _) = Compare(average, alertLimit, actionLimit, specLimit);

            location.RawReadings = string.Join(",", readings);
            location.CFUResult = average;
            location.CalculatedResult = average;
            // A CFU count is a whole-number concept - round for the
            // reported display, same as RecordBatchResultsAsync/
            // RecordCountTestAsync. CFUResult keeps the full-precision average.
            location.ReportedResult = Math.Round(average, MidpointRounding.AwayFromZero).ToString("0");
            location.AlertLimit = alertLimit;
            location.ActionLimit = actionLimit;
            location.SpecLimit = specLimit;
            location.Status = status;
            var configuredUnit = location.SamplingConfiguration?.Unit;
            location.Unit = !string.IsNullOrWhiteSpace(configuredUnit) ? configuredUnit : DeriveBatchLocationUnit(location);
            location.EnteredAt = _clock.UtcNow.UtcDateTime;
            location.EnteredByUserId = userId;

            if (status == ResultStatus.WithinLimits) conformCount++;
            if (StatusSeverity(status) > StatusSeverity(worstStatus)) worstStatus = status;
        }

        var summary = $"{sampleLocations.Count} locations: {conformCount} conform, {sampleLocations.Count - conformCount} alert/action/spec";

        _db.Results.Add(new Result
        {
            TestOrderId = testOrderId,
            RawValue = summary,
            InterpretedValue = $"{summary} (worst: {worstStatus})",
            Type = ResultType.Numeric,
            EnteredByUserId = userId
        });

        openIncubation.CompletedAt = _clock.UtcNow.UtcDateTime;
        openIncubation.CompletedByUserId = userId;
        openIncubation.Outcome = summary;

        foreach (var location in sampleLocations)
            await _resultProjection.UpsertFromSampleLocationAsync(location.Id);

        await WorkflowStateMachine.TransitionAsync(_db, order, WorkflowStep.Ready, userId, $"Water batch readings recorded: {summary}");
        await _sampleReviewService.AutoSubmitForReviewIfReadyAsync(order.SampleId, userId);
        await _db.SaveChangesAsync();

        return new TestWorkflowResult(summary, true, true, summary, null, null, worstStatus);
    }

    // EM/After Cleaning batch pathogen result entry - the final step's
    // per-location Detected/Absent call, once its minimum duration has
    // elapsed (same window mechanism as RecordBatchResultsAsync; every
    // intermediate step was just a shared incubation window closed via
    // CloseCurrentIncubationWindowAsync, no per-location judgment call).
    public Task<TestWorkflowResult> RecordBatchPathogenResultsAsync(
        int testOrderId, List<BatchLocationObservation>? observations, int userId) =>
        UnitOfWork.RunAsync(_db, () => RecordBatchPathogenResultsCoreAsync(testOrderId, observations, userId));

    private async Task<TestWorkflowResult> RecordBatchPathogenResultsCoreAsync(
        int testOrderId, List<BatchLocationObservation>? observations, int userId)
    {
        var (order, definition) = await LoadWithTemplateAsync(testOrderId);
        if (order.CurrentStep != WorkflowStep.Incubating)
            throw new InvalidOperationException("Media must be selected for this test before batch results can be recorded.");

        var (openIncubation, step, stepMedium) = await LoadOpenBatchWindowAsync(testOrderId, definition);
        if (!step.IsFinalStep)
            throw new InvalidOperationException($"\"{step.StepName}\" is not the final incubation window yet - close it and start the next window first.");

        RequireMinimumDurationElapsed(openIncubation, stepMedium);

        var sampleLocations = await GetLocationsAsync(testOrderId);
        if (sampleLocations.Count == 0)
            throw new InvalidOperationException("No locations are assigned to this test order.");

        var detectedCount = 0;

        if (observations is null || observations.Count == 0)
            throw new InvalidOperationException($"Step \"{step.StepName}\" requires a growth observation for every location.");

        var submitted = observations.ToDictionary(o => o.SampleLocationId);
        var missing = sampleLocations.Where(l => !submitted.ContainsKey(l.Id)).ToList();
        if (missing.Count > 0)
            throw new InvalidOperationException($"Results are missing for: {string.Join(", ", missing.Select(LocationName))}.");

        foreach (var location in sampleLocations)
        {
            var growth = submitted[location.Id].GrowthObserved;
            location.Status = growth ? ResultStatus.Detected : ResultStatus.Absent;
            location.ReportedResult = location.Status.ToString();
            location.EnteredAt = _clock.UtcNow.UtcDateTime;
            location.EnteredByUserId = userId;
            if (growth) detectedCount++;
        }

        var summary = $"{sampleLocations.Count} locations: {sampleLocations.Count - detectedCount} absent, {detectedCount} detected";
        var overallResult = detectedCount > 0 ? ResultStatus.Detected : ResultStatus.Absent;

        _db.Results.Add(new Result
        {
            TestOrderId = testOrderId,
            RawValue = summary,
            InterpretedValue = $"{summary} (overall: {overallResult})",
            Type = ResultType.Interpretive,
            EnteredByUserId = userId
        });

        openIncubation.CompletedAt = _clock.UtcNow.UtcDateTime;
        openIncubation.CompletedByUserId = userId;
        openIncubation.Outcome = summary;

        foreach (var location in sampleLocations)
            await _resultProjection.UpsertFromSampleLocationAsync(location.Id);

        await WorkflowStateMachine.TransitionAsync(_db, order, WorkflowStep.Ready, userId, $"Batch pathogen results recorded: {summary}");
        await _sampleReviewService.AutoSubmitForReviewIfReadyAsync(order.SampleId, userId);
        await _db.SaveChangesAsync();

        return new TestWorkflowResult(summary, true, true, overallResult.ToString(), null, null, overallResult);
    }

    private static string LocationName(SampleLocation l) =>
        l.RoomTestConfiguration?.Room?.Name ?? l.MachinePartConfiguration?.MachinePart?.Name ?? l.WaterSamplingPoint?.Code ?? $"Location {l.Id}";

    // Batch (SampleLocation) unit derivation for EM/After Cleaning/Water -
    // distinct from GetCfuUnit, which derives the non-batch CountTestReading
    // unit from Specification.Unit (required as of the 2026-09 Preparation
    // Configuration simplification). Batch locations never have a
    // SamplePreparation row (verified: 0 across every Water/EM/AC batch
    // sample), so the unit has to come from what was actually sampled -
    // RoomTestConfiguration.TestType / MachinePartConfiguration.TestType -
    // per QC Microbiology Supervisor sign-off (2026-08-22). Only called for
    // quantitative (CFU) locations; pathogen (Detected/Absent) locations
    // never call this and keep Unit = null.
    private static string DeriveBatchLocationUnit(SampleLocation location) => location switch
    {
        { RoomTestConfiguration.TestType: RoomTestTypes.PassiveAirSample } => "CFU/plate/4 hours",
        { RoomTestConfiguration.TestType: RoomTestTypes.SurfaceAirSample } => "CFU/25 cm2",
        // Active air, compressed air and drains: the lab enters the unit on
        // the room's test configuration (required for these types).
        { RoomTestConfiguration: { } rtc } when RoomTestTypes.UsesConfiguredUnit(rtc.TestType)
            && !string.IsNullOrWhiteSpace(rtc.Unit) => rtc.Unit.Trim(),
        { MachinePartConfiguration.TestType: "Swab" } => "CFU/25 cm2",
        { MachinePartConfiguration.TestType: "Rinse" } => "CFU/mL",
        { WaterSamplingPointId: not null } => "CFU/mL",
        _ => throw new InvalidOperationException(
            $"No unit mapping for SampleLocation {location.Id} " +
            $"(RoomTestConfiguration.TestType={location.RoomTestConfiguration?.TestType ?? "-"}, " +
            $"MachinePartConfiguration.TestType={location.MachinePartConfiguration?.TestType ?? "-"}). " +
            "Add it to DeriveBatchLocationUnit rather than guessing.")
    };

    private static int StatusSeverity(ResultStatus status) => status switch
    {
        ResultStatus.OutOfSpecification => 4,
        ResultStatus.ActionLimitExceeded => 3,
        ResultStatus.AlertLimitExceeded => 2,
        ResultStatus.LimitsNotConfigured => 1,
        _ => 0
    };

    private async Task<(string reported, decimal? average, decimal? calculated, ResultStatus status, CountTestReading reading)> RecordCountTestAsync(TestOrder order, TestWorkflowStep step, CountTestPayload payload, int userId)
    {
        if (payload.RawPlateReadings.Count == 0)
            throw new InvalidOperationException("At least one plate reading is required to calculate an average.");

        var sample = await _db.Samples
            .FirstOrDefaultAsync(s => s.Id == order.SampleId)
            ?? throw new NotFoundException("Sample not found for this test order.");

        // Force DF = 1 for direct count sample types regardless of client input
        var isDirectCount = sample.Category is
            SampleCategory.Water or
            SampleCategory.EnvironmentalMonitoring or
            SampleCategory.AfterCleaning;

        if (isDirectCount && payload.DilutionFactor != 1m)
        {
            payload = payload with { DilutionFactor = 1m };
        }

        // Direct-count sample types are never Item-based (Sample.ItemId is
        // Product/RM/PM only) so they never reach Specifications - the
        // force-to-1 rule above is the only DF logic that applies to them.
        // Everything else must have a configured DF before a result can be
        // recorded, and any entered value that disagrees with it needs a
        // justification note captured for the audit trail.
        decimal? configuredDilutionFactor = isDirectCount ? 1m : null;
        var dilutionFactorOverridden = false;
        if (!isDirectCount && sample.ItemId is not null)
        {
            var itemSpec = await SpecificationLookup.PrimaryAsync(_db, sample.Id, sample.ItemId.Value, order.TestCode);
            configuredDilutionFactor = itemSpec?.DilutionFactor;

            if (configuredDilutionFactor is null)
            {
                throw new WorkflowStepException(WorkflowErrorCodes.DilutionFactorNotConfigured,
                    $"No Dilution Factor is configured for \"{order.TestCode}\" on this item's Specifications. Configure it before recording a result.");
            }

            dilutionFactorOverridden = payload.DilutionFactor != configuredDilutionFactor.Value;
            if (dilutionFactorOverridden && string.IsNullOrWhiteSpace(payload.DilutionFactorOverrideNote))
            {
                throw new WorkflowStepException(WorkflowErrorCodes.DilutionFactorJustificationRequired,
                    $"The entered Dilution Factor ({payload.DilutionFactor}) differs from the configured value ({configuredDilutionFactor.Value}). A justification note is required.");
            }

            // SamplePreparation.Unit (removed in the 2026-09 Preparation
            // Configuration simplification) used to be a fallback CFU-unit
            // source when this was blank. Require it configured instead of
            // silently falling back, same rationale as the DF check above.
            if (string.IsNullOrWhiteSpace(itemSpec!.Unit))
            {
                throw new WorkflowStepException(WorkflowErrorCodes.CfuUnitNotConfigured,
                    $"No reporting Unit is configured for \"{order.TestCode}\" on this item's Specifications. Configure it before recording a result.");
            }
        }

        // Water/EM/AfterCleaning never reach a meaningful prepUnit here:
        // Water's CFU unit is hardcoded below regardless of prepUnit, and
        // EM/AfterCleaning count tests always go through the batch/location
        // path (RecordBatchResultsAsync), never this method.
        var unit = GetCfuUnit(sample.Category, null);

        bool hasNonNumeric = payload.RawPlateReadings
            .Any(r => r.Equals("TNTC", StringComparison.OrdinalIgnoreCase) ||
                      r.Equals("Uncountable", StringComparison.OrdinalIgnoreCase));

        CountTestReading reading;
        string reported;
        decimal? average = null;
        decimal? calculated = null;
        ResultStatus status;

        if (hasNonNumeric)
        {
            var nonNumericRaw = payload.RawPlateReadings
                .First(r => !decimal.TryParse(r, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out _))
                .Trim();

            var nonNumericValue = nonNumericRaw.Equals("TNTC", StringComparison.OrdinalIgnoreCase) ? "TNTC" : "Uncountable";

            status = ResultStatus.RequiresReview;
            reported = nonNumericValue;

            reading = new CountTestReading
            {
                TestOrderId = order.Id,
                StepName = step.StepName,
                PlateReadings = string.Join(",", payload.RawPlateReadings),
                DilutionFactor = payload.DilutionFactor,
                ConfiguredDilutionFactor = configuredDilutionFactor,
                DilutionFactorOverridden = dilutionFactorOverridden,
                DilutionFactorOverrideNote = dilutionFactorOverridden ? payload.DilutionFactorOverrideNote : null,
                Average = null,
                CalculatedResult = null,
                ReportedResult = reported,
                AlertLimit = null,
                ActionLimit = null,
                SpecLimit = null,
                Status = status,
                HasNonNumericReading = true,
                NonNumericValue = nonNumericValue,
                RequiresReview = true,
                EnteredByUserId = userId
            };
        }
        else
        {
            var numericReadings = payload.RawPlateReadings
                .Select(r => decimal.Parse(r, System.Globalization.CultureInfo.InvariantCulture))
                .ToList();

            average = numericReadings.Average();
            calculated = average.Value * payload.DilutionFactor;
            var lowerLimit = payload.DilutionFactor;

            string? alertLimit = null, actionLimit = null, specLimit = null, configuredUnit = null;
            if (sample.ItemId is not null)
            {
                var spec = await SpecificationLookup.PrimaryAsync(_db, sample.Id, sample.ItemId.Value, order.TestCode);
                alertLimit = spec?.AlertLimit; actionLimit = spec?.ActionLimit; specLimit = spec?.SpecLimit;
                configuredUnit = spec?.Unit;
            }
            else if (sample.WaterSamplingPointId is not null)
            {
                var config = await _db.SamplingConfigurations.FirstOrDefaultAsync(c => c.TestCode == order.TestCode && c.WaterSamplingPointId == sample.WaterSamplingPointId);
                alertLimit = config?.AlertLimit; actionLimit = config?.ActionLimit; specLimit = config?.SpecLimit;
                configuredUnit = config?.Unit;
            }

            var effectiveUnit = !string.IsNullOrWhiteSpace(configuredUnit)
                ? (configuredUnit.StartsWith("CFU/", StringComparison.OrdinalIgnoreCase) ? configuredUnit : $"CFU/{configuredUnit}")
                : unit;

            // A CFU count is a whole-number concept - round to the nearest
            // integer for the reported display, same as
            // RecordBatchResultsAsync already does for EM/After Cleaning.
            // G29 (up to 29 significant digits) could show a long repeating
            // decimal for readings that don't divide evenly; the
            // full-precision value is still stored on Average/
            // CalculatedResult, only the reported display is rounded.
            reported = calculated.Value < lowerLimit
                ? $"<{Math.Round(lowerLimit, MidpointRounding.AwayFromZero)} {effectiveUnit}"
                : $"{Math.Round(calculated.Value, MidpointRounding.AwayFromZero)} {effectiveUnit}";

            (status, _) = Compare(calculated.Value, alertLimit, actionLimit, specLimit);

            reading = new CountTestReading
            {
                TestOrderId = order.Id,
                StepName = step.StepName,
                PlateReadings = string.Join(",", payload.RawPlateReadings),
                DilutionFactor = payload.DilutionFactor,
                ConfiguredDilutionFactor = configuredDilutionFactor,
                DilutionFactorOverridden = dilutionFactorOverridden,
                DilutionFactorOverrideNote = dilutionFactorOverridden ? payload.DilutionFactorOverrideNote : null,
                Average = average,
                CalculatedResult = calculated,
                ReportedResult = reported,
                AlertLimit = alertLimit,
                ActionLimit = actionLimit,
                SpecLimit = specLimit,
                Status = status,
                HasNonNumericReading = false,
                NonNumericValue = null,
                RequiresReview = false,
                EnteredByUserId = userId
            };
        }

        _db.CountTestReadings.Add(reading);

        _db.Results.Add(new Result
        {
            TestOrderId = order.Id,
            RawValue = string.Join(",", payload.RawPlateReadings),
            InterpretedValue = hasNonNumeric ? $"{reported} ({status})" : $"{reported} ({status})",
            Type = ResultType.Numeric,
            EnteredByUserId = userId
        });

        return (reported, average, calculated, status, reading);
    }

    public static string GetCfuUnit(SampleCategory category, string? prepUnit)
    {
        return category switch
        {
            SampleCategory.Water
                => "CFU/mL",
            SampleCategory.EnvironmentalMonitoring
                => prepUnit == "25cm2" ? "CFU/25cm²" : "CFU/plate/4h",
            SampleCategory.AfterCleaning
                => prepUnit == "ml" ? "CFU/mL" : "CFU/25cm²",
            _ => prepUnit switch // Product, RM, PM
            {
                "ml"    => "CFU/mL",
                "gm"    => "CFU/g",
                "25cm2" => "CFU/25cm²",
                _       => $"CFU/{prepUnit ?? "unit"}"
            }
        };
    }

    // GrowthObservation != NoGrowth mirrors the old GrowthObserved bool -
    // GrowthNonConforming still counts as "growth" for this generic
    // Observation path (conformance judgment belongs to the pathogen-
    // specific step methods added in Tasks 9-11, not here).
    private async Task<string> RecordObservationAsync(int testOrderId, TestWorkflowStep step, ObservationPayload payload, int userId, int? mediaId)
    {
        _db.PathogenObservations.Add(new PathogenObservation
        {
            TestOrderId = testOrderId, StepName = step.StepName, StepOrder = step.StepOrder,
            Observation = payload.Observation, ObservedByUserId = userId, MediaId = mediaId
        });
        await Task.CompletedTask;
        var growthObserved = payload.Observation != GrowthObservation.NoGrowth;
        return step.IsFinalStep
            ? (growthObserved ? ResultStatus.Detected : ResultStatus.Absent).ToString()
            : (growthObserved ? "Growth" : "No Growth");
    }

    // Same Spec -> Action -> Alert precedence (most severe first) as
    // WaterWorkflowEngine.Compare/CountTestWorkflowEngine.Compare.
    public static (ResultStatus status, string? exceeded) Compare(decimal value, string? alert, string? action, string? spec) =>
        SpecLimitParser.Compare(value, alert, action, spec);
}
