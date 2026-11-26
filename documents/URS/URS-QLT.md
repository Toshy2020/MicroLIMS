# URS-QLT User Requirements Specification

| Field | Value |
|---|---|
| Document number | URS-QLT |
| Title | Quality records (deviation, CAPA, change control, complaint) user requirements |
| Version | 0.1 |
| Status | Draft — pending approval |
| Author | Claude (AI agent), draft from code |
| Reviewer | To be assigned by QA |
| Approver | To be assigned by QA |
| Effective date | Set when Approved |

## Change history

| Version | Date | Author | Change |
|---|---|---|---|
| 0.1 | 2026-11-25 | AI coding agent, draft from code | First draft for D2 Tasks 1 to 8 (merged on r3/integration). Requirements URS-QLT-001..013 (Draft — pending approval). |

## Scope and reading notes

- This document covers the quality record functions merged for D2 Tasks 1 to 8: the shared record core, links, attachments and comments, deviation, CAPA, change control, complaint, checklists, classifications, due rules, extensions, and the Task 8 scope and version fix on master-data writes.
- Not covered here, and to be added in part 2 of D2: the master-data change-control gate (D2 Task 9), queries, report, trending, notifications and the overdue job (Task 10), and the screens (Tasks 11 to 13).
- Each requirement is a need that the merged code serves. Where the approved specification and the code disagree, the difference is recorded in `documents/Conflicts.md` (CONF-228 to CONF-240) and is not written as a requirement here.

## Requirements

### URS-QLT-001
*(Draft — pending approval)*
**Requirement:** The system shall create quality records of four fixed types (deviation, CAPA, change control and complaint), each with a unique number in the form `DEV-`, `CAPA-`, `CC-` or `CMP-` followed by the year and a four-digit sequence, counted per type and per year, and shall not reuse a number.
**GxP:** Yes
**Serves:** 21 CFR 11.10(e); EU GMP Annex 11 clause 8 (to be confirmed by QA)
**Status:** Active

### URS-QLT-002
*(Draft — pending approval)*
**Requirement:** The system shall run the deviation path: a laboratory user or QA raises the deviation; QA accepts and classifies it and names an owner and due date, or rejects it with a reason; the owner records the investigation; the laboratory signs the conclusion; QA approves it (or returns it to the investigation) and decides whether a CAPA is required; QA closes it only when any required CAPA has an approved plan and every action is done; QA may cancel it while it is open.
**GxP:** Yes
**Serves:** 21 CFR 211.192; EU GMP Annex 11 clause 14 (to be confirmed by QA)
**Status:** Active

### URS-QLT-003
*(Draft — pending approval)*
**Requirement:** The system shall run the CAPA path: the owner saves a plan of corrective or preventive actions, each with a responsible person and a due date, and an effectiveness check with its method and planned date; QA approves or returns the plan; each responsible person records completion with evidence; QA verifies completion once all actions are done; QA records the effectiveness check no earlier than its planned date, and a "not effective" result requires a new linked CAPA.
**GxP:** Yes
**Serves:** 21 CFR 211.192; EU GMP Annex 11 clause 14 (to be confirmed by QA)
**Status:** Active

### URS-QLT-004
*(Draft — pending approval)*
**Requirement:** The system shall run the change control path: a Section Head, QC Manager or QA proposes a change and the records it affects (items, specifications, tests, equipment or controlled documents); the named owner or a user with investigation permission records an impact assessment with a checklist and implementation tasks; QA approves the change for implementation or rejects it; each task owner records completion with evidence; QA closes the change after a post-change review, once every task is done and every affected specification or test has been changed or justified. The affected list shall be frozen when the impact assessment is signed.
**GxP:** Yes
**Serves:** 21 CFR 11.10(e), 211.100; EU GMP Annex 11 clause 10 (to be confirmed by QA)
**Status:** Active

### URS-QLT-005
*(Draft — pending approval)*
**Requirement:** The system shall run the complaint path: QA records a received complaint with its source, date received, optional external reference, optional item and lot, and text; QA requests a laboratory investigation with a classification, an owner, a due date and what to test; the owner records findings and links samples; the laboratory signs the report; QA concludes the complaint as justified (requiring a linked non-cancelled follow-up deviation or CAPA), not justified or inconclusive, and closes it.
**GxP:** Yes
**Serves:** 21 CFR 211.198; EU GMP Annex 11 clause 14 (to be confirmed by QA)
**Status:** Active

### URS-QLT-006
*(Draft — pending approval)*
**Requirement:** The system shall prevent the person who raised or owns a quality record from approving or closing it; shall prevent the owner from signing the laboratory conclusion or report of the same record; shall prevent the analyst who produced a result from owning a deviation that comes from that result; and shall prevent QA users from owning a record or holding laboratory investigation and conclusion rights, enforcing these rules in the server and not only in the screens.
**GxP:** Yes
**Serves:** 21 CFR 11.10(d); EU GMP Annex 11 clause 12 (to be confirmed by QA)
**Status:** Active

### URS-QLT-007
*(Draft — pending approval)*
**Requirement:** The system shall require an electronic signature (user identity and password) with a stated meaning for each approval, closure, laboratory conclusion, assessment, verification and decision step; shall record each signed step as an append-only entry bound to the signature and a snapshot of the signed content; and shall not allow the signed content of a record to be changed afterwards through the normal update path or at the database level.
**GxP:** Yes
**Serves:** 21 CFR 11.50, 11.70, 11.10(e); EU GMP Annex 11 clause 14 (to be confirmed by QA)
**Status:** Active

### URS-QLT-008
*(Draft — pending approval)*
**Requirement:** The system shall refuse a change made against a stale record version, shall refuse a change to a record the user cannot read, shall audit every change to a quality record, and shall apply the same version and laboratory-scope checks to the master-data writes on which quality records depend (test workflow steps and test freeze or unfreeze).
**GxP:** Yes
**Serves:** 21 CFR 11.10(a), 11.10(e); EU GMP Annex 11 clause 9 (to be confirmed by QA)
**Status:** Active

### URS-QLT-009
*(Draft — pending approval)*
**Requirement:** The system shall let a quality record be linked to laboratory records (samples, test orders, OOS investigations, monitoring deviations, equipment, media lots, stock lots, items, specifications, tests, controlled documents, audit trail findings and other quality records); shall choose each link from the system's records rather than free text; shall show a linked record the reader cannot otherwise read by its number only; and shall allow links to be removed only before the first signed step.
**GxP:** Yes
**Serves:** 21 CFR 11.10(d); EU GMP Annex 11 clause 12 (to be confirmed by QA)
**Status:** Active

### URS-QLT-010
*(Draft — pending approval)*
**Requirement:** The system shall accept attachments only of allowed types whose content matches the type, up to 10 MB each, shall never delete an attachment (voiding keeps the row with its reason, actor and time), shall record each download in the audit trail, and shall accept comments that cannot be edited or deleted.
**GxP:** Yes
**Serves:** 21 CFR 11.10(e); EU GMP Annex 11 clause 9 (to be confirmed by QA)
**Status:** Active

### URS-QLT-011
*(Draft — pending approval)*
**Requirement:** The system shall answer each checklist item with yes, no or not applicable, keeping the item text as it was when answered; shall compute due dates in working days from site-configured rules per type and classification (falling back to the site rule when a classification rule is empty), leaving the due date empty when no rule is set; shall allow the record owner to request an extension of the record's or an action's due date, and the responsible user to request an extension of an action's due date, each with a reason; shall allow QA (other than the requester, raiser, or owner) to approve or refuse an extension with an electronic signature and keep every request in the record's history; and shall list open records and actions whose due date has passed.
**GxP:** Yes
**Serves:** 21 CFR 11.10(e), 211.192 (to be confirmed by QA)
**Status:** Active

### URS-QLT-012
*(Draft — pending approval)*
**Requirement:** The system shall let QA maintain the site-wide quality classifications, due rules and checklist templates, and shall let a Section Head maintain a laboratory checklist template when granted the investigation right for that laboratory; each change shall be audited, and shall be refused when the caller's version is stale.
**GxP:** Yes
**Serves:** 21 CFR 11.10(e), 11.10(d) (to be confirmed by QA)
**Status:** Active

### URS-QLT-013
*(Draft — pending approval)*
**Requirement:** The system shall show a quality record only to users who hold quality view permission and whose laboratory scope includes the record's laboratory, or to all such users when the record is site-wide; and shall show a user with no investigation, conclusion or approval permission only the records they raised, own or hold an action in.
**GxP:** Yes
**Serves:** 21 CFR 11.10(d); EU GMP Annex 11 clause 12 (to be confirmed by QA)
**Status:** Active

### URS-QLT-014
*(Draft — pending approval)*
**Requirement:** Every generated PDF report of a quality record shall manifest each electronic signature with the signer's printed full name, UTC date and time with explicit time zone, and signature meaning; shall show restricted linked records by number/code only with a restricted marker; and shall indicate when linked records exceed the display cap.
**GxP:** Yes
**Serves:** 21 CFR 11.50, 11.10(e); EU GMP Annex 11 clause 14 (to be confirmed by QA)
**Status:** Active

### URS-QLT-015
*(Draft — pending approval)*
**Requirement:** The system shall provide quality trending (defaulting to the last 365 laboratory days, capped at 5 years) across type, classification, cause, section, item (including sample-linked items) and source, excluding cancelled and rejected records from performance rates and repeat-cause detection, hiding restricted equipment names, and reporting repeat-cause clusters within the configured window; and shall provide a role-scoped dashboard capped at 50 items per list with total counts and Section Head laboratory open lists.
**GxP:** Yes
**Serves:** 21 CFR 11.10(d), 211.192; EU GMP Annex 11 clause 12 (to be confirmed by QA)
**Status:** Active

### URS-QLT-016
*(Draft — pending approval)*
**Requirement:** The system shall emit post-commit quality workflow notifications (raised, assigned, returned, awaiting QA, action assigned, closed, rejected, cancelled) without rolling back committed steps on notification failure, and shall run a scheduled overdue check emitting at most one notification per recipient per item per laboratory day for open records and approved-plan actions.
**GxP:** Yes
**Serves:** 21 CFR 211.192; EU GMP Annex 11 clause 12 (to be confirmed by QA)
**Status:** Active
