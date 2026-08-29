using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

// CertificateRemarks is only meaningful/used when Decision == Approve
// (SampleApprovalService ignores it otherwise) - a separate, explicitly
// named field from Comment, never auto-populated from it.
// SelectedTestOrderIds is required for RetestRetainedSample/NewSampleRequest
// only - the tests the Section Head chose to retest. NewSampleAnalystOneId/
// TwoId are required for NewSampleRequest only - the two analysts for the
// two new samples (must differ from each other and from whoever tested
// the original sample; enforced server-side in SampleApprovalService).
public record DecideSampleApprovalRequest(
    string Password, ApprovalDecision Decision, string? Comment, string? CertificateRemarks = null,
    List<int>? SelectedTestOrderIds = null, int? NewSampleAnalystOneId = null, int? NewSampleAnalystTwoId = null);

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
        await _approvalService.DecideAsync(id, CurrentUserId, request.Password, request.Decision, request.Comment, ip,
            request.CertificateRemarks, request.SelectedTestOrderIds, request.NewSampleAnalystOneId, request.NewSampleAnalystTwoId);
        return Ok(ApiResponse<object>.Ok(new { }));
    }
}
