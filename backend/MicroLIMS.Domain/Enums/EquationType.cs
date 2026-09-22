namespace MicroLIMS.Domain.Enums;

public enum EquationType
{
    None = 0,
    HplcAssay,        // retired (SC-3), value kept
    SystemSuitability,
    CalibrationCurve,
    Measurement,
    GravimetricLoss,
    GravimetricResidue,
    Qualitative,
    Dissolution,
    Disintegration,
    WeightVariation,
    HplcMultiAnalyte, // retired (SC-3), value kept
    StandardComparison
}
