using MicroLIMS.Application.Helpers;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

// CLAUDE.md Rule 5.4: every list endpoint caps its page size at 100.
public class PagingTests
{
    [Theory]
    [InlineData(1, 25, 1, 25)]       // ordinary request
    [InlineData(3, 100, 3, 100)]     // exactly the cap
    [InlineData(2, 101, 2, 100)]     // just above the cap
    [InlineData(1, 10_000, 1, 100)]  // an attempt to pull everything
    [InlineData(0, 25, 1, 25)]       // page below 1
    [InlineData(-5, 25, 1, 25)]
    [InlineData(1, 0, 1, 50)]        // not given -> the endpoint's default
    [InlineData(1, -1, 1, 50)]
    public void Normalize_FloorsPage_DefaultsAndCapsPageSize(int page, int pageSize, int expectedPage, int expectedSize)
    {
        Assert.Equal((expectedPage, expectedSize), Paging.Normalize(page, pageSize, defaultPageSize: 50));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void Normalize_RejectsADefaultOutsideTheCap(int defaultPageSize)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Paging.Normalize(1, 10, defaultPageSize));
    }
}
