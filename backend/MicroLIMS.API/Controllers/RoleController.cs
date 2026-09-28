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

public record CreateRoleRequest(string Name, string? Description, RoleType BaseType);
public record UpdateRoleRequest(string Name, string? Description);
public record UpdateRolePermissionsRequest(List<string> PermissionCodes);

[ApiController]
[Route("api/roles")]
[Authorize(Roles = RoleConstants.SystemAdministrator)]
public class RoleController : ControllerBase
{
    private readonly RoleService _roleService;

    public RoleController(RoleService roleService)
    {
        _roleService = roleService;
    }

    private int CurrentUserId => int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

    [HttpGet]
    public async Task<IActionResult> GetAll() => Ok(ApiResponse<List<Role>>.Ok(await _roleService.GetAllAsync()));

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
            var created = await _roleService.CreateAsync(request.Name, request.Description, request.BaseType);
            return Ok(ApiResponse<RoleDetailDto>.Ok(created));
        }
        catch (InvalidOperationException ex) when (ex is not NotFoundException)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, UpdateRoleRequest request)
    {
        try
        {
            var updated = await _roleService.UpdateAsync(id, request.Name, request.Description);
            return Ok(ApiResponse<RoleDetailDto>.Ok(updated));
        }
        catch (InvalidOperationException ex) when (ex is not NotFoundException)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            await _roleService.DeleteAsync(id);
            return Ok(ApiResponse<object>.Ok(new { }));
        }
        catch (InvalidOperationException ex) when (ex is not NotFoundException)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpPut("{id}/permissions")]
    public async Task<IActionResult> UpdatePermissions(int id, UpdateRolePermissionsRequest request)
    {
        try
        {
            var updated = await _roleService.UpdatePermissionsAsync(id, request.PermissionCodes, CurrentUserId);
            return Ok(ApiResponse<RoleDetailDto>.Ok(updated));
        }
        catch (InvalidOperationException ex) when (ex is not NotFoundException)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpGet("/api/permissions")]
    public async Task<IActionResult> GetAllPermissions() => Ok(ApiResponse<List<PermissionDto>>.Ok(await _roleService.GetAllPermissionsAsync()));
}
