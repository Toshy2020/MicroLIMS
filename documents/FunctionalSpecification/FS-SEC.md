# FS-SEC Functional Specification

| Field | Value |
|---|---|
| Document number | FS-SEC |
| Title | Authentication and session security functional specification |
| Version | 0.1 |
| Status | Draft |
| Author | Claude (AI agent), draft from code |
| Reviewer | To be assigned by QA |
| Approver | To be assigned by QA |
| Effective date | Set when Approved |

## Change history

| Version | Date | Author | Change |
|---|---|---|---|
| 0.1 | 2026-11-12 | Claude (AI agent), draft from code | First draft, written from the code as built. |

## Scope and reading notes

- Settings named below (`security.lockout.maxFailedAttempts` and so on) are read through the settings reader from the settings registry. Each has a default in the code and an allowed range (see `SystemSettingDefinitions.All`). An administrator changes them in FS-ADM-008. The constants `MaxFailedAttempts`, `LockDuration`, `PasswordHistoryLimit` and others at the top of `AuthenticationService` exist in the class but the methods read below use the settings.
- Values of secrets (the token signing key, mail passwords, stored hashes) are not part of this document. Only the fact that tokens and codes are stored as SHA-256 hash text and passwords as BCrypt hashes is stated.
- `AdminPasswordRecoveryService` is called from `UserController` (create) and `AuthenticationController` (confirm). Both steps are described here in FS-SEC-008.
- The access token lifetime comes from configuration `Jwt:AccessTokenMinutes` (default 15, maximum 480, in `JwtConfiguration`). The program start-up order of the rate limiter and authentication was not read.
- Nothing was run.

## Functions

### FS-SEC-001
**Satisfies:** URS-SEC-001, URS-SEC-002, URS-SEC-010
**Function:** Sign in (`POST api/auth/login`, anonymous, rate-limited policy `authentication`). Inputs: user name, password; IP address and user-agent are read from the request. The call runs in one unit of work. Unknown user name: a security event `AUTH_LOGIN_FAILED_UNKNOWN_USER` and a sign-in history row are written, a dummy BCrypt check is done to equalise time, and the generic message "Invalid username or password." is returned (HTTP 401). For an existing user the password is always checked first, whatever the account state. Locked account (`IsLockedAt`): event `AUTH_LOGIN_FAILED_ACCOUNT_LOCKED`, history row "Account locked", same generic message; the failure counter is not increased. Inactive account or wrong password: the failure counter on the user is increased by one; event `AUTH_LOGIN_FAILED_ACCOUNT_DISABLED` or `AUTH_LOGIN_FAILED_BAD_CREDENTIALS`; when the counter reaches the setting `security.lockout.maxFailedAttempts` (default 5, range 3 to 20) the user's `LockedUntil` is set to now plus `security.lockout.durationMinutes` (default 15, range 1 to 1440), all active refresh tokens of the user are revoked, and event `AUTH_ACCOUNT_LOCKED` is written with the system as actor and the number of revoked sessions. An inactive account with the right password therefore also counts failures and can lock. The history row names the reason ("Wrong password" or "Account inactive"). Success: counter set to 0, lock cleared, last login time set from the injected clock, history row written, an access token issued (see FS-SEC-003 for the claims; an account with no role gets no token and the sign-in is rolled back), events `AUTH_LOGIN_SUCCESS` and `AUTH_REFRESH_TOKEN_ISSUED` written, and a refresh token issued. The result carries the "must change password" flag and the role name. A locked account is unlocked by time passing, by an administrator (FS-ADM-005) or by a completed recovery (FS-SEC-008). The failure counter is not reset when the lock time passes (see CONF-036).
**Code:** `backend/MicroLIMS.Application/Services/AuthenticationService.cs` — `LoginAsync`, `LoginCoreAsync`, `RecordLoginAsync`; `backend/MicroLIMS.API/Controllers/AuthenticationController.cs` — `Login`
**Verified by:** reading (not run)
**Conflicts:** CONF-036, CONF-037

### FS-SEC-002
**Satisfies:** URS-SEC-003
**Function:** Renew a session (`POST api/auth/refresh`, anonymous, rate-limited policy `token-refresh`). Input: the refresh token text. The service hashes it with SHA-256 and looks up the stored row. Unknown, revoked or expired token (`IsActiveAt`): event `AUTH_REFRESH_REJECTED_INVALID_TOKEN` (the token value is not recorded) and the message "Refresh token is invalid or expired." (401). Token found but the user is missing, inactive or locked: event `AUTH_REFRESH_REJECTED_ACCOUNT_BLOCKED` and the same message. Otherwise the used token is revoked, its last-used time is set, event `AUTH_REFRESH_TOKEN_ROTATED` is written, a new refresh token is stored with expiry now plus `security.refreshToken.lifetimeDays` (default 7, range 1 to 30), the IP address and user agent (cut to 64 and 256 characters), and a new access token is issued with the user's current role and permissions. A revoked token that is presented again is refused; the code does not revoke the other tokens of that user in that case. The token text is a random 32-byte value in base 64; only its hash is stored.
**Code:** `backend/MicroLIMS.Application/Services/AuthenticationService.cs` — `RefreshAsync`, `RefreshCoreAsync`, `IssueRefreshTokenAsync`; `backend/MicroLIMS.Domain/Entities/RefreshToken.cs` — `IsActiveAt`
**Verified by:** reading (not run)

### FS-SEC-003
**Satisfies:** URS-SEC-004
**Function:** Check the access token on every authenticated request. The token is issued with the user ID, the role type, the role ID, the role's permission version and the permission codes of the role (`IssueAccessTokenAsync`). On each request `AccessTokenRevalidator.GetRejectionReasonAsync` reads the account and refuses the token (HTTP 401; the reason is logged, not returned) when: the user ID or issue time is missing; the account does not exist, is inactive, or has `LockedUntil` later than now; the role type in the token differs from the account's role type; the role ID or permission version claim is missing or differs from the account's current role ID or the role's `PermissionVersion`; or the password was changed after the token was issued (compared in whole seconds). The client then calls refresh (FS-SEC-002) to get a token with current rights. Permission checks on endpoints read the permission claims in the token and no database (`PermissionAuthorizationHandler`); the revalidation above is what ends the life of out-of-date claims. This check uses `DateTime.UtcNow` for the lock comparison, not the injected clock. Ending a user's sessions (FS-SEC-011) revokes only refresh tokens, so an access token that is still valid in the above sense works until it expires. Token validation allows 30 seconds of clock skew (`JwtAuthenticationExtensions`).
**Code:** `backend/MicroLIMS.API/Authorization/AccessTokenRevalidator.cs` — `GetRejectionReasonAsync`; `backend/MicroLIMS.API/Authorization/PermissionAuthorizationHandler.cs` — `HandleRequirementAsync`; `backend/MicroLIMS.Application/Services/AuthenticationService.cs` — `IssueAccessTokenAsync`
**Verified by:** reading (not run)

### FS-SEC-004
**Satisfies:** URS-SEC-009
**Function:** Force a password change. `MustChangePasswordFilter` runs before every MVC action for authenticated callers. If the action is `ChangePassword`, `Me` or `Logout` of `AuthenticationController`, it passes. Otherwise it reads `MustChangePassword` for the user from the database (not from the token). When true it returns HTTP 403 with the message "Your password must be changed before you can use MicroLIMS." and does not run the action. The flag is set when an administrator creates a user (FS-ADM-001) or forces a change (FS-ADM-005), and cleared when the password is changed, reset or recovered. Endpoints that are not MVC actions (for example middleware) are not covered by this filter.
**Code:** `backend/MicroLIMS.API/Filters/MustChangePasswordFilter.cs` — `OnActionExecutionAsync`
**Verified by:** reading (not run)

### FS-SEC-005
**Satisfies:** URS-SEC-005
**Function:** Password rules and history, applied when a password is changed or reset (`ValidateAndApplyNewPasswordAsync`). `PasswordPolicy.Validate` returns every failed rule: length below the setting `security.password.minimumLength` (default 8, range 8 to 64), no upper-case letter, no lower-case letter, no digit, no character that is not a letter or digit. The four character-class rules are fixed in code and cannot be configured. Any failure refuses the change with all messages. Then the new password is compared with the stored hashes of the user's most recent passwords, the number being the setting `security.password.historyLimit` (default 5, range 1 to 24); a match refuses the change. If accepted: a BCrypt hash is stored, `PasswordChangedAt` is set, `MustChangePassword` is cleared, a `PasswordHistory` row is added, older history rows beyond the limit are removed, all active refresh tokens of the user are revoked and, if any were revoked, event `AUTH_SESSION_REVOKED` is written. The caller saves. The same policy method is called when an administrator creates a user (FS-ADM-001) and in the recovery flow (FS-SEC-008). The recovery flow does not use the history setting (see CONF-038).
**Code:** `backend/MicroLIMS.Application/Services/AuthenticationService.cs` — `ValidateAndApplyNewPasswordAsync`; `backend/MicroLIMS.Shared/Validation/PasswordPolicy.cs` — `Validate`
**Verified by:** reading (not run)
**Conflicts:** CONF-037, CONF-038

### FS-SEC-006
**Satisfies:** URS-SEC-006, URS-SEC-012
**Function:** Change own password (`POST api/auth/change-password`; any signed-in user; rate-limited policy `password-change`, per user, default 5 per 900 seconds). Inputs: current password, new password. The user is the one in the token. If the user is missing or the current password does not verify, event `AUTH_PASSWORD_CHANGE_FAILED` is written and the call returns "Current password is incorrect." (400). Otherwise FS-SEC-005 is applied, event `AUTH_PASSWORD_CHANGED` is written with actor and target the same user, and the data is saved. A rule failure from FS-SEC-005 is thrown as a business-rule error. No electronic signature is involved. This method does not use a unit of work.
**Code:** `backend/MicroLIMS.Application/Services/AuthenticationService.cs` — `ChangePasswordAsync`; `backend/MicroLIMS.API/Controllers/AuthenticationController.cs` — `ChangePassword`
**Verified by:** reading (not run)

### FS-SEC-007
**Satisfies:** URS-SEC-007, URS-SEC-012
**Function:** Reset a forgotten password. Request (`POST api/auth/password-reset/request`, anonymous, policy `authentication`): input user name. The service creates a random token, stores its SHA-256 hash with expiry now plus `security.passwordReset.lifetimeMinutes` (default 60, range 5 to 1440) and, if the user has an email address, emails the raw token (not a link) with the expiry time; without an email address a warning is logged and nothing is sent. For an unknown user name the service throws a business-rule error, and the controller catches it; in all cases the controller returns the same message "If that account exists, a reset link has been generated." No security event and no sign-in history is written for the request. Confirm (`POST api/auth/password-reset/confirm`, anonymous, policy `authentication`): inputs token, new password. The hash is looked up; an unknown, used or expired token returns "Reset token is invalid or expired." (400). Otherwise FS-SEC-005 is applied to the token's user, the token is marked used, event `AUTH_PASSWORD_RESET` is written (no actor) and the data is saved. The confirm step does not check that the account is active or unlocked, and does not clear the failure counter or the lock. The same request method is used by the administrator-started reset (FS-ADM-005).
**Code:** `backend/MicroLIMS.Application/Services/AuthenticationService.cs` — `RequestPasswordResetAsync`, `ConfirmPasswordResetAsync`; `backend/MicroLIMS.API/Controllers/AuthenticationController.cs` — `RequestPasswordReset`, `ConfirmPasswordReset`
**Verified by:** reading (not run)
**Conflicts:** CONF-036, CONF-037

### FS-SEC-008
**Satisfies:** URS-SEC-008, URS-SEC-012
**Function:** Administrator-assisted recovery. Create (`POST api/users/{id}/admin-password-recovery`; permission `Users.Manage`): inputs target user ID, reason. Refused when the acting user is the target ("System Administrators cannot initiate admin-assisted password recovery for their own account."), when the reason is blank, when the user does not exist or when the user is inactive. Earlier pending requests of that user are set to Expired. A 12-character code from a 32-character set (format XXXX-XXXX-XXXX) is made from random bytes; only its SHA-256 hash is stored, with the creator, the reason and an expiry of 15 minutes after creation (fixed in the code). The plain code is returned once in the response. An `AuditLog` row `ADMIN_PASSWORD_RECOVERY_REQUESTED` is written by hand (it names the target and reason, not the code). Confirm (`POST api/auth/admin-password-recovery/confirm`, anonymous, policy `authentication`): inputs user name, code, new password. Blank inputs, an unknown user, an inactive user and "no pending request" give the message "Invalid or expired recovery code." (the last also writes an `AuditLog` row `ADMIN_PASSWORD_RECOVERY_FAILED`). If the request has expired: it is set to Expired, a row `ADMIN_PASSWORD_RECOVERY_EXPIRED` is written and the message is "Recovery code has expired. Please request a new recovery code." A wrong code: the attempt counter on the request is increased and a row `ADMIN_PASSWORD_RECOVERY_FAILED` is written; at 5 failed attempts (fixed in the code) the request is set to FailedLimitExceeded, a row `ADMIN_PASSWORD_RECOVERY_EXPIRED` is written and the message says the maximum was exceeded; otherwise the message is "Invalid recovery code." A right code: the new password is checked with `PasswordPolicy.Validate` (minimum length from the setting) and against the last 5 password hashes (fixed in the code); then the hash is replaced, `PasswordChangedAt` set, `MustChangePassword` cleared, the failure counter and lock cleared, a history row added (older history rows are not removed here), the request set to Used, all active refresh tokens revoked, and a row `ADMIN_PASSWORD_RECOVERY_USED` written. No security event (`SecurityAuditEvent`) is written in this flow, and no electronic signature is used. The failure rows are attributed to the target user, not to the caller.
**Code:** `backend/MicroLIMS.Application/Services/AdminPasswordRecoveryService.cs` — `CreateRecoveryRequestAsync`, `ConfirmRecoveryAsync`, `GenerateRecoveryCode`, `HashRecoveryCode`; `backend/MicroLIMS.API/Controllers/AuthenticationController.cs` — `ConfirmAdminPasswordRecovery`; `backend/MicroLIMS.API/Controllers/UserController.cs` — `CreateAdminPasswordRecovery`
**Verified by:** reading (not run)
**Conflicts:** CONF-038, CONF-039

### FS-SEC-009
**Satisfies:** URS-SEC-003, URS-SEC-010
**Function:** Sign out (`POST api/auth/logout`; any signed-in user). Input: optional refresh token. If a token is given and belongs to the caller (the lookup is limited to the caller's rows), that one token is revoked (kept if already revoked), event `AUTH_LOGOUT` is written with the reason "Single session ended." If no token is given, or it is not found for the caller, all active refresh tokens of the caller are revoked and the event states the count. The access token itself is not cancelled; it ends by expiry. Allowed even when the password must be changed (FS-SEC-004) and in maintenance mode.
**Code:** `backend/MicroLIMS.Application/Services/AuthenticationService.cs` — `LogoutAsync`, `RevokeAllRefreshTokensAsync`; `backend/MicroLIMS.API/Controllers/AuthenticationController.cs` — `Logout`
**Verified by:** reading (not run)

### FS-SEC-010
**Satisfies:** URS-SEC-010
**Function:** Write security events (`SecurityAuditService.Record`). The method adds a `SecurityAuditEvent` to the database context and does not save; the calling method saves it in its own transaction. Fields: time from the injected clock, event code (cut to 100 characters), outcome, actor user, a flag for system actor, target user and name, correlation text, IP address, user agent, request path and source taken from the request context (or empty, source "System", when no request is running), reason (cut to 1000), and optional metadata text. The codes are constants in `SecurityEventCodes`. The class comment says nothing here may hold a credential, and the fields given are limited to the ones above. `SecurityAuditEvent` is excluded from automatic audit capture. The sign-in history (`LoginHistories`) is written separately by `AuthenticationService.RecordLoginAsync`. Both tables are in the append-only trigger list (see FS-AUD-004). Events are written for sign-in, refresh, sign-out, password change, password reset, session revocation, account enable/disable/lock/unlock, role change and maintenance mode change. They are not written for the administrator recovery flow (FS-SEC-008), for forced password change, for user creation, or for signature failures (those use `AuditLogs`).
**Code:** `backend/MicroLIMS.Application/Services/SecurityAuditService.cs` — `Record`; `backend/MicroLIMS.Shared/Constants/SecurityEventCodes.cs` — `SecurityEventCodes`
**Verified by:** reading (not run)

### FS-SEC-011
**Satisfies:** URS-SEC-011
**Function:** Sessions of a user (`GET api/admin/users/{id}/sessions` and `POST api/admin/users/{id}/sessions/revoke`; permission `Users.Manage`). List: returns the user's refresh tokens that are not revoked and not expired: ID, created time, IP, user agent, last used and expiry; the token hash is not returned. Unknown user gives 404. Revoke: input reason (required, up to 500 characters). All active refresh tokens of the user are revoked, event `AUTH_SESSION_REVOKED` is written with actor, target, reason and count, an `AuditLog` row `USER_SESSIONS_REVOKED` is written by hand, and the count is returned. Access tokens already issued stay valid until expiry or until FS-SEC-003 refuses them (the code comment says so). No electronic signature is used. No stale-form version check is made (the method does not call `RecordVersion.EnsureCurrent`). The administrator can revoke their own sessions; no check prevents it. These two endpoints are also part of the user administration set in FS-ADM.
**Code:** `backend/MicroLIMS.API/Controllers/AdminUserSessionsController.cs` — `List`, `Revoke`; `backend/MicroLIMS.Application/Services/UserService.cs` — `GetActiveSessionsAsync`, `RevokeSessionsAsync`
**Verified by:** reading (not run)

### FS-SEC-012
**Satisfies:** URS-SEC-012
**Function:** Rate limits. Four named policies are defined with fixed windows: `authentication` per client IP (default 20 requests per 60 seconds; used by sign-in, reset request, reset confirm and recovery confirm), `token-refresh` per client IP (default 60 per 60 seconds), `password-change` per signed-in user or the IP if no user (default 5 per 900 seconds), and `agent` per instrument agent (default 120 per 60 seconds). Limits and windows can be overridden by configuration keys under `RateLimiting:`. A refused request gets HTTP 429, a `Retry-After` header and a message. The client IP is the address as resolved after forwarded headers. The partition key is never the host header. Whether the limiter runs after authentication in the pipeline was not checked here.
**Code:** `backend/MicroLIMS.API/Extensions/RateLimitingExtensions.cs` — `AddMicroLimsRateLimiting`, `ClientIp`, `FixedWindow`; `backend/MicroLIMS.API/Controllers/AuthenticationController.cs` — `Login`
**Verified by:** reading (not run)
