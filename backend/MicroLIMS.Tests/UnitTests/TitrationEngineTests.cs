using MicroLIMS.Application.Helpers;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class TitrationEngineTests
{
    [Fact]
    public void PrimaryStandard_KhpAgainstNaoh_WithBlankAndPurity()
    {
        // 0.1 N NaOH, KHP: E = 20.42 mg/mL; (408.4 x 0.999) / ((20.05 - 0.05) x 20.42) = 407.9916 / 408.4 = 0.999
        Assert.Equal(0.999m, decimal.Round(TitrationEngine.PrimaryStandardFactor(408.4m, 99.9m, 20.05m, 0.05m, 20.42m), 6));
    }

    [Fact]
    public void PrimaryStandard_NoPurity_Uses100()
    {
        // 204.2 / (10.00 x 20.42) = 1.0
        Assert.Equal(1.0m, decimal.Round(TitrationEngine.PrimaryStandardFactor(204.2m, null, 10.00m, 0m, 20.42m), 6));
    }

    [Fact]
    public void AgainstVs_Example()
    {
        // (25.00 x 1.002 x 0.1) / ((24.90 - 0) x 0.1) = 2.505 / 2.49 = 1.0060241 (7 dp)
        Assert.Equal(1.0060241m, decimal.Round(TitrationEngine.AgainstVolumetricSolutionFactor(25.00m, 1.002m, 0.1m, 24.90m, 0m, 0.1m), 7));
    }

    [Fact]
    public void VolumeNotAboveBlank_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            TitrationEngine.PrimaryStandardFactor(408.4m, 99.9m, 0.05m, 0.05m, 20.42m));
        Assert.Equal("Titrant volume must be greater than the blank.", ex.Message);
    }

    [Fact]
    public void Purity_Zero_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            TitrationEngine.PrimaryStandardFactor(408.4m, 0m, 20.05m, 0.05m, 20.42m));
        Assert.Equal("Purity must be greater than 0 and at most 100.", ex.Message);
    }

    [Fact]
    public void Weight_Zero_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            TitrationEngine.PrimaryStandardFactor(0m, 99.9m, 20.05m, 0.05m, 20.42m));
        Assert.Equal("Standard weight must be greater than zero.", ex.Message);
    }

    [Fact]
    public void Blank_Negative_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            TitrationEngine.PrimaryStandardFactor(408.4m, 99.9m, 20.05m, -0.05m, 20.42m));
        Assert.Equal("Blank must not be negative.", ex.Message);
    }

    [Fact]
    public void Evaluate_ThreeInRange_Passes()
    {
        // 0.998, 1.000, 1.002 -> mean 1.000, SD 0.002, RSD 0.2 %
        var e = TitrationEngine.Evaluate(new[] { 0.998m, 1.000m, 1.002m }, 0.95m, 1.05m, 0.5m);
        Assert.True(e.Passed);
        Assert.Equal(1.000m, decimal.Round(e.MeanFactor, 4));
        Assert.Equal(0.20m, decimal.Round(e.RsdPercent!.Value, 2));
    }

    [Fact]
    public void Evaluate_OneOutOfRange_Fails_NamesReplicate()
    {
        var e = TitrationEngine.Evaluate(new[] { 0.94m }, 0.95m, 1.05m, 0.5m);
        Assert.False(e.Passed);
        Assert.Contains("Replicate 1: factor 0.9400 outside 0.9500–1.0500.", e.FailureReasons);
    }

    [Fact]
    public void Evaluate_RsdTooHigh_Fails()
    {
        // 0.96, 1.04 with max RSD 0.5 -> both in range, RSD ~5.66% exceeds max
        var e = TitrationEngine.Evaluate(new[] { 0.96m, 1.04m }, 0.90m, 1.10m, 0.5m);
        Assert.False(e.Passed);
        Assert.Contains("RSD 5.66% exceeds 0.50%.", e.FailureReasons);
    }

    [Fact]
    public void Evaluate_SingleReplicate_RsdNull_Passes()
    {
        var e = TitrationEngine.Evaluate(new[] { 1.00m }, 0.95m, 1.05m, 0.5m);
        Assert.True(e.Passed);
        Assert.Null(e.RsdPercent);
    }

    [Fact]
    public void Evaluate_NoFactors_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            TitrationEngine.Evaluate(Array.Empty<decimal>(), 0.95m, 1.05m, 0.5m));
        Assert.Equal("At least one replicate is required.", ex.Message);
    }
}
