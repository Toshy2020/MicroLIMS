using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

public record ReceiveWaterRequest(int WaterSamplingPointId, int CauseOfTestingId, string SampleQuantity, string SampledBy, string ControlNumber);
public record CalculateWaterRequest(int TestOrderId, List<decimal> Readings);

[ApiController]
[Route("api/water")]
[Authorize]
public class WaterController : ControllerBase
{
    private readonly WaterService _waterService;

    public WaterController(WaterService waterService)
    {
        _waterService = waterService;
    }

    private int CurrentUserId => int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

    [HttpPost("receive")]
    public async Task<IActionResult> Receive(ReceiveWaterRequest request) =>
        Ok(ApiResponse<object>.Ok(await _waterService.ReceiveAsync(new WaterReceiveRequest(
            request.WaterSamplingPointId, request.CauseOfTestingId, request.SampleQuantity, request.SampledBy, request.ControlNumber, CurrentUserId))));

    [HttpPost("calculate")]
    public async Task<IActionResult> Calculate(CalculateWaterRequest request) =>
        Ok(ApiResponse<object>.Ok(await _waterService.CalculateAsync(request.TestOrderId, request.Readings)));

    [HttpGet("daily-report")]
    public async Task<IActionResult> DailyReport([FromQuery] DateTime date) =>
        Ok(ApiResponse<object>.Ok(await _waterService.GetDailyAggregateAsync(date)));
}
