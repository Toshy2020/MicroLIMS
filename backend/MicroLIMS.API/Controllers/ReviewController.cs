using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.Services;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

public record ReviewRequest(int TestOrderId, string? Comment, string Password, ReviewMode Mode = ReviewMode.Detailed);
public record QuickReviewBatchRequest(List<int> TestOrderIds, string Password);
public record ReturnToAnalystRequest(int TestOrderId, string? Reason);

// TestOrder-level review - superseded by SampleReviewController/
// SampleReviewService for the main workflow (no nav entry reaches this
// anymore), but kept because ReviewService is still exercised directly
// by SegregationOfDutiesTests/ElectronicSignatureTests.
[ApiController]
[Route("api/review")]
[Authorize(Roles = RoleConstants.Reviewer + "," + RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
public class ReviewController : ControllerBase
{
    private readonly ReviewService _reviewService;

    public ReviewController(ReviewService reviewService)
    {
        _reviewService = reviewService;
    }

    [HttpPost]
    public async Task<IActionResult> Review(ReviewRequest request)
    {
        var reviewerId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        await _reviewService.MarkReviewedAsync(request.TestOrderId, reviewerId, request.Comment, request.Password, ip, request.Mode);
        return Ok(ApiResponse<object>.Ok(new { }));
    }

    [HttpPost("return")]
    public async Task<IActionResult> ReturnToAnalyst([FromBody] ReturnToAnalystRequest request)
    {
        var reviewerId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        var result = await _reviewService.ReturnToAnalystAsync(request.TestOrderId, reviewerId, request.Reason);
        return Ok(ApiResponse<object>.Ok(result));
    }

    // Quick table review - hybrid mode #2 from the spec.
    [HttpPost("batch")]
    public async Task<IActionResult> ReviewBatch(QuickReviewBatchRequest request)
    {
        var reviewerId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await _reviewService.QuickReviewBatchAsync(request.TestOrderIds, reviewerId, request.Password, ip);
        return Ok(ApiResponse<object>.Ok(new { reviewed = result.Reviewed, skipped = result.Skipped }));
    }
}
