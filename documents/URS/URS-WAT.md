# URS-WAT User Requirements Specification

| Field | Value |
|---|---|
| Document number | URS-WAT |
| Title | Water monitoring user requirements |
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

Each requirement is a need that the code as built serves. Gaps found in the code are written in the FS entries and in `documents/_conflicts/CONF-task5.md`, not as requirements here. Count recording for water (incubation, plate counts, per-location limit comparison) is shared with other areas and is in URS-MIC; the entries below cover what is specific to water.

### URS-WAT-001
**Requirement:** The system shall let only users who hold the own-laboratory receiving permission register a water sample, only for an active water department, and only in a laboratory section that the user is allowed to create records in.
**GxP:** Yes
**Serves:** 21 CFR 11.10(d); EU GMP Annex 11 clause 12 (to be confirmed by QA)
**Status:** Active

### URS-WAT-002
**Requirement:** The system shall refuse a water sample whose sampling time is after the time of receipt.
**GxP:** Yes
**Serves:** ALCOA+ contemporaneous, accurate (to be confirmed by QA)
**Status:** Active

### URS-WAT-003
**Requirement:** The system shall give each water sample a unique reference number, and when two receipts collide shall either save one with the next number or save nothing and tell the user.
**GxP:** Yes
**Serves:** 21 CFR 211.188 (to be confirmed by QA)
**Status:** Active

### URS-WAT-004
**Requirement:** The system shall let a user prepare a received water sample once, by choosing active sampling points of the sample's own department, and shall create from them one test order per distinct test and one location per sampling point and test, using the tests the Section Head has assigned to the points.
**GxP:** Yes
**Serves:** EU GMP Annex 11 clause 4 (to be confirmed by QA)
**Status:** Active

### URS-WAT-005
**Requirement:** The system shall record, at preparation, how the water was stored and, for refrigerated water, for how many hours.
**GxP:** Yes
**Serves:** USP <1231> (to be confirmed by QA)
**Status:** Active

### URS-WAT-006
**Requirement:** The system shall compare the count of each water sampling point with the alert, action and specification limits that the Section Head configured for that point and test, and shall report the most severe limit exceeded.
**GxP:** Yes
**Serves:** USP <1231> (to be confirmed by QA)
**Status:** Active

### URS-WAT-007
**Requirement:** The system shall let a user average a list of readings for a water test order and compare the average with the limits configured for the sample's sampling point, as the earlier single-point way of entering water results.
**GxP:** Yes
**Serves:** USP <1231> (to be confirmed by QA)
**Status:** Active

### URS-WAT-008
**Requirement:** The system shall give a daily summary of the water results entered on a date, limited to the laboratory sections the user may read.
**GxP:** No
**Serves:** Not applicable
**Status:** Active
