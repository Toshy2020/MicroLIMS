using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;

namespace MicroLIMS.Application.Services.MasterDataWorkbook;

// Loads workbook rows through the sheet handlers, in sheet order, inside
// the caller's transaction. Each new row gets its own savepoint: a row
// that fails - including a PostgreSQL error, which would otherwise abort
// the whole transaction - is rolled back alone and the remaining rows are
// still checked, so the lab sees every problem in one pass.
public sealed class MasterDataWorkbookRunner
{
    public const int MaxDataRows = 5000;
    private const string Savepoint = "mdw_row";

    private readonly IMicroLimsDbContext _db;
    private readonly IReadOnlyList<SheetHandler> _sheets;

    public MasterDataWorkbookRunner(IMicroLimsDbContext db, IReadOnlyList<SheetHandler> sheets)
    {
        _db = db;
        _sheets = sheets;
    }

    public async Task<(WorkbookReport Report, List<CreatedRecord> Created)> RunAsync(IReadOnlyList<WorkbookSheet> sheets, int userId)
    {
        var tx = _db.Database.CurrentTransaction
            ?? throw new InvalidOperationException("The workbook load must run inside a transaction.");
        var errors = new List<WorkbookRowError>();
        var summaries = new List<WorkbookSheetSummary>();
        var created = new List<CreatedRecord>();

        foreach (var unknown in sheets.Where(s => _sheets.All(h => !h.Name.Equals(s.Name, StringComparison.OrdinalIgnoreCase))))
            errors.Add(new WorkbookRowError(unknown.Name, 1, null, $"Unknown sheet '{unknown.Name}'. Use the sheets of the downloaded workbook."));

        _db.CurrentUserId = userId;
        var ctx = new ImportContext(_db, userId, sheets);

        // What existed BEFORE this load - computed up front, so rows created
        // by an earlier sheet (e.g. stages created with their specification)
        // are not mistaken for pre-existing ones. Only sheets in the upload
        // are read, and unscoped: existence is checked across all sections.
        var existing = new Dictionary<string, Dictionary<string, Dictionary<string, string>>>();
        foreach (var handler in _sheets.Where(h => ctx.Sheet(h.Name) is not null))
            existing[handler.Name] = (await handler.ExportAsync(_db, sectionScope: null))
                .GroupBy(handler.KeyOf)
                .ToDictionary(g => g.Key, g => g.First());
        _db.ChangeTracker.Clear();

        // The limit is on NEW rows: a downloaded workbook lists every
        // existing record, and those cost only a comparison.
        var newRows = existing.Sum(e => ctx.Sheet(e.Key)!.Rows.Count(r => !e.Value.ContainsKey(_sheets.First(h => h.Name == e.Key).KeyOf(r.Cells))));
        if (newRows > MaxDataRows)
        {
            errors.Add(new WorkbookRowError(string.Empty, 0, null, $"The workbook has {newRows} new rows; the limit is {MaxDataRows}. Split it into several uploads."));
            return (new WorkbookReport(summaries, errors), created);
        }

        foreach (var handler in _sheets)
        {
            var sheet = ctx.Sheet(handler.Name);
            if (sheet is null) continue;
            var errorsBefore = errors.Count;
            int createdCount = 0, unchanged = 0;

            var missing = handler.Columns
                .Where(c => c.Required && !sheet.Columns.Contains(c.Name, StringComparer.OrdinalIgnoreCase))
                .Select(c => c.Name).ToList();
            if (missing.Count > 0)
            {
                errors.Add(new WorkbookRowError(sheet.Name, 1, null, $"Missing column(s): {string.Join(", ", missing)}."));
                summaries.Add(new WorkbookSheetSummary(sheet.Name, 0, 0, 1));
                continue;
            }

            var seen = new HashSet<string>();
            foreach (var row in sheet.Rows)
            {
                var key = handler.KeyOf(row.Cells);
                if (!seen.Add(key))
                {
                    errors.Add(new WorkbookRowError(sheet.Name, row.RowNumber, null, "The same record appears more than once on this sheet."));
                    continue;
                }

                if (existing[handler.Name].TryGetValue(key, out var current))
                {
                    var diff = WorkbookValues.DifferentColumns(handler.Columns, current, row.Cells);
                    if (diff.Count == 0) unchanged++;
                    else errors.Add(new WorkbookRowError(sheet.Name, row.RowNumber, diff[0],
                        $"Already in MicroLIMS with different values in: {string.Join(", ", diff)}. The workbook only adds records - change it in its screen."));
                    continue;
                }

                await tx.CreateSavepointAsync(Savepoint);
                try
                {
                    created.Add(await handler.CreateAsync(new RowReader(row), ctx));
                    await tx.ReleaseSavepointAsync(Savepoint);
                    createdCount++;
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    await tx.RollbackToSavepointAsync(Savepoint);
                    var rowError = e as RowError;
                    errors.Add(new WorkbookRowError(rowError?.Sheet ?? sheet.Name, rowError?.Row ?? row.RowNumber, rowError?.Column, MessageOf(e)));
                }
                finally
                {
                    _db.ChangeTracker.Clear();
                }
            }

            summaries.Add(new WorkbookSheetSummary(sheet.Name, createdCount, unchanged, errors.Count - errorsBefore));
        }

        return (new WorkbookReport(summaries, errors), created);
    }

    private static string MessageOf(Exception e) =>
        e is DbUpdateException { InnerException: { } inner } ? inner.Message : e.Message;
}
