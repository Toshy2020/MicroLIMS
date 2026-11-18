# URS-RCV User Requirements Specification

| Field | Value |
|---|---|
| Document number | URS-RCV |
| Title | Sample receiving user requirements |
| Version | 0.4 |
| Status | Draft |
| Author | Claude (AI agent), draft from code |
| Reviewer | To be assigned by QA |
| Approver | To be assigned by QA |
| Effective date | Set when Approved |

## Change history

| Version | Date | Author | Change |
|---|---|---|---|
| 0.1 | 2026-11-12 | Claude (AI agent), draft from code | First draft, written from the code as built. |
| 0.2 | 2026-11-14 | AI coding agent, draft from code | Added batch sample receiving, duplicate batch+control rule, grid navigation/paste, and receiving last-choice defaults (URS-RCV-013, URS-RCV-014). |
| 0.3 | 2026-11-17 | AI coding agent, draft from code | Added bulk analyst assignment requirement (URS-RCV-015). |
| 0.4 | 2026-11-18 | AI coding agent, draft from code | Restricted analyst assignment, reassignment and unassignment to tests without an entered/submitted result (URS-RCV-011). |

## Requirements

Each requirement is a need that the code as built serves. Gaps found in the code are written in the FS entries and in `documents/_conflicts/CONF-task5.md`, not as requirements here. This document covers receiving of product, raw material and packaging material samples and the changes that can be made to a received sample. Water, environmental monitoring and after-cleaning receipt are in URS-WAT, URS-ENV and URS-ACL.

### URS-RCV-001
**Requirement:** The system shall let only users who hold a receiving permission register a product, raw material or packaging material sample, and shall limit a user who holds only the own-laboratory receiving permission to laboratories of which that user is a member.
**GxP:** Yes
**Serves:** 21 CFR 11.10(d); EU GMP Annex 11 clause 12 (to be confirmed by QA)
**Status:** Active

### URS-RCV-002
**Requirement:** The system shall give every received sample a reference number that is unique and that shows the sample category and the month and year of receipt.
**GxP:** Yes
**Serves:** 21 CFR 211.188; EU GMP Chapter 4 (to be confirmed by QA)
**Status:** Active

### URS-RCV-003
**Requirement:** The system shall create the test orders of a received sample from the tests the Section Head has configured for the item, shall refuse to receive a sample against an item that has no tests, and shall not let a user create a test order by hand at receipt.
**GxP:** Yes
**Serves:** EU GMP Annex 11 clause 4 (to be confirmed by QA)
**Status:** Active

### URS-RCV-004
**Requirement:** The system shall refuse to receive a sample when the mandatory receipt data (item, cause of testing, batch number, control number, sampled by, and for a finished product the production stage) is missing or the production stage is unknown.
**GxP:** Yes
**Serves:** 21 CFR 211.188; ALCOA+ complete (to be confirmed by QA)
**Status:** Active

### URS-RCV-005
**Requirement:** The system shall require the grade stated on the supplier's certificate when the item has graded specifications, and shall create only the tests that apply to that grade.
**GxP:** Yes
**Serves:** EU GMP Chapter 6 (to be confirmed by QA)
**Status:** Active

### URS-RCV-006
**Requirement:** The system shall warn the receiving user when the stated sample quantity is below the minimum quantity configured for the item.
**GxP:** Yes
**Serves:** EU GMP Chapter 6 (to be confirmed by QA)
**Status:** Active

### URS-RCV-007
**Requirement:** The system shall let a Section Head or Reviewer choose, with an electronic signature, which of the configured tests are run on a sample received for a periodic retest, one laboratory at a time.
**GxP:** Yes
**Serves:** 21 CFR 11.50, 11.200 (to be confirmed by QA)
**Status:** Active

### URS-RCV-008
**Requirement:** The system shall let a user who holds the receiving permission add the tests of a further laboratory to an already received sample, with a reason and an electronic signature, under the same reference number.
**GxP:** Yes
**Serves:** 21 CFR 11.50, 11.200 (to be confirmed by QA)
**Status:** Active

### URS-RCV-009
**Requirement:** The system shall let a user who holds the correct permission correct the details of a received sample until it is submitted for review, only with a reason and an electronic signature, and shall record the old and new value of every changed field.
**GxP:** Yes
**Serves:** 21 CFR 11.10(e), 11.50, 11.200; EU GMP Annex 11 clause 9 (to be confirmed by QA)
**Status:** Active

### URS-RCV-010
**Requirement:** The system shall let a user who holds the correct permission void a sample only with a reason and an electronic signature, and shall keep the voiding separate from a rejection.
**GxP:** Yes
**Serves:** 21 CFR 11.10(e), 11.50, 11.200 (to be confirmed by QA)
**Status:** Active

### URS-RCV-011
**Requirement:** The system shall let a user who holds the assign-analyst permission assign, reassign or unassign an analyst on each test of a sample only before a result is entered or submitted for that test (decided 2026-11-18), and shall allow only active, unlocked users of the Analyst, Reviewer or Section Head role who are members of the test's laboratory.
**GxP:** Yes
**Serves:** 21 CFR 11.10(d), 211.25 (to be confirmed by QA)
**Status:** Active

### URS-RCV-012
**Requirement:** The system shall record every change of analyst assignment with the previous analyst, the new analyst, the acting user, the time and the reason.
**GxP:** Yes
**Serves:** 21 CFR 11.10(e); ALCOA+ attributable (to be confirmed by QA)
**Status:** Active

### URS-RCV-013
**Requirement:** The system shall allow a permitted receiving user to submit a batch of 1 to 100 sample rows of a single receiving category in one all-or-nothing request, shall report row- and field-specific validation errors across all submitted rows before persisting any record, and shall refuse duplicate batch number and control number pairs within a Product batch receive, and shall refuse to receive an item-based sample whose batch number and control number pair is already registered on a sample that is not Cancelled or Voided (decided 2026-11-16).
**GxP:** Yes
**Serves:** 21 CFR 11.10(e); ALCOA+ accurate and complete (to be confirmed by QA)
**Status:** Active

### URS-RCV-014
**Requirement:** The multi-sample receiving grid shall support keyboard cell navigation, copy-down from the row above (`Ctrl+D`), positional spreadsheet paste with a preview that requires user acceptance before populating the grid (and nothing saved until the user saves), and pre-filling `Sampled By` and active `Cause of Testing` on the first row from the acting user's latest receipt when last-choice defaults are enabled.
**GxP:** No
**Serves:** Operational usability and entry-error reduction during multi-sample receiving
**Status:** Active

### URS-RCV-015
**Requirement:** The system shall let a user with the sample-assignment permission assign one analyst to up to 200 selected samples in one action; shall check each sample against the usual assignment rules when the request is processed; shall assign the samples that pass and report each refused sample with its reason; and shall record each change to a test order in the audit trail.
**GxP:** Yes
**Serves:** 21 CFR 11.10(d), 11.10(g) (to be confirmed by QA)
**Status:** Active
