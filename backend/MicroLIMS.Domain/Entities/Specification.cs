using System.Text.Json.Serialization;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

public class Specification
{
    public int Id { get; set; }
    public int ItemId { get; set; }
    [JsonIgnore]
    public Item? Item { get; set; }
    public string TestCode { get; set; } = string.Empty;
    public string AlertLimit { get; set; } = string.Empty;
    public string ActionLimit { get; set; } = string.Empty;
    public string SpecLimit { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;

    // Only meaningful for count-type tests (TAMC/TYMC) - pathogen
    // presence/absence tests never set this. Null means "not configured
    // yet", which RecordCountTestAsync treats as blocking result entry
    // rather than silently defaulting to 1, so an unconfigured DF can't
    // slip through unnoticed.
    public decimal? DilutionFactor { get; set; }

    // Universal Specifications
    public string ParameterName { get; set; } = string.Empty;
    public int DisplayOrder { get; set; } = 0;
    public LimitType LimitType { get; set; } = LimitType.CountTiered;
    public string? ReferenceStandard { get; set; }
    public decimal? LowerLimit { get; set; }
    public decimal? UpperLimit { get; set; }
    public bool LowerInclusive { get; set; } = true;
    public bool UpperInclusive { get; set; } = true;
    public decimal? Target { get; set; }
    public decimal? Tolerance { get; set; }
    public ToleranceMode? ToleranceMode { get; set; }
    public string? ExpectedResultText { get; set; }
    public ExpectedPresence? ExpectedState { get; set; }
    public decimal? SampleQuantity { get; set; }
    public string? SampleQuantityUnit { get; set; }

    // Elemental Assay / Calibration Curve (Slice S3a)
    public int? TestAnalyteId { get; set; }
    [JsonIgnore]
    public TestAnalyte? TestAnalyte { get; set; }
    public ResultBasis? ResultBasis { get; set; }
    public SampleMatrix? SampleMatrix { get; set; }
    public decimal? LabelClaim { get; set; }
    public string? LabelClaimUnit { get; set; }
    public decimal ConversionFactor { get; set; } = 1.0m;

    // Finished Product / Weight Variation
    public DosageForm? DosageForm { get; set; }

    public List<SpecificationStage> Stages { get; set; } = new();
}
