using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.Services;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

public record ReplaceUserMembershipsRequest(List<UserOrgMembershipDto> Memberships);

// Laboratory departments/sections (for pickers) and user memberships (which
// decide the data each user can see and act on).
[ApiController]
[Route("api/org")]
[Authorize]
public class LaboratoryOrganizationController : ControllerBase
{
    private readonly LaboratoryOrganizationService _service;

    public LaboratoryOrganizationController(LaboratoryOrganizationService service)
    {
        _service = service;
    }

    private int CurrentUserId => int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

    [HttpGet("sections")]
    public async Task<IActionResult> GetSections() =>
        Ok(ApiResponse<object>.Ok(await _service.GetActiveSectionsAsync()));

    [HttpGet("my-sections")]
    public async Task<IActionResult> GetMySections() =>
        Ok(ApiResponse<object>.Ok(await _service.GetMySectionsAsync(CurrentUserId)));

    [Authorize(Roles = RoleConstants.SystemAdministrator)]
    [HttpGet("users/{userId}/memberships")]
    public async Task<IActionResult> GetMemberships(int userId) =>
        Ok(ApiResponse<object>.Ok(await _service.GetMembershipsAsync(userId)));

    [Authorize(Roles = RoleConstants.SystemAdministrator)]
    [HttpPut("users/{userId}/memberships")]
    public async Task<IActionResult> ReplaceMemberships(int userId, ReplaceUserMembershipsRequest request) =>
        Ok(ApiResponse<object>.Ok(await _service.ReplaceMembershipsAsync(userId, request.Memberships ?? new(), CurrentUserId)));
}
