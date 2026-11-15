using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.Services.Notifications;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

public sealed record UpdateNotificationPreferencesRequest(IReadOnlyList<UpdateNotificationPreferenceItem> Preferences);
public sealed record UpdateMandatoryNotificationTypesRequest(IReadOnlyList<string> Types);

/// <summary>
/// Per-user notification channel preferences and section-level mandatory notification settings.
/// </summary>
[ApiController]
[Route("api/notification-preferences")]
[Authorize]
public class NotificationPreferencesController : ControllerBase
{
    private readonly NotificationPreferencesService _service;

    public NotificationPreferencesController(NotificationPreferencesService service) => _service = service;

    private int CurrentUserId =>
        int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id)
            ? id
            : throw new UnauthorizedAccessException("User identity claim missing.");

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct = default)
    {
        var rows = await _service.GetPreferencesAsync(CurrentUserId, ct);
        return Ok(ApiResponse<IReadOnlyList<NotificationPreferenceRowDto>>.Ok(rows));
    }

    [HttpPut]
    public async Task<IActionResult> Update(
        [FromBody] UpdateNotificationPreferencesRequest request,
        CancellationToken ct = default)
    {
        var rows = await _service.UpdatePreferencesAsync(CurrentUserId, request.Preferences, ct);
        return Ok(ApiResponse<IReadOnlyList<NotificationPreferenceRowDto>>.Ok(rows));
    }

    [HttpGet("mandatory")]
    [Authorize(Policy = PermissionConstants.NotificationsConfigure)]
    public async Task<IActionResult> GetMandatory([FromQuery] int sectionId, CancellationToken ct = default)
    {
        var rows = await _service.GetMandatoryAsync(CurrentUserId, sectionId, ct);
        return Ok(ApiResponse<IReadOnlyList<MandatoryNotificationTypeDto>>.Ok(rows));
    }

    [HttpPut("mandatory")]
    [Authorize(Policy = PermissionConstants.NotificationsConfigure)]
    public async Task<IActionResult> SetMandatory(
        [FromQuery] int sectionId,
        [FromBody] UpdateMandatoryNotificationTypesRequest request,
        CancellationToken ct = default)
    {
        var rows = await _service.SetMandatoryAsync(CurrentUserId, sectionId, request.Types, ct);
        return Ok(ApiResponse<IReadOnlyList<MandatoryNotificationTypeDto>>.Ok(rows));
    }
}
