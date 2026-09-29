using MicroLIMS.Application.Helpers;
using MicroLIMS.Domain.Enums;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class SolutionPreparationCodeTests
{
    [Fact]
    public void Head_MobilePhase_WithAbbreviation_PrefixesAbbreviation()
    {
        var head = SolutionPreparationCode.Head(SolutionType.MobilePhase, "VIT-C");

        Assert.Equal("MP-VIT-C ", head);
    }

    [Fact]
    public void Head_MobilePhase_WithoutAbbreviation_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => SolutionPreparationCode.Head(SolutionType.MobilePhase, null));
        Assert.Throws<InvalidOperationException>(() => SolutionPreparationCode.Head(SolutionType.MobilePhase, "  "));
    }

    [Fact]
    public void Head_Diluent_HasNoAbbreviation()
    {
        Assert.Equal("DL-", SolutionPreparationCode.Head(SolutionType.Diluent, null));
    }

    [Fact]
    public void Head_Titrant_HasNoAbbreviation()
    {
        Assert.Equal("VS-", SolutionPreparationCode.Head(SolutionType.Titrant, null));
    }

    [Fact]
    public async Task NextAsync_FirstOfYear_Is01()
    {
        var next = await SolutionPreparationCode.NextAsync(
            new List<string>().AsQueryable(), "MP-VIT-C ", new DateTime(2026, 9, 1));

        Assert.Equal("MP-VIT-C 01/09/2026", next);
    }

    [Fact]
    public async Task NextAsync_ContinuesAfter02()
    {
        var issued = new List<string> { "MP-VIT-C 01/09/2026", "MP-VIT-C 02/09/2026" }.AsQueryable();

        var next = await SolutionPreparationCode.NextAsync(issued, "MP-VIT-C ", new DateTime(2026, 9, 15));

        Assert.Equal("MP-VIT-C 03/09/2026", next);
    }

    [Fact]
    public async Task NextAsync_IgnoresOtherYear()
    {
        var issued = new List<string> { "MP-VIT-C 05/12/2025" }.AsQueryable();

        var next = await SolutionPreparationCode.NextAsync(issued, "MP-VIT-C ", new DateTime(2026, 1, 1));

        Assert.Equal("MP-VIT-C 01/01/2026", next);
    }

    [Fact]
    public async Task NextAsync_IgnoresOtherHead()
    {
        var issued = new List<string> { "MP-VIT-B 01/09/2026", "MP-VIT-B 02/09/2026" }.AsQueryable();

        var next = await SolutionPreparationCode.NextAsync(issued, "MP-VIT-C ", new DateTime(2026, 9, 1));

        Assert.Equal("MP-VIT-C 01/09/2026", next);
    }

    [Fact]
    public async Task NextAsync_DiluentAndTitrantSeriesAreSeparate()
    {
        var issued = new List<string> { "DL-01/09/2026", "VS-01/09/2026", "VS-02/09/2026" }.AsQueryable();

        var nextDl = await SolutionPreparationCode.NextAsync(issued, "DL-", new DateTime(2026, 9, 1));
        var nextVs = await SolutionPreparationCode.NextAsync(issued, "VS-", new DateTime(2026, 9, 1));

        Assert.Equal("DL-02/09/2026", nextDl);
        Assert.Equal("VS-03/09/2026", nextVs);
    }

    [Fact]
    public async Task NextAsync_FormatsWithSlashes()
    {
        var next = await SolutionPreparationCode.NextAsync(
            new List<string>().AsQueryable(), "MP-VIT-C ", new DateTime(2026, 9, 5));

        Assert.Equal("MP-VIT-C 01/09/2026", next);
    }
}
