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

    // Who started this incubation window (selected media/lot and
    // incubator, or - for StageNumber 2 - performed the transfer). Not
    // the same person as whoever later records the count: see
    // CountTestReading.EnteredByUserId / SampleLocation.EnteredByUserId.
    public int? StartedByUserId { get; set; }

    // Set only on a StageNumber == 2 (or higher) row: the stage 1 row this
    // one continues from. The physical plate does not change between
    // stages, so MediaId is copied from the parent, never reselected.
    public int? ParentIncubationId { get; set; }
    public Incubation? ParentIncubation { get; set; }

    // 1 for every incubation window that isn't part of a transfer. A
    // transfer-enabled PlateCount step's stage 2 is a NEW row with
    // StageNumber == 2 and ParentIncubationId pointing at stage 1 - never
    // a mutation of the stage 1 row.
    public int StageNumber { get; set; } = 1;

    [NotMapped]
    public bool IsIncubationComplete =>
        IncubationEndUtc.HasValue && DateTime.UtcNow >= IncubationEndUtc.Value;
}
