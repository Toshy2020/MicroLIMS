using MicroLIMS.Application.Services.Spc;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class ControlChartCalculatorTests
{
    private static readonly ControlChartSettings AllRules = new(Rules: new[] { 1, 2, 3, 4, 5, 6, 7, 8 });

    // ---- Limits -------------------------------------------------------

    [Fact]
    public void MovingRangeMethod_UsesAverageMovingRangeOverD2()
    {
        var r = ControlChartCalculator.Compute(new double[] { 1, 3, 2, 4, 3 }, new ControlChartSettings());

        Assert.Equal(2.6, r.Mean!.Value, 6);
        Assert.Equal(1.5, r.MovingRangeMean!.Value, 6);           // |3-1|,|2-3|,|4-2|,|3-4|
        Assert.Equal(1.5 / 1.128, r.SigmaWithin!.Value, 6);
        Assert.Equal(Math.Round(2.6 + 2 * 1.5 / 1.128, 4), r.Limits.AlertUpper);
        Assert.Equal(Math.Round(2.6 + 3 * 1.5 / 1.128, 4), r.Limits.ActionUpper);
        Assert.Equal(0, r.Limits.ActionLower); // 2.6 - 3σ = -1.39, floored for non-negative results
        Assert.Equal(3.267 * 1.5, r.MovingRangeUcl!.Value, 6);
        Assert.Equal(0, r.MovingRangeLcl!.Value, 6);
    }

    [Fact]
    public void MovingRangeSpanThree_UsesRangeOfThreeAndItsD2()
    {
        var r = ControlChartCalculator.Compute(new double[] { 1, 3, 2, 4, 3 },
            new ControlChartSettings(MovingRangeLength: 3));

        Assert.Equal(new double?[] { null, null, 2, 2, 2 }, r.MovingRanges);
        Assert.Equal(2 / 1.693, r.SigmaWithin!.Value, 6);
    }

    [Fact]
    public void StandardDeviationMethod_UsesSampleStandardDeviation()
    {
        var r = ControlChartCalculator.Compute(new double[] { 1, 3, 2, 4, 3 },
            new ControlChartSettings(Method: LimitMethod.StandardDeviation));

        Assert.Equal(Math.Sqrt(1.3), r.StandardDeviation!.Value, 6);
        Assert.Equal(r.StandardDeviation, r.SigmaUsed);
        Assert.Equal(Math.Round(2.6 + 3 * Math.Sqrt(1.3), 4), r.Limits.ActionUpper);
    }

    [Fact]
    public void PercentileMethod_InterpolatesLikePercentileInc_AndHasNoLowerLimits()
    {
        var values = Enumerable.Range(1, 10).Select(v => (double)v).ToList();
        var r = ControlChartCalculator.Compute(values, new ControlChartSettings(Method: LimitMethod.Percentile));

        Assert.Equal(9.55, r.Limits.AlertUpper);
        Assert.Equal(9.91, r.Limits.ActionUpper);
        Assert.Null(r.Limits.AlertLower);
        Assert.Null(r.Limits.ActionLower);
    }

    [Fact]
    public void RoundingUpToWholeNumber_CeilsUpperAndFloorsLowerLimits()
    {
        var r = ControlChartCalculator.Compute(new double[] { 1, 3, 2, 4, 3 },
            new ControlChartSettings(Rounding: LimitRounding.UpToWholeNumber));

        Assert.Equal(6, r.Limits.AlertUpper);  // 5.26
        Assert.Equal(7, r.Limits.ActionUpper); // 6.59
        Assert.Equal(0, r.Limits.ActionLower); // -1.39, floored: the results are all non-negative
    }

    [Fact]
    public void CalculationWindow_EstimatesLimitsFromItsPointsOnly()
    {
        var values = new double[] { 1, 1, 1, 1, 50 };
        var inCalc = new[] { true, true, true, true, false };

        var r = ControlChartCalculator.Compute(values, AllRules, inCalculation: inCalc);

        Assert.Equal(4, r.Count);
        Assert.Equal(1, r.Mean);
        Assert.Equal(5, r.MovingRanges.Count); // the whole series is still charted
    }

    [Fact]
    public void BelowMinimumPoints_IsReported()
    {
        var r = ControlChartCalculator.Compute(new double[] { 1, 2, 3 }, new ControlChartSettings(MinimumPoints: 30));
        Assert.True(r.BelowMinimumPoints);
    }

    [Fact]
    public void EmptySeries_ReturnsNoLimits()
    {
        var r = ControlChartCalculator.Compute(Array.Empty<double>(), new ControlChartSettings());
        Assert.Equal(0, r.Count);
        Assert.Null(r.Limits.ActionUpper);
        Assert.Empty(r.Histogram);
    }

    [Theory]
    [InlineData(1, 10)]
    [InlineData(3, 2)]
    public void InvalidSettings_AreRefused(int movingRangeLength, double actionK)
    {
        Assert.Throws<InvalidOperationException>(() => ControlChartCalculator.Compute(
            new double[] { 1, 2 }, new ControlChartSettings(AlertK: 2.5, ActionK: actionK, MovingRangeLength: movingRangeLength)));
    }

    // ---- Rules --------------------------------------------------------

    [Fact]
    public void Rule1_FlagsPointsBeyondActionLimit()
    {
        var flags = ControlChartCalculator.EvaluateRules(new double[] { 1, 2, 9, 1 }, 2, 1, actionUpper: 5, actionLower: null, new[] { 1 });
        Assert.Equal(new[] { 1 }, flags[2]);
        Assert.Empty(flags[0]);
    }

    [Fact]
    public void Rule2_FlagsNinthPointOnOneSideOfMean()
    {
        var v = Enumerable.Repeat(0.0, 9).Concat(Enumerable.Repeat(10.0, 9)).ToList();
        var flags = ControlChartCalculator.EvaluateRules(v, 5, 1, null, null, new[] { 2 });

        Assert.Empty(flags[7]);
        Assert.Contains(2, flags[8]);
        Assert.Contains(2, flags[17]);
        Assert.Empty(flags[9]);
    }

    [Fact]
    public void Rule3_FlagsSixthPointOfASteadyRise()
    {
        var flags = ControlChartCalculator.EvaluateRules(new double[] { 1, 2, 3, 4, 5, 6 }, 3.5, 1, null, null, new[] { 3 });
        Assert.Empty(flags[4]);
        Assert.Contains(3, flags[5]);
    }

    [Fact]
    public void Rule4_FlagsFourteenthAlternatingPoint()
    {
        var v = Enumerable.Range(0, 14).Select(i => i % 2 == 0 ? 0.0 : 1.0).ToList();
        var flags = ControlChartCalculator.EvaluateRules(v, 0.5, 1, null, null, new[] { 4 });
        Assert.Empty(flags[12]);
        Assert.Contains(4, flags[13]);
    }

    [Fact]
    public void Rule5_FlagsTwoOfThreeBeyondTwoSigma()
    {
        var flags = ControlChartCalculator.EvaluateRules(new double[] { 0, 2.5, 0, 2.5 }, 0, 1, null, null, new[] { 5 });
        Assert.Empty(flags[1]);
        Assert.Contains(5, flags[3]);
    }

    [Fact]
    public void Rule6_FlagsFourOfFiveBeyondOneSigma()
    {
        var flags = ControlChartCalculator.EvaluateRules(new double[] { 1.5, 1.5, 0, 1.5, 1.5 }, 0, 1, null, null, new[] { 6 });
        Assert.Contains(6, flags[4]);
        Assert.Empty(flags[3]);
    }

    [Fact]
    public void Rule7_FlagsFifteenPointsHuggingTheMean()
    {
        var v = Enumerable.Repeat(0.1, 15).ToList();
        var flags = ControlChartCalculator.EvaluateRules(v, 0, 1, null, null, new[] { 7 });
        Assert.Empty(flags[13]);
        Assert.Contains(7, flags[14]);
    }

    [Fact]
    public void Rule8_FlagsEightPointsAwayFromTheMeanOnEitherSide()
    {
        var v = new double[] { 2, -2, 2, -2, 2, -2, 2, -2 };
        var flags = ControlChartCalculator.EvaluateRules(v, 0, 1, null, null, new[] { 8 });
        Assert.Empty(flags[6]);
        Assert.Contains(8, flags[7]);
    }

    [Fact]
    public void DisabledRules_FlagNothing()
    {
        var flags = ControlChartCalculator.EvaluateRules(new double[] { 1, 2, 3, 4, 5, 6 }, 3.5, 1, 2, null, Array.Empty<int>());
        Assert.All(flags, Assert.Empty);
    }

    // ---- Capability ---------------------------------------------------

    [Fact]
    public void Capability_UpperLimitOnly_CpkEqualsCpuAndCpIsUndefined()
    {
        var r = ControlChartCalculator.Compute(new double[] { 1, 3, 2, 4, 3 }, new ControlChartSettings(), new CapabilitySpec(null, 10));
        var sigma = 1.5 / 1.128;

        Assert.Null(r.Capability.Cp);
        Assert.Null(r.Capability.Pp);
        Assert.Equal((10 - 2.6) / (3 * sigma), r.Capability.Cpu!.Value, 6);
        Assert.Equal(r.Capability.Cpu, r.Capability.Cpk);
        Assert.Equal((10 - 2.6) / (3 * Math.Sqrt(1.3)), r.Capability.Ppk!.Value, 6);
        Assert.Equal(0, r.Capability.ObservedPercentOutside);
    }

    [Fact]
    public void Capability_TwoSidedLimits_ComputesCpAndTakesTheWorseSide()
    {
        var r = ControlChartCalculator.Compute(new double[] { 1, 3, 2, 4, 3 }, new ControlChartSettings(), new CapabilitySpec(0, 10));
        var sigma = 1.5 / 1.128;

        Assert.Equal(10 / (6 * sigma), r.Capability.Cp!.Value, 6);
        Assert.Equal(2.6 / (3 * sigma), r.Capability.Cpl!.Value, 6);
        Assert.Equal(r.Capability.Cpl, r.Capability.Cpk);
    }

    [Fact]
    public void Capability_WithoutLimits_IsEmpty()
    {
        var r = ControlChartCalculator.Compute(new double[] { 1, 2, 3 }, new ControlChartSettings());
        Assert.Null(r.Capability.Cpk);
        Assert.Null(r.Capability.ObservedPercentOutside);
    }

    [Fact]
    public void NormalCdf_MatchesTables()
    {
        Assert.Equal(0.5, ControlChartCalculator.NormalCdf(0), 6);
        Assert.Equal(0.975, ControlChartCalculator.NormalCdf(1.959964), 5);
        Assert.Equal(0.00135, ControlChartCalculator.NormalCdf(-3), 5);
    }

    // ---- Histogram / box ---------------------------------------------

    [Fact]
    public void Histogram_IntegerCounts_GetWholeNumberBinsCoveringEveryValue()
    {
        var values = new double[] { 0, 0, 1, 1, 1, 2, 3, 5, 8, 13 };
        var r = ControlChartCalculator.Compute(values, new ControlChartSettings());

        Assert.Equal(values.Length, r.Histogram.Sum(b => b.Count));
        Assert.All(r.Histogram, b => Assert.Equal(Math.Round(b.To - b.From), b.To - b.From, 9));
        Assert.Equal(-0.5, r.Histogram[0].From);
    }

    [Fact]
    public void Histogram_IdenticalValues_IsASingleBin()
    {
        var r = ControlChartCalculator.Histogram(new double[] { 2.5, 2.5, 2.5 }, 2.5, 0);
        Assert.Single(r);
        Assert.Equal(3, r[0].Count);
    }

    [Fact]
    public void Box_UsesQuartilesAndTukeyWhiskers()
    {
        var box = ControlChartCalculator.Box(new double[] { 1, 2, 3, 4, 5, 6, 7, 8, 100 })!;

        Assert.Equal(3, box.Q1);
        Assert.Equal(5, box.Median);
        Assert.Equal(7, box.Q3);
        Assert.Equal(8, box.UpperWhisker);
        Assert.Equal(new double[] { 100 }, box.Outliers);
    }
}
