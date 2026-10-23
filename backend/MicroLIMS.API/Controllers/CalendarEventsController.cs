using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.Services.EquipmentPlan;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

[ApiController]
[Authorize]
public class CalendarEventsController : ControllerBase
{
    private readonly CalendarEventService _service;

    public CalendarEventsController(CalendarEventService service)
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

    [HttpGet("api/calendar-events")]
    [Authorize(Policy = PermissionConstants.MonitoringPlanView)]
    public async Task<IActionResult> GetEvents([FromQuery] DateOnly from, [FromQuery] DateOnly to)
    {
        var result = await _service.GetCalendarEventsAsync(from, to);
        return Ok(ApiResponse<object>.Ok(result));
    }

    [HttpPost("api/calendar-events")]
    [Authorize(Policy = PermissionConstants.CalendarEventManage)]
    public async Task<IActionResult> Create([FromBody] CreateCalendarEventRequest request)
    {
        var result = await _service.CreateAsync(request, CurrentUserId);
        return Ok(ApiResponse<object>.Ok(result));
    }

    [HttpPut("api/calendar-events/{id:int}")]
    [Authorize(Policy = PermissionConstants.CalendarEventManage)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateCalendarEventRequest request)
    {
        var result = await _service.UpdateAsync(id, request, CurrentUserId);
        return Ok(ApiResponse<object>.Ok(result));
    }

    [HttpPost("api/calendar-events/{id:int}/cancel")]
    [Authorize(Policy = PermissionConstants.CalendarEventManage)]
    public async Task<IActionResult> Cancel(int id, [FromBody] CancelCalendarEventRequest request)
    {
        var reason = request?.Reason ?? string.Empty;
        var result = await _service.CancelAsync(id, reason, CurrentUserId);
        return Ok(ApiResponse<object>.Ok(result));
    }

    [HttpPost("api/calendar-events/{id:int}/documents")]
    [Authorize(Policy = PermissionConstants.CalendarEventManage)]
    [RequestSizeLimit(30 * 1024 * 1024)]
    public async Task<IActionResult> UploadDocument(int id, IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(ApiResponse<object>.Fail("No file was provided."));

        byte[] content;
        using (var ms = new MemoryStream())
        {
            await file.CopyToAsync(ms);
            content = ms.ToArray();
        }

        var result = await _service.UploadDocumentAsync(id, new UploadCalendarEventDocumentRequest(
            file.FileName,
            file.ContentType,
            content), CurrentUserId);

        return Ok(ApiResponse<object>.Ok(result));
    }

    [HttpGet("api/calendar-event-documents/{docId:int}/content")]
    [Authorize(Policy = PermissionConstants.MonitoringPlanView)]
    public async Task<IActionResult> GetDocumentContent(int docId)
    {
        var (meta, bytes) = await _service.GetDocumentContentAsync(docId, CurrentUserId);
        return File(bytes, meta.ContentType, meta.OriginalFileName);
    }
}
