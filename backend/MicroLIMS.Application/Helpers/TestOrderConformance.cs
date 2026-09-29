using MicroLIMS.Application.DTOs;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Helpers;

// How a whole test order stands, from the results it recorded - the one
// place that decides which results count for which kind of test. The
// Certificate of Analysis and the report archived at approval both read
// it, so they cannot disagree about the same test.
public static class TestOrderConformance
{
    // One verdict per recorded result: each location, each element of an
    // elemental assay, each measured parameter, the count reading, or the
    // single qualitative call.
    public static IEnumerable<ResultConformance> Outcomes(TestOrderSummaryDetailDto t)
    {
        if (t.Locations.Count > 0)
            return t.Locations.Select(l => ResultConformanceRules.FromStatus(l.Status));
        if (t.ElementalAssay is { } assay)
            return assay.Elements.Select(e => ResultConformanceRules.FromStatus(e.Status));
        if (t.Analysis is { } analysis)
            return analysis.ParameterResults.Select(p => ResultConformanceRules.FromStatus(p.ComparisonStatus));
        if (t.CountTestReadings.Count > 0)
            return new[] { ResultConformanceRules.FromStatus(t.CountTestReadings[^1].Status) };
        return new[] { Qualitative(t).Outcome };
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
