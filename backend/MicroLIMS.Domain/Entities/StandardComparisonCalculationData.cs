namespace MicroLIMS.Domain.Entities;

public record StandardComparisonPreparationData(
    int PreparationIndex,
    decimal TheoreticalWeightMg,
    decimal ActualWeightMg,
    decimal WeighInDeviationPercent,
    bool WeighInOutOfWindow,
    string? WeighInJustification,
    decimal TestResponse,
    decimal PercentAssay);

public record StandardComparisonCalculationData(
    string AnalyteName,
    int TestAnalyteId,
    int SystemSuitabilityRunAnalyteId,
    decimal StandardTheoreticalWeightMg,
    decimal StandardActualWeightMg,
    decimal StandardPurityPercent,
    decimal MoisturePercent,
    decimal StandardMeanArea,
    IReadOnlyList<StandardComparisonPreparationData> Preparations,
    decimal ReportedPercentAssay,
    decimal? PreparationRsdPercent,
    decimal? MaxPreparationRsdPercent,
    bool RsdExceeded,
    string? ReviewReason);
