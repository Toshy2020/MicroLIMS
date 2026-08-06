using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

public record ReceiveAfterCleaningRequest(int MachineId, int CauseOfTestingId, string SampledBy, string ControlNumber);
public record PrepareAfterCleaningRequest(int SampleId, List<int> MachinePartConfigurationIds);

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

    // The checklist screen - selecting which machine parts are included
    // in this batch generates the batch TestOrders + SampleLocations.
    [HttpPost("prepare")]
    public async Task<IActionResult> Prepare(PrepareAfterCleaningRequest request) =>
        Ok(ApiResponse<object>.Ok(await _afterCleaningService.PrepareAsync(request.SampleId, request.MachinePartConfigurationIds, CurrentUserId)));
}
