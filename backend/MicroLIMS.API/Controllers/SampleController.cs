using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Persistence.DbContext;
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
    List<int>? TargetSectionIds);

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
    int? MachineId = null);

public record AssignAnalystRequest(int? AnalystUserId, string? Reason);

public record VoidSampleRequest(string Reason, string Password);

public record AddLaboratoryRequest(int SectionId, string Password, string Reason);

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
    private readonly MicroLimsDbContext _db;

    public SampleController(
        IReceivingService receivingService,
        SampleCorrectionService correctionService,
        SampleAssignmentService assignmentService,
        IUserSectionScopeService scopeService,
        AddLaboratoryService addLaboratory,
        MicroLimsDbContext db)
    {
        _receivingService = receivingService;
        _correctionService = correctionService;
        _assignmentService = assignmentService;
        _scopeService = scopeService;
        _addLaboratory = addLaboratory;
        _db = db;
    }

    private int CurrentUserId => int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

    [HttpPost]
    [Authorize(Roles = RoleConstants.Analyst + "," + RoleConstants.Reviewer + "," + RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    public async Task<IActionResult> Receive(ReceiveItemBasedSampleRequest request)
    {
        try
        {
            // UnauthorizedAccessException (targeting a lab outside the
            // user's scope) is mapped globally by ExceptionMiddleware to
            // 403, so it is deliberately left uncaught here.
            var targets = await ReceiptLabGuard.ResolveTargetsAsync(_db, _scopeService, CurrentUserId,
                User.HasClaim("permission", PermissionConstants.SamplesReceive), request.TargetSectionIds);
            var sample = await _receivingService.ReceiveSampleAsync(new ItemBasedReceiveRequest(
                request.ItemId, request.CauseOfTestingId, request.SampleQuantity, request.SampledBy,
                request.BatchNumber, request.ControlNumber, request.MfgDate, request.ExpDate,
                request.ProductionStage, CurrentUserId, targets));
            return Ok(ApiResponse<object>.Ok(sample));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    // Lists which laboratories the given item's assigned tests belong to,
    // so the Receiving page's lab picker only ever offers real choices.
    [HttpGet("receipt-labs")]
    public async Task<IActionResult> ReceiptLabs([FromQuery] int itemId)
    {
        var codes = await _db.Items.Where(i => i.Id == itemId).SelectMany(i => i.AssignedTests.Select(t => t.TestCode)).ToListAsync();
        var rows = await _db.TestDefinitions.Where(t => codes.Contains(t.Code))
            .GroupBy(t => new { t.SectionId, t.Section!.Code, t.Section.Name })
            .Select(g => new ReceiptLabOptionDto(g.Key.SectionId, g.Key.Code, g.Key.Name, g.Count()))
            .ToListAsync();
        return Ok(ApiResponse<object>.Ok(rows));
    }

    // Analysts record the work; correcting or voiding the sample record is
    // for a Reviewer, Section Head or System Administrator.
    [HttpPut("{id}/correct")]
    [Authorize(Roles = RoleConstants.Reviewer + "," + RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    public async Task<IActionResult> Correct(int id, CorrectSampleRequest request)
    {
        await _scopeService.EnsureSampleAccessAsync(CurrentUserId, id);
        try
        {
            var sample = await _correctionService.CorrectAsync(
                id,
                new SampleCorrectionRequest(
                    request.ControlNumber, request.SampledBy, request.CauseOfTestingId,
                    request.BatchNumber, request.MfgDate, request.ExpDate, request.SampleQuantity,
                    request.ProductionStage, request.PreviousProductName, request.PreviousProductBatchNumber,
                    request.StorageCondition, request.StorageTimeHours,
                    request.ItemId, request.WaterDepartmentId, request.DepartmentId, request.MachineId),
                request.Reason,
                request.Password,
                CurrentUserId,
                HttpContext.Connection.RemoteIpAddress?.ToString());
            return Ok(ApiResponse<object>.Ok(sample));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpPut("{id}/assign-analyst")]
    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    public async Task<IActionResult> AssignAnalyst(int id, AssignAnalystRequest request)
    {
        await _scopeService.EnsureSampleAccessAsync(CurrentUserId, id);
        try
        {
            var sample = await _assignmentService.AssignAnalystAsync(id, request.AnalystUserId, CurrentUserId, request.Reason);
            return Ok(ApiResponse<object>.Ok(sample));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpPost("{id}/void")]
    [Authorize(Roles = RoleConstants.Reviewer + "," + RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    public async Task<IActionResult> Void(int id, [FromBody] VoidSampleRequest request)
    {
        await _scopeService.EnsureSampleAccessAsync(CurrentUserId, id);
        try
        {
            var sample = await _correctionService.VoidAsync(
                id, request.Reason, request.Password, CurrentUserId, HttpContext.Connection.RemoteIpAddress?.ToString());
            return Ok(ApiResponse<object>.Ok(sample));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    // A second laboratory's tests added to a sample already received -
    // same sample, same reference number.
    [HttpPost("{id}/laboratories")]
    [Authorize(Policy = PermissionConstants.SamplesReceive)]
    public async Task<IActionResult> AddLaboratory(int id, AddLaboratoryRequest request)
    {
        try
        {
            await _addLaboratory.AddAsync(id, request.SectionId, CurrentUserId, request.Password, request.Reason,
                HttpContext.Connection.RemoteIpAddress?.ToString());
            return Ok(ApiResponse<object>.Ok(new { }));
        }
        catch (InvalidOperationException ex) { return BadRequest(ApiResponse<object>.Fail(ex.Message)); }
    }
}
