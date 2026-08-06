using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

public record ReceiveEMRequest(int DepartmentId, int CauseOfTestingId, string SampledBy, string ControlNumber);
public record PrepareEMRequest(int SampleId, List<int> RoomTestConfigurationIds);

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

    // The checklist screen - selecting which rooms are included in this
    // monitoring session generates the batch TestOrders + SampleLocations.
    [HttpPost("prepare")]
    public async Task<IActionResult> Prepare(PrepareEMRequest request) =>
        Ok(ApiResponse<object>.Ok(await _emService.PrepareAsync(request.SampleId, request.RoomTestConfigurationIds, CurrentUserId)));
}
