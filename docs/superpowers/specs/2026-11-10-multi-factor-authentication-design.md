# B2 — Multi-Factor Authentication: Design Specification

**Roadmap package:** B2 (`improvement-roadmap.md`)
**Status:** Approved by the user 2026-11-10
**Date:** 2026-11-10

## 1. Purpose

Add a second login factor (a 6-digit time-based code) on top of username and password, so a stolen or shared password alone cannot open a MicroLIMS session. This supports 21 CFR Part 11 §11.300 and Annex 11 §12 access controls. Formal compliance still depends on the site's validation and SOPs.

## 2. Decisions (confirmed by the user, 2026-11-10)

| # | Decision | Answer |
|---|---|---|
| 1 | Second factor | TOTP codes from an authenticator app **or** a hardware token |
| 2 | Who must use it at go-live | System Administrator, Section Head, QC Manager. Others may enrol voluntarily |
| 3 | How often the code is asked | Once per shift on the same browser (trusted for a set number of hours, default 12) |
| 4 | Who resets a lost factor | Any System Administrator, with a reason, never their own |

Carried over from the roadmap: electronic signatures stay user ID + password at signing (CLAUDE.md Rule 3.3.5). MFA strengthens the session; it is not a signature component.

## 3. Out of scope

- Passkeys / FIDO2 / WebAuthn, SMS codes, email codes, push approval.
- Asking for a code at electronic signature.
- Single sign-on (package B3). Under SSO the identity provider owns MFA.
- Server-side idle timeout (today it is client-only; unchanged here).
- Instrument agent and API-key authentication (unchanged).

## 4. Current state (from code, not run)

- Login: `AuthenticationController` (`api/auth`) and `AuthenticationService.LoginCoreAsync`. Request `{ username, password }`; response `{ token, refreshToken, mustChangePassword, roleName }`.
- Tokens travel in the JSON body and sit in `localStorage` (`AuthContext.tsx`). Access token 15 min; refresh token rotated, stored as SHA-256 hash, 7 days.
- Lockout: `security.lockout.maxFailedAttempts` (5) and `security.lockout.durationMinutes` (15); a lock revokes all refresh tokens.
- Sessions: `RefreshTokens` rows are the session list; administrators list and revoke them (`AdminUserSessionsController`, `UserSessionsDialog.tsx`).
- No MFA code, no reversible encryption of secrets anywhere (passwords are BCrypt, tokens SHA-256).
- `[AuditRedacted]` exists and is applied centrally in `MicroLimsDbContext`.
- Idle logout after 20 minutes, client-side (`useIdleTimeout.ts`).

## 5. Design

### 5.1 The code algorithm

- RFC 6238 TOTP, HMAC-SHA1, 6 digits. These values are what every authenticator app and most hardware tokens support.
- Time step is stored per credential: 30 s (apps, most tokens) or 60 s (some tokens).
- Verification accepts the current step and one step either side (clock drift).
- **Replay protection:** the last accepted time step is stored on the credential; a code for that step or an earlier one is rejected.
- Implemented in the Application layer with `System.Security.Cryptography` and the injected `TimeProvider`. No new package. Unit tests use the RFC 6238 Appendix B test vectors.

### 5.2 Secret storage

A TOTP secret must be readable by the server, so it cannot be hashed. This is the first reversibly stored secret in the system.

- New `ISecretProtector` (Infrastructure): AES-256-GCM, random 12-byte nonce per value, output `keyId:nonce:ciphertext:tag`.
- The key comes from configuration (`Mfa__EncryptionKeys__<keyId>`, base64, 32 bytes) and `Mfa__ActiveKeyId`. Never in the database, never committed. Several keys may be configured so a key can be rotated: new values use the active key, old values still decrypt.
- Startup fails with a clear message if the MFA policy is not `Off` and no valid key is configured.
- Recovery codes are stored only as SHA-256 hashes (they are high-entropy random values, like refresh tokens).
- Every secret-bearing property carries `[AuditRedacted]`; `AuditCredentialRedactionTests` is extended.

### 5.3 Data model

| Entity | Fields | Notes |
|---|---|---|
| `UserMfaCredential` | `UserId`, `Type` (`AuthenticatorApp`/`HardwareToken`), `SecretProtected` *(redacted)*, `StepSeconds`, `SerialNumber?`, `Status` (`Pending`/`Active`/`Revoked`), `LastAcceptedStep`, `CreatedAt`, `ConfirmedAt`, `AssignedByUserId?`, `RevokedAt`, `RevokedByUserId`, `RevokeReason` | At most one `Active` and one `Pending` per user (filtered unique indexes) |
| `MfaRecoveryCode` | `UserId`, `CodeHash` *(redacted)*, `CreatedAt`, `UsedAt` | 10 per set; single use |
| `MfaChallenge` | `UserId`, `TokenHash` *(redacted)*, `Purpose` (`Verify`/`Enrol`), `ExpiresAt` (5 min), `FailedAttempts`, `ConsumedAt` | The half-logged-in state between password and code |
| `TrustedDevice` | `UserId`, `TokenHash` *(redacted)*, `CreatedAt`, `ExpiresAt`, `CreatedIp`, `UserAgent`, `RevokedAt`, `RevokedByUserId` | "Once per shift" memory |
| `User` (changed) | `MfaRequiredSince?` | Start of the enrolment grace period |
| `RefreshToken` (changed) | `MfaVerified` | Shown in the sessions list |

All are audited by default. `MfaChallenge` is short-lived operational state; it is proposed for `AuditCaptureExcludedTypes` because every outcome is written as an explicit audit event (5.9). **This exclusion needs your approval** (Rule 3.3.0); without it, challenges are simply audited like everything else.

### 5.4 Settings (existing System Settings, audited)

| Key | Default | Meaning |
|---|---|---|
| `security.mfa.policy` | `RequiredForRoles` | `Off`, `Optional`, `RequiredForRoles`, `RequiredForAll` |
| `security.mfa.requiredRoles` | `SystemAdministrator,SectionHead,QcManager` | Used when policy is `RequiredForRoles` |
| `security.mfa.trustedDeviceHours` | `12` | `0` means ask at every login. Allowed 0–24 |
| `security.mfa.enrolmentGraceDays` | `7` | Days a newly required user may still log in without enrolling. Allowed 0–30 |

A user "must use MFA" when the policy requires their role, or when they have an `Active` credential (voluntary enrolment counts until they remove it).

### 5.5 Login flow

1. `POST /api/auth/login` `{ username, password, deviceToken? }`. Password and lockout checks are unchanged and run first.
2. If the user does not need MFA, or `deviceToken` matches a valid, unexpired, unrevoked `TrustedDevice` of that user: tokens are issued as today.
3. If the user has an active credential: response `{ mfaRequired: true, mfaToken }` and **no** access or refresh token.
4. `POST /api/auth/mfa/verify` `{ mfaToken, code?, recoveryCode?, trustDevice }` → the normal login response, plus `deviceToken` when `trustDevice` is true and `trustedDeviceHours > 0`.
5. If MFA is required but the user is not enrolled:
   - inside the grace period: tokens are issued with `mfaEnrolmentDueAt`; the app shows a banner with the due date and a link to enrol;
   - after it: response `{ mfaEnrolmentRequired: true, mfaToken }`; the user must enrol (5.6) before any token is issued.
6. `mustChangePassword` is handled after MFA, as today.

Rules:
- A wrong code increments `User.FailedLoginAttempts` and the existing lockout applies. A challenge also dies after 5 wrong codes or 5 minutes.
- The `mfaToken` is random, stored hashed, single use, and is not a JWT; it cannot call any other endpoint.
- Token refresh never asks for a code. Refresh tokens are revoked on admin reset, lockout and password reset, as today.
- Password change, password reset, admin password recovery and admin MFA reset revoke all of the user's trusted devices.
- Email password reset does **not** bypass MFA: after the reset the code is still asked.
- MFA endpoints use the existing `auth` rate-limit partition.

### 5.6 Enrolment

**Authenticator app (self-service).** From the Profile page (voluntary) or the forced step at login:
1. `POST /api/auth/mfa/enrol/start` (re-enter password when already logged in) → creates a `Pending` credential and returns the secret as text, the `otpauth://` URI and a QR code.
2. The user scans the QR or types the key, then enters a code: `POST /api/auth/mfa/enrol/confirm`.
3. On success the credential becomes `Active`, 10 recovery codes are shown **once** (print or copy), and the user must tick "I have saved these codes" to continue.

The QR code is drawn by the backend with ZXing.Net, the library B1 adds. If B2 is built before B1, B2 adds and qualifies it. The secret never appears in a URL, log or audit row.

**Hardware token (assigned by an administrator).** For users who cannot carry a phone:
1. A System Administrator opens the user, chooses "Assign hardware token", enters the token serial number, the seed from the supplier's seed file, and the step (30/60 s), with a reason. Creates a `Pending` credential.
2. At next login the user enters a code from the token to confirm it; it becomes `Active` and recovery codes are shown.
3. An administrator cannot assign a token to their own account.

Known limit, stated openly: the administrator sees the seed while typing it. The SOP must cover seed-file custody. Programmable tokens that scan the QR code avoid this and work through the self-service path.

**Removing a factor.** A user may remove their own factor only when the policy does not require it, with password + current code. **Regenerating recovery codes** needs password + current code and invalidates the old set.

### 5.7 Lost device

1. **Recovery code:** entered at the challenge instead of the code. Single use. The user sees how many remain and is told by email that one was used.
2. **Administrator reset:** `POST /api/admin/users/{id}/mfa/reset` `{ reason }`, permission `Users.Manage`, **rejected by the backend when the target is the caller**. It revokes the credential, recovery codes, trusted devices and all sessions, sets `MfaRequiredSince` to now (a fresh grace period is **not** given: the user enrols at next login), and emails the user.
3. **Every administrator locked out:** see item 9.1.

### 5.8 Screens

- **Login** (`Login.tsx`): second step "Enter the 6-digit code", one field with `inputMode="numeric"` and `autocomplete="one-time-code"`, a "Trust this computer for 12 hours" checkbox (hidden when the setting is 0), a "Use a recovery code" link, and error text announced with `role="alert"`.
- **Forced enrolment** (new page): QR, key as text, code field, then the recovery-codes step.
- **Profile → Security** (`Profile.tsx`, today read-only): status, enrol, remove, regenerate codes, list of own trusted computers with "Forget".
- **Users** (`UsersPage.tsx`): column "Second factor" (Enrolled / Not enrolled / Due by date / Overdue) shown as text, not colour alone; row actions "Reset second factor" and "Assign hardware token".
- **Sessions dialog**: shows whether each session passed MFA, and the user's trusted computers with "Revoke".
- **Access Review**: MFA status is added to each user line, so the periodic review sees who is not enrolled.

All screens follow CLAUDE.md sections 6, 8 and 9.

### 5.9 Audit trail

Login-time events go through the existing `ISecurityAuditService` (the same trail as `AUTH_LOGIN_*` events); the administrator reset also writes an `AuditLogs` row with its reason in the same transaction. No event carries a secret. Events:
`MFA_ENROLMENT_STARTED`, `MFA_ENROLLED`, `MFA_TOKEN_ASSIGNED`, `MFA_VERIFIED`, `MFA_FAILED`, `MFA_RECOVERY_CODE_USED`, `MFA_RECOVERY_CODES_REGENERATED`, `MFA_REMOVED_BY_USER`, `MFA_RESET_BY_ADMIN` (with reason and target), `TRUSTED_DEVICE_ADDED`, `TRUSTED_DEVICE_REVOKED`. Setting changes are audited by `SystemSettingsService` as today.

## 6. Error handling

| Situation | Behaviour |
|---|---|
| Wrong or reused code | "The code is not correct." Counts toward lockout. No hint whether it was reused |
| Challenge expired or used | "Sign in again." Back to the password step |
| Account locked during challenge | Existing lockout message |
| Encryption key missing or wrong | Startup failure when policy ≠ `Off`; a credential that cannot be decrypted fails closed with an error id and an error-log entry, never a silent pass |
| Server clock wrong | Codes fail for everyone; the health page shows server UTC time. The SOP covers time synchronisation |
| Email cannot be sent | The security action still completes; the failure is logged (as for other notifications) |

## 7. Testing

- **Unit:** RFC 6238 vectors; drift window; replay rejection; 30 s and 60 s steps; protector round trip, tamper detection, key rotation.
- **`[PostgresFact]` (separate contexts, `AsNoTracking`, state + audit + negatives):** full login with code; trusted device accepted, expired, revoked, and belonging to another user; recovery code single use; wrong codes lock the account; challenge expiry and 5-attempt limit; forced enrolment after grace; grace period allows login; admin reset revokes everything and writes the audit row; admin cannot reset own factor; admin cannot assign a token to self; unique-index rule on active credentials; `mfaToken` cannot authorise any other endpoint.
- **Redaction:** `AuditCredentialRedactionTests` covers `SecretProtected`, `CodeHash`, both `TokenHash` fields.
- **Frontend (`userEvent`, real components, service-boundary mocks):** code step, recovery-code path, enrolment with the "saved" tick, Users reset dialog needing a reason.
- **Mutation self-check** on each: removing the code check or the self-reset guard must fail a test.

## 8. Delivery stages

1. Algorithm, secret protector, data model and migration.
2. Login challenge, trusted devices, recovery codes.
3. Self-service enrolment and Profile security section.
4. Administrator reset, hardware-token assignment, Users and sessions screens, Access Review line.
5. Settings, grace period, documents (requirements, SOP outline, validation notes).

Rollout: ship with policy `Optional`, let administrators enrol first, then switch to `RequiredForRoles`. The 7-day grace period starts at the switch.

## 9. Open items

1. **Sole administrator locked out (decided 2026-11-10).** A required policy cannot be switched on until two active System Administrators have set up a second factor; one can then reset the other. The existing admin password recovery is issued by another administrator inside the application (it is not an offline procedure) and stays password-only: it does not clear the second factor. If every administrator is locked out at once, recovery is a database intervention under the site's change control, described in the SOP; no application back door is built.
2. **Hardware token model** and how its seeds are delivered (decides 30 or 60 s and the SOP wording).
3. `MfaChallenge` is excluded from automatic audit capture — **approved by the user 2026-11-10**.
4. **Who holds the encryption key** and where it is backed up. Losing the key means every user must re-enrol.
5. **SOP:** enrolment, lost device, seed custody, time synchronisation, key rotation.
