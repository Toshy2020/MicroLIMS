# Water Batch Workflow — Phase 1 (Receive + Prepare) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make Water receiving capture only a Water Department (like EM/After Cleaning), with a new Preparation step where the analyst selects which sampling points are included in the batch — generating one `TestOrder` per distinct test code and one `SampleLocation` row per selected point×test.

**Architecture:** `WaterWorkflowEngine.ReceiveAsync` becomes a shell-only create (no TestOrders), mirroring `EMWorkflowEngine.ReceiveAsync`. A new `PrepareAsync(sampleId, waterSamplingPointIds, userId)` mirrors `EMWorkflowEngine.PrepareAsync`, generating TestOrders/SampleLocations from each point's `AssignedTestCodes`, and linking `SamplingConfiguration` for count tests. The existing single-TestOrder `CalculateAndCompareAsync` continues to serve **legacy** (already-received, per-point) water samples unchanged, but is guarded to reject any TestOrder that went through the new batch `PrepareAsync` — per-location result entry for those ships in Phase 2. Frontend: the receiving grid moves Water into the EM/AC "configured in Preparation" pattern (Department picker instead of Sampling Point), and a new `WaterPreparationForm` (mirroring `EMPreparationForm`) is wired into the existing `PreparationDialog`.

**Tech Stack:** .NET 8 / EF Core 8 (Npgsql, InMemory for tests, xUnit), ASP.NET Core controllers, React + TypeScript + MUI, axios (`apiClient`).

**Spec:** `docs/superpowers/specs/2026-08-17-water-batch-workflow-design.md` (this plan implements only "Phase 1 — Receive + Prepare" from that spec's phasing section)

## Global Constraints

- Mutating endpoints follow the existing pattern: no extra `[Authorize(Roles=...)]` restriction on `water/receive` or `water/prepare` — mirror `EMController`, which has none beyond the class-level `[Authorize]`.
- Every EF relationship gets **explicit fluent configuration** in the matching `*Configuration.cs` file under `MicroLIMS.Persistence/Configurations` — this codebase does not rely on convention-only FK mapping (see `SampleConfiguration.cs`, `SampleLocationConfiguration.cs`). Every new FK here uses `OnDelete(DeleteBehavior.Restrict)`, matching every existing FK on these two entities.
- **Existing water samples are not migrated.** Legacy per-point samples (already `WaterSamplingPointId` + a single TestOrder, no `SampleLocation` rows) must keep calculating exactly as before — this is the backward-compatibility surface this plan must not break.
- **No result-entry UI or per-location calculate endpoint in this phase.** That is Phase 2. This phase must leave the app in a *safe* state: a batch-prepared TestOrder must fail loudly (clear error), never silently mis-compute, if someone tries the old calculate path on it.
- Controller records and DTOs are C# `record`/class shapes exactly as given below — copy them verbatim, field names and casing included, since the frontend types are written to match exactly.

---

## File Structure

**Backend**
- `backend/MicroLIMS.Domain/Entities/Sample.cs` (modify) — add `WaterDepartmentId`/`WaterDepartment`.
- `backend/MicroLIMS.Domain/Entities/SampleLocation.cs` (modify) — add `WaterSamplingPointId`/`WaterSamplingPoint`, `SamplingConfigurationId`/`SamplingConfiguration`.
- `backend/MicroLIMS.Domain/Enums/LocationType.cs` (modify) — add `WaterSamplingPoint`.
- `backend/MicroLIMS.Persistence/Configurations/SampleConfiguration.cs` (modify) — FK config for `WaterDepartment`.
- `backend/MicroLIMS.Persistence/Configurations/SampleLocationConfiguration.cs` (modify) — FK config for `WaterSamplingPoint`/`SamplingConfiguration`.
- `backend/MicroLIMS.Persistence/DbContext/MicroLimsDbContext.cs` (no change — `WaterDepartments` DbSet already exists from the config-parity work).
- `backend/MicroLIMS.Persistence/Migrations/*` (new) — adds the new columns.
- `backend/MicroLIMS.Application/DTOs/SampleDto.cs` (modify) — add `WaterDepartmentId`.
- `backend/MicroLIMS.Application/Services/TestingWorkspaceService.cs` (modify) — map the new field, extend `DisplayName` fallback.
- `backend/MicroLIMS.Application/Workflows/WaterWorkflowEngine.cs` (modify) — `ReceiveAsync` shell, new `PrepareAsync`, guard on `CalculateAndCompareAsync`.
- `backend/MicroLIMS.Application/Services/WaterService.cs` (modify) — `PrepareAsync` passthrough.
- `backend/MicroLIMS.API/Controllers/WaterController.cs` (modify) — request record change, new `prepare` endpoint.
- `backend/MicroLIMS.Tests/WorkflowTests/WaterAndEMEngineTests.cs` (modify) — rewrite 4 existing tests, add `PrepareAsync` and guard tests.

**Frontend**
- `frontend/src/modules/receiving/types/receivingTypes.ts` (modify) — `WaterReceiveRequest`, `SampleRecord.waterDepartmentId`.
- `frontend/src/services/masterDataOptions.ts` (modify) — `getWaterDepartments`.
- `frontend/src/modules/receiving/dialogs/NewSampleDialog.tsx` (modify) — load water departments, validate/submit by department.
- `frontend/src/modules/receiving/dialogs/MultiSampleEntryGrid.tsx` (modify) — Water joins the EM/AC "Preparation" banner + Department column.
- `frontend/src/modules/receiving/ReceiveSamplePage.tsx` (modify) — pass `waterDepartmentId` into `PreparationDialog`.
- `frontend/src/modules/testingWorkspace/types/workspaceTypes.ts` (modify) — `SampleCard.waterDepartmentId`.
- `frontend/src/modules/testPreparation/PreparationDialog.tsx` (modify) — Water branch.
- `frontend/src/modules/laboratoryConfiguration/water/services/WaterPreparationService.ts` (new).
- `frontend/src/modules/laboratoryConfiguration/water/WaterPreparationForm.tsx` (new, mirrors `EMPreparationForm.tsx`).

---

## Task 1: Domain model, EF configuration, and migration

**Files:**
- Modify: `backend/MicroLIMS.Domain/Entities/Sample.cs`
- Modify: `backend/MicroLIMS.Domain/Entities/SampleLocation.cs`
- Modify: `backend/MicroLIMS.Domain/Enums/LocationType.cs`
- Modify: `backend/MicroLIMS.Persistence/Configurations/SampleConfiguration.cs`
- Modify: `backend/MicroLIMS.Persistence/Configurations/SampleLocationConfiguration.cs`
- Create: migration under `backend/MicroLIMS.Persistence/Migrations/`
- Test: `backend/MicroLIMS.Tests/WorkflowTests/WaterBatchPrepareTests.cs` (new file)

**Interfaces:**
- Produces: `Sample.WaterDepartmentId : int?`, `Sample.WaterDepartment : WaterDepartment?`
- Produces: `SampleLocation.WaterSamplingPointId : int?`, `SampleLocation.WaterSamplingPoint : WaterSamplingPoint?`, `SampleLocation.SamplingConfigurationId : int?`, `SampleLocation.SamplingConfiguration : SamplingConfiguration?`
- Produces: `LocationType.WaterSamplingPoint` enum member

- [ ] **Step 1: Write the failing test**

Create `backend/MicroLIMS.Tests/WorkflowTests/WaterBatchPrepareTests.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.WorkflowTests;

public class WaterBatchPrepareTests
{
    private static MicroLimsDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MicroLimsDbContext(options);
    }

    [Fact]
    public async Task SampleLocation_CanReferenceWaterSamplingPointAndSamplingConfiguration()
    {
        await using var db = NewDb();
        var department = new WaterDepartment { Name = "WTU" };
        db.WaterDepartments.Add(department);
        await db.SaveChangesAsync();

        var point = new WaterSamplingPoint { Code = "SP-1", WaterDepartmentId = department.Id, AssignedTestCodes = new() { "TAMC" } };
        db.WaterSamplingPoints.Add(point);
        var config = new SamplingConfiguration { WaterSamplingPointId = point.Id, TestCode = "TAMC", AlertLimit = "10", ActionLimit = "50", SpecLimit = "100" };
        db.SamplingConfigurations.Add(config);
        await db.SaveChangesAsync();

        var sample = new Sample
        {
            ReferenceNumber = "WT0817999", Category = SampleCategory.Water, WaterDepartmentId = department.Id,
            ControlNumber = "CTRL-99", SampledBy = "Analyst"
        };
        var order = new TestOrder { TestCode = "TAMC", Status = ApprovalStatus.Pending, CurrentStep = WorkflowStep.Waiting };
        sample.TestOrders.Add(order);
        sample.Locations.Add(new SampleLocation
        {
            TestOrder = order, LocationType = LocationType.WaterSamplingPoint,
            WaterSamplingPointId = point.Id, SamplingConfigurationId = config.Id
        });
        db.Samples.Add(sample);
        await db.SaveChangesAsync();

        var loaded = await db.SampleLocations.SingleAsync();
        Assert.Equal(LocationType.WaterSamplingPoint, loaded.LocationType);
        Assert.Equal(point.Id, loaded.WaterSamplingPointId);
        Assert.Equal(config.Id, loaded.SamplingConfigurationId);

        var loadedSample = await db.Samples.SingleAsync();
        Assert.Equal(department.Id, loadedSample.WaterDepartmentId);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test backend/MicroLIMS.Tests --filter WaterBatchPrepareTests`
Expected: FAIL to compile — `Sample.WaterDepartmentId`, `SampleLocation.WaterSamplingPointId`, `SampleLocation.SamplingConfigurationId`, and `LocationType.WaterSamplingPoint` do not exist yet.

- [ ] **Step 3: Add the LocationType member**

In `backend/MicroLIMS.Domain/Enums/LocationType.cs`:

```csharp
namespace MicroLIMS.Domain.Enums;

public enum LocationType
{
    Room,
    MachinePart,
    WaterSamplingPoint
}
```

- [ ] **Step 4: Add the Sample field**

In `backend/MicroLIMS.Domain/Entities/Sample.cs`, add after the existing `MachineId`/`Machine` pair (after line 22):

```csharp
    public int? WaterDepartmentId { get; set; }    // Water only (batch model)
    public WaterDepartment? WaterDepartment { get; set; }
```

- [ ] **Step 5: Add the SampleLocation fields**

In `backend/MicroLIMS.Domain/Entities/SampleLocation.cs`, add after the existing `MachinePartConfigurationId`/`MachinePartConfiguration` pair:

```csharp
    public int? WaterSamplingPointId { get; set; }
    public WaterSamplingPoint? WaterSamplingPoint { get; set; }
    public int? SamplingConfigurationId { get; set; }   // count-test limits source; null for pathogen locations
    public SamplingConfiguration? SamplingConfiguration { get; set; }
```

- [ ] **Step 6: Add the EF fluent configuration**

In `backend/MicroLIMS.Persistence/Configurations/SampleConfiguration.cs`, add after the existing `Machine` line:

```csharp
        builder.HasOne(s => s.WaterDepartment).WithMany().HasForeignKey(s => s.WaterDepartmentId).OnDelete(DeleteBehavior.Restrict);
```

In `backend/MicroLIMS.Persistence/Configurations/SampleLocationConfiguration.cs`, add after the existing `MachinePartConfiguration` line:

```csharp
        builder.HasOne(l => l.WaterSamplingPoint).WithMany().HasForeignKey(l => l.WaterSamplingPointId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(l => l.SamplingConfiguration).WithMany().HasForeignKey(l => l.SamplingConfigurationId).OnDelete(DeleteBehavior.Restrict);
```

And add a matching unique index after the existing `MachinePartConfigurationId` index (one result row per location per test type, same reasoning as the other two):

```csharp
        builder.HasIndex(l => new { l.TestOrderId, l.WaterSamplingPointId })
            .IsUnique()
            .HasFilter("\"WaterSamplingPointId\" IS NOT NULL");
```

- [ ] **Step 7: Run test to verify it passes**

Run: `dotnet test backend/MicroLIMS.Tests --filter WaterBatchPrepareTests`
Expected: PASS.

- [ ] **Step 8: Create the EF migration**

Run (from `backend/`):

```bash
dotnet ef migrations add AddWaterBatchModel --project MicroLIMS.Persistence --startup-project MicroLIMS.API
```

Open the generated migration and confirm it only adds columns/indexes/FKs (no data seed needed — all new columns are nullable and no backfill applies, per the "existing water samples are not migrated" constraint). If `dotnet ef` produces anything beyond `AddColumn`, `CreateIndex`, and `AddForeignKey` statements for these fields, stop and re-check Step 6 before proceeding.

- [ ] **Step 9: Build the full solution**

Run: `dotnet build backend/MicroLIMS.sln`
Expected: 0 errors.

- [ ] **Step 10: Commit**

```bash
git add backend/MicroLIMS.Domain backend/MicroLIMS.Persistence backend/MicroLIMS.Tests/WorkflowTests/WaterBatchPrepareTests.cs
git commit -m "feat(water): add batch model fields to Sample and SampleLocation"
```

---

## Task 2: `ReceiveAsync` becomes a department-only shell

**Files:**
- Modify: `backend/MicroLIMS.Application/Workflows/WaterWorkflowEngine.cs`
- Modify: `backend/MicroLIMS.Application/Services/WaterService.cs` (no signature change, just confirms compile)
- Modify: `backend/MicroLIMS.API/Controllers/WaterController.cs`
- Modify: `backend/MicroLIMS.Tests/WorkflowTests/WaterAndEMEngineTests.cs`

**Interfaces:**
- Produces: `WaterReceiveRequest(int WaterDepartmentId, int CauseOfTestingId, string SampleQuantity, string SampledBy, string ControlNumber, int ReceivedByUserId)` — **replaces** the old `WaterSamplingPointId`-keyed record.
- Consumes: `WaterDepartment` (Task 1's pre-existing `WaterDepartments` DbSet from the earlier config-parity work).

- [ ] **Step 1: Update the existing receive test to the new shape**

In `backend/MicroLIMS.Tests/WorkflowTests/WaterAndEMEngineTests.cs`, replace the `Water_ReceiveAsync_StartsAsNeedsPreparation` test:

```csharp
    [Fact]
    public async Task Water_ReceiveAsync_StartsAsNeedsPreparation()
    {
        await using var db = NewDb();
        var department = new WaterDepartment { Name = "WTU" };
        db.WaterDepartments.Add(department);
        await db.SaveChangesAsync();

        var engine = new WaterWorkflowEngine(db, new ReferenceNumberGenerator(db));
        var sample = await engine.ReceiveAsync(new WaterReceiveRequest(department.Id, 0, "500ml", "Analyst", "CTRL-2", 1));

        Assert.Equal(SamplePreparationStatus.NeedsPreparation, sample.PreparationStatus);
        Assert.Empty(sample.TestOrders);
    }
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test backend/MicroLIMS.Tests --filter Water_ReceiveAsync_StartsAsNeedsPreparation`
Expected: FAIL to compile — `WaterReceiveRequest`'s first parameter is still `WaterSamplingPointId`-typed for a point, and `WaterDepartment`/`WaterDepartments` aren't referenced by this constructor yet (compile error on the positional record args), or the assertion fails once it compiles (sample still gets TestOrders from `point.AssignedTestCodes`).

- [ ] **Step 3: Rewrite `ReceiveAsync` and the request record**

In `backend/MicroLIMS.Application/Workflows/WaterWorkflowEngine.cs`, replace the `WaterReceiveRequest` record (lines 10-12) and the `ReceiveAsync` method (lines 37-65):

```csharp
public record WaterReceiveRequest(
    int WaterDepartmentId, int CauseOfTestingId, string SampleQuantity, string SampledBy,
    string ControlNumber, int ReceivedByUserId);
```

```csharp
    public async Task<Sample> ReceiveAsync(WaterReceiveRequest request)
    {
        var department = await _db.WaterDepartments.FirstOrDefaultAsync(d => d.Id == request.WaterDepartmentId)
            ?? throw new InvalidOperationException($"Water department {request.WaterDepartmentId} not found.");

        var sample = new Sample
        {
            ReferenceNumber = await _refNumbers.GenerateAsync(SampleCategory.Water),
            Category = SampleCategory.Water,
            WaterDepartmentId = department.Id,
            CauseOfTestingId = request.CauseOfTestingId,
            SampleQuantity = request.SampleQuantity,
            SampledBy = request.SampledBy,
            ControlNumber = request.ControlNumber,
            ReceivedByUserId = request.ReceivedByUserId,
            Status = SampleStatus.Received,
            PreparationStatus = SamplePreparationStatus.NeedsPreparation
        };

        _db.Samples.Add(sample);
        await _db.SaveChangesAsync();
        return sample;
    }
```

- [ ] **Step 4: Update the controller's request record and mapping**

In `backend/MicroLIMS.API/Controllers/WaterController.cs`, replace line 9:

```csharp
public record ReceiveWaterRequest(int WaterDepartmentId, int CauseOfTestingId, string SampleQuantity, string SampledBy, string ControlNumber);
```

And update the `Receive` action body (line 28) to pass `request.WaterDepartmentId` instead of `request.WaterSamplingPointId`:

```csharp
    [HttpPost("receive")]
    public async Task<IActionResult> Receive(ReceiveWaterRequest request) =>
        Ok(ApiResponse<object>.Ok(await _waterService.ReceiveAsync(new WaterReceiveRequest(
            request.WaterDepartmentId, request.CauseOfTestingId, request.SampleQuantity, request.SampledBy, request.ControlNumber, CurrentUserId))));
```

- [ ] **Step 5: Run the test to verify it passes**

Run: `dotnet test backend/MicroLIMS.Tests --filter Water_ReceiveAsync_StartsAsNeedsPreparation`
Expected: PASS.

- [ ] **Step 6: Build the full solution**

Run: `dotnet build backend/MicroLIMS.sln`
Expected: compile errors will surface in the 3 other Water tests that still construct the old `WaterReceiveRequest(point.Id, ...)` shape — this is expected; Task 4 fixes them. Confirm the *only* errors are in `WaterAndEMEngineTests.cs`'s other three water tests (`Water_AverageExceedsSpecLimit_FlagsOutOfSpecification`, `Water_ConfiguredLimits_ProduceExpectedStatus`, `Water_NoConfiguredLimits_StaysWithinLimits`) before moving on.

- [ ] **Step 7: Commit**

```bash
git add backend/MicroLIMS.Application/Workflows/WaterWorkflowEngine.cs backend/MicroLIMS.API/Controllers/WaterController.cs backend/MicroLIMS.Tests/WorkflowTests/WaterAndEMEngineTests.cs
git commit -m "feat(water): ReceiveAsync creates a department-only shell, no eager TestOrders"
```

---

## Task 3: `PrepareAsync` — the batch-generation step

**Files:**
- Modify: `backend/MicroLIMS.Application/Workflows/WaterWorkflowEngine.cs`
- Modify: `backend/MicroLIMS.Application/Services/WaterService.cs`
- Modify: `backend/MicroLIMS.API/Controllers/WaterController.cs`
- Modify: `backend/MicroLIMS.Tests/WorkflowTests/WaterBatchPrepareTests.cs`

**Interfaces:**
- Consumes: `Sample.WaterDepartmentId`, `SampleLocation.WaterSamplingPointId/SamplingConfigurationId`, `LocationType.WaterSamplingPoint` (Task 1); `WaterReceiveRequest`/`ReceiveAsync` (Task 2).
- Produces: `IWaterWorkflowEngine.PrepareAsync(int sampleId, List<int> waterSamplingPointIds, int userId) : Task<Sample>`; `WaterService.PrepareAsync(int sampleId, List<int> waterSamplingPointIds, int userId) : Task<SampleDto>`; `POST /api/water/prepare`.

- [ ] **Step 1: Write the failing tests**

Add to `backend/MicroLIMS.Tests/WorkflowTests/WaterBatchPrepareTests.cs`:

```csharp
    [Fact]
    public async Task PrepareAsync_CreatesOneTestOrderPerDistinctCodeAndOneLocationPerPointTest()
    {
        await using var db = NewDb();
        var department = new WaterDepartment { Name = "WTU" };
        db.WaterDepartments.Add(department);
        await db.SaveChangesAsync();

        var pointA = new WaterSamplingPoint { Code = "SP-A", Location = "A", WaterDepartmentId = department.Id, AssignedTestCodes = new() { "TAMC", "Salmonella" } };
        var pointB = new WaterSamplingPoint { Code = "SP-B", Location = "B", WaterDepartmentId = department.Id, AssignedTestCodes = new() { "TAMC" } };
        db.WaterSamplingPoints.AddRange(pointA, pointB);
        db.TestDefinitions.Add(new TestDefinition { Code = "TAMC", DisplayName = "TAMC", WorkflowType = WorkflowType.CountTest });
        db.TestDefinitions.Add(new TestDefinition { Code = "Salmonella", DisplayName = "Salmonella", WorkflowType = WorkflowType.Observation });
        db.SamplingConfigurations.Add(new SamplingConfiguration { WaterSamplingPointId = pointA.Id, TestCode = "TAMC", AlertLimit = "10", ActionLimit = "50", SpecLimit = "100" });
        await db.SaveChangesAsync();

        var engine = new MicroLIMS.Application.Workflows.WaterWorkflowEngine(db, new MicroLIMS.Application.Services.ReferenceNumberGenerator(db));
        var sample = await engine.ReceiveAsync(new MicroLIMS.Application.Workflows.WaterReceiveRequest(department.Id, 0, "500ml", "Analyst", "CTRL-3", 1));

        var prepared = await engine.PrepareAsync(sample.Id, new List<int> { pointA.Id, pointB.Id }, 1);

        Assert.Equal(2, prepared.TestOrders.Count); // TAMC, Salmonella
        Assert.Equal(3, prepared.Locations.Count);  // TAMC@A, Salmonella@A, TAMC@B
        Assert.Equal(SamplePreparationStatus.Ready, prepared.PreparationStatus);

        var tamcOrder = prepared.TestOrders.Single(o => o.TestCode == "TAMC");
        var tamcAtPointA = prepared.Locations.Single(l => l.TestOrderId == tamcOrder.Id && l.WaterSamplingPointId == pointA.Id);
        Assert.NotNull(tamcAtPointA.SamplingConfigurationId);

        var salmonellaOrder = prepared.TestOrders.Single(o => o.TestCode == "Salmonella");
        var salmonellaLocation = prepared.Locations.Single(l => l.TestOrderId == salmonellaOrder.Id);
        Assert.Null(salmonellaLocation.SamplingConfigurationId);
    }

    [Fact]
    public async Task PrepareAsync_RejectsPointFromWrongDepartment()
    {
        await using var db = NewDb();
        var deptA = new WaterDepartment { Name = "A" };
        var deptB = new WaterDepartment { Name = "B" };
        db.WaterDepartments.AddRange(deptA, deptB);
        await db.SaveChangesAsync();
        var pointInB = new WaterSamplingPoint { Code = "SP-B", WaterDepartmentId = deptB.Id, AssignedTestCodes = new() { "TAMC" } };
        db.WaterSamplingPoints.Add(pointInB);
        await db.SaveChangesAsync();

        var engine = new MicroLIMS.Application.Workflows.WaterWorkflowEngine(db, new MicroLIMS.Application.Services.ReferenceNumberGenerator(db));
        var sample = await engine.ReceiveAsync(new MicroLIMS.Application.Workflows.WaterReceiveRequest(deptA.Id, 0, "500ml", "Analyst", "CTRL-4", 1));

        await Assert.ThrowsAsync<InvalidOperationException>(() => engine.PrepareAsync(sample.Id, new List<int> { pointInB.Id }, 1));
    }

    [Fact]
    public async Task PrepareAsync_RejectsEmptySelection()
    {
        await using var db = NewDb();
        var department = new WaterDepartment { Name = "WTU" };
        db.WaterDepartments.Add(department);
        await db.SaveChangesAsync();

        var engine = new MicroLIMS.Application.Workflows.WaterWorkflowEngine(db, new MicroLIMS.Application.Services.ReferenceNumberGenerator(db));
        var sample = await engine.ReceiveAsync(new MicroLIMS.Application.Workflows.WaterReceiveRequest(department.Id, 0, "500ml", "Analyst", "CTRL-5", 1));

        await Assert.ThrowsAsync<InvalidOperationException>(() => engine.PrepareAsync(sample.Id, new List<int>(), 1));
    }
```

Add `using MicroLIMS.Application.Services;` and `using System.Linq;` to the top of the file if not already implied by `Xunit`/`Microsoft.EntityFrameworkCore` (LINQ `Single`/`SelectMany` need `System.Linq`, which is implicit via `ImplicitUsings` — confirm by checking `MicroLIMS.Tests.csproj` has `<ImplicitUsings>enable</ImplicitUsings>`; it does, so no extra using statements are required beyond what's shown).

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test backend/MicroLIMS.Tests --filter WaterBatchPrepareTests`
Expected: FAIL to compile — `IWaterWorkflowEngine`/`WaterWorkflowEngine` has no `PrepareAsync` member.

- [ ] **Step 3: Add `PrepareAsync` to the interface and implementation**

In `backend/MicroLIMS.Application/Workflows/WaterWorkflowEngine.cs`, update the interface (lines 14-24) to add the new member:

```csharp
public interface IWaterWorkflowEngine : IStatefulWorkflowEngine
{
    Task<Sample> ReceiveAsync(WaterReceiveRequest request);

    // The checklist screen: selecting which sampling points are included
    // in this batch generates the TestOrders (one per distinct TestCode
    // across every selected point) and the SampleLocation rows (one per
    // selected point x assigned test code).
    Task<Sample> PrepareAsync(int sampleId, List<int> waterSamplingPointIds, int userId);

    // Calculation engine: averages the entered raw readings and compares
    // against Alert -> Action -> Specification limits, in that order of
    // severity (gap analysis #5). Legacy per-point samples only - see the
    // guard at the top of the method.
    Task<WaterComparisonResult> CalculateAndCompareAsync(int testOrderId, List<decimal> readings);

    Task<List<WaterComparisonResult>> GetDailyAggregateAsync(DateTime date);
}
```

Add the implementation directly after `ReceiveAsync` (before `CalculateAndCompareAsync`):

```csharp
    public async Task<Sample> PrepareAsync(int sampleId, List<int> waterSamplingPointIds, int userId)
    {
        var sample = await _db.Samples.Include(s => s.TestOrders).Include(s => s.Locations).FirstOrDefaultAsync(s => s.Id == sampleId)
            ?? throw new InvalidOperationException($"Sample {sampleId} not found.");

        if (sample.PreparationStatus != SamplePreparationStatus.NeedsPreparation)
            throw new InvalidOperationException("This sample has already been prepared.");

        if (waterSamplingPointIds.Count == 0)
            throw new InvalidOperationException("At least one sampling point must be selected.");

        var points = await _db.WaterSamplingPoints
            .Where(p => waterSamplingPointIds.Contains(p.Id))
            .ToListAsync();

        var missing = waterSamplingPointIds.Except(points.Select(p => p.Id)).ToList();
        if (missing.Count > 0)
            throw new InvalidOperationException($"Sampling point(s) not found: {string.Join(", ", missing)}.");

        var wrongDepartment = points.Where(p => p.WaterDepartmentId != sample.WaterDepartmentId).ToList();
        if (wrongDepartment.Count > 0)
            throw new InvalidOperationException(
                $"Sampling point(s) {string.Join(", ", wrongDepartment.Select(p => p.Code))} do not belong to this sample's department.");

        var allCodes = points.SelectMany(p => p.AssignedTestCodes).Distinct().ToList();
        var countTestCodeSet = (await _db.TestDefinitions
            .Where(t => allCodes.Contains(t.Code) && t.WorkflowType == WorkflowType.CountTest)
            .Select(t => t.Code)
            .ToListAsync())
            .ToHashSet();

        var configs = await _db.SamplingConfigurations
            .Where(c => waterSamplingPointIds.Contains(c.WaterSamplingPointId))
            .ToListAsync();

        // One TestOrder per distinct TestCode across every selected point -
        // the whole batch shares a single workflow per test type, same as
        // EMWorkflowEngine.PrepareAsync.
        var testOrdersByCode = new Dictionary<string, TestOrder>();
        foreach (var point in points)
        {
            foreach (var testCode in point.AssignedTestCodes)
            {
                if (!testOrdersByCode.TryGetValue(testCode, out var order))
                {
                    order = new TestOrder
                    {
                        TestCode = testCode,
                        Status = ApprovalStatus.Pending,
                        CurrentStep = WorkflowStep.Waiting
                    };
                    sample.TestOrders.Add(order);
                    testOrdersByCode[testCode] = order;
                }

                var location = new SampleLocation
                {
                    TestOrder = order,
                    LocationType = LocationType.WaterSamplingPoint,
                    WaterSamplingPointId = point.Id
                };

                if (countTestCodeSet.Contains(testCode))
                {
                    var config = configs.FirstOrDefault(c => c.WaterSamplingPointId == point.Id && c.TestCode == testCode);
                    if (config != null)
                        location.SamplingConfigurationId = config.Id;
                }

                sample.Locations.Add(location);
            }
        }

        sample.PreparationStatus = SamplePreparationStatus.Ready;
        await _db.SaveChangesAsync();
        return sample;
    }
```

- [ ] **Step 4: Wire `WaterService.PrepareAsync`**

In `backend/MicroLIMS.Application/Services/WaterService.cs`, add after `ReceiveAsync`:

```csharp
    public async Task<SampleDto> PrepareAsync(int sampleId, List<int> waterSamplingPointIds, int userId)
    {
        var sample = await _workflow.PrepareAsync(sampleId, waterSamplingPointIds, userId);
        return TestingWorkspaceService.ToDto(sample);
    }
```

- [ ] **Step 5: Add the controller endpoint**

In `backend/MicroLIMS.API/Controllers/WaterController.cs`, add the request record after `ReceiveWaterRequest`:

```csharp
public record PrepareWaterRequest(int SampleId, List<int> WaterSamplingPointIds);
```

Add the action after `Receive`:

```csharp
    // The checklist screen - selecting which sampling points are included
    // in this batch generates the TestOrders + SampleLocations.
    [HttpPost("prepare")]
    public async Task<IActionResult> Prepare(PrepareWaterRequest request) =>
        Ok(ApiResponse<object>.Ok(await _waterService.PrepareAsync(request.SampleId, request.WaterSamplingPointIds, CurrentUserId)));
```

- [ ] **Step 6: Run tests to verify they pass**

Run: `dotnet test backend/MicroLIMS.Tests --filter WaterBatchPrepareTests`
Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add backend/MicroLIMS.Application/Workflows/WaterWorkflowEngine.cs backend/MicroLIMS.Application/Services/WaterService.cs backend/MicroLIMS.API/Controllers/WaterController.cs backend/MicroLIMS.Tests/WorkflowTests/WaterBatchPrepareTests.cs
git commit -m "feat(water): add PrepareAsync batch-generation step and /water/prepare endpoint"
```

---

## Task 4: Guard the legacy calculate path, preserve backward compatibility

**Files:**
- Modify: `backend/MicroLIMS.Application/Workflows/WaterWorkflowEngine.cs`
- Modify: `backend/MicroLIMS.Tests/WorkflowTests/WaterAndEMEngineTests.cs`

**Interfaces:**
- Consumes: `SampleLocation` rows created by `PrepareAsync` (Task 3) as the discriminator between legacy and batch TestOrders.

This task rewrites the three Water tests still using the old `engine.ReceiveAsync(new WaterReceiveRequest(point.Id, ...))` shape — that constructor no longer exists (Task 2 changed its first parameter to a department id). Those three tests exist to prove `CalculateAndCompareAsync`'s Alert/Action/Spec logic works for a **legacy, per-point** sample — a shape that can no longer be created through the engine's public API (only `ReceiveAsync` creates new samples, and it now always produces the batch shell). So they're rewritten to construct the legacy sample shape directly via EF, exactly like `ResultProjectionTests.cs` already does for its own legacy-shape water test. A new test proves the guard rejects a batch-prepared TestOrder.

- [ ] **Step 1: Write the failing tests — rewrite the three legacy-shape tests and add the guard test**

In `backend/MicroLIMS.Tests/WorkflowTests/WaterAndEMEngineTests.cs`, replace `Water_AverageExceedsSpecLimit_FlagsOutOfSpecification`:

```csharp
    [Fact]
    public async Task Water_AverageExceedsSpecLimit_FlagsOutOfSpecification()
    {
        await using var db = NewDb();
        var point = new WaterSamplingPoint { Code = "WP-01", Location = "Utility Room", AssignedTestCodes = new() { "TAMC" } };
        db.WaterSamplingPoints.Add(point);
        db.SamplingConfigurations.Add(new SamplingConfiguration
        {
            WaterSamplingPointId = point.Id, TestCode = "TAMC", AlertLimit = "10", ActionLimit = "50", SpecLimit = "100"
        });
        await db.SaveChangesAsync();

        // Legacy per-point sample shape - created directly, the way
        // pre-batch water samples exist in the database today (no
        // SampleLocation rows, WaterSamplingPointId set directly).
        var sample = new Sample
        {
            ReferenceNumber = "WT0817001", Category = SampleCategory.Water, WaterSamplingPointId = point.Id,
            ControlNumber = "CTRL-1", SampledBy = "Analyst", Status = SampleStatus.Received,
            PreparationStatus = SamplePreparationStatus.Ready
        };
        sample.TestOrders.Add(new TestOrder { TestCode = "TAMC", Status = ApprovalStatus.Pending, CurrentStep = WorkflowStep.Waiting });
        db.Samples.Add(sample);
        await db.SaveChangesAsync();
        var order = sample.TestOrders.First();

        var engine = new WaterWorkflowEngine(db, new ReferenceNumberGenerator(db));
        var result = await engine.CalculateAndCompareAsync(order.Id, new List<decimal> { 90, 110, 120 });

        Assert.Equal("OutOfSpecification", result.Status);
    }
```

Replace `Water_ConfiguredLimits_ProduceExpectedStatus`:

```csharp
    [Theory]
    [InlineData(new double[] { 12, 14 }, "AlertLimitExceeded")]   // avg 13 > alert 10, < action 50
    [InlineData(new double[] { 60, 60 }, "ActionLimitExceeded")]  // avg 60 > action 50, < spec 100
    public async Task Water_ConfiguredLimits_ProduceExpectedStatus(double[] readings, string expected)
    {
        await using var db = NewDb();
        var point = new WaterSamplingPoint { Code = "WP-10", Location = "WTU", AssignedTestCodes = new() { "TAMC" } };
        db.WaterSamplingPoints.Add(point);
        db.SamplingConfigurations.Add(new SamplingConfiguration
        {
            WaterSamplingPointId = point.Id, TestCode = "TAMC", AlertLimit = "10", ActionLimit = "50", SpecLimit = "100"
        });
        await db.SaveChangesAsync();

        var sample = new Sample
        {
            ReferenceNumber = "WT0817010", Category = SampleCategory.Water, WaterSamplingPointId = point.Id,
            ControlNumber = "CTRL-10", SampledBy = "Analyst", Status = SampleStatus.Received,
            PreparationStatus = SamplePreparationStatus.Ready
        };
        sample.TestOrders.Add(new TestOrder { TestCode = "TAMC", Status = ApprovalStatus.Pending, CurrentStep = WorkflowStep.Waiting });
        db.Samples.Add(sample);
        await db.SaveChangesAsync();
        var order = sample.TestOrders.First();

        var engine = new WaterWorkflowEngine(db, new ReferenceNumberGenerator(db));
        var result = await engine.CalculateAndCompareAsync(order.Id, readings.Select(r => (decimal)r).ToList());

        Assert.Equal(expected, result.Status);
    }
```

Replace `Water_NoConfiguredLimits_StaysWithinLimits`:

```csharp
    [Fact]
    public async Task Water_NoConfiguredLimits_StaysWithinLimits()
    {
        await using var db = NewDb();
        var point = new WaterSamplingPoint { Code = "WP-11", Location = "WTU", AssignedTestCodes = new() { "TAMC" } };
        db.WaterSamplingPoints.Add(point);
        await db.SaveChangesAsync();

        var sample = new Sample
        {
            ReferenceNumber = "WT0817011", Category = SampleCategory.Water, WaterSamplingPointId = point.Id,
            ControlNumber = "CTRL-11", SampledBy = "Analyst", Status = SampleStatus.Received,
            PreparationStatus = SamplePreparationStatus.Ready
        };
        sample.TestOrders.Add(new TestOrder { TestCode = "TAMC", Status = ApprovalStatus.Pending, CurrentStep = WorkflowStep.Waiting });
        db.Samples.Add(sample);
        await db.SaveChangesAsync();
        var order = sample.TestOrders.First();

        var engine = new WaterWorkflowEngine(db, new ReferenceNumberGenerator(db));
        var result = await engine.CalculateAndCompareAsync(order.Id, new List<decimal> { 9999 });

        Assert.Equal("WithinLimits", result.Status);
    }
```

Add a new guard test right after `Water_NoConfiguredLimits_StaysWithinLimits`:

```csharp
    [Fact]
    public async Task Water_CalculateAndCompareAsync_RejectsBatchPreparedTestOrder()
    {
        await using var db = NewDb();
        var department = new WaterDepartment { Name = "WTU" };
        db.WaterDepartments.Add(department);
        await db.SaveChangesAsync();
        var point = new WaterSamplingPoint { Code = "SP-1", WaterDepartmentId = department.Id, AssignedTestCodes = new() { "TAMC" } };
        db.WaterSamplingPoints.Add(point);
        await db.SaveChangesAsync();

        var engine = new WaterWorkflowEngine(db, new ReferenceNumberGenerator(db));
        var sample = await engine.ReceiveAsync(new WaterReceiveRequest(department.Id, 0, "500ml", "Analyst", "CTRL-20", 1));
        var prepared = await engine.PrepareAsync(sample.Id, new List<int> { point.Id }, 1);
        var order = prepared.TestOrders.Single();

        await Assert.ThrowsAsync<InvalidOperationException>(() => engine.CalculateAndCompareAsync(order.Id, new List<decimal> { 5 }));
    }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test backend/MicroLIMS.Tests --filter WaterAndEMEngineTests`
Expected: the three rewritten tests PASS immediately (they exercise only pre-existing, unmodified `CalculateAndCompareAsync` logic against a legacy shape). `Water_CalculateAndCompareAsync_RejectsBatchPreparedTestOrder` FAILS — no exception thrown yet, since the guard doesn't exist.

- [ ] **Step 3: Add the guard**

In `backend/MicroLIMS.Application/Workflows/WaterWorkflowEngine.cs`, in `CalculateAndCompareAsync`, insert the guard immediately after loading `order` and before loading `sample`:

```csharp
    public async Task<WaterComparisonResult> CalculateAndCompareAsync(int testOrderId, List<decimal> readings)
    {
        if (readings.Count == 0)
            throw new InvalidOperationException("At least one reading is required to calculate an average.");

        var order = await WorkflowStateMachine.LoadOrThrowAsync(_db, testOrderId);

        // A TestOrder that went through the batch PrepareAsync always has
        // SampleLocation rows (one per selected sampling point). Per-
        // location result entry for those ships separately - this legacy
        // single-average path must never silently misattribute a batch
        // order's result to "the" sampling point.
        var isBatchPrepared = await _db.SampleLocations.AnyAsync(l => l.TestOrderId == testOrderId);
        if (isBatchPrepared)
            throw new InvalidOperationException(
                "This water test was prepared across multiple sampling points; per-location result entry is not available yet.");

        var sample = await _db.Samples.Include(s => s.TestOrders).FirstOrDefaultAsync(s => s.Id == order.SampleId)
            ?? throw new InvalidOperationException("Sample not found for this test order.");
```

(The rest of the method body is unchanged.)

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test backend/MicroLIMS.Tests --filter WaterAndEMEngineTests`
Expected: PASS, all Water tests including the new guard test.

- [ ] **Step 5: Run the full backend suite**

Run: `dotnet test backend/MicroLIMS.sln`
Expected: PASS — this also re-confirms `ResultProjectionTests.cs`'s own legacy-shape water test (which never goes through `SampleLocation` rows) still passes unaffected by the guard.

- [ ] **Step 6: Commit**

```bash
git add backend/MicroLIMS.Application/Workflows/WaterWorkflowEngine.cs backend/MicroLIMS.Tests/WorkflowTests/WaterAndEMEngineTests.cs
git commit -m "fix(water): guard legacy calculate against batch-prepared TestOrders"
```

---

## Task 5: DTO/service plumbing for `WaterDepartmentId`

**Files:**
- Modify: `backend/MicroLIMS.Application/DTOs/SampleDto.cs`
- Modify: `backend/MicroLIMS.Application/Services/TestingWorkspaceService.cs`

**Interfaces:**
- Consumes: `Sample.WaterDepartmentId`/`WaterDepartment` (Task 1).
- Produces: `SampleDto.WaterDepartmentId : int?` — what the frontend's `PreparationDialog` branch (Task 7) keys on, exactly like `DepartmentId` does for EM.

- [ ] **Step 1: Add the DTO field**

In `backend/MicroLIMS.Application/DTOs/SampleDto.cs`, add after `MachineId`:

```csharp
    public int? WaterDepartmentId { get; set; }
```

- [ ] **Step 2: Map it in `ToDto`**

In `backend/MicroLIMS.Application/Services/TestingWorkspaceService.cs`, update the `DisplayName` fallback chain and add the new field. Change:

```csharp
            DisplayName = s.Item?.Name ?? s.WaterSamplingPoint?.Code ?? s.Department?.Name ?? s.Machine?.Name ?? string.Empty,
            DepartmentId = s.DepartmentId,
            MachineId = s.MachineId,
```

to:

```csharp
            DisplayName = s.Item?.Name ?? s.WaterSamplingPoint?.Code ?? s.WaterDepartment?.Name ?? s.Department?.Name ?? s.Machine?.Name ?? string.Empty,
            DepartmentId = s.DepartmentId,
            MachineId = s.MachineId,
            WaterDepartmentId = s.WaterDepartmentId,
```

- [ ] **Step 3: Build and run the full backend suite**

Run: `dotnet build backend/MicroLIMS.sln && dotnet test backend/MicroLIMS.sln`
Expected: 0 errors, all tests pass (this task adds no new behavior, just plumbing — no new test needed; Task 3/4's tests already exercise `PrepareAsync`'s effect on the sample this DTO reads from).

- [ ] **Step 4: Commit**

```bash
git add backend/MicroLIMS.Application/DTOs/SampleDto.cs backend/MicroLIMS.Application/Services/TestingWorkspaceService.cs
git commit -m "feat(water): surface WaterDepartmentId on SampleDto"
```

---

## Task 6: Receiving grid — Water joins the "Preparation" pattern

**Files:**
- Modify: `frontend/src/modules/receiving/types/receivingTypes.ts`
- Modify: `frontend/src/services/masterDataOptions.ts`
- Modify: `frontend/src/modules/receiving/dialogs/NewSampleDialog.tsx`
- Modify: `frontend/src/modules/receiving/dialogs/MultiSampleEntryGrid.tsx`

**Interfaces:**
- Consumes: `GET /api/masterdata/water-departments` (already exists from the earlier Water config-parity work — returns `{ id, name, samplingPoints }[]`).
- Consumes: `POST /api/water/receive` with the new `{ waterDepartmentId, ... }` shape (Task 2).
- Produces: `masterDataOptions.getWaterDepartments()`, `ReceiveRowItem.departmentId` reused for water rows (the same field EM rows already use — a row belongs to exactly one category per dialog session, so no name collision).

- [ ] **Step 1: Update the frontend request/record types**

In `frontend/src/modules/receiving/types/receivingTypes.ts`, replace the `WaterReceiveRequest` interface (lines 50-56):

```typescript
export interface WaterReceiveRequest {
  waterDepartmentId: number;
  causeOfTestingId: number;
  sampleQuantity: string;
  sampledBy: string;
  controlNumber: string;
}
```

Add `waterDepartmentId` to `SampleRecord` (after line 17's `machineId`):

```typescript
  waterDepartmentId: number | null;
```

- [ ] **Step 2: Add the master-data loader**

In `frontend/src/services/masterDataOptions.ts`, add after the existing `getDepartments` line:

```typescript
  getWaterDepartments: () => apiClient.get("/masterdata/water-departments").then((r) => r.data.data),
```

- [ ] **Step 3: Load water departments and update validation/submit in `NewSampleDialog.tsx`**

In `frontend/src/modules/receiving/dialogs/NewSampleDialog.tsx`:

Update the `masterData` state type and initial value (lines 52-64) to add `waterDepartments`:

```typescript
  const [masterData, setMasterData] = useState<{
    items: any[];
    waterPoints: any[];
    waterDepartments: any[];
    departments: any[];
    machines: any[];
    causes: any[];
  }>({
    items: [],
    waterPoints: [],
    waterDepartments: [],
    departments: [],
    machines: [],
    causes: []
  });
```

Add a loader alongside the existing ones in the `useEffect` that runs on open (after the `getWaterSamplingPoints` call):

```typescript
      masterDataOptions.getWaterDepartments().then((waterDepartments) =>
        setMasterData((prev) => ({ ...prev, waterDepartments }))
      );
```

Update `validateRows`'s water branch (lines 159-163) from checking `waterSamplingPointId` to `departmentId`:

```typescript
      } else if (category === "water") {
        if (!row.departmentId) {
          errors.departmentId = "Department is required";
          isValid = false;
        }
```

Update `handleSaveAll`'s water branch (lines 229-236) to submit the department id:

```typescript
        } else if (category === "water") {
          await ReceiveService.receiveWater({
            waterDepartmentId: Number(row.departmentId),
            causeOfTestingId: Number(row.causeOfTestingId),
            sampleQuantity: row.sampleQuantity || "",
            sampledBy: row.sampledBy || "",
            controlNumber: row.controlNumber || ""
          });
```

- [ ] **Step 4: Update `MultiSampleEntryGrid.tsx`**

In `frontend/src/modules/receiving/dialogs/MultiSampleEntryGrid.tsx`:

Update the `MasterData` interface (lines 25-31) to add `waterDepartments`:

```typescript
interface MasterData {
  items: any[];
  waterPoints: any[];
  waterDepartments: any[];
  departments: any[];
  machines: any[];
  causes: any[];
}
```

Extend the amber "Preparation" banner (lines 57-76) to cover Water:

```typescript
      {(isEM || isAC || isWater) && (
        <Box
          sx={{
            display: "flex",
            alignItems: "center",
            gap: 1,
            p: 1.25,
            bgcolor: "#fef3c7",
            borderRadius: 1.5,
            border: "1px solid #fde68a"
          }}
        >
          <InfoOutlinedIcon sx={{ fontSize: 18, color: "#b45309" }} />
          <Typography sx={{ fontSize: 12, color: "#92400e", fontWeight: 500 }}>
            {isEM
              ? "Rooms, test locations, and test types are configured in the Preparation step after receiving."
              : isAC
              ? "Machine parts, test locations, and test types are configured in the Preparation step after receiving."
              : "Sampling points and test types are configured in the Preparation step after receiving."}
          </Typography>
        </Box>
      )}
```

Replace the Water header cell (lines 103-107):

```typescript
                {isWater && (
                  <TableCell sx={{ minWidth: 180 }}>
                    Department <span style={{ color: "#dc2626" }}>*</span>
                  </TableCell>
                )}
```

Replace the Water body cell (lines 216-238) — was bound to `row.waterSamplingPointId`/`masterData.waterPoints`, now bound to `row.departmentId`/`masterData.waterDepartments`:

```typescript
                    {/* Department (for Water) */}
                    {isWater && (
                      <TableCell>
                        <Select
                          size="small"
                          fullWidth
                          displayEmpty
                          value={row.departmentId ?? ""}
                          error={Boolean(errors.departmentId)}
                          onChange={(e) => onChangeRow(idx, "departmentId", e.target.value)}
                          sx={{ fontSize: 12 }}
                        >
                          <MenuItem value="">
                            <em style={{ color: "#9ca3af" }}>Select Department</em>
                          </MenuItem>
                          {masterData.waterDepartments.map((d) => (
                            <MenuItem key={d.id} value={d.id}>
                              {d.name}
                            </MenuItem>
                          ))}
                        </Select>
                      </TableCell>
                    )}
```

Do **not** change the Quantity column guard (`{!isEM && !isAC && (...)}`, line 311) — water already falls through it unchanged, keeping the Quantity field per the spec's ruling.

- [ ] **Step 5: Type-check**

Run: `cd frontend && npx tsc --noEmit`
Expected: no errors.

- [ ] **Step 6: Build**

Run: `cd frontend && npm run build`
Expected: build succeeds.

- [ ] **Step 7: Commit**

```bash
git add frontend/src/modules/receiving frontend/src/services/masterDataOptions.ts
git commit -m "feat(water): receiving grid captures a department, tests configured at Preparation"
```

---

## Task 7: `WaterPreparationForm` and `PreparationDialog` wiring

**Files:**
- Create: `frontend/src/modules/laboratoryConfiguration/water/services/WaterPreparationService.ts`
- Create: `frontend/src/modules/laboratoryConfiguration/water/WaterPreparationForm.tsx`
- Modify: `frontend/src/modules/testPreparation/PreparationDialog.tsx`
- Modify: `frontend/src/modules/receiving/ReceiveSamplePage.tsx`
- Modify: `frontend/src/modules/testingWorkspace/types/workspaceTypes.ts`

**Interfaces:**
- Consumes: `GET /api/masterdata/water-departments` (grouped departments with `samplingPoints`, each carrying `assignedTestCodes`), `POST /api/water/prepare` (Task 3).
- Produces: `<WaterPreparationForm sampleId waterDepartmentId onComplete />`, wired into `PreparationDialog` exactly like `EMPreparationForm`.

- [ ] **Step 1: Create the preparation service**

Create `frontend/src/modules/laboratoryConfiguration/water/services/WaterPreparationService.ts`:

```typescript
import { apiClient } from "../../../../services/apiClient";

// Backs the Water Preparation checklist (Testing Workspace -> Prepare).
// Mirrors EMPreparationService, but a water department's checklist items
// are its sampling points directly (each already carries its own
// assignedTestCodes), so one grouped GET is enough - no per-item config
// fetch like EM's Room -> RoomTestConfiguration two-step lookup.
export const WaterPreparationService = {
  getSamplingPointsForDepartment: (waterDepartmentId: number) =>
    apiClient.get("/masterdata/water-departments").then((r) => {
      const department = r.data.data.find((d: any) => d.id === waterDepartmentId);
      return department?.samplingPoints ?? [];
    }),
  prepare: (sampleId: number, waterSamplingPointIds: number[]) =>
    apiClient.post("/water/prepare", { sampleId, waterSamplingPointIds }).then((r) => r.data.data)
};
```

- [ ] **Step 2: Create the preparation form**

Create `frontend/src/modules/laboratoryConfiguration/water/WaterPreparationForm.tsx`:

```tsx
import { useEffect, useState } from "react";
import { Box, Table, TableHead, TableRow, TableCell, TableBody, Checkbox, Button, Alert, Typography } from "@mui/material";
import { WaterPreparationService } from "./services/WaterPreparationService";

interface SamplingPoint { id: number; code: string; location: string; assignedTestCodes: string[] }

interface Props {
  sampleId: number;
  waterDepartmentId: number;
  onComplete: () => void;
}

// One checkbox per sampling point - checking a point includes ALL of its
// assigned tests in this batch (one TestOrder per distinct TestCode
// across every selected point, not one TestOrder per point). Mirrors
// EMPreparationForm's Room checklist.
export function WaterPreparationForm({ sampleId, waterDepartmentId, onComplete }: Props) {
  const [points, setPoints] = useState<SamplingPoint[]>([]);
  const [checked, setChecked] = useState<Record<number, boolean>>({});
  const [message, setMessage] = useState<{ text: string; ok: boolean } | null>(null);

  useEffect(() => {
    WaterPreparationService.getSamplingPointsForDepartment(waterDepartmentId).then(setPoints);
  }, [waterDepartmentId]);

  const toggle = (pointId: number) => setChecked((c) => ({ ...c, [pointId]: !c[pointId] }));

  const confirm = async () => {
    setMessage(null);
    const waterSamplingPointIds = points.filter((p) => checked[p.id]).map((p) => p.id);

    try {
      await WaterPreparationService.prepare(sampleId, waterSamplingPointIds);
      onComplete();
    } catch (e: any) {
      setMessage({ text: e?.response?.data?.message ?? "Could not prepare sample.", ok: false });
    }
  };

  return (
    <Box>
      {message && <Alert severity="error" sx={{ mb: 2 }}>{message.text}</Alert>}
      {points.length > 0 && (
        <Box sx={{ overflowX: "auto" }}>
          <Table>
            <TableHead>
              <TableRow>
                <TableCell padding="checkbox" />
                <TableCell>Sampling Point</TableCell>
                <TableCell>Assigned Tests</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {points.map((point) => (
                <TableRow key={point.id} hover>
                  <TableCell padding="checkbox">
                    <Checkbox
                      checked={!!checked[point.id]}
                      disabled={!point.assignedTestCodes || point.assignedTestCodes.length === 0}
                      onChange={() => toggle(point.id)}
                    />
                  </TableCell>
                  <TableCell>
                    {point.code}{point.location ? ` (${point.location})` : ""}
                  </TableCell>
                  <TableCell>
                    <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
                      {point.assignedTestCodes && point.assignedTestCodes.length > 0
                        ? point.assignedTestCodes.join(", ")
                        : "No tests assigned"}
                    </Typography>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
          <Box sx={{ display: "flex", justifyContent: "flex-end", mt: 2 }}>
            <Button variant="contained" onClick={confirm}>Start Testing</Button>
          </Box>
        </Box>
      )}
    </Box>
  );
}
```

- [ ] **Step 3: Wire the Water branch into `PreparationDialog`**

Rewrite `frontend/src/modules/testPreparation/PreparationDialog.tsx`:

```tsx
import { FloatingDialog } from "../../components/FloatingDialog";
import { TestPreparationForm } from "./TestPreparationForm";
import { EMPreparationForm } from "../laboratoryConfiguration/environmentalMonitoring/EMPreparationForm";
import { AfterCleaningPreparationForm } from "../laboratoryConfiguration/afterCleaning/AfterCleaningPreparationForm";
import { WaterPreparationForm } from "../laboratoryConfiguration/water/WaterPreparationForm";

interface Props {
  open: boolean;
  sample: { sampleId: number; category: string; departmentId?: number | null; machineId?: number | null; waterDepartmentId?: number | null } | null;
  onClose: () => void;
}

// Opened directly from a Testing Workspace card when a sample "Needs
// Preparation" - routes to the right form by category, reusing the same
// forms the standalone EM/After Cleaning/Water/Test Preparation pages use.
export function PreparationDialog({ open, sample, onClose }: Props) {
  if (!sample) return null;

  return (
    <FloatingDialog open={open} title="Preparation" onClose={onClose}>
      {sample.category === "EnvironmentalMonitoring" && sample.departmentId != null && (
        <EMPreparationForm sampleId={sample.sampleId} departmentId={sample.departmentId} onComplete={onClose} />
      )}
      {sample.category === "AfterCleaning" && sample.machineId != null && (
        <AfterCleaningPreparationForm sampleId={sample.sampleId} machineId={sample.machineId} onComplete={onClose} />
      )}
      {sample.category === "Water" && sample.waterDepartmentId != null && (
        <WaterPreparationForm sampleId={sample.sampleId} waterDepartmentId={sample.waterDepartmentId} onComplete={onClose} />
      )}
      {sample.category !== "EnvironmentalMonitoring" && sample.category !== "AfterCleaning" && sample.category !== "Water" && (
        <TestPreparationForm sample={sample} onSaved={onClose} />
      )}
    </FloatingDialog>
  );
}
```

- [ ] **Step 4: Pass `waterDepartmentId` from `ReceiveSamplePage`**

In `frontend/src/modules/receiving/ReceiveSamplePage.tsx`, update the `PreparationDialog` call (around line 371-378):

```tsx
      <PreparationDialog
        open={Boolean(preparingSample)}
        sample={preparingSample ? {
          sampleId: preparingSample.sampleId,
          category: preparingSample.category,
          departmentId: preparingSample.departmentId,
          machineId: preparingSample.machineId,
          waterDepartmentId: preparingSample.waterDepartmentId
        } : null}
        onClose={() => {
          setPreparingSample(null);
          loadRecords();
        }}
      />
```

- [ ] **Step 5: Add `waterDepartmentId` to `SampleCard`**

In `frontend/src/modules/testingWorkspace/types/workspaceTypes.ts`, add after `machineId` (line 17):

```typescript
  waterDepartmentId: number | null;
```

This makes `TestingWorkspacePage.tsx`'s existing `<PreparationDialog sample={preparingSample} ...>` call (which passes the whole `SampleCard` object, no changes needed there) carry the new field automatically once it's part of the type mirrored from `SampleDto`.

- [ ] **Step 6: Type-check**

Run: `cd frontend && npx tsc --noEmit`
Expected: no errors.

- [ ] **Step 7: Build**

Run: `cd frontend && npm run build`
Expected: build succeeds.

- [ ] **Step 8: Commit**

```bash
git add frontend/src/modules/laboratoryConfiguration/water/services/WaterPreparationService.ts frontend/src/modules/laboratoryConfiguration/water/WaterPreparationForm.tsx frontend/src/modules/testPreparation/PreparationDialog.tsx frontend/src/modules/receiving/ReceiveSamplePage.tsx frontend/src/modules/testingWorkspace/types/workspaceTypes.ts
git commit -m "feat(water): add WaterPreparationForm and wire it into PreparationDialog"
```

---

## Task 8: End-to-end verification

**Files:** none (manual verification via the browser preview).

- [ ] **Step 1: Run the full backend test suite**

Run: `dotnet test backend/MicroLIMS.sln`
Expected: all tests pass, including `WaterBatchPrepareTests` and the rewritten `WaterAndEMEngineTests`.

- [ ] **Step 2: Apply the migration to the local dev database**

Run (from `backend/`):

```bash
dotnet ef database update --project MicroLIMS.Persistence --startup-project MicroLIMS.API
```

Expected: `AddWaterBatchModel` applies with no errors.

- [ ] **Step 3: Frontend build**

Run: `cd frontend && npx tsc --noEmit && npm run build`
Expected: no errors, build succeeds.

- [ ] **Step 4: Manual click-through (requires an authenticated session)**

1. Receiving → New Sample → Water. Confirm the grid shows a **Department** column (not Sampling Point) and the amber "configured in the Preparation step" banner. Confirm **Quantity** is still present.
2. Save a water sample. Confirm it appears with status "Needs Preparation" and **no assigned tests yet**.
3. Open its Preparation dialog from the Testing Workspace. Confirm it shows the `WaterPreparationForm` checklist of sampling points (with their assigned tests) for that department, not the generic `TestPreparationForm`.
4. Check one or more points and click "Start Testing". Confirm the sample becomes "Ready" with one TestOrder per distinct test code and the right number of locations.
5. Confirm a **legacy** water sample (one received before this change, if any exist in the dev DB) still calculates correctly through its existing result-entry screen — this proves the guard didn't break old data.

- [ ] **Step 5: Report status**

If step 4 cannot be completed (no authenticated session available), report exactly that — steps 1-3 (automated) are the completion bar for this phase; step 4 is manual confirmation for the user.

---

## Self-Review Notes

- **Spec coverage:** Receiving captures a Water Department (Task 6); Preparation selects sampling points, generating TestOrders/SampleLocations (Task 3, 7); count-test limit linking via `WorkflowType.CountTest` (Task 3); Quantity retained on Water (Task 6, explicitly not touched); existing samples not migrated + legacy calculate still works (Task 4, backward-compat test + guard); `SampleLocation` extended not forked (Task 1). Phase 2 (per-location count result entry) and Phase 3 (pathogen detection grid) are explicitly out of scope here per the spec's phasing — this plan implements Phase 1 only.
- **Type consistency:** `WaterReceiveRequest.WaterDepartmentId` (backend record) / `waterDepartmentId` (frontend interface, controller JSON) used consistently end-to-end. `PrepareWaterRequest.WaterSamplingPointIds` / `waterSamplingPointIds` likewise. `SampleLocation.SamplingConfigurationId` name matches between Task 1 (entity) and Task 3 (engine code that sets it). `SampleDto.WaterDepartmentId` / `SampleCard.waterDepartmentId` / `SampleRecord.waterDepartmentId` all mirror the same field across the three independent DTO-mirror files this codebase maintains (backend DTO, workspace types, receiving types) — matching the existing pattern where `departmentId`/`machineId` are each mirrored in all three.
- **Note for executor:** this repository is not a git repository in the current environment (confirmed in an earlier phase of this project). If `git` commands fail, skip the commit steps and treat each task boundary as a checkpoint instead, exactly as was done for the Water Configuration EM-parity plan that shipped earlier today.
