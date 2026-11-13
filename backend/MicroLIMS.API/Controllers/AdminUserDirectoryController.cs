using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.Services;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

public record DirectoryRelinkRequest(string? Reason = null);

// A user's company-account link, for the admin Users page.
[ApiController]
[Route("api/admin/users/{id:int}")]
[Authorize(Policy = PermissionConstants.UsersManage)]
public class AdminUserDirectoryController : ControllerBase
{
    private readonly UserService _userService;

    public AdminUserDirectoryController(UserService userService)
    {
        _userService = userService;
    }

    private int CurrentUserId => int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

    // Forgets the permanent company id so the next company sign-in binds again.
    [HttpPost("directory-relink")]
    public async Task<IActionResult> Relink(int id, DirectoryRelinkRequest request, CancellationToken ct)
    {
        await _userService.RelinkDirectoryAsync(id, request.Reason, CurrentUserId, ct);
        return Ok(ApiResponse<object>.Ok(new { }));
    }
}
