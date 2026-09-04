using System.ComponentModel.DataAnnotations;

namespace MicroLIMS.Application.DTOs.DocumentControl;

public class VerifyFindingRequest
{
    [MaxLength(4000)]
    public string? VerificationNotes { get; set; }
}
