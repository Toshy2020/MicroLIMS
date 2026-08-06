using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

public record CreateUserRequest(string FullName, string Username, string Password, int RoleId, string? Email = null);
public record UpdateEmailRequest(string? Email);

[ApiController]
[Route("api/users")]
[Authorize(Roles = RoleConstants.SystemAdministrator)]
public class UserController : ControllerBase
{
    private readonly UserService _userService;

    public UserController(UserService userService)
    {
        _userService = userService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll() => Ok(ApiResponse<object>.Ok(await _userService.GetAllAsync()));

    [HttpPost]
    public async Task<IActionResult> Create(CreateUserRequest request)
    {
        try
        {
            var user = new User { FullName = request.FullName, Username = request.Username, RoleId = request.RoleId, Email = request.Email };
            var created = await _userService.CreateAsync(user, request.Password);
            return Ok(ApiResponse<object>.Ok(created));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpPut("{id}/deactivate")]
    public async Task<IActionResult> Deactivate(int id)
    {
        await _userService.DeactivateAsync(id);
        return Ok(ApiResponse<object>.Ok(new { }));
    }

    [HttpPut("{id}/email")]
    public async Task<IActionResult> UpdateEmail(int id, UpdateEmailRequest request)
    {
        try
        {
            await _userService.UpdateEmailAsync(id, request.Email);
            return Ok(ApiResponse<object>.Ok(new { }));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }
}
