using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Services;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MicroLIMS.API.Controllers;

[ApiController]
[Authorize]
public class SupplierStatedAttributeController : ControllerBase
{
    private readonly SupplierStatedAttributeService _service;

    public SupplierStatedAttributeController(SupplierStatedAttributeService service)
    {
        _service = service;
    }

    private int CurrentUserId => int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var uid) ? uid : 0;

    [HttpGet("api/items/{itemId}/supplier-attributes")]
    public async Task<IActionResult> ListForItem(int itemId) =>
        Ok(ApiResponse<List<SupplierStatedAttributeDto>>.Ok(await _service.ListForItemAsync(itemId)));

    [Authorize(Policy = PermissionConstants.MasterDataManage)]
    [HttpPost("api/items/{itemId}/supplier-attributes")]
    public async Task<IActionResult> Create(int itemId, [FromBody] SupplierStatedAttributeRequest request) =>
        Ok(ApiResponse<SupplierStatedAttributeDto>.Ok(await _service.CreateAsync(CurrentUserId, request with { ItemId = itemId })));

    [Authorize(Policy = PermissionConstants.MasterDataManage)]
    [HttpPut("api/supplier-attributes/{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] SupplierStatedAttributeRequest request) =>
        Ok(ApiResponse<SupplierStatedAttributeDto>.Ok(await _service.UpdateAsync(CurrentUserId, id, request)));

    [Authorize(Policy = PermissionConstants.MasterDataManage)]
    [HttpDelete("api/supplier-attributes/{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        await _service.DeleteAsync(CurrentUserId, id);
        return Ok(ApiResponse<bool>.Ok(true));
    }
}
