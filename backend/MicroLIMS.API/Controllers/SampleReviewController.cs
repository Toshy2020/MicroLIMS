using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.Services;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

public record CompleteSampleReviewRequest(string Password, string? Comment);

// Sample-level review, reached by clicking a Sample's lifecycle badge in
// the Testing Workspace rather than a standalone Review page.
[ApiController]
[Route("api/samples/{id}/review")]
[Authorize(Roles = RoleConstants.Reviewer + "," + RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
public class SampleReviewController : ControllerBase
{
    private readonly SampleReviewService _reviewService;

    public SampleReviewController(SampleReviewService reviewService)
    {
        _reviewService = reviewService;
    }

    private int CurrentUserId => int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

    [HttpPost("complete")]
    public async Task<IActionResult> Complete(int id, CompleteSampleReviewRequest request)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        await _reviewService.CompleteReviewAsync(id, CurrentUserId, request.Password, request.Comment, ip);
        return Ok(ApiResponse<object>.Ok(new { }));
    }
}
