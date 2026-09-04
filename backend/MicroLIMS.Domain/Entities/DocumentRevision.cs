using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

public class DocumentRevision
{
    public int Id { get; set; }

    public int DocumentMasterId { get; set; }
    public DocumentMaster DocumentMaster { get; set; } = null!;

    public string RevisionNumber { get; set; } = string.Empty;
    public int RevisionSequence { get; set; }

    public DocumentRevisionStatus RevisionStatus { get; set; } = DocumentRevisionStatus.Draft;

    public DateTime? EffectiveDate { get; set; }
    public DateTime? NextReviewDate { get; set; }
    public int? ReviewCycleMonths { get; set; }

    public RevisionType? RevisionType { get; set; }
    public string? ReasonForRevision { get; set; }
    public string? ChangeSummary { get; set; }
    public string? ChangeReference { get; set; }

    public int? OriginatingPeriodicReviewTaskId { get; set; }

    public RecordOrigin RecordOrigin { get; set; } = RecordOrigin.Native;

    public int CreatedByUserId { get; set; }
    public User CreatedByUser { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int? CancelledByUserId { get; set; }
    public User? CancelledByUser { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancelReason { get; set; }

    public ICollection<RevisionFile> Files { get; set; } = new List<RevisionFile>();
    public ICollection<DocumentReviewTask> ReviewTasks { get; set; } = new List<DocumentReviewTask>();
    public ICollection<RevisionChangeItem> ChangeItems { get; set; } = new List<RevisionChangeItem>();
    public RevisionImpactAssessment? ImpactAssessment { get; set; }
    public ICollection<DocumentApprovalTask> ApprovalTasks { get; set; } = new List<DocumentApprovalTask>();
}
