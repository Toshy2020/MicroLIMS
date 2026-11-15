using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.Services.Notifications;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

/// <summary>
/// Laboratory weekly working-hours schedule used for escalation thresholds.
/// </summary>
[ApiController]
[Route("api/working-hours")]
[Authorize]
public class WorkingHoursController : ControllerBase
{
    private readonly WorkingHoursService _service;

    public WorkingHoursController(WorkingHoursService service) => _service = service;

    private int CurrentUserId =>
        int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id)
            ? id
            : throw new UnauthorizedAccessException("User identity claim missing.");

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct = default)
    {
        var result = await _service.GetAsync(ct);
        return Ok(ApiResponse<WorkingHoursDto>.Ok(result));
    }

    [HttpPut]
    [Authorize(Policy = PermissionConstants.NotificationsConfigure)]
    public async Task<IActionResult> Update(
        [FromBody] UpdateWorkingHoursRequest request,
        CancellationToken ct = default)
    {
        var result = await _service.UpdateAsync(CurrentUserId, request, ct);
        return Ok(ApiResponse<WorkingHoursDto>.Ok(result));
    }
}
