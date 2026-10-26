using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

public record SelectThawedVialRequest(int ThawEventId);
public record ThawAndSelectVialRequest(int CryovialId, string? Notes);
public record SelectLyophilizedDiskRequest(int MaterialId);
public record RecordIncubationRequest(int IncubatorEquipmentId);
public record RecordResultHttpRequest(
    decimal? OldMediaCount, decimal? NewMediaCount,
    int? ReferenceMediaId, string? ReferenceMediaLabel,
    bool? GrowthObserved,
    string? ObservedDescription, bool? ManualConform,
    bool? IsTurbid);

// Media preparation auto-assigns a MediaEvaluation (see
// MediaPreparationService.PrepareAsync) - this controller drives it
// through organism-source selection (a thawed cryovial vial or a
// lyophilized disk), incubation, and result recording. See
// MediaEvaluationEngine.
[ApiController]
[Route("api/media-evaluations")]
[Authorize(Policy = PermissionConstants.TestWorkflowExecute)]
public class MediaEvaluationController : ControllerBase
{
    private readonly MediaEvaluationService _service;
    private readonly IUserSectionScopeService _scopeService;

    public MediaEvaluationController(MediaEvaluationService service, IUserSectionScopeService scopeService)
    {
        _service = service;
        _scopeService = scopeService;
    }

    private int CurrentUserId => int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] MediaEvaluationStatus? status) =>
        Ok(ApiResponse<object>.Ok(await _service.GetAllAsync(status, (await _scopeService.GetReadableSectionIdsAsync(CurrentUserId)))));

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        await _scopeService.EnsureMediaEvaluationAccessAsync(CurrentUserId, id, access: SectionAccess.Read);
        return Ok(ApiResponse<object>.Ok(await _service.GetByIdAsync(id)));
    }

    // Vials already thawed that this challenge can use - empty means the
    // analyst must thaw one (thaw-vial below) before the challenge can run.
    [HttpGet("challenges/{challengeId}/thawed-vials")]
    public async Task<IActionResult> GetThawedVials(int challengeId)
    {
        await _scopeService.EnsureMediaEvaluationChallengeAccessAsync(CurrentUserId, challengeId, access: SectionAccess.Read);
        return Ok(ApiResponse<object>.Ok(await _service.GetThawedVialOptionsAsync(challengeId)));
    }

    [HttpPost("challenges/{challengeId}/thawed-vial")]
    public async Task<IActionResult> SelectThawedVial(int challengeId, SelectThawedVialRequest r)
    {
        await _scopeService.EnsureMediaEvaluationChallengeAccessAsync(CurrentUserId, challengeId);
        await _service.SelectThawedVialAsync(challengeId, r.ThawEventId, CurrentUserId);
        return Ok(ApiResponse<object>.Ok(new { }));
    }

    [HttpPost("challenges/{challengeId}/thaw-vial")]
    public async Task<IActionResult> ThawAndSelectVial(int challengeId, ThawAndSelectVialRequest r)
    {
        await _scopeService.EnsureMediaEvaluationChallengeAccessAsync(CurrentUserId, challengeId);
        await _scopeService.EnsureCryovialAccessAsync(CurrentUserId, r.CryovialId);
        return Ok(ApiResponse<object>.Ok(await _service.ThawAndSelectVialAsync(challengeId, r.CryovialId, CurrentUserId, r.Notes)));
    }

    [HttpPost("challenges/{challengeId}/lyophilized-disk")]
    public async Task<IActionResult> SelectLyophilizedDisk(int challengeId, SelectLyophilizedDiskRequest r)
    {
        await _scopeService.EnsureMediaEvaluationChallengeAccessAsync(CurrentUserId, challengeId);
        await _service.SelectLyophilizedDiskAsync(challengeId, r.MaterialId, CurrentUserId);
        return Ok(ApiResponse<object>.Ok(new { }));
    }

    [HttpPost("challenges/{challengeId}/incubation")]
    public async Task<IActionResult> RecordIncubation(int challengeId, RecordIncubationRequest r)
    {
        await _scopeService.EnsureMediaEvaluationChallengeAccessAsync(CurrentUserId, challengeId);
        return Ok(ApiResponse<object>.Ok(await _service.RecordIncubationAsync(challengeId, r.IncubatorEquipmentId, CurrentUserId)));
    }

    [HttpPost("challenges/{challengeId}/result")]
    public async Task<IActionResult> RecordResult(int challengeId, RecordResultHttpRequest r)
    {
        await _scopeService.EnsureMediaEvaluationChallengeAccessAsync(CurrentUserId, challengeId);
        return Ok(ApiResponse<object>.Ok(await _service.RecordResultAsync(new RecordResultRequest(
            challengeId, CurrentUserId, r.OldMediaCount, r.NewMediaCount,
            r.ReferenceMediaId, r.ReferenceMediaLabel, r.GrowthObserved,
            r.ObservedDescription, r.ManualConform, r.IsTurbid))));
    }
}
