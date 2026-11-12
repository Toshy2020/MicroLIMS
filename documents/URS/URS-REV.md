# URS-REV User Requirements Specification

| Field | Value |
|---|---|
| Document number | URS-REV |
| Title | Sample review, approval and segregation of duties user requirements |
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

### URS-REV-001
**Requirement:** The system shall let a reviewer complete the review of a laboratory section only for a section to which the reviewer is assigned, only when the section is under review and all its tests have results recorded, and only after the reviewer has entered the user password as an electronic signature with the meaning "Reviewed".
**GxP:** Yes
**Serves:** 21 CFR 11.50, 11.200(a); EU GMP Annex 11 clause 14 (to be confirmed by QA)
**Status:** Active

### URS-REV-002
**Requirement:** The system shall refuse a review by a user who performed the test, where performed means being the assigned analyst, preparing the sample, or entering or submitting a result, observation or analysis for the test.
**GxP:** Yes
**Serves:** 21 CFR 11.10(d); EU GMP Annex 11 clause 12 (to be confirmed by QA)
**Status:** Active

### URS-REV-003
**Requirement:** The system shall put the tests of a laboratory section under review automatically when all current tests of that section have results recorded.
**GxP:** Yes
**Serves:** EU GMP Annex 11 clause 5 (to be confirmed by QA)
**Status:** Active

### URS-REV-004
**Requirement:** The system shall let a reviewer return a count test or an analysis test to the analyst before the section is reviewed, shall keep the superseded results, and shall record the reviewer, the analyst, the time and the reason given.
**GxP:** Yes
**Serves:** 21 CFR 11.10(e) (to be confirmed by QA)
**Status:** Active

### URS-REV-005
**Requirement:** The system shall let a Section Head decide the approval of a laboratory section (approve, reject, retest the retained sample, or request new samples) only for a section to which the Section Head is assigned, only when the section is under approval, and only after the Section Head has entered the user password as an electronic signature whose meaning follows the decision.
**GxP:** Yes
**Serves:** 21 CFR 11.50, 11.200(a); EU GMP Annex 11 clause 14 (to be confirmed by QA)
**Status:** Active

### URS-REV-006
**Requirement:** The system shall refuse an approval decision by the user who reviewed the section and by any user who performed a test of the section.
**GxP:** Yes
**Serves:** 21 CFR 11.10(d); EU GMP Annex 11 clause 12 (to be confirmed by QA)
**Status:** Active

### URS-REV-007
**Requirement:** The system shall refuse to approve a section when a test that requires system suitability has no linked chromatography run with a passed system suitability, or when a test that uses an analysis entry has no active entry or has a stage still pending.
**GxP:** Yes
**Serves:** USP <621> system suitability (to be confirmed by QA)
**Status:** Active

### URS-REV-008
**Requirement:** The system shall create retest samples, linked to the original sample, for the tests the Section Head selects; for a request for new samples it shall require two different analysts, neither of whom tested the original sample.
**GxP:** Yes
**Serves:** FDA guidance on investigating out-of-specification results (to be confirmed by QA)
**Status:** Active

### URS-REV-009
**Requirement:** The system shall carry the outcome of a closed retest sample back to the section of the original sample that was waiting for it.
**GxP:** Yes
**Serves:** FDA guidance on investigating out-of-specification results (to be confirmed by QA)
**Status:** Active

### URS-REV-010
**Requirement:** The system shall build and archive the report document of a sample when the sample is approved or rejected.
**GxP:** Yes
**Serves:** 21 CFR 11.10(b), 11.10(c) (to be confirmed by QA)
**Status:** Active

### URS-REV-011
**Requirement:** The system shall require, at the physicochemical review of a raw material, the quantity of retention sample kept with a unit of g, mg, kg, mL or L, and shall refuse a retention quantity at any other review.
**GxP:** Yes
**Serves:** 21 CFR 211.170 reserve samples (to be confirmed by QA)
**Status:** Active

### URS-REV-012
**Requirement:** The system shall identify a role that holds both a system-administration permission and a GxP execution or approval permission, so that such a role can be refused.
**GxP:** Yes
**Serves:** 21 CFR 11.10(d), 11.10(g); EU GMP Annex 11 clause 12 (to be confirmed by QA)
**Status:** Active
