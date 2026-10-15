using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.Services.Kpi;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

// The user id and the Kpi.ViewTeam grant come from the token. The service enforces scope and D8; nothing here
// trusts a query string.
[ApiController]
[Route("api/kpi")]
[Authorize(Policy = PermissionConstants.KpiView)]
public class KpiDashboardController : ControllerBase
{
    private readonly KpiDashboardService _dashboard;

    public KpiDashboardController(KpiDashboardService dashboard) => _dashboard = dashboard;

    private int CurrentUserId => int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
    private bool CanViewTeam => User.HasClaim("permission", PermissionConstants.KpiViewTeam);

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard(
        [FromQuery] DateOnly from, [FromQuery] DateOnly to,
        [FromQuery] string? category, [FromQuery] string? location, [FromQuery] string? testCode,
        [FromQuery] int? analystId, CancellationToken ct) =>
        Ok(ApiResponse<object>.Ok(await _dashboard.GetDashboardAsync(
            CurrentUserId, CanViewTeam, from, to, category, location, testCode, analystId, ct)));

    [HttpGet("{key}/records")]
    public async Task<IActionResult> GetRecords(
        [FromRoute] string key, [FromQuery] DateOnly from, [FromQuery] DateOnly to,
        [FromQuery] string? category, [FromQuery] string? location, [FromQuery] string? testCode,
        [FromQuery] int? analystId, [FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        CancellationToken ct = default) =>
        Ok(ApiResponse<object>.Ok(await _dashboard.GetRecordsAsync(
            key, CurrentUserId, CanViewTeam, from, to, category, location, testCode, analystId, page, pageSize, ct)));

    [HttpGet("definitions")]
    public async Task<IActionResult> GetDefinitions([FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken ct) =>
        Ok(ApiResponse<object>.Ok(await _dashboard.GetDefinitionsAsync(from, to, ct)));
}
