# HPLC Chain S1 — Material Master Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A Reagent / Indicator / Reference Standard master that chemical stock lots must reference, plus moisture content on reference-standard lots.

**Architecture:** One new entity `MaterialMasterEntry` (Domain) with EF config (Persistence), one Application service modelled on `ChromatographyColumnService`, endpoints on a new `MaterialMasterController` under `api/masterdata`. `MaterialService.CreateAsync/UpdateAsync` gain the link rule. Frontend: one config page modelled on `ChromatographyColumnsPage`, plus a master picker and MC field in `AddMaterialDialog`.

**Tech Stack:** ASP.NET Core 8, EF Core + Npgsql, xUnit (in-memory + Postgres), React + TS + MUI.

**Spec:** `docs/superpowers/specs/2026-09-29-hplc-chain-design.md` §2 (D9, D10), §3.1

## Global Constraints
- Clean Architecture: Domain has no EF/HTTP; rules live in Application; Persistence only maps; API only routes.
- Keep it simple: no new permission codes in S1 — writes use `PermissionConstants.MasterDataManage`, reads are `[Authorize]` (stock receivers need the picker).
- Nothing hard-deleted; deactivate only. Audit comes from `MicroLimsDbContext.SaveChanges` (no extra code).
- Master link required only for new lots of `MaterialType.Chemical`, `Indicator`, `ReferenceStandard`; existing rows untouched.
- Category ↔ type: `Reagent`↔`Chemical`, `Indicator`↔`Indicator`, `ReferenceStandard`↔`ReferenceStandard`.
- UI: MUI + theme tokens, shared dialogs, no `alert/confirm`, status = text + icon.

## Review Focus
- Editing an **existing unlinked** chemical lot (created before S1) must still save without picking a master entry → `UpdateAsync` only enforces the link when the request carries one or the lot already has one.
- A **deactivated** entry must not be pickable for a new lot, but lots already linked to it must still load and update.
- Entry from **another section** must be refused for a lot (lab separation).
- `MoisturePercent` sent for a non-reference-standard lot must be refused, not silently dropped.
- Lot name must come from the entry (snapshot), so a typo in the typed name can't diverge from the master.

---

### Task 1: Domain + persistence + migration

**Files:**
- Create: `backend/MicroLIMS.Domain/Enums/MaterialMasterCategory.cs`
- Create: `backend/MicroLIMS.Domain/Entities/MaterialMasterEntry.cs`
- Create: `backend/MicroLIMS.Persistence/Configurations/MaterialMasterEntryConfiguration.cs`
- Modify: `backend/MicroLIMS.Domain/Entities/Material.cs` (add 3 members)
- Modify: `backend/MicroLIMS.Persistence/Configurations/MaterialConfiguration.cs`
- Modify: `backend/MicroLIMS.Application/Abstractions/Persistence/IMicroLimsDbContext.cs`, `backend/MicroLIMS.Persistence/DbContext/MicroLimsDbContext.cs` (DbSet)
- Create: migration `AddMaterialMaster`

**Interfaces — Produces:** `MaterialMasterEntry`, `MaterialMasterCategory`, `Material.MaterialMasterEntryId`, `Material.MaterialMasterEntry`, `Material.MoisturePercent`, `IMicroLimsDbContext.MaterialMasterEntries`.

- [ ] **Step 1: Enum**
```csharp
namespace MicroLIMS.Domain.Enums;

public enum MaterialMasterCategory
{
    Reagent,
    Indicator,
    ReferenceStandard
}
```

- [ ] **Step 2: Entity**
```csharp
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

// Defines WHAT a lab chemical is (reagent, indicator or reference standard).
// Lots, quantities and expiry stay in Material (the stock register), which
// references this entry. Deactivated, never deleted.
public class MaterialMasterEntry : IVersionedEntity
{
    public int Id { get; set; }
    public uint Version { get; set; }
    public int SectionId { get; set; }
    public DocumentSection? Section { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public MaterialMasterCategory Category { get; set; }
    public string? Grade { get; set; }
    // Supplier, or pharmacopoeial source for standards (USP, EP, BP, In-house)
    public string? Source { get; set; }
    public MaterialUnit BaseUnit { get; set; }
    public bool IsActive { get; set; } = true;

    // Indicator only (all optional)
    public string? WorkingConcentration { get; set; }
    public string? Solvent { get; set; }
    public decimal? TransitionRangeFrom { get; set; }
    public decimal? TransitionRangeTo { get; set; }
    public string? ColourChange { get; set; }
    public string? IndicatorUse { get; set; }

    public int CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public int LastModifiedByUserId { get; set; }
    public DateTime LastModifiedAt { get; set; }
}
```

- [ ] **Step 3: Material members** — add to `Material.cs` after `Purity`:
```csharp
    // Chemical, Indicator and ReferenceStandard lots reference their master entry (HPLC chain S1).
    public int? MaterialMasterEntryId { get; set; }
    public MaterialMasterEntry? MaterialMasterEntry { get; set; }
    // Reference standards only: moisture content % (0 <= x < 100), used with Purity in assay calculations.
    public decimal? MoisturePercent { get; set; }
```

- [ ] **Step 4: EF config**
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class MaterialMasterEntryConfiguration : IEntityTypeConfiguration<MaterialMasterEntry>
{
    public void Configure(EntityTypeBuilder<MaterialMasterEntry> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Code).IsRequired().HasMaxLength(50);
        builder.Property(e => e.Name).IsRequired().HasMaxLength(200);
        builder.Property(e => e.Grade).HasMaxLength(100);
        builder.Property(e => e.Source).HasMaxLength(150);
        builder.Property(e => e.WorkingConcentration).HasMaxLength(100);
        builder.Property(e => e.Solvent).HasMaxLength(100);
        builder.Property(e => e.ColourChange).HasMaxLength(100);
        builder.Property(e => e.IndicatorUse).HasMaxLength(100);
        builder.Property(e => e.TransitionRangeFrom).HasColumnType("decimal(5,2)");
        builder.Property(e => e.TransitionRangeTo).HasColumnType("decimal(5,2)");
        builder.HasIndex(e => new { e.SectionId, e.Code }).IsUnique();
        builder.HasOne(e => e.Section).WithMany().HasForeignKey(e => e.SectionId).OnDelete(DeleteBehavior.Restrict);
    }
}
```
Match the `Version` concurrency mapping used by `ChromatographyColumnConfiguration` (copy its `Version` line if it has one; if `IVersionedEntity` is mapped globally in the DbContext, add nothing).

In `MaterialConfiguration.Configure` add:
```csharp
        builder.Property(m => m.MoisturePercent).HasColumnType("decimal(6,3)");
        builder.HasOne(m => m.MaterialMasterEntry).WithMany().HasForeignKey(m => m.MaterialMasterEntryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(m => m.MaterialMasterEntryId);
```
DbSets: `DbSet<MaterialMasterEntry> MaterialMasterEntries { get; }` in the interface and `public DbSet<MaterialMasterEntry> MaterialMasterEntries => Set<MaterialMasterEntry>();` in the context.

- [ ] **Step 5: Build + migration**
```bash
cd backend && dotnet build MicroLIMS.sln -c Release
dotnet ef migrations add AddMaterialMaster --project MicroLIMS.Persistence --startup-project MicroLIMS.API --configuration Release
```
Expected: build OK; migration creates `MaterialMasterEntries`, adds `Materials.MaterialMasterEntryId`, `Materials.MoisturePercent`, FK + indexes. Read the generated file: no drops, no unrelated changes.

- [ ] **Step 6: Commit** `feat(inventory): material master entity and stock link columns`

---

### Task 2: MaterialMasterService (TDD)

**Files:**
- Create: `backend/MicroLIMS.Application/Services/MaterialMasterService.cs`
- Create: `backend/MicroLIMS.Application/DTOs/Responses/MaterialMasterEntryResponse.cs`
- Modify: `backend/MicroLIMS.Tests/TestServiceFactory.cs` (add `MaterialMaster(db)`)
- Test: `backend/MicroLIMS.Tests/UnitTests/MaterialMasterServiceTests.cs`

**Interfaces — Consumes:** Task 1 entity. **Produces:**
```csharp
public record SaveMaterialMasterEntryRequest(
    string Code, string Name, MaterialMasterCategory Category, string? Grade, string? Source,
    MaterialUnit BaseUnit, int? SectionId = null,
    string? WorkingConcentration = null, string? Solvent = null,
    decimal? TransitionRangeFrom = null, decimal? TransitionRangeTo = null,
    string? ColourChange = null, string? IndicatorUse = null);

public class MaterialMasterService
{
    Task<List<MaterialMasterEntryResponse>> GetAllAsync(int currentUserId, MaterialMasterCategory? category = null, bool activeOnly = false, CancellationToken ct = default);
    Task<MaterialMasterEntryResponse> CreateAsync(SaveMaterialMasterEntryRequest r, int currentUserId, CancellationToken ct = default);
    Task<MaterialMasterEntryResponse> UpdateAsync(int id, SaveMaterialMasterEntryRequest r, int currentUserId, CancellationToken ct = default);
    Task<MaterialMasterEntryResponse> SetActiveAsync(int id, bool isActive, int currentUserId, CancellationToken ct = default);
}
```
`MaterialMasterEntryResponse`: all entity fields except navigation + `SectionName`, with `static From(MaterialMasterEntry e)`.

- [ ] **Step 1: Failing tests** (same `NewDb()`/`SeedUserAsync` helpers as `MediaProductServiceTests`; seed a `DocumentSection` and make the user a member the way `ChromatographyColumn` tests do — copy their seeding helper):
```csharp
[Fact] public async Task Create_TrimsAndUppercasesCode_AndIsActive()
// Code " naoh-01 " -> "NAOH-01", Name trimmed, IsActive true

[Fact] public async Task Create_DuplicateCodeInSameSection_Throws()
// second create with same code -> InvalidOperationException containing "already exists"

[Fact] public async Task Create_IndicatorFieldsOnNonIndicator_Throws()
// Category Reagent + Solvent "Ethanol" -> InvalidOperationException containing "Indicator fields"

[Fact] public async Task Create_TransitionRangeFromAboveTo_Throws()
// Indicator, From 10, To 8 -> "Transition range"

[Fact] public async Task Update_ChangesFields_AndKeepsCategoryRuleOnExistingLots()
// entry with a linked Chemical lot: changing Category to Indicator -> Throws "linked stock lots"

[Fact] public async Task SetActive_False_HidesFromActiveOnlyList()
```

- [ ] **Step 2: Run, expect FAIL** — `dotnet test backend/MicroLIMS.Tests --filter MaterialMasterServiceTests` → compile errors (type not defined).

- [ ] **Step 3: Implement** — structure exactly like `ChromatographyColumnService` (scope from `IUserSectionScopeService`, `ResolveSectionForCreateAsync`, `RecordVersion.EnsureCurrent(_db, entity)` on update, `TimeProvider`). Validation in one private static method:
```csharp
private static void Validate(SaveMaterialMasterEntryRequest r)
{
    if (string.IsNullOrWhiteSpace(r.Code)) throw new InvalidOperationException("Code is required.");
    if (string.IsNullOrWhiteSpace(r.Name)) throw new InvalidOperationException("Name is required.");
    var hasIndicatorFields = r.WorkingConcentration != null || r.Solvent != null || r.TransitionRangeFrom != null
        || r.TransitionRangeTo != null || r.ColourChange != null || r.IndicatorUse != null;
    if (hasIndicatorFields && r.Category != MaterialMasterCategory.Indicator)
        throw new InvalidOperationException("Indicator fields are only allowed for indicators.");
    if (r.TransitionRangeFrom > r.TransitionRangeTo)
        throw new InvalidOperationException("Transition range 'from' must not be above 'to'.");
}
```
Uniqueness: `AnyAsync(e => e.SectionId == sectionId && e.Code == code && e.Id != id)`. Category change on update: refuse when `_db.Materials.AnyAsync(m => m.MaterialMasterEntryId == id)` and category differs → `"This entry has linked stock lots; its category can't change."`. Access check for update/SetActive: load entity, then require `scope == null || scope.Contains(entity.SectionId)`, else `NotFoundException`.

- [ ] **Step 4: Run, expect PASS.**
- [ ] **Step 5: Commit** `feat(inventory): material master service`

---

### Task 3: Stock lot link + moisture (TDD)

**Files:**
- Modify: `backend/MicroLIMS.Application/Services/MaterialService.cs` (`SaveMaterialRequest`, `CreateAsync`, `UpdateAsync`)
- Modify: `backend/MicroLIMS.API/Controllers/MaterialController.cs` (`SaveMaterialHttpRequest` + mapping)
- Modify: `backend/MicroLIMS.Application/DTOs/Responses/InventoryResponses.cs` (`MaterialResponse`: `MaterialMasterEntryId`, `MaterialMasterEntryCode`, `MoisturePercent`)
- Test: `backend/MicroLIMS.Tests/InventoryTests/MaterialMasterLinkTests.cs`

**Interfaces — Consumes:** Task 1/2. **Produces:** `SaveMaterialRequest` gains trailing `int? MaterialMasterEntryId = null, decimal? MoisturePercent = null` (optional, so existing callers compile). `public static bool RequiresMasterEntry(MaterialType t)` on `MaterialService`.

- [ ] **Step 1: Failing tests**
```csharp
[Fact] Create_Chemical_WithoutEntry_Throws                  // "Choose the material master entry"
[Fact] Create_Chemical_WithIndicatorEntry_Throws            // "category"
[Fact] Create_Chemical_WithInactiveEntry_Throws             // "inactive"
[Fact] Create_Chemical_WithOtherSectionEntry_Throws         // "another laboratory"
[Fact] Create_Chemical_TakesNameAndCodeFromEntry            // typed MaterialName ignored
[Fact] Create_DehydratedMedia_NoEntryNeeded                 // unchanged behaviour
[Fact] Create_ReferenceStandard_MoistureOutOfRange_Throws   // 100 -> "Moisture"
[Fact] Create_Chemical_WithMoisture_Throws                  // "only allowed for reference standards"
[Fact] Update_LegacyUnlinkedChemical_SavesWithoutEntry      // Review Focus 1
[Fact] Update_LinkedLot_WhoseEntryWasDeactivated_StillSaves // Review Focus 2
```

- [ ] **Step 2: Run, expect FAIL.**

- [ ] **Step 3: Implement** — in `MaterialService`:
```csharp
public static bool RequiresMasterEntry(MaterialType t) =>
    t is MaterialType.Chemical or MaterialType.Indicator or MaterialType.ReferenceStandard;

private static MaterialMasterCategory CategoryFor(MaterialType t) => t switch
{
    MaterialType.Indicator => MaterialMasterCategory.Indicator,
    MaterialType.ReferenceStandard => MaterialMasterCategory.ReferenceStandard,
    _ => MaterialMasterCategory.Reagent
};

// Loads and checks the entry; allowInactive is true only when the lot already points at it.
private async Task<MaterialMasterEntry> ResolveMasterEntryAsync(int entryId, MaterialType type, int sectionId, bool allowInactive)
{
    var entry = await _db.MaterialMasterEntries.FirstOrDefaultAsync(e => e.Id == entryId)
        ?? throw new InvalidOperationException($"Material master entry {entryId} not found.");
    if (entry.SectionId != sectionId) throw new InvalidOperationException("That master entry belongs to another laboratory.");
    if (!entry.IsActive && !allowInactive) throw new InvalidOperationException($"Master entry \"{entry.Code}\" is inactive.");
    if (entry.Category != CategoryFor(type)) throw new InvalidOperationException($"Master entry \"{entry.Code}\" is a {entry.Category}, not a {CategoryFor(type)}.");
    return entry;
}

public static void ValidateMoisture(MaterialType type, decimal? moisture)
{
    if (!moisture.HasValue) return;
    if (type != MaterialType.ReferenceStandard) throw new InvalidOperationException("Moisture content is only allowed for reference standards.");
    if (moisture.Value < 0m || moisture.Value >= 100m) throw new InvalidOperationException("Moisture content must be at least 0 and below 100.");
}
```
`CreateAsync`: after resolving `sectionId`, if `RequiresMasterEntry(type)`: require `r.MaterialMasterEntryId` (`"Choose the material master entry for this lot."`), resolve with `allowInactive: false`, set `materialName = entry.Name; code = entry.Code;`. Call `ValidateMoisture`. Persist both new fields.
`UpdateAsync`: if `r.MaterialMasterEntryId` is set → resolve with `allowInactive: r.MaterialMasterEntryId == entity.MaterialMasterEntryId`, snapshot name/code; if null and entity has one → keep entity's link (don't clear); if null and none → legacy row, no check. `ValidateMoisture`, persist.
Controller: add the two fields to `SaveMaterialHttpRequest` and pass them through.

- [ ] **Step 4: Run tests, expect PASS; run whole `InventoryTests` folder, expect no regressions.**
- [ ] **Step 5: Commit** `feat(inventory): chemical lots reference the material master; moisture on reference standards`

---

### Task 4: API endpoints

**Files:**
- Create: `backend/MicroLIMS.API/Controllers/MaterialMasterController.cs`
- Modify: `backend/MicroLIMS.API/Extensions/ServiceCollectionExtensions.cs` (`services.AddScoped<MaterialMasterService>();` next to `MaterialService`)

- [ ] **Step 1: Controller**
```csharp
[ApiController]
[Route("api/masterdata/material-masters")]
[Authorize]
public class MaterialMasterController : ControllerBase
{
    private readonly MaterialMasterService _service;
    public MaterialMasterController(MaterialMasterService service) => _service = service;
    private int CurrentUserId => int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] MaterialMasterCategory? category, [FromQuery] bool activeOnly = false) =>
        Ok(ApiResponse<object>.Ok(await _service.GetAllAsync(CurrentUserId, category, activeOnly)));

    [Authorize(Policy = PermissionConstants.MasterDataManage)]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SaveMaterialMasterEntryRequest r) =>
        Ok(ApiResponse<object>.Ok(await _service.CreateAsync(r, CurrentUserId)));

    [Authorize(Policy = PermissionConstants.MasterDataManage)]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] SaveMaterialMasterEntryRequest r) =>
        Ok(ApiResponse<object>.Ok(await _service.UpdateAsync(id, r, CurrentUserId)));

    [Authorize(Policy = PermissionConstants.MasterDataManage)]
    [HttpPut("{id:int}/active")]
    public async Task<IActionResult> SetActive(int id, [FromQuery] bool value) =>
        Ok(ApiResponse<object>.Ok(await _service.SetActiveAsync(id, value, CurrentUserId)));
}
```
If other controllers carry the record `Version` for optimistic concurrency in the request (check `UpdateChromatographyColumnRequest` callers / `RecordVersion`), follow the same mechanism.

- [ ] **Step 2:** `dotnet build MicroLIMS.sln -c Release` → OK.
- [ ] **Step 3: Commit** `feat(api): material master endpoints`

---

### Task 5: Frontend (agy, ui-ux-pro-max skill)

**Files:**
- Create: `frontend/src/modules/laboratoryConfiguration/masterDataSimple/MaterialMasterPage.tsx`
- Create: `frontend/src/modules/laboratoryConfiguration/masterDataSimple/services/MaterialMasterService.ts`
- Modify: `frontend/src/routes/AppRoutes.tsx`, `routes.ts`, `menuConfig.ts` (entry "Reagents & Standards" beside "Chromatography Columns", path `/laboratory-configuration/material-master`)
- Modify: `frontend/src/modules/inventory/materials/components/AddMaterialDialog.tsx`, `types/materialTypes.ts`, `services/MaterialService.ts`

- [ ] **Step 1:** Page modelled on `ChromatographyColumnsPage.tsx`: table (code, name, category chip with icon, grade, source, unit, status text+icon), category filter, Add/Edit dialog; indicator fields shown only for Indicator; Activate/Deactivate through the shared confirm dialog. Backend error messages shown as-is.
- [ ] **Step 2:** `AddMaterialDialog`: when type is Chemical/Indicator/ReferenceStandard, show an entry picker (active entries of the matching category, `GET api/masterdata/material-masters?category=…&activeOnly=true`); name + code fields become read-only from the entry. Moisture % field only for ReferenceStandard, next to Purity. Existing unlinked lots open without a picker value and still save.
- [ ] **Step 3:** `cd frontend && npm run build && npx eslint src --max-warnings=0` (or the repo's lint script) → clean.
- [ ] **Step 4: Commit** `feat(ui): reagents & standards master, stock lot picker and moisture`

---

### Task 6: Verify and hand back

- [ ] Backup LIMSV2: `"/c/Program Files/PostgreSQL/18/bin/pg_dump.exe" -Fc -f E:/MicroLIMS/db-backups/LIMSV2_before_material_master_20260929.dump …` (same connection style as earlier backups), then apply: `dotnet ef database update --project MicroLIMS.Persistence --startup-project MicroLIMS.API --configuration Release`.
- [ ] Full suite with Postgres: `bash .claude/scripts/run-postgres-tests.sh <temp artifacts dir>` after `dotnet build -c Release` into that artifacts path → 0 failures.
- [ ] Opus reviews the whole diff `git diff main...HEAD` (not just new files) and runs `/simplify`.
- [ ] Update memory `hplc-workspace-chain-decisions` with S1 commit ids. **Stop — report to the user before S2.**
