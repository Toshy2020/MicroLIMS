using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Enums;
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
    public async Task<IActionResult> GetAnalystKpis(
        [FromQuery] SampleCategory? category,
        [FromQuery] string? location,
        [FromQuery] string? testCode) =>
        Ok(ApiResponse<object>.Ok(await _kpiService.GetAnalystKpisAsync(category, location, testCode)));

    [HttpGet("workload-weights")]
    public async Task<IActionResult> GetWorkloadWeights() =>
        Ok(ApiResponse<object>.Ok(await _kpiService.GetWorkloadWeightsAsync()));

    [HttpPut("workload-weights/{testCode}")]
    public async Task<IActionResult> UpdateWorkloadWeight(
        [FromRoute] string testCode,
        [FromBody] UpdateWorkloadWeightRequest request)
    {
        var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        var userName = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? "Section Head";
        var result = await _kpiService.UpdateWorkloadWeightAsync(
            testCode, request.WorkloadWeight, request.ReasonForChange, userId, userName);
        return Ok(ApiResponse<object>.Ok(result));
    }

    [HttpGet("completion-stats")]
    public async Task<IActionResult> GetCompletionStats(
        [FromQuery] SampleCategory? category,
        [FromQuery] string? location,
        [FromQuery] string? testCode) =>
        Ok(ApiResponse<object>.Ok(await _kpiService.GetCompletionStatsAsync(category, location, testCode)));

    [HttpGet("delay-tracking")]
    public async Task<IActionResult> GetDelayTracking(
        [FromQuery] SampleCategory? category,
        [FromQuery] string? location,
        [FromQuery] string? testCode) =>
        Ok(ApiResponse<object>.Ok(await _kpiService.GetDelayTrackingAsync(category, location, testCode)));

    [HttpGet("sample-queue-counts")]
    public async Task<IActionResult> GetSampleQueueCounts(
        [FromQuery] SampleCategory? category,
        [FromQuery] string? location,
        [FromQuery] string? testCode) =>
        Ok(ApiResponse<object>.Ok(await _kpiService.GetSampleQueueCountsAsync(category, location, testCode)));

    [HttpGet("sample-assignment-sla")]
    public async Task<IActionResult> GetSampleAssignmentSla(
        [FromQuery] int? analystId,
        [FromQuery] DateTime fromDate,
        [FromQuery] DateTime toDate,
        [FromQuery] SampleCategory? category,
        [FromQuery] string? location,
        [FromQuery] string? testCode) =>
        Ok(ApiResponse<object>.Ok(await _kpiService.GetSampleAssignmentSlaAsync(analystId, fromDate, toDate, category, location, testCode)));

    [HttpGet("step-violations")]
    public async Task<IActionResult> GetStepViolations(
        [FromQuery] int? analystId,
        [FromQuery] DateTime fromDate,
        [FromQuery] DateTime toDate,
        [FromQuery] SampleCategory? category,
        [FromQuery] string? location,
        [FromQuery] string? testCode) =>
        Ok(ApiResponse<object>.Ok(await _kpiService.GetStepViolationsAsync(analystId, fromDate, toDate, category, location, testCode)));

    [HttpGet("sample-assignment-sla-by-analyst")]
    public async Task<IActionResult> GetSampleAssignmentSlaByAnalyst(
        [FromQuery] DateTime fromDate,
        [FromQuery] DateTime toDate,
        [FromQuery] SampleCategory? category,
        [FromQuery] string? location,
        [FromQuery] string? testCode) =>
        Ok(ApiResponse<object>.Ok(await _kpiService.GetSampleAssignmentSlaByAnalystAsync(fromDate, toDate, category, location, testCode)));

    [HttpGet("workflow-bottleneck-deltas")]
    public async Task<IActionResult> GetWorkflowBottleneckDeltas(
        [FromQuery] SampleCategory? category,
        [FromQuery] string? location,
        [FromQuery] string? testCode) =>
        Ok(ApiResponse<object>.Ok(await _kpiService.GetWorkflowBottleneckDeltasAsync(category, location, testCode)));

    [HttpGet("overall-on-time-completion")]
    public async Task<IActionResult> GetOverallOnTimeCompletion(
        [FromQuery] DateTime fromDate,
        [FromQuery] DateTime toDate,
        [FromQuery] SampleCategory? category,
        [FromQuery] string? location,
        [FromQuery] string? testCode) =>
        Ok(ApiResponse<object>.Ok(await _kpiService.GetOverallOnTimeCompletionAsync(fromDate, toDate, category, location, testCode)));

    [HttpGet("overall-on-time-completion-by-analyst")]
    public async Task<IActionResult> GetOverallOnTimeCompletionByAnalyst(
        [FromQuery] DateTime fromDate,
        [FromQuery] DateTime toDate,
        [FromQuery] SampleCategory? category,
        [FromQuery] string? location,
        [FromQuery] string? testCode) =>
        Ok(ApiResponse<object>.Ok(await _kpiService.GetOverallOnTimeCompletionByAnalystAsync(fromDate, toDate, category, location, testCode)));

    [HttpGet("stage-tat-summary")]
    public async Task<IActionResult> GetStageTatSummary(
        [FromQuery] int? analystId,
        [FromQuery] DateTime fromDate,
        [FromQuery] DateTime toDate,
        [FromQuery] SampleCategory? category,
        [FromQuery] string? location,
        [FromQuery] string? testCode) =>
        Ok(ApiResponse<object>.Ok(await _kpiService.GetStageTatSummaryAsync(analystId, fromDate, toDate, category, location, testCode)));

    [HttpGet("testing-tat-by-month")]
    public async Task<IActionResult> GetTestingTatByMonth(
        [FromQuery] int months = 6,
        [FromQuery] SampleCategory? category = null,
        [FromQuery] string? location = null,
        [FromQuery] string? testCode = null) =>
        Ok(ApiResponse<object>.Ok(await _kpiService.GetTestingTatByMonthAsync(months, category, location, testCode)));

    [HttpGet("return-to-analyst-count")]
    public async Task<IActionResult> GetReturnToAnalystCount(
        [FromQuery] int? analystId,
        [FromQuery] DateTime fromDate,
        [FromQuery] DateTime toDate) =>
        Ok(ApiResponse<object>.Ok(await _kpiService.GetReturnToAnalystCountAsync(analystId, fromDate, toDate)));
}
