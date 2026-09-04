using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.DTOs.DocumentControl;
using MicroLIMS.Application.Interfaces.DocumentControl;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers.DocumentControl;

[ApiController]
[Authorize]
[Route("api/document-control/acknowledgements")]
public class DocumentAcknowledgementController : ControllerBase
{
    private readonly IDocumentAcknowledgementService _ackService;

    public DocumentAcknowledgementController(IDocumentAcknowledgementService ackService)
    {
        _ackService = ackService;
    }

    private int CurrentUserId =>
        int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

    private string? ClientIpAddress =>
        HttpContext.Connection.RemoteIpAddress?.ToString();

    private string? UserAgent =>
        HttpContext.Request.Headers["User-Agent"].ToString();

    [HttpGet("assignments/{assignmentId:int}/context")]
    public async Task<IActionResult> GetAcknowledgementContext(int assignmentId)
    {
        try
        {
            var result = await _ackService.PresentAcknowledgementContextAsync(assignmentId, CurrentUserId);
            return Ok(ApiResponse<AcknowledgementPresentationDto>.Ok(result));
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
    }

    [HttpPost("assignments/{assignmentId:int}/submit")]
    public async Task<IActionResult> SubmitAcknowledgement(int assignmentId, [FromBody] AcknowledgementSubmissionRequest request)
    {
        if (assignmentId != request.AssignmentId)
        {
            return BadRequest(ApiResponse<object>.Fail("Route assignmentId does not match request body AssignmentId."));
        }

        try
        {
            var enrichedRequest = request with
            {
                ClientIpAddress = request.ClientIpAddress ?? ClientIpAddress,
                UserAgent = request.UserAgent ?? UserAgent
            };

            var result = await _ackService.AcknowledgeAssignmentAsync(enrichedRequest, CurrentUserId);
            return Ok(ApiResponse<AcknowledgementResultDto>.Ok(result, "Document revision successfully acknowledged."));
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
    }

    [HttpPost("assignments/{assignmentId:int}/reading-progress")]
    public async Task<IActionResult> RecordReadingProgress(int assignmentId, [FromBody] ReadingProgressUpdateRequest request)
    {
        if (assignmentId != request.AssignmentId)
        {
            return BadRequest(ApiResponse<object>.Fail("Route assignmentId does not match request body AssignmentId."));
        }

        try
        {
            var result = await _ackService.RecordReadingProgressAsync(request, CurrentUserId);
            return Ok(ApiResponse<ReadingProgressResultDto>.Ok(result));
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
    }

    [HttpGet("assignments/{assignmentId:int}/status")]
    public async Task<IActionResult> GetAcknowledgementStatus(int assignmentId)
    {
        try
        {
            var result = await _ackService.GetAcknowledgementStatusAsync(assignmentId, CurrentUserId);
            return Ok(ApiResponse<AcknowledgementResultDto?>.Ok(result));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, ApiResponse<object>.Fail(ex.Message));
        }
    }
}
