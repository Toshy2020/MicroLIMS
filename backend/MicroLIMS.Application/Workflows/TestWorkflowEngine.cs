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

// Generic step-runner replacing PathogenWorkflowEngine and
// CountTestWorkflowEngine: every test's chain (TAMC's single count
// step, a pathogen's five-stage Broth->Selective Broth->Selective
// Plating->Confirmatory Plating->Biochemical Test chain) is read from
// TestDefinition.WorkflowType + TestWorkflowStep, never hardcoded here.
// Nothing in this file compares against a literal test code or step
// name - that logic lives entirely in master data now.
//
// The engine itself is a facade over the parts of the workflow, each in
// its own class under Workflows/TestWorkflow: TestStepNavigator,
// IncubationStepRecorder, PathogenConfirmationRecorder,
// ElementalAssayRecorder, SingleResultRecorder, DissolutionRecorder,
// DisintegrationRecorder, WeightVariationRecorder
// and HplcMethodAssayRecorder. They all derive from TestWorkflowSupport,
// which holds what they share.
public class TestWorkflowEngine : ITestWorkflowEngine
{
    private readonly TestWorkflowSupport _support;
    private readonly TestStepNavigator _testStepNavigator;
    private readonly IncubationStepRecorder _incubationStepRecorder;
    private readonly ElementalAssayRecorder _elementalAssayRecorder;
    private readonly SingleResultRecorder _singleResultRecorder;
    private readonly DissolutionRecorder _dissolutionRecorder;
    private readonly DisintegrationRecorder _disintegrationRecorder;
    private readonly WeightVariationRecorder _weightVariationRecorder;
    private readonly HplcMethodAssayRecorder _hplcMethodAssayRecorder;
    private readonly TitrationRecorder _titrationRecorder;
    private readonly PathogenConfirmationRecorder _pathogenConfirmationRecorder;

    public TestWorkflowEngine(
        IMicroLimsDbContext db, SampleReviewService sampleReviewService, ResultProjectionService resultProjection,
        IncubatorEligibilityService incubatorEligibility, MediaAppearanceSnapshotService appearanceSnapshot,
        SegregationOfDutiesGuard sodGuard, ReviewGateService reviewGate, INotificationService notifications,
        IElectronicSignatureService? signatureService = null,
        IUserSectionScopeService? sectionScope = null,
        ILabClock? clock = null)
    {
        var deps = new TestWorkflowDependencies(
            db, sampleReviewService, resultProjection, incubatorEligibility, appearanceSnapshot,
            sodGuard, reviewGate, notifications,
            signatureService ?? new ElectronicSignatureService(db),
            sectionScope ?? new UserSectionScopeService(db),
            clock ?? LabClock.Default);
        _support = new TestWorkflowSupport(deps);
        _testStepNavigator = new TestStepNavigator(deps);
        _incubationStepRecorder = new IncubationStepRecorder(deps);
        _elementalAssayRecorder = new ElementalAssayRecorder(deps);
        _singleResultRecorder = new SingleResultRecorder(deps);
        _dissolutionRecorder = new DissolutionRecorder(deps);
        _disintegrationRecorder = new DisintegrationRecorder(deps);
        _weightVariationRecorder = new WeightVariationRecorder(deps);
        _hplcMethodAssayRecorder = new HplcMethodAssayRecorder(deps);
        _titrationRecorder = new TitrationRecorder(deps);
        _pathogenConfirmationRecorder = new PathogenConfirmationRecorder(deps);
    }

    public Task<bool> IsStepDoneAsync(int testOrderId, WorkflowType workflowType, TestWorkflowStep step) =>
        _testStepNavigator.IsStepDoneAsync(testOrderId, workflowType, step);

    public static bool IsStepDone(TestStepFacts facts, WorkflowType workflowType, TestWorkflowStep step) =>
        TestWorkflowSupport.IsStepDone(facts, workflowType, step);

    public Task<CurrentStepResult> GetCurrentStepAsync(int testOrderId) =>
        _testStepNavigator.GetCurrentStepAsync(testOrderId);

    public Task<CurrentStepDetails> GetCurrentStepDetailsAsync(int testOrderId) =>
        _testStepNavigator.GetCurrentStepDetailsAsync(testOrderId);

    public Task<IReadOnlyDictionary<int, CurrentStepLookup>> GetCurrentStepDetailsForOrdersAsync(IReadOnlyCollection<int> testOrderIds) =>
        _testStepNavigator.GetCurrentStepDetailsForOrdersAsync(testOrderIds);

    public async Task<IncubationResponse> SelectMediaAsync(int testOrderId, string stepName, int mediaLotId, int incubatorEquipmentId, int userId) =>
        IncubationResponse.From(await _incubationStepRecorder.SelectMediaAsync(testOrderId, stepName, mediaLotId, incubatorEquipmentId, userId));

    public Task<List<SiblingPathogenOrderDto>> GetSiblingPathogenOrdersAsync(int testOrderId, CancellationToken ct = default) =>
        _testStepNavigator.GetSiblingPathogenOrdersAsync(testOrderId, ct);

    public Task PropagateSharedTsbToSiblingOrdersAsync(
        int testOrderId,
        int incubationId,
        int userId,
        CancellationToken ct = default) =>
        _support.PropagateSharedTsbToSiblingOrdersAsync(testOrderId, incubationId, userId, ct);

    public async Task<IncubationResponse> StartStage2IncubationAsync(int testOrderId, string stepName, int incubatorEquipmentId, int userId) =>
        IncubationResponse.From(await _incubationStepRecorder.StartStage2IncubationAsync(testOrderId, stepName, incubatorEquipmentId, userId));

    public Task<TestWorkflowResult> RecordResultAsync(int testOrderId, string stepName, ResultPayload payload, int userId) =>
        _incubationStepRecorder.RecordResultAsync(testOrderId, stepName, payload, userId);

    public async Task<List<TestOrderLocationResponse>> GetLocationsAsync(int testOrderId) =>
        (await _incubationStepRecorder.GetLocationsAsync(testOrderId)).Select(TestOrderLocationResponse.From).ToList();

    public async Task<IncubationResponse> CloseCurrentIncubationWindowAsync(int testOrderId, int userId) =>
        IncubationResponse.From(await _incubationStepRecorder.CloseCurrentIncubationWindowAsync(testOrderId, userId));

    public async Task<IncubationResponse> OverrideMinimumDurationAsync(int testOrderId, int userId) =>
        IncubationResponse.From(await _incubationStepRecorder.OverrideMinimumDurationAsync(testOrderId, userId));

    public Task<TestWorkflowResult> RecordBatchResultsAsync(int testOrderId, List<BatchLocationReadings> locations, int userId) =>
        _incubationStepRecorder.RecordBatchResultsAsync(testOrderId, locations, userId);

    public Task<TestWorkflowResult> RecordWaterBatchReadingsAsync(int testOrderId, List<WaterBatchLocationReadings> locations, int userId) =>
        _incubationStepRecorder.RecordWaterBatchReadingsAsync(testOrderId, locations, userId);

    public Task<TestWorkflowResult> RecordBatchPathogenResultsAsync(
        int testOrderId, List<BatchLocationObservation>? observations, int userId) =>
        _incubationStepRecorder.RecordBatchPathogenResultsAsync(testOrderId, observations, userId);

    public static string GetCfuUnit(SampleCategory category, string? prepUnit) =>
        IncubationStepRecorder.GetCfuUnit(category, prepUnit);

    public static (ResultStatus status, string? exceeded) Compare(decimal value, string? alert, string? action, string? spec) =>
        IncubationStepRecorder.Compare(value, alert, action, spec);

    public Task<TestWorkflowResult> RecordElementalAssayResultAsync(
        int testOrderId, ElementalAssayPayload payload, int userId, string? ipAddress = null) =>
        _elementalAssayRecorder.RecordElementalAssayResultAsync(testOrderId, payload, userId, ipAddress);

    public Task<TestWorkflowResult> RecordMeasurementResultAsync(
        int testOrderId, MeasurementPayload payload, int userId, string? ipAddress = null) =>
        _singleResultRecorder.RecordMeasurementResultAsync(testOrderId, payload, userId, ipAddress);

    public Task<TestWorkflowResult> RecordTitrationResultAsync(
        int testOrderId, TitrationPayload payload, int userId, string? ipAddress = null) =>
        _titrationRecorder.RecordTitrationResultAsync(testOrderId, payload, userId, ipAddress);

    public Task<TestWorkflowResult> RecordGravimetricResultAsync(
        int testOrderId, GravimetricPayload payload, int userId, string? ipAddress = null) =>
        _singleResultRecorder.RecordGravimetricResultAsync(testOrderId, payload, userId, ipAddress);

    public Task<TestWorkflowResult> RecordQualitativeResultAsync(
        int testOrderId, QualitativePayload payload, int userId, string? ipAddress = null) =>
        _singleResultRecorder.RecordQualitativeResultAsync(testOrderId, payload, userId, ipAddress);

    public Task<TestWorkflowResult> RecordDissolutionResultAsync(
        int testOrderId, DissolutionPayload payload, int userId, string? ipAddress = null) =>
        _dissolutionRecorder.RecordDissolutionResultAsync(testOrderId, payload, userId, ipAddress);

    public Task<TestWorkflowResult> RecordDissolutionStageAsync(
        int testOrderId, DissolutionStagePayload payload, int userId, string? ipAddress = null) =>
        _dissolutionRecorder.RecordDissolutionStageAsync(testOrderId, payload, userId, ipAddress);

    public Task<TestWorkflowResult> RecordDisintegrationResultAsync(
        int testOrderId, DisintegrationPayload payload, int userId, string? ipAddress = null) =>
        _disintegrationRecorder.RecordDisintegrationResultAsync(testOrderId, payload, userId, ipAddress);

    public Task<TestWorkflowResult> RecordDisintegrationStageAsync(
        int testOrderId, DisintegrationStagePayload payload, int userId, string? ipAddress = null) =>
        _disintegrationRecorder.RecordDisintegrationStageAsync(testOrderId, payload, userId, ipAddress);

    public Task<TestWorkflowResult> RecordWeightVariationResultAsync(
        int testOrderId, WeightVariationPayload payload, int userId, string? ipAddress = null) =>
        _weightVariationRecorder.RecordWeightVariationResultAsync(testOrderId, payload, userId, ipAddress);

    public Task<TestWorkflowResult> RecordWeightVariationStageAsync(
        int testOrderId, WeightVariationStagePayload payload, int userId, string? ipAddress = null) =>
        _weightVariationRecorder.RecordWeightVariationStageAsync(testOrderId, payload, userId, ipAddress);

    public Task<TestWorkflowResult> SubmitHplcMethodAssayAsync(
        int runSampleId, string password, string? comment, int userId, string? ipAddress = null) =>
        _hplcMethodAssayRecorder.SubmitAsync(runSampleId, password, comment, userId, ipAddress);

    public Task<StepResultDto> SubmitBrothAsync(
        int testOrderId, string stepName, string? observation, int userId) =>
        _pathogenConfirmationRecorder.SubmitBrothAsync(testOrderId, stepName, observation, userId);

    public async Task<IncubationResponse> StartSelectivePlatingIncubationAsync(
        int testOrderId, string stepName, int mediaLotId, int equipmentId, DateTime? incubationStartUtc, int userId) =>
        IncubationResponse.From(await _pathogenConfirmationRecorder.StartSelectivePlatingIncubationAsync(testOrderId, stepName, mediaLotId, equipmentId, incubationStartUtc, userId));

    public Task<StepResultDto> SubmitSelectivePlatingObservationAsync(
        int testOrderId, string stepName, GrowthObservation observation, string? observedAppearanceNote, int userId) =>
        _pathogenConfirmationRecorder.SubmitSelectivePlatingObservationAsync(testOrderId, stepName, observation, observedAppearanceNote, userId);

    public Task<StepResultDto> SubmitSelectivePlatingAsync(
        int testOrderId, string stepName, int mediaLotId, int equipmentId,
        DateTime incubationStartUtc, DateTime incubationEndUtc, GrowthObservation observation, int userId) =>
        _pathogenConfirmationRecorder.SubmitSelectivePlatingAsync(testOrderId, stepName, mediaLotId, equipmentId, incubationStartUtc, incubationEndUtc, observation, userId);

    public Task<StepResultDto> SubmitConfirmatorySetupAsync(
        int testOrderId, string stepName, IReadOnlyList<ConfirmatorySelectionInput> selections,
        DateTime incubationStartUtc, DateTime incubationEndUtc, int userId) =>
        _pathogenConfirmationRecorder.SubmitConfirmatorySetupAsync(testOrderId, stepName, selections, incubationStartUtc, incubationEndUtc, userId);

    public Task<ConfirmatoryOutcomeDto> SubmitConfirmatoryObservationsAsync(
        int testOrderId, string stepName, IReadOnlyList<ConfirmatoryObservationInput> observations, int userId) =>
        _pathogenConfirmationRecorder.SubmitConfirmatoryObservationsAsync(testOrderId, stepName, observations, userId);

    public Task<StepResultDto> RecordAnalystDecisionAsync(int testOrderId, AnalystDecision decision, int userId) =>
        _pathogenConfirmationRecorder.RecordAnalystDecisionAsync(testOrderId, decision, userId);

    public Task<StepResultDto> SubmitBiochemicalAsync(
        int testOrderId, string stepName, string biochemicalResultText, int? attachmentId, bool organismDetected, int userId) =>
        _pathogenConfirmationRecorder.SubmitBiochemicalAsync(testOrderId, stepName, biochemicalResultText, attachmentId, organismDetected, userId);

    public Task<StepResultDto> RecordBiochemicalReviewDecisionAsync(
        int workflowStepResultId, bool approve, string comment, int reviewerUserId) =>
        _pathogenConfirmationRecorder.RecordBiochemicalReviewDecisionAsync(workflowStepResultId, approve, comment, reviewerUserId);

    public Task<WorkflowStep> AdvanceAsync(int testOrderId, int performedByUserId, string? note = null) =>
        _testStepNavigator.AdvanceAsync(testOrderId, performedByUserId, note);

    public Task<List<string>> ValidateAsync(int testOrderId) =>
        _testStepNavigator.ValidateAsync(testOrderId);

    public Task<bool> IsCompleteAsync(int testOrderId) =>
        _testStepNavigator.IsCompleteAsync(testOrderId);
}
