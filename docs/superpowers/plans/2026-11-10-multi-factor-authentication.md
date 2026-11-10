# Multi-Factor Authentication (B2) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a TOTP second factor (authenticator app or hardware token) to login, with trusted computers, recovery codes, forced enrolment by role and administrator reset.

**Architecture:** A pure TOTP algorithm and an AES-GCM secret protector sit under a new `MfaService` (Application). `AuthenticationService.LoginAsync` stops after the password check and returns a challenge instead of tokens when a code is needed; new `api/auth/mfa/*` endpoints complete the login. The frontend adds a second login step, an enrolment page and security controls on Profile and Users.

**Tech Stack:** .NET 10, EF Core 10, PostgreSQL, `System.Security.Cryptography` (HMAC-SHA1, AES-GCM), ZXing.Net (QR only), React 19, MUI 9, Vitest.

**Spec:** `docs/superpowers/specs/2026-11-10-multi-factor-authentication-design.md`

## Global Constraints

- `CLAUDE.md` binds every task: separate `DbContext` for arrange/act/assert with `AsNoTracking`; `[PostgresFact]` tests must run, not skip; `userEvent`, never `fireEvent`; mock only `services/` modules; no new `any`; no direct clock reads (use the injected `TimeProvider`).
- TOTP: RFC 6238, HMAC-SHA1, 6 digits, step 30 or 60 s per credential, window ±1 step, a step is accepted once.
- Secrets: TOTP secret encrypted with AES-256-GCM; recovery codes, challenge tokens and device tokens stored as SHA-256 hex. Every such property carries `[AuditRedacted]`. No secret in a log, URL, audit row or exception message.
- Limits: challenge lifetime 5 minutes, 5 wrong codes per challenge, 10 recovery codes per set, settings defaults `RequiredForRoles`, `SystemAdministrator,SectionHead,QcManager`, 12 hours, 7 days.
- Wrong codes increment `User.FailedLoginAttempts` and use the existing lockout settings.
- Electronic signatures are not touched (`ElectronicSignatureService` stays user ID + password).
- New package versions only in `backend/Directory.Packages.props`. If B1 has not merged, this plan adds `ZXing.Net` there (newest stable, Apache-2.0) and its qualification record.
- Every entity change goes into the single migration `AddMultiFactorAuthentication`; `dotnet ef migrations has-pending-model-changes` must report none.
- User-facing text uses "second factor" and "6-digit code", never "TOTP".

## Review Focus

1. **Half-logged-in token used elsewhere.** An `mfaToken` sent as a Bearer token or to `api/auth/refresh` must get 401. Test in Task 5.
2. **Same code used twice inside its 30 seconds** (shoulder-surfing). The second use must fail even though the code is "valid". Test in Task 5.
3. **Trusted-computer token from user A sent with user B's password.** Must be ignored and a code asked. Test in Task 5.
4. **Enrolment abandoned half-way, then started again.** The old `Pending` credential must be replaced, not block the unique index or leave two secrets. Test in Task 6.
5. **Policy switched on when only one administrator is enrolled or exists.** The setting change must be refused with a clear message, so the site cannot lock itself out. Test in Task 7.

## File Structure

| File | Responsibility |
|---|---|
| `backend/MicroLIMS.Application/Security/Totp.cs` | Code generation and verification, Base32 |
| `backend/MicroLIMS.Application/Abstractions/Security/ISecretProtector.cs` | Protect / unprotect contract |
| `backend/MicroLIMS.Infrastructure/Security/AesGcmSecretProtector.cs`, `MfaOptions.cs` | Encryption and key configuration |
| `backend/MicroLIMS.Domain/Entities/UserMfaCredential.cs`, `MfaRecoveryCode.cs`, `MfaChallenge.cs`, `TrustedDevice.cs`; `Enums/MfaCredentialType.cs`, `MfaCredentialStatus.cs`, `MfaChallengePurpose.cs` | Data model |
| `backend/MicroLIMS.Persistence/Configurations/*Configuration.cs` (one per entity) | Mapping, indexes |
| `backend/MicroLIMS.Application/Services/Mfa/MfaPolicyService.cs` | Who must use MFA, grace period |
| `backend/MicroLIMS.Application/Services/Mfa/MfaService.cs` (+ `Interfaces/IMfaService.cs`, `DTOs/MfaDtos.cs`) | Challenges, verification, trusted devices, recovery codes, enrolment |
| `backend/MicroLIMS.Application/Services/Mfa/MfaAdminService.cs` | Reset, hardware-token assignment, status list |
| `backend/MicroLIMS.API/Controllers/MfaController.cs` (`api/auth/mfa`), `AdminUserMfaController.cs` (`api/admin/users/{id}/mfa`) | HTTP |
| `frontend/src/modules/authentication/services/mfaService.ts` | API client |
| `frontend/src/pages/Login.tsx`, `frontend/src/pages/MfaEnrolment.tsx`, `frontend/src/pages/Profile.tsx` | Login step, forced enrolment, own security |
| `frontend/src/modules/authentication/components/MfaCodeField.tsx`, `EnrolmentSteps.tsx`, `RecoveryCodesPanel.tsx` | Shared pieces |
| `frontend/src/modules/users/dialogs/ResetMfaDialog.tsx`, `AssignHardwareTokenDialog.tsx` | Admin actions |

---

### Task 1: TOTP algorithm

**Files:** Create `backend/MicroLIMS.Application/Security/Totp.cs`; Test `backend/MicroLIMS.Tests/UnitTests/Security/TotpTests.cs`

**Interfaces — Produces:**
```csharp
public static class Totp
{
    public static long StepAt(DateTime utc, int stepSeconds);
    public static string Generate(ReadOnlySpan<byte> secret, long step);            // 6 digits, zero-padded
    // Returns the matched step, or null. Rejects any step <= lastAcceptedStep.
    public static long? Verify(ReadOnlySpan<byte> secret, string code, DateTime nowUtc, int stepSeconds, long? lastAcceptedStep);
    public static string ToBase32(ReadOnlySpan<byte> bytes);                         // RFC 4648, no padding
    public static byte[] FromBase32(string text);                                    // ignores spaces, case, padding; throws FormatException
    public static byte[] NewSecret();                                                // 20 random bytes
    public static string OtpAuthUri(string issuer, string username, string base32Secret, int stepSeconds);
}
```

- [ ] **Step 1: Write failing tests**
  - `Generate_MatchesRfc6238Sha1Vectors`: secret ASCII `12345678901234567890`; RFC gives 8 digits, assert the last 6: T=59 → `287082`, 1111111109 → `081804`, 1111111111 → `050471`, 1234567890 → `005924`, 2000000000 → `279037`.
  - `Verify_AcceptsPreviousAndNextStep_RejectsTwoStepsAway`.
  - `Verify_RejectsStepAlreadyAccepted_AndEarlier` (pass `lastAcceptedStep` = matched step).
  - `Verify_RejectsNonDigits_WrongLength_Empty` (returns null, does not throw).
  - `Verify_SixtySecondStep_UsesSixtySecondWindow`.
  - `Base32_RoundTrips_AndIgnoresSpacesAndCase`; `FromBase32_InvalidCharacter_Throws`.
  - `OtpAuthUri_EscapesIssuerAndUsername_AndIncludesPeriod` → `otpauth://totp/MicroLIMS:a%40b?secret=…&issuer=MicroLIMS&algorithm=SHA1&digits=6&period=30`.
- [ ] **Step 2:** `dotnet test backend/MicroLIMS.Tests --filter FullyQualifiedName~TotpTests` → FAIL (type missing).
- [ ] **Step 3: Implement.** Code comparison uses `CryptographicOperations.FixedTimeEquals`.
- [ ] **Step 4:** Same command → PASS.
- [ ] **Step 5:** Commit `feat(mfa): TOTP algorithm with RFC 6238 vectors`.

### Task 2: Secret protector

**Files:** Create `ISecretProtector.cs`, `AesGcmSecretProtector.cs`, `MfaOptions.cs`; Modify `backend/MicroLIMS.API/Extensions/ServiceCollectionExtensions.cs` (bind options near L283, register singleton); Test `backend/MicroLIMS.Tests/UnitTests/Security/AesGcmSecretProtectorTests.cs`

**Interfaces — Produces:**
```csharp
public interface ISecretProtector
{
    bool IsConfigured { get; }
    string Protect(ReadOnlySpan<byte> plaintext);     // "keyId:base64(nonce):base64(ciphertext):base64(tag)"
    byte[] Unprotect(string protectedValue);          // throws SecretProtectionException on unknown key or tamper
}
public sealed class MfaOptions
{
    public const string SectionName = "Mfa";
    public string? ActiveKeyId { get; set; }
    public Dictionary<string, string> EncryptionKeys { get; set; } = new();   // keyId -> base64 of 32 bytes
}
```

- [ ] **Step 1: Failing tests:** `RoundTrip`; `TwoProtectsOfSameValue_Differ` (random nonce); `TamperedCiphertext_Throws`; `UnknownKeyId_Throws`; `OldKeyStillDecrypts_AfterActiveKeyChanges`; `KeyNot32Bytes_IsNotConfigured`; `NoKeys_IsConfiguredFalse_AndProtectThrows`.
- [ ] **Step 2:** Run filter `AesGcmSecretProtectorTests` → FAIL.
- [ ] **Step 3: Implement** with `System.Security.Cryptography.AesGcm`, 12-byte nonce, 16-byte tag. Exception messages name the key id only.
- [ ] **Step 4:** PASS.
- [ ] **Step 5:** Add `Mfa__ActiveKeyId` / `Mfa__EncryptionKeys__k1` to the deployment env example file with a generation command (`openssl rand -base64 32`) and no real value. Commit `feat(mfa): AES-GCM secret protector`.

### Task 3: Data model, settings, event codes, migration

**Files:** Create the four entities, three enums, four configurations; Modify `User.cs` (`DateTime? MfaRequiredSince`), `RefreshToken.cs` (`bool MfaVerified`), `IMicroLimsDbContext` + `MicroLimsDbContext.cs` (DbSets `UserMfaCredentials`, `MfaRecoveryCodes`, `MfaChallenges`, `TrustedDevices`; add `typeof(MfaChallenge)` to `AuditCaptureExcludedTypes` with the comment below), `SystemSettingDefinitions.cs`, `backend/MicroLIMS.Shared/Constants/SecurityEventCodes.cs`; Create migration `AddMultiFactorAuthentication`; Test `backend/MicroLIMS.Tests/IntegrationTests/MfaSchemaPostgresTests.cs`

Exclusion comment (approved by the user 2026-11-10): `// short-lived login state; every outcome is recorded by MfaService as an explicit security event`.

**Interfaces — Produces:** entity fields exactly as spec 5.3, with:
- `UserMfaCredential.SecretProtected`, `MfaRecoveryCode.CodeHash`, `MfaChallenge.TokenHash`, `TrustedDevice.TokenHash` → `[AuditRedacted]`.
- Filtered unique indexes on `UserMfaCredentials(UserId)`: `HasFilter("\"Status\" = 1")` (Active) and a second with `"\"Status\" = 0"` (Pending). Enum values: `Pending = 0, Active = 1, Revoked = 2`.
- Unique index on each `TokenHash` and on `MfaRecoveryCodes(UserId, CodeHash)`.
- Setting keys in `SystemSettingKeys`: `MfaPolicy = "security.mfa.policy"`, `MfaRequiredRoles = "security.mfa.requiredRoles"`, `MfaTrustedDeviceHours = "security.mfa.trustedDeviceHours"`, `MfaEnrolmentGraceDays = "security.mfa.enrolmentGraceDays"`; definitions in `SecurityCategory`, `IsGxpRelevant: true`, integer ranges 0–24 and 0–30.
- `SecurityEventCodes`: `MfaEnrolmentStarted`, `MfaEnrolled`, `MfaTokenAssigned`, `MfaVerified`, `MfaFailed`, `MfaChallengeIssued`, `MfaRecoveryCodeUsed`, `MfaRecoveryCodesRegenerated`, `MfaRemovedByUser`, `MfaResetByAdmin`, `TrustedDeviceAdded`, `TrustedDeviceRevoked` (string values `AUTH_MFA_…` / `AUTH_TRUSTED_DEVICE_…` following the file's existing style).

- [ ] **Step 1: Failing `[PostgresFact]` tests:** `SecondActiveCredentialForSameUser_IsRejectedByUniqueIndex`; `PendingAndActiveCredential_CanCoexist`; `RevokedCredentials_AreNotLimited`; `DuplicateTrustedDeviceTokenHash_IsRejected`; `ExistingUsers_HaveNullMfaRequiredSince_AndRefreshTokensDefaultMfaVerifiedFalse`.
- [ ] **Step 2:** Run with `MICROLIMS_TEST_POSTGRES` set → FAIL.
- [ ] **Step 3:** Implement entities/configurations; `dotnet ef migrations add AddMultiFactorAuthentication --project backend/MicroLIMS.Persistence --startup-project backend/MicroLIMS.API`. Inspect `Up()`: it must not drop or rebuild any protected table.
- [ ] **Step 4:** Run `MfaSchemaPostgresTests`, `AuditCredentialRedactionTests`, `AppendOnlyTriggerCatalogPostgresTests`, `ClockUsageTests` → PASS, 0 skipped. `dotnet ef migrations has-pending-model-changes` → none.
- [ ] **Step 5:** Commit `feat(mfa): data model, settings and migration`.

### Task 4: Policy service

**Files:** Create `MfaPolicyService.cs`; register scoped; Test `backend/MicroLIMS.Tests/UnitTests/Mfa/MfaPolicyServiceTests.cs` (pure logic through a stub `ISystemSettingsReader`; no relational behaviour is being proved)

**Interfaces — Produces:**
```csharp
public enum MfaPolicy { Off, Optional, RequiredForRoles, RequiredForAll }
public enum MfaLoginRequirement { None, Verify, EnrolWithinGrace, EnrolNow }
public sealed record MfaRequirement(MfaLoginRequirement Kind, DateTime? EnrolmentDueAtUtc);
public sealed class MfaPolicyService
{
    public Task<MfaPolicy> GetPolicyAsync(CancellationToken ct = default);
    public Task<bool> IsRequiredForRoleAsync(string roleType, CancellationToken ct = default);
    // Pure decision; caller persists User.MfaRequiredSince when the result says it must be set.
    public Task<MfaRequirement> EvaluateAsync(string roleType, bool hasActiveCredential, DateTime? mfaRequiredSinceUtc, DateTime nowUtc, CancellationToken ct = default);
    public Task<int> GetTrustedDeviceHoursAsync(CancellationToken ct = default);
}
```

- [ ] **Step 1: Failing tests (theory rows):** policy `Off` + active credential → `None`; `Optional` + active → `Verify`; `Optional` + none → `None`; `RequiredForRoles` + role listed + none + `MfaRequiredSince` null → `EnrolWithinGrace` due now+7d; same with since = now−8d → `EnrolNow`; grace days 0 → `EnrolNow`; role not listed + none → `None`; `RequiredForAll` + any role → required; unknown policy string in settings → treated as `RequiredForAll` (fail closed); role list with spaces and mixed case matches.
- [ ] **Step 2–4:** FAIL → implement → PASS.
- [ ] **Step 5:** Commit `feat(mfa): policy evaluation`.

### Task 5: Login challenge, verification, trusted computers, recovery codes

**Files:** Create `IMfaService.cs`, `MfaService.cs`, `MfaDtos.cs`, `MfaController.cs`; Modify `IAuthenticationService.cs` (`LoginOutcome`, `LoginAsync`), `AuthenticationService.cs` (`LoginCoreAsync` L80–160, `IssueRefreshTokenAsync` L425), `AuthenticationController.cs` (`LoginRequest` L12, `Login` L33–42); Test `backend/MicroLIMS.Tests/IntegrationTests/MfaLoginPostgresTests.cs`

**Interfaces — Consumes:** `Totp` (Task 1), `ISecretProtector` (Task 2), entities (Task 3), `MfaPolicyService` (Task 4).

**Produces:**
```csharp
public record LoginOutcome(bool Success, string? Token, string? RefreshToken, string? FailureReason,
    bool MustChangePassword = false, string? RoleName = null,
    bool MfaRequired = false, bool MfaEnrolmentRequired = false, string? MfaToken = null,
    DateTime? MfaEnrolmentDueAt = null, string? DeviceToken = null, int? RecoveryCodesRemaining = null);

Task<LoginOutcome> LoginAsync(string username, string password, string? ipAddress = null, string? userAgent = null, string? deviceToken = null);

public interface IMfaService
{
    Task<string> CreateChallengeAsync(int userId, MfaChallengePurpose purpose, CancellationToken ct = default);   // returns raw token
    Task<bool> IsTrustedDeviceAsync(int userId, string? deviceToken, CancellationToken ct = default);
    Task<LoginOutcome> VerifyAsync(string mfaToken, string? code, string? recoveryCode, bool trustDevice, string? ip, string? userAgent, CancellationToken ct = default);
    Task<int> RevokeTrustedDevicesAsync(int userId, int? revokedByUserId, CancellationToken ct = default);          // no SaveChanges; caller's unit of work
}
```
`AuthenticationService` gains an internal `Task<LoginOutcome> CompleteLoginAsync(User user, string? ip, string? userAgent, bool mfaVerified)` holding today's success path (L141–160), which `MfaService` calls after a good code. HTTP: `POST api/auth/mfa/verify` `{ mfaToken, code?, recoveryCode?, trustDevice }`, `[AllowAnonymous]`, `[EnableRateLimiting(RateLimitingPolicies.Authentication)]`. Login and verify responses add `mfaRequired`, `mfaEnrolmentRequired`, `mfaToken`, `mfaEnrolmentDueAt`, `deviceToken`, `recoveryCodesRemaining`.

Behaviour the tests pin: password and lockout checks first and unchanged; a failed verify returns the same `LoginFailedMessage`-style generic text "The code is not correct."; verification and `LastAcceptedStep` update happen in one transaction with a concurrency token on `UserMfaCredential` so two parallel requests with one code cannot both pass; recovery code format `XXXXX-XXXXX` from the 32-character set already used by `AdminPasswordRecoveryService.GenerateRecoveryCode`; a used recovery code emails the user through `IEmailSender` after commit.

- [ ] **Step 1: Failing `[PostgresFact]` tests** (construct services directly with a `FakeTimeProvider`, a fixed test key, and separate contexts):
  - `Login_UserWithActiveCredential_ReturnsChallenge_NoTokens_NoRefreshTokenRow`
  - `Verify_CorrectCode_IssuesTokens_SetsMfaVerified_UpdatesLastAcceptedStep_RecordsMfaVerifiedEvent`
  - `Verify_SameCodeTwice_SecondFails` *(Review Focus 2)*
  - `Verify_WrongCode_IncrementsFailedLoginAttempts_RecordsMfaFailed`
  - `Verify_FifthWrongCode_LocksAccount_AndChallengeIsDead`
  - `Verify_ExpiredChallenge_Fails` (advance clock 5 min 1 s); `Verify_ConsumedChallenge_Fails`
  - `Verify_TrustDevice_ReturnsDeviceToken_StoresOnlyHash_ExpiresAfterConfiguredHours`
  - `Login_WithValidDeviceToken_SkipsChallenge`; `…_Expired_AsksCode`; `…_Revoked_AsksCode`; `Login_WithAnotherUsersDeviceToken_AsksCode` *(Review Focus 3)*
  - `TrustedDeviceHoursZero_NeverIssuesDeviceToken_AndIgnoresExistingOnes`
  - `Verify_RecoveryCode_Succeeds_MarksUsed_ReturnsRemainingCount`; `Verify_RecoveryCodeTwice_SecondFails`
  - `Login_PolicyOff_UserWithCredential_NoChallenge`
  - `Login_RequiredRole_NotEnrolled_InGrace_IssuesTokens_WithDueDate_AndSetsMfaRequiredSince`
  - `Login_RequiredRole_NotEnrolled_AfterGrace_ReturnsEnrolmentRequired_NoTokens`
  - `MfaToken_AsBearerOrRefreshToken_IsRejected` *(Review Focus 1; `WebApplicationFactory` test in `backend/MicroLIMS.Tests/IntegrationTests/MfaEndpointsPostgresTests.cs`: 401 on `api/auth/me` and failure on `api/auth/refresh`)*
- [ ] **Step 2:** Run → FAIL.
- [ ] **Step 3: Implement.**
- [ ] **Step 4:** Run the new classes plus `LoginEnumerationTests`, `SessionRevocationTests`, `AuthenticationRateLimitingTests` → PASS, 0 skipped. Mutation check: remove the `lastAcceptedStep` update → `Verify_SameCodeTwice_SecondFails` must fail.
- [ ] **Step 5:** Commit `feat(mfa): login challenge, trusted computers and recovery codes`.

### Task 6: Self-service enrolment

**Files:** Modify `IMfaService.cs`, `MfaService.cs`, `MfaController.cs`; Create `backend/MicroLIMS.Application/Abstractions/Security/IQrCodeRenderer.cs`, `backend/MicroLIMS.Infrastructure/Security/ZXingQrCodeRenderer.cs`; Test `backend/MicroLIMS.Tests/IntegrationTests/MfaEnrolmentPostgresTests.cs`, `backend/MicroLIMS.Tests/UnitTests/Security/ZXingQrCodeRendererTests.cs`

**Interfaces — Produces:**
```csharp
public sealed record EnrolmentStart(string ManualKey, string OtpAuthUri, string QrSvg);
public sealed record EnrolmentResult(IReadOnlyList<string> RecoveryCodes, LoginOutcome? Login);   // Login set only on the login-time path
public sealed record MfaStatus(bool Enrolled, string? Type, DateTime? ConfirmedAt, int RecoveryCodesRemaining, bool Required, DateTime? EnrolmentDueAt, IReadOnlyList<TrustedDeviceInfo> TrustedDevices);

Task<EnrolmentStart>  StartEnrolmentAsync(int userId, CancellationToken ct = default);
Task<EnrolmentResult> ConfirmEnrolmentAsync(int userId, string code, CancellationToken ct = default);
Task<IReadOnlyList<string>> RegenerateRecoveryCodesAsync(int userId, string password, string code, CancellationToken ct = default);
Task RemoveOwnFactorAsync(int userId, string password, string code, CancellationToken ct = default);
Task<MfaStatus> GetStatusAsync(int userId, CancellationToken ct = default);
Task ForgetOwnTrustedDeviceAsync(int userId, int trustedDeviceId, CancellationToken ct = default);
public interface IQrCodeRenderer { string RenderSvg(string content); }   // black modules on white, viewBox only, includes <title>
```
HTTP (all under `api/auth/mfa`):
- Login-time, `[AllowAnonymous]`, identified by `mfaToken` with purpose `Enrol`: `POST enrol/start`, `POST enrol/confirm` `{ mfaToken, code }` (returns recovery codes **and** the login response).
- Authenticated: `GET status`; `POST enrol/start` `{ password }`; `POST enrol/confirm` `{ code }`; `POST recovery-codes` `{ password, code }`; `DELETE` `{ password, code }`; `DELETE trusted-devices/{id}`. Password-taking actions use `RateLimitingPolicies.PasswordChange`.

- [ ] **Step 1: Failing tests:**
  - `Start_CreatesPendingCredential_SecretIsEncrypted_NotEqualToManualKey`
  - `Start_Twice_ReplacesPending_OnlyOnePendingRow` *(Review Focus 4)*
  - `Start_WhileActive_Fails` (must remove or be reset first)
  - `Confirm_CorrectCode_ActivatesCredential_CreatesTenHashedCodes_RecordsMfaEnrolled`
  - `Confirm_WrongCode_LeavesPending_NoRecoveryCodes`
  - `Confirm_LoginTimePath_ConsumesChallenge_IssuesTokensWithMfaVerified`
  - `Start_Authenticated_WrongPassword_Fails`
  - `Regenerate_InvalidatesOldCodes_ReturnsTenNew`; `Regenerate_WrongCode_Fails`
  - `RemoveOwnFactor_WhenRoleRequired_IsRejected`; `RemoveOwnFactor_WhenOptional_RevokesCredentialCodesAndDevices_RecordsEvent`
  - `ForgetTrustedDevice_OfAnotherUser_IsRejected`
  - `AuditLogs_ForCredentialInsert_ContainRedactedSecret` (assert the `NewValues` JSON holds `[REDACTED]` and not the protected string)
  - Unit: `RenderSvg_DecodesBackToContent` (rasterise modules from the encoder's `BitMatrix`, decode with ZXing) and `RenderSvg_HasTitleAndNoScript`.
- [ ] **Step 2–4:** FAIL → implement (`ZXing.QrCode.QRCodeWriter`, error correction M) → PASS, 0 skipped.
- [ ] **Step 5:** Commit `feat(mfa): self-service enrolment and recovery codes`.

### Task 7: Administrator actions and policy guard

**Files:** Create `MfaAdminService.cs`, `AdminUserMfaController.cs`; Modify `AdminUserSessionsController.cs` + its service (session DTO gains `mfaVerified`; add trusted-device list and revoke), the user list DTO/query (adds `mfaState`), `AccessReviewDtos.cs` + `AccessReviewService.cs` (`AccessReviewUserEntry` gains `string MfaState`), the System Settings write path (guard); Test `backend/MicroLIMS.Tests/IntegrationTests/MfaAdminPostgresTests.cs`

**Interfaces — Produces:**
```csharp
public enum MfaUserState { NotRequired, Enrolled, DueByDate, Overdue, TokenPendingConfirmation }
Task ResetAsync(int targetUserId, string reason, int actingUserId, CancellationToken ct = default);
Task AssignHardwareTokenAsync(int targetUserId, string serialNumber, string seed, bool seedIsHex, int stepSeconds, string reason, int actingUserId, CancellationToken ct = default);
```
HTTP, policy `PermissionConstants.UsersManage`: `POST api/admin/users/{id}/mfa/reset` `{ reason }`; `POST api/admin/users/{id}/mfa/hardware-token` `{ serialNumber, seed, seedFormat: "base32"|"hex", stepSeconds: 30|60, reason }`; `GET api/admin/users/{id}/trusted-devices`; `DELETE api/admin/users/{id}/trusted-devices/{deviceId}`.

Rules: reset and assignment reject `targetUserId == actingUserId`, an empty reason, and a disabled user (same checks and messages style as `AdminPasswordRecoveryService.CreateRecoveryRequestAsync` L53–64). Reset revokes credential, recovery codes, trusted devices and refresh tokens (`RevokeAllRefreshTokensAsync`), sets `MfaRequiredSince` so that grace is already over, writes an `AuditLogs` row `MFA_RESET_BY_ADMIN` with reason and target in the same transaction, and emails the user after commit. A user with a `Pending` hardware credential gets `mfaRequired` at login and the first correct code activates it and returns recovery codes. **Policy guard:** changing `security.mfa.policy` from `Off`/`Optional` to a required value is refused unless at least two active System Administrators have an `Active` credential; message: "Two System Administrators must set up a second factor before it can be required."

- [ ] **Step 1: Failing tests:**
  - `Reset_RevokesCredentialCodesDevicesSessions_WritesAuditRowWithReason` (triad)
  - `Reset_OwnAccount_Throws`; `Reset_EmptyReason_Throws`; `Reset_WithoutUsersManage_Returns403` (endpoint test)
  - `AfterReset_NextLogin_RequiresEnrolmentImmediately`
  - `AssignToken_CreatesPendingCredential_HexAndBase32SeedsGiveSameSecret`
  - `AssignToken_ToSelf_Throws`; `AssignToken_InvalidSeed_Throws_MessageDoesNotEchoSeed`; `AssignToken_Step45_Throws`
  - `AssignedToken_FirstCorrectCode_Activates_AndReturnsRecoveryCodes`
  - `PolicyChangeToRequired_WithOneEnrolledAdministrator_IsRefused` *(Review Focus 5)*; `…_WithTwo_Succeeds`
  - `UserList_ReportsMfaState_ForEachOfTheFiveStates`
  - `AccessReview_UserEntry_IncludesMfaState`
  - `AdminRevokeTrustedDevice_NextLoginAsksCode`
- [ ] **Step 2–4:** FAIL → implement → PASS, 0 skipped, plus existing `AccessReviewPostgresTests`.
- [ ] **Step 5:** Commit `feat(mfa): administrator reset, hardware tokens and policy guard`.

### Task 8: Revocation hooks and startup check

**Files:** Modify `AuthenticationService.cs` (`ChangePasswordAsync` L237, `ConfirmPasswordResetAsync` L294), `AdminPasswordRecoveryService.cs` (`ConfirmRecoveryAsync` L111), `backend/MicroLIMS.API/Program.cs` (startup validation); Test `backend/MicroLIMS.Tests/IntegrationTests/MfaRevocationPostgresTests.cs`

**Interfaces — Consumes:** `IMfaService.RevokeTrustedDevicesAsync`, `ISecretProtector.IsConfigured`.

- [ ] **Step 1: Failing tests:** `ChangePassword_RevokesTrustedDevices`; `PasswordReset_RevokesTrustedDevices_AndNextLoginStillAsksCode`; `AdminPasswordRecovery_DoesNotClearSecondFactor_ButRevokesTrustedDevices`; `Startup_PolicyRequired_NoKey_FailsWithMessageNamingMfaActiveKeyId`; `Startup_PolicyOff_NoKey_Starts`.
- [ ] **Step 2–4:** FAIL → implement → PASS.
- [ ] **Step 5:** Commit `feat(mfa): revoke trusted computers on password events; startup key check`.

### Task 9: Frontend — login step

**Files:** Create `mfaService.ts`, `MfaCodeField.tsx`; Modify `authenticationService.ts` (`login(username, password, deviceToken?)`, extended `LoginResult`), `AuthContext.tsx`, `Login.tsx`; Test `Login.test.tsx`, `MfaCodeField.test.tsx`

**Interfaces — Produces:**
```ts
export interface LoginResult { token?: string; refreshToken?: string; mustChangePassword?: boolean; roleName?: string;
  mfaRequired?: boolean; mfaEnrolmentRequired?: boolean; mfaToken?: string; mfaEnrolmentDueAt?: string; deviceToken?: string; recoveryCodesRemaining?: number; }
export const mfaService = { verify(body: { mfaToken: string; code?: string; recoveryCode?: string; trustDevice: boolean }): Promise<LoginResult>, /* Task 10 adds the rest */ };
// MfaCodeField props: { value: string; onChange(v: string): void; error?: string; disabled?: boolean; autoFocus?: boolean }
```
The device token lives in `localStorage` key `microlims_device_token`; it is **not** cleared on logout or idle timeout (that is the feature) and is cleared when the server ignores it. `mfaToken` is kept in component state only.

- [ ] **Step 1: Failing tests (`userEvent`, real `Login`, mocked `authenticationService`/`mfaService` modules):**
  - `shows the code step when login returns mfaRequired, and does not store tokens`
  - `submits the code and signs in`; `sends the stored device token with the password`
  - `shows "The code is not correct." in an alert and keeps focus in the code field`
  - `switches to the recovery code field and submits it`
  - `stores the device token when "Trust this computer" is ticked`
  - `hides the trust checkbox when the server says trusted hours is 0` (from `GET api/auth/mfa/options`, an anonymous endpoint returning `{ trustedDeviceHours }`; add it to `MfaController` in this task)
  - `returns to the password step when the challenge has expired`
  - `warns how many recovery codes remain after using one`
  - `MfaCodeField` accepts digits only, max 6, has label "6-digit code", `inputMode="numeric"`, `autocomplete="one-time-code"`
- [ ] **Step 2–4:** `cd frontend && npx vitest run src/pages/Login.test.tsx …` FAIL → implement → PASS.
- [ ] **Step 5:** `npm run lint && npx tsc -b && npm run debt:check`. Commit `feat(mfa): login code step`.

### Task 10: Frontend — enrolment and Profile security

**Files:** Create `MfaEnrolment.tsx` (route `/mfa-enrol`, added in `AppRoutes.tsx` beside `/change-password`), `EnrolmentSteps.tsx`, `RecoveryCodesPanel.tsx`; Modify `mfaService.ts`, `Profile.tsx`, `MainLayout.tsx` (grace banner); Tests beside each file

**Interfaces — Consumes:** Task 6 endpoints. `EnrolmentSteps` props: `{ start(): Promise<EnrolmentStart>; confirm(code: string): Promise<{ recoveryCodes: string[] }>; onDone(): void }` so the login-time page and the Profile dialog share it.

- [ ] **Step 1: Failing tests:**
  - `shows the QR image with alt text, and the key as selectable text`
  - `Continue is disabled until "I have saved these codes" is ticked`
  - `wrong code shows an inline error and stays on the code step`
  - `forced enrolment signs the user in after the codes step`
  - `Profile shows status, remaining recovery codes and trusted computers`
  - `Remove is disabled with the reason "Your role requires a second factor" when required`
  - `Regenerate asks for password and code, then shows the new codes once`
  - `Forget removes a trusted computer after confirmation`
  - `grace banner shows the due date with time zone and links to enrolment`
  - loading, empty (no trusted computers) and error states render
- [ ] **Step 2–4:** FAIL → implement (QR SVG rendered through an `<img>` data URI, never `dangerouslySetInnerHTML`; recovery codes in `monospaceFontFamily`; Copy and Print buttons) → PASS.
- [ ] **Step 5:** lint, `tsc -b`, `debt:check`. Commit `feat(mfa): enrolment and profile security`.

### Task 11: Frontend — administration

**Files:** Create `ResetMfaDialog.tsx`, `AssignHardwareTokenDialog.tsx`; Modify `UsersPage.tsx`, `UserSessionsDialog.tsx`, the users service file, the Access Review user line component; Tests beside each

- [ ] **Step 1: Failing tests:**
  - `Users table shows the second-factor state as text for each state`
  - `Reset second factor requires a reason; Cancel is focused first; calls the service with the reason`
  - `Reset is not offered on the signed-in administrator's own row`
  - `Assign hardware token validates serial, seed and step, and never shows the seed after submit`
  - `server refusal is shown as a toast and the dialog stays open`
  - `Sessions dialog shows "Second factor: Yes/No" per session and lists trusted computers with Revoke`
  - `Access review line shows the second-factor state`
- [ ] **Step 2–4:** FAIL → implement (`FormDialog`/`ConfirmationDialog`, status tokens from `theme.custom.status`, `aria-label` with the username on row actions) → PASS.
- [ ] **Step 5:** lint, `tsc -b`, `debt:check`. Commit `feat(mfa): user administration`.

### Task 12: Final verification and documents

**Files:** Create `documents/` entries per the repository's existing naming for requirements and SOP outlines (requirement IDs for each spec section 5.1–5.9; SOP outline: enrolment, lost device, seed custody, time synchronisation, key custody and rotation, sole-administrator procedure); Modify `improvement-roadmap.md` (B2 points to spec and plan)

- [ ] **Step 1:** `dotnet build backend/MicroLIMS.sln --configuration Release` then the full suite twice: without `MICROLIMS_TEST_POSTGRES` (every `[PostgresFact]` Skipped) and with it (0 skipped). Record exact counts.
- [ ] **Step 2:** `dotnet ef migrations has-pending-model-changes` → none.
- [ ] **Step 3:** `cd frontend && npm run lint && npx tsc -b && npm run test && npm run build && npm run debt:check`.
- [ ] **Step 4:** Run the app; with a real authenticator app enrol, log out, log in with a code, trust the computer, use a recovery code, reset from a second administrator. Keyboard-only pass and a screen-reader check of the code step. Report what was and was not exercised.
- [ ] **Step 5:** Commit `docs(mfa): requirements, SOP outline, roadmap links`.

## Manual gates (laboratory, after the plan)

- Enrolment with the site's chosen authenticator app and the chosen hardware token model.
- Server time synchronisation confirmed.
- Encryption key generated, stored and backed up under the key-custody SOP.
- QA approval of the SOP and of the ZXing.Net qualification record (shared with B1).
- Rollout order: `Optional` → two administrators enrolled → `RequiredForRoles`.
