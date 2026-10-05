# System Description — MicroLIMS

| Field | Value |
|---|---|
| **Document No.** | ML-SYS-SD-001 |
| **Version** | 0.1 — Draft for review |
| **Related plan** | ML-SYS-VP-001 Validation Plan |
| **Source** | Written from the source code at branch `feat/physchem-areas`, commit `45f698f` (2026-10-05). Re-confirm against the frozen baseline before approval. |

---

## 1. Purpose of the system

MicroLIMS manages the testing work of a pharmaceutical quality-control laboratory with two labs:

- **Microbiology:** microbial limits, sterility-related and environmental monitoring testing, water testing, media and reference-strain control.
- **Physicochemical:** assay and related tests by HPLC, GC, ICP, titration, dissolution, disintegration, weight variation and other calculations, with working standards and solution preparation.

It records samples from receipt to approval, guides analysts through test steps, calculates and evaluates results against specifications, flags out-of-specification (OOS) results, and captures review and approval with electronic signatures. It also runs Document Control for the lab's SOPs and training.

## 2. Users and roles

| Role | Typical user | Main activities |
|---|---|---|
| **SystemAdministrator** | IT / LIMS admin | Users, roles, master data, configuration, audit and error logs |
| **SectionHead** | Lab head | Assigns work, supervises tests, approves results, lab dashboards |
| **Reviewer** | QC reviewer | Reviews completed tests and samples, returns work, requests retests |
| **Analyst** | Lab analyst | Receives and prepares samples, executes test steps, records results |

Roles are combinations of **48 permissions** (`backend/MicroLIMS.Shared/Constants/PermissionConstants.cs`). Every API endpoint is protected by a permission policy (`backend/MicroLIMS.API/Authorization/`). Users belong to a laboratory (Microbiology or Physicochemical) and see that lab's work.

## 3. Architecture

```
 Browser (React SPA)
      │ HTTPS
      ▼
 Cloudflare Workers static assets ── serves the frontend only
      │ HTTPS, JWT bearer token
      ▼
 Render web service: ASP.NET Core 10 API (Docker, Linux, non-root user)
      │                    │                       │
      │ TLS                │ S3 API over HTTPS     │ SMTP (optional)
      ▼                    ▼                       ▼
 Neon PostgreSQL      Backblaze B2 bucket     Mail provider
 (all records)        (uploaded files, PDFs)  (password reset, alerts)
```

| Layer | Technology | Location in code |
|---|---|---|
| Frontend | React 19, TypeScript, Material UI 9, Vite 8, React Router 7 | `frontend/` |
| API | ASP.NET Core 10 Web API, JWT authentication | `backend/MicroLIMS.API` |
| Business rules | Application services (all lab and GMP rules live here) | `backend/MicroLIMS.Application` |
| Domain model | Entities and enums | `backend/MicroLIMS.Domain` |
| Data access | EF Core 10 + Npgsql, 150 migrations | `backend/MicroLIMS.Persistence` |
| Infrastructure | File storage (local / S3), e-mail, PDF | `backend/MicroLIMS.Infrastructure` |
| Tests | xUnit (unit, workflow, PostgreSQL integration, architecture); Vitest + Testing Library | `backend/MicroLIMS.Tests`, `frontend/src/**/*.test.ts(x)` |

Clean Architecture boundaries (Domain → Application → Persistence/Infrastructure → API) are checked by architecture tests in `backend/MicroLIMS.Tests/ArchitectureTests`.

### 3.1 Deployment components (production)

| Component | Provider | Configuration source |
|---|---|---|
| Frontend | Cloudflare Workers static assets | `wrangler.jsonc` (builds `frontend/dist`) |
| API | Render, Docker | `backend/Dockerfile`; environment variables in Render |
| Database | Neon serverless PostgreSQL, SSL required | `ConnectionStrings__Default` |
| Files | Backblaze B2, private bucket, encryption on, versioning kept | `Storage__*` variables |
| E-mail | Any SMTP provider (optional) | `Smtp__*` variables |

The deployment procedure and the full list of environment variables are in `DEPLOYMENT.md`. Database migrations run at API start-up when `APPLY_MIGRATIONS=true`. The first administrator is created from `Seed__InitialAdminPassword` and must change the password at first sign-in.

### 3.2 Background jobs (inside the API)

| Job | What it does |
|---|---|
| DatabaseHealthMonitorWorker | Polls database health; logs slow (>1 s) and blocked (>5 s) queries |
| CriticalAlertWorker | E-mails critical errors (disabled by default) |
| ErrorLogRetentionWorker | Deletes error-log entries after 180 days (low severity) or 365 days (high). **Error logs only, never GMP records.** |
| DocumentEffectiveDateWorker | Makes approved documents effective on their effective date |
| SolutionPreparationExpiryWorker | Marks prepared solutions expired |

## 4. Functional overview

| Area | Main functions | Frontend module(s) |
|---|---|---|
| Sample receiving | Register samples by type (RM, FP, water, EM, after-cleaning); reference numbers; assign tests from specifications | `receiving`, `receivingTesting` |
| Testing workspace | Step-by-step test execution, incubation tracking, readings, confirmatory/biochemical decisions | `testingWorkspace`, `testPreparation` |
| Physicochemical workspaces | HPLC (methods, runs, SST), GC, ICP, titration, calculations by equation type | `hplcWorkspace`, `icpWorkspace`, `testingWorkspace` |
| Solutions & standards | Solution preparation and expiry, working standard qualification, titrant standardisation | `solutionPreparation`, `workingStandards` |
| Media & strains | Media products and lots, preparation, release, evaluation; cryovials | `inventory`, `laboratoryConfiguration` |
| Review & approval | Test and sample review, return, retest, approval, CoA/report | `review`, `approval`, `reports` |
| OOS | OOS flagging and investigation tracking | `oosTracking` |
| Master data | Items, materials, specifications, test definitions and steps, equipment, rooms, water points, organisms | `laboratoryConfiguration`, `inventory` |
| Document Control | Register, revisions, review/approval, periodic review, training | `documentControl` |
| Administration | Users, roles, audit search, error monitoring | `users`, `roles`, `auditSearch`, `errorMonitoring` |
| Collaboration | Discussions, messages, notifications | `discussions`, `messages` |
| Dashboards | Role dashboards and KPIs | `dashboard` |

## 5. GMP records held by the system

| Record | Main tables |
|---|---|
| Samples and their tests | `Samples`, `TestOrders`, `SampleTests`, `TestAnalyses` |
| Results and readings | `Results`, `ResultRecords`, `ResultReadings`, `CountTestReadings`, `ParameterResults`, calculation-data tables |
| Instrument runs | `HplcRuns`, `IcpRuns`, titrant standardisations |
| Media, solutions, standards | `Media`, `MediaEvaluations`, `SolutionPreparations`, `WorkingStandardQualifications`, `Cryovials` |
| Workflow history | `WorkflowHistory`, `ReviewWorkflowEvents`, `TestReturnEvents` |
| Electronic signatures | `ElectronicSignatures` |
| Audit trail | `AuditLogs`, `AuditEventChanges`, `SecurityAuditEvents`, `LoginHistory` |
| Master data / configuration | `Specifications`, `TestDefinitions`, `TestWorkflowSteps`, `MediaConfigurations`, `HplcMethods`, `IcpMethods`, `Equipment`, ... |
| Controlled documents | `DocumentMasters`, `DocumentRevisions`, `RevisionFiles` (+ files in B2) |
| Archived records | `ArchivedRecords`, `ReportSnapshots` (+ PDFs in B2) |

## 6. Part 11 / Annex 11 controls implemented

| Requirement | How the system meets it | Code reference |
|---|---|---|
| Unique user accounts | Username per person; BCrypt password hashes | `Domain/Entities/User.cs` |
| Password rules | At least 8 characters, an uppercase letter and a digit; last 5 passwords cannot be reused | `Shared/Validation/PasswordPolicy.cs`, `Application/Services/AuthenticationService.cs` |
| Lockout | Account locks after 5 failed logins; signing locks after 5 failed attempts | `AuthenticationService.cs`, `ElectronicSignatureService.cs` |
| Session control | Access token 15 min by default (1–480 configurable), refresh token 7 days; disabling, locking, role change or password change takes effect on the next request; browser idle log-out after 20 min | `API/Authorization/AccessTokenRevalidator.cs`, `frontend/src/hooks/useIdleTimeout.ts` |
| Login rate limiting | Per-IP limits on login, token refresh and password change | `DEPLOYMENT.md` (`RateLimiting__*`) |
| Electronic signature | Password re-entry at signing; the record holds the signer's name, username and role **as at signing**, the meaning (e.g. Reviewed, Approved, ResultRecorded), date/time (UTC), comment and IP address, linked to the signed record | `Domain/Entities/ElectronicSignature.cs`, `Domain/Enums/SignatureMeaning.cs` |
| Audit trail | Each change to an audited entity records user, time, action, previous and new value, and reason where required; captured automatically on save | `Persistence/DbContext/MicroLimsDbContext.cs` (`CaptureAuditEntries`), `Domain/Entities/AuditLog.cs` |
| Audit / signature immutability | Database triggers reject UPDATE and DELETE on `AuditLogs`, `AuditEventChanges` and `ElectronicSignatures` | Migration `20260902181230_AddAuditImmutabilityTriggers` |
| Audit review | Audit search screen filtered by batch, sample, media lot, strain, cryovial, document | `frontend/src/modules/auditSearch` |
| Security events | Logins, lockouts, role changes and similar recorded as security audit events | `Shared/Constants/SecurityEventCodes.cs` |
| No hard deletion of GMP records | Samples are voided, not deleted; voiding and correction require a signature | `Application/Services` (sample correction/void) |
| Time stamps | Stored in UTC; displayed and used for due dates in the lab time zone (`Lab:TimeZoneId`) | `appsettings.json` |
| Data protection in transit | HTTPS to frontend and API; TLS to the database (`SSL Mode=Require`) | `DEPLOYMENT.md` |
| Data protection at rest | Provider-level encryption (Neon; B2 bucket encryption enabled) | Supplier assessment to confirm |

Items the GxP assessment (ML-SYS-GXP-001) must still confirm:

1. Password expiry (maximum age) — not found in code; decide whether it is required.
2. Backup retention and restore capability on the chosen Neon plan.
3. Whether all GMP entities are covered by audit capture (entity-by-entity check).
4. Clock source and synchronisation for the API host.

## 7. Interfaces

| Interface | Direction | Data |
|---|---|---|
| Browser ↔ API | Two-way, HTTPS, JSON | All user operations |
| API → PostgreSQL | Two-way, TLS | All records |
| API → B2 (S3 API) | Two-way, HTTPS | Uploaded documents, attachments, archived PDFs |
| API → SMTP | Outbound | Password reset and alert e-mails |
| Lab instruments | **No direct interface.** Results are typed in, or exported files are attached as evidence. | — |

## 8. Configuration settings (non-secret)

| Setting | Default | Notes |
|---|---|---|
| `Jwt:AccessTokenMinutes` | 15 | 1–480 |
| `Lab:TimeZoneId` | Africa/Cairo | Drives due dates and incubation windows |
| `Storage:Provider` | Local | **Must be `S3` in production** |
| `MaterialDocuments:MaxFileSizeBytes` | 25 MB | |
| `ErrorMonitoring:*` | See `appsettings.json` | Error-log retention only |

Secrets (database connection string, JWT key, storage keys, SMTP password, initial admin password) are set only as environment variables in the hosting provider and are never stored in Git.

## 9. Approvals

| Role | Name | Signature | Date |
|---|---|---|---|
| Author (Developer / SME) | | | |
| System Owner | | | |
| Quality Assurance | | | |
