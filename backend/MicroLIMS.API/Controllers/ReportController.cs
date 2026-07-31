using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.Interfaces;

namespace MicroLIMS.API.Controllers;

[ApiController]
[Route("api/reports")]
[Authorize]
public class ReportController : ControllerBase
{
    private readonly IReportService _reportService;

    public ReportController(IReportService reportService)
    {
        _reportService = reportService;
    }

    [HttpGet("product")]
    public async Task<IActionResult> Product([FromQuery] int sampleId)
    {
        var pdf = await _reportService.GenerateProductReportPdfAsync(sampleId);
        return File(pdf, "application/pdf", $"ProductReport_{sampleId}.pdf");
    }

    [HttpGet("water")]
    public async Task<IActionResult> Water([FromQuery] DateTime date)
    {
        var pdf = await _reportService.GenerateWaterReportPdfAsync(date);
        return File(pdf, "application/pdf", $"WaterReport_{date:yyyyMMdd}.pdf");
    }

    [HttpGet("em")]
    public async Task<IActionResult> EM([FromQuery] DateTime date)
    {
        var pdf = await _reportService.GenerateEMReportPdfAsync(date);
        return File(pdf, "application/pdf", $"EMReport_{date:yyyyMMdd}.pdf");
    }

    [HttpGet("aftercleaning")]
    public async Task<IActionResult> AfterCleaning([FromQuery] int sampleId)
    {
        var pdf = await _reportService.GenerateAfterCleaningReportPdfAsync(sampleId);
        return File(pdf, "application/pdf", $"AfterCleaningReport_{sampleId}.pdf");
    }
}
