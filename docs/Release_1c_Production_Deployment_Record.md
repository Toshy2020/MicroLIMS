# MicroLIMS Document Control — Release 1c
## Production Deployment & Operational Release Record

- **Document Identifier:** `ML-DC-R1C-PDR-001`
- **Release Identifier:** MicroLIMS `v1.2.0-rel1c`
- **Module:** Document Control Module — Subsystem: Training Matrix & Reading Lists
- **Controlled Change Reference:** `CC-DC-R1C-001`
- **Governing Standard:** GxP / 21 CFR Part 11 / Computer Software Assurance (CSA)
- **Deployment Execution Timestamp:** 2026-09-04T21:35:00+03:00
- **Deployment Operator:** CSV Engineer / Lead Deployment Operator
- **Final Disposition:** **DEPLOYED AND VERIFIED — RELEASE 1c OPERATIONAL BASELINE ACTIVE**

---

### 1. Formal Deployment Authorization Status

Prior to commencing deployment, formal governance authorizations were verified and confirmed against `ML-DC-R1C-RCR-001` and `ML-DC-R1C-DEP-001`:

- **QA Lead Approval:** **APPROVED** (September 4, 2026)
- **System Owner Approval:** **APPROVED** (September 4, 2026)
- **Authorized Deployment Window:** September 4, 2026, 21:00–23:00 UTC+3
- **Deployment Operator:** CSV Engineer / Lead Deployment Operator

---

### 2. Pre-Deployment Integrity & Source Baseline Verification

| Pre-Deployment Verification Check | Required Parameter / Standard | Verified Actual Value | Status |
|:---|:---|:---|:---:|
| **Target Release Commit SHA** | `b7ed1c79df6e2afc32e3d0919abac5c7c8360ef9` | `b7ed1c79df6e2afc32e3d0919abac5c7c8360ef9` | **CONFIRMED** |
| **Short SHA** | `b7ed1c7` | `b7ed1c7` | **CONFIRMED** |
| **Annotated Tag Resolution** | `git rev-parse v1.2.0-rel1c^{commit}` | `b7ed1c79df6e2afc32e3d0919abac5c7c8360ef9` | **CONFIRMED** |
| **Working Tree Cleanliness** | `git status --porcelain` empty | Clean (0 modified / 0 untracked files) | **CONFIRMED** |
| **Release 1b Rollback Baseline** | `6fe5a61439c12af1c8bcd0f0bdc1018b01f48cab` | Intact parent commit (`6fe5a61`) | **CONFIRMED** |
| **Frontend Production Asset** | `dist/assets/index-BUlBXqui.js` | Present (`2,526,932` bytes) | **CONFIRMED** |
| **Rollback Frontend Asset** | `index-BJP3wpKa.js` | Available in repository history | **CONFIRMED** |

---

### 3. Pre-Migration Production Database Backup

In accordance with Section 4 of the deployment specification, a full binary custom-format archive was captured and verified prior to schema migration:

- **Backup Tool:** PostgreSQL `pg_dump` 18.4 (Custom Format `-F c`)
- **Target Database:** `LIMSV2` on `localhost:5432`
- **Backup File:** `E:\MicroLIMS\backups\microlims_prod_pre_r1c_20260904_213125.dump`
- **File Size:** `1,114,598` bytes
- **Creation Timestamp:** `2026-09-04 21:31:24 UTC+3`
- **Restore Verification:** Verified via `pg_restore --list`. TOC header entries confirmed readable (1,190 TOC entries including user tables, sequences, functions, and ACLs).
- **Integrity Status:** **VERIFIED & RESTORABLE**

---

### 4. Database Migration & Schema Verification

The database schema reflects exactly 10 cumulative Document Control migrations with 0 destructive operations:

| Migration Sequence # | EF Core Migration Identifier | Release | Relational Modifications | Status in DB |
|:---:|:---|:---:|:---|:---:|
| **1** | `20260902181229_AddDocumentControlEntities` | Release 1a | Core master tables, types, sections | Applied |
| **2** | `20260902181230_AddAuditImmutabilityTriggers` | Release 1a | Audit log append-only triggers | Applied |
| **3** | `20260902181231_AddDatabaseSequences` | Release 1a | Sequences for document numbering | Applied |
| **4** | `20260902211621_AddDocumentReviewEntities` | Release 1b | Pre-approval review workflow & findings | Applied |
| **5** | `20260902214555_AddRevisionManagementEntities` | Release 1b | Revision files, change items, impact | Applied |
| **6** | `20260904072635_AddDocumentApprovalTaskEntity` | Release 1b | Approval tasks & e-signature linking | Applied |
| **7** | `20260904085622_AddPeriodicReviewEntities` | Release 1b | Periodic review schedule & tasks | Applied |
| **8** | `20260904140000_AddRelease1cTraining` | Release 1c | 4 tables: curricula, items, assignments, configs | Applied |
| **9** | `20260904160000_AddDocumentAcknowledgementRecords` | Release 1c | Table: `DocumentAcknowledgementRecords` + Trigger | Applied |
| **10** | `20260904180000_AddDocumentEscalationRecords` | Release 1c | Table: `DocumentEscalationRecords` + Indexes | Applied |

#### Invariant Verifications:
- **Total Cumulative Migrations:** 10 Document Control migrations.
- **Relational Tables Added:** Exactly 6 relational tables (`DocumentRoleCurricula`, `DocumentRoleCurriculumItems`, `DocumentTrainingAssignments`, `DocumentTrainingConfigurations`, `DocumentAcknowledgementRecords`, `DocumentEscalationRecords`).
- **Immutability Triggers Active:**
  - `trg_documentacknowledgementrecords_immutable` on `DocumentAcknowledgementRecords` (BEFORE UPDATE OR DELETE)
  - `trg_auditlogs_immutable` on `AuditLogs`
  - `trg_auditeventchanges_immutable` on `AuditEventChanges`
  - `trg_electronicsignatures_immutable` on `ElectronicSignatures`
- **Destructive Changes:** **ZERO** (No Release 1b tables dropped or altered).

---

### 5. Production Worker & Configuration Verification

- **Hosted Daemon:** `DocumentEffectiveDateWorker` running as ASP.NET Core `BackgroundService`.
- **Configured Interval:** `DocumentControl:EffectiveDateWorkerIntervalMinutes = 60` (Default verified in worker logs).
- **Startup Recovery Cycle:** Worker executed initial startup / downtime recovery catch-up cycle cleanly on initialization (`09/04/2026 18:34:18 UTC`).
- **Task Cycles Executed:** Matured revision evaluation, periodic review task scanning, and overdue training escalation scanning executed with 0 unhandled exceptions.

---

### 6. Twelve-Point Post-Deployment Smoke Test Execution

All twelve smoke tests were executed against the live application runtime:

| Test ID | Checkpoint Name | Action & Verification Procedure | Acceptance Criteria | Result | Evidence |
|:---:|:---|:---|:---|:---:|:---|
| **SMK-1C-01** | **API Health & Service Availability** | Send HTTP GET to `/health` | HTTP 200 OK, `status: Healthy` | **PASS** | HTTP 200 `{"status":"Healthy"}` |
| **SMK-1C-02** | **User Authentication & Session Scoping** | Authenticate via `/api/auth/login` | Valid JWT issued with role & user claims | **PASS** | Valid JWT issued (Admin & Analyst) |
| **SMK-1C-03** | **My Reading List** | Route to `/document-control/my-reading-list` | Reading list renders pending/completed assignments | **PASS** | Client route operational |
| **SMK-1C-04** | **Controlled PDF Viewer & Revision Header** | Access controlled file view endpoint | Serves controlled PDF stream inline with header | **PASS** | `api/document-control/files/{id}/view` |
| **SMK-1C-05** | **Informational Reading Progress** | Submit reading progress update | Progress saved; status remains `Reading` (not completed) | **PASS** | `DocumentTrainingAssignment.Status == Reading` |
| **SMK-1C-06** | **Conscious Electronic Acknowledgement** | Submit signed acknowledgement request | Generates immutable record; transitions to `Acknowledged` | **PASS** | Record created; trigger protects record |
| **SMK-1C-07** | **Personal Assignment Scoping** | Query `/api/document-control/training-assignments/my-assignments` | Returns strictly assignments for authenticated user ID | **PASS** | Scoped query returned user-isolated data |
| **SMK-1C-08** | **Overdue Status & Escalation** | Query `/api/document-control/escalations/summary/overdue` | Overdue summary loads; escalation level queryable | **PASS** | HTTP 200 summary retrieved |
| **SMK-1C-09** | **Training Matrix** | Query `/api/document-control/training-matrix/grid` | Personnel × Documents matrix loads with active users | **PASS** | HTTP 200 returned grid (10 users mapped) |
| **SMK-1C-10** | **Compliance Dashboard** | Query `/api/document-control/training-matrix/kpis` | KPI summary loads compliance rate & overdue totals | **PASS** | HTTP 200 returned KPI summary |
| **SMK-1C-11** | **Audit Trail Attribution** | Query `/api/document-control/audit` | All actions logged with UTC timestamp & user attribution | **PASS** | HTTP 200 (9,454+ immutable audit logs) |
| **SMK-1C-12** | **Background Worker Execution & Log Health** | Inspect runtime worker logs | Periodic evaluation cycle executed without contention | **PASS** | Worker initial cycle logged clean |

**Smoke Test Pass Rate:** **12 / 12 (100% PASS)**

---

### 7. Release 1b Regression Check

Regression testing of the Release 1b baseline functions was confirmed:
- **Automated Regression Suite:** **823 / 823 PASS** (`dotnet test backend\MicroLIMS.sln`)
- **Document Control Subsystem:** **224 / 224 PASS**
- **Document Master & Revision Access:** `/api/document-control/library` operational (HTTP 200).
- **Electronic Signatures:** Append-only trigger and signature service intact.
- **Periodic Review:** Automated review task generation active.
- **Audit Logging:** System-wide audit trails recording contemporaneously.

---

### 8. Intentionally Deferred Requirements Statement

In strict compliance with GxP validation protocol, exactly 5 requirements remain intentionally deferred as **PLANNED** and are not deployed:
1. `DC-URS-091` — Training completion metrics export utility
2. `DC-URS-117` — Automated training assignment on user onboarding
3. `DC-URS-121` — Audit logging of training report generation
4. `DC-URS-122` — Self-auditing CSV/PDF matrix export with SHA-256 integrity
5. `DC-URS-176` — Retraining assignment re-issuance rules on failed state

**Qualification Accounting:** Exactly **30 / 35** Release 1c requirements are qualified and active.

---

### 9. Rollback Status

- **Rollback Invoked:** **NO** (All deployment steps and 12 smoke tests succeeded).
- **Rollback Baseline Designated:** Release 1b commit `6fe5a61439c12af1c8bcd0f0bdc1018b01f48cab` with frontend asset `index-BJP3wpKa.js` and verified database backup `microlims_prod_pre_r1c_20260904_213125.dump`.

---

### 10. Active Production Baseline Transition & Sign-Off

```
====================================================================================================
MICRO LIMS DOCUMENT CONTROL — ACTIVE PRODUCTION BASELINE RECORD
====================================================================================================
Active Production Release:    MicroLIMS v1.2.0-rel1c
Active Production Commit:     b7ed1c79df6e2afc32e3d0919abac5c7c8360ef9 (Short: b7ed1c7)
Annotated Release Tag:        v1.2.0-rel1c
Active Frontend Asset:        frontend/dist/assets/index-BUlBXqui.js (2,526,932 bytes)
Active Database Migrations:   10 Cumulative Migrations (7 R1b + 3 R1c)
Production Worker Interval:   60 Minutes
Operational Status:           PRODUCTION ACTIVE

PREVIOUS / ROLLBACK BASELINE:
Previous Production Baseline: MicroLIMS v1.1.0-rel1b
Rollback Commit SHA:          6fe5a61439c12af1c8bcd0f0bdc1018b01f48cab
Rollback Frontend Asset:      index-BJP3wpKa.js
Rollback Database Backup:     microlims_prod_pre_r1c_20260904_213125.dump

SIGN-OFF CONFIRMATIONS:
QA Lead Authorization:        APPROVED (2026-09-04)
System Owner Authorization:   APPROVED (2026-09-04)
Post-Deployment Smoke:        12 / 12 PASS (100% Green)
Regression Test Outcome:      823 / 823 PASS (100% Green)

FINAL DISPOSITION:
DEPLOYED AND VERIFIED — RELEASE 1c OPERATIONAL BASELINE ACTIVE
====================================================================================================
```
