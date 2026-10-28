using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.Services;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

public record DecideMaterialReleaseRequest(string Password, bool Approved, string? Comment);

// The release gate for stock lots in categories that require approval before use.
[ApiController]
[Route("api/materials")]
[Authorize(Policy = PermissionConstants.MaterialsRelease)]
public class MaterialReleaseController : ControllerBase
{
    private readonly MaterialReleaseService _service;
    public MaterialReleaseController(MaterialReleaseService service) => _service = service;
    private int CurrentUserId => int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

    [HttpPost("{id:int}/release")]
    public async Task<IActionResult> Decide(int id, DecideMaterialReleaseRequest r)
    {
        await _service.DecideAsync(id, CurrentUserId, r.Password, r.Approved, r.Comment, HttpContext.Connection.RemoteIpAddress?.ToString());
        return Ok(ApiResponse<object>.Ok(new { }));
    }
}
