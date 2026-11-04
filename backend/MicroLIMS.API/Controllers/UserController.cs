using MicroLIMS.Shared.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

// Every administrative request carries a Reason. It is nullable here so a
// missing one reaches the service and is refused with the same 400 message
// as a blank one (AdminActionReason). Role change is also
// electronically signed: Password is the administrator's own, re-verified.
public record CreateUserRequest(string FullName, string Username, string Password, int RoleId, string? Email = null, string? JobTitle = null, string? Reason = null);
public record UpdateProfileRequest(string FullName, string Username, string? Email, string? JobTitle = null, string? Reason = null);
public record ChangeRoleRequest(int RoleId, string? Reason = null, string? Password = null);
public record SetStatusRequest(bool IsActive, string? Reason = null);
public record UnlockUserRequest(string? Reason = null);
public record AdminResetPasswordRequest(string? Reason = null);
public record ForcePasswordChangeRequest(string? Reason = null);
public record UpdateEmailRequest(string? Email, string? Reason = null);
public record AdminPasswordRecoveryRequest(string Reason);

[ApiController]
[Route("api/users")]
[Authorize(Policy = PermissionConstants.UsersManage)]
public class UserController : ControllerBase
{
    private readonly UserService _userService;
    private readonly AdminPasswordRecoveryService _adminPasswordRecoveryService;

    public UserController(UserService userService, AdminPasswordRecoveryService adminPasswordRecoveryService)
    {
        _userService = userService;
        _adminPasswordRecoveryService = adminPasswordRecoveryService;
    }

    private int CurrentUserId => int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

    private string? ClientIp => HttpContext.Connection.RemoteIpAddress?.ToString();

    [HttpGet]
    // ?status=active (default) | inactive | all. Read from the query string so the
    // action signature (and the reviewed authorization matrix) stays unchanged.
    public async Task<IActionResult> GetAll() => Ok(ApiResponse<object>.Ok(await _userService.GetAllAsync(Request.Query["status"].FirstOrDefault())));

    [HttpGet("{id}")]
    [Authorize(Policy = PermissionConstants.UsersManage)]
    public async Task<IActionResult> GetById(int id)
    {
        try
        {
            return Ok(ApiResponse<object>.Ok(await _userService.GetByIdAsync(id)));
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpPost]
    [Authorize(Policy = PermissionConstants.UsersManage)]
    public async Task<IActionResult> Create(CreateUserRequest request)
    {
        var user = new User { FullName = request.FullName, Username = request.Username, RoleId = request.RoleId, Email = request.Email, JobTitle = request.JobTitle };
        var created = await _userService.CreateAsync(user, request.Password, request.Reason);
        return Ok(ApiResponse<object>.Ok(created));
    }

    [HttpPut("{id}")]
    [Authorize(Policy = PermissionConstants.UsersManage)]
    public async Task<IActionResult> UpdateProfile(int id, UpdateProfileRequest request)
    {
        var updated = await _userService.UpdateProfileAsync(id, request.FullName, request.Username, request.Email, request.JobTitle, CurrentUserId, request.Reason);
        return Ok(ApiResponse<object>.Ok(updated));
    }

    [HttpPut("{id}/role")]
    [Authorize(Policy = PermissionConstants.UsersManage)]
    public async Task<IActionResult> ChangeRole(int id, ChangeRoleRequest request)
    {
        var updated = await _userService.ChangeRoleAsync(id, request.RoleId, request.Reason, CurrentUserId, request.Password, ClientIp);
        return Ok(ApiResponse<object>.Ok(updated));
    }

    [HttpPut("{id}/status")]
    [Authorize(Policy = PermissionConstants.UsersManage)]
    public async Task<IActionResult> SetStatus(int id, SetStatusRequest request)
    {
        var updated = await _userService.SetStatusAsync(id, request.IsActive, request.Reason, CurrentUserId);
        return Ok(ApiResponse<object>.Ok(updated));
    }

    [HttpPut("{id}/deactivate")]
    [Authorize(Policy = PermissionConstants.UsersManage)]
    public async Task<IActionResult> Deactivate(int id)
    {
        var updated = await _userService.SetStatusAsync(id, false, "Deactivated via legacy endpoint", CurrentUserId);
        return Ok(ApiResponse<object>.Ok(updated));
    }

    [HttpPut("{id}/email")]
    [Authorize(Policy = PermissionConstants.UsersManage)]
    public async Task<IActionResult> UpdateEmail(int id, UpdateEmailRequest request)
    {
        await _userService.UpdateEmailAsync(id, request.Email, request.Reason);
        return Ok(ApiResponse<object>.Ok(new { }));
    }

    [HttpPut("{id}/unlock")]
    [Authorize(Policy = PermissionConstants.UsersManage)]
    public async Task<IActionResult> Unlock(int id, UnlockUserRequest request)
    {
        var updated = await _userService.UnlockUserAsync(id, request.Reason, CurrentUserId);
        return Ok(ApiResponse<object>.Ok(updated));
    }

    [HttpPost("{id}/password-reset")]
    [Authorize(Policy = PermissionConstants.UsersManage)]
    public async Task<IActionResult> InitiatePasswordReset(int id, AdminResetPasswordRequest request)
    {
        await _userService.InitiatePasswordResetAsync(id, request.Reason, CurrentUserId);
        return Ok(ApiResponse<object>.Ok(new { message = "Password reset instructions sent to user's email." }));
    }

    [HttpPost("{id}/admin-password-recovery")]
    [Authorize(Policy = PermissionConstants.UsersManage)]
    public async Task<IActionResult> CreateAdminPasswordRecovery(int id, AdminPasswordRecoveryRequest request)
    {
        var result = await _adminPasswordRecoveryService.CreateRecoveryRequestAsync(id, request.Reason, CurrentUserId);
        return Ok(ApiResponse<object>.Ok(result));
    }

    [HttpPut("{id}/force-password-change")]
    [Authorize(Policy = PermissionConstants.UsersManage)]
    public async Task<IActionResult> ForcePasswordChange(int id, [FromBody] ForcePasswordChangeRequest? request)
    {
        var updated = await _userService.ForcePasswordChangeAsync(id, request?.Reason, CurrentUserId);
        return Ok(ApiResponse<object>.Ok(updated));
    }
}
