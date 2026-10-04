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
using MicroLIMS.Application.DTOs.Responses;
using MicroLIMS.Shared.Constants;

namespace MicroLIMS.Application.Workflows;

// Result payload union for RecordResultAsync - which record is passed
// depends on the TestDefinition's WorkflowType (CountTest) or plain
// Observation otherwise.
public abstract record ResultPayload;
public sealed record CountTestPayload(List<string> RawPlateReadings, decimal DilutionFactor, string? DilutionFactorOverrideNote = null) : ResultPayload
{
    public CountTestPayload(List<decimal> plateReadings, decimal dilutionFactor, string? dilutionFactorOverrideNote = null)
        : this(plateReadings.Select(p => p.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToList(), dilutionFactor, dilutionFactorOverrideNote)
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

public class PredecessorStepIncubationActiveException : WorkflowStepException
{
    public string PredecessorStepName { get; }

    public PredecessorStepIncubationActiveException(string predecessorStepName, long remainingSeconds)
        : base(WorkflowErrorCodes.PredecessorStepIncubationActive,
               $"Predecessor step \"{predecessorStepName}\" is still incubating ({remainingSeconds}s remaining). It must complete before starting the next step.",
               remainingSeconds)
    {
        PredecessorStepName = predecessorStepName;
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
    bool NextStepUnlocked, ResultStatus? WorkflowFinalResult, List<string> Flags);

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
    string? ReportedResult, decimal? CalculatedResult, ResultStatus? Status);

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

// GetCurrentStepAsync's result together with what it was computed from, so
// the step view can build its response without querying the same rows again.
public record CurrentStepDetails(CurrentStepResult Result, TestOrder Order, TestDefinition Definition, TestStepFacts Facts);

// One order's entry from GetCurrentStepDetailsForOrdersAsync: the details, or
// the message GetCurrentStepDetailsAsync would have thrown for that order.
public record CurrentStepLookup(CurrentStepDetails? Details, string? Error);

// Every row the step-completion rules read for one test order, loaded once
// instead of two or three queries per step checked.
public record TestStepFacts(
    bool HasLocations, IReadOnlyList<Incubation> Incubations, IReadOnlyList<StepResultFact> StepResults,
    IReadOnlyList<CountReadingFact> ActiveCountReadings, IReadOnlySet<string> ObservationStepNames,
    bool HasActiveAnalysis = false);

public record StepResultFact(
    int Id, int IncubationId, string StepName, bool? IsSharedSessionStep, bool HasConfirmatoryResult,
    string? BiochemicalResultText, bool? BiochemicalOrganismDetected, DateTime SubmittedAtUtc);

public record CountReadingFact(int Id, string? StepName, string ReportedResult, decimal? CalculatedResult, ResultStatus Status, DateTime EnteredAt);

public record TestWorkflowResult(
    string OutcomeSummary, bool IsDefinitive, bool AllStepsComplete, string? FinalResult,
    decimal? Average, decimal? CalculatedResult, ResultStatus? Status);

public record ElementalAssayElementInput(
    int SpecificationId,
    int CalibrationRunAnalyteId,
    decimal ReportedPpm,
    bool OverRange,
    bool BelowLoq);

public record ElementalAssayPayload(
    decimal UnitAmount,
    DateTime AnalysedAt,
    List<ElementalAssayElementInput> Elements,
    string Password,
    string? Comment = null);

public record MeasurementParameterInput(
    int SpecificationId,
    List<decimal> Readings);

public record MeasurementPayload(
    DateTime AnalysedAt,
    int? EquipmentId,
    List<MeasurementParameterInput> Parameters,
    string Password,
    string? Comment = null);

public record GravimetricParameterInput(
    int SpecificationId,
    List<GravimetricReplicateInput> Replicates);

public record GravimetricPayload(
    DateTime AnalysedAt,
    int? EquipmentId,
    Dictionary<string, string> Conditions,
    List<GravimetricParameterInput> Parameters,
    string Password,
    string? Comment = null);

public record QualitativeParameterInput(
    int SpecificationId,
    bool Conforms,
    string? Observation);

public record QualitativePayload(
    DateTime AnalysedAt,
    int? EquipmentId,
    List<QualitativeParameterInput> Parameters,
    string Password,
    string? Comment = null);

public record DissolutionPayload(
    DateTime AnalysedAt,
    int? EquipmentId,
    Dictionary<string, string>? Conditions,
    decimal MediumVolumeMl,
    decimal? DilutionFactor,
    List<decimal> VesselAreas,
    string Password,
    string? Comment = null);

public record DissolutionStagePayload(
    List<decimal> VesselAreas,
    string Password,
    string? Comment = null);

public record DisintegrationPayload(
    DateTime AnalysedAt,
    int? EquipmentId,
    Dictionary<string, string>? Conditions,
    List<decimal?> UnitMinutes,
    string Password,
    string? Comment = null);

public record DisintegrationStagePayload(
    List<decimal?> UnitMinutes,
    string Password,
    string? Comment = null);

public record WeightVariationUnitPayload(
    decimal? WeightMg = null,
    decimal? GrossMg = null,
    decimal? ShellMg = null);

public record WeightVariationPayload(
    DateTime AnalysedAt,
    int? EquipmentId,
    Dictionary<string, string>? Conditions,
    List<WeightVariationUnitPayload> Units,
    string Password,
    string? Comment = null);

public record WeightVariationStagePayload(
    List<WeightVariationUnitPayload> Units,
    string Password,
    string? Comment = null);

// One location's CFU reading submitted from the LocationResultGrid -
// EM/After Cleaning batch results, never used by the single-value
// RecordResultAsync path.
// Multiple raw plate readings per location, averaged the same way
// RecordCountTestAsync averages RawPlateReadings for a single order - the
// dilution factor is always 1 here (EM/After Cleaning are direct-count
// categories, same rule RecordCountTestAsync already forces for them).
public record BatchLocationReadings(int SampleLocationId, List<decimal> Readings);

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
    Task<CurrentStepDetails> GetCurrentStepDetailsAsync(int testOrderId);
    Task<IReadOnlyDictionary<int, CurrentStepLookup>> GetCurrentStepDetailsForOrdersAsync(IReadOnlyCollection<int> testOrderIds);
    Task<bool> IsStepDoneAsync(int testOrderId, WorkflowType workflowType, TestWorkflowStep step);
    Task<List<SiblingPathogenOrderDto>> GetSiblingPathogenOrdersAsync(int testOrderId, CancellationToken ct = default);
    Task PropagateSharedTsbToSiblingOrdersAsync(int testOrderId, int incubationId, int userId, CancellationToken ct = default);
    Task<IncubationResponse> SelectMediaAsync(int testOrderId, string stepName, int mediaLotId, int incubatorEquipmentId, int userId);
    Task<IncubationResponse> StartStage2IncubationAsync(int testOrderId, string stepName, int incubatorEquipmentId, int userId);
    Task<TestWorkflowResult> RecordResultAsync(int testOrderId, string stepName, ResultPayload payload, int userId);
    Task<TestWorkflowResult> SubmitHplcMethodAssayAsync(int runSampleId, string password, string? comment, int userId, string? ipAddress = null);
    Task<TestWorkflowResult> SubmitIcpMethodAssayAsync(int runSampleId, string password, string? comment, int userId, string? ipAddress = null);
    Task<TestWorkflowResult> RecordElementalAssayResultAsync(int testOrderId, ElementalAssayPayload payload, int userId, string? ipAddress = null);
    Task<TestWorkflowResult> RecordMeasurementResultAsync(int testOrderId, MeasurementPayload payload, int userId, string? ipAddress = null);
    Task<TestWorkflowResult> RecordGravimetricResultAsync(int testOrderId, GravimetricPayload payload, int userId, string? ipAddress = null);
    Task<TestWorkflowResult> RecordTitrationResultAsync(int testOrderId, TitrationPayload payload, int userId, string? ipAddress = null);
    Task<TestWorkflowResult> RecordQualitativeResultAsync(int testOrderId, QualitativePayload payload, int userId, string? ipAddress = null);
    Task<TestWorkflowResult> RecordDissolutionResultAsync(int testOrderId, DissolutionPayload payload, int userId, string? ipAddress = null);
    Task<TestWorkflowResult> RecordDissolutionStageAsync(int testOrderId, DissolutionStagePayload payload, int userId, string? ipAddress = null);
    Task<TestWorkflowResult> RecordDisintegrationResultAsync(int testOrderId, DisintegrationPayload payload, int userId, string? ipAddress = null);
    Task<TestWorkflowResult> RecordDisintegrationStageAsync(int testOrderId, DisintegrationStagePayload payload, int userId, string? ipAddress = null);
    Task<TestWorkflowResult> RecordWeightVariationResultAsync(int testOrderId, WeightVariationPayload payload, int userId, string? ipAddress = null);
    Task<TestWorkflowResult> RecordWeightVariationStageAsync(int testOrderId, WeightVariationStagePayload payload, int userId, string? ipAddress = null);
    Task<List<TestOrderLocationResponse>> GetLocationsAsync(int testOrderId);
    Task<IncubationResponse> CloseCurrentIncubationWindowAsync(int testOrderId, int userId);
    // Section Head/System Administrator only (enforced at the controller) -
    // bypasses the minimum-duration wait gate for the currently open
    // incubation on this test order. Never changes the recorded window.
    Task<IncubationResponse> OverrideMinimumDurationAsync(int testOrderId, int userId);
    Task<TestWorkflowResult> RecordBatchResultsAsync(int testOrderId, List<BatchLocationReadings> locations, int userId);
    Task<TestWorkflowResult> RecordWaterBatchReadingsAsync(int testOrderId, List<WaterBatchLocationReadings> locations, int userId);
    Task<TestWorkflowResult> RecordBatchPathogenResultsAsync(int testOrderId, List<BatchLocationObservation>? observations, int userId);

    // Broth steps carry no result logic - completion is the incubation
    // window elapsing plus the analyst submitting the form. The window
    // is server-controlled from Test Master and recorded when media is selected.
    Task<StepResultDto> SubmitBrothAsync(int testOrderId, string stepName, string? observation, int userId);

    Task<IncubationResponse> StartSelectivePlatingIncubationAsync(int testOrderId, string stepName, int mediaLotId, int equipmentId,
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

    Task<StepResultDto> SubmitBiochemicalAsync(int testOrderId, string stepName, string biochemicalResultText, int? attachmentId, bool organismDetected, int userId);

    Task<StepResultDto> RecordBiochemicalReviewDecisionAsync(int workflowStepResultId, bool approve, string comment, int reviewerUserId);
}
