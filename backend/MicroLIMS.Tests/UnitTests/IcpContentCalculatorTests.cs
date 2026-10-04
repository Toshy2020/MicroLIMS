using Xunit;
using MicroLIMS.Application.Helpers;

namespace MicroLIMS.Tests.UnitTests;

public class IcpContentCalculatorTests
{
    private static IcpReplicateInput Rep(int n, decimal w, decimal c) => new(n, w, 50m, 10m, c);

    [Fact]
    public void Calculate_MineralExample_HandChecked()
    {
        // Zn: C 0.80 mg/L, V 50 mL, DF 10, W 0.5000 g -> 0.80*50*10/0.5 = 800 µg/g
        // rep2: C 0.82, W 0.5100 -> 0.82*50*10/0.51 = 803.921569 µg/g ; mean 801.960784
        // unit 1.2500 g -> 801.960784*1.25/1000 = 1.002451 mg/unit ; claim 1 mg -> 100.2451 %
        var r = IcpContentCalculator.Calculate(new[] { Rep(1, 0.5000m, 0.80m), Rep(2, 0.5100m, 0.82m) }, 0.1m, 6m, 1m, 1.25m, 1m);
        Assert.Equal(800m, r.Replicates[0].ContentPerAmount);
        Assert.Equal(801.960784m, Math.Round(r.MeanContentPerAmount!.Value, 6));
        Assert.Equal(1.002451m, Math.Round(r.MgPerUnit!.Value, 6));
        Assert.Equal(100.2451m, Math.Round(r.PercentLabelClaim!.Value, 4));
    }

    [Fact]
    public void Calculate_ConversionFactorApplied()
    {
        var plain = IcpContentCalculator.Calculate(new[] { Rep(1, 0.5m, 0.8m) }, 0.1m, 6m, 1m, 1.25m, null);
        var caco3 = IcpContentCalculator.Calculate(new[] { Rep(1, 0.5m, 0.8m) }, 0.1m, 6m, 2.4973m, 1.25m, null);
        Assert.Equal(plain.MgPerUnit!.Value * 2.4973m, caco3.MgPerUnit!.Value);
    }

    [Fact]
    public void Calculate_AllBelowLoq_MeanNull()
    {
        var r = IcpContentCalculator.Calculate(new[] { Rep(1, 0.5m, 0.05m), Rep(2, 0.5m, 0.09m) }, 0.1m, 6m, 1m, 1.25m, 1m);
        Assert.Null(r.MeanContentPerAmount);
        Assert.True(r.AllBelowLoq);
        Assert.Null(r.MgPerUnit);
        Assert.Null(r.PercentLabelClaim);
    }

    [Fact]
    public void Calculate_SomeBelowLoq_MeanOfMeasured()
    {
        var r = IcpContentCalculator.Calculate(new[] { Rep(1, 0.5m, 0.05m), Rep(2, 0.5m, 0.8m) }, 0.1m, 6m, 1m, null, null);
        Assert.True(r.SomeBelowLoq);
        Assert.False(r.AllBelowLoq);
        Assert.True(r.Replicates[0].BelowLoq);
        Assert.Equal(800m, r.MeanContentPerAmount);
    }

    [Fact]
    public void Calculate_AboveTopStandard_OverRange()
    {
        var r = IcpContentCalculator.Calculate(new[] { Rep(1, 0.5m, 6.01m) }, 0.1m, 6m, 1m, null, null);
        Assert.True(r.AnyOverRange);
        Assert.True(r.Replicates[0].OverRange);
    }

    [Fact]
    public void Calculate_EqualToLevels_InRange()
    {
        var r = IcpContentCalculator.Calculate(new[] { Rep(1, 0.5m, 0.1m), Rep(2, 0.5m, 6m) }, 0.1m, 6m, 1m, null, null);
        Assert.False(r.AnyOverRange);
        Assert.False(r.SomeBelowLoq);
        Assert.False(r.AllBelowLoq);
    }

    [Fact]
    public void Calculate_NoUnitAmount_NoMgPerUnit()
    {
        var r = IcpContentCalculator.Calculate(new[] { Rep(1, 0.5m, 0.8m) }, 0.1m, 6m, 1m, null, 1m);
        Assert.NotNull(r.MeanContentPerAmount);
        Assert.Null(r.MgPerUnit);
        Assert.Null(r.PercentLabelClaim);
    }

    [Fact]
    public void Calculate_ZeroWeight_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            IcpContentCalculator.Calculate(new[] { Rep(1, 0m, 0.8m) }, 0.1m, 6m, 1m, null, null));
        Assert.Equal("Sample amount, volume and dilution must be positive (dilution at least 1).", ex.Message);
    }

    [Fact]
    public void Calculate_ZeroVolume_Throws()
    {
        var bad = new IcpReplicateInput(1, 0.5m, 0m, 10m, 0.8m);
        var ex = Assert.Throws<InvalidOperationException>(() => IcpContentCalculator.Calculate(new[] { bad }, 0.1m, 6m, 1m, null, null));
        Assert.Equal("Sample amount, volume and dilution must be positive (dilution at least 1).", ex.Message);
    }

    [Fact]
    public void Calculate_DilutionBelowOne_Throws()
    {
        var bad = new IcpReplicateInput(1, 0.5m, 50m, 0.5m, 0.8m);
        var ex = Assert.Throws<InvalidOperationException>(() => IcpContentCalculator.Calculate(new[] { bad }, 0.1m, 6m, 1m, null, null));
        Assert.Equal("Sample amount, volume and dilution must be positive (dilution at least 1).", ex.Message);
    }

    [Fact]
    public void Calculate_NoReplicates_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            IcpContentCalculator.Calculate(Array.Empty<IcpReplicateInput>(), 0.1m, 6m, 1m, null, null));
        Assert.Equal("At least one replicate is required.", ex.Message);
    }
}
