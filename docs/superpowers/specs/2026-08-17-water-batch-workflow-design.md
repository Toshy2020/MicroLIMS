# Water Batch Workflow — Parity with EM / After Cleaning

**Date:** 2026-08-17
**Status:** Design — awaiting approval
**Area:** Water receiving → preparation → result entry (Testing Workspace + Receiving)

## Problem

Water is the odd one out among the three location-based sample
categories. EM and After Cleaning use a **batch** model:

- **Receiving** captures only the top-level group (EM: Department,
  AC: Machine) and creates a "Needs Preparation" shell with **no
  TestOrders**.
- **Preparation** is a checklist where the analyst selects which
  rooms / machine-parts are included. Selecting them generates one
  `TestOrder` per distinct `TestCode` across the batch and one
  `SampleLocation` row per selected location×test.
- **Result entry** is per-`SampleLocation`, under a shared TestOrder.

Water instead forces the analyst to pick a **single sampling point** at
receiving (`WaterReceiveRequest.WaterSamplingPointId`), immediately
creates one TestOrder per assigned test, and enters results against that
one point. There is no preparation-selection step and no batching.

The user wants Water to behave like EM/After Cleaning end-to-end
(confirmed: full batch parity).

## Confirmed decisions (user, 2026-08-17)

1. **Full batch parity** — rebuild receive → prepare → result entry, not
   just the receiving screen.
2. **Receiving** captures a **Water Department**; the specific sampling
   points are chosen in **Preparation**.
3. **Count test (TAMC-Water):** keep **multi-reading averaging per
   point** — each selected point under the count TestOrder gets its own
   set of plate readings, averaged, compared to that point's
   Alert/Action/Specification.
4. **Pathogen tests:** per-location in the batch, keeping a **single
   pathogen (Observation) workflow** per TestOrder. Add a **new grid
   window** to record the final detection result **per location**
   (Detected / Absent, mark and save).

## Rulings made in this design (open to change on review)

- **Sample Quantity stays on Water receiving.** EM/AC omit quantity, but
  a water sample has a genuine volume (e.g. 100 ml). Dropping real data
  to match a template is wrong; Water keeps the Quantity column while
  moving from Sampling Point → Department.
- **Existing water samples are not migrated.** Samples already received
  under the old per-point model keep working as-is; only newly received
  water samples use the batch model. Rendering must tolerate both.
- **`SampleLocation` is extended, not forked.** Water joins the existing
  batch table rather than getting a parallel one.

## Architecture

### The three phases (mirroring EM)

```
Receive:  pick Water Department  ─────────────►  Sample shell (WaterDepartmentId,
                                                 no TestOrders, NeedsPreparation)
Prepare:  check sampling points  ─────────────►  1 TestOrder per distinct TestCode
                                                 + 1 SampleLocation per point×test
Results:  per SampleLocation
            • count  → multi-reading avg → compare to snapshot limits
            • pathogen → single workflow per TestOrder, then Detected/Absent grid per location
```

## Data model changes

### `Sample` (`Domain/Entities/Sample.cs`)
Add the water batch link, alongside the existing `DepartmentId` (EM) and
`MachineId` (AC):
```
+ public int? WaterDepartmentId { get; set; }   // Water batch
+ public WaterDepartment? WaterDepartment { get; set; }
```
`WaterSamplingPointId` stays (legacy per-point samples still reference
it); new water samples leave it null and use `WaterDepartmentId` +
`Locations`.

### `LocationType` (`Domain/Enums/LocationType.cs`)
```
  Room,
  MachinePart,
+ WaterSamplingPoint
```

### `SampleLocation` (`Domain/Entities/SampleLocation.cs`)
Add the water references and per-location raw readings:
```
+ public int? WaterSamplingPointId { get; set; }
+ public WaterSamplingPoint? WaterSamplingPoint { get; set; }
+ public int? SamplingConfigurationId { get; set; }   // count-test limits source; null for pathogens
+ public SamplingConfiguration? SamplingConfiguration { get; set; }
+ public string? RawReadings { get; set; }            // comma-joined plate readings (water count only)
```
The existing `CalculatedResult`, `AlertLimit/ActionLimit/SpecLimit`,
`Status`, `ReportedResult`, `EnteredAt/EnteredByUserId` fields already
cover both count (average + limits + status) and pathogen
(`ReportedResult` = "Detected"/"Absent", `Status`) results. `CFUResult`
and `DilutionFactor` remain EM/AC-oriented and are simply unused for
water count locations (which use `RawReadings` → average →
`CalculatedResult`).

### Migration
One EF migration adding `Sample.WaterDepartmentId` (FK, nullable),
`SampleLocation.WaterSamplingPointId` (FK), `SampleLocation.SamplingConfigurationId`
(FK), and `SampleLocation.RawReadings` (text, nullable). No data
backfill — the columns are nullable and only new water samples populate
them.

## Backend workflow (`WaterWorkflowEngine.cs`)

### `ReceiveAsync` — becomes a shell
`WaterReceiveRequest` changes from `WaterSamplingPointId` to
`WaterDepartmentId`:
```
record WaterReceiveRequest(int WaterDepartmentId, int CauseOfTestingId,
    string SampleQuantity, string SampledBy, string ControlNumber, int ReceivedByUserId);
```
Validates the department exists, creates a `Sample` with
`WaterDepartmentId`, `Category = Water`, `SampleQuantity`, no TestOrders,
`PreparationStatus = NeedsPreparation`. Mirrors `EMWorkflowEngine.ReceiveAsync`.

### `PrepareAsync(sampleId, waterSamplingPointIds[], userId)` — new
Mirrors `EMWorkflowEngine.PrepareAsync`, keyed on sampling points
instead of RoomTestConfigurations (because a water point's assignment is
its `AssignedTestCodes` list, not per-test config rows):

1. Load the sample (must be `NeedsPreparation`); require ≥1 point.
2. Load the selected `WaterSamplingPoint`s; verify each belongs to the
   sample's `WaterDepartmentId`.
3. For each point, for each of its `AssignedTestCodes`:
   - Ensure one `TestOrder` per distinct `TestCode` across the whole
     batch (dictionary keyed by code, same as EM).
   - Add a `SampleLocation { TestOrder, LocationType.WaterSamplingPoint,
     WaterSamplingPointId }`.
   - If the test is a **count** test (`TestDefinition.WorkflowType ==
     CountTest`), link `SamplingConfigurationId` for that point×code when
     one exists (limits source).
4. `PreparationStatus = Ready`.

### Result entry — split by test type

**Count (`CalculateLocationAsync(sampleLocationId, readings)`) — new,
replaces the sample-wide `CalculateAndCompareAsync`:**
- Average the readings, snapshot the location's
  `SamplingConfiguration` Alert/Action/Spec onto the `SampleLocation`,
  compute `Status` via the existing `Compare(...)`, store `RawReadings`,
  `CalculatedResult`, and `Status`.
- The count `TestOrder` becomes `Ready` only once **every** count
  `SampleLocation` under it has a result (validation mirrors
  `EMWorkflowEngine.ValidateAsync`).

**Pathogen (`RecordDetectionAsync(testOrderId, List<(sampleLocationId,
detected)>, userId)`) — new:**
- The pathogen TestOrder still runs the single Observation workflow
  chain as today (unchanged incubation/observation steps).
- At the terminal step, the new grid submits one Detected/Absent value
  per location. Each `SampleLocation` gets `ReportedResult` =
  "Detected"/"Absent" and `Status`. The TestOrder completes once every
  pathogen location has a value.

### Daily aggregate / reporting
`GetDailyAggregateAsync` and any water report projection currently read
`Result` rows keyed by TestOrder. They must additionally (or instead)
read `SampleLocation` rows for batch water samples. Legacy per-point
samples still read from `Result`. This is the main
backward-compatibility surface and is called out for the plan.

## API (`WaterController` + `MasterDataController`)

- `POST /api/water/receive` — body changes to `WaterDepartmentId`.
- `POST /api/water/prepare` — new: `{ sampleId, waterSamplingPointIds[] }`.
- `GET /api/masterdata/water-departments/{id}/sampling-points` (or reuse
  the existing grouped `water-departments` GET) — the preparation form
  needs each department's points and their assigned tests.
- `POST /api/water/locations/{id}/calculate` — new: count readings per
  location.
- `POST /api/water/detection` — new: pathogen Detected/Absent grid save.
- The old `POST /api/water/calculate` (per-TestOrder) is retained only
  for legacy samples, or removed if none remain — decided in the plan.

## Frontend

### Receiving (`MultiSampleEntryGrid.tsx`)
Move Water from its own branch into the EM/AC branch:
- Show the amber "configured in the Preparation step" banner for water.
- Replace the **Sampling Point** column with a **Department** column
  (sourced from water departments — `masterData` gains `waterDepartments`).
- Keep the **Quantity** column for water (ruling above): change the
  guard from `!isEM && !isAC` to also allow water.
- Receiving submit path maps the row's `waterDepartmentId` into the new
  `ReceiveWaterRequest`.

### Preparation (`WaterPreparationForm.tsx` — new, mirrors `EMPreparationForm`)
- Checklist: one checkbox per sampling point in the sample's department,
  showing each point's assigned tests.
- Confirm → `POST /api/water/prepare` with the checked point ids.
- `PreparationDialog.tsx` gains a Water branch keyed on
  `category === "Water" && waterDepartmentId != null`.

### Result entry (Testing Workspace)
- **Count grid:** a per-location grid for the TAMC-Water TestOrder —
  each sampling-point row accepts its plate readings, shows the computed
  average and pass/fail against that point's limits, saved via
  `calculate` per location. Follows the EM/AC batch result-entry grid
  pattern (component located during planning).
- **Pathogen detection grid (new window):** for a pathogen TestOrder,
  after the workflow reaches its terminal step, a grid lists every
  location with a Detected / Absent toggle; save writes all via
  `POST /api/water/detection`.

## Testing

- **Engine:** `ReceiveAsync` creates a department shell with no
  TestOrders; `PrepareAsync` generates one TestOrder per distinct code
  and one SampleLocation per point×test, rejects points from the wrong
  department, and links count-test limits; `CalculateLocationAsync`
  averages + compares + snapshots per location and only readies the
  TestOrder when all count locations are done; `RecordDetectionAsync`
  stores per-location Detected/Absent and completes the pathogen order
  when all locations are recorded.
- **Backward compatibility:** a legacy per-point water sample still
  calculates and reports.
- Extend `WaterAndEMEngineTests.cs` / add a `WaterBatchTests.cs`,
  following the existing InMemory-DB test style.

## Suggested phasing (each independently shippable/testable)

1. **Phase 1 — Receive + Prepare:** data model, migration, `ReceiveAsync`
   shell, `PrepareAsync`, receiving grid → Department, `WaterPreparationForm`,
   prepare endpoint. Water samples can be received and prepared into
   TestOrders + SampleLocations (no new result entry yet).
2. **Phase 2 — Count result entry per location:** `CalculateLocationAsync`,
   per-location count grid, TestOrder completion rule, reporting read
   from SampleLocations.
3. **Phase 3 — Pathogen detection grid:** `RecordDetectionAsync`, the
   Detected/Absent grid window, pathogen TestOrder completion.

Each phase gets its own implementation plan.

## Out of scope / YAGNI

- No migration of already-received (legacy) water samples.
- No change to the pathogen Observation workflow chain itself — only a
  new per-location terminal result-recording surface.
- No change to EM or After Cleaning behavior.
- No change to the Water **configuration** page shipped earlier today
  (departments, locations, limits) beyond consuming it here.
