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
public class DocumentControlController : ControllerBase
{
    private readonly IDocumentMasterService _masterService;

    public DocumentControlController(IDocumentMasterService masterService)
    {
        _masterService = masterService;
    }

    private int CurrentUserId =>
        int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

    [HttpGet("library")]
    public async Task<IActionResult> GetLibrary([FromQuery] DocumentLibraryFilterRequest filter)
    {
        try
        {
            var response = await _masterService.GetLibraryAsync(filter, CurrentUserId);
            return Ok(ApiResponse<DocumentLibraryResponse>.Ok(response));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpGet("documents/{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        try
        {
            var doc = await _masterService.GetByIdAsync(id, CurrentUserId);
            return Ok(ApiResponse<DocumentMasterDto>.Ok(doc));
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

    [HttpPost("documents")]
    public async Task<IActionResult> RegisterDocument([FromBody] RegisterDocumentMasterRequest request)
    {
        try
        {
            var result = await _masterService.RegisterDocumentMasterAsync(request, CurrentUserId);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, ApiResponse<DocumentMasterDto>.Ok(result));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ApiResponse<object>.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpPut("documents/{id:int}/metadata")]
    public async Task<IActionResult> UpdateDraftMetadata(int id, [FromBody] UpdateDocumentMasterDraftRequest request)
    {
        try
        {
            var result = await _masterService.UpdateDraftMetadataAsync(id, request, CurrentUserId);
            return Ok(ApiResponse<DocumentMasterDto>.Ok(result));
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
            return Conflict(ApiResponse<object>.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpPost("documents/{id:int}/void")]
    public async Task<IActionResult> VoidMaster(int id, [FromBody] VoidDocumentMasterRequest request)
    {
        try
        {
            var result = await _masterService.VoidMasterAsync(id, request, CurrentUserId);
            return Ok(ApiResponse<DocumentMasterDto>.Ok(result));
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

    [HttpPost("revisions/{revisionId:int}/cancel")]
    public async Task<IActionResult> CancelDraftRevision(int revisionId, [FromBody] CancelDraftRevisionRequest request)
    {
        try
        {
            var result = await _masterService.CancelDraftRevisionAsync(revisionId, request, CurrentUserId);
            return Ok(ApiResponse<DocumentRevisionDto>.Ok(result));
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

    [HttpPost("documents/{id:int}/assignments")]
    public async Task<IActionResult> AddAssignment(int id, [FromBody] CreateAssignmentRequest request)
    {
        try
        {
            var result = await _masterService.AddOrUpdateAssignmentAsync(id, request, CurrentUserId);
            return Ok(ApiResponse<DocumentMasterAssignmentDto>.Ok(result));
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

    // POST, not DELETE. RemoveAssignmentAsync already retains the record by
    // setting IsActive = false, so DC-URS-184 was satisfied, but FS-1a-170
    // requires that no delete endpoint exist for controlled records - a DELETE
    // verb here advertised a capability the system must not offer.
    [HttpPost("documents/{id:int}/assignments/{assignmentId:int}/deactivate")]
    public async Task<IActionResult> RemoveAssignment(int id, int assignmentId)
    {
        try
        {
            await _masterService.RemoveAssignmentAsync(id, assignmentId, CurrentUserId);
            return Ok(ApiResponse<bool>.Ok(true, "Assignment removed successfully."));
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
}
