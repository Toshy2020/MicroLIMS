using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

public sealed record TouchRecentRecordRequest(string RecordType, int RecordId);

// The caller's own recently opened records. The user is always the authenticated caller. Reading a record is
// checked again in the service (section scope / document authorisation); there is no role policy because
// what a user may open is decided per record.
[ApiController]
[Route("api/me/recent")]
[Authorize]
public class RecentRecordsController : ControllerBase
{
    private readonly IRecentRecordService _service;

    public RecentRecordsController(IRecentRecordService service) => _service = service;

    private int CurrentUserId =>
        int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;

    // 204 whether or not the record was stored, so the endpoint cannot be used to probe for records.
    [HttpPost]
    public async Task<IActionResult> Touch([FromBody] TouchRecentRecordRequest request, CancellationToken ct)
    {
        await _service.TouchAsync(CurrentUserId, request.RecordType, request.RecordId, ct);
        return NoContent();
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Ok(ApiResponse<IReadOnlyList<RecentRecordDto>>.Ok(await _service.ListAsync(CurrentUserId, ct)));

    [HttpDelete]
    public async Task<IActionResult> Clear(CancellationToken ct)
    {
        await _service.ClearAsync(CurrentUserId, ct);
        return NoContent();
    }
}
