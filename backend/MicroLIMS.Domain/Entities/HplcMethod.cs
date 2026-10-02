using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

// HPLC method (HPLC chain S3). One record referenced by tests; edited in place
// with a reason; runs store a snapshot (D7). Deactivated, never deleted.
public class HplcMethod : IVersionedEntity
{
    public int Id { get; set; }
    public uint Version { get; set; }
    public int SectionId { get; set; }
    public DocumentSection? Section { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Abbreviation { get; set; } = string.Empty;   // upper-case, used in MP/SST/run codes
    public DateTime EffectiveDate { get; set; }
    public bool IsActive { get; set; } = true;

    // Column (USP <621> Appendix A)
    public string ColumnDesignation { get; set; } = string.Empty; // e.g. "L1"
    public decimal ColumnLengthMm { get; set; }
    public decimal ColumnInternalDiameterMm { get; set; }
    public decimal ParticleSizeUm { get; set; }
    public string? ColumnBrand { get; set; }
    public string? ColumnPartNumber { get; set; }
    public decimal ColumnTemperatureC { get; set; }

    // Elution
    public ElutionMode ElutionMode { get; set; }
    public decimal? EquilibrationMin { get; set; }
    public decimal FlowRateMlPerMin { get; set; }

    // Detection / injection
    public HplcDetectorType DetectorType { get; set; }
    public decimal InjectionVolumeUl { get; set; }
    public decimal RunTimeMin { get; set; }

    public int DiluentSolutionId { get; set; }
    public SolutionMaster? DiluentSolution { get; set; }

    public List<HplcMethodMobilePhase> MobilePhases { get; set; } = new();
    public List<HplcMethodGradientStep> GradientSteps { get; set; } = new();
    public List<HplcMethodAnalyte> Analytes { get; set; } = new();

    public int CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public int LastModifiedByUserId { get; set; }
    public DateTime LastModifiedAt { get; set; }
}

public class HplcMethodMobilePhase
{
    public int Id { get; set; }
    public int HplcMethodId { get; set; }
    public HplcMethod? HplcMethod { get; set; }
    public string Channel { get; set; } = string.Empty;   // "A".."D"
    public int SolutionMasterId { get; set; }
    public SolutionMaster? SolutionMaster { get; set; }
    public decimal? RatioPercent { get; set; }            // isocratic composition
}

public class HplcMethodGradientStep
{
    public int Id { get; set; }
    public int HplcMethodId { get; set; }
    public HplcMethod? HplcMethod { get; set; }
    public decimal TimeMin { get; set; }
    public decimal PercentA { get; set; }
    public decimal PercentB { get; set; }
    public decimal PercentC { get; set; }
    public decimal PercentD { get; set; }
}

// Kept on update when unchanged (matched by Id) so specification rows stay linked.
public class HplcMethodAnalyte
{
    public int Id { get; set; }
    public int HplcMethodId { get; set; }
    public HplcMethod? HplcMethod { get; set; }
    public int DisplayOrder { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal WavelengthNm { get; set; }
    public int StandardEntryId { get; set; }              // ReferenceStandard master entry
    public MaterialMasterEntry? StandardEntry { get; set; }
    public decimal TheoreticalWeightStdMg { get; set; }
    public decimal TheoreticalWeightTestMg { get; set; }
    public int StandardInjections { get; set; }
    public decimal? SstMaxRsdPercent { get; set; }
    public decimal? SstMinResolution { get; set; }
    public decimal? SstMaxTailingFactor { get; set; }
    public decimal? SstMinTheoreticalPlates { get; set; }
    public decimal? SstMinRetentionFactor { get; set; }
    public decimal? SstMinSignalToNoise { get; set; }
    public decimal? SstMinPeakToValley { get; set; }
    public decimal? StandardDilution { get; set; } // mL; dissolution standard only (Cs = W x P x (100-MC) / dilution)
}
