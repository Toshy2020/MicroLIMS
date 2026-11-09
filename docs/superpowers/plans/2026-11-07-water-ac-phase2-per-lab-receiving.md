# Phase 2 — Each lab receives and prepares its own water / after-cleaning samples

Spec §3. Branch `feat/water-ac-chemistry` (worktree `E:/MicroLIMS/rls-tmp/wt-water-ac`).
After-cleaning UV/HPLC/ICP choice and previous-product pick are Phase 4, not here.

## Today
- Water / AC samples carry no lab until preparation. Microbiology is hard-coded as the
  preparing lab: `SampleWorkflowQueues.NeedsPreparationIn` / `VisibleInSections`
  (`includesPreparingLab`) and `TestingWorkspaceService.IncludesPreparingLabAsync`.
- `UserSectionScopeService.EnsureSampleAccessAsync` finds no section on such a sample.
- Prepare creates orders for **every** assigned code and calls `EnsureSectionsAccessAsync`
  over all their sections, so a point with chemistry tests blocks micro preparation.
- Receiving: `NewSampleDialog` with `labMode {kind:"fixed", sectionId}` in
  `ReceivingTestingWorkspacePage`; `getAllowedCategories` gives water/ac to MICRO only.
- Physchem area split by category: `PhyschemAreas.OfCategory` / `CategoriesOf`
  (RM & PM area = RawMaterial, PackagingMaterial; FP area = everything else).

## Backend (job A)

### A1. `Sample.LabSectionId`
- `Domain/Entities/Sample.cs`: `int? LabSectionId` + `DocumentSection? LabSection`
  ("Water / After Cleaning only: the lab that received the sample and prepares it").
- EF config: FK to DocumentSections, `OnDelete Restrict`, index.
- Migration `SampleLabSection` (`dotnet ef migrations add SampleLabSection --configuration Release`):
  add the column, then backfill
  `UPDATE "Samples" SET "LabSectionId" = (SELECT "Id" FROM "DocumentSections" WHERE "Code" = 'MICRO')
   WHERE "Category" IN (3, 5);` (Water = 3, AfterCleaning = 5). Down drops the column.
  Do not apply it to any database.
- Expose `LabSectionId` on the sample DTO that already carries `WaterDepartmentId` / `MachineId`
  (`Application/DTOs/SampleDto.cs` and the workspace list DTO the PreparationDialog reads; find with
  `git grep -n "WaterDepartmentId" -- backend/MicroLIMS.Application/DTOs backend/MicroLIMS.Application/Services/TestingWorkspaceService.cs`).

### A2. Physchem areas
`Application/Helpers/PhyschemAreas.cs`: Water belongs to the RM & PM area.
`OfCategory`: RawMaterial, PackagingMaterial, **Water** → RmPm.
`CategoriesOf(RmPm)` adds Water; `CategoriesOf(Fp)` therefore no longer includes Water.
AfterCleaning stays in FP (already).

### A3. Receive
- `ReceiveWaterRequest` / `ReceiveAfterCleaningRequest` (+ engine request records) get `int? LabSectionId`.
- Engines: `sample.LabSectionId = await _scope.ResolveSectionForCreateAsync(userId, request.LabSectionId)`
  (Write access). When `_scope` is null (old tests), use `request.LabSectionId ?? MICRO section id`.
- If the resolved lab is Physicochemical (`PhyschemAreas.SectionCode`), the user's write areas
  (`GetPhyschemAreasAsync`) must include `PhyschemAreas.OfCategory(category)`; otherwise
  `UnauthorizedAccessException("Water samples are received by the RM & PM area.")` /
  `("After-cleaning samples are received by the FP area.")`.

### A4. Prepare (both engines)
- The sample's lab = `sample.LabSectionId ?? MICRO id`.
- Allowed codes = tests with `SectionId == lab` and, for the Physicochemical lab,
  `PhyschemAreas.Includes(test.PhyschemArea, PhyschemAreas.OfCategory(sample.Category))`.
- Water: ignore assigned codes outside the allowed set (no order, no location). If nothing is
  left: `InvalidOperationException("None of the selected sampling points has {lab name} tests assigned.")`.
- After cleaning: a selected `MachinePartConfiguration` whose code is outside the set →
  `InvalidOperationException("Part test {code} belongs to another laboratory.")`.
- Keep `EnsureSectionsAccessAsync` (now always the one lab).

### A5. Visibility and access
- `SampleWorkflowQueues`: the "location sample with no tests yet" clause in `NeedsPreparationIn`
  (both branches) and `VisibleInSections` becomes
  `TestsCreatedAtPreparationCategories.Contains(s.Category) && no non-superseded tests &&
   (s.LabSectionId != null ? sectionIds.Contains(s.LabSectionId.Value) : includesPreparingLab)`.
  So the non-preparing-lab branch now also lists samples received by that lab. Keep EF-translatable.
  Update `IsAwaitingPreparationWithoutTests` doc comment if behaviour wording changes.
- `UserSectionScopeService.EnsureSampleAccessAsync`: include `s.LabSectionId` (when set) in the
  sample's section ids. Do not touch `SampleSectionRollup`.

### A6. Tests (`backend/MicroLIMS.Tests`, follow existing WaterBatchPrepareTests / AfterCleaningReceivingTests style)
1. Micro prepare at a point assigned TAMC + an RM chemistry code creates only the TAMC order.
2. RM-area user receives water into FP lab → `LabSectionId` = FP; prepare creates only the RM-area FP code.
3. FP-area-only user receiving water into FP lab → refused; RM-area user receiving AC → refused.
4. AC prepare with a config of the other lab → refused.
5. Queue: an FP-lab water sample with no tests is listed for the FP section (RM area) and not for MICRO;
   a legacy sample with `LabSectionId` null is listed for MICRO only.
6. `PhyschemAreas` mapping: Water → RmPm.
Full suite with Postgres must pass (see Verification).

## Frontend (job B)
- `ReceiveService.ts` + `receivingTypes.ts`: `labSectionId?: number` on water / AC receive requests.
- `NewSampleDialog.tsx`: for water and ac, send `labSectionId` from `labMode` when it is `fixed`.
- `ReceivingTestingWorkspacePage.tsx` `getAllowedCategories`: MICRO unchanged;
  rmpm → `["rm","pm","water"]`; fp → `["product","ac"]`.
- Sample type used by `PreparationDialog`: add `labSectionId?: number | null`; pass it to
  `WaterPreparationForm` / `AfterCleaningPreparationForm`.
- `WaterPreparationForm`: show only points that have at least one assigned code whose test
  (`useTestDefinitions`, field `sectionId`) is in `labSectionId` (null → treat as the MICRO lab: match
  tests whose section code is MICRO, or keep current behaviour if section code isn't available).
  Show only those codes as the tests to be created.
- `AfterCleaningPreparationForm`: offer only part configurations whose test is in that lab.
- Component tests for both forms' filtering, and `getAllowedCategories` if it's exported/testable.

## Verification (stop point)
- Backend: export `MICROLIMS_TEST_POSTGRES` (see memory) and run `dotnet test` on the
  **worktree** project with `--artifacts-path` inside `E:/MicroLIMS/rls-tmp`. 0 failures except the known
  `NegativeEntityIdCorrectionScript` artifacts-path test.
- Frontend: `tsc --noEmit`, `vitest run`, `npm run build`.
- Migration applied to LIMSV2 only after a backup (`E:/MicroLIMS/backups`), then a browser check.
- Local commits, no push.
