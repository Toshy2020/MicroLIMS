using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Services.Oos;

internal sealed record OosOpeningResult(TestOrderOutcome Outcome, string Summary);

public sealed class OosOpeningRule(IMicroLimsDbContext db)
{
    internal static bool Opens(ResultStatus? status) =>
        status is ResultStatus.OutOfSpecification or ResultStatus.Detected;

    private static ResultStatus? QualitativeStatus(ResultConformance outcome, string? value, string? interpretation)
    {
        if (outcome == ResultConformance.DoesNotConform)
            return ResultStatus.Detected;

        // Staged recorders append the final status in parentheses to the interpreted result.
        return value is not null && value == interpretation &&
            (value.Equals(nameof(ResultStatus.OutOfSpecification), StringComparison.OrdinalIgnoreCase) ||
             value.EndsWith($" ({nameof(ResultStatus.OutOfSpecification)})", StringComparison.Ordinal))
            ? ResultStatus.OutOfSpecification
            : null;
    }

    internal async Task<Dictionary<int, OosOpeningResult>> ReadFailingOrdersAsync(
        IReadOnlyCollection<int> orderIds, CancellationToken ct = default)
    {
        if (orderIds.Count == 0) return new();

        var orders = await db.TestOrders.AsNoTracking().Where(o => orderIds.Contains(o.Id))
            .AsSplitQuery()
            .Select(o => new
            {
                o.Id,
                Locations = db.SampleLocations.Where(l => l.TestOrderId == o.Id)
                    .OrderBy(l => l.Id).Select(l => new SampleLocationDetailDto
                    {
                        Status = l.Status, ReportedResult = l.ReportedResult, Unit = l.Unit,
                        CFUResult = l.CFUResult, CalculatedResult = l.CalculatedResult
                    }).ToList(),
                Analyses = db.TestAnalyses.Where(a => a.TestOrderId == o.Id && a.IsActive)
                    .OrderBy(a => a.Id)
                    .Select(a => new AnalysisDetailDto
                    {
                        ParameterResults = a.ParameterResults.Where(p => p.IsActive).OrderBy(p => p.Id)
                            .Select(p => new ParameterResultDetailDto
                            {
                                ComparisonStatus = p.ComparisonStatus, ReportedDisplay = p.ReportedDisplay,
                                ReportedValue = p.ReportedValue, Unit = p.Unit
                            }).ToList()
                    }).ToList(),
                Readings = db.CountTestReadings.Where(r => r.TestOrderId == o.Id && r.IsActive)
                    .OrderByDescending(r => r.Id).Take(1)
                    .Select(r => new CountTestReadingDetailDto
                    {
                        Status = r.Status, ReportedResult = r.ReportedResult, CalculatedResult = r.CalculatedResult
                    }).ToList(),
                Biochemical = db.WorkflowStepResults
                    .Where(r => r.TestOrderId == o.Id && r.BiochemicalResultText != null && r.BiochemicalOrganismDetected != null)
                    .OrderByDescending(r => r.Id).Take(1)
                    .Select(r => new BiochemicalResultDetailDto { OrganismDetected = r.BiochemicalOrganismDetected }).ToList(),
                Observations = db.PathogenObservations.Where(p => p.TestOrderId == o.Id)
                    .OrderBy(p => p.Id)
                    .Select(p => new PathogenObservationDetailDto { Observation = p.Observation.ToString() }).ToList(),
                Results = db.Results.Where(r => r.TestOrderId == o.Id)
                    .OrderByDescending(r => r.Id).Take(1)
                    .Select(r => new ResultDetailDto { InterpretedValue = r.InterpretedValue, RawValue = r.RawValue }).ToList()
            }).ToListAsync(ct);

        var failing = new Dictionary<int, OosOpeningResult>();
        foreach (var order in orders)
        {
            var summary = new TestOrderSummaryDetailDto
            {
                Locations = order.Locations,
                Analysis = order.Analyses.Count == 0 ? null : new AnalysisDetailDto
                {
                    ParameterResults = order.Analyses.SelectMany(a => a.ParameterResults).ToList()
                },
                CountTestReadings = order.Readings,
                BiochemicalResults = order.Biochemical,
                PathogenObservations = order.Observations,
                Results = order.Results
            };
            var interpretation = order.Results.LastOrDefault()?.InterpretedValue;
            var failures = TestOrderConformance.Outcomes<(ResultStatus? Status, string? Value, string? Unit)>(
                    summary, (status, value, unit) => (Status: status, Value: value, Unit: unit),
                    (conformance, value) => (QualitativeStatus(conformance, value, interpretation), value, null))
                .Where(r => Opens(r.Status)).ToList();
            if (failures.Count == 0) continue;

            var text = string.Join("; ", failures.Select(r => FormatValue(r.Status, r.Value, r.Unit)));
            if (text.Length > 500)
                text = text[..(char.IsHighSurrogate(text[499]) ? 499 : 500)];
            failing.Add(order.Id, new OosOpeningResult(new TestOrderOutcome(true, failures[0].Status), text));
        }

        return failing;
    }

    private static string FormatValue(ResultStatus? status, string? value, string? unit)
    {
        var text = string.IsNullOrWhiteSpace(value) ? status.ToString() ?? string.Empty : value.Trim();
        var suffix = unit?.Trim();
        if (string.IsNullOrEmpty(suffix) || text.Equals(suffix, StringComparison.Ordinal) ||
            text.EndsWith(" " + suffix, StringComparison.Ordinal) ||
            (suffix == "%" && text.EndsWith('%')))
            return text;
        return $"{text} {suffix}";
    }
}
