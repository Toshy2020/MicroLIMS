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
// SectionId: which laboratory section's tests are being decided. Optional -
// needed only when more than one section of the sample is under approval.
public record DecideSampleApprovalRequest(
    string Password, ApprovalDecision Decision, string? Comment, string? CertificateRemarks = null,
    List<int>? SelectedTestOrderIds = null, int? NewSampleAnalystOneId = null, int? NewSampleAnalystTwoId = null,
    int? SectionId = null);

// Closing testing: the reason a laboratory chose to stop rather than
// finish its own tests after another laboratory already rejected the
// sample.
public record CloseSectionTestingRequest(string Password, string Reason);

// Sample-level approval, reached by clicking a Sample's lifecycle badge
// in the Testing Workspace rather than a standalone Approval page.
[ApiController]
[Route("api/samples/{id}/approval")]
[Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
public class SampleApprovalController : ControllerBase
{
    private readonly SampleApprovalService _approvalService;
    private readonly SectionClosureService _closureService;

    public SampleApprovalController(SampleApprovalService approvalService, SectionClosureService closureService)
    {
        _approvalService = approvalService;
        _closureService = closureService;
    }

    private int CurrentUserId => int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

    [HttpPost("decide")]
    public async Task<IActionResult> Decide(int id, DecideSampleApprovalRequest request)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        await _approvalService.DecideAsync(id, CurrentUserId, request.Password, request.Decision, request.Comment, ip,
            request.CertificateRemarks, request.SelectedTestOrderIds, request.NewSampleAnalystOneId, request.NewSampleAnalystTwoId,
            request.SectionId);
        return Ok(ApiResponse<object>.Ok(new { }));
    }

    [HttpPost("sections/{sectionId:int}/close")]
    public async Task<IActionResult> CloseTesting(int id, int sectionId, CloseSectionTestingRequest request)
    {
        await _closureService.CloseTestingAsync(id, sectionId, CurrentUserId, request.Password, request.Reason,
            HttpContext.Connection.RemoteIpAddress?.ToString());
        return Ok(ApiResponse<object>.Ok(new { }));
    }
}
