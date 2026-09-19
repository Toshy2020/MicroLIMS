using MicroLIMS.Application.Helpers;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class MeasurementCalculatorTests
{
    private static Specification CreateTargetSpec(decimal target, decimal tolerance, string? unit = null) => new()
    {
        LimitType = LimitType.TargetWithTolerance,
        Target = target,
        Tolerance = tolerance,
        ToleranceMode = ToleranceMode.Absolute,
        Unit = unit ?? string.Empty
    };

    [Fact]
    public void WorkedExample_PhReadings_CalculatesMeanSdRsdAndDisplay()
    {
        // Spec: pH 6.02, 6.05, 5.98 -> mean 6.0166666667, min 5.98, max 6.05, SD 0.0351188458, RSD 0.5837 %
        // Spec Target 6.0 ± 0.5 (5.5-6.5) on Mean -> WithinLimits, display "6.02"
        var readings = new List<decimal> { 6.02m, 6.05m, 5.98m };
        var spec = CreateTargetSpec(6.0m, 0.5m);

        var result = MeasurementCalculator.Calculate(readings, MeasurementEvaluationBasis.Mean, spec);

        Assert.Equal(3, result.N);
        Assert.Equal(5.98m, result.Min);
        Assert.Equal(6.05m, result.Max);

        // Mean: 18.05 / 3 = 6.0166666666666666666666666667
        Assert.Equal(6.0166666667m, Math.Round(result.Mean, 10, MidpointRounding.AwayFromZero));

        // SD: sample SD (n-1) = 0.0351188458
        Assert.NotNull(result.Sd);
        Assert.Equal(0.0351188458m, result.Sd.Value);

        // RSD: ~0.5837 %
        Assert.NotNull(result.Rsd);
        Assert.Equal(0.5837m, Math.Round(result.Rsd.Value, 4, MidpointRounding.AwayFromZero));

        Assert.Equal(MeasurementEvaluationBasis.Mean, result.Basis);
        Assert.Equal(result.Mean, result.ReportedValue);
        Assert.Equal("WithinLimits", result.ComparisonStatus);
        Assert.Equal("6.02", result.ReportedDisplay);

        // CalculationJson check
        Assert.Contains("\"mean\":", result.CalculationJson);
        Assert.Contains("\"sd\":0.0351188458", result.CalculationJson);
        Assert.Contains("\"basis\":\"Mean\"", result.CalculationJson);
        Assert.Contains("\"n\":3", result.CalculationJson);
    }

    [Fact]
    public void N1_SingleReading_HasNullSdAndRsd()
    {
        var readings = new List<decimal> { 6.02m };
        var spec = CreateTargetSpec(6.0m, 0.5m);

        var result = MeasurementCalculator.Calculate(readings, MeasurementEvaluationBasis.Mean, spec);

        Assert.Equal(1, result.N);
        Assert.Equal(6.02m, result.Mean);
        Assert.Equal(6.02m, result.Min);
        Assert.Equal(6.02m, result.Max);
        Assert.Null(result.Sd);
        Assert.Null(result.Rsd);
        Assert.Equal("WithinLimits", result.ComparisonStatus);
        Assert.Equal("6.02", result.ReportedDisplay);
    }

    [Fact]
    public void BasisMean_EvaluatesMeanValue()
    {
        // Target 6.0 ± 0.5 (5.5 - 6.5)
        // Readings: 5.4, 6.0, 6.0 -> mean 5.8 (within limits), min 5.4 (OOS)
        var readings = new List<decimal> { 5.4m, 6.0m, 6.0m };
        var spec = CreateTargetSpec(6.0m, 0.5m);

        var result = MeasurementCalculator.Calculate(readings, MeasurementEvaluationBasis.Mean, spec);

        Assert.Equal(5.8m, result.Mean);
        Assert.Equal(5.8m, result.ReportedValue);
        Assert.Equal("WithinLimits", result.ComparisonStatus);
    }

    [Fact]
    public void BasisMin_EvaluatesMinValue()
    {
        // Readings: 5.4, 6.0, 6.0 -> min 5.4 is outside 5.5-6.5
        var readings = new List<decimal> { 5.4m, 6.0m, 6.0m };
        var spec = CreateTargetSpec(6.0m, 0.5m);

        var result = MeasurementCalculator.Calculate(readings, MeasurementEvaluationBasis.Min, spec);

        Assert.Equal(5.4m, result.ReportedValue);
        Assert.Equal("OutOfSpecification", result.ComparisonStatus);
    }

    [Fact]
    public void BasisMax_EvaluatesMaxValue()
    {
        // Target 6.0 ± 0.5 (5.5 - 6.5)
        // Readings: 5.8, 6.0, 6.6 -> max 6.6 is outside 5.5-6.5
        var readings = new List<decimal> { 5.8m, 6.0m, 6.6m };
        var spec = CreateTargetSpec(6.0m, 0.5m);

        var result = MeasurementCalculator.Calculate(readings, MeasurementEvaluationBasis.Max, spec);

        Assert.Equal(6.6m, result.ReportedValue);
        Assert.Equal("OutOfSpecification", result.ComparisonStatus);
    }

    [Fact]
    public void BasisEachValue_OneReadingFails_WorstStatusWins_ReportedValueIsMean()
    {
        // Readings: 5.4, 6.0, 6.0 -> min 5.4 is OOS, mean is 5.8 (WithinLimits)
        var readings = new List<decimal> { 5.4m, 6.0m, 6.0m };
        var spec = CreateTargetSpec(6.0m, 0.5m);

        var result = MeasurementCalculator.Calculate(readings, MeasurementEvaluationBasis.EachValue, spec);

        Assert.Equal(5.8m, result.ReportedValue); // ReportedValue is mean!
        Assert.Equal("OutOfSpecification", result.ComparisonStatus); // Worst status wins!
    }

    [Fact]
    public void BasisEachValue_AllReadingsPass_WithinLimits()
    {
        var readings = new List<decimal> { 5.8m, 6.0m, 6.2m };
        var spec = CreateTargetSpec(6.0m, 0.5m);

        var result = MeasurementCalculator.Calculate(readings, MeasurementEvaluationBasis.EachValue, spec);

        Assert.Equal(6.0m, result.ReportedValue);
        Assert.Equal("WithinLimits", result.ComparisonStatus);
    }

    [Fact]
    public void Boundary_Target6Point0Tolerance0Point5_EvaluatedUnrounded()
    {
        var spec = CreateTargetSpec(6.0m, 0.5m);

        // 6.5 exactly is WithinLimits
        var res1 = MeasurementCalculator.Calculate(new List<decimal> { 6.5m }, MeasurementEvaluationBasis.Mean, spec);
        Assert.Equal("WithinLimits", res1.ComparisonStatus);

        // 6.5000001 is OutOfSpecification (unrounded comparison)
        var res2 = MeasurementCalculator.Calculate(new List<decimal> { 6.5000001m }, MeasurementEvaluationBasis.Mean, spec);
        Assert.Equal("OutOfSpecification", res2.ComparisonStatus);

        // 5.5 exactly is WithinLimits
        var res3 = MeasurementCalculator.Calculate(new List<decimal> { 5.5m }, MeasurementEvaluationBasis.Mean, spec);
        Assert.Equal("WithinLimits", res3.ComparisonStatus);

        // 5.4999999 is OutOfSpecification
        var res4 = MeasurementCalculator.Calculate(new List<decimal> { 5.4999999m }, MeasurementEvaluationBasis.Mean, spec);
        Assert.Equal("OutOfSpecification", res4.ComparisonStatus);
    }

    [Fact]
    public void FormatReportedDisplay_IncludesUnitWhenConfigured()
    {
        var spec = CreateTargetSpec(1.05m, 0.05m, "g/mL");
        var readings = new List<decimal> { 1.04m, 1.06m };

        var result = MeasurementCalculator.Calculate(readings, MeasurementEvaluationBasis.Mean, spec);

        Assert.Equal("1.05 g/mL", result.ReportedDisplay);
    }

    [Fact]
    public void DecimalMathSqrt_BasicCasesAndErrors()
    {
        Assert.Equal(0m, DecimalMath.Sqrt(0m));
        Assert.Equal(2m, DecimalMath.Sqrt(4m));
        Assert.Equal(3m, DecimalMath.Sqrt(9m));
        Assert.Equal(0.5m, DecimalMath.Sqrt(0.25m));
        Assert.Throws<ArgumentOutOfRangeException>(() => DecimalMath.Sqrt(-1m));
    }
}
