using System.ComponentModel.DataAnnotations;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.DTOs.DocumentControl;

public class ReviewDecisionRequest
{
    [Required]
    public ReviewDecision Decision { get; set; }

    [MaxLength(4000)]
    public string? ReviewNotes { get; set; }
}
