using MicroLIMS.API.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.DTOs.Responses;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.MasterData;
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
    private readonly ItemStmService _stmService;
    private readonly MasterDataCopyService _copyService;

    public ItemController(ItemService itemService, ItemStmService stmService, MasterDataCopyService copyService)
    {
        _itemService = itemService;
        _stmService = stmService;
        _copyService = copyService;
    }

    // The item's STM as resolved from the Document Library (effective revision only).
    [HttpGet("{itemId:int}/stm")]
    public async Task<IActionResult> GetStm(int itemId)
    {
        try
        {
            var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            var userId = claim != null ? int.Parse(claim.Value) : 1;
            return Ok(ApiResponse<ItemStmResponse>.Ok(await _stmService.GetForItemAsync(itemId, userId)));
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
    }

    private int CurrentUserId =>
        int.TryParse(User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var uid) ? uid : 0;
    private CancellationToken Ct => HttpContext?.RequestAborted ?? CancellationToken.None;

    [HttpGet]
    public async Task<IActionResult> GetAll() => Ok(ApiResponse<List<ItemResponse>>.Ok(await _itemService.GetAllAsync()));

    // Grades the receive dialog offers; empty when the item is ungraded.
    [HttpGet("{id:int}/grades")]
    public async Task<IActionResult> GetGrades(int id) => Ok(ApiResponse<IReadOnlyList<string>>.Ok(await _itemService.GetGradesAsync(id)));

    [HttpPost]
    [Authorize(Policy = PermissionConstants.ItemsManage)]
    public async Task<IActionResult> Create([FromBody] ItemSaveRequest request) =>
        Ok(ApiResponse<ItemResponse>.Ok(await _copyService.CreateItemAsync(request, CurrentUserId, Ct)));

    [HttpPut("{id}")]
    [Authorize(Policy = PermissionConstants.ItemsManage)]
    public async Task<IActionResult> Update(int id, [FromBody] ItemSaveRequest request)
    {
        await _itemService.UpdateAsync(id, request, ChangeReasonHeader.Read(Request), CurrentUserId, Ct);
        return Ok(ApiResponse<object>.Ok(new { }));
    }

    [HttpPut("{id}/freeze")]
    [Authorize(Policy = PermissionConstants.ItemsManage)]
    public async Task<IActionResult> Freeze(int id)
    {
        await _itemService.SetActiveAsync(id, false, ChangeReasonHeader.Read(Request), CurrentUserId, Ct);
        return Ok(ApiResponse<object>.Ok(new { }));
    }

    [HttpPut("{id}/unfreeze")]
    [Authorize(Policy = PermissionConstants.ItemsManage)]
    public async Task<IActionResult> Unfreeze(int id)
    {
        await _itemService.SetActiveAsync(id, true, ChangeReasonHeader.Read(Request), CurrentUserId, Ct);
        return Ok(ApiResponse<object>.Ok(new { }));
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = PermissionConstants.ItemsManage)]
    public async Task<IActionResult> Delete(int id)
    {
        await _itemService.DeleteAsync(id, ChangeReasonHeader.Read(Request), CurrentUserId, Ct);
        return Ok(ApiResponse<object>.Ok(new { }));
    }
}
