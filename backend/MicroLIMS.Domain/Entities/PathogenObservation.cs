using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

public class PathogenObservation
{
    public int Id { get; set; }
    public int TestOrderId { get; set; }
    public TestOrder? TestOrder { get; set; }
    public string StepName { get; set; } = string.Empty;
    public int StepOrder { get; set; }

    public GrowthObservation Observation { get; set; }

    public int ObservedByUserId { get; set; }
    public DateTime ObservedAt { get; set; } = DateTime.UtcNow;

    public int? MediaId { get; set; }
    public Media? Media { get; set; }
}
