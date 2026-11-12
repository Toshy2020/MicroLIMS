# URS-SIG User Requirements Specification

| Field | Value |
|---|---|
| Document number | URS-SIG |
| Title | Electronic signatures user requirements |
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

Each requirement below is a need that the code as built serves. Gaps found in the code are not written as requirements here; they are recorded as conflicts (see `documents/_conflicts/CONF-task4.md`).

### URS-SIG-001
**Requirement:** The system shall apply an electronic signature only after the signer has entered their own password at the moment of signing, and shall not apply the signature when the password check fails.
**GxP:** Yes
**Serves:** 21 CFR 11.200(a)(1); EU GMP Annex 11 clause 14 (to be confirmed by QA)
**Status:** Active

### URS-SIG-002
**Requirement:** The system shall store with each signature the signer's printed name, user name, role, the meaning of the signature, the signed record, the date and time of signing and an optional comment, and shall keep the name and role as they were at signing even if the user is later renamed or moved to another role.
**GxP:** Yes
**Serves:** 21 CFR 11.50, 11.70 (to be confirmed by QA)
**Status:** Active

### URS-SIG-003
**Requirement:** The system shall refuse further signature attempts by a user after a configured number of failed attempts within a configured time window, without locking the user's login account, and shall record every failed and every refused attempt.
**GxP:** Yes
**Serves:** 21 CFR 11.300(d); EU GMP Annex 11 clause 12 (to be confirmed by QA)
**Status:** Active

### URS-SIG-004
**Requirement:** The system shall let an authorised user view the signatures recorded for a record, oldest first, and shall provide no way to change or delete a recorded signature.
**GxP:** Yes
**Serves:** 21 CFR 11.10(e), 11.70; EU GMP Annex 11 clause 9 (to be confirmed by QA)
**Status:** Active
