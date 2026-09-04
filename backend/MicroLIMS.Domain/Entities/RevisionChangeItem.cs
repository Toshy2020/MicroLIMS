namespace MicroLIMS.Domain.Entities;

public class RevisionChangeItem
{
    public int Id { get; set; }

    public int DocumentRevisionId { get; set; }
    public DocumentRevision DocumentRevision { get; set; } = null!;

    public string SectionNumber { get; set; } = string.Empty;
    public string SectionTitle { get; set; } = string.Empty;
    public string DescriptionOfChange { get; set; } = string.Empty;
    public string ChangeRationale { get; set; } = string.Empty;
    public string ChangeCategory { get; set; } = string.Empty; // e.g., "Addition", "Modification", "Deletion", "Clarification"
    public string Status { get; set; } = "Draft"; // "Draft", "Addressed", "Deferred"

    public int? OriginatingReviewFindingId { get; set; }
    public DocumentReviewFinding? OriginatingReviewFinding { get; set; }

    public int CreatedByUserId { get; set; }
    public User CreatedByUser { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ModifiedAt { get; set; }
}
