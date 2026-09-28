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

// Master data: reference list. One of the controllers that share the
// api/masterdata route; the rules and data access live in the
// Application layer (ReferenceListMasterDataService).
[ApiController]
[Route("api/masterdata")]
[Authorize]
public class ReferenceListMasterDataController : ControllerBase
{
    private readonly ReferenceListMasterDataService _service;

    public ReferenceListMasterDataController(ReferenceListMasterDataService service)
    {
        _service = service;
    }

    // ---- Cause of Testing ----
    [HttpGet("causes-of-testing")]
    public async Task<IActionResult> GetCausesOfTesting() =>
        Ok(ApiResponse<object>.Ok(await _service.GetCausesOfTestingAsync()));

    [Authorize(Policy = PermissionConstants.MasterDataManage)]
    [HttpPost("causes-of-testing")]
    public async Task<IActionResult> CreateCauseOfTesting([FromBody] string name) =>
        Ok(ApiResponse<object>.Ok(await _service.CreateCauseOfTestingAsync(name)));

    [Authorize(Policy = PermissionConstants.MasterDataManage)]
    [HttpPut("causes-of-testing/{id}")]
    public async Task<IActionResult> UpdateCauseOfTesting(int id, [FromBody] string name) =>
        Ok(ApiResponse<object>.Ok(await _service.UpdateCauseOfTestingAsync(id, name)));

    [Authorize(Policy = PermissionConstants.MasterDataManage)]
    [HttpDelete("causes-of-testing/{id}")]
    public async Task<IActionResult> DeleteCauseOfTesting(int id) =>
        Ok(ApiResponse<object>.Ok(await _service.DeleteCauseOfTestingAsync(id)));

    // ---- Samplers (suggestion list for the free-text "Sampled By" field on
    // Sample - not an FK, so no delete guard needed: removing a name here
    // never touches historical Sample rows, which store the string directly) ----
    [HttpGet("samplers")]
    public async Task<IActionResult> GetSamplers() =>
        Ok(ApiResponse<object>.Ok(await _service.GetSamplersAsync()));

    [Authorize(Policy = PermissionConstants.MasterDataManage)]
    [HttpPost("samplers")]
    public async Task<IActionResult> CreateSampler([FromBody] string name) =>
        Ok(ApiResponse<object>.Ok(await _service.CreateSamplerAsync(name)));

    [Authorize(Policy = PermissionConstants.MasterDataManage)]
    [HttpPut("samplers/{id}")]
    public async Task<IActionResult> UpdateSampler(int id, [FromBody] string name) =>
        Ok(ApiResponse<object>.Ok(await _service.UpdateSamplerAsync(id, name)));

    [Authorize(Policy = PermissionConstants.MasterDataManage)]
    [HttpDelete("samplers/{id}")]
    public async Task<IActionResult> DeleteSampler(int id) =>
        Ok(ApiResponse<object>.Ok(await _service.DeleteSamplerAsync(id)));

    // ---- Production Stages (fixed-choice list for the Finished Product
    // receiving form's Production Stage field - not an FK, same rationale
    // as Samplers above) ----
    [HttpGet("production-stages")]
    public async Task<IActionResult> GetProductionStages() =>
        Ok(ApiResponse<object>.Ok(await _service.GetProductionStagesAsync()));

    [Authorize(Policy = PermissionConstants.MasterDataManage)]
    [HttpPost("production-stages")]
    public async Task<IActionResult> CreateProductionStage(CreateProductionStageRequest request) =>
        Ok(ApiResponse<object>.Ok(await _service.CreateProductionStageAsync(request)));

    [Authorize(Policy = PermissionConstants.MasterDataManage)]
    [HttpPut("production-stages/{id}")]
    public async Task<IActionResult> UpdateProductionStage(int id, UpdateProductionStageRequest request) =>
        Ok(ApiResponse<object>.Ok(await _service.UpdateProductionStageAsync(id, request)));

    [Authorize(Policy = PermissionConstants.MasterDataManage)]
    [HttpDelete("production-stages/{id}")]
    public async Task<IActionResult> DeleteProductionStage(int id) =>
        Ok(ApiResponse<object>.Ok(await _service.DeleteProductionStageAsync(id)));

    // ---- Diluent Types ----
    [HttpGet("diluent-types")]
    public async Task<IActionResult> GetDiluentTypes() =>
        Ok(ApiResponse<object>.Ok(await _service.GetDiluentTypesAsync()));

    [Authorize(Policy = PermissionConstants.MasterDataManage)]
    [HttpPost("diluent-types")]
    public async Task<IActionResult> CreateDiluentType(CreateDiluentTypeRequest request) =>
        Ok(ApiResponse<object>.Ok(await _service.CreateDiluentTypeAsync(request)));

    // ---- Neutralizers ----
    [HttpGet("neutralizers")]
    public async Task<IActionResult> GetNeutralizers() =>
        Ok(ApiResponse<object>.Ok(await _service.GetNeutralizersAsync()));

    [Authorize(Policy = PermissionConstants.MasterDataManage)]
    [HttpPost("neutralizers")]
    public async Task<IActionResult> CreateNeutralizer([FromBody] string name) =>
        Ok(ApiResponse<object>.Ok(await _service.CreateNeutralizerAsync(name)));
}
