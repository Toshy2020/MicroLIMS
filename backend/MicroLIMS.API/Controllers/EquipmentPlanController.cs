using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.Services.EquipmentPlan;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

[ApiController]
[Route("api/equipment-plan")]
[Authorize]
public class EquipmentPlanController : ControllerBase
{
    private readonly EquipmentBaselineService _service;

    public EquipmentPlanController(EquipmentBaselineService service)
    {
        _service = service;
    }

    private int CurrentUserId => int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var uid) ? uid : 0;

    [Authorize(Policy = PermissionConstants.EquipmentPlanManage)]
    [HttpPut("instruments/{id:int}/schedule")]
    public async Task<IActionResult> SaveSchedule(
        int id,
        [FromBody] SaveEquipmentScheduleRequest request,
        [FromHeader(Name = "If-Match")] string? ifMatch = null)
    {
        uint version = 0;
        if (!string.IsNullOrWhiteSpace(ifMatch))
        {
            uint.TryParse(ifMatch.Trim().Trim('"'), NumberStyles.None, CultureInfo.InvariantCulture, out version);
        }

        var result = await _service.SaveScheduleAsync(id, request, version, CurrentUserId);
        return Ok(ApiResponse<EquipmentScheduleResponse>.Ok(result));
    }
}
