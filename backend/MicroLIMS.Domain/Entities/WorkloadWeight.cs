using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

public class WorkloadWeight
{
    public int Id { get; set; }
    public string TestCode { get; set; } = string.Empty;
    public string TestName { get; set; } = string.Empty;
    public SampleCategory Category { get; set; }
    public decimal Weight { get; set; } = 1.0m;
    public bool IsActive { get; set; } = true;
    public DateTime EffectiveDate { get; set; } = DateTime.UtcNow;
    public string ReasonForChange { get; set; } = string.Empty;
    public int ChangedByUserId { get; set; }
    public string ChangedByName { get; set; } = string.Empty;
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;

    public List<WorkloadWeightHistory> History { get; set; } = new();
}
