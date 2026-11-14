using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services.SystemSettings;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

// What the browser may read from the server settings. Display and local-storage
// parameters only; nothing here decides a laboratory outcome. Other modules add
// fields here rather than creating their own endpoint.
public sealed record ClientSettingsResponse(int BenchDraftRetentionHours, IReadOnlyList<string> Features);

[ApiController]
[Route("api/client-settings")]
[Authorize]
public class ClientSettingsController : ControllerBase
{
    private readonly ISystemSettingsReader _settings;

    public ClientSettingsController(ISystemSettingsReader settings) => _settings = settings;

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken = default)
    {
        var retention = await _settings.GetIntAsync(SystemSettingKeys.BenchDraftRetentionHours, cancellationToken);
        return Ok(ApiResponse<ClientSettingsResponse>.Ok(new ClientSettingsResponse(retention, Array.Empty<string>())));
    }
}
