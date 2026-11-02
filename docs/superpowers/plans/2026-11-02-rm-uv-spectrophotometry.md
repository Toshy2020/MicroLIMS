# RM UV Spectrophotometry (Plan 4) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A UV test type that covers the STM procedures: assay against a standard, assay from a specific absorbance, specific absorbance against a range, and absorbance not greater than a reference.

**Architecture:** A new `WorkflowType.UvSpectrophotometry` stored as TestAnalysis + ParameterResult, next to titration and gravimetric (not a run-based workspace like HPLC, because a UV reading has no run, column or SST). The method constants live on `TestDefinition`. A pure calculator does the sums, and a recorder in `SingleResultRecorder`'s style signs and stores them. Dried and anhydrous basis reuse `SampleMoistureResolver` and the return cascade.

**Tech Stack:** ASP.NET Core 10, EF Core + Npgsql, xUnit; React, TS, MUI, vitest.

**Spec:** `docs/superpowers/specs/2026-10-25-raw-material-stm-support-design.md` (goal 7).

## What the STMs require

| Material, test | Mode | Numbers for tests |
|---|---|---|
| Riboflavin 5-phosphate assay (444 nm, A1% 328, 73.0-79.0 % dried) | `SpecificAbsorbanceAssay` | W 0.1 g, total volume 5000 mL (1000 x 50 / 10), A 0.49, LOD 5 % gives as-is 74.70, dried 78.63 |
| Pyridoxine BP identification: three rows, 425-445, 175-195, 345-365 | `SpecificAbsorbanceValue` | W 2.5 g, total volume 250000 mL (50 x 50 x 100), A 0.435 gives 435 |
| Riboflavin 5-phosphate inorganic phosphate (700 nm) | `AbsorbanceLimit` | Test 0.120 against reference 0.150 conforms; 0.151 fails; equal conforms |
| Later materials (spec: "assay against a standard") | `StandardComparison` | A 0.50 / std 0.50, W 0.1 / std 0.1 g, equal volumes, purity 99.8, MC 0.1 gives the same as `StandardComparisonCalculator` |

Formulas (W in g, V in mL, 1 cm cell):
- `SpecificAbsorbanceAssay`: as-is % = A x V / (A1% x W)
- `SpecificAbsorbanceValue`: A(1%, 1 cm) = A x V / (W x 100)
- `StandardComparison`: `StandardComparisonCalculator.CalculatePreparationAssay(A, stdA, stdW, stdV, V, W, MC, purity)`, passing the volumes in the theoretical-weight positions only if that gives V/stdV; otherwise compute (A/stdA) x (stdW/W) x (V/stdV) x purity x (100-MC)/100 and prove it equal for equal volumes. The implementer reads that helper first and reports which.
- Basis: `HplcAssayCalculator.ApplyBasis` on the mean, moisture from the sample's own result.

## Decisions

- **Rulings already made by the user:** latest STM file wins; each grade runs its main method; retest selection, per-container and retention already exist and need nothing here.
- **Weights in g, as the STMs write them.** Not mg as HPLC does.
- **Total volume is one number on the test** ("flask x flask / aliquot"), with the sums shown as helper text. Dilutions are fixed by the STM, so the analyst doesn't type them. The value is copied into each result's `CalculationJson`, so later edits don't change signed results.
- **The analyst records the wavelength read**, required, 190-1100 nm, because the STMs give a range ("288-296 nm"). The expected wavelength goes in the parameter name.
- **Equipment is required** on a UV result.
- **Replicates** come from the test's `ReplicateCount` (1-30), as gravimetric does, and this field is on the Test Master for UV. Reported value is the mean.
- **Not built:** spectrum or printout upload (gravimetric and titration have none), blank absorbance (the instrument zeroes on the compensation liquid), a UV method master, workbook import (Plan 6).

## Global Constraints

- Never push or merge. Commit each tested slice locally. Do not touch `docs/plans/task.md`, `.agent/`, `docs/plans/2026-10-30-receiving-workspace-missing-items.md`.
- Artifacts on E:: `dotnet test backend/MicroLIMS.Tests --artifacts-path E:/MicroLIMS/rls-tmp/art-plan4`; migrations with `--configuration Release`. npm cache `E:/MicroLIMS/rls-tmp/npm-cache`.
- Memory is low (7.8 GB). Run targeted tests with `--filter`, vitest with `--maxWorkers=1 --testTimeout=30000`.
- Laboratory rules live on the backend; the panel's preview is display only. Every result is signed with the password and audited through `PersistTestAnalysisAndFinalizeAsync`.
- New enum members go last. `WorkflowType` and `EquationType` are stored as ints.
- Jest-dom matchers are not configured in vitest (use `toBeTruthy`, `getByText`).

## Review Focus

1. **Absorbance typed out of range** (0, negative, 40 for 0.40): refuse A <= 0 and A > 5 except `AbsorbanceLimit`, which accepts 0. Test in Task 2.
2. **LOD returned after a dried UV assay was signed:** the UV assay must be returned with it, like titration and HPLC. Test in Task 2.
3. **Test edited after signing** (A1% or volume changed): signed results keep the values they used. Test in Task 2.
4. **Test Master saved with a mode missing its constant** (no A1%, no volume, zero): refused on save, and refused again at record time for old rows. Tests in Tasks 1 and 2.
5. **Standard lot expired, out of stock or with no purity:** refused at record time, not only hidden from the picker. Test in Task 2.

---

### Task 1: Calculator, enums, Test Master fields

**Files:**
- Create: `backend/MicroLIMS.Domain/Enums/UvCalculationMode.cs`, `backend/MicroLIMS.Application/Helpers/UvCalculator.cs`, `backend/MicroLIMS.Tests/UnitTests/UvCalculatorTests.cs`, `backend/MicroLIMS.Tests/UnitTests/UvTestMasterValidationTests.cs`
- Modify: `Domain/Enums/WorkflowType.cs`, `EquationType.cs`, `ReadingKind.cs`, `Domain/Entities/AnalysisWorkflows.cs`, the `TestDefinition` entity, `Application/DTOs/MasterDataRequests.cs` (create and update requests), `Services/MasterData/TestDefinitionMasterDataService.cs` (validation, mapping, equation catalog near line 886), `TestWorkflowStepMasterDataService.cs` (dedicated equation list), `Services/Kpi/WorkflowProfileService.cs`, `TestDefinitionResponse`, migration `AddUvSpectrophotometryTest`

**Interfaces:**
- Produces: `enum UvCalculationMode { StandardComparison = 0, SpecificAbsorbanceAssay = 1, SpecificAbsorbanceValue = 2, AbsorbanceLimit = 3 }`; `WorkflowType.UvSpectrophotometry`; `EquationType.UvSpectrophotometry`; `ReadingKind.Absorbance = 7`.
- `TestDefinition`: `UvCalculationMode? UvCalculationMode`, `decimal? UvSpecificAbsorbance`, `decimal? UvTotalVolumeMl`, `decimal? UvStandardVolumeMl`.
- `UvCalculator.SpecificAbsorbanceAssay(decimal a, decimal totalVolumeMl, decimal specificAbsorbance, decimal weightG) -> decimal`
- `UvCalculator.SpecificAbsorbanceValue(decimal a, decimal totalVolumeMl, decimal weightG) -> decimal`
- `UvCalculator.StandardComparison(decimal a, decimal stdA, decimal stdWeightG, decimal stdVolumeMl, decimal totalVolumeMl, decimal weightG, decimal purityPercent, decimal moisturePercent) -> decimal`
- `UvCalculator.WithinReference(decimal a, decimal referenceA) -> bool`

- [ ] **Step 1: Write failing tests.** `UvCalculatorTests` pins the table above, rounded to 2 dp: `SpecificAbsorbanceAssay(0.49m, 5000m, 328m, 0.1m)` 74.70; `ApplyBasis` of that with `PercentDriedBasis`, 5 %, 78.63; `SpecificAbsorbanceValue(0.435m, 250000m, 2.5m)` 435; `WithinReference(0.150m, 0.150m)` true, `(0.151m, 0.150m)` false; `StandardComparison` with equal volumes equals `StandardComparisonCalculator.CalculatePreparationAssay` for the same inputs, and doubling `totalVolumeMl` doubles it. `UvTestMasterValidationTests`: each mode saves with its constants; refused with these exact messages: "A UV test needs a calculation mode.", "This UV mode needs a specific absorbance above 0.", "This UV mode needs a total volume above 0.", "A UV test against a standard needs a standard volume above 0."; `AbsorbanceLimit` needs no constants; UV fields on a non-UV test are refused; a replicate count outside 1-30 is refused; UV pairs only with `EquationType.UvSpectrophotometry`.
- [ ] **Step 2: Run, expect failure.** `dotnet test backend/MicroLIMS.Tests --artifacts-path E:/MicroLIMS/rls-tmp/art-plan4 --filter "FullyQualifiedName~Uv"`
- [ ] **Step 3: Implement,** following the gravimetric branches of `TestDefinitionMasterDataService` for pairing, including its update path. Decimal columns: A1% `numeric(10,4)`, volumes `numeric(14,4)`. Add the workflow to `AnalysisWorkflows` with display name "UV spectrophotometry" and to the KPI list. Generate the migration; check its Down and that it adds only these columns.
- [ ] **Step 4: Run again, expect pass.** Also run `--filter "FullyQualifiedName~TestDefinition|FullyQualifiedName~MasterData"`.
- [ ] **Step 5: Commit** `feat(uv): test type, calculator and Test Master fields`.

### Task 2: Recording, context, return cascade

**Files:**
- Create: `backend/MicroLIMS.Application/Workflows/TestWorkflow/UvRecorder.cs`, `backend/MicroLIMS.Tests/UnitTests/UvRecorderTests.cs`
- Modify: `TestWorkflowContracts.cs`, `TestWorkflowEngine.cs`, `API/Controllers/TestWorkflowController.cs`, `TestWorkflowRequests.cs`, `Services/SampleReviewService.cs` (`FindDependentAssaysAsync`), `Services/SpecificationService.cs` (UV branch near line 520), `backend/MicroLIMS.Tests` factory if a recorder must be registered, DI registration next to the other recorders

**Interfaces:**
- Consumes: Task 1; `ValidateTestAnalysisOrderAsync`, `PersistTestAnalysisAndFinalizeAsync`, `SampleMoistureResolver`, `HplcAssayCalculator.ApplyBasis`, `GravimetricCalculator.FormatReportedDisplay`.
- Produces: `POST /api/test-workflow/{id}/record-uv-result` with `RecordUvResultRequest(DateTime AnalysedAt, int EquipmentId, int? StandardMaterialId, decimal? StandardWeightG, decimal? StandardAbsorbance, List<RecordUvParameterRequest> Parameters, string Password, string? Comment)` and `RecordUvParameterRequest(int SpecificationId, decimal WavelengthNm, decimal? ReferenceAbsorbance, List<UvReplicateRequest> Replicates)`, `UvReplicateRequest(decimal? WeightG, decimal Absorbance)`.
- Stored: `ReadingKind.Absorbance` with `Value1` weight, `Value2` absorbance, `Value3` wavelength, `ComputedValue` the replicate's result. `CalculationJson` with `quantity: "Uv"`, `mode`, `wavelengthNm`, `totalVolumeMl`, `specificAbsorbance`, `standard` (lot id, code, purity, moisture, weight, absorbance, volume), `referenceAbsorbance`, `asIsValue`, `moisturePercent`, `replicates`, `reportedValue`. `ConditionsJson` holds only the moisture entry, in the shape `HplcMethodAssayRecorder` writes, so the cascade finds it.
- Spec rules: `AbsorbanceLimit` takes `LimitType.Qualitative`; the other modes take Range, NotMoreThan or NotLessThan. `ResultBasis`: `PercentAsIs`, `PercentDriedBasis`, `PercentAnhydrousBasis` or null; refuse `HplcMethodAnalyteId`, `IcpMethodElementId`, `RelatedSubstanceRow`.

- [ ] **Step 1: Write failing tests** in `UvRecorderTests`, each signing through the engine:
  - `Riboflavin_DriedBasis_UsesTheSampleLod`: 78.63, status within limits against 73-79, `ConditionsJson` moisture present.
  - `DriedBasis_WithoutLod_IsRefused`: `SampleMoistureResolver.MissingMessage`.
  - `Pyridoxine_ThreeRows_EachJudgedAlone`: three specs, 435 within, one row outside fails only that row.
  - `AbsorbanceLimit_EqualConforms_AboveFails`: status and reported display, with reference stored.
  - `StandardComparison_RefusesExpiredLot_NoPurity_OutOfStock_AndOtherSectionLot`.
  - `Absorbance_OutOfRange_IsRefused`: 0 and 5.01 refused, 5 accepted; 0 accepted in `AbsorbanceLimit`. Message: "Absorbance must be above 0 and at most 5."
  - `Wavelength_OutOfRange_IsRefused`: 189 and 1101.
  - `MissingEquipment_IsRefused`; `WrongReplicateCount_IsRefused`; `WrongPassword_IsRefused`; `FieldsForAnotherMode_AreRefused` (a standard on a specific absorbance test; a reference on an assay).
  - `SignedResult_KeepsItsConstants_WhenTheTestIsEdited`: change A1% after signing; stored JSON and value unchanged.
  - `ReturningTheLod_ReturnsTheDriedUvAssay`, and an as-is UV result on the same sample stays.
- [ ] **Step 2: Run, expect failure.** `--filter "FullyQualifiedName~UvRecorder"`
- [ ] **Step 3: Implement `UvRecorder`** with `RecordUvResultAsync(int testOrderId, UvPayload payload, int userId, string? ipAddress)` modelled on `RecordGravimetricResultCoreAsync`. Read old-row constants defensively and refuse with the Task 1 messages. Standard lot check mirrors `MaterialService.GetUsableReferenceStandardsAsync`, within the user's sections. Add `WorkflowType.UvSpectrophotometry` to `FindDependentAssaysAsync`.
- [ ] **Step 4: Run again, expect pass.** Also `--filter "FullyQualifiedName~SampleReview|FullyQualifiedName~Specification"`.
- [ ] **Step 5: Commit** `feat(uv): signed UV results with basis and return cascade`.

### Task 3: Screens

**Files:**
- Create: `frontend/src/modules/testingWorkspace/UvPanel.tsx`, `UvPanel.test.tsx`, `frontend/src/modules/testingWorkspace/uv/UvReviewPanel.tsx`, `UvReviewPanel.test.tsx`
- Modify: `TestWorkflowDialog.tsx` (branch next to Titration), `services/TestWorkflowService.ts`, `types/testWorkflowTypes.ts`, `types/sampleSummaryTypes.ts`, `kpi/types/workflowProfileTypes.ts`, `TestResultCards.tsx` and `sampleSummary/AnalysisBlocks.tsx` (render the panel per parameter), `laboratoryConfiguration/masterDataSimple/TestMasterPage.tsx` and a new `testMaster/UvTestFields.tsx`, `testMaster/testMasterConstants.ts`, `testMaster/WorkflowStepsSection.tsx` (read-only summary), `items/components/SpecificationParameterDialog.tsx`

**Interfaces:**
- Consumes: Task 2's endpoint and `CalculationJson`; `TestWorkflowService.getMoisture`; `formatMoistureLabel`; `MaterialService.getUsableReferenceStandards`; `getMissingMoistureMessage`; `GravimetricPanel` for layout, equipment picker and `SignatureDialog` use.
- Produces: `TestWorkflowService.recordUvResult(testOrderId, request)`.

- [ ] **Step 1: Write failing vitest tests.**
  - `UvPanel`: `SpecificAbsorbanceAssay` shows weight, absorbance and wavelength and no standard fields; `AbsorbanceLimit` shows reference and no weight; `StandardComparison` shows the lot, standard weight and standard absorbance; dried basis with no LOD shows the missing-moisture message and disables signing; with LOD shows "Loss on drying 0.5 % (from LOD-UI)"; the payload sent matches the request shape; the preview for the Riboflavin numbers reads 78.63.
  - `UvReviewPanel`: shows mode label, volume, A1%, wavelength, as-is value and moisture; shows the standard lot for `StandardComparison`; shows the reference for `AbsorbanceLimit`; no text contains "undefined".
  - Test Master: `UvTestFields` shows only the fields the chosen mode needs.
- [ ] **Step 2: Run, expect failure.** `npx vitest run src/modules/testingWorkspace/UvPanel src/modules/testingWorkspace/uv --maxWorkers=1 --testTimeout=30000`
- [ ] **Step 3: Implement.** Add "UV Spectrophotometry" to the physicochemical workflow list. Spec dialog: for a UV test, offer the limit types and bases from Task 2's rules and hide the analyte pickers. The preview may compute locally, labelled preview; the server's value is the record. Mobile-width layout follows `GravimetricPanel`.
- [ ] **Step 4: Run again, expect pass,** then `npx tsc --noEmit -p tsconfig.json` and eslint on the changed folders, clean.
- [ ] **Step 5: Commit** `feat(uv): entry, review and Test Master screens`.

### Task 4: CoA and phase check

**Files:**
- Modify: `backend/MicroLIMS.Application/Services/ReportDocumentMapper.cs` only if the check below shows wrong output; a test in the mapper's existing test file.
- Create: `E:/MicroLIMS/rls-tmp/neon-precheck-plan4.sql`, `docs/superpowers/plans/2026-11-02-rm-uv-spectrophotometry-followups.md`

- [ ] **Step 1: Write a failing or confirming mapper test:** a signed UV dried assay appears on the report with its reported value and limit, and shows the conditions in words, not as raw JSON. Fix the mapper if it fails.
- [ ] **Step 2: Whole-branch review:** a Sonnet reviewer and agy `adversarial_review`, then Opus final, against spec goal 7 and the Review Focus. One fix wave, one re-review.
- [ ] **Step 3: Full suites.** Backend with `MICROLIMS_TEST_POSTGRES` set: 0 failed, 0 skipped. `npx vitest run --maxWorkers=1 --testTimeout=40000`: 0 failed.
- [ ] **Step 4: Back up LIMSV2** to `E:/MicroLIMS/backups/LIMSV2-before-rm-uv-2026-11-02.dump`, then apply the migration.
- [ ] **Step 5: Browser check in Brave,** E2E accounts, on an RM test item:
  1. Test Master: create the Riboflavin assay test and the Pyridoxine identification test on screen, with the constants above.
  2. Riboflavin: refused before LOD; after LOD 5 %, 78.63 %, within 73-79; the reviewer sees the constants.
  3. Pyridoxine: three rows judged alone.
  4. Inorganic phosphate: 0.120 / 0.150 conforms, 0.151 / 0.150 out of specification.
  5. Returning the LOD returns the Riboflavin assay.
  6. A standard-comparison test with a usable lot.
  7. CoA shows the results.
- [ ] **Step 6:** Write the Neon check (no UV rows are expected: `select count(*) from "TestDefinitions" where "WorkflowType" = 15`, with the int the enum actually gets). Write follow-ups, update memory, remove the workspace, stop and report before Plan 5.
