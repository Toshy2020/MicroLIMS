# Conflicts

This list records every place where the code, the business rules, the guide or another source disagree, or where a rule is doubtful. Each entry has an ID (`CONF-nnn`), an area, a status and the facts on both sides. An entry stays here after it is decided, so the decision and its date remain on record.

Entries CONF-001 to CONF-004 are the four known items from section 5.5 of the specification-set design. Entries CONF-005 to CONF-008 come from the whole-programme review of 2026-11-11 (cross-plan corrections, item 1).

Locations for CONF-001 to CONF-008 were added on 2026-11-12 from the area workers' reading of the code; nothing was run. Entries from CONF-010 on were raised while the area specifications were written, in ID blocks per area group (010-029, 030-059, 060-089, 090-119, 120-149, 150-179), so the numbering has gaps.

### CONF-001
- **Area:** MIC
- **Status:** Open
- **What the code does:** The count preview on screen rounds differently from the server. Location (reported by the area worker from reading, 2026-11-12): server rounding is `Math.Round` away from zero to 0 decimals in `backend/MicroLIMS.Application/Workflows/TestWorkflow/IncubationStepRecorder.cs` — `RecordCountTestAsync`, `RecordBatchResultsCoreAsync`, `RecordWaterBatchReadingsCoreAsync`. The screen preview code has not been located yet. See also CONF-011.
- **What the other source says:** Not recorded yet. Location: to be recorded when the area's FS is written.
- **Question:** Which rounding should the count preview follow, the screen rounding or the server rounding?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-002
- **Area:** NTF
- **Status:** Open
- **What the code does:** My Tasks and the workspace use different "ready to read" rules. Location (reported from reading, 2026-11-12): workspace — `backend/MicroLIMS.Application/Services/TestingWorkspaceService.cs` — `GetActiveSamplesAsync`, `GetWorkloadCountsAsync` (via `SampleWorkflowQueues.HasTestReadyToRead`) and `WorkflowStateResolver.cs` — `Resolve`; My Tasks — `MyTasksService.cs` — `GetMyTasksAsync`; notifications — `DashboardNotificationService.cs` — `ComputeAsync`. See also CONF-010.
- **What the other source says:** Not recorded yet. Location: to be recorded when the area's FS is written.
- **Question:** Which "ready to read" rule is correct, and should My Tasks and the workspace use the same rule?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-003
- **Area:** EQP
- **Status:** Decided
- **What the code does:** As first recorded: incubator selection checks the temperature range but not calibration status. Correction from reading the code, 2026-11-12: `backend/MicroLIMS.Application/Services/IncubatorEligibilityService.cs` — `GetEligibleIncubatorsAsync` does check calibration (due date empty, or not yet past). A null due date counts as in calibration in every place found that checks a date; those places differ in other ways (see CONF-126 and CONF-128, and FS-EQP-012).
- **What the other source says:** Not recorded yet. Location: to be recorded when the area's FS is written.
- **Question:** Should incubator selection check calibration status as well as the temperature range?
- **Decision:** Decided by the user 2026-11-11: equipment with no calibration due date counts as in calibration.
- **Date:** 2026-11-11

### CONF-004
- **Area:** RCV
- **Status:** Open
- **What the code does:** Sample numbers can collide under concurrent receipt. Location (reported from reading, 2026-11-12): `backend/MicroLIMS.Application/Services/ReferenceNumberGenerator.cs` — `GenerateAsync` (count plus one, no lock), with the unique index in `backend/MicroLIMS.Persistence/Configurations/SampleConfiguration.cs`. Water, EM and After Cleaning `ReceiveAsync` retry once; `ProductWorkflowEngine.ReceiveAsync` does not. See also CONF-060.
- **What the other source says:** Not recorded yet. Location: to be recorded when the area's FS is written.
- **Question:** How should sample numbers be kept unique when two receipts happen at the same time?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-005
- **Area:** MDM
- **Status:** Open
- **What the code does:** Grade matching differs between services: some are case-insensitive, some lower-case the value, and some use exact matching. Location (reported from reading, 2026-11-12): exact match in `backend/MicroLIMS.Application/Services/SpecificationLookup.cs` — `ApplicableGradeAsync`, `SelectForStage`; case-insensitive in `backend/MicroLIMS.Application/Workflows/ProductWorkflowEngine.cs` — `ResolveGrade`, `TestAppliesToGrade`, `IsApprovedNotApplicableFor`. Other services not yet read.
- **What the other source says:** Not recorded yet. Location: to be recorded when the area's FS is written.
- **Question:** Which grade matching rule should every service use?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-006
- **Area:** MIC
- **Status:** Open
- **What the code does:** Limit comparison is implemented in several places: `SpecLimitParser`, `SpecificationService`, `PathogenSessionService` and `SpecificationEvaluator`. Location (reported from reading, 2026-11-12): `SpecLimitParser.Compare`; `SpecificationService.CompareAgainstLimits`; `PathogenSessionService.CompareAgainstLimits`; `SpecificationEvaluator.Evaluate`. `WaterWorkflowEngine.Compare` and `IncubationStepRecorder.Compare` delegate to the parser. See also CONF-012.
- **What the other source says:** Not recorded yet. Location: to be recorded when the area's FS is written.
- **Question:** Should one place perform limit comparison, and which one?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-007
- **Area:** MDM
- **Status:** Open
- **What the code does:** Specification lookups bypass `SpecificationLookup` in five services. Location (reported from reading, 2026-11-12): `backend/MicroLIMS.Application/Workflows/TestWorkflow/TestWorkflowSupport.cs` — `ValidateTestAnalysisOrderAsync` queries `_db.Specifications` directly after resolving grade and stage. A search also shows direct queries in `SampleTestSelectionService.cs`, `SampleCorrectionService.cs` and `AddLaboratoryService.cs`; which five services are meant is not confirmed.
- **What the other source says:** Not recorded yet. Location: to be recorded when the area's FS is written.
- **Question:** Should all five services use `SpecificationLookup`, and does the bypass change any result?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-008
- **Area:** REV
- **Status:** Open
- **What the code does:** Conformance is decided privately in `SampleApprovalService` and separately in `ReportDocumentMapper`. Location (reported from reading, 2026-11-12): `backend/MicroLIMS.Application/Services/SampleApprovalService.cs` — `DetermineOwnResultConformanceAsync`; `backend/MicroLIMS.Application/Services/ReportDocumentMapper.cs` — `ConformingStatuses`, `ToResultsMatrix`, `ToAnalysisCard`; the certificate uses `ResultConformanceRules.FromStatus` in `Helpers/ResultConformance.cs`. See also CONF-023 and CONF-157.
- **What the other source says:** Not recorded yet. Location: to be recorded when the area's FS is written.
- **Question:** Should one shared rule decide conformance for approval and for reports?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-010
- **Area:** MIC
- **Status:** Open
- **What the code does:** The rule for "an incubation is long enough to record a result" is applied differently in different places. (1) A single plate-count step with no incubation transfer: no elapsed-time check at all before the count is recorded. Location: `backend/MicroLIMS.Application/Workflows/TestWorkflow/IncubationStepRecorder.cs` — `RecordResultCoreAsync`. (2) A plate-count step with a transfer: the count needs the end of the stage 2 window (start plus the configured maximum hours) to have passed. Location: same file — `RecordResultCoreAsync` calling `TestWorkflowSupport.RequireIncubationComplete`. (3) Batch orders: the minimum hours must have passed (stage 2 minimum for a transfer step). Location: same file — `RecordBatchResultsCoreAsync`, `RequireMinimumDurationElapsed`, `RequireStage2MinimumDurationElapsed`. (4) Pathogen broth and selective plating observation: the minimum hours of the medium. Location: `backend/MicroLIMS.Application/Workflows/TestWorkflow/PathogenConfirmationRecorder.cs` — `SubmitBrothCoreAsync`, `SubmitSelectivePlatingObservationCoreAsync`. (5) Confirmatory observations: the end of the window (longest maximum hours). Location: same file — `SubmitConfirmatoryObservationsAsync`. (6) The screen state "ready for result entry" uses the minimum hours. Location: `backend/MicroLIMS.Application/Services/WorkflowStateResolver.cs` — `Resolve`.
- **What the other source says:** The code comment in `SelectMediaCoreAsync` (`IncubationStepRecorder.cs`) says "the analyst can complete once the minimum duration has elapsed AND the end time has passed". `CLAUDE.md` line 99 (Rule 3.2) says a step cannot be recorded before the predecessor step and required incubation are completed. Related to CONF-002 (different "ready to read" rules).
- **Question:** Which incubation rule is correct for recording a result: minimum hours, end of the maximum window, or both? Should a single plate-count step without a transfer be refused by the server before the minimum has passed?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-011
- **Area:** MIC
- **Status:** Open
- **What the code does:** The reported text for a low count differs by path. A single plate count shows "<" followed by the dilution factor (for example "<10") when the calculated result is below the dilution factor. Location: `backend/MicroLIMS.Application/Workflows/TestWorkflow/IncubationStepRecorder.cs` — `RecordCountTestAsync`. EM and After Cleaning batch counts show "<1" when the average is below 1. Location: same file — `RecordBatchResultsCoreAsync`. Water batch counts show the rounded average (so "0") with no "<" rule. Location: same file — `RecordWaterBatchReadingsCoreAsync`. All three round with `MidpointRounding.AwayFromZero` to 0 decimals. The pathogen session matrix takes the reported text from the client. Location: `backend/MicroLIMS.Application/Services/PathogenSessionService.cs` — `SaveResultMatrixAsync`.
- **What the other source says:** CONF-001 (`documents/Conflicts.md`) records that the on-screen count preview rounds differently from the server; the screen code was searched for in `frontend/src` by "plateReadings", "rawPlate" and "TNTC" and no count preview was found, so the screen side is not recorded. No approved specification line in `documents/` was found for the "<" rule.
- **Question:** What should be reported for a count below the countable limit, and should it be the same for single, EM, After Cleaning and water counts? Which rounding does the screen preview use?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-012
- **Area:** MIC
- **Status:** Open
- **What the code does:** Two separate implementations compare a value with limits and treat the limit itself differently. Text limits (`SpecLimitParser.Compare`) treat a value equal to a limit as inside (exceeded only when below the minimum or above the maximum), and give the status order Specification, Action, Alert. Location: `backend/MicroLIMS.Application/Services/SpecLimitParser.cs` — `Compare`, `IsExceededBy`. Structured specifications (`SpecificationEvaluator.Evaluate`) use per-row inclusive flags for lower and upper limits for Range, NotMoreThan and NotLessThan (an exclusive limit makes a value equal to the limit out of specification), always include both ends for TargetWithTolerance, and never return Alert or Action statuses except through CountTiered, which calls the text comparison. Location: `backend/MicroLIMS.Application/Services/SpecificationEvaluator.cs` — `Evaluate`. `WaterWorkflowEngine`, `SpecificationService` and `PathogenSessionService` each wrap the text comparison.
- **What the other source says:** `documents/Conflicts.md` CONF-006 records that limit comparison is implemented in several places. The plan `docs/superpowers/plans/2026-11-11-specification-set.md` (cross-plan corrections, item 1) lists `SpecLimitParser`, `SpecificationService`, `PathogenSessionService` and `SpecificationEvaluator`. No approved specification line was found stating whether a value equal to a limit conforms.
- **Question:** Is a value equal to a limit conforming for count limits? Should one place hold the comparison? Locations found for CONF-006: `SpecLimitParser.Compare` (also called by `IncubationStepRecorder.Compare`), `SpecificationService.CompareAgainstLimits` (line 682), `PathogenSessionService.CompareAgainstLimits` (line 1217), `SpecificationEvaluator.Evaluate`.
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-013
- **Area:** MIC
- **Status:** Open
- **What the code does:** The checks on a media lot differ by path. Media selection and the per-order selective plating and confirmatory setup require the lot flag `IsReleasedForUse` and a status other than OutOfStock or QuarantineFailed, and do not check the expiry date or the Expired and Destroyed statuses. Location: `backend/MicroLIMS.Application/Workflows/TestWorkflow/IncubationStepRecorder.cs` — `SelectMediaCoreAsync`; `backend/MicroLIMS.Application/Workflows/TestWorkflow/PathogenConfirmationRecorder.cs` — `LoadReleasedLotAsync`. The shared enrichment start checks the expiry date and does not check the release flag or status. Location: `backend/MicroLIMS.Application/Services/PathogenSessionService.cs` — `StartSharedTsbCoreAsync`. The session confirmatory setup checks the lot's section only, and accepts no lot at all (media ID 0). Location: same file — `StartSharedConfirmatorySetupAsync`, `ResolveChosenConfirmatoryMediaAsync`.
- **What the other source says:** `CLAUDE.md` line 102 (Rule 3.2, Media and Reference Strain Traceability) says media used in testing must have passed Growth Promotion Testing and sterility checks, be within expiry, and be linked by lot number to the test execution.
- **Question:** Which checks must every path apply to a media lot: release, rejection, expiry date, statuses Expired and Destroyed? Must a lot be named for the session confirmatory setup?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-014
- **Area:** PAT
- **Status:** Open
- **What the code does:** The incubation start time for the shared enrichment, the session confirmatory setup, selective plating and the per-order confirmatory setup is taken from the request when the request gives one, with no limit on how far in the past or future. The end of the window is start plus the configured maximum hours, so a past start makes the incubation count as finished at once. Location: `backend/MicroLIMS.Application/Services/PathogenSessionService.cs` — `StartSharedTsbCoreAsync`, `StartSharedConfirmatorySetupAsync`; `backend/MicroLIMS.Application/Workflows/TestWorkflow/PathogenConfirmationRecorder.cs` — `StartSelectivePlatingIncubationCoreAsync`, `SubmitConfirmatorySetupCoreAsync`. Media selection for count steps uses the server clock instead. Location: `backend/MicroLIMS.Application/Workflows/TestWorkflow/IncubationStepRecorder.cs` — `SelectMediaCoreAsync`.
- **What the other source says:** `CLAUDE.md` line 38 (Principle 2) says every consequential input from the frontend must be re-validated server-side. A code comment in `TestWorkflowSupport.RequireValidIncubationWindow` says under-incubation is the falsification risk.
- **Question:** May an analyst enter an incubation start time that differs from the time of entry, and if so within what limit and with what reason recorded?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-015
- **Area:** PAT
- **Status:** Open
- **What the code does:** The pathogen chain is read from the configured workflow steps of each test (step types BrothEnrichment, SelectiveBroth, SelectivePlating, ConfirmatoryPlating, BiochemicalTest). Broth steps carry no result. Selective plating with no growth or non-conforming growth ends the test as NotDetected; conforming growth continues. There is no early close at a broth step. The class `PathogenWorkflowEngine` does not exist; `TestWorkflowEngine` states in its header comment that it replaced it. Location: `backend/MicroLIMS.Application/Workflows/TestWorkflowEngine.cs` — class header; `backend/MicroLIMS.Application/Workflows/TestWorkflow/PathogenConfirmationRecorder.cs`.
- **What the other source says:** `documents/DesignSpecification/BusinessRules.md` lines 52 to 82 (section 3, "Pathogen Engine") name `Application/Workflows/PathogenWorkflowEngine.cs`, describe a universal chain that completes after one observation, describe an organism-specific chain with fixed steps in fixed order, say a negative result at the first two steps closes the chain early as Absent, and say the final result is Detected only when all three steps are positive. The code has no fixed chain and no early close at broth steps.
- **Question:** Is section 3 of `BusinessRules.md` to be rewritten to match the configured-step engine, or does the code have to change? Which pathogen chain rules are approved?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-016
- **Area:** PAT
- **Status:** Open
- **What the code does:** Two paths decide a pathogen result with different rules. Session path: a primary observation of GrowthNonConforming or GrowthConforming goes to confirmation; after confirmation all-conforming gives Detected, all NoGrowth or all non-conforming gives NotDetected, any mix gives Inconclusive. Location: `backend/MicroLIMS.Application/Services/PathogenSessionService.cs` — `SavePrimaryObservationsAsync`, `ApplyConfirmationAgreement`; `backend/MicroLIMS.Application/Services/ConfirmationAgreementEvaluator.cs` — `EvaluateAgreement`. Per-order path: selective plating GrowthNonConforming ends the test as NotDetected with no confirmation; confirmation gives AllConforming only when every medium is conforming, otherwise Inconclusive, so all-NoGrowth confirmation is Inconclusive; "Inconclusive" still allows the biochemical step. Location: `backend/MicroLIMS.Application/Workflows/TestWorkflow/PathogenConfirmationRecorder.cs` — `SubmitSelectivePlatingObservationCoreAsync`, `SubmitConfirmatoryObservationsAsync`, `SubmitBiochemicalCoreAsync`.
- **What the other source says:** `documents/DesignSpecification/BusinessRules.md` lines 52 to 82 describe a single chain with Detected or Absent as the outcome. `CLAUDE.md` line 99 (Rule 3.2) says all pathogens run through the same generic configuration-driven workflow.
- **Question:** Which of the two rule sets is correct? Should non-conforming growth at the primary stage go to confirmation or end the test? Should an all-no-growth confirmation be NotDetected or Inconclusive?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-017
- **Area:** PAT
- **Status:** Open
- **What the code does:** When confirmatory plates disagree, a mix of NoGrowth on one medium and GrowthNonConforming on another is called Inconclusive ("Retest"), while all NoGrowth or all GrowthNonConforming is called NotDetected. Location: `backend/MicroLIMS.Application/Services/ConfirmationAgreementEvaluator.cs` — `EvaluateAgreement`.
- **What the other source says:** No approved specification in `documents/` was searched and found for this rule (searched `documents/DesignSpecification/BusinessRules.md`). `CLAUDE.md` line 97 (Rule 3.2) says an ambiguous scientific rule must be put to the user before coding.
- **Question:** Should a mix of no growth and non-conforming growth be Inconclusive, or NotDetected because neither matches the target organism?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-018
- **Area:** PAT
- **Status:** Open
- **What the code does:** Recorded data is deleted or overwritten in place in the pathogen session. (1) Reset permanently deletes observations, step results, count readings, results and incubations of all the sample's test orders and resets the orders to Waiting, for any sample that is not Voided or Rejected, including an Approved sample, with the reason optional and no signature. Location: `backend/MicroLIMS.Application/Services/PathogenSessionService.cs` — `ResetSessionAsync`. (2) Starting the shared enrichment deletes existing incubations whose step name contains "TSB" or "Enrichment". Location: same file — `StartSharedTsbCoreAsync`. (3) Primary observations, confirmatory plate readings and location results are overwritten in place by later saves, and result entry stays allowed while a test is at Ready (pending review). Location: same file — `UpsertPrimaryObservation`, `SaveBatchConfirmatoryPlateReadingsAsync`, `SaveResultMatrixAsync`; `backend/MicroLIMS.Application/Services/WorkflowStateResolver.cs` — `Resolve`. Whether the automatic audit capture of the database context keeps the old values was not checked in this task.
- **What the other source says:** `CLAUDE.md` lines 92 to 94 (Rule 3.1) say corrections, invalidations and retests must append new linked records and preserve the original recorded values, attribution and reason. `CLAUDE.md` line 109 (Rule 3.3 item 2) requires reason and signature linkage where the workflow requires it.
- **Question:** May a reset delete recorded results, and for which sample statuses? Should overwritten observations be kept as versions?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-019
- **Area:** PAT
- **Status:** Open
- **What the code does:** For a quantitative cell in the result matrix, the reported result text is taken from the display text sent by the client, and the numeric value is stored as sent. The server does not round it, does not check that it is a whole number or not negative, and does not build the text. Location: `backend/MicroLIMS.Application/Services/PathogenSessionService.cs` — `SaveResultMatrixAsync`. The qualitative cell is read from the literal result code "DETECTED" or display text containing "Detected (+)".
- **What the other source says:** `CLAUDE.md` line 38 (Principle 2) says the backend is the sole authority for CFU calculation, rounding and precision, and `CLAUDE.md` line 99 (Rule 3.2) says to enforce whole-number colony counts in the backend.
- **Question:** Should the backend build the reported text and validate the counts in the session matrix, as it does for single plate counts?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-020
- **Area:** PAT
- **Status:** Open
- **What the code does:** The shared enrichment panel sets the sterility status to the fixed text "Passed" whatever the lot's records say, and sets the GPT status to "Passed (GPT Conform)" only from the release flag. Location: `backend/MicroLIMS.Application/Services/PathogenSession/SharedTsbStateBuilder.cs` — `Build`.
- **What the other source says:** `CLAUDE.md` line 102 (Rule 3.2) requires media to have passed GPT and sterility checks. `CLAUDE.md` line 216 (section 7, item 3, "No Mock Business Data in Production Code") says never to hard-code stubbed values to make a feature look functional.
- **Question:** Where should the sterility status come from, and should the panel show it only from a recorded sterility check?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-021
- **Area:** REV
- **Status:** Open
- **What the code does:** The segregation-of-duties guard that review and approval use does not read the user on `LocationPathogenObservation` rows, `SampleLocation.EnteredByUserId`, or the starter and closer of an incubation. The pathogen session saves primary observations and location results on those rows. `CompleteSessionAsync` writes no `Result` row, so a user who only entered session results and is not the assigned analyst, did not prepare the sample and did not start the enrichment may pass the guard. Confirmatory plate observations saved through the session are linked to a primary observation, not to a workflow step result, and the guard reads only the latter link. Location: `backend/MicroLIMS.Application/Services/SegregationOfDutiesGuard.cs` — `DidUserPerformTestAsync`; `backend/MicroLIMS.Application/Services/PathogenSessionService.cs` — `SavePrimaryObservationsAsync`, `SaveBatchConfirmatoryPlateReadingsAsync`, `CompleteSessionAsync`. The session has no segregation check of its own.
- **What the other source says:** `CLAUDE.md` line 44 (Principle 3) says an actor who performs or records a step cannot review or approve their own work. The guard's own comment says "performed" includes entering or observing any raw result.
- **Question:** Should the guard also read observer and entered-by users on session records and incubations?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-022
- **Area:** REV
- **Status:** Open
- **What the code does:** Review and approval are done per laboratory section of a sample, in `SampleReviewService.CompleteReviewCoreAsync` and `SampleApprovalService.DecideCoreAsync` (`backend/MicroLIMS.Application/Services/`). A section is reviewed when all its current tests are at step Ready; approval needs the section at UnderApproval. Only four decisions are accepted (Approve, Reject, RetestRetainedSample, NewSampleRequest); Investigation and OOSInvestigation are refused. No decision requires a comment. There is no quick table review method (searched for `QuickReviewBatch`; no hit in `backend/`). The files `ReviewService.cs` and `ApprovalService.cs` do not exist.
- **What the other source says:** `documents/DesignSpecification/BusinessRules.md` lines 157 to 185 (sections 8 and 9) name `Application/Services/ReviewService.cs` and `ApprovalService.cs`, say a test order is reviewed one at a time or in a quick batch (`QuickReviewBatchAsync`) that skips ineligible orders, say a test order cannot be decided before status Reviewed, list six decisions, and say Investigation and OOSInvestigation need a non-blank comment enforced server-side.
- **Question:** Is the business rules text to be changed to the per-section model, or must the code change (for example to keep the comment rule for investigation decisions)?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-023
- **Area:** REV
- **Status:** Open
- **What the code does:** Approval does not look at result statuses: a section whose results are OutOfSpecification, ActionLimitExceeded, RequiresReview or Inconclusive can be reviewed and approved. Only the system suitability and analysis-entry checks block approval. Location: `backend/MicroLIMS.Application/Services/SampleApprovalService.cs` — `DecideCoreAsync`; `backend/MicroLIMS.Application/Services/SampleReviewService.cs` — `CompleteReviewCoreAsync`. When a retest outcome is carried to the original sample, the order is classed as conforming when no result of the kinds read is found, and test analyses and parameter results are not read. Location: `backend/MicroLIMS.Application/Services/SampleApprovalService.cs` — `DetermineOwnResultConformanceAsync`. This is the private conformance rule in CONF-008.
- **What the other source says:** `documents/Conflicts.md` CONF-008 records that conformance is decided separately in `SampleApprovalService` and in `ReportDocumentMapper` (the mapper was not read in this task). No approved specification line was found on whether an out-of-specification result may be approved.
- **Question:** May a Section Head approve a section that holds out-of-specification results, and if so, must it be linked to an investigation? Should unrecorded results count as conforming?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-024
- **Area:** REV
- **Status:** Open
- **What the code does:** Returning a test to the analyst re-opens the latest incubation row of a count test by clearing its completed time, user and outcome, so the original completion record is changed in place. The reason is optional, no password or signature is asked for, and no check is made that the reviewer did not perform the test. The sample ID in the route is not compared with the order's sample. Observation-type (pathogen) tests cannot be returned. Location: `backend/MicroLIMS.Application/Services/SampleReviewService.cs` — `ReturnOrderAsync`, `ReturnToAnalystCoreAsync`; `backend/MicroLIMS.API/Controllers/SampleReviewController.cs` — `ReturnTestToAnalyst`.
- **What the other source says:** `CLAUDE.md` lines 92 to 94 (Rule 3.1) say corrections and retests append new linked records and preserve original values and the reason. `CLAUDE.md` line 44 (Principle 3) covers segregation of duties across the lifecycle.
- **Question:** Should the return keep the original incubation record and open a new one? Should a reason be required?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-025
- **Area:** REV
- **Status:** Open
- **What the code does:** The methods for incubation, count recording, pathogen session saves, review and approval do not call `RecordVersion.EnsureCurrent` and the controllers do not read `If-Match`. `RecordVersionMiddleware` only copies the header to the context. Transactions (`UnitOfWork.RunAsync`) are used by most of these methods but not by `StartStage2IncubationAsync`, `CloseCurrentIncubationWindowAsync`, `OverrideMinimumDurationAsync`, `SubmitConfirmatoryObservationsAsync` and the pathogen session methods other than the shared enrichment start. Whether a second concurrent request is stopped by a database constraint was not checked. Location: `backend/MicroLIMS.Application/Services/SampleReviewService.cs`, `backend/MicroLIMS.Application/Services/SampleApprovalService.cs`, `backend/MicroLIMS.Application/Services/PathogenSessionService.cs`, `backend/MicroLIMS.Application/Workflows/TestWorkflow/IncubationStepRecorder.cs`; `backend/MicroLIMS.API/Middleware/RecordVersionMiddleware.cs` — `InvokeAsync`. The `Sample` entity has a `Version` field; `RecordVersion.EnsureCurrent` is called for it in `SampleCorrectionService` only.
- **What the other source says:** `CLAUDE.md` line 177 (section 5, item 3) says critical state transitions (result entry, review, approval) must enforce optimistic concurrency so two users cannot silently overwrite each other or create duplicate approvals.
- **Note (orchestrator check, 2026-11-12):** Entities with a version column carry an automatic `xmin` concurrency token (`backend/MicroLIMS.Persistence/DbContext/MicroLimsDbContext.cs`, about line 296), so two saves of the same row that overlap in time are refused by the database layer. What is missing in these methods is the stale-form check: a save based on a screen loaded before someone else's change is not refused. Read only, not run.
- **Question:** Is a version check required on review, approval and result entry, and where is the duplicate approval protection meant to come from?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-026
- **Area:** MIC
- **Status:** Open
- **What the code does:** When the sample preparation gate refuses a test start, the code saves a `TestStartRefused` audit row and a workflow-history row and then throws. The callers (`SelectMediaAsync`, `StartSelectivePlatingIncubationAsync`, `StartSharedTsbAsync`) run inside `UnitOfWork.RunAsync`, which on an exception rolls the transaction back and re-writes only refused electronic-signature audit records. By reading, the refusal rows are rolled back on a relational database. This was not run. Location: `backend/MicroLIMS.Application/Workflows/TestWorkflow/TestWorkflowSupport.cs` — `RequireSamplePreparedAsync`; `backend/MicroLIMS.Application/Services/PathogenSessionService.cs` — `EnsurePreparedAsync`; `backend/MicroLIMS.Application/Helpers/UnitOfWork.cs` — `RunAsync`.
- **What the other source says:** The code comment in `RequireSamplePreparedAsync` says the refusal is logged contemporaneously to the audit log and workflow history for GMP data integrity.
- **Question:** Should refusals survive the rollback? Is there a test that proves they do (check by running the PostgreSQL tests)?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-027
- **Area:** MIC
- **Status:** Open
- **What the code does:** The unit shown with a recorded count in the pathogen session is read from the current item specification (or sampling configuration) when the session is displayed, not from the stored reading. The reported text saved at recording time holds the unit as it was then, so the two can differ after the specification is edited. The limits and dilution factor are stored on the reading. Location: `backend/MicroLIMS.Application/Services/PathogenSessionService.cs` — `BuildCountResultAsync`; `backend/MicroLIMS.Application/Workflows/TestWorkflow/IncubationStepRecorder.cs` — `RecordCountTestAsync`.
- **What the other source says:** `CLAUDE.md` lines 92 to 94 (Rule 3.1) say historical results stay bound to the units, precision and acceptance criteria active when the test was performed.
- **Question:** Should the unit be stored on the reading at recording time?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-028
- **Area:** PAT
- **Status:** Open
- **What the code does:** Behaviour is chosen by matching names in several places instead of configuration: step names containing "TSB" or "Enrichment" or equal to "Broth Enrichment" (removal and creation of incubations and step results), the literal result code "DETECTED", and the cause of testing named "Retest". Location: `backend/MicroLIMS.Application/Services/PathogenSessionService.cs` — `StartSharedTsbCoreAsync`, `SaveResultMatrixAsync`; `backend/MicroLIMS.Application/Services/WorkflowStateResolver.cs` — `Resolve`; `backend/MicroLIMS.Application/Workflows/TestWorkflow/PathogenConfirmationRecorder.cs` — `SubmitBrothCoreAsync`; `backend/MicroLIMS.Application/Services/SampleApprovalService.cs` — `DecideCoreAsync` (cause "Retest"). No organism name is matched in the files read.
- **What the other source says:** `CLAUDE.md` line 36 (Frozen Principle 1) and line 99 (Rule 3.2) say behaviour comes from configuration and the engine does not branch on names.
- **Question:** Should these name matches be replaced by configured step types and flags?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-029
- **Area:** MIC
- **Status:** Open
- **What the code does:** The plate-count request is not validated for content. Readings may be any decimal, including negative and fractional values; a dilution factor of zero or below is accepted; a reading that is neither a number nor TNTC or Uncountable throws a parse exception that the controller does not catch. A mix of numeric readings and TNTC records the whole result as needing review. Location: `backend/MicroLIMS.Application/Workflows/TestWorkflow/IncubationStepRecorder.cs` — `RecordCountTestAsync`; `backend/MicroLIMS.API/Controllers/TestWorkflowController.cs` — `RecordResult`. The average is the arithmetic mean of all plate readings with no countable-range test.
- **What the other source says:** `CLAUDE.md` line 99 (Rule 3.2) says to enforce whole-number colony counts and apply the configured countable-range, averaging and rounding rules in the backend. No approved specification line on the countable range was found in `documents/`.
- **Question:** What are the approved rules for a valid plate reading, a countable range per plate, and mixed numeric and TNTC readings?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-030
- **Area:** SIG
- **Status:** Open
- **What the code does:** A signature is tied to its record only by the entity type text and the entity ID number. The signature row holds no record version, no version of the specification, and no hash or copy of the signed content. Location: `backend/MicroLIMS.Domain/Entities/ElectronicSignature.cs` — `ElectronicSignature` (properties `EntityType`, `EntityId`); `backend/MicroLIMS.Application/Services/ElectronicSignatureService.cs` — `SignAsync`. Callers sign before they change the record (for example `SystemSettingsService.UpdateCoreAsync` signs with the setting row ID), so the signature points at the record, not at the state that was signed.
- **What the other source says:** CLAUDE.md line 115 (Rule 3.3, item 5): the signature must be permanently bound to the exact signed record version so it cannot be copied or transferred to a later version.
- **Question:** Is binding by entity type and ID enough for QA, or must each signature also carry the version or a content hash of the record it signs?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-031
- **Area:** SIG
- **Status:** Open
- **What the code does:** The signing time (`SignedAt`) and the time of the failed-attempt and throttled rows are not set by the signature service from the injected clock. `SignedAt` takes the entity default `DateTime.UtcNow`; the two `AuditLog` rows made in `SignAsync` set no `Timestamp`, so they take the `AuditLog` default `DateTime.UtcNow`. The throttle window start in the same method is taken from the injected clock (`_time`). Location: `backend/MicroLIMS.Domain/Entities/ElectronicSignature.cs` — `SignedAt`; `backend/MicroLIMS.Domain/Entities/AuditLog.cs` — `Timestamp`; `backend/MicroLIMS.Application/Services/ElectronicSignatureService.cs` — `SignAsync`.
- **What the other source says:** CLAUDE.md line 109 (Rule 3.3, item 2) and line 194 (Rule 5.6): audit and regulated timestamps come from the injected clock; the legacy entity defaults are allowed only as a capped legacy item and new ones must not be added.
- **Question:** Should the signature service set `SignedAt` and the failed-attempt row times from the injected clock, so that the throttle window and the stored times use one clock?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-032
- **Area:** AUD
- **Status:** Open
- **What the code does:** The automatic audit rows do not set `CorrelationId`. `BuildAuditLog` fills user, time, reason and the search columns but not the correlation number. Hand-made audit rows in `UserService`, `RoleService`, `AdminPasswordRecoveryService` and `SystemSettingsService` also leave it empty. Only `AuditEventService` takes a correlation number, and only if the caller passes one. The request correlation text made by `CorrelationIdMiddleware` is a different value, and its comment says it is unrelated to `AuditLog.CorrelationId`. Location: `backend/MicroLIMS.Persistence/DbContext/MicroLimsDbContext.cs` — `BuildAuditLog`; `backend/MicroLIMS.Application/Services/AuditEventService.cs` — `RecordUserEventAsync`; `backend/MicroLIMS.API/Middleware/CorrelationIdMiddleware.cs`.
- **What the other source says:** CLAUDE.md line 109 (Rule 3.3, item 2): every regulated mutation must record a `CorrelationId`.
- **Question:** Should automatic capture fill `CorrelationId` for every audit row, and from which value (the request correlation text, or a new Guid per save)?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-033
- **Area:** AUD
- **Status:** Open
- **What the code does:** When automatic capture runs with no user, no agent code and no system process name, the audit row records `UserId` 0 and no actor type. Location: `backend/MicroLIMS.Persistence/DbContext/MicroLimsDbContext.cs` — `BuildAuditLog` (`UserId = bySystem ? null : CurrentUserId ?? 0`). The search screen shows such rows as "System" when the user ID is null or 0 (`AuditSearchService.ToDtosAsync`), so an unattributed change and a system change look the same. Several anonymous flows reach this path, for example the password reset confirm (`AuthenticationService.ConfirmPasswordResetAsync`).
- **What the other source says:** CLAUDE.md line 109 (Rule 3.3, item 2): every regulated mutation must record an attributable user or an explicit agent or system identity.
- **Question:** Should a row written with no known actor carry an explicit marker (for example actor type System with a process name, or a distinct "anonymous" actor) instead of user 0?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-034
- **Area:** AUD
- **Status:** Open
- **What the code does:** The append-only database triggers cover `AuditLogs`, `AuditEventChanges`, `ElectronicSignatures`, `LoginHistories`, `SecurityAuditEvents`, `SystemSettingVersions`, `AccessReviews` and others listed in the catalogue test. `WorkflowHistories` is not in that list and no trigger for it was found in the migrations. Location: `backend/MicroLIMS.Tests/IntegrationTests/AppendOnlyTriggerCatalogPostgresTests.cs` — `ProtectedTables`; `backend/MicroLIMS.Persistence/DbContext/MicroLimsDbContext.cs` — `WorkflowHistories`.
- **What the other source says:** `documents/DesignSpecification/BusinessRules.md` lines 206-218 (section 11, Audit Trail): nothing is ever hard-deleted from the audit log and the workflow history table; treat them as append-only.
- **Question:** Is the workflow history table meant to be protected at database level like the audit log, or is "treat as append-only" a rule for code only?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-035
- **Area:** AUD
- **Status:** Open
- **What the code does:** Reading the audit trail, the traceability graph, the CSV export and the signature trail needs only the permission (`Audit.View`, `Signatures.Manage`). No method checks the laboratory section of the record. In the first-run seed the System Administrator and the Section Head hold `Audit.View` and `Signatures.Manage`. Location: `backend/MicroLIMS.Application/Services/AuditSearchService.cs` — `ApplyFilters`, `GetForEntityAsync`; `backend/MicroLIMS.Application/Services/AuditExportService.cs` — `PrepareAsync`; `backend/MicroLIMS.Application/Services/AuditTraceabilityService.cs` — `GetTraceabilityForEntityAsync`; `backend/MicroLIMS.Application/Services/ElectronicSignatureService.cs` — `GetTrailAsync`.
- **What the other source says:** CLAUDE.md line 175 (Rule 5.2, anti-IDOR): services must also check resource and laboratory-section scope before reading or changing a record by ID.
- **Question:** Should audit and signature reads be limited to the sections of the user (with `Sections.ViewAll` as the wider right), or is a global read right intended for these two permissions?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-036
- **Area:** SEC
- **Status:** Open
- **What the code does:** The failed-login counter is not reset when a lock expires. After the lock time passes, the counter is still at or above the limit, so the next single wrong password raises it again and sets a new lock. It is reset by a successful sign-in, by an administrator unlock and by a completed administrator recovery. The password-reset confirm (the forgotten-password flow) does not reset it and does not clear a lock. Location: `backend/MicroLIMS.Application/Services/AuthenticationService.cs` — `LoginCoreAsync`, `ConfirmPasswordResetAsync`; `backend/MicroLIMS.Application/Services/AdminPasswordRecoveryService.cs` — `ConfirmRecoveryAsync`.
- **What the other source says:** `documents/DesignSpecification/BusinessRules.md` lines 192-194 (section 10): five failed login attempts lock the account for 15 minutes. It does not say what happens to the count when the lock ends.
- **Question:** After a lock ends, should the next failure start a new count from zero, or lock again at once? Should a completed password reset clear the lock?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-037
- **Area:** SEC
- **Status:** Open
- **What the code does:** The security values are settings that an administrator can change, with defaults in the code: lockout attempts (default 5, range 3 to 20), lock time (15, 1 to 1440 minutes), refresh token life (7, 1 to 30 days), reset token life (60, 5 to 1440 minutes), password history (5, 1 to 24) and minimum length (8, 8 to 64). The constants in `AuthenticationService` (`MaxFailedAttempts`, `LockDuration`, `RefreshTokenLifetime`, `PasswordResetTokenLifetime`) are not read by the methods. Also, `RequestPasswordResetAsync` throws a business-rule error for an unknown user name and the controller hides it; it does not "always return a generic success message" itself. Location: `backend/MicroLIMS.Application/Services/SystemSettings/SystemSettingDefinitions.cs` — `All`; `backend/MicroLIMS.Application/Services/AuthenticationService.cs` — `LoginCoreAsync`, `RequestPasswordResetAsync`; `backend/MicroLIMS.API/Controllers/AuthenticationController.cs` — `RequestPasswordReset`.
- **What the other source says:** `documents/DesignSpecification/BusinessRules.md` lines 192-200 (section 10) state the values as fixed (5 attempts, 15 minutes, 7 days, 1 hour) and say `RequestPasswordResetAsync` always returns a generic success message.
- **Question:** Is the business rule the default value or the configured value? Should BusinessRules.md be updated to say the values are settings?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-038
- **Area:** SEC
- **Status:** Open
- **What the code does:** Two code paths set a new password with different rules. The normal path (`ValidateAndApplyNewPasswordAsync`) reads the history size from the setting `security.password.historyLimit`, removes history rows beyond the limit, and writes the security event `AUTH_SESSION_REVOKED`. The administrator recovery path checks the last 5 password hashes with a fixed number, does not remove old history rows, and writes no security event. The recovery path also uses a fixed expiry of 15 minutes and a fixed limit of 5 wrong codes. Location: `backend/MicroLIMS.Application/Services/AuthenticationService.cs` — `ValidateAndApplyNewPasswordAsync`; `backend/MicroLIMS.Application/Services/AdminPasswordRecoveryService.cs` — `ConfirmRecoveryAsync`, `CreateRecoveryRequestAsync`.
- **What the other source says:** The settings description in `backend/MicroLIMS.Application/Services/SystemSettings/SystemSettingDefinitions.cs` — `All` says the history setting is "how many of a user's last passwords cannot be reused" and has no exception for recovery.
- **Question:** Should recovery follow the history setting and write security events like the normal path, or is the fixed behaviour intended?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-039
- **Area:** SEC
- **Status:** Open
- **What the code does:** The recovery confirm endpoint is anonymous. Its code comment says an unknown user name, a disabled account and a wrong code "must all look the same to the caller". It gives "Invalid or expired recovery code." for unknown user, disabled user and no pending request, but it gives "Invalid recovery code." for a wrong code (when a pending request exists) and a different text for an expired request ("Recovery code has expired…") and for the fifth wrong try. A caller can therefore tell that the user name exists and has a pending recovery. Location: `backend/MicroLIMS.Application/Services/AdminPasswordRecoveryService.cs` — `ConfirmRecoveryAsync` (comment at `InvalidRecoveryCodeMessage`).
- **What the other source says:** The same method's comment (above) and `documents/DesignSpecification/BusinessRules.md` lines 197-200 (the API never reveals whether a user name exists; stated there for the reset request).
- **Question:** Should every failure of the recovery confirm return the same message?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-040
- **Area:** ADM
- **Status:** Open
- **What the code does:** The System Administrator built-in grants include `MasterData.Manage`, `Items.Manage`, `KpiConfigureProfiles`, `DocumentsConfigManage`, `Sections.ConfigureAll` and `System.ConfigureSettings`; the Section Head seed grants include `MasterData.Manage`, `Items.Manage` and `KpiConfigureProfiles`. The segregation-of-duties rule enforced by the role screen only refuses a role that holds `Users.Manage` or `Roles.Manage` together with a code in a fixed list of laboratory execution and approval codes; configuration codes are not in either list. The code comment says administrators control "users, roles, configuration, master data". Location: `backend/MicroLIMS.Shared/Constants/PermissionConstants.cs` — `SystemAdministratorGrants`; `backend/MicroLIMS.Persistence/Seed/DbSeeder.cs` — role grants for `RoleType.SectionHead`; `backend/MicroLIMS.Shared/Constants/SegregationOfDutiesRules.cs` — `AdministrationCodes`, `GxpExecutionAndApprovalCodes`, `FindConflicts`; `backend/MicroLIMS.Application/Services/RoleService.cs` — `UpdatePermissionsCoreAsync`.
- **What the other source says:** CLAUDE.md line 36 (Principle 1): laboratory configuration is created by the Section Head. CLAUDE.md lines 44-46 (Principle 3): System Administration and Laboratory Administration (Section Head) remain strictly separated.
- **Question:** Should master data, item and workflow configuration be a Section Head right only, with the System Administrator limited to users, roles and system settings? If not, which of these codes may both roles hold?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-060
- **Area:** RCV
- **Status:** Open
- **What the code does:** The reference number sequence is the count of samples of the same category received in the laboratory month, plus 1. The count and the save are separate and no lock is taken. A unique index on the reference number makes the second of two simultaneous receipts fail at save; nothing retries in the item-based receipt. The water receipt does retry once with a new number, then refuses with nothing saved. Location: `backend/MicroLIMS.Application/Services/ReferenceNumberGenerator.cs` — `GenerateAsync`; used by `backend/MicroLIMS.Application/Workflows/ProductWorkflowEngine.cs` — `ReceiveAsync`; retry in `backend/MicroLIMS.Application/Workflows/WaterWorkflowEngine.cs` — `ReceiveAsync`. Index: `backend/MicroLIMS.Persistence/Configurations/SampleConfiguration.cs`. This is the code location for CONF-004.
- **What the other source says:** `documents/Conflicts.md` CONF-004 asks how numbers are to stay unique under concurrent receipt. The XML comment on the class says the sequence "resets monthly". No approved line in `documents/` giving the number format was searched for beyond `BusinessRules.md` lines 33 to 51, which do not mention it.
- **Question:** Is a failed second receipt (database error, user must retry) acceptable, or must the number be allocated safely? Should voided samples keep their number in the count (they do now)?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-061
- **Area:** RCV
- **Status:** Open
- **What the code does:** Test orders are created in more places than receipt: `ProductWorkflowEngine.ReceiveAsync` (receipt), `SampleTestSelectionService.SelectCoreAsync` (periodic retest), `AddLaboratoryService.AddCoreAsync` (added laboratory) and `SampleCorrectionService` (rebuild after an item change or a grade change). Location: `backend/MicroLIMS.Application/Workflows/ProductWorkflowEngine.cs`, `backend/MicroLIMS.Application/Services/SampleTestSelectionService.cs`, `backend/MicroLIMS.Application/Services/AddLaboratoryService.cs`, `backend/MicroLIMS.Application/Services/SampleCorrectionService.cs`.
- **What the other source says:** `documents/DesignSpecification/BusinessRules.md` lines 44 to 49 say no user at any role can create a TestOrder by hand and the only entry points are "the five `ReceiveAsync` methods on the workflow engines". The code has four engine `ReceiveAsync` methods (Product, Water, EM, AfterCleaning). Water, EM and After Cleaning create their orders in `PrepareAsync`, not in `ReceiveAsync` (see FS-WAT, FS-ENV, FS-ACL).
- **Question:** Should the business rule be reworded to list all the places that create test orders, or should some of them be removed?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-062
- **Area:** RCV
- **Status:** Open
- **What the code does:** Adding a laboratory to a sample is allowed to any holder of `Samples.Receive`, with no laboratory-membership check and no sample-scope check. Correct, void and assign-analyst check scope. Location: `backend/MicroLIMS.API/Controllers/SampleController.cs` — `AddLaboratory`; `backend/MicroLIMS.Application/Services/AddLaboratoryService.cs` — `AddCoreAsync`.
- **What the other source says:** `CLAUDE.md` section 5, item 2 (Anti-IDOR) says backend services must verify resource-level and laboratory-section scope before mutating a record by ID. `Samples.Receive` is described in `ReceiptLabGuard` as the permission that may target any active laboratory, which may be the intended scope.
- **Question:** Is "any laboratory, any sample" the intended scope for `Samples.Receive`, or must the sample-scope check be added?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-063
- **Area:** RCV
- **Status:** Open
- **What the code does:** Voiding a sample needs `Samples.Correct`, a reason and a password. It refuses only a sample that is already Voided. A sample that is InTesting, in review or Approved can be voided. No second person is involved and there is no stale-form version check. Location: `backend/MicroLIMS.Application/Services/SampleCorrectionService.cs` — `VoidCoreAsync`.
- **What the other source says:** Correction of the same sample is refused once the sample is submitted for review (`SampleCorrectionService.IsCorrectable`), with the comment "review signs the data as it stands". `CLAUDE.md` Principle 3 says an actor cannot review or approve their own work across the record lifecycle. No line in `documents/` was found on voiding an approved sample.
- **Question:** May a sample that is submitted for review or Approved be voided by one person? If yes, should a second signer be required?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-064
- **Area:** WAT
- **Status:** Open
- **What the code does:** The water limit comparison is `SpecLimitParser.Compare` (specification, then action, then alert; first one exceeded wins) and can return the status LimitsNotConfigured as well as WithinLimits, AlertLimitExceeded, ActionLimitExceeded and OutOfSpecification. The single-point calculate path reads the limits for the sample's `WaterSamplingPointId`, and no code in Application or API sets that field; every prepared water test has sample locations, which that path refuses. The interface comment in the same file says "Alert -> Action -> Specification, in that order of severity". Location: `backend/MicroLIMS.Application/Workflows/WaterWorkflowEngine.cs` — `CalculateAndCompareAsync`, `Compare`; `backend/MicroLIMS.Application/Services/SpecLimitParser.cs` — `Compare`.
- **What the other source says:** `documents/DesignSpecification/BusinessRules.md` lines 83 to 98 describe the water engine as entering readings, averaging, and comparing "Specification, Action, Alert" with four status values (no LimitsNotConfigured), and say limits are read from the sampling configuration by test code and the sample's sampling point.
- **Question:** Is the single-point calculate path still wanted? Should the business rules file describe the per-location batch entry, and should LimitsNotConfigured be listed?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-065
- **Area:** WAT
- **Status:** Open
- **What the code does:** The daily water report reads the average and status back from the text field `InterpretedValue` by splitting on brackets. The batch method writes "n locations: x conform, y alert/action/spec (worst: status)" in that field, so the parsed average is 0 and the status text is "worst: status". The date match uses the stored date part without conversion to the laboratory time zone. All matching rows are loaded without paging. Location: `backend/MicroLIMS.Application/Workflows/WaterWorkflowEngine.cs` — `GetDailyAggregateAsync`, `ParseAggregateResult`; text written by `backend/MicroLIMS.Application/Workflows/TestWorkflow/IncubationStepRecorder.cs` — `RecordWaterBatchReadingsCoreAsync`.
- **What the other source says:** No line in `documents/` describes the daily report. `CLAUDE.md` section 5.4 says read queries must project in SQL and section 5.6 says to use the injected clock.
- **Question:** What should the daily report show for batch results, and should it read the stored numeric values instead of the display text?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-066
- **Area:** WAT
- **Status:** Open
- **What the code does:** Storage condition is checked differently in two places. Preparation accepts any non-blank text and needs a storage time only for exactly "Refrigerator" (negative time not refused). Correction accepts only "Refrigerator" or "RoomTemperature" and refuses a negative time. Location: `backend/MicroLIMS.Application/Workflows/WaterWorkflowEngine.cs` — `PrepareCoreAsync`; `backend/MicroLIMS.Application/Services/SampleCorrectionService.cs` — `CorrectCoreAsync`.
- **What the other source says:** No line in `documents/` was found on the storage condition values or the hold time.
- **Question:** Which values are valid for the storage condition of a water sample, and should preparation apply the same checks as correction?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-067
- **Area:** ENV
- **Status:** Open
- **What the code does:** No code in Application or API sets `RoomMonitoring.IsOutOfTrend` or computes a step 1 plus step 2 count for environmental monitoring. The entity and `EMResultDto` carry the fields. Environmental monitoring locations are compared against alert, action and specification limits only. Location: `backend/MicroLIMS.Domain/Entities/RoomMonitoring.cs`; `backend/MicroLIMS.Application/Workflows/EMWorkflowEngine.cs`; `backend/MicroLIMS.Application/Workflows/TestWorkflow/IncubationStepRecorder.cs` — `RecordBatchResultsCoreAsync`.
- **What the other source says:** `documents/DesignSpecification/BusinessRules.md` lines 102 to 118 describe a two-step incubation per room and an automatic out-of-trend flag set when the sum of the two counts is above the action limit, persisted to `RoomMonitoring.IsOutOfTrend`, and say step 2 cannot start before step 1 is completed.
- **Question:** Is the out-of-trend rule still required? If yes, where should it be computed; if no, should the business rules file and the unused entity fields be removed?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-068
- **Area:** ACL
- **Status:** Open
- **What the code does:** After-cleaning preparation creates one test order per distinct test code (or one per part for HPLC and ICP method tests in the physicochemical section) from the tests configured on the chosen machine parts. It has no collective pathogen order and no order named from "TAMC" and the part name. Location: `backend/MicroLIMS.Application/Workflows/AfterCleaningWorkflowEngine.cs` — `PrepareCoreAsync`.
- **What the other source says:** `documents/DesignSpecification/BusinessRules.md` lines 120 to 133 say one collective pathogen test order is created when a selected part is marked `IsCollectivePathogenSample`, and every selected part gets its own individual count order with test code "TAMC:{partName}".
- **Question:** Which description is correct, and should the business rules file be rewritten to match the configuration-driven engine?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-069
- **Area:** ACL
- **Status:** Open
- **What the code does:** The cleaning residue mean is evaluated against the item's specification row for the analyte. The analyte's MACO limit and unit (stored on the HPLC method) are carried into the calculator but are not used in the comparison; `HplcSampleEntryContext` also shows the MACO limit as the upper limit in the entry screen data. Location: `backend/MicroLIMS.Application/Helpers/CleaningResidueCalculator.cs` — `Evaluate`; `backend/MicroLIMS.Application/Services/HplcSampleEntryContext.cs` — `BuildCleaningResidueInputs`.
- **What the other source says:** No line in `documents/` was searched that fixes whether the MACO limit or the specification row decides conformity. The comment in the calculator cites "spec 2026-11-07", which was not found under `documents/`.
- **Question:** Which limit decides the conformity of a cleaning residue result, and must the two always be equal?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-070
- **Area:** MON
- **Status:** Open
- **What the code does:** "Excursion" means different things in two places. The occurrence status counts an approved result at the alert limit as PerformedExcursion. The deviation scan opens a deviation only for action limit, out of specification or detected, and not for the alert limit. A rejected, retested or superseded test also counts as PerformedExcursion in the status, but opens no deviation. Location: `backend/MicroLIMS.Application/Services/Monitoring/OccurrenceStatus.cs` — `OccurrenceProgressRules.For`; `backend/MicroLIMS.Application/Services/Monitoring/MonitoringDeviationService.cs` — `OpenExcursionsAsync`.
- **What the other source says:** The code comment in `OccurrenceProgressRules.For` says a rejected, retested or out-of-limit result still counts as performed because the sampling was done ("spec 10, N2"); that spec was not found under `documents/`. `documents/DesignSpecification/BusinessRules.md` has no monitoring plan section.
- **Question:** Should an alert-limit result be shown as an excursion in the plan status but open no deviation? Is it correct that a rejected test counts as the planned sampling done?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-071
- **Area:** MON
- **Status:** Open
- **What the code does:** When several deviations are closed in one request, one pair of signatures is created and bound to the first deviation's ID only; the other deviations link to the same two signatures. Location: `backend/MicroLIMS.Application/Services/Monitoring/MonitoringDeviationService.cs` — `CloseAsync`; `backend/MicroLIMS.Application/Services/Monitoring/DualSignature.cs` — `SignAsync`.
- **What the other source says:** `CLAUDE.md` Rule 3.3 item 5 says a signature must be permanently bound to the exact signed record version and not be transferable to another record. The same pair of signatures covers up to 500 deviations.
- **Question:** Is one signature pair for a batch of deviations acceptable, or must each deviation carry its own signature?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-072
- **Area:** ENV
- **Status:** Open
- **What the code does:** `EnsureSectionsAccessAsync` passes when the user is a member of at least one of the listed sections. Environmental monitoring preparation passes the sections of all chosen tests, so a user who belongs to only one of several sections involved can create orders for the others. Location: `backend/MicroLIMS.Application/Services/UserSectionScopeService.cs` — `EnsureSectionsAccessAsync`; used by `backend/MicroLIMS.Application/Workflows/EMWorkflowEngine.cs` — `PrepareCoreAsync`.
- **What the other source says:** `CLAUDE.md` section 5, item 2 says services must verify laboratory-section scope before mutating a record. The error text of the method ("These tests belong to a laboratory section you are not assigned to") reads as if every section must be in scope.
- **Question:** Must the user belong to every section involved, or is one enough?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-073
- **Area:** MON
- **Status:** Open
- **What the code does:** No method of the monitoring plan or deviation services checks the laboratory section of the user. A holder of `MonitoringPlan.View`, `Manage` or `Approve` sees and changes the plan for every laboratory. The approver of a revision is not required to differ from the user who generated the draft. Location: `backend/MicroLIMS.Application/Services/Monitoring/MonitoringPlanService.cs` — `GenerateDraftAsync`, `ApproveAsync`.
- **What the other source says:** `CLAUDE.md` section 5, item 2 requires section scope checks; Principle 3 requires that a person who performs a step cannot approve their own work across the regulated record lifecycle (monitoring plan revisions are not named there).
- **Question:** Is plan-wide access intended for these permissions, and must the approver differ from the generator?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-090
- **Area:** PHY
- **Status:** Open
- **What the code does:** The shared check for physicochemical recording tests the equipment's inventory status (InService) and its section code ("FP") only. No calibration due date, calibration status or qualification status is read. Location: `backend/MicroLIMS.Application/Workflows/TestWorkflow/TestWorkflowSupport.cs` — `ValidateTestAnalysisOrderAsync`. HPLC and ICP method assays pass no equipment ID to this check, so even that check is skipped for them. Location: `backend/MicroLIMS.Application/Workflows/TestWorkflow/HplcMethodAssayRecorder.cs` — `SubmitCoreAsync`.
- **What the other source says:** `CLAUDE.md` Rule 3.2 ("Equipment, Columns & Standards Traceability") says active equipment linked to test execution must be within valid calibration/qualification status and expiry. `HplcRunService.DetermineUnavailableReasonAsync` does read `Equipment.CalibrationDueDate` when an HPLC or GC run starts.
- **Question:** Should every physicochemical result recording check the equipment's calibration and qualification status at the time of recording, as the run start does for HPLC, GC and ICP?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-091
- **Area:** PHY
- **Status:** Open
- **What the code does:** The value compared with the specification is not rounded in the same way across result types. Measurement and gravimetric results are compared before display rounding. Location: `backend/MicroLIMS.Application/Helpers/MeasurementCalculator.cs` — `Calculate`; `backend/MicroLIMS.Application/Helpers/GravimetricCalculator.cs` — `Calculate`. Titration, UV (non-limit modes) and outsourced numeric results are rounded first (to the larger of 2 decimals and the specification decimals) and the rounded value is compared. Location: `backend/MicroLIMS.Application/Workflows/TestWorkflow/TitrationRecorder.cs` — `RecordCoreAsync`; `UvRecorder.cs` — `RecordCoreAsync`; `OutsourcedRecorder.cs` — `RecordCoreAsync`. For a measurement, the displayed text can therefore show a value that looks equal to a limit while the stored status was decided on the unrounded value.
- **What the other source says:** No approved specification line in `documents/` was found on which value is compared with a limit. `documents/DesignSpecification/BusinessRules.md` has no physicochemical rule (searched for dissolution, titration, HPLC, assay, gravimetric, weight variation, working standard, solution).
- **Question:** Is a physicochemical result compared with its limit before rounding, or after rounding to the reported precision? Should every result type follow the same rule?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-092
- **Area:** PHY
- **Status:** Open
- **What the code does:** A later stage of dissolution, disintegration or weight variation changes the existing analysis rows instead of adding linked records. The stage method replaces the signature, the entered-at time and (when given) the comment on the same `TestAnalysis`, and replaces the reported value, text, calculation text and status of the same parameter result. The entering user of the analysis is not changed, so a later stage signed by another user shows the first user as the enterer. Weight variation also rewrites the deviation and pass flag of every stage 1 reading against the combined mean. Location: `backend/MicroLIMS.Application/Workflows/TestWorkflow/DissolutionRecorder.cs` — `RecordDissolutionStageCoreAsync`; `DisintegrationRecorder.cs` — `RecordDisintegrationStageCoreAsync`; `WeightVariationRecorder.cs` — `RecordWeightVariationStageCoreAsync`. The earlier values remain only in the audit trail.
- **What the other source says:** `CLAUDE.md` Rule 3.1 says corrections, invalidations and retests must append new linked records or versioned states while preserving the original recorded values, attribution and reason. Rule 3.3 point 5 says a signature is permanently bound to the exact signed record version.
- **Question:** Should each stage be stored as its own signed record (or a versioned state) so that the stage 1 signature and values stay on the record, as Rule 3.1 and Rule 3.3 describe?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-093
- **Area:** PHY
- **Status:** Open
- **What the code does:** Numbers that define the method are written in the code and used when the Test Master has no value: dissolution offsets 5, 15, 25 and 2; disintegration units 6 and 12, maximum stage 1 failures 2, minimum passes 16; weight variation unit count 20, tablet bands 130 mg at 10 %, 324 mg at 7.5 %, 5 %, tablet maximum outside 2, capsule inner 10 %, outer 25 %, stage limits 2, 6, 40 and 6; titration expansion coefficient 0.0011; UV wavelength 190 to 1100 nm and maximum absorbance 5. Location: `backend/MicroLIMS.Application/Workflows/TestWorkflow/DissolutionRecorder.cs` — `RecordDissolutionResultCoreAsync`; `DisintegrationRecorder.cs` — `RecordDisintegrationResultCoreAsync`; `WeightVariationRecorder.cs` — `RecordWeightVariationResultCoreAsync`; `backend/MicroLIMS.Application/Services/MasterData/TitrationDefinitionRules.cs` — `DefaultExpansionCoefficient`; `UvRecorder.cs` — `MinWavelengthNm`, `MaxWavelengthNm`, `MaxAbsorbance`. The dissolution code comment cites a laboratory decision of 2026-09-19.
- **What the other source says:** `CLAUDE.md` Frozen Guiding Principle 1 and Rule 3.2 say formulas, limits and step sequences come from the approved method/test configuration and that no method-specific constant, limit or formula may be hardcoded in a service. No approved specification in `documents/` was found for the laboratory decision of 2026-09-19.
- **Question:** Should these defaults be removed so that a test with no configured value is refused, or are they approved site rules that belong in an approved document?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-094
- **Area:** PHY
- **Status:** Open
- **What the code does:** Recording a physicochemical result checks laboratory section membership only. The Physicochemical area of the user (finished product, or raw and packaging material) is checked when the workspace is listed but not when a result is recorded. Location: `backend/MicroLIMS.Application/Services/UserSectionScopeService.cs` — `EnsureTestOrderAccessAsync`; compare `GetPhyschemAreasAsync` and `backend/MicroLIMS.Application/Services/TestingWorkspaceService.cs` — `ResolveAreaAsync`.
- **What the other source says:** `CLAUDE.md` Rule 5.2 says services must verify resource-level scope, e.g. that the user is assigned to the target LaboratorySection / PhyschemArea, before mutating a record by ID.
- **Question:** Should the recorders refuse a user whose area membership does not cover the sample's area?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-095
- **Area:** INS
- **Status:** Open
- **What the code does:** HPLC and ICP results are judged on a value that is not the value shown. HPLC assay text is rounded to 1 decimal (`FormatPercent`) while the status is decided on the unrounded mean; a dried or anhydrous basis is rounded to 2 decimals before the status is decided. ICP assay text is rounded to 1 decimal and ICP ppm text to 4 decimals while the status is decided on the unrounded value. Related substances are rounded to 2 decimals (or replaced by 0 below the disregard limit) before the status is decided. Location: `backend/MicroLIMS.Application/Helpers/HplcSampleAssayEvaluator.cs` — `Evaluate`, `FormatPercent`; `HplcAssayCalculator.cs` — `ApplyBasis`; `IcpSampleEvaluator.cs` — `Evaluate`; `RelatedSubstancesEvaluator.cs` — `Calculate`.
- **What the other source says:** No approved specification line in `documents/` was found on precision or on which value is compared. Related to CONF-091.
- **Question:** At which precision should HPLC, ICP and related-substance values be compared with the limit, and should the shown text always be the compared value?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-096
- **Area:** INS
- **Status:** Open
- **What the code does:** The calibration due date of an HPLC, GC or ICP instrument is read when a run starts. It is not read when SST or calibration is confirmed, when samples are assigned, or when a result is sent for review, which can be days after the run started. Mobile phase expiry is re-checked at SST confirmation but not at submission. Location: `backend/MicroLIMS.Application/Services/HplcRunService.cs` — `StartRunAsync`, `DetermineUnavailableReasonAsync`, `ConfirmSstAsync`; `backend/MicroLIMS.Application/Services/IcpRunService.cs` — `StartRunAsync`; `backend/MicroLIMS.Application/Workflows/TestWorkflow/HplcMethodAssayRecorder.cs` — `SubmitCoreAsync`; `IcpMethodAssayRecorder.cs` — `SubmitCoreAsync`. An instrument with no calibration due date counts as in calibration, which follows the decision recorded under CONF-003.
- **What the other source says:** `CLAUDE.md` Rule 3.2 says equipment and columns linked to test execution must be within valid calibration/qualification status and expiry. The column's own expiry is not read anywhere in `StartRunAsync`.
- **Question:** Should calibration status (and column and mobile phase expiry) be checked again when the result is sent for review?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-097
- **Area:** INS
- **Status:** Open
- **What the code does:** The ICP minimum correlation (0.999) and the calibration validity (24 hours) are constants in the code ("There is no method master any more, so these are system-wide"). The correlation r of each element is typed by the analyst; the server judges the typed number and does not read it from the calibration report file. Location: `backend/MicroLIMS.Application/Helpers/IcpDefaults.cs` — `MinCorrelation`, `CalibrationValidHours`; `backend/MicroLIMS.Application/Services/IcpRunService.Calibration.cs` — `SaveCalibrationAsync`, `ConfirmCalibrationAsync`.
- **What the other source says:** `CLAUDE.md` Rule 3.2 says never hardcode a method-specific constant or limit in a service. Frozen Principle 1 says configuration drives behaviour. The HPLC equivalents (SST criteria) are configured in the method master and copied into the run.
- **Question:** Are 0.999 and 24 hours approved site rules? Should the criteria be configured, and should the typed r be checked against the report or the curve?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-098
- **Area:** PHY
- **Status:** Open
- **What the code does:** The UV standard comparison reuses the HPLC weight-based assay formula: the total volume is passed as the theoretical sample weight and the standard volume as the theoretical standard weight, and the result is multiplied by the standard's purity and (100 minus moisture) / 100. Location: `backend/MicroLIMS.Application/Helpers/UvCalculator.cs` — `StandardComparison`; `backend/MicroLIMS.Application/Helpers/StandardComparisonCalculator.cs` — `CalculatePreparationAssay`.
- **What the other source says:** No approved method or specification in `documents/` was found for the UV standard comparison. The parameter names of `CalculatePreparationAssay` are weights, not volumes.
- **Question:** Is it correct to use volumes in the weight positions of the standard comparison formula for a UV test?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-099
- **Area:** WST
- **Status:** Open
- **What the code does:** The working standard rules are numbers in the code: 6 replicates, a maximum RSD of 2.0 %, a validity of 12 months and a due-soon warning of 30 days. The RSD is compared after rounding to 2 decimals. The qualification passes on replicate count and RSD only; the potency is not compared with any limit. Location: `backend/MicroLIMS.Application/Helpers/WorkingStandardCalculator.cs` — `WorkingStandardRules`, `PassesRsd`, `Evaluate`.
- **What the other source says:** The code comment calls them a "Fixed site rule" (spec 2026-10-03). `CLAUDE.md` Rule 3.2 says limits and replicate rules come from the approved method configuration. No approved specification in `documents/` was found for them.
- **Question:** Are these approved site rules? Should a potency range apply, and should the RSD be compared before rounding?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-100
- **Area:** WST
- **Status:** Open
- **What the code does:** Approving a requalification replaces the purity, moisture and expiry of the existing working standard lot in place. Results already recorded keep their own copies (the SST copies purity and moisture when it is saved), but the lot row itself no longer shows the earlier values; they remain in the audit trail only. Location: `backend/MicroLIMS.Application/Services/WorkingStandardService.SignOff.cs` — `ApproveAsync`.
- **What the other source says:** `CLAUDE.md` Rule 3.1 says master data changes must not retroactively alter historical records and that corrections append new linked records or versioned states.
- **Question:** Should a requalification create a new lot version or a linked record instead of changing the lot in place?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-101
- **Area:** SOL
- **Status:** Open
- **What the code does:** A prepared solution whose recipe needs release is kept out of the availability lists while it is PendingReview or Rejected, and the prepared-solution picker refuses it. The methods that actually use a mobile phase or a titrant check only that the effective status is Prepared; they do not read the approval status. Location: `backend/MicroLIMS.Application/Services/HplcRunService.cs` — `StartRunAsync`; `backend/MicroLIMS.Application/Workflows/TestWorkflow/TitrationRecorder.cs` — `LoadPreparationAsync`; compare `backend/MicroLIMS.Application/Services/SolutionPreparationService.cs` — `GetAvailableAsync`, `CheckSource`.
- **What the other source says:** The comment in `backend/MicroLIMS.Application/Services/SolutionPreparationReleaseService.cs` says a PendingReview or Rejected preparation is not offered for use. `CLAUDE.md` Rule 1 principle 2 says the backend must re-validate consequential inputs from the frontend.
- **Question:** Should the server refuse a mobile phase or titrant preparation that is pending release or rejected when it is used in a run or a titration?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-120
- **Area:** MED
- **Status:** Open
- **What the code does:** The media lot is qualified by a media evaluation (challenges per lot, one result each, outcome Conform or NonConform) and released by a signed decision. No step records a sterility check, and no method sets or reads a GPT stage. The release decision approves a lot whose evaluation is Completed and Conform; it does not test the lot's expiry date or a sterility result. The lot is marked released for use by the signed decision alone (`IsReleasedForUse` set true). Other methods test the expiry date at the point of use; `IncubationStepRecorder` does not (see FS-MIC-002). The enum `GptStage` (Media Preparation, Sterility, Recovery, Release) still exists in the domain but is not used by the services read. Location: `backend/MicroLIMS.Application/Services/MediaReleaseService.cs` — `DecideCoreAsync`; `backend/MicroLIMS.Application/Workflows/MediaEvaluationEngine.cs` — `RecordResultAsync`; `backend/MicroLIMS.Domain/Enums/GptStage.cs`.
- **What the other source says:** `documents/DesignSpecification/BusinessRules.md` lines 136 to 153 (section 7) describe a GPT engine in `Application/Workflows/GptWorkflowEngine.cs` with stages Media Preparation, Sterility, Recovery, Release; advancing is strict; a lot passes only if every challenge organism passed; and `IsReleasedForUse` is true only when the stage is Release, the status is Active and the lot is not expired, checked together. That file does not exist in the repository. `CLAUDE.md` line 102 (Rule 3.2) says media used in testing must have passed GPT and sterility checks, be within expiry and be linked by prepared lot number.
- **Question:** Is the media evaluation plus the signed release the intended replacement for the GPT engine of the business rules? Must a sterility check be recorded before release, and must the expiry date be tested at release and at every use?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-121
- **Area:** MED
- **Status:** Open
- **What the code does:** For a growth promotion challenge, the outcome is Conform when no minimum recovery is configured (the maximum is then not tested at all). When a minimum is configured and the maximum is empty, the test `recovery <= maximum` is false and the outcome is always NonConform. Location: `backend/MicroLIMS.Application/Workflows/MediaEvaluationEngine.cs` — `RecordResultAsync`.
- **What the other source says:** No approved specification line in `documents/` was found that says what a missing minimum or maximum recovery means. The code comment in the same file says recovery is compared with the configured band, both ends included.
- **Question:** Should a growth promotion challenge with no configured recovery band be refused instead of conforming? Should a band with a minimum and no maximum (or the reverse) be refused when it is configured, or when it is used?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-122
- **Area:** MED
- **Status:** Open
- **What the code does:** Recovery percent is rounded to one decimal place with `Math.Round(value, 1)`, which uses the .NET default midpoint rule (to the nearest even digit). The count recorders round reported counts with `MidpointRounding.AwayFromZero`. The recovery is rounded before it is compared with the band, so a value just under the minimum may round up into the band. Location: `backend/MicroLIMS.Application/Workflows/MediaEvaluationEngine.cs` — `RecordResultAsync`; compare `backend/MicroLIMS.Application/Workflows/TestWorkflow/IncubationStepRecorder.cs` — `RecordCountTestAsync`.
- **What the other source says:** No approved specification line in `documents/` was found on rounding of recovery percent or on whether the band is compared before or after rounding. `CLAUDE.md` Rule 3.2 says rounding rules must be applied strictly from configuration.
- **Question:** What rounding rule (mode and decimals) applies to recovery percent, and is the comparison with the band made on the rounded or the unrounded value?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-123
- **Area:** MED
- **Status:** Open
- **What the code does:** Equipment used in media work is not checked for calibration, type, range or service status. (1) Media preparation accepts any equipment of type Autoclave. Location: `backend/MicroLIMS.Application/Services/MediaPreparationService.cs` — `PrepareCoreAsync`. (2) The media evaluation incubation accepts any equipment ID without checking it exists, is an incubator, has a set point in the configured range, is in calibration or is in service. Location: `backend/MicroLIMS.Application/Workflows/MediaEvaluationEngine.cs` — `RecordIncubationCoreAsync`. (3) Cryovial identity-confirmation rows store the incubator ID without any check. Location: `backend/MicroLIMS.Application/Services/CryovialService.cs` — `PrepareCryovialsCoreAsync`. In contrast, incubator selection for test media checks the set point range and the calibration due date. Location: `backend/MicroLIMS.Application/Services/IncubatorEligibilityService.cs` — `GetEligibleIncubatorsAsync`.
- **What the other source says:** `CLAUDE.md` line 103 (Rule 3.2): active equipment linked to test execution must be within valid calibration or qualification status and expiry. Decision CONF-003: equipment with no calibration due date counts as in calibration.
- **Question:** Must the autoclave of a media lot and the incubators of a media evaluation and of a cryovial identity confirmation pass the same calibration, status and range checks as the incubators used for test media?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-124
- **Area:** CRY
- **Status:** Open
- **What the code does:** A thawed vial may be used for 24 hours (`CryovialThaw.UsableFor`), and a destruction reminder is set at 48 hours (`CryovialThaw.DestructionReminderAfter`). Both are fixed values in the code, not configuration. The same 24-hour value is used in the query that lists usable thawed vials. Location: `backend/MicroLIMS.Application/Services/CryovialThaw.cs` — `UsableFor`, `DestructionReminderAfter`, `EnsureThawedVialUsable`; `backend/MicroLIMS.Application/Services/MediaEvaluationService.cs` — `GetThawedVialOptionsAsync`.
- **What the other source says:** No approved specification line in `documents/` was found for the 24-hour and 48-hour periods (a search of `documents/` for "thaw" and "cryovial" found only a scope line in `documents/DocumentControl/ML-SYS-VP-001_Validation_Plan.md` line 32, with no period). The code comment says only that after 24 hours a vial may no longer be used. `CLAUDE.md` Principle 1 and Rule 3.2 say times and limits come from configuration set by the Section Head.
- **Question:** Are the 24-hour use period and the 48-hour reminder the approved rules? Should they be configured by the Section Head instead of fixed in the code?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-125
- **Area:** EQP
- **Status:** Open
- **What the code does:** When an inventory item is linked to the configuration list, a new incubator is given a set point of 32.5 (a literal in the code), and the new item is placed in the section with code `MICRO` (also a literal). The set point is then used to decide which media an incubator may serve. No set-point history row is written for it. Location: `backend/MicroLIMS.Application/Services/EquipmentConfigurationService.cs` — `LinkInventoryEquipmentToMasterAsync`.
- **What the other source says:** `CLAUDE.md` Rule 3.2 (line 101) says never hardcode a method-specific constant or limit in a service; Principle 1 says configuration by the Section Head drives behaviour. No approved specification line in `documents/` was found for the value 32.5.
- **Question:** Should a linked incubator start with no set point until the Section Head enters one, and should the section be taken from the register row?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-126
- **Area:** EQP
- **Status:** Open
- **What the code does:** One physical asset has two records, matched only by code text. The incubator check (FS-EQP-012 place 1) reads the configuration record and looks at its calibration due date and set point only; it does not read the register status. The register record carries the status, the calibration plan and the automatic out-of-service scan. The scan changes the register row only. A calibration record updates both due dates, but a manual edit of the register (`EquipmentInventoryService.UpdateAsync`) or of the configuration item (`EquipmentMasterDataService.UpdateEquipmentAsync`) changes only its own record, with no reason for the date. The configured summary shows the configuration date or, if that is empty, the register date. So an incubator can be shown as having a past due date and be out of service in the register while the incubator check still offers it. The HPLC and ICP availability checks read both records. Location: `backend/MicroLIMS.Application/Services/IncubatorEligibilityService.cs` — `GetEligibleIncubatorsAsync`; `backend/MicroLIMS.Application/Services/HplcRunService.cs` — `DetermineUnavailableReasonAsync`; `backend/MicroLIMS.Application/Services/EquipmentPlan/EquipmentWorkService.cs` — `ApplyOverdueAsync`, `RecordCalibrationAsync`; `backend/MicroLIMS.Application/Services/EquipmentConfigurationService.cs` — `GetConfiguredEquipmentSummaryAsync`.
- **What the other source says:** Decision CONF-003 (`documents/Conflicts.md`): equipment with no calibration due date counts as in calibration. The decision does not say which of the two records holds the due date, or whether an out-of-service register status makes an incubator ineligible. `CLAUDE.md` line 103 says equipment must be within valid calibration status.
- **Question:** Which record is the single source for calibration due date and service status? Should the incubator check refuse an incubator that is OutOfService or Retired in the register, as the HPLC and ICP checks do? When the configuration date is empty and the register date is set, which one decides?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-127
- **Area:** INV
- **Status:** Open
- **What the code does:** The activity type shown in equipment traceability is decided by text: a step name containing "Pathogen", or a test code starting with "PAT", gives "Pathogen Test"; a step name containing "GPT" gives "GPT"; anything else gives "Media Incubation". The media evaluation incubation has the step name "MediaEvaluation", so it shows as "Media Incubation". The category of idle equipment is decided from the instrument type text and the code prefixes INC, AUT and LAF. The same text rule is written three times. Location: `backend/MicroLIMS.Application/Services/EquipmentInventoryService.cs` — `GetActiveActivitiesForEquipmentInternalAsync` (and the history and where-is-it methods), `ClassifyEquipmentCategory`.
- **What the other source says:** `CLAUDE.md` line 100 (Rule 3.2): never branch on a test code. `CLAUDE.md` Principle 1: workflow type comes from configuration, not from code text.
- **Question:** Should the activity type come from the configured workflow type of the test or step instead of the test code prefix and step name text? Should media evaluation incubations be shown as their own type?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-128
- **Area:** EQP
- **Status:** Open
- **What the code does:** The calibration due date is compared in two ways. (a) As an instant: the incubator checks treat a due date at 00:00 UTC on the due day as past once the clock is after that instant, so from the first minute of the due day the incubator is refused. Location: `backend/MicroLIMS.Application/Services/IncubatorEligibilityService.cs` — `GetEligibleIncubatorsAsync`; `backend/MicroLIMS.Application/Services/GroupedTestAction/BatchSelectMediaRules.cs` — `EnsureIncubatorUsable`. (b) As a date: the HPLC and ICP checks and the automatic scan treat the item as overdue only when the due date is before today's lab date, so the item is usable all through the due day. Location: `backend/MicroLIMS.Application/Services/HplcRunService.cs` — `DetermineUnavailableReasonAsync`; `backend/MicroLIMS.Application/Services/EquipmentPlan/EquipmentWorkService.cs` — `ApplyOverdueAsync`. The register flag uses UTC date; the lab date used by the scan and by HPLC and ICP comes from the lab clock. Location: `backend/MicroLIMS.Domain/Entities/EquipmentInventory.cs` — `IsCalibrationOverdueAt`; `backend/MicroLIMS.Application/Services/SectionCountersService.cs` — `EquipmentCardAsync`.
- **What the other source says:** Decision CONF-003 covers a missing due date only. No approved specification line in `documents/` was found on whether the due day itself counts as in calibration, or on which time zone the day is read in.
- **Question:** Is equipment in calibration through the whole due date, or only until the start of it? Which clock (UTC or lab date) defines the day?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-150
- **Area:** MDM
- **Status:** Open
- **What the code does:** A specification row is changed in place. `UpdateSpecificationAsync` overwrites the limits, unit, basis, grade and other fields of the existing row and re-creates its stages. `DeleteSpecificationAsync` removes the row. No effective date, no version number and no copy of the old row is kept; the old values survive only in the automatic audit row. No reason for change is asked for, and the delete has no stale-form version check. The code comment in the delete method says no dependent exists because results copy the alert, action and specification values as strings at calculation time; that was not checked against the result tables. Location: `backend/MicroLIMS.Application/Services/MasterData/SpecificationMasterDataService.cs` — `UpdateSpecificationAsync`, `DeleteSpecificationAsync`.
- **What the other source says:** `CLAUDE.md` Rule 3.1 (line 92): modifying an Item Specification or limit must never retroactively alter or re-evaluate recorded or approved results; historical records must stay bound to the specification version, units, precision and criteria active when the test was performed and approved; changes must append new linked records or versioned states and keep the reason for change.
- **Question:** Does every recorded result keep its own copy of the limit text, unit, precision and criteria, so that changing or deleting a specification row cannot change a past verdict? If not, must a specification row be versioned, and must a reason be required?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-151
- **Area:** MDM
- **Status:** Open
- **What the code does:** A Test Master row and a workflow step are changed in place. `UpdateTestDefinitionAsync` can change the test code, equation type, workflow type, replicate counts, system suitability and the other settings without checking whether test orders or results already use the test. When the code is renamed, no other table that stores the old code as text (specifications, assigned item tests, test orders) is changed by this method. A reason is required only for titration settings and the moisture role. `UpdateTestWorkflowStepAsync` edits a step that test orders have already used (the comment says an incubation row keeps its own copy of temperature and duration), while `DeleteTestWorkflowStepAsync` refuses a step that was used. Location: `backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs` — `UpdateTestDefinitionCoreAsync`; `backend/MicroLIMS.Application/Services/MasterData/TestWorkflowStepMasterDataService.cs` — `UpdateTestWorkflowStepAsync`, `DeleteTestWorkflowStepAsync`.
- **What the other source says:** `CLAUDE.md` Rule 3.1 (line 92): modifying a Workflow Profile, STM or master data must never retroactively alter previously recorded results, and corrections must append linked records and keep the reason for change.
- **Question:** Should a Test Master row that already has test orders be protected from a change of code, equation type or workflow type? Should a reason for change be required for every change to a test, and for a change to a step that has been used?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-152
- **Area:** MDM
- **Status:** Open
- **What the code does:** When a dissolution, disintegration or weight-variation test is saved with a setting left empty, the code fills in a number written in the service. Dissolution offsets 5, 15, 25 and 2; disintegration stage-1 units 6, stage-2 units 12, maximum stage-1 failures 2, minimum pass total 16; weight variation unit count 20, tablet band 1 up to 130 mg at 10 percent, band 2 up to 324 mg at 7.5 percent, band 3 at 5 percent, maximum outside 2, capsule inner 10 percent and outer 25 percent, capsule stage-1 maximum outside 2 and for retest 6, stage-2 extra units 40 and maximum outside 6. The same defaults are written again on update. The weight-variation canonical specification text also contains the text "USP <2091>". Location: `backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs` — `CreateTestDefinitionAsync`, `UpdateTestDefinitionCoreAsync`; `backend/MicroLIMS.Application/Services/SpecificationService.cs` — `BuildCanonicalSpecLimit`.
- **What the other source says:** `CLAUDE.md` Rule 3.2 (line 101): never hardcode a method-specific constant, limit or formula in a service; calculations use the configured equation type, replicate rules, units, precision and acceptance criteria. `CLAUDE.md` Principle 1 (line 36): configuration drives behaviour. No approved document in `documents/` was found that states these default numbers.
- **Question:** Are these defaults an approved starting point that the Section Head must confirm, or should the fields be required with no default? Which approved document gives the numbers?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-153
- **Area:** MDM
- **Status:** Open
- **What the code does:** The workbook load sets items, material master entries and reference organisms inactive until a second person verifies the load. Specifications and specification stages created by the same load are active at once and are not set inactive; the verification does not touch them. A specification added for an item that is already active is therefore live before any second-person check. Location: `backend/MicroLIMS.Application/Services/MasterDataWorkbook/Sheets/SpecificationSheets.cs` — `CreateAsync`; `backend/MicroLIMS.Application/Services/MasterDataWorkbook/MasterDataWorkbookService.cs` — `ImportAsync`, `VerifyAsync`.
- **What the other source says:** The workbook instructions say that a second person checks the load against the approved source documents and signs Verify, and that this activates the items, material master entries and reference organisms. Location: `backend/MicroLIMS.Application/Services/MasterDataWorkbook/MasterDataSheets.cs` — `Instructions`, line 48. Specifications are not mentioned.
- **Question:** Should specifications loaded by a workbook also wait for the second-person verification before they apply? Is it intended that media products and equipment loaded by a workbook are not held back either?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-154
- **Area:** MDM
- **Status:** Open
- **What the code does:** The workbook download filters sheets by the user's readable sections, but the specification and specification-stage sheets ignore that scope and list every specification. The list of loads (`GetImportsAsync`) and the stored file of a load (`GetFileAsync`) are available to any holder of `MasterData.Manage` with no section check. The existence test for rows during a load compares against all sections. Location: `backend/MicroLIMS.Application/Services/MasterDataWorkbook/Sheets/SpecificationSheets.cs` — `ExportAsync` (both sheets); `backend/MicroLIMS.Application/Services/MasterDataWorkbook/MasterDataWorkbookService.cs` — `GetImportsAsync`, `GetFileAsync`; `backend/MicroLIMS.Application/Services/MasterDataWorkbook/MasterDataWorkbookRunner.cs` — `RunAsync`.
- **What the other source says:** `CLAUDE.md` Section 5, item 2 (line 175): backend services must verify resource-level and laboratory-section scope before reading a record by ID. The same workbook download does apply the scope to the material master sheet.
- **Question:** Should the specification sheets, the load list and the stored load file be limited to the user's sections? Or are specifications and loads meant to be visible to all master-data users?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-155
- **Area:** MDM
- **Status:** Open
- **What the code does:** Room test configurations (environmental monitoring test type, test code, alert, action and specification limit text, unit) are created, changed and deleted by a holder of `MasterData.Manage` with no check of the laboratory that owns the test code, and the limit text is not checked by `LocationChemistryLimitRules`. The limits are overwritten in place. The corresponding water sampling configurations and machine part configurations do check test ownership and validate the limit fields. The control chart `apply-limits` action changes the same room limits only with a reason, a signature and a version check. Location: `backend/MicroLIMS.Application/Services/MasterData/EnvironmentalMonitoringMasterDataService.cs` — `CreateRoomTestConfigurationAsync`, `UpdateRoomTestConfigurationAsync`, `DeleteRoomTestConfigurationAsync`; compare `backend/MicroLIMS.Application/Services/MasterData/WaterMasterDataService.cs` — `UpdateWaterSamplingConfigurationAsync`.
- **What the other source says:** `CLAUDE.md` Section 5, item 2 (line 175): services must verify laboratory-section scope before mutating a record by ID. `CLAUDE.md` Rule 3.1 (line 92): changes to alert and action limits must not alter historical results.
- **Question:** Should room test configurations follow the same ownership and limit checks as water and after-cleaning configurations? Should a change of EM alert or action limits through the master-data screen need a reason and a signature, as it does through the control chart?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-156
- **Area:** RPT
- **Status:** Open
- **What the code does:** Two places decide behaviour from the text of a test code. The Certificate of Analysis matrix puts every test whose code starts with "TAMC" (upper-cased) first. The pathogen result matrix treats a test as quantitative when its workflow type is CountTest or its code contains "TAMC" or "TYMC". Location: `backend/MicroLIMS.Application/Services/CertificateOfAnalysisBuilder.cs` — `BuildMatrix` (line 99); `backend/MicroLIMS.Application/Services/PathogenSession/ResultMatrixBuilder.cs` (lines 36 and 37).
- **What the other source says:** `CLAUDE.md` Rule 3.2 (line 100): never branch on a test code or organism name; and Rule 3.2 (line 104): never order certificate rows by test code or name. `CLAUDE.md` Principle 1 (line 36): configuration drives behaviour. `OrganismLiteralTests` named in Rule 3.2 covers organism names; whether it covers these test-code literals was not checked.
- **Question:** Should the matrix column order and the quantitative decision come from the configured workflow type and the Test Master CoA order instead of the text of a test code?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-157
- **Area:** RPT
- **Status:** Open
- **What the code does:** The conformance of a result is decided differently in the two parts of the same report package. The Certificate of Analysis builder and the test card footer treat a missing status as "no result" (not certified) and a status with no configured limits as "limits not configured". The results matrix of the archived sample report counts a location with an empty status as conforming (`loc.Status is null || ConformingStatuses.Contains(...)`) and counts a LimitsNotConfigured location as an exception. The analysis card says "Out of limits" for any parameter whose status is not WithinLimits or Absent, including one with no limits configured. Location: `backend/MicroLIMS.Application/Services/ReportDocumentMapper.cs` — `ToResultsMatrix` (line 247), `ToAnalysisCard` (line 497), `ConformingStatuses` (line 210); `backend/MicroLIMS.Application/Helpers/ResultConformance.cs` — `ResultConformanceRules.FromStatus`.
- **What the other source says:** `CertificateOfAnalysisBuilder` states in its header comment that every verdict comes from `ResultConformanceRules` and that only a conforming result is certified. This is a related but separate point from CONF-008 (the private rule in `SampleApprovalService`).
- **Question:** Should the results matrix and the analysis card of the sample report use `ResultConformanceRules`, so that a missing result is never shown as conforming and a missing limit is not shown as "Out of limits"?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-158
- **Area:** RPT
- **Status:** Open
- **What the code does:** Dates and times in the archived sample report, including the signing time of each electronic signature, are formatted with the pattern "dd-MMM-yyyy HH:mm". No time zone or "UTC" mark is printed. Whether the value is converted from UTC before it reaches the mapper was not traced. Location: `backend/MicroLIMS.Application/Services/ReportDocumentMapper.cs` — `Dt` (line 16), `ToSignatureBlock` (line 44).
- **What the other source says:** `CLAUDE.md` Rule 3.3 item 5 (line 116): every generated report of a signed record must show the signer's printed full name, the UTC date and time of signing displayed with an explicit time zone, and the meaning. `CLAUDE.md` Section 5 item 6 (line 195): always display the time zone alongside regulated timestamps.
- **Question:** Should the report print the time zone with every regulated timestamp and signature time? Is the value passed to the mapper UTC?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-159
- **Area:** RPT
- **Status:** Open
- **What the code does:** The archived sample report rounds the average and calculated result of a count reading to a whole number with `MidpointRounding.AwayFromZero`, for display, and prints the reported result as stored. The Certificate of Analysis prints the stored calculated or CFU value with no rounding (format "0.############################"). A count value with a fraction can therefore read differently in the two documents. Location: `backend/MicroLIMS.Application/Services/ReportDocumentMapper.cs` — `Cfu` (line 205); `backend/MicroLIMS.Application/Services/CertificateOfAnalysisBuilder.cs` — `BuildMatrix`.
- **What the other source says:** `CLAUDE.md` Rule 3.2 (line 98): whole-number colony counts and the configured rounding rules are applied in backend services. CONF-001 records a different rounding difference (screen preview against server).
- **Question:** Which rounding applies to an average or calculated count shown in a report, and should the certificate and the sample report show the same number?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-160
- **Area:** RPT
- **Status:** Open
- **What the code does:** The trend and compare reports replace a result flagged below the detection limit by half of its detection limit (`DetectionLimit / 2`) before computing the mean, standard deviation, minimum, maximum and latest value, and the compare mean. The compliance percent leaves out results whose level is LimitsNotConfigured. The mean is rounded to 2 decimals and percentages to 1 decimal with the default `Math.Round` midpoint rule. None of this is configured by the Section Head. Location: `backend/MicroLIMS.Application/Services/ReportingQueryService.cs` — `Impute` (line 511), `GetTrendAsync`, `GetCompareBySubjectAsync` (lines 467 to 476).
- **What the other source says:** No approved specification in `documents/` was found for substituting half the detection limit, for the compliance percent, or for the rounding of these statistics. `CLAUDE.md` Rule 3.2 (line 98) says rounding and averaging rules come from configuration.
- **Question:** Is substituting half the detection limit the approved way to treat below-detection results in trends, and should the factor and the rounding be configured?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-161
- **Area:** NTF
- **Status:** Open
- **What the code does:** The approval and review waiting notifications count samples whose section sign-off is under approval or under review, for the role SectionHead (approval) or Reviewer and SectionHead (review), or for a holder of the permission `Dashboards.LabOverview` or `Dashboards.Review`. The incubation-ready notification also requires the test order not to be superseded and to be Pending or InProgress. Location: `backend/MicroLIMS.Application/Services/DashboardNotificationService.cs` — `ComputeAsync`.
- **What the other source says:** `documents/DesignSpecification/BusinessRules.md` section 12, lines 228 to 231: Approval Waiting is "any `TestOrder.Status == Reviewed`" shown to SectionHead and Admin only; Review Waiting is "any `TestOrder.Status == ResultEntered`" shown to Reviewer, SectionHead and Admin; Incubation Ready has only the completed-time and step conditions.
- **Question:** Which rule is right: waiting test orders by status, or samples by section sign-off? Which roles or permissions should see each notification?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-162
- **Area:** OOS
- **Status:** Open
- **What the code does:** The OOS tracking list loads sample entities with several related entities, with no `AsNoTracking`, no projection to DTOs in SQL, no paging and no row limit. Location: `backend/MicroLIMS.Application/Services/OosTrackingService.cs` — `GetOosGroupsAsync`.
- **What the other source says:** `CLAUDE.md` Section 5 item 4 (line 184): every read-only query in dashboards, reports, workspace listings and search services must use `AsNoTracking` and project to DTOs in SQL, and never materialize unbounded entity graphs.
- **Question:** Should the OOS tracking query be rewritten to meet the read-query rule, or is the list expected to stay small?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-163
- **Area:** RPT
- **Status:** Open
- **What the code does:** The reporting queries are not `AsNoTracking`, and the trend, compare and qualitative-event methods load the whole matching record list into memory before computing statistics. The search is paged with a cap of 100 and the CSV export is capped at 10,000 rows, but trend, compare, qualitative events and overview have no row limit. Location: `backend/MicroLIMS.Application/Services/ReportingQueryService.cs` — `GetTrendAsync`, `GetCompareBySubjectAsync`, `GetQualitativeEventsAsync`.
- **What the other source says:** `CLAUDE.md` Section 5 item 4 (line 184): read queries in reports must use `AsNoTracking`, project to DTOs in SQL and not materialize unbounded entity graphs.
- **Question:** Should these report queries be rewritten as SQL projections, or are they accepted at the present data volume?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-164
- **Area:** RPT
- **Status:** Open
- **What the code does:** The control chart action that applies proposed alert and action limits to room test configurations and water sampling configurations needs only the permission `MasterData.Manage`. It makes no check that the test or location belongs to a laboratory section the user may configure. The read scope is used only to load the series. Location: `backend/MicroLIMS.Application/Services/Spc/ControlChartService.cs` — `ApplyLimitsCoreAsync`, `LoadConfigurationAsync`.
- **What the other source says:** The master-data methods that change the same limits check test ownership: `TestCodeOwnership.EnsureCanEditAsync` in `WaterMasterDataService.UpdateWaterSamplingConfigurationAsync` and `AfterCleaningMasterDataService.UpdateMachinePartConfigurationAsync`. `CLAUDE.md` Section 5 item 2 (line 175): services must verify laboratory-section scope before mutating a record by ID.
- **Question:** Should applying control chart limits check that the user may configure the laboratory that owns the test code?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-165
- **Area:** NTF
- **Status:** Open
- **What the code does:** An error-severity notification sends an email to an address built from the user name, `{username}@microlims.local`, not from an e-mail address on the user record (whether the user record has an e-mail field was not checked). The email and the push are sent from a request that only reads the notification list, before the notification rows are saved. Location: `backend/MicroLIMS.Application/Services/DashboardNotificationService.cs` — `PersistAndDeliverAsync` (line 266).
- **What the other source says:** `documents/DesignSpecification/BusinessRules.md` section 12, lines 235 to 237: error notifications trigger an email through `IEmailSender` (no-op if SMTP is not configured) and a real-time push.
- **Question:** Is `{username}@microlims.local` the intended recipient, or should the user's stored e-mail address be used? Should the email be sent only after the notification rows are saved?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-180
- **Area:** INS
- **Status:** Open
- **What the code does:** Relative standard deviation (RSD) gates are applied in different ways. Preparation RSD, system suitability RSD and the titration factor and series RSD compare the unrounded RSD with the maximum using `>` (a value equal to the maximum passes). Location: `backend/MicroLIMS.Application/Helpers/StandardComparisonCalculator.cs` — `CalculatePreparationRsd`; `HplcSstEvaluator.cs` — `Evaluate`; `TitrationEngine.cs` — `Evaluate`, `Summarize`. The working standard qualification rounds the RSD to 2 decimals with `MidpointRounding.AwayFromZero` and then tests `<=` 2.0. Location: `backend/MicroLIMS.Application/Helpers/WorkingStandardCalculator.cs` — `PassesRsd`. The measurement calculator stores the RSD rounded to 10 decimals and gates nothing. Location: `MeasurementCalculator.cs` — `Calculate`. The titration series RSD divides by the absolute value of the mean (`TitrationEngine.Summarize`); the other methods divide by the mean as signed. The same sample-standard-deviation formula (n minus 1) is written out separately in five methods that were read (standard comparison, working standard, measurement, and two in the titration engine).
- **What the other source says:** No approved specification line in `documents/` was found on how an RSD is rounded or compared. CONF-099 asks the rounding question for the working standard only.
- **Question:** Should every RSD gate compare the same value (rounded to the same number of decimals, or unrounded) with the same operator, and should one shared routine compute it?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-181
- **Area:** PHY
- **Status:** Open
- **What the code does:** The number of decimals of a reported value, and the rounding of a dried-basis conversion, differ by result type. Reported decimals: titration uses the larger of 2 and the decimals of lower limit, upper limit, target and tolerance; UV and outsourced numeric results use the larger of 2 and the decimals of lower and upper limit only; the gravimetric display uses the larger of 2 and the decimals of target, tolerance and both limits; the measurement display uses the decimals of the readings as typed and of the same four fields, with no minimum; the HPLC assay text uses 1 decimal; related substances 2; cleaning residue 2; residual solvent 1; ICP 1 (percent) and 4 (ppm); dissolution 0. The dried-basis conversion `x * 100 / (100 minus moisture)` is rounded to 2 decimals inside the HPLC conversion (which the UV recorder also uses, and then rounds again), is not rounded in titration, and is rounded to 3 decimals for working standard potency. Location: `backend/MicroLIMS.Application/Workflows/TestWorkflow/TitrationRecorder.cs` — `RecordCoreAsync`; `UvRecorder.cs` — `RecordCoreAsync`; `OutsourcedRecorder.cs` — `RecordCoreAsync`; `backend/MicroLIMS.Application/Helpers/GravimetricCalculator.cs` — `FormatReportedDisplay`; `MeasurementCalculator.cs` — `FormatReportedDisplay`; `HplcAssayCalculator.cs` — `ApplyBasis`; `TitrationEngine.cs` — `ApplyBasis`; `WorkingStandardCalculator.cs` — `DriedBasisPotency`.
- **What the other source says:** No approved specification line in `documents/` was found on reported precision or on the precision of a dried-basis conversion. CONF-091 and CONF-095 ask whether the value is compared before or after rounding; this entry is about the precision itself.
- **Question:** Which precision applies to each result type, should it come from the specification or test configuration for all of them, and should a dried-basis conversion be rounded the same way everywhere?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-182
- **Area:** RPT
- **Status:** Open
- **What the code does:** The control chart statistics use numbers written in the code that the Section Head cannot configure: the bias constants D2, D3 and D4 for moving-range spans 2 to 10, the run lengths and sigma multiples of the eight special-cause rules, the factors 6 and 3 of the capability indices, the 1e-9 tolerance of whole-number rounding, and the report wording thresholds 1.33 ("capable") and 1.0 ("marginal"). Only the span, method, k values, percentiles, rounding mode, minimum points and the list of enabled rules are settings. Location: `backend/MicroLIMS.Application/Services/Spc/ControlChartCalculator.cs` — `Compute`, `EvaluateRules`, `Capability`, `Round`; `backend/MicroLIMS.Application/Services/Spc/ControlChartReportService.cs` — `Rating`.
- **What the other source says:** `documents/URS/URS-RPT.md` URS-RPT-008 (requirement on computing control chart limits, rule flags and capability on the server) does not give these numbers. No approved specification line was found for them. `CLAUDE.md` Principle 1 says item-specific limits come from configuration; whether statistical constants count as such is not stated.
- **Question:** Are these statistical constants and the capability thresholds approved site rules, and which of them, if any, should be configurable?
- **Decision:** Not decided.
- **Date:** Not applicable
