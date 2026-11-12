# URS-WST User Requirements Specification

| Field | Value |
|---|---|
| Document number | URS-WST |
| Title | Working standard qualification user requirements |
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

Each requirement below is a need that the code as built serves. Gaps found in the code are written in the FS entries and in `documents/_conflicts/CONF-task6.md`, not as requirements here. The HPLC run on which the qualification assay is measured is described in URS-INS.

### URS-WST-001
**Requirement:** The system shall let only users who hold the working-standard qualify permission create or edit a qualification and attach documents to it, limited to laboratory sections of which the user is a member, and shall show qualifications and lots only in the sections the user may read.
**GxP:** Yes
**Serves:** 21 CFR 11.10(d); EU GMP Annex 11 clause 12 (to be confirmed by QA)
**Status:** Active

### URS-WST-002
**Requirement:** The system shall start an initial qualification only from an active reference standard master entry with a named source (a received raw material sample with an approved assay result, or a material name and batch with the first test report attached), and shall start a requalification only for an existing working standard lot that has no open qualification.
**GxP:** Yes
**Serves:** 21 CFR 211.194(c); EU GMP Annex 11 clause 4 (to be confirmed by QA)
**Status:** Active

### URS-WST-003
**Requirement:** The system shall let a qualification be assayed only on an HPLC run whose system suitability has passed and used a primary reference standard, with exactly the required number of replicates, and shall calculate the mean assay, the relative standard deviation, the potency on a dried basis and the pass or fail result on the server.
**GxP:** Yes
**Serves:** 21 CFR 211.194(a)(5); EU GMP Annex 11 clause 4 (to be confirmed by QA)
**Status:** Active

### URS-WST-004
**Requirement:** The system shall require the analyst's electronic signature when the qualification assay is submitted, and shall require the review and the approval to be signed with the reviewer's and the approver's own passwords.
**GxP:** Yes
**Serves:** 21 CFR 11.50; 21 CFR 11.200(a); EU GMP Annex 11 clause 14 (to be confirmed by QA)
**Status:** Active

### URS-WST-005
**Requirement:** The system shall not let the analyst who submitted a qualification review, approve or reject it, and shall not let the reviewer also approve or reject it at approval.
**GxP:** Yes
**Serves:** 21 CFR 11.10(g); EU GMP Annex 11 clause 12 (to be confirmed by QA)
**Status:** Active

### URS-WST-006
**Requirement:** The system shall, when a passed qualification is approved, create the working standard lot (initial) or update its purity, moisture and expiry (requalification), with the measured potency as purity and a fixed validity period, and shall refuse to approve a failed qualification.
**GxP:** Yes
**Serves:** 21 CFR 211.194(c); EU GMP Annex 11 clause 4 (to be confirmed by QA)
**Status:** Active

### URS-WST-007
**Requirement:** The system shall show each working standard lot with a status of Valid, Due soon, Expired or Depleted, and the open qualification of the lot if one exists.
**GxP:** Yes
**Serves:** 21 CFR 211.194(c) (to be confirmed by QA)
**Status:** Active

### URS-WST-008
**Requirement:** The system shall let an analyst return an assayed qualification to draft or reject it with a stated reason, and shall keep the reason on the record.
**GxP:** Yes
**Serves:** ALCOA+ data integrity (to be confirmed by QA)
**Status:** Active
