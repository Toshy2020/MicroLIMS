using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

public class DocumentReviewTask
{
    public int Id { get; set; }

    public int DocumentRevisionId { get; set; }
    public DocumentRevision DocumentRevision { get; set; } = null!;

    public int AssignedReviewerUserId { get; set; }
    public User AssignedReviewerUser { get; set; } = null!;

    public int AssignedByUserId { get; set; }
    public User AssignedByUser { get; set; } = null!;

    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DueDate { get; set; }

    public ReviewTaskStatus Status { get; set; } = ReviewTaskStatus.Pending;

    public ReviewDecision? Decision { get; set; }
    public DateTime? DecisionAt { get; set; }
    public int? DecisionByUserId { get; set; }
    public User? DecisionByUser { get; set; }

    public string? ReviewNotes { get; set; }
    public string? SubmissionNotes { get; set; }

    // Release 1d: Multi-cycle review lineage
    public int ReviewCycleNumber { get; set; } = 1;

    public int? ReviewedSourceFileId { get; set; }
    public RevisionFile? ReviewedSourceFile { get; set; }

    public ICollection<DocumentReviewFinding> Findings { get; set; } = new List<DocumentReviewFinding>();
}
