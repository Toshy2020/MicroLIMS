using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
    string BatchNumber, string ControlNumber, DateTime? MfgDate, DateTime? ExpDate, string? ProductionStage);

public record CorrectSampleRequest(
    string? BatchNumber,
    string? ControlNumber,
    string? PreviousProductName = null,
    string? PreviousProductBatchNumber = null
);

public record AssignAnalystRequest(int? AnalystUserId, string? Reason);

public record VoidSampleRequest(string Reason);

[ApiController]
[Route("api/samples")]
[Authorize]
public class SampleController : ControllerBase
{
    private readonly IReceivingService _receivingService;
    private readonly SampleCorrectionService _correctionService;
    private readonly SampleAssignmentService _assignmentService;

    public SampleController(
        IReceivingService receivingService,
        SampleCorrectionService correctionService,
        SampleAssignmentService assignmentService)
    {
        _receivingService = receivingService;
        _correctionService = correctionService;
        _assignmentService = assignmentService;
    }

    private int CurrentUserId => int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

    [HttpPost]
    [Authorize(Roles = RoleConstants.Analyst + "," + RoleConstants.Reviewer + "," + RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    public async Task<IActionResult> Receive(ReceiveItemBasedSampleRequest request)
    {
        try
        {
            var sample = await _receivingService.ReceiveSampleAsync(new ItemBasedReceiveRequest(
                request.ItemId, request.CauseOfTestingId, request.SampleQuantity, request.SampledBy,
                request.BatchNumber, request.ControlNumber, request.MfgDate, request.ExpDate,
                request.ProductionStage, CurrentUserId));
            return Ok(ApiResponse<object>.Ok(sample));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpPut("{id}/correct")]
    [Authorize(Roles = RoleConstants.Analyst + "," + RoleConstants.Reviewer + "," + RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    public async Task<IActionResult> Correct(int id, CorrectSampleRequest request)
    {
        try
        {
            var sample = await _correctionService.CorrectAsync(
                id,
                request.BatchNumber,
                request.ControlNumber,
                request.PreviousProductName,
                request.PreviousProductBatchNumber);
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
    [Authorize(Roles = RoleConstants.Analyst + "," + RoleConstants.Reviewer + "," + RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    public async Task<IActionResult> Void(int id, [FromBody] VoidSampleRequest request)
    {
        try
        {
            var sample = await _correctionService.VoidAsync(id, request.Reason, CurrentUserId);
            return Ok(ApiResponse<object>.Ok(sample));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }
}
