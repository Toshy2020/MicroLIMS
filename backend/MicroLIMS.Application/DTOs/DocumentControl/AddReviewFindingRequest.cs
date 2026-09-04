using System.ComponentModel.DataAnnotations;

namespace MicroLIMS.Application.DTOs.DocumentControl;

public class AddReviewFindingRequest
{
    public int? PageNumber { get; set; }

    [MaxLength(100)]
    public string? SectionNumber { get; set; }

    [Required]
    [MinLength(5)]
    [MaxLength(4000)]
    public string CommentText { get; set; } = string.Empty;

    public bool IsMandatory { get; set; } = true;
}
