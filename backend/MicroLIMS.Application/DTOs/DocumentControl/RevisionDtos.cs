using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.DTOs.DocumentControl;

public record CreateRevisionRequest(
    RevisionType RevisionType,
    string ReasonForRevision,
    string? ChangeSummary = null,
    string? ChangeReference = null,
    string? CustomRevisionNumber = null,
    int? OriginatingPeriodicReviewTaskId = null
);

public record ProposeNextRevisionRequest(
    RevisionType RevisionType
);

public record ProposeNextRevisionResponse(
    string ProposedRevisionNumber,
    RevisionType RevisionType,
    string CurrentEffectiveRevisionNumber
);

public record RevisionChangeItemDto(
    int Id,
    int DocumentRevisionId,
    string SectionNumber,
    string SectionTitle,
    string DescriptionOfChange,
    string ChangeRationale,
    string ChangeCategory,
    string Status,
    int? OriginatingReviewFindingId,
    int CreatedByUserId,
    string CreatedByUsername,
    string CreatedByFullName,
    DateTime CreatedAt,
    DateTime? ModifiedAt
);

public record AddChangeItemRequest(
    string SectionNumber,
    string SectionTitle,
    string DescriptionOfChange,
    string ChangeRationale,
    string ChangeCategory,
    string? Status = "Draft",
    int? OriginatingReviewFindingId = null
);

public record UpdateChangeItemRequest(
    string SectionNumber,
    string SectionTitle,
    string DescriptionOfChange,
    string ChangeRationale,
    string ChangeCategory,
    string Status
);

public record ConvertFindingRequest(
    string SectionTitle,
    string ChangeRationale,
    string ChangeCategory
);

public record RevisionImpactAssessmentDto(
    int Id,
    int DocumentRevisionId,
    bool ProcedureOrMethodImpact,
    string? ProcedureOrMethodDetails,
    bool TrainingImpact,
    string? TrainingDetails,
    bool FormsOrTemplatesImpact,
    string? FormsOrTemplatesDetails,
    bool SpecificationsImpact,
    string? SpecificationsDetails,
    bool EquipmentImpact,
    string? EquipmentDetails,
    bool MaterialsOrMediaImpact,
    string? MaterialsOrMediaDetails,
    bool ValidationImpact,
    string? ValidationDetails,
    bool RegulatoryCommitmentImpact,
    string? RegulatoryCommitmentDetails,
    bool RelatedDocumentsImpact,
    string? RelatedDocumentsDetails,
    bool IsComplete,
    int CompletedByUserId,
    string CompletedByUsername,
    string CompletedByFullName,
    DateTime CompletedAt
);

public record SaveImpactAssessmentRequest(
    bool ProcedureOrMethodImpact,
    string? ProcedureOrMethodDetails,
    bool TrainingImpact,
    string? TrainingDetails,
    bool FormsOrTemplatesImpact,
    string? FormsOrTemplatesDetails,
    bool SpecificationsImpact,
    string? SpecificationsDetails,
    bool EquipmentImpact,
    string? EquipmentDetails,
    bool MaterialsOrMediaImpact,
    string? MaterialsOrMediaDetails,
    bool ValidationImpact,
    string? ValidationDetails,
    bool RegulatoryCommitmentImpact,
    string? RegulatoryCommitmentDetails,
    bool RelatedDocumentsImpact,
    string? RelatedDocumentsDetails
);
