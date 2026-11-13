> **SUPERSEDED (2026-11-13) by `.ai/handoffs/r2-start-2026-11-13.md`.** R1 is finished and approved; the Next action and Blockers below are stale (SSO Tasks 5-7/10 done, agy no longer used). Kept for background only.

# Handoff: R1 improvement programme (B4 QA role, B2 MFA, B3 SSO, B1 labels), 2026-11-13

Written at the end of a long session that delegated slices to Antigravity (`agy`, via the
`agy-bridge` MCP tools) and verified every result itself. Paused on the user's request
because Antigravity ran out of AI credits ("Your AI credits balance is too low to continue").

## Where everything is (all LOCAL, nothing pushed, no PR opened)

| Branch | Head at handoff | Worktree (advisory) | Role |
|---|---|---|---|
| `r1/b4-qa-role` | `85fb106d` + this handoff commit | `I:\MicroLIMS` | **Integration branch.** Everything verified is merged here. |
| `r1/b1-encoder` | `108fbc6e` | `I:\MicroLIMS-wt\b1-encoder` | Backend/model lane (MFA backend, SSO backend). Fully merged into integration. |
| `r1/b4-scope` | `ddd50964` | `I:\MicroLIMS-wt\b4-scope` | Frontend + documents lane. Fully merged into integration. |
| `r1/b4-ui` | `85fb106d` | `I:\MicroLIMS-wt\b4-ui` | Verification-only tree for full frontend runs (`npm ci` done). Fast-forward it from integration before a run. |

Older worktrees `b1-payload`, `b1-services`, `b2-totp` and the two `.claude/worktrees/*` trees
predate this session's later work; do not delete them without checking with the user.

Untracked in `I:\MicroLIMS`: `docs/ux-audit-2026-11-production-data.md`, `docs/ux-audit-queries/`.
They existed before this session and the name suggests production data, so they were
deliberately **not** committed. Ask the user.

Plans: `docs/superpowers/plans/2026-11-12-execution-breakdown.md`, `2026-11-11-build-order.md`,
`2026-11-11-qa-role.md` (B4), `2026-11-10-barcode-labels-scanning.md` (B1),
`2026-11-10-multi-factor-authentication.md` (B2), `2026-11-10-single-sign-on.md` (B3).
Specs beside them under `docs/superpowers/specs/`.

## Status

- DONE B4 QA role: code, plus documents `fd16d536` (URS/FS-SEC-013/014, ADM-011/012,
  AUD-009/010, `documents/UserManual/roles/qa.md`, CONF-186/187, roadmap status).
- DONE B1 barcode labels: scan field, pickers, print offers, label formats page, camera
  scanning (WASM bundled same-origin), documents `519fe3b6`.
- DONE B2 MFA: backend and frontend, documents `04cd0cb9` (URS/FS-SEC-015..022, CONF-188..190,
  `documents/SOP/SOP-Multi-Factor-Authentication.md` with an upgrade-notes section).
- DONE B3 SSO Task 1 (data model + migration `20261113075423_AddSingleSignOn`), Task 2 (mode
  guard, emergency accounts, local-login refusal), Task 3 (signing password: backend
  `2188700a`, screens `0bd7e490`), Task 4 (OpenID Connect backend `108fbc6e`, finished by
  Claude after agy ran out of credits), Task 8 (sign-in screens `b256c528`), Task 9 (admin
  screens `ddd50964`, done by a Sonnet subagent on top of agy drafts).
- NOT STARTED B3 SSO Task 5 (Active Directory sign-in), Task 6 (leavers + migration
  `AddRefreshTokenSessionStart`), **Task 7 (administration API; the Task 9 screens call
  endpoints that do not exist until this lands)**, Task 10 (final verification + documents).
- NOT STARTED B2 Task 12 remainder: suite run with `MICROLIMS_TEST_POSTGRES` unset (every
  `[PostgresFact]` must report Skipped).
- NOT STARTED B1 Task 15 remainder: run with DB unset, Playwright/screenshots step.
- NOT STARTED B4 Task 10 remainder: manual check list for the user (QA user opens laboratory
  actions by direct link; refusal expected).
- NOT STARTED release gate: full backend suite (0 skipped) + `has-pending-model-changes` +
  full frontend run on the final merged head; check the 7 (now 5) backend build warnings
  (two are `IPdfImage.Bounds` obsolete in `ReportTextReader.cs`); R1 summary and user
  approval before R2.

## Decisions (with why)

- OIDC sign-in accepts only users with `SignInMethod == Directory` and never emergency
  accounts (`OidcSignInService.HandleCallbackAsync` and `ExchangeAsync`). Why: an
  administrator may pre-link a Local account's company username before switching; that
  account must not get company sign-in early, and emergency accounts are Local by DB check.
  Refusal records `AUTH_SSO_LOGIN_FAILED_NOT_LINKED`. **Flagged to the user, not yet confirmed.**
- Optional `Sso:Oidc:RedirectUri` added (`SsoOptions`, used by `SsoController.BuildCallbackUri`,
  included in the configuration fingerprint). Why: behind a reverse proxy the request scheme
  and host differ from the address registered at the provider. Blank means build from request.
- `AuthenticationService.CompleteLoginAsync` and `AuthenticationController.ToLoginResponse`
  are `public` (were `internal`). Why: the OIDC service/controller and tests call them; there
  is no `InternalsVisibleTo` in the repo.
- Signing password (`POST api/auth/signing-password`) does not end the current session,
  unlike change password. Why: it is the first-login step for directory users.
- SSO connection-test results are stored as security events `SSO_CONNECTION_TEST_PASSED/_FAILED`
  with `{mode, configFingerprint}` (decided earlier; a deviation from the spec's code list,
  flagged to the user, may be overruled).
- `SsoPendingRequest` and `SsoExchangeCode` remain audited (with `[AuditRedacted]`). Excluding
  them from `AuditCaptureExcludedTypes` needs explicit user approval (CLAUDE.md 3.3.0); asked,
  not answered.
- Task 9 contract guesses (accepted by Claude): switch-on control shows only when mode is
  Off and saves `security.sso.mode` through the existing settings service; company-login
  users are created without a password; link-state labels "Not linked", "Linked, never
  signed in", "Linked", "Sign-in refused"; tab permission `Users.Manage`. **Task 7 must
  return exactly the shapes in the Task 9 brief** (see plan Task 7 "Interfaces - Produces").

## Verification actually run by Claude (this session)

- Backend, filter covering Sso/Oidc/DirectoryUserMatcher/SigningPassword/Signature/Mfa/
  ArchitectureTests/Authentication/Login/SessionRevocation/User/AuditCredentialRedaction/
  Password/AppendOnly (excluding `LoginEnumerationTests`) on `r1/b1-encoder` at `108fbc6e`
  content: **898 passed, 0 failed, 0 skipped** against PostgreSQL 18.4. `LoginEnumerationTests`
  alone: 4 passed. `has-pending-model-changes`: no changes.
- Mutation checks, each caught then restored: exchange-code consumed check removed
  (2 tests failed); Directory-only check removed (1 failed); matcher mismatch inverted
  (2 failed); signing-password "already set" guard disabled (1 failed).
- Frontend on integration head (via `r1/b4-ui` at `85fb106d`'s parent content): `tsc -b` clean,
  lint 0 errors / 413 warnings, `debt:check` pass, **229 files / 1,290 tests passed**, build OK.
  Baseline lowered to fireEventCalls 640, projectModuleMocks 116, anyInSource 293, anyInTests 23.
- Documents: `node scripts/docs/check-documents.mjs documents` exit 0.
- UNVERIFIED: full backend suite on the final merged head (last full run was on `5c813e0f`:
  5,177 passed / 1 failed `RetireCalibrationRunsMigrationTests.Migration_RepairsMisSavedHplcTest_AndKeepsItsData`,
  which passed when rerun alone; cause not captured). No run with DB unset. OIDC never run
  against a real provider. Task 9 screens never run against a backend (Task 7 not built).
  Mutation checks for Task 8/9 frontend were reported by the workers, not rerun by Claude.

## Environment gotchas

- Test DB: private PostgreSQL 18 data dir
  `C:\Users\moham\AppData\Local\Temp\claude\03f6d0c5af7613cf\07038e1c-8d53-41d9-b632-d313b994ae69\scratchpad\pgtest\data`,
  port 54329, role `microlims_test` (CREATEDB, trust on localhost). **Stopped at handoff;
  leftover `microlims_test_*` databases dropped (0 left).** Start:
  `"C:/Program Files/PostgreSQL/18/bin/pg_ctl" -D "<that data dir>" -o "-p 54329" -l "<that dir>/../pg.log" start`.
  If the temp dir was cleaned, create a new cluster with `initdb` + role `microlims_test LOGIN CREATEDB`.
  Never start other PostgreSQL instances; never run `dotnet ef database update` on a non-test DB.
- Env for backend tests: `MICROLIMS_TEST_POSTGRES="Host=127.0.0.1;Port=54329;Database=postgres;Username=microlims_test"`,
  `Jwt__Key="$(openssl rand -base64 48)"`.
- Build/test with an artifacts path (a running API locks bin):
  `dotnet build backend/MicroLIMS.sln --configuration Release --artifacts-path C:\Users\moham\AppData\Local\Temp\mlims-b2-totp`
  then `dotnet test backend/MicroLIMS.Tests --configuration Release --no-build --artifacts-path <same> --filter ...`.
- `has-pending-model-changes` needs `DOTNET_ROOT='C:\Users\moham\.dotnet' DOTNET_ROLL_FORWARD=Major`.
- `LoginEnumerationTests` is a timing test: run it alone, exclude it from grouped runs.
- `AuthorizationMatrixTests` reads `ArchitectureTests/authorization-matrix.txt` from source;
  every new endpoint needs a line there (ordinal sort by controller).
- agy: `mcp__agy-bridge__delegate` / `follow_up`; calls go to background after 120 s; a
  "failed" status often means a truncated report, so always inspect the worktree. **TaskStop
  on an agy task does not stop agy writing** (it kept editing for minutes); wait for the
  worktree to go quiet before giving it to anyone else. Sessions: backend lane
  `d90a838a-e325-4a19-a02c-7e827bc762e7`, frontend/docs lane `9e23ba2f-4640-4d73-89a3-7d04bb8f7e6c`.
- User instructions in force: delegate to agy with at most two workers; Claude prepares
  briefs and verifies everything; local commits only, no push/PR unless asked; commit
  trailer `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>` (latest attribution).

## Open questions for the user (none answered yet)

1. CONF-188: MFA policy defaults to RequiredForRoles, so an upgraded install will not start
   without the MFA key (upgrade note written in the SOP).
2. CONF-189: default MFA required roles omit the new QA role.
3. CONF-190: a signed-in user can replace an admin-assigned pending hardware token by
   self-enrolling an authenticator app.
4. OIDC refuses Local accounts that carry a company username (decision above).
5. Exclude `SsoPendingRequest`/`SsoExchangeCode` from audit capture? (needs approval)
6. Connection-test results as security events (decision above).
7. Older: CONF-183..187; five endpoint-policy-only actions without section check; scan
   endpoint rate limit; Arabic-layout scans; Destroyed media lots; `Cryovials.Code` varchar(250);
   QA-required doc type blocking in-flight revisions; audit-review cutoff race.

## Blockers

- Antigravity credits exhausted. Unblocked by: the user topping up / the quota resetting.
  Until then the user asked to pause (or use a Sonnet subagent for in-flight work only).

## Next action

1. `cd I:\MicroLIMS && git branch --show-current` must print `r1/b4-qa-role`; `git log --oneline -3`.
2. Ask the user whether agy is back. If yes: start the test DB (above), then dispatch SSO
   **Task 7 (administration API)** to the backend lane in `I:\MicroLIMS-wt\b1-encoder`
   (first `git -C I:\MicroLIMS-wt\b1-encoder merge --ff-only r1/b4-qa-role`), with the exact
   response shapes the Task 9 screens expect (`frontend/src/modules/admin/services/SsoAdminService.ts`,
   `frontend/src/modules/users/services/UserService.ts` are the contract). In parallel the
   docs lane can take B3 Task 10 documents for Tasks 1-4/8/9.
3. Then Task 5 (AD), Task 6 (leavers), Task 10, then the release gate listed under Status.

## Skills Review

- ✅ delegate: every agy result reviewed with `git diff` and re-verified by own runs before commit.
- ⚠️ delegate: TaskStop does not stop agy; a stopped run left drafts that collided with a Sonnet agent. Wait for a quiet worktree before reassigning.
- ✅ verification-before-completion: one test run was discarded because the build had failed and the counts came from a stale assembly; rerun after fixing.
- ⚠️ CLAUDE.md 4.4 triad: the worker's OIDC tests lacked the AuditLogs assertion for the subject binding; added by Claude. Check every worker test file for the triad, not just counts.
