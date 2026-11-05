using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

// Solution Preparation area (HPLC chain S4, spec 4). Reads are open to any
// authenticated user - the picker and lot-trace views need them; every
// write (start/save/complete/cancel/discard) requires Solutions.Prepare;
// the release decision requires Solutions.Release.
[ApiController]
[Route("api/solution-preparations")]
[Authorize]
public class SolutionPreparationController : ControllerBase
{
    private readonly SolutionPreparationService _service;
    private readonly TitrantStandardizationService _standardizations;
    private readonly SolutionPreparationReleaseService _release;
    public SolutionPreparationController(SolutionPreparationService service, TitrantStandardizationService standardizations, SolutionPreparationReleaseService release)
    {
        _service = service;
        _standardizations = standardizations;
        _release = release;
    }

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

    [HttpGet("usable")]
    public async Task<IActionResult> GetUsable([FromQuery] int? sectionId) =>
        Ok(ApiResponse<object>.Ok(await _service.GetUsableAsync(CurrentUserId, sectionId)));

    [HttpGet("by-lot/{materialId:int}")]
    public async Task<IActionResult> GetByLot(int materialId) =>
        Ok(ApiResponse<object>.Ok(await _service.GetByLotAsync(materialId, CurrentUserId)));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id) =>
        Ok(ApiResponse<object>.Ok(await _service.GetByIdAsync(id, CurrentUserId)));

    [HttpGet("{id:int}/components/{componentId:int}/lots")]
    public async Task<IActionResult> GetLotOptions(int id, int componentId) =>
        Ok(ApiResponse<object>.Ok(await _service.GetLotOptionsAsync(id, componentId, CurrentUserId)));

    [HttpGet("{id:int}/components/{componentId:int}/source-preparations")]
    public async Task<IActionResult> GetSourcePreparations(int id, int componentId) =>
        Ok(ApiResponse<object>.Ok(await _service.GetSourcePreparationsAsync(id, componentId, CurrentUserId)));

    // Release gate for a recipe that requires release before use (Reviewer or Section Head,
    // never the preparer). Approve or reject with an electronic signature.
    [Authorize(Policy = PermissionConstants.SolutionsRelease)]
    [HttpPost("{id:int}/release")]
    public async Task<IActionResult> Release(int id, [FromBody] ReleaseSolutionPreparationRequest r)
    {
        await _release.DecideAsync(id, r, CurrentUserId, ClientIpAddress);
        return Ok(ApiResponse<object>.Ok(await _service.GetByIdAsync(id, CurrentUserId)));
    }

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

    // Titrant standardization (HPLC chain S5, spec 4). Reads are open to any
    // authenticated user - same reasoning as the preparation reads above;
    // the standardize write requires Solutions.Prepare.
    [HttpGet("{id:int}/standardizations")]
    public async Task<IActionResult> GetStandardizations(int id) =>
        Ok(ApiResponse<object>.Ok(await _standardizations.GetForPreparationAsync(id, CurrentUserId)));

    [HttpGet("{id:int}/standardizations/standard-lots")]
    public async Task<IActionResult> GetStandardLotOptions(int id) =>
        Ok(ApiResponse<object>.Ok(await _standardizations.GetStandardLotOptionsAsync(id, CurrentUserId)));

    [HttpGet("{id:int}/standardizations/reference-options")]
    public async Task<IActionResult> GetReferenceOptions(int id) =>
        Ok(ApiResponse<object>.Ok(await _standardizations.GetReferenceOptionsAsync(id, CurrentUserId)));

    [Authorize(Policy = PermissionConstants.SolutionsPrepare)]
    [HttpPost("{id:int}/standardizations")]
    public async Task<IActionResult> Standardize(int id, [FromBody] StandardizeRequest r) =>
        Ok(ApiResponse<object>.Ok(await _standardizations.StandardizeAsync(id, r, CurrentUserId, ClientIpAddress)));
}

public record ReasonRequest(string Reason);
