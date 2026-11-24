using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

public class OosInvestigation : IVersionedEntity
{
    public int Id { get; set; }
    public string Number { get; set; } = string.Empty;
    public OosInvestigationKind Kind { get; set; }
    public OosInvestigationStatus Status { get; set; }
    public int SampleId { get; set; }
    public int SectionId { get; set; }
    public DateTime OpenedAtUtc { get; set; }
    public int OpenedByUserId { get; set; }
    public string TemplateSnapshotJson { get; set; } = string.Empty;
    public DateOnly? PhaseOneDueDate { get; set; }
    public DateOnly? DueDate { get; set; }
    public OosPhaseOneAssessment? PhaseOneAssessment { get; set; }
    public int? PhaseOneCauseCategoryId { get; set; }
    public string? PhaseOneCauseDescription { get; set; }
    public DateTime? PhaseOneSignedAtUtc { get; set; }
    public int? PhaseOneSignedByUserId { get; set; }
    public int? PhaseOneSignatureId { get; set; }
    public OosConclusion? Conclusion { get; set; }
    public int? RootCauseCategoryId { get; set; }
    public string? RootCauseDescription { get; set; }
    public string? ConclusionSummary { get; set; }
    public DateTime? ConcludedAtUtc { get; set; }
    public int? ConcludedByUserId { get; set; }
    public int? ConclusionSignatureId { get; set; }
    public uint Version { get; set; }
    public List<OosInvestigationResult> Results { get; set; } = new();
}

public class OosInvestigationResult
{
    public int Id { get; set; }
    public int InvestigationId { get; set; }
    public int TestOrderId { get; set; }
    public OosOutcomeClass OutcomeAtOpening { get; set; }
    public string ResultSummaryAtOpening { get; set; } = string.Empty;
    public DateTime AddedAtUtc { get; set; }
    public bool IsInvalidated { get; set; }
    public DateTime? InvalidatedAtUtc { get; set; }
}

public class OosInvestigationEntry
{
    public int Id { get; set; }
    public int InvestigationId { get; set; }
    public int Phase { get; set; }
    public OosEntryKind Kind { get; set; }
    public string? ChecklistItemKey { get; set; }
    public OosChecklistAnswer? Answer { get; set; }
    public string Text { get; set; } = string.Empty;
    public int? ParentEntryId { get; set; }
    public int RecordedByUserId { get; set; }
    public DateTime RecordedAtUtc { get; set; }
}

public class OosRetestPlan
{
    public int Id { get; set; }
    public int InvestigationId { get; set; }
    public int Revision { get; set; }
    public OosRetestSource Source { get; set; }
    public int RetestCount { get; set; }
    public string TestOrderIdsJson { get; set; } = string.Empty;
    public string AnalystUserIdsJson { get; set; } = string.Empty;
    public string JudgingRule { get; set; } = string.Empty;
    public string? AmendmentReason { get; set; }
    public DateTime? SignedAtUtc { get; set; }
    public int? SignedByUserId { get; set; }
    public int? SignatureId { get; set; }
    public DateTime? SamplesCreatedAtUtc { get; set; }
}

public class OosRetestPlanSample
{
    public int PlanId { get; set; }
    public int SampleId { get; set; }
}

public class OosDeviationRequest : IVersionedEntity
{
    public int Id { get; set; }
    public int InvestigationId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public int RequestedByUserId { get; set; }
    public DateTime RequestedAtUtc { get; set; }
    public OosDeviationRequestStatus Status { get; set; }
    public string? AddressedComment { get; set; }
    public int? AddressedByUserId { get; set; }
    public DateTime? AddressedAtUtc { get; set; }
    public uint Version { get; set; }
}

public class OosInvestigationReopening
{
    public int Id { get; set; }
    public int InvestigationId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public OosConclusion PreviousConclusion { get; set; }
    public int PreviousConclusionSignatureId { get; set; }
    public int? PreviousRootCauseCategoryId { get; set; }
    public string? PreviousRootCauseDescription { get; set; }
    public string? PreviousConclusionSummary { get; set; }
    public int? PreviousConcludedByUserId { get; set; }
    public DateTime? PreviousConcludedAtUtc { get; set; }
    public int ReopenedByUserId { get; set; }
    public DateTime ReopenedAtUtc { get; set; }
    public int SignatureId { get; set; }
}
