using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Workflows;
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
public record RecordTestResultRequest(string StepName, List<decimal>? PlateReadings, decimal? DilutionFactor);
public record BatchResultLocationRequest(int SampleLocationId, decimal CFUResult);
public record BatchResultsRequest(decimal DilutionFactor, List<BatchResultLocationRequest> Locations);
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
public record SubmitBiochemicalRequest(string StepName, string BiochemicalResultText, int? AttachmentId);
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

    public TestWorkflowController(
        ITestWorkflowEngine engine, MicroLimsDbContext db,
        IncubatorEligibilityService incubatorEligibility, MediaAppearanceSnapshotService appearanceSnapshot)
    {
        _engine = engine;
        _db = db;
        _incubatorEligibility = incubatorEligibility;
        _appearanceSnapshot = appearanceSnapshot;
    }

    private int CurrentUserId => int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

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
    public Task<IActionResult> GetCurrentStep(int testOrderId) => RunAsync(async () =>
    {
        var current = await _engine.GetCurrentStepAsync(testOrderId);

        // Sample's "name" has no single field - it is whichever of
        // Item/WaterSamplingPoint/Department/Machine is set for this
        // sample's category, same precedence SampleSummaryService uses
        // for its own DisplayName.
        var order = await _db.TestOrders
            .Include(t => t.Sample!).ThenInclude(s => s.Item)
            .Include(t => t.Sample!).ThenInclude(s => s.WaterSamplingPoint)
            .Include(t => t.Sample!).ThenInclude(s => s.Department)
            .Include(t => t.Sample!).ThenInclude(s => s.Machine)
            .Include(t => t.Sample!).ThenInclude(s => s.CauseOfTesting)
            .FirstOrDefaultAsync(t => t.Id == testOrderId)
            ?? throw new InvalidOperationException($"Test order {testOrderId} not found.");
        var sample = order.Sample!;

        var sampleContext = new Dictionary<string, object?>
        {
            ["sampleName"] = sample.Item?.Name ?? sample.WaterSamplingPoint?.Code ?? sample.Department?.Name ?? sample.Machine?.Name ?? string.Empty,
            ["batchNumber"] = sample.BatchNumber,
            ["controlNumber"] = sample.ControlNumber,
            ["reason"] = sample.CauseOfTesting?.Name,
            ["systemReferenceNumber"] = sample.ReferenceNumber,
            ["sampleType"] = sample.Category.ToString()
        };

        // Stage (Sample.ProductionStage) is a Finished-Product-only concept
        // - ProductWorkflowEngine.ReceiveAsync only ever populates it for
        // that category, leaving it null for Raw Material, Packaging
        // Material, Water, EM and After Cleaning. The key is left out
        // entirely for every other category, never sent as null.
        if (sample.Category == SampleCategory.FinishedProduct)
            sampleContext["stage"] = sample.ProductionStage;

        var openIncubation = current.OpenIncubation;
        var incubationLock = openIncubation?.IncubationEndUtc is null ? null : new
        {
            isLocked = !openIncubation.IsIncubationComplete,
            incubationEndUtc = openIncubation.IncubationEndUtc,
            remainingSeconds = Math.Max(0, (long)Math.Ceiling((openIncubation.IncubationEndUtc.Value - DateTime.UtcNow).TotalSeconds)),
            stageNumber = openIncubation.StageNumber
        };

        var previousSteps = await _db.Incubations
            .Where(i => i.TestOrderId == testOrderId)
            .Include(i => i.Media).Include(i => i.IncubatorEquipment)
            .OrderBy(i => i.StepNumber).ThenBy(i => i.Id)
            .Select(i => new
            {
                stepNumber = i.StepNumber,
                stepName = i.StepName,
                stageNumber = i.StageNumber,
                status = i.CompletedAt == null ? "Incubating" : "Complete",
                mediaName = i.Media!.LotNumber,
                lotNumber = i.Media.LotNumber,
                incubatorName = i.IncubatorEquipment!.Code,
                incubatorSetTemp = i.IncubatorEquipment.SetPointTemperature,
                incubationStartUtc = i.IncubationStartUtc,
                incubationEndUtc = i.IncubationEndUtc,
                observation = i.Outcome
            })
            .ToListAsync();

        return new
        {
            testOrderId,
            step = current.Step is null ? null : new
            {
                current.Step.Id, current.Step.StepOrder, current.Step.StepName, current.Step.MediaTypeId,
                stepType = current.Step.StepType.ToString(),
                current.Step.TargetOrganismId,
                mediaType = current.Step.MediaType is null ? null : new { current.Step.MediaType.Id, current.Step.MediaType.Class },
                current.Step.IncubationMinHours, current.Step.IncubationMaxHours,
                current.Step.TemperatureMin, current.Step.TemperatureMax,
                current.Step.IsFinalStep,
                current.Step.RequiresIncubationTransfer,
                incubationStages = current.Step.IncubationStages.OrderBy(x => x.StageNumber).Select(x => new
                {
                    x.StageNumber,
                    x.TempMin,
                    x.TempMax,
                    x.IncubationMinHours,
                    x.IncubationMaxHours
                })
            },
            stepNumber = current.Step?.StepOrder,
            totalSteps = current.TotalSteps,
            testName = order.TestCode,
            workflowType = current.WorkflowType.ToString(),
            sampleContext,
            incubationLock,
            previousSteps,
            allStepsComplete = current.AllStepsComplete,
            finalResult = current.FinalResult,
            allSteps = current.AllSteps.Select(s => new { s.StepOrder, s.StepName }),
            completedSteps = current.CompletedSteps.Select(s => new
            {
                s.StepOrder, s.StepName, stepType = s.StepType.ToString(), s.IsFinalStep,
                s.Outcome, s.ObservedAt, s.ReportedResult, s.CalculatedResult, s.Status
            })
        };
    });

    [HttpGet("{testOrderId}/eligible-incubators/{stepMediaId}")]
    public async Task<IActionResult> GetEligibleIncubators(int testOrderId, int stepMediaId)
    {
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
                .Where(m => m.MaterialId == medium.MaterialId && m.IsReleasedForUse && m.ExpiryDate > DateTime.UtcNow)
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
    public Task<IActionResult> SelectMedia(int testOrderId, SelectMediaRequest request) => RunAsync(async () =>
    {
        var incubation = await _engine.SelectMediaAsync(testOrderId, request.StepName, request.MediaLotId, request.IncubatorId, CurrentUserId);
        return new
        {
            incubation.Id, incubation.StepName, incubation.Temperature, incubation.Duration,
            incubation.StartedAt, incubation.ExpectedReadingAt
        };
    });

    // The transfer IS this call - starting stage 2's incubation closes
    // stage 1 and opens a new Incubation row in one step. See
    // TestWorkflowEngine.StartStage2IncubationAsync.
    [HttpPost("{testOrderId}/start-stage-2-incubation")]
    public Task<IActionResult> StartStage2Incubation(int testOrderId, StartStage2IncubationRequest request) => RunAsync(async () =>
    {
        var incubation = await _engine.StartStage2IncubationAsync(testOrderId, request.StepName, request.IncubatorId, CurrentUserId);
        return new
        {
            incubation.Id, incubation.StepName, incubation.StageNumber, incubation.ParentIncubationId,
            incubation.Temperature, incubation.Duration, incubation.StartedAt, incubation.ExpectedReadingAt
        };
    });

    [HttpPost("{testOrderId}/record-result")]
    public Task<IActionResult> RecordResult(int testOrderId, RecordTestResultRequest request) => RunAsync(() =>
    {
        // record-result now serves CountTest (PlateCount) steps only -
        // every pathogen step goes through the dedicated Submit* endpoints
        // below. RecordResultAsync itself also guards this (see the
        // StepType check at its top), so a stray dual-plate/observation
        // payload can never reach that stale code path either way.
        var payload = new CountTestPayload(
            request.PlateReadings ?? throw new InvalidOperationException("Plate readings are required."),
            request.DilutionFactor ?? 1);
        return _engine.RecordResultAsync(testOrderId, request.StepName, payload, CurrentUserId);
    });

    // EM/After Cleaning batch grid - one row per location, populated
    // before any result is entered so the analyst can see limits up front.
    [HttpGet("{testOrderId}/locations")]
    public Task<IActionResult> GetLocations(int testOrderId) => RunAsync(async () =>
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

    [HttpPost("{testOrderId}/batch-results")]
    public Task<IActionResult> RecordBatchResults(int testOrderId, BatchResultsRequest request) => RunAsync(() =>
    {
        var locations = request.Locations.Select(l => new BatchLocationResult(l.SampleLocationId, l.CFUResult)).ToList();
        return _engine.RecordBatchResultsAsync(testOrderId, request.DilutionFactor, locations, CurrentUserId);
    });

    // Water batch count entry - one set of plate readings per sampling
    // point, averaged with no shared dilution factor (see
    // TestWorkflowEngine.RecordWaterBatchReadingsAsync).
    [HttpPost("{testOrderId}/water-batch-readings")]
    public Task<IActionResult> RecordWaterBatchReadings(int testOrderId, WaterBatchReadingsRequest request) => RunAsync(() =>
    {
        var locations = request.Locations.Select(l => new WaterBatchLocationReadings(l.SampleLocationId, l.Readings)).ToList();
        return _engine.RecordWaterBatchReadingsAsync(testOrderId, locations, CurrentUserId);
    });

    // EM/After Cleaning multi-window incubation - closes the currently
    // open (non-final) window once its minimum duration has elapsed.
    // The analyst then calls select-media again for the next step, same
    // as opening the first window.
    [HttpPost("{testOrderId}/close-incubation-window")]
    public Task<IActionResult> CloseIncubationWindow(int testOrderId) => RunAsync(async () =>
    {
        var incubation = await _engine.CloseCurrentIncubationWindowAsync(testOrderId, CurrentUserId);
        return new { incubation.Id, incubation.StepName, incubation.CompletedAt };
    });

    [HttpPost("{testOrderId}/batch-pathogen-results")]
    public Task<IActionResult> RecordBatchPathogenResults(int testOrderId, BatchPathogenResultsRequest request) => RunAsync(() =>
    {
        var observations = request.Locations.Select(l => new BatchLocationObservation(
            l.SampleLocationId,
            l.GrowthObserved ?? throw new InvalidOperationException("A growth observation is required for every location."))).ToList();
        return _engine.RecordBatchPathogenResultsAsync(testOrderId, observations, CurrentUserId);
    });

    [HttpPost("{testOrderId}/submit-broth")]
    public Task<IActionResult> SubmitBroth(int testOrderId, SubmitBrothRequest request) =>
        RunAsync(() => _engine.SubmitBrothAsync(testOrderId, request.StepName, request.Observation, CurrentUserId));

    [HttpPost("{testOrderId}/start-selective-plating-incubation")]
    public Task<IActionResult> StartSelectivePlatingIncubation(int testOrderId, StartSelectivePlatingIncubationRequest request) =>
        RunAsync(async () =>
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

    [HttpPost("{testOrderId}/submit-selective-plating-observation")]
    public Task<IActionResult> SubmitSelectivePlatingObservation(int testOrderId, SubmitSelectivePlatingObservationRequest request) =>
        RunAsync(() => _engine.SubmitSelectivePlatingObservationAsync(
            testOrderId, request.StepName, request.Observation, request.ObservedAppearanceNote, CurrentUserId));

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
    public Task<IActionResult> SubmitConfirmatorySetup(int testOrderId, SubmitConfirmatorySetupRequest request) =>
        RunAsync(() => _engine.SubmitConfirmatorySetupAsync(testOrderId, request.StepName,
            request.Selections.Select(s => new ConfirmatorySelectionInput(s.StepMediaId, s.MediaLotId, s.EquipmentId)).ToList(),
            request.IncubationStartUtc, request.IncubationEndUtc, CurrentUserId));

    [HttpPost("{testOrderId}/submit-confirmatory-observations")]
    public Task<IActionResult> SubmitConfirmatoryObservations(int testOrderId, SubmitConfirmatoryObservationsRequest request) =>
        RunAsync(() => _engine.SubmitConfirmatoryObservationsAsync(testOrderId, request.StepName,
            request.Observations.Select(o => new ConfirmatoryObservationInput(o.MaterialId, o.Observation)).ToList(),
            CurrentUserId));

    [HttpPost("{testOrderId}/analyst-decision")]
    public Task<IActionResult> RecordAnalystDecision(int testOrderId, AnalystDecisionRequest request) =>
        RunAsync(() => _engine.RecordAnalystDecisionAsync(testOrderId, request.Decision, CurrentUserId));

    [HttpPost("{testOrderId}/submit-biochemical")]
    public Task<IActionResult> SubmitBiochemical(int testOrderId, SubmitBiochemicalRequest request) =>
        RunAsync(() => _engine.SubmitBiochemicalAsync(testOrderId, request.StepName,
            request.BiochemicalResultText, request.AttachmentId, CurrentUserId));

    [Authorize(Roles = RoleConstants.Reviewer + "," + RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpPost("results/{workflowStepResultId}/biochemical-decision")]
    public Task<IActionResult> RecordBiochemicalDecision(int workflowStepResultId, BiochemicalReviewRequest request) =>
        RunAsync(() => _engine.RecordBiochemicalReviewDecisionAsync(
            workflowStepResultId, request.Approve, request.Comment, CurrentUserId));
}
