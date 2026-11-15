using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.Services;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

public sealed record UpdateSectionSelfAssignmentRequest(bool Enabled, string? Reason, uint? Version = null);

// Section Heads control whether analysts holding Tests.SelfAssign may take unassigned tests from My Work
// in each section they belong to. Gated by Samples.AssignAnalyst (never held by System Administrator).
[ApiController]
[Route("api/sections")]
[Authorize(Policy = PermissionConstants.SamplesAssignAnalyst)]
public class SectionSelfAssignmentController : ControllerBase
{
    private readonly SectionSelfAssignmentService _service;

    public SectionSelfAssignmentController(SectionSelfAssignmentService service) => _service = service;

    private int CurrentUserId =>
        int.TryParse(User?.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id)
            ? id
            : throw new UnauthorizedAccessException();

    [HttpGet("self-assignment")]
    public async Task<IActionResult> GetAll(CancellationToken ct = default) =>
        Ok(ApiResponse<IReadOnlyList<SectionSelfAssignmentDto>>.Ok(await _service.GetForUserAsync(CurrentUserId, ct)));

    [HttpPut("{sectionId:int}/self-assignment")]
    public async Task<IActionResult> Update(
        int sectionId,
        [FromBody] UpdateSectionSelfAssignmentRequest request,
        CancellationToken ct = default) =>
        Ok(ApiResponse<SectionSelfAssignmentDto>.Ok(
            await _service.SetAsync(CurrentUserId, sectionId, request.Enabled, request.Reason, request.Version, ct)));
}
