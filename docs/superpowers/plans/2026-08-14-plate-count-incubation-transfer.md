# Two-Stage Incubation Transfer for PlateCount Steps — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add an opt-in second incubation stage to `PlateCount` workflow steps (used by After Cleaning / EM TAMC, and by any other PlateCount step a lab configures this way), so a plate can be read after being transferred to a second incubator/temperature/window before the count is recorded — entirely backend (domain, engine, controller, DTOs). No frontend files are touched.

**Architecture:** `TestWorkflowStep` gains a `RequiresIncubationTransfer` flag. Stage-2's temperature/hour window lives in a new child table `TestWorkflowStepIncubationStage`, keyed by `(TestWorkflowStepId, StageNumber)`, extensible to a stage 3 without a schema change. At runtime, stage 2 is a **new** `Incubation` row (`StageNumber = 2`, `ParentIncubationId` pointing at stage 1's row) created by a new engine method `StartStage2IncubationAsync` — this call itself *is* the transfer; it closes stage 1 and copies `MediaId` forward automatically. The existing single-value (`RecordResultAsync`) and batch (`RecordBatchResultsAsync`, used by EM/After Cleaning) result-recording paths are both gated: for a transfer-enabled step, they refuse to accept a count until stage 2 exists and its window has elapsed. Every other `PlateCount` step (`RequiresIncubationTransfer = false`) is provably untouched — all new logic is behind that flag.

**Tech Stack:** ASP.NET Core / EF Core (PostgreSQL provider in prod, EF InMemory in tests) / xUnit.

**Spec:** This plan *is* the spec — written directly from the user's Part 0–6 brief (two-stage incubation transfer for PlateCount/EM/After Cleaning TAMC steps) plus live codebase discovery. No separate spec doc exists; discovery findings are folded into each task below.

## Global Constraints

- Backend only. Do not create, edit, or reference any frontend file.
- Do not change behavior for any `TestWorkflowStep` with `RequiresIncubationTransfer = false`, or for any step whose `StepType != PlateCount`. Every new code path in the engine must be reached only through an explicit `RequiresIncubationTransfer` (or `RequiresIncubationTransfer`-derived) check.
- Do not add a separate transfer-confirmation entity or endpoint. Starting stage 2's `Incubation` row is the only "transfer" action there is.
- Follow existing patterns exactly: `WorkflowStepException(ErrorCode, message, remainingSeconds?)` for machine-readable failures a frontend would switch on; plain `InvalidOperationException` for "this can't happen given valid input" guards (matches existing `SelectMediaAsync`/`RecordBatchResultsAsync` style).
- EF configuration classes are auto-discovered (`ApplyConfigurationsFromAssembly` — confirmed by every existing `*Configuration.cs` needing no manual registration), so new `IEntityTypeConfiguration<T>` classes need no wiring beyond creating the file.
- Migration command (confirmed from `MicroLIMS/backend/README.md`):
  `dotnet ef migrations add <Name> --project backend/MicroLIMS.Persistence --startup-project backend/MicroLIMS.API`
- Money/temperature columns use `decimal(5,2)`; hour fields are plain `int` — matches `TestWorkflowStep`/`TestWorkflowStepMedia` today.

## Key Discovery Findings (carried from Part 0)

1. `TestWorkflowStep` (`MicroLIMS.Domain/Entities/TestWorkflowStep.cs`) already has `StepType`, `TemperatureMin/Max` (decimal), `IncubationMinHours/MaxHours` (int) post-refactor. No multi-stage concept exists on it today.
2. `Incubation` (`MicroLIMS.Domain/Entities/Incubation.cs`) is created in exactly one place for `PlateCount`: `TestWorkflowEngine.SelectMediaAsync` (`TestWorkflowEngine.cs:314-383`). This single call site serves both regular product/water TAMC/TYMC **and** EM/After Cleaning TAMC — there is no separate code path.
3. `TestWorkflowEngine` is a deliberately generic step-runner; nothing branches on sample category by code, only by data (`SampleLocation` presence). Regular samples record through `RecordResultAsync` → `RecordCountTestAsync` (writes one `CountTestReading`); EM/After Cleaning record through `RecordBatchResultsAsync` (writes per-location `SampleLocation` fields, no `CountTestReading`). **Both paths need the stage-gating logic**, since the same `RequiresIncubationTransfer` flag on a shared step template could be hit by either, and EM/After Cleaning TAMC — the case named in the request — always goes through the batch path.
4. `Incubation` has **no** "who started it" field today (`StartedByUserId` does not exist). `CountTestReading.EnteredByUserId` / `SampleLocation.EnteredByUserId` already correctly capture who recorded the final count, and are distinct entities from `Incubation` — not conflated. This plan adds `Incubation.StartedByUserId` since the feature requires it and it does not exist yet.
5. Existing gating patterns: `RequireIncubationComplete(DateTime incubationEndUtc)` (`TestWorkflowEngine.cs:886-892`) throws `WorkflowStepException(WorkflowErrorCodes.IncubationNotComplete, …, remainingSeconds)` — this is the pattern Part 4 asks to reuse. `RequireValidIncubationWindow(TestWorkflowStep step, start, end)` (`TestWorkflowEngine.cs:904-915`) validates an analyst-declared window (start ≤ end, duration ≥ template minimum); it currently only takes a full `TestWorkflowStep`, so this plan adds a `(string stepName, int minHours, start, end)` overload the original delegates to, so stage 2 (which has no `TestWorkflowStep` row of its own — its minimum lives on `TestWorkflowStepIncubationStage`) can reuse the exact same validation.
6. The **existing** multi-window mechanism (`CloseCurrentIncubationWindowAsync` + calling `SelectMediaAsync` again) is a *different, orthogonal* feature: it chains multiple separate `TestWorkflowStep` template rows (each producing its own `CountTestReading`/close event), proven by `CountTestWorkflowTests.RecordResultAsync_MultiStepTemplate_DoesNotSkipSecondStep`. This plan's stage-2 mechanism is scoped to a *single* step and produces exactly one `CountTestReading`/batch result after both stages — it does not reuse or modify `CloseCurrentIncubationWindowAsync`.
7. `TestWorkflowController.cs` (`api/test-workflow`) already has the exact endpoint shape to mirror for a new "start stage 2" action: `select-media` (245-254) and `close-incubation-window` (304-309) both call a one-line engine method and return a small anonymous projection. `RunAsync<T>` (70-83) already converts a thrown `WorkflowStepException` into `400` with `ErrorCode` + `remainingSeconds`.

---

### Task 1: Domain entities, EF configuration, and migration

**Files:**
- Modify: `backend/MicroLIMS.Domain/Entities/TestWorkflowStep.cs`
- Modify: `backend/MicroLIMS.Domain/Entities/Incubation.cs`
- Create: `backend/MicroLIMS.Domain/Entities/TestWorkflowStepIncubationStage.cs`
- Create: `backend/MicroLIMS.Persistence/Configurations/TestWorkflowStepIncubationStageConfiguration.cs`
- Modify: `backend/MicroLIMS.Persistence/Configurations/TestWorkflowStepConfiguration.cs`
- Modify: `backend/MicroLIMS.Persistence/Configurations/IncubationConfiguration.cs`
- Modify: `backend/MicroLIMS.Persistence/DbContext/MicroLimsDbContext.cs`
- Create (generated): a new EF migration under `backend/MicroLIMS.Persistence/Migrations/`

**Interfaces:**
- Produces: `TestWorkflowStep.RequiresIncubationTransfer` (bool), `TestWorkflowStep.IncubationStages` (`List<TestWorkflowStepIncubationStage>`); `TestWorkflowStepIncubationStage` entity with `Id, TestWorkflowStepId, StageNumber, TempMin, TempMax, IncubationMinHours, IncubationMaxHours`; `Incubation.StartedByUserId` (`int?`), `Incubation.ParentIncubationId` (`int?`), `Incubation.ParentIncubation` (nav), `Incubation.StageNumber` (`int`, default `1`). All later tasks depend on these exact names/types.

- [ ] **Step 1: Add `RequiresIncubationTransfer` and the `IncubationStages` navigation to `TestWorkflowStep`**

Edit `backend/MicroLIMS.Domain/Entities/TestWorkflowStep.cs`:

```csharp
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

public class TestWorkflowStep
{
    public int Id { get; set; }
    public int TestDefinitionId { get; set; }
    public TestDefinition? TestDefinition { get; set; }

    public int StepOrder { get; set; }
    public string StepName { get; set; } = string.Empty;

    public int MediaTypeId { get; set; }
    public MediaType? MediaType { get; set; }

    public int IncubationMinHours { get; set; }
    public int IncubationMaxHours { get; set; }
    public decimal TemperatureMin { get; set; }
    public decimal TemperatureMax { get; set; }

    public bool IsFinalStep { get; set; }

    public StepType StepType { get; set; }

    // Required for SelectivePlating and ConfirmatoryPlating; null otherwise.
    public int? TargetOrganismId { get; set; }
    public Organism? TargetOrganism { get; set; }

    public List<TestWorkflowStepMedia> StepMedia { get; set; } = new();

    // PlateCount only. When true, the step's own TemperatureMin/Max and
    // IncubationMinHours/MaxHours above describe stage 1; stage 2's window
    // lives in IncubationStages (StageNumber == 2). See
    // WorkflowTemplateValidator rule 7 for the "can't half-configure this"
    // guard.
    public bool RequiresIncubationTransfer { get; set; }

    // Stage 2+ incubation windows for a transfer-enabled PlateCount step.
    // Stage 1's window is NOT duplicated here - it stays on this row's own
    // TemperatureMin/Max/IncubationMinHours/MaxHours, since dozens of
    // existing call sites already read those directly. Keyed by
    // StageNumber so a stage 3 can be added later with no schema change.
    public List<TestWorkflowStepIncubationStage> IncubationStages { get; set; } = new();

    public bool RequiresTargetOrganism =>
        StepType is StepType.SelectivePlating or StepType.ConfirmatoryPlating;

    // BiochemicalTest is bench work with no incubation window, and
    // SelectivePlating is read off plates the previous step incubated.
    public bool RequiresIncubationLock =>
        StepType is StepType.BrothEnrichment or StepType.SelectiveBroth or StepType.ConfirmatoryPlating;
}
```

- [ ] **Step 2: Create the `TestWorkflowStepIncubationStage` entity**

Create `backend/MicroLIMS.Domain/Entities/TestWorkflowStepIncubationStage.cs`:

```csharp
namespace MicroLIMS.Domain.Entities;

// Stage 2+ (StageNumber >= 2) of a PlateCount step's incubation window,
// only meaningful when the owning TestWorkflowStep.RequiresIncubationTransfer
// is true. Stage 1 stays on TestWorkflowStep itself - see that entity's
// comment. Keyed by (TestWorkflowStepId, StageNumber) so a third stage can
// be added later without a schema change, even though only StageNumber == 2
// is used today.
public class TestWorkflowStepIncubationStage
{
    public int Id { get; set; }

    public int TestWorkflowStepId { get; set; }
    public TestWorkflowStep? TestWorkflowStep { get; set; }

    public int StageNumber { get; set; }

    public decimal TempMin { get; set; }
    public decimal TempMax { get; set; }
    public int IncubationMinHours { get; set; }
    public int IncubationMaxHours { get; set; }
}
```

- [ ] **Step 3: Add `StartedByUserId`, `ParentIncubationId`, `StageNumber` to `Incubation`**

Edit `backend/MicroLIMS.Domain/Entities/Incubation.cs`:

```csharp
using System.ComponentModel.DataAnnotations.Schema;

namespace MicroLIMS.Domain.Entities;

public class Incubation
{
    public int Id { get; set; }
    public int? TestOrderId { get; set; }
    public TestOrder? TestOrder { get; set; }
    public int StepNumber { get; set; }
    public string StepName { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public string? Outcome { get; set; }

    public int? MediaId { get; set; }
    public Media? Media { get; set; }

    public int? IncubatorEquipmentId { get; set; }
    public Equipment? IncubatorEquipment { get; set; }
    public string? Temperature { get; set; }
    public string? Duration { get; set; }
    public DateTime? ExpectedReadingAt { get; set; }

    // Analyst-declared incubation window. The lock in Task 8 is
    // enforced against IncubationEndUtc.
    public DateTime? IncubationStartUtc { get; set; }
    public DateTime? IncubationEndUtc { get; set; }

    // Server clock reading taken when the declared window above was
    // received. The window is analyst-supplied and therefore a claim;
    // this is the one timestamp on the row the analyst cannot influence,
    // so a reviewer can always see what was claimed AND when it was
    // actually submitted (ALCOA+ Contemporaneous/Attributable). Never
    // used to gate anything - it is evidence, not a control.
    public DateTime? WindowReceivedAtUtc { get; set; }

    // Who started this incubation window (selected media/lot and
    // incubator, or - for StageNumber 2 - performed the transfer). Not
    // the same person as whoever later records the count: see
    // CountTestReading.EnteredByUserId / SampleLocation.EnteredByUserId.
    public int? StartedByUserId { get; set; }

    // Set only on a StageNumber == 2 (or higher) row: the stage 1 row this
    // one continues from. The physical plate does not change between
    // stages, so MediaId is copied from the parent, never reselected.
    public int? ParentIncubationId { get; set; }
    public Incubation? ParentIncubation { get; set; }

    // 1 for every incubation window that isn't part of a transfer. A
    // transfer-enabled PlateCount step's stage 2 is a NEW row with
    // StageNumber == 2 and ParentIncubationId pointing at stage 1 - never
    // a mutation of the stage 1 row.
    public int StageNumber { get; set; } = 1;

    [NotMapped]
    public bool IsIncubationComplete =>
        IncubationEndUtc.HasValue && DateTime.UtcNow >= IncubationEndUtc.Value;
}
```

- [ ] **Step 4: EF configuration for the new table and columns**

Create `backend/MicroLIMS.Persistence/Configurations/TestWorkflowStepIncubationStageConfiguration.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class TestWorkflowStepIncubationStageConfiguration : IEntityTypeConfiguration<TestWorkflowStepIncubationStage>
{
    public void Configure(EntityTypeBuilder<TestWorkflowStepIncubationStage> builder)
    {
        builder.HasKey(s => s.Id);
        builder.Property(s => s.TempMin).HasColumnType("decimal(5,2)");
        builder.Property(s => s.TempMax).HasColumnType("decimal(5,2)");

        // One row per stage number per step - "stage 2 defined twice" is
        // a config error, not a valid multi-row state.
        builder.HasIndex(s => new { s.TestWorkflowStepId, s.StageNumber }).IsUnique();

        builder.HasOne(s => s.TestWorkflowStep)
            .WithMany(t => t.IncubationStages)
            .HasForeignKey(s => s.TestWorkflowStepId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
```

Edit `backend/MicroLIMS.Persistence/Configurations/IncubationConfiguration.cs` — add the self-referencing FK with `Restrict` (matches every other FK on this entity):

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class IncubationConfiguration : IEntityTypeConfiguration<Incubation>
{
    public void Configure(EntityTypeBuilder<Incubation> builder)
    {
        builder.HasKey(i => i.Id);

        builder.HasOne(i => i.Media)
            .WithMany()
            .HasForeignKey(i => i.MediaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.IncubatorEquipment)
            .WithMany()
            .HasForeignKey(i => i.IncubatorEquipmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.ParentIncubation)
            .WithMany()
            .HasForeignKey(i => i.ParentIncubationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Ignore(i => i.IsIncubationComplete);
    }
}
```

`TestWorkflowStepConfiguration.cs` needs no edit — `RequiresIncubationTransfer` is a plain `bool` column EF maps by convention, same as `IsFinalStep`.

- [ ] **Step 5: Register the new `DbSet`**

Edit `backend/MicroLIMS.Persistence/DbContext/MicroLimsDbContext.cs` — near the existing `TestWorkflowStep`/`TestWorkflowStepMedia` sets (around line 78-79), add:

```csharp
    public DbSet<TestWorkflowStepIncubationStage> TestWorkflowStepIncubationStages => Set<TestWorkflowStepIncubationStage>();
```

- [ ] **Step 6: Generate and inspect the migration**

Run from the repo root:

```bash
dotnet ef migrations add AddIncubationTransferStages --project backend/MicroLIMS.Persistence --startup-project backend/MicroLIMS.API
```

Open the generated `*_AddIncubationTransferStages.cs` and confirm it contains exactly:
- `AddColumn` for `TestWorkflowSteps.RequiresIncubationTransfer` (bool, default `false`)
- `AddColumn` for `Incubations.StartedByUserId` (int, nullable)
- `AddColumn` for `Incubations.ParentIncubationId` (int, nullable) + its FK (`Restrict`) + index
- `AddColumn` for `Incubations.StageNumber` (int, default `1`)
- `CreateTable` for `TestWorkflowStepIncubationStages` with the FK to `TestWorkflowSteps` (`Cascade`) and the unique index on `(TestWorkflowStepId, StageNumber)`

If it contains anything touching an unrelated table, stop and re-check Steps 1-5 for an accidental edit.

- [ ] **Step 7: Build and run the full existing test suite to confirm nothing broke**

```bash
dotnet build backend/MicroLIMS.sln
dotnet test backend/MicroLIMS.Tests/MicroLIMS.Tests.csproj
```

Expected: builds clean, all existing tests still pass (the new columns are all nullable/defaulted, so no existing seed data or test fixture needs updating).

- [ ] **Step 8: Commit**

```bash
git add backend/MicroLIMS.Domain/Entities/TestWorkflowStep.cs backend/MicroLIMS.Domain/Entities/Incubation.cs backend/MicroLIMS.Domain/Entities/TestWorkflowStepIncubationStage.cs backend/MicroLIMS.Persistence/Configurations/TestWorkflowStepIncubationStageConfiguration.cs backend/MicroLIMS.Persistence/Configurations/IncubationConfiguration.cs backend/MicroLIMS.Persistence/DbContext/MicroLimsDbContext.cs backend/MicroLIMS.Persistence/Migrations/
git commit -m "feat: add two-stage incubation transfer schema for PlateCount steps"
```

---

### Task 2: `WorkflowTemplateValidator` rule 7 — cannot save half-configured transfer

**Files:**
- Modify: `backend/MicroLIMS.Application/Services/WorkflowTemplateValidator.cs`
- Modify: `backend/MicroLIMS.Tests/WorkflowTests/WorkflowTemplateValidationTests.cs`

**Interfaces:**
- Consumes: `TestWorkflowStep.RequiresIncubationTransfer`, `TestWorkflowStep.IncubationStages` (Task 1).
- Produces: `WorkflowTemplateValidator.Validate(step)` now also returns rule-7 errors. `MasterDataController.ValidateStepRulesAsync` (Task 3) calls this unchanged — it already rethrows every returned error.

- [ ] **Step 1: Write the failing tests**

Add to `backend/MicroLIMS.Tests/WorkflowTests/WorkflowTemplateValidationTests.cs` (same file, same `Step`/`Medium` helpers already defined there):

```csharp
    private static TestWorkflowStepIncubationStage Stage2(decimal tempMin = 30, decimal tempMax = 35, int minHours = 24, int maxHours = 48) =>
        new() { StageNumber = 2, TempMin = tempMin, TempMax = tempMax, IncubationMinHours = minHours, IncubationMaxHours = maxHours };

    [Fact]
    public void Rule7_TransferEnabledPlateCount_WithStage2Configured_IsValid()
    {
        var step = Step(StepType.PlateCount, null);
        step.RequiresIncubationTransfer = true;
        step.IncubationStages.Add(Stage2());

        var errors = WorkflowTemplateValidator.Validate(step);
        Assert.Empty(errors);
    }

    [Fact]
    public void Rule7_TransferEnabledPlateCount_WithNoStage2_FailsRule7()
    {
        var step = Step(StepType.PlateCount, null);
        step.RequiresIncubationTransfer = true;

        var errors = WorkflowTemplateValidator.Validate(step);
        Assert.Contains(errors, e => e.RuleNumber == 7);
    }

    [Fact]
    public void Rule7_TransferEnabledPlateCount_WithInvertedStage2Temperature_FailsRule7()
    {
        var step = Step(StepType.PlateCount, null);
        step.RequiresIncubationTransfer = true;
        step.IncubationStages.Add(Stage2(tempMin: 40, tempMax: 30));

        var errors = WorkflowTemplateValidator.Validate(step);
        Assert.Contains(errors, e => e.RuleNumber == 7);
    }

    [Fact]
    public void Rule7_TransferEnabledPlateCount_WithZeroMinHours_FailsRule7()
    {
        var step = Step(StepType.PlateCount, null);
        step.RequiresIncubationTransfer = true;
        step.IncubationStages.Add(Stage2(minHours: 0, maxHours: 24));

        var errors = WorkflowTemplateValidator.Validate(step);
        Assert.Contains(errors, e => e.RuleNumber == 7);
    }

    [Fact]
    public void Rule7_NonTransferPlateCount_WithStage2Defined_FailsRule7()
    {
        var step = Step(StepType.PlateCount, null);
        step.RequiresIncubationTransfer = false;
        step.IncubationStages.Add(Stage2());

        var errors = WorkflowTemplateValidator.Validate(step);
        Assert.Contains(errors, e => e.RuleNumber == 7);
    }

    [Fact]
    public void Rule7_NonTransferPlateCount_WithNoStage2_IsValid()
    {
        var step = Step(StepType.PlateCount, null);
        var errors = WorkflowTemplateValidator.Validate(step);
        Assert.Empty(errors);
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

```bash
dotnet test backend/MicroLIMS.Tests/MicroLIMS.Tests.csproj --filter "Rule7"
```

Expected: `Rule7_TransferEnabledPlateCount_WithStage2Configured_IsValid` fails (rule 7 doesn't exist yet, but this one has no wrong state to flag — check it actually compiles; the "FailsRule7" tests fail because `errors` is empty).

- [ ] **Step 3: Implement rule 7**

Edit `backend/MicroLIMS.Application/Services/WorkflowTemplateValidator.cs` — add before the final `return errors;`:

```csharp
        if (step.StepType == StepType.PlateCount && step.RequiresIncubationTransfer)
        {
            var stage2 = step.IncubationStages.FirstOrDefault(s => s.StageNumber == 2);
            if (stage2 is null)
            {
                Fail(7, "A step requiring incubation transfer must define stage 2's temperature and incubation-hours range.");
            }
            else
            {
                if (stage2.TempMin >= stage2.TempMax)
                    Fail(7, "Stage 2's minimum temperature must be below its maximum.");
                if (stage2.IncubationMinHours <= 0 || stage2.IncubationMaxHours < stage2.IncubationMinHours)
                    Fail(7, "Stage 2's incubation-hours range must have a positive minimum and a maximum no less than the minimum.");
            }
        }
        else if (step.IncubationStages.Count > 0)
        {
            Fail(7, "Only a PlateCount step with incubation transfer enabled may define a second incubation stage.");
        }
```

(`Fail` is the local function already defined at the top of `Validate` — no new parameter needed.)

- [ ] **Step 4: Run the tests to verify they pass**

```bash
dotnet test backend/MicroLIMS.Tests/MicroLIMS.Tests.csproj --filter "Rule7"
```

Expected: all 6 pass.

- [ ] **Step 5: Run the full validator test file to confirm rules 1-6 are untouched**

```bash
dotnet test backend/MicroLIMS.Tests/MicroLIMS.Tests.csproj --filter "WorkflowTemplateValidationTests"
```

Expected: all pass.

- [ ] **Step 6: Commit**

```bash
git add backend/MicroLIMS.Application/Services/WorkflowTemplateValidator.cs backend/MicroLIMS.Tests/WorkflowTests/WorkflowTemplateValidationTests.cs
git commit -m "feat: validate stage-2 config is complete when incubation transfer is enabled"
```

---

### Task 3: `MasterDataController` — expose the field end to end on the Test Master step editor backend surface

**Files:**
- Modify: `backend/MicroLIMS.API/Controllers/MasterDataController.cs`

**Interfaces:**
- Consumes: `TestWorkflowStep.RequiresIncubationTransfer`/`IncubationStages` (Task 1), `WorkflowTemplateValidator.Validate` rule 7 (Task 2).
- Produces: `CreateTestWorkflowStepRequest`/`UpdateTestWorkflowStepRequest` now carry `RequiresIncubationTransfer` and `IncubationStages`; `GET test-definitions/{id}/steps` now returns both. This is the "frontend wiring is separate but the field exists end to end on this side" surface the brief asks for.

- [ ] **Step 1: Add the request DTO fields**

Edit the record definitions near line 41-44 of `backend/MicroLIMS.API/Controllers/MasterDataController.cs`:

```csharp
public record StepMediaRequest(int MaterialId, decimal TempMin, decimal TempMax, bool IsRequired, int DisplayOrder);
public record IncubationStageRequest(int StageNumber, decimal TempMin, decimal TempMax, int IncubationMinHours, int IncubationMaxHours);
public record CreateTestWorkflowStepRequest(string StepName, int MediaTypeId, int IncubationMinHours, int IncubationMaxHours, decimal TemperatureMin, decimal TemperatureMax, bool IsFinalStep, StepType StepType, int? TargetOrganismId, List<StepMediaRequest> StepMedia, bool RequiresIncubationTransfer, List<IncubationStageRequest>? IncubationStages);
public record UpdateTestWorkflowStepRequest(string StepName, int MediaTypeId, int IncubationMinHours, int IncubationMaxHours, decimal TemperatureMin, decimal TemperatureMax, bool IsFinalStep, StepType StepType, int? TargetOrganismId, List<StepMediaRequest> StepMedia, bool RequiresIncubationTransfer, List<IncubationStageRequest>? IncubationStages);
public record MoveTestWorkflowStepRequest(string Direction);
```

- [ ] **Step 2: Include `IncubationStages` in the GET projection**

Edit `GetTestWorkflowSteps` (around line 811-834):

```csharp
    [HttpGet("test-definitions/{id}/steps")]
    public async Task<IActionResult> GetTestWorkflowSteps(int id) =>
        Ok(ApiResponse<object>.Ok(await _db.TestWorkflowSteps
            .Include(s => s.MediaType)
            .Include(s => s.TargetOrganism)
            .Include(s => s.StepMedia).ThenInclude(m => m.Material)
            .Include(s => s.IncubationStages)
            .Where(s => s.TestDefinitionId == id)
            .OrderBy(s => s.StepOrder)
            .Select(s => new
            {
                s.Id, s.StepOrder, s.StepName, s.MediaTypeId,
                mediaType = s.MediaType == null ? null : new { s.MediaType.Id, s.MediaType.Class },
                s.IncubationMinHours, s.IncubationMaxHours, s.TemperatureMin, s.TemperatureMax,
                s.IsFinalStep,
                stepType = s.StepType.ToString(),
                s.TargetOrganismId,
                targetOrganism = s.TargetOrganism == null ? null : new { s.TargetOrganism.Id, name = s.TargetOrganism.ScientificName },
                stepMedia = s.StepMedia.OrderBy(m => m.DisplayOrder).Select(m => new
                {
                    stepMediaId = m.Id, m.MaterialId, materialName = m.Material!.MaterialName,
                    m.TempMin, m.TempMax, m.IsRequired, m.DisplayOrder
                }),
                s.RequiresIncubationTransfer,
                incubationStages = s.IncubationStages.OrderBy(x => x.StageNumber).Select(x => new
                {
                    x.StageNumber, x.TempMin, x.TempMax, x.IncubationMinHours, x.IncubationMaxHours
                })
            })
            .ToListAsync()));
```

- [ ] **Step 3: Populate the fields on create**

Edit `CreateTestWorkflowStep` (around line 869-898):

```csharp
    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpPost("test-definitions/{id}/steps")]
    public async Task<IActionResult> CreateTestWorkflowStep(int id, CreateTestWorkflowStepRequest request)
    {
        _ = await _db.TestDefinitions.FirstOrDefaultAsync(t => t.Id == id)
            ?? throw new InvalidOperationException($"Test {id} not found.");

        var nextOrder = 1 + await _db.TestWorkflowSteps.Where(s => s.TestDefinitionId == id)
            .Select(s => (int?)s.StepOrder).MaxAsync() ?? 1;

        var entity = new TestWorkflowStep
        {
            TestDefinitionId = id, StepOrder = nextOrder, StepName = request.StepName, MediaTypeId = request.MediaTypeId,
            IncubationMinHours = request.IncubationMinHours, IncubationMaxHours = request.IncubationMaxHours,
            TemperatureMin = request.TemperatureMin, TemperatureMax = request.TemperatureMax,
            IsFinalStep = request.IsFinalStep, StepType = request.StepType, TargetOrganismId = request.TargetOrganismId,
            RequiresIncubationTransfer = request.RequiresIncubationTransfer
        };
        entity.StepMedia.AddRange(request.StepMedia.Select(m => new TestWorkflowStepMedia
        {
            MaterialId = m.MaterialId, TempMin = m.TempMin, TempMax = m.TempMax,
            IsRequired = m.IsRequired, DisplayOrder = m.DisplayOrder
        }));
        entity.IncubationStages.AddRange((request.IncubationStages ?? new()).Select(s => new TestWorkflowStepIncubationStage
        {
            StageNumber = s.StageNumber, TempMin = s.TempMin, TempMax = s.TempMax,
            IncubationMinHours = s.IncubationMinHours, IncubationMaxHours = s.IncubationMaxHours
        }));

        await ValidateStepRulesAsync(id, excludeStepId: null, entity);

        _db.TestWorkflowSteps.Add(entity);
        await _db.SaveChangesAsync();
        await ValidateContiguousStepOrderAsync(id);
        return Ok(ApiResponse<object>.Ok(new { entity.Id, entity.StepOrder, entity.StepName }));
    }
```

- [ ] **Step 4: Populate the fields on update, replacing `IncubationStages` wholesale (same pattern as `StepMedia`)**

Edit `UpdateTestWorkflowStep` (around line 904-937):

```csharp
    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpPut("test-definitions/steps/{stepId}")]
    public async Task<IActionResult> UpdateTestWorkflowStep(int stepId, UpdateTestWorkflowStepRequest request)
    {
        var step = await _db.TestWorkflowSteps.Include(s => s.StepMedia).Include(s => s.IncubationStages)
            .FirstOrDefaultAsync(s => s.Id == stepId)
            ?? throw new InvalidOperationException($"Workflow step {stepId} not found.");

        step.StepName = request.StepName;
        step.MediaTypeId = request.MediaTypeId;
        step.IncubationMinHours = request.IncubationMinHours;
        step.IncubationMaxHours = request.IncubationMaxHours;
        step.TemperatureMin = request.TemperatureMin;
        step.TemperatureMax = request.TemperatureMax;
        step.IsFinalStep = request.IsFinalStep;
        step.StepType = request.StepType;
        step.TargetOrganismId = request.TargetOrganismId;
        step.RequiresIncubationTransfer = request.RequiresIncubationTransfer;

        // StepMedia is replaced wholesale on update - the analyst edits the
        // panel as a set, and the unique index makes incremental merging
        // error-prone for no benefit.
        _db.TestWorkflowStepMedias.RemoveRange(step.StepMedia);
        step.StepMedia.Clear();
        step.StepMedia.AddRange(request.StepMedia.Select(m => new TestWorkflowStepMedia
        {
            TestWorkflowStepId = step.Id, MaterialId = m.MaterialId, TempMin = m.TempMin, TempMax = m.TempMax,
            IsRequired = m.IsRequired, DisplayOrder = m.DisplayOrder
        }));

        _db.TestWorkflowStepIncubationStages.RemoveRange(step.IncubationStages);
        step.IncubationStages.Clear();
        step.IncubationStages.AddRange((request.IncubationStages ?? new()).Select(s => new TestWorkflowStepIncubationStage
        {
            TestWorkflowStepId = step.Id, StageNumber = s.StageNumber, TempMin = s.TempMin, TempMax = s.TempMax,
            IncubationMinHours = s.IncubationMinHours, IncubationMaxHours = s.IncubationMaxHours
        }));

        await ValidateStepRulesAsync(step.TestDefinitionId, excludeStepId: stepId, step);
        await _db.SaveChangesAsync();

        return Ok(ApiResponse<object>.Ok(new { step.Id, step.StepOrder, step.StepName }));
    }
```

- [ ] **Step 5: Build**

```bash
dotnet build backend/MicroLIMS.sln
```

Expected: builds clean. (No dedicated controller test file exists for `MasterDataController` in this codebase — Task 2's validator tests are the enforcement point for "cannot save half-configured", and this controller code calls that validator unchanged.)

- [ ] **Step 6: Commit**

```bash
git add backend/MicroLIMS.API/Controllers/MasterDataController.cs
git commit -m "feat: surface RequiresIncubationTransfer and stage-2 config on the Test Master step editor API"
```

---

### Task 4: Error codes and the `RequireValidIncubationWindow` overload

**Files:**
- Modify: `backend/MicroLIMS.Shared/Constants/WorkflowErrorCodes.cs`
- Modify: `backend/MicroLIMS.Application/Workflows/TestWorkflowEngine.cs`

**Interfaces:**
- Produces: `WorkflowErrorCodes.IncubationStage1NotComplete`, `WorkflowErrorCodes.IncubationStage2NotStarted`; `RequireValidIncubationWindow(string stepName, int minHours, DateTime start, DateTime end)` overload. Task 5 calls both; the existing `RequireValidIncubationWindow(TestWorkflowStep, start, end)` now delegates to it, unchanged for existing callers (`SubmitSelectivePlatingAsync`, `SubmitConfirmatorySetupAsync`).

- [ ] **Step 1: Add the two new error codes**

Edit `backend/MicroLIMS.Shared/Constants/WorkflowErrorCodes.cs` — add before the closing brace:

```csharp
    // Stage 1 of a two-stage incubation-transfer PlateCount step has not
    // reached its declared end yet - stage 2 cannot start until it has.
    public const string IncubationStage1NotComplete = "INCUBATION_STAGE1_NOT_COMPLETE";

    // Stage 2 of a two-stage incubation-transfer PlateCount step has not
    // been started yet - the count cannot be recorded until it has.
    public const string IncubationStage2NotStarted = "INCUBATION_STAGE2_NOT_STARTED";
```

- [ ] **Step 2: Refactor `RequireValidIncubationWindow` into a shared overload**

Edit `backend/MicroLIMS.Application/Workflows/TestWorkflowEngine.cs`, replacing the existing method at lines 894-915:

```csharp
    // RequireIncubationComplete only asks "has the declared end passed?",
    // which a one-second window satisfies as readily as a real 18-24h
    // one - and a window that ends before it starts satisfies it too.
    // The window is analyst-supplied, so it has to be checked against a
    // minimum the same way RequireMinimumDurationElapsed checks a
    // server-recorded window against it.
    //
    // Deliberately no upper bound: over-incubation happens in real labs
    // and is handled by explanation/deviation, not by refusing the
    // record. Under-incubation is the falsification risk.
    private static void RequireValidIncubationWindow(string stepName, int minHours, DateTime incubationStartUtc, DateTime incubationEndUtc)
    {
        if (incubationEndUtc < incubationStartUtc)
            throw new WorkflowStepException(WorkflowErrorCodes.IncubationWindowInvalid,
                $"The incubation window ends before it starts ({incubationStartUtc:yyyy-MM-dd HH:mm} to {incubationEndUtc:yyyy-MM-dd HH:mm} UTC).");

        var declaredHours = (incubationEndUtc - incubationStartUtc).TotalHours;
        if (declaredHours < minHours)
            throw new WorkflowStepException(WorkflowErrorCodes.IncubationWindowTooShort,
                $"Step \"{stepName}\" requires at least {minHours} hours of incubation - " +
                $"the declared window is {declaredHours:0.##} hours.");
    }

    private static void RequireValidIncubationWindow(TestWorkflowStep step, DateTime incubationStartUtc, DateTime incubationEndUtc) =>
        RequireValidIncubationWindow(step.StepName, step.IncubationMinHours, incubationStartUtc, incubationEndUtc);
```

- [ ] **Step 3: Build and run the pathogen-chain tests that exercise the original overload**

```bash
dotnet build backend/MicroLIMS.sln
dotnet test backend/MicroLIMS.Tests/MicroLIMS.Tests.csproj --filter "ConfirmatoryPlatingTests|SelectivePlatingTests"
```

Expected: builds clean, all pass unchanged (the refactor is behavior-preserving — same checks, same error codes, same messages for the existing overload's callers).

- [ ] **Step 4: Commit**

```bash
git add backend/MicroLIMS.Shared/Constants/WorkflowErrorCodes.cs backend/MicroLIMS.Application/Workflows/TestWorkflowEngine.cs
git commit -m "feat: add stage-transfer error codes and a step-agnostic incubation-window validator overload"
```

---

### Task 5: `StartStage2IncubationAsync` — the transfer action itself

**Files:**
- Modify: `backend/MicroLIMS.Application/Workflows/TestWorkflowEngine.cs` (interface + `SelectMediaAsync` + new method)
- Modify: `backend/MicroLIMS.API/Controllers/TestWorkflowController.cs`

**Interfaces:**
- Consumes: `TestWorkflowStepIncubationStage` (Task 1), `WorkflowErrorCodes.IncubationStage1NotComplete` + `RequireValidIncubationWindow(string, int, DateTime, DateTime)` (Task 4).
- Produces: `ITestWorkflowEngine.StartStage2IncubationAsync(int testOrderId, string stepName, int incubatorEquipmentId, int userId) : Task<Incubation>`. Task 6 relies on the fact that after this call, the step's only open `Incubation` row has `StageNumber == 2`. Controller route: `POST api/test-workflow/{testOrderId}/start-stage-2-incubation`.

- [ ] **Step 1: Populate `StartedByUserId` when stage 1 is created**

Edit `SelectMediaAsync` in `backend/MicroLIMS.Application/Workflows/TestWorkflowEngine.cs` (the `new Incubation { … }` block at lines 352-365) — add `StartedByUserId = userId`:

```csharp
        var incubation = new Incubation
        {
            TestOrderId = testOrderId,
            StepName = stepName,
            MediaId = mediaLotId,
            IncubatorEquipmentId = incubatorEquipmentId,
            Temperature = $"{step.TemperatureMin}-{step.TemperatureMax} °C",
            Duration = $"{step.IncubationMinHours}-{step.IncubationMaxHours} hours",
            StartedAt = startedAt,
            IncubationStartUtc = startedAt,
            IncubationEndUtc = startedAt.AddHours(step.IncubationMaxHours),
            ExpectedReadingAt = startedAt.AddHours(step.IncubationMaxHours),
            WindowReceivedAtUtc = startedAt,
            StartedByUserId = userId
        };
```

This is the only change to `SelectMediaAsync`'s behavior: a previously-always-null column now has a value. No existing assertion in `CountTestWorkflowTests`/`EMBatchLocationTests` checks `StartedByUserId`, so nothing regresses; it's what Task 8's report projection reads for "who started stage 1".

- [ ] **Step 2: Add the interface method**

Edit the `ITestWorkflowEngine` interface (around line 89) — add directly after `SelectMediaAsync`'s signature:

```csharp
    Task<Incubation> SelectMediaAsync(int testOrderId, string stepName, int mediaLotId, int incubatorEquipmentId, int userId);
    Task<Incubation> StartStage2IncubationAsync(int testOrderId, string stepName, int incubatorEquipmentId, int userId);
```

- [ ] **Step 3: Implement `StartStage2IncubationAsync`**

Add this method to `TestWorkflowEngine`, directly after `SelectMediaAsync` (after line 383):

```csharp
    // The transfer IS starting stage 2 - there is no separate
    // confirmation step or timestamp. The physical plate does not change
    // between stages, so MediaId is copied from stage 1 rather than
    // asking the analyst to reselect it; the incubator is new, since
    // that's the whole point of a transfer.
    public async Task<Incubation> StartStage2IncubationAsync(int testOrderId, string stepName, int incubatorEquipmentId, int userId)
    {
        var (order, definition) = await LoadWithTemplateAsync(testOrderId);
        var step = definition.Steps.FirstOrDefault(s => s.StepName == stepName)
            ?? throw new InvalidOperationException($"Step \"{stepName}\" is not part of the workflow template for \"{order.TestCode}\".");

        if (step.StepType != StepType.PlateCount || !step.RequiresIncubationTransfer)
            throw new InvalidOperationException($"Step \"{stepName}\" does not use a two-stage incubation transfer.");

        var openIncubation = await _db.Incubations
            .Where(i => i.TestOrderId == testOrderId && i.StepName == stepName && i.CompletedAt == null)
            .OrderByDescending(i => i.StartedAt)
            .FirstOrDefaultAsync()
            ?? throw new InvalidOperationException($"Media must be selected for step \"{stepName}\" before stage 2 incubation can start.");

        if (openIncubation.StageNumber != 1)
            throw new InvalidOperationException($"Stage 2 incubation has already been started for step \"{stepName}\".");

        if (DateTime.UtcNow < openIncubation.IncubationEndUtc)
            throw new WorkflowStepException(WorkflowErrorCodes.IncubationStage1NotComplete,
                $"Stage 1 incubation for step \"{stepName}\" has not finished yet.",
                Math.Max(0, (long)Math.Ceiling((openIncubation.IncubationEndUtc!.Value - DateTime.UtcNow).TotalSeconds)));

        var stage2Config = await _db.TestWorkflowStepIncubationStages
            .FirstOrDefaultAsync(s => s.TestWorkflowStepId == step.Id && s.StageNumber == 2)
            ?? throw new InvalidOperationException($"Step \"{stepName}\" has no stage 2 configuration.");

        var startedAt = DateTime.UtcNow;
        var endUtc = startedAt.AddHours(stage2Config.IncubationMaxHours);
        RequireValidIncubationWindow(stepName, stage2Config.IncubationMinHours, startedAt, endUtc);

        openIncubation.CompletedAt = startedAt;
        openIncubation.Outcome = "Transferred to stage 2 incubation.";

        var stage2 = new Incubation
        {
            TestOrderId = testOrderId,
            StepNumber = step.StepOrder,
            StepName = stepName,
            MediaId = openIncubation.MediaId,
            IncubatorEquipmentId = incubatorEquipmentId,
            Temperature = $"{stage2Config.TempMin}-{stage2Config.TempMax} °C",
            Duration = $"{stage2Config.IncubationMinHours}-{stage2Config.IncubationMaxHours} hours",
            StartedAt = startedAt,
            IncubationStartUtc = startedAt,
            IncubationEndUtc = endUtc,
            ExpectedReadingAt = endUtc,
            WindowReceivedAtUtc = startedAt,
            StartedByUserId = userId,
            ParentIncubationId = openIncubation.Id,
            StageNumber = 2
        };
        _db.Incubations.Add(stage2);
        await _db.SaveChangesAsync();

        return stage2;
    }
```

- [ ] **Step 4: Add the controller endpoint**

Edit `backend/MicroLIMS.API/Controllers/TestWorkflowController.cs` — add a request record near `SelectMediaRequest` (line 18):

```csharp
public record SelectMediaRequest(string StepName, int MediaLotId, int IncubatorId);
public record StartStage2IncubationRequest(string StepName, int IncubatorId);
```

Add the endpoint directly after `SelectMedia` (after line 254):

```csharp
    // The transfer IS this call - starting stage 2's incubation closes
    // stage 1 and opens a new Incubation row in one step. See
    // TestWorkflowEngine.StartStage2IncubationAsync.
    [HttpPost("{testOrderId}/start-stage-2-incubation")]
    public Task<IActionResult> StartStage2Incubation(int testOrderId, StartStage2IncubationRequest request) => RunAsync(async () =>
    {
        var incubation = await _engine.StartStage2IncubationAsync(testOrderId, request.StepName, request.IncubatorId, CurrentUserId);
        return new
        {
            incubation.Id, incubation.StepName, incubation.StageNumber, incubation.ParentIncubationId,
            incubation.Temperature, incubation.Duration, incubation.StartedAt, incubation.ExpectedReadingAt
        };
    });
```

- [ ] **Step 5: Also expose `RequiresIncubationTransfer` and `StageNumber` on the runtime current-step GET (so the endpoint pair this task adds is actually usable end to end)**

Edit `GetCurrentStep` in the same file — extend the `step` projection (around line 152-161) and the `incubationLock` object (around line 123-128):

```csharp
        var openIncubation = current.OpenIncubation;
        var incubationLock = openIncubation?.IncubationEndUtc is null ? null : new
        {
            isLocked = !openIncubation.IsIncubationComplete,
            incubationEndUtc = openIncubation.IncubationEndUtc,
            remainingSeconds = Math.Max(0, (long)Math.Ceiling((openIncubation.IncubationEndUtc.Value - DateTime.UtcNow).TotalSeconds)),
            stageNumber = openIncubation.StageNumber
        };
```

```csharp
            step = current.Step is null ? null : new
            {
                current.Step.Id, current.Step.StepOrder, current.Step.StepName, current.Step.MediaTypeId,
                stepType = current.Step.StepType.ToString(),
                current.Step.TargetOrganismId,
                mediaType = current.Step.MediaType is null ? null : new { current.Step.MediaType.Id, current.Step.MediaType.Class },
                current.Step.IncubationMinHours, current.Step.IncubationMaxHours,
                current.Step.TemperatureMin, current.Step.TemperatureMax,
                current.Step.IsFinalStep,
                current.Step.RequiresIncubationTransfer
            },
```

- [ ] **Step 6: Build**

```bash
dotnet build backend/MicroLIMS.sln
```

Expected: builds clean. (Engine-level tests for this method are written in Task 7 — implementing this method with only a build check here keeps this task's diff reviewable on its own; Task 6/7 exercise it thoroughly.)

- [ ] **Step 7: Commit**

```bash
git add backend/MicroLIMS.Application/Workflows/TestWorkflowEngine.cs backend/MicroLIMS.API/Controllers/TestWorkflowController.cs
git commit -m "feat: add StartStage2IncubationAsync - the transfer action for two-stage PlateCount steps"
```

---

### Task 6: Gate final result recording on stage 2 completion

**Files:**
- Modify: `backend/MicroLIMS.Application/Workflows/TestWorkflowEngine.cs` (`RecordResultAsync`, `RecordBatchResultsAsync`)
- Create: `backend/MicroLIMS.Tests/WorkflowTests/IncubationTransferTests.cs`

**Interfaces:**
- Consumes: `StartStage2IncubationAsync` (Task 5), `WorkflowErrorCodes.IncubationStage2NotStarted`/`IncubationNotComplete` (Task 4/existing), `RequireIncubationComplete` (existing, `TestWorkflowEngine.cs:886-892`).
- Produces: the full observable behavior this plan is graded on. `RecordResultAsync`/`RecordBatchResultsAsync` now throw before reaching `RecordCountTestAsync`/the location-writing loop when a transfer-enabled step's stage 2 hasn't started or hasn't elapsed.

- [ ] **Step 1: Write the seed helper and the failing tests**

Create `backend/MicroLIMS.Tests/WorkflowTests/IncubationTransferTests.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Constants;
using Xunit;

namespace MicroLIMS.Tests.WorkflowTests;

// Two-stage incubation transfer, opted into per PlateCount step via
// RequiresIncubationTransfer. Mirrors CountTestWorkflowTests' seed shape;
// the step's own TemperatureMin/Max/IncubationMinHours/MaxHours describe
// stage 1, and a TestWorkflowStepIncubationStage row (StageNumber == 2)
// describes stage 2.
public class IncubationTransferTests
{
    private static MicroLimsDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MicroLimsDbContext(options);
    }

    private static async Task<(TestOrder order, Media media, TestWorkflowStep step)> SeedTransferOrderAsync(
        MicroLimsDbContext db, int stage1MinHours = 24, int stage1MaxHours = 48, int stage2MinHours = 24, int stage2MaxHours = 48)
    {
        var testDefinition = new TestDefinition { Code = "TAMC-TRANSFER", DisplayName = "TAMC with transfer", WorkflowType = WorkflowType.CountTest };
        var generalAgar = new MediaType { Class = MediaClass.GeneralAgar, IncubationMinHours = 24, IncubationMaxHours = 48, RequiredTemperatureMin = 30, RequiredTemperatureMax = 35 };
        db.TestDefinitions.Add(testDefinition);
        db.MediaTypes.Add(generalAgar);
        await db.SaveChangesAsync();

        var step = new TestWorkflowStep
        {
            TestDefinitionId = testDefinition.Id, StepOrder = 1, StepName = "CountIncubation", MediaTypeId = generalAgar.Id,
            IncubationMinHours = stage1MinHours, IncubationMaxHours = stage1MaxHours, TemperatureMin = 30, TemperatureMax = 35,
            IsFinalStep = true, StepType = StepType.PlateCount, RequiresIncubationTransfer = true
        };
        step.IncubationStages.Add(new TestWorkflowStepIncubationStage
        {
            StageNumber = 2, TempMin = 20, TempMax = 25, IncubationMinHours = stage2MinHours, IncubationMaxHours = stage2MaxHours
        });
        db.TestWorkflowSteps.Add(step);
        await db.SaveChangesAsync();

        var material = new Material
        {
            MaterialType = MaterialType.DehydratedMedia, MaterialName = "TSA Powder", ManufacturerName = "Himedia",
            BatchNumber = "LOT-001", ReceivingDate = DateTime.UtcNow.AddDays(-10), Code = "TSA",
            Location = "Micro Lab", QuantityReceived = 500, QuantityRemaining = 500, Unit = MaterialUnit.Gram
        };
        db.Materials.Add(material);
        await db.SaveChangesAsync();

        var media = new Media { MediaTypeId = generalAgar.Id, MaterialId = material.Id, LotNumber = "TSA/1/26", IsReleasedForUse = true, Status = MediaStatus.Active, ExpiryDate = DateTime.UtcNow.AddDays(30) };
        db.Media.Add(media);

        var point = new WaterSamplingPoint { Code = "WP-01", Location = "Utility Room", AssignedTestCodes = new() { "TAMC-TRANSFER" } };
        db.WaterSamplingPoints.Add(point);
        await db.SaveChangesAsync();

        var sample = new Sample { Category = SampleCategory.Water, WaterSamplingPointId = point.Id, ControlNumber = "CTRL-1", Status = SampleStatus.Received };
        var order = new TestOrder { TestCode = "TAMC-TRANSFER", Status = ApprovalStatus.Pending, CurrentStep = WorkflowStep.Waiting };
        sample.TestOrders.Add(order);
        db.Samples.Add(sample);
        await db.SaveChangesAsync();

        return (order, media, step);
    }

    // Backdates stage 1's window so it reads as already elapsed, without
    // waiting real wall-clock hours in the test.
    private static async Task BackdateOpenIncubationAsync(MicroLimsDbContext db, int testOrderId, string stepName, TimeSpan elapsedSince)
    {
        var incubation = await db.Incubations.FirstAsync(i => i.TestOrderId == testOrderId && i.StepName == stepName && i.CompletedAt == null);
        incubation.StartedAt -= elapsedSince;
        incubation.IncubationStartUtc -= elapsedSince;
        incubation.IncubationEndUtc -= elapsedSince;
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task StartStage2Async_BeforeStage1WindowElapses_ThrowsStage1NotComplete()
    {
        await using var db = NewDb();
        var (order, media, _) = await SeedTransferOrderAsync(db);
        var engine = TestServiceFactory.TestWorkflow(db);
        await engine.SelectMediaAsync(order.Id, "CountIncubation", media.Id, incubatorEquipmentId: 1, userId: 1);

        var ex = await Assert.ThrowsAsync<WorkflowStepException>(() =>
            engine.StartStage2IncubationAsync(order.Id, "CountIncubation", incubatorEquipmentId: 2, userId: 1));

        Assert.Equal(WorkflowErrorCodes.IncubationStage1NotComplete, ex.ErrorCode);
    }

    [Fact]
    public async Task RecordResultAsync_BeforeStage2Started_ThrowsStage2NotStarted()
    {
        await using var db = NewDb();
        var (order, media, _) = await SeedTransferOrderAsync(db);
        var engine = TestServiceFactory.TestWorkflow(db);
        await engine.SelectMediaAsync(order.Id, "CountIncubation", media.Id, incubatorEquipmentId: 1, userId: 1);
        await BackdateOpenIncubationAsync(db, order.Id, "CountIncubation", TimeSpan.FromHours(49));

        var ex = await Assert.ThrowsAsync<WorkflowStepException>(() =>
            engine.RecordResultAsync(order.Id, "CountIncubation", new CountTestPayload(new List<decimal> { 10 }, 1), userId: 1));

        Assert.Equal(WorkflowErrorCodes.IncubationStage2NotStarted, ex.ErrorCode);
    }

    [Fact]
    public async Task RecordResultAsync_BeforeStage2WindowElapses_ThrowsIncubationNotComplete()
    {
        await using var db = NewDb();
        var (order, media, _) = await SeedTransferOrderAsync(db);
        var engine = TestServiceFactory.TestWorkflow(db);
        await engine.SelectMediaAsync(order.Id, "CountIncubation", media.Id, incubatorEquipmentId: 1, userId: 1);
        await BackdateOpenIncubationAsync(db, order.Id, "CountIncubation", TimeSpan.FromHours(49));
        await engine.StartStage2IncubationAsync(order.Id, "CountIncubation", incubatorEquipmentId: 2, userId: 2);

        var ex = await Assert.ThrowsAsync<WorkflowStepException>(() =>
            engine.RecordResultAsync(order.Id, "CountIncubation", new CountTestPayload(new List<decimal> { 10 }, 1), userId: 1));

        Assert.Equal(WorkflowErrorCodes.IncubationNotComplete, ex.ErrorCode);
    }

    [Fact]
    public async Task RecordResultAsync_AfterBothStagesElapse_Succeeds()
    {
        await using var db = NewDb();
        var (order, media, _) = await SeedTransferOrderAsync(db);
        var engine = TestServiceFactory.TestWorkflow(db);
        await engine.SelectMediaAsync(order.Id, "CountIncubation", media.Id, incubatorEquipmentId: 1, userId: 1);
        await BackdateOpenIncubationAsync(db, order.Id, "CountIncubation", TimeSpan.FromHours(49));
        await engine.StartStage2IncubationAsync(order.Id, "CountIncubation", incubatorEquipmentId: 2, userId: 2);
        await BackdateOpenIncubationAsync(db, order.Id, "CountIncubation", TimeSpan.FromHours(49));

        var result = await engine.RecordResultAsync(order.Id, "CountIncubation", new CountTestPayload(new List<decimal> { 10 }, 1), userId: 3);

        Assert.True(result.AllStepsComplete);
        var reading = await db.CountTestReadings.SingleAsync(r => r.TestOrderId == order.Id);
        Assert.Equal(3, reading.EnteredByUserId);
    }

    [Fact]
    public async Task StartStage2Async_CopiesMediaIdFromStage1()
    {
        await using var db = NewDb();
        var (order, media, _) = await SeedTransferOrderAsync(db);
        var engine = TestServiceFactory.TestWorkflow(db);
        await engine.SelectMediaAsync(order.Id, "CountIncubation", media.Id, incubatorEquipmentId: 1, userId: 1);
        await BackdateOpenIncubationAsync(db, order.Id, "CountIncubation", TimeSpan.FromHours(49));

        var stage2 = await engine.StartStage2IncubationAsync(order.Id, "CountIncubation", incubatorEquipmentId: 2, userId: 2);

        Assert.Equal(media.Id, stage2.MediaId);
        Assert.Equal(2, stage2.StageNumber);
        Assert.Equal(2, stage2.StartedByUserId);

        var stage1 = await db.Incubations.SingleAsync(i => i.TestOrderId == order.Id && i.StageNumber == 1);
        Assert.Equal(stage1.Id, stage2.ParentIncubationId);
        Assert.NotNull(stage1.CompletedAt);
        Assert.Equal(1, stage1.StartedByUserId);
    }

    // Regression: a PlateCount step with RequiresIncubationTransfer = false
    // (the default) behaves exactly as CountTestWorkflowTests already
    // proves - no stage-2 gate is reachable at all.
    [Fact]
    public async Task RecordResultAsync_NonTransferStep_RecordsImmediatelyAfterWindowElapses_NoStage2Required()
    {
        await using var db = NewDb();
        var (order, media, step) = await SeedTransferOrderAsync(db);
        step.RequiresIncubationTransfer = false;
        await db.SaveChangesAsync();
        var engine = TestServiceFactory.TestWorkflow(db);

        await engine.SelectMediaAsync(order.Id, "CountIncubation", media.Id, incubatorEquipmentId: 1, userId: 1);

        // No StartStage2IncubationAsync call at all - record-result must
        // succeed as soon as it's invoked, exactly like today, with no
        // stage-related exception.
        var result = await engine.RecordResultAsync(order.Id, "CountIncubation", new CountTestPayload(new List<decimal> { 10 }, 1), userId: 1);
        Assert.True(result.AllStepsComplete);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

```bash
dotnet test backend/MicroLIMS.Tests/MicroLIMS.Tests.csproj --filter "IncubationTransferTests"
```

Expected: `StartStage2Async_*` tests pass already (Task 5 implemented that method), but `RecordResultAsync_BeforeStage2Started_ThrowsStage2NotStarted` and `RecordResultAsync_BeforeStage2WindowElapses_ThrowsIncubationNotComplete` fail (no gate yet — they'd currently either throw a wrong generic error or, worse, succeed).

- [ ] **Step 3: Add the gate to `RecordResultAsync`**

Edit `backend/MicroLIMS.Application/Workflows/TestWorkflowEngine.cs` — insert immediately after the `openIncubation` lookup (after line 415, before the `switch (payload)` block):

```csharp
        var openIncubation = await _db.Incubations
            .Where(i => i.TestOrderId == testOrderId && i.StepName == stepName && i.CompletedAt == null)
            .OrderByDescending(i => i.StartedAt)
            .FirstOrDefaultAsync()
            ?? throw new InvalidOperationException($"Media must be selected for step \"{stepName}\" before a result can be recorded.");

        // Two-stage incubation transfer: the count cannot be recorded off
        // the stage 1 window at all, and not off stage 2 until its window
        // has elapsed. RequiresIncubationTransfer = false steps skip this
        // block entirely - unchanged from today.
        if (step.RequiresIncubationTransfer)
        {
            if (openIncubation.StageNumber != 2)
                throw new WorkflowStepException(WorkflowErrorCodes.IncubationStage2NotStarted,
                    $"Step \"{stepName}\" requires stage 2 incubation to be started before a count can be recorded.");

            RequireIncubationComplete(openIncubation.IncubationEndUtc!.Value);
        }

        string outcomeSummary;
```

(`string outcomeSummary;` already exists right after this point at line 417 — the new block is inserted between the `?? throw …;` and it, so the local-variable declarations that follow are untouched.)

- [ ] **Step 4: Add the equivalent gate to `RecordBatchResultsAsync` (the EM/After Cleaning path)**

Edit `RecordBatchResultsAsync` — insert right after the `(openIncubation, step)` load and the existing `IsFinalStep` check (after line 563), replacing the single `RequireMinimumDurationElapsed(openIncubation, step);` call at line 565:

```csharp
        var (openIncubation, step) = await LoadOpenBatchWindowAsync(testOrderId, definition);
        if (!step.IsFinalStep)
            throw new InvalidOperationException($"\"{step.StepName}\" is not the final incubation window yet - close it and start the next window first.");

        // Two-stage incubation transfer (After Cleaning / EM TAMC and any
        // other transfer-enabled PlateCount step): same gate as the
        // single-value RecordResultAsync path, since both terminate the
        // same server-locked Incubation window mechanics - see
        // TestWorkflowEngine.RecordResultAsync.
        if (step.RequiresIncubationTransfer)
        {
            if (openIncubation.StageNumber != 2)
                throw new WorkflowStepException(WorkflowErrorCodes.IncubationStage2NotStarted,
                    $"Step \"{step.StepName}\" requires stage 2 incubation to be started before results can be recorded.");

            RequireIncubationComplete(openIncubation.IncubationEndUtc!.Value);
        }
        else
        {
            RequireMinimumDurationElapsed(openIncubation, step);
        }
```

- [ ] **Step 5: Run the tests to verify they pass**

```bash
dotnet test backend/MicroLIMS.Tests/MicroLIMS.Tests.csproj --filter "IncubationTransferTests"
```

Expected: all 6 pass.

- [ ] **Step 6: Run the full existing workflow test suite to prove `RequiresIncubationTransfer = false` and EM/After Cleaning are untouched**

```bash
dotnet test backend/MicroLIMS.Tests/MicroLIMS.Tests.csproj --filter "CountTestWorkflowTests|EMBatchLocationTests"
```

Expected: all pass unchanged — `RequiresIncubationTransfer` defaults to `false` in every existing seed, so the new `if (step.RequiresIncubationTransfer)` branches are never entered by these tests, and the `else` branch in `RecordBatchResultsAsync` calls the exact same `RequireMinimumDurationElapsed` as before.

- [ ] **Step 7: Run the full suite one more time**

```bash
dotnet test backend/MicroLIMS.Tests/MicroLIMS.Tests.csproj
```

Expected: all pass.

- [ ] **Step 8: Commit**

```bash
git add backend/MicroLIMS.Application/Workflows/TestWorkflowEngine.cs backend/MicroLIMS.Tests/WorkflowTests/IncubationTransferTests.cs
git commit -m "feat: gate PlateCount result recording on stage-2 completion for transfer-enabled steps"
```

---

### Task 7: Report/projection — surface both stages and same/different analyst

**Files:**
- Modify: `backend/MicroLIMS.Application/DTOs/SampleSummaryDto.cs`
- Modify: `backend/MicroLIMS.Application/Services/SampleSummaryService.cs`
- Create: `backend/MicroLIMS.Tests/WorkflowTests/SampleSummaryIncubationStageTests.cs`

**Interfaces:**
- Consumes: `Incubation.StageNumber`/`StartedByUserId` (Task 1), populated by `SelectMediaAsync`/`StartStage2IncubationAsync` (Task 5).
- Produces: `IncubationDetailDto.StageNumber` (int), `IncubationDetailDto.StartedByName` (string?), `IncubationDetailDto.SameAnalystBothStages` (bool?, chosen field name — set only on the `StageNumber == 2` row for a given step, `null` everywhere else including every non-transfer step's single row). This is the exact field the brief asks to be named directly rather than left for the frontend to infer.

- [ ] **Step 1: Add the DTO fields**

Edit `backend/MicroLIMS.Application/DTOs/SampleSummaryDto.cs` — extend `IncubationDetailDto` (lines 87-99):

```csharp
public class IncubationDetailDto
{
    public string StepName { get; set; } = string.Empty;
    public int StageNumber { get; set; } = 1;
    public string? MediaLotNumber { get; set; }
    public string? MediaMaterialName { get; set; }
    public string? IncubatorName { get; set; }
    public string? Temperature { get; set; }
    public string? Duration { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? ExpectedReadingAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? Outcome { get; set; }
    public string? StartedByName { get; set; }

    // Set only on the StageNumber == 2 row of a two-stage transfer step:
    // true if the same analyst started both stages, false if different
    // analysts did, null everywhere else (including every
    // RequiresIncubationTransfer = false step's single row) - explicit
    // rather than making the frontend compare two user names itself.
    public bool? SameAnalystBothStages { get; set; }
}
```

- [ ] **Step 2: Include `StartedByUserId` in the batched name lookup and map the new fields**

Edit `backend/MicroLIMS.Application/Services/SampleSummaryService.cs` — extend the `userIds` `HashSet` build (lines 76-81):

```csharp
        var userIds = new HashSet<int>(results.Select(r => r.EnteredByUserId)
            .Concat(countTestReadings.Select(r => r.EnteredByUserId))
            .Concat(pathogenObservations.Select(p => p.ObservedByUserId))
            .Concat(workflowHistory.Select(w => w.PerformedByUserId))
            .Concat(sampleLocations.Where(l => l.EnteredByUserId is not null).Select(l => l.EnteredByUserId!.Value))
            .Concat(incubations.Where(i => i.StartedByUserId is not null).Select(i => i.StartedByUserId!.Value))
            .Append(sample.ReceivedByUserId));
```

Edit the `Incubations = incubations.Where(…).Select(…)` projection (lines 144-156):

```csharp
                Incubations = incubations.Where(i => i.TestOrderId == order.Id)
                    .OrderBy(i => i.StepNumber).ThenBy(i => i.StageNumber)
                    .Select(i => new IncubationDetailDto
                {
                    StepName = i.StepName,
                    StageNumber = i.StageNumber,
                    MediaLotNumber = i.Media?.LotNumber,
                    MediaMaterialName = i.Media?.Material?.MaterialName,
                    IncubatorName = i.IncubatorEquipment?.Name,
                    Temperature = i.Temperature,
                    Duration = i.Duration,
                    StartedAt = i.StartedAt,
                    ExpectedReadingAt = i.ExpectedReadingAt,
                    CompletedAt = i.CompletedAt,
                    Outcome = i.Outcome,
                    StartedByName = i.StartedByUserId is not null ? NameOf(i.StartedByUserId.Value) : null,
                    SameAnalystBothStages = i.StageNumber == 2
                        ? incubations
                            .Where(p => p.TestOrderId == order.Id && p.StepName == i.StepName && p.StageNumber == 1)
                            .Select(p => (bool?)(p.StartedByUserId == i.StartedByUserId))
                            .FirstOrDefault()
                        : null
                }).ToList(),
```

- [ ] **Step 3: Write the failing tests**

Create `backend/MicroLIMS.Tests/WorkflowTests/SampleSummaryIncubationStageTests.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.WorkflowTests;

public class SampleSummaryIncubationStageTests
{
    private static MicroLimsDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MicroLimsDbContext(options);
    }

    private static async Task<(int sampleId, TestOrder order, Media media)> SeedTransferOrderAsync(MicroLimsDbContext db)
    {
        var testDefinition = new TestDefinition { Code = "TAMC-TRANSFER", DisplayName = "TAMC with transfer", WorkflowType = WorkflowType.CountTest };
        var generalAgar = new MediaType { Class = MediaClass.GeneralAgar, IncubationMinHours = 24, IncubationMaxHours = 48, RequiredTemperatureMin = 30, RequiredTemperatureMax = 35 };
        db.TestDefinitions.Add(testDefinition);
        db.MediaTypes.Add(generalAgar);
        await db.SaveChangesAsync();

        var step = new TestWorkflowStep
        {
            TestDefinitionId = testDefinition.Id, StepOrder = 1, StepName = "CountIncubation", MediaTypeId = generalAgar.Id,
            IncubationMinHours = 1, IncubationMaxHours = 1, TemperatureMin = 30, TemperatureMax = 35,
            IsFinalStep = true, StepType = StepType.PlateCount, RequiresIncubationTransfer = true
        };
        step.IncubationStages.Add(new TestWorkflowStepIncubationStage { StageNumber = 2, TempMin = 20, TempMax = 25, IncubationMinHours = 1, IncubationMaxHours = 1 });
        db.TestWorkflowSteps.Add(step);

        var material = new Material
        {
            MaterialType = MaterialType.DehydratedMedia, MaterialName = "TSA Powder", ManufacturerName = "Himedia",
            BatchNumber = "LOT-001", ReceivingDate = DateTime.UtcNow.AddDays(-10), Code = "TSA",
            Location = "Micro Lab", QuantityReceived = 500, QuantityRemaining = 500, Unit = MaterialUnit.Gram
        };
        db.Materials.Add(material);
        await db.SaveChangesAsync();

        var media = new Media { MediaTypeId = generalAgar.Id, MaterialId = material.Id, LotNumber = "TSA/1/26", IsReleasedForUse = true, Status = MediaStatus.Active, ExpiryDate = DateTime.UtcNow.AddDays(30) };
        db.Media.Add(media);

        var point = new WaterSamplingPoint { Code = "WP-01", Location = "Utility Room", AssignedTestCodes = new() { "TAMC-TRANSFER" } };
        db.WaterSamplingPoints.Add(point);
        await db.SaveChangesAsync();

        var sample = new Sample { Category = SampleCategory.Water, WaterSamplingPointId = point.Id, ControlNumber = "CTRL-1", Status = SampleStatus.Received };
        var order = new TestOrder { TestCode = "TAMC-TRANSFER", Status = ApprovalStatus.Pending, CurrentStep = WorkflowStep.Waiting };
        sample.TestOrders.Add(order);
        db.Samples.Add(sample);
        await db.SaveChangesAsync();

        db.Users.Add(new User { Id = 1, FullName = "Alice Analyst", Username = "alice", PasswordHash = "x", Role = UserRole.Analyst });
        db.Users.Add(new User { Id = 2, FullName = "Bob Analyst", Username = "bob", PasswordHash = "x", Role = UserRole.Analyst });
        await db.SaveChangesAsync();

        return (sample.Id, order, media);
    }

    private static async Task BackdateOpenIncubationAsync(MicroLimsDbContext db, int testOrderId, string stepName, TimeSpan elapsedSince)
    {
        var incubation = await db.Incubations.FirstAsync(i => i.TestOrderId == testOrderId && i.StepName == stepName && i.CompletedAt == null);
        incubation.StartedAt -= elapsedSince;
        incubation.IncubationStartUtc -= elapsedSince;
        incubation.IncubationEndUtc -= elapsedSince;
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task GetSummaryAsync_SameAnalystBothStages_ReportsTrue()
    {
        await using var db = NewDb();
        var (sampleId, order, media) = await SeedTransferOrderAsync(db);
        var engine = TestServiceFactory.TestWorkflow(db);
        await engine.SelectMediaAsync(order.Id, "CountIncubation", media.Id, incubatorEquipmentId: 1, userId: 1);
        await BackdateOpenIncubationAsync(db, order.Id, "CountIncubation", TimeSpan.FromHours(2));
        await engine.StartStage2IncubationAsync(order.Id, "CountIncubation", incubatorEquipmentId: 2, userId: 1);

        var summary = await TestServiceFactory.SampleSummary(db).GetSummaryAsync(sampleId);

        var stage1 = summary!.TestOrders[0].Incubations.Single(i => i.StageNumber == 1);
        var stage2 = summary.TestOrders[0].Incubations.Single(i => i.StageNumber == 2);
        Assert.Equal("Alice Analyst", stage1.StartedByName);
        Assert.Equal("Alice Analyst", stage2.StartedByName);
        Assert.Null(stage1.SameAnalystBothStages);
        Assert.True(stage2.SameAnalystBothStages);
    }

    [Fact]
    public async Task GetSummaryAsync_DifferentAnalystsPerStage_ReportsFalse()
    {
        await using var db = NewDb();
        var (sampleId, order, media) = await SeedTransferOrderAsync(db);
        var engine = TestServiceFactory.TestWorkflow(db);
        await engine.SelectMediaAsync(order.Id, "CountIncubation", media.Id, incubatorEquipmentId: 1, userId: 1);
        await BackdateOpenIncubationAsync(db, order.Id, "CountIncubation", TimeSpan.FromHours(2));
        await engine.StartStage2IncubationAsync(order.Id, "CountIncubation", incubatorEquipmentId: 2, userId: 2);

        var summary = await TestServiceFactory.SampleSummary(db).GetSummaryAsync(sampleId);

        var stage2 = summary!.TestOrders[0].Incubations.Single(i => i.StageNumber == 2);
        Assert.Equal("Alice Analyst", summary.TestOrders[0].Incubations.Single(i => i.StageNumber == 1).StartedByName);
        Assert.Equal("Bob Analyst", stage2.StartedByName);
        Assert.False(stage2.SameAnalystBothStages);
    }
}
```

Check `User` entity's required properties (`Username`, `PasswordHash`, `Role` field names/types) against `backend/MicroLIMS.Domain/Entities/User.cs` before running — adjust the two `db.Users.Add(...)` lines to match its actual required members if they differ from the guess above (this is the one seed detail this plan cannot fully pin down without re-reading that file; every other entity used above was read directly during discovery).

- [ ] **Step 4: Run the tests to verify they fail**

```bash
dotnet test backend/MicroLIMS.Tests/MicroLIMS.Tests.csproj --filter "SampleSummaryIncubationStageTests"
```

Expected: compile error or assertion failure (`StageNumber`/`StartedByName`/`SameAnalystBothStages` don't exist on the DTO yet).

- [ ] **Step 5: Confirm Steps 1-2 above are in place, then run again**

```bash
dotnet test backend/MicroLIMS.Tests/MicroLIMS.Tests.csproj --filter "SampleSummaryIncubationStageTests"
```

Expected: both pass.

- [ ] **Step 6: Run the full suite**

```bash
dotnet test backend/MicroLIMS.Tests/MicroLIMS.Tests.csproj
```

Expected: all pass — every existing `IncubationDetailDto` consumer (PDF/Word report renderers) only reads the fields it already knew about; the three new fields are additive.

- [ ] **Step 7: Commit**

```bash
git add backend/MicroLIMS.Application/DTOs/SampleSummaryDto.cs backend/MicroLIMS.Application/Services/SampleSummaryService.cs backend/MicroLIMS.Tests/WorkflowTests/SampleSummaryIncubationStageTests.cs
git commit -m "feat: surface both incubation stages and same/different-analyst on the sample report"
```

---

## Self-Review Notes

- **Spec coverage:** Part 1 (flag + validation) → Task 2/3. Part 2 (stage-2 schema, extensible) → Task 1 (chose the child-table shape, per the brief's own steer toward `StageNumber`-keyed extensibility). Part 3 (Incubation model) → Task 1. Part 4 (engine: stage 1 unchanged, transfer unlocks stage 2 not count entry, transfer = starting stage 2, reuse `RequireValidIncubationWindow`, reuse `INCUBATION_NOT_COMPLETE` pattern, new error codes, `RequiresIncubationTransfer = false` untouched) → Tasks 4, 5, 6. Part 5 (report: both stages, who started each, `SameAnalystBothStages`) → Task 7. Part 6 (tests) → every task's own test step, consolidated in Task 6 (engine gating + regression) and Task 7 (report). "Do not" list: no frontend files touched anywhere above; no transfer-confirmation entity/endpoint added (the transfer *is* `StartStage2IncubationAsync`); `RequiresIncubationTransfer = false` / non-`PlateCount` steps only ever reach pre-existing, unmodified code paths.
- **Chosen schema shape:** new table `TestWorkflowStepIncubationStage`, keyed by `(TestWorkflowStepId, StageNumber)`, holding only stage 2+ (stage 1 stays on `TestWorkflowStep` itself). Chosen over extracting stage 1 into the same table because stage 1's fields (`TemperatureMin/Max`, `IncubationMinHours/MaxHours`) are read directly by roughly a dozen existing call sites across the engine, controller, and DTOs discovered in Part 0 — extracting them would be a much larger, riskier refactor than this feature needs, and the brief explicitly allows "two new nullable columns on TestWorkflowStep" as the small-change alternative only when stage 1 isn't cleanly extractable, which is the case here.
- **Request/response shapes:**
  - Start stage 2: `POST api/test-workflow/{testOrderId}/start-stage-2-incubation` with body `{ StepName, IncubatorId }`, returning `{ id, stepName, stageNumber, parentIncubationId, temperature, duration, startedAt, expectedReadingAt }`.
  - Record count: unchanged wire shape — `POST api/test-workflow/{testOrderId}/record-result` with the existing `{ StepName, PlateReadings, DilutionFactor }` (single-value) or `POST api/test-workflow/{testOrderId}/batch-results` with the existing `{ DilutionFactor, Locations }` (EM/After Cleaning) — both now reject early with `INCUBATION_STAGE2_NOT_STARTED` or `INCUBATION_NOT_COMPLETE` when the step is transfer-enabled and stage 2 isn't ready.
- **Same/different analyst field name:** `IncubationDetailDto.SameAnalystBothStages` (`bool?`), set only on the `StageNumber == 2` row, `null` everywhere else — chosen over making the frontend compare `StartedByName` strings itself, per the brief's stated preference.
- **Placeholder scan:** no "TBD"/"handle it"/"similar to Task N" left in any step above; every code step is complete, copy-pasteable C#, not a description of code.
- **Type consistency check:** `StartStage2IncubationAsync(int, string, int, int)` signature matches across the interface (Task 5 Step 2), implementation (Task 5 Step 3), controller call (Task 5 Step 4), and every test call site (Task 6, Task 7). `Incubation.StageNumber`/`StartedByUserId`/`ParentIncubationId` names match across Task 1's entity, Task 5's engine code, Task 6's tests, and Task 7's DTO/service/tests. `TestWorkflowStepIncubationStage`'s four data fields (`TempMin, TempMax, IncubationMinHours, IncubationMaxHours`) match across Task 1 (entity+config), Task 2 (validator+tests), Task 3 (controller DTOs), Task 5 (engine read), and Task 6 (test seed).

## Execution Handoff

Plan complete and saved to `docs/superpowers/plans/2026-08-14-plate-count-incubation-transfer.md`. Two execution options:

1. **Subagent-Driven (recommended)** - I dispatch a fresh subagent per task, review between tasks, fast iteration
2. **Inline Execution** - Execute tasks in this session using executing-plans, batch execution with checkpoints

Which approach?
