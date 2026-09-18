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

    private readonly IUserSectionScopeService _scopeService;

    public DashboardController(DashboardService dashboardService, DashboardNotificationService notificationService, RecentActivityService activityService, MyTasksService myTasksService,
        IUserSectionScopeService scopeService)
    {
        _scopeService = scopeService;
        _dashboardService = dashboardService;
        _notificationService = notificationService;
        _activityService = activityService;
        _myTasksService = myTasksService;
    }

    private int CurrentUserId => int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

    // Every dashboard figure is limited to the caller's laboratory sections.
    private Task<IReadOnlyList<int>?> Scope() => _scopeService.GetAccessibleSectionIdsAsync(CurrentUserId);
    private RoleType CurrentRole => Enum.Parse<RoleType>(User.FindFirst(System.Security.Claims.ClaimTypes.Role)!.Value);

    [HttpGet]
    public async Task<IActionResult> Get() => Ok(ApiResponse<object>.Ok(await _dashboardService.GetSummaryAsync(CurrentRole, CurrentUserId, await Scope())));

    [HttpGet("kpi-deltas")]
    public async Task<IActionResult> GetKpiDeltas() => Ok(ApiResponse<object>.Ok(await _dashboardService.GetKpiDeltasAsync(await Scope())));

    [HttpGet("monthly-trend")]
    public async Task<IActionResult> GetMonthlyTrend([FromQuery] int months = 6) => Ok(ApiResponse<object>.Ok(await _dashboardService.GetMonthlyTrendAsync(months, await Scope())));

    [HttpGet("category-distribution")]
    public async Task<IActionResult> GetCategoryDistribution() => Ok(ApiResponse<object>.Ok(await _dashboardService.GetCategoryDistributionAsync(await Scope())));

    [HttpGet("status-distribution")]
    public async Task<IActionResult> GetStatusDistribution() => Ok(ApiResponse<object>.Ok(await _dashboardService.GetStatusDistributionAsync(await Scope())));

    [HttpGet("notifications")]
    public async Task<IActionResult> GetNotifications() => Ok(ApiResponse<object>.Ok(await _notificationService.GetNotificationsAsync(CurrentRole, CurrentUserId)));

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
    public async Task<IActionResult> GetRecentActivity([FromQuery] int take = 25) => Ok(ApiResponse<object>.Ok(await _activityService.GetRecentAsync(take)));

    // Analyst-only personal task list - not meaningful for lab-wide roles,
    // so it's rejected server-side rather than just hidden client-side.
    [HttpGet("my-tasks")]
    public async Task<IActionResult> GetMyTasks()
    {
        if (CurrentRole != RoleType.Analyst) return Forbid();
        return Ok(ApiResponse<object>.Ok(await _myTasksService.GetMyTasksAsync(CurrentUserId)));
    }

    [HttpGet("todays-work")]
    public async Task<IActionResult> GetTodaysWork() => Ok(ApiResponse<object>.Ok(await _dashboardService.GetTodaysWorkAsync(CurrentRole, CurrentUserId, await Scope())));

    [HttpGet("incubation-overview")]
    public async Task<IActionResult> GetIncubationOverview([FromQuery] bool myIncubationsOnly = false) =>
        Ok(ApiResponse<object>.Ok(await _dashboardService.GetIncubationOverviewAsync(myIncubationsOnly, CurrentUserId, await Scope())));

    [HttpGet("section-head")]
    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    public async Task<IActionResult> GetSectionHeadDashboard() =>
        Ok(ApiResponse<object>.Ok(await _dashboardService.GetSectionHeadDashboardAsync(await Scope())));

    [HttpGet("reviewer")]
    [Authorize(Roles = RoleConstants.Reviewer + "," + RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    public async Task<IActionResult> GetReviewerDashboard() =>
        Ok(ApiResponse<object>.Ok(await _dashboardService.GetReviewerDashboardAsync(CurrentUserId, await Scope())));

    [HttpGet("analyst-metrics")]
    public async Task<IActionResult> GetAnalystMetrics()
    {
        if (CurrentRole != RoleType.Analyst) return Forbid();
        return Ok(ApiResponse<object>.Ok(await _dashboardService.GetAnalystMetricsAsync(CurrentUserId)));
    }
}
