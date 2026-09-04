using System.ComponentModel.DataAnnotations;

namespace MicroLIMS.Application.DTOs.DocumentControl;

public class SubmitForReviewRequest
{
    [Required]
    public int ReviewerUserId { get; set; }

    [MaxLength(2000)]
    public string? SubmissionNotes { get; set; }

    public DateTime? DueDate { get; set; }
}
