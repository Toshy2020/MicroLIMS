using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.Services;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

public record IdentityConfirmationRowRequest(int MediaId, int IncubatorEquipmentId, DateTime IncubationStart, DateTime IncubationEnd, string ObservationText);
public record PrepareCryovialsHttpRequest(
    int MaterialId, int NumberOfVialsPrepared, DateTime ExpiryDate, string StorageCondition, bool PhysicalCheckConfirmed, string PhysicalCheckText,
    List<IdentityConfirmationRowRequest> Panel, int DiscsUsed);
public record ApproveRequest(bool Approved, string Password, string? Comment);
public record ThawVialRequest(string? Notes);

// Cryovial batches - prepared directly from a LyophilizedMicroorganism
// Material row, identity-confirmed, and approval-gated before GPT can
// reference them.
[ApiController]
[Route("api/cryovials")]
[Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.Analyst + "," + RoleConstants.Reviewer + "," + RoleConstants.SystemAdministrator)]
public class CryovialController : ControllerBase
{
    private readonly CryovialService _service;
    private readonly CryovialSummaryService _summary;

    public CryovialController(CryovialService service, CryovialSummaryService summary)
    {
        _service = service;
        _summary = summary;
    }

    private int CurrentUserId => int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

    [HttpGet]
    public async Task<IActionResult> GetAll() => Ok(ApiResponse<object>.Ok(await _service.GetAllAsync()));

    [HttpPost("prepare")]
    public async Task<IActionResult> PrepareCryovials(PrepareCryovialsHttpRequest r) =>
        Ok(ApiResponse<object>.Ok(await _service.PrepareCryovialsAsync(new PrepareCryovialsRequest(
            r.MaterialId, r.NumberOfVialsPrepared, r.ExpiryDate, r.StorageCondition, r.PhysicalCheckConfirmed, r.PhysicalCheckText,
            r.Panel.Select(p => new IdentityConfirmationRow(p.MediaId, p.IncubatorEquipmentId, p.IncubationStart, p.IncubationEnd, p.ObservationText)).ToList(),
            r.DiscsUsed, CurrentUserId))));

    [HttpPost("{id}/approve")]
    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    public async Task<IActionResult> Approve(int id, ApproveRequest r)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        return Ok(ApiResponse<object>.Ok(await _service.ApproveAsync(id, r.Approved, CurrentUserId, r.Password, r.Comment, ip)));
    }

    [HttpPost("{id}/destroy")]
    public async Task<IActionResult> Destroy(int id)
    {
        await _service.DestroyAsync(id);
        return Ok(ApiResponse<object>.Ok(new { }));
    }

    [HttpPost("{id}/thaw")]
    public async Task<IActionResult> ThawVial(int id, ThawVialRequest r)
    {
        await _service.ThawVialAsync(id, CurrentUserId, r.Notes);
        return Ok(ApiResponse<object>.Ok(new { }));
    }

    [HttpGet("{id}/summary")]
    public async Task<IActionResult> GetSummary(int id)
    {
        var summary = await _summary.GetSummaryAsync(id);
        if (summary is null) return NotFound(ApiResponse<object>.Fail($"Cryovial batch {id} not found."));
        return Ok(ApiResponse<object>.Ok(summary));
    }

    [HttpGet("{id}/summary/pdf")]
    public async Task<IActionResult> GetSummaryPdf(int id)
    {
        var result = await _summary.GenerateSummaryPdfAsync(id);
        if (result is null) return NotFound(ApiResponse<object>.Fail($"Cryovial batch {id} not found."));
        return File(result.Value.bytes, "application/pdf", $"{result.Value.fileNameStem}.pdf");
    }

    [HttpGet("{id}/summary/word")]
    public async Task<IActionResult> GetSummaryWord(int id)
    {
        var result = await _summary.GenerateSummaryWordAsync(id);
        if (result is null) return NotFound(ApiResponse<object>.Fail($"Cryovial batch {id} not found."));
        return File(result.Value.bytes, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", $"{result.Value.fileNameStem}.docx");
    }
}
