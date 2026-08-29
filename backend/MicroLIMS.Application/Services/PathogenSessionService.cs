using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;

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
    string Status,
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
    int ApplicableLocationCount);

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

public class PathogenSessionService
{
    private readonly MicroLimsDbContext _db;
    private readonly MediaAppearanceSnapshotService? _appearanceSnapshot;
    private readonly ConfirmationAgreementEvaluator _agreementEvaluator;
    private readonly LocationPathogenObservationService _locationObsService;

    public PathogenSessionService(
        MicroLimsDbContext db,
        MediaAppearanceSnapshotService? appearanceSnapshot = null,
        ConfirmationAgreementEvaluator? agreementEvaluator = null,
        LocationPathogenObservationService? locationObsService = null)
    {
        _db = db;
        _appearanceSnapshot = appearanceSnapshot;
        _agreementEvaluator = agreementEvaluator ?? new ConfirmationAgreementEvaluator();
        _locationObsService = locationObsService ?? new LocationPathogenObservationService(db);
    }

    public async Task<PathogenTestingSessionDto?> GetSessionAsync(int sampleId)
    {
        var sample = await _db.Samples
            .Include(s => s.Item)
            .Include(s => s.Department)
            .Include(s => s.Machine)
            .Include(s => s.WaterSamplingPoint)
            .Include(s => s.WaterDepartment)
            .Include(s => s.CauseOfTesting)
            .Include(s => s.TestOrders)
            .Include(s => s.Locations)
                .ThenInclude(l => l.RoomTestConfiguration!)
                .ThenInclude(c => c.Room)
            .Include(s => s.Locations)
                .ThenInclude(l => l.MachinePartConfiguration!)
                .ThenInclude(c => c.MachinePart)
            .Include(s => s.Locations)
                .ThenInclude(l => l.WaterSamplingPoint!)
            .FirstOrDefaultAsync(s => s.Id == sampleId);

        if (sample == null) return null;

        var sessionId = $"SESSION-{sample.ReferenceNumber}";
        var testCodes = sample.TestOrders.Select(t => t.TestCode).Distinct().ToList();

        // Load Test Definitions from Test Master
        var testDefs = await _db.TestDefinitions
            .Include(t => t.Steps)
                .ThenInclude(s => s.StepMedia)
                    .ThenInclude(m => m.Material)
            .Where(t => testCodes.Contains(t.Code))
            .ToDictionaryAsync(t => t.Code);

        // Load Users for attribution
        var users = await _db.Users.ToDictionaryAsync(u => u.Id, u => u.FullName);
        string UserName(int? uid) => (uid.HasValue && users.TryGetValue(uid.Value, out var name)) ? name : "Unknown";

        // Group Locations
        var groupedLocations = new List<SessionLocationDto>();
        // Keyed by the underlying physical location's stable id, not its
        // display name - two distinct Room/MachinePart/WaterSamplingPoint
        // rows can share the same display text (e.g. multiple water points
        // all named "WTU"), and a display-name key would silently merge
        // them, dropping every TestCode mapping but the last one written.
        var locationMap = new Dictionary<(string Kind, int Id), SessionLocationDto>();

        foreach (var loc in sample.Locations)
        {
            var locName = loc.RoomTestConfiguration?.Room?.Name
                ?? loc.MachinePartConfiguration?.MachinePart?.Name
                ?? loc.WaterSamplingPoint?.Location
                ?? $"Location #{loc.Id}";

            var locKey = loc.RoomTestConfigurationId.HasValue ? ("Room", loc.RoomTestConfigurationId.Value)
                : loc.MachinePartConfigurationId.HasValue ? ("MachinePart", loc.MachinePartConfigurationId.Value)
                : loc.WaterSamplingPointId.HasValue ? ("Water", loc.WaterSamplingPointId.Value)
                : ("Loc", loc.Id);

            var grade = loc.RoomTestConfiguration?.Room?.GradeClassification.ToString();
            var locType = loc.LocationType.ToString();

            if (!locationMap.TryGetValue(locKey, out var group))
            {
                group = new SessionLocationDto(
                    groupedLocations.Count + 1,
                    loc.Id,
                    locName,
                    locType,
                    grade,
                    new Dictionary<string, int>());
                locationMap[locKey] = group;
                groupedLocations.Add(group);
            }

            var to = sample.TestOrders.FirstOrDefault(t => t.Id == loc.TestOrderId);
            if (to != null)
            {
                group.TestLocationMap[to.TestCode] = loc.Id;
            }
        }

        // If no locations exist on sample (e.g. single product sample), create 1 default session location
        if (groupedLocations.Count == 0)
        {
            var defaultLoc = new SessionLocationDto(
                1,
                0,
                sample.Item?.Name ?? sample.ReferenceNumber,
                "Product",
                null,
                new Dictionary<string, int>());
            foreach (var to in sample.TestOrders)
            {
                defaultLoc.TestLocationMap[to.TestCode] = 0;
            }
            groupedLocations.Add(defaultLoc);
        }

        // Load Incubations and WorkflowStepResults for all test orders
        var toIds = sample.TestOrders.Select(t => t.Id).ToList();
        var incubations = await _db.Incubations
            .Include(i => i.Media)
                .ThenInclude(m => m!.Material)
            .Include(i => i.IncubatorEquipment)
            .Where(i => i.TestOrderId.HasValue && toIds.Contains(i.TestOrderId.Value))
            .ToListAsync();

        var stepResults = await _db.WorkflowStepResults
            .Where(w => toIds.Contains(w.TestOrderId))
            .ToListAsync();

        var primaryObservations = await _db.LocationPathogenObservations
            .Include(o => o.ConfirmatoryPlateObservations)
                .ThenInclude(p => p.Material)
            .Where(o => toIds.Contains(o.TestOrderId))
            .ToListAsync();

        var primaryObsMap = primaryObservations
            .ToDictionary(o => (o.SampleLocationId, o.TestOrderId));

        var countTestReadings = await _db.CountTestReadings
            .Where(c => toIds.Contains(c.TestOrderId))
            .ToListAsync();

        // 1. Identify which tests require TSB from Test Master configuration
        var tsbApplicableCodes = new List<string>();
        decimal requiredTsbTempMin = 30;
        decimal requiredTsbTempMax = 35;
        int requiredTsbHoursMin = 18;
        int requiredTsbHoursMax = 24;

        foreach (var to in sample.TestOrders)
        {
            if (testDefs.TryGetValue(to.TestCode, out var def))
            {
                // SelectiveBroth (e.g. MBP for E.coli, RVS for Salmonella) is
                // species-specific and never shares this generic TSB lot -
                // matching it here was the bug that let a shared TSB batch
                // land on the wrong step. Only the true, common broth
                // enrichment step (or an explicitly TSB-named one) qualifies.
                var tsbStep = def.Steps.FirstOrDefault(s =>
                    s.StepType is StepType.BrothEnrichment ||
                    s.StepName.Contains("TSB", StringComparison.OrdinalIgnoreCase));

                if (tsbStep != null)
                {
                    tsbApplicableCodes.Add(to.TestCode);
                    var firstMedia = tsbStep.StepMedia.OrderBy(m => m.DisplayOrder).FirstOrDefault();
                    if (tsbStep.TemperatureMin > 0) requiredTsbTempMin = tsbStep.TemperatureMin;
                    else if (firstMedia?.TempMin > 0) requiredTsbTempMin = firstMedia.TempMin;

                    if (tsbStep.TemperatureMax > 0) requiredTsbTempMax = tsbStep.TemperatureMax;
                    else if (firstMedia?.TempMax > 0) requiredTsbTempMax = firstMedia.TempMax;

                    if (tsbStep.IncubationMinHours > 0) requiredTsbHoursMin = tsbStep.IncubationMinHours;
                    else if (firstMedia?.IncubationMinHours > 0) requiredTsbHoursMin = firstMedia.IncubationMinHours;

                    if (tsbStep.IncubationMaxHours > 0) requiredTsbHoursMax = tsbStep.IncubationMaxHours;
                    else if (firstMedia?.IncubationMaxHours > 0) requiredTsbHoursMax = firstMedia.IncubationMaxHours;
                }
            }
        }

        // Shared TSB State
        var sharedTsbIncubation = TsbDetectionHelper.FindSharedTsbIncubation(incubations);

        bool tsbStarted = sharedTsbIncubation != null;
        DateTime? tsbStart = sharedTsbIncubation?.IncubationStartUtc ?? sharedTsbIncubation?.StartedAt;
        DateTime? tsbMinReadyAt = TsbDetectionHelper.GetTsbMinReadyAt(sharedTsbIncubation, requiredTsbHoursMin);

        bool tsbIncubating = TsbDetectionHelper.IsTsbIncubating(sharedTsbIncubation, requiredTsbHoursMin, DateTime.UtcNow);
        bool tsbCompleted = TsbDetectionHelper.IsTsbComplete(sharedTsbIncubation, requiredTsbHoursMin, DateTime.UtcNow);

        SharedTsbStateDto sharedTsbDto;
        if (sharedTsbIncubation != null)
        {
            sharedTsbDto = new SharedTsbStateDto(
                IsStarted: true,
                IsIncubating: tsbIncubating,
                IsCompleted: tsbCompleted,
                IsLocked: true, // Once started, parameters are locked
                MediaLotId: sharedTsbIncubation.MediaId,
                MediaLotNumber: sharedTsbIncubation.Media?.LotNumber,
                MediaMaterialName: sharedTsbIncubation.Media?.Material?.MaterialName,
                GptStatus: sharedTsbIncubation.Media?.IsReleasedForUse == true ? "Passed (GPT Conform)" : "Pending",
                SterilityStatus: "Passed",
                IncubatorEquipmentId: sharedTsbIncubation.IncubatorEquipmentId,
                IncubatorCode: sharedTsbIncubation.IncubatorEquipment?.Code ?? _db.EquipmentInventories.FirstOrDefault(e => e.Id == sharedTsbIncubation.IncubatorEquipmentId)?.Code,
                RequiredTemperatureRange: $"{requiredTsbTempMin:0.0} – {requiredTsbTempMax:0.0} °C",
                RequiredDurationRange: $"{requiredTsbHoursMin} – {requiredTsbHoursMax} h",
                Temperature: sharedTsbIncubation.Temperature,
                IncubationDurationHours: sharedTsbIncubation.IncubationEndUtc.HasValue && sharedTsbIncubation.IncubationStartUtc.HasValue
                    ? (int)(sharedTsbIncubation.IncubationEndUtc.Value - sharedTsbIncubation.IncubationStartUtc.Value).TotalHours
                    : null,
                ActualStartUtc: tsbStart,
                MinReadyAt: tsbMinReadyAt,
                ExpectedCompletionUtc: sharedTsbIncubation.IncubationEndUtc,
                CompletedAtUtc: sharedTsbIncubation.CompletedAt,
                StartedByUserId: sharedTsbIncubation.StartedByUserId,
                StartedByUserName: UserName(sharedTsbIncubation.StartedByUserId),
                ApplicableTestCodes: tsbApplicableCodes.Distinct().ToList(),
                ApplicableLocationCount: groupedLocations.Count);
        }
        else
        {
            sharedTsbDto = new SharedTsbStateDto(
                IsStarted: false,
                IsIncubating: false,
                IsCompleted: false,
                IsLocked: false,
                MediaLotId: null,
                MediaLotNumber: null,
                MediaMaterialName: null,
                GptStatus: null,
                SterilityStatus: null,
                IncubatorEquipmentId: null,
                IncubatorCode: null,
                RequiredTemperatureRange: $"{requiredTsbTempMin:0.0} – {requiredTsbTempMax:0.0} °C",
                RequiredDurationRange: $"{requiredTsbHoursMin} – {requiredTsbHoursMax} h",
                Temperature: null,
                IncubationDurationHours: null,
                ActualStartUtc: null,
                MinReadyAt: null,
                ExpectedCompletionUtc: null,
                CompletedAtUtc: null,
                StartedByUserId: null,
                StartedByUserName: null,
                ApplicableTestCodes: tsbApplicableCodes.Distinct().ToList(),
                ApplicableLocationCount: groupedLocations.Count);
        }

        // Build Assigned Test DTOs and evaluate test-specific workflow states
        var assignedTestDtos = new List<SessionAssignedTestDto>();

        foreach (var to in sample.TestOrders)
        {
            testDefs.TryGetValue(to.TestCode, out var def);
            var steps = def?.Steps.OrderBy(s => s.StepOrder).ToList() ?? new List<TestWorkflowStep>();

            var requiresTsb = tsbApplicableCodes.Contains(to.TestCode);
            var toIncubations = incubations.Where(i => i.TestOrderId == to.Id).ToList();
            var toStepResults = stepResults.Where(w => w.TestOrderId == to.Id).ToList();

            var confirmatoryStep = steps.FirstOrDefault(s => s.StepType == StepType.ConfirmatoryPlating);
            var confirmatoryMediaCount = confirmatoryStep?.ConfirmatoryMediaCount ?? (def?.Code.Contains("SALMONELLA", StringComparison.OrdinalIgnoreCase) == true ? 2 : 1);

            var stepDtos = new List<SessionWorkflowStepDto>();
            foreach (var s in steps)
            {
                var inc = toIncubations.FirstOrDefault(i => i.StepName == s.StepName || (requiresTsb && s.StepOrder == 1 && i.StepName.Contains("TSB")));
                var res = toStepResults.FirstOrDefault(r => r.StepName == s.StepName);
                var isDone = (inc != null && inc.CompletedAt.HasValue) || res != null || (requiresTsb && s.StepOrder == 1 && tsbCompleted);
                var outcome = inc?.Outcome ?? (res != null ? "Complete" : (isDone ? "Complete" : null));

                var firstMedia = s.StepMedia.OrderBy(m => m.DisplayOrder).FirstOrDefault();
                stepDtos.Add(new SessionWorkflowStepDto(
                    s.StepOrder,
                    s.StepName,
                    s.StepType.ToString(),
                    null,
                    s.StepType == StepType.BiochemicalTest ? null : s.StepType.ToString(),
                    s.IncubationMinHours > 0 ? s.IncubationMinHours : (firstMedia?.IncubationMinHours ?? 0),
                    s.IncubationMaxHours > 0 ? s.IncubationMaxHours : (firstMedia?.IncubationMaxHours ?? 0),
                    s.TemperatureMin > 0 ? s.TemperatureMin : (firstMedia?.TempMin ?? 0),
                    s.TemperatureMax > 0 ? s.TemperatureMax : (firstMedia?.TempMax ?? 0),
                    isDone,
                    outcome,
                    inc?.CompletedAt ?? res?.SubmittedAtUtc));
            }

            // Determine Test Session State & Result Entry Allowance
            var stateResult = WorkflowStateResolver.Resolve(to, requiresTsb, sharedTsbIncubation, toIncubations, stepDtos, DateTime.UtcNow, requiredTsbHoursMin, steps);
            string testSessionState = stateResult.WorkflowState;
            string testSessionStateDisplay = stateResult.WorkflowStateDisplay;
            bool isResultEntryAllowed = stateResult.IsResultEntryAllowed;
            bool isWorkflowLocked = stateResult.IsWorkflowLocked;
            string? lockReason = stateResult.LockReason;
            string workflowStatus = stateResult.WorkflowStatus;

            var countReading = countTestReadings.FirstOrDefault(c => c.TestOrderId == to.Id);
            CountResultDto? countResultDto = null;
            if (countReading != null)
            {
                var prepUnit = sample.SamplePreparation?.Unit;
                var unit = TestWorkflowEngine.GetCfuUnit(sample.Category, prepUnit);
                countResultDto = new CountResultDto(
                    countReading.PlateReadings,
                    countReading.DilutionFactor,
                    countReading.Average,
                    countReading.CalculatedResult,
                    countReading.ReportedResult,
                    countReading.Status,
                    countReading.HasNonNumericReading,
                    countReading.NonNumericValue,
                    countReading.RequiresReview,
                    unit);
            }

            assignedTestDtos.Add(new SessionAssignedTestDto(
                to.Id,
                to.TestCode,
                def?.DisplayName ?? to.TestCode,
                def?.WorkflowType.ToString() ?? "Observation",
                to.Status.ToString(),
                to.CurrentStep.ToString(),
                to.AssignedAnalystId.HasValue ? UserName(to.AssignedAnalystId) : null,
                requiresTsb,
                testSessionState,
                testSessionStateDisplay,
                isResultEntryAllowed,
                isWorkflowLocked,
                lockReason,
                stepDtos,
                confirmatoryMediaCount,
                workflowStatus,
                countResultDto));
        }

        // Build Result Matrix with Tri-State Cell Model & Confirmation Details
        var matrix = new List<MatrixCellResultDto>();
        var missingResults = new List<MissingResultDto>();
        int completedCount = 0;
        int availableCount = 0;
        int lockedCount = 0;

        foreach (var loc in groupedLocations)
        {
            foreach (var test in assignedTestDtos)
            {
                var isQuantitative = test.WorkflowType.Equals("CountTest", StringComparison.OrdinalIgnoreCase) ||
                                     test.TestCode.Contains("TAMC", StringComparison.OrdinalIgnoreCase) ||
                                     test.TestCode.Contains("TYMC", StringComparison.OrdinalIgnoreCase);
                var resultType = isQuantitative ? "Quantitative" : "Qualitative";

                loc.TestLocationMap.TryGetValue(test.TestCode, out var slocId);
                var sloc = sample.Locations.FirstOrDefault(l => l.Id == slocId);

                string? resCode = null;
                string? resDisplay = null;
                decimal? numVal = null;
                string? status = null;
                DateTime? enteredAt = null;
                string? enteredByUser = null;
                string cellState;
                string? cellLockReason = null;

                int? primaryObsId = null;
                string? primaryObsStr = null;
                bool isEligibleForConfirmation = false;
                string? confirmationStatus = null;
                List<ConfirmatoryPlateObservationDetailDto>? confirmatoryPlates = null;

                if (sloc != null && primaryObsMap.TryGetValue((sloc.Id, test.TestOrderId), out var pObs))
                {
                    primaryObsId = pObs.Id;
                    primaryObsStr = pObs.GrowthObservation.ToString();
                    isEligibleForConfirmation = pObs.GrowthObservation != GrowthObservation.NoGrowth;

                    if (pObs.ConfirmatoryPlateObservations.Count > 0)
                    {
                        confirmatoryPlates = pObs.ConfirmatoryPlateObservations
                            .OrderBy(p => p.MediumIndex)
                            .Select(p => new ConfirmatoryPlateObservationDetailDto(
                                p.Id,
                                p.MediumIndex,
                                p.MaterialId,
                                p.Material?.MaterialName,
                                p.Observation.ToString(),
                                p.ExpectedAppearanceSnapshot,
                                p.RecordedAtUtc,
                                UserName(p.RecordedByUserId)))
                            .ToList();
                    }

                    if (pObs.GrowthObservation == GrowthObservation.NoGrowth)
                    {
                        confirmationStatus = "NotApplicable";
                    }
                    else if (confirmatoryPlates != null && confirmatoryPlates.Count >= test.ConfirmatoryMediaCount)
                    {
                        confirmationStatus = "Completed";
                    }
                    else if (isEligibleForConfirmation)
                    {
                        confirmationStatus = "Eligible";
                    }
                }

                if (sloc != null && (!string.IsNullOrWhiteSpace(sloc.ReportedResult) || sloc.CFUResult.HasValue) && sloc.Status != "PendingConfirmation")
                {
                    resDisplay = sloc.ReportedResult;
                    numVal = sloc.CFUResult ?? sloc.CalculatedResult;
                    status = sloc.Status ?? "Entered";
                    enteredAt = sloc.EnteredAt;
                    enteredByUser = UserName(sloc.EnteredByUserId);

                    if (isQuantitative)
                    {
                        resCode = numVal?.ToString() ?? sloc.ReportedResult;
                    }
                    else
                    {
                        resCode = (sloc.ReportedResult?.Contains("Detected (+)") == true || sloc.ReportedResult?.Equals("Detected", StringComparison.OrdinalIgnoreCase) == true)
                            ? "DETECTED"
                            : "NOT_DETECTED";
                    }

                    cellState = "COMPLETED";
                    completedCount++;
                }
                else
                {
                    if (test.IsResultEntryAllowed)
                    {
                        cellState = "AVAILABLE";
                        availableCount++;
                        missingResults.Add(new MissingResultDto(loc.LocationName, test.TestCode, test.DisplayName));
                    }
                    else
                    {
                        cellState = "LOCKED_PREREQUISITE";
                        cellLockReason = test.LockReason ?? "Locked until prerequisite is complete";
                        lockedCount++;
                        missingResults.Add(new MissingResultDto(loc.LocationName, test.TestCode, test.DisplayName));
                    }
                }

                matrix.Add(new MatrixCellResultDto(
                    slocId != 0 ? slocId : loc.PrimarySampleLocationId,
                    test.TestCode,
                    loc.LocationName,
                    resCode,
                    resDisplay,
                    numVal,
                    resultType,
                    status,
                    enteredAt,
                    enteredByUser,
                    IsEditable: cellState == "AVAILABLE",
                    CellState: cellState,
                    LockReason: cellLockReason,
                    PrimaryObservationId: primaryObsId,
                    PrimaryObservation: primaryObsStr,
                    IsEligibleForConfirmation: isEligibleForConfirmation,
                    ConfirmationStatus: confirmationStatus,
                    ConfirmatoryPlates: confirmatoryPlates));
            }
        }

        int totalRequired = groupedLocations.Count * assignedTestDtos.Count;
        int pendingCount = availableCount; // Only available and empty cells are truly pending entry

        // Overall Session Status Computation
        string sessionStatus = "NOT_STARTED";
        string sessionStatusDisplay = "Not Started";

        if (completedCount == totalRequired && totalRequired > 0)
        {
            sessionStatus = sample.Status == SampleStatus.Approved ? "COMPLETED" : "READY_FOR_REVIEW";
            sessionStatusDisplay = sample.Status == SampleStatus.Approved ? "Completed & Approved" : "Ready for Technical Review";
        }
        else if (completedCount > 0)
        {
            sessionStatus = "RESULTS_IN_PROGRESS";
            sessionStatusDisplay = $"Results In Progress ({completedCount}/{totalRequired})";
        }
        else if (tsbIncubating)
        {
            sessionStatus = "TSB_INCUBATING";
            sessionStatusDisplay = "TSB Enrichment Incubating";
        }
        else if (tsbCompleted)
        {
            sessionStatus = "TSB_COMPLETED";
            sessionStatusDisplay = "TSB Complete — Downstream Testing";
        }
        else if (tsbApplicableCodes.Count == 0)
        {
            sessionStatus = "DOWNSTREAM_TESTING";
            sessionStatusDisplay = "Testing In Progress";
        }

        var programName = sample.Category switch
        {
            SampleCategory.AfterCleaning => "After Cleaning Monitoring",
            SampleCategory.EnvironmentalMonitoring => "Environmental Monitoring",
            SampleCategory.Water => "Water Monitoring Plan",
            SampleCategory.FinishedProduct => "Finished Product Testing",
            _ => $"{sample.Category} Testing"
        };

        var deptName = sample.Department?.Name
            ?? sample.Machine?.Name
            ?? sample.WaterDepartment?.Name
            ?? sample.WaterSamplingPoint?.Location
            ?? sample.Item?.Name
            ?? "Laboratory Workspace";

        return new PathogenTestingSessionDto(
            sessionId,
            sample.Id,
            sample.ReferenceNumber,
            sample.Category.ToString(),
            programName,
            deptName,
            sample.ControlNumber,
            sample.BatchNumber,
            sample.ReceivedAt,
            sessionStatus,
            sessionStatusDisplay,
            groupedLocations.Count,
            assignedTestDtos.Count,
            totalRequired,
            completedCount,
            availableCount,
            lockedCount,
            pendingCount,
            groupedLocations,
            assignedTestDtos,
            sharedTsbDto,
            matrix,
            missingResults);
    }

    public async Task<SharedTsbStateDto> StartSharedTsbAsync(int sampleId, StartSharedTsbRequest request, int userId)
    {
        var sample = await _db.Samples
            .Include(s => s.TestOrders)
            .FirstOrDefaultAsync(s => s.Id == sampleId)
            ?? throw new InvalidOperationException($"Sample #{sampleId} not found.");

        var media = await _db.Media
            .Include(m => m.Material)
            .FirstOrDefaultAsync(m => m.Id == request.MediaLotId)
            ?? throw new InvalidOperationException($"Media #{request.MediaLotId} not found.");

        if (media.ExpiryDate.Date < DateTime.UtcNow.Date)
            throw new WorkflowStepException("MediaExpired", $"Media lot #{media.LotNumber} expired on {media.ExpiryDate:yyyy-MM-dd}.");

        var incubator = await _db.EquipmentInventories.FirstOrDefaultAsync(e => e.Id == request.IncubatorEquipmentId)
            ?? throw new InvalidOperationException($"Incubator #{request.IncubatorEquipmentId} not found.");

        var testCodes = sample.TestOrders.Select(t => t.TestCode).Distinct().ToList();
        var testDefs = await _db.TestDefinitions
            .Include(t => t.Steps)
            .Where(t => testCodes.Contains(t.Code))
            .ToDictionaryAsync(t => t.Code);

        var tsbOrders = new List<TestOrder>();
        decimal tsbTempMin = 30;
        decimal tsbTempMax = 35;
        int tsbDurationMin = 18;
        int tsbDurationMax = 24;

        foreach (var to in sample.TestOrders)
        {
            if (testDefs.TryGetValue(to.TestCode, out var def))
            {
                // SelectiveBroth (e.g. MBP for E.coli, RVS for Salmonella) is
                // species-specific and never shares this generic TSB lot -
                // matching it here was the bug that let a shared TSB batch
                // land on the wrong step. Only the true, common broth
                // enrichment step (or an explicitly TSB-named one) qualifies.
                var tsbStep = def.Steps.FirstOrDefault(s =>
                    s.StepType is StepType.BrothEnrichment ||
                    s.StepName.Contains("TSB", StringComparison.OrdinalIgnoreCase));

                if (tsbStep != null)
                {
                    tsbOrders.Add(to);
                    if (tsbStep.TemperatureMin > 0) tsbTempMin = tsbStep.TemperatureMin;
                    if (tsbStep.TemperatureMax > 0) tsbTempMax = tsbStep.TemperatureMax;
                    if (tsbStep.IncubationMinHours > 0) tsbDurationMin = tsbStep.IncubationMinHours;
                    if (tsbStep.IncubationMaxHours > 0) tsbDurationMax = tsbStep.IncubationMaxHours;
                }
            }
        }

        if (tsbOrders.Count == 0)
            throw new WorkflowStepException("NoTsbTests", "None of the assigned tests on this sample require TSB enrichment.");

        var startUtc = request.IncubationStartUtc ?? DateTime.UtcNow;
        var endUtc = startUtc.AddHours(tsbDurationMax);
        var targetTemp = $"{(tsbTempMin + tsbTempMax) / 2m:0.0} °C ({tsbTempMin:0.0} – {tsbTempMax:0.0} °C)";

        var toIds = tsbOrders.Select(t => t.Id).ToList();
        var existingIncubations = await _db.Incubations
            .Where(i => i.TestOrderId.HasValue && toIds.Contains(i.TestOrderId.Value) && (i.StepName.Contains("TSB") || i.StepName.Contains("Enrichment") || i.StepName == "Broth Enrichment"))
            .ToListAsync();

        if (existingIncubations.Count > 0)
            _db.Incubations.RemoveRange(existingIncubations);

        foreach (var to in tsbOrders)
        {
            var def = testDefs.TryGetValue(to.TestCode, out var d) ? d : null;
            var tsbStep = def?.Steps.FirstOrDefault(s =>
                s.StepType is StepType.BrothEnrichment ||
                s.StepName.Contains("TSB", StringComparison.OrdinalIgnoreCase));

            var inc = new Incubation
            {
                TestOrderId = to.Id,
                StepNumber = tsbStep?.StepOrder ?? 1,
                StepName = tsbStep?.StepName ?? "Broth enrichment",
                MediaId = request.MediaLotId,
                IncubatorEquipmentId = request.IncubatorEquipmentId,
                Temperature = targetTemp,
                Duration = $"{tsbDurationMin} – {tsbDurationMax} hours",
                StartedAt = startUtc,
                IncubationStartUtc = startUtc,
                IncubationEndUtc = endUtc,
                ExpectedReadingAt = endUtc,
                StartedByUserId = userId
            };
            _db.Incubations.Add(inc);

            to.CurrentStep = WorkflowStep.Incubating;
            to.Status = ApprovalStatus.Pending;

            _db.WorkflowHistories.Add(new WorkflowHistory
            {
                TestOrderId = to.Id,
                FromStep = WorkflowStep.Running,
                ToStep = WorkflowStep.Incubating,
                Note = $"Shared TSB Enrichment started: Media #{media.LotNumber} ({media.Material?.MaterialName ?? "TSB"}), Incubator {incubator.Code}, {tsbDurationMin}-{tsbDurationMax}h @ {targetTemp}.",
                PerformedByUserId = userId,
                Timestamp = DateTime.UtcNow
            });
        }

        await _db.SaveChangesAsync();

        foreach (var to in tsbOrders)
        {
            var def = testDefs.TryGetValue(to.TestCode, out var d) ? d : null;
            var tsbStep = def?.Steps.FirstOrDefault(s =>
                s.StepType is StepType.BrothEnrichment ||
                s.StepName.Contains("TSB", StringComparison.OrdinalIgnoreCase));
            var stepName = tsbStep?.StepName ?? "Broth Enrichment";

            var inc = await _db.Incubations
                .Where(i => i.TestOrderId == to.Id)
                .OrderByDescending(i => i.StartedAt)
                .FirstOrDefaultAsync();

            if (inc != null)
            {
                var stepNamesToAdd = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Broth Enrichment", "Broth enrichment", stepName };
                foreach (var name in stepNamesToAdd)
                {
                    var exists = await _db.WorkflowStepResults.AnyAsync(r => r.TestOrderId == to.Id && r.StepName == name);
                    if (!exists)
                    {
                        _db.WorkflowStepResults.Add(new WorkflowStepResult
                        {
                            TestOrderId = to.Id,
                            StepName = name,
                            StepType = StepType.BrothEnrichment,
                            IncubationId = inc.Id,
                            IsSharedSessionStep = true,
                            SubmittedByUserId = userId,
                            SubmittedAtUtc = DateTime.UtcNow
                        });
                    }
                }
            }
        }

        await _db.SaveChangesAsync();

        var session = await GetSessionAsync(sampleId);
        return session!.SharedTsb;
    }

    public async Task<List<EligibleLocationForConfirmationDto>> GetEligibleLocationsForConfirmationAsync(
        int sampleId,
        int? testOrderId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _db.LocationPathogenObservations
            .Include(o => o.SampleLocation)
                .ThenInclude(l => l!.RoomTestConfiguration!)
                .ThenInclude(c => c.Room)
            .Include(o => o.SampleLocation)
                .ThenInclude(l => l!.MachinePartConfiguration!)
                .ThenInclude(c => c.MachinePart)
            .Include(o => o.SampleLocation)
                .ThenInclude(l => l!.WaterSamplingPoint)
            .Include(o => o.TestOrder)
            .Where(o => o.SampleLocation != null && o.SampleLocation.SampleId == sampleId)
            .Where(o => o.GrowthObservation == GrowthObservation.GrowthNonConforming || o.GrowthObservation == GrowthObservation.GrowthConforming);

        if (testOrderId.HasValue)
        {
            query = query.Where(o => o.TestOrderId == testOrderId.Value);
        }

        var observations = await query.ToListAsync(cancellationToken);
        var testCodes = observations.Select(o => o.TestOrder?.TestCode ?? "").Where(c => !string.IsNullOrEmpty(c)).Distinct().ToList();
        var testDefs = await _db.TestDefinitions
            .Include(d => d.Steps)
            .Where(d => testCodes.Contains(d.Code))
            .ToDictionaryAsync(d => d.Code, cancellationToken);

        return observations.Select(o =>
        {
            var locName = o.SampleLocation?.RoomTestConfiguration?.Room?.Name
                ?? o.SampleLocation?.MachinePartConfiguration?.MachinePart?.Name
                ?? o.SampleLocation?.WaterSamplingPoint?.Location
                ?? $"Location #{o.SampleLocationId}";

            testDefs.TryGetValue(o.TestOrder?.TestCode ?? "", out var testDef);
            var confStep = testDef?.Steps.FirstOrDefault(s => s.StepType == StepType.ConfirmatoryPlating);
            var reqCount = confStep?.ConfirmatoryMediaCount ?? (o.TestOrder?.TestCode.Contains("SALMONELLA", StringComparison.OrdinalIgnoreCase) == true ? 2 : 1);

            return new EligibleLocationForConfirmationDto(
                LocationId: o.SampleLocationId,
                PrimaryObservationId: o.Id,
                LocationName: locName,
                TestOrderId: o.TestOrderId,
                TestCode: o.TestOrder?.TestCode ?? "UNKNOWN",
                TestDisplayName: testDef?.DisplayName ?? o.TestOrder?.TestCode ?? "Unknown Test",
                GrowthObservation: o.GrowthObservation,
                GrowthObservationDisplay: o.GrowthObservation == GrowthObservation.GrowthConforming ? "Growth Conforming (Presumptive +)" : "Growth Non-Conforming",
                RequiredConfirmatoryMediaCount: reqCount);
        }).ToList();
    }

    public async Task<PathogenTestingSessionDto> SavePrimaryObservationsAsync(
        int sampleId,
        SavePrimaryObservationsRequest request,
        int userId,
        CancellationToken cancellationToken = default)
    {
        var session = await GetSessionAsync(sampleId)
            ?? throw new InvalidOperationException($"Session for sample #{sampleId} not found.");

        var sample = await _db.Samples
            .Include(s => s.TestOrders)
            .Include(s => s.Locations)
            .FirstOrDefaultAsync(s => s.Id == sampleId, cancellationToken)
            ?? throw new InvalidOperationException($"Sample #{sampleId} not found.");

        var testOrdersByCode = sample.TestOrders.ToDictionary(t => t.TestCode);
        var locationsById = sample.Locations.ToDictionary(l => l.Id);
        var testStateByCode = session.AssignedTests.ToDictionary(t => t.TestCode);

        foreach (var obs in request.Observations)
        {
            if (testStateByCode.TryGetValue(obs.TestCode, out var testInfo) && !testInfo.IsResultEntryAllowed)
            {
                throw new WorkflowStepException(
                    "PrerequisiteNotMet",
                    $"Cannot enter primary observations for {obs.TestCode}: TSB incubation or required workflow steps are still in progress.");
            }

            if (!testOrdersByCode.TryGetValue(obs.TestCode, out var order))
                continue;

            if (locationsById.TryGetValue(obs.SampleLocationId, out var loc))
            {
                await _locationObsService.RecordPrimaryObservationAsync(
                    loc.Id,
                    order.Id,
                    obs.Observation,
                    obs.SelectiveMediaSnapshot,
                    userId,
                    cancellationToken);

                if (obs.Observation == GrowthObservation.NoGrowth)
                {
                    loc.ReportedResult = "Not Detected (-)";
                    loc.Status = "Absent";
                    loc.CFUResult = null;
                }
                else if (obs.Observation == GrowthObservation.GrowthConforming)
                {
                    loc.ReportedResult = "Growth Conforming (Presumptive +)";
                    loc.Status = "PendingConfirmation";
                    loc.CFUResult = null;
                }
                else if (obs.Observation == GrowthObservation.GrowthNonConforming)
                {
                    loc.ReportedResult = "Growth Non-Conforming";
                    loc.Status = "PendingConfirmation";
                    loc.CFUResult = null;
                }

                loc.EnteredAt = DateTime.UtcNow;
                loc.EnteredByUserId = userId;
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        return (await GetSessionAsync(sampleId))!;
    }

    public async Task<PathogenTestingSessionDto> StartSharedConfirmatorySetupAsync(
        int sampleId,
        BatchConfirmatorySetupRequest request,
        int userId,
        CancellationToken cancellationToken = default)
    {
        var sample = await _db.Samples
            .Include(s => s.TestOrders)
            .FirstOrDefaultAsync(s => s.Id == sampleId, cancellationToken)
            ?? throw new InvalidOperationException($"Sample #{sampleId} not found.");

        var testOrder = sample.TestOrders.FirstOrDefault(t => t.Id == request.TestOrderId)
            ?? throw new InvalidOperationException($"TestOrder #{request.TestOrderId} not found.");

        var testDef = await _db.TestDefinitions
            .Include(d => d.Steps)
            .FirstOrDefaultAsync(d => d.Code == testOrder.TestCode, cancellationToken);

        var confirmatoryStep = testDef?.Steps.FirstOrDefault(s => s.StepType == StepType.ConfirmatoryPlating);
        var requiredMediaCount = confirmatoryStep?.ConfirmatoryMediaCount ?? (testOrder.TestCode.Contains("SALMONELLA", StringComparison.OrdinalIgnoreCase) ? 2 : 1);

        if (request.MediaMaterialIds.Count != requiredMediaCount)
        {
            throw new WorkflowStepException(
                "InvalidMediaCount",
                $"Pathogen {testOrder.TestCode} requires exactly {requiredMediaCount} confirmatory media; received {request.MediaMaterialIds.Count}.");
        }

        // Validate that selected locations exist and have growth
        var eligible = await GetEligibleLocationsForConfirmationAsync(sampleId, request.TestOrderId, cancellationToken);
        var eligibleIds = eligible.Select(e => e.LocationId).ToHashSet();

        foreach (var locId in request.LocationIds)
        {
            if (!eligibleIds.Contains(locId))
            {
                throw new WorkflowStepException(
                    "LocationNotEligible",
                    $"Location #{locId} does not have a GrowthConforming or GrowthNonConforming primary observation and cannot proceed to confirmation.");
            }
        }

        var incubator = await _db.EquipmentInventories.FirstOrDefaultAsync(e => e.Id == request.IncubatorEquipmentId, cancellationToken)
            ?? throw new InvalidOperationException($"Incubator #{request.IncubatorEquipmentId} not found.");

        var startUtc = request.IncubationStartUtc ?? DateTime.UtcNow;
        var endUtc = startUtc.AddHours(confirmatoryStep?.IncubationMaxHours > 0 ? confirmatoryStep.IncubationMaxHours : 24);

        var inc = new Incubation
        {
            TestOrderId = testOrder.Id,
            StepNumber = confirmatoryStep?.StepOrder ?? 4,
            StepName = confirmatoryStep?.StepName ?? "Confirmatory Plating",
            MediaId = request.MediaLotIds?.FirstOrDefault() ?? 0,
            IncubatorEquipmentId = request.IncubatorEquipmentId,
            Temperature = $"{confirmatoryStep?.TemperatureMin ?? 35:0.0} – {confirmatoryStep?.TemperatureMax ?? 37:0.0} °C",
            Duration = $"{confirmatoryStep?.IncubationMinHours ?? 18} – {confirmatoryStep?.IncubationMaxHours ?? 24} hours",
            StartedAt = startUtc,
            IncubationStartUtc = startUtc,
            IncubationEndUtc = endUtc,
            ExpectedReadingAt = endUtc,
            StartedByUserId = userId
        };
        _db.Incubations.Add(inc);

        testOrder.CurrentStep = WorkflowStep.Incubating;
        _db.WorkflowHistories.Add(new WorkflowHistory
        {
            TestOrderId = testOrder.Id,
            FromStep = WorkflowStep.Running,
            ToStep = WorkflowStep.Incubating,
            Note = $"Shared Confirmatory Plating incubation started for {request.LocationIds.Count} location(s) on Incubator {incubator.Code}.",
            PerformedByUserId = userId,
            Timestamp = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);
        return (await GetSessionAsync(sampleId))!;
    }

    public async Task<PathogenTestingSessionDto> SaveBatchConfirmatoryPlateReadingsAsync(
        int sampleId,
        SaveBatchConfirmatoryPlateReadingsRequest request,
        int userId,
        CancellationToken cancellationToken = default)
    {
        var primaryObsIds = request.Readings.Select(r => r.LocationPathogenObservationId).Distinct().ToList();
        var primaryObsList = await _db.LocationPathogenObservations
            .Include(o => o.SampleLocation)
            .Include(o => o.TestOrder)
            .Include(o => o.ConfirmatoryPlateObservations)
            .Where(o => primaryObsIds.Contains(o.Id))
            .ToListAsync(cancellationToken);

        var testCodes = primaryObsList.Select(o => o.TestOrder?.TestCode ?? "").Where(c => !string.IsNullOrEmpty(c)).Distinct().ToList();
        var testDefs = await _db.TestDefinitions
            .Include(d => d.Steps)
            .Where(d => testCodes.Contains(d.Code))
            .ToDictionaryAsync(d => d.Code, cancellationToken);

        var primaryObsMap = primaryObsList.ToDictionary(o => o.Id);

        foreach (var reading in request.Readings)
        {
            if (!primaryObsMap.TryGetValue(reading.LocationPathogenObservationId, out var primaryObs))
                continue;

            testDefs.TryGetValue(primaryObs.TestOrder?.TestCode ?? "", out var testDef);
            var targetOrganismId = testDef?.Steps
                .FirstOrDefault(s => s.StepType == StepType.ConfirmatoryPlating)?.TargetOrganismId ?? 0;

            string? snapshot = null;
            if (_appearanceSnapshot != null && targetOrganismId > 0)
            {
                snapshot = await _appearanceSnapshot.GetExpectedAppearanceSnapshotAsync(reading.MaterialId, targetOrganismId, cancellationToken);
            }

            var existingPlate = primaryObs.ConfirmatoryPlateObservations
                .FirstOrDefault(p => p.MaterialId == reading.MaterialId && p.MediumIndex == reading.MediumIndex);

            if (existingPlate != null)
            {
                existingPlate.Observation = reading.Observation;
                existingPlate.RecordedAtUtc = DateTime.UtcNow;
                existingPlate.RecordedByUserId = userId;
                if (string.IsNullOrEmpty(existingPlate.ExpectedAppearanceSnapshot) && snapshot != null)
                    existingPlate.ExpectedAppearanceSnapshot = snapshot;
            }
            else
            {
                var newPlate = new ConfirmatoryPlateObservation
                {
                    LocationPathogenObservationId = primaryObs.Id,
                    SampleLocationId = primaryObs.SampleLocationId,
                    MaterialId = reading.MaterialId,
                    MediumIndex = reading.MediumIndex,
                    Observation = reading.Observation,
                    ExpectedAppearanceSnapshot = snapshot,
                    RecordedByUserId = userId,
                    RecordedAtUtc = DateTime.UtcNow
                };
                _db.ConfirmatoryPlateObservations.Add(newPlate);
                if (!primaryObs.ConfirmatoryPlateObservations.Contains(newPlate))
                {
                    primaryObs.ConfirmatoryPlateObservations.Add(newPlate);
                }
            }
        }

        // Apply ConfirmationAgreementEvaluator across all configured media for each primary observation
        foreach (var primaryObs in primaryObsList)
        {
            testDefs.TryGetValue(primaryObs.TestOrder?.TestCode ?? "", out var testDef);
            var confStep = testDef?.Steps.FirstOrDefault(s => s.StepType == StepType.ConfirmatoryPlating);
            var reqCount = confStep?.ConfirmatoryMediaCount ?? (primaryObs.TestOrder?.TestCode.Contains("SALMONELLA", StringComparison.OrdinalIgnoreCase) == true ? 2 : 1);

            var plates = primaryObs.ConfirmatoryPlateObservations
                .GroupBy(p => (p.MaterialId, p.MediumIndex))
                .Select(g => g.Last())
                .OrderBy(p => p.MediumIndex)
                .ToList();

            if (plates.Count >= reqCount && primaryObs.SampleLocation != null)
            {
                var outcome = _agreementEvaluator.EvaluateAgreement(plates.Take(reqCount).ToList(), reqCount);
                if (outcome == ConfirmationResult.Detected)
                {
                    primaryObs.SampleLocation.ReportedResult = "Detected (+)";
                    primaryObs.SampleLocation.Status = "Detected";
                }
                else if (outcome == ConfirmationResult.NotDetected)
                {
                    primaryObs.SampleLocation.ReportedResult = "Not Detected (-)";
                    primaryObs.SampleLocation.Status = "Absent";
                }
                else // Inconclusive
                {
                    primaryObs.SampleLocation.ReportedResult = "Inconclusive (Retest)";
                    primaryObs.SampleLocation.Status = "Inconclusive";
                }

                primaryObs.SampleLocation.EnteredAt = DateTime.UtcNow;
                primaryObs.SampleLocation.EnteredByUserId = userId;
            }
        }

        if (!string.IsNullOrWhiteSpace(request.BiochemicalComment))
        {
            var distinctTestOrders = primaryObsList.Select(o => o.TestOrder).Where(t => t != null).Distinct().ToList();
            foreach (var to in distinctTestOrders)
            {
                _db.WorkflowHistories.Add(new WorkflowHistory
                {
                    TestOrderId = to!.Id,
                    FromStep = to.CurrentStep,
                    ToStep = to.CurrentStep,
                    Note = $"Biochemical Supporting Observation: {request.BiochemicalComment.Trim()}",
                    PerformedByUserId = userId,
                    Timestamp = DateTime.UtcNow
                });
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        return (await GetSessionAsync(sampleId))!;
    }

    public async Task<PathogenTestingSessionDto> SaveResultMatrixAsync(int sampleId, SaveResultMatrixRequest request, int userId)
    {
        var session = await GetSessionAsync(sampleId)
            ?? throw new InvalidOperationException($"Session for sample #{sampleId} not found.");

        var sample = await _db.Samples
            .Include(s => s.TestOrders)
            .Include(s => s.Locations).ThenInclude(l => l.SamplingConfiguration)
            .FirstOrDefaultAsync(s => s.Id == sampleId)
            ?? throw new InvalidOperationException($"Sample #{sampleId} not found.");

        var testOrdersByCode = sample.TestOrders.ToDictionary(t => t.TestCode);
        var locationsById = sample.Locations.ToDictionary(l => l.Id);
        var testStateByCode = session.AssignedTests.ToDictionary(t => t.TestCode);

        // Step-gating validation: check that every submitted result cell is allowed by its test's workflow prerequisites
        foreach (var cell in request.Cells)
        {
            if (testStateByCode.TryGetValue(cell.TestCode, out var testInfo))
            {
                if (!testInfo.IsResultEntryAllowed)
                {
                    throw new WorkflowStepException(
                        "PrerequisiteNotMet",
                        $"Cannot enter results for {cell.TestCode}: TSB incubation or required workflow steps are still in progress ({testInfo.TestSessionStateDisplay}).");
                }
            }

            if (!testOrdersByCode.TryGetValue(cell.TestCode, out var order))
                continue;

            if (locationsById.TryGetValue(cell.SampleLocationId, out var loc))
            {
                var isQuantitative = cell.ResultType.Equals("Quantitative", StringComparison.OrdinalIgnoreCase);
                if (isQuantitative)
                {
                    loc.CFUResult = cell.NumericValue;
                    loc.CalculatedResult = cell.NumericValue;
                    loc.ReportedResult = cell.ResultDisplay ?? $"{cell.NumericValue} CFU";

                    // Was previously hardcoded to the literal "Conform" -
                    // a status string nothing else in the app recognizes
                    // (everywhere else uses the WithinLimits/AlertLimit
                    // Exceeded/ActionLimitExceeded/OutOfSpecification
                    // ladder), and never actually compared the value
                    // against a limit at all. Mirrors
                    // TestWorkflowEngine.RecordWaterBatchReadingsAsync's
                    // Compare(...) + limit-snapshot pattern.
                    var alertLimit = loc.SamplingConfiguration?.AlertLimit;
                    var actionLimit = loc.SamplingConfiguration?.ActionLimit;
                    var specLimit = loc.SamplingConfiguration?.SpecLimit;
                    loc.AlertLimit = alertLimit;
                    loc.ActionLimit = actionLimit;
                    loc.SpecLimit = specLimit;
                    loc.Status = cell.NumericValue.HasValue
                        ? CompareAgainstLimits(cell.NumericValue.Value, alertLimit, actionLimit, specLimit)
                        : null;
                }
                else
                {
                    var isDetected = cell.ResultCode == "DETECTED" || cell.ResultDisplay?.Contains("Detected (+)") == true;
                    loc.ReportedResult = isDetected ? "Detected (+)" : "Not Detected (-)";
                    loc.Status = isDetected ? "Detected" : "Absent";
                    loc.CFUResult = null;

                    // Also record primary observation record for data integrity
                    await _locationObsService.RecordPrimaryObservationAsync(
                        loc.Id,
                        order.Id,
                        isDetected ? GrowthObservation.GrowthConforming : GrowthObservation.NoGrowth,
                        null,
                        userId);
                }

                loc.EnteredAt = DateTime.UtcNow;
                loc.EnteredByUserId = userId;
            }
        }

        await _db.SaveChangesAsync();
        return (await GetSessionAsync(sampleId))!;
    }

    // Same semantics as TestWorkflowEngine's private Compare(...) - kept
    // as a separate copy rather than shared, since that method is
    // internal to a different service and already covered by its own
    // tests; duplicating this small pure comparison is lower risk than
    // reaching into another service's implementation detail.
    private static string CompareAgainstLimits(decimal value, string? alert, string? action, string? spec)
    {
        var hasSpec = decimal.TryParse(spec, out var specLimit);
        if (hasSpec && value > specLimit) return "OutOfSpecification";
        var hasAction = decimal.TryParse(action, out var actionLimit);
        if (hasAction && value > actionLimit) return "ActionLimitExceeded";
        var hasAlert = decimal.TryParse(alert, out var alertLimit);
        if (hasAlert && value > alertLimit) return "AlertLimitExceeded";
        if (!hasSpec && !hasAction && !hasAlert) return "LimitsNotConfigured";
        return "WithinLimits";
    }

    public async Task<PathogenTestingSessionDto> CompleteSessionAsync(int sampleId, int userId)
    {
        var session = await GetSessionAsync(sampleId)
            ?? throw new InvalidOperationException($"Session for sample #{sampleId} not found.");

        // 1. Completeness Validation
        if (session.MissingResults.Count > 0)
        {
            var missingList = string.Join(", ", session.MissingResults.Take(5).Select(m => $"{m.LocationName} ({m.TestCode})"));
            var extra = session.MissingResults.Count > 5 ? $" and {session.MissingResults.Count - 5} more" : "";
            throw new WorkflowStepException(
                "IncompleteResults",
                $"Cannot complete testing session: {session.MissingResults.Count} required Location × Test result(s) are missing ({missingList}{extra}).");
        }

        // 2. Validate that no test is still in an incomplete incubation or prerequisite state
        var incompleteTests = session.AssignedTests.Where(t => t.TestSessionState == "TSB_INCUBATING" || !t.IsResultEntryAllowed).ToList();
        if (incompleteTests.Count > 0)
        {
            throw new WorkflowStepException(
                "PrerequisitesIncomplete",
                $"Cannot complete testing session: Assigned tests {string.Join(", ", incompleteTests.Select(t => t.TestCode))} have incomplete workflow prerequisites.");
        }

        var sample = await _db.Samples
            .Include(s => s.TestOrders)
            .FirstOrDefaultAsync(s => s.Id == sampleId)
            ?? throw new InvalidOperationException($"Sample #{sampleId} not found.");

        // 3. Mark TestOrders as completed / ready for review
        foreach (var order in sample.TestOrders)
        {
            order.CurrentStep = WorkflowStep.Ready;
            order.Status = ApprovalStatus.Pending;

            _db.WorkflowHistories.Add(new WorkflowHistory
            {
                TestOrderId = order.Id,
                FromStep = WorkflowStep.Running,
                ToStep = WorkflowStep.Ready,
                Note = "Testing session completed: all location results entered and verified.",
                PerformedByUserId = userId,
                Timestamp = DateTime.UtcNow
            });
        }

        sample.Status = SampleStatus.UnderReview;
        sample.ReviewedAt = null;

        await _db.SaveChangesAsync();
        return (await GetSessionAsync(sampleId))!;
    }

    public async Task<PathogenTestingSessionDto> ResetSessionAsync(int sampleId, string? reason, int userId)
    {
        var sample = await _db.Samples
            .Include(s => s.TestOrders)
            .Include(s => s.Locations)
            .FirstOrDefaultAsync(s => s.Id == sampleId)
            ?? throw new InvalidOperationException($"Sample #{sampleId} not found.");

        var testOrderIds = sample.TestOrders.Select(t => t.Id).ToList();
        var locationIds = sample.Locations.Select(l => l.Id).ToList();
        var resetReason = string.IsNullOrWhiteSpace(reason) ? "Analyst requested session workflow reset" : reason.Trim();

        // 1. Delete Location Pathogen Observations & Confirmatory Plate Observations
        var locObs = await _db.LocationPathogenObservations
            .Include(o => o.ConfirmatoryPlateObservations)
            .Where(o => (testOrderIds.Contains(o.TestOrderId)) ||
                        (locationIds.Contains(o.SampleLocationId)))
            .ToListAsync();

        foreach (var lo in locObs)
        {
            if (lo.ConfirmatoryPlateObservations.Count > 0)
            {
                _db.ConfirmatoryPlateObservations.RemoveRange(lo.ConfirmatoryPlateObservations);
            }
        }
        if (locObs.Count > 0) _db.LocationPathogenObservations.RemoveRange(locObs);

        // 2. Delete Confirmatory Media Selections & WorkflowStepResults
        var wsrList = await _db.WorkflowStepResults
            .Where(w => testOrderIds.Contains(w.TestOrderId))
            .ToListAsync();
        var wsrIds = wsrList.Select(w => w.Id).ToList();

        var confSelections = await _db.ConfirmatoryMediaSelections
            .Where(c => wsrIds.Contains(c.WorkflowStepResultId))
            .ToListAsync();
        if (confSelections.Count > 0) _db.ConfirmatoryMediaSelections.RemoveRange(confSelections);

        if (wsrList.Count > 0) _db.WorkflowStepResults.RemoveRange(wsrList);

        // 3. Delete PathogenObservations & CountTestReadings
        var pathObs = await _db.PathogenObservations
            .Where(p => testOrderIds.Contains(p.TestOrderId))
            .ToListAsync();
        if (pathObs.Count > 0) _db.PathogenObservations.RemoveRange(pathObs);

        var countReadings = await _db.CountTestReadings
            .Where(c => testOrderIds.Contains(c.TestOrderId))
            .ToListAsync();
        if (countReadings.Count > 0) _db.CountTestReadings.RemoveRange(countReadings);

        // 4. Delete Results
        var results = await _db.Results
            .Where(r => testOrderIds.Contains(r.TestOrderId))
            .ToListAsync();
        if (results.Count > 0) _db.Results.RemoveRange(results);

        // 5. Delete Incubations
        var incubations = await _db.Incubations
            .Where(i => i.TestOrderId.HasValue && testOrderIds.Contains(i.TestOrderId.Value))
            .ToListAsync();
        if (incubations.Count > 0) _db.Incubations.RemoveRange(incubations);

        // 6. Reset Sample Locations
        foreach (var loc in sample.Locations)
        {
            loc.CFUResult = null;
        }

        // 7. Reset TestOrders
        foreach (var order in sample.TestOrders)
        {
            order.CurrentStep = WorkflowStep.Waiting;
            order.Status = ApprovalStatus.Pending;

            _db.WorkflowHistories.Add(new WorkflowHistory
            {
                TestOrderId = order.Id,
                FromStep = WorkflowStep.Running,
                ToStep = WorkflowStep.Waiting,
                Note = $"Testing session and workflow steps reset. Reason: {resetReason}",
                PerformedByUserId = userId,
                Timestamp = DateTime.UtcNow
            });
        }

        // 8. Reset Sample Status
        if (sample.Status == SampleStatus.InTesting || sample.Status == SampleStatus.Rejected || sample.Status == SampleStatus.UnderReview)
        {
            sample.Status = SampleStatus.Received;
        }

        // 9. Audit Log
        _db.AuditLogs.Add(new AuditLog
        {
            EntityName = "Sample",
            EntityId = sample.Id.ToString(),
            Action = "ResetWorkflowSteps",
            UserId = userId,
            Timestamp = DateTime.UtcNow,
            SampleId = sample.Id,
            SampleReferenceNumber = sample.ReferenceNumber,
            NewValue = $"Workflow steps reset for Sample #{sampleId} ({sample.ReferenceNumber}). Reason: {resetReason}"
        });

        await _db.SaveChangesAsync();
        return (await GetSessionAsync(sampleId))!;
    }
}
