# Controlled Change Record: CC-DC-R1C-001
## Training Cascade Integration into Document Effective Date Processing

- **Change Control ID:** `CC-DC-R1C-001`
- **Release Target:** Release 1c (Work Package 2 / Work Package 3)
- **Component Affected:**
  - `backend/MicroLIMS.Application/Services/DocumentControl/DocumentEffectiveDateService.cs`
  - `backend/MicroLIMS.API/BackgroundServices/DocumentEffectiveDateWorker.cs`
  - `backend/MicroLIMS.API/Extensions/ServiceCollectionExtensions.cs`
- **Classification:** **RELEASE 1b CONTROLLED CHANGE REQUIRED (MINIMAL NON-BREAKING EXTENSION)**
- **Date:** 2026-09-04
- **Author:** System Architecture & Validation Lead

---

### 1. Description of Change & Operational Justification

#### 1.1 Context
In Release 1b, `DocumentEffectiveDateService` automatically promotes `DocumentRevision` entities from `FutureEffective` to `Effective` when `EffectiveDate <= UtcNow`, supersedes prior effective revisions, and updates `DocumentMaster.CurrentEffectiveRevisionId`.

In Release 1c, requirements `DC-URS-080`, `DC-URS-170`, `DC-URS-171`, `DC-URS-172`, `DC-URS-175`, and `DC-URS-176` mandate that when a revision transitions to `Effective`, the system must:
1. Automatically generate reading and training assignments for eligible users (prior completers and/or active curricula).
2. Cascade retraining assignments with source ancestry preserved (`SourceAssignmentId`).
3. Terminally close open assignments for the superseded revision (`SupersededIncomplete`).
4. Prevent duplicate assignments idempotently.

#### 1.2 Exact Change
To maintain strict separation of concerns, transactional safety, and zero regression of Release 1b, an optional service hook is introduced:
- `ITrainingAssignmentService` is injected into `DocumentEffectiveDateService` (or resolved via DI) as an optional dependency (`ITrainingAssignmentService? trainingAssignmentService = null`).
- In `ProcessSingleMasterRevisionAsync`, after the revision is committed as `Effective` and prior revision(s) as `Superseded`:
  ```csharp
  if (_trainingAssignmentService != null)
  {
      await _trainingAssignmentService.ProcessEffectiveRevisionCascadeAsync(
          freshRevision.Id,
          supersededRevisionId,
          nowUtc,
          cancellationToken);
  }
  ```
- In `DocumentEffectiveDateWorker.cs`, `RunCycleAsync` also invokes `trainingAssignmentService.ProcessAllActiveEffectiveRevisionsCascadeAsync(...)` as part of the scheduled worker cycle.

---

### 2. Safety & Non-Breaking Invariants

1. **Null-Safe Decoupling:** If `ITrainingAssignmentService` is not registered or resolved (e.g. in test suites or configurations running solely Release 1b), `DocumentEffectiveDateService` and `DocumentEffectiveDateWorker` execute identically to their Release 1b qualified baseline.
2. **Failure Isolation:** If assignment generation encounters an unexpected error, it is logged with a critical system audit event and isolated; it does not roll back an already committed regulatory status transition of the document revision itself, unless executed within the designated cascade transaction boundary.
3. **Idempotency Guarantee:** If the worker or service hook runs multiple times for the same effective revision, zero duplicate assignments are created due to composite unique constraints on `(DocumentRevisionId, AssignedUserId, AssignmentType)`.

---

### 3. Risk & Impact Analysis

| Dimension | Assessment |
| :--- | :--- |
| **Risk of Regressing Release 1b Effective Date Logic** | **LOW:** Core revision transition logic, dates, and audit events remain unchanged. Tested against the 737 passing baseline. |
| **Database Schema Impact** | **NONE:** No columns or tables on Release 1b entities (`DocumentMaster`, `DocumentRevision`, `User`) are altered. Only Release 1c tables (`DocumentTrainingAssignments`, etc.) are queried/inserted. |
| **Performance Impact** | **NEGLIGIBLE:** Assignment generation occurs per activated revision and queries only active users mapped to relevant curricula or prior revision completers. |

---

### 4. Verification Requirements

1. **Unit Tests:** Verify that `DocumentEffectiveDateService` functions identically when `ITrainingAssignmentService` is null or provided.
2. **PostgreSQL Integration Tests:** Verify live database end-to-end activation from `FutureEffective` $\rightarrow$ `Effective` triggers assignment generation and supersedence of open assignments.
3. **Regression Test:** Full backend test suite must pass without a single failure (maintaining 100% green).

---

### 5. Rollback Strategy
If any anomaly is detected in Release 1c assignment generation, removing `ITrainingAssignmentService` from DI in `ServiceCollectionExtensions.cs` immediately returns the worker to pure Release 1b behavior without code modifications.
