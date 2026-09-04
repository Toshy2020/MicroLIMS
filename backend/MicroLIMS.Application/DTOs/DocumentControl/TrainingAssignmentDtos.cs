using System;
using System.Collections.Generic;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.Application.DTOs.DocumentControl;

public record TrainingAssignmentGenerationResultDto(
    int DocumentMasterId,
    int DocumentRevisionId,
    int TotalEligibleUsers,
    int NewAssignmentsCreated,
    int ExistingAssignmentsSkipped,
    int SupersededAssignmentsClosed,
    List<int> CreatedAssignmentIds,
    List<string> ErrorMessages
);

public record ManualTrainingAssignmentRequest(
    int DocumentRevisionId,
    List<int> UserIds,
    AssignmentType AssignmentType = AssignmentType.Reading,
    int? CustomGracePeriodDays = null,
    string? AssignmentReason = null
);

public record BulkGroupAssignmentRequest(
    int DocumentRevisionId,
    int? RoleId = null,
    int? DepartmentId = null,
    AssignmentType AssignmentType = AssignmentType.Reading,
    int? CustomGracePeriodDays = null,
    string? AssignmentReason = null
);

public record DocumentTrainingAssignmentDto(
    int Id,
    int DocumentMasterId,
    string MicroLimsDocumentId,
    string CompanyDocumentCode,
    string DocumentTitle,
    int DocumentRevisionId,
    string RevisionNumber,
    int AssignedUserId,
    string AssignedUserName,
    AssignmentType AssignmentType,
    TrainingAssignmentStatus Status,
    DateTime AssignedDateUtc,
    DateTime DueDateUtc,
    DateTime? AcknowledgedAtUtc,
    string? StatementText,
    int? AcknowledgedByUserId,
    string? AcknowledgedByUserName,
    DateTime? CompletedAtUtc,
    DateTime? SupersededAtUtc,
    string? ClosedReason,
    int? SourceAssignmentId,
    string? AssignmentReason,
    int CreatedByUserId,
    string CreatedByUserName,
    DateTime CreatedAtUtc,
    bool IsOverdue,
    double DaysRemainingOrOverdue
);

public record DocumentTrainingAssignmentFilter(
    int? DocumentMasterId = null,
    int? DocumentRevisionId = null,
    int? AssignedUserId = null,
    TrainingAssignmentStatus? Status = null,
    AssignmentType? AssignmentType = null,
    bool? OverdueOnly = null,
    int Page = 1,
    int PageSize = 50
);
