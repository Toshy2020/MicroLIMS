using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

// The diluent and neutralizer are each a source plus the one master of that source
// (ContainedInMedium, neutralizer only, takes none). The server fills the Diluent /
// Neutralizer display text from the master, so the client sends none.
public record UpsertPreparationConfigurationRequest(
    decimal Amount, string Technique, decimal? FiltrationVolume, decimal? WashingVolume,
    PreparationMaterialSource? DiluentSource = null, int? DiluentMediaProductId = null,
    int? DiluentSolutionMasterId = null, int? DiluentMaterialMasterEntryId = null,
    PreparationMaterialSource? NeutralizerSource = null, int? NeutralizerMediaProductId = null,
    int? NeutralizerSolutionMasterId = null, int? NeutralizerMaterialMasterEntryId = null);

// Per-item preparation protocol (Laboratory Configuration -> Items).
// Readable by anyone who can prepare a sample - the analyst's confirm
// dialogue renders from it; writes are Section Head territory.
[ApiController]
[Route("api/items/{itemId:int}/preparation-configuration")]
[Authorize]
public class ItemPreparationConfigurationController : ControllerBase
{
    private readonly ItemPreparationConfigurationService _service;

    public ItemPreparationConfigurationController(ItemPreparationConfigurationService service)
    {
        _service = service;
    }

    private int CurrentUserId => int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

    [HttpGet]
    public async Task<IActionResult> Get(int itemId) =>
        Ok(ApiResponse<ItemPreparationConfigurationDto?>.Ok(await _service.GetByItemIdAsync(itemId)));

    [HttpPut]
    [Authorize(Policy = PermissionConstants.ItemsManage)]
    public async Task<IActionResult> Upsert(int itemId, UpsertPreparationConfigurationRequest r)
    {
        var dto = await _service.UpsertAsync(itemId, new PreparationParameters(
            r.Amount, r.Technique, r.FiltrationVolume, r.WashingVolume,
            string.Empty, string.Empty,
            new PreparationMaterialChoice(r.DiluentSource, r.DiluentMediaProductId, r.DiluentSolutionMasterId, r.DiluentMaterialMasterEntryId),
            new PreparationMaterialChoice(r.NeutralizerSource, r.NeutralizerMediaProductId, r.NeutralizerSolutionMasterId, r.NeutralizerMaterialMasterEntryId)),
            CurrentUserId);

        return Ok(ApiResponse<ItemPreparationConfigurationDto>.Ok(dto));
    }

    [HttpPost("approve")]
    [Authorize(Policy = PermissionConstants.ItemsManage)]
    public async Task<IActionResult> Approve(int itemId) =>
        Ok(ApiResponse<ItemPreparationConfigurationDto>.Ok(await _service.ApproveAsync(itemId, CurrentUserId)));
}

// Cross-item queue for the Section Head's pending-approval tile.
[ApiController]
[Route("api/preparation-configurations")]
[Authorize(Policy = PermissionConstants.ItemsManage)]
public class PreparationConfigurationQueueController : ControllerBase
{
    private readonly ItemPreparationConfigurationService _service;

    public PreparationConfigurationQueueController(ItemPreparationConfigurationService service)
    {
        _service = service;
    }

    [HttpGet("pending")]
    public async Task<IActionResult> Pending() =>
        Ok(ApiResponse<List<ItemPreparationConfigurationDto>>.Ok(await _service.GetPendingApprovalAsync()));

    // Configurations whose diluent or neutralizer is still legacy text, for a Section Head to map.
    [HttpGet("unmapped")]
    public async Task<IActionResult> Unmapped() =>
        Ok(ApiResponse<List<ItemPreparationConfigurationDto>>.Ok(await _service.GetUnmappedAsync()));
}
