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
    private readonly AuditService _auditService;
    private readonly AuditSearchService _auditSearchService;
    private readonly AuditTraceabilityService _traceabilityService;
    public AuditController(
        AuditService auditService,
        AuditSearchService auditSearchService,
        AuditTraceabilityService traceabilityService)
    {
        _auditService = auditService;
        _auditSearchService = auditSearchService;
        _traceabilityService = traceabilityService;
    }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] string entityName, [FromQuery] string entityId) =>
        Ok(ApiResponse<object>.Ok(await _auditService.GetForEntityAsync(entityName, entityId)));

    // Search by every possible cross-reference - date range, batch,
    // control, media lot, sample reference, RS/cryovial code, etc.
    [HttpPost("search")]
    public async Task<IActionResult> Search([FromBody] AuditSearchRequest request) =>
        Ok(ApiResponse<object>.Ok(await _auditSearchService.SearchAsync(request)));

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

    [HttpGet("login-history")]
    public async Task<IActionResult> GetLoginHistory([FromQuery] string? username, [FromQuery] int take = 100) =>
        Ok(ApiResponse<object>.Ok(await _auditService.GetLoginHistoryAsync(username, take)));
}
