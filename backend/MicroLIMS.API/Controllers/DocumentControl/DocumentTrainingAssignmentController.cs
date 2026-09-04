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
[Route("api/document-control/training-assignments")]
public class DocumentTrainingAssignmentController : ControllerBase
{
    private readonly ITrainingAssignmentService _trainingService;

    public DocumentTrainingAssignmentController(ITrainingAssignmentService trainingService)
    {
        _trainingService = trainingService;
    }

    private int CurrentUserId =>
        int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

    private bool IsAdminOrSectionHead =>
        User.IsInRole(nameof(RoleType.SystemAdministrator)) || User.IsInRole(nameof(RoleType.SectionHead));

    /// <summary>
    /// Queries training assignments with filtering and pagination (Admin/SectionHead or Own Records).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAssignments([FromQuery] DocumentTrainingAssignmentFilter filter)
    {
        // Non-admin/section head can only query their own assignments
        if (!IsAdminOrSectionHead)
        {
            filter = filter with { AssignedUserId = CurrentUserId };
        }

        var result = await _trainingService.GetAssignmentsAsync(filter);
        return Ok(ApiResponse<PagedResult<DocumentTrainingAssignmentDto>>.Ok(result));
    }

    /// <summary>
    /// Retrieves a single training assignment by ID.
    /// </summary>
    [HttpGet("{assignmentId:int}")]
    public async Task<IActionResult> GetAssignmentById(int assignmentId)
    {
        var assignment = await _trainingService.GetAssignmentByIdAsync(assignmentId);
        if (assignment == null)
            return NotFound(ApiResponse<object>.Fail($"Training assignment {assignmentId} not found."));

        // Non-admin can only access their own assignment
        if (!IsAdminOrSectionHead && assignment.AssignedUserId != CurrentUserId)
        {
            return StatusCode(403, ApiResponse<object>.Fail("You are not authorized to view this training assignment."));
        }

        return Ok(ApiResponse<DocumentTrainingAssignmentDto>.Ok(assignment));
    }

    /// <summary>
    /// Retrieves active training assignments for the authenticated user (Personal Reading List read-side).
    /// </summary>
    [HttpGet("my-assignments")]
    public async Task<IActionResult> GetMyAssignments([FromQuery] TrainingAssignmentStatus? status = null)
    {
        var assignments = await _trainingService.GetUserAssignmentsAsync(CurrentUserId, status);
        return Ok(ApiResponse<IReadOnlyList<DocumentTrainingAssignmentDto>>.Ok(assignments));
    }

    /// <summary>
    /// Retrieves training assignments for a specific user (Privileged query).
    /// </summary>
    [HttpGet("users/{userId:int}")]
    public async Task<IActionResult> GetUserAssignments(int userId, [FromQuery] TrainingAssignmentStatus? status = null)
    {
        if (!IsAdminOrSectionHead && userId != CurrentUserId)
        {
            return StatusCode(403, ApiResponse<object>.Fail("You are not authorized to view training assignments for other users."));
        }

        var assignments = await _trainingService.GetUserAssignmentsAsync(userId, status);
        return Ok(ApiResponse<IReadOnlyList<DocumentTrainingAssignmentDto>>.Ok(assignments));
    }

    /// <summary>
    /// Manually assigns a controlled document revision to specified users (Privileged action).
    /// </summary>
    [HttpPost("manual-assign")]
    public async Task<IActionResult> AssignToUsers([FromBody] ManualTrainingAssignmentRequest request)
    {
        if (!IsAdminOrSectionHead)
        {
            return StatusCode(403, ApiResponse<object>.Fail("Only Section Heads and System Administrators can assign training."));
        }

        try
        {
            var result = await _trainingService.AssignToUsersAsync(request, CurrentUserId);
            return Ok(ApiResponse<TrainingAssignmentGenerationResultDto>.Ok(result, "Training assignments processed successfully."));
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

    /// <summary>
    /// Assigns a controlled document revision to a role or department group (Privileged action).
    /// </summary>
    [HttpPost("bulk-group-assign")]
    public async Task<IActionResult> AssignToGroup([FromBody] BulkGroupAssignmentRequest request)
    {
        if (!IsAdminOrSectionHead)
        {
            return StatusCode(403, ApiResponse<object>.Fail("Only Section Heads and System Administrators can assign training to groups."));
        }

        try
        {
            var result = await _trainingService.AssignToGroupAsync(request, CurrentUserId);
            return Ok(ApiResponse<TrainingAssignmentGenerationResultDto>.Ok(result, "Bulk training assignments processed successfully."));
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

    /// <summary>
    /// Calculates the due date and grace period for a document master (Reference endpoint).
    /// </summary>
    [HttpGet("calculate-due-date")]
    public async Task<IActionResult> CalculateDueDate([FromQuery] int documentMasterId, [FromQuery] int? customGracePeriodDays = null)
    {
        var (graceDays, dueDateUtc) = await _trainingService.CalculateDueDateAsync(documentMasterId, customGracePeriodDays);
        return Ok(ApiResponse<object>.Ok(new { gracePeriodDays = graceDays, dueDateUtc }));
    }

    /// <summary>
    /// Checks if a user is qualified on a superseded revision only.
    /// </summary>
    [HttpGet("superseded-gap-check")]
    public async Task<IActionResult> CheckSupersededGap([FromQuery] int userId, [FromQuery] int documentMasterId)
    {
        if (!IsAdminOrSectionHead && userId != CurrentUserId)
        {
            return StatusCode(403, ApiResponse<object>.Fail("Unauthorized gap check access."));
        }

        var isGap = await _trainingService.IsTrainedOnSupersededOnlyAsync(userId, documentMasterId);
        return Ok(ApiResponse<object>.Ok(new { userId, documentMasterId, isTrainedOnSupersededOnly = isGap }));
    }
}
