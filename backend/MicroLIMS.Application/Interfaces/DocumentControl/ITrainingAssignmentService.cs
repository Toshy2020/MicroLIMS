using MicroLIMS.Application.DTOs.DocumentControl;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.Application.Interfaces.DocumentControl;

public interface ITrainingAssignmentService
{
    /// <summary>
    /// Processes training and retraining cascade when a document revision becomes Effective.
    /// Handles eligible user determination, automatic assignment creation, idempotency,
    /// retraining ancestry linkage, and closing open assignments on superseded revisions (DC-URS-080, DC-URS-170, DC-URS-172).
    /// </summary>
    Task<TrainingAssignmentGenerationResultDto> ProcessEffectiveRevisionCascadeAsync(
        int effectiveRevisionId,
        int? supersededRevisionId = null,
        DateTime? utcNowOverride = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Idempotently assigns a controlled document revision to specified users.
    /// </summary>
    Task<TrainingAssignmentGenerationResultDto> AssignToUsersAsync(
        ManualTrainingAssignmentRequest request,
        int actingUserId,
        DateTime? utcNowOverride = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Idempotently assigns a controlled document revision to a role or department group.
    /// </summary>
    Task<TrainingAssignmentGenerationResultDto> AssignToGroupAsync(
        BulkGroupAssignmentRequest request,
        int actingUserId,
        DateTime? utcNowOverride = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Handles document obsolescence or voiding: terminally closes open assignments with Cancelled status (DC-URS-175).
    /// </summary>
    Task<int> HandleDocumentObsolescenceAsync(
        int documentMasterId,
        int? documentRevisionId = null,
        string? reason = null,
        int? actingUserId = null,
        DateTime? utcNowOverride = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves the calculated grace period and due date for a specific document and curriculum item.
    /// </summary>
    Task<(int GracePeriodDays, DateTime DueDateUtc)> CalculateDueDateAsync(
        int documentMasterId,
        int? customGracePeriodDays = null,
        DateTime? effectiveDate = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a user completed training on a superseded revision but lacks completion on the current effective revision (DC-URS-114, DC-URS-174).
    /// </summary>
    Task<bool> IsTrainedOnSupersededOnlyAsync(
        int userId,
        int documentMasterId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a single training assignment by ID.
    /// </summary>
    Task<DocumentTrainingAssignmentDto?> GetAssignmentByIdAsync(
        int assignmentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Queries training assignments with filtering and pagination.
    /// </summary>
    Task<PagedResult<DocumentTrainingAssignmentDto>> GetAssignmentsAsync(
        DocumentTrainingAssignmentFilter filter,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves training assignments for a specific user (e.g. My Reading List read side).
    /// </summary>
    Task<IReadOnlyList<DocumentTrainingAssignmentDto>> GetUserAssignmentsAsync(
        int userId,
        TrainingAssignmentStatus? status = null,
        CancellationToken cancellationToken = default);
}
