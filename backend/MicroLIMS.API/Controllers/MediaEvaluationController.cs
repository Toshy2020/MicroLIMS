using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

public record SelectCryovialRequest(int CryovialId);
public record RecordIncubationRequest(int IncubatorEquipmentId);
public record RecordResultHttpRequest(
    decimal? OldMediaCount, decimal? NewMediaCount,
    bool? GrowthObserved,
    string? ObservedDescription, bool? ManualConform,
    bool? IsTurbid);

// Media preparation auto-assigns a MediaEvaluation (see
// MediaPreparationService.PrepareAsync) - this controller drives it
// through cryovial selection, incubation, and result recording. See
// MediaEvaluationEngine.
[ApiController]
[Route("api/media-evaluations")]
[Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.Analyst + "," + RoleConstants.Reviewer + "," + RoleConstants.SystemAdministrator)]
public class MediaEvaluationController : ControllerBase
{
    private readonly MediaEvaluationService _service;

    public MediaEvaluationController(MediaEvaluationService service)
    {
        _service = service;
    }

    private int CurrentUserId => int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] MediaEvaluationStatus? status) =>
        Ok(ApiResponse<object>.Ok(await _service.GetAllAsync(status)));

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id) =>
        Ok(ApiResponse<object>.Ok(await _service.GetByIdAsync(id)));

    [HttpPost("challenges/{challengeId}/cryovial")]
    public async Task<IActionResult> SelectCryovial(int challengeId, SelectCryovialRequest r)
    {
        await _service.SelectCryovialAsync(challengeId, r.CryovialId, CurrentUserId);
        return Ok(ApiResponse<object>.Ok(new { }));
    }

    [HttpPost("challenges/{challengeId}/incubation")]
    public async Task<IActionResult> RecordIncubation(int challengeId, RecordIncubationRequest r) =>
        Ok(ApiResponse<object>.Ok(await _service.RecordIncubationAsync(challengeId, r.IncubatorEquipmentId, CurrentUserId)));

    [HttpPost("challenges/{challengeId}/result")]
    public async Task<IActionResult> RecordResult(int challengeId, RecordResultHttpRequest r) =>
        Ok(ApiResponse<object>.Ok(await _service.RecordResultAsync(new RecordResultRequest(
            challengeId, CurrentUserId, r.OldMediaCount, r.NewMediaCount, r.GrowthObserved,
            r.ObservedDescription, r.ManualConform, r.IsTurbid))));
}
