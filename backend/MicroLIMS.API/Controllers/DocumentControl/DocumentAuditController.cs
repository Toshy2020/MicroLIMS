using MicroLIMS.Shared.Exceptions;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
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
        var result = await _auditService.SearchAuditLogsAsync(filter, CurrentUserId);
        return Ok(ApiResponse<DocumentAuditResponse>.Ok(result));
    }

    [HttpGet("documents/{id:int}/audit")]
    public async Task<IActionResult> GetDocumentAuditHistory(int id, [FromQuery] int? revisionId = null)
    {
        var result = await _auditService.GetRecordAuditHistoryAsync(id, revisionId, CurrentUserId);
        return Ok(ApiResponse<List<DocumentAuditItemDto>>.Ok(result));
    }

    [HttpPost("audit/export")]
    public async Task<IActionResult> ExportAuditTrail([FromBody] DocumentAuditFilterRequest filter)
    {
        var (bytes, contentType, fileName) = await _auditService.ExportAuditTrailAsync(filter, CurrentUserId);

        Response.Headers["Content-Disposition"] = $"attachment; filename=\"{fileName}\"";
        Response.Headers["X-Content-Type-Options"] = "nosniff";

        return File(bytes, contentType);
    }
}
