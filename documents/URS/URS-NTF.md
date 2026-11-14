# URS-NTF User Requirements Specification

| Field | Value |
|---|---|
| Document number | URS-NTF |
| Title | Dashboards, notifications and messaging user requirements |
| Version | 0.3 |
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
