using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

// System Suitability Run (REQ-FP-001/001a/002/040/041)
// Represents a single HPLC suitability test run. Created via a signed action,
// immutable afterwards. Failed runs are retained for traceability.
public class SystemSuitabilityRun
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty; // e.g. "VIT-C S.S 05/112026"

    public int TestDefinitionId { get; set; }
    public TestDefinition? TestDefinition { get; set; }

    public int SectionId { get; set; }
    public DocumentSection? Section { get; set; }

    public int EquipmentId { get; set; }
    public Equipment? Equipment { get; set; }

    // Null for titration runs (no column)
    public int? ChromatographyColumnId { get; set; }
    public ChromatographyColumn? ChromatographyColumn { get; set; }

    public int ReferenceStandardMaterialId { get; set; }
    public Material? ReferenceStandardMaterial { get; set; }

    // Snapshot copied from Material.Purity at creation
    public decimal StandardPurityPercent { get; set; }
    public decimal StandardWeightMg { get; set; }
    public decimal StandardDilution { get; set; }
    public decimal StandardMeanArea { get; set; }

    // Entered criteria typed from CDS report (REQ-FP-001a)
    public decimal? RsdPercent { get; set; }
    public decimal? Resolution { get; set; }
    public decimal? TailingFactor { get; set; }
    public decimal? TheoreticalPlates { get; set; }

    // Pass rule evaluated server-side against TestDefinition acceptance criteria
    public bool Passed { get; set; }
    public string? FailureReasons { get; set; }

    public int PerformedByUserId { get; set; }
    public User? PerformedByUser { get; set; }

    public DateTime PerformedAt { get; set; }

    public int SignatureId { get; set; }
    public ElectronicSignature? Signature { get; set; }

    public string? Comment { get; set; }

    // Th.Wt.std: method target weighing for standard (> 0 when given)
    public decimal? TheoreticalWeightMg { get; set; }

    // MC: working standard moisture content (%), measured per run, never defaulted or copied from a previous run (0 <= x < 100 when given)
    public decimal? MoisturePercent { get; set; }

    // Computed deviation of actual weight from theoretical weight (%)
    public decimal? StandardWeighInDeviationPercent { get; set; }

    // True if deviation exceeds the ±5% window (warning only, never hard failure)
    public bool StandardWeighInOutOfWindow { get; set; }

    // Justification when standard weight is outside the ±5% window (max 1000)
    public string? WeighInJustification { get; set; }

    // Computed RSD across replicate responses (drives pass/fail gate when responses are given; transcribed RsdPercent is recorded alongside)
    public decimal? ComputedRsdPercent { get; set; }

    public List<SystemSuitabilityRunAnalyte> Analytes { get; set; } = new();
}
