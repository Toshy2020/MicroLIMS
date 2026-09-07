using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

public class DocumentApprovalTask
{
    public int Id { get; set; }

    public int DocumentRevisionId { get; set; }
    public DocumentRevision DocumentRevision { get; set; } = null!;

    public int AssignedApproverUserId { get; set; }
    public User AssignedApproverUser { get; set; } = null!;

    public int AssignedByUserId { get; set; }
    public User AssignedByUser { get; set; } = null!;

    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DueDate { get; set; }

    public DocumentApprovalTaskStatus Status { get; set; } = DocumentApprovalTaskStatus.Pending;

    public DocumentApprovalDecision? Decision { get; set; }
    public DateTime? DecisionAt { get; set; }
    public int? DecisionByUserId { get; set; }
    public User? DecisionByUser { get; set; }

    public string? DecisionNotes { get; set; }
    public string? SubmissionNotes { get; set; }

    public DateTime? TargetEffectiveDate { get; set; }

    // Release 1d: Approval dossier linkages
    public int? ApprovedSourceFileId { get; set; }
    public RevisionFile? ApprovedSourceFile { get; set; }

    public int? GeneratedControlledPdfId { get; set; }
    public RevisionFile? GeneratedControlledPdf { get; set; }
}
