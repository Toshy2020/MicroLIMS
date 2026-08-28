namespace MicroLIMS.Domain.Entities;

public class WorkloadWeightHistory
{
    public int Id { get; set; }
    public int WorkloadWeightId { get; set; }
    public WorkloadWeight WorkloadWeight { get; set; } = null!;
    public string Action { get; set; } = string.Empty; // "Created", "Updated"
    public string TestCode { get; set; } = string.Empty;
    public decimal PreviousWeight { get; set; }
    public decimal NewWeight { get; set; }
    public string ReasonForChange { get; set; } = string.Empty;
    public int ChangedByUserId { get; set; }
    public string ChangedByName { get; set; } = string.Empty;
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
}
