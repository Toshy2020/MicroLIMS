using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Services;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

// Cross-laboratory tracking board - every laboratory's own stage on every
// sample, in one paged list. Restricted to SamplesTrackAll: unlike the
// Testing Workspace queues, this deliberately isn't scoped to the caller's
// own section.
[ApiController]
[Route("api/samples/tracking")]
[Authorize(Policy = PermissionConstants.SamplesTrackAll)]
public class SampleTrackingController : ControllerBase
{
    private readonly SampleTrackingService _trackingService;

    public SampleTrackingController(SampleTrackingService trackingService)
    {
        _trackingService = trackingService;
    }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] SampleTrackingFilterDto filter)
    {
        var result = await _trackingService.GetTrackingAsync(filter);
        return Ok(ApiResponse<PagedResult<SampleTrackingRowDto>>.Ok(result));
    }
}
