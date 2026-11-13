using System.Security.Claims;
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
        Response.Headers.CacheControl = "no-store";
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

    [HttpPost("enrol/token/start")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitingPolicies.Authentication)]
    public async Task<IActionResult> EnrolTokenStart(MfaTokenRequest request, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        var started = await _mfaService.StartEnrolmentWithTokenAsync(request.MfaToken, ct);
        return Ok(ApiResponse<EnrolmentStart>.Ok(started));
    }

    [HttpPost("enrol/token/confirm")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitingPolicies.Authentication)]
    public async Task<IActionResult> EnrolTokenConfirm(MfaTokenConfirmRequest request, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = Request.Headers.UserAgent.ToString();
        var result = await _mfaService.ConfirmEnrolmentWithTokenAsync(request.MfaToken, request.Code, ip, userAgent, ct);
        return Ok(ApiResponse<object>.Ok(new
        {
            recoveryCodes = result.RecoveryCodes,
            login = AuthenticationController.ToLoginResponse(result.Login!)
        }));
    }

    [HttpGet("status")]
    [Authorize]
    public async Task<IActionResult> Status(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var status = await _mfaService.GetStatusAsync(userId, ct);
        return Ok(ApiResponse<MfaStatus>.Ok(status));
    }

    [HttpPost("enrol/start")]
    [Authorize]
    [EnableRateLimiting(RateLimitingPolicies.PasswordChange)]
    public async Task<IActionResult> EnrolStart(MfaPasswordRequest request, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        var userId = GetCurrentUserId();
        var started = await _mfaService.StartEnrolmentWithPasswordAsync(userId, request.Password, ct);
        return Ok(ApiResponse<EnrolmentStart>.Ok(started));
    }

    [HttpPost("enrol/confirm")]
    [Authorize]
    public async Task<IActionResult> EnrolConfirm(MfaCodeRequest request, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        var userId = GetCurrentUserId();
        var result = await _mfaService.ConfirmEnrolmentAsync(userId, request.Code, ct);
        return Ok(ApiResponse<object>.Ok(new { recoveryCodes = result.RecoveryCodes }));
    }

    [HttpPost("recovery-codes")]
    [Authorize]
    [EnableRateLimiting(RateLimitingPolicies.PasswordChange)]
    public async Task<IActionResult> RegenerateRecoveryCodes(MfaPasswordAndCodeRequest request, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        var userId = GetCurrentUserId();
        var codes = await _mfaService.RegenerateRecoveryCodesAsync(userId, request.Password, request.Code, ct);
        return Ok(ApiResponse<object>.Ok(new { recoveryCodes = codes }));
    }

    [HttpDelete]
    [Authorize]
    [EnableRateLimiting(RateLimitingPolicies.PasswordChange)]
    public async Task<IActionResult> RemoveFactor([FromBody] MfaPasswordAndCodeRequest request, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        await _mfaService.RemoveOwnFactorAsync(userId, request.Password, request.Code, ct);
        return Ok(ApiResponse<object>.Ok(new { }));
    }

    [HttpDelete("trusted-devices/{id:int}")]
    [Authorize]
    public async Task<IActionResult> ForgetTrustedDevice(int id, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        await _mfaService.ForgetOwnTrustedDeviceAsync(userId, id, ct);
        return Ok(ApiResponse<object>.Ok(new { }));
    }

    private int GetCurrentUserId() => int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
}
