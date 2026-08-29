using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.DTOs;

public record ReferenceStrainSearchRequest(
    string? Search = null,
    int? OrganismId = null,
    ApprovalGateStatus? ApprovalStatus = null,
    bool? IsDestroyed = null,
    DateTime? ReceiptFromDate = null,
    DateTime? ReceiptToDate = null,
    DateTime? UsageFromDate = null,
    DateTime? UsageToDate = null,
    int Page = 1,
    int PageSize = 25,
    string SortBy = "PreparedAt",
    bool SortDescending = true);

public record ReferenceStrainListDto(
    int Id,
    string StrainName,
    string? AtccNumber,
    string CryovialCode,
    string ManufacturerName,
    string SourceMaterialName,
    string SourceMaterialBatchNumber,
    DateTime ReceiptDate,
    DateTime PreparedAt,
    DateTime ExpiryDate,
    int NumberOfVialsPrepared,
    int VialsRemaining,
    string StorageCondition,
    string ApprovalStatus,
    bool IsDestroyed,
    string PreparedByName,
    string? ApprovedByName,
    DateTime? ApprovedAt,
    int DirectUsageCount);

public record ReferenceStrainSearchResult(List<ReferenceStrainListDto> Items, int TotalCount, int Page, int PageSize);

public record ReferenceStrainIdentityConfirmationDto(
    int Id,
    string? MediaLotNumber,
    string? MediaName,
    string? IncubatorName,
    DateTime IncubationStart,
    DateTime IncubationEnd,
    string ObservationText);

public record ReferenceStrainThawEventDto(
    int Id,
    DateTime ThawedAt,
    string ThawedByName,
    string? Notes);

public record ReferenceStrainDirectUsageDto(
    int ChallengeId,
    int MediaId,
    string MediaLotNumber,
    string MediaType,
    string EvaluationType,
    string? ChallengeRole,
    string? Outcome,
    string? ReadByName,
    DateTime? ReadAt,
    string EvaluationStatus);

public record ReferenceStrainDetailDto(
    int Id,
    string CryovialCode,
    string StrainName,
    string? AtccNumber,
    string ManufacturerName,
    string SourceMaterialName,
    string SourceMaterialBatchNumber,
    DateTime SourceMaterialReceivingDate,
    decimal SourceMaterialQuantityReceived,
    DateTime PreparedAt,
    DateTime ExpiryDate,
    int NumberOfVialsPrepared,
    int VialsRemaining,
    string StorageCondition,
    bool PhysicalCheckConfirmed,
    string PhysicalCheckText,
    string ApprovalStatus,
    bool IsDestroyed,
    string PreparedByName,
    string? ApprovedByName,
    DateTime? ApprovedAt,
    List<ReferenceStrainIdentityConfirmationDto> IdentityConfirmations,
    List<ReferenceStrainThawEventDto> ThawHistory,
    List<ReferenceStrainDirectUsageDto> DirectUsageLog,
    int DistinctQualifiedMediaLotsCount,
    int IndirectTestOrdersCount,
    string IndirectUsageSummary);

public record ReferenceStrainExportRowDto(
    string StrainName,
    string? AtccNumber,
    string CryovialCode,
    string ManufacturerName,
    string SourceMaterialName,
    string SourceMaterialBatchNumber,
    DateTime ReceiptDate,
    DateTime PreparedAt,
    DateTime ExpiryDate,
    int NumberOfVialsPrepared,
    int VialsRemaining,
    string StorageCondition,
    string ApprovalStatus,
    bool IsDestroyed,
    string PreparedByName,
    string? ApprovedByName,
    DateTime? ApprovedAt,
    int IdentityConfirmationsCount,
    int ThawEventsCount,
    int DirectGptUsageCount,
    int IndirectTestOrdersCount);

public record ReferenceStrainExportResult(List<ReferenceStrainExportRowDto> Items, int TotalCount, bool Exceeded);
public record OrganismOptionDto(int Id, string ScientificName, string? AtccNumber);
public record ReferenceStrainFilterOptionsDto(List<OrganismOptionDto> Organisms);
