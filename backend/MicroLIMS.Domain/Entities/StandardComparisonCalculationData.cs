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
    string? ReviewReason,
    // "PeakArea" or "TitrationVolume"; for titration StandardMeanArea holds the mean EP_std (mL)
    // and TestResponse the EP_test (mL); BlankTitreMl is the run's EP_blank.
    string ResponseMode = "PeakArea",
    decimal? BlankTitreMl = null);
