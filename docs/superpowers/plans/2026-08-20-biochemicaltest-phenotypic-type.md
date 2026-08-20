# BiochemicalTest Phenotypic Test Type Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Stop forcing a meaningless Media Type selection onto `BiochemicalTest` workflow steps in Test Master, and replace it with a proper `PhenotypicTestType` classification (Gram / Catalase / Oxidase / Coagulase / Antibiogram / Identification Kit).

**Architecture:** `TestWorkflowStep.MediaTypeId` becomes nullable and a new nullable `PhenotypicTestType` enum column is added. `WorkflowTemplateValidator` gets two new checks enforcing the two fields are mutually exclusive by `StepType`. The Test Master "Add Step" React form becomes reactive to `StepType`, swapping the Media Type dropdown for a Phenotypic Test Type dropdown when `BiochemicalTest` is selected, and hiding Incubation/Temp fields unless the phenotypic type is `Antibiogram`.

**Tech Stack:** ASP.NET Core / EF Core (backend), React + MUI + TypeScript, Vite (frontend), xUnit (backend tests).

**Spec:** This plan's spec is the task brief given directly in conversation (no separate spec doc) plus the investigation findings below, which correct several assumptions in that brief against the actual codebase.

## Global Constraints

- `PhenotypicTestType` enum values, exact names: `Gram`, `Catalase`, `Oxidase`, `Coagulase`, `Antibiogram`, `IdentificationKit`.
- Enums serialize as JSON strings project-wide (`Program.cs:68`, global `JsonStringEnumConverter`) — no custom converter needed anywhere in this plan.
- Follow the **existing** structural-validation pattern exactly: `WorkflowTemplateValidator.Validate()` returns `TemplateValidationError(RuleNumber, StepName, Message)` records; `MasterDataController.ValidateStepRulesAsync` joins them into one `InvalidOperationException`. Do **not** introduce `WorkflowStepException` here — that class (`TestWorkflowEngine.cs:28-38`) belongs to the separate *runtime step-submission* code path (e.g. `SubmitBiochemicalAsync`), which this plan does not touch.
- Do not touch `SelectivePlating` / `ConfirmatoryPlating` handling — already correct.
- Do not build the `ConfirmatoryIdentificationTest` result-entry entity/UI (free-text + outcome capture for analysts). Out of scope, deferred to a future session.
- Confirmed via direct inspection: `WorkflowStateResolver` never gates on a BiochemicalTest step's own `IncubationMinHours/MaxHours` — no `Incubation` row is ever created from a BiochemicalTest step's own template fields (`TestWorkflowEngine.cs:1936-1937`: *"a biochemical test has no incubation window of its own"*, reuses the ConfirmatoryPlating step's `IncubationId`). Storing `0` in those fields for non-Antibiogram BiochemicalTest steps is inert — never read for any gating decision.
- Decision (confirmed with user): Incubation/Temp fields (`IncubationMinHours`, `IncubationMaxHours`, `TemperatureMin`, `TemperatureMax`) stay **non-nullable** in the DB. For non-Antibiogram BiochemicalTest steps they are simply not shown in the form and saved as `0` — no backend rule enforces them to be `0`; this is a deliberate simplification (the fields are inert for that case, so enforcing a specific value adds rigidity with no behavioral benefit). Antibiogram continues to show and save real values, same as any other step type, but note: the engine does not yet *enforce* that window (that lands with the future result-entry work) — it is config/documentation-only today.
- Decision (confirmed with user): no frontend automated test this session — the frontend has zero test infrastructure (no vitest/jest, no `@testing-library/react`, no test script in `package.json`), and bootstrapping it was declined. Task 5 verifies the Add Step form manually via the browser instead.

## Corrections to the original task brief (read before starting)

1. There is no single `MediaId` field. Per-step media is `TestWorkflowStep.StepMedia: List<TestWorkflowStepMedia>`, and it's already correctly forced empty for `BiochemicalTest` at both layers (`WorkflowTemplateValidator.cs:48-53`, `TestMasterPage.tsx:81-84`). Nothing to fix there.
2. The field actually causing the bug is `MediaTypeId` — a separate, single, non-nullable FK on `TestWorkflowStep` used only for media-lot verification (`TestWorkflowEngine.cs:387`). It's forced for every `StepType` today per an explicit "do not hide" comment in `TestMasterPage.tsx:363-366`, and seeded with a dummy value (`DbSeeder.cs:279`, `MediaTypeId = selectiveAgarId`) for the sample BiochemicalTest step. This plan makes it nullable.
3. `WorkflowStepException` **does exist** (`TestWorkflowEngine.cs:28-38`), but it's the runtime-submission pattern, not the Test Master template-validation pattern — see Global Constraints above for which one this plan uses and why.
4. Incubation/Temp fields are read at 69 call sites across 13 backend files — full DB nullability for them was assessed as too large/risky for this session and was explicitly declined in favor of the UI-hide approach above.

---

## File Structure

| File | Responsibility |
|---|---|
| `backend/MicroLIMS.Domain/Enums/PhenotypicTestType.cs` | New enum: the six phenotypic test kinds. |
| `backend/MicroLIMS.Domain/Entities/TestWorkflowStep.cs` | `MediaTypeId` → nullable; add nullable `PhenotypicTestType` property. |
| `backend/MicroLIMS.Application/Services/WorkflowTemplateValidator.cs` | Extend Rule 4 (BiochemicalTest) + add Rule 8 (everything else) to enforce mutual exclusivity. |
| `backend/MicroLIMS.API/Controllers/MasterDataController.cs` | Request records + create/update handlers + GET projection carry `mediaTypeId` as nullable and add `phenotypicTestType`. |
| `backend/MicroLIMS.Persistence/Seed/DbSeeder.cs` | Fix the seeded BiochemicalTest step: drop the dummy `MediaTypeId`, set a real `PhenotypicTestType`. |
| `backend/MicroLIMS.Persistence/Migrations/<timestamp>_AddPhenotypicTestTypeToTestWorkflowStep.cs` | EF Core migration. |
| `backend/MicroLIMS.Tests/WorkflowTests/WorkflowTemplateValidationTests.cs` | New Rule 4 / Rule 8 test cases. |
| `frontend/src/modules/testingWorkspace/types/testWorkflowTypes.ts` | `TestWorkflowStepDto.mediaTypeId` → nullable (matches the already-nullable sibling type in `pathogenSessionTypes.ts:95`). |
| `frontend/src/services/masterDataOptions.ts` | `createTestWorkflowStep`/`updateTestWorkflowStep` payload types: `mediaTypeId` nullable, add `phenotypicTestType`. |
| `frontend/src/modules/laboratoryConfiguration/masterDataSimple/TestMasterPage.tsx` | Add Step form: conditional fields, new dropdown, table column, validation. |

---

## Task 1: Domain enum + entity + EF configuration

**Files:**
- Create: `MicroLIMS/backend/MicroLIMS.Domain/Enums/PhenotypicTestType.cs`
- Modify: `MicroLIMS/backend/MicroLIMS.Domain/Entities/TestWorkflowStep.cs`

**Interfaces:**
- Produces: `TestWorkflowStep.MediaTypeId` becomes `int?`; new `TestWorkflowStep.PhenotypicTestType` of type `PhenotypicTestType?`. Later tasks (validator, controller, migration) depend on both being nullable.

- [ ] **Step 1: Create the enum**

```csharp
namespace MicroLIMS.Domain.Enums;

// Classifies a BiochemicalTest step's phenotypic confirmation kind. Null
// for every non-BiochemicalTest StepType - see WorkflowTemplateValidator
// rules 4 and 8 for the mutual-exclusivity enforcement against MediaTypeId.
public enum PhenotypicTestType
{
    Gram,
    Catalase,
    Oxidase,
    Coagulase,
    Antibiogram,
    IdentificationKit
}
```

- [ ] **Step 2: Make `MediaTypeId` nullable and add `PhenotypicTestType` to the entity**

In `TestWorkflowStep.cs`, change:

```csharp
    public int MediaTypeId { get; set; }
    public MediaType? MediaType { get; set; }
```

to:

```csharp
    // Non-null for every StepType except BiochemicalTest, which uses
    // PhenotypicTestType instead - see WorkflowTemplateValidator rules 4/8.
    public int? MediaTypeId { get; set; }
    public MediaType? MediaType { get; set; }

    public PhenotypicTestType? PhenotypicTestType { get; set; }
```

Add `using MicroLIMS.Domain.Enums;` is already present at the top of the file (it's already imported for `StepType`).

- [ ] **Step 3: Build to confirm the entity change compiles**

Run: `dotnet build MicroLIMS/backend/MicroLIMS.sln`
Expected: build errors in `TestWorkflowEngine.cs`, `MasterDataController.cs`, `TestWorkflowController.cs`, `DbSeeder.cs`, `WorkflowTemplateValidator.cs`, `TestWorkflowStepConfiguration.cs` and various test files wherever `MediaTypeId` is assigned/compared as non-nullable in a way the compiler now flags (mostly it will still compile — `int?` unifies with `int` assignment sites via implicit conversion, and comparisons against non-null values still compile — but a couple of interpolated-string reads of `step.MediaType!.Class` may now need the existing null-forgiving operator you already saw at `TestWorkflowEngine.cs:389`, which is unaffected since that's about the `MediaType` navigation being loaded, not `MediaTypeId`'s nullability). Confirm the actual error list matches expectations before proceeding — do not paper over an unexpected one.

- [ ] **Step 4: Commit**

```bash
git add MicroLIMS/backend/MicroLIMS.Domain/Enums/PhenotypicTestType.cs MicroLIMS/backend/MicroLIMS.Domain/Entities/TestWorkflowStep.cs
git commit -m "domain: add PhenotypicTestType enum, make TestWorkflowStep.MediaTypeId nullable"
```

---

## Task 2: EF Core migration

**Files:**
- Create: migration files under `MicroLIMS/backend/MicroLIMS.Persistence/Migrations/` (name/timestamp assigned by the `dotnet ef` tool).

**Interfaces:**
- Consumes: `TestWorkflowStep.MediaTypeId` (now `int?`), `TestWorkflowStep.PhenotypicTestType` (new, `PhenotypicTestType?`) from Task 1.
- Produces: DB schema with `MediaTypeId` nullable and a new nullable `PhenotypicTestType` column (stored as the EF default - `int` - since no value converter is being added; this matches how `StepType` itself is already stored, per the existing `TestWorkflowStepConfiguration.cs` having no explicit conversion for it).

- [ ] **Step 1: Generate the migration**

Run (from `MicroLIMS/backend`):
```
dotnet ef migrations add AddPhenotypicTestTypeToTestWorkflowStep --project MicroLIMS.Persistence --startup-project MicroLIMS.API
```
Expected: a new `<timestamp>_AddPhenotypicTestTypeToTestWorkflowStep.cs` + `.Designer.cs`, and `MicroLimsDbContextModelSnapshot.cs` updated.

- [ ] **Step 2: Inspect the generated migration**

Open the new `Up()` method and confirm it contains exactly two operations: `AlterColumn` making `MediaTypeId` nullable on `TestWorkflowSteps`, and `AddColumn` for `PhenotypicTestType` (nullable `int`). If EF also touches the `MediaType` foreign key's `IsRequired`/`OnDelete` behavior, that's expected (an optional FK still uses `DeleteBehavior.Restrict` as already configured in `TestWorkflowStepConfiguration.cs:24-27` — no manual edit needed there since it doesn't hardcode required-ness).

- [ ] **Step 3: Apply the migration to the local dev database**

Run: `dotnet ef database update --project MicroLIMS.Persistence --startup-project MicroLIMS.API`
Expected: succeeds with no errors.

- [ ] **Step 4: Commit**

```bash
git add MicroLIMS/backend/MicroLIMS.Persistence/Migrations/
git commit -m "db: migration for nullable MediaTypeId and new PhenotypicTestType column"
```

---

## Task 3: WorkflowTemplateValidator rules

**Files:**
- Modify: `MicroLIMS/backend/MicroLIMS.Application/Services/WorkflowTemplateValidator.cs`
- Test: `MicroLIMS/backend/MicroLIMS.Tests/WorkflowTests/WorkflowTemplateValidationTests.cs`

**Interfaces:**
- Consumes: `TestWorkflowStep.MediaTypeId` (`int?`), `TestWorkflowStep.PhenotypicTestType` (`PhenotypicTestType?`) from Task 1.
- Produces: `WorkflowTemplateValidator.Validate(step)` now also fails Rule 4 when a BiochemicalTest step has `MediaTypeId != null` or `PhenotypicTestType == null`, and fails a new Rule 8 when any non-BiochemicalTest step has `MediaTypeId == null` or `PhenotypicTestType != null`. Later tasks (controller validation) rely on Rule 8 existing.

- [ ] **Step 1: Write the failing tests**

Add to `WorkflowTemplateValidationTests.cs` (the `Step` helper needs a `mediaTypeId`/`phenotypicTestType` overload - add it alongside the existing `Step` helper rather than changing the existing one's signature, since 12+ existing call sites use it positionally):

```csharp
    private static TestWorkflowStep StepWithMediaAndPhenotype(
        StepType type, int? mediaTypeId, PhenotypicTestType? phenotypicTestType,
        int? organismId = null, decimal tempMin = 35, decimal tempMax = 37,
        int incubationMinHours = 0, int incubationMaxHours = 0) =>
        new()
        {
            StepName = "S", StepType = type, TargetOrganismId = organismId,
            MediaTypeId = mediaTypeId, PhenotypicTestType = phenotypicTestType,
            TemperatureMin = tempMin, TemperatureMax = tempMax,
            IncubationMinHours = incubationMinHours, IncubationMaxHours = incubationMaxHours
        };

    [Fact]
    public void Rule4_BiochemicalTest_WithPhenotypicTestTypeAndNoMediaType_IsValid()
    {
        var step = StepWithMediaAndPhenotype(StepType.BiochemicalTest, mediaTypeId: null, phenotypicTestType: PhenotypicTestType.Catalase);
        var errors = WorkflowTemplateValidator.Validate(step);
        Assert.Empty(errors);
    }

    [Fact]
    public void Rule4_BiochemicalTest_WithMediaTypeId_FailsRule4()
    {
        var step = StepWithMediaAndPhenotype(StepType.BiochemicalTest, mediaTypeId: 3, phenotypicTestType: PhenotypicTestType.Catalase);
        var errors = WorkflowTemplateValidator.Validate(step);
        Assert.Contains(errors, e => e.RuleNumber == 4);
    }

    [Fact]
    public void Rule4_BiochemicalTest_WithoutPhenotypicTestType_FailsRule4()
    {
        var step = StepWithMediaAndPhenotype(StepType.BiochemicalTest, mediaTypeId: null, phenotypicTestType: null);
        var errors = WorkflowTemplateValidator.Validate(step);
        Assert.Contains(errors, e => e.RuleNumber == 4);
    }

    [Fact]
    public void Rule4_BiochemicalTest_Antibiogram_WithRealIncubationWindow_IsValid()
    {
        // Antibiogram is the one phenotypic type with a real incubation stage
        // (16-18h per SOP) - confirms the validator doesn't reject non-zero
        // Incubation/Temp values on a BiochemicalTest step, since those
        // fields are otherwise unvalidated/inert for this StepType.
        var step = StepWithMediaAndPhenotype(
            StepType.BiochemicalTest, mediaTypeId: null, phenotypicTestType: PhenotypicTestType.Antibiogram,
            tempMin: 35, tempMax: 37, incubationMinHours: 16, incubationMaxHours: 18);
        var errors = WorkflowTemplateValidator.Validate(step);
        Assert.Empty(errors);
    }

    [Theory]
    [InlineData(StepType.PlateCount)]
    [InlineData(StepType.BrothEnrichment)]
    [InlineData(StepType.SelectiveBroth)]
    [InlineData(StepType.SelectivePlating)]
    [InlineData(StepType.ConfirmatoryPlating)]
    public void Rule8_NonBiochemical_WithoutMediaTypeId_FailsRule8(StepType type)
    {
        var step = StepWithMediaAndPhenotype(type, mediaTypeId: null, phenotypicTestType: null,
            organismId: type is StepType.SelectivePlating or StepType.ConfirmatoryPlating ? 7 : null);
        var errors = WorkflowTemplateValidator.Validate(step);
        Assert.Contains(errors, e => e.RuleNumber == 8);
    }

    [Fact]
    public void Rule8_NonBiochemical_WithPhenotypicTestType_FailsRule8()
    {
        var step = StepWithMediaAndPhenotype(StepType.PlateCount, mediaTypeId: 3, phenotypicTestType: PhenotypicTestType.Gram);
        var errors = WorkflowTemplateValidator.Validate(step);
        Assert.Contains(errors, e => e.RuleNumber == 8);
    }

    [Fact]
    public void Rule8_NonBiochemical_WithMediaTypeIdAndNoPhenotypicTestType_IsValid()
    {
        var step = StepWithMediaAndPhenotype(StepType.PlateCount, mediaTypeId: 3, phenotypicTestType: null);
        var errors = WorkflowTemplateValidator.Validate(step);
        Assert.Empty(errors);
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test MicroLIMS/backend/MicroLIMS.sln --filter "FullyQualifiedName~WorkflowTemplateValidationTests"`
Expected: FAIL — `WorkflowTemplateValidator` doesn't reference `MediaTypeId`/`PhenotypicTestType` yet, so every new test either fails assertion or (more likely) fails to compile until Step 3 lands. If it's a compile failure, that's expected at this point too — proceed to Step 3.

- [ ] **Step 3: Implement the two rules**

In `WorkflowTemplateValidator.cs`, extend the `BiochemicalTest` case and add a new post-switch check:

```csharp
            case StepType.BiochemicalTest:
                if (media.Count != 0)
                    Fail(4, "A biochemical test step must have no assigned media.");
                if (step.TargetOrganismId is not null)
                    Fail(4, "A biochemical test step must not target an organism.");
                if (step.MediaTypeId is not null)
                    Fail(4, "A biochemical test step must not have a media type assigned.");
                if (step.PhenotypicTestType is null)
                    Fail(4, "A biochemical test step must specify a phenotypic test type.");
                break;
        }

        if (step.StepType != StepType.BiochemicalTest)
        {
            if (step.MediaTypeId is null)
                Fail(8, "A media type is required for this step type.");
            if (step.PhenotypicTestType is not null)
                Fail(8, "Only a biochemical test step may specify a phenotypic test type.");
        }
```

(This replaces the closing `break;` and `}` of the existing `switch` block — the new post-switch `if` goes immediately after the `switch` closes, before the existing `foreach (var medium in media.Where(...))` block.)

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test MicroLIMS/backend/MicroLIMS.sln --filter "FullyQualifiedName~WorkflowTemplateValidationTests"`
Expected: PASS, all tests including the pre-existing ones (the pre-existing `Step(...)` helper omits `mediaTypeId`, which defaults to `null` on the new nullable property — check whether any pre-existing test now spuriously fails Rule 8; if `PlateCountStep_IsNotSubjectToPathogenRules` at line 122-126 now fails Rule 8 because it constructs a bare `PlateCount` step with no `MediaTypeId`, update that one pre-existing test to set `MediaTypeId` via object-initializer so it keeps testing what it was testing — StepType.PlateCount not being subject to the pathogen-specific rules 1-4 — without now also tripping the new, StepType-agnostic Rule 8).

- [ ] **Step 5: Run the full backend test suite**

Run: `dotnet test MicroLIMS/backend/MicroLIMS.sln`
Expected: PASS. Confirm the total count is 314 (prior baseline) plus the new tests added in this task, with zero failures/skips introduced elsewhere.

- [ ] **Step 6: Commit**

```bash
git add MicroLIMS/backend/MicroLIMS.Application/Services/WorkflowTemplateValidator.cs MicroLIMS/backend/MicroLIMS.Tests/WorkflowTests/WorkflowTemplateValidationTests.cs
git commit -m "validation: enforce MediaTypeId/PhenotypicTestType mutual exclusivity by StepType"
```

---

## Task 4: Backend API surface (controller + seed data)

**Files:**
- Modify: `MicroLIMS/backend/MicroLIMS.API/Controllers/MasterDataController.cs`
- Modify: `MicroLIMS/backend/MicroLIMS.Persistence/Seed/DbSeeder.cs`

**Interfaces:**
- Consumes: `TestWorkflowStep.MediaTypeId`/`PhenotypicTestType` (Task 1), `WorkflowTemplateValidator` rules 4/8 (Task 3).
- Produces: `CreateTestWorkflowStepRequest`/`UpdateTestWorkflowStepRequest` records with `int? MediaTypeId` and `PhenotypicTestType? PhenotypicTestType`; `GET .../steps` response objects include a `phenotypicTestType` field (string or null) alongside the existing `mediaTypeId` (now nullable).

- [ ] **Step 1: Update the request records**

In `MasterDataController.cs`, change:

```csharp
public record CreateTestWorkflowStepRequest(string StepName, int MediaTypeId, int IncubationMinHours, int IncubationMaxHours, decimal TemperatureMin, decimal TemperatureMax, bool IsFinalStep, StepType StepType, int? TargetOrganismId, List<StepMediaRequest> StepMedia, bool RequiresIncubationTransfer, List<IncubationStageRequest>? IncubationStages, int? ConfirmatoryMediaCount);
public record UpdateTestWorkflowStepRequest(string StepName, int MediaTypeId, int IncubationMinHours, int IncubationMaxHours, decimal TemperatureMin, decimal TemperatureMax, bool IsFinalStep, StepType StepType, int? TargetOrganismId, List<StepMediaRequest> StepMedia, bool RequiresIncubationTransfer, List<IncubationStageRequest>? IncubationStages, int? ConfirmatoryMediaCount);
```

to:

```csharp
public record CreateTestWorkflowStepRequest(string StepName, int? MediaTypeId, int IncubationMinHours, int IncubationMaxHours, decimal TemperatureMin, decimal TemperatureMax, bool IsFinalStep, StepType StepType, int? TargetOrganismId, List<StepMediaRequest> StepMedia, bool RequiresIncubationTransfer, List<IncubationStageRequest>? IncubationStages, int? ConfirmatoryMediaCount, PhenotypicTestType? PhenotypicTestType);
public record UpdateTestWorkflowStepRequest(string StepName, int? MediaTypeId, int IncubationMinHours, int IncubationMaxHours, decimal TemperatureMin, decimal TemperatureMax, bool IsFinalStep, StepType StepType, int? TargetOrganismId, List<StepMediaRequest> StepMedia, bool RequiresIncubationTransfer, List<IncubationStageRequest>? IncubationStages, int? ConfirmatoryMediaCount, PhenotypicTestType? PhenotypicTestType);
```

- [ ] **Step 2: Wire the new field through Create/Update**

In `CreateTestWorkflowStep`, the entity initializer already includes `MediaTypeId = request.MediaTypeId` (line 996) — add `PhenotypicTestType = request.PhenotypicTestType` to the same initializer.

In `UpdateTestWorkflowStep`, right after `step.MediaTypeId = request.MediaTypeId;` (line 1035), add:
```csharp
        step.PhenotypicTestType = request.PhenotypicTestType;
```

- [ ] **Step 3: Add the field to the GET projection**

In `GetTestWorkflowSteps` (around line 928-948), add `s.PhenotypicTestType` to the projected anonymous object, e.g. immediately after `s.Id, s.StepOrder, s.StepName, s.MediaTypeId,`:
```csharp
                s.Id, s.StepOrder, s.StepName, s.MediaTypeId, s.PhenotypicTestType,
```

- [ ] **Step 4: Fix the seeded BiochemicalTest step**

In `DbSeeder.cs`, change the `biochemical` step initializer (lines 277-282) from:
```csharp
        var biochemical = new TestWorkflowStep
        {
            TestDefinitionId = test.Id, StepOrder = 5, StepName = "Biochemical Test", MediaTypeId = selectiveAgarId,
            IncubationMinHours = 0, IncubationMaxHours = 0, TemperatureMin = 35, TemperatureMax = 37,
            IsFinalStep = true, StepType = StepType.BiochemicalTest
        };
```
to:
```csharp
        var biochemical = new TestWorkflowStep
        {
            TestDefinitionId = test.Id, StepOrder = 5, StepName = "Biochemical Test", MediaTypeId = null,
            IncubationMinHours = 0, IncubationMaxHours = 0, TemperatureMin = 35, TemperatureMax = 37,
            IsFinalStep = true, StepType = StepType.BiochemicalTest, PhenotypicTestType = Domain.Enums.PhenotypicTestType.IdentificationKit
        };
```
(`IdentificationKit` is a reasonable generic default for this generically-named seed row; if `PhenotypicTestType` isn't already reachable via a bare namespace due to an `Enums.StepType` alias-style `using`, use the fully qualified `MicroLIMS.Domain.Enums.PhenotypicTestType` instead — check the file's existing `using` block for `MicroLIMS.Domain.Enums` before choosing which form compiles cleanly.)

- [ ] **Step 5: Build**

Run: `dotnet build MicroLIMS/backend/MicroLIMS.sln`
Expected: succeeds with no errors.

- [ ] **Step 6: Run the full backend test suite**

Run: `dotnet test MicroLIMS/backend/MicroLIMS.sln`
Expected: PASS, same count as Task 3 Step 5 (no new tests added in this task, but confirms the controller/seed changes didn't break anything).

- [ ] **Step 7: Commit**

```bash
git add MicroLIMS/backend/MicroLIMS.API/Controllers/MasterDataController.cs MicroLIMS/backend/MicroLIMS.Persistence/Seed/DbSeeder.cs
git commit -m "api: nullable MediaTypeId + PhenotypicTestType on workflow step create/update/get"
```

---

## Task 5: Frontend types + Add Step form

**Files:**
- Modify: `MicroLIMS/frontend/src/modules/testingWorkspace/types/testWorkflowTypes.ts`
- Modify: `MicroLIMS/frontend/src/services/masterDataOptions.ts`
- Modify: `MicroLIMS/frontend/src/modules/laboratoryConfiguration/masterDataSimple/TestMasterPage.tsx`

**Interfaces:**
- Consumes: backend `GET .../test-definitions/{id}/steps` now returns `mediaTypeId: number | null` and `phenotypicTestType: string | null` per step (Task 4).
- Produces: `masterDataOptions.createTestWorkflowStep`/`updateTestWorkflowStep` payload types accept `mediaTypeId: number | null` and `phenotypicTestType: string | null`.

- [ ] **Step 1: Fix the runtime-workspace DTO nullability**

In `testWorkflowTypes.ts`, change:
```typescript
  mediaTypeId: number;
```
to:
```typescript
  mediaTypeId: number | null;
```
(Matches the sibling type at `pathogenSessionTypes.ts:95`, which already declares `mediaTypeId: number | null` — this file was simply out of date. No other change to this file: adding `phenotypicTestType` display to the testing-workspace runtime dialog is out of scope per the brief.)

- [ ] **Step 2: Update `masterDataOptions.ts` payload types**

Change both `createTestWorkflowStep` and `updateTestWorkflowStep` payload type blocks from:
```typescript
    stepName: string; mediaTypeId: number; incubationMinHours: number; incubationMaxHours: number;
    temperatureMin: number; temperatureMax: number; isFinalStep: boolean; stepType: string;
    targetOrganismId: number | null;
```
to:
```typescript
    stepName: string; mediaTypeId: number | null; incubationMinHours: number; incubationMaxHours: number;
    temperatureMin: number; temperatureMax: number; isFinalStep: boolean; stepType: string;
    targetOrganismId: number | null; phenotypicTestType: string | null;
```
(Apply to both the `createTestWorkflowStep` and `updateTestWorkflowStep` payload object types, at lines 59-66 and 67-74 respectively.)

- [ ] **Step 3: Add the Phenotypic Test Type constant and extend form state**

In `TestMasterPage.tsx`, add near the other `STEP_TYPES_*` constants:
```typescript
const PHENOTYPIC_TEST_TYPES = ["Gram", "Catalase", "Oxidase", "Coagulase", "Antibiogram", "IdentificationKit"];
const PHENOTYPIC_TEST_TYPE_LABELS: Record<string, string> = {
  Gram: "Gram Stain", Catalase: "Catalase", Oxidase: "Oxidase", Coagulase: "Coagulase",
  Antibiogram: "Antibiogram", IdentificationKit: "Identification Kit"
};
```

Extend `StepFormState`:
```typescript
  phenotypicTestType?: string | null;
```

Extend `defaultStepForm`:
```typescript
const defaultStepForm = (): StepFormState => ({
  isFinalStep: false, stepType: "PlateCount", targetOrganismId: null, stepMedia: [], requiresIncubationTransfer: false,
  phenotypicTestType: null
});
```

- [ ] **Step 4: Extend `validateStepForm`**

Add, alongside the existing `isBiochemical` checks:
```typescript
  if (isBiochemical && !form.phenotypicTestType)
    return "A biochemical test step must specify a phenotypic test type.";
  if (!isBiochemical && form.phenotypicTestType)
    return "Only a biochemical test step may specify a phenotypic test type.";
```

- [ ] **Step 5: Wire `startEditStep`, `changeStepType`, and the save gate/payload**

In `startEditStep`, add `phenotypicTestType: s.phenotypicTestType ?? null,` to the `setForm({...})` call.

In `changeStepType`, clear both fields on every switch (media type only meaningfully needs clearing when moving to/from BiochemicalTest, but clearing unconditionally on every StepType change matches the existing behavior for `targetOrganismId`/`stepMedia`, which are already cleared unconditionally):
```typescript
  const changeStepType = (stepType: string) => setForm({
    ...form, stepType, targetOrganismId: null, stepMedia: [], mediaTypeId: "", phenotypicTestType: null,
    requiresIncubationTransfer: false, stage2TempMin: undefined, stage2TempMax: undefined,
    stage2IncubationMinHours: undefined, stage2IncubationMaxHours: undefined
  });
```

In `saveStep`, replace the hard-coded required-field gate:
```typescript
    if (!form.stepName || !form.mediaTypeId) {
      setError("Step Name and Media Type are required.");
      return;
    }
```
with:
```typescript
    const isBiochemical = form.stepType === "BiochemicalTest";
    if (!form.stepName || (!isBiochemical && !form.mediaTypeId) || (isBiochemical && !form.phenotypicTestType)) {
      setError(isBiochemical ? "Step Name and Phenotypic Test Type are required." : "Step Name and Media Type are required.");
      return;
    }
```

In the `payload` object inside `saveStep`, change:
```typescript
      stepName: form.stepName, mediaTypeId: Number(form.mediaTypeId),
```
to:
```typescript
      stepName: form.stepName, mediaTypeId: isBiochemical ? null : Number(form.mediaTypeId),
      phenotypicTestType: isBiochemical ? form.phenotypicTestType : null,
```

- [ ] **Step 6: Make the form fields reactive to StepType**

Wrap the Media Type `<Select>` (currently always rendered, `TestMasterPage.tsx:367-370`) so it only renders when not BiochemicalTest, and add the Phenotypic Test Type `<Select>` in the `isBiochemical` branch. Replace:
```tsx
        {/* Still required for every StepType, including the pathogen ones -
            TestWorkflowStep.MediaTypeId is non-nullable server-side, so this
            selector stays even though it's semantically vestigial for
            anything but PlateCount. Do not remove or conditionally hide it. */}
        <Select size="small" displayEmpty value={form.mediaTypeId ?? ""} onChange={(e) => setForm({ ...form, mediaTypeId: e.target.value as number })} sx={{ minWidth: 160 }}>
          <MenuItem value=""><em>Media Type</em></MenuItem>
          {mediaTypes.map((m) => <MenuItem key={m.id} value={m.id}>{mediaClassLabel(m.class)}</MenuItem>)}
        </Select>
```
with:
```tsx
        {isBiochemical ? (
          <Select size="small" displayEmpty value={form.phenotypicTestType ?? ""} onChange={(e) => setForm({ ...form, phenotypicTestType: e.target.value as string })} sx={{ minWidth: 180 }}>
            <MenuItem value=""><em>Phenotypic Test Type</em></MenuItem>
            {PHENOTYPIC_TEST_TYPES.map((t) => <MenuItem key={t} value={t}>{PHENOTYPIC_TEST_TYPE_LABELS[t]}</MenuItem>)}
          </Select>
        ) : (
          // MediaTypeId is non-nullable for every other StepType (media-lot
          // verification reads it at submission time - TestWorkflowEngine.cs:387).
          <Select size="small" displayEmpty value={form.mediaTypeId ?? ""} onChange={(e) => setForm({ ...form, mediaTypeId: e.target.value as number })} sx={{ minWidth: 160 }}>
            <MenuItem value=""><em>Media Type</em></MenuItem>
            {mediaTypes.map((m) => <MenuItem key={m.id} value={m.id}>{mediaClassLabel(m.class)}</MenuItem>)}
          </Select>
        )}
```

Note `isBiochemical` needs to be computed before this JSX (it's already computed as a local inside `saveStep` in Step 5 above, which is a different scope — add a component-level `const isBiochemical = form.stepType === "BiochemicalTest";` near the existing `needsOrganism`/`hasNoMedia`/`isSingleMedia`/`isConfirmatory` consts at lines 172-175, and reuse it both in the JSX here and in `saveStep`/`validateStepForm` call sites where convenient — `validateStepForm` already computes its own local `isBiochemical` internally, that one stays as-is since it's a standalone function taking `form` as a parameter, not a component-scope closure).

Now hide the Incubation/Temp fields unless not-BiochemicalTest or PhenotypicTestType is Antibiogram. Wrap the four existing `<TextField>`s (Min Hours, Max Hours, Temp Min, Temp Max, currently always rendered at lines 371-374):
```tsx
        {(!isBiochemical || form.phenotypicTestType === "Antibiogram") && (
          <>
            <TextField size="small" type="number" label="Min Hours" value={form.incubationMinHours ?? ""} onChange={(e) => setForm({ ...form, incubationMinHours: e.target.value })} sx={{ width: 100 }} />
            <TextField size="small" type="number" label="Max Hours" value={form.incubationMaxHours ?? ""} onChange={(e) => setForm({ ...form, incubationMaxHours: e.target.value })} sx={{ width: 100 }} />
            <TextField size="small" type="number" label="Temp Min" value={form.temperatureMin ?? ""} onChange={(e) => setForm({ ...form, temperatureMin: e.target.value })} sx={{ width: 90 }} />
            <TextField size="small" type="number" label="Temp Max" value={form.temperatureMax ?? ""} onChange={(e) => setForm({ ...form, temperatureMax: e.target.value })} sx={{ width: 90 }} />
          </>
        )}
```

- [ ] **Step 7: Update the Workflow Steps table**

The "Media" column (`TestMasterPage.tsx:335`) currently reads:
```tsx
                  <TableCell>{s.stepMedia?.length > 0 ? s.stepMedia.map((m: any) => m.materialName).join(", ") : <em>—</em>}</TableCell>
```
Change to show the phenotypic test type for BiochemicalTest rows instead of a dash:
```tsx
                  <TableCell>
                    {s.stepType === "BiochemicalTest"
                      ? (s.phenotypicTestType ? PHENOTYPIC_TEST_TYPE_LABELS[s.phenotypicTestType] ?? s.phenotypicTestType : <em>—</em>)
                      : (s.stepMedia?.length > 0 ? s.stepMedia.map((m: any) => m.materialName).join(", ") : <em>—</em>)}
                  </TableCell>
```

- [ ] **Step 8: Extend `stepNeedsConfiguration`**

Currently (lines 112-115):
```typescript
function stepNeedsConfiguration(s: any): boolean {
  if (STEP_TYPES_REQUIRING_ORGANISM.includes(s.stepType) && !s.targetOrganismId) return true;
  if (!["BiochemicalTest", "PlateCount"].includes(s.stepType) && (s.stepMedia?.length ?? 0) === 0) return true;
  return false;
}
```
Add a BiochemicalTest-specific check:
```typescript
function stepNeedsConfiguration(s: any): boolean {
  if (STEP_TYPES_REQUIRING_ORGANISM.includes(s.stepType) && !s.targetOrganismId) return true;
  if (!["BiochemicalTest", "PlateCount"].includes(s.stepType) && (s.stepMedia?.length ?? 0) === 0) return true;
  if (s.stepType === "BiochemicalTest" && !s.phenotypicTestType) return true;
  return false;
}
```

- [ ] **Step 9: Build**

Run: `cd MicroLIMS/frontend && npm run build`
Expected: succeeds with no TypeScript errors.

- [ ] **Step 10: Commit**

```bash
git add MicroLIMS/frontend/src/modules/testingWorkspace/types/testWorkflowTypes.ts MicroLIMS/frontend/src/services/masterDataOptions.ts MicroLIMS/frontend/src/modules/laboratoryConfiguration/masterDataSimple/TestMasterPage.tsx
git commit -m "frontend: BiochemicalTest steps use Phenotypic Test Type instead of Media Type"
```

---

## Task 6: Manual verification in the browser

**Files:** none (verification only).

- [ ] **Step 1: Start the backend and frontend dev servers**

Run backend: `dotnet run --project MicroLIMS/backend/MicroLIMS.API`
Run frontend: `cd MicroLIMS/frontend && npm run dev`

- [ ] **Step 2: Walk the Add Step form for BiochemicalTest**

Using claude-in-chrome (or the user driving manually): open Test Master, expand a test, select `BiochemicalTest` in the Step Type dropdown. Confirm:
- Media Type dropdown disappears, replaced by Phenotypic Test Type dropdown.
- Incubation Min/Max Hours and Temp Min/Max fields disappear.
- Selecting `Antibiogram` in Phenotypic Test Type brings the four Incubation/Temp fields back.
- Switching Phenotypic Test Type away from `Antibiogram` (or back to another StepType and back to BiochemicalTest) hides them again.
- Saving without a Phenotypic Test Type selected shows the validation error and does not submit.
- Saving a valid BiochemicalTest step succeeds, and the Workflow Steps table row shows the phenotypic test type label under the "Media" column instead of a dash.
- Switching Step Type back to e.g. `SelectivePlating` restores the Media Type dropdown and Incubation/Temp fields, and requires them again.

- [ ] **Step 3: Report results**

Note any deviation from the expected behavior above before considering this plan complete. No commit for this task (verification only).

---

## Self-Review Notes

- **Spec coverage:** Data model (Task 1-2), backend validation (Task 3), backend API surface + seed data (Task 4), frontend reactive form + table (Task 5), verification in lieu of automated frontend tests per the confirmed decision (Task 6), backend unit tests for both validation branches plus the Antibiogram case (Task 3). All original Step 1-5 asks are covered, adjusted per the corrections section above.
- **Placeholder scan:** no TBD/TODO left; every step has real code or a real command.
- **Type consistency:** `PhenotypicTestType` enum member names match exactly across the C# enum (Task 1), validator (Task 3), controller (Task 4), and the frontend string arrays (Task 5) — `Gram`, `Catalase`, `Oxidase`, `Coagulase`, `Antibiogram`, `IdentificationKit` throughout.
