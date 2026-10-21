# Phase 11 review — part B (§17–§29)

## Method and coverage
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

## Findings

### F-B01 Return to Analyst: count-test behaviour stated for all tests
- Severity: MEDIUM
- Section: §18.1
- Problem: "System behavior" and "Status" say the test always goes back to **Incubating** and its count readings are set aside. The system also accepts result-entry (analysis) tests, as the guide's own GAP-300 note says. For those tests the active analyses and parameter results are set aside instead, no incubation is reopened, and the test goes back to **Running**, not Incubating.
- Evidence: backend/MicroLIMS.Application/Services/SampleReviewService.cs:197-243 (the count branch and the `UsesTestAnalysis` branch), :249-251 (`targetStep = UsesTestAnalysis ? WorkflowStep.Running : WorkflowStep.Incubating`).
- Recommended correction: Say in the bullets that they apply to count tests. Add: "For a result-entry test returned through the system, the active entries are set aside and the test goes back to Running."

### F-B02 Retest outcome copied to the original sample: per-test results and traceability not described
- Severity: MEDIUM
- Section: §18.2, §18.3, §20.1 (System behavior)
- Problem: The guide says the retest's outcome "is copied to the original sample's section". That is true for the section status, but the system then sets each test of that section on the original sample to **Approved** or **Rejected** from that test's own recorded result. It ignores the retest outcome and whether the test was superseded. So after an approved retest, the original failing test stays **Rejected**. The outcome is copied only when the retest sample has exactly one laboratory section. The original sample's "approved by" field is filled with the user who decided the retest sample, and no signature is recorded on the original. A reviewer reading the original record needs these traceability facts.
- Evidence: backend/MicroLIMS.Application/Services/SampleApprovalService.cs:554 (one-section rule), :599 (`originSignoff.ApprovedByUserId = sectionHeadUserId`), :619-627 (`DetermineOwnResultConformanceAsync` for each original test), :646 ("deliberately ignores IsSuperseded").
- Recommended correction: Add to §18.2 "What happens next": "The original section takes the retest's outcome. Each original test is marked Approved or Rejected from its own recorded result, so the test that triggered the retest stays Rejected. The outcome is recorded under the name of the Section Head who decided the retest sample, and no new signature is applied." Refer to this from §18.3 and §20.1.

### F-B03 Sample status after Approve leaves out Retest requested
- Severity: LOW
- Section: §19.1 (System behavior, Status)
- Problem: The guide says "When no section of the sample is open any more and none is rejected, the sample becomes Approved." If another section is **Retest requested**, the sample becomes **Retest requested**, not Approved.
- Evidence: backend/MicroLIMS.Application/Services/SampleSectionRollup.cs:78-89 (`Compute`: Rejected comes before RetestRequested, which comes before Approved).
- Recommended correction: "...and none is rejected or waiting on a retest, the sample becomes Approved."

### F-B04 Archived copy label can contradict the sample's final status
- Severity: MEDIUM
- Section: §19.2, §21.3
- Problem: The guide says a rejected sample's archived copy is labelled "Sample Reject". The label actually names the decision that closes the sample (`Sample {decision}`), not the final status. Example: laboratory A rejects while laboratory B is still open, so nothing is archived yet. When laboratory B later approves its section, the sample closes as **Rejected**, but its frozen copy is labelled "Sample Approve". A GMP record labelled with the wrong outcome must be documented or fixed.
- Evidence: backend/MicroLIMS.Application/Services/SampleApprovalService.cs:522 (`FinalizeIfClosedAsync(sample, $"Sample {decision}", ...)`), :530 (archives whenever the sample status is Approved or Rejected).
- Recommended correction: In §21.3, state that the label names the decision that closed the sample. For a sample tested by more than one laboratory, this can differ from the final status. Raise it as a defect.

### F-B05 Dashboard review and approval counts depend on role type, not permission
- Severity: LOW
- Section: §17.1 (System behavior); the same wording appears in the §19.1 note
- Problem: "for users holding the Reviewer, Section Head or System Administrator role by default" suggests that the grant can be configured. The notice is hard-coded to role type. Granting *Sample Technical Review* to another role does not show that role the count.
- Evidence: backend/MicroLIMS.Application/Services/DashboardNotificationService.cs:170 (approval count: Section Head and System Administrator only), :184 (review count).
- Recommended correction: Drop "by default": "for users whose role is Reviewer, Section Head or System Administrator".

### F-B06 Growth Promotion: a minimum recovery without a maximum makes every challenge NonConform
- Severity: HIGH
- Section: §22.1 (How to use it, step 4), §22.3 (System behavior)
- Problem: The guide presents **Recovery Min (%)** and **Recovery Max (%)** as separately optional. It says a challenge is Conform "when the recovery is inside the configured minimum and maximum". The configuration accepts a minimum with a blank maximum. The evaluation then compares `recovery <= null`, which is always false. As a result, every Growth Promotion challenge is NonConform and the lot is set to **Rejected** at once. A user who follows step 4 and enters only a minimum (the usual pharmacopoeial way to state a recovery criterion) would reject good lots.
- Evidence: backend/MicroLIMS.Application/Workflows/MediaEvaluationEngine.cs:229-231; backend/MicroLIMS.Application/Services/MasterData/MediaMasterDataService.cs:89-90 and 102-103 (only min > max is checked; either value can be empty).
- Recommended correction: In §22.1 step 4, say: "Enter both Recovery Min (%) and Recovery Max (%), or neither. If you enter only a minimum, every result is judged NonConform." Raise it as a defect.

### F-B07 Media storage move: the listed reason message does not exist on this screen
- Severity: LOW
- Section: §22.5 (Validation)
- Problem: The guide lists "Reason must be at least 5 characters." The media **Move Storage** dialog does not show this message. It only turns the field red and disables **Move**, and its helper text reads "Recorded in the audit trail.". The message belongs to the cryovial dialogs. The server only checks that the reason is not blank.
- Evidence: frontend/src/modules/laboratoryConfiguration/media/components/MediaStoragePanel.tsx:130, 196-197. The message appears only in frontend/src/modules/laboratoryConfiguration/cryovials/components/MoveCryovialStorageDialog.tsx:76. Server check: backend/MicroLIMS.Application/Services/MediaStorageService.cs:143-144.
- Recommended correction: Replace it with: "The **Move** button stays disabled until the reason has at least 5 characters."

### F-B08 "Returned for revision" in the physicochemical panels contradicts §18.1
- Severity: MEDIUM
- Section: §27.2 (What you see, What happens next) and §27.5, §27.7 ("Returned samples"), compared with §18.1
- Problem: §27 tells analysts that a reviewer can return a physicochemical test, and that the panel then shows "Returned for revision: {reason}". §18.1 says the screen offers **Return to Analyst** for count tests only. The screen decides this with a heuristic: a test counts as a count test if it has count readings, or `Result` rows without biochemical, location or pathogen data.
  - Tests that write only analysis entries (measurement, gravimetric, qualitative, titration, HPLC and ICP assay) never get the button.
  - Dissolution and disintegration also write `Result` rows, so they may get it.
  
  A reader of §27 cannot tell whether a physicochemical test can be returned from the screen. When the server does return an analysis test, it goes back to **Running** (see F-B01).
- Evidence: frontend/src/modules/testingWorkspace/sampleSummary/TestResultsSection.tsx:38-44; backend/MicroLIMS.Application/Workflows/TestWorkflow/DissolutionRecorder.cs:194 and DisintegrationRecorder.cs:202 (`_db.Results.Add`); SampleReviewService.cs:249-251.
- Recommended correction: State in §27.2 which physicochemical tests a reviewer can return from the screen (check this in the running application) and refer to §18.1. Otherwise, state that a signed physicochemical result is corrected through Not Conform or a retest at approval (§18.2, §19.2).

### F-B09 Editing a lot: GMP-relevant fields can be changed without a reason or signature
- Severity: MEDIUM
- Section: §29.5
- Problem: The guide lists only "storage location, manufacturer, code or quantity received" as editable. The edit also changes a lot's purity, moisture content, expiry date, batch number, unit, material type and organism. This applies to every lot except working standards, at any time, including after the lot was used in a system suitability, a titrant standardization or a titration. No reason, password or signature is asked. Changing **Unit** does not convert the quantities, and lowering **Quantity Received** can take **Qty Remaining** below zero. For reference and primary standards, purity and moisture feed the calculations in §27.5 and §28.2, so users and QA need to know this.
- Evidence: backend/MicroLIMS.Application/Services/MaterialService.cs:210-299 (`UpdateAsync` sets every field, then `QuantityRemaining += receivedDelta`; no reason or signature).
- Recommended correction: List the editable fields in §29.5. State that no reason or signature is asked and that the unit is not converted. Add a reviewer note that changing purity, moisture or expiry after a lot was used is not blocked, and raise it as a GAP.

### F-B10 Chromatography and ICP Methods Masters have no procedure
- Severity: MEDIUM
- Section: §27 (completeness against USER_GUIDE_STRUCTURE row 27 and COVERAGE_MATRIX MOD-HPLCM-001 and MOD-ICPM-001)
- Problem: USER_GUIDE_STRUCTURE row 27 lists MOD-HPLCM-001 (Chromatography Methods Master, for HPLC and GC) and MOD-ICPM-001 (ICP Methods Master) among the modules of §27, and the coverage matrix places them in the physicochemical section. §27 only mentions these masters, for example "An active HPLC method ... exists in the Chromatography Methods Master". No sub-section explains how the method values are set: SST limits, injection count, theoretical weights, standard dilution, residual-solvent standard concentration, ICP calibration levels, minimum r, ICV and CCV limits, or calibration age. §27.5 to §27.7 say these values decide pass or fail. No heading in the guide mentions "Method".
- Evidence: documentation/USER_GUIDE_STRUCTURE.md row 27; documentation/COVERAGE_MATRIX.md:58-59; searching documentation/USER_GUIDE.md for headings that contain "Method" finds none.
- Recommended correction: Add §27.10 "Chromatography and ICP Methods Masters". Cover the permission (*Manage master data*), the reason-for-change dialog, and the fields that decide SST and calibration results. Alternatively, assign these masters to §31 and document them there.

### F-B11 Status names in the monitoring sections differ from §17
- Severity: LOW
- Section: §23.4, §23.5, §24.4, §24.5, §25.4, §25.5
- Problem: These sections say the sample moves to **Under Testing** and then **Pending Review**. §17 and §19 call the same states **In testing** / **In Testing** and **Under review** / **Under Review**. The application is itself inconsistent: the sample register shows "Under Testing" and "Pending Review" for `InTesting` and `UnderReview`, while the status badge shows "In Testing" and "Under Review". A reader cannot tell that these names mean the same states.
- Evidence: frontend/src/modules/receiving/components/SampleRegisterTable.tsx:61-62; frontend/src/components/StatusBadge.tsx:31-32.
- Recommended correction: Use **In Testing** and **Under Review**, as in §17. Add once: "The sample register shows these as Under Testing and Pending Review."

### F-B12 The quoted Tests to Retest helper text does not match the screen
- Severity: LOW
- Section: §18.2 (What you see)
- Problem: The quoted text says "new sample(s)". The screen prints "new sample" for Retest Retained Sample and "new samples" for New Sample Required.
- Evidence: frontend/src/modules/testingWorkspace/sampleSummary/ApprovalSignaturesCard.tsx:353.
- Recommended correction: In §18.2, quote "...only the checked test(s) move to the new sample; everything else on this sample is left untouched." Use the plural form in §18.3.

### F-B13 Only the screen stops approval of a destroyed pending cryovial batch
- Severity: LOW
- Section: §26.3 (Before you start, System behavior)
- Problem: "The batch must be Pending Review and not destroyed" reads as a rule the system enforces. The server checks only that the batch is still pending and that the signer did not prepare it. A pending batch destroyed through §26.4 is protected from approval only because the screen hides the actions.
- Evidence: backend/MicroLIMS.Application/Services/CryovialService.cs:148-151 (no `IsDestroyed` check before signing); frontend/src/modules/laboratoryConfiguration/cryovials/components/CryovialReviewTable.tsx:116.
- Recommended correction: "The screen offers Approve and Reject only for a pending batch that is not destroyed."

## Summary
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
