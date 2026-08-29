using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.Services;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

// Standalone OOS tracking page - every Sample spun off by an
// out-of-specification approval decision, gated to the same roles as the
// approval decision itself (SampleApprovalController).
[ApiController]
[Route("api/oos-tracking")]
[Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
public class OosTrackingController : ControllerBase
{
    private readonly OosTrackingService _service;

    public OosTrackingController(OosTrackingService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> Get() =>
        Ok(ApiResponse<object>.Ok(await _service.GetOosGroupsAsync()));
}
