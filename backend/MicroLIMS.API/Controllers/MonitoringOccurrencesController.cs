using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.Services.Monitoring;
using MicroLIMS.Domain.Enums;
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
    private readonly MonitoringOccurrenceService _service;

    public MonitoringOccurrencesController(MonitoringLinkService links, MonitoringOccurrenceService service)
    {
        _links = links;
        _service = service;
    }

    private int CurrentUserId => int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

    // The preparation checklist for a received sample: overdue, due now and
    // the rest of the month, for the sample's department, water department or machine.
    [Authorize(Policy = PermissionConstants.MonitoringPlanView)]
    [HttpGet("for-session")]
    public async Task<IActionResult> GetForSession([FromQuery] int sampleId) =>
        Ok(ApiResponse<object>.Ok(await _links.GetChecklistAsync(sampleId)));

    // Occurrences whose window touches a date range, for the month and day grids.
    [Authorize(Policy = PermissionConstants.MonitoringPlanView)]
    [HttpGet("calendar")]
    public async Task<IActionResult> GetCalendar(
        [FromQuery] DateOnly from, [FromQuery] DateOnly to, [FromQuery] MonitoringCategory? category, [FromQuery] bool includeRetired = false) =>
        Ok(ApiResponse<object>.Ok(await _service.GetCalendarAsync(from, to, category, includeRetired)));

    // Month totals for the year grid.
    [Authorize(Policy = PermissionConstants.MonitoringPlanView)]
    [HttpGet("calendar/year")]
    public async Task<IActionResult> GetYearSummary([FromQuery] int year, [FromQuery] MonitoringCategory? category) =>
        Ok(ApiResponse<object>.Ok(await _service.GetYearSummaryAsync(year, category)));

    // What is still to do this month, and anything overdue.
    [Authorize(Policy = PermissionConstants.MonitoringPlanView)]
    [HttpGet("due")]
    public async Task<IActionResult> GetDue([FromQuery] MonitoringCategory? category) =>
        Ok(ApiResponse<object>.Ok(await _service.GetDueAsync(category)));

    [Authorize(Policy = PermissionConstants.MonitoringPlanView)]
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetDetail(int id) =>
        Ok(ApiResponse<object>.Ok(await _service.GetDetailAsync(id)));

    // A manual After Cleaning entry.
    [Authorize(Policy = PermissionConstants.MonitoringPlanManage)]
    [HttpPost("manual")]
    public async Task<IActionResult> CreateManual(CreateManualOccurrenceRequest request) =>
        Ok(ApiResponse<object>.Ok(await _service.CreateManualAsync(request, CurrentUserId)));

    [Authorize(Policy = PermissionConstants.MonitoringPlanManage)]
    [HttpPost("{id:int}/skip")]
    public async Task<IActionResult> Skip(int id, SkipOccurrenceRequest request) =>
        Ok(ApiResponse<object>.Ok(await _service.SkipAsync(id, request, CurrentUserId, HttpContext.Connection.RemoteIpAddress?.ToString())));

    [Authorize(Policy = PermissionConstants.MonitoringPlanManage)]
    [HttpPost("{id:int}/change-window")]
    public async Task<IActionResult> ChangeWindow(int id, ChangeWindowRequest request) =>
        Ok(ApiResponse<object>.Ok(await _service.ChangeWindowAsync(id, request, CurrentUserId, HttpContext.Connection.RemoteIpAddress?.ToString())));
}
