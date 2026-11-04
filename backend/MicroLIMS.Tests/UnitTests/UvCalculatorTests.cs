using MicroLIMS.Application.Helpers;
using MicroLIMS.Domain.Enums;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class UvCalculatorTests
{
    [Fact]
    public void SpecificAbsorbanceAssay_CalculatesAsIsAndDriedBasis()
    {
        // Riboflavin 5-phosphate assay (444 nm, A1% 328, 73.0-79.0 % dried)
        // W 0.1 g, total volume 5000 mL, A 0.49
        decimal asIs = UvCalculator.SpecificAbsorbanceAssay(0.49m, 5000m, 328m, 0.1m);
        Assert.Equal(74.70m, Math.Round(asIs, 2, MidpointRounding.AwayFromZero));

        // LOD 5 % gives dried basis 78.63 %
        decimal dried = HplcAssayCalculator.ApplyBasis(asIs, ResultBasis.PercentDriedBasis, 5m);
        Assert.Equal(78.63m, Math.Round(dried, 2, MidpointRounding.AwayFromZero));
    }

    [Fact]
    public void SpecificAbsorbanceValue_CalculatesA1Percent1Cm()
    {
        // Pyridoxine BP identification: W 2.5 g, total volume 250000 mL, A 0.435 gives 435
        decimal val = UvCalculator.SpecificAbsorbanceValue(0.435m, 250000m, 2.5m);
        Assert.Equal(435m, Math.Round(val, 2, MidpointRounding.AwayFromZero));
    }

    [Fact]
    public void WithinReference_ChecksAbsorbanceNotGreaterThanReference()
    {
        // Riboflavin 5-phosphate inorganic phosphate (700 nm): reference 0.150
        Assert.True(UvCalculator.WithinReference(0.150m, 0.150m));
        Assert.False(UvCalculator.WithinReference(0.151m, 0.150m));
        Assert.True(UvCalculator.WithinReference(0.120m, 0.150m));
    }

    [Fact]
    public void StandardComparison_MatchesStandardComparisonCalculatorForEqualVolumes_AndDoublingVolumeDoublesResult()
    {
        decimal a = 0.50m;
        decimal stdA = 0.50m;
        decimal stdWeightG = 0.1m;
        decimal stdVolumeMl = 100m;
        decimal totalVolumeMl = 100m;
        decimal weightG = 0.1m;
        decimal purityPercent = 99.8m;
        decimal moisturePercent = 0.1m;

        decimal uvResult = UvCalculator.StandardComparison(
            a, stdA, stdWeightG, stdVolumeMl, totalVolumeMl, weightG, purityPercent, moisturePercent);

        decimal stdCalcResult = StandardComparisonCalculator.CalculatePreparationAssay(
            responseTest: a,
            responseStd: stdA,
            actWtStd: stdWeightG,
            thWtStd: stdVolumeMl,
            thWtTest: totalVolumeMl,
            actWtTest: weightG,
            moisturePercent: moisturePercent,
            purityPercent: purityPercent);

        Assert.Equal(stdCalcResult, uvResult);

        // Doubling totalVolumeMl doubles the result
        decimal doubledVolumeResult = UvCalculator.StandardComparison(
            a, stdA, stdWeightG, stdVolumeMl, totalVolumeMl * 2m, weightG, purityPercent, moisturePercent);

        Assert.Equal(uvResult * 2m, doubledVolumeResult);
    }
}
