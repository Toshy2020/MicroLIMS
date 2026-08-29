using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.Services;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

public record CompleteSampleReviewRequest(string Password, string? Comment);
public record ReturnTestToAnalystRequest(int TestOrderId, string? Reason);

// Sample-level review, reached by clicking a Sample's lifecycle badge in
// the Testing Workspace rather than a standalone Review page.
[ApiController]
[Route("api/samples/{id}/review")]
[Authorize(Roles = RoleConstants.Reviewer + "," + RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
public class SampleReviewController : ControllerBase
{
    private readonly SampleReviewService _reviewService;
    private readonly ReviewService _testOrderReviewService;

    public SampleReviewController(SampleReviewService reviewService, ReviewService testOrderReviewService)
    {
        _reviewService = reviewService;
        _testOrderReviewService = testOrderReviewService;
    }

    private int CurrentUserId => int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

    [HttpPost("complete")]
    public async Task<IActionResult> Complete(int id, CompleteSampleReviewRequest request)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        await _reviewService.CompleteReviewAsync(id, CurrentUserId, request.Password, request.Comment, ip);
        return Ok(ApiResponse<object>.Ok(new { }));
    }

    // Returns one test order on this sample to the analyst for revision
    // (count tests only - see ReviewService.ReturnToAnalystAsync). The
    // `id` route param (sample id) isn't needed by the service itself
    // (the transition is entirely TestOrderId-scoped, including reverting
    // the parent Sample's own status if needed) - kept on this route
    // because that's where a reviewer actually reaches this action from
    // (SampleSummaryDialog.tsx's per-test view), not api/review's now-dead
    // standalone ReviewPage.
    [HttpPost("return-test")]
    public async Task<IActionResult> ReturnTestToAnalyst(int id, ReturnTestToAnalystRequest request)
    {
        var result = await _testOrderReviewService.ReturnToAnalystAsync(request.TestOrderId, CurrentUserId, request.Reason);
        return Ok(ApiResponse<object>.Ok(result));
    }
}
