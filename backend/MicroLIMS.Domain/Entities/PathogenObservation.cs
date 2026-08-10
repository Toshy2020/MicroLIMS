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

    // Set on DualGrowth steps only, where a step produces two rows
    // sharing one StepName - MediaId is that plate's own lot (distinct
    // from its sibling row's), PlateLabel ("XLD"/"TSI", analyst-editable
    // at media-selection time) is what actually distinguishes the two
    // rows instead of insertion order. Both null on a plain Growth step.
    public int? MediaId { get; set; }
    public Media? Media { get; set; }
    public string? PlateLabel { get; set; }
}
