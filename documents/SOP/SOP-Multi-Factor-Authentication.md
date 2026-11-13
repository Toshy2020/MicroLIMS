# SOP Draft Outline: Multi-factor authentication (MFA)

> **Draft outline — not approved.** This is a working draft outline generated from the implemented MFA design (`2026-11-13`) for completion, review, and approval by Laboratory Management and Quality Assurance before go-live.

| Field | Value |
|---|---|
| Document number | |
| Title | Multi-factor authentication (draft outline) |
| Version | 0.1 |
| Status | Draft (outline — not approved) |
| Author | |
| Reviewer | |
| Approver | |
| Effective date | |

## 1. Purpose and scope

- Define how laboratory staff, System Administrators, and Quality Assurance manage second-factor authentication (`6`-digit RFC 6238 TOTP via authenticator application or assigned hardware token), recovery codes, trusted computers, encryption keys, and resets in MicroLIMS.
- Electronic signatures are **not** changed by MFA: every signature still requires user ID and password at signing time.

## 2. Responsibilities

- **All users:** Protect their authenticator device or hardware token and single-use recovery codes; never share codes or leave unlocked trusted browsers unattended.
- **System Administrators (`Users.Manage`):** Verify user identity before performing an MFA reset or assigning a hardware token, maintain encryption keys and NTP time synchronisation, and follow the two-administrator rule before enabling required MFA.
- **Quality Assurance (QA):** Approve this SOP, define site-specific periods and required roles (including whether `Qa` is in `security.mfa.requiredRoles`), and review security audit events.

## 3. Enrolment and grace period

- **Policy and roles:** Configured in System Settings (`security.mfa.policy`: `Off`, `Optional`, `RequiredForRoles` default, `RequiredForAll`; `security.mfa.requiredRoles`: default `SystemAdministrator,SectionHead,QcManager`; `security.mfa.enrolmentGraceDays`: default `7` days, allowed `0..30`).
- **Site-specific policy settings:** `[SITE TO COMPLETE: approved policy, required roles, and grace period in days]`.
- **Two-administrator guard:** Switching `security.mfa.policy` from non-required to required is blocked unless at least `2` active System Administrators already have an active second factor.
- **Authenticator app enrolment:** Scan the QR code (or enter the Base32 key manually) in an approved authenticator app (`MicroLIMS` issuer, `30`-second step, `6` digits) and confirm with a live code. Record or print the `10` single-use recovery codes (`XXXXX-XXXXX`) shown once at completion and store them securely offline (never on the same phone/computer).
- **Grace period behaviour:** Users in a required role who are within the grace period can sign in without MFA until `EnrolmentDueAt` (UTC); once overdue, sign-in forces enrolment before issuing an active session.

## 4. Lost device and recovery codes

- If your authenticator app or hardware token is unavailable, select **Use a recovery code instead** at sign-in and enter one of your `10` single-use recovery codes.
- MicroLIMS sends an e-mail notification whenever a recovery code is used and warns at sign-in when `2` or fewer recovery codes remain.
- Regenerate a fresh set of `10` recovery codes from **Profile -> Security** (requires password and current TOTP code) as soon as possible, or request an administrator MFA reset if your device is permanently lost.

## 5. Administrator MFA reset and identity verification

- Before resetting another user's MFA (`POST api/admin/users/{id}/mfa/reset`), the administrator must verify the user's identity out of band using `[SITE TO COMPLETE: approved identity verification method, e.g., in-person photo ID or supervisor confirmation]`.
- Enter a clear, specific reason (recorded in both `AuditLogs` as `MFA_RESET_BY_ADMIN` and the security audit trail as `MfaResetByAdmin`). Self-reset is blocked by the server.
- Resetting MFA revokes the user's active/pending factor, deletes unused recovery codes, revokes all trusted computers and active sessions, sends a notification e-mail, and backdates `MfaRequiredSince` so the user must enrol a new factor on their next sign-in (no fresh grace period is granted).

## 6. Hardware token issue, return, and seed custody

- **Issue:** Assign a hardware token in User Management (`SerialNumber`, Base32 or Hex seed, `30` or `60` second step, and documented reason). The token starts as `Pending` and activates on the user's first verified sign-in, which generates `10` recovery codes.
- **Seed custody:** `[SITE TO COMPLETE: where vendor seed files are stored offline, access controls, and destruction/retention rules]`. Seeds are encrypted at rest in MicroLIMS (`AesGcmSecretProtector`) and cannot be viewed again after entry.
- **Return / loss:** When a token is returned or lost, perform an administrator MFA reset immediately and record `[SITE TO COMPLETE: physical token return/decommission log]`.

## 7. Trusted computers

- When enabled (`security.mfa.trustedDeviceHours`, default `12` hours, allowed `0..24`), a user may trust a personal/workstation browser for `[SITE TO COMPLETE: approved hours; 0 on shared kiosk PCs]` to skip the MFA prompt on that browser.
- Users can revoke individual trusted browsers in **Profile -> Security**; administrators can revoke them in **User Sessions**.
- MicroLIMS automatically revokes all trusted browsers whenever a user changes or resets their password, removes their factor, or receives an administrator MFA reset.

## 8. Time synchronisation, key custody, and sole-administrator recovery

- **Time synchronisation:** TOTP accepts `-1` to `+1` time steps (`+/- 30` s for `30` s tokens). The API server must remain synchronised to `[SITE TO COMPLETE: internal NTP server]` within `< 15` seconds drift.
- **Encryption key custody & rotation:** `Mfa:ActiveKeyId` and `Mfa:EncryptionKeys` (`32`-byte AES-256-GCM keys) must be provisioned outside source control in `[SITE TO COMPLETE: server secret store / vault]` with backup custody by `[SITE TO COMPLETE: key custodians]`. Key rotation adds a new key ID as `ActiveKeyId` while keeping prior key IDs in `Mfa:EncryptionKeys` until all credentials are re-encrypted or rotated.
- **Sole-administrator recovery:** Ensure at least two System Administrators hold active MFA at all times. If all administrators lose their second factors simultaneously, recovery requires `[SITE TO COMPLETE: break-glass database/operations procedure approved by QA]`.

## 9. Periodic checks

- At `[SITE TO COMPLETE: monthly / quarterly]` intervals, review:
  - Users with `Overdue` or `TokenPendingConfirmation` MFA state in User Management.
  - Security audit events (`MfaFailed`, `MfaResetByAdmin`, `MfaTokenAssigned`, `TrustedDeviceRevoked`).
  - NTP drift and encryption key backup integrity.

## 10. Upgrade notes

- **No separate upgrade-notes file exists in the repository; deployment and upgrade impacts for MFA are recorded here.**
- **Encryption key required before API startup (`CONF-188`):** Because `security.mfa.policy` defaults to `"RequiredForRoles"`, `MfaStartupValidator` will refuse to start the API (`InvalidOperationException`) after upgrading unless `Mfa:ActiveKeyId` and `Mfa:EncryptionKeys:<id>` (base64 `32`-byte key) are configured before starting the service.
- **Database migration:** Apply migration `20261113013140_AddMultiFactorAuthentication` (creates `UserMfaCredentials`, `MfaRecoveryCodes`, `MfaChallenges`, `TrustedDevices`, and `Users.MfaRequiredSince` / `RefreshTokens.MfaVerified`).
- **SMTP notifications:** Ensure `Smtp:` configuration is active so recovery-code usage alerts and admin MFA reset notifications are delivered.
- **Open implementation gaps before release (`CONF-188` to `CONF-190`):** Review whether `Qa` should be added to `security.mfa.requiredRoles` and whether `StartEnrolmentCoreAsync` should block overwriting a `Pending` hardware token.

## 11. Review and approval

| Role | Name | Signature | Date |
|---|---|---|---|
| Author | | | |
| Reviewer (IT / System Administrator) | | | |
| Approver (Quality Assurance) | | | |
