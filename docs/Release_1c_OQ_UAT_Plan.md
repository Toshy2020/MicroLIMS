# MicroLIMS Document Control — Release 1c
## Formal Operational Qualification (OQ) & User Acceptance Testing (UAT) Plan

- **Document Identifier:** `ML-DC-R1C-OQP-001`
- **Release Version:** MicroLIMS v1.2.0-rel1c (Controlled Planning Protocol)
- **Module:** Document Control Module — Subsystem: Training & Reading Lists
- **Date:** 2026-09-04
- **Protocol Status:** **PLANNING ONLY (DO NOT EXECUTE UNTIL AUTHORIZED)**
- **Authoritative Baseline:** MicroLIMS Document Control URS v1.1 (`DC-URS-080`..`091`, `DC-URS-111`..`122`, `DC-URS-170`..`176`, `DC-URS-189`..`192`)
- **Scope:** 10 Formal OQ Protocols / 3 Realistic Regulated UAT Scenarios

---

### 1. Protocol Purpose & Execution Rules

This document specifies the formal test cases for Operational Qualification (OQ) and User Acceptance Testing (UAT) for Release 1c. 

**MANDATORY QUALIFICATION RULES:**
1. **No Pre-Execution / No Fabrication:** This document defines the protocol specification only. No test cases are executed, and no results are marked or fabricated during this planning task.
2. **Post-Implementation Execution:** Execution shall occur only after Work Packages 1 through 7 are complete, integrated, and verified in the staging environment.
3. **Traceability Binding:** Every protocol step maps directly to specific URS and FRS requirements in `ML-DC-RTM-1C-001`.

---

### 2. Operational Qualification (OQ) Protocol Specifications (10 Protocols)

| Protocol ID | Protocol Title & Target URS | Planned Test Procedure & Injected Conditions | Planned Acceptance Criteria | Pre-Planned Status |
|:---:|:---|:---|:---|:---:|
| **OQ-1C-01** | **Automatic Training Cascade on Effective Revision**<br>(`DC-URS-080`, `DC-URS-170`, `DC-URS-171`) | Seed Document Master with Rev 01 (Effective). Complete training for 3 analysts. Create Rev 02 (Future Effective). Trigger worker transition to Effective. | System automatically generates Rev 02 reading assignments for all 3 prior completers. Due date equals `UtcNow + GracePeriod`. | `[ ] PLANNED` |
| **OQ-1C-02** | **Manual & Role Curricula Assignment Distribution**<br>(`DC-URS-081`, `DC-URS-082`, `DC-URS-115`, `DC-URS-117`) | As Document Controller, assign effective SOP to "Microbiology Analysts" role curriculum. Create new user with Analyst role. | All current analysts receive assignment. Newly onboarded analyst automatically receives assignment upon account creation. | `[ ] PLANNED` |
| **OQ-1C-03** | **Due Date Calculation & Overdue Status Transitions**<br>(`DC-URS-083`, `DC-URS-084`, `DC-URS-116`) | Create assignment with due date set to $T - 1\text{ hour}$ (in the past). Query overdue status API and inspect manager alert view. | Assignment is flagged as `Overdue`; red alert badge displayed; overdue manager dashboard card increments. | `[ ] PLANNED` |
| **OQ-1C-04** | **Controlled Read-and-Understand Acknowledgement Ceremony**<br>(`DC-URS-086`, `DC-URS-189`, `DC-URS-191`, `DC-URS-192`) | User logs in, views assigned PDF, checks legal confirmation statement, and submits. Attempt submission without checkbox, and attempt proxy submission by peer. | Unchecked submission is blocked; proxy submission rejected with HTTP 403; valid submission captures UTC timestamp, user ID, and exact statement text. | `[ ] PLANNED` |
| **OQ-1C-05** | **Informational Reading Progress Isolation**<br>(`DC-URS-190`) | Open Controlled PDF Viewer. Scroll through 100% of document pages without clicking formal acknowledgement. | Viewer displays 100% reading progress; assignment status remains `Assigned` (not completed); formal acknowledgement required. | `[ ] PLANNED` |
| **OQ-1C-06** | **Permanent Retention of Revision-Specific Evidence**<br>(`DC-URS-090`, `DC-URS-173`) | User A completes Rev 01. Rev 02 becomes effective. Rev 03 becomes effective. Query User A qualification history. | Historical record for Rev 01 remains `Acknowledged` with original timestamp and statement text; no records overwritten or updated. | `[ ] PLANNED` |
| **OQ-1C-07** | **Terminal Closure of Incomplete Superseded Assignments**<br>(`DC-URS-172`, `DC-URS-175`, `DC-URS-184`) | User B has open assignment for Rev 01. Rev 02 becomes effective. Document Master is obsoleted. | Rev 01 assignment transitions to terminal state `SupersededIncomplete` with reason; open assignments on obsolete doc transition to `Cancelled`; zero rows deleted. | `[ ] PLANNED` |
| **OQ-1C-08** | **Training Matrix Multi-Axis Grid & Filter Performance**<br>(`DC-URS-111`, `DC-URS-112`, `DC-URS-113`) | Seed 200 users and 50 documents. Open Training Matrix page. Apply Department and Status filters. | Grid renders within <2.0s; color-coded cell states (`Qualified`, `Pending`, `Overdue`, `NotAssigned`) match database records exactly. | `[ ] PLANNED` |
| **OQ-1C-09** | **Identification of Superseded-Only Qualified Personnel**<br>(`DC-URS-114`, `DC-URS-174`) | User C completed Rev 01 but has not acknowledged newly effective Rev 02. Query Training Matrix with "Superseded Gap" filter. | User C is displayed with high-visibility orange warning tag `Trained on Superseded Only`; cell details show Rev 01 completion date. | `[ ] PLANNED` |
| **OQ-1C-10** | **Retraining Assignment Re-Issuance Rules**<br>(`DC-URS-176`) | Trigger retraining re-issuance for a failed or invalidated assignment according to configured document type policy. | System generates a new assignment record linked to the same revision, preserving prior attempt in history. | `[ ] PLANNED` |

---

### 3. User Acceptance Testing (UAT) Scenario Specifications (3 Scenarios)

#### UAT-1C-01: Microbiologist Daily Reading & Acknowledgement Workflow
- **Persona:** Sarah Jenkins (QC Microbiologist / Analyst).
- **Business Scenario:** Sarah logs into MicroLIMS at the start of her shift. She notices a notification badge on "My Reading List" indicating that a revised standard operating procedure, `SOP-MB-0042` (*Bioburden Testing of Purified Water*), has been assigned to her.
- **Planned Test Steps:**
  1. Sarah navigates to `/document-control/my-reading`.
  2. She reviews the due date countdown (14 days remaining) and document metadata.
  3. She clicks "Read Document", which launches the Controlled PDF Viewer displaying the official effective document with header watermark.
  4. She reads the document and clicks "Complete Reading".
  5. The system presents the formal legal acknowledgement statement: *"I confirm that I have read, understood, and agree to comply with SOP-MB-0042 Rev 02."*
  6. Sarah checks the confirmation box and enters optional comments.
  7. She clicks "Submit Acknowledgement".
- **Planned Acceptance Criteria:**
  - `SOP-MB-0042` immediately moves to Sarah's "Completed Reading" tab.
  - Audit trail immutably records her user ID, server UTC timestamp, and the statement text.
  - Her cell in the Training Matrix transitions to Green (`Qualified`).
- **Status:** `[ ] PLANNED FOR FORMAL UAT EXECUTION`

---

#### UAT-1C-02: Document Controller Revision Cascade & Supersedence Management
- **Persona:** David Chen (Document Controller).
- **Business Scenario:** A critical method update requires issuing Revision 03 of `SOP-MB-0015` (*Media Preparation and Growth Promotion Testing*). David needs to ensure that all 12 qualified media analysts are automatically assigned the new revision while preserving their historical qualification records.
- **Planned Test Steps:**
  1. David creates and approves Revision 03 under standard Release 1b workflow.
  2. Revision 03 becomes `Effective`.
  3. The background worker triggers the Release 1c Training Cascade.
  4. David opens the Training Matrix and filters for `SOP-MB-0015`.
- **Planned Acceptance Criteria:**
  - All 12 analysts who completed Revision 02 receive a new `Assigned` reading task for Revision 03.
  - The 1 analyst who had an incomplete assignment for Revision 02 sees that task closed as `SupersededIncomplete`.
  - The matrix displays all 12 analysts with the `Trained on Superseded Only` alert until they complete Revision 03.
  - Historical records for Revision 02 remain permanently accessible in employee training transcripts.
- **Status:** `[ ] PLANNED FOR FORMAL UAT EXECUTION`

---

#### UAT-1C-03: Quality Assurance Audit & Self-Auditing Matrix Export
- **Persona:** Dr. Elena Vance (QA Compliance Director).
- **Business Scenario:** During a regulatory inspection, Dr. Vance must demonstrate to an auditor that the laboratory is 100% compliant with current effective analytical SOPs and provide an attributable training export.
- **Planned Test Steps:**
  1. Dr. Vance navigates to the Document Control Compliance Dashboard.
  2. She inspects the executive KPI card showing "Overall Document Training Compliance: 96.8%".
  3. She opens the Training Matrix, filters by "Microbiology Department", and selects "Export Compliance Record".
  4. The system generates a cryptographic SHA-256 hash of the exported dataset and prompts for download.
  5. Dr. Vance downloads `MicroLIMS_TrainingMatrix_Microbiology_20260904.csv`.
  6. She opens the system Audit Trail to verify the export event.
- **Planned Acceptance Criteria:**
  - Downloaded CSV contains complete user, document, revision, status, and completion timestamp data.
  - Audit log contains event `TrainingMatrixExported` attributing the export to Dr. Vance with the identical SHA-256 hash.
- **Status:** `[ ] PLANNED FOR FORMAL UAT EXECUTION`

---

### 4. Protocol Sign-Off & Execution Hold Notice

```
====================================================================================================
RELEASE 1c OQ / UAT PROTOCOL AUTHORIZATION (PRE-EXECUTION)
====================================================================================================
Protocol Author (CSV Specialist): [PREPARED & BASELINED]        Date: 2026-09-04
Technical Lead Reviewer:          [PENDING IMPLEMENTATION]      Date: ________________________
QA Lead Approval:                 [PENDING IMPLEMENTATION]      Date: ________________________

HOLD DIRECTIVE: EXECUTION PROHIBITED UNTIL WP1-WP7 COMPLETION AND FORMAL TEST AUTHORIZATION.
====================================================================================================
```
