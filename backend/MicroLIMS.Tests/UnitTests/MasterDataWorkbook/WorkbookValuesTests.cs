using MicroLIMS.Application.Services.MasterDataWorkbook;
using Xunit;

namespace MicroLIMS.Tests.UnitTests.MasterDataWorkbook;

public class WorkbookValuesTests
{
    [Theory]
    [InlineData("1.50", "1.5")] [InlineData(" 7 ", "7")] [InlineData("TRUE", "yes")] [InlineData("No", "no")] [InlineData(null, "")]
    public void Normalise_Scalar(string? input, string expected) =>
        Assert.Equal(expected, WorkbookValues.Normalise(input, isList: false));

    [Fact]
    public void Normalise_List_SortsAndTrims() =>
        Assert.Equal("TAMC;TYMC", WorkbookValues.Normalise(" TYMC ; TAMC ", isList: true));

    [Fact]
    public void Key_IsUpperTrimmedAndPiped() =>
        Assert.Equal("RM-001|TAMC|", WorkbookValues.Key(" rm-001", "TAMC", null));

    [Fact]
    public void Fmt_DecimalAndDate()
    {
        Assert.Equal("0.5", WorkbookValues.Fmt(0.50m));
        Assert.Equal("", WorkbookValues.Fmt((decimal?)null));
        Assert.Equal("2027-03-31", WorkbookValues.Fmt(new DateTime(2027, 3, 31, 10, 0, 0, DateTimeKind.Utc)));
        Assert.Equal("Yes", WorkbookValues.Fmt(true));
    }
}
