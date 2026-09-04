using System;
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
[Route("api/document-control/training-matrix")]
public class DocumentTrainingMatrixController : ControllerBase
{
    private readonly ITrainingMatrixService _matrixService;

    public DocumentTrainingMatrixController(ITrainingMatrixService matrixService)
    {
        _matrixService = matrixService;
    }

    private int CurrentUserId =>
        int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

    private bool IsAdminOrSectionHead =>
        User.IsInRole(nameof(RoleType.SystemAdministrator)) || User.IsInRole(nameof(RoleType.SectionHead));

    /// <summary>
    /// Retrieves multi-axis Training Matrix grid (Privileged access: SystemAdministrator, SectionHead).
    /// </summary>
    [HttpGet("grid")]
    public async Task<IActionResult> GetTrainingMatrixGrid([FromQuery] TrainingMatrixFilterDto filter)
    {
        if (!IsAdminOrSectionHead)
        {
            // Non-administrative users can only see their own row in the matrix
            filter = filter with { UserId = CurrentUserId };
        }

        var grid = await _matrixService.GetTrainingMatrixGridAsync(filter);
        return Ok(ApiResponse<TrainingMatrixGridDto>.Ok(grid));
    }

    /// <summary>
    /// Retrieves organizational compliance KPIs, department ranking, and top overdue documents.
    /// </summary>
    [HttpGet("kpis")]
    public async Task<IActionResult> GetComplianceKpis([FromQuery] int? departmentId = null)
    {
        var kpis = await _matrixService.GetComplianceKpisAsync(departmentId);
        return Ok(ApiResponse<ComplianceKpiSummaryDto>.Ok(kpis));
    }

    /// <summary>
    /// Retrieves user-level drilldown compliance across all required/assigned documents.
    /// </summary>
    [HttpGet("users/{userId:int}")]
    public async Task<IActionResult> GetUserCompliance(int userId)
    {
        if (!IsAdminOrSectionHead && userId != CurrentUserId)
        {
            return StatusCode(403, ApiResponse<object>.Fail("You are not authorized to view compliance details for other users."));
        }

        var detail = await _matrixService.GetUserComplianceDetailAsync(userId);
        if (detail == null)
            return NotFound(ApiResponse<object>.Fail($"User {userId} not found."));

        return Ok(ApiResponse<UserComplianceDetailDto>.Ok(detail));
    }

    /// <summary>
    /// Retrieves document-level drilldown compliance across all assigned personnel.
    /// </summary>
    [HttpGet("documents/{documentMasterId:int}")]
    public async Task<IActionResult> GetDocumentCompliance(int documentMasterId)
    {
        var detail = await _matrixService.GetDocumentComplianceDetailAsync(documentMasterId);
        if (detail == null)
            return NotFound(ApiResponse<object>.Fail($"Document {documentMasterId} not found."));

        return Ok(ApiResponse<DocumentComplianceDetailDto>.Ok(detail));
    }
}
