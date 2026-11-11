# Specification Set (A1) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fill `documents/` with a numbered URS and FS for every existing area, a calculation register, and a conflicts list, checked by CI.

**Architecture:** One markdown URS and one FS per area, written from the code as built. A Node script (`scripts/docs/check-documents.mjs`) validates IDs, cross-references and headers and runs in CI. Document Control's existing set moves into `documents/DocumentControl/` unchanged.

**Tech Stack:** Markdown; Node 22 (built-in `node:test`, no new packages); GitHub Actions.

**Spec:** `docs/superpowers/specs/2026-11-11-specification-set-design.md`

## Global Constraints

- No product code changes. Only `documents/`, `docs/` pointers, `scripts/docs/`, and `.github/workflows/ci.yml`.
- IDs: `URS-<AREA>-nnn`, `FS-<AREA>-nnn`, `CALC-nnn`, `CONF-nnn`; three digits; never reused.
- Area codes exactly: RCV, MIC, PAT, WAT, ENV, ACL, PHY, INS, MED, SOL, CRY, WST, INV, EQP, OOS, MON, REV, SIG, AUD, RPT, MDM, NTF, ADM, SEC.
- FS entries are written only from code that was read in this task; each names its service and method. Nothing from memory of how LIMS products usually behave.
- No organism name, limit or formula is stated unless it is in the code or configuration; configured values are described as "configured by the Section Head".
- Pharmacopoeia references are chapter pointers with status "Reference to be confirmed by QA". Never state that the code matches the chapter text.
- Every document status is "Draft" in this package. Only QA sets "Approved".
- Where code, `BusinessRules.md` and the guide disagree: FS states the code, and a `CONF-nnn` entry is added. Never resolve silently.
- Plain English, short sentences.
- One pull request per task from Task 3 on (one area group each), so the user and QA can review in parts.

## Review Focus

1. A URS entry with no FS entry and no conflict entry passes unnoticed → checker test `urs_without_fs_or_conflict_fails` (Task 1).
2. An ID reused in two files (for example after copying a template) → `duplicate_id_across_files_fails` (Task 1).
3. An FS entry describes behaviour the code does not have → each area task's "code location exists" check: checker test `fs_code_location_must_exist` verifies the named file path exists (Task 1).
4. Document Control IDs break when files move → `existing_dc_ids_still_resolve` (Task 2): every `DC-URS-`/`FS-1a-` ID mentioned in `backend/` and `frontend/src` is found under `documents/DocumentControl/`.
5. A calculation site is missed in the register → `every_calculation_site_is_registered` (Task 9): each file in the site list below appears in at least one `CALC` row.

## File Structure

| File | Responsibility |
|---|---|
| `scripts/docs/check-documents.mjs` | Validate IDs, cross-references, headers, code locations |
| `scripts/docs/check-documents.test.mjs` | Tests with fixture folders under `scripts/docs/fixtures/` |
| `documents/README.md` | Index: every document, version, status |
| `documents/_templates/URS-template.md`, `FS-template.md` | Header table and entry format |
| `documents/URS/URS-<AREA>.md` × 24 | Requirements |
| `documents/FunctionalSpecification/FS-<AREA>.md` × 24 | Functions |
| `documents/DesignSpecification/CalculationRegister.md` | `CALC-nnn` rows |
| `documents/Conflicts.md` | `CONF-nnn` entries |
| `documents/DocumentControl/*` | Moved set |

## Entry formats (used by every area task)

```markdown
### URS-MIC-001
**Requirement:** The system shall …
**GxP:** Yes
**Serves:** 21 CFR 11.10(e); USP <61> (to be confirmed by QA)
**Status:** Active

### FS-MIC-001
**Satisfies:** URS-MIC-001, URS-MIC-004
**Function:** inputs → rule → result; who may do it; what is audited.
**Code:** `backend/MicroLIMS.Application/Workflows/TestWorkflow/IncubationStepRecorder.cs` — `RecordCountTestAsync`
**Verified by:** reading (not run)
**Conflicts:** CONF-003
```

Header table fields, in order: Document number, Title, Version, Status, Author, Reviewer, Approver, Effective date; then a Change history table.

`Conflicts.md` entry: ID, area, status (Open / Decided), what the code does (with location), what the other source says (with location), question, decision and date.

`CalculationRegister.md` columns: ID | What | Rule as coded | Rounding and precision | Where (file — method) | Configured or coded | FS ID | Reference (pointer) | Reference status.

---

### Task 1: Document checker and CI step

**Files:**
- Create: `scripts/docs/check-documents.mjs`, `scripts/docs/check-documents.test.mjs`, `scripts/docs/fixtures/{valid,duplicate,orphan-urs,bad-ref,bad-header,bad-code-path}/…`
- Modify: `.github/workflows/ci.yml` (new job `documents`: `node --test scripts/docs` then `node scripts/docs/check-documents.mjs documents`)

**Interfaces:**
- Produces: `checkDocuments(rootDir: string, repoRoot: string): { errors: string[] }`; CLI exits 1 when `errors.length > 0`, printing one line per error as `<file>:<line> <message>`.
- Rules: ID pattern `^(URS|FS)-[A-Z]{3}-\d{3}$`, `^CALC-\d{3}$`, `^CONF-\d{3}$`; IDs unique across all files; each `**Satisfies:**` ID exists; each Active URS ID appears in some `**Satisfies:**` or in a `Conflicts.md` entry; each `**Code:**` path exists relative to `repoRoot`; each URS/FS file has all eight header fields; each `**Conflicts:**` ID exists. Files under `documents/DocumentControl/` and `_templates/` are skipped.

- [ ] **Step 1:** Write failing tests: `valid_set_passes`, `duplicate_id_across_files_fails`, `urs_without_fs_or_conflict_fails`, `fs_naming_unknown_urs_fails`, `missing_header_field_fails`, `fs_code_location_must_exist`, `withdrawn_urs_needs_no_fs`, `empty_documents_folder_passes`. Each asserts the exact error text contains the offending ID and file.
- [ ] **Step 2:** Run `node --test scripts/docs`. Expected: FAIL (module not found).
- [ ] **Step 3:** Implement `checkDocuments` and the CLI wrapper.
- [ ] **Step 4:** Run `node --test scripts/docs` → PASS; `node scripts/docs/check-documents.mjs documents` → exit 0.
- [ ] **Step 5:** Add the CI job. Commit `ci: add documents ID and cross-reference check`.

### Task 2: Skeleton, templates and Document Control move

**Files:**
- Create: `documents/README.md`, `documents/_templates/URS-template.md`, `documents/_templates/FS-template.md`, `documents/Conflicts.md`, `documents/DesignSpecification/CalculationRegister.md` (headers only)
- Move with `git mv` to `documents/DocumentControl/`: `MicroLIMS_Document_Control_URS_v1_1_Amendment.docx`, `Release_1d_Delta_URS.md`, `MicroLIMS_Document_Control_FS_Release_1a_v0_1.docx`, `ML-DC-FRS-1A-001.md`, `ML-DC-FRS-1B-001.md`, `ML-DC-FRS-1C-001.md`, `Release_1d_Delta_FRS.md`, `Release_1a/1b/1c_Requirements_Traceability_Matrix.md`, `Release_1d_RTM_Delta.md`, `MicroLIMS_Document_Control_Risk_Assessment_v1_0.xlsx`, `Release_1b_Risk_Impact_Assessment.md`, `Release_1b_Risk_Reconciliation_Report.md`, `Release_1c_Risk_Impact_Assessment.md`, `validation/ML-SYS-VP-001_Validation_Plan.md`, `Release_1b_Validation_Strategy.md`, `Release_1c_Validation_Strategy.md`, `WP4_Release_1a_OQ_UAT_Protocol.md`, `Release_1b_OQ_UAT_Plan.md`, `Release_1b_OQ_Execution_Report.md`, `Release_1c_OQ_Protocol.md`, `Release_1c_OQ_UAT_Plan.md`, `Release_1c_OQ_Execution_Record.md`
- Create: a pointer `.md` at each old markdown path ("Moved to `documents/DocumentControl/<name>`")
- Test: add `existing_dc_ids_still_resolve` to `scripts/docs/check-documents.test.mjs`

- [ ] **Step 1:** Write the failing test: collect every `DC-URS-\d+` and `FS-1[a-d]-\d+` in `backend/` and `frontend/src`; assert each appears in a markdown file under `documents/DocumentControl/`. IDs defined only inside the `.docx` are listed in `documents/DocumentControl/README.md` under "IDs defined in Word documents" and the test accepts those.
- [ ] **Step 2:** Run → FAIL (folder missing).
- [ ] **Step 3:** Move the files; write pointers; fix relative links inside moved files and in files that link to them (`grep -rl` each old name).
- [ ] **Step 4:** Write `documents/README.md` with a table of all 24 areas (URS file, FS file, version 0.1, status "Not started") and the Document Control set; record in `Conflicts.md` the four known items from spec 5.5 as `CONF-001`…`CONF-004`.
- [ ] **Step 5:** Tests and checker pass. Commit `docs: documents skeleton and Document Control move`.

### Tasks 3–8: Area groups

Each task follows the same steps for its areas. Sources per area (C = `backend/MicroLIMS.API/Controllers/`, S = `backend/MicroLIMS.Application/Services/`, W = `backend/MicroLIMS.Application/Workflows/`, BR = `documents/DesignSpecification/BusinessRules.md` lines):

| Task | Area | Read |
|---|---|---|
| 3 | MIC | C `TestWorkflowController`; W `TestWorkflow/IncubationStepRecorder.cs`, `SpecLimitParser`, `SpecificationEvaluator`; BR 33–51 |
| 3 | PAT | C `PathogenSessionController`; S `PathogenSession/`; BR 52–82 |
| 3 | REV | C `SampleReviewController`, `SampleApprovalController`; S `SampleReviewService`, `SampleApprovalService`, `SegregationOfDutiesGuard`; BR 157–187 |
| 4 | SIG | C `SignaturesController`; S `ElectronicSignatureService` |
| 4 | AUD | C `AuditController`; S `AuditSearchService`; `MicroLimsDbContext.SaveChangesAsync`, `AuditCaptureExcludedTypes`; BR 206–221 |
| 4 | SEC | C `AuthenticationController`, `AdminUserSessionsController`; S `AuthenticationService`, `SecurityAuditService`; BR 188–205 |
| 4 | ADM | C `AdminSettingsController`, `UserController`, `RoleController`; S `UserService`, `RoleService`, `SystemSettings/` |
| 5 | RCV | C `SampleController`; S `ReceivingService`, `ReceiptLabService`, `ReferenceNumberGenerator` |
| 5 | WAT | C `WaterController`; S `WaterService`; W `WaterWorkflowEngine.cs`; BR 83–101 |
| 5 | ENV | C `EMController`; S `EMService`; BR 102–119 |
| 5 | ACL | C `AfterCleaningController`; S `AfterCleaningService`; `Helpers/CleaningResidueCalculator.cs`; BR 120–135 |
| 5 | MON | C `MonitoringPlanController`, `MonitoringDeviationsController`; S `Monitoring/` |
| 6 | PHY | C `TestingWorkspaceController`, `ResultController`; S `TestingWorkspaceService`, `ResultService`; W `TestWorkflow/{Titration,Uv,Outsourced}Recorder.cs`; Helpers `TitrationEngine`, `DissolutionCalculator`, `DisintegrationStageEvaluator`, `MeasurementCalculator` |
| 6 | INS | C `HplcWorkspaceController`, `IcpWorkspaceController`; S `HplcRunService`, `IcpRunService`, `InstrumentImport/`; Helpers `HplcAssayCalculator`, `HplcSampleAssayEvaluator`, `HplcSstEvaluator`, `RelatedSubstancesEvaluator`, `StandardComparisonCalculator` |
| 6 | WST | C `WorkingStandardsController`; S `WorkingStandardService`; Helpers `WorkingStandardCalculator` |
| 6 | SOL | C `SolutionPreparationController`; S `SolutionPreparationService` |
| 7 | MED | C `MediaController`, `MediaEvaluationController`; S `MediaPreparationService`, `MediaGptReportService`; W `MediaEvaluationEngine.cs`; BR 136–156 |
| 7 | CRY | C `CryovialController`, `ReferenceOrganismController`; S `CryovialService`, `ReferenceOrganismService` |
| 7 | INV | C `MaterialController`, `MaterialReleaseController`, `EquipmentInventoryController`; S `MaterialService`, `MaterialReleaseService` |
| 7 | EQP | C `EquipmentMasterDataController`, `EquipmentPlanController`; S `EquipmentPlan/`, `EquipmentConfigurationService` |
| 8 | MDM | C `ItemController`, `SpecificationMasterDataController`, `TestDefinitionMasterDataController`; S `ItemService`, `MasterData/`, `MasterDataWorkbook/` |
| 8 | OOS | C `OosTrackingController`; S `OosTrackingService` |
| 8 | RPT | C `ReportingController`; S `ReportingQueryService`, `CertificateOfAnalysisBuilder`, `CoaTestOrdering`, `ReportDocumentMapper`, `DataExportAuditService`, `Spc/ControlChartCalculator.cs` |
| 8 | NTF | C `DashboardController`, `MessageController`; S `Dashboard/`, `DashboardNotificationService`; BR 222–237 |

A file listed above that is not found is located by name with a search before writing; if it does not exist, say so in the pull request, do not guess its content.

For each area in the task:

- [ ] **Step 1:** Read the listed files, the area's entities and entity configurations, and the names of its tests (for A2). Use the guide chapter only to learn the vocabulary.
- [ ] **Step 2:** Write `FS-<AREA>.md` from the template: one entry per endpoint group or rule — permission and section scope, inputs and validation, state transitions with their preconditions, calculations (pointing to `CALC` rows, added in Task 9), segregation-of-duties checks, signature requirement, optimistic-concurrency behaviour, what is audited.
- [ ] **Step 3:** Write `URS-<AREA>.md`: one testable user statement per need the FS entries serve; GxP flag; regulation pointer where one applies.
- [ ] **Step 4:** Add `CONF` entries for each disagreement or scientifically doubtful rule found; link them from the FS entry.
- [ ] **Step 5:** Update the area's row in `documents/README.md` to "Draft 0.1".
- [ ] **Step 6:** Run `node scripts/docs/check-documents.mjs documents` → exit 0.
- [ ] **Step 7:** Commit `docs(<area>): URS and FS draft`; open the task's pull request listing the areas, entry counts, and the new conflicts as questions for the user.

### Task 9: Calculation register

**Files:**
- Modify: `documents/DesignSpecification/CalculationRegister.md`; FS files (add `CALC` links)
- Test: `scripts/docs/check-documents.test.mjs` — `every_calculation_site_is_registered`

Sites (all under `backend/MicroLIMS.Application/`): `Workflows/TestWorkflow/IncubationStepRecorder.cs`, `Workflows/WaterWorkflowEngine.cs`, `Services/SpecLimitParser.cs`, `Services/SpecificationEvaluator.cs`, `Workflows/TestWorkflow/TitrationRecorder.cs`, `Helpers/TitrationEngine.cs`, `Workflows/TestWorkflow/OutsourcedRecorder.cs`, `Workflows/TestWorkflow/UvRecorder.cs`, `Helpers/HplcAssayCalculator.cs`, `Helpers/HplcSampleAssayEvaluator.cs`, `Helpers/HplcSstEvaluator.cs`, `Helpers/RelatedSubstancesEvaluator.cs`, `Helpers/CleaningResidueCalculator.cs`, `Helpers/StandardComparisonCalculator.cs`, `Helpers/DissolutionCalculator.cs`, `Helpers/DisintegrationStageEvaluator.cs`, `Helpers/MeasurementCalculator.cs`, `Helpers/WorkingStandardCalculator.cs`, `Services/Spc/ControlChartCalculator.cs`, `Services/Monitoring/OccurrenceStatus.cs`, `Workflows/MediaEvaluationEngine.cs`, `Services/ReportDocumentMapper.cs`.

- [ ] **Step 1:** Write the failing test: the list above is a constant in the test; each path must appear in the "Where" column of at least one row. A second test, `no_unlisted_rounding_site`, greps `backend/MicroLIMS.Application` for `Math.Round(` and `MidpointRounding` and fails for any file neither in the register nor in an "Not a laboratory calculation" list in the register with a reason.
- [ ] **Step 2:** Run → FAIL.
- [ ] **Step 3:** For each site, read the method and write one row per distinct rule: the formula in the code's own terms, the rounding mode and number of decimals exactly as coded, the comparison operator, the unit handling, and whether each value is coded or read from configuration. Reference column: chapter pointer only, status "Reference to be confirmed by QA".
- [ ] **Step 4:** Where two sites compute the same thing differently (for example the two `Compare` methods, or the report `Cfu` mapper versus the recorder), add a `CONF` entry.
- [ ] **Step 5:** Link each row to its FS entry and back. Tests and checker pass. Commit `docs: calculation register`.

### Task 10: Close-out

- [ ] **Step 1:** `documents/README.md`: every area "Draft 0.1"; counts of URS, FS, CALC and open CONF entries; how to export a PDF for QA signature (browser print of the rendered file) and where the signed copy is stored (Document Control).
- [ ] **Step 2:** Replace the six stub READMEs' text where it no longer matches; update `README.md` line on `documents/`.
- [ ] **Step 3:** Run `node --test scripts/docs` and `node scripts/docs/check-documents.mjs documents`; report exact counts. Report honestly that no statement was verified by running the system and that no pharmacopoeia reference is confirmed.
- [ ] **Step 4:** Hand the open `CONF` list and the three open items of the spec (QA names, site numbering, editions) to the user. Commit `docs: specification set index and close-out`.

## Cross-plan corrections (whole-programme review, 2026-11-11)

These corrections come from reviewing all plans together against the code. Where they differ from the text above, they win. Shared names are defined once in `docs/superpowers/plans/2026-11-11-build-order.md` (section "Programme conventions").

1. Add these conflicts found in the whole-programme review to `Conflicts.md` in Task 2: grade matching differs between services (case-insensitive, lower-casing, exact); limit comparison is implemented in several places (`SpecLimitParser`, `SpecificationService`, `PathogenSessionService`, `SpecificationEvaluator`); spec lookups bypass `SpecificationLookup` in five services; conformance is decided privately in `SampleApprovalService` and separately in `ReportDocumentMapper`.

**Decision CONF-003 (user, 2026-11-11):** equipment with no calibration due date counts as in calibration (the rule used for incubators today). `EquipmentCalibration.IsCurrent` keeps that behaviour.
