using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Enums;
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

    public DashboardController(DashboardService dashboardService, DashboardNotificationService notificationService, RecentActivityService activityService)
    {
        _dashboardService = dashboardService;
        _notificationService = notificationService;
        _activityService = activityService;
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
}
