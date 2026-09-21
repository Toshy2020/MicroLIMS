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
}
