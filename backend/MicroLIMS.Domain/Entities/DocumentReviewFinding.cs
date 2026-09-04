using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

public class DocumentReviewFinding
{
    public int Id { get; set; }

    public int DocumentReviewTaskId { get; set; }
    public DocumentReviewTask DocumentReviewTask { get; set; } = null!;

    public int CreatedByUserId { get; set; }
    public User CreatedByUser { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int? PageNumber { get; set; }
    public string? SectionNumber { get; set; }
    public string CommentText { get; set; } = string.Empty;
    public bool IsMandatory { get; set; } = true;

    public ReviewFindingStatus Status { get; set; } = ReviewFindingStatus.Open;

    public string? AuthorResponse { get; set; }
    public DateTime? AuthorResponseAt { get; set; }
    public int? AuthorResponseByUserId { get; set; }
    public User? AuthorResponseByUser { get; set; }

    public string? ReviewerVerificationNotes { get; set; }
    public DateTime? ReviewerVerifiedAt { get; set; }
    public int? ReviewerVerifiedByUserId { get; set; }
    public User? ReviewerVerifiedByUser { get; set; }

    public DateTime? ResolvedAt { get; set; }
    public int? ResolvedByUserId { get; set; }
    public User? ResolvedByUser { get; set; }
}
