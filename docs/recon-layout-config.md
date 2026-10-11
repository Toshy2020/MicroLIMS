# Recon: User-Configurable Layout (columns + panels)

**Mode:** read-only recon. No code, migrations or config changed.
**Date:** 2026-10-05
**Scope:** check the proposed design (column registry + `useTableLayout` + `ColumnConfigurator` + per-user server persistence) against the code as it is today.

`[UNVERIFIED]` marks anything the code cannot confirm. All paths are relative to the repo root. `fe/` = `frontend/src/`, `be/` = `backend/`.

---

## 0. Headline findings (read these first)

1. **The stack is not what the brief assumes.** It is React **19.3** and MUI **9.4**, not React 18 (`frontend/package.json:16-24`). **No MUI X package of any kind** is installed, so the DataGrid Pro licence question does not arise. **No drag-and-drop library** is installed either (no `@dnd-kit`, no `react-beautiful-dnd`).
2. **The sample pipeline table is hand-written JSX, not data-driven.** `SampleRegisterTable` hard-codes 11 `<TableCell>`s in the header and 11 in each row (`fe/modules/receiving/components/SampleRegisterTable.tsx:271-505`, header `:529-583`). Before the hook can do anything, P1 has to rewrite it as a column array. That rewrite is the largest single item in P1.
3. **There are three table idioms.** The shared `RegisterTable` has a data-driven `RegisterColumn<T>[]` and 22 consumers. The shared `DataTable` has a data-driven `Column<T>[]` and 3 consumers. Around 85 files use hand-written MUI `<Table>`. Only the two shared wrappers can take a registry cheaply.
4. **Column show/hide already exists in three places, but it is session-only.** `ApprovedMediaListPage`, `ApprovedCryovialListPage` and `ReportResultsTable` each have their own `useState` column toggle that resets on reload. These should be migrated onto the new hook, not left as parallel implementations.
5. **Nothing persists per-user preferences on the server today.** Dark mode and sidebar collapse use **localStorage only**. Their keys are **not per-user** and **survive logout** (not in `AUTH_STORAGE_KEYS`). On a shared lab PC every user inherits the previous user's theme and sidebar state. The layout cache must not copy this: it needs userId-scoped keys.
6. **The audit trail is opt-out, not opt-in.** Every new entity is audited automatically unless it is added to the exclusion list in `MicroLimsDbContext.CapturePendingAuditEntries` (`be/MicroLIMS.Persistence/DbContext/MicroLimsDbContext.cs:302-312`). "No audit trail on layout changes" therefore needs an explicit code change plus a test. It will not happen on its own.
7. **There are 48 permission codes, not 18.** 38 of them are enforced (`be/MicroLIMS.Shared/Constants/PermissionConstants.cs:103-138`). None covers UI layout.
8. **`FinalResultMatrixPanel` does not exist** in the repo `[UNVERIFIED: may be a planned or renamed component]`. `PrimaryObservationMatrixPanel` and `BatchConfirmatoryPlatingPanel` do exist. Their columns are **data-driven by the session's assigned tests**, which makes them data, not presentation. They must stay outside the layout feature entirely.
9. **Missing API:** there is no preferences or view-layout endpoint of any kind. P1 must add one (see §2.3). No mock was written.

---

## 1. Inventory (A1–A5)

### 1.1 Shared table components

| Component | File | Column model | Features | Consumers |
|---|---|---|---|---|
| `RegisterTable<T>` | `fe/components/lab/RegisterTable.tsx` | Data-driven `RegisterColumn<T>[]`: `key, label, render, align, sortable, sortValue, width, numeric, nowrap` (`:12-25`) | Client sort, client pagination (25/50/100), sticky header when >15 rows (`:58`), `rowActions` kebab column rendered **outside** `columns` (`:338, :400-408`), `rowTone` GMP row tint (`:51, :384-391`), **phone card mode that uses `columns[0]` as the card title** (`:135-136`) | 22 files (§1.3) |
| `DataTable<T>` | `fe/components/DataTable.tsx` | Data-driven `Column<T>[]`: `key, label, render, align` (`:6-11`) | Optional checkbox selection column (`:16-25`), sticky header when >15 rows | 3 files (reports) |
| `PrintableTable<T>` | `fe/components/PrintableTable.tsx` | Its own `PrintColumn<T>[]` (`:4-7`) | Print-only. Deliberately separate from the screen table (`:17-20`) | 4 inventory pages |
| `TableSkeleton` | `fe/components/TableSkeleton.tsx` | n/a | Loading placeholder | — |
| `PageHeader` | `fe/components/PageHeader.tsx` | n/a | Page title bar. A natural host for a "Columns" button | many |

There is **no** shared column-config UI, column-visibility type or layout hook.

### 1.2 Sample pipeline / worklist (A3)

I read "sample pipeline / worklist" as the **Laboratory Register** in the Receiving & Testing workspace. The analyst's **Today's Work** table is the other candidate (see Open Question Q1).

| Item | Detail |
|---|---|
| Page | `fe/modules/receivingTesting/ReceivingTestingWorkspacePage.tsx` (1,166 lines). Rendered once per lab by `LabWorkspaceRoute.tsx:95, :121` (Micro and FP fp/rmpm), each scoped by `lab.sectionId/area` (`:124-130`). |
| View modes | `table` / `card` / `kanban`, held in the **URL query string only** (`:163-166`) and not persisted. |
| Main table | `SampleRegisterTable`: `fe/modules/receiving/components/SampleRegisterTable.tsx`. Also used by `/receiving` (`fe/modules/receiving/ReceivingPage.tsx:109`). |
| Column definition | **Hard-coded JSX.** Header `:529-583`, row cells `:271-505`. `colSpan={11}` is hard-coded (`:588`). `minWidth: 960` (`:529`). |
| Compact list (split-pane) | Separate 4-column hand-written table inside the workspace page (`:680-711`) built from `SampleTableRow`. That component already takes a `visibleColumns: Set<string>` prop (`fe/modules/testingWorkspace/SampleTableRow.tsx:16, :201-236`), but the caller passes a fixed literal set (`ReceivingTestingWorkspacePage.tsx:698`). |
| Rows | Nested retest children render recursively, indented inside the Item/Reference cell (`SampleRegisterTable.tsx:281, :507`). |

**`SampleRegisterTable` columns (11):**

| # | Header | Line | Content | Interactive / action content |
|---|---|---|---|---|
| 1 | Received At | 271 | date/time | — |
| 2 | # | 276 | internal `sampleId` | — |
| 3 | Item / Reference | 281 | name, reference no., Retest chip, OOS chip, retest expand chip, doc indicator | **Grouped-action checkbox lives in this cell** (`:284-299`), and so does the **select-all-on-page checkbox** in its header (`:537-552`). The retest expand toggle and controlled-documents button are here too. |
| 4 | Item Type | 378 | `CategoryBadge` | — |
| 5 | Cause | 383 | `CauseBadge` | — |
| 6 | Sampled By | 388 | text | — |
| 7 | Batch / Control No. | 393 | B:/C: | — |
| 8 | Assigned To | 411 | analyst chip | **"Assign Analyst" button / reassign chip**, conditional on `Samples.AssignAnalyst` (`:120, :413-470`). The same action is also in the Actions menu (`SampleActionMenu.tsx:174`). |
| 9 | Sample Status | 480 | status badge | — (drives what actions are valid) |
| 10 | Test Status Summary | 485 | per-test status | **Click-through into each test (result entry) and the "Needs Preparation" action** (`TestStatusSummaryCell.tsx:95, :111`) |
| 11 | Actions | 495 | `SampleActionMenu` | Edit/Void/Audit/Report/Assign. Items are permission-filtered (`SampleActionMenu.tsx:52, :69-70`). |

**Conditional columns (A4):** in this table no **column** is conditional on role, permission or department. Conditionality is at **cell content** level (Assigned To button) and **menu item** level (Actions). The layout feature must leave that untouched: the hook decides *which columns render*, and the cell keeps its own permission checks.

Elsewhere, columns conditional on permission or state that must stay conditional on top of user preference:

| File:line | Column | Condition |
|---|---|---|
| `fe/modules/laboratoryConfiguration/media/components/MediaConfigurationsSection.tsx:153` | Actions | `isManager` |
| `fe/modules/laboratoryConfiguration/media/components/MediaIncubationConditionsSection.tsx:140` | Actions | `isManager` |
| `fe/modules/reports/components/WorkloadWeightsDialog.tsx:120` | Action | `isAuthorized` |
| `fe/modules/documentControl/components/RevisionChangeItemsDialog.tsx:274` | Actions | `isEditable` (record state) |
| `fe/modules/documentControl/pages/DocumentDetailPage.tsx:842, :894` | edit controls | `canEditDraft` |
| `fe/modules/solutionPreparation/components/StandardizationResultView.tsx:98, :107` | Blank (mL) | `blankRequired` (method data) |

The design rule follows from this: **`rendered = registry ∩ allowedByContext ∩ visibleByUserLayout`**. The registry entry needs an optional `isAvailable(ctx)` predicate, so that a column the user may not see never shows in the configurator either.

### 1.3 Full table inventory (A1, A2)

Column counts are header cells, approximate (several tables have more than one `<TableHead>`). "Fit" means: **P1/P2** = candidate for user layout; **No** = data entry, a calculation, evidence, an audit or a detail grid, which stays fixed.

**A. `RegisterTable` consumers (data-driven, cheapest to adopt)**

| Page / component | File (`fe/modules/…`) | ~Cols | Fit |
|---|---|---|---|
| Materials inventory | `inventory/materials/MaterialsPage.tsx` | 12 | P2 |
| Equipment inventory | `inventory/equipment/EquipmentInventoryPage.tsx` | 9 | P2 |
| Active equipment activity | `inventory/equipment/components/ActiveEquipmentView.tsx` | 2 tables, ~6 each | P2 |
| Solution preparations list | `solutionPreparation/PreparationListPage.tsx` | 8 | P2 |
| Media lot register | `laboratoryConfiguration/media/components/MediaLotRegisterTable.tsx` | 6 | P2 |
| Cryovial review | `laboratoryConfiguration/cryovials/components/CryovialReviewTable.tsx` | 6 | P2 |
| Material master | `laboratoryConfiguration/masterDataSimple/MaterialMasterPage.tsx` | 8 | P2 |
| Solution master | `laboratoryConfiguration/masterDataSimple/SolutionMasterPage.tsx` | 7 | P2 |
| Chromatography columns | `laboratoryConfiguration/masterDataSimple/ChromatographyColumnsPage.tsx` | 7 | P2 |
| FP instruments | `laboratoryConfiguration/masterDataSimple/FpInstrumentsPage.tsx` | 8 | P2 |
| HPLC methods | `laboratoryConfiguration/masterDataSimple/hplcMethod/HplcMethodTable.tsx` | 7 | P2 |
| ICP methods | `laboratoryConfiguration/masterDataSimple/icpMethod/IcpMethodTable.tsx` | 7 | P2 |
| Equation types / Organisms / Test master | `…/EquationTypesPage.tsx`, `…/OrganismsPage.tsx`, `…/TestMasterPage.tsx` | 4 | low value (few cols) |
| HPLC / ICP run history | `hplcWorkspace/history/HplcRunHistoryTable.tsx`, `icpWorkspace/history/IcpRunHistoryTable.tsx` | 8 | P2 |
| HPLC run setup | `hplcWorkspace/run/HplcInstrumentWorkspace.tsx:263` | 4 | No |
| HPLC sample assignment | `hplcWorkspace/samples/SampleAssignmentPanel.tsx` | 7 | No (run workflow) |
| SST values | `hplcWorkspace/sst/SstValuesTable.tsx` | 7 | **No** (SST evidence) |
| Titration / Gravimetric | `testingWorkspace/TitrationPanel.tsx`, `testingWorkspace/GravimetricPanel.tsx` | 7-8 | **No** (result entry) |

**B. `DataTable` consumers**

| Page | File | ~Cols | Fit |
|---|---|---|---|
| Results report | `fe/modules/reports/components/ReportResultsTable.tsx` | 11 + actions | P2. **Already has a column toggle** (`:95-97, :335`), session-only. |
| Media GPT results | `fe/modules/reports/components/MediaGptResultsTable.tsx` | ~10 | P2 |
| Reference strain results | `fe/modules/reports/components/ReferenceStrainResultsTable.tsx` | ~13 | P2 |

**C. Hand-written MUI `<Table>` (would need a refactor to a column array first)**

*List pages (P2 candidates, highest value first):*

| Page | File (`fe/modules/…`) | ~Cols | Notes |
|---|---|---|---|
| **Sample register (pipeline)** | `receiving/components/SampleRegisterTable.tsx` | 11 | **P1 pilot** |
| Approved media | `inventory/approvedLists/ApprovedMediaListPage.tsx` | 12 | Existing session-only toggle (`:121-135, :197-203`). Fixed print columns (`:324`). |
| Approved cryovials | `inventory/approvedLists/ApprovedCryovialListPage.tsx` | 9 | Existing session-only toggle (`:105, :173`). Fixed print columns (`:281`). |
| Document library | `documentControl/pages/DocumentLibraryPage.tsx` | 11 | |
| Tracking board | `receiving/TrackingBoardPage.tsx` | 7 | |
| OOS tracking | `oosTracking/OosTrackingPage.tsx` | expandable rows | `[UNVERIFIED]` exact column count (nested heads) |
| Users | `users/UsersPage.tsx` | 7 | |
| Working standard lots / qualifications | `workingStandards/lots/WorkingStandardLotsTable.tsx`, `workingStandards/qualifications/QualificationsTable.tsx` | 9 / 7 | |
| My reading list | `documentControl/pages/MyReadingListPage.tsx` | 7 | |
| Reviewer dashboard | `dashboard/ReviewerDashboardPage.tsx` | 8 | |
| Today's Work (analyst) | `dashboard/components/TodaysWorkTable.tsx` | 6 | Alternative "worklist" reading (Q1) |
| Analyst KPI | `reports/components/AnalystKpiTab.tsx` | 11 | |
| Incidents (error log) | `errorMonitoring/components/IncidentTable.tsx` | 8 | |
| Doc control dashboard / compliance | `documentControl/pages/DocumentControlDashboardPage.tsx`, `…/ComplianceDashboardPage.tsx` | 5-6 | |

*Fixed tables that should stay fixed (No):*

- **Result entry, matrices, calculations:** `testingWorkspace/pathogenSession/PrimaryObservationMatrixPanel.tsx`, `…/BatchConfirmatoryPlatingPanel.tsx`, `…/SessionReviewPanel.tsx`, `…/SessionOverviewPanel.tsx`; `testingWorkspace/WeightVariationPanel.tsx`, `DisintegrationPanel.tsx`, `DissolutionPanel.tsx`, `LocationResultGridDialog.tsx`, `WaterLocationResultGridDialog.tsx`, `PathogenLocationResultGridDialog.tsx`; `hplcWorkspace/entry/*` (ReplicateEntryTable, CalculationSummaryCard, HplcSampleEntryPage, QualificationEntryPage, WorkingStandardPreviewCard); `icpWorkspace/entry/*`, `icpWorkspace/calibration/*`, `icpWorkspace/ccv/*`; `components/UnitEntryGrid.tsx`; `receiving/dialogs/MultiSampleEntryGrid.tsx` (17-col entry grid); `solutionPreparation/record/*`, `solutionPreparation/wizard/ReviewStep.tsx`.
- **Evidence and review:** `hplcWorkspace/evidence/*`, `hplcWorkspace/review/*`, `icpWorkspace/evidence/*`, `testingWorkspace/icp/*`.
- **Audit:** `auditSearch/components/AuditResultsTable.tsx`, `documentControl/pages/DocumentAuditPage.tsx`, `testingWorkspace/SampleSummaryDialog.tsx`.
- **Method and master-data detail editors:** `laboratoryConfiguration/masterDataSimple/hplcMethod/*`, `…/icpMethod/*`, `EquipmentPage.tsx` (6 sub-tables), `items/components/ItemSpecificationsSection.tsx`, media dialogs.
- **Dialog sub-tables in Document Control and Reports:** `ApprovalWorkspaceDialog`, `PeriodicReviewWorkspaceDialog`, `RevisionChangeItemsDialog`, `DocumentDetailPage` (6 sub-tables), `TrendingDataDialog`, `CompareDialog`, `MediaGptDetailDialog`, `ReferenceStrainDetailDialog`, `WorkloadWeightsDialog`.

### 1.4 Libraries (A5)

| Package | Present? | Version | Notes |
|---|---|---|---|
| `@mui/material` | yes | ^9.4.0 | MUI 9: check slot/API changes before reusing v5/v6 examples |
| `@mui/x-data-grid*` | **no** | — | No licence tier question. Reorder and pinning must be custom. |
| `@dnd-kit/*` | **no** | — | Adding it is a new dependency → needs your approval (Q4) |
| `react-beautiful-dnd` | **no** | — | Deprecated upstream anyway; do not add |
| `react` | yes | ^19.3.0 | Not 18 |
| Test stack | yes | vitest ^5, @testing-library/react ^16, jsdom | §5 |

---

## 2. Fit assessment of the proposed design

### 2.1 What fits as proposed

| Proposal element | Verdict | Evidence |
|---|---|---|
| Registry per table, keyed by stable `id` | **Fits.** Shared wrappers already key columns by `key` (`RegisterTable.tsx:13`, `DataTable.tsx:7`). | — |
| `useTableLayout(tableKey, registry)` returning ordered, visible columns | **Fits** `RegisterTable`/`DataTable` with no wrapper changes: the caller passes `columns={layout.visibleColumns}`. | `ReportResultsTable.tsx:245-288` already does exactly this filter-then-pass by hand |
| Reconcile on load (append new, drop unknown) | **Fits, and is necessary.** Several deployments a month add columns (e.g. recent FP/HPLC work). | — |
| Presentation only; exports keep their own columns | **Already true today**, see §4.2 | — |
| Server persistence + localStorage cache | **Fits** with the corrections in §2.2 | — |
| Not GxP / no audit | **Consistent with precedent**, but needs explicit code (§2.2 #3) | `MicroLimsDbContext.cs:302-312` |

### 2.2 Deviations, and what has to change in the design

1. **There is no DnD library.** For P1, recommend **up/down arrow buttons plus a visibility checkbox per row** in the configurator. That adds no dependency, is keyboard-accessible by default, and works on tablets. `@dnd-kit` can come later, after sign-off (Q4).
2. **The localStorage cache must be per-user and survive logout safely.** The current pattern is `microlims_sidebar_collapsed` and `microlims-theme-mode` (`fe/layouts/MainLayout.tsx:35-40`, `fe/theme/ThemeModeContext.tsx:8, :48`). These are global keys and are not cleared on logout (`fe/services/apiClient.ts:63-75`, `fe/contexts/AuthContext.tsx:120-128`). Proposed key: `microlims_layout:{userId}:{tableKey}`. The userId is already in localStorage as `microlims_user_id` (`AuthContext.tsx:105`). The server copy wins on load and the cache is only for first paint. (The same per-user bug exists for theme and sidebar. It is out of scope here; see Q8.)
3. **"No audit" needs explicit code plus a test.**
   - Add `e.Entity is not UserViewPreference && // UI presentation state; not a GxP record` to `CapturePendingAuditEntries` (`be/MicroLIMS.Persistence/DbContext/MicroLimsDbContext.cs:302-312`). Precedents in the same list: `NotificationLog` (read state), `RefreshToken`, `ErrorLog`, `Incident`.
   - Add a test modelled on `be/MicroLIMS.Tests/UnitTests/NotificationAuditExclusionTests.cs`.
4. **User-reference registry.** `UserDeletionTests.UserReferenceRegistry_AccountsForEveryUserIdNamedPropertyInTheModel` (`be/MicroLIMS.Tests/UnitTests/UserDeletionTests.cs:248`) fails the build if a new `*UserId` property is not listed in `be/MicroLIMS.Application/Services/UserReferenceRegistry.cs`. Proposed entry: `UserViewPreference.UserId` → **`Excluded`**, with a Cascade FK ("per-user UI housekeeping, deleted with the user"). This matches `PasswordHistory`, `RefreshToken` and `UserOrgMembership` (`:113-116`).
5. **Authorization snapshot.** `be/MicroLIMS.Tests/ArchitectureTests/authorization-matrix.txt` is a reviewed snapshot of every endpoint × role. New endpoints add lines there, and that diff is expected.
6. **Controllers must not touch the DbContext** (`be/MicroLIMS.Tests/ArchitectureTests/ControllerDependencyTests.cs`). The endpoint needs an Application service (`IUserViewPreferenceService`) that uses `IMicroLimsDbContext`.
7. **Locked columns are enforced client-side.** CLAUDE.md principle 2 keeps lab rules on the backend. Here the rule is safe client-side because the reconcile step **forces locked columns visible whatever the saved payload says**, so a tampered `hidden[]` cannot hide them. The server stores the layout as opaque JSON with a size cap. Q5 asks whether you also want a server-side locked list.
8. **`RegisterTable` phone mode uses `columns[0]` as the card title** (`RegisterTable.tsx:135-136`). The first column of every `RegisterTable` registry must be `locked` **and** pinned to position 0, and reorder must not be able to move another column into slot 0.
9. **On the pipeline, the selection checkbox sits inside the Item / Reference cell** (`SampleRegisterTable.tsx:284-299, :537-552`). The checkbox would disappear if that column could be hidden, which is one more reason to lock and pin it. A cleaner option is to split the checkbox into its own fixed leading column during the P1 refactor (Q6).
10. **`widths{}` / resize:** today widths are `sx` `minWidth`/`width` per cell, and there is no resize handle anywhere. Keep `widths` in the schema but **defer resize** to after P2.
11. **`schemaVersion`:** reconcile covers adding and removing columns. Keep `schemaVersion` for *semantic* changes such as a column id renamed or split, where the hook should discard the saved layout and fall back to default.
12. **The view mode (table/card/kanban) is URL-only** (`ReceivingTestingWorkspacePage.tsx:163-166`). It would fit naturally in the same preference store later. It is not in the brief, so it stays out of scope (Q7).

### 2.3 Backend shape that fits the conventions (B7, B8). This is a proposal and is not implemented.

| Layer | Item | Convention it follows |
|---|---|---|
| Domain | `UserViewPreference { int Id; int UserId; User User; string ViewKey (≤100); int SchemaVersion; string LayoutJson; DateTime UpdatedAtUtc }`, unique `(UserId, ViewKey)` | `ErrorLog`-style plain entity, no `IVersionedEntity` (last write wins is fine for UI state) |
| Persistence | `UserViewPreferenceConfiguration`, `LayoutJson` → `HasColumnType("jsonb")`, FK Cascade | jsonb pattern: `ErrorLogConfiguration.cs:34-35`, `SecurityAuditEventConfiguration.cs:31`, `ParameterResultConfiguration.cs:20` |
| Persistence | `DbSet<UserViewPreference>` + audit exclusion (§2.2 #3) + migration | `MicroLimsDbContext.cs:202-205` |
| Application | `IUserViewPreferenceService` (`GetAsync(userId, keys[])`, `UpsertAsync`, `DeleteAsync`), DTOs, validator (key format, JSON ≤ 16 KB `[proposal]`) | Services under `be/MicroLIMS.Application/Services`, interfaces under `…/Interfaces` |
| API | `UserPreferencesController` at `api/me/view-preferences`: `GET ?keys=a,b`, `PUT {viewKey}`, `DELETE {viewKey}` ("Reset to default"). Plain `[Authorize]`, no permission policy, because a user only ever touches their own rows. | Plain `[Authorize]` precedent: `AuthenticationController.cs:97-119` (`Me`, `ChangePassword`) |
| Current user | `int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value)`, as each controller's private `CurrentUserId` property | e.g. `SampleReviewController.cs:33`, `MediaController.cs:42` |
| Department context (P3) | `UserOrgMembership(DepartmentId, SectionId?, PhyschemArea?)` (`be/MicroLIMS.Domain/Entities/UserOrgMembership.cs`), `IUserSectionScopeService.GetAccessibleSectionIdsAsync` (`be/MicroLIMS.Application/Interfaces/IUserSectionScopeService.cs:7`; null = admin, unrestricted). Role: `User.RoleId`. | — |

**B6: how user preferences are treated today.** There is no server-side user preference store. `ConfigurationSetting` (`be/MicroLIMS.Domain/Entities/ConfigurationSetting.cs`) is **system-wide** key/value with `ModifiedByUserId`, and it **is** audited because it is not on the exclusion list. It is the wrong home for per-user UI state. The closest per-user, non-GxP precedent is `NotificationLog` read state, which is audit-excluded. The brief's "non-GxP, prunable" stance is consistent with how `ErrorLog`/`Incident` (`ErrorLogRetentionWorker`) and `NotificationLog` are treated. Rows do not need pruning, because each user has at most one row per view; deleting the user cascades them.

### 2.4 Permission for admin default layouts (B9, P3)

- 48 codes are declared and 38 enforced (`PermissionConstants.cs:103-138`). None covers UI or layout.
- Option A, **reuse `Roles.Manage`**. This is natural if defaults are **per role**, because whoever shapes a role's access also shapes its default view. There are no seed or catalog changes.
- Option B, **a new code `Layouts.ManageDefaults`**. This is cleaner if defaults are **per department/section** and Section Heads should set them without being able to edit roles. It costs a seed row and entries in `All` and `Enforced`, and `PermissionAuthorizationTests.EveryEnforcedCode_IsCheckedSomewhere` must pass. The `authorization-matrix.txt` diff follows.
- Recommendation: **B if Section Heads set departmental defaults; otherwise A.** See Q3.

---

## 3. Proposed phases (smallest first)

**P1: foundation + pilot on the sample register only**
- FE: `ColumnDef` registry type (`id, label, defaultVisible, defaultOrder, locked, pinned: 'start'|null, isAvailable?(ctx)`), a pure `reconcileLayout(registry, saved)` function, the `useTableLayout(tableKey, registry, ctx)` hook (server fetch, per-user localStorage cache, debounced PUT), and a `ColumnConfigurator` popover (checkbox + up/down + "Reset to default", locked rows disabled).
- FE: refactor `SampleRegisterTable` from hand-written JSX to a column array (same render output) and wire it to the hook. The `tableKey` is shared by `/receiving` and the lab workspaces, or kept per lab (Q2). Optionally split the selection checkbox into a fixed leading column (Q6). The compact split-pane list stays fixed.
- BE: entity + configuration + migration, audit exclusion + test, user-reference registry entry, Application service + validator, controller, authorization-matrix snapshot update.
- Tests: `reconcileLayout` unit tests (new column appended, unknown id dropped, locked forced visible, pinned stays first, bad JSON → default), a hook test with mocked service (pattern in §5), backend unit tests for service + audit exclusion.

**P2: roll out to list pages**
1. `RegisterTable` consumers (§1.3 A, list pages only), which only need registry entries.
2. `DataTable` reports. Migrate `ReportResultsTable`'s session toggle onto the hook.
3. Approved Media / Approved Cryovials. Replace their session toggles (refactor needed).
4. Hand-written list pages in priority order (Document Library, Tracking Board, Users, OOS, …). Each one needs a refactor to a column array first.

Optionally, lift the hook into `RegisterTable`/`DataTable` behind an optional `layoutKey` prop to avoid boilerplate.

**P3: admin defaults per role and/or department.** Add a nullable `RoleId`/`SectionId` scope to the same table, or a sibling table. Resolution order: user → section → role → registry default. Gated by the code chosen in §2.4.

**P4: workspace panels.** Reuse the same endpoint with `viewKey = panel:{workspace}`. Collapse/expand only at first, plus reorder for *informational* panels (§6).

---

## 4. Locked columns: proposal for your sign-off

### 4.1 `SampleRegisterTable` (P1)

| Column | Proposal | Reason |
|---|---|---|
| Item / Reference | **Locked, pinned first** | Sample identity (reference no.). Holds the grouped-action checkbox, the retest-chain expander, the OOS chip and the controlled-docs link. Retest rows indent inside it. |
| # (sample ID) | **Locked** (per your brief) | The internal ID analysts quote. *Note:* Item / Reference already carries the human reference no., so you could make this hideable (Q9). |
| Sample Status | **Locked** | Drives which actions are valid |
| Test Status Summary | **Locked** | The entry point to result entry per test and to the "Needs Preparation" action (`TestStatusSummaryCell.tsx:95-111`) |
| Actions | **Locked, pinned last** | Void / Edit / Audit / Report |
| Assigned To | Hideable | The Assign action is duplicated in the Actions menu (`SampleActionMenu.tsx:174`) |
| Received At, Item Type, Cause, Sampled By, Batch / Control No. | Hideable | Informational. *Flag:* for product samples Batch is a key identifier, so consider `defaultVisible: true` and leave it hideable. |

### 4.2 General rules for P2 tables (proposal)

- **First column (record identifier) is locked and pinned** on every `RegisterTable` (phone card title).
- **`rowActions` / Actions column is locked.** In `RegisterTable` it lies outside `columns` (`RegisterTable.tsx:338`), so it is locked automatically.
- **Status columns that carry a GMP signal are locked**: release/approval status, expiry, calibration/maintenance due, stock level. This applies to Approved Media (Status, Expiry), Approved Cryovials (Status, Expiry), Materials (Status/Expiry/Stock `[UNVERIFIED exact ids]`), Equipment (Status, Calibration due `[UNVERIFIED exact ids]`), Working Standard Lots (Status, Expiry), Document Library (Status, Effective/Review date), and Reports results (Result, Result Level, Status).
  - *Note:* today `ReportResultsTable` lets users hide **Result** and **Status** (`:95-97`). That is a change from current behaviour.
- **Columns already conditional on permission or record state** (§1.2 table) keep that condition through `isAvailable(ctx)`. User preference can only narrow it further.
- **Tables excluded from the feature entirely:** everything in §1.3 C "should stay fixed", plus SST, Titration/Gravimetric and HPLC/ICP entry tables.

---

## 5. Constraints and risks (D11–D13)

### 5.1 Tables where reorder or hide would break something (D11)

| Table | Feature | Risk if made configurable | Verdict |
|---|---|---|---|
| `PrimaryObservationMatrixPanel` | Sticky first column (`:505-519, :606`), sticky header, **columns = assigned tests (data)** (`:522`), **bulk-fill "Fill All Unset as No Growth"** (`:144-199, :418`), per-column lock by `isResultEntryAllowed` (`:523`) | Hiding a test column would hide unset cells while bulk-fill still writes them, so the analyst would sign values they cannot see | **Never configurable** |
| `BatchConfirmatoryPlatingPanel` | Sticky first column (`:772, :809`), data-driven columns | Same as above | **Never** |
| `MultiSampleEntryGrid` | 17-column entry grid | Hidden required fields → save fails or silent defaults | **Never** |
| `UnitEntryGrid`, HPLC/ICP replicate entry, Weight variation, Disintegration, Dissolution, location result grids | Result entry | Hidden inputs = incomplete GMP record | **Never** |
| `SampleRegisterTable` | Sticky header, nested retest rows, checkbox in column 3, hard-coded `colSpan={11}` (`:588`) | Empty-state `colSpan` must become `visibleColumns.length`. The checkbox column must stay. | OK with locks (§4.1) |
| `RegisterTable` (all) | Phone card uses `columns[0]` | Reordering would change the card title | OK with first column pinned |
| `DataTable` | Selection checkbox is a separate leading column (`DataTable.tsx:57-70`) | None | OK |

- **`FinalResultMatrixPanel`:** not found in the repo `[UNVERIFIED]`.
- **Virtualisation:** none anywhere (no react-window or virtuoso).
- **Row grouping:** only the `SampleRegisterTable` retest nesting and the OOS expandable rows.
- **Cell-to-cell keyboard navigation:** none. Arrow-key matches are icon imports only. Row-level Enter/Space activation exists in `RegisterTable`, `DataTable` and `SampleRegisterTable` and is column-agnostic.

### 5.2 Print and export (D12): confirmed independent of the on-screen columns

| Output | Source of columns | Reads screen layout? |
|---|---|---|
| `PrintableTable` (Approved Media/Cryovials, Materials, Equipment) | Fixed `printColumns` arrays (e.g. `ApprovedMediaListPage.tsx:324-345`, `ApprovedCryovialListPage.tsx:281`). The component comment states the intent (`PrintableTable.tsx:17-20`). | **No** |
| Results CSV (selected) | Fixed header list (`ReportResultsTable.tsx:179-183`) | **No**, even though this table already has a visibility toggle |
| Results CSV (all matching) | Server-side `ReportingService.exportCsv` (`:156`) | **No** |
| Results PDF | `exportResultsPdf(records, …)` builds its own `<thead>` (`fe/modules/reports/utils/exportPdf.ts:57, :236`) | **No** |
| Sample report / CoA / media / cryovial reports | Standalone pages (`SampleReportPage`, `SampleCoaPage`, `MediaReportPage`, `CryovialReportPage`) | **No** |
| `PinnedLightTheme` | Theme wrapper only, forces light mode (`fe/theme/PinnedLightTheme.tsx:5-15`) | n/a |

Guardrail for implementation: export and print code must never import `useTableLayout`. A one-line architecture test, or a knip/eslint rule, could enforce this `[proposal]`.

### 5.3 Test setup (D13)

- Vitest + jsdom (`frontend/vite.config.ts:6`, `globals: false`), `@testing-library/react` 16 with `renderHook`, `@testing-library/user-event`. There is no global setup file and no jest-dom matchers.
- 28 existing test files. The hook-test pattern is `fe/hooks/useMenuGroups.test.tsx`: `vi.mock` of `AuthContext` and the service, then `renderHook` + `waitFor`. A `useTableLayout` test follows it directly.
- Backend: xUnit with in-memory `MicroLimsDbContext` (`NotificationAuditExclusionTests.cs:13-19`), plus architecture tests (`ControllerDependencyTests`, `PermissionAuthorizationTests`, `AuthorizationMatrixTests`, and the user-reference scan in `UserDeletionTests.cs:248`) that will flag a new endpoint or entity unless it is wired as in §2.2.

---

## 6. Workspace panels: phase 2 feasibility (C10)

| Workspace | Composition | Order constraint | Hide / reorder safe? |
|---|---|---|---|
| **Pathogen session** (`fe/modules/testingWorkspace/pathogenSession/PathogenSessionDialog.tsx`) | **Static stepper**, 6 steps (`:44-51`): Overview → Shared TSB → Test Workflows → **Primary Matrix (Panel A)** → **Confirmatory Plating (Panel B)** → **Review & Complete (Panel C)**. One panel renders at a time (`:289-345`). | Steps are gated: Confirmatory is disabled without eligible confirmations, Review is disabled unless `canReview` (`:244-248`). A → B → C follows the microbiology. | **No.** All six steps are fixed. Not panels in the layout sense. |
| **HPLC instrument workspace** (`fe/modules/hplcWorkspace/run/HplcInstrumentWorkspace.tsx`) | **Static tabs** (`:237-251`) wrapping numbered `ResultSection step={1..4}`: Run setup → System suitability → Sample assignment → Testing & evidence, + History (`:256-319`) | SST precedes sample assignment and evidence by method | **No reorder.** Collapse-only inside a tab would be safe. `MethodReadOnlyPanel` could be collapsible. |
| **Selected sample testing panel** (`fe/modules/testingWorkspace/SelectedSampleTestingPanel.tsx`) | Static: header → `ItemDocumentsCard` (`:386`) → preparation warning → stack of `AssignedTestCard`s (`:475`) | Test cards are data-driven (one per assigned test) | Hiding or collapsing `ItemDocumentsCard` is safe. **The prep warning and test cards must not be hideable.** |
| **Count-test / pathogen step dialogs** (`fe/modules/testingWorkspace/pathogenSteps/*`, `TestWorkflowDialog.tsx`, `PathogenStepDialog.tsx`) | One step panel chosen by workflow state | Time gates (incubation / broth waiting: `BrothWaitingPanel.tsx`) | **No** |
| **ICP workspace** | Same shape as HPLC (`fe/modules/icpWorkspace/*`: calibration → CCV → samples → entry → evidence) `[UNVERIFIED: top-level container not read in detail]` | Calibration and CCV precede entry | **No reorder** |

There is no panel registry: every workspace is composed statically in JSX. **Never hideable or reorderable:** the pathogen steps (all 6, especially Panels A/B/C), HPLC/ICP numbered sections, the SST panel, the sample-preparation warning, assigned-test cards, result-entry panels, and review/sign-off panels. **Candidates for collapse only:** `ItemDocumentsCard`, `MethodReadOnlyPanel` / `IcpMethodReadOnlyPanel`, the history tables. P4 should therefore be scoped as **"collapse informational panels"**, not reorder.

---

## 7. Open questions for you

1. **Q1. Which table is the pilot?** I assumed "sample pipeline / worklist" means the **Laboratory Register** (`SampleRegisterTable`, used in Receiving & Testing and `/receiving`). The other candidate is the analyst dashboard's **Today's Work** table (`TodaysWorkTable.tsx`, 6 columns). Which one?
2. **Q2. `tableKey` scope:** one layout shared across `/receiving` and every lab workspace (Micro, FP, RM&PM), or one per lab? The columns are identical today.
3. **Q3. Admin defaults (P3):** per role, per department/section, or both? This decides between reusing `Roles.Manage` and adding `Layouts.ManageDefaults` (§2.4).
4. **Q4. Drag-and-drop:** OK to ship P1 with up/down buttons only (no new dependency), and to decide on `@dnd-kit` later?
5. **Q5. Server-side locked list:** is client-side reconcile enforcement enough (§2.2 #7), or do you want the backend to hold the locked column ids per `tableKey` and reject payloads that hide them?
6. **Q6. Pipeline checkbox:** during the P1 refactor, move the grouped-action checkbox out of the Item / Reference cell into its own fixed leading column? This is a visible UI change.
7. **Q7. View mode:** persist the table/card/kanban choice in the same store, or keep it URL-only? Out of scope unless you say so.
8. **Q8. Theme and sidebar keys:** fix the existing non-per-user localStorage keys (shared lab PCs inherit the previous user's theme and sidebar) in this work, or as a separate ticket?
9. **Q9. Sample "#" column:** keep it locked as briefed, or make it hideable now that Item / Reference shows the reference number?
10. **Q10. Reports behaviour change:** P2 would stop users hiding Result / Result Level / Status in `ReportResultsTable`, which they can do today. Agree?
11. **Q11. `FinalResultMatrixPanel`:** the brief names it but it is not in the repo. Is it planned, or does it go by another name?
