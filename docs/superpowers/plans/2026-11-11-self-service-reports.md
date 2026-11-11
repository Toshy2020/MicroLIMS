# Self-Service Reports (F5) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A report builder over defined, scoped datasets with grouping and charts, saved/shared/scheduled reports with link-only email, and server-side CSV/Excel/PDF exports that are all logged.

**Architecture:** `IReportDataset` implementations declare fields (`FieldDef { Key, Label, Type, Summable, UnitKey? }`) and return a scoped `IQueryable` projection; a `ReportQueryCompiler` turns a validated `ReportSpec` (columns, filters, grouping, aggregates, sort) into an EF expression tree — no SQL strings. `SavedReport`, `ReportShare`, `ReportSchedule`, `ReportSnapshot` entities; `ReportScheduleWorker`; exporters for CSV, Excel (ClosedXML, already referenced) and PDF (`IPdfGenerator`).

**Tech Stack:** .NET 10, EF Core 10, PostgreSQL, ClosedXML 0.105.1, xUnit `[PostgresFact]`; React 19, TypeScript, MUI, recharts, Vitest.

**Spec:** `docs/superpowers/specs/2026-11-11-self-service-reports-design.md`

## Global Constraints

- `CLAUDE.md` binds every task (anti-fake tests, `AsNoTracking` + SQL projection, section scope applied **inside** each dataset query, page clamp for previews (100), `ILabClock`, `CancellationToken`, no new `any`, `userEvent`, chart wrapper stub rules).
- No raw SQL from user input; filter values are parameters; field keys validated against the dataset definition.
- Turnaround fields reuse the existing KPI calculations (`SampleTatKpi`, `StageTatKpi` logic extracted to shared helpers, unchanged results — characterisation tests first).
- Unit mixing: aggregates over a field with `UnitKey` require grouping by the unit field and the test field.
- Export limit `Reports:MaxExportRows` default 50000; snapshot retention `Reports:SnapshotRetentionDays` default 90.
- Every export through `DataExportAuditService.LogExportAsync` with `ExportType` = `Report:{format}` and `FilterJson` = the spec.
- Permission codes: `Reports.Build` (Analyst, Reviewer, Section Head, QC Manager, QA), `Reports.Share` (Section Head, QC Manager, QA).

## Review Focus

1. A grouped query leaks counts from other laboratories (scope applied after grouping) → `Grouped_counts_respect_scope` (Task 1).
2. Shared report run with the owner's access → `Shared_report_runs_with_reader_access` (Task 3).
3. Snapshot opened by a recipient who lost access → `Snapshot_filtered_to_current_reader_access` (Task 4): snapshots store row keys plus scope fields, filtered at read time.
4. Relative date "last 30 days" evaluated in UTC instead of lab time → `Relative_dates_use_lab_clock` (Task 1).
5. A browser export path left unlogged → `No_client_side_export_remains` (Task 2, frontend source scan for `Blob(` with `text/csv` and `window.print` in report modules).

## File Structure

| File | Responsibility |
|---|---|
| `Application/Services/Reports/Datasets/*.cs` | `ResultsDataset`, `SamplesTurnaroundDataset`, `MediaDataset`, `EquipmentDataset`, `StockDataset`, `QualityRecordsDataset` |
| `Application/Services/Reports/ReportSpec.cs`, `ReportQueryCompiler.cs`, `ReportRunner.cs` | Spec, compilation, execution |
| `Application/Services/Reports/Export/*.cs` | CSV, Excel, PDF exporters |
| `Domain/Entities/Reports/*.cs` | `SavedReport`, `ReportShare`, `ReportSchedule`, `ReportSnapshot` |
| `API/Controllers/ReportsBuilderController.cs`, `API/BackgroundServices/ReportScheduleWorker.cs` | Endpoints, scheduler |
| `frontend/src/modules/reports/builder/*`, `services/reportBuilderService.ts` | Builder UI |

---

### Task 1: Datasets, spec, compiler, Results dataset

**Interfaces:** `ReportSpec { Dataset; Columns[]; Filters[{ Field; Op (Eq|Neq|In|Contains|Gt|Gte|Lt|Lte|Between|IsEmpty|RelativeDays); Values[] }]; GroupBy[0..2]; Aggregates[{ Field; Fn (Count|Sum|Avg|Min|Max) }]; Sort[]; IncludeHistory; Chart? { Type (Bar|Line|Pie); X; Y } }`; `ReportSpecValidator.Validate(spec, dataset) -> errors`; `IReportDataset { string Key; IReadOnlyList<FieldDef> Fields; IQueryable<object-row> Query(ReportPrincipal, includeHistory) }` (strongly typed row class per dataset); `ReportQueryCompiler.Compile(IQueryable<TRow>, ReportSpec)`; `ReportRunner.PreviewAsync(spec, user, ct)` (100 rows) / `RunAsync(spec, user, maxRows, ct)`. Results dataset reads `ResultRecord` with existing scope (`GetReadableSectionIdsAsync`), excluding superseded / invalidated unless `IncludeHistory`.

- [ ] **Step 1:** Failing tests: validator rejects unknown fields, wrong operators, unit-mixing aggregates; each operator per type; `Relative_dates_use_lab_clock`; `Grouped_counts_respect_scope`; history exclusion; preview limit; generated SQL contains no user strings (parameters only, asserted via `ToQueryString`).
- [ ] **Step 2:** FAIL → implement → PASS. Commit `feat(reports): dataset framework and results dataset`.

### Task 2: Server exports and logging

**Interfaces:** `IReportExporter { string Format; Task<(byte[] Content, string FileName, string ContentType)> ExportAsync(ReportRunResult run, ReportHeader header, ct) }` — CSV (UTF-8 BOM, header comment lines), Excel (sheet "Report" typed columns, sheet "About" with the header), PDF (`ReportDocument` with header, table, chart rendered server-side to PNG via SkiaSharp). Endpoint `POST api/reports/export?format=`. Replace browser exports: `ReportResultsTable.tsx` CSV Blob (L204-205), `modules/reports/utils/exportPdf.ts`, `exportMediaGptPdf.ts`, `exportReferenceStrainPdf.ts`, `modules/kpi/utils/exportKpiPdf.ts` → server endpoints (existing CSV endpoints keep working).

- [ ] **Step 1:** Failing tests: each format's content and header; row limit refusal; export logged with spec and row count; `No_client_side_export_remains`.
- [ ] **Step 2:** FAIL → implement → PASS. Commit `feat(reports): server-side exports with logging`.

### Task 3: Saving and sharing

**Interfaces:** `SavedReport : IVersionedEntity { Id; OwnerUserId; Name; Dataset; SpecJson; CreatedAtUtc; UpdatedAtUtc }`; `ReportShare { ReportId; SectionId? (null = everyone) }`; endpoints CRUD, `copy`, `share` (`Reports.Share`; sharer must be able to read the dataset in those sections). Runs always use the caller's principal.

- [ ] **Step 1:** Failing tests: personal visibility; share rules and permission; `Shared_report_runs_with_reader_access`; copy; field removed from dataset shown as removed and ignored; audit of shared report changes.
- [ ] **Step 2:** FAIL → implement → PASS. Commit `feat(reports): saved and shared reports`.

### Task 4: Other datasets

Samples and turnaround (one row per sample × section; TAT fields from extracted KPI helpers — characterisation tests prove KPI numbers unchanged), Media (`Media` + evaluations + release), Equipment (`EquipmentInventory` + calibration), Stock (`Material`), Quality records (D1 `OosInvestigation` + D2 `QualityRecord`; skip and report if not built). Each with scope rules of its module.

- [ ] **Step 1:** Failing tests per dataset: fields, scope, history flags; KPI characterisation equal before and after extraction.
- [ ] **Step 2:** FAIL → implement → PASS. Commit `feat(reports): turnaround, media, equipment, stock and quality datasets`.

### Task 5: Schedules and snapshots

**Interfaces:** `ReportSchedule : IVersionedEntity { ReportId; Frequency (Daily|Weekly|Monthly); Weekday?; DayOfMonth? (1–28); LocalTime; RecipientUserIds[]; RecipientSectionIds[]; IsActive; ConsecutiveFailures; PausedAtUtc? }`; `ReportSnapshot { Id; ReportId; RunAtUtc; SpecJson; RowsJson (compressed); RowCount; ScopeFieldIndex }` (retention cleanup in a housekeeping worker); `ReportScheduleWorker` (catalog entry, Housekeeping; runs due schedules as the **owner**, stores rows with section ids, notifies recipients after commit via the notification service / C5 email with a link `/reports/snapshots/{id}`); `GET api/reports/snapshots/{id}` filters rows by the reader's current scope.

- [ ] **Step 1:** Failing tests: due time in lab time incl. month days; snapshot stored; email body contains a link and no data; `Snapshot_filtered_to_current_reader_access`; pause after three failures and owner notified; retention cleanup.
- [ ] **Step 2:** FAIL → implement → PASS. Commit `feat(reports): scheduled reports with link-only email`.

### Task 6: Builder UI and documents

**Files:** `modules/reports/builder/ReportBuilderPage.tsx` (dataset → columns → filters → grouping → chart → preview), `FilterEditor.tsx`, `ColumnPicker.tsx` (keyboard reorder), `ChartPreview.tsx`, `MyReportsPage.tsx` (personal, shared, copy), `ScheduleDialog.tsx`, `SnapshotPage.tsx`; a "Builder" tab in `pages/Reports.tsx`; tests; manual chapter; A1 entries if present.

- [ ] **Step 1:** Failing tests (`userEvent`, `services/` mocked): build and preview; unit-mixing refusal message shown; save, share (button absent without permission), copy; schedule editor; export buttons call the server; snapshot page.
- [ ] **Step 2:** FAIL → implement → full checks PASS. Commit `feat(ui): report builder`.
- [ ] **Step 3:** Full verification per `CLAUDE.md`; report counts.

## Cross-plan corrections (whole-programme review, 2026-11-11)

These corrections come from reviewing all plans together against the code. Where they differ from the text above, they win. Shared names are defined once in `docs/superpowers/plans/2026-11-11-build-order.md` (section "Programme conventions").

1. `IReportDataset<TRow>` is generic; `Query` returns `IQueryable<TRow>`; the compiler is `ReportQueryCompiler.Compile<TRow>`.
2. Snapshots store `RowsJson` where every row includes its `SectionId` (and `CustomerId` when present), so rows can be filtered by the reader's current access when the snapshot is opened.
3. Migration `AddReportBuilder`.

**Translation (build order 2.7):** if F4 Task 4 is merged when this package is built, every new screen uses translation keys and every new refusal carries a message code.
