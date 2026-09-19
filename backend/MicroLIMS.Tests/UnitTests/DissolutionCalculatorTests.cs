using MicroLIMS.Application.Helpers;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class DissolutionCalculatorTests
{
    // --- T6 Tests from Build Spec ---

    [Fact]
    public void UnitAtExactlyQPlus5_PassesS1()
    {
        decimal q = 80m;
        // Exactly Q + 5 = 85
        var units = new List<decimal> { 85m, 85m, 85m, 85m, 85m, 85m };
        var result = DissolutionStageEvaluator.Evaluate(units, q);

        Assert.Equal(1, result.StageReached);
        Assert.Equal(DissolutionStageOutcome.Complies, result.Outcome);
        Assert.Equal(85m, result.Mean);
        Assert.Empty(result.Reasons);
    }

    [Fact]
    public void S1Fail_S2Needed()
    {
        decimal q = 80m;
        // One unit below Q + 5 (84 < 85), but above Q - 15 (65)
        var units = new List<decimal> { 84m, 85m, 85m, 85m, 85m, 85m };
        var result = DissolutionStageEvaluator.Evaluate(units, q);

        Assert.Equal(1, result.StageReached);
        Assert.Equal(DissolutionStageOutcome.NextStageRequired, result.Outcome);
        Assert.NotEmpty(result.Reasons);
    }

    [Fact]
    public void S2MeanExactlyQ_Passes()
    {
        decimal q = 80m;
        // 12 units with mean exactly 80, none < Q - 15 (65)
        var units = new List<decimal>
        {
            75m, 85m, 75m, 85m, 75m, 85m,
            75m, 85m, 75m, 85m, 75m, 85m
        };
        var result = DissolutionStageEvaluator.Evaluate(units, q);

        Assert.Equal(2, result.StageReached);
        Assert.Equal(DissolutionStageOutcome.Complies, result.Outcome);
        Assert.Equal(80m, result.Mean);
        Assert.Empty(result.Reasons);
    }

    [Fact]
    public void S3_TwoUnitsBelowQMinus15_Passes()
    {
        decimal q = 80m;
        // 24 units, mean >= 80, exactly 2 units < 65 (at 64), none < 55
        var units = new List<decimal> { 64m, 64m };
        for (int i = 0; i < 22; i++)
        {
            units.Add(82m);
        }
        // sum = 2*64 + 22*82 = 128 + 1804 = 1932; mean = 1932/24 = 80.5 >= 80
        var result = DissolutionStageEvaluator.Evaluate(units, q);

        Assert.Equal(3, result.StageReached);
        Assert.Equal(DissolutionStageOutcome.Complies, result.Outcome);
        Assert.True(result.Mean >= q);
        Assert.Empty(result.Reasons);
    }

    [Fact]
    public void S3_ThreeUnitsBelowQMinus15_Fails()
    {
        decimal q = 80m;
        // 24 units, mean >= 80, but 3 units < 65 (at 64)
        var units = new List<decimal> { 64m, 64m, 64m };
        for (int i = 0; i < 21; i++)
        {
            units.Add(83m);
        }
        // sum = 3*64 + 21*83 = 192 + 1743 = 1935; mean = 1935/24 = 80.625 >= 80
        var result = DissolutionStageEvaluator.Evaluate(units, q);

        Assert.Equal(3, result.StageReached);
        Assert.Equal(DissolutionStageOutcome.DoesNotComply, result.Outcome);
        Assert.NotEmpty(result.Reasons);
    }

    [Fact]
    public void AnyUnitBelowQMinus25_Fails()
    {
        decimal q = 80m; // Q - 25 = 55
        // Test in Stage 3: 1 unit at 54 (< 55)
        var unitsS3 = new List<decimal> { 54m };
        for (int i = 0; i < 23; i++)
        {
            unitsS3.Add(85m);
        }
        var resultS3 = DissolutionStageEvaluator.Evaluate(unitsS3, q);
        Assert.Equal(3, resultS3.StageReached);
        Assert.Equal(DissolutionStageOutcome.DoesNotComply, resultS3.Outcome);

        // Test in Stage 1: 1 unit at 54
        var unitsS1 = new List<decimal> { 54m, 85m, 85m, 85m, 85m, 85m };
        var resultS1 = DissolutionStageEvaluator.Evaluate(unitsS1, q);
        Assert.Equal(1, resultS1.StageReached);
        Assert.Equal(DissolutionStageOutcome.DoesNotComply, resultS1.Outcome);

        // Test in Stage 2: 1 unit at 54
        var unitsS2 = new List<decimal> { 54m };
        for (int i = 0; i < 11; i++)
        {
            unitsS2.Add(85m);
        }
        var resultS2 = DissolutionStageEvaluator.Evaluate(unitsS2, q);
        Assert.Equal(2, resultS2.StageReached);
        Assert.Equal(DissolutionStageOutcome.DoesNotComply, resultS2.Outcome);
    }

    // --- T6 Worked Example ---

    [Fact]
    public void T6_WorkedExample_S1Complies()
    {
        // A_s 0.500, C_s 0.0200 mg/mL, V 900 mL, DF 1, LC 18 mg -> % = A_u * 200
        // Vessels A_u: 0.4500, 0.4400, 0.4600, 0.4450, 0.4550, 0.4350
        // Expected %: 90.0, 88.0, 92.0, 89.0, 91.0, 87.0 %; Q = 80 -> every unit >= 85 -> S1 Complies
        decimal asStd = 0.500m;
        decimal cs = 0.0200m;
        decimal v = 900m;
        decimal df = 1m;
        decimal lc = 18m;
        decimal q = 80m;

        var areas = new[] { 0.4500m, 0.4400m, 0.4600m, 0.4450m, 0.4550m, 0.4350m };
        var percents = new List<decimal>();
        foreach (var area in areas)
        {
            var pct = DissolutionCalculator.CalculateVesselPercent(area, asStd, cs, v, df, lc);
            percents.Add(pct);
        }

        Assert.Equal(90.0m, percents[0]);
        Assert.Equal(88.0m, percents[1]);
        Assert.Equal(92.0m, percents[2]);
        Assert.Equal(89.0m, percents[3]);
        Assert.Equal(91.0m, percents[4]);
        Assert.Equal(87.0m, percents[5]);

        var eval = DissolutionStageEvaluator.Evaluate(percents, q);
        Assert.Equal(1, eval.StageReached);
        Assert.Equal(DissolutionStageOutcome.Complies, eval.Outcome);
        Assert.Equal(89.5m, eval.Mean);
        Assert.Equal("90 %", DissolutionCalculator.FormatReportedDisplay(eval.Mean));
    }

    // --- Multi-Stage Paths ---

    [Fact]
    public void Stage2_Path_Complies()
    {
        decimal q = 80m;
        // Stage 1: 6 units, one unit below Q+5 (84 < 85), but >= 65
        var s1Units = new List<decimal> { 84m, 88m, 92m, 89m, 91m, 87m };
        var s1Result = DissolutionStageEvaluator.Evaluate(s1Units, q);
        Assert.Equal(1, s1Result.StageReached);
        Assert.Equal(DissolutionStageOutcome.NextStageRequired, s1Result.Outcome);

        // Stage 2: append 6 more units
        var s2Units = new List<decimal>(s1Units) { 86m, 88m, 90m, 85m, 87m, 89m };
        var s2Result = DissolutionStageEvaluator.Evaluate(s2Units, q);
        Assert.Equal(2, s2Result.StageReached);
        Assert.Equal(DissolutionStageOutcome.Complies, s2Result.Outcome);
        Assert.True(s2Result.Mean >= q);
        Assert.All(s2Units, u => Assert.True(u >= q - 15m));
    }

    [Fact]
    public void Stage3_Path_Complies()
    {
        decimal q = 80m;
        // Stage 1: 6 units at 70 -> below Q+5, so S2 needed
        var s1Units = new List<decimal> { 70m, 70m, 70m, 70m, 70m, 70m };
        var s1Result = DissolutionStageEvaluator.Evaluate(s1Units, q);
        Assert.Equal(1, s1Result.StageReached);
        Assert.Equal(DissolutionStageOutcome.NextStageRequired, s1Result.Outcome);

        // Stage 2: 6 more units at 70 -> total 12 units at 70; mean = 70 < 80, none < 65 -> S3 needed
        var s2Units = new List<decimal>(s1Units) { 70m, 70m, 70m, 70m, 70m, 70m };
        var s2Result = DissolutionStageEvaluator.Evaluate(s2Units, q);
        Assert.Equal(2, s2Result.StageReached);
        Assert.Equal(DissolutionStageOutcome.NextStageRequired, s2Result.Outcome);

        // Stage 3: append 12 units at 92
        var s3Units = new List<decimal>(s2Units);
        for (int i = 0; i < 12; i++) s3Units.Add(92m);
        // mean = (12*70 + 12*92)/24 = 81.0 >= 80, none < 65, none < 55
        var s3Result = DissolutionStageEvaluator.Evaluate(s3Units, q);
        Assert.Equal(3, s3Result.StageReached);
        Assert.Equal(DissolutionStageOutcome.Complies, s3Result.Outcome);
        Assert.Equal(81.0m, s3Result.Mean);
        Assert.Equal("81 %", DissolutionCalculator.FormatReportedDisplay(s3Result.Mean));
    }

    // --- Calculator Details & Formula Checks ---

    [Fact]
    public void CalculateCs_ComputesCorrectly()
    {
        // W_std = 50 mg, P = 100%, D_std = 2500 -> C_s = 50 * 1.0 / 2500 = 0.0200 mg/mL
        decimal cs = DissolutionCalculator.CalculateCs(50m, 100m, 2500m);
        Assert.Equal(0.02m, cs);
    }

    [Fact]
    public void CalculateCs_ZeroOrNegative_ThrowsInvalidOperationException()
    {
        Assert.Throws<InvalidOperationException>(() => DissolutionCalculator.CalculateCs(0m, 100m, 100m));
        Assert.Throws<InvalidOperationException>(() => DissolutionCalculator.CalculateCs(50m, 0m, 100m));
        Assert.Throws<InvalidOperationException>(() => DissolutionCalculator.CalculateCs(50m, 100m, 0m));
    }

    [Fact]
    public void FormatReportedDisplay_RoundsAwayFromZero()
    {
        Assert.Equal("85 %", DissolutionCalculator.FormatReportedDisplay(84.5m));
        Assert.Equal("84 %", DissolutionCalculator.FormatReportedDisplay(84.49m));
        Assert.Equal("90 %", DissolutionCalculator.FormatReportedDisplay(89.5m));
        Assert.Equal("90 %", DissolutionCalculator.FormatReportedDisplay(90.0m));
    }

    [Fact]
    public void DissolutionStageEvaluator_InvalidUnitCounts_ThrowsInvalidOperationException()
    {
        var five = new List<decimal> { 85m, 85m, 85m, 85m, 85m };
        Assert.Throws<InvalidOperationException>(() => DissolutionStageEvaluator.Evaluate(five, 80m));

        var seven = new List<decimal> { 85m, 85m, 85m, 85m, 85m, 85m, 85m };
        Assert.Throws<InvalidOperationException>(() => DissolutionStageEvaluator.Evaluate(seven, 80m));
    }

    [Fact]
    public void DissolutionCalculator_FullCalculate_ProducesValidResultAndJson()
    {
        var std = new DissolutionStandardData(
            SystemSuitabilityRunId: 10,
            RunCode: "SST-2026-001",
            StandardWeightMg: 50m,
            StandardDilution: 2500m,
            StandardPurityPercent: 100m,
            StandardMeanArea: 0.500m,
            Cs: 0.0200m);

        var offsets = new DissolutionOffsetsData(5m, 15m, 25m, 2m);
        var inputs = new List<(int Stage, int VesselIndex, decimal Area)>
        {
            (1, 1, 0.4500m),
            (1, 2, 0.4400m),
            (1, 3, 0.4600m),
            (1, 4, 0.4450m),
            (1, 5, 0.4550m),
            (1, 6, 0.4350m)
        };

        var result = DissolutionCalculator.Calculate(
            inputs, std, mediumVolumeMl: 900m, dilutionFactor: 1m, labelClaim: 18m, q: 80m, offsets);

        Assert.Equal(1, result.StageReached);
        Assert.Equal(DissolutionStageOutcome.Complies, result.Outcome);
        Assert.Equal("WithinLimits", result.ComparisonStatus);
        Assert.Equal("90 %", result.ReportedDisplay);
        Assert.Equal(89.5m, result.ReportedValue);
        Assert.Equal(6, result.Vessels.Count);
        Assert.All(result.Vessels, v => Assert.True(v.Passed));
        Assert.Contains("\"outcome\":\"Complies\"", result.CalculationJson);
    }
}
