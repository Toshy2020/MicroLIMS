using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.DTOs.Responses;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

[ApiController]
[Route("api/items")]
[Authorize]
public class ItemController : ControllerBase
{
    private readonly ItemService _itemService;

    public ItemController(ItemService itemService)
    {
        _itemService = itemService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll() => Ok(ApiResponse<List<ItemResponse>>.Ok(await _itemService.GetAllAsync()));

    [HttpPost]
    [Authorize(Policy = PermissionConstants.ItemsManage)]
    public async Task<IActionResult> Create([FromBody] ItemSaveRequest request) => Ok(ApiResponse<ItemResponse>.Ok(await _itemService.CreateAsync(request)));

    [HttpPut("{id}")]
    [Authorize(Policy = PermissionConstants.ItemsManage)]
    public async Task<IActionResult> Update(int id, [FromBody] ItemSaveRequest request)
    {
        await _itemService.UpdateAsync(id, request);
        return Ok(ApiResponse<object>.Ok(new { }));
    }

    [HttpPut("{id}/freeze")]
    [Authorize(Policy = PermissionConstants.ItemsManage)]
    public async Task<IActionResult> Freeze(int id)
    {
        await _itemService.SetActiveAsync(id, false);
        return Ok(ApiResponse<object>.Ok(new { }));
    }

    [HttpPut("{id}/unfreeze")]
    [Authorize(Policy = PermissionConstants.ItemsManage)]
    public async Task<IActionResult> Unfreeze(int id)
    {
        await _itemService.SetActiveAsync(id, true);
        return Ok(ApiResponse<object>.Ok(new { }));
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = PermissionConstants.ItemsManage)]
    public async Task<IActionResult> Delete(int id)
    {
        await _itemService.DeleteAsync(id);
        return Ok(ApiResponse<object>.Ok(new { }));
    }
}
