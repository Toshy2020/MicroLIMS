using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.DTOs.DocumentControl;

public class DocumentReviewFindingDto
{
    public int Id { get; set; }
    public int DocumentReviewTaskId { get; set; }

    public int CreatedByUserId { get; set; }
    public string CreatedByUsername { get; set; } = string.Empty;
    public string CreatedByFullName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public int? PageNumber { get; set; }
    public string? SectionNumber { get; set; }
    public string CommentText { get; set; } = string.Empty;
    public bool IsMandatory { get; set; }

    // Release 1d: Linkage to specific source file version
    public int? RevisionFileId { get; set; }
    public int? SourceFileVersion { get; set; }

    public ReviewFindingStatus Status { get; set; }

    public string? AuthorResponse { get; set; }
    public DateTime? AuthorResponseAt { get; set; }
    public string? AuthorResponseUsername { get; set; }

    public string? ReviewerVerificationNotes { get; set; }
    public DateTime? ReviewerVerifiedAt { get; set; }
    public string? ReviewerVerifiedUsername { get; set; }

    public DateTime? ResolvedAt { get; set; }
    public string? ResolvedByUsername { get; set; }
}
