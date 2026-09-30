using MicroLIMS.Application.Helpers;
using MicroLIMS.Domain.Enums;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class HplcAssayCalculatorTests
{
    /*
     ========================================================================================
     HAND-WORKED EXAMPLE (docs/superpowers/plans/2026-09-29-hplc-chain-s6-workspace-backend.md, Task B1)
     ========================================================================================
     Formula (reuses StandardComparisonCalculator.CalculatePreparationAssay):
       A% = (R_test / R_std) x (W_std / Th.Wt.std) x (Th.Wt.test / W_test) x ((100 - MC)/100) x P

     Inputs:
       R_test = 1000, R_std (mean) = 1000, W_std = 50.5 mg, Th.Wt.std = 50 mg,
       Th.Wt.test = 200 mg, W_test = 202 mg, MC = 0.5 %, P = 99.5 %

     Step-by-step:
       responseRatio      = 1000 / 1000 = 1
       stdWeightRatio      = 50.5 / 50 = 1.01
       sampleWeightRatio   = 200 / 202 = 100 / 101
       moistureCorrection  = (100 - 0.5) / 100 = 0.995

       1 x 1.01 x (100/101) = (101/100) x (100/101) = 1   [exact]
       A% = 1 x 0.995 x 99.5 = 99.0025 %

     Amount per unit (label claim 500) = 99.0025 x 500 / 100 = 495.0125
     ========================================================================================
    */

    [Fact]
    public void Calculate_HandCheckedExample_Yields_99_0025()
    {
        var std = new AssayStandard(MeanResponse: 1000m, StandardWeightMg: 50.5m, PurityPercent: 99.5m, MoisturePercent: 0.5m);
        var reps = new[] { new AssayReplicateInput(ReplicateNo: 1, ActualWeightMg: 202m, Response: 1000m) };

        var result = HplcAssayCalculator.Calculate(analyteId: 1, name: "Ascorbic Acid", thWtStdMg: 50m, thWtTestMg: 200m, std, reps);

        Assert.Single(result.Replicates);
        Assert.Equal(99.0025m, decimal.Round(result.Replicates[0].AssayPercent, 4));
        Assert.Equal(99.0025m, decimal.Round(result.MeanAssayPercent!.Value, 4));
        Assert.Equal(1, result.HplcMethodAnalyteId);
        Assert.Equal("Ascorbic Acid", result.AnalyteName);
    }

    [Fact]
    public void AmountPerUnit_HandCheckedExample_Yields_495_0125()
    {
        decimal amount = HplcAssayCalculator.AmountPerUnit(99.0025m, labelClaim: 500m);

        Assert.Equal(495.0125m, decimal.Round(amount, 4));
    }

    [Fact]
    public void Calculate_MultipleReplicates_MeanIsAverage()
    {
        // Replicate 1: same as hand-checked example -> 99.0025 %
        // Replicate 2: W_test = 200 mg (no weighing deviation) -> A% = 1 x 1.01 x 1 x 0.995 x 99.5 = 99.995...
        //   1.01 x 0.995 = 1.00495; 1.00495 x 99.5 = 99.992525
        var std = new AssayStandard(MeanResponse: 1000m, StandardWeightMg: 50.5m, PurityPercent: 99.5m, MoisturePercent: 0.5m);
        var reps = new[]
        {
            new AssayReplicateInput(ReplicateNo: 1, ActualWeightMg: 202m, Response: 1000m),
            new AssayReplicateInput(ReplicateNo: 2, ActualWeightMg: 200m, Response: 1000m)
        };

        var result = HplcAssayCalculator.Calculate(analyteId: 1, name: "Ascorbic Acid", thWtStdMg: 50m, thWtTestMg: 200m, std, reps);

        Assert.Equal(2, result.Replicates.Count);
        Assert.Equal(99.0025m, decimal.Round(result.Replicates[0].AssayPercent, 4));
        Assert.Equal(99.9925m, decimal.Round(result.Replicates[1].AssayPercent, 4));
        Assert.Equal(99.4975m, decimal.Round(result.MeanAssayPercent!.Value, 4));
    }

    [Fact]
    public void Calculate_NoReplicates_Throws()
    {
        var std = new AssayStandard(1000m, 50.5m, 99.5m, 0.5m);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            HplcAssayCalculator.Calculate(1, "Ascorbic Acid", 50m, 200m, std, Array.Empty<AssayReplicateInput>()));
        Assert.Equal("At least one replicate is required.", ex.Message);
    }

    [Theory]
    [InlineData(ProductionStageRole.Bulk, true)]
    [InlineData(ProductionStageRole.InProcess, false)]
    [InlineData(ProductionStageRole.Finished, false)]
    [InlineData(ProductionStageRole.Stability, false)]
    [InlineData(ProductionStageRole.Other, false)]
    [InlineData(null, false)]
    public void IsIndividualBasis_OnlyBulk(ProductionStageRole? role, bool expected)
    {
        Assert.Equal(expected, HplcAssayCalculator.IsIndividualBasis(role));
    }

    [Theory]
    [InlineData(ProductionStageRole.Finished, true)]
    [InlineData(ProductionStageRole.Bulk, false)]
    [InlineData(ProductionStageRole.InProcess, false)]
    [InlineData(ProductionStageRole.Stability, false)]
    [InlineData(ProductionStageRole.Other, false)]
    [InlineData(null, false)]
    public void JudgesAmountPerUnit_OnlyFinished(ProductionStageRole? role, bool expected)
    {
        Assert.Equal(expected, HplcAssayCalculator.JudgesAmountPerUnit(role));
    }
}
