using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.Services;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

// The frozen PDFs cut at each final decision - what an auditor asks for
// when they want the record "as issued" rather than as it looks now.
//
// Read-only by design: there is deliberately no POST/PUT/DELETE here, and
// none should be added, for the same reason ElectronicSignature has none.
// Archives are written only as a side effect of a signed decision.
[ApiController]
[Route("api/archived-records")]
[Authorize]
public class ArchivedRecordsController : ControllerBase
{
    private readonly RecordArchiveService _archive;

    public ArchivedRecordsController(RecordArchiveService archive)
    {
        _archive = archive;
    }

    [HttpGet]
    public async Task<IActionResult> GetForEntity([FromQuery] string entityType, [FromQuery] int entityId)
    {
        var records = await _archive.GetForEntityAsync(entityType, entityId);
        return Ok(ApiResponse<object>.Ok(records.Select(r => new
        {
            r.Id, r.EntityType, r.EntityId, r.DocumentId, r.FileName,
            r.SizeBytes, r.ContentSha256, r.Reason,
            r.GeneratedByNameSnapshot, r.GeneratedAt
        })));
    }

    // Serves the archived bytes. The integrity check runs on every read;
    // a mismatch is surfaced in a response header rather than silently
    // handing back a file that no longer matches what was signed for.
    [HttpGet("{id}/download")]
    public async Task<IActionResult> Download(int id)
    {
        var result = await _archive.ReadAsync(id);
        if (result is null) return NotFound(ApiResponse<object>.Fail($"Archived record {id} not found."));

        var (record, bytes, integrityOk) = result.Value;
        Response.Headers.Append("X-Archive-Integrity", integrityOk ? "verified" : "FAILED");
        return File(bytes, "application/pdf", record.FileName);
    }
}
