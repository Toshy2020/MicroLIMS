using System.Globalization;
using System.Text.Json;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Helpers;

public record MeasurementCalculationResult(
    decimal Mean,
    decimal Min,
    decimal Max,
    decimal? Sd,
    decimal? Rsd,
    int N,
    MeasurementEvaluationBasis Basis,
    decimal ReportedValue,
    string ReportedDisplay,
    string ComparisonStatus,
    string CalculationJson);

public static class MeasurementCalculator
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static int GetDecimalPlaces(decimal value) =>
        (decimal.GetBits(value)[3] >> 16) & 0x7F;

    public static string FormatReportedDisplay(decimal reportedValue, IReadOnlyList<decimal> readings, Specification spec)
    {
        int decimals = readings.Select(GetDecimalPlaces).DefaultIfEmpty(0).Max();
        if (spec.Target.HasValue) decimals = Math.Max(decimals, GetDecimalPlaces(spec.Target.Value));
        if (spec.Tolerance.HasValue) decimals = Math.Max(decimals, GetDecimalPlaces(spec.Tolerance.Value));
        if (spec.LowerLimit.HasValue) decimals = Math.Max(decimals, GetDecimalPlaces(spec.LowerLimit.Value));
        if (spec.UpperLimit.HasValue) decimals = Math.Max(decimals, GetDecimalPlaces(spec.UpperLimit.Value));

        decimal rounded = Math.Round(reportedValue, decimals, MidpointRounding.AwayFromZero);
        string format = decimals > 0 ? "0." + new string('0', decimals) : "0";
        string roundedStr = rounded.ToString(format, CultureInfo.InvariantCulture);

        return string.IsNullOrWhiteSpace(spec.Unit)
            ? roundedStr
            : $"{roundedStr} {spec.Unit.Trim()}";
    }

    public static MeasurementCalculationResult Calculate(
        IReadOnlyList<decimal> readings,
        MeasurementEvaluationBasis basis,
        Specification spec)
    {
        ArgumentNullException.ThrowIfNull(readings);
        ArgumentNullException.ThrowIfNull(spec);

        if (readings.Count == 0)
            throw new ArgumentException("At least one reading is required.", nameof(readings));

        int n = readings.Count;
        decimal sum = readings.Sum();
        decimal mean = sum / n;
        decimal min = readings.Min();
        decimal max = readings.Max();

        decimal? sd = null;
        decimal? rsd = null;

        if (n >= 2)
        {
            decimal sumSquares = 0m;
            foreach (var r in readings)
            {
                var diff = r - mean;
                sumSquares += diff * diff;
            }
            decimal variance = sumSquares / (n - 1);
            sd = DecimalMath.Sqrt(variance, 10);

            if (sd.HasValue && mean != 0m)
            {
                rsd = Math.Round((sd.Value / mean) * 100m, 10, MidpointRounding.AwayFromZero);
            }
        }

        decimal reportedValue;
        string comparisonStatus;

        switch (basis)
        {
            case MeasurementEvaluationBasis.Mean:
                reportedValue = mean;
                comparisonStatus = SpecificationEvaluator.Evaluate(spec, mean);
                break;

            case MeasurementEvaluationBasis.Min:
                reportedValue = min;
                comparisonStatus = SpecificationEvaluator.Evaluate(spec, min);
                break;

            case MeasurementEvaluationBasis.Max:
                reportedValue = max;
                comparisonStatus = SpecificationEvaluator.Evaluate(spec, max);
                break;

            case MeasurementEvaluationBasis.EachValue:
                reportedValue = mean;
                comparisonStatus = "WithinLimits";
                foreach (var r in readings)
                {
                    var readingStatus = SpecificationEvaluator.Evaluate(spec, r);
                    if (readingStatus == "OutOfSpecification")
                    {
                        comparisonStatus = "OutOfSpecification";
                        break;
                    }
                    if (readingStatus == "RequiresReview" && comparisonStatus != "OutOfSpecification")
                    {
                        comparisonStatus = "RequiresReview";
                    }
                    else if (readingStatus != "WithinLimits" && comparisonStatus == "WithinLimits")
                    {
                        comparisonStatus = readingStatus;
                    }
                }
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(basis), $"Unsupported evaluation basis: {basis}");
        }

        string reportedDisplay = FormatReportedDisplay(reportedValue, readings, spec);

        var calcData = new MeasurementCalculationData(
            Mean: mean,
            Min: min,
            Max: max,
            Sd: sd,
            Rsd: rsd,
            Basis: basis.ToString(),
            N: n);

        var calculationJson = JsonSerializer.Serialize(calcData, JsonOptions);

        return new MeasurementCalculationResult(
            Mean: mean,
            Min: min,
            Max: max,
            Sd: sd,
            Rsd: rsd,
            N: n,
            Basis: basis,
            ReportedValue: reportedValue,
            ReportedDisplay: reportedDisplay,
            ComparisonStatus: comparisonStatus,
            CalculationJson: calculationJson);
    }
}
