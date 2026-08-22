using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

public record CreateUserRequest(string FullName, string Username, string Password, int RoleId, string? Email = null, string? JobTitle = null);
public record UpdateProfileRequest(string FullName, string Username, string? Email, string? JobTitle = null);
public record ChangeRoleRequest(int RoleId, string Reason);
public record SetStatusRequest(bool IsActive, string? Reason = null);
public record UnlockUserRequest(string? Reason = null);
public record AdminResetPasswordRequest(string? Reason = null);
public record UpdateEmailRequest(string? Email);
public record AdminPasswordRecoveryRequest(string Reason);

[ApiController]
[Route("api/users")]
[Authorize(Roles = RoleConstants.SystemAdministrator)]
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

    [HttpGet]
    public async Task<IActionResult> GetAll() => Ok(ApiResponse<object>.Ok(await _userService.GetAllAsync()));

    [HttpGet("{id}")]
    [Authorize(Roles = RoleConstants.SystemAdministrator)]
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
    [Authorize(Roles = RoleConstants.SystemAdministrator)]
    public async Task<IActionResult> Create(CreateUserRequest request)
    {
        try
        {
            var user = new User { FullName = request.FullName, Username = request.Username, RoleId = request.RoleId, Email = request.Email, JobTitle = request.JobTitle };
            var created = await _userService.CreateAsync(user, request.Password);
            return Ok(ApiResponse<object>.Ok(created));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpPut("{id}")]
    [Authorize(Roles = RoleConstants.SystemAdministrator)]
    public async Task<IActionResult> UpdateProfile(int id, UpdateProfileRequest request)
    {
        try
        {
            var updated = await _userService.UpdateProfileAsync(id, request.FullName, request.Username, request.Email, request.JobTitle, CurrentUserId);
            return Ok(ApiResponse<object>.Ok(updated));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpPut("{id}/role")]
    [Authorize(Roles = RoleConstants.SystemAdministrator)]
    public async Task<IActionResult> ChangeRole(int id, ChangeRoleRequest request)
    {
        try
        {
            var updated = await _userService.ChangeRoleAsync(id, request.RoleId, request.Reason, CurrentUserId);
            return Ok(ApiResponse<object>.Ok(updated));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpPut("{id}/status")]
    [Authorize(Roles = RoleConstants.SystemAdministrator)]
    public async Task<IActionResult> SetStatus(int id, SetStatusRequest request)
    {
        try
        {
            var updated = await _userService.SetStatusAsync(id, request.IsActive, request.Reason, CurrentUserId);
            return Ok(ApiResponse<object>.Ok(updated));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpPut("{id}/deactivate")]
    [Authorize(Roles = RoleConstants.SystemAdministrator)]
    public async Task<IActionResult> Deactivate(int id)
    {
        try
        {
            var updated = await _userService.SetStatusAsync(id, false, "Deactivated via legacy endpoint", CurrentUserId);
            return Ok(ApiResponse<object>.Ok(updated));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpPut("{id}/email")]
    [Authorize(Roles = RoleConstants.SystemAdministrator)]
    public async Task<IActionResult> UpdateEmail(int id, UpdateEmailRequest request)
    {
        try
        {
            await _userService.UpdateEmailAsync(id, request.Email);
            return Ok(ApiResponse<object>.Ok(new { }));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpPut("{id}/unlock")]
    [Authorize(Roles = RoleConstants.SystemAdministrator)]
    public async Task<IActionResult> Unlock(int id, UnlockUserRequest request)
    {
        try
        {
            var updated = await _userService.UnlockUserAsync(id, request.Reason, CurrentUserId);
            return Ok(ApiResponse<object>.Ok(updated));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpPost("{id}/password-reset")]
    [Authorize(Roles = RoleConstants.SystemAdministrator)]
    public async Task<IActionResult> InitiatePasswordReset(int id, AdminResetPasswordRequest request)
    {
        try
        {
            await _userService.InitiatePasswordResetAsync(id, request.Reason, CurrentUserId);
            return Ok(ApiResponse<object>.Ok(new { message = "Password reset instructions sent to user's email." }));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpPost("{id}/admin-password-recovery")]
    [Authorize(Roles = RoleConstants.SystemAdministrator)]
    public async Task<IActionResult> CreateAdminPasswordRecovery(int id, AdminPasswordRecoveryRequest request)
    {
        try
        {
            var result = await _adminPasswordRecoveryService.CreateRecoveryRequestAsync(id, request.Reason, CurrentUserId);
            return Ok(ApiResponse<object>.Ok(result));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpPut("{id}/force-password-change")]
    [Authorize(Roles = RoleConstants.SystemAdministrator)]
    public async Task<IActionResult> ForcePasswordChange(int id)
    {
        try
        {
            var updated = await _userService.ForcePasswordChangeAsync(id, CurrentUserId);
            return Ok(ApiResponse<object>.Ok(updated));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }
}
