using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.DTOs;

public sealed record QualityRecordFilter(
    QualityRecordType? Type = null,
    string? Status = null,
    int? ClassificationId = null,
    int? SectionId = null,
    int? OwnerUserId = null,
    bool? Overdue = null,
    DateTime? From = null,
    DateTime? To = null,
    int Page = 1,
    int PageSize = 25);

public sealed record QualityRecordListItemDto(
    int Id,
    string Number,
    QualityRecordType Type,
    string Title,
    string Status,
    int? SectionId,
    string? SectionName,
    int? ClassificationId,
    string? ClassificationName,
    int? RaisedByUserId,
    string? RaisedByName,
    DateTime RaisedAtUtc,
    int? OwnerUserId,
    string? OwnerName,
    DateOnly? DueDate,
    bool IsOverdue,
    bool IsCancelled,
    uint Version);

public sealed record QualityPathStepDto(
    string StepKey,
    string Label,
    bool IsCompleted,
    bool IsCurrent,
    DateTime? CompletedAtUtc,
    string? CompletedByName);

public sealed record QualitySignedTimelineEntryDto(
    int StepId,
    QualityStepKind Kind,
    string SignerFullName,
    string Username,
    string Role,
    DateTime SignedAtUtc,
    string SignedAtUtcText,
    string Meaning,
    string MeaningText,
    string? Comment);

public sealed record QualityActionDetailDto(
    int Id,
    QualityActionKind Kind,
    string Description,
    int ResponsibleUserId,
    string ResponsibleName,
    DateOnly DueDate,
    DateTime? DoneAtUtc,
    int? DoneByUserId,
    string? DoneByName,
    string? Evidence,
    int? EvidenceLinkId,
    uint Version);

public sealed record QualityDeviationDetailDto(
    QualityDeviationStatus Status,
    DateTime OccurredAtUtc,
    string? Location,
    string ImmediateAction,
    string? ImpactAssessment,
    int? RootCauseCategoryId,
    string? RootCauseCategoryName,
    string? RootCauseDescription,
    string? ProposedActions,
    bool? CapaRequired,
    string? RejectionReason);

public sealed record QualityCapaDetailDto(
    CapaStatus Status,
    int? SourceRecordId,
    string? SourceRecordNumber,
    string? EffectivenessCriteria,
    string? EffectivenessMethod,
    DateOnly? EffectivenessPlannedDate,
    CapaEffectiveness? EffectivenessResult,
    string? EffectivenessFindings,
    int? SuccessorCapaId,
    string? SuccessorCapaNumber);

public sealed record QualityChangeControlDetailDto(
    ChangeControlStatus Status,
    string Reason,
    string? ImpactSummary,
    string? RejectionReason,
    string? PostChangeReview,
    string? UnchangedJustification);

public sealed record QualityComplaintDetailDto(
    ComplaintStatus Status,
    string Source,
    DateOnly ReceivedOn,
    string? ExternalReference,
    int? ItemId,
    string? ItemName,
    string? LotText,
    string ComplaintText,
    string? WhatToTest,
    string? Findings,
    ComplaintOutcome? Outcome,
    string? ConclusionSummary);

public sealed record QualityRecordDetailDto(
    int Id,
    string Number,
    QualityRecordType Type,
    string Title,
    string Description,
    string Status,
    int? SectionId,
    string? SectionName,
    int? ClassificationId,
    string? ClassificationName,
    int? RaisedByUserId,
    string? RaisedByName,
    DateTime RaisedAtUtc,
    int? OwnerUserId,
    string? OwnerName,
    DateOnly? DueDate,
    bool IsOverdue,
    bool IsCancelled,
    string? CancelReason,
    uint Version,
    QualityDeviationDetailDto? Deviation,
    QualityCapaDetailDto? Capa,
    QualityChangeControlDetailDto? ChangeControl,
    QualityComplaintDetailDto? Complaint,
    IReadOnlyList<QualityPathStepDto> PathSteps,
    IReadOnlyList<QualitySignedTimelineEntryDto> Timeline,
    IReadOnlyList<QualityActionDetailDto> Actions,
    IReadOnlyList<QualityAttachmentDto> Attachments,
    int LinksCount);

public sealed record QualityTrendGroupDto(
    string Key,
    string Label,
    int TotalCount,
    int OpenCount,
    int ClosedCount,
    double? AverageTimeToCloseDays,
    double? OnTimeRate,
    double? CapaEffectivenessRate);

public sealed record QualityRepeatCauseDto(
    int CauseCategoryId,
    string CauseCategoryName,
    QualityLinkTargetType TargetType,
    int TargetId,
    string TargetLabel,
    int OccurrenceCount,
    DateTime FirstRaisedAtUtc,
    DateTime LastRaisedAtUtc,
    IReadOnlyList<string> RecordNumbers);

public sealed record QualityTrendReportDto(
    string GroupBy,
    DateTime? FromUtc,
    DateTime? ToUtc,
    int RepeatWindowDays,
    IReadOnlyList<QualityTrendGroupDto> Groups,
    IReadOnlyList<QualityRepeatCauseDto> RepeatCauses);

public sealed record QualityDashboardActionDto(
    int RecordId,
    string RecordNumber,
    QualityRecordType RecordType,
    string RecordTitle,
    int ActionId,
    QualityActionKind Kind,
    string Description,
    DateOnly DueDate,
    bool IsOverdue);

public sealed record QualityDashboardDto(
    IReadOnlyList<QualityRecordListItemDto> WaitingForMyDecision,
    IReadOnlyList<QualityRecordListItemDto> MyOpenRecords,
    IReadOnlyList<QualityDashboardActionDto> MyOpenActions,
    IReadOnlyList<QualityRecordListItemDto> OverdueRecords,
    IReadOnlyList<QualityDashboardActionDto> OverdueActions,
    IReadOnlyList<QualityRecordListItemDto> EffectivenessChecksDue);
