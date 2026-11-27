using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities.Quality;

public class QualityRecord : IVersionedEntity
{
    public int Id { get; set; }
    public string Number { get; set; } = string.Empty;
    public QualityRecordType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int? SectionId { get; set; }
    public int? RaisedByUserId { get; set; }
    public string? RaisedBySystemProcess { get; set; }
    public DateTime RaisedAtUtc { get; set; }
    public int? OwnerUserId { get; set; }
    public int? ClassificationId { get; set; }
    public DateOnly? DueDate { get; set; }
    public bool IsCancelled { get; set; }
    public string? CancelReason { get; set; }
    public uint Version { get; set; }

    public DeviationDetail? DeviationDetail { get; set; }
    public CapaDetail? CapaDetail { get; set; }
    public ChangeControlDetail? ChangeControlDetail { get; set; }
    public ComplaintDetail? ComplaintDetail { get; set; }

    public List<QualityRecordStep> Steps { get; set; } = new();
    public List<QualityAction> Actions { get; set; } = new();
    public List<QualityRecordLink> Links { get; set; } = new();
    public List<QualityAttachment> Attachments { get; set; } = new();
    public List<QualityComment> Comments { get; set; } = new();
    public List<QualityExtensionRequest> ExtensionRequests { get; set; } = new();
    public List<QualityChecklistAnswer> ChecklistAnswers { get; set; } = new();
}

public class DeviationDetail
{
    public int RecordId { get; set; }
    public QualityDeviationStatus Status { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public string? Location { get; set; }
    public string ImmediateAction { get; set; } = string.Empty;
    public string? ImpactAssessment { get; set; }
    public int? RootCauseCategoryId { get; set; }
    public string? RootCauseDescription { get; set; }
    public string? ProposedActions { get; set; }
    public bool? CapaRequired { get; set; }
    public string? RejectionReason { get; set; }
}

public class CapaDetail
{
    public int RecordId { get; set; }
    public CapaStatus Status { get; set; }
    public int? SourceRecordId { get; set; }
    public string? EffectivenessCriteria { get; set; }
    public string? EffectivenessMethod { get; set; }
    public DateOnly? EffectivenessPlannedDate { get; set; }
    public CapaEffectiveness? EffectivenessResult { get; set; }
    public string? EffectivenessFindings { get; set; }
    public int? SuccessorCapaId { get; set; }
}

public class ChangeControlDetail
{
    public int RecordId { get; set; }
    public ChangeControlStatus Status { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? ImpactSummary { get; set; }
    public string? RejectionReason { get; set; }
    public string? PostChangeReview { get; set; }
    public string? UnchangedJustification { get; set; }
}

public class ComplaintDetail
{
    public int RecordId { get; set; }
    public ComplaintStatus Status { get; set; }
    public string Source { get; set; } = string.Empty;
    public DateOnly ReceivedOn { get; set; }
    public string? ExternalReference { get; set; }
    public int? ItemId { get; set; }
    public string? LotText { get; set; }
    public string ComplaintText { get; set; } = string.Empty;
    public string? WhatToTest { get; set; }
    public string? Findings { get; set; }
    public ComplaintOutcome? Outcome { get; set; }
    public string? ConclusionSummary { get; set; }
}
