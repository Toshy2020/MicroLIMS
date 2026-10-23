using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.Services.Monitoring;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

// Deviations of the Annual Monitoring Plan: missed occurrences and results
// over their action limit. The rules live in MonitoringDeviationService.
[ApiController]
[Route("api/monitoring-deviations")]
[Authorize]
public class MonitoringDeviationsController : ControllerBase
{
    private readonly MonitoringDeviationService _service;

    public MonitoringDeviationsController(MonitoringDeviationService service)
    {
        _service = service;
    }

    private int CurrentUserId => int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

    [Authorize(Policy = PermissionConstants.MonitoringPlanView)]
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] DeviationStatus? status, [FromQuery] DeviationKind? kind) =>
        Ok(ApiResponse<object>.Ok(await _service.ListAsync(status, kind)));

    // Looks for new deviations now instead of waiting for the next scheduled scan.
    [Authorize(Policy = PermissionConstants.MonitoringPlanManage)]
    [HttpPost("scan")]
    public async Task<IActionResult> Scan() =>
        Ok(ApiResponse<object>.Ok(await _service.DetectAsync()));

    [Authorize(Policy = PermissionConstants.MonitoringPlanApprove)]
    [HttpPost("close")]
    public async Task<IActionResult> Close(CloseDeviationsRequest request) =>
        Ok(ApiResponse<object>.Ok(await _service.CloseAsync(request, CurrentUserId, HttpContext.Connection.RemoteIpAddress?.ToString())));
}
