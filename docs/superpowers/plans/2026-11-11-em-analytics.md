# Environmental Monitoring Analytics (F1) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Controlled grades, floor plans with room markers and state, trends by grade and season, flora from E4 isolates, and versioned adverse-trend rules that flag out-of-trend readings and open excursion reviews.

**Architecture:** New `EmGrade`, `EmArea` (+ drawing versions), `EmMarker`, `EmSeason`, `EmTrendRule` (+ versions), `EmTrendFiring`, `SampleLocationTrendFlag`. Read models project from `SampleLocation` and `ResultRecord` with `AsNoTracking`. `EmTrendRuleEvaluator` (pure) runs on result approval and nightly; firings flag readings and call D1's excursion review API. The dead `RoomMonitoring.IsOutOfTrend` is left untouched and documented.

**Tech Stack:** .NET 10, EF Core 10, PostgreSQL, xUnit `[PostgresFact]`; React 19, TypeScript, MUI, recharts, Vitest.

**Spec:** `docs/superpowers/specs/2026-11-11-em-analytics-design.md`

## Global Constraints

- `CLAUDE.md` binds every task (anti-fake tests, triad, `[PostgresFact]`, `AsNoTracking` + SQL projection, page clamp, `ILabClock`, `CancellationToken`, WCAG 2.2 AA: never colour alone, keyboard alternative for dragging, design tokens for states).
- No change to result evaluation. "At or above alert level" = `ResultStatus` in {`AlertLimitExceeded`, `ActionLimitExceeded`, `OutOfSpecification`}; "any growth" = count > 0 for count tests, `Detected` / growth for presence tests.
- No rule active and no number pre-filled. Rule versions immutable; evaluation uses the version effective at the result's sampled date.
- Out-of-trend flag stored in a new `SampleLocationTrendFlag` table (append-only), not on `RoomMonitoring` (unused by code).
- Firings open D1 excursion reviews through `OosInvestigationService.OpenManualExcursionAsync`-style API (generalised to accept a room and readings, D1 Task 9); if D1 is not built, firings notify only and the plan reports it.
- Drawings through `IFileStorageService`, key `em-areas/{areaId}/{versionNo}{ext}`; PNG/JPEG ≤ 20 MB; PDF first page rendered with the existing PDF tooling — verify a renderer exists, else accept PNG/JPEG only and report.
- Permission codes: `EmAnalytics.View`, `EmAnalytics.Configure`.

## Review Focus

1. Rule edited after results exist re-judges them → `Earlier_results_judged_by_version_at_sampled_date` (Task 4).
2. Nightly run and approval-time run both fire for the same pattern → `One_firing_per_pattern` (Task 4).
3. A corrected reading drops below alert after a firing → firing and flag kept, correction shown → `Corrected_reading_keeps_firing_history` (Task 5).
4. Marker positions break on a new drawing version with different size → positions stored relative (0–1) and the editor asks to confirm markers after a drawing change → `New_drawing_version_keeps_relative_positions` (Task 1).
5. Recovery rate with zero samples in a period → "No data", not 0 % → `Recovery_rate_without_samples_is_no_data` (Task 3).

## File Structure

| File | Responsibility |
|---|---|
| `Domain/Entities/EmAnalytics/*.cs` | `EmGrade`, `EmArea`, `EmAreaDrawing`, `EmMarker`, `EmSeason`, `EmTrendRule`, `EmTrendRuleVersion`, `EmTrendFiring`, `SampleLocationTrendFlag` |
| `Domain/Entities/Room.cs` (modify) | `GradeId?` (kept with `GradeClassification` text until mapping done) |
| `Application/Services/EmAnalytics/*.cs` | `EmGradeService`, `EmAreaService`, `EmStateService`, `EmTrendService`, `EmFloraService`, `EmTrendRuleService`, `EmTrendRuleEvaluator`, `EmTrendFiringService` |
| `API/Controllers/EmAnalyticsController.cs`, `API/BackgroundServices/EmTrendNightlyWorker.cs` | Endpoints, nightly evaluation |
| `frontend/src/modules/emAnalytics/*` | Floor plan, list view, room panel, trends, flora, rule and season editors |

---

### Task 1: Grades, areas, drawings, markers

**Interfaces:** `EmGrade { Id; Name (unique); Order; IsActive }`; `Room.GradeId`; `MapGradesAsync(IReadOnlyList<{ text, gradeId }>)` maps every distinct existing `GradeClassification` once (audited); room save requires `GradeId` once mapping is complete. `EmArea { Id; Name; SectionId? }`, `EmAreaDrawing { AreaId; VersionNo; StorageKey; ContentType; Width; Height; Sha256; UploadedByUserId; UploadedAtUtc }` (append-only), `EmMarker : IVersionedEntity { AreaId; RoomId? | WaterSamplingPointId?; X (0–1); Y (0–1) }` unique per target.

- [ ] **Step 1:** Failing tests: grade list CRUD with audit; mapping of free-text values; room save refused with unknown grade after mapping; drawing upload type and size refusal; versions kept; `New_drawing_version_keeps_relative_positions` and "confirm markers" flag; marker for room or water point, not both.
- [ ] **Step 2:** FAIL → implement → PASS. Commit `feat(em): grades, floor plans and markers`.

### Task 2: Room states

**Interfaces:** `GetAreaStateAsync(areaId, fromDate, toDate)` → per marker `{ state (NoData|Within|Alert|Action|AdverseTrend|Overdue), counts, lastSampledAt }`; precedence AdverseTrend > Action > Alert > Overdue > Within > NoData; Overdue from monitoring-plan occurrences (`MonitoringOccurrenceService.GetDueAsync` logic) in the period. `GetRoomPanelAsync(roomId, from, to)`.

- [ ] **Step 1:** Failing tests: each state and precedence; period boundaries in lab dates; section scope.
- [ ] **Step 2:** FAIL → implement → PASS. Commit `feat(em): room states for floor plans`.

### Task 3: Trends, seasons, flora

**Interfaces:** `EmSeason { Id; Name; StartMonth; EndMonth }` (may wrap the year end). `GetTrendAsync(EmTrendQuery { groupBy: Room|Grade|TestType; period: Month|Quarter|Season; from; to; testType? })` → rows with sample count, alert count, action count, growth count, recovery rate (`null` when sample count 0). `GetFloraAsync(roomId? | gradeId?, from, to)` from E4 isolates with final identity on EM sources → organism / genus / Gram group counts with links. CSV export logged.

- [ ] **Step 1:** Failing tests: grouping and period buckets incl. wrapping season; `Recovery_rate_without_samples_is_no_data`; flora counts and the "identified plates only" note; export logged.
- [ ] **Step 2:** FAIL → implement → PASS. Commit `feat(em): trends, seasons and flora`.

### Task 4: Trend rules and evaluation

**Interfaces:** `EmTrendRule { Id; Type (ConsecutiveAlert|CountInWindow|RecoveryRate|RepeatOrganism|ControlChart); GradeId; RoomId?; TestType?; IsActive }`, `EmTrendRuleVersion { RuleId; VersionNo; EffectiveFrom (DateOnly); ParametersJson; CreatedByUserId }` (append-only). Parameters: ConsecutiveAlert `{ n }`; CountInWindow `{ x, windowSamples? , windowDays? }` (exactly one window); RecoveryRate `{ maxPercent, period: Month|Quarter }`; RepeatOrganism `{ k, days, level: Species|Genus }`; ControlChart `{ rules: int[] }` (existing `ControlChartCalculator` rule ids). Pure `EmTrendRuleEvaluator.Evaluate(rule version, series) -> IReadOnlyList<TrendHit { ResultIds, Description }>`. `EmTrendFiring { RuleVersionId; RoomId; TestType?; PatternKey (sorted result ids hash); FiredAtUtc; ExcursionReviewId? }` unique (`RuleVersionId`, `RoomId`, `PatternKey`). Evaluation entry points: after section approval of an EM sample (hook next to D2/E2 hooks in `FinalizeIfClosedAsync`, writing firings in the same transaction; notifications after commit) and `EmTrendNightlyWorker` (catalog entry, Gxp) for window and rate rules.

- [ ] **Step 1:** Failing tests: each rule type at and around its boundary (unit tests on the evaluator with generated series); `Earlier_results_judged_by_version_at_sampled_date`; `One_firing_per_pattern` across approval and nightly runs; inactive rules ignored; parameters validated (no zero or negative, one window kind); rule changes audited.
- [ ] **Step 2:** FAIL → implement → PASS. Commit `feat(em): adverse-trend rules and evaluation`.

### Task 5: Firing effects

**Interfaces:** `SampleLocationTrendFlag { SampleLocationId; FiringId; FlaggedAtUtc }` (append-only); E4's `IdentificationRequirementService` reads OOT from this table (update E4 plan Task 4 text accordingly). On firing: flags, notification to the section's Section Head and QA (after commit), excursion review opened or joined (open review with same rule and room → add readings).

- [ ] **Step 1:** Failing tests: flags written; E4 requirement now lists the flagged plates; review opened; second firing joins the open review; `Corrected_reading_keeps_firing_history`; notification rows after commit and none on rollback.
- [ ] **Step 2:** FAIL → implement → PASS. Commit `feat(em): out-of-trend flags and excursion reviews from trend rules`.

### Task 6: Screens

**Files:** `modules/emAnalytics/FloorPlanPage.tsx` (drawing with absolutely positioned markers: icon + short text label + colour token from `statusTokens.ts`; keyboard: Tab through markers, Enter opens panel; edit mode: arrow keys move the focused marker 1 %, Shift+arrows 5 %), `FloorPlanListView.tsx`, `RoomPanel.tsx`, `TrendsPage.tsx`, `FloraPage.tsx`, `RulesPage.tsx` (types offered, all off, version history), `SeasonsCard.tsx`, `GradeMappingDialog.tsx`; tests

- [ ] **Step 1:** Failing tests (`userEvent`, `services/` mocked; chart wrapper stubbed per CLAUDE.md 4.5 with assertions on data): markers show icon and text, not colour only; keyboard move in edit mode; list view equals marker data; room panel; trend filters; rule editor creates a new version with effective date; season wrap-around.
- [ ] **Step 2:** FAIL → implement → lint, types, tests, debt check, build PASS. Commit `feat(ui): EM floor plans, trends, flora and rules`.

### Task 7: Documents and verification

- [ ] **Step 1:** Update `docs/superpowers/specs/2026-11-11-microbial-identification-design.md` and its plan: out of trend = flag from F1's rules (not `RoomMonitoring.IsOutOfTrend`). `BusinessRules.md` EM section; A1 entries if present; manual chapter.
- [ ] **Step 2:** Full verification per `CLAUDE.md`; report counts.

## Cross-plan corrections (whole-programme review, 2026-11-11)

These corrections come from reviewing all plans together against the code. Where they differ from the text above, they win. Shared names are defined once in `docs/superpowers/plans/2026-11-11-build-order.md` (section "Programme conventions").

1. Firings open reviews through D1's `OpenExcursionForReadingsAsync(roomId, sampleLocationIds, sourceRef = "trend:{ruleVersionId}:{roomId}", userId: null, ct)`.
2. E4's plan already reads `SampleLocationTrendFlag`; Task 7 only verifies it.
3. Migration names: `AddEmGradesAndFloorPlans` (Task 1), `AddEmTrendRules` (Task 4).

## Review corrections (independent review, 2026-11-11)

From the independent review of the build order. These win over the text above and over the earlier corrections section where they differ.

1. **Trend evaluation is not done inside the approval** (user decision 2026-11-11): F1's final-decision hook writes an `EmTrendEvaluationRequest { SampleId; RequestedAtUtc; Attempts; LastError?; DoneAtUtc? }` row in the same save; `EmTrendNightlyWorker` (renamed `EmTrendWorker`, runs every 5 minutes and nightly) processes requests and retries; after 5 failures the Section Head is notified. A failing evaluation never rolls back an approval.

**Translation (build order 2.7):** if F4 Task 4 is merged when this package is built, every new screen uses translation keys and every new refusal carries a message code.
