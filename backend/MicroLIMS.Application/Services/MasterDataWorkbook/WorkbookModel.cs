namespace MicroLIMS.Application.Services.MasterDataWorkbook;

// Plain data passed between the workbook file format and the import.
// Cells are always text: the format layer turns numbers, dates and
// booleans into invariant text, so the import parses one representation.

public sealed record WorkbookRow(int RowNumber, IReadOnlyDictionary<string, string> Cells);

public sealed record WorkbookSheet(string Name, IReadOnlyList<string> Columns, IReadOnlyList<WorkbookRow> Rows);

// A drop-down for one column: either fixed values (written to a hidden
// Lists sheet) or another sheet's column (so rows typed into the workbook
// can be picked as well).
public sealed record WorkbookColumnChoices(
    string Sheet, string Column,
    IReadOnlyList<string>? Values = null,
    string? SourceSheet = null, string? SourceColumn = null);

public sealed record WorkbookInstructionRow(string Sheet, string Column, string Required, string Help);

public sealed record WorkbookContent(
    IReadOnlyList<WorkbookSheet> Sheets,
    IReadOnlyList<WorkbookColumnChoices> Choices,
    IReadOnlyList<string> Instructions,
    IReadOnlyList<WorkbookInstructionRow> ColumnHelp);

public sealed record WorkbookRowError(string Sheet, int Row, string? Column, string Message);

public sealed record WorkbookSheetSummary(string Sheet, int Created, int Unchanged, int Errors);

public sealed record WorkbookReport(IReadOnlyList<WorkbookSheetSummary> Sheets, IReadOnlyList<WorkbookRowError> Errors)
{
    public bool HasErrors => Errors.Count > 0;
}

// One database row created by the import, kept on the import record.
public sealed record CreatedRecord(string EntityType, int EntityId, string Sheet = "", int RowNumber = 0);
