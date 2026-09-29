using MicroLIMS.Application.Helpers;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class HplcSstEvaluatorTests
{
    private static SstCriteria AllCriteria(int injections = 3) => new(
        MaxRsdPercent: 0.5m,
        MinResolution: 2.0m,
        MaxTailingFactor: 2.0m,
        MinTheoreticalPlates: 2000m,
        MinRetentionFactor: 2.0m,
        MinSignalToNoise: 10m,
        MinPeakToValley: 1.5m,
        StandardInjections: injections);

    private static SstEntered Entered(
        decimal[] responses,
        decimal? reportedRsd = 0.20m,
        decimal? resolution = 2.5m,
        decimal? tailing = 1.2m,
        decimal? plates = 3000m,
        decimal? retention = 3.0m,
        decimal? signalToNoise = 20m,
        decimal? peakToValley = 2.0m) =>
        new(responses, reportedRsd, resolution, tailing, plates, retention, signalToNoise, peakToValley);

    [Fact]
    public void Evaluate_AllCriteriaMet_Passes()
    {
        // 1000, 1000, 1002 -> mean 1000.6667, RSD ~0.11% (below 0.5% max)
        var outcome = HplcSstEvaluator.Evaluate("Ascorbic Acid", AllCriteria(), Entered(new decimal[] { 1000m, 1000m, 1002m }));

        Assert.True(outcome.Passed);
        Assert.Empty(outcome.FailureReasons);
        Assert.Equal(1000.6667m, decimal.Round(outcome.MeanResponse, 4));
        Assert.NotNull(outcome.ComputedRsdPercent);
        Assert.True(outcome.ComputedRsdPercent!.Value < 0.5m);
    }

    [Fact]
    public void Evaluate_WrongInjectionCount_Fails()
    {
        var outcome = HplcSstEvaluator.Evaluate("Ascorbic Acid", AllCriteria(3), Entered(new decimal[] { 1000m, 1000m }));

        Assert.False(outcome.Passed);
        Assert.Contains("Ascorbic Acid: 3 injections required, 2 entered.", outcome.FailureReasons);
    }

    [Fact]
    public void Evaluate_NonPositiveResponse_Fails()
    {
        var outcome = HplcSstEvaluator.Evaluate("Ascorbic Acid", AllCriteria(), Entered(new decimal[] { 1000m, 0m, 1002m }));

        Assert.False(outcome.Passed);
        Assert.Contains("Ascorbic Acid: injection 2 response must be greater than zero.", outcome.FailureReasons);
    }

    [Fact]
    public void Evaluate_RsdGate_UsesComputedNotReported()
    {
        // responses 1000, 1000, 1020 -> mean 1006.6667, RSD ~1.15% (exceeds 0.5% max)
        // reported RSD entered as 0.30% (would pass on its own) - gate must ignore it
        var outcome = HplcSstEvaluator.Evaluate(
            "Ascorbic Acid",
            AllCriteria(),
            Entered(new decimal[] { 1000m, 1000m, 1020m }, reportedRsd: 0.30m));

        Assert.False(outcome.Passed);
        Assert.Equal(1.15m, decimal.Round(outcome.ComputedRsdPercent!.Value, 2));
        Assert.Contains("Ascorbic Acid: RSD 1.15% exceeds 0.50%.", outcome.FailureReasons);
    }

    [Fact]
    public void Evaluate_ResolutionBelowMin_Fails()
    {
        var outcome = HplcSstEvaluator.Evaluate(
            "Ascorbic Acid",
            AllCriteria(),
            Entered(new decimal[] { 1000m, 1000m, 1002m }, resolution: 1.8m));

        Assert.False(outcome.Passed);
        Assert.Contains("Ascorbic Acid: resolution 1.8 is below 2.0.", outcome.FailureReasons);
    }

    [Fact]
    public void Evaluate_TailingFactorAboveMax_Fails()
    {
        var outcome = HplcSstEvaluator.Evaluate(
            "Ascorbic Acid",
            AllCriteria(),
            Entered(new decimal[] { 1000m, 1000m, 1002m }, tailing: 2.3m));

        Assert.False(outcome.Passed);
        Assert.Contains("Ascorbic Acid: tailing factor 2.3 is above 2.0.", outcome.FailureReasons);
    }

    [Fact]
    public void Evaluate_TheoreticalPlatesBelowMin_Fails()
    {
        var outcome = HplcSstEvaluator.Evaluate(
            "Ascorbic Acid",
            AllCriteria(),
            Entered(new decimal[] { 1000m, 1000m, 1002m }, plates: 1500m));

        Assert.False(outcome.Passed);
        Assert.Contains("Ascorbic Acid: theoretical plates 1500.0 is below 2000.0.", outcome.FailureReasons);
    }

    [Fact]
    public void Evaluate_RetentionFactorBelowMin_Fails()
    {
        var outcome = HplcSstEvaluator.Evaluate(
            "Ascorbic Acid",
            AllCriteria(),
            Entered(new decimal[] { 1000m, 1000m, 1002m }, retention: 1.5m));

        Assert.False(outcome.Passed);
        Assert.Contains("Ascorbic Acid: retention factor 1.5 is below 2.0.", outcome.FailureReasons);
    }

    [Fact]
    public void Evaluate_SignalToNoiseBelowMin_Fails()
    {
        var outcome = HplcSstEvaluator.Evaluate(
            "Ascorbic Acid",
            AllCriteria(),
            Entered(new decimal[] { 1000m, 1000m, 1002m }, signalToNoise: 8m));

        Assert.False(outcome.Passed);
        Assert.Contains("Ascorbic Acid: signal to noise 8.0 is below 10.0.", outcome.FailureReasons);
    }

    [Fact]
    public void Evaluate_PeakToValleyBelowMin_Fails()
    {
        var outcome = HplcSstEvaluator.Evaluate(
            "Ascorbic Acid",
            AllCriteria(),
            Entered(new decimal[] { 1000m, 1000m, 1002m }, peakToValley: 1.2m));

        Assert.False(outcome.Passed);
        Assert.Contains("Ascorbic Acid: peak to valley 1.2 is below 1.5.", outcome.FailureReasons);
    }

    [Fact]
    public void Evaluate_MissingCriterionValue_Fails()
    {
        var outcome = HplcSstEvaluator.Evaluate(
            "Ascorbic Acid",
            AllCriteria(),
            Entered(new decimal[] { 1000m, 1000m, 1002m }, resolution: null));

        Assert.False(outcome.Passed);
        Assert.Contains("Ascorbic Acid: resolution is required.", outcome.FailureReasons);
    }

    [Fact]
    public void Evaluate_CriterionNull_SkipsCheck()
    {
        // No limits set at all except injection count -> passes regardless of entered values
        var criteria = new SstCriteria(null, null, null, null, null, null, null, 1);
        var outcome = HplcSstEvaluator.Evaluate(
            "Ascorbic Acid",
            criteria,
            new SstEntered(new decimal[] { 1000m }, null, null, null, null, null, null, null));

        Assert.True(outcome.Passed);
        Assert.Empty(outcome.FailureReasons);
        Assert.Null(outcome.ComputedRsdPercent);
    }
}
