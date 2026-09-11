using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.DTOs.DocumentControl;

// Master & Revision Models
public record DocumentMasterSummaryDto(
    int Id,
    string MicroLimsDocumentId,
    string CompanyDocumentCode,
    string Title,
    int DocumentTypeId,
    string DocumentTypeName,
    string DocumentTypeCode,
    int DepartmentId,
    string DepartmentName,
    int SectionId,
    string SectionName,
    int DocumentOwnerUserId,
    string DocumentOwnerName,
    DocumentConfidentiality Confidentiality,
    string? Category,
    RecordOrigin RecordOrigin,
    DocumentRecordStatus RecordStatus,
    int? CurrentEffectiveRevisionId,
    string? CurrentEffectiveRevisionNumber,
    // Status of the master's current revision - the effective one where there
    // is one, otherwise the newest by RevisionSequence (the same selection
    // DocumentMasterDto uses). FRS-1A §5:104 requires a Status column on the
    // Document Library, and without this the client could only infer
    // "effective or not" from CurrentEffectiveRevisionId and was labelling
    // every other lifecycle state as Draft. Null only when a master somehow
    // holds no revisions at all.
    DocumentRevisionStatus? CurrentRevisionStatus,
    DateTime? EffectiveDate,
    DateTime? NextReviewDate,
    bool HasControlledPdf,
    bool HasSourceFile,
    DateTime CreatedAt
);

public record DocumentMasterDto(
    int Id,
    string MicroLimsDocumentId,
    string CompanyDocumentCode,
    string Title,
    int DocumentTypeId,
    string DocumentTypeName,
    string DocumentTypeCode,
    int DepartmentId,
    string DepartmentName,
    int SectionId,
    string SectionName,
    int DocumentOwnerUserId,
    string DocumentOwnerName,
    DocumentConfidentiality Confidentiality,
    string? Category,
    RecordOrigin RecordOrigin,
    DocumentRecordStatus RecordStatus,
    int? CurrentEffectiveRevisionId,
    string? CurrentEffectiveRevisionNumber,
    DateTime CreatedAt,
    int CreatedByUserId,
    string CreatedByName,
    DateTime? ModifiedAt,
    int? ModifiedByUserId,
    string? ModifiedByName,
    DateTime? VoidedAt,
    int? VoidedByUserId,
    string? VoidedByName,
    string? VoidReason,
    List<string> Keywords,
    List<DocumentMasterAssignmentDto> Assignments,
    List<DocumentRevisionDto> Revisions
);

public record DocumentMasterAssignmentDto(
    int Id,
    int DocumentMasterId,
    int UserId,
    string UserName,
    string UserFullName,
    AssignmentRole AssignmentRole,
    bool IsActive,
    DateTime AssignedAt,
    int AssignedByUserId,
    string AssignedByName
);

public record DocumentRevisionDto(
    int Id,
    int DocumentMasterId,
    string RevisionNumber,
    int RevisionSequence,
    DocumentRevisionStatus RevisionStatus,
    DateTime? EffectiveDate,
    DateTime? NextReviewDate,
    int? ReviewCycleMonths,
    RevisionType? RevisionType,
    string? ReasonForRevision,
    string? ChangeReference,
    RecordOrigin RecordOrigin,
    DateTime CreatedAt,
    int CreatedByUserId,
    string CreatedByName,
    DateTime? CancelledAt,
    int? CancelledByUserId,
    string? CancelledByName,
    string? CancelReason,
    List<RevisionFileDto> Files
);

public record RevisionFileDto(
    int Id,
    int DocumentRevisionId,
    FileRole FileRole,
    string FileName,
    string ContentType,
    long SizeBytes,
    string ContentSha256,
    bool IsActive,
    int? SupersededByFileId,
    DateTime UploadedAt,
    int UploadedByUserId,
    string UploadedByName,
    int FileVersion = 1,
    bool IsApprovedFinalSource = false,
    int? GeneratedFromSourceFileId = null
);

// Requests
public record RegisterDocumentMasterRequest(
    string CompanyDocumentCode,
    string Title,
    int DocumentTypeId,
    int DepartmentId,
    int SectionId,
    int DocumentOwnerUserId,
    DocumentConfidentiality Confidentiality = DocumentConfidentiality.Internal,
    string? Category = null,
    List<string>? Keywords = null,
    string InitialRevisionNumber = "01",
    int? ReviewCycleMonths = null,
    List<CreateAssignmentRequest>? InitialAssignments = null
);

public record CreateAssignmentRequest(
    int UserId,
    AssignmentRole AssignmentRole
);

public record UpdateDocumentMasterDraftRequest(
    string CompanyDocumentCode,
    string Title,
    int DocumentTypeId,
    int DepartmentId,
    int SectionId,
    int DocumentOwnerUserId,
    DocumentConfidentiality Confidentiality,
    string? Category = null,
    List<string>? Keywords = null
);

public record VoidDocumentMasterRequest(
    string Reason
);

public record CancelDraftRevisionRequest(
    string Reason
);

public record DocumentLibraryFilterRequest(
    string? SearchTerm = null,
    int? DocumentTypeId = null,
    int? DepartmentId = null,
    int? SectionId = null,
    int? OwnerUserId = null,
    DocumentRevisionStatus? RevisionStatus = null,
    DocumentRecordStatus? RecordStatus = null,
    RecordOrigin? RecordOrigin = null,
    bool IncludeCancelledAndVoided = false,
    bool? IsOverdue = null,
    int Page = 1,
    int PageSize = 20,
    string? SortBy = null,
    bool SortDescending = false
);

public record DocumentLibraryResponse(
    List<DocumentMasterSummaryDto> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages
);

// Configuration DTOs
public record DocumentTypeDto(
    int Id,
    string Code,
    string Name,
    int DefaultReviewCycleMonths,
    bool IsActive
);

public record CreateDocumentTypeRequest(
    string Code,
    string Name,
    int DefaultReviewCycleMonths
);

public record UpdateDocumentTypeRequest(
    string Name,
    int DefaultReviewCycleMonths,
    bool IsActive
);

public record DocumentDepartmentDto(
    int Id,
    string Code,
    string Name,
    bool IsActive,
    List<DocumentSectionDto>? Sections = null
);

public record CreateDocumentDepartmentRequest(
    string Code,
    string Name
);

public record UpdateDocumentDepartmentRequest(
    string Name,
    bool IsActive
);

public record DocumentSectionDto(
    int Id,
    int DepartmentId,
    string DepartmentName,
    string Name,
    bool IsActive
);

public record CreateDocumentSectionRequest(
    int DepartmentId,
    string Name
);

public record UpdateDocumentSectionRequest(
    string Name,
    bool IsActive
);

public record DocumentNumberingConfigDto(
    int Id,
    string Prefix,
    string NumberFormat,
    bool IsEnabled,
    DateTime ModifiedAt,
    int ModifiedByUserId,
    string ModifiedByName,
    string SampleNextId
);

public record UpdateDocumentNumberingConfigRequest(
    string Prefix,
    string NumberFormat,
    bool IsEnabled
);

public record ConfigurationSettingDto(
    int Id,
    string SettingKey,
    string SettingValue,
    string DataType,
    string SettingGroup,
    DateTime ModifiedAt,
    int? ModifiedByUserId,
    string? ModifiedByName
);

public record UpdateConfigurationSettingRequest(
    string SettingValue
);

// Audit Query DTOs
public record DocumentAuditFilterRequest(
    DateTime? DateFrom = null,
    DateTime? DateTo = null,
    int? UserId = null,
    AuditActionCategory? ActionCategory = null,
    string? ActionCode = null,
    string? RecordType = null,
    int? DocumentMasterId = null,
    int? DocumentRevisionId = null,
    string? SearchTerm = null,
    int Page = 1,
    int PageSize = 50
);

public record DocumentAuditItemDto(
    int Id,
    string? EventUid,
    DateTime Timestamp,
    ActorType? ActorType,
    int? UserId,
    string? UserName,
    string? SystemProcessName,
    string Action,
    string? ActionCode,
    AuditActionCategory? ActionCategory,
    string? EntityName,
    string? EntityId,
    string? Reason,
    string? SourceContext,
    Guid? CorrelationId,
    int? DocumentMasterId,
    int? DocumentRevisionId,
    List<AuditEventChangeDto> Changes
);

public record AuditEventChangeDto(
    string FieldName,
    string? PreviousValue,
    string? NewValue
);

public record DocumentAuditResponse(
    List<DocumentAuditItemDto> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages
);
