using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.Services;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

// Read model for the floating Sample Summary page - available to any
// authenticated role (the Testing Workspace itself is shared by every
// role, and an Approved sample's summary/report is meant to be viewable
// broadly, not just by the reviewer/approver who acted on it).
[ApiController]
[Route("api/samples")]
[Authorize]
public class SampleSummaryController : ControllerBase
{
    private readonly SampleSummaryService _summaryService;

    public SampleSummaryController(SampleSummaryService summaryService)
    {
        _summaryService = summaryService;
    }

    [HttpGet("{id}/summary")]
    public async Task<IActionResult> GetSummary(int id)
    {
        var summary = await _summaryService.GetSummaryAsync(id);
        if (summary is null) return NotFound(ApiResponse<object>.Fail($"Sample {id} not found."));
        return Ok(ApiResponse<object>.Ok(summary));
    }

    [HttpGet("{id}/summary/pdf")]
    public async Task<IActionResult> GetSummaryPdf(int id)
    {
        var result = await _summaryService.GenerateSummaryPdfAsync(id);
        if (result is null) return NotFound(ApiResponse<object>.Fail($"Sample {id} not found."));
        return File(result.Value.bytes, "application/pdf", $"{result.Value.fileNameStem}.pdf");
    }

    [HttpGet("{id}/summary/word")]
    public async Task<IActionResult> GetSummaryWord(int id)
    {
        var result = await _summaryService.GenerateSummaryWordAsync(id);
        if (result is null) return NotFound(ApiResponse<object>.Fail($"Sample {id} not found."));
        return File(result.Value.bytes, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", $"{result.Value.fileNameStem}.docx");
    }
}
