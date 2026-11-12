# MicroLIMS Document Control — Release 1c
## Work Package 9: Operational Qualification (OQ) Execution Record

- **Document Identifier:** `ML-DC-R1C-OQE-001`
- **Release Version:** MicroLIMS v1.2.0-rel1c (WP9 Executed)
- **Work Package:** WP9 — Formal Operational Qualification (OQ) & User Acceptance Testing (UAT)
- **Execution Date:** September 4, 2026
- **Execution Authority:** Automated Controlled Systems Verification Agent / MicroLIMS Quality Assurance
- **Execution Environment:**
  - **Operating System:** Microsoft Windows 11 Enterprise
  - **Runtime Framework:** .NET 8.0.400 (`net8.0`), ASP.NET Core 8.0
  - **Frontend Client:** React 18, Vite v5.4.21, TypeScript 5.5 (`dist/assets/index-BUlBXqui.js`)
  - **Database Engine:** PostgreSQL 16 on `localhost:5432` (`microlims_doccontrol_wp1_test`)
  - **Database Migrations:** 11 migrations applied (`20260902181229_AddDocumentControlEntities` through `20260904180000_AddDocumentEscalationRecords`)
  - **Qualified Commit SHA:** `6fe5a61439c12af1c8bcd0f0bdc1018b01f48cab`
- **Authoritative Protocol:** `Release_1c_OQ_Protocol.md` (`ML-DC-R1C-OQP-001`)
- **Protocol Scope:** 18 Formal Operational Qualification Cases (`OQ-1C-01` through `OQ-1C-18`)
- **Execution Result:** **ALL 18 OQ CASES PASSED (100% PASS RATE)**

---

### 1. Operational Qualification Execution Summary Table (30 Implemented Requirements Traced)

| OQ Case ID | URS Requirement(s) Traced | Test Objective & Controlled Condition | Expected Result | Observed Result | Status | Deviation Ref | Evidence Reference |
|:---|:---|:---|:---|:---|:---:|:---:|:---|
| **OQ-1C-01** | `DC-URS-080`, `DC-URS-170`, `DC-URS-171` | Revision effective promotion triggers automated reading assignment cascade | Assigned to all active users mapped by curriculum/prior completion; audit event emitted | Revision 02 effective; assignments created; system audit `TrainingCascadeAssignmentsCreated` logged | **PASS** | None | `Release1cIntegrationVerificationPostgresTests.cs` (Scenario A, D) |
| **OQ-1C-02** | `DC-URS-081`, `DC-URS-082` | Manual individual and bulk group reading assignment distribution | Target users assigned; duplicate assignments skipped; reason captured | Assignments dispatched; active users verified; duplicates skipped | **PASS** | None | `DocumentControlTrainingAssignmentEngineUnitTests`, `DocumentControlRestApiUnitTests` |
| **OQ-1C-03** | `DC-URS-083`, `DC-URS-115` | Due date grace period hierarchy evaluation | Hierarchy: Curriculum (14d) > Master (21d) > Type > Default (14d) | Due date computed exactly from effective date + grace period hierarchy | **PASS** | None | `Release1cIntegrationVerificationPostgresTests.cs` (Scenario A) |
| **OQ-1C-04** | `DC-URS-084`, `DC-URS-116` | Automated overdue identification and multi-level escalation alert | Status transitions to `Overdue`; `DocumentEscalationRecord` created | Status updated to `Overdue`; Level 1 supervisor record inserted; alerts suppressed on rerun | **PASS** | None | `Release1cIntegrationVerificationPostgresTests.cs` (Scenario C) |
| **OQ-1C-05** | `DC-URS-085`, `DC-URS-088` | Personal reading list authenticated scoping and status filtering | Users see only own assignments; filters by status (`Assigned`, `Reading`, `Overdue`, etc.) | Only authenticated user's records returned; filters match exact subsets | **PASS** | None | `Release1cIntegrationVerificationPostgresTests.cs` (Scenario B, F) |
| **OQ-1C-06** | `DC-URS-087`, `DC-URS-190` | Controlled PDF viewer launch and informational reading progress | Viewer streams verified PDF; scrolling records reading progress without completion | PDF streamed with SHA-256 header; status set to `Reading`; 100% progress does NOT complete assignment | **PASS** | None | `Release1cIntegrationVerificationPostgresTests.cs` (Scenario B) |
| **OQ-1C-07** | `DC-URS-086`, `DC-URS-189`, `DC-URS-191`, `DC-URS-192` | Explicit conscious acknowledgement ceremony with legal statement | Mandatory checkbox; exact text stored; SHA-256 hash recorded; transitions to `Acknowledged` | Unchecked/proxy rejected; valid creates immutable `DocumentAcknowledgementRecord`; status `Acknowledged` | **PASS** | None | `Release1cIntegrationVerificationPostgresTests.cs` (Scenario B, G) |
| **OQ-1C-08** | `DC-URS-089` | Visual due-date countdown and overdue status alerts | Highlights days remaining/overdue; flags overdue state | Positive/negative delta exposed in DTO; overdue badge triggered accurately | **PASS** | None | `DocumentControlRestApiUnitTests`, `Release1cIntegrationVerificationPostgresTests.cs` |
| **OQ-1C-09** | `DC-URS-090`, `DC-URS-173` | Permanent historical retention of completed reading evidence | Historical completed records retained across retraining cycles; immutable | Prior revision assignment and acknowledgement intact after new revision activation | **PASS** | None | `Release1cIntegrationVerificationPostgresTests.cs` (Scenario D, E) |
| **OQ-1C-10** | `DC-URS-172` | Terminal closure of uncompleted superseded assignments | Open assignments on superseded revision closed with `SupersededIncomplete` | Open assignments transitioned to `SupersededIncomplete`; zero rows deleted | **PASS** | None | `Release1cIntegrationVerificationPostgresTests.cs` (Scenario D) |
| **OQ-1C-11** | `DC-URS-170` | Retraining assignment ancestry tracking (`SourceAssignmentId`) | New assignment links to prior completed assignment ID | `SourceAssignmentId` populated with prior assignment ID, establishing full audit trail | **PASS** | None | `Release1cIntegrationVerificationPostgresTests.cs` (Scenario D) |
| **OQ-1C-12** | `DC-URS-114`, `DC-URS-174` | Explicit flag and gap detection for superseded-only training | Users trained on prior revision but unacknowledged on current identified | Matrix cell evaluates `TrainedOnSupersededOnly`; gap warning badge displayed | **PASS** | None | `Release1cIntegrationVerificationPostgresTests.cs` (Scenario D) |
| **OQ-1C-13** | `DC-URS-175` | Document obsolescence training cancellation | Master obsoleted; open assignments cancelled; historical preserved | Open assignments transition to `Cancelled`; historical records unaffected | **PASS** | None | `Release1cIntegrationVerificationPostgresTests.cs` (Scenario E) |
| **OQ-1C-14** | `DC-URS-111`, `DC-URS-112`, `DC-URS-113` | Multi-axis Training Matrix grid compilation and filtering | Aggregates Personnel × Documents; dynamic cell statuses; filters by Dept/Role | Grid correctly compiled; statuses mapped (`Qualified`, `Pending`, `Overdue`, etc.); filters active | **PASS** | None | `Release1cIntegrationVerificationPostgresTests.cs` (Scenario D, F) |
| **OQ-1C-15** | `DC-URS-118`, `DC-URS-119`, `DC-URS-120` | Executive compliance KPIs, departmental rankings, top overdue | Computes compliance %; ranks departments; lists top overdue documents | Compliance rate computed mathematically; department table ranked; top overdue tallied | **PASS** | None | `Release1cIntegrationVerificationPostgresTests.cs` (Scenario D), `DocumentControlTrainingMatrixUnitTests` |
| **OQ-1C-16** | `DC-URS-085`, `DC-URS-086`, `DC-URS-111`, `DC-URS-113`, `DC-URS-192` | Security, Authorization & Segregation of Duties (SoD) | Cross-user compliance queries and proxy acknowledgements rejected | HTTP 403 returned on unauthorized matrix/reading queries and proxy acknowledgements | **PASS** | None | `Release1cIntegrationVerificationPostgresTests.cs` (Scenario F) |
| **OQ-1C-17** | `DC-URS-189`, `DC-URS-173` | PostgreSQL database trigger immutability defense | Direct raw SQL UPDATE and DELETE on `DocumentAcknowledgementRecords` blocked | Triggers raise PL/pgSQL exceptions; transactions rolled back; zero modifications | **PASS** | None | `Release1cIntegrationVerificationPostgresTests.cs` (Scenario G) |
| **OQ-1C-18** | `DC-URS-080`, `DC-URS-084`, `DC-URS-170` | Background worker automated execution and recovery idempotency | Automated promotion, cascade, and escalation run idempotently | Worker cycle promotes revisions and generates assignments; duplicate run creates 0 duplicates | **PASS** | None | `Release1cIntegrationVerificationPostgresTests.cs` (Scenario H) |

---

### 2. Traceability Verification to Implemented Baseline

- **Total Release 1c Requirements in Scope:** 35 Requirements
- **Total Requirements Formally Tested in OQ:** **30 Requirements**
- **Total Requirements Passed in OQ:** **30 of 30 (100%)**
- **Excluded Planned Requirements (Not Tested):** **5 Requirements** (`DC-URS-091`, `DC-URS-117`, `DC-URS-121`, `DC-URS-122`, `DC-URS-176`)
- **Open Deviations:** **0**

---

### 3. Operational Qualification Execution Conclusion

The Operational Qualification for MicroLIMS Document Control Release 1c was executed against live PostgreSQL 16 and verified backend services. All 18 protocol test cases achieved their pre-defined acceptance criteria with zero deviations and zero defects.

**OQ Ruling:** **PASSED AND VERIFIED.**
