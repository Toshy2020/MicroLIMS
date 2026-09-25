using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

// Read model for the floating Sample Summary page - available to any
// authenticated role (the Testing Workspace itself is shared by every
// role, and an Approved sample's summary/report is meant to be viewable
// broadly, not just by the reviewer/approver who acted on it) - but only
// by users assigned to a laboratory section the sample has tests in.
[ApiController]
[Route("api/samples")]
[Authorize]
public class SampleSummaryController : ControllerBase
{
    private readonly SampleSummaryService _summaryService;
    private readonly IUserSectionScopeService _scopeService;

    public SampleSummaryController(SampleSummaryService summaryService, IUserSectionScopeService scopeService)
    {
        _summaryService = summaryService;
        _scopeService = scopeService;
    }

    private int CurrentUserId => int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

    [HttpGet("{id}/summary")]
    public async Task<IActionResult> GetSummary(int id, [FromQuery] bool forCertificate = false)
    {
        await _scopeService.EnsureSampleAccessAsync(CurrentUserId, id);
        var scope = await _scopeService.GetAccessibleSectionIdsAsync(CurrentUserId);
        // The CoA page asks forCertificate: a final sample's combined
        // certificate is printable by any of its labs' members.
        var summary = forCertificate
            ? await _summaryService.GetCertificateSummaryAsync(id, scope)
            : await _summaryService.GetSummaryAsync(id, scope);
        if (summary is null) return NotFound(ApiResponse<object>.Fail($"Sample {id} not found."));
        return Ok(ApiResponse<object>.Ok(summary));
    }

    [HttpGet("{id}/summary/pdf")]
    public async Task<IActionResult> GetSummaryPdf(int id)
    {
        await _scopeService.EnsureSampleAccessAsync(CurrentUserId, id);
        var result = await _summaryService.GenerateSummaryPdfAsync(id, await _scopeService.GetAccessibleSectionIdsAsync(CurrentUserId));
        if (result is null) return NotFound(ApiResponse<object>.Fail($"Sample {id} not found."));
        return File(result.Value.bytes, "application/pdf", $"{result.Value.fileNameStem}.pdf");
    }

    [HttpGet("{id}/summary/word")]
    public async Task<IActionResult> GetSummaryWord(int id)
    {
        await _scopeService.EnsureSampleAccessAsync(CurrentUserId, id);
        var result = await _summaryService.GenerateSummaryWordAsync(id, await _scopeService.GetAccessibleSectionIdsAsync(CurrentUserId));
        if (result is null) return NotFound(ApiResponse<object>.Fail($"Sample {id} not found."));
        return File(result.Value.bytes, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", $"{result.Value.fileNameStem}.docx");
    }
}
