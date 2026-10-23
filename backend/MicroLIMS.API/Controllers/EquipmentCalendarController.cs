using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.Services.EquipmentPlan;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

[ApiController]
[Route("api/equipment-plan")]
[Authorize]
public class EquipmentCalendarController : ControllerBase
{
    private readonly EquipmentCalendarService _service;

    public EquipmentCalendarController(EquipmentCalendarService service)
    {
        _service = service;
    }

    [Authorize(Policy = PermissionConstants.MonitoringPlanView)]
    [HttpGet("calendar")]
    public async Task<IActionResult> GetCalendar([FromQuery] DateOnly from, [FromQuery] DateOnly to) =>
        Ok(ApiResponse<object>.Ok(await _service.GetCalendarAsync(from, to)));

    [Authorize(Policy = PermissionConstants.MonitoringPlanView)]
    [HttpGet("year")]
    public async Task<IActionResult> GetYear([FromQuery] int year) =>
        Ok(ApiResponse<object>.Ok(await _service.GetYearAsync(year)));

    [Authorize(Policy = PermissionConstants.MonitoringPlanView)]
    [HttpGet("instruments/{id:int}/history")]
    public async Task<IActionResult> GetInstrumentHistory(int id, [FromQuery] int year) =>
        Ok(ApiResponse<object>.Ok(await _service.GetInstrumentHistoryAsync(id, year)));

    [Authorize(Policy = PermissionConstants.MonitoringPlanView)]
    [HttpGet("planned-vs-done")]
    public async Task<IActionResult> GetPlannedVsDone([FromQuery] int year, [FromQuery] EquipmentTaskType type) =>
        Ok(ApiResponse<object>.Ok(await _service.GetPlannedVsDoneAsync(year, type)));
}
