using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

// Pure ICP maths: assay %, the calibration r check and the sample evaluator.
public class IcpCalculationTests
{
    [Fact]
    public void Assay_Solid_MatchesTheFormula()
    {
        // 9.5 x 0.5 x 100 / (10 x 0.5)
        Assert.Equal(95m, IcpContentCalculator.AssayPercent(9.5m, 10m, 0.5m, null, 0.5m));
    }

    [Fact]
    public void Assay_Liquid_MultipliesBySpecificGravity()
    {
        // 10 x 10 x 1.2 x 100 / (10 x 12)
        Assert.Equal(100m, IcpContentCalculator.AssayPercent(10m, 10m, 10m, 1.2m, 12m));
    }

    [Theory]
    [InlineData(0, 0.5, 0.5)]   // target
    [InlineData(10, 0, 0.5)]    // theoretical amount
    [InlineData(10, 0.5, 0)]    // actual weight
    public void Assay_RejectsZeroDenominatorsAndAmounts(double target, double theoretical, double actual)
    {
        Assert.Throws<BusinessRuleException>(() =>
            IcpContentCalculator.AssayPercent(1m, (decimal)target, (decimal)theoretical, null, (decimal)actual));
    }

    [Theory]
    [InlineData("0.999", true)]    // boundary inclusive
    [InlineData("0.9995", true)]
    [InlineData("1", true)]
    [InlineData("0.9989", false)]
    public void Calibration_RIsJudgedAgainstTheFixedMinimum(string r, bool passes)
    {
        var v = IcpCalibrationEvaluator.Evaluate("Zn", decimal.Parse(r, System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(passes, v.Passed);
        Assert.Equal(passes, v.FailureReason is null);
    }

    [Fact]
    public void Calibration_MissingR_Fails()
    {
        var v = IcpCalibrationEvaluator.Evaluate("Zn", null);
        Assert.False(v.Passed);
        Assert.Equal("r is required.", v.FailureReason);
    }

    private static TestDefinition Definition(IcpTestMode mode) => new()
    {
        Code = "T", IcpMode = mode, IcpSampleForm = mode == IcpTestMode.Assay ? IcpSampleForm.Solid : null,
        IcpTheoreticalAmount = mode == IcpTestMode.Assay ? 0.5m : null,
        IcpElements = { new TestDefinitionIcpElement { Id = 7, Symbol = "Zn", TargetPpm = mode == IcpTestMode.Assay ? 10m : null } },
    };

    private static Specification Spec(LimitType limit = LimitType.Range) => new()
    {
        ParameterName = "Zn", IcpTestElementId = 7, LimitType = limit,
        LowerLimit = limit == LimitType.Range ? 90m : null, UpperLimit = limit == LimitType.Range ? 110m : 5m,
    };

    private static List<IcpElementStateDto> Valid() => new() { new IcpElementStateDto(7, "Zn", true, null) };
    private static List<IcpSampleConcentration> Conc(decimal ppm) => new() { new() { IcpTestElementId = 7, ResultPpm = ppm } };

    [Fact]
    public void Evaluator_Assay_RoundsDisplayHalfAwayFromZero_AndJudgesTheRange()
    {
        var row = Assert.Single(IcpSampleEvaluator.Evaluate(Definition(IcpTestMode.Assay), Valid(), new[] { Spec() }, Conc(9.55m), 0.5m));
        Assert.Equal(95.5m, row.Value);
        Assert.Equal("95.5 %", row.Display);
        Assert.Equal("%", row.Unit);
        Assert.Equal(ResultStatus.WithinLimits, row.Status);
    }

    [Fact]
    public void Evaluator_Assay_NoWeight_IsAProblem()
    {
        var row = Assert.Single(IcpSampleEvaluator.Evaluate(Definition(IcpTestMode.Assay), Valid(), new[] { Spec() }, Conc(10m), null));
        Assert.Equal("Enter the actual weight of the sample.", row.Problem);
        Assert.Null(row.Status);
    }

    [Fact]
    public void Evaluator_NoResultForElement_IsAProblem()
    {
        var row = Assert.Single(IcpSampleEvaluator.Evaluate(Definition(IcpTestMode.Assay), Valid(), new[] { Spec() }, new List<IcpSampleConcentration>(), 0.5m));
        Assert.Equal("Zn: enter the result from the instrument report.", row.Problem);
    }

    [Fact]
    public void Evaluator_InvalidElement_ReportsTheReason()
    {
        var states = new List<IcpElementStateDto> { new(7, "Zn", false, "Calibration is not confirmed.") };
        var row = Assert.Single(IcpSampleEvaluator.Evaluate(Definition(IcpTestMode.Assay), states, new[] { Spec() }, Conc(10m), 0.5m));
        Assert.Equal("Zn: Calibration is not confirmed.", row.Problem);
    }

    [Fact]
    public void Evaluator_Impurity_UsesThePpmAsIs()
    {
        var row = Assert.Single(IcpSampleEvaluator.Evaluate(Definition(IcpTestMode.Impurity), Valid(), new[] { Spec(LimitType.NotMoreThan) }, Conc(4.5m), null));
        Assert.Equal(4.5m, row.Value);
        Assert.Equal("4.5 ppm", row.Display);
        Assert.Equal(ResultStatus.WithinLimits, row.Status);
    }

    [Fact]
    public void Evaluator_SpecForForeignElement_Throws()
    {
        var spec = Spec();
        spec.IcpTestElementId = 99;
        Assert.Throws<BusinessRuleException>(() =>
            IcpSampleEvaluator.Evaluate(Definition(IcpTestMode.Assay), Valid(), new[] { spec }, Conc(10m), 0.5m));
    }
}
