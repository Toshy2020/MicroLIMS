using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

public record ApprovalRequest(int TestOrderId, ApprovalDecision Decision, string? Comment, string Password);

// TestOrder-level approval - superseded by SampleApprovalController/
// SampleApprovalService for the main workflow (no nav entry reaches this
// anymore), but kept because ApprovalService is still exercised directly
// by SegregationOfDutiesTests.
[ApiController]
[Route("api/approval")]
[Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
public class ApprovalController : ControllerBase
{
    private readonly ApprovalService _approvalService;
    private readonly IUserSectionScopeService _scopeService;

    public ApprovalController(ApprovalService approvalService, IUserSectionScopeService scopeService)
    {
        _approvalService = approvalService;
        _scopeService = scopeService;
    }

    [HttpPost]
    public async Task<IActionResult> Decide(ApprovalRequest request)
    {
        var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        await _scopeService.EnsureTestOrderAccessAsync(userId, request.TestOrderId);
        var result = await _approvalService.DecideAsync(request.TestOrderId, request.Decision, request.Comment, userId, request.Password, ip);
        return Ok(ApiResponse<object>.Ok(result));
    }
}
