namespace MicroLIMS.Domain.Entities;

// A medium permitted on a workflow step, configured in Test Master.
// MediaProductId points at the Culture Media entry (the product); any released
// Media lot of that product fits, and the physical lot chosen at run time is a
// Media row.
public class TestWorkflowStepMedia
{
    public int Id { get; set; }

    public int TestWorkflowStepId { get; set; }
    public TestWorkflowStep? TestWorkflowStep { get; set; }

    // Source of truth. Required on every row written by Test Master; nullable at
    // the database level only because the Master Testing Materials backfill could
    // not resolve some historic rows (see the StepMediaDiluentMediaProduct migration).
    public int? MediaProductId { get; set; }
    public MediaProduct? MediaProduct { get; set; }

    // Legacy: the dehydrated-media stock lot that stood in for "the medium" before
    // step media pointed at the product. Kept for history; no longer written or read.
    public int? MaterialId { get; set; }
    public Material? Material { get; set; }

    // The configured condition (time + temperature) from which TempMin/Max and
    // IncubationMinHours/MaxHours below were copied at save time in Test Master.
    // Nullable - legacy rows created before this field existed have none.
    // Denormalized on purpose, not resolved live through this FK: every
    // execution-time reader (IncubatorEligibilityService, SelectMediaAsync,
    // the GET projections) reads TempMin/Max and IncubationMinHours/MaxHours
    // directly off this row snapshot, and conditions are locked once used.
    public int? MediaIncubationConditionId { get; set; }
    public MediaIncubationCondition? IncubationCondition { get; set; }

    // Bounds the incubators offered for this medium at run time. Copied from
    // the chosen MediaIncubationCondition at save time (see MasterDataController).
    public decimal TempMin { get; set; }
    public decimal TempMax { get; set; }

    // Incubation duration bounds copied from the chosen MediaIncubationCondition
    // at save time (see MasterDataController).
    public int IncubationMinHours { get; set; }
    public int IncubationMaxHours { get; set; }

    // True = mandatory single medium (broth and selective plating steps).
    // False = analyst-selectable from the permitted list (confirmatory).
    public bool IsRequired { get; set; }

    public int DisplayOrder { get; set; }
}
