# KPI Remodel S4 - New KPI Page Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
> **Repository state note (checked 2026-10-13, see `2026-10-11-kpi-remodel-index.md`):** commit `570cf02a` has since committed the Media/Cryovial migration `20261011103646_...` and its snapshot hunks, so the "UNTRACKED migration / dirty snapshot" statements below describe the earlier state. Only `AnalystKpiTab.tsx`, `AnalystKpiService.ts` and `reportingTypes.ts` carry the uncommitted 2026-10-11 KPI page patch. Always re-run `git status` first; if shared files are dirty with someone else's hunks, the `git add -p` protocol in these plans still applies.

**Goal:** Replace the old Analyst KPI tab and the 14 remaining `/api/kpi/*` endpoints with a registry-driven KPI & Performance page that renders one dashboard response, traces every figure to its records, and prints exactly the figures shown on screen.

**Architecture:** The page is a thin renderer over the S2 (`kpiDashboardService`) and S3 (`kpiWorkloadService`) responses: two data hooks, one tile component, one section component, a workload table, two drawers (definitions, records) and a PDF builder that consumes the same objects and the same formatters as the screen. All rates, thresholds, on-time rules and scoping stay on the backend. After the page is mounted in `Reports.tsx`, the old frontend files, `KpiController`, and every `KpiService` method except `GetOverdueAnalystStageSamplesAsync` (still read by `DashboardService`) are deleted; the legacy `WorkloadWeight` tables stay one more release.

**Tech Stack:** React 19, TypeScript, MUI 9, Recharts 3, Vitest 5 + React Testing Library; ASP.NET Core, EF Core, xUnit.

**Spec:** `docs/superpowers/specs/2026-10-11-kpi-performance-remodel-design.md`, section 5 (page design), section 6 (permissions: no new permission here), section 7 row S4 (removals), section 8 (frontend tests). Contract: "SHARED CONTRACT: KPI and Performance remodel (S1 to S4)", section 5 (files, component contracts) and section 7 (deviations 15 and 17).

**Preconditions:** S1, S2 and S3 are merged on this branch. In particular these files exist: `frontend/src/modules/kpi/types/kpiTypes.ts`, `types/workloadTypes.ts`, `services/kpiDashboardService.ts`, `services/kpiWorkloadService.ts`, `components/WorkflowProfilesDialog.tsx`, and the backend `KpiDashboardController`, `KpiWorkloadController`, `TestServiceFactory.KpiDashboard`. Task 1 step 1 checks this and stops the plan if anything is missing.

## Global Constraints

- Branch `refactor/consolidation`. About 41 files that belong to other work are dirty or untracked (Media/Cryovial storage, an uncommitted migration `20261011103646_AddMediaStorageEntriesAndCryovialStorageEquipment`, a modified `MicroLimsDbContextModelSnapshot.cs`, a modified `frontend/src/routes/routes.ts`). Never touch, stage or revert them.
- Stage ONLY the files a task names, by explicit path (`git add <path> ...`, `git rm <path>`). Never `git add -A`, `git add .` or `git commit -a`. For `DbSeeder.cs` check `git diff -- <file>` first and use `git add -p` if anything but the KPI hunk shows.
- Index hygiene: a `git commit` commits everything already staged, and someone may have staged unrelated Media/Cryovial files. Task 1 step 1 checks `git diff --cached --name-only` is empty before the first commit (unstage strangers with `git restore --staged <path>`). Before EVERY later `git commit`, after the `git add`/`git rm` lines, run `git diff --cached --name-only` and confirm it lists only that task's files.
- Backend tests run with the artifacts form so a running `MicroLIMS.API` (which locks `bin`) does not matter: `dotnet test backend/MicroLIMS.Tests --artifacts-path "$TEMP/kpi-art" ...`. ripgrep/grep are not installed: use the Grep tool, Glob, or `git grep`.
- Every commit message ends with the line `Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>`. Write the message with a Bash heredoc (PowerShell 5.1 adds a BOM).
- S4 adds no database migration and no new permission. `Kpi.View` (`PERMISSIONS.KPI_VIEW = "Kpi.View"`) gates the Reports tab and stays the read policy on the replacement controllers.
- `MinSampleForRate = 5`: the backend sets `result.tooFewRecords`; the page only shows the muted value and the words "too few records to judge". It never recomputes the rule.
- 1 work unit = 1 standard analyst-hour. Utilisation above 100% is shown as "check profile" (server flag `utilisationCheckProfile`), never as praise.
- No rank, no "top performer", no leaderboard wording, no colour that treats low output as failure. The workload table is alphabetical by `fullName` by default; sorting by a performance value happens only after an explicit header click, is held in component state only, and is never saved (no `localStorage`).
- Forward load above 100% (server flag `forwardLoadOverCapacity`) is the only amber treatment on people. Everything else is neutral. (Gap and flag chips use the same amber tone as a data-quality warning, not as a judgement of output.)
- `status === "NoData"` (or a null value) renders "No data", never `0` or `100%`. A real `0` with status `Ok` renders as `0`.
- Prior-period text appears only when `result.prior != null`. The page computes no deltas, arrows or percentage changes.
- A KPI that failed (`entry.error`) becomes an error tile for that key only. Nothing is swallowed into zeros or an empty page.
- D8 is enforced by the server. The page never filters other analysts out itself and sends `analystId` only for the drill-down of a workload row it was given. When `data.onlyOwnRow` is true it says so.
- Laboratory logic stays on the backend: the page formats numbers, it does not calculate rates, targets, on-time outcomes or capacity.
- Printed figures equal screen figures: `exportKpiPdf` receives the `KpiDashboard` and `AnalystWorkload` objects the screen holds and calls the same formatters (`utils/kpiFormat.ts`).
- Wire scales are fixed in exactly one place, `utils/kpiFormat.ts`: `KPI_PERCENT_SCALE` (KPI values whose unit is `"%"`) and `WORKLOAD_RATIO_SCALE` (workload ratios). Task 1 step 2 confirms them against the backend before anything is built on them.
- Dates in the UI and on the wire are `yyyy-MM-dd`; `from` and `to` are inclusive.
- The legacy `WorkloadWeight` / `WorkloadWeightHistory` tables, entities, `DbSet`s and their two `UserReferenceRegistry` lines are NOT removed here (spec: read-only for one release). `DropLegacyWorkloadWeights` is a later release.
- Frontend test conventions: import `describe, it, expect, vi, afterEach` from `"vitest"` (globals are off), call `afterEach(cleanup)`, wrap themed components in `PinnedLightTheme`, assert on text and roles never on Recharts SVG, mock `useAuth` with `vi.mock`. Commands run from `E:\MicroLIMS\MicroLIMS\frontend`.
- Backend Application code never calls `DateTime.UtcNow` (`ClockUsageTests`); S4 adds no new backend production code, only tests and deletions.

## Review Focus

Failure modes the spec implies that a real user will hit, most likely first. Each is pinned by a named test in the owning task.

1. **A genuine zero.** WIP backlog of 0 tests, EM excursion rate of 0.0%, a `0` count of returns. Expected: an `Ok` tile shows `0 tests` / `0.0%`, never "No data" and never blank; only `NoData` or a null value shows "No data". Pinned in Task 1 (`entryView` keeps a zero as a value) and Task 2 (`KpiTile` "shows a real zero as a value, not as no data").
2. **Filter changed while a request is in flight.** The user flips the period twice; the slow first response arrives last. Expected: the newest filter's figures stay on screen, a late error from the old request is ignored, `loading` ends false. Pinned in Task 5 (`ignores a slow response for an earlier filter` and `ignores a late failure from an earlier filter`).
3. **One call fails, the rest are fine.** The workload endpoint answers 403/500 while the dashboard is healthy, or one KPI comes back as an error entry. Expected: tiles still render, the workload block shows its own error, only the failing KPI shows an error tile, nothing turns into zeros. Pinned in Task 2 (error tile) and Task 9 (`a workload failure leaves the tiles on screen`).
4. **A cleared or inverted date range.** `<input type="date">` reports `""` while the user is typing and a user can pick From after To. Expected: no request is sent, an inline message names the problem, the last valid figures stay. Pinned in Task 8 (`does not call onChange for an inverted or empty range`).
5. **A viewer without `Kpi.ViewTeam`.** The server returns one row with `onlyOwnRow: true`, and rows may arrive in an order that is not alphabetical (for example by delivered units). Expected: the page says "Showing your own numbers only", shows no team framing, and a multi-row response is alphabetical by default and returns to alphabetical after a remount (the explicit sort is not saved). Pinned in Task 4 (`says so when only the viewer's own row is returned`, `lists analysts alphabetically even when the API sends them by output`, `does not remember an explicit sort`).

6. **Percent shown 100x too small.** The backend sends every `"%"` KPI value and the `alertRate` / `actionRate` extras as 0..1 fractions (S2 `KpiScope.Rate`). Expected: 0.875 prints "87.5%", 0.018 prints "1.8%", a real 0 prints "0.0%". Pinned in Task 1 (`formatKpiValue('%', 0.875) === '87.5%'`, `treats KPI percent values as fractions`), Task 2 (`shows a fractional percent value as a percentage`), Task 3 (`feeds a two-point percent series to the chart as scaled percentages`) and Task 6 (`formats a fractional percent record value as a percentage`). All fixtures use fractions.
7. **A count panel with a genuine zero and no series** (for example no returns in the period). Expected: the panel prints "0 tests" and "None in this period", not a bare heading. Pinned in Task 3 (`shows a count panel with a genuine zero and no series as a value`).

## File Structure

All frontend paths are under `frontend/src/modules/kpi/` unless a full path is given.

| File | Action | Responsibility |
|---|---|---|
| `utils/kpiFormat.ts` | Create | The only place numbers become text: unit formatting, wire scales, `entryView` state machine, extras/prior/gap text, alphabetical sort |
| `utils/kpiFilters.ts` | Create | `KpiFiltersValue`, default 30-day range, range validation, filters to query params |
| `kpiFixtures.ts` | Create | Test-only builders for `KpiEntry`, `KpiDashboard`, `AnalystWorkload` (not a test file) |
| `components/KpiTile.tsx` | Create | One headline tile with Ok / NoData / Partial / error / too-few-records states; exports `KpiChips` |
| `components/KpiSection.tsx` | Create | A titled group of panels: stage bars, aging table, returns list, trend lines |
| `components/WorkloadTable.tsx` | Create | Team/own capacity and flow table, alphabetical, no ranking, banners and gap chips |
| `hooks/useGuardedFetch.ts` | Create | Latest-request-wins fetch state shared by both data hooks |
| `hooks/useKpiDashboard.ts` | Create | Dashboard fetch for a filter value |
| `hooks/useKpiWorkload.ts` | Create | Workload fetch for a filter value and optional analyst |
| `components/DefinitionsDrawer.tsx` | Create | Definitions, clock events, population, source and profiles in force, straight from the registry |
| `components/KpiRecordsDrawer.tsx` | Create | Paged record list behind any figure |
| `utils/exportKpiPdf.ts` | Create | HTML/print report from the same response objects |
| `components/KpiFilters.tsx` | Create | Page-owned filter bar and the Definitions / Workflow Profiles / Export buttons |
| `components/KpiPerformanceTab.tsx` | Create | Composes everything; the component Reports mounts |
| `frontend/src/pages/Reports.tsx` | Modify | Mount `KpiPerformanceTab`, hide the tab without `Kpi.View`, hide the page period selector on it |
| `frontend/src/pages/Reports.test.tsx` | Create | Tab visibility by permission |
| `frontend/src/modules/reports/components/OverviewTab.tsx` | Modify (optional `canViewKpi` prop) | Hide the "KPI / Performance" shortcut without `Kpi.View` |
| `frontend/src/modules/reports/components/AnalystKpiTab.tsx` | Delete | Replaced |
| `frontend/src/modules/reports/services/AnalystKpiService.ts` | Delete | Replaced |
| `frontend/src/modules/reports/services/kpiDtos.ts` | Delete | Obsolete DTOs |
| `frontend/src/modules/reports/utils/exportKpi.ts` | Delete | Replaced by `exportKpiPdf.ts` |
| `frontend/src/modules/reports/components/WorkloadWeightsDialog.tsx` | Delete if still present | S1 already removes it |
| `frontend/src/modules/reports/types/reportingTypes.ts` | Modify (lines 293 to end) | Drop the analyst-KPI types; keep `OverviewKpiCardData` |
| `frontend/src/modules/reports/services/OverviewService.ts` | Modify (one comment line) | Comment no longer names the deleted tab |
| `backend/MicroLIMS.API/Controllers/KpiController.cs` | Delete | The 14 old endpoints |
| `backend/MicroLIMS.Application/Services/KpiService.cs` | Rewrite | Keep only `GetOverdueAnalystStageSamplesAsync` and its private helpers |
| `backend/MicroLIMS.Application/Services/WorkloadWeightDefaults.cs` | Delete | Legacy seed |
| `backend/MicroLIMS.Persistence/Seed/DbSeeder.cs` (line 265) | Modify | Drop the `WorkloadWeightDefaults.Seed(db)` call |
| `backend/MicroLIMS.Application/Services/DashboardService.cs` (line 156) | Modify (comment) | Names the method that still exists |
| `backend/MicroLIMS.Tests/ArchitectureTests/authorization-matrix.txt` | Modify | Remove the `KpiController.*` lines |
| `backend/MicroLIMS.Tests/WorkflowTests/WorkloadWeightsTests.cs` | Delete | Tests only removed methods |
| `backend/MicroLIMS.Tests/WorkflowTests/KpiFilteringTests.cs` | Rewrite | Keep the one `GetOverdueAnalystStageSamplesAsync` regression test |
| `backend/MicroLIMS.Tests/UnitTests/Kpi/KpiDashboardFilterTests.cs` | Create | Category/location/test-code filters, migrated onto the registry's `wip-backlog` |
| `backend/MicroLIMS.Tests/WorkflowTests/ReturnToAnalystTests.cs` (lines 656-679) | Modify | Retarget the KPI read at the `right-first-time` KPI (Task 12) |
| `backend/MicroLIMS.Tests/WorkflowTests/MultiSectionReviewApprovalTests.cs` (lines 398-417) | Modify | `Kpis_CountOnlyTheViewersSections` rewritten on dashboard + workload services |
| `backend/MicroLIMS.Tests/IntegrationTests/SectionScopedQueriesPostgresIntegrationTests.cs` (lines 51-63) | Modify | Run every registry KPI + workload with real section lists |

---

### Task 1: Formatters, filter helpers and fixtures

**Files:**
- Create: `frontend/src/modules/kpi/utils/kpiFormat.ts`
- Create: `frontend/src/modules/kpi/utils/kpiFilters.ts`
- Create: `frontend/src/modules/kpi/kpiFixtures.ts`
- Test: `frontend/src/modules/kpi/utils/kpiFormat.test.ts`
- Test: `frontend/src/modules/kpi/utils/kpiFilters.test.ts`

**Interfaces:**
- Consumes (S2, `frontend/src/modules/kpi/types/kpiTypes.ts`; camelCase of the backend DTOs, enums as string unions). Check the real file against this block in step 1 and adapt only the fixtures if a field name differs:
```ts
export type KpiDirection = "HigherIsBetter" | "LowerIsBetter" | "Neutral";
export type KpiGroup = "Headline" | "Flow" | "Quality" | "Micro" | "Workload";
export type KpiStatus = "Ok" | "NoData" | "Partial";
export interface KpiPrior { value: number | null; n: number; numerator: number | null; denominator: number | null }
export interface KpiSeriesPoint { label: string; group: string | null; at: string | null; value: number | null; n: number; secondary: number | null }
export interface KpiGap { code: string; message: string; count: number }
export interface KpiResult { value: number | null; numerator: number | null; denominator: number | null; n: number; prior: KpiPrior | null; status: KpiStatus; tooFewRecords: boolean; gaps: KpiGap[]; series: KpiSeriesPoint[]; extras: Record<string, number | null>; flags: string[] }
export interface KpiEntry { key: string; name: string; unit: string; direction: KpiDirection; group: KpiGroup; order: number; result: KpiResult | null; error: string | null }
export interface KpiDashboard { from: string; to: string; priorFrom: string; priorTo: string; generatedAt: string; kpis: KpiEntry[] }
export interface KpiDefinition { key: string; name: string; unit: string; direction: KpiDirection; group: KpiGroup; order: number; definition: string; clockEvents: string; population: string; source: string }
export interface ProfileInForce { workflowType: string; testCode: string | null; workUnits: number | null; targetAnalystDays: number | null; effectiveFrom: string; versionId: number }
export interface KpiDefinitions { kpis: KpiDefinition[]; profilesInForce: ProfileInForce[] }
export interface KpiRecord { sampleId: number | null; testOrderId: number | null; controlNumber: string | null; testCode: string | null; category: string | null; analystName: string | null; eventAt: string | null; value: number | null; unit: string | null; inclusion: "Numerator" | "Denominator" | "Excluded"; note: string | null }
export interface KpiRecordPage { key: string; page: number; pageSize: number; total: number; items: KpiRecord[] }
```
- Consumes (S3, `types/workloadTypes.ts`): `AnalystWorkloadRow`, `AnalystWorkload` exactly as in contract section 4.6.
- Consumes (services, used from Task 5 on): `kpiDashboardService.dashboard(params)`, `.records(key, params)`, `.definitions(from, to)` returning `KpiDashboard`, `KpiRecordPage`, `KpiDefinitions`; `kpiWorkloadService.workload(params)`, `.workloadTests(params)` returning `AnalystWorkload`, `KpiRecordPage`. `params` is `{ from: string; to: string; category?: string; location?: string; testCode?: string; analystId?: number }`, plus `page?: number; pageSize?: number` for the record calls. Call sites in this plan pass object literals, so only the property names matter.
- Produces (`utils/kpiFilters.ts`):
```ts
export interface KpiFiltersValue { from: string; to: string; category: string | null; location: string | null; testCode: string | null }
export function defaultFilters(today?: Date): KpiFiltersValue
export function filtersError(f: { from: string; to: string }): string | null
export function toQueryParams(f: KpiFiltersValue): { from: string; to: string; category?: string; location?: string; testCode?: string }
```
- Produces (`utils/kpiFormat.ts`):
```ts
export const KPI_PERCENT_SCALE: number
export const WORKLOAD_RATIO_SCALE: number
export function scaleKpiValue(unit: string, value: number): number
export function formatKpiValue(unit: string, value: number | null | undefined): string
export function formatRatio(value: number | null | undefined): string
export function formatUnits(value: number | null | undefined): string
export type EntryView =
  | { kind: "error"; message: string }
  | { kind: "noData"; result: KpiResult }
  | { kind: "value"; result: KpiResult; text: string; muted: boolean }
export function entryView(entry: KpiEntry): EntryView
export function formatPrior(unit: string, prior: KpiPrior): string
export function formatExtras(entry: KpiEntry): { label: string; text: string }[]
export const FLAG_LABELS: Record<string, string>
export const DIRECTION_LABEL: Record<KpiDirection, string>
export function sortByName<T extends { fullName: string }>(rows: readonly T[]): T[]
```
- Produces (`kpiFixtures.ts`): `result(over?)`, `entry(over?, resultOver?)` (pass `null` as `resultOver` for a missing result), `headlineEntries()`, `dashboard(kpis)`, `point(label, value, over?)`, `workloadRow(over?)`, `workload(rows, over?)`.

- [ ] **Step 1: Check the preconditions**

Run from `E:\MicroLIMS\MicroLIMS`:
```bash
git status --short | head -60
git diff --cached --name-only   # must print nothing; if it lists files, they are not ours: git restore --staged <path>
git log --oneline -15
ls frontend/src/modules/kpi/types/kpiTypes.ts frontend/src/modules/kpi/types/workloadTypes.ts frontend/src/modules/kpi/services/kpiDashboardService.ts frontend/src/modules/kpi/services/kpiWorkloadService.ts frontend/src/modules/kpi/components/WorkflowProfilesDialog.tsx
ls backend/MicroLIMS.API/Controllers/KpiDashboardController.cs backend/MicroLIMS.API/Controllers/KpiWorkloadController.cs
git grep -n "public static KpiDashboardService KpiDashboard" -- backend/MicroLIMS.Tests/TestServiceFactory.cs
git grep -n "export" -- frontend/src/modules/kpi/types/kpiTypes.ts frontend/src/modules/kpi/types/workloadTypes.ts
```
Expected: every `ls` succeeds, the factory method is found, and the exports match the Consumes block. If a file is missing, STOP and report that S1/S2/S3 is not merged. If a type differs only in naming, note it and adapt the fixtures in step 3 and the call sites in later tasks; do not edit S2/S3 files.

- [ ] **Step 2: Confirm the wire scales**

```bash
git grep -n -E "100m|\* 100|Percent" -- backend/MicroLIMS.Application/Services/Kpi | head -40
git grep -n -E "Utilisation|WeightedOnTime|ForwardLoad" -- backend/MicroLIMS.Application/Services/Kpi/AnalystWorkloadService.cs | head -30
```
Confirm (do not decide) the scales. The S2 plan states that `KpiScope.Rate` returns `Math.Round(numerator/denominator, 4)`, so every `"%"` KPI `Value` is a 0..1 fraction (1.0 = 100%), and the `alertRate` / `actionRate` extras are fractions too (`KpiConstants.EmAlertRateAdverse = 0.025m`). Workload ratios (`Utilisation`, `ForwardLoad`, `WeightedOnTime`, `WeightedRightFirstTime`) are fractions as well. So step 6 uses `KPI_PERCENT_SCALE = 100` and `WORKLOAD_RATIO_SCALE = 100`, and every fixture and test below feeds fractions (0.875 prints "87.5%"). Read `RightFirstTimeKpi`, `OnTimeAllStagesKpi` and `AnalystWorkloadService` once to confirm. Only if the merged backend really returns 0..100 numbers, change the constants to 1 and rewrite the fractions in the tests accordingly (this is the "percent shown 100x too small" risk in Review Focus). The step 4 tests pin the constants so a later backend change cannot drift unnoticed.

- [ ] **Step 3: Write the fixtures**

Create `frontend/src/modules/kpi/kpiFixtures.ts`:
```ts
import type { KpiDashboard, KpiEntry, KpiResult, KpiSeriesPoint } from "./types/kpiTypes";
import type { AnalystWorkload, AnalystWorkloadRow } from "./types/workloadTypes";

// Test-only builders. Defaults describe a healthy "Sample TAT" tile.
export function result(over: Partial<KpiResult> = {}): KpiResult {
  return {
    value: 4.2, numerator: null, denominator: null, n: 31, prior: null, status: "Ok",
    tooFewRecords: false, gaps: [], series: [], extras: {}, flags: [], ...over
  };
}

// Pass null as resultOver for an entry that came back without a result.
export function entry(over: Partial<KpiEntry> = {}, resultOver: Partial<KpiResult> | null = {}): KpiEntry {
  return {
    key: "sample-tat", name: "Sample TAT", unit: "days", direction: "LowerIsBetter", group: "Headline", order: 1,
    result: resultOver === null ? null : result(resultOver), error: null, ...over
  };
}

const HEADLINE: [string, string, string][] = [
  ["sample-tat", "Sample TAT", "days"],
  ["on-time-all-stages", "On time, all stages", "%"],
  ["right-first-time", "Right first time", "%"],
  ["retest-rate", "Retest rate", "%"],
  ["oos-rate", "OOS rate", "%"],
  ["wip-backlog", "WIP backlog", "tests"],
  ["incubation-reads-on-time", "Incubation reads on time", "%"],
  ["em-excursion-rate", "EM excursion rate", "%"]
];

export function headlineEntries(): KpiEntry[] {
  return HEADLINE.map(([key, name, unit], i) => entry({ key, name, unit, order: i + 1 }, { value: i + 1, n: 10 + i }));
}

export function dashboard(kpis: KpiEntry[]): KpiDashboard {
  return {
    from: "2026-09-12T00:00:00Z", to: "2026-10-12T00:00:00Z",
    priorFrom: "2026-08-13T00:00:00Z", priorTo: "2026-09-12T00:00:00Z",
    generatedAt: "2026-10-11T09:30:00Z", kpis
  };
}

export function point(label: string, value: number | null, over: Partial<KpiSeriesPoint> = {}): KpiSeriesPoint {
  return { label, group: null, at: null, value, n: 10, secondary: null, ...over };
}

export function workloadRow(over: Partial<AnalystWorkloadRow> = {}): AnalystWorkloadRow {
  return {
    userId: 1, fullName: "Amy Analyst", capacityUnits: 30, deliveredUnits: 24, utilisation: 0.8,
    utilisationCheckProfile: false, openUnits: 10, forwardCapacityUnits: 30, forwardLoad: 0.33,
    forwardLoadOverCapacity: false, weightedOnTime: 0.9, weightedRightFirstTime: 0.95,
    deliveredTests: 12, unweightedTests: 0, unweightedOpenTests: 0, testsOnDefaultTarget: 0, ...over
  };
}

export function workload(rows: AnalystWorkloadRow[], over: Partial<AnalystWorkload> = {}): AnalystWorkload {
  return {
    from: "2026-09-12T00:00:00Z", to: "2026-10-12T00:00:00Z", capacityUsesDefaults: false,
    onlyOwnRow: false, unassignedOpenTests: 0, rows, gaps: [], ...over
  };
}
```

- [ ] **Step 4: Write the failing tests**

Create `frontend/src/modules/kpi/utils/kpiFormat.test.ts`:
```ts
import { describe, it, expect } from "vitest";
import { entry } from "../kpiFixtures";
import {
  entryView, formatExtras, formatKpiValue, formatPrior, formatRatio, formatUnits, sortByName
} from "./kpiFormat";

describe("formatKpiValue", () => {
  it("formats each unit", () => {
    expect(formatKpiValue("days", 4.2)).toBe("4.2 days");
    expect(formatKpiValue("%", 0.875)).toBe("87.5%");
    expect(formatKpiValue("tests", 12)).toBe("12 tests");
    expect(formatKpiValue("units", 6)).toBe("6.0 units");
  });

  it("keeps a real zero", () => {
    expect(formatKpiValue("%", 0)).toBe("0.0%");
    expect(formatKpiValue("tests", 0)).toBe("0 tests");
  });

  it("shows a dash only for a missing value", () => {
    expect(formatKpiValue("days", null)).toBe("—");
    expect(formatKpiValue("days", undefined)).toBe("—");
  });
});

describe("wire scales", () => {
  // These two pin the scales confirmed against the backend in Task 1 step 2.
  it("treats KPI percent values as fractions (percent shown 100x too small guard)", () => {
    expect(formatKpiValue("%", 0.5)).toBe("50.0%");
    expect(formatKpiValue("%", 0.875)).toBe("87.5%");
    expect(formatKpiValue("%", 1)).toBe("100.0%");
  });
  it("treats workload ratios as fractions", () => {
    expect(formatRatio(0.5)).toBe("50%");
    expect(formatRatio(1.2)).toBe("120%");
    expect(formatRatio(null)).toBe("—");
    expect(formatRatio(0)).toBe("0%");
  });
});

describe("formatUnits", () => {
  it("shows one decimal and a dash for null", () => {
    expect(formatUnits(24)).toBe("24.0");
    expect(formatUnits(null)).toBe("—");
  });
});

describe("entryView", () => {
  it("reports a failed entry", () => {
    expect(entryView(entry({ error: "timed out" }, null))).toEqual({ kind: "error", message: "timed out" });
  });

  it("treats a missing result with no error as an error, not as zero", () => {
    expect(entryView(entry({}, null)).kind).toBe("error");
  });

  it("reports NoData and a Partial entry with no value as no data", () => {
    expect(entryView(entry({}, { status: "NoData", value: null, n: 0 })).kind).toBe("noData");
    expect(entryView(entry({}, { status: "Partial", value: null })).kind).toBe("noData");
  });

  it("treats a null value with a populated series as data, not as no data", () => {
    const view = entryView(entry({ key: "stage-tat" }, { value: null, status: "Ok", series: [point("Testing", 2.1, { secondary: 4, n: 12 })] }));
    expect(view.kind).toBe("value");
  });

  it("keeps a zero as a value", () => {
    const view = entryView(entry({ unit: "%" }, { value: 0, n: 40 }));
    expect(view.kind).toBe("value");
    if (view.kind === "value") expect(view.text).toBe("0.0%");
  });

  it("mutes a value that has too few records", () => {
    const view = entryView(entry({ unit: "%" }, { value: 0.5, n: 3, tooFewRecords: true }));
    expect(view.kind === "value" && view.muted).toBe(true);
  });
});

describe("formatPrior and formatExtras", () => {
  it("states the prior value and its own n", () => {
    expect(formatPrior("days", { value: 5, n: 20, numerator: null, denominator: null })).toBe("Prior period: 5.0 days (n=20)");
  });

  it("formats known extras only, percent extras as percent", () => {
    const e = entry({ unit: "days" }, { extras: { p90: 12, alertRate: 0.018, actionRate: null, mystery: 9 } });
    expect(formatExtras(e)).toEqual([
      { label: "P90", text: "12.0 days" },
      { label: "Alert rate", text: "1.8%" },
      { label: "Action rate", text: "—" }
    ]);
  });
});

describe("sortByName", () => {
  it("sorts case-insensitively and does not mutate the input", () => {
    const rows = [{ fullName: "zed" }, { fullName: "Amy" }, { fullName: "bob" }];
    expect(sortByName(rows).map((r) => r.fullName)).toEqual(["Amy", "bob", "zed"]);
    expect(rows[0].fullName).toBe("zed");
  });
});
```

Create `frontend/src/modules/kpi/utils/kpiFilters.test.ts`:
```ts
import { describe, it, expect } from "vitest";
import { defaultFilters, filtersError, toQueryParams } from "./kpiFilters";

describe("defaultFilters", () => {
  it("covers the last 30 days including today", () => {
    expect(defaultFilters(new Date(2026, 9, 11))).toEqual({
      from: "2026-09-12", to: "2026-10-11", category: null, location: null, testCode: null
    });
  });
});

describe("filtersError", () => {
  it("accepts a valid and a single-day range", () => {
    expect(filtersError({ from: "2026-09-01", to: "2026-10-01" })).toBeNull();
    expect(filtersError({ from: "2026-10-01", to: "2026-10-01" })).toBeNull();
  });
  it("rejects empty and inverted ranges", () => {
    expect(filtersError({ from: "", to: "2026-10-01" })).toBe("Choose both dates.");
    expect(filtersError({ from: "2026-10-02", to: "2026-10-01" })).toBe("The start date must not be after the end date.");
  });
});

describe("toQueryParams", () => {
  it("leaves unset filters out instead of sending empty strings", () => {
    const q = toQueryParams({ from: "2026-09-01", to: "2026-10-01", category: null, location: null, testCode: "TAMC" });
    expect(q).toEqual({ from: "2026-09-01", to: "2026-10-01", category: undefined, location: undefined, testCode: "TAMC" });
  });
});
```

- [ ] **Step 5: Run to see them fail**

```bash
cd frontend && npx vitest run src/modules/kpi/utils
```
Expected: both files FAIL with "Failed to resolve import ./kpiFormat" / "./kpiFilters".

- [ ] **Step 6: Implement**

Create `frontend/src/modules/kpi/utils/kpiFilters.ts`:
```ts
import { toDateInputValue } from "../../reports/utils/dateRange";

export interface KpiFiltersValue {
  from: string; // yyyy-MM-dd, inclusive
  to: string;   // yyyy-MM-dd, inclusive
  category: string | null;
  location: string | null;
  testCode: string | null;
}

// The last 30 days including today.
export function defaultFilters(today: Date = new Date()): KpiFiltersValue {
  const from = new Date(today);
  from.setDate(today.getDate() - 29);
  return { from: toDateInputValue(from), to: toDateInputValue(today), category: null, location: null, testCode: null };
}

// <input type="date"> reports "" while a date is half typed, and ISO dates compare correctly as strings.
export function filtersError(f: { from: string; to: string }): string | null {
  if (!f.from || !f.to) return "Choose both dates.";
  if (f.from > f.to) return "The start date must not be after the end date.";
  return null;
}

// Unset filters become undefined so axios leaves them out of the query string.
export function toQueryParams(f: KpiFiltersValue) {
  return {
    from: f.from,
    to: f.to,
    category: f.category ?? undefined,
    location: f.location ?? undefined,
    testCode: f.testCode ?? undefined
  };
}
```

Create `frontend/src/modules/kpi/utils/kpiFormat.ts`:
```ts
import type { KpiDirection, KpiEntry, KpiPrior, KpiResult } from "../types/kpiTypes";

// Wire scales, confirmed against the backend in the S4 plan (Task 1 step 2).
// KPI values whose unit is "%" are scaled by KPI_PERCENT_SCALE to reach a
// 0-100 number (the backend sends 4-decimal fractions: 0.875 = 87.5%); workload
// ratios (utilisation, forward load, weighted rates) by WORKLOAD_RATIO_SCALE.
// If a backend slice changes either, change it here and nowhere else.
export const KPI_PERCENT_SCALE = 100;
export const WORKLOAD_RATIO_SCALE = 100;

const NONE = "—";

export function scaleKpiValue(unit: string, value: number): number {
  return unit === "%" ? value * KPI_PERCENT_SCALE : value;
}

// null/undefined is the only "no value"; 0 is a real value and must print.
export function formatKpiValue(unit: string, value: number | null | undefined): string {
  if (value == null) return NONE;
  const v = scaleKpiValue(unit, value);
  switch (unit) {
    case "%": return `${v.toFixed(1)}%`;
    case "days": return `${v.toFixed(1)} days`;
    case "tests": return `${Math.round(v)} tests`;
    default: return `${v.toFixed(1)} ${unit}`;
  }
}

export function formatRatio(value: number | null | undefined): string {
  return value == null ? NONE : `${(value * WORKLOAD_RATIO_SCALE).toFixed(0)}%`;
}

export function formatUnits(value: number | null | undefined): string {
  return value == null ? NONE : value.toFixed(1);
}

export type EntryView =
  | { kind: "error"; message: string }
  | { kind: "noData"; result: KpiResult }
  | { kind: "value"; result: KpiResult; text: string; muted: boolean };

// The one decision about what an entry looks like. Tiles, panels and the PDF
// all read it, so screen and print cannot disagree.
export function entryView(entry: KpiEntry): EntryView {
  if (entry.error != null) return { kind: "error", message: entry.error };
  const r = entry.result;
  if (!r) return { kind: "error", message: "No result was returned for this measure." };
  // The series-only supporting measures (stage-tat, backlog-aging, returns-by-test) may carry
  // value = null with status Ok and a populated series: that is data, not "no data".
  // Only NoData, or no value and no series, is empty.
  if (r.status === "NoData" || (r.value == null && r.series.length === 0)) return { kind: "noData", result: r };
  return { kind: "value", result: r, text: r.value == null ? "" : formatKpiValue(entry.unit, r.value), muted: r.tooFewRecords };
}

export function formatPrior(unit: string, prior: KpiPrior): string {
  return `Prior period: ${formatKpiValue(unit, prior.value)} (n=${prior.n})`;
}

const EXTRAS: { key: string; label: string; percent: boolean }[] = [
  { key: "p90", label: "P90", percent: false },
  { key: "alertRate", label: "Alert rate", percent: true },
  { key: "actionRate", label: "Action rate", percent: true }
];

// Only the extras the registry is known to send; an unknown key is ignored
// rather than printed unformatted.
export function formatExtras(entry: KpiEntry): { label: string; text: string }[] {
  const extras = entry.result?.extras ?? {};
  return EXTRAS.filter((x) => x.key in extras).map((x) => ({
    label: x.label,
    text: formatKpiValue(x.percent ? "%" : entry.unit, extras[x.key])
  }));
}

export const FLAG_LABELS: Record<string, string> = { AdverseTrend: "Adverse trend" };

export const DIRECTION_LABEL: Record<KpiDirection, string> = {
  HigherIsBetter: "Higher is better",
  LowerIsBetter: "Lower is better",
  Neutral: "No better or worse direction"
};

// Alphabetical, case-insensitive: the default order of every people list.
export function sortByName<T extends { fullName: string }>(rows: readonly T[]): T[] {
  return [...rows].sort((a, b) => a.fullName.localeCompare(b.fullName, undefined, { sensitivity: "base" }));
}
```

- [ ] **Step 7: Run to see them pass**

```bash
cd frontend && npx vitest run src/modules/kpi/utils && npx tsc --noEmit -p tsconfig.json
```
Expected: 2 files, all tests PASS; `tsc` prints nothing (if it reports a type mismatch in `kpiFixtures.ts`, fix the fixture to the real S2/S3 type).

- [ ] **Step 8: Commit**

```bash
cd /e/MicroLIMS/MicroLIMS && git add frontend/src/modules/kpi/utils/kpiFormat.ts frontend/src/modules/kpi/utils/kpiFormat.test.ts frontend/src/modules/kpi/utils/kpiFilters.ts frontend/src/modules/kpi/utils/kpiFilters.test.ts frontend/src/modules/kpi/kpiFixtures.ts
git commit -F - <<'EOF'
feat(kpi): formatters, filter helpers and fixtures for the new KPI page

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
EOF
```

---

### Task 2: KpiTile

**Files:**
- Create: `frontend/src/modules/kpi/components/KpiTile.tsx`
- Test: `frontend/src/modules/kpi/components/KpiTile.test.tsx`

**Interfaces:**
- Consumes: `entryView`, `formatExtras`, `formatPrior`, `FLAG_LABELS` (Task 1); `KpiEntry`, `KpiDefinition`, `KpiResult` (S2); `entry` fixture (Task 1); `theme.custom.status.{detected,inconclusive}` tokens (`{ bg, text, border }`).
- Produces:
```tsx
export function KpiChips(props: { result: KpiResult }): JSX.Element | null   // gap + flag chips, amber tone
export function KpiTile(props: { entry: KpiEntry; definition?: KpiDefinition; onOpenRecords(key: string): void }): JSX.Element
```
`KpiTile` renders: error tile (no button) when `entry.error`; otherwise one button whose accessible name is `"<name>: <value or No data>. Show records"`.

- [ ] **Step 1: Write the failing test**

Create `frontend/src/modules/kpi/components/KpiTile.test.tsx`:
```tsx
import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { entry } from "../kpiFixtures";
import type { KpiDefinition, KpiEntry } from "../types/kpiTypes";
import { KpiTile } from "./KpiTile";

afterEach(cleanup);

function renderTile(e: KpiEntry, over: { definition?: KpiDefinition; onOpenRecords?: (key: string) => void } = {}) {
  const onOpenRecords = over.onOpenRecords ?? vi.fn();
  render(
    <PinnedLightTheme>
      <KpiTile entry={e} definition={over.definition} onOpenRecords={onOpenRecords} />
    </PinnedLightTheme>
  );
  return onOpenRecords;
}

describe("KpiTile", () => {
  it("shows the value and n for an Ok result and no prior line when there is no prior", () => {
    renderTile(entry());
    expect(screen.getByText("4.2 days")).toBeTruthy();
    expect(screen.getByText("n = 31")).toBeTruthy();
    expect(screen.queryByText(/Prior period/)).toBeNull();
  });

  it("shows the prior value with its own n only when a prior exists", () => {
    renderTile(entry({}, { prior: { value: 5, n: 20, numerator: null, denominator: null } }));
    expect(screen.getByText("Prior period: 5.0 days (n=20)")).toBeTruthy();
  });

  it("shows No data and no number for NoData", () => {
    renderTile(entry({ unit: "%" }, { status: "NoData", value: null, n: 0 }));
    expect(screen.getByText("No data")).toBeTruthy();
    expect(screen.queryByText(/%/)).toBeNull();
    expect(screen.queryByText(/^0/)).toBeNull();
  });

  it("shows a real zero as a value, not as no data", () => {
    renderTile(entry({ key: "em-excursion-rate", name: "EM excursion rate", unit: "%" }, { value: 0, n: 40 }));
    expect(screen.getByText("0.0%")).toBeTruthy();
    expect(screen.queryByText("No data")).toBeNull();
  });

  it("shows a fractional percent value as a percentage, not 100x too small", () => {
    renderTile(entry({ key: "right-first-time", name: "Right first time", unit: "%" }, { value: 0.875, n: 40 }));
    expect(screen.getByText("87.5%")).toBeTruthy();
  });

  it("shows gap chips for a Partial result", () => {
    renderTile(entry({}, { status: "Partial", gaps: [{ code: "unweighted", message: "12 tests unweighted", count: 12 }] }));
    expect(screen.getByText("12 tests unweighted")).toBeTruthy();
  });

  it("labels the adverse-trend flag", () => {
    renderTile(entry({ unit: "%" }, { flags: ["AdverseTrend"] }));
    expect(screen.getByText("Adverse trend")).toBeTruthy();
  });

  it("mutes a rate with too few records and says why", () => {
    renderTile(entry({ unit: "%" }, { value: 0.5, n: 3, tooFewRecords: true }));
    expect(screen.getByText("too few records to judge")).toBeTruthy();
    expect(screen.getByText("50.0%").getAttribute("data-muted")).toBe("true");
  });

  it("does not mute a normal value", () => {
    renderTile(entry());
    expect(screen.getByText("4.2 days").getAttribute("data-muted")).toBe("false");
  });

  it("shows known extras such as P90", () => {
    renderTile(entry({}, { extras: { p90: 12 } }));
    expect(screen.getByText("P90 12.0 days")).toBeTruthy();
  });

  it("renders a failed KPI as an error tile that cannot open records", () => {
    renderTile(entry({ error: "Query timed out" }, null));
    expect(screen.getByText("Could not be calculated")).toBeTruthy();
    expect(screen.getByText("Query timed out")).toBeTruthy();
    expect(screen.queryByRole("button")).toBeNull();
  });

  it("opens the record list for its key when clicked", async () => {
    const onOpen = renderTile(entry());
    await userEvent.click(screen.getByRole("button", { name: /^Sample TAT: 4\.2 days/ }));
    expect(onOpen).toHaveBeenCalledWith("sample-tat");
  });

  it("shows the registry definition on hover", async () => {
    const definition: KpiDefinition = {
      key: "sample-tat", name: "Sample TAT", unit: "days", direction: "LowerIsBetter", group: "Headline", order: 1,
      definition: "Days from receipt to approval.", clockEvents: "", population: "", source: ""
    };
    renderTile(entry(), { definition });
    await userEvent.hover(screen.getByRole("button", { name: /^Sample TAT/ }));
    expect(await screen.findByText("Days from receipt to approval.")).toBeTruthy();
  });
});
```

- [ ] **Step 2: Run to see it fail**

```bash
cd frontend && npx vitest run src/modules/kpi/components/KpiTile.test.tsx
```
Expected: FAIL "Failed to resolve import ./KpiTile".

- [ ] **Step 3: Implement**

Create `frontend/src/modules/kpi/components/KpiTile.tsx`:
```tsx
import type { ReactNode } from "react";
import { ButtonBase, Chip, Paper, Stack, Tooltip, Typography, useTheme } from "@mui/material";
import type { SxProps, Theme } from "@mui/material";
import type { KpiDefinition, KpiEntry, KpiResult } from "../types/kpiTypes";
import { FLAG_LABELS, entryView, formatExtras, formatPrior } from "../utils/kpiFormat";

// Gap messages and flags from the server, in the one non-failure warning tone.
export function KpiChips({ result }: { result: KpiResult }) {
  const theme = useTheme();
  if (result.gaps.length === 0 && result.flags.length === 0) return null;
  const tone = theme.custom.status.inconclusive;
  const sx = { bgcolor: tone.bg, color: tone.text, border: `1px solid ${tone.border}`, fontSize: 12 };
  return (
    <Stack direction="row" useFlexGap sx={{ flexWrap: "wrap", gap: 0.5 }}>
      {result.gaps.map((g) => <Chip key={g.code} size="small" label={g.message} sx={sx} />)}
      {result.flags.map((f) => <Chip key={f} size="small" label={FLAG_LABELS[f] ?? f} sx={sx} />)}
    </Stack>
  );
}

// Typography as a block-level span: the text sits inside a <button>, where <p> is not allowed.
function Text({ children, sx, ...rest }: { children: ReactNode; sx?: SxProps<Theme>; "data-muted"?: string }) {
  return (
    <Typography component="span" {...rest} sx={[{ display: "block" }, ...(Array.isArray(sx) ? sx : [sx])]}>
      {children}
    </Typography>
  );
}

interface KpiTileProps {
  entry: KpiEntry;
  definition?: KpiDefinition;
  onOpenRecords(key: string): void;
}

export function KpiTile({ entry, definition, onOpenRecords }: KpiTileProps) {
  const theme = useTheme();
  const view = entryView(entry);

  if (view.kind === "error") {
    const tone = theme.custom.status.detected;
    return (
      <Paper sx={{ p: 2, height: "100%", border: `1px solid ${tone.border}` }}>
        <Typography sx={{ fontSize: 12.5, fontWeight: 600, color: "text.secondary" }}>{entry.name}</Typography>
        <Typography role="alert" sx={{ fontSize: 16, fontWeight: 700, color: tone.text, my: 0.5 }}>
          Could not be calculated
        </Typography>
        <Typography sx={{ fontSize: 12, color: "text.secondary" }}>{view.message}</Typography>
      </Paper>
    );
  }

  const { result } = view;
  const label = view.kind === "noData" ? "No data" : view.text;
  const extras = formatExtras(entry);

  const button = (
    <ButtonBase
      onClick={() => onOpenRecords(entry.key)}
      aria-label={`${entry.name}: ${label}. Show records`}
      sx={{
        display: "flex", flexDirection: "column", alignItems: "flex-start", gap: 0.5,
        width: "100%", height: "100%", p: 2, textAlign: "left", borderRadius: "inherit"
      }}
    >
      <Text sx={{ fontSize: 12.5, fontWeight: 600, color: "text.secondary" }}>{entry.name}</Text>
      {view.kind === "noData" ? (
        <Text sx={{ fontSize: 20, fontWeight: 700, color: "text.secondary" }}>No data</Text>
      ) : (
        <Text
          data-muted={view.muted ? "true" : "false"}
          sx={{ fontSize: 24, fontWeight: 800, color: view.muted ? "text.disabled" : theme.palette.primary.main }}
        >
          {view.text}
        </Text>
      )}
      {view.kind === "value" && view.muted && (
        <Text sx={{ fontSize: 12, color: "text.secondary" }}>too few records to judge</Text>
      )}
      {extras.map((x) => (
        <Text key={x.label} sx={{ fontSize: 12, color: "text.secondary" }}>{x.label} {x.text}</Text>
      ))}
      <Text sx={{ fontSize: 12, color: "text.secondary" }}>n = {result.n}</Text>
      {result.prior != null && (
        <Text sx={{ fontSize: 12, color: "text.secondary" }}>{formatPrior(entry.unit, result.prior)}</Text>
      )}
      <KpiChips result={result} />
    </ButtonBase>
  );

  return (
    <Paper sx={{ height: "100%" }}>
      {definition ? <Tooltip title={definition.definition} arrow describeChild>{button}</Tooltip> : button}
    </Paper>
  );
}
```

- [ ] **Step 4: Run to see it pass**

```bash
cd frontend && npx vitest run src/modules/kpi/components/KpiTile.test.tsx && npx tsc --noEmit -p tsconfig.json
```
Expected: 12 tests PASS; `tsc` clean. If the hover test is flaky because the tooltip delay exceeds `findByText`'s 1 s default, pass `{ timeout: 2000 }` to `findByText`; do not remove the test.

- [ ] **Step 5: Commit**

```bash
cd /e/MicroLIMS/MicroLIMS && git add frontend/src/modules/kpi/components/KpiTile.tsx frontend/src/modules/kpi/components/KpiTile.test.tsx
git commit -F - <<'EOF'
feat(kpi): KpiTile with ok, no-data, partial, error and too-few-records states

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
EOF
```

---

### Task 3: KpiSection

**Files:**
- Create: `frontend/src/modules/kpi/components/KpiSection.tsx`
- Test: `frontend/src/modules/kpi/components/KpiSection.test.tsx`

**Interfaces:**
- Consumes: `KpiChips` (Task 2); `entryView`, `formatExtras`, `formatKpiValue`, `scaleKpiValue` (Task 1); `KpiEntry`, `KpiSeriesPoint`; fixtures `entry`, `point`.
- Produces:
```tsx
export function KpiSection(props: { title: string; entries: KpiEntry[]; onOpenRecords(key: string): void }): JSX.Element | null
```
Returns `null` for an empty `entries`. One panel per entry (`<h3>` = entry name, a "Records" button named `Show records for <name>`). The series is drawn by key: `stage-tat` as bars with text `median X · P90 Y · n = N` (`value` = median, `secondary` = P90); `backlog-aging` as a table of stages (`group`) by buckets (`label` `within50`, `50to100`, `over100`); `returns-by-test` as a list by test code; any other entry as a trend line (needs two points, else "Not enough points for a trend yet.").

- [ ] **Step 1: Write the failing test**

Create `frontend/src/modules/kpi/components/KpiSection.test.tsx`:
```tsx
import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { entry, point } from "../kpiFixtures";
import type { KpiEntry } from "../types/kpiTypes";
import { KpiSection } from "./KpiSection";

// Recharts measures 0x0 in jsdom; the tests assert on text, never on the SVG.
// LineChart records its data prop so a test can prove the (scaled) points reach the chart.
const chart = vi.hoisted(() => ({ data: null as { label: string; value: number | null }[] | null }));
vi.mock("recharts", async () => {
  const { createElement, Fragment } = await import("react");
  const Pass = ({ children }: { children?: unknown }) => createElement(Fragment, null, children as never);
  const Nil = () => null;
  const LineChart = ({ data }: { data: { label: string; value: number | null }[] }) => {
    chart.data = data;
    return null;
  };
  return { ResponsiveContainer: Pass, LineChart, Line: Nil, XAxis: Nil, YAxis: Nil, Tooltip: Nil, CartesianGrid: Nil };
});

afterEach(cleanup);

function renderSection(entries: KpiEntry[], onOpenRecords: (key: string) => void = vi.fn()) {
  render(
    <PinnedLightTheme>
      <KpiSection title="Flow" entries={entries} onOpenRecords={onOpenRecords} />
    </PinnedLightTheme>
  );
  return onOpenRecords;
}

describe("KpiSection", () => {
  it("renders nothing for an empty list", () => {
    const { container } = render(
      <PinnedLightTheme><KpiSection title="Flow" entries={[]} onOpenRecords={() => {}} /></PinnedLightTheme>
    );
    expect(container.textContent).toBe("");
  });

  it("shows stage bars with median, P90 and n", () => {
    renderSection([
      entry({ key: "stage-tat", name: "Stage turnaround", group: "Flow", order: 20 }, {
        series: [point("Testing", 2.1, { secondary: 4, n: 12 }), point("Review", 0.5, { secondary: 1, n: 12 })]
      })
    ]);
    expect(screen.getByRole("heading", { name: "Flow" })).toBeTruthy();
    expect(screen.getByRole("heading", { name: "Stage turnaround" })).toBeTruthy();
    expect(screen.getByText("median 2.1 days · P90 4.0 days · n = 12")).toBeTruthy();
    expect(screen.getByText("Review")).toBeTruthy();
  });

  it("shows backlog aging by stage and bucket, and a dash where a bucket is missing", () => {
    renderSection([
      entry({ key: "backlog-aging", name: "Backlog aging", unit: "tests", group: "Flow", order: 21 }, {
        series: [
          point("within50", 5, { group: "Testing" }),
          point("over100", 2, { group: "Testing" })
        ]
      })
    ]);
    expect(screen.getByText("Testing")).toBeTruthy();
    expect(screen.getByText("5")).toBeTruthy();
    expect(screen.getByText("2")).toBeTruthy();
    expect(screen.getAllByText("—").length).toBe(1);
  });

  it("lists returns by test code", () => {
    renderSection([
      entry({ key: "returns-by-test", name: "Returns by test", unit: "tests", group: "Quality", order: 30 }, {
        series: [point("TAMC", 3)]
      })
    ]);
    expect(screen.getByText("TAMC")).toBeTruthy();
    expect(screen.getByText("3")).toBeTruthy();
  });

  it("asks for more points before drawing a trend", () => {
    renderSection([
      entry({ key: "retest-rate", name: "Retest rate", unit: "%", group: "Headline", order: 4 }, { series: [point("2026-09", 4)] })
    ]);
    expect(screen.getByText("Not enough points for a trend yet.")).toBeTruthy();
  });

  it("feeds a two-point percent series to the chart as scaled percentages", () => {
    chart.data = null;
    renderSection([
      entry({ key: "retest-rate", name: "Retest rate", unit: "%", group: "Headline", order: 4 }, {
        series: [point("2026-08", 0.04), point("2026-09", 0.05)]
      })
    ]);
    expect(chart.data).toEqual([{ label: "2026-08", value: 4 }, { label: "2026-09", value: 5 }]);
  });

  it("shows a count panel with a genuine zero and no series as a value, not as a broken panel", () => {
    renderSection([
      entry({ key: "returns-by-test", name: "Returns by test", unit: "tests", group: "Quality", order: 30 }, {
        value: 0, n: 0, status: "Ok", series: []
      })
    ]);
    expect(screen.getByText("0 tests")).toBeTruthy();
    expect(screen.getByText("None in this period")).toBeTruthy();
    expect(screen.queryByText(/No data/)).toBeNull();
  });

  it("shows alert and action rates and the adverse-trend flag for the EM entry", () => {
    renderSection([
      entry({ key: "em-excursion-rate", name: "EM excursion rate", unit: "%" }, {
        extras: { alertRate: 0.018, actionRate: 0.001 }, flags: ["AdverseTrend"],
        series: [point("2026-08", 1), point("2026-09", 2)]
      })
    ]);
    expect(screen.getByText("Alert rate 1.8%")).toBeTruthy();
    expect(screen.getByText("Action rate 0.1%")).toBeTruthy();
    expect(screen.getByText("Adverse trend")).toBeTruthy();
  });

  it("shows a failed entry as an error and a NoData entry as no data, without a zero", () => {
    renderSection([
      entry({ key: "stage-tat", name: "Stage turnaround", error: "boom" }, null),
      entry({ key: "backlog-aging", name: "Backlog aging", unit: "tests" }, { status: "NoData", value: null, n: 0 })
    ]);
    expect(screen.getByText("Could not be calculated: boom")).toBeTruthy();
    expect(screen.getByText("No data in this period")).toBeTruthy();
    expect(screen.queryByText("0")).toBeNull();
  });

  it("shows a series-only panel whose value is null", () => {
    renderSection([
      entry({ key: "stage-tat", name: "Stage turnaround" }, { value: null, status: "Ok", series: [point("Testing", 2.1, { secondary: 4, n: 12 })] })
    ]);
    expect(screen.getByText("Testing")).toBeTruthy();
    expect(screen.queryByText("No data in this period")).toBeNull();
  });

  it("notes too few records on a rate", () => {
    renderSection([
      entry({ key: "oos-rate", name: "OOS rate", unit: "%" }, { value: 0.2, n: 2, tooFewRecords: true, series: [] })
    ]);
    expect(screen.getByText("too few records to judge")).toBeTruthy();
  });

  it("opens the records for a panel's key", async () => {
    const onOpen = renderSection([entry({ key: "stage-tat", name: "Stage turnaround" })]);
    await userEvent.click(screen.getByRole("button", { name: "Show records for Stage turnaround" }));
    expect(onOpen).toHaveBeenCalledWith("stage-tat");
  });

  it("offers no records button for a failed entry", () => {
    renderSection([entry({ error: "boom" }, null)]);
    expect(screen.queryByRole("button")).toBeNull();
  });
});
```

- [ ] **Step 2: Run to see it fail**

```bash
cd frontend && npx vitest run src/modules/kpi/components/KpiSection.test.tsx
```
Expected: FAIL "Failed to resolve import ./KpiSection".

- [ ] **Step 3: Implement**

Create `frontend/src/modules/kpi/components/KpiSection.tsx`:
```tsx
import {
  Box, Button, Grid, Paper, Stack, Table, TableBody, TableCell, TableHead, TableRow, Typography, useTheme
} from "@mui/material";
import {
  CartesianGrid, Line, LineChart, ResponsiveContainer, Tooltip as RechartsTooltip, XAxis, YAxis
} from "recharts";
import type { KpiEntry, KpiSeriesPoint } from "../types/kpiTypes";
import { entryView, formatExtras, formatKpiValue, scaleKpiValue } from "../utils/kpiFormat";
import { KpiChips } from "./KpiTile";

const secondary = { fontSize: 12, color: "text.secondary" } as const;

// Median and P90 per stage; the server sends them in stage order.
function StageBars({ points, unit }: { points: KpiSeriesPoint[]; unit: string }) {
  const theme = useTheme();
  const max = Math.max(...points.map((p) => p.secondary ?? p.value ?? 0), 0.0001);
  return (
    <Stack spacing={1.25}>
      {points.map((p) => (
        <Box key={p.label}>
          <Typography sx={{ fontSize: 12.5, fontWeight: 600 }}>{p.label}</Typography>
          <Box sx={{ position: "relative", height: 10, borderRadius: 1, bgcolor: "action.hover" }}>
            <Box sx={{ position: "absolute", inset: 0, width: `${((p.secondary ?? 0) / max) * 100}%`, borderRadius: 1, bgcolor: theme.custom.chartSequential[1] }} />
            <Box sx={{ position: "absolute", inset: 0, width: `${((p.value ?? 0) / max) * 100}%`, borderRadius: 1, bgcolor: theme.custom.chartSequential[3] }} />
          </Box>
          <Typography sx={secondary}>
            median {formatKpiValue(unit, p.value)} · P90 {formatKpiValue(unit, p.secondary)} · n = {p.n}
          </Typography>
        </Box>
      ))}
    </Stack>
  );
}

const BUCKETS = [
  { label: "within50", header: "Within 50% of target" },
  { label: "50to100", header: "50-100% of target" },
  { label: "over100", header: "Over target" }
];

// A missing bucket prints a dash: the page does not invent a zero count.
function AgingTable({ points }: { points: KpiSeriesPoint[] }) {
  const stages = Array.from(new Set(points.map((p) => p.group ?? "All")));
  const cell = (stage: string, bucket: string) => {
    const v = points.find((p) => (p.group ?? "All") === stage && p.label === bucket)?.value;
    return v == null ? "—" : Math.round(v);
  };
  return (
    <Table size="small">
      <TableHead>
        <TableRow>
          <TableCell>Stage</TableCell>
          {BUCKETS.map((b) => <TableCell key={b.label} align="right">{b.header}</TableCell>)}
        </TableRow>
      </TableHead>
      <TableBody>
        {stages.map((stage) => (
          <TableRow key={stage}>
            <TableCell>{stage}</TableCell>
            {BUCKETS.map((b) => <TableCell key={b.label} align="right">{cell(stage, b.label)}</TableCell>)}
          </TableRow>
        ))}
      </TableBody>
    </Table>
  );
}

// By test code, never by person.
function ReturnsList({ points }: { points: KpiSeriesPoint[] }) {
  return (
    <Table size="small">
      <TableHead>
        <TableRow><TableCell>Test</TableCell><TableCell align="right">Returns</TableCell></TableRow>
      </TableHead>
      <TableBody>
        {points.map((p) => (
          <TableRow key={p.label}>
            <TableCell>{p.label}</TableCell>
            <TableCell align="right">{p.value == null ? "—" : Math.round(p.value)}</TableCell>
          </TableRow>
        ))}
      </TableBody>
    </Table>
  );
}

// The chart is decoration for the sighted; the same figures are one click away in the records drawer.
function TrendLine({ points, unit }: { points: KpiSeriesPoint[]; unit: string }) {
  const theme = useTheme();
  if (points.length < 2) return <Typography sx={secondary}>Not enough points for a trend yet.</Typography>;
  const data = points.map((p) => ({ label: p.label, value: p.value == null ? null : scaleKpiValue(unit, p.value) }));
  return (
    <Box sx={{ height: 150 }} aria-hidden>
      <ResponsiveContainer width="100%" height="100%">
        <LineChart data={data}>
          <CartesianGrid vertical={false} stroke={theme.palette.divider} />
          <XAxis dataKey="label" tick={{ fontSize: 12, fill: theme.palette.text.secondary }} />
          <YAxis width={40} tick={{ fontSize: 12, fill: theme.palette.text.secondary }} />
          <RechartsTooltip />
          <Line type="monotone" dataKey="value" stroke={theme.custom.status.info.text} connectNulls={false} />
        </LineChart>
      </ResponsiveContainer>
    </Box>
  );
}

function SeriesBlock({ entry }: { entry: KpiEntry }) {
  const points = entry.result?.series ?? [];
  if (points.length === 0) return null;
  switch (entry.key) {
    case "stage-tat": return <StageBars points={points} unit={entry.unit} />;
    case "backlog-aging": return <AgingTable points={points} />;
    case "returns-by-test": return <ReturnsList points={points} />;
    default: return <TrendLine points={points} unit={entry.unit} />;
  }
}

function Panel({ entry, onOpenRecords }: { entry: KpiEntry; onOpenRecords(key: string): void }) {
  const theme = useTheme();
  const view = entryView(entry);
  return (
    <Paper sx={{ p: 2, height: "100%" }}>
      <Stack direction="row" sx={{ justifyContent: "space-between", alignItems: "center", mb: 1 }}>
        <Typography component="h3" sx={{ fontSize: 14, fontWeight: 700 }}>{entry.name}</Typography>
        {view.kind !== "error" && (
          <Button size="small" onClick={() => onOpenRecords(entry.key)} aria-label={`Show records for ${entry.name}`}>
            Records
          </Button>
        )}
      </Stack>
      {view.kind === "error" && (
        <Typography role="alert" sx={{ fontSize: 13, color: theme.custom.status.detected.text }}>
          Could not be calculated: {view.message}
        </Typography>
      )}
      {view.kind === "noData" && (
        <>
          <Typography sx={{ fontSize: 13, color: "text.secondary", mb: 1 }}>No data in this period</Typography>
          <KpiChips result={view.result} />
        </>
      )}
      {view.kind === "value" && (
        <Stack spacing={1}>
          {formatExtras(entry).map((x) => (
            <Typography key={x.label} sx={secondary}>{x.label} {x.text}</Typography>
          ))}
          {view.muted && <Typography sx={secondary}>too few records to judge</Typography>}
          {/* A count panel with no series (for example no returns in the period) must still say something. */}
          {view.result.series.length === 0 && view.text !== "" && (
            <>
              <Typography sx={{ fontSize: 18, fontWeight: 700 }}>{view.text}</Typography>
              {view.result.value === 0 && <Typography sx={secondary}>None in this period</Typography>}
            </>
          )}
          <SeriesBlock entry={entry} />
          <KpiChips result={view.result} />
        </Stack>
      )}
    </Paper>
  );
}

interface KpiSectionProps {
  title: string;
  entries: KpiEntry[];
  onOpenRecords(key: string): void;
}

export function KpiSection({ title, entries, onOpenRecords }: KpiSectionProps) {
  if (entries.length === 0) return null;
  return (
    <Box component="section" aria-label={title} sx={{ mb: 3 }}>
      <Typography component="h2" sx={{ fontSize: 16, fontWeight: 700, mb: 1.5 }}>{title}</Typography>
      <Grid container spacing={2}>
        {entries.map((e) => (
          <Grid key={e.key} size={{ xs: 12, md: 6 }}>
            <Panel entry={e} onOpenRecords={onOpenRecords} />
          </Grid>
        ))}
      </Grid>
    </Box>
  );
}
```

- [ ] **Step 4: Run to see it pass**

```bash
cd frontend && npx vitest run src/modules/kpi/components/KpiSection.test.tsx && npx tsc --noEmit -p tsconfig.json
```
Expected: 12 tests PASS; `tsc` clean. (Test "shows backlog aging..." expects exactly one dash: the missing `50to100` bucket. Test "failed entry ... no zero" relies on neither panel printing a bare `0`.)

- [ ] **Step 5: Commit**

```bash
cd /e/MicroLIMS/MicroLIMS && git add frontend/src/modules/kpi/components/KpiSection.tsx frontend/src/modules/kpi/components/KpiSection.test.tsx
git commit -F - <<'EOF'
feat(kpi): KpiSection with stage bars, aging table, returns list and trend lines

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
EOF
```

---

### Task 4: WorkloadTable

**Files:**
- Create: `frontend/src/modules/kpi/components/WorkloadTable.tsx`
- Test: `frontend/src/modules/kpi/components/WorkloadTable.test.tsx`

**Interfaces:**
- Consumes: `AnalystWorkload`, `AnalystWorkloadRow` (S3); `formatRatio`, `formatUnits`, `sortByName` (Task 1); `workloadRow`, `workload` fixtures; `theme.custom.status.{inconclusive,info}`.
- Produces:
```tsx
export function WorkloadTable(props: {
  data: AnalystWorkload | null; loading: boolean; error: string | null; onOpenTests(userId: number): void
}): JSX.Element | null
```
Behaviour: rows alphabetical by `fullName`; header buttons (`Capacity (units)`, `Delivered units`, `Utilisation`, `Open units`, `Forward load`, `Weighted on time`, `Weighted right first time`) cycle ascending, descending, back to alphabetical; missing values always last; the sort lives in component state only. Each analyst name is a button named `Show tests for <name>` that calls `onOpenTests(userId)`. Chips: `check profile` (when `utilisationCheckProfile`), `Over capacity` (when `forwardLoadOverCapacity`, amber), gap chips. Banners: `Showing your own numbers only.` (`onlyOwnRow`), `Capacity assumes full availability and the default hours.` (`capacityUsesDefaults`).

- [ ] **Step 1: Write the failing test**

Create `frontend/src/modules/kpi/components/WorkloadTable.test.tsx`:
```tsx
import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { workload, workloadRow } from "../kpiFixtures";
import type { AnalystWorkload } from "../types/workloadTypes";
import { WorkloadTable } from "./WorkloadTable";

afterEach(cleanup);

function renderTable(data: AnalystWorkload | null, over: { loading?: boolean; error?: string | null; onOpenTests?: (id: number) => void } = {}) {
  const onOpenTests = over.onOpenTests ?? vi.fn();
  return {
    onOpenTests,
    ...render(
      <PinnedLightTheme>
        <WorkloadTable data={data} loading={over.loading ?? false} error={over.error ?? null} onOpenTests={onOpenTests} />
      </PinnedLightTheme>
    )
  };
}

const names = () => screen.getAllByRole("button", { name: /^Show tests for/ }).map((b) => b.textContent);

// The API sends these ordered by delivered units, highest first.
const byOutput = workload([
  workloadRow({ userId: 1, fullName: "Amy Analyst", deliveredUnits: 30 }),
  workloadRow({ userId: 3, fullName: "Zed Analyst", deliveredUnits: 20 }),
  workloadRow({ userId: 2, fullName: "Bob Analyst", deliveredUnits: 5 })
]);

describe("WorkloadTable", () => {
  it("lists analysts alphabetically even when the API sends them by output", () => {
    renderTable(byOutput);
    expect(names()).toEqual(["Amy Analyst", "Bob Analyst", "Zed Analyst"]);
  });

  it("sorts on an explicit header click, then descending, then back to alphabetical", async () => {
    renderTable(byOutput);
    const header = () => screen.getByRole("button", { name: /^Delivered units/ });
    await userEvent.click(header());
    expect(names()).toEqual(["Bob Analyst", "Zed Analyst", "Amy Analyst"]);
    await userEvent.click(header());
    expect(names()).toEqual(["Amy Analyst", "Zed Analyst", "Bob Analyst"]);
    await userEvent.click(header());
    expect(names()).toEqual(["Amy Analyst", "Bob Analyst", "Zed Analyst"]);
  });

  it("does not remember an explicit sort", async () => {
    const first = renderTable(byOutput);
    await userEvent.click(screen.getByRole("button", { name: /^Delivered units/ }));
    expect(names()[0]).toBe("Bob Analyst");
    first.unmount();
    renderTable(byOutput);
    expect(names()).toEqual(["Amy Analyst", "Bob Analyst", "Zed Analyst"]);
  });

  it("puts rows with a missing value last whichever way it sorts", async () => {
    renderTable(workload([
      workloadRow({ userId: 1, fullName: "Amy Analyst", utilisation: null }),
      workloadRow({ userId: 2, fullName: "Bob Analyst", utilisation: 0.5 })
    ]));
    await userEvent.click(screen.getByRole("button", { name: /^Utilisation/ }));
    expect(names()).toEqual(["Bob Analyst", "Amy Analyst"]);
    await userEvent.click(screen.getByRole("button", { name: /^Utilisation/ }));
    expect(names()).toEqual(["Bob Analyst", "Amy Analyst"]);
  });

  it("says so when only the viewer's own row is returned", () => {
    renderTable(workload([workloadRow()], { onlyOwnRow: true }));
    expect(screen.getByText("Showing your own numbers only.")).toBeTruthy();
    expect(names()).toEqual(["Amy Analyst"]);
  });

  it("says nothing about own numbers when the team is returned", () => {
    renderTable(byOutput);
    expect(screen.queryByText("Showing your own numbers only.")).toBeNull();
  });

  it("shows the capacity-defaults banner", () => {
    renderTable(workload([workloadRow()], { capacityUsesDefaults: true }));
    expect(screen.getByText("Capacity assumes full availability and the default hours.")).toBeTruthy();
  });

  it("flags forward load over capacity and utilisation to check, only on the rows that need it", () => {
    renderTable(workload([
      workloadRow({ userId: 1, fullName: "Amy Analyst", forwardLoad: 1.4, forwardLoadOverCapacity: true, utilisation: 1.2, utilisationCheckProfile: true }),
      workloadRow({ userId: 2, fullName: "Bob Analyst" })
    ]));
    expect(screen.getAllByText("Over capacity").length).toBe(1);
    expect(screen.getAllByText("check profile").length).toBe(1);
    expect(screen.getByText("140%")).toBeTruthy();
  });

  it("shows a dash, not 0%, for a rate with no basis", () => {
    renderTable(workload([workloadRow({ utilisation: null, weightedOnTime: null, weightedRightFirstTime: null, forwardLoad: null })]));
    expect(screen.getAllByText("—").length).toBe(4);
    expect(screen.queryByText("0%")).toBeNull();
  });

  it("shows gap chips for unweighted and default-target tests and for unassigned open tests", () => {
    renderTable(workload(
      [workloadRow({ unweightedTests: 2, unweightedOpenTests: 1, testsOnDefaultTarget: 3 })],
      { unassignedOpenTests: 4, gaps: [{ code: "x", message: "5 tests have no profile", count: 5 }] }
    ));
    expect(screen.getByText("2 delivered tests unweighted")).toBeTruthy();
    expect(screen.getByText("1 open tests unweighted")).toBeTruthy();
    expect(screen.getByText("3 tests on the default target")).toBeTruthy();
    expect(screen.getByText("4 open tests have no analyst")).toBeTruthy();
    expect(screen.getByText("5 tests have no profile")).toBeTruthy();
  });

  it("opens the tests behind a row", async () => {
    const { onOpenTests } = renderTable(byOutput);
    await userEvent.click(screen.getByRole("button", { name: "Show tests for Zed Analyst" }));
    expect(onOpenTests).toHaveBeenCalledWith(3);
  });

  it("states that units are a planning estimate, not a score", () => {
    renderTable(byOutput);
    expect(screen.getByText(/planning estimate,\s+not a performance score/)).toBeTruthy();
  });

  it("shows loading, error and empty states", () => {
    const loading = renderTable(null, { loading: true });
    expect(screen.getByRole("progressbar")).toBeTruthy();
    loading.unmount();
    const failed = renderTable(null, { error: "Not permitted" });
    expect(screen.getByText("Not permitted")).toBeTruthy();
    failed.unmount();
    renderTable(workload([]));
    expect(screen.getByText("No analysts to show for this scope.")).toBeTruthy();
  });

  it("renders nothing when there is no data, no error and no request in flight", () => {
    const { container } = renderTable(null);
    expect(container.textContent).toBe("");
  });
});
```

- [ ] **Step 2: Run to see it fail**

```bash
cd frontend && npx vitest run src/modules/kpi/components/WorkloadTable.test.tsx
```
Expected: FAIL "Failed to resolve import ./WorkloadTable".

- [ ] **Step 3: Implement**

Create `frontend/src/modules/kpi/components/WorkloadTable.tsx`:
```tsx
import { useMemo, useState } from "react";
import {
  Alert, Button, Chip, LinearProgress, Paper, Stack, Table, TableBody, TableCell, TableContainer,
  TableHead, TableRow, TableSortLabel, Typography, useTheme
} from "@mui/material";
import type { AnalystWorkload, AnalystWorkloadRow } from "../types/workloadTypes";
import { formatRatio, formatUnits, sortByName } from "../utils/kpiFormat";

type SortKey =
  | "capacityUnits" | "deliveredUnits" | "utilisation" | "openUnits"
  | "forwardLoad" | "weightedOnTime" | "weightedRightFirstTime";

const COLUMNS: { key: SortKey; label: string }[] = [
  { key: "capacityUnits", label: "Capacity (units)" },
  { key: "deliveredUnits", label: "Delivered units" },
  { key: "utilisation", label: "Utilisation" },
  { key: "openUnits", label: "Open units" },
  { key: "forwardLoad", label: "Forward load" },
  { key: "weightedOnTime", label: "Weighted on time" },
  { key: "weightedRightFirstTime", label: "Weighted right first time" }
];

type Sort = { key: SortKey; dir: "asc" | "desc" } | null;

// Missing values always sort last, so a blank never looks like the best or worst.
function compareBy(key: SortKey, dir: "asc" | "desc") {
  return (a: AnalystWorkloadRow, b: AnalystWorkloadRow) => {
    const x = a[key];
    const y = b[key];
    if (x == null && y == null) return 0;
    if (x == null) return 1;
    if (y == null) return -1;
    return dir === "asc" ? x - y : y - x;
  };
}

interface WorkloadTableProps {
  data: AnalystWorkload | null;
  loading: boolean;
  error: string | null;
  onOpenTests(userId: number): void;
}

export function WorkloadTable({ data, loading, error, onOpenTests }: WorkloadTableProps) {
  const theme = useTheme();
  // Held in state only: an explicit sort is a look at the data, not a saved preference.
  const [sort, setSort] = useState<Sort>(null);

  const rows = useMemo(() => {
    const alphabetical = sortByName(data?.rows ?? []);
    return sort ? [...alphabetical].sort(compareBy(sort.key, sort.dir)) : alphabetical;
  }, [data, sort]);

  const toggle = (key: SortKey) =>
    setSort((prev) => (!prev || prev.key !== key ? { key, dir: "asc" } : prev.dir === "asc" ? { key, dir: "desc" } : null));

  if (!data && !loading && !error) return null;

  const amber = theme.custom.status.inconclusive;
  const info = theme.custom.status.info;
  const chip = (label: string, tone: { bg: string; text: string; border: string }, ml = 0) => (
    <Chip key={label} size="small" label={label} sx={{ ml, bgcolor: tone.bg, color: tone.text, border: `1px solid ${tone.border}`, fontSize: 12 }} />
  );

  const cell = (r: AnalystWorkloadRow, key: SortKey) => {
    switch (key) {
      case "capacityUnits": return formatUnits(r.capacityUnits);
      case "openUnits": return formatUnits(r.openUnits);
      case "deliveredUnits":
        return (
          <>
            {formatUnits(r.deliveredUnits)}
            <Typography component="span" sx={{ display: "block", fontSize: 12, color: "text.secondary" }}>
              {r.deliveredTests} tests
            </Typography>
          </>
        );
      case "utilisation":
        return (
          <>
            {formatRatio(r.utilisation)}
            {r.utilisationCheckProfile && chip("check profile", info, 0.5)}
          </>
        );
      case "forwardLoad":
        return (
          <>
            {formatRatio(r.forwardLoad)}
            {r.forwardLoadOverCapacity && chip("Over capacity", amber, 0.5)}
          </>
        );
      case "weightedOnTime": return formatRatio(r.weightedOnTime);
      case "weightedRightFirstTime": return formatRatio(r.weightedRightFirstTime);
    }
  };

  return (
    <Paper component="section" aria-label="Workload" sx={{ p: 2, mb: 3 }}>
      <Typography component="h2" sx={{ fontSize: 16, fontWeight: 700 }}>Workload</Typography>
      <Typography sx={{ fontSize: 12, color: "text.secondary", mb: 1.5 }}>
        Listed alphabetically. Click a column heading to sort; the choice is not saved. Units are a planning estimate,
        not a performance score (1 unit = 1 standard analyst-hour).
      </Typography>

      {loading && <LinearProgress sx={{ mb: 1 }} />}
      {error && <Alert severity="error" sx={{ mb: 1 }}>{error}</Alert>}

      {data && (
        <Stack spacing={1}>
          {data.onlyOwnRow && <Alert severity="info">Showing your own numbers only.</Alert>}
          {data.capacityUsesDefaults && (
            <Alert severity="info">Capacity assumes full availability and the default hours.</Alert>
          )}
          {(data.unassignedOpenTests > 0 || data.gaps.length > 0) && (
            <Stack direction="row" useFlexGap sx={{ flexWrap: "wrap", gap: 0.5 }}>
              {data.unassignedOpenTests > 0 && chip(`${data.unassignedOpenTests} open tests have no analyst`, amber)}
              {data.gaps.map((g) => chip(g.message, amber))}
            </Stack>
          )}

          {rows.length === 0 ? (
            <Typography sx={{ fontSize: 13, color: "text.secondary" }}>No analysts to show for this scope.</Typography>
          ) : (
            <TableContainer>
              <Table size="small">
                <TableHead>
                  <TableRow>
                    <TableCell>Analyst</TableCell>
                    {COLUMNS.map((c) => (
                      <TableCell key={c.key} align="right" sortDirection={sort?.key === c.key ? sort.dir : false}>
                        <TableSortLabel
                          active={sort?.key === c.key}
                          direction={sort?.key === c.key ? sort.dir : "asc"}
                          onClick={() => toggle(c.key)}
                        >
                          {c.label}
                        </TableSortLabel>
                      </TableCell>
                    ))}
                    <TableCell>Gaps</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {rows.map((r) => (
                    <TableRow key={r.userId}>
                      <TableCell>
                        <Button
                          size="small"
                          onClick={() => onOpenTests(r.userId)}
                          aria-label={`Show tests for ${r.fullName}`}
                          sx={{ textTransform: "none", fontWeight: 600 }}
                        >
                          {r.fullName}
                        </Button>
                      </TableCell>
                      {COLUMNS.map((c) => <TableCell key={c.key} align="right">{cell(r, c.key)}</TableCell>)}
                      <TableCell>
                        <Stack direction="row" useFlexGap sx={{ flexWrap: "wrap", gap: 0.5 }}>
                          {r.unweightedTests > 0 && chip(`${r.unweightedTests} delivered tests unweighted`, amber)}
                          {r.unweightedOpenTests > 0 && chip(`${r.unweightedOpenTests} open tests unweighted`, amber)}
                          {r.testsOnDefaultTarget > 0 && chip(`${r.testsOnDefaultTarget} tests on the default target`, amber)}
                        </Stack>
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </TableContainer>
          )}
        </Stack>
      )}
    </Paper>
  );
}
```

- [ ] **Step 4: Run to see it pass**

```bash
cd frontend && npx vitest run src/modules/kpi/components/WorkloadTable.test.tsx && npx tsc --noEmit -p tsconfig.json
```
Expected: 14 tests PASS; `tsc` clean. Notes: (a) the "check profile" chip text and the header button `Utilisation` are distinct strings, the header regex `/^Utilisation/` does not match the chip; (b) the "dash" test expects exactly four dashes (utilisation, forward load, weighted on time, weighted RFT) because every units column has a number in the fixture; (c) if the planning-estimate paragraph wraps differently the regex `/planning estimate,\s+not a performance score/` still matches because it tolerates the line break.

- [ ] **Step 5: Commit**

```bash
cd /e/MicroLIMS/MicroLIMS && git add frontend/src/modules/kpi/components/WorkloadTable.tsx frontend/src/modules/kpi/components/WorkloadTable.test.tsx
git commit -F - <<'EOF'
feat(kpi): WorkloadTable, alphabetical by default with no ranking

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
EOF
```

---

### Task 5: Data hooks (`useKpiDashboard`, `useKpiWorkload`)

**Files:**
- Create: `frontend/src/modules/kpi/hooks/useGuardedFetch.ts`
- Create: `frontend/src/modules/kpi/hooks/useKpiDashboard.ts`
- Create: `frontend/src/modules/kpi/hooks/useKpiWorkload.ts`
- Test: `frontend/src/modules/kpi/hooks/useKpiDashboard.test.tsx`
- Test: `frontend/src/modules/kpi/hooks/useKpiWorkload.test.tsx`

**Interfaces:**
- Consumes: `kpiDashboardService.dashboard(params)` (S2), `kpiWorkloadService.workload(params)` (S3), `toQueryParams`, `KpiFiltersValue` (Task 1), fixtures `dashboard`, `entry`, `workload`, `workloadRow`.
- Produces:
```ts
export interface GuardedFetch<T> { data: T | null; loading: boolean; error: string | null; reload(): Promise<void> }
export function useGuardedFetch<T>(fetcher: () => Promise<T>, failureMessage: string): GuardedFetch<T>   // fetcher must be memoised by the caller
export function useKpiDashboard(filters: KpiFiltersValue): { data: KpiDashboard | null; loading: boolean; error: string | null; reload(): Promise<void> }
export function useKpiWorkload(filters: KpiFiltersValue, analystId?: number): { data: AnalystWorkload | null; loading: boolean; error: string | null; reload(): Promise<void> }
```
Rules: only the newest request may write state; an error clears `data` (stale figures are not left under an error banner); `loading` starts `true`; a message comes from `response.data.message` or the fallback.

- [ ] **Step 1: Write the failing tests**

Create `frontend/src/modules/kpi/hooks/useKpiDashboard.test.tsx`:
```tsx
import { beforeEach, describe, expect, it, vi } from "vitest";
import { act, renderHook, waitFor } from "@testing-library/react";
import { kpiDashboardService } from "../services/kpiDashboardService";
import { dashboard, entry } from "../kpiFixtures";
import type { KpiDashboard } from "../types/kpiTypes";
import { useKpiDashboard } from "./useKpiDashboard";

vi.mock("../services/kpiDashboardService", () => ({
  kpiDashboardService: { dashboard: vi.fn(), records: vi.fn(), definitions: vi.fn() }
}));

const base = { from: "2026-09-12", to: "2026-10-11", category: null, location: null, testCode: null };

function deferred<T>() {
  let resolve!: (v: T) => void;
  let reject!: (e: unknown) => void;
  const promise = new Promise<T>((res, rej) => { resolve = res; reject = rej; });
  return { promise, resolve, reject };
}

beforeEach(() => {
  vi.mocked(kpiDashboardService.dashboard).mockReset();
});

describe("useKpiDashboard", () => {
  it("loads the dashboard for the filters and leaves unset filters out", async () => {
    vi.mocked(kpiDashboardService.dashboard).mockResolvedValue(dashboard([entry()]));
    const { result } = renderHook(() => useKpiDashboard({ ...base, testCode: "TAMC" }));
    expect(result.current.loading).toBe(true);
    await waitFor(() => expect(result.current.loading).toBe(false));
    expect(result.current.data?.kpis[0].key).toBe("sample-tat");
    expect(result.current.error).toBeNull();
    expect(kpiDashboardService.dashboard).toHaveBeenCalledWith({
      from: "2026-09-12", to: "2026-10-11", category: undefined, location: undefined, testCode: "TAMC"
    });
  });

  it("ignores a slow response for an earlier filter", async () => {
    const first = deferred<KpiDashboard>();
    const second = deferred<KpiDashboard>();
    vi.mocked(kpiDashboardService.dashboard)
      .mockReturnValueOnce(first.promise)
      .mockReturnValueOnce(second.promise);

    const { result, rerender } = renderHook(({ f }) => useKpiDashboard(f), { initialProps: { f: base } });
    rerender({ f: { ...base, from: "2026-09-01" } });

    await act(async () => { second.resolve(dashboard([entry({ key: "newer" })])); });
    await waitFor(() => expect(result.current.data?.kpis[0].key).toBe("newer"));

    await act(async () => { first.resolve(dashboard([entry({ key: "older" })])); });
    expect(result.current.data?.kpis[0].key).toBe("newer");
    expect(result.current.loading).toBe(false);
  });

  it("ignores a late failure from an earlier filter", async () => {
    const first = deferred<KpiDashboard>();
    const second = deferred<KpiDashboard>();
    vi.mocked(kpiDashboardService.dashboard)
      .mockReturnValueOnce(first.promise)
      .mockReturnValueOnce(second.promise);

    const { result, rerender } = renderHook(({ f }) => useKpiDashboard(f), { initialProps: { f: base } });
    rerender({ f: { ...base, to: "2026-10-10" } });

    await act(async () => { second.resolve(dashboard([entry({ key: "newer" })])); });
    await act(async () => { first.reject({ response: { data: { message: "old request failed" } } }); });

    expect(result.current.error).toBeNull();
    expect(result.current.data?.kpis[0].key).toBe("newer");
  });

  it("surfaces the server message and drops the stale figures on failure", async () => {
    vi.mocked(kpiDashboardService.dashboard)
      .mockResolvedValueOnce(dashboard([entry()]))
      .mockRejectedValueOnce({ response: { data: { message: "Not permitted" } } });
    const { result } = renderHook(() => useKpiDashboard(base));
    await waitFor(() => expect(result.current.data).not.toBeNull());

    await act(async () => { await result.current.reload(); });
    expect(result.current.error).toBe("Not permitted");
    expect(result.current.data).toBeNull();
    expect(result.current.loading).toBe(false);
  });

  it("falls back to a plain message when the failure has no body", async () => {
    vi.mocked(kpiDashboardService.dashboard).mockRejectedValue(new Error("Network Error"));
    const { result } = renderHook(() => useKpiDashboard(base));
    await waitFor(() => expect(result.current.error).toBe("The KPI figures could not be loaded."));
  });

  it("does not refetch when only the filters object identity changes", async () => {
    vi.mocked(kpiDashboardService.dashboard).mockResolvedValue(dashboard([entry()]));
    const { result, rerender } = renderHook(({ f }) => useKpiDashboard(f), { initialProps: { f: base } });
    await waitFor(() => expect(result.current.loading).toBe(false));
    rerender({ f: { ...base } });
    expect(kpiDashboardService.dashboard).toHaveBeenCalledTimes(1);
  });

  it("reload fetches again", async () => {
    vi.mocked(kpiDashboardService.dashboard).mockResolvedValue(dashboard([entry()]));
    const { result } = renderHook(() => useKpiDashboard(base));
    await waitFor(() => expect(result.current.loading).toBe(false));
    await act(async () => { await result.current.reload(); });
    expect(kpiDashboardService.dashboard).toHaveBeenCalledTimes(2);
  });
});
```

Create `frontend/src/modules/kpi/hooks/useKpiWorkload.test.tsx`:
```tsx
import { beforeEach, describe, expect, it, vi } from "vitest";
import { renderHook, waitFor } from "@testing-library/react";
import { kpiWorkloadService } from "../services/kpiWorkloadService";
import { workload, workloadRow } from "../kpiFixtures";
import { useKpiWorkload } from "./useKpiWorkload";

vi.mock("../services/kpiWorkloadService", () => ({
  kpiWorkloadService: { workload: vi.fn(), workloadTests: vi.fn() }
}));

const base = { from: "2026-09-12", to: "2026-10-11", category: null, location: null, testCode: null };

beforeEach(() => {
  vi.mocked(kpiWorkloadService.workload).mockReset();
});

describe("useKpiWorkload", () => {
  it("loads the workload without an analyst filter by default", async () => {
    vi.mocked(kpiWorkloadService.workload).mockResolvedValue(workload([workloadRow()]));
    const { result } = renderHook(() => useKpiWorkload(base));
    await waitFor(() => expect(result.current.loading).toBe(false));
    expect(result.current.data?.rows).toHaveLength(1);
    expect(kpiWorkloadService.workload).toHaveBeenCalledWith({
      from: "2026-09-12", to: "2026-10-11", category: undefined, location: undefined, testCode: undefined, analystId: undefined
    });
  });

  it("passes the analyst when one is given", async () => {
    vi.mocked(kpiWorkloadService.workload).mockResolvedValue(workload([workloadRow()]));
    const { result } = renderHook(() => useKpiWorkload(base, 7));
    await waitFor(() => expect(result.current.loading).toBe(false));
    expect(kpiWorkloadService.workload).toHaveBeenCalledWith(expect.objectContaining({ analystId: 7 }));
  });

  it("reports a refusal as its own error", async () => {
    vi.mocked(kpiWorkloadService.workload).mockRejectedValue({ response: { data: { message: "Forbidden" } } });
    const { result } = renderHook(() => useKpiWorkload(base));
    await waitFor(() => expect(result.current.error).toBe("Forbidden"));
    expect(result.current.data).toBeNull();
  });
});
```

- [ ] **Step 2: Run to see them fail**

```bash
cd frontend && npx vitest run src/modules/kpi/hooks
```
Expected: both files FAIL with "Failed to resolve import ./useKpiDashboard" / "./useKpiWorkload".

- [ ] **Step 3: Implement**

Create `frontend/src/modules/kpi/hooks/useGuardedFetch.ts`:
```ts
import { useCallback, useEffect, useRef, useState } from "react";

export interface GuardedFetch<T> {
  data: T | null;
  loading: boolean;
  error: string | null;
  reload(): Promise<void>;
}

// Only the newest request may write state. Without that, a slow response for
// an older filter lands last and the page shows figures for the wrong period.
// An error drops the data too: stale figures must not sit under an error banner.
// `fetcher` must be memoised by the caller (useCallback on the filter values).
export function useGuardedFetch<T>(fetcher: () => Promise<T>, failureMessage: string): GuardedFetch<T> {
  const [data, setData] = useState<T | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const latest = useRef(0);

  const reload = useCallback(async () => {
    const mine = ++latest.current;
    setLoading(true);
    setError(null);
    try {
      const res = await fetcher();
      if (mine === latest.current) setData(res);
    } catch (e: any) {
      if (mine === latest.current) {
        setData(null);
        setError(e?.response?.data?.message ?? failureMessage);
      }
    } finally {
      if (mine === latest.current) setLoading(false);
    }
  }, [fetcher, failureMessage]);

  useEffect(() => { void reload(); }, [reload]);

  return { data, loading, error, reload };
}
```

Create `frontend/src/modules/kpi/hooks/useKpiDashboard.ts`:
```ts
import { useCallback } from "react";
import { kpiDashboardService } from "../services/kpiDashboardService";
import { toQueryParams } from "../utils/kpiFilters";
import type { KpiFiltersValue } from "../utils/kpiFilters";
import { useGuardedFetch } from "./useGuardedFetch";

export function useKpiDashboard(filters: KpiFiltersValue) {
  const { from, to, category, location, testCode } = filters;
  const fetcher = useCallback(
    () => kpiDashboardService.dashboard(toQueryParams({ from, to, category, location, testCode })),
    [from, to, category, location, testCode]
  );
  return useGuardedFetch(fetcher, "The KPI figures could not be loaded.");
}
```

Create `frontend/src/modules/kpi/hooks/useKpiWorkload.ts`:
```ts
import { useCallback } from "react";
import { kpiWorkloadService } from "../services/kpiWorkloadService";
import { toQueryParams } from "../utils/kpiFilters";
import type { KpiFiltersValue } from "../utils/kpiFilters";
import { useGuardedFetch } from "./useGuardedFetch";

export function useKpiWorkload(filters: KpiFiltersValue, analystId?: number) {
  const { from, to, category, location, testCode } = filters;
  const fetcher = useCallback(
    () => kpiWorkloadService.workload({ ...toQueryParams({ from, to, category, location, testCode }), analystId }),
    [from, to, category, location, testCode, analystId]
  );
  return useGuardedFetch(fetcher, "The workload figures could not be loaded.");
}
```

- [ ] **Step 4: Run to see them pass**

```bash
cd frontend && npx vitest run src/modules/kpi/hooks && npx tsc --noEmit -p tsconfig.json
```
Expected: 10 tests PASS; `tsc` clean. If `tsc` rejects the object literal passed to the services (a property name in S2/S3's params type differs), change only the call in the two hook files.

- [ ] **Step 5: Commit**

```bash
cd /e/MicroLIMS/MicroLIMS && git add frontend/src/modules/kpi/hooks/useGuardedFetch.ts frontend/src/modules/kpi/hooks/useKpiDashboard.ts frontend/src/modules/kpi/hooks/useKpiWorkload.ts frontend/src/modules/kpi/hooks/useKpiDashboard.test.tsx frontend/src/modules/kpi/hooks/useKpiWorkload.test.tsx
git commit -F - <<'EOF'
feat(kpi): useKpiDashboard and useKpiWorkload with latest-request-wins

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
EOF
```

---

### Task 6: DefinitionsDrawer and KpiRecordsDrawer

**Files:**
- Create: `frontend/src/modules/kpi/components/DefinitionsDrawer.tsx`
- Create: `frontend/src/modules/kpi/components/KpiRecordsDrawer.tsx`
- Test: `frontend/src/modules/kpi/components/DefinitionsDrawer.test.tsx`
- Test: `frontend/src/modules/kpi/components/KpiRecordsDrawer.test.tsx`

**Interfaces:**
- Consumes: `kpiDashboardService.definitions(from, to)` (S2); `KpiDefinitions`, `KpiRecordPage`; `DIRECTION_LABEL`, `formatKpiValue`, `formatUnits` (Task 1).
- Produces:
```tsx
export function DefinitionsDrawer(props: { open: boolean; onClose(): void; from: string; to: string }): JSX.Element
export function KpiRecordsDrawer(props: { title: string; onClose(): void; load(page: number, pageSize: number): Promise<KpiRecordPage> }): JSX.Element
```
`DefinitionsDrawer` fetches only while `open`, lists every KPI by `order` (name as `<h3>`, then unit and direction, Definition, Clock events, Population, Source), then a table "Profile versions in force on <to>" (workflow type, test or "Type default", units and target with "inherited" for null, effective from). `KpiRecordsDrawer` is always open (the parent mounts it only while a figure is selected); it calls `load(page + 1, 50)` (the server pages from 1) and shows control number, test, category, analyst, event time, value, role in the figure (Numerator, Denominator, Excluded) and note.

- [ ] **Step 1: Write the failing tests**

Create `frontend/src/modules/kpi/components/DefinitionsDrawer.test.tsx`:
```tsx
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { kpiDashboardService } from "../services/kpiDashboardService";
import type { KpiDefinitions } from "../types/kpiTypes";
import { DefinitionsDrawer } from "./DefinitionsDrawer";

vi.mock("../services/kpiDashboardService", () => ({
  kpiDashboardService: { dashboard: vi.fn(), records: vi.fn(), definitions: vi.fn() }
}));

afterEach(cleanup);
beforeEach(() => vi.mocked(kpiDashboardService.definitions).mockReset());

const defs: KpiDefinitions = {
  kpis: [
    {
      key: "right-first-time", name: "Right first time", unit: "%", direction: "HigherIsBetter", group: "Headline", order: 3,
      definition: "Units of tests approved with no return divided by units approved.",
      clockEvents: "Test approval, return event", population: "Tests approved in the period", source: "TestOrder, TestReturnEvent"
    },
    {
      key: "sample-tat", name: "Sample TAT", unit: "days", direction: "LowerIsBetter", group: "Headline", order: 1,
      definition: "Days from receipt to approval.", clockEvents: "ReceivedAt to ApprovedAt",
      population: "Samples approved in the period", source: "Sample"
    }
  ],
  profilesInForce: [
    { workflowType: "CountTest", testCode: null, workUnits: 1.5, targetAnalystDays: 7, effectiveFrom: "2026-10-01", versionId: 4 },
    { workflowType: "Titration", testCode: "TITR-1", workUnits: null, targetAnalystDays: 5, effectiveFrom: "2026-10-03", versionId: 9 }
  ]
};

function renderDrawer(open = true, onClose: () => void = vi.fn()) {
  render(
    <PinnedLightTheme>
      <DefinitionsDrawer open={open} onClose={onClose} from="2026-09-12" to="2026-10-11" />
    </PinnedLightTheme>
  );
  return onClose;
}

describe("DefinitionsDrawer", () => {
  it("lists each KPI's definition, clock events, population and source in registry order", async () => {
    vi.mocked(kpiDashboardService.definitions).mockResolvedValue(defs);
    renderDrawer();
    expect(await screen.findByText("Days from receipt to approval.")).toBeTruthy();
    expect(screen.getByText("ReceivedAt to ApprovedAt")).toBeTruthy();
    expect(screen.getByText("Samples approved in the period")).toBeTruthy();
    expect(screen.getByText("TestOrder, TestReturnEvent")).toBeTruthy();
    expect(screen.getByText(/Lower is better/)).toBeTruthy(); // rendered inside "Unit: days · Lower is better"
    const order = screen.getAllByRole("heading", { name: /^(Sample TAT|Right first time)$/ }).map((h) => h.textContent);
    expect(order).toEqual(["Sample TAT", "Right first time"]);
    expect(kpiDashboardService.definitions).toHaveBeenCalledWith("2026-09-12", "2026-10-11");
  });

  it("shows the profile versions in force for the chosen period, with inherited values marked", async () => {
    vi.mocked(kpiDashboardService.definitions).mockResolvedValue(defs);
    renderDrawer();
    expect(await screen.findByRole("heading", { name: "Profile versions in force on 2026-10-11" })).toBeTruthy();
    expect(screen.getByText("CountTest")).toBeTruthy();
    expect(screen.getByText("Type default")).toBeTruthy();
    expect(screen.getByText("1.5")).toBeTruthy();
    expect(screen.getByText("TITR-1")).toBeTruthy();
    expect(screen.getByText("inherited")).toBeTruthy();
  });

  it("says so when no profile is in force", async () => {
    vi.mocked(kpiDashboardService.definitions).mockResolvedValue({ ...defs, profilesInForce: [] });
    renderDrawer();
    expect(await screen.findByText(/No effective profile versions/)).toBeTruthy();
  });

  it("does not fetch while closed", () => {
    renderDrawer(false);
    expect(kpiDashboardService.definitions).not.toHaveBeenCalled();
  });

  it("shows the server's message when the definitions cannot be loaded", async () => {
    vi.mocked(kpiDashboardService.definitions).mockRejectedValue({ response: { data: { message: "Definitions unavailable" } } });
    renderDrawer();
    expect(await screen.findByText("Definitions unavailable")).toBeTruthy();
  });

  it("closes from its close button", async () => {
    vi.mocked(kpiDashboardService.definitions).mockResolvedValue(defs);
    const onClose = renderDrawer();
    await screen.findByText("Days from receipt to approval.");
    await userEvent.click(screen.getByRole("button", { name: "Close definitions" }));
    expect(onClose).toHaveBeenCalled();
  });
});
```

Create `frontend/src/modules/kpi/components/KpiRecordsDrawer.test.tsx`:
```tsx
import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import type { KpiRecordPage } from "../types/kpiTypes";
import { KpiRecordsDrawer } from "./KpiRecordsDrawer";

afterEach(cleanup);

const page: KpiRecordPage = {
  key: "sample-tat", page: 1, pageSize: 50, total: 120,
  items: [{
    sampleId: 7, testOrderId: 11, controlNumber: "FP-0007", testCode: "TAMC", category: "FinishedProduct",
    analystName: "Amy Analyst", eventAt: "2026-10-01T08:00:00Z", value: 3.5, unit: "days",
    inclusion: "Numerator", note: "Approved on the second review"
  }]
};

function renderDrawer(load: (p: number, s: number) => Promise<KpiRecordPage>, onClose: () => void = vi.fn()) {
  render(
    <PinnedLightTheme>
      <KpiRecordsDrawer title="Sample TAT - records" onClose={onClose} load={load} />
    </PinnedLightTheme>
  );
}

describe("KpiRecordsDrawer", () => {
  it("loads page 1 and shows the record with its role in the figure", async () => {
    const load = vi.fn().mockResolvedValue(page);
    renderDrawer(load);
    expect(await screen.findByText("FP-0007")).toBeTruthy();
    expect(screen.getByText("Numerator")).toBeTruthy();
    expect(screen.getByText("3.5 days")).toBeTruthy();
    expect(screen.getByText("Approved on the second review")).toBeTruthy();
    expect(screen.getByText("Sample TAT - records")).toBeTruthy();
    expect(load).toHaveBeenCalledWith(1, 50);
  });

  it("formats a fractional percent record value as a percentage", async () => {
    const pct = { ...page, items: [{ ...page.items[0], value: 0.875, unit: "%" }] };
    renderDrawer(vi.fn().mockResolvedValue(pct));
    expect(await screen.findByText("87.5%")).toBeTruthy();
  });

  it("asks the server for the next page", async () => {
    const load = vi.fn().mockResolvedValue(page);
    renderDrawer(load);
    await screen.findByText("FP-0007");
    await userEvent.click(screen.getByRole("button", { name: /next page/i }));
    expect(load).toHaveBeenLastCalledWith(2, 50);
  });

  it("shows an empty page as no records", async () => {
    renderDrawer(vi.fn().mockResolvedValue({ ...page, total: 0, items: [] }));
    expect(await screen.findByText("No records.")).toBeTruthy();
  });

  it("shows the server's message on failure", async () => {
    renderDrawer(vi.fn().mockRejectedValue({ response: { data: { message: "Not permitted" } } }));
    expect(await screen.findByText("Not permitted")).toBeTruthy();
  });

  it("closes from its close button", async () => {
    const onClose = vi.fn();
    renderDrawer(vi.fn().mockResolvedValue(page), onClose);
    await screen.findByText("FP-0007");
    await userEvent.click(screen.getByRole("button", { name: "Close records" }));
    expect(onClose).toHaveBeenCalled();
  });
});
```

- [ ] **Step 2: Run to see them fail**

```bash
cd frontend && npx vitest run src/modules/kpi/components/DefinitionsDrawer.test.tsx src/modules/kpi/components/KpiRecordsDrawer.test.tsx
```
Expected: both FAIL with "Failed to resolve import".

- [ ] **Step 3: Implement**

Create `frontend/src/modules/kpi/components/DefinitionsDrawer.tsx`:
```tsx
import { useEffect, useState } from "react";
import type { ReactNode } from "react";
import {
  Alert, Box, Divider, Drawer, IconButton, LinearProgress, Stack, Table, TableBody, TableCell,
  TableHead, TableRow, Typography
} from "@mui/material";
import CloseIcon from "@mui/icons-material/Close";
import { kpiDashboardService } from "../services/kpiDashboardService";
import type { KpiDefinitions } from "../types/kpiTypes";
import { DIRECTION_LABEL, formatUnits } from "../utils/kpiFormat";

function Field({ label, children }: { label: string; children: ReactNode }) {
  return (
    <Box sx={{ mb: 1 }}>
      <Typography sx={{ fontSize: 11.5, fontWeight: 600, color: "text.secondary", textTransform: "uppercase" }}>{label}</Typography>
      <Typography sx={{ fontSize: 13 }}>{children}</Typography>
    </Box>
  );
}

interface DefinitionsDrawerProps {
  open: boolean;
  onClose(): void;
  from: string;
  to: string;
}

// Everything here comes from the backend registry, so the text on screen is the
// text the calculation was written against.
export function DefinitionsDrawer({ open, onClose, from, to }: DefinitionsDrawerProps) {
  const [data, setData] = useState<KpiDefinitions | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    if (!open) return;
    let live = true;
    setLoading(true);
    setError(null);
    kpiDashboardService.definitions(from, to)
      .then((d) => { if (live) setData(d); })
      .catch((e: any) => {
        if (live) {
          setData(null);
          setError(e?.response?.data?.message ?? "The definitions could not be loaded.");
        }
      })
      .finally(() => { if (live) setLoading(false); });
    return () => { live = false; };
  }, [open, from, to]);

  const kpis = [...(data?.kpis ?? [])].sort((a, b) => a.order - b.order);

  return (
    <Drawer anchor="right" open={open} onClose={onClose} slotProps={{ paper: { sx: { width: { xs: "100%", sm: 480 }, p: 2 } } }}>
      <Stack direction="row" sx={{ justifyContent: "space-between", alignItems: "center", mb: 1 }}>
        <Typography component="h2" sx={{ fontSize: 18, fontWeight: 700 }}>Definitions</Typography>
        <IconButton aria-label="Close definitions" onClick={onClose}><CloseIcon /></IconButton>
      </Stack>
      {loading && <LinearProgress sx={{ mb: 1 }} />}
      {error && <Alert severity="error">{error}</Alert>}
      {data && (
        <Box>
          {kpis.map((k) => (
            <Box key={k.key} component="section" sx={{ mb: 2 }}>
              <Typography component="h3" sx={{ fontSize: 15, fontWeight: 700 }}>{k.name}</Typography>
              <Typography sx={{ fontSize: 12, color: "text.secondary", mb: 1 }}>
                Unit: {k.unit} · {DIRECTION_LABEL[k.direction]}
              </Typography>
              <Field label="Definition">{k.definition}</Field>
              <Field label="Clock events">{k.clockEvents}</Field>
              <Field label="Population">{k.population}</Field>
              <Field label="Source">{k.source}</Field>
              <Divider />
            </Box>
          ))}

          <Typography component="h3" sx={{ fontSize: 15, fontWeight: 700, mb: 1 }}>
            Profile versions in force on {to}
          </Typography>
          {data.profilesInForce.length === 0 ? (
            <Typography sx={{ fontSize: 13, color: "text.secondary" }}>
              No effective profile versions on this date, so weighted measures show their tests as unweighted.
            </Typography>
          ) : (
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell>Workflow type</TableCell>
                  <TableCell>Test</TableCell>
                  <TableCell align="right">Work units</TableCell>
                  <TableCell align="right">Target (analyst days)</TableCell>
                  <TableCell>Effective from</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {data.profilesInForce.map((p) => (
                  <TableRow key={p.versionId}>
                    <TableCell>{p.workflowType}</TableCell>
                    <TableCell>{p.testCode ?? "Type default"}</TableCell>
                    <TableCell align="right">{p.workUnits == null ? "inherited" : formatUnits(p.workUnits)}</TableCell>
                    <TableCell align="right">{p.targetAnalystDays == null ? "inherited" : formatUnits(p.targetAnalystDays)}</TableCell>
                    <TableCell>{p.effectiveFrom}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </Box>
      )}
    </Drawer>
  );
}
```

Create `frontend/src/modules/kpi/components/KpiRecordsDrawer.tsx`:
```tsx
import { useEffect, useState } from "react";
import {
  Alert, Drawer, IconButton, LinearProgress, Stack, Table, TableBody, TableCell, TableHead, TableRow,
  TablePagination, Typography
} from "@mui/material";
import CloseIcon from "@mui/icons-material/Close";
import type { KpiRecordPage } from "../types/kpiTypes";
import { formatKpiValue } from "../utils/kpiFormat";

const PAGE_SIZE = 50;

interface KpiRecordsDrawerProps {
  title: string;
  onClose(): void;
  // Memoised by the caller (it changes only when a different figure is chosen).
  load(page: number, pageSize: number): Promise<KpiRecordPage>;
}

// The records behind one figure. The parent mounts this only while a figure is
// selected, so choosing another figure starts again on page 1.
export function KpiRecordsDrawer({ title, onClose, load }: KpiRecordsDrawerProps) {
  const [page, setPage] = useState(0); // 0-based for TablePagination; the server pages from 1
  const [data, setData] = useState<KpiRecordPage | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let live = true;
    setLoading(true);
    setError(null);
    load(page + 1, PAGE_SIZE)
      .then((d) => { if (live) setData(d); })
      .catch((e: any) => {
        if (live) {
          setData(null);
          setError(e?.response?.data?.message ?? "The records could not be loaded.");
        }
      })
      .finally(() => { if (live) setLoading(false); });
    return () => { live = false; };
  }, [load, page]);

  return (
    <Drawer anchor="right" open onClose={onClose} slotProps={{ paper: { sx: { width: { xs: "100%", md: 760 }, p: 2 } } }}>
      <Stack direction="row" sx={{ justifyContent: "space-between", alignItems: "center", mb: 1 }}>
        <Typography component="h2" sx={{ fontSize: 18, fontWeight: 700 }}>{title}</Typography>
        <IconButton aria-label="Close records" onClick={onClose}><CloseIcon /></IconButton>
      </Stack>
      {loading && <LinearProgress sx={{ mb: 1 }} />}
      {error && <Alert severity="error">{error}</Alert>}
      {data && (data.items.length === 0 ? (
        <Typography sx={{ fontSize: 13, color: "text.secondary" }}>No records.</Typography>
      ) : (
        <>
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Control no.</TableCell>
                <TableCell>Test</TableCell>
                <TableCell>Category</TableCell>
                <TableCell>Analyst</TableCell>
                <TableCell>Event</TableCell>
                <TableCell align="right">Value</TableCell>
                <TableCell>In the figure as</TableCell>
                <TableCell>Note</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {data.items.map((r, i) => (
                <TableRow key={`${r.testOrderId ?? r.sampleId ?? "r"}-${i}`}>
                  <TableCell>{r.controlNumber ?? "—"}</TableCell>
                  <TableCell>{r.testCode ?? "—"}</TableCell>
                  <TableCell>{r.category ?? "—"}</TableCell>
                  <TableCell>{r.analystName ?? "—"}</TableCell>
                  <TableCell>{r.eventAt ? new Date(r.eventAt).toLocaleString() : "—"}</TableCell>
                  <TableCell align="right">{r.value == null ? "—" : r.unit ? formatKpiValue(r.unit, r.value) : String(r.value)}</TableCell>
                  <TableCell>{r.inclusion}</TableCell>
                  <TableCell>{r.note ?? ""}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
          <TablePagination
            component="div"
            count={data.total}
            page={page}
            rowsPerPage={PAGE_SIZE}
            rowsPerPageOptions={[PAGE_SIZE]}
            onPageChange={(_, p) => setPage(p)}
          />
        </>
      ))}
    </Drawer>
  );
}
```

- [ ] **Step 4: Run to see them pass**

```bash
cd frontend && npx vitest run src/modules/kpi/components/DefinitionsDrawer.test.tsx src/modules/kpi/components/KpiRecordsDrawer.test.tsx && npx tsc --noEmit -p tsconfig.json
```
Expected: 12 tests PASS; `tsc` clean. If `slotProps={{ paper: ... }}` is rejected by this MUI version's `Drawer` types, use `PaperProps={{ sx: ... }}` in both drawers.

- [ ] **Step 5: Commit**

```bash
cd /e/MicroLIMS/MicroLIMS && git add frontend/src/modules/kpi/components/DefinitionsDrawer.tsx frontend/src/modules/kpi/components/DefinitionsDrawer.test.tsx frontend/src/modules/kpi/components/KpiRecordsDrawer.tsx frontend/src/modules/kpi/components/KpiRecordsDrawer.test.tsx
git commit -F - <<'EOF'
feat(kpi): Definitions drawer from the registry and a paged records drawer

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
EOF
```

---

### Task 7: PDF export from the same response

**Files:**
- Create: `frontend/src/modules/kpi/utils/exportKpiPdf.ts`
- Test: `frontend/src/modules/kpi/utils/exportKpiPdf.test.ts`

**Interfaces:**
- Consumes: `KpiDashboard`, `AnalystWorkload`, `KpiFiltersValue`; `entryView`, `formatExtras`, `formatKpiValue`, `formatPrior`, `formatRatio`, `formatUnits`, `sortByName`, `FLAG_LABELS` (Task 1).
- Produces:
```ts
export function buildKpiReportHtml(dashboard: KpiDashboard, workload: AnalystWorkload | null, filters: KpiFiltersValue, generatedBy: string, workloadError?: string | null): string
export function exportKpiPdf(dashboard: KpiDashboard, workload: AnalystWorkload | null, filters: KpiFiltersValue, generatedBy: string, workloadError?: string | null): void   // opens a window and prints, like the report it replaces
```
Printed figures use the same `entryView` / formatters as the screen. Headline entries print as one table (measure, value, n, prior, notes); every other entry prints its series as a table; the workload block is omitted when `workload` is null and no error was passed, and prints analysts alphabetically; when `workload` is null and `workloadError` is set (the screen shows that error), the report prints "Workload could not be loaded: <message>" in place of the block so the printout never silently drops it; all text is HTML-escaped.

- [ ] **Step 1: Write the failing test**

Create `frontend/src/modules/kpi/utils/exportKpiPdf.test.ts`:
```ts
import { afterEach, describe, expect, it, vi } from "vitest";
import { dashboard, entry, headlineEntries, point, workload, workloadRow } from "../kpiFixtures";
import { buildKpiReportHtml, exportKpiPdf } from "./exportKpiPdf";
import { formatKpiValue } from "./kpiFormat";
import type { KpiFiltersValue } from "./kpiFilters";

afterEach(() => vi.restoreAllMocks());

const filters: KpiFiltersValue = { from: "2026-09-12", to: "2026-10-11", category: "FinishedProduct", location: null, testCode: null };

describe("buildKpiReportHtml", () => {
  it("prints each figure exactly as the screen formats it", () => {
    const kpis = headlineEntries();
    const html = buildKpiReportHtml(dashboard(kpis), null, filters, "Sam Head");
    for (const k of kpis) {
      expect(html).toContain(formatKpiValue(k.unit, k.result!.value));
      expect(html).toContain(k.name);
    }
    expect(html).toContain("n</th>");
  });

  it("prints the scope, the signer and the server's generation time", () => {
    const html = buildKpiReportHtml(dashboard([entry()]), null, filters, "Sam Head");
    expect(html).toContain("2026-09-12 to 2026-10-11");
    expect(html).toContain("FinishedProduct");
    expect(html).toContain("Sam Head");
    expect(html).toMatch(/2026/);
  });

  it("prints a failed KPI and a no-data KPI as such, never as a number", () => {
    const html = buildKpiReportHtml(dashboard([
      entry({ key: "a", name: "Failed measure", error: "timed out" }, null),
      entry({ key: "b", name: "Empty measure", unit: "%" }, { status: "NoData", value: null, n: 0 })
    ]), null, filters, "Sam Head");
    expect(html).toContain("Could not be calculated");
    expect(html).toContain("timed out");
    expect(html).toContain("No data");
    expect(html).not.toContain("0.0%");
  });

  it("prints a real zero", () => {
    const html = buildKpiReportHtml(dashboard([entry({ key: "w", name: "WIP backlog", unit: "tests" }, { value: 0, n: 0 })]), null, filters, "Sam Head");
    expect(html).toContain("0 tests");
  });

  it("prints prior period, gaps, flags and the too-few-records note", () => {
    const html = buildKpiReportHtml(dashboard([
      entry({ unit: "%" }, {
        value: 0.4, n: 3, tooFewRecords: true, flags: ["AdverseTrend"],
        gaps: [{ code: "g", message: "2 tests unweighted", count: 2 }],
        prior: { value: 0.3, n: 22, numerator: null, denominator: null }
      })
    ]), null, filters, "Sam Head");
    expect(html).toContain("Prior period: 30.0% (n=22)");
    expect(html).toContain("2 tests unweighted");
    expect(html).toContain("Adverse trend");
    expect(html).toContain("too few records to judge");
  });

  it("prints supporting series as tables with median and P90 for stage turnaround", () => {
    const html = buildKpiReportHtml(dashboard([
      entry({ key: "stage-tat", name: "Stage turnaround", group: "Flow", order: 20 }, {
        series: [point("Testing", 2.1, { secondary: 4, n: 12 })]
      })
    ]), null, filters, "Sam Head");
    expect(html).toContain("Median");
    expect(html).toContain("P90");
    expect(html).toContain("Testing");
    expect(html).toContain("2.1 days");
    expect(html).toContain("4.0 days");
  });

  it("prints analysts alphabetically and escapes their names", () => {
    const html = buildKpiReportHtml(dashboard([entry()]), workload([
      workloadRow({ userId: 2, fullName: "Zed Analyst", deliveredUnits: 99 }),
      workloadRow({ userId: 1, fullName: "<b>Amy</b> Analyst", deliveredUnits: 1 })
    ]), filters, "Sam Head");
    expect(html).toContain("&lt;b&gt;Amy&lt;/b&gt; Analyst");
    expect(html).not.toContain("<b>Amy</b>");
    expect(html.indexOf("Amy")).toBeLessThan(html.indexOf("Zed Analyst"));
  });

  it("notes own-numbers-only and leaves the workload block out when there is no workload", () => {
    const own = buildKpiReportHtml(dashboard([entry()]), workload([workloadRow()], { onlyOwnRow: true }), filters, "Sam Head");
    expect(own).toContain("Showing the viewer&#39;s own numbers only.");
    const none = buildKpiReportHtml(dashboard([entry()]), null, filters, "Sam Head");
    expect(none).not.toContain("Workload");
  });

  it("says so when the workload failed to load instead of silently omitting it", () => {
    const html = buildKpiReportHtml(dashboard([entry()]), null, filters, "Sam Head", "Workload refused <now>");
    expect(html).toContain("Workload could not be loaded: Workload refused &lt;now&gt;");
  });
});

describe("exportKpiPdf", () => {
  it("writes the report into a new window", () => {
    const doc = { open: vi.fn(), write: vi.fn(), close: vi.fn() };
    const spy = vi.spyOn(window, "open").mockReturnValue({ document: doc } as unknown as Window);
    exportKpiPdf(dashboard([entry()]), null, filters, "Sam Head");
    expect(spy).toHaveBeenCalledWith("", "_blank");
    expect(doc.write).toHaveBeenCalledTimes(1);
    expect(String(doc.write.mock.calls[0][0])).toContain("Sample TAT");
    expect(doc.close).toHaveBeenCalled();
  });

  it("does nothing when the browser blocks the window", () => {
    vi.spyOn(window, "open").mockReturnValue(null);
    expect(() => exportKpiPdf(dashboard([entry()]), null, filters, "Sam Head")).not.toThrow();
  });
});
```

- [ ] **Step 2: Run to see it fail**

```bash
cd frontend && npx vitest run src/modules/kpi/utils/exportKpiPdf.test.ts
```
Expected: FAIL "Failed to resolve import ./exportKpiPdf".

- [ ] **Step 3: Implement**

Create `frontend/src/modules/kpi/utils/exportKpiPdf.ts`:
```ts
import type { KpiDashboard, KpiEntry } from "../types/kpiTypes";
import type { AnalystWorkload } from "../types/workloadTypes";
import type { KpiFiltersValue } from "./kpiFilters";
import {
  FLAG_LABELS, entryView, formatExtras, formatKpiValue, formatPrior, formatRatio, formatUnits, sortByName
} from "./kpiFormat";

function esc(value: string | number | null | undefined): string {
  if (value === null || value === undefined) return "—";
  return String(value)
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;")
    .replace(/'/g, "&#39;");
}

const row = (cells: (string | number | null | undefined)[], tag: "td" | "th" = "td") =>
  `<tr>${cells.map((c) => `<${tag}>${esc(c)}</${tag}>`).join("")}</tr>`;

function valueText(e: KpiEntry): string {
  const v = entryView(e);
  if (v.kind === "error") return "Could not be calculated";
  if (v.kind === "noData") return "No data";
  return v.text;
}

function notesText(e: KpiEntry): string {
  const v = entryView(e);
  if (v.kind === "error") return v.message;
  const parts: string[] = [];
  if (v.kind === "value" && v.muted) parts.push("too few records to judge");
  for (const x of formatExtras(e)) parts.push(`${x.label} ${x.text}`);
  for (const g of v.result.gaps) parts.push(g.message);
  for (const f of v.result.flags) parts.push(FLAG_LABELS[f] ?? f);
  return parts.join("; ");
}

function headlineTable(entries: KpiEntry[]): string {
  const body = entries.map((e) => {
    const r = e.result;
    return row([e.name, valueText(e), r ? r.n : "—", r?.prior ? formatPrior(e.unit, r.prior) : "", notesText(e)]);
  });
  return `<table><thead>${row(["Measure", "Value", "n", "Prior period", "Notes"], "th")}</thead><tbody>${body.join("")}</tbody></table>`;
}

function seriesTable(e: KpiEntry): string {
  const points = e.result?.series ?? [];
  if (points.length === 0) return "";
  const stage = e.key === "stage-tat";
  const hasSecondary = points.some((p) => p.secondary != null);
  const head = ["Item", stage ? "Median" : "Value", ...(hasSecondary ? [stage ? "P90" : "Secondary"] : []), "n"];
  const body = points.map((p) =>
    row([
      p.group ? `${p.group} / ${p.label}` : p.label,
      formatKpiValue(e.unit, p.value),
      ...(hasSecondary ? [formatKpiValue(e.unit, p.secondary)] : []),
      p.n
    ])
  );
  return `<div class="section-title">${esc(e.name)}: ${esc(valueText(e))}</div><table><thead>${row(head, "th")}</thead><tbody>${body.join("")}</tbody></table>`;
}

function workloadBlock(w: AnalystWorkload): string {
  const notes: string[] = [];
  if (w.onlyOwnRow) notes.push("Showing the viewer's own numbers only.");
  if (w.capacityUsesDefaults) notes.push("Capacity assumes full availability and the default hours.");
  if (w.unassignedOpenTests > 0) notes.push(`${w.unassignedOpenTests} open tests have no analyst.`);
  for (const g of w.gaps) notes.push(g.message);
  const body = sortByName(w.rows).map((r) =>
    row([
      r.fullName, formatUnits(r.capacityUnits), formatUnits(r.deliveredUnits),
      r.utilisationCheckProfile ? `${formatRatio(r.utilisation)} (check profile)` : formatRatio(r.utilisation),
      formatUnits(r.openUnits),
      r.forwardLoadOverCapacity ? `${formatRatio(r.forwardLoad)} (over capacity)` : formatRatio(r.forwardLoad),
      formatRatio(r.weightedOnTime), formatRatio(r.weightedRightFirstTime)
    ])
  );
  const head = ["Analyst", "Capacity (units)", "Delivered units", "Utilisation", "Open units", "Forward load", "Weighted on time", "Weighted right first time"];
  return `<div class="section-title">Workload</div>
    ${notes.map((n) => `<div class="note">${esc(n)}</div>`).join("")}
    <table><thead>${row(head, "th")}</thead><tbody>${body.join("")}</tbody></table>
    <div class="note">Listed alphabetically. Units are a planning estimate, not a performance score (1 unit = 1 standard analyst-hour).</div>`;
}

export function buildKpiReportHtml(
  dashboard: KpiDashboard,
  workload: AnalystWorkload | null,
  filters: KpiFiltersValue,
  generatedBy: string,
  workloadError?: string | null
): string {
  const generatedAt = new Date(dashboard.generatedAt).toLocaleString("en-GB", {
    day: "2-digit", month: "short", year: "numeric", hour: "2-digit", minute: "2-digit"
  });
  const scope = [
    `Period: ${filters.from} to ${filters.to}`,
    filters.category ? `Category: ${filters.category}` : null,
    filters.location ? `Location: ${filters.location}` : null,
    filters.testCode ? `Test: ${filters.testCode}` : null
  ].filter(Boolean).join(" | ");

  const ordered = [...dashboard.kpis].sort((a, b) => a.order - b.order);
  const headline = ordered.filter((e) => e.group === "Headline");
  const supporting = ordered.filter((e) => e.group !== "Headline");

  return `<!DOCTYPE html>
<html>
<head>
  <meta charset="utf-8">
  <title>KPI &amp; Performance Report</title>
  <style>
    @page { size: A4 landscape; margin: 12mm; }
    body { font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Arial, sans-serif; font-size: 10pt; color: #1e293b; margin: 0; padding: 12px; }
    .header { border-bottom: 2px solid #0f766e; padding-bottom: 8px; margin-bottom: 12px; }
    .title { font-size: 16pt; font-weight: bold; color: #0f766e; }
    .meta { font-size: 8.5pt; color: #64748b; margin-top: 4px; }
    .section-title { font-size: 11pt; font-weight: bold; color: #0f766e; margin: 12px 0 6px 0; }
    table { width: 100%; border-collapse: collapse; font-size: 8.5pt; margin-bottom: 14px; }
    th { background: #0f766e; color: #ffffff; padding: 6px 8px; text-align: left; font-weight: 600; }
    td { padding: 5px 8px; border-bottom: 1px solid #e2e8f0; }
    .note { font-size: 8.5pt; color: #64748b; margin: 2px 0 6px 0; }
    .footer { margin-top: 16px; border-top: 1px solid #cbd5e1; padding-top: 8px; font-size: 7.5pt; color: #64748b; }
  </style>
</head>
<body>
  <div class="header">
    <div class="title">MicroLIMS KPI &amp; Performance Report</div>
    <div class="meta"><strong>Scope:</strong> ${esc(scope)} &nbsp;|&nbsp; <strong>Generated by:</strong> ${esc(generatedBy)} &nbsp;|&nbsp; <strong>Calculated:</strong> ${esc(generatedAt)}</div>
  </div>

  <div class="section-title">Headline measures</div>
  ${headlineTable(headline)}

  ${supporting.map(seriesTable).join("")}

  ${workload
    ? workloadBlock(workload)
    : workloadError
      ? `<div class="section-title">Workload</div><div class="note">${esc(`Workload could not be loaded: ${workloadError}`)}</div>`
      : ""}

  <div class="footer">Figures are as shown on screen for the same period and filters. GMP audit trail and data-integrity standard. MicroLIMS verified report.</div>

  <script>
    window.addEventListener("load", () => { window.print(); });
  </script>
</body>
</html>`;
}

export function exportKpiPdf(
  dashboard: KpiDashboard,
  workload: AnalystWorkload | null,
  filters: KpiFiltersValue,
  generatedBy: string,
  workloadError?: string | null
): void {
  const html = buildKpiReportHtml(dashboard, workload, filters, generatedBy, workloadError);
  const printWindow = window.open("", "_blank");
  if (printWindow) {
    printWindow.document.open();
    printWindow.document.write(html);
    printWindow.document.close();
  }
}
```

- [ ] **Step 4: Run to see it pass**

```bash
cd frontend && npx vitest run src/modules/kpi/utils/exportKpiPdf.test.ts && npx tsc --noEmit -p tsconfig.json
```
Expected: 11 tests PASS; `tsc` clean. Note for the executor: the test "prints a failed KPI and a no-data KPI ... not.toContain('0.0%')" holds because the fixture entries print "Could not be calculated" and "No data" and the report footer contains no number; do not add numeric footer text.

- [ ] **Step 5: Commit**

```bash
cd /e/MicroLIMS/MicroLIMS && git add frontend/src/modules/kpi/utils/exportKpiPdf.ts frontend/src/modules/kpi/utils/exportKpiPdf.test.ts
git commit -F - <<'EOF'
feat(kpi): PDF report built from the same dashboard and workload objects

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
EOF
```

---

### Task 8: KpiFilters

**Files:**
- Create: `frontend/src/modules/kpi/components/KpiFilters.tsx`
- Test: `frontend/src/modules/kpi/components/KpiFilters.test.tsx`

**Interfaces:**
- Consumes: `ReportingService.getFilterOptions()` (existing, `frontend/src/modules/reports/services/ReportingService.ts`, returns `{ categories, testCodes: {testCode, testDisplayName}[], subjectNames, units }`); `CATEGORY_LABELS` (existing, `OverviewService.ts`); `KpiFiltersValue`, `filtersError` (Task 1).
- Produces:
```tsx
export function KpiFilters(props: {
  value: KpiFiltersValue; onChange(v: KpiFiltersValue): void;
  onOpenDefinitions(): void; onOpenProfiles(): void; onExport(): void; exportDisabled: boolean
}): JSX.Element
```
`onChange` is called only with a valid range (both dates set, From not after To); an invalid or half-typed range shows an inline message and leaves the last valid value untouched. Buttons: `Definitions`, `Workflow Profiles`, `Export PDF`.

- [ ] **Step 1: Write the failing test**

Create `frontend/src/modules/kpi/components/KpiFilters.test.tsx`:
```tsx
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { ReportingService } from "../../reports/services/ReportingService";
import type { KpiFiltersValue } from "../utils/kpiFilters";
import { KpiFilters } from "./KpiFilters";

vi.mock("../../reports/services/ReportingService", () => ({
  ReportingService: { getFilterOptions: vi.fn() }
}));

afterEach(cleanup);
beforeEach(() => {
  vi.mocked(ReportingService.getFilterOptions).mockReset();
  vi.mocked(ReportingService.getFilterOptions).mockResolvedValue({
    categories: ["FinishedProduct", "Water"], testCodes: [{ testCode: "TAMC", testDisplayName: "TAMC" }],
    subjectNames: ["Product A"], units: []
  } as never);
});

const value: KpiFiltersValue = { from: "2026-09-12", to: "2026-10-11", category: null, location: null, testCode: null };

function renderFilters(over: { onChange?: (v: KpiFiltersValue) => void; exportDisabled?: boolean } = {}) {
  const handlers = {
    onChange: over.onChange ?? vi.fn(),
    onOpenDefinitions: vi.fn(),
    onOpenProfiles: vi.fn(),
    onExport: vi.fn()
  };
  render(
    <PinnedLightTheme>
      <KpiFilters value={value} exportDisabled={over.exportDisabled ?? false} {...handlers} />
    </PinnedLightTheme>
  );
  return handlers;
}

describe("KpiFilters", () => {
  it("reports a valid new range", () => {
    const { onChange } = renderFilters();
    fireEvent.change(screen.getByLabelText("From"), { target: { value: "2026-09-20" } });
    expect(onChange).toHaveBeenCalledWith({ ...value, from: "2026-09-20" });
  });

  it("does not call onChange for an inverted or empty range", () => {
    const { onChange } = renderFilters();
    fireEvent.change(screen.getByLabelText("From"), { target: { value: "2026-10-20" } });
    expect(screen.getByText("The start date must not be after the end date.")).toBeTruthy();
    fireEvent.change(screen.getByLabelText("From"), { target: { value: "" } });
    expect(screen.getByText("Choose both dates.")).toBeTruthy();
    expect(onChange).not.toHaveBeenCalled();
  });

  it("offers the categories with their readable labels and reports a choice", async () => {
    const { onChange } = renderFilters();
    await userEvent.click(screen.getByRole("combobox", { name: "Category" }));
    await userEvent.click(await screen.findByRole("option", { name: "Finished Product" }));
    expect(onChange).toHaveBeenCalledWith({ ...value, category: "FinishedProduct" });
  });

  it("tells the user when the option lists could not be loaded", async () => {
    vi.mocked(ReportingService.getFilterOptions).mockRejectedValue(new Error("boom"));
    renderFilters();
    expect(await screen.findByRole("alert")).toBeTruthy();
    expect(screen.getByText(/could not be loaded/)).toBeTruthy();
  });

  it("wires the three buttons and disables export on request", async () => {
    const h = renderFilters({ exportDisabled: true });
    await userEvent.click(screen.getByRole("button", { name: "Definitions" }));
    await userEvent.click(screen.getByRole("button", { name: "Workflow Profiles" }));
    expect(h.onOpenDefinitions).toHaveBeenCalled();
    expect(h.onOpenProfiles).toHaveBeenCalled();
    expect((screen.getByRole("button", { name: "Export PDF" }) as HTMLButtonElement).disabled).toBe(true);
  });
});
```

- [ ] **Step 2: Run to see it fail**

```bash
cd frontend && npx vitest run src/modules/kpi/components/KpiFilters.test.tsx
```
Expected: FAIL "Failed to resolve import ./KpiFilters".

- [ ] **Step 3: Implement**

Create `frontend/src/modules/kpi/components/KpiFilters.tsx`:
```tsx
import { useEffect, useState } from "react";
import { Alert, Button, MenuItem, Paper, Stack, TextField } from "@mui/material";
import InfoOutlinedIcon from "@mui/icons-material/InfoOutlined";
import PictureAsPdfIcon from "@mui/icons-material/PictureAsPdf";
import TuneIcon from "@mui/icons-material/Tune";
import { ReportingService } from "../../reports/services/ReportingService";
import { CATEGORY_LABELS } from "../../reports/services/OverviewService";
import { filtersError } from "../utils/kpiFilters";
import type { KpiFiltersValue } from "../utils/kpiFilters";

interface KpiFiltersProps {
  value: KpiFiltersValue;
  onChange(v: KpiFiltersValue): void;
  onOpenDefinitions(): void;
  onOpenProfiles(): void;
  onExport(): void;
  exportDisabled: boolean;
}

export function KpiFilters({ value, onChange, onOpenDefinitions, onOpenProfiles, onExport, exportDisabled }: KpiFiltersProps) {
  const [options, setOptions] = useState<{ categories: string[]; locations: string[]; testCodes: string[] }>({
    categories: [], locations: [], testCodes: []
  });
  const [optionsFailed, setOptionsFailed] = useState(false);
  // The date inputs hold their own draft: <input type="date"> reports "" while
  // a date is half typed, and a half-typed range must not reach the server.
  const [from, setFrom] = useState(value.from);
  const [to, setTo] = useState(value.to);
  const dateError = filtersError({ from, to });

  useEffect(() => {
    let live = true;
    ReportingService.getFilterOptions()
      .then((o) => {
        if (live && o) {
          setOptions({
            categories: o.categories ?? [],
            locations: o.subjectNames ?? [],
            testCodes: (o.testCodes ?? []).map((t) => t.testCode)
          });
        }
      })
      .catch(() => { if (live) setOptionsFailed(true); });
    return () => { live = false; };
  }, []);

  const changeDates = (nextFrom: string, nextTo: string) => {
    setFrom(nextFrom);
    setTo(nextTo);
    if (!filtersError({ from: nextFrom, to: nextTo })) onChange({ ...value, from: nextFrom, to: nextTo });
  };

  return (
    <Paper sx={{ p: 2, mb: 2.5 }}>
      <Stack direction={{ xs: "column", md: "row" }} spacing={1.5} useFlexGap sx={{ flexWrap: "wrap", alignItems: { md: "flex-start" } }}>
        <TextField
          size="small" type="date" label="From" value={from}
          onChange={(e) => changeDates(e.target.value, to)}
          slotProps={{ inputLabel: { shrink: true } }} error={!!dateError}
        />
        <TextField
          size="small" type="date" label="To" value={to}
          onChange={(e) => changeDates(from, e.target.value)}
          slotProps={{ inputLabel: { shrink: true } }} error={!!dateError} helperText={dateError ?? undefined}
        />
        <TextField
          select size="small" label="Category" value={value.category ?? ""} sx={{ minWidth: 180 }}
          onChange={(e) => onChange({ ...value, category: e.target.value || null })}
        >
          <MenuItem value="">All categories</MenuItem>
          {options.categories.map((c) => <MenuItem key={c} value={c}>{CATEGORY_LABELS[c] ?? c}</MenuItem>)}
        </TextField>
        <TextField
          select size="small" label="Location / subject" value={value.location ?? ""} sx={{ minWidth: 180 }}
          onChange={(e) => onChange({ ...value, location: e.target.value || null })}
        >
          <MenuItem value="">All locations</MenuItem>
          {options.locations.map((l) => <MenuItem key={l} value={l}>{l}</MenuItem>)}
        </TextField>
        <TextField
          select size="small" label="Test" value={value.testCode ?? ""} sx={{ minWidth: 140 }}
          onChange={(e) => onChange({ ...value, testCode: e.target.value || null })}
        >
          <MenuItem value="">All tests</MenuItem>
          {options.testCodes.map((t) => <MenuItem key={t} value={t}>{t}</MenuItem>)}
        </TextField>

        <Stack direction="row" spacing={1} useFlexGap sx={{ flexWrap: "wrap", ml: { md: "auto" } }}>
          <Button size="small" variant="outlined" startIcon={<InfoOutlinedIcon />} onClick={onOpenDefinitions}>Definitions</Button>
          <Button size="small" variant="outlined" startIcon={<TuneIcon />} onClick={onOpenProfiles}>Workflow Profiles</Button>
          <Button size="small" variant="contained" startIcon={<PictureAsPdfIcon />} onClick={onExport} disabled={exportDisabled}>Export PDF</Button>
        </Stack>
      </Stack>
      {optionsFailed && (
        <Alert severity="error" sx={{ mt: 1.5 }}>
          The category, location and test lists could not be loaded. Reload the page to try again.
        </Alert>
      )}
    </Paper>
  );
}
```

- [ ] **Step 4: Run to see it pass**

```bash
cd frontend && npx vitest run src/modules/kpi/components/KpiFilters.test.tsx && npx tsc --noEmit -p tsconfig.json
```
Expected: 5 tests PASS; `tsc` clean. Note: `<Alert>` has `role="alert"`; the test asserts on it.

- [ ] **Step 5: Commit**

```bash
cd /e/MicroLIMS/MicroLIMS && git add frontend/src/modules/kpi/components/KpiFilters.tsx frontend/src/modules/kpi/components/KpiFilters.test.tsx
git commit -F - <<'EOF'
feat(kpi): page-owned filter bar that never sends an invalid date range

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
EOF
```

---

### Task 9: KpiPerformanceTab

**Files:**
- Create: `frontend/src/modules/kpi/components/KpiPerformanceTab.tsx`
- Test: `frontend/src/modules/kpi/components/KpiPerformanceTab.test.tsx`

**Interfaces:**
- Consumes (this plan): `KpiFilters`, `KpiTile`, `KpiSection`, `WorkloadTable`, `DefinitionsDrawer`, `KpiRecordsDrawer`, `useKpiDashboard`, `useKpiWorkload`, `exportKpiPdf`, `defaultFilters`, `toQueryParams`, `KpiFiltersValue`. Consumes (S1): `WorkflowProfilesDialog` (`{ open: boolean; onClose(): void }`). Consumes (S2/S3): `kpiDashboardService.records(key, params)`, `.definitions(from, to)`, `kpiWorkloadService.workloadTests(params)`. Consumes (existing): `useAuth()` from `frontend/src/contexts/AuthContext.tsx` (`fullName`).
- Produces: `export function KpiPerformanceTab(): JSX.Element` (no props), the component `Reports.tsx` mounts. Layout, top to bottom: filter bar, error or progress, eight headline tiles (entries whose `group === "Headline"`, by `order`), `Flow` section (`stage-tat`, `backlog-aging`), `Quality` (`right-first-time`, `retest-rate`, `oos-rate`, `returns-by-test`), `Micro` (`em-excursion-rate`, `incubation-reads-on-time`), then the workload table. Clicking a tile, a panel's Records button or a workload analyst name opens `KpiRecordsDrawer` with `load` bound to the right service call and the current filters. Export is disabled while either request is in flight or the dashboard failed.

- [ ] **Step 1: Write the failing test**

Create `frontend/src/modules/kpi/components/KpiPerformanceTab.test.tsx`:
```tsx
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { ReportingService } from "../../reports/services/ReportingService";
import { kpiDashboardService } from "../services/kpiDashboardService";
import { kpiWorkloadService } from "../services/kpiWorkloadService";
import { dashboard, entry, headlineEntries, point, workload, workloadRow } from "../kpiFixtures";
import type { KpiRecordPage } from "../types/kpiTypes";
import { KpiPerformanceTab } from "./KpiPerformanceTab";

vi.mock("../services/kpiDashboardService", () => ({
  kpiDashboardService: { dashboard: vi.fn(), records: vi.fn(), definitions: vi.fn() }
}));
vi.mock("../services/kpiWorkloadService", () => ({
  kpiWorkloadService: { workload: vi.fn(), workloadTests: vi.fn() }
}));
vi.mock("../../reports/services/ReportingService", () => ({
  ReportingService: { getFilterOptions: vi.fn() }
}));
vi.mock("../../../contexts/AuthContext", () => ({
  useAuth: () => ({ fullName: "Sam Head" })
}));
vi.mock("./WorkflowProfilesDialog", async () => {
  const { createElement } = await import("react");
  return { WorkflowProfilesDialog: ({ open }: { open: boolean }) => (open ? createElement("div", null, "profiles dialog") : null) };
});
vi.mock("recharts", async () => {
  const { createElement, Fragment } = await import("react");
  const Pass = ({ children }: { children?: unknown }) => createElement(Fragment, null, children as never);
  const Nil = () => null;
  return { ResponsiveContainer: Pass, LineChart: Pass, Line: Nil, XAxis: Nil, YAxis: Nil, Tooltip: Nil, CartesianGrid: Nil };
});

afterEach(() => { cleanup(); vi.restoreAllMocks(); });

const recordsPage: KpiRecordPage = {
  key: "sample-tat", page: 1, pageSize: 50, total: 1,
  items: [{
    sampleId: 7, testOrderId: 11, controlNumber: "FP-0007", testCode: "TAMC", category: "FinishedProduct",
    analystName: "Amy Analyst", eventAt: "2026-10-01T08:00:00Z", value: 3.5, unit: "days", inclusion: "Numerator", note: null
  }]
};

const allEntries = () => [
  ...headlineEntries(),
  entry({ key: "stage-tat", name: "Stage turnaround", group: "Flow", order: 20 }, { series: [point("Testing", 2.1, { secondary: 4, n: 12 })] }),
  entry({ key: "backlog-aging", name: "Backlog aging", unit: "tests", group: "Flow", order: 21 }, { series: [point("within50", 5, { group: "Testing" })] }),
  entry({ key: "returns-by-test", name: "Returns by test", unit: "tests", group: "Quality", order: 30 }, { series: [point("TAMC", 3)] })
];

beforeEach(() => {
  vi.mocked(kpiDashboardService.dashboard).mockReset().mockResolvedValue(dashboard(allEntries()));
  vi.mocked(kpiDashboardService.records).mockReset().mockResolvedValue(recordsPage);
  vi.mocked(kpiDashboardService.definitions).mockReset().mockResolvedValue({ kpis: [], profilesInForce: [] });
  vi.mocked(kpiWorkloadService.workload).mockReset().mockResolvedValue(
    workload([workloadRow({ userId: 1, fullName: "Amy Analyst" }), workloadRow({ userId: 2, fullName: "Bob Analyst" })])
  );
  vi.mocked(kpiWorkloadService.workloadTests).mockReset().mockResolvedValue(recordsPage);
  vi.mocked(ReportingService.getFilterOptions).mockReset().mockResolvedValue(
    { categories: ["FinishedProduct"], testCodes: [], subjectNames: [], units: [] } as never
  );
});

function renderTab() {
  return render(<PinnedLightTheme><KpiPerformanceTab /></PinnedLightTheme>);
}

describe("KpiPerformanceTab", () => {
  it("renders the eight headline tiles, the sections and the workload table", async () => {
    renderTab();
    for (const name of ["Sample TAT", "On time, all stages", "Right first time", "Retest rate", "OOS rate", "WIP backlog", "Incubation reads on time", "EM excursion rate"]) {
      expect(await screen.findByRole("button", { name: new RegExp(`^${name}: `) })).toBeTruthy();
    }
    for (const title of ["Flow", "Quality", "Micro", "Workload"]) {
      expect(screen.getByRole("heading", { name: title })).toBeTruthy();
    }
    expect(screen.getByRole("heading", { name: "Stage turnaround" })).toBeTruthy();
    expect(screen.getByRole("button", { name: "Show tests for Amy Analyst" })).toBeTruthy();
  });

  it("opens the records behind a tile", async () => {
    renderTab();
    await userEvent.click(await screen.findByRole("button", { name: /^Sample TAT: / }));
    expect(await screen.findByText("FP-0007")).toBeTruthy();
    expect(kpiDashboardService.records).toHaveBeenCalledWith("sample-tat", expect.objectContaining({ page: 1, pageSize: 50 }));
  });

  it("opens the tests behind a workload row for that analyst", async () => {
    renderTab();
    await userEvent.click(await screen.findByRole("button", { name: "Show tests for Amy Analyst" }));
    expect(await screen.findByText("FP-0007")).toBeTruthy();
    expect(kpiWorkloadService.workloadTests).toHaveBeenCalledWith(expect.objectContaining({ analystId: 1, page: 1, pageSize: 50 }));
  });

  it("a workload failure leaves the tiles on screen", async () => {
    vi.mocked(kpiWorkloadService.workload).mockRejectedValue({ response: { data: { message: "Workload refused" } } });
    renderTab();
    expect(await screen.findByRole("button", { name: /^Sample TAT: / })).toBeTruthy();
    expect(await screen.findByText("Workload refused")).toBeTruthy();
    expect(screen.queryByText("Could not be calculated")).toBeNull();
  });

  it("shows one failed KPI as an error tile while the others render", async () => {
    const entries = allEntries();
    entries[0] = entry({ key: "sample-tat", name: "Sample TAT", error: "Query timed out" }, null);
    vi.mocked(kpiDashboardService.dashboard).mockResolvedValue(dashboard(entries));
    renderTab();
    expect(await screen.findByText("Could not be calculated")).toBeTruthy();
    expect(screen.getByText("Query timed out")).toBeTruthy();
    expect(screen.getByRole("button", { name: /^Right first time: / })).toBeTruthy();
  });

  it("shows a dashboard failure with a retry that recovers", async () => {
    vi.mocked(kpiDashboardService.dashboard)
      .mockRejectedValueOnce({ response: { data: { message: "Dashboard unavailable" } } })
      .mockResolvedValue(dashboard(allEntries()));
    renderTab();
    expect(await screen.findByText("Dashboard unavailable")).toBeTruthy();
    expect(screen.queryByRole("button", { name: /^Sample TAT: / })).toBeNull();
    await userEvent.click(screen.getByRole("button", { name: "Retry" }));
    expect(await screen.findByRole("button", { name: /^Sample TAT: / })).toBeTruthy();
    expect(kpiDashboardService.dashboard).toHaveBeenCalledTimes(2);
  });

  it("fetches again when the period changes", async () => {
    renderTab();
    await screen.findByRole("button", { name: /^Sample TAT: / });
    fireEvent.change(screen.getByLabelText("From"), { target: { value: "2020-01-01" } });
    await waitFor(() => expect(kpiDashboardService.dashboard).toHaveBeenCalledTimes(2));
    expect(vi.mocked(kpiDashboardService.dashboard).mock.calls[1][0]).toEqual(expect.objectContaining({ from: "2020-01-01" }));
    expect(vi.mocked(kpiWorkloadService.workload).mock.calls[1][0]).toEqual(expect.objectContaining({ from: "2020-01-01" }));
  });

  it("opens the definitions drawer and the workflow profiles dialog", async () => {
    renderTab();
    await screen.findByRole("button", { name: /^Sample TAT: / });
    await userEvent.click(screen.getByRole("button", { name: "Definitions" }));
    expect(await screen.findByRole("button", { name: "Close definitions" })).toBeTruthy();
    await userEvent.click(screen.getByRole("button", { name: "Close definitions" }));
    await userEvent.click(await screen.findByRole("button", { name: "Workflow Profiles" }));
    expect(await screen.findByText("profiles dialog")).toBeTruthy();
  });

  it("enables the PDF export only once the figures are loaded, then prints them", async () => {
    const open = vi.spyOn(window, "open").mockReturnValue(null);
    renderTab();
    const button = () => screen.getByRole("button", { name: "Export PDF" }) as HTMLButtonElement;
    expect(button().disabled).toBe(true);
    await waitFor(() => expect(button().disabled).toBe(false));
    await userEvent.click(button());
    expect(open).toHaveBeenCalledTimes(1);
  });

  it("prints the workload failure note when exporting after the workload request failed", async () => {
    vi.mocked(kpiWorkloadService.workload).mockRejectedValue({ response: { data: { message: "Workload refused" } } });
    const doc = { open: vi.fn(), write: vi.fn(), close: vi.fn() };
    vi.spyOn(window, "open").mockReturnValue({ document: doc } as unknown as Window);
    renderTab();
    await screen.findByText("Workload refused");
    const button = screen.getByRole("button", { name: "Export PDF" }) as HTMLButtonElement;
    await waitFor(() => expect(button.disabled).toBe(false));
    await userEvent.click(button);
    expect(String(doc.write.mock.calls[0][0])).toContain("Workload could not be loaded: Workload refused");
  });
});
```

- [ ] **Step 2: Run to see it fail**

```bash
cd frontend && npx vitest run src/modules/kpi/components/KpiPerformanceTab.test.tsx
```
Expected: FAIL "Failed to resolve import ./KpiPerformanceTab".

- [ ] **Step 3: Implement**

Create `frontend/src/modules/kpi/components/KpiPerformanceTab.tsx`:
```tsx
import { useEffect, useMemo, useState } from "react";
import { Alert, Box, Button, Grid, LinearProgress, Typography } from "@mui/material";
import { useAuth } from "../../../contexts/AuthContext";
import { kpiDashboardService } from "../services/kpiDashboardService";
import { kpiWorkloadService } from "../services/kpiWorkloadService";
import { useKpiDashboard } from "../hooks/useKpiDashboard";
import { useKpiWorkload } from "../hooks/useKpiWorkload";
import type { KpiDefinition, KpiEntry, KpiRecordPage } from "../types/kpiTypes";
import { exportKpiPdf } from "../utils/exportKpiPdf";
import { defaultFilters, toQueryParams } from "../utils/kpiFilters";
import type { KpiFiltersValue } from "../utils/kpiFilters";
import { DefinitionsDrawer } from "./DefinitionsDrawer";
import { KpiFilters } from "./KpiFilters";
import { KpiRecordsDrawer } from "./KpiRecordsDrawer";
import { KpiSection } from "./KpiSection";
import { KpiTile } from "./KpiTile";
import { WorkflowProfilesDialog } from "./WorkflowProfilesDialog";
import { WorkloadTable } from "./WorkloadTable";

// The sections regroup the registry by topic (spec section 5); the backend's
// Group says "Headline" for the eight tiles, several of which also trend here.
const FLOW_KEYS = ["stage-tat", "backlog-aging"];
const QUALITY_KEYS = ["right-first-time", "retest-rate", "oos-rate", "returns-by-test"];
const MICRO_KEYS = ["em-excursion-rate", "incubation-reads-on-time"];

const pick = (entries: KpiEntry[], keys: string[]) =>
  keys.map((k) => entries.find((e) => e.key === k)).filter((e): e is KpiEntry => e !== undefined);

interface RecordsTarget {
  title: string;
  load: (page: number, pageSize: number) => Promise<KpiRecordPage>;
}

export function KpiPerformanceTab() {
  const { fullName } = useAuth();
  const [filters, setFilters] = useState<KpiFiltersValue>(() => defaultFilters());
  const dash = useKpiDashboard(filters);
  const workload = useKpiWorkload(filters);

  const [definitionsOpen, setDefinitionsOpen] = useState(false);
  const [profilesOpen, setProfilesOpen] = useState(false);
  const [records, setRecords] = useState<RecordsTarget | null>(null);
  const [definitions, setDefinitions] = useState<Map<string, KpiDefinition>>(new Map());

  // Hover text for the tiles. The hover is a convenience: if this call fails the
  // tiles simply have no tooltip, and the Definitions drawer reports its own failure.
  useEffect(() => {
    let live = true;
    kpiDashboardService.definitions(filters.from, filters.to)
      .then((d) => { if (live) setDefinitions(new Map(d.kpis.map((k) => [k.key, k]))); })
      .catch(() => { if (live) setDefinitions(new Map()); });
    return () => { live = false; };
  }, [filters.from, filters.to]);

  // Memoised on dash.data so the empty fallback is not a new array every render (react-hooks/exhaustive-deps).
  const entries = useMemo(() => dash.data?.kpis ?? [], [dash.data]);
  const headline = useMemo(
    () => entries.filter((k) => k.group === "Headline").sort((a, b) => a.order - b.order),
    [entries]
  );

  const openRecords = (key: string) => {
    const name = entries.find((k) => k.key === key)?.name ?? key;
    setRecords({
      title: `${name} - records`,
      load: (page, pageSize) => kpiDashboardService.records(key, { ...toQueryParams(filters), page, pageSize })
    });
  };

  const openTests = (userId: number) => {
    const name = workload.data?.rows.find((r) => r.userId === userId)?.fullName ?? "Analyst";
    setRecords({
      title: `${name} - tests`,
      load: (page, pageSize) => kpiWorkloadService.workloadTests({ ...toQueryParams(filters), analystId: userId, page, pageSize })
    });
  };

  const exportDisabled = !dash.data || dash.loading || workload.loading;

  return (
    <Box sx={{ pb: 4 }}>
      <KpiFilters
        value={filters}
        onChange={setFilters}
        onOpenDefinitions={() => setDefinitionsOpen(true)}
        onOpenProfiles={() => setProfilesOpen(true)}
        onExport={() => { if (dash.data) exportKpiPdf(dash.data, workload.data, filters, fullName || "Authorized User", workload.error); }}
        exportDisabled={exportDisabled}
      />

      {dash.loading && <LinearProgress sx={{ mb: 1 }} />}
      {dash.error && (
        <Alert severity="error" sx={{ mb: 2 }} action={<Button color="inherit" size="small" onClick={() => void dash.reload()}>Retry</Button>}>
          {dash.error}
        </Alert>
      )}

      {dash.data && (
        <Box sx={{ opacity: dash.loading ? 0.6 : 1 }}>
          <Typography sx={{ fontSize: 12, color: "text.secondary", mb: 1.5 }}>
            Calculated {new Date(dash.data.generatedAt).toLocaleString()} for your laboratory sections. Click any figure to see the records behind it.
          </Typography>

          <Grid container spacing={2} sx={{ mb: 3 }}>
            {headline.map((e) => (
              <Grid key={e.key} size={{ xs: 12, sm: 6, md: 3 }}>
                <KpiTile entry={e} definition={definitions.get(e.key)} onOpenRecords={openRecords} />
              </Grid>
            ))}
          </Grid>

          <KpiSection title="Flow" entries={pick(entries, FLOW_KEYS)} onOpenRecords={openRecords} />
          <KpiSection title="Quality" entries={pick(entries, QUALITY_KEYS)} onOpenRecords={openRecords} />
          <KpiSection title="Micro" entries={pick(entries, MICRO_KEYS)} onOpenRecords={openRecords} />
        </Box>
      )}

      {/* The workload block has its own request and its own error: a refusal here must not blank the tiles above. */}
      <WorkloadTable data={workload.data} loading={workload.loading} error={workload.error} onOpenTests={openTests} />

      <DefinitionsDrawer open={definitionsOpen} onClose={() => setDefinitionsOpen(false)} from={filters.from} to={filters.to} />
      <WorkflowProfilesDialog open={profilesOpen} onClose={() => setProfilesOpen(false)} />
      {records && <KpiRecordsDrawer title={records.title} onClose={() => setRecords(null)} load={records.load} />}
    </Box>
  );
}
```

- [ ] **Step 4: Run to see it pass**

```bash
cd frontend && npx vitest run src/modules/kpi/components/KpiPerformanceTab.test.tsx && npx tsc --noEmit -p tsconfig.json
```
Expected: 10 tests PASS; `tsc` clean. Troubleshooting notes: (a) the headline `Sample TAT` also appears as a Flow-section heading? It does not: only `stage-tat`, `backlog-aging`, quality and micro keys are picked, and tiles are buttons while panel titles are headings, so `getByRole("button", { name: /^Retest rate: / })` stays unique. (b) In "fetches again when the period changes" both mocks must have been called exactly twice by then; if the definitions effect also refires that is a third service, not counted.

- [ ] **Step 5: Commit**

```bash
cd /e/MicroLIMS/MicroLIMS && git add frontend/src/modules/kpi/components/KpiPerformanceTab.tsx frontend/src/modules/kpi/components/KpiPerformanceTab.test.tsx
git commit -F - <<'EOF'
feat(kpi): KpiPerformanceTab composing tiles, sections, workload and drawers

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
EOF
```

---

### Task 10: Mount the new tab in Reports and gate it by Kpi.View

**Files:**
- Modify: `frontend/src/pages/Reports.tsx` (whole file, 140 lines; it has no other uncommitted changes)
- Create: `frontend/src/pages/Reports.test.tsx`
- Modify: `frontend/src/modules/reports/components/OverviewTab.tsx` (one optional prop and one conditional around the "KPI / Performance" shortcut button at lines 504-512; check `git diff` first, use `git add -p` if other hunks show)

**Interfaces:**
- Consumes: `KpiPerformanceTab` (Task 9); `useHasPermission` (`frontend/src/contexts/AuthContext.tsx:198`); `PERMISSIONS.KPI_VIEW` (`frontend/src/routes/routes.ts:241`, existing).
- Produces: `ReportsPage` shows the "KPI / Performance" tab only to holders of `Kpi.View`; `?tab=performance` without it falls back to Overview; the page-level quick-period selector is hidden on the KPI tab (that tab owns its filters). `OverviewTab` gains an optional `canViewKpi?: boolean` prop (default `true`); when `false` its "KPI / Performance" shortcut (`onNavigateTab(5)`) is not rendered, so a user without `Kpi.View` never sees a dead button that would land on Trending.

- [ ] **Step 1: Write the failing test**

Create `frontend/src/pages/Reports.test.tsx`:
```tsx
import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { PinnedLightTheme } from "../theme/PinnedLightTheme";
import { ReportsPage } from "./Reports";

const state = vi.hoisted(() => ({ canViewKpi: true }));

vi.mock("../contexts/AuthContext", () => ({ useHasPermission: () => state.canViewKpi }));
vi.mock("../modules/reports/components/OverviewTab", async () => {
  const { createElement } = await import("react");
  // Mirrors the real prop: the KPI shortcut is only offered when canViewKpi is not false.
  return {
    OverviewTab: ({ canViewKpi }: { canViewKpi?: boolean }) =>
      createElement("div", null, "overview tab", canViewKpi === false ? null : createElement("button", null, "KPI shortcut"))
  };
});
vi.mock("../modules/reports/RecordSearchTab", async () => {
  const { createElement } = await import("react");
  return { RecordSearchTab: () => createElement("div", null, "search tab") };
});
vi.mock("../modules/reports/MediaGptReportTab", async () => {
  const { createElement } = await import("react");
  return { MediaGptReportTab: () => createElement("div", null, "media tab") };
});
vi.mock("../modules/reports/ReferenceStrainReportTab", async () => {
  const { createElement } = await import("react");
  return { ReferenceStrainReportTab: () => createElement("div", null, "strain tab") };
});
vi.mock("../modules/reports/components/TrendingTab", async () => {
  const { createElement } = await import("react");
  return { TrendingTab: () => createElement("div", null, "trending tab") };
});
vi.mock("../modules/kpi/components/KpiPerformanceTab", async () => {
  const { createElement } = await import("react");
  return { KpiPerformanceTab: () => createElement("div", null, "kpi tab") };
});

afterEach(cleanup);

function renderAt(url: string) {
  render(
    <PinnedLightTheme>
      <MemoryRouter initialEntries={[url]}><ReportsPage /></MemoryRouter>
    </PinnedLightTheme>
  );
}

describe("ReportsPage KPI tab", () => {
  it("offers and mounts the KPI / Performance tab to a holder of Kpi.View", () => {
    state.canViewKpi = true;
    renderAt("/reports?tab=performance");
    expect(screen.getByRole("tab", { name: "KPI / Performance" })).toBeTruthy();
    expect(screen.getByText("kpi tab")).toBeTruthy();
  });

  it("hides the tab and falls back to Overview without Kpi.View", () => {
    state.canViewKpi = false;
    renderAt("/reports?tab=performance");
    expect(screen.queryByRole("tab", { name: "KPI / Performance" })).toBeNull();
    expect(screen.queryByText("kpi tab")).toBeNull();
    expect(screen.getByText("overview tab")).toBeTruthy();
  });

  it("offers the Overview KPI shortcut only to a holder of Kpi.View", () => {
    state.canViewKpi = true;
    renderAt("/reports?tab=overview");
    expect(screen.getByRole("button", { name: "KPI shortcut" })).toBeTruthy();
    cleanup();
    state.canViewKpi = false;
    renderAt("/reports?tab=overview");
    expect(screen.queryByRole("button", { name: "KPI shortcut" })).toBeNull();
  });

  it("hides the page-level period selector on the KPI tab, which owns its filters, and keeps it elsewhere", () => {
    state.canViewKpi = true;
    renderAt("/reports?tab=performance");
    expect(screen.queryByText("Last 7 Days")).toBeNull();
    cleanup();
    renderAt("/reports?tab=overview");
    expect(screen.getByText("Last 7 Days")).toBeTruthy();
  });
});
```

- [ ] **Step 2: Run to see it fail**

```bash
cd frontend && npx vitest run src/pages/Reports.test.tsx
```
Expected: FAIL: the first test cannot find `kpi tab` (the page still mounts `AnalystKpiTab`, and `Reports.tsx` does not import `useHasPermission`); the module mock for `KpiPerformanceTab` is unused.

- [ ] **Step 3: Implement**

Replace the whole content of `frontend/src/pages/Reports.tsx` with:
```tsx
import { useMemo, useState, useEffect } from "react";
import { useSearchParams } from "react-router-dom";
import { Tabs, Tab, Box } from "@mui/material";
import { PageHeader } from "../components/PageHeader";
import { useHasPermission } from "../contexts/AuthContext";
import { PERMISSIONS } from "../routes/routes";
import { QuickPeriodSelector } from "../modules/reports/components/QuickPeriodSelector";
import { OverviewTab } from "../modules/reports/components/OverviewTab";
import { RecordSearchTab } from "../modules/reports/RecordSearchTab";
import { MediaGptReportTab } from "../modules/reports/MediaGptReportTab";
import { ReferenceStrainReportTab } from "../modules/reports/ReferenceStrainReportTab";
import { TrendingTab } from "../modules/reports/components/TrendingTab";
import { KpiPerformanceTab } from "../modules/kpi/components/KpiPerformanceTab";
import { QuickPeriod, computeQuickPeriodRange, toDateInputValue } from "../modules/reports/utils/dateRange";

const TABS = [
  "Overview",
  "Record Search",
  "Media / GPT",
  "Reference Strains",
  "Trending & Analysis",
  "KPI / Performance"
] as const;

const TAB_KEYS = ["overview", "search", "media-gpt", "reference-strains", "trending", "performance"] as const;

// The KPI tab is the last entry. It owns its filters, so the page-level period selector is hidden there.
const PERFORMANCE_TAB = 5;

export function ReportsPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const tabParam = searchParams.get("tab")?.toLowerCase();

  // Without Kpi.View the KPI tab is dropped from both lists, so a link to
  // ?tab=performance falls back to Overview. The server enforces the permission too.
  const canViewKpi = useHasPermission(PERMISSIONS.KPI_VIEW);
  const tabs: readonly string[] = canViewKpi ? TABS : TABS.slice(0, PERFORMANCE_TAB);
  const tabKeys = useMemo<readonly string[]>(
    () => (canViewKpi ? TAB_KEYS : TAB_KEYS.slice(0, PERFORMANCE_TAB)),
    [canViewKpi]
  );

  const initialTab = useMemo(() => {
    if (!tabParam) return 0; // Overview active by default
    const idx = tabKeys.indexOf(tabParam);
    return idx >= 0 ? idx : 0;
  }, [tabParam, tabKeys]);

  const [tab, setTab] = useState(initialTab);

  useEffect(() => {
    if (tabParam) {
      const idx = tabKeys.indexOf(tabParam);
      const safeTab = idx >= 0 ? idx : 0;
      if (safeTab !== tab) {
        setTab(safeTab);
      }
      if (idx < 0) {
        setSearchParams({ tab: tabKeys[0] }, { replace: true });
      }
    }
  }, [tabParam, tab, tabKeys, setSearchParams]);

  const handleTabChange = (newTab: number) => {
    const safeTab = Math.max(0, Math.min(newTab, tabKeys.length - 1));
    setTab(safeTab);
    setSearchParams({ tab: tabKeys[safeTab] });
  };

  const [period, setPeriod] = useState<QuickPeriod>("30d");
  const defaultRange = useMemo(() => computeQuickPeriodRange("30d"), []);
  const [customFrom, setCustomFrom] = useState(toDateInputValue(new Date(Date.now() - 30 * 86400000)));
  const [customTo, setCustomTo] = useState(toDateInputValue(new Date()));

  // Cross-tab interaction state for Trending drill-down
  const [trendParams, setTrendParams] = useState<{ testCode?: string; subjectName?: string }>({});

  const range = useMemo(() => {
    if (period === "custom") {
      return { fromDate: customFrom ? `${customFrom}T00:00:00.000Z` : undefined, toDate: customTo ? `${customTo}T23:59:59.999Z` : undefined };
    }
    return computeQuickPeriodRange(period);
  }, [period, customFrom, customTo, defaultRange]);

  const handleAnalyzeTrend = (testCode: string, subjectName: string) => {
    setTrendParams({ testCode, subjectName });
    handleTabChange(4); // Switch to Trending & Analysis
  };

  return (
    <>
      <PageHeader title="Reports" subtitle="Search, trend, and export laboratory results." />

      {tab !== PERFORMANCE_TAB && (
        <QuickPeriodSelector
          period={period}
          customFrom={customFrom}
          customTo={customTo}
          onPeriodChange={setPeriod}
          onCustomChange={(from, to) => { setCustomFrom(from); setCustomTo(to); }}
        />
      )}

      <Box sx={{ borderBottom: 1, borderColor: "divider", mb: 2 }}>
        <Tabs
          value={tab}
          onChange={(_, v) => handleTabChange(v)}
          variant="scrollable"
          scrollButtons="auto"
          sx={{ "& .Mui-selected": { fontWeight: 700 } }}
        >
          {tabs.map((label) => <Tab key={label} label={label} />)}
        </Tabs>
      </Box>

      {tab === 0 && (
        <OverviewTab
          fromDate={range.fromDate}
          toDate={range.toDate}
          onNavigateTab={handleTabChange}
          canViewKpi={canViewKpi}
        />
      )}

      {tab === 1 && (
        <RecordSearchTab
          fromDate={range.fromDate}
          toDate={range.toDate}
          onAnalyzeTrend={handleAnalyzeTrend}
          onNavigateTab={handleTabChange}
        />
      )}

      {tab === 2 && (
        <MediaGptReportTab
          fromDate={range.fromDate}
          toDate={range.toDate}
        />
      )}

      {tab === 3 && (
        <ReferenceStrainReportTab />
      )}

      {tab === 4 && (
        <TrendingTab
          initialTestCode={trendParams.testCode}
          initialSubjectName={trendParams.subjectName}
        />
      )}

      {tab === PERFORMANCE_TAB && canViewKpi && (
        <KpiPerformanceTab />
      )}
    </>
  );
}
```

- [ ] **Step 4: Run to see it pass**

```bash
cd frontend && npx vitest run src/pages/Reports.test.tsx
```
Expected: 4 tests PASS. Do not run `tsc` yet: `Reports.tsx` passes `canViewKpi` to `OverviewTab`, whose props do not declare it until step 4b, so `tsc` would report TS2322 here. It runs at the end of step 4b.

- [ ] **Step 4b: Hide the Overview shortcut without Kpi.View**

In `frontend/src/modules/reports/components/OverviewTab.tsx` add `canViewKpi?: boolean;` to `OverviewTabProps` (next to `onNavigateTab`, line 53), destructure it as `canViewKpi = true` in `OverviewTab({ fromDate, toDate, onNavigateTab, canViewKpi = true })` (line 94), and wrap the last shortcut `<Button ... onClick={() => onNavigateTab(5)} ...>KPI / Performance</Button>` (lines 504-512) in `{canViewKpi && ( ... )}`. Then:
```bash
cd frontend && npx vitest run src/pages/Reports.test.tsx src/modules/reports && npx tsc --noEmit -p tsconfig.json
```
Expected: PASS and `tsc` prints nothing (existing `OverviewTab` callers that omit the prop keep the button).

- [ ] **Step 5: Commit**

```bash
cd /e/MicroLIMS/MicroLIMS && git add frontend/src/pages/Reports.tsx frontend/src/pages/Reports.test.tsx frontend/src/modules/reports/components/OverviewTab.tsx
git diff --cached --name-only
git commit -F - <<'EOF'
feat(kpi): mount the new KPI tab in Reports behind Kpi.View

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
EOF
```

---

### Task 11: Remove the old frontend KPI code

**Files:**
- Delete: `frontend/src/modules/reports/components/AnalystKpiTab.tsx` (uncommitted 2026-10-11 patch included)
- Delete: `frontend/src/modules/reports/services/AnalystKpiService.ts` (same)
- Delete: `frontend/src/modules/reports/services/kpiDtos.ts`
- Delete: `frontend/src/modules/reports/utils/exportKpi.ts`
- Delete if present: `frontend/src/modules/reports/components/WorkloadWeightsDialog.tsx` (S1 removes it)
- Modify: `frontend/src/modules/reports/types/reportingTypes.ts` (remove lines 293 to the end; keep lines 1 to 292, which include `OverviewKpiCardData`)
- Modify: `frontend/src/modules/reports/services/OverviewService.ts:5` (one comment line)

**Interfaces:**
- Consumes: Task 10 (nothing imports `AnalystKpiTab` any more).
- Produces: no frontend file refers to the old KPI types or to any removed `/kpi/...` route.

- [ ] **Step 1: Prove nothing live still uses the old code**

```bash
cd /e/MicroLIMS/MicroLIMS
git grep -n -E "AnalystKpiService|kpiDtos|exportKpi[^P]|AnalystKpiTab|WorkloadWeightsDialog|AnalystKpiFilters|AnalystComparisonRow|AnalystPerformanceDashboardData|AnalystPerformanceDetail|WorkloadWeightConfig|SampleSlaSummary|StepViolationSummary|SectionWorkflowBottleneck|StageTatSummary" -- frontend/src
git grep -n -E "kpi/(analysts|workload-weights|completion-stats|delay-tracking|sample-assignment|overall-on-time|return-to-analyst|sample-queue|stage-tat|testing-tat|step-violations|workflow-bottleneck)" -- frontend/src
```
Expected: the first command lists only the files about to be deleted, `reportingTypes.ts` (the types to cut) and the comment in `OverviewService.ts`; the second lists only `AnalystKpiService.ts`. Anything else is a live consumer: stop and report it.

- [ ] **Step 2: Delete the files and cut the types**

```bash
cd /e/MicroLIMS/MicroLIMS
git rm -f frontend/src/modules/reports/components/AnalystKpiTab.tsx frontend/src/modules/reports/services/AnalystKpiService.ts frontend/src/modules/reports/services/kpiDtos.ts frontend/src/modules/reports/utils/exportKpi.ts
git rm -f --ignore-unmatch frontend/src/modules/reports/components/WorkloadWeightsDialog.tsx
sed -n 288,296p frontend/src/modules/reports/types/reportingTypes.ts
```
Check that line 292 is the closing `}` of `QualitativeEventResult` and line 294 starts the `// ----` banner above "Analyst KPI & Performance Types". Then:
```bash
cd /e/MicroLIMS/MicroLIMS/frontend
head -n 292 src/modules/reports/types/reportingTypes.ts > "$TEMP/reportingTypes.ts.new" && mv "$TEMP/reportingTypes.ts.new" src/modules/reports/types/reportingTypes.ts
tail -n 5 src/modules/reports/types/reportingTypes.ts
```
Expected: the file now ends with the `}` of `QualitativeEventResult`.

- [ ] **Step 3: Fix the comment that names the deleted tab**

Edit `frontend/src/modules/reports/services/OverviewService.ts`, line 5 only:

old: `// Exported so other Reports tabs (e.g. AnalystKpiTab's category filter and`
new: `// Exported so other Reports tabs (e.g. the KPI tab's category filter and`

- [ ] **Step 4: Type-check and run the whole frontend suite**

```bash
cd /e/MicroLIMS/MicroLIMS/frontend && npx tsc --noEmit -p tsconfig.json && npm test
```
Expected: `tsc` prints nothing; every test file passes (the pre-existing 31 files plus the new KPI and Reports tests). If `tsc` reports a now-unused import in `reportingTypes.ts` (for example `SampleCategory` is still used above line 292, so none is expected), remove only that import.

- [ ] **Step 5: Commit**

```bash
cd /e/MicroLIMS/MicroLIMS
git add frontend/src/modules/reports/types/reportingTypes.ts frontend/src/modules/reports/services/OverviewService.ts
git status --short -- frontend/src/modules/reports/components/AnalystKpiTab.tsx frontend/src/modules/reports/services/AnalystKpiService.ts frontend/src/modules/reports/services/kpiDtos.ts frontend/src/modules/reports/utils/exportKpi.ts
git commit -F - <<'EOF'
refactor(kpi): remove the old Analyst KPI tab, service, DTOs and PDF export

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
EOF
```
Expected: the `git status` line shows the four deletions as staged (`D `), and the commit contains them plus the two modified files. `routes.ts` and the other dirty files stay unstaged.

---

### Task 12: Migrate the surviving KPI test coverage before anything is deleted

**Files:**
- Create: `backend/MicroLIMS.Tests/UnitTests/Kpi/KpiDashboardFilterTests.cs`
- Modify: `backend/MicroLIMS.Tests/WorkflowTests/ReturnToAnalystTests.cs:655-681` (delete one test method)
- Modify: `backend/MicroLIMS.Tests/WorkflowTests/MultiSectionReviewApprovalTests.cs:398-417` (`Kpis_CountOnlyTheViewersSections`, rewritten onto the dashboard and workload services)
- Modify: `backend/MicroLIMS.Tests/IntegrationTests/SectionScopedQueriesPostgresIntegrationTests.cs:51-63` (the old `kpi.*` block, replaced by every registry KPI plus the workload service)
- No change to `backend/MicroLIMS.Tests/TestServiceFactory.cs`: S2 already exposes `BuildKpiRegistry(db, time)`

**Interfaces:**
- Consumes (S3): `TestServiceFactory.AnalystWorkload(db, time)` returning `AnalystWorkloadService` (`GetAsync(KpiQuery, ct)`, `GetTestsAsync(KpiQuery, page, pageSize, ct)`); `KpiQuery(From, To, SectionIds, Category, Location, TestCode, AnalystId, Now, ViewerUserId, CanViewTeam)`; `IKpi.ComputeAsync` / `GetRecordsAsync`; `KpiRegistry.All`.
- Consumes (S2): `TestServiceFactory.KpiDashboard(MicroLimsDbContext db, TimeProvider? time = null)` returning `KpiDashboardService`; `KpiDashboardService.GetDashboardAsync(int userId, bool canViewTeam, DateOnly from, DateOnly to, string? category, string? location, string? testCode, int? analystId, CancellationToken ct = default)` returning `KpiDashboardDto { Kpis: IReadOnlyList<KpiEntryDto> }`; `KpiEntryDto { Key, Result (KpiResult?), Error (string?) }`; `KpiResult.Value (decimal?)`; the registry key `wip-backlog` (open tests: `Pending` or `InProgress`, not superseded, per contract section 3.3 and deviation 7). Existing: `IUserSectionScopeService` returns `null` (unrestricted) only for a `RoleType.SystemAdministrator` user.
- Existing and unchanged: `TestServiceFactory.SampleReview(db)`, `SeedCompletedTamcOrderAsync(db)` in `ReturnToAnalystTests`.
- Produces: tests only. Mapping of the retired `KpiService` tests (record it in the commit message body):

| Retired test | Now covered by |
|---|---|
| `GetCompletionStatsAsync_FiltersCorrectlyByCategoryLocationAndTestCode` | `KpiDashboardFilterTests` (this task), on `wip-backlog` |
| `GetStageTatSummaryAsync_...` | S2 `KpiTileTests` for `stage-tat` (checked in step 1) |
| `GetStepViolationsAsync_...` | S2 `KpiTileTests` for `incubation-reads-on-time` (checked in step 1) |
| `GetReturnToAnalystCountAsync_FiltersCorrectlyByDateRangeAndAnalyst` | the S2 right-first-time boundary and analyst-filter cases required in step 1 |
| `ReturnToAnalystTests.KpiService_GetReturnToAnalystCountAsync_IncrementsWhenReturnToAnalystExecuted` | retargeted in step 4 as `RightFirstTimeKpi_ExcludesATestReturnedByTheRealReturnPath` (real `ReturnToAnalystAsync`, then the `right-first-time` records and the analyst filter) |
| `MultiSectionReviewApprovalTests.Kpis_CountOnlyTheViewersSections` | rewritten in step 4b on `wip-backlog` (per section head) and `AnalystWorkloadService` rows |
| `SectionScopedQueriesPostgresIntegrationTests` `kpi.*` block (12 old methods) | step 4c: every registry KPI (`ComputeAsync`, `GetRecordsAsync`) with a real section list, `AnalystWorkloadService`, `KpiDashboardService`, and the surviving `GetOverdueAnalystStageSamplesAsync` |
| `GetSampleQueueCountsAsync_...`, `GetDelayTrackingAsync_...` | the 24 h delay count is retired with the metric. The queue counts' section scoping of the review gate is carried by `backlog-aging` "In Review" / "In Approval", asserted in step 4b (`Kpis_CountOnlyTheViewersSections`) |
| `GetOverdueAnalystStageSamplesAsync_RegressionCheck_...` | stays in `KpiFilteringTests` (Task 14) |

- [ ] **Step 1: Confirm S2 already covers the KPIs the other retired tests exercised**

```bash
cd /e/MicroLIMS/MicroLIMS
git grep -n "static KpiService Kpi" -- backend/MicroLIMS.Tests/TestServiceFactory.cs
git grep -n "TestServiceFactory.Kpi(" -- backend/MicroLIMS.Tests
git grep -n "new KpiService(" -- backend/MicroLIMS.Tests
git grep -n -E "stage-tat|incubation-reads-on-time|right-first-time" -- backend/MicroLIMS.Tests/UnitTests/Kpi backend/MicroLIMS.Tests/IntegrationTests
git grep -n -E "TestReturnEvent" -- backend/MicroLIMS.Tests/UnitTests/Kpi
```
Expected: the first command confirms the factory (`public static KpiService Kpi(MicroLimsDbContext db) => new(db);` at `TestServiceFactory.cs:164`; that line does not contain the string `TestServiceFactory.Kpi(`). The second lists exactly three test files: `ReturnToAnalystTests.cs`, `MultiSectionReviewApprovalTests.cs` and `SectionScopedQueriesPostgresIntegrationTests.cs`; any other caller is a further file this task must also migrate. The third lists only `KpiFilteringTests.cs` and `WorkloadWeightsTests.cs` (both handled in Task 14); the factory does not appear because it uses `new(db)`. A hit for a key only proves the key is mentioned, so also require these named cases in the S2 tests (read the test bodies, do not just count grep hits):
- right-first-time: a `TestReturnEvent` just inside and one just outside the period boundary, and an analyst-filtered query that excludes another analyst's return;
- incubation-reads-on-time: a read at exactly `ExpectedReadingAt` + 4 h (`IncubationGraceHours`) counted on time, and one a minute later counted late;
- stage-tat: a median over an even number of records (average of the two middle values) and a P90.

If any named case is absent, STOP the deletion: add that case to the S2 test file (`KpiTileTests.cs` or the file that owns the key) as a new test in this task's commit, run it, and only then continue. Do not just report it and delete.

- [ ] **Step 2: Write the migrated filter test**

This is a characterization test: the behaviour already exists in S2, so the first run is expected to pass.

Create `backend/MicroLIMS.Tests/UnitTests/Kpi/KpiDashboardFilterTests.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services.Kpi;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.UnitTests.Kpi;

// The category / location / test-code filters that the retired
// KpiService.GetCompletionStatsAsync test pinned, asserted now on the
// registry's wip-backlog tile (open = Pending or InProgress, not superseded).
public class KpiDashboardFilterTests
{
    private static MicroLimsDbContext NewDb() =>
        new(new DbContextOptionsBuilder<MicroLimsDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    // Sample A (finished product, "Product A"): one Approved and one Rejected TAMC test - nothing open.
    // Sample B (raw material, "Product B"): one Approved and one Pending TYMC test - one open.
    private static async Task<MicroLimsDbContext> SeedAsync()
    {
        var db = NewDb();

        // An administrator is unrestricted, so section scope does not narrow the figures under test.
        db.Roles.Add(new Role { Id = 1, Type = RoleType.SystemAdministrator, Name = "System Administrator" });
        db.Users.Add(new User { Id = 1, FullName = "Admin", Username = "admin", RoleId = 1, PasswordHash = "x" });

        var itemA = new Item { Id = 101, Name = "Product A", Code = "PA-01" };
        var itemB = new Item { Id = 102, Name = "Product B", Code = "PB-01" };
        db.Items.AddRange(itemA, itemB);

        var sampleA = new Sample
        {
            Id = 1, Category = SampleCategory.FinishedProduct, Item = itemA, ItemId = itemA.Id,
            ControlNumber = "FP-001", Status = SampleStatus.Approved
        };
        sampleA.TestOrders.Add(new TestOrder { Id = 1, TestCode = "TAMC", Status = ApprovalStatus.Approved, CurrentStep = WorkflowStep.Approved });
        sampleA.TestOrders.Add(new TestOrder { Id = 2, TestCode = "TAMC", Status = ApprovalStatus.Rejected, CurrentStep = WorkflowStep.Approved });

        var sampleB = new Sample
        {
            Id = 2, Category = SampleCategory.RawMaterial, Item = itemB, ItemId = itemB.Id,
            ControlNumber = "RM-001", Status = SampleStatus.InTesting
        };
        sampleB.TestOrders.Add(new TestOrder { Id = 3, TestCode = "TYMC", Status = ApprovalStatus.Approved, CurrentStep = WorkflowStep.Approved });
        sampleB.TestOrders.Add(new TestOrder { Id = 4, TestCode = "TYMC", Status = ApprovalStatus.Pending, CurrentStep = WorkflowStep.Waiting });

        db.Samples.AddRange(sampleA, sampleB);
        await db.SaveChangesAsync();
        return db;
    }

    private static async Task<decimal> OpenTestsAsync(
        MicroLimsDbContext db, string? category = null, string? location = null, string? testCode = null)
    {
        var service = TestServiceFactory.KpiDashboard(db);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var dashboard = await service.GetDashboardAsync(1, true, today.AddDays(-30), today, category, location, testCode, null);
        var entry = dashboard.Kpis.Single(k => k.Key == "wip-backlog");
        Assert.True(entry.Error is null, entry.Error);
        return entry.Result!.Value ?? 0m;
    }

    [Fact]
    public async Task WipBacklog_Unfiltered_CountsEveryOpenTest()
    {
        await using var db = await SeedAsync();
        Assert.Equal(1m, await OpenTestsAsync(db));
    }

    [Fact]
    public async Task WipBacklog_FiltersByCategory()
    {
        await using var db = await SeedAsync();
        Assert.Equal(0m, await OpenTestsAsync(db, category: "FinishedProduct"));
        Assert.Equal(1m, await OpenTestsAsync(db, category: "RawMaterial"));
    }

    [Fact]
    public async Task WipBacklog_FiltersByLocation()
    {
        await using var db = await SeedAsync();
        Assert.Equal(1m, await OpenTestsAsync(db, location: "Product B"));
        Assert.Equal(0m, await OpenTestsAsync(db, location: "Product A"));
    }

    [Fact]
    public async Task WipBacklog_FiltersByTestCode()
    {
        await using var db = await SeedAsync();
        Assert.Equal(1m, await OpenTestsAsync(db, testCode: "TYMC"));
        Assert.Equal(0m, await OpenTestsAsync(db, testCode: "TAMC"));
    }
}
```

- [ ] **Step 3: Run it**

```bash
cd /e/MicroLIMS/MicroLIMS && dotnet test backend/MicroLIMS.Tests --artifacts-path "$TEMP/kpi-art" --filter "FullyQualifiedName~KpiDashboardFilterTests"
```
Expected: 4 tests PASS. If one fails with a message from `entry.Error`, the registry KPI threw on the in-memory provider: report that as an S2 defect rather than weakening the test. If a count differs (for example the `location` filter means something else for `wip-backlog`), read `WipBacklogKpi` and correct the expected number only if contract deviation 7/9 supports it.

- [ ] **Step 4: Retarget the `ReturnToAnalystTests` KPI method at right-first-time**

The shared contract (section 5.1) says to retarget `ReturnToAnalystTests.cs:656-679` at the `right-first-time` KPI, not to delete it. The `TestReturnEvent` field assertions at lines 184-191 only prove the event row is written; this test is the only end-to-end proof that the real `ReturnToAnalystAsync` path produces a return that `RightFirstTimeKpi` reads (matched by `TestOrderId`, SPEC DEVIATION 5), and the S2 cases seed `TestReturnEvent` rows directly. Keep `SeedCompletedTamcOrderAsync` and the `ReturnToAnalystAsync` call; replace only the KPI reads.

First make the registry reachable: `git grep -n "KpiRegistry" -- backend/MicroLIMS.Tests/TestServiceFactory.cs`. S2 already added `public static KpiRegistry BuildKpiRegistry(MicroLimsDbContext db, TimeProvider? time = null)` to `TestServiceFactory` (the method cannot be called `KpiRegistry`, it would shadow the type); use that, no extraction is needed.

Replace the whole method (lines 655-681, from its `[Fact]` to its closing brace) with the text under "new_string" below, run it, and only after it passes treat the old method as gone (the old text is the old_string). Read `RightFirstTimeKpi` first: if a returned test is not in its population at all, change the first assertion to expect no record for the order instead, but keep a positive check that proves the return is visible to the KPI (for example the record is present with a non-`Numerator` inclusion, or the population row carries the return note).

old_string:
```csharp
    [Fact]
    public async Task KpiService_GetReturnToAnalystCountAsync_IncrementsWhenReturnToAnalystExecuted()
    {
        await using var db = NewDb();
        var (order, analyst, reviewer, _) = await SeedCompletedTamcOrderAsync(db);

        var fromDate = DateTime.UtcNow.AddDays(-7);
        var toDate = DateTime.UtcNow.AddDays(1);

        var kpiService = TestServiceFactory.Kpi(db);

        // Initial count for analyst is 0
        var initialCounts = await kpiService.GetReturnToAnalystCountAsync(analyst.Id, fromDate, toDate);
        Assert.Equal(0, initialCounts[analyst.Id]);

        // Reviewer returns count test order
        var reviewService = TestServiceFactory.SampleReview(db);
        await reviewService.ReturnToAnalystAsync(order.Id, reviewer.Id, "Recount required");

        // Count for analyst increments to 1
        var countsAfterReturn = await kpiService.GetReturnToAnalystCountAsync(analyst.Id, fromDate, toDate);
        Assert.Equal(1, countsAfterReturn[analyst.Id]);

        // Other analyst ID returns 0
        var otherAnalystCounts = await kpiService.GetReturnToAnalystCountAsync(999, fromDate, toDate);
        Assert.Equal(0, otherAnalystCounts[999]);
    }
```
new_string:
```csharp
    [Fact]
    public async Task RightFirstTimeKpi_ExcludesATestReturnedByTheRealReturnPath()
    {
        await using var db = NewDb();
        var (order, analyst, reviewer, _) = await SeedCompletedTamcOrderAsync(db);

        var reviewService = TestServiceFactory.SampleReview(db);
        await reviewService.ReturnToAnalystAsync(order.Id, reviewer.Id, "Recount required");

        var rft = TestServiceFactory.BuildKpiRegistry(db).Find("right-first-time")!;
        var now = DateTime.UtcNow;
        MicroLIMS.Application.Services.Kpi.KpiQuery Query(int? analystId) =>
            new(now.AddDays(-7), now.AddDays(1), null, null, null, null, analystId, now, reviewer.Id, true);

        // The returned test is visible to the KPI (matched by TestOrderId) and is not in the numerator.
        var forAnalyst = await rft.GetRecordsAsync(Query(analyst.Id), 1, 50, default);
        var row = Assert.Single(forAnalyst.Items, r => r.TestOrderId == order.Id);
        Assert.NotEqual("Numerator", row.Inclusion);

        // Another analyst's filter does not see this analyst's return.
        var forOther = await rft.GetRecordsAsync(Query(999), 1, 50, default);
        Assert.DoesNotContain(forOther.Items, r => r.TestOrderId == order.Id);
    }
```
The old method (`KpiService_GetReturnToAnalystCountAsync_IncrementsWhenReturnToAnalystExecuted`) is deleted only after this test passes; the date-range boundary and the S2 count maths stay pinned by the named S2 cases required in step 1.

- [ ] **Step 4b: Migrate `MultiSectionReviewApprovalTests.Kpis_CountOnlyTheViewersSections`**

`backend/MicroLIMS.Tests/WorkflowTests/MultiSectionReviewApprovalTests.cs:398-417` calls `GetSampleQueueCountsAsync` and `GetAnalystKpisAsync` (both deleted in Task 14). These are the only checks that a Section Head sees only their own section's KPI figures, so they are moved onto the new services, not dropped. Replace the whole method (its `[Fact]` to its closing brace, lines 398-417) with the text below. In the seed (`fpReady: false`) the Micro tests are `ResultEntered` (so not open) and the FP `ASSAY` test is `InProgress` (open), so `wip-backlog` is 0 for the Micro head and 1 for the FP head, and the analysts are 1 (Micro) and 2 (FP). The sample is in review for the Micro section only, so the `backlog-aging` "In Review" points sum to 1 for `w.HeadMicro` and 0 for `w.HeadFp` (this keeps the regression test for the review-gate section scoping that `ReviewQueueCount` used to carry; if the stage label differs, read `BacklogAgingKpi` and use its label).
```csharp
    [Fact]
    public async Task Kpis_CountOnlyTheViewersSections()
    {
        await using var db = NewDb();
        var w = await SeedAsync(db, fpReady: false);
        var review = TestServiceFactory.SampleReview(db);
        await review.AutoSubmitForReviewIfReadyAsync(w.Sample.Id, 1);
        await db.SaveChangesAsync();

        // The dashboard derives the section scope from the viewing user.
        var dashboard = TestServiceFactory.KpiDashboard(db);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        async Task<MicroLIMS.Application.Services.Kpi.KpiDashboardDto> DashFor(int userId) =>
            await dashboard.GetDashboardAsync(userId, true, today.AddDays(-30), today, null, null, null, null);

        var microDash = await DashFor(w.HeadMicro);
        var fpDash = await DashFor(w.HeadFp);
        Assert.All(microDash.Kpis.Concat(fpDash.Kpis), k => Assert.True(k.Error is null, k.Error));
        Assert.Equal(0m, microDash.Kpis.Single(k => k.Key == "wip-backlog").Result!.Value ?? 0m);
        Assert.Equal(1m, fpDash.Kpis.Single(k => k.Key == "wip-backlog").Result!.Value ?? 0m);

        // Replaces the old ReviewQueueCount check: the section-scoped review gate (SectionReviewQueues.Candidates)
        // is carried by backlog-aging "In Review". After AutoSubmitForReviewIfReadyAsync the Micro sample waits
        // for review in the Micro head's section only.
        static decimal InReview(MicroLIMS.Application.Services.Kpi.KpiDashboardDto d) =>
            d.Kpis.Single(k => k.Key == "backlog-aging").Result!.Series
                .Where(p => p.Group == "In Review").Sum(p => p.Value ?? 0m);
        Assert.Equal(1m, InReview(microDash));
        Assert.Equal(0m, InReview(fpDash));

        // Each Section Head sees only their own section's analysts.
        var scope = new UserSectionScopeService(db);
        var workload = TestServiceFactory.AnalystWorkload(db);
        async Task<List<int>> AnalystIds(int headUserId)
        {
            var now = DateTime.UtcNow;
            var q = new MicroLIMS.Application.Services.Kpi.KpiQuery(
                now.AddDays(-30), now.AddDays(1), await scope.GetAccessibleSectionIdsAsync(headUserId),
                null, null, null, null, now, headUserId, true);
            return (await workload.GetAsync(q, default)).Rows.Select(r => r.UserId).ToList();
        }
        var microAnalysts = await AnalystIds(w.HeadMicro);
        var fpAnalysts = await AnalystIds(w.HeadFp);
        Assert.DoesNotContain(2, microAnalysts);
        Assert.DoesNotContain(1, fpAnalysts);
    }
```
If analysts 1 and 2 are not seeded with `Role.Type == Analyst` (the S3 workload lists active analysts plus anyone with delivered units), the two `DoesNotContain` checks still hold but prove little: in that case seed the two analyst users in this test and assert `Assert.Contains(1, microAnalysts)` / `Assert.Contains(2, fpAnalysts)` too. If the `wip-backlog` numbers differ, read `WipBacklogKpi` and correct the expected values only if they still differ between the two heads; never weaken to a check that passes for both.

- [ ] **Step 4c: Migrate `SectionScopedQueriesPostgresIntegrationTests`**

`backend/MicroLIMS.Tests/IntegrationTests/SectionScopedQueriesPostgresIntegrationTests.cs:51-63` calls 12 methods that Task 14 deletes. Its purpose is to prove every section-filtered KPI query translates to SQL on Postgres, so it must keep running every new KPI with a real section list. First check whether the registry is reachable from tests: `git grep -n "KpiRegistry" -- backend/MicroLIMS.Tests/TestServiceFactory.cs`. S2 already provides `TestServiceFactory.BuildKpiRegistry(db, time)`; use it, no change to `TestServiceFactory.cs` is needed. Then replace lines 51-63 (from `var kpi = TestServiceFactory.Kpi(db);` through the `GetReturnToAnalystCountAsync` line) with:
```csharp
            var registry = TestServiceFactory.BuildKpiRegistry(db);
            var kpiQuery = new MicroLIMS.Application.Services.Kpi.KpiQuery(
                now.AddYears(-1), now, scope.ToList(), null, null, null, null, now, _fixture.SeededUserId, true);
            foreach (var kpi in registry.All)
            {
                await kpi.ComputeAsync(kpiQuery, default);
                await kpi.GetRecordsAsync(kpiQuery, 1, 50, default);
            }
            await TestServiceFactory.AnalystWorkload(db).GetAsync(kpiQuery, default);
            await TestServiceFactory.AnalystWorkload(db).GetTestsAsync(kpiQuery, 1, 50, default);

            // The dashboard derives its own scope from the user; it must not report an error entry.
            var kpiDash = await TestServiceFactory.KpiDashboard(db).GetDashboardAsync(
                _fixture.SeededUserId, true, DateOnly.FromDateTime(now.AddYears(-1)), DateOnly.FromDateTime(now), null, null, null, null);
            Assert.All(kpiDash.Kpis, k => Assert.True(k.Error is null, k.Error));

            // The one query DashboardService still reads.
            await TestServiceFactory.Kpi(db).GetOverdueAnalystStageSamplesAsync(now.AddYears(-1), now, scope);
```
Add `using MicroLIMS.Application.Services.Kpi;` instead of the long names if it does not clash. `ComputeAsync` throws on an untranslatable filter, which is exactly what this test exists to catch. Run it on Postgres (step 5b).

- [ ] **Step 5: Run the migrated tests and their neighbours**

```bash
cd /e/MicroLIMS/MicroLIMS && dotnet test backend/MicroLIMS.Tests --artifacts-path "$TEMP/kpi-art" --filter "FullyQualifiedName~ReturnToAnalystTests|FullyQualifiedName~KpiDashboardFilterTests|FullyQualifiedName~MultiSectionReviewApprovalTests"
```
Expected: all PASS, including `Kpis_CountOnlyTheViewersSections`. (`KpiFilteringTests`, `WorkloadWeightsTests` and the other old-KPI callers are not touched until Task 14, so the project still compiles here.)

- [ ] **Step 5b: Run the Postgres section-scope test**

```bash
cd /e/MicroLIMS/MicroLIMS && echo "${MICROLIMS_TEST_POSTGRES:+MICROLIMS_TEST_POSTGRES is set}"
dotnet test backend/MicroLIMS.Tests --artifacts-path "$TEMP/kpi-art" --filter "FullyQualifiedName~SectionScopedQueriesPostgresIntegrationTests"
```
Expected: `MICROLIMS_TEST_POSTGRES is set`, then PASS with nothing skipped. If the variable is not set, do not invent a value: report this step as NOT DONE and keep going, but Task 15 step 4 must run it.

- [ ] **Step 6: Commit**

```bash
cd /e/MicroLIMS/MicroLIMS && git add backend/MicroLIMS.Tests/UnitTests/Kpi/KpiDashboardFilterTests.cs backend/MicroLIMS.Tests/WorkflowTests/ReturnToAnalystTests.cs backend/MicroLIMS.Tests/WorkflowTests/MultiSectionReviewApprovalTests.cs backend/MicroLIMS.Tests/IntegrationTests/SectionScopedQueriesPostgresIntegrationTests.cs
# TestServiceFactory.cs needs no change (S2 provides BuildKpiRegistry)
# plus any S2 test file extended in step 1 with a missing named case: git add <that file>
git diff --cached --name-only
git commit -F - <<'EOF'
test(kpi): move the filter, section-scope and return-event coverage onto the registry

KpiService.GetCompletionStatsAsync filters are now asserted on wip-backlog.
Kpis_CountOnlyTheViewersSections asserts the section scope through
KpiDashboardService (wip-backlog) and AnalystWorkloadService (analyst rows);
the Postgres section-scope test runs every registry KPI and the workload
service with a real section list. The return-to-analyst KPI test is deleted: it
duplicated the TestReturnEvent assertions at ReturnToAnalystTests.cs:184-191; the
KPI side is covered by the S2 right-first-time cases. Stage TAT, incubation reads
and RFT are covered by the S2 KpiTileTests. Queue counts and the 24 h delay count
are retired with their methods.

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
EOF
```
`git diff --cached --name-only` must list only the files named above; if it lists anything else, `git restore --staged <path>` the extras before committing.

---

### Task 13: Delete `KpiController` and its authorization-matrix lines

**Files:**
- Delete: `backend/MicroLIMS.API/Controllers/KpiController.cs`
- Modify: `backend/MicroLIMS.Tests/ArchitectureTests/authorization-matrix.txt` (remove every line that starts with `KpiController.`)

**Interfaces:**
- Consumes: Task 11 (no frontend caller of an old `/kpi/...` route remains), S2/S3 controllers `KpiDashboardController` and `KpiWorkloadController`, which keep `[Authorize(Policy = PermissionConstants.KpiView)]` so `Kpi.View` stays referenced from API source (`EveryEnforcedCode_IsCheckedSomewhere`).
- Produces: `GET /api/kpi/analysts`, `/workload-weights`, `/completion-stats`, `/delay-tracking`, `/sample-assignment-sla`, `/sample-assignment-sla-by-analyst`, `/overall-on-time-completion`, `/overall-on-time-completion-by-analyst`, `/return-to-analyst-count`, `/sample-queue-counts`, `/stage-tat-summary`, `/testing-tat-by-month`, `/step-violations`, `/workflow-bottleneck-deltas` no longer exist (14 actions: 15 on `main`, minus `UpdateWorkloadWeight` removed by S1).

The matrix is edited by hand, not regenerated: regenerating would also write lines for the dirty Media/Cryovial controllers into this commit.

- [ ] **Step 1: Prove the controller has no remaining caller**

```bash
cd /e/MicroLIMS/MicroLIMS
git grep -n -E "KpiController" -- backend ':!backend/MicroLIMS.Tests/ArchitectureTests/authorization-matrix.txt'
git grep -n -E "kpi/(analysts|workload-weights|completion-stats|delay-tracking|sample-assignment|overall-on-time|return-to-analyst|sample-queue|stage-tat|testing-tat|step-violations|workflow-bottleneck)" -- frontend/src backend/MicroLIMS.Tests
git grep -c "^KpiController\." -- backend/MicroLIMS.Tests/ArchitectureTests/authorization-matrix.txt
```
Expected: the first two commands print nothing except the controller file itself; the count is `14`. If it is 15, S1 did not remove `UpdateWorkloadWeight`: stop and report it.

- [ ] **Step 2: Delete the controller and the matrix lines**

```bash
cd /e/MicroLIMS/MicroLIMS
git rm backend/MicroLIMS.API/Controllers/KpiController.cs
sed -i '/^KpiController\./d' backend/MicroLIMS.Tests/ArchitectureTests/authorization-matrix.txt
git grep -c "^Kpi" -- backend/MicroLIMS.Tests/ArchitectureTests/authorization-matrix.txt
git diff --stat -- backend/MicroLIMS.Tests/ArchitectureTests/authorization-matrix.txt
```
Expected: the diff shows 14 deletions and no other change; the remaining `Kpi*` lines belong to `KpiDashboardController`, `KpiWorkloadController` and `WorkflowProfilesController`-style controllers added by S1 to S3.

- [ ] **Step 3: Build and run the architecture tests**

```bash
cd /e/MicroLIMS/MicroLIMS
dotnet build backend/MicroLIMS.Tests/MicroLIMS.Tests.csproj --artifacts-path "$TEMP/kpi-art"
dotnet test backend/MicroLIMS.Tests --artifacts-path "$TEMP/kpi-art" --filter "FullyQualifiedName~AuthorizationMatrixTests|FullyQualifiedName~PermissionAuthorizationTests|FullyQualifiedName~ControllerDependencyTests|FullyQualifiedName~ApiContractTests"
```
Expected: build succeeds (`KpiService` is still registered and used by `DashboardService`); all four architecture groups PASS. If `AuthorizationMatrixTests` fails on lines that are not `KpiController.*`, those belong to other in-flight work: report them, do not edit them.

- [ ] **Step 4: Commit**

```bash
cd /e/MicroLIMS/MicroLIMS
git add backend/MicroLIMS.Tests/ArchitectureTests/authorization-matrix.txt
git diff --cached --name-only
git commit -F - <<'EOF'
refactor(kpi): remove the 14 legacy /api/kpi endpoints

The new page reads /api/kpi/dashboard, /records, /definitions and
/api/kpi/workload instead.

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
EOF
```
The controller deletion is already staged by `git rm`, so it is in this commit.

---

### Task 14: Trim `KpiService` to the one method still used, and drop the legacy weight seed

**Files:**
- Rewrite: `backend/MicroLIMS.Application/Services/KpiService.cs` (818 lines down to about 150)
- Delete: `backend/MicroLIMS.Application/Services/WorkloadWeightDefaults.cs`
- Delete: `backend/MicroLIMS.Tests/WorkflowTests/WorkloadWeightsTests.cs`
- Rewrite: `backend/MicroLIMS.Tests/WorkflowTests/KpiFilteringTests.cs`
- Already migrated in Task 12 (must be committed before this task starts): `backend/MicroLIMS.Tests/WorkflowTests/MultiSectionReviewApprovalTests.cs` and `backend/MicroLIMS.Tests/IntegrationTests/SectionScopedQueriesPostgresIntegrationTests.cs`; this task only verifies by grep (steps 1 and 4b) that no test still calls a removed method
- Modify: `backend/MicroLIMS.Persistence/Seed/DbSeeder.cs:265` (remove one line)
- Modify: `backend/MicroLIMS.Application/Services/DashboardService.cs:156` (one comment word)

**Interfaces:**
- Consumes: `DashboardService` calls only `_kpiService.GetOverdueAnalystStageSamplesAsync(Epoch, now, sectionIds)` (`DashboardService.cs:567`); `TestServiceFactory.Kpi(db)` is `new(db)` and `Dashboard(db)` is `new(db, Kpi(db))`; DI line `services.AddScoped<KpiService>();` (`ServiceCollectionExtensions.cs:97`) stays.
- Produces (unchanged public surface that remains):
```csharp
public record OverdueAnalystStageSampleDto(int SampleId, DateTime AssignedAt);
public class KpiService
{
    public KpiService(IMicroLimsDbContext db, TimeProvider? timeProvider = null);
    public Task<List<OverdueAnalystStageSampleDto>> GetOverdueAnalystStageSamplesAsync(DateTime fromDate, DateTime toDate, IReadOnlyCollection<int>? sectionIds = null);
}
```
The private helper `BuildSampleStageWindowsAsync` loses its three filter parameters (`category`, `location`, `testCode`): only the removed methods used them. Everything else in the helper is kept verbatim. The legacy `WorkloadWeight`/`WorkloadWeightHistory` entities, `DbSet`s, configurations and the two `UserReferenceRegistry` lines stay.

**Known deviation to hand to the human (spec section 1 and 4.3):** the kept `GetOverdueAnalystStageSamplesAsync` still applies the single flat 7-day Analyst-stage SLA (`AnalystAssignmentSla`), which the spec lists as a defect (a 14-day test is overdue by construction). `DashboardService`'s Section Head "Attention Items" will therefore keep flagging long-workflow tests as overdue while the new KPI page, which uses the per-workflow target, shows them on time. The two surfaces disagree. This plan accepts that to keep S4 removal-only; the upgrade path is a follow-up that feeds the per-workflow target from `WorkflowProfileService.LoadResolverAsync` into this method, keeping the 7 days only as the spec 4.3 fallback when no target resolves. Repeat this in the hand-off message.

- [ ] **Step 1: Check what the file looks like now and what still uses it**

```bash
cd /e/MicroLIMS/MicroLIMS
git grep -n -E "^\s+public .*\(" -- backend/MicroLIMS.Application/Services/KpiService.cs
git grep -n -E "KpiService|WorkloadWeightDefaults" -- backend ':!backend/MicroLIMS.Persistence/Migrations'
git grep -n "WorkloadWeight" -- backend/MicroLIMS.Tests
git diff --stat -- backend/MicroLIMS.Persistence/Seed/DbSeeder.cs backend/MicroLIMS.Application/Services/DashboardService.cs backend/MicroLIMS.Application/Services/KpiService.cs
```
Expected:
- the public methods are exactly the constructor, `GetWorkloadWeightsAsync`, `GetAnalystKpisAsync`, `GetCompletionStatsAsync`, `GetDelayTrackingAsync`, `GetSampleQueueCountsAsync`, `GetWorkflowBottleneckDeltasAsync`, `GetSampleAssignmentSlaAsync`, `GetSampleAssignmentSlaByAnalystAsync`, `GetOverdueAnalystStageSamplesAsync`, `GetOverallOnTimeCompletionAsync`, `GetOverallOnTimeCompletionByAnalystAsync`, `GetStageTatSummaryAsync`, `GetTestingTatByMonthAsync`, `GetStepViolationsAsync`, `GetReturnToAnalystCountAsync` (S1 removed `UpdateWorkloadWeightAsync`). Any other public member means a later slice added something here: stop and report;
- `KpiService` is referenced only by `DashboardService`, the DI line, `TestServiceFactory`, `KpiFilteringTests`, `WorkloadWeightsTests` and, for the one surviving method only, `SectionScopedQueriesPostgresIntegrationTests` (Task 12 step 4c); after Task 12, `ReturnToAnalystTests` and `MultiSectionReviewApprovalTests` no longer call it. Prove it before touching anything: `git grep -n -E "TestServiceFactory\.Kpi\(|new KpiService\(" -- backend/MicroLIMS.Tests` must list only `KpiFilteringTests.cs`, `WorkloadWeightsTests.cs` and `SectionScopedQueriesPostgresIntegrationTests.cs` (the `GetOverdueAnalystStageSamplesAsync` line); the factory itself (`TestServiceFactory.cs:164`, `new(db)`) matches neither pattern, so confirm it separately with `git grep -n "static KpiService Kpi" -- backend/MicroLIMS.Tests/TestServiceFactory.cs`. A fourth file is a test that still calls a method this task deletes: migrate it as in Task 12 before continuing; `WorkloadWeightDefaults` only by `KpiService` and `DbSeeder.cs:265`; test references to `WorkloadWeight` only in `WorkloadWeightsTests.cs`;
- the three files show no uncommitted diff (`DbSeeder.cs` may show only a later slice's committed state, none uncommitted). If `DbSeeder.cs` has uncommitted hunks from other work, use `git add -p` in step 7.

- [ ] **Step 2: Delete the dead tests and the legacy seed**

```bash
cd /e/MicroLIMS/MicroLIMS
git rm backend/MicroLIMS.Tests/WorkflowTests/WorkloadWeightsTests.cs backend/MicroLIMS.Application/Services/WorkloadWeightDefaults.cs
sed -i '/^        WorkloadWeightDefaults\.Seed(db);\r\?$/d' backend/MicroLIMS.Persistence/Seed/DbSeeder.cs
git diff -- backend/MicroLIMS.Persistence/Seed/DbSeeder.cs
sed -i 's/KpiService\.GetSampleAssignmentSlaAsync with an/KpiService.GetOverdueAnalystStageSamplesAsync with an/' backend/MicroLIMS.Application/Services/DashboardService.cs
git diff -- backend/MicroLIMS.Application/Services/DashboardService.cs
```
Expected: the `DbSeeder.cs` diff is exactly the one removed line; the `DashboardService.cs` diff is exactly the one changed comment line.

- [ ] **Step 3: Rewrite the slimmed regression test file**

Replace the whole of `backend/MicroLIMS.Tests/WorkflowTests/KpiFilteringTests.cs` with:
```csharp
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.WorkflowTests;

// What is left of the pre-remodel KpiService tests: DashboardService still reads
// GetOverdueAnalystStageSamplesAsync for its "Attention Items" list. The filter,
// TAT, queue and return-count behaviours moved to the KPI registry
// (UnitTests/Kpi).
public class KpiFilteringTests
{
    private static MicroLimsDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MicroLimsDbContext(options);
    }

    [Fact]
    public async Task GetOverdueAnalystStageSamplesAsync_RegressionCheck_ReturnsAllOverdueWithoutFilters()
    {
        await using var db = NewDb();

        var now = DateTime.UtcNow;
        var overdueAssigned = now.AddDays(-10); // > 7 days ago

        var s1 = new Sample { Id = 61, Category = SampleCategory.FinishedProduct, ControlNumber = "C61", Status = SampleStatus.InTesting };
        var s2 = new Sample { Id = 62, Category = SampleCategory.RawMaterial, ControlNumber = "C62", Status = SampleStatus.InTesting };
        s1.TestOrders.Add(new TestOrder { Id = 61, AssignedAnalystId = 1, TestCode = "TAMC", Status = ApprovalStatus.InProgress, CurrentStep = WorkflowStep.Running });
        s2.TestOrders.Add(new TestOrder { Id = 62, AssignedAnalystId = 2, TestCode = "TYMC", Status = ApprovalStatus.InProgress, CurrentStep = WorkflowStep.Running });

        db.Samples.AddRange(s1, s2);
        db.SamplePreparations.Add(new SamplePreparation { SampleId = 61, PreparedAt = overdueAssigned, PreparedByUserId = 1 });
        db.SamplePreparations.Add(new SamplePreparation { SampleId = 62, PreparedAt = overdueAssigned, PreparedByUserId = 2 });
        await db.SaveChangesAsync();

        var service = new KpiService(db);
        var overdue = await service.GetOverdueAnalystStageSamplesAsync(now.AddDays(-30), now);

        // Must return both overdue samples across all categories/analysts
        Assert.Equal(2, overdue.Count);
        Assert.Contains(overdue, o => o.SampleId == 61);
        Assert.Contains(overdue, o => o.SampleId == 62);
    }
}
```

- [ ] **Step 4: Rewrite `KpiService.cs`**

Replace the whole of `backend/MicroLIMS.Application/Services/KpiService.cs` with:
```csharp
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Application.Abstractions.Persistence;

namespace MicroLIMS.Application.Services;

// One sample that's overdue on Rule #1's 7-day Analyst-stage SLA -
// backs DashboardService's Section Head "Attention Items" list, which
// needs to name specific samples, not just a count.
public record OverdueAnalystStageSampleDto(int SampleId, DateTime AssignedAt);

// What is left of the pre-remodel KPI service: the 7-day Analyst-stage
// overdue check that DashboardService's live "Attention Items" list still
// reads. Every reported KPI now lives in the registry (Services/Kpi), so do
// not add metrics here.
public class KpiService
{
    // Rule #1's Analyst-stage SLA: assignment -> submitted for review.
    private static readonly TimeSpan AnalystAssignmentSla = TimeSpan.FromDays(7);

    private readonly IMicroLimsDbContext _db;
    private readonly TimeProvider _time;

    public KpiService(IMicroLimsDbContext db, TimeProvider? timeProvider = null)
    {
        _time = timeProvider ?? TimeProvider.System;
        _db = db;
    }

    // Per-sample stage windows - the shared calculation behind the overdue
    // check below, so there's one place that knows how to derive AssignedAt
    // and read the review/approval timeline.
    //
    // AssignedAt: there's no single AssignedAt column anywhere.
    // Assignment happens implicitly at Test Preparation
    // (SamplePreparationService sets TestOrder.AssignedAnalystId with no
    // timestamp of its own; the only nearby timestamp is
    // SamplePreparation.PreparedAt) or explicitly via Section Head
    // reassignment (SampleAssignmentService, logged to AuditLogs with a
    // real Timestamp but likewise no dedicated column). The clock starts
    // at whichever actually happened most recently - a reassignment
    // resets who's accountable, so it resets the SLA clock too. Verified
    // against live data: every sample with an assigned TestOrder has a
    // SamplePreparation row, so this fallback always resolves.
    //
    // Review/Approval timestamps: SampleStatus.RetestRequested is never
    // actually assigned anywhere in this codebase (a retest sends the
    // sample back to InTesting directly - SampleApprovalService.
    // DecideAsync's RetestRetainedSample branch - and a fresh testing
    // round re-triggers a brand new SubmittedForReview event later, once
    // the new TestOrders complete). Taking the LATEST of each event type
    // is what "excluding time spent in a retest loop" actually means
    // given how the state machine really works: it picks out only the
    // final, successful round's stage windows and discards any earlier
    // round's numbers entirely, rather than trying to subtract time
    // against a status value that's never set.
    private record SampleStageWindow(
        int SampleId, HashSet<int> AssignedAnalystIds, DateTime AssignedAt,
        DateTime? SubmittedForReviewAt, DateTime? ReviewCompletedAt, DateTime? ApprovalDecisionAt);

    private async Task<List<SampleStageWindow>> BuildSampleStageWindowsAsync(IReadOnlyCollection<int>? sectionIds)
    {
        var assignedOrders = await _db.TestOrders.Where(SectionReviewQueues.OrderIn(sectionIds))
            .Where(t => t.AssignedAnalystId != null)
            .Select(t => new { t.SampleId, AnalystId = t.AssignedAnalystId!.Value })
            .ToListAsync();

        var sampleAnalysts = assignedOrders
            .GroupBy(o => o.SampleId)
            .ToDictionary(g => g.Key, g => g.Select(o => o.AnalystId).ToHashSet());

        var sampleIds = sampleAnalysts.Keys.ToList();
        if (sampleIds.Count == 0) return new List<SampleStageWindow>();

        var preparedAtLookup = await _db.SamplePreparations
            .Where(p => sampleIds.Contains(p.SampleId))
            .ToDictionaryAsync(p => p.SampleId, p => p.PreparedAt);

        var reassignedAtLookup = await _db.AuditLogs
            .Where(a => a.SampleId != null && sampleIds.Contains(a.SampleId.Value)
                && (a.Action == "AssignedAnalyst" || a.Action == "ReassignedAnalyst"))
            .GroupBy(a => a.SampleId!.Value)
            .Select(g => new { SampleId = g.Key, LatestTimestamp = g.Max(a => a.Timestamp) })
            .ToDictionaryAsync(g => g.SampleId, g => g.LatestTimestamp);

        var events = await _db.ReviewWorkflowEvents
            .Where(e => e.EntityType == ReviewEntityTypes.Sample && sampleIds.Contains(e.EntityId))
            .Select(e => new { e.EntityId, e.EventType, e.Timestamp })
            .ToListAsync();

        var submittedAtLookup = events.Where(e => e.EventType == ReviewWorkflowEventType.SubmittedForReview)
            .GroupBy(e => e.EntityId).ToDictionary(g => g.Key, g => g.Max(e => e.Timestamp));
        var reviewCompletedAtLookup = events.Where(e => e.EventType == ReviewWorkflowEventType.ReviewCompleted)
            .GroupBy(e => e.EntityId).ToDictionary(g => g.Key, g => g.Max(e => e.Timestamp));
        var approvalDecisionAtLookup = events.Where(e => e.EventType == ReviewWorkflowEventType.ApprovalDecisionMade)
            .GroupBy(e => e.EntityId).ToDictionary(g => g.Key, g => g.Max(e => e.Timestamp));

        var result = new List<SampleStageWindow>();
        foreach (var sampleId in sampleIds)
        {
            // No SamplePreparation row means this sample was never
            // actually prepared - shouldn't happen for a TestOrder with
            // AssignedAnalystId set, but skip rather than assume a clock
            // start that was never recorded.
            if (!preparedAtLookup.TryGetValue(sampleId, out var preparedAt)) continue;

            var assignedAt = reassignedAtLookup.TryGetValue(sampleId, out var reassignedAt) && reassignedAt > preparedAt
                ? reassignedAt
                : preparedAt;

            DateTime? submittedAt = submittedAtLookup.TryGetValue(sampleId, out var sa) ? sa : null;
            DateTime? reviewCompletedAt = reviewCompletedAtLookup.TryGetValue(sampleId, out var rc) ? rc : null;
            DateTime? approvalDecisionAt = approvalDecisionAtLookup.TryGetValue(sampleId, out var ad) ? ad : null;

            result.Add(new SampleStageWindow(sampleId, sampleAnalysts[sampleId], assignedAt, submittedAt, reviewCompletedAt, approvalDecisionAt));
        }
        return result;
    }

    // Rule #1's Analyst-stage overdue check - the one place this
    // comparison is made, so GetOverdueAnalystStageSamplesAsync (and
    // DashboardService through it) can never drift apart on what
    // "overdue" means.
    private bool IsAnalystStageOverdue(SampleStageWindow w, DateTime now)
    {
        var deadline = w.AssignedAt.Add(AnalystAssignmentSla);
        return w.SubmittedForReviewAt.HasValue ? w.SubmittedForReviewAt.Value > deadline : now > deadline;
    }

    // The individual samples that are overdue on the 7-day Analyst-stage
    // SLA, oldest-assigned first - what DashboardService's Section Head
    // "Attention Items" list needs to name specific overdue samples
    // instead of just a count. AssignedAt is returned alongside each
    // SampleId so the caller can report real elapsed time against the
    // real clock start, not Sample.ReceivedAt.
    public async Task<List<OverdueAnalystStageSampleDto>> GetOverdueAnalystStageSamplesAsync(DateTime fromDate, DateTime toDate,
        IReadOnlyCollection<int>? sectionIds = null)
    {
        var windows = await BuildSampleStageWindowsAsync(sectionIds);
        var now = _time.GetUtcNow().UtcDateTime;

        return windows
            .Where(w => w.AssignedAt >= fromDate && w.AssignedAt <= toDate && IsAnalystStageOverdue(w, now))
            .OrderBy(w => w.AssignedAt)
            .Select(w => new OverdueAnalystStageSampleDto(w.SampleId, w.AssignedAt))
            .ToList();
    }
}
```

- [ ] **Step 4b: Sweep for callers of the removed methods before building**

Run this BEFORE the build so a missed test file is found by name, not by a wall of compile errors. It covers `backend/MicroLIMS.Tests` explicitly:
```bash
cd /e/MicroLIMS/MicroLIMS
git grep -n -E "GetAnalystKpisAsync|GetWorkloadWeightsAsync|GetCompletionStatsAsync|GetDelayTrackingAsync|GetSampleQueueCountsAsync|GetWorkflowBottleneckDeltasAsync|GetSampleAssignmentSla|GetOverallOnTimeCompletion|GetStageTatSummaryAsync|GetTestingTatByMonthAsync|GetStepViolationsAsync|GetReturnToAnalystCountAsync|UpdateWorkloadWeightAsync" -- backend/MicroLIMS.Tests backend/MicroLIMS.API backend/MicroLIMS.Application ':!backend/MicroLIMS.Application/Services/KpiService.cs'
```
Expected: no output (`WorkloadWeightsTests.cs` is already `git rm`-ed in step 2 and `KpiFilteringTests.cs` rewritten in step 3, and Task 12 migrated the other two files). Any hit is a blocker: migrate that test onto the registry before step 5.

- [ ] **Step 5: Build, then run the affected tests**

```bash
cd /e/MicroLIMS/MicroLIMS
dotnet build backend/MicroLIMS.Tests/MicroLIMS.Tests.csproj --artifacts-path "$TEMP/kpi-art"
dotnet test backend/MicroLIMS.Tests --artifacts-path "$TEMP/kpi-art" --filter "FullyQualifiedName~KpiFilteringTests|FullyQualifiedName~SampleOrientedSectionHeadAndAdminDashboardTests|FullyQualifiedName~ReturnToAnalystTests|FullyQualifiedName~KpiDashboardFilterTests|FullyQualifiedName~UserDeletionTests|FullyQualifiedName~Seed"
```
Expected: the build has no errors and no unused-member warning for `KpiService`; all selected tests PASS (`UserDeletionTests` still passes because the `WorkloadWeightHistory.ChangedByUserId` registry line and the entity stay).

- [ ] **Step 6: Confirm nothing else referenced the removed members**

```bash
cd /e/MicroLIMS/MicroLIMS
git grep -n -E "GetAnalystKpisAsync|GetWorkloadWeightsAsync|GetCompletionStatsAsync|GetDelayTrackingAsync|GetSampleQueueCountsAsync|GetWorkflowBottleneckDeltasAsync|GetSampleAssignmentSla|GetOverallOnTimeCompletion|GetStageTatSummaryAsync|GetTestingTatByMonthAsync|GetStepViolationsAsync|GetReturnToAnalystCountAsync|WorkloadWeightDefaults|UpdateWorkloadWeightRequest|AnalystKpiDto|WorkloadWeightDto" -- backend ':!backend/MicroLIMS.Persistence/Migrations'
```
Expected: no output (the DbSeeder, DashboardService comment and every test reference are gone). A hit inside a code comment elsewhere (for example a rule description in another service) may be reworded or left; a hit in code is a blocker.

- [ ] **Step 7: Commit**

For `DbSeeder.cs` and `DashboardService.cs`, `git diff` first: if any hunk other than the one named above appears (uncommitted work from another feature), stage with `git add -p` and take only that hunk.
```bash
cd /e/MicroLIMS/MicroLIMS
git add backend/MicroLIMS.Application/Services/KpiService.cs backend/MicroLIMS.Tests/WorkflowTests/KpiFilteringTests.cs backend/MicroLIMS.Persistence/Seed/DbSeeder.cs backend/MicroLIMS.Application/Services/DashboardService.cs
git status --short -- backend/MicroLIMS.Application/Services/WorkloadWeightDefaults.cs backend/MicroLIMS.Tests/WorkflowTests/WorkloadWeightsTests.cs
git diff --cached --name-only
git commit -F - <<'EOF'
refactor(kpi): trim KpiService to the overdue-sample query DashboardService reads

Removes the analyst KPI, completion, delay, queue, SLA, TAT, step-violation and
return-count methods, the WorkloadWeight seed and its tests. The legacy
WorkloadWeight tables stay read-only for one more release. Attention Items keeps
the flat 7-day analyst SLA (known inconsistency with spec 4.3; per-workflow
target is a follow-up).

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
EOF
```
Expected: the `git status` line shows both deletions staged; `git diff --cached --name-only` lists only `KpiService.cs`, `KpiFilteringTests.cs`, `DbSeeder.cs`, `DashboardService.cs`, `WorkloadWeightDefaults.cs` and `WorkloadWeightsTests.cs` (restore anything else with `git restore --staged <path>`); the commit includes the two deletions.

---

### Task 15: Full verification (frontend suite, type-check, lint, backend build and tests)

**Files:** none (no source change; fix forward in the owning task's files if something fails and re-run).

**Interfaces:** Consumes everything above. Produces a clean, committed branch state for S4.

- [ ] **Step 1: Frontend**

```bash
cd /e/MicroLIMS/MicroLIMS/frontend
npm test
npx tsc --noEmit -p tsconfig.json
npm run lint
npm run build
```
Expected: `npm test` all files PASS (the 31 pre-existing files plus: `kpiFormat`, `kpiFilters`, `KpiTile`, `KpiSection`, `WorkloadTable`, `useKpiDashboard`, `useKpiWorkload`, `DefinitionsDrawer`, `KpiRecordsDrawer`, `exportKpiPdf`, `KpiFilters`, `KpiPerformanceTab`, `Reports`, and the S1 to S3 frontend tests); `tsc` prints nothing; `npm run lint` reports no errors in `src/modules/kpi/**` or `src/pages/Reports*.tsx` (warnings elsewhere are the repository's existing baseline; do not touch them); `npm run build` completes.

- [ ] **Step 2: Backend build and the focused groups**

```bash
cd /e/MicroLIMS/MicroLIMS
dotnet build backend/MicroLIMS.Tests/MicroLIMS.Tests.csproj --artifacts-path "$TEMP/kpi-art"
dotnet test backend/MicroLIMS.Tests --artifacts-path "$TEMP/kpi-art" --filter "FullyQualifiedName~Kpi|FullyQualifiedName~WorkflowProfile|FullyQualifiedName~Workload"
```
Expected: zero build errors; the filtered tests PASS.

- [ ] **Step 3: Full backend suite**

```bash
cd /e/MicroLIMS/MicroLIMS
dotnet test backend/MicroLIMS.Tests --artifacts-path "$TEMP/kpi-art"
```
Expected: PASS. Postgres tests print as skipped when `MICROLIMS_TEST_POSTGRES` is unset; a skip does not count as a pass (next step). Failures in files you did not touch and that belong to the dirty Media/Cryovial work: report them with the test name; do not fix them here.

- [ ] **Step 4: Postgres tests once**

```bash
cd /e/MicroLIMS/MicroLIMS
echo "${MICROLIMS_TEST_POSTGRES:+MICROLIMS_TEST_POSTGRES is set}"
dotnet test backend/MicroLIMS.Tests --artifacts-path "$TEMP/kpi-art" --filter "FullyQualifiedName~Postgres"
```
Expected: the first line prints `MICROLIMS_TEST_POSTGRES is set`, then the Postgres group PASSES with none skipped (this covers `FreshDatabaseSeedPostgresTests` after the weight seed was dropped, and the S1 to S3 migration and golden-dataset tests). If the variable is not set, do not invent a value: it is a connection string to a throw-away maintenance database (see `backend/MicroLIMS.Tests/Fixtures/PostgresTestConfiguration.cs`); ask the human to supply it, and report the Postgres run as NOT DONE.

- [ ] **Step 5: Dangling-reference sweep**

```bash
cd /e/MicroLIMS/MicroLIMS
git grep -n -E "AnalystKpi|kpiDtos|exportKpi[^P]|WorkloadWeightsDialog|KpiController|WorkloadWeightDefaults" -- frontend/src backend ':!backend/MicroLIMS.Persistence/Migrations'
git status --short -- "*[Kk]pi*" "*[Ww]orkload*" "frontend/src/modules/reports" "frontend/src/pages/Reports*"
```
Expected: the first command prints nothing; the second prints nothing either (every KPI, workload and Reports path is committed). A line there means a task missed a file; other dirty files (Media, Cryovial, storage, routes) are not yours and must not appear in any S4 commit. `frontend/src/routes/routes.ts` stays modified because of that other work; S4 never edits it.

---

### Task 16: Browser verification with the preview tools

**Files:** `frontend/.env.local` (create; git-ignored by `frontend/.gitignore` rule `.env*.local`, never committed).

**Interfaces:** Consumes `.claude/launch.json` at the repo root, which defines `backend` (`dotnet run --project backend/MicroLIMS.API/MicroLIMS.API.csproj`, port 65435, url `https://localhost:65435`) and `frontend` (`npm run dev --prefix frontend`, port 5173). If the preview tool runs from `E:\MicroLIMS` instead, the parent `.claude/launch.json` names them `microlims-backend` and `microlims-frontend`. `apiClient` points at `http://localhost:5000/api` on localhost unless `VITE_API_BASE_URL` is set, and the backend listens on `https://localhost:65435`, so the variable is required. This task changes no tracked file.

- [ ] **Step 1: Point the frontend at the local backend**

Create `frontend/.env.local` with the single line:
```
VITE_API_BASE_URL=https://localhost:65435
```

- [ ] **Step 2: Start both servers**

With the Browser preview tools: `preview_start` name `backend`, then `preview_start` name `frontend`; check `preview_logs` for the backend until it reports it is listening and for any startup error. (Without the tools: run the two commands from `launch.json` in two terminals.) If a stale `MicroLIMS.API` is already running from an older build, stop it first so the old KPI routes are really gone.

- [ ] **Step 3: Sign in with the local development account**

Open `http://localhost:5173/reports?tab=performance`. Sign in with the local development seed user described in `backend/MicroLIMS.Persistence/Seed/DbSeeder.cs` (username `admin`; its password is in the project's seed or README, never a real account). If your session rules do not allow typing credentials, ask the human to sign in and continue from the signed-in page.

- [ ] **Step 4: Check the page against the spec, one item at a time**

Take a snapshot (`read_page` or `get_page_text`) for each and record pass or fail:
1. Eight headline tiles appear in registry order (Sample TAT, On time all stages, Right first time, Retest rate, OOS rate, WIP backlog, Incubation reads on time, EM excursion rate), each with an `n =` line. On an empty database each shows "No data", with no `0`, `0%` or `100%`.
2. The Flow, Quality and Micro sections and the Workload block render; the Reports page-level "Last 7 Days / Last 30 Days..." selector is NOT shown on this tab, and IS shown on the Overview tab.
3. The workload rows are alphabetical; clicking a column heading re-sorts; reloading the page returns to alphabetical.
4. Click a tile: the records drawer opens, `GET /api/kpi/<key>/records?...&page=1&pageSize=50` returns 200 (`read_network_requests`), and the drawer's `total` equals the count the tile reports where the tile is a count (for example WIP backlog).
5. Click `Definitions`: every KPI shows definition, clock events, population and source, and "Profile versions in force on <to date>" is present.
6. Click `Workflow Profiles`: the S1 dialog opens.
7. Change From to a later date than To: an inline message appears and no new `/api/kpi/dashboard` request is sent (`read_network_requests`); set a valid range: both `/api/kpi/dashboard` and `/api/kpi/workload` are requested once with the new `from`.
8. `Export PDF`: a print window opens with the same values as the screen (compare two tiles and one workload row).
9. `read_console_messages` with `onlyErrors: true`: no errors from the KPI page.
10. The old routes are gone. Unauthenticated, an absent route answers 404 before authentication: `curl -sk -o /dev/null -w "%{http_code}\n" https://localhost:65435/api/kpi/analysts` and `.../api/kpi/workload-weights` must print `404`, while `.../api/kpi/dashboard` prints `401`. A `401` for the old routes means they still exist.
11. Resize to the `mobile` preset (375 px wide): tiles stack to one column, the filter bar wraps, and there is no horizontal page scroll. Reset with the `desktop` preset afterwards.
12. If a user with only `Kpi.View` (no `Kpi.ViewTeam`) is available locally, sign in as that user: the Workload block shows "Showing your own numbers only." with one row. Otherwise record this as not exercised; the unit test covers the rendering and `KpiWorkloadController` tests cover the server side.

- [ ] **Step 5: Clean up**

Stop both servers (`preview_stop`), reset the viewport, and delete `frontend/.env.local`. Run `git status --short frontend` and confirm no KPI file changed during the check. If any item in step 4 failed, fix it in the task that owns the file, re-run that task's tests, and repeat only the failed item.

- [ ] **Step 6: Report**

Write the outcome of each numbered item (pass, fail with the evidence, or not exercised) in the hand-off message. Do not commit anything in this task.
