# Controlled Change Record: CC-DC-R1B-005
## Release 1b — Work Package 5 (WP5) Effective Date Automation Worker

**Document Identifier:** `CC-DC-R1B-005`  
**Change Request Date:** September 4, 2026  
**Change Classification:** Minor — Additive Service & Hosted Background Worker Implementation  
**Status:** APPROVED & BASELINED  
**Initiator:** Antigravity AI (Pair Programmer)  
**System Classification:** GAMP 5 Category 4 (Configured) / 21 CFR Part 11 & EU Annex 11 Compliant  

---

### 1. Change Description & Rationale

#### 1.1 Purpose
Work Package 5 (WP5) implements the automated effective-date activation and supersession processing engine for the MicroLIMS Document Control module, fulfilling:
- `DC-URS-177`: Automatic activation of `FutureEffective` revision on its scheduled effective date (`EffectiveDate <= UtcNow`).
- `DC-URS-178`: Automatic supersession of the prior `Effective` revision for the same document master within the same atomic transaction.
- `DC-URS-180`: System attribution in the audit trail (`ActorType.System`, `SystemProcessName = "DocumentEffectiveDateWorker"`, `UserId = null`, `Username = "SYSTEM"`).
- `DC-URS-181`: Downtime recovery and catch-up processing for revisions that matured while the application was stopped/offline, logging `IsDelayedExecution` audit metadata when executed $>1$ hour past due.
- `DC-URS-182`: Strict UTC standardization (`DateTime.UtcNow`) for all boundary comparisons and audit timestamps.
- `DC-URS-183`: Scheduled process failure alerting without halting batch processing of independent document masters; emission of `SystemProcessError` audit events.

#### 1.2 Components to be Created / Modified
1. **`backend/MicroLIMS.Application/Interfaces/DocumentControl/IDocumentEffectiveDateService.cs` (NEW):**
   - Defines `ProcessMaturedRevisionsAsync(DateTime? utcNowOverride = null, CancellationToken cancellationToken = default)`.
   - Returns a structured execution report (`EffectiveDateProcessingResultDto`) detailing processed, superseded, skipped, and failed revisions.
2. **`backend/MicroLIMS.Application/Services/DocumentControl/DocumentEffectiveDateService.cs` (NEW):**
   - Core domain business logic for selecting eligible `FutureEffective` revisions where `EffectiveDate <= UtcNow`.
   - Executes atomic transitions per document master:
     - Sets `FutureEffective` revision to `Effective`.
     - Calculates `NextReviewDate = EffectiveDate.AddMonths(ReviewCycleMonths)`.
     - Sets prior `Effective` revisions to `Superseded`.
     - Updates `DocumentMaster.CurrentEffectiveRevisionId`.
     - Records system-attributed audit events via `IAuditEventService.RecordSystemEventAsync` (`ActionCode = "RevisionAutomaticallyActivated"` and `"RevisionAutomaticallySuperseded"`).
     - Isolates failures per document master to guarantee that one malformed master does not block independent documents.
     - Logs `ActionCode = "SystemProcessError"` (`AuditActionCategory.Security` / `Other`) upon unhandled processing failure.
3. **`backend/MicroLIMS.Infrastructure/BackgroundServices/DocumentEffectiveDateWorker.cs` (NEW):**
   - Implements ASP.NET Core `BackgroundService`.
   - Configurable execution interval (defaults to 60 minutes, configurable via `DocumentControl:EffectiveDateWorkerIntervalMinutes` or `TimeSpan`).
   - Executes immediate catch-up cycle upon application startup (`ExecuteAsync`), guaranteeing automatic downtime recovery.
   - Leverages `IServiceScopeFactory` to create isolated DI scopes per cycle.
   - Implements non-overlapping concurrency lock (`SemaphoreSlim(1, 1)` or state flag) to prevent concurrent cycle overlap.
4. **`backend/MicroLIMS.API/Extensions/ServiceCollectionExtensions.cs` (MODIFIED):**
   - Register `IDocumentEffectiveDateService` as scoped service.
   - Register `DocumentEffectiveDateWorker` as hosted background service (`services.AddHostedService<DocumentEffectiveDateWorker>()`).
5. **Traceability & Test Evidence:**
   - Update `Release_1b_Requirements_Traceability_Matrix.md`.
   - Author dedicated unit tests (`DocumentEffectiveDateWorkerUnitTests.cs`) and live PostgreSQL integration tests (`DocumentEffectiveDateWorkerPostgresIntegrationTests.cs`).

---

### 2. Risk & Impact Assessment

| Evaluation Dimension | Assessment | Justification |
|---|---|---|
| **Schema Impact** | **Zero / No Schema Changes** | Reuses existing `DocumentRevisionStatus` (`FutureEffective`, `Effective`, `Superseded`), `DocumentRevision`, `DocumentMaster`, and `AuditLogs` tables. No EF Core migrations required. |
| **Transaction Integrity** | **Atomic per Master** | Activation of the new revision, supersession of the prior revision, and updating of `DocumentMaster.CurrentEffectiveRevisionId` are executed inside a single atomic `DbContext` transaction per document master. |
| **Concurrency & Idempotency** | **Fully Idempotent & Safe** | Query filters strictly on `RevisionStatus == FutureEffective`. Once activated to `Effective`, subsequent worker runs will not select or re-process the revision. |
| **Downtime Recovery** | **Deterministic Catch-up** | Startup query evaluates `EffectiveDate <= UtcNow`. Any revision that matured during offline periods is immediately activated with `IsDelayedExecution` audit attribution. |
| **Segregation of Duties & Part 11** | **Preserved** | The worker does NOT generate human electronic signatures. The electronic signature was already captured during formal approval (WP4). The worker merely automates the scheduled lifecycle transition. |
| **Regression Risk** | **Zero** | Prior 676 tests will remain untouched and green. |

---

### 3. Verification & Acceptance Criteria
1. Dedicated unit tests verify: eligible future-effective transitions, not-yet-effective skips, exact UTC boundary matching, prior revision supersession, master pointer updates, idempotency on repeated runs, multi-document batch isolation, transient error handling, system audit attribution, and delayed execution flags.
2. PostgreSQL integration tests prove: atomic transitions in live PostgreSQL database, raw transactional consistency, rollback on simulated exception, and immutable audit row generation with `ActorType.System`.
3. Solution regression suite passes 100% with $\ge 676$ passing tests.
4. Frontend production build (`tsc -b && vite build`) succeeds with zero errors.
5. Traceability matrix updated for `DC-URS-177`, `178`, `180`, `181`, `182`, `183`.
