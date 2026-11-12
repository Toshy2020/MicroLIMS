# Conflicts raised by Task 3 (areas MIC, PAT, REV)

IDs CONF-010 to CONF-029 belong to this file. The orchestrator copies them into `documents/Conflicts.md`. Each entry states what the code does and what the other source says. No entry takes a side.

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
