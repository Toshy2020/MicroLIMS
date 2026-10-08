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

    [Fact]
    public void DifferentColumns_IgnoresCaseTrailingZerosAndListOrder()
    {
        var columns = new[] { new SheetColumn("Code", Required: true), new SheetColumn("Limit"), new SheetColumn("Tests", IsList: true), new SheetColumn("Name") };
        var db = new Dictionary<string, string> { ["Code"] = "TSA", ["Limit"] = "1.5", ["Tests"] = "A;B", ["Name"] = "Soy" };
        var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["Code"] = "tsa", ["Limit"] = "1.50", ["Tests"] = "B; A", ["Name"] = "Soya" };

        Assert.Equal(new[] { "Name" }, WorkbookValues.DifferentColumns(columns, db, row));
    }
}
