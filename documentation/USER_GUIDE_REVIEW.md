# User Guide Review (Phase 11)

Independent review of `USER_GUIDE.md` DRAFT 0.1 by three fresh-context Opus reviewers (parts A §1–16, B §17–29, C §30–44 + header). Reviewers used only the repository, the knowledge base and the guide; writer notes (HTML comments, briefs, R-reviews) were excluded.

Findings are reconciled in `RECONCILIATION.md` (Phase 12).


## Phase 11 review — part A (§1–§16)

### Method and coverage
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

### Findings

#### F-A01 EM, Water and After Cleaning receiving needs no receiving permission, but early sections say receiving is permission-controlled
- Severity: HIGH
- Section: §2.4 (last bullet), §4.2 (Analyst "Limits" / closing line), §4.3 rows 7 and 45, §7.1 (RECEIVING row)
- Problem: §2.4 and §4.2 state "Receiving is done by users who hold a receiving permission", and §4.3 presents *Global Sample Receiving* / *Own-Lab Sample Receiving* as the controls on receiving. In code the EM, Water and After Cleaning receive and prepare endpoints carry only `[Authorize]` (sign-in), with no permission and no laboratory scoping. §12.2–§12.4 say so correctly, so the guide contradicts itself. An administrator who removes the receiving permissions from a role to stop it receiving will believe EM/Water/AC receipt is also blocked; it is not.
- Evidence: `backend/MicroLIMS.API/Controllers/EMController.cs:14` (class-level `[Authorize]` only), `:26` receive, `:33` prepare; same pattern `WaterController.cs:30,37`, `AfterCleaningController.cs:33,46`. Guide §12.2 "Who can use it" and GAP-227 note.
- Recommended correction: In §2.4, §4.2 and the §4.3 rows for `Samples.Receive` / `Samples.ReceiveOwnLab`, add: "These permissions control receiving of Product, Raw Material and Packaging Material samples only. Environmental Monitoring, Water and After Cleaning samples can be received and prepared by any signed-in user." Keep the GAP-227 note.

#### F-A02 Sample-register status filter: "Cancelled / Voided" and "Pending Review" options described inconsistently and incompletely
- Severity: MEDIUM
- Section: §10.2 (Sample register, note GAP-204), §11.1 ("What you see" filter list, "System behavior")
- Problem: (a) The §10.2 note says the label "Cancelled / Voided" is used "for the status that other screens call Retest Requested". §11.1 says it covers three statuses. The backend filter for this option returns Retest Requested, Cancelled and Voided samples together, so §10.2 is wrong and contradicts §11.1. (b) The filter option **Pending Review** returns samples with any section under review **or under approval**. The guide lists **Pending Review**, **Under Review** and **Under Approval** as separate options without saying that they overlap. (c) In the table, the register shows Under Approval samples as **Pending Approval**, but the filter calls the same status **Under Approval**. §11.1 lists both labels without explaining the difference.
- Evidence: `frontend/src/modules/receiving/constants/receivingConstants.ts:48-58` (`RetestRequested` → "Cancelled / Voided", `PendingReview` option); `backend/MicroLIMS.Application/Services/TestingWorkspaceService.cs:134-155` (PendingReview = UnderReview ∪ UnderApproval sections; RetestRequested option = RetestRequested ∪ Cancelled ∪ Voided); `frontend/src/modules/receiving/components/SampleRegisterTable.tsx:59-71` (UnderApproval displayed "Pending Approval").
- Recommended correction: Replace the §10.2 note with the wording in §11.1. Add: "**Cancelled / Voided** finds samples that were voided, cancelled, or closed with a retest request. **Pending Review** finds samples waiting for either technical review or the release decision. In the table, samples under approval are labelled **Pending Approval**."

#### F-A03 TestWorkflow.Execute description omits media evaluation
- Severity: LOW
- Section: §4.3 row 10
- Problem: The row says the permission allows entering microbiology counts, observations, pathogen steps and test results. It also gates the whole Media Evaluation controller (growth promotion reading), and the seeded description reads "Execute pathogen session and media evaluation workflow steps." A role without it cannot evaluate media lots, and the table does not say so.
- Evidence: `backend/MicroLIMS.API/Controllers/MediaEvaluationController.cs:28`; `backend/MicroLIMS.Persistence/Seed/DbSeeder.cs:461`.
- Recommended correction: Add "and media evaluation (growth promotion) steps" to row 10.

#### F-A04 Global search is being added on this branch; §10.1 will become wrong
- Severity: LOW
- Section: §10.1 first sentence, §6.1 header list
- Problem: §10.1 states "MicroLIMS has no single search box that searches the whole application." This is true of the committed code. The working tree has uncommitted work that adds a header search box ("Search MicroLIMS...", Ctrl+K) and a `GlobalSearchController`. If that work is merged, §10.1 and the §6.1 header list become wrong.
- Evidence: `git status` shows untracked `backend/MicroLIMS.API/Controllers/GlobalSearchController.cs`, `frontend/src/components/search/`, and modified `frontend/src/components/Header.tsx` (adds `aria-label="Search MicroLIMS (Ctrl+K)"`).
- Recommended correction: No change for the committed baseline. Add a release-gate check: if global search ships with this guide, update §6.1 and §10.1.

#### F-A05 Wrong cross-reference for sample preparation
- Severity: LOW
- Section: §12.1 "What happens next"
- Problem: The text says "preparation must be confirmed before incubation can start (see Section 13)". Preparation is described in Section 14 (14.2–14.4). Section 13 covers test orders and assignment.
- Evidence: Guide headings: `## 14. Analyst Testing Workspace and Sample Preparation`.
- Recommended correction: Change "(see Section 13)" to "(see Section 14)".

#### F-A06 Void purpose and dialog say "open tests"; the system voids every non-superseded test, including closed ones
- Severity: MEDIUM
- Section: §11.4 Purpose, "What you see" banner, "System behavior"
- Problem: The Purpose says the sample "and all of its open tests" are struck. The banner text quoted from the screen says the same. "System behavior" correctly says that all non-superseded test orders are set to Voided. The code also voids Approved or Rejected test orders, because voiding is allowed after approval (GAP-203). A user who reads the Purpose or the banner may expect approved results to stay intact.
- Evidence: `backend/MicroLIMS.Application/Services/SampleCorrectionService.cs:273-277` (`Where(t => !t.IsSuperseded)`, no status filter); `frontend/src/modules/receiving/dialogs/VoidSampleConfirmationDialog.tsx:128`.
- Recommended correction: In the Purpose, change "all of its open tests" to "all of its current (non-superseded) tests, including tests that are already approved or rejected". Keep the banner quote, and add a sentence saying that the banner wording is narrower than what the system does.

#### F-A07 §16.5 biochemical procedure names the wrong button and describes an attachment that the screen does not have
- Severity: HIGH
- Section: §16.5 "What you see", "How to use it" steps 4–5
- Problem: The guide says the biochemical panel has an optional attachment, and that the analyst records the result by clicking **Proceed to Biochemical Test**. In code, **Proceed to Biochemical Test** and **Submit as Detected (skip biochemical)** belong to the *decision* phase. This phase is shown only after an all-conforming confirmatory result, and **Submit as Detected** finalises the test as Detected. The result form itself has a required **Biochemical result** text field (helper "For example an IMViC pattern or API strip result."), the choices **Not Detected** / **Detected** under the question "Based on this result, is the organism Detected or Not Detected?…", and the button **Submit Result**. No attachment control exists: the client always sends `attachmentId: null`. An analyst who follows step 5 never submits the result. Clicking the button next to it (**Submit as Detected**) records a Detected pathogen result without biochemical confirmation.
- Evidence: `frontend/src/modules/testingWorkspace/pathogenSteps/BiochemicalTestPanel.tsx:90-108` (decision phase buttons), `:118-150` (form: "Biochemical result", Detected/Not Detected, "Submit Result"); `frontend/src/modules/testingWorkspace/services/TestWorkflowService.ts:262-265` (`attachmentId: null`).
- Recommended correction: Rewrite "What you see" with the two phases: (1) decision, shown only after an all-conforming confirmatory result, with **Submit as Detected (skip biochemical)** and **Proceed to Biochemical Test** and the warning "Submitting as Detected without biochemical confirmation will be flagged for the reviewer."; (2) the result form: **Biochemical result** (required), **Detected** / **Not Detected**, and **Submit Result**. Steps: enter the result, choose Detected or Not Detected, click **Submit Result**. Remove step 4 (attachment) and the reviewer note about unconfirmed labels.

#### F-A08 §14.7 says no screen exists for editing or approving item preparation configurations; one exists in Items
- Severity: HIGH
- Section: §14.7 "What you see", reviewer note (GAP-214); also §8.2 tile 6 (consistency)
- Problem: The guide states that no dedicated screen for editing or approving the configuration has been located, so an approver has no procedure. In code, **Items** has a Preparation Configuration tab that shows the **Approved** / **Pending Approval** chip with "Created by …" or "Approved by … on …", the fields **Sample Amount**, **Technique**, **Filtration Volume (ml)**, **Washing Volume (ml)**, **Diluent**, **Neutralizer**, and the buttons **Configure Preparation** / **Edit**, **Save Configuration** and **Approve**. The messages are "Preparation configuration saved. It is pending approval." and "Preparation configuration approved." §8.2 already says the "Prep configs pending" tile opens the Items configuration, which contradicts §14.7. Pending configurations are already used in testing, so the approval step matters for GMP.
- Evidence: `frontend/src/modules/laboratoryConfiguration/items/components/ItemWorkspace.tsx:526-532` (Tab 4: Preparation Configuration); `frontend/src/modules/laboratoryConfiguration/items/components/ItemPreparationConfigurationSection.tsx:74,88,104-108,114-123,129-133,141-180`; `backend/MicroLIMS.Application/Services/ItemPreparationConfigurationService.cs:85-99` (approve: no signature, no check that the approver differs from the creator).
- Recommended correction: Replace "What you see" with the Items > (item) > Preparation Configuration tab and add the procedure: open the item, open the Preparation Configuration tab, check the steps, click **Approve**. Keep the GAP-214 note only for the missing check that the approver differs from the creator. Remove "the screen ... have not been located".

#### F-A09 §14.3 says EM/Water/AC preparation is limited to users with access to the sample's section; the system does not check this
- Severity: HIGH
- Section: §14.3 "Who can use it" (contradicts the same section's GAP-212/227 note)
- Problem: "Any signed-in user with access to the sample's laboratory section" states a laboratory-scope control. The EM, Water and After Cleaning prepare endpoints and their workflow engines perform no section-scope check. Product/RM/PM preparation does check scope (`EnsureSampleAccessAsync`). Preparation is the step that makes the preparer the assigned analyst on every test, so the wrong statement affects segregation of duties.
- Evidence: `backend/MicroLIMS.API/Controllers/EMController.cs:33-35` (no `EnsureSampleAccessAsync`, compare `SamplePreparationController.cs:44,56`); `git grep "_scope|Scope"` in `EMService.cs`, `EMWorkflowEngine.cs`, `WaterWorkflowEngine.cs`, `AfterCleaningWorkflowEngine.cs` returns nothing.
- Recommended correction: Change "Who can use it" to: "Any signed-in user. The system checks neither a permission nor the user's laboratory assignment (see the note below)."

#### F-A10 Biochemical decision permission and its segregation rule are presented as usable, but no screen calls them
- Severity: MEDIUM
- Section: §4.3 row 11, §4.4 row "Pathogen biochemical decision", §16.5 reviewer note
- Problem: §4.3 lists *Biochemical Decision Recording* as allowing users to "Record biochemical confirmation decisions in pathogen testing", and §4.4 lists its segregation message as an active control. Neither says that no screen exists. The frontend service method carries the comment "No frontend UI calls this yet", and nothing calls it. §16.5 says only that the screen "has not been found". Reviewers therefore have no way to return a "Detected without biochemical confirmation" result through this route, and auditors may assume that such a control works.
- Evidence: `frontend/src/modules/testingWorkspace/services/TestWorkflowService.ts:267-271` ("Reviewer-only. No frontend UI calls this yet"); `git grep recordBiochemicalDecision frontend/src` finds only the definition; endpoint `backend/MicroLIMS.API/Controllers/TestWorkflowController.cs:527-528`.
- Recommended correction: In §4.3 row 11 add "(no screen is provided for it)", as rows 4 and 9 already do. In §4.4 mark the row as having no screen. In §16.5 change the note to "No screen exists for this reviewer decision."

#### F-A11 Skip Wait procedure omits the confirmation dialog; the on-screen text names roles, but the permission decides
- Severity: MEDIUM
- Section: §14.6 "How to use it", "What you see"
- Problem: (a) Clicking **Skip Wait** opens a confirmation dialog: "Skip the remaining minimum incubation wait time for this step? This bypasses the wait only; the recorded incubation window is not changed." The user must confirm it. Steps 2–3 leave this out. (b) The guide repeats the notice "Minimum wait time was skipped by a Section Head/System Administrator." without saying that the text is fixed. Any custom role with *Test Workflow Supervision* can skip the wait, and the notice still names those two roles. This matters when the record is read as evidence of who acted.
- Evidence: `frontend/src/modules/testingWorkspace/TestWorkflowDialog.tsx:164-165` (permission check), `:598-601`, `:624-629` (ConfirmationDialog message), `:592-594` (fixed text).
- Recommended correction: Add the step "Confirm the question 'Skip the remaining minimum incubation wait time for this step? …'". Add to the System behavior: "The notice always names Section Head/System Administrator. The user who skipped the wait is the one recorded on the incubation."

#### F-A12 Pathogen session reset deletes data of every test of the sample in every laboratory, not only the pathogen session
- Severity: MEDIUM
- Section: §16.7 Purpose and "System behavior"
- Problem: The Purpose ("clear all entries of a pathogen testing session") suggests that the reset affects the session only. The code loads `sample.TestOrders` without filtering by section, test type or superseded flag. It deletes Results, count readings, step results and incubations for all of them, and sets every order to Waiting/Pending. This includes plate-count tests and the test orders of the Physicochemical laboratory on the same sample. Section sign-off rows are not reset. The bullet "for every test of the sample" is literally correct, but the reader of a pathogen section is unlikely to see what it covers. GAP-221 covers only the status gap.
- Evidence: `backend/MicroLIMS.Application/Services/PathogenSessionService.cs:1300-1303` (`testOrderIds = sample.TestOrders.Select(t => t.Id)`), `:1363-1367` (every order reset), `:1381-1384` (only InTesting/UnderReview samples return to Received).
- Recommended correction: Add to Purpose and System behavior: "The reset applies to every test order of the sample, including count tests, physicochemical tests and superseded orders, not only the pathogen tests of the session." Add a warning before step 3.

#### F-A13 Assign Analyst also changes voided, cancelled and finished-but-unapproved test orders
- Severity: LOW
- Section: §13.3 "System behavior" first bullet; §11.1
- Problem: The guide correctly says that orders that are not Approved or Rejected are updated. The Purpose ("all open test orders") is wider than that rule allows. The code also reassigns Voided, Cancelled, RetestRequested and Ready/Result Entered orders. Reassigning a Ready test makes the new user count as "performed" for segregation of duties (assigned analyst = performed), so the assignee cannot review or approve it.
- Evidence: `backend/MicroLIMS.Application/Services/SampleAssignmentService.cs:51-53`; `SegregationOfDutiesGuard.cs:33`.
- Recommended correction: Change the Purpose to "every current test order that is not yet Approved or Rejected (including tests whose results are already entered)". Add: "Assigning a user to a test whose results are already entered prevents that user from reviewing or approving the sample."

### Summary
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


## Phase 11 review — part B (§17–§29)

### Method and coverage
- Sub-sections reviewed: all 47 sub-sections from §17.1 to §29.5. HTML comments were ignored.
- Priority: GMP and permission content was traced through the code end to end. This covered review, return to analyst, approval, retest and new-sample decisions, close testing, the OOS outcome propagation and archiving, the media lot release, the cryovial batch approval and the working-standard sign-off. Files: SampleReviewService.cs, SampleApprovalService.cs (DecideCoreAsync, FinalizeIfClosedAsync, PropagateOosOutcomeAsync), SectionClosureService.cs, SampleSectionRollup.cs, SegregationOfDutiesGuard.cs, MediaReleaseService.cs, CryovialService.cs and WorkingStandardsController.cs, plus the `[Authorize]` policies on the controllers involved.
- Permission claims: every "by default: …" statement in §17–§29 was compared with the default grants in backend/MicroLIMS.Persistence/Seed/DbSeeder.cs:523-576. The permissions covered were Samples.Review, Samples.Approve, Samples.AssignAnalyst, Oos.Manage, MasterData.Manage, Media.Prepare, Media.Release, Cryovials.Manage, Cryovials.Approve, Materials.Manage, Equipment.Manage, Hplc.Operate, Solutions.Prepare and WorkingStandards.Qualify, plus the media-code change (`[Authorize(Roles = SectionHead)]`). All default-role statements matched. The only exception is the dashboard notice, which is tied to role type rather than a permission (F-B05).
- Labels and messages: about 120 strings were checked with `git grep -F`, at least 3 per sub-section. All were found except those reported in F-B07 and F-B12.
- Calculations and rules checked in code:
  - media recovery % and challenge outcome (MediaEvaluationEngine.cs);
  - EM, After Cleaning and Water batch averaging, rounding and the order of limit checks (IncubationStepRecorder.cs, SpecLimitParser.cs);
  - the working-standard RSD rule (WorkingStandardCalculator.cs);
  - the default titration temperature coefficient;
  - the order of the material consumption checks (MaterialService.cs);
  - the sample status roll-up (SampleSectionRollup.cs).
- Completeness was checked against USER_GUIDE_STRUCTURE rows 17–29 and the matching COVERAGE_MATRIX modules.
- Not checked:
  - the dissolution, disintegration and weight-variation stage arithmetic;
  - the titration, ICP and GC residual-solvent formulas, line by line;
  - the Certificate of Analysis layouts and variant availability rules;
  - the content of the archived PDFs;
  - screen layouts and screenshots (no running application was used);
  - the HPLC replicate count by stage role.
- No compliance or validation claims were found in §17–§29. No figures based on mock data were found.

### Findings

#### F-B01 Return to Analyst: count-test behaviour stated for all tests
- Severity: MEDIUM
- Section: §18.1
- Problem: "System behavior" and "Status" say the test always goes back to **Incubating** and its count readings are set aside. The system also accepts result-entry (analysis) tests, as the guide's own GAP-300 note says. For those tests the active analyses and parameter results are set aside instead, no incubation is reopened, and the test goes back to **Running**, not Incubating.
- Evidence: backend/MicroLIMS.Application/Services/SampleReviewService.cs:197-243 (the count branch and the `UsesTestAnalysis` branch), :249-251 (`targetStep = UsesTestAnalysis ? WorkflowStep.Running : WorkflowStep.Incubating`).
- Recommended correction: Say in the bullets that they apply to count tests. Add: "For a result-entry test returned through the system, the active entries are set aside and the test goes back to Running."

#### F-B02 Retest outcome copied to the original sample: per-test results and traceability not described
- Severity: MEDIUM
- Section: §18.2, §18.3, §20.1 (System behavior)
- Problem: The guide says the retest's outcome "is copied to the original sample's section". That is true for the section status, but the system then sets each test of that section on the original sample to **Approved** or **Rejected** from that test's own recorded result. It ignores the retest outcome and whether the test was superseded. So after an approved retest, the original failing test stays **Rejected**. The outcome is copied only when the retest sample has exactly one laboratory section. The original sample's "approved by" field is filled with the user who decided the retest sample, and no signature is recorded on the original. A reviewer reading the original record needs these traceability facts.
- Evidence: backend/MicroLIMS.Application/Services/SampleApprovalService.cs:554 (one-section rule), :599 (`originSignoff.ApprovedByUserId = sectionHeadUserId`), :619-627 (`DetermineOwnResultConformanceAsync` for each original test), :646 ("deliberately ignores IsSuperseded").
- Recommended correction: Add to §18.2 "What happens next": "The original section takes the retest's outcome. Each original test is marked Approved or Rejected from its own recorded result, so the test that triggered the retest stays Rejected. The outcome is recorded under the name of the Section Head who decided the retest sample, and no new signature is applied." Refer to this from §18.3 and §20.1.

#### F-B03 Sample status after Approve leaves out Retest requested
- Severity: LOW
- Section: §19.1 (System behavior, Status)
- Problem: The guide says "When no section of the sample is open any more and none is rejected, the sample becomes Approved." If another section is **Retest requested**, the sample becomes **Retest requested**, not Approved.
- Evidence: backend/MicroLIMS.Application/Services/SampleSectionRollup.cs:78-89 (`Compute`: Rejected comes before RetestRequested, which comes before Approved).
- Recommended correction: "...and none is rejected or waiting on a retest, the sample becomes Approved."

#### F-B04 Archived copy label can contradict the sample's final status
- Severity: MEDIUM
- Section: §19.2, §21.3
- Problem: The guide says a rejected sample's archived copy is labelled "Sample Reject". The label actually names the decision that closes the sample (`Sample {decision}`), not the final status. Example: laboratory A rejects while laboratory B is still open, so nothing is archived yet. When laboratory B later approves its section, the sample closes as **Rejected**, but its frozen copy is labelled "Sample Approve". A GMP record labelled with the wrong outcome must be documented or fixed.
- Evidence: backend/MicroLIMS.Application/Services/SampleApprovalService.cs:522 (`FinalizeIfClosedAsync(sample, $"Sample {decision}", ...)`), :530 (archives whenever the sample status is Approved or Rejected).
- Recommended correction: In §21.3, state that the label names the decision that closed the sample. For a sample tested by more than one laboratory, this can differ from the final status. Raise it as a defect.

#### F-B05 Dashboard review and approval counts depend on role type, not permission
- Severity: LOW
- Section: §17.1 (System behavior); the same wording appears in the §19.1 note
- Problem: "for users holding the Reviewer, Section Head or System Administrator role by default" suggests that the grant can be configured. The notice is hard-coded to role type. Granting *Sample Technical Review* to another role does not show that role the count.
- Evidence: backend/MicroLIMS.Application/Services/DashboardNotificationService.cs:170 (approval count: Section Head and System Administrator only), :184 (review count).
- Recommended correction: Drop "by default": "for users whose role is Reviewer, Section Head or System Administrator".

#### F-B06 Growth Promotion: a minimum recovery without a maximum makes every challenge NonConform
- Severity: HIGH
- Section: §22.1 (How to use it, step 4), §22.3 (System behavior)
- Problem: The guide presents **Recovery Min (%)** and **Recovery Max (%)** as separately optional. It says a challenge is Conform "when the recovery is inside the configured minimum and maximum". The configuration accepts a minimum with a blank maximum. The evaluation then compares `recovery <= null`, which is always false. As a result, every Growth Promotion challenge is NonConform and the lot is set to **Rejected** at once. A user who follows step 4 and enters only a minimum (the usual pharmacopoeial way to state a recovery criterion) would reject good lots.
- Evidence: backend/MicroLIMS.Application/Workflows/MediaEvaluationEngine.cs:229-231; backend/MicroLIMS.Application/Services/MasterData/MediaMasterDataService.cs:89-90 and 102-103 (only min > max is checked; either value can be empty).
- Recommended correction: In §22.1 step 4, say: "Enter both Recovery Min (%) and Recovery Max (%), or neither. If you enter only a minimum, every result is judged NonConform." Raise it as a defect.

#### F-B07 Media storage move: the listed reason message does not exist on this screen
- Severity: LOW
- Section: §22.5 (Validation)
- Problem: The guide lists "Reason must be at least 5 characters." The media **Move Storage** dialog does not show this message. It only turns the field red and disables **Move**, and its helper text reads "Recorded in the audit trail.". The message belongs to the cryovial dialogs. The server only checks that the reason is not blank.
- Evidence: frontend/src/modules/laboratoryConfiguration/media/components/MediaStoragePanel.tsx:130, 196-197. The message appears only in frontend/src/modules/laboratoryConfiguration/cryovials/components/MoveCryovialStorageDialog.tsx:76. Server check: backend/MicroLIMS.Application/Services/MediaStorageService.cs:143-144.
- Recommended correction: Replace it with: "The **Move** button stays disabled until the reason has at least 5 characters."

#### F-B08 "Returned for revision" in the physicochemical panels contradicts §18.1
- Severity: MEDIUM
- Section: §27.2 (What you see, What happens next) and §27.5, §27.7 ("Returned samples"), compared with §18.1
- Problem: §27 tells analysts that a reviewer can return a physicochemical test, and that the panel then shows "Returned for revision: {reason}". §18.1 says the screen offers **Return to Analyst** for count tests only. The screen decides this with a heuristic: a test counts as a count test if it has count readings, or `Result` rows without biochemical, location or pathogen data.
  - Tests that write only analysis entries (measurement, gravimetric, qualitative, titration, HPLC and ICP assay) never get the button.
  - Dissolution and disintegration also write `Result` rows, so they may get it.
  
  A reader of §27 cannot tell whether a physicochemical test can be returned from the screen. When the server does return an analysis test, it goes back to **Running** (see F-B01).
- Evidence: frontend/src/modules/testingWorkspace/sampleSummary/TestResultsSection.tsx:38-44; backend/MicroLIMS.Application/Workflows/TestWorkflow/DissolutionRecorder.cs:194 and DisintegrationRecorder.cs:202 (`_db.Results.Add`); SampleReviewService.cs:249-251.
- Recommended correction: State in §27.2 which physicochemical tests a reviewer can return from the screen (check this in the running application) and refer to §18.1. Otherwise, state that a signed physicochemical result is corrected through Not Conform or a retest at approval (§18.2, §19.2).

#### F-B09 Editing a lot: GMP-relevant fields can be changed without a reason or signature
- Severity: MEDIUM
- Section: §29.5
- Problem: The guide lists only "storage location, manufacturer, code or quantity received" as editable. The edit also changes a lot's purity, moisture content, expiry date, batch number, unit, material type and organism. This applies to every lot except working standards, at any time, including after the lot was used in a system suitability, a titrant standardization or a titration. No reason, password or signature is asked. Changing **Unit** does not convert the quantities, and lowering **Quantity Received** can take **Qty Remaining** below zero. For reference and primary standards, purity and moisture feed the calculations in §27.5 and §28.2, so users and QA need to know this.
- Evidence: backend/MicroLIMS.Application/Services/MaterialService.cs:210-299 (`UpdateAsync` sets every field, then `QuantityRemaining += receivedDelta`; no reason or signature).
- Recommended correction: List the editable fields in §29.5. State that no reason or signature is asked and that the unit is not converted. Add a reviewer note that changing purity, moisture or expiry after a lot was used is not blocked, and raise it as a GAP.

#### F-B10 Chromatography and ICP Methods Masters have no procedure
- Severity: MEDIUM
- Section: §27 (completeness against USER_GUIDE_STRUCTURE row 27 and COVERAGE_MATRIX MOD-HPLCM-001 and MOD-ICPM-001)
- Problem: USER_GUIDE_STRUCTURE row 27 lists MOD-HPLCM-001 (Chromatography Methods Master, for HPLC and GC) and MOD-ICPM-001 (ICP Methods Master) among the modules of §27, and the coverage matrix places them in the physicochemical section. §27 only mentions these masters, for example "An active HPLC method ... exists in the Chromatography Methods Master". No sub-section explains how the method values are set: SST limits, injection count, theoretical weights, standard dilution, residual-solvent standard concentration, ICP calibration levels, minimum r, ICV and CCV limits, or calibration age. §27.5 to §27.7 say these values decide pass or fail. No heading in the guide mentions "Method".
- Evidence: documentation/USER_GUIDE_STRUCTURE.md row 27; documentation/COVERAGE_MATRIX.md:58-59; searching documentation/USER_GUIDE.md for headings that contain "Method" finds none.
- Recommended correction: Add §27.10 "Chromatography and ICP Methods Masters". Cover the permission (*Manage master data*), the reason-for-change dialog, and the fields that decide SST and calibration results. Alternatively, assign these masters to §31 and document them there.

#### F-B11 Status names in the monitoring sections differ from §17
- Severity: LOW
- Section: §23.4, §23.5, §24.4, §24.5, §25.4, §25.5
- Problem: These sections say the sample moves to **Under Testing** and then **Pending Review**. §17 and §19 call the same states **In testing** / **In Testing** and **Under review** / **Under Review**. The application is itself inconsistent: the sample register shows "Under Testing" and "Pending Review" for `InTesting` and `UnderReview`, while the status badge shows "In Testing" and "Under Review". A reader cannot tell that these names mean the same states.
- Evidence: frontend/src/modules/receiving/components/SampleRegisterTable.tsx:61-62; frontend/src/components/StatusBadge.tsx:31-32.
- Recommended correction: Use **In Testing** and **Under Review**, as in §17. Add once: "The sample register shows these as Under Testing and Pending Review."

#### F-B12 The quoted Tests to Retest helper text does not match the screen
- Severity: LOW
- Section: §18.2 (What you see)
- Problem: The quoted text says "new sample(s)". The screen prints "new sample" for Retest Retained Sample and "new samples" for New Sample Required.
- Evidence: frontend/src/modules/testingWorkspace/sampleSummary/ApprovalSignaturesCard.tsx:353.
- Recommended correction: In §18.2, quote "...only the checked test(s) move to the new sample; everything else on this sample is left untouched." Use the plural form in §18.3.

#### F-B13 Only the screen stops approval of a destroyed pending cryovial batch
- Severity: LOW
- Section: §26.3 (Before you start, System behavior)
- Problem: "The batch must be Pending Review and not destroyed" reads as a rule the system enforces. The server checks only that the batch is still pending and that the signer did not prepare it. A pending batch destroyed through §26.4 is protected from approval only because the screen hides the actions.
- Evidence: backend/MicroLIMS.Application/Services/CryovialService.cs:148-151 (no `IsDestroyed` check before signing); frontend/src/modules/laboratoryConfiguration/cryovials/components/CryovialReviewTable.tsx:116.
- Recommended correction: "The screen offers Approve and Reject only for a pending batch that is not destroyed."

### Summary
- CRITICAL: 0
- HIGH: 1 (F-B06)
- MEDIUM: 6 (F-B01, F-B02, F-B04, F-B08, F-B09, F-B10)
- LOW: 6 (F-B03, F-B05, F-B07, F-B11, F-B12, F-B13)

Overall, part B is accurate and closely tied to the code. The GMP core matches the implementation in detail:
- the review and approval gates, and the order of the checks;
- the segregation-of-duties rules, with no exemption for System Administrator;
- the signature meanings;
- the four release decisions;
- the close-testing rules;
- the media and cryovial release rules;
- the working-standard sign-off by three different people;
- the default role grants.

Almost every sampled label and message exists verbatim. The guide already flags most real system weaknesses through its GAP notes.

The main risks are:
- the Growth Promotion recovery configuration trap (F-B06);
- under-described traceability where a retest outcome is copied back to the original sample (F-B02);
- archived-copy labels that can contradict the final status (F-B04);
- unsigned editing of standard-lot purity and moisture (F-B09);
- missing documentation of the HPLC, GC and ICP method masters (F-B10).

## Phase 11 review — part C (§30–§44, plus document-control header and contents list)

### Method and coverage

Scope: the document-control header and contents list (lines 1–63), and §30–§44 (lines 6855–10496 of `documentation/USER_GUIDE.md`). HTML comments were ignored.

Order of work: GMP-sensitive and permission content first (§30 equipment checks, §31 item, specification and preparation approval, §32.4 signed workbook load and verification, §35–§37 audit trail, signatures and segregation of duties, §38 Document Control approval, review and acknowledgement, §39 users and roles). Then every other sub-section was sampled.

Evidence used:
- Default grants: `backend/MicroLIMS.Persistence/Seed/DbSeeder.cs:521-575`, `backend/MicroLIMS.Tests/ArchitectureTests/authorization-matrix.txt` and `PermissionConstants.Enforced`.
- Service and controller code for each rule.
- Front-end source for labels.
- Every quoted message or label was checked with `git grep -F`.

Coverage:
- Sub-sections reviewed: 30.1–30.4, 31.1–31.6, 32.1–32.4, 33.1–33.2, 34.1–34.8, 35.1–35.3, 36.1–36.3, 37.1–37.4, 38.1–38.15, 39.1–39.2, 40.1–40.2, 41.1, 42.1–42.9, 43.1–43.4, 44.1–44.6, plus the header and contents.
- Claims sampled: about 190. That is about 150 quoted strings or labels checked with `git grep -F`, plus about 40 behaviour, permission or calculation claims read in code. Examples of the behaviour claims: incubator eligibility, specification evaluator, Count-Tiered comparison, workbook verify and segregation of duties, the segregation-of-duties guard, the audit exclusion list, the signature lockout constants, KPI constants, Document Control approval, review and void rules, training-matrix scoping, report export limits and audit, and retest analyst carry-over.
- Contents list: every anchor was compared with its heading slug. All 44 resolve.
- The header's code commit `f773042c` is an ancestor of `HEAD`.

Not checked:
- Screenshots (placeholders only).
- Runtime behaviour. Nothing was run, per the brief.
- Visual layout claims, for example "forced light theme" and chip colours.
- Most KPI formula details beyond their constants.
- The fields of Change items and Impact assessment (already GAP-043).
- Whether database-level audit protection exists in the live database (the guide already qualifies this).
- §42 messages that belong to other parts' modules: sampled only, not traced to every caller.

### Findings

#### F-C01 Incubator rejection message is not "always" the same
- Severity: MEDIUM
- Section: §30.3 (What happens next)
- Problem: The guide says that when an incubator is rejected "the message is always 'The selected incubator's set point is outside this medium's temperature range.'" That generic text is used only by the individual/grouped test-step path. Shared TSB starts and pathogen confirmatory setup (both listed in the same bullet above) return different messages that name the incubator and the medium.
- Evidence: `backend/MicroLIMS.Application/Workflows/TestWorkflow/TestWorkflowSupport.cs:807` (generic text); `backend/MicroLIMS.Application/Workflows/TestWorkflow/IncubationStepRecorder.cs:150` ("The shared TSB can't start: the selected incubator's set point is outside the {min}-{max} °C range of ..."); `backend/MicroLIMS.Application/Services/PathogenSessionService.cs:558` and `:975` ("Incubator {code}'s set point is outside the {range} range of ...").
- Recommended correction: Replace "always" with "in test steps"; list the shared-TSB and pathogen-confirmation messages too. Keep the point that none of them names an overdue calibration as the cause.

#### F-C02 An incubator with no calibration due date passes the calibration check
- Severity: MEDIUM
- Section: §30.1 (System behavior, "Which record do test steps check?"), §30.3 (What happens next)
- Problem: The guide says the check is "that its calibration due date has not passed". The code also accepts an incubator whose configuration record has **no** calibration due date (`CalibrationDueDate == null`). A user would assume a missing date blocks use. This matters for GMP equipment control and is not stated anywhere in §30.
- Evidence: `backend/MicroLIMS.Application/Services/IncubatorEligibilityService.cs` (`GetEligibleIncubatorsAsync`: `(e.CalibrationDueDate == null || e.CalibrationDueDate >= now)`).
- Recommended correction: Add: "An incubator with no calibration due date in its configuration record is accepted." Add a reviewer note or GAP reference.

#### F-C03 Production-stage delete dialog quoted as fact, but samples lose their stage
- Severity: HIGH
- Section: §32.2 (What you see, Production Stages delete confirmation; How to use it step 5)
- Problem: The guide quotes the confirmation "Samples already recorded with this stage keep it; only the dropdown changes." without comment, and the procedure tells the user to "click the bin icon and confirm". In fact deleting a stage sets `ProductionStageId` to null on every sample that used it. That changes which stage-specific specifications and replicate counts apply (§31.3, §31.5). The reviewer note in §32.2 (GAP-709) says this, so the section contradicts itself, and a user following the procedure would trust the on-screen text.
- Evidence: `frontend/src/modules/laboratoryConfiguration/masterDataSimple/ProductionStagesCard.tsx:390` (dialog text); `backend/MicroLIMS.Application/Services/MasterData/ReferenceListMasterDataService.cs:143-151` (hard delete, no reference check); `backend/MicroLIMS.Persistence/Configurations/SampleConfiguration.cs:32` (`OnDelete(DeleteBehavior.SetNull)`).
- Recommended correction: In "What you see", mark the dialog text as inaccurate. In step 5, add a warning: "Deleting a stage removes it from every sample already recorded with it. Do not delete a stage that is in use." Keep GAP-709.

#### F-C04 "No exemption, two people for every decision" is overstated and contradicts other sections
- Severity: HIGH
- Section: §37.4 ("Absolute Rule: No Administrative Exemption"); also §42.4 intro and §43.3 FAQ 15
- Problem: The guide says "Every analytical review and release decision requires two distinct authorized individuals". It also says that a System Administrator who "standardizes a titrant ... is strictly blocked from reviewing or approving that record". §42.4 says "MicroLIMS does not let the same person perform and then review or approve."
  - Titrant standardization has no review or approval step, and it is not counted as "performing a test" by the segregation-of-duties guard. The titrant example is unsupported.
  - The guide's own §31.2 says the item preparation configuration can be approved by the person who saved it.
  - The guide's own §34.7 says workflow profile versions are signed by their creator with no second person.
  - Solution preparation completion, HPLC system suitability confirmation and ICP calibration confirmation are single-signer actions.
  - Segregation of duties is enforced only for the actions in the §37.4 table. A reader relying on the "absolute" wording would assume controls that do not exist.
- Evidence:
  - `backend/MicroLIMS.Application/Services/SegregationOfDutiesGuard.cs:25-54`: "performed" covers the assigned analyst, preparation, results, count readings, pathogen observations, step results, test analyses and confirmatory observations. Titrant standardization is not included.
  - `backend/MicroLIMS.Application/Services/TitrantStandardizationService.cs:244`: single signature, no review step.
  - `backend/MicroLIMS.Application/Services/ItemPreparationConfigurationService.cs:84-97` (`ApproveAsync`): no check against the creator.
  - `backend/MicroLIMS.Application/Services/Kpi/WorkflowProfileService.cs:81,117` (GAP-820).
- Recommended correction:
  - Replace the "Absolute Rule" paragraph with: "The rules in the table compare user accounts; System Administrators are not exempt from them. Other signed or approved actions (for example item preparation configuration approval, workflow profile versions, titrant standardization, solution completion) have no second-person check."
  - Remove "standardizes a titrant".
  - In §42.4 and FAQ 15, limit the wording to "for the actions listed in §37.4".

#### F-C05 "Select All Matching" exports more than the approved records, and the PDF is silently cut at 200
- Severity: HIGH
- Section: §34.2 (How to use it, step 7; Selection action bar)
- Problem: The guide says "Only records with Approved status can be selected" and tells the user to "Select All {totalCount} Matching Records" before **Export Selected CSV / PDF**. After **Select All**:
  - **Export Selected CSV** runs the normal server export for the current filters. It includes records of every approval status (Pending, Rejected, Voided, Cancelled), not only the approved ones.
  - **Export Selected PDF** fetches only the first 200 matching rows, again of every status, and gives no warning. Its title is "Laboratory Results Export (All Matching)".

  A user who thinks they are exporting approved results only could pass on unapproved or rejected results, or an incomplete list. This matters for GMP reporting.
- Evidence:
  - `frontend/src/modules/reports/components/ReportResultsTable.tsx:141-144` (`selectAllMatchingRecords` only sets a flag).
  - `:173-177` (`handleExportSelectedCsv` calls `runExport()` with `appliedParams`; no approval filter is added).
  - `:213-228` (`handleExportSelectedPdf` uses `searchResults({ ...appliedParams, page: 1, pageSize: 200 })`).
  - The tooltip at `:348` claims "Selects every Approved result matching your current filters".
- Recommended correction: State that after **Select All Matching Records** the export contains every record that matches the filters, whatever its approval status. Tell users to set **Approval Status = Approved** first. Add that the selected-PDF export holds at most 200 records. Raise this as a GAP; the code does not match its own tooltip.

#### F-C06 Document compliance drill-down shows every person's status to any user
- Severity: MEDIUM
- Section: §38.10 (Who can use it; How to use it step 4)
- Problem: The guide says users without the *Document Training Assignment* permission "see only their own row". That is true for the grid and the user drill-down. The **Document Compliance Profile** drawer (column header click) is not restricted, though. Any signed-in user can see the qualification status of every assigned person on a document. The step 4 text lists this drawer without any limit.
- Evidence: `backend/MicroLIMS.API/Controllers/DocumentControl/DocumentTrainingMatrixController.cs`. `GetTrainingMatrixGrid` and `GetUserCompliance` check `IsAdminOrSectionHead`. `GetDocumentCompliance(int documentMasterId)` has no check.
- Recommended correction: Add: "The Document Compliance Profile drawer shows every assigned person's status to any user who can open the matrix." Raise a GAP.

#### F-C07 "Previous Period" comparison option does nothing
- Severity: MEDIUM
- Section: §34.5 (What you see, **Compare With**)
- Problem: **Previous Period** is listed as a comparison choice with no caveat. Selecting it has no effect. Only Product Comparison and Location Comparison open the comparison dialog, and the chart has no previous-period overlay.
- Evidence: `frontend/src/modules/reports/components/TrendingTab.tsx:305-321`. A code comment says "Previous Period" is out of scope, and only the two comparison values call `setCompareDialogOpen(true)`.
- Recommended correction: Add: "**Previous Period** can be selected but does not change the chart." Raise a GAP.

#### F-C08 Trending Category list is described as fixed
- Severity: LOW
- Section: §34.5 (What you see, **Category**)
- Problem: The four categories listed (FinishedProduct, RawMaterial, Water, EnvironmentalMonitoring) are only the built-in fallback, shown when the filter options fail to load. Normally the list comes from the server and can include After Cleaning and other categories. The same page mentions an After Cleaning location cascade.
- Evidence: `frontend/src/modules/reports/components/TrendingTab.tsx:269-281`.
- Recommended correction: "Category: the categories that have results; if the lists cannot be loaded, four default categories are shown." Link this to GAP-800.

#### F-C09 Two Quick Reports tiles are not filters
- Severity: LOW
- Section: §34.2 (What you see, Quick Reports)
- Problem: The guide calls all eight tiles "one-click filter tiles". **Media/GPT** and **Reference Strains** switch to their own tabs, and **Custom Report** clears the presets. Only the five category tiles filter the Record Search.
- Evidence: `frontend/src/modules/reports/components/QuickReportsTiles.tsx:12-48`; `frontend/src/modules/reports/RecordSearchTab.tsx:97-102` (`onNavigateTab?.(2)` / `(3)`).
- Recommended correction: Describe the five filter tiles. Say that **Media/GPT** and **Reference Strains** open the matching tabs.

#### F-C10 Raw signature meaning code shown to users
- Severity: LOW
- Section: §34.7 (How to use it, step 6)
- Problem: "The system attaches the electronic signature meaning **MasterDataChanged**" is an internal enum name. §32.4 writes "master data changed" for the same meaning, and the §32 review log says meaning codes were removed.
- Evidence: `backend/MicroLIMS.Application/Services/Kpi/WorkflowProfileService.cs:81`.
- Recommended correction: Write "meaning: master data changed", consistent with §32.4.

#### F-C11 "Nine kinds" of unaudited records, eight listed
- Severity: LOW
- Section: §35.1 (Records that are not audited)
- Problem: The text says nine kinds but names eight. "The audit records themselves" covers two record types: audit log entries and audit field-change rows.
- Evidence: `backend/MicroLIMS.Persistence/DbContext/MicroLimsDbContext.cs:310-318`.
- Recommended correction: Write "the audit records and their field-change details", or drop the number.

#### F-C12 Glossary "Active Equipment" contradicts §30.1
- Severity: LOW
- Section: §44.5
- Problem: The glossary says the tab lists "in-service assets". §30.1 says it "lists every asset in scope". The code returns all inventory assets in scope, whatever their status.
- Evidence: `backend/MicroLIMS.Application/Services/EquipmentInventoryService.cs:242-246` (no status filter).
- Recommended correction: "A tab of the Equipment Inventory listing every asset in your laboratories with its activities."

#### F-C13 Glossary "Source File" access rule is wrong
- Severity: LOW
- Section: §44.6
- Problem: The glossary says the Word source is "accessible only during draft and technical review stages". Access depends on who you are, not on the revision stage:
  - System Administrators, Section Heads and the document owner or assigned author can always open it.
  - A technical reviewer can open it only while their review task is open.
- Evidence: `backend/MicroLIMS.Application/Services/DocumentControl/DocumentAuthorizationService.cs:156-190` (`CanAccessSourceFileAsync`).
- Recommended correction: "The editable Word file. It can be opened by System Administrators, Section Heads, the document owner or assigned author, and by a technical reviewer while their review is open."

#### F-C14 Glossary terms that do not match the screens
- Severity: LOW
- Section: §44.2, §44.5
- Problem:
  - "Set Point ... for an incubator, refrigerator, or oven": set points can be edited only for incubators (§30.3), and the equipment types have no refrigerator or oven.
  - "On-Time Reading Rate" is not a screen label. The KPI tile is **Incubation reads on time** (§34.6).
- Evidence: `EquipmentConfigurationService` ("Equipment {code} is not an incubator."); §30.2 type list; `backend/MicroLIMS.Application/Services/Kpi/IncubationReadsOnTimeKpi.cs:18`.
- Recommended correction: Limit Set Point to incubators. Rename the term to **Incubation reads on time**.

#### F-C15 Guide asserts "legal" effect of reading acknowledgement in its own voice
- Severity: MEDIUM
- Section: §38.9 (Purpose; System behavior, "Conscious Legal Acknowledgement")
- Problem: Screen text that is quoted is fine; GAP-023 already covers the on-screen regulatory wording. The guide's own prose, however, states "record conscious legal read-and-understand acknowledgements" and "Legal acknowledgement strictly requires ...". That claims a legal or regulatory status the brief does not allow the guide to assert. The acknowledgement is unsigned (no password), as the same section says.
- Evidence: `backend/MicroLIMS.Application/Services/DocumentControl/DocumentAcknowledgementService.cs:127-131` (checkbox check only; no signature service call).
- Recommended correction: Use neutral wording: "record a read-and-understood acknowledgement (checkbox confirmation, no password)". Keep the legal wording only inside quoted screen text.

#### F-C16 Document-control header lacks approval and history fields; reviewer notes are in the user text
- Severity: LOW
- Section: Document-control header (lines 3–13)
- Problem: The header has Title, Version, Date, branch, code commit, Author and Status. It has no rows for reviewer, approver, approval date or signature, and no revision history table. The guide also contains many "Note for reviewers" boxes in the body. These suit a draft but must be removed or turned into user-facing warnings before controlled use. The header names DOCUMENTATION_GAPS.md without a path.
- Evidence: `documentation/USER_GUIDE.md:3-13`.
- Recommended correction: Add Reviewed by, Approved by, Effective date and a revision history table. Plan how reviewer notes are resolved before release. Give the path `documentation/DOCUMENTATION_GAPS.md`.

#### F-C17 Incubation-reads KPI window described ambiguously
- Severity: LOW
- Section: §34.6 tile 7; §44.2 "On-Time Reading Rate"
- Problem: "Finished within 4 hours of the expected read time" suggests a window of plus or minus 4 hours. In the code, a read is on time when it happens no later than the expected time plus 4 hours, so early reads also count as on time.
- Evidence: `backend/MicroLIMS.Application/Services/Kpi/IncubationReadsOnTimeKpi.cs:18,59`; `KpiTypes.cs:46`.
- Recommended correction: "read no later than 4 hours after the expected read time".

### Summary

Counts by severity: CRITICAL 0, HIGH 3, MEDIUM 5, LOW 9 (17 findings).

- HIGH: F-C03 (production-stage delete), F-C04 (segregation of duties overstated), F-C05 (Select-All export).
- MEDIUM: F-C01, F-C02, F-C06, F-C07, F-C15.
- LOW: F-C08 to F-C14, F-C16, F-C17.

Overall assessment:
- Part C is in good shape on the points that matter most for GMP:
  - Default role grants and the permissions named in §30–§39 match `DbSeeder` and the authorization matrix in every case sampled.
  - Almost every quoted message exists word for word.
  - The audit-trail scope, the signature lockout (5 attempts in 15 minutes), the second-person workbook verification, the Document Control segregation-of-duties and void rules, and the "not implemented" lists (instrument import, ERP, obsolescence, IQ/OQ/PQ) are accurate and properly marked with GAP references.
  - No CRITICAL compliance claims were found. Regulatory wording appears only as quoted screen text with GAP-023 notes.
- The main weaknesses:
  1. Over-general statements about segregation of duties that the guide itself contradicts elsewhere (F-C04).
  2. On-screen text repeated as fact where the code behaves differently (F-C03, F-C05).
  3. A few report features described as working when they do nothing or behave differently (F-C07, F-C09).

  These should be corrected before the guide is used under document control.

