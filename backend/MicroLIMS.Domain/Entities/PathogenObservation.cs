namespace MicroLIMS.Domain.Entities;

// One step in the pathogen chain (TSB / RVS / XLD+TSI, or the simple
// single-step chain for non-Salmonella pathogens). The Pathogen Engine
// reads the full set for a TestOrder to decide Detected/Absent.
public class PathogenObservation
{
    public int Id { get; set; }
    public int TestOrderId { get; set; }
    public TestOrder? TestOrder { get; set; }
    public string StepName { get; set; } = string.Empty; // "TSB", "RVS", "XLD_TSI", or "Simple"
    public int StepOrder { get; set; }
    public bool GrowthObserved { get; set; }
    public int ObservedByUserId { get; set; }
    public DateTime ObservedAt { get; set; } = DateTime.UtcNow;
}
