using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Services.Oos;

public sealed record OosInvestigationFilter(
    OosInvestigationStatus? Status = null,
    OosInvestigationKind? Kind = null,
    int? SectionId = null,
    bool? Overdue = null,
    DateTime? From = null,
    DateTime? To = null,
    int Page = 1,
    int PageSize = 25,
    bool? OpenOnly = null);

public sealed record OosInvestigationListItemDto(
    int Id,
    string Number,
    OosInvestigationKind Kind,
    OosInvestigationStatus Status,
    int SampleId,
    string SampleReferenceNumber,
    string SampleDisplayName,
    string? BatchNumber,
    int SectionId,
    string SectionName,
    DateTime OpenedAtUtc,
    string OpenedByName,
    DateOnly? PhaseOneDueDate,
    DateOnly? DueDate,
    bool IsOverdue,
    OosConclusion? Conclusion,
    DateTime? ConcludedAtUtc,
    string? RootCauseCategoryName,
    IReadOnlyList<string> TestCodes,
    uint Version);

public sealed record OosChecklistTemplateItemDto(int Phase, string Key, int Order, string Text);

public sealed record OosInvestigationResultDto(
    int Id,
    int TestOrderId,
    string TestCode,
    string TestDisplayName,
    OosOutcomeClass OutcomeAtOpening,
    string ResultSummaryAtOpening,
    DateTime AddedAtUtc,
    bool IsInvalidated,
    DateTime? InvalidatedAtUtc);

public sealed record OosInvestigationEntryDto(
    int Id,
    int Phase,
    OosEntryKind Kind,
    string? ChecklistItemKey,
    OosChecklistAnswer? Answer,
    string Text,
    int? ParentEntryId,
    int RecordedByUserId,
    string RecordedByName,
    DateTime RecordedAtUtc);

public sealed record OosRetestPlanSampleDto(int SampleId, string ReferenceNumber, SampleStatus Status);

public sealed record OosRetestPlanDetailDto(
    int Id,
    int Revision,
    OosRetestSource Source,
    int RetestCount,
    IReadOnlyList<int> TestOrderIds,
    IReadOnlyList<int> AnalystUserIds,
    string JudgingRule,
    string? AmendmentReason,
    DateTime? SignedAtUtc,
    int? SignedByUserId,
    string? SignedByName,
    DateTime? SamplesCreatedAtUtc,
    IReadOnlyList<OosRetestPlanSampleDto> Samples);

public sealed record OosInvestigationReopeningDto(
    int Id,
    string Reason,
    OosConclusion PreviousConclusion,
    string? PreviousRootCauseCategoryName,
    string? PreviousRootCauseDescription,
    string? PreviousConclusionSummary,
    string? PreviousConcludedByName,
    DateTime? PreviousConcludedAtUtc,
    int ReopenedByUserId,
    string ReopenedByName,
    DateTime ReopenedAtUtc);

public sealed record OosDeviationRequestDetailDto(
    int Id,
    string Reason,
    int RequestedByUserId,
    string RequestedByName,
    DateTime RequestedAtUtc,
    OosDeviationRequestStatus Status,
    string? AddressedComment,
    int? AddressedByUserId,
    string? AddressedByName,
    DateTime? AddressedAtUtc,
    uint Version);

public sealed record OosSignedTimelineEntryDto(
    string SignerFullName,
    string Username,
    string Role,
    DateTime SignedAtUtc,
    string SignedAtUtcText,
    string Meaning,
    string MeaningText,
    string? Comment);

public sealed record OosInvestigationDetailDto(
    int Id,
    string Number,
    OosInvestigationKind Kind,
    OosInvestigationStatus Status,
    int SampleId,
    string SampleReferenceNumber,
    string SampleCategory,
    string SampleDisplayName,
    string? BatchNumber,
    int SectionId,
    string SectionName,
    DateTime OpenedAtUtc,
    int OpenedByUserId,
    string OpenedByName,
    DateOnly? PhaseOneDueDate,
    DateOnly? DueDate,
    bool IsOverdue,
    OosPhaseOneAssessment? PhaseOneAssessment,
    int? PhaseOneCauseCategoryId,
    string? PhaseOneCauseCategoryName,
    string? PhaseOneCauseDescription,
    DateTime? PhaseOneSignedAtUtc,
    int? PhaseOneSignedByUserId,
    string? PhaseOneSignedByName,
    OosConclusion? Conclusion,
    int? RootCauseCategoryId,
    string? RootCauseCategoryName,
    string? RootCauseDescription,
    string? ConclusionSummary,
    DateTime? ConcludedAtUtc,
    int? ConcludedByUserId,
    string? ConcludedByName,
    uint Version,
    IReadOnlyList<OosChecklistTemplateItemDto> ChecklistItems,
    IReadOnlyList<OosInvestigationResultDto> Results,
    IReadOnlyList<OosInvestigationEntryDto> Entries,
    IReadOnlyList<OosRetestPlanDetailDto> RetestPlans,
    IReadOnlyList<OosInvestigationReopeningDto> Reopenings,
    IReadOnlyList<OosDeviationRequestDetailDto> DeviationRequests,
    IReadOnlyList<OosSignedTimelineEntryDto> Timeline);

public sealed record OosTrendGroupDto(
    string Key,
    string Label,
    int TotalCount,
    int OpenCount,
    int ConcludedCount,
    int ConfirmedCount,
    int InvalidatedCount,
    int ExcursionClosedCount,
    double? MedianWorkingDaysToConclude);

public sealed record OosTrendReportDto(
    string GroupBy,
    DateTime? FromUtc,
    DateTime? ToUtc,
    IReadOnlyList<OosTrendGroupDto> Groups);
