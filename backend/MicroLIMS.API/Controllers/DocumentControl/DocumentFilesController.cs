using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.DTOs.DocumentControl;
using MicroLIMS.Application.Interfaces.DocumentControl;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers.DocumentControl;

[ApiController]
[Authorize]
[Route("api/document-control")]
public class DocumentFilesController : ControllerBase
{
    private readonly IDocumentFileService _fileService;

    public DocumentFilesController(IDocumentFileService fileService)
    {
        _fileService = fileService;
    }

    private int CurrentUserId =>
        int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

    [HttpPost("revisions/{revisionId:int}/files")]
    [RequestSizeLimit(50 * 1024 * 1024)]
    public async Task<IActionResult> UploadFile(int revisionId, IFormFile file, [FromForm] FileRole fileRole)
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
            var result = await _fileService.UploadRevisionFileAsync(
                revisionId,
                fileRole,
                file.FileName,
                file.ContentType,
                content,
                CurrentUserId);

            return Ok(ApiResponse<RevisionFileDto>.Ok(result));
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
            return StatusCode(422, ApiResponse<object>.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpGet("files/{fileId:int}/download")]
    public async Task<IActionResult> DownloadFile(int fileId)
    {
        try
        {
            var (meta, content) = await _fileService.GetFileContentAsync(fileId, CurrentUserId, isDownload: true);

            var safeFileName = Uri.EscapeDataString(meta.FileName);
            Response.Headers["Content-Disposition"] = $"attachment; filename=\"{safeFileName}\"";
            Response.Headers["X-Content-Type-Options"] = "nosniff";

            return File(content, meta.ContentType);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, ApiResponse<object>.Fail(ex.Message));
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("integrity"))
        {
            return StatusCode(500, ApiResponse<object>.Fail(ex.Message));
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

    [HttpGet("files/{fileId:int}/view")]
    public async Task<IActionResult> ViewFile(int fileId)
    {
        try
        {
            var (meta, content) = await _fileService.GetFileContentAsync(fileId, CurrentUserId, isDownload: false);

            var safeFileName = Uri.EscapeDataString(meta.FileName);
            Response.Headers["Content-Disposition"] = $"inline; filename=\"{safeFileName}\"";
            Response.Headers["X-Content-Type-Options"] = "nosniff";

            return File(content, meta.ContentType);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, ApiResponse<object>.Fail(ex.Message));
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("integrity"))
        {
            return StatusCode(500, ApiResponse<object>.Fail(ex.Message));
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
