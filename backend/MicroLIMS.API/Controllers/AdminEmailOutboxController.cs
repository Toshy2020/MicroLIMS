using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.Services.Notifications;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

/// <summary>
/// Administrative visibility into email outbox health and users missing an email address.
/// </summary>
[ApiController]
[Route("api/admin/email-outbox")]
[Authorize(Policy = PermissionConstants.UsersManage)]
public class AdminEmailOutboxController : ControllerBase
{
    private readonly NotificationQueryService _service;

    public AdminEmailOutboxController(NotificationQueryService service) => _service = service;

    private int CurrentUserId =>
        int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id)
            ? id
            : throw new UnauthorizedAccessException("User identity claim missing.");

    [HttpGet("summary")]
    public async Task<IActionResult> Summary(CancellationToken ct = default)
    {
        var summary = await _service.GetOutboxSummaryAsync(CurrentUserId, ct);
        return Ok(ApiResponse<EmailOutboxSummaryDto>.Ok(summary));
    }
}
