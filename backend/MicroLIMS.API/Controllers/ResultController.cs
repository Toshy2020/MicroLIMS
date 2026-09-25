using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

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

    [HttpGet]
    public async Task<IActionResult> GetForTestOrder([FromQuery] int testOrderId)
    {
        var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        await _scopeService.EnsureTestOrderAccessAsync(userId, testOrderId);
        return Ok(ApiResponse<object>.Ok(await _resultService.GetResultsForTestOrderAsync(testOrderId)));
    }
}
