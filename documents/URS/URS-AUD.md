# URS-AUD User Requirements Specification

| Field | Value |
|---|---|
| Document number | URS-AUD |
| Title | Audit trail user requirements |
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

### URS-AUD-001
**Requirement:** The system shall record every creation, change and deletion of stored records automatically, with the acting user, the date and time in UTC, the record type, the record number, the previous values and the new values, and shall save the record of the change in the same transaction as the change.
**GxP:** Yes
**Serves:** 21 CFR 11.10(e); EU GMP Annex 11 clause 9 (to be confirmed by QA)
**Status:** Active

### URS-AUD-002
**Requirement:** The system shall not write password hashes, token hashes or recovery-code hashes into the values of the audit trail; it shall record only that such a value changed.
**GxP:** Yes
**Serves:** 21 CFR 11.10(e), 11.300(b) (to be confirmed by QA)
**Status:** Active

### URS-AUD-003
**Requirement:** The system shall let the code record a named audit event for a user action or a system process, with an event number, an action code, a category, a reason, an optional correlation number and a list of field changes.
**GxP:** Yes
**Serves:** 21 CFR 11.10(e) (to be confirmed by QA)
**Status:** Active

### URS-AUD-004
**Requirement:** The system shall prevent audit records from being changed or deleted, in the application and in the database.
**GxP:** Yes
**Serves:** 21 CFR 11.10(e); EU GMP Annex 11 clause 9 (to be confirmed by QA)
**Status:** Active

### URS-AUD-005
**Requirement:** The system shall let an authorised user search the audit trail by date range, reference numbers, sample, test order, user, record type, action, category, actor type, correlation number and reason, and read the result in pages, newest first.
**GxP:** Yes
**Serves:** 21 CFR 11.10(b), 11.10(e); EU GMP Annex 11 clause 9 (to be confirmed by QA)
**Status:** Active

### URS-AUD-006
**Requirement:** The system shall let an authorised user export the filtered audit trail as a CSV file, and shall record the export itself as an audit event before the file is sent.
**GxP:** Yes
**Serves:** 21 CFR 11.10(b); EU GMP Annex 11 clause 8 (to be confirmed by QA)
**Status:** Active

### URS-AUD-007
**Requirement:** The system shall show the linked records (sample, tests, results, reviews, signatures, media, material lots, cultures, equipment, item set-up) that belong to an audited record.
**GxP:** Yes
**Serves:** EU GMP Annex 11 clause 9 (to be confirmed by QA)
**Status:** Active

### URS-AUD-008
**Requirement:** The system shall keep a separate security audit trail of sign-in, session and account events and sign-in history, and shall show them only to users holding a separate security-audit permission.
**GxP:** Yes
**Serves:** 21 CFR 11.10(d), 11.10(e); EU GMP Annex 11 clause 12 (to be confirmed by QA)
**Status:** Active
