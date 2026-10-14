using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.Services.Kpi;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

// Analyst workload, capacity settings and availability (KPI remodel S3). Everyone with Kpi.View may call the reads,
// but the services return only the caller's own figures unless the JWT carries Kpi.ViewTeam (D8). Writes also need
// Kpi.ConfigureProfiles; the two policies intersect.
[ApiController]
[Route("api/kpi")]
[Authorize(Policy = PermissionConstants.KpiView)]
public class KpiWorkloadController : ControllerBase
{
    private readonly AnalystWorkloadService _workload;
    private readonly KpiSettingsService _settings;
    private readonly AnalystAvailabilityService _availability;

    public KpiWorkloadController(AnalystWorkloadService workload, KpiSettingsService settings, AnalystAvailabilityService availability)
    {
        _workload = workload;
        _settings = settings;
        _availability = availability;
    }

    private int CurrentUserId => int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
    private bool CanViewTeam => User.HasClaim("permission", PermissionConstants.KpiViewTeam);
    private CancellationToken Ct => HttpContext.RequestAborted;

    [HttpGet("workload")]
    public async Task<IActionResult> GetWorkload(
        [FromQuery] DateOnly from, [FromQuery] DateOnly to, [FromQuery] string? category,
        [FromQuery] string? location, [FromQuery] string? testCode, [FromQuery] int? analystId)
    {
        var q = await _workload.BuildQueryAsync(CurrentUserId, CanViewTeam, from, to, category, location, testCode, analystId, Ct);
        return Ok(ApiResponse<object>.Ok(await _workload.GetAsync(q, Ct)));
    }

    [HttpGet("workload/tests")]
    public async Task<IActionResult> GetWorkloadTests(
        [FromQuery] DateOnly from, [FromQuery] DateOnly to, [FromQuery] string? category,
        [FromQuery] string? location, [FromQuery] string? testCode, [FromQuery] int? analystId,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        var q = await _workload.BuildQueryAsync(CurrentUserId, CanViewTeam, from, to, category, location, testCode, analystId, Ct);
        return Ok(ApiResponse<object>.Ok(await _workload.GetTestsAsync(q, page, pageSize, Ct)));
    }

    [HttpGet("settings/{sectionId:int}")]
    public async Task<IActionResult> GetSettings(int sectionId) =>
        Ok(ApiResponse<object>.Ok(await _settings.GetAsync(sectionId, CurrentUserId, Ct)));

    [Authorize(Policy = PermissionConstants.KpiConfigureProfiles)]
    [HttpPut("settings/{sectionId:int}")]
    public async Task<IActionResult> UpdateSettings(int sectionId, [FromBody] UpdateKpiSettingsRequest request) =>
        Ok(ApiResponse<object>.Ok(await _settings.UpdateAsync(sectionId, request, CurrentUserId, Ct)));

    [HttpGet("availability")]
    public async Task<IActionResult> ListAvailability([FromQuery] int? userId, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to) =>
        Ok(ApiResponse<object>.Ok(await _availability.ListAsync(CurrentUserId, CanViewTeam, userId, from, to, Ct)));

    [Authorize(Policy = PermissionConstants.KpiConfigureProfiles)]
    [HttpPost("availability")]
    public async Task<IActionResult> CreateAvailability([FromBody] SaveAvailabilityRequest request) =>
        Ok(ApiResponse<object>.Ok(await _availability.CreateAsync(request, CurrentUserId, Ct)));

    [Authorize(Policy = PermissionConstants.KpiConfigureProfiles)]
    [HttpPut("availability/{id:int}")]
    public async Task<IActionResult> UpdateAvailability(int id, [FromBody] SaveAvailabilityRequest request) =>
        Ok(ApiResponse<object>.Ok(await _availability.UpdateAsync(id, request, CurrentUserId, Ct)));
}
