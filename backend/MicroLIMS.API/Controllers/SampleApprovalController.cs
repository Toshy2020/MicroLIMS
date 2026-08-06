using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

public record DecideSampleApprovalRequest(string Password, ApprovalDecision Decision, string? Comment);

// Sample-level approval, reached by clicking a Sample's lifecycle badge
// in the Testing Workspace rather than a standalone Approval page.
[ApiController]
[Route("api/samples/{id}/approval")]
[Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
public class SampleApprovalController : ControllerBase
{
    private readonly SampleApprovalService _approvalService;

    public SampleApprovalController(SampleApprovalService approvalService)
    {
        _approvalService = approvalService;
    }

    private int CurrentUserId => int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

    [HttpPost("decide")]
    public async Task<IActionResult> Decide(int id, DecideSampleApprovalRequest request)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        await _approvalService.DecideAsync(id, CurrentUserId, request.Password, request.Decision, request.Comment, ip);
        return Ok(ApiResponse<object>.Ok(new { }));
    }
}
