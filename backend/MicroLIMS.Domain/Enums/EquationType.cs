namespace MicroLIMS.Domain.Enums;

public enum EquationType
{
    None = 0,
    HplcAssay,        // retired (SC-3), value kept
    SystemSuitability,
    CalibrationCurve, // retired (slice R)
    Measurement,
    GravimetricLoss,
    GravimetricResidue,
    Qualitative,
    Dissolution,
    Disintegration,
    WeightVariation,
    HplcMultiAnalyte, // retired (SC-3), value kept
    StandardComparison, // retired, value kept
    HplcMethodAssay, // HPLC chain S3 - assay against an HplcMethod master (spec 3.3-3.4)
    Titration, // volumetric titration assay
    IcpMethodAssay // ICP workspace - assay against an IcpMethod master (spec 2026-10-03 §4.1)
}
