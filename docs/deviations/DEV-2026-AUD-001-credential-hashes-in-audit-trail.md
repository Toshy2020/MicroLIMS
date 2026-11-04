# DEV-2026-AUD-001 - Credential hashes captured in the audit trail

| Field | Value |
|---|---|
| Deviation ID | DEV-2026-AUD-001 |
| System | MicroLIMS (backend: ASP.NET Core / EF Core / PostgreSQL) |
| Category | Data integrity / information security (audit trail content) |
| Detected | Security review, findings H3 / N2 |
| Decision | Owner decision D4: force every user to reset their password; record this deviation |
| Status | Corrective actions implemented in code; QA approval pending |
| Convention note | `docs/` has no prior deviation template; the release-level logs (for example `docs/Release_1c_Deviation_Log.md`) are release scoped. This record is a stand-alone, per-event record. |

## 1. Description

The automatic audit capture in `MicroLimsDbContext` (`backend/MicroLIMS.Persistence/DbContext/MicroLimsDbContext.cs`) serialises every changed property of an audited entity into `AuditLogs` (previous/new value JSON) and `AuditEventChanges`. Before the `[AuditRedacted]` fix it did not exclude credential-bearing properties, so the following values were written to the audit trail in clear (as stored hashes) whenever the owning row was created or changed:

| Entity.Property | Content | Hash algorithm |
|---|---|---|
| `User.PasswordHash` | Login password | BCrypt, default work factor (`BCrypt.Net-Next`, `AuthenticationService.cs`, `AdminPasswordRecoveryService.cs:216`) |
| `PasswordHistory.PasswordHash` | Previous passwords | BCrypt |
| `PasswordResetToken.TokenHash` | Self-service reset token | SHA-256 of a random token, hex (`AuthenticationService.Hash`, line ~428) |
| `AdminPasswordRecovery.CodeHash` | Administrator-issued recovery code | SHA-256 of the normalised code, hex (`AdminPasswordRecoveryService.HashRecoveryCode`, line 40) |
| `InstrumentAgent.KeyHash` | Instrument-agent API key | SHA-256 of the full key, hex (`InstrumentAgentService.HashKey`, line 36) |

Plain-text passwords, tokens and keys were never stored in the audit trail; only the hashes were.

## 2. Impact assessment

Who could read the data: only holders of the `Audit.View` permission (`AuditController`, `[Authorize(Policy = PermissionConstants.AuditView)]`). By default that is the System Administrator role and the other roles listed with `AuditView` in `backend/MicroLIMS.Shared/Constants/PermissionConstants.cs`. The audit trail is not exposed to unauthenticated users. Database administrators with direct table access could also read the rows (as they could read the source tables).

Risk by credential type:

- BCrypt password hashes (`User`, `PasswordHistory`): offline guessing is possible but slow; weak passwords remain at risk. Exposure includes historical passwords, which matters for users who reuse passwords.
- SHA-256 reset tokens and recovery codes: these are short-lived (recovery codes live 15 minutes, `AdminPasswordRecoveryService.cs:73`) and single-use, so the residual risk is limited to hashes captured while still valid. Random high-entropy tokens cannot be recovered from a SHA-256 hash.
- SHA-256 agent keys (`InstrumentAgent.KeyHash`): the keys are 32 random bytes, so a hash cannot be reversed. The authenticator compares the hash itself (`AgentKeyAuthenticationHandler.cs:64`), but the hash is not the key and cannot be presented as the key. Risk is low; the key is re-issued as a precaution.
- No evidence of misuse was identified at the time of writing. QA/IT security should confirm by reviewing who held `Audit.View` and the access log for the audit endpoints since the first affected row.

Data integrity of GxP records was not affected. Only the confidentiality of credential material was.

## 3. Root cause

The audit capture was a generic "serialise all properties" mechanism with no concept of a sensitive property. New credential columns were audited by default, and nothing in review or automated testing detected a credential-bearing property flowing into `AuditLogs`.

## 4. Immediate actions

1. Redaction fix: commit `5124d192` ("fix(audit): redact credential hashes in the automatic audit capture"). `[AuditRedacted]` (`backend/MicroLIMS.Domain/Attributes/AuditRedactedAttribute.cs`) is applied to the five properties above and honoured by `MicroLimsDbContext` (`IsAuditRedacted`), so new rows record a placeholder value instead of the hash.
2. Forced password reset for every user: migration `20261027033154_ForcePasswordResetAfterAuditHashExposure` sets `Users.MustChangePassword = TRUE` for every user, including inactive accounts so that a reactivated account cannot return with an exposed password. The reset is enforced on the server: `MustChangePasswordFilter` (`backend/MicroLIMS.API/Filters/MustChangePasswordFilter.cs`) returns 403 for every authenticated request except change-password, `/me` and logout, so a client that ignores the flag cannot keep working.
3. Refresh-token revocation: the same migration sets `RefreshTokens.RevokedAt` for every unrevoked token (rows are kept). Sessions cannot be renewed; users sign in again and are routed to change their password. Already-issued access tokens (short-lived) are neutralised by the same filter, which re-reads the flag from the database on each request, so no separate access-token invalidation was added.
4. Audit record of the action: the migration writes one `AuditLogs` row (ActorType System, ActionCategory Security, `SystemProcessName` "Migration 20261027033154_ForcePasswordResetAfterAuditHashExposure", action `FORCED_PASSWORD_RESET`) with the reason "Forced password reset: credential hashes were captured in the audit trail before redaction (deviation DEV-2026-AUD-001)" and counts of users affected and tokens revoked.
5. Instrument agent re-key (manual step, not done by the migration). A new key is shown to an administrator only once, and revoking an agent would force a new agent code, so SQL cannot do this safely. For every row of `InstrumentAgents` with `IsActive = true`, an administrator with access to the equipment calls the key-rotation endpoint (`InstrumentAgentService.RotateKeyAsync`, writes audit event `INSTRUMENT_AGENT_KEY_ROTATED`), then installs the new key on the on-site agent PC. The old key stops working immediately. Record completion per agent below.

   | Agent code | Rotated by | Date | New key installed (initials) |
   |---|---|---|---|
   | | | | |

6. Deployment note: after the migration is applied, users must be told to expect a forced password change at next sign-in. A user's new password must pass the existing password policy and history checks.

## 5. Why the existing audit rows are retained unmodified

The affected `AuditLogs` / `AuditEventChanges` rows are append-only (database trigger `prevent_audit_modification`) and are deliberately not edited, redacted retroactively or deleted:

- 21 CFR Part 11 section 11.10(e): audit trails must be secure, computer-generated and time-stamped, and record changes must not obscure previously recorded information; audit trail documentation must be retained for the period of the underlying records.
- EU GMP Annex 11 section 9: changes and deletions of GMP-relevant data must be recorded in an audit trail; the trail must not be alterable.

Altering historical audit entries to remove the hashes would itself be an audit-trail manipulation and would need its own justification. The risk is controlled instead by retiring the exposed credentials (items 2, 3 and 5) and by restricting access to the trail (`Audit.View`). The hash values remaining in historical rows are therefore of no use for authentication once the reset is complete.

## 6. CAPA / preventive action

- Corrective: items 1 to 5 above.
- Preventive: architecture test `EveryCredentialLookingStringProperty_IsRedactedOrExplicitlyAllowlisted` (`backend/MicroLIMS.Tests/UnitTests/AuditCredentialRedactionTests.cs`) fails the build when any audited string property whose name looks like credential material is neither `[AuditRedacted]` nor in the reviewed allow-list (each allow-list entry needs a stated reason; a companion test checks the allow-list is valid).
- Process: a reviewer adding a new credential, token or key column must mark it `[AuditRedacted]`; the test above enforces this.
- Effectiveness check: confirm after deployment that new `AuditLogs` rows for password changes, reset requests and key rotations show the redaction placeholder and not a hash.

## 7. Sign-off

| Role | Name | Signature | Date |
|---|---|---|---|
| Author / System owner | | | |
| IT security | | | |
| Quality Assurance (approval of deviation and CAPA) | | | |
| Quality Assurance (closure after effectiveness check) | | | |
