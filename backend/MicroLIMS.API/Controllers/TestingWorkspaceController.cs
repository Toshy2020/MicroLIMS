using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.DTOs;
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

    private int? CurrentUserId =>
        int.TryParse(User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var id) ? id : null;

    // Unpaged, unfiltered, and deliberately left alone. Five frontend services
    // read this route and all of them expect a bare array:
    // ReceiveService, WorkspaceService, SamplePreparationService,
    // EMPreparationService and AfterCleaningPreparationService. Returning a
    // PagedResult here instead would break every one of them.
    [HttpGet]
    public async Task<IActionResult> GetActive() =>
        Ok(ApiResponse<object>.Ok(await _workspaceService.GetActiveSamplesAsync()));

    // The paged, filtered route the Receiving & Testing workspace moves to.
    // Additive, so callers migrate one at a time rather than all at once.
    [HttpGet("page")]
    public async Task<IActionResult> GetActivePaged([FromQuery] TestingWorkspaceFilterDto filter)
    {
        var result = await _workspaceService.GetActiveSamplesAsync(filter, CurrentUserId);
        return Ok(ApiResponse<PagedResult<SampleDto>>.Ok(result));
    }

    [HttpGet("counts")]
    public async Task<IActionResult> GetWorkloadCounts()
    {
        var counts = await _workspaceService.GetWorkloadCountsAsync(CurrentUserId);
        return Ok(ApiResponse<WorkspaceTileCountsDto>.Ok(counts));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetOne(int id)
    {
        var sample = await _workspaceService.GetSampleAsync(id);
        return sample is null ? NotFound(ApiResponse<object>.Fail("Not found.")) : Ok(ApiResponse<object>.Ok(sample));
    }

    [HttpPut("{id:int}")]
    public IActionResult UpdateStatus(int id) => Ok(ApiResponse<object>.Ok(new { id }, "Use /api/results, /api/review, or /api/approval to progress a test order."));
}
