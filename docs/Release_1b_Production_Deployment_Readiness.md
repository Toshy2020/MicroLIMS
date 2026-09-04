# MicroLIMS Document Control — Release 1b
## Production Deployment Readiness & Operational Runbook

- **Document Identifier:** `ML-DC-R1B-DEP-001`
- **Release Version:** MicroLIMS v1.1.0-rel1b
- **Module:** Document Control Module (Release 1b)
- **Target Deployment State:** Production Staging / Controlled Production
- **Date:** 2026-09-04
- **Operational Status:** **TECHNICAL READINESS COMPLETE — PENDING RELEASE AUTHORIZATION**
- **Controlled Baseline:** Git commit `6fe5a61439c12af1c8bcd0f0bdc1018b01f48cab` (abbreviated: `6fe5a61`), PostgreSQL 16, .NET 8.0, React 18 Production Bundle

---

### 1. Production Deployment Scope & Baseline Delta

This runbook specifies the precise, controlled procedures required to promote MicroLIMS Document Control from the qualified test environment to production upon receipt of formal release authorization.

#### 1.1 Baseline Delta: Release 1a vs. Release 1b

| Dimension | Release 1a (Current Production) | Release 1b (Qualified Target) | Delta & Operational Impact |
|:---|:---|:---|:---|
| **URS Scope** | 60 Requirements (`DC-URS-001`..`060`) | 103 Cumulative Requirements (+43 Release 1b) | Technical reviews, revisions, approvals, e-signatures, workers |
| **Backend Build** | v1.0.0-rel1a | v1.1.0-rel1b (Commit `6fe5a61439c12af1c8bcd0f0bdc1018b01f48cab` / `6fe5a61`) | .NET 8.0 API binaries updated; background worker service added |
| **Frontend Bundle** | Release 1a static dist assets | Release 1b static dist bundle (`index-BJP3wpKa.js`) | Vite production build with review dossiers and e-sig modals |
| **Database Migrations** | Migrations 1–3 applied | Migrations 1–7 applied (+4 new migrations) | Schema changes: 4 new entity tables, review & periodic indexes |
| **Background Services**| None | `EffectiveDateWorker` hosted service | Periodic worker scanning scheduled activations |
| **Signature Engine** | Basic document audit logging | 21 CFR Part 11 Electronic Signature ceremony | Re-authentication gate with dual-factor password check |

---

### 2. Environment Configuration & Operational Controls

All target environments (Staging, Pre-Production, Production) must configure the following environmental parameters prior to binary execution:

| Parameter Name | Target Type | Production Requirement | Purpose & Compliance Context |
|:---|:---|:---|:---|
| `ConnectionStrings__DefaultConnection` | Database URI | Host, port, pool, SSL mode `Require` | PostgreSQL 16 connection with encrypted TLS |
| `JwtSettings__Secret` | Security Key | Cryptographically secure 256-bit key | API authentication & session token validation |
| `JwtSettings__ExpiryMinutes` | Integer | `60` (or organizational standard) | Token lifetime control for GxP session inactivity |
| `DocControl__StoragePath` | File Path | High-availability resilient storage mount | Secure repository for uploaded controlled document source files |
| `DocControl__EffectiveWorkerIntervalSeconds`| Integer | `60` (or `300`) | Frequency of background scan for future effective documents |
| `DocControl__SystemActorId` | String | `system:effective-date-worker` | Attributable ALCOA+ identity for automated batch promotions |
| `ASPNETCORE_ENVIRONMENT` | String | `Production` | Disables debug endpoints and swagger schema exposures |
| `TZ` / System Clock | System Setting | `UTC` | Standardizes all contemporaneous timestamps across hosts |

---

### 3. PostgreSQL Database Migration Runbook

Database modifications must be executed during an authorized maintenance window by a designated Database Administrator (DBA).

#### 3.1 Pre-Migration Verification & Backup Procedure
1. **Quiesce Application Traffic:** Shift ingress traffic to maintenance notice page.
2. **Execute Full Database Snapshot:**
   ```bash
   pg_dump -h <prod-db-host> -U <db-admin> -d microlims_prod -F c -b -v -f /var/backups/microlims_pre_r1b_$(date +%Y%m%d_%H%M%S).dump
   ```
3. **Verify Backup Integrity:**
   ```bash
   pg_restore --list /var/backups/microlims_pre_r1b_*.dump | head -n 20
   ```
4. **Inspect Current Migration State:**
   Confirm that the `__EFMigrationsHistory` table contains exactly the first 3 Release 1a migrations:
   - `20260902181229_AddDocumentControlEntities`
   - `20260902181230_AddAuditImmutabilityTriggers`
   - `20260902181231_AddDatabaseSequences`

#### 3.2 Controlled Migration Execution Steps
Apply the four (4) Release 1b migrations sequentially using the pre-compiled EF Core script or runtime migration command:

1. **Migration 4: `20260902211621_AddDocumentReviewEntities`**
   - Creates: `document_reviews`, `review_comments`
   - Foreign Keys: `document_reviews.document_id` -> `documents.id`
2. **Migration 5: `20260902214555_AddRevisionManagementEntities`**
   - Modifies: `document_revisions` (adds `superseded_by_revision_id`, `change_summary`, `revision_notes`)
   - Indexes: `ix_document_revisions_superseded_by`
3. **Migration 6: `20260904072635_AddDocumentApprovalTaskEntity`**
   - Creates: `document_approval_tasks`, `document_signatures`
   - Adds: Electronic signature fields (`signature_manifest`, `meaning`, `authenticated_user_id`, `signed_at_utc`)
4. **Migration 7: `20260904085622_AddPeriodicReviewEntities`**
   - Creates: `periodic_review_configs`, `periodic_review_schedules`, `periodic_review_history`
   - Indexes: `ix_periodic_reviews_next_due_date`

**Command Line Execution:**
```bash
dotnet ef database update --project src/MicroLIMS.Infrastructure --startup-project src/MicroLIMS.WebAPI --connection "<PROD_DB_CONNECTION_STRING>"
```

#### 3.3 Post-Migration Schema Verification
Run verification query to confirm all 7 migrations are registered:
```sql
SELECT "MigrationId", "ProductVersion" FROM "__EFMigrationsHistory" ORDER BY "MigrationId";
```
Expected row count: **7 rows**.

---

### 4. Application Service Deployment Sequence

To avoid race conditions or transient availability errors, deploy the application tiers in the following strict order:

```
[1. Database Snapshot & Migrations]
                ↓
[2. Backend API Service Deployment (.NET 8)]
                ↓
[3. Background Worker Service Activation]
                ↓
[4. Frontend Static Asset Deployment (Nginx/CDN)]
                ↓
[5. Post-Deployment Smoke Test Protocol Execution]
```

1. **Step 1: Database Migration:** Verify schema migration complete as detailed in Section 3.
2. **Step 2: Backend API Service:** Deploy compiled .NET 8.0 binaries (`MicroLIMS.WebAPI.dll`). Start the systemd service or container task. Verify `/health` returns `HTTP 200 OK`.
3. **Step 3: Background Worker:** Start the `EffectiveDateWorker` service. Monitor application logs to confirm worker initializes without lock contention.
4. **Step 4: Frontend Static Assets:** Replace static web assets in the web server directory (`/var/www/microlims/`) with the qualified Release 1b bundle (`dist/`). Purge edge CDN caches.
5. **Step 5: Post-Deployment Smoke Test:** Execute `ML-DC-R1B-SMK-001` prior to reopening user access.

---

### 5. Rollback & Contingency Plan

In the event of an unrecoverable failure during deployment or a critical smoke test anomaly, the following rollback protocol must be invoked immediately:

#### 5.1 Rollback Decision Criteria
- Database migration script failure or lock acquisition timeout.
- Backend API service failing to start or throwing unhandled database connection exceptions.
- Post-deployment smoke test failure on Core Part 11 or Segregation of Duties invariants.

#### 5.2 Step-by-Step Rollback Execution
1. **Stop Application Services:**
   ```bash
   systemctl stop microlims-worker
   systemctl stop microlims-api
   ```
2. **Revert Database Schema:**
   Option A (Targeted Migration Reversion):
   ```bash
   dotnet ef database update 20260902181231_AddDatabaseSequences --project src/MicroLIMS.Infrastructure --startup-project src/MicroLIMS.WebAPI
   ```
   Option B (Full Snapshot Restore - Recommended if data was mutated):
   ```bash
   dropdb -h <prod-db-host> -U <db-admin> microlims_prod
   createdb -h <prod-db-host> -U <db-admin> microlims_prod
   pg_restore -h <prod-db-host> -U <db-admin> -d microlims_prod -v /var/backups/microlims_pre_r1b_*.dump
   ```
3. **Revert Application Binaries:**
   Deploy previous Release 1a backend binary package and restart services.
4. **Revert Frontend Assets:**
   Restore previous Release 1a `dist/` bundle to web root and invalidate CDN cache.
5. **Verification & Incident Reporting:**
   Run Release 1a regression smoke test. Log Formal Deployment Deviation Record.

---

### 6. Operational Monitoring & Health Indicators

Post-deployment monitoring must track the following system health indicators:

- **HTTP Endpoint Availability:** Continuous synthetic probing of `/health` and `/api/v1/documents`.
- **Database Connection Pool Metrics:** Monitor active connections in PostgreSQL (`pg_stat_activity`), ensuring connection count does not exceed allocated pool maximum (default: 100).
- **Background Worker Health:** Ensure log entries from `system:effective-date-worker` appear at the configured polling frequency without unhandled exceptions.
- **Audit Trail Trigger Latency:** Verify insert duration on `audit_trails` table remains under 10ms.
- **Error Rate Alerting:** Automated paging on HTTP 5xx responses exceeding 0.1% over a 5-minute rolling window.

---

### 7. Controlled Deployment Authorization Statement

This deployment runbook has been verified in the staging environment. 

**MANDATORY GOVERNANCE RESTRICTION:**  
Execution of this runbook in the production environment is strictly prohibited until the **Formal Release Approval Record (`ML-DC-R1B-APP-001`)** has been signed by the Technical Lead, Quality Assurance Lead, and System Owner.
