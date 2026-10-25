using MicroLIMS.Shared.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

// Shared receiving endpoint for Product / Raw Material / Packaging
// Material - identical shape, only the resolved Item's Category differs.
public record ReceiveItemBasedSampleRequest(
    int ItemId, int CauseOfTestingId, string SampleQuantity, string SampledBy,
    string BatchNumber, string ControlNumber, DateTime? MfgDate, DateTime? ExpDate, string? ProductionStage,
    // Which laboratories to receive the sample for (ReceiptLabGuard resolves
    // and authorizes it before the engine sees it).
    List<int>? TargetSectionIds,
    // Grade stated on the supplier's CoA (required when the item has graded specifications).
    string? Grade = null);

// The sample's full corrected details (see SampleCorrectionRequest), plus the
// reason and the signer's password.
public record CorrectSampleRequest(
    string Reason,
    string Password,
    string ControlNumber,
    string SampledBy,
    int CauseOfTestingId,
    string? BatchNumber = null,
    DateTime? MfgDate = null,
    DateTime? ExpDate = null,
    string? SampleQuantity = null,
    string? ProductionStage = null,
    string? PreviousProductName = null,
    string? PreviousProductBatchNumber = null,
    string? StorageCondition = null,
    int? StorageTimeHours = null,
    int? ItemId = null,
    int? WaterDepartmentId = null,
    int? DepartmentId = null,
    int? MachineId = null,
    string? Grade = null);

public record AssignAnalystRequest(int? AnalystUserId, string? Reason);

public record AssignTestAnalystsRequest(List<TestAnalystAssignment> Assignments, string? Reason);

public record VoidSampleRequest(string Reason, string Password);

public record AddLaboratoryRequest(int SectionId, string Password, string Reason);

public record SelectSampleTestsRequest(List<string> TestCodes, string Password, string? Comment);

[ApiController]
[Route("api/samples")]
[Authorize]
public class SampleController : ControllerBase
{
    private readonly IReceivingService _receivingService;
    private readonly SampleCorrectionService _correctionService;
    private readonly SampleAssignmentService _assignmentService;
    private readonly IUserSectionScopeService _scopeService;
    private readonly AddLaboratoryService _addLaboratory;
    private readonly ReceiptLabService _receiptLabs;
    private readonly SampleTestSelectionService _testSelection;

    public SampleController(
        IReceivingService receivingService,
        SampleCorrectionService correctionService,
        SampleAssignmentService assignmentService,
        IUserSectionScopeService scopeService,
        AddLaboratoryService addLaboratory,
        ReceiptLabService receiptLabs,
        SampleTestSelectionService testSelection)
    {
        _testSelection = testSelection;
        _receivingService = receivingService;
        _correctionService = correctionService;
        _assignmentService = assignmentService;
        _scopeService = scopeService;
        _addLaboratory = addLaboratory;
        _receiptLabs = receiptLabs;
    }

    private int CurrentUserId => int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

    [HttpPost]
    [Authorize(Policy = PermissionConstants.SamplesReceiveOwnLab)]
    public async Task<IActionResult> Receive(ReceiveItemBasedSampleRequest request)
    {
        // UnauthorizedAccessException (targeting a lab outside the
        // user's scope) is mapped globally by ExceptionMiddleware to
        // 403, so it is deliberately left uncaught here.
        var targets = await _receiptLabs.ResolveTargetsAsync(CurrentUserId,
            User.HasClaim("permission", PermissionConstants.SamplesReceive), request.TargetSectionIds);
        var sample = await _receivingService.ReceiveSampleAsync(new ItemBasedReceiveRequest(
            request.ItemId, request.CauseOfTestingId, request.SampleQuantity, request.SampledBy,
            request.BatchNumber, request.ControlNumber, request.MfgDate, request.ExpDate,
            request.ProductionStage, CurrentUserId, targets, request.Grade));
        return Ok(ApiResponse<object>.Ok(sample));
    }

    // Lists which laboratories the given item's assigned tests belong to,
    // so the Receiving page's lab picker only ever offers real choices.
    [HttpGet("receipt-labs")]
    public async Task<IActionResult> ReceiptLabs([FromQuery] int itemId) =>
        Ok(ApiResponse<object>.Ok(await _receiptLabs.GetForItemAsync(itemId, CurrentUserId,
            User.HasClaim("permission", PermissionConstants.SamplesReceive))));

    // Analysts record the work; correcting or voiding the sample record is
    // for a Reviewer, Section Head or System Administrator.
    [HttpPut("{id}/correct")]
    [Authorize(Policy = PermissionConstants.SamplesCorrect)]
    public async Task<IActionResult> Correct(int id, CorrectSampleRequest request)
    {
        await _scopeService.EnsureSampleAccessAsync(CurrentUserId, id);
        var sample = await _correctionService.CorrectAsync(
            id,
            new SampleCorrectionRequest(
                request.ControlNumber, request.SampledBy, request.CauseOfTestingId,
                request.BatchNumber, request.MfgDate, request.ExpDate, request.SampleQuantity,
                request.ProductionStage, request.PreviousProductName, request.PreviousProductBatchNumber,
                request.StorageCondition, request.StorageTimeHours,
                request.ItemId, request.WaterDepartmentId, request.DepartmentId, request.MachineId, request.Grade),
            request.Reason,
            request.Password,
            CurrentUserId,
            HttpContext.Connection.RemoteIpAddress?.ToString());
        return Ok(ApiResponse<object>.Ok(sample));
    }

    [HttpPut("{id}/assign-analyst")]
    [Authorize(Policy = PermissionConstants.SamplesAssignAnalyst)]
    public async Task<IActionResult> AssignAnalyst(int id, AssignAnalystRequest request)
    {
        await _scopeService.EnsureSampleAccessAsync(CurrentUserId, id);
        var sample = await _assignmentService.AssignAnalystAsync(id, request.AnalystUserId, CurrentUserId, request.Reason);
        return Ok(ApiResponse<object>.Ok(sample));
    }

    // Each test on the sample, who is on it, and who the caller may put on it.
    [HttpGet("{id}/assignment-options")]
    [Authorize(Policy = PermissionConstants.SamplesAssignAnalyst)]
    public async Task<IActionResult> AssignmentOptions(int id)
    {
        await _scopeService.EnsureSampleAccessAsync(CurrentUserId, id);
        return Ok(ApiResponse<object>.Ok(await _assignmentService.GetAssignmentOptionsAsync(id, CurrentUserId)));
    }

    // A different analyst per test, saved together.
    [HttpPut("{id}/test-assignments")]
    [Authorize(Policy = PermissionConstants.SamplesAssignAnalyst)]
    public async Task<IActionResult> AssignTestAnalysts(int id, AssignTestAnalystsRequest request)
    {
        await _scopeService.EnsureSampleAccessAsync(CurrentUserId, id);
        var sample = await _assignmentService.AssignTestsAsync(id, request.Assignments ?? new(), CurrentUserId, request.Reason);
        return Ok(ApiResponse<object>.Ok(sample));
    }

    [HttpPost("{id}/void")]
    [Authorize(Policy = PermissionConstants.SamplesCorrect)]
    public async Task<IActionResult> Void(int id, [FromBody] VoidSampleRequest request)
    {
        await _scopeService.EnsureSampleAccessAsync(CurrentUserId, id);
        var sample = await _correctionService.VoidAsync(
            id, request.Reason, request.Password, CurrentUserId, HttpContext.Connection.RemoteIpAddress?.ToString());
        return Ok(ApiResponse<object>.Ok(sample));
    }

    // Periodic retest: the tests of the laboratories still pending; canSelect marks those the caller may choose
    // (read-only for everyone else with access to the sample).
    [HttpGet("{id}/test-selection")]
    public async Task<IActionResult> GetTestSelection(int id, CancellationToken ct)
    {
        await _scopeService.EnsureSampleAccessAsync(CurrentUserId, id, ct);
        return Ok(ApiResponse<object>.Ok(await _testSelection.GetEligibleAsync(id, CurrentUserId, ct)));
    }

    [HttpPost("{id}/test-selection")]
    [Authorize(Policy = PermissionConstants.SamplesCorrect)]
    public async Task<IActionResult> SelectTests(int id, SelectSampleTestsRequest request, CancellationToken ct)
    {
        await _testSelection.SelectAsync(id, new SelectTestsRequest(request.TestCodes ?? new(), request.Password, request.Comment),
            CurrentUserId, HttpContext.Connection.RemoteIpAddress?.ToString(), ct);
        return Ok(ApiResponse<object>.Ok(new { }));
    }

    // A second laboratory's tests added to a sample already received -
    // same sample, same reference number.
    [HttpPost("{id}/laboratories")]
    [Authorize(Policy = PermissionConstants.SamplesReceive)]
    public async Task<IActionResult> AddLaboratory(int id, AddLaboratoryRequest request)
    {
        await _addLaboratory.AddAsync(id, request.SectionId, CurrentUserId, request.Password, request.Reason,
            HttpContext.Connection.RemoteIpAddress?.ToString());
        return Ok(ApiResponse<object>.Ok(new { }));
    }
}
