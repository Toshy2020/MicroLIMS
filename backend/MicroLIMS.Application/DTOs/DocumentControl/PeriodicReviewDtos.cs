using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.DTOs.DocumentControl;

public record PeriodicReviewTaskDto(
    int Id,
    int DocumentMasterId,
    string MicroLimsDocumentId,
    string CompanyDocumentCode,
    string DocumentTitle,
    int DocumentRevisionId,
    string RevisionNumber,
    int ReviewCycleMonths,
    DateTime ScheduledDueDate,
    bool IsOverdue,
    int? AssignedReviewerUserId,
    string? AssignedReviewerUsername,
    string? AssignedReviewerFullName,
    DateTime CreatedAt,
    PeriodicReviewTaskStatus Status,
    PeriodicReviewOutcome? Outcome,
    DateTime? CompletedAt,
    int? CompletedByUserId,
    string? CompletedByUsername,
    string? CompletedByFullName,
    string? ReviewSummary,
    int TotalFindingsCount,
    List<PeriodicReviewFindingDto> Findings
);

public record PeriodicReviewFindingDto(
    int Id,
    int PeriodicReviewTaskId,
    int? PageNumber,
    string? SectionNumber,
    string NoteText,
    PeriodicReviewFindingStatus Status,
    int CreatedByUserId,
    string CreatedByUsername,
    string CreatedByFullName,
    DateTime CreatedAt
);

public record CreatePeriodicReviewFindingRequest(
    int? PageNumber,
    string? SectionNumber,
    string NoteText
);

public record CompletePeriodicReviewRequest(
    PeriodicReviewOutcome Outcome,
    string ReviewSummary,
    string? ProposedObsolescenceReason = null
);

public record AssignPeriodicReviewerRequest(
    int ReviewerUserId
);

public record PeriodicReviewWorkspaceDto(
    PeriodicReviewTaskDto Task,
    DocumentMasterSummaryDto Master,
    DocumentRevisionDto Revision,
    RevisionFileDto? ControlledPdf,
    List<PeriodicReviewFindingDto> Findings,
    List<PeriodicReviewTaskDto> HistoricalReviews
);

public record PeriodicReviewGenerationResultDto(
    int EvaluatedMastersCount,
    int CreatedTasksCount,
    int SkippedCount,
    List<int> CreatedTaskIds,
    List<string> Errors
);
