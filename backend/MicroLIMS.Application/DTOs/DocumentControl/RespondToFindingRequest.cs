using System.ComponentModel.DataAnnotations;

namespace MicroLIMS.Application.DTOs.DocumentControl;

public class RespondToFindingRequest
{
    [Required]
    [MinLength(3)]
    [MaxLength(4000)]
    public string Response { get; set; } = string.Empty;
}
