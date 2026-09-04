# MicroLIMS Document Control — Release 1c
## Work Package 9: Operational Qualification (OQ) Protocol

- **Document Identifier:** `ML-DC-R1C-OQP-001`
- **Version:** 2.0 (Approved for WP9 Execution)
- **Module:** Document Control Module — Subsystem: Training & Reading Lists
- **Release:** MicroLIMS Release 1c
- **Controlled Change Reference:** `CC-DC-R1C-001`
- **Date:** September 4, 2026
- **CSV / GxP Authority:** GAMP 5 (Category 5), 21 CFR Part 11, EU GMP Annex 11, ALCOA+
- **Pre-Execution Baseline:** Implemented & Integration-Verified Baseline (WP1–WP8 Complete; 30 Implemented Requirements, 5 Planned Requirements Excluded)

---

### 1. Protocol Objective, Scope & Execution Rules

#### 1.1 Objective
The purpose of this Operational Qualification (OQ) protocol is to provide documented, objective evidence that the software functions of **MicroLIMS Document Control Release 1c** operate in strict accordance with approved specifications (`ML-DC-FRS-1C-001`) and User Requirements (`MicroLIMS Document Control URS v1.1`).

#### 1.2 Boundary & Non-Functional Development Rule
1. **Strict Implemented Scope:** Only the **30 implemented and integration-verified requirements** (`DC-URS-080`..`090`, `DC-URS-111`..`116`, `DC-URS-118`..`120`, `DC-URS-170`..`175`, `DC-URS-189`..`192`) are subject to formal qualification.
2. **Exclusion of Planned Requirements:** The 5 planned requirements (`DC-URS-091`, `DC-URS-117`, `DC-URS-121`, `DC-URS-122`, `DC-URS-176`) are explicitly **excluded** from qualification and shall not be executed.
3. **No New Development:** WP9 is strictly a qualification work package. No new functional features shall be developed.
4. **Frozen Production Protection:** The Release 1b production baseline (frozen at commit `6fe5a61`) must remain unmodified, active, and fully protected.

---

### 2. Operational Qualification Test Domains (Domains A through J)

| Domain | Functional Scope | Governed Requirements |
|:---|:---|:---:|
| **Domain A** | **Document Revision & Effective Date Automation** | `DC-URS-080`, `DC-URS-170`, `DC-URS-171` |
| **Domain B** | **Training Assignment Distribution & Grace Hierarchy** | `DC-URS-081`, `DC-URS-082`, `DC-URS-083`, `DC-URS-115` |
| **Domain C** | **Retraining Cascade, Supersedence & Historical Retention** | `DC-URS-090`, `DC-URS-114`, `DC-URS-172`, `DC-URS-173`, `DC-URS-174`, `DC-URS-175` |
| **Domain D** | **Personal Reading List & Controlled PDF Viewer** | `DC-URS-085`, `DC-URS-087`, `DC-URS-088`, `DC-URS-089` |
| **Domain E** | **Electronic Acknowledgement & Primary Evidence Recording** | `DC-URS-086`, `DC-URS-189`, `DC-URS-190`, `DC-URS-191`, `DC-URS-192` |
| **Domain F** | **Escalations & Overdue Training Management** | `DC-URS-084`, `DC-URS-116` |
| **Domain G** | **Multi-Axis Training Matrix & Drilldowns** | `DC-URS-111`, `DC-URS-112`, `DC-URS-113` |
| **Domain H** | **Executive Compliance KPIs & Departmental Analytics** | `DC-URS-118`, `DC-URS-119`, `DC-URS-120` |
| **Domain I** | **Security, Authorization & Segregation of Duties (SoD)** | `DC-URS-085`, `DC-URS-086`, `DC-URS-111`, `DC-URS-113`, `DC-URS-192` |
| **Domain J** | **Data Integrity, ALCOA+ & Immutability Defense** | `DC-URS-090`, `DC-URS-173`, `DC-URS-189` |

---

### 3. Detailed OQ Test Protocol Specifications (OQ-1C-01 through OQ-1C-18)

#### OQ-1C-01: Automatic Reading Assignment Generation upon Revision Effective Promotion
- **Target Requirements:** `DC-URS-080`, `DC-URS-170`, `DC-URS-171` (Domain A, C)
- **Preconditions:** Document Master `DOC-2026-0001` exists with Rev 01 `Effective`. User Role Curriculum maps Analysts to this SOP. Document training config requires retraining on revision.
- **Test Steps:**
  1. Create Revision 02 in `Draft`, approve through workflow to `FutureEffective`.
  2. Invoke `DocumentEffectiveDateWorker` or mature effective date past `UtcNow`.
  3. Query database table `DocumentTrainingAssignments`.
- **Expected Acceptance Criteria:**
  - Revision 02 status promotes to `Effective`.
  - Reading assignments automatically inserted for all active users assigned via role curriculum or prior completers.
  - Audit event `TrainingCascadeAssignmentsCreated` logged with `ActorType.System` and `ActionCategory = Document`.

#### OQ-1C-02: Manual Assignment to Individual Users & Groups
- **Target Requirements:** `DC-URS-081`, `DC-URS-082` (Domain B)
- **Preconditions:** Active effective SOP exists. Document Controller authenticated.
- **Test Steps:**
  1. POST `/api/document-control/training-assignments/manual-assign` with target user IDs.
  2. POST `/api/document-control/training-assignments/bulk-group-assign` with target role ID.
- **Expected Acceptance Criteria:**
  - Individual assignments created with `AssignmentReason` populated.
  - Group assignments created for all active users holding specified role.
  - Duplicate assignments skipped idempotently.

#### OQ-1C-03: Grace Period Hierarchy & Due Date Calculation
- **Target Requirements:** `DC-URS-083`, `DC-URS-115` (Domain B)
- **Preconditions:** Role curriculum item configures 14 days; document master configures 21 days; system default is 14 days.
- **Test Steps:**
  1. Trigger assignment calculation for curriculum item with custom grace period (e.g. 14 days).
  2. Trigger assignment calculation where no curriculum grace exists but master config exists.
  3. Verify resulting `DueDateUtc`.
- **Expected Acceptance Criteria:**
  - Hierarchy strictly observed: Role Curriculum Item > Master Configuration > Document Type Configuration > Default 14 Days.
  - Due date computed precisely as `EffectiveDate.AddDays(GracePeriod)`.

#### OQ-1C-04: Automatic Overdue Transition & Multi-Level Escalations
- **Target Requirements:** `DC-URS-084`, `DC-URS-116` (Domain F)
- **Preconditions:** Reading assignment exists with `DueDateUtc` in the past ($T - 2\text{ hours}$).
- **Test Steps:**
  1. Execute escalation engine cycle via `DocumentEffectiveDateWorker` or `/api/document-control/escalations/process-due`.
  2. Inspect assignment status and `DocumentEscalationRecords` table.
- **Expected Acceptance Criteria:**
  - Assignment status transitions from `Assigned` to `Overdue`.
  - `DocumentEscalationRecord` inserted with `EscalationLevel = Level1_Supervisor` and notification flags set.
  - Re-executing escalation cycle within suppression window does not insert duplicate escalations (idempotent).

#### OQ-1C-05: Authenticated User Reading List Personal Scoping
- **Target Requirements:** `DC-URS-085`, `DC-URS-088` (Domain D, I)
- **Preconditions:** User A (Analyst) and User B (Analyst) both have open reading assignments.
- **Test Steps:**
  1. Authenticate as User A and GET `/api/document-control/training-assignments/my-assignments`.
  2. Attempt to query User B's assignments directly.
- **Expected Acceptance Criteria:**
  - Returned dataset strictly restricted to assignments where `AssignedUserId == UserA.Id`.
  - Cross-user assignment queries denied (`403 Forbidden` or automatically scoped).

#### OQ-1C-06: Controlled PDF Viewer Launch & Informational Reading Progress
- **Target Requirements:** `DC-URS-087`, `DC-URS-190` (Domain D, E)
- **Preconditions:** User has open reading assignment on Revision 01 with controlled PDF blob stored.
- **Test Steps:**
  1. Request controlled file blob via viewer endpoint. Verify SHA-256 header.
  2. Update reading progress to 100% via POST `/api/document-control/acknowledgements/reading-progress`.
  3. Inspect assignment status in database.
- **Expected Acceptance Criteria:**
  - Viewer receives authentic PDF with SHA-256 integrity match.
  - Assignment status transitions from `Assigned` to `Reading` (informational progress).
  - Assignment is NOT marked `Acknowledged` or `Completed`; scrolling alone does not constitute legal completion.

#### OQ-1C-07: Conscious Read-and-Understand Electronic Acknowledgement
- **Target Requirements:** `DC-URS-086`, `DC-URS-189`, `DC-URS-191`, `DC-URS-192` (Domain E, I)
- **Preconditions:** Assignment in `Reading` status. Configured statement text active.
- **Test Steps:**
  1. Submit acknowledgement without confirming statement checkbox.
  2. Submit acknowledgement as User B for User A's assignment.
  3. Submit acknowledgement for mismatched revision ID.
  4. Submit valid conscious acknowledgement as User A with confirmed legal statement.
- **Expected Acceptance Criteria:**
  - Unchecked submission rejected with HTTP 400.
  - Proxy/cross-user submission rejected with HTTP 403.
  - Revision mismatch rejected with HTTP 400.
  - Valid submission inserts immutable `DocumentAcknowledgementRecord` capturing UTC timestamp, user ID, exact frozen statement text, and PDF SHA-256 hash; transitions assignment status to `Acknowledged`.

#### OQ-1C-08: Due-Date Countdown & Status Highlighting
- **Target Requirements:** `DC-URS-089` (Domain D)
- **Preconditions:** Assignments exist with due dates in future (10 days) and in past (-2 days).
- **Test Steps:**
  1. GET `/api/document-control/training-assignments/my-assignments`.
  2. Inspect `DaysRemainingOrOverdue` and `IsOverdue` attributes.
- **Expected Acceptance Criteria:**
  - Future assignment returns positive delta ($+10.0$) and `IsOverdue = false`.
  - Overdue assignment returns negative delta ($-2.0$) and `IsOverdue = true`.
  - Frontend badges display distinct countdown and alert styling.

#### OQ-1C-09: Permanent Historical Retention Across Retraining Cycles
- **Target Requirements:** `DC-URS-090`, `DC-URS-173` (Domain C, J)
- **Preconditions:** User completed Rev 01 acknowledgement. Rev 02 became effective and generated retraining assignment.
- **Test Steps:**
  1. Query database for User's assignments on Document Master.
  2. Inspect historical Rev 01 assignment and acknowledgement record.
- **Expected Acceptance Criteria:**
  - Rev 01 assignment and `DocumentAcknowledgementRecord` remain 100% intact, immutable, and queryable.
  - Zero rows deleted or overwritten during retraining generation.

#### OQ-1C-10: Terminal Closure of Incomplete Superseded Assignments
- **Target Requirements:** `DC-URS-172` (Domain C)
- **Preconditions:** User has open (uncompleted) assignment on Rev 01 in `Assigned` status. Rev 02 becomes `Effective`.
- **Test Steps:**
  1. Trigger effective revision cascade for Rev 02.
  2. Query database for status of Rev 01 assignment.
- **Expected Acceptance Criteria:**
  - Rev 01 assignment status transitions to `SupersededIncomplete`.
  - `ClosedReason` records `"Superseded by Revision 02"`.
  - `SupersededAtUtc` records timestamp; record is preserved, not deleted.

#### OQ-1C-11: Retraining Ancestry & Parentage Tracking (`SourceAssignmentId`)
- **Target Requirements:** `DC-URS-170` (Domain C)
- **Preconditions:** User qualified on Rev 01 (`AssignmentId = 101`). Rev 02 activates retraining.
- **Test Steps:**
  1. Inspect newly generated Rev 02 assignment record.
- **Expected Acceptance Criteria:**
  - Rev 02 assignment has `SourceAssignmentId = 101`.
  - Lineage provides complete retraining traceability from Rev 01 to Rev 02.

#### OQ-1C-12: Identification of Superseded-Only Qualified Personnel
- **Target Requirements:** `DC-URS-114`, `DC-URS-174` (Domain C, G)
- **Preconditions:** User qualified on Rev 01, but has open retraining assignment on newly effective Rev 02.
- **Test Steps:**
  1. Execute `IsTrainedOnSupersededOnlyAsync(UserId, MasterId)`.
  2. Query Training Matrix grid for this cell.
- **Expected Acceptance Criteria:**
  - Service returns `true`.
  - Training Matrix cell status evaluates to `TrainedOnSupersededOnly` and displays high-visibility warning badge.
  - Cell displays prior revision qualification details and retraining gap notice.

#### OQ-1C-13: Document Obsolescence Training Cancellation
- **Target Requirements:** `DC-URS-175` (Domain C)
- **Preconditions:** Document Master has open assignments on Rev 01 and completed historical assignments on Rev 01.
- **Test Steps:**
  1. Execute `HandleDocumentObsolescenceAsync(MasterId)`.
  2. Query database for assignment statuses.
- **Expected Acceptance Criteria:**
  - Open assignments transition to `Cancelled` with `ClosedReason = "Document Obsoleted / Cancelled"`.
  - Historical completed assignments and acknowledgement records remain untouched.

#### OQ-1C-14: Multi-Axis Training Matrix Generation & Status Classification
- **Target Requirements:** `DC-URS-111`, `DC-URS-112`, `DC-URS-113` (Domain G)
- **Preconditions:** 20+ users across multiple departments; 10+ documents with active effective revisions.
- **Test Steps:**
  1. GET `/api/document-control/training-matrix/grid`.
  2. Filter by `departmentId` and `roleId`.
- **Expected Acceptance Criteria:**
  - Grid accurately compiles Users × Documents matrix.
  - Cells correctly classified as `Qualified`, `Pending`, `Overdue`, `TrainedOnSupersededOnly`, or `NotAssigned`.
  - Filters return exact matching subsets.

#### OQ-1C-15: Organizational Compliance KPIs & Departmental Rankings
- **Target Requirements:** `DC-URS-118`, `DC-URS-119`, `DC-URS-120` (Domain H)
- **Preconditions:** Assignments distributed across departments with varying completion states.
- **Test Steps:**
  1. GET `/api/document-control/training-matrix/kpis`.
- **Expected Acceptance Criteria:**
  - `OverallComplianceRatePercentage` accurately equals $\frac{\text{Completed}}{\text{Total Required}} \times 100$.
  - Department breakdown ranks departments by compliance percentage.
  - Top overdue documents list identifies masters with highest overdue counts.

#### OQ-1C-16: Segregation of Duties & Authorization Barriers
- **Target Requirements:** `DC-URS-085`, `DC-URS-111`, `DC-URS-113` (Domain I)
- **Preconditions:** User A (Analyst) and User B (Supervisor/Admin).
- **Test Steps:**
  1. User A attempts to request User B's compliance detail (`GET /api/document-control/training-matrix/users/{UserB.Id}`).
  2. User A attempts to manually assign reading to other users.
- **Expected Acceptance Criteria:**
  - Unauthorized requests rejected with HTTP 403 Forbidden.
  - Non-privileged users restricted to own reading list and personal training records.

#### OQ-1C-17: Database Trigger Protection of Acknowledgement Immutability
- **Target Requirements:** `DC-URS-189`, `DC-URS-173` (Domain J)
- **Preconditions:** Live PostgreSQL database with table `DocumentAcknowledgementRecords` and trigger `trg_doc_ack_immutability`.
- **Test Steps:**
  1. Execute direct raw SQL `UPDATE` against existing acknowledgement record.
  2. Execute direct raw SQL `DELETE` against existing acknowledgement record.
- **Expected Acceptance Criteria:**
  - Both statements fail with PostgreSQL trigger exception: `"Updates to DocumentAcknowledgementRecords are strictly prohibited."` and `"Deletions from DocumentAcknowledgementRecords are strictly prohibited."`
  - Zero rows modified or deleted.

#### OQ-1C-18: Background Worker Automation & Recovery Idempotency
- **Target Requirements:** `DC-URS-080`, `DC-URS-084`, `DC-URS-170` (Domain A, F)
- **Preconditions:** Multiple matured revisions and overdue assignments present in PostgreSQL database.
- **Test Steps:**
  1. Execute `DocumentEffectiveDateWorker.RunCycleAsync`.
  2. Immediately execute a second worker cycle.
- **Expected Acceptance Criteria:**
  - Cycle 1 promotes matured revisions, cascades retraining, and transitions overdue assignments.
  - Cycle 2 executes cleanly with 0 duplicate assignments, 0 duplicate escalations, and 0 duplicate status changes.
