using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

public record HplcAssayReplicate(int ReplicateNumber, decimal Area, decimal AssayPercent);

// HPLC Assay Result (REQ-FP-004/021/033)
// Represents a recorded HPLC assay result for a TestOrder.
// Requires an electronic signature and a linked Passed SystemSuitabilityRun.
public class HplcAssayResult
{
    public int Id { get; set; }

    public int TestOrderId { get; set; }
    public TestOrder? TestOrder { get; set; }

    public int SystemSuitabilityRunId { get; set; }
    public SystemSuitabilityRun? SystemSuitabilityRun { get; set; }

    // Snapshot of the run's standard values used in calculation
    public decimal StandardPurityPercent { get; set; }
    public decimal StandardWeightMg { get; set; }
    public decimal StandardDilution { get; set; }
    public decimal StandardMeanArea { get; set; }

    // Sample inputs
    public decimal SampleWeightMg { get; set; }
    public decimal SampleDilution { get; set; }

    // Replicate areas and per-replicate % stored as jsonb
    public string ReplicatesJson { get; set; } = "[]";

    public decimal MeanAssayPercent { get; set; }
    public string ReportedResult { get; set; } = string.Empty; // e.g. "99.5 %"
    public string? AlertLimit { get; set; }
    public string? ActionLimit { get; set; }
    public string? SpecLimit { get; set; }
    public string ComparisonStatus { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public int EnteredByUserId { get; set; }
    public User? EnteredByUser { get; set; }

    public DateTime EnteredAt { get; set; } = DateTime.UtcNow;

    public int SignatureId { get; set; }
    public ElectronicSignature? Signature { get; set; }
}
