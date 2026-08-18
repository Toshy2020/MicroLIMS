using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

// HTTP records for request binding (multipart is handled in actions directly).
public record VoidDocumentHttpRequest(string Reason);

// Material Lot Documents API — serves the document subsystem attached to
// received material lots (Material.Id is the lot identifier).
//
// Authorization:
//   List / Content / COA-eligibility : Analyst, SectionHead, SystemAdministrator
//   Upload                           : Analyst, SectionHead, SystemAdministrator
//   Supersede / Void                 : SectionHead, SystemAdministrator only
[ApiController]
[Authorize(Roles = RoleConstants.Analyst + "," + RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
public class MaterialDocumentController : ControllerBase
{
    private readonly MaterialDocumentService _service;
    private const long MaxStreamBytes = 30 * 1024 * 1024; // hard stream limit (slightly over configured max)

    public MaterialDocumentController(MaterialDocumentService service)
    {
        _service = service;
    }

    private int CurrentUserId =>
        int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

    // ---- List documents for a lot ----
    [HttpGet("api/inventory/materials/{materialId:int}/documents")]
    public async Task<IActionResult> GetDocuments(int materialId)
    {
        try
        {
            var docs = await _service.GetDocumentsAsync(materialId, CurrentUserId);
            return Ok(ApiResponse<object>.Ok(docs));
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
    }

    // ---- Upload a new document ----
    [HttpPost("api/inventory/materials/{materialId:int}/documents")]
    [RequestSizeLimit(30 * 1024 * 1024)]
    public async Task<IActionResult> Upload(int materialId, IFormFile file, [FromForm] MaterialDocumentType documentType)
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
            var result = await _service.UploadAsync(materialId, new UploadMaterialDocumentRequest(
                documentType,
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

    // ---- Download / view document content ----
    // Returns the file bytes with appropriate Content-Disposition.
    // PDFs and images use "inline" so the browser can render them; others use "attachment".
    [HttpGet("api/inventory/material-documents/{documentId:int}/content")]
    public async Task<IActionResult> GetContent(int documentId, [FromQuery] int materialId)
    {
        try
        {
            var (meta, content) = await _service.GetContentAsync(documentId, materialId, CurrentUserId);

            var disposition = IsInlineType(meta.ContentType) ? "inline" : "attachment";
            var safeFileName = Uri.EscapeDataString(meta.OriginalFileName);

            Response.Headers["Content-Disposition"] = $"{disposition}; filename=\"{safeFileName}\"";
            Response.Headers["X-Content-Type-Options"] = "nosniff";
            return File(content, meta.ContentType);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("integrity"))
        {
            return StatusCode(500, ApiResponse<object>.Fail(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
    }

    // ---- COA eligibility for a lot ----
    [HttpGet("api/inventory/materials/{materialId:int}/coa-eligibility")]
    public async Task<IActionResult> GetCOAEligibility(int materialId)
    {
        try
        {
            var result = await _service.GetCOAEligibilityAsync(materialId);
            return Ok(ApiResponse<object>.Ok(result));
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
    }

    // ---- Supersede (SectionHead / SystemAdministrator only) ----
    [HttpPost("api/inventory/material-documents/{documentId:int}/supersede")]
    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [RequestSizeLimit(30 * 1024 * 1024)]
    public async Task<IActionResult> Supersede(int documentId, IFormFile file, [FromForm] string reason, [FromForm] int materialId)
    {
        if (file == null || file.Length == 0)
            return BadRequest(ApiResponse<object>.Fail("A replacement file is required for supersession."));

        if (string.IsNullOrWhiteSpace(reason))
            return BadRequest(ApiResponse<object>.Fail("A supersession reason is required."));

        byte[] content;
        using (var ms = new MemoryStream())
        {
            await file.CopyToAsync(ms);
            content = ms.ToArray();
        }

        try
        {
            var result = await _service.SupersedeAsync(documentId, materialId,
                new SupersedeMaterialDocumentRequest(file.FileName, file.ContentType, content, reason),
                CurrentUserId);
            return Ok(ApiResponse<object>.Ok(result));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    // ---- Void (SectionHead / SystemAdministrator only) ----
    [HttpPost("api/inventory/material-documents/{documentId:int}/void")]
    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    public async Task<IActionResult> Void(int documentId, [FromQuery] int materialId, VoidDocumentHttpRequest request)
    {
        try
        {
            var result = await _service.VoidAsync(documentId, materialId,
                new VoidMaterialDocumentRequest(request.Reason), CurrentUserId);
            return Ok(ApiResponse<object>.Ok(result));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    private static bool IsInlineType(string contentType) =>
        contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ||
        contentType == "application/pdf";
}
