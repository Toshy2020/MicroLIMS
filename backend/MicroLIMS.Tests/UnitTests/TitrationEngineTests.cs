using MicroLIMS.Domain.Enums;
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
    public void PrimaryStandard_FullPurity_NoBlank()
    {
        // 204.2 x 100% / (10.00 x 20.42) = 1.0
        Assert.Equal(1.0m, decimal.Round(TitrationEngine.PrimaryStandardFactor(204.2m, 100m, 10.00m, 0m, 20.42m), 6));
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

    // ---- Titration assay engine ----

    private static void Near(decimal expected, decimal actual, decimal tol = 0.0001m) =>
        Assert.True(Math.Abs(expected - actual) <= tol, $"expected {expected} actual {actual}");

    [Fact]
    public void Direct_AscorbicAcid_Iodine()
    {
        // (22.6 - 0.1) x 0.1 N x 1.0023 x 88.06 = 22.5 x 0.1 = 2.25; x 1.0023 = 2.255175; x 88.06 = 198.5907105 mg
        var mg = TitrationEngine.MgDirect(TitrationEngine.NetVolume(false, 22.6m, 0.1m), 0.1m, 1.0023m, 88.06m);
        Near(198.5907105m, mg);
        // W = 400 mg: 198.5907105 / 400 x 100 = 49.647677625 %
        Near(49.647677625m, TitrationEngine.ApplyBasis(mg, 400m, ResultBasis.PercentAsIs, null, null, null));
    }

    [Fact]
    public void Direct_CalciumEdta_Molar()
    {
        // 24.4 x 0.1 M x 0.998 x 40.08 = 2.44 x 0.998 = 2.43512; x 40.08 = 97.5996096 mg; W 100 mg -> 97.5996096 %
        var mg = TitrationEngine.MgDirect(24.4m, 0.1m, 0.998m, 40.08m);
        Near(97.5996096m, mg);
        Near(97.5996096m, TitrationEngine.ApplyBasis(mg, 100m, ResultBasis.PercentAsIs, null, null, null));
    }

    [Fact]
    public void Residual_WithBlank()
    {
        // blank 25.10, back titre 10.30: net 14.80 x 0.1 x 1.002 x 8.0 = 1.48 x 1.002 = 1.48296; x 8 = 11.86368 mg
        var net = TitrationEngine.NetVolume(true, 10.30m, 25.10m);
        Near(14.80m, net);
        Near(11.86368m, TitrationEngine.MgDirect(net, 0.1m, 1.002m, 8.0m));
    }

    [Fact]
    public void Residual_WithoutBlank()
    {
        // excess 25.00 x 0.1 x 1.001 = 2.5025 mEq; back 10.30 x 0.1 x 1.002 = 1.03206 mEq; diff 1.47044 x 8.0 = 11.76352 mg
        Near(11.76352m, TitrationEngine.MgResidualNoBlank(25.00m, 0.1m, 1.001m, 10.30m, 0.1m, 1.002m, 8.0m));
    }

    [Fact]
    public void Residual_BackTitreAboveExcess_Throws() =>
        Assert.Throws<InvalidOperationException>(() => TitrationEngine.MgResidualNoBlank(10m, 0.1m, 1m, 20m, 0.1m, 1m, 8m));

    [Fact]
    public void KarlFischer_WaterPercent()
    {
        // F_KF = N x f = 5.0 x 1.0024 = 5.012 mg/mL; (4.20 - 0.05) = 4.15 x 5.012 = 20.7998 mg; W 500 mg -> 4.15996 %
        var fKf = 5.0m * 1.0024m;
        Near(5.012m, fKf);
        var mg = TitrationEngine.MgDirect(4.15m, fKf, 1m, 1m);
        Near(20.7998m, mg);
        Near(4.15996m, TitrationEngine.ApplyBasis(mg, 500m, ResultBasis.PercentAsIs, null, null, null));
    }

    [Fact]
    public void Relative_IncludesPurityAndMoisture()
    {
        // K = 100 x 0.995 x 0.995 / (10.00 - 0.10) = 99.0025 / 9.90 = 10.00025253 mg/mL
        var k = TitrationEngine.StandardK(100m, 99.5m, 0.5m, TitrationEngine.NetVolume(false, 10.00m, 0.10m));
        Near(10.00025253m, k, 0.00000001m);
        // sample: (12.40 - 0.10) = 12.30 x K = 123.0031061 mg; W 250 -> 49.20124 %
        var mg = TitrationEngine.MgRelative(TitrationEngine.NetVolume(false, 12.40m, 0.10m), k);
        Near(123.0031061m, mg, 0.00001m);
        Near(49.2012424m, TitrationEngine.ApplyBasis(mg, 250m, ResultBasis.PercentAsIs, null, null, null), 0.0001m);
    }

    [Fact]
    public void Relative_ResidualUsesBlankMinusTitre()
    {
        // standard: 100 mg, pure, net = 20.00 - 10.00 = 10.00 -> K = 10; sample net = 20.00 - 12.00 = 8.00 -> 80 mg
        var k = TitrationEngine.StandardK(100m, 100m, 0m, TitrationEngine.NetVolume(true, 10.00m, 20.00m));
        Near(10m, k);
        Near(80m, TitrationEngine.MgRelative(TitrationEngine.NetVolume(true, 12.00m, 20.00m), k));
    }

    [Fact]
    public void TemperatureCorrection()
    {
        // 20.00 x (1 + (25 - 30) x 0.0011) = 20.00 x 0.9945 = 19.89
        Near(19.89m, TitrationEngine.CorrectVolume(20.00m, 25m, 30m, 0.0011m));
        // colder than standardization: 20.00 x (1 + (25 - 20) x 0.0011) = 20.11
        Near(20.11m, TitrationEngine.CorrectVolume(20.00m, 25m, 20m, 0.0011m));
    }

    [Fact]
    public void Bases()
    {
        const decimal mg = 198.5907105m; // W 400 mg -> 49.647677625 % as is
        Near(52.26071329m, TitrationEngine.ApplyBasis(mg, 400m, ResultBasis.PercentDriedBasis, 5m, null, null), 0.00001m); // /0.95
        Near(52.26071329m, TitrationEngine.ApplyBasis(mg, 400m, ResultBasis.PercentAnhydrousBasis, 5m, null, null), 0.00001m);
        // 0.49647677625 x 500 mg unit / 250 mg claim x 100 = 99.29535525 %
        Near(99.29535525m, TitrationEngine.ApplyBasis(mg, 400m, ResultBasis.PercentLabelClaim, null, 500m, 250m), 0.00001m);
        // 0.49647677625 x 500 = 248.238388125 mg/unit
        Near(248.238388125m, TitrationEngine.ApplyBasis(mg, 400m, ResultBasis.MgPerUnit, null, 500m, null), 0.00001m);
        Assert.Throws<InvalidOperationException>(() => TitrationEngine.ApplyBasis(mg, 400m, ResultBasis.PercentDriedBasis, null, null, null));
        Assert.Throws<InvalidOperationException>(() => TitrationEngine.ApplyBasis(mg, 400m, ResultBasis.MgPerUnit, null, null, null));
    }

    [Fact]
    public void Rsd_Rule()
    {
        // 99, 100, 101: mean 100, SD = sqrt((1+0+1)/2) = 1, RSD = 1.0 %
        var s = TitrationEngine.Summarize(new[] { 99m, 100m, 101m }, 0.5m);
        Near(100m, s.Mean);
        Near(1m, s.RsdPercent!.Value, 0.000001m);
        Assert.True(s.RsdExceeded);
        Assert.False(TitrationEngine.Summarize(new[] { 99m, 100m, 101m }, 1.5m).RsdExceeded);
        Assert.False(TitrationEngine.Summarize(new[] { 99m, 100m, 101m }, null).RsdExceeded);
        Assert.Null(TitrationEngine.Summarize(new[] { 99m }, 1m).RsdPercent);
    }

    [Fact]
    public void Guards()
    {
        Assert.Throws<InvalidOperationException>(() => TitrationEngine.NetVolume(false, 0.1m, 0.1m));
        Assert.Throws<InvalidOperationException>(() => TitrationEngine.NetVolume(true, 5m, 5m));
        Assert.Throws<InvalidOperationException>(() => TitrationEngine.MgDirect(1m, 0.1m, 0m, 88m));
        Assert.Throws<InvalidOperationException>(() => TitrationEngine.StandardK(0m, 100m, 0m, 1m));
    }
}
