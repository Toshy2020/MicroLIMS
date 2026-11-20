using MicroLIMS.Application.DTOs;
using System.Globalization;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Helpers;

// How a whole test order stands, from the results it recorded - the one
// place that decides which results count for which kind of test. The
// Certificate of Analysis and the report archived at approval both read
// it, so they cannot disagree about the same test.
public static class TestOrderConformance
{
    // A single location labels the analysis. A batch has a verdict at each location.
    internal static bool UsesLocationResults(int locationCount, bool hasParameterResults) =>
        locationCount > 0 && (locationCount != 1 || !hasParameterResults);

    // One verdict per recorded result: each location, each measured
    // parameter, the count reading, or the single qualitative call.
    public static IEnumerable<ResultConformance> Outcomes(TestOrderSummaryDetailDto t) =>
        Outcomes(t, (status, _, _) => ResultConformanceRules.FromStatus(status), (outcome, _) => outcome);

    internal static IEnumerable<T> Outcomes<T>(
        TestOrderSummaryDetailDto t, Func<ResultStatus?, string?, string?, T> fromStatus,
        Func<ResultConformance, string?, T> fromQualitative)
    {
        if (UsesLocationResults(t.Locations.Count, t.Analysis?.ParameterResults.Count > 0))
            return t.Locations.Select(l => fromStatus(l.Status,
                l.ReportedResult ?? (l.CFUResult ?? l.CalculatedResult)?.ToString(CultureInfo.InvariantCulture), l.Unit));
        if (t.Analysis is { } analysis)
            return analysis.ParameterResults.Select(p => fromStatus(p.ComparisonStatus,
                string.IsNullOrWhiteSpace(p.ReportedDisplay) ? p.ReportedValue?.ToString(CultureInfo.InvariantCulture) : p.ReportedDisplay,
                p.Unit));
        if (t.CountTestReadings.Count > 0)
        {
            var reading = t.CountTestReadings[^1];
            var value = string.IsNullOrWhiteSpace(reading.ReportedResult)
                ? reading.CalculatedResult?.ToString(CultureInfo.InvariantCulture)
                : reading.ReportedResult;
            return new[] { fromStatus(reading.Status, value, null) };
        }
        var (outcome, result) = Qualitative(t);
        return new[] { fromQualitative(outcome, result) };
    }

    // The test's overall standing: any failure fails it; otherwise it
    // cannot be certified while a result is missing or has no limits.
    public static ResultConformance Overall(TestOrderSummaryDetailDto t)
    {
        var outcomes = Outcomes(t).ToList();
        if (outcomes.Count == 0) return ResultConformance.NoResult;
        if (outcomes.Contains(ResultConformance.DoesNotConform)) return ResultConformance.DoesNotConform;
        if (outcomes.Contains(ResultConformance.LimitsNotConfigured)) return ResultConformance.LimitsNotConfigured;
        if (outcomes.Contains(ResultConformance.NoResult)) return ResultConformance.NoResult;
        return ResultConformance.Conforms;
    }

    // A qualitative call: the analyst's biochemical interpretation, else
    // the pathogen chain, else the entered result value. Only conforming
    // growth is the target organism; growth that is not the organism under
    // test is not a detection. Result is the text to print, null when
    // nothing was recorded.
    public static (ResultConformance Outcome, string? Result) Qualitative(TestOrderSummaryDetailDto t)
    {
        var lastBiochemical = t.BiochemicalResults.LastOrDefault(b => b.OrganismDetected is not null);
        if (lastBiochemical is not null)
        {
            var detected = lastBiochemical.OrganismDetected!.Value;
            return (ResultConformanceRules.FromDetection(detected), (detected ? ResultStatus.Detected : ResultStatus.Absent).ToString());
        }

        if (t.PathogenObservations.Count > 0)
        {
            var detected = t.PathogenObservations.Any(p => p.Observation == "GrowthConforming");
            return (ResultConformanceRules.FromDetection(detected), (detected ? ResultStatus.Detected : ResultStatus.Absent).ToString());
        }

        var lastResult = t.Results.LastOrDefault();
        var value = lastResult?.InterpretedValue ?? lastResult?.RawValue;
        return (ResultConformanceRules.FromResultValue(value), string.IsNullOrWhiteSpace(value) ? null : value);
    }
}
