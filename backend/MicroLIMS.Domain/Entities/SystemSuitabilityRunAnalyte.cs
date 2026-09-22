namespace MicroLIMS.Domain.Entities;

public class SystemSuitabilityRunAnalyte
{
    public int Id { get; set; }

    public int SystemSuitabilityRunId { get; set; }
    public SystemSuitabilityRun? SystemSuitabilityRun { get; set; }

    public int TestAnalyteId { get; set; }
    public TestAnalyte? TestAnalyte { get; set; }

    // Snapshots copied from TestAnalyte
    public string AnalyteName { get; set; } = string.Empty;
    public decimal WavelengthNm { get; set; }

    public int ReferenceStandardMaterialId { get; set; }
    public Material? ReferenceStandardMaterial { get; set; }

    // Snapshot copied from Material.Purity at creation
    public decimal StandardPurityPercent { get; set; }
    public decimal StandardWeightMg { get; set; }
    public decimal StandardDilution { get; set; }
    public decimal StandardMeanArea { get; set; }

    // Entered criteria typed from CDS report
    public decimal? RsdPercent { get; set; }
    public decimal? Resolution { get; set; }
    public decimal? TailingFactor { get; set; }
    public decimal? TheoreticalPlates { get; set; }

    // Pass rule evaluated server-side against TestAnalyte acceptance criteria
    public bool Passed { get; set; }
    public string? FailureReasons { get; set; }

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

    // Replicate responses for this standard
    public List<SystemSuitabilityStandardResponse> Responses { get; set; } = new();
}
