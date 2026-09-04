using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

public class PeriodicReviewTask
{
    public int Id { get; set; }

    public int DocumentMasterId { get; set; }
    public DocumentMaster DocumentMaster { get; set; } = null!;

    public int DocumentRevisionId { get; set; }
    public DocumentRevision DocumentRevision { get; set; } = null!;

    public int ReviewCycleMonths { get; set; }
    public DateTime ScheduledDueDate { get; set; }

    public int? AssignedReviewerUserId { get; set; }
    public User? AssignedReviewerUser { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public PeriodicReviewTaskStatus Status { get; set; } = PeriodicReviewTaskStatus.Pending;

    public PeriodicReviewOutcome? Outcome { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int? CompletedByUserId { get; set; }
    public User? CompletedByUser { get; set; }

    public string? ReviewSummary { get; set; }

    public ICollection<PeriodicReviewFinding> Findings { get; set; } = new List<PeriodicReviewFinding>();
}
