using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.Services.Monitoring;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

// Annual Monitoring Plan reports.
[ApiController]
[Route("api/monitoring-reports")]
[Authorize]
public class MonitoringReportsController : ControllerBase
{
    private readonly MonitoringReportService _service;

    public MonitoringReportsController(MonitoringReportService service)
    {
        _service = service;
    }

    // Planned, performed, late, overdue and skipped per target.
    [Authorize(Policy = PermissionConstants.MonitoringPlanView)]
    [HttpGet("coverage")]
    public async Task<IActionResult> GetCoverage([FromQuery] int year, [FromQuery] MonitoringCategory? category) =>
        Ok(ApiResponse<object>.Ok(await _service.GetCoverageAsync(year, category)));

    // Gaps between two fulfilments of one target that are too short or too long.
    [Authorize(Policy = PermissionConstants.MonitoringPlanView)]
    [HttpGet("spacing")]
    public async Task<IActionResult> GetSpacing(
        [FromQuery] int year, [FromQuery] MonitoringCategory? category, [FromQuery] bool flaggedOnly = false) =>
        Ok(ApiResponse<object>.Ok(await _service.GetSpacingAsync(year, category, flaggedOnly)));
}
