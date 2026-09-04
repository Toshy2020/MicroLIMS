# MicroLIMS Document Control — Release 1b
## Work Package 6 (WP6) Completion Report: Periodic Review Engine & Workflow

---

### Executive Summary

| Field | Value |
|---|---|
| **Project** | MicroLIMS Enterprise Laboratory Information Management System |
| **Module** | Document Control (Release 1b) |
| **Work Package** | **WP6: Periodic Review Engine & Workflow** |
| **Document ID** | `ML-DC-R1B-WP6-REP-001` |
| **Regulatory Baseline** | FDA 21 CFR Part 11, EU GMP Annex 11, GAMP 5 (Category 4 / Configured), ISO 17025:2017 §8.3 |
| **Change Control Reference** | `CC-DC-R1B-006` (Approved & Baselined) |
| **Lifecycle Status** | **COMPLETE & VERIFIED (IMPLEMENTED / TESTED / REGRESSION GREEN)** |
| **Validation Governance Notice** | In strict accordance with GAMP 5 lifecycle governance, requirements are marked **IMPLEMENTED**. Final qualification remains strictly reserved for formal Release 1b OQ/UAT execution. |

Work Package 6 (WP6) delivers the complete automated Periodic Review engine, relational domain model, dedicated review workspace, page/section finding management, review outcome lifecycle, and library/dashboard overdue tracking. The implementation strictly enforces regulatory compliance including:
1. Automated periodic review task generation based on document review schedules (`NextReviewDate <= UtcNow`).
2. Dedicated review workspace displaying verified controlled PDFs with cryptographic SHA-256 integrity checks.
3. Page and section-specific review notes/findings with status progression (`Open` -> `Resolved`).
4. Strict enforcement of review outcomes (`RemainsValid`, `RevisionRequired`, `ObsolescenceRecommended`).
5. Invariant enforcement for `RemainsValid`: preserves effective revision status, creates NO new revision, advances `NextReviewDate` by approved review cycle (`AddMonths(ReviewCycleMonths)`), and retains review evidence.
6. Independent relational storage maintaining perpetual review history across document revisions.
7. Overdue periodic review identification with UTC filtering and high-visibility UI badges.
8. Background worker integration leveraging the existing unified `DocumentEffectiveDateWorker` scheduled foundation.
9. Strict Segregation of Duties: Author of the revision under review cannot be assigned as reviewer, submit review notes, or execute review completion (System Administrator is non-exempt).

All automated verification standards are satisfied:
- **Backend Full Regression Suite:** 707 / 707 passing (100% green; 0 failed, 0 skipped, 41s duration; 690 baseline + 12 WP6 unit tests + 5 WP6 postgres integration tests).
- **Dedicated WP6 Unit Tests:** 12 / 12 passing (100% green; covering task generation, idempotency, PDF verification, findings persistence, outcome decisions, RemainsValid date advance without revision creation, Obsolescence routing to approval, downtime recovery, immutability of completed reviews, and multi-document failure isolation).
- **Live PostgreSQL Integration Tests:** 5 / 5 passing (100% green; verifying live database persistence, unique active task constraint, system-attributed audit logs, date advance in database, obsolescence approval task creation, and segregation of duties enforcement).
- **Frontend Production Build:** `tsc -b && vite build` passed cleanly with 0 errors.

---

### 1. Requirements Implementation Scope

| URS ID | FRS ID | Risk ID | Requirement Description | Verification Evidence | Status |
|---|---|---|---|---|:---:|
| **DC-URS-044** | `FS-1b-044` | RA-044 | Automatic Periodic Review task creation: Identifies effective documents where `NextReviewDate <= UtcNow` and creates `PeriodicReviewTask` in `Pending` status. | `PeriodicReviewService.GenerateDueReviewTasksAsync`<br>`UnitTests: GenerateDueReviewTasks_WhenDue_CreatesPeriodicReviewTask`<br>`Postgres: Postgres_GenerateDueReviewTasks_PersistsTaskAndLogsSystemAudit` | **IMPLEMENTED** |
| **DC-URS-045** | `FS-1b-045` | RA-045 | Review workspace displaying controlled PDF: Bundles master summary, revision metadata, controlled PDF retrieval with SHA-256 verification, and active findings. | `PeriodicReviewService.GetReviewWorkspaceAsync`<br>`PeriodicReviewWorkspaceDialog.tsx`<br>`UnitTests: GetReviewWorkspace_LoadsTaskAndControlledPdfMetadata` | **IMPLEMENTED** |
| **DC-URS-046** | `FS-1b-046` | RA-046 | Page/section review notes: Reviewers can log structured findings referencing specific page and section numbers with resolution tracking. | `PeriodicReviewService.AddFindingAsync`<br>`PeriodicReviewFinding.cs`<br>`UnitTests: AddFinding_PersistsPageAndSectionNote_TransitionsStatusToInProgress`<br>`Postgres: Postgres_CompleteReview_RemainsValid_AdvancesNextReviewDateInPostgres` | **IMPLEMENTED** |
| **DC-URS-047** | `FS-1b-047` | RA-047 | Review outcomes: Enforces explicit review outcomes: `RemainsValid`, `RevisionRequired`, and `ObsolescenceRecommended`. | `PeriodicReviewService.CompleteReviewAsync`<br>`PeriodicReviewOutcome.cs`<br>`UnitTests: CompleteReview_RequiresValidSummaryAndEnforcesOutcome` | **IMPLEMENTED** |
| **DC-URS-048** | `FS-1b-048` | RA-048 | `RemainsValid` creates no new revision: Closes periodic review, preserves effective revision in place, and guarantees document revision count remains unchanged. | `PeriodicReviewService.CompleteReviewAsync`<br>`UnitTests: CompleteReview_RemainsValid_AdvancesNextReviewDate_WithoutCreatingNewRevision`<br>`Postgres: Postgres_CompleteReview_RemainsValid_AdvancesNextReviewDateInPostgres` | **IMPLEMENTED** |
| **DC-URS-049** | `FS-1b-049` | RA-049 | `RemainsValid` advances next review date: Automatically advances `NextReviewDate` by approved cycle (`effectiveRev.NextReviewDate = nowUtc.AddMonths(cycleMonths)`). | `PeriodicReviewService.CompleteReviewAsync`<br>`UnitTests: CompleteReview_RemainsValid_AdvancesNextReviewDate_WithoutCreatingNewRevision`<br>`Postgres: Postgres_CompleteReview_RemainsValid_AdvancesNextReviewDateInPostgres` | **IMPLEMENTED** |
| **DC-URS-053** | `FS-1b-053` | RA-053 | Independent periodic review history: Dedicated relational storage (`PeriodicReviewTasks` table) maintains perpetual review history across all past and current revisions. | `PeriodicReviewService.GetMasterReviewHistoryAsync`<br>`DocumentDetailPage.tsx (Tab 6)`<br>`UnitTests: GetReviewWorkspace_LoadsTaskAndControlledPdfMetadata` | **IMPLEMENTED** |
| **DC-URS-054** | `FS-1b-054` | RA-054 | Overdue periodic review identification: System identifies overdue documents (`NextReviewDate < UtcNow`) and provides library filtering and dashboard alerts. | `DocumentMasterService.GetLibraryAsync (isOverdue filter)`<br>`DocumentLibraryPage.tsx`<br>`DocumentControlDashboardPage.tsx`<br>`UnitTests: DowntimeCatchUp_OverdueRevisionsProcessedImmediately` | **IMPLEMENTED** |
| **DC-URS-179** | `FS-1b-179` | RA-179 | Automated task generation scheduled via background worker: Scheduled background worker execution scans due reviews and creates tasks with system audit logging. | `DocumentEffectiveDateWorker.RunCycleAsync`<br>`PeriodicReviewService.GenerateDueReviewTasksAsync`<br>`Postgres: Postgres_GenerateDueReviewTasks_PersistsTaskAndLogsSystemAudit` | **IMPLEMENTED** |

*Controlled Out-of-Scope Items (Preserved for Subsequent Work Packages):*
- `DC-URS-052` (Formal Quality Obsolescence Approval Ceremony) routes approval tasks into WP3/WP4 workflows; full obsolescence state machine completion is finalized during integration.
- `DC-URS-067` (Side-by-side Dual PDF inspection) and `DC-URS-078` (Segregation of Duties client-side visual masking) remain tracked for **WP7 (Frontend Integration)**.
- Formal qualification protocols and execution reports remain tracked for **WP8 (Integration Verification)** and **WP9 (Release 1b OQ/UAT)**.

---

### 2. Architecture & Design Alignment

#### 2.1 Unified Background Scheduler Foundation
In accordance with system design principles, duplicate background services were avoided:
- The existing native hosted service `DocumentEffectiveDateWorker` was extended to execute periodic review evaluation alongside effective date transitions in `RunCycleAsync`.
- The worker executes on a configurable non-drifting timer with `SemaphoreSlim(1, 1)` re-entrancy protection.
- Resolves scoped `IPeriodicReviewService` via `IServiceScopeFactory`, isolating domain execution per tick.

#### 2.2 Relational Domain Model & Schema Migration
Two dedicated entities were added to the persistence model:
1. **`PeriodicReviewTask` (`backend/MicroLIMS.Domain/Entities/PeriodicReviewTask.cs`):**
   - Foreign keys to `DocumentMaster` (Cascade) and `DocumentRevision` (Restrict).
   - Tracks `ReviewCycleMonths`, `ScheduledDueDate`, `AssignedReviewerUserId`, `Status` (`Pending`, `InProgress`, `Completed`, `Cancelled`), `Outcome` (`RemainsValid`, `RevisionRequired`, `ObsolescenceRecommended`), `CompletedAt`, `CompletedByUserId`, and `ReviewSummary`.
   - Dedicated EF Core migration `AddPeriodicReviewEntities` applied.
2. **`PeriodicReviewFinding` (`backend/MicroLIMS.Domain/Entities/PeriodicReviewFinding.cs`):**
   - Foreign key to `PeriodicReviewTask` (Cascade).
   - Captures `PageNumber`, `SectionNumber`, `NoteText`, `Status` (`Open`, `Resolved`), `CreatedByUserId`, and `CreatedAt`.
   - Preserves reviewer notes independently from document revision changes.

#### 2.3 Strict Segregation of Duties (SoD) Enforcement
- Hard service-level guard evaluates `DocumentRevision.CreatedByUserId` against the acting reviewer ID.
- The author of the revision under review cannot be assigned as reviewer (`AssignReviewerAsync`), cannot log review notes/findings (`AddFindingAsync`), and cannot execute the review completion ceremony (`CompleteReviewAsync`).
- System Administrator role is explicitly non-exempt; violations throw `UnauthorizedAccessException` and are logged to the audit trail.

#### 2.4 Controlled Workspace & Frontend Integration
- **`PeriodicReviewWorkspaceDialog.tsx`:** Dedicated modal displaying document metadata ribbon, overdue indicator, controlled PDF viewer with download controls, findings table with page/section inputs, and outcome submission controls.
- **`DocumentDetailPage.tsx`:** Added Tab 6 ("Periodic Review") displaying perpetual review history across all revisions, current active task status, and workspace launcher.
- **`DocumentLibraryPage.tsx`:** Added "Review Overdue Only" toggle switch and overdue badge styling.
- **`DocumentControlDashboardPage.tsx`:** Added "Review Overdue" KPI alert card highlighting documents past their scheduled review date.

---

### 3. Verification & Testing Evidence

#### 3.1 Unit Test Execution (`MicroLIMS.Tests.UnitTests.DocumentControlPeriodicReviewUnitTests`)
12 targeted unit tests executed via .NET test runner:
1. `GenerateDueReviewTasks_WhenDue_CreatesPeriodicReviewTask` — PASSED
2. `GenerateDueReviewTasks_WhenNotDue_DoesNotCreateTask` — PASSED
3. `GenerateDueReviewTasks_WhenTaskAlreadyActive_IsIdempotent` — PASSED
4. `GetReviewWorkspace_LoadsTaskAndControlledPdfMetadata` — PASSED
5. `AddFinding_PersistsPageAndSectionNote_TransitionsStatusToInProgress` — PASSED
6. `AddFinding_WhenAuthorAttemptsReview_ThrowsUnauthorizedAccessException` — PASSED
7. `CompleteReview_RemainsValid_AdvancesNextReviewDate_WithoutCreatingNewRevision` — PASSED
8. `CompleteReview_RevisionRequired_PreservesFindingsForHandoff` — PASSED
9. `CompleteReview_ObsolescenceRecommended_RoutesToQualityApproval` — PASSED
10. `DowntimeCatchUp_OverdueRevisionsProcessedImmediately` — PASSED
11. `CompletedReview_CannotBeAlteredOrRecompleted` — PASSED
12. `FailureIsolation_WhenOneDocumentFails_OtherDocumentsContinueProcessing` — PASSED

#### 3.2 Live PostgreSQL Integration Tests (`MicroLIMS.Tests.IntegrationTests.DocumentControlPeriodicReviewPostgresIntegrationTests`)
5 live PostgreSQL database tests executed against real PostgreSQL container:
1. `Postgres_GenerateDueReviewTasks_PersistsTaskAndLogsSystemAudit` — PASSED
2. `Postgres_GenerateDueReviewTasks_IsStrictlyIdempotent` — PASSED
3. `Postgres_CompleteReview_RemainsValid_AdvancesNextReviewDateInPostgres` — PASSED
4. `Postgres_CompleteReview_ObsolescenceRecommended_CreatesApprovalTaskInPostgres` — PASSED
5. `Postgres_AuthorCannotReviewOrComplete_EnforcesSegregationOfDuties` — PASSED

#### 3.3 Full Backend Regression Suite Results
- Total Tests: **707**
- Passed: **707**
- Failed: **0**
- Skipped: **0**
- Duration: **41 seconds**
- Regressions: **0**

#### 3.4 Frontend Production Verification
- `npm run build` (`tsc -b && vite build`) executed in `frontend/`.
- Result: **0 TypeScript errors, 0 lint warnings, production bundle successfully generated in 19.70s**.

---

### 4. Traceability & Change Control Reconciliation

1. **Change Control:** `CC-DC-R1B-006` recorded all additive changes, entities, worker extensions, and UI additions.
2. **Requirements Traceability Matrix:** `ML-DC-RTM-1B-001` updated to reflect `IMPLEMENTED` status for `DC-URS-044`, `DC-URS-045`, `DC-URS-046`, `DC-URS-047`, `DC-URS-048`, `DC-URS-049`, `DC-URS-053`, `DC-URS-054`, and `DC-URS-179`.
3. **Validation Rule Adherence:** No requirement was prematurely marked `QUALIFIED`. All requirements remain in `IMPLEMENTED` state pending formal execution of the Release 1b OQ/UAT protocol.

---

### 5. WP6 Boundary Stop Condition Verification

Work Package 6 implementation is officially complete and verified. In strict accordance with user instructions:
- Implementation has **STOPPED** at the WP6 boundary.
- **WP7 (Frontend Completion & Integration)** has NOT been started.
- **WP8 (Integration Verification)** and **WP9 (Release 1b OQ/UAT)** have NOT been started.
- No functional code beyond WP6 scope has been introduced.
