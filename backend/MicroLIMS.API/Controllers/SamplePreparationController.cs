using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.Services;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

// Manual entry - only reachable when the Item has no preparation
// configuration yet; these values also become that Item's standing config.
public record PrepareSampleHttpRequest(
    int SampleId, decimal Amount, string Unit, string Technique, decimal? FiltrationVolume, decimal? WashingVolume,
    int DiluentTypeId, int? DiluentMediaId, int NeutralizerId, string Password);

// Confirm-only - the Item's configured steps are the ones performed.
public record ConfirmPreparationHttpRequest(int SampleId, string Password);

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
            r.SampleId, r.Amount, r.Unit, r.Technique, r.FiltrationVolume, r.WashingVolume,
            r.DiluentTypeId, r.DiluentMediaId, r.NeutralizerId, CurrentUserId, r.Password), ClientIp);

        return Ok(ApiResponse<object>.Ok(Project(prep)));
    }

    [HttpPost("confirm")]
    public async Task<IActionResult> Confirm(ConfirmPreparationHttpRequest r)
    {
        var prep = await _service.ConfirmFromConfigurationAsync(
            new ConfirmPreparationRequest(r.SampleId, CurrentUserId, r.Password), ClientIp);

        return Ok(ApiResponse<object>.Ok(Project(prep)));
    }

    [HttpGet("{sampleId}/is-prepared")]
    public async Task<IActionResult> IsPrepared(int sampleId) =>
        Ok(ApiResponse<object>.Ok(new { prepared = await _service.IsPreparedAsync(sampleId) }));

    // Avoid the SamplePreparation <-> Sample <-> TestOrders navigation
    // cycle when serializing (auto-assign loads the sample's TestOrders
    // into the same tracked context).
    private static object Project(Domain.Entities.SamplePreparation prep) => new
    {
        prep.Id, prep.SampleId, prep.Amount, prep.Unit, prep.Technique, prep.FiltrationVolume, prep.WashingVolume,
        prep.DiluentTypeId, prep.DiluentMediaId, prep.NeutralizerId, prep.PreparedByUserId, prep.PreparedAt,
        prep.SourceConfigurationId, prep.WasConfirmedFromConfig
    };
}
