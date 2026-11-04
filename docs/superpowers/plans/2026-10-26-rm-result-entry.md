# RM Result Entry (Plan 2 of 6) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Description tests can be recorded per received container with a derived "all containers comply" conclusion; titration assays on dried / anhydrous basis take LOD % / water % from the same sample's own LOD / KF result (and are sent back for recalculation when that result is returned); the physicochemical reviewer of a raw material records the retention quantity when passing review.

**Architecture:** Two new `TestDefinition` fields drive everything: `PerContainer` (Qualitative tests only) and `MoistureRole` (`LossOnDrying` / `Water`). The qualitative recorder accepts a container list when `PerContainer` is set and stores one `ResultReading` per container. A new `SampleMoistureResolver` finds the active moisture result of the sample; `TitrationRecorder` uses it instead of the typed value, and `SampleReviewService.ReturnToAnalystAsync` also returns the assays that used a returned moisture result. Retention quantity is two nullable columns on `Sample`, required by `CompleteReviewAsync` for a raw-material sample's FP section.

**Tech Stack:** ASP.NET Core 10, EF Core + PostgreSQL (Npgsql), xUnit, React + TypeScript + MUI.

**Spec:** `docs/superpowers/specs/2026-10-25-raw-material-stm-support-design.md` (goals 3, 4, 8 "Retention quantity"; calls "Dried / anhydrous source", "Retention quantity").

**Scope note:** today only titration supports a dried/anhydrous basis (HPLC and ICP spec validation refuses those bases; HPLC/ICP moisture inputs are for standards only). Plan 3 (HPLC) and Plan 4 (UV) call `SampleMoistureResolver` when they add sample-basis assays. No other workflow changes here.

## Global Constraints

- Clean Architecture: rules in `MicroLIMS.Application`; frontend only displays and collects.
- Every result write keeps the existing signature path (`PersistTestAnalysisAndFinalizeAsync` / `ReviewGateService.SignAndLogAsync`); retention quantity is part of the signed review and also written with `IAuditEventService.RecordUserEventAsync`.
- Existing finished-product / water / EM samples and tests without the new flags behave exactly as today.
- An assay on dried/anhydrous basis never finalizes with a moisture value not traceable to a result (spec anti-goal).
- Builds/tests: `dotnet build backend/MicroLIMS.sln --artifacts-path E:/MicroLIMS/rls-tmp/rm2-build`; `dotnet test backend/MicroLIMS.Tests --artifacts-path E:/MicroLIMS/rls-tmp/rm2-test` with `MICROLIMS_TEST_POSTGRES` set; migrations `dotnet ef migrations add <Name> --configuration Release` (the API runs locally and locks Debug bins).
- Frontend: `npm run build` and `npx eslint <changed files>` clean in `frontend/`.
- Commit each tested task locally on `feat/rm-stm-support`; never push. Commit messages end with the session's Co-Authored-By / Claude-Session lines.

## Review Focus

1. **Container count changed between drafts / mismatched list** (count 4, 3 entries, duplicate index, index 0): refused with a clear message, nothing written. Test in Task 2.
2. **LOD result exists but was returned (inactive) or is OOS**: inactive results are ignored (assay refused until re-recorded); an OOS LOD still supplies the value (it is the sample's true moisture). Test in Task 3.
3. **Moisture test with two parameter rows or no numeric value** (e.g. a "Complies" text result): the resolver refuses with a configuration message instead of picking one. Test in Task 3.
4. **Returning an LOD test whose dependent assay is still being worked (not `ResultEntered`)**: only `ResultEntered` dependents are returned; an assay not yet recorded simply picks up the new value later. Test in Task 3.
5. **Retention unit/value abuse** (0, negative, "bottles", supplied on a micro or finished-product review): refused with a clear message. Test in Task 4.

---

### Task 1: Test definition flags + spec-save rule

**Files:**
- Create: `backend/MicroLIMS.Domain/Enums/MoistureRole.cs`
- Modify: `backend/MicroLIMS.Domain/Entities/TestDefinition.cs`, `backend/MicroLIMS.Application/DTOs/MasterDataRequests.cs` (create ~175 and update ~250 test-definition requests), the test-definition response DTO, `backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs`, `backend/MicroLIMS.Application/Services/SpecificationService.cs` (titration basis validation ~406)
- Migration: `AddTestDefinitionContainerAndMoistureRole`
- Test: `backend/MicroLIMS.Tests/UnitTests/GravimetricQualitativeMasterDataValidationTests.cs` (extend), new `backend/MicroLIMS.Tests/UnitTests/MoistureRoleSpecificationTests.cs`

**Interfaces:**
- Produces: `enum MoistureRole { None = 0, LossOnDrying = 1, Water = 2 }`; `TestDefinition.PerContainer` (bool, default false), `TestDefinition.MoistureRole` (MoistureRole, default None); request/response fields `bool? PerContainer`, `MoistureRole? MoistureRole` (null on update = keep).

- [ ] **Step 1: Failing tests**
  - `PerContainer_OnlyAllowedForQualitative`: create with `WorkflowType.Gravimetric, PerContainer: true` throws "Per-container results are only available for qualitative tests."
  - `LossOnDryingRole_RequiresGravimetric` / `WaterRole_RequiresKarlFischerTitration`: LossOnDrying on a Titration test throws "Loss on drying role needs a gravimetric test."; Water on a non-KF titration throws "Water role needs a Karl Fischer titration test."
  - `DriedBasisTitrationSpec_NeedsAssignedLodTest`: item assigned only the titration test; saving a `PercentDriedBasis` titration spec throws "Assign a loss on drying test to this item before using a dried basis." Anhydrous → "Assign a water (Karl Fischer) test to this item before using an anhydrous basis." With the LOD test assigned (role LossOnDrying) the save succeeds — regardless of whether the LOD spec row exists yet.
  - Existing tests still pass (flags default off).
- [ ] **Step 2:** run, see FAIL.
- [ ] **Step 3: Implement** entity fields, enum, DTO plumbing (create/update/response), validation in the test-definition master-data service, and the item-assigned-test check in the titration branch of `SpecificationService` validation (look up `Item.AssignedTests` codes → `TestDefinitions` with the role).
- [ ] **Step 4:** migration `AddTestDefinitionContainerAndMoistureRole` (two non-null columns, defaults false / 0); Designer + snapshot generated by EF.
- [ ] **Step 5:** run the new and touched test files + full Postgres suite, PASS.
- [ ] **Step 6:** commit `feat(tests): per-container and moisture-role flags on test definitions`.

### Task 2: Per-container description result

**Files:**
- Modify: `backend/MicroLIMS.Application/Workflows/TestWorkflow/TestWorkflowContracts.cs:147-157`, `backend/MicroLIMS.Application/Workflows/TestWorkflow/SingleResultRecorder.cs:242-327`, `backend/MicroLIMS.Domain/Entities/QualitativeCalculationData.cs`, `backend/MicroLIMS.Application/DTOs/TestWorkflowRequests.cs:20-21` (API request mirror)
- Test: `backend/MicroLIMS.Tests/UnitTests/QualitativeWorkflowUnitTests.cs`, `backend/MicroLIMS.Tests/IntegrationTests/QualitativeResultPostgresIntegrationTests.cs`

**Interfaces:**
- Consumes: `TestDefinition.PerContainer` (Task 1).
- Produces: `record QualitativeContainerInput(int Index, bool Conforms, string? Observation)`; `QualitativeParameterInput` gains `List<QualitativeContainerInput>? Containers = null`; `QualitativePayload` gains `int? ContainerCount = null`; `QualitativeCalculationData` gains `int? ContainerCount` and `List<int>? FailedContainers`.

- [ ] **Step 1: Failing tests**
  - `PerContainer_AllComply`: count 3, all conform → `ReportedDisplay == "All 3 containers comply"`, `ComparisonStatus == WithinLimits`, 3 readings (Kind Replicate, Index 1..3, Passed true, Text = observation).
  - `PerContainer_OneFails`: count 4, container 2 fails with observation "Lumps" → `"Container 2 of 4 does not comply"`, `OutOfSpecification`, calculation `FailedContainers == [2]`; two failing → `"Containers 2, 4 of 4 do not comply"`.
  - Refusals (nothing written): count missing / <1 / >200 → "Enter the number of containers received (1 to 200)."; entries not exactly indexes 1..count → "Record a result for each container 1 to {n}."; failing container without observation → "Describe what container {i} shows."; containers sent for a non-per-container test → "This test is not recorded per container."; per-container test sent without containers → "Record a result for each container 1 to {n}.".
  - Parameter-level `Conforms` from the client is ignored for per-container tests (the conclusion is derived).
  - Postgres: per-container record end to end, readings persisted, order finalized.
- [ ] **Step 2:** run, FAIL.
- [ ] **Step 3: Implement** the branch in `RecordQualitativeResultCoreAsync`; non-per-container path untouched.
- [ ] **Step 4:** tests PASS, full Postgres suite PASS.
- [ ] **Step 5:** commit `feat(results): description results per received container`.

### Task 3: Moisture from the sample's own LOD / KF result

**Files:**
- Create: `backend/MicroLIMS.Application/Workflows/TestWorkflow/SampleMoistureResolver.cs`
- Modify: `backend/MicroLIMS.Application/Workflows/TestWorkflow/TitrationRecorder.cs` (~96-104 typed-LOD checks, ~256 basis call, ~333 snapshot), `backend/MicroLIMS.Application/Services/SampleReviewService.cs` (`ReturnToAnalystAsync` ~157-250), `backend/MicroLIMS.API/Controllers/TestWorkflowController.cs` (new GET), DI registration
- Test: new `backend/MicroLIMS.Tests/IntegrationTests/SampleMoisturePostgresTests.cs`; update `backend/MicroLIMS.Tests/UnitTests/TitrationRecorderTests.cs`, `TitrationPostgresIntegrationTests.cs`, `TitrationScenario.cs`

**Interfaces:**
- Consumes: `TestDefinition.MoistureRole` (Task 1).
- Produces: `record SampleMoisture(decimal Percent, int TestOrderId, int ParameterResultId, string TestCode, MoistureRole Role)`; `SampleMoistureResolver.ResolveAsync(int sampleId, MoistureRole role) -> Task<SampleMoisture?>` (throws `InvalidOperationException` on configuration errors); `GET api/test-workflow/{testOrderId}/moisture` → `{ role, percent, testCode } | null` for the titration screen. `TitrationPayload.LossOnDryingPercent` is kept in the contract but must be null.

- [ ] **Step 1: Failing tests**
  - Resolver: active `ParameterResult` of the sample's test order whose definition has the role → its `ReportedValue`; none → null; returned (inactive) results ignored; OOS LOD still returned; moisture test with ≠1 active parameter result or null `ReportedValue` → "The loss on drying test must report one numeric result." (water: "The water test must report one numeric result."); value outside 0 to <100 → same refusal.
  - Titration on `PercentDriedBasis` with LOD 2.5 recorded: result equals the engine with loss 2.5, snapshot holds `moisture = { role, percent, testOrderId, parameterResultId, testCode }`; anhydrous reads the Water test.
  - No LOD result yet → "Record the sample's loss on drying result before this assay." (anhydrous: "Record the sample's water content (Karl Fischer) result before this assay."); nothing written.
  - Client sends `LossOnDryingPercent` → "The loss on drying / water value is read from the sample's own result." Spec set mixing dried and anhydrous rows on one titration → "One titration cannot report on both dried and anhydrous basis."
  - Return path: returning the LOD order also returns every `ResultEntered` titration order of the same sample whose active analysis snapshot references the returned `parameterResultId`, with reason "Recalculate: the loss on drying result was returned." (water: "...the water content result was returned."), in the same unit of work; dependents in other states are untouched; returning a non-moisture test is unchanged.
  - Existing titration tests that typed a loss value move to a recorded LOD result in `TitrationScenario`.
- [ ] **Step 2:** run, FAIL.
- [ ] **Step 3: Implement** resolver, recorder change, return-path cascade, GET endpoint (same section-scope check as the other test-workflow GETs).
- [ ] **Step 4:** tests PASS, full Postgres suite PASS.
- [ ] **Step 5:** commit `feat(titration): dried/anhydrous basis reads the sample's own LOD/KF result`.

### Task 4: Retention quantity at physicochemical review

**Files:**
- Modify: `backend/MicroLIMS.Domain/Entities/Sample.cs`, `backend/MicroLIMS.Application/Services/SampleReviewService.cs:95-146`, `backend/MicroLIMS.API/Controllers/SampleReviewController.cs:12,33`, sample summary DTO (where `Grade` was added in Plan 1, `SampleSummaryDto.cs`)
- Migration: `AddSampleRetentionQuantity`
- Test: `backend/MicroLIMS.Tests/WorkflowTests/MultiSectionReviewApprovalTests.cs` (extend)

**Interfaces:**
- Produces: `Sample.RetentionQuantity` (`decimal?`, numeric(18,6)), `Sample.RetentionQuantityUnit` (`string?`, max 10); `CompleteSampleReviewRequest(string Password, string? Comment, int? SectionId, decimal? RetentionQuantity = null, string? RetentionQuantityUnit = null)`; `CompleteReviewAsync(..., decimal? retentionQuantity = null, string? retentionUnit = null)`; summary DTO `retentionQuantity`, `retentionQuantityUnit`.

- [ ] **Step 1: Failing tests**
  - RM sample, FP section (`PhyschemAreas.SectionCode`) review without retention → "Enter the retention quantity kept for this raw material." nothing signed.
  - Value ≤ 0 → "Retention quantity must be greater than zero."; unit not in `g, mg, kg, mL, L` (case-insensitive, stored in that canonical spelling) → "Retention unit must be g, mg, kg, mL or L."
  - Valid 50 g → stored on sample, review signed, one audit event `RetentionQuantityRecorded` with the value.
  - Micro section review of the same sample, or FP review of a finished product, with retention supplied → "Retention quantity is recorded only at the physicochemical review of a raw material."; without it → unchanged behaviour.
  - Re-review after a return overwrites the value (audit records old and new).
- [ ] **Step 2:** run, FAIL.
- [ ] **Step 3: Implement** columns, migration, service validation before signing, controller plumbing, summary DTO.
- [ ] **Step 4:** tests PASS, full Postgres suite PASS.
- [ ] **Step 5:** commit `feat(review): retention quantity recorded at raw-material physchem review`.

### Task 5: Screens — test master flags + per-container entry

**Files:**
- Modify: `frontend/src/modules/laboratoryConfiguration/masterDataSimple/TestMasterPage.tsx` (+ its types/service), `frontend/src/modules/testingWorkspace/QualitativePanel.tsx`, `frontend/src/modules/testingWorkspace/services/TestWorkflowService.ts:84-98`

**Interfaces:** Consumes Task 1 fields and Task 2 request shape.

- [ ] **Step 1:** Test master: "Recorded per container" switch shown for Qualitative tests; "Moisture role" select (None / Loss on drying / Water) shown for Gravimetric and Karl Fischer titration tests.
- [ ] **Step 2:** QualitativePanel for a per-container test: "Containers received" number field (1-200) builds rows 1..n, each with Complies / Does not comply and an observation field (required on does-not-comply); a read-only conclusion line ("All n containers comply" / "Containers 2, 4 of n do not comply") mirrors the backend wording; submit sends `containerCount` + `containers`. Other tests unchanged.
- [ ] **Step 3:** vitest for the conclusion line + payload shape; `npm run build`, eslint clean.
- [ ] **Step 4:** commit `feat(ui): per-container description entry and test master flags`.

### Task 6: Screens — moisture source + retention

**Files:**
- Modify: `frontend/src/modules/testingWorkspace/TitrationPanel.tsx` (~151, 218, 334, 542-553), `frontend/src/modules/testingWorkspace/SampleSummaryDialog.tsx` (~262), `frontend/src/modules/testingWorkspace/services/SampleSummaryService.ts:33-38`, sample summary types

**Interfaces:** Consumes Task 3 GET and Task 4 request/DTO fields.

- [ ] **Step 1:** TitrationPanel: replace the typed LOD field with a read-only line "Loss on drying 2.5 % (from LOD-xxx)" / "Water 0.4 % (from KF-xxx)" from the GET; when null, show "Record the sample's loss on drying result first." and disable submit; stop sending `lossOnDryingPercent`.
- [ ] **Step 2:** SampleSummaryDialog review: for a raw-material sample's FP section, "Retention quantity" number + unit select (g, mg, kg, mL, L), required before Sign; shown read-only in the summary once recorded (not on the CoA).
- [ ] **Step 3:** vitest for the two conditionals; `npm run build`, eslint clean.
- [ ] **Step 4:** commit `feat(ui): titration moisture from sample result, retention at review`.

### Task 7: Phase check

- [ ] Final whole-branch review (Opus) of Plan 2 commits against spec goals 3, 4, 8-retention and the Review Focus list.
- [ ] Full Postgres suite green; LIMSV2 backup, then migrations applied.
- [ ] Browser check on local item 295 (E2E RM Grade Check) after adding a per-container description test and an LOD test: record 3 drums with one failing → OOS conclusion; dried-basis titration refused before LOD, then reads the LOD value; return LOD → titration returned; RM physchem review asks for retention, micro review does not.
- [ ] Neon pre-merge note: prod query for titration specs with `ResultBasis` in (4, 5) — those items need an LOD/KF test assigned before this ships.
- [ ] Stop and report to the user before Plan 3.
