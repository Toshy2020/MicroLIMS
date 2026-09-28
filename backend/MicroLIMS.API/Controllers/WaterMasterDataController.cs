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

// Master data: water. One of the controllers that share the
// api/masterdata route; the rules and data access live in the
// Application layer (WaterMasterDataService).
[ApiController]
[Route("api/masterdata")]
[Authorize]
public class WaterMasterDataController : ControllerBase
{
    private readonly WaterMasterDataService _service;

    public WaterMasterDataController(WaterMasterDataService service)
    {
        _service = service;
    }

    // ---- Water Sampling Points ----
    [HttpGet("water-sampling-points")]
    public async Task<IActionResult> GetWaterSamplingPoints() =>
        Ok(ApiResponse<object>.Ok(await _service.GetWaterSamplingPointsAsync()));

    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpPost("water-sampling-points")]
    public async Task<IActionResult> CreateWaterSamplingPoint(CreateWaterSamplingPointRequest request) =>
        Ok(ApiResponse<object>.Ok(await _service.CreateWaterSamplingPointAsync(request)));

    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpPut("water-sampling-points/{id}")]
    public async Task<IActionResult> UpdateWaterSamplingPoint(int id, UpdateWaterSamplingPointRequest request) =>
        Ok(ApiResponse<object>.Ok(await _service.UpdateWaterSamplingPointAsync(id, request)));

    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpDelete("water-sampling-points/{id}")]
    public async Task<IActionResult> DeleteWaterSamplingPoint(int id) =>
        Ok(ApiResponse<object>.Ok(await _service.DeleteWaterSamplingPointAsync(id)));

    // ---- Water Departments ----
    [HttpGet("water-departments")]
    public async Task<IActionResult> GetWaterDepartments() =>
        Ok(ApiResponse<object>.Ok(await _service.GetWaterDepartmentsAsync()));

    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpPost("water-departments")]
    public async Task<IActionResult> CreateWaterDepartment(CreateWaterDepartmentRequest request) =>
        Ok(ApiResponse<object>.Ok(await _service.CreateWaterDepartmentAsync(request)));

    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpPut("water-departments/{id}")]
    public async Task<IActionResult> UpdateWaterDepartment(int id, UpdateWaterDepartmentRequest request) =>
        Ok(ApiResponse<object>.Ok(await _service.UpdateWaterDepartmentAsync(id, request)));

    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpDelete("water-departments/{id}")]
    public async Task<IActionResult> DeleteWaterDepartment(int id) =>
        Ok(ApiResponse<object>.Ok(await _service.DeleteWaterDepartmentAsync(id)));

    // ---- Water Sampling Configurations (per sample location x test limits) ----
    [HttpGet("water-sampling-configurations")]
    public async Task<IActionResult> GetWaterSamplingConfigurations([FromQuery] int pointId) =>
        Ok(ApiResponse<object>.Ok(await _service.GetWaterSamplingConfigurationsAsync(pointId)));

    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpPost("water-sampling-configurations")]
    public async Task<IActionResult> CreateWaterSamplingConfiguration(CreateWaterSamplingConfigRequest request) =>
        Ok(ApiResponse<object>.Ok(await _service.CreateWaterSamplingConfigurationAsync(request)));

    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpPut("water-sampling-configurations/{id}")]
    public async Task<IActionResult> UpdateWaterSamplingConfiguration(int id, UpdateWaterSamplingConfigRequest request) =>
        Ok(ApiResponse<object>.Ok(await _service.UpdateWaterSamplingConfigurationAsync(id, request)));

    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpDelete("water-sampling-configurations/{id}")]
    public async Task<IActionResult> DeleteWaterSamplingConfiguration(int id) =>
        Ok(ApiResponse<object>.Ok(await _service.DeleteWaterSamplingConfigurationAsync(id)));
}
