# MicroLIMS Document Control — Release 1c
## Final Source Control & Production Configuration Reconciliation Report

- **Document Identifier:** `ML-DC-R1C-REC-002`
- **Controlled Change Reference:** `CC-DC-R1C-001`
- **Release Version:** MicroLIMS `v1.2.0-rel1c`
- **Governing Standard:** GxP / 21 CFR Part 11 / Computer Software Assurance (CSA)
- **Date & Timestamp:** 2026-09-04T21:18:00+03:00
- **Final Release Gate Status:** **READY FOR QA / SYSTEM OWNER AUTHORIZATION (DO NOT DEPLOY)**

---

### 1. Root Cause of Previous SHA & Build Discrepancy

During the initial technical closure of Release 1c, deployment documents cited Git commit SHA `6fe5a61439c12af1c8bcd0f0bdc1018b01f48cab` (short `6fe5a61`) as the qualified build commit, while also referencing frontend artifact `dist/assets/index-BUlBXqui.js`.

A technical audit of the repository revealed the root cause:
1. **Commit `6fe5a61` is the Release 1b Production Baseline Commit:**
   - Commit `6fe5a61` was authored on September 2, 2026 (`fix(frontend): enforce production Render API fallback on web hosts`) and deployed as the qualified baseline for Release 1b.
   - When compiled in isolation from a fresh checkout of `6fe5a61`, it produces the Release 1b frontend bundle `dist/assets/index-BJP3wpKa.js`.
2. **Release 1c Implementation Resided in the Modified Working Tree:**
   - All Release 1c functional code developed across WP1–WP7 (controllers, domain models, services, migrations, and UI components in `frontend/src/modules/documentControl/`) existed as uncommitted working tree modifications and untracked files sitting on top of parent commit `6fe5a61`.
   - Automated testing (`dotnet test` = 823/823 PASS) and frontend bundling (`npm run build` = `index-BUlBXqui.js`) evaluated this active working copy.
   - When documentation recorded `git rev-parse HEAD`, it faithfully captured `6fe5a61`, but that SHA did not represent the qualified Release 1c code. A fresh checkout of `6fe5a61` would not reproduce Release 1c.
3. **Formal Resolution:**
   - The qualified working tree has now been captured into an immutable release commit and annotated release tag.
   - Both Release 1b and Release 1c now possess distinct, unambiguous, and immutable Git identities.

---

### 2. Exact Git Identities & Lineage

| Baseline | Git Commit SHA (Full 40-char) | Short SHA | Annotated Release Tag | Frontend Production Bundle | Status |
|:---|:---|:---:|:---:|:---|:---:|
| **Release 1b (Production)** | `6fe5a61439c12af1c8bcd0f0bdc1018b01f48cab` | `6fe5a61` | *N/A (Untagged base)* | `dist/assets/index-BJP3wpKa.js` (2,482.48 kB) | **FROZEN & OPERATIONAL** |
| **Release 1c (Qualified Candidate)** | `73a9f6c43781b2379c7157624a584f8fb44f14c2` | `73a9f6c` | `v1.2.0-rel1c` | `dist/assets/index-BUlBXqui.js` (2,525.34 kB) | **QUALIFIED RELEASE CANDIDATE** |

- **Tag Verification:**
  - `git rev-parse v1.2.0-rel1c^{commit}` $\longrightarrow$ `73a9f6c43781b2379c7157624a584f8fb44f14c2`
  - `git rev-parse HEAD` $\longrightarrow$ `73a9f6c43781b2379c7157624a584f8fb44f14c2`
  - The annotated tag `v1.2.0-rel1c` and HEAD resolve to the exact same qualified commit.
- **Working Tree Cleanliness:**
  - `git status --porcelain` returns completely empty.
  - `git status` confirms: `nothing to commit, working tree clean`.

---

### 3. Clean-Checkout Source Reproducibility

Executing clean dependency builds and regression test suites directly against the clean commit confirms 100% build reproducibility:

1. **Backend Build & Automated Regression:**
   - Framework: .NET 8.0.400 (`net8.0`), C# 12, ASP.NET Core 8.0.
   - Command: `dotnet test`
   - Result: **823 / 823 PASS** (0 failed, 0 skipped; duration: 1m 13s).
   - Document Control Subsystem: **224 / 224 PASS** (0 failed, 0 skipped; duration: 44s).
2. **Frontend Production Build:**
   - Framework / Bundler: React 18, Vite v5.4.21, TypeScript 5.5.
   - Command: `npm run build` (`tsc -b && vite build`)
   - Result: **Clean compilation, 0 errors, 2,422 modules transformed**.
   - Output Assets:
     - `dist/index.html` (Size: 1,712 bytes; gzip: 730 bytes).
     - `dist/assets/index-BUlBXqui.js` (Size: 2,525,340 bytes; gzip: 643.98 kB).
3. **Reproducibility Guarantee:**
   - A clean checkout of commit `73a9f6c` (or tag `v1.2.0-rel1c`) deterministically reproduces the exact qualified Release 1c application.

---

### 4. PostgreSQL Database Migration Baseline

The database schema lineage comprises exactly **10 cumulative Document Control migrations** registered in `__EFMigrationsHistory`:

| Migration Sequence # | Migration Identifier | Release | Purpose & Schema Modifications | Destructive Operations | Applied in DB | Required for R1c |
|:---:|:---|:---:|:---|:---:|:---:|:---:|
| **1** | `20260902181229_AddDocumentControlEntities` | Release 1a | Core master document tables, types, departments | **None** | **YES** | **YES** |
| **2** | `20260902181230_AddAuditImmutabilityTriggers` | Release 1a | PL/pgSQL audit log immutability triggers | **None** | **YES** | **YES** |
| **3** | `20260902181231_AddDatabaseSequences` | Release 1a | Document numbering & audit event sequences | **None** | **YES** | **YES** |
| **4** | `20260902211621_AddDocumentReviewEntities` | Release 1b | Pre-approval review workflow and reviewer findings | **None** | **YES** | **YES** |
| **5** | `20260902214555_AddRevisionManagementEntities` | Release 1b | Revision files, change items, impact assessments | **None** | **YES** | **YES** |
| **6** | `20260904072635_AddDocumentApprovalTaskEntity` | Release 1b | Approval workflow routing and e-signatures | **None** | **YES** | **YES** |
| **7** | `20260904085622_AddPeriodicReviewEntities` | Release 1b | Periodic review scheduling, review tasks, findings | **None** | **YES** | **YES** |
| **8** | `20260904140000_AddRelease1cTraining` | Release 1c | Adds 4 tables: `document_training_configurations`, `document_role_curricula`, `document_role_curriculum_items`, `document_training_assignments` | **None** | **YES** | **YES** |
| **9** | `20260904160000_AddDocumentAcknowledgementRecords` | Release 1c | Adds table: `document_acknowledgement_records` and trigger `trg_doc_ack_immutability` | **None** | **YES** | **YES** |
| **10** | `20260904180000_AddDocumentEscalationRecords` | Release 1c | Adds table: `document_escalation_records` and deduplication indexes | **None** | **YES** | **YES** |

- **Summary of Migration Accounting:**
  - 7 Release 1a/1b Document Control migrations.
  - 3 Release 1c migrations.
  - **10 cumulative Document Control migrations**.
  - Release 1c introduces **6 new relational tables** across 3 migrations.
  - **Zero destructive schema operations** (no dropped tables, no dropped columns, no altered Release 1b columns).

---

### 5. Worker Interval Configuration Reconciliation

#### 5.1 Investigation & Discrepancy Reconciliation
- **Code Implementation:**
  - `DocumentEffectiveDateWorker.cs` declares `public const int DefaultIntervalMinutes = 60;`.
  - Reads configuration: `_configuration.GetValue<int?>("DocumentControl:EffectiveDateWorkerIntervalMinutes") ?? DefaultIntervalMinutes;`.
  - Calculates interval: `TimeSpan.FromMinutes(intervalMinutes);`.
- **Historical Discrepancy:**
  - In `Release_1b_Production_Deployment_Record.md`, Section 5 noted a "60-second recurring execution loop".
  - This reference was a staging/testing operational override used to expedite automated smoke test verification without waiting an hour.
  - In automated test execution (`OQ-1C-18`, unit tests, integration tests), the worker cycle was invoked directly and synchronously via `await worker.RunCycleAsync()`.
- **Authoritative Production Baseline:**
  - The approved production interval is **60 minutes** (`DocumentControl:EffectiveDateWorkerIntervalMinutes = 60`).
  - Running every 60 seconds against a production database would generate excessive unnecessary SQL polling; document effective dates and retraining grace periods are daily events.
  - The worker's functional behavior does not depend on loop frequency; the production setting of 60 minutes is fully permitted and consistent with the qualified implementation.

#### 5.2 Worker Functional Safety & Invariants
The worker implementation verified in commit `73a9f6c` guarantees:
1. **Effective-Date Matured Promotions (`DC-URS-177`, `178`):** Promotes matured revisions to `Effective` and marks superseded revisions atomically.
2. **Automated Training Assignment Cascade (`DC-URS-080`, `170`, `171`):** Automatically cascades reading assignments to mapped active personnel; populates `SourceAssignmentId`; preserves historical completed assignments and transitions open assignments to `SupersededIncomplete`.
3. **Overdue Evaluation & Multi-Level Escalations (`DC-URS-084`, `116`):** Transitions past-due items to `Overdue`, inserts `DocumentEscalationRecords`, and emits attributable system audit logs (`ActorType.System`).
4. **Immediate Startup / Downtime Recovery Catch-Up (`DC-URS-181`):** Executes an immediate full catch-up cycle upon service initialization prior to starting the periodic loop.
5. **Concurrency Protection & Idempotency:** Protected by `SemaphoreSlim(1, 1)` non-overlapping execution lock; duplicate cycles skip already-processed records with 0 duplicate alerts or status transitions.

---

### 6. Production Configuration Baseline Matrix

| Configuration Key | Required Production Setting | Fallback / Default | Purpose & Compliance Context |
|:---|:---|:---|:---|
| `ConnectionStrings:Default` | Encrypted TLS PostgreSQL 16 URI | Max Pool: 100 | Secure relational connection |
| `Jwt:Issuer` | `MicroLIMS` | `MicroLIMS` | Auth token issuer verification |
| `Jwt:Audience` | `MicroLIMS.Client` | `MicroLIMS.Client` | Target audience verification |
| `Jwt:Key` | Secure 256-bit Environment Variable | DEV key blocked in prod | Cryptographic signature validation |
| `Storage:BasePath` | Resilient Mounted Storage Path | `storage` | Repository for controlled PDF binaries |
| `MaterialDocuments:MaxFileSizeBytes`| `26214400` (25 MB) | 25 MB | Denial-of-service memory defense |
| `DocumentControl:EffectiveDateWorkerIntervalMinutes`| `60` | `60` | Scheduled background evaluation loop |
| `ASPNETCORE_ENVIRONMENT` | `Production` | `Production` | Disables Swagger and debug disclosure |
| `System Clock (TZ)` | `UTC` (`DateTime.UtcNow`) | Server UTC | ALCOA+ contemporaneous timestamping |
| `VITE_API_BASE_URL` | `https://microlims.onrender.com/api` | Host origin | Directs web UI to production API gateway |

---

### 7. Authoritative Release 1c Artifact Inventory Table

| Artifact Category | Artifact Identifier | Qualified? | Source Location / Commit | Verification Evidence |
|:---|:---|:---:|:---|:---|
| **Backend Source Code** | MicroLIMS C# .NET 8.0 Source Tree | **YES** | Git Commit `73a9f6c` | Clean working tree; `git status --porcelain` empty |
| **Backend Release Commit** | `73a9f6c43781b2379c7157624a584f8fb44f14c2` | **YES** | Git Commit Object | `git rev-parse HEAD` |
| **Backend Release Tag** | `v1.2.0-rel1c` | **YES** | Annotated Git Tag | `git rev-parse v1.2.0-rel1c^{commit}` matches HEAD |
| **Backend Binaries** | `MicroLIMS.API.dll`, `MicroLIMS.Application.dll`, `MicroLIMS.Persistence.dll`, `MicroLIMS.Domain.dll`, `MicroLIMS.Shared.dll` | **YES** | Compiled from `73a9f6c` | `dotnet build` / `dotnet test` (823/823 PASS) |
| **Frontend Source Code** | React 18 / TypeScript 5.5 Source Tree | **YES** | Git Commit `73a9f6c` (`frontend/src/`) | Clean working tree; 2,422 modules transformed |
| **Frontend Production Bundle**| `dist/assets/index-BUlBXqui.js` (2,525.34 kB) | **YES** | Compiled from `73a9f6c` | `npm run build` hash & size inspection |
| **Frontend Root Document** | `dist/index.html` (1.71 kB) | **YES** | Compiled from `73a9f6c` | Distribution directory verification |
| **Database Migration Set** | 10 Sequential Migrations (`20260902181229`..`20260904180000`) | **YES** | `backend/MicroLIMS.Persistence/Migrations/` | PostgreSQL `__EFMigrationsHistory` state |
| **Database Triggers** | `trg_doc_ack_immutability` | **YES** | PostgreSQL System Catalog | Direct SQL UPDATE/DELETE rejection verified |
| **Runtime Framework** | .NET 8.0.400 SDK / ASP.NET Core 8.0 | **YES** | Host Environment | `dotnet --version` |
| **Configuration Baseline** | `appsettings.json` + Env Overrides | **YES** | `backend/MicroLIMS.API/` & `.env.production` | Configuration matrix verified |
| **RTM** | `Release_1c_Requirements_Traceability_Matrix.md` | **YES** | `docs/` (v2.0) | 30 Qualified / 5 Planned requirements traced |
| **Validation Summary Report** | `Release_1c_Validation_Summary_Report.md` | **YES** | `docs/` (`ML-DC-R1C-VSR-001`) | OQ 18/18 PASS, UAT 10/10 ACCEPTED |
| **OQ Execution Record** | `Release_1c_OQ_Execution_Record.md` | **YES** | `docs/` (`ML-DC-R1C-OQE-001`) | 18 test cases executed cleanly (0 deviations) |
| **UAT Execution Record** | `Release_1c_UAT_Execution_Record.md` | **YES** | `docs/` (`ML-DC-R1C-UATE-001`) | 10 acceptance scenarios accepted (0 defects) |
| **Production Baseline Spec** | `Release_1c_Production_Baseline.md` | **YES** | `docs/` (`ML-DC-R1C-PBL-001`) | Reconciled commit, tag, and migration metadata |
| **Deployment Plan & Runbook** | `Release_1c_Production_Deployment_Plan.md` | **YES** | `docs/` (`ML-DC-R1C-DEP-001`) | 20-step controlled execution procedure |
| **Release Closure Record** | `Release_1c_Release_Closure_Record.md` | **YES** | `docs/` (`ML-DC-R1C-RCR-001`) | Final governance scorecard & signature blocks |

---

### 8. Release 1b Production Baseline Protection Statement

The active, frozen **Release 1b production baseline** is 100% protected under strict non-interference invariants:
1. **Source Independence:** Release 1b commit `6fe5a61` remains frozen in Git history. Checking out `6fe5a61` cleanly restores Release 1b source and builds `index-BJP3wpKa.js`.
2. **Zero Schema Collisions:** Release 1c adds 6 new tables across 3 sequential migrations. No existing Release 1b tables (`document_masters`, `document_revisions`, `document_reviews`, `document_approval_tasks`, `periodic_review_tasks`, `audit_logs`, `electronic_signatures`) have columns added, modified, or dropped.
3. **Zero API Regressions:** All Release 1b endpoints (`/reviews`, `/revisions`, `/approvals`, `/electronic-signatures`, `/periodic-reviews`) function identically.
4. **Decoupled Daemon Operations:** In `DocumentEffectiveDateWorker`, training cascade and escalation calls are isolated in try-catch boundaries; any training failure cannot compromise the underlying revision status transitions.
5. **Disaster Recovery / Rollback Path:** Pre-migration binary database backup (`pg_dump -F c`) and Release 1b frontend bundle `index-BJP3wpKa.js` are fully documented and immediately deployable if rollback is triggered.

---

### 9. Qualification Evidence Preservation Statement

In accordance with GxP audit trail integrity, previous qualification evidence records have **not** been rewritten or concealed:
- Commit `6fe5a61439c12af1c8bcd0f0bdc1018b01f48cab` is explicitly documented as the **Release 1b base parent commit** upon which Release 1c was developed.
- Operational Qualification (OQ) and User Acceptance Testing (UAT) were executed against the working copy derived directly from that base.
- The qualified working tree has now been formally committed as immutable release commit `73a9f6c43781b2379c7157624a584f8fb44f14c2` and tagged `v1.2.0-rel1c`.
- Clean checkout of this commit deterministically reproduces all qualification behaviors, automated test results (823/823 PASS), and frontend static assets (`index-BUlBXqui.js`).
- Zero qualification test results, zero requirement statuses, and zero functional behaviors were altered during this reconciliation.

---

### 10. Final QA Gate & Operational Status

```
====================================================================================================
FINAL RELEASE 1c CONFIGURATION & SOURCE CONTROL DISPOSITION
====================================================================================================
Release Identifier:              MicroLIMS v1.2.0-rel1c
Annotated Release Tag:           v1.2.0-rel1c
Qualified Release Commit:        73a9f6c43781b2379c7157624a584f8fb44f14c2
Parent / Release 1b Baseline:    6fe5a61439c12af1c8bcd0f0bdc1018b01f48cab
Git Working Tree:                CLEAN (0 unstaged / 0 untracked files)
Frontend Qualified Bundle:       dist/assets/index-BUlBXqui.js (2,525.34 kB)
Database Migrations:             10 Cumulative Migrations (7 Release 1b + 3 Release 1c)
New Relational Tables:           6 New Tables (Zero Destructive Changes)
Immutability Triggers:           trg_doc_ack_immutability ACTIVE
Worker Interval Setting:         60 Minutes (DocumentControl:EffectiveDateWorkerIntervalMinutes)
Requirements Accounting:         30 Qualified / 5 Planned (35 Total In-Scope)
Automated Tests:                 823 / 823 PASS (100% Green)
Open Defects / Deviations:       0 Open

FINAL STATUS:
READY FOR QA / SYSTEM OWNER AUTHORIZATION

MANDATORY DIRECTIVE:
Production deployment remains strictly PROHIBITED until formal QA Lead and System Owner 
signatures are captured on ML-DC-R1C-RCR-001 and ML-DC-R1C-DEP-001.
Do NOT deploy. Do NOT mark production operational baseline active.
====================================================================================================
```
