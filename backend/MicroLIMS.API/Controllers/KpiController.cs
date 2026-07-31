using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.Services;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

[ApiController]
[Route("api/kpi")]
[Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
public class KpiController : ControllerBase
{
    private readonly KpiService _kpiService;

    public KpiController(KpiService kpiService)
    {
        _kpiService = kpiService;
    }

    [HttpGet("analysts")]
    public async Task<IActionResult> GetAnalystKpis() => Ok(ApiResponse<object>.Ok(await _kpiService.GetAnalystKpisAsync()));

    [HttpGet("completion-stats")]
    public async Task<IActionResult> GetCompletionStats() => Ok(ApiResponse<object>.Ok(await _kpiService.GetCompletionStatsAsync()));

    [HttpGet("delay-tracking")]
    public async Task<IActionResult> GetDelayTracking() => Ok(ApiResponse<object>.Ok(await _kpiService.GetDelayTrackingAsync()));
}
