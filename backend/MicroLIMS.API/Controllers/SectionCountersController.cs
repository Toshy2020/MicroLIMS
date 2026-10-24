using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Services;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

// Live figures for the cards on a section page: a laboratory's (lab=MICRO /
// FP) or Document Control's.
// Each card's figures are computed only when the caller passes the policy of
// the page that card opens, so the counts never reveal more than the pages do.
[ApiController]
[Route("api/section-counters")]
[Authorize]
public class SectionCountersController : ControllerBase
{
    private readonly SectionCountersService _counters;
    private readonly IAuthorizationService _authorization;

    public SectionCountersController(SectionCountersService counters, IAuthorizationService authorization)
    {
        _counters = counters;
        _authorization = authorization;
    }

    private int CurrentUserId => int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

    private async Task<bool> Allowed(string policy) => (await _authorization.AuthorizeAsync(User, policy)).Succeeded;

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] string lab)
    {
        var access = new SectionCardAccess(
            Media: await Allowed(PermissionConstants.MediaPrepare),
            Cryovials: await Allowed(PermissionConstants.CryovialsManage),
            Materials: await Allowed(PermissionConstants.MaterialsManage),
            Equipment: await Allowed(PermissionConstants.EquipmentManage),
            Solutions: await Allowed(PermissionConstants.SolutionsPrepare));
        var cards = await _counters.GetAsync(CurrentUserId, lab, access);
        return Ok(ApiResponse<IReadOnlyList<SectionCardCountersDto>>.Ok(cards));
    }

    // The Document Control section page: the caller's own reading list.
    [HttpGet("document-control")]
    public async Task<IActionResult> GetDocumentControl() =>
        Ok(ApiResponse<IReadOnlyList<SectionCardCountersDto>>.Ok(await _counters.GetDocumentControlAsync(CurrentUserId)));
}
