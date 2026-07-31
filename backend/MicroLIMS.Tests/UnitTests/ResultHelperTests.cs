using MicroLIMS.Shared.Helpers;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class ResultHelperTests
{
    [Theory]
    [InlineData(12.4, 12)]
    [InlineData(12.5, 13)]
    [InlineData(12.6, 13)]
    public void ToWholeNumber_RoundsAwayFromZero(decimal input, int expected)
    {
        Assert.Equal(expected, ResultHelper.ToWholeNumber(input));
    }
}
