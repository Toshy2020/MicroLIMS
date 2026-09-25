using MicroLIMS.Application.Helpers;
using MicroLIMS.Domain.Enums;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class WeightVariationCalculatorTests
{
    private static WeightVariationUnitInput Tab(decimal weight) => new(WeightMg: weight);
    private static WeightVariationUnitInput Cap(decimal gross, decimal shell) => new(GrossMg: gross, ShellMg: shell);

    // Net contents with deviations that cancel out, so the mean stays 100 mg and every
    // listed deviation is between the inner (10 %) and outer (25 %) limits.
    private static readonly decimal[] Three = { 12m, 12m, -24m };
    private static readonly decimal[] Six = { 15m, 15m, 15m, -15m, -15m, -15m };
    private static readonly decimal[] Seven = { 12m, 12m, -24m, 15m, 15m, -15m, -15m };

    private static List<WeightVariationUnitInput> SoftCaps(int total, decimal[] deviations) =>
        Enumerable.Range(0, total)
            .Select(i => Cap(200m + (i < deviations.Length ? deviations[i] : 0m), 100m))
            .ToList();

    [Theory]
    [InlineData(3)]
    [InlineData(6)]
    public void SoftCapsule_ThreeToSixOutsideInner_RequiresStage2(int outside)
    {
        var res = WeightVariationEvaluator.Evaluate(DosageForm.SoftCapsule, SoftCaps(20, outside == 3 ? Three : Six));
        Assert.Equal(100m, res.MeanWeightMg);
        Assert.Equal(DissolutionStageOutcome.NextStageRequired, res.Outcome);
    }

    [Fact]
    public void SoftCapsule_SevenOutsideInner_FailsAtStage1()
    {
        var res = WeightVariationEvaluator.Evaluate(DosageForm.SoftCapsule, SoftCaps(20, Seven));
        Assert.Equal(DissolutionStageOutcome.DoesNotComply, res.Outcome);
    }

    [Fact]
    public void Capsule_Stage2_SixOf60Pass_SevenOf60Fail()
    {
        Assert.Equal(DissolutionStageOutcome.Complies,
            WeightVariationEvaluator.Evaluate(DosageForm.SoftCapsule, SoftCaps(60, Six)).Outcome);
        Assert.Equal(DissolutionStageOutcome.DoesNotComply,
            WeightVariationEvaluator.Evaluate(DosageForm.SoftCapsule, SoftCaps(60, Seven)).Outcome);
    }

    [Fact]
    public void HardCapsule_StepAPass_ReportsGrossDeviations_NotNet()
    {
        // Identical gross weights, but three shells so heavy that the net contents would
        // fail outright (> 25 %). The intact-capsule shortcut decides, so no unit is a fail.
        var units = Enumerable.Range(0, 20).Select(i => Cap(200m, i < 3 ? 130m : 100m)).ToList();

        var res = WeightVariationEvaluator.Evaluate(DosageForm.HardCapsule, units);

        Assert.Equal(DissolutionStageOutcome.Complies, res.Outcome);
        Assert.True(res.StepAPassed);
        Assert.All(res.Units, u => Assert.True(u.Passed));
        Assert.All(res.Units, u => Assert.Equal(0m, u.DeviationPercent));
    }

    [Fact]
    public void Tablet_BandEdges_SelectCorrectBandPercent()
    {
        // Mean exactly 130 -> Band 1 (10%)
        var units130 = Enumerable.Range(0, 20).Select(_ => Tab(130m)).ToList();
        var res130 = WeightVariationEvaluator.Evaluate(DosageForm.Tablet, units130);
        Assert.Equal(130m, res130.MeanWeightMg);
        Assert.Equal(10m, res130.BandPercentUsed);

        // Mean 130.000001 -> Band 2 (7.5%)
        var units130Plus = Enumerable.Range(0, 19).Select(_ => Tab(130m)).ToList();
        units130Plus.Add(Tab(130.000020m)); // (19*130 + 130.000020)/20 = 130.000001
        var res130Plus = WeightVariationEvaluator.Evaluate(DosageForm.Tablet, units130Plus);
        Assert.Equal(130.000001m, res130Plus.MeanWeightMg);
        Assert.Equal(7.5m, res130Plus.BandPercentUsed);

        // Mean exactly 324 -> Band 2 (7.5%)
        var units324 = Enumerable.Range(0, 20).Select(_ => Tab(324m)).ToList();
        var res324 = WeightVariationEvaluator.Evaluate(DosageForm.Tablet, units324);
        Assert.Equal(324m, res324.MeanWeightMg);
        Assert.Equal(7.5m, res324.BandPercentUsed);

        // Mean 324.000001 -> Band 3 (5%)
        var units324Plus = Enumerable.Range(0, 19).Select(_ => Tab(324m)).ToList();
        units324Plus.Add(Tab(324.000020m)); // (19*324 + 324.000020)/20 = 324.000001
        var res324Plus = WeightVariationEvaluator.Evaluate(DosageForm.Tablet, units324Plus);
        Assert.Equal(324.000001m, res324Plus.MeanWeightMg);
        Assert.Equal(5m, res324Plus.BandPercentUsed);
    }

    [Fact]
    public void Tablet_UnitDeviationExactlyAtBand_Passes()
    {
        // Mean = 500mg, Band 3 (5%). 5% of 500 is 25mg.
        // A unit at 525mg has deviation |525 - 500| * 100 / 500 = 5.0%.
        // 10 units at 525, 10 units at 475 -> Mean = 500mg, all deviations exactly 5.0%
        var units = new List<WeightVariationUnitInput>();
        for (int i = 0; i < 10; i++) units.Add(Tab(525m));
        for (int i = 0; i < 10; i++) units.Add(Tab(475m));

        var res = WeightVariationEvaluator.Evaluate(DosageForm.Tablet, units);
        Assert.Equal(500m, res.MeanWeightMg);
        Assert.Equal(5m, res.BandPercentUsed);
        Assert.Equal(DissolutionStageOutcome.Complies, res.Outcome);
        Assert.All(res.Units, u =>
        {
            Assert.Equal(5.0m, u.DeviationPercent);
            Assert.True(u.Passed);
        });
    }

    [Fact]
    public void Tablet_TwoOutsideBand_Passes_ThreeOutside_Fails()
    {
        // Mean = 500mg, Band 3 (5%). Dev > 5% but <= 10% (2P).
        // 2 units outside 5% (e.g. 530mg, dev = 6.0%)
        // 18 units balanced so mean = 500
        // sum = 20 * 500 = 10000. 2 * 530 = 1060. Remaining 18 units sum to 8940 -> 8940 / 18 = 496.6666666667
        // Let's create an exact integer balance:
        // 2 units at 530 (+30 each, +60 total)
        // 2 units at 470 (-30 each, -60 total)
        // 16 units at 500 (0 deviation)
        // Mean = 500. 530 is 6.0% dev, 470 is 6.0% dev. Both > 5% and <= 10%.
        // Exactly 2 units outside 5% -> Complies
        var unitsPass = new List<WeightVariationUnitInput>
        {
            Tab(530m), Tab(530m),
            Tab(470m), Tab(470m)
        };
        // wait, 530 and 470: both have dev 6.0%, so that's 4 units outside!
        // To have exactly 2 units outside:
        // 1 unit at 530 (+30), 1 unit at 470 (-30) -> 2 units outside 5%.
        // 18 units at 500 -> 0% deviation.
        var unitsTwoOutside = new List<WeightVariationUnitInput>
        {
            Tab(530m),
            Tab(470m)
        };
        for (int i = 0; i < 18; i++) unitsTwoOutside.Add(Tab(500m));

        var resPass = WeightVariationEvaluator.Evaluate(DosageForm.Tablet, unitsTwoOutside);
        Assert.Equal(500m, resPass.MeanWeightMg);
        Assert.Equal(5m, resPass.BandPercentUsed);
        Assert.Equal(DissolutionStageOutcome.Complies, resPass.Outcome);
        Assert.Equal(2, resPass.Units.Count(u => !u.Passed));

        // 3 units outside 5% (dev > 5%, <= 10%):
        // 2 units at 530 (+60), 1 unit at 440 (-60 -> dev 12% > 10% so that would fail 2P).
        // Let's do: 2 units at 530 (+30 each = +60), 1 unit at 470 (-30), 2 units at 485 (-15 each = -30).
        // Deviations: 530 -> 6.0% (>5%), 470 -> 6.0% (>5%), 485 -> 3.0% (<=5%), 500 -> 0%.
        // Wait, what if we have 3 units outside:
        // 2 units at 530 (+60 total, dev 6%), 1 unit at 470 (-30, dev 6%), 3 units at 490 (-10 each = -30, dev 2%).
        // 14 units at 500 (0 dev).
        // Total sum: 2*530 (1060) + 470 + 3*490 (1470) + 14*500 (7000) = 10000. Mean = 500.
        // Units with dev > 5%: the two 530s and the one 470 = exactly 3 units with dev 6%.
        // 490 dev = 2% <= 5%, 500 dev = 0% <= 5%.
        // No unit has dev > 10% (2P = 10%).
        var unitsThreeOutside = new List<WeightVariationUnitInput>
        {
            Tab(530m), Tab(530m),
            Tab(470m),
            Tab(490m), Tab(490m), Tab(490m)
        };
        for (int i = 0; i < 14; i++) unitsThreeOutside.Add(Tab(500m));

        var resFail = WeightVariationEvaluator.Evaluate(DosageForm.Tablet, unitsThreeOutside);
        Assert.Equal(500m, resFail.MeanWeightMg);
        Assert.Equal(DissolutionStageOutcome.DoesNotComply, resFail.Outcome);
        Assert.Equal(3, resFail.Units.Count(u => !u.Passed));
        Assert.Contains(resFail.Reasons, r => r.Contains("3 unit(s) exceeded limit of 5 %"));
    }

    [Fact]
    public void Tablet_OneBeyondTwoP_FailsImmediately()
    {
        // Mean = 500mg, Band 3 (5%, 2P = 10%).
        // 1 unit at 555mg (+55, dev = 11.0% > 10% = 2P).
        // 1 unit at 445mg (-55, dev = 11.0% > 10% = 2P).
        // Or 1 unit at 555 (+55), 11 units at 495 (-5 each = -55), 8 units at 500.
        // Sum = 555 + 11*495 (5445) + 8*500 (4000) = 10000. Mean = 500.
        // Deviations: 555 -> 11% (> 10% 2P), 495 -> 1% (<= 5%), 500 -> 0%.
        // Only ONE unit is outside 5%, but it is beyond 2P (10%)!
        var units = new List<WeightVariationUnitInput> { Tab(555m) };
        for (int i = 0; i < 11; i++) units.Add(Tab(495m));
        for (int i = 0; i < 8; i++) units.Add(Tab(500m));

        var res = WeightVariationEvaluator.Evaluate(DosageForm.Tablet, units);
        Assert.Equal(500m, res.MeanWeightMg);
        Assert.Equal(DissolutionStageOutcome.DoesNotComply, res.Outcome);
        Assert.Equal(1, res.Units.Count(u => !u.Passed));
        Assert.Contains(res.Units, u => u.DeviationPercent > 10m);
        Assert.Contains(res.Reasons, r => r.Contains("exceeded 2x limit"));
    }

    [Fact]
    public void HardCapsule_StepA_IntactWithinInnerPercent_CompliesWithoutNetCheck()
    {
        // Gross weights all within 10% of mean gross weight (e.g. all 600mg).
        // But shells are varied such that net weights have many units outside 10%!
        // Mean gross = 600mg, gross dev = 0% for all.
        // Under USP <2091>, Step A succeeds -> Complies!
        var units = new List<WeightVariationUnitInput>();
        for (int i = 0; i < 10; i++) units.Add(Cap(gross: 600m, shell: 100m)); // net = 500
        for (int i = 0; i < 10; i++) units.Add(Cap(gross: 600m, shell: 300m)); // net = 300
        // Net mean = 400. 500 is 25% dev from 400, 300 is 25% dev from 400.
        // If evaluated on net, all 20 units would fail. But intact gross weights are identical!
        var res = WeightVariationEvaluator.Evaluate(DosageForm.HardCapsule, units);

        Assert.Equal(DissolutionStageOutcome.Complies, res.Outcome);
        Assert.True(res.StepAPassed);
        Assert.Equal(1, res.StageReached);
        Assert.Equal(600m, res.MeanGrossMg);
        Assert.Equal(400m, res.MeanWeightMg); // reported value is net mean
    }

    [Fact]
    public void HardCapsule_StepB_EvaluatesNetContents_Pass_Retest_Fail()
    {
        // Gross weights fail Step A (at least one gross dev > 10%):
        // Gross mean = 600. One gross at 700 (+100, dev 16.7% > 10%), one at 500 (-100).
        // 18 at 600.
        // Shells:
        // For the 700 gross, shell = 300 -> net = 400.
        // For the 500 gross, shell = 100 -> net = 400.
        // For the 18 600 gross:
        // If shell = 200, net = 400.
        // Mean net = 400.

        // Case 1: 2 net units outside 10% (<= S1MaxOutside = 2), none outside 25% -> Complies
        var unitsPass = new List<WeightVariationUnitInput>
        {
            Cap(gross: 700m, shell: 300m), // net 400
            Cap(gross: 500m, shell: 100m), // net 400
            Cap(gross: 600m, shell: 150m), // net 450 (dev 12.5% > 10%, <= 25%)
            Cap(gross: 600m, shell: 250m)  // net 350 (dev 12.5% > 10%, <= 25%)
        };
        for (int i = 0; i < 16; i++) unitsPass.Add(Cap(gross: 600m, shell: 200m)); // net 400 (dev 0%)

        var resPass = WeightVariationEvaluator.Evaluate(DosageForm.HardCapsule, unitsPass);
        Assert.False(resPass.StepAPassed);
        Assert.Equal(DissolutionStageOutcome.Complies, resPass.Outcome);
        Assert.Equal(2, resPass.Units.Count(u => !u.Passed));

        // Case 2: 4 net units outside 10% (between 3 and 6) -> NextStageRequired
        var unitsRetest = new List<WeightVariationUnitInput>
        {
            Cap(gross: 700m, shell: 300m), // net 400
            Cap(gross: 500m, shell: 100m), // net 400
            Cap(gross: 600m, shell: 150m), // net 450 (dev 12.5%)
            Cap(gross: 600m, shell: 150m), // net 450 (dev 12.5%)
            Cap(gross: 600m, shell: 250m), // net 350 (dev 12.5%)
            Cap(gross: 600m, shell: 250m)  // net 350 (dev 12.5%)
        };
        for (int i = 0; i < 14; i++) unitsRetest.Add(Cap(gross: 600m, shell: 200m)); // net 400

        var resRetest = WeightVariationEvaluator.Evaluate(DosageForm.HardCapsule, unitsRetest);
        Assert.False(resRetest.StepAPassed);
        Assert.Equal(DissolutionStageOutcome.NextStageRequired, resRetest.Outcome);
        Assert.Equal(4, resRetest.Units.Count(u => !u.Passed));
        Assert.Contains(resRetest.Reasons, r => r.Contains("Proceed to Stage 2"));

        // Case 3: 7 net units outside 10% (> S1MaxForRetest = 6) -> DoesNotComply
        var unitsFail = new List<WeightVariationUnitInput>
        {
            Cap(gross: 700m, shell: 300m), // net 400
            Cap(gross: 500m, shell: 100m), // net 400
            Cap(gross: 600m, shell: 150m), // net 450
            Cap(gross: 600m, shell: 150m), // net 450
            Cap(gross: 600m, shell: 150m), // net 450
            Cap(gross: 600m, shell: 150m), // net 450
            Cap(gross: 600m, shell: 250m), // net 350
            Cap(gross: 600m, shell: 250m), // net 350
            Cap(gross: 600m, shell: 250m)  // net 350
        };
        for (int i = 0; i < 11; i++) unitsFail.Add(Cap(gross: 600m, shell: 200m)); // net 400

        var resFail = WeightVariationEvaluator.Evaluate(DosageForm.HardCapsule, unitsFail);
        Assert.False(resFail.StepAPassed);
        Assert.Equal(DissolutionStageOutcome.DoesNotComply, resFail.Outcome);
        Assert.Equal(7, resFail.Units.Count(u => !u.Passed));

        // Case 4: One unit > 25% (outer limit) -> DoesNotComply immediately
        // Mean net = 400. 25% of 400 is 100. Net 510 -> dev 27.5% > 25%.
        var unitsOuterFail = new List<WeightVariationUnitInput>
        {
            Cap(gross: 700m, shell: 190m), // net 510 (dev 27.5% > 25%)
            Cap(gross: 500m, shell: 210m)  // net 290 (dev 27.5% > 25%)
        };
        for (int i = 0; i < 18; i++) unitsOuterFail.Add(Cap(gross: 600m, shell: 200m)); // net 400

        var resOuter = WeightVariationEvaluator.Evaluate(DosageForm.HardCapsule, unitsOuterFail);
        Assert.False(resOuter.StepAPassed);
        Assert.Equal(DissolutionStageOutcome.DoesNotComply, resOuter.Outcome);
        Assert.Contains(resOuter.Reasons, r => r.Contains("exceeded outer limit of 25 %"));
    }

    [Fact]
    public void SoftCapsule_SkipsStepA_DirectlyEvaluatesStepB()
    {
        // Intact gross weights are identical (would pass Step A for hard capsule).
        // But net weights have 4 units outside 10%.
        // For SoftCapsule, Step A is skipped, so it goes directly to Step B -> NextStageRequired.
        var units = new List<WeightVariationUnitInput>
        {
            Cap(gross: 600m, shell: 150m), // net 450 (dev 12.5%)
            Cap(gross: 600m, shell: 150m), // net 450 (dev 12.5%)
            Cap(gross: 600m, shell: 250m), // net 350 (dev 12.5%)
            Cap(gross: 600m, shell: 250m)  // net 350 (dev 12.5%)
        };
        for (int i = 0; i < 16; i++) units.Add(Cap(gross: 600m, shell: 200m)); // net 400

        var res = WeightVariationEvaluator.Evaluate(DosageForm.SoftCapsule, units);
        Assert.Null(res.StepAPassed);
        Assert.Equal(DissolutionStageOutcome.NextStageRequired, res.Outcome);
    }

    [Fact]
    public void Capsule_Stage2_Evaluates60UnitMean_PassAndFail()
    {
        // 60 units (20 stage 1 + 40 stage 2). Mean net = 400.
        // Limit: Inner 10%, Outer 25%, S2MaxOutside = 6.

        // Case 1: Exactly 6 units outside 10%, none > 25% -> Complies
        var units60Pass = new List<WeightVariationUnitInput>
        {
            Cap(gross: 600m, shell: 150m), // net 450 (dev 12.5%)
            Cap(gross: 600m, shell: 150m),
            Cap(gross: 600m, shell: 150m),
            Cap(gross: 600m, shell: 250m), // net 350 (dev 12.5%)
            Cap(gross: 600m, shell: 250m),
            Cap(gross: 600m, shell: 250m)
        };
        for (int i = 0; i < 54; i++) units60Pass.Add(Cap(gross: 600m, shell: 200m)); // net 400

        var resPass = WeightVariationEvaluator.Evaluate(DosageForm.HardCapsule, units60Pass);
        Assert.Equal(2, resPass.StageReached);
        Assert.Equal(DissolutionStageOutcome.Complies, resPass.Outcome);
        Assert.Equal(6, resPass.Units.Count(u => !u.Passed));

        // Case 2: 7 units outside 10% -> DoesNotComply
        var units60Fail = new List<WeightVariationUnitInput>
        {
            Cap(gross: 600m, shell: 150m),
            Cap(gross: 600m, shell: 150m),
            Cap(gross: 600m, shell: 150m),
            Cap(gross: 600m, shell: 150m),
            Cap(gross: 600m, shell: 250m),
            Cap(gross: 600m, shell: 250m),
            Cap(gross: 600m, shell: 250m)
        };
        // Balance mean with 1 unit at 350 (from the 4th 450)
        units60Fail.Add(Cap(gross: 600m, shell: 250m)); // net 350 (now 8 units outside)
        for (int i = 0; i < 52; i++) units60Fail.Add(Cap(gross: 600m, shell: 200m));

        var resFail = WeightVariationEvaluator.Evaluate(DosageForm.HardCapsule, units60Fail);
        Assert.Equal(2, resFail.StageReached);
        Assert.Equal(DissolutionStageOutcome.DoesNotComply, resFail.Outcome);
        Assert.Equal(8, resFail.Units.Count(u => !u.Passed));
    }

    [Fact]
    public void UnitInputValidation_ThrowsOnInvalidData()
    {
        // Shell >= Gross
        var unitsEqual = new List<WeightVariationUnitInput>();
        for (int i = 0; i < 19; i++) unitsEqual.Add(Cap(600m, 200m));
        unitsEqual.Add(Cap(200m, 200m));
        var exEqual = Assert.Throws<InvalidOperationException>(() =>
            WeightVariationEvaluator.Evaluate(DosageForm.HardCapsule, unitsEqual));
        Assert.Contains("Shell weight must be less than gross weight", exEqual.Message);

        // Shell <= 0
        var unitsZeroShell = new List<WeightVariationUnitInput>();
        for (int i = 0; i < 19; i++) unitsZeroShell.Add(Cap(600m, 200m));
        unitsZeroShell.Add(Cap(600m, 0m));
        var exZero = Assert.Throws<InvalidOperationException>(() =>
            WeightVariationEvaluator.Evaluate(DosageForm.HardCapsule, unitsZeroShell));
        Assert.Contains("Shell weight must be greater than zero", exZero.Message);

        // Tablet weight <= 0
        var unitsZeroTab = new List<WeightVariationUnitInput>();
        for (int i = 0; i < 19; i++) unitsZeroTab.Add(Tab(500m));
        unitsZeroTab.Add(Tab(0m));
        var exZeroTab = Assert.Throws<InvalidOperationException>(() =>
            WeightVariationEvaluator.Evaluate(DosageForm.Tablet, unitsZeroTab));
        Assert.Contains("Tablet unit weight must be greater than zero", exZeroTab.Message);

        // Tablet with GrossMg
        var unitsTabGross = new List<WeightVariationUnitInput>();
        for (int i = 0; i < 19; i++) unitsTabGross.Add(Tab(500m));
        unitsTabGross.Add(new WeightVariationUnitInput(WeightMg: 500m, GrossMg: 600m));
        var exTabGross = Assert.Throws<InvalidOperationException>(() =>
            WeightVariationEvaluator.Evaluate(DosageForm.Tablet, unitsTabGross));
        Assert.Contains("Tablet units must only contain WeightMg", exTabGross.Message);

        // Capsule with WeightMg
        var unitsCapWeight = new List<WeightVariationUnitInput>();
        for (int i = 0; i < 19; i++) unitsCapWeight.Add(Cap(600m, 200m));
        unitsCapWeight.Add(new WeightVariationUnitInput(WeightMg: 400m, GrossMg: 600m, ShellMg: 200m));
        var exCapWeight = Assert.Throws<InvalidOperationException>(() =>
            WeightVariationEvaluator.Evaluate(DosageForm.HardCapsule, unitsCapWeight));
        Assert.Contains("Capsule units must only contain GrossMg and ShellMg", exCapWeight.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(19)]
    [InlineData(21)]
    [InlineData(59)]
    [InlineData(61)]
    public void WrongUnitCounts_Rejected(int count)
    {
        var units = Enumerable.Range(0, count).Select(_ => Cap(600m, 200m)).ToList();
        var ex = Assert.Throws<InvalidOperationException>(() =>
            WeightVariationEvaluator.Evaluate(DosageForm.HardCapsule, units));
        Assert.Contains("requires 20 (Stage 1) or 60 (Stage 2) units", ex.Message);
    }

    [Fact]
    public void Tablet_Stage2Count_Rejected()
    {
        var units = Enumerable.Range(0, 60).Select(_ => Tab(500m)).ToList();
        var ex = Assert.Throws<InvalidOperationException>(() =>
            WeightVariationEvaluator.Evaluate(DosageForm.Tablet, units));
        Assert.Contains("Tablets do not support Stage 2 evaluation", ex.Message);
    }

    [Fact]
    public void InvalidConfig_Throws()
    {
        var units = Enumerable.Range(0, 20).Select(_ => Tab(500m)).ToList();

        // UnitCount < 1
        Assert.Throws<InvalidOperationException>(() =>
            WeightVariationEvaluator.Evaluate(DosageForm.Tablet, units, new WeightVariationConfig(UnitCount: 0)));

        // TabletBand1MaxMg >= TabletBand2MaxMg
        Assert.Throws<InvalidOperationException>(() =>
            WeightVariationEvaluator.Evaluate(DosageForm.Tablet, units, new WeightVariationConfig(TabletBand1MaxMg: 350m, TabletBand2MaxMg: 324m)));

        // CapsuleInnerPercent >= CapsuleOuterPercent
        Assert.Throws<InvalidOperationException>(() =>
            WeightVariationEvaluator.Evaluate(DosageForm.Tablet, units, new WeightVariationConfig(CapsuleInnerPercent: 25m, CapsuleOuterPercent: 25m)));

        // CapsuleS1MaxOutside >= CapsuleS1MaxForRetest
        Assert.Throws<InvalidOperationException>(() =>
            WeightVariationEvaluator.Evaluate(DosageForm.Tablet, units, new WeightVariationConfig(CapsuleS1MaxOutside: 6, CapsuleS1MaxForRetest: 6)));

        // CapsuleS2MaxOutside >= total units (60)
        Assert.Throws<InvalidOperationException>(() =>
            WeightVariationEvaluator.Evaluate(DosageForm.Tablet, units, new WeightVariationConfig(CapsuleS2MaxOutside: 60)));
    }
}
