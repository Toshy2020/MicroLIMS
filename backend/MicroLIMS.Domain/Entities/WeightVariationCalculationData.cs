using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

public record WeightVariationConfigData(
    int UnitCount,
    decimal TabletBand1MaxMg,
    decimal TabletBand1Percent,
    decimal TabletBand2MaxMg,
    decimal TabletBand2Percent,
    decimal TabletBand3Percent,
    int TabletMaxOutside,
    decimal CapsuleInnerPercent,
    decimal CapsuleOuterPercent,
    int CapsuleS1MaxOutside,
    int CapsuleS1MaxForRetest,
    int CapsuleS2ExtraUnits,
    int CapsuleS2MaxOutside);

public record WeightVariationUnitData(
    int Stage,
    int UnitIndex,
    decimal? WeightMg,
    decimal? GrossMg,
    decimal? ShellMg,
    decimal NetMg,
    decimal DeviationPercent,
    bool Passed);

public record WeightVariationCalculationData(
    DosageForm DosageForm,
    WeightVariationConfigData Config,
    bool? StepAPassed,
    decimal MeanWeightMg,
    decimal? MeanGrossMg,
    decimal? BandPercentUsed,
    IReadOnlyList<WeightVariationUnitData> Units,
    string Outcome,
    IReadOnlyList<string> Reasons);
