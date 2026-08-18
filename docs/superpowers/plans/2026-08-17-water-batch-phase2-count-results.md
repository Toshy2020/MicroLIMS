# Water Batch Workflow — Phase 2 (Per-Location Count Result Entry) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let a water count TestOrder (TAMC-Water) prepared under Phase 1's batch model actually reach a result — currently every water TestOrder is hard-blocked (the shared engine's own guard rejects single-value `record-result` submissions once `SampleLocation` rows exist, and `WaterWorkflowEngine.CalculateAndCompareAsync` refuses batch-prepared orders by Phase 1's own design). This phase adds a water-specific per-location, multi-reading-average result path alongside EM/After Cleaning's existing single-CFU-×-shared-dilution path, on the same shared engine and UI shell they already use.

**Architecture:** Phase 1 investigation revealed that a water TestOrder, once prepared, is driven by the **same generic `TestWorkflowEngine`/`TestWorkflowController`/`TestWorkflowDialog`** that already runs EM/After Cleaning's incubation stepping and batch result entry — not by `WaterWorkflowEngine`, which is now unreachable for any newly-prepared water sample. `TAMC-Water` already has a `TestWorkflowStep` template configured (confirmed against the dev database), identical in shape to EM's own single-step TAMC variants, so no Test Master configuration work is needed. The only gap is that `TestWorkflowEngine.GetLocationsAsync`/`RecordBatchResultsAsync` and `ResultProjectionService.UpsertFromSampleLocationAsync` only know how to read `RoomTestConfiguration`/`MachinePartConfiguration` locations. This plan (1) teaches those to also read `WaterSamplingPoint`/`SamplingConfiguration` locations for identification and limits, and (2) adds a **new, separate** result-recording method (`RecordWaterBatchReadingsAsync`) that averages a list of raw plate readings per location with no dilution factor — confirmed by the user as the required model, distinct from EM/AC's CFU × shared-dilution-factor model, which is left completely untouched.

**Tech Stack:** .NET 8 / EF Core 8 (Npgsql, InMemory for tests, xUnit), ASP.NET Core controllers, React + TypeScript + MUI, axios (`apiClient`).

**Spec:** `docs/superpowers/specs/2026-08-17-water-batch-workflow-design.md` (Phase 2 of that spec's phasing — this plan supersedes that spec's original Phase 2 sketch, which proposed a bespoke `WaterWorkflowEngine.CalculateLocationAsync`/new dialog; investigation during planning found the shared `TestWorkflowEngine` machinery is the correct, minimal-duplication place for this, confirmed with the user)

## Global Constraints

- **EM/After Cleaning's existing `RecordBatchResultsAsync` (CFU × shared dilution factor) is never modified.** Every change to shared code (`GetLocationsAsync`, `LocationName`, `UpsertFromSampleLocationAsync`) must be additive (new fallback branches for `WaterSamplingPoint`/`SamplingConfiguration`) and must not alter Room/MachinePart behavior — `EMBatchLocationTests.cs` passing unchanged is the proof.
- Water's count result is **the average of the submitted plate readings, with no dilution factor** — `CalculatedResult = readings.Average()`, compared directly against the location's `SamplingConfiguration` Alert/Action/Spec via the existing `Compare(value, alert, action, spec)` helper already in `TestWorkflowEngine.cs`.
- Mutating endpoints follow the existing `TestWorkflowController` pattern: class-level `[Authorize(Roles = Analyst,Reviewer,SectionHead,SystemAdministrator)]`, no extra per-action restriction, wrapped in the existing `RunAsync<T>` helper for `WorkflowStepException` handling.
- `SampleLocation.RawReadings` (deferred from Phase 1 as YAGNI) is added now — comma-joined string of the submitted readings, mirroring how `WaterWorkflowEngine.CalculateAndCompareAsync` already stores `Result.RawValue` for legacy samples.

---

## File Structure

**Backend**
- `backend/MicroLIMS.Domain/Entities/SampleLocation.cs` (modify) — add `RawReadings`.
- `backend/MicroLIMS.Persistence/Migrations/*` (new) — adds the column.
- `backend/MicroLIMS.Application/Workflows/TestWorkflowEngine.cs` (modify) — `GetLocationsAsync` Includes, `LocationName` fallback, new `RecordWaterBatchReadingsAsync` + its request record, `SelectMediaAsync`'s preparation guard extended to Water.
- `backend/MicroLIMS.Application/Services/ResultProjectionService.cs` (modify) — `UpsertFromSampleLocationAsync` Include + `SubjectName` fallback.
- `backend/MicroLIMS.API/Controllers/TestWorkflowController.cs` (modify) — `/locations` projection fallbacks + `RawReadings`, new `POST /{testOrderId}/water-batch-readings` endpoint.
- `backend/MicroLIMS.Tests/WorkflowTests/WaterBatchResultTests.cs` (new).

**Frontend**
- `frontend/src/modules/testingWorkspace/services/TestWorkflowService.ts` (modify) — `recordWaterBatchReadings`.
- `frontend/src/modules/testingWorkspace/WaterLocationResultGridDialog.tsx` (new, mirrors `LocationResultGridDialog.tsx`).
- `frontend/src/modules/testingWorkspace/TestWorkflowDialog.tsx` (modify) — route water CountTest orders to the new dialog and into the batch-result button branches.

---

## Task 1: `SampleLocation.RawReadings` and migration

**Files:**
- Modify: `backend/MicroLIMS.Domain/Entities/SampleLocation.cs`
- Create: migration under `backend/MicroLIMS.Persistence/Migrations/`
- Test: `backend/MicroLIMS.Tests/WorkflowTests/WaterBatchResultTests.cs` (new file)

**Interfaces:**
- Produces: `SampleLocation.RawReadings : string?`

- [ ] **Step 1: Write the failing test**

Create `backend/MicroLIMS.Tests/WorkflowTests/WaterBatchResultTests.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.WorkflowTests;

public class WaterBatchResultTests
{
    private static MicroLimsDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MicroLimsDbContext(options);
    }

    [Fact]
    public async Task SampleLocation_StoresRawReadings()
    {
        await using var db = NewDb();
        var location = new SampleLocation { SampleId = 0, TestOrderId = 0, LocationType = LocationType.WaterSamplingPoint, RawReadings = "12,14,13" };
        db.SampleLocations.Add(location);
        await db.SaveChangesAsync();

        var loaded = await db.SampleLocations.SingleAsync();
        Assert.Equal("12,14,13", loaded.RawReadings);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test backend/MicroLIMS.Tests --filter SampleLocation_StoresRawReadings`
Expected: FAIL to compile — `SampleLocation.RawReadings` does not exist.

- [ ] **Step 3: Add the field**

In `backend/MicroLIMS.Domain/Entities/SampleLocation.cs`, add after `SamplingConfiguration`:

```csharp
    public string? RawReadings { get; set; }   // comma-joined plate readings (water count only)
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test backend/MicroLIMS.Tests --filter SampleLocation_StoresRawReadings`
Expected: PASS.

- [ ] **Step 5: Create the EF migration**

Run (from `backend/`):

```bash
dotnet ef migrations add AddSampleLocationRawReadings --project MicroLIMS.Persistence --startup-project MicroLIMS.API
```

Confirm the generated migration only adds one nullable text column (`RawReadings` on `SampleLocations`) — no data seed needed.

- [ ] **Step 6: Build the full solution**

Run: `dotnet build backend/MicroLIMS.sln`
Expected: 0 errors.

- [ ] **Step 7: Commit**

```bash
git add backend/MicroLIMS.Domain/Entities/SampleLocation.cs backend/MicroLIMS.Persistence/Migrations backend/MicroLIMS.Tests/WorkflowTests/WaterBatchResultTests.cs
git commit -m "feat(water): add SampleLocation.RawReadings for per-location plate readings"
```

---

## Task 2: `TestWorkflowEngine` learns to read water locations

**Files:**
- Modify: `backend/MicroLIMS.Application/Workflows/TestWorkflowEngine.cs`
- Test: `backend/MicroLIMS.Tests/WorkflowTests/WaterBatchResultTests.cs`

**Interfaces:**
- Consumes: `SampleLocation.WaterSamplingPointId/SamplingConfigurationId` (Phase 1), `.RawReadings` (Task 1).
- Produces: `GetLocationsAsync` now eagerly loads `WaterSamplingPoint` and `SamplingConfiguration`; `LocationName(SampleLocation)` resolves a water point's code.

This task only extends read paths — no new endpoint yet. It's tested indirectly through Task 3's `RecordWaterBatchReadingsAsync`, which depends on `GetLocationsAsync` returning the water navigation properties. Verifying it standalone here keeps the failure surface small.

- [ ] **Step 1: Write the failing test**

Add to `WaterBatchResultTests.cs`:

```csharp
    [Fact]
    public async Task GetLocationsAsync_LoadsWaterSamplingPointAndSamplingConfiguration()
    {
        await using var db = NewDb();
        var department = new WaterDepartment { Name = "WTU" };
        db.WaterDepartments.Add(department);
        await db.SaveChangesAsync();
        var point = new WaterSamplingPoint { Code = "SP-1", WaterDepartmentId = department.Id, AssignedTestCodes = new() { "TAMC-Water" } };
        db.WaterSamplingPoints.Add(point);
        var config = new SamplingConfiguration { WaterSamplingPointId = point.Id, TestCode = "TAMC-Water", AlertLimit = "10", ActionLimit = "50", SpecLimit = "100" };
        db.SamplingConfigurations.Add(config);
        await db.SaveChangesAsync();

        var sample = new Sample { ReferenceNumber = "WT0817500", Category = SampleCategory.Water, WaterDepartmentId = department.Id, ControlNumber = "CTRL-500", SampledBy = "Analyst" };
        var order = new TestOrder { TestCode = "TAMC-Water", Status = ApprovalStatus.Pending, CurrentStep = WorkflowStep.Waiting };
        sample.TestOrders.Add(order);
        sample.Locations.Add(new SampleLocation { TestOrder = order, LocationType = LocationType.WaterSamplingPoint, WaterSamplingPointId = point.Id, SamplingConfigurationId = config.Id });
        db.Samples.Add(sample);
        await db.SaveChangesAsync();

        // TestServiceFactory.TestWorkflow(db) is this test project's own
        // shared helper for constructing TestWorkflowEngine with all its
        // real dependencies wired to the same in-memory db - see
        // EMBatchLocationTests.cs, which uses the identical pattern.
        var engine = TestServiceFactory.TestWorkflow(db);

        var locations = await engine.GetLocationsAsync(order.Id);

        var loaded = Assert.Single(locations);
        Assert.NotNull(loaded.WaterSamplingPoint);
        Assert.Equal("SP-1", loaded.WaterSamplingPoint!.Code);
        Assert.NotNull(loaded.SamplingConfiguration);
        Assert.Equal("50", loaded.SamplingConfiguration!.ActionLimit);
    }
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test backend/MicroLIMS.Tests --filter GetLocationsAsync_LoadsWaterSamplingPointAndSamplingConfiguration`
Expected: FAIL — `loaded.WaterSamplingPoint` is null (not yet included in the query).

- [ ] **Step 3: Extend `GetLocationsAsync`**

In `backend/MicroLIMS.Application/Workflows/TestWorkflowEngine.cs`, change:

```csharp
    public async Task<List<SampleLocation>> GetLocationsAsync(int testOrderId) =>
        await _db.SampleLocations
            .Include(l => l.RoomTestConfiguration!).ThenInclude(c => c.Room)
            .Include(l => l.MachinePartConfiguration!).ThenInclude(c => c.MachinePart)
            .Where(l => l.TestOrderId == testOrderId)
            .ToListAsync();
```

to:

```csharp
    public async Task<List<SampleLocation>> GetLocationsAsync(int testOrderId) =>
        await _db.SampleLocations
            .Include(l => l.RoomTestConfiguration!).ThenInclude(c => c.Room)
            .Include(l => l.MachinePartConfiguration!).ThenInclude(c => c.MachinePart)
            .Include(l => l.WaterSamplingPoint)
            .Include(l => l.SamplingConfiguration)
            .Where(l => l.TestOrderId == testOrderId)
            .ToListAsync();
```

- [ ] **Step 4: Extend `LocationName`**

Change:

```csharp
    private static string LocationName(SampleLocation l) =>
        l.RoomTestConfiguration?.Room?.Name ?? l.MachinePartConfiguration?.MachinePart?.Name ?? $"Location {l.Id}";
```

to:

```csharp
    private static string LocationName(SampleLocation l) =>
        l.RoomTestConfiguration?.Room?.Name ?? l.MachinePartConfiguration?.MachinePart?.Name ?? l.WaterSamplingPoint?.Code ?? $"Location {l.Id}";
```

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test backend/MicroLIMS.Tests --filter GetLocationsAsync_LoadsWaterSamplingPointAndSamplingConfiguration`
Expected: PASS.

- [ ] **Step 6: Run the EM/AC regression suite**

Run: `dotnet test backend/MicroLIMS.Tests --filter EMBatchLocationTests`
Expected: PASS unchanged — proves the added `Include`s and `LocationName` fallback don't affect Room/MachinePart behavior.

- [ ] **Step 7: Commit**

```bash
git add backend/MicroLIMS.Application/Workflows/TestWorkflowEngine.cs backend/MicroLIMS.Tests/WorkflowTests/WaterBatchResultTests.cs
git commit -m "feat(water): TestWorkflowEngine reads WaterSamplingPoint/SamplingConfiguration on batch locations"
```

---

## Task 3: `RecordWaterBatchReadingsAsync` — the water-specific result path

**Files:**
- Modify: `backend/MicroLIMS.Application/Workflows/TestWorkflowEngine.cs`
- Modify: `backend/MicroLIMS.API/Controllers/TestWorkflowController.cs`
- Test: `backend/MicroLIMS.Tests/WorkflowTests/WaterBatchResultTests.cs`

**Interfaces:**
- Consumes: `GetLocationsAsync` (Task 2), `Compare(decimal, string?, string?, string?)` (existing private static helper — reused, not duplicated), `LoadOpenBatchWindowAsync`, `RequireMinimumDurationElapsed`/`RequireStage2MinimumDurationElapsed`, `StatusSeverity` (all existing private helpers in this file — reused verbatim).
- Produces: `record WaterBatchLocationReadings(int SampleLocationId, List<decimal> Readings)`; `ITestWorkflowEngine.RecordWaterBatchReadingsAsync(int testOrderId, List<WaterBatchLocationReadings> locations, int userId) : Task<TestWorkflowResult>`; `POST /api/test-workflow/{testOrderId}/water-batch-readings`.

- [ ] **Step 1: Write the failing tests**

Add to `WaterBatchResultTests.cs`. First a shared setup helper (place it above the test methods, inside the class) — this mirrors `EMBatchLocationTests.cs`'s own `SeedCountTestWorkflowAsync` + `HappyPath_ThreeRooms_...` pattern exactly, substituting Water's real `PrepareAsync` for EM's, and driving the incubation window through the real `SelectMediaAsync` (never hand-crafting an `Incubation` row):

```csharp
    private static async Task<(MicroLIMS.Application.Workflows.TestWorkflowEngine engine, int testOrderId, int locationAId, int locationBId)>
        SetupPreparedWaterCountOrderAsync(MicroLimsDbContext db)
    {
        var department = new WaterDepartment { Name = "WTU" };
        db.WaterDepartments.Add(department);
        await db.SaveChangesAsync();

        var pointA = new WaterSamplingPoint { Code = "SP-A", WaterDepartmentId = department.Id, AssignedTestCodes = new() { "TAMC-Water" } };
        var pointB = new WaterSamplingPoint { Code = "SP-B", WaterDepartmentId = department.Id, AssignedTestCodes = new() { "TAMC-Water" } };
        db.WaterSamplingPoints.AddRange(pointA, pointB);
        await db.SaveChangesAsync();
        db.SamplingConfigurations.Add(new SamplingConfiguration { WaterSamplingPointId = pointA.Id, TestCode = "TAMC-Water", AlertLimit = "10", ActionLimit = "50", SpecLimit = "100" });
        db.SamplingConfigurations.Add(new SamplingConfiguration { WaterSamplingPointId = pointB.Id, TestCode = "TAMC-Water", AlertLimit = "10", ActionLimit = "50", SpecLimit = "100" });
        await db.SaveChangesAsync();

        // Single-step CountTest template, minHours = 0 so the batch result
        // can be recorded immediately - same shape as EMBatchLocationTests'
        // SeedCountTestWorkflowAsync, just for "TAMC-Water".
        var mediaType = new MediaType { Class = MediaClass.GeneralAgar, IncubationMinHours = 24, IncubationMaxHours = 48, RequiredTemperatureMin = 20, RequiredTemperatureMax = 25 };
        var testDefinition = new TestDefinition { Code = "TAMC-Water", DisplayName = "TAMC-Water", WorkflowType = WorkflowType.CountTest };
        db.MediaTypes.Add(mediaType);
        db.TestDefinitions.Add(testDefinition);
        await db.SaveChangesAsync();

        db.TestWorkflowSteps.Add(new TestWorkflowStep
        {
            TestDefinitionId = testDefinition.Id, StepOrder = 1, StepName = "CountIncubation", MediaTypeId = mediaType.Id,
            IncubationMinHours = 0, IncubationMaxHours = 24, TemperatureMin = 20, TemperatureMax = 25, IsFinalStep = true
        });

        var material = new Material
        {
            MaterialType = MaterialType.DehydratedMedia, MaterialName = "TSA Powder", ManufacturerName = "Himedia",
            BatchNumber = "LOT-W01", ReceivingDate = DateTime.UtcNow.AddDays(-10), Code = "TSA-WATER",
            Location = "Micro Lab", QuantityReceived = 500, QuantityRemaining = 500, Unit = MaterialUnit.Gram
        };
        db.Materials.Add(material);
        await db.SaveChangesAsync();

        var media = new Media
        {
            MediaTypeId = mediaType.Id, MaterialId = material.Id, LotNumber = "TSA/WATER", IsReleasedForUse = true,
            Status = MediaStatus.Active, ExpiryDate = DateTime.UtcNow.AddDays(30)
        };
        var equipment = new Equipment { Name = "Incubator Water", Code = "INC-WATER", Type = EquipmentType.Incubator };
        db.Media.Add(media);
        db.Equipment.Add(equipment);
        await db.SaveChangesAsync();

        var waterEngine = new MicroLIMS.Application.Workflows.WaterWorkflowEngine(db, new ReferenceNumberGenerator(db));
        var sample = await waterEngine.ReceiveAsync(new MicroLIMS.Application.Workflows.WaterReceiveRequest(department.Id, 0, "500ml", "Analyst", "CTRL-600", 1));
        var prepared = await waterEngine.PrepareAsync(sample.Id, new List<int> { pointA.Id, pointB.Id }, 1);
        var order = prepared.TestOrders.Single();

        var engine = TestServiceFactory.TestWorkflow(db);
        await engine.SelectMediaAsync(order.Id, "CountIncubation", media.Id, equipment.Id, 1);

        var locations = await engine.GetLocationsAsync(order.Id);
        var locationA = locations.Single(l => l.WaterSamplingPointId == pointA.Id);
        var locationB = locations.Single(l => l.WaterSamplingPointId == pointB.Id);

        return (engine, order.Id, locationA.Id, locationB.Id);
    }

    [Fact]
    public async Task RecordWaterBatchReadingsAsync_AveragesReadingsPerLocationNoDilution()
    {
        await using var db = NewDb();
        var (engine, testOrderId, locationAId, locationBId) = await SetupPreparedWaterCountOrderAsync(db);

        var result = await engine.RecordWaterBatchReadingsAsync(testOrderId, new List<MicroLIMS.Application.Workflows.WaterBatchLocationReadings>
        {
            new(locationAId, new List<decimal> { 12, 14 }),   // avg 13 -> AlertLimitExceeded
            new(locationBId, new List<decimal> { 5, 5 })      // avg 5  -> WithinLimits
        }, 1);

        Assert.True(result.AllStepsComplete);

        var locationA = await db.SampleLocations.FirstAsync(l => l.Id == locationAId);
        Assert.Equal(13m, locationA.CalculatedResult);
        Assert.Equal("AlertLimitExceeded", locationA.Status);
        Assert.Equal("12,14", locationA.RawReadings);
        Assert.Equal(0m, locationA.DilutionFactor); // never set for water - CFU x dilution model is untouched

        var locationB = await db.SampleLocations.FirstAsync(l => l.Id == locationBId);
        Assert.Equal(5m, locationB.CalculatedResult);
        Assert.Equal("WithinLimits", locationB.Status);
    }

    [Fact]
    public async Task RecordWaterBatchReadingsAsync_TransitionsTestOrderToReady()
    {
        await using var db = NewDb();
        var (engine, testOrderId, locationAId, locationBId) = await SetupPreparedWaterCountOrderAsync(db);

        await engine.RecordWaterBatchReadingsAsync(testOrderId, new List<MicroLIMS.Application.Workflows.WaterBatchLocationReadings>
        {
            new(locationAId, new List<decimal> { 1 }),
            new(locationBId, new List<decimal> { 1 })
        }, 1);

        var order = await db.TestOrders.FirstAsync(o => o.Id == testOrderId);
        Assert.Equal(WorkflowStep.Ready, order.CurrentStep);
    }

    [Fact]
    public async Task RecordWaterBatchReadingsAsync_RejectsMissingLocation()
    {
        await using var db = NewDb();
        var (engine, testOrderId, locationAId, _) = await SetupPreparedWaterCountOrderAsync(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() => engine.RecordWaterBatchReadingsAsync(testOrderId,
            new List<MicroLIMS.Application.Workflows.WaterBatchLocationReadings> { new(locationAId, new List<decimal> { 1 }) }, 1));
    }

    [Fact]
    public async Task RecordWaterBatchReadingsAsync_RejectsEmptyReadingsForALocation()
    {
        await using var db = NewDb();
        var (engine, testOrderId, locationAId, locationBId) = await SetupPreparedWaterCountOrderAsync(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() => engine.RecordWaterBatchReadingsAsync(testOrderId,
            new List<MicroLIMS.Application.Workflows.WaterBatchLocationReadings>
            {
                new(locationAId, new List<decimal>()),
                new(locationBId, new List<decimal> { 1 })
            }, 1));
    }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test backend/MicroLIMS.Tests --filter WaterBatchResultTests`
Expected: FAIL to compile — `RecordWaterBatchReadingsAsync`/`WaterBatchLocationReadings` don't exist yet.

- [ ] **Step 3: Add the request record and interface member**

In `backend/MicroLIMS.Application/Workflows/TestWorkflowEngine.cs`, add near `BatchLocationResult`:

```csharp
// One location's plate readings submitted from WaterLocationResultGridDialog -
// water batch results only. Averaged directly with no dilution factor,
// unlike BatchLocationResult's CFU x dilution model (EM/After Cleaning).
public record WaterBatchLocationReadings(int SampleLocationId, List<decimal> Readings);
```

Add to `ITestWorkflowEngine`, after `RecordBatchResultsAsync`:

```csharp
    Task<TestWorkflowResult> RecordWaterBatchReadingsAsync(int testOrderId, List<WaterBatchLocationReadings> locations, int userId);
```

- [ ] **Step 4: Implement `RecordWaterBatchReadingsAsync`**

Add directly after `RecordBatchResultsAsync`'s closing brace (mirrors its structure exactly, only the per-location computation differs):

```csharp
    // Water-only batch result entry: each sampling point gets its own
    // set of raw plate readings, averaged directly (no shared dilution
    // factor) and compared to that point's own SamplingConfiguration
    // limits - the multi-reading model water has always used, now
    // applied per-location instead of per-TestOrder. Everything about
    // opening/closing the incubation window and transitioning the
    // TestOrder is identical to RecordBatchResultsAsync; only the
    // result computation differs.
    public async Task<TestWorkflowResult> RecordWaterBatchReadingsAsync(int testOrderId, List<WaterBatchLocationReadings> locations, int userId)
    {
        var (order, definition) = await LoadWithTemplateAsync(testOrderId);
        if (order.CurrentStep != WorkflowStep.Incubating)
            throw new InvalidOperationException("Media must be selected for this test before batch results can be recorded.");

        var (openIncubation, step) = await LoadOpenBatchWindowAsync(testOrderId, definition);
        var isFinalIncubation = step.IsFinalStep || step.RequiresIncubationTransfer || (step.StepOrder == definition.Steps.Max(s => s.StepOrder));
        if (!isFinalIncubation)
            throw new InvalidOperationException($"\"{step.StepName}\" is not the final incubation window yet - close it and start the next window first.");

        if (step.RequiresIncubationTransfer)
        {
            if (openIncubation.StageNumber != 2)
                throw new WorkflowStepException(WorkflowErrorCodes.IncubationStage2NotStarted,
                    $"Step \"{step.StepName}\" requires stage 2 incubation to be started before results can be recorded.");

            RequireStage2MinimumDurationElapsed(openIncubation, step);
        }
        else
        {
            RequireMinimumDurationElapsed(openIncubation, step);
        }

        var sampleLocations = await GetLocationsAsync(testOrderId);
        if (sampleLocations.Count == 0)
            throw new InvalidOperationException("No locations are assigned to this test order.");

        var submitted = locations.ToDictionary(l => l.SampleLocationId);
        var missing = sampleLocations.Where(l => !submitted.ContainsKey(l.Id)).ToList();
        if (missing.Count > 0)
            throw new InvalidOperationException($"Results are missing for: {string.Join(", ", missing.Select(LocationName))}.");

        var emptyReadings = sampleLocations.Where(l => submitted[l.Id].Readings.Count == 0).ToList();
        if (emptyReadings.Count > 0)
            throw new InvalidOperationException($"At least one plate reading is required for: {string.Join(", ", emptyReadings.Select(LocationName))}.");

        var worstStatus = "WithinLimits";
        var conformCount = 0;
        foreach (var location in sampleLocations)
        {
            var readings = submitted[location.Id].Readings;
            var average = readings.Average();

            var alertLimit = location.SamplingConfiguration?.AlertLimit;
            var actionLimit = location.SamplingConfiguration?.ActionLimit;
            var specLimit = location.SamplingConfiguration?.SpecLimit;
            var (status, _) = Compare(average, alertLimit, actionLimit, specLimit);

            location.RawReadings = string.Join(",", readings);
            location.CalculatedResult = average;
            location.ReportedResult = average.ToString("0.##");
            location.AlertLimit = alertLimit;
            location.ActionLimit = actionLimit;
            location.SpecLimit = specLimit;
            location.Status = status;
            location.EnteredAt = DateTime.UtcNow;
            location.EnteredByUserId = userId;

            if (status == "WithinLimits") conformCount++;
            if (StatusSeverity(status) > StatusSeverity(worstStatus)) worstStatus = status;
        }

        var summary = $"{sampleLocations.Count} locations: {conformCount} conform, {sampleLocations.Count - conformCount} alert/action/spec";

        _db.Results.Add(new Result
        {
            TestOrderId = testOrderId,
            RawValue = summary,
            InterpretedValue = $"{summary} (worst: {worstStatus})",
            Type = ResultType.Numeric,
            EnteredByUserId = userId
        });

        openIncubation.CompletedAt = DateTime.UtcNow;
        openIncubation.Outcome = summary;

        foreach (var location in sampleLocations)
            await _resultProjection.UpsertFromSampleLocationAsync(location.Id);

        await WorkflowStateMachine.TransitionAsync(_db, order, WorkflowStep.Ready, userId, $"Water batch readings recorded: {summary}");
        await _sampleReviewService.AutoSubmitForReviewIfReadyAsync(order.SampleId, userId);
        await _db.SaveChangesAsync();

        return new TestWorkflowResult(summary, true, true, summary, null, null, worstStatus);
    }
```

- [ ] **Step 5: Skipped — do not extend the "Preparation not complete" guard to Water**

The plan originally proposed adding `SampleCategory.Water` to this EM/AC-only guard "for consistency." **Do not do this.** `CountTestWorkflowTests.cs` has legitimate, pre-existing coverage of a `SampleCategory.Water` sample with **no** `SampleLocation` rows (the single-order, non-batch `RecordResultAsync` path, still valid for direct/legacy-shaped test fixtures). Extending the guard to Water breaks that coverage, confirmed by running the full suite after this task and seeing `CountTestWorkflowTests`/`IncubationTransferTests`/`ResultProjectionTests`/`SampleReviewApprovalTests`/`SampleSummaryIncubationStageTests` fail with "Preparation not complete - no locations assigned to this test." Leave `SelectMediaAsync`'s guard exactly as it was — EM/AC only.

- [ ] **Step 6: Add the controller endpoint**

In `backend/MicroLIMS.API/Controllers/TestWorkflowController.cs`, add the request records near `BatchResultsRequest`:

```csharp
public record WaterBatchLocationRequest(int SampleLocationId, List<decimal> Readings);
public record WaterBatchReadingsRequest(List<WaterBatchLocationRequest> Locations);
```

Add the action after `RecordBatchResults`:

```csharp
    // Water batch count entry - one set of plate readings per sampling
    // point, averaged with no shared dilution factor (see
    // TestWorkflowEngine.RecordWaterBatchReadingsAsync).
    [HttpPost("{testOrderId}/water-batch-readings")]
    public Task<IActionResult> RecordWaterBatchReadings(int testOrderId, WaterBatchReadingsRequest request) => RunAsync(() =>
    {
        var locations = request.Locations.Select(l => new WaterBatchLocationReadings(l.SampleLocationId, l.Readings)).ToList();
        return _engine.RecordWaterBatchReadingsAsync(testOrderId, locations, CurrentUserId);
    });
```

Extend the `/locations` GET projection (`GetLocations` action) to include the water fallbacks and `RawReadings`. Change:

```csharp
        return locations.Select(l => new
        {
            l.Id,
            locationType = l.LocationType.ToString(),
            locationName = l.RoomTestConfiguration?.Room?.Name ?? l.MachinePartConfiguration?.MachinePart?.Name ?? string.Empty,
            gradeClassification = l.RoomTestConfiguration?.Room?.GradeClassification,
            alertLimit = l.AlertLimit ?? l.RoomTestConfiguration?.AlertLimit ?? l.MachinePartConfiguration?.AlertLimit,
            actionLimit = l.ActionLimit ?? l.RoomTestConfiguration?.ActionLimit ?? l.MachinePartConfiguration?.ActionLimit,
            specLimit = l.SpecLimit ?? l.RoomTestConfiguration?.SpecLimit ?? l.MachinePartConfiguration?.SpecLimit,
            l.CFUResult,
            l.CalculatedResult,
            l.ReportedResult,
            l.Status,
            l.EnteredAt
        });
```

to:

```csharp
        return locations.Select(l => new
        {
            l.Id,
            locationType = l.LocationType.ToString(),
            locationName = l.RoomTestConfiguration?.Room?.Name ?? l.MachinePartConfiguration?.MachinePart?.Name ?? l.WaterSamplingPoint?.Code ?? string.Empty,
            gradeClassification = l.RoomTestConfiguration?.Room?.GradeClassification,
            alertLimit = l.AlertLimit ?? l.RoomTestConfiguration?.AlertLimit ?? l.MachinePartConfiguration?.AlertLimit ?? l.SamplingConfiguration?.AlertLimit,
            actionLimit = l.ActionLimit ?? l.RoomTestConfiguration?.ActionLimit ?? l.MachinePartConfiguration?.ActionLimit ?? l.SamplingConfiguration?.ActionLimit,
            specLimit = l.SpecLimit ?? l.RoomTestConfiguration?.SpecLimit ?? l.MachinePartConfiguration?.SpecLimit ?? l.SamplingConfiguration?.SpecLimit,
            l.CFUResult,
            l.CalculatedResult,
            l.ReportedResult,
            l.RawReadings,
            l.Status,
            l.EnteredAt
        });
```

- [ ] **Step 7: Run tests to verify they pass**

Run: `dotnet test backend/MicroLIMS.Tests --filter WaterBatchResultTests`
Expected: PASS.

- [ ] **Step 8: Run the full backend suite**

Run: `dotnet test backend/MicroLIMS.sln`
Expected: PASS — confirms `EMBatchLocationTests.cs` and every other existing test is unaffected.

- [ ] **Step 9: Commit**

```bash
git add backend/MicroLIMS.Application/Workflows/TestWorkflowEngine.cs backend/MicroLIMS.API/Controllers/TestWorkflowController.cs backend/MicroLIMS.Tests/WorkflowTests/WaterBatchResultTests.cs
git commit -m "feat(water): add RecordWaterBatchReadingsAsync - per-location plate-reading averages"
```

---

## Task 4: Reporting projection reads water locations

**Files:**
- Modify: `backend/MicroLIMS.Application/Services/ResultProjectionService.cs`
- Test: `backend/MicroLIMS.Tests/WorkflowTests/WaterBatchResultTests.cs`

**Interfaces:**
- Consumes: `SampleLocation.WaterSamplingPoint` (Phase 1), the row already written by Task 3.
- Produces: `ResultRecord.SubjectName` populated for water batch locations, same as it already is for Room/MachinePart.

- [ ] **Step 1: Write the failing test**

Add to `WaterBatchResultTests.cs`:

```csharp
    [Fact]
    public async Task UpsertFromSampleLocationAsync_SetsSubjectNameFromWaterSamplingPoint()
    {
        await using var db = NewDb();
        var (engine, testOrderId, locationAId, locationBId) = await SetupPreparedWaterCountOrderAsync(db);

        await engine.RecordWaterBatchReadingsAsync(testOrderId, new List<MicroLIMS.Application.Workflows.WaterBatchLocationReadings>
        {
            new(locationAId, new List<decimal> { 1 }),
            new(locationBId, new List<decimal> { 1 })
        }, 1);

        // ResultRecord rows are keyed by (SourceTable, SourceId, Round), not
        // a dedicated FK column - see ResultProjectionService.GetOrCreateAsync.
        var record = await db.ResultRecords.FirstAsync(r => r.SourceTable == "SampleLocation" && r.SourceId == locationAId);
        Assert.Equal("SP-A", record.SubjectName);
    }
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test backend/MicroLIMS.Tests --filter UpsertFromSampleLocationAsync_SetsSubjectNameFromWaterSamplingPoint`
Expected: FAIL — `SubjectName` is empty (water fallback not wired in).

- [ ] **Step 3: Extend the query and fallback**

In `backend/MicroLIMS.Application/Services/ResultProjectionService.cs`, change:

```csharp
        var location = await _db.SampleLocations
            .Include(l => l.Sample!)
            .Include(l => l.TestOrder!)
            .Include(l => l.RoomTestConfiguration!).ThenInclude(c => c.Room)
            .Include(l => l.MachinePartConfiguration!).ThenInclude(c => c.MachinePart)
            .FirstOrDefaultAsync(l => l.Id == sampleLocationId)
            ?? throw new InvalidOperationException($"SampleLocation {sampleLocationId} not found.");
```

to:

```csharp
        var location = await _db.SampleLocations
            .Include(l => l.Sample!)
            .Include(l => l.TestOrder!)
            .Include(l => l.RoomTestConfiguration!).ThenInclude(c => c.Room)
            .Include(l => l.MachinePartConfiguration!).ThenInclude(c => c.MachinePart)
            .Include(l => l.WaterSamplingPoint)
            .FirstOrDefaultAsync(l => l.Id == sampleLocationId)
            ?? throw new InvalidOperationException($"SampleLocation {sampleLocationId} not found.");
```

And change:

```csharp
        record.SubjectName = location.RoomTestConfiguration?.Room?.Name ?? location.MachinePartConfiguration?.MachinePart?.Name ?? string.Empty;
```

to:

```csharp
        record.SubjectName = location.RoomTestConfiguration?.Room?.Name ?? location.MachinePartConfiguration?.MachinePart?.Name ?? location.WaterSamplingPoint?.Code ?? string.Empty;
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test backend/MicroLIMS.Tests --filter UpsertFromSampleLocationAsync_SetsSubjectNameFromWaterSamplingPoint`
Expected: PASS.

- [ ] **Step 5: Run the full backend suite**

Run: `dotnet test backend/MicroLIMS.sln`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add backend/MicroLIMS.Application/Services/ResultProjectionService.cs backend/MicroLIMS.Tests/WorkflowTests/WaterBatchResultTests.cs
git commit -m "feat(water): reporting projection resolves SubjectName from WaterSamplingPoint"
```

---

## Task 5: Frontend service + `WaterLocationResultGridDialog`

**Files:**
- Modify: `frontend/src/modules/testingWorkspace/services/TestWorkflowService.ts`
- Create: `frontend/src/modules/testingWorkspace/WaterLocationResultGridDialog.tsx` (mirrors `LocationResultGridDialog.tsx`)

**Interfaces:**
- Consumes: `GET /api/test-workflow/{testOrderId}/locations` (extended, Task 3), `POST /api/test-workflow/{testOrderId}/water-batch-readings` (Task 3).
- Produces: `<WaterLocationResultGridDialog open testOrderId testCode displayName minReadyAt onClose onSubmitted />` — same prop shape as `LocationResultGridDialog`, so `TestWorkflowDialog` can swap between them with no other changes.

- [ ] **Step 1: Add the service method**

In `frontend/src/modules/testingWorkspace/services/TestWorkflowService.ts`, add after `recordBatchResults`:

```typescript
  recordWaterBatchReadings: (testOrderId: number, locations: { sampleLocationId: number; readings: number[] }[]) =>
    apiClient.post(`/test-workflow/${testOrderId}/water-batch-readings`, { locations }).then((r) => r.data.data),
```

- [ ] **Step 2: Create the dialog**

Create `frontend/src/modules/testingWorkspace/WaterLocationResultGridDialog.tsx`, mirroring `LocationResultGridDialog.tsx`'s structure but with a per-row list of readings instead of a single CFU value, and no shared dilution field:

```tsx
import { useEffect, useMemo, useState } from "react";
import { Box, Table, TableHead, TableRow, TableCell, TableBody, TextField, IconButton, Button, Stack, Alert, Typography } from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import CloseIcon from "@mui/icons-material/Close";
import { FloatingDialog } from "../../components/FloatingDialog";
import { StatusBadge } from "../../components/StatusBadge";
import { LoadingSpinner } from "../../components/LoadingSpinner";
import { TestWorkflowService } from "./services/TestWorkflowService";

interface LocationRow {
  id: number;
  locationType: string;
  locationName: string;
  alertLimit: string | null;
  actionLimit: string | null;
  specLimit: string | null;
  rawReadings: string | null;
  calculatedResult: number | null;
  status: string | null;
}

interface Props {
  open: boolean;
  testOrderId: number;
  testCode: string;
  displayName: string;
  minReadyAt: Date | null;
  onClose: () => void;
  onSubmitted: () => void;
}

// Same Spec -> Action -> Alert precedence as the backend's
// TestWorkflowEngine.Compare - mirrored here only for the live preview
// as the analyst types; the server recomputes and is the authority on
// the persisted Status.
function compareStatus(value: number, alert: string | null, action: string | null, spec: string | null): string {
  const specLimit = spec !== null ? Number(spec) : NaN;
  if (!Number.isNaN(specLimit) && value > specLimit) return "OutOfSpecification";
  const actionLimit = action !== null ? Number(action) : NaN;
  if (!Number.isNaN(actionLimit) && value > actionLimit) return "ActionLimitExceeded";
  const alertLimit = alert !== null ? Number(alert) : NaN;
  if (!Number.isNaN(alertLimit) && value > alertLimit) return "AlertLimitExceeded";
  return "WithinLimits";
}

export function WaterLocationResultGridDialog({ open, testOrderId, testCode, displayName, minReadyAt, onClose, onSubmitted }: Props) {
  const isTimeReady = !minReadyAt || new Date() >= minReadyAt;
  const [rows, setRows] = useState<LocationRow[] | null>(null);
  const [readingsByLocation, setReadingsByLocation] = useState<Record<number, string[]>>({});
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    if (!open) return;
    setRows(null);
    setError(null);
    setReadingsByLocation({});
    TestWorkflowService.getLocations(testOrderId).then((data) => {
      setRows(data);
      const initial: Record<number, string[]> = {};
      data.forEach((r: LocationRow) => {
        initial[r.id] = r.rawReadings ? r.rawReadings.split(",") : [""];
      });
      setReadingsByLocation(initial);
    });
  }, [open, testOrderId]);

  const updateReading = (locationId: number, i: number, value: string) =>
    setReadingsByLocation((m) => ({ ...m, [locationId]: (m[locationId] ?? [""]).map((v, idx) => (idx === i ? value : v)) }));
  const addReading = (locationId: number) =>
    setReadingsByLocation((m) => ({ ...m, [locationId]: [...(m[locationId] ?? [""]), ""] }));
  const removeReading = (locationId: number, i: number) =>
    setReadingsByLocation((m) => {
      const current = m[locationId] ?? [""];
      return { ...m, [locationId]: current.length > 1 ? current.filter((_, idx) => idx !== i) : current };
    });

  const computed = useMemo(() => {
    if (!rows) return [];
    return rows.map((r) => {
      const raw = readingsByLocation[r.id] ?? [];
      const parsed = raw.map(Number).filter((n) => !Number.isNaN(n) && raw.some((v) => v !== ""));
      const values = raw.filter((v) => v !== "").map(Number);
      if (values.length === 0 || values.some(Number.isNaN)) {
        return { ...r, liveAverage: null as number | null, liveStatus: null as string | null };
      }
      const average = values.reduce((a, b) => a + b, 0) / values.length;
      const status = compareStatus(average, r.alertLimit, r.actionLimit, r.specLimit);
      return { ...r, liveAverage: average, liveStatus: status };
    });
  }, [rows, readingsByLocation]);

  const allEntered = rows !== null && rows.length > 0 && rows.every((r) => {
    const values = (readingsByLocation[r.id] ?? []).filter((v) => v !== "").map(Number);
    return values.length > 0 && values.every((v) => !Number.isNaN(v));
  });

  const conformCount = computed.filter((r) => r.liveStatus === "WithinLimits").length;
  const worstStatus = computed.reduce<string>((worst, r) => {
    const order = ["WithinLimits", "AlertLimitExceeded", "ActionLimitExceeded", "OutOfSpecification"];
    if (!r.liveStatus) return worst;
    return order.indexOf(r.liveStatus) > order.indexOf(worst) ? r.liveStatus : worst;
  }, "WithinLimits");

  const submit = async () => {
    if (!rows) return;
    setError(null);
    setSubmitting(true);
    try {
      await TestWorkflowService.recordWaterBatchReadings(
        testOrderId,
        rows.map((r) => ({
          sampleLocationId: r.id,
          readings: (readingsByLocation[r.id] ?? []).filter((v) => v !== "").map(Number)
        }))
      );
      onSubmitted();
    } catch (e: any) {
      setError(e?.response?.data?.message ?? "Could not record batch readings.");
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <FloatingDialog open={open} title={`${testCode} Results — ${displayName}`} onClose={onClose}>
      {!rows && !error && <LoadingSpinner />}
      {error && !rows && <Alert severity="error">{error}</Alert>}
      {rows && (
        <Stack spacing={2}>
          {error && <Alert severity="error">{error}</Alert>}
          <Box sx={{ overflowX: "auto" }}>
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell>Sampling Point</TableCell>
                  <TableCell>Limits (Alert / Action / Spec)</TableCell>
                  <TableCell>Plate Readings</TableCell>
                  <TableCell>Average</TableCell>
                  <TableCell>Status</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {computed.map((r) => (
                  <TableRow key={r.id}>
                    <TableCell>{r.locationName}</TableCell>
                    <TableCell sx={{ fontSize: 12, color: "text.secondary" }}>
                      {r.alertLimit ?? "—"} / {r.actionLimit ?? "—"} / {r.specLimit ?? "—"}
                    </TableCell>
                    <TableCell>
                      <Stack spacing={0.5}>
                        {(readingsByLocation[r.id] ?? [""]).map((v, i) => (
                          <Stack direction="row" spacing={0.5} key={i} alignItems="center">
                            <TextField
                              size="small" type="number" sx={{ width: 90 }}
                              value={v}
                              onChange={(e) => updateReading(r.id, i, e.target.value)}
                            />
                            <IconButton size="small" onClick={() => removeReading(r.id, i)}><CloseIcon fontSize="small" /></IconButton>
                          </Stack>
                        ))}
                        <Button size="small" startIcon={<AddIcon />} onClick={() => addReading(r.id)} sx={{ alignSelf: "flex-start" }}>
                          Add Plate
                        </Button>
                      </Stack>
                    </TableCell>
                    <TableCell>{r.liveAverage != null ? r.liveAverage.toFixed(2) : "—"}</TableCell>
                    <TableCell>{r.liveStatus ? <StatusBadge status={r.liveStatus} /> : "—"}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </Box>
          <Typography sx={{ fontSize: 13 }}>
            {conformCount}/{rows.length} locations within spec — worst status: <StatusBadge status={worstStatus} />
          </Typography>
          {!isTimeReady && minReadyAt && (
            <Alert severity="warning">Results cannot be submitted before {minReadyAt.toLocaleString()}.</Alert>
          )}
          <Stack direction="row" justifyContent="flex-end">
            <Button variant="contained" disabled={!allEntered || !isTimeReady || submitting} onClick={submit}>
              {submitting ? "Submitting…" : "Submit Results"}
            </Button>
          </Stack>
        </Stack>
      )}
    </FloatingDialog>
  );
}
```

- [ ] **Step 3: Type-check**

Run: `cd frontend && npx tsc --noEmit`
Expected: no errors.

- [ ] **Step 4: Commit**

```bash
git add frontend/src/modules/testingWorkspace/services/TestWorkflowService.ts frontend/src/modules/testingWorkspace/WaterLocationResultGridDialog.tsx
git commit -m "feat(water): add WaterLocationResultGridDialog for per-location plate-reading entry"
```

---

## Task 6: Route water CountTest orders into the new dialog

**Files:**
- Modify: `frontend/src/modules/testingWorkspace/TestWorkflowDialog.tsx`

**Interfaces:**
- Consumes: `WaterLocationResultGridDialog` (Task 5).

`TestWorkflowDialog` currently gates every "this is a batch order with a location grid" branch on `isEmOrAfterCleaning`. Water CountTest orders need the same batch treatment (skip the single-reading `enter-result` phase, show "Record Results", open a location grid) but must route to `WaterLocationResultGridDialog` instead of `LocationResultGridDialog`.

- [ ] **Step 1: Add an `isWaterCountBatch` flag and extend the gating conditions**

In `frontend/src/modules/testingWorkspace/TestWorkflowDialog.tsx`, add the import and the flag near the top:

```tsx
import { WaterLocationResultGridDialog } from "./WaterLocationResultGridDialog";
```

Change:

```tsx
export function TestWorkflowDialog({ testOrderId, testCode, category, displayName }: Props) {
  const isEmOrAfterCleaning = category === "EnvironmentalMonitoring" || category === "AfterCleaning";
```

to:

```tsx
export function TestWorkflowDialog({ testOrderId, testCode, category, displayName }: Props) {
  const isEmOrAfterCleaning = category === "EnvironmentalMonitoring" || category === "AfterCleaning";
  // Water's TAMC-Water TestOrder is also SampleLocation-batched (Phase 1
  // PrepareAsync), but its result computation is the per-location
  // plate-reading average (WaterLocationResultGridDialog), not EM/AC's
  // CFU x shared-dilution-factor grid.
  const isWaterCountBatch = category === "Water" && current?.workflowType === "CountTest";
  const isBatchOrder = isEmOrAfterCleaning || isWaterCountBatch;
```

- [ ] **Step 2: Replace every `isEmOrAfterCleaning` batch-gating use with `isBatchOrder`**

Three spots in the `awaiting-result` phase's button branch use `isEmOrAfterCleaning` to decide between "open the location grid" and "go to the single-order enter-result phase". Change:

```tsx
            {isTwoStageTransfer && !isStage2 ? (
              <Button variant="contained" disabled={!isTimeReady} onClick={() => setPhase("transfer-stage-2")}>
                Transfer to Stage 2 Incubation
              </Button>
            ) : isTwoStageTransfer && isStage2 ? (
              isEmOrAfterCleaning ? (
                <Button variant="contained" disabled={!isTimeReady} onClick={() => setShowLocationGrid(true)}>Record Results</Button>
              ) : (
                <Button variant="contained" disabled={!isTimeReady} onClick={() => setPhase("enter-result")}>Record Result</Button>
              )
            ) : isEmOrAfterCleaning && step && !step.isFinalStep ? (
              <Button variant="contained" disabled={!isTimeReady} onClick={advanceIncubationWindow}>Advance to Next Incubation Window</Button>
            ) : isEmOrAfterCleaning ? (
              <Button variant="contained" disabled={!isTimeReady} onClick={() => setShowLocationGrid(true)}>Record Results</Button>
            ) : (
              <Button variant="contained" disabled={!isTimeReady} onClick={() => setPhase("enter-result")}>Record Result</Button>
            )}
```

to:

```tsx
            {isTwoStageTransfer && !isStage2 ? (
              <Button variant="contained" disabled={!isTimeReady} onClick={() => setPhase("transfer-stage-2")}>
                Transfer to Stage 2 Incubation
              </Button>
            ) : isTwoStageTransfer && isStage2 ? (
              isBatchOrder ? (
                <Button variant="contained" disabled={!isTimeReady} onClick={() => setShowLocationGrid(true)}>Record Results</Button>
              ) : (
                <Button variant="contained" disabled={!isTimeReady} onClick={() => setPhase("enter-result")}>Record Result</Button>
              )
            ) : isBatchOrder && step && !step.isFinalStep ? (
              <Button variant="contained" disabled={!isTimeReady} onClick={advanceIncubationWindow}>Advance to Next Incubation Window</Button>
            ) : isBatchOrder ? (
              <Button variant="contained" disabled={!isTimeReady} onClick={() => setShowLocationGrid(true)}>Record Results</Button>
            ) : (
              <Button variant="contained" disabled={!isTimeReady} onClick={() => setPhase("enter-result")}>Record Result</Button>
            )}
```

- [ ] **Step 3: Route to the right grid dialog by category, not just workflow type**

Change:

```tsx
      {current.workflowType === "CountTest" ? (
        <LocationResultGridDialog
          open={showLocationGrid}
          testOrderId={testOrderId}
          testCode={testCode}
          displayName={displayName}
          minReadyAt={minReadyAt}
          onClose={() => setShowLocationGrid(false)}
          onSubmitted={() => { setShowLocationGrid(false); load(); }}
        />
      ) : (
        <PathogenLocationResultGridDialog
          open={showLocationGrid}
          testOrderId={testOrderId}
          testCode={testCode}
          displayName={displayName}
          minReadyAt={minReadyAt}
          onClose={() => setShowLocationGrid(false)}
          onSubmitted={() => { setShowLocationGrid(false); load(); }}
        />
      )}
```

to:

```tsx
      {isWaterCountBatch ? (
        <WaterLocationResultGridDialog
          open={showLocationGrid}
          testOrderId={testOrderId}
          testCode={testCode}
          displayName={displayName}
          minReadyAt={minReadyAt}
          onClose={() => setShowLocationGrid(false)}
          onSubmitted={() => { setShowLocationGrid(false); load(); }}
        />
      ) : current.workflowType === "CountTest" ? (
        <LocationResultGridDialog
          open={showLocationGrid}
          testOrderId={testOrderId}
          testCode={testCode}
          displayName={displayName}
          minReadyAt={minReadyAt}
          onClose={() => setShowLocationGrid(false)}
          onSubmitted={() => { setShowLocationGrid(false); load(); }}
        />
      ) : (
        <PathogenLocationResultGridDialog
          open={showLocationGrid}
          testOrderId={testOrderId}
          testCode={testCode}
          displayName={displayName}
          minReadyAt={minReadyAt}
          onClose={() => setShowLocationGrid(false)}
          onSubmitted={() => { setShowLocationGrid(false); load(); }}
        />
      )}
```

- [ ] **Step 4: Type-check**

Run: `cd frontend && npx tsc --noEmit`
Expected: no errors.

- [ ] **Step 5: Build**

Run: `cd frontend && npm run build`
Expected: build succeeds.

- [ ] **Step 6: Commit**

```bash
git add frontend/src/modules/testingWorkspace/TestWorkflowDialog.tsx
git commit -m "feat(water): route water count-test batch orders to WaterLocationResultGridDialog"
```

---

## Task 7: End-to-end verification

**Files:** none (manual verification via the browser preview).

- [ ] **Step 1: Run the full backend test suite**

Run: `dotnet test backend/MicroLIMS.sln`
Expected: all tests pass, including the new `WaterBatchResultTests.cs` and the unchanged `EMBatchLocationTests.cs`.

- [ ] **Step 2: Apply the migration to the local dev database**

Run (from `backend/`):

```bash
dotnet ef database update --project MicroLIMS.Persistence --startup-project MicroLIMS.API
```

Expected: `AddSampleLocationRawReadings` applies with no errors.

- [ ] **Step 3: Frontend build**

Run: `cd frontend && npx tsc --noEmit && npm run build`
Expected: no errors, build succeeds.

- [ ] **Step 4: Manual click-through (requires an authenticated session)**

1. Take a water sample through Phase 1's flow (receive → prepare with 2+ sampling points including TAMC-Water).
2. Open its TAMC-Water TestOrder from the Testing Workspace, select media/incubator, wait for (or fast-forward) the incubation window.
3. Confirm the "Record Results" button opens `WaterLocationResultGridDialog` — one row per sampling point, each with its own "Add Plate" control, no shared dilution field.
4. Enter multiple readings for one point, single reading for another; confirm the live average and status preview update per row.
5. Submit; confirm the TestOrder reaches "Ready" and the sample can proceed to review.
6. Check the Reports module shows the correct sampling-point names for these rows (proves Task 4's `SubjectName` fix reached the report projection, not just the raw table).

- [ ] **Step 5: Report status**

If step 4 cannot be completed (no authenticated session available), report exactly that — steps 1-3 (automated) are the completion bar for this phase; step 4 is manual confirmation for the user.

---

## Self-Review Notes

- **Spec coverage:** Per-location count result entry (Task 3); TestOrder completion/Ready transition (Task 3, reusing `WorkflowStateMachine.TransitionAsync` verbatim); reporting reads from SampleLocations (Task 4). All three items from the original spec's "Phase 2" bullet are covered — via the corrected, shared-engine-based design rather than the spec's original bespoke-engine sketch, per the mid-planning finding confirmed with the user.
- **Type consistency:** `WaterBatchLocationReadings.SampleLocationId`/`Readings` (backend record) match `WaterBatchLocationRequest.SampleLocationId`/`Readings` (controller) match `{ sampleLocationId, readings }` (frontend service call and dialog submit payload) end-to-end. `RecordWaterBatchReadingsAsync`'s signature is referenced identically in the interface (Task 3 Step 3), implementation (Step 4), controller (Step 6), and every test (Task 3/4).
- **Isolation from EM/AC:** confirmed no existing method body is edited beyond additive `??` fallbacks and new `.Include()` calls; `RecordBatchResultsAsync` itself is never touched. Task 2's Step 6 and Task 3/4's "run full suite" steps are the enforcement mechanism — a broken `EMBatchLocationTests.cs` at any point means a fallback bled into EM/AC's own path and must be fixed before continuing.
- **Note for executor:** this repository is not a git repository in the current environment. If `git` commands fail, skip the commit steps and treat each task boundary as a checkpoint instead. Task 3's fixture (`SetupPreparedWaterCountOrderAsync`) and `TestServiceFactory.TestWorkflow(db)` were verified against the real `EMBatchLocationTests.cs` and `TestServiceFactory.cs` during planning — the entity field names, factory signature, and `ResultRecord.SourceTable/SourceId` keying used throughout are copied from working code, not guessed.
