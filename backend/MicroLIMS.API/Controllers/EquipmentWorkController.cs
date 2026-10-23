using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.Services.EquipmentPlan;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

[ApiController]
[Authorize]
public class EquipmentWorkController : ControllerBase
{
    private readonly EquipmentWorkService _service;

    public EquipmentWorkController(EquipmentWorkService service)
    {
        _service = service;
    }

    private int CurrentUserId
    {
        get
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            return claim != null && int.TryParse(claim.Value, out var id) ? id : 0;
        }
    }

    private string? ClientIp => HttpContext.Connection.RemoteIpAddress?.ToString();

    [HttpPost("api/equipment-plan/instruments/{id:int}/calibrations")]
    [Authorize(Policy = PermissionConstants.EquipmentWorkRecord)]
    public async Task<IActionResult> RecordCalibration(int id, [FromBody] RecordCalibrationRequest request)
    {
        var result = await _service.RecordCalibrationAsync(id, request, CurrentUserId, ClientIp);
        return Ok(ApiResponse<EquipmentTaskResponse>.Ok(result));
    }

    [HttpGet("api/equipment/{id:int}/maintenance-log")]
    [Authorize(Policy = PermissionConstants.MonitoringPlanView)]
    public async Task<IActionResult> GetMaintenanceLog(int id)
    {
        var result = await _service.GetMaintenanceLogAsync(id);
        return Ok(ApiResponse<List<MaintenanceLogEntryResponse>>.Ok(result));
    }

    [HttpPost("api/equipment/{id:int}/maintenance-log")]
    [Authorize(Policy = PermissionConstants.EquipmentWorkRecord)]
    public async Task<IActionResult> AddLogEntry(int id, [FromBody] AddLogEntryRequest request)
    {
        var result = await _service.AddLogEntryAsync(id, request, CurrentUserId, ClientIp);
        return Ok(ApiResponse<MaintenanceLogEntryResponse>.Ok(result));
    }

    [HttpPost("api/maintenance-log/{entryId:int}/void")]
    [Authorize(Policy = PermissionConstants.EquipmentWorkRecord)]
    public async Task<IActionResult> VoidLogEntry(int entryId, [FromBody] VoidLogEntryRequest request)
    {
        await _service.VoidLogEntryAsync(entryId, request.Reason, request.Password, CurrentUserId, ClientIp);
        return Ok(ApiResponse<object>.Ok(new { message = "Maintenance log entry voided." }));
    }
}
