using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.Services.Monitoring;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

// Annual Monitoring Plan: site-wide settings and the lab calendar's
// non-working dates. Logic lives in MonitoringSettingsService.
[ApiController]
[Route("api/monitoring")]
[Authorize]
public class MonitoringSettingsController : ControllerBase
{
    private readonly MonitoringSettingsService _service;

    public MonitoringSettingsController(MonitoringSettingsService service)
    {
        _service = service;
    }

    private int CurrentUserId => int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

    [HttpGet("settings")]
    public async Task<IActionResult> GetSettings() =>
        Ok(ApiResponse<object>.Ok(await _service.GetSettingsAsync()));

    [Authorize(Policy = PermissionConstants.MasterDataManage)]
    [HttpPut("settings")]
    public async Task<IActionResult> SaveSettings(MonitoringSettingsRequest request) =>
        Ok(ApiResponse<object>.Ok(await _service.SaveSettingsAsync(request, CurrentUserId)));

    [HttpGet("calendar")]
    public async Task<IActionResult> GetCalendarDates([FromQuery] int year) =>
        Ok(ApiResponse<object>.Ok(await _service.GetDatesAsync(year)));

    [Authorize(Policy = PermissionConstants.MasterDataManage)]
    [HttpPost("calendar")]
    public async Task<IActionResult> AddCalendarDate(LabCalendarDateRequest request) =>
        Ok(ApiResponse<object>.Ok(await _service.AddDateAsync(request)));

    [Authorize(Policy = PermissionConstants.MasterDataManage)]
    [HttpDelete("calendar/{id}")]
    public async Task<IActionResult> RemoveCalendarDate(int id)
    {
        await _service.RemoveDateAsync(id);
        return Ok(ApiResponse<object>.Ok(new { }));
    }

    [Authorize(Policy = PermissionConstants.MasterDataManage)]
    [HttpPost("frequencies/import-legacy")]
    public async Task<IActionResult> ImportLegacyFrequencies() =>
        Ok(ApiResponse<object>.Ok(await _service.ImportLegacyFrequenciesAsync()));
}
