using System.Text.Json;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class GravimetricCalculatorTests
{
    private static Specification CreateNmtSpec(decimal upperLimit, string? unit = "%") => new()
    {
        LimitType = LimitType.NotMoreThan,
        UpperLimit = upperLimit,
        Unit = unit ?? string.Empty
    };

    [Fact]
    public void WorkedExample_LodLossWithTare_CalculatesExpectedValues()
    {
        // Worked example (LOD): container 25.1234 g, initial 27.1234 g (W1 = 2.0000),
        // final 27.0334 g (W2 = 1.9100) -> loss 4.50 %, NMT 5.0 -> WithinLimits.
        var replicates = new List<GravimetricReplicateInput>
        {
            new(Container: 25.1234m, Initial: 27.1234m, Final: 27.0334m)
        };
        var spec = CreateNmtSpec(5.0m);

        var result = GravimetricCalculator.Calculate(replicates, EquationType.GravimetricLoss, spec);

        Assert.Equal(1, result.N);
        Assert.Equal(4.50m, result.Mean);
        Assert.Equal(4.50m, result.ReportedValue);
        Assert.Equal("4.50 %", result.ReportedDisplay);
        Assert.Equal("WithinLimits", result.ComparisonStatus);

        Assert.Single(result.Replicates);
        var rep = result.Replicates[0];
        Assert.Equal(25.1234m, rep.Container);
        Assert.Equal(27.1234m, rep.Initial);
        Assert.Equal(27.0334m, rep.Final);
        Assert.Equal(2.0000m, rep.W1);
        Assert.Equal(1.9100m, rep.W2);
        Assert.Equal(4.50m, rep.Percent);

        using var doc = JsonDocument.Parse(result.CalculationJson);
        Assert.Equal("GravimetricLoss", doc.RootElement.GetProperty("mode").GetString());
        Assert.Equal(4.50m, doc.RootElement.GetProperty("mean").GetDecimal());
        Assert.Equal(1, doc.RootElement.GetProperty("n").GetInt32());
        var repsJson = doc.RootElement.GetProperty("replicates");
        Assert.Equal(1, repsJson.GetArrayLength());
        Assert.Equal(2.0000m, repsJson[0].GetProperty("w1").GetDecimal());
        Assert.Equal(1.9100m, repsJson[0].GetProperty("w2").GetDecimal());
        Assert.Equal(4.50m, repsJson[0].GetProperty("percent").GetDecimal());
    }

    [Fact]
    public void WorkedExample_ResidueNoTare_CalculatesExpectedValues()
    {
        // Worked example (Residue): no tare, initial 1.0000, final 0.0020 -> 0.20 %.
        var replicates = new List<GravimetricReplicateInput>
        {
            new(Container: null, Initial: 1.0000m, Final: 0.0020m)
        };
        var spec = CreateNmtSpec(0.50m);

        var result = GravimetricCalculator.Calculate(replicates, EquationType.GravimetricResidue, spec);

        Assert.Equal(1, result.N);
        Assert.Equal(0.20m, result.Mean);
        Assert.Equal(0.20m, result.ReportedValue);
        Assert.Equal("0.20 %", result.ReportedDisplay);
        Assert.Equal("WithinLimits", result.ComparisonStatus);

        Assert.Single(result.Replicates);
        var rep = result.Replicates[0];
        Assert.Null(rep.Container);
        Assert.Equal(1.0000m, rep.Initial);
        Assert.Equal(0.0020m, rep.Final);
        Assert.Equal(1.0000m, rep.W1);
        Assert.Equal(0.0020m, rep.W2);
        Assert.Equal(0.20m, rep.Percent);

        using var doc = JsonDocument.Parse(result.CalculationJson);
        Assert.Equal("GravimetricResidue", doc.RootElement.GetProperty("mode").GetString());
        Assert.Equal(0.20m, doc.RootElement.GetProperty("mean").GetDecimal());
        Assert.Equal(1, doc.RootElement.GetProperty("n").GetInt32());
    }

    [Theory]
    [InlineData(EquationType.GravimetricLoss)]
    [InlineData(EquationType.GravimetricResidue)]
    public void FinalGreaterThanInitial_W2GreaterThanW1_Throws(EquationType mode)
    {
        var replicates = new List<GravimetricReplicateInput>
        {
            new(Container: 10.0m, Initial: 12.0m, Final: 12.5m)
        };
        var spec = CreateNmtSpec(5.0m);

        var ex = Assert.Throws<InvalidOperationException>(() => GravimetricCalculator.Calculate(replicates, mode, spec));
        Assert.Contains("Final weight (W2) cannot be greater than initial weight (W1)", ex.Message);
    }

    [Theory]
    [InlineData(EquationType.GravimetricLoss)]
    [InlineData(EquationType.GravimetricResidue)]
    public void InitialSampleWeightZeroOrNegative_W1ZeroOrNegative_Throws(EquationType mode)
    {
        // Container == Initial -> W1 = 0
        var zeroReplicates = new List<GravimetricReplicateInput>
        {
            new(Container: 10.0m, Initial: 10.0m, Final: 10.0m)
        };
        var spec = CreateNmtSpec(5.0m);

        var exZero = Assert.Throws<InvalidOperationException>(() => GravimetricCalculator.Calculate(zeroReplicates, mode, spec));
        Assert.Contains("Initial sample weight (W1) must be greater than zero", exZero.Message);

        // Initial < Container -> W1 < 0
        var negReplicates = new List<GravimetricReplicateInput>
        {
            new(Container: 10.0m, Initial: 9.5m, Final: 9.0m)
        };
        var exNeg = Assert.Throws<InvalidOperationException>(() => GravimetricCalculator.Calculate(negReplicates, mode, spec));
        Assert.Contains("Initial sample weight (W1) must be greater than zero", exNeg.Message);
    }

    [Fact]
    public void NegativeFinalWeight_W2Negative_Throws()
    {
        // Final < Container -> W2 < 0
        var replicates = new List<GravimetricReplicateInput>
        {
            new(Container: 10.0m, Initial: 12.0m, Final: 9.5m)
        };
        var spec = CreateNmtSpec(5.0m);

        var ex = Assert.Throws<InvalidOperationException>(() => GravimetricCalculator.Calculate(replicates, EquationType.GravimetricLoss, spec));
        Assert.Contains("Final weight (W2) cannot be negative", ex.Message);
    }

    [Fact]
    public void Boundary_Target5Point0Nmt_EvaluatedUnrounded()
    {
        var spec = CreateNmtSpec(5.0m);

        // 5.000 % exactly on NMT 5.0 -> WithinLimits
        // W1 = 100, W2 = 95 -> Loss = 5.000%
        var repExact = new List<GravimetricReplicateInput>
        {
            new(Container: null, Initial: 100.0m, Final: 95.0m)
        };
        var resExact = GravimetricCalculator.Calculate(repExact, EquationType.GravimetricLoss, spec);
        Assert.Equal("WithinLimits", resExact.ComparisonStatus);
        Assert.Equal("5.00 %", resExact.ReportedDisplay);

        // 5.0000001 % -> OutOfSpecification (unrounded comparison)
        var repOver = new List<GravimetricReplicateInput>
        {
            new(Container: null, Initial: 100.0m, Final: 94.9999999m)
        };
        var resOver = GravimetricCalculator.Calculate(repOver, EquationType.GravimetricLoss, spec);
        Assert.Equal("OutOfSpecification", resOver.ComparisonStatus);
    }

    [Fact]
    public void MultipleReplicates_CalculatesMean()
    {
        // Replicate 1: loss 4.0%, Replicate 2: loss 5.0% -> mean 4.5%
        var replicates = new List<GravimetricReplicateInput>
        {
            new(Container: null, Initial: 100.0m, Final: 96.0m),
            new(Container: null, Initial: 100.0m, Final: 95.0m)
        };
        var spec = CreateNmtSpec(5.0m);

        var result = GravimetricCalculator.Calculate(replicates, EquationType.GravimetricLoss, spec);

        Assert.Equal(2, result.N);
        Assert.Equal(4.50m, result.Mean);
        Assert.Equal("4.50 %", result.ReportedDisplay);
        Assert.Equal("WithinLimits", result.ComparisonStatus);
    }

    [Fact]
    public void FormatReportedDisplay_FormatsAtLeastTwoDecimalsOrLimitsDecimals()
    {
        // NMT 5 (0 dp) -> at least 2 dp
        var spec0 = CreateNmtSpec(5m);
        Assert.Equal("4.50 %", GravimetricCalculator.FormatReportedDisplay(4.5m, spec0));

        // NMT 5.000 (3 dp) -> 3 dp
        var spec3 = CreateNmtSpec(5.000m);
        Assert.Equal("4.500 %", GravimetricCalculator.FormatReportedDisplay(4.5m, spec3));
    }
}
