using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

public record GeneralAgarRequest(int MediaId, string OrganismName, int CryovialId, string Atcc, string InitialInoculum, int OldMediaResult, int NewMediaResult, bool NegativeControlGrowth);
public record GeneralBrothRequest(int MediaId, string TurbidResult);
public record SelectiveRequest(int MediaId, string Panel, string OrganismName, int CryovialId, string InitialInoculum, string ObservationText, bool Passed);

// Media preparation -> Sterility -> Recovery -> Release. See GptWorkflowEngine.
[ApiController]
[Route("api/gpt")]
[Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.Analyst + "," + RoleConstants.SystemAdministrator)]
public class GptController : ControllerBase
{
    private readonly GptService _gptService;

    public GptController(GptService gptService)
    {
        _gptService = gptService;
    }

    private int CurrentUserId => int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

    [HttpGet("media")]
    public async Task<IActionResult> GetAllMedia() => Ok(ApiResponse<object>.Ok(await _gptService.GetAllMediaAsync()));

    [HttpPost("media/{mediaId}/advance")]
    public async Task<IActionResult> Advance(int mediaId) =>
        Ok(ApiResponse<object>.Ok(await _gptService.AdvanceStageAsync(mediaId, CurrentUserId)));

    [HttpPost("challenge/general-agar")]
    public async Task<IActionResult> GeneralAgar(GeneralAgarRequest r) =>
        Ok(ApiResponse<object>.Ok(await _gptService.RecordGeneralAgarAsync(new GeneralAgarChallengeRequest(
            r.MediaId, r.OrganismName, r.CryovialId, r.Atcc, r.InitialInoculum, r.OldMediaResult, r.NewMediaResult, r.NegativeControlGrowth, CurrentUserId))));

    [HttpPost("challenge/general-broth")]
    public async Task<IActionResult> GeneralBroth(GeneralBrothRequest r) =>
        Ok(ApiResponse<object>.Ok(await _gptService.RecordGeneralBrothAsync(new GeneralBrothChallengeRequest(r.MediaId, r.TurbidResult, CurrentUserId))));

    [HttpPost("challenge/selective")]
    public async Task<IActionResult> Selective(SelectiveRequest r) =>
        Ok(ApiResponse<object>.Ok(await _gptService.RecordSelectiveAsync(new SelectiveChallengeRequest(
            r.MediaId, r.Panel, r.OrganismName, r.CryovialId, r.InitialInoculum, r.ObservationText, r.Passed, CurrentUserId))));

    [HttpPost("media/{mediaId}/release")]
    public async Task<IActionResult> Release(int mediaId) =>
        Ok(ApiResponse<object>.Ok(await _gptService.ReleaseAsync(mediaId, CurrentUserId)));

    [HttpGet("media/{mediaId}/is-released")]
    public async Task<IActionResult> IsReleased(int mediaId) =>
        Ok(ApiResponse<object>.Ok(new { released = await _gptService.IsReleasedForUseAsync(mediaId) }));
}
