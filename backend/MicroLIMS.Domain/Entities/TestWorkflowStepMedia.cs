namespace MicroLIMS.Domain.Entities;

// A medium permitted on a workflow step, configured in Test Master.
// MaterialId points at the medium itself (a Material with MaterialType.
// DehydratedMedia); the physical lot chosen at run time is a Media row.
public class TestWorkflowStepMedia
{
    public int Id { get; set; }

    public int TestWorkflowStepId { get; set; }
    public TestWorkflowStep? TestWorkflowStep { get; set; }

    public int MaterialId { get; set; }
    public Material? Material { get; set; }

    // The configured profile TempMin/Max below were copied from, when one
    // was picked in Test Master. Nullable - rows created before this field
    // existed, or via any path that still free-types the range, have none.
    // Denormalized on purpose, not resolved live through this FK: every
    // execution-time reader (IncubatorEligibilityService, SelectMediaAsync,
    // the GET projections) already reads TempMin/Max directly off this row,
    // and a later edit to the MediaConfiguration shouldn't silently change
    // what an already-saved step template requires without a human
    // re-saving it - same reasoning as MediaAppearanceSnapshotService's
    // snapshot-on-write.
    public int? MediaConfigurationId { get; set; }
    public MediaConfiguration? MediaConfiguration { get; set; }

    // Bounds the incubators offered for this medium at run time. Free-typed
    // when MediaConfigurationId is null; otherwise server-derived from that
    // row's TemperatureMin/Max at save time (see MasterDataController).
    public decimal TempMin { get; set; }
    public decimal TempMax { get; set; }

    // The step's own IncubationMinHours/MaxHours/TemperatureMin/Max are no
    // longer read at execution time (see TestWorkflowEngine.cs) - a step
    // with more than one permitted medium can have genuinely different
    // requirements per medium (e.g. Confirmatory Plating's XLD at 35-37C
    // vs TSI at 40-45C), which a single step-level field can never
    // represent. This medium's own window is now the operative source,
    // same treatment and same reasoning as TempMin/TempMax above.
    public int IncubationMinHours { get; set; }
    public int IncubationMaxHours { get; set; }

    // True = mandatory single medium (broth and selective plating steps).
    // False = analyst-selectable from the permitted list (confirmatory).
    public bool IsRequired { get; set; }

    public int DisplayOrder { get; set; }
}
