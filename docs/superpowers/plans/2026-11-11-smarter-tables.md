# Smarter Tables (C3) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add saved views, URL-held state, column choice, density, row selection with bulk actions, audited CSV export and virtual scrolling to `RegisterTable`, then move the main registers onto it.

**Architecture:** All table state (filters, sort, columns, density, page size) lives in one hook, `useTableState(tableKey)`, which syncs the shareable part to the URL and can load or save it as a server-stored view. `RegisterTable` reads that state when given a `tableKey` and is unchanged without one. Selection is a second hook so the existing microbiology grouped actions, bulk assign, label printing and export all consume the same selected ids.

**Tech Stack:** React 19, TypeScript 6, MUI 9, Vitest, `@tanstack/react-virtual`; .NET 10, EF Core 10, PostgreSQL.

**Spec:** `docs/superpowers/specs/2026-11-11-smarter-tables-design.md`

## Global Constraints

- `CLAUDE.md` binds every task (separate contexts + `AsNoTracking`; `[PostgresFact]` executed, 0 skipped; `userEvent`; real components; mock only `services/`; no `any`; WCAG 2.2 AA; design tokens).
- A `RegisterTable` without `tableKey` must render and behave exactly as before (pinned by a characterisation test written first, Task 1).
- A view or a link carries settings only. No endpoint in this plan widens what a user may read.
- Limits: 20 personal views per user per table; view name ≤ 60 characters; state JSON ≤ 8 KB; bulk assign ≤ 200 ids; export ≤ 5,000 rows; virtual scrolling above 100 rendered rows.
- Permissions: `TableViews.Share` → Section Head **and QC Manager**; `Data.Export` → Section Head, Reviewer, QC Manager. Granted in migration `AddTableViews` through `RoleGrantSql.Grant`.
- Sections are `DocumentSection`; membership is `UserOrgMembership` (a null `SectionId` means the whole department).
- Export is CSV only, UTF-8 with BOM, CRLF line ends, formula-leading cells (`=`, `+`, `-`, `@`, tab, CR) prefixed with `'`.
- No bulk review, approval, result entry or signature. The microbiology grouped actions keep their endpoints (`GET test-workflow/actionable-groups`, `POST test-workflow/batch-select-media`), rules and audit.
- Each register migration is its own commit and keeps the page's columns, filters, row actions and service calls identical.

## Review Focus

1. **A shared link opened by someone with less access.** Same filters, only their own permitted rows, no error and no hint of hidden rows. Task 2 (frontend) relies on existing backend scope; one endpoint test in Task 3 proves a view id from another section returns 404.
2. **"Select all 312 that match" then the list changes underneath** (another user approves one). The bulk action reports that row as refused; it does not fail the whole request or silently skip it. Task 6.
3. **A saved view after a column is removed from the page in a later release.** The rest applies, a notice names what was skipped, and saving again cleans the view. Task 4.
4. **CSV opened in Excel with a batch number like `-2024/15` or `=A1`.** The cell must show as text. Task 5.
5. **Sample register rows expand to show tests.** Virtual scrolling with rows of changing height must keep the expanded row in place and keyboard focus on it. Task 8 and Task 9.

## File Structure

| File | Responsibility |
|---|---|
| `frontend/src/components/lab/table/tableRegistry.ts` | Every `tableKey`, its columns and filter names |
| `frontend/src/components/lab/table/useTableState.ts`, `tableUrlCodec.ts` | State, URL sync |
| `frontend/src/components/lab/table/useRowSelection.ts` | Selection across pages |
| `frontend/src/components/lab/table/ColumnsMenu.tsx`, `ViewsMenu.tsx`, `SaveViewDialog.tsx`, `DensityToggle.tsx`, `BulkActionBar.tsx`, `TableToolbar.tsx`, `VirtualBody.tsx` | UI parts |
| `frontend/src/components/lab/table/exportCsv.ts` | CSV building and download |
| `frontend/src/services/tableViewService.ts`, `exportLogService.ts` | API clients |
| `backend/MicroLIMS.Domain/Entities/TableView.cs`, `UserTableDefault.cs`; `Enums/TableViewScope.cs` | Data model |
| `backend/MicroLIMS.Application/Services/TableViews/TableViewService.cs`, `TableKeyRegistry.cs`; `DTOs/TableViewDtos.cs` | View rules |
| `backend/MicroLIMS.API/Controllers/TableViewsController.cs` (`api/table-views`), `ExportsController.cs` (`api/exports`) | HTTP |
| `backend/MicroLIMS.Application/Services/SampleBulkAssignmentService.cs` | Bulk assign |

---

### Task 1: Characterisation tests and table registry

**Files:** Create `frontend/src/components/lab/RegisterTable.test.tsx` (none exists), `tableRegistry.ts`, `tableRegistry.test.ts`

**Interfaces — Produces:**
```ts
export interface TableDefinition { key: string; title: string; idColumn: string; columns: { key: string; label: string; defaultVisible: boolean }[]; filters: string[]; }
export const TABLES: Record<string, TableDefinition>;
export function getTable(key: string): TableDefinition;      // throws on unknown key
```

- [ ] **Step 1: Write tests that pass against today's `RegisterTable`:** `sort cycles asc → desc → none and sets aria-sort`; `defaultSort applies before any click`; `pagination appears above 25 rows; options 25/50/100; page resets when row count changes`; `row actions menu opens from "Row actions for {id}" and calls onClick; disabled action cannot be chosen`; `Enter on a focused row calls onRowClick`; `phone width renders cards with a Sort by select`; `empty and loading states`. Run → PASS. These are the "unchanged" contract for every later task.
- [ ] **Step 2: Failing test:** `registry: every key is unique, has an idColumn that exists in columns and is defaultVisible`.
- [ ] **Step 3:** Implement the registry with entries for the 25 current `RegisterTable` pages (keys `<module>.<list>`, e.g. `inventory.materials`, `inventory.equipment`).
- [ ] **Step 4:** PASS. **Step 5:** Commit `test(tables): pin RegisterTable behaviour; add table registry`.

### Task 2: Table state, URL sync, columns, density

**Files:** Create `useTableState.ts`, `tableUrlCodec.ts`, `ColumnsMenu.tsx`, `DensityToggle.tsx`, `TableToolbar.tsx` with tests; Modify `RegisterTable.tsx` (props; header L270–300; body L320–360; pagination L366)

**Interfaces — Produces:**
```ts
export interface TableState { filters: Record<string, string | string[]>; search: string; sort: { key: string; direction: "asc" | "desc" } | null;
  pageSize: 25 | 50 | 100 | "all"; columns: string[]; /* visible, in order */ density: "comfortable" | "compact"; }
export interface UseTableState { state: TableState; page: number; viewId: number | null; isDefault: boolean; skipped: string[];
  setFilter(name: string, value: string | string[] | null): void; setSearch(v: string): void; setSort(s: TableState["sort"]): void;
  setPage(p: number): void; setPageSize(s: TableState["pageSize"]): void; setColumns(c: string[]): void; setDensity(d: TableState["density"]): void;
  applyState(s: Partial<TableState>, viewId: number | null): void; reset(): void; }
export function useTableState(tableKey: string, defaults?: Partial<TableState>): UseTableState;
export function encodeState(def: TableDefinition, s: TableState, page: number, viewId: number | null): URLSearchParams;   // omits defaults
export function decodeState(def: TableDefinition, p: URLSearchParams): { state: Partial<TableState>; page: number; viewId: number | null; skipped: string[] };
// RegisterTable new optional props: tableKey?: string; tableState?: UseTableState; toolbar?: ReactNode
```
URL parameters: `q`, `sort` (`key:asc`), `page`, `size`, `view`, and `f.<name>` per filter. Columns and density are not in the URL (they are personal); they persist per user per table in `localStorage` key `microlims.table.<tableKey>` and inside saved views. Filter changes push a history entry; typing in search replaces the current one. Unknown parameters and unknown filter names are ignored and reported in `skipped`. Bench mode (`theme.custom.bench`) forces `comfortable`.

- [ ] **Step 1: Failing tests:** `encode omits defaults; decode(encode(s)) === s` (property-style over a table of states); `unknown filter and unknown column are skipped and listed`; `Back restores the previous filter`; `search typing does not add history entries`; `filter change resets page to 1`; `columns menu: hide, show, Move up/Move down by keyboard; id column checkbox disabled with reason; Reset to default`; `column order and visibility drive header and cells`; `density compact changes row padding; bench mode ignores compact`; `two tables on one page do not share URL parameters` (second table is given a prefix); `without tableKey the Task 1 tests still pass unchanged`.
- [ ] **Step 2–4:** FAIL → implement → PASS. **Step 5:** lint, `tsc -b`, `debt:check`. Commit `feat(tables): state in the URL, column menu, density`.

### Task 3: Saved views — backend

**Files:** Create `TableView.cs`, `UserTableDefault.cs`, `TableViewScope.cs`, configurations, `TableKeyRegistry.cs`, `TableViewService.cs`, `TableViewDtos.cs`, `TableViewsController.cs`, migration `AddTableViews`; Modify `PermissionConstants.cs` (`TableViewsShare = "TableViews.Share"`, `DataExport = "Data.Export"`), `IMicroLimsDbContext`/`MicroLimsDbContext`, `ServiceCollectionExtensions.cs`; Tests `backend/MicroLIMS.Tests/IntegrationTests/TableViewPostgresTests.cs`, `backend/MicroLIMS.Tests/UnitTests/TableKeyRegistryTests.cs`

**Interfaces — Produces:**
```csharp
public enum TableViewScope { Personal = 0, Section = 1 }
// TableView: Id, OwnerUserId, TableKey (≤100), Name (≤60), Scope, SectionId?, StateJson (jsonb, string), CreatedAt, UpdatedAt, UpdatedByUserId
// UserTableDefault: UserId, TableKey, TableViewId   (PK UserId+TableKey; FK cascade on view delete)
public sealed record TableViewDto(int Id, string TableKey, string Name, string Scope, int? SectionId, string? SectionName, string StateJson, bool IsMine, bool CanEdit, bool IsDefault, string OwnerName);
public sealed class TableViewService
{
    Task<IReadOnlyList<TableViewDto>> ListAsync(int userId, string tableKey, CancellationToken ct = default);
    Task<TableViewDto> CreateAsync(int userId, string tableKey, string name, string stateJson, TableViewScope scope, int? sectionId, CancellationToken ct = default);
    Task<TableViewDto> UpdateAsync(int userId, int viewId, string? name, string? stateJson, CancellationToken ct = default);
    Task DeleteAsync(int userId, int viewId, CancellationToken ct = default);
    Task SetDefaultAsync(int userId, string tableKey, int? viewId, CancellationToken ct = default);
}
public static class TableKeyRegistry { public static bool IsKnown(string key); }   // same keys as the frontend registry
```
Indexes: unique `(OwnerUserId, TableKey, Name)` filtered `"Scope" = 0`; unique `(SectionId, TableKey, Name)` filtered `"Scope" = 1`; check constraint `Scope = 1` ⇔ `SectionId IS NOT NULL`. Visibility: own personal views + section views whose `SectionId` the user is a member of (membership with null `SectionId` covers every section of that department). `CanEdit`: owner, or a `TableViews.Share` holder who is a member of the view's section. Creating a section view requires `TableViews.Share` and membership of that section. A view the caller cannot see answers 404, not 403. HTTP: `GET api/table-views?tableKey=`, `POST`, `PUT {id}`, `DELETE {id}`, `PUT api/table-views/default` `{ tableKey, viewId }`. `StateJson` must parse as a JSON object with integer property `v`.

- [ ] **Step 1: Failing tests:**
  - `Create_Personal_PersistsAndAudits` (triad); `DuplicateNameSameOwnerAndTable_Rejected`; `SameNameDifferentUser_Allowed`
  - `TwentyFirstPersonalView_Rejected`; `UnknownTableKey_Rejected`; `StateOver8Kb_Rejected`; `StateNotJsonObject_Rejected`; `Name61Chars_Rejected`
  - `List_ReturnsMineAndMySectionViews_NotOtherUsersPersonal_NotOtherSections`
  - `DepartmentWideMember_SeesViewsOfEverySectionInDepartment`
  - `CreateSectionView_WithoutPermission_Throws`; `…_ForSectionIAmNotIn_Throws`; `…_AsQcManagerMember_Succeeds`
  - `UpdateOrDelete_OthersPersonalView_Returns404` *(Review Focus 1)*; `Update_SectionView_ByAnotherSharerInSection_Succeeds`; `…_ByPlainMember_Throws`
  - `SetDefault_ToViewICannotSee_Throws`; `DeleteView_RemovesDefaultsPointingAtIt`
  - `Migration_GrantsShareToSectionHeadAndQcManager_ExportToSectionHeadReviewerQcManager`
  - Unit: `BackendRegistryKeys_EqualFrontendRegistryKeys` (reads `frontend/src/components/lab/table/tableRegistry.ts` keys with a regex; fails when the two lists drift).
- [ ] **Step 2–4:** FAIL → implement → PASS, 0 skipped; `has-pending-model-changes` → none.
- [ ] **Step 5:** Commit `feat(tables): saved views API`.

### Task 4: Saved views — frontend

**Files:** Create `tableViewService.ts`, `ViewsMenu.tsx`, `SaveViewDialog.tsx` with tests; Modify `TableToolbar.tsx`, `useTableState.ts` (default view on first load when the URL carries no state), `frontend/src/routes/routes.ts` (`PERMISSIONS`)

State JSON shape: `{ v: 1, filters, search, sort, pageSize, columns, density }`.

- [ ] **Step 1: Failing tests:** `views menu lists "My views" and "Shared views" with the section name`; `choosing a view applies it and puts view=<id> in the URL`; `changing a filter after applying marks the view as modified and offers "Save changes" (owner) or "Save a copy" (shared)`; `Save view validates name (required, ≤ 60) and shows the server's duplicate-name message on the field`; `"Share with section" appears only with TableViews.Share and lists only my sections`; `Set as default / Clear default`; `default view applies on open with a clean URL and not when the URL has filters`; `view naming a removed column applies the rest and shows "Not applied: column 'x'"` *(Review Focus 3)*; `saving that view again drops the unknown parts`; `views fail to load → table works, notice shown`; `delete asks for confirmation with Cancel focused`; `a link with view=<id> the user cannot see falls back to page defaults with a notice`.
- [ ] **Step 2–4:** FAIL → implement → PASS. **Step 5:** Commit `feat(tables): saved views UI`.

### Task 5: Selection, action bar, audited CSV export

**Files:** Create `useRowSelection.ts`, `BulkActionBar.tsx`, `exportCsv.ts`, `exportLogService.ts` with tests; backend `ExportsController.cs`; Modify `DataExportAuditService.cs` (overload below), `RegisterTable.tsx` (checkbox column); Test `backend/MicroLIMS.Tests/IntegrationTests/ListExportPostgresTests.cs`

**Interfaces — Produces:**
```ts
export interface RowSelection<Id> { selected: ReadonlySet<Id>; allMatching: boolean; count: number; isSelected(id: Id): boolean; toggle(id: Id): void;
  togglePage(ids: Id[]): void; selectAllMatching(total: number): void; clear(): void; }
export function useRowSelection<Id>(resetKey: string): RowSelection<Id>;      // clears when resetKey (filters + search) changes
export interface BulkAction<Id> { key: string; label: string; permission?: string; run(ids: Id[]): Promise<void> | void; eligible?(id: Id): boolean; ineligibleReason?: string }
export function buildCsv(header: string[][], columns: string[], rows: string[][]): string;
export function neutralise(cell: string): string;
export const exportLogService = { log(body: { tableKey: string; filterJson: string; columns: string[]; rowCount: number; recordIds: string[] }): Promise<{ exportId: number; serverTimeUtc: string; timeZoneId: string; exportedBy: string }> };
// RegisterTable new optional props: selection?: RowSelection<string | number>; bulkActions?: BulkAction<string | number>[]; totalMatching?: number; loadAllMatching?(): Promise<T[]>
```
```csharp
// DataExportAuditService: returns the log id; exportType = "ListCsv:" + tableKey; filterJson holds filters, columns and record ids
public Task<DataExportLog> LogListExportAsync(int userId, string tableKey, string filterJson, int rowCount, CancellationToken ct = default);
```
HTTP: `POST api/exports/log`, policy `DataExport`; rejects unknown table key, `rowCount` > 5,000, `recordIds.Count != rowCount`, body > 256 KB. The file's first lines: `Exported by`, `Exported at` (server time with zone), `List`, `Filters`, `Rows`, `Export ID`, then a blank line, then the header row.

- [ ] **Step 1: Failing tests:**
  - Selection: `toggle, togglePage, selectAllMatching; survives page and sort changes; cleared when filters change`; `header checkbox is indeterminate when some rows on the page are selected`; `checkbox column appears only when the user may use at least one action`; `action bar shows "14 selected" and announces changes politely; Clear empties it`; `ineligible rows are counted: "12 of 14 can be …"`.
  - CSV: `neutralise prefixes cells starting with = + - @ tab CR and leaves others` *(Review Focus 4)*; `quotes, commas, newlines are escaped`; `output starts with BOM and uses CRLF`; `columns follow visible order`; `Arabic text round-trips`.
  - Flow: `export calls the log service first; when it rejects, no file is created and the error is shown`; `file header lines contain the export id and server time returned`; `more than 5,000 selected → refused with guidance, service not called`; `Export action hidden without Data.Export`.
  - Backend: `Log_WithPermission_WritesDataExportLog_WithUserFiltersRowCount`; `Log_WithoutPermission_403`; `Log_Over5000_Rejected`; `Log_UnknownTableKey_Rejected`; `Log_RowCountMismatch_Rejected`.
- [ ] **Step 2–4:** FAIL → implement → PASS. **Step 5:** Commit `feat(tables): selection and audited CSV export`.

### Task 6: Bulk assign

**Files:** Create `SampleBulkAssignmentService.cs`; Modify `SampleController.cs` (action beside L136), `SampleAssignmentService.cs` (reuse `ApplyAsync`, L152); frontend `frontend/src/modules/receiving/dialogs/BulkAssignDialog.tsx`, `ReceiveService.ts`; Tests `backend/MicroLIMS.Tests/IntegrationTests/SampleBulkAssignPostgresTests.cs`, `BulkAssignDialog.test.tsx`

**Interfaces — Produces:**
```csharp
public sealed record BulkAssignRequest(IReadOnlyList<int> SampleIds, int AnalystUserId, string? Reason);
public sealed record BulkAssignRowResult(int SampleId, string ReferenceNumber, bool Assigned, string? RefusedReason);
public sealed record BulkAssignResult(int AssignedCount, int RefusedCount, IReadOnlyList<BulkAssignRowResult> Rows);
Task<BulkAssignResult> AssignAsync(BulkAssignRequest request, int actingUserId, CancellationToken ct = default);
```
`POST api/samples/bulk-assign`, policy `SamplesAssignAnalyst`. Request-level failures (empty list, > 200 ids, duplicate ids, unknown analyst) reject the whole request with 400 and change nothing. Per row, the same checks as `AssignAnalystAsync` (L41): section scope (`Ensure…AccessAsync`), `AnalystAssignmentRules.EnsureAssignableAsync`, sample state. Passing rows are applied in one transaction, each test order getting its own audit row through `ApplyAsync`; refused rows carry the rule's own message. Samples are loaded in one query; no query per row beyond what `ApplyAsync` already does.

- [ ] **Step 1: Failing tests:**
  - `AllValid_AssignsEveryTest_OneAuditRowPerChangedTest` (triad)
  - `Mixed_AssignsValid_ReturnsRefusedWithReason_NoAuditForRefused`
  - `SampleInSectionICannotWrite_IsRefused_NotAssigned`
  - `AnalystNotMemberOfSampleSection_RowRefused`
  - `SampleApprovedBetweenSelectionAndSubmit_RowRefused_OthersAssigned` *(Review Focus 2)*
  - `Request201Ids_400_NothingChanged`; `DuplicateIds_400`; `UnknownAnalyst_400`
  - `WithoutPermission_403`
  - `AlreadyAssignedToSameAnalyst_CountsAsAssigned_NoNewAuditRow`
  - Frontend: `dialog lists selected count, analyst picker and optional reason`; `result dialog shows assigned count and each refused row with its reason`; `selection keeps only the refused rows afterwards`.
- [ ] **Step 2–4:** FAIL → implement → PASS, 0 skipped. **Step 5:** Commit `feat(tables): bulk assign analyst`.

### Task 7: Move the three Reports tables; remove `DataTable`

**Files:** Modify `frontend/src/modules/reports/components/ReportResultsTable.tsx` (L103–148 selection, L417–429 table, L174–212 inline CSV), `MediaGptResultsTable.tsx`, `ReferenceStrainResultsTable.tsx`; Delete `frontend/src/components/DataTable.tsx` and its test; registry entries `reports.results`, `reports.mediaGpt`, `reports.referenceStrains`

- [ ] **Step 1:** Write characterisation tests for each table against today's code: columns and order, sort, `isSelectable` only for `approvalStatus === "Approved"`, select-all-matching via `fetchAllPages`, the server export call `ReportingService.exportCsv(appliedParams)` unchanged, PDF export receives the selected records. Run → PASS.
- [ ] **Step 2:** Move each to `RegisterTable` + `useRowSelection`; replace the inline CSV with `exportCsv.ts`.
- [ ] **Step 3:** Same tests → PASS; `grep -r "components/DataTable"` → no results; delete the file.
- [ ] **Step 4:** lint, `tsc -b`, `debt:check` (`debt:update` if counts fell). **Step 5:** Commit `refactor(tables): reports on RegisterTable; remove DataTable`.

### Task 8: Virtual scrolling and expandable rows

**Files:** Create `VirtualBody.tsx` with tests; Modify `RegisterTable.tsx`, `frontend/package.json`

**Interfaces — Produces:** `RegisterTable` optional props `renderExpanded?(row: T): ReactNode`, `expandedIds?: ReadonlySet<string | number>`, `onToggleExpand?(id: string | number): void`. `VirtualBody` is used when the rendered row count exceeds 100 (page size "all" or a client list); it measures row height dynamically, sets `aria-rowcount` on the table and `aria-rowindex` on each row, keeps the header sticky, and keeps the focused row mounted.

- [ ] **Step 1: Failing tests:** `100 rows render all; 101 render a window`; `aria-rowcount equals the total and each rendered row has the right aria-rowindex`; `page size "All" is offered and persists in state`; `arrow-key focus moves beyond the rendered window and scrolls it into view`; `expanding a row renders renderExpanded beneath it and does not shift scroll position` *(Review Focus 5)*; `expanded row stays expanded after scrolling away and back`; `selection checkbox state is correct for rows that were unmounted and remounted`; `phone card mode is not virtualised and still paginates`.
- [ ] **Step 2–4:** FAIL → implement → PASS. **Step 5:** Commit `feat(tables): virtual scrolling and expandable rows`.

### Task 9: Sample register and the workspaces

**Files:** Modify `frontend/src/modules/receiving/components/SampleRegisterTable.tsx` (1,034 lines), `SampleFilterBar.tsx`, `frontend/src/modules/receivingTesting/ReceivingTestingWorkspacePage.tsx` (filter state L240–306, `GroupedActionsPanel` L902–903, table L991), `frontend/src/modules/receiving/ReceivingPage.tsx` (L56, L115); registry keys `samples.workspace.micro`, `samples.workspace.fp`, `samples.receiving`

Rules: the workspace list stays **server-paged** through `ReceiveService.getRecordsPaged`; `useTableState` supplies `page`, `pageSize`, sort and the filter values that build `TestingWorkspaceFilter` (L290–306). `checkedSampleIds` becomes `useRowSelection`; `GroupedActionsPanel` receives `selectedSampleIds={[...selection.selected]}` and `onDeselectAll={selection.clear}` with no other change. Bulk actions on this table: "Assign analyst" (Task 6), "Export selected", "Print labels" (only when B1's `PrintLabelsDialog` module exists, detected by a feature flag from the client-settings endpoint). The existing `localStorage` keys `microlims.sampleRegister.columns` and `microlims.receivingRegister.columns` (including the `"due:off"` marker) are read once into the new state and removed.

- [ ] **Step 1:** Extend `SampleRegisterTable.test.tsx` against today's code to pin: the 11 columns and default-visible six, every row action callback, test chips opening `onTestClick`, expand/collapse, the exact `TestingWorkspaceFilter` sent for a set of filters, `GroupedActionsPanel` appearing with ≥ 2 selected samples. Run → PASS.
- [ ] **Step 2: Failing tests for the new behaviour:** `filters, sort and page are in the URL and restore on reload`; `old column preference is migrated once and the old key removed`; `selecting 2 samples still shows GroupedActionsPanel with the same sample ids; Deselect all clears the new selection`; `batch-select-media is called with the same payload as before`; `Assign analyst opens BulkAssignDialog with the selected ids`; `Print labels is absent when the feature flag is off`; `saved view restores workloadFilter and analyst filter`.
- [ ] **Step 3:** Implement. **Step 4:** All tests PASS; lint, `tsc -b`, `debt:check`.
- [ ] **Step 5:** Commit `refactor(tables): sample register and workspaces on RegisterTable`.

### Task 10: Remaining registers

One commit per page, same recipe as Task 9 (characterise → move → same tests pass → add URL/view tests): `EquipmentPage.tsx` (master, 1,609 lines), `ApprovedMediaListPage.tsx`, `ApprovedCryovialListPage.tsx`, `MediaPage.tsx`, `CryovialsPage.tsx`, `ItemsPage.tsx`, document library page, `OosTrackingPage.tsx`. For `MaterialsPage.tsx` and `EquipmentInventoryPage.tsx` (already on `RegisterTable`, filters in `useState`, URL read once on mount): replace the local filter state with `useTableState`.

- [ ] **Step 1 (per page):** characterisation test (columns, filters, row actions, service calls) → PASS on old code.
- [ ] **Step 2:** Move; add registry entry in both registries.
- [ ] **Step 3:** Same test PASS + `URL round trip` + `saved view applies` tests.
- [ ] **Step 4:** lint, `tsc -b`, `debt:check`. **Step 5:** Commit `refactor(tables): <page> on RegisterTable`.

A page whose table carries behaviour `RegisterTable` cannot express without a new feature is **not** forced: stop, list what is missing in the PR, and leave that page as it is.

### Task 11: Final verification and documents

- [ ] **Step 1:** Backend Release build; full suite without and with PostgreSQL; exact counts. `has-pending-model-changes` → none.
- [ ] **Step 2:** Frontend lint, `tsc -b`, test, build, `debt:check`.
- [ ] **Step 3:** Run the app: build a filtered view on the sample register, save it, share it as a Section Head, open the link as an analyst in another section (sees own rows only), bulk-assign with one row that must be refused, export and open the CSV in a spreadsheet, scroll a 5,000-row list. Keyboard-only pass on one register. Report what was and was not exercised.
- [ ] **Step 4:** `documents/` requirements and user-guide page; roadmap links.
- [ ] **Step 5:** Commit `docs(tables): requirements, guide, roadmap links`.
