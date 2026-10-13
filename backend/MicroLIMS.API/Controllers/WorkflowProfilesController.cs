using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.Services.Kpi;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

// Workflow profiles (KPI remodel S1). Reads need Kpi.View; every write also needs
// Kpi.ConfigureProfiles - the two policies intersect, so a writer needs both.
// A Draft is signed with If-Match: "<version>" (RecordVersionMiddleware).
[ApiController]
[Route("api/kpi/workflow-profiles")]
[Authorize(Policy = PermissionConstants.KpiView)]
public class WorkflowProfilesController : ControllerBase
{
    private readonly WorkflowProfileService _service;

    public WorkflowProfilesController(WorkflowProfileService service) => _service = service;

    private int CurrentUserId => int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
    private string? ClientIp => HttpContext.Connection.RemoteIpAddress?.ToString();

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] WorkflowType? workflowType,
        [FromQuery] string? testCode,
        [FromQuery] bool defaultOnly = false,
        [FromQuery] WorkflowProfileStatus? status = null) =>
        Ok(ApiResponse<object>.Ok(await _service.ListAsync(workflowType, testCode, defaultOnly, status)));

    [HttpGet("overview")]
    public async Task<IActionResult> Overview() =>
        Ok(ApiResponse<object>.Ok(await _service.GetOverviewAsync()));

    [HttpGet("coverage")]
    public async Task<IActionResult> Coverage() =>
        Ok(ApiResponse<object>.Ok(await _service.GetCoverageAsync()));

    [Authorize(Policy = PermissionConstants.KpiConfigureProfiles)]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateWorkflowProfileVersionRequest request) =>
        Ok(ApiResponse<object>.Ok(await _service.AddVersionAsync(request, CurrentUserId, ClientIp)));

    [Authorize(Policy = PermissionConstants.KpiConfigureProfiles)]
    [HttpPost("{id:int}/sign")]
    public async Task<IActionResult> Sign(int id, [FromBody] SignWorkflowProfileDraftRequest request) =>
        Ok(ApiResponse<object>.Ok(await _service.SignDraftAsync(id, request, CurrentUserId, ClientIp)));
}
