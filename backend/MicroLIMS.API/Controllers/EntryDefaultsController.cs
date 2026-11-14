using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.Services.EntryDefaults;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

[ApiController]
[Route("api/entry-defaults")]
[Authorize]
public sealed class EntryDefaultsController : ControllerBase
{
    private readonly EntryDefaultsService _entryDefaults;

    public EntryDefaultsController(EntryDefaultsService entryDefaults)
    {
        _entryDefaults = entryDefaults;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<EntryDefaultsResponse>>> Get(
        [FromQuery] string context,
        [FromQuery] int? testOrderId = null,
        [FromQuery] int? stepMediaId = null,
        [FromQuery] int? mediaProductId = null,
        [FromQuery] int? solutionMasterId = null,
        [FromQuery] string? category = null,
        CancellationToken cancellationToken = default)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var response = await _entryDefaults.GetAsync(
            userId,
            context,
            testOrderId,
            stepMediaId,
            mediaProductId,
            solutionMasterId,
            category,
            cancellationToken);
        return Ok(ApiResponse<EntryDefaultsResponse>.Ok(response));
    }
}
