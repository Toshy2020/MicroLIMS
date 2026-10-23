# Equipment Calibration & Maintenance Plan Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Plan, sign, record and print calibration and preventive maintenance (PM) per instrument, keep a signed in-app maintenance logbook, add ad-hoc calendar events, and show all of it in the Monitoring Plan calendar.

**Architecture:** New Domain entities next to `EquipmentInventory` (the Equipment Register). A pure `EquipmentScheduleCalculator` projects due dates; `EquipmentPlanService` generates and signs yearly revisions modelled on `MonitoringPlanService`; `EquipmentWorkService` closes tasks (calibration with certificate, PM with logbook entry) and re-projects; a hosted worker sets overdue-calibration instruments Out of service. The frontend Monitoring Plan page reads a new equipment calendar endpoint and merges it into its grids.

**Tech Stack:** ASP.NET Core, EF Core + PostgreSQL (Npgsql), xUnit (InMemory + Postgres suite), React + TypeScript + MUI, Vitest + RTL.

**Spec:** `docs/superpowers/specs/2026-10-23-equipment-calibration-maintenance-plan-design.md`

## Global Constraints

- Clean Architecture: rules in `MicroLIMS.Application`; Domain has no EF/UI; frontend never enforces rules (CLAUDE.md).
- Nothing is hard-deleted: logbook entries, certificates, events, tasks, revisions are voided/cancelled/retired with a reason.
- Signed actions use `IElectronicSignatureService.SignAsync(userId, password, SignatureMeaning, entityName, entityId, reason, ip)`; new `SignatureMeaning` values are appended at the end of the enum (persisted as int).
- New enums persisted as int are append-only.
- Calibration baseline = a real last calibration date entered by a user; never derived.
- PM baseline = a typed last PM date; audited, not signed; printed as "Baseline", not as a logbook entry.
- Certificate next-due date, when given, wins over actual + interval.
- Recorder may sign their own calibration/logbook entry. Equipment plan approver must differ from `GeneratedByUserId` (new rule; the monitoring plan does not enforce this).
- Overdue calibration -> automatic Out of service; overdue PM -> warning only.
- Interval units: Weeks or Months, value 1..104 (weeks) / 1..60 (months).
- Builds/tests while the API runs: `dotnet test backend/MicroLIMS.Tests --artifacts-path E:/MicroLIMS/rls-tmp/eqplan`; migrations `dotnet ef migrations add <Name> --configuration Release`. All temp/artifacts on E: (never C:).
- Commit each tested task locally; never push or merge unasked.

## Review Focus

1. Instrument with an interval but no baseline date: excluded from the plan and listed as an exception "No last calibration date" / "No last maintenance date" (test in Task 4).
2. Due date on Fri/Sat or a lab non-working date whose previous working day is before today or before the last actual date: moves forward instead (test in Task 2).
3. Instrument manually set Out of service after the worker's automatic switch: attaching a certificate must NOT return it to service (test in Task 6).
4. Calibration recorded with an actual date in the future or before the previous actual date: rejected (test in Task 5).
5. Re-projection pushing tasks past 31 Dec: tasks beyond the year are not created in this year's plan; next year's generation starts from the last actual date (test in Task 2).

---

## File Structure

Backend (Domain):
- Create `backend/MicroLIMS.Domain/Enums/EquipmentPlanEnums.cs` — `IntervalUnit {Weeks, Months}`, `EquipmentTaskType {Calibration, PreventiveMaintenance}`, `EquipmentTaskEventType {Created, Rescheduled, Completed, Retired}`, `MaintenanceEntryType {PreventiveMaintenance, BreakdownRepair, Other}`, `CalendarEventType {VendorVisit, BreakdownRepair, Audit, ProficiencyTesting, Other}`, `CalendarEventStatus {Active, Cancelled}`.
- Create `backend/MicroLIMS.Domain/Entities/EquipmentPlanRevision.cs` — same shape as `MonitoringPlanRevision` (Year, RevisionNumber, Status `PlanRevisionStatus`, Reason, EffectiveFrom, GeneratedAt, GeneratedByUserId, ApprovalSignatureId, ApprovedAt).
- Create `backend/MicroLIMS.Domain/Entities/EquipmentTask.cs` — EquipmentInventoryId, TaskType, Year, Sequence, IsUnplanned (bool), CreatedByRevisionId?, RetiredByRevisionId?, OriginalDueDate (DateOnly, frozen), DueDate (DateOnly, current), CompletedOn (DateOnly?), CalibrationDocumentId? (FK EquipmentDocument), MaintenanceLogEntryId?, IntervalSnapshot (string, e.g. "Every 6 months"), Version.
- Create `backend/MicroLIMS.Domain/Entities/EquipmentTaskEvent.cs` — EquipmentTaskId, EventType, OldValue, NewValue, Reason, PerformedByUserId, PerformedAt, ElectronicSignatureId?.
- Create `backend/MicroLIMS.Domain/Entities/MaintenanceLogEntry.cs` — EquipmentInventoryId, EntryType, PerformedOn (DateOnly), PerformedByUserId, WorkDone, PartsReplaced?, Findings?, ElectronicSignatureId, RecordedAt, VoidedAt?, VoidedByUserId?, VoidReason?, CalendarEventId?.
- Create `backend/MicroLIMS.Domain/Entities/CalendarEvent.cs` — EventType, Title, Date (DateOnly?) XOR Month (string "yyyy-MM"), Note?, EquipmentInventoryId?, Status, CreatedBy/At, LastModifiedBy/At, CancelReason?, Version.
- Create `backend/MicroLIMS.Domain/Entities/CalendarEventDocument.cs` — mirrors `EquipmentDocument` storage fields (OriginalFileName, StorageKey, FileExtension, ContentType, FileSizeBytes, ContentSha256, UploadedBy/At, Status `MaterialDocumentStatus`, void fields).
- Modify `backend/MicroLIMS.Domain/Entities/EquipmentInventory.cs` — add CalibrationIntervalValue?, CalibrationIntervalUnit?, LastCalibratedOn (DateOnly?), PmIntervalValue?, PmIntervalUnit?, LastPmOn (DateOnly?), AutoOutOfServiceSince (DateOnly?).
- Modify `backend/MicroLIMS.Domain/Enums/SignatureMeaning.cs` — append `EquipmentPlanApproved, CalibrationRecorded, MaintenanceEntryRecorded, MaintenanceEntryVoided`.

Backend (Application) — `backend/MicroLIMS.Application/Services/EquipmentPlan/`:
- `EquipmentScheduleCalculator.cs` (pure), `EquipmentPlanService.cs`, `EquipmentWorkService.cs`, `EquipmentBaselineService.cs`, `CalendarEventService.cs`, `EquipmentCalendarService.cs` (read model + print), `EquipmentPlanResponses.cs` (records).

Backend (API/Persistence/Shared):
- Controllers `EquipmentPlanController.cs`, `MaintenanceLogController.cs`, `CalendarEventsController.cs` in `backend/MicroLIMS.API/Controllers/`.
- Worker `backend/MicroLIMS.API/BackgroundServices/EquipmentOverdueWorker.cs` (pattern: `MonitoringDeviationWorker.cs`).
- `backend/MicroLIMS.Persistence/DbContext/MicroLimsDbContext.cs` + `IMicroLimsDbContext.cs` DbSets and configurations; migration `AddEquipmentPlan`.
- `backend/MicroLIMS.Shared/Constants/PermissionConstants.cs`: `EquipmentPlanManage = "EquipmentPlan.Manage"`, `EquipmentPlanApprove = "EquipmentPlan.Approve"`, `EquipmentWorkRecord = "EquipmentWork.Record"`, `CalendarEventManage = "CalendarEvent.Manage"`; viewing uses `MonitoringPlanView`. Add to the same role groups as the MonitoringPlan permissions (lines ~134, ~150).
- DI in `backend/MicroLIMS.API/Extensions/ServiceCollectionExtensions.cs`.

Frontend:
- `frontend/src/modules/monitoringPlan/`: `types.ts`, `services/EquipmentPlanService.ts`, `components/EquipmentPlanPanel.tsx`, `components/AddEventDialog.tsx`, `components/EventDialog.tsx`; modify `MonthGrid.tsx`, `YearGrid.tsx`, `DuePanel.tsx`, `MonitoringPlanPage.tsx`.
- `frontend/src/modules/inventory/equipment/components/`: `EquipmentScheduleSection.tsx` (intervals + baselines), `RecordCalibrationDialog.tsx`, `MaintenanceLogSection.tsx`, `AddLogEntryDialog.tsx`, `EquipmentHistoryPrint.tsx`; modify `EquipmentDetailsDialog.tsx`.

---

### Task 1: Domain, persistence, migration, permissions

**Files:** all Domain files above; DbContext + interface; `PermissionConstants.cs`; migration `AddEquipmentPlan`.
- Test: `backend/MicroLIMS.Tests/UnitTests/EquipmentPlanModelTests.cs`

**Interfaces — Produces:** the entities/enums/permission constants exactly as named in File Structure; DbSets `EquipmentPlanRevisions, EquipmentTasks, EquipmentTaskEvents, MaintenanceLogEntries, CalendarEvents, CalendarEventDocuments`.

- [ ] **Step 1: Failing test** `EquipmentTask_unique_per_instrument_type_year_sequence`: with the Postgres fixture, adding two `EquipmentTask` rows with the same (EquipmentInventoryId, TaskType, Year, Sequence) throws `DbUpdateException`. Plus `SignatureMeaning_new_values_are_appended`: `(int)SignatureMeaning.EquipmentPlanApproved == (int)SignatureMeaning.MonitoringDeviationClosed + 1`.
- [ ] **Step 2:** Run `dotnet test backend/MicroLIMS.Tests --artifacts-path E:/MicroLIMS/rls-tmp/eqplan --filter EquipmentPlanModelTests` → FAIL (types missing).
- [ ] **Step 3:** Add entities, configurations (unique index above; FKs Restrict; `CalendarEvent` check constraint `("Date" IS NULL) <> ("Month" IS NULL)`), DbSets, permissions, migration `AddEquipmentPlan` (`--configuration Release`). Seed the new permissions the way `MonitoringPlan.*` are seeded.
- [ ] **Step 4:** Rerun → PASS; `dotnet build backend` clean.
- [ ] **Step 5:** Commit `feat(equipment-plan): entities, migration, permissions`.

### Task 2: EquipmentScheduleCalculator (pure)

**Files:** Create `backend/MicroLIMS.Application/Services/EquipmentPlan/EquipmentScheduleCalculator.cs`; Test `backend/MicroLIMS.Tests/UnitTests/EquipmentScheduleCalculatorTests.cs`.

**Interfaces — Consumes:** `PlanWindowCalculator.IsWorkingDay/PreviousWorkingDay/NextWorkingDay`, `MonitoringSettings.WorkingWeekdays`.
**Produces:**
```csharp
public static DateOnly AddInterval(DateOnly from, int value, IntervalUnit unit);
// Raw due = certificateNextDue ?? AddInterval(lastActual, ...); then working-day move.
public static DateOnly DueAfter(DateOnly lastActual, int value, IntervalUnit unit, DateOnly? certificateNextDue,
    DateOnly today, int workingWeekdays, IReadOnlySet<DateOnly> nonWorking);
// Every due date inside [from, yearEnd] chained from lastActual; first may use certificateNextDue.
public static List<DateOnly> ProjectYear(DateOnly lastActual, int value, IntervalUnit unit, DateOnly? certificateNextDue,
    int year, DateOnly from, DateOnly today, int workingWeekdays, IReadOnlySet<DateOnly> nonWorking);
public static string Describe(int value, IntervalUnit unit); // "Weekly", "Every 2 weeks", "Monthly", "Every 6 months"
```
Chaining after the first date continues from the raw (unmoved) date, so a weekend move never drifts the series.

- [ ] **Step 1: Failing tests** (Sun–Thu working = 79, no non-working dates unless stated):
  - `AddInterval_months_clamps_month_end`: 2026-01-31 + 1 month = 2026-02-28.
  - `DueAfter_moves_weekend_back`: lastActual 2026-04-24, 1 week → raw 2026-05-01 (Fri) → 2026-04-30.
  - `DueAfter_moves_forward_when_previous_is_before_today`: today 2026-05-01, raw 2026-05-01 (Fri) → 2026-05-03 (Sun).
  - `DueAfter_moves_forward_when_previous_is_before_last_actual`: lastActual 2026-04-30 (Thu), today 2026-04-28, certificateNextDue 2026-05-01 (Fri); previous working day 2026-04-30 is not after lastActual → 2026-05-03 (Sun).
  - `DueAfter_certificate_wins`: lastActual 2026-03-10, 12 months, certificate 2027-02-15 (Mon) → 2027-02-15.
  - `ProjectYear_weekly_counts`: lastActual 2025-12-28, 1 week, year 2026, from 2026-01-01 → 52 dates, none on Fri/Sat, all within 2026.
  - `ProjectYear_stops_at_year_end` (Review Focus 5): lastActual 2026-10-01, 6 months, year 2026, from 2026-01-01 → empty (2027-04-01 is outside the year).
  - `Describe_texts`: (1,Weeks)→"Weekly", (2,Weeks)→"Every 2 weeks", (1,Months)→"Monthly", (6,Months)→"Every 6 months".
- [ ] **Step 2:** Run with `--filter EquipmentScheduleCalculatorTests` → FAIL.
- [ ] **Step 3:** Implement.
- [ ] **Step 4:** Run → PASS.
- [ ] **Step 5:** Commit `feat(equipment-plan): schedule calculator`.

### Task 3: Baselines and intervals (EquipmentBaselineService)

**Files:** Create `EquipmentBaselineService.cs`; endpoints in `EquipmentPlanController.cs` (`PUT api/equipment-plan/instruments/{id}/schedule`); Test `backend/MicroLIMS.Tests/WorkflowTests/EquipmentBaselineServiceTests.cs`.

**Produces:**
```csharp
public record SaveEquipmentScheduleRequest(int? CalibrationIntervalValue, IntervalUnit? CalibrationIntervalUnit,
    DateOnly? LastCalibratedOn, int? PmIntervalValue, IntervalUnit? PmIntervalUnit, DateOnly? LastPmOn);
public Task<EquipmentScheduleResponse> SaveScheduleAsync(int equipmentId, SaveEquipmentScheduleRequest r, uint version, int userId);
```
Rules: value and unit set together or both null; ranges per Global Constraints; baseline dates not in the future; `LastCalibratedOn` may only be set while the instrument has no completed calibration task (afterwards actual dates come from completions). Every change writes an audit-trail entry (existing audit mechanism used by `EquipmentInventoryService.UpdateAsync`) naming old → new values. Permission `EquipmentPlan.Manage`.

- [ ] **Step 1: Failing tests:** `Rejects_value_without_unit`, `Rejects_future_baseline`, `Rejects_weeks_over_104`, `Saves_and_audits_change` (audit row contains "Every 6 months"), `Rejects_baseline_edit_after_completed_calibration`.
- [ ] **Step 2:** Run → FAIL. **Step 3:** Implement + endpoint. **Step 4:** PASS. **Step 5:** Commit `feat(equipment-plan): instrument intervals and baselines`.

### Task 4: EquipmentPlanService (generate, preview, approve)

**Files:** Create `EquipmentPlanService.cs`, `EquipmentPlanResponses.cs`; endpoints `GET api/equipment-plan/revisions?year=`, `POST api/equipment-plan/drafts {year, goLiveDate?}`, `GET api/equipment-plan/revisions/{id}/preview`, `POST api/equipment-plan/revisions/{id}/approve {password, reason?}`; Test `backend/MicroLIMS.Tests/WorkflowTests/EquipmentPlanServiceTests.cs`.

**Consumes:** `EquipmentScheduleCalculator.ProjectYear`, settings + `LabCalendarDates`, `ILabClock`, `IElectronicSignatureService`.
**Produces:** `GenerateDraftAsync(int year, int userId, DateOnly? goLiveDate)`, `ApproveAsync(int revisionId, ApprovePlanRequest request, int userId, string? ip)` (reuse `ApprovePlanRequest`), `GetRevisionsAsync(int year)`, `GetPreviewAsync(int id)` returning tasks to add / retire / reschedule and exceptions (`NoBaseline`, `NoInterval`, `Retired instrument`).

Same lifecycle and go-live rules as `MonitoringPlanService.GenerateDraftAsync` (go-live default today / 1 Jan of a future year; no dates before go-live). Generation projects from the latest completed actual date (or baseline) per instrument and type; only instruments with status not Retired. Task identity key = (EquipmentInventoryId, TaskType, Year, Sequence); an approved revision adds missing keys, retires keys no longer projected (never completed ones), and reschedules open ones whose DueDate differs (Rescheduled event, signed by the approval signature). `OriginalDueDate` is set once on add and never changed. Reason required when a previous approved revision exists. Approver ≠ `GeneratedByUserId` → `BusinessRuleException("The plan must be approved by someone other than the person who generated it.")`. Signature meaning `EquipmentPlanApproved`.

- [ ] **Step 1: Failing tests:** `Draft_excludes_instrument_without_baseline_and_reports_exception`, `Draft_projects_monthly_and_weekly` (6-month calibration from 2026-03-10 with go-live 2026-01-01 → due 2026-09-10 only), `Approve_rejects_same_user`, `Approve_requires_reason_for_second_revision`, `Second_revision_keeps_original_due_date_and_records_rescheduled_event`, `Retired_instrument_tasks_are_retired_not_deleted`.
- [ ] **Step 2:** FAIL. **Step 3:** Implement. **Step 4:** PASS. **Step 5:** Commit `feat(equipment-plan): yearly plan revisions with signed approval`.

### Task 5: EquipmentWorkService (calibration + maintenance logbook, re-projection)

**Files:** Create `EquipmentWorkService.cs`; `MaintenanceLogController.cs` (`GET api/equipment/{id}/maintenance-log`, `POST api/equipment/{id}/maintenance-log`, `POST api/maintenance-log/{entryId}/void`); calibration endpoint `POST api/equipment-plan/instruments/{id}/calibrations`; Test `backend/MicroLIMS.Tests/WorkflowTests/EquipmentWorkServiceTests.cs`.

**Produces:**
```csharp
public record RecordCalibrationRequest(int CertificateDocumentId, DateOnly CalibratedOn, DateOnly? CertificateNextDue, string Password);
public Task<EquipmentTaskResponse> RecordCalibrationAsync(int equipmentId, RecordCalibrationRequest r, int userId, string? ip);
public record AddLogEntryRequest(MaintenanceEntryType EntryType, DateOnly PerformedOn, string WorkDone,
    string? PartsReplaced, string? Findings, int? CalendarEventId, string Password);
public Task<MaintenanceLogEntryResponse> AddLogEntryAsync(int equipmentId, AddLogEntryRequest r, int userId, string? ip);
public Task VoidLogEntryAsync(int entryId, string reason, string password, int userId, string? ip);
```
Calibration: certificate must be a Current `EquipmentDocument` of type `CalibrationCertificate` on the same instrument; `CalibratedOn` ≤ today and > previous actual; closes the earliest open calibration task of the instrument; when none is open it adds an `EquipmentTask` with `IsUnplanned = true`, `OriginalDueDate = DueDate = CompletedOn = CalibratedOn`, next free Sequence, and `CreatedByRevisionId` of the live approved revision (if there is no approved revision for that year, `CreatedByRevisionId` is null — make it nullable in Task 1). Then re-project following open calibration tasks with `DueAfter` (certificate next-due for the first), each move a Rescheduled event; set `EquipmentInventory.CalibrationDueDate` to the new due date and the matching `Equipment` row (resolved like `EquipmentConfigurationService` line ~127) too; if `AutoOutOfServiceSince != null` and status is OutOfService, set InService, clear the flag, add `EquipmentStatusHistory` "Calibration recorded; returned to service". Signature `CalibrationRecorded`.
Logbook: append-only; PreventiveMaintenance entries close the earliest open PM task and re-project PM like calibration; BreakdownRepair/Other close nothing. Void requires reason + signature (`MaintenanceEntryVoided`); voiding a PM entry reopens its task only if no later PM entry exists (otherwise void only). Signature for entries `MaintenanceEntryRecorded`.

- [ ] **Step 1: Failing tests:** `Calibration_rejects_certificate_of_other_instrument`, `Calibration_rejects_future_date`, `Calibration_rejects_date_before_previous_actual`, `Late_calibration_reprojects_from_actual` (12 months, planned 2026-03-10, done 2026-04-14 → next due 2027-04-14 minus weekend rule; OriginalDueDate unchanged), `Certificate_next_due_wins`, `Calibration_updates_register_and_equipment_due_dates`, `Calibration_returns_auto_out_of_service_to_service`, `Pm_entry_closes_task_and_reprojects`, `Breakdown_entry_closes_nothing`, `Void_requires_reason`.
- [ ] **Step 2:** FAIL. **Step 3:** Implement + endpoints (permission `EquipmentWork.Record`). **Step 4:** PASS. **Step 5:** Commit `feat(equipment-plan): record calibration and maintenance logbook`.

### Task 6: Overdue worker and PM warnings

**Files:** Create `backend/MicroLIMS.API/BackgroundServices/EquipmentOverdueWorker.cs` (hourly, like `MonitoringDeviationWorker`) calling `EquipmentWorkService.ApplyOverdueAsync(DateOnly today)`; modify `EquipmentInventoryService.UpdateAsync` so a manual status change clears `AutoOutOfServiceSince`; modify `MonitoringReminderService` to include overdue PM and calibration counts. Test `EquipmentWorkServiceTests` (same file).

**Produces:** `public Task<int> ApplyOverdueAsync(DateOnly today)` — for InService instruments with `CalibrationDueDate < today`: status OutOfService, `AutoOutOfServiceSince = today`, status-history comment `Calibration overdue since {due:yyyy-MM-dd} (automatic)`, system user as performer. Returns count. Idempotent.

- [ ] **Step 1: Failing tests:** `Overdue_calibration_sets_out_of_service_once` (second call returns 0), `Manual_status_change_clears_auto_flag_so_certificate_does_not_restore` (Review Focus 3), `Overdue_pm_does_not_change_status`.
- [ ] **Step 2:** FAIL. **Step 3:** Implement, register worker. **Step 4:** PASS. **Step 5:** Commit `feat(equipment-plan): overdue calibration takes instruments out of service`.

### Task 7: Calendar events

**Files:** Create `CalendarEventService.cs`, `CalendarEventsController.cs` (`GET api/calendar-events?from=&to=`, `POST`, `PUT {id}` with If-Match, `POST {id}/cancel {reason}`, `POST {id}/documents` multipart, `GET api/calendar-event-documents/{docId}/content`); Test `backend/MicroLIMS.Tests/WorkflowTests/CalendarEventServiceTests.cs`.

**Produces:** `CalendarEventResponse(int Id, CalendarEventType Type, string Title, DateOnly? Date, string? Month, string? Note, int? EquipmentInventoryId, string? EquipmentCode, CalendarEventStatus Status, bool ReportPending, IReadOnlyList<DocumentSummary> Documents, uint Version)`. `ReportPending` = Type ProficiencyTesting && no Current document. Document storage follows `EquipmentDocumentService` (server key `calendar-event-documents/{eventId}/{docId}{ext}`, SHA-256, no hard delete). Edits/cancel audited; no signature. Permission `CalendarEvent.Manage`.

- [ ] **Step 1: Failing tests:** `Requires_date_or_month_not_both`, `Pt_event_is_report_pending_until_document_attached`, `Cancel_requires_reason_and_keeps_row`, `List_returns_month_events_overlapping_range`.
- [ ] **Step 2:** FAIL. **Step 3:** Implement. **Step 4:** PASS. **Step 5:** Commit `feat(equipment-plan): ad-hoc calendar events`.

### Task 8: Calendar read model and print data

**Files:** Create `EquipmentCalendarService.cs`; endpoints `GET api/equipment-plan/calendar?from=&to=`, `GET api/equipment-plan/year?year=`, `GET api/equipment-plan/instruments/{id}/history?year=`, `GET api/equipment-plan/planned-vs-done?year=&type=`; Test `backend/MicroLIMS.Tests/WorkflowTests/EquipmentCalendarServiceTests.cs`.

**Produces:**
```csharp
public enum EquipmentTaskStatus { Upcoming, Due, Overdue, Done, Retired } // derived, not stored; Due = within 7 days
public record EquipmentTaskItem(int Id, int EquipmentId, string EquipmentCode, string InstrumentType, EquipmentTaskType Type,
    DateOnly OriginalDueDate, DateOnly DueDate, DateOnly? CompletedOn, EquipmentTaskStatus Status, string? EvidenceLabel);
public record EquipmentMonthSummary(int Month, int Planned, int Done, int Overdue, int Upcoming, int Events);
public record InstrumentHistoryResponse(/* instrument header */, IReadOnlyList<EquipmentTaskItem> Tasks,
    IReadOnlyList<TaskEventLine> Reschedules, IReadOnlyList<StatusLine> StatusChanges,
    IReadOnlyList<MaintenanceLogEntryResponse> Logbook, BaselineLine? CalibrationBaseline, BaselineLine? PmBaseline,
    DateOnly? NextCalibrationDue, DateOnly? NextPmDue);
```
- [ ] **Step 1: Failing tests:** `Status_overdue_when_past_due_and_not_done`, `Month_summary_counts_tasks_and_events`, `History_shows_original_and_actual_dates_and_baseline_label`.
- [ ] **Step 2:** FAIL. **Step 3:** Implement. **Step 4:** PASS; full backend suite green on Postgres (`dotnet test ... --artifacts-path E:/MicroLIMS/rls-tmp/eqplan`). **Step 5:** Commit `feat(equipment-plan): calendar and history read model`.

### Task 9: Frontend — Monitoring Plan calendar integration

**Files:** `types.ts`, `services/EquipmentPlanService.ts`, modify `MonthGrid.tsx`, `YearGrid.tsx`, `DuePanel.tsx`, `MonitoringPlanPage.tsx`; create `AddEventDialog.tsx`, `EventDialog.tsx`, `EquipmentPlanPanel.tsx` (Plan tab: equipment revisions, go-live, preview, approve with signature dialog reused from `PlanRevisionsPanel`). Tests next to each component (`*.test.tsx`).

- Area filter gains "Calibration", "Preventive maintenance", "Events".
- Clicking a day cell opens `AddEventDialog` with that date; clicking a month tile header offers "Add event for this month". Opening an existing event shows `EventDialog` (edit, cancel with reason, attach report; PT shows "Report pending" chip).
- Equipment tasks render with the existing `StatusPill` colours (Overdue red, Due amber, Done green).
- [ ] **Step 1: Failing tests:** `MonthGrid` renders an equipment task and an event on their dates; clicking an empty day calls `onAddEvent("2026-10-25")`; `AddEventDialog` requires a title and submits `{type, title, date}`; `EventDialog` shows "Report pending" for PT without documents; `EquipmentPlanPanel` disables Approve for the generating user.
- [ ] **Step 2:** `cd frontend && npx vitest run src/modules/monitoringPlan` → FAIL. **Step 3:** Implement. **Step 4:** PASS + `npx tsc --noEmit` + `npm run lint`. **Step 5:** Commit `feat(equipment-plan): calendar shows calibration, maintenance and events`.

### Task 10: Frontend — instrument schedule, logbook, record calibration, print

**Files:** create `EquipmentScheduleSection.tsx`, `RecordCalibrationDialog.tsx` (pick existing certificate or upload via `UploadEquipmentDocumentDialog`, then date, optional certificate next-due, password), `MaintenanceLogSection.tsx`, `AddLogEntryDialog.tsx`, `EquipmentHistoryPrint.tsx` (print view, follow the register's existing print view); modify `EquipmentDetailsDialog.tsx` to add tabs Schedule / Logbook / History. Tests alongside.

- [ ] **Step 1: Failing tests:** `EquipmentScheduleSection` blocks save when value set without unit; `RecordCalibrationDialog` requires certificate, date and password; `AddLogEntryDialog` requires work done; `EquipmentHistoryPrint` shows "Planned 2026-03-10 · Done 2026-04-14" and "Baseline" label.
- [ ] **Step 2:** `npx vitest run src/modules/inventory/equipment` → FAIL. **Step 3:** Implement. **Step 4:** PASS + tsc + lint. **Step 5:** Commit `feat(equipment-plan): instrument schedule, logbook and history print`.

### Task 11: End-to-end verification

- [ ] Apply migration to local LIMSV2; run the API and frontend locally.
- [ ] Browser flow: set intervals + baselines on two instruments (one weekly PM), generate a 2026 equipment draft as user A, approve as user B; record a late calibration with certificate → next due from actual; record a PM entry → task Done; add a vendor visit on a day and a PT event for a month, attach a report → "Report pending" clears; set an instrument's calibration due date in the past → worker run → Out of service; incubator picker refuses it; print history.
- [ ] Full suites: backend Postgres suite and `npx vitest run` green; record counts in the commit message.
- [ ] Commit `test(equipment-plan): end-to-end verified`. Do not push.
