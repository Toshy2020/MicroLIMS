using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

public record SaveResultRequest(int TestOrderId, string RawValue);

[ApiController]
[Route("api/results")]
[Authorize]
public class ResultController : ControllerBase
{
    private readonly IResultService _resultService;
    private readonly IUserSectionScopeService _scopeService;

    public ResultController(IResultService resultService, IUserSectionScopeService scopeService)
    {
        _resultService = resultService;
        _scopeService = scopeService;
    }

    [HttpPost]
    public async Task<IActionResult> Save(SaveResultRequest request)
    {
        var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        await _scopeService.EnsureTestOrderAccessAsync(userId, request.TestOrderId);
        var result = await _resultService.SaveResultAsync(request.TestOrderId, request.RawValue, userId);
        return Ok(ApiResponse<object>.Ok(result));
    }

    [HttpPut]
    public async Task<IActionResult> Update(SaveResultRequest request)
    {
        var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        await _scopeService.EnsureTestOrderAccessAsync(userId, request.TestOrderId);
        var result = await _resultService.SaveResultAsync(request.TestOrderId, request.RawValue, userId);
        return Ok(ApiResponse<object>.Ok(result));
    }

    [HttpGet]
    public async Task<IActionResult> GetForTestOrder([FromQuery] int testOrderId)
    {
        var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        await _scopeService.EnsureTestOrderAccessAsync(userId, testOrderId);
        return Ok(ApiResponse<object>.Ok(await _resultService.GetResultsForTestOrderAsync(testOrderId)));
    }
}
