# URS-SOL User Requirements Specification

| Field | Value |
|---|---|
| Document number | URS-SOL |
| Title | Solution preparation and titrant standardisation user requirements |
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

Each requirement below is a need that the code as built serves. Gaps found in the code are written in the FS entries and in `documents/_conflicts/CONF-task6.md`, not as requirements here. The use of a titrant in a titration assay is in URS-PHY. The use of a mobile phase in an HPLC run is in URS-INS.

### URS-SOL-001
**Requirement:** The system shall let only users who hold the solution-prepare permission start, change, complete, cancel or discard a solution preparation, limited to laboratory sections of which the user is a member, and shall show preparations only in the sections the user may read.
**GxP:** Yes
**Serves:** 21 CFR 11.10(d); EU GMP Annex 11 clause 12 (to be confirmed by QA)
**Status:** Active

### URS-SOL-002
**Requirement:** The system shall start a preparation only from an active solution master of the user's laboratory, shall keep a copy of the recipe as it was when the preparation started, and shall require an active method of the same laboratory that uses the solution when the solution is a mobile phase.
**GxP:** Yes
**Serves:** 21 CFR 211.100; EU GMP Annex 11 clause 4 (to be confirmed by QA)
**Status:** Active

### URS-SOL-003
**Requirement:** The system shall accept for each recipe line only a stock lot of the right master entry in the same laboratory that is released, not rejected, in date and in stock for the quantity used, or a prepared bottle of the right solution that is prepared, in date and, where the recipe needs it, released.
**GxP:** Yes
**Serves:** 21 CFR 211.80; EU GMP Annex 11 clause 4 (to be confirmed by QA)
**Status:** Active

### URS-SOL-004
**Requirement:** The system shall complete a preparation only when every line has its lot or bottle, the final volume is entered, the measured pH is within the recipe's tolerance where the recipe has one, and the autoclave record is complete where the recipe needs sterilisation, shall require the preparer's electronic signature, and shall then deduct the stock used, give the preparation a code and set its expiry from the recipe's shelf life.
**GxP:** Yes
**Serves:** 21 CFR 11.50; 21 CFR 211.100; EU GMP Annex 11 clause 14 (to be confirmed by QA)
**Status:** Active

### URS-SOL-005
**Requirement:** The system shall hold a completed preparation whose recipe needs release as pending review, shall not offer it for use until a reviewer has approved it with an electronic signature, and shall not let the person who prepared it release it.
**GxP:** Yes
**Serves:** 21 CFR 11.10(g); EU GMP Annex 11 clause 12 (to be confirmed by QA)
**Status:** Active

### URS-SOL-006
**Requirement:** The system shall treat a prepared solution as expired from its expiry time, shall not offer an expired, discarded or unreleased preparation for use, and shall require a reason to cancel or discard a preparation.
**GxP:** Yes
**Serves:** 21 CFR 211.94; ALCOA+ data integrity (to be confirmed by QA)
**Status:** Active

### URS-SOL-007
**Requirement:** The system shall record a titrant standardisation as a signed set of replicate titrations against a primary standard lot or against a standardised volumetric solution, shall calculate the factor of each replicate and the mean factor and relative standard deviation on the server, and shall judge the result against the factor range and maximum relative standard deviation saved with the preparation.
**GxP:** Yes
**Serves:** 21 CFR 211.194(a)(2); 21 CFR 11.50; Volumetric solutions, chapter pointer USP <541> (to be confirmed by QA)
**Status:** Active

### URS-SOL-008
**Requirement:** The system shall use as the current factor of a titrant only the latest passed standardisation, shall show it as valid, due or to be repeated before each use, and shall deduct the weighed primary standard from its stock lot when a standardisation is signed.
**GxP:** Yes
**Serves:** 21 CFR 211.194(c); EU GMP Annex 11 clause 4 (to be confirmed by QA)
**Status:** Active

### URS-SOL-009
**Requirement:** The system shall show which preparations consumed a given stock lot and which preparations are available for a given solution, method or free pick.
**GxP:** Yes
**Serves:** 21 CFR 211.188; EU GMP Chapter 4 (to be confirmed by QA)
**Status:** Active
