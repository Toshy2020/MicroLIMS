using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.Services.Monitoring;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

public record GeneratePlanRequest(int Year);

// Annual Monitoring Plan: yearly revisions. The rules live in
// MonitoringPlanService; this only maps HTTP to it.
[ApiController]
[Route("api/monitoring-plan")]
[Authorize]
public class MonitoringPlanController : ControllerBase
{
    private readonly MonitoringPlanService _service;

    public MonitoringPlanController(MonitoringPlanService service)
    {
        _service = service;
    }

    private int CurrentUserId => int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

    [Authorize(Policy = PermissionConstants.MonitoringPlanView)]
    [HttpGet("revisions")]
    public async Task<IActionResult> GetRevisions([FromQuery] int year) =>
        Ok(ApiResponse<object>.Ok(await _service.GetRevisionsAsync(year)));

    [Authorize(Policy = PermissionConstants.MonitoringPlanManage)]
    [HttpPost("revisions")]
    public async Task<IActionResult> GenerateRevision(GeneratePlanRequest request) =>
        Ok(ApiResponse<object>.Ok(await _service.GenerateDraftAsync(request.Year, CurrentUserId)));

    [Authorize(Policy = PermissionConstants.MonitoringPlanView)]
    [HttpGet("revisions/{id}/preview")]
    public async Task<IActionResult> GetPreview(int id) =>
        Ok(ApiResponse<object>.Ok(await _service.GetPreviewAsync(id)));

    [Authorize(Policy = PermissionConstants.MonitoringPlanApprove)]
    [HttpPost("revisions/{id}/approve")]
    public async Task<IActionResult> ApproveRevision(int id, ApprovePlanRequest request) =>
        Ok(ApiResponse<object>.Ok(await _service.ApproveAsync(id, request, CurrentUserId, HttpContext.Connection.RemoteIpAddress?.ToString())));
}
