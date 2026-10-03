using MicroLIMS.Application.Helpers;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class ResidualSolventCalculatorTests
{
    // Hand check: C_std 60 µg/mL, V 20 mL, W 400 mg = 0.4 g -> 3000 µg/g at r_u = r_std.
    //   rep 1: r_u 900  -> 3000 × 0.9 = 2700 ppm
    //   rep 2: r_u 1000 -> 3000 ppm;  mean 2850 ppm
    [Fact]
    public void Calculate_HandCheckedExample()
    {
        var r = ResidualSolventCalculator.Calculate(60m, 20m, 1000m, new[]
        {
            new ResidualSolventReplicateInput(1, 400m, 900m),
            new ResidualSolventReplicateInput(2, 400m, 1000m),
        });
        Assert.Equal(2700m, r.Replicates[0].Ppm);
        Assert.Equal(3000m, r.Replicates[1].Ppm);
        Assert.Equal(2850m, r.MeanPpm);
        Assert.Equal("2850.0 ppm", ResidualSolventCalculator.FormatPpm(r.MeanPpm));
    }

    [Theory]
    [InlineData(0, 20, 1000, 400)]
    [InlineData(60, 0, 1000, 400)]
    [InlineData(60, 20, 0, 400)]
    [InlineData(60, 20, 1000, 0)]
    public void Calculate_NonPositiveInput_Throws(decimal conc, decimal volume, decimal stdMean, decimal weight)
    {
        Assert.Throws<InvalidOperationException>(() => ResidualSolventCalculator.Calculate(conc, volume, stdMean,
            new[] { new ResidualSolventReplicateInput(1, weight, 900m) }));
    }

    [Fact]
    public void Calculate_ZeroStandardMean_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => ResidualSolventCalculator.Calculate(60m, 20m, 0m,
            new[] { new ResidualSolventReplicateInput(1, 400m, 900m) }));
        Assert.Equal("The standard mean response must be greater than zero.", ex.Message);
    }

    [Fact]
    public void Calculate_NoReplicates_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => ResidualSolventCalculator.Calculate(60m, 20m, 1000m, Array.Empty<ResidualSolventReplicateInput>()));
    }

    [Fact]
    public void Calculate_ZeroAndNonZeroReplicate_MeanIncludesZero()
    {
        var r = ResidualSolventCalculator.Calculate(60m, 20m, 1000m, new[]
        {
            new ResidualSolventReplicateInput(1, 400m, 0m),
            new ResidualSolventReplicateInput(2, 400m, 1000m),
        });
        Assert.Equal(0m, r.Replicates[0].Ppm);
        Assert.Equal(3000m, r.Replicates[1].Ppm);
        Assert.Equal(1500m, r.MeanPpm);
    }

    [Fact]
    public void Calculate_NegativeResponse_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => ResidualSolventCalculator.Calculate(60m, 20m, 1000m,
            new[] { new ResidualSolventReplicateInput(1, 400m, 900m), new ResidualSolventReplicateInput(2, 400m, -1m) }));
        Assert.Equal("Replicate 2: the response must be zero or more.", ex.Message);
    }
}
