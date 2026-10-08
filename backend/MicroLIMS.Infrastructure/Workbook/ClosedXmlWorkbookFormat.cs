using System.Globalization;
using ClosedXML.Excel;
using MicroLIMS.Application.Abstractions.Workbook;
using MicroLIMS.Application.Services.MasterDataWorkbook;

namespace MicroLIMS.Infrastructure.Workbook;

// .xlsx reading and writing for the master data workbook (ClosedXML, MIT).
// Every data column is formatted as text so Excel keeps codes like "007"
// and does not turn "1-2" into a date; drop-downs are list validations.
public class ClosedXmlWorkbookFormat : IWorkbookFormat
{
    public const string InstructionsSheet = "Instructions";
    public const string ListsSheet = "Lists";
    // Drop-downs cover the existing rows plus this many new rows (the
    // load limit, MasterDataWorkbookRunner.MaxDataRows).
    private const int ValidatedRows = MasterDataWorkbookRunner.MaxDataRows;

    private static int LastValidatedRow(WorkbookContent content, string sheet) =>
        1 + (content.Sheets.FirstOrDefault(s => s.Name == sheet)?.Rows.Count ?? 0) + ValidatedRows;

    public byte[] Write(WorkbookContent content)
    {
        using var wb = new XLWorkbook();
        WriteInstructions(wb.AddWorksheet(InstructionsSheet), content);

        foreach (var sheet in content.Sheets)
        {
            var ws = wb.AddWorksheet(sheet.Name);
            for (var c = 0; c < sheet.Columns.Count; c++)
            {
                ws.Column(c + 1).Style.NumberFormat.Format = "@";
                ws.Cell(1, c + 1).Value = sheet.Columns[c];
            }
            ws.Row(1).Style.Font.Bold = true;
            ws.SheetView.FreezeRows(1);

            var r = 2;
            foreach (var row in sheet.Rows)
            {
                for (var c = 0; c < sheet.Columns.Count; c++)
                    ws.Cell(r, c + 1).Value = row.Cells.TryGetValue(sheet.Columns[c], out var v) ? v : string.Empty;
                r++;
            }
            ws.Columns().AdjustToContents(1, 50);
        }

        var lists = wb.AddWorksheet(ListsSheet);
        var listColumn = 0;
        foreach (var choice in content.Choices)
        {
            var target = wb.Worksheets.FirstOrDefault(w => w.Name == choice.Sheet);
            var col = ColumnIndex(content, choice.Sheet, choice.Column);
            if (target is null || col is null) continue;
            var range = target.Range(2, col.Value, LastValidatedRow(content, choice.Sheet), col.Value);

            if (choice.Values is { Count: > 0 } values)
            {
                listColumn++;
                for (var i = 0; i < values.Count; i++)
                    lists.Cell(i + 1, listColumn).Value = values[i];
                range.CreateDataValidation().List(lists.Range(1, listColumn, values.Count, listColumn), true);
            }
            else if (choice.SourceSheet is not null && choice.SourceColumn is not null)
            {
                var source = wb.Worksheets.FirstOrDefault(w => w.Name == choice.SourceSheet);
                var sourceCol = ColumnIndex(content, choice.SourceSheet, choice.SourceColumn);
                if (source is null || sourceCol is null) continue;
                range.CreateDataValidation().List(source.Range(2, sourceCol.Value, LastValidatedRow(content, choice.SourceSheet), sourceCol.Value), true);
            }
        }
        lists.Visibility = XLWorksheetVisibility.Hidden;

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    public IReadOnlyList<WorkbookSheet> Read(Stream xlsx)
    {
        XLWorkbook wb;
        try { wb = new XLWorkbook(xlsx); }
        catch (Exception e) { throw new WorkbookFormatException("The file is not a readable .xlsx workbook.", e); }

        using (wb)
        {
            var sheets = new List<WorkbookSheet>();
            foreach (var ws in wb.Worksheets)
            {
                if (ws.Name is InstructionsSheet or ListsSheet) continue;
                var lastCol = ws.Row(1).LastCellUsed()?.Address.ColumnNumber ?? 0;
                if (lastCol == 0) continue;

                var columns = Enumerable.Range(1, lastCol).Select(c => CellText(ws.Cell(1, c))).ToList();
                var lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;
                var rows = new List<WorkbookRow>();
                for (var r = 2; r <= lastRow; r++)
                {
                    var cells = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    for (var c = 1; c <= lastCol; c++)
                        if (columns[c - 1].Length > 0)
                            cells[columns[c - 1]] = CellText(ws.Cell(r, c));
                    if (cells.Values.Any(v => v.Length > 0))
                        rows.Add(new WorkbookRow(r, cells));
                }
                sheets.Add(new WorkbookSheet(ws.Name, columns.Where(c => c.Length > 0).ToList(), rows));
            }
            return sheets;
        }
    }

    private static string CellText(IXLCell cell)
    {
        var v = cell.Value;
        if (v.IsBlank) return string.Empty;
        if (v.IsNumber) return ((decimal)v.GetNumber()).ToString(CultureInfo.InvariantCulture);
        if (v.IsDateTime) return v.GetDateTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        if (v.IsBoolean) return v.GetBoolean() ? "Yes" : "No";
        if (v.IsText) return v.GetText().Trim();
        return cell.GetFormattedString().Trim();
    }

    private static int? ColumnIndex(WorkbookContent content, string sheet, string column)
    {
        var s = content.Sheets.FirstOrDefault(x => x.Name == sheet);
        var i = s?.Columns.ToList().FindIndex(c => c == column) ?? -1;
        return i < 0 ? null : i + 1;
    }

    private static void WriteInstructions(IXLWorksheet ws, WorkbookContent content)
    {
        var r = 1;
        foreach (var line in content.Instructions)
            ws.Cell(r++, 1).Value = line;
        r++;
        ws.Cell(r, 1).Value = "Sheet"; ws.Cell(r, 2).Value = "Column"; ws.Cell(r, 3).Value = "Required"; ws.Cell(r, 4).Value = "What to enter";
        ws.Row(r).Style.Font.Bold = true;
        foreach (var h in content.ColumnHelp)
        {
            r++;
            ws.Cell(r, 1).Value = h.Sheet; ws.Cell(r, 2).Value = h.Column; ws.Cell(r, 3).Value = h.Required; ws.Cell(r, 4).Value = h.Help;
        }
        ws.Column(1).Width = 24; ws.Column(2).Width = 26; ws.Column(3).Width = 10; ws.Column(4).Width = 90;
    }
}
