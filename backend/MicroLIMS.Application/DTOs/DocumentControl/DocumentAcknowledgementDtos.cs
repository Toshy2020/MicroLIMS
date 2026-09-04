using System;
using System.Collections.Generic;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.DTOs.DocumentControl;

public record AcknowledgementPresentationDto(
    int AssignmentId,
    int DocumentMasterId,
    string CompanyDocumentCode,
    string DocumentTitle,
    int DocumentRevisionId,
    string RevisionNumber,
    int AssignedUserId,
    string AssignedUserName,
    TrainingAssignmentStatus AssignmentStatus,
    DateTime DueDateUtc,
    int? ControlledFileId,
    string? ControlledFileName,
    string? ControlledFileSha256,
    string LegalStatementText,
    bool CanAcknowledge,
    string? ValidationMessage
);

public record AcknowledgementSubmissionRequest(
    int AssignmentId,
    int DocumentRevisionId,
    bool ConfirmedLegalStatement,
    string? Comments = null,
    string? ClientIpAddress = null,
    string? UserAgent = null
);

public record AcknowledgementResultDto(
    int AcknowledgementRecordId,
    int AssignmentId,
    int DocumentMasterId,
    int DocumentRevisionId,
    string RevisionNumber,
    int UserId,
    string StatementText,
    DateTime AcknowledgedAtUtc,
    TrainingAssignmentStatus Status,
    string? ControlledFileHash
);

public record ReadingProgressUpdateRequest(
    int AssignmentId,
    int DocumentRevisionId,
    int ProgressPercentage,
    int? PageNumber = null
);

public record ReadingProgressResultDto(
    int AssignmentId,
    TrainingAssignmentStatus Status,
    int ProgressPercentage,
    string Note = "Reading progress is strictly informational and does not constitute legal acknowledgement."
);
