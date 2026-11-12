# G5 — Laboratory Scope and Version Checks on Test Configuration Writes: Design Specification

**Roadmap package:** G5 (`improvement-roadmap.md`, Continuous — Engineering health)
**Status:** Approved by the user 2026-11-11 (pulled out of D2 Task 8 to be done early)
**Date:** 2026-11-11
**Kind:** Security fix. Size S.

## 1. Purpose

Make the server refuse changes to a test's workflow and to its frozen state when the user may not configure that test's laboratory, and refuse a save that would silently overwrite someone else's change.

## 2. Findings (from reading the code, not run)

| Action | Endpoint | Laboratory check | Overwrite check |
|---|---|---|---|
| Change workflow type | `PUT api/masterdata/test-definitions/{id}/workflow-type` | **None** | **None** |
| Add workflow step | `POST api/masterdata/test-definitions/{id}/steps` | **None** | Not needed (new row) |
| Change workflow step | `PUT api/masterdata/test-definitions/steps/{stepId}` | **None** | Present |
| Move workflow step | `PUT api/masterdata/test-definitions/steps/{stepId}/move` | **None** | **None** |
| Delete workflow step | `DELETE api/masterdata/test-definitions/steps/{stepId}` | **None** | **None** |
| Freeze test | `PUT api/masterdata/test-definitions/{id}/freeze` | **None** | **None** |
| Unfreeze test | `PUT api/masterdata/test-definitions/{id}/unfreeze` | **None** | **None** |

- All seven require the `MasterData.Manage` permission, so only users with that permission (Section Heads, and System Administrators by grant) can reach them. Within that group, a user can change the workflow of, or freeze, a test that belongs to a laboratory they do not configure.
- `TestWorkflowStepMasterDataService` receives only the database context: it has no access to the user or to the laboratory scope service. The freeze and unfreeze methods receive only the test id.
- The test's own update (`UpdateTestDefinitionAsync`) already checks both laboratory scope and version; it is the model for this fix.
- Every change is audited automatically by the database context; this fix does not change auditing.
- Reading a test's steps (`GET …/steps`) also has no laboratory check. It is left as it is: reading the test catalogue across laboratories is not shown to be wrong, and no write is possible through it.

`CLAUDE.md` section 5.2 requires resource- and laboratory-scoped authorization on the server for every change. Section 5.3 requires overwrite protection on critical changes.

## 3. Design

- Each of the seven actions receives the acting user's id and finds the laboratory of the test (`TestDefinition.SectionId`; for a step, through its test). The user must be allowed to configure that laboratory, through the existing `GetConfigurableSectionIdsAsync` used by the test update. Otherwise the request is refused before anything is saved, with the same message and status the test update uses for this case.
- A test whose laboratory is empty follows the test update's existing rule for that case; no new rule is invented.
- Move, delete, workflow type, freeze and unfreeze check the record version sent by the screen (the existing `If-Match` mechanism), as the test update does. A stale version is refused with the existing "reload" response.
- For move, the version checked is the step's version. For workflow type, freeze and unfreeze, it is the test's version.
- The screens send the version they loaded for these five actions. The step change already does.
- No change to who holds `MasterData.Manage`, to the screens' layout, or to any laboratory rule.

## 4. Testing

`[PostgresFact]`, separate contexts for arrange / act / assert, `AsNoTracking` in assert. Per action:
- A Section Head of laboratory B acting on a test of laboratory A is refused; the test and its steps are unchanged and no audit row is written.
- A Section Head of laboratory A succeeds; the change is persisted and audited with that user's id.
- A stale version is refused with nothing changed (the five actions with a version check).
- A holder of `Sections.ConfigureAll` succeeds on any laboratory.

Frontend (`userEvent`, `services/` mocked only): the five actions send `If-Match`; a refusal message from the server is shown.

## 5. Done when

All seven actions are refused across laboratories and the five are refused on stale versions, proven by the tests above against PostgreSQL, and the full backend and frontend suites pass.
