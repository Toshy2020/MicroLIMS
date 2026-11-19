using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MicroLIMS.API.Extensions;
using MicroLIMS.Application.Services.Sso;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

public record SsoExchangeRequest(string Code);

[ApiController]
[Route("api/auth/sso")]
[AllowAnonymous]
[EnableRateLimiting(RateLimitingPolicies.Authentication)]
public class SsoController : ControllerBase
{
    private readonly OidcSignInService _oidcSignIn;
    private readonly SsoModeService _ssoMode;
    private readonly IConfiguration _config;
    private readonly SsoOptions _options;

    public SsoController(OidcSignInService oidcSignIn, SsoModeService ssoMode, IConfiguration config, SsoOptions options)
    {
        _oidcSignIn = oidcSignIn;
        _ssoMode = ssoMode;
        _config = config;
        _options = options;
    }

    [HttpGet("mode")]
    public async Task<IActionResult> GetMode(CancellationToken ct)
    {
        var mode = await _ssoMode.GetModeAsync(ct);
        return Ok(ApiResponse<object>.Ok(new { mode = mode.ToString() }));
    }

    [HttpGet("start")]
    public async Task<IActionResult> Start(CancellationToken ct)
    {
        try
        {
            var authorizeUri = await _oidcSignIn.StartAsync(BuildCallbackUri(), ct);
            return Redirect(authorizeUri.ToString());
        }
        catch (SsoModeNotEnabledException)
        {
            return NotFound();
        }
    }

    [HttpGet("callback")]
    public async Task<IActionResult> Callback(
        [FromQuery] string? state,
        [FromQuery] string? code,
        [FromQuery] string? error,
        CancellationToken ct)
    {
        try
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            var redirectUri = await _oidcSignIn.HandleCallbackAsync(
                state,
                code,
                error,
                BuildCallbackUri(),
                ResolveFrontendOrigin(),
                ip,
                ct);
            return Redirect(redirectUri.ToString());
        }
        catch (SsoModeNotEnabledException)
        {
            return NotFound();
        }
    }

    [HttpPost("exchange")]
    public async Task<IActionResult> Exchange([FromBody] SsoExchangeRequest request, CancellationToken ct)
    {
        try
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            var userAgent = Request.Headers.UserAgent.ToString();
            var outcome = await _oidcSignIn.ExchangeAsync(request.Code, ip, userAgent, ct);
            if (!outcome.Success)
                return Unauthorized(ApiResponse<object>.Fail(outcome.FailureReason ?? "SSO exchange failed."));

            return Ok(ApiResponse<object>.Ok(AuthenticationController.ToLoginResponse(outcome)));
        }
        catch (SsoModeNotEnabledException)
        {
            return NotFound();
        }
    }

    private string BuildCallbackUri() =>
        string.IsNullOrWhiteSpace(_options.Oidc.RedirectUri)
            ? $"{Request.Scheme}://{Request.Host}{Request.PathBase}/api/auth/sso/callback"
            : _options.Oidc.RedirectUri.Trim();

    private string ResolveFrontendOrigin()
    {
        var raw = _config["Frontend:Origin"]
            ?? _config["Frontend__Origin"]
            ?? "http://localhost:5173";

        var first = raw.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();

        return string.IsNullOrWhiteSpace(first) ? "http://localhost:5173" : first;
    }
}
