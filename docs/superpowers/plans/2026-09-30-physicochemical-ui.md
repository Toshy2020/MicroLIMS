# Physicochemical Laboratory UI — Plan (A → F)

Branch: `feat/physchem-ui` (from `feat/hplc-chain` be66141). Frontend only — no API or GMP-rule changes.
Verification per sub-project: `npm run build` (tsc + vite) clean, `npm run lint` no new errors, browser pass on every touched page (light + dark).

## Goal
One consistent, faster, modern UI for the 14 physicochemical pages (Laboratory + Configuration areas) and the FP result-entry panels, then promote the proven patterns app-wide.

## Audit findings driving the design (2026-09-30)
- Every page hand-builds its MUI `Table`: no sort anywhere, pagination only on Materials Stock, no search on Instruments / Equation Types.
- Status shown with raw `<Chip>` instead of the token-driven `StatusBadge`.
- Loading = `CircularProgress`, not skeletons; empty states differ per page.
- Three ways to open a record (row click, text link, action button); create is a modal on some pages, a route on others.
- Form validation shown as one top `Alert`, not on the field.
- Result panels: acceptance criteria in a table (Gravimetric) vs buried in prose (Dissolution); live calculated preview in one, not the other; completed view shows raw data in one, not the other.

## Design rules (all sub-projects)
1. **Page anatomy**: `LabPage` = header (title, subtitle, primary action right) → optional `KpiStrip` → `FilterBar` → content. Page padding, gaps and max width come from `LabPage`, never per page.
2. **Registers**: `RegisterTable` for every list — sortable columns, client pagination (25/50/100), sticky header, skeleton loading, `EmptyState` with the primary action, row click = open record, `⋮` menu = secondary actions (edit, deactivate, history).
3. **Status**: only `StatusBadge` (tokens in `statusTokens.ts`); new FP statuses are added to `STATUS_TONE`, never local colors.
4. **Forms**: `FormDialog` (on `FloatingDialog`) with per-field errors (`errors: Record<field,string>` → `helperText`), a server error alert only for non-field errors, fixed footer. Signed actions keep `SignatureDialog`; reason-for-change uses the existing `ReasonDialog` pattern.
5. **Numbers**: measured and calculated values right-aligned, tabular figures (`font-variant-numeric: tabular-nums`), units in a muted suffix; IDs/codes in `monospaceFontFamily`.
6. **Result panels**: `ResultSection` (numbered outlined section) → `CriteriaCard` (acceptance criteria as a table, always visible above entry) → entry grid with live calculated column → `VerdictBanner` (Pass / Fail / Pending, with the governing limit). The completed view shows the same raw data read-only.
7. Colors, radius and shadows come from the theme only. Brand purple stays.

## Sub-projects

### A — Foundation (`src/components/lab/`)
New components (each one file, exported from `src/components/lab/index.ts`):
- `LabPage` — `{ title, subtitle?, actions?, kpis?, filters?, children }`.
- `KpiStrip` — wraps `configHierarchy/SummaryTiles` API (`tiles`), adds `loading` skeleton.
- `FilterBar` — `{ search, onSearch, placeholder?, children (extra selects), resultCount?, onRefresh? }`; debounced search 250 ms.
- `RegisterTable<T>` — columns `{ key, label, render?, align?, sortable?, sortValue?, width?, numeric? }`, `rows`, `getRowId`, `onRowClick?`, `rowActions?: (row) => {label, onClick, disabled?, danger?}[]`, `loading`, `empty: { title, description?, action? }`, `pageSize?` (default 25), `dense?`. Client-side sort + pagination. Uses `tableHeadSx`.
- `EmptyState` — icon, title, description, action.
- `FormDialog` — `{ open, title, onClose, onSubmit, submitLabel, submitting, error?, children, maxWidth? }`, form element so Enter submits.
- `ResultSection` — `{ step?, title, status?, children, actions? }`.
- `CriteriaCard` — `{ rows: { parameter, criterion, unit?, source? }[] }`.
- `VerdictBanner` — `{ verdict: "Pass"|"Fail"|"Pending"|"Inconclusive", detail? }` using status tokens.
- `NumericCell` / `formatNumber(value, decimals)` — tabular numbers + unit suffix.
- STATUS_TONE additions: `Pass`, `Fail`, `BelowSpec`/`Below Spec`, `Suitable`, `NotSuitable`, `Conforms`, `DoesNotConform`, `Prepared`, `InUse`, `Discarded`, `Valid`, `Invalid`, `Draft`-safe (check existing keys first).
- Reference migration: `EquationTypesPage` + `FpInstrumentsPage` onto the kit (proves the API).

### B — Configuration
Migrate onto the kit: `ChromatographyColumnsPage`, `MaterialMasterPage`, `SolutionMasterPage` (+ solutionMaster dialogs → field errors), `HplcMethodsPage`/`HplcMethodTable`, physicochemical view of `TestMasterPage` (`lab="fp"` branch only — the micro branch stays untouched until F).

### C — Lab support
`SystemSuitabilityRunsPage`, `CalibrationRunsPage` (new-run form moves from cramped dialog to `maxWidth="lg"` sectioned `FormDialog`), `PreparationListPage`, `MaterialsPage`, `EquipmentInventoryPage` (shared with micro — kit only changes presentation, so both labs benefit).

### D — HPLC Workspace
`HplcWorkspacePage` overview (instrument cards → consistent card grid + KpiStrip), run wizard steps, SST panel, sample entry (`ReplicateEntryTable` → numeric cells + live calculation), evidence, review panel — `ResultSection`/`CriteriaCard`/`VerdictBanner`.

### E — Physicochemical result panels
`DissolutionPanel`, `DisintegrationPanel`, `WeightVariationPanel`, `GravimetricPanel`, `ElementalAssayPanel`, `StandardComparisonPanel`, `MeasurementPanel`: criteria card above entry, live calculated preview everywhere (display only — the server stays the calculator of record), consistent completed read-only view with raw replicates and verdict. No change to request payloads.

### F — Promote to global theme
Move `src/components/lab/*` to be the app-wide kit: global theme gets tabular numerals on tables, `MuiTableCell` density, `MuiChip` → steer to `StatusBadge`; migrate the remaining high-traffic micro registers (Media, Cryovials, Organisms, Water/EM/AC config lists) onto `RegisterTable` without behaviour change.

## Execution
- Sonnet subagents (≤3 parallel) implement, disjoint files each; agy (≤3 parallel) for reading/review.
- Each sub-project: implement → build + lint → browser check → commit.
- Out of scope: backend, permissions, calculation logic, report/CoA print layouts.
