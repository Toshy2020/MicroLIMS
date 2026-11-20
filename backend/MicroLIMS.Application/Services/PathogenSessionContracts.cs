using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Services;

public record StartSharedTsbRequest(
    int MediaLotId,
    int IncubatorEquipmentId,
    DateTime? IncubationStartUtc);

public record RecordSharedStepRequest(
    int TestOrderId,
    string StepName,
    int? MediaLotId,
    int? IncubatorEquipmentId,
    decimal? Temperature,
    int? IncubationHours,
    DateTime? IncubationStartUtc,
    GrowthObservation? Observation,
    string? BiochemicalResultText);

public record MatrixCellInput(
    int SampleLocationId,
    string TestCode,
    string ResultCode,      // "NOT_DETECTED", "DETECTED", or numeric string "6"
    string ResultDisplay,   // "Not Detected (-)", "Detected (+)", "6 CFU"
    decimal? NumericValue,
    string ResultType);     // "Qualitative" or "Quantitative"

public record SaveResultMatrixRequest(List<MatrixCellInput> Cells);

public record PrimaryObservationInput(
    int SampleLocationId,
    string TestCode,
    GrowthObservation Observation,
    string? SelectiveMediaSnapshot = null);

public record SavePrimaryObservationsRequest(List<PrimaryObservationInput> Observations);

public record EligibleLocationForConfirmationDto(
    int LocationId,
    int PrimaryObservationId,
    string LocationName,
    int TestOrderId,
    string TestCode,
    string TestDisplayName,
    GrowthObservation GrowthObservation,
    string GrowthObservationDisplay,
    int RequiredConfirmatoryMediaCount);

public record BatchConfirmatorySetupRequest(
    int TestOrderId,
    List<int> LocationIds,
    List<int> MediaMaterialIds,
    List<int>? MediaLotIds,
    int IncubatorEquipmentId,
    DateTime? IncubationStartUtc);

public record ResetPathogenSessionRequest(string? Reason);

public record BatchConfirmatoryPlateReadingInput(
    int LocationPathogenObservationId,
    int MediumIndex,
    int MaterialId,
    GrowthObservation Observation);

public record SaveBatchConfirmatoryPlateReadingsRequest(
    List<BatchConfirmatoryPlateReadingInput> Readings,
    string? BiochemicalComment = null);

public record ConfirmatoryPlateObservationDetailDto(
    int Id,
    int MediumIndex,
    int MaterialId,
    string? MaterialName,
    string Observation,
    string? ExpectedAppearanceSnapshot,
    DateTime RecordedAtUtc,
    string RecordedByUserName);

public record SessionLocationDto(
    int Id,
    int PrimarySampleLocationId,
    string LocationName,
    string LocationType,
    string? GradeClassification,
    Dictionary<string, int> TestLocationMap); // TestCode -> SampleLocationId

public record CountResultDto(
    string PlateReadings,
    decimal DilutionFactor,
    decimal? Average,
    decimal? FinalCfu,
    string ReportedResult,
    ResultStatus Status,
    bool HasNonNumericReading,
    string? NonNumericValue,
    bool RequiresReview,
    string Unit
);

public record SessionAssignedTestDto(
    int TestOrderId,
    string TestCode,
    string DisplayName,
    string WorkflowType,
    string Status,
    string CurrentStep,
    string? AssignedAnalystName,
    bool RequiresTsb,
    string TestSessionState,
    string TestSessionStateDisplay,
    bool IsResultEntryAllowed,
    bool IsWorkflowLocked,
    string? LockReason,
    List<SessionWorkflowStepDto> Steps,
    int ConfirmatoryMediaCount = 1,
    string WorkflowStatus = "Pending",
    CountResultDto? CountResult = null);

// MediaTypeId/MediaTypeName predate the Media Configuration Migration -
// MediaTypeId is now always null (the old class-level concept no longer
// exists) and MediaTypeName is populated from StepType instead of
// MediaType.Class, which the frontend's "Broth" substring checks still
// match correctly (BrothEnrichment/SelectiveBroth). Kept rather than
// renamed to avoid a frontend type change outside this migration's scope.
public record SessionWorkflowStepDto(
    int StepOrder,
    string StepName,
    string StepType,
    int? MediaTypeId,
    string? MediaTypeName,
    int IncubationMinHours,
    int IncubationMaxHours,
    decimal TemperatureMin,
    decimal TemperatureMax,
    bool IsCompleted,
    string? Outcome,
    DateTime? CompletedAt);

public record SharedTsbStateDto(
    bool IsStarted,
    bool IsIncubating,
    bool IsCompleted,
    bool IsLocked,
    int? MediaLotId,
    string? MediaLotNumber,
    string? MediaMaterialName,
    string? GptStatus,
    string? SterilityStatus,
    int? IncubatorEquipmentId,
    string? IncubatorCode,
    string? RequiredTemperatureRange,
    string? RequiredDurationRange,
    string? Temperature,
    int? IncubationDurationHours,
    DateTime? ActualStartUtc,
    DateTime? MinReadyAt,
    DateTime? ExpectedCompletionUtc,
    DateTime? CompletedAtUtc,
    int? StartedByUserId,
    string? StartedByUserName,
    List<string> ApplicableTestCodes,
    int ApplicableLocationCount,
    // True when a test's own TSB window can't be resolved from Test Master -
    // the TSB then never reports ready instead of using a guessed window.
    bool WindowNotConfigured = false);

public record MatrixCellResultDto(
    int SampleLocationId,
    string TestCode,
    string LocationName,
    string? ResultCode,
    string? ResultDisplay,
    decimal? NumericValue,
    string ResultType,
    string? Status,
    DateTime? EnteredAt,
    string? EnteredByUserName,
    bool IsEditable,
    string CellState,       // "COMPLETED", "AVAILABLE", "LOCKED_PREREQUISITE"
    string? LockReason,
    int? PrimaryObservationId = null,
    string? PrimaryObservation = null,
    bool IsEligibleForConfirmation = false,
    string? ConfirmationStatus = null,
    List<ConfirmatoryPlateObservationDetailDto>? ConfirmatoryPlates = null);

public record MissingResultDto(
    string LocationName,
    string TestCode,
    string TestDisplayName);

public record PathogenTestingSessionDto(
    string SessionId,
    int SampleId,
    string SampleReferenceNumber,
    string Category,
    string ProgramName,
    string DepartmentOrAreaName,
    string ControlNumber,
    string? BatchNumber,
    DateTime SamplingDate,
    string OverallSessionStatus,
    string OverallSessionStatusDisplay,
    int TotalLocations,
    int TotalAssignedTests,
    int RequiredResultCount,
    int CompletedResultCount,
    int AvailableResultCount,
    int LockedResultCount,
    int PendingResultCount,
    List<SessionLocationDto> Locations,
    List<SessionAssignedTestDto> AssignedTests,
    SharedTsbStateDto SharedTsb,
    List<MatrixCellResultDto> ResultMatrix,
    List<MissingResultDto> MissingResults);
