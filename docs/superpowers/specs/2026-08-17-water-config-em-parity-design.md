# Water Configuration — Environmental Monitoring Parity

**Date:** 2026-08-17
**Status:** Design — awaiting approval
**Area:** Laboratory Configuration → Water

## Problem

The Water configuration page is a flat list of sampling points, each
holding a bag of assigned test codes but **no limits**. The
Environmental Monitoring (EM) page, by contrast, is a
`Department → Room → per-test Alert/Action/Spec` hierarchy that users
can expand, edit, and delete.

The user wants Water to work like EM:

> department, and under each department sample locations; choose tests
> for each sample location, and the limits for the TAMC test (Alert,
> Action, Specification).

### Key finding — the limits engine already exists and is already consumed

`WaterWorkflowEngine.CalculateAndCompareAsync`
(`backend/MicroLIMS.Application/Workflows/WaterWorkflowEngine.cs:76-82`)
already looks up a `SamplingConfiguration` row by
`(TestCode, WaterSamplingPointId)` and feeds its `AlertLimit /
ActionLimit / SpecLimit` into `Compare(...)`. The `SamplingConfiguration`
entity already exists with exactly those fields.

**But nothing ever creates those rows** — there is no CRUD endpoint and
no UI. So `config` is always `null` and every water count result
silently returns `"WithinLimits"` regardless of the reading. This is
the identical latent gap the EM page already fixed for rooms
(see the comment at `EMConfigPage.tsx:18-22`).

Therefore the bulk of the "compare against limits" machinery is done.
This work is about adding the **configuration layer** (departments,
nesting, and the limits form) that feeds it.

## Decisions (confirmed with user)

1. **Separate Water departments** — a new `WaterDepartment` entity,
   independent of EM's `Department`.
2. **Water Department fields:** `Name` + `TestingFrequency` (mirrors the
   EM department table, which shows Class + Testing Frequency; Water
   omits Class).
3. **Limits only for count tests.** A sample location may have many
   assigned tests, but Alert/Action/Specification fields appear only for
   tests whose `TestDefinition.WorkflowType == CountTest` (TAMC-Water,
   TYMC). Presence/absence pathogens (E.coli, Salmonella, P.aeruginosa,
   S.aureus, BCC) get no limit fields. Selection is by workflow type, not
   a hardcoded "TAMC-Water" string.

## Target hierarchy

```
Water Department  (Name, Testing Frequency)
  └─ Sample Location  (= existing WaterSamplingPoint, now under a department)
       ├─ Assigned Tests   (drives which TestOrders are created on receipt)
       └─ Limits           (Alert / Action / Specification — count tests only)
```

This is the same shape as EM's `Department → Room → per-test limits`.
A **Sample Location maps to an EM Room.**

## Data model (backend)

### New entity — `WaterDepartment`
```
Id : int
Name : string
TestingFrequency : string
```
Mirrors `Department` minus `Class`. Register in `MicroLimsDbContext`
with a `DbSet<WaterDepartment>`.

### `WaterSamplingPoint` — add department FK
```
+ WaterDepartmentId : int?         // nullable so existing rows migrate cleanly
+ WaterDepartment  : WaterDepartment?
```
Keeping the existing `WaterSamplingPoint.Id` is **mandatory** — both
`Sample.WaterSamplingPointId` and `SamplingConfiguration.WaterSamplingPointId`
reference it. We add a parent, we do not replace the entity.

### `SamplingConfiguration` — reused unchanged
Already: `Id, WaterSamplingPointId, TestCode, AlertLimit, ActionLimit,
SpecLimit`. One row per count test per location. No schema change.

### Assignment vs. limits (unchanged split)
- `WaterSamplingPoint.AssignedTestCodes` continues to drive which
  `TestOrder`s are created on receipt (`WaterWorkflowEngine.cs:59`).
- `SamplingConfiguration` rows carry the limits the engine reads on
  calculate. A limit row is only meaningful for a test that is also
  assigned; the UI only offers limits for assigned count tests.

### Engine — **zero changes**
Order creation (from `AssignedTestCodes`) and limit lookup (from
`SamplingConfiguration`) already work. After this change the lookup
finds real rows instead of `null`.

## API — `MasterDataController` (mirroring existing EM endpoints)

New request records alongside the existing ones (lines 13-45):
```
CreateWaterDepartmentRequest(string Name, string TestingFrequency)
UpdateWaterDepartmentRequest(string Name, string TestingFrequency)
CreateWaterSamplingConfigRequest(int WaterSamplingPointId, string TestCode,
                                 string AlertLimit, string ActionLimit, string SpecLimit)
UpdateWaterSamplingConfigRequest(string TestCode, string AlertLimit,
                                 string ActionLimit, string SpecLimit)
```

### `water-departments` (mirror of `departments`, lines 112-163)
- `GET  water-departments` → departments each including their sample
  locations (shaped projection to avoid the nav cycle, exactly like
  `GetDepartments`).
- `POST water-departments` — create.
- `PUT  water-departments/{id}` — update Name + TestingFrequency.
- `DELETE water-departments/{id}` — **blocked** if it still has sample
  locations, with a clear message (mirror of `DeleteDepartment`).

### `water-sampling-points` (extend existing, lines 63-109)
- `Create` / `Update` requests gain `WaterDepartmentId`.
- `GET` may stay flat (the grouped `water-departments` GET is what the
  page renders); keep returning locations with their `WaterDepartmentId`.
- Delete guard unchanged — already blocks on referencing samples and
  sampling configurations (lines 100-104), matching the EM room guard.

### `water-sampling-configurations` (mirror of `room-test-configurations`, lines 462-505)
- `GET  water-sampling-configurations?pointId=` → rows for one location.
- `POST water-sampling-configurations` — create a limit row.
- `PUT  water-sampling-configurations/{id}` — update limits.
- `DELETE water-sampling-configurations/{id}` — delete a limit row.

All mutating endpoints carry the same
`[Authorize(Roles = SectionHead + SystemAdministrator)]` attribute the
sibling water/EM endpoints use.

## Frontend

### `water/services/WaterConfigService.ts` — extend
Add `getWaterDepartments / create / update / delete`, add
`WaterDepartmentId` to sampling-point create/update, and add
`get/create/update/delete SamplingConfiguration` methods (mirror of
`EMConfigService`'s room-test-configuration methods).

### `water/WaterConfigPage.tsx` — rebuild to mirror `EMConfigPage.tsx`
- **New Water Department** form: Name, Testing Frequency.
- **New Sample Location** form: Point Code, Location, **Department
  dropdown**, Assigned Tests (existing `TestCodePickerMulti`).
- **Departments table** → expand to **Sample Locations** table → expand
  to a **`SamplingPointTestConfigSection`** (mirror of
  `RoomTestConfigSection`).

### `SamplingPointTestConfigSection` (new, mirror of `RoomTestConfigSection`)
- Lists the location's `SamplingConfiguration` rows (Test, Alert,
  Action, Spec) with edit/delete.
- "Add / Edit Configuration" row: a **Test dropdown limited to the
  location's assigned tests whose `workflowType === "CountTest"`**, plus
  Alert / Action / Specification fields.
- If a location has no assigned count test, show a hint ("Assign a count
  test such as TAMC-Water to set limits") instead of the form.
- Uses `useTestDefinitions()` to resolve each assigned code's
  `workflowType`.

## Migration

Single EF migration:
1. Create `WaterDepartments` table.
2. Add nullable `WaterDepartmentId` column + FK to `WaterSamplingPoints`.
3. Data step: create one default department
   (`Name = "Water"`, `TestingFrequency = ""`) and assign all existing
   sampling points (SWT, SP103, SP104, SP105, SP106, SP205) to it, so no
   location is orphaned in the new UI.

`WaterDepartmentId` stays nullable at the DB level (locations created
before assignment remain valid); the UI requires a department on create.

## Testing

Extend `backend/MicroLIMS.Tests/WorkflowTests/WaterAndEMEngineTests.cs`:
- **Limit evaluation now works:** with a `SamplingConfiguration` whose
  Alert/Action/Spec are set for the count test, readings above each
  threshold produce `AlertLimitExceeded / ActionLimitExceeded /
  SpecLimitExceeded`; readings below produce `WithinLimits`. (Proves the
  previously-dead path is now live.)
- **No config → WithinLimits** still holds (backwards compatible).

Controller/CRUD coverage for `water-departments` and
`water-sampling-configurations` following the existing MasterData test
patterns; department delete blocked while it has locations.

## Out of scope / YAGNI
- No change to the water receipt or calculation flow beyond config being
  present.
- No `Class` field on water departments.
- No per-test limits for pathogens.
- No merge of `AssignedTestCodes` into `SamplingConfiguration` — the two
  keep their existing, separate roles.

## Files touched (anticipated)
- `backend/MicroLIMS.Domain/Entities/WaterDepartment.cs` (new)
- `backend/MicroLIMS.Domain/Entities/WaterSamplingPoint.cs` (+ FK)
- `backend/MicroLIMS.Persistence/DbContext/MicroLimsDbContext.cs` (DbSet + config)
- `backend/MicroLIMS.Persistence/Migrations/*` (new migration)
- `backend/MicroLIMS.API/Controllers/MasterDataController.cs` (endpoints)
- `frontend/src/modules/laboratoryConfiguration/water/WaterConfigPage.tsx`
- `frontend/src/modules/laboratoryConfiguration/water/services/WaterConfigService.ts`
- `backend/MicroLIMS.Tests/WorkflowTests/WaterAndEMEngineTests.cs`
