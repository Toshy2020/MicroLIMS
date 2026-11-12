# FS-MIC Functional Specification

| Field | Value |
|---|---|
| Document number | FS-MIC |
| Title | Microbiology enumeration and incubation functional specification |
| Version | 0.1 |
| Status | Draft |
| Author | Claude (AI agent), draft from code |
| Reviewer | To be assigned by QA |
| Approver | To be assigned by QA |
| Effective date | Set when Approved |

## Change history

| Version | Date | Author | Change |
|---|---|---|---|
| 0.1 | 2026-11-12 | Claude (AI agent), draft from code | First draft, written from the code as built. |

## Scope and reading notes

- This document covers the media selection, incubation, plate-count, batch-count and limit-comparison functions that sit in `TestWorkflowController` and `IncubationStepRecorder`. Pathogen steps are in FS-PAT. Physicochemical, outsourced and instrument endpoints that share `TestWorkflowController` belong to other areas and are not described here.
- Every statement was written from the code files named in each entry. Nothing was run. Pharmacopoeia chapters are pointers only (to be confirmed by QA).
- Limits, dilution factors, incubation hours and temperatures are not in the code. They are configured by the Section Head (Test Master, item Specifications, sampling configuration).
- None of the methods in this document checks a record version (`If-Match`) or calls `RecordVersion.EnsureCurrent`. Where a method uses a database transaction this is stated in the entry.

## Functions

### FS-MIC-001
**Satisfies:** URS-MIC-001
**Function:** Every endpoint of `TestWorkflowController` needs the permission `TestWorkflowExecute`. Most endpoints that take a test order ID then call `IUserSectionScopeService.EnsureTestOrderAccessAsync`, which refuses (UnauthorizedAccessException) when the test order belongs to a laboratory section that the user is not assigned to. Read endpoints ask for read access, write endpoints for write access. For read access a user who holds the permission `Sections.ViewAll` may read every section; for write access scope is always the user's member sections, for every role. If the test order does not exist the scope check returns without error. `WorkflowStepException` raised by the engine is returned as HTTP 400 with the error code as the first error and, for incubation locks, the remaining seconds as the second. The `close-incubation-window`, `record-result`, batch and media endpoints in this document all make the scope check before calling the engine. `override-minimum-duration` needs the extra permission `TestWorkflowSupervise`. No endpoint here checks that the caller is the analyst assigned to the test order (`AssignedAnalystId`).
**Code:** `backend/MicroLIMS.API/Controllers/TestWorkflowController.cs` — `RunAsync`, `SelectMedia`, `RecordResult`
**Verified by:** reading (not run)

### FS-MIC-002
**Satisfies:** URS-MIC-002, URS-MIC-004, URS-MIC-005
**Function:** Select media and start an incubation. Inputs: test order ID, step name, media lot ID, incubator equipment ID. The method runs in one database transaction (`UnitOfWork.RunAsync`). Rules in order: (1) the step must be in the test's configured workflow; (2) all lower-numbered steps must be done (see FS-MIC-011) and the step must be the first incomplete one; (3) the sample preparation gate must pass (FS-MIC-011); (4) no incubation for the same step may already be open; (5) the lot must exist, be flagged `IsReleasedForUse`, and have a status other than OutOfStock or QuarantineFailed; (6) the lot must have been prepared from material of the same laboratory section as the test order (`SectionMediaRule.EnsureLot`); (7) for broth steps at least one medium must be configured; (8) the lot's media product must match one of the step's configured media; (9) the matched medium must have a configured window (minimum hours above 0, maximum not below minimum, temperature maximum above 0 and not below minimum) or the method refuses with an error code; (10) the incubator must be an Incubator-type equipment whose set point lies inside the medium's temperature range and whose calibration due date is empty or not in the past (FS-MIC-003); (11) for a shared enrichment broth step, every other test of the sample that would join must also have a configured window and an in-range incubator. Result: an `Incubation` row is added with the start time from the injected clock (never from the client), the window text, `IncubationEndUtc` and `ExpectedReadingAt` equal to start plus the medium's maximum hours, and the user ID. The sample moves from Received to InTesting. A test order in Waiting moves to Incubating through `WorkflowStateMachine.TransitionAsync`, which writes a `WorkflowHistory` row. The window cannot be changed by the analyst. No check is made in this method of the media lot's expiry date and a lot with status Expired or Destroyed is not refused by the status test in (5). This method writes no explicit audit row; it was not checked here whether the context's automatic capture covers these tables.
**Code:** `backend/MicroLIMS.Application/Workflows/TestWorkflow/IncubationStepRecorder.cs` — `SelectMediaAsync`, `SelectMediaCoreAsync`
**Verified by:** reading (not run)
**Conflicts:** CONF-013, CONF-026

### FS-MIC-003
**Satisfies:** URS-MIC-003
**Function:** Eligible incubators for a step medium. Inputs: step medium ID. Rule: equipment of type Incubator with a set point that is not empty and lies between the medium's minimum and maximum temperature (both ends included) and whose calibration due date is empty or not earlier than now (injected `TimeProvider`). Equipment with no calibration due date therefore counts as in calibration (decision recorded under CONF-003). The list is returned with the label "Current" for every row. `IsWithinRangeAsync` returns true when the equipment is in that list. The list does not look at whether the equipment is active, retired or in a particular laboratory section. Used by media selection, the shared broth start and the confirmatory setup. The stage-2 transfer method does not call it (FS-MIC-004).
**Code:** `backend/MicroLIMS.Application/Services/IncubatorEligibilityService.cs` — `GetEligibleIncubatorsAsync`, `IsWithinRangeAsync`
**Verified by:** reading (not run)
**Conflicts:** CONF-003

### FS-MIC-004
**Satisfies:** URS-MIC-006
**Function:** Start stage 2 of a two-stage incubation. Allowed only for a plate-count step that is configured with an incubation transfer. Inputs: test order ID, step name, new incubator ID. Preconditions: an open stage 1 incubation exists for the step; stage 1 has run for at least its medium's minimum hours unless a user has overridden the minimum duration (FS-MIC-009); a stage 2 configuration exists for the step. Result: the stage 1 incubation is closed (completed time and user, outcome text "Transferred to stage 2 incubation."), and a new incubation row is added with stage number 2, a link to the stage 1 row, the same media lot, the new incubator, start from the injected clock, and end equal to start plus the configured stage 2 maximum hours. The declared window is checked to end after it starts and to last at least the configured stage 2 minimum hours. The method does not call the incubator eligibility check, so the new incubator's set point and calibration are not checked against the stage 2 temperature. The method does not use a database transaction wrapper (`UnitOfWork`); it saves once at the end. No scope check is made inside the method; the controller makes it.
**Code:** `backend/MicroLIMS.Application/Workflows/TestWorkflow/IncubationStepRecorder.cs` — `StartStage2IncubationAsync`
**Calculations:** CALC-009
**Verified by:** reading (not run)

### FS-MIC-005
**Satisfies:** URS-MIC-007, URS-MIC-008, URS-MIC-009
**Function:** Record a plate-count result (endpoint `record-result`). Inputs: step name, plate readings as text (or decimals), dilution factor (default 1 in the controller when missing), optional override note. Preconditions: the order has no sample locations (batch orders use FS-MIC-008); the step type is plate count; an open incubation exists for the step; the test's workflow type is count test. If the step has an incubation transfer, stage 2 must have been started and the incubation end time (start plus maximum hours) must have passed unless the minimum duration was overridden. If the step has no transfer, this method makes no check that any incubation time has elapsed. Rule as coded: for samples in category Water, EnvironmentalMonitoring or AfterCleaning the dilution factor is forced to 1. For other samples that have an item, the dilution factor is read from the item's specification for the test (`SpecificationLookup.PrimaryAsync`); the method refuses when none is configured, refuses when the specification has no reporting unit, and when the entered factor differs from the configured one it requires a non-blank override note. Readings equal to TNTC or Uncountable (any letter case) make the result status RequiresReview, the reported result the word TNTC or Uncountable, and the average and calculated values empty. Otherwise each reading is parsed as a decimal, the average is the arithmetic mean of the readings, and the calculated result is the average multiplied by the dilution factor. The reported text is the calculated result rounded to a whole number with `MidpointRounding.AwayFromZero` followed by the unit; when the calculated result is below the dilution factor the text is "<" followed by the dilution factor (rounded the same way) and the unit. The unit is the specification unit (or sampling-configuration unit for water points), prefixed with "CFU/" when it does not already start with it, otherwise a default from the sample category. The status comes from FS-MIC-006 using the limits of the item's specification (or the water sampling configuration). A sample with neither an item nor a water point gets no limits. The full-precision average and calculated values, the configured and entered dilution factors, the override flag and note, and a copy of the three limit texts are stored in a `CountTestReading` row; a `Result` row is also added; the incubation is closed. The method does not check that readings are whole numbers or non-negative and does not check that the dilution factor is above zero. A reading that is neither a number nor TNTC/Uncountable makes the number parse throw, and the controller does not catch that exception. No password or signature is asked for. The method runs in one transaction. On the final step the order moves to Ready, the result is projected to the reporting table, and `AutoSubmitForReviewIfReadyAsync` is called (FS-REV-003).
**Code:** `backend/MicroLIMS.Application/Workflows/TestWorkflow/IncubationStepRecorder.cs` — `RecordResultCoreAsync`, `RecordCountTestAsync`, `GetCfuUnit`
**Calculations:** CALC-001, CALC-002, CALC-003, CALC-004
**Verified by:** reading (not run)
**Conflicts:** CONF-010, CONF-011, CONF-027, CONF-029

### FS-MIC-006
**Satisfies:** URS-MIC-010
**Function:** Parse limit text and compare a value with alert, action and specification limits. A limit text is parsed in this order: a plain number accepted by the decimal parser (including thousands separators) is an upper bound; "target ± tolerance" (also written +/- or +-), with the tolerance optionally a percentage of the target; a range "min-max" using hyphen, en dash, em dash, horizontal bar or minus sign (the range is refused when min is above max); "NMT value" (upper bound); "NLT value" (lower bound); a plain number with a unit. Text that does not match, or is empty, gives no limit and no error. A limit is exceeded when the value is below the minimum or above the maximum; a value equal to a limit is not exceeded. `Compare` checks specification first (status OutOfSpecification), then action (ActionLimitExceeded), then alert (AlertLimitExceeded), returning the first that is exceeded. When none of the three texts gives a limit the status is LimitsNotConfigured; otherwise WithinLimits. The unit text found in a limit is parsed but `Compare` does not use it.
**Code:** `backend/MicroLIMS.Application/Services/SpecLimitParser.cs` — `Parse`, `Compare`, `CompareAgainstLimits`
**Calculations:** CALC-012
**Verified by:** reading (not run)
**Conflicts:** CONF-012

### FS-MIC-007
**Satisfies:** URS-MIC-010
**Function:** Evaluate a numeric value against a `Specification` row by its limit type. Range: refuses with LimitsNotConfigured when neither limit is set; a value below the lower limit is OutOfSpecification (equal to the lower limit is out only when the row's lower limit is marked not inclusive); same for the upper limit with its own inclusive flag. NotMoreThan and NotLessThan: same rule with one limit. TargetWithTolerance: the tolerance is the stored tolerance, or the target times the tolerance divided by 100 when the tolerance mode is Percent; the value is out when below target minus tolerance or above target plus tolerance (both ends included). CountTiered: calls the text comparison in FS-MIC-006 with the row's alert, action and specification texts. Qualitative, PresenceAbsence, MultiStage, DissolutionQ, DisintegrationTime and WeightVariation throw a business-rule error because they are not numeric. An unknown limit type throws. The method returns a status only; the Range, NotMoreThan, NotLessThan and TargetWithTolerance branches never return AlertLimitExceeded or ActionLimitExceeded. This method is called by physicochemical and instrument calculators; those calls are described in their own areas.
**Code:** `backend/MicroLIMS.Application/Services/SpecificationEvaluator.cs` — `Evaluate`
**Calculations:** CALC-013, CALC-014, CALC-015
**Verified by:** reading (not run)
**Conflicts:** CONF-012

### FS-MIC-008
**Satisfies:** URS-MIC-011
**Function:** Record counts for a batch order, one set of readings per sample location (endpoints `batch-results` and `water-batch-readings`). Preconditions: the order is at Incubating; an open incubation exists and its step is the final window (or a transfer step); for a transfer step stage 2 must be running and its minimum hours (the stage 2 configuration, or the medium's minimum when none) must have passed; otherwise the medium's minimum hours must have passed (an override by a user skips the check); every location of the order must have a submission with at least one reading. Per location: the average of the readings is the result (dilution factor 1). `batch-results` (EM and After Cleaning) takes limits from the room test configuration or machine part configuration and writes the reported text "<1" when the average is below 1, otherwise the average rounded to a whole number (`AwayFromZero`). `water-batch-readings` takes limits from the sampling configuration and writes the average rounded to a whole number with no "<1" rule. Both compare with FS-MIC-006, store the three limit texts on the location, take the unit from configuration or derive it from the test type (the derivation throws when it has no mapping), and set entered time and user. One `Result` row summarises the order ("n locations: x conform, y alert/action/spec") and the worst status; "conform" means status WithinLimits. The incubation closes, each location is projected, the order moves to Ready and `AutoSubmitForReviewIfReadyAsync` is called, all in one transaction. The readings are numbers only here; there is no TNTC handling and no whole-number or negative check.
**Code:** `backend/MicroLIMS.Application/Workflows/TestWorkflow/IncubationStepRecorder.cs` — `RecordBatchResultsCoreAsync`, `RecordWaterBatchReadingsCoreAsync`, `DeriveBatchLocationUnit`
**Calculations:** CALC-005, CALC-007
**Verified by:** reading (not run)
**Conflicts:** CONF-010, CONF-011

### FS-MIC-009
**Satisfies:** URS-MIC-012
**Function:** Close a window and override the minimum duration. `close-incubation-window` closes the open window of a multi-window batch order after the medium's minimum hours (not allowed on a transfer step or on the final window); it records completed time and user. `override-minimum-duration` (permission `TestWorkflowSupervise`) sets the overriding user ID and time on the order's open incubation; later minimum-duration and end-time checks in the engine then pass for that incubation. The override takes no reason, no password and no signature, and it does not change the recorded window or temperature. The override method does not check that the caller is not the analyst of the test. No transaction wrapper is used.
**Code:** `backend/MicroLIMS.Application/Workflows/TestWorkflow/IncubationStepRecorder.cs` — `CloseCurrentIncubationWindowAsync`, `OverrideMinimumDurationAsync`
**Calculations:** CALC-008
**Verified by:** reading (not run)

### FS-MIC-010
**Satisfies:** URS-MIC-011
**Function:** Record a growth observation per location for a batch order whose last step is a presence/absence step (endpoint `batch-pathogen-results`). Preconditions: order at Incubating, open window is the final step, minimum hours passed (or overridden), every location has an observation. Each location gets status Detected when growth was observed and Absent otherwise, and the text of the status as reported result. One `Result` row records the counts and overall status Detected when at least one location has growth. The incubation closes, locations are projected, the order moves to Ready and the sample review hook is called, in one transaction. The method records growth yes/no only; it does not apply any confirmation step.
**Code:** `backend/MicroLIMS.Application/Workflows/TestWorkflow/IncubationStepRecorder.cs` — `RecordBatchPathogenResultsCoreAsync`
**Verified by:** reading (not run)

### FS-MIC-011
**Satisfies:** URS-MIC-005
**Function:** Step order and preparation gate used by media selection and by the pathogen start methods. `FindFirstIncompleteStep` returns the lowest-numbered configured step that is not done. `CheckPredecessorStepsReadyAsync` looks at every lower-numbered step: if one of its incubations is still open the call fails with `PredecessorStepIncubationActiveException` (carrying the seconds left to the incubation end, or 0 after an override); if a step is not done the call fails with a "workflow order violation" error. `RequireSamplePreparedAsync` passes at once for orders that skip preparation; otherwise the sample's preparation status must be Ready and, for samples with an item, a sample preparation record must exist. When the gate fails the method writes an `AuditLog` row (action TestStartRefused, user, sample, test order, reason) and a `WorkflowHistory` row, saves them, and then throws a `WorkflowStepException` with code PreparationNotConfirmed. These two rows are saved before the exception is thrown. The callers `SelectMediaAsync` and `StartSelectivePlatingIncubationAsync` run inside `UnitOfWork.RunAsync`, which on an exception rolls the transaction back and re-writes only refused electronic-signature audit records. By reading the code, the TestStartRefused rows are therefore rolled back on a relational database. This was not run.
**Code:** `backend/MicroLIMS.Application/Workflows/TestWorkflow/TestWorkflowSupport.cs` — `CheckPredecessorStepsReadyAsync`, `RequireSamplePreparedAsync`, `FindFirstIncompleteStep`
**Verified by:** reading (not run)
**Conflicts:** CONF-026
