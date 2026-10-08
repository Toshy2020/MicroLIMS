using MicroLIMS.Application.Services.MasterDataWorkbook;
using MicroLIMS.Domain.Enums;
using Xunit;

namespace MicroLIMS.Tests.UnitTests.MasterDataWorkbook;

public class RowReaderTests
{
    private static RowReader Reader(params (string Col, string Val)[] cells) =>
        new(new WorkbookRow(7, cells.ToDictionary(c => c.Col, c => c.Val, StringComparer.OrdinalIgnoreCase)));

    [Fact]
    public void Text_Missing_ThrowsWithColumn()
    {
        var e = Assert.Throws<RowError>(() => Reader(("Name", "  ")).Text("Name"));
        Assert.Equal("Name", e.Column);
        Assert.Contains("required", e.Message);
    }

    [Fact]
    public void OptionalText_BlankIsNull() => Assert.Null(Reader(("Grade", "")).OptionalText("Grade"));

    [Fact]
    public void Decimal_Dot_Parses() => Assert.Equal(0.25m, Reader(("Upper", "0.25")).Decimal("Upper"));

    [Fact]
    public void Decimal_WithComma_IsRejected()
    {
        var e = Assert.Throws<RowError>(() => Reader(("Upper", "1,5")).Decimal("Upper"));
        Assert.Equal("Upper", e.Column);
        Assert.Contains("dot", e.Message);
    }

    [Theory]
    [InlineData("Yes", true)] [InlineData("no", false)] [InlineData("TRUE", true)] [InlineData("False", false)]
    public void Bool_AcceptsYesNoTrueFalse(string text, bool expected) =>
        Assert.Equal(expected, Reader(("Flag", text)).Bool("Flag"));

    [Fact]
    public void Enum_ByNameCaseInsensitive() =>
        Assert.Equal(LimitType.NotMoreThan, Reader(("LimitType", "notmorethan")).Enum<LimitType>("LimitType"));

    [Fact]
    public void Enum_Number_IsRejectedAndListsNames()
    {
        var e = Assert.Throws<RowError>(() => Reader(("LimitType", "2")).Enum<LimitType>("LimitType"));
        Assert.Contains("NotMoreThan", e.Message);
    }

    [Fact]
    public void List_SplitsOnSemicolonAndTrims() =>
        Assert.Equal(new[] { "TAMC", "TYMC" }, Reader(("TestCodes", " TAMC ;; TYMC ")).List("TestCodes"));

    [Fact]
    public void Date_IsoParsesAsUtc()
    {
        var d = Reader(("Due", "2027-03-31")).Date("Due");
        Assert.Equal(new DateTime(2027, 3, 31), d!.Value.Date);
        Assert.Equal(DateTimeKind.Utc, d.Value.Kind);
    }

    [Fact]
    public void Date_OtherFormat_IsRejected() =>
        Assert.Throws<RowError>(() => Reader(("Due", "31/03/2027")).Date("Due"));
}
