using MicroLIMS.Application.DTOs.Responses;
using MicroLIMS.Shared.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

// Reason is required on every request (blank or missing is refused with 400
// by AdminActionReason). Permission edits and role deletion are also
// electronically signed with the administrator's own Password.
public record CreateRoleRequest(string Name, string? Description, RoleType BaseType, string? Reason = null);
public record UpdateRoleRequest(string Name, string? Description, string? Reason = null);
public record DeleteRoleRequest(string? Reason = null, string? Password = null);
public record UpdateRolePermissionsRequest(List<string> PermissionCodes, string? Reason = null, string? Password = null);

[ApiController]
[Route("api/roles")]
[Authorize(Policy = PermissionConstants.RolesManage)]
public class RoleController : ControllerBase
{
    private readonly RoleService _roleService;

    public RoleController(RoleService roleService)
    {
        _roleService = roleService;
    }

    private int CurrentUserId => int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

    [HttpGet]
    public async Task<IActionResult> GetAll() => Ok(ApiResponse<List<RoleResponse>>.Ok(await _roleService.GetAllAsync()));

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var role = await _roleService.GetByIdAsync(id);
        return role is null ? NotFound(ApiResponse<object>.Fail($"Role {id} not found.")) : Ok(ApiResponse<RoleDetailDto>.Ok(role));
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateRoleRequest request)
    {
        try
        {
            var created = await _roleService.CreateAsync(request.Name, request.Description, request.BaseType, request.Reason);
            return Ok(ApiResponse<RoleDetailDto>.Ok(created));
        }
        catch (SegregationOfDutiesViolationException ex)
        {
            return Conflict(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, UpdateRoleRequest request)
    {
        var updated = await _roleService.UpdateAsync(id, request.Name, request.Description, request.Reason);
        return Ok(ApiResponse<RoleDetailDto>.Ok(updated));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id, [FromBody] DeleteRoleRequest? request)
    {
        await _roleService.DeleteAsync(id, request?.Reason, CurrentUserId, request?.Password, HttpContext.Connection.RemoteIpAddress?.ToString());
        return Ok(ApiResponse<object>.Ok(new { }));
    }

    [HttpPut("{id}/permissions")]
    public async Task<IActionResult> UpdatePermissions(int id, UpdateRolePermissionsRequest request)
    {
        try
        {
            var updated = await _roleService.UpdatePermissionsAsync(id, request.PermissionCodes, CurrentUserId, request.Reason, request.Password, HttpContext.Connection.RemoteIpAddress?.ToString());
            return Ok(ApiResponse<RoleDetailDto>.Ok(updated));
        }
        catch (SegregationOfDutiesViolationException ex)
        {
            return Conflict(ApiResponse<object>.Fail(ex.Message));
        }
    }

    // The two sets the server enforces, so the permission matrix can disable a conflicting box up front.
    [HttpGet("sod-rules")]
    public IActionResult GetSodRules() => Ok(ApiResponse<SodRulesDto>.Ok(RoleService.GetSodRules()));

    [HttpGet("/api/permissions")]
    public async Task<IActionResult> GetAllPermissions() => Ok(ApiResponse<List<PermissionDto>>.Ok(await _roleService.GetAllPermissionsAsync()));
}
