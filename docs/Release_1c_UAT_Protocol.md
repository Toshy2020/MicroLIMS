# MicroLIMS Document Control — Release 1c
## Work Package 9: User Acceptance Testing (UAT) Protocol & Operational Scripts

- **Document Identifier:** `ML-DC-R1C-UATP-001`
- **Version:** 2.0 (Approved for WP9 Execution)
- **Module:** Document Control Module — Subsystem: Training & Reading Lists
- **Release:** MicroLIMS Release 1c
- **Controlled Change Reference:** `CC-DC-R1C-001`
- **Date:** September 4, 2026
- **CSV / GxP Authority:** GAMP 5, 21 CFR Part 11, EU GMP Annex 11, ALCOA+
- **Pre-Execution Baseline:** Implemented & Integration-Verified Baseline (WP1–WP8 Complete)

---

### 1. Protocol Objective & Operational Scope

The purpose of User Acceptance Testing (UAT) is to demonstrate that MicroLIMS Document Control Release 1c fulfills its intended operational use in realistic regulated microbiological laboratory workflows. 

UAT verifies end-to-end user journeys from the perspective of key laboratory roles:
1. **Section Head / Document Controller:** Managing curriculum training rules, reviewing matrix compliance, and managing document revisions.
2. **QC Microbiologist / Analyst:** Interacting with personal reading lists, viewing controlled SOPs in the viewer, recording informational progress, and consciously acknowledging documents.
3. **QA Compliance Director / Supervisor:** Tracking overdue training, monitoring escalation alerts, reviewing departmental compliance rankings, and analyzing superseded qualification gaps.

---

### 2. Operational UAT Scenarios (Scenarios UAT-1C-01 through UAT-1C-10)

| Scenario ID | User Persona & Role | Business Process / Scenario Title | Scope of Workflow Verification | Target URS Requirements |
|:---|:---|:---|:---|:---:|
| **UAT-1C-01** | Section Head (`SectionHead`) | Document Training Configuration & Curriculum Setup | Configure reading requirements, grace period (14 days), and role curriculum mapping for analytical SOPs. | `DC-URS-083`, `DC-URS-115`, `DC-URS-171` |
| **UAT-1C-02** | Analyst (`Analyst`) | Notification & Personal Reading List Inspection | Analyst logs in, navigates to "My Reading List", inspects due dates, countdown badges, and filters. | `DC-URS-085`, `DC-URS-088`, `DC-URS-089` |
| **UAT-1C-03** | Analyst (`Analyst`) | Controlled PDF Revision Viewing | Launch controlled viewer directly from list; verify official PDF watermark, revision header, and SHA-256 integrity. | `DC-URS-087` |
| **UAT-1C-04** | Analyst (`Analyst`) | Informational Reading Progress Recording | Analyst scrolls through document pages; progress bar reaches 100%; verify assignment is NOT auto-signed. | `DC-URS-190` |
| **UAT-1C-05** | Analyst (`Analyst`) | Conscious Read-and-Understand Electronic Acknowledgement | Complete reading, open acknowledgement modal, verify legal statement text, check box, submit, verify status transitions to `Qualified`. | `DC-URS-086`, `DC-URS-189`, `DC-URS-191`, `DC-URS-192` |
| **UAT-1C-06** | Supervisor (`Supervisor`) / QA | Overdue Training Identification & Escalation Alerts | View overdue assignment alerts in manager dashboard; inspect Level 1 supervisor escalation details. | `DC-URS-084`, `DC-URS-116` |
| **UAT-1C-07** | Compliance Officer (`Quality`) | Multi-Axis Training Matrix Audit Review | Inspect multi-axis grid; apply Department and Role filters; inspect color-coded badges (`Qualified`, `Pending`, `Overdue`). | `DC-URS-111`, `DC-URS-112`, `DC-URS-113` |
| **UAT-1C-08** | Executive / QA Lead (`Quality`) | Executive Compliance Dashboard & Departmental Rankings | Review overall compliance %, department ranking scorecard, and top overdue documents table. | `DC-URS-118`, `DC-URS-119`, `DC-URS-120` |
| **UAT-1C-09** | Document Controller / Analyst | Revision Supersession & Retraining Cascade Lifecycle | Promote Revision 02 to `Effective`; verify retraining assignment created for Revision 01 completers; verify `Trained on Superseded Only` gap alert. | `DC-URS-080`, `DC-URS-090`, `DC-URS-114`, `DC-URS-170`, `DC-URS-172`, `DC-URS-173`, `DC-URS-174` |
| **UAT-1C-10** | Document Controller (`SectionHead`) | Analytical Method Obsolescence & Training Cancellation | Obsolete analytical document master; verify open assignments transition to `Cancelled` while historical records remain immutable. | `DC-URS-175`, `DC-URS-090`, `DC-URS-173` |

---

### 3. Detailed UAT Script Steps & Pre-Defined Acceptance Criteria

*(Refer to `Release_1c_UAT_Execution_Record.md` for individual execution logs, step observations, and acceptance determinations).*
