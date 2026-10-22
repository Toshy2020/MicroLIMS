using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.Services;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

[ApiController]
[Route("api/users/analysts")]
[Authorize(Policy = PermissionConstants.SamplesAssignAnalyst)]
public class UserAnalystController : ControllerBase
{
    private readonly UserService _userService;

    public UserAnalystController(UserService userService)
    {
        _userService = userService;
    }

    private int CurrentUserId => int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

    [HttpGet]
    public async Task<IActionResult> GetEligibleAnalysts() =>
        Ok(ApiResponse<object>.Ok(await _userService.GetEligibleAnalystsAsync(CurrentUserId)));
}
