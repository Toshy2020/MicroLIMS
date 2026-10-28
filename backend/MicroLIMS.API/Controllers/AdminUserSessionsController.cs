using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.Services;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

public record RevokeUserSessionsRequest(string? Reason = null);

// Active sign-in sessions (refresh tokens) of a user, for the admin panel.
// Access tokens already issued remain valid until they expire.
[ApiController]
[Route("api/admin/users/{id:int}/sessions")]
[Authorize(Policy = PermissionConstants.UsersManage)]
public class AdminUserSessionsController : ControllerBase
{
    private readonly UserService _userService;

    public AdminUserSessionsController(UserService userService)
    {
        _userService = userService;
    }

    private int CurrentUserId => int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

    [HttpGet]
    public async Task<IActionResult> List(int id) =>
        Ok(ApiResponse<object>.Ok(await _userService.GetActiveSessionsAsync(id)));

    [HttpPost("revoke")]
    public async Task<IActionResult> Revoke(int id, RevokeUserSessionsRequest request)
    {
        var revoked = await _userService.RevokeSessionsAsync(id, request.Reason, CurrentUserId);
        return Ok(ApiResponse<object>.Ok(new { revokedCount = revoked }));
    }
}
