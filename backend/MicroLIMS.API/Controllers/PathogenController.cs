using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.Services;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

public record RecordObservationRequest(int TestOrderId, string StepName, bool GrowthObserved);

// Universal chain: TSB -> Observation -> Continue -> Detection Media ->
// Growth = Detected / No Growth = Absent. Salmonella exception:
// TSB -> RVS -> XLD+TSI -> Detected/Absent. See PathogenWorkflowEngine.
[ApiController]
[Route("api/pathogen")]
[Authorize]
public class PathogenController : ControllerBase
{
    private readonly PathogenService _pathogenService;

    public PathogenController(PathogenService pathogenService)
    {
        _pathogenService = pathogenService;
    }

    [HttpPost("observation")]
    public async Task<IActionResult> RecordObservation(RecordObservationRequest request)
    {
        var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        var observation = await _pathogenService.RecordObservationAsync(request.TestOrderId, request.StepName, request.GrowthObserved, userId);
        return Ok(ApiResponse<object>.Ok(observation));
    }

    [HttpGet("interpret/{testOrderId}")]
    public async Task<IActionResult> Interpret(int testOrderId) =>
        Ok(ApiResponse<object>.Ok(new { result = await _pathogenService.InterpretAsync(testOrderId) }));
}
