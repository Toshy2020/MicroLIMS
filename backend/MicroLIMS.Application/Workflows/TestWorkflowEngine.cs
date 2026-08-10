using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Constants;

namespace MicroLIMS.Application.Workflows;

// Result payload union for RecordResultAsync - which record is passed
// depends on the TestDefinition's WorkflowType (CountTest) or plain
// Observation otherwise.
public abstract record ResultPayload;
public sealed record CountTestPayload(List<decimal> PlateReadings, decimal DilutionFactor) : ResultPayload;
public sealed record ObservationPayload(GrowthObservation Observation) : ResultPayload;

// A business-rule failure that carries a machine-readable code for the
// frontend. Derives from InvalidOperationException so that if a call
// site does not special-case it, ExceptionMiddleware still returns 400
// with the message rather than a 500.
public class WorkflowStepException : InvalidOperationException
{
    public string ErrorCode { get; }
    public long? RemainingSeconds { get; }

    public WorkflowStepException(string errorCode, string message, long? remainingSeconds = null) : base(message)
    {
        ErrorCode = errorCode;
        RemainingSeconds = remainingSeconds;
    }
}

// The outcome of any single pathogen step submission (Tasks 8-11) -
// StepType is sent as its string name since the frontend has no reason
// to know the C# enum. WorkflowFinalResult/NextStepUnlocked are mutually
// informative: a non-null final result always means NextStepUnlocked is
// false, and vice versa for an in-progress chain.
public record StepResultDto(
    int StepInstanceId, string StepType, string Status,
    int SubmittedByUserId, DateTime SubmittedAtUtc,
    bool NextStepUnlocked, string? WorkflowFinalResult, List<string> Flags);

// One already-completed step for the step-chain strip - Outcome is
// always the same summary string RecordResultAsync already computed
// and persisted onto that step's Incubation.Outcome, never recomputed
// here. ReportedResult/CalculatedResult/Status only populated for
// PlateCount.
public record CompletedStepSummary(
    int StepOrder, string StepName, StepType StepType, bool IsFinalStep,
    string Outcome, DateTime? ObservedAt,
    string? ReportedResult, decimal? CalculatedResult, string? Status);

// What GET current-step needs to render any phase of the workflow
// dialog: the step template (null once every step is done), whether an
// incubation is already open for it (Phase A vs B), the final result
// once AllStepsComplete, and (for the step-chain strip) every already-
// completed step plus the template's total step count.
// StepOrder/StepName only for every step in the template, regardless of
// completion state - the step-chain strip needs this to label the
// remaining (not-yet-reached) chips, which CompletedSteps/Step alone
// don't cover.
public record StepOutline(int StepOrder, string StepName);

public record CurrentStepResult(
    TestWorkflowStep? Step, WorkflowType WorkflowType, Incubation? OpenIncubation, bool AllStepsComplete, string? FinalResult,
    List<CompletedStepSummary> CompletedSteps, int TotalSteps, List<StepOutline> AllSteps);

public record TestWorkflowResult(
    string OutcomeSummary, bool IsDefinitive, bool AllStepsComplete, string? FinalResult,
    decimal? Average, decimal? CalculatedResult, string? Status);

// One location's CFU reading submitted from the LocationResultGrid -
// EM/After Cleaning batch results, never used by the single-value
// RecordResultAsync path.
public record BatchLocationResult(int SampleLocationId, decimal CFUResult);

// EM/After Cleaning batch pathogen results - the final step's per-
// location growth observation call.
public record BatchLocationObservation(int SampleLocationId, bool GrowthObserved);

public interface ITestWorkflowEngine : IStatefulWorkflowEngine
{
    Task<CurrentStepResult> GetCurrentStepAsync(int testOrderId);
    Task<Incubation> SelectMediaAsync(int testOrderId, string stepName, int mediaLotId, int incubatorEquipmentId, int userId);
    Task<TestWorkflowResult> RecordResultAsync(int testOrderId, string stepName, ResultPayload payload, int userId);
    Task<List<SampleLocation>> GetLocationsAsync(int testOrderId);
    Task<Incubation> CloseCurrentIncubationWindowAsync(int testOrderId, int userId);
    Task<TestWorkflowResult> RecordBatchResultsAsync(int testOrderId, decimal dilutionFactor, List<BatchLocationResult> locations, int userId);
    Task<TestWorkflowResult> RecordBatchPathogenResultsAsync(int testOrderId, List<BatchLocationObservation>? observations, int userId);

    // Broth steps carry no result logic - completion is the incubation
    // window elapsing plus the analyst submitting the form.
    Task<StepResultDto> SubmitBrothAsync(int testOrderId, string stepName, int mediaLotId, int equipmentId,
        DateTime incubationStartUtc, DateTime incubationEndUtc, string? observation, int userId);

    Task<StepResultDto> SubmitSelectivePlatingAsync(int testOrderId, string stepName, int mediaLotId, int equipmentId,
        DateTime incubationStartUtc, DateTime incubationEndUtc, GrowthObservation observation, int userId);
}

// Generic step-runner replacing PathogenWorkflowEngine and
// CountTestWorkflowEngine: every test's chain (TAMC's single count
// step, a pathogen's five-stage Broth->Selective Broth->Selective
// Plating->Confirmatory Plating->Biochemical Test chain) is read from
// TestDefinition.WorkflowType + TestWorkflowStep, never hardcoded here.
// Nothing in this file compares against a literal test code or step
// name - that logic lives entirely in master data now.
public class TestWorkflowEngine : ITestWorkflowEngine
{
    private readonly MicroLimsDbContext _db;
    private readonly SampleReviewService _sampleReviewService;
    private readonly ResultProjectionService _resultProjection;
    private readonly IncubatorEligibilityService _incubatorEligibility;
    private readonly MediaAppearanceSnapshotService _appearanceSnapshot;

    public TestWorkflowEngine(
        MicroLimsDbContext db, SampleReviewService sampleReviewService, ResultProjectionService resultProjection,
        IncubatorEligibilityService incubatorEligibility, MediaAppearanceSnapshotService appearanceSnapshot)
    {
        _db = db;
        _sampleReviewService = sampleReviewService;
        _resultProjection = resultProjection;
        _incubatorEligibility = incubatorEligibility;
        _appearanceSnapshot = appearanceSnapshot;
    }

    private async Task<(TestOrder order, TestDefinition definition)> LoadWithTemplateAsync(int testOrderId)
    {
        var order = await WorkflowStateMachine.LoadOrThrowAsync(_db, testOrderId);
        var definition = await _db.TestDefinitions
            .Include(t => t.Steps).ThenInclude(s => s.MediaType)
            .FirstOrDefaultAsync(t => t.Code == order.TestCode)
            ?? throw new InvalidOperationException($"Test code \"{order.TestCode}\" has no workflow template configured in Test Master.");

        if (definition.Steps.Count == 0)
            throw new InvalidOperationException($"Test code \"{order.TestCode}\" has no workflow steps configured yet - add them in Test Master.");

        return (order, definition);
    }

    // A step is "done" once a definitive result has been recorded for
    // it: a CountTestReading for CountTest workflows (always one step),
    // or any observation for a plain Observation step.
    private async Task<bool> IsStepDoneAsync(int testOrderId, WorkflowType workflowType, TestWorkflowStep step)
    {
        // EM/After Cleaning batch orders never write a CountTestReading or
        // PathogenObservation for any step - they carry per-location
        // results on SampleLocation instead, only for the final step
        // (see RecordBatchResultsAsync). A non-final step is "done" once
        // its incubation window has been explicitly closed via
        // CloseCurrentIncubationWindowAsync; the final step's "done"-ness
        // is irrelevant here since a completed batch order's TestOrder
        // moves straight to Ready and GetCurrentStepAsync short-circuits
        // before ever reaching this check again.
        if (await _db.SampleLocations.AnyAsync(l => l.TestOrderId == testOrderId))
            return await _db.Incubations.AnyAsync(i => i.TestOrderId == testOrderId && i.StepName == step.StepName && i.CompletedAt != null);

        if (workflowType == WorkflowType.CountTest)
            return await _db.CountTestReadings.AnyAsync(r => r.TestOrderId == testOrderId && r.StepName == step.StepName);

        return await _db.PathogenObservations.AnyAsync(o => o.TestOrderId == testOrderId && o.StepName == step.StepName);
    }

    // Finds the lowest-StepOrder step that isn't done yet, or null if the
    // whole template is complete. Shared by GetCurrentStepAsync (to know
    // what to show) and SelectMediaAsync (to reject an out-of-order
    // start attempt - the equivalent of the old PathogenWorkflowEngine's
    // "chain violation" guard, generalized to any template).
    private async Task<TestWorkflowStep?> FindFirstIncompleteStepAsync(int testOrderId, TestDefinition definition)
    {
        foreach (var step in definition.Steps.OrderBy(s => s.StepOrder))
        {
            if (!await IsStepDoneAsync(testOrderId, definition.WorkflowType, step))
                return step;
        }
        return null;
    }

    public async Task<CurrentStepResult> GetCurrentStepAsync(int testOrderId)
    {
        var (order, definition) = await LoadWithTemplateAsync(testOrderId);
        var totalSteps = definition.Steps.Count;
        var allSteps = definition.Steps.OrderBy(s => s.StepOrder).Select(s => new StepOutline(s.StepOrder, s.StepName)).ToList();

        // Once a TestOrder is at Ready or beyond, its workflow is done -
        // short-circuit rather than walking the step template. Needed for
        // EM/After Cleaning batch orders, whose RecordBatchResultsAsync
        // transitions straight to Ready without ever writing a
        // CountTestReading, so IsStepDoneAsync's CountTestReadings check
        // would otherwise report the order as perpetually incomplete.
        if (order.CurrentStep is WorkflowStep.Ready or WorkflowStep.Reviewed or WorkflowStep.Approved)
        {
            var doneResult = order.Results.OrderByDescending(r => r.Id).FirstOrDefault()?.InterpretedValue
                ?? order.Results.OrderByDescending(r => r.Id).FirstOrDefault()?.RawValue;
            var completed = await BuildCompletedStepsAsync(testOrderId, definition, currentStep: null);
            return new CurrentStepResult(null, definition.WorkflowType, null, true, doneResult, completed, totalSteps, allSteps);
        }

        var step = await FindFirstIncompleteStepAsync(testOrderId, definition);
        if (step is not null)
        {
            var openIncubation = await _db.Incubations
                .Where(i => i.TestOrderId == testOrderId && i.StepName == step.StepName && i.CompletedAt == null)
                .OrderByDescending(i => i.StartedAt)
                .FirstOrDefaultAsync();

            var completed = await BuildCompletedStepsAsync(testOrderId, definition, currentStep: step);
            return new CurrentStepResult(step, definition.WorkflowType, openIncubation, false, null, completed, totalSteps, allSteps);
        }

        var finalResult = order.Results.OrderByDescending(r => r.Id).FirstOrDefault()?.InterpretedValue
            ?? order.Results.OrderByDescending(r => r.Id).FirstOrDefault()?.RawValue;
        var allCompleted = await BuildCompletedStepsAsync(testOrderId, definition, currentStep: null);
        return new CurrentStepResult(null, definition.WorkflowType, null, true, finalResult, allCompleted, totalSteps, allSteps);
    }

    // Every step ordered before currentStep (or every step, once the
    // whole template is done) that IsStepDoneAsync confirms is actually
    // complete - drives the frontend's step-chain strip. Outcome is
    // always read back from that step's own closed Incubation.Outcome
    // (the summary RecordResultAsync/RecordBatchResultsAsync already
    // computed), never recomputed here.
    private async Task<List<CompletedStepSummary>> BuildCompletedStepsAsync(int testOrderId, TestDefinition definition, TestWorkflowStep? currentStep)
    {
        var summaries = new List<CompletedStepSummary>();
        foreach (var step in definition.Steps.OrderBy(s => s.StepOrder))
        {
            if (currentStep is not null && step.StepOrder >= currentStep.StepOrder) break;
            if (!await IsStepDoneAsync(testOrderId, definition.WorkflowType, step)) continue;

            var latestIncubation = await _db.Incubations
                .Where(i => i.TestOrderId == testOrderId && i.StepName == step.StepName && i.CompletedAt != null)
                .OrderByDescending(i => i.CompletedAt)
                .FirstOrDefaultAsync();
            var outcome = latestIncubation?.Outcome ?? string.Empty;
            var observedAt = latestIncubation?.CompletedAt;

            string? reportedResult = null, status = null;
            decimal? calculatedResult = null;

            if (step.StepType == StepType.PlateCount)
            {
                var reading = await _db.CountTestReadings
                    .Where(r => r.TestOrderId == testOrderId && r.StepName == step.StepName)
                    .OrderByDescending(r => r.Id)
                    .FirstOrDefaultAsync();
                if (reading is not null)
                {
                    reportedResult = reading.ReportedResult;
                    calculatedResult = reading.CalculatedResult;
                    status = reading.Status;
                    observedAt = reading.EnteredAt;
                }
            }

            summaries.Add(new CompletedStepSummary(
                step.StepOrder, step.StepName, step.StepType, step.IsFinalStep,
                outcome, observedAt,
                reportedResult, calculatedResult, status));
        }
        return summaries;
    }

    public async Task<Incubation> SelectMediaAsync(int testOrderId, string stepName, int mediaLotId, int incubatorEquipmentId, int userId)
    {
        var (order, definition) = await LoadWithTemplateAsync(testOrderId);
        var step = definition.Steps.FirstOrDefault(s => s.StepName == stepName)
            ?? throw new InvalidOperationException($"Step \"{stepName}\" is not part of the workflow template for \"{order.TestCode}\".");

        var currentStep = await FindFirstIncompleteStepAsync(testOrderId, definition);
        if (currentStep is null)
            throw new InvalidOperationException($"All workflow steps for \"{order.TestCode}\" are already complete.");
        if (currentStep.StepName != stepName)
            throw new InvalidOperationException($"Workflow order violation: step \"{currentStep.StepName}\" must be completed before \"{stepName}\".");

        var sampleCategory = await _db.Samples.Where(s => s.Id == order.SampleId).Select(s => s.Category).FirstAsync();
        if (sampleCategory is SampleCategory.EnvironmentalMonitoring or SampleCategory.AfterCleaning)
        {
            var hasLocations = await _db.SampleLocations.AnyAsync(l => l.TestOrderId == testOrderId);
            if (!hasLocations)
                throw new InvalidOperationException("Preparation not complete - no locations assigned to this test.");
        }

        var alreadyOpen = await _db.Incubations.AnyAsync(i => i.TestOrderId == testOrderId && i.StepName == stepName && i.CompletedAt == null);
        if (alreadyOpen)
            throw new InvalidOperationException($"Media has already been selected for step \"{stepName}\" - awaiting its result.");

        var media = await _db.Media.Include(m => m.MediaType).FirstOrDefaultAsync(m => m.Id == mediaLotId)
            ?? throw new InvalidOperationException($"Media lot {mediaLotId} not found.");

        // The step template IS the approved specification for this test -
        // Temperature/Duration below are hard-locked from it, never from
        // the picked Media's own MediaType record.
        if (media.MediaTypeId != step.MediaTypeId)
            throw new InvalidOperationException(
                $"This step requires {step.MediaType!.Class} media. The selected lot is {media.MediaType!.Class}.");

        var startedAt = DateTime.UtcNow;
        var incubation = new Incubation
        {
            TestOrderId = testOrderId,
            StepName = stepName,
            MediaId = mediaLotId,
            IncubatorEquipmentId = incubatorEquipmentId,
            Temperature = $"{step.TemperatureMin}-{step.TemperatureMax} °C",
            Duration = $"{step.IncubationMinHours}-{step.IncubationMaxHours} hours",
            StartedAt = startedAt,
            ExpectedReadingAt = startedAt.AddHours(step.IncubationMaxHours)
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

        return incubation;
    }

    public async Task<TestWorkflowResult> RecordResultAsync(int testOrderId, string stepName, ResultPayload payload, int userId)
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

        var openIncubation = await _db.Incubations
            .Where(i => i.TestOrderId == testOrderId && i.StepName == stepName && i.CompletedAt == null)
            .OrderByDescending(i => i.StartedAt)
            .FirstOrDefaultAsync()
            ?? throw new InvalidOperationException($"Media must be selected for step \"{stepName}\" before a result can be recorded.");

        string outcomeSummary;
        decimal? average = null, calculatedResult = null;
        string? status = null;
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

        openIncubation.CompletedAt = DateTime.UtcNow;
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

        if (!step.IsFinalStep)
            return new TestWorkflowResult(outcomeSummary, true, false, null, average, calculatedResult, status);

        // Final step - CountTest already wrote its own Result row above;
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
        // are one logical operation, but a real DB transaction here isn't
        // viable: the whole test suite runs on EF Core's InMemory
        // provider, which throws on BeginTransactionAsync by default, and
        // no other code path in this codebase uses an explicit
        // transaction either. This matches the same "sequential saves,
        // not fully atomic" pattern WorkflowStateMachine.TransitionAsync
        // already uses for the result-then-transition sequence above.
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
            .Where(l => l.TestOrderId == testOrderId)
            .ToListAsync();

    // Finds the single currently-open incubation for a batch TestOrder
    // (there is never more than one at a time) and the step template it
    // belongs to - shared by CloseCurrentIncubationWindowAsync and
    // RecordBatchResultsAsync, both of which need to know whether that
    // window is the chain's final one and when its minimum duration ends.
    private async Task<(Incubation incubation, TestWorkflowStep step)> LoadOpenBatchWindowAsync(int testOrderId, TestDefinition definition)
    {
        var openIncubation = await _db.Incubations
            .Where(i => i.TestOrderId == testOrderId && i.CompletedAt == null)
            .OrderByDescending(i => i.StartedAt)
            .FirstOrDefaultAsync()
            ?? throw new InvalidOperationException("No incubation window is currently open.");

        var step = definition.Steps.FirstOrDefault(s => s.StepName == openIncubation.StepName)
            ?? throw new InvalidOperationException($"Step \"{openIncubation.StepName}\" is not part of the workflow template for this test.");

        return (openIncubation, step);
    }

    private static void RequireMinimumDurationElapsed(Incubation incubation, TestWorkflowStep step)
    {
        var minReadyAt = incubation.StartedAt.AddHours((double)step.IncubationMinHours);
        if (DateTime.UtcNow < minReadyAt)
            throw new InvalidOperationException(
                $"This incubation window needs at least {step.IncubationMinHours} hours - not ready until {minReadyAt:yyyy-MM-dd HH:mm} UTC.");
    }

    // EM/After Cleaning multi-window incubation - closes the currently
    // open (non-final) window once its minimum duration has elapsed.
    // Opening the NEXT window is then just a normal SelectMediaAsync call
    // for that step's name, reusing all of its existing validation
    // (media-type match, etc.) rather than duplicating it here.
    public async Task<Incubation> CloseCurrentIncubationWindowAsync(int testOrderId, int userId)
    {
        var (order, definition) = await LoadWithTemplateAsync(testOrderId);
        var (incubation, step) = await LoadOpenBatchWindowAsync(testOrderId, definition);

        if (step.IsFinalStep)
            throw new InvalidOperationException("This is the final incubation window - record results instead of advancing.");

        RequireMinimumDurationElapsed(incubation, step);

        incubation.CompletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return incubation;
    }

    // EM/After Cleaning batch result entry - one CFU value per location,
    // all sharing one dilution factor, submitted together once the
    // chain's FINAL incubation window has met its minimum duration.
    // Bypasses the step-template machinery for recording (no
    // CountTestReading is written) but still validates against it to
    // make sure every non-final window was actually closed first.
    public async Task<TestWorkflowResult> RecordBatchResultsAsync(int testOrderId, decimal dilutionFactor, List<BatchLocationResult> locations, int userId)
    {
        var (order, definition) = await LoadWithTemplateAsync(testOrderId);
        if (order.CurrentStep != WorkflowStep.Incubating)
            throw new InvalidOperationException("Media must be selected for this test before batch results can be recorded.");

        var (openIncubation, step) = await LoadOpenBatchWindowAsync(testOrderId, definition);
        if (!step.IsFinalStep)
            throw new InvalidOperationException($"\"{step.StepName}\" is not the final incubation window yet - close it and start the next window first.");

        RequireMinimumDurationElapsed(openIncubation, step);

        if (dilutionFactor <= 0)
            throw new InvalidOperationException("Dilution factor must be greater than zero.");

        var sampleLocations = await GetLocationsAsync(testOrderId);
        if (sampleLocations.Count == 0)
            throw new InvalidOperationException("No locations are assigned to this test order.");

        var submitted = locations.ToDictionary(l => l.SampleLocationId);
        var missing = sampleLocations.Where(l => !submitted.ContainsKey(l.Id)).ToList();
        if (missing.Count > 0)
            throw new InvalidOperationException($"Results are missing for: {string.Join(", ", missing.Select(LocationName))}.");

        var worstStatus = "WithinLimits";
        var conformCount = 0;
        foreach (var location in sampleLocations)
        {
            var cfu = submitted[location.Id].CFUResult;
            var calculated = cfu * dilutionFactor;
            var reported = calculated < 1 ? "<1" : Math.Round(calculated).ToString("0");

            var alertLimit = location.RoomTestConfiguration?.AlertLimit ?? location.MachinePartConfiguration?.AlertLimit;
            var actionLimit = location.RoomTestConfiguration?.ActionLimit ?? location.MachinePartConfiguration?.ActionLimit;
            var specLimit = location.RoomTestConfiguration?.SpecLimit ?? location.MachinePartConfiguration?.SpecLimit;
            var (status, _) = Compare(calculated, alertLimit, actionLimit, specLimit);

            location.DilutionFactor = dilutionFactor;
            location.CFUResult = cfu;
            location.CalculatedResult = calculated;
            location.ReportedResult = reported;
            location.AlertLimit = alertLimit;
            location.ActionLimit = actionLimit;
            location.SpecLimit = specLimit;
            location.Status = status;
            location.EnteredAt = DateTime.UtcNow;
            location.EnteredByUserId = userId;

            if (status == "WithinLimits") conformCount++;
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

        openIncubation.CompletedAt = DateTime.UtcNow;
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

    // EM/After Cleaning batch pathogen result entry - the final step's
    // per-location Detected/Absent call, once its minimum duration has
    // elapsed (same window mechanism as RecordBatchResultsAsync; every
    // intermediate step was just a shared incubation window closed via
    // CloseCurrentIncubationWindowAsync, no per-location judgment call).
    public async Task<TestWorkflowResult> RecordBatchPathogenResultsAsync(
        int testOrderId, List<BatchLocationObservation>? observations, int userId)
    {
        var (order, definition) = await LoadWithTemplateAsync(testOrderId);
        if (order.CurrentStep != WorkflowStep.Incubating)
            throw new InvalidOperationException("Media must be selected for this test before batch results can be recorded.");

        var (openIncubation, step) = await LoadOpenBatchWindowAsync(testOrderId, definition);
        if (!step.IsFinalStep)
            throw new InvalidOperationException($"\"{step.StepName}\" is not the final incubation window yet - close it and start the next window first.");

        RequireMinimumDurationElapsed(openIncubation, step);

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
            location.Status = growth ? "Detected" : "Absent";
            location.ReportedResult = location.Status;
            location.EnteredAt = DateTime.UtcNow;
            location.EnteredByUserId = userId;
            if (growth) detectedCount++;
        }

        var summary = $"{sampleLocations.Count} locations: {sampleLocations.Count - detectedCount} absent, {detectedCount} detected";
        var overallResult = detectedCount > 0 ? "Detected" : "Absent";

        _db.Results.Add(new Result
        {
            TestOrderId = testOrderId,
            RawValue = summary,
            InterpretedValue = $"{summary} (overall: {overallResult})",
            Type = ResultType.Interpretive,
            EnteredByUserId = userId
        });

        openIncubation.CompletedAt = DateTime.UtcNow;
        openIncubation.Outcome = summary;

        foreach (var location in sampleLocations)
            await _resultProjection.UpsertFromSampleLocationAsync(location.Id);

        await WorkflowStateMachine.TransitionAsync(_db, order, WorkflowStep.Ready, userId, $"Batch pathogen results recorded: {summary}");
        await _sampleReviewService.AutoSubmitForReviewIfReadyAsync(order.SampleId, userId);
        await _db.SaveChangesAsync();

        return new TestWorkflowResult(summary, true, true, overallResult, null, null, overallResult);
    }

    private static string LocationName(SampleLocation l) =>
        l.RoomTestConfiguration?.Room?.Name ?? l.MachinePartConfiguration?.MachinePart?.Name ?? $"Location {l.Id}";

    private static int StatusSeverity(string status) => status switch
    {
        "OutOfSpecification" => 3,
        "ActionLimitExceeded" => 2,
        "AlertLimitExceeded" => 1,
        _ => 0
    };

    private async Task<(string reported, decimal average, decimal calculated, string status, CountTestReading reading)> RecordCountTestAsync(TestOrder order, TestWorkflowStep step, CountTestPayload payload, int userId)
    {
        if (payload.PlateReadings.Count == 0)
            throw new InvalidOperationException("At least one plate reading is required to calculate an average.");

        var sample = await _db.Samples.FirstOrDefaultAsync(s => s.Id == order.SampleId)
            ?? throw new InvalidOperationException("Sample not found for this test order.");

        var average = payload.PlateReadings.Average();
        var calculated = average * payload.DilutionFactor;
        var reported = calculated < 1 ? "<1" : Math.Round(calculated).ToString("0");

        string? alertLimit = null, actionLimit = null, specLimit = null;
        if (sample.ItemId is not null)
        {
            var spec = await _db.Specifications.FirstOrDefaultAsync(s => s.ItemId == sample.ItemId && s.TestCode == order.TestCode);
            alertLimit = spec?.AlertLimit; actionLimit = spec?.ActionLimit; specLimit = spec?.SpecLimit;
        }
        else if (sample.WaterSamplingPointId is not null)
        {
            var config = await _db.SamplingConfigurations.FirstOrDefaultAsync(c => c.TestCode == order.TestCode && c.WaterSamplingPointId == sample.WaterSamplingPointId);
            alertLimit = config?.AlertLimit; actionLimit = config?.ActionLimit; specLimit = config?.SpecLimit;
        }

        var (status, _) = Compare(calculated, alertLimit, actionLimit, specLimit);

        var reading = new CountTestReading
        {
            TestOrderId = order.Id,
            StepName = step.StepName,
            PlateReadings = string.Join(",", payload.PlateReadings),
            DilutionFactor = payload.DilutionFactor,
            Average = average,
            CalculatedResult = calculated,
            ReportedResult = reported,
            AlertLimit = alertLimit,
            ActionLimit = actionLimit,
            SpecLimit = specLimit,
            Status = status,
            EnteredByUserId = userId
        };
        _db.CountTestReadings.Add(reading);

        _db.Results.Add(new Result
        {
            TestOrderId = order.Id,
            RawValue = string.Join(",", payload.PlateReadings),
            InterpretedValue = $"{reported} CFU ({status})",
            Type = ResultType.Numeric,
            EnteredByUserId = userId
        });

        return (reported, average, calculated, status, reading);
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
            ? (growthObserved ? "Detected" : "Absent")
            : (growthObserved ? "Growth" : "No Growth");
    }

    // Same Spec -> Action -> Alert precedence (most severe first) as
    // WaterWorkflowEngine.Compare/CountTestWorkflowEngine.Compare.
    private static (string status, string? exceeded) Compare(decimal value, string? alert, string? action, string? spec)
    {
        if (decimal.TryParse(spec, out var specLimit) && value > specLimit)
            return ("OutOfSpecification", "Specification");
        if (decimal.TryParse(action, out var actionLimit) && value > actionLimit)
            return ("ActionLimitExceeded", "Action");
        if (decimal.TryParse(alert, out var alertLimit) && value > alertLimit)
            return ("AlertLimitExceeded", "Alert");
        return ("WithinLimits", null);
    }

    // Resolves the step template by name and guards workflow order,
    // reusing the existing order-violation message.
    private async Task<TestWorkflowStep> LoadStepAsync(int testOrderId, string stepName)
    {
        var order = await _db.TestOrders.FirstOrDefaultAsync(t => t.Id == testOrderId)
            ?? throw new InvalidOperationException($"Test order {testOrderId} not found.");
        var test = await _db.TestDefinitions.Include(t => t.Steps).FirstOrDefaultAsync(t => t.Code == order.TestCode)
            ?? throw new InvalidOperationException($"No test definition for {order.TestCode}.");
        return test.Steps.FirstOrDefault(s => s.StepName == stepName)
            ?? throw new InvalidOperationException($"Step '{stepName}' is not part of {order.TestCode}.");
    }

    private async Task<TestWorkflowStepMedia> LoadStepMediumAsync(int stepId, int materialId)
    {
        return await _db.TestWorkflowStepMedias
            .FirstOrDefaultAsync(m => m.TestWorkflowStepId == stepId && m.MaterialId == materialId)
            ?? throw new WorkflowStepException(WorkflowErrorCodes.MediaNotInPermittedList,
                "That medium is not on this step's permitted list.");
    }

    private async Task RequireEligibleIncubatorAsync(int stepMediaId, int equipmentId)
    {
        if (!await _incubatorEligibility.IsWithinRangeAsync(stepMediaId, equipmentId))
            throw new WorkflowStepException(WorkflowErrorCodes.IncubatorTempOutOfRange,
                "The selected incubator's set point is outside this medium's temperature range.");
    }

    private static void RequireIncubationComplete(DateTime incubationEndUtc)
    {
        var remaining = (long)Math.Ceiling((incubationEndUtc - DateTime.UtcNow).TotalSeconds);
        if (remaining > 0)
            throw new WorkflowStepException(WorkflowErrorCodes.IncubationNotComplete,
                "This step's incubation period has not finished yet.", remaining);
    }

    // The lot the analyst picked must be a released lot of the permitted
    // material and of the class the step template locks the step to.
    private async Task<Media> LoadReleasedLotAsync(int mediaLotId, int materialId, int mediaTypeId)
    {
        var lot = await _db.Media.FirstOrDefaultAsync(m => m.Id == mediaLotId)
            ?? throw new InvalidOperationException($"Media lot {mediaLotId} not found.");
        if (!lot.IsReleasedForUse)
            throw new InvalidOperationException($"Media lot {lot.LotNumber} has not been released for use.");
        if (lot.MaterialId != materialId)
            throw new WorkflowStepException(WorkflowErrorCodes.MediaNotInPermittedList,
                $"Media lot {lot.LotNumber} is not a lot of the permitted medium for this step.");
        if (lot.MediaTypeId != mediaTypeId)
            throw new InvalidOperationException($"Media lot {lot.LotNumber} is the wrong media class for this step.");
        return lot;
    }

    // Broth steps carry no result logic - completion is the incubation
    // window elapsing plus the analyst submitting the form. The free-text
    // observation is recorded but never branches the workflow.
    public async Task<StepResultDto> SubmitBrothAsync(
        int testOrderId, string stepName, int mediaLotId, int equipmentId,
        DateTime incubationStartUtc, DateTime incubationEndUtc, string? observation, int userId)
    {
        var step = await LoadStepAsync(testOrderId, stepName);
        if (step.StepType is not (StepType.BrothEnrichment or StepType.SelectiveBroth))
            throw new InvalidOperationException($"Step '{stepName}' is not a broth step.");

        var stepMedium = step.StepMedia.Count == 1
            ? step.StepMedia[0]
            : await _db.TestWorkflowStepMedias.FirstOrDefaultAsync(m => m.TestWorkflowStepId == step.Id)
                ?? throw new InvalidOperationException($"Step '{stepName}' has no assigned medium.");

        var lot = await LoadReleasedLotAsync(mediaLotId, stepMedium.MaterialId, step.MediaTypeId);
        await RequireEligibleIncubatorAsync(stepMedium.Id, equipmentId);
        RequireIncubationComplete(incubationEndUtc);

        var incubation = new Incubation
        {
            TestOrderId = testOrderId, StepNumber = step.StepOrder, StepName = step.StepName,
            MediaId = lot.Id, IncubatorEquipmentId = equipmentId,
            Temperature = $"{step.TemperatureMin}-{step.TemperatureMax}",
            Duration = $"{step.IncubationMinHours}-{step.IncubationMaxHours}h",
            IncubationStartUtc = incubationStartUtc, IncubationEndUtc = incubationEndUtc,
            ExpectedReadingAt = incubationEndUtc,
            CompletedAt = DateTime.UtcNow, Outcome = observation
        };
        _db.Incubations.Add(incubation);
        await _db.SaveChangesAsync();

        var result = new WorkflowStepResult
        {
            IncubationId = incubation.Id, TestOrderId = testOrderId,
            StepName = step.StepName, StepType = step.StepType,
            SubmittedByUserId = userId, SubmittedAtUtc = DateTime.UtcNow
        };
        _db.WorkflowStepResults.Add(result);
        await _db.SaveChangesAsync();

        return new StepResultDto(incubation.Id, step.StepType.ToString(), "Complete",
            userId, result.SubmittedAtUtc, NextStepUnlocked: true, WorkflowFinalResult: null, Flags: new List<string>());
    }

    // Growth that is absent or does not match the expected appearance
    // means the organism being sought is not there - the workflow ends
    // as NotDetected without running confirmatory plating.
    public async Task<StepResultDto> SubmitSelectivePlatingAsync(
        int testOrderId, string stepName, int mediaLotId, int equipmentId,
        DateTime incubationStartUtc, DateTime incubationEndUtc, GrowthObservation observation, int userId)
    {
        var step = await LoadStepAsync(testOrderId, stepName);
        if (step.StepType != StepType.SelectivePlating)
            throw new InvalidOperationException($"Step '{stepName}' is not a selective plating step.");

        var stepMedium = await _db.TestWorkflowStepMedias.FirstOrDefaultAsync(m => m.TestWorkflowStepId == step.Id)
            ?? throw new InvalidOperationException($"Step '{stepName}' has no assigned medium.");
        var lot = await LoadReleasedLotAsync(mediaLotId, stepMedium.MaterialId, step.MediaTypeId);
        await RequireEligibleIncubatorAsync(stepMedium.Id, equipmentId);

        var incubation = new Incubation
        {
            TestOrderId = testOrderId, StepNumber = step.StepOrder, StepName = step.StepName,
            MediaId = lot.Id, IncubatorEquipmentId = equipmentId,
            Temperature = $"{step.TemperatureMin}-{step.TemperatureMax}",
            Duration = $"{step.IncubationMinHours}-{step.IncubationMaxHours}h",
            IncubationStartUtc = incubationStartUtc, IncubationEndUtc = incubationEndUtc,
            ExpectedReadingAt = incubationEndUtc, CompletedAt = DateTime.UtcNow,
            Outcome = observation.ToString()
        };
        _db.Incubations.Add(incubation);
        await _db.SaveChangesAsync();

        // Snapshot taken at submission, never afterwards (ALCOA+).
        var snapshot = await _appearanceSnapshot.GetExpectedAppearanceSnapshotAsync(
            stepMedium.MaterialId, step.TargetOrganismId!.Value);

        var result = new WorkflowStepResult
        {
            IncubationId = incubation.Id, TestOrderId = testOrderId,
            StepName = step.StepName, StepType = step.StepType,
            SelectivePlatingObservation = observation, ExpectedAppearanceSnapshot = snapshot,
            SubmittedByUserId = userId, SubmittedAtUtc = DateTime.UtcNow
        };
        _db.WorkflowStepResults.Add(result);

        _db.PathogenObservations.Add(new PathogenObservation
        {
            TestOrderId = testOrderId, StepName = step.StepName, StepOrder = step.StepOrder,
            Observation = observation, ObservedByUserId = userId, MediaId = lot.Id
        });
        await _db.SaveChangesAsync();

        if (observation == GrowthObservation.GrowthConforming)
            return new StepResultDto(incubation.Id, step.StepType.ToString(), "Complete",
                userId, result.SubmittedAtUtc, NextStepUnlocked: true, WorkflowFinalResult: null, Flags: new List<string>());

        await FinalizeWorkflowAsync(testOrderId, "NotDetected", userId);
        return new StepResultDto(incubation.Id, step.StepType.ToString(), "Complete",
            userId, result.SubmittedAtUtc, NextStepUnlocked: false, WorkflowFinalResult: "NotDetected", Flags: new List<string>());
    }

    // One exit point for a finished pathogen workflow: write the Result
    // row, project it, move the order to Ready, and let the existing
    // sample review service decide whether the sample can now be
    // submitted for review.
    private async Task FinalizeWorkflowAsync(int testOrderId, string finalResult, int userId)
    {
        var order = await _db.TestOrders.FirstOrDefaultAsync(t => t.Id == testOrderId)
            ?? throw new InvalidOperationException($"Test order {testOrderId} not found.");

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

    public async Task<WorkflowStep> AdvanceAsync(int testOrderId, int performedByUserId, string? note = null)
    {
        var order = await WorkflowStateMachine.LoadOrThrowAsync(_db, testOrderId);
        var errors = await ValidateAsync(testOrderId);
        if (errors.Count > 0) throw new InvalidOperationException(string.Join(" ", errors));

        var next = order.CurrentStep switch
        {
            WorkflowStep.Waiting => WorkflowStep.Incubating,
            WorkflowStep.Incubating => WorkflowStep.Ready,
            WorkflowStep.Ready => WorkflowStep.Reviewed,
            WorkflowStep.Reviewed => WorkflowStep.Approved,
            _ => order.CurrentStep
        };
        return await WorkflowStateMachine.TransitionAsync(_db, order, next, performedByUserId, note);
    }

    public async Task<List<string>> ValidateAsync(int testOrderId)
    {
        var order = await WorkflowStateMachine.LoadOrThrowAsync(_db, testOrderId);
        var errors = new List<string>();

        if (order.CurrentStep is WorkflowStep.Waiting or WorkflowStep.Incubating)
        {
            var current = await GetCurrentStepAsync(testOrderId);
            if (!current.AllStepsComplete)
                errors.Add("Not all workflow steps are complete for this test yet.");
        }

        return errors;
    }

    public async Task<bool> IsCompleteAsync(int testOrderId)
    {
        var order = await WorkflowStateMachine.LoadOrThrowAsync(_db, testOrderId);
        return order.CurrentStep == WorkflowStep.Approved;
    }
}
