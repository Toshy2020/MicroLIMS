using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

// SelectMediaRequest/RecordTestResultRequest are CountTest-only now -
// every pathogen step is submitted through the dedicated Submit*
// endpoints below instead. Their dual-plate fields (Plate2MediaId,
// Plate1Label, Plate2Label, GrowthObserved, Plate1/2GrowthObserved) are
// gone along with the dual-plate model itself; see TestWorkflowEngine.
public record SelectMediaRequest(string StepName, int MediaLotId, int IncubatorId);
public record StartStage2IncubationRequest(string StepName, int IncubatorId);
public record RecordTestResultRequest(string StepName, List<decimal>? PlateReadings, decimal? DilutionFactor, List<string>? RawPlateReadings = null, string? DilutionFactorOverrideNote = null);
public record RecordHplcAssayResultRequest(decimal SampleWeightMg, decimal SampleDilution, List<decimal> SampleAreas, string Password, string? Comment = null);
public record RecordElementalAssayElementRequest(int SpecificationId, int CalibrationRunAnalyteId, decimal ReportedPpm, bool OverRange, bool BelowLoq);
public record RecordElementalAssayResultRequest(decimal UnitAmount, DateTime AnalysedAt, List<RecordElementalAssayElementRequest> Elements, string Password, string? Comment = null);
public record RecordMeasurementParameterRequest(int SpecificationId, List<decimal> Readings);
public record RecordMeasurementResultRequest(DateTime AnalysedAt, int? EquipmentId, List<RecordMeasurementParameterRequest> Parameters, string Password, string? Comment = null);
public record GravimetricReplicateRequest(decimal? Container, decimal Initial, decimal Final);
public record RecordGravimetricParameterRequest(int SpecificationId, List<GravimetricReplicateRequest> Replicates);
public record RecordGravimetricResultRequest(DateTime AnalysedAt, int? EquipmentId, Dictionary<string, string> Conditions, List<RecordGravimetricParameterRequest> Parameters, string Password, string? Comment = null);
public record RecordQualitativeParameterRequest(int SpecificationId, bool Conforms, string? Observation);
public record RecordQualitativeResultRequest(DateTime AnalysedAt, int? EquipmentId, List<RecordQualitativeParameterRequest> Parameters, string Password, string? Comment = null);
public record RecordDissolutionResultRequest(DateTime AnalysedAt, int? EquipmentId, Dictionary<string, string>? Conditions, decimal MediumVolumeMl, decimal? DilutionFactor, List<decimal> VesselAreas, string Password, string? Comment = null);
public record RecordDissolutionStageRequest(List<decimal> VesselAreas, string Password, string? Comment = null);
public record RecordDisintegrationResultRequest(DateTime AnalysedAt, int? EquipmentId, Dictionary<string, string>? Conditions, List<decimal?> UnitMinutes, string Password, string? Comment = null);
public record RecordDisintegrationStageRequest(List<decimal?> UnitMinutes, string Password, string? Comment = null);
public record RecordWeightVariationUnitRequest(decimal? WeightMg = null, decimal? GrossMg = null, decimal? ShellMg = null);
public record RecordWeightVariationResultRequest(DateTime AnalysedAt, int? EquipmentId, Dictionary<string, string>? Conditions, List<RecordWeightVariationUnitRequest> Units, string Password, string? Comment = null);
public record RecordWeightVariationStageRequest(List<RecordWeightVariationUnitRequest> Units, string Password, string? Comment = null);
public record BatchResultLocationRequest(int SampleLocationId, List<decimal> Readings);
public record BatchResultsRequest(List<BatchResultLocationRequest> Locations);
public record WaterBatchLocationRequest(int SampleLocationId, List<decimal> Readings);
public record WaterBatchReadingsRequest(List<WaterBatchLocationRequest> Locations);
public record BatchPathogenLocationRequest(int SampleLocationId, bool? GrowthObserved);
public record BatchPathogenResultsRequest(List<BatchPathogenLocationRequest> Locations);

public record SubmitBrothRequest(string StepName, string? Observation);

public record StartSelectivePlatingIncubationRequest(string StepName, int MediaLotId, int EquipmentId, DateTime? IncubationStartUtc = null);
public record SubmitSelectivePlatingObservationRequest(string StepName, GrowthObservation Observation, string? ObservedAppearanceNote = null);

[Obsolete("Use StartSelectivePlatingIncubationRequest and SubmitSelectivePlatingObservationRequest.")]
public record SubmitSelectivePlatingRequest(string StepName, int MediaLotId, int EquipmentId,
    DateTime IncubationStartUtc, DateTime IncubationEndUtc, GrowthObservation Observation);

public record ConfirmatorySelectionRequest(int StepMediaId, int MediaLotId, int EquipmentId);
public record SubmitConfirmatorySetupRequest(string StepName, List<ConfirmatorySelectionRequest> Selections,
    DateTime IncubationStartUtc, DateTime IncubationEndUtc);

public record ConfirmatoryObservationRequest(int MaterialId, GrowthObservation Observation);
public record SubmitConfirmatoryObservationsRequest(string StepName, List<ConfirmatoryObservationRequest> Observations);

public record AnalystDecisionRequest(AnalystDecision Decision);
public record SubmitBiochemicalRequest(string StepName, string BiochemicalResultText, int? AttachmentId, bool OrganismDetected);
public record BiochemicalReviewRequest(bool Approve, string Comment);

// Generic step-runner API for any TestDefinition with a configured
// workflow template (WorkflowType + TestWorkflowStep) - replaces the
// separate /api/count-tests and /api/pathogen endpoints. See
// TestWorkflowEngine; nothing here branches on a specific test code.
[ApiController]
[Route("api/test-workflow")]
[Authorize(Roles = RoleConstants.Analyst + "," + RoleConstants.Reviewer + "," + RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
public class TestWorkflowController : ControllerBase
{
    private readonly ITestWorkflowEngine _engine;
    private readonly MicroLimsDbContext _db;
    private readonly IncubatorEligibilityService _incubatorEligibility;
    private readonly MediaAppearanceSnapshotService _appearanceSnapshot;
    private readonly IUserSectionScopeService _scopeService;
    private readonly GroupedTestActionService _groupedTestActionService;
    private readonly CurrentStepViewService _currentStepView;

    public TestWorkflowController(
        ITestWorkflowEngine engine, MicroLimsDbContext db,
        IncubatorEligibilityService incubatorEligibility, MediaAppearanceSnapshotService appearanceSnapshot,
        IUserSectionScopeService scopeService,
        GroupedTestActionService? groupedTestActionService = null,
        CurrentStepViewService? currentStepView = null)
    {
        _engine = engine;
        _db = db;
        _incubatorEligibility = incubatorEligibility;
        _appearanceSnapshot = appearanceSnapshot;
        _scopeService = scopeService;
        _groupedTestActionService = groupedTestActionService ?? new GroupedTestActionService(db, engine, incubatorEligibility);
        _currentStepView = currentStepView ?? new CurrentStepViewService(db, engine);
    }

    private int CurrentUserId => int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
    private RoleType CurrentRole => Enum.TryParse<RoleType>(User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value, out var r) ? r : RoleType.Analyst;
    private string? ClientIpAddress => HttpContext.Connection.RemoteIpAddress?.ToString();

    [HttpGet("actionable-groups")]
    public async Task<IActionResult> GetActionableGroups(
        [FromQuery] string? scope = "mine",
        [FromQuery] string? actionType = null,
        [FromQuery] string? sampleIds = null,
        CancellationToken ct = default)
    {
        List<int>? parsedSampleIds = null;
        if (!string.IsNullOrWhiteSpace(sampleIds))
        {
            parsedSampleIds = sampleIds.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => int.TryParse(s.Trim(), out var id) ? id : (int?)null)
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .ToList();
        }

        var userScope = await _scopeService.GetAccessibleSectionIdsAsync(CurrentUserId, ct);

        var result = await _groupedTestActionService.GetActionableGroupsAsync(
            CurrentUserId, CurrentRole, scope, actionType, parsedSampleIds, userScope, ct);
        return Ok(ApiResponse<ActionableGroupsResponse>.Ok(result));
    }

    [HttpPost("batch-select-media")]
    public async Task<IActionResult> BatchSelectMedia(
        [FromBody] BatchSelectMediaRequest request,
        CancellationToken ct = default)
    {
        if (request.TestOrderIds != null && request.TestOrderIds.Count > 0)
        {
            await _scopeService.EnsureTestOrdersAccessAsync(CurrentUserId, request.TestOrderIds, ct);
        }

        try
        {
            var result = await _groupedTestActionService.ExecuteBatchSelectMediaAsync(
                request, CurrentUserId, CurrentRole, ct);
            return Ok(ApiResponse<BatchSelectMediaResponse>.Ok(result));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    // ApiResponse has no error-code field, so the code travels as the
    // first entry in Errors, with remainingSeconds appended when the
    // failure is an incubation lock.
    private async Task<IActionResult> RunAsync<T>(Func<Task<T>> action)
    {
        try
        {
            return Ok(ApiResponse<object>.Ok((await action())!));
        }
        catch (WorkflowStepException ex)
        {
            var errors = new List<string> { ex.ErrorCode };
            if (ex.RemainingSeconds is long remaining)
                errors.Add(remaining.ToString());
            return BadRequest(ApiResponse<object>.Fail(ex.Message, errors));
        }
    }

    [HttpGet("{testOrderId}/current-step")]
    public async Task<IActionResult> GetCurrentStep(int testOrderId)
    {
        await _scopeService.EnsureTestOrderAccessAsync(CurrentUserId, testOrderId);
        return await RunAsync(() => _currentStepView.GetAsync(testOrderId));
    }

    [HttpGet("{testOrderId}/sibling-pathogen-orders")]
    public async Task<IActionResult> GetSiblingPathogenOrders(int testOrderId, CancellationToken ct)
    {
        await _scopeService.EnsureTestOrderAccessAsync(CurrentUserId, testOrderId, ct);
        var siblings = await _engine.GetSiblingPathogenOrdersAsync(testOrderId, ct);
        return Ok(ApiResponse<List<SiblingPathogenOrderDto>>.Ok(siblings));
    }

    [HttpGet("{testOrderId}/eligible-incubators/{stepMediaId}")]
    public async Task<IActionResult> GetEligibleIncubators(int testOrderId, int stepMediaId)
    {
        await _scopeService.EnsureTestOrderAccessAsync(CurrentUserId, testOrderId);
        var incubators = await _incubatorEligibility.GetEligibleIncubatorsAsync(stepMediaId);
        var stepMedia = await _db.TestWorkflowStepMedias.FirstOrDefaultAsync(m => m.Id == stepMediaId)
            ?? throw new InvalidOperationException($"Step media {stepMediaId} not found.");

        return Ok(ApiResponse<object>.Ok(new
        {
            stepMediaId,
            tempMin = stepMedia.TempMin,
            tempMax = stepMedia.TempMax,
            eligibleIncubators = incubators.Select(i => new
            {
                i.Id, name = i.Name, code = i.Code, setTemperature = i.SetTemperature, calibrationStatus = i.CalibrationStatus
            })
        }));
    }

    [HttpGet("{testOrderId}/permitted-confirmatory-media")]
    public async Task<IActionResult> GetPermittedConfirmatoryMedia(int testOrderId, [FromQuery] string stepName)
    {
        await _scopeService.EnsureTestOrderAccessAsync(CurrentUserId, testOrderId);
        var order = await _db.TestOrders.FirstOrDefaultAsync(t => t.Id == testOrderId)
            ?? throw new InvalidOperationException($"Test order {testOrderId} not found.");
        var step = await _db.TestWorkflowSteps
            .Include(s => s.StepMedia).ThenInclude(m => m.Material)
            .Include(s => s.TargetOrganism)
            .Include(s => s.TestDefinition)
            .FirstOrDefaultAsync(s => s.TestDefinition!.Code == order.TestCode && s.StepName == stepName)
            ?? throw new InvalidOperationException($"Step '{stepName}' is not part of {order.TestCode}.");

        var permitted = new List<object>();
        foreach (var medium in step.StepMedia.OrderBy(m => m.DisplayOrder))
        {
            var expected = step.TargetOrganismId is int organismId
                ? await _appearanceSnapshot.GetExpectedAppearanceSnapshotAsync(medium.MaterialId, organismId)
                : null;

            var lots = await _db.Media
                .Where(m => m.MaterialId == medium.MaterialId && m.IsReleasedForUse && m.Status == MediaStatus.Active && m.ExpiryDate > DateTime.UtcNow)
                .OrderBy(m => m.ExpiryDate)
                .Select(m => new { m.Id, lotNumber = m.LotNumber, expiryDate = m.ExpiryDate })
                .ToListAsync();

            permitted.Add(new
            {
                stepMediaId = medium.Id,
                materialId = medium.MaterialId,
                mediaName = medium.Material!.MaterialName,
                expectedAppearance = expected,
                tempMin = medium.TempMin,
                tempMax = medium.TempMax,
                availableLots = lots
            });
        }

        return Ok(ApiResponse<object>.Ok(new
        {
            testOrderId,
            stepName,
            organism = step.TargetOrganism is null ? null : new { step.TargetOrganism.Id, name = step.TargetOrganism.ScientificName },
            permittedMedia = permitted
        }));
    }

    [HttpPost("{testOrderId}/select-media")]
    public async Task<IActionResult> SelectMedia(int testOrderId, SelectMediaRequest request)
    {
        await _scopeService.EnsureTestOrderAccessAsync(CurrentUserId, testOrderId);
        return await RunAsync(async () =>
        {
            var incubation = await _engine.SelectMediaAsync(testOrderId, request.StepName, request.MediaLotId, request.IncubatorId, CurrentUserId);
            return new
            {
                incubation.Id, incubation.StepName, incubation.Temperature, incubation.Duration,
                incubation.StartedAt, incubation.ExpectedReadingAt
            };
        });
    }

    // The transfer IS this call - starting stage 2's incubation closes
    // stage 1 and opens a new Incubation row in one step. See
    // TestWorkflowEngine.StartStage2IncubationAsync.
    [HttpPost("{testOrderId}/start-stage-2-incubation")]
    public async Task<IActionResult> StartStage2Incubation(int testOrderId, StartStage2IncubationRequest request)
    {
        await _scopeService.EnsureTestOrderAccessAsync(CurrentUserId, testOrderId);
        return await RunAsync(async () =>
        {
            var incubation = await _engine.StartStage2IncubationAsync(testOrderId, request.StepName, request.IncubatorId, CurrentUserId);
            return new
            {
                incubation.Id, incubation.StepName, incubation.StageNumber, incubation.ParentIncubationId,
                incubation.Temperature, incubation.Duration, incubation.StartedAt, incubation.ExpectedReadingAt
            };
        });
    }

    [HttpPost("{testOrderId}/record-result")]
    public async Task<IActionResult> RecordResult(int testOrderId, RecordTestResultRequest request)
    {
        await _scopeService.EnsureTestOrderAccessAsync(CurrentUserId, testOrderId);
        return await RunAsync(() =>
        {
            // record-result serves CountTest (PlateCount) steps
            CountTestPayload payload;
            if (request.RawPlateReadings is { Count: > 0 })
            {
                payload = new CountTestPayload(request.RawPlateReadings, request.DilutionFactor ?? 1, request.DilutionFactorOverrideNote);
            }
            else if (request.PlateReadings is { Count: > 0 })
            {
                payload = new CountTestPayload(request.PlateReadings, request.DilutionFactor ?? 1, request.DilutionFactorOverrideNote);
            }
            else
            {
                throw new InvalidOperationException("Plate readings are required.");
            }

            return _engine.RecordResultAsync(testOrderId, request.StepName, payload, CurrentUserId);
        });
    }

    [HttpPost("{testOrderId}/record-hplc-result")]
    public async Task<IActionResult> RecordHplcResult(int testOrderId, RecordHplcAssayResultRequest request)
    {
        await _scopeService.EnsureTestOrderAccessAsync(CurrentUserId, testOrderId);
        return await RunAsync(() =>
        {
            var payload = new HplcAssayPayload(request.SampleWeightMg, request.SampleDilution, request.SampleAreas, request.Password, request.Comment);
            return _engine.RecordHplcAssayResultAsync(testOrderId, payload, CurrentUserId, ClientIpAddress);
        });
    }

    [HttpPost("{testOrderId}/record-elemental-result")]
    [Authorize(Policy = PermissionConstants.TestWorkflowExecute)]
    public async Task<IActionResult> RecordElementalResult(int testOrderId, RecordElementalAssayResultRequest request)
    {
        await _scopeService.EnsureTestOrderAccessAsync(CurrentUserId, testOrderId);
        return await RunAsync(() =>
        {
            var payload = new ElementalAssayPayload(
                request.UnitAmount,
                request.AnalysedAt,
                request.Elements.Select(e => new ElementalAssayElementInput(
                    e.SpecificationId,
                    e.CalibrationRunAnalyteId,
                    e.ReportedPpm,
                    e.OverRange,
                    e.BelowLoq)).ToList(),
                request.Password,
                request.Comment);
            return _engine.RecordElementalAssayResultAsync(testOrderId, payload, CurrentUserId, ClientIpAddress);
        });
    }

    [HttpPost("{testOrderId}/record-measurement-result")]
    [Authorize(Policy = PermissionConstants.TestWorkflowExecute)]
    public async Task<IActionResult> RecordMeasurementResult(int testOrderId, RecordMeasurementResultRequest request)
    {
        await _scopeService.EnsureTestOrderAccessAsync(CurrentUserId, testOrderId);
        return await RunAsync(() =>
        {
            var payload = new MeasurementPayload(
                request.AnalysedAt,
                request.EquipmentId,
                request.Parameters.Select(p => new MeasurementParameterInput(
                    p.SpecificationId,
                    p.Readings)).ToList(),
                request.Password,
                request.Comment);
            return _engine.RecordMeasurementResultAsync(testOrderId, payload, CurrentUserId, ClientIpAddress);
        });
    }

    [HttpPost("{testOrderId}/record-gravimetric-result")]
    [Authorize(Policy = PermissionConstants.TestWorkflowExecute)]
    public async Task<IActionResult> RecordGravimetricResult(int testOrderId, RecordGravimetricResultRequest request)
    {
        await _scopeService.EnsureTestOrderAccessAsync(CurrentUserId, testOrderId);
        return await RunAsync(() =>
        {
            var payload = new GravimetricPayload(
                request.AnalysedAt,
                request.EquipmentId,
                request.Conditions,
                request.Parameters.Select(p => new GravimetricParameterInput(
                    p.SpecificationId,
                    p.Replicates.Select(r => new GravimetricReplicateInput(r.Container, r.Initial, r.Final)).ToList())).ToList(),
                request.Password,
                request.Comment);
            return _engine.RecordGravimetricResultAsync(testOrderId, payload, CurrentUserId, ClientIpAddress);
        });
    }

    [HttpPost("{testOrderId}/record-qualitative-result")]
    [Authorize(Policy = PermissionConstants.TestWorkflowExecute)]
    public async Task<IActionResult> RecordQualitativeResult(int testOrderId, RecordQualitativeResultRequest request)
    {
        await _scopeService.EnsureTestOrderAccessAsync(CurrentUserId, testOrderId);
        return await RunAsync(() =>
        {
            var payload = new QualitativePayload(
                request.AnalysedAt,
                request.EquipmentId,
                request.Parameters.Select(p => new QualitativeParameterInput(
                    p.SpecificationId,
                    p.Conforms,
                    p.Observation)).ToList(),
                request.Password,
                request.Comment);
            return _engine.RecordQualitativeResultAsync(testOrderId, payload, CurrentUserId, ClientIpAddress);
        });
    }

    [HttpPost("{testOrderId}/record-dissolution-result")]
    [Authorize(Policy = PermissionConstants.TestWorkflowExecute)]
    public async Task<IActionResult> RecordDissolutionResult(int testOrderId, RecordDissolutionResultRequest request)
    {
        await _scopeService.EnsureTestOrderAccessAsync(CurrentUserId, testOrderId);
        return await RunAsync(() =>
        {
            var payload = new DissolutionPayload(
                request.AnalysedAt,
                request.EquipmentId,
                request.Conditions,
                request.MediumVolumeMl,
                request.DilutionFactor,
                request.VesselAreas,
                request.Password,
                request.Comment);
            return _engine.RecordDissolutionResultAsync(testOrderId, payload, CurrentUserId, ClientIpAddress);
        });
    }

    [HttpPost("{testOrderId}/record-dissolution-stage")]
    [Authorize(Policy = PermissionConstants.TestWorkflowExecute)]
    public async Task<IActionResult> RecordDissolutionStage(int testOrderId, RecordDissolutionStageRequest request)
    {
        await _scopeService.EnsureTestOrderAccessAsync(CurrentUserId, testOrderId);
        return await RunAsync(() =>
        {
            var payload = new DissolutionStagePayload(
                request.VesselAreas,
                request.Password,
                request.Comment);
            return _engine.RecordDissolutionStageAsync(testOrderId, payload, CurrentUserId, ClientIpAddress);
        });
    }

    [HttpPost("{testOrderId}/record-disintegration-result")]
    [Authorize(Policy = PermissionConstants.TestWorkflowExecute)]
    public async Task<IActionResult> RecordDisintegrationResult(int testOrderId, RecordDisintegrationResultRequest request)
    {
        await _scopeService.EnsureTestOrderAccessAsync(CurrentUserId, testOrderId);
        return await RunAsync(() =>
        {
            var payload = new DisintegrationPayload(
                request.AnalysedAt,
                request.EquipmentId,
                request.Conditions,
                request.UnitMinutes,
                request.Password,
                request.Comment);
            return _engine.RecordDisintegrationResultAsync(testOrderId, payload, CurrentUserId, ClientIpAddress);
        });
    }

    [HttpPost("{testOrderId}/record-disintegration-stage")]
    [Authorize(Policy = PermissionConstants.TestWorkflowExecute)]
    public async Task<IActionResult> RecordDisintegrationStage(int testOrderId, RecordDisintegrationStageRequest request)
    {
        await _scopeService.EnsureTestOrderAccessAsync(CurrentUserId, testOrderId);
        return await RunAsync(() =>
        {
            var payload = new DisintegrationStagePayload(
                request.UnitMinutes,
                request.Password,
                request.Comment);
            return _engine.RecordDisintegrationStageAsync(testOrderId, payload, CurrentUserId, ClientIpAddress);
        });
    }

    [HttpPost("{testOrderId}/record-weight-variation-result")]
    [Authorize(Policy = PermissionConstants.TestWorkflowExecute)]
    public async Task<IActionResult> RecordWeightVariationResult(int testOrderId, RecordWeightVariationResultRequest request)
    {
        await _scopeService.EnsureTestOrderAccessAsync(CurrentUserId, testOrderId);
        return await RunAsync(() =>
        {
            var units = request.Units?
                .Select(u => new WeightVariationUnitPayload(u.WeightMg, u.GrossMg, u.ShellMg))
                .ToList();
            var payload = new WeightVariationPayload(
                request.AnalysedAt,
                request.EquipmentId,
                request.Conditions,
                units!,
                request.Password,
                request.Comment);
            return _engine.RecordWeightVariationResultAsync(testOrderId, payload, CurrentUserId, ClientIpAddress);
        });
    }

    [HttpPost("{testOrderId}/record-weight-variation-stage")]
    [Authorize(Policy = PermissionConstants.TestWorkflowExecute)]
    public async Task<IActionResult> RecordWeightVariationStage(int testOrderId, RecordWeightVariationStageRequest request)
    {
        await _scopeService.EnsureTestOrderAccessAsync(CurrentUserId, testOrderId);
        return await RunAsync(() =>
        {
            var units = request.Units?
                .Select(u => new WeightVariationUnitPayload(u.WeightMg, u.GrossMg, u.ShellMg))
                .ToList();
            var payload = new WeightVariationStagePayload(
                units!,
                request.Password,
                request.Comment);
            return _engine.RecordWeightVariationStageAsync(testOrderId, payload, CurrentUserId, ClientIpAddress);
        });
    }

    // EM/After Cleaning batch grid - one row per location, populated
    // before any result is entered so the analyst can see limits up front.
    [HttpGet("{testOrderId}/locations")]
    public async Task<IActionResult> GetLocations(int testOrderId)
    {
        await _scopeService.EnsureTestOrderAccessAsync(CurrentUserId, testOrderId);
        return await RunAsync(async () =>
        {
            var locations = await _engine.GetLocationsAsync(testOrderId);
            return locations.Select(l => new
            {
                l.Id,
                locationType = l.LocationType.ToString(),
                locationName = l.RoomTestConfiguration?.Room?.Name ?? l.MachinePartConfiguration?.MachinePart?.Name ?? l.WaterSamplingPoint?.Code ?? string.Empty,
                gradeClassification = l.RoomTestConfiguration?.Room?.GradeClassification,
                alertLimit = l.AlertLimit ?? l.RoomTestConfiguration?.AlertLimit ?? l.MachinePartConfiguration?.AlertLimit ?? l.SamplingConfiguration?.AlertLimit,
                actionLimit = l.ActionLimit ?? l.RoomTestConfiguration?.ActionLimit ?? l.MachinePartConfiguration?.ActionLimit ?? l.SamplingConfiguration?.ActionLimit,
                specLimit = l.SpecLimit ?? l.RoomTestConfiguration?.SpecLimit ?? l.MachinePartConfiguration?.SpecLimit ?? l.SamplingConfiguration?.SpecLimit,
                l.CFUResult,
                l.CalculatedResult,
                l.ReportedResult,
                l.RawReadings,
                l.Status,
                l.EnteredAt
            });
        });
    }

    [HttpPost("{testOrderId}/batch-results")]
    public async Task<IActionResult> RecordBatchResults(int testOrderId, BatchResultsRequest request)
    {
        await _scopeService.EnsureTestOrderAccessAsync(CurrentUserId, testOrderId);
        return await RunAsync(() =>
        {
            var locations = request.Locations.Select(l => new BatchLocationReadings(l.SampleLocationId, l.Readings)).ToList();
            return _engine.RecordBatchResultsAsync(testOrderId, locations, CurrentUserId);
        });
    }

    // Water batch count entry - one set of plate readings per sampling
    // point, averaged with no shared dilution factor (see
    // TestWorkflowEngine.RecordWaterBatchReadingsAsync).
    [HttpPost("{testOrderId}/water-batch-readings")]
    public async Task<IActionResult> RecordWaterBatchReadings(int testOrderId, WaterBatchReadingsRequest request)
    {
        await _scopeService.EnsureTestOrderAccessAsync(CurrentUserId, testOrderId);
        return await RunAsync(() =>
        {
            var locations = request.Locations.Select(l => new WaterBatchLocationReadings(l.SampleLocationId, l.Readings)).ToList();
            return _engine.RecordWaterBatchReadingsAsync(testOrderId, locations, CurrentUserId);
        });
    }

    // EM/After Cleaning multi-window incubation - closes the currently
    // open (non-final) window once its minimum duration has elapsed.
    // The analyst then calls select-media again for the next step, same
    // as opening the first window.
    [HttpPost("{testOrderId}/close-incubation-window")]
    public async Task<IActionResult> CloseIncubationWindow(int testOrderId)
    {
        await _scopeService.EnsureTestOrderAccessAsync(CurrentUserId, testOrderId);
        return await RunAsync(async () =>
        {
            var incubation = await _engine.CloseCurrentIncubationWindowAsync(testOrderId, CurrentUserId);
            return new { incubation.Id, incubation.StepName, incubation.CompletedAt };
        });
    }

    [HttpPost("{testOrderId}/batch-pathogen-results")]
    public async Task<IActionResult> RecordBatchPathogenResults(int testOrderId, BatchPathogenResultsRequest request)
    {
        await _scopeService.EnsureTestOrderAccessAsync(CurrentUserId, testOrderId);
        return await RunAsync(() =>
        {
            var observations = request.Locations.Select(l => new BatchLocationObservation(
                l.SampleLocationId,
                l.GrowthObserved ?? throw new InvalidOperationException("A growth observation is required for every location."))).ToList();
            return _engine.RecordBatchPathogenResultsAsync(testOrderId, observations, CurrentUserId);
        });
    }

    [HttpPost("{testOrderId}/submit-broth")]
    public async Task<IActionResult> SubmitBroth(int testOrderId, SubmitBrothRequest request)
    {
        await _scopeService.EnsureTestOrderAccessAsync(CurrentUserId, testOrderId);
        return await RunAsync(() => _engine.SubmitBrothAsync(testOrderId, request.StepName, request.Observation, CurrentUserId));
    }

    [HttpPost("{testOrderId}/start-selective-plating-incubation")]
    public async Task<IActionResult> StartSelectivePlatingIncubation(int testOrderId, StartSelectivePlatingIncubationRequest request)
    {
        await _scopeService.EnsureTestOrderAccessAsync(CurrentUserId, testOrderId);
        return await RunAsync(async () =>
        {
            var incubation = await _engine.StartSelectivePlatingIncubationAsync(
                testOrderId, request.StepName, request.MediaLotId, request.EquipmentId, request.IncubationStartUtc, CurrentUserId);
            return new
            {
                incubation.Id,
                incubation.StepName,
                incubation.IncubationStartUtc,
                incubation.IncubationEndUtc,
                incubation.ExpectedReadingAt
            };
        });
    }

    [HttpPost("{testOrderId}/submit-selective-plating-observation")]
    public async Task<IActionResult> SubmitSelectivePlatingObservation(int testOrderId, SubmitSelectivePlatingObservationRequest request)
    {
        await _scopeService.EnsureTestOrderAccessAsync(CurrentUserId, testOrderId);
        return await RunAsync(() => _engine.SubmitSelectivePlatingObservationAsync(
            testOrderId, request.StepName, request.Observation, request.ObservedAppearanceNote, CurrentUserId));
    }

    [HttpPost("{testOrderId}/submit-selective-plating")]
    [Obsolete("Use start-selective-plating-incubation followed by submit-selective-plating-observation.")]
    public IActionResult SubmitSelectivePlating_Retired(int testOrderId)
    {
        return StatusCode(410, new
        {
            error = "This endpoint is retired.",
            message = "Use start-selective-plating-incubation followed by submit-selective-plating-observation.",
            replacedBy = new[]
            {
                $"POST /api/test-workflow/{testOrderId}/start-selective-plating-incubation",
                $"POST /api/test-workflow/{testOrderId}/submit-selective-plating-observation"
            }
        });
    }

    [HttpPost("{testOrderId}/submit-confirmatory-setup")]
    public async Task<IActionResult> SubmitConfirmatorySetup(int testOrderId, SubmitConfirmatorySetupRequest request)
    {
        await _scopeService.EnsureTestOrderAccessAsync(CurrentUserId, testOrderId);
        return await RunAsync(() => _engine.SubmitConfirmatorySetupAsync(testOrderId, request.StepName,
            request.Selections.Select(s => new ConfirmatorySelectionInput(s.StepMediaId, s.MediaLotId, s.EquipmentId)).ToList(),
            request.IncubationStartUtc, request.IncubationEndUtc, CurrentUserId));
    }

    [HttpPost("{testOrderId}/submit-confirmatory-observations")]
    public async Task<IActionResult> SubmitConfirmatoryObservations(int testOrderId, SubmitConfirmatoryObservationsRequest request)
    {
        await _scopeService.EnsureTestOrderAccessAsync(CurrentUserId, testOrderId);
        return await RunAsync(() => _engine.SubmitConfirmatoryObservationsAsync(testOrderId, request.StepName,
            request.Observations.Select(o => new ConfirmatoryObservationInput(o.MaterialId, o.Observation)).ToList(),
            CurrentUserId));
    }

    [HttpPost("{testOrderId}/analyst-decision")]
    public async Task<IActionResult> RecordAnalystDecision(int testOrderId, AnalystDecisionRequest request)
    {
        await _scopeService.EnsureTestOrderAccessAsync(CurrentUserId, testOrderId);
        return await RunAsync(() => _engine.RecordAnalystDecisionAsync(testOrderId, request.Decision, CurrentUserId));
    }

    [HttpPost("{testOrderId}/submit-biochemical")]
    public async Task<IActionResult> SubmitBiochemical(int testOrderId, SubmitBiochemicalRequest request)
    {
        await _scopeService.EnsureTestOrderAccessAsync(CurrentUserId, testOrderId);
        return await RunAsync(() => _engine.SubmitBiochemicalAsync(testOrderId, request.StepName,
            request.BiochemicalResultText, request.AttachmentId, request.OrganismDetected, CurrentUserId));
    }

    [Authorize(Roles = RoleConstants.Reviewer + "," + RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpPost("results/{workflowStepResultId}/biochemical-decision")]
    public async Task<IActionResult> RecordBiochemicalDecision(int workflowStepResultId, BiochemicalReviewRequest request)
    {
        var testOrderId = await _db.WorkflowStepResults
            .AsNoTracking()
            .Where(w => w.Id == workflowStepResultId)
            .Select(w => (int?)w.TestOrderId)
            .FirstOrDefaultAsync();
        if (testOrderId.HasValue)
        {
            await _scopeService.EnsureTestOrderAccessAsync(CurrentUserId, testOrderId.Value);
        }

        return await RunAsync(() => _engine.RecordBiochemicalReviewDecisionAsync(
            workflowStepResultId, request.Approve, request.Comment, CurrentUserId));
    }

    // Bypasses the minimum-duration wait gate for the currently open
    // incubation only - never changes the recorded window/temperature.
    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpPost("{testOrderId}/override-minimum-duration")]
    public async Task<IActionResult> OverrideMinimumDuration(int testOrderId)
    {
        await _scopeService.EnsureTestOrderAccessAsync(CurrentUserId, testOrderId);
        return await RunAsync(() => _engine.OverrideMinimumDurationAsync(testOrderId, CurrentUserId));
    }
}
