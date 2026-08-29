using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.Services;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

public record PrepareMediaHttpRequest(
    int MaterialId, decimal TotalWeight, string TotalVolume,
    int AutoclaveEquipmentId, string AutoclaveProgram, string LoadType, decimal Temperature,
    int CycleTime, int CycleNumber, decimal Ph, DateTime ExpiryDate);

public record DecideMediaReleaseRequest(string Password, bool Approved, string? Comment);

public record MarkOutOfStockHttpRequest(string? Comment);

// Media Preparation module - the autoclave/cycle/pH grid. Nothing here
// is usable in routine testing until it also passes GPT.
[ApiController]
[Route("api/media")]
[Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.Analyst + "," + RoleConstants.Reviewer + "," + RoleConstants.SystemAdministrator)]
public class MediaController : ControllerBase
{
    private readonly MediaPreparationService _mediaPrep;
    private readonly MediaReleaseService _mediaRelease;
    private readonly MediaSummaryService _summary;
    private readonly MediaExpiryService _expiry;

    public MediaController(MediaPreparationService mediaPrep, MediaReleaseService mediaRelease, MediaSummaryService summary, MediaExpiryService expiry)
    {
        _mediaPrep = mediaPrep;
        _mediaRelease = mediaRelease;
        _summary = summary;
        _expiry = expiry;
    }

    private int CurrentUserId => int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

    [HttpGet]
    public async Task<IActionResult> GetAll() => Ok(ApiResponse<object>.Ok(await _mediaPrep.GetAllAsync()));

    // Powers the Dashboard's Media Expiry panel.
    [HttpGet("expiring")]
    public async Task<IActionResult> GetExpiring([FromQuery] int withinDays = 7) =>
        Ok(ApiResponse<object>.Ok(await _expiry.GetExpiringAsync(withinDays)));

    [HttpGet("released")]
    public async Task<IActionResult> GetReleased([FromQuery] int? materialId, [FromQuery] bool includeExpired = false, [FromQuery] int? excludeId = null) =>
        Ok(ApiResponse<object>.Ok(await _mediaPrep.GetReleasedAsync(materialId, includeExpired, excludeId)));

    [HttpPost]
    public async Task<IActionResult> Prepare(PrepareMediaHttpRequest r) =>
        Ok(ApiResponse<object>.Ok(await _mediaPrep.PrepareAsync(new PrepareMediaRequest(
            r.MaterialId, r.TotalWeight, r.TotalVolume,
            r.AutoclaveEquipmentId, r.AutoclaveProgram, r.LoadType, r.Temperature,
            r.CycleTime, r.CycleNumber, r.Ph, r.ExpiryDate, CurrentUserId))));

    // Lots that passed evaluation and are waiting on a release signature.
    [HttpGet("awaiting-approval")]
    public async Task<IActionResult> GetAwaitingApproval() =>
        Ok(ApiResponse<object>.Ok(await _mediaRelease.GetAwaitingApprovalAsync()));

    // The release gate itself - Section Head only, matching
    // CryovialController.Approve's restriction on the equivalent action.
    [HttpPost("{id}/release")]
    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    public async Task<IActionResult> DecideRelease(int id, DecideMediaReleaseRequest r)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        await _mediaRelease.DecideAsync(id, CurrentUserId, r.Password, r.Approved, r.Comment, ip);
        return Ok(ApiResponse<object>.Ok(new { }));
    }

    [HttpPost("{id}/mark-out-of-stock")]
    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.Analyst + "," + RoleConstants.Reviewer + "," + RoleConstants.SystemAdministrator)]
    public async Task<IActionResult> MarkOutOfStock(int id, [FromBody] MarkOutOfStockHttpRequest? request)
    {
        await _mediaPrep.MarkOutOfStockAsync(id, CurrentUserId, request?.Comment);
        return Ok(ApiResponse<object>.Ok(new { }));
    }

    [HttpGet("{id}/summary")]
    public async Task<IActionResult> GetSummary(int id)
    {
        var summary = await _summary.GetSummaryAsync(id);
        if (summary is null) return NotFound(ApiResponse<object>.Fail($"Media lot {id} not found."));
        return Ok(ApiResponse<object>.Ok(summary));
    }

    [HttpGet("{id}/summary/pdf")]
    public async Task<IActionResult> GetSummaryPdf(int id)
    {
        var result = await _summary.GenerateSummaryPdfAsync(id);
        if (result is null) return NotFound(ApiResponse<object>.Fail($"Media lot {id} not found."));
        return File(result.Value.bytes, "application/pdf", $"{result.Value.fileNameStem}.pdf");
    }

    [HttpGet("{id}/summary/word")]
    public async Task<IActionResult> GetSummaryWord(int id)
    {
        var result = await _summary.GenerateSummaryWordAsync(id);
        if (result is null) return NotFound(ApiResponse<object>.Fail($"Media lot {id} not found."));
        return File(result.Value.bytes, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", $"{result.Value.fileNameStem}.docx");
    }
}
