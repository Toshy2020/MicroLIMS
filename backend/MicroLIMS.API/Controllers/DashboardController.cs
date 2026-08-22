using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

    public DashboardController(DashboardService dashboardService, DashboardNotificationService notificationService, RecentActivityService activityService, MyTasksService myTasksService)
    {
        _dashboardService = dashboardService;
        _notificationService = notificationService;
        _activityService = activityService;
        _myTasksService = myTasksService;
    }

    private int CurrentUserId => int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
    private RoleType CurrentRole => Enum.Parse<RoleType>(User.FindFirst(System.Security.Claims.ClaimTypes.Role)!.Value);

    [HttpGet]
    public async Task<IActionResult> Get() => Ok(ApiResponse<object>.Ok(await _dashboardService.GetSummaryAsync(CurrentRole, CurrentUserId)));

    [HttpGet("kpi-deltas")]
    public async Task<IActionResult> GetKpiDeltas() => Ok(ApiResponse<object>.Ok(await _dashboardService.GetKpiDeltasAsync()));

    [HttpGet("monthly-trend")]
    public async Task<IActionResult> GetMonthlyTrend([FromQuery] int months = 6) => Ok(ApiResponse<object>.Ok(await _dashboardService.GetMonthlyTrendAsync(months)));

    [HttpGet("category-distribution")]
    public async Task<IActionResult> GetCategoryDistribution() => Ok(ApiResponse<object>.Ok(await _dashboardService.GetCategoryDistributionAsync()));

    [HttpGet("status-distribution")]
    public async Task<IActionResult> GetStatusDistribution() => Ok(ApiResponse<object>.Ok(await _dashboardService.GetStatusDistributionAsync()));

    [HttpGet("notifications")]
    public async Task<IActionResult> GetNotifications() => Ok(ApiResponse<object>.Ok(await _notificationService.GetNotificationsAsync(CurrentRole, CurrentUserId)));

    [HttpPost("notifications/{id}/read")]
    public async Task<IActionResult> MarkNotificationRead(int id)
    {
        await _notificationService.MarkAsReadAsync(id, CurrentUserId);
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
    public async Task<IActionResult> GetTodaysWork() => Ok(ApiResponse<object>.Ok(await _dashboardService.GetTodaysWorkAsync(CurrentRole, CurrentUserId)));

    [HttpGet("incubation-overview")]
    public async Task<IActionResult> GetIncubationOverview([FromQuery] bool myIncubationsOnly = false) =>
        Ok(ApiResponse<object>.Ok(await _dashboardService.GetIncubationOverviewAsync(myIncubationsOnly, CurrentUserId)));

    [HttpGet("section-head")]
    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    public async Task<IActionResult> GetSectionHeadDashboard() =>
        Ok(ApiResponse<object>.Ok(await _dashboardService.GetSectionHeadDashboardAsync()));

    [HttpGet("reviewer")]
    [Authorize(Roles = RoleConstants.Reviewer + "," + RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    public async Task<IActionResult> GetReviewerDashboard() =>
        Ok(ApiResponse<object>.Ok(await _dashboardService.GetReviewerDashboardAsync(CurrentUserId)));

    [HttpGet("analyst-metrics")]
    public async Task<IActionResult> GetAnalystMetrics()
    {
        if (CurrentRole != RoleType.Analyst) return Forbid();
        return Ok(ApiResponse<object>.Ok(await _dashboardService.GetAnalystMetricsAsync(CurrentUserId)));
    }
}
