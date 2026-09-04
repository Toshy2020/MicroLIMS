# Controlled Change Record: CC-DC-R1B-006
## Release 1b — Work Package 6 (WP6) Periodic Review Engine & Workflow

**Document Identifier:** `CC-DC-R1B-006`  
**Change Request Date:** September 4, 2026  
**Change Classification:** Major — Additive Domain Entities, Service, Controller, Worker Extension & Workspace Implementation  
**Status:** APPROVED & BASELINED  
**Initiator:** Antigravity AI (Pair Programmer)  
**System Classification:** GAMP 5 Category 4 (Configured) / 21 CFR Part 11 & EU Annex 11 Compliant  

---

### 1. Change Description & Rationale

#### 1.1 Purpose
Work Package 6 (WP6) implements the dedicated Periodic Review management engine for the MicroLIMS Document Control module, fulfilling:
- `DC-URS-044`: Automated Periodic Review task creation based on the review schedule of the effective revision.
- `DC-URS-045`: Periodic Review workspace displaying the verified controlled PDF associated with the effective revision.
- `DC-URS-046`: Page and section-specific structured review notes/findings preserved as historical evidence.
- `DC-URS-047`: Enforced periodic review outcomes (`RemainsValid`, `RevisionRequired`, `ObsolescenceRecommended`).
- `DC-URS-048`: `RemainsValid` closes review, retains evidence, and creates NO new revision while preserving the current effective revision.
- `DC-URS-049`: `RemainsValid` advances the `NextReviewDate` according to the approved review cycle (`AddMonths(ReviewCycleMonths)` in UTC).
- `DC-URS-053`: Dedicated relational storage (`PeriodicReviewTask`, `PeriodicReviewFinding`) maintaining independent review history across revisions.
- `DC-URS-054`: Overdue periodic review identification (`NextReviewDate < UtcNow` in UTC) with library filtering and dashboard alerts.
- `DC-URS-179`: Automated task generation scheduled via background worker execution with full idempotency, downtime recovery, system attribution, and error isolation.

#### 1.2 Components to be Created / Modified
1. **Domain Entities & Enums:**
   - `backend/MicroLIMS.Domain/Enums/PeriodicReviewTaskStatus.cs` (Pending, InProgress, Completed, Cancelled).
   - `backend/MicroLIMS.Domain/Enums/PeriodicReviewOutcome.cs` (RemainsValid, RevisionRequired, ObsolescenceRecommended).
   - `backend/MicroLIMS.Domain/Enums/PeriodicReviewFindingStatus.cs` (Open, Resolved).
   - `backend/MicroLIMS.Domain/Entities/PeriodicReviewTask.cs` (Relational link to DocumentMaster, DocumentRevision, AssignedReviewerUser, CompletedByUser, with review cycle metadata, dates, outcome, notes).
   - `backend/MicroLIMS.Domain/Entities/PeriodicReviewFinding.cs` (Page, section, finding text, reviewer, status).
2. **Persistence & Database Configuration:**
   - `backend/MicroLIMS.Persistence/Configurations/PeriodicReviewTaskConfiguration.cs`.
   - `backend/MicroLIMS.Persistence/Configurations/PeriodicReviewFindingConfiguration.cs`.
   - Register DbSets in `MicroLimsDbContext`.
   - EF Core Migration `AddPeriodicReviewEntities`.
3. **Application Layer (DTOs & Service):**
   - `backend/MicroLIMS.Application/DTOs/DocumentControl/PeriodicReviewDtos.cs`.
   - `backend/MicroLIMS.Application/Interfaces/DocumentControl/IPeriodicReviewService.cs`.
   - `backend/MicroLIMS.Application/Services/DocumentControl/PeriodicReviewService.cs` (Generation, finding CRUD, completion with outcome rules, SoD validation: Author != Reviewer, admin non-exempt, RemainsValid date advance, RevisionRequired originating link handoff, Obsolescence routing to approval).
4. **Worker Integration:**
   - Extend `DocumentEffectiveDateWorker` to execute `IPeriodicReviewService.GenerateDueReviewTasksAsync` in its scheduled cycle and startup catch-up, preserving unified scheduler infrastructure while keeping business logic strictly separated.
5. **API Layer:**
   - `backend/MicroLIMS.API/Controllers/DocumentControl/PeriodicReviewController.cs`.
   - Update `DocumentMasterService.GetLibraryAsync` to support `IsOverdue` filtering.
6. **Frontend Layer:**
   - `frontend/src/modules/documentControl/types/documentControlTypes.ts` (Periodic review types).
   - `frontend/src/modules/documentControl/services/periodicReviewService.ts`.
   - `frontend/src/modules/documentControl/components/PeriodicReviewWorkspaceDialog.tsx` (PDF viewer, findings, outcomes, handoffs).
   - `frontend/src/modules/documentControl/pages/DocumentDetailPage.tsx` (Add dedicated Periodic Review History tab).
   - `frontend/src/modules/documentControl/pages/DocumentLibraryPage.tsx` (Add Overdue filter & status badge).
   - `frontend/src/modules/documentControl/pages/DocumentControlDashboardPage.tsx` (Add Overdue / Due Review KPI card).

---

### 2. Risk & Impact Assessment

| Dimension | Assessment | Justification |
|---|---|---|
| **Database Schema** | **Additive Only** | Introduces `PeriodicReviewTasks` and `PeriodicReviewFindings` tables. No existing tables are modified destructively. |
| **Data Integrity** | **ALCOA+ Compliant** | Review history is immutable once completed. Cannot alter completed reviews or delete history. |
| **Scheduler Separation** | **Unified Host, Clean Services** | Background worker delegates to `IPeriodicReviewService`, preventing duplicated timers while maintaining single-responsibility domain logic. |
| **Segregation of Duties** | **Strictly Enforced** | Author cannot be Reviewer; System Administrator cannot bypass SoD; unauthorized users blocked from review completion. |
| **Regression Impact** | **Zero** | 690 baseline tests verified; WP1-WP5 behavior unaffected. |

---

### 3. Acceptance & Verification Criteria
1. Automated unit test suite covers task creation, idempotency, duplicate prevention, overdue recognition, finding tracking, RemainsValid no-revision behavior, date advancement, RevisionRequired handoff, Obsolescence routing, SoD blocking, and failure isolation.
2. Live PostgreSQL integration tests verify persistence, triggers, rollback, and audit logging.
3. Full solution regression tests pass 100% green ($\ge 700$ tests).
4. Frontend build succeeds with zero errors.
