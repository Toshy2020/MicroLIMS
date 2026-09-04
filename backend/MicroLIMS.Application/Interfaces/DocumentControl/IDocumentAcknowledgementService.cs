using System;
using System.Threading;
using System.Threading.Tasks;
using MicroLIMS.Application.DTOs.DocumentControl;

namespace MicroLIMS.Application.Interfaces.DocumentControl;

public interface IDocumentAcknowledgementService
{
    /// <summary>
    /// Presents the controlled acknowledgement context, including document identity, assigned revision,
    /// controlled PDF file metadata, configured legal statement, and server-side eligibility checks (DC-URS-086, DC-URS-191, DC-URS-192).
    /// </summary>
    Task<AcknowledgementPresentationDto> PresentAcknowledgementContextAsync(
        int assignmentId,
        int actingUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the formal, conscious, read-and-understand acknowledgement ceremony for an assigned revision.
    /// Validates ownership, revision binding, active document state, controlled file integrity,
    /// persists immutable evidentiary record, updates assignment status, and logs audit events (DC-URS-086, DC-URS-189, DC-URS-191, DC-URS-192).
    /// </summary>
    Task<AcknowledgementResultDto> AcknowledgeAssignmentAsync(
        AcknowledgementSubmissionRequest request,
        int actingUserId,
        DateTime? utcNowOverride = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the legal acknowledgement status and historical evidence record for an assignment.
    /// </summary>
    Task<AcknowledgementResultDto?> GetAcknowledgementStatusAsync(
        int assignmentId,
        int actingUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Records informational reading progress (scroll percentage, pages viewed).
    /// Progress is strictly informational and does NOT satisfy legal acknowledgement (DC-URS-190).
    /// Transitions status from Assigned to Reading.
    /// </summary>
    Task<ReadingProgressResultDto> RecordReadingProgressAsync(
        ReadingProgressUpdateRequest request,
        int actingUserId,
        DateTime? utcNowOverride = null,
        CancellationToken cancellationToken = default);
}
