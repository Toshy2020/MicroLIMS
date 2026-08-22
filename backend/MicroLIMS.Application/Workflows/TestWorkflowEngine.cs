using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Infrastructure.Notifications;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Constants;

namespace MicroLIMS.Application.Workflows;

// Result payload union for RecordResultAsync - which record is passed
// depends on the TestDefinition's WorkflowType (CountTest) or plain
// Observation otherwise.
public abstract record ResultPayload;
public sealed record CountTestPayload(List<string> RawPlateReadings, decimal DilutionFactor) : ResultPayload
{
    public CountTestPayload(List<decimal> plateReadings, decimal dilutionFactor)
        : this(plateReadings.Select(p => p.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToList(), dilutionFactor)
    {
    }
}
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

public record ConfirmatorySelectionInput(int StepMediaId, int MediaLotId, int EquipmentId);
public record ConfirmatoryObservationInput(int MaterialId, GrowthObservation Observation);
public record ConfirmatoryOutcomeDto(int StepInstanceId, string ConfirmatoryResult, bool AnalystDecisionRequired, List<string> Flags);

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

// One location's plate readings submitted from WaterLocationResultGridDialog -
// water batch results only. Averaged directly with no dilution factor,
// unlike BatchLocationResult's CFU x dilution model (EM/After Cleaning).
public record WaterBatchLocationReadings(int SampleLocationId, List<decimal> Readings);

// EM/After Cleaning batch pathogen results - the final step's per-
// location growth observation call.
public record BatchLocationObservation(int SampleLocationId, bool GrowthObserved);

public record SiblingPathogenOrderDto(int TestOrderId, string PathogenName, string TestCode);

public interface ITestWorkflowEngine : IStatefulWorkflowEngine
{
    Task<CurrentStepResult> GetCurrentStepAsync(int testOrderId);
    Task<List<SiblingPathogenOrderDto>> GetSiblingPathogenOrdersAsync(int testOrderId, CancellationToken ct = default);
    Task PropagateSharedTsbToSiblingOrdersAsync(int testOrderId, int incubationId, int userId, CancellationToken ct = default);
    Task<Incubation> SelectMediaAsync(int testOrderId, string stepName, int mediaLotId, int incubatorEquipmentId, int userId);
    Task<Incubation> StartStage2IncubationAsync(int testOrderId, string stepName, int incubatorEquipmentId, int userId);
    Task<TestWorkflowResult> RecordResultAsync(int testOrderId, string stepName, ResultPayload payload, int userId);
    Task<List<SampleLocation>> GetLocationsAsync(int testOrderId);
    Task<Incubation> CloseCurrentIncubationWindowAsync(int testOrderId, int userId);
    Task<TestWorkflowResult> RecordBatchResultsAsync(int testOrderId, decimal dilutionFactor, List<BatchLocationResult> locations, int userId);
    Task<TestWorkflowResult> RecordWaterBatchReadingsAsync(int testOrderId, List<WaterBatchLocationReadings> locations, int userId);
    Task<TestWorkflowResult> RecordBatchPathogenResultsAsync(int testOrderId, List<BatchLocationObservation>? observations, int userId);

    // Broth steps carry no result logic - completion is the incubation
    // window elapsing plus the analyst submitting the form. The window
    // is server-controlled from Test Master and recorded when media is selected.
    Task<StepResultDto> SubmitBrothAsync(int testOrderId, string stepName, string? observation, int userId);

    Task<Incubation> StartSelectivePlatingIncubationAsync(int testOrderId, string stepName, int mediaLotId, int equipmentId,
        DateTime? incubationStartUtc, int userId);

    Task<StepResultDto> SubmitSelectivePlatingObservationAsync(int testOrderId, string stepName, GrowthObservation observation,
        string? observedAppearanceNote, int userId);

    [Obsolete("Use StartSelectivePlatingIncubationAsync followed by SubmitSelectivePlatingObservationAsync.")]
    Task<StepResultDto> SubmitSelectivePlatingAsync(int testOrderId, string stepName, int mediaLotId, int equipmentId,
        DateTime incubationStartUtc, DateTime incubationEndUtc, GrowthObservation observation, int userId);

    Task<StepResultDto> SubmitConfirmatorySetupAsync(int testOrderId, string stepName,
        IReadOnlyList<ConfirmatorySelectionInput> selections, DateTime incubationStartUtc, DateTime incubationEndUtc, int userId);

    Task<ConfirmatoryOutcomeDto> SubmitConfirmatoryObservationsAsync(int testOrderId, string stepName,
        IReadOnlyList<ConfirmatoryObservationInput> observations, int userId);

    Task<StepResultDto> RecordAnalystDecisionAsync(int testOrderId, AnalystDecision decision, int userId);

    Task<StepResultDto> SubmitBiochemicalAsync(int testOrderId, string stepName, string biochemicalResultText, int? attachmentId, int userId);

    Task<StepResultDto> RecordBiochemicalReviewDecisionAsync(int workflowStepResultId, bool approve, string comment, int reviewerUserId);
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
    private readonly SegregationOfDutiesGuard _sodGuard;
    private readonly ReviewGateService _reviewGate;
    private readonly INotificationService _notifications;

    public TestWorkflowEngine(
        MicroLimsDbContext db, SampleReviewService sampleReviewService, ResultProjectionService resultProjection,
        IncubatorEligibilityService incubatorEligibility, MediaAppearanceSnapshotService appearanceSnapshot,
        SegregationOfDutiesGuard sodGuard, ReviewGateService reviewGate, INotificationService notifications)
    {
        _db = db;
        _sampleReviewService = sampleReviewService;
        _resultProjection = resultProjection;
        _incubatorEligibility = incubatorEligibility;
        _appearanceSnapshot = appearanceSnapshot;
        _sodGuard = sodGuard;
        _reviewGate = reviewGate;
        _notifications = notifications;
    }

    private async Task<(TestOrder order, TestDefinition definition)> LoadWithTemplateAsync(int testOrderId)
    {
        var order = await WorkflowStateMachine.LoadOrThrowAsync(_db, testOrderId);
        var definition = await _db.TestDefinitions
            .Include(t => t.Steps).ThenInclude(s => s.MediaType)
            .Include(t => t.Steps).ThenInclude(s => s.IncubationStages)
            .FirstOrDefaultAsync(t => t.Code == order.TestCode)
            ?? throw new InvalidOperationException($"Test code \"{order.TestCode}\" has no workflow template configured in Test Master.");

        if (definition.Steps.Count == 0)
            throw new InvalidOperationException($"Test code \"{order.TestCode}\" has no workflow steps configured yet - add them in Test Master.");

        return (order, definition);
    }

    // The five pathogen step types record their completion as a
    // WorkflowStepResult row, not a PathogenObservation - only
    // SubmitSelectivePlatingAsync writes an observation, so a
    // PathogenObservations-only "done" test can never see a broth,
    // confirmatory or biochemical step finish. Legacy Observation-type
    // steps (StepType.PlateCount aside, anything driven through
    // RecordResultAsync) still go through PathogenObservations.
    private static bool IsPathogenStepType(StepType stepType) =>
        stepType is StepType.BrothEnrichment or StepType.SelectiveBroth or StepType.SelectivePlating
            or StepType.ConfirmatoryPlating or StepType.BiochemicalTest;

    // A step is "done" once a definitive result has been recorded for
    // it: a CountTestReading for CountTest workflows (always one step),
    // a WorkflowStepResult for a pathogen step, or any observation for a
    // plain Observation step.
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
        {
            if (step.RequiresIncubationTransfer)
            {
                return await _db.Incubations.AnyAsync(i =>
                    i.TestOrderId == testOrderId && i.StepName == step.StepName && i.StageNumber == 2 && i.CompletedAt != null);
            }

            return await _db.Incubations.AnyAsync(i => i.TestOrderId == testOrderId && i.StepName == step.StepName && i.CompletedAt != null);
        }

        if (workflowType == WorkflowType.CountTest)
            return await _db.CountTestReadings.AnyAsync(r => r.TestOrderId == testOrderId && r.StepName == step.StepName);

        if (IsPathogenStepType(step.StepType))
        {
            // Confirmatory plating is the one pathogen step whose result
            // row is written in two passes - setup first, plate readings
            // afterwards. It is only done once the readings have produced
            // a ConfirmatoryResult; treating the setup row as "done"
            // would push the chain past the step the analyst still has to
            // read out.
            if (step.StepType == StepType.ConfirmatoryPlating)
                return await _db.WorkflowStepResults.AnyAsync(r =>
                    r.TestOrderId == testOrderId && r.StepName == step.StepName && r.ConfirmatoryResult != null);

            return await _db.WorkflowStepResults.AnyAsync(r => r.TestOrderId == testOrderId && r.StepName == step.StepName);
        }

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

        if (!media.IsReleasedForUse || media.Status == MediaStatus.OutOfStock || media.Status == MediaStatus.QuarantineFailed)
            throw new InvalidOperationException($"Media lot \"{media.LotNumber}\" is not released for use, out of stock, or rejected.");

        if (step.StepType is StepType.BrothEnrichment or StepType.SelectiveBroth)
        {
            var hasStepMedia = await _db.TestWorkflowStepMedias.AnyAsync(m => m.TestWorkflowStepId == step.Id);
            if (!hasStepMedia)
                throw new InvalidOperationException($"Step \"{stepName}\" has no media configured in Test Master.");
        }

        // The step template IS the approved specification for this test -
        // Temperature/Duration below are hard-locked from it, never from
        // the picked Media's own MediaType record.
        if (media.MediaTypeId != step.MediaTypeId)
            throw new InvalidOperationException(
                $"This step requires {step.MediaType!.Class} media. The selected lot is {media.MediaType!.Class}.");

        var startedAt = DateTime.UtcNow;
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
            Temperature = $"{step.TemperatureMin}-{step.TemperatureMax} °C",
            Duration = $"{step.IncubationMinHours}-{step.IncubationMaxHours} hours",
            StartedAt = startedAt,
            IncubationStartUtc = startedAt,
            IncubationEndUtc = startedAt.AddHours(step.IncubationMaxHours),
            ExpectedReadingAt = startedAt.AddHours(step.IncubationMaxHours),
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

        if (step.StepType == StepType.BrothEnrichment || step.StepName.Contains("TSB", StringComparison.OrdinalIgnoreCase))
        {
            await PropagateSharedTsbToSiblingOrdersAsync(testOrderId, incubation.Id, userId);
        }

        return incubation;
    }

    public async Task<List<SiblingPathogenOrderDto>> GetSiblingPathogenOrdersAsync(int testOrderId, CancellationToken ct = default)
    {
        var testOrder = await _db.TestOrders
            .FirstOrDefaultAsync(t => t.Id == testOrderId, ct)
            ?? throw new InvalidOperationException($"Test order #{testOrderId} not found.");

        var siblings = await _db.TestOrders
            .Include(t => t.Sample)
            .Where(t => t.SampleId == testOrder.SampleId && t.Id != testOrderId && t.Status != ApprovalStatus.Approved && !t.IsSuperseded)
            .ToListAsync(ct);

        var testCodes = siblings.Select(t => t.TestCode).Distinct().ToList();
        var testDefs = await _db.TestDefinitions
            .Include(t => t.Steps)
            .Where(t => testCodes.Contains(t.Code))
            .ToDictionaryAsync(t => t.Code, ct);

        var result = new List<SiblingPathogenOrderDto>();
        foreach (var sib in siblings)
        {
            if (testDefs.TryGetValue(sib.TestCode, out var def))
            {
                var requiresBroth = def.Steps.Any(s =>
                    s.StepType == StepType.BrothEnrichment ||
                    s.StepName.Contains("TSB", StringComparison.OrdinalIgnoreCase));

                if (requiresBroth)
                {
                    result.Add(new SiblingPathogenOrderDto(
                        sib.Id,
                        def.DisplayName ?? sib.TestCode,
                        sib.TestCode));
                }
            }
        }

        return result;
    }

    public async Task PropagateSharedTsbToSiblingOrdersAsync(
        int testOrderId,
        int incubationId,
        int userId,
        CancellationToken ct = default)
    {
        var sourceOrder = await _db.TestOrders
            .FirstOrDefaultAsync(t => t.Id == testOrderId, ct)
            ?? throw new InvalidOperationException($"Test order #{testOrderId} not found.");

        var incubation = await _db.Incubations
            .Include(i => i.Media)
            .Include(i => i.IncubatorEquipment)
            .FirstOrDefaultAsync(i => i.Id == incubationId, ct)
            ?? throw new InvalidOperationException($"Incubation #{incubationId} not found.");

        var siblings = await _db.TestOrders
            .Where(t => t.SampleId == sourceOrder.SampleId && t.Id != testOrderId && t.Status != ApprovalStatus.Approved && !t.IsSuperseded)
            .ToListAsync(ct);

        if (!siblings.Any()) return;

        var testCodes = siblings.Select(t => t.TestCode).Distinct().ToList();
        var testDefs = await _db.TestDefinitions
            .Include(t => t.Steps)
            .Where(t => testCodes.Contains(t.Code))
            .ToDictionaryAsync(t => t.Code, ct);

        var brothStepName = "Broth Enrichment";
        var mediaLotNumber = incubation.Media?.LotNumber ?? "TSB";
        var incubatorCode = incubation.IncubatorEquipment?.Code ?? "INC";

        foreach (var sibling in siblings)
        {
            if (!testDefs.TryGetValue(sibling.TestCode, out var def)) continue;

            var tsbStep = def.Steps.FirstOrDefault(s =>
                s.StepType == StepType.BrothEnrichment ||
                s.StepName.Contains("TSB", StringComparison.OrdinalIgnoreCase));

            if (tsbStep == null) continue;

            var targetStepName = tsbStep.StepName;

            // Ensure an Incubation row exists for sibling
            var existingInc = await _db.Incubations
                .Where(i => i.TestOrderId == sibling.Id && (i.StepName == targetStepName || i.StepName == brothStepName))
                .OrderByDescending(i => i.StartedAt)
                .FirstOrDefaultAsync(ct);

            int siblingIncId;
            if (existingInc == null)
            {
                var newInc = new Incubation
                {
                    TestOrderId = sibling.Id,
                    StepNumber = tsbStep.StepOrder,
                    StepName = targetStepName,
                    StageNumber = 1,
                    MediaId = incubation.MediaId,
                    IncubatorEquipmentId = incubation.IncubatorEquipmentId,
                    Temperature = incubation.Temperature,
                    Duration = incubation.Duration,
                    StartedAt = incubation.StartedAt,
                    IncubationStartUtc = incubation.IncubationStartUtc,
                    IncubationEndUtc = incubation.IncubationEndUtc,
                    ExpectedReadingAt = incubation.ExpectedReadingAt,
                    WindowReceivedAtUtc = incubation.WindowReceivedAtUtc,
                    CompletedAt = incubation.CompletedAt,
                    Outcome = incubation.Outcome,
                    StartedByUserId = userId
                };
                _db.Incubations.Add(newInc);
                await _db.SaveChangesAsync(ct);
                siblingIncId = newInc.Id;
            }
            else
            {
                if (incubation.CompletedAt.HasValue && !existingInc.CompletedAt.HasValue)
                {
                    existingInc.CompletedAt = incubation.CompletedAt;
                    existingInc.Outcome = incubation.Outcome;
                }
                siblingIncId = existingInc.Id;
            }

            // Ensure WorkflowStepResult exists for sibling
            var existsWsr = await _db.WorkflowStepResults
                .AnyAsync(r => r.TestOrderId == sibling.Id && (r.StepName == targetStepName || r.StepName == brothStepName), ct);

            if (!existsWsr)
            {
                _db.WorkflowStepResults.Add(new WorkflowStepResult
                {
                    TestOrderId = sibling.Id,
                    StepName = targetStepName,
                    StepType = StepType.BrothEnrichment,
                    IncubationId = siblingIncId,
                    IsSharedSessionStep = true,
                    SubmittedByUserId = userId,
                    SubmittedAtUtc = DateTime.UtcNow
                });

                _db.WorkflowHistories.Add(new WorkflowHistory
                {
                    TestOrderId = sibling.Id,
                    FromStep = sibling.CurrentStep,
                    ToStep = sibling.CurrentStep == WorkflowStep.Waiting ? WorkflowStep.Incubating : sibling.CurrentStep,
                    Note = $"Broth enrichment linked to shared TSB (propagated from Test Order #{testOrderId}). Lot: {mediaLotNumber}, Incubator: {incubatorCode}.",
                    PerformedByUserId = userId,
                    Timestamp = DateTime.UtcNow
                });
            }
        }

        await _db.SaveChangesAsync(ct);
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

        var stage1MinReadyAt = openIncubation.IncubationStartUtc!.Value.AddHours(step.IncubationMinHours);
        if (DateTime.UtcNow < stage1MinReadyAt)
            throw new WorkflowStepException(WorkflowErrorCodes.IncubationStage1NotComplete,
                $"Stage 1 incubation for step \"{stepName}\" requires at least {step.IncubationMinHours} hours of incubation - not ready until {stage1MinReadyAt:yyyy-MM-dd HH:mm} UTC.",
                Math.Max(0, (long)Math.Ceiling((stage1MinReadyAt - DateTime.UtcNow).TotalSeconds)));

        var stage2Config = await _db.TestWorkflowStepIncubationStages
            .FirstOrDefaultAsync(s => s.TestWorkflowStepId == step.Id && s.StageNumber == 2)
            ?? throw new InvalidOperationException($"Step \"{stepName}\" has no stage 2 configuration.");

        var startedAt = DateTime.UtcNow;
        if (startedAt < openIncubation.StartedAt)
            throw new InvalidOperationException($"Stage 2 start time ({startedAt:yyyy-MM-dd HH:mm} UTC) cannot precede Stage 1 start time ({openIncubation.StartedAt:yyyy-MM-dd HH:mm} UTC).");

        var endUtc = startedAt.AddHours(stage2Config.IncubationMaxHours);
        RequireValidIncubationWindow(stepName, stage2Config.IncubationMinHours, startedAt, endUtc);

        openIncubation.CompletedAt = startedAt;
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

            RequireIncubationComplete(openIncubation.IncubationEndUtc!.Value);
        }

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

        var isFinalStep = step.IsFinalStep || step.RequiresIncubationTransfer || (step.StepOrder == definition.Steps.Max(s => s.StepOrder));
        if (!isFinalStep)
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
            .Include(l => l.WaterSamplingPoint)
            .Include(l => l.SamplingConfiguration)
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
            ?? throw new InvalidOperationException($"Step \"{openIncubation.StepName}\" is not part of the workflow template for \"{definition.Code}\".");

        return (openIncubation, step);
    }

    private static void RequireMinimumDurationElapsed(Incubation incubation, TestWorkflowStep step)
    {
        var minReadyAt = incubation.StartedAt.AddHours((double)step.IncubationMinHours);
        if (DateTime.UtcNow < minReadyAt)
            throw new InvalidOperationException(
                $"This incubation window needs at least {step.IncubationMinHours} hours - not ready until {minReadyAt:yyyy-MM-dd HH:mm} UTC.");
    }

    private static void RequireStage2MinimumDurationElapsed(Incubation incubation, TestWorkflowStep step)
    {
        var stage2Config = step.IncubationStages.FirstOrDefault(s => s.StageNumber == 2);
        var minHours = stage2Config?.IncubationMinHours ?? step.IncubationMinHours;
        var startUtc = incubation.IncubationStartUtc ?? incubation.StartedAt;
        var minReadyAt = startUtc.AddHours(minHours);
        if (DateTime.UtcNow < minReadyAt)
        {
            var remaining = (long)Math.Ceiling((minReadyAt - DateTime.UtcNow).TotalSeconds);
            throw new WorkflowStepException(WorkflowErrorCodes.IncubationNotComplete,
                $"Stage 2 incubation for step \"{step.StepName}\" requires at least {minHours} hours of incubation - not ready until {minReadyAt:yyyy-MM-dd HH:mm} UTC.",
                remaining);
        }
    }

    public async Task<Incubation> CloseCurrentIncubationWindowAsync(int testOrderId, int userId)
    {
        var (order, definition) = await LoadWithTemplateAsync(testOrderId);
        var (incubation, step) = await LoadOpenBatchWindowAsync(testOrderId, definition);

        if (step.RequiresIncubationTransfer)
            throw new InvalidOperationException("This step requires incubation transfer - start stage 2 incubation instead of advancing window.");

        if (step.IsFinalStep || (step.StepOrder == definition.Steps.Max(s => s.StepOrder) && !step.RequiresIncubationTransfer))
            throw new InvalidOperationException("This is the final incubation window - record results instead of advancing.");

        RequireMinimumDurationElapsed(incubation, step);

        incubation.CompletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return incubation;
    }

    public async Task<TestWorkflowResult> RecordBatchResultsAsync(int testOrderId, decimal dilutionFactor, List<BatchLocationResult> locations, int userId)
    {
        var (order, definition) = await LoadWithTemplateAsync(testOrderId);
        if (order.CurrentStep != WorkflowStep.Incubating)
            throw new InvalidOperationException("Media must be selected for this test before batch results can be recorded.");

        var (openIncubation, step) = await LoadOpenBatchWindowAsync(testOrderId, definition);
        var isFinalIncubation = step.IsFinalStep || step.RequiresIncubationTransfer || (step.StepOrder == definition.Steps.Max(s => s.StepOrder));
        if (!isFinalIncubation)
            throw new InvalidOperationException($"\"{step.StepName}\" is not the final incubation window yet - close it and start the next window first.");

        if (step.RequiresIncubationTransfer)
        {
            if (openIncubation.StageNumber != 2)
                throw new WorkflowStepException(WorkflowErrorCodes.IncubationStage2NotStarted,
                    $"Step \"{step.StepName}\" requires stage 2 incubation to be started before results can be recorded.");

            RequireStage2MinimumDurationElapsed(openIncubation, step);
        }
        else
        {
            RequireMinimumDurationElapsed(openIncubation, step);
        }

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
            location.Unit = DeriveBatchLocationUnit(location);
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

    // Water-only batch result entry: each sampling point gets its own
    // set of raw plate readings, averaged directly (no shared dilution
    // factor) and compared to that point's own SamplingConfiguration
    // limits - the multi-reading model water has always used, now
    // applied per-location instead of per-TestOrder. Everything about
    // opening/closing the incubation window and transitioning the
    // TestOrder is identical to RecordBatchResultsAsync; only the
    // result computation differs.
    public async Task<TestWorkflowResult> RecordWaterBatchReadingsAsync(int testOrderId, List<WaterBatchLocationReadings> locations, int userId)
    {
        var (order, definition) = await LoadWithTemplateAsync(testOrderId);
        if (order.CurrentStep != WorkflowStep.Incubating)
            throw new InvalidOperationException("Media must be selected for this test before batch results can be recorded.");

        var (openIncubation, step) = await LoadOpenBatchWindowAsync(testOrderId, definition);
        var isFinalIncubation = step.IsFinalStep || step.RequiresIncubationTransfer || (step.StepOrder == definition.Steps.Max(s => s.StepOrder));
        if (!isFinalIncubation)
            throw new InvalidOperationException($"\"{step.StepName}\" is not the final incubation window yet - close it and start the next window first.");

        if (step.RequiresIncubationTransfer)
        {
            if (openIncubation.StageNumber != 2)
                throw new WorkflowStepException(WorkflowErrorCodes.IncubationStage2NotStarted,
                    $"Step \"{step.StepName}\" requires stage 2 incubation to be started before results can be recorded.");

            RequireStage2MinimumDurationElapsed(openIncubation, step);
        }
        else
        {
            RequireMinimumDurationElapsed(openIncubation, step);
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

        var worstStatus = "WithinLimits";
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
            location.ReportedResult = average.ToString("0.##");
            location.AlertLimit = alertLimit;
            location.ActionLimit = actionLimit;
            location.SpecLimit = specLimit;
            location.Status = status;
            location.Unit = DeriveBatchLocationUnit(location);
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
        l.RoomTestConfiguration?.Room?.Name ?? l.MachinePartConfiguration?.MachinePart?.Name ?? l.WaterSamplingPoint?.Code ?? $"Location {l.Id}";

    // Batch (SampleLocation) unit derivation for EM/After Cleaning/Water -
    // distinct from GetCfuUnit, which derives the non-batch CountTestReading
    // unit from SamplePreparation.Unit. Batch locations never have a
    // SamplePreparation row (verified: 0 across every Water/EM/AC batch
    // sample), so the unit has to come from what was actually sampled -
    // RoomTestConfiguration.TestType / MachinePartConfiguration.TestType -
    // per QC Microbiology Supervisor sign-off (2026-08-22). Only called for
    // quantitative (CFU) locations; pathogen (Detected/Absent) locations
    // never call this and keep Unit = null.
    private static string DeriveBatchLocationUnit(SampleLocation location) => location switch
    {
        { RoomTestConfiguration.TestType: "PassiveAirSample" } => "CFU/plate/4 hours",
        { RoomTestConfiguration.TestType: "SurfaceAirSample" } => "CFU/25 sq.cm",
        { MachinePartConfiguration.TestType: "Swab" } => "CFU/25 sq.cm",
        { MachinePartConfiguration.TestType: "Rinse" } => "CFU/mL",
        { WaterSamplingPointId: not null } => "CFU/mL",
        _ => throw new InvalidOperationException(
            $"No unit mapping for SampleLocation {location.Id} " +
            $"(RoomTestConfiguration.TestType={location.RoomTestConfiguration?.TestType ?? "-"}, " +
            $"MachinePartConfiguration.TestType={location.MachinePartConfiguration?.TestType ?? "-"}). " +
            "Add it to DeriveBatchLocationUnit rather than guessing.")
    };

    private static int StatusSeverity(string status) => status switch
    {
        "OutOfSpecification" => 3,
        "ActionLimitExceeded" => 2,
        "AlertLimitExceeded" => 1,
        _ => 0
    };

    private async Task<(string reported, decimal? average, decimal? calculated, string status, CountTestReading reading)> RecordCountTestAsync(TestOrder order, TestWorkflowStep step, CountTestPayload payload, int userId)
    {
        if (payload.RawPlateReadings.Count == 0)
            throw new InvalidOperationException("At least one plate reading is required to calculate an average.");

        var sample = await _db.Samples
            .Include(s => s.SamplePreparation)
            .FirstOrDefaultAsync(s => s.Id == order.SampleId)
            ?? throw new InvalidOperationException("Sample not found for this test order.");

        // Force DF = 1 for direct count sample types regardless of client input
        var isDirectCount = sample.Category is
            SampleCategory.Water or
            SampleCategory.EnvironmentalMonitoring or
            SampleCategory.AfterCleaning;

        if (isDirectCount && payload.DilutionFactor != 1m)
        {
            payload = payload with { DilutionFactor = 1m };
        }

        var prepUnit = sample.SamplePreparation?.Unit;
        var unit = GetCfuUnit(sample.Category, prepUnit);

        bool hasNonNumeric = payload.RawPlateReadings
            .Any(r => r.Equals("TNTC", StringComparison.OrdinalIgnoreCase) ||
                      r.Equals("Uncountable", StringComparison.OrdinalIgnoreCase));

        CountTestReading reading;
        string reported;
        decimal? average = null;
        decimal? calculated = null;
        string status;

        if (hasNonNumeric)
        {
            var nonNumericRaw = payload.RawPlateReadings
                .First(r => !decimal.TryParse(r, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out _))
                .Trim();

            var nonNumericValue = nonNumericRaw.Equals("TNTC", StringComparison.OrdinalIgnoreCase) ? "TNTC" : "Uncountable";

            status = "RequiresReview";
            reported = nonNumericValue;

            reading = new CountTestReading
            {
                TestOrderId = order.Id,
                StepName = step.StepName,
                PlateReadings = string.Join(",", payload.RawPlateReadings),
                DilutionFactor = payload.DilutionFactor,
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

            reported = calculated.Value < lowerLimit
                ? $"<{lowerLimit:G29} {unit}"
                : $"{calculated.Value:G29} {unit}";

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

            (status, _) = Compare(calculated.Value, alertLimit, actionLimit, specLimit);

            reading = new CountTestReading
            {
                TestOrderId = order.Id,
                StepName = step.StepName,
                PlateReadings = string.Join(",", payload.RawPlateReadings),
                DilutionFactor = payload.DilutionFactor,
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
            ?? throw new InvalidOperationException($"Test order {testOrderId} not found.");
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
        if (currentStep.StepName != stepName)
            throw new InvalidOperationException(
                $"Workflow order violation: step \"{currentStep.StepName}\" must be completed before \"{stepName}\".");

        return (order, step);
    }

    // A TestOrder at Ready or beyond has had its result reported and (for
    // Reviewed/Approved) signed. Re-submitting a step against it would
    // silently append a second, contradictory result behind the reported
    // one - the same class of falsification B2 blocks for confirmatory
    // re-runs, at order level.
    private static void RequireOrderNotFinalized(TestOrder order)
    {
        if (order.CurrentStep is WorkflowStep.Ready or WorkflowStep.Reviewed or WorkflowStep.Approved)
            throw new InvalidOperationException(
                $"Test order {order.Id} is already at {order.CurrentStep} - its workflow can no longer be submitted against.");
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
        return step.StepMedia.FirstOrDefault()
            ?? await _db.TestWorkflowStepMedias.FirstOrDefaultAsync(m => m.TestWorkflowStepId == step.Id)
            ?? throw new InvalidOperationException(
                $"Step \"{step.StepName}\" has no assigned medium - complete this step's template in Test Master before recording results.");
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

    // RequireIncubationComplete only asks "has the declared end passed?",
    // which a one-second window satisfies as readily as a real 18-24h
    // one - and a window that ends before it starts satisfies it too.
    // The window is analyst-supplied, so it has to be checked against a
    // minimum the same way RequireMinimumDurationElapsed checks a
    // server-recorded window against it.
    //
    // Deliberately no upper bound: over-incubation happens in real labs
    // and is handled by explanation/deviation, not by refusing the
    // record. Under-incubation is the falsification risk.
    private static void RequireValidIncubationWindow(string stepName, int minHours, DateTime incubationStartUtc, DateTime incubationEndUtc)
    {
        if (incubationEndUtc < incubationStartUtc)
            throw new WorkflowStepException(WorkflowErrorCodes.IncubationWindowInvalid,
                $"The incubation window ends before it starts ({incubationStartUtc:yyyy-MM-dd HH:mm} to {incubationEndUtc:yyyy-MM-dd HH:mm} UTC).");

        var declaredHours = (incubationEndUtc - incubationStartUtc).TotalHours;
        if (declaredHours < minHours)
            throw new WorkflowStepException(WorkflowErrorCodes.IncubationWindowTooShort,
                $"Step \"{stepName}\" requires at least {minHours} hours of incubation - " +
                $"the declared window is {declaredHours:0.##} hours.");
    }

    private static void RequireValidIncubationWindow(TestWorkflowStep step, DateTime incubationStartUtc, DateTime incubationEndUtc) =>
        RequireValidIncubationWindow(step.StepName, step.IncubationMinHours, incubationStartUtc, incubationEndUtc);

    // The lot the analyst picked must be a released lot of the permitted
    // material and of the class the step template locks the step to.
    private async Task<Media> LoadReleasedLotAsync(int mediaLotId, int materialId, int mediaTypeId)
    {
        var lot = await _db.Media.FirstOrDefaultAsync(m => m.Id == mediaLotId)
            ?? throw new InvalidOperationException($"Media lot {mediaLotId} not found.");
        if (!lot.IsReleasedForUse || lot.Status == MediaStatus.OutOfStock || lot.Status == MediaStatus.QuarantineFailed)
            throw new InvalidOperationException($"Media lot {lot.LotNumber} is not released for use, out of stock, or rejected.");
        if (lot.MaterialId != materialId)
            throw new WorkflowStepException(WorkflowErrorCodes.MediaNotInPermittedList,
                $"Media lot {lot.LotNumber} is not a lot of the permitted medium for this step.");
        if (lot.MediaTypeId != mediaTypeId)
            throw new InvalidOperationException($"Media lot {lot.LotNumber} is the wrong media class for this step.");
        return lot;
    }

    // Broth steps carry no result logic - completion is the incubation
    // window elapsing plus the analyst submitting the form. The incubation
    // window is server-controlled and recorded when SelectMediaAsync is
    // called; the analyst cannot override it. This method just records
    // that the window has completed and optionally saves an observation.
    public async Task<StepResultDto> SubmitBrothAsync(
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
            i.CompletedAt == null &&
            (string.Equals(i.StepName, stepName, StringComparison.OrdinalIgnoreCase) ||
             (step.StepType == StepType.BrothEnrichment && (i.StepName.Contains("Broth", StringComparison.OrdinalIgnoreCase) || i.StepName.Contains("TSB", StringComparison.OrdinalIgnoreCase)))));

        if (incubation == null)
        {
            // Check if there is an already completed incubation (e.g. from Shared TSB or previous completion)
            var completedInc = incubations.FirstOrDefault(i =>
                string.Equals(i.StepName, stepName, StringComparison.OrdinalIgnoreCase) ||
                (step.StepType == StepType.BrothEnrichment && (i.StepName.Contains("Broth", StringComparison.OrdinalIgnoreCase) || i.StepName.Contains("TSB", StringComparison.OrdinalIgnoreCase))));

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
                        SubmittedAtUtc = DateTime.UtcNow,
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

        // Verify that the minimum duration has elapsed.
        var minReadyAt = incubation.IncubationStartUtc!.Value.AddHours((double)step.IncubationMinHours);
        if (DateTime.UtcNow < minReadyAt)
            throw new WorkflowStepException(WorkflowErrorCodes.IncubationNotComplete,
                $"This step requires at least {step.IncubationMinHours} hours of incubation - not ready until {minReadyAt:yyyy-MM-dd HH:mm} UTC.",
                Math.Max(0, (long)Math.Ceiling((minReadyAt - DateTime.UtcNow).TotalSeconds)));

        // Record the completion of the incubation.
        incubation.CompletedAt = DateTime.UtcNow;
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
                SubmittedAtUtc = DateTime.UtcNow,
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
        if (currentStep.StepName != stepName)
            throw new InvalidOperationException($"Workflow order violation: step \"{currentStep.StepName}\" must be completed before \"{stepName}\".");

        var alreadyOpen = await _db.Incubations.AnyAsync(i => i.TestOrderId == testOrderId && i.StepName == stepName && i.CompletedAt == null);
        if (alreadyOpen)
            throw new InvalidOperationException($"Incubation has already been started for step \"{stepName}\" - awaiting its result.");

        var stepMedium = await RequireSingleStepMediumAsync(step);
        // SelectivePlating always has a MediaTypeId - only BiochemicalTest
        // (checked out above) leaves it null.
        var lot = await LoadReleasedLotAsync(mediaLotId, stepMedium.MaterialId, step.MediaTypeId!.Value);
        await RequireEligibleIncubatorAsync(stepMedium.Id, equipmentId);

        var startedAt = incubationStartUtc ?? DateTime.UtcNow;
        var incubation = new Incubation
        {
            TestOrderId = testOrderId,
            StepNumber = step.StepOrder,
            StepName = step.StepName,
            StageNumber = 1,
            MediaId = lot.Id,
            IncubatorEquipmentId = equipmentId,
            Temperature = $"{step.TemperatureMin}-{step.TemperatureMax} °C",
            Duration = $"{step.IncubationMinHours}-{step.IncubationMaxHours} hours",
            StartedAt = DateTime.UtcNow,
            IncubationStartUtc = startedAt,
            IncubationEndUtc = startedAt.AddHours(step.IncubationMaxHours),
            ExpectedReadingAt = startedAt.AddHours(step.IncubationMaxHours),
            WindowReceivedAtUtc = DateTime.UtcNow,
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

    public async Task<StepResultDto> SubmitSelectivePlatingObservationAsync(
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

        // GMP GATE: enforce minimum incubation time server-side
        var minReadyAt = incubation.IncubationStartUtc!.Value.AddHours((double)step.IncubationMinHours);
        if (DateTime.UtcNow < minReadyAt)
        {
            var remainingSeconds = Math.Max(0, (long)Math.Ceiling((minReadyAt - DateTime.UtcNow).TotalSeconds));
            throw new WorkflowStepException(WorkflowErrorCodes.IncubationNotComplete,
                $"Minimum incubation time not elapsed. Available from: {minReadyAt:yyyy-MM-dd HH:mm} UTC.",
                remainingSeconds);
        }

        incubation.CompletedAt = DateTime.UtcNow;
        incubation.Outcome = observation.ToString();
        await _db.SaveChangesAsync();

        var stepMedium = await RequireSingleStepMediumAsync(step);
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
            SubmittedAtUtc = DateTime.UtcNow
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

        await FinalizeWorkflowAsync(testOrderId, "NotDetected", userId);
        return new StepResultDto(incubation.Id, step.StepType.ToString(), "Complete",
            userId, result.SubmittedAtUtc, NextStepUnlocked: false, WorkflowFinalResult: "NotDetected", Flags: new List<string>());
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
    public async Task<StepResultDto> SubmitConfirmatorySetupAsync(
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

        var durationMax = step.IncubationMaxHours > 0 ? step.IncubationMaxHours : 24;
        incubationEndUtc = incubationStartUtc.AddHours((double)durationMax);

        RequireValidIncubationWindow(step, incubationStartUtc, incubationEndUtc);

        var permitted = await _db.TestWorkflowStepMedias
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

            // ConfirmatoryPlating always has a MediaTypeId - only
            // BiochemicalTest (checked out above) leaves it null.
            var lot = await LoadReleasedLotAsync(selection.MediaLotId, medium.MaterialId, step.MediaTypeId!.Value);
            await RequireEligibleIncubatorAsync(medium.Id, selection.EquipmentId);
            resolved.Add((medium, lot, selection.EquipmentId));
        }

        var incubation = new Incubation
        {
            TestOrderId = testOrderId, StepNumber = step.StepOrder, StepName = step.StepName,
            MediaId = resolved[0].Lot.Id, IncubatorEquipmentId = resolved[0].EquipmentId,
            Temperature = $"{step.TemperatureMin}-{step.TemperatureMax}",
            Duration = $"{step.IncubationMinHours}-{step.IncubationMaxHours}h",
            IncubationStartUtc = incubationStartUtc, IncubationEndUtc = incubationEndUtc,
            WindowReceivedAtUtc = DateTime.UtcNow,
            ExpectedReadingAt = incubationEndUtc
        };
        _db.Incubations.Add(incubation);
        await _db.SaveChangesAsync();

        var result = new WorkflowStepResult
        {
            IncubationId = incubation.Id, TestOrderId = testOrderId,
            StepName = step.StepName, StepType = step.StepType,
            SubmittedByUserId = userId, SubmittedAtUtc = DateTime.UtcNow
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
        RequireIncubationComplete(incubation.IncubationEndUtc
            ?? throw new WorkflowStepException(WorkflowErrorCodes.IncompleteConfirmatorySetup,
                "This step has no recorded incubation window."));

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
                ExpectedAppearanceSnapshot = snapshot, RecordedByUserId = userId, RecordedAtUtc = DateTime.UtcNow
            });
        }

        var allConforming = observations.All(o => o.Observation == GrowthObservation.GrowthConforming);
        result.ConfirmatoryResult = allConforming ? ConfirmatoryResult.AllConforming : ConfirmatoryResult.Inconclusive;
        incubation.CompletedAt = DateTime.UtcNow;
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
    public async Task<StepResultDto> RecordAnalystDecisionAsync(int testOrderId, AnalystDecision decision, int userId)
    {
        var order = await _db.TestOrders.FirstOrDefaultAsync(t => t.Id == testOrderId)
            ?? throw new InvalidOperationException($"Test order {testOrderId} not found.");
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
        confirmatory.AnalystDecisionAtUtc = DateTime.UtcNow;
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

        await FinalizeWorkflowAsync(testOrderId, "Detected", userId);

        return new StepResultDto(confirmatory.IncubationId, StepType.ConfirmatoryPlating.ToString(), "Complete",
            userId, confirmatory.SubmittedAtUtc, NextStepUnlocked: false, WorkflowFinalResult: "Detected",
            Flags: new List<string> { "BiochemicalNotPerformed" });
    }

    // Free-text confirmation with an optional attachment. There is no
    // incubation lock and no media on this step.
    public async Task<StepResultDto> SubmitBiochemicalAsync(
        int testOrderId, string stepName, string biochemicalResultText, int? attachmentId, int userId)
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

        var precedingConforms = precedingPlateStep.StepType == StepType.ConfirmatoryPlating
            ? precedingResult.ConfirmatoryResult == ConfirmatoryResult.AllConforming
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
            SkippedBiochemical = false,
            SubmittedByUserId = userId, SubmittedAtUtc = DateTime.UtcNow
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

        await FinalizeWorkflowAsync(testOrderId, "Detected", userId);

        return new StepResultDto(result.IncubationId, step.StepType.ToString(), "Complete",
            userId, result.SubmittedAtUtc, NextStepUnlocked: false, WorkflowFinalResult: "Detected", Flags: new List<string>());
    }

    // Reviewer action on a result flagged BiochemicalNotPerformed.
    // Returning re-opens the biochemical step for the analyst; the
    // signature/timeline entry goes through the existing review gate.
    public async Task<StepResultDto> RecordBiochemicalReviewDecisionAsync(
        int workflowStepResultId, bool approve, string comment, int reviewerUserId)
    {
        var result = await _db.WorkflowStepResults.FirstOrDefaultAsync(r => r.Id == workflowStepResultId)
            ?? throw new InvalidOperationException($"Workflow step result {workflowStepResultId} not found.");

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
                WorkflowFinalResult: "Detected", Flags: new List<string>());
        }

        result.RequiresBiochemical = true;
        result.ReturnReason = comment;
        result.ReturnedAtUtc = DateTime.UtcNow;
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
        await _db.SaveChangesAsync();

        if (order.AssignedAnalystId is int analystId)
            await _notifications.NotifyAsync(analystId,
                $"Test order #{result.TestOrderId} was returned for biochemical confirmation.");

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
