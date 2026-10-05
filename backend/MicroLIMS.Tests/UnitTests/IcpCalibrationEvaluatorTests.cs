using Xunit;
using MicroLIMS.Application.Helpers;

namespace MicroLIMS.Tests.UnitTests;

public class IcpCalibrationEvaluatorTests
{
    private static readonly IcpCheckLimits Limits = new(0.999m, true, 0.01m, true, 1m, 90m, 110m);
    private static IcpCalibrationElementInput In(decimal? r, decimal? blank = 0.005m, decimal? icv = 1.0m) => new("Zn", r, blank, icv);

    [Fact]
    public void Evaluate_AllChecksPass()
    {
        var v = IcpCalibrationEvaluator.Evaluate(In(0.9995m, 0.005m, 0.95m), Limits);
        Assert.True(v.Passed);
        Assert.Empty(v.FailureReasons);
        Assert.Equal(95m, v.IcvRecoveryPercent);
    }

    [Fact]
    public void Evaluate_RBelowMin_Fails()
    {
        var zn = IcpCalibrationEvaluator.Evaluate(In(0.996383m), Limits);
        Assert.False(zn.Passed);
        Assert.Equal("r 0.996383 is below the minimum 0.999.", Assert.Single(zn.FailureReasons));
        Assert.True(IcpCalibrationEvaluator.Evaluate(In(0.999386m), Limits).Passed);
    }

    [Fact]
    public void Evaluate_RExactlyMin_Passes() =>
        Assert.True(IcpCalibrationEvaluator.Evaluate(In(0.999m), Limits).Passed);

    [Fact]
    public void Evaluate_BlankOverLimit_Fails()
    {
        var v = IcpCalibrationEvaluator.Evaluate(In(0.9995m, 0.02m), Limits);
        Assert.False(v.Passed);
        Assert.Equal("Blank 0.02 mg/L is above the limit 0.01 mg/L.", Assert.Single(v.FailureReasons));
        Assert.True(IcpCalibrationEvaluator.Evaluate(In(0.9995m, 0.01m), Limits).Passed);
    }

    [Fact]
    public void Evaluate_IcvOutOfRange_Fails()
    {
        var v = IcpCalibrationEvaluator.Evaluate(In(0.9995m, icv: 0.875m), Limits);
        Assert.False(v.Passed);
        Assert.Equal(87.5m, v.IcvRecoveryPercent);
        Assert.Equal("ICV recovery 87.5% is outside 90-110%.", Assert.Single(v.FailureReasons));
    }

    [Fact]
    public void Evaluate_ChecksOff_IgnoresBlankAndIcv()
    {
        var off = Limits with { RequireBlank = false, RequireIcv = false };
        Assert.True(IcpCalibrationEvaluator.Evaluate(In(0.9995m, 99m, 99m), off).Passed);
        Assert.True(IcpCalibrationEvaluator.Evaluate(In(0.9995m, null, null), off).Passed);
    }

    [Fact]
    public void Evaluate_MissingR_Fails()
    {
        var v = IcpCalibrationEvaluator.Evaluate(In(null), Limits);
        Assert.False(v.Passed);
        Assert.Equal("r is required.", Assert.Single(v.FailureReasons));
        var m = IcpCalibrationEvaluator.Evaluate(In(0.9995m, null, null), Limits);
        Assert.Equal(new[] { "Blank is required.", "ICV result is required." }, m.FailureReasons);
    }

    [Fact]
    public void EvaluateCcv_Boundaries()
    {
        Assert.True(IcpCalibrationEvaluator.EvaluateCcv(0.9m, 1m, 90m, 110m).Passed);
        Assert.True(IcpCalibrationEvaluator.EvaluateCcv(1.1m, 1m, 90m, 110m).Passed);
        Assert.False(IcpCalibrationEvaluator.EvaluateCcv(0.89m, 1m, 90m, 110m).Passed);
        Assert.False(IcpCalibrationEvaluator.EvaluateCcv(1.11m, 1m, 90m, 110m).Passed);
        Assert.Equal(100m, IcpCalibrationEvaluator.EvaluateCcv(2m, 2m, 90m, 110m).RecoveryPercent);
    }
}
