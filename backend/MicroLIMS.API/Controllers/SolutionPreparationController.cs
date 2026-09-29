using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

// Solution Preparation area (HPLC chain S4, spec 4). Reads are open to any
// authenticated user - the picker and lot-trace views need them; every
// write (start/save/complete/cancel/discard) requires Solutions.Prepare.
[ApiController]
[Route("api/solution-preparations")]
[Authorize]
public class SolutionPreparationController : ControllerBase
{
    private readonly SolutionPreparationService _service;
    public SolutionPreparationController(SolutionPreparationService service) => _service = service;

    private int CurrentUserId => int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
    private string? ClientIpAddress => HttpContext.Connection.RemoteIpAddress?.ToString();

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] SolutionPreparationStatus? status, [FromQuery] SolutionType? type) =>
        Ok(ApiResponse<object>.Ok(await _service.GetAllAsync(CurrentUserId, status, type)));

    // Static segments ("available", "by-lot/{materialId}") are matched
    // before {id:int} only because they are not numeric - order here does
    // not matter to routing, but is kept list-then-detail for readability.
    [HttpGet("available")]
    public async Task<IActionResult> GetAvailable([FromQuery] int solutionMasterId, [FromQuery] int? hplcMethodId) =>
        Ok(ApiResponse<object>.Ok(await _service.GetAvailableAsync(CurrentUserId, solutionMasterId, hplcMethodId)));

    [HttpGet("by-lot/{materialId:int}")]
    public async Task<IActionResult> GetByLot(int materialId) =>
        Ok(ApiResponse<object>.Ok(await _service.GetByLotAsync(materialId, CurrentUserId)));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id) =>
        Ok(ApiResponse<object>.Ok(await _service.GetByIdAsync(id, CurrentUserId)));

    [HttpGet("{id:int}/components/{componentId:int}/lots")]
    public async Task<IActionResult> GetLotOptions(int id, int componentId) =>
        Ok(ApiResponse<object>.Ok(await _service.GetLotOptionsAsync(id, componentId, CurrentUserId)));

    [Authorize(Policy = PermissionConstants.SolutionsPrepare)]
    [HttpPost]
    public async Task<IActionResult> Start([FromBody] StartPreparationRequest r) =>
        Ok(ApiResponse<object>.Ok(await _service.StartAsync(r, CurrentUserId)));

    [Authorize(Policy = PermissionConstants.SolutionsPrepare)]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Save(int id, [FromBody] SavePreparationRequest r) =>
        Ok(ApiResponse<object>.Ok(await _service.SaveAsync(id, r, CurrentUserId)));

    [Authorize(Policy = PermissionConstants.SolutionsPrepare)]
    [HttpPost("{id:int}/complete")]
    public async Task<IActionResult> Complete(int id, [FromBody] CompletePreparationRequest r) =>
        Ok(ApiResponse<object>.Ok(await _service.CompleteAsync(id, r, CurrentUserId, ClientIpAddress)));

    [Authorize(Policy = PermissionConstants.SolutionsPrepare)]
    [HttpPost("{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id, [FromBody] ReasonRequest body) =>
        Ok(ApiResponse<object>.Ok(await _service.CancelAsync(id, body.Reason, CurrentUserId)));

    [Authorize(Policy = PermissionConstants.SolutionsPrepare)]
    [HttpPost("{id:int}/discard")]
    public async Task<IActionResult> Discard(int id, [FromBody] ReasonRequest body) =>
        Ok(ApiResponse<object>.Ok(await _service.DiscardAsync(id, body.Reason, CurrentUserId)));
}

public record ReasonRequest(string Reason);
