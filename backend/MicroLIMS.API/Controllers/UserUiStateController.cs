using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

public sealed record SetUiStateRequest(string Value);

// The caller's own interface preferences. The user is always the authenticated
// caller: there is no user id in any route or body.
[ApiController]
[Route("api/me/ui-state")]
[Authorize]
public class UserUiStateController : ControllerBase
{
    private readonly IUserUiStateService _service;

    public UserUiStateController(IUserUiStateService service) => _service = service;

    private int CurrentUserId =>
        int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var state = await _service.GetAllAsync(CurrentUserId, ct);
        return Ok(ApiResponse<IReadOnlyDictionary<string, string>>.Ok(state));
    }

    [HttpPut("{key}")]
    public async Task<IActionResult> Set(string key, [FromBody] SetUiStateRequest request, CancellationToken ct)
    {
        await _service.SetAsync(CurrentUserId, key, request.Value, ct);
        return Ok(ApiResponse<object?>.Ok(null));
    }

    [HttpDelete("{key}")]
    public async Task<IActionResult> Remove(string key, CancellationToken ct)
    {
        await _service.RemoveAsync(CurrentUserId, key, ct);
        return Ok(ApiResponse<object?>.Ok(null));
    }
}
