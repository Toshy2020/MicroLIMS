# URS-ACL User Requirements Specification

| Field | Value |
|---|---|
| Document number | URS-ACL |
| Title | After-cleaning verification user requirements |
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

Each requirement is a need that the code as built serves. Gaps found in the code are written in the FS entries and in `documents/_conflicts/CONF-task5.md`, not as requirements here. Microbiological count recording for after-cleaning samples is shared with other areas and is in URS-MIC. The HPLC run itself is in URS-INS.

### URS-ACL-001
**Requirement:** The system shall let only users who hold the own-laboratory receiving permission register an after-cleaning sample, only for an active machine, in a laboratory section the user is allowed to create records in, and only with the previous product's batch number.
**GxP:** Yes
**Serves:** 21 CFR 211.67; EU GMP Chapter 5 (to be confirmed by QA)
**Status:** Active

### URS-ACL-002
**Requirement:** The system shall record the previous product of an after-cleaning sample, either from an active finished-product item or, for microbiology, as text, and shall require the item and at least one cleaning technique (UV, HPLC or ICP) when the sample is received in the physicochemical laboratory.
**GxP:** Yes
**Serves:** 21 CFR 211.67; EU GMP Chapter 5 (to be confirmed by QA)
**Status:** Active

### URS-ACL-003
**Requirement:** The system shall give each after-cleaning sample a unique reference number, and when two receipts collide shall either save one with the next number or save nothing and tell the user.
**GxP:** Yes
**Serves:** 21 CFR 211.188 (to be confirmed by QA)
**Status:** Active

### URS-ACL-004
**Requirement:** The system shall let a user prepare a received after-cleaning sample once, by choosing active machine part configurations of the sample's own machine, and shall create the test orders and one location per chosen part, using only tests of the sample's laboratory.
**GxP:** Yes
**Serves:** EU GMP Annex 11 clause 4 (to be confirmed by QA)
**Status:** Active

### URS-ACL-005
**Requirement:** In the physicochemical laboratory the system shall allow only tests whose technique was chosen at receipt, and shall allow an HPLC cleaning test only when a cleaning HPLC method exists for the previous product.
**GxP:** Yes
**Serves:** 21 CFR 211.67 (to be confirmed by QA)
**Status:** Active

### URS-ACL-006
**Requirement:** The system shall calculate the HPLC cleaning residue of each analyte from the sample responses, the mean standard response, the standard concentration, the extraction volume and, for a per-area limit, the swabbed area, and shall evaluate the mean against the item's specification.
**GxP:** Yes
**Serves:** 21 CFR 211.67; EU GMP Annex 15 (to be confirmed by QA)
**Status:** Active

### URS-ACL-007
**Requirement:** The system shall compare the count of each swab or rinse location with the alert, action and specification limits that the Section Head configured for that machine part, and shall report the most severe limit exceeded.
**GxP:** Yes
**Serves:** 21 CFR 211.67 (to be confirmed by QA)
**Status:** Active

### URS-ACL-008
**Requirement:** The system shall not let an after-cleaning test order go forward to review while it is waiting, running or incubating and results for every location have not been entered.
**GxP:** Yes
**Serves:** 21 CFR 211.194(a) (to be confirmed by QA)
**Status:** Active
