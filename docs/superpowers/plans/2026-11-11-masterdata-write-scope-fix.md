# Test Configuration Write Scope Fix (G5) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Enforce laboratory scope on seven test-configuration writes and version checks on five of them.

**Architecture:** Give `TestWorkflowStepMasterDataService` the scope service and the acting user; add the user id to `FreezeTestDefinitionAsync` / `UnfreezeTestDefinitionAsync`; reuse the exact scope check and `RecordVersion.EnsureCurrent` that `UpdateTestDefinitionAsync` already uses. Frontend sends `If-Match` on the five actions.

**Tech Stack:** .NET 10, EF Core 10, PostgreSQL, xUnit `[PostgresFact]`; React 19, TypeScript, Vitest.

**Spec:** `docs/superpowers/specs/2026-11-11-masterdata-write-scope-fix-design.md`

## Global Constraints

- `CLAUDE.md` binds every task: separate DbContexts and `AsNoTracking` in assert; state + audit + negative checks; `[PostgresFact]` must run (Skipped > 0 is a failure); `CancellationToken` forwarded on every changed signature; no new `any`; `userEvent`; mock only `services/`.
- Copy the scope check and its refusal (exception type and message) from `UpdateTestDefinitionAsync` (`backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs`, around lines 444–448), including how a null `SectionId` is treated. Do not invent a new message or rule. Extract it to one private helper `EnsureCanConfigureTestAsync(int currentUserId, TestDefinition test, CancellationToken ct)` shared by both services (place it in a small internal static helper or on `IUserSectionScopeService` usage — one implementation, not two copies).
- No schema change, no migration, no permission change.
- `GET …/steps` is not changed.

## Review Focus

1. A step whose test has `SectionId = null` behaves differently from the test update → `Step_write_on_test_without_section_follows_test_update_rule` (Task 1).
2. Move checks the moved step's version, but the step it swaps with was changed meanwhile → `Move_with_stale_version_of_moved_step_refused`; the swapped step's change is not lost because both rows are saved in one transaction (existing `UnitOfWork.RunAsync`) — assert the neighbour's other fields are unchanged.
3. Delete of a step that another user just changed → `Delete_with_stale_version_refused` (Task 1).
4. Unfreeze by a Section Head of another laboratory reactivates a test they cannot configure → `Unfreeze_by_other_laboratory_refused` (Task 2).
5. The screen keeps showing the old version after a successful move, so the next move is refused → frontend test `uses returned version for the next action` (Task 3).

## File Structure

| File | Change |
|---|---|
| `backend/MicroLIMS.Application/Services/MasterData/TestWorkflowStepMasterDataService.cs` | Constructor gains `IUserSectionScopeService`; each write gains `int currentUserId` and `CancellationToken` |
| `backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs` | Freeze / unfreeze gain `int currentUserId`, scope and version checks; shared helper |
| `backend/MicroLIMS.API/Controllers/TestWorkflowStepMasterDataController.cs`, `TestDefinitionMasterDataController.cs` | Pass `CurrentUserId`, `HttpContext.RequestAborted` |
| DI registration of `TestWorkflowStepMasterDataService` | Unchanged lifetime; new dependency resolves |
| `frontend/src/services/masterDataOptions.ts` (lines 305–328) | Version argument and `ifMatch(version)` on five calls |
| Callers in `frontend/src/modules/laboratoryConfiguration/masterDataSimple/TestMasterPage.tsx` and the workflow-step editor | Pass the loaded version; keep the returned one |
| `backend/MicroLIMS.Tests/IntegrationTests/TestConfigurationWriteScopePostgresTests.cs` | New |

---

### Task 1: Workflow step and workflow type writes

**Interfaces:**
- `UpdateWorkflowTypeAsync(int currentUserId, int id, UpdateWorkflowTypeRequest request, CancellationToken ct = default)` — scope on the test; `RecordVersion.EnsureCurrent(_db, test)`.
- `CreateTestWorkflowStepAsync(int currentUserId, int id, CreateTestWorkflowStepRequest request, CancellationToken ct = default)` — scope on the test.
- `UpdateTestWorkflowStepAsync(int currentUserId, int stepId, UpdateTestWorkflowStepRequest request, CancellationToken ct = default)` — scope on the step's test; existing version check kept.
- `MoveTestWorkflowStepAsync(int currentUserId, int stepId, MoveTestWorkflowStepRequest request, CancellationToken ct = default)` — scope; version of the moved step.
- `DeleteTestWorkflowStepAsync(int currentUserId, int stepId, CancellationToken ct = default)` — scope; version of the step.
- Scope and version are checked before any change is made to tracked entities.

- [ ] **Step 1: Failing tests** in `TestConfigurationWriteScopePostgresTests` (fixture: laboratories A and B; Section Head A member of A; Section Head B member of B; a holder of `Sections.ConfigureAll`; test T in A with three steps). For each of the five methods: `<Action>_by_other_laboratory_refused` (exception type and message equal to the test update's; steps and test unchanged in a fresh context; no new `AuditLogs` row for those records), `<Action>_by_own_laboratory_persists_and_audits` (fields, audit `UserId` = Section Head A, `RecordId`, old and new values), `<Action>_by_configure_all_succeeds`. Plus `Move_with_stale_version_of_moved_step_refused`, `Delete_with_stale_version_refused`, `Workflow_type_with_stale_version_refused`, `Step_write_on_test_without_section_follows_test_update_rule` (assert the same outcome the test update gives for a null-section test).
- [ ] **Step 2:** Run with PostgreSQL (`service postgresql start`, `MICROLIMS_TEST_POSTGRES` per `CLAUDE.md`) → FAIL.
- [ ] **Step 3:** Implement; update the controller to pass `CurrentUserId` and `HttpContext.RequestAborted`.
- [ ] **Step 4:** Run → PASS; run the existing test-definition and workflow-step test classes → PASS.
- [ ] **Step 5:** Mutation self-check: comment out the scope call in one method → its refusal test fails; remove one `EnsureCurrent` → its stale test fails. Restore.
- [ ] **Step 6:** Commit `fix(masterdata): laboratory scope and version checks on workflow step writes`.

### Task 2: Freeze and unfreeze

**Interfaces:** `FreezeTestDefinitionAsync(int currentUserId, int id, CancellationToken ct = default)`, `UnfreezeTestDefinitionAsync(int currentUserId, int id, CancellationToken ct = default)` — shared helper scope check; `RecordVersion.EnsureCurrent(_db, entity)`; everything else unchanged. Update every caller (`grep -rn "FreezeTestDefinitionAsync\|UnfreezeTestDefinitionAsync" backend`), including tests that call them directly.

- [ ] **Step 1: Failing tests:** `Freeze_by_other_laboratory_refused`, `Unfreeze_by_other_laboratory_refused`, `Freeze_by_own_laboratory_persists_and_audits` (`IsActive = false`, audit row with the user id), `Unfreeze_by_own_laboratory_persists_and_audits`, `Freeze_with_stale_version_refused`, `Unfreeze_with_stale_version_refused`.
- [ ] **Step 2–4:** FAIL → implement → PASS; existing suites pass.
- [ ] **Step 5:** Commit `fix(masterdata): laboratory scope and version checks on test freeze and unfreeze`.

### Task 3: Frontend sends versions

**Files:** `frontend/src/services/masterDataOptions.ts` — `freezeTestDefinition(id, version)`, `unfreezeTestDefinition(id, version)`, `updateWorkflowType(testDefinitionId, workflowType, version)`, `moveTestWorkflowStep(stepId, direction, version)`, `deleteTestWorkflowStep(stepId, version)`, each passing `ifMatch(version)`; their callers pass the version of the loaded row and replace it with the version returned by the server (or reload the list after delete). Tests beside the changed components.

- [ ] **Step 1: Failing tests** (`userEvent`, real components, only `services/` mocked): `freeze sends If-Match with the loaded version`, `move sends the step version`, `uses returned version for the next action` (two moves in a row send two different versions), `delete sends the step version`, `server refusal is shown as an error` (no silent failure).
- [ ] **Step 2–4:** FAIL → implement → `npm run lint`, `npx tsc -b`, `npm run test`, `npm run debt:check`, `npm run build` PASS.
- [ ] **Step 5:** Commit `fix(ui): send record version on test freeze and workflow step actions`.

### Task 4: Verification and record

- [ ] **Step 1:** Full backend suite with PostgreSQL per `CLAUDE.md` section 2; report exact passed / failed / skipped; skipped `[PostgresFact]` > 0 is a failure.
- [ ] **Step 2:** Frontend: lint, types, tests, debt check, build; report results.
- [ ] **Step 3:** In `docs/superpowers/plans/2026-11-11-quality-events.md`, mark D2 Task 8 as done by G5 (D2 Task 9 depends on it).
- [ ] **Step 4:** Pull request text states it is a security fix, lists the seven endpoints, and says the behaviour for users acting on their own laboratory is unchanged.

## Cross-plan corrections (whole-programme review, 2026-11-11)

These corrections come from reviewing all plans together against the code. Where they differ from the text above, they win. Shared names are defined once in `docs/superpowers/plans/2026-11-11-build-order.md` (section "Programme conventions").

1. `UpdateWorkflowTypeAsync` lives in `TestWorkflowStepMasterDataService`.
2. The shared helper is a private method in each service calling `IUserSectionScopeService.GetConfigurableSectionIdsAsync`, copied from `UpdateTestDefinitionAsync`; if both services need it, put it in `backend/MicroLIMS.Application/Services/MasterData/TestConfigurationScope.cs` (internal static).
