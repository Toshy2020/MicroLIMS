# Retire the legacy System Suitability (HPLC) module

Date: 2026-10-02 · Branch: `feat/retire-legacy-sst` (from `fix/hplc-sst-blank-fields`)

## Goal

One place for HPLC system suitability: the HPLC workspace run. The old
"System Suitability (HPLC)" page (`SystemSuitabilityRuns`) and everything that
only exists to feed it are removed.

## Decisions (user, 2026-10-02)

- Retire the old module fully, now.
- Existing old SST data is test data: delete it (2 runs locally, plus the
  `Water soluble` orders that point at them). Production is checked first.
- Dissolution joins workspace runs: dissolution orders are assigned to an HPLC
  run and take their standard from that run's passed SST.
- The Standard Comparison workflow (including its unused titration mode) and
  the `Water soluble` test definition are removed. Multi-vitamin assay is done
  with HPLC method tests (e.g. HPLC-VIT-B).
- Dissolution standard concentration applies moisture:
  `Cs = W × P/100 × (100 − MC)/100 ÷ standard dilution`.

## Current state (local LIMSV2, 2026-10-02)

| Consumer of `SystemSuitabilityRuns` | Data |
|---|---|
| `StandardComparisonRecorder` (`Water soluble`, WorkflowType 11) | 4 orders, all linked to `WATERSOLUBLE S.S 02/092026` (statuses 0, 2, 4, 8) |
| `DissolutionRecorder` (`DISS-UI`, WorkflowType 7) | 1 pending order, unlinked |
| `SampleApprovalService` legacy SST gate | - |
| `TestAnalyteMasterDataService`, `TestDefinitionMasterDataService` in-use checks | - |
| `HplcRunService` SST code series (concatenates legacy codes) | - |

Not affected: `CalibrationRunService` (ICP/AAS) only shares the
`SystemSuitabilityRunCode` helper; test analytes stay (ICP/AAS use them).

## Stage 1 - Dissolution in the workspace

Additive; nothing is removed. Stop point after this stage.

### Method link
- A dissolution `TestDefinition` sets `HplcMethodId` (same field HPLC method
  assay tests use). Test master UI shows the method picker for Dissolution.
- `HplcMethodAnalyte` gets `StandardDilution` (decimal?, > 0), a method
  constant next to Th.Wt, edited on the HPLC method master and included in the
  method snapshot and method history. Migration: nullable column, no backfill.

### Assignment
- `GetEligibleTestsAsync` returns `HplcMethodAssay` **and** `Dissolution`
  orders whose test's `HplcMethodId` equals the run's method.
- `AssignSamplesAsync` refuses a dissolution order when the run's method
  snapshot has no `StandardDilution` on its (single) analyte, or the method has
  more than one analyte: "Method X needs a standard dilution before dissolution
  samples can be assigned."

### Entry and submission
- The workspace sample entry page branches on the order's workflow. For
  Dissolution it shows the existing 6-vessel form (medium volume, dilution
  factor, vessel areas, configured condition fields) instead of the replicate
  table, plus a read-only standard block from the run's SST: mean response,
  weight, purity, moisture, standard dilution, SST code.
- Send for Review signs and calls `DissolutionRecorder` with the run sample.
  Equipment = the run's instrument; analysed-at = now (lab clock).
- `DissolutionRecorder` takes the standard from the order's assigned
  (`Status == Assigned`) run sample: the run's SST must be `Passed`; the SST
  analyte supplies mean response, weight, purity, moisture; the method snapshot
  supplies standard dilution. Missing/invalid values throw before signing.
- `DissolutionCalculator.CalculateCs` gains the moisture term.
  `DissolutionStandardData` records `HplcRunId`, run code and SST code instead
  of `SystemSuitabilityRunId`; adds `StandardMoisturePercent`.
- Run sample "submitted" and run completion use the same rule as assay
  samples (order no longer editable / sent for review).

### Testing page
- `TestWorkflowDialog` no longer renders the dissolution SST-run picker; for
  Dissolution it shows "Enter results in the HPLC workspace" with a link to the
  assigned run (or "Assign this sample to an HPLC run" when unassigned), the
  same treatment HPLC method assay tests get.

### Approval gate
- `SampleApprovalService` treats Dissolution like `HplcMethodAssay`: approval
  needs an assigned run sample on a run whose SST is `Passed`.

### Stage 1 tests
- Dissolution calculator: Cs with moisture.
- Recorder: standard taken from the assigned run; refuses when unassigned,
  SST not passed, or standard dilution missing.
- Eligibility / assignment: dissolution order listed for a matching method;
  assignment refused without standard dilution.
- Approval gate for a dissolution order with and without a passed run.
- Browser: DISS-UI linked to a single-analyte method, assigned to a run with a
  passed SST, 6 vessel areas entered, sent for review, approved.

## Stage 2 - Remove the legacy module

Stop point after this stage (before any push).

### Backend removed
- `SystemSuitabilityController`, `ISystemSuitabilityService`,
  `SystemSuitabilityService`, `SystemSuitabilityDtos`.
- Entities and configurations: `SystemSuitabilityRun`,
  `SystemSuitabilityRunAnalyte`, `SystemSuitabilityStandardResponse`; their
  `DbSet`s and unique index names.
- `StandardComparisonRecorder`, `StandardComparisonContextDto`,
  `StandardComparisonCalculationData`, the Standard Comparison endpoints on
  `TestWorkflowController`, contracts and engine wiring.
- `TestDefinition.ResponseMode` and the `ResponseMode` enum (titration mode).
- `TestOrder.SystemSuitabilityRunId`.
- Legacy branches in `SampleApprovalService`, `TestAnalyteMasterDataService`,
  `TestDefinitionMasterDataService`, `UserReferenceRegistry`,
  `UserSectionScopeService`, `ReportingQueryService`, `SpecificationService`,
  `TestWorkflowQueryService`, `AnalysisWorkflows`.
- `HplcRunService` SST code series no longer concatenates legacy codes.
- Tests that only cover removed code; authorization matrix entries.

### Backend kept
- `WorkflowType.StandardComparison` value, commented as retired (stored int).
- `EquationType.StandardComparison` only if still referenced by remaining
  specification code; otherwise marked retired the same way.
- `StandardComparisonCalculator.CalculatePreparationAssay` and
  `CalculatePreparationRsd` (used by `HplcAssayCalculator`,
  `HplcSstEvaluator`, `HplcRunService`); old-only members deleted.
- `SystemSuitabilityRunCode` helper.

### Frontend removed
- `modules/systemSuitability/*`, its routes in `AppRoutes.tsx`, the
  "System Suitability (HPLC)" entry in `menuConfig.ts`.
- `StandardComparisonPanel`, Standard Comparison types and service calls in
  `testingWorkspace`, and their cases in `TestWorkflowDialog`,
  `TestResultCards`, `SampleSummaryDialog`.
- Standard Comparison / response-mode options in `TestMasterPage`,
  `ItemSpecificationsSection`, `SpecificationParameterDialog`,
  `useTestDefinitions`, `masterDataOptions`.

### Data migration (one migration, `RetireLegacySystemSuitability`)
1. Guard: if any `TestOrders` row uses a test definition with
   `WorkflowType = 11` other than `Water soluble`, raise an exception naming
   the test codes (no silent deletion).
2. Delete everything hanging off `Water soluble` orders (result readings,
   parameter results, result records, test analyses, return events, workflow
   step results, workflow histories, `HplcRunSamples` none expected), then the
   orders.
3. Delete the `Water soluble` test definition with its specifications and item
   links (and any `WorkflowType = 11` definitions with no orders).
4. Delete the old SST rows (responses, analytes, runs); drop the three tables;
   drop `TestOrders.SystemSuitabilityRunId` (FK + index) and
   `TestDefinitions.ResponseMode`.
5. `Down` recreates the tables and columns empty (data is not restored).

Run on LIMSV2 only after a `pg_dump` backup
(`LIMSV2_before_retire_legacy_sst_<date>.dump`).

### Production guard
Production migrates at startup. Before merging, run read-only on Neon:
- `select count(*) from "SystemSuitabilityRuns";`
- `select "TestCode", count(*) from "TestOrders" where "TestCode" in (select "Code" from "TestDefinitions" where "WorkflowType" = 11) group by 1;`
- `select count(*) from "TestOrders" where "TestCode" in (select "Code" from "TestDefinitions" where "WorkflowType" = 7);`

Any non-zero result stops the merge until the user decides again (the delete
decision was made on local data).

### Stage 2 tests
- Full Postgres suite green (build first; the script uses `--no-build`).
- Migration Up and Down on a restored copy of LIMSV2.
- `tsc` and eslint clean; `npm test` green.
- Browser: menu entry gone, `/laboratory/system-suitability` falls to the
  not-found route, HPLC workspace assay + dissolution still complete.

## Out of scope
- Multi-analyte dissolution.
- Dissolution stages S2/S3 entry (unchanged from today: Stage 1 only).
- Renaming `StandardComparisonCalculator`.
