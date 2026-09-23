using MicroLIMS.Application.Helpers;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class CalibrationStandardLevelsHelperTests
{
    [Fact]
    public void ParseAndValidate_UnorderedLevels_SortsAndNormalizes()
    {
        var (levels, normalized) = CalibrationStandardLevelsHelper.ParseAndValidate("5,1");

        Assert.Equal(new List<decimal> { 1m, 5m }, levels);
        Assert.Equal("1, 5", normalized);
    }

    [Fact]
    public void ParseAndValidate_AlreadyNormalizedSpacedInput_RoundTrips()
    {
        var (levels, normalized) = CalibrationStandardLevelsHelper.ParseAndValidate("1, 5");

        Assert.Equal(new List<decimal> { 1m, 5m }, levels);
        Assert.Equal("1, 5", normalized);
    }

    [Fact]
    public void ParseAndValidate_SingleLevel_Rejected()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => CalibrationStandardLevelsHelper.ParseAndValidate("1"));
        Assert.Contains("At least two standard levels", ex.Message);
    }

    [Fact]
    public void ParseAndValidate_ZeroLevel_Rejected()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => CalibrationStandardLevelsHelper.ParseAndValidate("0, 5"));
        Assert.Contains("must be greater than zero", ex.Message);
    }

    [Fact]
    public void ParseAndValidate_DuplicateLevels_Rejected()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => CalibrationStandardLevelsHelper.ParseAndValidate("1, 1"));
        Assert.Contains("must be distinct", ex.Message);
    }

    [Fact]
    public void ParseAndValidate_NonNumericLevel_Rejected()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => CalibrationStandardLevelsHelper.ParseAndValidate("abc, 5"));
        Assert.Contains("is not a valid standard level", ex.Message);
    }

    [Fact]
    public void ParseAndValidate_EmptyString_Rejected()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => CalibrationStandardLevelsHelper.ParseAndValidate(""));
        Assert.Contains("cannot be empty", ex.Message);
    }
}
