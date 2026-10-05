# I1 — ICP Method Master (backend) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** An `IcpMethod` master (elements, calibration levels, optional blank/ICV/CCV checks, sample-prep defaults) that a new `IcpMethodAssay` Test Master workflow and its specification rows can point at.

**Architecture:** New `IcpMethod` + `IcpMethodElement` tables copying the `HplcMethod` master pattern exactly (edit in place with a reason, one audit event per edit whose single change is the whole method JSON before → after, deactivate never delete, section-scoped). `TestDefinition.IcpMethodId` and `Specification.IcpMethodElementId` mirror `HplcMethodId` / `HplcMethodAnalyteId`. No run, calibration or sample code here (that is I2).

**Tech Stack:** ASP.NET Core, EF Core (PostgreSQL), xUnit (EF InMemory for unit tests, `MICROLIMS_TEST_POSTGRES` for integration tests).

**Spec:** `docs/superpowers/specs/2026-10-03-icp-gc-workspaces-design.md` §4.1 (decisions D2, D3, D4, D10).

## Global Constraints

- Clean Architecture: entities/enums in Domain, service + validation in Application, EF config + migration in Persistence, controller in API.
- Builds/tests while the API runs: `dotnet test backend/MicroLIMS.Tests --artifacts-path E:/MicroLIMS/rls-tmp/i1-build`. All build output and temp files on `E:`.
- Migrations: `dotnet ef migrations add <Name> --project backend/MicroLIMS.Persistence --startup-project backend/MicroLIMS.API --configuration Release`. Do NOT apply to LIMSV2 before Task 6 (backup first).
- Writes need `PermissionConstants.MasterDataManage`; reads any authenticated user (D10).
- Enum values are appended, never inserted (stored as ints).
- Every user-facing error is an `InvalidOperationException` with the exact message given below (tests assert the text).
- Copy idioms from `HplcMethodService` / `HplcMethodController` / `HplcMethodConfiguration`; do not refactor them.

## Review Focus

1. Editing a method and dropping an element that a specification row uses → refused, nothing saved — Task 2 `Update_RemovingElementUsedBySpec_Throws`.
2. Editing a method and sending a different `Mode` → refused, never silently converted — Task 2 `Update_ChangingMode_Throws`.
3. ICV switched on with the same standard entry as the calibration standard (not a second source) → refused — Task 2 `Create_IcvSameAsCalibrationStandard_Throws`.
4. An optional check switched off but its values still sent (e.g. `RequireCcv=false`, `CcvNominalMgPerL=5`) → refused, so stale values never sit on a method — Task 2 `Create_CheckOffWithValues_Throws`.
5. A mineral-assay element given a µg/g spec, or an impurities element given mg/unit → refused — Task 5 `Spec_MineralAssay_RejectsMgPerKg`, `Spec_Impurities_RejectsMgPerUnit`.

---

### Task 1: Domain, EF configuration and migration

**Files:**
- Create: `backend/MicroLIMS.Domain/Enums/IcpMethodMode.cs`
- Create: `backend/MicroLIMS.Domain/Entities/IcpMethod.cs`
- Modify: `backend/MicroLIMS.Domain/Enums/WorkflowType.cs` (append `IcpMethodAssay`)
- Modify: `backend/MicroLIMS.Domain/Enums/EquationType.cs` (append `IcpMethodAssay`)
- Modify: `backend/MicroLIMS.Domain/Entities/TestDefinition.cs` (after `HplcMethod`)
- Modify: `backend/MicroLIMS.Domain/Entities/Specification.cs` (after `HplcMethodAnalyte`)
- Create: `backend/MicroLIMS.Persistence/Configurations/IcpMethodConfiguration.cs`
- Modify: `IMicroLimsDbContext` + `MicroLimsDbContext` (DbSets `IcpMethods`, `IcpMethodElements`)
- Migration: `AddIcpMethodMaster`

**Interfaces — Produces:** `IcpMethod`, `IcpMethodElement`, `IcpMethodMode`, `WorkflowType.IcpMethodAssay`, `EquationType.IcpMethodAssay`, `TestDefinition.IcpMethodId`, `Specification.IcpMethodElementId`, DbSets `IcpMethods`, `IcpMethodElements`.

- [ ] **Step 1: Enum**

```csharp
namespace MicroLIMS.Domain.Enums;

// ICP workspace (spec 2026-10-03 §4.1, D2).
public enum IcpMethodMode { MineralAssay = 0, ElementalImpurities = 1 }
```

Append to `WorkflowType` (after `Titration`): `IcpMethodAssay // ICP workspace - assay against an IcpMethod master (spec 2026-10-03 §4.1)`. Same line appended to `EquationType`.

- [ ] **Step 2: Entities** (`IcpMethod.cs`)

```csharp
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

// ICP-OES method master (spec 2026-10-03 §4.1). Same lifecycle as HplcMethod:
// edited in place with a reason, runs snapshot it, deactivated never deleted.
public class IcpMethod : IVersionedEntity
{
    public int Id { get; set; }
    public uint Version { get; set; }
    public int SectionId { get; set; }
    public DocumentSection? Section { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Abbreviation { get; set; } = string.Empty;   // upper-case, used in run codes
    public DateTime EffectiveDate { get; set; }
    public bool IsActive { get; set; } = true;
    public IcpMethodMode Mode { get; set; }                    // fixed at creation

    // Calibration (D3: summary only - one r per element at run time)
    public string StandardLevelsMgPerL { get; set; } = string.Empty; // normalised "0.1, 0.5, 1, 3, 6"
    public int CalibrationStandardEntryId { get; set; }
    public MaterialMasterEntry? CalibrationStandardEntry { get; set; }
    public decimal MinCorrelation { get; set; }                // e.g. 0.999

    // Optional checks (D4, all off by default)
    public bool RequireBlank { get; set; }
    public decimal? BlankMaxMgPerL { get; set; }
    public bool RequireIcv { get; set; }
    public int? IcvStandardEntryId { get; set; }               // second source
    public MaterialMasterEntry? IcvStandardEntry { get; set; }
    public decimal? IcvNominalMgPerL { get; set; }
    public decimal? IcvRecoveryLowPercent { get; set; }
    public decimal? IcvRecoveryHighPercent { get; set; }
    public bool RequireCcv { get; set; }
    public decimal? CcvNominalMgPerL { get; set; }
    public decimal? CcvRecoveryLowPercent { get; set; }
    public decimal? CcvRecoveryHighPercent { get; set; }
    public int MaxCalibrationAgeHours { get; set; } = 24;

    // Sample-prep defaults, prefilled on entry (I2)
    public decimal SampleVolumeMl { get; set; }
    public decimal DilutionFactor { get; set; } = 1m;

    public List<IcpMethodElement> Elements { get; set; } = new();

    public int CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public int LastModifiedByUserId { get; set; }
    public DateTime LastModifiedAt { get; set; }
}

// Kept by Id on update so specification rows stay linked.
public class IcpMethodElement
{
    public int Id { get; set; }
    public int IcpMethodId { get; set; }
    public IcpMethod? IcpMethod { get; set; }
    public int DisplayOrder { get; set; }
    public string Symbol { get; set; } = string.Empty;         // "Zn", "Ca"
    public decimal WavelengthNm { get; set; }
    public AnalyteView View { get; set; }
    public decimal ConversionFactor { get; set; } = 1m;        // element -> reported form
}
```

`TestDefinition`: `// ICP workspace - IcpMethodAssay tests point at an ICP method master` + `public int? IcpMethodId { get; set; }` + `public IcpMethod? IcpMethod { get; set; }`.
`Specification`: `// ICP workspace - IcpMethodAssay specification rows are keyed by method element + basis.` + `public int? IcpMethodElementId { get; set; }` + `public IcpMethodElement? IcpMethodElement { get; set; }`.

- [ ] **Step 3: EF configuration** — copy `HplcMethodConfiguration` conventions: `HasIndex(SectionId, Abbreviation).IsUnique()`; strings `Name` 200, `Abbreviation` 20, `StandardLevelsMgPerL` 200, `Symbol` 3; decimals `HasPrecision(18, 6)` (`MinCorrelation` `HasPrecision(8, 6)`); `Version` row-version the same way `HplcMethod` does; Elements cascade-delete from method; FKs to `MaterialMasterEntries` (calibration + ICV) `Restrict`; `TestDefinition.IcpMethodId` → `IcpMethods` `Restrict`; `Specification.IcpMethodElementId` → `IcpMethodElements` `Restrict`; `HasIndex(IcpMethodId, DisplayOrder)` on elements.

- [ ] **Step 4: Migration**

Run: `dotnet ef migrations add AddIcpMethodMaster --project backend/MicroLIMS.Persistence --startup-project backend/MicroLIMS.API --configuration Release`
Check the generated Up/Down: two new tables, two nullable FK columns, no other table touched. Run `dotnet build backend/MicroLIMS.Tests --artifacts-path E:/MicroLIMS/rls-tmp/i1-build` → 0 errors.

- [ ] **Step 5: Commit** — `feat(icp): ICP method master schema`

---

### Task 2: IcpMethodService (TDD)

**Files:**
- Create: `backend/MicroLIMS.Application/Services/IcpMethodService.cs`
- Create: `backend/MicroLIMS.Application/DTOs/Responses/IcpMethodResponse.cs`
- Modify: `backend/MicroLIMS.Tests/TestServiceFactory.cs` (add `IcpMethod(db)` like `HplcMethod(db)`)
- Test: `backend/MicroLIMS.Tests/UnitTests/IcpMethodServiceTests.cs`

**Interfaces — Consumes:** Task 1 types; `IUserSectionScopeService`, `IAuditEventService`, `TimeProvider`, `CalibrationStandardLevelsHelper.ParseAndValidate`, `SnapshotJson.Options`, `RecordVersion.EnsureCurrent` (all existing).
**Produces:**

```csharp
public record IcpElementInput(int? Id, string Symbol, decimal WavelengthNm, AnalyteView View, decimal ConversionFactor = 1m);

public record SaveIcpMethodRequest(
    string Name, string Abbreviation, DateTime EffectiveDate, IcpMethodMode Mode,
    string StandardLevelsMgPerL, int CalibrationStandardEntryId, decimal MinCorrelation,
    decimal SampleVolumeMl, List<IcpElementInput> Elements,
    decimal DilutionFactor = 1m, int MaxCalibrationAgeHours = 24,
    bool RequireBlank = false, decimal? BlankMaxMgPerL = null,
    bool RequireIcv = false, int? IcvStandardEntryId = null, decimal? IcvNominalMgPerL = null,
    decimal? IcvRecoveryLowPercent = null, decimal? IcvRecoveryHighPercent = null,
    bool RequireCcv = false, decimal? CcvNominalMgPerL = null,
    decimal? CcvRecoveryLowPercent = null, decimal? CcvRecoveryHighPercent = null,
    int? SectionId = null, string? Reason = null);   // Reason required on update, ignored on create

public record IcpMethodListItem(int Id, string Name, string Abbreviation, IcpMethodMode Mode, bool IsActive,
    int ElementCount, string SectionName, DateTime LastModifiedAt);
public record IcpMethodHistoryEntry(DateTime At, string UserName, string Action, string? Reason, string? BeforeJson, string? AfterJson);

public class IcpMethodService   // ctor(IMicroLimsDbContext, IUserSectionScopeService, IAuditEventService, TimeProvider? = null) like HplcMethodService
{
    Task<List<IcpMethodListItem>> GetAllAsync(int currentUserId, bool activeOnly = false, CancellationToken ct = default);
    Task<IcpMethodResponse> GetByIdAsync(int id, int currentUserId, CancellationToken ct = default);
    Task<IcpMethodResponse> CreateAsync(SaveIcpMethodRequest r, int currentUserId, CancellationToken ct = default);
    Task<IcpMethodResponse> UpdateAsync(int id, SaveIcpMethodRequest r, int currentUserId, CancellationToken ct = default);
    Task<IcpMethodResponse> SetActiveAsync(int id, bool isActive, string reason, int currentUserId, CancellationToken ct = default);
    Task<List<IcpMethodHistoryEntry>> GetHistoryAsync(int id, int currentUserId, CancellationToken ct = default);
}
```

`IcpMethodResponse` (class, `static From(IcpMethod m)`): every entity field, plus `SectionName`, `CalibrationStandardEntryCode`, `IcvStandardEntryCode`, `Version`, `Elements` as `IcpMethodElementResponse(int Id, int DisplayOrder, string Symbol, decimal WavelengthNm, AnalyteView View, decimal ConversionFactor)` ordered by `DisplayOrder`.

Audit: action codes `IcpMethod.Created` / `IcpMethod.Updated` / `IcpMethod.Deactivated` / `IcpMethod.Activated`, record type `IcpMethod`, `AuditActionCategory.Configuration`, single change `new AuditFieldChange("Method", before, after)` with the response JSON — exactly as `HplcMethodService`.

**Validation (in this order, exact messages):**

| Rule | Message |
|---|---|
| Name blank | `Method name is required.` |
| Name > 200 | `Method name cannot exceed 200 characters.` |
| Abbreviation blank | `Abbreviation is required.` |
| Abbreviation (trim, upper) not `^[A-Z0-9-]{2,20}$` | `Abbreviation must be 2-20 uppercase letters, digits or hyphens.` |
| Levels | `CalibrationStandardLevelsHelper.ParseAndValidate` (its own messages); store `Normalized` |
| `MinCorrelation` not in (0, 1] | `Minimum correlation must be greater than 0 and at most 1.` |
| `SampleVolumeMl` ≤ 0 | `Sample volume must be greater than zero.` |
| `DilutionFactor` < 1 | `Dilution factor must be 1 or more.` |
| `MaxCalibrationAgeHours` not 1..168 | `Calibration age must be between 1 and 168 hours.` |
| Calibration standard entry: missing / other section / not `ReferenceStandard` / inactive (unless already on this method) | `Calibration standard not found.` / `Calibration standard "{Code}" belongs to another laboratory.` / `Calibration standard "{Code}" must be a reference standard.` / `Calibration standard "{Code}" is inactive.` |
| `RequireBlank` and `BlankMaxMgPerL` not > 0 | `Blank limit (mg/L) is required when the blank check is on.` |
| `RequireIcv`: entry null, nominal not > 0, low/high not > 0, low ≥ high | `ICV standard, nominal (mg/L) and recovery limits are required when the ICV check is on.` |
| `RequireIcv` and ICV entry == calibration entry | `The ICV standard must be a second source, not the calibration standard.` |
| ICV entry checks (as calibration standard, prefix `ICV standard`) | `ICV standard not found.` etc. |
| `RequireCcv`: nominal not > 0, low/high not > 0, low ≥ high | `CCV nominal (mg/L) and recovery limits are required when the CCV check is on.` |
| any check off but one of its values sent | `Blank, ICV and CCV values are only used when that check is on.` |
| Elements empty | `At least one element is required.` |
| Symbol blank or > 3 chars | `Element symbol must be 1-3 letters.` (normalise to `Zn` casing: first upper, rest lower) |
| Symbol repeated | `Element "{Symbol}" is listed more than once.` |
| `WavelengthNm` ≤ 0 | `{Symbol}: wavelength must be greater than zero.` |
| `ConversionFactor` ≤ 0 | `{Symbol}: conversion factor must be greater than zero.` |
| duplicate abbreviation in section (create/update) | `An ICP method abbreviated "{ABBR}" already exists.` |
| Update: reason blank / > 500 | same as `HplcMethodService.ValidateReason` |
| Update: `Mode` differs | `The mode can't be changed after the method is created.` |
| Update: element dropped while a spec uses it | `Element "{Symbol}" is used by specifications; it can't be removed.` |
| Update: element `Id` not on this method | `Element {Id} does not belong to this method.` |

Elements on update: keep rows whose `Id` is sent (update fields), add rows without `Id`, remove the rest (after the spec check). `DisplayOrder` = list position starting at 1. Not found / other section → `NotFoundException($"ICP method {id} not found.")` like HPLC.

- [ ] **Step 1: Write the failing tests** (copy `SeedAsync` / `AddEntryAsync` / `NewDb` helpers from `HplcMethodServiceTests`)

```csharp
public class IcpMethodServiceTests
{
    // NewDb, SeedAsync, AddEntryAsync copied from HplcMethodServiceTests

    private static SaveIcpMethodRequest Req(int calStdId, params IcpElementInput[] elements) => new(
        Name: "Minerals by ICP-OES", Abbreviation: "min-icp", EffectiveDate: new DateTime(2026, 10, 1),
        Mode: IcpMethodMode.MineralAssay, StandardLevelsMgPerL: "6, 0.1, 1, 0.5, 3",
        CalibrationStandardEntryId: calStdId, MinCorrelation: 0.999m, SampleVolumeMl: 50m,
        Elements: elements.Length > 0 ? elements.ToList() : new() { new(null, "zn", 213.857m, AnalyteView.Axial) });

    [Fact]
    public async Task Create_ValidMineralMethod_NormalisesAndAudits()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var std = await AddEntryAsync(db, section.Id, "RS-ICP-1");
        var svc = TestServiceFactory.IcpMethod(db);

        var m = await svc.CreateAsync(Req(std.Id), userId);

        Assert.Equal("MIN-ICP", m.Abbreviation);
        Assert.Equal("0.1, 0.5, 1, 3, 6", m.StandardLevelsMgPerL);
        Assert.Equal("Zn", m.Elements.Single().Symbol);
        Assert.Equal(1m, m.Elements.Single().ConversionFactor);
        Assert.Equal(24, m.MaxCalibrationAgeHours);
        Assert.Single(db.AuditLogs.Where(l => l.ActionCode == "IcpMethod.Created"));
    }

    [Fact]
    public async Task Create_IcvSameAsCalibrationStandard_Throws()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var std = await AddEntryAsync(db, section.Id, "RS-ICP-1");
        var r = Req(std.Id) with { RequireIcv = true, IcvStandardEntryId = std.Id, IcvNominalMgPerL = 1m, IcvRecoveryLowPercent = 90m, IcvRecoveryHighPercent = 110m };
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => TestServiceFactory.IcpMethod(db).CreateAsync(r, userId));
        Assert.Equal("The ICV standard must be a second source, not the calibration standard.", ex.Message);
    }

    [Fact]
    public async Task Create_CheckOffWithValues_Throws()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var std = await AddEntryAsync(db, section.Id, "RS-ICP-1");
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            TestServiceFactory.IcpMethod(db).CreateAsync(Req(std.Id) with { CcvNominalMgPerL = 5m }, userId));
        Assert.Equal("Blank, ICV and CCV values are only used when that check is on.", ex.Message);
    }

    [Fact]
    public async Task Update_ChangingMode_Throws()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var std = await AddEntryAsync(db, section.Id, "RS-ICP-1");
        var svc = TestServiceFactory.IcpMethod(db);
        var m = await svc.CreateAsync(Req(std.Id), userId);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.UpdateAsync(m.Id, Req(std.Id) with { Mode = IcpMethodMode.ElementalImpurities, Reason = "x" }, userId));
        Assert.Equal("The mode can't be changed after the method is created.", ex.Message);
    }

    [Fact]
    public async Task Update_KeepsElementIds_AndRemovingElementUsedBySpec_Throws()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var std = await AddEntryAsync(db, section.Id, "RS-ICP-1");
        var svc = TestServiceFactory.IcpMethod(db);
        var m = await svc.CreateAsync(Req(std.Id, new(null, "Zn", 213.857m, AnalyteView.Axial), new(null, "Ca", 317.933m, AnalyteView.Radial)), userId);
        var zn = m.Elements.Single(e => e.Symbol == "Zn");
        var ca = m.Elements.Single(e => e.Symbol == "Ca");

        // keep both, change Zn wavelength -> same ids
        var kept = await svc.UpdateAsync(m.Id, Req(std.Id, new(zn.Id, "Zn", 206.200m, AnalyteView.Axial), new(ca.Id, "Ca", 317.933m, AnalyteView.Radial)) with { Reason = "wl" }, userId);
        Assert.Equal(zn.Id, kept.Elements.Single(e => e.Symbol == "Zn").Id);

        db.Specifications.Add(new Specification { ItemId = 1, TestCode = "ICP-T", ParameterName = "Ca", IcpMethodElementId = ca.Id, LimitType = LimitType.Range, LowerLimit = 90, UpperLimit = 110 });
        await db.SaveChangesAsync();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.UpdateAsync(m.Id, Req(std.Id, new(zn.Id, "Zn", 206.200m, AnalyteView.Axial)) with { Reason = "drop Ca" }, userId));
        Assert.Equal("Element \"Ca\" is used by specifications; it can't be removed.", ex.Message);
    }
}
```

Plus one `[Theory]` per remaining validation-table row asserting its exact message (levels "1" → `At least two standard levels are required.`; MinCorrelation 1.2; SampleVolumeMl 0; DilutionFactor 0.5; calibration age 0; blank on without limit; ICV on missing nominal; CCV low ≥ high; no elements; symbol "Abcd"; duplicate "zn"/"Zn"; wavelength 0; duplicate abbreviation; update without reason), and `SetActive_Deactivate_AuditsWithReason`, `GetHistory_ReturnsBeforeAfter`, `GetAll_ActiveOnlyFilters`.

- [ ] **Step 2: Run to verify they fail** — `dotnet test backend/MicroLIMS.Tests --artifacts-path E:/MicroLIMS/rls-tmp/i1-build --filter IcpMethodServiceTests` → compile errors / FAIL.
- [ ] **Step 3: Implement `IcpMethodService` + response + factory** following `HplcMethodService` structure (`ValidateAsync` → `ApplyFields` → `ApplyElements`).
- [ ] **Step 4: Run the filter again** → PASS.
- [ ] **Step 5: Commit** — `feat(icp): ICP method master service`

---

### Task 3: Controller, DI and authorization matrix

**Files:**
- Create: `backend/MicroLIMS.API/Controllers/IcpMethodController.cs` — route `api/masterdata/icp-methods`, copy `HplcMethodController` (GetAll `?activeOnly=`, GetById, GetHistory, Create/Update/SetActive under `[Authorize(Policy = PermissionConstants.MasterDataManage)]`).
- Modify: DI registration where `HplcMethodService` is registered (`services.AddScoped<IcpMethodService>()`).
- Modify: the authorization matrix snapshot used by `backend/MicroLIMS.Tests/ArchitectureTests/AuthorizationMatrixTests.cs` (regenerate with `MICROLIMS_WRITE_AUTH_MATRIX=1`, then review the diff: only the six new `IcpMethod` endpoints may appear).

- [ ] **Step 1:** Run `AuthorizationMatrixTests` → FAIL (new endpoints not in matrix).
- [ ] **Step 2:** Add controller + DI; regenerate matrix; inspect diff.
- [ ] **Step 3:** Run `AuthorizationMatrixTests` → PASS.
- [ ] **Step 4: Commit** — `feat(icp): ICP method master API`

---

### Task 4: Test Master — `IcpMethodAssay` workflow

**Files:**
- Modify: `backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs` (next to the `HplcMethodAssay` block, ~line 296-336) + its create/update request record (`IcpMethodId` optional param, default null) + response (`IcpMethodId`)
- Test: `backend/MicroLIMS.Tests/UnitTests/IcpMethodTestMasterTests.cs`

Rules (exact messages):
- `EquationType.IcpMethodAssay` ⇔ `WorkflowType.IcpMethodAssay`: `Workflow type must be IcpMethodAssay when equation type is IcpMethodAssay.` / `Equation type must be IcpMethodAssay when workflow type is IcpMethodAssay.`
- IcpMethodAssay without `IcpMethodId` → `ICP method is required for ICP method assay tests.`; not found → `ICP method not found.`; other section → `The ICP method belongs to another laboratory.`; inactive → `The ICP method is inactive.`
- `IcpMethodId` on any other workflow → `ICP method is only allowed for ICP method assay tests.`
- `RequiresSystemSuitability` must be false for IcpMethodAssay (calibration is the gate) → `ICP method assay tests use the run calibration, not system suitability.`
- `IcpMethodAssay` tests carry no analytes / Cal* fields of their own, same as HplcMethodAssay (reuse the existing "no analytes" guard by adding the workflow to it).

- [ ] **Step 1:** Tests `TestDef_IcpAssay_LinksMethod`, `TestDef_IcpAssay_WithoutMethod_Throws`, `TestDef_IcpAssay_InactiveMethod_Throws`, `TestDef_Titration_WithIcpMethod_Throws`, `TestDef_IcpAssay_RequiresSst_Throws` (arrange like `HplcMethodAssayTestMasterTests`).
- [ ] **Step 2:** Run → FAIL. **Step 3:** Implement. **Step 4:** Run → PASS.
- [ ] **Step 5: Commit** — `feat(icp): IcpMethodAssay tests on the Test Master`

---

### Task 5: Specifications keyed by ICP method element

**Files:**
- Modify: `backend/MicroLIMS.Application/Services/SpecificationService.cs` (new `else if (testDef?.WorkflowType == WorkflowType.IcpMethodAssay)` branch beside the HplcMethodAssay one; hoist an element+basis duplicate check next to the existing analyte+basis one) + `CreateSpecificationRequest`/update request (`IcpMethodElementId` optional, default null) + spec response
- Test: `backend/MicroLIMS.Tests/UnitTests/IcpMethodSpecificationTests.cs`

Rules (exact messages):
- `IcpMethodElementId` required → `Method element is required for ICP method assay specifications.`
- element must belong to the test's `IcpMethodId` → `That element does not belong to the method of test '{TestCode}'.`
- Mode `MineralAssay`: basis `MgPerUnit` or `PercentLabelClaim` → else `Result basis must be mg per unit or % of label claim for mineral assay.`; both need `LabelClaim > 0` and `LabelClaimUnit` → `Mineral assay specifications need a label claim and its unit.`
- Mode `ElementalImpurities`: basis `MgPerKg` (= µg/g) → else `Result basis must be µg/g (MgPerKg) for elemental impurities.`; label claim not allowed → `Label claim is not used for elemental impurities.`
- `TestAnalyteId`, `SampleMatrix` must be null and `ConversionFactor` 1.0 (the factor lives on the method element) → `Test analyte, sample matrix and conversion factor are not used for ICP method assay specifications.`
- Limit types Range / NotMoreThan / NotLessThan / TargetWithTolerance only → `Limit type '{LimitType}' is not supported for ICP method assay specifications.`
- duplicate element + basis + stage → `A specification for this element and basis already exists.`
- `IcpMethodElementId` on any other workflow → `Method element is only allowed for ICP method assay specifications.`

- [ ] **Step 1:** Tests `Spec_MineralAssay_MgPerUnitAndPercent_Accepted`, `Spec_MineralAssay_RejectsMgPerKg`, `Spec_Impurities_RejectsMgPerUnit`, `Spec_Impurities_RejectsLabelClaim`, `Spec_ElementFromOtherMethod_Throws`, `Spec_DuplicateElementBasis_Throws`, `Spec_ConversionFactorNotOne_Throws`, `Spec_HplcTest_WithIcpElement_Throws`.
- [ ] **Step 2:** Run → FAIL. **Step 3:** Implement. **Step 4:** Run → PASS (also rerun `HplcMethodAssayTestMasterTests`, `ElementalAssayResultTests` — unchanged behaviour).
- [ ] **Step 5: Commit** — `feat(icp): specifications keyed by ICP method element`

---

### Task 6: Apply migration locally and full suite

- [ ] **Step 1:** Backup: `pg_dump -Fc -f E:/MicroLIMS/backups/LIMSV2_before_icp_method_master_<yyyymmdd>.dump LIMSV2` (password from `appsettings.Development.json`, never printed).
- [ ] **Step 2:** Postgres integration test `IcpMethodPostgresIntegrationTests.CreateUpdateDeactivate_RoundTrip` (create method with 2 elements + ICV on, update keeping ids, link a test + spec, deactivate) using the existing Postgres fixture.
- [ ] **Step 3:** `dotnet build backend/MicroLIMS.Tests --artifacts-path E:/MicroLIMS/rls-tmp/i1-build` then `bash .claude/scripts/run-postgres-tests.sh E:/MicroLIMS/rls-tmp/i1-build` → 0 failed, 0 skipped.
- [ ] **Step 4:** Apply: `dotnet ef database update --project backend/MicroLIMS.Persistence --startup-project backend/MicroLIMS.API --configuration Release` against LIMSV2; verify `\d "IcpMethods"`.
- [ ] **Step 5: Commit** — `test(icp): ICP method master Postgres round trip`

---

## Frontend (I1b, agy after the backend lands)

Separate contract file `E:/MicroLIMS/rls-tmp/i1-icp-frontend-contract.md` written from the final DTOs: ICP Methods page + dialog (elements table, calibration, optional checks with toggles, sample-prep defaults, history dialog), Test Master `IcpMethodAssay` option with method picker, specification dialog element + basis fields per mode. agy loads ui-ux-pro-max then frontend-design (spec §6).
