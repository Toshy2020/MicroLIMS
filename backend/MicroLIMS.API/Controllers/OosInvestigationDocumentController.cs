using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.Services;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

// API for managing controlled documents (Lab Investigation Reports) attached to OOS groups.
// Gated to SectionHead and SystemAdministrator (same as OOS Tracking).
[ApiController]
[Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
public class OosInvestigationDocumentController : ControllerBase
{
    private readonly OosInvestigationDocumentService _service;

    public OosInvestigationDocumentController(OosInvestigationDocumentService service)
    {
        _service = service;
    }

    private int CurrentUserId => int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

    // List all investigation documents for an OOS group.
    [HttpGet("api/oos-tracking/{oosGroupCode}/investigation-documents")]
    public async Task<IActionResult> GetDocuments(string oosGroupCode)
    {
        try
        {
            var result = await _service.GetDocumentsAsync(oosGroupCode);
            return Ok(ApiResponse<object>.Ok(result));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    // Upload a new investigation document to an OOS group.
    [HttpPost("api/oos-tracking/{oosGroupCode}/investigation-documents")]
    [RequestSizeLimit(30 * 1024 * 1024)] // 30 MB server transport ceiling
    public async Task<IActionResult> Upload(string oosGroupCode, IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(ApiResponse<object>.Fail("No file was provided."));

        byte[] content;
        using (var ms = new MemoryStream())
        {
            await file.CopyToAsync(ms);
            content = ms.ToArray();
        }

        try
        {
            var result = await _service.UploadAsync(oosGroupCode, new UploadOosInvestigationDocumentRequest(
                file.FileName,
                file.ContentType,
                content), CurrentUserId);
            return Ok(ApiResponse<object>.Ok(result));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    // Retrieve the raw content of an investigation document.
    [HttpGet("api/oos-investigation-documents/{documentId:int}/content")]
    public async Task<IActionResult> GetContent(int documentId, [FromQuery] string oosGroupCode)
    {
        try
        {
            var (meta, bytes) = await _service.GetContentAsync(documentId, oosGroupCode, CurrentUserId);
            return File(bytes, meta.ContentType, meta.OriginalFileName);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    // Supersede an existing document with a replacement file.
    [HttpPost("api/oos-investigation-documents/{documentId:int}/supersede")]
    [RequestSizeLimit(30 * 1024 * 1024)]
    public async Task<IActionResult> Supersede(int documentId, [FromQuery] string oosGroupCode, IFormFile file, [FromForm] string reason)
    {
        if (file == null || file.Length == 0)
            return BadRequest(ApiResponse<object>.Fail("A replacement file is required."));
        if (string.IsNullOrWhiteSpace(reason))
            return BadRequest(ApiResponse<object>.Fail("A reason for supersession is required."));

        byte[] content;
        using (var ms = new MemoryStream())
        {
            await file.CopyToAsync(ms);
            content = ms.ToArray();
        }

        try
        {
            var result = await _service.SupersedeAsync(documentId, oosGroupCode, new SupersedeOosInvestigationDocumentRequest(
                file.FileName,
                file.ContentType,
                content,
                reason), CurrentUserId);
            return Ok(ApiResponse<object>.Ok(result));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    // Void a document. The file is retained for audit; document is marked Voided.
    [HttpPost("api/oos-investigation-documents/{documentId:int}/void")]
    public async Task<IActionResult> Void(int documentId, [FromQuery] string oosGroupCode, [FromBody] VoidOosInvestigationDocumentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request?.Reason))
            return BadRequest(ApiResponse<object>.Fail("A reason for voiding is required."));

        try
        {
            var result = await _service.VoidAsync(documentId, oosGroupCode, request, CurrentUserId);
            return Ok(ApiResponse<object>.Ok(result));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }
}
