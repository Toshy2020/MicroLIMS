namespace MicroLIMS.Application.DTOs;

public class ApprovalDto
{
    public int TestOrderId { get; set; }
    public string Decision { get; set; } = string.Empty; // Approve / Reject / Retest
    public string? Comment { get; set; }
    public int DecidedByUserId { get; set; }
}
