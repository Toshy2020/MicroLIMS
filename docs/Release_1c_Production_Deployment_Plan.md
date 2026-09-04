# MicroLIMS Document Control — Release 1c
## Production Deployment Plan & Operational Runbook

- **Document Identifier:** `ML-DC-R1C-DEP-001`
- **Release Version:** MicroLIMS v1.2.0-rel1c
- **Module:** Document Control Module — Subsystem: Training Matrix & Reading Lists
- **Target Deployment State:** Controlled Production Environment
- **Date:** September 4, 2026
- **Operational Status:** **TECHNICAL DEPLOYMENT PREPARATION COMPLETE — READY FOR QA / SYSTEM OWNER AUTHORIZATION**
- **Controlled Change Reference:** `CC-DC-R1C-001`
- **Qualified Software Build:** Git Commit SHA `276efb317336fcfcfc68c925bf13fed67d2e93bb` (Annotated Tag: `v1.2.0-rel1c`; Base Commit: `6fe5a61439c12af1c8bcd0f0bdc1018b01f48cab`)
- **Frontend Production Asset:** `frontend/dist/assets/index-BUlBXqui.js` (2,525.34 kB, gzip: 643.98 kB)
- **Database Engine:** PostgreSQL 16 on .NET 8.0 (`net8.0`)

---

### 1. Production Deployment Scope & Baseline Delta

This deployment runbook defines the controlled sequence required to transition the qualified **MicroLIMS Document Control Release 1c** baseline into production service upon receipt of formal QA and System Owner authorization.

#### 1.1 Baseline Delta: Production Release 1b vs. Target Release 1c

| Dimension | Release 1b (Current Production Baseline) | Release 1c (Qualified Target Release Candidate) | Delta & Operational Impact |
|:---|:---|:---|:---|
| **URS Scope** | 103 Requirements (`DC-URS-001`..`079`, `DC-URS-177`..`183`) | 133 Cumulative Qualified Requirements (+30 Qualified Release 1c; 5 Planned) | Full Personnel Qualification, My Reading List, Controlled PDF Viewer, Acknowledgement Engine, Escalations, Training Matrix, Compliance Dashboard |
| **Git Commit SHA** | `6fe5a61439c12af1c8bcd0f0bdc1018b01f48cab` | `276efb317336fcfcfc68c925bf13fed67d2e93bb` (Tag: `v1.2.0-rel1c`) | Source verified strictly matching qualified release commit baseline |
| **Backend Runtime** | .NET 8.0 (`net8.0`), C# 12 | .NET 8.0 (`net8.0`), C# 12 | Release 1c services, controllers, and worker hooks compiled |
| **Frontend Bundle** | `dist/assets/index-BJP3wpKa.js` | `dist/assets/index-BUlBXqui.js` (Size: 2,525,340 bytes) | React 18 production build with Training Matrix and Reading List |
| **Database Migrations** | 7 Migrations (`20260902181229`..`20260904085622`) | 10 Migrations (+3 Release 1c migrations) | 6 new relational tables, composite deduplication indexes, append-only trigger `trg_doc_ack_immutability` |
| **Worker Automation** | `DocumentEffectiveDateWorker` (Lifecycle activations only) | `DocumentEffectiveDateWorker` (Lifecycle + Training Cascade + Escalations) | Automated retraining cascade and overdue assignment escalations processed idempotently |
| **Evidentiary Capture** | Electronic signatures for approvals | Electronic Read-and-Understand acknowledgements | Immutable primary legal evidence capturing exact statement text, user ID, UTC time, and SHA-256 revision hash |

---

### 2. Qualified Database Migration Level & Inspection State

Release 1c requires applying three (3) sequential, non-destructive schema migrations on top of the active Release 1b database:

1. **Migration 8: `20260904140000_AddRelease1cTraining`**
   - Tables Added: `document_training_configurations`, `document_role_curricula`, `document_role_curriculum_items`, `document_training_assignments`
   - Constraints: Foreign keys to `document_masters`, `document_revisions`, `users`, `roles`, `document_departments`
   - Indexes: Unique composite index on `(document_revision_id, assigned_user_id, assignment_type)` preventing duplicate assignments.
2. **Migration 9: `20260904160000_AddDocumentAcknowledgementRecords`**
   - Table Added: `document_acknowledgement_records`
   - Attributes: `id`, `document_training_assignment_id`, `document_revision_id`, `acknowledged_by_user_id`, `acknowledged_at_utc`, `statement_text`, `document_file_hash_sha256`, `ip_address`, `session_id`, `created_at_utc`
   - Trigger Defense: Installs PL/pgSQL append-only trigger `trg_doc_ack_immutability` unconditionally rejecting raw `UPDATE` and `DELETE` queries.
3. **Migration 10: `20260904180000_AddDocumentEscalationRecords`**
   - Table Added: `document_escalation_records`
   - Attributes: `id`, `document_training_assignment_id`, `escalation_level`, `escalated_at_utc`, `recipient_user_id`, `supervisor_user_id`, `qa_user_id`, `resolved_at_utc`, `resolution_reason`
   - Indexes: Deduplication index on `(document_training_assignment_id, escalation_level, escalated_at_utc)`.

*All migrations are additive. Zero existing Release 1b tables (`document_masters`, `document_revisions`, `document_reviews`, `audit_logs`, `electronic_signatures`) are dropped or destructively modified.*

---

### 3. Production Configuration Verification & Non-Secret Effective Values

Target production hosts must verify the following non-secret configuration settings prior to startup:

| Configuration Setting Key | Required Value / Policy | Purpose & Regulatory Safety Context |
|:---|:---|:---|
| `ConnectionStrings:Default` | Encrypted TLS PostgreSQL 16 URI | Secure relational connection with connection pooling (Max: 100) |
| `Jwt:Issuer` | `MicroLIMS` | Authentication token issuer verification |
| `Jwt:Audience` | `MicroLIMS.Client` | Validates target audience |
| `Storage:BasePath` | Resilient Mounted Storage Path | Physical directory containing uploaded controlled PDF binaries |
| `MaterialDocuments:MaxFileSizeBytes`| `26214400` (25 MB) | Guard against denial-of-service memory exhaustion |
| `DocumentControl:EffectiveDateWorkerIntervalMinutes` | `60` (or `300`) | Scheduled evaluation loop for effective activations and overdue scans |
| `ASPNETCORE_ENVIRONMENT` | `Production` | Disables Swagger, development error pages, and stack trace exposure |
| `System Clock (TZ)` | `UTC` (`DateTime.UtcNow`) | Strict synchronization of all contemporaneously recorded ALCOA+ timestamps |

---

### 4. Controlled Deployment Sequence (20 Steps)

```
[1. Confirm QA / System Owner Authorization]
                ↓
[2. Verify Maintenance Window & Quiesce Ingress Traffic]
                ↓
[3. Capture Full Pre-Deployment Binary Database Backup]
                ↓
[4. Verify Database Backup Integrity (pg_restore --list)]
                ↓
[5. Deploy Release 1c Backend Binaries (.NET 8.0)]
                ↓
[6. Apply Release 1c Migrations (Migrations 8–10)]
                ↓
[7. Verify PostgreSQL Triggers & Schema Constraints]
                ↓
[8. Deploy Release 1c Frontend Static Bundle (index-BUlBXqui.js)]
                ↓
[9. Invalidate CDN Edge Caches]
                ↓
[10. Start Backend API & Verify /health (HTTP 200 OK)]
                ↓
[11. Start DocumentEffectiveDateWorker Service]
                ↓
[12. Verify Initial Startup / Downtime Catch-Up Cycle in Logs]
                ↓
[13. Execute Pre-Release Production Smoke Suite (SMK-1C-01 to SMK-1C-12)]
                ↓
[14. Verify Controlled PDF Streaming & SHA-256 Checksum]
                ↓
[15. Verify Reading Assignment Scoping & Electronic Acknowledgement]
                ↓
[16. Verify Training Matrix Grid & Filter Rendering]
                ↓
[17. Verify Compliance Dashboard KPI Consistency]
                ↓
[18. Reopen Production User Ingress Traffic]
                ↓
[19. Compile & Sign Off Production Deployment Record]
                ↓
[20. Declare Operational Release 1c Baseline Active]
```

---

### 5. Backup & Rollback Readiness Strategy

#### 5.1 Pre-Deployment Database Backup Specification
- **Tool:** `pg_dump` with custom binary archive format (`-F c`).
- **Command:**
  ```bash
  pg_dump -h <prod-db-host> -U <db-admin> -d LIMSV2 -F c -b -v -f /var/backups/microlims_pre_r1c_$(date +%Y%m%d_%H%M%S).dump
  ```
- **Integrity Verification:**
  ```bash
  pg_restore --list /var/backups/microlims_pre_r1c_*.dump | grep "TABLE DATA" | head -n 30
  ```

#### 5.2 Controlled Rollback Plan
- **Rollback Decision Authority:** System Owner and Quality Assurance Lead.
- **Rollback Invalidation Conditions:**
  - Migration script failure or fatal deadlocks during schema update.
  - Backend API failing to initialize or throwing unhandled startup exceptions.
  - Critical failure on smoke testPart 11 e-signature or acknowledgement invariants.
- **Rollback Steps:**
  1. Stop `microlims-worker` and `microlims-api`.
  2. Restore database from pre-migration dump:
     ```bash
     dropdb -h <prod-db-host> -U <db-admin> LIMSV2
     createdb -h <prod-db-host> -U <db-admin> LIMSV2
     pg_restore -h <prod-db-host> -U <db-admin> -d LIMSV2 -v /var/backups/microlims_pre_r1c_*.dump
     ```
  3. Deploy qualified Release 1b backend binaries (`commit 6fe5a61`).
  4. Restore Release 1b frontend bundle (`index-BJP3wpKa.js`).
  5. Restart services, execute Release 1b smoke tests, and document incident.

---

### 6. QA & System Owner Release Gate Status

In strict adherence to GxP governance, production deployment cannot occur until formal authorizations are executed:

```
====================================================================================================
RELEASE 1c PRODUCTION DEPLOYMENT AUTHORIZATION GATE
====================================================================================================
QA Lead Authorization:        [ ] APPROVED    [ ] REJECTED    Date: ________________________
System Owner Authorization:   [ ] APPROVED    [ ] REJECTED    Date: ________________________

CURRENT GOVERNANCE STATUS: TECHNICAL PREPARATION COMPLETE — READY FOR AUTHORIZATION
(PRODUCTION DEPLOYMENT PROHIBITED UNTIL SIGNATURES ARE FORMALLY RECORDED)
====================================================================================================
```
