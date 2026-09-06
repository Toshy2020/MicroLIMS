namespace MicroLIMS.Domain.Entities;

// Plate-reading calculation for a Count Test (TAMC/TYMC) - average of the
// entered plate readings, multiplied by the dilution factor, compared
// against Spec/Action/Alert limits. Mirrors the Water calculation engine
// but persists its own record since Count Tests also track an
// incubation setup step Water doesn't have.
public class CountTestReading
{
    public int Id { get; set; }
    public int TestOrderId { get; set; }
    public TestOrder? TestOrder { get; set; }
    // Which TestWorkflowStep this reading belongs to - needed so a
    // multi-step CountTest template (e.g. "CountIncubation" then
    // "transfer") can tell which steps are actually done instead of
    // treating any reading on the test order as completing the whole chain.
    public string? StepName { get; set; }
    public string PlateReadings { get; set; } = string.Empty; // comma-separated raw readings
    // The value actually used in the CFU calculation - configured value
    // unless the analyst overrode it.
    public decimal DilutionFactor { get; set; }
    // ALCOA+ snapshot of what Specifications had configured at entry
    // time, so a later config change can't retroactively alter what this
    // result claims was used. Null for direct-count sample types (Water/
    // EM/AfterCleaning), which never go through Specifications.
    public decimal? ConfiguredDilutionFactor { get; set; }
    public bool DilutionFactorOverridden { get; set; } = false;
    public string? DilutionFactorOverrideNote { get; set; }
    public decimal? Average { get; set; }
    public decimal? CalculatedResult { get; set; }
    public string ReportedResult { get; set; } = string.Empty; // "<1" or the rounded whole number / "TNTC"
    public string? AlertLimit { get; set; }
    public string? ActionLimit { get; set; }
    public string? SpecLimit { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool HasNonNumericReading { get; set; } = false;
    public string? NonNumericValue { get; set; } // "TNTC" | "Uncountable"
    public bool RequiresReview { get; set; } = false;
    public bool IsActive { get; set; } = true;
    public int EnteredByUserId { get; set; }
    public DateTime EnteredAt { get; set; } = DateTime.UtcNow;
}
