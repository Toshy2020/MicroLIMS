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

    // Always paged: default 50 rows, hard maximum 200 (see
    // TestingWorkspaceService). The bare route used to return every sample
    // ever received in one response; it now answers exactly like /page,
    // which stays as an alias for the Receiving & Testing workspace.
    [HttpGet]
    [HttpGet("page")]
    public async Task<IActionResult> GetActivePaged([FromQuery] TestingWorkspaceFilterDto filter)
    {
        var result = await _workspaceService.GetActiveSamplesAsync(filter, CurrentUserId);
        return Ok(ApiResponse<PagedResult<SampleDto>>.Ok(result));
    }

    [HttpGet("counts")]
    public async Task<IActionResult> GetWorkloadCounts([FromQuery] int? labSectionId = null)
    {
        var counts = await _workspaceService.GetWorkloadCountsAsync(CurrentUserId, labSectionId);
        return Ok(ApiResponse<WorkspaceTileCountsDto>.Ok(counts));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetOne(int id, [FromQuery] int? labSectionId = null)
    {
        var sample = await _workspaceService.GetSampleAsync(id, CurrentUserId, labSectionId);
        return sample is null ? NotFound(ApiResponse<object>.Fail("Not found.")) : Ok(ApiResponse<object>.Ok(sample));
    }
}
