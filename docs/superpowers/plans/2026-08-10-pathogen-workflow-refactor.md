# Pathogen Detection Workflow — Backend Refactor Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the pathogen detection backend's DualPlate model with a configurable, organism-specific, multi-stage confirmatory workflow (BrothEnrichment → SelectiveBroth → SelectivePlating → ConfirmatoryPlating → BiochemicalTest) driven entirely by Test Master configuration.

**Architecture:** The existing generic `TestWorkflowEngine` stays as the single engine; its per-step behaviour switch is re-keyed from `StepResultType` (`PlateCount/Growth/DualGrowth`) to a new `StepType` enum carrying the five pathogen stages plus `PlateCount`. Multi-media steps are configured via a new `TestWorkflowStepMedia` child table on `TestWorkflowStep`, and per-run analyst choices/readings are recorded in new `WorkflowStepResult` → `ConfirmatoryMediaSelection` / `ConfirmatoryPlateObservation` tables. Review send-back reuses the existing `ReviewWorkflowEvent` + `ReviewGateService` infrastructure rather than introducing a new audit mechanism.

**Tech Stack:** ASP.NET Core 8, EF Core 8 (PostgreSQL), xUnit + EF Core InMemory, Clean Architecture (Domain / Application / Persistence / Infrastructure / API / Shared).

---

## Global Constraints

- **Do not modify any frontend file.** `frontend/**` is out of scope; it is handled in Prompt 2. Breaking frontend contracts is expected and must be recorded in the final report, not fixed here.
- **Do not change the audit trail infrastructure.** `MicroLimsDbContext.CaptureAuditEntries` already writes an `AuditLog` row (User, Timestamp UTC, EntityName, EntityId, Action, PreviousValue JSON, NewValue JSON) for every insert/update/delete automatically. New entities inherit this for free. Semantic workflow events go to `WorkflowHistory` (per-TestOrder) and `ReviewWorkflowEvent` (per gated record) via `ReviewGateService` — extend usage only, never replace.
- **Do not change authentication or permission middleware.** Reuse `[Authorize(Roles = ...)]` with `RoleConstants` (`SystemAdministrator`, `SectionHead`, `Reviewer`, `Analyst`). These four are the only roles.
- **All timestamps are `DateTime.UtcNow`, server-generated.** Client-supplied timestamps are never authoritative. Exception: `incubationStartUtc`/`incubationEndUtc` on broth/confirmatory setup are analyst-entered planning values and are persisted as given, but the *audit* timestamp is always server-generated.
- **`ExpectedAppearanceSnapshot`, once written, is never updated by any code path.** No update statement, no recalculation, no backfill may touch it.
- **Enums persist as integers.** Verified: the codebase has zero `HasConversion<string>()` calls; every enum column in `MicroLimsDbContextModelSnapshot.cs` is `integer`. Match this — no string conversion.
- **Business-rule violations throw `InvalidOperationException`** (the convention used throughout `TestWorkflowEngine.cs`). `ExceptionMiddleware` maps both it and `BusinessRuleException` to HTTP 400 + `ApiResponse<object>.Fail(ex.Message)`.
- **Error codes:** `ApiResponse<T>.Fail(string message, List<string>? errors = null)` has no error-code field. Structured error codes required by the spec (`INCUBATION_NOT_COMPLETE`, etc.) are emitted as the **first element of the `errors` list**, with the human-readable text in `message`. See Task 3 for the exact helper.
- **Request DTOs are positional `record` types declared at the top of the controller file**, above the `[ApiController]` class, in namespace `MicroLIMS.API.Controllers`.
- **Services are registered in** `backend/MicroLIMS.API/Extensions/ServiceCollectionExtensions.cs`; test wiring lives in `backend/MicroLIMS.Tests/TestServiceFactory.cs`. Every new service must be added to both.
- **Build/test commands** (run from `E:\MicroLIMS\MicroLIMS`):
  - Build: `dotnet build backend/MicroLIMS.sln`
  - All tests: `dotnet test backend/MicroLIMS.Tests/MicroLIMS.Tests.csproj`
  - Single test: `dotnet test backend/MicroLIMS.Tests/MicroLIMS.Tests.csproj --filter "FullyQualifiedName~TestName"`

---

## Terminology Mapping (spec → this codebase)

The spec was written against generic names. This codebase uses different ones. **These mappings are binding for the whole plan:**

| Spec name | Actual codebase entity | Notes |
|---|---|---|
| `Media` (a medium, e.g. "EMB agar") | `Material` (with `MaterialType.DehydratedMedia`) | The *kind* of medium. `Material.MaterialName` is its name. |
| `MediaLot` (a physical released lot) | `Media` | `Media.LotNumber`, `Media.ExpiryDate`, `Media.IsReleasedForUse`, `Media.MaterialId` → `Material`. |
| `MediaEvaluationCriteria` | `MediaChallengeSpec` | Fields: `MaterialName`, `EvaluationType`, `OrganismId`, `ChallengeRole?`, `ExpectedDescription?`. `ExpectedDescription` is the appearance text to snapshot. |
| `Equipment` / incubator | `Equipment` where `Type == EquipmentType.Incubator` | Set point is `SetPointTemperature` (nullable decimal), not `SetTemperature`. Calibration is `CalibrationDueDate` (nullable DateTime), not a `CalibrationStatus` string — derive the string. |
| `IsActive` on Equipment | **does not exist** | `Equipment` has no `IsActive`. Eligibility filters on `Type`, `SetPointTemperature` range, and calibration not past due. |
| `WorkflowStepInstance` | `Incubation` | One row per step attempt, already carries `StepName`, `StepNumber`, `StartedAt`, `CompletedAt`, `ExpectedReadingAt`, `IncubatorEquipmentId`, `MediaId`. Incubation lock fields are added here. |
| `ReviewDecision` / `ReviewAction` | `ReviewWorkflowEvent` (+ `ReviewGateService`) | Polymorphic on `(EntityType, EntityId)`. Biochemical send-back state lives on `WorkflowStepResult`, not on this shared table — see Task 9. |
| `Attachment` | **does not exist** | No attachment entity in the codebase. `BiochemicalAttachmentId` is added as a nullable `int?` with **no FK and no navigation** — a forward hook, documented as a gap. |
| `PendingReviewFlag` | `WorkflowStepResult.SkippedBiochemical` (bool) + derived flags list | No enum flag column is introduced; the DTO `flags` array is computed. |

---

## File Structure

**Domain (`backend/MicroLIMS.Domain/`)**
- `Enums/WorkflowType.cs` — MODIFY: remove `DualPlate`.
- `Enums/StepResultType.cs` — DELETE (replaced).
- `Enums/StepType.cs` — CREATE: `PlateCount`, `BrothEnrichment`, `SelectiveBroth`, `SelectivePlating`, `ConfirmatoryPlating`, `BiochemicalTest`.
- `Enums/GrowthObservation.cs` — CREATE: `NoGrowth`, `GrowthNonConforming`, `GrowthConforming`.
- `Enums/ConfirmatoryResult.cs` — CREATE: `AllConforming`, `Inconclusive`.
- `Enums/AnalystDecision.cs` — CREATE: `SubmitAsDetected`, `ProceedToBiochemical`.
- `Entities/TestWorkflowStep.cs` — MODIFY: drop `IsDualPlate`/`Plate1DefaultLabel`/`Plate2DefaultLabel`, rename `StepResultType`→`StepType`, add `TargetOrganismId`/`TargetOrganism`/`StepMedia`, add `Validate()`.
- `Entities/TestWorkflowStepMedia.cs` — CREATE.
- `Entities/WorkflowStepResult.cs` — CREATE.
- `Entities/ConfirmatoryMediaSelection.cs` — CREATE.
- `Entities/ConfirmatoryPlateObservation.cs` — CREATE.
- `Entities/Incubation.cs` — MODIFY: drop `Plate2MediaId`/`Plate2Media`, add `IncubationStartUtc`/`IncubationEndUtc`/`IsIncubationComplete`.
- `Entities/PathogenObservation.cs` — MODIFY: `GrowthObserved` bool → `Observation` (`GrowthObservation`), drop `PlateLabel`.

**Persistence (`backend/MicroLIMS.Persistence/`)**
- `Configurations/TestWorkflowStepConfiguration.cs` — MODIFY.
- `Configurations/TestWorkflowStepMediaConfiguration.cs` — CREATE.
- `Configurations/WorkflowStepResultConfiguration.cs` — CREATE.
- `Configurations/ConfirmatoryMediaSelectionConfiguration.cs` — CREATE.
- `Configurations/ConfirmatoryPlateObservationConfiguration.cs` — CREATE.
- `Configurations/IncubationConfiguration.cs` — MODIFY.
- `Configurations/PathogenObservationConfiguration.cs` — MODIFY.
- `DbContext/MicroLimsDbContext.cs` — MODIFY: add four `DbSet`s.
- `Migrations/<timestamp>_AddPathogenWorkflowRefactor.cs` — CREATE (scaffolded).
- `Seed/DbSeeder.cs` — MODIFY: `SeedWorkflowTemplates` re-shaped to the 5-stage model.

**Application (`backend/MicroLIMS.Application/`)**
- `Services/IncubatorEligibilityService.cs` — CREATE.
- `Services/MediaAppearanceSnapshotService.cs` — CREATE.
- `Services/WorkflowTemplateValidator.cs` — CREATE (Rules 1–6).
- `Workflows/TestWorkflowEngine.cs` — MODIFY heavily.
- `Services/ResultProjectionService.cs` — MODIFY.
- `Services/SegregationOfDutiesGuard.cs` — MODIFY (`PathogenObservation` column rename).

**API (`backend/MicroLIMS.API/`)**
- `Controllers/TestWorkflowController.cs` — MODIFY: replace dual-plate endpoints with the new step endpoints.
- `Controllers/MasterDataController.cs` — MODIFY: step CRUD carries `StepType`, `TargetOrganismId`, `StepMedia`; validation delegates to `WorkflowTemplateValidator`.
- `Extensions/ServiceCollectionExtensions.cs` — MODIFY: register three new services.

**Shared (`backend/MicroLIMS.Shared/`)**
- `Responses/ApiResponse.cs` — unchanged.
- `Constants/WorkflowErrorCodes.cs` — CREATE.

**Tests (`backend/MicroLIMS.Tests/`)**
- `TestServiceFactory.cs` — MODIFY.
- `WorkflowTests/PathogenWorkflowTests.cs` — REWRITE for the 5-stage model.
- `WorkflowTests/CountTestWorkflowTests.cs` — MODIFY (`StepResultType.PlateCount` → `StepType.PlateCount`).
- `WorkflowTests/WorkflowTemplateValidationTests.cs` — CREATE.
- `WorkflowTests/IncubatorEligibilityTests.cs` — CREATE.
- `WorkflowTests/ConfirmatoryPlatingTests.cs` — CREATE.
- `WorkflowTests/BiochemicalReviewTests.cs` — CREATE.

---

## Pre-flight (do this before Task 1)

- [ ] **Confirm a clean starting point**

```bash
git -C E:/MicroLIMS/MicroLIMS status --short
```

There is substantial uncommitted work in the tree (the dual-plate model). Commit it first so this refactor is a reviewable diff on top of a known baseline:

```bash
git -C E:/MicroLIMS/MicroLIMS add -A && git -C E:/MicroLIMS/MicroLIMS commit -m "chore: snapshot dual-plate workflow before pathogen refactor"
```

- [ ] **Confirm the baseline builds and all tests pass**

```bash
dotnet build backend/MicroLIMS.sln && dotnet test backend/MicroLIMS.Tests/MicroLIMS.Tests.csproj
```

Expected: build succeeds, all tests pass. If not, stop and report — do not start the refactor on a red baseline.

---

## Task 1: Domain enums

**Files:**
- Modify: `backend/MicroLIMS.Domain/Enums/WorkflowType.cs`
- Delete: `backend/MicroLIMS.Domain/Enums/StepResultType.cs`
- Create: `backend/MicroLIMS.Domain/Enums/StepType.cs`
- Create: `backend/MicroLIMS.Domain/Enums/GrowthObservation.cs`
- Create: `backend/MicroLIMS.Domain/Enums/ConfirmatoryResult.cs`
- Create: `backend/MicroLIMS.Domain/Enums/AnalystDecision.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: `WorkflowType` (`CountTest`, `Observation`), `StepType` (`PlateCount`, `BrothEnrichment`, `SelectiveBroth`, `SelectivePlating`, `ConfirmatoryPlating`, `BiochemicalTest`), `GrowthObservation` (`NoGrowth`, `GrowthNonConforming`, `GrowthConforming`), `ConfirmatoryResult` (`AllConforming`, `Inconclusive`), `AnalystDecision` (`SubmitAsDetected`, `ProceedToBiochemical`). All in namespace `MicroLIMS.Domain.Enums`.

This task will not compile on its own — it deliberately breaks every `StepResultType` consumer. Tasks 2 and 3 restore the build. Commit at the end of Task 3, not here.

- [ ] **Step 1: Rewrite `WorkflowType.cs`**

`DualPlate` is removed. Salmonella (XLD+TSI), the only `DualPlate` test in seed data, is re-modelled in Task 12 as an `Observation` test whose confirmatory step permits both XLD and TSI as selectable media — exactly the case `ConfirmatoryPlating` was designed for, so no Salmonella-specific flag survives.

```csharp
namespace MicroLIMS.Domain.Enums;

public enum WorkflowType
{
    CountTest,   // plate readings + dilution factor (TAMC, TYMC)
    Observation  // staged pathogen detection chain, driven by each step's StepType
}
```

- [ ] **Step 2: Delete `StepResultType.cs`**

```bash
git -C E:/MicroLIMS/MicroLIMS rm backend/MicroLIMS.Domain/Enums/StepResultType.cs
```

- [ ] **Step 3: Create `StepType.cs`**

`PlateCount` stays at ordinal 0 so existing `CountTest` step rows keep their stored integer across the migration.

```csharp
namespace MicroLIMS.Domain.Enums;

// Per-step behaviour discriminator. PlateCount belongs to CountTest
// workflows; the remaining five are the pathogen detection stages, in
// the order a template would normally arrange them.
public enum StepType
{
    PlateCount,          // plate readings + dilution factor (TAMC/TYMC)
    BrothEnrichment,     // no result logic - incubate and record the setup
    SelectiveBroth,      // no result logic - incubate and record the setup
    SelectivePlating,    // single GrowthObservation; non-conforming ends the workflow
    ConfirmatoryPlating, // analyst-selected media panel, one observation per medium
    BiochemicalTest      // free-text confirmation, optional attachment
}
```

- [ ] **Step 4: Create `GrowthObservation.cs`**

```csharp
namespace MicroLIMS.Domain.Enums;

// Replaces the old GrowthObserved boolean. "Conforming" means growth
// matching the expected appearance for the target organism on that
// medium; growth that does not match is not the organism being sought.
public enum GrowthObservation
{
    NoGrowth,
    GrowthNonConforming,
    GrowthConforming
}
```

- [ ] **Step 5: Create `ConfirmatoryResult.cs`**

```csharp
namespace MicroLIMS.Domain.Enums;

public enum ConfirmatoryResult
{
    AllConforming,
    Inconclusive
}
```

- [ ] **Step 6: Create `AnalystDecision.cs`**

```csharp
namespace MicroLIMS.Domain.Enums;

public enum AnalystDecision
{
    SubmitAsDetected,
    ProceedToBiochemical
}
```

- [ ] **Step 7: Verify the enums are syntactically valid**

```bash
dotnet build backend/MicroLIMS.Domain/MicroLIMS.Domain.csproj
```

Expected: FAIL, but only with errors in `Entities/*.cs` referencing `StepResultType` / `IsDualPlate` (e.g. `CS0246: The type or namespace name 'StepResultType' could not be found`). No errors inside `Enums/`. Task 2 fixes these.

---

## Task 2: Domain entities

**Files:**
- Modify: `backend/MicroLIMS.Domain/Entities/TestWorkflowStep.cs`
- Create: `backend/MicroLIMS.Domain/Entities/TestWorkflowStepMedia.cs`
- Create: `backend/MicroLIMS.Domain/Entities/WorkflowStepResult.cs`
- Create: `backend/MicroLIMS.Domain/Entities/ConfirmatoryMediaSelection.cs`
- Create: `backend/MicroLIMS.Domain/Entities/ConfirmatoryPlateObservation.cs`
- Modify: `backend/MicroLIMS.Domain/Entities/Incubation.cs`
- Modify: `backend/MicroLIMS.Domain/Entities/PathogenObservation.cs`

**Interfaces:**
- Consumes: all enums from Task 1.
- Produces:
  - `TestWorkflowStep.StepType` (`StepType`), `.TargetOrganismId` (`int?`), `.TargetOrganism` (`Organism?`), `.StepMedia` (`List<TestWorkflowStepMedia>`), `.RequiresTargetOrganism` (bool, computed), `.RequiresIncubationLock` (bool, computed).
  - `TestWorkflowStepMedia`: `Id`, `TestWorkflowStepId`, `TestWorkflowStep`, `MaterialId`, `Material`, `TempMin`, `TempMax`, `IsRequired`, `DisplayOrder`.
  - `WorkflowStepResult`: `Id`, `IncubationId`, `Incubation`, `TestOrderId`, `TestOrder`, `StepName`, `StepType`, `SelectivePlatingObservation` (`GrowthObservation?`), `ExpectedAppearanceSnapshot` (`string?`), `ConfirmatoryResult` (`ConfirmatoryResult?`), `BiochemicalResultText` (`string?`), `BiochemicalAttachmentId` (`int?`), `SkippedBiochemical` (bool), `RequiresBiochemical` (bool), `ReturnReason` (`string?`), `ReturnedAtUtc` (`DateTime?`), `ReturnedByUserId` (`int?`), `SubmittedByUserId` (int), `SubmittedAtUtc` (DateTime), `Selections`, `ConfirmatoryObservations`.
  - `ConfirmatoryMediaSelection`: `Id`, `WorkflowStepResultId`, `WorkflowStepResult`, `MaterialId`, `Material`, `MediaId`, `Media`, `EquipmentId`, `Equipment`, `WasAnalystAdded`.
  - `ConfirmatoryPlateObservation`: `Id`, `WorkflowStepResultId`, `WorkflowStepResult`, `MaterialId`, `Material`, `Observation` (`GrowthObservation`), `ExpectedAppearanceSnapshot` (`string?`), `RecordedByUserId`, `RecordedAtUtc`.
  - `Incubation.IncubationStartUtc` (`DateTime?`), `.IncubationEndUtc` (`DateTime?`), `.IsIncubationComplete` (bool, `[NotMapped]`).
  - `PathogenObservation.Observation` (`GrowthObservation`).

- [ ] **Step 1: Rewrite `TestWorkflowStep.cs`**

`MediaTypeId` stays — it is the media-*class* lock the engine already enforces (`MediaClass.SelectiveAgar` etc.). `StepMedia` narrows that further to specific permitted materials.

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

    public bool RequiresTargetOrganism =>
        StepType is StepType.SelectivePlating or StepType.ConfirmatoryPlating;

    // BiochemicalTest is bench work with no incubation window, and
    // SelectivePlating is read off plates the previous step incubated.
    public bool RequiresIncubationLock =>
        StepType is StepType.BrothEnrichment or StepType.SelectiveBroth or StepType.ConfirmatoryPlating;
}
```

- [ ] **Step 2: Create `TestWorkflowStepMedia.cs`**

```csharp
namespace MicroLIMS.Domain.Entities;

// A medium permitted on a workflow step, configured in Test Master.
// MaterialId points at the medium itself (a Material with MaterialType.
// DehydratedMedia); the physical lot chosen at run time is a Media row.
public class TestWorkflowStepMedia
{
    public int Id { get; set; }

    public int TestWorkflowStepId { get; set; }
    public TestWorkflowStep? TestWorkflowStep { get; set; }

    public int MaterialId { get; set; }
    public Material? Material { get; set; }

    // Bounds the incubators offered for this medium at run time.
    public decimal TempMin { get; set; }
    public decimal TempMax { get; set; }

    // True = mandatory single medium (broth and selective plating steps).
    // False = analyst-selectable from the permitted list (confirmatory).
    public bool IsRequired { get; set; }

    public int DisplayOrder { get; set; }
}
```

- [ ] **Step 3: Create `WorkflowStepResult.cs`**

One row per completed step attempt, keyed to the `Incubation` that attempt opened. The biochemical send-back fields live here rather than on `ReviewWorkflowEvent` because that table is polymorphic across Sample/Media/Cryovial and shared by every gate — pathogen-only columns there would apply to unrelated records.

```csharp
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

public class WorkflowStepResult
{
    public int Id { get; set; }

    // The step attempt this result closes. One result per incubation.
    public int IncubationId { get; set; }
    public Incubation? Incubation { get; set; }

    public int TestOrderId { get; set; }
    public TestOrder? TestOrder { get; set; }

    public string StepName { get; set; } = string.Empty;
    public StepType StepType { get; set; }

    public GrowthObservation? SelectivePlatingObservation { get; set; }

    // Written once, at submission, from MediaChallengeSpec.ExpectedDescription.
    // Never updated afterwards (ALCOA+ Original and Contemporaneous).
    public string? ExpectedAppearanceSnapshot { get; set; }

    public ConfirmatoryResult? ConfirmatoryResult { get; set; }

    public string? BiochemicalResultText { get; set; }

    // No Attachment entity exists yet - unmapped forward hook, no FK.
    public int? BiochemicalAttachmentId { get; set; }

    // Analyst submitted Detected straight off confirmatory plating.
    public bool SkippedBiochemical { get; set; }

    // Set when a reviewer returns the result for biochemical confirmation.
    public bool RequiresBiochemical { get; set; }
    public string? ReturnReason { get; set; }
    public DateTime? ReturnedAtUtc { get; set; }
    public int? ReturnedByUserId { get; set; }

    public int SubmittedByUserId { get; set; }
    public DateTime SubmittedAtUtc { get; set; } = DateTime.UtcNow;

    public List<ConfirmatoryMediaSelection> Selections { get; set; } = new();
    public List<ConfirmatoryPlateObservation> ConfirmatoryObservations { get; set; } = new();
}
```

- [ ] **Step 4: Create `ConfirmatoryMediaSelection.cs`**

```csharp
namespace MicroLIMS.Domain.Entities;

// What the analyst actually chose for one confirmatory plating run:
// which permitted medium, which released lot of it, which incubator.
public class ConfirmatoryMediaSelection
{
    public int Id { get; set; }

    public int WorkflowStepResultId { get; set; }
    public WorkflowStepResult? WorkflowStepResult { get; set; }

    public int MaterialId { get; set; }
    public Material? Material { get; set; }

    // The released lot (Media row) of that material.
    public int MediaId { get; set; }
    public Media? Media { get; set; }

    public int EquipmentId { get; set; }
    public Equipment? Equipment { get; set; }

    // Always false: a medium outside the permitted list is rejected
    // before this row is created. Persisted so the record states the
    // fact rather than leaving it implied.
    public bool WasAnalystAdded { get; set; }
}
```

- [ ] **Step 5: Create `ConfirmatoryPlateObservation.cs`**

```csharp
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

// One plate reading per selected confirmatory medium per run.
public class ConfirmatoryPlateObservation
{
    public int Id { get; set; }

    public int WorkflowStepResultId { get; set; }
    public WorkflowStepResult? WorkflowStepResult { get; set; }

    public int MaterialId { get; set; }
    public Material? Material { get; set; }

    public GrowthObservation Observation { get; set; }

    // Written once at submission; never updated (ALCOA+).
    public string? ExpectedAppearanceSnapshot { get; set; }

    public int RecordedByUserId { get; set; }
    public DateTime RecordedAtUtc { get; set; } = DateTime.UtcNow;
}
```

- [ ] **Step 6: Rewrite `Incubation.cs`**

`Plate2MediaId`/`Plate2Media` go away with the dual-plate model. `IncubationStartUtc`/`IncubationEndUtc` are the analyst-declared window the lock is enforced against; `StartedAt` remains the server-stamped row-creation time.

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

    [NotMapped]
    public bool IsIncubationComplete =>
        IncubationEndUtc.HasValue && DateTime.UtcNow >= IncubationEndUtc.Value;
}
```

- [ ] **Step 7: Rewrite `PathogenObservation.cs`**

```csharp
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

public class PathogenObservation
{
    public int Id { get; set; }
    public int TestOrderId { get; set; }
    public TestOrder? TestOrder { get; set; }
    public string StepName { get; set; } = string.Empty;
    public int StepOrder { get; set; }

    public GrowthObservation Observation { get; set; }

    public int ObservedByUserId { get; set; }
    public DateTime ObservedAt { get; set; } = DateTime.UtcNow;

    public int? MediaId { get; set; }
    public Media? Media { get; set; }
}
```

- [ ] **Step 8: Build the domain project**

```bash
dotnet build backend/MicroLIMS.Domain/MicroLIMS.Domain.csproj
```

Expected: PASS. `Organism`, `Material`, `Media`, and `Equipment` are all in `MicroLIMS.Domain.Entities` already — if they report missing, check the `using MicroLIMS.Domain.Enums;` line rather than the entity references.

---

## Task 3: Persistence configuration, DbSets, seed data, error codes

**Files:**
- Create: `backend/MicroLIMS.Shared/Constants/WorkflowErrorCodes.cs`
- Modify: `backend/MicroLIMS.Persistence/Configurations/TestWorkflowStepConfiguration.cs`
- Create: `backend/MicroLIMS.Persistence/Configurations/TestWorkflowStepMediaConfiguration.cs`
- Create: `backend/MicroLIMS.Persistence/Configurations/WorkflowStepResultConfiguration.cs`
- Create: `backend/MicroLIMS.Persistence/Configurations/ConfirmatoryMediaSelectionConfiguration.cs`
- Create: `backend/MicroLIMS.Persistence/Configurations/ConfirmatoryPlateObservationConfiguration.cs`
- Modify: `backend/MicroLIMS.Persistence/Configurations/IncubationConfiguration.cs`
- Modify: `backend/MicroLIMS.Persistence/Configurations/PathogenObservationConfiguration.cs`
- Modify: `backend/MicroLIMS.Persistence/DbContext/MicroLimsDbContext.cs`
- Modify: `backend/MicroLIMS.Persistence/Seed/DbSeeder.cs`

**Interfaces:**
- Consumes: every entity from Task 2.
- Produces: `MicroLimsDbContext.TestWorkflowStepMedias`, `.WorkflowStepResults`, `.ConfirmatoryMediaSelections`, `.ConfirmatoryPlateObservations`; `WorkflowErrorCodes` constants (namespace `MicroLIMS.Shared.Constants`).

- [ ] **Step 1: Create `WorkflowErrorCodes.cs`**

```csharp
namespace MicroLIMS.Shared.Constants;

// Machine-readable codes the frontend switches on. ApiResponse has no
// dedicated code field, so these are returned as the first entry of
// ApiResponse.Errors with the human-readable text in Message.
public static class WorkflowErrorCodes
{
    public const string IncubationNotComplete = "INCUBATION_NOT_COMPLETE";
    public const string MediaNotInPermittedList = "MEDIA_NOT_IN_PERMITTED_LIST";
    public const string NoMediaSelected = "NO_MEDIA_SELECTED";
    public const string IncompleteConfirmatorySetup = "INCOMPLETE_CONFIRMATORY_SETUP";
    public const string IncubatorTempOutOfRange = "INCUBATOR_TEMP_OUT_OF_RANGE";
    public const string BiochemicalResultRequired = "BIOCHEMICAL_RESULT_REQUIRED";
    public const string SegregationOfDutiesViolation = "SEGREGATION_OF_DUTIES_VIOLATION";
    public const string TemplateValidationFailed = "TEMPLATE_VALIDATION_FAILED";
}
```

- [ ] **Step 2: Rewrite `TestWorkflowStepConfiguration.cs`**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class TestWorkflowStepConfiguration : IEntityTypeConfiguration<TestWorkflowStep>
{
    public void Configure(EntityTypeBuilder<TestWorkflowStep> builder)
    {
        builder.HasKey(s => s.Id);
        builder.Property(s => s.StepName).IsRequired().HasMaxLength(100);
        builder.Property(s => s.TemperatureMin).HasColumnType("decimal(5,2)");
        builder.Property(s => s.TemperatureMax).HasColumnType("decimal(5,2)");

        builder.HasIndex(s => new { s.TestDefinitionId, s.StepOrder }).IsUnique();

        builder.HasOne(s => s.TestDefinition)
            .WithMany(t => t.Steps)
            .HasForeignKey(s => s.TestDefinitionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.MediaType)
            .WithMany()
            .HasForeignKey(s => s.MediaTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.TargetOrganism)
            .WithMany()
            .HasForeignKey(s => s.TargetOrganismId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Ignore(s => s.RequiresTargetOrganism);
        builder.Ignore(s => s.RequiresIncubationLock);
    }
}
```

- [ ] **Step 3: Create `TestWorkflowStepMediaConfiguration.cs`**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class TestWorkflowStepMediaConfiguration : IEntityTypeConfiguration<TestWorkflowStepMedia>
{
    public void Configure(EntityTypeBuilder<TestWorkflowStepMedia> builder)
    {
        builder.HasKey(m => m.Id);
        builder.Property(m => m.TempMin).HasColumnType("decimal(5,2)");
        builder.Property(m => m.TempMax).HasColumnType("decimal(5,2)");

        // The same medium cannot be listed twice on one step.
        builder.HasIndex(m => new { m.TestWorkflowStepId, m.MaterialId }).IsUnique();

        builder.HasOne(m => m.TestWorkflowStep)
            .WithMany(s => s.StepMedia)
            .HasForeignKey(m => m.TestWorkflowStepId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(m => m.Material)
            .WithMany()
            .HasForeignKey(m => m.MaterialId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
```

- [ ] **Step 4: Create `WorkflowStepResultConfiguration.cs`**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class WorkflowStepResultConfiguration : IEntityTypeConfiguration<WorkflowStepResult>
{
    public void Configure(EntityTypeBuilder<WorkflowStepResult> builder)
    {
        builder.HasKey(r => r.Id);
        builder.Property(r => r.StepName).IsRequired().HasMaxLength(100);
        builder.Property(r => r.ReturnReason).HasMaxLength(1000);

        // Not unique: a BiochemicalTest result deliberately shares the
        // confirmatory step's incubation, since it has no window of its own.
        builder.HasIndex(r => r.IncubationId);

        builder.HasOne(r => r.Incubation)
            .WithMany()
            .HasForeignKey(r => r.IncubationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.TestOrder)
            .WithMany()
            .HasForeignKey(r => r.TestOrderId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
```

- [ ] **Step 5: Create `ConfirmatoryMediaSelectionConfiguration.cs`**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class ConfirmatoryMediaSelectionConfiguration : IEntityTypeConfiguration<ConfirmatoryMediaSelection>
{
    public void Configure(EntityTypeBuilder<ConfirmatoryMediaSelection> builder)
    {
        builder.HasKey(s => s.Id);

        builder.HasIndex(s => new { s.WorkflowStepResultId, s.MaterialId }).IsUnique();

        builder.HasOne(s => s.WorkflowStepResult)
            .WithMany(r => r.Selections)
            .HasForeignKey(s => s.WorkflowStepResultId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.Material).WithMany().HasForeignKey(s => s.MaterialId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(s => s.Media).WithMany().HasForeignKey(s => s.MediaId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(s => s.Equipment).WithMany().HasForeignKey(s => s.EquipmentId).OnDelete(DeleteBehavior.Restrict);
    }
}
```

- [ ] **Step 6: Create `ConfirmatoryPlateObservationConfiguration.cs`**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class ConfirmatoryPlateObservationConfiguration : IEntityTypeConfiguration<ConfirmatoryPlateObservation>
{
    public void Configure(EntityTypeBuilder<ConfirmatoryPlateObservation> builder)
    {
        builder.HasKey(o => o.Id);

        builder.HasIndex(o => new { o.WorkflowStepResultId, o.MaterialId }).IsUnique();

        builder.HasOne(o => o.WorkflowStepResult)
            .WithMany(r => r.ConfirmatoryObservations)
            .HasForeignKey(o => o.WorkflowStepResultId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(o => o.Material)
            .WithMany()
            .HasForeignKey(o => o.MaterialId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
```

- [ ] **Step 7: Rewrite `IncubationConfiguration.cs`**

Drop the `Plate2Media` relationship; the rest is unchanged from the current file.

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

        builder.Ignore(i => i.IsIncubationComplete);
    }
}
```

- [ ] **Step 8: Rewrite `PathogenObservationConfiguration.cs`**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class PathogenObservationConfiguration : IEntityTypeConfiguration<PathogenObservation>
{
    public void Configure(EntityTypeBuilder<PathogenObservation> builder)
    {
        builder.HasKey(o => o.Id);

        builder.HasOne(o => o.Media)
            .WithMany()
            .HasForeignKey(o => o.MediaId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
```

- [ ] **Step 9: Add the four DbSets to `MicroLimsDbContext.cs`**

Add directly below the existing `public DbSet<TestWorkflowStep> TestWorkflowSteps => Set<TestWorkflowStep>();` line (currently line 78, under the `// Test Master` comment):

```csharp
    public DbSet<TestWorkflowStepMedia> TestWorkflowStepMedias => Set<TestWorkflowStepMedia>();
    public DbSet<WorkflowStepResult> WorkflowStepResults => Set<WorkflowStepResult>();
    public DbSet<ConfirmatoryMediaSelection> ConfirmatoryMediaSelections => Set<ConfirmatoryMediaSelection>();
    public DbSet<ConfirmatoryPlateObservation> ConfirmatoryPlateObservations => Set<ConfirmatoryPlateObservation>();
```

Nothing else in this file changes — `CaptureAuditEntries` picks the new entities up automatically because it iterates `ChangeTracker.Entries()` untyped.

- [ ] **Step 10: Rewrite `SeedWorkflowTemplates` in `DbSeeder.cs`**

Replace the whole `SeedWorkflowTemplates` region. The Salmonella `DualPlate` template becomes a five-stage `Observation` template; the generic two-step fallback becomes a three-stage one. Both need a `Material` per medium and an `Organism` to target, so this method now resolves those first and skips (with a console note, matching the existing style) when they are absent.

```csharp
    private static void SeedWorkflowTemplates(MicroLimsDbContext db)
    {
        var generalAgar = db.MediaTypes.First(m => m.Class == MediaClass.GeneralAgar);
        var generalBroth = db.MediaTypes.First(m => m.Class == MediaClass.GeneralBroth);
        var selectiveAgar = db.MediaTypes.First(m => m.Class == MediaClass.SelectiveAgar);
        var selectiveBroth = db.MediaTypes.First(m => m.Class == MediaClass.SelectiveBroth);

        SeedCountTestTemplate(db, "TAMC", generalAgar.Id);
        SeedCountTestTemplate(db, "TYMC", generalAgar.Id);

        SeedPathogenTemplate(db, "PATHOGEN_SALMONELLA", "Salmonella enterica",
            generalBroth.Id, selectiveBroth.Id, selectiveAgar.Id,
            selectivePlatingMedium: "XLD Agar",
            confirmatoryMedia: new[] { ("XLD Agar", 35m, 37m), ("TSI Agar", 35m, 37m) });

        foreach (var test in db.TestDefinitions
            .Where(t => t.WorkflowType == WorkflowType.Observation && !db.TestWorkflowSteps.Any(s => s.TestDefinitionId == t.Id))
            .ToList())
        {
            SeedPathogenTemplate(db, test.Code, organismScientificName: null,
                generalBroth.Id, selectiveBroth.Id, selectiveAgar.Id,
                selectivePlatingMedium: "Selective Agar",
                confirmatoryMedia: new[] { ("Selective Agar", 35m, 37m) });
        }
    }

    private static void SeedCountTestTemplate(MicroLimsDbContext db, string testCode, int generalAgarId)
    {
        var test = db.TestDefinitions.FirstOrDefault(t => t.Code == testCode);
        if (test is null) { Console.WriteLine($"Seed: {testCode} not in Test Master - workflow template skipped."); return; }
        if (db.TestWorkflowSteps.Any(s => s.TestDefinitionId == test.Id)) return;

        test.WorkflowType = WorkflowType.CountTest;
        db.TestWorkflowSteps.Add(new TestWorkflowStep
        {
            TestDefinitionId = test.Id, StepOrder = 1, StepName = "CountIncubation", MediaTypeId = generalAgarId,
            IncubationMinHours = 72, IncubationMaxHours = 120, TemperatureMin = 30, TemperatureMax = 35,
            IsFinalStep = true, StepType = StepType.PlateCount
        });
        db.SaveChanges();
    }

    // Five-stage pathogen chain. Every step gets exactly the StepMedia
    // rows its StepType requires (see WorkflowTemplateValidator's rules).
    private static void SeedPathogenTemplate(
        MicroLimsDbContext db, string testCode, string? organismScientificName,
        int generalBrothId, int selectiveBrothId, int selectiveAgarId,
        string selectivePlatingMedium, (string Name, decimal TempMin, decimal TempMax)[] confirmatoryMedia)
    {
        var test = db.TestDefinitions.FirstOrDefault(t => t.Code == testCode);
        if (test is null) { Console.WriteLine($"Seed: {testCode} not in Test Master - workflow template skipped."); return; }
        if (db.TestWorkflowSteps.Any(s => s.TestDefinitionId == test.Id)) return;

        var organismId = organismScientificName is null
            ? db.Organisms.Select(o => (int?)o.Id).FirstOrDefault()
            : db.Organisms.Where(o => o.ScientificName == organismScientificName).Select(o => (int?)o.Id).FirstOrDefault();
        if (organismId is null) { Console.WriteLine($"Seed: no Organism for {testCode} - workflow template skipped."); return; }

        test.WorkflowType = WorkflowType.Observation;

        var tsb = new TestWorkflowStep
        {
            TestDefinitionId = test.Id, StepOrder = 1, StepName = "Broth Enrichment", MediaTypeId = generalBrothId,
            IncubationMinHours = 18, IncubationMaxHours = 24, TemperatureMin = 35, TemperatureMax = 37,
            IsFinalStep = false, StepType = StepType.BrothEnrichment
        };
        var selectiveBroth = new TestWorkflowStep
        {
            TestDefinitionId = test.Id, StepOrder = 2, StepName = "Selective Broth", MediaTypeId = selectiveBrothId,
            IncubationMinHours = 18, IncubationMaxHours = 24, TemperatureMin = 41, TemperatureMax = 43,
            IsFinalStep = false, StepType = StepType.SelectiveBroth
        };
        var selectivePlating = new TestWorkflowStep
        {
            TestDefinitionId = test.Id, StepOrder = 3, StepName = "Selective Plating", MediaTypeId = selectiveAgarId,
            IncubationMinHours = 18, IncubationMaxHours = 24, TemperatureMin = 35, TemperatureMax = 37,
            IsFinalStep = false, StepType = StepType.SelectivePlating, TargetOrganismId = organismId
        };
        var confirmatory = new TestWorkflowStep
        {
            TestDefinitionId = test.Id, StepOrder = 4, StepName = "Confirmatory Plating", MediaTypeId = selectiveAgarId,
            IncubationMinHours = 18, IncubationMaxHours = 24, TemperatureMin = 35, TemperatureMax = 37,
            IsFinalStep = false, StepType = StepType.ConfirmatoryPlating, TargetOrganismId = organismId
        };
        var biochemical = new TestWorkflowStep
        {
            TestDefinitionId = test.Id, StepOrder = 5, StepName = "Biochemical Test", MediaTypeId = selectiveAgarId,
            IncubationMinHours = 0, IncubationMaxHours = 0, TemperatureMin = 35, TemperatureMax = 37,
            IsFinalStep = true, StepType = StepType.BiochemicalTest
        };
        db.TestWorkflowSteps.AddRange(tsb, selectiveBroth, selectivePlating, confirmatory, biochemical);
        db.SaveChanges();

        AddStepMedium(db, tsb.Id, "Tryptone Soya Broth", 35, 37, isRequired: true, order: 1);
        AddStepMedium(db, selectiveBroth.Id, "Rappaport Vassiliadis Broth", 41, 43, isRequired: true, order: 1);
        AddStepMedium(db, selectivePlating.Id, selectivePlatingMedium, 35, 37, isRequired: true, order: 1);
        for (var i = 0; i < confirmatoryMedia.Length; i++)
        {
            var (name, tempMin, tempMax) = confirmatoryMedia[i];
            AddStepMedium(db, confirmatory.Id, name, tempMin, tempMax, isRequired: false, order: i + 1);
        }
        db.SaveChanges();
    }

    // Resolves the medium by Material name, creating nothing - a missing
    // material means that medium simply is not offered, which the
    // template validator will surface the next time the step is saved.
    private static void AddStepMedium(MicroLimsDbContext db, int stepId, string materialName, decimal tempMin, decimal tempMax, bool isRequired, int order)
    {
        var materialId = db.Materials
            .Where(m => m.MaterialType == MaterialType.DehydratedMedia && m.MaterialName == materialName)
            .Select(m => (int?)m.Id).FirstOrDefault();
        if (materialId is null) { Console.WriteLine($"Seed: material '{materialName}' not found - step media skipped."); return; }

        db.TestWorkflowStepMedias.Add(new TestWorkflowStepMedia
        {
            TestWorkflowStepId = stepId, MaterialId = materialId.Value,
            TempMin = tempMin, TempMax = tempMax, IsRequired = isRequired, DisplayOrder = order
        });
    }
```

- [ ] **Step 11: Build Domain + Shared + Persistence**

```bash
dotnet build backend/MicroLIMS.Persistence/MicroLIMS.Persistence.csproj && dotnet build backend/MicroLIMS.Shared/MicroLIMS.Shared.csproj
```

Expected: PASS for both. `MicroLIMS.Application` and `MicroLIMS.API` are still red — Tasks 5–11 fix them.

- [ ] **Step 12: Commit**

```bash
git -C E:/MicroLIMS/MicroLIMS add backend/MicroLIMS.Domain backend/MicroLIMS.Persistence backend/MicroLIMS.Shared && git -C E:/MicroLIMS/MicroLIMS commit -m "refactor: pathogen workflow domain model and persistence configuration"
```

---

## Task 4: EF Core migration

**Files:**
- Create: `backend/MicroLIMS.Persistence/Migrations/<timestamp>_AddPathogenWorkflowRefactor.cs` (+ `.Designer.cs`, scaffolded)
- Modify: `backend/MicroLIMS.Persistence/Migrations/MicroLimsDbContextModelSnapshot.cs` (scaffolded)

**Interfaces:**
- Consumes: the entity + configuration shape from Tasks 2–3.
- Produces: a migration named `AddPathogenWorkflowRefactor`.

Enums persist as **integers** — confirmed, the codebase has no `HasConversion<string>()` anywhere and every enum column in the snapshot is `integer`. The scaffolder will emit `integer` columns automatically; do not hand-write conversions.

- [ ] **Step 1: Scaffold the migration**

```bash
dotnet ef migrations add AddPathogenWorkflowRefactor --project backend/MicroLIMS.Persistence --startup-project backend/MicroLIMS.API
```

If `dotnet ef` is missing: `dotnet tool install --global dotnet-ef --version 8.*`.

This will fail if `MicroLIMS.API` does not compile. If so, do Tasks 5–11 first and return here — the migration is the same either way. Note the deviation in the final report if the order had to change.

- [ ] **Step 2: Review the generated `Up()` against this checklist**

The generated migration must contain all of the following. Read the file and confirm each; if any is missing, the entity or configuration from Tasks 2–3 is wrong — fix that and re-scaffold rather than hand-editing the migration.

- `DropColumn` on `TestWorkflowSteps`: `IsDualPlate`, `Plate1DefaultLabel`, `Plate2DefaultLabel`
- `RenameColumn` on `TestWorkflowSteps`: `StepResultType` → `StepType`
- `AddColumn` on `TestWorkflowSteps`: `TargetOrganismId` (`integer`, nullable) + FK to `Organisms` + index
- `CreateTable` `TestWorkflowStepMedias` with unique index on `(TestWorkflowStepId, MaterialId)` and cascade delete from `TestWorkflowSteps`
- `CreateTable` `WorkflowStepResults` with unique index on `IncubationId`
- `CreateTable` `ConfirmatoryMediaSelections` with unique index on `(WorkflowStepResultId, MaterialId)` and cascade delete from `WorkflowStepResults`
- `CreateTable` `ConfirmatoryPlateObservations` with unique index on `(WorkflowStepResultId, MaterialId)` and cascade delete from `WorkflowStepResults`
- `DropForeignKey`/`DropIndex`/`DropColumn` for `Incubations.Plate2MediaId`
- `AddColumn` on `Incubations`: `IncubationStartUtc`, `IncubationEndUtc` (both `timestamp with time zone`, nullable)
- `DropColumn` on `PathogenObservations`: `GrowthObserved`, `PlateLabel`; `AddColumn` `Observation` (`integer`, not null, default 0)

- [ ] **Step 3: Add the DualPlate data migration by hand**

The scaffolder cannot know that existing `DualPlate` rows need re-homing. Add this as the **first** statement inside `Up()`, before any schema change:

```csharp
            // WorkflowType.DualPlate (ordinal 2) is removed; the enum now
            // stops at Observation (1). Any test still on DualPlate becomes
            // an Observation test - its steps are re-typed below.
            migrationBuilder.Sql(@"UPDATE ""TestDefinitions"" SET ""WorkflowType"" = 1 WHERE ""WorkflowType"" = 2;");

            // Old StepResultType ordinals: 0 PlateCount, 1 Growth, 2 DualGrowth.
            // New StepType ordinals: 0 PlateCount, 3 SelectivePlating, 4 ConfirmatoryPlating.
            // Growth steps become SelectivePlating and DualGrowth steps become
            // ConfirmatoryPlating - the closest behavioural equivalents. Both
            // now require a TargetOrganismId and StepMedia rows, which no
            // existing row has, so every migrated template must be re-saved
            // through Test Master before it will pass WorkflowTemplateValidator.
            // FLAGGED FOR MANUAL REVIEW: Salmonella's XLD_TSI step in particular
            // needs its two confirmatory media (XLD, TSI) re-entered by hand.
            migrationBuilder.Sql(@"UPDATE ""TestWorkflowSteps"" SET ""StepResultType"" = 4 WHERE ""StepResultType"" = 2;");
            migrationBuilder.Sql(@"UPDATE ""TestWorkflowSteps"" SET ""StepResultType"" = 3 WHERE ""StepResultType"" = 1;");

            // GrowthObserved bool -> GrowthObservation enum: true became
            // GrowthConforming (2), false became NoGrowth (0). The old model
            // had no way to express GrowthNonConforming, so nothing maps to 1.
            migrationBuilder.AddColumn<int>(
                name: "Observation", table: "PathogenObservations", type: "integer", nullable: false, defaultValue: 0);
            migrationBuilder.Sql(@"UPDATE ""PathogenObservations"" SET ""Observation"" = CASE WHEN ""GrowthObserved"" THEN 2 ELSE 0 END;");
```

Then **delete** the scaffolder's own `AddColumn` for `PathogenObservations.Observation` (it is now added above) and make sure the `DropColumn` for `GrowthObserved` still runs *after* these statements.

The two `StepResultType` updates run before the `RenameColumn`, so they must use the **old** column name — that is why they read `"StepResultType"`, not `"StepType"`. Confirm the `RenameColumn` call appears after them in the file; move it if not.

- [ ] **Step 4: Mirror the data migration in `Down()`**

```csharp
            migrationBuilder.Sql(@"UPDATE ""TestWorkflowSteps"" SET ""StepType"" = 2 WHERE ""StepType"" = 4;");
            migrationBuilder.Sql(@"UPDATE ""TestWorkflowSteps"" SET ""StepType"" = 1 WHERE ""StepType"" = 3;");
```

Place these before the `RenameColumn` back to `StepResultType`. `WorkflowType` is not reverted — a test that was DualPlate cannot be identified after the fact, and guessing would corrupt data.

- [ ] **Step 5: Verify the migration applies to a scratch database**

```bash
dotnet ef database update --project backend/MicroLIMS.Persistence --startup-project backend/MicroLIMS.API
```

Expected: applies with no error. If no development database is reachable, verify the SQL instead and note it in the report:

```bash
dotnet ef migrations script --idempotent --project backend/MicroLIMS.Persistence --startup-project backend/MicroLIMS.API --output backend/artifacts/pathogen-refactor.sql
```

- [ ] **Step 6: Commit**

```bash
git -C E:/MicroLIMS/MicroLIMS add backend/MicroLIMS.Persistence/Migrations && git -C E:/MicroLIMS/MicroLIMS commit -m "feat: add AddPathogenWorkflowRefactor migration"
```

---

## Task 5: Template save validation (spec 3.1)

**Files:**
- Create: `backend/MicroLIMS.Application/Services/WorkflowTemplateValidator.cs`
- Create: `backend/MicroLIMS.Tests/WorkflowTests/WorkflowTemplateValidationTests.cs`

**Interfaces:**
- Consumes: `TestWorkflowStep`, `TestWorkflowStepMedia`, `StepType`.
- Produces: `record TemplateValidationError(int RuleNumber, string StepName, string Message)`; `static class WorkflowTemplateValidator` with `IReadOnlyList<TemplateValidationError> Validate(TestWorkflowStep step)`. Both in namespace `MicroLIMS.Application.Services`.

A pure static function over one step and its `StepMedia` — no database access, so it is trivially testable and callable from both the create and update paths in `MasterDataController`.

- [ ] **Step 1: Write the failing tests**

Create `backend/MicroLIMS.Tests/WorkflowTests/WorkflowTemplateValidationTests.cs`:

```csharp
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using Xunit;

namespace MicroLIMS.Tests.WorkflowTests;

// The six structural rules a workflow step template must satisfy before
// it can be saved (spec 3.1). Pure function - no database.
public class WorkflowTemplateValidationTests
{
    private static TestWorkflowStep Step(StepType type, int? organismId, params TestWorkflowStepMedia[] media)
    {
        var step = new TestWorkflowStep { StepName = "S", StepType = type, TargetOrganismId = organismId };
        step.StepMedia.AddRange(media);
        return step;
    }

    private static TestWorkflowStepMedia Medium(int materialId, bool isRequired, decimal tempMin = 35, decimal tempMax = 37) =>
        new() { MaterialId = materialId, IsRequired = isRequired, TempMin = tempMin, TempMax = tempMax };

    [Theory]
    [InlineData(StepType.BrothEnrichment)]
    [InlineData(StepType.SelectiveBroth)]
    public void Rule1_BrothStep_WithExactlyOneRequiredMedium_IsValid(StepType type)
    {
        var errors = WorkflowTemplateValidator.Validate(Step(type, null, Medium(1, isRequired: true)));
        Assert.Empty(errors);
    }

    [Theory]
    [InlineData(StepType.BrothEnrichment)]
    [InlineData(StepType.SelectiveBroth)]
    public void Rule1_BrothStep_WithTwoMedia_FailsRule1(StepType type)
    {
        var errors = WorkflowTemplateValidator.Validate(Step(type, null, Medium(1, true), Medium(2, true)));
        Assert.Contains(errors, e => e.RuleNumber == 1);
    }

    [Fact]
    public void Rule1_BrothStep_WithOptionalMedium_FailsRule1()
    {
        var errors = WorkflowTemplateValidator.Validate(Step(StepType.BrothEnrichment, null, Medium(1, isRequired: false)));
        Assert.Contains(errors, e => e.RuleNumber == 1);
    }

    [Fact]
    public void Rule2_SelectivePlating_WithOneRequiredMediumAndOrganism_IsValid()
    {
        var errors = WorkflowTemplateValidator.Validate(Step(StepType.SelectivePlating, organismId: 7, Medium(1, true)));
        Assert.Empty(errors);
    }

    [Fact]
    public void Rule2_SelectivePlating_WithoutOrganism_FailsRule2()
    {
        var errors = WorkflowTemplateValidator.Validate(Step(StepType.SelectivePlating, organismId: null, Medium(1, true)));
        Assert.Contains(errors, e => e.RuleNumber == 2);
    }

    [Fact]
    public void Rule3_ConfirmatoryPlating_WithTwoOptionalMediaAndOrganism_IsValid()
    {
        var errors = WorkflowTemplateValidator.Validate(
            Step(StepType.ConfirmatoryPlating, organismId: 7, Medium(1, false), Medium(2, false)));
        Assert.Empty(errors);
    }

    [Fact]
    public void Rule3_ConfirmatoryPlating_WithNoMedia_FailsRule3()
    {
        var errors = WorkflowTemplateValidator.Validate(Step(StepType.ConfirmatoryPlating, organismId: 7));
        Assert.Contains(errors, e => e.RuleNumber == 3);
    }

    [Fact]
    public void Rule3_ConfirmatoryPlating_WithRequiredMedium_FailsRule3()
    {
        var errors = WorkflowTemplateValidator.Validate(
            Step(StepType.ConfirmatoryPlating, organismId: 7, Medium(1, isRequired: true)));
        Assert.Contains(errors, e => e.RuleNumber == 3);
    }

    [Fact]
    public void Rule4_BiochemicalTest_WithNoMediaAndNoOrganism_IsValid()
    {
        var errors = WorkflowTemplateValidator.Validate(Step(StepType.BiochemicalTest, null));
        Assert.Empty(errors);
    }

    [Fact]
    public void Rule4_BiochemicalTest_WithMedium_FailsRule4()
    {
        var errors = WorkflowTemplateValidator.Validate(Step(StepType.BiochemicalTest, null, Medium(1, true)));
        Assert.Contains(errors, e => e.RuleNumber == 4);
    }

    [Fact]
    public void Rule4_BiochemicalTest_WithOrganism_FailsRule4()
    {
        var errors = WorkflowTemplateValidator.Validate(Step(StepType.BiochemicalTest, organismId: 7));
        Assert.Contains(errors, e => e.RuleNumber == 4);
    }

    [Fact]
    public void Rule5_TempMinNotBelowTempMax_FailsRule5()
    {
        var errors = WorkflowTemplateValidator.Validate(
            Step(StepType.BrothEnrichment, null, Medium(1, true, tempMin: 37, tempMax: 35)));
        Assert.Contains(errors, e => e.RuleNumber == 5);
    }

    [Fact]
    public void Rule6_DuplicateMaterial_FailsRule6()
    {
        var errors = WorkflowTemplateValidator.Validate(
            Step(StepType.ConfirmatoryPlating, organismId: 7, Medium(1, false), Medium(1, false)));
        Assert.Contains(errors, e => e.RuleNumber == 6);
    }

    [Fact]
    public void PlateCountStep_IsNotSubjectToPathogenRules()
    {
        var errors = WorkflowTemplateValidator.Validate(Step(StepType.PlateCount, null));
        Assert.Empty(errors);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

```bash
dotnet test backend/MicroLIMS.Tests/MicroLIMS.Tests.csproj --filter "FullyQualifiedName~WorkflowTemplateValidationTests"
```

Expected: compile error `CS0103: The name 'WorkflowTemplateValidator' does not exist in the current context`.

- [ ] **Step 3: Implement `WorkflowTemplateValidator.cs`**

```csharp
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Services;

public record TemplateValidationError(int RuleNumber, string StepName, string Message);

// The structural rules a pathogen workflow step must satisfy before it
// can be saved (spec 3.1). Returns every violation rather than throwing
// on the first, so the Test Master screen can show them all at once.
public static class WorkflowTemplateValidator
{
    public static IReadOnlyList<TemplateValidationError> Validate(TestWorkflowStep step)
    {
        var errors = new List<TemplateValidationError>();
        var media = step.StepMedia;

        void Fail(int rule, string message) => errors.Add(new TemplateValidationError(rule, step.StepName, message));

        switch (step.StepType)
        {
            case StepType.BrothEnrichment:
            case StepType.SelectiveBroth:
                if (media.Count != 1 || !media[0].IsRequired)
                    Fail(1, "A broth step must have exactly one assigned medium, marked as required.");
                if (step.TargetOrganismId is not null)
                    Fail(1, "A broth step must not target an organism.");
                break;

            case StepType.SelectivePlating:
                if (media.Count != 1 || !media[0].IsRequired)
                    Fail(2, "A selective plating step must have exactly one assigned medium, marked as required.");
                if (step.TargetOrganismId is null)
                    Fail(2, "A selective plating step must target an organism.");
                break;

            case StepType.ConfirmatoryPlating:
                if (media.Count < 1 || media.Any(m => m.IsRequired))
                    Fail(3, "A confirmatory plating step must have at least one permitted medium, all analyst-selectable.");
                if (step.TargetOrganismId is null)
                    Fail(3, "A confirmatory plating step must target an organism.");
                break;

            case StepType.BiochemicalTest:
                if (media.Count != 0)
                    Fail(4, "A biochemical test step must have no assigned media.");
                if (step.TargetOrganismId is not null)
                    Fail(4, "A biochemical test step must not target an organism.");
                break;
        }

        foreach (var medium in media.Where(m => m.TempMin >= m.TempMax))
            Fail(5, $"Medium {medium.MaterialId}: the minimum temperature must be below the maximum.");

        foreach (var duplicate in media.GroupBy(m => m.MaterialId).Where(g => g.Count() > 1))
            Fail(6, $"Medium {duplicate.Key} is assigned to this step more than once.");

        return errors;
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

```bash
dotnet test backend/MicroLIMS.Tests/MicroLIMS.Tests.csproj --filter "FullyQualifiedName~WorkflowTemplateValidationTests"
```

Expected: PASS, 15 tests.

- [ ] **Step 5: Commit**

```bash
git -C E:/MicroLIMS/MicroLIMS add backend/MicroLIMS.Application/Services/WorkflowTemplateValidator.cs backend/MicroLIMS.Tests/WorkflowTests/WorkflowTemplateValidationTests.cs && git -C E:/MicroLIMS/MicroLIMS commit -m "feat: add workflow template structural validation"
```

---

## Task 6: Incubator eligibility service (spec 3.2)

**Files:**
- Create: `backend/MicroLIMS.Application/Services/IncubatorEligibilityService.cs`
- Create: `backend/MicroLIMS.Tests/WorkflowTests/IncubatorEligibilityTests.cs`
- Modify: `backend/MicroLIMS.Tests/TestServiceFactory.cs`
- Modify: `backend/MicroLIMS.API/Extensions/ServiceCollectionExtensions.cs`

**Interfaces:**
- Consumes: `TestWorkflowStepMedia`, `Equipment`, `EquipmentType`.
- Produces: `record EligibleIncubatorDto(int Id, string Name, string Code, decimal? SetTemperature, string CalibrationStatus)`; `class IncubatorEligibilityService` with constructor `(MicroLimsDbContext db)` and methods:
  - `Task<IReadOnlyList<EligibleIncubatorDto>> GetEligibleIncubatorsAsync(int stepMediaId, CancellationToken cancellationToken = default)`
  - `Task<bool> IsWithinRangeAsync(int stepMediaId, int equipmentId, CancellationToken cancellationToken = default)`

`Equipment` has **no `IsActive` column** — eligibility is `Type == Incubator`, a non-null `SetPointTemperature` inside `[TempMin, TempMax]`, and calibration not past due. `CalibrationStatus` is derived: `"Current"` when `CalibrationDueDate` is null or in the future, `"Overdue"` otherwise; overdue incubators are excluded from the list entirely.

- [ ] **Step 1: Write the failing tests**

Create `backend/MicroLIMS.Tests/WorkflowTests/IncubatorEligibilityTests.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.WorkflowTests;

public class IncubatorEligibilityTests
{
    private static MicroLimsDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MicroLimsDbContext(options);
    }

    private static async Task<int> SeedStepMediaAsync(MicroLimsDbContext db, decimal tempMin, decimal tempMax)
    {
        var stepMedia = new TestWorkflowStepMedia { TestWorkflowStepId = 1, MaterialId = 1, TempMin = tempMin, TempMax = tempMax, IsRequired = true, DisplayOrder = 1 };
        db.TestWorkflowStepMedias.Add(stepMedia);
        await db.SaveChangesAsync();
        return stepMedia.Id;
    }

    private static Equipment Incubator(string code, decimal? setPoint, DateTime? calibrationDue = null) =>
        new() { Name = code, Code = code, Type = EquipmentType.Incubator, SetPointTemperature = setPoint, CalibrationDueDate = calibrationDue };

    [Fact]
    public async Task InRangeIncubator_IsReturned()
    {
        await using var db = NewDb();
        var stepMediaId = await SeedStepMediaAsync(db, 35, 37);
        db.Equipment.Add(Incubator("INC-03", 35));
        await db.SaveChangesAsync();

        var result = await new IncubatorEligibilityService(db).GetEligibleIncubatorsAsync(stepMediaId);

        Assert.Single(result);
        Assert.Equal("INC-03", result[0].Code);
        Assert.Equal("Current", result[0].CalibrationStatus);
    }

    [Fact]
    public async Task OutOfRangeIncubator_IsExcluded()
    {
        await using var db = NewDb();
        var stepMediaId = await SeedStepMediaAsync(db, 35, 37);
        db.Equipment.Add(Incubator("INC-09", 43));
        await db.SaveChangesAsync();

        Assert.Empty(await new IncubatorEligibilityService(db).GetEligibleIncubatorsAsync(stepMediaId));
    }

    [Fact]
    public async Task NonIncubatorEquipment_IsExcluded()
    {
        await using var db = NewDb();
        var stepMediaId = await SeedStepMediaAsync(db, 35, 37);
        db.Equipment.Add(new Equipment { Name = "AUT-01", Code = "AUT-01", Type = EquipmentType.Autoclave, SetPointTemperature = 36 });
        await db.SaveChangesAsync();

        Assert.Empty(await new IncubatorEligibilityService(db).GetEligibleIncubatorsAsync(stepMediaId));
    }

    [Fact]
    public async Task IncubatorWithNoSetPoint_IsExcluded()
    {
        await using var db = NewDb();
        var stepMediaId = await SeedStepMediaAsync(db, 35, 37);
        db.Equipment.Add(Incubator("INC-11", null));
        await db.SaveChangesAsync();

        Assert.Empty(await new IncubatorEligibilityService(db).GetEligibleIncubatorsAsync(stepMediaId));
    }

    [Fact]
    public async Task OverdueCalibration_IsExcluded()
    {
        await using var db = NewDb();
        var stepMediaId = await SeedStepMediaAsync(db, 35, 37);
        db.Equipment.Add(Incubator("INC-04", 36, calibrationDue: DateTime.UtcNow.AddDays(-1)));
        await db.SaveChangesAsync();

        Assert.Empty(await new IncubatorEligibilityService(db).GetEligibleIncubatorsAsync(stepMediaId));
    }

    [Fact]
    public async Task BoundaryTemperatures_AreInclusive()
    {
        await using var db = NewDb();
        var stepMediaId = await SeedStepMediaAsync(db, 35, 37);
        db.Equipment.AddRange(Incubator("INC-LOW", 35), Incubator("INC-HIGH", 37));
        await db.SaveChangesAsync();

        Assert.Equal(2, (await new IncubatorEligibilityService(db).GetEligibleIncubatorsAsync(stepMediaId)).Count);
    }

    [Fact]
    public async Task IsWithinRangeAsync_MatchesListMembership()
    {
        await using var db = NewDb();
        var stepMediaId = await SeedStepMediaAsync(db, 35, 37);
        var good = Incubator("INC-03", 36);
        var bad = Incubator("INC-09", 43);
        db.Equipment.AddRange(good, bad);
        await db.SaveChangesAsync();

        var service = new IncubatorEligibilityService(db);
        Assert.True(await service.IsWithinRangeAsync(stepMediaId, good.Id));
        Assert.False(await service.IsWithinRangeAsync(stepMediaId, bad.Id));
    }

    [Fact]
    public async Task UnknownStepMedia_Throws()
    {
        await using var db = NewDb();
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => new IncubatorEligibilityService(db).GetEligibleIncubatorsAsync(999));
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

```bash
dotnet test backend/MicroLIMS.Tests/MicroLIMS.Tests.csproj --filter "FullyQualifiedName~IncubatorEligibilityTests"
```

Expected: compile error `CS0246: The type or namespace name 'IncubatorEligibilityService' could not be found`.

- [ ] **Step 3: Implement `IncubatorEligibilityService.cs`**

```csharp
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Application.Services;

public record EligibleIncubatorDto(int Id, string Name, string Code, decimal? SetTemperature, string CalibrationStatus);

// Incubator selection was previously filtered in the browser only. This
// makes the temperature window an enforced, server-side fact so a
// hand-crafted request cannot assign an out-of-range incubator.
public class IncubatorEligibilityService
{
    private readonly MicroLimsDbContext _db;

    public IncubatorEligibilityService(MicroLimsDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<EligibleIncubatorDto>> GetEligibleIncubatorsAsync(
        int stepMediaId, CancellationToken cancellationToken = default)
    {
        var stepMedia = await _db.TestWorkflowStepMedias
            .FirstOrDefaultAsync(m => m.Id == stepMediaId, cancellationToken)
            ?? throw new InvalidOperationException($"Step media {stepMediaId} not found.");

        var now = DateTime.UtcNow;

        return await _db.Equipment
            .Where(e => e.Type == EquipmentType.Incubator
                     && e.SetPointTemperature != null
                     && e.SetPointTemperature >= stepMedia.TempMin
                     && e.SetPointTemperature <= stepMedia.TempMax
                     && (e.CalibrationDueDate == null || e.CalibrationDueDate >= now))
            .OrderBy(e => e.Code)
            .Select(e => new EligibleIncubatorDto(e.Id, e.Name, e.Code, e.SetPointTemperature, "Current"))
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> IsWithinRangeAsync(int stepMediaId, int equipmentId, CancellationToken cancellationToken = default)
    {
        var eligible = await GetEligibleIncubatorsAsync(stepMediaId, cancellationToken);
        return eligible.Any(e => e.Id == equipmentId);
    }
}
```

- [ ] **Step 4: Register the service in both containers**

In `backend/MicroLIMS.API/Extensions/ServiceCollectionExtensions.cs`, alongside the other `AddScoped` service registrations:

```csharp
        services.AddScoped<IncubatorEligibilityService>();
```

In `backend/MicroLIMS.Tests/TestServiceFactory.cs`, alongside the other factory methods:

```csharp
    public static IncubatorEligibilityService IncubatorEligibility(MicroLimsDbContext db) => new(db);
```

- [ ] **Step 5: Run the tests to verify they pass**

```bash
dotnet test backend/MicroLIMS.Tests/MicroLIMS.Tests.csproj --filter "FullyQualifiedName~IncubatorEligibilityTests"
```

Expected: PASS, 8 tests.

- [ ] **Step 6: Commit**

```bash
git -C E:/MicroLIMS/MicroLIMS add backend/MicroLIMS.Application backend/MicroLIMS.API backend/MicroLIMS.Tests && git -C E:/MicroLIMS/MicroLIMS commit -m "feat: add server-side incubator eligibility filtering"
```

---

## Task 7: Media appearance snapshot service (spec 3.3)

**Files:**
- Create: `backend/MicroLIMS.Application/Services/MediaAppearanceSnapshotService.cs`
- Create: `backend/MicroLIMS.Tests/WorkflowTests/MediaAppearanceSnapshotTests.cs`
- Modify: `backend/MicroLIMS.Tests/TestServiceFactory.cs`
- Modify: `backend/MicroLIMS.API/Extensions/ServiceCollectionExtensions.cs`

**Interfaces:**
- Consumes: `MediaChallengeSpec`, `Material`.
- Produces: `class MediaAppearanceSnapshotService` with constructor `(MicroLimsDbContext db, ILogger<MediaAppearanceSnapshotService> logger)` and `Task<string?> GetExpectedAppearanceSnapshotAsync(int materialId, int organismId, CancellationToken cancellationToken = default)`.

`MediaChallengeSpec` keys on `MaterialName` (a string), not `MaterialId` — so this resolves the `Material` first and matches on its name. A missing spec returns `null` and logs a warning; it never blocks result entry.

- [ ] **Step 1: Write the failing tests**

Create `backend/MicroLIMS.Tests/WorkflowTests/MediaAppearanceSnapshotTests.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.WorkflowTests;

public class MediaAppearanceSnapshotTests
{
    private static MicroLimsDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MicroLimsDbContext(options);
    }

    private static MediaAppearanceSnapshotService Service(MicroLimsDbContext db) =>
        new(db, NullLogger<MediaAppearanceSnapshotService>.Instance);

    private static async Task<(int materialId, int organismId)> SeedAsync(MicroLimsDbContext db, string? expectedDescription)
    {
        var organism = new Organism { ScientificName = "Escherichia coli" };
        db.Organisms.Add(organism);
        var material = new Material
        {
            MaterialType = MaterialType.DehydratedMedia, MaterialName = "EMB Agar", ManufacturerName = "Himedia",
            BatchNumber = "B-1", ReceivingDate = DateTime.UtcNow, Location = "Micro Lab",
            QuantityReceived = 100, QuantityRemaining = 100, Unit = MaterialUnit.Gram
        };
        db.Materials.Add(material);
        await db.SaveChangesAsync();

        if (expectedDescription is not null)
        {
            db.MediaChallengeSpecs.Add(new MediaChallengeSpec
            {
                MaterialName = "EMB Agar", EvaluationType = EvaluationType.GrowthPromotion,
                OrganismId = organism.Id, ExpectedDescription = expectedDescription
            });
            await db.SaveChangesAsync();
        }

        return (material.Id, organism.Id);
    }

    [Fact]
    public async Task ReturnsExpectedDescription_WhenSpecExists()
    {
        await using var db = NewDb();
        var (materialId, organismId) = await SeedAsync(db, "Metallic green sheen colonies, 1-2 mm");

        var snapshot = await Service(db).GetExpectedAppearanceSnapshotAsync(materialId, organismId);

        Assert.Equal("Metallic green sheen colonies, 1-2 mm", snapshot);
    }

    [Fact]
    public async Task ReturnsNull_WhenNoSpecExists()
    {
        await using var db = NewDb();
        var (materialId, organismId) = await SeedAsync(db, null);

        Assert.Null(await Service(db).GetExpectedAppearanceSnapshotAsync(materialId, organismId));
    }

    [Fact]
    public async Task ReturnsNull_WhenSpecExistsForADifferentOrganism()
    {
        await using var db = NewDb();
        var (materialId, _) = await SeedAsync(db, "Metallic green sheen colonies");
        var other = new Organism { ScientificName = "Salmonella enterica" };
        db.Organisms.Add(other);
        await db.SaveChangesAsync();

        Assert.Null(await Service(db).GetExpectedAppearanceSnapshotAsync(materialId, other.Id));
    }

    [Fact]
    public async Task ReturnsNull_WhenMaterialIsUnknown()
    {
        await using var db = NewDb();
        var (_, organismId) = await SeedAsync(db, "Metallic green sheen colonies");

        Assert.Null(await Service(db).GetExpectedAppearanceSnapshotAsync(999, organismId));
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

```bash
dotnet test backend/MicroLIMS.Tests/MicroLIMS.Tests.csproj --filter "FullyQualifiedName~MediaAppearanceSnapshotTests"
```

Expected: compile error `CS0246: The type or namespace name 'MediaAppearanceSnapshotService' could not be found`.

- [ ] **Step 3: Implement `MediaAppearanceSnapshotService.cs`**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Application.Services;

// Reads the expected colony appearance for a medium/organism pair at the
// moment an observation is submitted. The caller stores the returned
// string on the result row and never updates it again - it is the
// criteria as they stood when the analyst looked at the plate
// (ALCOA+ Original and Contemporaneous).
//
// MediaChallengeSpec keys on MaterialName rather than MaterialId, so the
// material is resolved to its name first.
public class MediaAppearanceSnapshotService
{
    private readonly MicroLimsDbContext _db;
    private readonly ILogger<MediaAppearanceSnapshotService> _logger;

    public MediaAppearanceSnapshotService(MicroLimsDbContext db, ILogger<MediaAppearanceSnapshotService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<string?> GetExpectedAppearanceSnapshotAsync(
        int materialId, int organismId, CancellationToken cancellationToken = default)
    {
        var materialName = await _db.Materials
            .Where(m => m.Id == materialId)
            .Select(m => m.MaterialName)
            .FirstOrDefaultAsync(cancellationToken);

        if (materialName is null)
        {
            _logger.LogWarning("No material {MaterialId} - appearance snapshot recorded as null.", materialId);
            return null;
        }

        var expected = await _db.MediaChallengeSpecs
            .Where(s => s.MaterialName == materialName && s.OrganismId == organismId)
            .Select(s => s.ExpectedDescription)
            .FirstOrDefaultAsync(cancellationToken);

        if (expected is null)
            _logger.LogWarning(
                "No MediaChallengeSpec for material '{MaterialName}' and organism {OrganismId} - appearance snapshot recorded as null.",
                materialName, organismId);

        return expected;
    }
}
```

- [ ] **Step 4: Register the service in both containers**

In `ServiceCollectionExtensions.cs`:

```csharp
        services.AddScoped<MediaAppearanceSnapshotService>();
```

In `TestServiceFactory.cs`:

```csharp
    public static MediaAppearanceSnapshotService AppearanceSnapshot(MicroLimsDbContext db) =>
        new(db, NullLogger<MediaAppearanceSnapshotService>.Instance);
```

- [ ] **Step 5: Run the tests to verify they pass**

```bash
dotnet test backend/MicroLIMS.Tests/MicroLIMS.Tests.csproj --filter "FullyQualifiedName~MediaAppearanceSnapshotTests"
```

Expected: PASS, 4 tests.

- [ ] **Step 6: Commit**

```bash
git -C E:/MicroLIMS/MicroLIMS add backend/MicroLIMS.Application backend/MicroLIMS.API backend/MicroLIMS.Tests && git -C E:/MicroLIMS/MicroLIMS commit -m "feat: add media appearance snapshot service"
```

---

## Task 8: Engine surface — remove dual-plate, add broth submission and the incubation lock

**Files:**
- Modify: `backend/MicroLIMS.Application/Workflows/TestWorkflowEngine.cs`
- Modify: `backend/MicroLIMS.Application/Services/ResultProjectionService.cs`
- Modify: `backend/MicroLIMS.Application/Services/SegregationOfDutiesGuard.cs`
- Modify: `backend/MicroLIMS.Tests/TestServiceFactory.cs`
- Create: `backend/MicroLIMS.Tests/WorkflowTests/IncubationLockTests.cs`

**Interfaces:**
- Consumes: `IncubatorEligibilityService`, `MediaAppearanceSnapshotService`, `WorkflowErrorCodes`, all Task 2 entities.
- Produces (all in namespace `MicroLIMS.Application.Workflows`):
  - `class WorkflowStepException : InvalidOperationException` with `string ErrorCode { get; }`, `long? RemainingSeconds { get; }`, constructor `(string errorCode, string message, long? remainingSeconds = null)`.
  - `record StepResultDto(int StepInstanceId, string StepType, string Status, int SubmittedByUserId, DateTime SubmittedAtUtc, bool NextStepUnlocked, string? WorkflowFinalResult, List<string> Flags)`.
  - `Task<StepResultDto> SubmitBrothAsync(int testOrderId, string stepName, int mediaLotId, int equipmentId, DateTime incubationStartUtc, DateTime incubationEndUtc, string? observation, int userId)` on `ITestWorkflowEngine`.
  - `TestWorkflowEngine` constructor becomes `(MicroLimsDbContext db, SampleReviewService sampleReviewService, ResultProjectionService resultProjection, IncubatorEligibilityService incubatorEligibility, MediaAppearanceSnapshotService appearanceSnapshot)`.
- Removed from `ITestWorkflowEngine`: `DualPlatePayload`, `BatchLocationDualPlateObservation`, the `plate2MediaId`/`plate1Label`/`plate2Label` parameters on `SelectMediaAsync`, and the `dualPlateObservations` parameter on `RecordBatchPathogenResultsAsync`.

- [ ] **Step 1: Write the failing tests**

Create `backend/MicroLIMS.Tests/WorkflowTests/IncubationLockTests.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Constants;
using Xunit;

namespace MicroLIMS.Tests.WorkflowTests;

// The incubation lock applies to BrothEnrichment, SelectiveBroth and
// ConfirmatoryPlating steps only (spec 3.5).
public class IncubationLockTests
{
    [Fact]
    public void IsIncubationComplete_IsFalse_BeforeTheWindowEnds()
    {
        var incubation = new Incubation { IncubationEndUtc = DateTime.UtcNow.AddHours(4) };
        Assert.False(incubation.IsIncubationComplete);
    }

    [Fact]
    public void IsIncubationComplete_IsTrue_AfterTheWindowEnds()
    {
        var incubation = new Incubation { IncubationEndUtc = DateTime.UtcNow.AddSeconds(-1) };
        Assert.True(incubation.IsIncubationComplete);
    }

    [Fact]
    public void IsIncubationComplete_IsFalse_WhenNoWindowIsSet()
    {
        Assert.False(new Incubation().IsIncubationComplete);
    }

    [Fact]
    public void WorkflowStepException_CarriesCodeAndRemainingSeconds()
    {
        var ex = new WorkflowStepException(WorkflowErrorCodes.IncubationNotComplete, "Still incubating.", 52320);
        Assert.Equal("INCUBATION_NOT_COMPLETE", ex.ErrorCode);
        Assert.Equal(52320, ex.RemainingSeconds);
    }

    [Fact]
    public async Task SubmitBrothAsync_RecordsTheIncubationWindowAndDoesNotSetAFinalResult()
    {
        await using var db = PathogenTestData.NewDb();
        var (order, media, incubator) = await PathogenTestData.SeedFiveStageOrderAsync(db);
        var engine = TestServiceFactory.TestWorkflow(db);

        var start = DateTime.UtcNow.AddHours(-30);
        var end = DateTime.UtcNow.AddHours(-6);
        var result = await engine.SubmitBrothAsync(order.Id, "Broth Enrichment", media.BrothLotId, incubator.Id, start, end, "Slight turbidity.", userId: 4);

        var incubation = await db.Incubations.SingleAsync(i => i.TestOrderId == order.Id && i.StepName == "Broth Enrichment");
        Assert.Equal(start, incubation.IncubationStartUtc);
        Assert.Equal(end, incubation.IncubationEndUtc);
        Assert.Null(result.WorkflowFinalResult);
        Assert.True(result.NextStepUnlocked);
    }

    [Fact]
    public async Task SubmitBrothAsync_BeforeTheWindowEnds_ThrowsIncubationNotComplete()
    {
        await using var db = PathogenTestData.NewDb();
        var (order, media, incubator) = await PathogenTestData.SeedFiveStageOrderAsync(db);
        var engine = TestServiceFactory.TestWorkflow(db);

        var ex = await Assert.ThrowsAsync<WorkflowStepException>(() => engine.SubmitBrothAsync(
            order.Id, "Broth Enrichment", media.BrothLotId, incubator.Id,
            DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddHours(23), null, userId: 4));

        Assert.Equal(WorkflowErrorCodes.IncubationNotComplete, ex.ErrorCode);
        Assert.NotNull(ex.RemainingSeconds);
        Assert.True(ex.RemainingSeconds > 0);
    }

    [Fact]
    public async Task SubmitBrothAsync_WithAnOutOfRangeIncubator_ThrowsTempOutOfRange()
    {
        await using var db = PathogenTestData.NewDb();
        var (order, media, _) = await PathogenTestData.SeedFiveStageOrderAsync(db);
        var wrongIncubator = new Equipment { Name = "INC-99", Code = "INC-99", Type = EquipmentType.Incubator, SetPointTemperature = 55 };
        db.Equipment.Add(wrongIncubator);
        await db.SaveChangesAsync();
        var engine = TestServiceFactory.TestWorkflow(db);

        var ex = await Assert.ThrowsAsync<WorkflowStepException>(() => engine.SubmitBrothAsync(
            order.Id, "Broth Enrichment", media.BrothLotId, wrongIncubator.Id,
            DateTime.UtcNow.AddHours(-30), DateTime.UtcNow.AddHours(-6), null, userId: 4));

        Assert.Equal(WorkflowErrorCodes.IncubatorTempOutOfRange, ex.ErrorCode);
    }
}
```

- [ ] **Step 2: Write the shared test fixture the tests above depend on**

Create `backend/MicroLIMS.Tests/WorkflowTests/PathogenTestData.cs`. Every pathogen test in Tasks 8–11 builds its world through this, so the five-stage template is defined exactly once.

```csharp
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Tests.WorkflowTests;

public record SeededMedia(int BrothLotId, int SelectiveBrothLotId, int SelectivePlatingLotId, int XldLotId, int TsiLotId,
    int BrothMaterialId, int SelectiveBrothMaterialId, int SelectivePlatingMaterialId, int XldMaterialId, int TsiMaterialId,
    int XldStepMediaId, int TsiStepMediaId, int SelectiveBrothIncubatorId);

// Builds the canonical five-stage pathogen template plus every master
// row it depends on. Mirrors DbSeeder.SeedPathogenTemplate.
public static class PathogenTestData
{
    public static MicroLimsDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MicroLimsDbContext(options);
    }

    public static async Task<(TestOrder order, SeededMedia media, Equipment incubator)> SeedFiveStageOrderAsync(MicroLimsDbContext db)
    {
        var generalBroth = await AddMediaTypeAsync(db, MediaClass.GeneralBroth, 35, 37);
        var selectiveBrothType = await AddMediaTypeAsync(db, MediaClass.SelectiveBroth, 41, 43);
        var selectiveAgar = await AddMediaTypeAsync(db, MediaClass.SelectiveAgar, 35, 37);

        var organism = new Organism { ScientificName = "Salmonella enterica" };
        db.Organisms.Add(organism);

        var incubator = new Equipment { Name = "INC-03", Code = "INC-03", Type = EquipmentType.Incubator, SetPointTemperature = 36 };
        // Selective Broth's 41-43C range is disjoint from every other
        // step's 35-37C - a real incubator can't serve both, so this
        // fixture needs a second one rather than one shared set point.
        var selectiveBrothIncubator = new Equipment { Name = "INC-07", Code = "INC-07", Type = EquipmentType.Incubator, SetPointTemperature = 42 };
        db.Equipment.AddRange(incubator, selectiveBrothIncubator);

        var test = new TestDefinition { Code = "PATHOGEN_SALMONELLA", DisplayName = "Salmonella", WorkflowType = WorkflowType.Observation };
        db.TestDefinitions.Add(test);
        await db.SaveChangesAsync();

        var steps = new[]
        {
            new TestWorkflowStep { TestDefinitionId = test.Id, StepOrder = 1, StepName = "Broth Enrichment", MediaTypeId = generalBroth.Id, IncubationMinHours = 18, IncubationMaxHours = 24, TemperatureMin = 35, TemperatureMax = 37, StepType = StepType.BrothEnrichment },
            new TestWorkflowStep { TestDefinitionId = test.Id, StepOrder = 2, StepName = "Selective Broth", MediaTypeId = selectiveBrothType.Id, IncubationMinHours = 18, IncubationMaxHours = 24, TemperatureMin = 41, TemperatureMax = 43, StepType = StepType.SelectiveBroth },
            new TestWorkflowStep { TestDefinitionId = test.Id, StepOrder = 3, StepName = "Selective Plating", MediaTypeId = selectiveAgar.Id, IncubationMinHours = 18, IncubationMaxHours = 24, TemperatureMin = 35, TemperatureMax = 37, StepType = StepType.SelectivePlating, TargetOrganismId = organism.Id },
            new TestWorkflowStep { TestDefinitionId = test.Id, StepOrder = 4, StepName = "Confirmatory Plating", MediaTypeId = selectiveAgar.Id, IncubationMinHours = 18, IncubationMaxHours = 24, TemperatureMin = 35, TemperatureMax = 37, StepType = StepType.ConfirmatoryPlating, TargetOrganismId = organism.Id },
            new TestWorkflowStep { TestDefinitionId = test.Id, StepOrder = 5, StepName = "Biochemical Test", MediaTypeId = selectiveAgar.Id, IncubationMinHours = 0, IncubationMaxHours = 0, TemperatureMin = 35, TemperatureMax = 37, IsFinalStep = true, StepType = StepType.BiochemicalTest }
        };
        db.TestWorkflowSteps.AddRange(steps);
        await db.SaveChangesAsync();

        var (brothMaterial, brothLot) = await AddMediumAsync(db, generalBroth, "Tryptone Soya Broth", "TSB/1/26");
        var (selBrothMaterial, selBrothLot) = await AddMediumAsync(db, selectiveBrothType, "Rappaport Vassiliadis Broth", "RVS/1/26");
        var (platingMaterial, platingLot) = await AddMediumAsync(db, selectiveAgar, "XLD Agar", "XLD/1/26");
        var (tsiMaterial, tsiLot) = await AddMediumAsync(db, selectiveAgar, "TSI Agar", "TSI/1/26");

        db.TestWorkflowStepMedias.AddRange(
            new TestWorkflowStepMedia { TestWorkflowStepId = steps[0].Id, MaterialId = brothMaterial.Id, TempMin = 35, TempMax = 37, IsRequired = true, DisplayOrder = 1 },
            new TestWorkflowStepMedia { TestWorkflowStepId = steps[1].Id, MaterialId = selBrothMaterial.Id, TempMin = 41, TempMax = 43, IsRequired = true, DisplayOrder = 1 },
            new TestWorkflowStepMedia { TestWorkflowStepId = steps[2].Id, MaterialId = platingMaterial.Id, TempMin = 35, TempMax = 37, IsRequired = true, DisplayOrder = 1 });
        var xldStepMedia = new TestWorkflowStepMedia { TestWorkflowStepId = steps[3].Id, MaterialId = platingMaterial.Id, TempMin = 35, TempMax = 37, IsRequired = false, DisplayOrder = 1 };
        var tsiStepMedia = new TestWorkflowStepMedia { TestWorkflowStepId = steps[3].Id, MaterialId = tsiMaterial.Id, TempMin = 35, TempMax = 37, IsRequired = false, DisplayOrder = 2 };
        db.TestWorkflowStepMedias.AddRange(xldStepMedia, tsiStepMedia);

        db.MediaChallengeSpecs.AddRange(
            new MediaChallengeSpec { MaterialName = "XLD Agar", EvaluationType = EvaluationType.GrowthPromotion, OrganismId = organism.Id, ExpectedDescription = "Red colonies with black centres" },
            new MediaChallengeSpec { MaterialName = "TSI Agar", EvaluationType = EvaluationType.GrowthPromotion, OrganismId = organism.Id, ExpectedDescription = "Alkaline slant, acid butt, H2S positive" });
        await db.SaveChangesAsync();

        var sample = new Sample { Category = SampleCategory.FinishedProduct, ControlNumber = "CTRL-1", Status = SampleStatus.Received };
        var order = new TestOrder { TestCode = "PATHOGEN_SALMONELLA", Status = ApprovalStatus.Pending, CurrentStep = WorkflowStep.Waiting };
        sample.TestOrders.Add(order);
        db.Samples.Add(sample);
        await db.SaveChangesAsync();

        var media = new SeededMedia(brothLot.Id, selBrothLot.Id, platingLot.Id, platingLot.Id, tsiLot.Id,
            brothMaterial.Id, selBrothMaterial.Id, platingMaterial.Id, platingMaterial.Id, tsiMaterial.Id,
            xldStepMedia.Id, tsiStepMedia.Id, selectiveBrothIncubator.Id);
        return (order, media, incubator);
    }

    private static async Task<MediaType> AddMediaTypeAsync(MicroLimsDbContext db, MediaClass mediaClass, decimal tempMin, decimal tempMax)
    {
        var mediaType = new MediaType { Class = mediaClass, IncubationMinHours = 18, IncubationMaxHours = 24, RequiredTemperatureMin = tempMin, RequiredTemperatureMax = tempMax };
        db.MediaTypes.Add(mediaType);
        await db.SaveChangesAsync();
        return mediaType;
    }

    private static async Task<(Material material, Media lot)> AddMediumAsync(MicroLimsDbContext db, MediaType mediaType, string materialName, string lotNumber)
    {
        var material = new Material
        {
            MaterialType = MaterialType.DehydratedMedia, MaterialName = materialName, ManufacturerName = "Himedia",
            BatchNumber = $"LOT-{lotNumber}", ReceivingDate = DateTime.UtcNow.AddDays(-10), Location = "Micro Lab",
            QuantityReceived = 500, QuantityRemaining = 500, Unit = MaterialUnit.Gram
        };
        db.Materials.Add(material);
        await db.SaveChangesAsync();

        var lot = new Media { MediaTypeId = mediaType.Id, MaterialId = material.Id, LotNumber = lotNumber, IsReleasedForUse = true, Status = MediaStatus.Active, ExpiryDate = DateTime.UtcNow.AddDays(30) };
        db.Media.Add(lot);
        await db.SaveChangesAsync();
        return (material, lot);
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

```bash
dotnet test backend/MicroLIMS.Tests/MicroLIMS.Tests.csproj --filter "FullyQualifiedName~IncubationLockTests"
```

Expected: compile errors — `WorkflowStepException` and `SubmitBrothAsync` do not exist.

- [ ] **Step 4: Add `WorkflowStepException` to `TestWorkflowEngine.cs`**

Place it directly above the `ITestWorkflowEngine` interface declaration. It derives from `InvalidOperationException` so the existing `ExceptionMiddleware` catch already maps it to a 400 — the controller catches it first to attach the error code.

```csharp
// A business-rule failure that carries a machine-readable code for the
// frontend. Derives from InvalidOperationException so that if a call
// site does not special-case it, ExceptionMiddleware still returns 400
// with the message rather than a 500.
public class WorkflowStepException : InvalidOperationException
{
    public string ErrorCode { get; }
    public long? RemainingSeconds { get; }

    public WorkflowStepException(string errorCode, string message, long? remainingSeconds = null) : base(message)
    {
        ErrorCode = errorCode;
        RemainingSeconds = remainingSeconds;
    }
}
```

- [ ] **Step 5: Add `StepResultDto` to `TestWorkflowEngine.cs`**

Place it with the other `record` declarations at the top of the file:

```csharp
public record StepResultDto(
    int StepInstanceId, string StepType, string Status,
    int SubmittedByUserId, DateTime SubmittedAtUtc,
    bool NextStepUnlocked, string? WorkflowFinalResult, List<string> Flags);
```

- [ ] **Step 6: Strip the dual-plate model out of the engine**

Delete from `TestWorkflowEngine.cs`:
- the `DualPlatePayload` and `BatchLocationDualPlateObservation` records
- `LoadLatestDualPlateObservationsAsync` and `RecordDualPlateAsync`
- the `StepResultType.DualGrowth` branch of `IsStepDoneAsync`
- the `plate2MediaId` / `plate1Label` / `plate2Label` parameters of `SelectMediaAsync` and every validation branch that reads them
- the `dualPlateObservations` parameter of `RecordBatchPathogenResultsAsync` and its branch
- the `Plate1Label` / `Plate1GrowthObserved` / `Plate1MediaLotNumber` / `Plate2*` members of `CompletedStepSummary` and everything populating them

Replace every remaining `StepResultType` reference with `StepType`, and every `StepResultType.PlateCount` with `StepType.PlateCount`. `ObservationPayload(bool GrowthObserved)` becomes `ObservationPayload(GrowthObservation Observation)`.

In `ResultProjectionService.cs`, delete the `StepResultType.DualGrowth` branch and its two-observation pairing; `reportedValue` for a pathogen result now reads from the final `WorkflowStepResult` (`"Detected"` when a final result was set, `"Not Detected"` otherwise). In `SegregationOfDutiesGuard.cs`, no column it reads changed name — leave `PathogenObservations.ObservedByUserId` as is, but add `WorkflowStepResults` to the "did this user perform the test" check:

```csharp
        if (await _db.WorkflowStepResults.AnyAsync(r => r.TestOrderId == testOrderId && r.SubmittedByUserId == userId)) return true;
```

- [ ] **Step 7: Add the two new constructor dependencies**

```csharp
    private readonly MicroLimsDbContext _db;
    private readonly SampleReviewService _sampleReviewService;
    private readonly ResultProjectionService _resultProjection;
    private readonly IncubatorEligibilityService _incubatorEligibility;
    private readonly MediaAppearanceSnapshotService _appearanceSnapshot;

    public TestWorkflowEngine(
        MicroLimsDbContext db, SampleReviewService sampleReviewService, ResultProjectionService resultProjection,
        IncubatorEligibilityService incubatorEligibility, MediaAppearanceSnapshotService appearanceSnapshot)
    {
        _db = db;
        _sampleReviewService = sampleReviewService;
        _resultProjection = resultProjection;
        _incubatorEligibility = incubatorEligibility;
        _appearanceSnapshot = appearanceSnapshot;
    }
```

Update `TestServiceFactory.TestWorkflow` to match:

```csharp
    public static TestWorkflowEngine TestWorkflow(MicroLimsDbContext db) =>
        new(db, SampleReview(db), ResultProjection(db), IncubatorEligibility(db), AppearanceSnapshot(db));
```

- [ ] **Step 8: Implement the shared step helpers**

Add these private helpers to `TestWorkflowEngine`. Every step submission in Tasks 8–11 goes through them, so the lock, the incubator check, and the result row are written in exactly one place.

```csharp
    // Resolves the step template by name and guards workflow order,
    // reusing the existing order-violation message.
    private async Task<TestWorkflowStep> LoadStepAsync(int testOrderId, string stepName)
    {
        var order = await _db.TestOrders.FirstOrDefaultAsync(t => t.Id == testOrderId)
            ?? throw new InvalidOperationException($"Test order {testOrderId} not found.");
        var test = await _db.TestDefinitions.Include(t => t.Steps).FirstOrDefaultAsync(t => t.Code == order.TestCode)
            ?? throw new InvalidOperationException($"No test definition for {order.TestCode}.");
        return test.Steps.FirstOrDefault(s => s.StepName == stepName)
            ?? throw new InvalidOperationException($"Step '{stepName}' is not part of {order.TestCode}.");
    }

    private async Task<TestWorkflowStepMedia> LoadStepMediumAsync(int stepId, int materialId)
    {
        return await _db.TestWorkflowStepMedias
            .FirstOrDefaultAsync(m => m.TestWorkflowStepId == stepId && m.MaterialId == materialId)
            ?? throw new WorkflowStepException(WorkflowErrorCodes.MediaNotInPermittedList,
                "That medium is not on this step's permitted list.");
    }

    private async Task RequireEligibleIncubatorAsync(int stepMediaId, int equipmentId)
    {
        if (!await _incubatorEligibility.IsWithinRangeAsync(stepMediaId, equipmentId))
            throw new WorkflowStepException(WorkflowErrorCodes.IncubatorTempOutOfRange,
                "The selected incubator's set point is outside this medium's temperature range.");
    }

    private static void RequireIncubationComplete(DateTime incubationEndUtc)
    {
        var remaining = (long)Math.Ceiling((incubationEndUtc - DateTime.UtcNow).TotalSeconds);
        if (remaining > 0)
            throw new WorkflowStepException(WorkflowErrorCodes.IncubationNotComplete,
                "This step's incubation period has not finished yet.", remaining);
    }

    // The lot the analyst picked must be a released lot of the permitted
    // material and of the class the step template locks the step to.
    private async Task<Media> LoadReleasedLotAsync(int mediaLotId, int materialId, int mediaTypeId)
    {
        var lot = await _db.Media.FirstOrDefaultAsync(m => m.Id == mediaLotId)
            ?? throw new InvalidOperationException($"Media lot {mediaLotId} not found.");
        if (!lot.IsReleasedForUse)
            throw new InvalidOperationException($"Media lot {lot.LotNumber} has not been released for use.");
        if (lot.MaterialId != materialId)
            throw new WorkflowStepException(WorkflowErrorCodes.MediaNotInPermittedList,
                $"Media lot {lot.LotNumber} is not a lot of the permitted medium for this step.");
        if (lot.MediaTypeId != mediaTypeId)
            throw new InvalidOperationException($"Media lot {lot.LotNumber} is the wrong media class for this step.");
        return lot;
    }
```

- [ ] **Step 9: Implement `SubmitBrothAsync`**

Add to `ITestWorkflowEngine`:

```csharp
    Task<StepResultDto> SubmitBrothAsync(int testOrderId, string stepName, int mediaLotId, int equipmentId,
        DateTime incubationStartUtc, DateTime incubationEndUtc, string? observation, int userId);
```

Implementation:

```csharp
    // Broth steps carry no result logic - completion is the incubation
    // window elapsing plus the analyst submitting the form. The free-text
    // observation is recorded but never branches the workflow.
    public async Task<StepResultDto> SubmitBrothAsync(
        int testOrderId, string stepName, int mediaLotId, int equipmentId,
        DateTime incubationStartUtc, DateTime incubationEndUtc, string? observation, int userId)
    {
        var step = await LoadStepAsync(testOrderId, stepName);
        if (step.StepType is not (StepType.BrothEnrichment or StepType.SelectiveBroth))
            throw new InvalidOperationException($"Step '{stepName}' is not a broth step.");

        var stepMedium = step.StepMedia.Count == 1
            ? step.StepMedia[0]
            : await _db.TestWorkflowStepMedias.FirstOrDefaultAsync(m => m.TestWorkflowStepId == step.Id)
                ?? throw new InvalidOperationException($"Step '{stepName}' has no assigned medium.");

        var lot = await LoadReleasedLotAsync(mediaLotId, stepMedium.MaterialId, step.MediaTypeId);
        await RequireEligibleIncubatorAsync(stepMedium.Id, equipmentId);
        RequireIncubationComplete(incubationEndUtc);

        var incubation = new Incubation
        {
            TestOrderId = testOrderId, StepNumber = step.StepOrder, StepName = step.StepName,
            MediaId = lot.Id, IncubatorEquipmentId = equipmentId,
            Temperature = $"{step.TemperatureMin}-{step.TemperatureMax}",
            Duration = $"{step.IncubationMinHours}-{step.IncubationMaxHours}h",
            IncubationStartUtc = incubationStartUtc, IncubationEndUtc = incubationEndUtc,
            ExpectedReadingAt = incubationEndUtc,
            CompletedAt = DateTime.UtcNow, Outcome = observation
        };
        _db.Incubations.Add(incubation);
        await _db.SaveChangesAsync();

        var result = new WorkflowStepResult
        {
            IncubationId = incubation.Id, TestOrderId = testOrderId,
            StepName = step.StepName, StepType = step.StepType,
            SubmittedByUserId = userId, SubmittedAtUtc = DateTime.UtcNow
        };
        _db.WorkflowStepResults.Add(result);
        await _db.SaveChangesAsync();

        return new StepResultDto(incubation.Id, step.StepType.ToString(), "Complete",
            userId, result.SubmittedAtUtc, NextStepUnlocked: true, WorkflowFinalResult: null, Flags: new List<string>());
    }
```

- [ ] **Step 10: Run the tests to verify they pass**

```bash
dotnet test backend/MicroLIMS.Tests/MicroLIMS.Tests.csproj --filter "FullyQualifiedName~IncubationLockTests"
```

Expected: PASS, 7 tests. The rest of the suite is still red (`PathogenWorkflowTests` still references the dual-plate model) — Task 12 rewrites it.

- [ ] **Step 11: Commit**

```bash
git -C E:/MicroLIMS/MicroLIMS add backend/MicroLIMS.Application backend/MicroLIMS.Tests && git -C E:/MicroLIMS/MicroLIMS commit -m "refactor: replace dual-plate engine paths with broth submission and incubation lock"
```

---

## Task 9: Selective plating result logic (spec 3.4)

**Files:**
- Modify: `backend/MicroLIMS.Application/Workflows/TestWorkflowEngine.cs`
- Create: `backend/MicroLIMS.Tests/WorkflowTests/SelectivePlatingTests.cs`

**Interfaces:**
- Consumes: `LoadStepAsync`, `LoadStepMediumAsync`, `LoadReleasedLotAsync`, `RequireEligibleIncubatorAsync`, `StepResultDto`, `MediaAppearanceSnapshotService` (all from Task 8).
- Produces on `ITestWorkflowEngine`:
  `Task<StepResultDto> SubmitSelectivePlatingAsync(int testOrderId, string stepName, int mediaLotId, int equipmentId, DateTime incubationStartUtc, DateTime incubationEndUtc, GrowthObservation observation, int userId)`.

`NoGrowth` and `GrowthNonConforming` both end the workflow as `NotDetected`. Only `GrowthConforming` unlocks the confirmatory step. There is **no** incubation lock on this step type (spec 3.5).

- [ ] **Step 1: Write the failing tests**

Create `backend/MicroLIMS.Tests/WorkflowTests/SelectivePlatingTests.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Domain.Enums;
using Xunit;

namespace MicroLIMS.Tests.WorkflowTests;

public class SelectivePlatingTests
{
    private static async Task<(int orderId, SeededMedia media, int incubatorId, ITestWorkflowEngine engine, MicroLIMS.Persistence.DbContext.MicroLimsDbContext db)> ReadyForPlatingAsync()
    {
        var db = PathogenTestData.NewDb();
        var (order, media, incubator) = await PathogenTestData.SeedFiveStageOrderAsync(db);
        var engine = TestServiceFactory.TestWorkflow(db);

        var start = DateTime.UtcNow.AddHours(-30);
        var end = DateTime.UtcNow.AddHours(-6);
        await engine.SubmitBrothAsync(order.Id, "Broth Enrichment", media.BrothLotId, incubator.Id, start, end, null, userId: 4);
        await engine.SubmitBrothAsync(order.Id, "Selective Broth", media.SelectiveBrothLotId, media.SelectiveBrothIncubatorId, start, end, null, userId: 4);
        return (order.Id, media, incubator.Id, engine, db);
    }

    [Theory]
    [InlineData(GrowthObservation.NoGrowth)]
    [InlineData(GrowthObservation.GrowthNonConforming)]
    public async Task NonConformingGrowth_EndsTheWorkflowAsNotDetected(GrowthObservation observation)
    {
        var (orderId, media, incubatorId, engine, db) = await ReadyForPlatingAsync();
        await using var _ = db;

        var result = await engine.SubmitSelectivePlatingAsync(orderId, "Selective Plating",
            media.SelectivePlatingLotId, incubatorId, DateTime.UtcNow.AddHours(-30), DateTime.UtcNow.AddHours(-6), observation, userId: 4);

        Assert.Equal("NotDetected", result.WorkflowFinalResult);
        Assert.False(result.NextStepUnlocked);
    }

    [Fact]
    public async Task ConformingGrowth_UnlocksTheNextStepWithoutSettingAFinalResult()
    {
        var (orderId, media, incubatorId, engine, db) = await ReadyForPlatingAsync();
        await using var _ = db;

        var result = await engine.SubmitSelectivePlatingAsync(orderId, "Selective Plating",
            media.SelectivePlatingLotId, incubatorId, DateTime.UtcNow.AddHours(-30), DateTime.UtcNow.AddHours(-6),
            GrowthObservation.GrowthConforming, userId: 4);

        Assert.Null(result.WorkflowFinalResult);
        Assert.True(result.NextStepUnlocked);
    }

    [Fact]
    public async Task Submission_SnapshotsTheExpectedAppearance()
    {
        var (orderId, media, incubatorId, engine, db) = await ReadyForPlatingAsync();
        await using var _ = db;

        await engine.SubmitSelectivePlatingAsync(orderId, "Selective Plating",
            media.SelectivePlatingLotId, incubatorId, DateTime.UtcNow.AddHours(-30), DateTime.UtcNow.AddHours(-6),
            GrowthObservation.GrowthConforming, userId: 4);

        var stored = await db.WorkflowStepResults.SingleAsync(r => r.StepName == "Selective Plating");
        Assert.Equal("Red colonies with black centres", stored.ExpectedAppearanceSnapshot);
        Assert.Equal(GrowthObservation.GrowthConforming, stored.SelectivePlatingObservation);
    }

    [Fact]
    public async Task Submission_IsNotBlockedByAnUnfinishedIncubationWindow()
    {
        var (orderId, media, incubatorId, engine, db) = await ReadyForPlatingAsync();
        await using var _ = db;

        // Selective plating has no incubation lock - a window ending in
        // the future must still be accepted.
        var result = await engine.SubmitSelectivePlatingAsync(orderId, "Selective Plating",
            media.SelectivePlatingLotId, incubatorId, DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddHours(23),
            GrowthObservation.GrowthConforming, userId: 4);

        Assert.True(result.NextStepUnlocked);
    }

    [Fact]
    public async Task Submission_WritesAPathogenObservationRow()
    {
        var (orderId, media, incubatorId, engine, db) = await ReadyForPlatingAsync();
        await using var _ = db;

        await engine.SubmitSelectivePlatingAsync(orderId, "Selective Plating",
            media.SelectivePlatingLotId, incubatorId, DateTime.UtcNow.AddHours(-30), DateTime.UtcNow.AddHours(-6),
            GrowthObservation.NoGrowth, userId: 4);

        var observation = await db.PathogenObservations.SingleAsync(o => o.TestOrderId == orderId);
        Assert.Equal(GrowthObservation.NoGrowth, observation.Observation);
        Assert.Equal(4, observation.ObservedByUserId);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

```bash
dotnet test backend/MicroLIMS.Tests/MicroLIMS.Tests.csproj --filter "FullyQualifiedName~SelectivePlatingTests"
```

Expected: compile error — `SubmitSelectivePlatingAsync` does not exist.

- [ ] **Step 3: Implement `SubmitSelectivePlatingAsync`**

Add to `ITestWorkflowEngine`:

```csharp
    Task<StepResultDto> SubmitSelectivePlatingAsync(int testOrderId, string stepName, int mediaLotId, int equipmentId,
        DateTime incubationStartUtc, DateTime incubationEndUtc, GrowthObservation observation, int userId);
```

Implementation:

```csharp
    // Growth that is absent or does not match the expected appearance
    // means the organism being sought is not there - the workflow ends
    // as NotDetected without running confirmatory plating.
    public async Task<StepResultDto> SubmitSelectivePlatingAsync(
        int testOrderId, string stepName, int mediaLotId, int equipmentId,
        DateTime incubationStartUtc, DateTime incubationEndUtc, GrowthObservation observation, int userId)
    {
        var step = await LoadStepAsync(testOrderId, stepName);
        if (step.StepType != StepType.SelectivePlating)
            throw new InvalidOperationException($"Step '{stepName}' is not a selective plating step.");

        var stepMedium = await _db.TestWorkflowStepMedias.FirstOrDefaultAsync(m => m.TestWorkflowStepId == step.Id)
            ?? throw new InvalidOperationException($"Step '{stepName}' has no assigned medium.");
        var lot = await LoadReleasedLotAsync(mediaLotId, stepMedium.MaterialId, step.MediaTypeId);
        await RequireEligibleIncubatorAsync(stepMedium.Id, equipmentId);

        var incubation = new Incubation
        {
            TestOrderId = testOrderId, StepNumber = step.StepOrder, StepName = step.StepName,
            MediaId = lot.Id, IncubatorEquipmentId = equipmentId,
            Temperature = $"{step.TemperatureMin}-{step.TemperatureMax}",
            Duration = $"{step.IncubationMinHours}-{step.IncubationMaxHours}h",
            IncubationStartUtc = incubationStartUtc, IncubationEndUtc = incubationEndUtc,
            ExpectedReadingAt = incubationEndUtc, CompletedAt = DateTime.UtcNow,
            Outcome = observation.ToString()
        };
        _db.Incubations.Add(incubation);
        await _db.SaveChangesAsync();

        // Snapshot taken at submission, never afterwards (ALCOA+).
        var snapshot = await _appearanceSnapshot.GetExpectedAppearanceSnapshotAsync(
            stepMedium.MaterialId, step.TargetOrganismId!.Value);

        var result = new WorkflowStepResult
        {
            IncubationId = incubation.Id, TestOrderId = testOrderId,
            StepName = step.StepName, StepType = step.StepType,
            SelectivePlatingObservation = observation, ExpectedAppearanceSnapshot = snapshot,
            SubmittedByUserId = userId, SubmittedAtUtc = DateTime.UtcNow
        };
        _db.WorkflowStepResults.Add(result);

        _db.PathogenObservations.Add(new PathogenObservation
        {
            TestOrderId = testOrderId, StepName = step.StepName, StepOrder = step.StepOrder,
            Observation = observation, ObservedByUserId = userId, MediaId = lot.Id
        });
        await _db.SaveChangesAsync();

        if (observation == GrowthObservation.GrowthConforming)
            return new StepResultDto(incubation.Id, step.StepType.ToString(), "Complete",
                userId, result.SubmittedAtUtc, NextStepUnlocked: true, WorkflowFinalResult: null, Flags: new List<string>());

        await FinalizeWorkflowAsync(testOrderId, "NotDetected", userId);
        return new StepResultDto(incubation.Id, step.StepType.ToString(), "Complete",
            userId, result.SubmittedAtUtc, NextStepUnlocked: false, WorkflowFinalResult: "NotDetected", Flags: new List<string>());
    }
```

- [ ] **Step 4: Implement the shared `FinalizeWorkflowAsync` helper**

This is the single place a pathogen workflow reaches a final result. It reuses the existing transition + auto-submit-for-review path the engine already had at the end of `RecordResultAsync`.

```csharp
    // One exit point for a finished pathogen workflow: write the Result
    // row, project it, move the order to Ready, and let the existing
    // sample review service decide whether the sample can now be
    // submitted for review.
    private async Task FinalizeWorkflowAsync(int testOrderId, string finalResult, int userId)
    {
        var order = await _db.TestOrders.FirstOrDefaultAsync(t => t.Id == testOrderId)
            ?? throw new InvalidOperationException($"Test order {testOrderId} not found.");

        _db.Results.Add(new Result
        {
            TestOrderId = testOrderId, RawValue = finalResult, InterpretedValue = finalResult,
            Type = ResultType.Qualitative, EnteredByUserId = userId
        });

        await _db.SaveChangesAsync();

        // TransitionAsync owns the WorkflowStep -> ApprovalStatus mapping
        // and the WorkflowHistory row. Setting CurrentStep/Status inline
        // here would duplicate that mapping and let the two copies drift.
        await WorkflowStateMachine.TransitionAsync(
            _db, order, WorkflowStep.Ready, userId, $"Workflow complete: {finalResult}");

        await _resultProjection.UpsertFromPathogenResultAsync(testOrderId);
        await _sampleReviewService.AutoSubmitForReviewIfReadyAsync(order.SampleId, userId);

        // Both calls above only stage their changes - the ResultRecord
        // projection and the sample's submit-for-review transition. The
        // sibling finalization paths all flush them here; without this
        // save both are silently discarded.
        await _db.SaveChangesAsync();
    }
```

If `ResultType.Qualitative` does not exist, read `backend/MicroLIMS.Domain/Enums/ResultType.cs` and use whichever value the previous pathogen path passed when it wrote its `Result` row — do not invent a new enum value.

- [ ] **Step 5: Run the tests to verify they pass**

```bash
dotnet test backend/MicroLIMS.Tests/MicroLIMS.Tests.csproj --filter "FullyQualifiedName~SelectivePlatingTests"
```

Expected: PASS, 6 tests.

- [ ] **Step 6: Commit**

```bash
git -C E:/MicroLIMS/MicroLIMS add backend/MicroLIMS.Application backend/MicroLIMS.Tests && git -C E:/MicroLIMS/MicroLIMS commit -m "feat: add selective plating result logic"
```

---

## Task 10: Confirmatory plating — setup and observations (spec 3.4)

**Files:**
- Modify: `backend/MicroLIMS.Application/Workflows/TestWorkflowEngine.cs`
- Create: `backend/MicroLIMS.Tests/WorkflowTests/ConfirmatoryPlatingTests.cs`

**Interfaces:**
- Consumes: everything from Tasks 8–9.
- Produces (namespace `MicroLIMS.Application.Workflows`):
  - `record ConfirmatorySelectionInput(int StepMediaId, int MediaLotId, int EquipmentId);`
  - `record ConfirmatoryObservationInput(int MaterialId, GrowthObservation Observation);`
  - `record ConfirmatoryOutcomeDto(int StepInstanceId, string ConfirmatoryResult, bool AnalystDecisionRequired, List<string> Flags);`
  - On `ITestWorkflowEngine`:
    - `Task<StepResultDto> SubmitConfirmatorySetupAsync(int testOrderId, string stepName, IReadOnlyList<ConfirmatorySelectionInput> selections, DateTime incubationStartUtc, DateTime incubationEndUtc, int userId)`
    - `Task<ConfirmatoryOutcomeDto> SubmitConfirmatoryObservationsAsync(int testOrderId, string stepName, IReadOnlyList<ConfirmatoryObservationInput> observations, int userId)`

Setup and observations are two calls because the incubation happens between them. Setup creates the `Incubation` and the `WorkflowStepResult` with its `ConfirmatoryMediaSelection` rows; observations fill in the readings and compute `ConfirmatoryResult`.

- [ ] **Step 1: Write the failing tests**

Create `backend/MicroLIMS.Tests/WorkflowTests/ConfirmatoryPlatingTests.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Constants;
using Xunit;

namespace MicroLIMS.Tests.WorkflowTests;

public class ConfirmatoryPlatingTests
{
    private static async Task<(int orderId, SeededMedia media, int incubatorId, ITestWorkflowEngine engine, MicroLimsDbContext db)> ReadyForConfirmatoryAsync()
    {
        var db = PathogenTestData.NewDb();
        var (order, media, incubator) = await PathogenTestData.SeedFiveStageOrderAsync(db);
        var engine = TestServiceFactory.TestWorkflow(db);
        var start = DateTime.UtcNow.AddHours(-30);
        var end = DateTime.UtcNow.AddHours(-6);

        await engine.SubmitBrothAsync(order.Id, "Broth Enrichment", media.BrothLotId, incubator.Id, start, end, null, 4);
        await engine.SubmitBrothAsync(order.Id, "Selective Broth", media.SelectiveBrothLotId, media.SelectiveBrothIncubatorId, start, end, null, 4);
        await engine.SubmitSelectivePlatingAsync(order.Id, "Selective Plating", media.SelectivePlatingLotId, incubator.Id,
            start, end, GrowthObservation.GrowthConforming, 4);
        return (order.Id, media, incubator.Id, engine, db);
    }

    private static ConfirmatorySelectionInput[] BothMedia(SeededMedia media, int incubatorId) => new[]
    {
        new ConfirmatorySelectionInput(media.XldStepMediaId, media.XldLotId, incubatorId),
        new ConfirmatorySelectionInput(media.TsiStepMediaId, media.TsiLotId, incubatorId)
    };

    [Fact]
    public async Task Setup_RecordsOneSelectionPerChosenMedium()
    {
        var (orderId, media, incubatorId, engine, db) = await ReadyForConfirmatoryAsync();
        await using var _ = db;

        await engine.SubmitConfirmatorySetupAsync(orderId, "Confirmatory Plating", BothMedia(media, incubatorId),
            DateTime.UtcNow.AddHours(-30), DateTime.UtcNow.AddHours(-6), 4);

        var result = await db.WorkflowStepResults.Include(r => r.Selections).SingleAsync(r => r.StepName == "Confirmatory Plating");
        Assert.Equal(2, result.Selections.Count);
        Assert.All(result.Selections, s => Assert.False(s.WasAnalystAdded));
    }

    [Fact]
    public async Task Setup_WithNoMedia_ThrowsNoMediaSelected()
    {
        var (orderId, _, _, engine, db) = await ReadyForConfirmatoryAsync();
        await using var _db = db;

        var ex = await Assert.ThrowsAsync<WorkflowStepException>(() => engine.SubmitConfirmatorySetupAsync(
            orderId, "Confirmatory Plating", Array.Empty<ConfirmatorySelectionInput>(),
            DateTime.UtcNow.AddHours(-30), DateTime.UtcNow.AddHours(-6), 4));

        Assert.Equal(WorkflowErrorCodes.NoMediaSelected, ex.ErrorCode);
    }

    [Fact]
    public async Task Setup_WithAMediumFromAnotherStep_ThrowsMediaNotInPermittedList()
    {
        var (orderId, media, incubatorId, engine, db) = await ReadyForConfirmatoryAsync();
        await using var _ = db;

        // The selective plating step's own medium is not on the
        // confirmatory step's permitted list.
        var foreign = await db.TestWorkflowStepMedias
            .Include(m => m.TestWorkflowStep)
            .FirstAsync(m => m.TestWorkflowStep!.StepType == StepType.SelectivePlating);

        var ex = await Assert.ThrowsAsync<WorkflowStepException>(() => engine.SubmitConfirmatorySetupAsync(
            orderId, "Confirmatory Plating",
            new[] { new ConfirmatorySelectionInput(foreign.Id, media.SelectivePlatingLotId, incubatorId) },
            DateTime.UtcNow.AddHours(-30), DateTime.UtcNow.AddHours(-6), 4));

        Assert.Equal(WorkflowErrorCodes.MediaNotInPermittedList, ex.ErrorCode);
    }

    [Fact]
    public async Task Observations_AllConforming_RequiresAnAnalystDecision()
    {
        var (orderId, media, incubatorId, engine, db) = await ReadyForConfirmatoryAsync();
        await using var _ = db;
        await engine.SubmitConfirmatorySetupAsync(orderId, "Confirmatory Plating", BothMedia(media, incubatorId),
            DateTime.UtcNow.AddHours(-30), DateTime.UtcNow.AddHours(-6), 4);

        var outcome = await engine.SubmitConfirmatoryObservationsAsync(orderId, "Confirmatory Plating", new[]
        {
            new ConfirmatoryObservationInput(media.XldMaterialId, GrowthObservation.GrowthConforming),
            new ConfirmatoryObservationInput(media.TsiMaterialId, GrowthObservation.GrowthConforming)
        }, 4);

        Assert.Equal("AllConforming", outcome.ConfirmatoryResult);
        Assert.True(outcome.AnalystDecisionRequired);
        Assert.Empty(outcome.Flags);
    }

    [Theory]
    [InlineData(GrowthObservation.NoGrowth)]
    [InlineData(GrowthObservation.GrowthNonConforming)]
    public async Task Observations_MixedResults_AreInconclusiveAndFlagged(GrowthObservation second)
    {
        var (orderId, media, incubatorId, engine, db) = await ReadyForConfirmatoryAsync();
        await using var _ = db;
        await engine.SubmitConfirmatorySetupAsync(orderId, "Confirmatory Plating", BothMedia(media, incubatorId),
            DateTime.UtcNow.AddHours(-30), DateTime.UtcNow.AddHours(-6), 4);

        var outcome = await engine.SubmitConfirmatoryObservationsAsync(orderId, "Confirmatory Plating", new[]
        {
            new ConfirmatoryObservationInput(media.XldMaterialId, GrowthObservation.GrowthConforming),
            new ConfirmatoryObservationInput(media.TsiMaterialId, second)
        }, 4);

        Assert.Equal("Inconclusive", outcome.ConfirmatoryResult);
        Assert.False(outcome.AnalystDecisionRequired);
        Assert.Contains("InconclusiveResult", outcome.Flags);
    }

    [Fact]
    public async Task Observations_SnapshotExpectedAppearancePerMedium()
    {
        var (orderId, media, incubatorId, engine, db) = await ReadyForConfirmatoryAsync();
        await using var _ = db;
        await engine.SubmitConfirmatorySetupAsync(orderId, "Confirmatory Plating", BothMedia(media, incubatorId),
            DateTime.UtcNow.AddHours(-30), DateTime.UtcNow.AddHours(-6), 4);

        await engine.SubmitConfirmatoryObservationsAsync(orderId, "Confirmatory Plating", new[]
        {
            new ConfirmatoryObservationInput(media.XldMaterialId, GrowthObservation.GrowthConforming),
            new ConfirmatoryObservationInput(media.TsiMaterialId, GrowthObservation.GrowthConforming)
        }, 4);

        var observations = await db.ConfirmatoryPlateObservations.ToListAsync();
        Assert.Equal(2, observations.Count);
        Assert.Contains(observations, o => o.ExpectedAppearanceSnapshot == "Red colonies with black centres");
        Assert.Contains(observations, o => o.ExpectedAppearanceSnapshot == "Alkaline slant, acid butt, H2S positive");
    }

    [Fact]
    public async Task Observations_BeforeTheWindowEnds_ThrowIncubationNotComplete()
    {
        var (orderId, media, incubatorId, engine, db) = await ReadyForConfirmatoryAsync();
        await using var _ = db;
        await engine.SubmitConfirmatorySetupAsync(orderId, "Confirmatory Plating", BothMedia(media, incubatorId),
            DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddHours(23), 4);

        var ex = await Assert.ThrowsAsync<WorkflowStepException>(() => engine.SubmitConfirmatoryObservationsAsync(
            orderId, "Confirmatory Plating",
            new[] { new ConfirmatoryObservationInput(media.XldMaterialId, GrowthObservation.GrowthConforming) }, 4));

        Assert.Equal(WorkflowErrorCodes.IncubationNotComplete, ex.ErrorCode);
    }

    [Fact]
    public async Task Observations_MissingAMediumThatWasSetUp_ThrowsIncompleteSetup()
    {
        var (orderId, media, incubatorId, engine, db) = await ReadyForConfirmatoryAsync();
        await using var _ = db;
        await engine.SubmitConfirmatorySetupAsync(orderId, "Confirmatory Plating", BothMedia(media, incubatorId),
            DateTime.UtcNow.AddHours(-30), DateTime.UtcNow.AddHours(-6), 4);

        var ex = await Assert.ThrowsAsync<WorkflowStepException>(() => engine.SubmitConfirmatoryObservationsAsync(
            orderId, "Confirmatory Plating",
            new[] { new ConfirmatoryObservationInput(media.XldMaterialId, GrowthObservation.GrowthConforming) }, 4));

        Assert.Equal(WorkflowErrorCodes.IncompleteConfirmatorySetup, ex.ErrorCode);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

```bash
dotnet test backend/MicroLIMS.Tests/MicroLIMS.Tests.csproj --filter "FullyQualifiedName~ConfirmatoryPlatingTests"
```

Expected: compile errors — `SubmitConfirmatorySetupAsync` / `SubmitConfirmatoryObservationsAsync` do not exist.

- [ ] **Step 3: Add the input/output records**

At the top of `TestWorkflowEngine.cs` with the other records:

```csharp
public record ConfirmatorySelectionInput(int StepMediaId, int MediaLotId, int EquipmentId);
public record ConfirmatoryObservationInput(int MaterialId, GrowthObservation Observation);
public record ConfirmatoryOutcomeDto(int StepInstanceId, string ConfirmatoryResult, bool AnalystDecisionRequired, List<string> Flags);
```

- [ ] **Step 4: Implement `SubmitConfirmatorySetupAsync`**

Add to `ITestWorkflowEngine`:

```csharp
    Task<StepResultDto> SubmitConfirmatorySetupAsync(int testOrderId, string stepName,
        IReadOnlyList<ConfirmatorySelectionInput> selections, DateTime incubationStartUtc, DateTime incubationEndUtc, int userId);
```

Implementation:

```csharp
    // The analyst's media panel for this run. Every chosen medium must be
    // on the step's permitted list, with a released lot and an in-range
    // incubator, before any plate goes into an incubator.
    public async Task<StepResultDto> SubmitConfirmatorySetupAsync(
        int testOrderId, string stepName, IReadOnlyList<ConfirmatorySelectionInput> selections,
        DateTime incubationStartUtc, DateTime incubationEndUtc, int userId)
    {
        var step = await LoadStepAsync(testOrderId, stepName);
        if (step.StepType != StepType.ConfirmatoryPlating)
            throw new InvalidOperationException($"Step '{stepName}' is not a confirmatory plating step.");

        if (selections.Count == 0)
            throw new WorkflowStepException(WorkflowErrorCodes.NoMediaSelected,
                "At least one confirmatory medium must be selected.");

        var permitted = await _db.TestWorkflowStepMedias
            .Where(m => m.TestWorkflowStepId == step.Id)
            .ToDictionaryAsync(m => m.Id);

        var resolved = new List<(TestWorkflowStepMedia Medium, Media Lot, int EquipmentId)>();
        foreach (var selection in selections)
        {
            if (!permitted.TryGetValue(selection.StepMediaId, out var medium))
                throw new WorkflowStepException(WorkflowErrorCodes.MediaNotInPermittedList,
                    "That medium is not on this step's permitted list.");
            if (selection.MediaLotId <= 0 || selection.EquipmentId <= 0)
                throw new WorkflowStepException(WorkflowErrorCodes.IncompleteConfirmatorySetup,
                    "Every selected medium needs a lot and an incubator.");

            var lot = await LoadReleasedLotAsync(selection.MediaLotId, medium.MaterialId, step.MediaTypeId);
            await RequireEligibleIncubatorAsync(medium.Id, selection.EquipmentId);
            resolved.Add((medium, lot, selection.EquipmentId));
        }

        var incubation = new Incubation
        {
            TestOrderId = testOrderId, StepNumber = step.StepOrder, StepName = step.StepName,
            MediaId = resolved[0].Lot.Id, IncubatorEquipmentId = resolved[0].EquipmentId,
            Temperature = $"{step.TemperatureMin}-{step.TemperatureMax}",
            Duration = $"{step.IncubationMinHours}-{step.IncubationMaxHours}h",
            IncubationStartUtc = incubationStartUtc, IncubationEndUtc = incubationEndUtc,
            ExpectedReadingAt = incubationEndUtc
        };
        _db.Incubations.Add(incubation);
        await _db.SaveChangesAsync();

        var result = new WorkflowStepResult
        {
            IncubationId = incubation.Id, TestOrderId = testOrderId,
            StepName = step.StepName, StepType = step.StepType,
            SubmittedByUserId = userId, SubmittedAtUtc = DateTime.UtcNow
        };
        foreach (var (medium, lot, equipmentId) in resolved)
            result.Selections.Add(new ConfirmatoryMediaSelection
            {
                MaterialId = medium.MaterialId, MediaId = lot.Id, EquipmentId = equipmentId, WasAnalystAdded = false
            });
        _db.WorkflowStepResults.Add(result);
        await _db.SaveChangesAsync();

        return new StepResultDto(incubation.Id, step.StepType.ToString(), "Incubating",
            userId, result.SubmittedAtUtc, NextStepUnlocked: false, WorkflowFinalResult: null, Flags: new List<string>());
    }
```

- [ ] **Step 5: Implement `SubmitConfirmatoryObservationsAsync`**

Add to `ITestWorkflowEngine`:

```csharp
    Task<ConfirmatoryOutcomeDto> SubmitConfirmatoryObservationsAsync(int testOrderId, string stepName,
        IReadOnlyList<ConfirmatoryObservationInput> observations, int userId);
```

Implementation:

```csharp
    // Every selected medium must be read, and every reading must be
    // conforming, before the analyst is offered a decision. Anything
    // else is Inconclusive and is flagged for investigation - there is
    // no path from here to Detected.
    public async Task<ConfirmatoryOutcomeDto> SubmitConfirmatoryObservationsAsync(
        int testOrderId, string stepName, IReadOnlyList<ConfirmatoryObservationInput> observations, int userId)
    {
        var step = await LoadStepAsync(testOrderId, stepName);
        if (step.StepType != StepType.ConfirmatoryPlating)
            throw new InvalidOperationException($"Step '{stepName}' is not a confirmatory plating step.");

        var result = await _db.WorkflowStepResults
            .Include(r => r.Selections)
            .Include(r => r.ConfirmatoryObservations)
            .Where(r => r.TestOrderId == testOrderId && r.StepName == stepName)
            .OrderByDescending(r => r.Id)
            .FirstOrDefaultAsync()
            ?? throw new WorkflowStepException(WorkflowErrorCodes.IncompleteConfirmatorySetup,
                "This step's media selection has not been submitted yet.");

        var incubation = await _db.Incubations.FirstAsync(i => i.Id == result.IncubationId);
        RequireIncubationComplete(incubation.IncubationEndUtc
            ?? throw new WorkflowStepException(WorkflowErrorCodes.IncompleteConfirmatorySetup,
                "This step has no recorded incubation window."));

        var selectedMaterialIds = result.Selections.Select(s => s.MaterialId).ToHashSet();
        var observedMaterialIds = observations.Select(o => o.MaterialId).ToHashSet();
        if (!selectedMaterialIds.SetEquals(observedMaterialIds))
            throw new WorkflowStepException(WorkflowErrorCodes.IncompleteConfirmatorySetup,
                "Exactly one observation is required for each selected medium.");

        foreach (var observation in observations)
        {
            var snapshot = await _appearanceSnapshot.GetExpectedAppearanceSnapshotAsync(
                observation.MaterialId, step.TargetOrganismId!.Value);

            result.ConfirmatoryObservations.Add(new ConfirmatoryPlateObservation
            {
                MaterialId = observation.MaterialId, Observation = observation.Observation,
                ExpectedAppearanceSnapshot = snapshot, RecordedByUserId = userId, RecordedAtUtc = DateTime.UtcNow
            });
        }

        var allConforming = observations.All(o => o.Observation == GrowthObservation.GrowthConforming);
        result.ConfirmatoryResult = allConforming ? ConfirmatoryResult.AllConforming : ConfirmatoryResult.Inconclusive;
        incubation.CompletedAt = DateTime.UtcNow;
        incubation.Outcome = result.ConfirmatoryResult.ToString();

        if (!allConforming)
            _db.WorkflowHistories.Add(new WorkflowHistory
            {
                TestOrderId = testOrderId, FromStep = WorkflowStep.Incubating, ToStep = WorkflowStep.Incubating,
                Note = "Confirmatory plating inconclusive - flagged for investigation.", PerformedByUserId = userId
            });

        await _db.SaveChangesAsync();

        return new ConfirmatoryOutcomeDto(
            result.IncubationId,
            result.ConfirmatoryResult.ToString()!,
            AnalystDecisionRequired: allConforming,
            Flags: allConforming ? new List<string>() : new List<string> { "InconclusiveResult" });
    }
```

- [ ] **Step 6: Run the tests to verify they pass**

```bash
dotnet test backend/MicroLIMS.Tests/MicroLIMS.Tests.csproj --filter "FullyQualifiedName~ConfirmatoryPlatingTests"
```

Expected: PASS, 9 tests.

- [ ] **Step 7: Commit**

```bash
git -C E:/MicroLIMS/MicroLIMS add backend/MicroLIMS.Application backend/MicroLIMS.Tests && git -C E:/MicroLIMS/MicroLIMS commit -m "feat: add confirmatory plating setup and observation recording"
```

---

## Task 11: Analyst decision, biochemical test, and reviewer send-back (spec 3.4)

**Files:**
- Modify: `backend/MicroLIMS.Application/Workflows/TestWorkflowEngine.cs`
- Create: `backend/MicroLIMS.Tests/WorkflowTests/BiochemicalReviewTests.cs`

**Interfaces:**
- Consumes: everything from Tasks 8–10, plus `SegregationOfDutiesGuard`, `ReviewGateService`, `INotificationService`.
- Produces on `ITestWorkflowEngine`:
  - `Task<StepResultDto> RecordAnalystDecisionAsync(int testOrderId, AnalystDecision decision, int userId)`
  - `Task<StepResultDto> SubmitBiochemicalAsync(int testOrderId, string stepName, string biochemicalResultText, int? attachmentId, int userId)`
  - `Task<StepResultDto> RecordBiochemicalReviewDecisionAsync(int workflowStepResultId, bool approve, string comment, int reviewerUserId)`
- `TestWorkflowEngine` gains three more constructor dependencies: `SegregationOfDutiesGuard`, `ReviewGateService`, `INotificationService`.

- [ ] **Step 1: Write the failing tests**

Create `backend/MicroLIMS.Tests/WorkflowTests/BiochemicalReviewTests.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Constants;
using Xunit;

namespace MicroLIMS.Tests.WorkflowTests;

public class BiochemicalReviewTests
{
    private const int AnalystId = 4;
    private const int ReviewerId = 9;

    private static async Task<(int orderId, ITestWorkflowEngine engine, MicroLimsDbContext db)> AllConformingAsync()
    {
        var db = PathogenTestData.NewDb();
        var (order, media, incubator) = await PathogenTestData.SeedFiveStageOrderAsync(db);
        var engine = TestServiceFactory.TestWorkflow(db);
        var start = DateTime.UtcNow.AddHours(-30);
        var end = DateTime.UtcNow.AddHours(-6);

        await engine.SubmitBrothAsync(order.Id, "Broth Enrichment", media.BrothLotId, incubator.Id, start, end, null, AnalystId);
        await engine.SubmitBrothAsync(order.Id, "Selective Broth", media.SelectiveBrothLotId, media.SelectiveBrothIncubatorId, start, end, null, AnalystId);
        await engine.SubmitSelectivePlatingAsync(order.Id, "Selective Plating", media.SelectivePlatingLotId, incubator.Id,
            start, end, GrowthObservation.GrowthConforming, AnalystId);
        await engine.SubmitConfirmatorySetupAsync(order.Id, "Confirmatory Plating", new[]
        {
            new ConfirmatorySelectionInput(media.XldStepMediaId, media.XldLotId, incubator.Id),
            new ConfirmatorySelectionInput(media.TsiStepMediaId, media.TsiLotId, incubator.Id)
        }, start, end, AnalystId);
        await engine.SubmitConfirmatoryObservationsAsync(order.Id, "Confirmatory Plating", new[]
        {
            new ConfirmatoryObservationInput(media.XldMaterialId, GrowthObservation.GrowthConforming),
            new ConfirmatoryObservationInput(media.TsiMaterialId, GrowthObservation.GrowthConforming)
        }, AnalystId);
        return (order.Id, engine, db);
    }

    [Fact]
    public async Task SubmitAsDetected_SetsDetectedAndFlagsTheMissingBiochemical()
    {
        var (orderId, engine, db) = await AllConformingAsync();
        await using var _ = db;

        var result = await engine.RecordAnalystDecisionAsync(orderId, AnalystDecision.SubmitAsDetected, AnalystId);

        Assert.Equal("Detected", result.WorkflowFinalResult);
        Assert.Contains("BiochemicalNotPerformed", result.Flags);

        var stored = await db.WorkflowStepResults.SingleAsync(r => r.StepName == "Confirmatory Plating");
        Assert.True(stored.SkippedBiochemical);
    }

    [Fact]
    public async Task ProceedToBiochemical_UnlocksTheStepWithoutSettingAResult()
    {
        var (orderId, engine, db) = await AllConformingAsync();
        await using var _ = db;

        var result = await engine.RecordAnalystDecisionAsync(orderId, AnalystDecision.ProceedToBiochemical, AnalystId);

        Assert.Null(result.WorkflowFinalResult);
        Assert.True(result.NextStepUnlocked);
    }

    [Fact]
    public async Task AnalystDecision_BeforeAllConforming_IsRejected()
    {
        var db = PathogenTestData.NewDb();
        await using var _ = db;
        var (order, _, _) = await PathogenTestData.SeedFiveStageOrderAsync(db);
        var engine = TestServiceFactory.TestWorkflow(db);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => engine.RecordAnalystDecisionAsync(order.Id, AnalystDecision.SubmitAsDetected, AnalystId));
    }

    [Fact]
    public async Task SubmitBiochemical_SetsDetectedAndClearsTheSkippedFlag()
    {
        var (orderId, engine, db) = await AllConformingAsync();
        await using var _ = db;
        await engine.RecordAnalystDecisionAsync(orderId, AnalystDecision.ProceedToBiochemical, AnalystId);

        var result = await engine.SubmitBiochemicalAsync(orderId, "Biochemical Test", "IMViC: + + - -", null, AnalystId);

        Assert.Equal("Detected", result.WorkflowFinalResult);
        Assert.Empty(result.Flags);

        var stored = await db.WorkflowStepResults.SingleAsync(r => r.StepName == "Biochemical Test");
        Assert.False(stored.SkippedBiochemical);
        Assert.Equal("IMViC: + + - -", stored.BiochemicalResultText);
    }

    [Fact]
    public async Task SubmitBiochemical_WithBlankText_ThrowsBiochemicalResultRequired()
    {
        var (orderId, engine, db) = await AllConformingAsync();
        await using var _ = db;
        await engine.RecordAnalystDecisionAsync(orderId, AnalystDecision.ProceedToBiochemical, AnalystId);

        var ex = await Assert.ThrowsAsync<WorkflowStepException>(
            () => engine.SubmitBiochemicalAsync(orderId, "Biochemical Test", "   ", null, AnalystId));

        Assert.Equal(WorkflowErrorCodes.BiochemicalResultRequired, ex.ErrorCode);
    }

    [Fact]
    public async Task ReviewerApprove_ClearsRequiresBiochemical()
    {
        var (orderId, engine, db) = await AllConformingAsync();
        await using var _ = db;
        await engine.RecordAnalystDecisionAsync(orderId, AnalystDecision.SubmitAsDetected, AnalystId);
        var resultId = (await db.WorkflowStepResults.SingleAsync(r => r.StepName == "Confirmatory Plating")).Id;

        await engine.RecordBiochemicalReviewDecisionAsync(resultId, approve: true, "Evidence sufficient.", ReviewerId);

        var stored = await db.WorkflowStepResults.SingleAsync(r => r.Id == resultId);
        Assert.False(stored.RequiresBiochemical);
        Assert.Null(stored.ReturnedAtUtc);
    }

    [Fact]
    public async Task ReviewerReturn_SetsTheReturnFieldsAndNotifiesTheAnalyst()
    {
        var (orderId, engine, db) = await AllConformingAsync();
        await using var _ = db;
        await engine.RecordAnalystDecisionAsync(orderId, AnalystDecision.SubmitAsDetected, AnalystId);
        var resultId = (await db.WorkflowStepResults.SingleAsync(r => r.StepName == "Confirmatory Plating")).Id;

        await engine.RecordBiochemicalReviewDecisionAsync(resultId, approve: false, "Required per SOP-MB-007.", ReviewerId);

        var stored = await db.WorkflowStepResults.SingleAsync(r => r.Id == resultId);
        Assert.True(stored.RequiresBiochemical);
        Assert.Equal("Required per SOP-MB-007.", stored.ReturnReason);
        Assert.Equal(ReviewerId, stored.ReturnedByUserId);
        Assert.NotNull(stored.ReturnedAtUtc);
    }

    [Fact]
    public async Task ReviewerReturn_WithoutAReason_IsRejected()
    {
        var (orderId, engine, db) = await AllConformingAsync();
        await using var _ = db;
        await engine.RecordAnalystDecisionAsync(orderId, AnalystDecision.SubmitAsDetected, AnalystId);
        var resultId = (await db.WorkflowStepResults.SingleAsync(r => r.StepName == "Confirmatory Plating")).Id;

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => engine.RecordBiochemicalReviewDecisionAsync(resultId, approve: false, "  ", ReviewerId));
    }

    [Fact]
    public async Task ReviewerCannotDecideOnTheirOwnResult()
    {
        var (orderId, engine, db) = await AllConformingAsync();
        await using var _ = db;
        await engine.RecordAnalystDecisionAsync(orderId, AnalystDecision.SubmitAsDetected, AnalystId);
        var resultId = (await db.WorkflowStepResults.SingleAsync(r => r.StepName == "Confirmatory Plating")).Id;

        var ex = await Assert.ThrowsAsync<WorkflowStepException>(
            () => engine.RecordBiochemicalReviewDecisionAsync(resultId, approve: true, "Fine.", AnalystId));

        Assert.Equal(WorkflowErrorCodes.SegregationOfDutiesViolation, ex.ErrorCode);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

```bash
dotnet test backend/MicroLIMS.Tests/MicroLIMS.Tests.csproj --filter "FullyQualifiedName~BiochemicalReviewTests"
```

Expected: compile errors — the three new methods do not exist.

- [ ] **Step 3: Add the three new engine dependencies**

Extend the constructor from Task 8:

```csharp
    private readonly SegregationOfDutiesGuard _sodGuard;
    private readonly ReviewGateService _reviewGate;
    private readonly INotificationService _notifications;

    public TestWorkflowEngine(
        MicroLimsDbContext db, SampleReviewService sampleReviewService, ResultProjectionService resultProjection,
        IncubatorEligibilityService incubatorEligibility, MediaAppearanceSnapshotService appearanceSnapshot,
        SegregationOfDutiesGuard sodGuard, ReviewGateService reviewGate, INotificationService notifications)
```

Add `using MicroLIMS.Infrastructure.Notifications;` to the file. Update `TestServiceFactory.TestWorkflow`:

```csharp
    public static TestWorkflowEngine TestWorkflow(MicroLimsDbContext db) =>
        new(db, SampleReview(db), ResultProjection(db), IncubatorEligibility(db), AppearanceSnapshot(db),
            new SegregationOfDutiesGuard(db), ReviewGate(db), new NoOpNotificationService());
```

And add the test double to `TestServiceFactory.cs`:

```csharp
// Tests assert on persisted state, not on delivery.
public class NoOpNotificationService : INotificationService
{
    public Task NotifyAsync(int userId, string message) => Task.CompletedTask;
}
```

If `MicroLIMS.Tests.csproj` does not already reference `MicroLIMS.Infrastructure`, add it:

```xml
    <ProjectReference Include="..\MicroLIMS.Infrastructure\MicroLIMS.Infrastructure.csproj" />
```

- [ ] **Step 4: Implement `RecordAnalystDecisionAsync`**

```csharp
    // Offered only once confirmatory plating came back AllConforming.
    // Submitting as Detected is allowed but is permanently flagged so a
    // reviewer sees that no biochemical confirmation was performed.
    public async Task<StepResultDto> RecordAnalystDecisionAsync(int testOrderId, AnalystDecision decision, int userId)
    {
        var confirmatory = await _db.WorkflowStepResults
            .Where(r => r.TestOrderId == testOrderId && r.StepType == StepType.ConfirmatoryPlating)
            .OrderByDescending(r => r.Id)
            .FirstOrDefaultAsync()
            ?? throw new InvalidOperationException("Confirmatory plating has not been completed for this test order.");

        if (confirmatory.ConfirmatoryResult != ConfirmatoryResult.AllConforming)
            throw new InvalidOperationException("An analyst decision is only available after an all-conforming confirmatory result.");

        if (decision == AnalystDecision.ProceedToBiochemical)
            return new StepResultDto(confirmatory.IncubationId, StepType.ConfirmatoryPlating.ToString(), "Complete",
                userId, confirmatory.SubmittedAtUtc, NextStepUnlocked: true, WorkflowFinalResult: null, Flags: new List<string>());

        confirmatory.SkippedBiochemical = true;
        await _db.SaveChangesAsync();

        await FinalizeWorkflowAsync(testOrderId, "Detected", userId);

        return new StepResultDto(confirmatory.IncubationId, StepType.ConfirmatoryPlating.ToString(), "Complete",
            userId, confirmatory.SubmittedAtUtc, NextStepUnlocked: false, WorkflowFinalResult: "Detected",
            Flags: new List<string> { "BiochemicalNotPerformed" });
    }
```

- [ ] **Step 5: Implement `SubmitBiochemicalAsync`**

```csharp
    // Free-text confirmation with an optional attachment. There is no
    // incubation lock and no media on this step.
    public async Task<StepResultDto> SubmitBiochemicalAsync(
        int testOrderId, string stepName, string biochemicalResultText, int? attachmentId, int userId)
    {
        var step = await LoadStepAsync(testOrderId, stepName);
        if (step.StepType != StepType.BiochemicalTest)
            throw new InvalidOperationException($"Step '{stepName}' is not a biochemical test step.");

        if (string.IsNullOrWhiteSpace(biochemicalResultText))
            throw new WorkflowStepException(WorkflowErrorCodes.BiochemicalResultRequired,
                "A biochemical result is required.");

        var confirmatory = await _db.WorkflowStepResults
            .Where(r => r.TestOrderId == testOrderId && r.StepType == StepType.ConfirmatoryPlating)
            .OrderByDescending(r => r.Id)
            .FirstOrDefaultAsync()
            ?? throw new InvalidOperationException("Confirmatory plating has not been completed for this test order.");

        if (confirmatory.ConfirmatoryResult != ConfirmatoryResult.AllConforming)
            throw new InvalidOperationException("A biochemical test is only available after an all-conforming confirmatory result.");

        // Reuses the confirmatory step's incubation as the step instance
        // - a biochemical test has no incubation window of its own.
        var result = new WorkflowStepResult
        {
            IncubationId = confirmatory.IncubationId, TestOrderId = testOrderId,
            StepName = step.StepName, StepType = step.StepType,
            BiochemicalResultText = biochemicalResultText, BiochemicalAttachmentId = attachmentId,
            SkippedBiochemical = false,
            SubmittedByUserId = userId, SubmittedAtUtc = DateTime.UtcNow
        };
        _db.WorkflowStepResults.Add(result);

        // Clears a reviewer's outstanding send-back, if there was one.
        confirmatory.RequiresBiochemical = false;
        confirmatory.SkippedBiochemical = false;
        await _db.SaveChangesAsync();

        await FinalizeWorkflowAsync(testOrderId, "Detected", userId);

        return new StepResultDto(result.IncubationId, step.StepType.ToString(), "Complete",
            userId, result.SubmittedAtUtc, NextStepUnlocked: false, WorkflowFinalResult: "Detected", Flags: new List<string>());
    }
```

This writes a second `WorkflowStepResult` against the confirmatory step's incubation. That is why the `IncubationId` index in Task 3 is deliberately **not** unique — confirm it reads `builder.HasIndex(r => r.IncubationId);` before running the tests.

- [ ] **Step 6: Implement `RecordBiochemicalReviewDecisionAsync`**

```csharp
    // Reviewer action on a result flagged BiochemicalNotPerformed.
    // Returning re-opens the biochemical step for the analyst; the
    // signature/timeline entry goes through the existing review gate.
    public async Task<StepResultDto> RecordBiochemicalReviewDecisionAsync(
        int workflowStepResultId, bool approve, string comment, int reviewerUserId)
    {
        var result = await _db.WorkflowStepResults.FirstOrDefaultAsync(r => r.Id == workflowStepResultId)
            ?? throw new InvalidOperationException($"Workflow step result {workflowStepResultId} not found.");

        if (await _sodGuard.DidUserPerformTestAsync(result.TestOrderId, reviewerUserId))
            throw new WorkflowStepException(WorkflowErrorCodes.SegregationOfDutiesViolation,
                "A reviewer cannot decide on a result they performed.");

        if (!approve && string.IsNullOrWhiteSpace(comment))
            throw new InvalidOperationException("A reason is required when returning a result for biochemical confirmation.");

        var order = await _db.TestOrders.FirstAsync(t => t.Id == result.TestOrderId);

        if (approve)
        {
            result.RequiresBiochemical = false;
            await _reviewGate.LogEventAsync(ReviewEntityTypes.Sample, order.SampleId, reviewerUserId,
                ReviewWorkflowEventType.ReviewCompleted, comment, ApprovalDecision.Approve);
            await _db.SaveChangesAsync();

            return new StepResultDto(result.IncubationId, result.StepType.ToString(), "Approved",
                result.SubmittedByUserId, result.SubmittedAtUtc, NextStepUnlocked: false,
                WorkflowFinalResult: "Detected", Flags: new List<string>());
        }

        result.RequiresBiochemical = true;
        result.ReturnReason = comment;
        result.ReturnedAtUtc = DateTime.UtcNow;
        result.ReturnedByUserId = reviewerUserId;

        // TransitionAsync reads the real FromStep off the order and owns
        // the WorkflowStep -> ApprovalStatus mapping. Hardcoding
        // FromStep here would record a transition that never happened:
        // FinalizeWorkflowAsync leaves the order at Ready, not Reviewed.
        await WorkflowStateMachine.TransitionAsync(_db, order, WorkflowStep.Incubating, reviewerUserId,
            $"Returned for biochemical confirmation: {comment}");

        await _reviewGate.LogEventAsync(ReviewEntityTypes.Sample, order.SampleId, reviewerUserId,
            ReviewWorkflowEventType.ReviewCompleted, comment, ApprovalDecision.Investigation);
        await _db.SaveChangesAsync();

        if (order.AssignedAnalystId is int analystId)
            await _notifications.NotifyAsync(analystId,
                $"Test order #{result.TestOrderId} was returned for biochemical confirmation.");

        return new StepResultDto(result.IncubationId, result.StepType.ToString(), "ReturnedForBiochemical",
            result.SubmittedByUserId, result.SubmittedAtUtc, NextStepUnlocked: true,
            WorkflowFinalResult: null, Flags: new List<string> { "ReturnedForBiochemical" });
    }
```

- [ ] **Step 7: Run the tests to verify they pass**

```bash
dotnet test backend/MicroLIMS.Tests/MicroLIMS.Tests.csproj --filter "FullyQualifiedName~BiochemicalReviewTests"
```

Expected: PASS, 9 tests. `ReviewerCannotDecideOnTheirOwnResult` depends on `SegregationOfDutiesGuard` seeing the analyst's `WorkflowStepResult` rows — that is the line added in Task 8 Step 6.

- [ ] **Step 8: Commit**

```bash
git -C E:/MicroLIMS/MicroLIMS add backend/MicroLIMS.Application backend/MicroLIMS.Tests && git -C E:/MicroLIMS/MicroLIMS commit -m "feat: add analyst decision, biochemical test, and reviewer send-back"
```

---

## Task 12: API endpoints (spec Part 4) and the sample header DTO (spec Part 5)

**Files:**
- Modify: `backend/MicroLIMS.API/Controllers/TestWorkflowController.cs`

**Interfaces:**
- Consumes: every `ITestWorkflowEngine` method from Tasks 8–11, `IncubatorEligibilityService`, `WorkflowErrorCodes`, `ApiResponse<T>`.
- Produces: the eight routes listed below, all under `api/test-workflow` with the controller's existing `[Authorize]` attribute.

The spec writes routes as `/api/workflow-steps/{stepInstanceId}/...`. This codebase routes workflow actions by **`testOrderId`**, and a step is addressed by name within that order — `TestWorkflowService.ts` and every existing caller depend on it. Keeping `api/test-workflow/{testOrderId}/...` avoids inventing a second addressing scheme. Record this deviation in the Part 8 report.

- [ ] **Step 1: Add the error-code translation helper**

Add to `TestWorkflowController`, and wrap every engine call in it. `WorkflowStepException` already returns 400 via middleware, but without the machine-readable code — this attaches it.

```csharp
    // ApiResponse has no error-code field, so the code travels as the
    // first entry in Errors, with remainingSeconds appended when the
    // failure is an incubation lock.
    private async Task<IActionResult> RunAsync<T>(Func<Task<T>> action)
    {
        try
        {
            return Ok(ApiResponse<object>.Ok(await action()!));
        }
        catch (WorkflowStepException ex)
        {
            var errors = new List<string> { ex.ErrorCode };
            if (ex.RemainingSeconds is long remaining)
                errors.Add(remaining.ToString());
            return BadRequest(ApiResponse<object>.Fail(ex.Message, errors));
        }
    }
```

- [ ] **Step 2: Replace the request records**

Delete `SelectMediaRequest`, `RecordTestResultRequest`, `BatchPathogenLocationRequest`, and `BatchPathogenResultsRequest`'s dual-plate fields. Add:

```csharp
public record SubmitBrothRequest(string StepName, int MediaLotId, int EquipmentId,
    DateTime IncubationStartUtc, DateTime IncubationEndUtc, string? Observation);

public record SubmitSelectivePlatingRequest(string StepName, int MediaLotId, int EquipmentId,
    DateTime IncubationStartUtc, DateTime IncubationEndUtc, GrowthObservation Observation);

public record ConfirmatorySelectionRequest(int StepMediaId, int MediaLotId, int EquipmentId);
public record SubmitConfirmatorySetupRequest(string StepName, List<ConfirmatorySelectionRequest> Selections,
    DateTime IncubationStartUtc, DateTime IncubationEndUtc);

public record ConfirmatoryObservationRequest(int MaterialId, GrowthObservation Observation);
public record SubmitConfirmatoryObservationsRequest(string StepName, List<ConfirmatoryObservationRequest> Observations);

public record AnalystDecisionRequest(AnalystDecision Decision);
public record SubmitBiochemicalRequest(string StepName, string BiochemicalResultText, int? AttachmentId);
public record BiochemicalReviewRequest(bool Approve, string Comment);
```

`BatchResultsRequest`, `BatchResultLocationRequest`, and the EM/batch routes are unchanged — they never used the dual-plate model except through the parameter removed in Task 8.

- [ ] **Step 3: Add the eight endpoints**

```csharp
    [HttpGet("{testOrderId}/eligible-incubators/{stepMediaId}")]
    public async Task<IActionResult> GetEligibleIncubators(int testOrderId, int stepMediaId)
    {
        var incubators = await _incubatorEligibility.GetEligibleIncubatorsAsync(stepMediaId);
        var stepMedia = await _db.TestWorkflowStepMedias.FirstOrDefaultAsync(m => m.Id == stepMediaId)
            ?? throw new InvalidOperationException($"Step media {stepMediaId} not found.");

        return Ok(ApiResponse<object>.Ok(new
        {
            stepMediaId,
            tempMin = stepMedia.TempMin,
            tempMax = stepMedia.TempMax,
            eligibleIncubators = incubators.Select(i => new
            {
                i.Id, name = i.Name, code = i.Code, setTemperature = i.SetTemperature, calibrationStatus = i.CalibrationStatus
            })
        }));
    }

    [HttpGet("{testOrderId}/permitted-confirmatory-media")]
    public async Task<IActionResult> GetPermittedConfirmatoryMedia(int testOrderId, [FromQuery] string stepName)
    {
        var order = await _db.TestOrders.FirstOrDefaultAsync(t => t.Id == testOrderId)
            ?? throw new InvalidOperationException($"Test order {testOrderId} not found.");
        var step = await _db.TestWorkflowSteps
            .Include(s => s.StepMedia).ThenInclude(m => m.Material)
            .Include(s => s.TargetOrganism)
            .Include(s => s.TestDefinition)
            .FirstOrDefaultAsync(s => s.TestDefinition!.Code == order.TestCode && s.StepName == stepName)
            ?? throw new InvalidOperationException($"Step '{stepName}' is not part of {order.TestCode}.");

        var permitted = new List<object>();
        foreach (var medium in step.StepMedia.OrderBy(m => m.DisplayOrder))
        {
            var expected = step.TargetOrganismId is int organismId
                ? await _appearanceSnapshot.GetExpectedAppearanceSnapshotAsync(medium.MaterialId, organismId)
                : null;

            var lots = await _db.Media
                .Where(m => m.MaterialId == medium.MaterialId && m.IsReleasedForUse && m.ExpiryDate > DateTime.UtcNow)
                .OrderBy(m => m.ExpiryDate)
                .Select(m => new { m.Id, lotNumber = m.LotNumber, expiryDate = m.ExpiryDate })
                .ToListAsync();

            permitted.Add(new
            {
                stepMediaId = medium.Id,
                materialId = medium.MaterialId,
                mediaName = medium.Material!.MaterialName,
                expectedAppearance = expected,
                tempMin = medium.TempMin,
                tempMax = medium.TempMax,
                availableLots = lots
            });
        }

        return Ok(ApiResponse<object>.Ok(new
        {
            testOrderId,
            stepName,
            organism = step.TargetOrganism is null ? null : new { step.TargetOrganism.Id, name = step.TargetOrganism.ScientificName },
            permittedMedia = permitted
        }));
    }

    [HttpPost("{testOrderId}/submit-broth")]
    public Task<IActionResult> SubmitBroth(int testOrderId, SubmitBrothRequest request) =>
        RunAsync(() => _engine.SubmitBrothAsync(testOrderId, request.StepName, request.MediaLotId, request.EquipmentId,
            request.IncubationStartUtc, request.IncubationEndUtc, request.Observation, CurrentUserId));

    [HttpPost("{testOrderId}/submit-selective-plating")]
    public Task<IActionResult> SubmitSelectivePlating(int testOrderId, SubmitSelectivePlatingRequest request) =>
        RunAsync(() => _engine.SubmitSelectivePlatingAsync(testOrderId, request.StepName, request.MediaLotId, request.EquipmentId,
            request.IncubationStartUtc, request.IncubationEndUtc, request.Observation, CurrentUserId));

    [HttpPost("{testOrderId}/submit-confirmatory-setup")]
    public Task<IActionResult> SubmitConfirmatorySetup(int testOrderId, SubmitConfirmatorySetupRequest request) =>
        RunAsync(() => _engine.SubmitConfirmatorySetupAsync(testOrderId, request.StepName,
            request.Selections.Select(s => new ConfirmatorySelectionInput(s.StepMediaId, s.MediaLotId, s.EquipmentId)).ToList(),
            request.IncubationStartUtc, request.IncubationEndUtc, CurrentUserId));

    [HttpPost("{testOrderId}/submit-confirmatory-observations")]
    public Task<IActionResult> SubmitConfirmatoryObservations(int testOrderId, SubmitConfirmatoryObservationsRequest request) =>
        RunAsync(() => _engine.SubmitConfirmatoryObservationsAsync(testOrderId, request.StepName,
            request.Observations.Select(o => new ConfirmatoryObservationInput(o.MaterialId, o.Observation)).ToList(),
            CurrentUserId));

    [HttpPost("{testOrderId}/analyst-decision")]
    public Task<IActionResult> RecordAnalystDecision(int testOrderId, AnalystDecisionRequest request) =>
        RunAsync(() => _engine.RecordAnalystDecisionAsync(testOrderId, request.Decision, CurrentUserId));

    [HttpPost("{testOrderId}/submit-biochemical")]
    public Task<IActionResult> SubmitBiochemical(int testOrderId, SubmitBiochemicalRequest request) =>
        RunAsync(() => _engine.SubmitBiochemicalAsync(testOrderId, request.StepName,
            request.BiochemicalResultText, request.AttachmentId, CurrentUserId));

    [Authorize(Roles = RoleConstants.Reviewer + "," + RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpPost("results/{workflowStepResultId}/biochemical-decision")]
    public Task<IActionResult> RecordBiochemicalDecision(int workflowStepResultId, BiochemicalReviewRequest request) =>
        RunAsync(() => _engine.RecordBiochemicalReviewDecisionAsync(
            workflowStepResultId, request.Approve, request.Comment, CurrentUserId));
```

The controller now needs `MicroLimsDbContext`, `IncubatorEligibilityService`, and `MediaAppearanceSnapshotService` injected alongside `ITestWorkflowEngine`. Add them to the constructor and the corresponding `using` directives (`Microsoft.EntityFrameworkCore`, `MicroLIMS.Application.Services`, `MicroLIMS.Domain.Enums`, `MicroLIMS.Persistence.DbContext`).

- [ ] **Step 4: Confirm the `Sample` field names the header block needs**

```bash
grep -n "public" backend/MicroLIMS.Domain/Entities/Sample.cs
```

Record the exact property names for: the product/material name, batch number, control number, reason for testing, stage, system reference number, and sample category. `ControlNumber` and `Category` (a `SampleCategory`) are confirmed to exist; the audit capture in `MicroLimsDbContext` also reads `BatchNumber` and `ReferenceNumber` off entities, so those names are very likely. Use whatever the file actually says — if a field genuinely has no counterpart (e.g. no "reason for testing" on `Sample`, only a `CauseOfTestingId` FK), include it by joining `CausesOfTesting` and note the shape in the report.

- [ ] **Step 5: Extend `GetCurrentStep` with `sampleContext`, `incubationLock`, and `previousSteps`**

Replace the existing `GetCurrentStep` response body. `stage` is **omitted entirely** — not null — for non-product samples, which is why it is assembled into a `Dictionary<string, object?>` rather than an anonymous type.

```csharp
    [HttpGet("{testOrderId}/current-step")]
    public async Task<IActionResult> GetCurrentStep(int testOrderId)
    {
        var current = await _engine.GetCurrentStepAsync(testOrderId);

        var order = await _db.TestOrders.Include(t => t.Sample)
            .FirstOrDefaultAsync(t => t.Id == testOrderId)
            ?? throw new InvalidOperationException($"Test order {testOrderId} not found.");
        var sample = order.Sample!;

        var sampleContext = new Dictionary<string, object?>
        {
            ["sampleName"] = sample.MaterialName,
            ["batchNumber"] = sample.BatchNumber,
            ["controlNumber"] = sample.ControlNumber,
            ["reason"] = sample.CauseOfTesting?.Name,
            ["systemReferenceNumber"] = sample.ReferenceNumber,
            ["sampleType"] = sample.Category.ToString()
        };

        // Stage is a product-only concept - the key is left out entirely
        // for water and environmental monitoring samples.
        if (sample.Category is SampleCategory.FinishedProduct or SampleCategory.InProcess or SampleCategory.RawMaterial)
            sampleContext["stage"] = sample.Category.ToString();

        var openIncubation = current.OpenIncubation;
        var incubationLock = openIncubation?.IncubationEndUtc is null ? null : new
        {
            isLocked = !openIncubation.IsIncubationComplete,
            incubationEndUtc = openIncubation.IncubationEndUtc,
            remainingSeconds = Math.Max(0, (long)Math.Ceiling((openIncubation.IncubationEndUtc.Value - DateTime.UtcNow).TotalSeconds))
        };

        var previousSteps = await _db.Incubations
            .Where(i => i.TestOrderId == testOrderId)
            .Include(i => i.Media).Include(i => i.IncubatorEquipment)
            .OrderBy(i => i.StepNumber).ThenBy(i => i.Id)
            .Select(i => new
            {
                stepNumber = i.StepNumber,
                stepName = i.StepName,
                status = i.CompletedAt == null ? "Incubating" : "Complete",
                mediaName = i.Media!.LotNumber,
                lotNumber = i.Media.LotNumber,
                incubatorName = i.IncubatorEquipment!.Code,
                incubatorSetTemp = i.IncubatorEquipment.SetPointTemperature,
                incubationStartUtc = i.IncubationStartUtc,
                incubationEndUtc = i.IncubationEndUtc,
                observation = i.Outcome
            })
            .ToListAsync();

        return Ok(ApiResponse<object>.Ok(new
        {
            testOrderId,
            step = current.Step is null ? null : new
            {
                current.Step.Id, current.Step.StepOrder, current.Step.StepName, current.Step.MediaTypeId,
                stepType = current.Step.StepType.ToString(),
                current.Step.TargetOrganismId,
                mediaType = current.Step.MediaType is null ? null : new { current.Step.MediaType.Id, current.Step.MediaType.Class },
                current.Step.IncubationMinHours, current.Step.IncubationMaxHours,
                current.Step.TemperatureMin, current.Step.TemperatureMax,
                current.Step.IsFinalStep
            },
            stepNumber = current.Step?.StepOrder,
            totalSteps = current.TotalSteps,
            testName = order.TestCode,
            workflowType = current.WorkflowType.ToString(),
            sampleContext,
            incubationLock,
            previousSteps,
            allStepsComplete = current.AllStepsComplete,
            finalResult = current.FinalResult,
            allSteps = current.AllSteps.Select(s => new { s.StepOrder, s.StepName })
        }));
    }
```

If `Sample` has no `CauseOfTesting` navigation, drop that `Include` and read the FK's name via a join; if it has no `MaterialName`, use whatever names the product — do not add a field to `Sample`.

- [ ] **Step 6: Build the API project**

```bash
dotnet build backend/MicroLIMS.API/MicroLIMS.API.csproj
```

Expected: PASS. Remaining errors will be in `MasterDataController.cs` — Task 13.

- [ ] **Step 7: Commit**

```bash
git -C E:/MicroLIMS/MicroLIMS add backend/MicroLIMS.API && git -C E:/MicroLIMS/MicroLIMS commit -m "feat: add pathogen workflow step endpoints and sample header context"
```

---

## Task 13: Test Master step CRUD (spec 3.1 wiring)

**Files:**
- Modify: `backend/MicroLIMS.API/Controllers/MasterDataController.cs`

**Interfaces:**
- Consumes: `WorkflowTemplateValidator`, `TemplateValidationError`, `StepType`, `TestWorkflowStepMedia`.
- Produces: `StepMediaRequest`, updated `CreateTestWorkflowStepRequest` / `UpdateTestWorkflowStepRequest`, and a `GET test-definitions/{id}/steps` response that includes `stepMedia`.

- [ ] **Step 1: Replace the step request records**

```csharp
public record StepMediaRequest(int MaterialId, decimal TempMin, decimal TempMax, bool IsRequired, int DisplayOrder);

public record CreateTestWorkflowStepRequest(string StepName, int MediaTypeId,
    int IncubationMinHours, int IncubationMaxHours, decimal TemperatureMin, decimal TemperatureMax,
    bool IsFinalStep, StepType StepType, int? TargetOrganismId, List<StepMediaRequest> StepMedia);

public record UpdateTestWorkflowStepRequest(string StepName, int MediaTypeId,
    int IncubationMinHours, int IncubationMaxHours, decimal TemperatureMin, decimal TemperatureMax,
    bool IsFinalStep, StepType StepType, int? TargetOrganismId, List<StepMediaRequest> StepMedia);
```

- [ ] **Step 2: Replace `ValidateStepRulesAsync`**

The dual-plate label rules are gone; the six structural rules come from `WorkflowTemplateValidator`. The "only one final step per test" rule is kept — it is a template-level invariant the validator (which sees one step at a time) cannot check.

```csharp
    // Structural rules come from WorkflowTemplateValidator; this adds the
    // one rule that spans the whole template rather than a single step.
    private async Task ValidateStepRulesAsync(int testDefinitionId, int? excludeStepId, TestWorkflowStep candidate)
    {
        var errors = WorkflowTemplateValidator.Validate(candidate);
        if (errors.Count > 0)
            throw new InvalidOperationException(string.Join(" ",
                errors.Select(e => $"Rule {e.RuleNumber} ({e.StepName}): {e.Message}")));

        if (candidate.IsFinalStep)
        {
            var otherFinalExists = await _db.TestWorkflowSteps
                .AnyAsync(s => s.TestDefinitionId == testDefinitionId && s.IsFinalStep && s.Id != (excludeStepId ?? -1));
            if (otherFinalExists)
                throw new InvalidOperationException("Only one step per test can be marked as the final step.");
        }
    }
```

- [ ] **Step 3: Rewrite `CreateTestWorkflowStep`**

The `DualPlate` workflow-type guard is removed with the enum value. The step and its media are built in memory first so the validator sees the whole thing before anything is written.

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
            IsFinalStep = request.IsFinalStep, StepType = request.StepType, TargetOrganismId = request.TargetOrganismId
        };
        entity.StepMedia.AddRange(request.StepMedia.Select(m => new TestWorkflowStepMedia
        {
            MaterialId = m.MaterialId, TempMin = m.TempMin, TempMax = m.TempMax,
            IsRequired = m.IsRequired, DisplayOrder = m.DisplayOrder
        }));

        await ValidateStepRulesAsync(id, excludeStepId: null, entity);

        _db.TestWorkflowSteps.Add(entity);
        await _db.SaveChangesAsync();
        await ValidateContiguousStepOrderAsync(id);
        return Ok(ApiResponse<object>.Ok(new { entity.Id, entity.StepOrder, entity.StepName }));
    }
```

- [ ] **Step 4: Rewrite `UpdateTestWorkflowStep`**

`StepMedia` is replaced wholesale on update — the analyst edits the panel as a set, and the unique index makes incremental merging error-prone for no benefit.

```csharp
    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpPut("test-definitions/steps/{stepId}")]
    public async Task<IActionResult> UpdateTestWorkflowStep(int stepId, UpdateTestWorkflowStepRequest request)
    {
        var step = await _db.TestWorkflowSteps.Include(s => s.StepMedia)
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

        _db.TestWorkflowStepMedias.RemoveRange(step.StepMedia);
        step.StepMedia.Clear();
        step.StepMedia.AddRange(request.StepMedia.Select(m => new TestWorkflowStepMedia
        {
            TestWorkflowStepId = step.Id, MaterialId = m.MaterialId, TempMin = m.TempMin, TempMax = m.TempMax,
            IsRequired = m.IsRequired, DisplayOrder = m.DisplayOrder
        }));

        await ValidateStepRulesAsync(step.TestDefinitionId, excludeStepId: stepId, step);
        await _db.SaveChangesAsync();

        return Ok(ApiResponse<object>.Ok(new { step.Id, step.StepOrder, step.StepName }));
    }
```

- [ ] **Step 5: Include `stepMedia` in the steps listing**

```csharp
    [HttpGet("test-definitions/{id}/steps")]
    public async Task<IActionResult> GetTestWorkflowSteps(int id) =>
        Ok(ApiResponse<object>.Ok(await _db.TestWorkflowSteps
            .Include(s => s.MediaType)
            .Include(s => s.TargetOrganism)
            .Include(s => s.StepMedia).ThenInclude(m => m.Material)
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
                })
            })
            .ToListAsync()));
```

- [ ] **Step 6: Build the whole solution**

```bash
dotnet build backend/MicroLIMS.sln
```

Expected: PASS. Any remaining error is in the test project — Task 14.

- [ ] **Step 7: Commit**

```bash
git -C E:/MicroLIMS/MicroLIMS add backend/MicroLIMS.API && git -C E:/MicroLIMS/MicroLIMS commit -m "feat: carry step type, target organism, and step media through Test Master"
```

---

## Task 14: Retire the dual-plate tests and prove the full chain green

**Files:**
- Rewrite: `backend/MicroLIMS.Tests/WorkflowTests/PathogenWorkflowTests.cs`
- Modify: `backend/MicroLIMS.Tests/WorkflowTests/CountTestWorkflowTests.cs`
- Modify: any other test referencing `StepResultType`, `IsDualPlate`, `GrowthObserved`, or `WorkflowType.DualPlate`

**Interfaces:**
- Consumes: `PathogenTestData` (Task 8), every engine method from Tasks 8–11.
- Produces: no new production interfaces — this task ends with the whole suite green.

- [ ] **Step 1: Find every remaining reference to the removed model**

```bash
grep -rn "StepResultType\|IsDualPlate\|GrowthObserved\|DualPlate\|DualGrowth\|Plate1Label\|Plate2MediaId" backend/MicroLIMS.Tests
```

Every hit must be resolved in this task. There should be none left in `backend/MicroLIMS.Application`, `backend/MicroLIMS.API`, `backend/MicroLIMS.Domain`, or `backend/MicroLIMS.Persistence` — run the same grep over those and fix anything that appears.

- [ ] **Step 2: Update `CountTestWorkflowTests.cs`**

Mechanical: `StepResultType.PlateCount` → `StepType.PlateCount`, and remove the `IsDualPlate = false` initialisers. The count-test behaviour itself is unchanged, so every assertion stays as it is. Do not weaken or delete any count-test assertion — if one now fails, the refactor broke count tests and that is a real regression to fix.

- [ ] **Step 3: Rewrite `PathogenWorkflowTests.cs` as the end-to-end chain**

Replace the entire file. The per-stage behaviours are covered by Tasks 8–11's suites; this file proves the five stages compose.

```csharp
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Domain.Enums;
using Xunit;

namespace MicroLIMS.Tests.WorkflowTests;

// End-to-end pathogen chain: Broth Enrichment -> Selective Broth ->
// Selective Plating -> Confirmatory Plating -> Biochemical Test, driven
// entirely by the seeded template. No step name is special-cased in the
// engine; this is one template shape among many.
public class PathogenWorkflowTests
{
    private const int AnalystId = 4;

    [Fact]
    public async Task FullChain_AllConformingThroughBiochemical_EndsDetected()
    {
        await using var db = PathogenTestData.NewDb();
        var (order, media, incubator) = await PathogenTestData.SeedFiveStageOrderAsync(db);
        var engine = TestServiceFactory.TestWorkflow(db);
        var start = DateTime.UtcNow.AddHours(-30);
        var end = DateTime.UtcNow.AddHours(-6);

        await engine.SubmitBrothAsync(order.Id, "Broth Enrichment", media.BrothLotId, incubator.Id, start, end, "Turbid.", AnalystId);
        await engine.SubmitBrothAsync(order.Id, "Selective Broth", media.SelectiveBrothLotId, media.SelectiveBrothIncubatorId, start, end, null, AnalystId);
        await engine.SubmitSelectivePlatingAsync(order.Id, "Selective Plating", media.SelectivePlatingLotId, incubator.Id,
            start, end, GrowthObservation.GrowthConforming, AnalystId);
        await engine.SubmitConfirmatorySetupAsync(order.Id, "Confirmatory Plating", new[]
        {
            new ConfirmatorySelectionInput(media.XldStepMediaId, media.XldLotId, incubator.Id),
            new ConfirmatorySelectionInput(media.TsiStepMediaId, media.TsiLotId, incubator.Id)
        }, start, end, AnalystId);
        await engine.SubmitConfirmatoryObservationsAsync(order.Id, "Confirmatory Plating", new[]
        {
            new ConfirmatoryObservationInput(media.XldMaterialId, GrowthObservation.GrowthConforming),
            new ConfirmatoryObservationInput(media.TsiMaterialId, GrowthObservation.GrowthConforming)
        }, AnalystId);
        await engine.RecordAnalystDecisionAsync(order.Id, AnalystDecision.ProceedToBiochemical, AnalystId);
        var final = await engine.SubmitBiochemicalAsync(order.Id, "Biochemical Test", "IMViC: + + - -", null, AnalystId);

        Assert.Equal("Detected", final.WorkflowFinalResult);

        var reloaded = await db.TestOrders.SingleAsync(t => t.Id == order.Id);
        Assert.Equal(WorkflowStep.Ready, reloaded.CurrentStep);
        Assert.Single(await db.Results.Where(r => r.TestOrderId == order.Id).ToListAsync());
    }

    [Fact]
    public async Task Chain_StopsAtSelectivePlating_WhenGrowthIsNonConforming()
    {
        await using var db = PathogenTestData.NewDb();
        var (order, media, incubator) = await PathogenTestData.SeedFiveStageOrderAsync(db);
        var engine = TestServiceFactory.TestWorkflow(db);
        var start = DateTime.UtcNow.AddHours(-30);
        var end = DateTime.UtcNow.AddHours(-6);

        await engine.SubmitBrothAsync(order.Id, "Broth Enrichment", media.BrothLotId, incubator.Id, start, end, null, AnalystId);
        await engine.SubmitBrothAsync(order.Id, "Selective Broth", media.SelectiveBrothLotId, media.SelectiveBrothIncubatorId, start, end, null, AnalystId);
        var result = await engine.SubmitSelectivePlatingAsync(order.Id, "Selective Plating", media.SelectivePlatingLotId,
            incubator.Id, start, end, GrowthObservation.GrowthNonConforming, AnalystId);

        Assert.Equal("NotDetected", result.WorkflowFinalResult);
        Assert.Empty(await db.WorkflowStepResults.Where(r => r.StepType == StepType.ConfirmatoryPlating).ToListAsync());
    }

    [Fact]
    public async Task BrothSteps_DoNotBranchOnTheirObservationText()
    {
        await using var db = PathogenTestData.NewDb();
        var (order, media, incubator) = await PathogenTestData.SeedFiveStageOrderAsync(db);
        var engine = TestServiceFactory.TestWorkflow(db);
        var start = DateTime.UtcNow.AddHours(-30);
        var end = DateTime.UtcNow.AddHours(-6);

        // "No turbidity" is recorded verbatim and changes nothing - broth
        // steps carry no result logic (spec 3.4).
        var result = await engine.SubmitBrothAsync(order.Id, "Broth Enrichment", media.BrothLotId, incubator.Id,
            start, end, "No turbidity observed.", AnalystId);

        Assert.Null(result.WorkflowFinalResult);
        Assert.True(result.NextStepUnlocked);
    }

    [Fact]
    public async Task SelectingAnUnreleasedLot_IsRejected()
    {
        await using var db = PathogenTestData.NewDb();
        var (order, media, incubator) = await PathogenTestData.SeedFiveStageOrderAsync(db);

        var lot = await db.Media.SingleAsync(m => m.Id == media.BrothLotId);
        lot.IsReleasedForUse = false;
        await db.SaveChangesAsync();

        var engine = TestServiceFactory.TestWorkflow(db);
        await Assert.ThrowsAsync<InvalidOperationException>(() => engine.SubmitBrothAsync(
            order.Id, "Broth Enrichment", media.BrothLotId, incubator.Id,
            DateTime.UtcNow.AddHours(-30), DateTime.UtcNow.AddHours(-6), null, AnalystId));
    }
}
```

- [ ] **Step 4: Run the full suite**

```bash
dotnet test backend/MicroLIMS.Tests/MicroLIMS.Tests.csproj
```

Expected: PASS, every test. If an unrelated suite (inventory, integration) fails, it is a real regression from the entity changes — fix the cause, do not skip the test.

- [ ] **Step 5: Commit**

```bash
git -C E:/MicroLIMS/MicroLIMS add backend/MicroLIMS.Tests && git -C E:/MicroLIMS/MicroLIMS commit -m "test: replace dual-plate coverage with five-stage pathogen chain"
```

---

## Task 15: Verification and the Part 8 backend report

**Files:**
- Create: `docs/superpowers/reports/2026-08-10-pathogen-workflow-backend-report.md`

**Interfaces:**
- Consumes: the finished implementation.
- Produces: the report the spec's Part 8 requires before any frontend work begins.

- [ ] **Step 1: Run the full verification sweep and capture the actual output**

```bash
dotnet build backend/MicroLIMS.sln && dotnet test backend/MicroLIMS.Tests/MicroLIMS.Tests.csproj
```

Do not write the report from memory or expectation. Paste real command output. If either command fails, the report says so and Section 7 is "No".

- [ ] **Step 2: Confirm nothing frontend changed**

```bash
git -C E:/MicroLIMS/MicroLIMS diff --stat HEAD~10 -- frontend
```

Expected: empty. The spec forbids frontend edits in this prompt. If anything shows, revert it.

- [ ] **Step 3: Confirm the snapshot-immutability constraint holds**

```bash
grep -rn "ExpectedAppearanceSnapshot" backend --include=*.cs
```

Every hit must be either the property declaration, an object-initialiser at creation, a read, or a test assertion. **No assignment to an already-persisted entity's `ExpectedAppearanceSnapshot`** may exist. If one does, remove it — this is a hard constraint, not a preference.

- [ ] **Step 4: Write the report**

Create `docs/superpowers/reports/2026-08-10-pathogen-workflow-backend-report.md` using the spec's exact Part 8 format:

```
=== MicroLIMS Pathogen Workflow - Backend Refactor Report ===

1. MIGRATION STATUS
   - Migration name: AddPathogenWorkflowRefactor
   - Tables created: TestWorkflowStepMedias, WorkflowStepResults,
     ConfirmatoryMediaSelections, ConfirmatoryPlateObservations
   - Columns added: TestWorkflowSteps.TargetOrganismId;
     Incubations.IncubationStartUtc, Incubations.IncubationEndUtc;
     PathogenObservations.Observation
   - Columns removed: TestWorkflowSteps.IsDualPlate,
     TestWorkflowSteps.Plate1DefaultLabel, TestWorkflowSteps.Plate2DefaultLabel;
     Incubations.Plate2MediaId; PathogenObservations.GrowthObserved,
     PathogenObservations.PlateLabel
   - Enum changes: WorkflowType lost DualPlate; StepResultType renamed to
     StepType and its Growth/DualGrowth values replaced with the five
     pathogen stages; GrowthObservation, ConfirmatoryResult, and
     AnalystDecision added. All enums persist as integers.

2. ENDPOINTS IMPLEMENTED
   [one line per route, method + path + status]

3. FINAL REQUEST/RESPONSE SHAPES
   [paste the actual request and response bodies as implemented, taken
    from the controller code - not from the spec. Note every deviation
    and why, including the testOrderId-based routing.]

4. VALIDATION RULES IMPLEMENTED
   [each rule with the error code it returns]

5. AUDIT EVENTS REGISTERED
   [each event and its trigger]

6. KNOWN GAPS OR DEVIATIONS
   [see the running list below - add anything found during execution]

7. READY FOR PROMPT 2
   [Yes / No, with blockers if No]
```

- [ ] **Step 5: Populate Section 6 with the deviations this plan already knows about**

These are decided, not open questions. Carry them into the report verbatim and add anything discovered during execution:

1. **Routing:** endpoints are `api/test-workflow/{testOrderId}/...`, not `/api/workflow-steps/{stepInstanceId}/...`. This codebase addresses workflow actions by test order with the step named in the body; a second addressing scheme would have been a new pattern.
2. **`WorkflowType` scope:** the spec's Part 1.1 list would have deleted `CountTest` and `Observation`, breaking every TAMC/TYMC and non-pathogen test. The five stage names were implemented as the per-step `StepType` enum instead, which is what the rest of the spec describes them as.
3. **Media terminology:** `MediaId` in the spec maps to `MaterialId` here (the medium) and `MediaLotId` maps to `MediaId` (the released lot). See the plan's terminology table.
4. **`MediaEvaluationCriteria`** does not exist; `MediaChallengeSpec.ExpectedDescription` is the source for appearance snapshots, matched by material *name* because that table keys on `MaterialName`.
5. **No `Attachment` entity exists.** `BiochemicalAttachmentId` is an unmapped `int?` with no FK — a hook for a future attachments feature.
6. **`ReviewDecision` does not exist.** Biochemical send-back state lives on `WorkflowStepResult`; the signature and timeline entry go through the existing `ReviewGateService` + `ReviewWorkflowEvent`, which is shared by Sample/Media/Cryovial gates.
7. **Equipment has no `IsActive`.** Incubator eligibility is `Type == Incubator`, a non-null `SetPointTemperature` within range, and calibration not past due; `CalibrationStatus` is derived rather than stored.
8. **Audit events** are not a new event-type registry. Inserts and updates on every new entity are captured automatically by `MicroLimsDbContext.CaptureAuditEntries` into `AuditLog`; semantic transitions go to `WorkflowHistory` and `ReviewWorkflowEvent`. The spec's nine `PathoWorkflow.*` names are documented as the mapping in Section 5, not implemented as a parallel mechanism — the spec forbids creating one.
9. **Frontend is broken by design.** `TestWorkflowDialog.tsx`, `TestMasterPage.tsx`, `TestWorkflowService.ts`, and `masterDataOptions.ts` still expect `stepResultType`, `plate1DefaultLabel`, `growthObserved`, and the removed routes. Prompt 2 must update them.
10. **Existing DualPlate data needs manual attention.** The migration re-types `Growth` → `SelectivePlating` and `DualGrowth` → `ConfirmatoryPlating`, but no migrated step has a `TargetOrganismId` or `StepMedia` rows, so every pre-existing pathogen template must be re-saved through Test Master before it will validate. Salmonella's XLD+TSI panel in particular must be re-entered.

- [ ] **Step 6: Commit**

```bash
git -C E:/MicroLIMS/MicroLIMS add docs && git -C E:/MicroLIMS/MicroLIMS commit -m "docs: add pathogen workflow backend refactor report"
```

---

## Spec coverage map

Every numbered section of `MicroLIMS_Pathogen_Workflow_Prompt1.md` and where this plan implements it:

| Spec | Task |
|---|---|
| 1.1 WorkflowType / remove DualPlate | 1 (as `StepType`; see deviation 2) |
| 1.2 GrowthObservation enum | 1 |
| 1.3 TestWorkflowStep fields | 2 |
| 1.4 TestWorkflowStepMedia | 2, 3 |
| 1.5 WorkflowStepResult | 2, 3 |
| 1.6 ConfirmatoryMediaSelection | 2, 3 |
| 1.7 ConfirmatoryPlateObservation | 2, 3 |
| 1.8 ReviewDecision fields | 2 (on `WorkflowStepResult`), 11 |
| 1.9 Incubation lock fields | 2 |
| Part 2 migration | 4 |
| 3.1 template validation rules 1–6 | 5, 13 |
| 3.2 incubator filtering | 6 |
| 3.3 appearance snapshot | 7 |
| 3.4 broth steps | 8 |
| 3.4 selective plating | 9 |
| 3.4 confirmatory plating | 10 |
| 3.4 analyst decision / biochemical / review return | 11 |
| 3.5 incubation lock enforcement | 8 |
| 4.1 endpoints | 12 |
| 4.2 standard step result DTO | 8 (`StepResultDto`) |
| Part 5 sample header DTO | 12 |
| Part 6 audit trail | automatic via `CaptureAuditEntries`; documented in 15 |
| Part 7 validation rules 1–8 | 8 (1, 6), 10 (2, 3, 4, 5), 11 (7, 8) |
| Part 8 report | 15 |

---

## Execution notes

- **Tasks 1–3 must be done together** before anything builds. Task 1 alone leaves the tree red on purpose; that is expected, not a failure.
- **Task 4 (the migration) may need to move after Task 13** if `dotnet ef` cannot scaffold against a non-compiling API project. Either order is fine; note it in the report if changed.
- **Do not weaken a failing test to make it pass.** If a count-test or inventory test breaks, the refactor caused a real regression.
- **Every task ends with a commit.** If a task's tests do not pass, stop and report rather than committing red.
