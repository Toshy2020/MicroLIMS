using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

public record ApprovalRequest(int TestOrderId, ApprovalDecision Decision, string? Comment);

[ApiController]
[Route("api/approval")]
[Authorize(Roles = RoleConstants.SectionHead)]
public class ApprovalController : ControllerBase
{
    private readonly ApprovalService _approvalService;

    public ApprovalController(ApprovalService approvalService)
    {
        _approvalService = approvalService;
    }

    [HttpPost]
    public async Task<IActionResult> Decide(ApprovalRequest request)
    {
        var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        var result = await _approvalService.DecideAsync(request.TestOrderId, request.Decision, request.Comment, userId);
        return Ok(ApiResponse<object>.Ok(result));
    }
}
