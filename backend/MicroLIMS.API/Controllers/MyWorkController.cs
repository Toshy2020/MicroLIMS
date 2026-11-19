using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.MyWork;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

// What is waiting for the signed-in user. The user is always the authenticated
// caller (never a route or query value); each source enforces section scope.
[ApiController]
[Route("api/my-work")]
[Authorize]
public class MyWorkController : ControllerBase
{
    private readonly IMyWorkService _service;
    private readonly DashboardLabScopeService _labScope;

    public MyWorkController(IMyWorkService service, DashboardLabScopeService labScope)
    {
        _service = service;
        _labScope = labScope;
    }

    private int CurrentUserId =>
        int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] string? lab, CancellationToken cancellationToken)
    {
        var scope = string.IsNullOrWhiteSpace(lab) ? null : await _labScope.ResolveAsync(CurrentUserId, lab, cancellationToken);
        var response = await _service.GetAsync(CurrentUserId, scope, cancellationToken);
        return Ok(ApiResponse<MyWorkResponse>.Ok(response));
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary(CancellationToken cancellationToken)
    {
        var summary = await _service.GetSummaryAsync(CurrentUserId, cancellationToken);
        return Ok(ApiResponse<MyWorkSummary>.Ok(summary));
    }
}
