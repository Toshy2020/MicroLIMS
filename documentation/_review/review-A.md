# Phase 11 review — part A (§1–§16)

## Method and coverage
- Scope: `documentation/USER_GUIDE.md` lines 64–3176 (§1.1 to §16.7, up to the heading "## 17."). Every sub-section was read in full. HTML comments were ignored.
- Ground truth: the source code on branch `docs/user-guide` (HEAD c884b654). Searches used Grep and `git grep -F` / `git grep -n` only. Nothing was built or run, and no database was touched. Uncommitted global-search files in the working tree were noted (F-A04) but not treated as baseline.
- Priority areas checked first: permissions, segregation of duties, signatures, approval and status rules.
  - §4: all 50 permission codes, enforced flags and default role grants against `PermissionConstants.cs` and `DbSeeder.cs:440-590`, with role counts SA 50 / SH 43 / REV 15 / ANA 15.
  - §4.4: all 9 segregation-of-duties messages, plus the "performed" definition in `SegregationOfDutiesGuard.cs`.
  - §7.1: the menu gating in `menuConfig.ts`.
  - Signature dialog behaviour in `SignatureDialog.tsx` and `ElectronicSignatureService.cs`.
  - §11.3/§11.4: correction and void rules in `SampleCorrectionService.cs`.
  - §13.3: assignment in `SampleAssignmentService.cs`.
  - §14.6 and §16.7: Skip Wait and session reset.
  - §15.3: the count calculation and limit comparison (`IncubationStepRecorder.cs`, `SpecLimitParser.cs`).
- Claims sampled: about 360 concrete claims, at least 3 per sub-section.
  - Exact messages and labels checked with `git grep -F`: about 230 strings across §4–§16, all found except those noted in the findings.
  - Numeric rules checked:
    - lockout 5 attempts / 15 minutes;
    - sign-in rate limit 20 per 60 seconds;
    - password change limit 5 per 900 seconds;
    - recovery code 15 minutes and 5 tries;
    - reset token 1 hour;
    - password history 5;
    - access token 15 minutes;
    - idle timeout 20 minutes plus a 60-second warning;
    - signature lockout 5 attempts / 15 minutes;
    - notifications: 30 newest, 7-day expiry window, 12-hour deduplication;
    - audit search cap 300.
  - Enum and status lists checked: ApprovalStatus, WorkflowStep, SampleStatus, and the register filter options.
- Not checked: the KPI and Reports tab internals (§10.3) beyond the labels; the Document Control filter values (§10.5) beyond 3 labels; the analyst dashboard metric formulas (§8.3); the dashboard tile queries in detail; the Messages and Discussions validation strings, of which only some were sampled; the attachment type and size limits in §9.3; the GC/ICP/HPLC references (out of part A); and the live default grants in the deployed database (GAP-015 stands).
- Completeness: §1–§16 cover the COVERAGE_MATRIX modules AUTH, NAV, NOTIF, DASH, ROLES, SIG (partly), MSG, DISC, SAMPLE, TRACK, PREP, TESTING, COUNT, OBS and PATH, with their WF-AUTH-001..005, WF-ROLES-001, WF-NOTIF/MSG/DISC-001, WF-SAMPLE-001..008, WF-PREP-001/002, WF-TESTING-001/002, WF-COUNT-001/002, WF-OBS-001 and WF-PATH-001..003. No module assigned to this range is missing. The one coverage gap found is the approval screen for item preparation configurations (F-A08).

## Findings

### F-A01 EM, Water and After Cleaning receiving needs no receiving permission, but early sections say receiving is permission-controlled
- Severity: HIGH
- Section: §2.4 (last bullet), §4.2 (Analyst "Limits" / closing line), §4.3 rows 7 and 45, §7.1 (RECEIVING row)
- Problem: §2.4 and §4.2 state "Receiving is done by users who hold a receiving permission", and §4.3 presents *Global Sample Receiving* / *Own-Lab Sample Receiving* as the controls on receiving. In code the EM, Water and After Cleaning receive and prepare endpoints carry only `[Authorize]` (sign-in), with no permission and no laboratory scoping. §12.2–§12.4 say so correctly, so the guide contradicts itself. An administrator who removes the receiving permissions from a role to stop it receiving will believe EM/Water/AC receipt is also blocked; it is not.
- Evidence: `backend/MicroLIMS.API/Controllers/EMController.cs:14` (class-level `[Authorize]` only), `:26` receive, `:33` prepare; same pattern `WaterController.cs:30,37`, `AfterCleaningController.cs:33,46`. Guide §12.2 "Who can use it" and GAP-227 note.
- Recommended correction: In §2.4, §4.2 and the §4.3 rows for `Samples.Receive` / `Samples.ReceiveOwnLab`, add: "These permissions control receiving of Product, Raw Material and Packaging Material samples only. Environmental Monitoring, Water and After Cleaning samples can be received and prepared by any signed-in user." Keep the GAP-227 note.

### F-A02 Sample-register status filter: "Cancelled / Voided" and "Pending Review" options described inconsistently and incompletely
- Severity: MEDIUM
- Section: §10.2 (Sample register, note GAP-204), §11.1 ("What you see" filter list, "System behavior")
- Problem: (a) The §10.2 note says the label "Cancelled / Voided" is used "for the status that other screens call Retest Requested". §11.1 says it covers three statuses. The backend filter for this option returns Retest Requested, Cancelled and Voided samples together, so §10.2 is wrong and contradicts §11.1. (b) The filter option **Pending Review** returns samples with any section under review **or under approval**. The guide lists **Pending Review**, **Under Review** and **Under Approval** as separate options without saying that they overlap. (c) In the table, the register shows Under Approval samples as **Pending Approval**, but the filter calls the same status **Under Approval**. §11.1 lists both labels without explaining the difference.
- Evidence: `frontend/src/modules/receiving/constants/receivingConstants.ts:48-58` (`RetestRequested` → "Cancelled / Voided", `PendingReview` option); `backend/MicroLIMS.Application/Services/TestingWorkspaceService.cs:134-155` (PendingReview = UnderReview ∪ UnderApproval sections; RetestRequested option = RetestRequested ∪ Cancelled ∪ Voided); `frontend/src/modules/receiving/components/SampleRegisterTable.tsx:59-71` (UnderApproval displayed "Pending Approval").
- Recommended correction: Replace the §10.2 note with the wording in §11.1. Add: "**Cancelled / Voided** finds samples that were voided, cancelled, or closed with a retest request. **Pending Review** finds samples waiting for either technical review or the release decision. In the table, samples under approval are labelled **Pending Approval**."

### F-A03 TestWorkflow.Execute description omits media evaluation
- Severity: LOW
- Section: §4.3 row 10
- Problem: The row says the permission allows entering microbiology counts, observations, pathogen steps and test results. It also gates the whole Media Evaluation controller (growth promotion reading), and the seeded description reads "Execute pathogen session and media evaluation workflow steps." A role without it cannot evaluate media lots, and the table does not say so.
- Evidence: `backend/MicroLIMS.API/Controllers/MediaEvaluationController.cs:28`; `backend/MicroLIMS.Persistence/Seed/DbSeeder.cs:461`.
- Recommended correction: Add "and media evaluation (growth promotion) steps" to row 10.

### F-A04 Global search is being added on this branch; §10.1 will become wrong
- Severity: LOW
- Section: §10.1 first sentence, §6.1 header list
- Problem: §10.1 states "MicroLIMS has no single search box that searches the whole application." This is true of the committed code. The working tree has uncommitted work that adds a header search box ("Search MicroLIMS...", Ctrl+K) and a `GlobalSearchController`. If that work is merged, §10.1 and the §6.1 header list become wrong.
- Evidence: `git status` shows untracked `backend/MicroLIMS.API/Controllers/GlobalSearchController.cs`, `frontend/src/components/search/`, and modified `frontend/src/components/Header.tsx` (adds `aria-label="Search MicroLIMS (Ctrl+K)"`).
- Recommended correction: No change for the committed baseline. Add a release-gate check: if global search ships with this guide, update §6.1 and §10.1.

### F-A05 Wrong cross-reference for sample preparation
- Severity: LOW
- Section: §12.1 "What happens next"
- Problem: The text says "preparation must be confirmed before incubation can start (see Section 13)". Preparation is described in Section 14 (14.2–14.4). Section 13 covers test orders and assignment.
- Evidence: Guide headings: `## 14. Analyst Testing Workspace and Sample Preparation`.
- Recommended correction: Change "(see Section 13)" to "(see Section 14)".

### F-A06 Void purpose and dialog say "open tests"; the system voids every non-superseded test, including closed ones
- Severity: MEDIUM
- Section: §11.4 Purpose, "What you see" banner, "System behavior"
- Problem: The Purpose says the sample "and all of its open tests" are struck. The banner text quoted from the screen says the same. "System behavior" correctly says that all non-superseded test orders are set to Voided. The code also voids Approved or Rejected test orders, because voiding is allowed after approval (GAP-203). A user who reads the Purpose or the banner may expect approved results to stay intact.
- Evidence: `backend/MicroLIMS.Application/Services/SampleCorrectionService.cs:273-277` (`Where(t => !t.IsSuperseded)`, no status filter); `frontend/src/modules/receiving/dialogs/VoidSampleConfirmationDialog.tsx:128`.
- Recommended correction: In the Purpose, change "all of its open tests" to "all of its current (non-superseded) tests, including tests that are already approved or rejected". Keep the banner quote, and add a sentence saying that the banner wording is narrower than what the system does.

### F-A07 §16.5 biochemical procedure names the wrong button and describes an attachment that the screen does not have
- Severity: HIGH
- Section: §16.5 "What you see", "How to use it" steps 4–5
- Problem: The guide says the biochemical panel has an optional attachment, and that the analyst records the result by clicking **Proceed to Biochemical Test**. In code, **Proceed to Biochemical Test** and **Submit as Detected (skip biochemical)** belong to the *decision* phase. This phase is shown only after an all-conforming confirmatory result, and **Submit as Detected** finalises the test as Detected. The result form itself has a required **Biochemical result** text field (helper "For example an IMViC pattern or API strip result."), the choices **Not Detected** / **Detected** under the question "Based on this result, is the organism Detected or Not Detected?…", and the button **Submit Result**. No attachment control exists: the client always sends `attachmentId: null`. An analyst who follows step 5 never submits the result. Clicking the button next to it (**Submit as Detected**) records a Detected pathogen result without biochemical confirmation.
- Evidence: `frontend/src/modules/testingWorkspace/pathogenSteps/BiochemicalTestPanel.tsx:90-108` (decision phase buttons), `:118-150` (form: "Biochemical result", Detected/Not Detected, "Submit Result"); `frontend/src/modules/testingWorkspace/services/TestWorkflowService.ts:262-265` (`attachmentId: null`).
- Recommended correction: Rewrite "What you see" with the two phases: (1) decision, shown only after an all-conforming confirmatory result, with **Submit as Detected (skip biochemical)** and **Proceed to Biochemical Test** and the warning "Submitting as Detected without biochemical confirmation will be flagged for the reviewer."; (2) the result form: **Biochemical result** (required), **Detected** / **Not Detected**, and **Submit Result**. Steps: enter the result, choose Detected or Not Detected, click **Submit Result**. Remove step 4 (attachment) and the reviewer note about unconfirmed labels.

### F-A08 §14.7 says no screen exists for editing or approving item preparation configurations; one exists in Items
- Severity: HIGH
- Section: §14.7 "What you see", reviewer note (GAP-214); also §8.2 tile 6 (consistency)
- Problem: The guide states that no dedicated screen for editing or approving the configuration has been located, so an approver has no procedure. In code, **Items** has a Preparation Configuration tab that shows the **Approved** / **Pending Approval** chip with "Created by …" or "Approved by … on …", the fields **Sample Amount**, **Technique**, **Filtration Volume (ml)**, **Washing Volume (ml)**, **Diluent**, **Neutralizer**, and the buttons **Configure Preparation** / **Edit**, **Save Configuration** and **Approve**. The messages are "Preparation configuration saved. It is pending approval." and "Preparation configuration approved." §8.2 already says the "Prep configs pending" tile opens the Items configuration, which contradicts §14.7. Pending configurations are already used in testing, so the approval step matters for GMP.
- Evidence: `frontend/src/modules/laboratoryConfiguration/items/components/ItemWorkspace.tsx:526-532` (Tab 4: Preparation Configuration); `frontend/src/modules/laboratoryConfiguration/items/components/ItemPreparationConfigurationSection.tsx:74,88,104-108,114-123,129-133,141-180`; `backend/MicroLIMS.Application/Services/ItemPreparationConfigurationService.cs:85-99` (approve: no signature, no check that the approver differs from the creator).
- Recommended correction: Replace "What you see" with the Items > (item) > Preparation Configuration tab and add the procedure: open the item, open the Preparation Configuration tab, check the steps, click **Approve**. Keep the GAP-214 note only for the missing check that the approver differs from the creator. Remove "the screen ... have not been located".

### F-A09 §14.3 says EM/Water/AC preparation is limited to users with access to the sample's section; the system does not check this
- Severity: HIGH
- Section: §14.3 "Who can use it" (contradicts the same section's GAP-212/227 note)
- Problem: "Any signed-in user with access to the sample's laboratory section" states a laboratory-scope control. The EM, Water and After Cleaning prepare endpoints and their workflow engines perform no section-scope check. Product/RM/PM preparation does check scope (`EnsureSampleAccessAsync`). Preparation is the step that makes the preparer the assigned analyst on every test, so the wrong statement affects segregation of duties.
- Evidence: `backend/MicroLIMS.API/Controllers/EMController.cs:33-35` (no `EnsureSampleAccessAsync`, compare `SamplePreparationController.cs:44,56`); `git grep "_scope|Scope"` in `EMService.cs`, `EMWorkflowEngine.cs`, `WaterWorkflowEngine.cs`, `AfterCleaningWorkflowEngine.cs` returns nothing.
- Recommended correction: Change "Who can use it" to: "Any signed-in user. The system checks neither a permission nor the user's laboratory assignment (see the note below)."

### F-A10 Biochemical decision permission and its segregation rule are presented as usable, but no screen calls them
- Severity: MEDIUM
- Section: §4.3 row 11, §4.4 row "Pathogen biochemical decision", §16.5 reviewer note
- Problem: §4.3 lists *Biochemical Decision Recording* as allowing users to "Record biochemical confirmation decisions in pathogen testing", and §4.4 lists its segregation message as an active control. Neither says that no screen exists. The frontend service method carries the comment "No frontend UI calls this yet", and nothing calls it. §16.5 says only that the screen "has not been found". Reviewers therefore have no way to return a "Detected without biochemical confirmation" result through this route, and auditors may assume that such a control works.
- Evidence: `frontend/src/modules/testingWorkspace/services/TestWorkflowService.ts:267-271` ("Reviewer-only. No frontend UI calls this yet"); `git grep recordBiochemicalDecision frontend/src` finds only the definition; endpoint `backend/MicroLIMS.API/Controllers/TestWorkflowController.cs:527-528`.
- Recommended correction: In §4.3 row 11 add "(no screen is provided for it)", as rows 4 and 9 already do. In §4.4 mark the row as having no screen. In §16.5 change the note to "No screen exists for this reviewer decision."

### F-A11 Skip Wait procedure omits the confirmation dialog; the on-screen text names roles, but the permission decides
- Severity: MEDIUM
- Section: §14.6 "How to use it", "What you see"
- Problem: (a) Clicking **Skip Wait** opens a confirmation dialog: "Skip the remaining minimum incubation wait time for this step? This bypasses the wait only; the recorded incubation window is not changed." The user must confirm it. Steps 2–3 leave this out. (b) The guide repeats the notice "Minimum wait time was skipped by a Section Head/System Administrator." without saying that the text is fixed. Any custom role with *Test Workflow Supervision* can skip the wait, and the notice still names those two roles. This matters when the record is read as evidence of who acted.
- Evidence: `frontend/src/modules/testingWorkspace/TestWorkflowDialog.tsx:164-165` (permission check), `:598-601`, `:624-629` (ConfirmationDialog message), `:592-594` (fixed text).
- Recommended correction: Add the step "Confirm the question 'Skip the remaining minimum incubation wait time for this step? …'". Add to the System behavior: "The notice always names Section Head/System Administrator. The user who skipped the wait is the one recorded on the incubation."

### F-A12 Pathogen session reset deletes data of every test of the sample in every laboratory, not only the pathogen session
- Severity: MEDIUM
- Section: §16.7 Purpose and "System behavior"
- Problem: The Purpose ("clear all entries of a pathogen testing session") suggests that the reset affects the session only. The code loads `sample.TestOrders` without filtering by section, test type or superseded flag. It deletes Results, count readings, step results and incubations for all of them, and sets every order to Waiting/Pending. This includes plate-count tests and the test orders of the Physicochemical laboratory on the same sample. Section sign-off rows are not reset. The bullet "for every test of the sample" is literally correct, but the reader of a pathogen section is unlikely to see what it covers. GAP-221 covers only the status gap.
- Evidence: `backend/MicroLIMS.Application/Services/PathogenSessionService.cs:1300-1303` (`testOrderIds = sample.TestOrders.Select(t => t.Id)`), `:1363-1367` (every order reset), `:1381-1384` (only InTesting/UnderReview samples return to Received).
- Recommended correction: Add to Purpose and System behavior: "The reset applies to every test order of the sample, including count tests, physicochemical tests and superseded orders, not only the pathogen tests of the session." Add a warning before step 3.

### F-A13 Assign Analyst also changes voided, cancelled and finished-but-unapproved test orders
- Severity: LOW
- Section: §13.3 "System behavior" first bullet; §11.1
- Problem: The guide correctly says that orders that are not Approved or Rejected are updated. The Purpose ("all open test orders") is wider than that rule allows. The code also reassigns Voided, Cancelled, RetestRequested and Ready/Result Entered orders. Reassigning a Ready test makes the new user count as "performed" for segregation of duties (assigned analyst = performed), so the assignee cannot review or approve it.
- Evidence: `backend/MicroLIMS.Application/Services/SampleAssignmentService.cs:51-53`; `SegregationOfDutiesGuard.cs:33`.
- Recommended correction: Change the Purpose to "every current test order that is not yet Approved or Rejected (including tests whose results are already entered)". Add: "Assigning a user to a test whose results are already entered prevents that user from reviewing or approving the sample."

## Summary
- CRITICAL: 0
- HIGH: 4 (F-A01, F-A07, F-A08, F-A09)
- MEDIUM: 5 (F-A02, F-A06, F-A10, F-A11, F-A12)
- LOW: 4 (F-A03, F-A04, F-A05, F-A13)

Overall assessment: Part A (§1–§16) is accurate to a high degree. Almost every label, message and number sampled matches the code exactly. This includes all 50 permission rows and their default grants, the 40/10 enforced/legacy split, all segregation-of-duties messages, the lockout and rate-limit values, password and recovery rules, the idle timeout, the plate-count arithmetic, the rounding and limit order, and the pathogen session messages. The guide makes no compliance claim. The footer claim on the sign-in screen ("FDA 21 CFR Part 11 & EU Annex 11 Compliant", `frontend/src/pages/Login.tsx:204`) is correctly kept out of the text and flagged (GAP-023). Mock data appears only in the worked example in §15.3, which is labelled as fake data.

The weaknesses cluster in four places:
1. Access-control statements that contradict the code and, in places, the guide's own notes: EM/Water/AC receiving and preparation (F-A01, F-A09).
2. Two procedures where the guide could not see the screen and filled the gap wrongly or left it open: the biochemical result form (F-A07) and the approval of item preparation configurations (F-A08).
3. Destructive or overriding actions whose reach is understated: void (F-A06), session reset (F-A12), Skip Wait (F-A11).
4. Register status filter semantics (F-A02).

None of these is a compliance claim. F-A07 and F-A08 should be fixed before the screenshots are captured.

