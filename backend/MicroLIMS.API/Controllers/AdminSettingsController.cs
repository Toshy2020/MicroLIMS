using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

// System settings registry for the admin panel. The panel controls the system,
// never GxP data: these are operating parameters. Every change needs a reason;
// GxP-relevant keys also need the administrator's password (electronic signature).
[ApiController]
[Route("api/admin/settings")]
[Authorize(Policy = PermissionConstants.SystemConfigureSettings)]
public class AdminSettingsController : ControllerBase
{
    private readonly ISystemSettingsService _service;

    public AdminSettingsController(ISystemSettingsService service)
    {
        _service = service;
    }

    private int CurrentUserId => int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
    private string? ClientIp => HttpContext.Connection.RemoteIpAddress?.ToString();

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct) =>
        Ok(ApiResponse<object>.Ok(await _service.GetOverviewAsync(ct)));

    [HttpPut("{key}")]
    public async Task<IActionResult> Update(string key, UpdateSystemSettingRequest request) =>
        Ok(ApiResponse<object>.Ok(await _service.UpdateAsync(key, request.Value, request.Reason, CurrentUserId, request.Password, ClientIp)));

    [HttpGet("{key}/history")]
    public async Task<IActionResult> History(string key, CancellationToken ct) =>
        Ok(ApiResponse<object>.Ok(await _service.GetHistoryAsync(key, ct)));
}
