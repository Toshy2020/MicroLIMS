# URS-PHY User Requirements Specification

| Field | Value |
|---|---|
| Document number | URS-PHY |
| Title | Physicochemical testing user requirements |
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

Each requirement below is a need that the code as built serves. Gaps found in the code are written in the FS entries and in `documents/_conflicts/CONF-task6.md`, not as requirements here. This document covers the physicochemical workspace, the single-entry result types (measurement, gravimetric, qualitative), the staged result types (dissolution, disintegration, weight variation), titration, UV spectrophotometry and outsourced tests. Chromatography and ICP runs are in URS-INS. Working standards are in URS-WST. Solution preparation is in URS-SOL.

### URS-PHY-001
**Requirement:** The system shall let only users who hold the workflow-execute permission record physicochemical results, and shall refuse a user who is not a member of the laboratory section that owns the test order.
**GxP:** Yes
**Serves:** 21 CFR 11.10(d); EU GMP Annex 11 clause 12 (to be confirmed by QA)
**Status:** Active

### URS-PHY-002
**Requirement:** The system shall require the analyst to enter their own password when a physicochemical result is recorded, and shall store the signature with the meaning "result recorded", the time and the comment, linked to the test order.
**GxP:** Yes
**Serves:** 21 CFR 11.50; 21 CFR 11.200(a); EU GMP Annex 11 clause 14 (to be confirmed by QA)
**Status:** Active

### URS-PHY-003
**Requirement:** The system shall calculate the reported value, the spread and the comparison with the specification for a measurement or gravimetric test on the server, from the readings entered, the replicate count, the evaluation basis and the specification configured by the Section Head.
**GxP:** Yes
**Serves:** 21 CFR 211.194(a)(5); EU GMP Annex 11 clause 4 (to be confirmed by QA)
**Status:** Active

### URS-PHY-004
**Requirement:** The system shall record a qualitative result as complies or does not comply, shall require an observation when a result does not comply, and shall not let the analyst record a qualitative result against a specification that is not qualitative.
**GxP:** Yes
**Serves:** 21 CFR 211.194(a); EU GMP Annex 11 clause 4 (to be confirmed by QA)
**Status:** Active

### URS-PHY-005
**Requirement:** The system shall evaluate a dissolution test by stages on the server, using the standard from a chromatography run to which the sample is assigned and whose system suitability has passed, and the acceptance offsets configured for the test, and shall hold the test open for a further stage when the earlier stage neither complies nor fails.
**GxP:** Yes
**Serves:** Dissolution acceptance testing, chapter pointer USP <711> (to be confirmed by QA)
**Status:** Active

### URS-PHY-006
**Requirement:** The system shall evaluate a disintegration test by stages on the server, using the unit counts and pass counts configured for the test, and shall judge the second stage by the limit and stage rules saved with the first stage.
**GxP:** Yes
**Serves:** Disintegration testing, chapter pointer USP <701> (to be confirmed by QA)
**Status:** Active

### URS-PHY-007
**Requirement:** The system shall evaluate a weight variation test for tablets and capsules on the server, using the unit counts, percentage bands and outside-limit counts configured for the test, and shall judge a second capsule stage by the criteria saved with the first stage.
**GxP:** Yes
**Serves:** Uniformity of dosage units, chapter pointer USP <905> (to be confirmed by QA)
**Status:** Active

### URS-PHY-008
**Requirement:** The system shall calculate a titration assay on the server, shall accept only titrant preparations and standard lots that are in date and standardised as the test method requires, and shall require a written justification when a titrant is due for standardisation.
**GxP:** Yes
**Serves:** 21 CFR 211.194(a)(2); EU GMP Annex 11 clause 4 (to be confirmed by QA)
**Status:** Active

### URS-PHY-009
**Requirement:** The system shall calculate a UV spectrophotometry result on the server from the absorbance, weight, constants configured in the Test Master and, where the method needs one, a standard lot that is in date and has recorded purity and moisture.
**GxP:** Yes
**Serves:** 21 CFR 211.194(a)(2); EU GMP Annex 11 clause 4 (to be confirmed by QA)
**Status:** Active

### URS-PHY-010
**Requirement:** The system shall let a user who holds the outsource permission send a physicochemical test to an external laboratory only with a signed decision that gives the laboratory and the reason, and shall then accept the result only from the external report uploaded for that test.
**GxP:** Yes
**Serves:** 21 CFR 211.194(a)(5); EU GMP Chapter 7 (to be confirmed by QA)
**Status:** Active

### URS-PHY-011
**Requirement:** The system shall accept equipment for a physicochemical result only when it is in service, belongs to the Physicochemical laboratory section and is of a type the test method allows.
**GxP:** Yes
**Serves:** 21 CFR 211.68; EU GMP Annex 15 (to be confirmed by QA)
**Status:** Active

### URS-PHY-012
**Requirement:** The system shall take the loss on drying or water value used for a dried or anhydrous basis result from the same sample's own recorded result, and shall not accept a value typed by the analyst.
**GxP:** Yes
**Serves:** 21 CFR 211.194(a); EU GMP Annex 11 clause 4 (to be confirmed by QA)
**Status:** Active

### URS-PHY-013
**Requirement:** The system shall refuse a second active result entry for the same test order, and shall refuse any result entry for a test order that is superseded or already at Ready or beyond.
**GxP:** Yes
**Serves:** 21 CFR 211.194(a); ALCOA+ data integrity (to be confirmed by QA)
**Status:** Active

### URS-PHY-014
**Requirement:** The system shall list in the testing workspace only the samples and test orders in the laboratory sections the user may read, and shall limit the Physicochemical workspace to the area (finished product, or raw and packaging material) the user may read.
**GxP:** Yes
**Serves:** 21 CFR 11.10(d); EU GMP Annex 11 clause 12 (to be confirmed by QA)
**Status:** Active

### URS-PHY-015
**Requirement:** The system shall show the recorded results of a test order only to a user who may read the laboratory section that owns the test order.
**GxP:** Yes
**Serves:** 21 CFR 11.10(d) (to be confirmed by QA)
**Status:** Active
