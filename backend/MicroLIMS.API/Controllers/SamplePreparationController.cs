using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.Services;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

public record PrepareSampleHttpRequest(
    int SampleId, decimal Amount, string Unit, string Technique, decimal? FiltrationVolume, decimal? WashingVolume,
    int DiluentTypeId, int? DiluentMediaId, int NeutralizerId, string? StorageCondition, int? StorageTimeHours);

// Test Preparation step - Product/RM/PM/Water, once per Sample, must
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

    [HttpPost]
    public async Task<IActionResult> Prepare(PrepareSampleHttpRequest r) =>
        Ok(ApiResponse<object>.Ok(await _service.PrepareAsync(new PrepareSampleRequest(
            r.SampleId, r.Amount, r.Unit, r.Technique, r.FiltrationVolume, r.WashingVolume,
            r.DiluentTypeId, r.DiluentMediaId, r.NeutralizerId, CurrentUserId, r.StorageCondition, r.StorageTimeHours))));

    [HttpGet("{sampleId}/is-prepared")]
    public async Task<IActionResult> IsPrepared(int sampleId) =>
        Ok(ApiResponse<object>.Ok(new { prepared = await _service.IsPreparedAsync(sampleId) }));
}
