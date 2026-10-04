using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

// ICP-OES method master (spec 2026-10-03 §4.1). Same lifecycle as HplcMethod:
// edited in place with a reason, runs snapshot it, deactivated never deleted.
public class IcpMethod : IVersionedEntity
{
    public int Id { get; set; }
    public uint Version { get; set; }
    public int SectionId { get; set; }
    public DocumentSection? Section { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Abbreviation { get; set; } = string.Empty;   // upper-case, used in run codes
    public DateTime EffectiveDate { get; set; }
    public bool IsActive { get; set; } = true;
    public IcpMethodMode Mode { get; set; }                    // fixed at creation

    // Calibration (D3: summary only - one r per element at run time)
    public string StandardLevelsMgPerL { get; set; } = string.Empty; // normalised "0.1, 0.5, 1, 3, 6"
    public int CalibrationStandardEntryId { get; set; }
    public MaterialMasterEntry? CalibrationStandardEntry { get; set; }
    public decimal MinCorrelation { get; set; }                // e.g. 0.999

    // Optional checks (D4, all off by default)
    public bool RequireBlank { get; set; }
    public decimal? BlankMaxMgPerL { get; set; }
    public bool RequireIcv { get; set; }
    public int? IcvStandardEntryId { get; set; }               // second source
    public MaterialMasterEntry? IcvStandardEntry { get; set; }
    public decimal? IcvNominalMgPerL { get; set; }
    public decimal? IcvRecoveryLowPercent { get; set; }
    public decimal? IcvRecoveryHighPercent { get; set; }
    public bool RequireCcv { get; set; }
    public decimal? CcvNominalMgPerL { get; set; }
    public decimal? CcvRecoveryLowPercent { get; set; }
    public decimal? CcvRecoveryHighPercent { get; set; }
    public int MaxCalibrationAgeHours { get; set; } = 24;

    // Sample-prep defaults, prefilled on entry (I2)
    public decimal SampleVolumeMl { get; set; }
    public decimal DilutionFactor { get; set; } = 1m;

    public List<IcpMethodElement> Elements { get; set; } = new();

    public int CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public int LastModifiedByUserId { get; set; }
    public DateTime LastModifiedAt { get; set; }
}

// Kept by Id on update so specification rows stay linked.
public class IcpMethodElement
{
    public int Id { get; set; }
    public int IcpMethodId { get; set; }
    public IcpMethod? IcpMethod { get; set; }
    public int DisplayOrder { get; set; }
    public string Symbol { get; set; } = string.Empty;         // "Zn", "Ca"
    public decimal WavelengthNm { get; set; }
    public AnalyteView View { get; set; }
    public decimal ConversionFactor { get; set; } = 1m;        // element -> reported form
}
