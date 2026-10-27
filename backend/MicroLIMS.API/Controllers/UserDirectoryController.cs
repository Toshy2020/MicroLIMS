using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.Services;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

[ApiController]
[Route("api/users/directory")]
[Authorize]
public class UserDirectoryController : ControllerBase
{
    private readonly UserService _userService;

    public UserDirectoryController(UserService userService)
    {
        _userService = userService;
    }

    [HttpGet]
    public async Task<IActionResult> GetDirectory([FromQuery] string? permission = null)
    {
        var users = await _userService.GetDirectoryAsync(permission);
        return Ok(ApiResponse<List<UserDirectoryDto>>.Ok(users));
    }
}
