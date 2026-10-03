using MicroLIMS.Application.Helpers;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class WorkingStandardCalculatorTests
{
    // Hand-checked: mean 99.0; deviations ±0.5/0 -> sum sq 1.0, var 0.2, SD 0.44721,
    // RSD 0.4517 %; potency = 99.0 x 100 / 99.5 = 99.4975 -> 99.497.
    [Fact]
    public void Evaluate_SixGoodReplicates_PassesWithDriedBasisPotency()
    {
        var r = WorkingStandardCalculator.Evaluate(new[] { 98.5m, 99.0m, 99.5m, 98.5m, 99.0m, 99.5m }, 0.5m);

        Assert.True(r.Passed);
        Assert.Null(r.FailureReasons);
        Assert.Equal(99.0m, r.MeanAssayPercent);
        Assert.Equal(0.45m, Math.Round(r.RsdPercent!.Value, 2));
        Assert.Equal(99.497m, r.PotencyPercent);
    }

    // Hand-checked: mean 100, deviations ±3 -> var 54/5 = 10.8, SD 3.2863, RSD 3.29 %.
    [Fact]
    public void Evaluate_HighRsd_Fails()
    {
        var r = WorkingStandardCalculator.Evaluate(new[] { 97m, 103m, 97m, 103m, 97m, 103m }, 0m);

        Assert.False(r.Passed);
        Assert.Contains("RSD 3.29 % is above 2.0 %", r.FailureReasons);
    }

    [Fact]
    public void Evaluate_FiveReplicates_Fails()
    {
        var r = WorkingStandardCalculator.Evaluate(new[] { 99m, 99m, 99m, 99m, 99m }, 0m);

        Assert.False(r.Passed);
        Assert.Contains("Exactly 6 replicates are required (5 entered).", r.FailureReasons);
    }

    // OD1: dried-basis potency above 100 % is allowed as measured. 99.8 x 100 / 99.5 = 100.3015.
    [Fact]
    public void Evaluate_PotencyAbove100_AllowedAsMeasured()
    {
        var r = WorkingStandardCalculator.Evaluate(new[] { 99.8m, 99.8m, 99.8m, 99.8m, 99.8m, 99.8m }, 0.5m);

        Assert.True(r.Passed);
        Assert.Equal(100.302m, r.PotencyPercent);
    }

    [Theory]
    [InlineData("2.0", true)]
    [InlineData("2.004", true)]
    [InlineData("2.005", false)]
    [InlineData("2.1", false)]
    public void PassesRsd_RoundsToTwoDecimalsAwayFromZero(string rsd, bool expected) =>
        Assert.Equal(expected, WorkingStandardCalculator.PassesRsd(decimal.Parse(rsd, System.Globalization.CultureInfo.InvariantCulture)));

    [Theory]
    [InlineData("-0.1")]
    [InlineData("100")]
    public void DriedBasisPotency_MoistureOutOfRange_Throws(string mc) =>
        Assert.Throws<InvalidOperationException>(() =>
            WorkingStandardCalculator.DriedBasisPotency(99m, decimal.Parse(mc, System.Globalization.CultureInfo.InvariantCulture)));
}
