using System.Globalization;
using System.Security.Claims;
using MicroLIMS.API.Extensions;
using MicroLIMS.Application.Labels;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Shared.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MicroLIMS.API.Controllers;

[ApiController]
[Route("api/scan")]
[Authorize]
public class ScanController(IScanResolveService scanResolveService) : ControllerBase
{
    private readonly IScanResolveService _scanResolveService =
        scanResolveService ?? throw new ArgumentNullException(nameof(scanResolveService));

    private int CurrentUserId =>
        int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var uid)
            ? uid
            : throw new UnauthorizedAccessException("Authenticated user identity could not be determined.");

    [HttpGet("resolve")]
    // No per-endpoint limiter yet: the only per-user policy is the password-change one (5 per 15 minutes),
    // which would block normal scanning. A dedicated scan limit needs an approved number first.
    public async Task<IActionResult> Resolve([FromQuery] string? value, CancellationToken ct)
    {
        try
        {
            var result = await _scanResolveService.ResolveAsync(value, CurrentUserId, ct);
            return Ok(ApiResponse<ScanResolveResponse>.Ok(result));
        }
        catch (BusinessRuleException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }
}
