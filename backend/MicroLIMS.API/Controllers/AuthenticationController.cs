using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

public record LoginRequest(string Username, string Password);
public record RefreshRequest(string RefreshToken);
public record RequestPasswordResetRequest(string Username);
public record ConfirmPasswordResetRequest(string ResetToken, string NewPassword);
public record ChangePasswordRequest(string CurrentPassword, string NewPassword);

[ApiController]
[Route("api/auth")]
public class AuthenticationController : ControllerBase
{
    private readonly IAuthenticationService _authService;

    public AuthenticationController(IAuthenticationService authService)
    {
        _authService = authService;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var outcome = await _authService.LoginAsync(request.Username, request.Password, ip);
        if (!outcome.Success) return Unauthorized(ApiResponse<object>.Fail(outcome.FailureReason ?? "Login failed."));
        return Ok(ApiResponse<object>.Ok(new { token = outcome.Token, refreshToken = outcome.RefreshToken, mustChangePassword = outcome.MustChangePassword }));
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh(RefreshRequest request)
    {
        var outcome = await _authService.RefreshAsync(request.RefreshToken);
        if (!outcome.Success) return Unauthorized(ApiResponse<object>.Fail(outcome.FailureReason ?? "Refresh failed."));
        return Ok(ApiResponse<object>.Ok(new { token = outcome.Token, refreshToken = outcome.RefreshToken }));
    }

    [HttpPost("password-reset/request")]
    [AllowAnonymous]
    public async Task<IActionResult> RequestPasswordReset(RequestPasswordResetRequest request)
    {
        try
        {
            await _authService.RequestPasswordResetAsync(request.Username);
        }
        catch (InvalidOperationException)
        {
            // Don't leak whether the account exists.
        }

        // Same generic message regardless of outcome - doesn't leak
        // whether the account exists or whether it has an email on file.
        return Ok(ApiResponse<object>.Ok(new { message = "If that account exists, a reset link has been generated." }));
    }

    [HttpPost("password-reset/confirm")]
    [AllowAnonymous]
    public async Task<IActionResult> ConfirmPasswordReset(ConfirmPasswordResetRequest request)
    {
        var success = await _authService.ConfirmPasswordResetAsync(request.ResetToken, request.NewPassword);
        return success ? Ok(ApiResponse<object>.Ok(new { })) : BadRequest(ApiResponse<object>.Fail("Reset token is invalid or expired."));
    }

    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request)
    {
        var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        var success = await _authService.ChangePasswordAsync(userId, request.CurrentPassword, request.NewPassword);
        return success ? Ok(ApiResponse<object>.Ok(new { })) : BadRequest(ApiResponse<object>.Fail("Current password is incorrect."));
    }

    [HttpPost("logout")]
    [Authorize]
    public IActionResult Logout() => Ok(ApiResponse<object>.Ok(new { }, "Logged out. Discard tokens client-side."));

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me()
    {
        var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        var info = await _authService.GetCurrentUserAsync(userId);
        if (info is null) return Unauthorized(ApiResponse<object>.Fail("User not found."));

        return Ok(ApiResponse<object>.Ok(new
        {
            userId = info.UserId,
            username = info.Username,
            fullName = info.FullName,
            role = info.Role,
            lastLoginAt = info.LastLoginAt,
            passwordChangedAt = info.PasswordChangedAt,
            mustChangePassword = info.MustChangePassword
        }));
    }
}
