# FS-WAT Functional Specification

| Field | Value |
|---|---|
| Document number | FS-WAT |
| Title | Water monitoring functional specification |
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

- This document covers `WaterController` (route `api/water`), `WaterService` and `WaterWorkflowEngine`: receipt, preparation, the single-point calculation and the daily report. The plate-count recording for water (endpoint `water-batch-readings`) is in `IncubationStepRecorder` and is described in FS-MIC-008 and FS-MIC-006; the entry FS-WAT-005 here only adds what is specific to water.
- Every statement was written from the code files named in each entry. Nothing was run. Pharmacopoeia chapters are pointers only (to be confirmed by QA).
- Sampling points, their test assignments, the alert, action and specification limit texts and the unit are configured by the Section Head (sampling configuration). No limit is in the code.
- No method in this document calls `RecordVersion.EnsureCurrent` (no stale-form version check). `WaterService` is a thin wrapper: it calls the engine and maps the result.
- None of the methods here writes an explicit audit row. Whether the context's automatic audit capture covers the rows they change was not checked in this task.

## Functions

### FS-WAT-001
**Satisfies:** URS-WAT-001, URS-WAT-002
**Function:** Receive a water sample (`POST api/water/receive`). Permission: `Samples.ReceiveOwnLab`. Inputs: water department ID, cause of testing ID, sample quantity, sampled by, control number, optional sampled-at time, optional laboratory section ID. The received-by user is the caller. `WaterWorkflowEngine.ReceiveAsync` refuses when the department does not exist or is inactive. The laboratory section is resolved with `IUserSectionScopeService.ResolveSectionForCreateAsync` (write access) when the scope service is present; otherwise the requested section, or the section with the code `LaboratoryCodes.Microbiology`. When the resolved section is the section whose code is `PhyschemAreas.SectionCode`, the caller must hold the physicochemical area for the Water category, otherwise `UnauthorizedAccessException` ("Water samples are received by the RM & PM area."). The sample is created with status Received and preparation status NeedsPreparation. If the sampling time is after the receipt time, the method refuses; this check is made after the sample object is created and before it is added, so nothing is saved. The method does not check the cause of testing ID, the sample quantity text or the control number (no blank check and no existence check in this method; no validator class is used for water receipt). No test orders are created at receipt (see FS-WAT-003).
**Code:** `backend/MicroLIMS.Application/Workflows/WaterWorkflowEngine.cs` — `ReceiveAsync`
**Verified by:** reading (not run)

### FS-WAT-002
**Satisfies:** URS-WAT-003
**Function:** Reference number for a water sample. The number is made by `ReferenceNumberGenerator.GenerateAsync` (rule in FS-RCV-003, category code for Water). The sample is added and saved with `TrySaveChangesAsync` on the unique index of the reference number. If the first save fails on that index, a new number is generated and the save is tried a second time. If the second save also fails on the index, the method throws a business-rule error stating that nothing was saved and the sample must be received again. The same retry is not present for item-based samples (FS-RCV-003), nor in the EM and After Cleaning receipts unless stated in FS-ENV-001 and FS-ACL-001.
**Code:** `backend/MicroLIMS.Application/Workflows/WaterWorkflowEngine.cs` — `ReceiveAsync`
**Verified by:** reading (not run)
**Conflicts:** CONF-060

### FS-WAT-003
**Satisfies:** URS-WAT-004, URS-WAT-005
**Function:** Prepare a water sample (`POST api/water/prepare`; checklist screen). Permission: `Samples.ReceiveOwnLab`. Inputs: sample ID, sampling point IDs, storage condition, storage time in hours, optional monitoring occurrence IDs. The call is wrapped in a transaction (`UnitOfWork.RunAsync`) only when occurrence IDs are sent; otherwise it is a single `SaveChangesAsync`. Rules in order: the sample must exist; its preparation status must be NeedsPreparation (otherwise "already been prepared"); at least one point must be selected; a storage condition must be present; if the condition is exactly "Refrigerator" a storage time must be present (no check that it is not negative, and any other text is accepted as a condition; the correction method accepts only Refrigerator or RoomTemperature, see FS-RCV-009); every point must exist, be active and belong to the sample's water department. The laboratory is the sample's lab section, or the Microbiology section when empty. The tests allowed are those assigned to the points (`AssignedTestCodes`, configured by the Section Head) whose test definition belongs to that laboratory; for the physicochemical section also only tests whose area includes the area for the Water category. If no test remains the method refuses. Section access is then checked with `EnsureSectionsAccessAsync` for the caller (when the scope service is present). One `TestOrder` is created per distinct allowed test code (Pending, Waiting) and one `SampleLocation` (type water sampling point) per point and test code, linked to the point's sampling configuration for that test when one exists. Each new test order has the preparing user as its assigned analyst; `AnalystAssignmentRules` is not applied to that assignment. Storage condition and time (time is cleared unless Refrigerator) are written to the sample only after all checks pass. If occurrence IDs are sent, `MonitoringLinkService.LinkAsync` is called (FS-MON-004). The preparation status becomes Ready. The controller makes no sample-scope call; the section check is inside the engine. A second call after the first succeeds is refused by the status test.
**Code:** `backend/MicroLIMS.Application/Workflows/WaterWorkflowEngine.cs` — `PrepareAsync`, `PrepareCoreAsync`
**Verified by:** reading (not run)
**Conflicts:** CONF-061, CONF-066

### FS-WAT-004
**Satisfies:** URS-WAT-006
**Function:** Limit comparison used for water counts. The comparison is `WaterWorkflowEngine.Compare`, which calls `SpecLimitParser.Compare` (rule in FS-MIC-006): specification is tested first, then action, then alert, and the first one exceeded is returned; when no limit text gives a limit the status is LimitsNotConfigured. The interface comment in the same file describes the order as Alert, Action, Specification "in ascending order of severity"; the code tests Specification first. Another method with the same body exists in `IncubationStepRecorder.Compare`.
**Code:** `backend/MicroLIMS.Application/Workflows/WaterWorkflowEngine.cs` — `Compare`
**Verified by:** reading (not run)
**Conflicts:** CONF-006, CONF-064

### FS-WAT-005
**Satisfies:** URS-WAT-006
**Function:** Water batch readings, specific points only (the full rule is FS-MIC-008). Endpoint `POST api/testworkflow/{id}/water-batch-readings`, recorded by `IncubationStepRecorder.RecordWaterBatchReadingsCoreAsync` in one transaction. For each sampling-point location the readings are averaged directly; there is no dilution factor and no TNTC handling. The limits are taken from the location's own sampling configuration (alert, action and specification texts) and compared as in FS-WAT-004. The reported result is the average rounded to a whole number with `MidpointRounding.AwayFromZero`; the full-precision average is stored in the location's calculated result and CFU result. There is no "<" rule for a low count (CONF-011). The unit is the configured unit or, if none, "CFU/mL" for a water sampling point location. The three limit texts are copied onto the location at entry. Permission and section scope are those of FS-MIC-001. The order, the minimum incubation hours and the closing of the window are as in FS-MIC-008.
**Code:** `backend/MicroLIMS.Application/Workflows/TestWorkflow/IncubationStepRecorder.cs` — `RecordWaterBatchReadingsCoreAsync`, `DeriveBatchLocationUnit`
**Calculations:** CALC-006
**Verified by:** reading (not run)
**Conflicts:** CONF-010, CONF-011

### FS-WAT-006
**Satisfies:** URS-WAT-007
**Function:** Single-point calculation (`POST api/water/calculate`). The endpoint needs only a logged-in user (no permission policy), then calls `EnsureTestOrderAccessAsync` (write scope). Inputs: test order ID, a list of decimal readings. `WaterWorkflowEngine.CalculateAndCompareAsync` refuses an empty list. It refuses if the test order has any sample location, which is the case for every order created by FS-WAT-003. For an order without locations it reads the sampling configuration for the order's test code and the sample's `WaterSamplingPointId`; in Application and API code no place was found that sets `Sample.WaterSamplingPointId`, and `ReceiveAsync` (FS-WAT-001) does not. With no point, the limits are all empty and the status is LimitsNotConfigured. The average is the arithmetic mean of the readings, not rounded. A `Result` row is added with the readings joined by commas and the text "average (status)" where the average is formatted with at most two decimals (`0.##`, using the current culture of the process); the entered-by user is not set on the row. The order is moved to Ready by `WorkflowStateMachine.TransitionAsync` for the order's assigned analyst (or user 0 when none). The method does not check the order's current step, so any step except a closed or superseded one is moved to Ready. `AutoSubmitForReviewIfReadyAsync` is not called. No explicit transaction is used; the result is added and saved by the state machine call. Readings are not checked for being whole numbers or non-negative.
**Code:** `backend/MicroLIMS.Application/Workflows/WaterWorkflowEngine.cs` — `CalculateAndCompareAsync`
**Calculations:** CALC-011
**Verified by:** reading (not run)
**Conflicts:** CONF-064

### FS-WAT-007
**Satisfies:** URS-WAT-008
**Function:** Daily water report (`GET api/water/daily-report?date=`). Permission: logged-in user only. The read scope of the user (`GetReadableSectionIdsAsync`; null means all sections) limits the test orders. The engine selects all `Result` rows whose entered date (date part of `EnteredAt`, as stored) equals the requested date and whose test order belongs to a Water sample, loads them into memory (no paging) and, for each, reads the average and the status back out of the text field `InterpretedValue` by splitting on brackets; a value that is not a number becomes 0 and a missing status becomes "Unknown". The "exceeded limit" field is always empty. The result rows written by the batch method (FS-WAT-005) have the text "n locations: ... (worst: status)" in that field, so for them the parsed average is 0 and the status text is "worst: status". The date is compared without conversion to the laboratory time zone.
**Code:** `backend/MicroLIMS.Application/Workflows/WaterWorkflowEngine.cs` — `GetDailyAggregateAsync`, `ParseAggregateResult`
**Verified by:** reading (not run)
**Conflicts:** CONF-065
