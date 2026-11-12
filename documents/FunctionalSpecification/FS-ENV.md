# FS-ENV Functional Specification

| Field | Value |
|---|---|
| Document number | FS-ENV |
| Title | Environmental monitoring functional specification |
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

- This document covers `EMController` (route `api/em`), `EMService` and `EMWorkflowEngine`: receipt, preparation (room selection) and the engine's own advance and validate methods. Recording of room counts and growth observations is done by `IncubationStepRecorder` and is described in FS-MIC-008, FS-MIC-006 and FS-MIC-010; FS-ENV-003 only adds what is specific to environmental monitoring.
- Every statement was written from the code files named in each entry. Nothing was run. Pharmacopoeia and guideline chapters are pointers only (to be confirmed by QA).
- Departments, rooms, room test configurations (test code, test type, unit, alert, action and specification limit texts) are configured by the Section Head. No limit is in the code.
- No method in this document calls `RecordVersion.EnsureCurrent` (no stale-form version check). `EMService` is a thin wrapper.
- None of the methods here writes an explicit audit row. Whether the context's automatic audit capture covers the rows they change was not checked in this task.
- The business rules file describes an automatic out-of-trend rule for environmental monitoring. No code that sets the out-of-trend flag was found (FS-ENV-003, CONF-067).

## Functions

### FS-ENV-001
**Satisfies:** URS-ENV-001, URS-ENV-002
**Function:** Receive an environmental monitoring sample (`POST api/em/receive`). Permission: `Samples.ReceiveOwnLab`. Inputs: department ID, cause of testing ID, sampled by, control number, optional sampled-at time. The received-by user is the caller. `EMWorkflowEngine.ReceiveAsync` refuses when the department does not exist or is inactive. The sample is created with category EnvironmentalMonitoring, status Received and preparation status NeedsPreparation. Unlike water and after-cleaning receipt, no laboratory section is resolved or stored on the sample and no laboratory-membership check is made in this method. The method does not check the cause of testing ID, sampled-by or control number (no blank or existence check). A sampling time after the receipt time is refused before anything is saved. The reference number is made by `ReferenceNumberGenerator` (FS-RCV-003). The save uses the unique index on the reference number: if the first save fails on it, one new number is generated and the save is tried once more; if that also fails the method refuses with a message that nothing was saved. No test orders are created at receipt.
**Code:** `backend/MicroLIMS.Application/Workflows/EMWorkflowEngine.cs` — `ReceiveAsync`
**Verified by:** reading (not run)
**Conflicts:** CONF-060

### FS-ENV-002
**Satisfies:** URS-ENV-003
**Function:** Prepare an environmental monitoring sample (`POST api/em/prepare`; checklist screen). Permission: `Samples.ReceiveOwnLab`. Inputs: sample ID, room test configuration IDs, optional monitoring occurrence IDs. The call is wrapped in a transaction (`UnitOfWork.RunAsync`) only when occurrence IDs are sent. Rules in order: the sample must exist; its preparation status must be NeedsPreparation; at least one configuration must be selected; every ID must exist; each configuration and its room must be active; each room must belong to the sample's department. The test sections of the configured test codes are looked up (`TestSectionLookup.ResolveAsync`) and `EnsureSectionsAccessAsync` is called for the caller. That method passes when the caller is a member of at least one of the sections in the list, so a caller who belongs to only one of several sections involved is not refused. One `TestOrder` is created per distinct test code (Pending, Waiting, section from the lookup, assigned analyst set to the preparing user without applying `AnalystAssignmentRules`) and one `SampleLocation` (type Room) per chosen configuration, linked to its test order. If occurrence IDs are sent, `MonitoringLinkService.LinkAsync` is called (FS-MON-004). The preparation status becomes Ready and the sample is saved. The controller makes no sample-scope call.
**Code:** `backend/MicroLIMS.Application/Workflows/EMWorkflowEngine.cs` — `PrepareAsync`, `PrepareCoreAsync`
**Verified by:** reading (not run)
**Conflicts:** CONF-061, CONF-067

### FS-ENV-003
**Satisfies:** URS-ENV-004
**Function:** What is specific to environmental monitoring in batch count entry (the full rule is FS-MIC-008, endpoint `batch-results`, `IncubationStepRecorder.RecordBatchResultsCoreAsync`). Per room location: the readings are averaged; the dilution factor is 1; the reported text is "<1" when the average is below 1, otherwise the average rounded to a whole number (`MidpointRounding.AwayFromZero`). The alert, action and specification texts are taken from the location's room test configuration (or the machine part configuration when there is none) and compared as in FS-MIC-006, so the most severe limit exceeded wins. The unit is the configured unit or, if empty, derived from the room test type by `DeriveBatchLocationUnit`; the derivation throws a business-rule error for a type it has no mapping for. The limit texts are copied onto the location at entry. For a room test of presence/absence type, growth is recorded per location by `RecordBatchPathogenResultsCoreAsync` (FS-MIC-010). The business rules file (section 5) describes a rule that sets the out-of-trend flag when the sum of the step 1 and step 2 counts is above the action limit. In the code read, `RoomMonitoring.IsOutOfTrend` is declared in the entity and in a DTO, but no method in the Application or API projects assigns it, and no step 1 / step 2 counting exists in this engine. The status compared here is the limit status only.
**Code:** `backend/MicroLIMS.Application/Workflows/TestWorkflow/IncubationStepRecorder.cs` — `RecordBatchResultsCoreAsync`, `DeriveBatchLocationUnit`
**Verified by:** reading (not run)
**Conflicts:** CONF-006, CONF-010, CONF-011, CONF-067

### FS-ENV-004
**Satisfies:** URS-ENV-005
**Function:** Advance and validate for an environmental monitoring test order (`EMWorkflowEngine.AdvanceAsync`, `ValidateAsync`). `ValidateAsync` returns the error "Batch results must be entered for every location before this test order can proceed." when the order is at Waiting, Running or Incubating. `AdvanceAsync` loads the order, refuses with the validation errors, then moves Ready to Reviewed and Reviewed to Approved through `WorkflowStateMachine.TransitionAsync`; for any other step it leaves the step unchanged (and still writes a transition row to the same step). The method does not check who reviews or approves, so it makes no segregation-of-duties check; the review and approval rules are in FS-REV. `IsCompleteAsync` is true when the step is Approved. No stale-form version check. The state machine refuses a superseded order or an order whose status is closed.
**Code:** `backend/MicroLIMS.Application/Workflows/EMWorkflowEngine.cs` — `AdvanceAsync`, `ValidateAsync`
**Verified by:** reading (not run)
