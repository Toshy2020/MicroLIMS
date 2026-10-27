using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.Services.BackgroundJobs;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Responses;
using System.Security.Claims;

namespace MicroLIMS.API.Controllers;

// Background job status, run history and manual runs of housekeeping jobs.
// GxP jobs are status-only: POST run answers 409 for them.
[ApiController]
[Route("api/admin/jobs")]
[Authorize(Policy = PermissionConstants.SystemManageJobs)]
public class AdminJobsController : ControllerBase
{
    private readonly IBackgroundJobAdminService _service;

    public AdminJobsController(IBackgroundJobAdminService service) => _service = service;

    private int CurrentUserId =>
        int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken) =>
        Ok(ApiResponse<object>.Ok(await _service.ListAsync(cancellationToken)));

    [HttpGet("{name}/runs")]
    public async Task<IActionResult> Runs(
        string name, [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default) =>
        Ok(ApiResponse<object>.Ok(await _service.GetRunsAsync(name, page, pageSize, cancellationToken)));

    [HttpPost("{name}/run")]
    public async Task<IActionResult> Run(
        string name, [FromBody] RunBackgroundJobRequest request, CancellationToken cancellationToken)
    {
        var result = await _service.RunManuallyAsync(name, CurrentUserId, request.Reason, cancellationToken);
        return result.Status switch
        {
            ManualJobRunStatus.Completed => Ok(ApiResponse<object>.Ok(result.Run!, result.Message)),
            _ => Conflict(ApiResponse<object>.Fail(result.Message))
        };
    }
}
