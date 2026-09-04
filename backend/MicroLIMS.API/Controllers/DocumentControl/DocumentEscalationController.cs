using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.DTOs.DocumentControl;
using MicroLIMS.Application.Interfaces.DocumentControl;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers.DocumentControl;

[ApiController]
[Authorize]
[Route("api/document-control/escalations")]
public class DocumentEscalationController : ControllerBase
{
    private readonly IDocumentEscalationService _escalationService;

    public DocumentEscalationController(IDocumentEscalationService escalationService)
    {
        _escalationService = escalationService;
    }

    private int CurrentUserId =>
        int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

    private bool IsAdminOrSectionHead =>
        User.IsInRole(nameof(RoleType.SystemAdministrator)) || User.IsInRole(nameof(RoleType.SectionHead));

    /// <summary>
    /// Retrieves historical escalation records for a specific training assignment.
    /// </summary>
    [HttpGet("assignments/{assignmentId:int}/history")]
    public async Task<IActionResult> GetAssignmentEscalationHistory(int assignmentId)
    {
        var history = await _escalationService.GetAssignmentEscalationHistoryAsync(assignmentId);
        return Ok(ApiResponse<IReadOnlyList<DocumentEscalationSummaryDto>>.Ok(history));
    }

    /// <summary>
    /// Retrieves organizational overdue and escalated training summary (Privileged manager query).
    /// </summary>
    [HttpGet("summary/overdue")]
    public async Task<IActionResult> GetOverdueTrainingSummary()
    {
        if (!IsAdminOrSectionHead)
        {
            return StatusCode(403, ApiResponse<object>.Fail("Only Section Heads and System Administrators can view the overdue training summary."));
        }

        var summary = await _escalationService.GetOverdueTrainingSummaryAsync();
        return Ok(ApiResponse<OverdueTrainingSummaryDto>.Ok(summary));
    }

    /// <summary>
    /// Triggers immediate evaluation of due escalations (Privileged administrative execution).
    /// </summary>
    [HttpPost("process-due")]
    public async Task<IActionResult> ProcessDueEscalations()
    {
        if (!IsAdminOrSectionHead)
        {
            return StatusCode(403, ApiResponse<object>.Fail("Only Section Heads and System Administrators can manually trigger escalation processing."));
        }

        var result = await _escalationService.ProcessDueEscalationsAsync(
            processName: $"ManualTrigger:{CurrentUserId}");

        return Ok(ApiResponse<EscalationProcessingResultDto>.Ok(result, "Escalation evaluation completed."));
    }

    /// <summary>
    /// Resolves an open escalation record with an attributable regulatory reason (Privileged manager action).
    /// </summary>
    [HttpPost("resolve")]
    public async Task<IActionResult> ResolveEscalation([FromBody] ResolveEscalationRequest request)
    {
        if (!IsAdminOrSectionHead)
        {
            return StatusCode(403, ApiResponse<object>.Fail("Only Section Heads and System Administrators can resolve escalation records."));
        }

        try
        {
            var result = await _escalationService.ResolveEscalationAsync(request, CurrentUserId);
            return Ok(ApiResponse<DocumentEscalationSummaryDto>.Ok(result, "Escalation resolved successfully."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }
}
