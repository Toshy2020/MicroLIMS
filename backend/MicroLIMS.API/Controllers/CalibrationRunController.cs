using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

[ApiController]
[Route("api/calibration-runs")]
[Authorize]
public class CalibrationRunController : ControllerBase
{
    private readonly ICalibrationRunService _service;

    public CalibrationRunController(ICalibrationRunService service)
    {
        _service = service;
    }

    private int CurrentUserId =>
        int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var uid) ? uid : 0;

    private string? ClientIpAddress =>
        HttpContext.Connection.RemoteIpAddress?.ToString();

    // Create a new signed Calibration Run (Slice S1)
    [Authorize(Policy = PermissionConstants.TestWorkflowExecute)]
    [HttpPost]
    [RequestSizeLimit(30 * 1024 * 1024)]
    public async Task<IActionResult> Create([FromForm(Name = "payload")] string? payload, [FromForm(Name = "report")] IFormFile? report)
    {
        if (report == null || report.Length == 0)
            return BadRequest(ApiResponse<object>.Fail("A calibration report file is required."));

        if (string.IsNullOrWhiteSpace(payload))
            return BadRequest(ApiResponse<object>.Fail("Payload is required."));

        CreateCalibrationRunRequest? request;
        try
        {
            request = JsonSerializer.Deserialize<CreateCalibrationRunRequest>(payload, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<object>.Fail($"Invalid JSON payload: {ex.Message}"));
        }

        if (request == null)
            return BadRequest(ApiResponse<object>.Fail("Payload could not be parsed."));

        await using var stream = report.OpenReadStream();
        var run = await _service.CreateAsync(
            request,
            stream,
            report.FileName,
            report.ContentType,
            CurrentUserId,
            ClientIpAddress);

        return Ok(ApiResponse<object>.Ok(CalibrationRunView.From(run)));
    }

    // Preview Calibration Run without saving or signing (Slice S1 hardening)
    [Authorize(Policy = PermissionConstants.TestWorkflowExecute)]
    [HttpPost("preview")]
    public async Task<IActionResult> Preview([FromBody] PreviewCalibrationRunRequest request)
    {
        var result = await _service.PreviewAsync(request, CurrentUserId);
        return Ok(ApiResponse<object>.Ok(result));
    }

    // Withdraw a signed Calibration Run (one-way supervisory retirement)
    [Authorize(Policy = PermissionConstants.SamplesApprove)]
    [HttpPost("{id:int}/withdraw")]
    public async Task<IActionResult> Withdraw(int id, [FromBody] WithdrawCalibrationRunRequest request)
    {
        var run = await _service.WithdrawAsync(id, request, CurrentUserId, ClientIpAddress);
        return Ok(ApiResponse<object>.Ok(CalibrationRunView.From(run)));
    }

    // List Calibration Runs with filters and section scoping
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int? testDefinitionId = null,
        [FromQuery] string? testCode = null,
        [FromQuery] bool? passed = null,
        [FromQuery] string? status = null,
        [FromQuery] CalibrationRunStatus? runStatus = null,
        [FromQuery] DateTime? date = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null)
    {
        bool? effectivePassed = passed;
        CalibrationRunStatus? effectiveStatus = runStatus;

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (string.Equals(status, "passed", StringComparison.OrdinalIgnoreCase))
                effectivePassed = true;
            else if (string.Equals(status, "failed", StringComparison.OrdinalIgnoreCase))
                effectivePassed = false;
            else if (Enum.TryParse<CalibrationRunStatus>(status, true, out var parsedStatus))
                effectiveStatus = parsedStatus;
        }

        var filter = new CalibrationRunFilter(
            TestDefinitionId: testDefinitionId,
            TestCode: testCode,
            Passed: effectivePassed,
            Status: effectiveStatus,
            Date: date,
            FromDate: fromDate,
            ToDate: toDate);

        var runs = await _service.GetAllAsync(filter, CurrentUserId);
        return Ok(ApiResponse<object>.Ok(runs.Select(CalibrationRunView.From)));
    }


    // Get Calibration Run by ID (scoped)
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var run = await _service.GetByIdAsync(id, CurrentUserId);
        if (run is null)
            return NotFound(ApiResponse<object>.Fail($"Calibration run {id} not found."));

        return Ok(ApiResponse<object>.Ok(CalibrationRunView.From(run)));
    }

    // Printable run report: the run, its acceptance criteria, signature, document, and analytes
    [HttpGet("{id:int}/report")]
    public async Task<IActionResult> GetReport(int id)
    {
        var run = await _service.GetByIdAsync(id, CurrentUserId);
        if (run is null)
            return NotFound(ApiResponse<object>.Fail($"Calibration run {id} not found."));

        var details = await _service.GetReportDetailsAsync(id, CurrentUserId);
        return Ok(ApiResponse<object>.Ok(new { Run = CalibrationRunView.From(run), Details = details }));
    }

    // Retrieve the raw uploaded report document with integrity hash verification
    [HttpGet("{id:int}/document")]
    public async Task<IActionResult> GetDocument(int id)
    {
        var (document, content) = await _service.GetDocumentContentAsync(id, CurrentUserId);
        return File(content, document.ContentType, document.OriginalFileName);
    }
}
