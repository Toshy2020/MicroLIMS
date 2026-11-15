using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.Services.Notifications;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

/// <summary>
/// Notification centre endpoints for reading and acknowledging the authenticated user's notifications.
/// </summary>
[ApiController]
[Route("api/notifications")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly NotificationQueryService _service;

    public NotificationsController(NotificationQueryService service) => _service = service;

    private int CurrentUserId =>
        int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id)
            ? id
            : throw new UnauthorizedAccessException("User identity claim missing.");

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? state = null,
        [FromQuery] string? type = null,
        [FromQuery] int? sectionId = null,
        [FromQuery] string? q = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = NotificationQueryService.DefaultPageSize,
        CancellationToken ct = default)
    {
        var result = await _service.ListAsync(CurrentUserId, state, type, sectionId, q, page, pageSize, ct);
        return Ok(ApiResponse<PagedResult<NotificationDto>>.Ok(result));
    }

    [HttpGet("summary")]
    public async Task<IActionResult> Summary(CancellationToken ct = default)
    {
        var summary = await _service.GetSummaryAsync(CurrentUserId, ct);
        return Ok(ApiResponse<NotificationSummaryDto>.Ok(summary));
    }

    [HttpPost("{id:int}/read")]
    public async Task<IActionResult> MarkRead(int id, CancellationToken ct = default)
    {
        await _service.MarkReadAsync(CurrentUserId, id, ct);
        return Ok(ApiResponse<object>.Ok(new { }));
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct = default)
    {
        await _service.MarkAllReadAsync(CurrentUserId, ct);
        return Ok(ApiResponse<object>.Ok(new { }));
    }
}
