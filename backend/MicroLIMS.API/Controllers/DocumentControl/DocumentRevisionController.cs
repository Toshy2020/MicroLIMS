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
public class DocumentRevisionController : ControllerBase
{
    private readonly IDocumentRevisionService _revisionService;

    public DocumentRevisionController(IDocumentRevisionService revisionService)
    {
        _revisionService = revisionService;
    }

    private int CurrentUserId =>
        int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

    [HttpPost("documents/{masterId:int}/propose-revision")]
    public async Task<IActionResult> ProposeNextRevision(int masterId, [FromBody] ProposeNextRevisionRequest request)
    {
        try
        {
            var result = await _revisionService.ProposeNextRevisionAsync(masterId, request.RevisionType, CurrentUserId);
            return Ok(ApiResponse<ProposeNextRevisionResponse>.Ok(result));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
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

    [HttpPost("documents/{masterId:int}/revisions")]
    public async Task<IActionResult> CreateRevisionFromEffective(int masterId, [FromBody] CreateRevisionRequest request)
    {
        try
        {
            var result = await _revisionService.CreateRevisionFromEffectiveAsync(masterId, request, CurrentUserId);
            return CreatedAtAction(nameof(GetRevisionDetails), new { revisionId = result.Id }, ApiResponse<DocumentRevisionDto>.Ok(result, "Revision created successfully in Draft status."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, ApiResponse<object>.Fail(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
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

    [HttpGet("revisions/{revisionId:int}")]
    public async Task<IActionResult> GetRevisionDetails(int revisionId)
    {
        try
        {
            var result = await _revisionService.GetRevisionDetailsAsync(revisionId, CurrentUserId);
            return Ok(ApiResponse<DocumentRevisionDto>.Ok(result));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpGet("revisions/{revisionId:int}/change-items")]
    public async Task<IActionResult> GetChangeItems(int revisionId)
    {
        try
        {
            var result = await _revisionService.GetChangeItemsAsync(revisionId, CurrentUserId);
            return Ok(ApiResponse<IReadOnlyList<RevisionChangeItemDto>>.Ok(result));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpPost("revisions/{revisionId:int}/change-items")]
    public async Task<IActionResult> AddChangeItem(int revisionId, [FromBody] AddChangeItemRequest request)
    {
        try
        {
            var result = await _revisionService.AddChangeItemAsync(revisionId, request, CurrentUserId);
            return Ok(ApiResponse<RevisionChangeItemDto>.Ok(result, "Change item added successfully."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, ApiResponse<object>.Fail(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
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

    [HttpPut("change-items/{changeItemId:int}")]
    public async Task<IActionResult> UpdateChangeItem(int changeItemId, [FromBody] UpdateChangeItemRequest request)
    {
        try
        {
            var result = await _revisionService.UpdateChangeItemAsync(changeItemId, request, CurrentUserId);
            return Ok(ApiResponse<RevisionChangeItemDto>.Ok(result, "Change item updated successfully."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, ApiResponse<object>.Fail(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
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

    // POST, not DELETE: FS-1a-170 requires that no delete endpoint exist for
    // controlled records, and DC-URS-184 forbids permanent deletion. The change
    // item is deactivated and retained.
    [HttpPost("change-items/{changeItemId:int}/deactivate")]
    public async Task<IActionResult> DeactivateChangeItem(int changeItemId)
    {
        try
        {
            await _revisionService.DeactivateChangeItemAsync(changeItemId, CurrentUserId);
            return Ok(ApiResponse<object>.Ok(null!, "Change item removed successfully."));
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

    [HttpPost("revisions/{revisionId:int}/findings/{findingId:int}/convert")]
    public async Task<IActionResult> ConvertFindingToChangeItem(int revisionId, int findingId, [FromBody] ConvertFindingRequest request)
    {
        try
        {
            var result = await _revisionService.ConvertFindingToChangeItemAsync(revisionId, findingId, request, CurrentUserId);
            return Ok(ApiResponse<RevisionChangeItemDto>.Ok(result, "Review finding transferred to structured change item."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, ApiResponse<object>.Fail(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
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

    [HttpGet("revisions/{revisionId:int}/impact-assessment")]
    public async Task<IActionResult> GetImpactAssessment(int revisionId)
    {
        try
        {
            var result = await _revisionService.GetImpactAssessmentAsync(revisionId, CurrentUserId);
            return Ok(ApiResponse<RevisionImpactAssessmentDto?>.Ok(result));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpPost("revisions/{revisionId:int}/impact-assessment")]
    public async Task<IActionResult> SaveImpactAssessment(int revisionId, [FromBody] SaveImpactAssessmentRequest request)
    {
        try
        {
            var result = await _revisionService.SaveImpactAssessmentAsync(revisionId, request, CurrentUserId);
            return Ok(ApiResponse<RevisionImpactAssessmentDto>.Ok(result, "Revision impact assessment saved successfully."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, ApiResponse<object>.Fail(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
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
