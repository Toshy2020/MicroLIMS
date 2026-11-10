# B3 — Single Sign-On (OpenID Connect and Active Directory): Design Specification

**Roadmap package:** B3 (`improvement-roadmap.md`)
**Status:** Approved by the user 2026-11-10 (12-hour session cap accepted)
**Date:** 2026-11-10
**Depends on:** B2 (second factor) for emergency accounts and for the Active Directory path.

## 1. Purpose

Let users sign in with their company account, so joiners, leavers and password rules are managed in one place, while MicroLIMS keeps its own roles, sections, audit trail and electronic signatures.

## 2. Decisions (confirmed by the user, 2026-11-10)

| # | Decision | Answer |
|---|---|---|
| 1 | Directory | Build both paths; one setting picks the site's mode: OpenID Connect (Entra ID and similar) or on-premises Active Directory (LDAP) |
| 2 | Electronic signature under SSO (roadmap D-4) | Each user keeps a separate **MicroLIMS signing password** |
| 3 | Local login when SSO is on | Only named emergency administrator accounts |
| 4 | Provisioning | An administrator creates and links the user first; unknown company accounts are refused |

Roadmap decision D-4 is closed by row 2.

## 3. Out of scope

- SAML, Kerberos / Windows integrated sign-in, SCIM provisioning.
- Mapping directory groups to MicroLIMS roles or sections. Roles and sections stay in MicroLIMS so Access Review and Segregation of Duties keep working.
- Creating users on first login.
- Single sign-out across other company applications.

## 4. Current state (from code, not run)

- Login is local only: `AuthenticationService.LoginAsync`, BCrypt `User.PasswordHash`, tokens in the JSON body, stored in `localStorage`.
- `ElectronicSignatureService.SignAsync(userId, password, …)` checks the same `PasswordHash`, with its own failure throttle.
- Password policy, history, reset by email and administrator recovery exist.
- No OIDC, LDAP or SAML code. `Microsoft.AspNetCore.Authentication.JwtBearer` is the only authentication package.
- Access tokens last 15 minutes, refresh tokens 7 days; refresh does not re-check anything outside MicroLIMS.

## 5. Design

### 5.1 Modes

Setting `security.sso.mode`: `Off` (today's behaviour), `OpenIdConnect`, `ActiveDirectory`. One mode per installation.

Connection details hold secrets and server addresses, so they live in server configuration, not in the database:

| Mode | Configuration |
|---|---|
| OpenID Connect | `Sso__Oidc__Authority`, `ClientId`, `ClientSecret`, `UsernameClaim` (default `preferred_username`), `RequireMfaClaim` (default `false`) |
| Active Directory | `Sso__Ldap__Host`, `Port` (default 636), `BaseDn`, `BindDn`, `BindPassword`, `UsernameAttribute` (default `sAMAccountName`), `UseStartTls` |

An administrator page "Company sign-in" shows the non-secret values read-only and has a **Test connection** action.

### 5.2 User record

| Field | Meaning |
|---|---|
| `SignInMethod` | `Local` or `Directory` |
| `DirectoryUsername` | What the administrator types to link the user (UPN, email or Windows username). Unique, case-insensitive |
| `DirectorySubjectId` | The directory's permanent id, stored at the first successful sign-in (OIDC issuer + `sub`; Active Directory `objectGUID`). After that, the match is on this id, so renaming an account does not break the link and re-using a username for a new person does not grant the old person's access |
| `IsEmergencyAccount` | Local login allowed while SSO is on |
| `SigningPasswordSetAt` | Null until a directory user has chosen a signing password |

All changes are audited by default. `DirectorySubjectId` can be cleared only by an administrator, with a reason ("Re-link").

### 5.3 OpenID Connect sign-in

Authorization code flow with PKCE, run by the backend as a confidential client. No cookie session is introduced.

1. Login page button "Sign in with your company account" → `GET /api/auth/sso/start` → redirect to the provider with `state`, `nonce` and PKCE challenge. The pending request is stored server-side, hashed, for 10 minutes.
2. Provider returns to `GET /api/auth/sso/callback`. The backend checks `state`, exchanges the code, and validates the ID token (signature against the provider's published keys, issuer, audience, expiry, `nonce`).
3. The user is matched (5.2). No match, inactive user, or a subject id that differs from the stored one → refused with a generic message and a security event.
4. The backend redirects to the frontend `/sso-complete#code=…` with a one-time code (random, hashed, 60 seconds, single use). The frontend posts it to `POST /api/auth/sso/exchange` and receives the normal login response. MicroLIMS tokens never appear in a URL.
5. If `RequireMfaClaim` is on, the ID token must show multi-factor sign-in (`amr` contains `mfa`); otherwise refused. The B2 code is **not** asked in this mode: the provider owns the second factor.

Uses `Microsoft.IdentityModel.Protocols.OpenIdConnect`, which the existing JwtBearer package already brings in.

### 5.4 Active Directory sign-in

1. The login page stays as it is; the username label becomes "Company username".
2. The backend connects over **LDAPS or StartTLS only** (a plain connection is refused at startup), finds the user with the service account, then binds as the user with the typed password. The password is never stored or logged.
3. The directory's `objectGUID` and disabled flag are read; the user is matched as in 5.2.
4. MicroLIMS's own failed-attempt counter and lockout run **before** contacting the directory, so MicroLIMS cannot be used to lock out a company account by guessing.
5. The B2 second factor applies after a successful bind, by the B2 policy.

Library: `System.DirectoryServices.Protocols` (Microsoft, MIT). On Linux it needs the system LDAP library in the container image; this is added to the `Dockerfile` and proven by a test against a containerised directory. If that proves unreliable, the fallback is `Novell.Directory.Ldap.NETStandard` (MIT). The choice is confirmed in the plan.

### 5.5 Signing password

Electronic signatures keep both components inside MicroLIMS: user ID + MicroLIMS signing password. `ElectronicSignatureService` is unchanged.

- For a `Directory` user, `User.PasswordHash` **is** the signing password. It cannot be used to log in (5.6).
- At the first company sign-in the user must choose it before entering the application (same gate as today's "must change password"). Existing users who move to company sign-in keep their current password as the signing password.
- Policy, history, lockout throttle, change, reset by email and administrator recovery work as today; the screens say "signing password".
- `SignatureDialog` label becomes "Signing password" with a one-line hint for directory users: "This is your MicroLIMS signing password, not your company password."
- The system cannot compare it with the company password (it never has one in OpenID Connect mode). The SOP states they must differ.

### 5.6 Local login and emergency accounts

When the mode is not `Off`:
- Local login is refused for every user except those with `IsEmergencyAccount`.
- Emergency accounts must hold the System Administrator role, must have a B2 second factor, and are capped at 3.
- Every emergency sign-in writes a security event and emails all System Administrators.
- The login page shows a small "Emergency administrator sign-in" link.
- **Guard on switching SSO on:** refused unless (a) Test connection passed, (b) at least one emergency account has a second factor, and (c) the administrator making the change is linked to a directory account. A pre-switch report lists users with no `DirectoryUsername`; they will not be able to sign in.

### 5.7 Leavers and disabled accounts

A MicroLIMS session must not outlive the company account for long.

- **Active Directory:** an hourly worker checks every linked active user. A disabled or deleted directory account → the MicroLIMS user is deactivated, all sessions revoked, an audit row written by the named system process, and System Administrators notified. Also checked at every sign-in.
- **OpenID Connect:** the provider does not announce disabled accounts. Sessions of directory users are capped by `security.sso.sessionMaxHours` (default 12): after that the refresh token is refused and the user returns to the provider, which will refuse a disabled account. The MicroLIMS user is then marked "Directory sign-in refused" for the administrator. A direct check through Microsoft Graph is an open item (9.3).
- Reactivation is always a manual administrator action. A directory account coming back does not reactivate the MicroLIMS user.
- If the directory is unreachable, the worker changes nothing and raises an alert; it never deactivates on a failed lookup.

### 5.8 Screens

- **Login:** per mode as above; clear messages for "no MicroLIMS account is linked to this company account" and "company sign-in is unavailable — contact an administrator" (the second also names the emergency route to administrators only in the SOP, not on screen).
- **`/sso-complete`:** progress and error state with a "Back to sign in" link.
- **Set signing password:** first-login step.
- **Users:** fields "Sign-in method", "Company username", "Emergency account"; columns for link state (Not linked / Linked, never signed in / Linked); action "Re-link" with reason; CSV import of `username, companyUsername` for the first roll-out, with a preview and per-row errors.
- **Company sign-in (admin):** mode, read-only configuration, Test connection, unlinked-users report.
- **Access Review:** each user line shows sign-in method and emergency flag.

### 5.9 Audit and security events

`AUTH_SSO_LOGIN_SUCCESS`, `AUTH_SSO_LOGIN_FAILED_NOT_LINKED`, `AUTH_SSO_LOGIN_FAILED_TOKEN_INVALID`, `AUTH_SSO_LOGIN_FAILED_SUBJECT_MISMATCH`, `AUTH_SSO_LOGIN_FAILED_MFA_CLAIM_MISSING`, `AUTH_DIRECTORY_UNAVAILABLE`, `AUTH_EMERGENCY_LOGIN`, `AUTH_LOCAL_LOGIN_REFUSED_SSO_ON`, `USER_DIRECTORY_LINKED`, `USER_DIRECTORY_RELINKED`, `USER_DEACTIVATED_BY_DIRECTORY`. `LoginHistory` records the method. All regulated actions remain attributed to the MicroLIMS `UserId`, so the audit trail does not change shape.

## 6. Error handling

| Situation | Behaviour |
|---|---|
| Provider or directory unreachable | Sign-in fails with a plain message; existing sessions continue until they need the directory; emergency accounts work |
| ID token invalid, expired, wrong audience, replayed `state` or code | Refused, generic message, security event with the technical reason |
| Company account valid, no linked user | Refused; nothing is created |
| Same company account linked to two users | Prevented by the unique index; import reports the row |
| Clock skew with the provider | Up to 2 minutes tolerated |
| LDAP certificate not trusted | Connection refused; never falls back to an unencrypted connection |
| Configuration incomplete for the chosen mode | Startup fails with a message naming the missing key |

## 7. Testing

- **Unit:** user matching rules; ID-token validation with a test signing key (bad signature, issuer, audience, nonce, expiry, missing MFA claim); mode guard.
- **`[PostgresFact]`:** full OIDC flow against an in-process fake provider (discovery, keys, token endpoint); one-time code single use and expiry; subject-id binding and mismatch; local login refused when SSO is on; emergency login allowed with second factor and alerts sent; session cap refuses refresh after the limit; worker deactivates a disabled account and leaves users alone when the directory is unreachable; signing with the signing password works for a directory user and the company password is never accepted; unique link index; audit rows and security events for each.
- **LDAP:** the same tests against an `ILdapDirectory` fake, plus one real-directory test run in CI against a containerised LDAP server (skipped locally only when no container is available, and reported as such).
- **Frontend:** login page per mode, `/sso-complete` success and failure, signing-password step, Users link fields and CSV import preview, admin page guard messages.
- **Manual (site):** sign-in against the real provider or domain controller; disable a test account and confirm the session ends within the stated time.

## 8. Delivery stages

1. User fields, settings, mode guard, emergency accounts, local-login refusal.
2. Signing-password step and wording.
3. OpenID Connect sign-in.
4. Active Directory sign-in and the leaver worker.
5. Session cap, admin page, Users linking and import, Access Review lines.
6. Documents: requirements, SOP outline (linking, leavers, emergency accounts, signing password), validation notes.

## 9. Open items

1. Which mode the first site uses, and its connection details (tenant or domain, who registers the application or creates the service account).
2. Whether the server can reach the provider (outbound internet for Entra ID) or the domain controller (LDAPS port and certificate).
3. Optional Microsoft Graph check for disabled Entra accounts (needs an extra directory permission) to shorten the 12-hour window.
4. Whether the provider's multi-factor claim should be required (`RequireMfaClaim`).
5. SOP texts, including the rule that the signing password differs from the company password.
