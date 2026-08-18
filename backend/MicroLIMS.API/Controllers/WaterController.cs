using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

public record ReceiveWaterRequest(int WaterDepartmentId, int CauseOfTestingId, string SampleQuantity, string SampledBy, string ControlNumber);
public record PrepareWaterRequest(int SampleId, List<int> WaterSamplingPointIds);
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
            request.WaterDepartmentId, request.CauseOfTestingId, request.SampleQuantity, request.SampledBy, request.ControlNumber, CurrentUserId))));

    // The checklist screen - selecting which sampling points are included
    // in this batch generates the TestOrders + SampleLocations.
    [HttpPost("prepare")]
    public async Task<IActionResult> Prepare(PrepareWaterRequest request) =>
        Ok(ApiResponse<object>.Ok(await _waterService.PrepareAsync(request.SampleId, request.WaterSamplingPointIds, CurrentUserId)));

    [HttpPost("calculate")]
    public async Task<IActionResult> Calculate(CalculateWaterRequest request) =>
        Ok(ApiResponse<object>.Ok(await _waterService.CalculateAsync(request.TestOrderId, request.Readings)));

    [HttpGet("daily-report")]
    public async Task<IActionResult> DailyReport([FromQuery] DateTime date) =>
        Ok(ApiResponse<object>.Ok(await _waterService.GetDailyAggregateAsync(date)));
}
