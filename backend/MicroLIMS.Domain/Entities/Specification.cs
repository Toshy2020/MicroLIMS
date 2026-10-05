using System.Text.Json.Serialization;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

public class Specification : IVersionedEntity
{
    public int Id { get; set; }
    public uint Version { get; set; }
    public int ItemId { get; set; }
    [JsonIgnore]
    public Item? Item { get; set; }
    public string TestCode { get; set; } = string.Empty;

    // The production stage this row applies to (e.g. Bulk vs Finished),
    // keyed on ProductionStageRole - never the renameable stage name.
    // Null = every stage. For a sample whose stage role has rows of its
    // own for this TestCode, those rows are used and the every-stage rows
    // are not; otherwise the every-stage rows are (SpecificationStageResolver).
    public ProductionStageRole? ProductionStageRole { get; set; }
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

    // Result basis / sample matrix / label claim / conversion factor
    public ResultBasis? ResultBasis { get; set; }
    public SampleMatrix? SampleMatrix { get; set; }
    public decimal? LabelClaim { get; set; }
    public string? LabelClaimUnit { get; set; }
    public decimal ConversionFactor { get; set; } = 1.0m;

    // Finished Product / Weight Variation
    public DosageForm? DosageForm { get; set; }

    // HPLC chain S3 - HplcMethodAssay specification rows are keyed by
    // method analyte (spec 3.4).
    public int? HplcMethodAnalyteId { get; set; }
    [JsonIgnore]
    public HplcMethodAnalyte? HplcMethodAnalyte { get; set; }

    // ICP workspace - IcpMethodAssay specification rows are keyed by method element + basis.
    public int? IcpMethodElementId { get; set; }
    [JsonIgnore]
    public IcpMethodElement? IcpMethodElement { get; set; }

    public List<SpecificationStage> Stages { get; set; } = new();
}
