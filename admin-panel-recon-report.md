# Admin Control Panel — Phase 0 Recon Report

**Scope:** Read-only recon of the MicroLIMS code at `f1cfb92` (branch `claude/eager-ramanujan-izk0z7`, same as `main` HEAD at time of recon).
**Method:** Source reading only. No files were modified, no migrations were run, nothing was built or installed. The live database was **not** queried, so anything that depends on production data (actual role grants, row counts, historic audit rows) is listed as an open question.
**Paths:** All paths are relative to the repo root (`E:\MicroLIMS\MicroLIMS` on the user's machine). `B/` = `backend/`, `F/` = `frontend/src/`.

**Governing principle (from the brief):** the admin panel controls the *system*, never the GxP *data*. Admins must not be able to edit results, approve or release samples, or modify audit records (21 CFR Part 11 §11.10(d)(g); EU GMP Annex 11 §12).

---

## 0. Headline findings (read these first)

| # | Severity | Finding | Evidence |
|---|---|---|---|
| H1 | **Critical (compliance)** | **The System Administrator role holds every permission, including sample review, sample approval/release, result-producing workflow execution, sample correction/void, media/cryovial release and plan approvals.** Nothing prevents it. This breaks the governing principle directly. | `B/MicroLIMS.Persistence/Seed/DbSeeder.cs:530` (`SystemAdministrator → PermissionConstants.All`); later migrations also grant new lab permissions to `Type = 0` (e.g. `…/Migrations/20260929184541_AddHplcWorkspace.cs:424-427`, `20260929173324_AddSolutionPreparation.cs:184-187`, `20260924192621_LabSeparationPrivileges.cs:29-32`) |
| H2 | **Critical (data protection)** | **A production database dump is committed to git**: `backups/microlims_prod_r1b_20260904_161300.dump` (≈1 MB, tracked). DbSeeder's comments state the repository is public (`DbSeeder.cs:399-404`). The dump would contain user password hashes and GxP records. I did not open it. | `git ls-files backups` |
| H3 | **High (security)** | **The generic audit capture writes `User.PasswordHash` into `AuditLogs`.** Every `User` update — including the `LastLoginAt` write on every successful login — produces an `Update` row whose `PreviousValue`/`NewValue` JSON holds all columns. `PasswordHistory` and `PasswordResetToken` inserts are captured the same way. The audit search API returns those JSON blobs unredacted to anyone with `Audit.View` (Section Heads included). Because `AuditLogs` is append-only by trigger, already-written hashes cannot be removed by an UPDATE. | Capture: `B/MicroLIMS.Persistence/DbContext/MicroLimsDbContext.cs:498-545` (only `RefreshToken` among auth tables is excluded, line 507); login write: `B/MicroLIMS.Application/Services/AuthenticationService.cs:138-139`; exposure: `B/MicroLIMS.Application/Services/AuditSearchService.cs:24-31, 112-118`; SectionHead has `Audit.View`: `DbSeeder.cs:533` |
| H4 | **High (audit integrity)** | `USER_ROLE_CHANGED` records the **new** RoleId as the previous value. `user.RoleId` is overwritten at line 178 and serialized as "previous" at line 195. The role *name* in the same row is correct. | `B/MicroLIMS.Application/Services/UserService.cs:177-178, 195` |
| H5 | **High (access control)** | Permission changes don't reach signed-in users until their access token expires. Moving a user between two roles with the **same base `RoleType`** isn't detected either. `AccessTokenRevalidator` only compares the role *Type* claim, not the role id or the permission set. | `B/MicroLIMS.API/Authorization/AccessTokenRevalidator.cs:43-65`; permissions are baked into the JWT: `B/MicroLIMS.API/Authorization/PermissionAuthorizationHandler.cs:5-16`; access token lifetime 15 min default, up to 480: `B/MicroLIMS.API/Extensions/JwtConfiguration.cs:36-37, 68-78` |
| H6 | Medium | Hard delete of users **already exists** (the brief asked to "confirm absent"). It is history-guarded, audited and blocks deleting the last admin. Details in §2. | `B/MicroLIMS.API/Controllers/UserController.cs:136-152`; `B/MicroLIMS.Application/Services/UserDeletionService.cs:57-106` |
| H7 | Medium | Security Audit Trail events are recorded, but **no endpoint or screen reads them**. The `System.ViewSecurityAudit` permission exists but isn't enforced anywhere. | `B/MicroLIMS.Shared/Constants/PermissionConstants.cs:42` (not in `Enforced`, lines 147-159); no reader in `B/MicroLIMS.API/Controllers/` |
| H8 | Low (bug) | Editing a user's profile from the Users page **clears their Job Title**. The frontend sends no `jobTitle`, the request defaults it to `null`, and the service assigns it unconditionally. | `F/modules/users/services/UserService.ts:34-35`; `B/MicroLIMS.API/Controllers/UserController.cs:12`; `B/MicroLIMS.Application/Services/UserService.cs:146` |

### 0.1 Verification pass (2026-10-26) — errata and new findings

Five independent read-only agents re-checked this report. Two checked every file:line citation (~257 citations; 2 errors, both fixed inline above), one inventoried tooling, and two re-derived the security/audit and access-control findings. Results:

**New findings (not in the original report):**

| # | Severity | Finding | Evidence |
|---|---|---|---|
| N1 | **High (live defect, likely)** | **Recording a Biological Waste Destruction or a Maintenance Log Entry probably fails on PostgreSQL.** Both services insert the e-signature with `EntityId = 0`, then set the real id and save again. That second save is an UPDATE on `ElectronicSignatures`, which `trg_electronicsignatures_immutable` rejects. In-memory tests can't catch this, and no PostgreSQL test covers these paths. The two plan-approval variants assign an already-known id, so they should be no-ops. The maintenance path also sets `ElectronicSignatureId = signature.Id` before the signature is saved (`EquipmentWorkService.cs:391`); on Npgsql that reads `0`, so the FK insert likely fails first. | `B/MicroLIMS.Application/Services/BiologicalWasteDestructionService.cs:192-222`; `B/MicroLIMS.Application/Services/EquipmentPlan/EquipmentWorkService.cs:372-399`; trigger `20260902181230_AddAuditImmutabilityTriggers.cs:30-32` (never relaxed later) |
| N2 | High | H3 is wider than stated. `AdminPasswordRecovery.CodeHash` (unsalted SHA-256 of a ~60-bit, 15-minute code) and `InstrumentAgent.KeyHash` are also captured. `PasswordHistory` pruning writes old BCrypt hashes as `Delete` rows. Failed logins and password changes also write full `User` rows. Only the BCrypt hashes are realistically crackable; the SHA-256 token/key hashes are high-entropy. | `MicroLimsDbContext.cs:544-545`; `AuthenticationService.cs:106, 131, 317-323`; `AdminPasswordRecoveryService.cs:17-45, 223`; `InstrumentAgentService.cs:76-96, 150` |
| N3 | High (remediation hazard) | `RoleService.UpdatePermissionsAsync` can strip `Users.Manage`/`Roles.Manage` from the only role holding them, which locks everyone out of administration. Nothing stops a user editing **their own** role's permissions. | `RoleService.cs:129-171`; `PermissionService.cs:41-47` |
| N4 | Medium | The last-admin guard on hard delete counts by `RoleType`, so a custom role holding `Users.Manage` is invisible to it. | `UserDeletionService.cs:65-68` |
| N5 | Medium | The frontend hard-codes admin authority. Seven HPLC/ICP pages treat `role === "SystemAdministrator"` as an operator. Two test-preparation forms exempt admin from the assigned-analyst lock. Document detail lets admin create revisions and edit drafts. `useMyLabs` gives admin every lab. | `F/modules/hplcWorkspace/entry/HplcSampleEntryPage.tsx:50-52` (+6 HPLC/ICP files); `F/modules/testPreparation/ConfirmPreparationForm.tsx:30-32`, `TestPreparationForm.tsx:35`; `F/modules/documentControl/pages/DocumentDetailPage.tsx:206`; `F/hooks/useMyLabs.ts:48, 56` |
| N6 | Medium | More `RoleType.SystemAdministrator` shortcuts: lab approval/review queues on the dashboard; the system actor for auto-generated equipment-overdue records is "the lowest-id admin, or a user named admin/system". Slimming the admin role needs a real system actor first. | `DashboardNotificationService.cs:174, 188`; `EquipmentWorkService.cs:661` |
| N7 | Medium | `Media.Release` and `Cryovials.Approve` stack on the class-level `Media.Prepare` / `Cryovials.Manage` gates. Removing only the approve/release code leaves the base write access. | `MediaController.cs:24, 74`; `CryovialController.cs:23, 50, 76` |
| N8 | Medium (process) | **Frontend tests never run in CI.** The workflow has lint, tsc and build but no `npm test`. `AuthorizationMatrixTests` + `authorization-matrix.txt` (611 lines naming SystemAdministrator) is the main backend safety net for any grant change. | `.github/workflows/ci.yml:218-245`; `B/MicroLIMS.Tests/ArchitectureTests/AuthorizationMatrixTests.cs:103-116` |
| N9 | Low (unverified) | Tracker bypasses not yet traced: raw Npgsql in `DatabaseSequenceHelper.cs:44` and `DatabaseHealthMonitorWorker.cs:174, 338`; `ChangeTracker.Clear()` in DocumentFileService and the master-data workbook runner; `ExecuteUpdateAsync` on the `InstrumentAgents.LastSeenAt` heartbeat (non-GxP). | Audit deep-check agent |

**Corrections to this report:**
- **H1 / B8:** `Documents.Approve` is granted but **inert**: no endpoint checks it. Document approval authority comes from the `RoleType` gates in `DocumentApprovalService.cs:109, 416`.
- **H1 sizing:** `TestWorkflow.Execute` gates ~52 endpoints, `Hplc.Operate` 26, `MasterData.Manage` ~95. `Samples.Receive` gates only 1 endpoint; registration itself uses `Samples.ReceiveOwnLab` (`SampleController.cs:84`).
- **D13:** Of the later-protected tables, only `InstrumentIngestPayloads` and `DocumentAcknowledgementRecords` are fully append-only. `InstrumentIngestVersions` and `InstrumentIngests` allow limited updates. `WorkflowProfileVersions` blocks updates only once Effective.
- **D14 / D16:** The Document Control CSV export **is** itself audited (`DocumentAuditService.cs:43-45`). Only the main trail has no export.
- **D15:** No explicit `AuditLogs.Add` / `Record*EventAsync` site was found reading a temporary key. ~60 sites were read and all 87 event calls were scanned. The remaining exposure is historical rows only.
- **A3:** The registry-enforcement test exists (corrected inline in §2).

---

## 1. Summary table — all checklist items

Status: **E** = Exists, **P** = Partial, **M** = Missing.

| # | Item | Status | Location (backend / frontend) | Main gap vs target |
|---|---|---|---|---|
| A1 | User entity fields | **E** | `B/MicroLIMS.Domain/Entities/User.cs:5-25`; departments via `B/MicroLIMS.Domain/Entities/UserOrgMembership.cs:4-16` | No `DeactivatedAt/By`, no `LastFailedLoginAt`, no "account expiry / review due" date. |
| A2 | User operations | **E** | `B/MicroLIMS.API/Controllers/UserController.cs:38-152`; `B/MicroLIMS.Application/Services/UserService.cs:103-343`; `F/modules/users/UsersPage.tsx`, `F/modules/users/services/UserService.ts:22-57` | Hard delete **exists** (H6). No last-admin guard on demote/disable. Email edit has no explicit audit or reason. Job Title bug (H8). Role-change audit bug (H4). |
| A3 | FK map for User | **E** (curated registry) | `B/MicroLIMS.Application/Services/UserReferenceRegistry.cs:35-262` | 160+ columns are listed, but **most have no DB FK**, so they're enforced in the application only (see §2). |
| A4 | Sessions / refresh tokens; admin revoke | **P** | `B/MicroLIMS.Domain/Entities/RefreshToken.cs`; `AuthenticationService.cs:161-210, 342-403` | Revocation happens only as a side effect (disable, lockout, password change, logout). There's **no "revoke sessions" endpoint**, no list of active sessions, and no cleanup of expired tokens. |
| A5 | Last-login / access history | **P** | `User.LastLoginAt` (`User.cs:25`); `LoginHistory` written at `AuthenticationService.cs:405-416`; read at `GET /api/admin/audit/login-history` (`B/MicroLIMS.API/Controllers/AuditController.cs:51-53`) | Data exists, but there's **no UI** (DOCUMENTATION_GAPS.md:274 confirms) and **no access-review report**. Max 100 rows by default, no date filter. |
| B6 | Roles UI, permission matrix, codes | **E** | `B/MicroLIMS.API/Controllers/RoleController.cs:19-70`; `B/MicroLIMS.Application/Services/RoleService.cs`; `F/modules/roles/RolesPage.tsx`, `RoleDetailPage.tsx`, `components/PermissionMatrix.tsx` | There are **57 codes, not 18** (47 enforced, 10 declared only) — see §3. |
| B7 | Admin-specific permissions | **E** | `Users.Manage`, `Roles.Manage`, `Audit.View`, `Reporting.Admin`, `System.ViewErrorLog`, `System.ViewSecurityAudit`, `Documents.ConfigManage`, `MasterData.Manage` (`PermissionConstants.cs:10-13, 37, 42, 77, 29`) | Every one of these is held by System Administrator. `Audit.View` and `MasterData.Manage` are also held by Section Head. No `System.Configure`, `System.ViewHealth` or `Sessions.Revoke` codes exist. |
| B8 | Segregation of duties (role level) | **M** | Seed: `DbSeeder.cs:528-588`; per-record SoD only: `SampleApprovalService.cs:162-170`, `CryovialService.cs:151-152`, `MediaReleaseService.cs:81-82` | **No role-level SoD.** Admin = all lab permissions (H1). See §4. |
| B9 | Role-rename propagation | **P** (by design + display defects) | JWT carries `Role.Type` (`AuthenticationService.cs:143, 208`); `/me` returns Type (`:293`); header maps Type → hard-coded label (`F/components/Header.tsx:73-76`, `F/utils/userDisplay.ts:11-17`) | The role display in the header and session ignores `Role.Name`. Details in §5. |
| C10 | Password / lockout / session / token config | **P** (mostly hard-coded) | `B/MicroLIMS.Shared/Validation/PasswordPolicy.cs:8-24`; `AuthenticationService.cs:20-24`; `JwtConfiguration.cs:36-37, 68`; `ElectronicSignatureService.cs:17-18` | Only the access-token lifetime can be configured (appsettings/env). Everything else is a `const`. Nothing is DB-editable. No password expiry or idle timeout. |
| C11 | E-signature | **E** | `B/MicroLIMS.Application/Services/ElectronicSignatureService.cs:38-97`; entity `B/MicroLIMS.Domain/Entities/ElectronicSignature.cs`; meanings `B/MicroLIMS.Domain/Enums/SignatureMeaning.cs` | `RoleSnapshot` stores `Role.Type`, not the role's display name (`:89`). Signature failures are throttled but never lock the account (deliberate, `:62-70`). |
| D12 | Audit schema & writers | **E** | `B/MicroLIMS.Domain/Entities/AuditLog.cs:10-43`; config `B/MicroLIMS.Persistence/Configurations/AuditLogConfiguration.cs`; automatic capture `MicroLimsDbContext.cs:461-576`; semantic events `B/MicroLIMS.Application/Services/AuditEventService.cs` | Captures every entity except a 10-type exclusion list (`MicroLimsDbContext.cs:501-510`). **Serializes secrets** (H3). Actor falls back to `0` when no user (`:563`). |
| D13 | DB-level protection | **P** | Trigger: `B/MicroLIMS.Persistence/Migrations/20260902181230_AddAuditImmutabilityTriggers.cs:15-32` (AuditLogs, AuditEventChanges, ElectronicSignatures) | Row triggers block UPDATE/DELETE but **not TRUNCATE**, and the table owner can disable them. **`SecurityAuditEvents` and `LoginHistories` have no trigger.** `SecurityAuditEvents` *can't* have one as built, because its FKs are `SET NULL` (`UserReferenceRegistry.cs:124-125`). |
| D14 | Audit viewer | **P** | `GET/POST /api/admin/audit/*` (`AuditController.cs:24-53`); `F/modules/auditSearch/AuditSearchPage.tsx` | Client-side paging over a hard-coded `Take: 300` (`AuditSearchPage.tsx:47`). Server `Take` isn't capped (`AuditSearchService.cs:22, 72`). **No export** (only Document Control has CSV export, max 5,000 rows). No security-event viewer. |
| D15 | EntityId negative-key bug | **P** — fixed going forward, **no backfill found** | Two-phase capture: `MicroLimsDbContext.cs:451-490` | No migration or script rewrites the old rows, and the trigger forbids UPDATE anyway. How many rows are affected in prod is unknown (open question Q7). |
| D16 | Admin changes audited (old / new / reason) | **P** | User ops: `UserService.cs:148-157, 184-199, 232-248, 278-292, 308-317, 330-339`; role permissions: `RoleService.cs:158-168`; role rename/create/delete: generic capture only (`RoleService.cs:73, 102, 123`) | Reason is **required** only for role change and disable. It's optional for unlock and password reset, and **absent** for create user, edit profile, email, force-password-change, role rename/create/delete, permission edits and hard delete. Config settings use an auto-generated reason (`DocumentConfigurationService.cs:572`). |
| E17 | Error Log / Incidents | **E** | `B/MicroLIMS.Domain/Entities/Incident.cs`, `ErrorLog.cs`; `B/MicroLIMS.API/Controllers/ErrorMonitoringController.cs:19-86`; `B/MicroLIMS.Application/Services/CriticalAlertService.cs:56-83`; `F/modules/errorMonitoring/ErrorMonitoringPage.tsx` | Critical email is **off by default**, and its single recipient is set in appsettings (`appsettings.json:55-59`). |
| E18 | Background services | **P** | 7 hosted services, `B/MicroLIMS.API/Extensions/ServiceCollectionExtensions.cs:157-163` | Run status shows up **only in logs**. No heartbeat, last-run or last-error table, and no token-cleanup worker. |
| E19 | Health / DB size / backups | **P** | `/health`, `/health/ready` (`B/MicroLIMS.API/Extensions/HealthCheckExtensions.cs:43-73`); `DatabaseHealthMonitorWorker` (slow / blocked queries) | No DB-size reporting. Backups are a shell script (`scripts/DatabaseBackup/backup.sh`) with **no status visible in the app**. |
| F20 | Configuration inventory | **P** | See §6 | Spread across `const`, appsettings and 6+ DB tables. No single registry. |
| F21 | Config editable in UI; versioned / audited | **P** | Document Control settings (`DocumentConfigurationService.cs:503-586`), KPI settings (`B/MicroLIMS.Application/Services/Kpi/KpiSettingsService.cs`), Monitoring settings (`…/Monitoring/MonitoringSettingsService.cs`), Workflow profiles (signed, append-only versions) | All are audited (generic or semantic). Only workflow profiles are **versioned with a signature**. No user-entered reason for change on any of them. |
| G22 | Admin navigation & guards | **E** | `F/routes/menuConfig.ts:87, 90, 105-106, 224-226, 263-279`; `F/routes/AppRoutes.tsx:216-284`; `F/routes/PermissionRoute.tsx` | Admin pages are spread over 5 sidebar groups. Guards are permission-based in both UI and API. The Document audit link is gated by role, not permission (`menuConfig.ts:108-113`). |

---

## 2. Users & access — details

### A1. User entity (`B/MicroLIMS.Domain/Entities/User.cs`)

| Field | Line | Notes |
|---|---|---|
| `IsActive` | 14 | Status. Deactivation = `IsActive=false`. |
| `FailedLoginAttempts`, `LockedUntil`, `IsLocked` (computed) | 18-20 | Lockout is time-based (15 min), so it unlocks itself. |
| `PasswordChangedAt`, `MustChangePassword` | 23-24 | No expiry policy reads `PasswordChangedAt`. |
| `LastLoginAt` | 25 | Set on successful login (`AuthenticationService.cs:138`). |
| `CreatedAt` | 15 | No `CreatedByUserId`. |
| Department links | — | Not on `User`. Lives in `UserOrgMembership` (`DepartmentId`, `SectionId?`, `PhyschemArea?`) and is managed via `PUT /api/org/users/{id}/memberships` (`B/MicroLIMS.API/Controllers/LaboratoryOrganizationController.cs:35-41`, `Users.Manage`), UI `F/modules/users/dialogs/UserSectionsDialog.tsx`. System Administrators skip section scoping entirely (`B/MicroLIMS.Application/Services/UserSectionScopeService.cs:26-29`). |

### A2. Existing operations (`UserController` → `UserService`), all under `Users.Manage`

| Operation | Endpoint | Service | Audit | Reason |
|---|---|---|---|---|
| List / get | `GET /api/users`, `/{id}` | `:59, :96` | — | — |
| Create | `POST /api/users` | `:103-126` (forces `MustChangePassword`) | Generic `Create` row only (includes PasswordHash, H3) | none |
| Edit profile | `PUT /{id}` | `:128-161` (version-checked) | `USER_DATA_UPDATED` | none |
| Change role | `PUT /{id}/role` | `:163-204` (blocks self) | `USER_ROLE_CHANGED` + security event | **required** |
| Enable/disable | `PUT /{id}/status`; legacy `PUT /{id}/deactivate` | `:206-252` (blocks self-disable, revokes sessions) | `USER_ENABLED/DISABLED` + security event | required to disable |
| Edit email | `PUT /{id}/email` | `:259-265` | Generic capture only | none |
| Unlock | `PUT /{id}/unlock` | `:267-296` | `USER_UNLOCKED` + security event | optional |
| Password reset (email token) | `POST /{id}/password-reset` | `:298-320` | `ADMIN_PASSWORD_RESET_REQUESTED` | optional |
| Admin password recovery | `POST /{id}/admin-password-recovery` | `AdminPasswordRecoveryService` | (service-level) | **required** |
| Force password change | `PUT /{id}/force-password-change` | `:322-343` | `FORCE_PASSWORD_CHANGE_SET` | none |
| **Hard delete** | `DELETE /{id}` | `UserDeletionService.cs:57-106` | `USER_HARD_DELETED` with snapshot | none |

Gaps:
- **No last-admin guard on `ChangeRoleAsync` or `SetStatusAsync`.** Admin A can demote or disable admin B, and B can do the same to A, leaving nobody. Hard delete does have the guard (`UserDeletionService.cs:65-72`).
- `DeactivateAsync` (`UserService.cs:254-257`) records the target as their own actor (`actingUserId = userId`). It's only a legacy service method, but if anything calls it the attribution is wrong.

### A3 / Deliverable 2. FK map for `User`

`UserReferenceRegistry.All` (`B/MicroLIMS.Application/Services/UserReferenceRegistry.cs:35-262`) is a hand-reviewed list of every column that references `User.Id`. A reflection test enforces it: `UserReferenceRegistry_AccountsForEveryUserIdNamedPropertyInTheModel` and `…_EveryEntryMatchesARealIntProperty` in `B/MicroLIMS.Tests/UnitTests/UserDeletionTests.cs:247-271`. The registry comment at `:25-28` gives the test a name (`UserReferenceModelScanTests`) that doesn't exist. Grouped by how they're enforced:

**Group A — real DB FK, `Restrict` (blocks delete). Lines 38-110, plus 238-239, 250, 260.**
`AdminPasswordRecovery.{UserId, CreatedByUserId}`, `ElectronicSignature.UserId`, `EquipmentDocument.UploadedByUserId`, `EquipmentStatusHistory.ChangedByUserId`, `ItemDocument.UploadedByUserId`, `ItemDocumentAccessLog.UserId`, `LocationPathogenObservation.ObservedByUserId`, `MaterialDocument.UploadedByUserId`, `OosInvestigationDocument.UploadedByUserId`, `Sample.{ReceivedByUserId, ReviewedByUserId, ApprovedByUserId}`, `SampleSectionSignoff.{ReviewedByUserId, ApprovedByUserId, ClosedByUserId}`, `TestReturnEvent.{ReviewerUserId, AssignedAnalystId}`, `Discussion*` / `Conversation*` / `DirectMessage` authorship (7 columns), the whole Document Control family (`DocumentMaster`, `DocumentMasterAssignment`, `DocumentRevision`, `RevisionFile`, `DocumentNumberingConfiguration`, `ConfigurationSetting`, `DocumentReviewTask/Finding`, `RevisionChangeItem`, `RevisionImpactAssessment`, `DocumentApprovalTask`, `PeriodicReviewTask/Finding`, `DocumentTrainingAssignment/Configuration`, `DocumentRoleCurriculum`, `DocumentAcknowledgementRecord`, `DocumentEscalationRecord` — 33 columns), `MasterDataImport.{ImportedByUserId, VerifiedByUserId}`, `AnalystAvailability.UserId`, `CalendarEventDocument.UploadedByUserId`.

**Excluded — FK `Cascade` (deleted with the user). Lines 113-116.**
`PasswordHistory.UserId`, `PasswordResetToken.UserId`, `RefreshToken.UserId`, `UserOrgMembership.UserId`.

**Excluded — FK `SetNull` (user reference cleared, row kept). Lines 122-125.**
`ErrorLog.UserId`, `Incident.ResolvedByUserId`, `SecurityAuditEvent.{ActorUserId, TargetUserId}`.

**Group B — no DB FK, enforced in the application only. Lines 128-261.**
- Traceability: `AuditLog.UserId`, `LoginHistory.UserId`, `DataExportLog.UserId`, `NotificationLog.UserId`, `ArchivedRecord.GeneratedByUserId`.
- Result provenance: `Result.EnteredByUserId`, `ResultRecord.{ResultEnteredByUserId, ApprovedByUserId}`, `CountTestReading`, `ConfirmatoryPlateObservation`, `PathogenObservation`, `TestAnalysis`, `TestOrder.AssignedAnalystId`, `WorkflowHistory`, `WorkflowStepResult` (×3), `ReviewWorkflowEvent`.
- Lab records: `Media`, `MediaUsage`, `MediaEvaluation*`, `MediaStorageEntry`, `Cryovial` (×2), `ThawEvent`, `Incubation` (×3), `SamplePreparation`, `SampleLocation`, `BiologicalWasteDestruction`, `ItemPreparationConfiguration` (×2).
- Inventory and documents: `EquipmentInventory`, `Material`, `*Document.{Superseded,Voided}ByUserId`, `*DocumentAccessLog`, `AutoclaveProgram*`, `IncubatorSetPointHistory`.
- HPLC/ICP chain: `ChromatographyColumn`, `MaterialMasterEntry`, `SolutionMaster`, `HplcMethod`, `IcpMethod`, `SolutionPreparation*`, `TitrantStandardization`, `HplcRun*`, `HplcSstRecord`, `HplcEvidence`, `IcpRun*`, `IcpCalibration`, `IcpCcvReading`, `IcpEvidence`, `WorkingStandardQualification` (×5), `WorkingStandardDocument`, `InstrumentAgent` (×2), `InstrumentIngestVersion`.
- Planning: `WorkloadWeight*`, `WorkflowProfileVersion`, `KpiSetting`, `MonitoringSettings`, `MonitoringPlanRevision`, `MonitoringOccurrenceEvent`, `MonitoringDeviation`, `AnalystAvailability.EnteredByUserId`, `EquipmentPlanRevision`, `EquipmentTaskEvent`, `MaintenanceLogEntry` (×2), `CalendarEvent` (×2), `CalendarEventDocument.VoidedByUserId`.

**How hard delete decides today** (`UserDeletionService.cs:37-55`): if `LastLoginAt` is not null → blocked; if any **Blocks** entry matches → blocked; otherwise the user row is deleted and the cascade/set-null rows follow.

**Recommendation — which users are safely hard-deletable:**
1. **Deletable:** only accounts created in error that **never signed in** (`LastLoginAt IS NULL`) and have **zero** Blocks-references. That's exactly what the current guard enforces. Keep it. Deactivation stays the normal off-boarding path.
2. **Never deletable:** anyone with an `ElectronicSignature`, `AuditLog`, `LoginHistory` or any Group A/B row. For these, "delete" must mean **deactivate**, and the person's identity must stay resolvable for audit reading (§11.10(e), Annex 11 §9).
3. Tighten before exposing hard delete in the consolidated panel:
   - Add a **required reason** and an **e-signature** (Part 11 §11.200 re-authentication) to `DELETE /api/users/{id}`.
   - Note that `AuditLog.UserId`, `LoginHistory.UserId` and the other Group B columns have **no DB FK**, so a direct DB delete would orphan them without error. DB access controls are the backstop (see D13).
   - **H3 interaction:** a hard-deleted user's PasswordHash already sits in `AuditLogs` (from creation). If H3 is fixed, decide how to handle historical rows (see Q4).
   - **SetNull on `SecurityAuditEvents`** means deleting a user rewrites rows in the security trail. That's acceptable only if the trail is formally declared non-immutable (Q5).

### A4. Sessions / refresh tokens

- Refresh tokens: random 32-byte values, SHA-256 hashed at rest, 7-day lifetime, rotated on every refresh (`AuthenticationService.cs:23, 199-205, 392-403`). Refresh refuses disabled or locked accounts (`:185-195`).
- Access tokens are re-checked on every request against active / locked / role-type / password-changed (`AccessTokenRevalidator.cs:43-72`).
- Revocation triggers: disable (`UserService.cs:226-227`), automatic lockout (`AuthenticationService.cs:120`), password change/reset (`:329`), logout (`:342-375`).
- **Gaps:**
  - No admin endpoint to revoke a user's sessions without disabling the account.
  - No view of active sessions (`RefreshTokens` has no IP / user-agent / last-used columns to show).
  - Expired and revoked `RefreshTokens` and `PasswordResetTokens` are **never purged** — nothing removes them anywhere in `B/MicroLIMS.Application` or `B/MicroLIMS.API`.
  - **Unlock doesn't need to revoke** (lockout already did), which is fine. But role and **permission** changes don't force re-authentication (H5).

### A5. Access history

`LoginHistory` rows (user id, username, success, failure reason, IP) are written for every attempt, including unknown usernames (`AuthenticationService.cs:85, 100, 132, 140, 405-416`). Read via `GET /api/admin/audit/login-history?username=&take=100` (`AuditController.cs:51-53`) under `Audit.View`. **No frontend caller.** The Security Audit Trail (`SecurityAuditEvent`, event codes in `B/MicroLIMS.Shared/Constants/SecurityEventCodes.cs`) also covers role changes, enable/disable, unlock, logout and session revocation, but **has no reader**. Together they're enough for an access-review report (user × role × permissions × last login × status changes) once a query and screen exist.

---

## 3. Roles & permissions

### B6. Roles UI and permission codes

- API: `B/MicroLIMS.API/Controllers/RoleController.cs:19-70` (`Roles.Manage`), covering list, get, create (with a base `RoleType`), rename/describe (version-checked), delete (non-system and unassigned only, `RoleService.cs:108-124`), `PUT /{id}/permissions`, and `GET /api/permissions`.
- UI: `F/modules/roles/RolesPage.tsx`, `CreateRolePage.tsx`, `RoleDetailPage.tsx`, with the matrix in `F/modules/roles/components/PermissionMatrix.tsx` (shows an "Enforced" / "Legacy-only" badge, `:71-78`).
- A new role **copies the permissions of its base system role** (`RoleService.cs:75-86`). A new role based on `SystemAdministrator` therefore starts with every lab permission.
- **The brief's "18 permission codes" doesn't match the code.** `PermissionConstants.All` has **57** codes (`B/MicroLIMS.Shared/Constants/PermissionConstants.cs:121-142`). 47 are enforced (`:147-159`).

| Area | Codes |
|---|---|
| Administration | `Users.Manage`, `Roles.Manage`, `Audit.View`, `Reporting.Admin`, `System.ViewErrorLog`, `System.ViewSecurityAudit`*, `MasterData.Manage`, `Documents.ConfigManage` |
| Samples | `Samples.Receive`, `Samples.ReceiveOwnLab`, `Samples.TrackAll`, `Samples.AssignAnalyst`, `Samples.Review`, `Samples.Approve`, `Samples.Correct` |
| Test execution | `TestWorkflow.Execute`, `TestWorkflow.BiochemicalDecision`, `TestWorkflow.Supervise`, `Solutions.Prepare`, `Hplc.Operate`, `WorkingStandards.Qualify` |
| Media / cryovials | `Media.Prepare`, `Media.Release`, `Cryovials.Manage`, `Cryovials.Approve` |
| Inventory / items | `Materials.Manage`, `Materials.DocumentControl`, `Equipment.Manage`, `Equipment.DocumentControl`, `Items.Manage`, `Items.DocumentUpload` |
| Quality | `Signatures.Manage`, `Oos.Manage` |
| Plans / equipment work | `MonitoringPlan.View`, `MonitoringPlan.Manage`, `MonitoringPlan.Approve`, `EquipmentPlan.Manage`, `EquipmentPlan.Approve`, `EquipmentWork.Record`, `CalendarEvent.Manage` |
| Dashboards / KPI | `Dashboards.LabOverview`, `Dashboards.Review`, `Kpi.View`, `Kpi.ViewTeam`, `Kpi.ConfigureProfiles` |
| Document Control | `Documents.Register`, `Documents.DraftEdit`*, `Documents.RevisionCreate`*, `Documents.Review`*, `Documents.Approve`*, `Documents.PeriodicReview`*, `Documents.TrainingAssign`, `Documents.TrainingViewMatrix`* |
| Collaboration | `Discussions.View`*, `Discussions.Create`*, `Discussions.EditAny`, `Messages.Use`* |

\* = declared but not in `Enforced`.

Note: `Permission.IsEnforced` is written to the DB **only when the row is first inserted** (`DbSeeder.cs:511-520`), so the UI badge can go stale when a code later becomes enforced.

### B7. Admin-specific permissions — who holds them (seed defaults)

| Code | SysAdmin | Section Head | Reviewer | Analyst |
|---|---|---|---|---|
| `Users.Manage`, `Roles.Manage`, `Reporting.Admin`, `System.ViewErrorLog`, `System.ViewSecurityAudit`, `Documents.ConfigManage` | ✔ | — | — | — |
| `Audit.View`, `MasterData.Manage` | ✔ | ✔ | — | — |

Source: `DbSeeder.cs:528-588`. Production may differ because grants are editable (Q1).

Some admin behaviour is still decided by **`RoleType`, not by permission**, and is invisible on the Roles screen:
- `UserSectionScopeService.cs:26` — admins see every section.
- `DocumentAuthorizationService.cs:100-249` — admin passes 9 document rules.
- `DocumentReviewService.cs:292, 433, 493` and `DocumentApprovalService.cs:109, 416` — admin can act on review/approval tasks not assigned to them.
- `PeriodicReviewService.cs:563`, `ItemStmService.cs:80`, `DocumentFileService.cs:325`.
- `DocumentConfigurationService.cs:31-36` — config requires the admin *role*.

### B8 / Deliverable 3. Segregation of duties — ⚠ COMPLIANCE RISK

**Finding: unrestricted.** A single role can hold admin and lab permissions together, and the built-in System Administrator role does by default.

- **Seed:** `SystemAdministrator → PermissionConstants.All` (`DbSeeder.cs:530`). That includes `Samples.Review`, `Samples.Approve` (final release), `Samples.Correct` (correct/void sample records), `TestWorkflow.Execute` / `BiochemicalDecision` / `Supervise` (result-producing steps and overriding incubation minimums), `Hplc.Operate`, `Solutions.Prepare`, `WorkingStandards.Qualify`, `Media.Release`, `Cryovials.Approve`, `MonitoringPlan.Approve`, `EquipmentPlan.Approve`, `EquipmentWork.Record`, `Kpi.ConfigureProfiles`, `Documents.Approve` and `Discussions.EditAny`.
- **Migrations keep adding to it.** Every lab-permission migration includes `Type = 0`, for example `20260929184541_AddHplcWorkspace.cs:424-427` and `20260929173324_AddSolutionPreparation.cs:184-187`. Deployed databases therefore match the seed.
- **Nothing on the save path checks combinations.** `RoleService.UpdatePermissionsCoreAsync` (`:129-171`) accepts any set of valid codes. `UserService.ChangeRoleAsync` (`:163-204`) has no check either.
- **What does exist** is per-record SoD — you can't approve a sample you reviewed or tested (`SampleApprovalService.cs:162-170`), a cryovial you prepared (`CryovialService.cs:151-152`), or media you prepared or evaluated (`MediaReleaseService.cs:81-82`). Document Control enforces author ≠ reviewer ≠ approver as a fixed rule (`PermissionConstants.cs:56-57`). These stop a person approving their *own* work. They **don't** stop an admin account from reviewing or releasing *someone else's* sample.
- **Aggravating factors:**
  - Admins skip section scoping (`UserSectionScopeService.cs:26-29`), so an admin can act on every lab's samples.
  - An admin can grant themselves any permission and re-sign in (only self-*role*-change is blocked, `UserService.cs:165-166`; editing your own role's permissions isn't blocked).
  - Admins can edit and hard-delete users and change roles, which together allow "create an account, grant, act, disable" sequences. These are audited, but they aren't prevented.

**Regulatory read:** Part 11 §11.10(d) and (g) (limited system access, authority checks) and Annex 11 §12.1 / PIC/S PI 041 §9 expect administrative (system) privileges to be **separate** from GxP data privileges. The current default isn't defensible at inspection.

**Recommendation (for the build phases):**
- (a) Remove lab and approval permissions from the System Administrator seed and migrations, with a data migration for existing DBs.
- (b) Add a server-side **SoD rule set** (for example, `{Users.Manage | Roles.Manage}` can't be combined with `{Samples.Review | Samples.Approve | Samples.Correct | TestWorkflow.* | *.Approve | *.Release}`), enforced in `RoleService` and shown in the matrix.
- (c) Move the `RoleType.SystemAdministrator` shortcuts in Document Control and section scope to explicit permissions, or remove them.
- (d) Block editing the permissions of a role you hold yourself.

### B9. Role-rename bug — trace

`Role.Name` is read **live** wherever a list shows it:
- Users page (`UserService.cs:352`), directory (`:79`)
- Messages (`MessageService.cs:106-377`), discussions (`DiscussionService.cs:204-555`)
- Training matrix (`TrainingMatrixService.cs:221, 369`), assignable analysts (`SampleAssignmentService.cs:138`)
- Audit search "User role" (`AuditSearchService.cs:109`)

Those pick up a rename on the next fetch. The places that **ignore** `Role.Name` (or freeze it):

| Where | What it stores / shows | Effect after renaming a role |
|---|---|---|
| JWT `role` claim — `AuthenticationService.cs:143, 208`; `JwtTokenService.cs:37` | `Role.Type` enum name (e.g. `SectionHead`) | Never reflects the name. |
| `/api/auth/me` → `CurrentUserInfo.Role` — `AuthenticationService.cs:293`; `AuthenticationController.cs:131` | `Role.Type` | Same. |
| Frontend session — `F/contexts/AuthContext.tsx:57, 97, 111` (`localStorage.microlims_role`) | `Role.Type` | Same. |
| Header / profile title — `F/components/Header.tsx:73-76, 86`; `F/utils/userDisplay.ts:11-17` | **Hard-coded label** from Type ("System Administrator", "Quality Reviewer", …) | **The rename never shows in the header.** A custom role also shows its *base* type's label. This is most likely the reported symptom. |
| Audit enum labels — `F/modules/auditSearch/utils/auditEnumLabels.ts:43` | Hard-coded 4 names | Same. |
| E-signature `RoleSnapshot` — `ElectronicSignatureService.cs:89` | `Role.Type` at signing | Signed records show the base type, not the role name the user actually held. Freezing a snapshot is correct (11.50), but it should capture the **name** (and arguably id + type). |
| Audit search "User role" — `AuditSearchService.cs:109` | **Current** `Role.Name` | The opposite problem: renames **rewrite history in the viewer**. A 2025 action shows the role name as of today. |
| `AccessTokenRevalidator.cs:63-65` | Compares Type only | Moving a user between two roles with the same base type isn't detected (H5). |

**Root cause:** `RoleType` (4 legacy buckets) is still the identity carried in the token and UI, and the custom-role model only lives in `RolePermission`. See the comment at `RoleService.cs:52-62`. Fix direction: carry `roleId` (and name) in the token and `/me`, display the name, revalidate on `roleId` plus a role "permission version" stamp, and snapshot the name in signatures and audit rows. Needs a decision (Q3) because it touches the JWT contract.

---

## 4. Security policy

### C10. Where values live

| Setting | Value | Where | Kind |
|---|---|---|---|
| Password min length / complexity | 8; upper, lower, digit, special | `B/MicroLIMS.Shared/Validation/PasswordPolicy.cs:8, 15-24` | Hard-coded |
| Password history | last 5 | `AuthenticationService.cs:21` | Hard-coded |
| Password expiry | **none** | — | Missing |
| Lockout threshold / duration | 5 attempts / 15 min | `AuthenticationService.cs:20, 22` | Hard-coded |
| Signature failure throttle | 5 per 15 min (no lock) | `ElectronicSignatureService.cs:17-18` | Hard-coded |
| Access token lifetime | 15 min default, 1–480 allowed | `JwtConfiguration.cs:36-37, 68-78` (`Jwt:AccessTokenMinutes`) | appsettings / env |
| Refresh token lifetime | 7 days | `AuthenticationService.cs:23` | Hard-coded |
| Password reset token lifetime | 1 h | `AuthenticationService.cs:24` | Hard-coded |
| Idle / session timeout | **none** server-side. The refresh token sliding window is 7 days. | — | Missing |
| JWT key / issuer / audience | — | `appsettings.json:5-8` + `JwtConfiguration.Resolve` (startup fails without a key) | appsettings / env |

None of these are DB-editable, versioned or visible in the UI.

### C11. E-signatures

- **Re-authentication:** the password is re-verified at the moment of signing (`ElectronicSignatureService.cs:60-65`; the 11.200(a)(1) rationale is at `IElectronicSignatureService.cs:18`). Inactive and locked users are refused with the same generic message (`:62-65`).
- **Failures:** each one writes an `AuditLog` `SignatureFailed` row. After 5 in 15 minutes, attempts are refused without checking the password (`:40-58`). The login account is deliberately not locked (`:67-70`).
- **Meaning:** a `SignatureMeaning` enum with 27 values (`B/MicroLIMS.Domain/Enums/SignatureMeaning.cs`), stored on the row.
- **Linkage:** `EntityType` (string) + `EntityId` (int), plus name/username/role snapshots, IP, comment and `SignedAt` (`ElectronicSignature.cs:12-32`). The signature is queued on the same DbContext so it commits atomically with the state change (`IElectronicSignatureService.cs:16-24`). The row is immutable by DB trigger (`20260902181230_AddAuditImmutabilityTriggers.cs:30-32`).
- **Gaps:**
  - No hash or binding of the signed record's *content*. The link is by id only, so a later edit to the record isn't cryptographically detectable. Mitigated by audit capture and the record locks.
  - `RoleSnapshot` = Type (see B9).
  - Signature policy values are hard-coded.

---

## 5. Audit trail

### D12. Schema and writers

- **`AuditLog`** (`B/MicroLIMS.Domain/Entities/AuditLog.cs:10-43`):
  - Core fields: `EntityName`, `EntityId` (string ≤100), `Action` (≤50), `PreviousValue` / `NewValue` (full-row JSON), `UserId?`, `Timestamp`.
  - Search keys: batch, control, sample ref, media lot, strain, cryovial, `SampleId`, `TestOrderId`.
  - Semantic layer: `EventUid`, `ActorType`, `SystemProcessName`, `ActionCode`, `ActionCategory`, `Reason` (≤2000), `SourceContext`, `CorrelationId`, document ids.
  - Child table: `AuditEventChange` (per-field changes).
- **Writers:**
  1. **Automatic:** `MicroLimsDbContext.SaveChanges[Async]` override (`:461-490`) captures every Added/Modified/Deleted entity except the 10 excluded types (`:501-510`). It's two-phase so new rows get real ids.
  2. **Explicit:** services call `_db.AuditLogs.Add(...)` for semantic actions (UserService, RoleService, ElectronicSignatureService, …).
  3. **`AuditEventService`:** `RecordUserEventAsync` / `RecordSystemEventAsync`, using `EVT-nnnnnnn` UIDs.
- **Actor:** `CurrentUserId` is set by the audit middleware (`Program.cs:207-209`; `B/MicroLIMS.API/Middleware/AuditMiddleware.cs`). Instrument agents are recorded as System + agent code (`MicroLimsDbContext.cs:551-565`). Background workers fall back to `UserId = 0` (`:563`).
- **Coverage:** effectively all domain entities, except `AuditEventChange`, `Material/EquipmentDocumentAccessLog`, `Incident`, `ErrorLog`, `RefreshToken`, `NotificationLog`, `InstrumentIngestPayload` and `SecurityAuditEvent`.
- **Risk (H3):** credentials are captured. Also, changes made through `ExecuteUpdate`/`ExecuteDelete` or raw SQL bypass the tracker. I didn't inventory those call sites; that's a follow-up for the build phase.

### D13. DB-level protection

- `prevent_audit_modification()` raises on `BEFORE UPDATE OR DELETE … FOR EACH ROW` for `AuditLogs`, `AuditEventChanges` and `ElectronicSignatures` (`20260902181230_AddAuditImmutabilityTriggers.cs:15-32`). Later migrations add it to `DocumentAcknowledgementRecords`, the `InstrumentIngest*` tables and `WorkflowProfileVersions`.
- **Not protected:**
  - **TRUNCATE.** Row triggers don't fire on it. A `BEFORE TRUNCATE … FOR EACH STATEMENT` trigger or `REVOKE TRUNCATE` is needed.
  - **`ALTER TABLE … DISABLE TRIGGER` by the owner.** The app connects as `postgres` in the default config (`appsettings.json:3`), so the app role can disable the triggers.
  - **`SecurityAuditEvents` and `LoginHistories`** have no trigger.
- **Recommendation:** run the app under a non-owner role with `INSERT, SELECT` only on audit tables. Decision Q6.

### D14. Viewer / query

- Endpoints: `GET /api/admin/audit?entityName&entityId`, `POST /api/admin/audit/search`, the traceability graph, and login-history (`AuditController.cs:24-53`), all under `Audit.View`.
- Search filters: date range, batch, control, media lot, sample ref, strain, cryovial, sample id, test order id, user id, entity name, action (`AuditSearchService.cs:8-22, 58-70`).
- **No server paging or offset.** `Take` defaults to 200, has **no upper bound**, and is client-controlled (`:22, 72`). The UI requests 300 and pages in the browser (`F/modules/auditSearch/AuditSearchPage.tsx:33-47`).
- **No export**, and exports aren't logged for the main trail. Document Control has its own trail with CSV export (≤5,000 rows; `DocumentAuditService.cs:88-120` per `documentation/knowledge/reports.md:156`).
- No viewer for the Security Audit Trail (H7) or login history.

### D15. EntityId negative-key bug

- **Code: fixed.** Capture is two-phase, and new-row ids and values are read after the insert (`MicroLimsDbContext.cs:451-490`, comment `:454-460`). The hard-delete service comment still mentions "the Added-entity temp-key issue that affects Create audit rows elsewhere" (`UserDeletionService.cs:89-93`). That comment is stale relative to the DbContext, but it confirms the bug existed.
- **Backfill: none found.** No migration or script updates `AuditLogs.EntityId`, and the immutability trigger would reject an UPDATE anyway. Historical rows created before the fix probably still carry large negative `EntityId` values, and possibly negative FK values inside the `NewValue` JSON.
- Repo history starts at a squashed import (`2a4777a`, 2026-10-14), so the fix date can't be found from git.
- **Explicit `AuditLogs.Add` sites that read an id before `SaveChanges`** would still have the problem. I didn't check them all (follow-up).
- How many prod rows are affected is unknown (Q7).

### D16. Admin / configuration changes audited?

| Change | Old → new | Reason | Evidence |
|---|---|---|---|
| User profile edit | ✔ | ✘ | `UserService.cs:142-157` |
| User role change | ⚠ previous RoleId wrong (H4) | ✔ required | `UserService.cs:177-199` |
| User enable/disable | ✔ | disable: required | `UserService.cs:239-248` |
| Unlock | ✔ | optional | `UserService.cs:283-292` |
| Password reset / force change | new only / ✔ | optional / ✘ | `UserService.cs:308-317, 330-339` |
| Email edit | generic capture only | ✘ | `UserService.cs:259-265` |
| User create / hard delete | generic / snapshot | ✘ | `UserService.cs:121-122`; `UserDeletionService.cs:94-103` |
| Section memberships | generic capture (`UserOrgMembership` not excluded) | ✘ | `MicroLimsDbContext.cs:501-510` |
| Role create / rename / delete | generic capture | ✘ | `RoleService.cs:73, 102, 123` |
| Role permissions | ✔ consolidated old/new/granted/revoked | ✘ | `RoleService.cs:158-168` |
| Document Control settings | ✔ field change | auto-text only | `DocumentConfigurationService.cs:563-574` |
| KPI / Monitoring settings | ✔ semantic event (`KpiSettingsChanged`) | not verified | `KpiSettingsService.cs:16-24` |

Also note: role permission grants are saved row by row, and **the consolidated audit row is a separate `SaveChanges`** (`RoleService.cs:152-168`). It does sit inside `UnitOfWork.RunAsync`, so it should be atomic, but that's worth verifying.

---

## 6. System health

### E17. Error Log / Incidents

- **Entities:**
  - `Incident` (Guid id, correlation id, first/last seen, status, severity, summary, `AlertedAtUtc`, resolver, notes; `B/MicroLIMS.Domain/Entities/Incident.cs:13-46`).
  - `ErrorLog` rows grouped under incidents.
  - Both are excluded from GxP audit and are prunable.
- **Capture:** backend exceptions via `ExceptionMiddleware`, plus anonymous client crash reports via `POST /api/system/client-errors`. That endpoint is rate limited and size-capped (`B/MicroLIMS.API/Controllers/SystemController.cs:44-113`).
- **API:** under `System.ViewErrorLog` — list/detail incidents, resolve, reopen, override severity, CSV export (`ErrorMonitoringController.cs:19-86`).
- **UI:** `F/modules/errorMonitoring/ErrorMonitoringPage.tsx` (filters, export).
- **Email on critical:** `CriticalAlertWorker` polls `CriticalAlertService` and sends one email per Critical incident not yet alerted (`CriticalAlertService.cs:56-83`). It's **disabled by default**, and the single recipient is in `ErrorMonitoring:CriticalAlerts` (`appsettings.json:55-59`). SMTP must be configured; otherwise there's only a startup warning (`Program.cs:112-122`).
- **Retention:** 180 / 365 days by severity, run by `ErrorLogRetentionWorker` (`appsettings.json:49-54`).

### E18. Background services (`ServiceCollectionExtensions.cs:157-163`)

| Worker | Cadence / config | Observable? |
|---|---|---|
| `DocumentEffectiveDateWorker` | 60 min, `DocumentControl:EffectiveDateWorkerIntervalMinutes` | Logs only |
| `DatabaseHealthMonitorWorker` | 60 s, slow 1 s / blocked 5 s thresholds, `ErrorMonitoring:DatabaseHealth:*` | Writes findings into Error Log (visible on the Error Monitoring page); its own liveness is logs only |
| `ErrorLogRetentionWorker` | 24 h, `ErrorMonitoring:Retention:*` | Logs only |
| `CriticalAlertWorker` | 60 s (min 15), off by default | Logs + `Incident.AlertedAtUtc` |
| `SolutionPreparationExpiryWorker` | 5 min, **hard-coded** (`:14`) | Logs only |
| `MonitoringDeviationWorker` | 60 min, `MonitoringDeviations:*` | Logs only |
| `EquipmentOverdueWorker` | 60 min, `EquipmentOverdue:*` | Logs only |

- **No token cleanup worker.**
- Client-side polling: notifications every 60 s (`F/components/Header.tsx:63`).
- **Gap:** no job registry, heartbeat or last-run/last-error status in the DB or API.

### E19. Health, DB size, backups

- `/health` is liveness. `/health/ready` checks PostgreSQL reachability and that the schema is current. Both are anonymous and return no secrets (`HealthCheckExtensions.cs:36-101`; `B/MicroLIMS.API/HealthChecks/DatabaseReadinessHealthCheck.cs`).
- **DB size:** no `pg_database_size` or similar query exists anywhere in the app.
- **Backups:** `scripts/DatabaseBackup/backup.sh` and `scripts/Restore/restore.sh` are operator-run. The app records nothing about backups.
- See **H2**: a prod dump is tracked in git.

---

## 7. System configuration (F20 / F21)

| Setting | Current location | Kind | UI-editable | Audited / versioned |
|---|---|---|---|---|
| Lab time zone | `appsettings.json:12-14` `Lab:TimeZoneId` (fallback "Africa/Cairo", `LabClock.cs:19`; generator fallback `ReferenceNumberGenerator.cs:31`) | appsettings | ✘ | ✘ |
| `System.TimeZone` = "UTC" | `ConfigurationSettings` row (`DbSeeder.cs:611-618`) | DB | via DC config page (DC group only?) | ✔ (no reason) |
| Sample reference format `{Cat}{MM}{YY}{seq:D3}`, monthly reset, category codes | `ReferenceNumberGenerator.cs:6-49` | Hard-coded | ✘ | n/a |
| Other code formats (`BWD-nnnnn`, `MD-yyyy-nnnnn`, `EVT-nnnnnnn`, `AGT-…`, `CCR-…`, `MP-…`) | `BiologicalWasteDestructionService.cs:39`, `MonitoringDeviationService.cs:213`, `AuditEventService.cs:36, 95`, `InstrumentAgentService.cs:64`, `ControlChartReportService.cs:93`, `SolutionPreparationCode.cs:19` | Hard-coded | ✘ | n/a |
| Document numbering (prefix / format / enabled) | `DocumentNumberingConfiguration` | DB | ✔ DC Configuration (`Documents.ConfigManage` + admin role) | ✔ |
| Document Control settings (download/print permitted, watermark) | `ConfigurationSettings` (`DbSeeder.cs:619-642`) | DB | ✔ | ✔ field change, auto reason |
| Departments / sections (org model) | `DocumentDepartment` / `DocumentSection` + `UserOrgMembership` | DB | ✔ DC Configuration | generic |
| EM departments / rooms / water points etc. | `Department` (`B/MicroLIMS.Domain/Entities/Department.cs`) and master-data tables | DB | ✔ Lab Configuration (`MasterData.Manage`) | generic (+ `MasterDataChanged` signature meaning exists) |
| KPI capacity (hours/day, working days, horizon) | `KpiSetting` per section | DB | ✔ | ✔ semantic event + version check |
| KPI / SLA targets (work units, stage targets) | `WorkflowProfileVersion` | DB, **signed and append-only** | ✔ (`Kpi.ConfigureProfiles`) | ✔ versioned |
| Monitoring plan tolerances, grace, deviation due days | `MonitoringSettings` (`B/MicroLIMS.Domain/Entities/MonitoringSettings.cs:9-35`) | DB (one row) | ✔ (`MonitoringPlan.Manage`) | ✔ |
| SMTP host/port/user/password/from/SSL | `appsettings.json:30-37` | appsettings / env | ✘ | ✘ |
| Critical alert enable / recipient / interval | `appsettings.json:55-59` | appsettings | ✘ | ✘ |
| Error retention, DB health thresholds | `appsettings.json:38-54` | appsettings | ✘ | ✘ |
| Client-error rate limit; agent auth-audit limits | `appsettings.json:45-48`; `RateLimiting:AgentAuthAudit:*` | appsettings | ✘ | ✘ |
| File storage provider / S3; material document max size | `appsettings.json:16-29` | appsettings / env | ✘ | ✘ |
| Worker intervals / enable flags | See E18 | appsettings (one hard-coded) | ✘ | ✘ |
| Security policy values | See C10 | Hard-coded (one in appsettings) | ✘ | ✘ |
| CORS origins, forwarded-header proxies | `Frontend:Origin`; `ForwardedHeaders:KnownNetworks` | appsettings / env | ✘ | ✘ |
| Instrument agents (edge keys) | `InstrumentAgent` (`B/MicroLIMS.API/Controllers/InstrumentAgentController.cs`) | DB | ✔ | ✔ (create/revoke provenance) |

**F21 summary:** DB-backed configuration exists piecemeal (Document Control, KPI, Monitoring, Workflow profiles, master data, instrument agents). All of it is audited. Only Workflow profiles are versioned with a signature. **None asks for a reason-for-change.** No generic, typed, validated settings registry exists. The `ConfigurationSettings` table is close to one, but it's Document Control-scoped (new keys are forced into group `"DocumentControl"`, `DocumentConfigurationService.cs:546`).

---

## 8. Frontend navigation & guards (G22)

| Page | Sidebar group | Route | Guard (UI) | Guard (API) |
|---|---|---|---|---|
| Users | ADMINISTRATION (`menuConfig.ts:105`) | `/users` (`AppRoutes.tsx:278-279`) | `Users.Manage` | `Users.Manage` |
| Roles | ADMINISTRATION (`:106`) | `/roles`, `/roles/new`, `/roles/:id` (`:281-284`) | `Roles.Manage` | `Roles.Manage` |
| Error Monitoring | SYSTEM (`:90`) | `/error-monitoring` (`:271-272`) | `System.ViewErrorLog` | same |
| Audit Search | AUDIT & COMPLIANCE (`:87`) | `/audit-search` (`:216-217`) | `Audit.View` | same |
| Document Control → Configuration | Document Control area (`:130`) | (`:275`) | `Documents.ConfigManage` | permission + admin role |
| Document Control → Audit Trail | Document Control area (`:129`) | — | **role** check (`:108-113`) | role rule |
| Microbiology / Physicochemical Configuration | lab areas (`:157-162, 204-209`) | `/laboratory-configuration/*` | `MasterData.Manage` (+ lab membership) | same |
| Items, Receiving Configuration, Master Data Workbook | GENERAL LABORATORY CONFIGURATION (`:224-226`) | — | `Items.Manage` / `MasterData.Manage` | same |
| Admin Password Recovery | public route | `/admin-recovery` (`AppRoutes.tsx:100`) | token-based | — |

**Guard mechanics:**
- The sidebar filters by permission (`menuConfig.ts:228-247`).
- Routes use `PermissionRoute` (`F/routes/PermissionRoute.tsx:11-13`), which redirects to `/dashboard`.
- Permissions come from the JWT claims cached in `localStorage` (`F/contexts/AuthContext.tsx:51-60`).
- Every endpoint also carries `[Authorize(Policy=…)]`, with a deny-by-default fallback (`B/MicroLIMS.API/Extensions/AuthorizationExtensions.cs:17-19`).
- One leftover legacy role attribute: `MediaMasterDataController.cs:52` `[Authorize(Roles = SectionHead)]`.

---

## 9. Deliverable 4 — Proposed admin panel information architecture

Single entry: **`/admin`**, a new sidebar group "ADMINISTRATION", visible if the user holds any `Admin.*` / `System.*` permission. Each tab is guarded by its own permission. The panel contains **no** lab-data actions.

```
/admin
├── Overview            (health summary, open critical incidents, locked accounts, jobs, last backup)
├── Identity & Access
│   ├── Users           ← moves from /users (UsersPage + UserSectionsDialog)
│   ├── Roles & Permissions ← moves from /roles (+ SoD rule display / violations)
│   ├── Sessions        (NEW: active refresh tokens per user, revoke)
│   └── Access Review   (NEW, Phase 3: user × role × perms × last login × status history, export, sign-off)
├── Security
│   ├── Security Policy (NEW: password / lockout / token / signature settings, DB-backed, audited)
│   └── Security Events (NEW: SecurityAuditEvent + LoginHistory viewer)
├── Audit
│   └── Audit Trail     ← moves from /audit-search (read-only; server paging; export logged)
│       (Document Control audit stays inside DC; linked from here)
├── System Health
│   ├── Error Log & Incidents ← moves from /error-monitoring
│   ├── Background Jobs (NEW, Phase 2)
│   └── Database & Backups (NEW, Phase 2)
├── Configuration       (NEW, Phase 2: typed settings registry; reason-for-change; versioned)
│   └── links out to: Document Control Configuration, KPI/Workflow Profiles, Monitoring Settings
└── Integrations        (Phase 3: instrument agents, SMTP status, AI agent/MCP controls)
    └── Maintenance & Announcements (Phase 3)
```

**Stays where it is (lab data / GMP master data, not "system"):**
- Microbiology and Physicochemical Configuration, Items, Receiving Configuration, Master Data Workbook (`MasterData.Manage`, owned by Section Heads).
- Document Control Configuration.
- KPI Workflow Profiles.
- Monitoring Plan settings.

The admin panel links to these but doesn't absorb them, so admins don't gain lab master-data authority through it. Whether the System Administrator should keep `MasterData.Manage` at all is decision Q2.

---

## 10. Deliverable 5 — Phased implementation plan

### Phase 1 — Consolidate and close the compliance gaps

Prerequisites (bug and risk fixes; each needs a gate decision):
1. **H2:** remove the prod dump from the repo and its history, and rotate any credentials present in it. This is operational and outside the code build.
2. **H3:** stop capturing secrets. Exclude `PasswordHash` and `TokenHash` properties, plus the `PasswordHistory` / `PasswordResetToken` / `AdminPasswordRecovery` secret columns, from `SerializeValues`. Decide how to handle historical rows (Q4).
3. **H1 / B8:** strip lab and approval permissions from System Administrator (data migration), and add the server-side SoD rule check in `RoleService` / `UserService`.
4. **H4:** fix `ChangeRoleAsync` previous-value capture.
5. **H8:** fix the Job Title wipe.
6. Add last-admin guards to role change and disable.

Build:
- `/admin` shell, new admin group, and route moves for Users, Roles and Error Log (redirect old paths).
- **Users:** required reason on every admin user action (create, edit, email, force change, unlock, reset, delete). E-signature on hard delete and role change (Q8). New **Revoke sessions** endpoint and button (reuses `RevokeAllRefreshTokensAsync`).
- **Roles:** show SoD violations; block editing your own role; propagate permission changes immediately (token revalidation on role id + permission stamp, H5).
- **Audit Viewer:** server-side paging, capped `Take`, more filters (action category, actor type, correlation id). CSV/PDF export, with the export itself audited. Read-only by construction. Security Events tab (enforce `System.ViewSecurityAudit`) and Login History.
- **Security Policy (read-only in Phase 1):** a page showing the current effective values from C10, with the source of each. Making them editable is Phase 2, via the config registry.
- **DB hardening:** a non-owner app DB role, `REVOKE UPDATE, DELETE, TRUNCATE` on audit tables, a statement-level TRUNCATE trigger, and an append-only trigger on `LoginHistories`. `SecurityAuditEvents` depends on Q5.

### Phase 2 — Configuration and operations

- **Settings registry:** `SystemSetting` (key, typed value, group, validation metadata, effective-from, version) plus an append-only `SystemSettingVersion`. Reason-for-change and e-signature are required for GxP-relevant keys. Every change is audited (`ActionCategory.Configuration`).
- Move into the registry: security policy values (C10), critical-alert recipient/enable, retention days, worker enable flags and intervals. Infrastructure secrets (JWT key, SMTP password, S3 keys, connection string) stay in env/appsettings and are shown masked, read-only.
- Fold Document Control `ConfigurationSettings` into the registry, or keep it and expose it read-only. Resolve the duplicate time zone (Q9).
- **Background jobs:** a `BackgroundJobRun` table (job, started, finished, outcome, error, items processed) written by each worker; an Overview tile and Jobs page; and a manual "run now" for safe jobs only (Q10). Add a token cleanup worker.
- **Database & backups:** DB size and top tables (`pg_database_size`, `pg_total_relation_size`); backup status from a `BackupRun` record written by `backup.sh` (or read from the backup target); last successful restore test.

### Phase 3 — Integrations, review and operations

- **Integrations:** instrument agents (move or link from `InstrumentAgentController`), SMTP test-send and status, storage provider status.
- **AI agent / MCP controls:** an inventory of AI/agent integrations, per-agent enable/disable, scopes, and an audit of agent actions (agent actor type already exists, `MicroLimsDbContext.cs:551-565`). Scope needs definition (Q11).
- **Access review report:** a periodic (e.g. quarterly) generated report per Annex 11 §12.3, covering user, role, effective permissions, last login, status history and SoD flags. A reviewer signs it off with an e-signature, and it's stored as an immutable record.
- **Maintenance mode / announcements:** a banner and read-only mode (a write-blocking middleware with an admin bypass for the admin panel only), audited on/off with a reason.

---

## 11. Deliverable 6 — Open questions (decisions needed before build)

| # | Question | Why it matters |
|---|---|---|
| Q1 | What are the **actual** role → permission grants and user → role assignments in production? (Not queried.) | Sizes the SoD remediation and data migration. |
| Q2 | Should **System Administrator keep any lab or master-data permissions** (`MasterData.Manage`, `Items.Manage`, `Documents.ConfigManage`, `Audit.View`)? Is a separate "Lab Configuration Admin" role preferred? | Core of the governing principle. Document Control specs currently *require* admin for DC config (`DocumentConfigurationService.cs:31-36`). |
| Q3 | May the **JWT contract change** (add `roleId`, role name, permission stamp)? Should the header show `Role.Name` or `JobTitle`? | Fixes the role-rename and H5 issues; affects every client and token. |
| Q4 | For **password hashes already in `AuditLogs`**: accept and document; re-hash/rotate everyone's password; or a controlled, documented DB-owner intervention (a deviation) to redact? | Immutability vs data protection; needs QA sign-off. |
| Q5 | Is the **Security Audit Trail** a regulated, immutable record? If yes, change its FKs from `SET NULL` to `RESTRICT` (which blocks hard delete of anyone with events) and add the trigger. | Conflicts with the current hard-delete design. |
| Q6 | Can production run under a **separate non-owner DB role**? Who holds owner credentials? | Needed for real audit immutability (D13). |
| Q7 | How many historical `AuditLogs` rows have negative `EntityId`? Correct them with append-only "correction" rows, or annotate in documentation only? | D15 backfill strategy (UPDATE is blocked by design). |
| Q8 | Which admin actions need an **e-signature** vs a reason only? (Suggested: hard delete, role change, permission change, security policy change, maintenance mode.) | UX and Part 11 scope. |
| Q9 | Which time zone is authoritative: `Lab:TimeZoneId` (Africa/Cairo) or the DB `System.TimeZone` (UTC)? | Duplicate, conflicting settings. |
| Q10 | May admins **trigger background jobs manually**? Which ones? | Some jobs change GxP state (expiry, deviations, effective dates). |
| Q11 | "AI agent / MCP controls" — which agents? In-app features, external MCP servers, or the CLI tooling in `.claude/`? | Phase 3 scope is undefined. |
| Q12 | The brief cites **18 permission codes**; the code has **57**. Is there a target catalogue (e.g. `rbac-permission-catalog.md`) the panel should converge on? | Matrix design. |
| Q13 | Should the 10 declared-but-unenforced codes be enforced, removed, or hidden? | Matrix clarity; the stale `IsEnforced` flag. |
| Q14 | Keep **hard delete** at all once the panel consolidates Users, or restrict it to a "created in error, within N hours" window? | Risk reduction. |
| Q15 | **H2:** is the repository public today? Has the dump been exposed? Is an incident or deviation report required? | Determines urgency and process (outside code). |

---

## Appendix — things verified absent

- No admin endpoint to list or revoke sessions (only `RevokeAllRefreshTokensAsync` callers: `AuthenticationService.cs:120, 329, 368`; `UserService.cs:227`).
- No reader for `SecurityAuditEvents` in `B/MicroLIMS.Application` or `B/MicroLIMS.API` (the DbSet is declared only, `IMicroLimsDbContext.cs:32`).
- No expired-token purge.
- No DB size or backup status code.
- No role-level SoD check.
- No password expiry or idle timeout.
- No migration that backfills `AuditLogs.EntityId`.
