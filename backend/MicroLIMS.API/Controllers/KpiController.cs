using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.Services;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

[ApiController]
[Route("api/kpi")]
[Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
public class KpiController : ControllerBase
{
    private readonly KpiService _kpiService;

    public KpiController(KpiService kpiService)
    {
        _kpiService = kpiService;
    }

    [HttpGet("analysts")]
    public async Task<IActionResult> GetAnalystKpis() => Ok(ApiResponse<object>.Ok(await _kpiService.GetAnalystKpisAsync()));

    [HttpGet("completion-stats")]
    public async Task<IActionResult> GetCompletionStats() => Ok(ApiResponse<object>.Ok(await _kpiService.GetCompletionStatsAsync()));

    [HttpGet("delay-tracking")]
    public async Task<IActionResult> GetDelayTracking() => Ok(ApiResponse<object>.Ok(await _kpiService.GetDelayTrackingAsync()));

    [HttpGet("sample-queue-counts")]
    public async Task<IActionResult> GetSampleQueueCounts() => Ok(ApiResponse<object>.Ok(await _kpiService.GetSampleQueueCountsAsync()));

    [HttpGet("sample-assignment-sla")]
    public async Task<IActionResult> GetSampleAssignmentSla([FromQuery] int? analystId, [FromQuery] DateTime fromDate, [FromQuery] DateTime toDate) =>
        Ok(ApiResponse<object>.Ok(await _kpiService.GetSampleAssignmentSlaAsync(analystId, fromDate, toDate)));

    [HttpGet("step-violations")]
    public async Task<IActionResult> GetStepViolations([FromQuery] int? analystId, [FromQuery] DateTime fromDate, [FromQuery] DateTime toDate) =>
        Ok(ApiResponse<object>.Ok(await _kpiService.GetStepViolationsAsync(analystId, fromDate, toDate)));

    [HttpGet("sample-assignment-sla-by-analyst")]
    public async Task<IActionResult> GetSampleAssignmentSlaByAnalyst([FromQuery] DateTime fromDate, [FromQuery] DateTime toDate) =>
        Ok(ApiResponse<object>.Ok(await _kpiService.GetSampleAssignmentSlaByAnalystAsync(fromDate, toDate)));

    [HttpGet("workflow-bottleneck-deltas")]
    public async Task<IActionResult> GetWorkflowBottleneckDeltas() =>
        Ok(ApiResponse<object>.Ok(await _kpiService.GetWorkflowBottleneckDeltasAsync()));

    [HttpGet("overall-on-time-completion")]
    public async Task<IActionResult> GetOverallOnTimeCompletion([FromQuery] DateTime fromDate, [FromQuery] DateTime toDate) =>
        Ok(ApiResponse<object>.Ok(await _kpiService.GetOverallOnTimeCompletionAsync(fromDate, toDate)));

    [HttpGet("overall-on-time-completion-by-analyst")]
    public async Task<IActionResult> GetOverallOnTimeCompletionByAnalyst([FromQuery] DateTime fromDate, [FromQuery] DateTime toDate) =>
        Ok(ApiResponse<object>.Ok(await _kpiService.GetOverallOnTimeCompletionByAnalystAsync(fromDate, toDate)));

    [HttpGet("stage-tat-summary")]
    public async Task<IActionResult> GetStageTatSummary([FromQuery] int? analystId, [FromQuery] DateTime fromDate, [FromQuery] DateTime toDate) =>
        Ok(ApiResponse<object>.Ok(await _kpiService.GetStageTatSummaryAsync(analystId, fromDate, toDate)));

    [HttpGet("testing-tat-by-month")]
    public async Task<IActionResult> GetTestingTatByMonth([FromQuery] int months = 6) =>
        Ok(ApiResponse<object>.Ok(await _kpiService.GetTestingTatByMonthAsync(months)));
}
