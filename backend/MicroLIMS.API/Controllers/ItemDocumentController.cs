using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

[ApiController]
[Authorize]
public class ItemDocumentController : ControllerBase
{
    private readonly ItemDocumentService _service;

    public ItemDocumentController(ItemDocumentService service)
    {
        _service = service;
    }

    private int CurrentUserId
    {
        get
        {
            var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            return claim != null ? int.Parse(claim.Value) : 1;
        }
    }

    [HttpGet("api/items/{itemId:int}/documents")]
    public async Task<IActionResult> GetDocuments(int itemId)
    {
        try
        {
            var docs = await _service.GetDocumentsForItemAsync(itemId);
            return Ok(ApiResponse<object>.Ok(docs));
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpPost("api/items/{itemId:int}/documents")]
    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [RequestSizeLimit(30 * 1024 * 1024)]
    public async Task<IActionResult> UploadDocument(
        int itemId,
        IFormFile file,
        [FromForm] ItemDocumentType documentType,
        [FromForm] string? version,
        [FromForm] DateTime? effectiveDate)
    {
        if (file == null || file.Length == 0)
            return BadRequest(ApiResponse<object>.Fail("No file provided."));

        try
        {
            using var stream = file.OpenReadStream();
            var doc = await _service.UploadDocumentAsync(
                itemId,
                documentType,
                version ?? "Rev 01",
                effectiveDate,
                stream,
                file.FileName,
                file.ContentType ?? "application/pdf",
                file.Length,
                CurrentUserId);

            return Ok(ApiResponse<object>.Ok(doc));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpGet("api/item-documents/{documentId:int}/content")]
    public async Task<IActionResult> GetContent(int documentId, [FromQuery] bool download = false)
    {
        try
        {
            var (stream, contentType, fileName) = await _service.GetDocumentContentAsync(documentId, CurrentUserId, download);
            if (download)
            {
                return File(stream, contentType, fileName);
            }
            return File(stream, contentType);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
    }
}
