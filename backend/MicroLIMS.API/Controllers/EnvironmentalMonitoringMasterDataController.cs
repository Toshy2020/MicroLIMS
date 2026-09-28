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

// Master data: environmental monitoring. One of the controllers that share the
// api/masterdata route; the rules and data access live in the
// Application layer (EnvironmentalMonitoringMasterDataService).
[ApiController]
[Route("api/masterdata")]
[Authorize]
public class EnvironmentalMonitoringMasterDataController : ControllerBase
{
    private readonly EnvironmentalMonitoringMasterDataService _service;

    public EnvironmentalMonitoringMasterDataController(EnvironmentalMonitoringMasterDataService service)
    {
        _service = service;
    }

    // ---- Departments & Rooms (EM) ----
    [HttpGet("departments")]
    public async Task<IActionResult> GetDepartments() =>
        Ok(ApiResponse<object>.Ok(await _service.GetDepartmentsAsync()));

    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpPost("departments")]
    public async Task<IActionResult> CreateDepartment(CreateDepartmentRequest request) =>
        Ok(ApiResponse<object>.Ok(await _service.CreateDepartmentAsync(request)));

    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpPut("departments/{id}")]
    public async Task<IActionResult> UpdateDepartment(int id, UpdateDepartmentRequest request) =>
        Ok(ApiResponse<object>.Ok(await _service.UpdateDepartmentAsync(id, request)));

    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpDelete("departments/{id}")]
    public async Task<IActionResult> DeleteDepartment(int id) =>
        Ok(ApiResponse<object>.Ok(await _service.DeleteDepartmentAsync(id)));

    [HttpGet("rooms")]
    public async Task<IActionResult> GetRooms() =>
        Ok(ApiResponse<object>.Ok(await _service.GetRoomsAsync()));

    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpPost("rooms")]
    public async Task<IActionResult> CreateRoom(CreateRoomRequest request) =>
        Ok(ApiResponse<object>.Ok(await _service.CreateRoomAsync(request)));

    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpPut("rooms/{id}")]
    public async Task<IActionResult> UpdateRoom(int id, UpdateRoomRequest request) =>
        Ok(ApiResponse<object>.Ok(await _service.UpdateRoomAsync(id, request)));

    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpDelete("rooms/{id}")]
    public async Task<IActionResult> DeleteRoom(int id) =>
        Ok(ApiResponse<object>.Ok(await _service.DeleteRoomAsync(id)));

    // ---- Room Test Configurations (EM) ----
    [HttpGet("room-test-configurations")]
    public async Task<IActionResult> GetRoomTestConfigurations([FromQuery] int roomId) =>
        Ok(ApiResponse<object>.Ok(await _service.GetRoomTestConfigurationsAsync(roomId)));

    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpPost("room-test-configurations")]
    public async Task<IActionResult> CreateRoomTestConfiguration(CreateRoomTestConfigRequest request) =>
        Ok(ApiResponse<object>.Ok(await _service.CreateRoomTestConfigurationAsync(request)));

    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpPut("room-test-configurations/{id}")]
    public async Task<IActionResult> UpdateRoomTestConfiguration(int id, UpdateRoomTestConfigRequest request) =>
        Ok(ApiResponse<object>.Ok(await _service.UpdateRoomTestConfigurationAsync(id, request)));

    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpDelete("room-test-configurations/{id}")]
    public async Task<IActionResult> DeleteRoomTestConfiguration(int id) =>
        Ok(ApiResponse<object>.Ok(await _service.DeleteRoomTestConfigurationAsync(id)));
}
