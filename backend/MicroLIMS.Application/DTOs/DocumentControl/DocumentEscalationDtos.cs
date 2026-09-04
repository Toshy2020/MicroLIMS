using System;
using System.Collections.Generic;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.DTOs.DocumentControl;

public record EscalationProcessingResultDto(
    DateTime EvaluationTimestampUtc,
    int TotalEvaluatedCount,
    int TotalEscalationsCreated,
    int ApproachingDueCreated,
    int DueCreated,
    int OverdueCreated,
    int SkippedAlreadyEscalatedCount,
    int SkippedExemptOrTerminalCount,
    int FailedCount,
    IReadOnlyList<int> CreatedEscalationRecordIds,
    IReadOnlyList<string> Errors
);

public record DocumentEscalationSummaryDto(
    int Id,
    int DocumentTrainingAssignmentId,
    int DocumentMasterId,
    string CompanyDocumentCode,
    string DocumentTitle,
    int DocumentRevisionId,
    string RevisionNumber,
    int AssignedUserId,
    string AssignedUserName,
    DateTime DueDateUtc,
    DocumentEscalationLevel EscalationLevel,
    DocumentEscalationStatus Status,
    DateTime ScheduledTriggerUtc,
    DateTime ExecutedAtUtc,
    string RecipientRoleOrTarget,
    string EscalationReason,
    DateTime? ResolvedAtUtc,
    int? ResolvedByUserId,
    string? ResolvedByUserName,
    string? ResolutionReason,
    string ProcessName
);

public record ResolveEscalationRequest(
    int EscalationRecordId,
    string ResolutionReason
);

public record OverdueTrainingSummaryDto(
    int OverdueAssignmentsCount,
    int EscalatedCount,
    IReadOnlyList<OverdueAssignmentItemDto> OverdueItems
);

public record OverdueAssignmentItemDto(
    int AssignmentId,
    int DocumentMasterId,
    string CompanyDocumentCode,
    string DocumentTitle,
    int DocumentRevisionId,
    string RevisionNumber,
    int AssignedUserId,
    string AssignedUserName,
    DateTime DueDateUtc,
    double DaysOverdue,
    TrainingAssignmentStatus Status,
    DocumentEscalationLevel? CurrentEscalationLevel
);
