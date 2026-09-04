using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.DTOs.DocumentControl;

public record CreateApprovalTaskRequest(
    int ApproverUserId,
    DateTime? DueDate = null,
    string? SubmissionNotes = null,
    DateTime? TargetEffectiveDate = null
);

public record DocumentApprovalTaskDto(
    int Id,
    int DocumentRevisionId,
    int DocumentMasterId,
    string CompanyDocumentCode,
    string DocumentTitle,
    string RevisionNumber,
    int RevisionSequence,
    int AssignedApproverUserId,
    string AssignedApproverUsername,
    string AssignedApproverFullName,
    int AssignedByUserId,
    string AssignedByUsername,
    string AssignedByFullName,
    DateTime AssignedAt,
    DateTime? DueDate,
    DocumentApprovalTaskStatus Status,
    DocumentApprovalDecision? Decision,
    DateTime? DecisionAt,
    int? DecisionByUserId,
    string? DecisionByUsername,
    string? DecisionByFullName,
    string? DecisionNotes,
    string? SubmissionNotes,
    DateTime? TargetEffectiveDate
);

public record ApprovalReadinessDto(
    bool IsReady,
    List<string> ValidationErrors,
    bool RequiresElectronicSignature,
    DocumentRevisionStatus TargetStatusOnApproval,
    bool HasActiveControlledPdf,
    bool IsTechnicalReviewCompleted,
    bool AreMandatoryFindingsResolved,
    bool IsImpactAssessmentCompleted,
    bool AreChangeItemsAddressed,
    bool IsSegregationOfDutiesSatisfied
);

public record ElectronicSignatureDto(
    int Id,
    int UserId,
    string UserFullNameSnapshot,
    string UsernameSnapshot,
    string RoleSnapshot,
    SignatureMeaning MeaningOfSignature,
    string EntityType,
    int EntityId,
    DateTime SignedAt,
    string? Comment,
    string? IpAddress
);

public record ApprovalDossierDto(
    DocumentApprovalTaskDto Task,
    DocumentRevisionDto Revision,
    DocumentMasterDto Master,
    RevisionFileDto? ControlledPdf,
    IReadOnlyList<RevisionChangeItemDto> ChangeItems,
    RevisionImpactAssessmentDto? ImpactAssessment,
    IReadOnlyList<DocumentReviewTaskDto> ReviewHistory,
    ApprovalReadinessDto Readiness,
    ElectronicSignatureDto? ActiveSignature = null
);

public record ExecuteApprovalDecisionRequest(
    DocumentApprovalDecision Decision,
    string? DecisionNotes = null,
    DateTime? EffectiveDate = null,
    string? Password = null,
    string? SignatureToken = null
);

public record SignApprovalRequest(
    string Password,
    string? DecisionNotes = null,
    DateTime? EffectiveDate = null
);
