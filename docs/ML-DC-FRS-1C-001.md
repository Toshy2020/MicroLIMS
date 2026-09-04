# MicroLIMS Document Control — Release 1c
## Functional Requirements Specification (FRS): Training Matrix & Reading Lists

- **Document Identifier:** `ML-DC-FRS-1C-001`
- **Release:** Release 1c (Controlled Planning Baseline)
- **Module:** Document Control Module — Subsystem: Training & Reading Lists
- **Effective Date:** 2026-09-04
- **Authoritative Baseline:** MicroLIMS Document Control URS v1.1 (`DC-URS-080`..`091`, `DC-URS-111`..`122`, `DC-URS-170`..`176`, `DC-URS-189`..`192`)
- **Validation Standard:** GAMP 5, 21 CFR Part 11, EU GMP Annex 11, ICH Q9

---

### 1. Functional Specification Overview

This Functional Requirements Specification (FRS) translates thirty-five (35) User Requirements Specification (URS) items into unambiguous, verifiable engineering behaviors for **Release 1c**.

Release 1c manages the complete personnel document qualification lifecycle:
1. **Curricula & Assignment Engine:** Automatically creating reading assignments when document revisions become effective or assigning ad-hoc curricula.
2. **My Reading List Portal:** Delivering controlled documents to individual users with status tracking and due date countdowns.
3. **Controlled Acknowledgement Ceremony:** Capturing unambiguous read-and-understand evidence tied to exact document revision IDs.
4. **Training Matrix:** Visualizing organizational compliance across Users × Documents × Revisions, identifying gaps (e.g., personnel trained only on superseded revisions).
5. **Historical Retention:** Preserving training evidence immutably across document revisions.

---

### 2. Detailed Functional Specifications

#### FS-1c-001: Automatic Reading Assignment Generation & Training Cascade
- **Covered URS Requirements:** `DC-URS-080`, `DC-URS-170`, `DC-URS-171`
- **Functional Behavior:** When a document revision transitions to `Effective`, the system checks `DocumentTrainingConfiguration`. If enabled, the system automatically creates `ReadingAssignment` records for all active users assigned to the previous effective revision or belonging to mapped curricula.
- **Actor:** System (`system:effective-date-worker` or `DocumentEffectiveDateService`) / Document Controller.
- **Preconditions:** Document revision is in `Effective` state; `DocumentTrainingConfiguration.RequiresReading = true`.
- **Inputs:** `DocumentRevisionId`, configured target population (prior revision completers or role curricula).
- **Processing:**
  1. Identify target users based on configuration rules.
  2. For each active user, generate a new `ReadingAssignment` record with `Status = Assigned`, `AssignedAtUtc = UtcNow`, `DueDateUtc = UtcNow + GracePeriodDays`.
  3. Emit audit event `ReadingAssignmentsCreatedBatch`.
- **Outputs:** Persisted `ReadingAssignment` records; badge notification incremented for users.
- **Workflow Status:** `Draft` / `Approved` $\rightarrow$ `Effective` triggers assignment generation; assignment status starts at `Assigned`.
- **Authorization:** Automated worker executes under system credentials. Manual bulk generation requires `DocControl_Admin` or `DocController`.
- **Audit Trail:** Attributable audit record logging total assignments created, revision ID, and rule basis.
- **Segregation of Duties:** Authors and Document Controllers do not receive automatic training exemptions.
- **Data Integrity:** Idempotent processing prevents duplicate assignment creation for the same user and revision.
- **Acceptance Criteria:** 100% of target users receive an assignment; zero duplicate records created; due dates correctly offset.
- **Verification Method:** Automated Integration Test & OQ Protocol.

---

#### FS-1c-002: Manual & Role Curricula Assignment Distribution
- **Covered URS Requirements:** `DC-URS-081`, `DC-URS-082`, `DC-URS-115`, `DC-URS-117`
- **Functional Behavior:** Authorized Document Controllers can assign a controlled document revision to individual users, departments, or role groups.
- **Actor:** Document Controller, QA Manager, Section Head.
- **Preconditions:** Target document revision is currently `Effective`; target users are active.
- **Inputs:** `DocumentRevisionId`, array of `UserIds`, optional custom `DueDateUtc`, assignment reason.
- **Processing:** Validate target revision is effective; create `ReadingAssignment` records; skip users already holding an active or completed assignment for that exact revision.
- **Outputs:** Success response with count of assigned users; notifications queued.
- **Workflow Status:** New assignment created in `Assigned` status.
- **Authorization:** Requires permission `doccontrol.training.assign`.
- **Audit Trail:** Action logged with performing user ID, recipient user IDs, revision ID, and justification.
- **Segregation of Duties:** No user can assign training to themselves without designated managerial authority.
- **Data Integrity:** Relational foreign keys enforce valid user and revision IDs.
- **Acceptance Criteria:** Unauthorized users receive HTTP 403; assigned users immediately see assignment in "My Reading List".
- **Verification Method:** Unit Tests & OQ-1C-02.

---

#### FS-1c-003: Due Date Calculation & Overdue Status Transitions
- **Covered URS Requirements:** `DC-URS-083`, `DC-URS-084`, `DC-URS-116`
- **Functional Behavior:** Assignments track due dates based on configured grace periods. When `DueDateUtc < DateTime.UtcNow` and status is not terminal, status automatically reflects `Overdue`.
- **Actor:** Background Worker / Query Evaluator.
- **Preconditions:** Active assignment in `Assigned` or `InReview` state.
- **Inputs:** System UTC clock source.
- **Processing:** Filter active assignments where `DueDateUtc < UtcNow`; transition or compute display status as `Overdue`; flag record in Training Matrix.
- **Outputs:** Overdue visual alerts, email reminders, and KPI metrics.
- **Workflow Status:** `Assigned` $\rightarrow$ `Overdue`.
- **Authorization:** System automated query or worker evaluation.
- **Audit Trail:** Transition to overdue logged or dynamically derived with audit traceability.
- **Segregation of Duties:** N/A (Automated time-based logic).
- **Data Integrity:** UTC time standardized across hosts prevents timezone calculation discrepancies.
- **Acceptance Criteria:** Overdue status calculated accurately to the second; overdue badge rendered in red.
- **Verification Method:** Unit Test Suite & OQ-1C-03.

---

#### FS-1c-004: "My Reading List" User Portal
- **Covered URS Requirements:** `DC-URS-085`, `DC-URS-087`, `DC-URS-088`
- **Functional Behavior:** Authenticated users access a personalized portal displaying all assigned documents requiring read-and-understand acknowledgement, categorized by `Pending`, `Overdue`, and `Completed`.
- **Actor:** Any authenticated laboratory user (Analyst, Reviewer, Approver, etc.).
- **Preconditions:** User is logged in with active JWT token.
- **Inputs:** HTTP `GET /api/document-control/my-reading-list`.
- **Processing:** Query `ReadingAssignments` where `AssignedUserId == CurrentUserId` and `DocumentRevision.Master.RecordStatus != Voided`. Return document metadata, revision number, due date, status, and PDF viewing URL.
- **Outputs:** JSON list of user assignments; responsive UI table with direct link to Controlled PDF Viewer.
- **Workflow Status:** Reads current status of user assignments.
- **Authorization:** Open to all authenticated users for their own assignments (`OwnRecordsOnly`).
- **Audit Trail:** Access to reading list logged in standard application telemetry.
- **Segregation of Duties:** Users can only view their own reading assignments unless granted `doccontrol.training.view_all` permission.
- **Data Integrity:** Ensures voided document masters are excluded from active reading lists per `DC-URS-187`.
- **Acceptance Criteria:** User sees exactly their assignments; clicking item opens the exact assigned revision PDF.
- **Verification Method:** Automated API Integration Test & UAT-1C-01.

---

#### FS-1c-005: Controlled Read-and-Understand Acknowledgement Ceremony
- **Covered URS Requirements:** `DC-URS-086`, `DC-URS-189`, `DC-URS-191`, `DC-URS-192`
- **Functional Behavior:** To complete reading, the user must view the controlled PDF and submit a formal acknowledgement. The system records the user ID, exact UTC timestamp, and the exact statement text presented.
- **Actor:** Assigned User.
- **Preconditions:** Assignment exists in `Assigned` or `Overdue` state for `CurrentUserId`; user has opened the PDF.
- **Inputs:** `AssignmentId`, confirmation checkbox, optional feedback comments.
- **Processing:**
  1. Validate that the assignment belongs to `CurrentUserId`.
  2. Verify that the document revision assigned matches the active displayed revision (`DC-URS-192`).
  3. Capture the exact configurable legal statement text (e.g., *"I confirm that I have read, understood, and agree to comply with the contents of this controlled document revision."*).
  4. Set `Status = Acknowledged`, `AcknowledgedAtUtc = UtcNow`, `StatementText = configuredText`.
  5. Emit immutable audit event `ReadingAssignmentAcknowledged`.
- **Outputs:** Assignment marked completed; record rendered in user's completed history.
- **Workflow Status:** `Assigned` / `Overdue` $\rightarrow$ `Acknowledged` (Terminal successful state).
- **Authorization:** Restricted strictly to the assigned user (`CurrentUserId == AssignedUserId`).
- **Audit Trail:** Attributable audit entry with user ID, revision ID, timestamp, and statement text.
- **Segregation of Duties:** No supervisor, administrator, or peer can acknowledge reading on behalf of another user.
- **Data Integrity:** Relational constraint prevents acknowledging an unassigned revision; record becomes append-only upon completion.
- **Acceptance Criteria:** User cannot acknowledge without checking confirmation; timestamp is strictly server UTC; statement text is permanently preserved.
- **Verification Method:** Integration Test & OQ-1C-04.

---

#### FS-1c-006: Informational Reading Progress Indicators
- **Covered URS Requirements:** `DC-URS-190`
- **Functional Behavior:** The Controlled PDF Viewer may display informational page scroll or reading progress (e.g., "Page 5 of 12 read"), but progress percentage is **strictly informational** and shall never be used as a programmatic acceptance criterion to block or grant completion. Completion is governed solely by explicit user acknowledgement per `FS-1c-005`.
- **Actor:** User / Frontend UI.
- **Preconditions:** PDF open in `ControlledPdfViewer`.
- **Inputs:** User scroll and page change events.
- **Processing:** Calculate client-side scroll percentage; render informational progress bar.
- **Outputs:** Informational progress bar with clear label: *"Informational Only — Formal Acknowledgement Required"*.
- **Workflow Status:** State unaffected by progress value.
- **Authorization:** Available to reader.
- **Audit Trail:** Scroll telemetry not audited as a GxP event.
- **Segregation of Duties:** N/A.
- **Data Integrity:** Protects against false compliance where automated scrolling is mistaken for regulatory comprehension.
- **Acceptance Criteria:** User with 100% scroll is not marked completed until formal acknowledgement; user can acknowledge even if scroll tracker fails.
- **Verification Method:** Code Inspection & OQ-1C-05.

---

#### FS-1c-007: Revision-Specific Historical Evidence Retention
- **Covered URS Requirements:** `DC-URS-090`, `DC-URS-173`
- **Functional Behavior:** When a document revision is superseded by a newer revision, all completed reading and training records for the superseded revision remain completely untouched, immutable, and fully retrievable in historical queries.
- **Actor:** System / Auditor / QA Viewer.
- **Preconditions:** User completed reading for revision `v01`; revision `v02` becomes effective.
- **Inputs:** Query historical training records for user or document.
- **Processing:** Retain all `ReadingAssignment` records where `DocumentRevisionId == v01.Id` and `Status == Acknowledged`. New assignments for `v02` are created as separate independent records.
- **Outputs:** Complete chronological qualification transcript for the employee.
- **Workflow Status:** Status remains `Acknowledged` indefinitely.
- **Authorization:** Read-only access to authorized auditors and managers.
- **Audit Trail:** Historical records protected by append-only triggers.
- **Segregation of Duties:** N/A.
- **Data Integrity:** Historical training records can never be updated, overwritten, or re-associated with a different revision ID.
- **Acceptance Criteria:** Inspection of historical records confirms 100% data preservation after 10+ successive revisions.
- **Verification Method:** Database Integration Test & OQ-1C-06.

---

#### FS-1c-008: Terminal Closure of Incomplete Superseded Assignments
- **Covered URS Requirements:** `DC-URS-172`, `DC-URS-175`, `DC-URS-184`
- **Functional Behavior:** If a document revision is superseded or voided/obsoleted while open reading assignments remain incomplete, the system automatically transitions those incomplete assignments to a terminal status (`SupersededIncomplete` or `Cancelled`) with a recorded reason. Records are **never deleted**.
- **Actor:** System (`DocumentEffectiveDateWorker`) or Document Controller (Obsolescence).
- **Preconditions:** Open assignment (`Assigned` or `Overdue`) exists for revision $R_A$; revision $R_B$ becomes effective or $R_A$ becomes obsolete.
- **Inputs:** Event `DocumentRevisionSuperseded` or `DocumentObsoleted`.
- **Processing:**
  1. Locate open assignments for superseded/obsolete revision.
  2. Transition status to `SupersededIncomplete` (or `Cancelled`).
  3. Record `ClosedReason = "Superseded by Revision {NewRevNumber}"` or `"Document Obsoleted"`.
  4. Record `ClosedAtUtc = UtcNow`.
  5. Emit audit event `ReadingAssignmentClosedDueToSupersedence`.
- **Outputs:** Incomplete assignment removed from user's active reading list, moved to historical archive.
- **Workflow Status:** `Assigned` / `Overdue` $\rightarrow$ `SupersededIncomplete`.
- **Authorization:** Automated system action or authorized controller.
- **Audit Trail:** Detailed audit entry recording transition reason and system actor.
- **Segregation of Duties:** N/A.
- **Data Integrity:** Zero hard deletes; full historical accountability of non-completed training.
- **Acceptance Criteria:** No open assignments remain for superseded revisions; zero records deleted from database.
- **Verification Method:** Integration Test & OQ-1C-07.

---

#### FS-1c-009: Training Matrix Grid & Multi-Axis Compliance Inspection
- **Covered URS Requirements:** `DC-URS-111`, `DC-URS-112`, `DC-URS-113`
- **Functional Behavior:** The Training Matrix provides a multi-axis matrix interface displaying Users (rows) versus Controlled Documents/Revisions (columns). Each cell indicates qualification status using standardized visual indicators:
  - `Qualified` (Green checkmark): Completed reading on current effective revision.
  - `Pending` (Yellow clock): Assigned to current effective revision, not yet overdue.
  - `Overdue` (Red exclamation): Assigned and past due date.
  - `Trained on Superseded Only` (Orange warning): Qualified on prior revision, not yet on current.
  - `Not Assigned` (Gray dash): Not required or not assigned.
- **Actor:** Document Controller, QA Compliance Lead, Lab Manager, Auditor.
- **Preconditions:** User has `doccontrol.training.view_matrix` permission.
- **Inputs:** Filter criteria: Department, Role, Document Type, Compliance Status.
- **Processing:** Aggregate active users, active documents, current effective revisions, and latest `ReadingAssignment` records. Compute cell matrix states.
- **Outputs:** Interactive grid with sticky headers, color-coded cell statuses, cell drilldown drawer showing assignment details, and CSV export.
- **Workflow Status:** Read-only compliance visualization.
- **Authorization:** Requires QA Viewer, Document Controller, or Manager role.
- **Audit Trail:** Matrix queries and CSV exports emit `TrainingMatrixViewed` and `TrainingMatrixExported` audit events.
- **Segregation of Duties:** Standard analysts can only view their own department's matrix if permitted.
- **Data Integrity:** Real-time calculation guarantees zero stale compliance data.
- **Acceptance Criteria:** Matrix accurately reflects all cell states; sorting and filtering operate within <2 seconds for 500 users × 100 documents.
- **Verification Method:** Performance Test & OQ-1C-08.

---

#### FS-1c-010: Identification of Personnel Qualified Only on Superseded Revisions
- **Covered URS Requirements:** `DC-URS-114`, `DC-URS-174`
- **Functional Behavior:** The system specifically computes and highlights personnel who were qualified on a prior revision of a document but have not yet completed training on the currently effective revision.
- **Actor:** Quality Assurance Auditor, Section Head.
- **Preconditions:** Document has $\ge 2$ revisions; user completed revision $N-1$; user has not acknowledged revision $N$.
- **Inputs:** Query filter `ComplianceGap = TrainedOnSupersededOnly`.
- **Processing:** Identify users where $\exists \text{ completed assignment for } \text{Rev}_{N-1}$ and $\nexists \text{ completed assignment for } \text{Rev}_N$.
- **Outputs:** Filtered list of at-risk personnel with gap alert badge in Training Matrix.
- **Workflow Status:** Calculated compliance indicator.
- **Authorization:** Requires compliance viewing permissions.
- **Audit Trail:** Query execution logged.
- **Segregation of Duties:** N/A.
- **Data Integrity:** Ensures compliance auditors can instantly detect training lag during revision transitions.
- **Acceptance Criteria:** 100% of personnel with superseded-only qualification are identified; zero false positives.
- **Verification Method:** Integration Test & OQ-1C-09.

---

#### FS-1c-011: Document Training Compliance KPI Reports & Export
- **Covered URS Requirements:** `DC-URS-091`, `DC-URS-118`, `DC-URS-119`, `DC-URS-120`, `DC-URS-121`, `DC-URS-122`
- **Functional Behavior:** Generates operational and executive compliance metrics:
  - Overall Organizational Document Compliance Percentage ($C = \frac{\text{Completed}}{\text{Required}} \times 100\%$).
  - Departmental Compliance Ranking.
  - Overdue Assignments by Department & Document Type.
  - Top 10 Overdue Documents.
  - Self-auditing CSV / PDF compliance export.
- **Actor:** Executive Management, QA Director, Document Controller.
- **Preconditions:** Reporting permissions active.
- **Inputs:** Date range, department filter, export format.
- **Processing:** Compute aggregate statistics across `ReadingAssignments`. Generate chart visual widgets and tabular summaries.
- **Outputs:** Executive KPI cards, trend charts, downloadable CSV/PDF report with cryptographic hash.
- **Workflow Status:** Reporting engine.
- **Authorization:** `doccontrol.reports.view`.
- **Audit Trail:** Report generation and file export logged with filter parameters and recipient user ID.
- **Segregation of Duties:** N/A.
- **Data Integrity:** Mathematical formulas validated; export file hashes recorded in audit trail.
- **Acceptance Criteria:** Compliance percentage calculation matches manual ledger calculations to 2 decimal places.
- **Verification Method:** Calculation Verification & UAT-1C-03.

---

### 3. FRS Traceability Summary

All thirty-five (35) Release 1c requirements are covered across the detailed specifications:

| FRS ID | Functional Domain | Mapped URS Requirements | Count |
|:---|:---|:---|:---:|
| `FS-1c-001` | Automatic Assignment Cascade | `DC-URS-080`, `DC-URS-170`, `DC-URS-171` | 3 |
| `FS-1c-002` | Manual & Curricula Assignments | `DC-URS-081`, `DC-URS-082`, `DC-URS-115`, `DC-URS-117` | 4 |
| `FS-1c-003` | Due Dates & Overdue Lifecycles | `DC-URS-083`, `DC-URS-084`, `DC-URS-116` | 3 |
| `FS-1c-004` | "My Reading List" Portal | `DC-URS-085`, `DC-URS-087`, `DC-URS-088` | 3 |
| `FS-1c-005` | Controlled Acknowledgement | `DC-URS-086`, `DC-URS-189`, `DC-URS-191`, `DC-URS-192` | 4 |
| `FS-1c-006` | Informational Progress Display | `DC-URS-190` | 1 |
| `FS-1c-007` | Historical Evidence Retention | `DC-URS-090`, `DC-URS-173` | 2 |
| `FS-1c-008` | Superseded Incomplete Closure | `DC-URS-172`, `DC-URS-175`, `DC-URS-184` | 3 |
| `FS-1c-009` | Training Matrix Grid | `DC-URS-111`, `DC-URS-112`, `DC-URS-113` | 3 |
| `FS-1c-010` | Superseded-Only Gap Tracking | `DC-URS-114`, `DC-URS-174` | 2 |
| `FS-1c-011` | Compliance Reports & Export | `DC-URS-091`, `DC-URS-118`, `DC-URS-119`, `DC-URS-120`, `DC-URS-121`, `DC-URS-122` | 6 |
| `FS-1c-012` | Retraining Re-Issuance Rules | `DC-URS-176` | 1 |
| **Total** | — | **35 Requirements Fully Specified** | **35** |
