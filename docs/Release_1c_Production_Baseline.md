# MicroLIMS Document Control — Release 1c
## Formal Production Baseline Specification

- **Document Identifier:** `ML-DC-R1C-PBL-001`
- **Release Identifier:** MicroLIMS `v1.2.0-rel1c`
- **Module:** Document Control Module (Release 1c)
- **Target Operational Baseline:** Controlled Production Environment
- **Date:** September 4, 2026
- **Controlled Change Reference:** `CC-DC-R1C-001`
- **Operational Status:** **PRODUCTION ACTIVE — RELEASE 1c OPERATIONAL BASELINE**

---

### 1. Controlled Artifact Identity & System Baseline

| Dimension | Controlled Release Baseline Specification | Verification Evidence |
|:---|:---|:---|
| **Release Identifier / Version** | `v1.2.0-rel1c` (Annotated Tag: `v1.2.0-rel1c`) | Controlled Release Marker & Tag |
| **Git Repository Root** | `E:\MicroLIMS\MicroLIMS` | Local Controlled Repository |
| **Release 1c Qualified Commit SHA**| `b7ed1c79df6e2afc32e3d0919abac5c7c8360ef9` (Short: `b7ed1c7`)<br>*(Parent / R1b Base: `6fe5a61439c12af1c8bcd0f0bdc1018b01f48cab`)* | `git rev-parse HEAD` & `git rev-parse v1.2.0-rel1c^{commit}` |
| **Backend Runtime & SDK** | .NET 8.0.400 (`net8.0`), C# 12, ASP.NET Core 8.0 | `dotnet --version` Verified |
| **Backend Assemblies** | `MicroLIMS.API.dll`, `MicroLIMS.Application.dll`, `MicroLIMS.Persistence.dll`, `MicroLIMS.Domain.dll`, `MicroLIMS.Shared.dll` | Debug/Release Build Artifacts |
| **Frontend Framework & Build** | React 18, Vite v5.4.21, TypeScript 5.5 | `npm run build` Verified |
| **Frontend Asset Hash** | `dist/assets/index-BUlBXqui.js` (Size: 2,526,932 bytes) | Distribution Bundle Inspection |
| **Frontend Root Document** | `dist/index.html` (Size: 1,712 bytes; gzip: 730 bytes) | Distribution HTML Verification |
| **Database Management System** | PostgreSQL 16 / 18 (Relational Engine with PL/pgSQL Triggers) | PostgreSQL Service Catalog |
| **Target Database Migrations** | 10 Total Document Control Migrations (7 Release 1b + 3 Release 1c: `20260904140000`..`20260904180000`) | EF Core Migration History State |
| **Database Trigger Invariants** | `trg_documentacknowledgementrecords_immutable`, `trg_auditlogs_immutable`, `trg_electronicsignatures_immutable` | PostgreSQL System Catalog Triggers |
| **Hosted Automation Daemon** | `DocumentEffectiveDateWorker` (Default interval: 60 minutes; configurable via `DocumentControl:EffectiveDateWorkerIntervalMinutes`) | Background Service Configuration |
| **System Time Standard** | UTC Standardized (`DateTime.UtcNow`) | Server Clock Synchronization |

---

### 2. Requirements & Scope Accounting

- **Total In-Scope Release 1c Requirements:** **35 Requirements (100%)**
- **Requirements Implemented & Formally Qualified:** **Exactly 30 Requirements (85.7%)**
  - Reading Workflow & Assignments: `DC-URS-080`, `081`, `082`, `083`, `084`, `085`, `086`, `087`, `088`, `089`, `090` (10)
  - Training Matrix & Compliance: `DC-URS-111`, `112`, `113`, `114`, `115`, `116`, `118`, `119`, `120` (9)
  - Retraining & Supersession: `DC-URS-170`, `171`, `172`, `173`, `174`, `175` (6)
  - Evidentiary Recording & Integrity: `DC-URS-189`, `190`, `191`, `192` (4)
- **Requirements Intentionally Deferred (PLANNED):** **Exactly 5 Requirements (14.3%)**
  1. `DC-URS-091` — Training completion metrics export utility
  2. `DC-URS-117` — Automated training assignment on user onboarding
  3. `DC-URS-121` — Audit logging of training report generation
  4. `DC-URS-122` — Self-auditing CSV/PDF matrix export with SHA-256 integrity
  5. `DC-URS-176` — Retraining assignment re-issuance rules on failed state

*Note: This release baseline explicitly represents 30 qualified requirements. It is NOT 35/35 qualified.*

---

### 3. Release 1b Production Baseline Protection Confirmation

The frozen Release 1b baseline is protected under strict non-interference invariants:
- **Zero Schema Collisions:** Release 1c introduces 6 new relational tables across 3 sequential migrations (`document_role_curricula`, `document_training_configurations`, `document_role_curriculum_items`, `document_training_assignments`, `document_acknowledgement_records`, `document_escalation_records`). No existing Release 1b tables or columns are modified.
- **Zero API Regressions:** All Release 1b endpoints (`/api/document-control/reviews`, `/api/document-control/revisions`, `/api/document-control/approvals`, `/api/document-control/electronic-signatures`, `/api/document-control/periodic-reviews`) function identically.
- **Full Backward-Compatible Worker:** `DocumentEffectiveDateService` contains a decoupled, null-safe invocation of `ITrainingAssignmentService`. If training assignments fail, the underlying revision promotion to `Effective` remains transactionally isolated unless executed within the designated cascade scope.

---

### 4. Technical Governance & Operational Status

```
====================================================================================================
MICRO LIMS RELEASE 1c BASELINE DISPOSITION
====================================================================================================
Current Status:         PRODUCTION ACTIVE — RELEASE 1c OPERATIONAL BASELINE
Qualified Build SHA:    b7ed1c79df6e2afc32e3d0919abac5c7c8360ef9 (Tag: v1.2.0-rel1c)
Parent Base Commit:     6fe5a61439c12af1c8bcd0f0bdc1018b01f48cab (Release 1b Rollback Baseline)
Backend Regression:     823 / 823 PASS (100% Green)
Frontend Production:    CLEAN BUILD (0 Errors, index-BUlBXqui.js)
Worker Interval:        60 Minutes (Default) / Configurable via DocumentControl:EffectiveDateWorkerIntervalMinutes
QA Lead Approval:       APPROVED (2026-09-04)
System Owner Approval:  APPROVED (2026-09-04)
Smoke Test Result:      12 / 12 PASS (100% Green)
Open Defects:           0
Open Deviations:        0
====================================================================================================
```
