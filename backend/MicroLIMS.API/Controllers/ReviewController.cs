using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.Services;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

public record ReviewRequest(int TestOrderId, string? Comment, ReviewMode Mode = ReviewMode.Detailed);
public record QuickReviewBatchRequest(List<int> TestOrderIds);

[ApiController]
[Route("api/review")]
[Authorize(Roles = RoleConstants.Reviewer + "," + RoleConstants.SectionHead)]
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
        await _reviewService.MarkReviewedAsync(request.TestOrderId, reviewerId, request.Comment, request.Mode);
        return Ok(ApiResponse<object>.Ok(new { }));
    }

    // Quick table review - hybrid mode #2 from the spec.
    [HttpPost("batch")]
    public async Task<IActionResult> ReviewBatch(QuickReviewBatchRequest request)
    {
        var reviewerId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        var reviewed = await _reviewService.QuickReviewBatchAsync(request.TestOrderIds, reviewerId);
        return Ok(ApiResponse<object>.Ok(new { reviewed }));
    }
}
