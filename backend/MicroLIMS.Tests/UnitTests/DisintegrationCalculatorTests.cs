using MicroLIMS.Application.Helpers;
using MicroLIMS.Domain.Enums;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class DisintegrationCalculatorTests
{
    [Fact]
    public void UnitAtExactlyLimit_PassesS1()
    {
        decimal limit = 30.0m;
        // Exactly 30.0 min passes
        var units = new List<decimal?> { 30.0m, 30.0m, 30.0m, 30.0m, 30.0m, 30.0m };
        var result = DisintegrationStageEvaluator.Evaluate(units, limit);

        Assert.Equal(1, result.StageReached);
        Assert.Equal(DissolutionStageOutcome.Complies, result.Outcome);
        Assert.Equal(6, result.PassedCount);
        Assert.Equal(30.0m, result.LongestMinutes);
        Assert.Empty(result.Reasons);
    }

    [Fact]
    public void UnitAtLimitPlusEpsilon_FailsS1()
    {
        decimal limit = 30.0m;
        // 30.0000001 fails unrounded -> 1 failure -> S2 needed
        var units = new List<decimal?> { 29.0m, 29.0m, 29.0m, 29.0m, 29.0m, 30.0000001m };
        var result = DisintegrationStageEvaluator.Evaluate(units, limit);

        Assert.Equal(1, result.StageReached);
        Assert.Equal(DissolutionStageOutcome.NextStageRequired, result.Outcome);
        Assert.Equal(5, result.PassedCount);
        Assert.Equal(30.0000001m, result.LongestMinutes);
        Assert.NotEmpty(result.Reasons);
        Assert.Contains("1 unit(s) failed", result.Reasons[0]);
    }

    [Fact]
    public void OneAndTwoFailuresAtS1_ProceedToS2()
    {
        decimal limit = 30.0m;

        // 1 failure
        var units1 = new List<decimal?> { 25.0m, 26.0m, 27.0m, 28.0m, 29.0m, 35.0m };
        var result1 = DisintegrationStageEvaluator.Evaluate(units1, limit);
        Assert.Equal(DissolutionStageOutcome.NextStageRequired, result1.Outcome);
        Assert.Equal(5, result1.PassedCount);

        // 2 failures
        var units2 = new List<decimal?> { 25.0m, 26.0m, 27.0m, 28.0m, 32.0m, 35.0m };
        var result2 = DisintegrationStageEvaluator.Evaluate(units2, limit);
        Assert.Equal(DissolutionStageOutcome.NextStageRequired, result2.Outcome);
        Assert.Equal(4, result2.PassedCount);
    }

    [Fact]
    public void ThreeFailuresAtS1_FailsImmediately()
    {
        decimal limit = 30.0m;
        // 3 failures at S1 -> DoesNotComply (no stage 2)
        var units = new List<decimal?> { 25.0m, 26.0m, 27.0m, 31.0m, 32.0m, 35.0m };
        var result = DisintegrationStageEvaluator.Evaluate(units, limit);

        Assert.Equal(1, result.StageReached);
        Assert.Equal(DissolutionStageOutcome.DoesNotComply, result.Outcome);
        Assert.Equal(3, result.PassedCount);
        Assert.NotEmpty(result.Reasons);
        Assert.Contains("exceeding maximum allowed for Stage 1", result.Reasons[0]);
    }

    [Fact]
    public void Stage2_16Of18_Passes()
    {
        decimal limit = 30.0m;
        // 16 pass, 2 fail
        var units = new List<decimal?>();
        for (int i = 0; i < 16; i++) units.Add(25.0m);
        units.Add(32.0m);
        units.Add(35.0m);

        var result = DisintegrationStageEvaluator.Evaluate(units, limit);

        Assert.Equal(2, result.StageReached);
        Assert.Equal(DissolutionStageOutcome.Complies, result.Outcome);
        Assert.Equal(16, result.PassedCount);
        Assert.Empty(result.Reasons);
    }

    [Fact]
    public void Stage2_15Of18_Fails()
    {
        decimal limit = 30.0m;
        // 15 pass, 3 fail
        var units = new List<decimal?>();
        for (int i = 0; i < 15; i++) units.Add(25.0m);
        units.Add(31.0m);
        units.Add(32.0m);
        units.Add(35.0m);

        var result = DisintegrationStageEvaluator.Evaluate(units, limit);

        Assert.Equal(2, result.StageReached);
        Assert.Equal(DissolutionStageOutcome.DoesNotComply, result.Outcome);
        Assert.Equal(15, result.PassedCount);
        Assert.NotEmpty(result.Reasons);
        Assert.Contains("below the required 16 units", result.Reasons[0]);
    }

    [Fact]
    public void NotDisintegrated_CountsAsFailure()
    {
        decimal limit = 30.0m;

        // null time counts as failure
        var unitsS1 = new List<decimal?> { 25.0m, 26.0m, 27.0m, 28.0m, 29.0m, null };
        var result1 = DisintegrationStageEvaluator.Evaluate(unitsS1, limit);
        Assert.Equal(DissolutionStageOutcome.NextStageRequired, result1.Outcome);
        Assert.Equal(5, result1.PassedCount);
        Assert.Equal(29.0m, result1.LongestMinutes);

        // All null -> longest is null, fails
        var unitsAllNull = new List<decimal?> { null, null, null, null, null, null };
        var resultAllNull = DisintegrationStageEvaluator.Evaluate(unitsAllNull, limit);
        Assert.Equal(DissolutionStageOutcome.DoesNotComply, resultAllNull.Outcome);
        Assert.Equal(0, resultAllNull.PassedCount);
        Assert.Null(resultAllNull.LongestMinutes);
    }

    [Fact]
    public void CustomTestMasterConfig_Honoured()
    {
        decimal limit = 20.0m;
        var customConfig = new DisintegrationStageConfig(
            Stage1Units: 8,
            Stage2Units: 16,
            MaxStage1Failures: 3,
            MinPassTotal: 20);

        // 8 units with 3 failures -> S2
        var s1Units = new List<decimal?> { 15m, 15m, 15m, 15m, 15m, 25m, 25m, 25m };
        var res1 = DisintegrationStageEvaluator.Evaluate(s1Units, limit, customConfig);
        Assert.Equal(DissolutionStageOutcome.NextStageRequired, res1.Outcome);

        // 8 units with 4 failures -> Fail
        var s1FailUnits = new List<decimal?> { 15m, 15m, 15m, 15m, 25m, 25m, 25m, 25m };
        var resFail = DisintegrationStageEvaluator.Evaluate(s1FailUnits, limit, customConfig);
        Assert.Equal(DissolutionStageOutcome.DoesNotComply, resFail.Outcome);

        // 24 units with 20 pass -> Complies
        var s2PassUnits = new List<decimal?>();
        for (int i = 0; i < 20; i++) s2PassUnits.Add(18m);
        for (int i = 0; i < 4; i++) s2PassUnits.Add(22m);
        var resS2Pass = DisintegrationStageEvaluator.Evaluate(s2PassUnits, limit, customConfig);
        Assert.Equal(DissolutionStageOutcome.Complies, resS2Pass.Outcome);

        // 24 units with 19 pass -> DoesNotComply
        var s2FailUnits = new List<decimal?>();
        for (int i = 0; i < 19; i++) s2FailUnits.Add(18m);
        for (int i = 0; i < 5; i++) s2FailUnits.Add(22m);
        var resS2Fail = DisintegrationStageEvaluator.Evaluate(s2FailUnits, limit, customConfig);
        Assert.Equal(DissolutionStageOutcome.DoesNotComply, resS2Fail.Outcome);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(7)]
    [InlineData(11)]
    [InlineData(15)]
    [InlineData(19)]
    public void WrongUnitCounts_Rejected(int count)
    {
        var units = new List<decimal?>();
        for (int i = 0; i < count; i++) units.Add(20m);

        var ex = Assert.Throws<InvalidOperationException>(() => DisintegrationStageEvaluator.Evaluate(units, 30m));
        Assert.Contains("requires 6 (Stage 1) or 18 (Stage 2) units", ex.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void NegativeOrZeroLimit_Throws(decimal limit)
    {
        var units = new List<decimal?> { 20m, 20m, 20m, 20m, 20m, 20m };
        var ex = Assert.Throws<InvalidOperationException>(() => DisintegrationStageEvaluator.Evaluate(units, limit));
        Assert.Contains("greater than zero", ex.Message);
    }

    [Fact]
    public void NegativeOrZeroUnitMinutes_Throws()
    {
        var units = new List<decimal?> { 20m, 20m, 0m, 20m, 20m, 20m };
        var ex = Assert.Throws<InvalidOperationException>(() => DisintegrationStageEvaluator.Evaluate(units, 30m));
        Assert.Contains("greater than zero", ex.Message);
    }

    [Fact]
    public void InvalidConfig_Throws()
    {
        var units = new List<decimal?> { 20m, 20m, 20m, 20m, 20m, 20m };

        // Stage1Units < 1
        Assert.Throws<InvalidOperationException>(() =>
            DisintegrationStageEvaluator.Evaluate(units, 30m, new DisintegrationStageConfig(Stage1Units: 0)));

        // MaxFailures >= Stage1Units
        Assert.Throws<InvalidOperationException>(() =>
            DisintegrationStageEvaluator.Evaluate(units, 30m, new DisintegrationStageConfig(Stage1Units: 6, MaxStage1Failures: 6)));

        // MinPassTotal > TotalUnits
        Assert.Throws<InvalidOperationException>(() =>
            DisintegrationStageEvaluator.Evaluate(units, 30m, new DisintegrationStageConfig(Stage1Units: 6, Stage2Units: 12, MinPassTotal: 19)));
    }
}
