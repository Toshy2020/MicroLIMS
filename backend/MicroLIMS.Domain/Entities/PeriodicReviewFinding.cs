using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

public class PeriodicReviewFinding
{
    public int Id { get; set; }

    public int PeriodicReviewTaskId { get; set; }
    public PeriodicReviewTask PeriodicReviewTask { get; set; } = null!;

    public int? PageNumber { get; set; }
    public string? SectionNumber { get; set; }
    public string NoteText { get; set; } = string.Empty;

    public PeriodicReviewFindingStatus Status { get; set; } = PeriodicReviewFindingStatus.Open;

    public int CreatedByUserId { get; set; }
    public User CreatedByUser { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
