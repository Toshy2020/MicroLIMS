# Equipment calibration and preventive maintenance plan, plus calendar events

Date: 2026-10-23. Status: draft for approval.

## Why

Calibration and preventive maintenance (PM) are planned activities that need
evidence that they happened. When an auditor asks, the lab must be able to print,
per instrument: what was planned in the first place, what was actually done (with
its evidence), and what is coming next. Today the Equipment Register holds only one
calibration due date per instrument, PM is not in the app at all, and the paper
maintenance logbook is the only maintenance record.

The lab also wants one calendar for all planned work: the Monitoring Plan calendar
should show calibration, PM and ad-hoc events next to water, EM and after cleaning.

## Goals

1. Each instrument in the Equipment Register (`EquipmentInventory`) can have its own
   calibration interval and its own PM interval, in weeks or months. Either may be
   empty (e.g. a ruler: calibration only). Some PM is weekly.
2. The next due date always counts from the date the work was actually done
   (calibrated in April instead of March: the next one is April + interval).
3. A yearly equipment plan is generated from these intervals and approved with an
   electronic signature, like the monitoring plan. The approved revision is frozen
   and is "the plan in the first place".
4. Calibration is closed by attaching the vendor's certificate (CoA) and recording
   the actual date, with a signature.
5. The app becomes the maintenance logbook. PM is closed by a signed logbook entry
   (what was done, by whom, when, parts replaced, findings). Breakdown repairs can be
   written in the same logbook so the instrument's history is complete (my guess,
   confirmed in playback).
6. Overdue calibration takes the instrument out of service automatically; it returns
   to service when the certificate is attached. Overdue PM only warns.
7. Ad-hoc events can be added by clicking any date or month in the calendar: vendor
   visit, equipment breakdown repair, audit, proficiency testing (PT) samples, other.
   They are reminders only. A PT event can have its result report attached later
   (results always arrive late) and shows "report pending" until then.
8. Calibration, PM and events appear in the Monitoring Plan calendar.
9. Printable audit record per instrument: planned vs done vs next, with links to the
   certificate or logbook entry.

## Non-goals

- No change to how water, EM and after-cleaning occurrences are generated.
- No vendor management, purchase orders or cost tracking.
- No scheduling of who performs the work (no assignment).
- Ad-hoc events are not part of the signed plan and are never "missed".

## Anti-goals (would make this a failure even if it works)

- The original approved plan being overwritten when dates shift. Shifts must never
  erase what was planned.
- An instrument with overdue calibration still being usable anywhere.
- A manual "Out of service" (e.g. broken) being lifted automatically because a
  certificate was attached.
- Hard deletes of logbook entries, certificates, events or plan rows.

## What exists today (build on it)

- `EquipmentInventory`: the Equipment Register (code, type, serial, location, lab
  section, `CalibrationDueDate`, `Status` with `EquipmentStatusHistory`).
- `EquipmentDocument` with type `CalibrationCertificate`: controlled documents with
  supersession and void, never deleted. Reuse it for the CoA.
- Overdue calibration is already enforced by date: `IncubatorEligibilityService`,
  `BatchSelectMediaRules`, `HplcRunService`, `IcpRunService` refuse an instrument
  whose `CalibrationDueDate` is past. Some read `Equipment.CalibrationDueDate` with
  a fallback to the register's (`EquipmentConfigurationService`). This feature must
  keep those dates correct; the existing blocks then just work.
- Monitoring plan: `MonitoringPlanRevision` (Draft, Approved, Superseded, signed
  approval, `EffectiveFrom` go-live), `MonitoringOccurrence` with frozen
  `OriginalTargetDate` / `OriginalWindowEnd`, `MonitoringOccurrenceEvent`
  (Rescheduled etc.), working days and non-working dates in `MonitoringSettings` and
  the lab calendar, `PlanWindowCalculator` working-day helpers, an hourly hosted
  worker (deviation scan), and the calendar UI in `frontend/src/modules/monitoringPlan`.

## Design

### Instrument settings

On each register instrument: calibration interval (number + unit weeks/months),
PM interval (number + unit), PM last done date. Calibration's last-done date is
derived from `CalibrationDueDate` minus interval when there is no history yet
(builder may instead ask for it explicitly; see open points). Changing an interval
is audited.

### Equipment plan revisions

A separate yearly equipment plan (its own revision series, same lifecycle as
`MonitoringPlanRevision`: Draft, Approved with signature, Superseded; reason
required after 1 January; go-live date). Generation projects, per instrument and
task type, every due date in the year from the last actual date + interval.
Each planned task row stores the frozen original due date from the approved
revision and the current due date.

My call: separate from the monitoring plan revision rather than one combined
revision, because equipment dates re-project on every completion while monitoring
windows are fixed; one shared approval would force a monitoring re-revision every
time a pipette is calibrated late.

### Re-projection when work is done

When a calibration or PM is closed with an actual date, the following not-yet-done
tasks of that instrument and task type are moved to actual + n x interval. Each move
records a Rescheduled event (who, when, old and new date, caused by which
completion). The original due date from the approved revision is never changed, so
the printout can show "planned March, done April". A late completion in December
may push a task into next year; the next year's generation picks it up from the
actual date.

Due dates that fall on a non-working day move to the working day before (my call:
earlier, so the task is never late because of a weekend), using the existing
working-day settings and non-working dates.

### Closing a calibration

Attach the certificate (existing `EquipmentDocument`, type `CalibrationCertificate`),
enter the actual calibration date (and optionally the certificate's own next-due
date), sign. Effects: task marked done with evidence link, register
`CalibrationDueDate` = actual + interval (and the linked `Equipment` row's date where
one exists), re-projection, and if the instrument was put out of service
automatically for overdue calibration, back to In service with a status-history
entry. A calibration can also be recorded with no planned task (unplanned).

### Maintenance logbook

New logbook entry per instrument: entry type (Preventive maintenance, Breakdown
repair, Other), date performed, performed by, work done, parts replaced, findings,
optional attachments, signature. Append only; a wrong entry is voided with a reason,
never edited or deleted. A PM entry closes the planned PM task it is recorded
against (or the earliest open one) and re-projects. A breakdown repair entry may
link to a breakdown event from the calendar.

### Overdue behaviour

- Calibration past its due date: the hourly worker sets the register status to Out
  of service with an automatic status-history comment ("Calibration overdue since
  <date>"). It remembers the change was automatic, so attaching a certificate only
  restores In service when the instrument was taken out automatically; a manual
  Out of service stays.
- PM past its due date: shown red in calendar and register and raised as a
  notification. No block.

### Ad-hoc calendar events

New event record: type (Vendor visit, Equipment breakdown repair, Audit,
Proficiency testing, Other), title, a date or a whole month, note, optional
instrument, attachments. Created by clicking a day or a month in the calendar.
Editable and cancellable with an audit trail; never deleted. PT events have a
result report slot and show "report pending" until a report is attached. Not
signed, never overdue.

### Calendar

The Monitoring Plan calendar gains Calibration, Preventive maintenance and Events
alongside Water, EM and After cleaning, with the existing area filter extended.
Month tiles count equipment tasks done / due / overdue like occurrences.
The plan tab handles both the monitoring and the equipment plan revisions.

### Printout

Per instrument, for a year: approved plan dates, actual dates, who, evidence
(certificate file name and upload, logbook entry), reschedules with reasons,
status changes, and the next due dates. Plus a lab-wide list "planned vs done" for
the year for calibration and for PM.

### Permissions and audit

View under the monitoring plan view permission; recording calibration, logbook
entries and events, and approving the equipment plan need their own permissions
(builder names them following the existing permission catalog). Every sensitive
action writes audit trail entries; calibration closure, logbook entries and plan
approval carry electronic signatures (CLAUDE.md principle 3).

## Constraints

- Clean Architecture; business rules in the Application layer, frontend only
  displays (CLAUDE.md).
- PostgreSQL + EF Core migrations; prod migrates at startup on Neon free tier.
- Existing calibration blocks must keep working unchanged.

## Decided by the user after the builder check

1. Calibration baseline: an instrument enters the plan only after someone enters its
   real last calibration date (with the old certificate when available). No derived
   or estimated dates. This replaces the "derived from CalibrationDueDate" idea in
   Instrument settings.
2. PM baseline: a typed last maintenance date (from the paper logbook) is enough.
   It is audited (who entered it, when) but not signed, and the printout labels it
   as a baseline, not as a logbook entry.
3. When a certificate states its own next-due date, that date wins over actual date
   + interval for `CalibrationDueDate` and for re-projection.
4. The person who records a calibration or maintenance entry may sign it. The yearly
   equipment plan is approved by a different person than the one who generated it,
   as for the monitoring plan.

## Calls made after the builder check (my calls, not the user's)

- Task identity: a planned task row keeps a stable identity across revisions. Its
  original due date is set by the first approved revision that contained it and is
  never changed. A later revision (reason required, like the monitoring plan) may add
  or retire tasks; retired tasks stay visible as retired, never deleted.
- An interval change or an instrument added mid-year goes through a new revision.
  Until that revision is approved, re-projection uses the old interval.
- Planned tasks are Upcoming, Due, Overdue (past due date, not done) or Done. They
  stay Overdue until done; there is no Missed state for equipment tasks.
- Early and late completions both re-project the following open tasks. An unplanned
  calibration or PM entry closes the earliest open task of that type.
- Non-working-day move: to the working day before, unless that is before today or
  before the last actual date; then to the next working day.
- Out of service enforcement: the existing date checks stay the guard. The automatic
  status change is for visibility and the record. A manual status change made after
  an automatic one clears the automatic flag, so the certificate never lifts it.
- `Equipment` (workflow list) rows that have a matching register row (as
  `EquipmentConfigurationService` already resolves them) get their
  `CalibrationDueDate` updated too.

## Left to the builder

- Entity and table names, DTO shapes, endpoint routes, permission names.
- Exact calendar visuals and filter labels, following the existing calendar.
- Print format (follow existing print views).
- Notification wording and cadence (reuse the existing notification recompute).
