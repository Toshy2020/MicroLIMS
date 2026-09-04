using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.DTOs.DocumentControl;
using MicroLIMS.Application.Interfaces.DocumentControl;
using MicroLIMS.Domain.Enums;
using System.Security.Claims;

namespace MicroLIMS.API.Controllers.DocumentControl;

[ApiController]
[Route("api/document-control/periodic-reviews")]
[Authorize]
public class PeriodicReviewController : ControllerBase
{
    private readonly IPeriodicReviewService _reviewService;

    public PeriodicReviewController(IPeriodicReviewService reviewService)
    {
        _reviewService = reviewService;
    }

    private int GetCurrentUserId()
    {
        var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (int.TryParse(idClaim, out var id))
            return id;
        return 1; // Default fallback for integration tests
    }

    [HttpGet("tasks")]
    public async Task<ActionResult<List<PeriodicReviewTaskDto>>> GetReviewTasks(
        [FromQuery] int? masterId,
        [FromQuery] int? revisionId,
        [FromQuery] PeriodicReviewTaskStatus? status,
        [FromQuery] int? reviewerUserId)
    {
        var tasks = await _reviewService.GetReviewTasksAsync(masterId, revisionId, status, reviewerUserId);
        return Ok(tasks);
    }

    [HttpGet("tasks/{id}")]
    public async Task<ActionResult<PeriodicReviewTaskDto>> GetReviewTaskById(int id)
    {
        var userId = GetCurrentUserId();
        var task = await _reviewService.GetReviewTaskByIdAsync(id, userId);
        return Ok(task);
    }

    [HttpGet("tasks/{id}/workspace")]
    public async Task<ActionResult<PeriodicReviewWorkspaceDto>> GetReviewWorkspace(int id)
    {
        var userId = GetCurrentUserId();
        var workspace = await _reviewService.GetReviewWorkspaceAsync(id, userId);
        return Ok(workspace);
    }

    [HttpPost("tasks/{id}/findings")]
    public async Task<ActionResult<PeriodicReviewFindingDto>> AddFinding(
        int id,
        [FromBody] CreatePeriodicReviewFindingRequest request)
    {
        var userId = GetCurrentUserId();
        var finding = await _reviewService.AddFindingAsync(id, request, userId);
        return CreatedAtAction(nameof(GetReviewTaskById), new { id }, finding);
    }

    [HttpPut("tasks/{id}/findings/{findingId}/resolve")]
    public async Task<ActionResult<PeriodicReviewFindingDto>> ResolveFinding(
        int id,
        int findingId)
    {
        var userId = GetCurrentUserId();
        var finding = await _reviewService.ResolveFindingAsync(id, findingId, userId);
        return Ok(finding);
    }

    [HttpPost("tasks/{id}/assign")]
    public async Task<ActionResult<PeriodicReviewTaskDto>> AssignReviewer(
        int id,
        [FromBody] AssignPeriodicReviewerRequest request)
    {
        var userId = GetCurrentUserId();
        var task = await _reviewService.AssignReviewerAsync(id, request, userId);
        return Ok(task);
    }

    [HttpPost("tasks/{id}/complete")]
    public async Task<ActionResult<PeriodicReviewTaskDto>> CompleteReview(
        int id,
        [FromBody] CompletePeriodicReviewRequest request)
    {
        var userId = GetCurrentUserId();
        var task = await _reviewService.CompleteReviewAsync(id, request, userId);
        return Ok(task);
    }

    [HttpGet("masters/{masterId}/history")]
    public async Task<ActionResult<List<PeriodicReviewTaskDto>>> GetMasterReviewHistory(int masterId)
    {
        var userId = GetCurrentUserId();
        var history = await _reviewService.GetMasterReviewHistoryAsync(masterId, userId);
        return Ok(history);
    }

    [HttpPost("trigger-worker")]
    public async Task<ActionResult<PeriodicReviewGenerationResultDto>> TriggerGenerationManually(
        [FromQuery] DateTime? utcNowOverride = null)
    {
        var result = await _reviewService.GenerateDueReviewTasksAsync(utcNowOverride);
        return Ok(result);
    }
}
