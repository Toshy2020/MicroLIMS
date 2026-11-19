# URS-AUD User Requirements Specification

| Field | Value |
|---|---|
| Document number | URS-AUD |
| Title | Audit trail user requirements |
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
| 0.2 | 2026-11-13 | AI coding agent, draft from code | Added periodic audit trail review and review immutability requirements (URS-AUD-009, URS-AUD-010). |
| 0.3 | 2026-11-17 | AI coding agent, draft from code | Added table export log, bench draft source context and interface-settings exclusion requirements (URS-AUD-011 to URS-AUD-013). |

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

### URS-AUD-009
**Requirement:** The system shall let an authorised user (`Audit.Review`) start a periodic audit trail review with a title, past period, and scope (`All`, `EntityName`, or `Record`) fixed at start by cut-off audit ID and entry count, view paged entries (`Audit.View`) and the count of newer entries outside the cut-off, record and close findings, export a review PDF, and complete the review by the starter's own electronic signature (`AuditTrailReviewed`).
**GxP:** Yes
**Serves:** 21 CFR 11.10(e), 11.50, 11.200(a); EU GMP Annex 11 clauses 9, 14 (to be confirmed by QA)
**Status:** Active

### URS-AUD-010
**Requirement:** The system shall prevent signed audit trail reviews from being changed or deleted and prevent audit trail review findings from being deleted or edited beyond a single Open-to-Closed transition, enforced in both the application and PostgreSQL database triggers.
**GxP:** Yes
**Serves:** 21 CFR 11.10(e); EU GMP Annex 11 clause 9 (to be confirmed by QA)
**Status:** Active

### URS-AUD-011
**Requirement:** The system shall record every table export in the data export log with the user, the time with its time zone, the table, the filters, the columns and the identifiers of the exported rows, and shall refuse an export whose row count does not match the identifiers sent.
**GxP:** Yes
**Serves:** 21 CFR 11.10(b), 11.10(e) (to be confirmed by QA)
**Status:** Active

### URS-AUD-012
**Requirement:** The system shall, when a bench device saves a draft that was started more than 5 minutes earlier, record in the audit entries of that save the draft start time the device states, labelled as an unverified device clock, while the audit timestamp stays the server's own clock; and shall store the device time as stated, without correction.
**GxP:** Yes
**Serves:** ALCOA+ (contemporaneous, attributable); 21 CFR 11.10(e) (to be confirmed by QA)
**Status:** Active

### URS-AUD-013
**Requirement:** The system shall keep per-user interface settings and the list of recently opened records out of the automatic audit trail, because they hold no laboratory data, and shall record that decision in the system's configuration.
**GxP:** No
**Serves:** Not applicable
**Status:** Active
