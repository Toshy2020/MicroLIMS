# URS-MIC User Requirements Specification

| Field | Value |
|---|---|
| Document number | URS-MIC |
| Title | Microbiology enumeration and incubation user requirements |
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

Each requirement below is a need that the code as built serves. Gaps found in the code are not written as requirements here; they are recorded as conflicts (see `documents/_conflicts/CONF-task3.md`).

### URS-MIC-001
**Requirement:** The system shall let only users who hold the workflow-execute permission start incubations and record microbiology results, and only for test orders in a laboratory section to which they are assigned.
**GxP:** Yes
**Serves:** 21 CFR 11.10(d); EU GMP Annex 11 clause 12 (to be confirmed by QA)
**Status:** Active

### URS-MIC-002
**Requirement:** The system shall accept a media lot for an incubation only when the lot is released for use, is not out of stock or rejected, was prepared from material of the same laboratory section as the test, and is one of the media configured for that step.
**GxP:** Yes
**Serves:** Media traceability for microbiological testing (reference to be confirmed by QA)
**Status:** Active

### URS-MIC-003
**Requirement:** The system shall accept an incubator for an incubation only when its set point is inside the temperature range configured for the medium and its calibration is not past due.
**GxP:** Yes
**Serves:** EU GMP Annex 15 equipment qualification (to be confirmed by QA)
**Status:** Active

### URS-MIC-004
**Requirement:** The system shall take the incubation time window and temperature from the medium configuration set by the Section Head, shall record the start time from the server clock and the starting user, and shall refuse to start an incubation when the window is not configured.
**GxP:** Yes
**Serves:** 21 CFR 11.10(e); ALCOA+ contemporaneous recording (to be confirmed by QA)
**Status:** Active

### URS-MIC-005
**Requirement:** The system shall require the steps of a test to be started in their configured order, shall refuse a step while an earlier step is incubating or not done, and shall refuse to start testing until sample preparation is confirmed.
**GxP:** Yes
**Serves:** EU GMP Annex 11 clause 5 (to be confirmed by QA)
**Status:** Active

### URS-MIC-006
**Requirement:** The system shall support a configured two-stage incubation in which starting stage 2 closes stage 1 and keeps the stage 1 record.
**GxP:** Yes
**Serves:** Configured incubation schedule of the method (reference to be confirmed by QA)
**Status:** Active

### URS-MIC-007
**Requirement:** The system shall calculate a plate-count result on the server as the average of the plate readings multiplied by the dilution factor, shall report it as a whole number with the configured unit, and shall store the readings, the dilution factor and the limits that applied.
**GxP:** Yes
**Serves:** USP <61> (to be confirmed by QA)
**Status:** Active

### URS-MIC-008
**Requirement:** The system shall use the dilution factor configured in the item specification and shall require a written justification when the analyst enters a different dilution factor.
**GxP:** Yes
**Serves:** 21 CFR 11.10(e) audit of changed data (to be confirmed by QA)
**Status:** Active

### URS-MIC-009
**Requirement:** The system shall record a reading of TNTC or Uncountable as a result that needs review, and shall not turn it into a number.
**GxP:** Yes
**Serves:** USP <61> (to be confirmed by QA)
**Status:** Active

### URS-MIC-010
**Requirement:** The system shall compare a numeric result with the configured specification, action and alert limits on the server, with the most severe exceeded limit taking precedence, and shall report a result with no configured limit as "limits not configured".
**GxP:** Yes
**Serves:** 21 CFR 211.194(a)(2) (to be confirmed by QA)
**Status:** Active

### URS-MIC-011
**Requirement:** The system shall record counts or growth observations per sampling location for environmental, after-cleaning and water orders, using the limits configured for each location, and shall summarise the order result.
**GxP:** Yes
**Serves:** Environmental monitoring programme records (reference to be confirmed by QA)
**Status:** Active

### URS-MIC-012
**Requirement:** The system shall let a user with the supervise permission override the minimum incubation duration of an open incubation, and shall record who did it and when.
**GxP:** Yes
**Serves:** 21 CFR 11.10(e) (to be confirmed by QA)
**Status:** Active
