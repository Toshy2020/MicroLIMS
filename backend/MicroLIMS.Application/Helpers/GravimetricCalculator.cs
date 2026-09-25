using System.Globalization;
using System.Text.Json;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Helpers;

public record GravimetricReplicateInput(decimal? Container, decimal Initial, decimal Final);

public record GravimetricCalculationResult(
    decimal Mean,
    int N,
    decimal ReportedValue,
    string ReportedDisplay,
    string ComparisonStatus,
    string CalculationJson,
    IReadOnlyList<GravimetricReplicateCalculationData> Replicates);

public static class GravimetricCalculator
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static int GetDecimalPlaces(decimal value) =>
        (decimal.GetBits(value)[3] >> 16) & 0x7F;

    public static string FormatReportedDisplay(decimal reportedValue, Specification spec)
    {
        int decimals = 2;
        if (spec.Target.HasValue) decimals = Math.Max(decimals, GetDecimalPlaces(spec.Target.Value));
        if (spec.Tolerance.HasValue) decimals = Math.Max(decimals, GetDecimalPlaces(spec.Tolerance.Value));
        if (spec.LowerLimit.HasValue) decimals = Math.Max(decimals, GetDecimalPlaces(spec.LowerLimit.Value));
        if (spec.UpperLimit.HasValue) decimals = Math.Max(decimals, GetDecimalPlaces(spec.UpperLimit.Value));

        decimal rounded = Math.Round(reportedValue, decimals, MidpointRounding.AwayFromZero);
        string format = "0." + new string('0', decimals);
        string roundedStr = rounded.ToString(format, CultureInfo.InvariantCulture);

        string unit = string.IsNullOrWhiteSpace(spec.Unit) ? "%" : spec.Unit.Trim();
        return $"{roundedStr} {unit}";
    }

    public static GravimetricCalculationResult Calculate(
        IReadOnlyList<GravimetricReplicateInput> replicates,
        EquationType equationType,
        Specification spec)
    {
        ArgumentNullException.ThrowIfNull(replicates);
        ArgumentNullException.ThrowIfNull(spec);

        if (replicates.Count == 0)
            throw new ArgumentException("At least one replicate is required.", nameof(replicates));

        if (equationType is not (EquationType.GravimetricLoss or EquationType.GravimetricResidue))
            throw new ArgumentException($"Unsupported equation type for gravimetric calculation: {equationType}", nameof(equationType));

        var details = new List<GravimetricReplicateCalculationData>(replicates.Count);
        decimal sumPercent = 0m;

        foreach (var rep in replicates)
        {
            decimal container = rep.Container ?? 0m;
            decimal w1 = rep.Initial - container;
            decimal w2 = rep.Final - container;

            if (w1 <= 0m)
                throw new InvalidOperationException("Initial sample weight (W1) must be greater than zero.");
            if (w2 < 0m)
                throw new InvalidOperationException("Final weight (W2) cannot be negative.");
            if (w2 > w1)
                throw new InvalidOperationException("Final weight (W2) cannot be greater than initial weight (W1).");

            decimal percent = equationType == EquationType.GravimetricLoss
                ? ((w1 - w2) * 100m) / w1
                : (w2 * 100m) / w1;

            details.Add(new GravimetricReplicateCalculationData(
                rep.Container,
                rep.Initial,
                rep.Final,
                w1,
                w2,
                percent));

            sumPercent += percent;
        }

        int n = replicates.Count;
        decimal mean = sumPercent / n;
        decimal reportedValue = mean;

        string comparisonStatus = SpecificationEvaluator.Evaluate(spec, reportedValue);
        string reportedDisplay = FormatReportedDisplay(reportedValue, spec);

        var calcData = new GravimetricCalculationData(
            Mode: equationType.ToString(),
            Replicates: details,
            Mean: mean,
            N: n);

        string calculationJson = JsonSerializer.Serialize(calcData, JsonOptions);

        return new GravimetricCalculationResult(
            Mean: mean,
            N: n,
            ReportedValue: reportedValue,
            ReportedDisplay: reportedDisplay,
            ComparisonStatus: comparisonStatus,
            CalculationJson: calculationJson,
            Replicates: details);
    }
}
