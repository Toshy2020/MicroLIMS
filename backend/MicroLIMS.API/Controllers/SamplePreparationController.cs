using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

// Manual entry - only reachable when the Item has no preparation
// configuration yet; these values also become that Item's standing config.
// The diluent / neutralizer text is filled from the chosen master, so only the
// source and its master id are sent; the lot ids are interpreted by that source.
public record PrepareSampleHttpRequest(
    int SampleId, decimal Amount, string Technique, decimal? FiltrationVolume, decimal? WashingVolume,
    string Password,
    PreparationMaterialSource? DiluentSource = null, int? DiluentMediaProductId = null,
    int? DiluentSolutionMasterId = null, int? DiluentMaterialMasterEntryId = null,
    PreparationMaterialSource? NeutralizerSource = null, int? NeutralizerMediaProductId = null,
    int? NeutralizerSolutionMasterId = null, int? NeutralizerMaterialMasterEntryId = null,
    int? DiluentLotId = null, int? NeutralizerLotId = null);

// Confirm-only - the Item's configured steps are the ones performed. The lot ids
// are required when the configuration names a diluent / neutralizer master.
public record ConfirmPreparationHttpRequest(int SampleId, string Password, int? DiluentLotId = null, int? NeutralizerLotId = null);

// Grouped confirm - the same confirm-only step applied to every sample in
// one item-configuration group, signed once. One lot pair for the whole group.
public record BatchConfirmPreparationHttpRequest(
    List<int> SampleIds, int ConfigurationId, string Password, int? DiluentLotId = null, int? NeutralizerLotId = null);

// Test Preparation step - Product/RM/PM, once per Sample, must
// complete before results can be entered.
[ApiController]
[Route("api/sample-preparation")]
[Authorize]
public class SamplePreparationController : ControllerBase
{
    private readonly SamplePreparationService _service;
    private readonly IUserSectionScopeService _scopeService;
    private readonly PreparationMaterialService _materials;

    public SamplePreparationController(
        SamplePreparationService service, IUserSectionScopeService scopeService, PreparationMaterialService materials)
    {
        _service = service;
        _scopeService = scopeService;
        _materials = materials;
    }

    private int CurrentUserId => int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
    private string? ClientIp => HttpContext.Connection.RemoteIpAddress?.ToString();

    [HttpPost]
    [Authorize(Policy = PermissionConstants.SamplesPrepare)]
    public async Task<IActionResult> Prepare(PrepareSampleHttpRequest r)
    {
        await _scopeService.EnsureSampleAccessAsync(CurrentUserId, r.SampleId);
        var prep = await _service.PrepareAsync(new PrepareSampleRequest(
            r.SampleId, r.Amount, r.Technique, r.FiltrationVolume, r.WashingVolume,
            string.Empty, string.Empty, CurrentUserId, r.Password,
            new PreparationMaterialChoice(r.DiluentSource, r.DiluentMediaProductId, r.DiluentSolutionMasterId, r.DiluentMaterialMasterEntryId),
            new PreparationMaterialChoice(r.NeutralizerSource, r.NeutralizerMediaProductId, r.NeutralizerSolutionMasterId, r.NeutralizerMaterialMasterEntryId),
            r.DiluentLotId, r.NeutralizerLotId), ClientIp);

        return Ok(ApiResponse<object>.Ok(prep));
    }

    [HttpPost("confirm")]
    [Authorize(Policy = PermissionConstants.SamplesPrepare)]
    public async Task<IActionResult> Confirm(ConfirmPreparationHttpRequest r)
    {
        await _scopeService.EnsureSampleAccessAsync(CurrentUserId, r.SampleId);
        var prep = await _service.ConfirmFromConfigurationAsync(
            new ConfirmPreparationRequest(
                r.SampleId, CurrentUserId, r.Password, DiluentLotId: r.DiluentLotId, NeutralizerLotId: r.NeutralizerLotId), ClientIp);

        return Ok(ApiResponse<object>.Ok(prep));
    }

    // Grouped preparation - which of the checked samples can be prepared
    // together, bucketed by the item configuration each would confirm, plus
    // the ones that cannot with the reason why.
    [HttpGet("groups")]
    public async Task<IActionResult> GetGroups([FromQuery] string? sampleIds = null, CancellationToken ct = default)
    {
        var ids = (sampleIds ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => int.TryParse(s.Trim(), out var id) ? id : (int?)null)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToList();

        foreach (var id in ids)
            await _scopeService.EnsureSampleAccessAsync(CurrentUserId, id, ct, access: SectionAccess.Read);

        var result = await _service.GetGroupedPreparationAsync(ids, CurrentUserId, ct);
        return Ok(ApiResponse<GroupedPreparationResponse>.Ok(result));
    }

    [HttpPost("batch-confirm")]
    [Authorize(Policy = PermissionConstants.SamplesPrepare)]
    public async Task<IActionResult> BatchConfirm(BatchConfirmPreparationHttpRequest r, CancellationToken ct = default)
    {
        if (r.SampleIds != null && r.SampleIds.Count > 0)
        {
            foreach (var sampleId in r.SampleIds)
            {
                await _scopeService.EnsureSampleAccessAsync(CurrentUserId, sampleId, ct);
            }
        }

        var result = await _service.ConfirmBatchFromConfigurationAsync(
            new BatchConfirmPreparationRequest(r.SampleIds ?? [], r.ConfigurationId, r.Password, r.DiluentLotId, r.NeutralizerLotId),
            CurrentUserId, ClientIp, ct);

        return Ok(ApiResponse<BatchConfirmPreparationResponse>.Ok(result));
    }

    // The lots the analyst may pick for the diluent and neutralizer of the sample's Item
    // (sampleId), or of an item configuration (configurationId, the grouped path).
    [HttpGet("lot-options")]
    public async Task<IActionResult> GetLotOptions(
        [FromQuery] int? sampleId = null, [FromQuery] int? configurationId = null, CancellationToken ct = default)
    {
        if (sampleId.HasValue == configurationId.HasValue)
            throw new InvalidOperationException("Give exactly one of sampleId or configurationId.");

        if (sampleId.HasValue)
        {
            await _scopeService.EnsureSampleAccessAsync(CurrentUserId, sampleId.Value, ct, access: SectionAccess.Read);
            return Ok(ApiResponse<PreparationLotOptionsDto>.Ok(await _service.GetLotOptionsForSampleAsync(sampleId.Value, CurrentUserId, ct)));
        }

        return Ok(ApiResponse<PreparationLotOptionsDto>.Ok(await _service.GetLotOptionsForConfigurationAsync(configurationId!.Value, CurrentUserId, ct)));
    }

    // Manual first entry has no configuration yet: the lots of the master the analyst just chose.
    [HttpGet("lot-options/master")]
    public async Task<IActionResult> GetLotOptionsForMaster(
        [FromQuery] PreparationMaterialSource source, [FromQuery] int masterId, CancellationToken ct = default) =>
        Ok(ApiResponse<PreparationLotGroupDto>.Ok(await _materials.GetLotGroupForMasterAsync(source, masterId, CurrentUserId, ct)));

    // Every sample whose diluent or neutralizer was this lot (exactly one id), limited to the samples the user can read.
    [HttpGet("where-used")]
    public async Task<IActionResult> WhereUsed(
        [FromQuery] int? mediaId = null, [FromQuery] int? solutionPreparationId = null, [FromQuery] int? materialId = null,
        CancellationToken ct = default) =>
        Ok(ApiResponse<List<PreparationWhereUsedDto>>.Ok(
            await _materials.GetWhereUsedAsync(mediaId, solutionPreparationId, materialId, CurrentUserId, ct)));

    [HttpGet("{sampleId}/is-prepared")]
    public async Task<IActionResult> IsPrepared(int sampleId)
    {
        await _scopeService.EnsureSampleAccessAsync(CurrentUserId, sampleId, access: SectionAccess.Read);
        return Ok(ApiResponse<object>.Ok(new { prepared = await _service.IsPreparedAsync(sampleId) }));
    }
}
