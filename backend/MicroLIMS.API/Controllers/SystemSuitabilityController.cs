using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

// What the API returns for a run. Never the entity itself: its navigation to
// the performing User would serialize that user's PasswordHash.
public record SystemSuitabilityRunView(
    int Id, string Code, bool Passed, string? FailureReasons,
    int TestDefinitionId, string? TestCode, string? TestName, string? MethodAbbreviation,
    int SectionId, string? SectionName,
    int EquipmentId, string? EquipmentCode, string? EquipmentName,
    int ChromatographyColumnId, string? ColumnCode, string? ColumnName,
    int ReferenceStandardMaterialId, string? ReferenceStandardName, string? ReferenceStandardBatch,
    decimal StandardPurityPercent, decimal StandardWeightMg, decimal StandardDilution, decimal StandardMeanArea,
    decimal? RsdPercent, decimal? Resolution, decimal? TailingFactor, decimal? TheoreticalPlates,
    int PerformedByUserId, string? PerformedByName, DateTime PerformedAt, string? Comment,
    List<SystemSuitabilityRunAnalyteView>? Analytes = null,
    decimal? TheoreticalWeightMg = null,
    decimal? MoisturePercent = null,
    decimal? StandardWeighInDeviationPercent = null,
    bool StandardWeighInOutOfWindow = false,
    string? WeighInJustification = null,
    decimal? ComputedRsdPercent = null)
{
    public static SystemSuitabilityRunView From(SystemSuitabilityRun r) => new(
        r.Id, r.Code, r.Passed, r.FailureReasons,
        r.TestDefinitionId, r.TestDefinition?.Code, r.TestDefinition?.DisplayName, r.TestDefinition?.MethodAbbreviation,
        r.SectionId, r.Section?.Name,
        r.EquipmentId, r.Equipment?.Code, r.Equipment?.Name,
        r.ChromatographyColumnId, r.ChromatographyColumn?.Code, r.ChromatographyColumn?.Name,
        r.ReferenceStandardMaterialId, r.ReferenceStandardMaterial?.MaterialName, r.ReferenceStandardMaterial?.BatchNumber,
        r.StandardPurityPercent, r.StandardWeightMg, r.StandardDilution, r.StandardMeanArea,
        r.RsdPercent, r.Resolution, r.TailingFactor, r.TheoreticalPlates,
        r.PerformedByUserId, r.PerformedByUser?.FullName ?? r.Signature?.UserFullNameSnapshot, r.PerformedAt, r.Comment,
        r.Analytes != null && r.Analytes.Count > 0 ? r.Analytes.Select(SystemSuitabilityRunAnalyteView.From).ToList() : null,
        r.TheoreticalWeightMg,
        r.MoisturePercent,
        r.StandardWeighInDeviationPercent,
        r.StandardWeighInOutOfWindow,
        r.WeighInJustification,
        r.ComputedRsdPercent);
}

[ApiController]
[Route("api/system-suitability-runs")]
[Authorize]
public class SystemSuitabilityController : ControllerBase
{
    private readonly ISystemSuitabilityService _service;

    public SystemSuitabilityController(ISystemSuitabilityService service)
    {
        _service = service;
    }

    private int CurrentUserId =>
        int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var uid) ? uid : 0;

    private string? ClientIpAddress =>
        HttpContext.Connection.RemoteIpAddress?.ToString();

    // Create a new signed System Suitability Run (REQ-FP-001/001a/002/040/041)
    [Authorize(Policy = PermissionConstants.TestWorkflowExecute)]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSystemSuitabilityRunRequest request)
    {
        var run = await _service.CreateAsync(request, CurrentUserId, ClientIpAddress);
        return Ok(ApiResponse<object>.Ok(SystemSuitabilityRunView.From(run)));
    }

    // List System Suitability Runs with filters and section scoping (REQ-FP-001/040)
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int? testDefinitionId = null,
        [FromQuery] string? testCode = null,
        [FromQuery] bool? passed = null,
        [FromQuery] string? status = null,
        [FromQuery] DateTime? date = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null)
    {
        bool? effectivePassed = passed;
        if (!effectivePassed.HasValue && !string.IsNullOrWhiteSpace(status))
        {
            if (string.Equals(status, "passed", StringComparison.OrdinalIgnoreCase))
                effectivePassed = true;
            else if (string.Equals(status, "failed", StringComparison.OrdinalIgnoreCase))
                effectivePassed = false;
        }

        var filter = new SystemSuitabilityRunFilter(
            TestDefinitionId: testDefinitionId,
            TestCode: testCode,
            Passed: effectivePassed,
            Date: date,
            FromDate: fromDate,
            ToDate: toDate);

        var runs = await _service.GetAllAsync(filter, CurrentUserId);
        return Ok(ApiResponse<object>.Ok(runs.Select(SystemSuitabilityRunView.From)));
    }

    // Get System Suitability Run by ID (scoped)
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var run = await _service.GetByIdAsync(id, CurrentUserId);
        if (run is null)
            return NotFound(ApiResponse<object>.Fail($"System suitability run {id} not found."));

        return Ok(ApiResponse<object>.Ok(SystemSuitabilityRunView.From(run)));
    }

    // Printable run report: the run, its acceptance criteria, signature and
    // every test linked to it.
    [HttpGet("{id:int}/report")]
    public async Task<IActionResult> GetReport(int id)
    {
        var run = await _service.GetByIdAsync(id, CurrentUserId);
        if (run is null)
            return NotFound(ApiResponse<object>.Fail($"System suitability run {id} not found."));

        var details = await _service.GetReportDetailsAsync(id, CurrentUserId);
        return Ok(ApiResponse<object>.Ok(new { Run = SystemSuitabilityRunView.From(run), Details = details }));
    }

    // List passed runs selectable for a given TestOrder (REQ-FP-003)
    [HttpGet("selectable")]
    public async Task<IActionResult> GetSelectable([FromQuery] int testOrderId)
    {
        var runs = await _service.GetSelectableRunsForTestOrderAsync(testOrderId, CurrentUserId);
        return Ok(ApiResponse<object>.Ok(runs.Select(SystemSuitabilityRunView.From)));
    }

    // The run a test order is linked to; data is null when not linked yet.
    [HttpGet("linked")]
    public async Task<IActionResult> GetLinked([FromQuery] int testOrderId)
    {
        var run = await _service.GetLinkedRunForTestOrderAsync(testOrderId, CurrentUserId);
        return Ok(ApiResponse<object?>.Ok(run is null ? null : SystemSuitabilityRunView.From(run)));
    }

    // Link one or more TestOrders to a passed suitability run, all-or-nothing (REQ-FP-003)
    [Authorize(Policy = PermissionConstants.TestWorkflowExecute)]
    [HttpPost("{id:int}/link")]
    public async Task<IActionResult> LinkBulk(int id, [FromBody] BulkLinkOrdersToSuitabilityRunRequest request)
    {
        var orderIds = request.TestOrderIds ?? new List<int>();
        await _service.LinkTestOrdersAsync(id, orderIds, CurrentUserId);
        return Ok(ApiResponse<object>.Ok(new { message = $"Linked {orderIds.Count} test orders to suitability run {id}." }));
    }
}
