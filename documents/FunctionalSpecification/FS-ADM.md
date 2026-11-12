# FS-ADM Functional Specification

| Field | Value |
|---|---|
| Document number | FS-ADM |
| Title | System administration functional specification |
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

- Three controllers are covered: `UserController` (route `api/users`, permission `Users.Manage`), `RoleController` (route `api/roles`, permission `Roles.Manage`) and `AdminSettingsController` (route `api/admin/settings`, permission `System.ConfigureSettings`). The session endpoints of `AdminUserSessionsController` are in FS-SEC-011.
- Every administrative action in `UserService` and `RoleService` takes a reason through `AdminActionReason.Require`: it must not be blank and must not be longer than 500 characters. The refusal is a business-rule error (HTTP 400).
- Where the code uses an electronic signature, it signs before the first change, using `ElectronicSignatureService` (see FS-SIG). A wrong password stops the call and nothing changes.
- None of these services checks a laboratory section. User and role administration is not tied to a laboratory section in the code read.
- The stale-form check (`RecordVersion.EnsureCurrent` with the `If-Match` header) is used in only two places here: `UserService.UpdateProfileAsync` and `RoleService.UpdateAsync`. It runs only when the client sends the header. All other methods below have no stale-form version check. Overlapping saves of one row are still refused by the automatic `xmin` concurrency token, because `User` and `Role` carry a version column.
- A user change is audited in two ways: the automatic capture of the `User` row (password hash redacted, see FS-AUD-002) and, in most methods, a hand-made `AuditLog` row with an action name. Both are saved in one `SaveChanges`.
- `UserSectionScopeService`, `AccessReviewService`, `MaintenanceStatus` and `EmailAddressList` were not read for this document.
- Nothing was run.

## Functions

### FS-ADM-001
**Satisfies:** URS-ADM-001, URS-ADM-010
**Function:** Create a user (`POST api/users`; `Users.Manage`). Inputs: full name, user name, password, role ID, optional email and job title, reason. Refused when the reason is blank, user name or full name is blank, the password fails `PasswordPolicy.Validate` (minimum length from the setting, plus the fixed character classes; see FS-SEC-005), the user name already exists (exact match, no case folding in this method) or the role ID does not exist. The password is stored as a BCrypt hash, `MustChangePassword` is set, and the user is saved. The new user's active flag takes the entity default (active). No check is made on which role may be assigned; a user can be created with any role, including a role that holds user or role administration. No electronic signature is required. No email format check is made. The change is audited only by the automatic capture of the new `User` row (with the reason); no hand-made row and no security event is written. The list (`GET api/users`, `status` of active, inactive or all; anything else is refused) and the single-user read (`GET api/users/{id}`) return `UserDto` including the version, the lock state and the role; the list has no paging.
**Code:** `backend/MicroLIMS.Application/Services/UserService.cs` — `CreateAsync`, `GetAllAsync`, `GetByIdAsync`; `backend/MicroLIMS.API/Controllers/UserController.cs` — `Create`, `GetAll`, `GetById`
**Verified by:** reading (not run)
**Conflicts:** CONF-040

### FS-ADM-002
**Satisfies:** URS-ADM-002, URS-ADM-010
**Function:** Edit a profile and an email (`PUT api/users/{id}` and `PUT api/users/{id}/email`; `Users.Manage`). Profile inputs: full name, user name, email, job title, reason. The user must exist. For the profile edit, `RecordVersion.EnsureCurrent` is called: if the client sent an `If-Match` version and it differs from the user's version, a concurrency error is raised and nothing is saved; with no header the check passes. Refused when full name or user name is blank or the new user name is taken by another user. The old and new name, user name, email and job title are written in a hand-made `AuditLog` row `USER_DATA_UPDATED` (with reason, actor and time from the injected clock); the automatic capture adds its own row. The email-only call checks the reason and the user, sets the email and saves; it has no stale-form version check, no hand-made audit row (only the automatic row with the reason) and no email format check. No signature. The role and the active flag cannot be changed with these two calls.
**Code:** `backend/MicroLIMS.Application/Services/UserService.cs` — `UpdateProfileAsync`, `UpdateEmailAsync`; `backend/MicroLIMS.API/Controllers/UserController.cs` — `UpdateProfile`, `UpdateEmail`
**Verified by:** reading (not run)

### FS-ADM-003
**Satisfies:** URS-ADM-003, URS-ADM-010
**Function:** Change a user's role (`PUT api/users/{id}/role`; `Users.Manage`). Inputs: new role ID, reason, password. Runs in one unit of work. Refused when the reason is blank, when the acting user is the target ("System Administrators cannot modify their own role."), when the user or the role does not exist, or when `AdministrationContinuityGuard` finds that after the change no active user would hold `Users.Manage` or no active user would hold `Roles.Manage` (permissions are counted through the role, not the role type; locked users still count). Then the administrator signs with their own password, meaning `UserRoleChanged`, bound to entity `User` and the target ID, with the old and new role names and the reason as the comment; the IP address is stored. Then the role is changed, a security event `AUTH_ROLE_CHANGED` is written (actor, target, reason, old and new role names), and a hand-made `AuditLog` row `USER_ROLE_CHANGED` is written. No session is revoked; the old access token stops working at the next request because the role ID claim no longer matches (FS-SEC-003). No stale-form version check is made. No check is made that the new role is suitable for the user, and no section scope applies.
**Code:** `backend/MicroLIMS.Application/Services/UserService.cs` — `ChangeRoleAsync`, `ChangeRoleCoreAsync`; `backend/MicroLIMS.Application/Services/AdministrationContinuityGuard.cs` — `EnsureAdministrableAfterAsync`; `backend/MicroLIMS.API/Controllers/UserController.cs` — `ChangeRole`
**Verified by:** reading (not run)
**Conflicts:** CONF-040

### FS-ADM-004
**Satisfies:** URS-ADM-004, URS-ADM-010
**Function:** Enable or disable a user (`PUT api/users/{id}/status`, and the older `PUT api/users/{id}/deactivate`, which uses the fixed reason "Deactivated via legacy endpoint"; `Users.Manage`). Inputs: active flag, reason. Refused when the reason is blank, when an administrator disables their own account, or when the user does not exist. For a disable, the continuity guard must pass (FS-ADM-003). The flag is set; on a change the deactivated time is set (disable) or cleared (enable). On a disable, all active refresh tokens are revoked in the same save; an enable revokes nothing. A security event (`AUTH_ACCOUNT_ENABLED` or `AUTH_ACCOUNT_DISABLED`, with the number of revoked sessions) and a hand-made `AuditLog` row (`USER_ENABLED` or `USER_DISABLED`) are written. No signature and no stale-form version check. The re-enable does not clear a lock or the failure counter. The method does not check that the status really changes; it can be called with the value the user already has.
**Code:** `backend/MicroLIMS.Application/Services/UserService.cs` — `SetStatusAsync`; `backend/MicroLIMS.API/Controllers/UserController.cs` — `SetStatus`, `Deactivate`
**Verified by:** reading (not run)

### FS-ADM-005
**Satisfies:** URS-ADM-005, URS-ADM-010
**Function:** Unlock, force a password change, start a reset (`PUT api/users/{id}/unlock`, `PUT api/users/{id}/force-password-change`, `POST api/users/{id}/password-reset`; `Users.Manage`). Each needs a reason and an existing user. Unlock: sets the failure counter to 0 and clears `LockedUntil`; writes security event `AUTH_ACCOUNT_UNLOCKED` and an `AuditLog` row `USER_UNLOCKED` with the old counter and lock time. Force change: sets `MustChangePassword` and writes `FORCE_PASSWORD_CHANGE_SET`; no security event, and no session is revoked. Start a reset: runs `RequestPasswordResetAsync` for the user (FS-SEC-007), then writes `ADMIN_PASSWORD_RESET_REQUESTED` with whether the user has an email address. The response text is "Password reset instructions sent to user's email." even when the user has no email address on file (the reset code then logs a warning only). An administrator can use these calls on their own account; no check prevents it. The separate recovery-code flow is in FS-SEC-008. No signature and no stale-form version check in any of the three.
**Code:** `backend/MicroLIMS.Application/Services/UserService.cs` — `UnlockUserAsync`, `ForcePasswordChangeAsync`, `InitiatePasswordResetAsync`; `backend/MicroLIMS.API/Controllers/UserController.cs` — `Unlock`, `ForcePasswordChange`, `InitiatePasswordReset`
**Verified by:** reading (not run)

### FS-ADM-006
**Satisfies:** URS-ADM-006, URS-ADM-010
**Function:** Roles list, create, rename, delete (`GET api/roles`, `GET api/roles/{id}`, `POST api/roles`, `PUT api/roles/{id}`, `DELETE api/roles/{id}`; `Roles.Manage`). Create: inputs name, description, base role type, reason; runs in one unit of work. The name must not be blank; no check for a duplicate name is made in this method. Before anything is saved, the grants of the built-in role of the base type are checked with `SegregationOfDutiesRules.FindConflicts`; a conflict is refused (HTTP 409). The role is created as a non-built-in, active role. It then receives a copy of the permission grants of the built-in role of that base type. No signature. Rename (`UpdateAsync`): needs reason and a name; `RecordVersion.EnsureCurrent` is called (client-supplied `If-Match` version); name and description are saved; no signature. Delete (`DeleteAsync`): needs reason; refused for a built-in role, and for a role assigned to any user (the count is in the message); then the administrator signs with their own password (meaning `RoleDeleted`, bound to entity `Role` and the role ID), the role's grants and the role are deleted. No stale-form version check on delete. Create and rename are audited by automatic capture (with reason); delete by automatic capture of the role and each removed grant. Hidden permission codes (`PermissionConstants.Hidden`) are left out of the lists the screen reads.
**Code:** `backend/MicroLIMS.Application/Services/RoleService.cs` — `CreateAsync`, `CreateCoreAsync`, `UpdateAsync`, `DeleteAsync`, `DeleteCoreAsync`, `GetAllAsync`, `GetByIdAsync`; `backend/MicroLIMS.API/Controllers/RoleController.cs` — `Create`, `Update`, `Delete`
**Verified by:** reading (not run)

### FS-ADM-007
**Satisfies:** URS-ADM-007, URS-ADM-010
**Function:** Set the permissions of a role (`PUT api/roles/{id}/permissions`; `Roles.Manage`). Inputs: the full list of permission codes, reason, password. Runs in one unit of work. Refused when the reason is blank, the role does not exist, the acting user holds this role ("You cannot change the permissions of your own role."), the list contains a hidden code, the list contains an unknown code, the list holds a pair of one administration code (`Users.Manage`, `Roles.Manage`) and one code in the list of laboratory execution and approval codes in `SegregationOfDutiesRules` (HTTP 409 with the pairs named), or `AdministrationContinuityGuard` finds that no active user would keep `Users.Manage` or `Roles.Manage`. Then the administrator signs (meaning `RolePermissionsChanged`, bound to entity `Role` and the role ID). Hidden grants already on the role are kept. Missing grants are added and extra grants are removed, each with its own save. When anything changed, the role's `PermissionVersion` is increased, which makes tokens issued before the change fail (FS-SEC-003). A hand-made `AuditLog` row `ROLE_PERMISSIONS_UPDATED` stores the old set, the new set, the granted codes and the revoked codes; automatic capture adds a row for each grant or revoke. No stale-form version check is made. The two sets of codes used for the conflict test are fixed in code (`SegregationOfDutiesRules`); the roles screen reads them from `GET api/roles/sod-rules`. The conflict test is applied to the list being saved, so a role that already breaks the rule can still be saved if the new list removes the conflict.
**Code:** `backend/MicroLIMS.Application/Services/RoleService.cs` — `UpdatePermissionsAsync`, `UpdatePermissionsCoreAsync`, `GetSodRules`; `backend/MicroLIMS.Shared/Constants/SegregationOfDutiesRules.cs` — `FindConflicts`; `backend/MicroLIMS.Application/Services/PermissionService.cs` — `GrantAsync`, `RevokeAsync`; `backend/MicroLIMS.API/Controllers/RoleController.cs` — `UpdatePermissions`, `GetSodRules`
**Verified by:** reading (not run)
**Conflicts:** CONF-040

### FS-ADM-008
**Satisfies:** URS-ADM-008, URS-ADM-010
**Function:** System settings (`GET api/admin/settings`, `PUT api/admin/settings/{key}`, `GET api/admin/settings/{key}/history`; `System.ConfigureSettings`). The registry is a list in code (`SystemSettingDefinitions.All`): key, category, type (integer, boolean, string, email list), default, unit, minimum, maximum, allowed values and a GxP-relevant flag. Settings flagged GxP-relevant are the password, lockout, refresh token, reset token and signature settings, and maintenance mode. The value in use is the database value if it is valid, otherwise the old configuration key if one is defined and valid, otherwise the default (`Resolve`). Overview: for each definition, the value in use, its source, default, limits, flag, current version, and who changed it last; it also lists fixed items that are not settings (token lifetime, password character classes, forced password change, "failed signature locks the login account: No", laboratory time zone, a masked line for secrets, and the Document Control configuration table, read-only). Update: inputs key, new value, reason, password. Runs in one unit of work. Refused when the key is unknown (404), the reason is blank, the value fails `Normalize` (type, whole number, minimum, maximum, allowed values, up to 500 characters) or equals the value in use. If no row exists yet it is created first. For a GxP-relevant setting the administrator signs (meaning `SystemSettingChanged`, bound to entity `SystemSetting` and the setting row ID, comment "key: old -> new: reason"); a wrong password rolls everything back. The row's version number goes up by one and a `SystemSettingVersion` row is added (version, value, previous value, user, time from the injected clock, reason, signature link); a hand-made `AuditLog` row `SYSTEM_SETTING_CHANGED` (category Configuration) is written; for maintenance mode a security event `ADMIN_MAINTENANCE_MODE_CHANGED` is also written. All are saved together. After the call the shared cache is cleared. A setting that is not GxP-relevant (for example retention days, the banner and its severity, the maintenance message, access-review dormant days) needs a reason but no signature. Readers use a cache of 30 seconds, so another server instance can show the old value for up to 30 seconds. History returns the versions newest first with the user name and the signature ID. No stale-form version check is made; the "same value" refusal and the row `xmin` token are the only guards against overlapping changes.
**Code:** `backend/MicroLIMS.Application/Services/SystemSettings/SystemSettingsService.cs` — `GetOverviewAsync`, `UpdateAsync`, `UpdateCoreAsync`, `GetHistoryAsync`; `backend/MicroLIMS.Application/Services/SystemSettings/SystemSettingDefinitions.cs` — `All`, `Normalize`; `backend/MicroLIMS.Application/Services/SystemSettings/SystemSettingsReader.cs` — `Resolve`, `SystemSettingsCache`; `backend/MicroLIMS.API/Controllers/AdminSettingsController.cs` — `Update`
**Verified by:** reading (not run)

### FS-ADM-009
**Satisfies:** URS-ADM-009
**Function:** Maintenance mode (`MaintenanceModeMiddleware`). While the setting `System.MaintenanceMode` is true, any request that is not GET, HEAD or OPTIONS from a signed-in caller or from a caller sending the instrument-agent key header is refused with HTTP 503, the header `X-Maintenance-Mode: on`, a `Retry-After` of 300 seconds and the maintenance message setting as the body. Always allowed: sign-in, sign-out, token refresh, change-password, and paths under `/api/admin/settings`. Also allowed: any path under `/api/admin` from a caller holding `System.ConfigureSettings`. Anonymous requests without the agent header are passed on. The state is read through the cached settings reader (see FS-ADM-008 for the cache). Turning the mode on or off is a signed settings change (FS-ADM-008). The route `/api/admin/settings` is allowed for every caller by this middleware; the controller still requires `System.ConfigureSettings`.
**Code:** `backend/MicroLIMS.API/Middleware/MaintenanceModeMiddleware.cs` — `InvokeAsync`, `IsAlwaysAllowed`
**Verified by:** reading (not run)

### FS-ADM-010
**Satisfies:** URS-ADM-003, URS-ADM-004, URS-ADM-007
**Function:** Administration continuity rule (`AdministrationContinuityGuard`). Used by the role change (FS-ADM-003), the disable (FS-ADM-004) and the permission change (FS-ADM-007). It loads the active users and the roles that hold `Users.Manage` and `Roles.Manage`, applies the proposed change to that picture without touching the change tracker, and checks that for each of the two permissions at least one active user would still hold it. If not, it refuses with a message naming the permission. Locked users count as holders. The role delete (FS-ADM-006) is protected differently (a role in use cannot be deleted). Deleting a user does not exist as an endpoint in the files read.
**Code:** `backend/MicroLIMS.Application/Services/AdministrationContinuityGuard.cs` — `EnsureAdministrableAfterAsync`, `FindMissingPermissionAfterAsync`
**Verified by:** reading (not run)
