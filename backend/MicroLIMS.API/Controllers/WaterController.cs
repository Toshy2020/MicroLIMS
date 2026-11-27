using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

public record PrepareWaterRequest(int SampleId, List<int> WaterSamplingPointIds, string? StorageCondition, int? StorageTimeHours, List<int>? MonitoringOccurrenceIds = null);

[ApiController]
[Route("api/water")]
[Authorize]
public class WaterController : ControllerBase
{
    private readonly WaterService _waterService;
    private readonly IUserSectionScopeService _scopeService;

    public WaterController(WaterService waterService, IUserSectionScopeService scopeService)
    {
        _scopeService = scopeService;
        _waterService = waterService;
    }

    private int CurrentUserId => int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

    [HttpPost("receive")]
    [Authorize(Policy = PermissionConstants.SamplesReceiveOwnLab)]
    public async Task<IActionResult> Receive(ReceiveWaterRequest request) =>
        Ok(ApiResponse<object>.Ok(await _waterService.ReceiveAsync(new WaterReceiveRequest(
            request.WaterDepartmentId, request.CauseOfTestingId, request.SampleQuantity, request.SampledBy, request.ControlNumber, CurrentUserId, request.SampledAt, request.LabSectionId))));

    // The checklist screen - selecting which sampling points are included
    // in this batch generates the TestOrders + SampleLocations.
    [HttpPost("prepare")]
    [Authorize(Policy = PermissionConstants.SamplesReceiveOwnLab)]
    public async Task<IActionResult> Prepare(PrepareWaterRequest request, CancellationToken ct = default) =>
        Ok(ApiResponse<object>.Ok(await _waterService.PrepareAsync(
            request.SampleId, request.WaterSamplingPointIds, CurrentUserId, request.StorageCondition, request.StorageTimeHours, request.MonitoringOccurrenceIds, ct)));

    [HttpGet("daily-report")]
    public async Task<IActionResult> DailyReport([FromQuery] DateTime date) =>
        Ok(ApiResponse<object>.Ok(await _waterService.GetDailyAggregateAsync(date, await _scopeService.GetReadableSectionIdsAsync(CurrentUserId))));
}
