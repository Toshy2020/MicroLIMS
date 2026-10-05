# R — Retire the old ICP-OES / AAS calibration-run path Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove the old calibration-run / ElementalAssay path (code, screens, tables, `Cal*` test fields), delete its data, repair the production test `HPLC-FOLIC`, and close the hole that let a test's workflow type be changed without checks.

**Architecture:** Pure removal plus one guarded data migration. The new ICP workspace (I1–I4) replaces the path. Enum values stay as retired ints (`WorkflowType.ElementalAssay`, `EquationType.CalibrationCurve`, `EquipmentType.Aas`, existing `SignatureMeaning` values).

**Tech Stack:** ASP.NET Core, EF Core (PostgreSQL), React + TS, xUnit, Vitest.

**Spec:** `docs/superpowers/specs/2026-10-03-icp-gc-workspaces-design.md` §4.4 (slice R), D5, D6.

## Gate result (2026-10-05, read-only on Neon prod `shy-water-37349953` / branch `production`)
CalibrationRuns 0, CalibrationRunAnalytes 0, CalibrationRunChecks 0, CalibrationRunDocuments 0, ElementalAssay TestAnalyses 0,
ParameterResults with ValidityRecordItemId 0, AAS instruments 0 → deletion allowed. Prod ElementalAssay (WorkflowType 3) test definitions:
`Metals ICP` (no orders) and `HPLC-FOLIC` (EquationType 13 HplcMethodAssay, HplcMethodId set, order 774 with an HPLC run sample — a
mis-saved HPLC test). Prod audit: user 2 changed HPLC-FOLIC WorkflowType 12→3→5→3 on 2026-10-04 08:05 via
`PUT api/masterdata/test-definitions/{id}/workflow-type` (`TestWorkflowStepMasterDataService.UpdateWorkflowTypeAsync`, no validation; the
Test Master details dialog's Workflow Steps section saves on every dropdown change).

## User rulings (2026-10-05, binding)
- The R migration repairs HPLC-FOLIC: every test with `EquationType = HplcMethodAssay (13)` and an `HplcMethodId` gets `WorkflowType = HplcMethodAssay (12)`.
- Old elemental test definitions (local `Metals ICP`, `Minerals AAS`; prod `Metals ICP`) are **deleted**, with their item assignments, specifications and (local smoke) orders/analyses.

## Global Constraints
- Build/test with `--artifacts-path E:/MicroLIMS/rls-tmp/r-build` (the API may be running). Nothing on C:.
- Migration: `dotnet ef migrations add RetireCalibrationRuns --project backend/MicroLIMS.Persistence --startup-project backend/MicroLIMS.API --configuration Release`; apply to LIMSV2 only in Task 5 after a backup. Production migrates at API start, so the data steps must be safe on prod data (above) and idempotent.
- Never delete data that isn't part of the old path; the HPLC-FOLIC repair runs BEFORE any delete keyed on WorkflowType 3.
- Retired enum values stay (stored ints). Do not renumber enums.
- Frontend: remove dead code and menu/routes; no new UI beyond hiding the workflow-type dropdown where it no longer applies.

## Review Focus
1. HPLC-FOLIC-shaped rows (Workflow 3, Equation 13, HplcMethodId set, with orders/analyses/run samples) survive the migration and end as Workflow 12 — Task 3 Postgres test `Migration_RepairsMisSavedHplcTest_AndKeepsItsData`.
2. A real non-elemental test whose orders reference no calibration run is untouched — Task 3 same test.
3. The workflow-type endpoint can no longer move an equation-bound test (HplcMethodAssay, IcpMethodAssay, Dissolution, Titration, …) to another workflow — Task 1 `UpdateWorkflowType_EquationBoundTest_Throws`.
4. Creating a new ElementalAssay / CalibrationCurve test is refused — Task 2 `CreateTest_ElementalAssay_Refused`.
5. Existing HPLC/ICP/titration/micro suites stay green after the removal — Task 5 full Postgres suite.

---

### Task 1: Guard the workflow-type endpoint (root cause)

**Files:** `backend/MicroLIMS.Application/Services/MasterData/TestWorkflowStepMasterDataService.cs`; `frontend/src/modules/laboratoryConfiguration/masterDataSimple/TestMasterPage.tsx` (WorkflowStepsSection ~line 826-860); tests `backend/MicroLIMS.Tests/UnitTests/TestWorkflowStepMasterDataTests.cs` (new or existing file).

Rule: `UpdateWorkflowTypeAsync` only switches between the step-driven workflows `CountTest` and `Observation`, and only when the test's current workflow is one of them and its `EquationType` is not a dedicated equation (i.e. the equation is not HplcMethodAssay, IcpMethodAssay, Dissolution, Disintegration, WeightVariation, Titration, Measurement, Qualitative, Gravimetric — use the equation/workflow pairs in `TestDefinitionMasterDataService` as the reference). Otherwise throw `InvalidOperationException("The workflow type of this test is set by its test type; change it in the test settings.")`. Frontend: render the Workflow Type dropdown in WorkflowStepsSection only for tests whose workflow is CountTest or Observation, with only those two options.

- [ ] Tests: `UpdateWorkflowType_CountTestToObservation_Works`, `UpdateWorkflowType_EquationBoundTest_Throws` (HplcMethodAssay test → ElementalAssay refused, entity unchanged), `UpdateWorkflowType_ToNonStepWorkflow_Throws`.
- [ ] Implement; run tests; tsc/eslint/vitest for the frontend change. Commit `fix(masters): workflow type can only switch between step-driven workflows`.

### Task 2: Remove the old path from backend code

Delete: `Domain/Entities/CalibrationRun*.cs`, `Domain/Enums/CalibrationRunStatus.cs`, `Domain/Enums/ReportedConcentrationBasis.cs`, `Persistence/Configurations/CalibrationRun*Configuration.cs`, `Application/Services/CalibrationRunService.cs`, `Application/Interfaces/ICalibrationRunService.cs`, `Application/DTOs/CalibrationRunDtos.cs`, `API/Controllers/CalibrationRunController.cs`, `Application/Workflows/TestWorkflow/ElementalAssayRecorder.cs` and their tests (`CalibrationCurveRunPostgresIntegrationTests`, `ElementalAssayResultPostgresIntegrationTests`, `CalibrationCurveSliceS1Tests`, `CalibrationRunPayloadParsingTests`, `ElementalAssayResultTests`). Remove `CalibrationStandardLevelsHelper` only if nothing else uses it (I1 IcpMethodService does — keep it).
Edit (remove only the old-path parts): `TestDefinition` (`Cal*` fields, `CalInstrumentType`, `ReportedConcentrationBasis`, `CorrelationType` enum if unused afterwards), `ParameterResult.ValidityRecordItemId` (+ config FK/index; remove from SampleSummary DTO/service), `IMicroLimsDbContext`/`MicroLimsDbContext` DbSets, `UniqueIndexNames`, `MasterDataRequests`, `ConfigurationResponses`, `SampleSummaryDto`/`SampleSummaryService`, `TestOrderConformance`, `CertificateOfAnalysisBuilder`, `TestAnalyteMasterDataService` (CalInstrumentType filter), `TestDefinitionMasterDataService` (CalibrationCurve/ElementalAssay create+update branches → refuse with `Elemental assay tests are retired; use an ICP method assay test.`), `SpecificationService` (CalibrationCurve branch → refuse `Elemental assay tests are retired; use an ICP method assay test.` for specs on such tests), `TestWorkflowEngine`/`TestWorkflowContracts`/`TestWorkflowController` (elemental submit), `AnalysisWorkflows` (remove the ElementalAssay entry), `UserReferenceRegistry`, `UserSectionScopeService`/`IUserSectionScopeService` (calibration-run access), `ServiceCollectionExtensions`, `TestServiceFactory`, `SharedResultFoundationGenericTests`/`ReportConformanceTests` (drop elemental cases only), authorization matrix (regenerate; only CalibrationRun lines disappear).
Keep: enum values `WorkflowType.ElementalAssay`, `EquationType.CalibrationCurve`, `EquipmentType.Aas`, `SignatureMeaning.*` (comment "retired").

- [ ] Tests: `CreateTest_ElementalAssay_Refused`, `CreateTest_CalibrationCurveEquation_Refused`, `Spec_OnRetiredElementalTest_Refused` (seed a WorkflowType 3 definition directly).
- [ ] Build 0 errors; unit suite green (Postgres skips expected) with the old tests deleted; no migration yet (the model snapshot will differ — that's Task 3). Commit `refactor(icp): remove the old calibration-run and elemental assay path`.

### Task 3: Migration `RetireCalibrationRuns` (data + schema)

Create the migration after Task 2, then put these raw-SQL data steps at the **top** of `Up`, in this order, before the generated schema drops:
1. Repair: `UPDATE "TestDefinitions" SET "WorkflowType" = 12 WHERE "EquationType" = 13 AND "HplcMethodId" IS NOT NULL AND "WorkflowType" <> 12;`
2. Delete everything belonging to test definitions still at `WorkflowType = 3`: ParameterResults (and their child rows) of TestAnalyses of their orders; TestAnalyses; every other table with an FK to those TestOrders (discover with `SELECT conrelid::regclass, conname FROM pg_constraint WHERE confrelid = '"TestOrders"'::regclass AND contype = 'f'` on LIMSV2 and delete children first; ResultRecords projections included); TestOrders; Specifications and SampleTests by test code; child rows of the definitions (stage replicates, test analytes links, workflow steps — discover FKs the same way); the TestDefinitions.
3. `DELETE` from CalibrationRunChecks, CalibrationRunDocuments, CalibrationRunAnalytes, CalibrationRuns (after nulling `ParameterResults.ValidityRecordItemId`).
Then the generated drops: FK + index + column `ParameterResults.ValidityRecordItemId`, the four tables, the `Cal*`/`CalInstrumentType`/`ReportedConcentrationBasis` columns. `Down` recreates the schema only (data is not restored) — say so in a comment.

- [ ] Postgres integration test `RetireCalibrationRunsMigrationTests.Migration_RepairsMisSavedHplcTest_AndKeepsItsData`: on a fresh Postgres DB migrate to the migration BEFORE RetireCalibrationRuns, insert (a) an HPLC-FOLIC-shaped definition (Workflow 3, Equation 13, HplcMethodId) with a test order + analysis + parameter result, (b) an elemental definition (Workflow 3, Equation CalibrationCurve) with a SampleTest, Specification, order, analysis, result and a calibration run with an analyte linked from the result, (c) an unrelated CountTest definition with an order; migrate to RetireCalibrationRuns; assert (a) Workflow 12 and all its rows present, (b) all gone, (c) untouched, old tables gone. (Use the existing Postgres fixture's migrator; if it cannot target a specific migration, add a minimal helper with `IMigrator.Migrate(target)`.)
- [ ] Commit `feat(icp): migration retiring calibration runs and elemental tests`.

### Task 4: Remove the old path from the frontend

Delete `frontend/src/modules/calibrationRuns/` and `frontend/src/modules/testingWorkspace/ElementalAssayPanel.tsx`; remove their routes (`AppRoutes.tsx`) and the "Calibration Runs (ICP-OES / AAS)" menu entry (`menuConfig.ts`); remove the elemental branches from `TestWorkflowDialog.tsx`, `TestResultCards.tsx`, `SampleSummaryDialog.tsx`, `types/sampleSummaryTypes.ts`, `types/testWorkflowTypes.ts`; in `TestMasterPage.tsx` (+ `masterDataOptions.ts`, `useTestDefinitions.ts`) remove the Elemental Assay / Calibration Curve options and every `Cal*` / `calInstrumentType` / `reportedConcentrationBasis` field. HPLC/ICP/GC/titration/micro screens unchanged.
- [ ] `npx tsc -b --noEmit`, eslint on changed files, `npx vitest run` green. Commit `refactor(icp): remove the old calibration-run screens`.

### Task 5: Apply locally and full suite

- [ ] Backup `E:/MicroLIMS/backups/LIMSV2_before_retire_calibration_runs_<yyyymmdd>.dump`; apply the migration to LIMSV2; verify: the four tables gone, `Metals ICP`/`Minerals AAS` gone, no TestDefinition with WorkflowType 3, local HPLC tests with Equation 13 all Workflow 12.
- [ ] Full suite with Postgres (`bash .claude/scripts/run-postgres-tests.sh E:/MicroLIMS/rls-tmp/r-build`) → 0 failed, 0 skipped.
- [ ] Neon pre-merge note: production will run this migration at API start; the only prod rows it touches are HPLC-FOLIC (repaired) and `Metals ICP` (deleted with its SampleTests/Specifications — prod has no orders for it).
