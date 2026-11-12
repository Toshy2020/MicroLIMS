# URS-EQP User Requirements Specification

| Field | Value |
|---|---|
| Document number | URS-EQP |
| Title | Equipment configuration and calibration plan user requirements |
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

Each requirement below is a need that the code as built serves. Gaps found in the code are not written as requirements here; they are recorded in the functional specification entries and as conflicts (see `documents/_conflicts/CONF-task7.md`).

### URS-EQP-001
**Requirement:** The system shall keep a configuration list of laboratory equipment (name, code, type, location, section, and for incubators a set point and calibration due date), shall let every user read the part of it in their sections, and shall let only users who hold the master-data-manage permission, in the owning section, create or edit an item.
**GxP:** Yes
**Serves:** EU GMP Annex 15 equipment records; 21 CFR 11.10(d) (to be confirmed by QA)
**Status:** Active

### URS-EQP-002
**Requirement:** The system shall refuse a duplicate equipment code, shall require a CDS software choice for chromatography and ICP equipment and refuse it for other types, and shall refuse an edit made from an out-of-date form.
**GxP:** Yes
**Serves:** Master data control (reference to be confirmed by QA)
**Status:** Active

### URS-EQP-003
**Requirement:** The system shall require a reason for each change of an incubator set point and shall keep each change with the previous value, the new value, the user and the time.
**GxP:** Yes
**Serves:** 21 CFR 11.10(e) audit trail; ALCOA+ (to be confirmed by QA)
**Status:** Active

### URS-EQP-004
**Requirement:** The system shall keep autoclave programmes (code, name, load type, temperature, cycle time, active flag) per autoclave, shall refuse a duplicate programme code on one autoclave, and shall keep a history of each creation, update and status change with the user and time.
**GxP:** Yes
**Serves:** Equipment configuration control (reference to be confirmed by QA)
**Status:** Active

### URS-EQP-005
**Requirement:** The system shall let a master-data manager create the configuration record of an inventory equipment item so that the item can be used in the workflows that select configured equipment.
**GxP:** Yes
**Serves:** Equipment traceability (reference to be confirmed by QA)
**Status:** Active

### URS-EQP-006
**Requirement:** The system shall let an authorised user set, for an inventory equipment item, a calibration interval and a maintenance interval and the dates of the last calibration and last maintenance, shall refuse incomplete intervals, intervals outside the allowed range and dates in the future, shall refuse a change of the last calibration date once a calibration has been completed, and shall keep the previous and new schedule in the audit trail.
**GxP:** Yes
**Serves:** EU GMP Annex 15 calibration programme (to be confirmed by QA)
**Status:** Active

### URS-EQP-007
**Requirement:** The system shall generate a draft calibration and maintenance plan for a calendar year from the intervals and baselines of the inventory equipment, using the laboratory working days, and shall list the items that could not be planned and the reason.
**GxP:** Yes
**Serves:** EU GMP Annex 15 calibration programme (to be confirmed by QA)
**Status:** Active

### URS-EQP-008
**Requirement:** The system shall approve a plan revision only on the electronic signature of a user who holds the plan-approve permission and did not generate the draft, shall require a reason when the year already has an approved plan, shall refuse approval if the master data changed since the draft was made, and shall keep the retired and rescheduled tasks with the signature.
**GxP:** Yes
**Serves:** 21 CFR 11.50, 11.200; EU GMP Annex 11 clause 14 (to be confirmed by QA)
**Status:** Active

### URS-EQP-009
**Requirement:** The system shall record a calibration only on the electronic signature of the user, against a current calibration certificate of the same item, with a calibration date that is not in the future and is after the previous calibration, and shall then set the next due date and update the calibration due date of the item.
**GxP:** Yes
**Serves:** 21 CFR 11.50, 11.200; EU GMP Annex 15 (to be confirmed by QA)
**Status:** Active

### URS-EQP-010
**Requirement:** The system shall record maintenance log entries only on the electronic signature of the user, shall refuse a duplicate entry for the same item and day, and shall void an entry only with a reason and the electronic signature of the user, keeping the original entry.
**GxP:** Yes
**Serves:** 21 CFR 11.50, 11.10(e) (to be confirmed by QA)
**Status:** Active

### URS-EQP-011
**Requirement:** The system shall take an in-service equipment item out of service, with a history entry marked as automatic, when its calibration due date has passed.
**GxP:** Yes
**Serves:** EU GMP Annex 15 (to be confirmed by QA)
**Status:** Active

### URS-EQP-012
**Requirement:** The system shall offer an incubator for media selection only when its set point lies in the temperature range of the medium and its calibration due date is empty or not past; an incubator with no calibration due date counts as in calibration (decision recorded under CONF-003).
**GxP:** Yes
**Serves:** EU GMP Annex 15 equipment status (to be confirmed by QA)
**Status:** Active
