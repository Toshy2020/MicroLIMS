using System.Globalization;

namespace MicroLIMS.Application.Helpers;

public record SstCriteria(
    decimal? MaxRsdPercent,
    decimal? MinResolution,
    decimal? MaxTailingFactor,
    decimal? MinTheoreticalPlates,
    decimal? MinRetentionFactor,
    decimal? MinSignalToNoise,
    decimal? MinPeakToValley,
    int StandardInjections);

public record SstEntered(
    IReadOnlyList<decimal> Responses,
    decimal? ReportedRsdPercent,
    decimal? Resolution,
    decimal? TailingFactor,
    decimal? TheoreticalPlates,
    decimal? RetentionFactor,
    decimal? SignalToNoise,
    decimal? PeakToValley);

public record SstOutcome(decimal MeanResponse, decimal? ComputedRsdPercent, bool Passed, IReadOnlyList<string> FailureReasons);

// System Suitability Testing gate for an HPLC SST analyte row. Pure function, no I/O.
public static class HplcSstEvaluator
{
    public static SstOutcome Evaluate(string analyteName, SstCriteria criteria, SstEntered entered)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        ArgumentNullException.ThrowIfNull(entered);

        var responses = entered.Responses ?? Array.Empty<decimal>();
        var failureReasons = new List<string>();

        if (responses.Count != criteria.StandardInjections)
        {
            failureReasons.Add($"{analyteName}: {criteria.StandardInjections} injections required, {responses.Count} entered.");
        }

        for (int i = 0; i < responses.Count; i++)
        {
            if (responses[i] <= 0m)
            {
                failureReasons.Add($"{analyteName}: injection {i + 1} response must be greater than zero.");
            }
        }

        decimal meanResponse = responses.Count > 0 ? responses.Average() : 0m;

        // Same RSD approach as StandardComparisonCalculator.CalculatePreparationRsd (sample SD, n-1;
        // null when n < 2). maxPreparationRsdPercent is passed null - we only want the computed value here;
        // the RSD gate below is applied separately so its failure message can be analyte-scoped.
        var (computedRsdPercent, _, _) = StandardComparisonCalculator.CalculatePreparationRsd(responses, null);

        if (criteria.MaxRsdPercent.HasValue && computedRsdPercent.HasValue && computedRsdPercent.Value > criteria.MaxRsdPercent.Value)
        {
            failureReasons.Add($"{analyteName}: RSD {computedRsdPercent.Value:0.00}% exceeds {criteria.MaxRsdPercent.Value:0.00}%.");
        }

        CheckMin(analyteName, "resolution", criteria.MinResolution, entered.Resolution, failureReasons);
        CheckMax(analyteName, "tailing factor", criteria.MaxTailingFactor, entered.TailingFactor, failureReasons);
        CheckMin(analyteName, "theoretical plates", criteria.MinTheoreticalPlates, entered.TheoreticalPlates, failureReasons);
        CheckMin(analyteName, "retention factor", criteria.MinRetentionFactor, entered.RetentionFactor, failureReasons);
        CheckMin(analyteName, "signal to noise", criteria.MinSignalToNoise, entered.SignalToNoise, failureReasons);
        CheckMin(analyteName, "peak to valley", criteria.MinPeakToValley, entered.PeakToValley, failureReasons);

        return new SstOutcome(meanResponse, computedRsdPercent, failureReasons.Count == 0, failureReasons);
    }

    private static void CheckMin(string analyteName, string label, decimal? limit, decimal? entered, List<string> failureReasons)
    {
        if (!limit.HasValue)
            return;

        if (!entered.HasValue)
        {
            failureReasons.Add($"{analyteName}: {label} is required.");
            return;
        }

        if (entered.Value < limit.Value)
        {
            failureReasons.Add($"{analyteName}: {label} {Format(entered.Value)} is below {Format(limit.Value)}.");
        }
    }

    private static void CheckMax(string analyteName, string label, decimal? limit, decimal? entered, List<string> failureReasons)
    {
        if (!limit.HasValue)
            return;

        if (!entered.HasValue)
        {
            failureReasons.Add($"{analyteName}: {label} is required.");
            return;
        }

        if (entered.Value > limit.Value)
        {
            failureReasons.Add($"{analyteName}: {label} {Format(entered.Value)} is above {Format(limit.Value)}.");
        }
    }

    private static string Format(decimal value) => value.ToString("0.0###", CultureInfo.InvariantCulture);
}
