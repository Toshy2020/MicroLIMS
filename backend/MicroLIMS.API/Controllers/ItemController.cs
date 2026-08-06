using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
    public async Task<IActionResult> GetAll() => Ok(ApiResponse<List<Item>>.Ok(await _itemService.GetAllAsync()));

    [HttpPost]
    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    public async Task<IActionResult> Create([FromBody] Item item) => Ok(ApiResponse<Item>.Ok(await _itemService.CreateAsync(item)));

    [HttpPut("{id}")]
    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    public async Task<IActionResult> Update(int id, [FromBody] Item item)
    {
        await _itemService.UpdateAsync(id, item);
        return Ok(ApiResponse<object>.Ok(new { }));
    }

    [HttpPut("{id}/freeze")]
    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    public async Task<IActionResult> Freeze(int id)
    {
        await _itemService.SetActiveAsync(id, false);
        return Ok(ApiResponse<object>.Ok(new { }));
    }

    [HttpPut("{id}/unfreeze")]
    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    public async Task<IActionResult> Unfreeze(int id)
    {
        await _itemService.SetActiveAsync(id, true);
        return Ok(ApiResponse<object>.Ok(new { }));
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    public async Task<IActionResult> Delete(int id)
    {
        await _itemService.DeleteAsync(id);
        return Ok(ApiResponse<object>.Ok(new { }));
    }
}
