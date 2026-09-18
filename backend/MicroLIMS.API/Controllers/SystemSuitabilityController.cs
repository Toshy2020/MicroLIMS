using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

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
        return Ok(ApiResponse<object>.Ok(run));
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
        return Ok(ApiResponse<object>.Ok(runs));
    }

    // Get System Suitability Run by ID (scoped)
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var run = await _service.GetByIdAsync(id, CurrentUserId);
        if (run is null)
            return NotFound(ApiResponse<object>.Fail($"System suitability run {id} not found."));

        return Ok(ApiResponse<object>.Ok(run));
    }

    // List passed runs selectable for a given TestOrder (REQ-FP-003)
    [HttpGet("selectable")]
    public async Task<IActionResult> GetSelectable([FromQuery] int testOrderId)
    {
        var runs = await _service.GetSelectableRunsForTestOrderAsync(testOrderId, CurrentUserId);
        return Ok(ApiResponse<object>.Ok(runs));
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
