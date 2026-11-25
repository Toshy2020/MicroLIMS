# URS-OOS User Requirements Specification

| Field | Value |
|---|---|
| Document number | URS-OOS |
| Title | Out-of-specification tracking user requirements |
| Version | 0.2 |
| Status | Draft — pending approval |
| Author | Claude (AI agent), draft from code |
| Reviewer | To be assigned by QA |
| Approver | To be assigned by QA |
| Effective date | Set when Approved |

## Change history

| Version | Date | Author | Change |
|---|---|---|---|
| 0.1 | 2026-11-12 | Claude (AI agent), draft from code | First draft, written from the code as built. |
| 0.2 | 2026-11-25 | AI coding agent, draft from code | Added structured OOS investigation, retest plan, conclusion, excursion review, QA deviation request, template, and trending requirements (URS-OOS-003..012; Draft — pending approval). |

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


### URS-OOS-003
*(Draft — pending approval)*
**Requirement:** When a reviewer completes the review of a laboratory section with one or more out-of-specification or detected results, the system shall automatically open (or join an existing open) OOS investigation in the same transaction, assign a unique sequential `OOS{MM}{yy}{seq:D3}` number, snapshot the section's checklist template, and compute working-day due dates. The system shall also allow an authorized Section Head to manually open an investigation for a reviewed failing section that has no investigation.
**GxP:** Yes
**Serves:** 21 CFR 211.192; EU GMP Annex 11 clause 9 (to be confirmed by QA)
**Status:** Active

### URS-OOS-004
*(Draft — pending approval)*
**Requirement:** During Phase I of an OOS investigation, the system shall allow a performing analyst to record an analyst statement, allow authorized users to answer the snapshotted Phase I checklist items with optional comments alongside read-only pre-filled laboratory facts, and require an independent Section Head to electronically sign the Phase I assessment (`LaboratoryErrorConfirmed` with an active assignable cause category and description, or `NoLaboratoryErrorFound`).
**GxP:** Yes
**Serves:** 21 CFR 11.50, 211.192; EU GMP Annex 11 clause 14 (to be confirmed by QA)
**Status:** Active

### URS-OOS-005
*(Draft — pending approval)*
**Requirement:** After Phase I is signed, the system shall allow an authorized Section Head to draft and electronically sign a retest plan specifying 1 to 10 retests, source (`RetainedSample` or `NewSample`), eligible failing test orders, one assignable analyst per sample, and a written judging rule, and shall create the linked retest samples only upon signing. Any amendment shall require a written reason and a new signature.
**GxP:** Yes
**Serves:** 21 CFR 11.50, 211.192; FDA OOS Guidance (to be confirmed by QA)
**Status:** Active

### URS-OOS-006
*(Draft — pending approval)*
**Requirement:** In Phase II, the system shall allow authorized users to record outside-laboratory review findings, hypotheses, hypothesis outcomes, investigation notes, and Phase II checklist answers, and shall require an independent Section Head to electronically sign the final conclusion (`Confirmed` or `Invalidated` with an assignable cause category other than "No cause found" and completed retest reviews) without altering or deleting the original result rows.
**GxP:** Yes
**Serves:** 21 CFR 11.10(e), 11.50, 211.192; EU GMP Annex 11 clause 14 (to be confirmed by QA)
**Status:** Active

### URS-OOS-007
*(Draft — pending approval)*
**Requirement:** The system shall allow an independent Section Head to electronically sign a reopening of a concluded OOS investigation with a written reason only while the origin laboratory section has not yet been approved or rejected, preserving the prior signed conclusion and signature in an immutable reopening record and reversing any origin invalidation flags.
**GxP:** Yes
**Serves:** 21 CFR 11.10(e), 11.50 (to be confirmed by QA)
**Status:** Active

### URS-OOS-008
*(Draft — pending approval)*
**Requirement:** The system shall allow a QA user holding `Oos.RequestDeviation` to submit a deviation request on any OOS investigation without blocking its conclusion, and shall allow a Section Head holding `Oos.Manage` to address the request with a comment.
**GxP:** Yes
**Serves:** 21 CFR 211.192 (to be confirmed by QA)
**Status:** Active

### URS-OOS-009
*(Draft — pending approval)*
**Requirement:** The system shall record separate Excursion reviews (`Kind = Excursion`) for action-limit exceedances (and alert-limit exceedances when enabled on the section's Excursion template) on samples not belonging to a monitoring plan occurrence, as well as manual excursion openings, and shall allow an independent Section Head to close them by electronic signature without blocking sample approval or certificate issuance.
**GxP:** Yes
**Serves:** EU GMP Annex 11 clause 9 (to be confirmed by QA)
**Status:** Active

### URS-OOS-010
*(Draft — pending approval)*
**Requirement:** The system shall provide a paged OOS register (open, overdue, concluded, excursions, and legacy groups), investigation detail view with signed timeline, printable PDF report with signature manifestations and export audit logging, and server-side trending by product, test, analyst, instrument, cause, or conclusion, scoped to the user's readable laboratory sections (and for analysts without `Oos.Manage`, only investigations containing a test they performed).
**GxP:** Yes
**Serves:** 21 CFR 11.10(d), 11.50; EU GMP Annex 11 clause 12 (to be confirmed by QA)
**Status:** Active

### URS-OOS-011
*(Draft — pending approval)*
**Requirement:** The system shall allow an authorized Section Head to configure per-laboratory OOS and Excursion checklist templates and working-day due targets (shipping empty by default), and shall allow users holding both `Oos.Manage` and `MasterData.Manage` to maintain shared cause categories with an audited reason while preventing renaming or deactivation of the fixed "No cause found" category.
**GxP:** Yes
**Serves:** 21 CFR 11.10(e) (to be confirmed by QA)
**Status:** Active
