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

// Master data: after cleaning. One of the controllers that share the
// api/masterdata route; the rules and data access live in the
// Application layer (AfterCleaningMasterDataService).
[ApiController]
[Route("api/masterdata")]
[Authorize]
public class AfterCleaningMasterDataController : ControllerBase
{
    private readonly AfterCleaningMasterDataService _service;

    public AfterCleaningMasterDataController(AfterCleaningMasterDataService service)
    {
        _service = service;
    }

    // ---- Machines & Parts (After Cleaning) ----
    [HttpGet("machines")]
    public async Task<IActionResult> GetMachines() =>
        Ok(ApiResponse<object>.Ok(await _service.GetMachinesAsync()));

    [Authorize(Policy = PermissionConstants.MasterDataManage)]
    [HttpPost("machines")]
    public async Task<IActionResult> CreateMachine(CreateMachineRequest request) =>
        Ok(ApiResponse<object>.Ok(await _service.CreateMachineAsync(request)));

    [Authorize(Policy = PermissionConstants.MasterDataManage)]
    [HttpPut("machines/{id}")]
    public async Task<IActionResult> UpdateMachine(int id, UpdateMachineRequest request) =>
        Ok(ApiResponse<object>.Ok(await _service.UpdateMachineAsync(id, request)));

    [Authorize(Policy = PermissionConstants.MasterDataManage)]
    [HttpDelete("machines/{id}")]
    public async Task<IActionResult> DeleteMachine(int id) =>
        Ok(ApiResponse<object>.Ok(await _service.DeleteMachineAsync(id)));

    [Authorize(Policy = PermissionConstants.MasterDataManage)]
    [HttpPost("machine-parts")]
    public async Task<IActionResult> CreateMachinePart(CreateMachinePartRequest request) =>
        Ok(ApiResponse<object>.Ok(await _service.CreateMachinePartAsync(request)));

    [Authorize(Policy = PermissionConstants.MasterDataManage)]
    [HttpPut("machine-parts/{id}")]
    public async Task<IActionResult> UpdateMachinePart(int id, UpdateMachinePartRequest request) =>
        Ok(ApiResponse<object>.Ok(await _service.UpdateMachinePartAsync(id, request)));

    [Authorize(Policy = PermissionConstants.MasterDataManage)]
    [HttpDelete("machine-parts/{id}")]
    public async Task<IActionResult> DeleteMachinePart(int id) =>
        Ok(ApiResponse<object>.Ok(await _service.DeleteMachinePartAsync(id)));

    // ---- Machine Part Test Configurations (After Cleaning) ----
    [HttpGet("machine-part-configurations")]
    public async Task<IActionResult> GetMachinePartConfigurations([FromQuery] int machinePartId) =>
        Ok(ApiResponse<object>.Ok(await _service.GetMachinePartConfigurationsAsync(machinePartId)));

    [Authorize(Policy = PermissionConstants.MasterDataManage)]
    [HttpPost("machine-part-configurations")]
    public async Task<IActionResult> CreateMachinePartConfiguration(CreateMachinePartConfigRequest request) =>
        Ok(ApiResponse<object>.Ok(await _service.CreateMachinePartConfigurationAsync(request)));

    [Authorize(Policy = PermissionConstants.MasterDataManage)]
    [HttpPut("machine-part-configurations/{id}")]
    public async Task<IActionResult> UpdateMachinePartConfiguration(int id, UpdateMachinePartConfigRequest request) =>
        Ok(ApiResponse<object>.Ok(await _service.UpdateMachinePartConfigurationAsync(id, request)));

    [Authorize(Policy = PermissionConstants.MasterDataManage)]
    [HttpDelete("machine-part-configurations/{id}")]
    public async Task<IActionResult> DeleteMachinePartConfiguration(int id) =>
        Ok(ApiResponse<object>.Ok(await _service.DeleteMachinePartConfigurationAsync(id)));
}
