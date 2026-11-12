# C3 — Smarter Tables: Design Specification

**Roadmap package:** C3 (`improvement-roadmap.md`)
**Status:** Approved by the user 2026-11-11
**Date:** 2026-11-11

## 1. Purpose

Give the main list pages one consistent table: saved views, column choice, filters that live in the link, selection of many rows with actions on them, smooth long lists, and a density switch.

## 2. Decisions (confirmed by the user, 2026-11-11)

| # | Decision | Answer |
|---|---|---|
| 1 | Which lists | The main registers first: the pages already on the shared tables plus the big hand-built registers |
| 2 | Saved views | Personal views for everyone, plus views a Section Head shares with the section |
| 3 | Bulk actions | Export selected rows; assign analyst; print labels (B1); and the bulk work that already exists in the microbiology workspace (media selection and incubation) |
| 4 | Export | New permission, given to Section Head, Reviewer and QC Manager; every export audited |

Confirmed by the user: the microbiology workspace's existing grouped actions for media selection and incubation keep their rules and are driven by the new row selection.

## 3. Out of scope

- The small tables inside dialogs, forms and detail pages (about 80 files).
- Converting client-loaded lists to server paging (several registers load every row today; this package makes them render well, it does not change how they load).
- Editing cells in the table, grouping, pivoting, column resizing by drag.
- Excel (.xlsx) export; CSV only.
- Changing any existing server-side report or audit export.

## 4. Current state (from code, not run)

- `RegisterTable` is used by 25 pages; `DataTable` by 3 (all in Reports). Both sort and page on the client.
- 118 files under `modules/` build tables directly from MUI. The busiest registers are among them: sample register (1,034 lines), receiving/testing workspace (1,179), equipment (1,609).
- No saved views and no user preferences on the server. One page (sample register) keeps its column choice in `localStorage`.
- 10 pages keep some filters in the URL, each in its own way; there is no shared hook.
- Selection with a bulk action exists in three places (reports PDF export, HPLC sample assignment, sample preparation batch confirm). The microbiology workspace has `GroupedActionsPanel` with `POST api/test-workflow/batch-select-media`.
- Assign analyst is one sample at a time (`PUT api/samples/{id}/assign-analyst`, `…/test-assignments`).
- Server CSV exports exist for reports, audit search, access reviews and error monitoring; most are audited through `DataExportAuditService`. There is no general list export.
- No virtualization library. Equipment, materials, cryovials, items, media and OOS lists load all rows; the receiving register fetches every page and then pages on the client.

## 5. Design

### 5.1 One table

`RegisterTable` gains the new features, all opt-in through one prop, `tableKey`, a stable name such as `samples.register`. A page without `tableKey` behaves exactly as today.

- The 3 `DataTable` pages move to `RegisterTable`; `DataTable` is removed.
- The hand-built registers in scope are rebuilt on `RegisterTable`: sample register, receiving register, microbiology and physicochemical workspaces, equipment (inventory and master), media, cryovials, items, document library, OOS tracking. Their columns, filters and actions stay the same.
- A registry lists every `tableKey` with its columns and filter names. The backend holds the same list of keys so a view cannot be saved for a table that does not exist.

### 5.2 Table state and the link

A table's state is: filters, search text, sort, page size, visible columns and their order, density.

- A shared hook keeps filters, search, sort, page and the chosen view in the URL query string. Default values are left out so links stay short.
- Copying the address gives a colleague the same filtered list. Back and Forward step through filter changes.
- **A link never grants access.** The list still comes from the backend with the reader's own permissions and sections; a colleague sees only their own rows under the same filters.

### 5.3 Saved views

- "Save view" stores the current state under a name. "Views" lists My views and Shared views; picking one applies it.
- A user can mark one view per table as their default; it is applied when they open the page without filters in the link.
- Limits: 20 personal views per table per user; names up to 60 characters, unique per owner and table.
- **Shared views:** a user with the new permission `TableViews.Share` (seeded to Section Head and QC Manager) can publish a view to one of their sections. Members of that section see it under Shared views, read-only; they can "Save a copy". Only the owner or another holder of the permission in that section can change or remove it.
- A view stores settings only, never rows.
- If a view names a column or filter that no longer exists, the rest is applied and a notice says what was skipped.
- Stored on the server (`TableView`: owner, table key, name, scope, section, state as versioned JSON up to 8 KB; `UserTableDefault`: user, table key, view). Changes are audited by default like any other record.
- The sample register's existing `localStorage` column choice is carried into the new state once, then removed.

### 5.4 Columns

- A "Columns" menu lists every column with a checkbox and Move up / Move down buttons. No dragging is needed, so it works by keyboard and on a tablet.
- The identifier column of each table cannot be hidden.
- "Reset to default" restores the page's original columns.

### 5.5 Density

Comfortable or Compact, chosen in the table toolbar, remembered per user per table and stored in views. Bench mode (C2) always uses Comfortable.

### 5.6 Long lists

- Page sizes 25 / 50 / 100 stay, with a new choice "All".
- When more than 100 rows are shown, only the rows near the screen are drawn (virtual scrolling), with the header fixed. Screen readers still get the true row count and position.
- Library: `@tanstack/react-virtual` (MIT), qualified like the other new libraries.
- Sorting and filtering stay where they are today for each list (client for fully loaded lists, server for paged endpoints).

### 5.7 Selecting rows

- A checkbox column appears on tables that have at least one bulk action the user may use.
- Header checkbox selects the rows on the page; a line then offers "Select all 312 that match". Selection survives paging and sorting and is cleared when filters change.
- An action bar appears while rows are selected: "14 selected", the actions, and Clear.
- Rows a given action cannot apply to are counted before it runs: "12 of 14 can be assigned. 2 are already approved."

### 5.8 Bulk actions

| Action | Where | How |
|---|---|---|
| Assign analyst | Sample register, workspaces | New `POST api/samples/bulk-assign` with up to 200 test or sample ids, one analyst and an optional reason. Each row passes the same checks as single assignment (`AnalystAssignmentRules`, section scope, permission `Samples.AssignAnalyst`). Rows that pass are assigned in one transaction, each with its own audit entry; rows that fail are returned with their reason. Nothing is assigned if the request itself is invalid |
| Print labels | Any table of a labelled record type | Opens B1's print dialog with the selected records. Hidden until B1 is installed |
| Media selection and incubation | Microbiology workspace | The existing grouped actions (`GroupedActionsPanel`, `batch-select-media` and the incubation action it offers) are opened from the new selection. Their endpoints, eligibility rules and audit do not change |
| Export selected | Every table in scope | See 5.9 |

No bulk review, approval, result entry or signature is added.

### 5.9 Export

- CSV of the selected rows, or of all rows matching the filters, with the visible columns in their current order.
- New permission `Data.Export`, seeded to Section Head, Reviewer and QC Manager. Without it the action is not shown and the server refuses.
- **Audit first:** before the file is produced the browser calls `POST api/exports/log` with table key, filters, columns, row count and the record identifiers (up to 5,000). The server checks the permission, writes the audit entry through the existing `DataExportAuditService`, and returns an export id and the server time. If that call fails, no file is produced.
- The file is then built in the browser from the rows the user already has. It starts with header lines: exported by, date and time with zone, list name, filters, row count, export id.
- Limit 5,000 rows per export; above that the user is told to narrow the filters or use the Reports module.
- Cells beginning with `=`, `+`, `-` or `@` are neutralised so a spreadsheet cannot run them as formulas. The file is UTF-8 with a byte-order mark so Arabic text opens correctly in Excel.

Stated openly: this export is produced by the browser from data the user can already see, so the audit entry is a record of an export being requested, not a server-certified copy of the file. Certified outputs remain the server-side reports and the certificate of analysis.

### 5.10 Accessibility

Sortable headers announce their state; the column menu, views menu and action bar are fully keyboard operable; selection count and bulk results are announced politely; virtual scrolling keeps focus on the same row; nothing depends on colour alone.

## 6. Error handling

| Situation | Behaviour |
|---|---|
| Views cannot be loaded | The table works with page defaults; a notice says views are unavailable |
| Saving a view fails | The state stays applied; a message offers Retry |
| Link contains an unknown filter or value | Ignored; a notice says the link was partly applied |
| Shared view's section no longer includes the user | The view disappears from their list; a default pointing at it falls back to page default |
| Bulk assign: some rows refused | Result dialog lists each refused row with its reason; accepted rows are done |
| Export log call fails | No file; error shown |
| More than 5,000 rows selected for export | Refused with guidance |

## 7. Testing

- **Backend `[PostgresFact]`:** view create/update/delete with audit; name uniqueness; 20-view limit; unknown table key rejected; state over 8 KB rejected; a user cannot read or change another user's personal view; shared view visible only to section members; sharing without the permission refused; default falls back when the view is removed. Bulk assign: all valid, mixed, all invalid; each success has its own audit row; other-section rows refused; 201 ids refused; concurrent change of one row. Export log: permission enforced; audit row with filters and row count; over-limit refused.
- **Frontend (`userEvent`, real components):** state ↔ URL round trip and Back button; view apply, save, rename, delete, default, copy of a shared view; partial apply notice; column hide, reorder by keyboard, mandatory column locked, reset; density; selection across pages, "select all matching", cleared on filter change; action bar counts; bulk-assign result dialog; export blocked when the log call fails; CSV content, header lines, formula neutralising, BOM; virtual list exposes the true row count; a table without `tableKey` renders exactly as before.
- **Per migrated register:** the same columns, filters, row actions and service calls as before the move (pinned by tests written first against the old page).
- **Manual:** a 5,000-row list scrolls smoothly on the lab's typical PC; keyboard-only and screen reader pass on one register.

## 8. Delivery stages

1. Table state hook, URL sync, column menu, density in `RegisterTable`; `DataTable` pages moved.
2. Saved views (backend and UI), personal then shared.
3. Selection, action bar, export.
4. Bulk assign; print labels hook; microbiology grouped actions on the new selection.
5. Virtual scrolling.
6. Move the hand-built registers, one page per change, busiest first: sample register, workspaces, equipment, media, cryovials, items, document library, OOS tracking, receiving register.
7. Documents: requirements and user guide.

## 9. Notes from the code read

- Both workspaces are one page (`ReceivingTestingWorkspacePage`) that renders the sample register table, which already has row selection feeding `GroupedActionsPanel`. The plan swaps that selection for the shared one; the grouped actions are untouched.
- The sample register has expandable rows, so the shared table gains expandable rows.
- Exports are logged in the existing `DataExportLog` table, like the Reports exports.
- Limits confirmed: 200 rows for bulk assign, 5,000 rows for export.
