using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MicroLIMS.API.Extensions;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

[ApiController]
[Route("api/auth/mfa")]
public class MfaController : ControllerBase
{
    private readonly IMfaService _mfaService;

    public MfaController(IMfaService mfaService)
    {
        _mfaService = mfaService;
    }

    [HttpPost("verify")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitingPolicies.Authentication)]
    public async Task<IActionResult> Verify(MfaVerifyRequest request, CancellationToken ct)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = Request.Headers.UserAgent.ToString();
        var outcome = await _mfaService.VerifyAsync(
            request.MfaToken,
            request.Code,
            request.RecoveryCode,
            request.TrustDevice,
            ip,
            userAgent,
            ct);

        if (!outcome.Success)
            return Unauthorized(ApiResponse<object>.Fail(outcome.FailureReason ?? "The code is not correct."));

        return Ok(ApiResponse<object>.Ok(AuthenticationController.ToLoginResponse(outcome)));
    }
}
