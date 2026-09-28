using MicroLIMS.Domain.Enums;
using System.Text.RegularExpressions;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Helpers;

namespace MicroLIMS.Application.Services;

// Builds the Certificate of Analysis from a sample summary: which tests
// and locations it shows, whether each result conforms, and the overall
// conclusion. This used to run in the browser (coaAggregation.ts) with
// its own conformance rule, which disagreed with the server's; every
// verdict now comes from ResultConformanceRules, and only a result that
// conforms is certified - a missing result or one without configured
// limits reads "Cannot certify".
public static class CertificateOfAnalysisBuilder
{
    public const string SampleCompliesNoResultsText = "Cannot certify — no results are recorded.";

    public static CertificateOfAnalysisDto Build(SampleSummaryDto summary)
    {
        var sample = BuildScope(summary.TestOrders, noResultsText: SampleCompliesNoResultsText);

        // A rejected sample still gets a certificate; it names the rejecting
        // laboratories instead of restating the result verdict.
        if (summary.OverallStatus == "Rejected")
        {
            var rejectingLabs = summary.Sections.Where(s => s.Status == "Rejected").Select(s => s.SectionName).ToList();
            sample.Complies = false;
            sample.ConclusionText = $"Rejected — {(rejectingLabs.Count > 0 ? string.Join(", ", rejectingLabs) : Humanize(summary.Status))}";
        }

        return new CertificateOfAnalysisDto
        {
            Sample = sample,
            Sections = summary.Sections
                .Select(s => new CoaSectionScopeDto
                {
                    SectionId = s.SectionId,
                    SectionName = s.SectionName,
                    Scope = BuildScope(summary.TestOrders.Where(t => t.SectionId == s.SectionId).ToList(), noResultsText: SampleCompliesNoResultsText)
                })
                .ToList()
        };
    }

    public static CoaScopeDto BuildScope(IReadOnlyList<TestOrderSummaryDetailDto> testOrders, string noResultsText)
    {
        var matrix = BuildMatrix(testOrders);
        var simple = matrix is null ? BuildSimpleRows(testOrders) : null;

        return new CoaScopeDto
        {
            Matrix = matrix,
            Simple = simple,
            Complies = matrix?.OverallComplies ?? simple?.OverallComplies ?? false,
            ConclusionText = matrix is not null ? MatrixConclusionText(matrix)
                : simple is not null ? SimpleConclusionText(simple)
                : noResultsText,
            ResultDate = ResultDate(testOrders)
        };
    }

    // ---- Water / EM / After Cleaning: location x test grid ----

    public static CoaMatrixDto? BuildMatrix(IReadOnlyList<TestOrderSummaryDetailDto> testOrders)
    {
        // One column per TestCode even when an OOS retest pulls the same test
        // in twice: the one with a result that does not conform, else the
        // first.
        var byTestCode = new Dictionary<string, TestOrderSummaryDetailDto>();
        var testCodeOrder = new List<string>();
        foreach (var t in testOrders)
        {
            if (t.Locations.Count == 0 || t.IsSuperseded) continue;
            if (!byTestCode.TryGetValue(t.TestCode, out var existing))
            {
                byTestCode[t.TestCode] = t;
                testCodeOrder.Add(t.TestCode);
                continue;
            }
            if (HasNonConformingLocation(t) && !HasNonConformingLocation(existing))
                byTestCode[t.TestCode] = t;
        }

        // TAMC leads, right after Location; the rest keep their order.
        var tests = testCodeOrder.Select(c => byTestCode[c])
            .OrderBy(t => t.TestCode.ToUpperInvariant().StartsWith("TAMC") ? 0 : 1)
            .ToList();
        if (tests.Count == 0) return null;

        var columns = tests.Select(t => new CoaColumnDto
        {
            TestOrderId = t.TestOrderId,
            TestCode = t.TestCode,
            TestDisplayName = t.TestDisplayName,
            IsQuantitative = IsQuantitative(t),
            Unit = IsQuantitative(t) ? t.Locations.FirstOrDefault(l => !string.IsNullOrEmpty(l.Unit))?.Unit : null
        }).ToList();

        // Rows = distinct physical locations, first seen across tests.
        var keysInOrder = new List<string>();
        var namesByKey = new Dictionary<string, string>();
        foreach (var t in tests)
            foreach (var l in t.Locations)
                if (namesByKey.TryAdd(l.LocationKey, l.LocationName))
                    keysInOrder.Add(l.LocationKey);

        var conclusions = tests.ToDictionary(t => t.TestOrderId, t => new CoaTestConclusionDto
        {
            TestOrderId = t.TestOrderId,
            TestCode = t.TestCode,
            TestDisplayName = t.TestDisplayName
        });
        var overallComplies = true;

        var rows = keysInOrder.Select(key =>
        {
            var locationName = namesByKey[key];
            var cells = tests.Select((t, i) =>
            {
                var loc = t.Locations.FirstOrDefault(l => l.LocationKey == key);
                if (loc is null) return null;

                var outcome = ResultConformanceRules.FromStatus(loc.Status);
                var conform = outcome == ResultConformance.Conforms;
                if (!conform)
                {
                    overallComplies = false;
                    var conclusion = conclusions[t.TestOrderId];
                    (outcome switch
                    {
                        ResultConformance.LimitsNotConfigured => conclusion.UnconfiguredLocationNames,
                        ResultConformance.NoResult => conclusion.MissingResultLocationNames,
                        _ => conclusion.FailingLocationNames
                    }).Add(locationName);
                }

                if (columns[i].IsQuantitative)
                {
                    var value = loc.CFUResult ?? loc.CalculatedResult;
                    return new CoaCellDto
                    {
                        Kind = "quantitative",
                        Alert = loc.AlertLimit,
                        Action = loc.ActionLimit,
                        Spec = loc.SpecLimit,
                        // No trailing zeros: 12.0 prints as 12, as the page always showed it.
                        Result = value?.ToString("0.############################", System.Globalization.CultureInfo.InvariantCulture) ?? "—",
                        Conform = conform
                    };
                }
                return (CoaCellDto?)new CoaCellDto
                {
                    Kind = "qualitative",
                    Result = conform ? nameof(ResultStatus.Absent) : loc.ReportedResult ?? "—",
                    Conform = conform
                };
            }).ToList();
            return new CoaRowDto { LocationKey = key, LocationName = locationName, Cells = cells };
        }).ToList();

        foreach (var c in conclusions.Values)
            c.Conforms = c.FailingLocationNames.Count == 0 && c.UnconfiguredLocationNames.Count == 0 && c.MissingResultLocationNames.Count == 0;

        return new CoaMatrixDto
        {
            Columns = columns,
            Rows = rows,
            TestConclusions = tests.Select(t => conclusions[t.TestOrderId]).ToList(),
            OverallComplies = overallComplies,
            TotalTests = tests.Count,
            TotalLocations = keysInOrder.Count,
            Units = columns.Where(c => c.IsQuantitative && !string.IsNullOrEmpty(c.Unit)).Select(c => c.Unit!).Distinct().ToList()
        };
    }

    public static string MatrixConclusionText(CoaMatrixDto matrix)
    {
        if (matrix.OverallComplies)
        {
            return $"This sample complies with the specified requirements. All {matrix.TotalTests} test{(matrix.TotalTests == 1 ? "" : "s")} " +
                   $"conform across all {matrix.TotalLocations} sampling location{(matrix.TotalLocations == 1 ? "" : "s")}.";
        }

        static List<string> Named(IEnumerable<CoaTestConclusionDto> conclusions, Func<CoaTestConclusionDto, List<string>> locations) =>
            conclusions.Where(c => locations(c).Count > 0)
                .Select(c => $"{(string.IsNullOrEmpty(c.TestDisplayName) ? c.TestCode : c.TestDisplayName)} at {string.Join(", ", locations(c))}")
                .ToList();

        var fails = Named(matrix.TestConclusions, c => c.FailingLocationNames);
        var unconfigured = Named(matrix.TestConclusions, c => c.UnconfiguredLocationNames);
        var missing = Named(matrix.TestConclusions, c => c.MissingResultLocationNames);

        var cannotCertify = new List<string>();
        if (unconfigured.Count > 0) cannotCertify.Add($"limits are not configured for: {string.Join("; ", unconfigured)}");
        if (missing.Count > 0) cannotCertify.Add($"no result is recorded for: {string.Join("; ", missing)}");

        if (fails.Count > 0)
        {
            var text = $"This sample does not comply with the specified requirements. Exceptions: {string.Join("; ", fails)}.";
            return cannotCertify.Count > 0 ? $"{text} Additionally, cannot certify — {string.Join("; and ", cannotCertify)}." : text;
        }
        return $"Cannot certify — {string.Join("; and ", cannotCertify)}.";
    }

    // ---- Product / Raw Material / Packaging: one row per test ----

    public static CoaSimpleResultDto? BuildSimpleRows(IReadOnlyList<TestOrderSummaryDetailDto> testOrders)
    {
        // TAMC, then TYMC, then E.coli, then the rest in their order.
        var tests = testOrders
            .Where(t => t.Locations.Count == 0 && !t.IsSuperseded)
            .OrderBy(t => SimpleRowRank(t.TestCode))
            .ToList();
        if (tests.Count == 0) return null;

        var rows = tests.SelectMany(SimpleRowsFor).ToList();
        var overallComplies = rows.All(r => r.Conform);

        // One row per TestCode, as for the matrix: the first that does not
        // conform, else the first. overallComplies already counts every
        // duplicate, shown or not.
        var deduped = new List<CoaSimpleRowDto>();
        foreach (var row in rows)
        {
            var i = deduped.FindIndex(r => r.TestCode == row.TestCode);
            if (i == -1) deduped.Add(row);
            else if (!row.Conform && deduped[i].Conform) deduped[i] = row;
        }

        return new CoaSimpleResultDto { Rows = deduped, OverallComplies = overallComplies };
    }

    private static IEnumerable<CoaSimpleRowDto> SimpleRowsFor(TestOrderSummaryDetailDto t)
    {
        if (t.ElementalAssay is { } assay)
        {
            return assay.Elements.Select(e => Row(t, ResultConformanceRules.FromStatus(e.Status),
                testCode: $"{t.TestCode}:{e.Element}", displayName: e.ParameterName,
                specification: WithUnit(e.SpecLimit, e.Unit), result: e.ReportedDisplay,
                analystName: assay.EnteredByName, analystAt: assay.EnteredAt));
        }

        if (t.Analysis is { } analysis)
        {
            return analysis.ParameterResults.Select(p => Row(t, ResultConformanceRules.FromStatus(p.ComparisonStatus),
                testCode: string.IsNullOrEmpty(p.ParameterName) ? t.TestCode : $"{t.TestCode}:{p.ParameterName}",
                displayName: string.IsNullOrEmpty(p.ParameterName) ? t.TestDisplayName : p.ParameterName,
                specification: WithUnit(p.SpecLimit, p.Unit), result: p.ReportedDisplay,
                analystName: analysis.EnteredByName, analystAt: analysis.EnteredAt));
        }

        if (IsQuantitative(t))
        {
            // A single-value count reading (TAMC/TYMC with no location split).
            var reading = t.CountTestReadings.LastOrDefault();
            return new[]
            {
                Row(t, ResultConformanceRules.FromStatus(reading?.Status), t.TestCode, t.TestDisplayName, t.SpecificationText,
                    reading?.ReportedResult ?? "—", reading?.EnteredByName, reading?.EnteredAt)
            };
        }

        var lastResult = t.Results.LastOrDefault();
        var lastObservation = t.PathogenObservations.LastOrDefault();
        var (outcome, result) = TestOrderConformance.Qualitative(t);

        return new[]
        {
            Row(t, outcome, t.TestCode, t.TestDisplayName, t.SpecificationText, result ?? "—",
                lastResult?.EnteredByName ?? lastObservation?.ObservedByName,
                lastResult?.EnteredAt ?? lastObservation?.ObservedAt)
        };
    }

    private static CoaSimpleRowDto Row(TestOrderSummaryDetailDto t, ResultConformance outcome, string testCode, string displayName,
        string? specification, string result, string? analystName, DateTime? analystAt) => new()
    {
        TestOrderId = t.TestOrderId,
        TestCode = testCode,
        TestDisplayName = displayName,
        Specification = specification,
        Result = result,
        AnalystName = string.IsNullOrEmpty(analystName) ? null : analystName,
        AnalystAt = analystAt,
        Conform = outcome == ResultConformance.Conforms,
        LimitsNotConfigured = outcome == ResultConformance.LimitsNotConfigured,
        NoResult = outcome == ResultConformance.NoResult
    };

    public static string SimpleConclusionText(CoaSimpleResultDto simple)
    {
        if (simple.OverallComplies)
            return "Test results of the sample are Conform according to specification and decision rule.";

        var hasFails = simple.Rows.Any(r => !r.Conform && !r.LimitsNotConfigured && !r.NoResult);
        if (hasFails)
            return "Test results of the sample are Non-Conform according to specification and decision rule.";

        var unconfigured = simple.Rows.Any(r => r.LimitsNotConfigured);
        var missing = simple.Rows.Any(r => r.NoResult);
        return unconfigured && missing ? "Cannot certify — one or more results has no configured limit, and one or more tests has no recorded result."
            : missing ? "Cannot certify — one or more tests has no recorded result."
            : "Cannot certify — one or more results has no configured limit.";
    }

    // ---- Shared ----

    public static DateTime? ResultDate(IEnumerable<TestOrderSummaryDetailDto> testOrders)
    {
        DateTime? max = null;
        void Consider(DateTime? ts)
        {
            if (ts is { } v && (max is null || v > max)) max = v;
        }
        foreach (var t in testOrders.Where(t => !t.IsSuperseded))
        {
            t.Results.ForEach(r => Consider(r.EnteredAt));
            t.CountTestReadings.ForEach(r => Consider(r.EnteredAt));
            t.Locations.ForEach(l => Consider(l.EnteredAt));
            t.PathogenObservations.ForEach(p => Consider(p.ObservedAt));
            t.BiochemicalResults.ForEach(b => Consider(b.SubmittedAt));
        }
        return max;
    }

    private static bool IsQuantitative(TestOrderSummaryDetailDto t) =>
        t.CountTestReadings.Count > 0 || t.Locations.Any(l => l.CFUResult is not null);

    private static bool HasNonConformingLocation(TestOrderSummaryDetailDto t) =>
        t.Locations.Any(l => ResultConformanceRules.FromStatus(l.Status) != ResultConformance.Conforms);

    private static int SimpleRowRank(string testCode)
    {
        var code = testCode.ToUpperInvariant();
        if (code.StartsWith("TAMC")) return 0;
        if (code.StartsWith("TYMC")) return 1;
        if (code.StartsWith("E.COLI") || code.StartsWith("ECOLI")) return 2;
        return 3;
    }

    private static string? WithUnit(string? value, string? unit) =>
        string.IsNullOrEmpty(value) ? null : string.IsNullOrEmpty(unit) ? value : $"{value} {unit}";

    // "RetestRequested" -> "Retest requested", as the page's humanize().
    private static string Humanize(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "—";
        var spaced = Regex.Replace(value, "([a-z0-9])([A-Z])", "$1 $2");
        spaced = Regex.Replace(spaced, "([A-Z]+)([A-Z][a-z])", "$1 $2");
        return Regex.Replace(spaced, @"\s+(.)", m => " " + m.Groups[1].Value.ToLowerInvariant());
    }
}
