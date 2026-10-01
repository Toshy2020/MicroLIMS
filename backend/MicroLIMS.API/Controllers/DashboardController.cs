using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly DashboardService _dashboardService;
    private readonly DashboardNotificationService _notificationService;
    private readonly RecentActivityService _activityService;
    private readonly MyTasksService _myTasksService;

    private readonly DashboardLabScopeService _labScope;

    public DashboardController(DashboardService dashboardService, DashboardNotificationService notificationService, RecentActivityService activityService, MyTasksService myTasksService,
        DashboardLabScopeService labScope)
    {
        _labScope = labScope;
        _dashboardService = dashboardService;
        _notificationService = notificationService;
        _activityService = activityService;
        _myTasksService = myTasksService;
    }

    private int CurrentUserId => int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

    // Every dashboard figure is limited to the caller's laboratory sections,
    // and - when `lab` is given (MICRO / FP) - to that one laboratory, so a
    // Microbiology dashboard never counts Physicochemical work and vice
    // versa. Asking for a laboratory the caller does not belong to is a 403.
    private Task<DashboardLabScope> LabScope(string? lab) => _labScope.ResolveAsync(CurrentUserId, lab);
    private async Task<IReadOnlyList<int>?> Scope(string? lab) => (await LabScope(lab)).SectionIds;
    private RoleType CurrentRole => Enum.Parse<RoleType>(User.FindFirst(System.Security.Claims.ClaimTypes.Role)!.Value);

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] string? lab = null) => Ok(ApiResponse<object>.Ok(await _dashboardService.GetSummaryAsync(CurrentRole, CurrentUserId, await Scope(lab))));

    [HttpGet("kpi-deltas")]
    public async Task<IActionResult> GetKpiDeltas([FromQuery] string? lab = null) => Ok(ApiResponse<object>.Ok(await _dashboardService.GetKpiDeltasAsync(await Scope(lab))));

    [HttpGet("monthly-trend")]
    public async Task<IActionResult> GetMonthlyTrend([FromQuery] int months = 6, [FromQuery] string? lab = null) => Ok(ApiResponse<object>.Ok(await _dashboardService.GetMonthlyTrendAsync(months, await Scope(lab))));

    [HttpGet("category-distribution")]
    public async Task<IActionResult> GetCategoryDistribution([FromQuery] string? lab = null) => Ok(ApiResponse<object>.Ok(await _dashboardService.GetCategoryDistributionAsync(await Scope(lab))));

    [HttpGet("status-distribution")]
    public async Task<IActionResult> GetStatusDistribution([FromQuery] string? lab = null) => Ok(ApiResponse<object>.Ok(await _dashboardService.GetStatusDistributionAsync(await Scope(lab))));

    [HttpGet("notifications")]
    // Without `lab` (the header bell) every notification is returned; with it
    // (a laboratory dashboard) only that laboratory's are.
    public async Task<IActionResult> GetNotifications([FromQuery] string? lab = null) =>
        Ok(ApiResponse<object>.Ok(await _notificationService.GetNotificationsAsync(CurrentRole, CurrentUserId, await LabScope(lab))));

    [HttpPost("notifications/{id}/read")]
    public async Task<IActionResult> MarkNotificationRead(int id)
    {
        await _notificationService.MarkAsReadAsync(id, CurrentUserId);
        return Ok(ApiResponse<object>.Ok(new { }));
    }

    [HttpPost("notifications/read-all")]
    public async Task<IActionResult> MarkAllNotificationsRead()
    {
        await _notificationService.MarkAllAsReadAsync(CurrentUserId);
        return Ok(ApiResponse<object>.Ok(new { }));
    }

    [HttpGet("recent-activity")]
    public async Task<IActionResult> GetRecentActivity([FromQuery] int take = 25, [FromQuery] string? lab = null) => Ok(ApiResponse<object>.Ok(await _activityService.GetRecentAsync(take, await Scope(lab))));

    // Analyst-only personal task list - not meaningful for lab-wide roles,
    // so it's rejected server-side rather than just hidden client-side.
    [HttpGet("my-tasks")]
    public async Task<IActionResult> GetMyTasks([FromQuery] string? lab = null)
    {
        if (CurrentRole != RoleType.Analyst) return Forbid();
        return Ok(ApiResponse<object>.Ok(await _myTasksService.GetMyTasksAsync(CurrentUserId, await LabScope(lab))));
    }

    [HttpGet("todays-work")]
    public async Task<IActionResult> GetTodaysWork([FromQuery] string? lab = null) => Ok(ApiResponse<object>.Ok(await _dashboardService.GetTodaysWorkAsync(CurrentRole, CurrentUserId, await Scope(lab))));

    [HttpGet("incubation-overview")]
    public async Task<IActionResult> GetIncubationOverview([FromQuery] bool myIncubationsOnly = false, [FromQuery] string? lab = null) =>
        Ok(ApiResponse<object>.Ok(await _dashboardService.GetIncubationOverviewAsync(myIncubationsOnly, CurrentUserId, await Scope(lab))));

    [HttpGet("section-head")]
    [Authorize(Policy = PermissionConstants.DashboardsLabOverview)]
    public async Task<IActionResult> GetSectionHeadDashboard([FromQuery] string? lab = null) =>
        Ok(ApiResponse<object>.Ok(await _dashboardService.GetSectionHeadDashboardAsync(await Scope(lab))));

    [HttpGet("reviewer")]
    [Authorize(Policy = PermissionConstants.DashboardsReview)]
    public async Task<IActionResult> GetReviewerDashboard([FromQuery] string? lab = null) =>
        Ok(ApiResponse<object>.Ok(await _dashboardService.GetReviewerDashboardAsync(CurrentUserId, await Scope(lab))));

    [HttpGet("analyst-metrics")]
    public async Task<IActionResult> GetAnalystMetrics([FromQuery] string? lab = null)
    {
        if (CurrentRole != RoleType.Analyst) return Forbid();
        return Ok(ApiResponse<object>.Ok(await _dashboardService.GetAnalystMetricsAsync(CurrentUserId, await LabScope(lab))));
    }
}
