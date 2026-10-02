# Retire Legacy SST Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the HPLC workspace run the only source of HPLC system suitability: Dissolution takes its standard from an assigned workspace run, then the old System Suitability (HPLC) module, the Standard Comparison workflow and the `Water soluble` test are deleted.

**Architecture:** Stage 1 adds a single helper (`HplcDissolutionStandard`) that resolves a dissolution order's standard from its assigned workspace run; `DissolutionRecorder`, a new read endpoint, and the Testing-page `DissolutionPanel` all use it. Stage 2 deletes the legacy code paths and runs one migration that deletes the legacy data and drops the tables.

**Tech Stack:** ASP.NET Core / EF Core (PostgreSQL, InMemory for unit tests), xUnit; React + TypeScript + MUI.

**Spec:** `docs/superpowers/specs/2026-10-02-retire-legacy-sst-design.md`

## Spec amendment (made while planning - confirm at plan review)

The spec put dissolution entry on the workspace sample entry page with equipment = the run's instrument. The existing `DissolutionPanel` (Testing page) records the **dissolution tester** as its equipment and also owns the Stage 2 / Stage 3 entry (`RecordDissolutionStageAsync`). This plan therefore keeps entry in `DissolutionPanel` and only replaces its "System suitability run" block with the assigned workspace run's standard. The workspace sample row for a dissolution order links to the Testing page instead of the replicate entry page. `TestDefinition.HplcMethodId` is **allowed** (not required) for Dissolution; a dissolution test without a method simply never appears in a run's eligible list and its panel says so.

## Global Constraints

- Branch: `feat/retire-legacy-sst` (already contains the SST blank-field fix and the spec).
- Dissolution standard concentration: `Cs = W × P/100 × (100 − MC)/100 ÷ standard dilution`.
- Dissolution standard comes only from the order's `HplcRunSample` with `Status == Assigned` on a run whose SST is `Passed`.
- Standard dilution is a method-analyte constant `HplcMethodAnalyte.StandardDilution` (decimal?, > 0); dissolution needs a single-analyte method with it set.
- Old SST data and `Water soluble` orders are deleted (decision made on local data; production is checked first - Task 12).
- `WorkflowType.StandardComparison` enum value is kept (stored int), commented as retired.
- Builds/tests while the API runs: `dotnet test backend/MicroLIMS.Tests --artifacts-path <temp dir>`; migrations: `dotnet ef migrations add <Name> --project backend/MicroLIMS.Persistence --startup-project backend/MicroLIMS.API --configuration Release`.
- Before applying any migration to LIMSV2: `pg_dump -Fc -h localhost -U postgres -d LIMSV2 -f E:/MicroLIMS/backups/LIMSV2_before_<name>_20261002.dump`.
- psql: `"/c/Program Files/PostgreSQL/18/bin/psql.exe" -h localhost -U postgres -d LIMSV2` (run from Bash with a heredoc; PowerShell strips quotes).
- User-facing text: no em dashes.
- Commits end with the session attribution lines (see conversation).

## Review Focus

1. A dissolution order assigned to a run that is later **abandoned** (run sample stays `Assigned` but run status `Abandoned`) must not supply a standard - expect "Assign this sample to an HPLC run with a passed system suitability." (Task 4 test `Standard_AbandonedRun_IsMissing`.)
2. A dissolution order assigned, then **removed** from the run (`Status == Removed`) must not supply a standard. (Task 4 test `Standard_RemovedSample_IsMissing`.)
3. A **multi-analyte** method on a dissolution test: assignment must refuse with a clear message, not crash on `.Single()`. (Task 5 test `Assign_DissolutionMultiAnalyteMethod_Throws`.)
4. **Stage 2/3 entry after the run's standard changed** - Stage 2/3 reuse the standard stored in Stage 1's `CalculationJson`; the recorder must not re-resolve it. (Task 4 keeps the existing Stage 2 test green unmodified apart from seeding.)
5. Migration on a database where a **non-`Water soluble` Standard Comparison test has orders** must abort with the test codes, deleting nothing. (Task 10 step "guard check" runs it against a scratch DB.)

---

# Stage 1 - Dissolution in the workspace

### Task 1: `StandardDilution` on the HPLC method analyte

**Files:**
- Modify: `backend/MicroLIMS.Domain/Entities/HplcMethod.cs` (class `HplcMethodAnalyte`)
- Modify: `backend/MicroLIMS.Persistence/Configurations/HplcMethodConfiguration.cs` (`HplcMethodAnalyteConfiguration`)
- Modify: `backend/MicroLIMS.Application/Services/HplcMethodService.cs` (record `HplcAnalyteInput` line ~14; update loop line ~347; validation line ~497)
- Modify: `backend/MicroLIMS.Application/DTOs/Responses/HplcMethodResponse.cs` (record `HplcMethodAnalyteResponse` line ~12; mapping line ~96)
- Create: migration `AddHplcMethodAnalyteStandardDilution` (generated)
- Test: `backend/MicroLIMS.Tests/UnitTests/HplcMethodServiceTests.cs`

**Interfaces:**
- Produces: `HplcMethodAnalyte.StandardDilution : decimal?`; `HplcAnalyteInput(..., decimal? StandardDilution = null)` (last parameter); `HplcMethodAnalyteResponse(..., decimal? StandardDilution = null)` (last parameter, so old run snapshots deserialize with null).

- [ ] **Step 1: Write the failing tests** (append to `HplcMethodServiceTests`, next to `Create_ThWtZero_Throws`)

```csharp
    [Fact]
    public async Task Create_StandardDilutionZero_Throws()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var (diluent, mp, standard) = await SeedBasicsAsync(db, section.Id);
        var service = TestServiceFactory.HplcMethod(db);

        var analyte = new HplcAnalyteInput(null, "A1", 254m, standard.Id, 50m, 50m, 5, StandardDilution: 0m);
        var req = Req(diluent.Id, new List<HplcMobilePhaseInput> { new("A", mp.Id, null) }, new List<HplcAnalyteInput> { analyte });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(req, userId));
        Assert.Contains("Standard dilution", ex.Message);
    }

    [Fact]
    public async Task Create_StandardDilution_RoundTrips()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var (diluent, mp, standard) = await SeedBasicsAsync(db, section.Id);
        var service = TestServiceFactory.HplcMethod(db);

        var analyte = new HplcAnalyteInput(null, "A1", 254m, standard.Id, 50m, 50m, 5, StandardDilution: 2500m);
        var created = await service.CreateAsync(
            Req(diluent.Id, new List<HplcMobilePhaseInput> { new("A", mp.Id, null) }, new List<HplcAnalyteInput> { analyte }), userId);

        Assert.Equal(2500m, created.Analytes[0].StandardDilution);
    }
```

- [ ] **Step 2: Run to verify they fail**

Run: `dotnet test backend/MicroLIMS.Tests --artifacts-path "$TEMP/rls-art" --filter "FullyQualifiedName~HplcMethodServiceTests.Create_StandardDilution"`
Expected: build error - `HplcAnalyteInput` has no parameter `StandardDilution`.

- [ ] **Step 3: Implement**

Entity (`HplcMethodAnalyte`, after `SstMinPeakToValley`):
```csharp
    public decimal? StandardDilution { get; set; } // mL; dissolution standard only (Cs = W x P x (100-MC) / dilution)
```
Configuration (`HplcMethodAnalyteConfiguration`, after `SstMinPeakToValley`):
```csharp
        builder.Property(e => e.StandardDilution).HasColumnType("decimal(12,4)");
```
`HplcAnalyteInput` - append the last parameter:
```csharp
    decimal? SstMinSignalToNoise = null, decimal? SstMinPeakToValley = null, decimal? StandardDilution = null);
```
Update loop (after `analyte.SstMinPeakToValley = input.SstMinPeakToValley;`):
```csharp
            analyte.StandardDilution = input.StandardDilution;
```
Validation (after the theoretical-weight check):
```csharp
            if (a.StandardDilution.HasValue && a.StandardDilution.Value <= 0m)
                throw new InvalidOperationException("Standard dilution must be greater than zero.");
```
`HplcMethodAnalyteResponse` - append:
```csharp
    decimal? SstMinPeakToValley, decimal? StandardDilution = null);
```
Mapping in `HplcMethodResponse.From` - pass it last:
```csharp
                a.SstMinPeakToValley, a.StandardDilution))
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test backend/MicroLIMS.Tests --artifacts-path "$TEMP/rls-art" --filter "FullyQualifiedName~HplcMethod"`
Expected: all pass.

- [ ] **Step 5: Generate and inspect the migration**

Run: `dotnet ef migrations add AddHplcMethodAnalyteStandardDilution --project backend/MicroLIMS.Persistence --startup-project backend/MicroLIMS.API --configuration Release`
Expected: `Up` contains only `AddColumn<decimal>("StandardDilution", "HplcMethodAnalytes", type: "numeric(12,4)", nullable: true)`; `Down` drops it. Delete and regenerate if anything else appears.

- [ ] **Step 6: Commit**

```bash
git add backend/MicroLIMS.Domain/Entities/HplcMethod.cs backend/MicroLIMS.Persistence backend/MicroLIMS.Application/Services/HplcMethodService.cs backend/MicroLIMS.Application/DTOs/Responses/HplcMethodResponse.cs backend/MicroLIMS.Tests/UnitTests/HplcMethodServiceTests.cs
git commit -m "Add standard dilution to HPLC method analytes"
```

---

### Task 2: Allow an HPLC method on Dissolution tests

**Files:**
- Modify: `backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs` (create: lines ~332-350 and ~414; update: lines ~759-781 and ~870)
- Test: `backend/MicroLIMS.Tests/UnitTests/HplcMethodAssayTestMasterTests.cs`

**Interfaces:**
- Produces: a Dissolution `TestDefinition` may carry `HplcMethodId` (same section, active on change). Optional.

- [ ] **Step 1: Write the failing tests** (in `HplcMethodAssayTestMasterTests`, after `AssayReq`)

```csharp
    private static CreateTestDefinitionRequest DissolutionReq(int sectionId, int? hplcMethodId, string code = "DISS-T1") =>
        new(
            Code: code,
            DisplayName: "Dissolution Test",
            SectionId: sectionId,
            WorkflowType: WorkflowType.Dissolution,
            EquationType: EquationType.Dissolution,
            RequiresSystemSuitability: true,
            MethodAbbreviation: code,
            HplcMethodId: hplcMethodId);

    [Fact]
    public async Task TestDef_Dissolution_WithMethod_SavesMethod()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var method = await AddMethodAsync(db, section.Id, userId);
        var service = new TestDefinitionMasterDataService(db, new UserSectionScopeService(db));

        var created = await service.CreateTestDefinitionAsync(userId, DissolutionReq(section.Id, method.Id));

        Assert.Equal(method.Id, (await db.TestDefinitions.FirstAsync(t => t.Id == created.Id)).HplcMethodId);
    }

    [Fact]
    public async Task TestDef_Dissolution_MethodFromOtherSection_Throws()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var otherSection = await AddOtherSectionAsync(db, section.DepartmentId);
        var method = await AddMethodAsync(db, otherSection.Id, userId);
        var service = new TestDefinitionMasterDataService(db, new UserSectionScopeService(db));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateTestDefinitionAsync(userId, DissolutionReq(section.Id, method.Id)));
        Assert.Contains("another laboratory", ex.Message);
    }
```

If `CreateTestDefinitionAsync` returns a type without `Id`, read the id the same way the existing `TestDef_HplcMethodAssay_*` success test in this file does.

- [ ] **Step 2: Run to verify they fail**

Run: `dotnet test backend/MicroLIMS.Tests --artifacts-path "$TEMP/rls-art" --filter "FullyQualifiedName~HplcMethodAssayTestMasterTests.TestDef_Dissolution"`
Expected: FAIL - "HPLC method is only allowed for HPLC method assay tests."

- [ ] **Step 3: Implement**

Create path - replace the `else if (request.HplcMethodId.HasValue)` throw block with:
```csharp
        else if (request.WorkflowType == WorkflowType.Dissolution && request.HplcMethodId.HasValue)
        {
            // Optional for dissolution: the workspace run of this method supplies the standard.
            var hplcMethod = await _db.HplcMethods.FirstOrDefaultAsync(m => m.Id == request.HplcMethodId.Value)
                ?? throw new InvalidOperationException("HPLC method not found.");
            if (hplcMethod.SectionId != sectionId)
                throw new InvalidOperationException("The HPLC method belongs to another laboratory.");
            if (!hplcMethod.IsActive)
                throw new InvalidOperationException("The HPLC method is inactive.");
        }
        else if (request.HplcMethodId.HasValue)
        {
            throw new InvalidOperationException("HPLC method is only allowed for HPLC method assay and dissolution tests.");
        }
```
Create entity initializer (line ~414):
```csharp
            HplcMethodId = request.WorkflowType is WorkflowType.HplcMethodAssay or WorkflowType.Dissolution ? request.HplcMethodId : null
```
Update path - replace `else if (effectiveHplcMethodId.HasValue)` throw block with:
```csharp
        else if (effectiveWorkflowType == WorkflowType.Dissolution && effectiveHplcMethodId.HasValue)
        {
            var hplcMethod = await _db.HplcMethods.FirstOrDefaultAsync(m => m.Id == effectiveHplcMethodId.Value)
                ?? throw new InvalidOperationException("HPLC method not found.");
            if (hplcMethod.SectionId != entity.SectionId)
                throw new InvalidOperationException("The HPLC method belongs to another laboratory.");
            if (!hplcMethod.IsActive && effectiveHplcMethodId != entity.HplcMethodId)
                throw new InvalidOperationException("The HPLC method is inactive.");
        }
        else if (effectiveHplcMethodId.HasValue)
        {
            throw new InvalidOperationException("HPLC method is only allowed for HPLC method assay and dissolution tests.");
        }
```
Also change the Dissolution `RequiresSystemSuitability` message (line ~205) to: `"Dissolution tests must require system suitability: the standard comes from the HPLC run the sample is assigned to."`

- [ ] **Step 4: Run tests**

Run: `dotnet test backend/MicroLIMS.Tests --artifacts-path "$TEMP/rls-art" --filter "FullyQualifiedName~HplcMethodAssayTestMasterTests|FullyQualifiedName~DissolutionMasterDataValidationTests"`
Expected: all pass (update any assertion that matched the old "only allowed" or SST message text).

- [ ] **Step 5: Commit**

```bash
git add backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs backend/MicroLIMS.Tests/UnitTests
git commit -m "Allow an HPLC method on dissolution tests"
```

---

### Task 3: Moisture-corrected Cs and run-sourced standard data

**Files:**
- Modify: `backend/MicroLIMS.Application/Helpers/DissolutionCalculator.cs` (`CalculateCs`)
- Modify: `backend/MicroLIMS.Domain/Entities/DissolutionCalculationData.cs` (`DissolutionStandardData`)
- Test: `backend/MicroLIMS.Tests/UnitTests/DissolutionCalculatorTests.cs`

**Interfaces:**
- Produces: `DissolutionCalculator.CalculateCs(decimal standardWeightMg, decimal standardPurityPercent, decimal standardMoisturePercent, decimal standardDilution)`.
- Produces: `record DissolutionStandardData(int HplcRunId, string RunCode, string SstCode, decimal StandardWeightMg, decimal StandardDilution, decimal StandardPurityPercent, decimal StandardMoisturePercent, decimal StandardMeanArea, decimal Cs)`.

- [ ] **Step 1: Write the failing test** (append to `DissolutionCalculatorTests`)

```csharp
    [Fact]
    public void CalculateCs_AppliesMoisture()
    {
        // 50 mg x 99.5 % x (100 - 2.0) % / 2500 mL = 0.019502 mg/mL
        var cs = DissolutionCalculator.CalculateCs(50m, 99.5m, 2.0m, 2500m);
        Assert.Equal(0.019502m, Math.Round(cs, 6));
    }

    [Fact]
    public void CalculateCs_MoistureOutOfRange_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => DissolutionCalculator.CalculateCs(50m, 99.5m, 100m, 2500m));
        Assert.Throws<InvalidOperationException>(() => DissolutionCalculator.CalculateCs(50m, 99.5m, -1m, 2500m));
    }
```
Update every existing `CalculateCs(w, p, d)` call in this test file to `CalculateCs(w, p, 0m, d)` (moisture 0 keeps their expected values).

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test backend/MicroLIMS.Tests --artifacts-path "$TEMP/rls-art" --filter "FullyQualifiedName~DissolutionCalculatorTests"`
Expected: build error - no 4-argument `CalculateCs`.

- [ ] **Step 3: Implement**

```csharp
    public static decimal CalculateCs(decimal standardWeightMg, decimal standardPurityPercent, decimal standardMoisturePercent, decimal standardDilution)
    {
        if (standardWeightMg <= 0m)
            throw new InvalidOperationException("Standard weight must be greater than zero.");
        if (standardPurityPercent <= 0m)
            throw new InvalidOperationException("Standard purity must be greater than zero.");
        if (standardMoisturePercent < 0m || standardMoisturePercent >= 100m)
            throw new InvalidOperationException("Standard moisture content must be from 0 to below 100 %.");
        if (standardDilution <= 0m)
            throw new InvalidOperationException("Standard dilution must be greater than zero.");

        return standardWeightMg * (standardPurityPercent / 100m) * ((100m - standardMoisturePercent) / 100m) / standardDilution;
    }
```
`DissolutionStandardData`:
```csharp
public record DissolutionStandardData(
    int HplcRunId,
    string RunCode,
    string SstCode,
    decimal StandardWeightMg,
    decimal StandardDilution,
    decimal StandardPurityPercent,
    decimal StandardMoisturePercent,
    decimal StandardMeanArea,
    decimal Cs);
```
The recorder will not compile until Task 4 - that is expected; do not commit yet.

- [ ] **Step 4: Continue directly to Task 4** (commit together at the end of Task 4).

---

### Task 4: Resolve the dissolution standard from the assigned run

**Files:**
- Create: `backend/MicroLIMS.Application/Services/HplcDissolutionStandard.cs`
- Modify: `backend/MicroLIMS.Application/Workflows/TestWorkflow/DissolutionRecorder.cs` (lines ~40-58 and ~108-116)
- Modify: `backend/MicroLIMS.Tests/UnitTests/DissolutionWorkflowEngineTests.cs` (seeding; delete `Relink_AfterStage1_IsBlocked`)
- Modify: `backend/MicroLIMS.Tests/IntegrationTests/DissolutionResultPostgresIntegrationTests.cs` (seeding, same pattern)
- Create: `backend/MicroLIMS.Tests/UnitTests/HplcDissolutionStandardTests.cs`

**Interfaces:**
- Consumes: Task 1 `StandardDilution` on `HplcMethodAnalyteResponse`; Task 3 `CalculateCs`, `DissolutionStandardData`.
- Produces:
```csharp
public sealed record HplcDissolutionStandardValues(
    int HplcRunId, int RunSampleId, int EquipmentId, string RunCode, string SstCode,
    decimal MeanResponse, decimal StandardWeightMg, decimal PurityPercent, decimal MoisturePercent,
    decimal StandardDilution, decimal Cs);
public sealed record HplcDissolutionStandardResult(HplcDissolutionStandardValues? Standard, string? Problem);
public static class HplcDissolutionStandard
{
    public const string NotAssigned = "Assign this sample to an HPLC run with a passed system suitability.";
    public static Task<HplcDissolutionStandardResult> ResolveAsync(IMicroLimsDbContext db, int testOrderId, CancellationToken ct = default);
}
```

- [ ] **Step 1: Write the failing tests** - `HplcDissolutionStandardTests.cs`

```csharp
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs.Responses;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class HplcDissolutionStandardTests
{
    private static MicroLimsDbContext NewDb() =>
        new(new DbContextOptionsBuilder<MicroLimsDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options) { CurrentUserId = 1 };

    // Seeds a run (method snapshot with one analyte), its SST and one assigned
    // run sample for test order 100. Returns the run so tests can alter it.
    internal static HplcRun SeedRun(MicroLimsDbContext db, int testOrderId = 100,
        HplcSstStatus sst = HplcSstStatus.Passed, HplcRunStatus runStatus = HplcRunStatus.Open,
        decimal? dilution = 2500m, int analyteCount = 1)
    {
        var analytes = Enumerable.Range(1, analyteCount).Select(i => new HplcMethodAnalyteResponse(
            i, i, $"Analyte {i}", 290m, 1, "STD", 50m, 50m, 5,
            null, null, null, null, null, null, null, dilution)).ToList();
        var run = new HplcRun
        {
            SectionId = 1, Code = "SILD RUN 01/102026", EquipmentId = 7, ChromatographyColumnId = 1, HplcMethodId = 1,
            MethodSnapshotJson = JsonSerializer.Serialize(new HplcMethodResponse { Id = 1, Name = "M", Abbreviation = "SILD", ColumnDesignation = "L1", Analytes = analytes }, SnapshotJson.Options),
            AnalystUserId = 1, StartedAt = DateTime.UtcNow, Status = runStatus,
            Sst = new HplcSstRecord
            {
                Code = "SILD S.S 01/102026", Status = sst,
                Analytes = analytes.Select(a => new HplcSstAnalyte
                {
                    HplcMethodAnalyteId = a.Id, AnalyteName = a.Name,
                    StandardWeightMg = 50m, StandardPurityPercent = 100m, StandardMoisturePercent = 0m, MeanResponse = 0.500m
                }).ToList()
            },
            Samples = { new HplcRunSample { TestOrderId = testOrderId, Status = HplcRunSampleStatus.Assigned, AssignedAt = DateTime.UtcNow, AssignedByUserId = 1 } }
        };
        db.HplcRuns.Add(run);
        db.SaveChanges();
        return run;
    }

    [Fact]
    public async Task Standard_FromAssignedPassedRun()
    {
        await using var db = NewDb();
        var run = SeedRun(db);

        var result = await HplcDissolutionStandard.ResolveAsync(db, 100);

        Assert.Null(result.Problem);
        Assert.Equal(run.Id, result.Standard!.HplcRunId);
        Assert.Equal(7, result.Standard.EquipmentId);
        Assert.Equal(0.02m, result.Standard.Cs); // 50 x 1.0 x 1.0 / 2500
    }

    [Fact]
    public async Task Standard_NotAssigned_IsMissing()
    {
        await using var db = NewDb();
        Assert.Equal(HplcDissolutionStandard.NotAssigned, (await HplcDissolutionStandard.ResolveAsync(db, 100)).Problem);
    }

    [Fact]
    public async Task Standard_RemovedSample_IsMissing()
    {
        await using var db = NewDb();
        var run = SeedRun(db);
        run.Samples[0].Status = HplcRunSampleStatus.Removed;
        db.SaveChanges();
        Assert.Equal(HplcDissolutionStandard.NotAssigned, (await HplcDissolutionStandard.ResolveAsync(db, 100)).Problem);
    }

    [Fact]
    public async Task Standard_AbandonedRun_IsMissing()
    {
        await using var db = NewDb();
        SeedRun(db, runStatus: HplcRunStatus.Abandoned);
        Assert.Equal(HplcDissolutionStandard.NotAssigned, (await HplcDissolutionStandard.ResolveAsync(db, 100)).Problem);
    }

    [Fact]
    public async Task Standard_SstNotPassed_IsMissing()
    {
        await using var db = NewDb();
        SeedRun(db, sst: HplcSstStatus.Pending);
        Assert.Contains("has not passed", (await HplcDissolutionStandard.ResolveAsync(db, 100)).Problem);
    }

    [Fact]
    public async Task Standard_NoDilution_IsMissing()
    {
        await using var db = NewDb();
        SeedRun(db, dilution: null);
        Assert.Contains("standard dilution", (await HplcDissolutionStandard.ResolveAsync(db, 100)).Problem);
    }
}
```

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test backend/MicroLIMS.Tests --artifacts-path "$TEMP/rls-art" --filter "FullyQualifiedName~HplcDissolutionStandardTests"`
Expected: build error - `HplcDissolutionStandard` does not exist.

- [ ] **Step 3: Implement `HplcDissolutionStandard.cs`**

```csharp
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.DTOs.Responses;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Services;

public sealed record HplcDissolutionStandardValues(
    int HplcRunId, int RunSampleId, int EquipmentId, string RunCode, string SstCode,
    decimal MeanResponse, decimal StandardWeightMg, decimal PurityPercent, decimal MoisturePercent,
    decimal StandardDilution, decimal Cs);

public sealed record HplcDissolutionStandardResult(HplcDissolutionStandardValues? Standard, string? Problem);

// The dissolution standard for one test order, taken from the HPLC workspace
// run it is assigned to (one source for the recorder, the read endpoint and
// the approval gate). Dissolution methods have exactly one analyte.
public static class HplcDissolutionStandard
{
    public const string NotAssigned = "Assign this sample to an HPLC run with a passed system suitability.";

    public static async Task<HplcDissolutionStandardResult> ResolveAsync(IMicroLimsDbContext db, int testOrderId, CancellationToken ct = default)
    {
        var runSample = await db.HplcRunSamples
            .Include(s => s.HplcRun!).ThenInclude(r => r.Sst!).ThenInclude(s => s.Analytes)
            .Where(s => s.TestOrderId == testOrderId && s.Status == HplcRunSampleStatus.Assigned
                && s.HplcRun!.Status != HplcRunStatus.Abandoned)
            .OrderByDescending(s => s.Id)
            .FirstOrDefaultAsync(ct);
        if (runSample?.HplcRun?.Sst == null)
            return new(null, NotAssigned);

        var run = runSample.HplcRun;
        if (run.Sst!.Status != HplcSstStatus.Passed)
            return new(null, $"System suitability {run.Sst.Code} has not passed.");

        var snapshot = JsonSerializer.Deserialize<HplcMethodResponse>(run.MethodSnapshotJson, SnapshotJson.Options);
        if (snapshot == null || snapshot.Analytes.Count != 1)
            return new(null, "Dissolution needs an HPLC method with exactly one analyte.");
        var analyte = snapshot.Analytes[0];
        if (analyte.StandardDilution is not > 0m)
            return new(null, $"Method {snapshot.Abbreviation} has no standard dilution for {analyte.Name}.");

        var sst = run.Sst.Analytes.FirstOrDefault(a => a.HplcMethodAnalyteId == analyte.Id);
        if (sst?.MeanResponse is not > 0m || sst.StandardWeightMg is not > 0m
            || sst.StandardPurityPercent is not > 0m || sst.StandardMoisturePercent is null)
            return new(null, $"{analyte.Name}: the system suitability standard values are incomplete.");

        var cs = DissolutionCalculator.CalculateCs(sst.StandardWeightMg.Value, sst.StandardPurityPercent.Value,
            sst.StandardMoisturePercent.Value, analyte.StandardDilution.Value);
        return new(new HplcDissolutionStandardValues(
            run.Id, runSample.Id, run.EquipmentId, run.Code, run.Sst.Code,
            sst.MeanResponse.Value, sst.StandardWeightMg.Value, sst.StandardPurityPercent.Value,
            sst.StandardMoisturePercent.Value, analyte.StandardDilution.Value, cs), null);
    }
}
```
(`SnapshotJson` lives in `MicroLIMS.Application.Helpers` - confirm with `git grep -n "class SnapshotJson"` and fix the using if not.)

- [ ] **Step 4: Run the helper tests**

Run: `dotnet test backend/MicroLIMS.Tests --artifacts-path "$TEMP/rls-art" --filter "FullyQualifiedName~HplcDissolutionStandardTests"`
Expected: build still fails in `DissolutionRecorder` (Task 3 signature changes). Continue to Step 5.

- [ ] **Step 5: Switch `DissolutionRecorder` to the helper**

Replace the block from `if (!order.SystemSuitabilityRunId.HasValue)` through the `StandardMeanArea <= 0 ...` check with:
```csharp
        var resolved = await HplcDissolutionStandard.ResolveAsync(_db, order.Id);
        var std = resolved.Standard ?? throw new InvalidOperationException(resolved.Problem);
```
Replace the `cs` / `standardData` construction with:
```csharp
        decimal cs = std.Cs;

        var standardData = new DissolutionStandardData(
            HplcRunId: std.HplcRunId,
            RunCode: std.RunCode,
            SstCode: std.SstCode,
            StandardWeightMg: std.StandardWeightMg,
            StandardDilution: std.StandardDilution,
            StandardPurityPercent: std.PurityPercent,
            StandardMoisturePercent: std.MoisturePercent,
            StandardMeanArea: std.MeanResponse,
            Cs: cs);
```
Leave the Stage 2/3 path (`RecordDissolutionStageAsync`) as is: it reads the standard from the Stage 1 calculation JSON. Check with `git grep -n "SystemSuitabilityRun" backend/MicroLIMS.Application/Workflows/TestWorkflow/DissolutionRecorder.cs` - expect no hits. If the stage path deserializes `DissolutionStandardData` and fails on old JSON, that is acceptable: old dissolution data is deleted in Task 10.

- [ ] **Step 6: Re-seed the dissolution engine tests**

In `DissolutionWorkflowEngineTests.SetupDissolutionScenario`: remove the `ChromatographyColumn`, `Material` standard and `SystemSuitabilityRun` objects and `SystemSuitabilityRunId = sstRun.Id` on the order; change the tuple to drop `SystemSuitabilityRun run`; after `db.SaveChanges()` for the order add:
```csharp
        // Standard from an HPLC workspace run: Cs = 50 x 1.0 x 1.0 / 2500 = 0.02 mg/mL, mean area 0.500.
        HplcDissolutionStandardTests.SeedRun(db, order.Id);
```
Update the destructuring in `Stage1_NextStageRequired_...` to the new tuple. Delete `Relink_AfterStage1_IsBlocked` (the legacy link endpoint is removed in Stage 2). Apply the same change to `DissolutionResultPostgresIntegrationTests` (keep its expected percentages; Cs stays 0.02).

- [ ] **Step 7: Run the dissolution tests**

Run: `dotnet test backend/MicroLIMS.Tests --artifacts-path "$TEMP/rls-art" --filter "FullyQualifiedName~Dissolution"`
Expected: all pass (Postgres tests skip or pass depending on the env; run the Postgres suite in Task 8).

- [ ] **Step 8: Commit (Tasks 3 + 4)**

```bash
git add backend/MicroLIMS.Application backend/MicroLIMS.Domain/Entities/DissolutionCalculationData.cs backend/MicroLIMS.Tests
git commit -m "Take the dissolution standard from the assigned HPLC run"
```

---

### Task 5: Workspace assignment, read endpoint and approval gate

**Files:**
- Modify: `backend/MicroLIMS.Application/Services/HplcRunService.Samples.cs` (`EligibleOrdersQuery`, `AssignSamplesAsync`, `BuildSampleSummariesAsync`)
- Modify: `backend/MicroLIMS.Application/Services/HplcRunService.cs` (record `HplcRunSampleSummaryDto` line ~56; new method `GetDissolutionStandardAsync`)
- Modify: `backend/MicroLIMS.API/Controllers/HplcWorkspaceController.cs`
- Modify: `backend/MicroLIMS.Application/Services/SampleApprovalService.cs` (lines ~186-205)
- Test: `backend/MicroLIMS.Tests/UnitTests/HplcRunServiceSampleTests.cs`

**Interfaces:**
- Consumes: Task 4 `HplcDissolutionStandard.ResolveAsync`, `HplcDissolutionStandardValues`.
- Produces: `HplcRunSampleSummaryDto(..., bool Submitted, bool IsDissolution)`; `HplcRunService.GetDissolutionStandardAsync(int testOrderId, int userId, CancellationToken ct = default) : Task<HplcDissolutionStandardResult>`; `GET api/hplc-workspace/test-orders/{testOrderId}/dissolution-standard`.

- [ ] **Step 1: Write the failing tests** (in `HplcRunServiceSampleTests`; reuse its existing scenario helpers that start a run and pass SST - read the top of the file first and mirror the existing `Assign_*` test setup)

```csharp
    [Fact]
    public async Task Eligible_IncludesDissolutionOrderOfSameMethod()
    {
        // Arrange: the file's standard scenario with a passed SST, plus a Dissolution
        // TestDefinition { Code = "DISS-1", WorkflowType = Dissolution, HplcMethodId = <run method id>,
        // SectionId = <run section> } and one TestOrder for it (CurrentStep = Running).
        // Set StandardDilution = 2500 on the method's single analyte BEFORE StartRunAsync
        // so the snapshot carries it.
        // Act
        var eligible = await service.GetEligibleTestsAsync(run.Id, null, s.UserId);
        // Assert
        Assert.Contains(eligible, e => e.TestCode == "DISS-1");
    }

    [Fact]
    public async Task Assign_DissolutionWithoutStandardDilution_Throws()
    {
        // Same arrangement, but StandardDilution left null.
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.AssignSamplesAsync(run.Id, new List<int> { dissOrder.Id }, s.UserId));
        Assert.Contains("standard dilution", ex.Message);
    }

    [Fact]
    public async Task Assign_DissolutionMultiAnalyteMethod_Throws()
    {
        // Same arrangement with a two-analyte method (both with StandardDilution).
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.AssignSamplesAsync(run.Id, new List<int> { dissOrder.Id }, s.UserId));
        Assert.Contains("exactly one analyte", ex.Message);
    }

    [Fact]
    public async Task Summary_FlagsDissolutionSample()
    {
        // Same arrangement with StandardDilution = 2500; assign the dissolution order.
        var updated = await service.AssignSamplesAsync(run.Id, new List<int> { dissOrder.Id }, s.UserId);
        Assert.True(updated.Samples.Single(x => x.TestOrderId == dissOrder.Id).IsDissolution);
    }
```
The arrangement comments describe the exact rows; write them using the file's existing seeding helpers (method creation goes through `HplcAnalyteInput(..., StandardDilution: 2500m)` from Task 1). Every test must compile and assert exactly what is shown.

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test backend/MicroLIMS.Tests --artifacts-path "$TEMP/rls-art" --filter "FullyQualifiedName~HplcRunServiceSampleTests"`
Expected: new tests fail (dissolution not eligible / `IsDissolution` missing).

- [ ] **Step 3: Implement**

`EligibleOrdersQuery`:
```csharp
        var codes = _db.TestDefinitions
            .Where(t => (t.WorkflowType == WorkflowType.HplcMethodAssay || t.WorkflowType == WorkflowType.Dissolution)
                && t.HplcMethodId == run.HplcMethodId)
            .Select(t => t.Code);
```
`AssignSamplesAsync`, after the eligibility loop and before adding samples:
```csharp
        var dissolutionCodes = await _db.TestDefinitions
            .Where(t => t.WorkflowType == WorkflowType.Dissolution && t.HplcMethodId == run.HplcMethodId)
            .Select(t => t.Code).ToListAsync(ct);
        if (await _db.TestOrders.AnyAsync(o => ids.Contains(o.Id) && dissolutionCodes.Contains(o.TestCode), ct))
        {
            var snapshot = JsonSerializer.Deserialize<HplcMethodResponse>(run.MethodSnapshotJson, JsonOptions)!;
            if (snapshot.Analytes.Count != 1)
                throw new InvalidOperationException("Dissolution needs an HPLC method with exactly one analyte.");
            if (snapshot.Analytes[0].StandardDilution is not > 0m)
                throw new InvalidOperationException($"Method {snapshot.Abbreviation} needs a standard dilution before dissolution samples can be assigned.");
        }
```
(add `using MicroLIMS.Application.DTOs.Responses;` if missing).

`HplcRunSampleSummaryDto` - append `bool IsDissolution`. In `BuildSampleSummariesAsync`, before the `return`:
```csharp
        var codes = orders.Values.Select(o => o.TestCode).Distinct().ToList();
        var dissolutionCodes = (await _db.TestDefinitions.Where(t => codes.Contains(t.Code) && t.WorkflowType == WorkflowType.Dissolution)
            .Select(t => t.Code).ToListAsync(ct)).ToHashSet();
```
and pass `o != null && dissolutionCodes.Contains(o.TestCode)` as the last constructor argument.

`HplcRunService` (next to `GetTestOrderEvidenceAsync`):
```csharp
    public async Task<HplcDissolutionStandardResult> GetDissolutionStandardAsync(int testOrderId, int userId, CancellationToken ct = default)
    {
        await _scope.EnsureTestOrderAccessAsync(userId, testOrderId, ct);
        return await HplcDissolutionStandard.ResolveAsync(_db, testOrderId, ct);
    }
```
Controller (after `GetTestOrderEvidence`):
```csharp
    [HttpGet("test-orders/{testOrderId:int}/dissolution-standard")]
    public async Task<IActionResult> GetDissolutionStandard(int testOrderId) =>
        Ok(ApiResponse<object>.Ok(await _service.GetDissolutionStandardAsync(testOrderId, CurrentUserId)));
```
`SampleApprovalService` - replace the `hplcWorkspaceCodes` filter with:
```csharp
            var hplcWorkspaceCodes = sstDefinitions
                .Where(t => t.WorkflowType is WorkflowType.HplcMethodAssay or WorkflowType.Dissolution)
                .Select(t => t.Code).ToList();
```
and add `&& s.HplcRun.Status != HplcRunStatus.Abandoned` to its `runPassed` predicate.

Add `authorization-matrix.txt` line for the new endpoint by running the architecture test and copying its reported expected line (the matrix test prints the missing entry).

- [ ] **Step 4: Approval gate test** (in the existing dissolution engine test file, `DissolutionWorkflowEngineTests`): extend `Stage1_NextStageRequired_...` is not needed; add:

```csharp
    [Fact]
    public async Task Approval_DissolutionOnAbandonedRun_Blocked()
    {
        using var db = NewDb();
        var (_, equip, _, _, _, sample, order, analyst, head) = SetupDissolutionScenario(db);
        var run = db.HplcRuns.Single();
        run.Status = HplcRunStatus.Abandoned;
        db.SaveChanges();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            TestServiceFactory.TestWorkflow(db).RecordDissolutionResultAsync(order.Id, new DissolutionPayload(
                DateTime.UtcNow, equip.Id, new Dictionary<string, string> { ["Medium"] = "0.01M HCl 900mL", ["RPM"] = "50" },
                900m, 1m, new List<decimal> { 0.45m, 0.45m, 0.45m, 0.45m, 0.45m, 0.45m }, "Password123!"), analyst.Id));
        Assert.Equal(HplcDissolutionStandard.NotAssigned, ex.Message);
    }
```

- [ ] **Step 5: Run tests**

Run: `dotnet test backend/MicroLIMS.Tests --artifacts-path "$TEMP/rls-art" --filter "FullyQualifiedName~Hplc|FullyQualifiedName~Dissolution|FullyQualifiedName~Authorization|FullyQualifiedName~SampleApproval"`
Expected: all pass.

- [ ] **Step 6: Commit**

```bash
git add backend
git commit -m "Assign dissolution samples to HPLC runs and gate approval on the run"
```

---

### Task 6: Frontend - method dilution and test-master method picker

**Files:**
- Modify: `frontend/src/modules/laboratoryConfiguration/masterDataSimple/services/HplcMethodService.ts` (analyte response ~line 27, request ~line 98)
- Modify: `frontend/src/modules/laboratoryConfiguration/masterDataSimple/hplcMethod/hplcMethodForm.ts` (type ~32, defaults ~114, from-response ~177, to-request ~239)
- Modify: `frontend/src/modules/laboratoryConfiguration/masterDataSimple/hplcMethod/hplcMethodValidation.ts` (~205)
- Modify: `frontend/src/modules/laboratoryConfiguration/masterDataSimple/hplcMethod/HplcAnalytesSection.tsx` (after the Th.Wt test field ~227)
- Modify: `frontend/src/modules/laboratoryConfiguration/masterDataSimple/TestMasterPage.tsx` (picker block ~2887; payload lines ~2442 and ~2503)

**Interfaces:**
- Consumes: Task 1 API field `standardDilution: number | null`; Task 2 Dissolution `hplcMethodId`.

- [ ] **Step 1: Service types** - add `standardDilution?: number | null;` to both the analyte response and request interfaces.

- [ ] **Step 2: Form model** - in `hplcMethodForm.ts` add `standardDilution: string | number;` to the analyte form type, `standardDilution: ""` to the new-analyte default, `standardDilution: a.standardDilution ?? ""` in the from-response mapper, and in the to-request mapper:
```ts
      standardDilution: a.standardDilution === "" || a.standardDilution == null ? null : Number(a.standardDilution),
```

- [ ] **Step 3: Validation** - in `hplcMethodValidation.ts`, after the Th.Wt test check:
```ts
    if (a.standardDilution !== "" && a.standardDilution != null) {
      const dil = Number(a.standardDilution);
      if (isNaN(dil) || dil <= 0) {
        add(key("standardDilution"), `Analyte "${aName}" standard dilution must be greater than zero.`);
      }
    }
```

- [ ] **Step 4: Field** - in `HplcAnalytesSection.tsx`, copy the Th.Wt test `TextField` block and change it to:
```tsx
                <TextField
                  size="small"
                  type="number"
                  label="Standard Dilution (mL)"
                  value={a.standardDilution}
                  onChange={(e) => onAnalyteChange(idx, "standardDilution", e.target.value)}
                  error={Boolean(err("standardDilution"))}
                  helperText={err("standardDilution") ?? "Dissolution only: volume the standard is diluted to"}
                />
```
(match the surrounding `Grid` wrapper and props of the Th.Wt field exactly).

- [ ] **Step 5: Test master** - change the picker condition `{workflowType === "HplcMethodAssay" && (` to `{(workflowType === "HplcMethodAssay" || workflowType === "Dissolution") && (`; for Dissolution the title reads "HPLC Method (supplies the dissolution standard)" and the select is not `required` (use `const isDissolutionMethod = workflowType === "Dissolution";` and conditional label/required). In both payloads change:
```ts
          hplcMethodId: (isHplcMethodAssay || workflowType === "Dissolution") && hplcMethodId !== "" ? Number(hplcMethodId) : null
```
Do not set `methodAbbreviation` from the chosen method for Dissolution (keep the `setMethodAbbreviation` call inside `if (chosen && workflowType === "HplcMethodAssay")`).

- [ ] **Step 6: Verify**

Run (in `frontend`): `npx tsc --noEmit -p . && npx eslint src/modules/laboratoryConfiguration/masterDataSimple`
Expected: no output.

- [ ] **Step 7: Commit**

```bash
git add frontend/src/modules/laboratoryConfiguration
git commit -m "Edit standard dilution and link dissolution tests to an HPLC method"
```

---

### Task 7: Frontend - dissolution panel uses the workspace run

**Files:**
- Modify: `frontend/src/modules/hplcWorkspace/services/HplcWorkspaceService.ts`
- Modify: `frontend/src/modules/hplcWorkspace/types.ts` (`HplcRunSampleSummaryDto` gains `isDissolution: boolean`; new `HplcDissolutionStandard` types)
- Modify: `frontend/src/modules/testingWorkspace/DissolutionPanel.tsx` (state ~90-95, load ~159-171, `linkRun` ~245-262, validation deps ~300, SST block ~586-640)
- Modify: `frontend/src/modules/hplcWorkspace/samples/SampleAssignmentPanel.tsx` (actions ~105-115)

**Interfaces:**
- Consumes: Task 5 `GET api/hplc-workspace/test-orders/{id}/dissolution-standard` returning `{ standard: {...} | null, problem: string | null }`, `isDissolution` on run samples.

- [ ] **Step 1: Types and service**

`types.ts`:
```ts
export interface HplcDissolutionStandardValues {
  hplcRunId: number;
  runSampleId: number;
  equipmentId: number;
  runCode: string;
  sstCode: string;
  meanResponse: number;
  standardWeightMg: number;
  purityPercent: number;
  moisturePercent: number;
  standardDilution: number;
  cs: number;
}

export interface HplcDissolutionStandardResult {
  standard: HplcDissolutionStandardValues | null;
  problem: string | null;
}
```
and add `isDissolution: boolean;` to the run-sample summary interface.

`HplcWorkspaceService.ts` (follow the file's existing GET helper style, e.g. the one used for `test-orders/{id}/evidence`):
```ts
  getDissolutionStandard: (testOrderId: number) =>
    get<HplcDissolutionStandardResult>(`/hplc-workspace/test-orders/${testOrderId}/dissolution-standard`),
```

- [ ] **Step 2: DissolutionPanel state and load** - replace the four SST-run state hooks with:
```ts
  // Standard from the HPLC workspace run this sample is assigned to.
  const [hplcStandard, setHplcStandard] = useState<HplcDissolutionStandardResult | null>(null);
```
Replace the "4. Fetch suitability runs" try block with:
```ts
      try {
        setHplcStandard(await HplcWorkspaceService.getDissolutionStandard(testOrderId));
      } catch {
        setHplcStandard({ standard: null, problem: "The HPLC run standard could not be loaded." });
      }
```
Delete `linkRun` and the `SystemSuitabilityService` / `SystemSuitabilityRun` imports. In the Stage 1 validation memo replace any `linkedRun` check with `if (!hplcStandard?.standard) return false;` and swap `linkedRun` for `hplcStandard` in its dependency list.

- [ ] **Step 3: DissolutionPanel block** - replace the whole `<ResultSection step={1} title="System suitability run">...</ResultSection>` with:
```tsx
      <ResultSection step={1} title="HPLC run standard">
        {hplcStandard?.standard ? (
          <Stack useFlexGap direction="row" spacing={1} sx={{ alignItems: "center", flexWrap: "wrap" }}>
            <Typography sx={{ fontWeight: 600, fontSize: 13 }}>{hplcStandard.standard.runCode}</Typography>
            <StatusBadge status="Passed" label={hplcStandard.standard.sstCode} />
            <Typography sx={{ fontSize: 13, color: "text.secondary" }}>
              Mean area {hplcStandard.standard.meanResponse} · weight {hplcStandard.standard.standardWeightMg} mg · P {hplcStandard.standard.purityPercent}% · MC {hplcStandard.standard.moisturePercent}% · dilution {hplcStandard.standard.standardDilution} mL · Cs {hplcStandard.standard.cs.toFixed(6)} mg/mL
            </Typography>
            <Button size="small" component={RouterLink} to={`/hplc-workspace/${hplcStandard.standard.equipmentId}/run/${hplcStandard.standard.hplcRunId}/samples`}>
              Open run
            </Button>
          </Stack>
        ) : (
          <Alert severity="info">
            {hplcStandard?.problem ?? "Assign this sample to an HPLC run with a passed system suitability."} Use the HPLC Workspace.
          </Alert>
        )}
      </ResultSection>
```
(import `Link as RouterLink` from `react-router-dom` if not imported.)

- [ ] **Step 4: Workspace sample row** - in `SampleAssignmentPanel.tsx`, for dissolution rows send the analyst to the Testing page instead of the replicate entry:
```tsx
              onClick={() =>
                s.isDissolution
                  ? navigate(`/receiving-testing?testOrderId=${s.testOrderId}`)
                  : navigate(`/hplc-workspace/${run.equipmentId}/run/${run.id}/sample/${s.id}`)
              }
```
and label: `{s.submitted ? "View Entry" : s.isDissolution ? "Enter Dissolution" : "Enter Replicates"}`.
Before writing the URL, confirm how the Testing page opens a specific test order: `git grep -n "testOrderId" frontend/src/modules/testingWorkspace/*Page*.tsx`. If it has no deep-link parameter, navigate to `/receiving-testing` with the sample reference as the search text the page already supports (`git grep -n "searchParams\|useSearchParams" frontend/src/modules/testingWorkspace`), and note which one was used in the commit message.

- [ ] **Step 5: Verify**

Run (in `frontend`): `npx tsc --noEmit -p . && npx eslint src/modules/testingWorkspace/DissolutionPanel.tsx src/modules/hplcWorkspace && npx vitest run src/modules/testingWorkspace`
Expected: clean; existing tests pass.

- [ ] **Step 6: Commit**

```bash
git add frontend/src/modules/hplcWorkspace frontend/src/modules/testingWorkspace/DissolutionPanel.tsx
git commit -m "Show the HPLC run standard on the dissolution panel"
```

---

### Task 8: Stage 1 verification - STOP POINT

- [ ] **Step 1:** Back up and apply the Task 1 migration:
```bash
pg_dump -Fc -h localhost -U postgres -d LIMSV2 -f E:/MicroLIMS/backups/LIMSV2_before_standard_dilution_20261002.dump
dotnet ef database update --project backend/MicroLIMS.Persistence --startup-project backend/MicroLIMS.API --configuration Release
```
- [ ] **Step 2:** `dotnet build backend` then the full Postgres suite (`bash .claude/scripts/run-postgres-tests.sh` or the repo's documented command; it uses `--no-build`, so build first). Expected: 0 failures.
- [ ] **Step 3:** Frontend: `npx tsc --noEmit -p .`, `npx eslint src`, `npm test`.
- [ ] **Step 4:** Browser (API + Vite restarted on this branch): set standard dilution on a single-analyte method; link DISS-UI to it in Test Master; start a run, pass SST; assign the DISS-UI order; open it from the run; record Stage 1 with the dissolution tester; send for review; approve.
- [ ] **Step 5:** Report results to the user and **stop** until they approve Stage 2.

---

# Stage 2 - Remove the legacy module

### Task 9: Backend code removal

**Files (delete):**
- `backend/MicroLIMS.API/Controllers/SystemSuitabilityController.cs`
- `backend/MicroLIMS.Application/Interfaces/ISystemSuitabilityService.cs`
- `backend/MicroLIMS.Application/Services/SystemSuitabilityService.cs`
- `backend/MicroLIMS.Application/DTOs/SystemSuitabilityDtos.cs`
- `backend/MicroLIMS.Application/DTOs/StandardComparisonContextDto.cs`
- `backend/MicroLIMS.Application/Workflows/TestWorkflow/StandardComparisonRecorder.cs`
- `backend/MicroLIMS.Domain/Entities/SystemSuitabilityRun.cs`, `SystemSuitabilityRunAnalyte.cs`, `SystemSuitabilityStandardResponse.cs`, `StandardComparisonCalculationData.cs`
- `backend/MicroLIMS.Domain/Enums/ResponseMode.cs`
- `backend/MicroLIMS.Persistence/Configurations/SystemSuitabilityRunConfiguration.cs`, `SystemSuitabilityRunAnalyteConfiguration.cs`, `SystemSuitabilityStandardResponseConfiguration.cs`
- Tests: `IntegrationTests/StandardComparisonPostgresIntegrationTests.cs`, `StandardComparisonTitrationPostgresIntegrationTests.cs`, `SystemSuitabilityRunCodePostgresIntegrationTests.cs` (move any `SystemSuitabilityRunCode` cases still meaningful for HPLC/calibration codes into `HplcRunServiceTests` first), `SystemSuitabilityStandardPostgresIntegrationTests.cs`; `UnitTests/StandardComparisonTitrationTests.cs`, `StandardComparisonWorkflowUnitTests.cs`, `SystemSuitabilitySliceA2Tests.cs`, `SystemSuitabilityStandardDescriptionTests.cs`.

**Files (modify):** `TestOrder.cs` (drop `SystemSuitabilityRunId` + nav), `TestOrderConfiguration.cs`, `TestDefinition.cs` (drop `ResponseMode`), `MicroLimsDbContext.cs` + `IMicroLimsDbContext.cs` (drop 3 DbSets), `UniqueIndexNames.cs`, `ServiceCollectionExtensions.cs` (DI), `TestWorkflowController.cs`, `TestWorkflowContracts.cs`, `TestWorkflowEngine.cs`, `TestWorkflowSupport.cs`, `TestWorkflowQueryService.cs`, `SampleApprovalService.cs` (delete the legacy branch after `continue;`), `TestAnalyteMasterDataService.cs`, `TestDefinitionMasterDataService.cs` (ResponseMode + SST checks, `HplcMaxPreparationRsdPercent` only if exclusively StandardComparison), `UserReferenceRegistry.cs`, `UserSectionScopeService.cs`, `ReportingQueryService.cs`, `SpecificationService.cs`, `AnalysisWorkflows.cs`, `EquationType.cs`, `WorkflowType.cs` (comment `StandardComparison` as retired like `HplcAssay`), `HplcRunService.cs` (SST code series: `_db.HplcSstRecords.Select(x => x.Code)` only), `StandardComparisonCalculator.cs` (keep `CalculatePreparationAssay`, `CalculatePreparationRsd` and constants they use; delete the rest), `TestServiceFactory.cs`, `authorization-matrix.txt`.

- [ ] **Step 1:** Delete the files above (`git rm`).
- [ ] **Step 2:** `dotnet build backend 2>&1 | grep -E "error CS" | sort -u` and fix each error by removing the legacy branch (never by stubbing). For each `WorkflowType.StandardComparison` reference: remove it from lists of active workflows; where a switch must stay exhaustive, map it to the same "retired" handling `HplcAssay` gets.
- [ ] **Step 3:** `git grep -n -i "SystemSuitabilityRun\b\|SystemSuitabilityRuns\|StandardComparisonRecorder\|ResponseMode\|SystemSuitabilityRunId" -- backend ':!backend/MicroLIMS.Persistence/Migrations'` - expect only `SystemSuitabilityRunCode` (helper) hits.
- [ ] **Step 4:** Run `dotnet test backend/MicroLIMS.Tests --artifacts-path "$TEMP/rls-art"` (InMemory suite). Expected: 0 failures. The model snapshot change is generated in Task 10, so do not commit until Task 10's migration exists.

---

### Task 10: Data migration `RetireLegacySystemSuitability`

**Files:**
- Create: migration `RetireLegacySystemSuitability` (generated, then edited)

- [ ] **Step 1:** Generate: `dotnet ef migrations add RetireLegacySystemSuitability --project backend/MicroLIMS.Persistence --startup-project backend/MicroLIMS.API --configuration Release`. Expected generated `Up`: drop FK/index/column `TestOrders.SystemSuitabilityRunId`, drop column `TestDefinitions.ResponseMode`, drop the three tables.

- [ ] **Step 2:** Insert this SQL at the **top** of `Up` (before the generated drops):
```csharp
            // Legacy SST retirement (spec 2026-10-02): delete Standard Comparison
            // orders and old SST rows. Abort if any Standard Comparison test other
            // than 'Water soluble' has orders - that data was not reviewed.
            migrationBuilder.Sql(@"
DO $$
DECLARE codes text;
BEGIN
  SELECT string_agg(DISTINCT o.""TestCode"", ', ') INTO codes
  FROM ""TestOrders"" o JOIN ""TestDefinitions"" d ON d.""Code"" = o.""TestCode""
  WHERE d.""WorkflowType"" = 11 AND d.""Code"" <> 'Water soluble';
  IF codes IS NOT NULL THEN
    RAISE EXCEPTION 'RetireLegacySystemSuitability: Standard Comparison tests still have orders: %', codes;
  END IF;
END $$;

CREATE TEMP TABLE sc_orders AS
  SELECT o.""Id"" FROM ""TestOrders"" o JOIN ""TestDefinitions"" d ON d.""Code"" = o.""TestCode"" WHERE d.""WorkflowType"" = 11;
CREATE TEMP TABLE sc_codes AS SELECT ""Code"" FROM ""TestDefinitions"" WHERE ""WorkflowType"" = 11;

DELETE FROM ""ResultReadings"" WHERE ""ParameterResultId"" IN (SELECT ""Id"" FROM ""ParameterResults"" WHERE ""TestOrderId"" IN (SELECT ""Id"" FROM sc_orders));
DELETE FROM ""ParameterResults"" WHERE ""TestOrderId"" IN (SELECT ""Id"" FROM sc_orders);
DELETE FROM ""TestAnalyses"" WHERE ""TestOrderId"" IN (SELECT ""Id"" FROM sc_orders);
DELETE FROM ""ResultRecords"" WHERE ""TestOrderId"" IN (SELECT ""Id"" FROM sc_orders);
DELETE FROM ""TestReturnEvents"" WHERE ""TestOrderId"" IN (SELECT ""Id"" FROM sc_orders);
DELETE FROM ""WorkflowStepResults"" WHERE ""TestOrderId"" IN (SELECT ""Id"" FROM sc_orders);
DELETE FROM ""LocationPathogenObservations"" WHERE ""TestOrderId"" IN (SELECT ""Id"" FROM sc_orders);
DELETE FROM ""HplcRunSamples"" WHERE ""TestOrderId"" IN (SELECT ""Id"" FROM sc_orders);
DELETE FROM ""TestOrders"" WHERE ""Id"" IN (SELECT ""Id"" FROM sc_orders);

DELETE FROM ""SystemSuitabilityRuns"";

DELETE FROM ""Specifications"" WHERE ""TestCode"" IN (SELECT ""Code"" FROM sc_codes);
DELETE FROM ""SampleTests"" WHERE ""TestCode"" IN (SELECT ""Code"" FROM sc_codes);
DELETE FROM ""SamplingConfigurations"" WHERE ""TestCode"" IN (SELECT ""Code"" FROM sc_codes);
DELETE FROM ""MachinePartConfigurations"" WHERE ""TestCode"" IN (SELECT ""Code"" FROM sc_codes);
DELETE FROM ""RoomTestConfigurations"" WHERE ""TestCode"" IN (SELECT ""Code"" FROM sc_codes);
DELETE FROM ""WorkloadWeightHistories"" WHERE ""TestCode"" IN (SELECT ""Code"" FROM sc_codes);
DELETE FROM ""WorkloadWeights"" WHERE ""TestCode"" IN (SELECT ""Code"" FROM sc_codes);
DELETE FROM ""TestAnalytes"" WHERE ""TestDefinitionId"" IN (SELECT ""Id"" FROM ""TestDefinitions"" WHERE ""WorkflowType"" = 11);
DELETE FROM ""TestDefinitions"" WHERE ""WorkflowType"" = 11;

DROP TABLE sc_orders;
DROP TABLE sc_codes;
");
```
Before trusting the list: run `"\d+"` style checks for every table above (`\d "TestReturnEvents"` etc.) - the column names must exist; and run
```sql
select conrelid::regclass::text from pg_constraint where contype='f' and confrelid::regclass::text in ('"TestOrders"','"TestDefinitions"','"Specifications"','"TestAnalytes"','"ParameterResults"') and confdeltype <> 'c';
```
Every table it lists with a RESTRICT/NO ACTION FK must have a `DELETE` line above (or be proven empty for these rows). Add missing ones. Remove lines for tables that have no such column.

- [ ] **Step 3: `Down`:** keep the generated recreate statements (tables and columns come back empty). Add a comment: `// Data deleted in Up is not restored.`

- [ ] **Step 4: Guard check (Review Focus 5)** on a scratch copy:
```bash
createdb -h localhost -U postgres LIMSV2_rls_scratch
pg_restore -h localhost -U postgres -d LIMSV2_rls_scratch E:/MicroLIMS/backups/LIMSV2_before_standard_dilution_20261002.dump
```
Rename the Standard Comparison test so the guard sees a non-`Water soluble` test with orders:
```sql
UPDATE "TestDefinitions" SET "Code" = 'SC-GUARD' WHERE "Code" = 'Water soluble';
UPDATE "TestOrders" SET "TestCode" = 'SC-GUARD' WHERE "TestCode" = 'Water soluble';
```
Then `dotnet ef database update --project backend/MicroLIMS.Persistence --startup-project backend/MicroLIMS.API --configuration Release --connection "Host=localhost;Port=5432;Database=LIMSV2_rls_scratch;Username=postgres;Password=<local password>"`.
Expected: fails with `Standard Comparison tests still have orders: SC-GUARD`, and `select count(*) from "SystemSuitabilityRuns"` on the scratch DB is still 2. Then `dropdb -h localhost -U postgres LIMSV2_rls_scratch`.

- [ ] **Step 5: Apply to LIMSV2**
```bash
pg_dump -Fc -h localhost -U postgres -d LIMSV2 -f E:/MicroLIMS/backups/LIMSV2_before_retire_legacy_sst_20261002.dump
dotnet ef database update --project backend/MicroLIMS.Persistence --startup-project backend/MicroLIMS.API --configuration Release
```
Verify: `\dt *uitab*` returns no rows; `select count(*) from "TestDefinitions" where "WorkflowType"=11` = 0.

- [ ] **Step 6: Down/Up round trip** on LIMSV2: `dotnet ef database update AddHplcMethodAnalyteStandardDilution ...` then `dotnet ef database update ...` again. Expected: both succeed.

- [ ] **Step 7: Commit (Tasks 9 + 10)**
```bash
git add -A backend
git commit -m "Retire the legacy system suitability module and Standard Comparison workflow"
```

---

### Task 11: Frontend removal

**Files (delete):** `frontend/src/modules/systemSuitability/` (whole folder), `frontend/src/modules/testingWorkspace/StandardComparisonPanel.tsx`.
**Files (modify):** `frontend/src/routes/AppRoutes.tsx` (lazy imports lines ~34, ~48 and route ~159), `frontend/src/routes/menuConfig.ts` (line ~128), `testingWorkspace/TestWorkflowDialog.tsx`, `TestResultCards.tsx`, `SampleSummaryDialog.tsx`, `services/TestWorkflowService.ts`, `types/testWorkflowTypes.ts`, `types/sampleSummaryTypes.ts`, `laboratoryConfiguration/masterDataSimple/TestMasterPage.tsx` (remove `"StandardComparison"` from the FP workflow list line ~64, labels, response-mode and Standard Comparison config UI, `HplcMaxPreparationRsdPercent` field if only Standard Comparison used it), `laboratoryConfiguration/items/components/ItemSpecificationsSection.tsx`, `SpecificationParameterDialog.tsx`, `hooks/useTestDefinitions.ts`, `services/masterDataOptions.ts`.

- [ ] **Step 1:** `git rm -r frontend/src/modules/systemSuitability frontend/src/modules/testingWorkspace/StandardComparisonPanel.tsx`
- [ ] **Step 2:** Run `npx tsc --noEmit -p .` and remove each reference it reports (delete the branch/case/option; do not leave stubs).
- [ ] **Step 3:** `git grep -n -i "standardComparison\|systemSuitability\|responseMode" -- frontend/src` - expect only `hplcWorkspace/sst/*` (workspace SST) hits.
- [ ] **Step 4:** `npx eslint src` and `npm test`. Expected: clean / green.
- [ ] **Step 5: Commit**
```bash
git add -A frontend
git commit -m "Remove the legacy system suitability and standard comparison screens"
```

---

### Task 12: Stage 2 verification and production guard - STOP POINT

- [ ] **Step 1:** `dotnet build backend` then the full Postgres suite. Expected: 0 failures.
- [ ] **Step 2:** Browser: menu has no "System Suitability (HPLC)"; `/laboratory/system-suitability` shows the not-found page; one HPLC-xxx assay and one dissolution sample still complete through a workspace run.
- [ ] **Step 3:** Hand the user these read-only Neon queries (do not run against production yourself):
```sql
select count(*) from "SystemSuitabilityRuns";
select "TestCode", count(*) from "TestOrders" where "TestCode" in (select "Code" from "TestDefinitions" where "WorkflowType" = 11) group by 1;
select count(*) from "TestOrders" where "TestCode" in (select "Code" from "TestDefinitions" where "WorkflowType" = 7);
```
Any non-zero result: stop; the user decides again before merge.
- [ ] **Step 4:** Update memory `retire-legacy-sst.md` with commits and status; report to the user; **stop** (no push, no PR unless asked).
