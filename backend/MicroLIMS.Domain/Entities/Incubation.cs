namespace MicroLIMS.Domain.Entities;

// Supports multi-step incubation workflows (EM Step 1/Step 2, Salmonella
// TSB -> RVS -> XLD+TSI chain). TestOrderId is nullable because Media
// Evaluation challenges also create Incubation rows (locked Temperature/
// Duration from the Media's MediaType) with no TestOrder involved - see
// MediaEvaluationEngine.RecordIncubationAsync.
public class Incubation
{
    public int Id { get; set; }
    public int? TestOrderId { get; set; }
    public TestOrder? TestOrder { get; set; }
    public int StepNumber { get; set; }
    public string StepName { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public string? Outcome { get; set; }

    // Count Test (TAMC/TYMC) and Pathogen incubation setup: which prepared
    // Media lot and Incubator were used, and the Temperature/Duration
    // ranges hard-locked from that lot's MediaType at setup time - never
    // editable by the analyst afterward.
    public int? MediaId { get; set; }
    public Media? Media { get; set; }
    public int? IncubatorEquipmentId { get; set; }
    public Equipment? IncubatorEquipment { get; set; }
    public string? Temperature { get; set; }
    public string? Duration { get; set; }
    public DateTime? ExpectedReadingAt { get; set; }
}
