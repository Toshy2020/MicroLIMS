using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.Services.Integrations;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

public record TestAlertRequest(string? Reason);
public record TestEmailRequest(string? To, string? Reason);
public record StorageProbeRequest(string? Reason);

// Integrations tab of the admin panel: what the system talks to (instrument
// agents, SMTP, file storage) and on-demand checks that it still can. The
// agent lifecycle actions (rotate, revoke) stay on InstrumentAgentController.
// Test sends and the storage probe need a reason, leave a security event and
// are limited to one call per minute.
[ApiController]
[Authorize(Policy = PermissionConstants.SystemConfigureSettings)]
public class AdminIntegrationsController : ControllerBase
{
    private readonly IIntegrationsService _service;
    public AdminIntegrationsController(IIntegrationsService service) => _service = service;

    private int CurrentUserId => int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

    [HttpPost("api/admin/alerts/test")]
    public Task<IActionResult> TestAlert([FromBody] TestAlertRequest request, CancellationToken ct) =>
        Limited(async () => await _service.SendTestAlertAsync(CurrentUserId, request.Reason, ct));

    [HttpGet("api/admin/integrations/instrument-agents")]
    public async Task<IActionResult> InstrumentAgents(CancellationToken ct) =>
        Ok(ApiResponse<object>.Ok(await _service.ListInstrumentAgentsAsync(ct)));

    [HttpGet("api/admin/integrations/smtp")]
    public IActionResult Smtp() => Ok(ApiResponse<object>.Ok(_service.GetSmtpStatus()));

    [HttpPost("api/admin/integrations/smtp/test")]
    public Task<IActionResult> TestEmail([FromBody] TestEmailRequest request, CancellationToken ct) =>
        Limited(async () => await _service.SendTestEmailAsync(CurrentUserId, request.To, request.Reason, ct));

    [HttpGet("api/admin/integrations/storage")]
    public IActionResult Storage() => Ok(ApiResponse<object>.Ok(_service.GetStorageStatus()));

    [HttpPost("api/admin/integrations/storage/probe")]
    public Task<IActionResult> StorageProbe([FromBody] StorageProbeRequest request, CancellationToken ct) =>
        Limited(async () => await _service.RunStorageProbeAsync(CurrentUserId, request.Reason, ct));

    private async Task<IActionResult> Limited(Func<Task<object>> action)
    {
        try
        {
            return Ok(ApiResponse<object>.Ok(await action()));
        }
        catch (RateLimitedException ex)
        {
            Response.Headers.RetryAfter = ex.RetryAfterSeconds.ToString();
            return StatusCode(StatusCodes.Status429TooManyRequests, ApiResponse<object>.Fail(ex.Message));
        }
    }
}
