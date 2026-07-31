using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

[ApiController]
[Route("api/testorders")]
[Authorize]
public class TestingWorkspaceController : ControllerBase
{
    private readonly ITestWorkspaceService _workspaceService;

    public TestingWorkspaceController(ITestWorkspaceService workspaceService)
    {
        _workspaceService = workspaceService;
    }

    [HttpGet]
    public async Task<IActionResult> GetActive() => Ok(ApiResponse<object>.Ok(await _workspaceService.GetActiveSamplesAsync()));

    [HttpGet("{id}")]
    public async Task<IActionResult> GetOne(int id)
    {
        var sample = await _workspaceService.GetSampleAsync(id);
        return sample is null ? NotFound(ApiResponse<object>.Fail("Not found.")) : Ok(ApiResponse<object>.Ok(sample));
    }

    [HttpPut("{id}")]
    public IActionResult UpdateStatus(int id) => Ok(ApiResponse<object>.Ok(new { id }, "Use /api/results, /api/review, or /api/approval to progress a test order."));
}
