# URS-MON User Requirements Specification

| Field | Value |
|---|---|
| Document number | URS-MON |
| Title | Annual monitoring plan and deviations user requirements |
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

Each requirement is a need that the code as built serves. Gaps found in the code are written in the FS entries and in `documents/_conflicts/CONF-task5.md`, not as requirements here. This document covers the monitoring plan revisions, the planning rules, linking of samples to planned occurrences, occurrence status and deviations. The manual, skip and change-window functions of occurrences, the monitoring settings, reports and reminders were not read in this task and have no requirement here.

### URS-MON-001
**Requirement:** The system shall let only users who hold the monitoring plan view, manage or approve permission see, generate or approve plan revisions and see, scan for or close deviations, as each permission allows.
**GxP:** Yes
**Serves:** 21 CFR 11.10(d); EU GMP Annex 11 clause 12 (to be confirmed by QA)
**Status:** Active

### URS-MON-002
**Requirement:** The system shall generate a draft plan revision for a year from the sampling frequencies configured by the Section Head for active water sampling points and rooms, and shall list the targets it could not plan and why.
**GxP:** Yes
**Serves:** EU GMP Annex 1 clause 9; USP <1231> (to be confirmed by QA)
**Status:** Active

### URS-MON-003
**Requirement:** The system shall place each planned occurrence in a window computed from the configured frequency, the laboratory week start, the working weekdays, the laboratory calendar of non-working days and the configured early and late tolerance days, and shall plan nothing before the go-live date.
**GxP:** Yes
**Serves:** EU GMP Annex 1 clause 9 (to be confirmed by QA)
**Status:** Active

### URS-MON-004
**Requirement:** When a year already has an approved plan, the system shall let a new revision add missing occurrences, retire occurrences that no longer apply and have not started, and move occurrences that have not started, without changing occurrences that were started or that a person changed.
**GxP:** Yes
**Serves:** 21 CFR 11.10(e); ALCOA+ original, enduring (to be confirmed by QA)
**Status:** Active

### URS-MON-005
**Requirement:** The system shall let a user who holds the approve permission put a draft revision into force only with an electronic signature, shall require a reason when the year already has an approved plan, and shall refuse approval when the master data has changed since the draft was generated.
**GxP:** Yes
**Serves:** 21 CFR 11.50, 11.200 (to be confirmed by QA)
**Status:** Active

### URS-MON-006
**Requirement:** The system shall show an analyst preparing a water, environmental monitoring or after-cleaning sample the planned occurrences of that sample's department, water department or machine that are still open, and shall link the sample's locations to the occurrences the analyst ticks.
**GxP:** Yes
**Serves:** EU GMP Annex 1 clause 9 (to be confirmed by QA)
**Status:** Active

### URS-MON-007
**Requirement:** The system shall refuse to link a sample to an occurrence that is retired, skipped, in an unapproved revision, in another group, or whose expected test is already fulfilled by another sample.
**GxP:** Yes
**Serves:** EU GMP Annex 1 clause 9 (to be confirmed by QA)
**Status:** Active

### URS-MON-008
**Requirement:** The system shall work out the status of each occurrence from its window, its exception state and the progress of the tests linked to it, at the time it is read, and shall not store the status.
**GxP:** Yes
**Serves:** EU GMP Annex 1 clause 9 (to be confirmed by QA)
**Status:** Active

### URS-MON-009
**Requirement:** The system shall open a deviation for each occurrence that is missed, that is, whose window and grace days have passed without all expected tests started.
**GxP:** Yes
**Serves:** 21 CFR 211.192 (to be confirmed by QA)
**Status:** Active

### URS-MON-010
**Requirement:** The system shall open a deviation when an approved result linked to a planned occurrence is over the action limit, out of specification or marks a detection, and shall create follow-up sampling of the same tests at the same target.
**GxP:** Yes
**Serves:** 21 CFR 211.192; EU GMP Annex 1 clause 9 (to be confirmed by QA)
**Status:** Active

### URS-MON-011
**Requirement:** The system shall close a deviation only with a reason and the electronic signatures of two different people who both hold the approve permission, and shall allow only the closure types that fit the deviation kind.
**GxP:** Yes
**Serves:** 21 CFR 11.50, 11.200; 21 CFR 211.192 (to be confirmed by QA)
**Status:** Active

### URS-MON-012
**Requirement:** The system shall scan for new deviations automatically at a configured interval and on request by a user who holds the manage permission, without opening the same deviation twice.
**GxP:** Yes
**Serves:** 21 CFR 211.192 (to be confirmed by QA)
**Status:** Active

### URS-MON-013
**Requirement:** The system shall list deviations by status and kind with the numbers open and past due.
**GxP:** No
**Serves:** Not applicable
**Status:** Active
