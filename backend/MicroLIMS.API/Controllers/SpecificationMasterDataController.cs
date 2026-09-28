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

// Master data: specification. One of the controllers that share the
// api/masterdata route; the rules and data access live in the
// Application layer (SpecificationMasterDataService).
[ApiController]
[Route("api/masterdata")]
[Authorize]
public class SpecificationMasterDataController : ControllerBase
{
    private readonly SpecificationMasterDataService _service;

    public SpecificationMasterDataController(SpecificationMasterDataService service)
    {
        _service = service;
    }

    private int CurrentUserId => int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var uid) ? uid : 0;

    // ---- Specifications (Product) ----
    [HttpGet("specifications")]
    public async Task<IActionResult> GetSpecifications([FromQuery] int itemId) =>
        Ok(ApiResponse<object>.Ok(await _service.GetSpecificationsAsync(CurrentUserId, itemId)));

    [Authorize(Policy = PermissionConstants.MasterDataManage)]
    [HttpPost("specifications")]
    public async Task<IActionResult> CreateSpecification(CreateSpecificationRequest request) =>
        Ok(ApiResponse<object>.Ok(await _service.CreateSpecificationAsync(CurrentUserId, request)));

    [Authorize(Policy = PermissionConstants.MasterDataManage)]
    [HttpPut("specifications/{id}")]
    public async Task<IActionResult> UpdateSpecification(int id, UpdateSpecificationRequest request) =>
        Ok(ApiResponse<object>.Ok(await _service.UpdateSpecificationAsync(CurrentUserId, id, request)));

    [Authorize(Policy = PermissionConstants.MasterDataManage)]
    [HttpDelete("specifications/{id}")]
    public async Task<IActionResult> DeleteSpecification(int id) =>
        Ok(ApiResponse<object>.Ok(await _service.DeleteSpecificationAsync(CurrentUserId, id)));
}
