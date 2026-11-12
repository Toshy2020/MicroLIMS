# URS-NTF User Requirements Specification

| Field | Value |
|---|---|
| Document number | URS-NTF |
| Title | Dashboards, notifications and messaging user requirements |
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
