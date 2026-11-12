# URS-OOS User Requirements Specification

| Field | Value |
|---|---|
| Document number | URS-OOS |
| Title | Out-of-specification tracking user requirements |
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

### URS-OOS-001
**Requirement:** The system shall show a user who holds the OOS permission each out-of-specification group as its root sample and the retest samples spun off from it, with their references, statuses, tests, analysts and opening dates.
**GxP:** Yes
**Serves:** 21 CFR 211.192; EU GMP Annex 11 clause 9 (to be confirmed by QA)
**Status:** Active

### URS-OOS-002
**Requirement:** The system shall limit the OOS tracking list to the laboratory sections the user may read, and shall show for each group whether an investigation document exists.
**GxP:** Yes
**Serves:** 21 CFR 11.10(d); EU GMP Annex 11 clause 12 (to be confirmed by QA)
**Status:** Active
