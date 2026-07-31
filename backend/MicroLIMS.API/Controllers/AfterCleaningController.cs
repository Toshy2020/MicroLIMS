using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

public record ReceiveAfterCleaningRequest(int MachineId, int CauseOfTestingId, string SampledBy, string ControlNumber);
public record PrepareAfterCleaningRequest(int SampleId, List<AfterCleaningPreparationSelection> Selections);
public record CompleteAfterCleaningRequest(int TestOrderId, int FinalCount);

[ApiController]
[Route("api/aftercleaning")]
[Authorize]
public class AfterCleaningController : ControllerBase
{
    private readonly AfterCleaningService _afterCleaningService;

    public AfterCleaningController(AfterCleaningService afterCleaningService)
    {
        _afterCleaningService = afterCleaningService;
    }

    private int CurrentUserId => int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

    [HttpPost("receive")]
    public async Task<IActionResult> Receive(ReceiveAfterCleaningRequest request) =>
        Ok(ApiResponse<object>.Ok(await _afterCleaningService.ReceiveAsync(new AfterCleaningReceiveRequest(
            request.MachineId, request.CauseOfTestingId, request.SampledBy, request.ControlNumber, CurrentUserId))));

    [HttpPost("prepare")]
    public async Task<IActionResult> Prepare(PrepareAfterCleaningRequest request) =>
        Ok(ApiResponse<object>.Ok(await _afterCleaningService.PrepareAsync(request.SampleId, request.Selections, CurrentUserId)));

    [HttpPost("step1/start/{testOrderId}")]
    public async Task<IActionResult> StartStep1(int testOrderId)
    {
        await _afterCleaningService.StartStep1Async(testOrderId, CurrentUserId);
        return Ok(ApiResponse<object>.Ok(new { }));
    }

    [HttpPost("step2/start/{testOrderId}")]
    public async Task<IActionResult> StartStep2(int testOrderId)
    {
        await _afterCleaningService.StartStep2Async(testOrderId, CurrentUserId);
        return Ok(ApiResponse<object>.Ok(new { }));
    }

    [HttpPost("complete")]
    public async Task<IActionResult> Complete(CompleteAfterCleaningRequest request) =>
        Ok(ApiResponse<object>.Ok(await _afterCleaningService.CompleteAsync(request.TestOrderId, request.FinalCount, CurrentUserId)));
}
