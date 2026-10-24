using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services.Spc;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

// Control charts, process capability and trend-based limit review over
// the ResultRecord projection. Reads are open to every authenticated role
// (scoped to the caller's laboratory sections, like the rest of
// Reporting); replacing a Water / EM configuration's alert and action
// limits needs MasterData.Manage and an electronic signature.
[ApiController]
[Route("api/reporting/control-chart")]
[Authorize]
public class ControlChartController : ControllerBase
{
    // 60 charts at a few hundred KB of PNG each, base64-encoded.
    private const long MaxReportRequestBytes = 120L * 1024 * 1024;

    private readonly ControlChartService _service;
    private readonly ControlChartReportService _reports;
    private readonly IUserSectionScopeService _scopeService;

    public ControlChartController(ControlChartService service, ControlChartReportService reports, IUserSectionScopeService scopeService)
    {
        _service = service;
        _reports = reports;
        _scopeService = scopeService;
    }

    private int CurrentUserId => int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
    private string? ClientIp => HttpContext.Connection.RemoteIpAddress?.ToString();
    private Task<IReadOnlyList<int>?> Scope() => _scopeService.GetAccessibleSectionIdsAsync(CurrentUserId);

    [HttpGet]
    public async Task<IActionResult> GetChart(
        [FromQuery] string testCode,
        [FromQuery] string subjectName,
        [FromQuery] SampleCategory? category,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] DateTime? calculationFrom,
        [FromQuery] DateTime? calculationTo,
        [FromQuery] bool approvedOnly,
        [FromQuery] SettingsParameters settings,
        [FromQuery] CapabilityBasis capabilityBasis = CapabilityBasis.SpecLimit,
        [FromQuery] double? customLsl = null,
        [FromQuery] double? customUsl = null) =>
        Ok(ApiResponse<object>.Ok(await _service.GetChartAsync(
            new ControlChartQuery(testCode, subjectName, category, fromDate, toDate, calculationFrom, calculationTo, approvedOnly,
                settings.ToSettings(), capabilityBasis, customLsl, customUsl),
            await Scope())));

    [HttpGet("limit-review")]
    public async Task<IActionResult> GetLimitReview(
        [FromQuery] string testCode,
        [FromQuery] SampleCategory category,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] bool approvedOnly,
        [FromQuery] SettingsParameters settings) =>
        Ok(ApiResponse<object>.Ok(await _service.GetLimitReviewAsync(
            new LimitReviewQuery(testCode, category, fromDate, toDate, approvedOnly, settings.ToSettings()),
            await Scope())));

    [Authorize(Policy = PermissionConstants.MasterDataManage)]
    [HttpPost("apply-limits")]
    public async Task<IActionResult> ApplyLimits([FromBody] ApplyTrendLimitsRequest request) =>
        Ok(ApiResponse<object>.Ok(await _service.ApplyLimitsAsync(request, CurrentUserId, ClientIp, await Scope())));

    // One PDF or Word document from many charts. Read-only on the data
    // (same scope as the charts themselves), logged as a data export.
    [HttpPost("report")]
    [RequestSizeLimit(MaxReportRequestBytes)]
    public async Task<IActionResult> GenerateReport([FromBody] ControlChartReportRequest request)
    {
        var report = await _reports.GenerateAsync(request, CurrentUserId, await Scope());
        return File(report.Content, report.ContentType, report.FileName);
    }

    // Flat query-string form of ControlChartSettings; rules as "1,2,5".
    public class SettingsParameters
    {
        public LimitMethod Method { get; set; } = LimitMethod.MovingRange;
        public double AlertK { get; set; } = 2;
        public double ActionK { get; set; } = 3;
        public double AlertPercentile { get; set; } = 95;
        public double ActionPercentile { get; set; } = 99;
        public int MovingRangeLength { get; set; } = 2;
        public LimitRounding Rounding { get; set; } = LimitRounding.None;
        public int MinimumPoints { get; set; } = 30;
        public string? Rules { get; set; }
        public bool ExcludeOutOfSpec { get; set; } = true;

        public ControlChartSettings ToSettings()
        {
            int[]? rules = null;
            if (Rules is not null)
            {
                rules = Rules.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(r => int.TryParse(r, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)
                        ? n
                        : throw new InvalidOperationException($"'{r}' is not a control chart rule number."))
                    .Distinct()
                    .ToArray();
            }
            return new ControlChartSettings(Method, AlertK, ActionK, AlertPercentile, ActionPercentile, MovingRangeLength,
                Rounding, MinimumPoints, rules, ExcludeOutOfSpec);
        }
    }
}
