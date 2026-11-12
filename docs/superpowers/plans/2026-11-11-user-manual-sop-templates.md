# User Manual, SOP Templates and Screenshots (A3) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Move the guide into `documents/UserManual`, add five task-based role guides and nine SOP templates, and regenerate every screenshot with a Playwright script that builds its own demo data through the API.

**Architecture:** Documents are markdown under `documents/`. A standalone Node project `tools/screenshots/` (Playwright) seeds demo data via the REST API against a local throwaway database and captures pictures from a declarative shot list. The A1 document checker gains manual and template checks.

**Tech Stack:** Markdown; Node 22; Playwright (dev dependency of `tools/screenshots` only); existing API; GitHub Actions (manual job).

**Spec:** `docs/superpowers/specs/2026-11-11-user-manual-sop-templates-design.md`

## Global Constraints

- No production code changes in `backend/` or `frontend/src`. No demo seeder in the product. No direct database inserts for demo data.
- Demo names only: "Product A", "Lot L-0001", "SMP-0001", "Analyst A". Never the words "validated", "compliant" as a claim about the system.
- The capture tool refuses any API or app address whose host is not `localhost` or `127.0.0.1`.
- No passwords committed: demo passwords are generated per run and kept in memory; the initial admin password comes from env `Seed__InitialAdminPassword` set by the run script to a random value.
- Role names exactly: System Administrator, Section Head, Reviewer, Analyst, QC Manager.
- Chapter numbers and file names `NN-<slug>.md` unchanged. Screenshot IDs `SS-NNN` and file names `ss-NNN-<slug>.png` unchanged.
- SOP templates: first line "Template. Not an approved procedure until completed and approved by the site."; site values as `[SITE TO DEFINE: …]`; system defaults as "current default — confirm" and read from code, never from memory.
- Capture settings: viewport 1440×900, `deviceScaleFactor` 2, light theme (`localStorage["microlims-theme-mode"]`), `reducedMotion: "reduce"`, timezone `Africa/Cairo`, locale `en-GB`.
- Every role-guide step checked against code; unconfirmed steps marked `> **Note for reviewers:** …`.
- Plain English, short sentences. English only.

## Review Focus

1. Capture tool pointed at a real system → `refuses_non_local_address` (Task 3).
2. A shot silently skipped leaves an old picture in place → `missing_shot_is_reported_and_exit_code_nonzero` (Task 5).
3. A manual link to a picture that does not exist after the move → `image_link_without_file_fails` (Task 1).
4. Demo sample reviewed by the same user who tested it (setup would fail on SoD and stop the run) → `review_and_approval_use_different_users` (Task 4).
5. A template shipped with a wrong system default (for example lock time) → `template_defaults_match_code` (Task 8): the check reads the named constants and compares.

## File Structure

| File | Responsibility |
|---|---|
| `documents/UserManual/reference/*.md` | The 44 chapters (moved) |
| `documents/UserManual/roles/*.md` | Five role guides |
| `documents/UserManual/README.md`, `USER_MANUAL.md` | Index; combined file (generated) |
| `documents/Screenshots/*.png`, `shots.json` | Pictures; shot list |
| `documents/SOP/templates/SOP-TPL-0N-<slug>.md` | Nine templates |
| `scripts/docs/check-documents.mjs` (modify) | Manual, picture and template checks |
| `scripts/docs/build-manual.mjs` | Build `USER_MANUAL.md` |
| `tools/screenshots/package.json`, `src/config.ts`, `src/api.ts`, `src/demo-data.ts`, `src/capture.ts`, `src/shots.ts`, `run.sh` | Capture tool |
| `.github/workflows/screenshots.yml` | Manual job |

---

### Task 1: Move the guide and pictures; manual checks

**Files:**
- `git mv documentation/guide/NN-*.md documents/UserManual/reference/`; `git mv documentation/screenshots/* documents/Screenshots/`; `documentation/guide/_ss/*`, `WRITING_BRIEF.md`, `REVIEW_GUIDE_BRIEF.md` → `documents/UserManual/_authoring/`; `documentation/SCREENSHOT_PLAN.md` → `documents/Screenshots/SCREENSHOT_PLAN.md`; `documentation/USER_GUIDE.md` deleted (replaced by generated `USER_MANUAL.md`)
- Create: pointer `README.md` in `documentation/guide/` and `documentation/screenshots/`; `documents/UserManual/README.md`; `scripts/docs/build-manual.mjs`
- Modify: `scripts/docs/check-documents.mjs` and its test; image links in chapters (`../screenshots/` → `../../Screenshots/`); `docs/superpowers/specs/2026-11-11-in-app-help-design.md` and `docs/superpowers/plans/2026-11-11-in-app-help.md` (replace `documentation/guide` with `documents/UserManual/reference`, `documentation/screenshots` with `documents/Screenshots`)

**Interfaces:**
- `checkManual(documentsDir): string[]` — errors for: broken relative link between markdown files; image link without file; image file under `Screenshots/` referenced by no manual file and not in `shots.json` (warning list, not error, until Task 5); role-guide task (`### ` heading under `roles/`) with no link into `reference/`.
- `buildManual(documentsDir): string` — README intro, role guides in the order of the role list, then chapters in number order; `--check` fails if the committed file differs.

- [ ] **Step 1:** Failing tests: `image_link_without_file_fails`, `broken_relative_link_fails`, `role_task_without_reference_link_fails`, `placeholder_screenshot_marker_is_allowed` (`[SCREENSHOT SS-NNN]` text is not a link), `build_manual_is_stable`.
- [ ] **Step 2:** Run → FAIL. **Step 3:** Implement and move files. **Step 4:** `node --test scripts/docs`, `check-documents.mjs documents`, `build-manual.mjs --check` → PASS.
- [ ] **Step 5:** `grep -rn "documentation/guide\|documentation/screenshots" --include=*.md --include=*.ts --include=*.tsx --include=*.yml .` → only pointer files and historical docs remain; fix the rest. Commit `docs: move user guide and screenshots into documents/`.

### Task 2: Role guides

**Files:** Create `documents/UserManual/roles/{analyst,reviewer,section-head,qc-manager,system-administrator}.md`

Task format:

```markdown
### Receive a sample
<!-- requirements: URS-RCV-001, URS-RCV-004 -->
**When:** …
**Before you start:** …
1. …
**The system checks:** …
**What happens next:** …
**More detail:** [12. Sample receiving](../reference/12-sample-receiving.md#…)
```

- [ ] **Step 1:** For each role, list the tasks of spec 5.2. For each task read the reference chapter, then confirm in code: the permission (controller `[Authorize]` policy and service scope check), the refusal messages (exact text thrown), the status change. Use a Haiku Explore agent per role for the reading.
- [ ] **Step 2:** Write the file. Quote refusal messages exactly. Mark unconfirmed steps for reviewers. If A1 is not complete, leave the `requirements` comment as `<!-- requirements: pending A1 -->`.
- [ ] **Step 3:** Record each chapter error found in `documents/UserManual/_authoring/corrections.md` (chapter, line, what the code does) and fix the chapter.
- [ ] **Step 4:** Checks pass; rebuild `USER_MANUAL.md`. One commit per role: `docs(manual): <role> guide`.

### Task 3: Capture tool skeleton and safety

**Files:** Create `tools/screenshots/package.json` (private; scripts `capture`, `test`; devDependencies `playwright`, `typescript`, `tsx` with exact versions), `tsconfig.json`, `src/config.ts`, `src/config.test.ts`, `run.sh`, `.gitignore` (`node_modules`, `out/`)

**Interfaces:**
- `loadConfig(env: NodeJS.ProcessEnv): { apiBaseUrl: string; appBaseUrl: string; adminPassword: string; outDir: string }` — throws `Error("Screenshots can only be captured against localhost.")` unless both hosts are `localhost` or `127.0.0.1`; throws when `adminPassword` is empty.
- `run.sh`: creates database `microlims_shots_<timestamp>` on local PostgreSQL, exports a random `Seed__InitialAdminPassword` and `Jwt__Key`, starts the API (`dotnet run --project backend/MicroLIMS.API`, Development) and the frontend (`npm run dev` with `VITE_API_BASE_URL`), waits for health, runs `npm run capture`, stops both, drops the database (also on failure, via `trap`).

- [ ] **Step 1:** Failing tests (`node --test` via `tsx`): `refuses_non_local_address` (e.g. `https://lims.example.com`), `accepts_localhost_and_127`, `refuses_empty_admin_password`. **Step 2:** FAIL. **Step 3:** Implement. **Step 4:** PASS.
- [ ] **Step 5:** Commit `tools: screenshot capture skeleton with localhost guard`.

### Task 4: Demo data through the API

**Files:** Create `tools/screenshots/src/api.ts`, `src/demo-data.ts`, `src/demo-data.test.ts`

**Interfaces:**
- `class ApiSession { static login(cfg, username, password): Promise<ApiSession>; get<T>(path): Promise<T>; post<T>(path, body, opts?: { ifMatch?: string }): Promise<T>; put<T>(…): Promise<T> }` — throws `ApiError(status, errors: string[], path)` on a non-success `ApiResponse`.
- `buildDemoData(cfg): Promise<DemoContext>`; `DemoContext = { users: Record<"admin"|"sectionHead"|"analystA"|"analystB"|"reviewer"|"qcManager", { username: string; password: string }>; itemId: number; sampleIds: Record<"received"|"inTesting"|"awaitingReview"|"approved"|"oos", number>; mediaLotId: number; equipmentId: number }`.
- Steps inside, in order, each a named function that logs its name: change the seeded admin's password (forced first-login change); create the five users and assign roles and sections; as Section Head: configure specifications and tests on the seeded example item, release a media lot, confirm equipment; as Analyst A: receive five samples, record tests on four; as Reviewer: review; as Section Head / QC Manager: approve one; one sample given an out-of-limit count.
- **Before writing each step, read the controller and DTO it calls** (routes and property names are never guessed); list the endpoints used at the top of `demo-data.ts`.

- [ ] **Step 1:** Failing tests against a fake `ApiSession` recorder: `review_and_approval_use_different_users` (the session used for review and for approval differs from the one that recorded results), `steps_run_in_dependency_order`, `api_error_names_the_failing_step`.
- [ ] **Step 2:** FAIL. **Step 3:** Implement. **Step 4:** PASS.
- [ ] **Step 5:** Run `./run.sh` up to the end of demo data (flag `--data-only`); every step succeeds against the real API. If a step cannot be done through the API (a missing endpoint or a rule that blocks demo setup), stop and report it; do not work around it with SQL.
- [ ] **Step 6:** Commit `tools: demo data setup through the API`.

### Task 5: Shot list and capture

**Files:** Create `documents/Screenshots/shots.json`, `tools/screenshots/src/shots.ts`, `src/capture.ts`, `src/capture.test.ts`

**Interfaces:**
- Shot: `{ id: "SS-051"; file: "ss-051-sign-in.png"; role: keyof DemoContext["users"] | "anonymous"; route: string; actions?: Action[]; waitFor: { role?: string; name?: string; text?: string }; mask?: string[]; fullPage?: boolean }`; `Action = { click: { role: string; name: string } } | { fill: { label: string; value: string } } | { press: string }`. `route` may contain `{sampleIds.approved}`-style tokens resolved from `DemoContext`.
- `validateShots(shots, planIds: string[]): string[]` — duplicate ID or file; ID not in `SCREENSHOT_PLAN.md`; plan ID with no shot and no entry in `shots.json.notCapturable` (`{ id, reason }`).
- `captureAll(cfg, ctx, shots): Promise<{ taken: string[]; failed: { id: string; reason: string }[] }>`; process exits 1 when `failed` is non-empty and writes `out/report.md`.
- Locators use accessible roles and names only (no CSS classes).

- [ ] **Step 1:** Failing tests: `duplicate_shot_id_fails`, `plan_id_without_shot_or_reason_fails`, `route_tokens_are_resolved`, `missing_shot_is_reported_and_exit_code_nonzero` (a `waitFor` that never appears → listed in `failed`, old file not left as if current), `mask_is_applied_to_changing_timestamps`.
- [ ] **Step 2:** FAIL. **Step 3:** Implement. **Step 4:** PASS.
- [ ] **Step 5:** Fill `shots.json` for all 123 plan IDs from `SCREENSHOT_PLAN.md` and the `_authoring/_ss` fragments (procedure and controls columns give the actions and `waitFor`). A feature that does not exist goes to `notCapturable` with the reason.
- [ ] **Step 6:** Run `./run.sh`; look at every produced picture; fix shots until `failed` is empty. Run twice and confirm the second run changes no file (`git status` clean). Report how many of 123 were taken and list the rest.
- [ ] **Step 7:** Replace `[SCREENSHOT SS-NNN]` placeholders in the manual with image links for pictures now taken, with alt text from the plan's Purpose column. Promote the unused-picture warning of Task 1 to an error. Commit `docs: regenerate screenshots from script`.

### Task 6: Manual CI job

**Files:** Create `.github/workflows/screenshots.yml` (`workflow_dispatch` only): PostgreSQL service, .NET and Node setup as in `ci.yml`, `npx playwright install --with-deps chromium`, `tools/screenshots/run.sh`, upload `documents/Screenshots` and `out/report.md` as an artifact. It does not commit.
Modify: `.github/workflows/ci.yml` — add `node --test` for `tools/screenshots` unit tests (no browser) to the documents job.

- [ ] **Step 1:** Add both. **Step 2:** Trigger the manual job once; record the result honestly, including if it could not be triggered from this environment. **Step 3:** Commit `ci: manual screenshot job`.

### Task 7: SOP templates

**Files:** Create `documents/SOP/templates/SOP-TPL-01-system-use.md`, `-02-account-management.md`, `-03-electronic-signatures.md`, `-04-audit-trail-review.md`, `-05-backup-and-restore.md`, `-06-periodic-review.md`, `-07-incident-handling.md`, `-08-change-control-and-release.md`, `-09-business-continuity.md`, and `documents/SOP/README.md`

Headings in every template, in order: header table; `## 1. Purpose`; `## 2. Scope`; `## 3. Responsibilities`; `## 4. Definitions`; `## 5. Procedure`; `## 6. Records`; `## 7. References`; `## 8. Change history`.

Read before writing (facts come only from these):

| Template | Read |
|---|---|
| 01 | Role guides (Task 2); `frontend/src/hooks/useIdleTimeout.ts` |
| 02 | `UserService`, `RoleService`, `AccessReviewService`, `AuthenticationService` (lock rules), `backend/MicroLIMS.Shared/Validation/PasswordPolicy.cs`, `SystemSettingDefinitions` |
| 03 | `ElectronicSignatureService`; where signatures are shown on reports (`CertificateOfAnalysisBuilder`) |
| 04 | `AuditSearchService`, `AuditExportService`, `frontend/src/modules/auditSearch` |
| 05 | `scripts/DatabaseBackup/backup.sh`, `scripts/Restore/restore.sh`, `BackupRun` entity and its service |
| 06 | `PeriodicReviewService` (state what it covers; if it is Document Control only, say so), `AccessReviewService`, `ErrorMonitoringService` |
| 07 | `ErrorCaptureService`, `ErrorMonitoringService`, `Incident` entity, `frontend/src/modules/errorMonitoring` |
| 08 | `.github/workflows/ci.yml`, `scripts/Deployment/deploy.sh`, `scripts/Migration/apply-migrations.sh`, A2 validation documents if present |
| 09 | Correction and late-entry behaviour in the result services (reason required, audit); no paper forms exist in the repository → forms are `[SITE TO DEFINE]` |

- [ ] **Step 1:** Write each template. Procedure steps name the screen or function. Site decisions as `[SITE TO DEFINE: …]`. System defaults written as `<setting key> — current default <value> — confirm`.
- [ ] **Step 2:** Template 09: the rule that paper results are entered afterwards by the original analyst with a reason and a second-person check is a **proposal**; mark it `[SITE TO DEFINE: confirm rule]`, because no approved rule exists in `documents/`.
- [ ] **Step 3:** Commit `docs: SOP templates`.

### Task 8: Template checks

**Files:** Modify `scripts/docs/check-documents.mjs` and its test

**Interfaces:** `checkSopTemplates(documentsDir, repoRoot): string[]` — errors for: missing first line; missing or out-of-order heading; a "current default" line whose value differs from the code. Defaults map in the checker: `{ key, file, pattern }` entries for password minimum length (`PasswordPolicy.cs`), lock duration (`AuthenticationService.cs` `LockDuration`), idle timeout (`useIdleTimeout.ts`); each `pattern` is a regex capturing the number.

- [ ] **Step 1:** Failing tests: `template_without_notice_line_fails`, `template_missing_heading_fails`, `template_defaults_match_code` (fixture says lock 30 minutes while fixture code says 15 → error naming both), `default_pattern_not_found_in_code_fails` (so a renamed constant is noticed).
- [ ] **Step 2:** FAIL. **Step 3:** Implement. **Step 4:** PASS on fixtures and on the real templates. Commit `ci: SOP template checks`.

### Task 9: Close-out

- [ ] **Step 1:** Update `documents/README.md` (manual, pictures, templates with status Draft / Template) and the stub READMEs.
- [ ] **Step 2:** Run `node --test scripts/docs`, `check-documents.mjs documents`, `build-manual.mjs --check`, `tools/screenshots` unit tests; frontend and backend suites are not affected (no product code changed) — state that they were not run, or run them and report counts.
- [ ] **Step 3:** Report to the user: pictures taken out of 123 and the not-capturable list with reasons; chapter corrections made; steps still marked for reviewers; every `[SITE TO DEFINE]` item per template; the three open items of the spec.

## Cross-plan corrections (whole-programme review, 2026-11-11)

These corrections come from reviewing all plans together against the code. Where they differ from the text above, they win. Shared names are defined once in `docs/superpowers/plans/2026-11-11-build-order.md` (section "Programme conventions").

1. Role guides: add `qa.md` when B4 is built and `rd-manager.md` when D3 is built; the Global Constraint's five role names become seven at that point.
2. Task 3 `run.sh` interface includes the flag `--data-only` (stop after demo data) used in Task 4 Step 5.
3. Task 8 reads the lock duration and maximum failed attempts from the defaults in `SystemSettingDefinitions` (keys `LockoutDurationMinutes`, `LockoutMaxFailedAttempts`), not from the `AuthenticationService` constants, because the settings are what the system uses at run time.
4. B1's label screenshots are captured by this tool as entries in `shots.json`, not written by B1 directly.
