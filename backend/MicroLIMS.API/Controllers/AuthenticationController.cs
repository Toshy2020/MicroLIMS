using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

public record LoginRequest(string Username, string Password);
public record RefreshRequest(string RefreshToken);
public record LogoutRequest(string? RefreshToken);
public record RequestPasswordResetRequest(string Username);
public record ConfirmPasswordResetRequest(string ResetToken, string NewPassword);
public record ChangePasswordRequest(string CurrentPassword, string NewPassword);
public record ConfirmAdminPasswordRecoveryRequest(string Username, string RecoveryCode, string NewPassword);

[ApiController]
[Route("api/auth")]
public class AuthenticationController : ControllerBase
{
    private readonly IAuthenticationService _authService;
    private readonly AdminPasswordRecoveryService _adminPasswordRecoveryService;

    public AuthenticationController(IAuthenticationService authService, AdminPasswordRecoveryService adminPasswordRecoveryService)
    {
        _authService = authService;
        _adminPasswordRecoveryService = adminPasswordRecoveryService;
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

        return Ok(ApiResponse<object>.Ok(new { message = "If that account exists, a reset link has been generated." }));
    }

    [HttpPost("password-reset/confirm")]
    [AllowAnonymous]
    public async Task<IActionResult> ConfirmPasswordReset(ConfirmPasswordResetRequest request)
    {
        var success = await _authService.ConfirmPasswordResetAsync(request.ResetToken, request.NewPassword);
        return success ? Ok(ApiResponse<object>.Ok(new { })) : BadRequest(ApiResponse<object>.Fail("Reset token is invalid or expired."));
    }

    [HttpPost("admin-password-recovery/confirm")]
    [AllowAnonymous]
    public async Task<IActionResult> ConfirmAdminPasswordRecovery(ConfirmAdminPasswordRecoveryRequest request)
    {
        try
        {
            await _adminPasswordRecoveryService.ConfirmRecoveryAsync(request.Username, request.RecoveryCode, request.NewPassword);
            return Ok(ApiResponse<object>.Ok(new { message = "Password recovery successful. You may now log in with your new password." }));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request)
    {
        var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        var success = await _authService.ChangePasswordAsync(userId, request.CurrentPassword, request.NewPassword);
        return success ? Ok(ApiResponse<object>.Ok(new { })) : BadRequest(ApiResponse<object>.Fail("Current password is incorrect."));
    }

    // The body is optional so an older client that posts nothing still
    // logs out - it just revokes every session instead of the named one.
    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] LogoutRequest? request)
    {
        var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        await _authService.LogoutAsync(userId, request?.RefreshToken);
        return Ok(ApiResponse<object>.Ok(new { }, "Logged out."));
    }

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
            jobTitle = info.JobTitle,
            lastLoginAt = info.LastLoginAt,
            passwordChangedAt = info.PasswordChangedAt,
            mustChangePassword = info.MustChangePassword
        }));
    }
}
