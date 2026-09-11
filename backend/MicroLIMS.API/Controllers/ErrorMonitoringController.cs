using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Responses;
using System.Security.Claims;

namespace MicroLIMS.API.Controllers;

// The admin error monitoring page.
//
// Permission-gated rather than role-string gated: this is a new capability
// with no legacy [Authorize(Roles=...)] equivalent to reproduce, so it is
// the first endpoint in the codebase to be permission-only. The policy
// name resolves dynamically through PermissionPolicyProvider.
[ApiController]
[Route("api/error-monitoring")]
[Authorize(Policy = PermissionConstants.SystemViewErrorLog)]
public class ErrorMonitoringController : ControllerBase
{
    private readonly IErrorMonitoringService _service;

    public ErrorMonitoringController(IErrorMonitoringService service)
    {
        _service = service;
    }

    private int CurrentUserId =>
        int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;

    [HttpGet("incidents")]
    public async Task<IActionResult> Search(
        [FromQuery] ErrorSeverity? severity,
        [FromQuery] ErrorSource? source,
        [FromQuery] IncidentStatus? status,
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        var result = await _service.SearchAsync(
            new IncidentQuery(severity, source, status, fromUtc, toUtc, search, page, pageSize),
            cancellationToken);

        return Ok(ApiResponse<object>.Ok(result));
    }

    [HttpGet("incidents/{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var incident = await _service.GetAsync(id, cancellationToken);
        if (incident is null) return NotFound(ApiResponse<object>.Fail("Incident not found."));
        return Ok(ApiResponse<object>.Ok(incident));
    }

    [HttpPost("incidents/{id:guid}/resolve")]
    public async Task<IActionResult> Resolve(
        Guid id, [FromBody] ResolveIncidentRequest request, CancellationToken cancellationToken)
    {
        var ok = await _service.ResolveAsync(id, CurrentUserId, request.ResolutionNotes, cancellationToken);
        if (!ok) return NotFound(ApiResponse<object>.Fail("Incident not found."));
        return Ok(ApiResponse<object>.Ok(new { resolved = true }));
    }

    [HttpPost("incidents/{id:guid}/reopen")]
    public async Task<IActionResult> Reopen(Guid id, CancellationToken cancellationToken)
    {
        var ok = await _service.ReopenAsync(id, cancellationToken);
        if (!ok) return NotFound(ApiResponse<object>.Fail("Incident not found."));
        return Ok(ApiResponse<object>.Ok(new { reopened = true }));
    }

    [HttpPost("error-logs/{id:guid}/severity-override")]
    public async Task<IActionResult> OverrideSeverity(
        Guid id, [FromBody] SeverityOverrideRequest request, CancellationToken cancellationToken)
    {
        var ok = await _service.OverrideSeverityAsync(id, request.Severity, cancellationToken);
        if (!ok) return NotFound(ApiResponse<object>.Fail("Error log entry not found."));
        return Ok(ApiResponse<object>.Ok(new { overridden = request.Severity is not null }));
    }

    [HttpGet("incidents/export")]
    public async Task<IActionResult> Export(
        [FromQuery] ErrorSeverity? severity,
        [FromQuery] ErrorSource? source,
        [FromQuery] IncidentStatus? status,
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] string? search,
        CancellationToken cancellationToken = default)
    {
        var csv = await _service.ExportCsvAsync(
            new IncidentQuery(severity, source, status, fromUtc, toUtc, search),
            cancellationToken);

        var fileName = $"microlims-incidents-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv";
        return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", fileName);
    }
}
