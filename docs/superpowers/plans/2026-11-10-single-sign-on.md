# Single Sign-On (B3) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let users sign in with a company account through OpenID Connect or Active Directory (one mode per site), keep a separate MicroLIMS signing password, restrict local login to emergency administrator accounts, and end sessions of leavers.

**Architecture:** A `SignInMethod` on `User` and a `security.sso.mode` setting gate the existing local login. Two new sign-in paths (`OidcSignInService`, `LdapSignInService`) both end in the existing `AuthenticationService.CompleteLoginAsync` (added by B2), so tokens, sessions and audit stay in one place. Directory access sits behind two small interfaces (`IOidcProviderClient`, `ILdapDirectory`) so every rule is tested without a real directory.

**Tech Stack:** .NET 10, EF Core 10, PostgreSQL, `Microsoft.IdentityModel.Protocols.OpenIdConnect` (already transitive), `System.DirectoryServices.Protocols`, React 19, MUI 9, Vitest.

**Spec:** `docs/superpowers/specs/2026-11-10-single-sign-on-design.md`

**Prerequisite:** B2 is merged (`CompleteLoginAsync`, `IMfaService`, `UserMfaCredential`, `MfaPolicyService`).

## Global Constraints

- `CLAUDE.md` binds every task (separate contexts + `AsNoTracking`; `[PostgresFact]` executed, 0 skipped; `userEvent`; mock only `services/`; no `any`; injected `TimeProvider`).
- `ElectronicSignatureService` is not modified. A signature is user ID + MicroLIMS signing password in every mode.
- A company password is never stored, logged, placed in an audit row or in an exception message.
- MicroLIMS tokens never appear in a URL. Only the one-time exchange code does, in the URL fragment.
- LDAP connects with LDAPS or StartTLS only; there is no unencrypted fallback.
- Roles and sections are never read from the directory.
- Fixed values: pending OIDC request 10 minutes; exchange code 60 seconds, single use; clock skew 2 minutes; session cap default 12 hours (`security.sso.sessionMaxHours`, 1–24); emergency accounts at most 3; leaver worker interval 60 minutes.
- Failure messages to the user are generic; the technical reason goes to the security event only.
- New package versions only in `backend/Directory.Packages.props`. One migration: `AddSingleSignOn`.

## Review Focus

1. **A company username re-used for a new employee** after the old one left. The stored permanent id differs, so sign-in must be refused, not silently matched by name. Test in Task 4.
2. **The directory is unreachable when the leaver worker runs.** No user may be deactivated on a failed lookup. Test in Task 6.
3. **Callback replay.** The same `state` or the same exchange code used twice must fail the second time. Tests in Task 4.
4. **A directory user tries the signing password on the login form.** Must be refused with the same generic message as a wrong password, and must not reveal that the password was right. Test in Task 2.
5. **Switching the mode on with no working emergency account.** Must be refused, or the site can lock itself out the first time the directory is down. Test in Task 2.

## File Structure

| File | Responsibility |
|---|---|
| `backend/MicroLIMS.Domain/Enums/SignInMethod.cs`; `Entities/User.cs` (changed); `Entities/SsoPendingRequest.cs`; `Entities/SsoExchangeCode.cs` | Data model |
| `backend/MicroLIMS.Application/Services/Sso/SsoModeService.cs` | Mode, guards, local-login decision |
| `backend/MicroLIMS.Application/Services/Sso/DirectoryUserMatcher.cs` | Username / permanent-id matching rule |
| `backend/MicroLIMS.Application/Services/Sso/OidcSignInService.cs`; `Abstractions/Sso/IOidcProviderClient.cs` | OpenID Connect flow |
| `backend/MicroLIMS.Infrastructure/Sso/OidcProviderClient.cs`, `SsoOptions.cs` | Discovery, token exchange, ID-token validation |
| `backend/MicroLIMS.Application/Services/Sso/LdapSignInService.cs`; `Abstractions/Sso/ILdapDirectory.cs` | Active Directory sign-in |
| `backend/MicroLIMS.Infrastructure/Sso/LdapDirectory.cs` | LDAP calls |
| `backend/MicroLIMS.API/BackgroundServices/DirectoryLeaverWorker.cs`; `Application/Services/Sso/DirectoryLeaverService.cs` | Hourly check |
| `backend/MicroLIMS.API/Controllers/SsoController.cs` (`api/auth/sso`), `AdminSsoController.cs` (`api/admin/sso`) | HTTP |
| `frontend/src/modules/authentication/services/ssoService.ts`; `frontend/src/pages/SsoComplete.tsx`, `SetSigningPassword.tsx`; `frontend/src/modules/admin/CompanySignInPage.tsx`; `frontend/src/modules/users/dialogs/ImportDirectoryLinksDialog.tsx` | Frontend |

---

### Task 1: Data model, settings, event codes, migration

**Files:** Create the enum and two entities with configurations; Modify `User.cs`, its configuration, `IMicroLimsDbContext`/`MicroLimsDbContext.cs`, `SystemSettingDefinitions.cs`, `SecurityEventCodes.cs`, `LoginHistory.cs` (`string? Method`); migration `AddSingleSignOn`; Test `backend/MicroLIMS.Tests/IntegrationTests/SsoSchemaPostgresTests.cs`

**Interfaces — Produces:**
```csharp
public enum SignInMethod { Local = 0, Directory = 1 }
// User: SignInMethod SignInMethod; string? DirectoryUsername; string? DirectorySubjectId;
//       bool IsEmergencyAccount; DateTime? SigningPasswordSetAt; DateTime? DirectorySignInRefusedAt
// SsoPendingRequest: Id, StateHash [AuditRedacted], Nonce, CodeVerifier [AuditRedacted], CreatedAt, ExpiresAt, ConsumedAt
// SsoExchangeCode:   Id, UserId, CodeHash [AuditRedacted], CreatedAt, ExpiresAt, ConsumedAt
```
- `DirectoryUsername` stored trimmed and lower-cased; unique index filtered `"DirectoryUsername" IS NOT NULL`. Same for `DirectorySubjectId`.
- Check constraint: `IsEmergencyAccount` implies `SignInMethod = 0`.
- Settings: `security.sso.mode` (`Off`), `security.sso.sessionMaxHours` (`12`, 1–24), both `IsGxpRelevant: true`.
- `SsoPendingRequest` and `SsoExchangeCode` are short-lived login state; add both to `AuditCaptureExcludedTypes` with the comment `// short-lived sign-in state; outcomes are recorded as explicit security events`. **This needs the user's approval before the task starts** (Rule 3.3.0); without it, leave them audited.
- `SecurityEventCodes`: the eleven codes in spec 5.9.

- [ ] **Step 1: Failing tests:** `TwoUsersWithSameDirectoryUsername_DifferingOnlyInCase_AreRejected`; `TwoUsersWithSameSubjectId_AreRejected`; `ManyUsersWithNullDirectoryUsername_AreAllowed`; `EmergencyAccountWithDirectorySignIn_IsRejectedByCheckConstraint`; `ExistingUsers_DefaultToLocal_NotEmergency`.
- [ ] **Step 2:** Run with PostgreSQL → FAIL.
- [ ] **Step 3:** Implement; `dotnet ef migrations add AddSingleSignOn …`; confirm `Up()` touches no protected table.
- [ ] **Step 4:** New tests + `AuditCredentialRedactionTests` + `AppendOnlyTriggerCatalogPostgresTests` → PASS, 0 skipped; `has-pending-model-changes` → none.
- [ ] **Step 5:** Commit `feat(sso): data model, settings and migration`.

### Task 2: Mode service, local-login refusal, emergency accounts

**Files:** Create `SsoModeService.cs`; Modify `AuthenticationService.cs` (`LoginCoreAsync`), the user update service (emergency flag, sign-in method, directory username), the System Settings write path (mode guard); Test `backend/MicroLIMS.Tests/IntegrationTests/SsoLocalLoginPostgresTests.cs`

**Interfaces — Produces:**
```csharp
public enum SsoMode { Off, OpenIdConnect, ActiveDirectory }
public sealed class SsoModeService
{
    public Task<SsoMode> GetModeAsync(CancellationToken ct = default);
    public Task<bool> IsLocalLoginAllowedAsync(User user, CancellationToken ct = default);   // Off → true; otherwise user.IsEmergencyAccount
    public Task<IReadOnlyList<string>> GetSwitchOnBlockersAsync(int actingUserId, SsoMode target, CancellationToken ct = default);
    public Task RecordConnectionTestPassedAsync(SsoMode mode, CancellationToken ct = default);
}
```
Rules: in `ActiveDirectory` mode the login form is shared, so `LoginCoreAsync` routes a non-emergency user to `LdapSignInService` (Task 5) and an emergency user to the local path; in `OpenIdConnect` mode a non-emergency user on the form is refused. Refusal runs the password check anyway (constant work), returns `LoginFailedMessage`, records `AUTH_LOCAL_LOGIN_REFUSED_SSO_ON`, and does **not** increment the lockout counter when the password was correct. Emergency login always requires the B2 second factor (no trusted-computer skip), records `AUTH_EMERGENCY_LOGIN`, and emails every active System Administrator after commit. Setting `IsEmergencyAccount` is refused unless the user is a System Administrator, has an `Active` `UserMfaCredential`, and fewer than 3 others exist. Blocker messages (exact): "Run a successful connection test first."; "At least one emergency administrator account with a second factor is required."; "Link your own account to a company username first."

- [ ] **Step 1: Failing `[PostgresFact]` tests:**
  - `ModeOff_LocalLoginWorksAsBefore`
  - `OidcMode_DirectoryUser_CorrectSigningPassword_IsRefused_GenericMessage_NoTokens_NoLockoutIncrement` *(Review Focus 4)*
  - `OidcMode_DirectoryUser_WrongPassword_IncrementsLockout`
  - `OidcMode_EmergencyAccount_PasswordThenCode_Succeeds_RecordsEvent_EmailsAdministrators`
  - `EmergencyLogin_IgnoresTrustedDeviceToken_AlwaysAsksCode`
  - `MarkEmergency_NonAdministrator_Throws`; `…_WithoutSecondFactor_Throws`; `…_FourthAccount_Throws`
  - `SwitchOn_WithoutConnectionTest_Refused`; `…_WithoutEmergencyAccount_Refused` *(Review Focus 5)*; `…_ActingAdminNotLinked_Refused`; `…_AllConditionsMet_Succeeds_Audited`
  - `SwitchOff_IsAlwaysAllowed`
- [ ] **Step 2–4:** FAIL → implement → PASS, plus `LoginEnumerationTests`, B2 `MfaLoginPostgresTests`.
- [ ] **Step 5:** Commit `feat(sso): mode guard, emergency accounts, local-login refusal`.

### Task 3: Signing password

**Files:** Modify `AuthenticationService.cs` (`ChangePasswordAsync` sets `SigningPasswordSetAt`), `IAuthenticationService.cs` + `LoginOutcome` (`bool SigningPasswordRequired`), `AuthenticationController.cs` (response field), `frontend/src/components/SignatureDialog.tsx`, `frontend/src/pages/ChangePassword.tsx`; Create `frontend/src/pages/SetSigningPassword.tsx` (route `/set-signing-password`); Tests `backend/MicroLIMS.Tests/IntegrationTests/SigningPasswordPostgresTests.cs`, `SignatureDialog.test.tsx`, `SetSigningPassword.test.tsx`

**Interfaces — Produces:** `Task SetInitialSigningPasswordAsync(int userId, string newPassword, CancellationToken ct = default)` on `IAuthenticationService` — allowed only when `SignInMethod == Directory` and `SigningPasswordSetAt == null`; applies `PasswordPolicy` and history; `POST api/auth/signing-password` (authenticated, `RateLimitingPolicies.PasswordChange`). `CompleteLoginAsync` sets `SigningPasswordRequired` for such users. New directory users are created with an unusable random `PasswordHash`.

- [ ] **Step 1: Failing tests:**
  - `DirectoryUser_FirstSignIn_ReportsSigningPasswordRequired`
  - `SetInitial_AppliesPolicy_StoresHash_SetsTimestamp_WritesAuditWithRedactedHash`
  - `SetInitial_SecondCall_Throws`; `SetInitial_LocalUser_Throws`
  - `Sign_DirectoryUser_WithSigningPassword_Succeeds` (calls the unmodified `ElectronicSignatureService.SignAsync`)
  - `Sign_DirectoryUser_BeforeSettingSigningPassword_Fails`
  - `UserMovedFromLocalToDirectory_KeepsPasswordAsSigningPassword_NotAskedAgain`
  - Frontend: `SignatureDialog shows "Signing password" and the hint for a directory user, "Password" for a local user`; `SetSigningPassword blocks navigation until saved, shows policy errors inline`.
- [ ] **Step 2–4:** FAIL → implement (the frontend learns the method from `GET api/auth/me`, which gains `signInMethod`) → PASS.
- [ ] **Step 5:** Commit `feat(sso): signing password for directory users`.

### Task 4: OpenID Connect sign-in

**Files:** Create `DirectoryUserMatcher.cs`, `IOidcProviderClient.cs`, `OidcSignInService.cs`, `OidcProviderClient.cs`, `SsoOptions.cs`, `SsoController.cs`; Modify `ServiceCollectionExtensions.cs`, `Program.cs` (startup validation of options for the active mode); Tests `backend/MicroLIMS.Tests/UnitTests/Sso/DirectoryUserMatcherTests.cs`, `OidcTokenValidationTests.cs`, `backend/MicroLIMS.Tests/IntegrationTests/OidcSignInPostgresTests.cs`

**Interfaces — Produces:**
```csharp
public sealed record DirectoryIdentity(string SubjectId, string Username, bool MfaSatisfied);
public enum MatchFailure { None, NotLinked, SubjectMismatch, UserInactive }
public sealed record MatchResult(User? User, MatchFailure Failure, bool BindSubjectNow);
public static class DirectoryUserMatcher { public static MatchResult Match(DirectoryIdentity id, User? bySubject, User? byUsername); }

public interface IOidcProviderClient
{
    Task<Uri> BuildAuthorizeUriAsync(string state, string nonce, string codeChallenge, Uri redirectUri, CancellationToken ct = default);
    // Exchanges the code and validates the ID token. Throws OidcValidationException(reasonCode) on any failure.
    Task<DirectoryIdentity> RedeemAsync(string code, string codeVerifier, string expectedNonce, Uri redirectUri, CancellationToken ct = default);
    Task TestAsync(CancellationToken ct = default);
}
public sealed class OidcSignInService
{
    public Task<Uri> StartAsync(CancellationToken ct = default);
    public Task<Uri> HandleCallbackAsync(string? state, string? code, string? error, string? ip, CancellationToken ct = default);   // returns the frontend redirect
    public Task<LoginOutcome> ExchangeAsync(string exchangeCode, string? ip, string? userAgent, CancellationToken ct = default);
}
```
Matcher rule: a user found by `SubjectId` wins; otherwise a user found by username with a null `DirectorySubjectId` is bound now; a user found by username whose stored subject id differs → `SubjectMismatch`. `SubjectId` for OIDC is `issuer + "|" + sub`. HTTP: `GET api/auth/sso/start` (302), `GET api/auth/sso/callback` (302 to `{frontend}/sso-complete#code=…` or `#error=signin_failed`), `POST api/auth/sso/exchange` `{ code }`; all `[AllowAnonymous]` + `RateLimitingPolicies.Authentication`. `GET api/auth/sso/mode` returns `{ mode }` for the login page.

- [ ] **Step 1: Failing tests:**
  - Unit matcher: `BySubject_Wins`; `ByUsername_FirstTime_BindsSubject`; `ByUsername_DifferentStoredSubject_IsMismatch` *(Review Focus 1)*; `NoUser_NotLinked`; `InactiveUser_UserInactive`; `UsernameMatch_IsCaseInsensitive`.
  - Unit token validation (real `OidcProviderClient` against an in-memory discovery document and a generated RSA key): `Valid_ReturnsIdentity`; `BadSignature`, `WrongIssuer`, `WrongAudience`, `Expired`, `NonceMismatch` each throw with their reason code; `ThreeMinuteSkew_Rejected_NinetySeconds_Accepted`; `RequireMfaClaim_WithoutAmrMfa_Throws`; `UsernameClaimMissing_Throws`.
  - `[PostgresFact]` with a fake `IOidcProviderClient`: `Callback_LinkedUser_IssuesExchangeCode_ThenExchangeReturnsTokens_RecordsSuccess_LoginHistoryMethodOidc`; `Callback_SameStateTwice_SecondFails`; `Exchange_SameCodeTwice_SecondFails` *(Review Focus 3)*; `Exchange_After61Seconds_Fails`; `Callback_After10Minutes_Fails`; `Callback_NotLinked_RedirectsWithGenericError_RecordsNotLinked_CreatesNoUser`; `Callback_SubjectMismatch_Refused_Recorded`; `FirstSignIn_StoresSubjectId_AuditedAsUserDirectoryLinked`; `Callback_ProviderError_RedirectsWithGenericError`; `OidcSignIn_DoesNotAskSecondFactor`; `ModeNotOidc_StartReturns404`.
  - Startup: `OidcMode_MissingClientSecret_FailsNamingTheKey`.
- [ ] **Step 2–4:** FAIL → implement (PKCE S256; state, verifier and exchange code stored as SHA-256 hex; `ConfigurationManager<OpenIdConnectConfiguration>` for discovery and key refresh) → PASS, 0 skipped.
- [ ] **Step 5:** Commit `feat(sso): OpenID Connect sign-in`.

### Task 5: Active Directory sign-in

**Files:** Create `ILdapDirectory.cs`, `LdapSignInService.cs`, `LdapDirectory.cs`; Modify `backend/Directory.Packages.props`, `MicroLIMS.Infrastructure.csproj`, `backend/Dockerfile` (system LDAP library), `.github/workflows/ci.yml` (LDAP service container for one test class); Tests `backend/MicroLIMS.Tests/IntegrationTests/LdapSignInPostgresTests.cs`, `LdapDirectoryContainerTests.cs`

**Interfaces — Produces:**
```csharp
public sealed record LdapEntry(string SubjectId, string Username, bool Disabled);
public interface ILdapDirectory
{
    Task<LdapEntry?> FindAsync(string username, CancellationToken ct = default);          // service-account search
    Task<bool> BindAsync(string username, string password, CancellationToken ct = default); // false = bad credentials; throws DirectoryUnavailableException otherwise
    Task<IReadOnlyDictionary<string, LdapEntry?>> FindManyBySubjectAsync(IReadOnlyCollection<string> subjectIds, CancellationToken ct = default);
    Task TestAsync(CancellationToken ct = default);
}
public sealed class LdapSignInService
{
    public Task<LoginOutcome> SignInAsync(string username, string password, string? ip, string? userAgent, string? deviceToken, CancellationToken ct = default);
}
```
Order inside `SignInAsync`: find MicroLIMS user by `DirectoryUsername` → MicroLIMS lockout check (locked → refuse **without** contacting the directory) → `BindAsync` → `FindAsync` for subject id and disabled flag → matcher → B2 requirement (`MfaPolicyService`, trusted computer) → `CompleteLoginAsync`. Search filters escape the username (RFC 4515).

- [ ] **Step 1: Failing tests (fake `ILdapDirectory` that records calls):**
  - `ValidBind_LinkedUser_SignsIn_MethodLdap_BindsSubjectFirstTime`
  - `BadPassword_IncrementsMicroLimsCounter_GenericMessage`
  - `LockedInMicroLims_DirectoryIsNotContacted` (assert zero bind calls)
  - `DisabledInDirectory_Refused_UserDeactivated_SessionsRevoked_Audited`
  - `SubjectMismatch_Refused`; `NotLinked_Refused_BindNotAttemptedForUnknownUser`
  - `DirectoryUnavailable_ReturnsUnavailableMessage_RecordsEvent_NoLockoutIncrement`
  - `SecondFactorRequired_ReturnsChallenge_ThenVerifyCompletes`
  - `PasswordNeverAppearsInSecurityEventsAuditLogsOrLoginHistory` (search every text column written by the test for the password string)
  - Container test (CI): `RealDirectory_BindSucceedsWithCorrectPassword_FailsWithWrong`; `UsernameWithFilterCharacters_IsEscaped`; `PlainConnection_IsRefused`.
- [ ] **Step 2–4:** FAIL → implement → PASS. If the container test cannot run locally, report it as **not executed**, not as passed.
- [ ] **Step 5:** Commit `feat(sso): Active Directory sign-in`.

### Task 6: Leavers — session cap and hourly worker

**Files:** Create `DirectoryLeaverService.cs`, `DirectoryLeaverWorker.cs`; Modify `AuthenticationService.cs` (`RefreshCoreAsync` L166), `RefreshToken.cs` (`DateTime? SessionStartedAt`, carried across rotation; add to migration `AddSingleSignOn` if Task 1 is not merged, else a new migration `AddRefreshTokenSessionStart`); Test `backend/MicroLIMS.Tests/IntegrationTests/DirectoryLeaverPostgresTests.cs`

**Interfaces — Produces:** `Task<LeaverRunResult> RunAsync(CancellationToken ct = default)` with `record LeaverRunResult(int Checked, int Deactivated, bool DirectoryUnavailable)`.

Rules: in `OpenIdConnect` mode `RefreshCoreAsync` refuses a refresh for a `Directory` user when `now − SessionStartedAt > sessionMaxHours`, records `RefreshRejected…` with reason `session_cap`; emergency and (mode `Off`) local users are not capped. A failed company sign-in for a linked user sets `DirectorySignInRefusedAt`. In `ActiveDirectory` mode the worker batches linked active users (100 per query), deactivates those reported disabled or missing, revokes their sessions, writes the audit row via `RecordSystemEventAsync("DirectoryLeaverWorker", …)` with action `USER_DEACTIVATED_BY_DIRECTORY`, and emails System Administrators once per run.

- [ ] **Step 1: Failing tests:**
  - `Oidc_RefreshAt11h59_Succeeds_At12h01_Refused` (FakeTimeProvider; rotation in between keeps `SessionStartedAt`)
  - `Oidc_EmergencyAccount_NotCapped`; `ModeOff_NotCapped`
  - `Worker_DisabledAccount_DeactivatesUser_RevokesSessions_WritesSystemAudit`
  - `Worker_MissingAccount_Deactivates`
  - `Worker_DirectoryUnavailable_ChangesNothing_ReportsUnavailable` *(Review Focus 2)*
  - `Worker_EnabledAgain_DoesNotReactivate`
  - `Worker_ModeOidcOrOff_DoesNothing`
  - `Worker_UserWithoutSubjectId_IsSkipped`
- [ ] **Step 2–4:** FAIL → implement → PASS, 0 skipped.
- [ ] **Step 5:** Commit `feat(sso): session cap and leaver worker`.

### Task 7: Administration API

**Files:** Create `AdminSsoController.cs`; Modify the user DTOs/services (create/update accept `signInMethod`, `directoryUsername`, `isEmergencyAccount`; list returns `signInMethod`, `linkState`, `isEmergencyAccount`), `AccessReviewDtos.cs`/`AccessReviewService.cs` (`string SignInMethod`, `bool IsEmergencyAccount`); Test `backend/MicroLIMS.Tests/IntegrationTests/SsoAdminPostgresTests.cs`

**Interfaces — Produces** (policy `UsersManage`): `GET api/admin/sso` → `{ mode, configuration (non-secret only), lastTestPassedAt, blockers[], unlinkedUsers[] }`; `POST api/admin/sso/test`; `POST api/admin/sso/links/preview` and `…/links/import` (body: rows `{ username, companyUsername }`, max 2000) → per-row `{ row, username, status: Ok|UnknownUser|Duplicate|AlreadyLinked|Invalid, message }`; `POST api/admin/users/{id}/directory-relink` `{ reason }` (clears `DirectorySubjectId`). `linkState`: `NotLinked`, `LinkedNeverSignedIn`, `Linked`, `SignInRefused`.

- [ ] **Step 1: Failing tests:** `GetSso_NeverReturnsClientSecretOrBindPassword` (serialise the response and search); `Test_Success_RecordsTimestamp`; `Test_Failure_ReturnsReason_NoTimestamp`; `ImportPreview_ReportsEachStatus_ChangesNothing`; `Import_AppliesOnlyOkRows_InOneTransaction_AuditsEachUser`; `Import_DuplicateCompanyUsernameInFile_MarkedDuplicate`; `Relink_RequiresReason_ClearsSubject_Audited`; `Relink_Self_Throws`; `UserList_ReturnsLinkStates`; `AccessReviewEntry_IncludesSignInMethodAndEmergencyFlag`; `Endpoints_WithoutUsersManage_Return403`.
- [ ] **Step 2–4:** FAIL → implement → PASS.
- [ ] **Step 5:** Commit `feat(sso): administration API`.

### Task 8: Frontend — sign-in

**Files:** Create `ssoService.ts`, `SsoComplete.tsx` (route `/sso-complete`); Modify `Login.tsx`, `AppRoutes.tsx`; Tests `Login.test.tsx`, `SsoComplete.test.tsx`

- [ ] **Step 1: Failing tests (`userEvent`; `ssoService`/`authenticationService` mocked):**
  - `mode Off shows the form unchanged`
  - `mode OpenIdConnect shows "Sign in with your company account" as a link to the start endpoint and hides the form behind "Emergency administrator sign-in"`
  - `mode ActiveDirectory labels the field "Company username"`
  - `mode request fails → the local form is shown with a notice` (never a blank page)
  - `SsoComplete reads the code from the fragment, clears it from the address bar, exchanges it and signs in`
  - `SsoComplete with #error shows the generic message and "Back to sign in"`
  - `SsoComplete sends a directory user without a signing password to /set-signing-password`
  - `SsoComplete with no code shows the error state`
- [ ] **Step 2–4:** FAIL → implement → PASS; lint, `tsc -b`, `debt:check`.
- [ ] **Step 5:** Commit `feat(sso): sign-in screens`.

### Task 9: Frontend — administration

**Files:** Create `CompanySignInPage.tsx` (tab in `frontend/src/modules/admin/adminTabs.ts`), `ImportDirectoryLinksDialog.tsx`; Modify `UsersPage.tsx` and its user form dialog, the Access Review user line; Tests beside each

- [ ] **Step 1: Failing tests:**
  - `page shows mode, read-only configuration, last test time with time zone, and each blocker as text`
  - `Test connection shows success, or the failure reason in an alert`
  - `switch-on control is disabled with the blockers listed while any blocker remains`
  - `unlinked users report lists users and is replaced by an empty state when none`
  - `user form: choosing "Company login" requires a company username; "Emergency account" is disabled with its reason for non-administrators`
  - `Users table shows link state as text`
  - `import: pasted or uploaded CSV is previewed with per-row status; Import is disabled while any row is invalid unless "Skip invalid rows" is ticked; result summary announced`
  - `Re-link requires a reason`
  - loading, error and partial-outage states
- [ ] **Step 2–4:** FAIL → implement → PASS; lint, `tsc -b`, `debt:check`.
- [ ] **Step 5:** Commit `feat(sso): company sign-in administration`.

### Task 10: Final verification and documents

**Files:** `documents/` requirements and SOP outline (linking users, leavers, emergency accounts and their custody, signing password must differ from the company password, directory outage procedure); `improvement-roadmap.md` (B3 links; D-4 marked decided: separate signing password)

- [ ] **Step 1:** Release build; full backend suite without and with PostgreSQL; record exact pass/fail/skip counts and name any container test not executed.
- [ ] **Step 2:** `has-pending-model-changes` → none.
- [ ] **Step 3:** Frontend: lint, `tsc -b`, test, build, `debt:check`.
- [ ] **Step 4:** Run the app against a local OpenID Connect provider (Keycloak container) and an LDAP container: sign in, set a signing password, sign a record, disable the account and observe the session end. Report what was and was not exercised.
- [ ] **Step 5:** Commit `docs(sso): requirements, SOP outline, roadmap links`.

## Manual gates (site, after the plan)

- Application registration (Entra ID) or service account (Active Directory) created by the company's IT.
- Network path and certificate trust confirmed from the server.
- Sign-in, leaver and outage tests against the real directory, recorded for validation.
- Emergency accounts created, second factor enrolled, credentials held under the SOP.
- QA approval of the SOP.

## Cross-plan corrections (whole-programme review, 2026-11-11)

These corrections come from reviewing all plans together against the code. Where they differ from the text above, they win. Shared names are defined once in `docs/superpowers/plans/2026-11-11-build-order.md` (section "Programme conventions").

1. Migrations: `AddSingleSignOn` and, if Task 6 needs it, `AddRefreshTokenSessionStart` (two migrations are allowed).
2. Audit capture for `SsoPendingRequest` and `SsoExchangeCode` is approved (user, 2026-11-11): add both to `AuditCaptureExcludedTypes` with the comment given in Global Constraints.
3. Security event codes: add `SessionCapReached` and `RefreshRejected` to the 11 codes listed, so Task 6's reason codes exist.
4. Emergency accounts always require the B2 second factor, even when the MFA policy is off; OIDC sign-in never asks for a MicroLIMS second factor; LDAP sign-in follows the B2 policy.
