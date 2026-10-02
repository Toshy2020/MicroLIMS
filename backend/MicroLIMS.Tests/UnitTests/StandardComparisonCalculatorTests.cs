using System.Globalization;
using System.Text.Json;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class StandardComparisonCalculatorTests
{
    /*
     ========================================================================================
     HAND-WORKED EXAMPLE (STM-PC-013 6.9.2.5 / STM-PC-023 Standard-Comparison Assay Formula)
     ========================================================================================
     Formula:
       % Assay (of label claim) =
           (Response_test / Response_std)
         x (ActWt_std / ThWt_std)
         x (ThWt_test / ActWt_test)
         x ((100 - MC) / 100)
         x P

     Inputs:
       Analyte: "Ascorbic Acid"
       Standard (Run Analyte):
         - Response_std = 2,500,000.0 (StandardMeanArea)
         - ActWt_std    = 50.2 mg (StandardWeightMg)
         - ThWt_std     = 50.0 mg (TheoreticalWeightMg)
         - MC           = 0.50 % (MoisturePercent)
         - P            = 99.80 % (StandardPurityPercent)

       Sample Preparation 1:
         - ThWt_test    = 100.0 mg
         - ActWt_test   = 100.5 mg (deviation = (100.5 - 100.0) / 100.0 * 100 = +0.5%)
         - Response_test = 2,490,000.0

       Sample Preparation 2:
         - ThWt_test    = 100.0 mg
         - ActWt_test   = 99.8 mg (deviation = (99.8 - 100.0) / 100.0 * 100 = -0.2%)
         - Response_test = 2,515,000.0

     Step-by-step Arithmetic:
     ----------------------------------------------------------------------------------------
     Preparation 1:
       Factor 1 (Response ratio)      = 2490000 / 2500000 = 0.996
       Factor 2 (Std weight ratio)    = 50.2 / 50.0 = 1.004
       Factor 3 (Sample weight ratio) = 100.0 / 100.5 = 200 / 201 ≈ 0.995024875621890547263681592
       Factor 4 (Moisture correction) = (100 - 0.5) / 100 = 99.5 / 100 = 0.995
       Factor 5 (Purity)              = 99.8

       % Assay (Prep 1) = 0.996 x 1.004 x (100.0 / 100.5) x 0.995 x 99.8
                        = 124124263980000 / 1256250000000
                        = 98.805384262686567164179104478 %

     Preparation 2:
       Factor 1 (Response ratio)      = 2515000 / 2500000 = 1.006
       Factor 2 (Std weight ratio)    = 50.2 / 50.0 = 1.004
       Factor 3 (Sample weight ratio) = 100.0 / 99.8 = 500 / 499 ≈ 1.002004008016032064128256513
       Factor 4 (Moisture correction) = (100 - 0.5) / 100 = 0.995
       Factor 5 (Purity)              = 99.8

       % Assay (Prep 2) = 1.006 x 1.004 x (100.0 / 99.8) x 0.995 x 99.8
                        = 1256217350000 / 12500000000
                        = 100.49738800000000000000000000 %

     Mean Reported % Assay:
       Mean = (98.805384262686567164179104478 + 100.497388) / 2
            = 99.651386131343283582089552239 %
       Reported Display (1 dp) = "99.7 %"

     Preparation RSD:
       Mean (x̄) = 99.651386131343283582089552239
       d1 = 98.805384262686567164179104478 - 99.651386131343283582089552239 = -0.846001868656716417910447761
       d2 = 100.497388 - 99.651386131343283582089552239 = +0.846001868656716417910447761
       d1^2 + d2^2 = 2 * (0.846001868656716417910447761)^2 = 1.4314383235414638708170366624
       variance (s^2) = 1.4314383235414638708170366624 / (2 - 1) = 1.4314383235414638708170366624
       stdDev (s) = sqrt(1.4314383235414638708170366624) ≈ 1.1964273164473772274482490518
       RSD % = (s / x̄) * 100 ≈ 1.200612821102927230495346083 %
     ========================================================================================
    */

    [Fact]
    public void CalculatePreparationAssay_MatchesHandCalculatedExample()
    {
        // Prep 1
        decimal prep1 = StandardComparisonCalculator.CalculatePreparationAssay(
            responseTest: 2490000m,
            responseStd: 2500000m,
            actWtStd: 50.2m,
            thWtStd: 50.0m,
            thWtTest: 100.0m,
            actWtTest: 100.5m,
            moisturePercent: 0.5m,
            purityPercent: 99.8m);

        decimal expectedPrep1 = (2490000m / 2500000m) * (50.2m / 50.0m) * (100.0m / 100.5m) * ((100m - 0.5m) / 100m) * 99.8m;
        Assert.Equal(expectedPrep1, prep1);
        Assert.Equal(98.8m, Math.Round(prep1, 1, MidpointRounding.AwayFromZero));

        // Prep 2
        decimal prep2 = StandardComparisonCalculator.CalculatePreparationAssay(
            responseTest: 2515000m,
            responseStd: 2500000m,
            actWtStd: 50.2m,
            thWtStd: 50.0m,
            thWtTest: 100.0m,
            actWtTest: 99.8m,
            moisturePercent: 0.5m,
            purityPercent: 99.8m);

        decimal expectedPrep2 = (2515000m / 2500000m) * (50.2m / 50.0m) * (100.0m / 99.8m) * ((100m - 0.5m) / 100m) * 99.8m;
        Assert.Equal(expectedPrep2, prep2);
        Assert.Equal(100.5m, Math.Round(prep2, 1, MidpointRounding.AwayFromZero));

        // RSD of the two preparations
        var (rsd, exceeded, _) = StandardComparisonCalculator.CalculatePreparationRsd(new[] { prep1, prep2 }, 2.0m);
        Assert.False(exceeded);
        Assert.NotNull(rsd);
        Assert.InRange(rsd.Value, 1.20m, 1.21m);
    }

    [Fact]
    public void MoistureCorrection_ZeroMoisture_BehavesAsNoCorrection()
    {
        // When MC = 0, factor (100 - 0)/100 = 1.0
        decimal assay = StandardComparisonCalculator.CalculatePreparationAssay(
            responseTest: 1000m,
            responseStd: 1000m,
            actWtStd: 50.0m,
            thWtStd: 50.0m,
            thWtTest: 100.0m,
            actWtTest: 100.0m,
            moisturePercent: 0m,
            purityPercent: 100m);

        Assert.Equal(100.0m, assay);
    }

    [Fact]
    public void MoistureCorrection_InvalidMoisture_ThrowsInvalidOperationException()
    {
        Assert.Throws<InvalidOperationException>(() =>
            StandardComparisonCalculator.CalculatePreparationAssay(
                1000m, 1000m, 50m, 50m, 100m, 100m, moisturePercent: -1m, purityPercent: 100m));

        Assert.Throws<InvalidOperationException>(() =>
            StandardComparisonCalculator.CalculatePreparationAssay(
                1000m, 1000m, 50m, 50m, 100m, 100m, moisturePercent: 100m, purityPercent: 100m));
    }

    [Fact]
    public void PreparationRsd_SinglePreparation_ReturnsNullRsdAndDoesNotExceed()
    {
        var (rsd, exceeded, reason) = StandardComparisonCalculator.CalculatePreparationRsd(
            new List<decimal> { 99.5m }, maxPreparationRsdPercent: 2.0m);

        Assert.Null(rsd);
        Assert.False(exceeded);
        Assert.Null(reason);
    }

    [Fact]
    public void PreparationRsd_ExactlyAtLimit_PassesWithoutReview()
    {
        // Two preparations: 98.0 and 102.0 -> mean = 100.0
        // diffs = -2.0, +2.0; sumDiff^2 = 8; var = 8 / (2 - 1) = 8; stdDev = sqrt(8) ≈ 2.8284271247
        // RSD = 2.8284271247 %
        var values = new List<decimal> { 98.0m, 102.0m };
        decimal rsdValue = (DecimalMath.Sqrt(8m, 10) / 100.0m) * 100m;

        // If max limit is set to the exact RSD value, it should NOT exceed (rsd > max is false)
        var (rsd, exceeded, reason) = StandardComparisonCalculator.CalculatePreparationRsd(values, maxPreparationRsdPercent: rsdValue);

        Assert.False(exceeded);
        Assert.Null(reason);
    }

    [Fact]
    public void PreparationRsd_OverLimit_SetsRequiresReview()
    {
        var (_, exceeded, reason) = StandardComparisonCalculator.CalculatePreparationRsd(new[] { 95.0m, 104.5m }, 2.0m);

        Assert.True(exceeded);
        Assert.NotNull(reason);
        Assert.Contains("exceeds maximum allowed 2.00%", reason);
    }
}
