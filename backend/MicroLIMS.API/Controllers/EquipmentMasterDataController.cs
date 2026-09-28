using MicroLIMS.Shared.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Responses;
using MicroLIMS.Application.Services.MasterData;

namespace MicroLIMS.API.Controllers;

// Master data: equipment. One of the controllers that share the
// api/masterdata route; the rules and data access live in the
// Application layer (EquipmentMasterDataService).
[ApiController]
[Route("api/masterdata")]
[Authorize]
public class EquipmentMasterDataController : ControllerBase
{
    private readonly EquipmentMasterDataService _service;
    private readonly EquipmentConfigurationService _configService;
    private readonly IUserSectionScopeService _scope;
    private readonly ChromatographyColumnService _columnService;

    public EquipmentMasterDataController(EquipmentMasterDataService service, EquipmentConfigurationService configService, IUserSectionScopeService scope, ChromatographyColumnService columnService)
    {
        _service = service;
        _configService = configService;
        _scope = scope;
        _columnService = columnService;
    }

    private int CurrentUserId => int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var uid) ? uid : 0;

    // ---- Equipment ----
    [HttpGet("equipment")]
    public async Task<IActionResult> GetEquipment([FromQuery] EquipmentType? type) =>
        Ok(ApiResponse<object>.Ok(await _service.GetEquipmentAsync(CurrentUserId, type)));

    [HttpGet("equipment/{id:int}")]
    public async Task<IActionResult> GetEquipmentById(int id) =>
        Ok(ApiResponse<object>.Ok(await _service.GetEquipmentByIdAsync(CurrentUserId, id)));

    [HttpGet("equipment/configured-summary")]
    public async Task<IActionResult> GetConfiguredSummary() =>
        Ok(ApiResponse<object>.Ok(await _configService.GetConfiguredEquipmentSummaryAsync()));

    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpPost("equipment/link-inventory/{inventoryId:int}")]
    public async Task<IActionResult> LinkInventory(int inventoryId)
    {
        var master = await _configService.LinkInventoryEquipmentToMasterAsync(inventoryId, CurrentUserId);
        return Ok(ApiResponse<object>.Ok(master));
    }

    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpPut("equipment/{id:int}/set-point")]
    public async Task<IActionResult> UpdateSetPoint(int id, [FromBody] UpdateIncubatorSetPointRequest request)
    {
        await _scope.EnsureEquipmentAccessAsync(CurrentUserId, id);
        try
        {
            var updated = await _configService.UpdateIncubatorSetPointAsync(id, request, CurrentUserId);
            return Ok(ApiResponse<object>.Ok(updated));
        }
        catch (InvalidOperationException ex) when (ex is not NotFoundException)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpGet("equipment/{id:int}/set-point-history")]
    public async Task<IActionResult> GetSetPointHistory(int id)
    {
        await _scope.EnsureEquipmentAccessAsync(CurrentUserId, id);
        return Ok(ApiResponse<object>.Ok(await _configService.GetIncubatorSetPointHistoryAsync(id)));
    }

    [HttpGet("equipment/{id:int}/autoclave-programs")]
    public async Task<IActionResult> GetAutoclavePrograms(int id, [FromQuery] bool? activeOnly)
    {
        await _scope.EnsureEquipmentAccessAsync(CurrentUserId, id);
        return Ok(ApiResponse<object>.Ok(await _configService.GetAutoclaveProgramsAsync(id, activeOnly)));
    }

    [HttpGet("equipment/autoclave-programs/all")]
    public async Task<IActionResult> GetAllAutoclavePrograms([FromQuery] bool? activeOnly) =>
        Ok(ApiResponse<object>.Ok(await _configService.GetAutoclaveProgramsAsync(null, activeOnly)));

    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpPost("equipment/{id:int}/autoclave-programs")]
    public async Task<IActionResult> SaveAutoclaveProgram(int id, [FromBody] SaveAutoclaveProgramRequest request)
    {
        await _scope.EnsureEquipmentAccessAsync(CurrentUserId, id);
        try
        {
            var req = request with { EquipmentId = id };
            var saved = await _configService.SaveAutoclaveProgramAsync(req, CurrentUserId);
            return Ok(ApiResponse<object>.Ok(saved));
        }
        catch (InvalidOperationException ex) when (ex is not NotFoundException)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpPut("equipment/autoclave-programs/{programId:int}")]
    public async Task<IActionResult> UpdateAutoclaveProgram(int programId, [FromBody] SaveAutoclaveProgramRequest request)
    {
        try
        {
            var req = request with { Id = programId };
            var saved = await _configService.SaveAutoclaveProgramAsync(req, CurrentUserId);
            return Ok(ApiResponse<object>.Ok(saved));
        }
        catch (InvalidOperationException ex) when (ex is not NotFoundException)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpPut("equipment/autoclave-programs/{programId:int}/status")]
    public async Task<IActionResult> SetAutoclaveProgramStatus(int programId, [FromBody] SetAutoclaveProgramStatusHttpRequest request)
    {
        try
        {
            await _configService.SetAutoclaveProgramStatusAsync(programId, request.IsActive, request.Comment ?? "", CurrentUserId);
            return Ok(ApiResponse<object>.Ok(new { }));
        }
        catch (InvalidOperationException ex) when (ex is not NotFoundException)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpGet("equipment/autoclave-programs/{programId:int}/history")]
    public async Task<IActionResult> GetAutoclaveProgramHistory(int programId) =>
        Ok(ApiResponse<object>.Ok(await _configService.GetAutoclaveProgramHistoryAsync(programId)));

    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpPost("equipment")]
    public async Task<IActionResult> CreateEquipment(CreateEquipmentRequest request) =>
        Ok(ApiResponse<object>.Ok(await _service.CreateEquipmentAsync(CurrentUserId, request)));

    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpPut("equipment/{id:int}")]
    public async Task<IActionResult> UpdateEquipment(int id, UpdateEquipmentRequest request) =>
        Ok(ApiResponse<object>.Ok(await _service.UpdateEquipmentAsync(CurrentUserId, id, request)));

    // ---- Column Master (REQ-FP-011) ----
    [HttpGet("columns")]
    public async Task<IActionResult> GetColumns([FromQuery] bool? activeOnly) =>
        Ok(ApiResponse<object>.Ok(await _columnService.GetAllAsync(CurrentUserId, activeOnly)));

    [HttpGet("columns/{id:int}")]
    public async Task<IActionResult> GetColumnById(int id) =>
        Ok(ApiResponse<object>.Ok(await _columnService.GetByIdAsync(id, CurrentUserId)));

    [Authorize(Policy = PermissionConstants.EquipmentManage)]
    [HttpPost("columns")]
    public async Task<IActionResult> CreateColumn([FromBody] CreateChromatographyColumnRequest request) =>
        Ok(ApiResponse<object>.Ok(await _columnService.CreateAsync(request, CurrentUserId)));

    [Authorize(Policy = PermissionConstants.EquipmentManage)]
    [HttpPut("columns/{id:int}")]
    public async Task<IActionResult> UpdateColumn(int id, [FromBody] UpdateChromatographyColumnRequest request) =>
        Ok(ApiResponse<object>.Ok(await _columnService.UpdateAsync(id, request, CurrentUserId)));

    [Authorize(Policy = PermissionConstants.EquipmentManage)]
    [HttpPut("columns/{id:int}/deactivate")]
    public async Task<IActionResult> DeactivateColumn(int id) =>
        Ok(ApiResponse<object>.Ok(await _columnService.DeactivateAsync(id, CurrentUserId)));
}
