# Phase 1 — Shared water / after-cleaning master data, lab-owned test rows

Spec: `docs/superpowers/specs/2026-11-07-water-after-cleaning-chemistry-design.md` §1.
Branch: `feat/water-ac-chemistry` (worktree `E:/MicroLIMS/rls-tmp/wt-water-ac`).
No workflow change, no migration.

## Ownership rule (one helper, used everywhere below)
A test code is editable by user U when:
- U's scope is unrestricted (System Administrator: `GetAccessibleSectionIdsAsync` returns null), or
- the code's `TestDefinition.SectionId` is in U's accessible sections **and**, if the
  test has a `PhyschemArea` (FP / RawPackaging / Both), U's `GetPhyschemAreasAsync`
  covers it (`Both` on the test = either area is enough).

## Task 1 — `TestCodeOwnership` helper (backend)
File: new `backend/MicroLIMS.Application/Services/TestCodeOwnership.cs`.
- `Task<HashSet<string>?> GetEditableCodesAsync(db, scope, userId)`: null = unrestricted;
  otherwise the codes passing the rule above.
- `Task EnsureCanEditAsync(db, scope, userId, IEnumerable<string> codes, string what)`:
  throws `InvalidOperationException` for an unknown code and
  `UnauthorizedAccessException("{what} for test {code} belongs to {lab} - only its Section Head can change it.")`.
- Leave `SpecificationOwnership` unchanged. Its behaviour (section only, no area) is covered by tests.
- Tests: `backend/MicroLIMS.Tests/UnitTests/TestCodeOwnershipTests.cs`, covering admin, own section,
  other section, physchem area match / mismatch / `Both`, and unknown code.
  Copy the setup from `SpecificationOwnershipTests.cs`.

## Task 2 — enforce on water master data
File: `backend/MicroLIMS.Application/Services/MasterData/WaterMasterDataService.cs`
(inject `IUserSectionScopeService`; the controller passes the current user id, the same way
`SpecificationMasterDataController` does).
- Create point: every code in `AssignedTestCodes` is editable.
- Update point: only the **changed** codes (added ∪ removed versus the stored list) must be
  editable. Unchanged codes from other labs pass through. Code, location, department,
  frequency and active fields stay open to anyone with the endpoint permission.
- Delete point: blocked if it carries any code the user cannot edit
  ("... still has {lab} tests assigned").
- SamplingConfiguration create / update (old and new code) / delete: the code is editable.
- Departments: unchanged.
- GET points: unchanged. The UI learns what it can edit from the Task 4 endpoint.

## Task 3 — enforce on after-cleaning master data
File: `backend/MicroLIMS.Application/Services/MasterData/AfterCleaningMasterDataService.cs`.
- MachinePartConfiguration create / update (old and new `TestCode`) / delete: the code is editable.
- Machines and parts: unchanged (shared).
- Note: Swab/Rinse rows carry `TestCode` too. Check it, not `TestType`.

## Task 4 — `GET api/masterdata/test-definitions/editable-codes`
Returns `string[]` from `GetEditableCodesAsync`, or every active code when unrestricted.
Put it on `TestDefinitionMasterDataController`.
Frontend: add `getEditableTestCodes()` to `frontend/src/services/masterDataOptions.ts`
plus a tiny `useEditableTestCodes()` hook (react-query, same pattern as `useTestDefinitions`).

## Task 5 — frontend: water page
Files: `frontend/src/modules/laboratoryConfiguration/water/components/WaterLocationPanel.tsx`,
`WaterLocationList.tsx`, `frontend/src/components/TestCodePickerMulti.tsx`.
- `TestCodePickerMulti` gets an optional `editableCodes?: Set<string>`. Options outside the
  set are not offered, and selected chips outside it render as locked (no delete, tooltip
  shows the owning lab's section name from the test definition).
- Limit rows for codes the user can't edit render read-only.
- Delete point is disabled with a tooltip when the point has locked codes.

## Task 6 — frontend: after-cleaning page
Files: `afterCleaning/components/AcPanels.tsx`, `AcPartCard.tsx`, `PathogenPicker.tsx`,
`frontend/src/components/TestCodePicker.tsx`.
- Same rule: pickers offer only editable codes, and other labs' config rows are read-only
  (no edit or delete buttons, with a lab-name caption).

## Task 7 — menu
File: `frontend/src/routes/menuConfig.ts`.
- Remove Water and After Cleaning from the Microbiology Configuration children.
- Add them as top-level items in group `GENERAL LABORATORY CONFIGURATION` (same style as
  `itemsItem`), permission `MASTER_DATA_MANAGE`, same paths, so links and `routes.ts` keep working.
- Update the descriptions: "Water systems, sampling points and each lab's tests and limits." /
  "Machines, parts and each lab's after-cleaning tests and limits."
- Update any menu snapshot or menu test that lists these items.

## Verification (stop point)
- `dotnet test backend/MicroLIMS.Tests --artifacts-path E:/MicroLIMS/rls-tmp/art-water-ac`,
  Postgres suite all green.
- `npm test` + `npm run build` in `frontend` (npm cache on E:).
- Browser check (Brave): log in as a micro Section Head and an RM user (accounts in
  `frontend/.env.e2e.local`). Each sees both pages under General Laboratory Configuration.
  Each can add only its own tests and sees the other lab's rows locked.
- One local commit per task group (backend 1-4, frontend 5-7). Don't push.
