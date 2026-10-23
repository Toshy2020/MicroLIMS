using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.Services.EquipmentPlan;
using MicroLIMS.Application.Services.Monitoring;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

[ApiController]
[Route("api/equipment-plan")]
[Authorize]
public class EquipmentPlanRevisionsController : ControllerBase
{
    private readonly EquipmentPlanService _service;

    public EquipmentPlanRevisionsController(EquipmentPlanService service)
    {
        _service = service;
    }

    private int CurrentUserId => int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var uid) ? uid : 0;

    [Authorize(Policy = PermissionConstants.MonitoringPlanView)]
    [HttpGet("revisions")]
    public async Task<IActionResult> GetRevisions([FromQuery] int year) =>
        Ok(ApiResponse<object>.Ok(await _service.GetRevisionsAsync(year)));

    [Authorize(Policy = PermissionConstants.EquipmentPlanManage)]
    [HttpPost("drafts")]
    public async Task<IActionResult> GenerateDraft([FromBody] GenerateEquipmentDraftRequest request) =>
        Ok(ApiResponse<object>.Ok(await _service.GenerateDraftAsync(request.Year, CurrentUserId, request.GoLiveDate)));

    [Authorize(Policy = PermissionConstants.MonitoringPlanView)]
    [HttpGet("revisions/{id:int}/preview")]
    public async Task<IActionResult> GetPreview(int id) =>
        Ok(ApiResponse<object>.Ok(await _service.GetPreviewAsync(id)));

    [Authorize(Policy = PermissionConstants.EquipmentPlanApprove)]
    [HttpPost("revisions/{id:int}/approve")]
    public async Task<IActionResult> ApproveRevision(int id, [FromBody] ApprovePlanRequest request) =>
        Ok(ApiResponse<object>.Ok(await _service.ApproveAsync(id, request, CurrentUserId, HttpContext.Connection.RemoteIpAddress?.ToString())));
}
