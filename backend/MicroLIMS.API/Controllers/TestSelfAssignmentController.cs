using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.Services;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

// An analyst takes an unassigned test in a section they belong to, or gives it back before recording anything.
// Separate from TestingWorkspaceController, which is read-only by design. The caller is always the signed-in user.
[ApiController]
[Route("api/testorders")]
[Authorize]
public class TestSelfAssignmentController : ControllerBase
{
    private readonly TestSelfAssignmentService _selfAssignment;

    public TestSelfAssignmentController(TestSelfAssignmentService selfAssignment) => _selfAssignment = selfAssignment;

    private int CurrentUserId =>
        int.TryParse(User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var id)
            ? id
            : throw new UnauthorizedAccessException();

    [HttpPost("{id:int}/take")]
    [Authorize(Policy = PermissionConstants.TestsSelfAssign)]
    public async Task<IActionResult> Take(int id, CancellationToken cancellationToken)
    {
        await _selfAssignment.TakeAsync(id, CurrentUserId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }));
    }

    [HttpPost("{id:int}/release")]
    [Authorize(Policy = PermissionConstants.TestsSelfAssign)]
    public async Task<IActionResult> Release(int id, CancellationToken cancellationToken)
    {
        await _selfAssignment.ReleaseAsync(id, CurrentUserId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }));
    }
}
