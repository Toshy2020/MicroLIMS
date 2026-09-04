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
public class DocumentAuditController : ControllerBase
{
    private readonly IDocumentAuditService _auditService;

    public DocumentAuditController(IDocumentAuditService auditService)
    {
        _auditService = auditService;
    }

    private int CurrentUserId =>
        int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

    [HttpGet("audit")]
    public async Task<IActionResult> SearchAuditTrail([FromQuery] DocumentAuditFilterRequest filter)
    {
        try
        {
            var result = await _auditService.SearchAuditLogsAsync(filter, CurrentUserId);
            return Ok(ApiResponse<DocumentAuditResponse>.Ok(result));
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

    [HttpGet("documents/{id:int}/audit")]
    public async Task<IActionResult> GetDocumentAuditHistory(int id, [FromQuery] int? revisionId = null)
    {
        try
        {
            var result = await _auditService.GetRecordAuditHistoryAsync(id, revisionId, CurrentUserId);
            return Ok(ApiResponse<List<DocumentAuditItemDto>>.Ok(result));
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

    [HttpPost("audit/export")]
    public async Task<IActionResult> ExportAuditTrail([FromBody] DocumentAuditFilterRequest filter)
    {
        try
        {
            var (bytes, contentType, fileName) = await _auditService.ExportAuditTrailAsync(filter, CurrentUserId);

            Response.Headers["Content-Disposition"] = $"attachment; filename=\"{fileName}\"";
            Response.Headers["X-Content-Type-Options"] = "nosniff";

            return File(bytes, contentType);
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
