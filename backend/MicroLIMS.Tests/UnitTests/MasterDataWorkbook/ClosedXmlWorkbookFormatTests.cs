using ClosedXML.Excel;
using MicroLIMS.Application.Abstractions.Workbook;
using MicroLIMS.Application.Services.MasterDataWorkbook;
using MicroLIMS.Infrastructure.Workbook;
using Xunit;

namespace MicroLIMS.Tests.UnitTests.MasterDataWorkbook;

public class ClosedXmlWorkbookFormatTests
{
    private readonly ClosedXmlWorkbookFormat _format = new();

    private static WorkbookContent Content(params WorkbookSheet[] sheets) =>
        new(sheets, Array.Empty<WorkbookColumnChoices>(), new[] { "Read me" }, Array.Empty<WorkbookInstructionRow>());

    private static WorkbookRow Row(int n, params (string Col, string Val)[] cells) =>
        new(n, cells.ToDictionary(c => c.Col, c => c.Val));

    [Fact]
    public void WriteThenRead_RoundTripsSheetsHeadersAndText()
    {
        var sheet = new WorkbookSheet("Organisms", new[] { "ScientificName", "AtccNumber" },
            new[] { Row(2, ("ScientificName", "Escherichia coli"), ("AtccNumber", "8739")) });

        var bytes = _format.Write(Content(sheet));
        var read = _format.Read(new MemoryStream(bytes));

        var only = Assert.Single(read);
        Assert.Equal("Organisms", only.Name);
        Assert.Equal(new[] { "ScientificName", "AtccNumber" }, only.Columns);
        var row = Assert.Single(only.Rows);
        Assert.Equal(2, row.RowNumber);
        Assert.Equal("Escherichia coli", row.Cells["ScientificName"]);
        Assert.Equal("8739", row.Cells["AtccNumber"]);
    }

    [Fact]
    public void Read_SkipsInstructionsListsAndBlankRows()
    {
        using var wb = new XLWorkbook();
        wb.AddWorksheet("Instructions").Cell(1, 1).Value = "x";
        wb.AddWorksheet("Lists").Cell(1, 1).Value = "x";
        var ws = wb.AddWorksheet("MediaProducts");
        ws.Cell(1, 1).Value = "Code"; ws.Cell(1, 2).Value = "Name";
        ws.Cell(2, 1).Value = "TSA"; ws.Cell(2, 2).Value = "Tryptic Soy Agar";
        ws.Cell(4, 1).Value = "SDA"; ws.Cell(4, 2).Value = "Sabouraud";   // row 3 blank
        var bytes = Save(wb);

        var sheet = Assert.Single(_format.Read(new MemoryStream(bytes)));
        Assert.Equal(new[] { 2, 4 }, sheet.Rows.Select(r => r.RowNumber));
    }

    [Fact]
    public void Read_NumberDateBoolCells()
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Equipment");
        ws.Cell(1, 1).Value = "Num"; ws.Cell(1, 2).Value = "Date"; ws.Cell(1, 3).Value = "Flag"; ws.Cell(1, 4).Value = "Text";
        ws.Cell(2, 1).Value = 0.5;
        ws.Cell(2, 2).Value = new DateTime(2027, 3, 31);
        ws.Cell(2, 3).Value = true;
        ws.Cell(2, 4).Value = "  1,5  ";
        var cells = Assert.Single(Assert.Single(_format.Read(new MemoryStream(Save(wb)))).Rows).Cells;

        Assert.Equal("0.5", cells["Num"]);
        Assert.Equal("2027-03-31", cells["Date"]);
        Assert.Equal("Yes", cells["Flag"]);
        Assert.Equal("1,5", cells["Text"]);   // trimmed, not converted
    }

    [Fact]
    public void Read_NotAWorkbook_Throws()
    {
        Assert.Throws<WorkbookFormatException>(() => _format.Read(new MemoryStream(new byte[] { 1, 2, 3 })));
    }

    [Fact]
    public void Write_AddsDropDownFromValuesAndFromSheet()
    {
        var rooms = new WorkbookSheet("EmRooms", new[] { "Department", "Name" }, Array.Empty<WorkbookRow>());
        var depts = new WorkbookSheet("EmDepartments", new[] { "Name", "Class" }, Array.Empty<WorkbookRow>());
        var content = new WorkbookContent(new[] { depts, rooms },
            new[]
            {
                new WorkbookColumnChoices("EmDepartments", "Class", Values: new[] { "Grade A", "Grade B" }),
                new WorkbookColumnChoices("EmRooms", "Department", SourceSheet: "EmDepartments", SourceColumn: "Name"),
            },
            new[] { "Read me" }, Array.Empty<WorkbookInstructionRow>());

        using var wb = new XLWorkbook(new MemoryStream(_format.Write(content)));

        Assert.True(wb.Worksheet("EmDepartments").DataValidations.Any());
        Assert.True(wb.Worksheet("EmRooms").DataValidations.Any());
        Assert.Equal(XLWorksheetVisibility.Hidden, wb.Worksheet("Lists").Visibility);
        Assert.Equal("Instructions", wb.Worksheets.First().Name);
    }

    private static byte[] Save(XLWorkbook wb)
    {
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }
}
