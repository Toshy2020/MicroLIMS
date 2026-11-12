# URS-ENV User Requirements Specification

| Field | Value |
|---|---|
| Document number | URS-ENV |
| Title | Environmental monitoring user requirements |
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

Each requirement is a need that the code as built serves. Gaps found in the code are written in the FS entries and in `documents/_conflicts/CONF-task5.md`, not as requirements here. In particular, no requirement is written for automatic out-of-trend detection, because the code does not do it (CONF-067). Count and presence/absence recording for environmental monitoring is shared with other areas and is in URS-MIC.

### URS-ENV-001
**Requirement:** The system shall let only users who hold the own-laboratory receiving permission register an environmental monitoring sample, only for an active department, and shall refuse a sampling time that is after the time of receipt.
**GxP:** Yes
**Serves:** 21 CFR 11.10(d); ALCOA+ contemporaneous (to be confirmed by QA)
**Status:** Active

### URS-ENV-002
**Requirement:** The system shall give each environmental monitoring sample a unique reference number, and when two receipts collide shall either save one with the next number or save nothing and tell the user.
**GxP:** Yes
**Serves:** 21 CFR 211.188 (to be confirmed by QA)
**Status:** Active

### URS-ENV-003
**Requirement:** The system shall let a user prepare a received environmental monitoring sample once, by choosing active room test configurations of the sample's own department, and shall create from them one test order per distinct test and one location per chosen room test configuration.
**GxP:** Yes
**Serves:** EU GMP Annex 11 clause 4 (to be confirmed by QA)
**Status:** Active

### URS-ENV-004
**Requirement:** The system shall compare the count of each monitored room position with the alert, action and specification limits that the Section Head configured for that room test, and shall report the most severe limit exceeded.
**GxP:** Yes
**Serves:** EU GMP Annex 1 clause 9 (to be confirmed by QA)
**Status:** Active

### URS-ENV-005
**Requirement:** The system shall not let an environmental monitoring test order go forward to review while it is waiting, running or incubating and results for every location have not been entered.
**GxP:** Yes
**Serves:** 21 CFR 211.194(a) (to be confirmed by QA)
**Status:** Active
