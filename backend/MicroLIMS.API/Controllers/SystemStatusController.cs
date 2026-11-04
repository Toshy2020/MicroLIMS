using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services.SystemSettings;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

// Public, read-only status the browser shows on every page, including the
// sign-in page: whether maintenance mode is on, its message, and the
// announcement banner. Anonymous by necessity, so it returns these four
// values and nothing else.
[ApiController]
[Route("api/system/status")]
public class SystemStatusController : ControllerBase
{
    private readonly ISystemSettingsReader _settings;

    public SystemStatusController(ISystemSettingsReader settings) => _settings = settings;

    public sealed record SystemStatusResponse(bool Maintenance, string Message, string Banner, string Severity);

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var status = await _settings.GetMaintenanceStatusAsync(cancellationToken);
        Response.Headers.CacheControl = "no-store";
        return Ok(ApiResponse<SystemStatusResponse>.Ok(
            new SystemStatusResponse(status.Maintenance, status.Message, status.Banner, status.Severity)));
    }
}
