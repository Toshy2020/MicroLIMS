using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.Services;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

[ApiController]
[Route("api/users/analysts")]
[Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
public class UserAnalystController : ControllerBase
{
    private readonly UserService _userService;

    public UserAnalystController(UserService userService)
    {
        _userService = userService;
    }

    [HttpGet]
    public async Task<IActionResult> GetEligibleAnalysts() =>
        Ok(ApiResponse<object>.Ok(await _userService.GetEligibleAnalystsAsync()));
}
