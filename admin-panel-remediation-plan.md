# Admin Control Panel — Remediation & Build Plan

**Input:** `admin-panel-recon-report.md`, including its §0.1 verification errata.
**Status:** PLAN ONLY. Nothing here is implemented. Each wave starts only after its gate decisions are signed off.
**Covers:** every finding — H1–H8, N1–N9 and the 22 checklist gaps (A1–G22). The traceability matrix in §6 maps each one to a work package (WP).

---

## 1. Orchestration model (token economy)

| Tier | Model | Used for | Never used for |
|---|---|---|---|
| Orchestrator | **Opus** (this session) | Gate decisions with the user; writing each WP spec (≤1 page, exact files/lines, acceptance tests); reviewing **security-critical** diffs (H3, H5, N1, N3, SoD, migrations); final PR sign-off | Bulk code writing, running test suites, reading large files |
| Implementer | **Sonnet** | Multi-file slices: services + migrations + tests; anything that needs judgement about EF, transactions or auth | Decisions that belong to the user; widening scope beyond the spec |
| Worker | **Haiku** | Mechanical edits from an exact spec (≤3 files); regenerating snapshots and counts; role→permission swaps in the frontend; running lint/tsc/vitest and summarising failures; CI log triage; checklist review of a Sonnet diff against its spec; citation and doc updates | New design, security logic, migrations |

**Rules that keep tokens low:**
1. **Spec-in, diff-out.** Each agent gets only its WP spec, not this conversation. It returns a ≤300-word summary, a file list and test output, never whole files.
2. **One WP per agent, one branch per WP.** Agents run with `isolation: "worktree"` so parallel WPs can't collide. Opus merges WP branches into the PR branch in dependency order.
3. **Two-pass review.** Haiku runs a cheap pass first (spec checklist, tests added, no files touched outside scope). Opus reviews only WPs tagged 🔒 and only the diff.
4. **Follow-ups reuse the same agent** (SendMessage) instead of spawning a fresh one that has to re-read context.
5. **Parallelise within a wave**, but serialize WPs that touch the same files (noted under *Conflicts*).
6. **CI is the backend oracle.** This container has **.NET 10 SDK and PostgreSQL 16** (installed this session, see E1), so agents build and run both test passes locally the way CI does. CI on the PR stays the final gate. Frontend checks run locally (Node 22 present; `npm ci` needed once).

---

## 2. Gate decisions (owner: you)

| ID | Decision | Blocks | Recommendation |
|---|---|---|---|
| **E1** | ✅ **Resolved 2026-10-26.** The app targets **.NET 10**. `dotnet-sdk-10.0` installed via apt (`packages.microsoft.com` config; `dot.net` is blocked by the proxy), plus a local PostgreSQL 16 cluster with the CI role `microlims_ci` (trust auth). This is per-session; add `apt-get install -y dotnet-sdk-10.0` to the environment setup script so new sessions have it. | — | Done |
| **D0** | ✅ **Partly resolved.** The dump was removed going forward in commit `14e4cf3`, and backups are ignored. **Still open:** history rewrite (force-push to `main`), credential rotation, deviation record (Q15). | WP0.1 | Rewriting history is your call. |
| **D1** | ✅ **Decided 2026-10-26.** The System Administrator keeps: Users.Manage, Roles.Manage, Audit.View, Reporting.Admin, System.ViewErrorLog, System.ViewSecurityAudit, **MasterData.Manage, Items.Manage**, Documents.ConfigManage, **Kpi.ConfigureProfiles**, Discussions.View/Create, Messages.Use, plus the read-only views Signatures.Manage, Dashboards.LabOverview/Review, Kpi.View/ViewTeam, Samples.TrackAll, MonitoringPlan.View, Documents.TrainingViewMatrix, and the new **Sections.ViewAll** (read-only all-sections scope). Every other code is removed from the built-in admin role; custom roles are untouched. Classification of codes the owner was not asked about: Opus. | Wave 1 | Done |
| **D2** | ✅ **Decided 2026-10-26: block.** A role can't hold {Users.Manage, Roles.Manage} together with any GxP execution/approval code (Samples.Review/Approve/Correct/Receive/ReceiveOwnLab/AssignAnalyst, TestWorkflow.*, Hplc.Operate, Solutions.Prepare, WorkingStandards.Qualify, Media.Prepare/Release, Cryovials.Manage/Approve, MonitoringPlan.Approve, EquipmentPlan.Approve, EquipmentWork.Record, Oos.Manage, Documents.Review/Approve). Also decided: Document Control **admin override removed** (Section Head override unchanged); lab scope is **read-only all sections** via Sections.ViewAll. | WP1.3, WP1.5 | Done |
| **D3** | JWT contract change (Q3): add `rid` (role id) and `pv` (role permission-version) claims; show `Role.Name` in the header; signatures snapshot the role **name**. | WP1.6 | ✅ Approved 2026-10-26 ("go ahead"). |
| **D4** | Historical password hashes already in `AuditLogs` (Q4). | WP0.3 part B | Force a password reset for all users after WP0.3 ships, and document the residual rows in a deviation. Don't break the immutability trigger. |
| **D5** | Is `SecurityAuditEvents` an immutable regulated record (Q5)? | WP2.5 | Yes. Switch FKs to `RESTRICT` and add the trigger, so hard delete stays limited to never-active users (it already is). |
| **D6** | Separate non-owner DB role in production (Q6). | WP2.5 part B | Yes. This is an ops runbook item, not code. |
| **D7** | Which admin actions need e-signature vs reason only (Q8). | WP2.2 | E-sign: hard delete, role change, permission change, security-policy change, maintenance mode. Reason only: everything else. |
| **D8** | Negative-`EntityId` historical rows (Q7). | WP2.9 | Append "correction" audit events via a one-off script; never UPDATE. |
| **D9** | Remaining report questions Q9–Q14 (time zone, manual job runs, AI/MCP scope, catalogue target, unenforced codes, keep hard delete). | Waves 3–4 | Decide at the Wave 3 gate. |

---

## 3. Work packages

Notation: 🔒 = Opus reviews the diff. **Conflicts** = WPs that must not run in parallel with this one.

### Wave 0 — Urgent fixes (independent of the admin panel; gate: D0, D4 only)

> **Status 2026-10-26: COMPLETE on `claude/eager-ramanujan-izk0z7`.** The merged branch has the full backend suite green locally against PostgreSQL 16: 3,613 passed, 0 failed, 0 skipped. Baseline before Wave 0 was 3,580, and its only 4 failures were the OCR tests, caused by a missing tesseract binary in this container (now installed). The no-DB pass has 3,363 passed and 250 skipped. Frontend: 90 files / 436 Vitest tests passed, `tsc -b` clean, lint 0 errors. `ef migrations has-pending-model-changes` reports none.
>
> | WP | Result | Agent → review |
> |---|---|---|
> | WP0.1 | Dump untracked, backups ignored (`14e4cf3`, `bc604f4`). History rewrite still open (D0). | Opus |
> | WP0.2 | **Bug confirmed on PostgreSQL before the fix**, in **three** places: waste destruction (trigger rejects the signature UPDATE), maintenance entry and calibration (FK to signature id `0`). Fix: reserve the record id from its identity sequence via a new `IDatabaseSequenceHelper.ReserveIdAsync<T>`, then insert the signature and record together. Plan-approval variants verified as no-ops. 6 new PG tests. | Sonnet → Opus (sent back once: the first version put PostgreSQL SQL in the Application layer) |
> | WP0.3 | `[AuditRedacted]` on 5 credential properties; capture writes `[REDACTED]`. Architecture test with a 1-entry allowlist (`DocumentAcknowledgementRecord.ControlledFileHash`). Opus restored subtype-aware exclusion (`d3c6a40`). | Sonnet → Opus |
> | WP0.4 | Previous RoleId fixed + test. | Haiku → Opus |
> | WP0.5 | Job Title kept on edit and settable on create + 3 Vitest tests. | Haiku → Opus |
> | WP0.6 | `AdministrationContinuityGuard` (by permission, active users) wired into role change, disable, permission edit and hard delete; own-role permission edits blocked. 22 existing fixtures now seed grants and a standing admin. | Sonnet → Opus |
> | WP0.7 | Audit search `Take` clamped to 1–1000; `npm test` added to CI. | Haiku → Opus |
>
> Still open from Wave 0: **D0** (history rewrite, credential rotation) and **D4 part B** (forced password reset plus a deviation note for hashes already in `AuditLogs`).

| WP | Covers | Model | Scope / spec summary | Acceptance |
|---|---|---|---|---|
| **WP0.1** | H2 | Haiku | `git rm --cached backups/*.dump`; add `backups/*.dump` and `*.dump` to `.gitignore`; note in `DEPLOYMENT.md` where backups belong. History rewrite and secret rotation are **human** steps (D0). | The file is untracked; CI is green. |
| **WP0.2** 🔒 | N1 | Sonnet | In `BiologicalWasteDestructionService.cs:192-222` and `EquipmentWorkService.cs:372-399`, reorder inside the existing `UnitOfWork`: add and save the record first, then `SignAsync(..., entityId: record.Id)`, link it, save. A wrong password throws; `UnitOfWork.cs:54-58` already rolls back and keeps the refused-attempt row. **Second defect on the same path:** `EquipmentWorkService.cs:392` sets `ElectronicSignatureId = signature.Id` while the signature is still unsaved. EF Core (≥3.0) doesn't write temporary keys back to the CLR property, so this is `0` on Npgsql. The FK insert probably fails before the trigger is even reached. In-memory tests hide it, because that provider assigns real keys on `Add`. Link via the navigation (`ElectronicSignature = signature`), or set the FK in the second save (that UPDATE is on the entry, which is allowed, not on the signature). **First** write a PostgreSQL integration test for each path that reproduces the trigger error, so the fix is proven. Add an architecture test: no code assigns `ElectronicSignature.EntityId` after creation. Have Haiku grep for other `= signature.Id` assignments made before a save, and fold any hits into this WP. | Both new PG tests pass in CI; existing signature tests are unchanged. |
| **WP0.3** 🔒 | H3, N2 | Sonnet | **Part A:** property-level redaction in `MicroLimsDbContext.SerializeValues` (`:544-545`). Add a `[AuditRedacted]` marker attribute in Domain (no persistence dependency) on `User.PasswordHash`, `PasswordHistory.PasswordHash`, `PasswordResetToken.TokenHash`, `AdminPasswordRecovery.CodeHash`, `InstrumentAgent.KeyHash`. Serialize as `"[REDACTED]"` so the fact of change stays visible. Add an architecture test (same pattern as `UserDeletionTests.cs:247-271`): any property named `*Hash`, `*Secret`, `*Token` or `*Code*Hash` must carry the marker or sit on an excluded entity. **Part B (after D4):** a forced-reset runbook. No data change to `AuditLogs`. | Unit test: a login writes a User row with `PasswordHash: "[REDACTED]"`; the architecture test passes. |
| **WP0.4** | H4 | Haiku | `UserService.cs:177-199`: capture `prevRoleId` before line 178 and use it in `PreviousValue`. Add a unit test. | The test asserts the old id is in PreviousValue. |
| **WP0.5** | H8 | Haiku | Frontend `UserService.updateProfile` sends `jobTitle`; add a Job Title field to the Users edit dialog. Backend unchanged. Add a Vitest test. | Editing a profile keeps the job title. |
| **WP0.6** 🔒 | N3, N4, A2 last-admin | Sonnet | One shared `AdministrationContinuityGuard` in Application: "after this change, at least one **active, unlocked** user must hold `Users.Manage` **and** one must hold `Roles.Manage`" — counted **by permission, not RoleType**. Call it from `UserService.ChangeRoleAsync`, `SetStatusAsync`, `RoleService.UpdatePermissionsAsync`, `RoleService.DeleteAsync` and `UserDeletionService.HardDeleteAsync` (replacing `:65-72`). Also block editing the permissions of a role the acting user holds. Fix the stale test name in the `UserReferenceRegistry.cs:25-28` comment. | Unit tests for each path; the existing hard-delete last-admin test still passes. **Conflicts:** WP0.4 (same file — run WP0.4 first). |
| **WP0.7** | D14 cap, N8 | Haiku | Clamp `AuditSearchRequest.Take` to 1–1000 (`AuditSearchService.cs:72`); add `npm test` to the CI frontend job (`ci.yml:218-245`), after confirming the suite passes locally. | CI runs Vitest; the cap has a unit test. |

### Wave 1 — Segregation of duties & identity correctness (gate: D1, D2, D3)

> **Status (2026-10-26): Wave 1 is complete.** The full suite passed with PostgreSQL (3,681 tests), `has-pending-model-changes` reported no changes, and `tsc -b`, lint (0 errors) and Vitest (466 tests) passed.
>
> | WP | Delivered |
> |---|---|
> | WP1.1 | Equipment status and inventory changes made by workers are recorded as the System actor. `CurrentSystemProcessName` is set by all 7 workers. Migration `AllowSystemActorOnEquipmentChanges`. |
> | WP1.2 | `SystemAdministratorGrants` is a keep-list of 22 codes, including the new `Sections.ViewAll`. Migration `RestrictSystemAdministratorToSystemPermissions` touches only the system role of Type 0. It writes a System-actor audit row and bumps `PermissionVersion`, and its `Down` restores the grants. `Documents.TrainingViewMatrix` is now enforced. |
> | WP1.2b | Seed, correction, EM-batch and permission-enforcement tests were updated. The authorization matrix was regenerated: SystemAdministrator lost 194 endpoints and no other role changed. |
> | WP1.3 | Every `RoleType.SystemAdministrator` shortcut was removed from Application. Section scope is split: reads use `GetReadableSectionIdsAsync` (`Sections.ViewAll` means all sections, read-only), and writes use `GetMemberSectionIdsAsync`. The Document Control admin override is gone. |
> | WP1.4 | The HPLC/ICP pages and the preparation forms check permissions, not the role name. |
> | WP1.5 | `SegregationOfDutiesRules`: holding both {Users.Manage, Roles.Manage} and any GxP execute, review or approve code returns 409. `GET /api/roles/sod-rules` exposes the rules, and the permission matrix greys out conflicting codes. |
> | WP1.6 | Tokens carry `rid` and `pv`, so a role move or a permission edit invalidates them. `roleName` appears in login and `/me`. Signatures snapshot `RoleNameSnapshot`. Audit search labels "current role". |
>
> **Resolved (2026-10-27):** the owner decided the administrator may configure any section. A new `Sections.ConfigureAll` permission (System Administrator only, outside every SoD set; migration `GrantSystemAdministratorConfigureAllSections`) opens master-data and settings writes (specifications, test definitions, stage replicates, material/solution/HPLC/ICP method masters, KPI settings, workbook import) in every section. GxP records still follow membership. Equipment, chromatography columns and materials stay on membership because their services share guards with operational flows.
>
> **D4 done:** migration `ForcePasswordResetAfterAuditHashExposure` sets `MustChangePassword` for every user, revokes all refresh tokens, and writes one System-actor audit row. Deviation record: `docs/deviations/DEV-2026-AUD-001-credential-hashes-in-audit-trail.md`. Instrument agent keys must be rotated by an administrator (manual step in the deviation).

| WP | Covers | Model | Scope / spec summary | Acceptance |
|---|---|---|---|---|
| **WP1.1** | N6 prerequisite | Sonnet | Replace `EquipmentWorkService.cs:661` ("lowest-id admin / user named admin or system") with an explicit system actor: `ActorType.System` + `SystemProcessName`, and `PerformedByUserId` null for system-generated entries (schema change if the column is non-null). Audit rows already support a System actor (`MicroLimsDbContext.cs:551-565`). | Auto-generated overdue records show "System: EquipmentOverdueWorker". |
| **WP1.2** 🔒 | H1, B8, N7 | Sonnet | Data migration (pattern: `20261014160538_SeedKpiViewTeamPermission.cs:11-36`) that **revokes** from `Type = 0` every code outside the D1 keep-list, written to an audit event per role. `Down()` restores. Change `DbSeeder.cs:530` from `All` to an explicit `SystemAdministratorGrants` list in `PermissionConstants`. Add a guard comment and an architecture test so future migrations can't grant GxP codes to Type 0. For N7, confirm that removing the base codes `Media.Prepare` / `Cryovials.Manage` is in the revoke list. | Seed and migration tests pass. |
| **WP1.2b** | WP1.2 fallout | Haiku | Mechanical test updates: `RolePermissionSeedDataTests.cs:68-73, 207, 217, 224-228`; regenerate `ArchitectureTests/authorization-matrix.txt` with `MICROLIMS_WRITE_AUTH_MATRIX=1` (locally, now that E1 is done); `SampleCorrectionTests.cs:595-602`; `EMBatchLocationTests.cs:703-707`; `KpiViewTeamPermissionMigrationPostgresTests.cs:55`. Opus reviews the regenerated matrix diff. | CI is green; the matrix diff shows only removals for SystemAdministrator. |
| **WP1.3** 🔒 | B7 shortcuts, N6 | Sonnet | Replace each `RoleType.SystemAdministrator` grant with a permission check or remove it: `DocumentAuthorizationService.cs:100-249` (9 sites), `DocumentReviewService.cs:292, 433, 493`, `DocumentApprovalService.cs:109, 416` (map to `Documents.Review/Approve` — this also makes the inert `Documents.Approve` real), `PeriodicReviewService.cs:563`, `DocumentFileService.cs:325`, `ItemStmService.cs:80`, `DashboardNotificationService.cs:174, 188`. Keep `UserSectionScopeService.cs:26` only for read endpoints, or replace it with an explicit `Sections.ViewAll` code (decide at D1). | Document Control tests pass; the RoleType grep for SystemAdministrator in Application returns only the documented exceptions. **Conflicts:** WP1.6 (DocumentAuthorizationService). |
| **WP1.4** | N5 | Haiku ×2 (parallel: HPLC/ICP; the rest) | Frontend: replace `role === "SystemAdministrator"` with permission checks in the 7 HPLC/ICP files, `ConfirmPreparationForm.tsx:32`, `TestPreparationForm.tsx:35`, `DocumentDetailPage.tsx:206`, `useMyLabs.ts:48, 56` (labs come from memberships for everyone). Leave display-only mappings to WP1.6. | `npm run lint`, `tsc -b` and `npm test` are green; the grep shows only display mappings left. |
| **WP1.5** 🔒 | B8, D2 | Sonnet | `SodRules` in Shared (conflict sets from D2). Enforce in `RoleService.CreateAsync` / `UpdatePermissionsAsync` (reject with 409 and the conflicting pair). Add `GET /api/roles/sod-rules`. `PermissionMatrix.tsx` greys out conflicting codes and explains why. Add a startup or health check that flags any existing role in violation. | Unit tests per conflict; matrix UI test. |
| **WP1.6** 🔒 | H5, B9, C11 snapshot | Sonnet | Add `Role.PermissionVersion` (incremented in `PermissionService.Grant/Revoke`) and claims `rid` and `pv` (`JwtTokenService.cs:31-48`, `AuthenticationService.cs:143, 208`). `AccessTokenRevalidator.cs:43-72` rejects when `rid` or `pv` differ. Add `roleName` to login and `/me`; the header and `formatRoleFallback` show it (`Header.tsx:73-76`, `userDisplay.ts:11-17`). `ElectronicSignatureService.cs:89` snapshots `Role.Name` (add a `RoleTypeSnapshot` column if the type is still needed). `AuditSearchService.cs:109` shows the role **at the time** (snapshot it at capture, or label it "current role"). | `AccessTokenRevalidationTests` gets cases for a same-type role move and a permission edit → 401 → refresh picks up the new claims. |

### Wave 2 — Admin panel Phase 1 build (gate: D5, D6, D7)

> **Status (2026-10-27): Wave 2 is complete.** D5–D8 were taken as recommended. Validation: full suite with PostgreSQL (3,753 tests), `has-pending-model-changes` clean, `tsc -b`, lint (0 errors), Vitest (505 tests).
>
> | WP | Delivered |
> |---|---|
> | WP2.1 | `/admin` shell (`frontend/src/modules/admin/`) with Users, Roles, Audit Trail, Error Log and Security Policy tabs; old URLs redirect. The document audit link keeps mirroring the server rule (Reviewer, Section Head, or `Audit.View`). |
> | WP2.2 | A reason is required on every admin user and role action and is stored in `AuditLog.Reason` (`IMicroLimsDbContext.CurrentReason`). Hard delete, role change, permission edit and role delete are e-signed. |
> | WP2.3 | `GET/POST /api/admin/users/{id}/sessions[/revoke]` with a reason; `RefreshToken` gains `CreatedIp`, `UserAgent`, `LastUsedAt`. Issued access tokens remain valid until they expire. |
> | WP2.4 | Audit search uses keyset paging and new filters; CSV export (max 50,000 rows, formula-safe, recorded). Security Events and Login History require `System.ViewSecurityAudit`, which is now enforced — Section Heads lose login history unless granted it. |
> | WP2.5 | TRUNCATE guards on 10 append-only tables; LoginHistories and SecurityAuditEvents are append-only, with `SecurityAuditEvents` FKs set to RESTRICT. Runbook: `docs/operations/database-roles-runbook.md`. |
> | WP2.6 | Read-only `GET /api/admin/security-policy` and its page (`Users.Manage`). |
> | WP2.7 | `SessionTokenCleanupWorker` prunes old revoked or expired refresh and reset tokens daily. |
> | WP2.8 | The seeder re-syncs `IsEnforced` and `Description`. |
> | WP2.9 | The audit viewer flags negative-EntityId rows; `scripts/audit/append-negative-entityid-corrections.sql` (dry run by default, append-only, idempotent). |
> | WP2.10 | No GxP write bypasses the audit capture. The only deliberate bypasses are the agent heartbeat (`LastSeenAt`) and the notification read flag. |

| WP | Covers | Model | Scope | Acceptance |
|---|---|---|---|---|
| **WP2.1** | G22, IA | Sonnet (shell) + Haiku (route moves) | `/admin` layout with tabs from report §9; move Users, Roles, Error Log and Audit routes under `/admin/*` with redirects from the old paths; one new sidebar group. Fix the role-gated Document audit link (`menuConfig.ts:108-113`) to a permission. | `menuConfig.test.ts` is updated; the old URLs redirect. |
| **WP2.2** 🔒 | A2, D16, D7 | Sonnet | A required `reason` on every admin user and role endpoint (create, profile, email, unlock, reset, force change, hard delete, role create/rename/delete, permission edit). E-signature on the D7 list using `IElectronicSignatureService`. Reasons are stored in the `AuditLog.Reason` column, not only inside JSON. UI dialogs collect the reason. | Each endpoint's tests send no reason → 400. |
| **WP2.3** | A4 | Sonnet | `GET /api/admin/users/{id}/sessions` and `POST …/sessions/revoke` (reuses `RevokeAllRefreshTokensAsync`; security event `SessionRevoked` with the actor). Add `CreatedIp`, `UserAgent` and `LastUsedAt` to `RefreshToken`. Sessions tab. | Unit and integration tests; the revoke takes effect on the next request (via WP1.6). |
| **WP2.4** | D14, H7, A5 | Sonnet | Audit viewer: server paging (keyset on Id), filters (+ ActionCategory, ActorType, CorrelationId, Reason), CSV export recorded as an audit event; response values are already redacted by WP0.3. Security Events and Login History tabs enforce `System.ViewSecurityAudit` (which moves into `Enforced`). | Paging, filter and export tests; the permission is enforced. |
| **WP2.5** 🔒 | D13, D5, D6 | Sonnet + ops runbook | Migration: statement-level `BEFORE TRUNCATE` trigger on all append-only tables; append-only trigger on `LoginHistories`; per D5, `SecurityAuditEvents` FKs → `RESTRICT` plus trigger (update `UserReferenceRegistry.cs:124-125` to Blocks). The runbook covers the app role with `INSERT, SELECT` only on audit tables (D6). | A PG integration test shows TRUNCATE and UPDATE both fail. |
| **WP2.6** | C10 | Haiku | Read-only Security Policy page: `GET /api/admin/security-policy` returns the current constants (`PasswordPolicy`, `AuthenticationService.cs:20-24`, `JwtSettings`, `ElectronicSignatureService.cs:17-18`) with their source. | Snapshot test of the payload. |
| **WP2.7** | A4 token GC, E18 | Haiku | A `SessionTokenCleanupWorker` that deletes expired or revoked refresh tokens older than N days and used/expired reset tokens. Both tables are excluded or non-GxP, so a delete is fine; record a security event summary. | Unit test with TimeProvider. |
| **WP2.8** | B6 stale flag, Q13 | Haiku | `DbSeeder.SeedPermissionsAndGrants` re-syncs `IsEnforced` and `Description` for existing rows (`DbSeeder.cs:511-520`). | Seed test. |
| **WP2.9** | D15 / D8 | Sonnet | A read-only query in the Audit viewer flags rows with `EntityId` < 0. A one-off script appends correction events (D8). | Script dry-run output reviewed by Opus. |
| **WP2.10** | N9 | Sonnet (read-only research, then a fix if needed) | Trace the raw Npgsql writes (`DatabaseSequenceHelper.cs:44`, `DatabaseHealthMonitorWorker.cs:174, 338`) and the `ChangeTracker.Clear()` sites for unaudited GxP writes. | A findings note; a fix WP only if a GxP gap is found. |

### Wave 3 — Phase 2: configuration & operations (gate: D9 / Q9, Q10)

| WP | Covers | Model | Scope |
|---|---|---|---|
| **WP3.1** 🔒 | F20, F21, C10 | Sonnet | `SystemSetting` + append-only `SystemSettingVersion` registry; typed and validated; reason + e-sign for GxP keys; audited as `ActionCategory.Configuration`. Migrate the security-policy constants and alert/retention/worker settings into it. Secrets stay in env and display masked. |
| **WP3.2** | F20 | Sonnet | Fold the Document Control `ConfigurationSettings` into the registry or expose it read-only; resolve the duplicate time zone (Q9). |
| **WP3.3** | E18 | Sonnet | `BackgroundJobRun` table written by all 7 workers plus the WP2.7 worker; Jobs page; "run now" only for jobs approved in Q10. |
| **WP3.4** | E19 | Haiku | DB size and top-tables endpoint (`pg_database_size`); `BackupRun` record written by `scripts/DatabaseBackup/backup.sh`; Overview tiles. |
| **WP3.5** | E17 | Haiku | Critical-alert recipients come from the registry (multi-recipient); "send test alert" button. |

### Wave 4 — Phase 3: integrations, review, operations (gate: Q11, Q14)

| WP | Covers | Model | Scope |
|---|---|---|---|
| **WP4.1** | Integrations | Sonnet | Instrument agents tab (link/move from `InstrumentAgentController`), SMTP status and test, storage status. |
| **WP4.2** 🔒 | AI/MCP controls | Opus spec → Sonnet | Scope per Q11: inventory, enable/disable, scopes, and an agent-action audit view (uses the existing System actor + `SystemProcessName`). |
| **WP4.3** 🔒 | A5 access review | Sonnet | Periodic access-review report (user × role × effective perms × last login × status history × SoD flags), reviewer e-signature, stored immutable. |
| **WP4.4** | Ops | Sonnet | Maintenance mode (write-blocking middleware, admin-panel bypass, audited on/off with reason) and announcement banner. |
| **WP4.5** | Q14 | Haiku | Restrict hard delete to a "created in error within N hours" window, if decided. |

---

## 4. Sequencing & parallelism

```
Wave 0:  WP0.1 ─┐  WP0.2 🔒 ─┐  WP0.3 🔒 ─┐  WP0.4 → WP0.6 🔒   WP0.5   WP0.7
                └────────────┴────────────┴─ all independent except WP0.4→WP0.6
Wave 1:  WP1.1 → WP1.2 🔒 → WP1.2b      WP1.4 (×2 Haiku, parallel)
                 WP1.3 🔒 → WP1.6 🔒    WP1.5 🔒 (after WP1.2)
Wave 2:  WP2.1 → {WP2.2 🔒, WP2.3, WP2.4, WP2.6}   WP2.5 🔒   WP2.7  WP2.8  WP2.9  WP2.10
Wave 3/4: as listed, after their gates
```

- **Max parallel agents per wave:** 4. That's enough to keep CI and review from becoming the bottleneck.
- **PR strategy:** one PR per wave (Wave 0 may split WP0.2 and WP0.3 into their own PRs, since they're hotfix-class). Each PR runs the full CI; Opus does the 🔒 reviews before marking it ready.

## 5. Per-WP validation checklist (Haiku runs; Opus spot-checks)

1. A failing test written first for any defect fix (WP0.2, WP0.3, WP0.4, WP0.6).
2. Backend: `dotnet build -c Release`, unit run, PG run (CI; locally once E1 is done). The `verify_test_results.py` gates are satisfied.
3. `dotnet ef migrations has-pending-model-changes` is clean for any WP with entity changes.
4. Frontend: `npm run lint`, `npx tsc -b`, `npm test`.
5. `authorization-matrix.txt` is regenerated and the diff reviewed for any WP that touches grants or policies.
6. No file outside the WP's spec list is touched.
7. GxP check: no new UPDATE/DELETE on append-only tables, every new admin action is audited with actor, old/new values and reason.

---

## 6. Traceability matrix — finding → work package

| Finding | WP | | Finding | WP |
|---|---|---|---|---|
| H1 admin holds all perms | WP1.2, WP1.2b | | A1 user fields | WP2.3 (session cols); rest optional in WP4.3 |
| H2 prod dump in git | WP0.1 (+ D0 human) | | A2 user ops gaps | WP0.4, WP0.5, WP0.6, WP2.2 |
| H3 secrets in audit | WP0.3 (+ D4) | | A3 FK map / hard delete | WP0.6, WP2.2 (e-sign), WP4.5 |
| H4 role-change prev id | WP0.4 | | A4 sessions | WP2.3, WP2.7 |
| H5 stale permissions | WP1.6 | | A5 access history | WP2.4, WP4.3 |
| H6 hard delete exists | WP0.6, WP2.2, WP4.5 | | B6 matrix / stale flag | WP1.5, WP2.8 |
| H7 no security-event viewer | WP2.4 | | B7 admin perms / RoleType shortcuts | WP1.3 |
| H8 job title wipe | WP0.5 | | B8 SoD | WP1.2, WP1.5 |
| N1 signature UPDATE vs trigger | **WP0.2** | | B9 role rename | WP1.6 |
| N2 wider secret capture | WP0.3 | | C10 security config | WP2.6, WP3.1 |
| N3 admin lock-out | WP0.6 | | C11 e-sig snapshot | WP1.6 |
| N4 guard by RoleType | WP0.6 | | D12/D13 audit protection | WP0.3, WP2.5 |
| N5 frontend admin shortcuts | WP1.4 | | D14 audit viewer | WP0.7, WP2.4 |
| N6 more RoleType shortcuts / system actor | WP1.1, WP1.3 | | D15 negative EntityId | WP2.9 |
| N7 stacked base perms | WP1.2 | | D16 reasons | WP2.2, WP3.1 |
| N8 frontend tests not in CI | WP0.7 | | E17 error log | WP3.5 |
| N9 untraced bypasses | WP2.10 | | E18 jobs | WP2.7, WP3.3 |
| | | | E19 health / DB / backups | WP3.4 |
| | | | F20/F21 config | WP3.1, WP3.2 |
| | | | G22 navigation | WP2.1 |

## 7. Rough effort (agent-runs, not calendar)

| Wave | WPs | Sonnet runs | Haiku runs | Opus reviews |
|---|---|---|---|---|
| 0 | 7 | 3 | 4 | 3 |
| 1 | 7 | 5 | 3 | 5 |
| 2 | 10 | 6 | 4 (+ checks) | 3 |
| 3 | 5 | 3 | 2 | 1 |
| 4 | 5 | 4 | 1 | 2 |

Most of the context-heavy reading is done by Sonnet and Haiku. Opus reads only specs, summaries and the 🔒 diffs.
