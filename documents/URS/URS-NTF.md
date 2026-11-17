# URS-NTF User Requirements Specification

| Field | Value |
|---|---|
| Document number | URS-NTF |
| Title | Dashboards, notifications and messaging user requirements |
| Version | 0.5 |
| Status | Draft |
| Author | Claude (AI agent), draft from code |
| Reviewer | To be assigned by QA |
| Approver | To be assigned by QA |
| Effective date | Set when Approved |

## Change history

| Version | Date | Author | Change |
|---|---|---|---|
| 0.1 | 2026-11-12 | Claude (AI agent), draft from code | First draft, written from the code as built. |
| 0.2 | 2026-11-14 | AI coding agent, draft from code | Added My Work and test self-assignment requirements (URS-NTF-006 to URS-NTF-010). |
| 0.3 | 2026-11-15 | AI coding agent, draft from code | Added separate 24-hour incubation look-ahead group requirement (URS-NTF-011). |
| 0.4 | 2026-11-16 | AI coding agent, draft from code | Added notification centre, notification generation, preferences and mandatory types, e-mail outbox, working hours and escalation rules, live updates and record-changed banner requirements (URS-NTF-012 to URS-NTF-018). |
| 0.5 | 2026-11-17 | AI coding agent, draft from code | Added favourites, recently opened records, remembered laboratory and tab, breadcrumbs and action availability requirements (URS-NTF-019 to URS-NTF-023). |

## Requirements

Each requirement below is a need that the code as built serves. Gaps found in the code are not written as requirements here; they are recorded in the functional specification entries and as conflicts (see `documents/_conflicts/CONF-task8.md`).

### URS-NTF-001
**Requirement:** The system shall limit every dashboard figure to the laboratory sections the user may read, and to one laboratory when the user chooses it, and shall refuse a laboratory the user does not belong to.
**GxP:** Yes
**Serves:** 21 CFR 11.10(d) (to be confirmed by QA)
**Status:** Active

### URS-NTF-002
**Requirement:** The system shall tell a user about expiring media, incubations ready to read, samples waiting for review or approval, tests returned for revision and monitoring reminders, and shall keep each user's read state.
**GxP:** No
**Serves:** Not applicable
**Status:** Active

### URS-NTF-003
**Requirement:** The system shall list the open reading and revision tasks of an analyst, and shall refuse that list to users who are not analysts.
**GxP:** No
**Serves:** Not applicable
**Status:** Active

### URS-NTF-004
**Requirement:** The system shall let a signed-in user exchange messages with other active users in conversations in which they take part, and shall refuse reading or writing in a conversation the user is not in.
**GxP:** No
**Serves:** Not applicable
**Status:** Active

### URS-NTF-005
**Requirement:** The system shall show a Section Head the review and approval queues of the user's sections with the age of each entry, and a short list of items that need attention.
**GxP:** No
**Serves:** Not applicable
**Status:** Active

### URS-NTF-006
**Requirement:** The system shall provide the signed-in user with a single My Work task list grouped in a fixed display order and sorted within each group by unlocked before locked, then urgency, then oldest first, capped at 50 items per group with an exact actionable count, where each unlocked row identifies its target so the screen opens the exact workspace step, dialog, or record, and shall return the remaining groups and name any failed source when an individual task source fails.
**GxP:** No
**Serves:** Not applicable
**Status:** Active

### URS-NTF-007
**Requirement:** The system shall compute all My Work due times, overdue states, and the end of the current laboratory day on the server from the laboratory clock and time zone, using one shared incubation ready-to-read rule across My Work, My Tasks, and the workspaces, the 24-hour workflow status-age window, a configurable look-ahead window in days, and a configurable reading-overdue threshold in hours.
**GxP:** Yes
**Serves:** 21 CFR 11.10(e); EU GMP Annex 11 clause 4 (to be confirmed by QA)
**Status:** Active

### URS-NTF-008
**Requirement:** The system shall limit each My Work source to the signed-in user's own assignments, permissions, and laboratory sections (and to one laboratory when requested), ensuring that cross-section read access never treats another user's assigned work as the caller's, and shall mark sample review, sample approval, and media release entries that the caller performed, reviewed, prepared, or evaluated as locked with the reason, exclude locked entries from actionable counts, and disable opening or acting on them from the list.
**GxP:** Yes
**Serves:** 21 CFR 11.10(d), 11.10(g); EU GMP Annex 11 clause 12 (to be confirmed by QA)
**Status:** Active

### URS-NTF-009
**Requirement:** When test self-assignment is enabled, the system shall allow an active user who holds the self-assignment permission, belongs to the test's laboratory section, and satisfies analyst assignment eligibility to take an unassigned open non-outsourced test order under optimistic concurrency control, shall allow that user to release the test order back to unassigned only while they remain the assigned analyst, the latest assignment audit record on that test order is their own take action, and they have recorded no sample preparation, incubation, workflow step result, count reading, pathogen observation, test analysis, or result on the sample, and shall record every take and release in the audit trail.
**GxP:** Yes
**Serves:** 21 CFR 11.10(d), 11.10(e), 11.10(g); EU GMP Annex 11 clauses 9 and 12 (to be confirmed by QA)
**Status:** Active

### URS-NTF-010
**Requirement:** The system shall land the user on My Work after sign-in (redirecting once per session to the role dashboard when the user has no My Work groups and holds a dashboard permission) and shall display the live total task count and overdue status on the My Work sidebar item when all task sources succeed, hiding the sidebar badge when the summary is zero or incomplete.
**GxP:** No
**Serves:** Not applicable
**Status:** Active

### URS-NTF-011
**Requirement:** The system shall partition open incubations on active tests assigned to or started by the signed-in user into separate My Work groups for incubations ready to read, incubations not yet ready whose expected or end time is on or before the end of the current laboratory day, and incubations not yet ready whose expected or end time falls after the end of the current laboratory day and within 24 hours from the server clock, placing each incubation in at most one group.
**GxP:** No
**Serves:** Not applicable
**Status:** Active

### URS-NTF-012
**Requirement:** The system shall give each signed-in user a notification centre that lists only that user's own notifications, lets the user filter them by state (unread, read, done), type, laboratory section and text, mark one or all of them as read, and open the record a notification refers to, and shall remove notifications older than a configured retention period.
**GxP:** No
**Serves:** Not applicable
**Status:** Active

### URS-NTF-013
**Requirement:** The system shall create a notification for the people concerned when a sample waits for review or approval, a test is returned or assigned, an incubation is ready to read, media, solutions or reference strains approach or pass expiry, equipment calibration falls due, an item is overdue, a document or training task is assigned, or a message or discussion needs attention. It shall create a notification that a change causes in the same database transaction as that change, so that a change that is rolled back leaves no notification; shall not create a second open notification for the same condition and recipient; shall mark a notification done when its condition ends; and shall not put analytical result values, specification limits, passwords or tokens into notification or e-mail text.
**GxP:** No
**Serves:** Not applicable
**Status:** Active

### URS-NTF-014
**Requirement:** The system shall let each user choose, for each notification type that may be e-mailed, whether the user receives it by e-mail at once, in a daily digest, or not at all; shall not let a user switch off in-application delivery; and shall let a user with the notification-configuration permission mark e-mail types as mandatory for a laboratory section the user belongs to, so that the members of that section cannot switch those types off.
**GxP:** No
**Serves:** Not applicable
**Status:** Active

### URS-NTF-015
**Requirement:** The system shall send notification e-mails from a stored queue after the change that caused them is committed, shall retry a failed send a limited number of times with growing delays, shall not send the same queued e-mail twice when more than one process is running, and shall show a user with the user-management permission how many e-mails are waiting or have failed, when the last one was sent, and which active users have no e-mail address.
**GxP:** No
**Serves:** Not applicable
**Status:** Active

### URS-NTF-016
**Requirement:** The system shall let a user with the notification-configuration permission define the weekly working hours of the laboratory and, for a laboratory section the user belongs to, escalation rules that notify a role when work has waited longer than a threshold counted in working hours (excluding closed hours, non-working weekdays and calendar non-working dates, in laboratory local time); shall not fire an escalation twice for the same rule and record; and shall keep every change to working hours and escalation rules in the audit trail.
**GxP:** Yes
**Serves:** 21 CFR 11.10(e); EU GMP Annex 11 clause 9 (to be confirmed by QA)
**Status:** Active

### URS-NTF-017
**Requirement:** The system shall update the notification count, My Work, the section counters and messages on a signed-in user's screen shortly after the data changes, using signals that carry no business data and reach only users who may read the data, and shall fall back to periodic refresh, with a visible "Live updates paused" status, when the live connection is not available.
**GxP:** No
**Serves:** Not applicable
**Status:** Active

### URS-NTF-018
**Requirement:** The system shall tell a user who has a record or a configuration form open that another user has changed that record, shall not do so for the user's own save, and shall not reload the record or replace what the user has typed unless the user chooses to reload.
**GxP:** No
**Serves:** Not applicable
**Status:** Active

### URS-NTF-019
**Requirement:** The system shall let each signed-in user keep a list of up to 15 favourite pages and saved views, shown in the sidebar and in the search dialog, in an order the user chooses; and shall show a favourite whose page is no longer in the user's menu as not available, rather than hiding it.
**GxP:** No
**Serves:** Not applicable
**Status:** Active

### URS-NTF-020
**Requirement:** The system shall keep, for each signed-in user, the 20 records the user opened most recently, show the five newest in the sidebar, list only records the user may still read, never show the name of a record the user can no longer read, and let the user clear the list.
**GxP:** No
**Serves:** Not applicable
**Status:** Active

### URS-NTF-021
**Requirement:** The system shall remember, for each signed-in user, the laboratory last chosen on the dashboard and the tab last shown on each tabbed page; shall open the laboratory or tab named in the address first; and shall open the default when the remembered value is no longer available.
**GxP:** No
**Serves:** Not applicable
**Status:** Active

### URS-NTF-022
**Requirement:** The system shall show above each page its place in the menu (group, area and page), and on a record or step page the record's identifier as the last crumb, and shall show the same parts in the browser tab title.
**GxP:** No
**Serves:** Not applicable
**Status:** Active

### URS-NTF-023
**Requirement:** The system shall answer, for each gated action on a sample, test order, media lot or document revision, whether the signed-in user may perform the action now and, when not, the same sentence the server gives when the action is refused. The answer shall change nothing, and shall not reveal anything about a record the user may not read.
**GxP:** Yes
**Serves:** 21 CFR 11.10(d), 11.10(g) (to be confirmed by QA)
**Status:** Active
