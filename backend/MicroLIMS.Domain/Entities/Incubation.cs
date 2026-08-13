using System.ComponentModel.DataAnnotations.Schema;

namespace MicroLIMS.Domain.Entities;

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

    public int? MediaId { get; set; }
    public Media? Media { get; set; }

    public int? IncubatorEquipmentId { get; set; }
    public Equipment? IncubatorEquipment { get; set; }
    public string? Temperature { get; set; }
    public string? Duration { get; set; }
    public DateTime? ExpectedReadingAt { get; set; }

    // Analyst-declared incubation window. The lock in Task 8 is
    // enforced against IncubationEndUtc.
    public DateTime? IncubationStartUtc { get; set; }
    public DateTime? IncubationEndUtc { get; set; }

    // Server clock reading taken when the declared window above was
    // received. The window is analyst-supplied and therefore a claim;
    // this is the one timestamp on the row the analyst cannot influence,
    // so a reviewer can always see what was claimed AND when it was
    // actually submitted (ALCOA+ Contemporaneous/Attributable). Never
    // used to gate anything - it is evidence, not a control.
    public DateTime? WindowReceivedAtUtc { get; set; }

    [NotMapped]
    public bool IsIncubationComplete =>
        IncubationEndUtc.HasValue && DateTime.UtcNow >= IncubationEndUtc.Value;
}
