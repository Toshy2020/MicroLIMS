using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.Services;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

[ApiController]
[Route("api/admin/audit")]
[Authorize(Policy = PermissionConstants.AuditView)]
public class AuditController : ControllerBase
{
    private readonly AuditSearchService _auditSearchService;
    private readonly AuditTraceabilityService _traceabilityService;
    private readonly AuditExportService _exportService;
    public AuditController(
        AuditSearchService auditSearchService,
        AuditTraceabilityService traceabilityService,
        AuditExportService exportService)
    {
        _exportService = exportService;
        _auditSearchService = auditSearchService;
        _traceabilityService = traceabilityService;
    }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] string entityName, [FromQuery] string entityId) =>
        Ok(ApiResponse<object>.Ok(await _auditSearchService.GetForEntityAsync(entityName, entityId)));

    // Search by every possible cross-reference - date range, batch,
    // control, media lot, sample reference, RS/cryovial code, etc.
    [HttpPost("search")]
    public async Task<IActionResult> Search([FromBody] AuditSearchRequest request) =>
        Ok(ApiResponse<object>.Ok(await _auditSearchService.SearchPageAsync(request)));

    // CSV of the current filter, newest first, at most AuditExportService.MaxExportRows rows.
    // The export is itself recorded as an audit event before the file is streamed.
    [HttpPost("export")]
    public async Task Export([FromBody] AuditSearchRequest request, CancellationToken ct)
    {
        var plan = await _exportService.PrepareAsync(request, ct);
        Response.ContentType = "text/csv; charset=utf-8";
        Response.Headers.ContentDisposition = $"attachment; filename=\"audit-trail-{DateTime.UtcNow:yyyyMMddHHmmss}.csv\"";
        Response.Headers["X-Export-Row-Count"] = plan.RowCount.ToString();
        Response.Headers["X-Export-Truncated"] = plan.Truncated ? "true" : "false";
        await using var writer = new StreamWriter(Response.Body, new System.Text.UTF8Encoding(true), leaveOpen: true);
        await _exportService.WriteCsvAsync(plan, writer, ct);
        await writer.FlushAsync(ct);
    }

    // Dynamic record traceability graph built from real database relations
    [HttpGet("{auditLogId:int}/traceability")]
    public async Task<IActionResult> GetTraceability(int auditLogId)
    {
        var result = await _traceabilityService.GetTraceabilityAsync(auditLogId);
        if (result == null) return NotFound(ApiResponse<object>.Fail("Audit log or traceability target not found."));
        return Ok(ApiResponse<object>.Ok(result));
    }

    [HttpGet("traceability")]
    public async Task<IActionResult> GetTraceabilityForEntity([FromQuery] string entityName, [FromQuery] string entityId)
    {
        var result = await _traceabilityService.GetTraceabilityForEntityAsync(entityName, entityId);
        if (result == null) return NotFound(ApiResponse<object>.Fail("Traceability target not found."));
        return Ok(ApiResponse<object>.Ok(result));
    }

    // The security audit trail (authentication, session and account events) is a separate
    // permission from the GxP audit trail: Audit.View alone does not open it.
    [HttpGet("login-history")]
    [Authorize(Policy = PermissionConstants.SystemViewSecurityAudit)]
    public async Task<IActionResult> GetLoginHistory([FromQuery] string? username, [FromQuery] int take = 100, [FromQuery] int? beforeId = null) =>
        Ok(ApiResponse<object>.Ok(await _auditSearchService.GetLoginHistoryPageAsync(username, take, beforeId)));

    [HttpGet("security-events")]
    [Authorize(Policy = PermissionConstants.SystemViewSecurityAudit)]
    public async Task<IActionResult> GetSecurityEvents(
        [FromQuery] string? eventCode, [FromQuery] string? username, [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] int take = 100, [FromQuery] long? beforeId = null) =>
        Ok(ApiResponse<object>.Ok(await _auditSearchService.GetSecurityEventsAsync(eventCode, username, from, to, take, beforeId)));
}
