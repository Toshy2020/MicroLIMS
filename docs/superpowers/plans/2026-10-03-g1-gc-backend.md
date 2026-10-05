# G1 — GC Backend on the Chromatography Module Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let an `HplcMethod` describe a GC method (assay or residual solvents) and let the existing run → SST → sample → submission chain execute it on a GC instrument.

**Architecture:** GC extends the existing `Hplc*` tables (spec D9): a `Technique` and `ResultMode` on `HplcMethod`, nullable GC columns, an oven-program child table. Run code branches on the snapshot's technique (instrument type, no mobile phases) and result mode (ppm calculator instead of assay %). No new tables for runs/samples; no rename.

**Tech Stack:** ASP.NET Core 8, EF Core + Npgsql, xUnit (EF InMemory unit tests + Postgres integration suite).

**Spec:** `docs/superpowers/specs/2026-10-03-icp-gc-workspaces-design.md` (§3, §5, §7 G1, §8).

## Global Constraints

- Branch `feat/instrument-workspaces`. Commit after every task; message ends with the session's attribution lines.
- All build output on E: — use `--artifacts-path E:/MicroLIMS/rls-tmp/g1-artifacts` for every `dotnet build`/`dotnet test`.
- Postgres suite: `dotnet build backend/MicroLIMS.Tests --artifacts-path E:/MicroLIMS/rls-tmp/g1-artifacts` THEN `bash .claude/scripts/run-postgres-tests.sh E:/MicroLIMS/rls-tmp/g1-artifacts` (the script uses `--no-build`).
- Migrations: `dotnet ef migrations add <Name> --project backend/MicroLIMS.Persistence --startup-project backend/MicroLIMS.API --configuration Release`. Do NOT apply to LIMSV2 — Task 7 does that after a backup.
- Enum values are appended only (stored as int); never reorder.
- Existing HPLC methods, runs and snapshots must behave exactly as before (old snapshot JSON has no `technique`/`resultMode` → defaults `Hplc`/`Assay`).
- Backend owns every rule; error messages below are user-facing and must match exactly.
- Keep new files small; put GC-specific service code in partial files (`*.Gc.cs`).

## Review Focus

1. An existing HPLC method saved before this change (null GC fields, snapshot without `technique`) still edits, starts runs and submits unchanged — Task 2 test `Update_ExistingHplcMethod_StillValid`, Task 5 test `OldSnapshotWithoutTechnique_ReadsAsHplc`.
2. Someone edits a GC method and sends a different technique or result mode — refused, never silently converted — Task 2 test `Update_ChangingTechniqueOrMode_Throws`.
3. Starting a GC run on an HPLC instrument (or an HPLC run on a GC instrument) — refused with a clear message — Task 5 tests `StartRun_GcMethodOnHplcInstrument_Throws` / `StartRun_HplcMethodOnGcInstrument_Throws`.
4. A residual-solvent sample whose SST standard mean is zero or missing — entry problem message, no divide-by-zero — Task 4 test `Calculate_ZeroStandardMean_Throws` + Task 6 test `RsEntry_MissingSstMean_ShowsProblem`.
5. A residual-solvents test whose item has no ppm spec, or an assay-% spec on a residual-solvents method — clear refusal — Task 3 test `Spec_RsMethod_RequiresPpmNotMoreThan`, Task 6 test `RsEntry_NoPpmSpec_ShowsProblem`.

---

### Task 1: Domain, EF configuration and migration

**Files:**
- Create: `backend/MicroLIMS.Domain/Enums/HplcTechnique.cs`, `backend/MicroLIMS.Domain/Enums/HplcResultMode.cs`, `backend/MicroLIMS.Domain/Enums/CarrierGas.cs`
- Modify: `backend/MicroLIMS.Domain/Enums/HplcDetectorType.cs`, `backend/MicroLIMS.Domain/Enums/ResultBasis.cs`, `backend/MicroLIMS.Domain/Entities/HplcMethod.cs`
- Modify: `backend/MicroLIMS.Persistence/Configurations/HplcMethodConfiguration.cs`, `backend/MicroLIMS.Persistence/DbContext/MicroLimsDbContext.cs:113`, `backend/MicroLIMS.Application/Abstractions/Persistence/IMicroLimsDbContext.cs:109`
- Modify (compile fixes only): `backend/MicroLIMS.Application/DTOs/Responses/HplcMethodResponse.cs`, `backend/MicroLIMS.Application/Services/HplcMethodService.cs`
- Create: migration `AddGcMethodFields`

**Interfaces — Produces:**
```csharp
public enum HplcTechnique { Hplc = 0, Gc = 1 }
public enum HplcResultMode { Assay = 0, ResidualSolvents = 1 }
public enum CarrierGas { Helium = 0, Nitrogen = 1, Hydrogen = 2 }
// HplcDetectorType: UV, PDA, FLD, RI, ELSD, Other, Fid, Tcd, Ecd, Ms   (appended)
// ResultBasis: ..., PercentAnhydrousBasis, Ppm                         (appended)
public class HplcMethodOvenStep { int Id; int HplcMethodId; HplcMethod? HplcMethod; int StepNo; decimal? RateCPerMin; decimal TemperatureC; decimal HoldMin; }
// DbSet<HplcMethodOvenStep> HplcMethodOvenSteps on IMicroLimsDbContext + MicroLimsDbContext
```

- [ ] **Step 1: Enums.** Create the three enum files exactly as above (namespace `MicroLIMS.Domain.Enums`, one-line comment each: `// GC on the chromatography module (spec 2026-10-03 §3).`). Append `Fid, Tcd, Ecd, Ms` to `HplcDetectorType` and `Ppm` to `ResultBasis`.

- [ ] **Step 2: Entity.** In `HplcMethod.cs`:
  - add after `IsActive`:
    ```csharp
    public HplcTechnique Technique { get; set; } = HplcTechnique.Hplc;   // fixed at creation
    public HplcResultMode ResultMode { get; set; } = HplcResultMode.Assay; // fixed at creation
    ```
  - change `ParticleSizeUm` and `ColumnTemperatureC` to `decimal?` with comment `// HPLC only`.
  - add after `ColumnTemperatureC`:
    ```csharp
    // GC only (null on HPLC). FlowRateMlPerMin is the carrier gas flow on GC.
    public decimal? FilmThicknessUm { get; set; }
    public CarrierGas? CarrierGas { get; set; }
    public decimal? SplitRatio { get; set; }              // null = splitless
    public decimal? InletTemperatureC { get; set; }
    public decimal? DetectorTemperatureC { get; set; }
    public bool HeadspaceEnabled { get; set; }
    public decimal? HeadspaceEquilibrationTemperatureC { get; set; }
    public decimal? HeadspaceEquilibrationMin { get; set; }
    public decimal? HeadspaceTransferLineTemperatureC { get; set; }
    public decimal? SampleSolutionVolumeMl { get; set; } // residual solvents only
    ```
  - add `public List<HplcMethodOvenStep> OvenSteps { get; set; } = new();` after `GradientSteps`.
  - `HplcMethodAnalyte`: `WavelengthNm` → `decimal?` (`// HPLC only`); add `public decimal? StandardConcentrationUgPerMl { get; set; } // residual solvents only`.
  - add the `HplcMethodOvenStep` class (shape above) at the end of the file, comment `// GC oven program row; the first row has no ramp rate (initial temperature + hold).`

- [ ] **Step 3: EF config.** In `HplcMethodConfiguration`:
  - `decimal(10,3)` for `FilmThicknessUm, SplitRatio, InletTemperatureC, DetectorTemperatureC, HeadspaceEquilibrationTemperatureC, HeadspaceEquilibrationMin, HeadspaceTransferLineTemperatureC, SampleSolutionVolumeMl`.
  - `builder.HasMany(e => e.OvenSteps).WithOne(c => c.HplcMethod).HasForeignKey(c => c.HplcMethodId).OnDelete(DeleteBehavior.Cascade);`
  - analyte: `builder.Property(e => e.StandardConcentrationUgPerMl).HasColumnType("decimal(12,4)");`
  - new class:
    ```csharp
    public class HplcMethodOvenStepConfiguration : IEntityTypeConfiguration<HplcMethodOvenStep>
    {
        public void Configure(EntityTypeBuilder<HplcMethodOvenStep> builder)
        {
            builder.HasKey(e => e.Id);
            builder.Property(e => e.RateCPerMin).HasColumnType("decimal(10,3)");
            builder.Property(e => e.TemperatureC).HasColumnType("decimal(10,3)");
            builder.Property(e => e.HoldMin).HasColumnType("decimal(10,3)");
            builder.HasIndex(e => new { e.HplcMethodId, e.StepNo }).IsUnique();
        }
    }
    ```
  - DbSet in both context files next to `HplcMethodGradientSteps`: `DbSet<HplcMethodOvenStep> HplcMethodOvenSteps` (interface `{ get; }`, context `=> Set<HplcMethodOvenStep>();`).

- [ ] **Step 4: Compile fixes.** `HplcMethodResponse`: `ParticleSizeUm`, `ColumnTemperatureC` → `decimal?`; `HplcMethodAnalyteResponse.WavelengthNm` → `decimal?`. `HplcMethodService` request records: `SaveHplcMethodRequest.ParticleSizeUm`, `.ColumnTemperatureC` → `decimal?`; `HplcAnalyteInput.WavelengthNm` → `decimal?`. In `ValidateCommonAsync` keep behaviour identical for now: replace `r.ParticleSizeUm <= 0m` with `!(r.ParticleSizeUm > 0m)`, `r.ColumnTemperatureC <= 0m` with `!(r.ColumnTemperatureC > 0m)`, and the wavelength check with `if (!(a.WavelengthNm >= 190m && a.WavelengthNm <= 900m))`. Existing call sites pass decimals and still compile.

- [ ] **Step 5: Build + existing tests.**
  Run: `dotnet build backend/MicroLIMS.Tests --artifacts-path E:/MicroLIMS/rls-tmp/g1-artifacts` → 0 errors.
  Run: `dotnet test backend/MicroLIMS.Tests --no-build --artifacts-path E:/MicroLIMS/rls-tmp/g1-artifacts --filter "FullyQualifiedName~Hplc"` → all PASS.

- [ ] **Step 6: Migration.** `dotnet ef migrations add AddGcMethodFields --project backend/MicroLIMS.Persistence --startup-project backend/MicroLIMS.API --configuration Release`. Read the generated `Up`: it must contain ONLY — new nullable columns on `HplcMethods`, `Technique`/`ResultMode` int NOT NULL default 0, `HeadspaceEnabled` bool NOT NULL default false, `ParticleSizeUm`/`ColumnTemperatureC` altered to nullable, `HplcMethodAnalytes.WavelengthNm` nullable + `StandardConcentrationUgPerMl`, new table `HplcMethodOvenSteps` + unique index. Anything else (drops, renames, other tables) → stop and report.

- [ ] **Step 7: Commit** `feat(gc): technique, GC method fields and oven program schema`.

---

### Task 2: HplcMethodService — GC methods (TDD)

**Files:**
- Modify: `backend/MicroLIMS.Application/Services/HplcMethodService.cs` (make class `partial`; extend records; call new validators; apply/replace oven steps; include oven steps in `QueryWithIncludes` and the update load; technique filter in `GetAllAsync`; list item gets `Technique`)
- Create: `backend/MicroLIMS.Application/Services/HplcMethodService.Gc.cs` (validation split by technique)
- Modify: `backend/MicroLIMS.Application/DTOs/Responses/HplcMethodResponse.cs`
- Modify: `backend/MicroLIMS.API/Controllers/HplcMethodController.cs:21-23`
- Modify: `backend/MicroLIMS.Tests/UnitTests/HplcMethodServiceTests.cs:10` (`public partial class`)
- Create: `backend/MicroLIMS.Tests/UnitTests/HplcMethodServiceGcTests.cs`

**Interfaces — Produces (later tasks and G2 rely on these names):**
```csharp
public record GcOvenStepInput(decimal? RateCPerMin, decimal TemperatureC, decimal HoldMin);
public record HplcAnalyteInput(int? Id, string Name, decimal? WavelengthNm, int StandardEntryId,
    decimal TheoreticalWeightStdMg, decimal TheoreticalWeightTestMg, int StandardInjections,
    decimal? SstMaxRsdPercent = null, decimal? SstMinResolution = null, decimal? SstMaxTailingFactor = null,
    decimal? SstMinTheoreticalPlates = null, decimal? SstMinRetentionFactor = null,
    decimal? SstMinSignalToNoise = null, decimal? SstMinPeakToValley = null, decimal? StandardDilution = null,
    decimal? StandardConcentrationUgPerMl = null);
public record SaveHplcMethodRequest(
    string Name, string Abbreviation, DateTime EffectiveDate,
    string ColumnDesignation, decimal ColumnLengthMm, decimal ColumnInternalDiameterMm, decimal? ParticleSizeUm,
    decimal? ColumnTemperatureC, ElutionMode ElutionMode, decimal FlowRateMlPerMin,
    HplcDetectorType DetectorType, decimal InjectionVolumeUl, decimal RunTimeMin, int DiluentSolutionId,
    List<HplcMobilePhaseInput> MobilePhases, List<HplcGradientStepInput> GradientSteps, List<HplcAnalyteInput> Analytes,
    string? ColumnBrand = null, string? ColumnPartNumber = null, decimal? EquilibrationMin = null,
    int? SectionId = null, string? Reason = null,
    HplcTechnique Technique = HplcTechnique.Hplc, HplcResultMode ResultMode = HplcResultMode.Assay,
    decimal? FilmThicknessUm = null, CarrierGas? CarrierGas = null, decimal? SplitRatio = null,
    decimal? InletTemperatureC = null, decimal? DetectorTemperatureC = null,
    List<GcOvenStepInput>? OvenSteps = null,
    bool HeadspaceEnabled = false, decimal? HeadspaceEquilibrationTemperatureC = null,
    decimal? HeadspaceEquilibrationMin = null, decimal? HeadspaceTransferLineTemperatureC = null,
    decimal? SampleSolutionVolumeMl = null);
public record HplcMethodListItem(int Id, string Name, string Abbreviation, bool IsActive, int AnalyteCount,
    string SectionName, DateTime LastModifiedAt, HplcTechnique Technique = HplcTechnique.Hplc, HplcResultMode ResultMode = HplcResultMode.Assay);
public Task<List<HplcMethodListItem>> GetAllAsync(int currentUserId, bool activeOnly = false, HplcTechnique? technique = null, CancellationToken ct = default);
// HplcMethodResponse gains: Technique, ResultMode, FilmThicknessUm, CarrierGas, SplitRatio, InletTemperatureC,
//   DetectorTemperatureC, HeadspaceEnabled, HeadspaceEquilibrationTemperatureC, HeadspaceEquilibrationMin,
//   HeadspaceTransferLineTemperatureC, SampleSolutionVolumeMl, List<HplcMethodOvenStepResponse> OvenSteps
public record HplcMethodOvenStepResponse(int Id, int StepNo, decimal? RateCPerMin, decimal TemperatureC, decimal HoldMin);
// HplcMethodAnalyteResponse gains trailing `decimal? StandardConcentrationUgPerMl = null`
```

**Validation rules (exact messages):**

Common (both techniques, already present — keep): name, abbreviation, column designation, diluent checks, analyte name/duplicate, standard entry checks, `StandardInjections >= 1`, SST criteria `> 0` when given. Common numeric: `ColumnLengthMm, ColumnInternalDiameterMm, FlowRateMlPerMin, InjectionVolumeUl, RunTimeMin` all `> 0` → `"Column and run parameters must be greater than zero."`

HPLC (`Technique == Hplc`) — current rules moved into `ValidateHplcSection(r)`:
- `ParticleSizeUm > 0` and `ColumnTemperatureC > 0` else `"Column and run parameters must be greater than zero."`
- detector in `{UV, PDA, FLD, RI, ELSD, Other}` else `"Detector {d} is not an HPLC detector."`
- `ResultMode == Assay` else `"Residual solvents mode is only available for GC methods."`
- any GC field set (`FilmThicknessUm, CarrierGas, SplitRatio, InletTemperatureC, DetectorTemperatureC, SampleSolutionVolumeMl` non-null, `OvenSteps` non-empty, `HeadspaceEnabled`) → `"GC settings are not used on an HPLC method."`
- mobile phases, ratios, gradient, wavelength 190–900, Th.Wt > 0, StandardDilution > 0 when given — existing code and messages unchanged.

GC (`Technique == Gc`) in `ValidateGcSection(r)` (in `HplcMethodService.Gc.cs`):
- `FilmThicknessUm > 0` else `"Film thickness is required for a GC method."`
- `CarrierGas` not null else `"Carrier gas is required for a GC method."`
- `InletTemperatureC > 0 && DetectorTemperatureC > 0` else `"Inlet and detector temperatures are required for a GC method."`
- `SplitRatio` null or `>= 1` else `"Split ratio must be 1 or more (leave empty for splitless)."`
- detector in `{Fid, Tcd, Ecd, Ms}` else `"Detector {d} is not a GC detector."`
- `MobilePhases.Count == 0 && GradientSteps.Count == 0 && ElutionMode == Isocratic && ParticleSizeUm == null && ColumnTemperatureC == null && EquilibrationMin == null` else `"Mobile phases, gradient, particle size, column temperature and equilibration are HPLC-only."`
- oven: `OvenSteps` count >= 1 else `"A GC method needs at least one oven program step."`; step 1 `RateCPerMin == null` else `"The first oven step is the initial temperature and has no ramp rate."`; steps 2.. `RateCPerMin > 0` else `"Oven step {n}: the ramp rate must be greater than zero."`; every `TemperatureC > 0` and `HoldMin >= 0` else `"Oven step {n}: temperature must be greater than zero and hold zero or more."`
- headspace: when `HeadspaceEnabled`, all three headspace values `> 0` else `"Headspace temperatures and equilibration time are required when headspace is on."`; when off, all three null else `"Headspace values are only used when headspace is on."`
- analytes: `WavelengthNm == null` else `"Wavelength is not used on a GC method."`
- `ResultMode == Assay`: Th.Wt > 0 (existing message `"Theoretical weights must be greater than zero."`), `StandardConcentrationUgPerMl == null`, `SampleSolutionVolumeMl == null` else `"Standard concentration and sample solution volume are only used for residual solvents."`
- `ResultMode == ResidualSolvents`: `SampleSolutionVolumeMl > 0` else `"Sample solution volume is required for residual solvents."`; each analyte `StandardConcentrationUgPerMl > 0` else `"{name}: standard concentration (µg/mL) is required."`; each analyte Th.Wt both `== 0` and `StandardDilution == null` else `"Theoretical weights and standard dilution are not used for residual solvents."`

Update only (in `UpdateAsync`, before `ValidateCommonAsync`): `r.Technique != method.Technique || r.ResultMode != method.ResultMode` → `"The technique and result mode can't be changed after the method is created."`

Restructure `ValidateCommonAsync`: keep the common part, then `if (r.Technique == HplcTechnique.Gc) ValidateGcSection(r); else await ValidateHplcSectionAsync(r, sectionId, alreadyUsedSolutionIds, ct);` (mobile-phase solution lookups need the db, so the HPLC part stays async in the main file; the analyte loop's wavelength/Th.Wt checks move into the per-technique validators; the standard-entry lookup stays common).

`ApplyFields` sets every new field (on update technique/mode are already equal). New `ReplaceOvenSteps(method, r)` mirroring `ReplaceGradientSteps` with `StepNo = index + 1`. `ApplyAnalytes` sets `StandardConcentrationUgPerMl`. `QueryWithIncludes` and the update load add `.Include(m => m.OvenSteps)`. Response maps oven steps ordered by `StepNo`.

Controller: `public async Task<IActionResult> GetAll([FromQuery] bool activeOnly = false, [FromQuery] HplcTechnique? technique = null) => Ok(ApiResponse<object>.Ok(await _service.GetAllAsync(CurrentUserId, activeOnly, technique)));`

- [ ] **Step 1: Write the failing tests** — `HplcMethodServiceGcTests.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

// GC on the chromatography module (spec 2026-10-03 §3.1). Shares helpers with HplcMethodServiceTests.
public partial class HplcMethodServiceTests
{
    private static HplcAnalyteInput SolventInput(int standardEntryId, string name = "Methanol", int? id = null, decimal? conc = 60m) =>
        new(id, name, null, standardEntryId, 0m, 0m, 6, SstMaxRsdPercent: 15m, StandardConcentrationUgPerMl: conc);

    private static SaveHplcMethodRequest GcReq(
        int diluentId, List<HplcAnalyteInput> analytes,
        HplcResultMode mode = HplcResultMode.ResidualSolvents, List<GcOvenStepInput>? oven = null,
        HplcDetectorType detector = HplcDetectorType.Fid, decimal? sampleVolume = 20m,
        string abbreviation = "RS-01", bool headspace = true, string? reason = null) =>
        new("Residual Solvents", abbreviation, DateTime.UtcNow,
            "G43", 30000m, 0.53m, null, null, ElutionMode.Isocratic, 3.5m,
            detector, 1000m, 40m, diluentId,
            new List<HplcMobilePhaseInput>(), new List<HplcGradientStepInput>(), analytes,
            Reason: reason,
            Technique: HplcTechnique.Gc, ResultMode: mode, FilmThicknessUm: 3m, CarrierGas: CarrierGas.Helium,
            SplitRatio: 5m, InletTemperatureC: 140m, DetectorTemperatureC: 250m,
            OvenSteps: oven ?? new List<GcOvenStepInput> { new(null, 40m, 20m), new(10m, 240m, 20m) },
            HeadspaceEnabled: headspace,
            HeadspaceEquilibrationTemperatureC: headspace ? 80m : null,
            HeadspaceEquilibrationMin: headspace ? 60m : null,
            HeadspaceTransferLineTemperatureC: headspace ? 105m : null,
            SampleSolutionVolumeMl: mode == HplcResultMode.ResidualSolvents ? sampleVolume : null);

    [Fact]
    public async Task CreateGc_ResidualSolvents_SavesGcFieldsAndOvenSteps()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var (diluent, _, standard) = await SeedBasicsAsync(db, section.Id);
        var service = TestServiceFactory.HplcMethod(db);

        var result = await service.CreateAsync(GcReq(diluent.Id, new() { SolventInput(standard.Id) }), userId);

        Assert.Equal(HplcTechnique.Gc, result.Technique);
        Assert.Equal(HplcResultMode.ResidualSolvents, result.ResultMode);
        Assert.Equal(CarrierGas.Helium, result.CarrierGas);
        Assert.Equal(2, result.OvenSteps.Count);
        Assert.Null(result.OvenSteps[0].RateCPerMin);
        Assert.Equal(2, result.OvenSteps[1].StepNo);
        Assert.Empty(result.MobilePhases);
        Assert.Equal(60m, result.Analytes[0].StandardConcentrationUgPerMl);
        Assert.Null(result.Analytes[0].WavelengthNm);
        Assert.Equal(20m, result.SampleSolutionVolumeMl);
    }

    [Fact]
    public async Task CreateGc_Assay_RequiresThWtAndRejectsStandardConcentration()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var (diluent, _, standard) = await SeedBasicsAsync(db, section.Id);
        var service = TestServiceFactory.HplcMethod(db);

        var ok = await service.CreateAsync(GcReq(diluent.Id,
            new() { new(null, "Vitamin E", null, standard.Id, 50m, 50m, 5) }, mode: HplcResultMode.Assay, abbreviation: "VE-GC"), userId);
        Assert.Equal(HplcResultMode.Assay, ok.ResultMode);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(GcReq(diluent.Id,
            new() { new(null, "Vitamin E", null, standard.Id, 50m, 50m, 5, StandardConcentrationUgPerMl: 10m) },
            mode: HplcResultMode.Assay, abbreviation: "VE-GC2"), userId));
        Assert.Equal("Standard concentration and sample solution volume are only used for residual solvents.", ex.Message);
    }

    [Theory]
    [InlineData("film")]
    [InlineData("gas")]
    [InlineData("detector")]
    [InlineData("mobile")]
    [InlineData("oven-empty")]
    [InlineData("oven-first-rate")]
    [InlineData("oven-ramp")]
    [InlineData("headspace")]
    [InlineData("wavelength")]
    [InlineData("volume")]
    [InlineData("conc")]
    public async Task CreateGc_InvalidField_Throws(string field)
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var (diluent, mp, standard) = await SeedBasicsAsync(db, section.Id);
        var service = TestServiceFactory.HplcMethod(db);
        var r = GcReq(diluent.Id, new() { SolventInput(standard.Id) });

        (SaveHplcMethodRequest req, string message) = field switch
        {
            "film" => (r with { FilmThicknessUm = null }, "Film thickness is required for a GC method."),
            "gas" => (r with { CarrierGas = null }, "Carrier gas is required for a GC method."),
            "detector" => (r with { DetectorType = HplcDetectorType.UV }, "Detector UV is not a GC detector."),
            "mobile" => (r with { MobilePhases = new() { new("A", mp.Id, null) } }, "Mobile phases, gradient, particle size, column temperature and equilibration are HPLC-only."),
            "oven-empty" => (r with { OvenSteps = new() }, "A GC method needs at least one oven program step."),
            "oven-first-rate" => (r with { OvenSteps = new() { new(10m, 40m, 5m) } }, "The first oven step is the initial temperature and has no ramp rate."),
            "oven-ramp" => (r with { OvenSteps = new() { new(null, 40m, 5m), new(0m, 200m, 5m) } }, "Oven step 2: the ramp rate must be greater than zero."),
            "headspace" => (r with { HeadspaceEquilibrationMin = null }, "Headspace temperatures and equilibration time are required when headspace is on."),
            "wavelength" => (r with { Analytes = new() { SolventInput(standard.Id) with { WavelengthNm = 254m } } }, "Wavelength is not used on a GC method."),
            "volume" => (r with { SampleSolutionVolumeMl = null }, "Sample solution volume is required for residual solvents."),
            "conc" => (r with { Analytes = new() { SolventInput(standard.Id, conc: null) } }, "Methanol: standard concentration (µg/mL) is required."),
            _ => throw new ArgumentOutOfRangeException(nameof(field)),
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(req, userId));
        Assert.Equal(message, ex.Message);
    }

    [Fact]
    public async Task CreateHplc_WithGcFieldsOrResidualMode_Throws()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var (diluent, mp, standard) = await SeedBasicsAsync(db, section.Id);
        var service = TestServiceFactory.HplcMethod(db);
        var hplc = Req(diluent.Id, new() { new("A", mp.Id, null) }, new() { AnalyteInput(standard.Id) });

        var gcField = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(hplc with { CarrierGas = CarrierGas.Helium }, userId));
        Assert.Equal("GC settings are not used on an HPLC method.", gcField.Message);

        var mode = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(hplc with { ResultMode = HplcResultMode.ResidualSolvents }, userId));
        Assert.Equal("Residual solvents mode is only available for GC methods.", mode.Message);

        var detector = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(hplc with { DetectorType = HplcDetectorType.Fid }, userId));
        Assert.Equal("Detector Fid is not an HPLC detector.", detector.Message);
    }

    [Fact]
    public async Task Update_ChangingTechniqueOrMode_Throws()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var (diluent, _, standard) = await SeedBasicsAsync(db, section.Id);
        var service = TestServiceFactory.HplcMethod(db);
        var created = await service.CreateAsync(GcReq(diluent.Id, new() { SolventInput(standard.Id) }), userId);

        var assay = GcReq(diluent.Id, new() { new(created.Analytes[0].Id, "Methanol", null, standard.Id, 50m, 50m, 6) },
            mode: HplcResultMode.Assay, reason: "switch");
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateAsync(created.Id, assay, userId));
        Assert.Equal("The technique and result mode can't be changed after the method is created.", ex.Message);
    }

    [Fact]
    public async Task Update_Gc_ReplacesOvenSteps()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var (diluent, _, standard) = await SeedBasicsAsync(db, section.Id);
        var service = TestServiceFactory.HplcMethod(db);
        var created = await service.CreateAsync(GcReq(diluent.Id, new() { SolventInput(standard.Id) }), userId);

        var updated = await service.UpdateAsync(created.Id, GcReq(diluent.Id,
            new() { SolventInput(standard.Id, id: created.Analytes[0].Id) },
            oven: new() { new(null, 35m, 5m), new(8m, 120m, 0m), new(20m, 240m, 10m) }, reason: "new oven"), userId);

        Assert.Equal(3, updated.OvenSteps.Count);
        Assert.Equal(3, await db.HplcMethodOvenSteps.CountAsync(s => s.HplcMethodId == created.Id));
        Assert.Equal(created.Analytes[0].Id, updated.Analytes[0].Id);
    }

    [Fact]
    public async Task Update_ExistingHplcMethod_StillValid()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var (diluent, mp, standard) = await SeedBasicsAsync(db, section.Id);
        var service = TestServiceFactory.HplcMethod(db);
        var created = await service.CreateAsync(Req(diluent.Id, new() { new("A", mp.Id, null) }, new() { AnalyteInput(standard.Id) }), userId);

        var updated = await service.UpdateAsync(created.Id,
            Req(diluent.Id, new() { new("A", mp.Id, null) }, new() { AnalyteInput(standard.Id, id: created.Analytes[0].Id) }, name: "Renamed", reason: "rename"),
            userId);

        Assert.Equal(HplcTechnique.Hplc, updated.Technique);
        Assert.Equal("Renamed", updated.Name);
        Assert.Empty(updated.OvenSteps);
    }

    [Fact]
    public async Task GetAll_FiltersByTechnique()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var (diluent, mp, standard) = await SeedBasicsAsync(db, section.Id);
        var service = TestServiceFactory.HplcMethod(db);
        await service.CreateAsync(Req(diluent.Id, new() { new("A", mp.Id, null) }, new() { AnalyteInput(standard.Id) }), userId);
        await service.CreateAsync(GcReq(diluent.Id, new() { SolventInput(standard.Id) }), userId);

        var gc = await service.GetAllAsync(userId, technique: HplcTechnique.Gc);
        var all = await service.GetAllAsync(userId);

        Assert.Single(gc);
        Assert.Equal(HplcTechnique.Gc, gc[0].Technique);
        Assert.Equal(2, all.Count);
    }
}
```

- [ ] **Step 2: Run, expect FAIL (compile errors on new members).**
  Run: `dotnet build backend/MicroLIMS.Tests --artifacts-path E:/MicroLIMS/rls-tmp/g1-artifacts`

- [ ] **Step 3: Implement** per the records, rules and wiring above. Change `HplcMethodServiceTests` declaration to `public partial class HplcMethodServiceTests`.

- [ ] **Step 4: Run, expect PASS.**
  Run: `dotnet build backend/MicroLIMS.Tests --artifacts-path E:/MicroLIMS/rls-tmp/g1-artifacts && dotnet test backend/MicroLIMS.Tests --no-build --artifacts-path E:/MicroLIMS/rls-tmp/g1-artifacts --filter "FullyQualifiedName~Hplc"`

- [ ] **Step 5: Commit** `feat(gc): GC method validation, oven program and technique filter`.

---

### Task 3: Specification basis ppm for residual solvents (TDD)

**Files:**
- Modify: `backend/MicroLIMS.Application/Services/SpecificationService.cs:260-293`
- Modify: `backend/MicroLIMS.Tests/UnitTests/HplcMethodAssayTestMasterTests.cs` (add tests + a GC method helper)

**Rule:** inside the `HplcMethodAssay` block load the analyte with its method (`.Include(a => a.HplcMethod)`). If `analyte.HplcMethod!.ResultMode == HplcResultMode.ResidualSolvents`: basis must be `Ppm` and limit type `NotMoreThan`, label claim empty → else `"Residual-solvent specifications use ppm with a not-more-than limit."`; skip the assay-basis checks. Otherwise (assay): the existing checks, plus `Ppm` is refused by the existing `"Result basis must be assay % ..."` message. The duplicate check applies to both.

- [ ] **Step 1: Failing tests** (append to `HplcMethodAssayTestMasterTests`):

```csharp
    private static async Task<HplcMethodResponse> AddRsMethodAsync(MicroLimsDbContext db, int sectionId)
    {
        var adminId = await EnsureAdminUserAsync(db);
        var diluent = await AddSolutionAsync(db, sectionId, "Diluent DMSO", SolutionType.Diluent);
        var standard = await AddEntryAsync(db, sectionId, "STD-MEOH");
        return await TestServiceFactory.HplcMethod(db).CreateAsync(new SaveHplcMethodRequest(
            "Residual Solvents", "RS-01", DateTime.UtcNow,
            "G43", 30000m, 0.53m, null, null, ElutionMode.Isocratic, 3.5m,
            HplcDetectorType.Fid, 1000m, 40m, diluent.Id,
            new(), new(), new() { new(null, "Methanol", null, standard.Id, 0m, 0m, 6, StandardConcentrationUgPerMl: 60m) },
            SectionId: sectionId, Technique: HplcTechnique.Gc, ResultMode: HplcResultMode.ResidualSolvents,
            FilmThicknessUm: 3m, CarrierGas: CarrierGas.Helium, InletTemperatureC: 140m, DetectorTemperatureC: 250m,
            OvenSteps: new() { new(null, 40m, 20m) }, SampleSolutionVolumeMl: 20m), adminId);
    }

    private static async Task<(SpecificationMasterDataService Svc, Item Item, TestDefinitionDto Def, int AnalyteId, int UserId)> ArrangeRsSpecAsync(MicroLimsDbContext db)
    {
        var (section, userId) = await SeedAsync(db);
        var method = await AddRsMethodAsync(db, section.Id);
        var testDef = await new TestDefinitionMasterDataService(db, new UserSectionScopeService(db))
            .CreateTestDefinitionAsync(userId, AssayReq(section.Id, method.Id, code: "RS-T1"));
        var item = new Item
        {
            Code = "ITEM-" + Guid.NewGuid().ToString("N")[..6], Name = "Item 1",
            AssignedTests = { new SampleTest { TestCode = testDef.Code, DisplayName = testDef.DisplayName } }
        };
        db.Items.Add(item);
        await db.SaveChangesAsync();
        return (new SpecificationMasterDataService(db, new UserSectionScopeService(db)), item, testDef, method.Analytes[0].Id, userId);
    }

    [Fact]
    public async Task Spec_RsMethod_AcceptsPpmNotMoreThan()
    {
        await using var db = NewDb();
        var a = await ArrangeRsSpecAsync(db);
        await a.Svc.CreateSpecificationAsync(a.UserId, new CreateSpecificationRequest(
            ItemId: a.Item.Id, TestCode: a.Def.Code, ParameterName: "Methanol",
            LimitType: LimitType.NotMoreThan, LowerLimit: null, UpperLimit: 3000m,
            ResultBasis: ResultBasis.Ppm, HplcMethodAnalyteId: a.AnalyteId));
        Assert.True(await db.Specifications.AnyAsync(s => s.ResultBasis == ResultBasis.Ppm && s.UpperLimit == 3000m));
    }

    [Theory]
    [InlineData(ResultBasis.PercentLabelClaim, LimitType.NotMoreThan)]
    [InlineData(ResultBasis.Ppm, LimitType.Range)]
    public async Task Spec_RsMethod_RequiresPpmNotMoreThan(ResultBasis basis, LimitType limitType)
    {
        await using var db = NewDb();
        var a = await ArrangeRsSpecAsync(db);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => a.Svc.CreateSpecificationAsync(a.UserId, new CreateSpecificationRequest(
            ItemId: a.Item.Id, TestCode: a.Def.Code, ParameterName: "Methanol",
            LimitType: limitType, LowerLimit: limitType == LimitType.Range ? 0m : null, UpperLimit: 3000m,
            ResultBasis: basis, HplcMethodAnalyteId: a.AnalyteId)));
        Assert.Equal("Residual-solvent specifications use ppm with a not-more-than limit.", ex.Message);
    }

    [Fact]
    public async Task Spec_AssayMethod_RejectsPpm()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var method = await AddMethodAsync(db, section.Id, userId);
        var testDef = await new TestDefinitionMasterDataService(db, new UserSectionScopeService(db)).CreateTestDefinitionAsync(userId, AssayReq(section.Id, method.Id));
        var item = new Item { Code = "ITEM-" + Guid.NewGuid().ToString("N")[..6], Name = "Item 1",
            AssignedTests = { new SampleTest { TestCode = testDef.Code, DisplayName = testDef.DisplayName } } };
        db.Items.Add(item);
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => new SpecificationMasterDataService(db, new UserSectionScopeService(db))
            .CreateSpecificationAsync(userId, new CreateSpecificationRequest(
                ItemId: item.Id, TestCode: testDef.Code, ParameterName: "Assay",
                LimitType: LimitType.NotMoreThan, LowerLimit: null, UpperLimit: 3000m,
                ResultBasis: ResultBasis.Ppm, HplcMethodAnalyteId: method.Analytes[0].Id)));
        Assert.Contains("Result basis must be assay %", ex.Message);
    }
```
  If `CreateTestDefinitionAsync` returns a type other than `TestDefinitionDto`, use its actual return type in the tuple (check the existing call at line ~281).

- [ ] **Step 2: Run, expect FAIL.** `--filter "FullyQualifiedName~HplcMethodAssayTestMasterTests"`
- [ ] **Step 3: Implement** the rule.
- [ ] **Step 4: Run, expect PASS** (same filter).
- [ ] **Step 5: Commit** `feat(gc): ppm specifications for residual-solvent methods`.

---

### Task 4: ResidualSolventCalculator (pure, TDD)

**Files:**
- Create: `backend/MicroLIMS.Application/Helpers/ResidualSolventCalculator.cs`
- Create: `backend/MicroLIMS.Tests/UnitTests/ResidualSolventCalculatorTests.cs`

**Interfaces — Produces:**
```csharp
public record ResidualSolventReplicateInput(int ReplicateNo, decimal SampleWeightMg, decimal Response);
public record ResidualSolventReplicateResult(int ReplicateNo, decimal Ppm);
public record ResidualSolventResult(IReadOnlyList<ResidualSolventReplicateResult> Replicates, decimal MeanPpm);
public static class ResidualSolventCalculator
{
    // ppm (µg/g) = (C_std [µg/mL] × V [mL] / W [g]) × (r_u / r̄_std)
    public static ResidualSolventResult Calculate(decimal standardConcentrationUgPerMl, decimal sampleSolutionVolumeMl,
        decimal standardMeanResponse, IReadOnlyList<ResidualSolventReplicateInput> reps);
    public static string FormatPpm(decimal v); // rounded to 1 decimal, "2850.0 ppm"
}
```

- [ ] **Step 1: Failing tests:**

```csharp
using MicroLIMS.Application.Helpers;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class ResidualSolventCalculatorTests
{
    // Hand check: C_std 60 µg/mL, V 20 mL, W 400 mg = 0.4 g -> 3000 µg/g at r_u = r_std.
    //   rep 1: r_u 900  -> 3000 × 0.9 = 2700 ppm
    //   rep 2: r_u 1000 -> 3000 ppm;  mean 2850 ppm
    [Fact]
    public void Calculate_HandCheckedExample()
    {
        var r = ResidualSolventCalculator.Calculate(60m, 20m, 1000m, new[]
        {
            new ResidualSolventReplicateInput(1, 400m, 900m),
            new ResidualSolventReplicateInput(2, 400m, 1000m),
        });
        Assert.Equal(2700m, r.Replicates[0].Ppm);
        Assert.Equal(3000m, r.Replicates[1].Ppm);
        Assert.Equal(2850m, r.MeanPpm);
        Assert.Equal("2850.0 ppm", ResidualSolventCalculator.FormatPpm(r.MeanPpm));
    }

    [Theory]
    [InlineData(0, 20, 1000, 400)]
    [InlineData(60, 0, 1000, 400)]
    [InlineData(60, 20, 0, 400)]
    [InlineData(60, 20, 1000, 0)]
    public void Calculate_NonPositiveInput_Throws(decimal conc, decimal volume, decimal stdMean, decimal weight)
    {
        Assert.Throws<InvalidOperationException>(() => ResidualSolventCalculator.Calculate(conc, volume, stdMean,
            new[] { new ResidualSolventReplicateInput(1, weight, 900m) }));
    }

    [Fact]
    public void Calculate_ZeroStandardMean_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => ResidualSolventCalculator.Calculate(60m, 20m, 0m,
            new[] { new ResidualSolventReplicateInput(1, 400m, 900m) }));
        Assert.Equal("The standard mean response must be greater than zero.", ex.Message);
    }

    [Fact]
    public void Calculate_NoReplicates_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => ResidualSolventCalculator.Calculate(60m, 20m, 1000m, Array.Empty<ResidualSolventReplicateInput>()));
    }
}
```

- [ ] **Step 2: Run, expect FAIL.** `--filter "FullyQualifiedName~ResidualSolventCalculatorTests"`
- [ ] **Step 3: Implement:**

```csharp
using System.Globalization;

namespace MicroLIMS.Application.Helpers;

public record ResidualSolventReplicateInput(int ReplicateNo, decimal SampleWeightMg, decimal Response);
public record ResidualSolventReplicateResult(int ReplicateNo, decimal Ppm);
public record ResidualSolventResult(IReadOnlyList<ResidualSolventReplicateResult> Replicates, decimal MeanPpm);

// GC residual solvents (USP <467> quantitation, spec 2026-10-03 §3.3). Pure - no I/O.
// ppm (µg/g) = (C_std [µg/mL] × V [mL] / W [g]) × (r_u / r̄_std)
public static class ResidualSolventCalculator
{
    public static ResidualSolventResult Calculate(decimal standardConcentrationUgPerMl, decimal sampleSolutionVolumeMl,
        decimal standardMeanResponse, IReadOnlyList<ResidualSolventReplicateInput> reps)
    {
        if (standardConcentrationUgPerMl <= 0m) throw new InvalidOperationException("The standard concentration must be greater than zero.");
        if (sampleSolutionVolumeMl <= 0m) throw new InvalidOperationException("The sample solution volume must be greater than zero.");
        if (standardMeanResponse <= 0m) throw new InvalidOperationException("The standard mean response must be greater than zero.");
        if (reps.Count == 0) throw new InvalidOperationException("At least one replicate is required.");

        var results = reps.Select(rep =>
        {
            if (rep.SampleWeightMg <= 0m) throw new InvalidOperationException($"Replicate {rep.ReplicateNo}: the sample weight must be greater than zero.");
            var ppm = standardConcentrationUgPerMl * sampleSolutionVolumeMl / (rep.SampleWeightMg / 1000m) * (rep.Response / standardMeanResponse);
            return new ResidualSolventReplicateResult(rep.ReplicateNo, ppm);
        }).ToList();

        return new ResidualSolventResult(results, results.Average(r => r.Ppm));
    }

    public static string FormatPpm(decimal v) =>
        $"{Math.Round(v, 1, MidpointRounding.AwayFromZero).ToString("0.0", CultureInfo.InvariantCulture)} ppm";
}
```

- [ ] **Step 4: Run, expect PASS.**
- [ ] **Step 5: Commit** `feat(gc): residual solvent ppm calculator`.

---

### Task 5: Runs by technique — instruments, method options, start run, columns (TDD)

**Files:**
- Modify: `backend/MicroLIMS.Application/Services/HplcRunService.cs` (`GetInstrumentsAsync`, `GetMethodOptionsAsync`, `StartRunAsync`, `HplcMethodOptionDto`)
- Modify: `backend/MicroLIMS.Application/Services/ChromatographyColumnService.cs:117,187`
- Modify: `backend/MicroLIMS.API/Controllers/HplcWorkspaceController.cs:33-39`
- Create: `backend/MicroLIMS.Tests/UnitTests/HplcRunServiceGcTests.cs` (partial `HplcRunServiceTests`)

**Interfaces — Produces:**
```csharp
public static class HplcTechniqueEquipment
{
    public static EquipmentType For(HplcTechnique t) => t == HplcTechnique.Gc ? EquipmentType.Gc : EquipmentType.Hplc;
    public static string Label(HplcTechnique t) => t == HplcTechnique.Gc ? "GC" : "HPLC";
} // in HplcRunService.cs, above the class
public record HplcMethodOptionDto(int Id, string Abbreviation, string Name, string ColumnDesignation, int EligibleTestOrderCount,
    HplcTechnique Technique = HplcTechnique.Hplc, HplcResultMode ResultMode = HplcResultMode.Assay);
public Task<List<HplcInstrumentDto>> GetInstrumentsAsync(int userId, HplcTechnique technique = HplcTechnique.Hplc, CancellationToken ct = default);
public Task<List<HplcMethodOptionDto>> GetMethodOptionsAsync(int userId, HplcTechnique technique = HplcTechnique.Hplc, CancellationToken ct = default);
```

**Changes:**
- `GetInstrumentsAsync`: `e.Type == HplcTechniqueEquipment.For(technique)`.
- `GetMethodOptionsAsync`: `.Where(m => m.IsActive && m.Technique == technique)`; DTO gets technique + mode.
- `StartRunAsync`: replace the `EquipmentType.Hplc` check — after loading the method:
  `if (equipment.Type != HplcTechniqueEquipment.For(method.Technique)) throw new InvalidOperationException($"\"{equipment.Name}\" is not a {HplcTechniqueEquipment.Label(method.Technique)} instrument; method {method.Abbreviation} needs one.");`
  Move the equipment-type check below the method load (the open-run and availability checks stay where they are). Add `.Include(m => m.OvenSteps)` to the method load so the snapshot carries the oven program. GC methods have no mobile phases, so the existing loop is a no-op and the request's `MobilePhases` must be empty (`inputs.Count != method.MobilePhases.Count` already enforces it).
- `ChromatographyColumnService` lines 117 and 187: allow `EquipmentType.Hplc` or `EquipmentType.Gc`; message `"Equipment \"{eq.Code}\" is not an HPLC or GC instrument. Only chromatography equipment can be linked to a column."`
- Controller: `GetInstruments([FromQuery] HplcTechnique technique = HplcTechnique.Hplc)` and `GetMethodOptions([FromQuery] HplcTechnique technique = HplcTechnique.Hplc)` pass it through.

- [ ] **Step 1: Failing tests** — `HplcRunServiceGcTests.cs`:

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

// GC on the chromatography module (spec 2026-10-03 §3.3).
public partial class HplcRunServiceTests
{
    private sealed class GcScenario
    {
        public required DocumentSection Section;
        public required int UserId;
        public required HplcMethod Method;
        public required Equipment Equipment;
        public required ChromatographyColumn Column;
        public required MaterialMasterEntry StandardEntry;
    }

    private static async Task<GcScenario> SeedGcScenarioAsync(MicroLimsDbContext db, HplcResultMode mode = HplcResultMode.ResidualSolvents)
    {
        var (section, userId) = await SeedAsync(db);
        var diluentEntry = await AddEntryAsync(db, section.Id, "DMSO");
        var standardEntry = await AddEntryAsync(db, section.Id, "STD-MEOH", MaterialMasterCategory.ReferenceStandard);
        var diluent = await AddSolutionAsync(db, section.Id, "Diluent DMSO", SolutionType.Diluent, diluentEntry);
        var rs = mode == HplcResultMode.ResidualSolvents;
        var created = await TestServiceFactory.HplcMethod(db).CreateAsync(new SaveHplcMethodRequest(
            "Residual Solvents", "RS-01", DateTime.UtcNow,
            "G43", 30000m, 0.53m, null, null, ElutionMode.Isocratic, 3.5m,
            HplcDetectorType.Fid, 1000m, 40m, diluent.Id,
            new(), new(),
            new() { rs
                ? new(null, "Methanol", null, standardEntry.Id, 0m, 0m, 3, SstMaxRsdPercent: 15m, StandardConcentrationUgPerMl: 60m)
                : new(null, "Vitamin E", null, standardEntry.Id, 50m, 50m, 3, SstMaxRsdPercent: 2m) },
            SectionId: section.Id, Technique: HplcTechnique.Gc, ResultMode: mode,
            FilmThicknessUm: 3m, CarrierGas: CarrierGas.Helium, InletTemperatureC: 140m, DetectorTemperatureC: 250m,
            OvenSteps: new() { new(null, 40m, 20m), new(10m, 240m, 20m) },
            SampleSolutionVolumeMl: rs ? 20m : null), 1);
        var method = await db.HplcMethods.FirstAsync(m => m.Id == created.Id);

        var gc = new Equipment { Name = "GC GC-01", Code = "GC-01", Type = EquipmentType.Gc, SectionId = section.Id, CalibrationDueDate = DateTime.UtcNow.AddYears(1) };
        db.Equipment.Add(gc);
        await db.SaveChangesAsync();
        var column = await AddColumnAsync(db, section.Id, gc, code: "GCOL-01", uspDesignation: "G43");

        return new GcScenario { Section = section, UserId = userId, Method = method, Equipment = gc, Column = column, StandardEntry = standardEntry };
    }

    private static StartHplcRunRequest GcStart(GcScenario g) => new(g.Equipment.Id, g.Method.Id, g.Column.Id, new());

    [Fact]
    public async Task StartRun_GcMethodOnGcInstrument_NoMobilePhases_SnapshotHasOvenSteps()
    {
        await using var db = NewDb();
        var (_, clock) = NewClock(SepFirst);
        var g = await SeedGcScenarioAsync(db);
        var service = TestServiceFactory.HplcRun(db, clock: clock);

        var run = await service.StartRunAsync(GcStart(g), g.UserId);

        Assert.Empty(run.MobilePhases);
        var stored = await db.HplcRuns.FirstAsync(r => r.Id == run.Id);
        var snapshot = JsonSerializer.Deserialize<HplcMethodResponse>(stored.MethodSnapshotJson, SnapshotJson.Options)!;
        Assert.Equal(HplcTechnique.Gc, snapshot.Technique);
        Assert.Equal(2, snapshot.OvenSteps.Count);
    }

    [Fact]
    public async Task StartRun_GcMethodOnHplcInstrument_Throws()
    {
        await using var db = NewDb();
        var (_, clock) = NewClock(SepFirst);
        var g = await SeedGcScenarioAsync(db);
        var hplc = await AddHplcEquipmentAsync(db, g.Section.Id);
        var service = TestServiceFactory.HplcRun(db, clock: clock);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.StartRunAsync(new StartHplcRunRequest(hplc.Id, g.Method.Id, g.Column.Id, new()), g.UserId));
        Assert.Equal("\"HPLC HPLC-01\" is not a GC instrument; method RS-01 needs one.", ex.Message);
    }

    [Fact]
    public async Task StartRun_HplcMethodOnGcInstrument_Throws()
    {
        await using var db = NewDb();
        var (_, clock) = NewClock(SepFirst);
        var s = await SeedScenarioAsync(db, clock);
        var gc = new Equipment { Name = "GC GC-09", Code = "GC-09", Type = EquipmentType.Gc, SectionId = s.Section.Id, CalibrationDueDate = DateTime.UtcNow.AddYears(1) };
        db.Equipment.Add(gc);
        await db.SaveChangesAsync();
        var service = TestServiceFactory.HplcRun(db, clock: clock);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.StartRunAsync(StartRequest(s) with { EquipmentId = gc.Id }, s.UserId));
        Assert.Equal("\"GC GC-09\" is not a HPLC instrument; method VIT-C needs one.", ex.Message);
    }

    [Fact]
    public async Task Instruments_AndMethodOptions_FilterByTechnique()
    {
        await using var db = NewDb();
        var (_, clock) = NewClock(SepFirst);
        var g = await SeedGcScenarioAsync(db);
        await AddHplcEquipmentAsync(db, g.Section.Id);
        var service = TestServiceFactory.HplcRun(db, clock: clock);

        var gcInstruments = await service.GetInstrumentsAsync(g.UserId, HplcTechnique.Gc);
        var hplcInstruments = await service.GetInstrumentsAsync(g.UserId);
        var gcMethods = await service.GetMethodOptionsAsync(g.UserId, HplcTechnique.Gc);
        var hplcMethods = await service.GetMethodOptionsAsync(g.UserId);

        Assert.Equal("GC-01", Assert.Single(gcInstruments).Code);
        Assert.Equal("HPLC-01", Assert.Single(hplcInstruments).Code);
        Assert.Equal(HplcResultMode.ResidualSolvents, Assert.Single(gcMethods).ResultMode);
        Assert.Empty(hplcMethods);
    }

    [Fact]
    public void OldSnapshotWithoutTechnique_ReadsAsHplc()
    {
        var snapshot = JsonSerializer.Deserialize<HplcMethodResponse>("{\"id\":1,\"name\":\"Old\",\"abbreviation\":\"OLD\",\"columnDesignation\":\"L1\"}", SnapshotJson.Options)!;
        Assert.Equal(HplcTechnique.Hplc, snapshot.Technique);
        Assert.Equal(HplcResultMode.Assay, snapshot.ResultMode);
        Assert.Empty(snapshot.OvenSteps);
    }
}
```
  Add one test to the existing column-service tests file (find it with `git grep -l "ChromatographyColumnService" -- backend/MicroLIMS.Tests`) asserting a column can list a `Gc` equipment as compatible, mirroring that file's existing "Hplc equipment is accepted" test with `Type = EquipmentType.Gc`.

- [ ] **Step 2: Run, expect FAIL.** `--filter "FullyQualifiedName~HplcRunServiceTests|FullyQualifiedName~ChromatographyColumn"`
- [ ] **Step 3: Implement** the changes listed above.
- [ ] **Step 4: Run, expect PASS** (same filter; all existing HPLC run tests must stay green).
- [ ] **Step 5: Commit** `feat(gc): GC runs on GC instruments and columns`.

---

### Task 6: Residual-solvents SST, entry preview and submission (TDD)

**Files:**
- Modify: `backend/MicroLIMS.Application/Services/HplcRunService.cs` (`SaveSstAsync`, `MissingSstEntries`)
- Modify: `backend/MicroLIMS.Application/Services/HplcSampleEntryContext.cs` (`EntryProblem`, new `EvaluateRows`)
- Create: `backend/MicroLIMS.Application/Helpers/HplcResidualSolventEvaluator.cs`
- Modify: `backend/MicroLIMS.Application/Services/HplcRunService.Samples.cs:~306` (preview uses `c.EvaluateRows()`, label by basis)
- Modify: `backend/MicroLIMS.Application/Workflows/TestWorkflow/HplcMethodAssayRecorder.cs` (rows from `c.EvaluateRows()`, RS calculation JSON)
- Modify: `backend/MicroLIMS.Application/Services/HplcRunService.WorkingStandards.cs` (`EligibleQualificationsAsync`: return empty for RS snapshots)
- Create: `backend/MicroLIMS.Tests/UnitTests/HplcRunServiceGcSampleTests.cs` (partial `HplcRunServiceTests`)

**Interfaces — Produces:**
```csharp
public record HplcResidualSolventInput(int AnalyteId, string Name, decimal StandardConcentrationUgPerMl,
    decimal SampleSolutionVolumeMl, decimal StandardMeanResponse, IReadOnlyList<ResidualSolventReplicateInput> Reps, Specification PpmSpec);
public static class HplcResidualSolventEvaluator
{
    // One mean row per solvent, Basis = ResultBasis.Ppm, Unit "ppm"; HplcAssayReading.AssayPercent carries the replicate ppm.
    public static List<HplcAssayRow> Evaluate(IReadOnlyList<HplcResidualSolventInput> solvents);
}
// HplcSampleEntryContext:
public bool IsResidualSolvents => Snapshot.ResultMode == HplcResultMode.ResidualSolvents;
public List<HplcResidualSolventInput> BuildResidualSolventInputs();
public List<HplcAssayRow> EvaluateRows(); // RS -> HplcResidualSolventEvaluator, else HplcSampleAssayEvaluator.Evaluate(BuildAnalyteInputs(), StageRole)
```

**Rules:**
- `SaveSstAsync` (read mode from `JsonSerializer.Deserialize<HplcMethodResponse>(run.MethodSnapshotJson, JsonOptions)`): in RS mode the standard lot is still required and checked with `LotUsability.Check`, but `StandardWeightMg` may be `0` (stored as `null`), and purity/moisture are not required (store the lot's values when present, else null). Assay mode unchanged.
- `MissingSstEntries`: in RS mode skip the `"actual standard weight"` field.
- Stock deduction already skips rows with null `StandardWeightMg` — no change.
- `EntryProblem` in RS mode: replicate weight/response checks unchanged; spec check becomes `Specs.Any(s => s.HplcMethodAnalyteId == analyte.Id && s.ResultBasis == ResultBasis.Ppm)` else `"No residual-solvent specification for {analyte.Name} on this item."`; SST check becomes `sstRow?.MeanResponse is null or <= 0` → `"{analyte.Name}: the system suitability standard values are incomplete."`. Assay mode unchanged.
- RS reported value is always the mean (no individual basis, no amount per unit).
- Preview label: `row.Basis == ResultBasis.Ppm ? "Residual solvent (ppm)" : row.Basis == ResultBasis.MgPerUnit ? "Amount per unit" : "Assay %"`.
- Recorder: `var rows = c.EvaluateRows();` For RS rows, calculation JSON:
  `{ analyte, hplcMethodAnalyteId, quantity = "ResidualSolventPpm", basis = "Mean", standardConcentrationUgPerMl, sampleSolutionVolumeMl, standardMeanResponse, replicates = [{ replicateNo, sampleWeightMg, response, ppm, ppmDisplay }], reportedValue, runCode, sstCode, hplcMethodId, hplcRunId, hplcRunSampleId }`; readings: `Value1 = response`, `ComputedValue = ppm`. Assay rows keep the existing JSON unchanged (look up inputs only when `!c.IsResidualSolvents`).
- Working-standard qualifications are not offered on RS runs: `EligibleQualificationsAsync` returns `new()` when `snapshot.ResultMode == HplcResultMode.ResidualSolvents`.

- [ ] **Step 1: Failing tests** — `HplcRunServiceGcSampleTests.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

// GC residual solvents through the run (spec 2026-10-03 §3.3).
public partial class HplcRunServiceTests
{
    private static async Task<(HplcRunService Service, HplcRunDto Run, TestOrder Order, GcScenario G)> ArrangeRsRunAsync(
        MicroLimsDbContext db, ILabClock clock, bool ppmSpec = true, List<decimal>? stdResponses = null)
    {
        var g = await SeedGcScenarioAsync(db);
        var analyte = await db.HplcMethodAnalytes.FirstAsync(a => a.HplcMethodId == g.Method.Id);
        var def = new TestDefinition
        {
            Code = "RS_TEST", DisplayName = "Residual solvents", SectionId = g.Section.Id,
            WorkflowType = WorkflowType.HplcMethodAssay, EquationType = EquationType.HplcMethodAssay,
            RequiresSystemSuitability = true, HplcMethodId = g.Method.Id, MethodAbbreviation = "RS-01", IsActive = true,
        };
        db.TestDefinitions.Add(def);
        var item = new Item { Code = "ITM-RS", Name = "Tablets", Category = SampleCategory.FinishedProduct, IsActive = true };
        db.Items.Add(item);
        await db.SaveChangesAsync();
        if (ppmSpec)
        {
            db.Specifications.Add(new Specification
            {
                ItemId = item.Id, TestCode = def.Code, ParameterName = "Methanol", HplcMethodAnalyteId = analyte.Id,
                ResultBasis = ResultBasis.Ppm, LimitType = LimitType.NotMoreThan, UpperLimit = 3000m, UpperInclusive = true,
                Unit = "ppm", DisplayOrder = 1,
            });
            await db.SaveChangesAsync();
        }

        // Reuse the assay order helper through a minimal Scenario/AssayFixture pair.
        var scenario = new Scenario
        {
            Section = g.Section, UserId = g.UserId, MobilePhase = null!, MobileEntry = null!, Method = g.Method,
            Equipment = g.Equipment, Column = g.Column, StandardEntry = g.StandardEntry, MobilePhasePrep = null!,
        };
        var fixture = new AssayFixture { Item = item, Definition = def, Analyte = analyte, AssaySpec = null! };
        var order = await AddAssayOrderAsync(db, scenario, fixture, ProductionStageRole.Finished, sampleReplicates: 2);

        var service = TestServiceFactory.HplcRun(db, clock: clock);
        var run = await service.StartRunAsync(GcStart(g), g.UserId);
        var lot = await AddStandardLotAsync(db, g.Section.Id, g.StandardEntry, purity: null, moisturePercent: null);
        await service.SaveSstAsync(run.Id, new SaveSstRequest(new List<SaveSstAnalyteInput> {
            new(run.Sst!.Analytes[0].Id, lot.Id, 0m, stdResponses ?? new List<decimal> { 1000m, 1000m, 1000m }, null, null, null, null, null, null, null) }), g.UserId);
        await service.UploadEvidenceAsync(run.Id, null, HplcEvidenceContext.Sst, HplcEvidenceKind.StandardReport, "std.pdf", "application/pdf", PdfBytes(), g.UserId);
        run = await service.ConfirmSstAsync(run.Id, new ConfirmSstRequest(Password, null), g.UserId, null);
        return (service, run, order, g);
    }

    [Fact]
    public async Task RsSst_NoWeightNoPurity_Passes()
    {
        await using var db = NewDb();
        var (_, clock) = NewClock(SepFirst);
        var (_, run, _, _) = await ArrangeRsRunAsync(db, clock);
        Assert.Equal(HplcSstStatus.Passed, run.Sst!.Status);
    }

    [Fact]
    public async Task RsEntry_PreviewShowsMeanPpmAgainstLimit()
    {
        await using var db = NewDb();
        var (_, clock) = NewClock(SepFirst);
        var (service, run, order, g) = await ArrangeRsRunAsync(db, clock);
        run = await service.AssignSamplesAsync(run.Id, new List<int> { order.Id }, g.UserId);
        var runSampleId = run.Samples.Single().Id;
        var analyteId = (await db.HplcMethodAnalytes.FirstAsync(a => a.HplcMethodId == g.Method.Id)).Id;

        var entry = await service.SaveReplicatesAsync(runSampleId, Replicates(analyteId, (400m, 900m), (400m, 1000m)), g.UserId);

        var row = Assert.Single(entry.Preview);
        Assert.Equal("Residual solvent (ppm)", row.Quantity);
        Assert.Equal(2850m, row.Value);
        Assert.Equal("2850.0 ppm", row.Display);
        Assert.Equal(ResultStatus.WithinLimits, row.Status);
    }

    [Fact]
    public async Task RsEntry_NoPpmSpec_ShowsProblem()
    {
        await using var db = NewDb();
        var (_, clock) = NewClock(SepFirst);
        var (service, run, order, g) = await ArrangeRsRunAsync(db, clock, ppmSpec: false);
        run = await service.AssignSamplesAsync(run.Id, new List<int> { order.Id }, g.UserId);
        var analyteId = (await db.HplcMethodAnalytes.FirstAsync(a => a.HplcMethodId == g.Method.Id)).Id;

        var entry = await service.SaveReplicatesAsync(run.Samples.Single().Id, Replicates(analyteId, (400m, 900m), (400m, 1000m)), g.UserId);

        Assert.Empty(entry.Preview);
        Assert.Equal("No residual-solvent specification for Methanol on this item.", entry.SubmitProblem);
    }

    [Fact]
    public async Task RsEntry_MissingSstMean_ShowsProblem()
    {
        await using var db = NewDb();
        var (_, clock) = NewClock(SepFirst);
        var (service, run, order, g) = await ArrangeRsRunAsync(db, clock);
        run = await service.AssignSamplesAsync(run.Id, new List<int> { order.Id }, g.UserId);
        var sstRow = await db.HplcSstAnalytes.FirstAsync();
        sstRow.MeanResponse = null;
        await db.SaveChangesAsync();
        var analyteId = (await db.HplcMethodAnalytes.FirstAsync(a => a.HplcMethodId == g.Method.Id)).Id;

        var entry = await service.SaveReplicatesAsync(run.Samples.Single().Id, Replicates(analyteId, (400m, 900m), (400m, 1000m)), g.UserId);

        Assert.Equal("Methanol: the system suitability standard values are incomplete.", entry.SubmitProblem);
    }

    [Fact]
    public async Task RsSubmit_StoresPpmResultWithCalculationJson()
    {
        await using var db = NewDb();
        var (_, clock) = NewClock(SepFirst);
        var (service, run, order, g) = await ArrangeRsRunAsync(db, clock);
        run = await service.AssignSamplesAsync(run.Id, new List<int> { order.Id }, g.UserId);
        var runSampleId = run.Samples.Single().Id;
        var analyteId = (await db.HplcMethodAnalytes.FirstAsync(a => a.HplcMethodId == g.Method.Id)).Id;
        await service.SaveReplicatesAsync(runSampleId, Replicates(analyteId, (400m, 900m), (400m, 1000m)), g.UserId);
        await service.UploadEvidenceAsync(run.Id, runSampleId, HplcEvidenceContext.Sample, HplcEvidenceKind.SampleReport, "s.pdf", "application/pdf", PdfBytes(), g.UserId);

        await TestServiceFactory.TestWorkflow(db, clock: clock).SubmitHplcMethodAssayAsync(runSampleId, Password, "ok", g.UserId, "127.0.0.1");

        var result = await db.ParameterResults.Include(p => p.Readings).SingleAsync(p => p.TestOrderId == order.Id && p.IsActive);
        Assert.Equal(2850m, result.ReportedValue);
        Assert.Equal("ppm", result.Unit);
        Assert.Equal(ResultStatus.WithinLimits, result.ComparisonStatus);
        Assert.Contains("\"quantity\":\"ResidualSolventPpm\"", result.CalculationJson);
        Assert.Equal(new[] { 2700m, 3000m }, result.Readings.OrderBy(r => r.Stage).Select(r => r.ComputedValue!.Value).ToArray());
    }
}
```
  Before writing, confirm the exact names used by the existing sample tests: `AssignSamplesAsync`, `SaveReplicatesAsync`, `HplcSampleEntryDto.Preview` / `.SubmitProblem`, `HplcPreviewResultDto.Quantity` / `.Value` / `.Display` / `.Status`, and `HplcEvidenceContext.Sample` / `HplcEvidenceKind.SampleReport` (grep `HplcRunServiceSampleTests.cs` and `HplcRunSubmissionTests.cs`). Adjust only the names, never the asserted values.

- [ ] **Step 2: Run, expect FAIL.** `--filter "FullyQualifiedName~HplcRunServiceTests.Rs"`
- [ ] **Step 3: Implement** — `HplcResidualSolventEvaluator`:

```csharp
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Helpers;

public record HplcResidualSolventInput(int AnalyteId, string Name, decimal StandardConcentrationUgPerMl,
    decimal SampleSolutionVolumeMl, decimal StandardMeanResponse, IReadOnlyList<ResidualSolventReplicateInput> Reps, Specification PpmSpec);

// GC residual solvents: one judged mean-ppm row per solvent (spec 2026-10-03 §3.3). Pure - no I/O.
// HplcAssayReading.AssayPercent carries the replicate ppm (shared row shape with the assay path).
public static class HplcResidualSolventEvaluator
{
    public static List<HplcAssayRow> Evaluate(IReadOnlyList<HplcResidualSolventInput> solvents) =>
        solvents.Select(s =>
        {
            var calc = ResidualSolventCalculator.Calculate(s.StandardConcentrationUgPerMl, s.SampleSolutionVolumeMl, s.StandardMeanResponse, s.Reps);
            var readings = calc.Replicates
                .Select(r => new HplcAssayReading(r.ReplicateNo, s.Reps.First(x => x.ReplicateNo == r.ReplicateNo).Response, r.Ppm))
                .ToList();
            return new HplcAssayRow(s.AnalyteId, s.Name, s.PpmSpec, ResultBasis.Ppm, null,
                calc.MeanPpm, ResidualSolventCalculator.FormatPpm(calc.MeanPpm), "ppm",
                SpecificationEvaluator.Evaluate(s.PpmSpec, calc.MeanPpm), readings);
        }).ToList();
}
```
  Context `BuildResidualSolventInputs()`: per snapshot analyte (ordered by `DisplayOrder`) → `StandardConcentrationUgPerMl!.Value`, `Snapshot.SampleSolutionVolumeMl!.Value`, the SST row's `MeanResponse!.Value`, replicates `(ReplicateNo, ActualWeightMg, response for this analyte)`, the `Ppm` spec. Then the remaining rules above.

- [ ] **Step 4: Run, expect PASS** — then the whole Hplc set: `--filter "FullyQualifiedName~Hplc"` all green.
- [ ] **Step 5: Commit** `feat(gc): residual solvents SST, entry preview and submission`.

---

### Task 7: Full verification and local database migration

- [ ] **Step 1:** `dotnet build backend/MicroLIMS.API --artifacts-path E:/MicroLIMS/rls-tmp/g1-artifacts` → 0 errors, no new warnings in touched files.
- [ ] **Step 2:** `dotnet build backend/MicroLIMS.Tests --artifacts-path E:/MicroLIMS/rls-tmp/g1-artifacts` then `bash .claude/scripts/run-postgres-tests.sh E:/MicroLIMS/rls-tmp/g1-artifacts` → 0 failures. Record the pass/fail/skip counts.
- [ ] **Step 3: Backup** LIMSV2 (password from `appsettings.Development.json`, never printed):
  `"/c/Program Files/PostgreSQL/18/bin/pg_dump.exe" -h localhost -U postgres -Fc -f E:/MicroLIMS/backups/LIMSV2_before_gc_method_fields_20261003.dump LIMSV2`
- [ ] **Step 4: Apply** `dotnet ef database update --project backend/MicroLIMS.Persistence --startup-project backend/MicroLIMS.API --configuration Release`. Verify: `select count(*), min("Technique"), max("Technique") from "HplcMethods";` → every existing row `Technique = 0`.
- [ ] **Step 5: Commit** any remaining changes; report counts and the backup path. Stop — G2 (frontend) starts only after the user reviews G1.

---

## Self-review notes

- Spec §3.1 coverage: technique/mode (T1–T2), column G-codes (unchanged designation field + T5 column check), length m→mm (UI in G2; backend stores mm), film thickness, carrier gas + flow (`FlowRateMlPerMin` reused as carrier flow — noted in entity comment), split, inlet/detector temps, oven steps, headspace, detector enum, optional wavelength, RS analytes + sample volume (T1–T2). §3.2 ppm basis (T3). §3.3 run checks + calculator + recorder (T4–T6). §3.4 menus are G2. §5 audit: method edits reuse the existing audit event; SST confirm and submission reuse existing signatures.
- Deviation from spec wording: no separate `CarrierFlowMlPerMin` column — `FlowRateMlPerMin` already means mL/min and is required on both techniques.
- Permission label change ("Instrument workspaces – operate") is frontend-only → G2.
