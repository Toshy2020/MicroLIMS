using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.DTOs.DocumentControl;

public class DocumentReviewTaskDto
{
    public int Id { get; set; }
    public int DocumentMasterId { get; set; }
    public string MicroLimsDocumentId { get; set; } = string.Empty;
    public string CompanyDocumentCode { get; set; } = string.Empty;
    public string DocumentTitle { get; set; } = string.Empty;

    public int DocumentRevisionId { get; set; }
    public string RevisionNumber { get; set; } = string.Empty;
    public DocumentRevisionStatus RevisionStatus { get; set; }

    public int AssignedReviewerUserId { get; set; }
    public string AssignedReviewerUsername { get; set; } = string.Empty;
    public string AssignedReviewerFullName { get; set; } = string.Empty;

    public int AssignedByUserId { get; set; }
    public string AssignedByUsername { get; set; } = string.Empty;
    public DateTime AssignedAt { get; set; }
    public DateTime? DueDate { get; set; }

    public ReviewTaskStatus Status { get; set; }
    public ReviewDecision? Decision { get; set; }
    public DateTime? DecisionAt { get; set; }
    public string? DecisionByUsername { get; set; }

    public string? ReviewNotes { get; set; }
    public string? SubmissionNotes { get; set; }

    public int TotalFindingsCount { get; set; }
    public int OpenMandatoryFindingsCount { get; set; }

    public List<DocumentReviewFindingDto> Findings { get; set; } = new();
}
