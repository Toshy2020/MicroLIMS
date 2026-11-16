using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.Services;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

// Whether the caller could perform each gated action on a record now, with the server's own refusal sentence
// when not. A display aid: the actions enforce the same checks themselves. There is no role policy because the
// answer is per record and per caller; a record the caller may not read answers 404, exactly as an unknown one.
// Read-only.
[ApiController]
[Route("api/actions")]
[Authorize]
public class ActionAvailabilityController : ControllerBase
{
    private readonly ActionAvailabilityService _service;

    public ActionAvailabilityController(ActionAvailabilityService service) => _service = service;

    private int CurrentUserId =>
        int.TryParse(User?.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id)
            ? id
            : throw new UnauthorizedAccessException();

    [HttpGet("sample/{id:int}")]
    public async Task<IActionResult> ForSample(int id, [FromQuery] int? sectionId, CancellationToken cancellationToken) =>
        Answer(await _service.ForSampleAsync(id, sectionId, CurrentUserId, cancellationToken));

    [HttpGet("test-order/{id:int}")]
    public async Task<IActionResult> ForTestOrder(int id, CancellationToken cancellationToken) =>
        Answer(await _service.ForTestOrderAsync(id, CurrentUserId, cancellationToken));

    [HttpGet("media/{id:int}")]
    public async Task<IActionResult> ForMedia(int id, CancellationToken cancellationToken) =>
        Answer(await _service.ForMediaAsync(id, CurrentUserId, cancellationToken));

    [HttpGet("document-revision/{id:int}")]
    public async Task<IActionResult> ForDocumentRevision(int id, CancellationToken cancellationToken) =>
        Answer(await _service.ForDocumentRevisionAsync(id, CurrentUserId, cancellationToken));

    private IActionResult Answer(IReadOnlyList<ActionAvailability>? actions) =>
        actions is null
            ? NotFound(ApiResponse<object>.Fail("Record not found."))
            : Ok(ApiResponse<IReadOnlyList<ActionAvailability>>.Ok(actions));
}
