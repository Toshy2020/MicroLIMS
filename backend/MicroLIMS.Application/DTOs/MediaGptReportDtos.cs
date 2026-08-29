using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.DTOs;

public record MediaGptSearchRequest(
    string? Search = null,
    string? MediaType = null,
    EvaluationType? EvaluationType = null,
    EvaluationOutcome? Outcome = null,
    ApprovalGateStatus? ApprovalStatus = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    int Page = 1,
    int PageSize = 25,
    string SortBy = "PreparedAt",
    bool SortDescending = true);

public record MediaGptListDto(
    int Id,
    string LotNumber,
    string MediaType,
    DateTime PreparedAt,
    DateTime ExpiryDate,
    string EvaluationType,
    string EvaluationStatus,
    string? EvaluationOutcome,
    string ApprovalStatus,
    bool IsReleasedForUse,
    string PreparedByName,
    string? ApprovedByName,
    DateTime? ApprovedAt,
    int ChallengeCount,
    int ConformedChallengeCount);

public record MediaGptSearchResult(List<MediaGptListDto> Items, int TotalCount, int Page, int PageSize);

public record MediaGptChallengeDetailDto(
    int Id,
    string OrganismName,
    string? AtccNumber,
    string? ChallengeRole,
    string? StrainSource,
    string InitialInoculum,
    decimal? OldMediaCount,
    decimal? NewMediaCount,
    decimal? RecoveryPercent,
    decimal? ExpectedMinRecoveryPercent,
    decimal? ExpectedMaxRecoveryPercent,
    string? ReferenceMediaLot,
    bool? GrowthObserved,
    string? ObservedDescription,
    string? ExpectedDescription,
    bool? IsTurbid,
    string? Outcome,
    string? ReadByName,
    DateTime? ReadAt);

public record MediaGptDetailDto(
    int Id,
    string LotNumber,
    string MediaType,
    string ManufacturerName,
    string ManufacturerLot,
    decimal TotalWeight,
    string TotalVolume,
    string? AutoclaveName,
    string AutoclaveProgram,
    string LoadType,
    decimal Temperature,
    int CycleTime,
    int CycleNumber,
    decimal Ph,
    DateTime PreparedAt,
    DateTime ExpiryDate,
    string PreparedByName,
    string ApprovalStatus,
    bool IsReleasedForUse,
    string? ApprovedByName,
    DateTime? ApprovedAt,
    string EvaluationType,
    string EvaluationStatus,
    string? EvaluationOutcome,
    DateTime? EvaluationCompletedAt,
    string? EvaluationCompletedByName,
    List<MediaGptChallengeDetailDto> Challenges);

public record MediaGptSummaryItemDto(
    string MediaType,
    int TotalLots,
    int ConformedLots,
    int NonConformedLots,
    int PendingLots,
    double PassRatePercent);

public record MediaGptSummaryDto(
    int TotalLots,
    int TotalConformed,
    int TotalNonConformed,
    int TotalPending,
    double OverallPassRatePercent,
    List<MediaGptSummaryItemDto> MediaTypes);

public record MediaGptExportRowDto(
    string LotNumber,
    string MediaType,
    DateTime PreparedAt,
    DateTime ExpiryDate,
    string ApprovalStatus,
    bool IsReleasedForUse,
    string PreparedByName,
    string? ApprovedByName,
    DateTime? ApprovedAt,
    string EvaluationType,
    string EvaluationStatus,
    string? EvaluationOutcome,
    DateTime? EvaluationCompletedAt,
    string OrganismName,
    string? AtccNumber,
    string? ChallengeRole,
    string? StrainSource,
    string InitialInoculum,
    string? ReferenceMediaLot,
    decimal? OldMediaCount,
    decimal? NewMediaCount,
    decimal? RecoveryPercent,
    string? ExpectedRecoveryRange,
    bool? GrowthObserved,
    string? ObservedDescription,
    string? ExpectedDescription,
    bool? IsTurbid,
    string? ChallengeOutcome,
    string? ReadByName,
    DateTime? ReadAt);

public record MediaGptExportResult(List<MediaGptExportRowDto> Items, int TotalCount, bool Exceeded);
public record MediaGptFilterOptionsDto(List<string> MediaTypes, List<string> EvaluationTypes);
