# FS-EQP Functional Specification

| Field | Value |
|---|---|
| Document number | FS-EQP |
| Title | Equipment configuration and calibration plan functional specification |
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

- This document covers the equipment configuration list (`Equipment`, used by workflows that select equipment), incubator set points, autoclave programmes, the link from the inventory register to that list, and the calibration and maintenance plan for inventory equipment (`EquipmentInventory`): schedules, yearly plan revisions, calibration records, maintenance log and the overdue scan. The inventory register itself is in FS-INV.
- Not read, and not described: `EquipmentCalendarController` and its services, `EquipmentDocumentController` and `EquipmentDocumentService` (calibration certificates), `ChromatographyColumnService` (column master on the same controller), and `CalendarEventService`.
- Two records exist for one physical asset: `EquipmentInventory` (the register, with status and the calibration plan) and `Equipment` (the configuration list, with the set point, used by workflows). They are matched by code text, ignoring letter case. This matters for the calibration rule; see FS-EQP-012 and CONF-126.
- Calibration rule as coded (decision CONF-003, user, 2026-11-11: equipment with no calibration due date counts as in calibration). The code has no single method named for it. The places that decide "in calibration" or "overdue" are listed in FS-EQP-012. The sentence "an item with no due date counts as in calibration" holds in every place that checks a due date; the places differ in how they compare the date.
- Every statement was written from the code files named in each entry. Nothing was run.
- `Equipment`, `EquipmentInventory`, `EquipmentPlanRevision` and `EquipmentTask` have a version column. Where a method calls `RecordVersion.EnsureCurrent` this is stated. `RecordVersion.EnsureCurrent` compares versions only when an expected version was supplied with the request (the If-Match header); without it, no stale-form check is made. Audit rows for inserts and updates come from the automatic capture in `MicroLimsDbContext`; none of these entities is on the exclusion list.

## Functions

### FS-EQP-001
**Satisfies:** URS-EQP-001
**Function:** Access to the configuration list. `EquipmentMasterDataController` needs an authenticated user. Reads (`GET equipment`, by ID, set-point history, autoclave programmes) are filtered by `GetReadableSectionIdsAsync` or call `EnsureEquipmentAccessAsync` with read access; `GET equipment/configured-summary`, `GET equipment/autoclave-programs/all` and `GET autoclave-programs/{id}/history` make no section filter and return every section. Writes need `MasterData.Manage`. Create resolves the section with `ResolveSectionForCreateAsync`. Update, set point and save-programme-for-an-autoclave call `EnsureEquipmentAccessAsync` (write). The programme update (`PUT autoclave-programs/{programId}`) and the programme status change (`PUT autoclave-programs/{programId}/status`) make no section check and take the autoclave from the request body or the stored programme: any holder of `MasterData.Manage` can change programmes of any section. `LinkInventory` makes no section check.
**Code:** `backend/MicroLIMS.API/Controllers/EquipmentMasterDataController.cs` — `GetEquipment`, `GetConfiguredSummary`, `UpdateSetPoint`, `SaveAutoclaveProgram`, `UpdateAutoclaveProgram`, `SetAutoclaveProgramStatus`, `LinkInventory`
**Verified by:** reading (not run)

### FS-EQP-002
**Satisfies:** URS-EQP-001, URS-EQP-002
**Function:** Create and edit a configuration item. Inputs: name, code, type, location, set point, calibration due date, vendor, CDS software, connection settings, section. Rules: name and code are required and trimmed; the code must be unique (exact text match); vendor at most 100 characters; for chromatography (HPLC) and ICP-OES types a CDS software value is required, and for every other type it is refused. Create resolves the section with `ResolveSectionForCreateAsync`. Update needs write access, calls `RecordVersion.EnsureCurrent`, and may move the item to another section only through `ResolveSectionForCreateAsync` for the section sent. The set point and calibration due date are overwritten with the values sent. No reason is asked, no password is asked, no calibration evidence is required, and no range is checked on the set point. A set point or due date changed through this method is not written to the set-point history (FS-EQP-003). Changes are audited by the automatic capture.
**Code:** `backend/MicroLIMS.Application/Services/MasterData/EquipmentMasterDataService.cs` — `CreateEquipmentAsync`, `UpdateEquipmentAsync`
**Verified by:** reading (not run)
**Conflicts:** CONF-126

### FS-EQP-003
**Satisfies:** URS-EQP-003
**Function:** Change an incubator set point. Needs `MasterData.Manage` and write section access. Inputs: new set point and reason. The reason is required. The item must be of type Incubator. The previous set point (zero is recorded when the item had none) and the new one are stored in a history row with the reason, the user and the injected-clock time, and the item is updated, in one save. No range is checked on the new set point and no stale-form version check is made. Workflows that select an incubator read this set point (FS-MIC-003 describes the check that uses it). The history is read through `GetIncubatorSetPointHistoryAsync` (read access).
**Code:** `backend/MicroLIMS.Application/Services/EquipmentConfigurationService.cs` — `UpdateIncubatorSetPointAsync`, `GetIncubatorSetPointHistoryAsync`
**Verified by:** reading (not run)

### FS-EQP-004
**Satisfies:** URS-EQP-004
**Function:** Autoclave programmes. Save: the equipment must exist and be of type Autoclave. Create: the programme code must not already exist on that autoclave (exact match, checked before trimming and upper-casing the saved value); the programme is saved with code upper-cased, trimmed name and load type, temperature, cycle time in minutes and active flag, and a history row with action Created is added (comment from the request or a default text). Update: `RecordVersion.EnsureCurrent` is called; a changed code must not exist on the autoclave; a history row with the previous and new name, load type, temperature, cycle time and active flag, comment (request or default text), user and time is added, and the programme is overwritten. Status change: no change is made if the flag is unchanged; otherwise a history row and the new flag; the comment may be empty. No range is checked on temperature or cycle time, and the programme values are not read by media preparation (FS-MED-002). Programme lists can be filtered to active only; the history list is read without a section check.
**Code:** `backend/MicroLIMS.Application/Services/EquipmentConfigurationService.cs` — `SaveAutoclaveProgramAsync`, `SetAutoclaveProgramStatusAsync`, `GetAutoclaveProgramsAsync`, `GetAutoclaveProgramHistoryAsync`
**Verified by:** reading (not run)

### FS-EQP-005
**Satisfies:** URS-EQP-005
**Function:** Link an inventory item to the configuration list. Needs `MasterData.Manage`; no section check. If a configuration item with the same code (ignoring letter case) exists, it is returned unchanged. Otherwise a configuration item is created from the register row: name is the instrument type text, location and calibration due date are copied, and the type is guessed from the instrument type text (contains the text for incubator, autoclave or cabinet; anything else becomes Other). A new incubator is given a set point of 32.5, a value written in the code. The new item is placed in the section whose code is `MICRO`; the method refuses if that section does not exist. No set-point history row is written. The user ID is not stored.
**Code:** `backend/MicroLIMS.Application/Services/EquipmentConfigurationService.cs` — `LinkInventoryEquipmentToMasterAsync`
**Verified by:** reading (not run)
**Conflicts:** CONF-125

### FS-EQP-006
**Satisfies:** URS-EQP-001
**Function:** Configured equipment summary. For every configuration item, in type then code order, the summary shows the calibration due date as the configuration item's date, or, if that is empty, the date of the matching register row. The status shown is the register row status, or "InService" when no register row matches. Location, serial number and manufacturer come from the register row where there is one. The number of autoclave programmes is shown. Read only. No section filter.
**Code:** `backend/MicroLIMS.Application/Services/EquipmentConfigurationService.cs` — `GetConfiguredEquipmentSummaryAsync`
**Verified by:** reading (not run)
**Conflicts:** CONF-126

### FS-EQP-007
**Satisfies:** URS-EQP-006
**Function:** Save the calibration and maintenance schedule of an inventory item (`PUT api/equipment-plan/instruments/{id}/schedule`). Needs `EquipmentPlan.Manage`. The If-Match header is read as a version number; when it is absent or not a number the version is zero and no stale-form check is made. Inputs: calibration interval value and unit, last calibrated on, maintenance interval value and unit, last maintenance on. Rules: value and unit of one interval are set together or both empty; weeks 1 to 104, months 1 to 60, years 1 to 5 (literal ranges in the code); the last calibration and last maintenance dates must not be after today's lab date; the last calibration date cannot be changed once any calibration task of the item is completed. The item is overwritten and an explicit audit row with the previous and new schedule text is added, besides the automatic capture. No section scope check is made in the controller or the method, and no electronic signature is asked.
**Code:** `backend/MicroLIMS.API/Controllers/EquipmentPlanController.cs` — `SaveSchedule`; `backend/MicroLIMS.Application/Services/EquipmentPlan/EquipmentBaselineService.cs` — `SaveScheduleAsync`, `ValidateScheduleRequest`
**Verified by:** reading (not run)

### FS-EQP-008
**Satisfies:** URS-EQP-007
**Function:** Schedule rule as coded (no calculation ID yet). Adding an interval: weeks add 7 days per week, months and years use calendar addition (a month-end clamps, for example 31 January plus one month is the last day of February). The first raw due date is the certificate's next-due date if there is one, otherwise the last actual date plus one interval. For a year, due dates are chained from the first raw date by whole intervals (each step counted from the first raw date so a month-end clamp does not drift) until the end of the year, and only dates from the plan start date to the end of the year are kept. A raw date that is not a working day moves to the previous working day, unless that day is before today or on or before the last actual date, in which case it moves to the next working day. Working weekdays and non-working calendar dates come from monitoring settings and the lab calendar. The interval value must be above zero or the projection throws. The text snapshot of an interval is "Weekly", "Monthly", "Yearly" for one, otherwise "Every n weeks/months/years".
**Code:** `backend/MicroLIMS.Application/Services/EquipmentPlan/EquipmentScheduleCalculator.cs` — `AddInterval`, `DueAfter`, `ProjectYear`, `Describe`
**Verified by:** reading (not run)

### FS-EQP-009
**Satisfies:** URS-EQP-007
**Function:** Generate a draft plan for a year (`POST api/equipment-plan/drafts`). Needs `EquipmentPlan.Manage`; no section scope. Input: year (2000 to 2100) and an optional go-live date. The year must not have ended; the go-live date must not be before today or the year start and must fall inside the year. The plan is built from every inventory item: retired items are listed as exceptions; items with the same manufacturer and a meaningful serial number as another item are not planned except the one with the lowest ID, and the others are listed as possible duplicates; items with no interval and no baseline date are listed as exceptions; an interval with no baseline (no completed task and no last date) is an exception ("No last calibration date" or "No last maintenance date"); a baseline date with no interval is an exception. Otherwise tasks are projected by FS-EQP-008 from the baseline (latest completed task date, else last date on the item). For calibration the certificate next-due date used is the item's calibration due date only if it is later than the baseline. Sequence numbers continue after the tasks already completed in that year. Only tasks whose type, item, year and sequence do not already exist in a non-draft revision are added to the draft; live tasks no longer in the plan are listed for retirement and live tasks with a different date for rescheduling. If the year already has an approved plan and nothing differs, the method refuses. An existing draft for the year is rebuilt (its tasks removed and re-created); otherwise a new revision with the next revision number is made. The generating user and time are stored. All in one transaction. The calibration due date of the item is the same field that is used in FS-EQP-012.
**Code:** `backend/MicroLIMS.Application/Services/EquipmentPlan/EquipmentPlanService.cs` — `GenerateDraftAsync`, `BuildPlanAsync`; `backend/MicroLIMS.API/Controllers/EquipmentPlanRevisionsController.cs` — `GenerateDraft`
**Verified by:** reading (not run)

### FS-EQP-010
**Satisfies:** URS-EQP-008
**Function:** Approve a plan revision (`POST revisions/{id}/approve`). Needs `EquipmentPlan.Approve`. Inputs: password and reason. The version is checked (`RecordVersion.EnsureCurrent`). Preconditions: the revision is a draft; segregation of duties: the approving user is not the user who generated the draft; if an approved revision already exists for the year, a reason is required; the plan is rebuilt and the set of tasks to be added must equal the draft's tasks, otherwise the method refuses and asks for a new draft. One transaction: the user's password is verified and an electronic signature with meaning EquipmentPlanApproved is written (signature rules are in FS-SIG); the revision becomes Approved with the reason, signature and time; the earlier approved revision becomes Superseded; tasks to retire are marked retired by this revision and tasks to reschedule get the new due date, each with a task event (Retired or Rescheduled) carrying the old and new date, the reason or a default text, the user, the time and the signature. Preview and revision list reads need `MonitoringPlan.View` and have no section filter.
**Code:** `backend/MicroLIMS.Application/Services/EquipmentPlan/EquipmentPlanService.cs` — `ApproveAsync`, `GetPreviewAsync`, `GetRevisionsAsync`; `backend/MicroLIMS.API/Controllers/EquipmentPlanRevisionsController.cs` — `ApproveRevision`
**Verified by:** reading (not run)

### FS-EQP-011
**Satisfies:** URS-EQP-009
**Function:** Record a calibration (`POST instruments/{id}/calibrations`). Needs `EquipmentWork.Record`; no section scope check. Inputs: calibration certificate document ID, calibrated-on date, optional certificate next-due date, password. Preconditions: the item exists; the certificate exists, belongs to the same item, is of type calibration certificate and has status Current; the calibrated-on date is not after today's lab date and is after the previous actual date (latest completed calibration task, or the item's last calibrated date if later); the certificate next-due date, if given, is after the calibrated-on date. One transaction: the earliest live open calibration task is completed with the date and the certificate, or, if there is none, an unplanned task is created already completed. The user's password is verified and an electronic signature with meaning CalibrationRecorded is written, and a task event Completed with the signature is added. If the item has a calibration interval, the next due date is computed with FS-EQP-008 (certificate date or date plus interval, moved to a working day), later open tasks are re-projected from it with Rescheduled events, and the item's calibration due date is set to the next due date at 00:00 UTC. If the item has no interval but the certificate gave a next-due date, that date is used. The same date is written to the configuration item with the same code (ignoring letter case), if there is one. If the item was taken out of service automatically, it returns to service with a history row. When neither an interval nor a certificate date exists, the calibration due date is left as it was. The certificate contents (dates on the document) are not compared with the dates entered. No second person reviews the record. No stale-form version check.
**Code:** `backend/MicroLIMS.Application/Services/EquipmentPlan/EquipmentWorkService.cs` — `RecordCalibrationAsync`; `backend/MicroLIMS.API/Controllers/EquipmentWorkController.cs` — `RecordCalibration`
**Verified by:** reading (not run)

### FS-EQP-012
**Satisfies:** URS-EQP-011, URS-EQP-012
**Function:** Where the code decides "in calibration" or "overdue" (decision CONF-003: no due date counts as in calibration). Each place is listed with how it compares. (1) Incubator offered for a medium: the configuration item must be an incubator with a set point in the medium's range and a due date that is empty or not before the current instant (`>= now`). It does not look at the register status. (2) Incubator used in the grouped media selection: refused when the due date is not empty and is before the current instant; also refused when the set point is empty; the set point range is not checked in this method. (3) HPLC and ICP availability: unavailable when the due date is before today's lab date ("Calibration overdue since ..."); then unavailable when the register row with the same code (ignoring letter case) has status OutOfService or Retired. (4) Register display flag: overdue when the due date's date part is before the date part of now (UTC). (5) Section equipment counters: counts non-retired register rows whose due date is before today's UTC date. (6) Automatic scan (FS-EQP-013): date part of the due date before today's lab date. (7) Planned-task overdue reminders count open calibration tasks with a due date before today; this is task-based and does not read the item's due date. (8) Display only: the configured summary (FS-EQP-006) and the calendar service show the due date. Places that use equipment but make no calibration check: media preparation autoclave (FS-MED-002), media evaluation incubator (FS-MED-006), cryovial identity-confirmation incubator (FS-CRY-002), media and cryovial storage equipment (status InService only), and stage-2 incubator transfer (FS-MIC-004). The due date can be changed directly through FS-INV-010 and FS-EQP-002 without a calibration record, and is set from a calibration record only by FS-EQP-011.
**Code:** `backend/MicroLIMS.Application/Services/IncubatorEligibilityService.cs` — `GetEligibleIncubatorsAsync`; `backend/MicroLIMS.Application/Services/GroupedTestAction/BatchSelectMediaRules.cs` — `EnsureIncubatorUsable`; `backend/MicroLIMS.Application/Services/HplcRunService.cs` — `DetermineUnavailableReasonAsync`; `backend/MicroLIMS.Application/Services/IcpRunService.cs` — `DetermineUnavailableReasonAsync`; `backend/MicroLIMS.Domain/Entities/EquipmentInventory.cs` — `IsCalibrationOverdueAt`; `backend/MicroLIMS.Application/Services/SectionCountersService.cs` — `EquipmentCardAsync`; `backend/MicroLIMS.Application/Services/EquipmentPlan/EquipmentWorkService.cs` — `ApplyOverdueAsync`
**Verified by:** reading (not run)
**Conflicts:** CONF-003, CONF-123, CONF-126, CONF-128

### FS-EQP-013
**Satisfies:** URS-EQP-011
**Function:** Overdue scan. A background worker (default on, every 60 minutes, first run 30 seconds after start; configured by `EquipmentOverdue:Enabled` and `EquipmentOverdue:IntervalMinutes`) calls `ApplyOverdueAsync` with today's lab date. Register rows with status InService and a due date whose date part is before today are set to OutOfService, with the automatic marker set to today, last-modified user empty (system), and a status-history row "Calibration overdue since ... (automatic)" with no user. All in one transaction. Only the register row changes: the configuration item (used by FS-EQP-012 place 1) is not touched. The marker is cleared and the status returned to InService by a recorded calibration (FS-EQP-011) or by a manual edit with a comment (FS-INV-010). A row set to OutOfService manually or already out of service is not touched.
**Code:** `backend/MicroLIMS.Application/Services/EquipmentPlan/EquipmentWorkService.cs` — `ApplyOverdueAsync`; `backend/MicroLIMS.API/BackgroundServices/EquipmentOverdueWorker.cs` — `ScanAsync`
**Verified by:** reading (not run)
**Conflicts:** CONF-126

### FS-EQP-014
**Satisfies:** URS-EQP-010
**Function:** Maintenance log. Add (`POST api/equipment/{id}/maintenance-log`): needs `EquipmentWork.Record`; no section scope check. Inputs: entry type, performed-on date, work done, parts replaced, findings, optional calendar event, password. Rules: work done is required; the date must not be after today's lab date; a calendar event, if given, must exist and, if tied to an item, belong to this item; a preventive maintenance entry is refused if a non-voided one exists for the same item and day; any other type is refused if a non-voided entry of the same type, item and day has the same work text (compared without surrounding spaces and ignoring letter case). One transaction: an ID is reserved, the user's password is verified and an electronic signature with meaning MaintenanceEntryRecorded is written with the work text, the entry is saved with the signature and the injected-clock time, and a preventive maintenance entry completes the earliest open preventive maintenance task or creates an unplanned completed one (the rest of that logic was not read in detail). Void (`POST api/maintenance-log/{entryId}/void`): needs `EquipmentWork.Record`; a reason is required; an already voided entry is refused; the password is verified and an electronic signature with meaning MaintenanceEntryVoided is written; the entry gets void time, user and reason (the original text stays). Voiding a preventive maintenance entry that has no later preventive entry re-opens the linked task. Any holder of the permission may void any entry, including their own or another user's; no check compares the voiding user with the author. The log read needs `MonitoringPlan.View` and checks only that the item exists.
**Code:** `backend/MicroLIMS.Application/Services/EquipmentPlan/EquipmentWorkService.cs` — `AddLogEntryAsync`, `VoidLogEntryAsync`, `GetMaintenanceLogAsync`; `backend/MicroLIMS.API/Controllers/EquipmentWorkController.cs` — `AddLogEntry`, `VoidLogEntry`
**Verified by:** reading (not run)
