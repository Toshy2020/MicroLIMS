# Master Data Workbook Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A lab loads its flat master data and specifications into a new MicroLIMS deployment from one Excel workbook. The load is dry-run checked, signed, audited, kept as evidence, and verified by a second person.

**Architecture:** An Application-layer service reads the workbook through an `IWorkbookFormat` abstraction, implemented in Infrastructure with ClosedXML. The service then passes each row to the **existing** master-data create service inside one database transaction, with a savepoint per row. A dry run rolls back; a commit signs and keeps the file. Items and material-master entries load inactive, and a second user's verification activates them. One download returns the current data in the same format, so the same workbook is the template, the export, and the Configuration Specification.

**Tech Stack:** ASP.NET Core 10, EF Core 10 + Npgsql, ClosedXML (new, MIT), xUnit; React 19 + TypeScript + MUI 9, Vitest.

**Spec:** `docs/superpowers/specs/2026-10-05-master-data-workbook-design.md`

**Nothing in this plan deploys.** All work stays on the local machine and local branch; no push, no Neon, no Render.

## Global Constraints

- Branch: work on a new local branch `feat/master-data-workbook` created from the current `feat/physchem-areas` HEAD. Commit each task locally; never push or merge.
- All build output, test artifacts and scratch files go under `E:/MicroLIMS/rls-tmp/` (never C:). Use `--artifacts-path E:/MicroLIMS/rls-tmp/mdw-<task>` on every `dotnet build`/`dotnet test`.
- Application code never calls `DateTime.UtcNow`/`Now`/`Today`; it uses the injected `TimeProvider` (enforced by `ArchitectureTests/ClockUsageTests`).
- No source file over 2000 lines (`ArchitectureTests/SourceFileSizeTests`).
- Controllers call Application services only, never the DbContext (`ArchitectureTests/ControllerDependencyTests`). Controllers return DTOs, never entities (`ArchitectureTests/ApiContractTests`).
- Every new endpoint is listed in `backend/MicroLIMS.Tests/ArchitectureTests/authorization-matrix.txt`.
- Permission: `MasterData.Manage` (`PermissionConstants.MasterDataManage`) for every endpoint. Today that is SystemAdministrator and SectionHead.
- Signatures: commit = `SignatureMeaning.MasterDataChanged`; verify = `SignatureMeaning.Reviewed`; entity type string `"MasterDataImport"`.
- File limit 10 MB (`10 * 1024 * 1024` bytes); at most 5,000 data rows per upload.
- Numbers in the workbook use a dot as decimal separator; a comma is rejected, not guessed.
- Commit messages: write them with a Bash heredoc (PowerShell 5.1 adds a BOM) and end them with
  `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.
- Postgres integration tests need `MICROLIMS_TEST_POSTGRES` set (already set on this PC); without it they show as skipped, which does **not** count as passing.

## Review Focus

1. **A row the existing service rejects halfway through a save** (e.g. a unique-index violation raised by PostgreSQL rather than a pre-check). Expect only that row to fail, with the database message, and every later row still checked. Pinned in Task 6 (`OneBadRowDoesNotStopTheRest`).
2. **Re-uploading a workbook that was already loaded, or the downloaded export.** Expect every row "unchanged", zero errors, nothing created. Pinned in Task 6 (`ExportThenReimport_IsAllUnchanged`).
3. **The same person loads and verifies.** Expect refusal with a clear message and no state change. Pinned in Task 7 (`Verify_BySameUser_IsRefused`).
4. **A wrong password on commit.** Expect nothing created, and the failed signing attempt still recorded so it counts toward the lockout. Pinned in Task 7 (`Commit_WrongPassword_CreatesNothing`).
5. **Excel-typed numbers and dates** (a number cell `0.5`, a date cell, `1,5` typed as text, `Yes`/`TRUE`). Expect the dot-decimal number and the date read correctly, the comma rejected with the column named, and both boolean spellings accepted. Pinned in Task 1 (`Read_NumberDateBoolCells`) and Task 2 (`Decimal_WithComma_IsRejected`).

---

## File map

**Backend: create**

| File | Responsibility |
|---|---|
| `backend/MicroLIMS.Application/Abstractions/Workbook/IWorkbookFormat.cs` | Read/write seam for `.xlsx` + `WorkbookFormatException` |
| `backend/MicroLIMS.Application/Services/MasterDataWorkbook/WorkbookModel.cs` | Plain records: rows, sheets, content, choices, report, created record |
| `backend/MicroLIMS.Application/Services/MasterDataWorkbook/RowReader.cs` | Typed reading of one row's cells + `RowError` |
| `backend/MicroLIMS.Application/Services/MasterDataWorkbook/WorkbookValues.cs` | Formatting for export and normalised comparison |
| `backend/MicroLIMS.Application/Services/MasterDataWorkbook/SheetHandler.cs` | Abstract sheet + `SheetColumn` + `ImportContext` |
| `backend/MicroLIMS.Application/Services/MasterDataWorkbook/Sheets/ReferenceSheets.cs` | Organisms, ProductionStages, EmDepartments, EmRooms, WaterDepartments, WaterPoints |
| `backend/MicroLIMS.Application/Services/MasterDataWorkbook/Sheets/InventorySheets.cs` | MaterialMaster, MediaProducts, Equipment, Items |
| `backend/MicroLIMS.Application/Services/MasterDataWorkbook/Sheets/SpecificationSheets.cs` | Specifications, SpecificationStages |
| `backend/MicroLIMS.Application/Services/MasterDataWorkbook/MasterDataSheets.cs` | Ordered list of the 12 handlers + instructions text |
| `backend/MicroLIMS.Application/Services/MasterDataWorkbook/MasterDataWorkbookRunner.cs` | Savepoint-per-row load loop that builds the report |
| `backend/MicroLIMS.Application/Services/MasterDataWorkbook/MasterDataWorkbookService.cs` | Download, validate, import, history, verify, file |
| `backend/MicroLIMS.Application/Services/MasterDataWorkbook/MasterDataWorkbookDtos.cs` | API DTOs |
| `backend/MicroLIMS.Domain/Entities/MasterDataImport.cs` | `MasterDataImport` + `MasterDataImportRecord` |
| `backend/MicroLIMS.Domain/Enums/MasterDataImportStatus.cs` | `Loaded`, `Verified` |
| `backend/MicroLIMS.Persistence/Configurations/MasterDataImportConfiguration.cs` | EF mapping for both entities |
| `backend/MicroLIMS.Persistence/Migrations/<timestamp>_AddMasterDataImports.cs` | Generated migration |
| `backend/MicroLIMS.Infrastructure/Workbook/ClosedXmlWorkbookFormat.cs` | ClosedXML implementation |
| `backend/MicroLIMS.API/Controllers/MasterDataWorkbookController.cs` | HTTP endpoints |
| `backend/MicroLIMS.Tests/UnitTests/MasterDataWorkbook/ClosedXmlWorkbookFormatTests.cs` | Round-trip and cell-type tests |
| `backend/MicroLIMS.Tests/UnitTests/MasterDataWorkbook/RowReaderTests.cs` | Parsing tests |
| `backend/MicroLIMS.Tests/UnitTests/MasterDataWorkbook/WorkbookValuesTests.cs` | Comparison tests |
| `backend/MicroLIMS.Tests/IntegrationTests/MasterDataWorkbookPostgresIntegrationTests.cs` | End-to-end against PostgreSQL |

**Backend: modify**

| File | Change |
|---|---|
| `backend/MicroLIMS.Infrastructure/MicroLIMS.Infrastructure.csproj` | Add ClosedXML package |
| `backend/MicroLIMS.Application/Abstractions/Persistence/IMicroLimsDbContext.cs` | Two DbSets |
| `backend/MicroLIMS.Persistence/DbContext/MicroLimsDbContext.cs` | Two DbSets |
| `backend/MicroLIMS.Application/Services/ReviewGateService.cs` (class `ReviewEntityTypes`, line ~102) | Add `MasterDataImport` constant |
| `backend/MicroLIMS.API/Extensions/ServiceCollectionExtensions.cs` (master data block, line ~184) | Register format + service |
| `backend/MicroLIMS.Tests/ArchitectureTests/authorization-matrix.txt` | Six new lines |

**Frontend: create**

| File | Responsibility |
|---|---|
| `frontend/src/modules/laboratoryConfiguration/masterDataWorkbook/types.ts` | DTO types + `canLoad` |
| `frontend/src/modules/laboratoryConfiguration/masterDataWorkbook/masterDataWorkbookService.ts` | API calls |
| `frontend/src/modules/laboratoryConfiguration/masterDataWorkbook/WorkbookReportTable.tsx` | Per-sheet counts + error table |
| `frontend/src/modules/laboratoryConfiguration/masterDataWorkbook/MasterDataWorkbookPage.tsx` | The page |
| `frontend/src/modules/laboratoryConfiguration/masterDataWorkbook/WorkbookReportTable.test.tsx` | Tests |

**Frontend: modify:** `frontend/src/routes/AppRoutes.tsx`, `frontend/src/routes/menuConfig.ts`, `frontend/src/routes/routes.ts`.

**Docs: create:** `docs/validation/ML-SYS-CS-001_Configuration_Specification.md`, `docs/validation/ML-SOP-MDW-001_Master_Data_Workbook_User_Guide.md`. **Modify:** the design spec (three decisions refined, Task 0).

---

## Phase 1: Read side (stop for review after Task 4)

### Task 0: Branch and spec refinements

**Files:**
- Modify: `docs/superpowers/specs/2026-10-05-master-data-workbook-design.md`

Three decisions were refined while planning against the code. Record them in the spec before building.

- [ ] **Step 1: Create the branch**

```bash
cd /e/MicroLIMS/MicroLIMS && git switch -c feat/master-data-workbook
```

- [ ] **Step 2: Edit the spec.** Under "Key decisions", replace decision 1's last sentence, decision 6's `SegregationOfDutiesGuard` mention, and decision 7 with:

```markdown
1. (...) Each row runs inside its own savepoint: a row that fails is rolled
   back to that savepoint and the change tracker is cleared, so a
   PostgreSQL error on one row cannot abort the transaction for the rest.
6. (...) The same user who committed cannot verify: the service compares the
   verifier's user id with the importer's (the test-order
   `SegregationOfDutiesGuard` does not apply to imports). The import
   history lists items from the import that have no specification yet, so
   the verifier can see when verification would release an item without
   limits.
7. **One download, not two.** "Download workbook" returns the current data
   in the template format; on an empty database it is the blank template.
   Re-uploading it gives all "unchanged". Reference drop-downs point at the
   referenced sheet's own column, so rows typed into the workbook can be
   picked too.
```

And in the API table, replace the `template` and `export` rows with:

```markdown
| GET | `api/masterdata/workbook` | Current data in workbook format (blank template on an empty database) |
```

- [ ] **Step 3: Commit**

```bash
git add docs/superpowers/specs/2026-10-05-master-data-workbook-design.md
git commit -F - <<'EOF'
docs(spec): master data workbook - savepoints, verifier check, single download

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
EOF
```

---

### Task 1: Workbook model, format abstraction, ClosedXML implementation

**Files:**
- Create: `backend/MicroLIMS.Application/Services/MasterDataWorkbook/WorkbookModel.cs`
- Create: `backend/MicroLIMS.Application/Abstractions/Workbook/IWorkbookFormat.cs`
- Create: `backend/MicroLIMS.Infrastructure/Workbook/ClosedXmlWorkbookFormat.cs`
- Modify: `backend/MicroLIMS.Infrastructure/MicroLIMS.Infrastructure.csproj`
- Test: `backend/MicroLIMS.Tests/UnitTests/MasterDataWorkbook/ClosedXmlWorkbookFormatTests.cs`

**Interfaces:**
- Produces: `WorkbookRow`, `WorkbookSheet`, `WorkbookColumnChoices`, `WorkbookInstructionRow`, `WorkbookContent`, `WorkbookRowError`, `WorkbookSheetSummary`, `WorkbookReport`, `CreatedRecord`; `IWorkbookFormat.Write(WorkbookContent) : byte[]`, `IWorkbookFormat.Read(Stream) : IReadOnlyList<WorkbookSheet>`; `WorkbookFormatException`.

- [ ] **Step 1: Add the package**

```bash
cd /e/MicroLIMS/MicroLIMS && dotnet add backend/MicroLIMS.Infrastructure package ClosedXML
```

Expected: a `<PackageReference Include="ClosedXML" Version="..." />` line in `MicroLIMS.Infrastructure.csproj`. Note the version in the commit message.

- [ ] **Step 2: Write the model** (`WorkbookModel.cs`)

```csharp
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
```

- [ ] **Step 3: Write the abstraction** (`IWorkbookFormat.cs`)

```csharp
using MicroLIMS.Application.Services.MasterDataWorkbook;

namespace MicroLIMS.Application.Abstractions.Workbook;

// The seam between the import and the .xlsx file format. Only the
// Infrastructure implementation knows about the spreadsheet library.
public interface IWorkbookFormat
{
    byte[] Write(WorkbookContent content);

    // Every data sheet (not "Instructions" or "Lists"): row 1 is the header,
    // data starts on row 2, fully blank rows are skipped. Throws
    // WorkbookFormatException when the stream is not a readable .xlsx.
    IReadOnlyList<WorkbookSheet> Read(Stream xlsx);
}

public class WorkbookFormatException : Exception
{
    public WorkbookFormatException(string message, Exception? inner = null) : base(message, inner) { }
}
```

- [ ] **Step 4: Write the failing tests** (`ClosedXmlWorkbookFormatTests.cs`)

```csharp
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
```

- [ ] **Step 5: Run them to confirm they fail**

```bash
cd /e/MicroLIMS/MicroLIMS && dotnet test backend/MicroLIMS.Tests --artifacts-path E:/MicroLIMS/rls-tmp/mdw-t1 --filter "FullyQualifiedName~ClosedXmlWorkbookFormatTests"
```

Expected: build error, `ClosedXmlWorkbookFormat` not found.

- [ ] **Step 6: Implement** (`ClosedXmlWorkbookFormat.cs`)

```csharp
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
    private const int ValidatedRows = 5000;

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
            var range = target.Range(2, col.Value, ValidatedRows + 1, col.Value);

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
                range.CreateDataValidation().List(source.Range(2, sourceCol.Value, ValidatedRows + 1, sourceCol.Value), true);
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
```

If the installed ClosedXML version names a member differently (for example `CreateDataValidation` vs `SetDataValidation`), use the version's equivalent and keep the behaviour; do not add another package.

- [ ] **Step 7: Run the tests**

Same command as Step 5. Expected: 5 passed.

- [ ] **Step 8: Commit**

```bash
git add backend/MicroLIMS.Infrastructure backend/MicroLIMS.Application/Abstractions/Workbook backend/MicroLIMS.Application/Services/MasterDataWorkbook/WorkbookModel.cs backend/MicroLIMS.Tests/UnitTests/MasterDataWorkbook/ClosedXmlWorkbookFormatTests.cs
git commit -F - <<'EOF'
feat(master-data): xlsx workbook format (ClosedXML <version>)

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
EOF
```

---

### Task 2: RowReader and WorkbookValues

**Files:**
- Create: `backend/MicroLIMS.Application/Services/MasterDataWorkbook/RowReader.cs`
- Create: `backend/MicroLIMS.Application/Services/MasterDataWorkbook/WorkbookValues.cs`
- Test: `backend/MicroLIMS.Tests/UnitTests/MasterDataWorkbook/RowReaderTests.cs`
- Test: `backend/MicroLIMS.Tests/UnitTests/MasterDataWorkbook/WorkbookValuesTests.cs`

**Interfaces:**
- Consumes: `WorkbookRow` (Task 1).
- Produces: `RowError(string? column, string message)` exception with `.Column`; `RowReader(WorkbookRow)` with `Text`, `OptionalText`, `Decimal`, `Int`, `RequiredInt`, `Bool`, `Enum<T>`, `OptionalEnum<T>`, `List`, `Date`; static `WorkbookValues.Fmt(...)` overloads, `WorkbookValues.Key(params string?[])`, `WorkbookValues.Normalise(string?, bool isList)`, `WorkbookValues.DifferentColumns(IEnumerable<SheetColumn>, IReadOnlyDictionary<string,string>, IReadOnlyDictionary<string,string>) : List<string>`. (`SheetColumn` is defined in Task 3; define `WorkbookValues.DifferentColumns` in Task 3 if you implement strictly in order. The test for it lives in Task 3.)

- [ ] **Step 1: Write the failing tests** (`RowReaderTests.cs`)

```csharp
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
```

`WorkbookValuesTests.cs`:

```csharp
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
```

- [ ] **Step 2: Run them to confirm they fail**

```bash
dotnet test backend/MicroLIMS.Tests --artifacts-path E:/MicroLIMS/rls-tmp/mdw-t2 --filter "FullyQualifiedName~RowReaderTests|FullyQualifiedName~WorkbookValuesTests"
```

Expected: build error, `RowReader` not found.

- [ ] **Step 3: Implement `RowReader.cs`**

```csharp
using System.Globalization;

namespace MicroLIMS.Application.Services.MasterDataWorkbook;

// A problem with one cell or row of the workbook. The message is shown to
// the lab as-is, next to the sheet, row and column.
public class RowError : Exception
{
    public RowError(string? column, string message) : base(message) { Column = column; }
    public string? Column { get; }
}

// Typed access to one workbook row. Every value arrives as invariant text
// (see IWorkbookFormat); this is the only place it is parsed.
public sealed class RowReader
{
    private readonly WorkbookRow _row;

    public RowReader(WorkbookRow row) { _row = row; }

    public int RowNumber => _row.RowNumber;

    public string? OptionalText(string column) =>
        _row.Cells.TryGetValue(column, out var v) && !string.IsNullOrWhiteSpace(v) ? v.Trim() : null;

    public string Text(string column) =>
        OptionalText(column) ?? throw new RowError(column, $"{column} is required.");

    public decimal? Decimal(string column)
    {
        var text = OptionalText(column);
        if (text is null) return null;
        if (text.Contains(','))
            throw new RowError(column, $"{column} '{text}' uses a comma. Use a dot as the decimal separator, for example 0.5.");
        return decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var d)
            ? d
            : throw new RowError(column, $"{column} '{text}' is not a number.");
    }

    public int? Int(string column)
    {
        var d = Decimal(column);
        if (d is null) return null;
        return d == decimal.Truncate(d.Value) ? (int)d.Value : throw new RowError(column, $"{column} must be a whole number.");
    }

    public int RequiredInt(string column) => Int(column) ?? throw new RowError(column, $"{column} is required.");

    public bool? Bool(string column) => OptionalText(column)?.ToLowerInvariant() switch
    {
        null => null,
        "yes" or "true" => true,
        "no" or "false" => false,
        var other => throw new RowError(column, $"{column} '{other}' must be Yes or No."),
    };

    public T Enum<T>(string column) where T : struct, System.Enum =>
        OptionalEnum<T>(column) ?? throw new RowError(column, $"{column} is required. Allowed: {string.Join(", ", System.Enum.GetNames<T>())}.");

    public T? OptionalEnum<T>(string column) where T : struct, System.Enum
    {
        var text = OptionalText(column);
        if (text is null) return null;
        // Names only: a number would bind to whatever value happens to have it.
        if (!int.TryParse(text, out _) && System.Enum.TryParse<T>(text, ignoreCase: true, out var value) && System.Enum.IsDefined(value))
            return value;
        throw new RowError(column, $"{column} '{text}' is not allowed. Allowed: {string.Join(", ", System.Enum.GetNames<T>())}.");
    }

    public IReadOnlyList<string> List(string column) =>
        (OptionalText(column) ?? string.Empty)
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

    public DateTime? Date(string column)
    {
        var text = OptionalText(column);
        if (text is null) return null;
        return DateTime.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? DateTime.SpecifyKind(d, DateTimeKind.Utc)
            : throw new RowError(column, $"{column} '{text}' must be a date written as yyyy-MM-dd.");
    }
}
```

- [ ] **Step 4: Implement `WorkbookValues.cs`** (the `DifferentColumns` method is added in Task 3, once `SheetColumn` exists)

```csharp
using System.Globalization;

namespace MicroLIMS.Application.Services.MasterDataWorkbook;

// How values are written to the workbook on download, and how a workbook
// value is compared with the database's value to decide "unchanged".
public static partial class WorkbookValues
{
    public static string Fmt(string? v) => v ?? string.Empty;
    public static string Fmt(decimal? v) => v?.ToString("G29", CultureInfo.InvariantCulture) ?? string.Empty;
    public static string Fmt(int? v) => v?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
    public static string Fmt(bool? v) => v is null ? string.Empty : v.Value ? "Yes" : "No";
    public static string Fmt(DateTime? v) => v?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty;
    public static string Fmt<T>(T? v) where T : struct, Enum => v?.ToString() ?? string.Empty;
    public static string FmtList(IEnumerable<string> values) => string.Join(";", values);

    // Case-insensitive, trimmed, pipe-joined: the identity of a row.
    public static string Key(params string?[] parts) =>
        string.Join("|", parts.Select(p => (p ?? string.Empty).Trim().ToUpperInvariant()));

    public static string Normalise(string? value, bool isList)
    {
        var text = (value ?? string.Empty).Trim();
        if (isList)
            return string.Join(";", text.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
        if (text.Length == 0) return text;
        if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var d))
            return d.ToString("G29", CultureInfo.InvariantCulture);
        return text.ToLowerInvariant() switch
        {
            "true" or "yes" => "yes",
            "false" or "no" => "no",
            _ => text,
        };
    }
}
```

- [ ] **Step 5: Run the tests.** Same command as Step 2. Expected: all pass.

- [ ] **Step 6: Commit**

```bash
git add backend/MicroLIMS.Application/Services/MasterDataWorkbook/RowReader.cs backend/MicroLIMS.Application/Services/MasterDataWorkbook/WorkbookValues.cs backend/MicroLIMS.Tests/UnitTests/MasterDataWorkbook/RowReaderTests.cs backend/MicroLIMS.Tests/UnitTests/MasterDataWorkbook/WorkbookValuesTests.cs
git commit -F - <<'EOF'
feat(master-data): workbook row parsing and value comparison

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
EOF
```

---

### Task 3: Sheet handlers (columns, keys, download, create)

**Files:**
- Create: `backend/MicroLIMS.Application/Services/MasterDataWorkbook/SheetHandler.cs`
- Create: `backend/MicroLIMS.Application/Services/MasterDataWorkbook/Sheets/ReferenceSheets.cs`
- Create: `backend/MicroLIMS.Application/Services/MasterDataWorkbook/Sheets/InventorySheets.cs`
- Create: `backend/MicroLIMS.Application/Services/MasterDataWorkbook/Sheets/SpecificationSheets.cs`
- Create: `backend/MicroLIMS.Application/Services/MasterDataWorkbook/MasterDataSheets.cs`
- Modify: `backend/MicroLIMS.Application/Services/MasterDataWorkbook/WorkbookValues.cs` (add `DifferentColumns`)
- Test: add to `backend/MicroLIMS.Tests/UnitTests/MasterDataWorkbook/WorkbookValuesTests.cs`

**Interfaces:**
- Consumes: `RowReader`, `RowError`, `WorkbookValues`, `CreatedRecord`, `WorkbookColumnChoices` (Tasks 1–2); existing services listed below.
- Produces:
  - `SheetColumn(string Name, bool Required = false, bool IsList = false, Type? EnumType = null, string? SourceSheet = null, string? SourceColumn = null, string Help = "")`
  - `ImportContext(IMicroLimsDbContext db, int userId, IReadOnlyList<WorkbookSheet> sheets)` with `Db`, `UserId`, `Sheets`, `CreatedSpecificationKeys : HashSet<string>`
  - `abstract class SheetHandler` with `string Name`, `IReadOnlyList<SheetColumn> Columns`, `string KeyOf(IReadOnlyDictionary<string,string> cells)`, `Task<List<Dictionary<string,string>>> ExportAsync(IMicroLimsDbContext db)`, `Task<CreatedRecord> CreateAsync(RowReader row, ImportContext ctx)`, `virtual Task<IReadOnlyList<string>> DbChoicesAsync(IMicroLimsDbContext db, string column)`
  - `MasterDataSheets.Create(...)` returning the 12 handlers in load order; `MasterDataSheets.Instructions : IReadOnlyList<string>`

Existing services called (all already registered in DI, all share the request's scoped `IMicroLimsDbContext`):

| Sheet | Call |
|---|---|
| Organisms | `OrganismMasterDataService.CreateOrganismAsync(CreateOrganismRequest)` |
| ProductionStages | `ReferenceListMasterDataService.CreateProductionStageAsync(CreateProductionStageRequest)` |
| EmDepartments | `EnvironmentalMonitoringMasterDataService.CreateDepartmentAsync(CreateDepartmentRequest)` |
| EmRooms | `EnvironmentalMonitoringMasterDataService.CreateRoomAsync(CreateRoomRequest)` |
| WaterDepartments | `WaterMasterDataService.CreateWaterDepartmentAsync(CreateWaterDepartmentRequest)` |
| WaterPoints | `WaterMasterDataService.CreateWaterSamplingPointAsync(CreateWaterSamplingPointRequest)` |
| MaterialMaster | `MaterialMasterService.CreateAsync(SaveMaterialMasterEntryRequest, int userId)`, then `SetActiveAsync(id, false, userId)` |
| MediaProducts | `MediaProductService.CreateAsync(string name, string code)` |
| Equipment | `EquipmentMasterDataService.CreateEquipmentAsync(int userId, CreateEquipmentRequest)` |
| Items | `ItemService.CreateAsync(ItemSaveRequest)`, then `SetActiveAsync(id, false)` |
| Specifications | `SpecificationMasterDataService.CreateSpecificationAsync(int userId, CreateSpecificationRequest)` |

Every response type above exposes `int Id`.

- [ ] **Step 1: Write the failing test** (append to `WorkbookValuesTests.cs`)

```csharp
    [Fact]
    public void DifferentColumns_IgnoresCaseTrailingZerosAndListOrder()
    {
        var columns = new[] { new SheetColumn("Code", Required: true), new SheetColumn("Limit"), new SheetColumn("Tests", IsList: true), new SheetColumn("Name") };
        var db = new Dictionary<string, string> { ["Code"] = "TSA", ["Limit"] = "1.5", ["Tests"] = "A;B", ["Name"] = "Soy" };
        var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["Code"] = "tsa", ["Limit"] = "1.50", ["Tests"] = "B; A", ["Name"] = "Soya" };

        Assert.Equal(new[] { "Name" }, WorkbookValues.DifferentColumns(columns, db, row));
    }
```

- [ ] **Step 2: Implement `SheetHandler.cs`**

```csharp
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

    // Keys (SpecificationsSheet.KeyOf) of specifications created in this
    // load: their stage rows were created with them.
    public HashSet<string> CreatedSpecificationKeys { get; } = new();

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
```

- [ ] **Step 3: Add `DifferentColumns` to `WorkbookValues.cs`** (inside the class)

```csharp
    // Columns whose workbook value differs from the database value after
    // normalising (case, spaces, trailing zeros, Yes/True, list order).
    public static List<string> DifferentColumns(
        IEnumerable<SheetColumn> columns,
        IReadOnlyDictionary<string, string> current,
        IReadOnlyDictionary<string, string> row) =>
        columns
            .Where(c => !string.Equals(
                Normalise(current.TryGetValue(c.Name, out var a) ? a : null, c.IsList),
                Normalise(row.TryGetValue(c.Name, out var b) ? b : null, c.IsList),
                StringComparison.OrdinalIgnoreCase))
            .Select(c => c.Name)
            .ToList();
```

- [ ] **Step 4: Implement `Sheets/ReferenceSheets.cs`**

```csharp
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Services.MasterData;
using MicroLIMS.Domain.Enums;
using static MicroLIMS.Application.Services.MasterDataWorkbook.WorkbookValues;

namespace MicroLIMS.Application.Services.MasterDataWorkbook.Sheets;

public sealed class OrganismsSheet : SheetHandler
{
    private readonly OrganismMasterDataService _service;
    public OrganismsSheet(OrganismMasterDataService service) { _service = service; }

    public override string Name => "Organisms";
    public override IReadOnlyList<SheetColumn> Columns { get; } = new[]
    {
        new SheetColumn("ScientificName", Required: true, Help: "Full scientific name, e.g. Escherichia coli. Must be unique."),
        new SheetColumn("AtccNumber", Help: "ATCC number without the prefix, e.g. 8739. Must be unique when given."),
        new SheetColumn("CommonName"),
        new SheetColumn("Description"),
    };

    public override string KeyOf(IReadOnlyDictionary<string, string> c) => Key(Cell(c, "ScientificName"));

    public override async Task<List<Dictionary<string, string>>> ExportAsync(IMicroLimsDbContext db) =>
        (await db.Organisms.AsNoTracking().OrderBy(o => o.ScientificName).ToListAsync())
        .Select(o => new Dictionary<string, string>
        {
            ["ScientificName"] = o.ScientificName, ["AtccNumber"] = Fmt(o.AtccNumber),
            ["CommonName"] = Fmt(o.CommonName), ["Description"] = Fmt(o.Description),
        }).ToList();

    public override async Task<CreatedRecord> CreateAsync(RowReader r, ImportContext ctx)
    {
        var created = await _service.CreateOrganismAsync(new CreateOrganismRequest(
            r.Text("ScientificName"), r.OptionalText("AtccNumber"), r.OptionalText("CommonName"), r.OptionalText("Description")));
        return Created("Organism", created.Id, r);
    }
}

public sealed class ProductionStagesSheet : SheetHandler
{
    private readonly ReferenceListMasterDataService _service;
    public ProductionStagesSheet(ReferenceListMasterDataService service) { _service = service; }

    public override string Name => "ProductionStages";
    public override IReadOnlyList<SheetColumn> Columns { get; } = new[]
    {
        new SheetColumn("Name", Required: true, Help: "Stage name shown on the receiving form, e.g. Bulk."),
        new SheetColumn("Role", Required: true, EnumType: typeof(ProductionStageRole), Help: "What the stage means to the system: Bulk, InProcess, Finished, Stability or Other."),
    };

    public override string KeyOf(IReadOnlyDictionary<string, string> c) => Key(Cell(c, "Name"));

    public override async Task<List<Dictionary<string, string>>> ExportAsync(IMicroLimsDbContext db) =>
        (await db.ProductionStages.AsNoTracking().OrderBy(s => s.Name).ToListAsync())
        .Select(s => new Dictionary<string, string> { ["Name"] = s.Name, ["Role"] = s.Role.ToString() }).ToList();

    public override async Task<CreatedRecord> CreateAsync(RowReader r, ImportContext ctx)
    {
        var created = await _service.CreateProductionStageAsync(new CreateProductionStageRequest(r.Text("Name"), r.Enum<ProductionStageRole>("Role")));
        return Created("ProductionStage", created.Id, r);
    }
}

public sealed class EmDepartmentsSheet : SheetHandler
{
    private readonly EnvironmentalMonitoringMasterDataService _service;
    public EmDepartmentsSheet(EnvironmentalMonitoringMasterDataService service) { _service = service; }

    public override string Name => "EmDepartments";
    public override IReadOnlyList<SheetColumn> Columns { get; } = new[]
    {
        new SheetColumn("Name", Required: true, Help: "Production department monitored by EM, e.g. Sterile Filling."),
        new SheetColumn("Class", Help: "Cleanroom classification, e.g. Grade B."),
        new SheetColumn("TestingFrequency", Help: "Information only, e.g. Weekly."),
    };

    public override string KeyOf(IReadOnlyDictionary<string, string> c) => Key(Cell(c, "Name"));

    public override async Task<List<Dictionary<string, string>>> ExportAsync(IMicroLimsDbContext db) =>
        (await db.Departments.AsNoTracking().OrderBy(d => d.Name).ToListAsync())
        .Select(d => new Dictionary<string, string> { ["Name"] = d.Name, ["Class"] = d.Class, ["TestingFrequency"] = d.TestingFrequency }).ToList();

    public override async Task<CreatedRecord> CreateAsync(RowReader r, ImportContext ctx)
    {
        var created = await _service.CreateDepartmentAsync(new CreateDepartmentRequest(
            r.Text("Name"), r.OptionalText("Class") ?? string.Empty, r.OptionalText("TestingFrequency") ?? string.Empty));
        return Created("Department", created.Id, r);
    }
}

public sealed class EmRoomsSheet : SheetHandler
{
    private readonly EnvironmentalMonitoringMasterDataService _service;
    public EmRoomsSheet(EnvironmentalMonitoringMasterDataService service) { _service = service; }

    public override string Name => "EmRooms";
    public override IReadOnlyList<SheetColumn> Columns { get; } = new[]
    {
        new SheetColumn("Department", Required: true, SourceSheet: "EmDepartments", SourceColumn: "Name", Help: "Name of a department on the EmDepartments sheet."),
        new SheetColumn("Name", Required: true, Help: "Room name, unique within its department."),
        new SheetColumn("GradeClassification", Help: "e.g. Grade C."),
    };

    public override string KeyOf(IReadOnlyDictionary<string, string> c) => Key(Cell(c, "Department"), Cell(c, "Name"));

    public override async Task<List<Dictionary<string, string>>> ExportAsync(IMicroLimsDbContext db) =>
        (await db.Rooms.AsNoTracking().Include(r => r.Department).OrderBy(r => r.Department!.Name).ThenBy(r => r.Name).ToListAsync())
        .Select(r => new Dictionary<string, string>
        {
            ["Department"] = r.Department?.Name ?? string.Empty, ["Name"] = r.Name, ["GradeClassification"] = r.GradeClassification,
        }).ToList();

    public override async Task<CreatedRecord> CreateAsync(RowReader r, ImportContext ctx)
    {
        var deptName = r.Text("Department");
        var deptId = await ctx.Db.Departments.Where(d => d.Name.ToLower() == deptName.ToLower()).Select(d => (int?)d.Id).FirstOrDefaultAsync()
            ?? throw new RowError("Department", $"No EM department named '{deptName}'. Add it on the EmDepartments sheet.");
        var created = await _service.CreateRoomAsync(new CreateRoomRequest(r.Text("Name"), deptId, r.OptionalText("GradeClassification") ?? string.Empty));
        return Created("Room", created.Id, r);
    }
}

public sealed class WaterDepartmentsSheet : SheetHandler
{
    private readonly WaterMasterDataService _service;
    public WaterDepartmentsSheet(WaterMasterDataService service) { _service = service; }

    public override string Name => "WaterDepartments";
    public override IReadOnlyList<SheetColumn> Columns { get; } = new[]
    {
        new SheetColumn("Name", Required: true, Help: "Water system or area, e.g. Purified Water Loop 1."),
    };

    public override string KeyOf(IReadOnlyDictionary<string, string> c) => Key(Cell(c, "Name"));

    public override async Task<List<Dictionary<string, string>>> ExportAsync(IMicroLimsDbContext db) =>
        (await db.WaterDepartments.AsNoTracking().OrderBy(d => d.Name).ToListAsync())
        .Select(d => new Dictionary<string, string> { ["Name"] = d.Name }).ToList();

    public override async Task<CreatedRecord> CreateAsync(RowReader r, ImportContext ctx)
    {
        var created = await _service.CreateWaterDepartmentAsync(new CreateWaterDepartmentRequest(r.Text("Name")));
        return Created("WaterDepartment", created.Id, r);
    }
}

public sealed class WaterPointsSheet : SheetHandler
{
    private readonly WaterMasterDataService _service;
    public WaterPointsSheet(WaterMasterDataService service) { _service = service; }

    public override string Name => "WaterPoints";
    public override IReadOnlyList<SheetColumn> Columns { get; } = new[]
    {
        new SheetColumn("Code", Required: true, Help: "Sampling point code, unique, e.g. PW-01."),
        new SheetColumn("Location", Required: true),
        new SheetColumn("TestingFrequency", Help: "e.g. Daily."),
        new SheetColumn("TestCodes", IsList: true, Help: "Test codes from Test Master, separated by ; e.g. TAMC;PSEUDO."),
        new SheetColumn("WaterDepartment", SourceSheet: "WaterDepartments", SourceColumn: "Name", Help: "Name on the WaterDepartments sheet."),
    };

    public override string KeyOf(IReadOnlyDictionary<string, string> c) => Key(Cell(c, "Code"));

    public override async Task<List<Dictionary<string, string>>> ExportAsync(IMicroLimsDbContext db) =>
        (await db.WaterSamplingPoints.AsNoTracking().Include(p => p.WaterDepartment).OrderBy(p => p.Code).ToListAsync())
        .Select(p => new Dictionary<string, string>
        {
            ["Code"] = p.Code, ["Location"] = p.Location, ["TestingFrequency"] = p.TestingFrequency,
            ["TestCodes"] = FmtList(p.AssignedTestCodes), ["WaterDepartment"] = p.WaterDepartment?.Name ?? string.Empty,
        }).ToList();

    public override async Task<CreatedRecord> CreateAsync(RowReader r, ImportContext ctx)
    {
        int? deptId = null;
        if (r.OptionalText("WaterDepartment") is { } deptName)
            deptId = await ctx.Db.WaterDepartments.Where(d => d.Name.ToLower() == deptName.ToLower()).Select(d => (int?)d.Id).FirstOrDefaultAsync()
                ?? throw new RowError("WaterDepartment", $"No water department named '{deptName}'.");

        var codes = r.List("TestCodes");
        var known = await ctx.Db.TestDefinitions.Where(t => codes.Contains(t.Code)).Select(t => t.Code).ToListAsync();
        if (codes.Except(known).FirstOrDefault() is { } missing)
            throw new RowError("TestCodes", $"No test definition with code '{missing}'.");

        var created = await _service.CreateWaterSamplingPointAsync(new CreateWaterSamplingPointRequest(
            r.Text("Code"), r.Text("Location"), r.OptionalText("TestingFrequency") ?? string.Empty, codes.ToList(), deptId));
        return Created("WaterSamplingPoint", created.Id, r);
    }
}
```

If a request record lives in a namespace other than `MicroLIMS.Application.DTOs` (check with `git grep -n "record CreateRoomRequest" -- backend/MicroLIMS.Application`), add that `using`. Do not change the request types.

- [ ] **Step 5: Implement `Sheets/InventorySheets.cs`**

```csharp
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Services.MasterData;
using MicroLIMS.Domain.Enums;
using static MicroLIMS.Application.Services.MasterDataWorkbook.WorkbookValues;

namespace MicroLIMS.Application.Services.MasterDataWorkbook.Sheets;

internal static class SectionLookup
{
    public static async Task<int> IdAsync(IMicroLimsDbContext db, string code) =>
        await db.DocumentSections.Where(s => s.Code.ToLower() == code.ToLower()).Select(s => (int?)s.Id).FirstOrDefaultAsync()
        ?? throw new RowError("SectionCode", $"No laboratory section with code '{code}'.");

    public static async Task<IReadOnlyList<string>> CodesAsync(IMicroLimsDbContext db) =>
        await db.DocumentSections.Where(s => s.IsActive).OrderBy(s => s.Code).Select(s => s.Code).ToListAsync();
}

public sealed class MaterialMasterSheet : SheetHandler
{
    private readonly MaterialMasterService _service;
    public MaterialMasterSheet(MaterialMasterService service) { _service = service; }

    public override string Name => "MaterialMaster";
    public override IReadOnlyList<SheetColumn> Columns { get; } = new[]
    {
        new SheetColumn("SectionCode", Required: true, Help: "Code of the lab section that owns the entry."),
        new SheetColumn("Code", Required: true, Help: "Unique within the section, e.g. RG-011."),
        new SheetColumn("Name", Required: true),
        new SheetColumn("Category", Required: true, EnumType: typeof(MaterialMasterCategory)),
        new SheetColumn("Grade"),
        new SheetColumn("Source"),
        new SheetColumn("BaseUnit", Required: true, EnumType: typeof(MaterialUnit)),
        new SheetColumn("WorkingConcentration", Help: "Indicators only."),
        new SheetColumn("Solvent", Help: "Indicators only."),
        new SheetColumn("TransitionRangeFrom", Help: "Indicators only, pH."),
        new SheetColumn("TransitionRangeTo", Help: "Indicators only, pH."),
        new SheetColumn("ColourChange", Help: "Indicators only."),
        new SheetColumn("IndicatorUse", Help: "Indicators only."),
    };

    public override string KeyOf(IReadOnlyDictionary<string, string> c) => Key(Cell(c, "SectionCode"), Cell(c, "Code"));

    public override async Task<List<Dictionary<string, string>>> ExportAsync(IMicroLimsDbContext db) =>
        (await db.MaterialMasterEntries.AsNoTracking().Include(e => e.Section).OrderBy(e => e.Code).ToListAsync())
        .Select(e => new Dictionary<string, string>
        {
            ["SectionCode"] = e.Section?.Code ?? string.Empty, ["Code"] = e.Code, ["Name"] = e.Name,
            ["Category"] = e.Category.ToString(), ["Grade"] = Fmt(e.Grade), ["Source"] = Fmt(e.Source),
            ["BaseUnit"] = e.BaseUnit.ToString(), ["WorkingConcentration"] = Fmt(e.WorkingConcentration),
            ["Solvent"] = Fmt(e.Solvent), ["TransitionRangeFrom"] = Fmt(e.TransitionRangeFrom),
            ["TransitionRangeTo"] = Fmt(e.TransitionRangeTo), ["ColourChange"] = Fmt(e.ColourChange),
            ["IndicatorUse"] = Fmt(e.IndicatorUse),
        }).ToList();

    public override async Task<IReadOnlyList<string>> DbChoicesAsync(IMicroLimsDbContext db, string column) =>
        column == "SectionCode" ? await SectionLookup.CodesAsync(db) : Array.Empty<string>();

    // Loaded inactive: the second-person verification of the import
    // activates it (MasterDataWorkbookService.VerifyAsync).
    public override async Task<CreatedRecord> CreateAsync(RowReader r, ImportContext ctx)
    {
        var sectionId = await SectionLookup.IdAsync(ctx.Db, r.Text("SectionCode"));
        var created = await _service.CreateAsync(new SaveMaterialMasterEntryRequest(
            r.Text("Code"), r.Text("Name"), r.Enum<MaterialMasterCategory>("Category"),
            r.OptionalText("Grade"), r.OptionalText("Source"), r.Enum<MaterialUnit>("BaseUnit"), sectionId,
            r.OptionalText("WorkingConcentration"), r.OptionalText("Solvent"),
            r.Decimal("TransitionRangeFrom"), r.Decimal("TransitionRangeTo"),
            r.OptionalText("ColourChange"), r.OptionalText("IndicatorUse")), ctx.UserId);
        await _service.SetActiveAsync(created.Id, false, ctx.UserId);
        return Created("MaterialMasterEntry", created.Id, r);
    }
}

public sealed class MediaProductsSheet : SheetHandler
{
    private readonly MediaProductService _service;
    public MediaProductsSheet(MediaProductService service) { _service = service; }

    public override string Name => "MediaProducts";
    public override IReadOnlyList<SheetColumn> Columns { get; } = new[]
    {
        new SheetColumn("Code", Required: true, Help: "Short unique code used in lot numbers, e.g. TSA."),
        new SheetColumn("Name", Required: true, Help: "e.g. Tryptic Soy Agar."),
    };

    public override string KeyOf(IReadOnlyDictionary<string, string> c) => Key(Cell(c, "Code"));

    public override async Task<List<Dictionary<string, string>>> ExportAsync(IMicroLimsDbContext db) =>
        (await db.MediaProducts.AsNoTracking().OrderBy(p => p.Code).ToListAsync())
        .Select(p => new Dictionary<string, string> { ["Code"] = p.Code, ["Name"] = p.Name }).ToList();

    public override async Task<CreatedRecord> CreateAsync(RowReader r, ImportContext ctx)
    {
        var created = await _service.CreateAsync(r.Text("Name"), r.Text("Code"));
        return Created("MediaProduct", created.Id, r);
    }
}

public sealed class EquipmentSheet : SheetHandler
{
    private readonly EquipmentMasterDataService _service;
    public EquipmentSheet(EquipmentMasterDataService service) { _service = service; }

    public override string Name => "Equipment";
    public override IReadOnlyList<SheetColumn> Columns { get; } = new[]
    {
        new SheetColumn("Code", Required: true, Help: "Asset or equipment ID, e.g. INC-03."),
        new SheetColumn("Name", Required: true),
        new SheetColumn("Type", Required: true, EnumType: typeof(EquipmentType)),
        new SheetColumn("SectionCode", Help: "Lab section that uses it."),
        new SheetColumn("Location"),
        new SheetColumn("SetPointTemperature", Help: "Incubators: °C, dot decimal."),
        new SheetColumn("CalibrationDueDate", Help: "yyyy-MM-dd."),
        new SheetColumn("Vendor"),
        new SheetColumn("CdsSoftware", EnumType: typeof(CdsSoftware), Help: "Chromatography/ICP instruments only."),
    };

    public override string KeyOf(IReadOnlyDictionary<string, string> c) => Key(Cell(c, "Code"));

    public override async Task<List<Dictionary<string, string>>> ExportAsync(IMicroLimsDbContext db) =>
        (await db.Equipment.AsNoTracking().Include(e => e.Section).OrderBy(e => e.Code).ToListAsync())
        .Select(e => new Dictionary<string, string>
        {
            ["Code"] = e.Code, ["Name"] = e.Name, ["Type"] = e.Type.ToString(),
            ["SectionCode"] = e.Section?.Code ?? string.Empty, ["Location"] = Fmt(e.Location),
            ["SetPointTemperature"] = Fmt(e.SetPointTemperature), ["CalibrationDueDate"] = Fmt(e.CalibrationDueDate),
            ["Vendor"] = Fmt(e.Vendor), ["CdsSoftware"] = Fmt(e.CdsSoftware),
        }).ToList();

    public override async Task<IReadOnlyList<string>> DbChoicesAsync(IMicroLimsDbContext db, string column) =>
        column == "SectionCode" ? await SectionLookup.CodesAsync(db) : Array.Empty<string>();

    public override async Task<CreatedRecord> CreateAsync(RowReader r, ImportContext ctx)
    {
        int? sectionId = r.OptionalText("SectionCode") is { } code ? await SectionLookup.IdAsync(ctx.Db, code) : null;
        var created = await _service.CreateEquipmentAsync(ctx.UserId, new CreateEquipmentRequest(
            r.Text("Name"), r.Text("Code"), r.Enum<EquipmentType>("Type"), r.OptionalText("Location"),
            r.Decimal("SetPointTemperature"), r.Date("CalibrationDueDate"), r.OptionalText("Vendor"),
            r.OptionalEnum<CdsSoftware>("CdsSoftware"), null, sectionId));
        return Created("Equipment", created.Id, r);
    }
}

public sealed class ItemsSheet : SheetHandler
{
    private readonly ItemService _service;
    public ItemsSheet(ItemService service) { _service = service; }

    public override string Name => "Items";
    public override IReadOnlyList<SheetColumn> Columns { get; } = new[]
    {
        new SheetColumn("Code", Required: true, Help: "Product or material code, unique."),
        new SheetColumn("Name", Required: true),
        new SheetColumn("Category", Required: true, EnumType: typeof(SampleCategory), Help: "FinishedProduct, RawMaterial or PackagingMaterial."),
        new SheetColumn("SopNumber"),
        new SheetColumn("TestCodes", IsList: true, Help: "Tests assigned on receipt, from Test Master, separated by ;."),
    };

    public override string KeyOf(IReadOnlyDictionary<string, string> c) => Key(Cell(c, "Code"));

    public override async Task<List<Dictionary<string, string>>> ExportAsync(IMicroLimsDbContext db) =>
        (await db.Items.AsNoTracking().Include(i => i.AssignedTests).OrderBy(i => i.Code).ToListAsync())
        .Select(i => new Dictionary<string, string>
        {
            ["Code"] = i.Code, ["Name"] = i.Name, ["Category"] = i.Category.ToString(),
            ["SopNumber"] = Fmt(i.SopNumber), ["TestCodes"] = FmtList(i.AssignedTests.Select(t => t.TestCode)),
        }).ToList();

    // Loaded inactive: an inactive item cannot receive samples
    // (ProductWorkflowEngine.ReceiveAsync), so nothing is tested against
    // it until a second person verifies the import.
    public override async Task<CreatedRecord> CreateAsync(RowReader r, ImportContext ctx)
    {
        var codes = r.List("TestCodes");
        var tests = await ctx.Db.TestDefinitions.Where(t => codes.Contains(t.Code)).Select(t => new { t.Code, t.DisplayName }).ToListAsync();
        if (codes.FirstOrDefault(c => tests.All(t => t.Code != c)) is { } missing)
            throw new RowError("TestCodes", $"No test definition with code '{missing}'. Create it in Test Master first.");

        var created = await _service.CreateAsync(new ItemSaveRequest(
            r.Text("Name"), r.Text("Code"), r.Enum<SampleCategory>("Category"), r.OptionalText("SopNumber"),
            codes.Select(c => new ItemTestRequest(c, tests.First(t => t.Code == c).DisplayName)).ToList()));
        await _service.SetActiveAsync(created.Id, false);
        return Created("Item", created.Id, r);
    }
}
```

- [ ] **Step 6: Implement `Sheets/SpecificationSheets.cs`**

```csharp
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Services.MasterData;
using MicroLIMS.Domain.Enums;
using static MicroLIMS.Application.Services.MasterDataWorkbook.WorkbookValues;

namespace MicroLIMS.Application.Services.MasterDataWorkbook.Sheets;

public sealed class SpecificationsSheet : SheetHandler
{
    public const string SheetName = "Specifications";
    private readonly SpecificationMasterDataService _service;
    public SpecificationsSheet(SpecificationMasterDataService service) { _service = service; }

    public override string Name => SheetName;
    public override IReadOnlyList<SheetColumn> Columns { get; } = new[]
    {
        new SheetColumn("ItemCode", Required: true, SourceSheet: "Items", SourceColumn: "Code"),
        new SheetColumn("TestCode", Required: true, Help: "Code from Test Master. The test must exist before this sheet is loaded."),
        new SheetColumn("ParameterName", Required: true, Help: "What is reported, e.g. TAMC or Assay of Paracetamol. Unique per item, test and stage."),
        new SheetColumn("ProductionStageRole", EnumType: typeof(ProductionStageRole), Help: "Blank = applies to every stage."),
        new SheetColumn("DisplayOrder"),
        new SheetColumn("LimitType", Required: true, EnumType: typeof(LimitType)),
        new SheetColumn("LowerLimit"), new SheetColumn("UpperLimit"),
        new SheetColumn("LowerInclusive", Help: "Yes/No, default Yes."), new SheetColumn("UpperInclusive", Help: "Yes/No, default Yes."),
        new SheetColumn("Target"), new SheetColumn("Tolerance"),
        new SheetColumn("ToleranceMode", EnumType: typeof(ToleranceMode)),
        new SheetColumn("AlertLimit", Help: "Count-tiered tests, as text, e.g. 50."),
        new SheetColumn("ActionLimit"), new SheetColumn("SpecLimit"),
        new SheetColumn("Unit", Help: "e.g. CFU/g, %."),
        new SheetColumn("DilutionFactor"),
        new SheetColumn("ExpectedResultText", Help: "Qualitative tests: the text that passes."),
        new SheetColumn("ExpectedState", EnumType: typeof(ExpectedPresence), Help: "Presence/absence tests."),
        new SheetColumn("ReferenceStandard", Help: "e.g. USP <61>."),
        new SheetColumn("SampleQuantity"), new SheetColumn("SampleQuantityUnit"),
        new SheetColumn("ResultBasis", EnumType: typeof(ResultBasis)),
        new SheetColumn("LabelClaim"), new SheetColumn("LabelClaimUnit"),
        new SheetColumn("DosageForm", EnumType: typeof(DosageForm)),
        new SheetColumn("HplcAnalyte", Help: "Method abbreviation / analyte name, e.g. PCM / Paracetamol. The method must exist."),
        new SheetColumn("IcpElement", Help: "Method abbreviation / element symbol, e.g. ICP1 / Zn. The method must exist."),
    };

    public static string SpecKey(string? item, string? test, string? parameter, string? role) => Key(item, test, parameter, role);

    public override string KeyOf(IReadOnlyDictionary<string, string> c) =>
        SpecKey(Cell(c, "ItemCode"), Cell(c, "TestCode"), Cell(c, "ParameterName"), Cell(c, "ProductionStageRole"));

    public override async Task<List<Dictionary<string, string>>> ExportAsync(IMicroLimsDbContext db) =>
        (await db.Specifications.AsNoTracking()
            .Include(s => s.Item)
            .Include(s => s.HplcMethodAnalyte).ThenInclude(a => a!.HplcMethod)
            .Include(s => s.IcpMethodElement).ThenInclude(e => e!.IcpMethod)
            .OrderBy(s => s.Item!.Code).ThenBy(s => s.TestCode).ThenBy(s => s.DisplayOrder)
            .ToListAsync())
        .Select(s => new Dictionary<string, string>
        {
            ["ItemCode"] = s.Item?.Code ?? string.Empty, ["TestCode"] = s.TestCode, ["ParameterName"] = s.ParameterName,
            ["ProductionStageRole"] = Fmt(s.ProductionStageRole), ["DisplayOrder"] = Fmt(s.DisplayOrder),
            ["LimitType"] = s.LimitType.ToString(), ["LowerLimit"] = Fmt(s.LowerLimit), ["UpperLimit"] = Fmt(s.UpperLimit),
            ["LowerInclusive"] = Fmt(s.LowerInclusive), ["UpperInclusive"] = Fmt(s.UpperInclusive),
            ["Target"] = Fmt(s.Target), ["Tolerance"] = Fmt(s.Tolerance), ["ToleranceMode"] = Fmt(s.ToleranceMode),
            ["AlertLimit"] = s.AlertLimit, ["ActionLimit"] = s.ActionLimit, ["SpecLimit"] = s.SpecLimit, ["Unit"] = s.Unit,
            ["DilutionFactor"] = Fmt(s.DilutionFactor), ["ExpectedResultText"] = Fmt(s.ExpectedResultText),
            ["ExpectedState"] = Fmt(s.ExpectedState), ["ReferenceStandard"] = Fmt(s.ReferenceStandard),
            ["SampleQuantity"] = Fmt(s.SampleQuantity), ["SampleQuantityUnit"] = Fmt(s.SampleQuantityUnit),
            ["ResultBasis"] = Fmt(s.ResultBasis), ["LabelClaim"] = Fmt(s.LabelClaim), ["LabelClaimUnit"] = Fmt(s.LabelClaimUnit),
            ["DosageForm"] = Fmt(s.DosageForm),
            ["HplcAnalyte"] = s.HplcMethodAnalyte is { } a ? $"{a.HplcMethod?.Abbreviation} / {a.Name}" : string.Empty,
            ["IcpElement"] = s.IcpMethodElement is { } e ? $"{e.IcpMethod?.Abbreviation} / {e.Symbol}" : string.Empty,
        }).ToList();

    public override async Task<IReadOnlyList<string>> DbChoicesAsync(IMicroLimsDbContext db, string column) => column switch
    {
        "TestCode" => await db.TestDefinitions.Where(t => t.IsActive).OrderBy(t => t.Code).Select(t => t.Code).ToListAsync(),
        _ => Array.Empty<string>(),
    };

    public override async Task<CreatedRecord> CreateAsync(RowReader r, ImportContext ctx)
    {
        var itemCode = r.Text("ItemCode");
        var itemId = await ctx.Db.Items.Where(i => i.Code == itemCode).Select(i => (int?)i.Id).FirstOrDefaultAsync()
            ?? throw new RowError("ItemCode", $"No item with code '{itemCode}'. Add it on the Items sheet.");
        var testCode = r.Text("TestCode");
        if (!await ctx.Db.TestDefinitions.AnyAsync(t => t.Code == testCode))
            throw new RowError("TestCode", $"No test definition with code '{testCode}'. Create it in Test Master first.");
        var parameter = r.Text("ParameterName");
        var role = r.OptionalEnum<ProductionStageRole>("ProductionStageRole");
        var key = SpecKey(itemCode, testCode, parameter, role?.ToString());

        var created = await _service.CreateSpecificationAsync(ctx.UserId, new CreateSpecificationRequest(
            ItemId: itemId, TestCode: testCode,
            AlertLimit: r.OptionalText("AlertLimit"), ActionLimit: r.OptionalText("ActionLimit"), SpecLimit: r.OptionalText("SpecLimit"),
            Unit: r.OptionalText("Unit"), DilutionFactor: r.Decimal("DilutionFactor"), ParameterName: parameter,
            DisplayOrder: r.Int("DisplayOrder"), LimitType: r.Enum<LimitType>("LimitType"),
            ReferenceStandard: r.OptionalText("ReferenceStandard"), LowerLimit: r.Decimal("LowerLimit"), UpperLimit: r.Decimal("UpperLimit"),
            LowerInclusive: r.Bool("LowerInclusive"), UpperInclusive: r.Bool("UpperInclusive"),
            Target: r.Decimal("Target"), Tolerance: r.Decimal("Tolerance"), ToleranceMode: r.OptionalEnum<ToleranceMode>("ToleranceMode"),
            ExpectedResultText: r.OptionalText("ExpectedResultText"), ExpectedState: r.OptionalEnum<ExpectedPresence>("ExpectedState"),
            SampleQuantity: r.Decimal("SampleQuantity"), SampleQuantityUnit: r.OptionalText("SampleQuantityUnit"),
            Stages: StagesFor(ctx, key),
            ResultBasis: r.OptionalEnum<ResultBasis>("ResultBasis"), LabelClaim: r.Decimal("LabelClaim"),
            LabelClaimUnit: r.OptionalText("LabelClaimUnit"), DosageForm: r.OptionalEnum<DosageForm>("DosageForm"),
            HplcMethodAnalyteId: await AnalyteIdAsync(ctx.Db, r.OptionalText("HplcAnalyte")),
            ProductionStageRole: role,
            IcpMethodElementId: await ElementIdAsync(ctx.Db, r.OptionalText("IcpElement"))));

        ctx.CreatedSpecificationKeys.Add(key);
        return Created("Specification", created.Id, r);
    }

    // Stage rows travel with their specification: the service validates a
    // multi-stage specification together with its stages.
    private static List<SpecificationStageDto>? StagesFor(ImportContext ctx, string specKey)
    {
        var sheet = ctx.Sheet(SpecificationStagesSheet.SheetName);
        if (sheet is null) return null;
        var stages = sheet.Rows
            .Where(row => SpecificationStagesSheet.ParentKey(row.Cells) == specKey)
            .Select(row =>
            {
                var s = new RowReader(row);
                try
                {
                    return new SpecificationStageDto(null, s.RequiredInt("StageNumber"), s.Text("StageLabel"), s.Text("AcceptanceCriteria"));
                }
                catch (RowError e)
                {
                    throw new RowError(e.Column, $"{SpecificationStagesSheet.SheetName} row {row.RowNumber}: {e.Message}");
                }
            })
            .ToList();
        return stages.Count == 0 ? null : stages;
    }

    private static async Task<int?> AnalyteIdAsync(IMicroLimsDbContext db, string? text)
    {
        if (text is null) return null;
        var (abbr, name) = Split(text, "HplcAnalyte");
        return await db.HplcMethodAnalytes.Where(a => a.HplcMethod!.Abbreviation == abbr && a.Name == name).Select(a => (int?)a.Id).FirstOrDefaultAsync()
            ?? throw new RowError("HplcAnalyte", $"No HPLC method '{abbr}' with analyte '{name}'.");
    }

    private static async Task<int?> ElementIdAsync(IMicroLimsDbContext db, string? text)
    {
        if (text is null) return null;
        var (abbr, symbol) = Split(text, "IcpElement");
        return await db.IcpMethodElements.Where(e => e.IcpMethod!.Abbreviation == abbr && e.Symbol == symbol).Select(e => (int?)e.Id).FirstOrDefaultAsync()
            ?? throw new RowError("IcpElement", $"No ICP method '{abbr}' with element '{symbol}'.");
    }

    private static (string, string) Split(string text, string column)
    {
        var parts = text.Split('/', 2, StringSplitOptions.TrimEntries);
        return parts.Length == 2 && parts[0].Length > 0 && parts[1].Length > 0
            ? (parts[0], parts[1])
            : throw new RowError(column, $"{column} must be written as 'METHOD / name'.");
    }
}

public sealed class SpecificationStagesSheet : SheetHandler
{
    public const string SheetName = "SpecificationStages";

    public override string Name => SheetName;
    public override IReadOnlyList<SheetColumn> Columns { get; } = new[]
    {
        new SheetColumn("ItemCode", Required: true), new SheetColumn("TestCode", Required: true),
        new SheetColumn("ParameterName", Required: true),
        new SheetColumn("ProductionStageRole", EnumType: typeof(ProductionStageRole)),
        new SheetColumn("StageNumber", Required: true, Help: "1, 2, 3 ..."),
        new SheetColumn("StageLabel", Required: true, Help: "e.g. S1."),
        new SheetColumn("AcceptanceCriteria", Required: true),
    };

    public static string ParentKey(IReadOnlyDictionary<string, string> c) =>
        SpecificationsSheet.SpecKey(Cell(c, "ItemCode"), Cell(c, "TestCode"), Cell(c, "ParameterName"), Cell(c, "ProductionStageRole"));

    public override string KeyOf(IReadOnlyDictionary<string, string> c) => ParentKey(c) + "|" + Key(Cell(c, "StageNumber"));

    public override async Task<List<Dictionary<string, string>>> ExportAsync(IMicroLimsDbContext db) =>
        (await db.SpecificationStages.AsNoTracking().Include(s => s.Specification).ThenInclude(s => s!.Item)
            .OrderBy(s => s.Specification!.Item!.Code).ThenBy(s => s.Specification!.TestCode).ThenBy(s => s.StageNumber)
            .ToListAsync())
        .Select(s => new Dictionary<string, string>
        {
            ["ItemCode"] = s.Specification?.Item?.Code ?? string.Empty, ["TestCode"] = s.Specification?.TestCode ?? string.Empty,
            ["ParameterName"] = s.Specification?.ParameterName ?? string.Empty,
            ["ProductionStageRole"] = Fmt(s.Specification?.ProductionStageRole),
            ["StageNumber"] = Fmt(s.StageNumber), ["StageLabel"] = s.StageLabel, ["AcceptanceCriteria"] = s.AcceptanceCriteriaText,
        }).ToList();

    public override async Task<CreatedRecord> CreateAsync(RowReader r, ImportContext ctx)
    {
        var parent = ParentKey(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ItemCode"] = r.Text("ItemCode"), ["TestCode"] = r.Text("TestCode"), ["ParameterName"] = r.Text("ParameterName"),
            ["ProductionStageRole"] = r.OptionalText("ProductionStageRole") ?? string.Empty,
        });
        if (!ctx.CreatedSpecificationKeys.Contains(parent))
            throw new RowError(null, "Stages load only together with a new row on the Specifications sheet. To change the stages of an existing specification, use the Specifications screen.");

        var itemCode = r.Text("ItemCode"); var testCode = r.Text("TestCode"); var parameter = r.Text("ParameterName");
        var stageNumber = r.RequiredInt("StageNumber");
        var id = await ctx.Db.SpecificationStages
            .Where(s => s.StageNumber == stageNumber && s.Specification!.Item!.Code == itemCode
                        && s.Specification.TestCode == testCode && s.Specification.ParameterName == parameter)
            .Select(s => s.Id).FirstAsync();
        return Created("SpecificationStage", id, r);
    }
}
```

The navigation properties used above (`Specification.Item`, `Specification.HplcMethodAnalyte`, `HplcMethodAnalyte.HplcMethod`, `IcpMethodElement.IcpMethod`, `SpecificationStage.Specification`, `MaterialMasterEntry.Section`, `Equipment.Section`, `WaterSamplingPoint.WaterDepartment`, `Room.Department`) exist in `backend/MicroLIMS.Domain/Entities`. Confirm each with `git grep -n "public .* <Nav>? <Nav>" -- backend/MicroLIMS.Domain/Entities` before compiling.

- [ ] **Step 7: Implement `MasterDataSheets.cs`**

```csharp
using MicroLIMS.Application.Services.MasterData;
using MicroLIMS.Application.Services.MasterDataWorkbook.Sheets;

namespace MicroLIMS.Application.Services.MasterDataWorkbook;

// The workbook's sheets in load order: a sheet may refer only to sheets
// above it (or to records already in the database).
public static class MasterDataSheets
{
    public static IReadOnlyList<SheetHandler> Create(
        OrganismMasterDataService organisms,
        ReferenceListMasterDataService referenceLists,
        EnvironmentalMonitoringMasterDataService em,
        WaterMasterDataService water,
        MaterialMasterService materialMaster,
        MediaProductService mediaProducts,
        EquipmentMasterDataService equipment,
        ItemService items,
        SpecificationMasterDataService specifications) => new SheetHandler[]
    {
        new OrganismsSheet(organisms),
        new ProductionStagesSheet(referenceLists),
        new EmDepartmentsSheet(em),
        new EmRoomsSheet(em),
        new WaterDepartmentsSheet(water),
        new WaterPointsSheet(water),
        new MaterialMasterSheet(materialMaster),
        new MediaProductsSheet(mediaProducts),
        new EquipmentSheet(equipment),
        new ItemsSheet(items),
        new SpecificationsSheet(specifications),
        new SpecificationStagesSheet(),
    };

    public static readonly IReadOnlyList<string> Instructions = new[]
    {
        "MicroLIMS master data workbook",
        "1. Each sheet is one kind of record. Row 1 is the header - do not rename or reorder the headers.",
        "2. Rows already in MicroLIMS are listed. Add new rows underneath; leave existing rows as they are.",
        "   The workbook only ADDS records. To change an existing record, use its screen in MicroLIMS.",
        "3. Use the drop-down lists where a column has one. Lists are separated by ; (for example TAMC;TYMC).",
        "4. Numbers use a dot as the decimal separator (0.5). Dates are written yyyy-MM-dd (2027-03-31).",
        "5. Load order: sheets 1-10 first. Then create test definitions and HPLC/GC/ICP methods in MicroLIMS.",
        "   Then fill Specifications and SpecificationStages and load the workbook again.",
        "6. In MicroLIMS: Master Data Workbook -> Check file. Fix every error listed, then Load and sign.",
        "7. Items and material master entries are loaded INACTIVE. A second person checks the load against",
        "   the approved source documents and signs Verify; that activates them.",
        "8. Required columns are marked Yes in the table below.",
    };
}
```

- [ ] **Step 8: Build and run the unit tests**

```bash
dotnet build backend/MicroLIMS.API --artifacts-path E:/MicroLIMS/rls-tmp/mdw-t3
dotnet test backend/MicroLIMS.Tests --artifacts-path E:/MicroLIMS/rls-tmp/mdw-t3 --filter "FullyQualifiedName~MasterDataWorkbook"
```

Expected: build succeeds with no new warnings; all workbook unit tests pass, including `DifferentColumns_IgnoresCaseTrailingZerosAndListOrder`.

- [ ] **Step 9: Commit**

```bash
git add backend/MicroLIMS.Application/Services/MasterDataWorkbook backend/MicroLIMS.Tests/UnitTests/MasterDataWorkbook
git commit -F - <<'EOF'
feat(master-data): workbook sheets for flat lists and specifications

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
EOF
```

---

### Task 4: Download endpoint (service read side + controller GET)

**Files:**
- Create: `backend/MicroLIMS.Application/Services/MasterDataWorkbook/MasterDataWorkbookService.cs` (read side only in this task)
- Create: `backend/MicroLIMS.API/Controllers/MasterDataWorkbookController.cs` (GET only in this task)
- Modify: `backend/MicroLIMS.API/Extensions/ServiceCollectionExtensions.cs`
- Modify: `backend/MicroLIMS.Tests/ArchitectureTests/authorization-matrix.txt`
- Test: `backend/MicroLIMS.Tests/IntegrationTests/MasterDataWorkbookPostgresIntegrationTests.cs`

**Interfaces:**
- Consumes: `IWorkbookFormat`, `MasterDataSheets`, all handlers.
- Produces: `MasterDataWorkbookService.DownloadAsync() : Task<(byte[] Content, string FileName)>`; `MasterDataWorkbookService.XlsxContentType`; internal `ReadSheets(byte[] file) : IReadOnlyList<WorkbookSheet>` used by Task 6.

- [ ] **Step 1: Write the failing integration test**

```csharp
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.MasterData;
using MicroLIMS.Application.Services.MasterDataWorkbook;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Infrastructure.Workbook;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

[Collection("PostgresDatabaseCollection")]
public class MasterDataWorkbookPostgresIntegrationTests
{
    private const string Password = "Workbook-Signer-1!";
    private readonly PostgresTestFixture _fixture;

    public MasterDataWorkbookPostgresIntegrationTests(PostgresTestFixture fixture) { _fixture = fixture; }

    internal static MasterDataWorkbookService Service(MicroLimsDbContext db, InMemoryFileStorageService? storage = null)
    {
        var scope = new UserSectionScopeService(db);
        var signatures = new ElectronicSignatureService(db);
        var audit = new AuditEventService(db, new MicroLIMS.Persistence.Helpers.DatabaseSequenceHelper(db));
        return new MasterDataWorkbookService(
            db, new ClosedXmlWorkbookFormat(), storage ?? new InMemoryFileStorageService(), signatures, TimeProvider.System,
            new OrganismMasterDataService(db), new ReferenceListMasterDataService(db),
            new EnvironmentalMonitoringMasterDataService(db), new WaterMasterDataService(db),
            new MaterialMasterService(db, scope), new MediaProductService(db, signatures, audit),
            new EquipmentMasterDataService(db, scope), new ItemService(db),
            new SpecificationMasterDataService(db, scope));
    }

    [PostgresFact]
    public async Task Download_HasAllSheetsInLoadOrder_AndCurrentRows()
    {
        await using var db = _fixture.CreateDbContext();
        var tag = Guid.NewGuid().ToString("N")[..8];
        db.Organisms.Add(new Organism { ScientificName = "Dl organism " + tag });
        await db.SaveChangesAsync();

        var (content, fileName) = await Service(db).DownloadAsync();

        Assert.EndsWith(".xlsx", fileName);
        using var wb = new XLWorkbook(new MemoryStream(content));
        Assert.Equal(
            new[] { "Instructions", "Organisms", "ProductionStages", "EmDepartments", "EmRooms", "WaterDepartments", "WaterPoints",
                    "MaterialMaster", "MediaProducts", "Equipment", "Items", "Specifications", "SpecificationStages", "Lists" },
            wb.Worksheets.Select(w => w.Name));
        Assert.Contains(wb.Worksheet("Organisms").Column(1).CellsUsed(), c => c.GetString() == "Dl organism " + tag);
    }
}
```

`DatabaseSequenceHelper` is the type `_fixture.CreateSequenceHelper(db)` returns; if its name differs, use `_fixture.CreateSequenceHelper(db)` instead.

- [ ] **Step 2: Run it to confirm it fails** (build error: `MasterDataWorkbookService` missing)

```bash
dotnet test backend/MicroLIMS.Tests --artifacts-path E:/MicroLIMS/rls-tmp/mdw-t4 --filter "FullyQualifiedName~MasterDataWorkbookPostgres"
```

- [ ] **Step 3: Implement the read side of `MasterDataWorkbookService.cs`**

```csharp
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Abstractions.Storage;
using MicroLIMS.Application.Abstractions.Workbook;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services.MasterData;

namespace MicroLIMS.Application.Services.MasterDataWorkbook;

// Master data workbook: download the current data in workbook form, check
// an uploaded workbook (dry run), load it (signed), and verify a load
// (second person, signed). Every record is created by the existing
// master-data service for its type - see SheetHandler.
public class MasterDataWorkbookService
{
    public const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    public const long MaxFileBytes = 10 * 1024 * 1024;

    private readonly IMicroLimsDbContext _db;
    private readonly IWorkbookFormat _format;
    private readonly IFileStorageService _storage;
    private readonly IElectronicSignatureService _signatures;
    private readonly TimeProvider _time;
    private readonly ItemService _items;
    private readonly MaterialMasterService _materialMaster;
    private readonly IReadOnlyList<SheetHandler> _sheets;

    public MasterDataWorkbookService(
        IMicroLimsDbContext db,
        IWorkbookFormat format,
        IFileStorageService storage,
        IElectronicSignatureService signatures,
        TimeProvider time,
        OrganismMasterDataService organisms,
        ReferenceListMasterDataService referenceLists,
        EnvironmentalMonitoringMasterDataService em,
        WaterMasterDataService water,
        MaterialMasterService materialMaster,
        MediaProductService mediaProducts,
        EquipmentMasterDataService equipment,
        ItemService items,
        SpecificationMasterDataService specifications)
    {
        _db = db;
        _format = format;
        _storage = storage;
        _signatures = signatures;
        _time = time;
        _items = items;
        _materialMaster = materialMaster;
        _sheets = MasterDataSheets.Create(organisms, referenceLists, em, water, materialMaster, mediaProducts, equipment, items, specifications);
    }

    public async Task<(byte[] Content, string FileName)> DownloadAsync()
    {
        var sheets = new List<WorkbookSheet>();
        var choices = new List<WorkbookColumnChoices>();
        var help = new List<WorkbookInstructionRow>();

        foreach (var handler in _sheets)
        {
            var columns = handler.Columns.Select(c => c.Name).ToList();
            var rows = (await handler.ExportAsync(_db))
                .Select((cells, i) => new WorkbookRow(i + 2, cells))
                .ToList();
            sheets.Add(new WorkbookSheet(handler.Name, columns, rows));

            foreach (var column in handler.Columns)
            {
                help.Add(new WorkbookInstructionRow(handler.Name, column.Name, column.Required ? "Yes" : "", column.Help));
                if (column.EnumType is not null)
                    choices.Add(new WorkbookColumnChoices(handler.Name, column.Name, Values: Enum.GetNames(column.EnumType)));
                else if (column.SourceSheet is not null)
                    choices.Add(new WorkbookColumnChoices(handler.Name, column.Name, SourceSheet: column.SourceSheet, SourceColumn: column.SourceColumn));
                else if (await handler.DbChoicesAsync(_db, column.Name) is { Count: > 0 } values)
                    choices.Add(new WorkbookColumnChoices(handler.Name, column.Name, Values: values));
            }
        }

        var bytes = _format.Write(new WorkbookContent(sheets, choices, MasterDataSheets.Instructions, help));
        var stamp = _time.GetUtcNow().UtcDateTime.ToString("yyyy-MM-dd");
        return (bytes, $"MicroLIMS-master-data-{stamp}.xlsx");
    }

    internal IReadOnlyList<WorkbookSheet> ReadSheets(byte[] file)
    {
        if (file.Length == 0)
            throw new InvalidOperationException("The file is empty.");
        if (file.Length > MaxFileBytes)
            throw new InvalidOperationException($"The file is larger than {MaxFileBytes / (1024 * 1024)} MB. Split it into several workbooks.");
        try
        {
            return _format.Read(new MemoryStream(file));
        }
        catch (WorkbookFormatException e)
        {
            throw new InvalidOperationException(e.Message);
        }
    }
}
```

- [ ] **Step 4: Register in DI** (`ServiceCollectionExtensions.cs`, after `services.AddScoped<TestStageReplicateMasterDataService>();`)

```csharp
        services.AddScoped<MicroLIMS.Application.Abstractions.Workbook.IWorkbookFormat, MicroLIMS.Infrastructure.Workbook.ClosedXmlWorkbookFormat>();
        services.AddScoped<MicroLIMS.Application.Services.MasterDataWorkbook.MasterDataWorkbookService>();
```

(Or add `using` lines at the top in the file's existing style.)

- [ ] **Step 5: Controller with the GET** (`MasterDataWorkbookController.cs`)

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.Services.MasterDataWorkbook;
using MicroLIMS.Shared.Constants;

namespace MicroLIMS.API.Controllers;

// Master data workbook. Maps HTTP to MasterDataWorkbookService; the rules
// live in the Application layer.
[ApiController]
[Route("api/masterdata/workbook")]
[Authorize]
public class MasterDataWorkbookController : ControllerBase
{
    private readonly MasterDataWorkbookService _service;

    public MasterDataWorkbookController(MasterDataWorkbookService service) { _service = service; }

    private int CurrentUserId => int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var uid) ? uid : 0;
    private string? IpAddress => HttpContext.Connection.RemoteIpAddress?.ToString();

    [Authorize(Policy = PermissionConstants.MasterDataManage)]
    [HttpGet]
    public async Task<IActionResult> Download()
    {
        var (content, fileName) = await _service.DownloadAsync();
        return File(content, MasterDataWorkbookService.XlsxContentType, fileName);
    }
}
```

- [ ] **Step 6: Authorization matrix.** Run the matrix test; it fails and prints the missing line. Add it to `authorization-matrix.txt` in alphabetical position:

```
MasterDataWorkbookController.Download(): SystemAdministrator,SectionHead
```

```bash
dotnet test backend/MicroLIMS.Tests --artifacts-path E:/MicroLIMS/rls-tmp/mdw-t4 --filter "FullyQualifiedName~ArchitectureTests|FullyQualifiedName~MasterDataWorkbook"
```

Expected: all pass (the Postgres test must show **passed**, not skipped).

- [ ] **Step 7: Manual check.** Start the API locally, sign in as admin and call `GET http://localhost:5000/api/masterdata/workbook` (or wait for the Task 9 page). Open the file in Excel: 12 data sheets, Instructions first, drop-downs on the Category/Type/LimitType columns, Department picks from EmDepartments.

- [ ] **Step 8: Commit, then STOP for review (end of Phase 1)**

```bash
git add backend
git commit -F - <<'EOF'
feat(master-data): download the master data workbook

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
EOF
```

---

## Phase 2: Write side (stop for review after Task 8)

### Task 5: Import record entities and migration

**Files:**
- Create: `backend/MicroLIMS.Domain/Enums/MasterDataImportStatus.cs`
- Create: `backend/MicroLIMS.Domain/Entities/MasterDataImport.cs`
- Create: `backend/MicroLIMS.Persistence/Configurations/MasterDataImportConfiguration.cs`
- Modify: `IMicroLimsDbContext.cs`, `MicroLimsDbContext.cs`, `ReviewGateService.cs` (`ReviewEntityTypes`)
- Generated: `backend/MicroLIMS.Persistence/Migrations/<timestamp>_AddMasterDataImports.cs`

**Interfaces:**
- Produces: `MasterDataImport`, `MasterDataImportRecord`, `MasterDataImportStatus { Loaded, Verified }`, `DbSet<MasterDataImport> MasterDataImports`, `DbSet<MasterDataImportRecord> MasterDataImportRecords`, `ReviewEntityTypes.MasterDataImport = "MasterDataImport"`.

- [ ] **Step 1: Enum**

```csharp
namespace MicroLIMS.Domain.Enums;

// Appended, never inserted: persisted as int.
public enum MasterDataImportStatus
{
    Loaded,
    Verified
}
```

- [ ] **Step 2: Entities** (`MasterDataImport.cs`)

```csharp
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

// One signed load of the master data workbook: who loaded which file
// (kept in file storage, identified by its SHA-256), what it created, and
// who verified it. The created rows themselves carry their own audit
// trail; this record ties them to the file they came from.
public class MasterDataImport
{
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FileSha256 { get; set; } = string.Empty;
    public string StorageKey { get; set; } = string.Empty;
    public int ImportedByUserId { get; set; }
    public User? ImportedByUser { get; set; }
    public DateTime ImportedAt { get; set; }
    public string? Comment { get; set; }
    // Per-sheet created/unchanged counts, JSON (WorkbookSheetSummary[]).
    public string SummaryJson { get; set; } = "[]";
    public MasterDataImportStatus Status { get; set; }
    public int? VerifiedByUserId { get; set; }
    public User? VerifiedByUser { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public string? VerificationComment { get; set; }
    public List<MasterDataImportRecord> Records { get; set; } = new();
}

public class MasterDataImportRecord
{
    public int Id { get; set; }
    public int MasterDataImportId { get; set; }
    public MasterDataImport? MasterDataImport { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public int EntityId { get; set; }
    public string Sheet { get; set; } = string.Empty;
    public int RowNumber { get; set; }
}
```

- [ ] **Step 3: Configuration** (`MasterDataImportConfiguration.cs`; copy the namespace and `using` style from `DataExportLogConfiguration.cs`)

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class MasterDataImportConfiguration : IEntityTypeConfiguration<MasterDataImport>
{
    public void Configure(EntityTypeBuilder<MasterDataImport> builder)
    {
        builder.Property(i => i.FileName).HasMaxLength(260).IsRequired();
        builder.Property(i => i.FileSha256).HasMaxLength(64).IsRequired();
        builder.Property(i => i.StorageKey).HasMaxLength(512).IsRequired();
        builder.Property(i => i.Comment).HasMaxLength(2000);
        builder.Property(i => i.VerificationComment).HasMaxLength(2000);
        builder.HasOne(i => i.ImportedByUser).WithMany().HasForeignKey(i => i.ImportedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(i => i.VerifiedByUser).WithMany().HasForeignKey(i => i.VerifiedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(i => i.Records).WithOne(r => r.MasterDataImport!).HasForeignKey(r => r.MasterDataImportId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class MasterDataImportRecordConfiguration : IEntityTypeConfiguration<MasterDataImportRecord>
{
    public void Configure(EntityTypeBuilder<MasterDataImportRecord> builder)
    {
        builder.Property(r => r.EntityType).HasMaxLength(64).IsRequired();
        builder.Property(r => r.Sheet).HasMaxLength(64).IsRequired();
        builder.HasIndex(r => new { r.EntityType, r.EntityId });
    }
}
```

`UserReferenceRegistry` (`backend/MicroLIMS.Application/Services/UserReferenceRegistry.cs`) lists every FK to `User`; a test fails if a new one is missing. Add two entries in the same style as the existing ones, disposition **Blocking** (a user who loaded or verified master data must not be deleted):

```csharp
new UserReferenceEntry(typeof(MasterDataImport), nameof(MasterDataImport.ImportedByUserId), UserReferenceDisposition.Blocking, "Loaded a master data workbook (GMP record)"),
new UserReferenceEntry(typeof(MasterDataImport), nameof(MasterDataImport.VerifiedByUserId), UserReferenceDisposition.Blocking, "Verified a master data workbook load (GMP record)"),
```

Use the disposition name that file actually defines for "blocks deletion"; read the file first.

- [ ] **Step 4: DbSets.** In `IMicroLimsDbContext.cs` next to `DataExportLogs`:

```csharp
    DbSet<MasterDataImport> MasterDataImports { get; }
    DbSet<MasterDataImportRecord> MasterDataImportRecords { get; }
```

In `MicroLimsDbContext.cs` next to `DataExportLogs`:

```csharp
    public DbSet<MasterDataImport> MasterDataImports => Set<MasterDataImport>();
    public DbSet<MasterDataImportRecord> MasterDataImportRecords => Set<MasterDataImportRecord>();
```

In `ReviewEntityTypes` (`ReviewGateService.cs`):

```csharp
    public const string MasterDataImport = "MasterDataImport";
```

- [ ] **Step 5: Generate the migration** (Release configuration, so a running API does not lock the build)

```bash
cd /e/MicroLIMS/MicroLIMS && dotnet ef migrations add AddMasterDataImports --project backend/MicroLIMS.Persistence --startup-project backend/MicroLIMS.API --configuration Release
```

Expected: `<timestamp>_AddMasterDataImports.cs` creates `MasterDataImports` and `MasterDataImportRecords` with the FKs and index above, and **nothing else**. If it touches other tables, stop: the model snapshot is out of date, and that must be solved first.

- [ ] **Step 6: Build and run the full backend suite** (proves the migration applies on a fresh database and the user registry is complete)

```bash
dotnet test backend/MicroLIMS.Tests --artifacts-path E:/MicroLIMS/rls-tmp/mdw-t5
```

Expected: everything passes; note the totals (passed / failed / skipped).

- [ ] **Step 7: Commit**

```bash
git add backend
git commit -F - <<'EOF'
feat(master-data): import record entities and migration

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
EOF
```

---

### Task 6: Dry run (runner + ValidateAsync)

**Files:**
- Create: `backend/MicroLIMS.Application/Services/MasterDataWorkbook/MasterDataWorkbookRunner.cs`
- Modify: `MasterDataWorkbookService.cs` (add `ValidateAsync`)
- Modify: `MasterDataWorkbookController.cs` (add `Validate`)
- Modify: `authorization-matrix.txt`
- Test: `MasterDataWorkbookPostgresIntegrationTests.cs`

**Interfaces:**
- Produces: `MasterDataWorkbookRunner(IMicroLimsDbContext db, IReadOnlyList<SheetHandler> sheets)`, `RunAsync(IReadOnlyList<WorkbookSheet> sheets, int userId) : Task<(WorkbookReport Report, List<CreatedRecord> Created)>` (caller owns the transaction); `MasterDataWorkbookRunner.MaxDataRows = 5000`; `MasterDataWorkbookService.ValidateAsync(byte[] file, int userId) : Task<WorkbookReport>`.

- [ ] **Step 1: Test helpers + failing tests** (add to the integration test class)

```csharp
    // Builds an .xlsx with the given sheets (header from the first row's keys).
    internal static byte[] Workbook(params (string Sheet, Dictionary<string, string>[] Rows)[] sheets)
    {
        var content = new WorkbookContent(
            sheets.Select(s => new WorkbookSheet(s.Sheet,
                s.Rows.SelectMany(r => r.Keys).Distinct().ToList(),
                s.Rows.Select((r, i) => new WorkbookRow(i + 2, r)).ToList())).ToList(),
            Array.Empty<WorkbookColumnChoices>(), Array.Empty<string>(), Array.Empty<WorkbookInstructionRow>());
        return new ClosedXmlWorkbookFormat().Write(content);
    }

    internal static Dictionary<string, string> R(params (string Col, string Val)[] cells) => cells.ToDictionary(c => c.Col, c => c.Val);

    private async Task<User> AdminAsync(MicroLimsDbContext db)
    {
        var role = await db.Roles.FirstAsync(r => r.Type == RoleType.SystemAdministrator);
        var user = new User
        {
            FullName = "Workbook Admin", Username = ("mdw_" + Guid.NewGuid().ToString("N"))[..15],
            PasswordHash = TestPasswords.Hash(Password), RoleId = role.Id, IsActive = true,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    [PostgresFact]
    public async Task Validate_ReportsCreatedCounts_AndLeavesNoRowsOrAudit()
    {
        await using var db = _fixture.CreateDbContext();
        var admin = await AdminAsync(db);
        var tag = Guid.NewGuid().ToString("N")[..8];
        var auditBefore = await db.AuditLogs.CountAsync();

        var report = await Service(db).ValidateAsync(Workbook(
            ("Organisms", new[] { R(("ScientificName", "Org " + tag)) }),
            ("EmDepartments", new[] { R(("Name", "Dept " + tag), ("Class", "Grade B")) }),
            ("EmRooms", new[] { R(("Department", "Dept " + tag), ("Name", "Room " + tag)) })), admin.Id);

        Assert.False(report.HasErrors, string.Join("\n", report.Errors.Select(e => $"{e.Sheet} {e.Row} {e.Column}: {e.Message}")));
        Assert.Equal(1, report.Sheets.Single(s => s.Sheet == "EmRooms").Created);
        Assert.False(await db.Organisms.AnyAsync(o => o.ScientificName == "Org " + tag));
        Assert.False(await db.Departments.AnyAsync(d => d.Name == "Dept " + tag));
        Assert.Equal(auditBefore, await db.AuditLogs.CountAsync());
    }

    [PostgresFact]
    public async Task OneBadRowDoesNotStopTheRest()
    {
        await using var db = _fixture.CreateDbContext();
        var admin = await AdminAsync(db);
        var tag = Guid.NewGuid().ToString("N")[..8];
        // An ATCC number already in use: the unique index (not a pre-check)
        // rejects it, which poisons the transaction without a savepoint.
        db.Organisms.Add(new Organism { ScientificName = "Existing " + tag, AtccNumber = "A" + tag });
        await db.SaveChangesAsync();

        var report = await Service(db).ValidateAsync(Workbook(("Organisms", new[]
        {
            R(("ScientificName", "Dup atcc " + tag), ("AtccNumber", "A" + tag)),
            R(("ScientificName", "Fine " + tag), ("AtccNumber", "")),
        })), admin.Id);

        var error = Assert.Single(report.Errors);
        Assert.Equal(2, error.Row);
        Assert.Equal(1, report.Sheets.Single().Created);
    }

    [PostgresFact]
    public async Task Validate_UnknownReference_NamesSheetRowAndColumn()
    {
        await using var db = _fixture.CreateDbContext();
        var admin = await AdminAsync(db);

        var report = await Service(db).ValidateAsync(Workbook(
            ("EmRooms", new[] { R(("Department", "No such dept"), ("Name", "R1")) })), admin.Id);

        var e = Assert.Single(report.Errors);
        Assert.Equal(("EmRooms", 2, "Department"), (e.Sheet, e.Row, e.Column));
    }

    [PostgresFact]
    public async Task Validate_CommaDecimal_IsAnError()
    {
        await using var db = _fixture.CreateDbContext();
        var admin = await AdminAsync(db);
        var tag = Guid.NewGuid().ToString("N")[..8];

        var report = await Service(db).ValidateAsync(Workbook(("Equipment", new[]
        {
            R(("Code", "INC-" + tag), ("Name", "Incubator"), ("Type", "Incubator"), ("SetPointTemperature", "32,5")),
        })), admin.Id);

        Assert.Equal("SetPointTemperature", Assert.Single(report.Errors).Column);
    }

    [PostgresFact]
    public async Task Validate_DuplicateRowInSheet_IsAnError()
    {
        await using var db = _fixture.CreateDbContext();
        var admin = await AdminAsync(db);
        var tag = Guid.NewGuid().ToString("N")[..8];

        var report = await Service(db).ValidateAsync(Workbook(("MediaProducts", new[]
        {
            R(("Code", "M" + tag), ("Name", "One " + tag)), R(("Code", "m" + tag), ("Name", "Two " + tag)),
        })), admin.Id);

        Assert.Equal(3, Assert.Single(report.Errors).Row);
    }

    [PostgresFact]
    public async Task Validate_UnknownSheet_IsAnError()
    {
        await using var db = _fixture.CreateDbContext();
        var admin = await AdminAsync(db);
        var report = await Service(db).ValidateAsync(Workbook(("Sheet1", new[] { R(("A", "x")) })), admin.Id);
        Assert.Contains("Unknown sheet", Assert.Single(report.Errors).Message);
    }
```

- [ ] **Step 2: Run them to confirm they fail** (build error: `ValidateAsync` missing)

```bash
dotnet test backend/MicroLIMS.Tests --artifacts-path E:/MicroLIMS/rls-tmp/mdw-t6 --filter "FullyQualifiedName~MasterDataWorkbookPostgres"
```

- [ ] **Step 3: Implement `MasterDataWorkbookRunner.cs`**

```csharp
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

        var rowCount = sheets.Sum(s => s.Rows.Count);
        if (rowCount > MaxDataRows)
        {
            errors.Add(new WorkbookRowError(string.Empty, 0, null, $"The workbook has {rowCount} rows; the limit is {MaxDataRows}. Split it into several uploads."));
            return (new WorkbookReport(summaries, errors), created);
        }

        _db.CurrentUserId = userId;
        var ctx = new ImportContext(_db, userId, sheets);

        // What existed BEFORE this load - computed up front, so rows created
        // by an earlier sheet (e.g. stages created with their specification)
        // are not mistaken for pre-existing ones.
        var existing = new Dictionary<string, Dictionary<string, Dictionary<string, string>>>();
        foreach (var handler in _sheets)
            existing[handler.Name] = (await handler.ExportAsync(_db))
                .GroupBy(handler.KeyOf)
                .ToDictionary(g => g.Key, g => g.First());
        _db.ChangeTracker.Clear();

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
                    errors.Add(new WorkbookRowError(sheet.Name, row.RowNumber, (e as RowError)?.Column, MessageOf(e)));
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
```

- [ ] **Step 4: Add `ValidateAsync` to the service**

Add the field `private readonly MasterDataWorkbookRunner _runner;` and, at the end of the constructor, `_runner = new MasterDataWorkbookRunner(db, _sheets);`. Then:

```csharp
    // Dry run: everything a load would do, then rolled back.
    public async Task<WorkbookReport> ValidateAsync(byte[] file, int userId)
    {
        var sheets = ReadSheets(file);
        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            var (report, _) = await _runner.RunAsync(sheets, userId);
            return report;
        }
        finally
        {
            await tx.RollbackAsync();
            _db.ChangeTracker.Clear();
        }
    }
```

- [ ] **Step 5: Controller action**

```csharp
    [Authorize(Policy = PermissionConstants.MasterDataManage)]
    [HttpPost("validate")]
    [RequestSizeLimit(MasterDataWorkbookService.MaxFileBytes + 1024 * 1024)]
    public async Task<IActionResult> Validate(IFormFile file) =>
        Ok(ApiResponse<object>.Ok(await _service.ValidateAsync(await ReadAsync(file), CurrentUserId)));

    private static async Task<byte[]> ReadAsync(IFormFile file)
    {
        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        return ms.ToArray();
    }
```

Add `using MicroLIMS.Shared.Responses;` at the top. Matrix line:

```
MasterDataWorkbookController.Validate(IFormFile): SystemAdministrator,SectionHead
```

- [ ] **Step 6: Run the tests** (same command as Step 2, then the ArchitectureTests filter). Expected: all pass, none skipped.

- [ ] **Step 7: Commit**

```bash
git add backend
git commit -F - <<'EOF'
feat(master-data): workbook dry run with a savepoint per row

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
EOF
```

---

### Task 7: Signed load, history, verification, stored file

**Files:**
- Create: `backend/MicroLIMS.Application/Services/MasterDataWorkbook/MasterDataWorkbookDtos.cs`
- Modify: `MasterDataWorkbookService.cs`, `MasterDataWorkbookController.cs`, `authorization-matrix.txt`
- Test: `MasterDataWorkbookPostgresIntegrationTests.cs`

**Interfaces:**
- Produces:
  - `MasterDataImportDto(int Id, string FileName, string FileSha256, string ImportedBy, DateTime ImportedAt, string? Comment, string Status, string? VerifiedBy, DateTime? VerifiedAt, IReadOnlyList<WorkbookSheetSummary> Sheets, IReadOnlyList<string> ItemsWithoutSpecifications)`
  - `MasterDataImportResult(bool Committed, WorkbookReport Report, MasterDataImportDto? Import)`
  - `VerifyMasterDataImportRequest(string Password, string? Comment)`
  - Service: `ImportAsync(byte[] file, string fileName, int userId, string password, string? comment, string? ip) : Task<MasterDataImportResult>`, `GetImportsAsync() : Task<List<MasterDataImportDto>>`, `VerifyAsync(int id, int userId, string password, string? comment, string? ip) : Task<MasterDataImportDto>`, `GetFileAsync(int id) : Task<(byte[] Content, string FileName)>`

- [ ] **Step 1: Failing tests** (add to the integration class)

```csharp
    private static byte[] ItemWorkbook(string tag) => Workbook(
        ("MediaProducts", new[] { R(("Code", "P" + tag), ("Name", "Product " + tag)) }),
        ("Items", new[] { R(("Code", "IT-" + tag), ("Name", "Item " + tag), ("Category", "RawMaterial"), ("TestCodes", "")) }));

    [PostgresFact]
    public async Task Commit_CreatesRowsSignatureRecordAndFile_ItemsInactive()
    {
        await using var db = _fixture.CreateDbContext();
        var admin = await AdminAsync(db);
        var tag = Guid.NewGuid().ToString("N")[..8];
        var storage = new InMemoryFileStorageService();
        var file = ItemWorkbook(tag);

        var result = await Service(db, storage).ImportAsync(file, "lab.xlsx", admin.Id, Password, "initial load", "127.0.0.1");

        Assert.True(result.Committed);
        var import = await db.MasterDataImports.Include(i => i.Records).SingleAsync(i => i.Id == result.Import!.Id);
        Assert.Equal("Loaded", result.Import!.Status);
        Assert.Equal(2, import.Records.Count);
        Assert.Equal(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(file)), import.FileSha256);
        Assert.Equal(file, storage.Files[import.StorageKey]);
        Assert.False((await db.Items.SingleAsync(i => i.Code == "IT-" + tag)).IsActive);
        Assert.True(await db.ElectronicSignatures.AnyAsync(s =>
            s.EntityType == "MasterDataImport" && s.EntityId == import.Id && s.MeaningOfSignature == SignatureMeaning.MasterDataChanged));
        Assert.Contains("IT-" + tag, result.Import.ItemsWithoutSpecifications);
    }

    [PostgresFact]
    public async Task Commit_WithErrors_CreatesNothing()
    {
        await using var db = _fixture.CreateDbContext();
        var admin = await AdminAsync(db);
        var tag = Guid.NewGuid().ToString("N")[..8];

        var result = await Service(db).ImportAsync(Workbook(
            ("MediaProducts", new[] { R(("Code", "Q" + tag), ("Name", "Ok " + tag)) }),
            ("EmRooms", new[] { R(("Department", "No dept " + tag), ("Name", "R")) })), "x.xlsx", admin.Id, Password, null, null);

        Assert.False(result.Committed);
        Assert.True(result.Report.HasErrors);
        Assert.False(await db.MediaProducts.AnyAsync(p => p.Code == "Q" + tag));
    }

    [PostgresFact]
    public async Task Commit_WrongPassword_CreatesNothing()
    {
        await using var db = _fixture.CreateDbContext();
        var admin = await AdminAsync(db);
        var tag = Guid.NewGuid().ToString("N")[..8];

        await Assert.ThrowsAsync<MicroLIMS.Application.Interfaces.SignatureVerificationException>(() =>
            Service(db).ImportAsync(ItemWorkbook(tag), "x.xlsx", admin.Id, "wrong", null, null));

        await using var fresh = _fixture.CreateDbContext();
        Assert.False(await fresh.Items.AnyAsync(i => i.Code == "IT-" + tag));
        Assert.False(await fresh.MasterDataImports.AnyAsync(i => i.ImportedByUserId == admin.Id));
    }

    [PostgresFact]
    public async Task ExportThenReimport_IsAllUnchanged()
    {
        await using var db = _fixture.CreateDbContext();
        var admin = await AdminAsync(db);
        var tag = Guid.NewGuid().ToString("N")[..8];
        await Service(db).ImportAsync(ItemWorkbook(tag), "x.xlsx", admin.Id, Password, null, null);

        var (export, _) = await Service(db).DownloadAsync();
        var report = await Service(db).ValidateAsync(export, admin.Id);

        Assert.False(report.HasErrors, string.Join("\n", report.Errors.Select(e => $"{e.Sheet} {e.Row} {e.Column}: {e.Message}")));
        Assert.All(report.Sheets, s => Assert.Equal(0, s.Created));
    }

    [PostgresFact]
    public async Task Verify_BySameUser_IsRefused()
    {
        await using var db = _fixture.CreateDbContext();
        var admin = await AdminAsync(db);
        var result = await Service(db).ImportAsync(ItemWorkbook(Guid.NewGuid().ToString("N")[..8]), "x.xlsx", admin.Id, Password, null, null);

        var e = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Service(db).VerifyAsync(result.Import!.Id, admin.Id, Password, null, null));
        Assert.Contains("cannot verify", e.Message);
    }

    [PostgresFact]
    public async Task Verify_ByAnotherUser_ActivatesItems()
    {
        await using var db = _fixture.CreateDbContext();
        var loader = await AdminAsync(db);
        var verifier = await AdminAsync(db);
        var tag = Guid.NewGuid().ToString("N")[..8];
        var result = await Service(db).ImportAsync(ItemWorkbook(tag), "x.xlsx", loader.Id, Password, null, null);

        var verified = await Service(db).VerifyAsync(result.Import!.Id, verifier.Id, Password, "checked against spec sheets", null);

        Assert.Equal("Verified", verified.Status);
        await using var fresh = _fixture.CreateDbContext();
        Assert.True((await fresh.Items.SingleAsync(i => i.Code == "IT-" + tag)).IsActive);
        Assert.True(await fresh.ElectronicSignatures.AnyAsync(s =>
            s.EntityType == "MasterDataImport" && s.EntityId == verified.Id && s.MeaningOfSignature == SignatureMeaning.Reviewed && s.UserId == verifier.Id));
    }

    [PostgresFact]
    public async Task Specifications_WithStages_LoadTogether()
    {
        await using var db = _fixture.CreateDbContext();
        var admin = await AdminAsync(db);
        var tag = Guid.NewGuid().ToString("N")[..8];
        db.TestDefinitions.Add(new TestDefinition { Code = "DIS-" + tag, DisplayName = "Dissolution " + tag, SectionId = _fixture.SeededSectionId, IsActive = true });
        await db.SaveChangesAsync();

        var result = await Service(db).ImportAsync(Workbook(
            ("Items", new[] { R(("Code", "FP-" + tag), ("Name", "Tablet " + tag), ("Category", "FinishedProduct"), ("TestCodes", "DIS-" + tag)) }),
            ("Specifications", new[] { R(("ItemCode", "FP-" + tag), ("TestCode", "DIS-" + tag), ("ParameterName", "Dissolution"),
                ("LimitType", "MultiStage"), ("Unit", "%")) }),
            ("SpecificationStages", new[]
            {
                R(("ItemCode", "FP-" + tag), ("TestCode", "DIS-" + tag), ("ParameterName", "Dissolution"), ("StageNumber", "1"), ("StageLabel", "S1"), ("AcceptanceCriteria", "Each unit not less than Q+5%")),
                R(("ItemCode", "FP-" + tag), ("TestCode", "DIS-" + tag), ("ParameterName", "Dissolution"), ("StageNumber", "2"), ("StageLabel", "S2"), ("AcceptanceCriteria", "Average not less than Q")),
            })), "x.xlsx", admin.Id, Password, null, null);

        Assert.True(result.Committed, string.Join("\n", result.Report.Errors.Select(e => $"{e.Sheet} {e.Row} {e.Column}: {e.Message}")));
        Assert.Equal(2, result.Report.Sheets.Single(s => s.Sheet == "SpecificationStages").Created);
        Assert.Empty(result.Import!.ItemsWithoutSpecifications);
    }
```

If `SpecificationService.Validate` refuses the MultiStage row, read its message and adjust **the test row** (for example add a column it requires); never weaken the validator.

- [ ] **Step 2: Run to confirm they fail** (build errors: `ImportAsync` etc. missing).

- [ ] **Step 3: DTOs** (`MasterDataWorkbookDtos.cs`)

```csharp
namespace MicroLIMS.Application.Services.MasterDataWorkbook;

public sealed record MasterDataImportDto(
    int Id,
    string FileName,
    string FileSha256,
    string ImportedBy,
    DateTime ImportedAt,
    string? Comment,
    string Status,
    string? VerifiedBy,
    DateTime? VerifiedAt,
    IReadOnlyList<WorkbookSheetSummary> Sheets,
    IReadOnlyList<string> ItemsWithoutSpecifications);

public sealed record MasterDataImportResult(bool Committed, WorkbookReport Report, MasterDataImportDto? Import);

public sealed record VerifyMasterDataImportRequest(string Password, string? Comment);
```

- [ ] **Step 4: Service write side** (add to `MasterDataWorkbookService.cs`; add `using System.Security.Cryptography; using System.Text.Json; using Microsoft.EntityFrameworkCore; using MicroLIMS.Application.Helpers; using MicroLIMS.Domain.Entities; using MicroLIMS.Domain.Enums; using MicroLIMS.Shared.Exceptions;`)

```csharp
    private sealed class RejectedException : Exception
    {
        public RejectedException(WorkbookReport report) { Report = report; }
        public WorkbookReport Report { get; }
    }

    // Signed load. The import record is signed FIRST, so a wrong password
    // fails before any row is touched; UnitOfWork keeps the refused
    // attempt on record even though everything else rolls back. Any row
    // error rolls the whole load back - nothing is half-loaded.
    public async Task<MasterDataImportResult> ImportAsync(byte[] file, string fileName, int userId, string password, string? comment, string? ipAddress)
    {
        var sheets = ReadSheets(file);
        try
        {
            var importId = await UnitOfWork.RunAsync(_db, async () =>
            {
                _db.CurrentUserId = userId;
                var import = new MasterDataImport
                {
                    FileName = Path.GetFileName(fileName),
                    FileSha256 = Convert.ToHexString(SHA256.HashData(file)),
                    ImportedByUserId = userId,
                    ImportedAt = _time.GetUtcNow().UtcDateTime,
                    Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim(),
                    Status = MasterDataImportStatus.Loaded,
                };
                _db.MasterDataImports.Add(import);
                await _db.SaveChangesAsync();

                await _signatures.SignAsync(userId, password, SignatureMeaning.MasterDataChanged,
                    ReviewEntityTypes.MasterDataImport, import.Id, import.Comment ?? $"Loaded {import.FileName}", ipAddress);
                await _db.SaveChangesAsync();
                var id = import.Id;

                var (report, created) = await _runner.RunAsync(sheets, userId);
                if (report.HasErrors)
                    throw new RejectedException(report);

                // ponytail: the file is stored before the final commit; if
                // that commit then fails, the stored copy is orphaned (no row
                // points at it). Harmless, and rare.
                var key = await _storage.SaveAsync(
                    $"master-data-imports/{_time.GetUtcNow().UtcDateTime:yyyyMMddHHmmss}_{Guid.NewGuid():N}.xlsx", file);

                var saved = await _db.MasterDataImports.FirstAsync(i => i.Id == id);
                saved.StorageKey = key;
                saved.SummaryJson = JsonSerializer.Serialize(report.Sheets);
                _db.MasterDataImportRecords.AddRange(created.Select(c => new MasterDataImportRecord
                {
                    MasterDataImportId = id, EntityType = c.EntityType, EntityId = c.EntityId, Sheet = c.Sheet, RowNumber = c.RowNumber,
                }));
                await _db.SaveChangesAsync();
                return id;
            });

            var dto = (await LoadDtosAsync(i => i.Id == importId)).Single();
            return new MasterDataImportResult(true, new WorkbookReport(dto.Sheets, Array.Empty<WorkbookRowError>()), dto);
        }
        catch (RejectedException rejected)
        {
            return new MasterDataImportResult(false, rejected.Report, null);
        }
    }

    public Task<List<MasterDataImportDto>> GetImportsAsync() => LoadDtosAsync(_ => true);

    // Second-person verification: a different user checks the load against
    // the approved source documents and signs; that activates the items and
    // material master entries it created.
    public async Task<MasterDataImportDto> VerifyAsync(int id, int userId, string password, string? comment, string? ipAddress)
    {
        await UnitOfWork.RunAsync(_db, async () =>
        {
            var import = await _db.MasterDataImports.Include(i => i.Records).FirstOrDefaultAsync(i => i.Id == id)
                ?? throw new NotFoundException($"Master data import {id} not found.");
            if (import.Status != MasterDataImportStatus.Loaded)
                throw new InvalidOperationException("This load has already been verified.");
            if (import.ImportedByUserId == userId)
                throw new InvalidOperationException("The person who loaded the workbook cannot verify it. Ask a second person.");

            _db.CurrentUserId = userId;
            await _signatures.SignAsync(userId, password, SignatureMeaning.Reviewed,
                ReviewEntityTypes.MasterDataImport, id, string.IsNullOrWhiteSpace(comment) ? null : comment.Trim(), ipAddress);

            import.Status = MasterDataImportStatus.Verified;
            import.VerifiedByUserId = userId;
            import.VerifiedAt = _time.GetUtcNow().UtcDateTime;
            import.VerificationComment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
            await _db.SaveChangesAsync();

            foreach (var record in import.Records.Where(r => r.EntityType == "Item"))
                if (await _db.Items.AnyAsync(i => i.Id == record.EntityId))
                    await _items.SetActiveAsync(record.EntityId, true);
            foreach (var record in import.Records.Where(r => r.EntityType == "MaterialMasterEntry"))
                if (await _db.MaterialMasterEntries.AnyAsync(e => e.Id == record.EntityId))
                    await _materialMaster.SetActiveAsync(record.EntityId, true, userId);
        });

        return (await LoadDtosAsync(i => i.Id == id)).Single();
    }

    public async Task<(byte[] Content, string FileName)> GetFileAsync(int id)
    {
        var import = await _db.MasterDataImports.AsNoTracking().FirstOrDefaultAsync(i => i.Id == id)
            ?? throw new NotFoundException($"Master data import {id} not found.");
        return (await _storage.ReadAsync(import.StorageKey), import.FileName);
    }

    private async Task<List<MasterDataImportDto>> LoadDtosAsync(System.Linq.Expressions.Expression<Func<MasterDataImport, bool>> filter)
    {
        var imports = await _db.MasterDataImports.AsNoTracking()
            .Include(i => i.ImportedByUser).Include(i => i.VerifiedByUser).Include(i => i.Records)
            .Where(filter).OrderByDescending(i => i.ImportedAt).ToListAsync();

        var itemIds = imports.SelectMany(i => i.Records).Where(r => r.EntityType == "Item").Select(r => r.EntityId).Distinct().ToList();
        var withoutSpecs = await _db.Items.AsNoTracking()
            .Where(i => itemIds.Contains(i.Id) && !_db.Specifications.Any(s => s.ItemId == i.Id))
            .Select(i => new { i.Id, i.Code }).ToListAsync();

        return imports.Select(i => new MasterDataImportDto(
            i.Id, i.FileName, i.FileSha256, i.ImportedByUser?.FullName ?? string.Empty, i.ImportedAt, i.Comment,
            i.Status.ToString(), i.VerifiedByUser?.FullName, i.VerifiedAt,
            JsonSerializer.Deserialize<List<WorkbookSheetSummary>>(i.SummaryJson) ?? new List<WorkbookSheetSummary>(),
            withoutSpecs.Where(w => i.Records.Any(r => r.EntityType == "Item" && r.EntityId == w.Id)).Select(w => w.Code).ToList()))
            .ToList();
    }
```

- [ ] **Step 5: Controller actions**

```csharp
    [Authorize(Policy = PermissionConstants.MasterDataManage)]
    [HttpPost("import")]
    [RequestSizeLimit(MasterDataWorkbookService.MaxFileBytes + 1024 * 1024)]
    public async Task<IActionResult> Import(IFormFile file, [FromForm] string password, [FromForm] string? comment) =>
        Ok(ApiResponse<object>.Ok(await _service.ImportAsync(await ReadAsync(file), file.FileName, CurrentUserId, password, comment, IpAddress)));

    [Authorize(Policy = PermissionConstants.MasterDataManage)]
    [HttpGet("imports")]
    public async Task<IActionResult> GetImports() =>
        Ok(ApiResponse<object>.Ok(await _service.GetImportsAsync()));

    [Authorize(Policy = PermissionConstants.MasterDataManage)]
    [HttpPost("imports/{id}/verify")]
    public async Task<IActionResult> Verify(int id, VerifyMasterDataImportRequest request) =>
        Ok(ApiResponse<object>.Ok(await _service.VerifyAsync(id, CurrentUserId, request.Password, request.Comment, IpAddress)));

    [Authorize(Policy = PermissionConstants.MasterDataManage)]
    [HttpGet("imports/{id}/file")]
    public async Task<IActionResult> GetFile(int id)
    {
        var (content, fileName) = await _service.GetFileAsync(id);
        return File(content, MasterDataWorkbookService.XlsxContentType, fileName);
    }
```

Matrix lines:

```
MasterDataWorkbookController.GetFile(Int32): SystemAdministrator,SectionHead
MasterDataWorkbookController.GetImports(): SystemAdministrator,SectionHead
MasterDataWorkbookController.Import(IFormFile, String, String): SystemAdministrator,SectionHead
MasterDataWorkbookController.Verify(Int32, VerifyMasterDataImportRequest): SystemAdministrator,SectionHead
```

- [ ] **Step 6: Run the workbook tests, then the whole backend suite**

```bash
dotnet test backend/MicroLIMS.Tests --artifacts-path E:/MicroLIMS/rls-tmp/mdw-t7 --filter "FullyQualifiedName~MasterDataWorkbook|FullyQualifiedName~ArchitectureTests"
dotnet test backend/MicroLIMS.Tests --artifacts-path E:/MicroLIMS/rls-tmp/mdw-t7
```

Expected: all pass, Postgres tests passed (not skipped). Note the totals.

- [ ] **Step 7: Commit**

```bash
git add backend
git commit -F - <<'EOF'
feat(master-data): signed workbook load, history and second-person verification

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
EOF
```

### Task 8: Apply the migration to the local development database

- [ ] **Step 1:** Back up the local database first (memory: backups go to `E:/MicroLIMS/backups`):

```bash
"C:/Program Files/PostgreSQL/<ver>/bin/pg_dump" -h localhost -U postgres -Fc -f E:/MicroLIMS/backups/LIMSV2-before-mdw.dump LIMSV2
```

- [ ] **Step 2:** Apply:

```bash
dotnet ef database update --project backend/MicroLIMS.Persistence --startup-project backend/MicroLIMS.API --configuration Release
```

Expected: `AddMasterDataImports` applied; nothing else pending. **Local LIMSV2 only. Never Neon.**

- [ ] **Step 3: STOP for review (end of Phase 2).** Report the test totals and the migration.

---

## Phase 3: Frontend (stop for review after Task 10)

### Task 9: Service, types, report table, page, route, menu

**Files:** see the File map (Frontend).

**Interfaces:**
- Consumes: the six endpoints of Tasks 4, 6 and 7 (all under `/masterdata/workbook`).
- Produces: route `/laboratory-configuration/master-data-workbook` (`ROUTES.MASTER_DATA_WORKBOOK`), menu item in "GENERAL LABORATORY CONFIGURATION".

- [ ] **Step 1: Failing test** (`WorkbookReportTable.test.tsx`)

```tsx
import { describe, expect, it, afterEach } from "vitest";
import { render, screen, cleanup } from "@testing-library/react";
import { WorkbookReportTable } from "./WorkbookReportTable";
import { canLoad } from "./types";
import type { WorkbookReport } from "./types";

afterEach(cleanup);

const clean: WorkbookReport = { sheets: [{ sheet: "Organisms", created: 3, unchanged: 1, errors: 0 }], errors: [], hasErrors: false };
const failing: WorkbookReport = {
  sheets: [{ sheet: "EmRooms", created: 0, unchanged: 0, errors: 1 }],
  errors: [{ sheet: "EmRooms", row: 4, column: "Department", message: "No EM department named 'X'." }],
  hasErrors: true,
};

describe("WorkbookReportTable", () => {
  it("shows per-sheet counts", () => {
    render(<WorkbookReportTable report={clean} />);
    expect(screen.getByText("Organisms")).toBeTruthy();
    expect(screen.getByText("3")).toBeTruthy();
  });

  it("lists each error with sheet, row and column", () => {
    render(<WorkbookReportTable report={failing} />);
    expect(screen.getByText("No EM department named 'X'.")).toBeTruthy();
    expect(screen.getByText("Department")).toBeTruthy();
    expect(screen.getByText("4")).toBeTruthy();
  });
});

describe("canLoad", () => {
  it("only with a report that has no errors", () => {
    expect(canLoad(null)).toBe(false);
    expect(canLoad(failing)).toBe(false);
    expect(canLoad(clean)).toBe(true);
  });
});
```

- [ ] **Step 2: Run it to confirm it fails**

```bash
cd /e/MicroLIMS/MicroLIMS/frontend && npx vitest run src/modules/laboratoryConfiguration/masterDataWorkbook
```

- [ ] **Step 3: `types.ts`**

```ts
export interface WorkbookSheetSummary { sheet: string; created: number; unchanged: number; errors: number }
export interface WorkbookRowError { sheet: string; row: number; column: string | null; message: string }
export interface WorkbookReport { sheets: WorkbookSheetSummary[]; errors: WorkbookRowError[]; hasErrors: boolean }

export interface MasterDataImport {
  id: number;
  fileName: string;
  fileSha256: string;
  importedBy: string;
  importedAt: string;
  comment: string | null;
  status: "Loaded" | "Verified";
  verifiedBy: string | null;
  verifiedAt: string | null;
  sheets: WorkbookSheetSummary[];
  itemsWithoutSpecifications: string[];
}

export interface MasterDataImportResult { committed: boolean; report: WorkbookReport; import: MasterDataImport | null }

// Load is offered only after a dry run with no errors; the server checks
// again on load, so this is convenience, not a rule.
export const canLoad = (report: WorkbookReport | null): boolean => !!report && !report.hasErrors;
```

- [ ] **Step 4: `masterDataWorkbookService.ts`** (unwrap like other services: `res.data.data`; check the `ApiResponse` type in `masterDataSimple/services/EquipmentConfigurationService.ts`)

```ts
import { apiClient } from "../../../services/apiClient";
import type { ApiResponse } from "../masterDataSimple/services/EquipmentConfigurationService";
import type { MasterDataImport, MasterDataImportResult, WorkbookReport } from "./types";

const BASE = "/masterdata/workbook";

function saveBlob(data: Blob, fileName: string) {
  const url = window.URL.createObjectURL(data);
  const link = document.createElement("a");
  link.href = url;
  link.download = fileName;
  link.click();
  window.URL.revokeObjectURL(url);
}

function fileNameFrom(disposition: string | undefined, fallback: string) {
  const match = disposition?.match(/filename\*?=(?:UTF-8'')?"?([^";]+)"?/i);
  return match ? decodeURIComponent(match[1]) : fallback;
}

export const masterDataWorkbookService = {
  async download() {
    const res = await apiClient.get(BASE, { responseType: "blob" });
    saveBlob(res.data, fileNameFrom(res.headers["content-disposition"], "MicroLIMS-master-data.xlsx"));
  },

  async validate(file: File): Promise<WorkbookReport> {
    const form = new FormData();
    form.append("file", file);
    const res = await apiClient.post<ApiResponse<WorkbookReport>>(`${BASE}/validate`, form);
    return res.data.data;
  },

  async load(file: File, password: string, comment: string): Promise<MasterDataImportResult> {
    const form = new FormData();
    form.append("file", file);
    form.append("password", password);
    form.append("comment", comment);
    const res = await apiClient.post<ApiResponse<MasterDataImportResult>>(`${BASE}/import`, form);
    return res.data.data;
  },

  async imports(): Promise<MasterDataImport[]> {
    const res = await apiClient.get<ApiResponse<MasterDataImport[]>>(`${BASE}/imports`);
    return res.data.data;
  },

  async verify(id: number, password: string, comment: string): Promise<MasterDataImport> {
    const res = await apiClient.post<ApiResponse<MasterDataImport>>(`${BASE}/imports/${id}/verify`, { password, comment });
    return res.data.data;
  },

  async downloadFile(item: MasterDataImport) {
    const res = await apiClient.get(`${BASE}/imports/${item.id}/file`, { responseType: "blob" });
    saveBlob(res.data, item.fileName);
  },
};
```

- [ ] **Step 5: `WorkbookReportTable.tsx`**

```tsx
import { Table, TableBody, TableCell, TableHead, TableRow, Typography, Stack } from "@mui/material";
import type { WorkbookReport } from "./types";

// Result of a dry run or a load: one count row per sheet, then every
// error with the sheet, row and column the lab must fix.
export function WorkbookReportTable({ report }: { report: WorkbookReport }) {
  return (
    <Stack spacing={2}>
      <Table size="small" aria-label="Rows per sheet">
        <TableHead>
          <TableRow><TableCell>Sheet</TableCell><TableCell align="right">New</TableCell><TableCell align="right">Unchanged</TableCell><TableCell align="right">Errors</TableCell></TableRow>
        </TableHead>
        <TableBody>
          {report.sheets.map((s) => (
            <TableRow key={s.sheet}>
              <TableCell>{s.sheet}</TableCell>
              <TableCell align="right">{s.created}</TableCell>
              <TableCell align="right">{s.unchanged}</TableCell>
              <TableCell align="right">{s.errors}</TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>

      {report.errors.length > 0 && (
        <>
          <Typography variant="subtitle2" color="error">
            {report.errors.length} problem{report.errors.length === 1 ? "" : "s"} to fix in the workbook
          </Typography>
          <Table size="small" aria-label="Problems to fix">
            <TableHead>
              <TableRow><TableCell>Sheet</TableCell><TableCell>Row</TableCell><TableCell>Column</TableCell><TableCell>Problem</TableCell></TableRow>
            </TableHead>
            <TableBody>
              {report.errors.map((e, i) => (
                <TableRow key={`${e.sheet}-${e.row}-${i}`}>
                  <TableCell>{e.sheet}</TableCell>
                  <TableCell>{e.row}</TableCell>
                  <TableCell>{e.column ?? "—"}</TableCell>
                  <TableCell>{e.message}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </>
      )}
    </Stack>
  );
}
```

- [ ] **Step 6: `MasterDataWorkbookPage.tsx`**

```tsx
import { useCallback, useEffect, useState } from "react";
import { Alert, Button, Paper, Stack, Typography } from "@mui/material";
import { LabPage, RegisterTable } from "../../../components/lab";
import type { RegisterColumn } from "../../../components/lab";
import { SectionTitle } from "../../../components/SectionTitle";
import { SignatureDialog } from "../../../components/SignatureDialog";
import { masterDataWorkbookService as api } from "./masterDataWorkbookService";
import { WorkbookReportTable } from "./WorkbookReportTable";
import { canLoad } from "./types";
import type { MasterDataImport, WorkbookReport } from "./types";

// Master data workbook: download, check (dry run), load (signed) and
// second-person verify. Every rule is on the server; this page only
// shows its report.
export function MasterDataWorkbookPage() {
  const [file, setFile] = useState<File | null>(null);
  const [report, setReport] = useState<WorkbookReport | null>(null);
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState<{ text: string; ok: boolean } | null>(null);
  const [imports, setImports] = useState<MasterDataImport[]>([]);
  const [signing, setSigning] = useState<"load" | MasterDataImport | null>(null);
  const [comment, setComment] = useState("");

  const refresh = useCallback(() => api.imports().then(setImports), []);
  useEffect(() => { void refresh(); }, [refresh]);

  const check = async () => {
    if (!file) return;
    setBusy(true); setMessage(null);
    try { setReport(await api.validate(file)); }
    catch (e: any) { setMessage({ text: e?.response?.data?.message ?? "The file could not be checked.", ok: false }); }
    finally { setBusy(false); }
  };

  const sign = async (password: string) => {
    if (signing === "load" && file) {
      const result = await api.load(file, password, comment);
      setReport(result.report);
      if (result.committed) {
        setMessage({ text: "Workbook loaded. A second person must now verify it.", ok: true });
        setFile(null);
      } else {
        setMessage({ text: "Nothing was loaded: the workbook has problems to fix.", ok: false });
      }
    } else if (signing && signing !== "load") {
      await api.verify(signing.id, password, comment);
      setMessage({ text: `Load of ${signing.fileName} verified. Its items are now active.`, ok: true });
    }
    setSigning(null); setComment("");
    await refresh();
  };

  const columns: RegisterColumn<MasterDataImport>[] = [
    { key: "importedAt", label: "Loaded", sortable: true, render: (i) => new Date(i.importedAt).toLocaleString() },
    { key: "importedBy", label: "Loaded by", sortable: true },
    { key: "fileName", label: "File", render: (i) => <Button size="small" onClick={() => api.downloadFile(i)}>{i.fileName}</Button> },
    { key: "sheets", label: "New records", render: (i) => i.sheets.reduce((n, s) => n + s.created, 0) },
    { key: "status", label: "Status", render: (i) => (i.status === "Verified" ? `Verified by ${i.verifiedBy}` : "Waiting for verification") },
    {
      key: "id", label: "",
      render: (i) => i.status === "Loaded" ? <Button size="small" variant="outlined" onClick={() => setSigning(i)}>Verify</Button> : null,
    },
  ];

  const verifyStatement = (i: MasterDataImport) =>
    i.itemsWithoutSpecifications.length > 0
      ? `I verified this load against the approved source documents. Note: these items have no specifications yet and will become usable: ${i.itemsWithoutSpecifications.join(", ")}.`
      : "I verified this load against the approved source documents.";

  return (
    <LabPage
      title="Master Data Workbook"
      subtitle="Load organisms, departments, rooms, water points, material master, media products, equipment, items and specifications from one Excel workbook."
      actions={<Button variant="outlined" onClick={() => api.download()}>Download workbook</Button>}
    >
      <Stack spacing={3}>
        {message && <Alert severity={message.ok ? "success" : "error"} onClose={() => setMessage(null)}>{message.text}</Alert>}

        <Paper sx={{ p: 2 }}>
          <SectionTitle>1. Check the workbook</SectionTitle>
          <Stack direction="row" spacing={2} alignItems="center" sx={{ mt: 1 }}>
            <Button variant="outlined" component="label">
              Choose file
              <input hidden type="file" accept=".xlsx" onChange={(e) => { setFile(e.target.files?.[0] ?? null); setReport(null); }} />
            </Button>
            <Typography variant="body2">{file?.name ?? "No file chosen"}</Typography>
            <Button variant="contained" disabled={!file || busy} onClick={check}>{busy ? "Checking..." : "Check file"}</Button>
            <Button variant="contained" color="success" disabled={!canLoad(report) || busy} onClick={() => setSigning("load")}>Load and sign</Button>
          </Stack>
          {report && <Stack sx={{ mt: 2 }}><WorkbookReportTable report={report} /></Stack>}
        </Paper>

        <Paper sx={{ p: 2 }}>
          <SectionTitle>2. Loads and verification</SectionTitle>
          <RegisterTable rows={imports} columns={columns} getRowKey={(i) => i.id} />
        </Paper>
      </Stack>

      <SignatureDialog
        open={signing !== null}
        meaningStatement={signing === "load" || signing === null
          ? "I am loading this master data workbook into MicroLIMS."
          : verifyStatement(signing)}
        showComment
        comment={comment}
        onCommentChange={setComment}
        onCancel={() => { setSigning(null); setComment(""); }}
        onConfirm={sign}
      />
    </LabPage>
  );
}
```

Check `RegisterTable`'s real prop names in `frontend/src/components/lab/RegisterTable.tsx` (`rows`, `columns`, row key prop) and `SectionTitle`'s props, and match them.

- [ ] **Step 7: Route, path constant, menu**

`routes/routes.ts`, next to `ORGANISMS`:

```ts
  MASTER_DATA_WORKBOOK: "/laboratory-configuration/master-data-workbook",
```

`routes/AppRoutes.tsx`: lazy import beside the others, and a route inside the existing `PERMISSIONS.MASTER_DATA_MANAGE` block:

```tsx
const MasterDataWorkbookPage = lazy(() => import("../modules/laboratoryConfiguration/masterDataWorkbook/MasterDataWorkbookPage").then((m) => ({ default: m.MasterDataWorkbookPage })));
// ...
              <Route path="/laboratory-configuration/master-data-workbook" element={<MasterDataWorkbookPage />} />
```

`routes/menuConfig.ts`: an item beside `receivingConfigItem`, added to the `items.push(...)` call after it (reuse an icon already imported in the file, e.g. `FactCheckOutlinedIcon`, or import `TableViewOutlinedIcon` from `@mui/icons-material/TableViewOutlined`):

```ts
const masterDataWorkbookItem: MenuItem = { label: "Master Data Workbook", description: "Load master data and specifications from one Excel workbook.", path: "/laboratory-configuration/master-data-workbook", icon: TableViewOutlinedIcon, group: "GENERAL LABORATORY CONFIGURATION", permission: PERMISSIONS.MASTER_DATA_MANAGE };
```

```ts
    itemsItem, receivingConfigItem, masterDataWorkbookItem,
```

- [ ] **Step 8: Run typecheck, tests, lint**

```bash
cd /e/MicroLIMS/MicroLIMS/frontend && npx tsc --noEmit -p . && npx vitest run && npx eslint src/modules/laboratoryConfiguration/masterDataWorkbook src/routes
```

Expected: clean; all frontend tests pass (previous total 159 + 3 new).

- [ ] **Step 9: Commit**

```bash
git add frontend/src
git commit -F - <<'EOF'
feat(master-data): master data workbook page

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
EOF
```

### Task 10: Browser check with a real workbook (local only)

- [ ] **Step 1:** Start the local API (`dotnet run --project backend/MicroLIMS.API`) and frontend (`npm run dev` in `frontend`). Sign in as an administrator.
- [ ] **Step 2:** Menu → General Laboratory Configuration → Master Data Workbook → **Download workbook**. Open it in Excel: Instructions sheet first, existing rows present, drop-downs work (Category, Type, LimitType; EmRooms → Department lists the EmDepartments names).
- [ ] **Step 3:** Add two test rows on each of Organisms, EmDepartments, EmRooms, MediaProducts, Equipment and Items. Put a deliberate comma decimal in `SetPointTemperature`. **Check file**: exactly that one error is listed with sheet, row and column; Load is disabled.
- [ ] **Step 4:** Fix it, check again (zero errors), **Load and sign** with your password. The history shows "Waiting for verification"; the new items show as frozen on the Items page.
- [ ] **Step 5:** Try **Verify** as the same user: the dialog shows "cannot verify". Sign in as a second SectionHead or administrator and verify; the items become active.
- [ ] **Step 6:** Download the workbook again and **Check file** with it unchanged: every sheet shows 0 new, no errors.
- [ ] **Step 7:** Check the audit trail (Audit Search) shows the created rows by the loader and the two signatures.
- [ ] **Step 8:** Take screenshots of steps 3, 4 and 5 into `E:/MicroLIMS/rls-tmp/mdw-browser/`. Remove the test rows only if they are in the way; they are local data.
- [ ] **Step 9: STOP for review (end of Phase 3).**

---

## Phase 4: Validation documents

### Task 11: Configuration Specification and lab user guide

**Files:**
- Create: `docs/validation/ML-SYS-CS-001_Configuration_Specification.md`
- Create: `docs/validation/ML-SOP-MDW-001_Master_Data_Workbook_User_Guide.md`

- [ ] **Step 1: Configuration Specification.** Write it with these sections and this content:
  1. **Purpose and scope.** It defines the configuration of one MicroLIMS deployment: environment settings (from ML-SYS-SD-001 §3.1 and §8) and lab master data.
  2. **Environment configuration.** A table of every non-secret setting with its required PROD value (`Storage:Provider = S3`, `Lab:TimeZoneId`, `Jwt:AccessTokenMinutes`, `ErrorMonitoring:*`), plus the list of secrets by name only, with who holds them.
  3. **Lab master data: what defines it.** The **approved master data workbook** is the specification for sheets 1–12. It is identified by file name and SHA-256, and the import record (`MasterDataImports`) shows the same hash. Test definitions, workflow steps, media configurations, HPLC/GC/ICP methods and solution masters are configured in their screens. Each is specified by a printout or export of its screen, approved alongside the workbook (a list table: record type → screen → owner).
  4. **Procedure.** Prepare the workbook from approved source documents (specifications, SOPs, equipment list), then check, load and sign. A second person verifies against the sources and signs. Approve the workbook (Process Owner + QA) and record its hash here.
  5. **Moving configuration from VAL to PROD.** Download the workbook from VAL, then check and load it in PROD. The PROD import record hash must equal the approved hash, and the dry-run "0 errors" report is attached as evidence.
  6. **Verification evidence for IQ/PQ.** Per sheet, record counts (import record vs. workbook rows), the two signatures, and a spot-check of N records per sheet against source documents. N is set by the risk assessment: all specifications (High risk); 10% of other sheets, at least 5.
  7. **Approvals table** (same layout as ML-SYS-VP-001 §11).

- [ ] **Step 2: Lab user guide.** Write it as numbered steps a lab user can follow without help:
  1. Who may do it (MasterData.Manage: SectionHead or SystemAdministrator) and the two-person rule.
  2. Gather the sources: approved specifications, the equipment list, the media list, the EM/water sampling plans.
  3. Download the workbook. Read the Instructions sheet.
  4. Fill sheets 1–10, one rule per sheet, using the column table from the Instructions sheet. Copy the column help text for each sheet into the guide.
  5. Check file → fix errors (the 5 most common errors and their fixes: unknown reference, comma decimal, duplicate row, "already exists with different values", missing required column).
  6. Load and sign.
  7. Create test definitions and HPLC/GC/ICP methods in their screens (pointer to each screen).
  8. Fill Specifications and SpecificationStages, then check, load and sign again. A worked example covers one count-tiered micro limit (TAMC NMT 1000 CFU/g), one range assay (95.0–105.0 %), one presence/absence (*E. coli* absent in 1 g), and one multi-stage dissolution (S1/S2).
  9. Second person: open each load, compare with the sources, then Verify and sign. What to do if something is wrong: do not verify; correct it in the screen with a reason, then verify.
  10. Changing data later: always in the screens, never by re-loading. Explain why: changes need reasons and signatures.

- [ ] **Step 3: Commit**

```bash
git add docs/validation
git commit -F - <<'EOF'
docs(validation): configuration specification and master data workbook guide

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
EOF
```

- [ ] **Step 4: Final whole-branch review.** Run the full backend and frontend suites one last time and record the totals. Request a code review of `git diff feat/physchem-areas...feat/master-data-workbook`. Report to the user; do **not** push.
