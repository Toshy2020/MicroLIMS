using System.Globalization;
using System.Text.Json;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Helpers;

public record StandardComparisonCalculationResult(
    IReadOnlyList<StandardComparisonPreparationData> Preparations,
    decimal ReportedValue,
    string ReportedDisplay,
    decimal? PreparationRsdPercent,
    bool RsdExceeded,
    string? ReviewReason,
    string ComparisonStatus,
    string CalculationJson,
    StandardComparisonCalculationData CalculationData);

public static class StandardComparisonCalculator
{
    // SOP STM-PC-013 6.9.2.5 / STM-PC-023: sample weigh-in tolerance is ±10% of theoretical weight (warning only)
    public const decimal SampleWeighInTolerancePercent = 10m;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static decimal CalculatePreparationAssay(
        decimal responseTest,
        decimal responseStd,
        decimal actWtStd,
        decimal thWtStd,
        decimal thWtTest,
        decimal actWtTest,
        decimal moisturePercent,
        decimal purityPercent,
        decimal? blankTitre = null)
    {
        if (blankTitre.HasValue)
        {
            // Titration (SOP STM-PC-013 6.9.2.5): Response = (EP_test - EP_blank) / (EP_std - EP_blank)
            if (blankTitre.Value < 0m)
                throw new InvalidOperationException("Blank titre must not be negative.");
            if (responseTest <= blankTitre.Value)
                throw new InvalidOperationException("Sample titre must be greater than the blank titre.");
            if (responseStd <= blankTitre.Value)
                throw new InvalidOperationException("Standard titre must be greater than the blank titre.");
        }

        if (responseTest <= 0m)
            throw new InvalidOperationException("Test response must be greater than zero.");
        if (responseStd <= 0m)
            throw new InvalidOperationException("Standard response must be greater than zero.");
        if (actWtStd <= 0m)
            throw new InvalidOperationException("Standard actual weight must be greater than zero.");
        if (thWtStd <= 0m)
            throw new InvalidOperationException("Standard theoretical weight must be greater than zero.");
        if (thWtTest <= 0m)
            throw new InvalidOperationException("Sample theoretical weight must be greater than zero.");
        if (actWtTest <= 0m)
            throw new InvalidOperationException("Sample actual weight must be greater than zero.");
        if (moisturePercent < 0m || moisturePercent >= 100m)
            throw new InvalidOperationException("Standard moisture percent must be between 0 and 100 (exclusive).");
        if (purityPercent <= 0m)
            throw new InvalidOperationException("Standard purity percent must be greater than zero.");

        // % Assay (of label claim) =
        //     (Response_test / Response_std)      [titration: (EP_test - EP_blank) / (EP_std - EP_blank)]
        //   x (ActWt_std / ThWt_std)
        //   x (ThWt_test / ActWt_test)
        //   x ((100 - MC) / 100)
        //   x P
        decimal blank = blankTitre ?? 0m;
        decimal responseRatio = (responseTest - blank) / (responseStd - blank);
        decimal stdWeightRatio = actWtStd / thWtStd;
        decimal sampleWeightRatio = thWtTest / actWtTest;
        decimal moistureCorrection = (100m - moisturePercent) / 100m;

        return responseRatio * stdWeightRatio * sampleWeightRatio * moistureCorrection * purityPercent;
    }

    public static (decimal? RsdPercent, bool RsdExceeded, string? ReviewReason) CalculatePreparationRsd(
        IReadOnlyList<decimal> preparationValues,
        decimal? maxPreparationRsdPercent)
    {
        if (preparationValues == null || preparationValues.Count < 2)
            return (null, false, null);

        decimal mean = preparationValues.Average();
        if (mean == 0m)
            return (0m, false, null);

        int n = preparationValues.Count;
        decimal sumSquaredDiffs = 0m;
        foreach (var val in preparationValues)
        {
            decimal diff = val - mean;
            sumSquaredDiffs += diff * diff;
        }

        decimal variance = sumSquaredDiffs / (n - 1);
        decimal stdDev = DecimalMath.Sqrt(variance, 10);
        decimal rsd = (stdDev / mean) * 100m;

        bool rsdExceeded = maxPreparationRsdPercent.HasValue && rsd > maxPreparationRsdPercent.Value;
        string? reviewReason = rsdExceeded
            ? $"Preparation RSD {Math.Round(rsd, 2, MidpointRounding.AwayFromZero):0.00}% exceeds maximum allowed {Math.Round(maxPreparationRsdPercent!.Value, 2, MidpointRounding.AwayFromZero):0.00}%."
            : null;

        return (rsd, rsdExceeded, reviewReason);
    }

    public static StandardComparisonCalculationResult Calculate(
        string analyteName,
        int testAnalyteId,
        int systemSuitabilityRunAnalyteId,
        decimal standardTheoreticalWeightMg,
        decimal standardActualWeightMg,
        decimal standardPurityPercent,
        decimal moisturePercent,
        decimal standardMeanArea,
        Specification spec,
        IReadOnlyList<Workflows.StandardComparisonPreparationInput> preparations,
        IReadOnlyList<Workflows.StandardComparisonResponseInput> responses,
        decimal? maxPreparationRsdPercent,
        ResponseMode responseMode = ResponseMode.PeakArea,
        decimal? blankTitreMl = null)
    {
        if (responseMode == ResponseMode.TitrationVolume && !blankTitreMl.HasValue)
            throw new InvalidOperationException("Blank titre is required for titration.");
        if (responseMode == ResponseMode.PeakArea && blankTitreMl.HasValue)
            throw new InvalidOperationException("Blank titre applies only to titration.");

        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(preparations);
        ArgumentNullException.ThrowIfNull(responses);

        var prepDataList = new List<StandardComparisonPreparationData>();

        for (int p = 1; p <= preparations.Count; p++)
        {
            var prep = preparations[p - 1];
            var respMatch = responses.FirstOrDefault(r => r.PreparationIndex == p)
                ?? throw new InvalidOperationException($"Missing response for preparation {p}.");

            decimal deviation = (prep.ActualWeightMg - prep.TheoreticalWeightMg) / prep.TheoreticalWeightMg * 100m;
            bool outOfWindow = Math.Abs(deviation) > SampleWeighInTolerancePercent;

            decimal prepAssay = CalculatePreparationAssay(
                respMatch.Response,
                standardMeanArea,
                standardActualWeightMg,
                standardTheoreticalWeightMg,
                prep.TheoreticalWeightMg,
                prep.ActualWeightMg,
                moisturePercent,
                standardPurityPercent,
                blankTitreMl);

            prepDataList.Add(new StandardComparisonPreparationData(
                PreparationIndex: p,
                TheoreticalWeightMg: prep.TheoreticalWeightMg,
                ActualWeightMg: prep.ActualWeightMg,
                WeighInDeviationPercent: deviation,
                WeighInOutOfWindow: outOfWindow,
                WeighInJustification: prep.WeighInJustification,
                TestResponse: respMatch.Response,
                PercentAssay: prepAssay));
        }

        decimal reportedValue = prepDataList.Select(p => p.PercentAssay).Average();

        var (rsdPercent, rsdExceeded, reviewReason) = CalculatePreparationRsd(
            prepDataList.Select(p => p.PercentAssay).ToList(),
            maxPreparationRsdPercent);

        var rounded = Math.Round(reportedValue, 1, MidpointRounding.AwayFromZero).ToString("0.0", CultureInfo.InvariantCulture);
        string reportedDisplay = $"{rounded} %";

        string status = SpecificationEvaluator.Evaluate(spec, reportedValue);
        if (rsdExceeded)
        {
            status = "RequiresReview";
        }

        var calcData = new StandardComparisonCalculationData(
            AnalyteName: analyteName,
            TestAnalyteId: testAnalyteId,
            SystemSuitabilityRunAnalyteId: systemSuitabilityRunAnalyteId,
            StandardTheoreticalWeightMg: standardTheoreticalWeightMg,
            StandardActualWeightMg: standardActualWeightMg,
            StandardPurityPercent: standardPurityPercent,
            MoisturePercent: moisturePercent,
            StandardMeanArea: standardMeanArea,
            Preparations: prepDataList,
            ReportedPercentAssay: reportedValue,
            PreparationRsdPercent: rsdPercent,
            MaxPreparationRsdPercent: maxPreparationRsdPercent,
            RsdExceeded: rsdExceeded,
            ReviewReason: reviewReason,
            ResponseMode: responseMode.ToString(),
            BlankTitreMl: blankTitreMl);

        string calculationJson = JsonSerializer.Serialize(calcData, JsonOptions);

        return new StandardComparisonCalculationResult(
            Preparations: prepDataList,
            ReportedValue: reportedValue,
            ReportedDisplay: reportedDisplay,
            PreparationRsdPercent: rsdPercent,
            RsdExceeded: rsdExceeded,
            ReviewReason: reviewReason,
            ComparisonStatus: status,
            CalculationJson: calculationJson,
            CalculationData: calcData);
    }
}
