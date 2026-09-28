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

// Master data: media. One of the controllers that share the
// api/masterdata route; the rules and data access live in the
// Application layer (MediaMasterDataService).
[ApiController]
[Route("api/masterdata")]
[Authorize]
public class MediaMasterDataController : ControllerBase
{
    private readonly MediaMasterDataService _service;
    private readonly MediaProductService _mediaProductService;
    private readonly MediaIncubationConditionService _mediaIncubationConditionService;

    public MediaMasterDataController(MediaMasterDataService service, MediaProductService mediaProductService, MediaIncubationConditionService mediaIncubationConditionService)
    {
        _service = service;
        _mediaProductService = mediaProductService;
        _mediaIncubationConditionService = mediaIncubationConditionService;
    }

    private int CurrentUserId => int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var uid) ? uid : 0;

    // ---- Media Products (Media Product Master) ----
    [HttpGet("media-products")]
    public async Task<IActionResult> GetMediaProducts() =>
        Ok(ApiResponse<object>.Ok(await _mediaProductService.GetAllAsync()));

    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpPost("media-products")]
    public async Task<IActionResult> CreateMediaProduct(CreateMediaProductRequest request) =>
        Ok(ApiResponse<object>.Ok(await _mediaProductService.CreateAsync(request.Name, request.Code)));

    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpPut("media-products/{id}")]
    public async Task<IActionResult> UpdateMediaProduct(int id, UpdateMediaProductRequest request) =>
        Ok(ApiResponse<object>.Ok(await _mediaProductService.RenameAsync(id, request.Name)));

    [Authorize(Roles = RoleConstants.SectionHead)]
    [HttpPut("media-products/{id}/code")]
    public async Task<IActionResult> ChangeMediaProductCode(int id, ChangeMediaProductCodeRequest request) =>
        Ok(ApiResponse<object>.Ok(await _mediaProductService.ChangeCodeAsync(
            id, request.Code, request.Reason, request.Password, CurrentUserId, HttpContext.Connection.RemoteIpAddress?.ToString())));

    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpDelete("media-products/{id}")]
    public async Task<IActionResult> DeleteMediaProduct(int id)
    {
        await _mediaProductService.DeleteAsync(id);
        return Ok(ApiResponse<object>.Ok(new { }));
    }

    // ---- Media Incubation Conditions ----
    [HttpGet("media-incubation-conditions")]
    public async Task<IActionResult> GetMediaIncubationConditions([FromQuery] int? mediaProductId) =>
        Ok(ApiResponse<object>.Ok(await _mediaIncubationConditionService.GetAllAsync(mediaProductId)));

    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpPost("media-incubation-conditions")]
    public async Task<IActionResult> CreateMediaIncubationCondition(CreateMediaIncubationConditionRequest request) =>
        Ok(ApiResponse<object>.Ok(await _mediaIncubationConditionService.CreateAsync(
            request.MediaProductId, request.IncubationMinHours, request.IncubationMaxHours, request.TemperatureMin, request.TemperatureMax)));

    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpPut("media-incubation-conditions/{id}")]
    public async Task<IActionResult> UpdateMediaIncubationCondition(int id, UpdateMediaIncubationConditionRequest request) =>
        Ok(ApiResponse<object>.Ok(await _mediaIncubationConditionService.UpdateAsync(
            id, request.IncubationMinHours, request.IncubationMaxHours, request.TemperatureMin, request.TemperatureMax)));

    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpDelete("media-incubation-conditions/{id}")]
    public async Task<IActionResult> DeleteMediaIncubationCondition(int id)
    {
        await _mediaIncubationConditionService.DeleteAsync(id);
        return Ok(ApiResponse<object>.Ok(new { }));
    }

    // ---- Media Configurations (Media Configuration Migration) ----
    [HttpGet("media-configurations")]
    public async Task<IActionResult> GetMediaConfigurations() =>
        Ok(ApiResponse<object>.Ok(await _service.GetMediaConfigurationsAsync()));

    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpPost("media-configurations")]
    public async Task<IActionResult> CreateMediaConfiguration(CreateMediaConfigurationRequest request) =>
        Ok(ApiResponse<object>.Ok(await _service.CreateMediaConfigurationAsync(request)));

    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpPut("media-configurations/{id}")]
    public async Task<IActionResult> UpdateMediaConfiguration(int id, UpdateMediaConfigurationRequest request) =>
        Ok(ApiResponse<object>.Ok(await _service.UpdateMediaConfigurationAsync(id, request)));

    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpDelete("media-configurations/{id}")]
    public async Task<IActionResult> DeleteMediaConfiguration(int id) =>
        Ok(ApiResponse<object>.Ok(await _service.DeleteMediaConfigurationAsync(id)));
}
