# Water Configuration — EM Parity Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give the Water configuration page an EM-style hierarchy — Water Department → Sample Location → assigned tests → Alert/Action/Specification limits (count tests only) — so water count results are evaluated against real limits instead of always returning "WithinLimits".

**Architecture:** Add a new `WaterDepartment` entity and a nullable `WaterDepartmentId` FK on the existing `WaterSamplingPoint` (never replacing it — `Sample` and `SamplingConfiguration` reference its Id). Add CRUD endpoints on `MasterDataController` mirroring the existing EM `departments` / `rooms` / `room-test-configurations` endpoints, plus a `water-sampling-configurations` family that writes the already-consumed `SamplingConfiguration` table. Rebuild `WaterConfigPage.tsx` to mirror `EMConfigPage.tsx`. The `WaterWorkflowEngine` needs **zero** changes — it already reads `SamplingConfiguration` limits.

**Tech Stack:** .NET 8 / EF Core (InMemory for tests, xUnit), ASP.NET Core controllers, React + TypeScript + MUI, axios (`apiClient`).

**Spec:** `docs/superpowers/specs/2026-08-17-water-config-em-parity-design.md`

## Global Constraints

- Mutating master-data endpoints carry `[Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]`, matching every sibling endpoint in `MasterDataController`.
- All controller actions return `Ok(ApiResponse<object>.Ok(payload))`; frontend reads `r.data.data`.
- `WaterSamplingPoint.Id` is immutable and must be preserved — it is referenced by `Sample.WaterSamplingPointId` and `SamplingConfiguration.WaterSamplingPointId`.
- Limits fields are surfaced **only** for tests whose `TestDefinition.WorkflowType == CountTest`. Never hardcode the string `"TAMC-Water"`.
- Backend tests use `MicroLimsDbContext` on `UseInMemoryDatabase(Guid.NewGuid().ToString())`, following `WaterAndEMEngineTests.cs`.
- `WaterDepartment` fields are exactly `Name` + `TestingFrequency` (no `Class`).

---

## File Structure

**Backend**
- `backend/MicroLIMS.Domain/Entities/WaterDepartment.cs` (new) — the department entity.
- `backend/MicroLIMS.Domain/Entities/WaterSamplingPoint.cs` (modify) — add FK + nav.
- `backend/MicroLIMS.Persistence/DbContext/MicroLimsDbContext.cs` (modify) — `DbSet` + relationship config.
- `backend/MicroLIMS.Persistence/Migrations/*` (new) — EF migration + data seed of default department.
- `backend/MicroLIMS.API/Controllers/MasterDataController.cs` (modify) — request records + 3 endpoint families.
- `backend/MicroLIMS.Tests/WorkflowTests/WaterAndEMEngineTests.cs` (modify) — limit-evaluation tests.
- `backend/MicroLIMS.Tests/WorkflowTests/WaterConfigCrudTests.cs` (new) — controller CRUD + guard tests.

**Frontend**
- `frontend/src/modules/laboratoryConfiguration/water/services/WaterConfigService.ts` (modify) — department + config methods, department id on points.
- `frontend/src/modules/laboratoryConfiguration/water/WaterConfigPage.tsx` (rewrite) — EM-mirrored hierarchy.

---

## Task 1: WaterDepartment entity, FK, DbSet, and migration

**Files:**
- Create: `backend/MicroLIMS.Domain/Entities/WaterDepartment.cs`
- Modify: `backend/MicroLIMS.Domain/Entities/WaterSamplingPoint.cs`
- Modify: `backend/MicroLIMS.Persistence/DbContext/MicroLimsDbContext.cs:43` (DbSet block) and its `OnModelCreating`
- Create: migration under `backend/MicroLIMS.Persistence/Migrations/`
- Test: `backend/MicroLIMS.Tests/WorkflowTests/WaterConfigCrudTests.cs`

**Interfaces:**
- Produces: `WaterDepartment { int Id; string Name; string TestingFrequency; List<WaterSamplingPoint> SamplingPoints; }`
- Produces: `WaterSamplingPoint.WaterDepartmentId : int?` and `WaterSamplingPoint.WaterDepartment : WaterDepartment?`
- Produces: `MicroLimsDbContext.WaterDepartments : DbSet<WaterDepartment>`

- [ ] **Step 1: Write the failing test**

Create `backend/MicroLIMS.Tests/WorkflowTests/WaterConfigCrudTests.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.WorkflowTests;

public class WaterConfigCrudTests
{
    private static MicroLimsDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MicroLimsDbContext(options);
    }

    [Fact]
    public async Task SamplingPoint_CanBeLinkedToWaterDepartment()
    {
        await using var db = NewDb();
        var dept = new WaterDepartment { Name = "WTU", TestingFrequency = "Weekly" };
        db.WaterDepartments.Add(dept);
        await db.SaveChangesAsync();

        db.WaterSamplingPoints.Add(new WaterSamplingPoint
        {
            Code = "SP106", Location = "WTU", WaterDepartmentId = dept.Id, AssignedTestCodes = new() { "TAMC-Water" }
        });
        await db.SaveChangesAsync();

        var loaded = await db.WaterDepartments.Include(d => d.SamplingPoints).FirstAsync(d => d.Id == dept.Id);
        Assert.Single(loaded.SamplingPoints);
        Assert.Equal("SP106", loaded.SamplingPoints[0].Code);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test backend/MicroLIMS.Tests --filter WaterConfigCrudTests`
Expected: FAIL to compile — `WaterDepartment` / `WaterDepartments` / `WaterDepartmentId` do not exist yet.

- [ ] **Step 3: Create the entity**

`backend/MicroLIMS.Domain/Entities/WaterDepartment.cs`:

```csharp
namespace MicroLIMS.Domain.Entities;

// Water-specific department, deliberately separate from the EM
// Department entity. A sample location (WaterSamplingPoint) hangs off
// one of these, mirroring EM's Department -> Room hierarchy.
public class WaterDepartment
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string TestingFrequency { get; set; } = string.Empty;
    public List<WaterSamplingPoint> SamplingPoints { get; set; } = new();
}
```

- [ ] **Step 4: Add the FK to WaterSamplingPoint**

Modify `backend/MicroLIMS.Domain/Entities/WaterSamplingPoint.cs` to:

```csharp
namespace MicroLIMS.Domain.Entities;

public class WaterSamplingPoint
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public List<string> AssignedTestCodes { get; set; } = new();

    public int? WaterDepartmentId { get; set; }
    public WaterDepartment? WaterDepartment { get; set; }
}
```

- [ ] **Step 5: Register the DbSet and relationship**

In `backend/MicroLIMS.Persistence/DbContext/MicroLimsDbContext.cs`, add next to the other water DbSet (line 43):

```csharp
    public DbSet<WaterDepartment> WaterDepartments => Set<WaterDepartment>();
```

Then in `OnModelCreating`, add the relationship (place it near the other water/EM entity configuration, following the file's existing style):

```csharp
        modelBuilder.Entity<WaterSamplingPoint>()
            .HasOne(p => p.WaterDepartment)
            .WithMany(d => d.SamplingPoints)
            .HasForeignKey(p => p.WaterDepartmentId)
            .OnDelete(DeleteBehavior.Restrict);
```

- [ ] **Step 6: Run test to verify it passes**

Run: `dotnet test backend/MicroLIMS.Tests --filter WaterConfigCrudTests`
Expected: PASS.

- [ ] **Step 7: Create the EF migration with a default-department data seed**

Run (from `backend/`, adjust project paths to match how migrations are usually generated in this repo — the startup project is `MicroLIMS.API`):

```bash
dotnet ef migrations add AddWaterDepartment --project MicroLIMS.Persistence --startup-project MicroLIMS.API
```

Then open the generated migration's `Up(...)` and, **after** the `CreateTable`/`AddColumn` calls EF generated, append raw SQL that seeds one default department and backfills existing points so none are orphaned:

```csharp
            migrationBuilder.Sql(@"
INSERT INTO ""WaterDepartments"" (""Name"", ""TestingFrequency"") VALUES ('Water', '');
UPDATE ""WaterSamplingPoints""
SET ""WaterDepartmentId"" = (SELECT ""Id"" FROM ""WaterDepartments"" WHERE ""Name"" = 'Water' LIMIT 1)
WHERE ""WaterDepartmentId"" IS NULL;");
```

(If the production DB is SQL Server rather than Postgres, use `SELECT TOP 1 Id ...` and drop the double-quotes to match the dialect already used by other migrations in this folder. Match the existing migrations' quoting style.)

- [ ] **Step 8: Verify the solution builds**

Run: `dotnet build backend/MicroLIMS.sln`
Expected: build succeeds.

- [ ] **Step 9: Commit**

```bash
git add backend/MicroLIMS.Domain/Entities/WaterDepartment.cs backend/MicroLIMS.Domain/Entities/WaterSamplingPoint.cs backend/MicroLIMS.Persistence/DbContext/MicroLimsDbContext.cs backend/MicroLIMS.Persistence/Migrations backend/MicroLIMS.Tests/WorkflowTests/WaterConfigCrudTests.cs
git commit -m "feat(water): add WaterDepartment entity and sampling-point FK"
```

---

## Task 2: water-departments CRUD endpoints

**Files:**
- Modify: `backend/MicroLIMS.API/Controllers/MasterDataController.cs` (request records near line 13-45; new endpoints after the Water Sampling Points block, ~line 109)
- Test: `backend/MicroLIMS.Tests/WorkflowTests/WaterConfigCrudTests.cs`

**Interfaces:**
- Consumes: `WaterDepartment`, `MicroLimsDbContext.WaterDepartments` (Task 1).
- Produces endpoints: `GET/POST/PUT/DELETE /api/masterdata/water-departments[/{id}]`.
- Produces request records: `CreateWaterDepartmentRequest(string Name, string TestingFrequency)`, `UpdateWaterDepartmentRequest(string Name, string TestingFrequency)`.

- [ ] **Step 1: Write the failing tests**

Add to `WaterConfigCrudTests.cs`:

```csharp
    [Fact]
    public async Task CreateWaterDepartment_PersistsRow()
    {
        await using var db = NewDb();
        var controller = new MicroLIMS.API.Controllers.MasterDataController(db);

        await controller.CreateWaterDepartment(
            new MicroLIMS.API.Controllers.CreateWaterDepartmentRequest("WTU", "Weekly"));

        var dept = await db.WaterDepartments.SingleAsync();
        Assert.Equal("WTU", dept.Name);
        Assert.Equal("Weekly", dept.TestingFrequency);
    }

    [Fact]
    public async Task DeleteWaterDepartment_WithSamplingPoints_Throws()
    {
        await using var db = NewDb();
        var dept = new WaterDepartment { Name = "WTU", TestingFrequency = "" };
        db.WaterDepartments.Add(dept);
        await db.SaveChangesAsync();
        db.WaterSamplingPoints.Add(new WaterSamplingPoint { Code = "SP1", WaterDepartmentId = dept.Id });
        await db.SaveChangesAsync();

        var controller = new MicroLIMS.API.Controllers.MasterDataController(db);
        await Assert.ThrowsAsync<InvalidOperationException>(() => controller.DeleteWaterDepartment(dept.Id));
    }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test backend/MicroLIMS.Tests --filter WaterConfigCrudTests`
Expected: FAIL to compile — `CreateWaterDepartment`, `DeleteWaterDepartment`, and the request records don't exist.

- [ ] **Step 3: Add the request records**

In `MasterDataController.cs`, alongside the existing records (near line 15):

```csharp
public record CreateWaterDepartmentRequest(string Name, string TestingFrequency);
public record UpdateWaterDepartmentRequest(string Name, string TestingFrequency);
```

- [ ] **Step 4: Add the endpoints**

In `MasterDataController.cs`, add a new region after the Water Sampling Points block (after line 109), mirroring `GetDepartments`/`CreateDepartment`/`UpdateDepartment`/`DeleteDepartment`:

```csharp
    // ---- Water Departments ----
    [HttpGet("water-departments")]
    public async Task<IActionResult> GetWaterDepartments()
    {
        // Shaped projection to avoid the WaterDepartment.SamplingPoints <->
        // WaterSamplingPoint.WaterDepartment navigation cycle, same pattern
        // as GetDepartments.
        var departments = await _db.WaterDepartments
            .Select(d => new
            {
                d.Id, d.Name, d.TestingFrequency,
                SamplingPoints = d.SamplingPoints.Select(p => new
                {
                    p.Id, p.Code, p.Location, p.WaterDepartmentId, p.AssignedTestCodes
                })
            })
            .ToListAsync();
        return Ok(ApiResponse<object>.Ok(departments));
    }

    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpPost("water-departments")]
    public async Task<IActionResult> CreateWaterDepartment(CreateWaterDepartmentRequest request)
    {
        var dept = new WaterDepartment { Name = request.Name, TestingFrequency = request.TestingFrequency };
        _db.WaterDepartments.Add(dept);
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(dept));
    }

    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpPut("water-departments/{id}")]
    public async Task<IActionResult> UpdateWaterDepartment(int id, UpdateWaterDepartmentRequest request)
    {
        var dept = await _db.WaterDepartments.FirstOrDefaultAsync(d => d.Id == id)
            ?? throw new InvalidOperationException($"Water department {id} not found.");
        dept.Name = request.Name;
        dept.TestingFrequency = request.TestingFrequency;
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(dept));
    }

    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpDelete("water-departments/{id}")]
    public async Task<IActionResult> DeleteWaterDepartment(int id)
    {
        var dept = await _db.WaterDepartments.FirstOrDefaultAsync(d => d.Id == id)
            ?? throw new InvalidOperationException($"Water department {id} not found.");

        var pointCount = await _db.WaterSamplingPoints.CountAsync(p => p.WaterDepartmentId == id);
        if (pointCount > 0)
            throw new InvalidOperationException($"Cannot delete '{dept.Name}' - it still has {pointCount} sample location(s). Delete those first.");

        _db.WaterDepartments.Remove(dept);
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(new { }));
    }
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test backend/MicroLIMS.Tests --filter WaterConfigCrudTests`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add backend/MicroLIMS.API/Controllers/MasterDataController.cs backend/MicroLIMS.Tests/WorkflowTests/WaterConfigCrudTests.cs
git commit -m "feat(water): add water-departments CRUD endpoints"
```

---

## Task 3: Sample locations gain a department

**Files:**
- Modify: `backend/MicroLIMS.API/Controllers/MasterDataController.cs:13-14` (request records) and `:63-88` (create/update)
- Test: `backend/MicroLIMS.Tests/WorkflowTests/WaterConfigCrudTests.cs`

**Interfaces:**
- Consumes: `WaterSamplingPoint.WaterDepartmentId` (Task 1).
- Produces: `CreateWaterSamplingPointRequest` and `UpdateWaterSamplingPointRequest` each gain `int? WaterDepartmentId`.

- [ ] **Step 1: Write the failing test**

Add to `WaterConfigCrudTests.cs`:

```csharp
    [Fact]
    public async Task CreateSamplingPoint_StoresDepartmentId()
    {
        await using var db = NewDb();
        var dept = new WaterDepartment { Name = "WTU", TestingFrequency = "" };
        db.WaterDepartments.Add(dept);
        await db.SaveChangesAsync();

        var controller = new MicroLIMS.API.Controllers.MasterDataController(db);
        await controller.CreateWaterSamplingPoint(new MicroLIMS.API.Controllers.CreateWaterSamplingPointRequest(
            "SP205", "WTU", new List<string> { "TAMC-Water" }, dept.Id));

        var point = await db.WaterSamplingPoints.SingleAsync();
        Assert.Equal(dept.Id, point.WaterDepartmentId);
    }
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test backend/MicroLIMS.Tests --filter WaterConfigCrudTests`
Expected: FAIL to compile — the request record has no 4th parameter.

- [ ] **Step 3: Extend the request records**

In `MasterDataController.cs` change lines 13-14 to:

```csharp
public record CreateWaterSamplingPointRequest(string Code, string Location, List<string> AssignedTestCodes, int? WaterDepartmentId);
public record UpdateWaterSamplingPointRequest(string Code, string Location, List<string> AssignedTestCodes, int? WaterDepartmentId);
```

- [ ] **Step 4: Set the FK in create and update**

In `CreateWaterSamplingPoint` (line 71), extend the object initializer:

```csharp
        var point = new WaterSamplingPoint { Code = request.Code, Location = request.Location, AssignedTestCodes = request.AssignedTestCodes, WaterDepartmentId = request.WaterDepartmentId };
```

In `UpdateWaterSamplingPoint` (after line 85), add:

```csharp
        point.WaterDepartmentId = request.WaterDepartmentId;
```

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test backend/MicroLIMS.Tests --filter WaterConfigCrudTests`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add backend/MicroLIMS.API/Controllers/MasterDataController.cs backend/MicroLIMS.Tests/WorkflowTests/WaterConfigCrudTests.cs
git commit -m "feat(water): sample locations carry a WaterDepartmentId"
```

---

## Task 4: water-sampling-configurations CRUD endpoints

**Files:**
- Modify: `backend/MicroLIMS.API/Controllers/MasterDataController.cs` (request records + new endpoint family after the Water Departments block)
- Test: `backend/MicroLIMS.Tests/WorkflowTests/WaterConfigCrudTests.cs`

**Interfaces:**
- Consumes: `SamplingConfiguration` and `MicroLimsDbContext.SamplingConfigurations` (existing).
- Produces endpoints: `GET /api/masterdata/water-sampling-configurations?pointId=`, `POST`, `PUT /{id}`, `DELETE /{id}`.
- Produces records: `CreateWaterSamplingConfigRequest(int WaterSamplingPointId, string TestCode, string AlertLimit, string ActionLimit, string SpecLimit)`, `UpdateWaterSamplingConfigRequest(string TestCode, string AlertLimit, string ActionLimit, string SpecLimit)`.

- [ ] **Step 1: Write the failing test**

Add to `WaterConfigCrudTests.cs`:

```csharp
    [Fact]
    public async Task CreateWaterSamplingConfig_PersistsLimits()
    {
        await using var db = NewDb();
        var point = new WaterSamplingPoint { Code = "SP104", Location = "WTU" };
        db.WaterSamplingPoints.Add(point);
        await db.SaveChangesAsync();

        var controller = new MicroLIMS.API.Controllers.MasterDataController(db);
        await controller.CreateWaterSamplingConfiguration(new MicroLIMS.API.Controllers.CreateWaterSamplingConfigRequest(
            point.Id, "TAMC-Water", "10", "50", "100"));

        var config = await db.SamplingConfigurations.SingleAsync();
        Assert.Equal(point.Id, config.WaterSamplingPointId);
        Assert.Equal("TAMC-Water", config.TestCode);
        Assert.Equal("50", config.ActionLimit);
    }
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test backend/MicroLIMS.Tests --filter WaterConfigCrudTests`
Expected: FAIL to compile — endpoint/record missing.

- [ ] **Step 3: Add the request records**

In `MasterDataController.cs`, alongside the other records:

```csharp
public record CreateWaterSamplingConfigRequest(int WaterSamplingPointId, string TestCode, string AlertLimit, string ActionLimit, string SpecLimit);
public record UpdateWaterSamplingConfigRequest(string TestCode, string AlertLimit, string ActionLimit, string SpecLimit);
```

- [ ] **Step 4: Add the endpoints**

After the Water Departments block, mirroring `room-test-configurations` (lines 461-506):

```csharp
    // ---- Water Sampling Configurations (per sample location x test limits) ----
    [HttpGet("water-sampling-configurations")]
    public async Task<IActionResult> GetWaterSamplingConfigurations([FromQuery] int pointId) =>
        Ok(ApiResponse<object>.Ok(await _db.SamplingConfigurations.Where(c => c.WaterSamplingPointId == pointId).ToListAsync()));

    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpPost("water-sampling-configurations")]
    public async Task<IActionResult> CreateWaterSamplingConfiguration(CreateWaterSamplingConfigRequest request)
    {
        var entity = new SamplingConfiguration
        {
            WaterSamplingPointId = request.WaterSamplingPointId, TestCode = request.TestCode,
            AlertLimit = request.AlertLimit, ActionLimit = request.ActionLimit, SpecLimit = request.SpecLimit
        };
        _db.SamplingConfigurations.Add(entity);
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(entity));
    }

    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpPut("water-sampling-configurations/{id}")]
    public async Task<IActionResult> UpdateWaterSamplingConfiguration(int id, UpdateWaterSamplingConfigRequest request)
    {
        var entity = await _db.SamplingConfigurations.FirstOrDefaultAsync(c => c.Id == id)
            ?? throw new InvalidOperationException($"Water sampling configuration {id} not found.");
        entity.TestCode = request.TestCode;
        entity.AlertLimit = request.AlertLimit;
        entity.ActionLimit = request.ActionLimit;
        entity.SpecLimit = request.SpecLimit;
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(entity));
    }

    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpDelete("water-sampling-configurations/{id}")]
    public async Task<IActionResult> DeleteWaterSamplingConfiguration(int id)
    {
        var entity = await _db.SamplingConfigurations.FirstOrDefaultAsync(c => c.Id == id)
            ?? throw new InvalidOperationException($"Water sampling configuration {id} not found.");
        _db.SamplingConfigurations.Remove(entity);
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(new { }));
    }
```

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test backend/MicroLIMS.Tests --filter WaterConfigCrudTests`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add backend/MicroLIMS.API/Controllers/MasterDataController.cs backend/MicroLIMS.Tests/WorkflowTests/WaterConfigCrudTests.cs
git commit -m "feat(water): add water-sampling-configurations CRUD endpoints"
```

---

## Task 5: Lock the now-live limit evaluation with engine tests

**Files:**
- Modify: `backend/MicroLIMS.Tests/WorkflowTests/WaterAndEMEngineTests.cs`

**Interfaces:**
- Consumes: `WaterWorkflowEngine.ReceiveAsync`, `CalculateAndCompareAsync`, `SamplingConfiguration` (all existing).

This task adds no production code — it proves the previously-dead limit path (`WaterWorkflowEngine.cs:76-82`) now produces real Alert/Action verdicts once configs exist, and stays "WithinLimits" with none. The existing `Water_AverageExceedsSpecLimit_FlagsOutOfSpecification` already covers the spec ceiling; we add the alert and action rungs plus the no-config baseline.

- [ ] **Step 1: Write the failing tests**

Add to `WaterAndEMEngineTests.cs`:

```csharp
    [Theory]
    [InlineData(new object[] { new double[] { 12, 14 }, "AlertLimitExceeded" })]   // avg 13 > alert 10, < action 50
    [InlineData(new object[] { new double[] { 60, 60 }, "ActionLimitExceeded" })]  // avg 60 > action 50, < spec 100
    public async Task Water_ConfiguredLimits_ProduceExpectedStatus(double[] readings, string expected)
    {
        await using var db = NewDb();
        var point = new WaterSamplingPoint { Code = "WP-10", Location = "WTU", AssignedTestCodes = new() { "TAMC" } };
        db.WaterSamplingPoints.Add(point);
        await db.SaveChangesAsync();
        db.SamplingConfigurations.Add(new SamplingConfiguration
        {
            WaterSamplingPointId = point.Id, TestCode = "TAMC", AlertLimit = "10", ActionLimit = "50", SpecLimit = "100"
        });
        await db.SaveChangesAsync();

        var engine = new WaterWorkflowEngine(db, new ReferenceNumberGenerator(db));
        var sample = await engine.ReceiveAsync(new WaterReceiveRequest(point.Id, 0, "500ml", "Analyst", "CTRL-10", 1));
        var order = sample.TestOrders.First();

        var result = await engine.CalculateAndCompareAsync(order.Id, readings.Select(r => (decimal)r).ToList());

        Assert.Equal(expected, result.Status);
    }

    [Fact]
    public async Task Water_NoConfiguredLimits_StaysWithinLimits()
    {
        await using var db = NewDb();
        var point = new WaterSamplingPoint { Code = "WP-11", Location = "WTU", AssignedTestCodes = new() { "TAMC" } };
        db.WaterSamplingPoints.Add(point);
        await db.SaveChangesAsync();

        var engine = new WaterWorkflowEngine(db, new ReferenceNumberGenerator(db));
        var sample = await engine.ReceiveAsync(new WaterReceiveRequest(point.Id, 0, "500ml", "Analyst", "CTRL-11", 1));
        var order = sample.TestOrders.First();

        var result = await engine.CalculateAndCompareAsync(order.Id, new List<decimal> { 9999 });

        Assert.Equal("WithinLimits", result.Status);
    }
```

- [ ] **Step 2: Run the tests**

Run: `dotnet test backend/MicroLIMS.Tests --filter WaterAndEMEngineTests`
Expected: PASS (the engine already implements this; these lock it). If a status string differs, read `WaterWorkflowEngine.Compare` (lines 100-108) and correct the expected value to the engine's actual return — do not change the engine.

- [ ] **Step 3: Commit**

```bash
git add backend/MicroLIMS.Tests/WorkflowTests/WaterAndEMEngineTests.cs
git commit -m "test(water): lock alert/action/within-limits evaluation for configured limits"
```

---

## Task 6: Frontend WaterConfigService — departments, department id, configs

**Files:**
- Modify: `frontend/src/modules/laboratoryConfiguration/water/services/WaterConfigService.ts`

**Interfaces:**
- Consumes: the Task 2/3/4 endpoints.
- Produces methods used by Task 7: `getWaterDepartments`, `createWaterDepartment`, `updateWaterDepartment`, `deleteWaterDepartment`, `createSamplingPoint(code, location, assignedTestCodes, waterDepartmentId)`, `updateSamplingPoint(id, code, location, assignedTestCodes, waterDepartmentId)`, `getSamplingConfigurations(pointId)`, `createSamplingConfiguration(pointId, testCode, alert, action, spec)`, `updateSamplingConfiguration(id, testCode, alert, action, spec)`, `deleteSamplingConfiguration(id)`.

- [ ] **Step 1: Replace the service file**

Rewrite `WaterConfigService.ts`:

```typescript
import { apiClient } from "../../../../services/apiClient";

// Water configuration - Laboratory Configuration > Water page. Mirrors
// EMConfigService: Water Departments -> Sample Locations (sampling
// points) -> per-count-test limits (SamplingConfiguration). Separate
// from WaterService.ts, which is the calculation engine used inside the
// Testing Workspace dialog.
export const WaterConfigService = {
  getWaterDepartments: () => apiClient.get("/masterdata/water-departments").then((r) => r.data.data),
  createWaterDepartment: (name: string, testingFrequency: string) =>
    apiClient.post("/masterdata/water-departments", { name, testingFrequency }).then((r) => r.data.data),
  updateWaterDepartment: (id: number, name: string, testingFrequency: string) =>
    apiClient.put(`/masterdata/water-departments/${id}`, { name, testingFrequency }).then((r) => r.data.data),
  deleteWaterDepartment: (id: number) => apiClient.delete(`/masterdata/water-departments/${id}`),

  getSamplingPoints: () => apiClient.get("/masterdata/water-sampling-points").then((r) => r.data.data),
  createSamplingPoint: (code: string, location: string, assignedTestCodes: string[], waterDepartmentId: number) =>
    apiClient.post("/masterdata/water-sampling-points", { code, location, assignedTestCodes, waterDepartmentId }).then((r) => r.data.data),
  updateSamplingPoint: (id: number, code: string, location: string, assignedTestCodes: string[], waterDepartmentId: number) =>
    apiClient.put(`/masterdata/water-sampling-points/${id}`, { code, location, assignedTestCodes, waterDepartmentId }).then((r) => r.data.data),
  deleteSamplingPoint: (id: number) => apiClient.delete(`/masterdata/water-sampling-points/${id}`),

  getSamplingConfigurations: (pointId: number) =>
    apiClient.get("/masterdata/water-sampling-configurations", { params: { pointId } }).then((r) => r.data.data),
  createSamplingConfiguration: (waterSamplingPointId: number, testCode: string, alertLimit: string, actionLimit: string, specLimit: string) =>
    apiClient.post("/masterdata/water-sampling-configurations", { waterSamplingPointId, testCode, alertLimit, actionLimit, specLimit }).then((r) => r.data.data),
  updateSamplingConfiguration: (id: number, testCode: string, alertLimit: string, actionLimit: string, specLimit: string) =>
    apiClient.put(`/masterdata/water-sampling-configurations/${id}`, { testCode, alertLimit, actionLimit, specLimit }).then((r) => r.data.data),
  deleteSamplingConfiguration: (id: number) => apiClient.delete(`/masterdata/water-sampling-configurations/${id}`)
};
```

- [ ] **Step 2: Type-check**

Run: `cd frontend && npx tsc --noEmit`
Expected: no errors from this file (callers are updated in Task 7; if the current `WaterConfigPage.tsx` breaks here, that's expected — Task 7 rewrites it in the same branch).

- [ ] **Step 3: Commit**

```bash
git add frontend/src/modules/laboratoryConfiguration/water/services/WaterConfigService.ts
git commit -m "feat(water): extend WaterConfigService for departments and limit configs"
```

---

## Task 7: Rebuild WaterConfigPage to mirror EMConfigPage

**Files:**
- Rewrite: `frontend/src/modules/laboratoryConfiguration/water/WaterConfigPage.tsx`

**Interfaces:**
- Consumes: `WaterConfigService` (Task 6), `useTestDefinitions` (`frontend/src/hooks/useTestDefinitions.ts`, exposes `options: { code; workflowType }[]`), `TestCodePickerMulti`, `PageHeader`, `SectionTitle`, `ConfirmationDialog`.

The structure mirrors `EMConfigPage.tsx` exactly: a nested `SamplingPointTestConfigSection` (mirror of `RoomTestConfigSection`), then the page with a New Water Department form, a New Sample Location form (with a Department dropdown), and a Departments table expanding to Sample Locations expanding to the limits section.

- [ ] **Step 1: Write the `SamplingPointTestConfigSection` component**

Create it at the top of `WaterConfigPage.tsx` (above the page component). It lists the location's `SamplingConfiguration` rows and offers a limits form whose test dropdown is restricted to the location's assigned **count** tests:

```tsx
import { Fragment, useEffect, useState } from "react";
import {
  Paper, Stack, TextField, Select, MenuItem, Button, Typography, Alert, Box,
  Table, TableHead, TableRow, TableCell, TableBody, IconButton, Collapse
} from "@mui/material";
import EditIcon from "@mui/icons-material/Edit";
import DeleteIcon from "@mui/icons-material/Delete";
import ExpandMoreIcon from "@mui/icons-material/ExpandMore";
import ExpandLessIcon from "@mui/icons-material/ExpandLess";
import { PageHeader } from "../../../components/PageHeader";
import { SectionTitle } from "../../../components/SectionTitle";
import { TestCodePickerMulti } from "../../../components/TestCodePickerMulti";
import { ConfirmationDialog } from "../../../components/ConfirmationDialog";
import { useTestDefinitions } from "../../../hooks/useTestDefinitions";
import { WaterConfigService } from "./services/WaterConfigService";

interface SamplingPoint { id: number; code: string; location: string; assignedTestCodes: string[]; waterDepartmentId: number | null }
interface WaterDept { id: number; name: string; testingFrequency: string; samplingPoints: SamplingPoint[] }

// Per-sample-location limit rows. Only CountTest-typed assigned tests
// (TAMC-Water/TYMC) get Alert/Action/Spec - pathogens are presence/
// absence. Mirrors EMConfigPage's RoomTestConfigSection.
function SamplingPointTestConfigSection({ point }: { point: SamplingPoint }) {
  const { options } = useTestDefinitions();
  const [configs, setConfigs] = useState<any[]>([]);
  const [editingId, setEditingId] = useState<number | null>(null);
  const [form, setForm] = useState<Record<string, any>>({});
  const [pendingDelete, setPendingDelete] = useState<any | null>(null);
  const [error, setError] = useState<string | null>(null);

  const countTestCodes = point.assignedTestCodes.filter(
    (code) => options.find((o) => o.code === code)?.workflowType === "CountTest"
  );

  const load = () => WaterConfigService.getSamplingConfigurations(point.id).then(setConfigs);
  useEffect(() => { load(); /* eslint-disable-next-line react-hooks/exhaustive-deps */ }, [point.id]);

  const setField = (k: string, v: any) => setForm((f) => ({ ...f, [k]: v }));
  const startEdit = (c: any) => {
    setEditingId(c.id);
    setForm({ testCode: c.testCode, alertLimit: c.alertLimit, actionLimit: c.actionLimit, specLimit: c.specLimit });
    setError(null);
  };
  const cancelEdit = () => { setEditingId(null); setForm({}); };

  const save = async () => {
    setError(null);
    if (!form.testCode) { setError("Select a count test."); return; }
    try {
      if (editingId) {
        await WaterConfigService.updateSamplingConfiguration(editingId, form.testCode, form.alertLimit ?? "", form.actionLimit ?? "", form.specLimit ?? "");
      } else {
        await WaterConfigService.createSamplingConfiguration(point.id, form.testCode, form.alertLimit ?? "", form.actionLimit ?? "", form.specLimit ?? "");
      }
      cancelEdit();
      load();
    } catch (e: any) {
      setError(e?.response?.data?.message ?? "Could not save this configuration.");
    }
  };

  const remove = async (id: number) => {
    await WaterConfigService.deleteSamplingConfiguration(id);
    setPendingDelete(null);
    load();
  };

  return (
    <Box sx={{ p: 2, bgcolor: "#faf9fc" }}>
      {error && <Alert severity="error" sx={{ mb: 1.5 }}>{error}</Alert>}
      {configs.length > 0 ? (
        <Table size="small" sx={{ mb: 1.5 }}>
          <TableHead>
            <TableRow><TableCell>Test Code</TableCell><TableCell>Alert</TableCell><TableCell>Action</TableCell><TableCell>Specification</TableCell><TableCell /></TableRow>
          </TableHead>
          <TableBody>
            {configs.map((c) => (
              <TableRow key={c.id}>
                <TableCell>{c.testCode}</TableCell>
                <TableCell>{c.alertLimit || "—"}</TableCell>
                <TableCell>{c.actionLimit || "—"}</TableCell>
                <TableCell>{c.specLimit || "—"}</TableCell>
                <TableCell align="right">
                  <IconButton size="small" onClick={() => startEdit(c)} title="Edit"><EditIcon fontSize="small" /></IconButton>
                  <IconButton size="small" color="error" onClick={() => setPendingDelete(c)} title="Delete"><DeleteIcon fontSize="small" /></IconButton>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      ) : (
        <Typography variant="body2" color="text.secondary" sx={{ mb: 1.5 }}>No limits configured yet for this location.</Typography>
      )}

      {countTestCodes.length === 0 ? (
        <Typography variant="body2" color="text.secondary">Assign a count test (e.g. TAMC-Water) to this location to set Alert/Action/Specification limits.</Typography>
      ) : (
        <>
          <Typography sx={{ fontWeight: 700, fontSize: 13, mb: 1 }}>{editingId ? "Edit Limits" : "Add Limits"}</Typography>
          <Stack direction="row" spacing={1.5} flexWrap="wrap" alignItems="center">
            <Select size="small" displayEmpty value={form.testCode ?? ""} onChange={(e) => setField("testCode", e.target.value)} sx={{ minWidth: 180 }}>
              <MenuItem value=""><em>Count Test</em></MenuItem>
              {countTestCodes.map((code) => <MenuItem key={code} value={code}>{code}</MenuItem>)}
            </Select>
            <TextField size="small" placeholder="Alert" value={form.alertLimit ?? ""} onChange={(e) => setField("alertLimit", e.target.value)} sx={{ width: 100 }} />
            <TextField size="small" placeholder="Action" value={form.actionLimit ?? ""} onChange={(e) => setField("actionLimit", e.target.value)} sx={{ width: 100 }} />
            <TextField size="small" placeholder="Specification" value={form.specLimit ?? ""} onChange={(e) => setField("specLimit", e.target.value)} sx={{ width: 120 }} />
            {editingId && <Button onClick={cancelEdit}>Cancel</Button>}
            <Button variant="contained" onClick={save}>{editingId ? "Save Changes" : "Add"}</Button>
          </Stack>
        </>
      )}

      <ConfirmationDialog
        open={pendingDelete != null}
        message={pendingDelete ? `Delete the ${pendingDelete.testCode} limits for this location?` : ""}
        onCancel={() => setPendingDelete(null)}
        onConfirm={() => pendingDelete && remove(pendingDelete.id)}
      />
    </Box>
  );
}
```

- [ ] **Step 2: Write the page component**

Append the page component to the same file, mirroring `EMConfigPage` (Department + Room forms + expandable table). Reuse the exact state/handler shape from `EMConfigPage.tsx:115-315`, substituting Water terms:

```tsx
export function WaterConfigPage() {
  const [departments, setDepartments] = useState<WaterDept[]>([]);
  const [deptForm, setDeptForm] = useState<Record<string, any>>({});
  const [editingDeptId, setEditingDeptId] = useState<number | null>(null);
  const [pendingDeleteDept, setPendingDeleteDept] = useState<WaterDept | null>(null);

  const [pointForm, setPointForm] = useState<Record<string, any>>({ testCodes: [] });
  const [editingPointId, setEditingPointId] = useState<number | null>(null);
  const [pendingDeletePoint, setPendingDeletePoint] = useState<SamplingPoint | null>(null);

  const [expandedDeptId, setExpandedDeptId] = useState<number | null>(null);
  const [expandedPointId, setExpandedPointId] = useState<number | null>(null);
  const [message, setMessage] = useState<{ text: string; ok: boolean } | null>(null);

  const load = () => WaterConfigService.getWaterDepartments().then(setDepartments).catch(() => setDepartments([]));
  useEffect(() => { load(); }, []);

  const cancelDeptEdit = () => { setEditingDeptId(null); setDeptForm({}); };
  const startDeptEdit = (d: WaterDept) => { setEditingDeptId(d.id); setDeptForm({ name: d.name, frequency: d.testingFrequency }); setMessage(null); };
  const saveDept = async () => {
    setMessage(null);
    try {
      if (editingDeptId) { await WaterConfigService.updateWaterDepartment(editingDeptId, deptForm.name, deptForm.frequency ?? ""); setMessage({ text: "Department updated.", ok: true }); }
      else { await WaterConfigService.createWaterDepartment(deptForm.name, deptForm.frequency ?? ""); setMessage({ text: "Department created.", ok: true }); }
      cancelDeptEdit(); load();
    } catch (e: any) { setMessage({ text: e?.response?.data?.message ?? "Could not save this department.", ok: false }); }
  };
  const deleteDept = async (d: WaterDept) => {
    setMessage(null);
    try { await WaterConfigService.deleteWaterDepartment(d.id); setPendingDeleteDept(null); load(); }
    catch (e: any) { setPendingDeleteDept(null); setMessage({ text: e?.response?.data?.message ?? "Could not delete this department.", ok: false }); }
  };

  const cancelPointEdit = () => { setEditingPointId(null); setPointForm({ testCodes: [] }); };
  const startPointEdit = (p: SamplingPoint) => { setEditingPointId(p.id); setPointForm({ code: p.code, location: p.location, departmentId: p.waterDepartmentId, testCodes: p.assignedTestCodes }); setMessage(null); };
  const savePoint = async () => {
    setMessage(null);
    if (!pointForm.code || !pointForm.departmentId) { setMessage({ text: "Point Code and Department are required.", ok: false }); return; }
    try {
      if (editingPointId) { await WaterConfigService.updateSamplingPoint(editingPointId, pointForm.code, pointForm.location ?? "", pointForm.testCodes ?? [], Number(pointForm.departmentId)); setMessage({ text: "Sample location updated.", ok: true }); }
      else { await WaterConfigService.createSamplingPoint(pointForm.code, pointForm.location ?? "", pointForm.testCodes ?? [], Number(pointForm.departmentId)); setMessage({ text: "Sample location created.", ok: true }); }
      cancelPointEdit(); load();
    } catch (e: any) { setMessage({ text: e?.response?.data?.message ?? "Could not save this sample location.", ok: false }); }
  };
  const deletePoint = async (p: SamplingPoint) => {
    setMessage(null);
    try { await WaterConfigService.deleteSamplingPoint(p.id); setPendingDeletePoint(null); load(); }
    catch (e: any) { setPendingDeletePoint(null); setMessage({ text: e?.response?.data?.message ?? "Could not delete this sample location.", ok: false }); }
  };

  return (
    <>
      <PageHeader title="Water" subtitle="Departments, sample locations, assigned tests, and per-location limits." />
      {message && <Alert severity={message.ok ? "success" : "error"} sx={{ mb: 2 }}>{message.text}</Alert>}

      <SectionTitle>{editingDeptId ? "Edit Department" : "New Department"}</SectionTitle>
      <Paper sx={{ p: 2.5, mb: 3 }}>
        <Stack direction="row" spacing={2} flexWrap="wrap" alignItems="center">
          <TextField size="small" label="Name" value={deptForm.name ?? ""} onChange={(e) => setDeptForm({ ...deptForm, name: e.target.value })} />
          <TextField size="small" label="Testing Frequency" value={deptForm.frequency ?? ""} onChange={(e) => setDeptForm({ ...deptForm, frequency: e.target.value })} placeholder="e.g. Weekly" />
          {editingDeptId && <Button onClick={cancelDeptEdit}>Cancel</Button>}
          <Button variant="outlined" onClick={saveDept}>{editingDeptId ? "Save Changes" : "Add Department"}</Button>
        </Stack>
      </Paper>

      <SectionTitle>{editingPointId ? "Edit Sample Location" : "New Sample Location"}</SectionTitle>
      <Paper sx={{ p: 2.5, mb: 3 }}>
        <Stack direction="row" spacing={2} flexWrap="wrap" alignItems="center">
          <TextField size="small" label="Point Code" value={pointForm.code ?? ""} onChange={(e) => setPointForm({ ...pointForm, code: e.target.value })} />
          <TextField size="small" label="Location" value={pointForm.location ?? ""} onChange={(e) => setPointForm({ ...pointForm, location: e.target.value })} />
          <Select size="small" displayEmpty value={pointForm.departmentId ?? ""} onChange={(e) => setPointForm({ ...pointForm, departmentId: e.target.value })} sx={{ minWidth: 180 }}>
            <MenuItem value=""><em>Department</em></MenuItem>
            {departments.map((d) => <MenuItem key={d.id} value={d.id}>{d.name}</MenuItem>)}
          </Select>
          <TestCodePickerMulti value={pointForm.testCodes ?? []} onChange={(codes) => setPointForm({ ...pointForm, testCodes: codes })} label="Assigned Tests" sx={{ minWidth: 280 }} />
          {editingPointId && <Button onClick={cancelPointEdit}>Cancel</Button>}
          <Button variant="outlined" onClick={savePoint}>{editingPointId ? "Save Changes" : "Add Sample Location"}</Button>
        </Stack>
      </Paper>

      <SectionTitle>Departments</SectionTitle>
      <Paper sx={{ p: 2.5 }}>
        <Table>
          <TableHead><TableRow><TableCell sx={{ width: 40 }} /><TableCell>Department</TableCell><TableCell>Testing Frequency</TableCell><TableCell /></TableRow></TableHead>
          <TableBody>
            {departments.map((d) => (
              <Fragment key={d.id}>
                <TableRow>
                  <TableCell>
                    <IconButton size="small" onClick={() => setExpandedDeptId(expandedDeptId === d.id ? null : d.id)} title="Sample Locations">
                      {expandedDeptId === d.id ? <ExpandLessIcon fontSize="small" /> : <ExpandMoreIcon fontSize="small" />}
                    </IconButton>
                  </TableCell>
                  <TableCell>{d.name}</TableCell>
                  <TableCell>{d.testingFrequency || "—"}</TableCell>
                  <TableCell align="right">
                    <IconButton size="small" onClick={() => startDeptEdit(d)} title="Edit"><EditIcon fontSize="small" /></IconButton>
                    <IconButton size="small" color="error" onClick={() => setPendingDeleteDept(d)} title="Delete"><DeleteIcon fontSize="small" /></IconButton>
                  </TableCell>
                </TableRow>
                <TableRow>
                  <TableCell sx={{ p: 0, border: 0 }} colSpan={4}>
                    <Collapse in={expandedDeptId === d.id} unmountOnExit>
                      <Box sx={{ p: 2, bgcolor: "#f5f3fa" }}>
                        {(d.samplingPoints ?? []).length === 0 ? (
                          <Typography variant="body2" color="text.secondary">No sample locations yet.</Typography>
                        ) : (
                          <Table size="small">
                            <TableHead><TableRow><TableCell sx={{ width: 40 }} /><TableCell>Location Code</TableCell><TableCell>Location</TableCell><TableCell>Assigned Tests</TableCell><TableCell /></TableRow></TableHead>
                            <TableBody>
                              {(d.samplingPoints ?? []).map((p) => (
                                <Fragment key={p.id}>
                                  <TableRow>
                                    <TableCell>
                                      <IconButton size="small" onClick={() => setExpandedPointId(expandedPointId === p.id ? null : p.id)} title="Limits">
                                        {expandedPointId === p.id ? <ExpandLessIcon fontSize="small" /> : <ExpandMoreIcon fontSize="small" />}
                                      </IconButton>
                                    </TableCell>
                                    <TableCell>{p.code}</TableCell>
                                    <TableCell>{p.location || "—"}</TableCell>
                                    <TableCell>{(p.assignedTestCodes ?? []).join(", ") || "—"}</TableCell>
                                    <TableCell align="right">
                                      <IconButton size="small" onClick={() => startPointEdit(p)} title="Edit"><EditIcon fontSize="small" /></IconButton>
                                      <IconButton size="small" color="error" onClick={() => setPendingDeletePoint(p)} title="Delete"><DeleteIcon fontSize="small" /></IconButton>
                                    </TableCell>
                                  </TableRow>
                                  <TableRow>
                                    <TableCell sx={{ p: 0, border: 0 }} colSpan={5}>
                                      <Collapse in={expandedPointId === p.id} unmountOnExit>
                                        <SamplingPointTestConfigSection point={p} />
                                      </Collapse>
                                    </TableCell>
                                  </TableRow>
                                </Fragment>
                              ))}
                            </TableBody>
                          </Table>
                        )}
                      </Box>
                    </Collapse>
                  </TableCell>
                </TableRow>
              </Fragment>
            ))}
          </TableBody>
        </Table>
      </Paper>

      <ConfirmationDialog
        open={pendingDeleteDept != null}
        message={pendingDeleteDept ? `Delete department "${pendingDeleteDept.name}"? This cannot be undone.` : ""}
        onCancel={() => setPendingDeleteDept(null)}
        onConfirm={() => pendingDeleteDept && deleteDept(pendingDeleteDept)}
      />
      <ConfirmationDialog
        open={pendingDeletePoint != null}
        message={pendingDeletePoint ? `Delete sample location "${pendingDeletePoint.code}"? This cannot be undone.` : ""}
        onCancel={() => setPendingDeletePoint(null)}
        onConfirm={() => pendingDeletePoint && deletePoint(pendingDeletePoint)}
      />
    </>
  );
}
```

- [ ] **Step 3: Type-check**

Run: `cd frontend && npx tsc --noEmit`
Expected: no errors.

- [ ] **Step 4: Build**

Run: `cd frontend && npm run build`
Expected: build succeeds.

- [ ] **Step 5: Commit**

```bash
git add frontend/src/modules/laboratoryConfiguration/water/WaterConfigPage.tsx
git commit -m "feat(water): rebuild Water config page as Department -> Location -> limits"
```

---

## Task 8: End-to-end verification in the running app

**Files:** none (manual verification via the browser preview).

- [ ] **Step 1: Run the full backend test suite**

Run: `dotnet test backend/MicroLIMS.sln`
Expected: all tests pass, including `WaterConfigCrudTests` and `WaterAndEMEngineTests`.

- [ ] **Step 2: Start the app and exercise the Water page**

Start backend + frontend per the repo's usual dev commands. In the browser (Laboratory Configuration → Water):
1. Confirm the 6 existing points (SWT, SP103–106, SP205) appear under the seeded "Water" department.
2. Create a new department (Name + Testing Frequency); confirm it lists.
3. Create a sample location under it with TAMC-Water assigned; expand it; confirm the limits form offers only TAMC-Water (no pathogens) and save Alert/Action/Spec.
4. Confirm a pathogen-only location shows the "assign a count test" hint and no limit fields.
5. Delete guard: attempt to delete a department that still has locations; confirm the clear error message.

- [ ] **Step 3: Confirm limits reach the engine**

Receive a water sample at the configured location, enter a count reading above the Action limit, and confirm the result status is `ActionLimitExceeded` (not `WithinLimits`) — proving the config now feeds `WaterWorkflowEngine`.

- [ ] **Step 4: Final commit if any fixups were needed**

```bash
git add -A
git commit -m "chore(water): verification fixups"
```

---

## Self-Review Notes

- **Spec coverage:** WaterDepartment entity + fields (T1); FK preserving point Id (T1); default-department migration seed (T1); water-departments CRUD + delete guard (T2); department id on locations (T3); water-sampling-configurations CRUD (T4); engine unchanged, limits now evaluated (T5); count-test-only limits via WorkflowType (T7); frontend service + page mirror (T6/T7); verification (T8). All spec sections mapped.
- **Type consistency:** `WaterDepartmentId` (int?) used identically across entity, records, service (`waterDepartmentId: number`), and page (`Number(pointForm.departmentId)`). `SamplingConfiguration` fields (`AlertLimit/ActionLimit/SpecLimit`, `TestCode`, `WaterSamplingPointId`) reused verbatim. `workflowType === "CountTest"` matches the `WorkflowType` enum name serialized by the API.
- **Note for executor:** this repo's environment reports it is *not* a git repository. If `git` commands fail, skip the commit steps and treat each task boundary as a checkpoint instead.
