using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

public record HplcMultiAnalyteInjectionData(
    int PreparationIndex,
    int InjectionIndex,
    decimal Area,
    decimal AmountPerUnit,
    decimal ConvertedAmount);

public record HplcMultiAnalytePreparationData(
    int PreparationIndex,
    decimal SampleAmount,
    decimal SampleDilutionMl,
    IReadOnlyList<HplcMultiAnalyteInjectionData> Injections,
    decimal MeanAmountPerUnit,
    decimal MeanConvertedAmount);

public record HplcMultiAnalyteCalculationData(
    string AnalyteName,
    int TestAnalyteId,
    int SystemSuitabilityRunAnalyteId,
    decimal StandardWeightMg,
    decimal StandardPurityPercent,
    decimal StandardDilution,
    decimal StandardMeanArea,
    decimal Cs,
    string UnitAmountSource,
    decimal UnitAmount,
    SampleMatrix SampleMatrix,
    IReadOnlyList<HplcMultiAnalytePreparationData> Preparations,
    decimal Mpu,
    decimal ConversionFactor,
    decimal Result,
    decimal? LabelClaim,
    string? LabelClaimUnit,
    decimal? PercentLabelClaim,
    decimal? PreparationRsdPercent,
    decimal? MaxPreparationRsdPercent,
    bool RsdExceeded,
    string? ReviewReason);
