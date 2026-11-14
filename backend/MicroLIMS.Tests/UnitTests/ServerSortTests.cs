using MicroLIMS.Application.Helpers;
using MicroLIMS.Shared.Exceptions;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

// Pure ordering rules of the server-side sort helper. LINQ to Objects keeps ties in input order,
// so rows are given out of Id order on purpose: only the Id tie-breaker puts them back.
public class ServerSortTests
{
    private sealed record Row(int Id, string Key, int Group);

    private static readonly Row[] Rows =
    {
        new(3, "b", 0), new(1, "b", 0), new(5, "a", 1), new(2, "b", 0), new(4, "a", 0)
    };

    [Theory]
    [InlineData(true, new[] { 4, 5, 1, 2, 3 })]
    [InlineData(false, new[] { 1, 2, 3, 4, 5 })]
    public void ApplyOrder_SortsByKey_ThenAscendingId_InBothDirections(bool ascending, int[] expected)
    {
        var ids = ServerSort.ApplyOrder(Rows.AsQueryable(), r => r.Key, ascending, r => r.Id).Select(r => r.Id).ToArray();
        Assert.Equal(expected, ids);
    }

    [Fact]
    public void ApplyOrderWithGroup_PutsGroupZeroFirst_InBothDirections()
    {
        var asc = ServerSort.ApplyOrderWithGroup(Rows.AsQueryable(), r => r.Group, r => r.Key, true, r => r.Id).Select(r => r.Id);
        var desc = ServerSort.ApplyOrderWithGroup(Rows.AsQueryable(), r => r.Group, r => r.Key, false, r => r.Id).Select(r => r.Id);
        Assert.Equal(new[] { 4, 1, 2, 3, 5 }, asc);
        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, desc);
    }

    [Theory]
    [InlineData(null, false, true)]
    [InlineData(null, true, false)]
    [InlineData("ASC", true, true)]
    [InlineData(" desc ", false, false)]
    public void ParseAscending_ReadsDirection_OrFallsBack(string? dir, bool fallbackDescending, bool expected) =>
        Assert.Equal(expected, ServerSort.ParseAscending(dir, fallbackDescending));

    [Fact]
    public void ParseAscending_UnknownDirection_Refused() =>
        Assert.Throws<BusinessRuleException>(() => ServerSort.ParseAscending("sideways"));
}
