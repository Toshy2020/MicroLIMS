using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

[ApiController]
[Route("api/admin/audit")]
[Authorize(Roles = RoleConstants.SystemAdministrator + "," + RoleConstants.SectionHead)]
public class AuditController : ControllerBase
{
    private readonly AuditService _auditService;
    private readonly AuditSearchService _auditSearchService;
    private readonly MicroLimsDbContext _db;

    public AuditController(AuditService auditService, AuditSearchService auditSearchService, MicroLimsDbContext db)
    {
        _auditService = auditService;
        _auditSearchService = auditSearchService;
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] string entityName, [FromQuery] string entityId) =>
        Ok(ApiResponse<object>.Ok(await _auditService.GetForEntityAsync(entityName, entityId)));

    // Search by every possible cross-reference - date range, batch,
    // control, media lot, sample reference, RS/cryovial code, etc.
    [HttpPost("search")]
    public async Task<IActionResult> Search([FromBody] AuditSearchRequest request) =>
        Ok(ApiResponse<object>.Ok(await _auditSearchService.SearchAsync(request)));

    [HttpGet("login-history")]
    public async Task<IActionResult> GetLoginHistory([FromQuery] string? username, [FromQuery] int take = 100)
    {
        var query = _db.LoginHistories.AsQueryable();
        if (!string.IsNullOrWhiteSpace(username)) query = query.Where(l => l.Username == username);
        var history = await query.OrderByDescending(l => l.Timestamp).Take(take).ToListAsync();
        return Ok(ApiResponse<object>.Ok(history));
    }
}
