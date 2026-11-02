# Functional Specification — MicroLIMS

| Field | Value |
|---|---|
| **Document No.** | ML-SYS-FS-001 |
| **Version** | 0.1 — Draft for review (Group A only) |
| **System** | MicroLIMS (Laboratory Information Management System) |
| **Related plan** | ML-SYS-VP-001 Validation Plan (deliverable 7) |
| **Specifies** | ML-SYS-URS-001 User Requirements Specification, Group A |
| **Software baseline** | Written from the source code at `main`, commit `cc492d3` (2026-11-02). Re-confirm against the frozen baseline before approval. |
| **Date** | 2026-11-02 |

> **Status of this draft.** Prepared with AI assistance by reading the source code. It describes what the software does today, not what it was intended to do. It carries no approval until reviewed and signed by the people named in section 6.

---

## 1. Purpose

This document says **how** MicroLIMS meets each user requirement in ML-SYS-URS-001: the behaviour a tester can observe, the setting values and limits, and where in the code the behaviour lives. It is the basis for the Operational Qualification scripts and for the Requirements Traceability Matrix.

Where the system does not meet a requirement, the entry says so plainly and points to Annex 1.

## 2. How to read this document

**Entry layout.** Every functional specification entry has an ID `FS-A-nnn`, the URS requirements it satisfies, a description of the behaviour, the code that implements it, and the automated evidence that already exists.

**Code references** are repository-relative paths and symbol names. They do not carry line numbers, which change with every edit. `B/` stands for `backend/` and `F/` for `frontend/src/`.

**Automated evidence** names test classes in `backend/MicroLIMS.Tests`. The VP (section 3, point 2) accepts these as evidence for low- and medium-risk functions once the OQ document records the commit, date and pass count of a full run. "None found" means no dedicated test was found. It does not prove the behaviour is untested. OQ must cover it.

**Settings** named `security.*`, `retention.*` and so on are keys of the system settings registry (FS-A-071). Values given as "default" are the values a new installation starts with.

## 3. Architecture of the Group A functions

| Concern | Where it lives |
|---|---|
| Sign-in, tokens, passwords | `B/MicroLIMS.Application/Services/AuthenticationService.cs`, `AdminPasswordRecoveryService.cs`; API `B/MicroLIMS.API/Controllers/AuthenticationController.cs` |
| Per-request checks | `B/MicroLIMS.API/Authorization/` (permission policies, `AccessTokenRevalidator`), `B/MicroLIMS.API/Filters/MustChangePasswordFilter.cs`, `B/MicroLIMS.API/Middleware/` |
| Users and roles | `UserService.cs`, `RoleService.cs`, `PermissionService.cs`, `AdministrationContinuityGuard`; `B/MicroLIMS.Shared/Constants/PermissionConstants.cs`, `SegregationOfDutiesRules.cs` |
| Electronic signatures | `ElectronicSignatureService.cs`; entity `ElectronicSignature` |
| Audit trail | `B/MicroLIMS.Persistence/DbContext/MicroLimsDbContext.cs` (automatic capture), `AuditEventService.cs`, `AuditSearchService.cs`, `AuditExportService.cs`; triggers in `B/MicroLIMS.Persistence/Migrations/` |
| Security events | `SecurityAuditService`, `B/MicroLIMS.Shared/Constants/SecurityEventCodes.cs` |
| Archiving | `RecordArchiveService.cs`; entity `ArchivedRecord` |
| Error monitoring | `ExceptionMiddleware`, `CriticalAlertService`, background workers; entities `Incident`, `ErrorLog` |
| System configuration and operations | `B/MicroLIMS.Application/Services/SystemSettings/`, `BackgroundJobs/`, `Integrations/`, `AccessReviewService.cs` |
| Administration screens | `F/modules/admin/`, `F/modules/users/`, `F/modules/roles/`, `F/modules/auditSearch/`, `F/modules/errorMonitoring/` under the `/admin` area |

All business rules live in the Application layer. The screens show state and collect input but do not decide permissions or rules (Clean Architecture boundary, checked by architecture tests).

---

## 4. Functional specifications — Group A

### A1. Sign-in

**FS-A-001** · Satisfies URS-A-001
`POST /api/auth/login` is anonymous. The service looks the user up by username and accepts the attempt only when the account is active, not locked, and the password matches. A username is checked for uniqueness when an account is created or renamed (`UserService`), and the refusal message is "Username '…' is already taken."
*Implemented in:* `B/MicroLIMS.API/Controllers/AuthenticationController.cs`, `B/MicroLIMS.Application/Services/AuthenticationService.cs`, `B/MicroLIMS.Application/Services/UserService.cs`
*Evidence:* `UserManagementSecurityTests`, `LoginEnumerationTests`

**FS-A-002** · Satisfies URS-A-002
Every refusal returns the same text, "Invalid username or password." For an unknown username the service verifies the password against a dummy hash. For an existing account it verifies the password before looking at whether the account is locked or disabled. All refusals therefore take about the same time.
*Implemented in:* `B/MicroLIMS.Application/Services/AuthenticationService.cs` (`LoginCoreAsync`)
*Evidence:* `LoginEnumerationTests`

**FS-A-003** · Satisfies URS-A-003
Each wrong password, and each attempt on a disabled account, adds one to the user's failed-attempt counter. At the limit (`security.lockout.maxFailedAttempts`, 3 to 20, default 5) the account is locked until now plus `security.lockout.durationMinutes` (1 to 1440, default 15), all the user's refresh tokens are revoked, and the security event `AUTH_ACCOUNT_LOCKED` is recorded as a system action. A correct sign-in resets the counter and clears the lock.
*Implemented in:* `B/MicroLIMS.Application/Services/AuthenticationService.cs`, `B/MicroLIMS.Domain/Entities/User.cs` (`FailedLoginAttempts`, `LockedUntil`, `IsLocked`)
*Evidence:* `UserManagementSecurityTests`, `SecurityAuditTrailTests`

**FS-A-004** · Satisfies URS-A-004
`PUT /api/users/{id}/unlock` (permission `Users.Manage`) clears the lock and the counter. The request carries a reason. The audit trail records `USER_UNLOCKED` and the security trail records `AUTH_ACCOUNT_UNLOCKED`.
*Implemented in:* `B/MicroLIMS.API/Controllers/UserController.cs`, `B/MicroLIMS.Application/Services/UserService.cs`
*Evidence:* `UserManagementSecurityTests`, `AdminReasonAndSignatureTests`

**FS-A-005** · Satisfies URS-A-005
Every sign-in attempt writes a `LoginHistory` row: user (empty for an unknown username), username, success flag, failure reason ("User not found", "Account locked", "Wrong password", "Account inactive"), source address and time. `GET /api/admin/audit/login-history` returns the rows to holders of `System.ViewSecurityAudit`.
*Implemented in:* `B/MicroLIMS.Application/Services/AuthenticationService.cs` (`RecordLoginAsync`), `B/MicroLIMS.API/Controllers/AuditController.cs`
*Evidence:* `SecurityAuditTrailTests`, `AuditViewerPagingFilterExportTests`

**FS-A-006** · Satisfies URS-A-006
Named rate-limit policies apply a fixed window per source address: `Authentication` (sign-in, password-reset request and confirm, administrator-recovery confirm), `TokenRefresh`, `PasswordChange` (per signed-in user), `ClientErrors` (anonymous crash reports) and `Agent` (instrument agents). Limits come from the `RateLimiting:*` configuration keys. A refused request returns HTTP 429 with a `Retry-After` header.
*Implemented in:* `B/MicroLIMS.API/Extensions/RateLimitingExtensions.cs`, the `[EnableRateLimiting]` attributes in `AuthenticationController.cs` and `SystemController.cs`
*Evidence:* `AuthenticationRateLimitingTests`

**FS-A-007** · Satisfies URS-A-007
A global action filter reads the user's `MustChangePassword` flag from the database on every authenticated request. While it is set, the server answers 403 to everything except `ChangePassword`, `Me` and `Logout` on the authentication controller. The flag is not read from the token, so a change by an administrator or by the user applies at once. After a successful change the flag is cleared.
*Implemented in:* `B/MicroLIMS.API/Filters/MustChangePasswordFilter.cs`; the screen routes the user to the change-password page from the sign-in result (`F/modules/authentication/`)
*Evidence:* `MustChangePasswordEnforcementTests`

**FS-A-008** · Satisfies URS-A-008
On first start `DbSeeder` creates the initial administrator from the deployment secret `Seed__InitialAdminPassword`, with `MustChangePassword` set.
*Implemented in:* `B/MicroLIMS.Persistence/Seed/DbSeeder.cs`
*Evidence:* `InitialAdministratorSeedTests`, `FreshDatabaseSeedPostgresTests`

### A2. Passwords

**FS-A-009** · Satisfies URS-A-009
Passwords are hashed with BCrypt before they are stored. The hash property is marked `[AuditRedacted]` (FS-A-051). Reset tokens, recovery codes and refresh tokens are stored only as hashes.
*Implemented in:* `B/MicroLIMS.Application/Services/AuthenticationService.cs`, `B/MicroLIMS.Domain/Entities/User.cs`
*Evidence:* `UserManagementSecurityTests`, `AuditCredentialRedactionTests`

**FS-A-010** · Satisfies URS-A-010
`PasswordPolicy.Validate` returns every failure at once: shorter than the minimum, no uppercase letter, no lowercase letter, no digit, no special (non-alphanumeric) character. The minimum comes from `security.password.minimumLength` (8 to 64, default 8). The rule runs when a user is created, a password is changed, and a password is reset or recovered. A change to the setting does not re-check existing passwords.
*Implemented in:* `B/MicroLIMS.Shared/Validation/PasswordPolicy.cs`, `AuthenticationService.cs` (`ValidateAndApplyNewPasswordAsync`)
*Evidence:* `UserManagementSecurityTests`, `SystemSettingsRegistryTests`

**FS-A-011** · Satisfies URS-A-011
The previous password hashes of a user are kept in `PasswordHistory`. A new password that matches any of the last `security.password.historyLimit` (1 to 24, default 5) is refused. Older rows are pruned.
*Implemented in:* `B/MicroLIMS.Application/Services/AuthenticationService.cs`, entity `PasswordHistory`
*Evidence:* `UserManagementSecurityTests`

**FS-A-012** · Satisfies URS-A-012
`POST /api/auth/change-password` (signed in, rate limited per user) verifies the current password. A wrong current password records `AUTH_PASSWORD_CHANGE_FAILED` and changes nothing. On success the password, `PasswordChangedAt` and the history are updated, `MustChangePassword` is cleared and `AUTH_PASSWORD_CHANGED` is recorded.
*Implemented in:* `B/MicroLIMS.API/Controllers/AuthenticationController.cs`, `AuthenticationService.cs` (`ChangePasswordAsync`)
*Evidence:* `UserManagementSecurityTests`, `SecurityAuditTrailTests`

**FS-A-013** · Satisfies URS-A-013
`POST /api/auth/password-reset/request` creates a random 32-byte token, stores its hash with an expiry of `security.passwordReset.lifetimeMinutes` (5 to 1440, default 60), and emails the token to the address on the account. The answer is the same whether the username exists or not. `POST /api/auth/password-reset/confirm` accepts a valid, unexpired, unused token with a new password that meets FS-A-010 and FS-A-011. If the account has no email address the token cannot be delivered and a warning is logged for the administrator.
*Implemented in:* `AuthenticationController.cs`, `AuthenticationService.cs` (`RequestPasswordResetAsync`, `ConfirmPasswordResetAsync`), entity `PasswordResetToken`
*Evidence:* `UserManagementSecurityTests`, `LoginEnumerationTests`

**FS-A-014** · Satisfies URS-A-014
`POST /api/users/{id}/admin-password-recovery` (`Users.Manage`, reason required) creates a one-time recovery code in the form `XXXX-XXXX-XXXX` from 32 unambiguous characters. The code is valid for 15 minutes and an earlier active code for the same user is invalidated. The user confirms on the public page `/admin-recovery` (`POST /api/auth/admin-password-recovery/confirm`) with username, code and a new password. Five wrong codes invalidate the code. Unknown username, disabled account and wrong code give the same answer. Confirming ends the user's sessions.
*Implemented in:* `B/MicroLIMS.Application/Services/AdminPasswordRecoveryService.cs`, `UserController.cs`, `AuthenticationController.cs`, `F/pages/AdminPasswordRecovery.tsx`
*Evidence:* `AdminPasswordRecoveryTests`

**FS-A-015** · Satisfies URS-A-015
`PUT /api/users/{id}/force-password-change` (`Users.Manage`, reason required) sets `MustChangePassword`. The audit trail records `FORCE_PASSWORD_CHANGE_SET` with the previous value. The Users screen shows a "Must Change Password" badge.
*Implemented in:* `UserController.cs`, `UserService.cs`, `F/modules/users/UsersPage.tsx`
*Evidence:* `UserManagementSecurityTests`, `AdminReasonAndSignatureTests`

**FS-A-016** · Satisfies URS-A-016
A password change, a reset and a recovery set `PasswordChangedAt` and revoke every refresh token of the user. `AccessTokenRevalidator` rejects any access token issued before `PasswordChangedAt` (FS-A-020).
*Implemented in:* `AuthenticationService.cs`, `AdminPasswordRecoveryService.cs`, `B/MicroLIMS.API/Authorization/AccessTokenRevalidator.cs`
*Evidence:* `AccessTokenRevalidationTests`, `SessionRevocationTests`

**FS-A-017** · Satisfies URS-A-017 · **Not implemented**
`PasswordChangedAt` is stored and shown, but no rule reads it to expire a password. See Annex 1, O1.
*Implemented in:* not applicable
*Evidence:* not applicable

### A3. Sessions

**FS-A-018** · Satisfies URS-A-018
Sign-in returns a signed access token (JWT) carrying the user id, the role type, the role id, the role's permission version and the permission codes. Its lifetime is `Jwt:AccessTokenMinutes` in the deployment configuration (1 to 480, default 15). A value outside the range stops the application from starting.
*Implemented in:* `B/MicroLIMS.API/Extensions/JwtConfiguration.cs`, `B/MicroLIMS.Infrastructure/Authentication/JwtTokenService.cs`
*Evidence:* `JwtTokenServiceTests`, `JwtConfigurationTests`

**FS-A-019** · Satisfies URS-A-019
Sign-in also issues a random refresh token. Only its SHA-256 hash is stored, with the creation time, source address, user agent and an expiry of `security.refreshToken.lifetimeDays` (1 to 30, default 7). `POST /api/auth/refresh` accepts only an active token of an active, unlocked user. It marks that token used and revoked, issues a new refresh token and a new access token, and records `AUTH_REFRESH_TOKEN_ROTATED`. A token that is unknown, revoked or expired, or whose account is blocked, is refused with the same message and a security event. A refused token does not end the other sessions of the user (O4).
*Implemented in:* `AuthenticationService.cs` (`RefreshCoreAsync`, `IssueRefreshTokenAsync`), entity `RefreshToken`
*Evidence:* `SessionRevocationTests`, `RefreshTokenSessionMetadataMigrationPostgresTests`

**FS-A-020** · Satisfies URS-A-020
On every request `AccessTokenRevalidator` compares the token with the current account. It refuses the token when the account no longer exists, is disabled or is locked; when the role type, role id or the role's permission version differs from the token; or when the token was issued before the last password change. Editing a role's permissions raises its permission version, so users of that role are refused on their next request and obtain new permissions with a silent refresh.
*Implemented in:* `B/MicroLIMS.API/Authorization/AccessTokenRevalidator.cs`, `PermissionService.cs`
*Evidence:* `AccessTokenRevalidationTests`

**FS-A-021** · Satisfies URS-A-021
`GET /api/admin/users/{id}/sessions` lists the active sessions (start, source address, user agent, last use, expiry). `POST /api/admin/users/{id}/sessions/revoke` (reason required) revokes them and records `AUTH_SESSION_REVOKED`. An access token already issued stays valid until it expires, at most 15 minutes by default.
*Implemented in:* `B/MicroLIMS.API/Controllers/AdminUserSessionsController.cs`, `UserService.cs` (`GetActiveSessionsAsync`, `RevokeSessionsAsync`)
*Evidence:* `UserSessionsAdminTests`, `SessionRevocationTests`

**FS-A-022** · Satisfies URS-A-022
`POST /api/auth/logout` revokes the refresh token that was sent, only if it belongs to the caller, and records `AUTH_LOGOUT`. Without a usable token it revokes every session of the caller.
*Implemented in:* `AuthenticationService.cs` (`LogoutAsync`)
*Evidence:* `SessionRevocationTests`

**FS-A-023** · Satisfies URS-A-023
`SessionTokenCleanupWorker` deletes expired and revoked refresh tokens and used or expired reset tokens older than `retention.sessionTokens.retentionDays` (1 to 365, default 30). It runs every `retention.sessionTokens.intervalHours` (1 to 168, default 24) and records a `AUTH_SESSION_TOKENS_PURGED` event.
*Implemented in:* `B/MicroLIMS.API/BackgroundServices/SessionTokenCleanupWorker.cs`
*Evidence:* `SessionTokenCleanupWorkerUnitTests`

**FS-A-024** · Satisfies URS-A-024 · **Not implemented**
No inactivity rule exists on the server. A session lasts as long as it is renewed, and the browser renews it automatically. See Annex 1, O2.
*Implemented in:* not applicable
*Evidence:* not applicable

### A4. User account management

**FS-A-025** · Satisfies URS-A-025
`POST /api/users` (`Users.Manage`) creates an account from username, full name, role, email, job title, initial password and a reason. The password must meet FS-A-010. `MustChangePassword` is set. The audit trail records the creation.
*Implemented in:* `UserController.cs`, `UserService.cs` (`CreateAsync`), `F/modules/users/UsersPage.tsx`
*Evidence:* `UserManagementSecurityTests`, `AdminReasonAndSignatureTests`

**FS-A-026** · Satisfies URS-A-026
`PUT /api/users/{id}` edits full name, username, email and job title with a version check (FS-A-059). `PUT /api/users/{id}/email` edits the email alone. Each change records `USER_DATA_UPDATED` with the old and new values. Leaving the job title out of a request keeps the existing one.
*Implemented in:* `UserController.cs`, `UserService.cs`
*Evidence:* `UserManagementSecurityTests`, `AdminReasonAndSignatureTests`

**FS-A-027** · Satisfies URS-A-027
`PUT /api/users/{id}/role` needs a reason and the administrator's password (electronic signature, meaning `UserRoleChanged`). It is refused when the target is the caller. The audit trail records `USER_ROLE_CHANGED` with the previous role id and name and the new ones, and the security trail records `AUTH_ROLE_CHANGED`. The user's sessions stop at the next request (FS-A-020).
*Implemented in:* `UserController.cs`, `UserService.cs` (`ChangeRoleAsync`)
*Evidence:* `AdminReasonAndSignatureTests`, `UserManagementSecurityTests`

**FS-A-028** · Satisfies URS-A-028
`PUT /api/users/{id}/status` enables or disables an account. Disabling needs a reason, is refused for the caller's own account, and revokes the user's refresh tokens. The audit trail records `USER_ENABLED` or `USER_DISABLED` and the security trail records `AUTH_ACCOUNT_ENABLED` or `AUTH_ACCOUNT_DISABLED`.
*Implemented in:* `UserController.cs`, `UserService.cs` (`SetStatusAsync`)
*Evidence:* `UserManagementSecurityTests`, `SessionRevocationTests`

**FS-A-029** · Satisfies URS-A-029
No endpoint or service deletes a user account. Disabling is the only way to take an account out of use (FS-A-028). The signature meaning `UserDeleted` is retired but stays in the enumeration so stored values keep their numbers. The user's name, username and role are also copied into each electronic signature, so a signature stays readable whatever happens to the account later.
*Implemented in:* `B/MicroLIMS.API/Controllers/UserController.cs` (no delete action), `B/MicroLIMS.Domain/Enums/SignatureMeaning.cs`
*Evidence:* `AuthorizationMatrixTests` (the reviewed endpoint list holds no user delete), `InactiveUserVisibilityTests`

**FS-A-030** · Satisfies URS-A-030
`GET /api/users?status=active|inactive|all` returns active users by default. The Users screen has Active and Inactive lists.
*Implemented in:* `UserController.cs`, `UserService.cs` (`GetAllAsync`), `F/modules/users/UsersPage.tsx`
*Evidence:* `InactiveUserVisibilityTests`

**FS-A-031** · Satisfies URS-A-031
`AdministrationContinuityGuard` counts active, unlocked users by permission, not by role type. It runs inside user role change, user disable and role permission edit. Deleting a role needs no check, because only a role with no users can be deleted (FS-A-036). A change that would leave no holder of `Users.Manage` or no holder of `Roles.Manage` is refused.
*Implemented in:* `B/MicroLIMS.Application/Services/AdministrationContinuityGuard.cs`
*Evidence:* `UserManagementSecurityTests`, `RoleManagementTests`

**FS-A-032** · Satisfies URS-A-032
`PUT /api/org/users/{userId}/memberships` (`Users.Manage`) sets the user's departments, sections and physico-chemical areas (`UserOrgMembership`). A reason is required. `GET /api/org/my-sections` returns the caller's own sections.
*Implemented in:* `B/MicroLIMS.API/Controllers/LaboratoryOrganizationController.cs`, `F/modules/users/dialogs/UserSectionsDialog.tsx`
*Evidence:* `OrganizationAdministrationTests`, `UserSectionScopeServiceTests`

**FS-A-033** · Satisfies URS-A-033
Every administrative request carries a `Reason`. `AdminActionReason.Require` refuses an empty reason and one longer than 500 characters. The reason is stored in the `Reason` column of the audit entry (FS-A-053).
*Implemented in:* `B/MicroLIMS.Application/Helpers/AdminActionReason.cs`, `UserService.cs`, `RoleService.cs`
*Evidence:* `AdminReasonAndSignatureTests`

**FS-A-034** · Satisfies URS-A-034
Pickers, rosters and notification recipients leave out inactive users. Historical records keep the user's name.
*Implemented in:* the user queries in the Application services, `F/hooks/` pickers
*Evidence:* `InactiveUserVisibilityTests`

### A5. Roles, permissions and segregation of duties

**FS-A-035** · Satisfies URS-A-035
Each controller or action carries `[Authorize(Policy = <permission code>)]`. A policy provider turns a permission code into a requirement that is met when the access token carries that code. A fallback policy denies any endpoint with no declared policy. The reviewed list of every endpoint and its policy is `authorization-matrix.txt`; a test fails if an endpoint's policy changes without the list being regenerated.
*Implemented in:* `B/MicroLIMS.API/Authorization/`, `B/MicroLIMS.API/Extensions/AuthorizationExtensions.cs`
*Evidence:* `AuthorizationMatrixTests`, `PermissionAuthorizationTests`

**FS-A-036** · Satisfies URS-A-036
`GET/POST/PUT/DELETE /api/roles` (`Roles.Manage`) list, create (from a base role type), rename and describe (version-checked), and delete roles. A built-in role and a role with users assigned cannot be deleted.
*Implemented in:* `B/MicroLIMS.API/Controllers/RoleController.cs`, `RoleService.cs`, `F/modules/roles/`
*Evidence:* `RoleManagementTests`

**FS-A-037** · Satisfies URS-A-037
`PUT /api/roles/{id}/permissions` and `DELETE /api/roles/{id}` need a reason and the administrator's password (electronic signatures `RolePermissionsChanged` and `RoleDeleted`). The audit trail records the permissions granted and revoked as one entry. The role's permission version is raised (FS-A-020).
*Implemented in:* `RoleService.cs` (`UpdatePermissionsAsync`, `DeleteAsync`)
*Evidence:* `AdminReasonAndSignatureTests`, `RoleManagementTests`

**FS-A-038** · Satisfies URS-A-038
`SegregationOfDutiesRules` defines two sets. Administration: `Users.Manage`, `Roles.Manage`. GxP execution and approval: sample receive, assign, review, approve and correct; test workflow execute, biochemical decision and supervise; HPLC operate; solution prepare; working-standard qualify; media prepare and release; materials release; cryovial manage and approve; monitoring-plan and equipment-plan approve; equipment work record; OOS manage; document review and approve. `RoleService` refuses to create or save a role that holds a code from both sets, with HTTP 409 and the conflicting pair (for example "Users.Manage + Samples.Approve"). `GET /api/roles/sod-rules` supplies the sets and the permission matrix greys out conflicting codes.
*Implemented in:* `B/MicroLIMS.Shared/Constants/SegregationOfDutiesRules.cs`, `RoleService.cs`, `F/modules/roles/components/PermissionMatrix.tsx`
*Evidence:* `SegregationOfDutiesRulesTests`, `AdministratorSegregationTests`, `RoleManagementTests`

**FS-A-039** · Satisfies URS-A-039
`PermissionConstants.SystemAdministratorGrants` is a fixed list of 26 codes: user, role, audit, reporting, error-log, security-audit, settings and job administration; master-data, item and document-configuration management; KPI profile configuration; read-only dashboards, KPI, sample tracking, monitoring-plan and training-matrix views; the signature trail; discussions and messages; access review; and the two section scopes `Sections.ViewAll` and `Sections.ConfigureAll`. A migration removed all other grants from the built-in role, and a test fails if the list gains a code from the GxP set.
*Implemented in:* `B/MicroLIMS.Shared/Constants/PermissionConstants.cs`, migration `RestrictSystemAdministratorToSystemPermissions`, `B/MicroLIMS.Persistence/Seed/DbSeeder.cs`
*Evidence:* `SystemAdministratorGrantsTests`, `SystemAdministratorGrantsPostgresTests`, `RolePermissionSeedDataTests`

**FS-A-040** · Satisfies URS-A-035, URS-A-042
At this baseline the catalogue holds 65 permission codes. 62 are enforced by at least one endpoint. Three (`Discussions.View`, `Discussions.Create`, `Messages.Use`) are declared but not enforced and are hidden from the matrix. The matrix shows each code's description and an "Enforced" or "Legacy-only" badge. The seeder re-synchronises the enforced flag and description on every start.
*Implemented in:* `PermissionConstants.cs` (`All`, `Enforced`), `DbSeeder.cs`, `F/modules/roles/components/PermissionMatrix.tsx`
*Evidence:* `RolePermissionCatalogPostgresTests`, `PermissionEnforcementMigrationPostgresTests`

**FS-A-041** · Satisfies URS-A-040
`RoleService` refuses a permission edit when the role is the caller's own role.
*Implemented in:* `RoleService.cs` (`UpdatePermissionsAsync`)
*Evidence:* `RoleManagementTests`

**FS-A-042** · Satisfies URS-A-041
`UserSectionScopeService` returns two section lists. For reading, `Sections.ViewAll` gives all sections; otherwise the user's memberships. For acting on laboratory records (writes), only the user's memberships count. `Sections.ConfigureAll` opens writes to master-data and settings services in every section (specifications, test definitions, method masters, KPI settings, workbook import). GxP records still follow membership.
*Implemented in:* `B/MicroLIMS.Application/Services/UserSectionScopeService.cs`
*Evidence:* `UserSectionScopeServiceTests`, `SectionScopedQueriesPostgresIntegrationTests`, `AdministratorConfigureAllSectionsTests`

### A6. Electronic signatures

**FS-A-043** · Satisfies URS-A-043, URS-A-044, URS-A-045
`ElectronicSignatureService.SignAsync` is called by each action that needs a signature, with the user's password. It refuses when the user is unknown, inactive or locked, or the password is wrong, always with the same text. On success it creates an `ElectronicSignature` with: user id; full-name, username and role-type snapshots; role-name snapshot; the meaning (one of the 32 values in `SignatureMeaning`, of which three are retired and no longer offered); record type and id; comment; source address; and the time (UTC).
*Implemented in:* `B/MicroLIMS.Application/Services/ElectronicSignatureService.cs`, `B/MicroLIMS.Domain/Entities/ElectronicSignature.cs`, `B/MicroLIMS.Domain/Enums/SignatureMeaning.cs`
*Evidence:* `AdminReasonAndSignatureTests`, `SignedRecordPostgresIntegrationTests`

**FS-A-044** · Satisfies URS-A-046
Database triggers `trg_electronicsignatures_immutable` and `trg_electronicsignatures_no_truncate` reject update, delete and truncate on `ElectronicSignatures`.
*Implemented in:* migrations `AddAuditImmutabilityTriggers` and `HardenAppendOnlyAuditTables`
*Evidence:* `AppendOnlyAuditHardeningPostgresTests`

**FS-A-045** · Satisfies URS-A-047
The signature is added to the same database context as the action and saved with it in one transaction (`UnitOfWork`). A wrong password throws before anything is saved, and only the refused-attempt audit row is kept. Where a record needs its own id inside the signature, the id is reserved from the table's sequence first, then the record and signature are saved together.
*Implemented in:* `B/MicroLIMS.Application/Helpers/UnitOfWork.cs`, `IDatabaseSequenceHelper.ReserveIdAsync`
*Evidence:* `UnitOfWorkPostgresIntegrationTests`, `SignedRecordPostgresIntegrationTests`

**FS-A-046** · Satisfies URS-A-048
Each failed signature writes an audit row `SignatureFailed`. When the signer has `security.signature.maxFailedAttempts` failures (3 to 20, default 5) within `security.signature.windowMinutes` (1 to 1440, default 15), the next attempt is refused before the password is checked, with the text "Too many failed signature attempts…", and a `SignatureThrottled` row is written. The signer's login counters are not touched. The refused attempts still count if the surrounding command is rolled back.
*Implemented in:* `ElectronicSignatureService.cs`, `UnitOfWork.cs`
*Evidence:* `AdminReasonAndSignatureTests`, `SignedRecordPostgresIntegrationTests`

**FS-A-047** · Satisfies URS-A-049
`GetTrailAsync` returns a record's signatures oldest first, showing name, username, role name, meaning, time and comment. `GET /api/signatures` (`Signatures.Manage`) lists signatures.
*Implemented in:* `ElectronicSignatureService.cs`, `B/MicroLIMS.API/Controllers/SignaturesController.cs`
*Evidence:* `SignedRecordPostgresIntegrationTests`

**FS-A-048** · Satisfies URS-A-050 · **Not implemented**
A signature points to its record by type and id only. Nothing stores a fingerprint of the record content at signing time. See Annex 1, O5.
*Implemented in:* not applicable
*Evidence:* not applicable

### A7. Audit trail

**FS-A-049** · Satisfies URS-A-051, URS-A-052
`MicroLimsDbContext` overrides `SaveChanges`. It captures every added, modified and deleted entity (except FS-A-052) into `AuditLog` with entity name and id, action, previous and new values as JSON, user and time, in two steps so new rows are written with their real id. The actor is the signed-in user (set by `AuditMiddleware`). Background workers write as a System actor with a process name. Instrument agents write as a System actor with the agent code. Semantic events (for example `USER_ROLE_CHANGED`) add an event id, action code, category, reason and per-field changes.
*Implemented in:* `B/MicroLIMS.Persistence/DbContext/MicroLimsDbContext.cs`, `B/MicroLIMS.API/Middleware/AuditMiddleware.cs`, `B/MicroLIMS.Application/Services/AuditEventService.cs`, `B/MicroLIMS.Domain/Entities/AuditLog.cs`
*Evidence:* `AuditEventServiceTests`, `AgentAuditAttributionTests`, `AuditTrailRecordIdsPostgresIntegrationTests`

**FS-A-050** · Satisfies URS-A-053
Row triggers on `AuditLogs`, `AuditEventChanges`, `ElectronicSignatures`, `SecurityAuditEvents`, `LoginHistories`, `SystemSettingVersions`, `AccessReviews` and `AccessReviewFindings` (and other append-only tables such as `DocumentAcknowledgementRecords`, `WorkflowProfileVersions` and the instrument-ingest tables) raise an error on update and delete. Statement triggers do the same for truncate. The trigger functions are `prevent_audit_modification` and `prevent_audit_truncate`. A table owner can still disable a trigger. Production must therefore run the application under a role that is not the owner (`docs/operations/database-roles-runbook.md`).
*Implemented in:* migrations `AddAuditImmutabilityTriggers`, `AddSecurityAuditEvents`, `HardenAppendOnlyAuditTables`, `AddSystemSettingsRegistry` and `AddAccessReviews`; `docs/operations/database-roles-runbook.md`
*Evidence:* `AppendOnlyAuditHardeningPostgresTests`

**FS-A-051** · Satisfies URS-A-054
Properties marked `[AuditRedacted]` (the password hash of a user and of a password-history row, the reset token hash, the administrator-recovery code hash, and the instrument-agent key hash) are written as `"[REDACTED]"`, so the fact of a change stays visible but not the secret. An architecture test fails when a property whose name looks like a credential is neither marked nor on a one-entry allowlist.
*Implemented in:* `MicroLimsDbContext.cs` (`SerializeValues`), the entity classes
*Evidence:* `AuditCredentialRedactionTests`

**FS-A-052** · Satisfies URS-A-055
`AuditCaptureExcludedTypes` lists the entities that automatic capture skips: `AuditLog`, `AuditEventChange`, `MaterialDocumentAccessLog`, `EquipmentDocumentAccessLog`, `Incident`, `ErrorLog`, `BackgroundJobRun`, `BackupRun`, `RefreshToken`, `NotificationLog`, `InstrumentIngestPayload`, `SecurityAuditEvent`, `SystemSetting`, `SystemSettingVersion`, `AccessReview` and `AccessReviewFinding`. The first group is operational. The remainder have their own record: the security trail, the setting history (FS-A-072) or an explicit audit entry written by their service.
*Implemented in:* `MicroLimsDbContext.cs` (`AuditCaptureExcludedTypes`)
*Evidence:* `NotificationAuditExclusionTests`, `AuditEventServiceTests`

**FS-A-053** · Satisfies URS-A-056
Services set the request's reason on the database context (`CurrentReason`). Capture copies it into `AuditLog.Reason` (up to 2000 characters). Administrative actions (users, roles, sessions, settings, jobs, tests) always set it.
*Implemented in:* `B/MicroLIMS.Application/Abstractions/Persistence/IMicroLimsDbContext.cs`, `MicroLimsDbContext.cs`
*Evidence:* `AdminReasonAndSignatureTests`

**FS-A-054** · Satisfies URS-A-057, URS-A-058, URS-A-062
`POST /api/admin/audit/search` and `GET /api/admin/audit` (permission `Audit.View`) filter by date range, batch, control, media lot, sample reference, strain, cryovial, sample id, test order id, user, entity name, action category, actor type, correlation id and reason. Results are paged by the entry id. A request for more than 1000 rows is cut to 1000 (`MaxTake`). The Audit Trail screen offers the filters and shows the user's current role name.
*Implemented in:* `B/MicroLIMS.API/Controllers/AuditController.cs`, `B/MicroLIMS.Application/Services/AuditSearchService.cs`, `F/modules/auditSearch/`
*Evidence:* `AuditSearchAndTraceabilityTests`, `AuditViewerPagingFilterExportTests`

**FS-A-055** · Satisfies URS-A-059
`POST /api/admin/audit/export` returns a CSV of the filtered result, newest first, up to 50,000 rows. When more rows match, a first line states that the file is truncated. The export writes its own audit entry with the row count. A cell starting with `=`, `+`, `-`, `@`, tab or carriage return is prefixed with an apostrophe so a spreadsheet does not run it.
*Implemented in:* `B/MicroLIMS.Application/Services/AuditExportService.cs`
*Evidence:* `AuditViewerPagingFilterExportTests`

**FS-A-056** · Satisfies URS-A-060
`GET /api/admin/audit/{auditLogId}/traceability` and `GET /api/admin/audit/traceability` return the records related to an entry through the search keys (sample, batch, media lot, strain, cryovial, test order).
*Implemented in:* `AuditController.cs`, `AuditSearchService.cs`
*Evidence:* `AuditSearchAndTraceabilityTests`

**FS-A-057** · Satisfies URS-A-061
The viewer marks audit rows whose entity id is negative, the sign of a temporary key written before the capture was made two-step. The script `scripts/audit/append-negative-entityid-corrections.sql` adds correction events for them. It runs as a dry run by default, only inserts, and can be run again safely.
*Implemented in:* `AuditSearchService.cs`, `scripts/audit/append-negative-entityid-corrections.sql`
*Evidence:* `AuditTrailRecordIdsPostgresIntegrationTests`

**FS-A-058** · Satisfies URS-A-063
Times are stored as UTC. Clocks are read through `TimeProvider` or `LabClock`, never directly. The screens show times in the laboratory time zone, `Lab:TimeZoneId` (Africa/Cairo when not set). A test fails when code reads the system clock directly.
*Implemented in:* `B/MicroLIMS.Application/Services/LabClock.cs`, `F/` date formatting
*Evidence:* `ClockUsageTests`

**FS-A-059** · Satisfies URS-A-064
Editable records carry a version number. The browser sends it in the `If-Match` header. `RecordVersionMiddleware` passes it to the context and `RecordVersion.EnsureCurrent` compares it with the stored version when the record is saved. A mismatch returns HTTP 409 with a message that the record changed, and the user must reload.
*Implemented in:* `B/MicroLIMS.API/Middleware/RecordVersionMiddleware.cs`, `ExceptionMiddleware.cs`, `IVersionedEntity`
*Evidence:* `OptimisticConcurrencyPostgresIntegrationTests`, `StaleFormPostgresIntegrationTests`, `ExceptionMiddlewareStatusTests`

### A8. Security event trail

**FS-A-060** · Satisfies URS-A-065, URS-A-066
`SecurityAuditService.Record` queues a `SecurityAuditEvent` that is saved together with the action that caused it. There are 26 event codes: sign-in success and four failure kinds; refresh token issued, rotated and two rejection kinds; logout; session revoked and tokens purged; password changed, change failed and reset; account disabled, enabled, locked and unlocked; role changed; background job manually triggered; critical-alert test sent; SMTP test sent; storage probe run; maintenance mode changed; and agent authentication failed. Each event holds time, actor (or "system"), target user and username, outcome, reason, metadata and source.
*Implemented in:* `B/MicroLIMS.Shared/Constants/SecurityEventCodes.cs`, `B/MicroLIMS.Application/Services/SecurityAuditService.cs`, entity `SecurityAuditEvent`
*Evidence:* `SecurityAuditTrailTests`

**FS-A-061** · Satisfies URS-A-067
Triggers `trg_securityauditevents_immutable` and `trg_securityauditevents_no_truncate` block update, delete and truncate. The foreign keys to users are `RESTRICT`, so a user referenced by an event cannot be removed.
*Implemented in:* migrations `AddSecurityAuditEvents` and `HardenAppendOnlyAuditTables`
*Evidence:* `AppendOnlyAuditHardeningPostgresTests`

**FS-A-062** · Satisfies URS-A-068
`GET /api/admin/audit/security-events` and `GET /api/admin/audit/login-history` need `System.ViewSecurityAudit`, which is not part of `Audit.View`. The Audit Trail screen shows a Security Trail panel to holders of the permission.
*Implemented in:* `AuditController.cs`, `F/modules/auditSearch/components/SecurityTrailPanel.tsx`
*Evidence:* `SecurityAuditTrailTests`, `AuditViewerPagingFilterExportTests`

### A9. Record archiving

**FS-A-063** · Satisfies URS-A-069, URS-A-070
When a final decision is signed, `RecordArchiveService.ArchiveAsync` renders the record as a PDF, stores it through the file-storage service, and writes an `ArchivedRecord` with: record type and id; the printed document id; file name and path; size; SHA-256 of the bytes; reason; the generating user's id and name; and the time. It is called for sample approval (including OOS outcomes), media release and cryovial approval. No endpoint updates or deletes an archived record, and a code comment forbids adding one. The database does not enforce this with a trigger (O9).
*Implemented in:* `B/MicroLIMS.Application/Services/RecordArchiveService.cs`, `SampleApprovalService.cs`, `MediaReleaseService.cs`, `CryovialService.cs`, `B/MicroLIMS.Domain/Entities/ArchivedRecord.cs`
*Evidence:* `RecordArchiveTests`, `OosWorkflowAndPropagationTests`, `MultiSectionReviewApprovalTests`

**FS-A-064** · Satisfies URS-A-071
`GET /api/archived-records/{id}/download` re-computes the SHA-256 of the stored file and compares it with the saved value. The response carries the header `X-Archive-Integrity: verified` or `FAILED`. The file is returned either way.
*Implemented in:* `B/MicroLIMS.API/Controllers/ArchivedRecordsController.cs`, `RecordArchiveService.cs` (`ReadAsync`)
*Evidence:* `RecordArchiveTests`

**FS-A-065** · Satisfies URS-A-072
`ArchiveAsync` catches rendering and storage errors, logs them and returns nothing. The signed decision stands. The archive can be cut again.
*Implemented in:* `RecordArchiveService.cs`
*Evidence:* `RecordArchiveTests`

### A10. Error monitoring and incidents

**FS-A-066** · Satisfies URS-A-073
`ExceptionMiddleware` answers an unhandled server exception with HTTP 500 and the generic text "An unexpected error occurred." (no internal detail reaches the user) and stores it as an `ErrorLog` row with the correlation identifier from `CorrelationIdMiddleware`. Business-rule refusals are answered as 400 with their own message, and a stale-record save as 409 (FS-A-059). `POST /api/system/client-errors` is anonymous, rate limited and size limited, and stores crash reports from the browser. The correlation id header is exposed to the browser so client and server reports can be matched.
*Implemented in:* `B/MicroLIMS.API/Middleware/ExceptionMiddleware.cs`, `CorrelationIdMiddleware.cs`, `B/MicroLIMS.API/Controllers/SystemController.cs`, entities `ErrorLog` and `Incident`
*Evidence:* `OperationalLoggingTests`, `ExceptionMiddlewareStatusTests`. No dedicated test of the client-error endpoint was found.

**FS-A-067** · Satisfies URS-A-074, URS-A-075, URS-A-076
Related errors are grouped under one `Incident` with first and last seen, status and severity. `GET /api/error-monitoring/incidents` lists and filters, `…/incidents/{id}` opens one, `POST …/resolve` and `…/reopen` change status with a note and the resolver, `POST …/error-logs/{id}/severity-override` changes severity, and `GET …/incidents/export` returns a CSV. All need `System.ViewErrorLog`. The Error Log tab of `/admin` is the screen.
*Implemented in:* `B/MicroLIMS.API/Controllers/ErrorMonitoringController.cs`, `F/modules/errorMonitoring/ErrorMonitoringPage.tsx`
*Evidence:* `OperationalLoggingTests`. No test of the resolve, reopen or export actions was found.

**FS-A-068** · Satisfies URS-A-077
`CriticalAlertWorker` calls `CriticalAlertService` every 60 seconds (minimum 15). For each Critical incident not yet alerted it sends one email to each address in `Alerts.CriticalRecipients` (up to 20; empty means nobody) and stamps the incident. The feature is off unless `ErrorMonitoring:CriticalAlerts:Enabled` is set in the deployment configuration, and it needs the SMTP settings. `POST /api/admin/alerts/test` sends a test alert, needs a reason, is rate limited and records `ADMIN_CRITICAL_ALERT_TEST_SENT`.
*Implemented in:* `B/MicroLIMS.Application/Services/CriticalAlertService.cs`, `B/MicroLIMS.API/BackgroundServices/CriticalAlertWorker.cs`, `AdminIntegrationsController.cs`
*Evidence:* `CriticalAlertTests`, `IntegrationsTests`

**FS-A-069** · Satisfies URS-A-078
`ErrorLogRetentionWorker` deletes `ErrorLog` and `Incident` rows older than `retention.errorLog.lowSeverityDays` (Info and Warning; 30 to 3650, default 180) or `retention.errorLog.highSeverityDays` (Error and Critical; 30 to 3650, default 365). It runs every `retention.errorLog.intervalHours` (1 to 168, default 24). It touches no other table.
*Implemented in:* `B/MicroLIMS.API/BackgroundServices/ErrorLogRetentionWorker.cs`
*Evidence:* None found for the pruning rule. `BackgroundJobRunTests` covers the run record.

**FS-A-070** · Satisfies URS-A-079
`DatabaseHealthMonitorWorker` polls PostgreSQL statistics every 60 seconds. A query running longer than `monitoring.database.slowQueryThresholdMs` (100 to 60000, default 1000) is reported as slow. A query waiting on a lock longer than `monitoring.database.blockedQueryThresholdMs` (500 to 600000, default 5000) is reported as blocked. Findings become incidents.
*Implemented in:* `B/MicroLIMS.API/BackgroundServices/DatabaseHealthMonitorWorker.cs`
*Evidence:* `BackgroundJobRunTests` (run record only)

### A11. System configuration

**FS-A-071** · Satisfies URS-A-080
`SystemSettingDefinitions.All` declares 22 settings: Security 8 (password length and history, lockout attempts and duration, refresh lifetime, reset lifetime, signature attempts and window), Retention 5, Monitoring 2, Alerts 1, Document Control 1 (`DocumentControl.ReassignmentEscalationDays`), System 4 (maintenance mode and message, announcement and its severity) and Access Review 1. Each has a type (integer, boolean, text, email list), a minimum and maximum, a default and a GxP flag. At run time a value comes from the database row first, then from the old configuration key (for deployments that predate the registry), then from the default. A stored value that no longer validates is ignored. `GET /api/admin/settings` lists, `PUT /api/admin/settings/{key}` changes and `GET …/{key}/history` shows the history. All need `System.ConfigureSettings`.
*Implemented in:* `B/MicroLIMS.Application/Services/SystemSettings/`, `B/MicroLIMS.API/Controllers/AdminSettingsController.cs`, `F/modules/admin/SettingsPage.tsx`
*Evidence:* `SystemSettingsRegistryTests`, `SystemSettingsPostgresTests`

**FS-A-072** · Satisfies URS-A-081, URS-A-082
A change needs a reason. For a GxP-flagged setting it also needs the administrator's password (electronic signature, meaning `SystemSettingChanged`). Each change appends a `SystemSettingVersion` row (previous value, new value, who, when, reason, signature) and an audit entry in the Configuration category. Triggers make `SystemSettingVersions` append-only.
*Implemented in:* `B/MicroLIMS.Application/Services/SystemSettings/SystemSettingsService.cs`, migration `AddSystemSettingsRegistry`
*Evidence:* `SystemSettingsRegistryTests`, `SystemSettingsPostgresTests`

**FS-A-073** · Satisfies URS-A-083
Secrets are never settings. The token signing key, SMTP password, storage keys and database connection string are read from the deployment configuration (environment variables) and cannot be edited in the application. The Integrations screen shows the SMTP password always masked.
*Implemented in:* `B/MicroLIMS.API/Extensions/JwtConfiguration.cs`, `B/MicroLIMS.Application/Services/Integrations/IntegrationsService.cs`
*Evidence:* `IntegrationsTests`

**FS-A-074** · Satisfies URS-A-084
`Lab:TimeZoneId` in the deployment configuration sets the laboratory time zone (Africa/Cairo when not set). It drives due dates, incubation times and displayed times. It is not in the registry.
*Implemented in:* `B/MicroLIMS.Application/Services/LabClock.cs`, `appsettings.json`
*Evidence:* `ClockUsageTests`

### A12. System operations

**FS-A-075** · Satisfies URS-A-085, URS-A-086
`BackgroundJobRunRecorder` wraps each worker pass and writes a `BackgroundJobRun` (start, end, outcome, error, items processed). `GET /api/admin/jobs` lists the jobs with their schedule and classification, and `GET /api/admin/jobs/{name}/runs` lists runs. `POST /api/admin/jobs/{name}/run` runs a job now, needs a reason, and is allowed only for jobs classed as housekeeping (`SessionTokenCleanupWorker`, `ErrorLogRetentionWorker`, `DatabaseHealthMonitorWorker`). Each manual run records `ADMIN_BACKGROUND_JOB_TRIGGERED`. All need `System.ManageJobs`.
*Implemented in:* `B/MicroLIMS.Application/Services/BackgroundJobs/`, `B/MicroLIMS.API/Controllers/AdminJobsController.cs`, `F/modules/admin/JobsPage.tsx`
*Evidence:* `BackgroundJobRunTests`

**FS-A-076** · Satisfies URS-A-087, URS-A-088
`GET /api/admin/database/overview` (`System.ViewErrorLog`) returns the database size, the largest tables and the recent `BackupRun` rows. `scripts/DatabaseBackup/backup.sh` writes a `BackupRun` on each backup. `scripts/Restore/restore.sh` restores a backup and is run by an operator. The application keeps no record of a restore test, so that evidence comes from the IQ/OQ restore script.
*Implemented in:* `B/MicroLIMS.API/Controllers/DatabaseOverviewController.cs`, `DatabaseOverviewService`, `scripts/DatabaseBackup/`, `scripts/Restore/`, `F/modules/admin/DatabaseOverviewPage.tsx`
*Evidence:* `DatabaseOverviewServiceTests`, `DatabaseStatsQueryPostgresTests`

**FS-A-077** · Satisfies URS-A-089
The Integrations screen (`System.ConfigureSettings`) shows instrument agents (rotate or revoke through the existing endpoints), SMTP status and a test email, and storage status with a write, read and delete probe. Each test needs a reason, is rate limited, and records a security event (`ADMIN_SMTP_TEST_SENT`, `ADMIN_STORAGE_PROBE_RUN`).
*Implemented in:* `B/MicroLIMS.API/Controllers/AdminIntegrationsController.cs`, `IntegrationsService.cs`, `F/modules/admin/IntegrationsPage.tsx`
*Evidence:* `IntegrationsTests`

**FS-A-078** · Satisfies URS-A-090
Setting `System.MaintenanceMode` (GxP flag, so electronic signature and reason) turns maintenance on and records `ADMIN_MAINTENANCE_MODE_CHANGED`. `MaintenanceModeMiddleware` then answers HTTP 503 with the maintenance message, `Retry-After: 300` and the header `X-Maintenance-Mode: on` to every non-reading request from a signed-in user or an instrument agent. Let through: reading requests; sign-in, sign-out, refresh and change-password; the settings endpoint; and everything under `/api/admin/` for a caller holding `System.ConfigureSettings`. Workers classed as GxP skip their passes while maintenance is on.
*Implemented in:* `B/MicroLIMS.API/Middleware/MaintenanceModeMiddleware.cs`, `BackgroundJobRunRecorder.cs`, `F/modules/admin/MaintenanceSection.tsx`
*Evidence:* `MaintenanceModeTests`

**FS-A-079** · Satisfies URS-A-091
`System.AnnouncementBanner` (text) and `System.AnnouncementSeverity` (`info` or `warning`) set a banner shown on every page, including sign-in. `GET /api/system/status` is anonymous and returns the maintenance state, message and banner.
*Implemented in:* `B/MicroLIMS.API/Controllers/SystemStatusController.cs`, `F/` status banner component
*Evidence:* `MaintenanceModeTests`

**FS-A-080** · Satisfies URS-A-092
`/health` reports that the process is alive. `/health/ready` checks that PostgreSQL is reachable and that the schema is current. Both are anonymous and return no secrets.
*Implemented in:* `B/MicroLIMS.API/Extensions/HealthCheckExtensions.cs`, `DatabaseReadinessHealthCheck`
*Evidence:* `HealthCheckTests`

### A13. Periodic access review

**FS-A-081** · Satisfies URS-A-093, URS-A-094, URS-A-098
`POST /api/admin/access-reviews` (`Access.Review`) builds a snapshot of every user: role, effective permissions, section memberships, last sign-in, the last 12 months of status changes, and flags for segregation-of-duties conflict, dormant (no sign-in for `AccessReview.DormantDays`, 1 to 3650, default 90, or never signed in and older than that) and never signed in. The snapshot is stored as JSON with its SHA-256. A review keeps the dormant threshold it was made with.
*Implemented in:* `B/MicroLIMS.Application/Services/AccessReviewService.cs`, `B/MicroLIMS.API/Controllers/AdminAccessReviewsController.cs`, `F/modules/admin/AccessReviewPage.tsx`
*Evidence:* `AccessReviewTests`, `AccessReviewPostgresTests`

**FS-A-082** · Satisfies URS-A-095
While a review is in draft, `POST …/{id}/findings` and `PUT …/{id}/findings/{findingId}` record findings.
*Implemented in:* `AccessReviewService.cs`, `AdminAccessReviewsController.cs`
*Evidence:* `AccessReviewTests`

**FS-A-083** · Satisfies URS-A-096
`POST …/{id}/sign` needs the reviewer's password (electronic signature, meaning `AccessReviewSigned`) and a conclusion. It is refused when the review concerns the signer's own access (a finding about the signer's account, or the report flagging it), and when the SHA-256 of the stored snapshot no longer matches the saved value.
*Implemented in:* `AccessReviewService.cs` (`SignAsync`)
*Evidence:* `AccessReviewTests`, `AccessReviewPostgresTests`

**FS-A-084** · Satisfies URS-A-097
Triggers `trg_accessreviews_immutable`, `trg_accessreviewfindings_immutable` and the matching truncate triggers keep a signed review unchangeable. `GET …/{id}/export` returns a CSV and writes an audit entry.
*Implemented in:* migration `AddAccessReviews`, `AccessReviewService.cs`
*Evidence:* `AccessReviewPostgresTests`

### A14. Platform protection

**FS-A-085** · Satisfies URS-A-099
Protection in transit is provided by the hosting set-up, not by the software. The application takes the client address from forwarded headers only when the request comes from a configured proxy network, so that rate limits and the login history use the real address. IQ must verify the encrypted connection.
*Implemented in:* deployment (`DEPLOYMENT.md`); `B/MicroLIMS.API/Extensions/` (forwarded-header configuration)
*Evidence:* None in the test suite. IQ.

**FS-A-086** · Satisfies URS-A-100
Every response carries `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY` and `Referrer-Policy: no-referrer`. Responses other than the API explorer also carry `Content-Security-Policy: default-src 'none'; frame-ancestors 'none'`.
*Implemented in:* `B/MicroLIMS.API/Middleware/SecurityHeadersMiddleware.cs`
*Evidence:* `ApiHardeningDefaultsTests`

**FS-A-087** · Satisfies URS-A-101
The CORS policy `Frontend` allows only the origins in the deployment configuration, with any header and method. It exposes the correlation id, content-disposition and maintenance headers to the browser.
*Implemented in:* `B/MicroLIMS.API/Program.cs`
*Evidence:* None found. OQ.

**FS-A-088** · Satisfies URS-A-102
`JwtConfiguration.Resolve` stops the application at start-up when the signing key is missing, shorter than the minimum, or equal to the known development placeholder outside development, or when `Jwt:AccessTokenMinutes` is outside its range. Error messages never contain the key.
*Implemented in:* `B/MicroLIMS.API/Extensions/JwtConfiguration.cs`
*Evidence:* `JwtConfigurationTests`

---

## 5. Traceability: URS to FS (Group A)

<!-- RTM:BEGIN -->
| URS | Priority | Status | FS |
|---|---|---|---|
| URS-A-001 | Must | Implemented | FS-A-001 |
| URS-A-002 | Must | Implemented | FS-A-002 |
| URS-A-003 | Must | Implemented | FS-A-003 |
| URS-A-004 | Must | Implemented | FS-A-004 |
| URS-A-005 | Must | Implemented | FS-A-005 |
| URS-A-006 | Should | Implemented | FS-A-006 |
| URS-A-007 | Must | Implemented | FS-A-007 |
| URS-A-008 | Must | Implemented | FS-A-008 |
| URS-A-009 | Must | Implemented | FS-A-009 |
| URS-A-010 | Must | Implemented | FS-A-010 |
| URS-A-011 | Must | Implemented | FS-A-011 |
| URS-A-012 | Must | Implemented | FS-A-012 |
| URS-A-013 | Must | Implemented | FS-A-013 |
| URS-A-014 | Should | Implemented | FS-A-014 |
| URS-A-015 | Must | Implemented | FS-A-015 |
| URS-A-016 | Must | Implemented | FS-A-016 |
| URS-A-017 | Should | Not implemented | FS-A-017 |
| URS-A-018 | Must | Implemented | FS-A-018 |
| URS-A-019 | Must | Implemented | FS-A-019 |
| URS-A-020 | Must | Implemented | FS-A-020 |
| URS-A-021 | Should | Implemented | FS-A-021 |
| URS-A-022 | Must | Implemented | FS-A-022 |
| URS-A-023 | Should | Implemented | FS-A-023 |
| URS-A-024 | Should | Not implemented | FS-A-024 |
| URS-A-025 | Must | Implemented | FS-A-025 |
| URS-A-026 | Must | Implemented | FS-A-026 |
| URS-A-027 | Must | Implemented | FS-A-027 |
| URS-A-028 | Must | Implemented | FS-A-028 |
| URS-A-029 | Must | Implemented | FS-A-029 |
| URS-A-030 | Should | Implemented | FS-A-030 |
| URS-A-031 | Must | Implemented | FS-A-031 |
| URS-A-032 | Must | Implemented | FS-A-032 |
| URS-A-033 | Must | Implemented | FS-A-033 |
| URS-A-034 | Should | Implemented | FS-A-034 |
| URS-A-035 | Must | Implemented | FS-A-035, FS-A-040 |
| URS-A-036 | Must | Implemented | FS-A-036 |
| URS-A-037 | Must | Implemented | FS-A-037 |
| URS-A-038 | Must | Implemented | FS-A-038 |
| URS-A-039 | Must | Implemented | FS-A-039 |
| URS-A-040 | Must | Implemented | FS-A-041 |
| URS-A-041 | Must | Implemented | FS-A-042 |
| URS-A-042 | Should | Implemented | FS-A-040 |
| URS-A-043 | Must | Implemented | FS-A-043 |
| URS-A-044 | Must | Implemented | FS-A-043 |
| URS-A-045 | Must | Implemented | FS-A-043 |
| URS-A-046 | Must | Implemented | FS-A-044 |
| URS-A-047 | Must | Implemented | FS-A-045 |
| URS-A-048 | Must | Implemented | FS-A-046 |
| URS-A-049 | Should | Implemented | FS-A-047 |
| URS-A-050 | Should | Not implemented | FS-A-048 |
| URS-A-051 | Must | Implemented | FS-A-049 |
| URS-A-052 | Must | Implemented | FS-A-049 |
| URS-A-053 | Must | Implemented | FS-A-050 |
| URS-A-054 | Must | Implemented | FS-A-051 |
| URS-A-055 | Must | Implemented | FS-A-052 |
| URS-A-056 | Must | Implemented | FS-A-053 |
| URS-A-057 | Must | Implemented | FS-A-054 |
| URS-A-058 | Should | Implemented | FS-A-054 |
| URS-A-059 | Should | Implemented | FS-A-055 |
| URS-A-060 | Should | Implemented | FS-A-056 |
| URS-A-061 | Should | Implemented | FS-A-057 |
| URS-A-062 | Must | Implemented | FS-A-054 |
| URS-A-063 | Must | Implemented | FS-A-058 |
| URS-A-064 | Must | Implemented | FS-A-059 |
| URS-A-065 | Must | Implemented | FS-A-060 |
| URS-A-066 | Must | Implemented | FS-A-060 |
| URS-A-067 | Must | Implemented | FS-A-061 |
| URS-A-068 | Must | Implemented | FS-A-062 |
| URS-A-069 | Must | Implemented | FS-A-063 |
| URS-A-070 | Must | Partial | FS-A-063 |
| URS-A-071 | Must | Implemented | FS-A-064 |
| URS-A-072 | Should | Implemented | FS-A-065 |
| URS-A-073 | Must | Implemented | FS-A-066 |
| URS-A-074 | Must | Implemented | FS-A-067 |
| URS-A-075 | Should | Implemented | FS-A-067 |
| URS-A-076 | Could | Implemented | FS-A-067 |
| URS-A-077 | Should | Implemented | FS-A-068 |
| URS-A-078 | Should | Implemented | FS-A-069 |
| URS-A-079 | Could | Implemented | FS-A-070 |
| URS-A-080 | Must | Implemented | FS-A-071 |
| URS-A-081 | Must | Implemented | FS-A-072 |
| URS-A-082 | Must | Implemented | FS-A-072 |
| URS-A-083 | Must | Implemented | FS-A-073 |
| URS-A-084 | Must | Implemented | FS-A-074 |
| URS-A-085 | Should | Implemented | FS-A-075 |
| URS-A-086 | Should | Implemented | FS-A-075 |
| URS-A-087 | Should | Implemented | FS-A-076 |
| URS-A-088 | Must | Partial | FS-A-076 |
| URS-A-089 | Should | Implemented | FS-A-077 |
| URS-A-090 | Should | Implemented | FS-A-078 |
| URS-A-091 | Could | Implemented | FS-A-079 |
| URS-A-092 | Should | Implemented | FS-A-080 |
| URS-A-093 | Must | Implemented | FS-A-081 |
| URS-A-094 | Should | Implemented | FS-A-081 |
| URS-A-095 | Should | Implemented | FS-A-082 |
| URS-A-096 | Must | Implemented | FS-A-083 |
| URS-A-097 | Must | Implemented | FS-A-084 |
| URS-A-098 | Could | Implemented | FS-A-081 |
| URS-A-099 | Must | Deployment control | FS-A-085 |
| URS-A-100 | Should | Implemented | FS-A-086 |
| URS-A-101 | Should | Implemented | FS-A-087 |
| URS-A-102 | Must | Implemented | FS-A-088 |
<!-- RTM:END -->

---

## Annex 1. Observations from the retrospective

These came to light while reading the code. They are not defects in a requirement's wording. Each needs a decision by the System Owner and QA, and some need an OQ test.

| # | Observation | Related | Suggested handling |
|---|---|---|---|
| O1 | **No password expiry.** `PasswordChangedAt` is stored but unused for expiry. | URS-A-017 | Process Owner and QA decide whether site policy needs it. If yes, add a registry setting and a check at sign-in. |
| O2 | **No idle timeout.** A session lasts while it is renewed. The browser polls for notifications every 60 seconds, so an open, unattended tab is likely to stay signed in. To be confirmed by an OQ test. | URS-A-024 | Decide the idle period. Until built, mitigate by a workstation-lock rule in the user-access SOP. |
| O3 | **Username uniqueness is checked in application code only.** No unique index on `Users.Username` was found in the model or the migrations. Usernames can also be edited. Two simultaneous requests could create duplicates. | URS-A-001, URS-A-026 | Add a unique index (case-insensitive) after checking existing data. Decide whether usernames may change. |
| O4 | **A re-used refresh token is refused but not treated as theft.** The other sessions of that user stay valid. | URS-A-019 | Optional hardening: end the user's sessions when a revoked token is presented. |
| O5 | **Signatures are not bound to record content.** Mitigated by the audit trail and by locks on signed records. | URS-A-050 | Decide: accept with documented mitigation, or add a content fingerprint to signatures. |
| O6 | **The database owner can disable triggers.** The audit protection depends on running the application under a non-owner role. | URS-A-053 | Deployment control. Verify at IQ against `docs/operations/database-roles-runbook.md`. |
| O7 | **Audit rows written before the redaction control contain password hashes** (deviation DEV-2026-AUD-001). A migration forced every user to change their password and ended all sessions. Instrument-agent keys must be rotated by an administrator. | URS-A-054 | Confirm the deviation is closed and the agent keys are rotated, before go-live. |
| O8 | **The correction script for negative entity ids has not been run** on a production database. Detection exists. | URS-A-061 | Run it in the production environment once it exists, and file the output. |
| O9 | **Archived records are append-only by convention.** No database trigger protects `ArchivedRecords`, unlike audit entries and signatures. The fingerprint check on download is the safeguard. | URS-A-070 | Consider a trigger like the audit tables. Cover the download check in OQ. |
| O10 | **Evidence gaps.** No dedicated automated test was found for: the client-error endpoint, the error-log retention rule, resolve/reopen/export of incidents, the CORS restriction, the Error Log screen. | URS-A-073, -074, -078, -101 | Cover in OQ. |
| O11 | **Older documents are out of date.** ML-SYS-SD-001 says 48 permissions and describes a cloud deployment. This baseline has 65 permissions and the URS is hosting-neutral. ML-SYS-VP-001 section 10 (risk R1) and section 5 (supplier assessments) assume free-tier cloud hosting. | — | Update SD-001 and VP-001 before approval. |

## Revision history

| Version | Date | Author | Change |
|---|---|---|---|
| 0.1 | 2026-11-02 | AI-assisted draft for the Validation Lead | First issue: Group A (88 entries). |

## 6. Approvals

| Role | Name | Signature | Date |
|---|---|---|---|
| Author (Developer) | | | |
| System Owner | | | |
| Process Owner — Microbiology | | | |
| Process Owner — Physicochemical | | | |
| Quality Assurance | | | |
