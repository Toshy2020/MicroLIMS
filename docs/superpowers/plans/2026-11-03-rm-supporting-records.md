# RM Supporting Records Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build three supporting records for raw-material testing, plus the QC Manager role that one of them needs:

- outsourced tests, done by an external lab;
- not-applicable tests, approved by the Section Head and then the QC Manager;
- supplier-stated attributes, printed on the CoA.

**Architecture:**

- **Not-applicable test:** a master-data record per item and grade, with sequential two-role signatures. Once approved, it removes the test from ordering through the one grade filter all receipt paths share, and it prints on the CoA.
- **Supplier-stated attribute:** a master-data list per item and grade, printed on the CoA.
- **Outsourced test:** a signed flag on a physicochemical test order. The order then takes its result from a new recorder: values typed against the spec, with the external lab's report attached as a sample document. The result goes through normal review.

**Tech Stack:** ASP.NET Core 10, EF Core + Npgsql, xUnit; React + TS + MUI, vitest.

**Spec:** `docs/superpowers/specs/2026-10-25-raw-material-stm-support-design.md` (goal 8, "User decisions after the builder check", "Calls made after the builder check"). Retention quantity was already built in Plan 2.

## Global Constraints

- **GMP rules live in the Application layer.** The frontend shows state and never decides it.
- **Every new sensitive action is audit-trailed, and approvals carry electronic signatures.** The sensitive actions here are not-applicable create, approve and withdraw; outsourcing a test; and recording an outsourced result.
- **Not-applicable test: "approved with signatures (Section Head, then QC Manager). It is not ordered and shows on the CoA as 'Not applicable' with the reference."**
- **Not-applicable fields:** "per item (and grade), test + justification + criticality + immediate and long-term action".
- **Supplier-stated attributes:** "per item spec set, name + stated value, printed on the CoA as 'stated by supplier', no result entry".
- **Outsourced test:** "mark a test order as outsourced, record the external lab, attach its report, enter the result against the spec; it goes through normal review".
- **QC Manager role:** "gets only this approval for now", plus read access to the screens it approves from.
- **The not-applicable spec row is absent:** "the NA record is the trace".
- **An outsourced LOD or KF result "counts once entered"** as the sample's moisture for dried and anhydrous assays.
- **Enum values are appended, never inserted,** because they are persisted as int. This covers `RoleType`, `SignatureMeaning` and `SampleDocumentType`.
- **Builds and tests write artifacts under `E:/MicroLIMS/rls-tmp`.** The Postgres suite (`MICROLIMS_TEST_POSTGRES`) must pass with 0 skipped.
- **Leave these out of commits:** `docs/plans/task.md`, `.agent/`, `documents/`, `documentation/screenshots/*.zip`. Never push.

## Review Focus

1. **A not-applicable record approved after a sample was received** does not cancel existing orders, and only later receipts skip the test. Test in Task 2.
2. **One person signing both approvals** (a user with both permissions) is refused at the second signature. Test in Task 2.
3. **An outsourced result with no current external report attached** is refused, and so is one whose report was voided. Test in Task 5.
4. **A BP-only not-applicable record on a USP receipt** still orders the test, and a record with no grade skips it for every grade. Test in Task 2.
5. **Not-applicable and supplier rows on the CoA** never change the overall conclusion, even when every tested row conforms or one fails. Test in Task 4.

---

### Task 1: QC Manager role and the new permissions

**Files:**
- Modify: `backend/MicroLIMS.Domain/Enums/RoleType.cs`.
- Modify: `backend/MicroLIMS.Shared/Constants/RoleConstants.cs`.
- Modify: `backend/MicroLIMS.Shared/Constants/PermissionConstants.cs` (the `All` list at line 121 and the `Enforced` list at line 147).
- Modify: `backend/MicroLIMS.Persistence/Seed/DbSeeder.cs` (role rows at lines 27-32, grants at lines 535-595).
- Create: an EF data migration `AddQcManagerRole`, if roles and grants are not reseeded at startup. Check how the last permission was added; commit 41cd0c34 is the reference.
- Modify (frontend Role unions): `frontend/src/contexts/AuthContext.tsx:6` and `frontend/src/modules/authentication/types/authTypes.ts:1`.
- Modify (frontend labels): `frontend/src/components/Header.tsx:74` and `frontend/src/modules/auditSearch/utils/auditEnumLabels.ts:43`.
- Modify (user admin role picker, wherever the role options are listed): find it with `git grep -n "SectionHead" -- frontend/src/modules/userManagement`.
- Test: `backend/MicroLIMS.Tests/UnitTests/QcManagerRoleTests.cs`, and `authorization-matrix.txt` if grants change it.

**Interfaces:**
- Produces:
  - `RoleType.QcManager`, appended.
  - `RoleConstants.QcManager = "QcManager"`.
  - Permissions:
    - `PermissionConstants.NotApplicableTestsManage` ("tests.not-applicable.manage"), granted to SectionHead and SystemAdministrator.
    - `PermissionConstants.NotApplicableTestsFinalApprove` ("tests.not-applicable.final-approve"), granted to QcManager only.
    - `PermissionConstants.TestsOutsource` ("tests.outsource"), granted to SectionHead and SystemAdministrator.
  - QcManager also gets every permission Reviewer holds whose code ends in `.view` or `.read`, to see items, samples and the CoA, and nothing else. List them in the report.

- [ ] **Step 1: Write failing tests**
  - `QcManager_HoldsFinalApproveAndReadOnly`: the seeded QcManager role has `NotApplicableTestsFinalApprove` and no permission ending in `.manage`, `.record`, `.sign` or `.approve` other than it.
  - `SectionHead_HoldsNotApplicableManageAndOutsource`.
  - `Reviewer_DoesNotHoldTheNewPermissions`.
- [ ] **Step 2: Run them.** Expected: fail to compile or FAIL.
- [ ] **Step 3: Add the enum value, constants, seed rows, grants and migration.** Then add the frontend unions, labels and role picker entry "QC Manager".
- [ ] **Step 4: Run the tests and `AuthorizationMatrixTests`.** Expected: PASS. Regenerate the matrix with `MICROLIMS_WRITE_AUTH_MATRIX=1` only if a grant line changed, and check the diff.
- [ ] **Step 5: Commit** as `feat(roles): QC Manager role and supporting-record permissions`.

---

### Task 2: Not-applicable test records (backend)

**Files:**
- Create: `backend/MicroLIMS.Domain/Entities/NotApplicableTest.cs` and `backend/MicroLIMS.Domain/Enums/NotApplicableStatus.cs`.
- Create: `backend/MicroLIMS.Persistence/Configurations/NotApplicableTestConfiguration.cs` and migration `AddNotApplicableTests`.
- Create: `backend/MicroLIMS.Application/Services/NotApplicableTestService.cs` and its DTOs in `backend/MicroLIMS.Application/DTOs/NotApplicableTestDtos.cs`.
- Create: `backend/MicroLIMS.API/Controllers/NotApplicableTestController.cs`.
- Modify: `backend/MicroLIMS.Domain/Enums/SignatureMeaning.cs`, appending `NotApplicableHeadApproved` and `NotApplicableFinalApproved`.
- Modify: `backend/MicroLIMS.Application/Workflows/ProductWorkflowEngine.cs:313-322`, and the four callers of `AppliesToGrade`:
  - `ProductWorkflowEngine.cs:113`;
  - `AddLaboratoryService.cs:49`;
  - `SampleTestSelectionService.cs:159`;
  - `SampleCorrectionService.cs:496`.
- Test: `backend/MicroLIMS.Tests/UnitTests/NotApplicableTestServiceTests.cs`.

**Interfaces:**
- Consumes: Task 1 permissions.
- Produces:
  - Entity `NotApplicableTest` with these fields:
    - `Id`, `ItemId`, `Grade` (`string?`; null means every grade), `TestCode`;
    - `Justification`, `Criticality`, `ImmediateAction`, `LongTermAction` (all required strings);
    - `Reference` (`string?`; the form number, e.g. "QC form NA-01");
    - `Status` (Draft=0, HeadApproved=1, Approved=2, Withdrawn=3);
    - `HeadSignatureId?`, `FinalSignatureId?`, `CreatedByUserId`, `CreatedAt`.
  - Unique: one non-withdrawn record per (ItemId, Grade, TestCode).
  - Service methods:
    - `Task<NotApplicableTestDto> CreateAsync(int userId, CreateNotApplicableTestRequest r)`
    - `Task<NotApplicableTestDto> HeadApproveAsync(int userId, int id, string password, string? comment)`
    - `Task<NotApplicableTestDto> FinalApproveAsync(int userId, int id, string password, string? comment)`
    - `Task<NotApplicableTestDto> WithdrawAsync(int userId, int id, string password, string reason)`
    - `Task<List<NotApplicableTestDto>> ListForItemAsync(int itemId)`
  - Endpoints: `api/items/{itemId}/not-applicable-tests` (GET, POST) and `api/not-applicable-tests/{id}/head-approve | final-approve | withdraw` (POST).
  - `ProductWorkflowEngine.AppliesToGrade(IEnumerable<Specification> itemSpecs, IReadOnlyCollection<NotApplicableTest> notApplicable, string testCode, string? grade)`. It returns false when an Approved record matches the test code and either has a null grade or the same grade (case-insensitive). Otherwise it applies the existing rule.
  - Static loader `NotApplicableTestService.LoadApprovedAsync(IMicroLimsDbContext db, int itemId)`, used by all four callers.
  - DTO `NotApplicableTestDto`: every entity field, plus `TestDisplayName`, `HeadSignedBy`, `HeadSignedAt`, `FinalSignedBy`, `FinalSignedAt`.

Rules:
- **Create:** needs `NotApplicableTestsManage`. The test code must be assigned to the item. The grade, when given, must be one of the item's spec grades (`SpecificationService.NormalizeGrade`). Audit category Configuration.
- **Head approve:** needs `NotApplicableTestsManage` and status Draft.
- **Final approve:** needs `NotApplicableTestsFinalApprove` and status HeadApproved. The signer must not be the head signer, with the message "The QC Manager approval must come from a different person than the Section Head approval."
- **Withdraw:** needs `NotApplicableTestsManage`, from any status except Withdrawn. A withdrawn record no longer filters ordering.
- **Signatures:** use `IElectronicSignatureService.SignAsync` with entity type "NotApplicableTest".

- [ ] **Step 1: Write failing tests**
  - `Create_RefusesATestNotAssignedToTheItem` and `Create_RefusesAGradeTheItemDoesNotHave`.
  - `FinalApprove_BeforeHeadApprove_IsRefused`.
  - `FinalApprove_BySameUserAsHead_IsRefused`: a user holding both permissions signs both.
  - `ApprovedRecord_SkipsTheTestAtReceipt`: receive through `ProductWorkflowEngine.ReceiveAsync`; no TestOrder for that code.
  - `DraftOrHeadApprovedOrWithdrawn_DoesNotSkip`.
  - `GradedRecord_SkipsOnlyThatGrade`: a BP record on a USP receipt still orders the test, a BP receipt skips it, and a null-grade record skips both.
  - `ApprovingAfterReceipt_LeavesExistingOrders`.
  - `PeriodicRetestSelection_DoesNotOfferANotApplicableTest`, through `SampleTestSelectionService.EligibleAsync`.
  - `EachStep_WritesASignatureAndAuditEntry`.
- [ ] **Step 2: Run** `dotnet test backend/MicroLIMS.Tests --artifacts-path E:/MicroLIMS/rls-tmp/art-p5 --filter "FullyQualifiedName~NotApplicable"`. Expected: FAIL.
- [ ] **Step 3: Implement** the entity, migration, service, controller and the `AppliesToGrade` overload. Update all four callers and delete the old overload, so no path skips the filter.
- [ ] **Step 4: Run the filter above, plus** `FullyQualifiedName~ProductWorkflow|FullyQualifiedName~TestSelection|FullyQualifiedName~AddLaboratory|FullyQualifiedName~SampleCorrection|FullyQualifiedName~AuthorizationMatrix`. Expected: PASS. Update the authorization matrix for the new endpoints.
- [ ] **Step 5: Commit** as `feat(rm): not-applicable test records with two-role approval`.

---

### Task 3: Supplier-stated attributes (backend)

**Files:**
- Create: `backend/MicroLIMS.Domain/Entities/SupplierStatedAttribute.cs`, its configuration, and migration `AddSupplierStatedAttributes`.
- Create: `backend/MicroLIMS.Application/Services/SupplierStatedAttributeService.cs` and its DTOs.
- Create: `backend/MicroLIMS.API/Controllers/SupplierStatedAttributeController.cs`.
- Test: `backend/MicroLIMS.Tests/UnitTests/SupplierStatedAttributeServiceTests.cs`.

**Interfaces:**
- Produces:
  - Entity `SupplierStatedAttribute` with `Id`, `ItemId`, `Grade` (`string?`; null means every grade), `Name` (required, max 200), `StatedValue` (required, max 500) and `SortOrder`.
  - Service methods:
    - `ListForItemAsync(int itemId)`
    - `CreateAsync(int userId, SupplierStatedAttributeRequest r)`
    - `UpdateAsync(int userId, int id, SupplierStatedAttributeRequest r)`
    - `DeleteAsync(int userId, int id)`
    - `ForSampleAsync(int itemId, string? grade)`, which returns the null-grade rows plus the rows of that grade, ordered by `SortOrder` then `Name`.
  - Endpoints: `api/items/{itemId}/supplier-attributes` (GET, POST) and `api/supplier-attributes/{id}` (PUT, DELETE).
  - Writes use the same permission as editing specifications (`git grep -n "Authorize" -- backend/MicroLIMS.API/Controllers/SpecificationController.cs`). Each write writes an audit entry with field changes, as `MediaProductService.cs:152` does.

- [ ] **Step 1: Write failing tests**
  - `ForSample_ReturnsUngradedAndMatchingGradeOnly`.
  - `Create_RefusesUnknownGrade`.
  - `Update_AuditsOldAndNewValue`.
  - `Delete_IsAudited`.
- [ ] **Step 2: Run** with `--filter "FullyQualifiedName~SupplierStated"`. Expected: FAIL.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run the filter, plus** `FullyQualifiedName~AuthorizationMatrix`. Expected: PASS.
- [ ] **Step 5: Commit** as `feat(rm): supplier-stated attributes per item and grade`.

---

### Task 4: CoA rows for not-applicable tests and supplier attributes

**Files:**
- Modify: `backend/MicroLIMS.Application/DTOs/CertificateOfAnalysisDto.cs:94-112`.
- Modify: `backend/MicroLIMS.Application/Services/CertificateOfAnalysisBuilder.cs`, at `BuildSimpleRows` (:208, the verdict at :218) and `SimpleConclusionText` (:283).
- Modify: `frontend/src/modules/testingWorkspace/SampleCoaPage.tsx` (results table at :231-250), and its types.
- Modify: `ReportDocumentMapper.cs`, so the archived report prints the same rows, if it builds its own results table.
- Test: `backend/MicroLIMS.Tests/UnitTests/CertificateOfAnalysisBuilderTests.cs` and `frontend/src/modules/testingWorkspace/SampleCoaPage.test.tsx` (create it if missing).

**Interfaces:**
- Consumes: `NotApplicableTestService.LoadApprovedAsync` (Task 2) and `SupplierStatedAttributeService.ForSampleAsync` (Task 3).
- Produces:
  - `CoaSimpleRowDto` gains `string Kind`, one of "Tested", "NotApplicable" or "SupplierStated" (default "Tested").
  - `CoaSimpleResultDto` gains `List<CoaSimpleRowDto> SupportingRows`.
  - A not-applicable row:
    - `TestDisplayName`: the test name;
    - `Specification`: blank;
    - `Result`: "Not applicable (" + Reference + ")", or "Not applicable" when the reference is null;
    - no analyst.
  - A supplier row:
    - `TestDisplayName`: the attribute name;
    - `Specification`: blank;
    - `Result`: StatedValue + " (stated by supplier)";
    - no analyst.
  - Use only approved records matching the sample's item and grade.
  - `OverallComplies` and the conclusion read the tested rows only.

- [ ] **Step 1: Write failing tests**
  - `NotApplicableRow_PrintsWithReference_AndDoesNotChangeTheVerdict`: all tested rows conform, so the verdict is complies, plus one N/A row.
  - `SupplierRow_PrintsStatedBySupplier`.
  - `FailingTestedRow_StillFails_WithSupportingRowsPresent`.
  - `OnlyApprovedNotApplicableRecordsOfTheSampleGrade_Print`.
  - Frontend: `SampleCoaPage` renders "Not applicable (QC form NA-01)" and "20 mesh (stated by supplier)" below the results.
- [ ] **Step 2: Run** with `--filter "FullyQualifiedName~CertificateOfAnalysis|FullyQualifiedName~SampleReportDocument"` and vitest on the page test. Expected: FAIL.
- [ ] **Step 3: Implement.** In `BuildScope`, the builder needs the sample's `ItemId` and `Grade`. Load both lists once per CoA.
- [ ] **Step 4: Run the same commands.** Expected: PASS.
- [ ] **Step 5: Commit** as `feat(coa): not-applicable and supplier-stated rows`.

---

### Task 5: Outsourced test (backend)

**Files:**
- Modify: `backend/MicroLIMS.Domain/Entities/TestOrder.cs`, adding `OutsourcedToLab` (`string?`, max 200), `OutsourcedAt?` and `OutsourcedByUserId?`.
- Modify: `backend/MicroLIMS.Domain/Enums/SampleDocumentType.cs` (append `ExternalLabReport`) and `SampleDocument.cs` (add `int? TestOrderId`). Migration `AddOutsourcedTests`.
- Modify: `backend/MicroLIMS.Domain/Enums/SignatureMeaning.cs`, appending `TestOutsourced`.
- Create: `backend/MicroLIMS.Application/Workflows/TestWorkflow/OutsourcedRecorder.cs`.
- Modify:
  - `TestWorkflowController.cs` and `TestWorkflowRequests.cs`;
  - the engine and contracts that expose recorders, following how `UvRecorder` was wired;
  - `SampleDocumentController.cs` / `SampleDocumentService.cs`, so an upload takes an optional `testOrderId` and the `ExternalLabReport` type, under policy `TestWorkflowExecute` for that type;
  - the test-order summary DTO, adding `OutsourcedToLab`;
  - `CertificateOfAnalysisBuilder.Row`: the analyst column reads "External lab: {lab}".
- Test: `backend/MicroLIMS.Tests/UnitTests/OutsourcedRecorderTests.cs`.

**Interfaces:**
- Produces:
  - `POST api/test-workflow/{testOrderId}/outsource`
    - Body: `OutsourceTestRequest(string ExternalLab, string Reason, string Password)`.
    - Needs `TestsOutsource`.
    - Allowed only for a test whose workflow uses a test analysis (`AnalysisWorkflows.UsesTestAnalysis`) and whose order is Pending or InProgress with no active analysis.
    - Signs `TestOutsourced` and audits.
    - Micro tests with incubation steps are refused with "Only physicochemical tests can be outsourced."
  - `POST api/test-workflow/{testOrderId}/record-outsourced-result`
    - Body: `RecordOutsourcedResultRequest(DateTime ReportedAt, string ExternalReportNumber, int ReportDocumentId, List<OutsourcedParameterRequest> Parameters, string Password, string? Comment)`.
    - Each parameter is `OutsourcedParameterRequest(int SpecificationId, decimal? Value, string? Text)`.
  - Recorder rules:
    - The order is outsourced.
    - `ReportDocumentId` is a Current `ExternalLabReport` document of this sample and this test order.
    - Every spec of the test appears exactly once (`ValidateTestAnalysisOrderAsync`).
    - Range, NMT and NLT specs need `Value` and are judged with `SpecificationEvaluator.Evaluate`.
    - Qualitative specs need `Text` and are judged the way `SingleResultRecorder.RecordQualitativeResultAsync` judges them. Reuse that code; do not copy it.
    - Persist through `PersistTestAnalysisAndFinalizeAsync` with the test's own `WorkflowType` and equipment null.
    - `CalculationJson` = `{ quantity: "Outsourced", externalLab, externalReportNumber, reportDocumentId, reportedAt }`.
    - A test whose role is LossOnDrying or Water must still feed `SampleMoistureResolver`. Check what the resolver reads, and make the outsourced result look the same to it.

- [ ] **Step 1: Write failing tests**
  - `Outsource_ByAnalyst_IsRefused` and `Outsource_MicroTest_IsRefused`.
  - `Outsource_SignsAndAudits`.
  - `Record_NotOutsourced_IsRefused`.
  - `Record_WithoutReport_IsRefused`, `Record_WithVoidedReport_IsRefused` and `Record_WithAnotherOrdersReport_IsRefused`.
  - `Record_RangeAndQualitative_AreJudged`: 0.5 % against NMT 1 conforms, and 1.2 % is OOS.
  - `Record_OutsourcedLod_FeedsADriedAssay`: an outsourced LOD of 5 % lets a dried titration or UV assay sign with moisture 5.
  - `Coa_ShowsExternalLabAsAnalyst`.
- [ ] **Step 2: Run** with `--filter "FullyQualifiedName~Outsourced"`. Expected: FAIL.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run the filter, plus** `FullyQualifiedName~SampleDocument|FullyQualifiedName~AuthorizationMatrix|FullyQualifiedName~CertificateOfAnalysis`. Expected: PASS.
- [ ] **Step 5: Commit** as `feat(rm): outsourced tests with external lab report`.

---

### Task 6: Screens: item supporting records

**Files:**
- Modify: `frontend/src/modules/laboratoryConfiguration/items/components/ItemWorkspace.tsx`, adding a tab "Supporting Records" after Specifications (the tabs are at :186-200).
- Create: `frontend/src/modules/laboratoryConfiguration/items/components/NotApplicableTestsSection.tsx`, `SupplierAttributesSection.tsx`, their services in `.../items/services/`, and their tests.

**Interfaces:**
- Consumes: the Task 2 and Task 3 endpoints and DTOs.

Screens:
- **Not-applicable tests section:**
  - A table showing test, grade, criticality, status, head signer and time, QC Manager signer and time, and reference.
  - "Declare not applicable", for holders of `NotApplicableTestsManage`, opens a dialog with these fields:
    - test (the item's assigned tests);
    - grade (the item's grades, or "All grades");
    - justification, criticality, immediate action, long-term action, reference.
  - Row actions:
    - "Approve (Section Head)" on Draft;
    - "Approve (QC Manager)" on HeadApproved, for holders of the final permission;
    - "Withdraw".
  - Each action opens the shared `SignatureDialog`. The server message is shown on refusal.
- **Supplier attributes section:** a table of name, stated value and grade, with add, edit and delete for spec editors.
- Permissions come from the existing auth context helper; find it with `git grep -n "hasPermission" -- frontend/src/contexts`.

- [ ] **Step 1: Write failing tests**
  - Draft shows only the head action to a Section Head.
  - HeadApproved shows the QC Manager action only to a QC Manager.
  - Submit sends the fields.
  - Supplier add sends name, value and grade.
- [ ] **Step 2: Run** `npx vitest run src/modules/laboratoryConfiguration/items --maxWorkers=1 --testTimeout=40000`. Expected: FAIL.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run it again, plus** `npx tsc --noEmit`. Expected: PASS, 0 errors.
- [ ] **Step 5: Commit** as `feat(ui): item supporting records tab`.

---

### Task 7: Screens: outsourced test

**Files:**
- Create: `frontend/src/modules/testingWorkspace/OutsourcedPanel.tsx` and `OutsourcedPanel.test.tsx`.
- Modify: `frontend/src/modules/testingWorkspace/TestWorkflowDialog.tsx`:
  - show "Outsource test" for holders of `TestsOutsource` while the order is Pending or InProgress;
  - render `OutsourcedPanel` instead of the normal panel when `outsourcedToLab` is set.
- Modify: `TestWorkflowService.ts` and the types.
- Modify: the sample summary test cards (`TestResultCards.tsx` / `sampleSummary/AnalysisBlocks.tsx`), adding an "External lab: X" chip and a link to the report.

Screens:
- **Outsource dialog:** external lab and reason, then a signature.
- **`OutsourcedPanel`:**
  - an upload control for the external report (PDF or image, through the sample document endpoint with `testOrderId` and type `ExternalLabReport`);
  - report number and report date;
  - one row per spec: a number for Range, NMT and NLT, or text for Qualitative;
  - comment;
  - "Sign and record result", disabled until a report is uploaded and every row is filled.
- The judgement shown after signing comes from the server.

- [ ] **Step 1: Write failing tests**
  - Signing stays disabled until the report is uploaded.
  - The payload carries `reportDocumentId` and one parameter per spec, with a value or a text.
  - The outsource button is hidden from a user without the permission.
- [ ] **Step 2: Run** `npx vitest run src/modules/testingWorkspace/OutsourcedPanel.test.tsx --maxWorkers=1 --testTimeout=40000`. Expected: FAIL.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run it again, plus** `npx tsc --noEmit`. Expected: PASS.
- [ ] **Step 5: Commit** as `feat(ui): outsourced test entry`.

---

### Task 8: Whole-branch check

- [ ] **Step 1: Whole-branch review.** Sonnet, then agy `adversarial_review`, then Opus final, against goal 8 and the Review Focus. One fix wave, one re-review.
- [ ] **Step 2: Full suites.** Backend with `MICROLIMS_TEST_POSTGRES` (0 failed, 0 skipped) and vitest full, using `bash E:/MicroLIMS/rls-tmp/plan4-suites.sh`.
- [ ] **Step 3: Back up and migrate.** Back up LIMSV2 to `E:/MicroLIMS/backups/LIMSV2-before-rm-supporting-records-<date>.dump`; the migrations apply at API start.
- [ ] **Step 4: Create a local QC Manager test account.** Name it `e2e.qcmanager` and add its password to `frontend/.env.e2e.local`. Never commit it.
- [ ] **Step 5: Browser check in Brave**, on item E2E-RM-UV (297):
  1. As e2e.head, declare UV-PHOS not applicable for all grades with reference "QC form NA-01", and approve as Section Head.
  2. As the same head, the QC Manager action is absent. As e2e.qcmanager, approve it.
  3. Receive a sample: no UV-PHOS order.
  4. Add a supplier attribute "Particle size" = "20 mesh".
  5. As e2e.head, outsource the LOD-UI order to "External Lab A". As the analyst, upload a PDF, enter 5.0 % and sign. Then sign Riboflavin dried.
  6. Review and approve. On the CoA, LOD shows "External lab: External Lab A", plus "Not applicable (QC form NA-01)" and "20 mesh (stated by supplier)", with the verdict unchanged.
- [ ] **Step 6: Wrap up.**
  - Write `E:/MicroLIMS/rls-tmp/neon-precheck-plan5.sql`, a read-only check that the new tables and columns do not already exist and that `Roles` has no QC Manager row.
  - Write the follow-ups doc `docs/superpowers/plans/2026-11-03-rm-supporting-records-followups.md`.
  - Update memory, list the rulings, remove the workspace, and stop before Plan 6.
