using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

// Elemental Assay Result (REQ-FP-022, Slice S3a)
// Represents one element's quantified result for a TestOrder and Specification.
public class ElementalAssayResult
{
    public int Id { get; set; }

    public int EntryId { get; set; }
    public ElementalAssayEntry? Entry { get; set; }

    public int TestOrderId { get; set; }
    public TestOrder? TestOrder { get; set; }

    public int SpecificationId { get; set; }
    public Specification? Specification { get; set; }

    public int CalibrationRunAnalyteId { get; set; }
    public CalibrationRunAnalyte? CalibrationRunAnalyte { get; set; }

    // Snapshots
    public string ParameterName { get; set; } = string.Empty;
    public string Element { get; set; } = string.Empty;

    public decimal ReportedPpm { get; set; }
    public bool OverRange { get; set; }
    public bool BelowLoq { get; set; }

    public decimal? MgPerUnit { get; set; }
    public decimal? ResultClaim { get; set; }
    public decimal? PercentLabelClaim { get; set; }
    public decimal? ReportedValue { get; set; }
    public string ReportedDisplay { get; set; } = string.Empty; // e.g. "10.6 mg", "106.3 %", "<LOQ", "Over range"

    public ResultBasis ResultBasis { get; set; }
    public string? SpecLimit { get; set; }
    public string? Unit { get; set; }
    public string ComparisonStatus { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
}
