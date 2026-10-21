using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.Services.Monitoring;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

// Annual Monitoring Plan occurrences: what a monitoring session can fulfil.
[ApiController]
[Route("api/monitoring-occurrences")]
[Authorize]
public class MonitoringOccurrencesController : ControllerBase
{
    private readonly MonitoringLinkService _links;

    public MonitoringOccurrencesController(MonitoringLinkService links)
    {
        _links = links;
    }

    // The preparation checklist for a received sample: overdue, due now and
    // the rest of the month, for the sample's department, water department or machine.
    [Authorize(Policy = PermissionConstants.MonitoringPlanView)]
    [HttpGet("for-session")]
    public async Task<IActionResult> GetForSession([FromQuery] int sampleId) =>
        Ok(ApiResponse<object>.Ok(await _links.GetChecklistAsync(sampleId)));
}
