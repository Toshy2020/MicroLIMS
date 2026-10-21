# User Guide Reconciliation (Phase 12)

| Item | Value |
|---|---|
| Input review | `documentation/USER_GUIDE_REVIEW.md` (43 findings: F-A01..A13, F-B01..B13, F-C01..C17) |
| Guide reviewed | `documentation/USER_GUIDE.md` DRAFT 0.1, sources `documentation/guide/NN-*.md` |
| Ground truth | Source code at HEAD `c884b654` (branch `docs/user-guide`). Uncommitted global-search files were not treated as baseline. |
| Method | Every finding was re-checked in the code (paths below). The knowledge base (clusters, GAP file) was used for context only and was checked against the code where it mattered. Nothing was built or run, and no database was touched. |
| Rule applied | Only corrections that materially improve accuracy or usability are listed. Where a fact cannot be verified from the repository, the instruction is to write **REQUIRES CONFIRMATION**, not to guess. |

Decision values:
- **CONFIRMED**: the reviewer's problem and evidence hold.
- **PARTLY CONFIRMED**: the problem is real, but part of the reviewer's analysis or correction is wrong. The correction given here replaces the reviewer's.
- **REJECTED**: the guide is already accurate, or the change is not material.

All paths in section 1 are relative to `documentation/guide/`.

---

## 1. Summary table

| Finding | Severity (reviewer) | Decision | Final severity | Target file(s) | One-line correction |
|---|---|---|---|---|---|
| F-A01 | HIGH | CONFIRMED (+ recurrences) | HIGH | 02-purpose-scope.md, 04-roles-permissions.md, 43-faq.md | Receiving permissions cover Product/RM/PM only; any signed-in user can receive EM, Water and After Cleaning samples. |
| F-A02 | MEDIUM | CONFIRMED | MEDIUM | 10-search-filters.md, 11-sample-management.md | Fix the meaning of the **Cancelled / Voided** and **Pending Review** filters, and the Pending Approval label. |
| F-A03 | LOW | CONFIRMED | LOW | 04-roles-permissions.md | Add media evaluation to §4.3 row 10. |
| F-A04 | LOW | CONFIRMED (no text change now) | LOW | 06-user-interface.md, 10-search-filters.md | Release-gate check only: update §6.1 and §10.1 if global search ships. |
| F-A05 | LOW | CONFIRMED | LOW | 12-sample-receiving.md | "(see Section 13)" becomes "(see Section 14)". |
| F-A06 | MEDIUM | CONFIRMED | MEDIUM | 11-sample-management.md | Void strikes every non-superseded test, including approved and rejected tests. |
| F-A07 | HIGH | CONFIRMED | HIGH | 16-pathogen-testing.md | Rewrite §16.5 with the decision phase and the result form (**Submit Result**); there is no attachment control. |
| F-A08 | HIGH | CONFIRMED | HIGH | 14-testing-workspace.md | §14.7 must point to the Items > Preparation Configuration tab (already documented in §31.2). |
| F-A09 | HIGH | CONFIRMED | HIGH | 14-testing-workspace.md (also 23, 24, 25) | EM/Water/AC preparation has no permission and no laboratory check. |
| F-A10 | MEDIUM | CONFIRMED (+ §37.3) | MEDIUM | 04-roles-permissions.md, 16-pathogen-testing.md, 37-electronic-signatures.md | Mark the biochemical decision permission and segregation rule as having no screen. |
| F-A11 | MEDIUM | CONFIRMED | LOW | 14-testing-workspace.md | Add the Skip Wait confirmation step; the notice text always names the same two roles. |
| F-A12 | MEDIUM | CONFIRMED (upgraded) | HIGH | 16-pathogen-testing.md | Reset affects every test order of the sample and is not blocked for Under Approval or Approved samples; add a warning. |
| F-A13 | LOW | CONFIRMED | LOW | 13-test-orders-assignment.md | The Purpose must match the rule (not Approved or Rejected); state the segregation-of-duties side effect. |
| F-B01 | MEDIUM | CONFIRMED | MEDIUM | 18-return-rework-retest.md | §18.1 behaviour must cover result-entry tests too: active entries set aside, test returns to **Running**. |
| F-B02 | MEDIUM | CONFIRMED | MEDIUM | 18-return-rework-retest.md, 20-oos-tracking.md | Explain how the retest outcome is copied back: per-test status, one-section rule, recorded approver, no signature. |
| F-B03 | LOW | CONFIRMED | LOW | 19-approval.md | Approved only when no section is rejected **or waiting on a retest**. |
| F-B04 | MEDIUM | CONFIRMED | MEDIUM | 19-approval.md, 21-sample-report-coa.md | The archive label names the closing decision, not the final status. |
| F-B05 | LOW | CONFIRMED | LOW | 17-review.md, 19-approval.md | The dashboard counts follow the role's base type, not a permission. |
| F-B06 | HIGH | CONFIRMED | HIGH | 22-gpt-media.md | Recovery Min without Max makes every challenge NonConform; no Min makes every challenge Conform. |
| F-B07 | LOW | CONFIRMED | LOW | 22-gpt-media.md | Remove the message from §22.5; **Move** stays disabled until the reason has 5 characters. |
| F-B08 | MEDIUM | PARTLY CONFIRMED | MEDIUM | 18-return-rework-retest.md, 04-roles-permissions.md, 27-physicochemical-testing.md | The screen's "count test" check also matches physicochemical tests, so §18.1 ("count tests only") is wrong and §27 is right. |
| F-B09 | MEDIUM | CONFIRMED | MEDIUM | 29-material-stock.md | List every editable lot field; no reason or signature is asked; the unit is not converted. |
| F-B10 | MEDIUM | CONFIRMED | MEDIUM | 27-physicochemical-testing.md (new §27.10) | Add a short Methods Masters sub-section with verified facts only; fields REQUIRES CONFIRMATION. |
| F-B11 | LOW | PARTLY CONFIRMED | LOW | 11-sample-management.md, 44-glossary.md, 17-review.md | Add one label mapping (register label = badge label) instead of renaming statuses across 9 sections. |
| F-B12 | LOW | CONFIRMED | LOW | 18-return-rework-retest.md | Quote the singular helper text and note the plural for New Sample Required. |
| F-B13 | LOW | CONFIRMED | LOW | 26-reference-strains.md | "The screen offers ...": the server does not check for destroyed batches. |
| F-C01 | MEDIUM | CONFIRMED | LOW | 30-equipment.md | "always" becomes "in individual and grouped test steps"; add the shared TSB and pathogen messages. |
| F-C02 | MEDIUM | CONFIRMED | MEDIUM | 30-equipment.md | An incubator with no calibration due date is accepted. |
| F-C03 | HIGH | PARTLY CONFIRMED | HIGH | 32-laboratory-configuration.md | The stage name text is kept, but the stage link that drives specifications and replicate counts is cleared; add a warning to step 5. |
| F-C04 | HIGH | CONFIRMED (+ recurrences) | HIGH | 37-electronic-signatures.md, 42-troubleshooting.md, 43-faq.md | Replace "Absolute Rule"; remove the titrant example; limit "two people" wording to the listed actions. |
| F-C05 | HIGH | CONFIRMED | HIGH | 34-reports-kpis.md | Select All exports every matching record, whatever its status; the PDF holds at most 200 rows. |
| F-C06 | MEDIUM | CONFIRMED | MEDIUM | 38-document-control.md | The Document Compliance Profile drawer (and KPIs) is not limited to the user's own data. |
| F-C07 | MEDIUM | CONFIRMED | LOW | 34-reports-kpis.md | **Previous Period** can be selected but changes nothing. |
| F-C08 | LOW | CONFIRMED | LOW | 34-reports-kpis.md | The four categories are only the fallback list. |
| F-C09 | LOW | CONFIRMED | LOW | 34-reports-kpis.md | Five tiles filter; Media/GPT and Reference Strains open tabs; Custom Report clears. |
| F-C10 | LOW | CONFIRMED | LOW | 34-reports-kpis.md | **MasterDataChanged** becomes "master data changed". |
| F-C11 | LOW | REJECTED | – | – | The count of nine is correct (see §4). |
| F-C12 | LOW | CONFIRMED | LOW | 44-glossary.md | Active Equipment lists every asset in scope. |
| F-C13 | LOW | PARTLY CONFIRMED | LOW | 44-glossary.md | Source File access is by person; the reviewer's wording leaves out the approver. |
| F-C14 | LOW | CONFIRMED | LOW | 44-glossary.md | Set Point applies to incubators only; rename the term to **Incubation reads on time**. |
| F-C15 | MEDIUM | CONFIRMED | MEDIUM | 38-document-control.md | Remove "legal" from the guide's own voice. |
| F-C16 | LOW | CONFIRMED | LOW | USER_GUIDE.md header (assembly step) | Add review and approval rows, revision history, and the gaps file path. |
| F-C17 | LOW | CONFIRMED | LOW | 34-reports-kpis.md, 44-glossary.md | "no later than 4 hours after the expected read time". |
| R-NEW-01 | – | NEW | HIGH | 23-environmental-monitoring.md, 24-water-monitoring.md, 25-after-cleaning.md | "Who can use it" for EM/Water/AC receiving is wrong: the workspace **+ New Sample** button is shown to every lab member, and the server checks nothing. |
| R-NEW-02 | – | NEW | MEDIUM | 04-roles-permissions.md, 11-sample-management.md, 12-sample-receiving.md | *Global sample receiving* alone cannot receive Product/RM/PM samples; the endpoint also requires *Own-lab sample receiving*. |

---

## 2. Per-finding decisions and correction instructions

### F-A01: EM/Water/AC receiving has no permission check. CONFIRMED, HIGH

**Evidence:**
- `backend/MicroLIMS.API/Controllers/EMController.cs`: line 14 has class-level `[Authorize]` only; receive at :26, prepare at :33.
- `WaterController.cs:16,30,37` and `AfterCleaningController.cs:21,33,46` follow the same pattern.
- `git grep -i "scope|permission|Ensure.*Access"` over `EMService.cs`, `WaterService.cs`, `AfterCleaningService.cs` and the three `*WorkflowEngine.cs` files returns nothing.
- By contrast, Product receipt carries `[Authorize(Policy = SamplesReceiveOwnLab)]` (`SampleController.cs:82`).
- §7.1 (RECEIVING row) is correct: the menu entry and the `/receiving` route are gated on either receiving permission (`menuConfig.ts:85-92`, `AppRoutes.tsx:139-141`). It needs no change.

The same wrong claim also appears in `43-faq.md:88` (recurrence).

**Correction:**
- `02-purpose-scope.md` §2.4, last bullet (line 44): replace "Receiving is done by users who hold a receiving permission" with "Product, Raw Material and Packaging Material samples are received by users who hold a receiving permission. Environmental Monitoring, Water and After Cleaning samples can be received by any signed-in user (see §23–§25)."
- `04-roles-permissions.md` §4.2 closing line (line 53): use the same wording.
- `04-roles-permissions.md` §4.3, rows 7 (`Samples.Receive`) and 45 (`Samples.ReceiveOwnLab`): append "Applies to Product, Raw Material and Packaging Material samples only."
- `43-faq.md` question at line 88: replace "Sample receiving is done by users who hold a receiving permission" with "Product, Raw Material and Packaging Material samples are received by users who hold a receiving permission; Environmental Monitoring, Water and After Cleaning samples by any signed-in user".
- Keep the GAP-227 references.

### F-A02: Register status filter semantics. CONFIRMED, MEDIUM

**Evidence:**
- `frontend/src/modules/receiving/constants/receivingConstants.ts:47-58`: the option `RetestRequested` is labelled "Cancelled / Voided".
- `backend/MicroLIMS.Application/Services/TestingWorkspaceService.cs:134-155`:
  - **Pending Review** = sections that are UnderReview **or** UnderApproval;
  - **Cancelled / Voided** = RetestRequested, Cancelled and Voided together.
- `SampleRegisterTable.tsx:59-71`: UnderApproval is displayed as "Pending Approval".

**Correction:**
- `10-search-filters.md` §10.2: replace the note at line 29 with: "**Cancelled / Voided** finds samples that were voided, cancelled, or closed with a retest request. **Pending Review** finds samples with a section waiting for technical review **or** for the release decision, so it overlaps **Under Review** and **Under Approval**. In the table, samples under approval are labelled **Pending Approval**. (GAP-204)"
- `11-sample-management.md` §11.1 "What you see", line 26: complete the dropdown list. Add "Rejected" and "Cancelled / Voided", and the same overlap sentence.

### F-A03: TestWorkflow.Execute also gates media evaluation. CONFIRMED, LOW

**Evidence:** `MediaEvaluationController.cs:28` (`[Authorize(Policy = TestWorkflowExecute)]`); `DbSeeder.cs:461`. §22.3 (`22-gpt-media.md:202`) already says so.

**Correction:** `04-roles-permissions.md` §4.3 row 10: append "and media evaluation (growth promotion, indication and inhibition) steps".

### F-A04: Global search is uncommitted. CONFIRMED, LOW (no text change for the baseline)

**Evidence:**
- `git status`: untracked `GlobalSearchController.cs` and `frontend/src/components/search/`; `Header.tsx` is modified.
- §10.1 (`10-search-filters.md:4`) is correct for HEAD `c884b654`.

**Correction:** No edit in Phase 13. Add a release-gate line to the Phase 14 checklist: "If global search is merged before release, rewrite §6.1 (header list) and §10.1 sentence 1."

### F-A05: Wrong cross-reference. CONFIRMED, LOW

**Evidence:** `12-sample-receiving.md:98`; preparation is in §14.2–§14.4.

**Correction:** "(see Section 13)" becomes "(see Section 14)".

### F-A06: Void scope. CONFIRMED, MEDIUM

**Evidence:**
- `SampleCorrectionService.cs:273-277`: `Where(t => !t.IsSuperseded)`, with no status filter.
- The only status guard is "Sample is already voided." (:260).
- Banner text: `VoidSampleConfirmationDialog.tsx:128`.

**Correction:** `11-sample-management.md` §11.4:
- Purpose (line 234): "all of its open tests" becomes "all of its current (non-superseded) tests, including tests that are already approved or rejected".
- After the banner quote (line 245), add: "The banner says 'open tests', but the system voids every current test of the sample, including approved and rejected ones."

### F-A07: §16.5 biochemical procedure is wrong. CONFIRMED, HIGH

**Evidence:**
- `frontend/src/modules/testingWorkspace/pathogenSteps/BiochemicalTestPanel.tsx`:
  - :90-110 is the decision phase. It shows the alert "Confirmatory result: All Conforming. An analyst decision is required before this workflow proceeds.", the warning "Submitting as Detected without biochemical confirmation will be flagged for the reviewer.", and the buttons **Submit as Detected (skip biochemical)** and **Proceed to Biochemical Test**.
  - :112-153 is the result form. It has the required **Biochemical result** field (helper "For example an IMViC pattern or API strip result."), the question "Based on this result, is the organism Detected or Not Detected? …", the choices Detected / Not Detected, and the button **Submit Result**.
- `TestWorkflowService.ts:262-265` always sends `attachmentId: null`.
- `BiochemicalReviewTests.cs:41-52`: SubmitAsDetected sets the result to Detected and adds the flag `BiochemicalNotPerformed`.

**Correction:** `16-pathogen-testing.md` §16.5:
- **Purpose**: remove "optionally attach supporting evidence".
- **What you see**: replace it with two parts.
  - (1) **Decision**, shown only after an all-conforming confirmatory result: the two alerts above and the two buttons. **Submit as Detected (skip biochemical)** ends the test as **Detected** without a biochemical result, and the result is flagged for the reviewer.
  - (2) **Result form**: **Biochemical result** (required), **Detected** / **Not Detected**, and the button **Submit Result** (disabled until both are filled).
  - For an Inconclusive confirmatory result, the screen also shows "Confirmatory plating was Inconclusive and has been flagged for investigation in the audit trail. …".
- Remove the "exact field labels … not confirmed" reviewer note.
- **How to use it**:
  1. Open the biochemical step.
  2. If the decision buttons are shown, click **Proceed to Biochemical Test** (use **Submit as Detected** only when the laboratory procedure allows it).
  3. Enter the **Biochemical result**.
  4. Choose **Detected** or **Not Detected**.
  5. Click **Submit Result**.
  6. Click **Done / Close**.
  Delete the old steps 4 and 5.
- Keep the System behavior and Validation lists.
- Replace the reviewer-return note with the F-A10 wording.

### F-A08: Item preparation configuration screen exists. CONFIRMED, HIGH

**Evidence:**
- `ItemWorkspace.tsx:526-532` (tab 4).
- `ItemPreparationConfigurationSection.tsx:74,88,117-122,168,173-179`: the chips Approved / Pending Approval, "Approved by … on …", and the buttons **Save Configuration**, **Approve** (shown only for PendingReview), **Edit** and **Configure Preparation**.
- `ItemPreparationConfigurationController.cs:45-48`: `Items.Manage`.
- `ItemPreparationConfigurationService.cs:85-99`: no signature and no check against the creator.
- §31.2 (`31-items-test-master-specifications.md:80-130`) already documents this tab. §14.7 therefore contradicts §31.2 and §8.2.

**Correction:** `14-testing-workspace.md` §14.7:
- Replace "What you see" with: "The configuration is kept on the item: **Items** > (item) > **Preparation Configuration** tab (see §31.2 for the fields and buttons). A pending configuration shows the chip **Pending Approval** and the button **Approve**."
- Add **How to use it**:
  1. Open **Items** and select the item.
  2. Open **Preparation Configuration**.
  3. Check the values.
  4. Click **Approve**.
- Replace the reviewer note with: "Approval is a single unsigned click and can be made by the person who saved the configuration; the configuration is used for testing before approval. (GAP-214, GAP-704, GAP-707)"

**Knowledge base:** `knowledge/_clusters/C2-sample-testing.md` lines 24 and 121 ("UI screen still not located") are wrong. GAP-214 in `DOCUMENTATION_GAPS.md` should be narrowed to the approver-differs question (Phase 13 maintains the gaps file).

### F-A09: EM/Water/AC preparation has no laboratory scope. CONFIRMED, HIGH

**Evidence:**
- `EMController.cs:33-35`, `WaterController.cs:37-40` and `AfterCleaningController.cs:46-48` call no `EnsureSampleAccessAsync`. Compare `SamplePreparationController.cs:45,56`.
- The engines set `AssignedAnalystId = userId` on every created test (`EMWorkflowEngine.cs:97`, `AfterCleaningWorkflowEngine.cs:117`, Water likewise).

**Correction:**
- `14-testing-workspace.md` §14.3 "Who can use it" (line 162): "Any signed-in user. The system checks neither a permission nor the user's laboratory assignment. The user who prepares the sample becomes the assigned analyst of every test it creates, and so cannot review or approve that sample. (GAP-212, GAP-227)"
- In §23.3, §24.3 and §25.3 (lines 146 / 145 / 159), "Any signed-in user working in the Microbiology workspace" becomes "Any signed-in user (the system does not check the laboratory)".

### F-A10: Biochemical decision has no screen. CONFIRMED, MEDIUM

**Evidence:**
- `TestWorkflowService.ts:267-271`: the comment says "No frontend UI calls this yet".
- `git grep recordBiochemicalDecision frontend/src` finds the definition only.
- The endpoint is `TestWorkflowController.cs:527-529`.

**Correction:**
- `04-roles-permissions.md` §4.3 row 11: append "(no screen is provided for it)".
- §4.4, row "Pathogen biochemical decision": append "(no screen; reachable only through the system interface)".
- `16-pathogen-testing.md` §16.5 note: "A reviewer decision on a result submitted as Detected without biochemical confirmation exists in the system, but no screen offers it. (GAP-205)" Use the existing pathogen gap or a new one; see GAP-901.
- `37-electronic-signatures.md` §37.3, "Actions That Do Not Ask for a Signature" (line 117): change "**Biochemical decision**" to "**Biochemical decision** (no screen exists for it)".

### F-A11: Skip Wait confirmation and fixed notice. CONFIRMED, LOW

**Evidence:** `TestWorkflowDialog.tsx`:
- :164-165: the button needs `TEST_WORKFLOW_SUPERVISE`;
- :598-601: the button;
- :624-629: the confirmation dialog;
- :592-594: the fixed notice text.

The override records `MinimumDurationOverriddenByUserId/At` (`LaboratoryWorkResponses.cs:268-269`).

**Correction:** `14-testing-workspace.md` §14.6:
- Insert step 3: "Confirm the question 'Skip the remaining minimum incubation wait time for this step? This bypasses the wait only; the recorded incubation window is not changed.'". Renumber the steps that follow.
- Add to System behavior: "The notice always reads 'Section Head/System Administrator', whatever role the user has; the user who skipped the wait is recorded on the incubation."

### F-A12: Pathogen session reset reach. CONFIRMED, upgraded to HIGH

**Evidence:** `PathogenSessionService.cs`:
- :1268-1272: all `TestOrders` are loaded, with no filter.
- :1276-1295: only Voided and Rejected samples are refused.
- :1300: `testOrderIds` covers every order.
- :1314-1350: observations, step results, count readings, Results and incubations are deleted.
- :1363-1367: every order is set to Waiting/Pending.
- :1381-1384: only InTesting and UnderReview samples go back to Received.

The **Reset Session** button is gated only by `TEST_WORKFLOW_SUPERVISE`, not by sample status (`PathogenSessionDialog.tsx:56, 217`).

Consequence: an **Under Approval** or **Approved** sample can be reset. Its results are deleted, its tests return to Pending, and the sample keeps the status **Approved**. Physicochemical test orders on the same sample are also set to Pending. Their analysis entries are not in the delete list.

**Correction:** `16-pathogen-testing.md` §16.7:
- **Purpose**: "Let a supervisor clear all entries of a sample's testing session so that the tests can be started again. The reset applies to every test order of the sample, including count tests, physicochemical tests and superseded orders, not only the pathogen tests."
- **Before you start**: add "> **Warning:** The system does not stop a reset of a sample that is **Under Approval** or **Approved**. Its results are deleted, its tests return to Pending, and an approved sample keeps the status **Approved**. Do not reset a sample that has been reviewed or released."
- **System behavior**: add "Test orders of every laboratory on the sample are set to Pending."
- Keep the GAP-221 note and extend it (see GAP-221 update in §3).

### F-A13: Assign Analyst scope. CONFIRMED, LOW

**Evidence:** `SampleAssignmentService.cs:51-53` excludes only superseded, Approved and Rejected orders. Under `SegregationOfDutiesGuard.cs:33`, the assigned analyst counts as "performed".

**Correction:** `13-test-orders-assignment.md` §13.3:
- Purpose (line 4): "all open test orders" becomes "every current test order that is not Approved or Rejected (including tests whose results are already entered, and voided or cancelled tests)".
- Add to System behavior: "A user assigned to a test whose results are already entered counts as having performed it and can no longer review or approve the sample."

### F-B01: Return to Analyst for result-entry tests. CONFIRMED, MEDIUM

**Evidence:**
- `SampleReviewService.cs:188-189` accepts CountTest and analysis workflows.
- :191-213 is the count branch: readings are set inactive and the incubation is reopened.
- :214-233 is the analysis branch: TestAnalyses and ParameterResults are set inactive.
- :246-250: the target step is `Running` for analysis workflows and `Incubating` for count tests.

As F-B08 shows, the screen does offer this action for physicochemical tests, so the gap is not confined to the API.

**Correction:** `18-return-rework-retest.md` §18.1:
- Purpose: "Send a count test or a physicochemical result-entry test back to the assigned analyst…".
- System behavior: state that the count-test bullets apply to count tests, and add "For a physicochemical (result-entry) test, the active analysis entries and parameter results are set aside, no incubation is reopened, and the test goes back to **Running**."
- Status: "Count test: **Incubating**. Result-entry test: **Running**."

### F-B02: Retest outcome copied to the original. CONFIRMED, MEDIUM

**Evidence:** `SampleApprovalService.cs`:
- :552-554: copied only when the retest has exactly one section;
- :560-580: RetestRetainedSample takes the retest decision; NewSampleRequest takes "both siblings approved";
- :597-605: the approver and time on the original are set to the deciding Section Head, and the comment at :583-589 says no signature is recorded;
- :617-626: each original test is set Approved or Rejected from its own result;
- :646: superseded orders are deliberately included;
- :635: the archive label is "Sample {outcome} (OOS resolved)".

GAP-018 already covers the missing signature.

**Correction:**
- `18-return-rework-retest.md` §18.2, "What happens next" (and the note at line 82): add "When the retest is decided, the original sample's section takes the outcome (Retest Retained Sample: the retest's outcome; New Sample Required: Approved only when both new samples are approved). Each original test is then marked Approved or Rejected from its own recorded result, so the test that triggered the retest stays **Rejected**. The original section records the Section Head who decided the retest as its approver; no new signature is applied to the original. This happens only when the retest sample covers one laboratory section."
- In §18.3 (line 133) and `20-oos-tracking.md` §20.1 (line 42), add "(see §18.2)" after "copied to the original sample's section".

**Knowledge base:** `knowledge/_clusters/C3-review-approval.md` BR-APPROVAL-021 should carry the per-test rule if it does not already.

### F-B03: Sample status after Approve. CONFIRMED, LOW

**Evidence:** `SampleSectionRollup.cs:78-87`. The order is open sections first, then Rejected, then RetestRequested, then Approved.

**Correction:** `19-approval.md`:
- line 75: "…and none is rejected or waiting on a retest, the sample becomes **Approved**. If another section is waiting on a retest, the sample becomes **Retest requested** (shown as **Cancelled / Voided** in the register)."
- line 93: update to match.

### F-B04: Archive label versus final status. CONFIRMED, MEDIUM

**Evidence:** `SampleApprovalService.cs:522` (`$"Sample {decision}"`), :528-535 (archives whenever the sample is Approved or Rejected). `SampleSectionRollup.cs:78-87`: an earlier Rejected section makes the sample Rejected when the last section approves.

**Correction:**
- `19-approval.md` lines 76 and 130: "…archived copy labelled with the decision that closed the sample (for example 'Sample Approve')".
- `21-sample-report-coa.md` §21.3 (line 149 area): add "The label names the decision that closed the sample, not its final status. For a sample tested by more than one laboratory, a copy labelled 'Sample Approve' can belong to a sample whose final status is **Rejected**, because another laboratory rejected it earlier. Check the status on the record. (GAP-902)"

### F-B05: Dashboard counts by base role type. CONFIRMED, LOW

**Evidence:** `DashboardNotificationService.cs:171` (approval count: SectionHead and SystemAdministrator) and :185 (review count). The `RoleType` here is the base type, so custom roles built on those base types also see the counts.

**Correction:** `17-review.md:16` and the matching sentence in §19.1: "for users whose role is, or is based on, Reviewer, Section Head or System Administrator (the count follows the role type, not the permission)".

### F-B06: Growth Promotion recovery band trap. CONFIRMED, HIGH

**Evidence:**
- `MediaEvaluationEngine.cs:229-231`: `!Min.HasValue || (recovery >= Min && recovery <= Max)`. A null Max always evaluates false.
- `MediaMasterDataService.cs:89-93`: Min > Max is checked only when both values are present.

The knowledge base states this correctly (`C5-media-strains-stock.md:290`); the guide left it out.

**Correction:** `22-gpt-media.md`:
- §22.1 step 4 (line 54): "For Growth Promotion, enter both **Recovery Min (%)** and **Recovery Max (%)**. If you enter only a minimum, every challenge is judged **NonConform** and the lot is rejected. If you leave the minimum empty, every challenge is judged **Conform** whatever the recovery."
- §22.3 System behavior (line 241): append the same two sentences.
- Add a reviewer note referencing GAP-505 and GAP-903.

### F-B07: Media storage reason message. CONFIRMED, LOW

**Evidence:**
- `MediaStoragePanel.tsx:130` (valid only when the reason has at least 5 characters) and :196-197 (the field turns red; helper "Recorded in the audit trail.").
- The message itself exists only in `MoveCryovialStorageDialog.tsx:76` and `ThawVialReasonDialog.tsx:171`.

**Correction:** `22-gpt-media.md:422`: replace the message with "The **Move** button stays disabled until the reason has at least 5 characters." Line 388 can stay.

### F-B08: Return to Analyst for physicochemical tests. PARTLY CONFIRMED, MEDIUM

The contradiction between §18.1 and §27 is real. The reviewer's analysis of which tests get the button is wrong.

**Evidence:**
- `TestResultsSection.tsx:38-44`: the button shows when a test is `ResultEntered`, not superseded, and "count-like". "Count-like" means it has count readings, or it has `Result` rows with no biochemical, location or pathogen data.
- Every physicochemical recorder writes a `Result` row:
  - `TestWorkflowSupport.cs:733`, inside `PersistTestAnalysisAndFinalizeAsync`, is used by `SingleResultRecorder` (Measurement, Gravimetric, Qualitative), `TitrationRecorder`, `HplcMethodAssayRecorder` and `IcpMethodAssayRecorder`;
  - `DissolutionRecorder.cs:194,352`, `DisintegrationRecorder.cs:202,359` and `WeightVariationRecorder.cs:239,431` write their own.
- `SampleSummaryTestOrderBuilder.cs:65` passes those rows to the screen as `results`.

So the screen offers **Return to Analyst** for physicochemical tests too. §27's "Returned for revision" text is right, and §18.1, the §4.2 Reviewer text and GAP-300 are wrong.

Because the screen heuristic decides this rather than a stated rule, verify it in the running application when the §18 screenshots are taken.

**Correction:**
- `18-return-rework-retest.md` §18.1:
  - Before you start (line 11): "The test is a count test or a physicochemical result-entry test whose result is entered. Pathogen, location (EM/Water/After Cleaning) and biochemical tests do not offer the button."
  - Remove the reviewer note at line 16 (GAP-300).
  - Step 2: "Find the test and expand it".
  - Keep the dialog subtitle quote "Return count test for recount or result re-entry" (it is screen text).
- `04-roles-permissions.md` §4.2 Reviewer (line 47) and §4.3 row 5: "returning count tests" becomes "returning count and physicochemical result-entry tests".
- `27-physicochemical-testing.md` §27.2: add after line 119 "(see §18.1; the test goes back to **Running**)".

**Knowledge base:**
- `knowledge/_clusters/C3-review-approval.md` lines 218 and 336, and `knowledge/_clusters/R3-review.md` lines 12 and 109, say "UI offers it for count tests only". This is wrong.
- GAP-300 in `DOCUMENTATION_GAPS.md` should be rewritten (see §3).

### F-B09: Unsigned lot edits. CONFIRMED, MEDIUM

**Evidence:** `MaterialService.cs:211-299` (`UpdateAsync`):
- only working standards are refused (:219-220);
- media product changes are refused once lots exist (:237-238);
- otherwise these fields are all overwritten: type, custom type, name, manufacturer, batch, receiving date, expiry, code, location, ATCC, organism, quantity received, unit, minimum stock, purity and moisture;
- `QuantityRemaining += receivedDelta` (:291);
- no reason, password or signature is asked;
- the change is audited through the general field audit.

**Correction:** `29-material-stock.md` §29.5:
- Purpose: "Correct the details of a lot: material type, name, manufacturer, code, batch number, receiving and expiry dates, storage location, organism and ATCC number, quantity received, unit, minimum stock level, and purity and moisture for standards."
- System behavior:
  - "No reason, password or signature is asked; the change is recorded in the audit trail."
  - "Changing **Unit** does not convert the quantities."
  - "Changing **Quantity Received** changes **Qty Remaining** by the same amount, which can take it below zero."
  - "Working standards cannot be edited here; the media product of dehydrated media cannot be changed once lots were prepared from it."
- Add a reviewer note: "Purity, moisture and expiry of a lot can be changed after the lot was used in a calculation, without a reason or signature. (GAP-904)"

### F-B10: Methods masters have no procedure. CONFIRMED, MEDIUM

**Evidence:**
- `COVERAGE_MATRIX.md:58-59` and `USER_GUIDE_STRUCTURE.md:35` assign MOD-HPLCM-001 and MOD-ICPM-001 to §27, and no sub-section covers them.
- Verified facts in `knowledge/_clusters/C6-physchem.md:44-53`:
  - routes `/laboratory-configuration/hplc-methods` and `/icp-methods`, guarded by *Master Data Management* (MasterData.Manage);
  - reading is open to any signed-in user;
  - create, edit and activate require MasterData.Manage;
  - edits require a reason ("A reason is required.") and are not signed (GAP-615);
  - purpose lists in C6:45 and :51.
- Field-level screen labels are not enumerated in the knowledge base (GAP-043).

**Correction:** Add `27-physicochemical-testing.md` §27.10 "Chromatography and ICP Methods Masters". Write only:
- **Purpose**: the purpose lists from C6:45 and :51.
- **Who can use it**: *Master Data Management* (by default: Section Head, System Administrator); others can view the methods inside the workspaces.
- **Navigation**: **Laboratories** > **Physicochemical Configuration** > **HPLC Methods** / **ICP Methods** (§7.1).
- **System behavior**: a reason is required for changes, and changes are audited and not signed (GAP-615). An inactive method cannot be used to start a run ("ICP method \"X\" is inactive.").
- **What you see / How to use it**: "**REQUIRES CONFIRMATION**: field labels to be taken from the screen during the screenshot phase (GAP-043)."

Do not invent field names.

### F-B11: Status label mismatch. PARTLY CONFIRMED, LOW

The mismatch is real (`SampleRegisterTable.tsx:61-63` versus `StatusBadge.tsx:31-33`). The reviewer's correction (rename to In Testing / Under Review) would touch §14, §15, §16 and §23–§25 (about 20 occurrences), and the register labels are what users see most. A single mapping is cheaper and equally accurate.

**Correction:**
- `11-sample-management.md` §11.1 System behavior: add a short table:

  | Register label | Badge / review screens | Status |
  |---|---|---|
  | **Under Testing** | **In Testing** | in testing |
  | **Pending Review** | **Under Review** | under review |
  | **Pending Approval** | **Under Approval** | under approval |
  | **Cancelled / Voided** | **Retest Requested**, **Cancelled** or **Voided** | closed without approval |

- Add the same mapping to `44-glossary.md`, and add one pointer sentence in §17.1 and §19.1: "(the sample register shows these as Pending Review / Pending Approval; see §11.1)".
- No renaming elsewhere.

### F-B12: Helper text quote. CONFIRMED, LOW

**Evidence:** `ApprovalSignaturesCard.tsx:353`.

**Correction:** `18-return-rework-retest.md:90`: quote "…only the checked test(s) move to the new sample; everything else on this sample is left untouched." and add "(for **New Sample Required** the text reads 'new samples')".

### F-B13: Destroyed pending cryovial batch. CONFIRMED, LOW

**Evidence:** `CryovialService.cs:148-152` (checks pending status and the preparer only); `CryovialReviewTable.tsx:116` (`pending && !isDestroyed`).

**Correction:** `26-reference-strains.md` §26.3, line 146: "The screen offers **Approve** and **Reject** only for a pending batch that is not destroyed (the server itself checks only that the batch is still pending)."

### F-C01: Incubator rejection messages. CONFIRMED, LOW

The reviewer's MEDIUM is reduced to LOW because only the message wording is wrong, not the rule.

**Evidence:**
- `TestWorkflowSupport.cs:807` (generic message);
- `IncubationStepRecorder.cs:150` and `PathogenSessionService.cs:558` ("The shared TSB can't start: …");
- `PathogenSessionService.cs:975` ("Incubator {code}'s set point is outside the {range} range of \"{medium}\".").

**Correction:** `30-equipment.md:229`: "In individual and grouped test steps the message is 'The selected incubator's set point is outside this medium's temperature range.'. Shared TSB starts show 'The shared TSB can't start: …' and pathogen confirmation shows 'Incubator {code}'s set point is outside the {range} range of …'. None of the messages names an overdue calibration as the cause."

**Knowledge base:** `knowledge/_clusters/C5-media-strains-stock.md:369` says "the single message" and then lists the TSB message. Remove "single".

### F-C02: No calibration date is accepted. CONFIRMED, MEDIUM

**Evidence:** `IncubatorEligibilityService.cs:38` (`CalibrationDueDate == null || CalibrationDueDate >= now`). The knowledge base is correct (`C5-media-strains-stock.md:369`).

**Correction:** `30-equipment.md`:
- line 48: add "An incubator whose configuration record has no calibration due date is accepted."
- line 228: add the same sentence.
- Add a reviewer note (GAP-905).

### F-C03: Production stage delete. PARTLY CONFIRMED, HIGH

The reviewer's statement "samples lose their stage" is overstated. The stage name stored on the sample is kept (`Sample.cs:31-34`, `ProductionStage` string). The stage link (`ProductionStageId`) is cleared (`SampleConfiguration.cs:30`, `SetNull`; `ReferenceListMasterDataService.cs:143-151`: hard delete, no usage check). Specification and replicate selection read the link:
- `SpecificationLookup.cs:21-22`;
- `StageReplicateResolver.cs`: "Sample has no reconciled production stage …" (GAP-709 facts).

So the dialog text is half true. The real effect is not visible to the user and changes the rules applied to samples still in testing. HIGH is kept.

**Correction:** `32-laboratory-configuration.md` §32.2:
- After the dialog quote (line 100), add "(the stage name stays visible on old samples, but their link to the stage is removed; see the warning below)".
- In step 5 (line 121), add: "> **Warning:** Deleting a stage removes the stage link from every sample recorded with it. Stage-specific specification rows and replicate counts then no longer apply to those samples, and stage-dependent physicochemical tests on samples still in testing cannot resolve their replicate count. Do not delete a stage that is in use; rename it instead."
- Keep GAP-709.

### F-C04: Segregation of duties overstated. CONFIRMED, HIGH

**Evidence:**
- `SegregationOfDutiesGuard.cs:25-54`: "performed" does not include titrant standardization.
- `TitrantStandardizationService.cs:244`: a single signature, with no review step.
- `ItemPreparationConfigurationService.cs:85-99`.
- `WorkflowProfileService.cs:81` (GAP-820).
- The guide itself says the opposite in §31.2 (`31-items-test-master-specifications.md:128`) and in §4.4 (GAP-026 note).

**Correction:**
- `37-electronic-signatures.md` §37.4: replace the "Absolute Rule: No Administrative Exemption" block (two bullets) with: "#### No administrative exemption. The rules in the table compare user accounts; System Administrators and Section Heads are not exempt from them. Segregation of duties is enforced only for the actions in the table. Other signed or approved actions (for example item preparation configuration approval, KPI workflow profile versions, titrant standardization, solution completion, HPLC system suitability and ICP calibration confirmation) have no second-person check. (GAP-026, GAP-820)"
- `42-troubleshooting.md:110`: "For the actions listed in §37.4, MicroLIMS does not let the same person perform and then review or approve. System Administrators are not exempt."
- `43-faq.md:75`, FAQ 15: "No. The segregation-of-duties rules (§37.4) compare user accounts only and System Administrators are not exempt. A System Administrator who prepared or tested a record covered by those rules cannot review or approve it."

### F-C05: Select All export. CONFIRMED, HIGH

**Evidence:** `frontend/src/modules/reports/components/ReportResultsTable.tsx`:
- :141-144: Select All sets a flag only;
- :172-176: CSV calls `runExport()` with `appliedParams` (:155);
- :213-228: the PDF uses `pageSize: 200`;
- :348: the tooltip claims "Approved" only.

`ReportingQueryService.cs:202-215` applies an approval filter only when one is requested.

**Correction:** `34-reports-kpis.md` §34.2:
- Step 7a (line 101): append "Ticking rows allows only **Approved** records. **Select All {totalCount} Matching Records** is different: it exports every record that matches the filters, whatever its approval status, although its tooltip says 'Approved'. Set **Approval Status** to **Approved** first if you need approved results only. **Export Selected PDF** after **Select All** contains at most the first 200 matching records and gives no warning."
- Add a reviewer note (GAP-906).

**Knowledge base:** `knowledge/_clusters/C8-reports-dc.md` lines 91 and 443 repeat the tooltip as fact. Correct them.

### F-C06: Document compliance drill-down unrestricted. CONFIRMED, MEDIUM

**Evidence:** `DocumentTrainingMatrixController.cs`:
- :37-46: the grid is limited to the user's own row without `Documents.TrainingAssign`;
- :63-67: user drill-down likewise;
- :81-88: `GetDocumentCompliance` has no check;
- :53-57: `GetComplianceKpis` (department ranking, top overdue documents) has no check.

Whether a non-privileged user can open the drawer from the screen (column headers are visible in their one-row grid) should be verified when the screenshots are taken.

**Correction:** `38-document-control.md` §38.10:
- Who can use it (line 8): append "The **Document Compliance Profile** drawer and the compliance KPIs are not limited in this way: any user who can open the matrix can see every assigned person's status for a document."
- Add a reviewer note (GAP-907).

### F-C07: Previous Period does nothing. CONFIRMED, LOW

The reviewer's MEDIUM is reduced to LOW: the option has no effect, but it does not mislead users about any data.

**Evidence:** `TrendingTab.tsx:305-321`: only Product and Location Comparison act, and a code comment says Previous Period is out of scope. `compareWith` is not used anywhere else (`git grep compareWith frontend/src/modules/reports`).

**Correction:** `34-reports-kpis.md:229`: append "**Previous Period** can be selected but does not change the chart. (GAP-908)"

### F-C08: Trending categories. CONFIRMED, LOW

**Evidence:** `TrendingTab.tsx:269-281`: the server list is used, and the fixed four are shown only when it is empty.

**Correction:** `34-reports-kpis.md:227`: "**Category** dropdown: the categories returned by the server; if the lists cannot be loaded, four default categories are shown (Finished Product, Raw Material, Water, Environmental Monitoring). (GAP-002, GAP-800)"

### F-C09: Quick Reports tiles. CONFIRMED, LOW

**Evidence:**
- `QuickReportsTiles.tsx:13-21,34-35`;
- `RecordSearchTab.tsx:97-102`.

`10-search-filters.md:59` is already correct.

**Correction:** `34-reports-kpis.md:68`: "Eight tiles. **Product**, **Raw Material**, **Water**, **Environmental Monitoring** and **After Cleaning** set the category filter; **Media/GPT** and **Reference Strains** open their own tabs; **Custom Report** clears the filters."

### F-C10: Raw meaning code. CONFIRMED, LOW

**Evidence:** `WorkflowProfileService.cs:81`; `34-reports-kpis.md:390`.

**Correction:** "the electronic signature meaning **MasterDataChanged**" becomes "an electronic signature (meaning: master data changed)".

### F-C11: REJECTED (see §4).

### F-C12: Active Equipment glossary. CONFIRMED, LOW

**Evidence:** `EquipmentInventoryService.cs:238-246`: every inventory asset in scope, with no status filter.

**Correction:** `44-glossary.md:122`: "A tab of the Equipment Inventory listing every asset in your laboratories, whatever its status, with its activities."

### F-C13: Source File glossary. PARTLY CONFIRMED, LOW

The reviewer's replacement text leaves out the approver.

**Evidence:** `DocumentAuthorizationService.cs:156-204`. Access is granted to:
- System Administrator or Section Head (by role type);
- the owner or assigned author;
- the technical reviewer while the review task is Pending or InProgress;
- the approver while the approval task is Pending.

**Correction:** `44-glossary.md:150`: "The editable Word file of a revision. System Administrators, Section Heads and the document owner or assigned author can open it; a technical reviewer or approver can open it only while their task on that revision is open."

**Knowledge base:** `knowledge/_clusters/C8-reports-dc.md:988` ("during workflow") should match C8:397.

### F-C14: Glossary terms. CONFIRMED, LOW

**Evidence:**
- `EquipmentConfigurationService.cs:185` ("is not an incubator.");
- `EquipmentType.cs` has no refrigerator or oven types;
- `IncubationReadsOnTimeKpi.cs` is the KPI tile.

**Correction:** `44-glossary.md`:
- line 123: "The target temperature configured for an incubator in Laboratory Configuration: Equipment (§30.3)."
- line 49: rename the term to **Incubation reads on time** and use the F-C17 wording.

### F-C15: "Legal" acknowledgement in the guide's voice. CONFIRMED, MEDIUM

**Evidence:** `DocumentAcknowledgementService.cs:126-131` checks a checkbox flag only, with no signature service call.

**Correction:** `38-document-control.md`:
- line 540: "…and record a read-and-understood acknowledgement (checkbox confirmation, no password)."
- line 589: rename the heading to "**Acknowledgement requires the checkbox**: reading progress and scroll tracking are informational only; the acknowledgement is recorded only when the confirmation checkbox is ticked."
- line 603: "legal acknowledgement" becomes "acknowledgement".
- Keep the screen quotes at lines 566, 595 and 596 unchanged (quoted text, GAP-023).

### F-C16: Header and reviewer notes. CONFIRMED, LOW

**Evidence:** `USER_GUIDE.md:3-13`; 277 "Note for reviewers" boxes. The header is produced by the assembly step, not by a `guide/` file.

**Correction:** In the assembly step for USER_GUIDE.md:
- add the rows **Reviewed by**, **Approved by**, **Effective date** (all "to be completed") and a **Revision history** table (0.1 draft, 2026-10-20);
- change "`DOCUMENTATION_GAPS.md`" to "`documentation/DOCUMENTATION_GAPS.md`".

Plan for Phase 14: reviewer notes stay in the draft. Before controlled use, each is either resolved or converted to a user-facing "Note:" or "Warning:" (decision for the document owner).

### F-C17: KPI read window. CONFIRMED, LOW

**Evidence:** `IncubationReadsOnTimeKpi.cs:18-19,59` (on time when `CompletedAt <= Expected + 4 h`).

**Correction:** `34-reports-kpis.md:303`: "…completed incubation windows read no later than 4 hours after the expected read time (earlier reads count as on time)…". Use the same wording in the glossary (F-C14).

### R-NEW-01: EM/Water/AC receive "Who can use it" is wrong. NEW, HIGH

This recurs from F-A01.

**Evidence:**
- `23-environmental-monitoring.md:105`, `24-water-monitoring.md:104` and `25-after-cleaning.md:113` say "Users who can receive samples see the **Receive** button".
- The **Receive** button on the Receive Sample page is gated (`ReceivingPage.tsx:31-33,96`).
- The **+ New Sample** button in the laboratory workspace has no permission check (`ReceivingTestingWorkspacePage.tsx:772-784`; the only permission used on the page is assign-analyst, :793). That workspace is open to every member of the laboratory (`LabWorkspaceRoute.tsx:42-66`).
- The server checks no permission for these three categories (F-A01).

The same sections already carry the correct GAP-227 note, so each section contradicts itself.

**Correction:** In §23.2, §24.2 and §25.2, replace "Who can use it" with: "Any signed-in user. The **+ New Sample** button in the Microbiology workspace is shown to every member of the laboratory, and the server checks neither a receiving permission nor the laboratory. On the **Receive Sample** page, the **Receive** button is shown to users who hold a receiving permission. (GAP-227)"

### R-NEW-02: Global receiving alone cannot receive Product/RM/PM. NEW, MEDIUM

**Evidence:**
- `SampleController.cs:81-82`: `[Authorize(Policy = PermissionConstants.SamplesReceiveOwnLab)]` on Product/RM/PM receipt.
- `PermissionAuthorizationHandler.cs:11-16` checks the exact permission claim, with no hierarchy.
- `Samples.Receive` only widens the target laboratories (`SampleController.cs:88-89`, `ResolveTargetsAsync`).
- A custom role with *Global sample receiving* but without *Own-lab sample receiving* can open the Receive page (`AppRoutes.tsx:139`) but is refused when it receives. The default roles hold both, so they are not affected.

**Correction:**
- `04-roles-permissions.md` §4.3 row 7: append "Product, Raw Material and Packaging Material receipt also requires *Own-lab sample receiving*; this permission widens it to every laboratory."
- `12-sample-receiving.md` §12.1, first "Who can use it" bullet (line 9): "Users who hold both the *Global sample receiving* and the *Own-lab sample receiving* permissions (by default: Section Head, System Administrator) can receive for any laboratory section."
- `11-sample-management.md:8`: same qualification.
- Mark this "FACT NEEDING INTENT CONFIRMATION" (GAP-909).

---

## 3. New gaps needed (proposed; Phase 13 adds them to DOCUMENTATION_GAPS.md)

Numbers are proposals; renumber if they clash. Updates to existing gaps are listed first.

**Updates to existing gaps:**
- **GAP-214**: narrow it. The screen is located (Items > Preparation Configuration tab). Remaining question: should the approver differ from the person who saved the configuration? Sources: C2 lines 24 and 121.
- **GAP-221**: add these facts:
  - the reset is not refused for UnderApproval or Approved samples;
  - the **Reset Session** button is not status-gated (`PathogenSessionDialog.tsx:56,217`);
  - an Approved sample keeps the status Approved while its results are deleted;
  - test orders of all laboratories on the sample are reset.
- **GAP-227**: add that the Microbiology workspace **+ New Sample** button is shown to all laboratory members without a permission check.
- **GAP-300**: rewrite.
  - Facts: the screen offers the action for count tests and for every physicochemical test, through a heuristic, because all physicochemical recorders write a `Result` row (`TestWorkflowSupport.cs:733`, `TestResultsSection.tsx:38-44`).
  - Open question: is return of physicochemical tests intended, and should the heuristic be replaced by the workflow type?
  - Also correct C3:218, :336, :589 and R3:12, :109.

**New gaps:**

| Proposed ID | Question | Affected | Why it matters | Who could confirm |
|---|---|---|---|---|
| GAP-901 | Should reviewers have a screen to approve or return a pathogen result submitted as Detected without biochemical confirmation (`TestWorkflow.BiochemicalDecision`)? Today no screen calls the endpoint. | MOD-PATH-001, §4.3 row 11, §4.4, §16.5 | A permission and a segregation-of-duties rule appear to be active controls but cannot be used; the "flagged for the reviewer" text has no follow-up action. | Product Owner, QA |
| GAP-902 | Is the archived-copy label "Sample {decision}" intended when a multi-laboratory sample closes as Rejected through a later approval? | MOD-APPROVAL-001, §19, §21.3 | A frozen GMP record can carry a label that contradicts the sample's final status. | QA, Product Owner |
| GAP-903 | Is a Growth Promotion configuration with a minimum but no maximum recovery meant to be allowed? Every challenge is then NonConform and the lot is rejected. | MOD-MEDIA-001, §22.1, §22.3 (with GAP-505) | Good lots are rejected; the configuration screen accepts the setting without warning. | QC Microbiology lead |
| GAP-904 | Should editing a lot's purity, moisture, expiry, unit or quantity after use require a reason or signature, convert units, and stop Qty Remaining going below zero? | MOD-MATERIAL-001, §29.5, §27.5, §28.2 | Standard purity and moisture feed assay calculations; unsigned changes after use weaken traceability. | QA, QC Chemistry lead |
| GAP-905 | Is an incubator with no calibration due date meant to pass the eligibility check? | MOD-EQUIP-001, §30.1, §30.3 | A missing calibration date silently counts as "in calibration". | QA, Equipment owner |
| GAP-906 | Should **Select All Matching Records** export only Approved records, as its tooltip says, and should the selected-PDF export warn about or remove its 200-row cap? | MOD-REPORTS-001, §34.2 | Users may pass on unapproved results, or an incomplete list, believing it is complete and approved. | Product Owner, QA |
| GAP-907 | Should the Document Compliance Profile drawer and the compliance KPIs be restricted like the grid and the user drill-down? | MOD-DC (training matrix), §38.10 | Personnel qualification status is visible to every user; the access rule is inconsistent. | Document Control owner, HR/QA |
| GAP-908 | Is the **Previous Period** comparison planned, or should the option be removed? | MOD-REPORTS-001, §34.5 | The option is offered but does nothing. | Product Owner |
| GAP-909 | Is it intended that *Global sample receiving* works only together with *Own-lab sample receiving* for Product/RM/PM receipt? | MOD-SAMPLE-001, §4.3, §12.1 | Custom roles configured with the global permission alone cannot receive, while the menu offers the page. | System Administrator, Product Owner |
| GAP-910 | Should the Skip Wait notice name the user who skipped the wait, instead of the fixed text "Section Head/System Administrator"? | MOD-TESTING-001, §14.6 | Screen evidence of who acted can be misread when a custom role holds the permission. | QA |

---

## 4. Rejected findings

| Finding | Reason |
|---|---|
| F-C11 "Nine kinds" of unaudited records, eight listed | The audit capture excludes nine entity types (`MicroLimsDbContext.cs:309-317`): AuditLog, AuditEventChange, MaterialDocumentAccessLog, EquipmentDocumentAccessLog, Incident, ErrorLog, RefreshToken, NotificationLog and SecurityAuditEvent. The guide's list names them in pairs ("the audit records themselves" = 2, "document access logs for material and equipment documents" = 2, "technical incidents and error log entries" = 2) plus three single items, which makes nine. The text is accurate, and rewording would not materially improve it. |

No other finding was rejected outright. Four were narrowed (PARTLY CONFIRMED):
- **F-B08**: the analysis of which tests get the button is reversed.
- **F-B11**: a mapping note replaces renaming across sections.
- **F-C03**: the stage name is kept; the link is lost.
- **F-C13**: the replacement wording left out the approver.

---

## 5. Counts and residual risk

| Decision | Count |
|---|---|
| CONFIRMED | 38 (incl. F-A04, no text change for the baseline) |
| PARTLY CONFIRMED | 4 (F-B08, F-B11, F-C03, F-C13) |
| REJECTED | 1 (F-C11) |
| NEW (R-NEW) | 2 (R-NEW-01 HIGH, R-NEW-02 MEDIUM) |
| Total reviewed | 43 + 2 new |

Final severities:
- HIGH 10: F-A01, F-A07, F-A08, F-A09, F-A12, F-B06, F-C03, F-C04, F-C05, R-NEW-01.
- MEDIUM 13: F-A02, F-A06, F-A10, F-B01, F-B02, F-B04, F-B08, F-B09, F-B10, F-C02, F-C06, F-C15, R-NEW-02.
- LOW 21: all the others except the rejected F-C11.

**CRITICAL:** none remains. No finding shows the guide claiming regulatory compliance or validation, and no procedure would by itself create a false GMP record. The closest are:
- F-A12: the guide's "Before you start" implies that an approved sample may be reset, and the system allows it;
- F-C05: an export believed to contain approved results only.

Both stay HIGH because the text can be corrected and the guide already flags the underlying system behaviour (GAP-221) or will after Phase 13 (GAP-906).

**Knowledge-base files to correct alongside the guide (Phase 13):**
- `knowledge/_clusters/C2-sample-testing.md:24,121` (preparation configuration screen);
- `knowledge/_clusters/C3-review-approval.md:218,336,589` and `knowledge/_clusters/R3-review.md:12,109` (Return to Analyst scope);
- `knowledge/_clusters/C5-media-strains-stock.md:369` ("single message");
- `knowledge/_clusters/C8-reports-dc.md:91,443` (Select All tooltip as fact), `:160` (Previous Period caveat) and `:988` (Source File access).
