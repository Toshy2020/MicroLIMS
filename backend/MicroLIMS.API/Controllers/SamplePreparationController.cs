using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Services;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

// Manual entry - only reachable when the Item has no preparation
// configuration yet; these values also become that Item's standing config.
public record PrepareSampleHttpRequest(
    int SampleId, decimal Amount, string Technique, decimal? FiltrationVolume, decimal? WashingVolume,
    string Diluent, string Neutralizer, string Password);

// Confirm-only - the Item's configured steps are the ones performed.
public record ConfirmPreparationHttpRequest(int SampleId, string Password);

// Grouped confirm - the same confirm-only step applied to every sample in
// one item-configuration group, signed once.
public record BatchConfirmPreparationHttpRequest(List<int> SampleIds, int ConfigurationId, string Password);

// Test Preparation step - Product/RM/PM, once per Sample, must
// complete before results can be entered.
[ApiController]
[Route("api/sample-preparation")]
[Authorize]
public class SamplePreparationController : ControllerBase
{
    private readonly SamplePreparationService _service;

    public SamplePreparationController(SamplePreparationService service)
    {
        _service = service;
    }

    private int CurrentUserId => int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
    private string? ClientIp => HttpContext.Connection.RemoteIpAddress?.ToString();

    [HttpPost]
    public async Task<IActionResult> Prepare(PrepareSampleHttpRequest r)
    {
        var prep = await _service.PrepareAsync(new PrepareSampleRequest(
            r.SampleId, r.Amount, r.Technique, r.FiltrationVolume, r.WashingVolume,
            r.Diluent, r.Neutralizer, CurrentUserId, r.Password), ClientIp);

        return Ok(ApiResponse<object>.Ok(Project(prep)));
    }

    [HttpPost("confirm")]
    public async Task<IActionResult> Confirm(ConfirmPreparationHttpRequest r)
    {
        var prep = await _service.ConfirmFromConfigurationAsync(
            new ConfirmPreparationRequest(r.SampleId, CurrentUserId, r.Password), ClientIp);

        return Ok(ApiResponse<object>.Ok(Project(prep)));
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

        var result = await _service.GetGroupedPreparationAsync(ids, CurrentUserId, ct);
        return Ok(ApiResponse<GroupedPreparationResponse>.Ok(result));
    }

    [HttpPost("batch-confirm")]
    public async Task<IActionResult> BatchConfirm(BatchConfirmPreparationHttpRequest r, CancellationToken ct = default)
    {
        var result = await _service.ConfirmBatchFromConfigurationAsync(
            new BatchConfirmPreparationRequest(r.SampleIds, r.ConfigurationId, r.Password),
            CurrentUserId, ClientIp, ct);

        return Ok(ApiResponse<BatchConfirmPreparationResponse>.Ok(result));
    }

    [HttpGet("{sampleId}/is-prepared")]
    public async Task<IActionResult> IsPrepared(int sampleId) =>
        Ok(ApiResponse<object>.Ok(new { prepared = await _service.IsPreparedAsync(sampleId) }));

    // Avoid the SamplePreparation <-> Sample <-> TestOrders navigation
    // cycle when serializing (auto-assign loads the sample's TestOrders
    // into the same tracked context).
    private static object Project(Domain.Entities.SamplePreparation prep) => new
    {
        prep.Id, prep.SampleId, prep.Amount, prep.Technique, prep.FiltrationVolume, prep.WashingVolume,
        prep.Diluent, prep.Neutralizer, prep.PreparedByUserId, prep.PreparedAt,
        prep.SourceConfigurationId, prep.WasConfirmedFromConfig
    };
}
