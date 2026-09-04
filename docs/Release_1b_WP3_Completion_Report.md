# MicroLIMS Document Control — Release 1b
## Work Package 3 (WP3) Completion Report: Approval Workflow

---

### Executive Summary

| Project | MicroLIMS LIMS Enterprise System |
|---|---|
| **Module** | Document Control (Release 1b) |
| **Work Package** | **WP3: Approval Workflow** |
| **Document ID** | `ML-DC-R1B-WP3-REP-001` |
| **Baseline Standards** | 21 CFR Part 11, EU Annex 11, GAMP 5, ISO 17025:2017 §8.3 |
| **Change Control** | `CC-DC-R1B-003` (Approved) |
| **Status** | **COMPLETE & VERIFIED (IMPLEMENTED)** |
| **Validation Note** | Requirements marked **IMPLEMENTED**. Qualification remains reserved for formal Release 1b OQ/UAT execution. |

Work Package 3 (WP3) delivers the formal Document Approval Workflow for the MicroLIMS Document Control module. WP3 establishes the approver inspection dossier, readiness validation checks, three-way formal decision processing (`Approve`, `ReturnForCorrection`, `Decline`), transactional status transitions, and strict Segregation of Duties (SoD) enforcing that an individual cannot approve a document they authored or technically reviewed.

All automated backend regression tests (659/659 passing), 17 dedicated WP3 unit tests, 3 live PostgreSQL integration tests, and the frontend production build (`tsc -b && vite build`) passed with zero errors and zero warnings.

---

### 1. Requirements Implementation Scope

| URS ID | FRS ID | Requirement Description | Verification Evidence | Status |
|---|---|---|---|:---:|
| **DC-URS-074** | `FS-1b-074` | Approver inspection dossier displaying controlled PDF, metadata, change items, impact assessments, technical review log, and readiness checklist. | `DocumentApprovalService.GetApprovalDossierAsync`<br>`GET /api/document-control/approvals/{id}/dossier`<br>`ApprovalDossier_CompilesFullEvidence` unit test | **IMPLEMENTED** |
| **DC-URS-075** | `FS-1b-075` | Approver decision options (`Approve`, `ReturnForCorrection`, `Decline`) with mandatory notes ($\ge 10$ characters) for non-approval outcomes and automatic draft reversion or cancellation. | `DocumentApprovalService.ExecuteApprovalDecisionAsync`<br>`POST /api/document-control/approvals/{id}/decide`<br>`ExecuteDecision_Approve_ReturnsEffectiveOrFutureEffective`<br>`ExecuteDecision_ReturnForCorrection_RevertsToDraft` | **IMPLEMENTED** |
| **DC-URS-078** (Part) | `FS-1b-078` | Segregation of Duties: Author $\neq$ Approver, Reviewer $\neq$ Approver. System Administrators are non-exempt. Enforced at both assignment and decision execution. | `ValidateDecisionSegregationOfDuties`<br>`ValidateAssignmentSegregationOfDuties`<br>`NegativeTests_SegregationOfDuties_Enforced` | **IMPLEMENTED** |
| **DC-URS-079** (Part) | `FS-1b-079` | Complete semantic audit trail for dossier inspection, approver assignment, and approval decisions. | `ApprovalDossierViewed`, `DocumentApprovalTaskCreated`, `DocumentRevisionApproved`, `RevisionReturnedByApprover`, `RevisionDeclinedByApprover` | **IMPLEMENTED** |

*Scope Boundary Note:*  
- `DC-URS-076` & `DC-URS-077` (Part 11 password re-authentication and electronic signature ceremony) belong to **WP4** and were stubbed with clean architectural boundaries.
- `DC-URS-177` & `DC-URS-178` (Background worker auto-activation of future effective dates) belong to **WP5**; WP3 tags future effective dates as `DocumentRevisionStatus.FutureEffective`.

---

### 2. Architectural & Database Modifications

#### 2.1 Domain Enums & Entities
1. **`DocumentApprovalDecision.cs`**:
   - `Approve = 1`
   - `ReturnForCorrection = 2`
   - `Decline = 3`
2. **`DocumentApprovalTaskStatus.cs`**:
   - `Pending = 1`
   - `Approved = 2`
   - `ReturnedForCorrection = 3`
   - `Declined = 4`
   - `Cancelled = 5`
3. **`DocumentApprovalTask.cs`**:
   - Primary Key: `Id` (`int`, Identity)
   - Foreign Keys:
     - `DocumentRevisionId` $\rightarrow$ `DocumentRevisions.Id` (FK Restrict)
     - `AssignedApproverUserId` $\rightarrow$ `Users.Id` (FK Restrict)
     - `AssignedByUserId` $\rightarrow$ `Users.Id` (FK Restrict)
     - `DecisionByUserId` $\rightarrow$ `Users.Id` (FK Restrict, Nullable)
   - State Tracking: `Status`, `Decision`, `TargetEffectiveDate`, `AssignedAt`, `DecisionAt`, `SubmissionNotes`, `DecisionNotes`
4. **`DocumentRevision.cs`**:
   - Added additive navigation collection: `ApprovalTasks` (`ICollection<DocumentApprovalTask>`).
5. **`UserReferenceRegistry.cs`**:
   - Registered `DocumentApprovalTask.AssignedApproverUserId`, `AssignedByUserId`, and `DecisionByUserId` under `UserReferenceDisposition.Blocks`.

#### 2.2 EF Core Configuration & Migration
- **Configuration:** `DocumentApprovalTaskConfiguration.cs` with query filters (`IsDeleted == false`), indexes on `(DocumentRevisionId, Status)` and `(AssignedApproverUserId, Status)`.
- **Migration:** `20260904072635_AddDocumentApprovalTaskEntity.cs` applied to PostgreSQL database `LIMSV2`.

---

### 3. Application & API Layer

#### 3.1 Service Implementation (`DocumentApprovalService.cs`)
- **`CreateApprovalTaskAsync`**:
  - Guards revision state: must be `AwaitingApproval`.
  - Verifies technical review completion.
  - Enforces SoD: Assigned approver cannot be the document author or any technical reviewer. System Administrator is non-exempt.
  - Enforces approver role qualification: user must possess `Reviewer`, `SectionHead`, or `SystemAdministrator`.
- **`GetApprovalDossierAsync`**:
  - Compiles Master details, Revision details, Controlled PDF metadata (with SHA-256 integrity check), Change Items, Impact Assessment, Technical Review findings/comments, and Readiness Checklist.
  - Emits semantic audit event `ApprovalDossierViewed`.
- **`ExecuteApprovalDecisionAsync`**:
  - Strictly validates SoD *prior* to task matching, ensuring any author/reviewer attempting to execute an approval receives `Segregation of Duties Violation`.
  - Enforces note length ($\ge 10$ chars) on `ReturnForCorrection` and `Decline`.
  - On `ReturnForCorrection`: Sets revision to `Draft`, cancels task as `ReturnedForCorrection`, emits `RevisionReturnedByApprover`.
  - On `Decline`: Cancels revision (`Cancelled`), cancels task as `Declined`, emits `RevisionDeclinedByApprover`.
  - On `Approve`: Verifies readiness checklist. If `TargetEffectiveDate` is future-dated, sets revision to `FutureEffective`. If immediate/null, sets to `Effective` and supersedes previous effective revision in the same atomic transaction. Emits `DocumentRevisionApproved`.

#### 3.2 REST API Controller (`DocumentApprovalController.cs`)
- `POST /api/document-control/revisions/{id}/approvals`: Assign approver and initialize task.
- `GET /api/document-control/revisions/{id}/active-approval`: Retrieve currently pending approval task.
- `GET /api/document-control/approvals/{id}`: Retrieve approval task by ID.
- `GET /api/document-control/approvals`: List approval tasks with status filters.
- `GET /api/document-control/approvals/{id}/dossier`: Fetch compiled inspection dossier.
- `GET /api/document-control/approvals/{id}/readiness`: Evaluate pre-decision readiness checklist.
- `POST /api/document-control/approvals/{id}/decide`: Submit formal decision (`Approve`, `ReturnForCorrection`, `Decline`).

---

### 4. Frontend Workspace Implementation

1. **`documentControlTypes.ts`**:
   - Added `AwaitingApproval` and `FutureEffective` to `DocumentRevisionStatus`.
   - Added TypeScript interfaces: `DocumentApprovalTaskDto`, `ApprovalDossierDto`, `ApprovalReadinessDto`, `CreateApprovalTaskRequest`, `ExecuteApprovalDecisionRequest`.
2. **`documentApprovalService.ts`**:
   - Axios client integration for all 7 approval endpoints.
3. **`AssignApproverDialog.tsx`**:
   - Modal to select eligible approvers, automatically filtering out the author and reviewers.
   - Allows specification of an optional `TargetEffectiveDate` with validation.
4. **`ApprovalWorkspaceDialog.tsx`**:
   - 3-Tab Comprehensive Workspace:
     - **Tab 1: Inspection Dossier** (Master details, Revision metadata, Controlled PDF SHA-256 card, Affected Change Items, Multi-category Impact Assessment, Technical Review Log).
     - **Tab 2: Readiness Checklist** (Real-time indicators for PDF presence, Impact Assessment completion, Technical Review closure, Open Comment resolution, SoD validation).
     - **Tab 3: Formal Decision Controls** (Decision selector, target date override, mandatory justification input with character counter, 21 CFR Part 11 Electronic Signature ceremony notification placeholder).
5. **`DocumentDetailPage.tsx`**:
   - Connected "Assign Approver" button when revision is `AwaitingApproval` and no pending task exists.
   - Connected "Approval Workspace" button when revision is `AwaitingApproval` and task is pending.

---

### 5. Verification Evidence & Test Summary

#### 5.1 Automated Backend Testing (`dotnet test`)
- **Dedicated WP3 Unit Tests** (`DocumentControlApprovalUnitTests.cs`):
  - `CreateApprovalTask_AwaitingApproval_AssignsTaskSuccessfully`: **PASS**
  - `CreateApprovalTask_DuplicatePending_ThrowsInvalidOperation`: **PASS**
  - `CreateApprovalTask_NotAwaitingApproval_ThrowsInvalidOperation`: **PASS**
  - `CreateApprovalTask_AuthorAsApprover_ThrowsSoDViolation`: **PASS**
  - `CreateApprovalTask_ReviewerAsApprover_ThrowsSoDViolation`: **PASS**
  - `CreateApprovalTask_AdminAsAuthor_CannotApprove`: **PASS**
  - `CreateApprovalTask_IneligibleRole_ThrowsValidation`: **PASS**
  - `GetApprovalDossier_EmitsAuditAndCompilesDetails`: **PASS**
  - `EvaluateReadiness_MissingPdf_ReportsNotReady`: **PASS**
  - `ExecuteDecision_Approve_ImmediateEffective_SetsEffectiveAndSupersedes`: **PASS**
  - `ExecuteDecision_Approve_FutureEffective_SetsFutureEffective`: **PASS**
  - `ExecuteDecision_ReturnForCorrection_RevertsToDraft`: **PASS**
  - `ExecuteDecision_Decline_CancelsRevision`: **PASS**
  - `ExecuteDecision_ReturnOrDecline_RequiresAtLeast10CharNotes`: **PASS**
  - `ExecuteDecision_AuthorOrReviewer_ThrowsSoDViolation`: **PASS**
  - `ExecuteDecision_NonAssignedUser_ThrowsUnauthorized`: **PASS**
  - `ExecuteDecision_AlreadyDecided_ThrowsInvalidOperation`: **PASS**
  - **Result: 17 / 17 passed.**

- **Live PostgreSQL Integration Tests** (`DocumentControlApprovalPostgresIntegrationTests.cs`):
  - `Postgres_CreateApprovalTask_PersistsWithDatabaseConstraints`: **PASS**
  - `Postgres_ExecuteApprovalDecision_Approve_SupersedesPriorEffectiveInSameTx`: **PASS**
  - `Postgres_ExecuteApprovalDecision_ApproveFutureDate_SetsFutureEffective`: **PASS**
  - **Result: 3 / 3 passed.**

- **Full Solution Regression Test Suite**:
  - **Total Tests Passed:** **659 / 659**
  - **Failures:** **0**
  - **Skipped:** **0**
  - **Duration:** 38.2 seconds

#### 5.2 Frontend Production Build (`npm run build`)
- Command: `tsc -b && vite build`
- Modules transformed: 2,383
- Exit code: `0` (Success)
- Errors: `0`

---

### 6. Strict Scope Boundary Confirmation

| Boundary Rule | Confirmation |
|---|---|
| Electronic Signatures (Part 11 re-authentication ceremony) | **NOT IMPLEMENTED.** WP3 renders the regulatory notice and clean architectural hook for WP4. |
| Automatic Effective Date Background Activation | **NOT IMPLEMENTED.** Future effective documents remain in `FutureEffective` state pending WP5. |
| Periodic Review Scheduler | **NOT IMPLEMENTED.** Excluded from WP3. |
| Training / Reading Lists / KAF Integration | **NOT IMPLEMENTED.** Excluded from WP3. |
| Boundary Enforcement | **IMPLEMENTATION STOPPED AT WP3 BOUNDARY.** |
