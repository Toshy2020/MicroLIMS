using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.DTOs.DocumentControl;
using MicroLIMS.Application.Interfaces.DocumentControl;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers.DocumentControl;

[ApiController]
[Authorize]
[Route("api/document-control")]
public class DocumentApprovalController : ControllerBase
{
    private readonly IDocumentApprovalService _approvalService;

    public DocumentApprovalController(IDocumentApprovalService approvalService)
    {
        _approvalService = approvalService;
    }

    private int CurrentUserId =>
        int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

    [HttpPost("revisions/{revisionId:int}/approvals")]
    public async Task<IActionResult> CreateApprovalTask(int revisionId, [FromBody] CreateApprovalTaskRequest request)
    {
        try
        {
            var result = await _approvalService.CreateApprovalTaskAsync(revisionId, request, CurrentUserId);
            return CreatedAtAction(nameof(GetApprovalTask), new { approvalTaskId = result.Id },
                ApiResponse<DocumentApprovalTaskDto>.Ok(result, "Approval task created successfully."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, ApiResponse<object>.Fail(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpGet("revisions/{revisionId:int}/active-approval")]
    public async Task<IActionResult> GetActiveApprovalTask(int revisionId)
    {
        try
        {
            var task = await _approvalService.GetActiveApprovalTaskForRevisionAsync(revisionId, CurrentUserId);
            return Ok(ApiResponse<DocumentApprovalTaskDto?>.Ok(task));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpGet("approvals/{approvalTaskId:int}")]
    public async Task<IActionResult> GetApprovalTask(int approvalTaskId)
    {
        try
        {
            var task = await _approvalService.GetApprovalTaskByIdAsync(approvalTaskId, CurrentUserId);
            return Ok(ApiResponse<DocumentApprovalTaskDto>.Ok(task));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpGet("approvals")]
    public async Task<IActionResult> GetApprovalTasks(
        [FromQuery] int? revisionId,
        [FromQuery] int? approverUserId,
        [FromQuery] DocumentApprovalTaskStatus? status)
    {
        try
        {
            var tasks = await _approvalService.GetApprovalTasksAsync(revisionId, approverUserId, status, CurrentUserId);
            return Ok(ApiResponse<IReadOnlyList<DocumentApprovalTaskDto>>.Ok(tasks));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpGet("approvals/{approvalTaskId:int}/dossier")]
    public async Task<IActionResult> GetApprovalDossier(int approvalTaskId)
    {
        try
        {
            var dossier = await _approvalService.GetApprovalDossierAsync(approvalTaskId, CurrentUserId);
            return Ok(ApiResponse<ApprovalDossierDto>.Ok(dossier));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, ApiResponse<object>.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpGet("approvals/{approvalTaskId:int}/readiness")]
    public async Task<IActionResult> ValidateApprovalReadiness(int approvalTaskId)
    {
        try
        {
            var readiness = await _approvalService.ValidateApprovalReadinessAsync(approvalTaskId, CurrentUserId);
            return Ok(ApiResponse<ApprovalReadinessDto>.Ok(readiness));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpPost("approvals/{approvalTaskId:int}/decide")]
    public async Task<IActionResult> ExecuteApprovalDecision(int approvalTaskId, [FromBody] ExecuteApprovalDecisionRequest request)
    {
        try
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            var result = await _approvalService.ExecuteApprovalDecisionAsync(approvalTaskId, request, CurrentUserId, ip);
            return Ok(ApiResponse<DocumentApprovalTaskDto>.Ok(result, $"Approval decision '{request.Decision}' executed successfully."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, ApiResponse<object>.Fail(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpPost("approvals/{approvalTaskId:int}/sign")]
    public async Task<IActionResult> SignApproval(int approvalTaskId, [FromBody] SignApprovalRequest request)
    {
        try
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            var decideRequest = new ExecuteApprovalDecisionRequest(
                Decision: DocumentApprovalDecision.Approve,
                DecisionNotes: request.DecisionNotes,
                EffectiveDate: request.EffectiveDate,
                Password: request.Password
            );

            var result = await _approvalService.ExecuteApprovalDecisionAsync(approvalTaskId, decideRequest, CurrentUserId, ip);
            return Ok(ApiResponse<DocumentApprovalTaskDto>.Ok(result, "21 CFR Part 11 Electronic signature applied and revision approved successfully."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, ApiResponse<object>.Fail(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpGet("approvals/{approvalTaskId:int}/signature")]
    public async Task<IActionResult> GetApprovalSignature(int approvalTaskId)
    {
        try
        {
            var signature = await _approvalService.GetApprovalSignatureAsync(approvalTaskId, CurrentUserId);
            return Ok(ApiResponse<ElectronicSignatureDto?>.Ok(signature));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }
}
