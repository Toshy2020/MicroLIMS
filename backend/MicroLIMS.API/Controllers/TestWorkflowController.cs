using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

public record SelectMediaRequest(string StepName, int MediaLotId, int IncubatorId);
public record RecordTestResultRequest(
    string StepName,
    List<decimal>? PlateReadings, decimal? DilutionFactor,
    bool? GrowthObserved,
    bool? Plate1GrowthObserved, bool? Plate2GrowthObserved);
public record BatchResultLocationRequest(int SampleLocationId, decimal CFUResult);
public record BatchResultsRequest(decimal DilutionFactor, List<BatchResultLocationRequest> Locations);
public record BatchPathogenLocationRequest(int SampleLocationId, bool? GrowthObserved, bool? Plate1GrowthObserved, bool? Plate2GrowthObserved);
public record BatchPathogenResultsRequest(List<BatchPathogenLocationRequest> Locations);

// Generic step-runner API for any TestDefinition with a configured
// workflow template (WorkflowType + TestWorkflowStep) - replaces the
// separate /api/count-tests and /api/pathogen endpoints. See
// TestWorkflowEngine; nothing here branches on a specific test code.
[ApiController]
[Route("api/test-workflow")]
[Authorize]
public class TestWorkflowController : ControllerBase
{
    private readonly ITestWorkflowEngine _engine;

    public TestWorkflowController(ITestWorkflowEngine engine)
    {
        _engine = engine;
    }

    private int CurrentUserId => int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

    [HttpGet("{testOrderId}/current-step")]
    public async Task<IActionResult> GetCurrentStep(int testOrderId)
    {
        var current = await _engine.GetCurrentStepAsync(testOrderId);

        // Shaped to avoid navigation cycles (Step.TestDefinition,
        // Incubation.TestOrder) when serializing.
        return Ok(ApiResponse<object>.Ok(new
        {
            step = current.Step is null ? null : new
            {
                current.Step.Id, current.Step.StepOrder, current.Step.StepName, current.Step.MediaTypeId,
                mediaType = current.Step.MediaType is null ? null : new { current.Step.MediaType.Id, current.Step.MediaType.Class },
                current.Step.IncubationMinHours, current.Step.IncubationMaxHours,
                current.Step.TemperatureMin, current.Step.TemperatureMax,
                current.Step.IsFinalStep, current.Step.IsDualPlate
            },
            workflowType = current.WorkflowType,
            incubation = current.OpenIncubation is null ? null : new
            {
                current.OpenIncubation.Id, current.OpenIncubation.MediaId, current.OpenIncubation.IncubatorEquipmentId,
                current.OpenIncubation.Temperature, current.OpenIncubation.Duration,
                current.OpenIncubation.StartedAt, current.OpenIncubation.ExpectedReadingAt
            },
            allStepsComplete = current.AllStepsComplete,
            finalResult = current.FinalResult
        }));
    }

    [HttpPost("{testOrderId}/select-media")]
    public async Task<IActionResult> SelectMedia(int testOrderId, SelectMediaRequest request)
    {
        var incubation = await _engine.SelectMediaAsync(testOrderId, request.StepName, request.MediaLotId, request.IncubatorId, CurrentUserId);
        return Ok(ApiResponse<object>.Ok(new
        {
            incubation.Id, incubation.StepName, incubation.Temperature, incubation.Duration,
            incubation.StartedAt, incubation.ExpectedReadingAt
        }));
    }

    [HttpPost("{testOrderId}/record-result")]
    public async Task<IActionResult> RecordResult(int testOrderId, RecordTestResultRequest request)
    {
        ResultPayload payload = request.PlateReadings is not null
            ? new CountTestPayload(request.PlateReadings, request.DilutionFactor ?? 1)
            : request.Plate1GrowthObserved is not null
                ? new DualPlatePayload(request.Plate1GrowthObserved.Value, request.Plate2GrowthObserved
                    ?? throw new InvalidOperationException("Plate 2's result is required for a dual-plate step."))
                : new ObservationPayload(request.GrowthObserved
                    ?? throw new InvalidOperationException("A growth observation, plate readings, or dual-plate result is required."));

        var result = await _engine.RecordResultAsync(testOrderId, request.StepName, payload, CurrentUserId);
        return Ok(ApiResponse<object>.Ok(result));
    }

    // EM/After Cleaning batch grid - one row per location, populated
    // before any result is entered so the analyst can see limits up front.
    [HttpGet("{testOrderId}/locations")]
    public async Task<IActionResult> GetLocations(int testOrderId)
    {
        var locations = await _engine.GetLocationsAsync(testOrderId);
        return Ok(ApiResponse<object>.Ok(locations.Select(l => new
        {
            l.Id,
            locationType = l.LocationType.ToString(),
            locationName = l.RoomTestConfiguration?.Room?.Name ?? l.MachinePartConfiguration?.MachinePart?.Name ?? string.Empty,
            gradeClassification = l.RoomTestConfiguration?.Room?.GradeClassification,
            alertLimit = l.AlertLimit ?? l.RoomTestConfiguration?.AlertLimit ?? l.MachinePartConfiguration?.AlertLimit,
            actionLimit = l.ActionLimit ?? l.RoomTestConfiguration?.ActionLimit ?? l.MachinePartConfiguration?.ActionLimit,
            specLimit = l.SpecLimit ?? l.RoomTestConfiguration?.SpecLimit ?? l.MachinePartConfiguration?.SpecLimit,
            l.CFUResult,
            l.CalculatedResult,
            l.ReportedResult,
            l.Status,
            l.EnteredAt
        })));
    }

    [HttpPost("{testOrderId}/batch-results")]
    public async Task<IActionResult> RecordBatchResults(int testOrderId, BatchResultsRequest request)
    {
        var locations = request.Locations.Select(l => new BatchLocationResult(l.SampleLocationId, l.CFUResult)).ToList();
        var result = await _engine.RecordBatchResultsAsync(testOrderId, request.DilutionFactor, locations, CurrentUserId);
        return Ok(ApiResponse<object>.Ok(result));
    }

    // EM/After Cleaning multi-window incubation - closes the currently
    // open (non-final) window once its minimum duration has elapsed.
    // The analyst then calls select-media again for the next step, same
    // as opening the first window.
    [HttpPost("{testOrderId}/close-incubation-window")]
    public async Task<IActionResult> CloseIncubationWindow(int testOrderId)
    {
        var incubation = await _engine.CloseCurrentIncubationWindowAsync(testOrderId, CurrentUserId);
        return Ok(ApiResponse<object>.Ok(new { incubation.Id, incubation.StepName, incubation.CompletedAt }));
    }

    [HttpPost("{testOrderId}/batch-pathogen-results")]
    public async Task<IActionResult> RecordBatchPathogenResults(int testOrderId, BatchPathogenResultsRequest request)
    {
        var isDualPlate = request.Locations.Any(l => l.Plate1GrowthObserved is not null);
        TestWorkflowResult result;
        if (isDualPlate)
        {
            var dualPlate = request.Locations.Select(l => new BatchLocationDualPlateObservation(
                l.SampleLocationId,
                l.Plate1GrowthObserved ?? throw new InvalidOperationException("Plate 1's result is required for every location."),
                l.Plate2GrowthObserved ?? throw new InvalidOperationException("Plate 2's result is required for every location."))).ToList();
            result = await _engine.RecordBatchPathogenResultsAsync(testOrderId, null, dualPlate, CurrentUserId);
        }
        else
        {
            var observations = request.Locations.Select(l => new BatchLocationObservation(
                l.SampleLocationId,
                l.GrowthObserved ?? throw new InvalidOperationException("A growth observation is required for every location."))).ToList();
            result = await _engine.RecordBatchPathogenResultsAsync(testOrderId, observations, null, CurrentUserId);
        }
        return Ok(ApiResponse<object>.Ok(result));
    }
}
