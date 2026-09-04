using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.DTOs.DocumentControl;
using MicroLIMS.Application.Interfaces.DocumentControl;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers.DocumentControl;

[ApiController]
[Authorize]
[Route("api/document-control")]
public class DocumentReviewController : ControllerBase
{
    private readonly IDocumentReviewService _reviewService;

    public DocumentReviewController(IDocumentReviewService reviewService)
    {
        _reviewService = reviewService;
    }

    private int CurrentUserId =>
        int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

    [HttpPost("revisions/{id:int}/submit-review")]
    public async Task<IActionResult> SubmitForReview(int id, [FromBody] SubmitForReviewRequest request)
    {
        try
        {
            var task = await _reviewService.SubmitForReviewAsync(id, request, CurrentUserId);
            return Ok(ApiResponse<DocumentReviewTaskDto>.Ok(task, "Revision submitted for technical review successfully."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, ApiResponse<object>.Fail(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpGet("reviews/{id:int}")]
    public async Task<IActionResult> GetReviewTaskById(int id)
    {
        try
        {
            var task = await _reviewService.GetReviewTaskByIdAsync(id, CurrentUserId);
            return Ok(ApiResponse<DocumentReviewTaskDto>.Ok(task));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, ApiResponse<object>.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpGet("revisions/{id:int}/review-tasks")]
    public async Task<IActionResult> GetReviewTasksByRevisionId(int id)
    {
        try
        {
            var tasks = await _reviewService.GetReviewTasksByRevisionIdAsync(id, CurrentUserId);
            return Ok(ApiResponse<List<DocumentReviewTaskDto>>.Ok(tasks));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, ApiResponse<object>.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpGet("reviews/my-tasks")]
    public async Task<IActionResult> GetMyAssignedReviewTasks()
    {
        try
        {
            var tasks = await _reviewService.GetMyAssignedReviewTasksAsync(CurrentUserId);
            return Ok(ApiResponse<List<DocumentReviewTaskDto>>.Ok(tasks));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpPost("reviews/{id:int}/findings")]
    public async Task<IActionResult> AddReviewFinding(int id, [FromBody] AddReviewFindingRequest request)
    {
        try
        {
            var finding = await _reviewService.AddReviewFindingAsync(id, request, CurrentUserId);
            return Ok(ApiResponse<DocumentReviewFindingDto>.Ok(finding, "Review finding created successfully."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, ApiResponse<object>.Fail(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpPost("findings/{id:int}/respond")]
    public async Task<IActionResult> RespondToFinding(int id, [FromBody] RespondToFindingRequest request)
    {
        try
        {
            var finding = await _reviewService.RespondToFindingAsync(id, request, CurrentUserId);
            return Ok(ApiResponse<DocumentReviewFindingDto>.Ok(finding, "Author response recorded successfully."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, ApiResponse<object>.Fail(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpPost("findings/{id:int}/verify")]
    public async Task<IActionResult> VerifyFinding(int id, [FromBody] VerifyFindingRequest request)
    {
        try
        {
            var finding = await _reviewService.VerifyFindingAsync(id, request, CurrentUserId);
            return Ok(ApiResponse<DocumentReviewFindingDto>.Ok(finding, "Finding marked as verified by reviewer."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, ApiResponse<object>.Fail(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpPost("findings/{id:int}/resolve")]
    public async Task<IActionResult> ResolveFinding(int id)
    {
        try
        {
            var finding = await _reviewService.ResolveFindingAsync(id, CurrentUserId);
            return Ok(ApiResponse<DocumentReviewFindingDto>.Ok(finding, "Finding marked as resolved."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, ApiResponse<object>.Fail(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpPost("reviews/{id:int}/decide")]
    public async Task<IActionResult> DecideReview(int id, [FromBody] ReviewDecisionRequest request)
    {
        try
        {
            var task = await _reviewService.DecideReviewAsync(id, request, CurrentUserId);
            return Ok(ApiResponse<DocumentReviewTaskDto>.Ok(task, "Review decision recorded successfully."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, ApiResponse<object>.Fail(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }
}
