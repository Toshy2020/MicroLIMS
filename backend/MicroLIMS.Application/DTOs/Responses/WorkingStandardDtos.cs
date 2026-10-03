using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.DTOs.Responses;

public record CreateQualificationRequest(
    WorkingStandardQualificationKind Kind, int? WorkingStandardMaterialId, int? MaterialMasterEntryId,
    int? SourceSampleId, string? SourceMaterialName, string? SourceBatchNumber,
    decimal? QuantityGrams, string? Location, decimal? MoisturePercent);
public record UpdateQualificationRequest(decimal? QuantityGrams, string? Location, decimal? MoisturePercent);
public record WorkingStandardSignRequest(string Password, string? Comment);
public record WorkingStandardReasonRequest(string Password, string Reason);
public record WorkingStandardReturnRequest(string Reason);
public record EligibleSourceSampleDto(int SampleId, string ReferenceNumber, string MaterialName, string? BatchNumber, string TestCode);
public record WorkingStandardDocumentDto(int Id, WorkingStandardDocumentKind Kind, string FileName, string ContentType, DateTime UploadedAt, string? UploadedByUserName, bool IsCurrent);
public record WorkingStandardRunLinkDto(int HplcRunId, int EquipmentId, string RunCode, int RunSampleId, HplcRunSampleStatus Status);
public record WorkingStandardQualificationDto(
    int Id, uint Version, string Code, WorkingStandardQualificationKind Kind, WorkingStandardQualificationStatus Status,
    int SectionId, int? WorkingStandardMaterialId, string? WorkingStandardCode,
    int MaterialMasterEntryId, string MaterialMasterCode, string MaterialMasterName,
    int? SourceSampleId, string? SourceSampleReference, string SourceMaterialName, string SourceBatchNumber,
    decimal? QuantityGrams, string? Location, decimal? MoisturePercent,
    List<decimal>? ReplicateAssayPercents, decimal? MeanAssayPercent, decimal? RsdPercent, decimal? PotencyPercent,
    bool Passed, string? FailureReasons,
    string? CreatedByUserName, DateTime CreatedAt,
    string? PreparedByUserName, DateTime? PreparedAt, string? ReviewedByUserName, DateTime? ReviewedAt,
    string? ApprovedByUserName, DateTime? ApprovedAt, string? RejectedByUserName, DateTime? RejectedAt,
    string? RejectReason, string? ReturnReason,
    WorkingStandardRunLinkDto? Run, List<WorkingStandardDocumentDto> Documents, List<int> RunEvidenceIds);
public record WorkingStandardLotDto(
    int MaterialId, string Code, string MaterialName, string BatchNumber, string MasterEntryCode,
    decimal? PotencyPercent, decimal? MoisturePercent, decimal QuantityRemaining, DateTime? ExpiryDate,
    string Status, int? OpenQualificationId);   // Status: "Valid" | "DueSoon" | "Expired" | "Depleted"
