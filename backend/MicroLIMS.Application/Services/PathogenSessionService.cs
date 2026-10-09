using MicroLIMS.Shared.Exceptions;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Shared.Constants;

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

public class PathogenSessionService
{
    private readonly IMicroLimsDbContext _db;
    private readonly MediaAppearanceSnapshotService? _appearanceSnapshot;
    private readonly ConfirmationAgreementEvaluator _agreementEvaluator;
    private readonly IncubatorEligibilityService _incubatorEligibility;
    private readonly TimeProvider _time;

    public PathogenSessionService(
        IMicroLimsDbContext db,
        MediaAppearanceSnapshotService? appearanceSnapshot = null,
        ConfirmationAgreementEvaluator? agreementEvaluator = null,
        IncubatorEligibilityService? incubatorEligibility = null,
        TimeProvider? timeProvider = null)
    {
        _time = timeProvider ?? TimeProvider.System;
        _db = db;
        _appearanceSnapshot = appearanceSnapshot;
        _agreementEvaluator = agreementEvaluator ?? new ConfirmationAgreementEvaluator();
        _incubatorEligibility = incubatorEligibility ?? new IncubatorEligibilityService(db);
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
        var data = await PathogenSessionData.LoadAsync(_db, sample);
        var testDefs = data.TestDefs;
        var UserName = data.UserName;
        var incubations = data.Incubations;
        var stepResults = data.StepResults;
        var primaryObservations = data.PrimaryObservations;
        var countTestReadings = data.CountTestReadings;

        // Group Locations
        var groupedLocations = SessionLocationGrouper.Group(sample);

        var primaryObsMap = primaryObservations
            .ToDictionary(o => (o.SampleLocationId, o.TestOrderId));

        var mediaLookup = incubations
            .Where(i => i.MediaId.HasValue && i.Media != null)
            .GroupBy(i => i.MediaId!.Value)
            .ToDictionary(
                g => g.Key,
                g => (MaterialId: g.First().Media!.MaterialId, MediaProductId: g.First().Media!.Material?.MediaProductId));

        var utcNow = _time.GetUtcNow().UtcDateTime;

        var tsb = SharedTsbStateBuilder.Build(sample, data, mediaLookup, groupedLocations.Count, utcNow);
        var sharedTsbDto = tsb.Dto;
        var tsbApplicableCodes = tsb.ApplicableCodes;
        var ownTsbByOrder = tsb.OwnTsbByOrder;
        var tsbIncubating = tsb.IsIncubating;
        var tsbCompleted = tsb.IsCompleted;

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

            var ownTsbComplete = ownTsbByOrder.TryGetValue(to.Id, out var ownTsb) && ownTsb.IsComplete;

            var stepDtos = AssignedTestStepBuilder.Build(steps, toIncubations, toStepResults, requiresTsb, ownTsbComplete, mediaLookup);

            // Determine Test Session State & Result Entry Allowance
            var stateResult = WorkflowStateResolver.Resolve(to, requiresTsb, toIncubations, stepDtos, _time.GetUtcNow().UtcDateTime, steps, sample.Status, mediaLookup);
            string testSessionState = stateResult.WorkflowState;
            string testSessionStateDisplay = stateResult.WorkflowStateDisplay;
            bool isResultEntryAllowed = stateResult.IsResultEntryAllowed;
            bool isWorkflowLocked = stateResult.IsWorkflowLocked;
            string? lockReason = stateResult.LockReason;
            string workflowStatus = stateResult.WorkflowStatus;

            var countReading = countTestReadings.FirstOrDefault(c => c.TestOrderId == to.Id);
            var countResultDto = await BuildCountResultAsync(sample, to, countReading);

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

        var built = ResultMatrixBuilder.Build(groupedLocations, assignedTestDtos, sample, primaryObsMap, UserName);
        var matrix = built.Matrix;
        var missingResults = built.Missing;
        int completedCount = built.Completed;
        int availableCount = built.Available;
        int lockedCount = built.Locked;

        int totalRequired = groupedLocations.Count * assignedTestDtos.Count;
        int pendingCount = availableCount; // Only available and empty cells are truly pending entry

        // Overall Session Status Computation
        var (sessionStatus, sessionStatusDisplay) = SessionStatusResolver.Resolve(
            completedCount, totalRequired, sample.Status, tsbIncubating, tsbCompleted, tsbApplicableCodes.Count);

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

    // The count test's recorded reading with its reporting unit: the
    // specification's (or sampling configuration's) unit first, the category
    // default otherwise.
    private async Task<CountResultDto?> BuildCountResultAsync(Sample sample, TestOrder to, CountTestReading? countReading)
    {
        CountResultDto? countResultDto = null;
        if (countReading != null)
        {
            // Same configured-unit-first pattern as TestWorkflowEngine/
            // ResultProjectionService - Specification.Unit is required
            // for Item-based count tests as of the 2026-09 Preparation
            // Configuration simplification, which removed the old
            // SamplePreparation.Unit fallback used here.
            string? configuredUnit = null;
            if (sample.ItemId is not null)
            {
                var spec = await SpecificationLookup.PrimaryAsync(_db, sample.Id, sample.ItemId.Value, to.TestCode);
                configuredUnit = spec?.Unit;
            }
            else if (sample.WaterSamplingPointId is not null)
            {
                var config = await _db.SamplingConfigurations.FirstOrDefaultAsync(c => c.TestCode == to.TestCode && c.WaterSamplingPointId == sample.WaterSamplingPointId);
                configuredUnit = config?.Unit;
            }

            var unit = !string.IsNullOrWhiteSpace(configuredUnit)
                ? (configuredUnit.StartsWith("CFU/", StringComparison.OrdinalIgnoreCase) ? configuredUnit : $"CFU/{configuredUnit}")
                : TestWorkflowEngine.GetCfuUnit(sample.Category, null);
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

        return countResultDto;
    }

    // GMP gate: a shared TSB start is refused (and the refusal audited) until
    // the sample's test preparation is confirmed.
    private async Task EnsurePreparedAsync(Sample sample, int userId)
    {
        var isPrepared = sample.PreparationStatus == SamplePreparationStatus.Ready;
        if (isPrepared && sample.ItemId != null)
        {
            isPrepared = await _db.SamplePreparations.AnyAsync(p => p.SampleId == sample.Id);
        }

        if (!isPrepared)
        {
            if (sample.TestOrders.Count > 0)
            {
                foreach (var to in sample.TestOrders)
                {
                    _db.AuditLogs.Add(new AuditLog
                    {
                        EntityName = "TestOrder",
                        EntityId = to.Id.ToString(),
                        Action = "TestStartRefused",
                        UserId = userId,
                        SampleId = sample.Id,
                        TestOrderId = to.Id,
                        SampleReferenceNumber = sample.ReferenceNumber,
                        Reason = $"GMP Refusal: Test preparation stage must be confirmed for sample {sample.ReferenceNumber} before starting shared TSB enrichment."
                    });

                    _db.WorkflowHistories.Add(new WorkflowHistory
                    {
                        TestOrderId = to.Id,
                        FromStep = to.CurrentStep,
                        ToStep = to.CurrentStep,
                        Note = $"Transition refused: Test preparation not confirmed for sample {sample.ReferenceNumber} (step \"Shared TSB\").",
                        PerformedByUserId = userId,
                        Timestamp = _time.GetUtcNow().UtcDateTime
                    });
                }
            }
            else
            {
                _db.AuditLogs.Add(new AuditLog
                {
                    EntityName = "Sample",
                    EntityId = sample.Id.ToString(),
                    Action = "TestStartRefused",
                    UserId = userId,
                    SampleId = sample.Id,
                    SampleReferenceNumber = sample.ReferenceNumber,
                    Reason = $"GMP Refusal: Test preparation stage must be confirmed for sample {sample.ReferenceNumber} before starting shared TSB enrichment."
                });
            }

            await _db.SaveChangesAsync();

            throw new WorkflowStepException(
                WorkflowErrorCodes.PreparationNotConfirmed,
                $"Test Preparation must be completed and confirmed for sample {sample.ReferenceNumber} before incubation can be started for step \"Shared TSB\".");
        }
    }

    // Which of the sample's tests take this TSB lot: each is timed by its own
    // TSB medium, and the whole start is refused (naming the test) when a
    // joining test's window is unconfigured or the incubator is out of range.
    private async Task<List<(TestOrder Order, TestWorkflowStep Step, IncubationWindow Window)>> ResolveJoiningTestsAsync(
        Sample sample, Media media, Equipment incubator)
    {
        var testCodes = sample.TestOrders.Select(t => t.TestCode).Distinct().ToList();
        var testDefs = await _db.TestDefinitions
            .Include(t => t.Steps)
                .ThenInclude(s => s.StepMedia)
                    .ThenInclude(m => m.Material)
            .Include(t => t.Steps)
                .ThenInclude(s => s.StepMedia)
                    .ThenInclude(m => m.IncubationCondition)
            .Where(t => testCodes.Contains(t.Code))
            .ToDictionaryAsync(t => t.Code);

        // Each test is timed by its own TSB medium from Test Master. A test
        // joins only when that medium accepts this lot - one configured with a
        // different medium starts on its own. Among the joining tests, an
        // unconfigured window or an incubator outside a test's range refuses
        // the whole start, naming the test.
        var hasTsbTests = false;
        var joining = new List<(TestOrder Order, TestWorkflowStep Step, IncubationWindow Window)>();
        foreach (var to in sample.TestOrders)
        {
            if (!testDefs.TryGetValue(to.TestCode, out var def)) continue;

            // SelectiveBroth (e.g. MBP for E.coli, RVS for Salmonella) is
            // species-specific and never shares this generic TSB lot -
            // matching it here was the bug that let a shared TSB batch
            // land on the wrong step.
            var tsbStep = def.Steps.OrderBy(s => s.StepOrder).FirstOrDefault(TsbDetectionHelper.IsSharedTsbStep);
            if (tsbStep == null) continue;
            hasTsbTests = true;

            var medium = IncubationWindowResolver.MatchStepMedium(tsbStep.StepMedia, media.MaterialId, media.Material?.MediaProductId);
            if (medium == null) continue;

            var mediumName = medium.Material?.MaterialName ?? media.Material?.MaterialName ?? "TSB";
            var window = IncubationWindowResolver.TryGet(medium)
                ?? throw new WorkflowStepException(WorkflowErrorCodes.IncubationWindowNotConfigured,
                    $"The shared TSB can't start: the incubation window for \"{mediumName}\" on test {to.TestCode} (step \"{tsbStep.StepName}\") is not configured - set its incubation hours and temperature in Test Master.");

            if (!await _incubatorEligibility.IsWithinRangeAsync(medium.Id, incubator.Id))
                throw new WorkflowStepException(WorkflowErrorCodes.IncubatorTempOutOfRange,
                    $"The shared TSB can't start: incubator {incubator.Code}'s set point is outside the {window.TemperatureText} range of \"{mediumName}\" on test {to.TestCode}.");

            joining.Add((to, tsbStep, window));
        }

        if (!hasTsbTests)
            throw new WorkflowStepException("NoTsbTests", "None of the assigned tests on this sample require TSB enrichment.");

        if (joining.Count == 0)
            throw new WorkflowStepException(WorkflowErrorCodes.MediaNotInPermittedList,
                $"Media lot #{media.LotNumber} ({media.Material?.MaterialName ?? "unknown"}) is not the TSB medium of any test on this sample.");

        return joining;
    }

    public Task<SharedTsbStateDto> StartSharedTsbAsync(int sampleId, StartSharedTsbRequest request, int userId) =>
        UnitOfWork.RunAsync(_db, () => StartSharedTsbCoreAsync(sampleId, request, userId));

    private async Task<SharedTsbStateDto> StartSharedTsbCoreAsync(int sampleId, StartSharedTsbRequest request, int userId)
    {
        var sample = await _db.Samples
            .Include(s => s.TestOrders)
            .FirstOrDefaultAsync(s => s.Id == sampleId)
            ?? throw new NotFoundException($"Sample #{sampleId} not found.");

        await EnsurePreparedAsync(sample, userId);

        var media = await _db.Media
            .Include(m => m.Material)
            .FirstOrDefaultAsync(m => m.Id == request.MediaLotId)
            ?? throw new InvalidOperationException($"Media #{request.MediaLotId} not found.");

        if (media.ExpiryDate.Date < _time.GetUtcNow().UtcDateTime.Date)
            throw new WorkflowStepException("MediaExpired", $"Media lot #{media.LotNumber} expired on {media.ExpiryDate:yyyy-MM-dd}.");

        // The incubator is Laboratory Configuration equipment, which carries
        // the set point - never the EquipmentInventories asset register.
        var incubator = await _db.Equipment.FirstOrDefaultAsync(e => e.Id == request.IncubatorEquipmentId && e.Type == EquipmentType.Incubator)
            ?? throw new InvalidOperationException($"Incubator #{request.IncubatorEquipmentId} not found.");

        var joining = await ResolveJoiningTestsAsync(sample, media, incubator);

        foreach (var sectionId in joining.Select(j => j.Order.SectionId).Distinct())
            SectionMediaRule.EnsureLot(media, sectionId);

        var startUtc = request.IncubationStartUtc ?? _time.GetUtcNow().UtcDateTime;

        var toIds = joining.Select(j => j.Order.Id).ToList();
        var existingIncubations = await _db.Incubations
            .Where(i => i.TestOrderId.HasValue && toIds.Contains(i.TestOrderId.Value) && (i.StepName.Contains("TSB") || i.StepName.Contains("Enrichment") || i.StepName == "Broth Enrichment"))
            .ToListAsync();

        if (existingIncubations.Count > 0)
            _db.Incubations.RemoveRange(existingIncubations);

        var started = new List<(TestOrder Order, TestWorkflowStep Step, Incubation Incubation)>();
        foreach (var (to, tsbStep, window) in joining)
        {
            var endUtc = window.EndAt(startUtc);
            var inc = new Incubation
            {
                TestOrderId = to.Id,
                StepNumber = tsbStep.StepOrder,
                StepName = tsbStep.StepName,
                MediaId = request.MediaLotId,
                IncubatorEquipmentId = incubator.Id,
                Temperature = window.TemperatureText,
                Duration = window.DurationText,
                StartedAt = startUtc,
                IncubationStartUtc = startUtc,
                IncubationEndUtc = endUtc,
                ExpectedReadingAt = endUtc,
                StartedByUserId = userId
            };
            _db.Incubations.Add(inc);
            started.Add((to, tsbStep, inc));

            to.CurrentStep = WorkflowStep.Incubating;
            to.Status = ApprovalStatus.Pending;

            _db.WorkflowHistories.Add(new WorkflowHistory
            {
                TestOrderId = to.Id,
                FromStep = WorkflowStep.Running,
                ToStep = WorkflowStep.Incubating,
                Note = $"Shared TSB Enrichment started: Media #{media.LotNumber} ({media.Material?.MaterialName ?? "TSB"}), Incubator {incubator.Code}, {window.DurationText} @ {window.TemperatureText}.",
                PerformedByUserId = userId,
                Timestamp = _time.GetUtcNow().UtcDateTime
            });
        }

        await _db.SaveChangesAsync();

        foreach (var (to, tsbStep, inc) in started)
        {
            var stepNamesToAdd = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Broth Enrichment", "Broth enrichment", tsbStep.StepName };
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
                        SubmittedAtUtc = _time.GetUtcNow().UtcDateTime
                    });
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
            ?? throw new NotFoundException($"Session for sample #{sampleId} not found.");

        var sample = await _db.Samples
            .Include(s => s.TestOrders)
            .Include(s => s.Locations)
            .FirstOrDefaultAsync(s => s.Id == sampleId, cancellationToken)
            ?? throw new NotFoundException($"Sample #{sampleId} not found.");

        var testOrdersByCode = sample.TestOrders.ToDictionary(t => t.TestCode);
        var locationsById = sample.Locations.ToDictionary(l => l.Id);
        var testStateByCode = session.AssignedTests.ToDictionary(t => t.TestCode);

        // Validate the whole request BEFORE anything is changed, so a rejected request records nothing.
        foreach (var obs in request.Observations)
        {
            if (testStateByCode.TryGetValue(obs.TestCode, out var testInfo) && !testInfo.IsResultEntryAllowed)
            {
                throw new WorkflowStepException(
                    "PrerequisiteNotMet",
                    $"Cannot enter primary observations for {obs.TestCode}: TSB incubation or required workflow steps are still in progress.");
            }
        }

        var primaryObservations = await LoadPrimaryObservationsAsync(locationsById.Keys, cancellationToken);

        foreach (var obs in request.Observations)
        {
            if (!testOrdersByCode.TryGetValue(obs.TestCode, out var order))
                continue;

            if (locationsById.TryGetValue(obs.SampleLocationId, out var loc))
            {
                UpsertPrimaryObservation(primaryObservations, loc.Id, order.Id, obs.Observation, obs.SelectiveMediaSnapshot, userId);

                if (obs.Observation == GrowthObservation.NoGrowth)
                {
                    loc.ReportedResult = "Not Detected (-)";
                    loc.Status = ResultStatus.Absent;
                    loc.CFUResult = null;
                }
                else if (obs.Observation == GrowthObservation.GrowthConforming)
                {
                    loc.ReportedResult = "Growth Conforming (Presumptive +)";
                    loc.Status = ResultStatus.PendingConfirmation;
                    loc.CFUResult = null;
                }
                else if (obs.Observation == GrowthObservation.GrowthNonConforming)
                {
                    loc.ReportedResult = "Growth Non-Conforming";
                    loc.Status = ResultStatus.PendingConfirmation;
                    loc.CFUResult = null;
                }

                loc.EnteredAt = _time.GetUtcNow().UtcDateTime;
                loc.EnteredByUserId = userId;
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        return (await GetSessionAsync(sampleId))!;
    }

    // Primary observations for a sample's locations, loaded once and keyed by (location, test order), so a
    // batch save can upsert in memory and write everything with one SaveChanges. The (location, test order)
    // index is not unique, so duplicate rows are tolerated the way a FirstOrDefault lookup would, rather than
    // failing the save.
    private async Task<Dictionary<(int SampleLocationId, int TestOrderId), LocationPathogenObservation>> LoadPrimaryObservationsAsync(
        IEnumerable<int> sampleLocationIds,
        CancellationToken cancellationToken = default)
    {
        var locationIds = sampleLocationIds.ToList();
        return (await _db.LocationPathogenObservations
                .Where(o => locationIds.Contains(o.SampleLocationId))
                .ToListAsync(cancellationToken))
            .GroupBy(o => (o.SampleLocationId, o.TestOrderId))
            .ToDictionary(g => g.Key, g => g.OrderBy(o => o.Id).First());
    }

    // Same semantics as LocationPathogenObservationService.RecordPrimaryObservationAsync - update the
    // existing row in place (keeping its media snapshot when none is supplied) or add one - but staged on
    // the context instead of saved immediately.
    private void UpsertPrimaryObservation(
        Dictionary<(int SampleLocationId, int TestOrderId), LocationPathogenObservation> primaryObservations,
        int sampleLocationId,
        int testOrderId,
        GrowthObservation observation,
        string? selectiveMediaSnapshot,
        int observedByUserId)
    {
        var now = _time.GetUtcNow().UtcDateTime;
        if (primaryObservations.TryGetValue((sampleLocationId, testOrderId), out var existing))
        {
            existing.GrowthObservation = observation;
            existing.SelectiveMediaSnapshot = selectiveMediaSnapshot ?? existing.SelectiveMediaSnapshot;
            existing.ObservedAt = now;
            existing.ObservedByUserId = observedByUserId;
            return;
        }

        var entity = new LocationPathogenObservation
        {
            SampleLocationId = sampleLocationId,
            TestOrderId = testOrderId,
            GrowthObservation = observation,
            SelectiveMediaSnapshot = selectiveMediaSnapshot,
            ObservedAt = now,
            ObservedByUserId = observedByUserId,
            CreatedAt = now
        };
        _db.LocationPathogenObservations.Add(entity);
        primaryObservations[(sampleLocationId, testOrderId)] = entity;
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
            ?? throw new NotFoundException($"Sample #{sampleId} not found.");

        var testOrder = sample.TestOrders.FirstOrDefault(t => t.Id == request.TestOrderId)
            ?? throw new InvalidOperationException($"TestOrder #{request.TestOrderId} not found.");

        var testDef = await _db.TestDefinitions
            .Include(d => d.Steps)
                .ThenInclude(s => s.StepMedia)
                    .ThenInclude(m => m.Material)
            .Include(d => d.Steps)
                .ThenInclude(s => s.StepMedia)
                    .ThenInclude(m => m.IncubationCondition)
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

        if (confirmatoryStep == null)
            throw new InvalidOperationException($"Test \"{testOrder.TestCode}\" has no confirmatory plating step in Test Master.");

        // Laboratory Configuration equipment (carries the set point), never
        // the EquipmentInventories asset register.
        var incubator = await _db.Equipment.FirstOrDefaultAsync(e => e.Id == request.IncubatorEquipmentId && e.Type == EquipmentType.Incubator, cancellationToken)
            ?? throw new InvalidOperationException($"Incubator #{request.IncubatorEquipmentId} not found.");

        var chosen = await ResolveChosenConfirmatoryMediaAsync(request, testOrder, confirmatoryStep, incubator, cancellationToken);

        var startUtc = request.IncubationStartUtc ?? _time.GetUtcNow().UtcDateTime;
        var endUtc = startUtc.AddHours(chosen.Max(c => c.Window.MaxHours));

        var inc = new Incubation
        {
            TestOrderId = testOrder.Id,
            StepNumber = confirmatoryStep.StepOrder,
            StepName = confirmatoryStep.StepName,
            MediaId = request.MediaLotIds?.FirstOrDefault() ?? 0,
            IncubatorEquipmentId = incubator.Id,
            Temperature = string.Join("; ", chosen.Select(c => $"{c.Window.TempMin}-{c.Window.TempMax}").Distinct()),
            Duration = string.Join("; ", chosen.Select(c => $"{c.Window.MinHours}-{c.Window.MaxHours}h").Distinct()),
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
            Timestamp = _time.GetUtcNow().UtcDateTime
        });

        await _db.SaveChangesAsync(cancellationToken);
        return (await GetSessionAsync(sampleId))!;
    }

    // Each chosen confirmatory medium with its own configured window, after
    // checking the section's media rule and that the incubator suits every
    // medium's temperature range.
    private async Task<List<(TestWorkflowStepMedia Medium, IncubationWindow Window)>> ResolveChosenConfirmatoryMediaAsync(
        BatchConfirmatorySetupRequest request, TestOrder testOrder, TestWorkflowStep confirmatoryStep,
        Equipment incubator, CancellationToken cancellationToken)
    {
        // One incubation holds every chosen plate, each medium timed by its own
        // configuration on this step (never step-level or default hours): it
        // runs to the longest maximum, never short of the longest minimum, and
        // the incubator must suit every medium's temperature range - the same
        // rule as TestWorkflowEngine.SubmitConfirmatorySetupAsync.
        var materialIds = request.MediaMaterialIds.Distinct().ToList();
        await SectionMediaRule.EnsureMaterialsAsync(_db, materialIds, testOrder.SectionId, cancellationToken);
        if (request.MediaLotIds is { Count: > 0 })
            await SectionMediaRule.EnsureLotsAsync(_db, request.MediaLotIds, testOrder.SectionId, cancellationToken);
        var productByMaterial = await _db.Materials
            .Where(m => materialIds.Contains(m.Id))
            .ToDictionaryAsync(m => m.Id, m => m.MediaProductId, cancellationToken);

        var chosen = new List<(TestWorkflowStepMedia Medium, IncubationWindow Window)>();
        foreach (var materialId in materialIds)
        {
            var medium = IncubationWindowResolver.MatchStepMedium(confirmatoryStep.StepMedia, materialId, productByMaterial.GetValueOrDefault(materialId))
                ?? throw new WorkflowStepException(WorkflowErrorCodes.MediaNotInPermittedList,
                    $"Material #{materialId} is not on the permitted media list of step \"{confirmatoryStep.StepName}\".");
            var window = IncubationWindowResolver.Require(medium, confirmatoryStep.StepName);

            if (!await _incubatorEligibility.IsWithinRangeAsync(medium.Id, incubator.Id, cancellationToken))
                throw new WorkflowStepException(WorkflowErrorCodes.IncubatorTempOutOfRange,
                    $"Incubator {incubator.Code}'s set point is outside the {window.TemperatureText} range of \"{medium.Material?.MaterialName ?? $"material #{materialId}"}\".");

            chosen.Add((medium, window));
        }

        return chosen;
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
                existingPlate.RecordedAtUtc = _time.GetUtcNow().UtcDateTime;
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
                    RecordedAtUtc = _time.GetUtcNow().UtcDateTime
                };
                _db.ConfirmatoryPlateObservations.Add(newPlate);
                if (!primaryObs.ConfirmatoryPlateObservations.Contains(newPlate))
                {
                    primaryObs.ConfirmatoryPlateObservations.Add(newPlate);
                }
            }
        }

        ApplyConfirmationAgreement(primaryObsList, testDefs, userId);

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
                    Timestamp = _time.GetUtcNow().UtcDateTime
                });
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        return (await GetSessionAsync(sampleId))!;
    }

    // Decides each primary observation's reported result from how its
    // confirmatory plates agree, once all required media have a reading.
    private void ApplyConfirmationAgreement(
        List<LocationPathogenObservation> primaryObsList, Dictionary<string, TestDefinition> testDefs, int userId)
    {
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
                    primaryObs.SampleLocation.Status = ResultStatus.Detected;
                }
                else if (outcome == ConfirmationResult.NotDetected)
                {
                    primaryObs.SampleLocation.ReportedResult = "Not Detected (-)";
                    primaryObs.SampleLocation.Status = ResultStatus.Absent;
                }
                else // Inconclusive
                {
                    primaryObs.SampleLocation.ReportedResult = "Inconclusive (Retest)";
                    primaryObs.SampleLocation.Status = ResultStatus.Inconclusive;
                }

                primaryObs.SampleLocation.EnteredAt = _time.GetUtcNow().UtcDateTime;
                primaryObs.SampleLocation.EnteredByUserId = userId;
            }
        }
    }

    public async Task<PathogenTestingSessionDto> SaveResultMatrixAsync(int sampleId, SaveResultMatrixRequest request, int userId)
    {
        var session = await GetSessionAsync(sampleId)
            ?? throw new NotFoundException($"Session for sample #{sampleId} not found.");

        var sample = await _db.Samples
            .Include(s => s.TestOrders)
            .Include(s => s.Locations).ThenInclude(l => l.SamplingConfiguration)
            .FirstOrDefaultAsync(s => s.Id == sampleId)
            ?? throw new NotFoundException($"Sample #{sampleId} not found.");

        var testOrdersByCode = sample.TestOrders.ToDictionary(t => t.TestCode);
        var locationsById = sample.Locations.ToDictionary(l => l.Id);
        var testStateByCode = session.AssignedTests.ToDictionary(t => t.TestCode);

        // Step-gating validation: check that every submitted result cell is allowed by its test's workflow prerequisites.
        // Runs over the whole request BEFORE anything is changed, so a rejected request records nothing.
        foreach (var cell in request.Cells)
        {
            if (testStateByCode.TryGetValue(cell.TestCode, out var testInfo) && !testInfo.IsResultEntryAllowed)
            {
                throw new WorkflowStepException(
                    "PrerequisiteNotMet",
                    $"Cannot enter results for {cell.TestCode}: TSB incubation or required workflow steps are still in progress ({testInfo.TestSessionStateDisplay}).");
            }
        }

        var primaryObservations = await LoadPrimaryObservationsAsync(locationsById.Keys);

        foreach (var cell in request.Cells)
        {
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
                        : (ResultStatus?)null;
                }
                else
                {
                    var isDetected = cell.ResultCode == "DETECTED" || cell.ResultDisplay?.Contains("Detected (+)") == true;
                    loc.ReportedResult = isDetected ? "Detected (+)" : "Not Detected (-)";
                    loc.Status = isDetected ? ResultStatus.Detected : ResultStatus.Absent;
                    loc.CFUResult = null;

                    // Also record primary observation record for data integrity
                    UpsertPrimaryObservation(
                        primaryObservations,
                        loc.Id,
                        order.Id,
                        isDetected ? GrowthObservation.GrowthConforming : GrowthObservation.NoGrowth,
                        null,
                        userId);
                }

                loc.EnteredAt = _time.GetUtcNow().UtcDateTime;
                loc.EnteredByUserId = userId;
            }
        }

        await _db.SaveChangesAsync();
        return (await GetSessionAsync(sampleId))!;
    }

    // Same semantics as TestWorkflowEngine's Compare(...) - delegates to shared SpecLimitParser
    private static ResultStatus CompareAgainstLimits(decimal value, string? alert, string? action, string? spec) =>
        SpecLimitParser.CompareAgainstLimits(value, alert, action, spec);

    public async Task<PathogenTestingSessionDto> CompleteSessionAsync(int sampleId, int userId)
    {
        var session = await GetSessionAsync(sampleId)
            ?? throw new NotFoundException($"Session for sample #{sampleId} not found.");

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
            ?? throw new NotFoundException($"Sample #{sampleId} not found.");

        // 3. Mark TestOrders as completed / ready for review
        foreach (var order in sample.TestOrders)
        {
            order.CurrentStep = WorkflowStep.Ready;
            order.Status = ApprovalStatus.ResultEntered;

            _db.WorkflowHistories.Add(new WorkflowHistory
            {
                TestOrderId = order.Id,
                FromStep = WorkflowStep.Running,
                ToStep = WorkflowStep.Ready,
                Note = "Testing session completed: all location results entered and verified.",
                PerformedByUserId = userId,
                Timestamp = _time.GetUtcNow().UtcDateTime
            });
        }

        sample.Status = SampleStatus.UnderReview;
        sample.ReviewedAt = null;

        await ReviewEventLog.LogAsync(
            _db, ReviewEntityTypes.Sample, sampleId, userId,
            ReviewWorkflowEventType.SubmittedForReview,
            "Testing session completed - submitted for review");

        await _db.SaveChangesAsync();
        return (await GetSessionAsync(sampleId))!;
    }

    public async Task<PathogenTestingSessionDto> ResetSessionAsync(int sampleId, string? reason, int userId)
    {
        var sample = await _db.Samples
            .Include(s => s.TestOrders)
            .Include(s => s.Locations)
            .FirstOrDefaultAsync(s => s.Id == sampleId)
            ?? throw new NotFoundException($"Sample #{sampleId} not found.");

        // Rejecting a sample takes SectionHead or SystemAdministrator
        // (ApprovalController). This endpoint takes Analyst. Resetting used to
        // send a Rejected sample back to Received, which let the lower
        // privilege undo the higher one's decision about the material and left
        // nothing but an audit line behind. A rejection is reversed through the
        // approval route by someone entitled to reverse it, not by a session
        // reset.
        //
        // Checked before anything is deleted, so a refused reset changes nothing.
        // A voided sample is closed for the same reason: resetting it would
        // quietly bring a record struck from the register back into testing.
        if (sample.Status == SampleStatus.Voided)
        {
            throw new WorkflowStepException(
                "SampleVoided",
                $"Sample #{sample.ReferenceNumber} has been voided and its testing session cannot be reset.");
        }

        if (sample.Status == SampleStatus.Rejected)
        {
            throw new WorkflowStepException(
                "SampleRejected",
                $"Sample #{sample.ReferenceNumber} has been rejected and its testing session cannot be reset. "
                + "A rejection is reversed through the approval workflow by a Section Head.");
        }

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
                Timestamp = _time.GetUtcNow().UtcDateTime
            });
        }

        // 8. Reset Sample Status
        // Rejected is absent deliberately: the guard at the top of this method
        // refuses that sample outright, so it can never reach this line.
        if (sample.Status == SampleStatus.InTesting || sample.Status == SampleStatus.UnderReview)
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
            Timestamp = _time.GetUtcNow().UtcDateTime,
            SampleId = sample.Id,
            SampleReferenceNumber = sample.ReferenceNumber,
            NewValue = $"Workflow steps reset for Sample #{sampleId} ({sample.ReferenceNumber}). Reason: {resetReason}"
        });

        await _db.SaveChangesAsync();
        return (await GetSessionAsync(sampleId))!;
    }
}
