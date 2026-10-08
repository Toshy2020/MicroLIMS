using MicroLIMS.Application.Abstractions.Persistence;

namespace MicroLIMS.Application.Services.MasterDataWorkbook;

// One column of a sheet: whether it is required, whether it holds a
// ;-separated list, and where its drop-down values come from (an enum, a
// column of another sheet, or the database via DbChoicesAsync).
public sealed record SheetColumn(
    string Name,
    bool Required = false,
    bool IsList = false,
    Type? EnumType = null,
    string? SourceSheet = null,
    string? SourceColumn = null,
    string Help = "");

// State shared by all rows of one load.
public sealed class ImportContext
{
    public ImportContext(IMicroLimsDbContext db, int userId, IReadOnlyList<WorkbookSheet> sheets)
    {
        Db = db;
        UserId = userId;
        Sheets = sheets;
    }

    public IMicroLimsDbContext Db { get; }
    public int UserId { get; }
    public IReadOnlyList<WorkbookSheet> Sheets { get; }

    // Specifications created in this load (SpecificationsSheet.KeyOf -> id):
    // their stage rows were created with them.
    public Dictionary<string, int> CreatedSpecifications { get; } = new(StringComparer.OrdinalIgnoreCase);

    public WorkbookSheet? Sheet(string name) =>
        Sheets.FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
}

// One sheet of the master data workbook. A handler only maps between
// workbook rows and the existing master-data service for its record type;
// it never repeats that service's rules.
public abstract class SheetHandler
{
    public abstract string Name { get; }
    public abstract IReadOnlyList<SheetColumn> Columns { get; }

    // The row's identity; equal keys mean "the same record".
    public abstract string KeyOf(IReadOnlyDictionary<string, string> cells);

    // Every current database record of this type, as workbook rows.
    public abstract Task<List<Dictionary<string, string>>> ExportAsync(IMicroLimsDbContext db);

    // Creates the record through the existing service. Throws RowError
    // (or whatever the service throws) when the row cannot be created.
    public abstract Task<CreatedRecord> CreateAsync(RowReader row, ImportContext ctx);

    // Drop-down values that come from the database (codes of records this
    // workbook does not contain, such as test definitions).
    public virtual Task<IReadOnlyList<string>> DbChoicesAsync(IMicroLimsDbContext db, string column) =>
        Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());

    protected static string Cell(IReadOnlyDictionary<string, string> cells, string column) =>
        cells.TryGetValue(column, out var v) ? v : string.Empty;

    protected CreatedRecord Created(string entityType, int id, RowReader row) => new(entityType, id, Name, row.RowNumber);
}
