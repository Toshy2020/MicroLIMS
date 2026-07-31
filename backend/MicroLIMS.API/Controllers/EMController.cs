using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

public record ReceiveEMRequest(int DepartmentId, int CauseOfTestingId, string SampledBy, string ControlNumber);
public record PrepareEMRequest(int SampleId, List<EMPreparationSelection> Selections);
public record CompleteEMRequest(int TestOrderId, int FinalCount, int ActionLimit);

[ApiController]
[Route("api/em")]
[Authorize]
public class EMController : ControllerBase
{
    private readonly EMService _emService;

    public EMController(EMService emService)
    {
        _emService = emService;
    }

    private int CurrentUserId => int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

    [HttpPost("receive")]
    public async Task<IActionResult> Receive(ReceiveEMRequest request) =>
        Ok(ApiResponse<object>.Ok(await _emService.ReceiveAsync(new EMReceiveRequest(
            request.DepartmentId, request.CauseOfTestingId, request.SampledBy, request.ControlNumber, CurrentUserId))));

    // The checkbox screen - selecting Room x TestType generates the TestOrders.
    [HttpPost("prepare")]
    public async Task<IActionResult> Prepare(PrepareEMRequest request) =>
        Ok(ApiResponse<object>.Ok(await _emService.PrepareAsync(request.SampleId, request.Selections, CurrentUserId)));

    [HttpPost("step1/start/{testOrderId}")]
    public async Task<IActionResult> StartStep1(int testOrderId)
    {
        await _emService.StartStep1Async(testOrderId, CurrentUserId);
        return Ok(ApiResponse<object>.Ok(new { }));
    }

    [HttpPost("step2/start/{testOrderId}")]
    public async Task<IActionResult> StartStep2(int testOrderId)
    {
        await _emService.StartStep2Async(testOrderId, CurrentUserId);
        return Ok(ApiResponse<object>.Ok(new { }));
    }

    [HttpPost("complete")]
    public async Task<IActionResult> Complete(CompleteEMRequest request) =>
        Ok(ApiResponse<object>.Ok(await _emService.CompleteAsync(request.TestOrderId, request.FinalCount, CurrentUserId, request.ActionLimit)));
}
