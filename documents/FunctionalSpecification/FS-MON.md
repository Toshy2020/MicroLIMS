# FS-MON Functional Specification

| Field | Value |
|---|---|
| Document number | FS-MON |
| Title | Annual monitoring plan and deviations functional specification |
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

- This document covers `MonitoringPlanController` (route `api/monitoring-plan`), `MonitoringDeviationsController` (route `api/monitoring-deviations`) and the services behind them in `backend/MicroLIMS.Application/Services/Monitoring/`: `MonitoringPlanService`, `PlanGenerator`, `PlanWindowCalculator`, `FrequencyRules`, `MonitoringLinkService`, `OccurrenceStatus`, `OccurrenceStatusService`, `MonitoringDeviationService`, `DualSignature`, and the background worker `MonitoringDeviationWorker`.
- Not read in this task and therefore not described: `MonitoringOccurrenceService` and its controller (manual occurrences, skip, change window, calendar), `MonitoringSettingsService`, `MonitoringReportService`, `MonitoringReminderService` and `LegacyFrequencyParser`.
- Every statement was written from the code files named in each entry. Nothing was run. Guideline chapters are pointers only (to be confirmed by QA).
- Frequencies, tolerance days, grace days, working weekdays, laboratory week start, deviation due days, follow-up window days and the laboratory calendar are configured (frequencies by the Section Head; settings and calendar in the monitoring settings). None of those values is in the code, except that a settings object with defaults is used when no settings row exists (the defaults were not read).
- Concurrency: `MonitoringPlanService.ApproveAsync` calls `RecordVersion.EnsureCurrent` on the revision. No other method in this document does (no stale-form version check). Overlapping saves of one row are still refused by the `xmin` token on entities that carry a version column.
- None of the methods here writes an explicit `AuditLog` row. They write `MonitoringOccurrenceEvent` rows (event type, old and new value, user, time, reason, signature) and signed fields on the revision and deviation. Whether the context's automatic audit capture covers these tables was not checked in this task.
- There is no laboratory-section scope check in any method of this document. The permissions are the only access control.

## Functions

### FS-MON-001
**Satisfies:** URS-MON-001, URS-MON-002
**Function:** Access and the revision list. `GET api/monitoring-plan/revisions?year=` and `GET api/monitoring-plan/revisions/{id}/preview` need `MonitoringPlan.View`. `POST api/monitoring-plan/revisions` (generate) needs `MonitoringPlan.Manage`. `POST api/monitoring-plan/revisions/{id}/approve` needs `MonitoringPlan.Approve`. `GET api/monitoring-deviations` needs `View`, `POST api/monitoring-deviations/scan` needs `Manage`, `POST api/monitoring-deviations/close` needs `Approve`. The services receive `IUserSectionScopeService` by injection but do not call it, so a holder of the permission sees and changes the plan for every laboratory. `GetRevisionsAsync` returns the revisions of a year in revision order with the number of occurrences each created. `GetPreviewAsync` for a draft rebuilds the plan from master data and shows, per category, the number of targets and occurrences, the occurrences that would be retired, the occurrences that would move, and the targets that could not be planned (`PlanException` kinds: no frequency, weekly water without a weekday, invalid frequency, no tests, target date moved to a working day). For a non-draft revision it shows what was stored.
**Code:** `backend/MicroLIMS.Application/Services/Monitoring/MonitoringPlanService.cs` — `GetRevisionsAsync`, `GetPreviewAsync`
**Verified by:** reading (not run)
**Conflicts:** CONF-073

### FS-MON-002
**Satisfies:** URS-MON-002, URS-MON-004
**Function:** Generate a draft revision (`MonitoringPlanService.GenerateDraftAsync`). Inputs: year, optional go-live date. Rules: the year must be between 2000 and 2100; the year must not have ended (compared with the laboratory date); the earliest allowed go-live is today or 1 January of the year, whichever is later; a go-live date before that or after 31 December is refused; if the year already has an approved revision and the new plan would add, retire and reschedule nothing, the method refuses ("nothing to revise"). The plan is built from active water sampling points (whose water department is active, or has none) and active rooms of active departments (FS-MON-003). Existing occurrences of the year (periodic keys starting with the year or "D" and the year, not machine targets, not retired, no exception, in an approved or later revision, not yet linked to a sample) are compared with the plan by key (target type, target, laboratory section, period key, sequence). Occurrences with a new key are added; those no longer wanted whose window starts after today are candidates for retirement; those wanted on other dates, still ahead of today, and never changed by a person (events TargetDateChanged or WindowExtended), are candidates for rescheduling. In one transaction (`UnitOfWork.RunAsync`) the existing draft of that year is reused and its occurrences replaced, or a new draft with the next revision number is created; effective-from, generated time (laboratory clock) and generating user are set; new occurrences are added. Nothing is retired or moved until approval (FS-MON-005). No signature is needed to generate. No stale-form version check.
**Code:** `backend/MicroLIMS.Application/Services/Monitoring/MonitoringPlanService.cs` — `GenerateDraftAsync`, `LiveOccurrencesAsync`, `RetirementCandidates`, `RescheduleCandidatesAsync`
**Verified by:** reading (not run)

### FS-MON-003
**Satisfies:** URS-MON-002, URS-MON-003
**Function:** Frequency validation and window calculation, as coded. Targets: each active water sampling point is split by the laboratory section of its assigned tests (a point-and-laboratory override frequency is used when one exists, otherwise the point's own); each active room uses its own frequency, else its parent room's, else its department's (`FrequencyRules.Resolve`). `FrequencyRules.Validate`: daily takes no interval, weekday, anchor month or week of month; weekly and monthly intervals are 1 to 52 (monthly at most 12); a weekly rule takes no anchor month or week of month; a weekly rule for water needs a sampling weekday; an anchor month is 1 to 12 and only allowed when the monthly interval is above 1; week of month is 1 to 4. `PlanGenerator.Generate` records an exception and skips a target that has no frequency, an invalid frequency or no tests. Tolerances (early and late days) come from the monitoring settings per category and are 0 for daily rules. `PlanWindowCalculator`: lab weeks start on the configured week start day; a week belongs to the year that holds its fourth day; weekly rules give one period per interval weeks, with a target date (the sampling weekday moved forward to the next working day) and a window of the early and late tolerance in working days around it, or with no weekday a window of the week trimmed to working days; monthly rules give a period every interval months from the anchor month, a window of the whole month (or interval months) trimmed to working days, or the chosen lab week of the month, or a target day in that week; daily rules give one period per working day with no tolerance, and non-working days are skipped, not moved. Periods whose window ends before the effective-from date are not generated. Working days are the configured working weekdays minus the laboratory calendar dates. Searches for a working day stop after 400 days (4000 for moving by a number of days).
**Code:** `backend/MicroLIMS.Application/Services/Monitoring/PlanWindowCalculator.cs` — `Calculate`, `Weekly`, `Monthly`, `Daily`; also `backend/MicroLIMS.Application/Services/Monitoring/FrequencyRules.cs` — `Validate`, `Resolve`; `backend/MicroLIMS.Application/Services/Monitoring/PlanGenerator.cs` — `Generate`
**Verified by:** reading (not run)

### FS-MON-004
**Satisfies:** URS-MON-006, URS-MON-007
**Function:** Checklist and linking while a sample is prepared. `GET` of the session checklist (`MonitoringOccurrencesController`, `for-session`, permission `MonitoringPlan.View`) calls `MonitoringLinkService.GetChecklistAsync`: only Water, EM and After Cleaning samples are accepted. It lists occurrences of the sample's group (rooms of its department; water sampling points of its water department and same laboratory section; the machine) that are not retired, have no exception, are in a non-draft revision, whose window ended no earlier than 1 January of last year and started no later than the end of this month, and that still have at least one expected item with no linked test. They are grouped as Overdue, Due now and Also due this month; one occurrence per target is pre-ticked. `LinkAsync` is called by the water, EM and After Cleaning engines inside `PrepareCoreAsync` (FS-WAT-003, FS-ENV-002, FS-ACL-002) before the sample is saved; it saves nothing itself. It takes a row lock on the chosen occurrences (`SELECT ... FOR UPDATE`) on a relational database so two sessions cannot fulfil one item twice. It refuses when an occurrence does not exist, is of another category or not in the sample's group, is skipped or retired, or belongs to a draft revision. For each occurrence, in window order, each expected item takes one new location of the sample that matches it (room test configuration; machine part, where an item with no part takes every part sampled; or water sampling point and test code). If the item is already fulfilled by another sample the method refuses and names that sample. If no location matches any item of a ticked occurrence the method refuses. A `Linked` event with the sample reference and user is added for each occurrence. The check of the group for water uses the sample's laboratory section.
**Code:** `backend/MicroLIMS.Application/Services/Monitoring/MonitoringLinkService.cs` — `GetChecklistAsync`, `LinkAsync`
**Verified by:** reading (not run)

### FS-MON-005
**Satisfies:** URS-MON-004, URS-MON-005
**Function:** Approve a revision (`POST api/monitoring-plan/revisions/{id}/approve`; `MonitoringPlanService.ApproveAsync`). Permission `MonitoringPlan.Approve`. Inputs: revision ID, password, reason. The revision is loaded and `RecordVersion.EnsureCurrent` is called (stale-form check). Rules: only a Draft can be approved; when the year already has an approved revision a reason is required; the plan is rebuilt from the current master data and the set of occurrences it would add must equal the set stored in the draft, otherwise the method refuses ("master data has changed ... generate the draft again"). In one transaction the password is verified by `IElectronicSignatureService.SignAsync` with meaning `MonitoringPlanApproved` on the revision; the revision becomes Approved with the reason, signature and approval time (laboratory clock); a previous approved revision of the year becomes Superseded; occurrences to retire get the revision ID, exception state Retired and a `Retired` event with the old window, reason, user and signature; occurrences to reschedule get a `Rescheduled` event with old and new dates and the new dates are written; the signature's entity ID is set to the revision ID in a second save. The method does not check that the approver is a different person from the user who generated the draft (`GeneratedByUserId`), so one person who holds both permissions can generate and approve.
**Code:** `backend/MicroLIMS.Application/Services/Monitoring/MonitoringPlanService.cs` — `ApproveAsync`
**Verified by:** reading (not run)
**Conflicts:** CONF-073

### FS-MON-006
**Satisfies:** URS-MON-008
**Function:** Occurrence status, as coded in `OccurrenceStatusCalculator.Calculate` and `OccurrenceStatusService.ComputeAsync`. The status is derived at read time and not stored. For each expected item the best matching linked location decides the progress: a location of a Voided or Cancelled test counts for nothing; a superseded test or a Rejected or RetestRequested test counts as PerformedExcursion; an Approved test counts as PerformedExcursion when its location result is AlertLimitExceeded, ActionLimitExceeded, OutOfSpecification or Detected, otherwise Performed; other tests count as AwaitingReview (step Ready), AwaitingApproval (step Reviewed or Approved) or InTesting. For a machine item with no part, every sampled part counts and the slowest decides. Status rules in order: exception state Retired gives Retired; Skipped gives Skipped; when every item is linked, all performed gives Performed (or PerformedExcursion if any item is), otherwise the slowest item gives InTesting, AwaitingReview or AwaitingApproval; otherwise by date: before the window start Upcoming; within the window DueToday if the target date is today, else Open; after the window end and within the grace days Overdue; later Missed; if some items are linked while the date status is Upcoming, Open or DueToday the status is PartlyPerformed. An occurrence is flagged late when the latest fulfilling sample was received after the window end. Dates use the laboratory clock. Grace days are configured in the monitoring settings. The same calculator serves the calendar, the checklist, the reports and the deviation scan, per the comment in the service.
**Code:** `backend/MicroLIMS.Application/Services/Monitoring/OccurrenceStatus.cs` — `OccurrenceStatusCalculator.Calculate`, `OccurrenceProgressRules.For`; also `backend/MicroLIMS.Application/Services/Monitoring/OccurrenceStatusService.cs` — `ComputeAsync`
**Calculations:** CALC-071, CALC-072
**Verified by:** reading (not run)
**Conflicts:** CONF-070

### FS-MON-007
**Satisfies:** URS-MON-009, URS-MON-012
**Function:** Scan for missed occurrences (`MonitoringDeviationService.DetectAsync`, `OpenMissedAsync`). A static in-process lock allows one scan at a time in one application instance; the code has no lock across several instances. Candidates are occurrences in a non-draft revision, not retired, with no exception, whose window ended before today minus the grace days, and that have no missed-occurrence deviation yet. The status of each is computed (FS-MON-006); those with status Missed get a deviation of kind MissedOccurrence with opened time, a due date of today plus the configured deviation due days, and a text saying either that nothing was sampled or how many of the expected tests were started. In one transaction the rows are saved and numbered "MD-" + opened year (UTC) + "-" + the row ID padded to five digits. The method is safe to run repeatedly because of the "no deviation yet" filter. `POST api/monitoring-deviations/scan` (permission `Manage`) runs it on request.
**Code:** `backend/MicroLIMS.Application/Services/Monitoring/MonitoringDeviationService.cs` — `DetectAsync`, `OpenMissedAsync`
**Verified by:** reading (not run)

### FS-MON-008
**Satisfies:** URS-MON-010, URS-MON-012
**Function:** Scan for excursions (`MonitoringDeviationService.OpenExcursionsAsync`). Candidates are sample locations linked to an occurrence whose result status is ActionLimitExceeded, OutOfSpecification or Detected, whose test order is Approved and not superseded, and for which the occurrence has no ActionLimitExceeded deviation yet. A result at the alert limit does not open a deviation. Occurrences that are retired or in a draft revision are left out. One deviation of kind ActionLimitExceeded is opened per occurrence, with a text that lists test code, wording ("over the action limit", "out of specification" or "organism detected") and sample reference. In the same transaction a follow-up occurrence is created for the same target and the same expected items, with period key "resp-" and the deviation ID, a window starting on the next working day and ending the configured follow-up window days later (in working days), an event `Created`, and a link from the deviation to it. The deviation is created only after the test order is Approved, so a result that is entered but not yet approved opens nothing. The follow-up occurrence event has no user (`PerformedByUserId` not set).
**Code:** `backend/MicroLIMS.Application/Services/Monitoring/MonitoringDeviationService.cs` — `OpenExcursionsAsync`
**Verified by:** reading (not run)
**Conflicts:** CONF-070

### FS-MON-009
**Satisfies:** URS-MON-013
**Function:** List deviations (`GET api/monitoring-deviations?status=&kind=`). Permission `MonitoringPlan.View`. Optional status and kind filters. Returns the count of all open deviations, the count of open deviations past their due date (laboratory date), and up to 1000 rows ordered by status, due date and ID, each with number, kind, status, target and group names, period, window, text, opened time, due date, follow-up occurrence, closure type, reason and time, and the row version. There is no paging and no section filter.
**Code:** `backend/MicroLIMS.Application/Services/Monitoring/MonitoringDeviationService.cs` — `ListAsync`
**Verified by:** reading (not run)

### FS-MON-010
**Satisfies:** URS-MON-011
**Function:** Close deviations (`POST api/monitoring-deviations/close`; `MonitoringDeviationService.CloseAsync`). Permission `MonitoringPlan.Approve`. Inputs: deviation IDs (at most 500), closure type, reason, the caller's password, the second signer's username and password. Rules: the reason must be at least five characters after trimming; at least one ID; every ID must exist; every deviation must be open; a MissedOccurrence deviation may be closed only as NotApplicableBeforeGoLive or Justified, an ActionLimitExceeded deviation only as Investigated. Closing as NotApplicableBeforeGoLive is refused when any expected test of the occurrence was started or the occurrence is already skipped or retired, and marks the occurrence Skipped with a `Skipped` event. In one transaction `DualSignature.SignAsync` is called: a second username is required; the second user must exist and be active, must be a different person from the caller, and must hold `MonitoringPlan.Approve` (checked in the role's permissions); both passwords are verified through `IElectronicSignatureService.SignAsync` with meaning `MonitoringDeviationClosed`. A wrong second password gives the same refusal as any failed signature. The deviation is then set to Closed with the closure type, reason, time, closing user and both signature links. Both signatures are created with the first deviation of the request as their entity ID, even when several deviations are closed together; the other deviations point to the same two signatures. The request has no record version, so there is no stale-form version check on the deviations.
**Code:** `backend/MicroLIMS.Application/Services/Monitoring/MonitoringDeviationService.cs` — `CloseAsync`; `backend/MicroLIMS.Application/Services/Monitoring/DualSignature.cs` — `SignAsync`
**Verified by:** reading (not run)
**Conflicts:** CONF-071

### FS-MON-011
**Satisfies:** URS-MON-012
**Function:** Automatic scan. `MonitoringDeviationWorker` is a hosted background service. It is enabled unless the configuration key `MonitoringDeviations:Enabled` is false. It waits 30 seconds after start, then calls `MonitoringDeviationService.DetectAsync` every `MonitoringDeviations:IntervalMinutes` minutes (60 when the key is missing or not above zero). The database context is marked with the process name "MonitoringDeviationWorker" so that changes are attributed to the process. A failed pass is logged and retried at the next tick; the pass is recorded when a job-run recorder is present. The scan has no user, so deviations it opens have no opening user.
**Code:** `backend/MicroLIMS.API/BackgroundServices/MonitoringDeviationWorker.cs` — `ExecuteAsync`, `ScanAsync`
**Verified by:** reading (not run)
