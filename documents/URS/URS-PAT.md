# URS-PAT User Requirements Specification

| Field | Value |
|---|---|
| Document number | URS-PAT |
| Title | Pathogen testing workflow user requirements |
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

Each requirement below is a need that the code as built serves. Gaps found in the code are not written as requirements here; they are recorded as conflicts (see `documents/_conflicts/CONF-task3.md`). Steps, media and incubation windows of a pathogen test are configured by the Section Head; no organism is built into these requirements.

### URS-PAT-001
**Requirement:** The system shall let only users who hold the workflow-execute permission record pathogen session actions, and shall refuse actions on a sample whose pathogen tests belong to a laboratory section to which the user is not assigned.
**GxP:** Yes
**Serves:** 21 CFR 11.10(d); EU GMP Annex 11 clause 12 (to be confirmed by QA)
**Status:** Active

### URS-PAT-002
**Requirement:** The system shall let an analyst start one shared enrichment broth for all tests of a sample that use the same broth, only when the lot is in date, belongs to the section of every joining test, is a permitted medium for each joining test, and is incubated in an incubator within the configured temperature range, and shall refuse the whole start when any joining test has no configured window.
**GxP:** Yes
**Serves:** Media traceability for microbiological testing (reference to be confirmed by QA)
**Status:** Active

### URS-PAT-003
**Requirement:** The system shall refuse to start enrichment on a sample whose test preparation is not confirmed.
**GxP:** Yes
**Serves:** 21 CFR 11.10(f) operational checks (to be confirmed by QA)
**Status:** Active

### URS-PAT-004
**Requirement:** The system shall allow entry of pathogen results for a test only when the server finds the test ready for result entry, based on the configured minimum incubation time of its steps, and shall name the reason when entry is refused.
**GxP:** Yes
**Serves:** 21 CFR 11.10(f); EU GMP Annex 11 clause 5 (to be confirmed by QA)
**Status:** Active

### URS-PAT-005
**Requirement:** The system shall record, for each sampling location and pathogen test, whether growth was seen and whether it matches the expected appearance, shall send only locations with growth to confirmation, and shall take the number of confirmatory media from the test configuration.
**GxP:** Yes
**Serves:** Pathogen test method records (reference to be confirmed by QA)
**Status:** Active

### URS-PAT-006
**Requirement:** The system shall decide the result of a location after confirmation from the readings of all configured confirmatory media, and shall report a disagreement between media as inconclusive.
**GxP:** Yes
**Serves:** Pathogen test method records (reference to be confirmed by QA)
**Status:** Active

### URS-PAT-007
**Requirement:** The system shall not complete a pathogen testing session while any location-by-test result is missing or any test has incomplete prerequisites, and on completion shall submit the tests for review.
**GxP:** Yes
**Serves:** ALCOA+ completeness (to be confirmed by QA)
**Status:** Active

### URS-PAT-008
**Requirement:** The system shall let only a user with the supervise permission reset a pathogen testing session, shall store the reason, and shall refuse a reset on a rejected or voided sample.
**GxP:** Yes
**Serves:** 21 CFR 11.10(e) (to be confirmed by QA)
**Status:** Active

### URS-PAT-009
**Requirement:** The system shall require the steps of a pathogen test to be recorded in their configured order, and shall refuse to close an incubation before the minimum incubation time of the medium has passed unless a user with the supervise permission has overridden it.
**GxP:** Yes
**Serves:** Pathogen test method records (reference to be confirmed by QA)
**Status:** Active

### URS-PAT-010
**Requirement:** The system shall end a pathogen test with the final result "not detected" when selective plating shows no growth or growth that does not match the expected appearance.
**GxP:** Yes
**Serves:** Pathogen test method records (reference to be confirmed by QA)
**Status:** Active

### URS-PAT-011
**Requirement:** The system shall require exactly one observation for each confirmatory medium that was set up, shall record the expected appearance that applied at the time of the observation, and shall flag a confirmatory result as inconclusive unless every medium shows conforming growth.
**GxP:** Yes
**Serves:** Pathogen test method records (reference to be confirmed by QA)
**Status:** Active

### URS-PAT-012
**Requirement:** The system shall flag a result that an analyst submitted as detected without biochemical confirmation, shall never take the final result from free text, and shall let a reviewer who did not perform the test approve it or return it for biochemical confirmation with a reason.
**GxP:** Yes
**Serves:** 21 CFR 11.10(d); EU GMP Annex 11 clause 12 (to be confirmed by QA)
**Status:** Active
