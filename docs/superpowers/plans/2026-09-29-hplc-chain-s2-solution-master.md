# HPLC Chain S2 — Solution Master Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A Solution master for Mobile Phases, Diluents and Titrants: recipe components from the Material master, shelf life, pH requirement, instructions, and the titrant standardization settings.

**Architecture:** Two new entities, `SolutionMaster` and `SolutionComponent` (Domain), with EF configs (Persistence). One Application service `SolutionMasterService` modelled on `MaterialMasterService` (S1), whose edits require a reason recorded through `IAuditEventService.RecordUserEventAsync`. Endpoints on `SolutionMasterController` under `api/masterdata/solution-masters`. One frontend config page.

**Tech Stack:** ASP.NET Core, EF Core + Npgsql, xUnit, React + TS + MUI.

**Spec:** `docs/superpowers/specs/2026-09-29-hplc-chain-design.md` §2 (D7, D8), §3.2

## Global Constraints
- Clean Architecture: Domain has no EF; rules in Application; Persistence maps; controller routes.
- Keep it simple: no versions (D7). Edits are in place with a required reason (1–500 chars). Field changes are captured automatically by `SaveChanges`; the reason goes in one `RecordUserEventAsync` event (`actionCode "SolutionMaster.Updated"`, `AuditActionCategory.Configuration`, recordType `"SolutionMaster"`, `entityId` = id).
- Writes use `PermissionConstants.MasterDataManage`; reads `[Authorize]`.
- Deactivate, never delete. Section-scoped like S1.
- Components reference **active** Material master entries of the **same section**. No free text.
- Titrant fields only for `Type == Titrant`, rejected otherwise. Titrant validation below.
- UI: MUI + theme tokens, shared dialogs, no `alert/confirm`, status = text + icon.

## Review Focus
- Saving an **existing** solution whose component entry was **later deactivated** must still work if the component is unchanged (deactivation only blocks new picks) → validation allows an inactive entry that the solution already uses.
- A titrant in `AgainstVolumetricSolution` mode pointing at **itself**, or at a non-titrant, must be refused.
- Changing `Type` away from Titrant must clear nothing silently; titrant fields left on the request are refused ("only allowed for titrants").
- Update without a reason → refused, nothing saved.
- A component list with the **same entry twice** → refused.

---

### Task 1: Domain + persistence + migration

**Files:**
- Create: `backend/MicroLIMS.Domain/Enums/SolutionType.cs`, `ShelfLifeUnit.cs`, `SolutionComponentUnit.cs`, `TitrantStrengthUnit.cs`, `StandardizationMode.cs`
- Create: `backend/MicroLIMS.Domain/Entities/SolutionMaster.cs`, `SolutionComponent.cs`
- Create: `backend/MicroLIMS.Persistence/Configurations/SolutionMasterConfiguration.cs`, `SolutionComponentConfiguration.cs`
- Modify: `IMicroLimsDbContext.cs`, `MicroLimsDbContext.cs` (DbSets `SolutionMasters`, `SolutionComponents`)
- Modify: `backend/MicroLIMS.Application/Services/UserReferenceRegistry.cs` (CreatedBy/LastModifiedBy entries, same pattern S1 used)
- Create: migration `AddSolutionMaster`

- [ ] **Step 1: Enums** (one file each, namespace `MicroLIMS.Domain.Enums`)
```csharp
public enum SolutionType { MobilePhase, Diluent, Titrant }
public enum ShelfLifeUnit { Hours, Days }
public enum SolutionComponentUnit { Gram, Milligram, Milliliter, Liter, PercentVolume, Parts }
public enum TitrantStrengthUnit { Normal, Molar }
public enum StandardizationMode { PrimaryStandard, AgainstVolumetricSolution }
```

- [ ] **Step 2: Entities**
```csharp
namespace MicroLIMS.Domain.Entities;

// Recipe for a mobile phase, diluent or titrant (HPLC chain S2). Edited in
// place with a reason; each preparation stores its own snapshot (D7).
public class SolutionMaster : IVersionedEntity
{
    public int Id { get; set; }
    public uint Version { get; set; }
    public int SectionId { get; set; }
    public DocumentSection? Section { get; set; }
    public string Name { get; set; } = string.Empty;
    public SolutionType Type { get; set; }
    public int ShelfLifeValue { get; set; }
    public ShelfLifeUnit ShelfLifeUnit { get; set; }
    public string StorageCondition { get; set; } = string.Empty;
    public decimal FinalVolumeMl { get; set; }
    public decimal? PhTarget { get; set; }
    public decimal? PhTolerance { get; set; }
    public int? PhAdjustingEntryId { get; set; }
    public MaterialMasterEntry? PhAdjustingEntry { get; set; }
    public string Instructions { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public List<SolutionComponent> Components { get; set; } = new();

    // Titrant only (USP Volumetric Solutions)
    public decimal? NominalStrength { get; set; }
    public TitrantStrengthUnit? StrengthUnit { get; set; }
    public StandardizationMode? StandardizationMode { get; set; }
    public int? StandardEntryId { get; set; }            // PrimaryStandard: the primary standard
    public MaterialMasterEntry? StandardEntry { get; set; }
    public decimal? EquivalenceMgPerMl { get; set; }     // PrimaryStandard: mg of standard per mL of nominal titrant
    public int? ReferenceSolutionId { get; set; }        // AgainstVolumetricSolution: the reference titrant
    public SolutionMaster? ReferenceSolution { get; set; }
    public bool BlankRequired { get; set; }
    public int? ReplicateCount { get; set; }
    public decimal? FactorMin { get; set; }
    public decimal? FactorMax { get; set; }
    public decimal? MaxRsdPercent { get; set; }
    public int? ValidityDays { get; set; }               // 0 = restandardize before each use

    public int CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public int LastModifiedByUserId { get; set; }
    public DateTime LastModifiedAt { get; set; }
}

public class SolutionComponent
{
    public int Id { get; set; }
    public int SolutionMasterId { get; set; }
    public SolutionMaster? SolutionMaster { get; set; }
    public int Order { get; set; }
    public int MaterialMasterEntryId { get; set; }
    public MaterialMasterEntry? MaterialMasterEntry { get; set; }
    public decimal Quantity { get; set; }
    public SolutionComponentUnit Unit { get; set; }
}
```

- [ ] **Step 3: EF configs** — `SolutionMaster`: Name required 200, StorageCondition 200, Instructions 4000; decimals `decimal(10,3)` (FinalVolumeMl, EquivalenceMgPerMl, NominalStrength `decimal(10,5)`, factors `decimal(8,5)`, pH `decimal(4,2)`, RSD `decimal(6,3)`); unique index `(SectionId, Name)`; FKs Section, PhAdjustingEntry, StandardEntry, ReferenceSolution all `Restrict`; `HasMany(Components).WithOne(SolutionMaster).OnDelete(Cascade)`. `SolutionComponent`: Quantity `decimal(12,4)`, FK MaterialMasterEntry `Restrict`, index `(SolutionMasterId, Order)`. Version mapping as S1's `MaterialMasterEntryConfiguration`.
- [ ] **Step 4:** `dotnet build … -c Release --artifacts-path <art>`; `dotnet ef migrations add AddSolutionMaster --project backend/MicroLIMS.Persistence --startup-project backend/MicroLIMS.API --configuration Release`. Read it: only the two new tables + FKs/indexes.
- [ ] **Step 5: Commit** `feat(masters): solution master entities`

---

### Task 2: SolutionMasterService (TDD)

**Files:**
- Create: `backend/MicroLIMS.Application/Services/SolutionMasterService.cs`
- Create: `backend/MicroLIMS.Application/DTOs/Responses/SolutionMasterResponse.cs`
- Modify: `backend/MicroLIMS.Tests/TestServiceFactory.cs` (`SolutionMaster(db)`, wiring `AuditEventService` the way `MediaProduct(db)` does)
- Test: `backend/MicroLIMS.Tests/UnitTests/SolutionMasterServiceTests.cs`

**Interfaces — Produces:**
```csharp
public record SolutionComponentInput(int MaterialMasterEntryId, decimal Quantity, SolutionComponentUnit Unit);

public record SaveSolutionMasterRequest(
    string Name, SolutionType Type, int ShelfLifeValue, ShelfLifeUnit ShelfLifeUnit, string StorageCondition,
    decimal FinalVolumeMl, string Instructions, List<SolutionComponentInput> Components,
    decimal? PhTarget = null, decimal? PhTolerance = null, int? PhAdjustingEntryId = null, int? SectionId = null,
    decimal? NominalStrength = null, TitrantStrengthUnit? StrengthUnit = null, StandardizationMode? StandardizationMode = null,
    int? StandardEntryId = null, decimal? EquivalenceMgPerMl = null, int? ReferenceSolutionId = null,
    bool BlankRequired = false, int? ReplicateCount = null, decimal? FactorMin = null, decimal? FactorMax = null,
    decimal? MaxRsdPercent = null, int? ValidityDays = null,
    string? Reason = null);   // required on update, ignored on create

public class SolutionMasterService
{
    Task<List<SolutionMasterResponse>> GetAllAsync(int currentUserId, SolutionType? type = null, bool activeOnly = false, CancellationToken ct = default);
    Task<SolutionMasterResponse> GetByIdAsync(int id, int currentUserId, CancellationToken ct = default);
    Task<SolutionMasterResponse> CreateAsync(SaveSolutionMasterRequest r, int currentUserId, CancellationToken ct = default);
    Task<SolutionMasterResponse> UpdateAsync(int id, SaveSolutionMasterRequest r, int currentUserId, CancellationToken ct = default);
    Task<SolutionMasterResponse> SetActiveAsync(int id, bool isActive, string reason, int currentUserId, CancellationToken ct = default);
}
```
`SolutionMasterResponse`: all scalar fields + `SectionName`, `PhAdjustingEntryCode`, `StandardEntryCode`, `ReferenceSolutionName`, and `Components: List<SolutionComponentResponse(Id, Order, MaterialMasterEntryId, EntryCode, EntryName, Quantity, Unit)>` ordered by `Order`.

- [ ] **Step 1: Failing tests** (seed a section, a member user, and Material master entries directly in the in-memory db):
```csharp
[Fact] Create_MobilePhase_WithComponents_OrdersThem                 // Order = 1..n as given
[Fact] Create_NoComponents_Throws                                   // "at least one component"
[Fact] Create_DuplicateComponentEntry_Throws                        // "listed twice"
[Fact] Create_ComponentFromOtherSection_Throws                      // "another laboratory"
[Fact] Create_InactiveComponentEntry_Throws                         // "inactive"
[Fact] Create_ShelfLifeZero_Throws; Create_FinalVolumeZero_Throws   // "greater than zero"
[Fact] Create_PhToleranceWithoutTarget_Throws                       // "pH target"
[Fact] Create_TitrantFieldsOnDiluent_Throws                         // "only allowed for titrants"
[Fact] Create_Titrant_PrimaryStandard_RequiresStandardAndEquivalence
[Fact] Create_Titrant_PrimaryStandard_StandardMustBeReferenceStandardOrReagent // Indicator entry -> throws
[Fact] Create_Titrant_AgainstVs_ReferenceMustBeActiveTitrant        // Diluent id -> throws
[Fact] Create_Titrant_FactorMinAboveMax_Throws
[Fact] Create_Titrant_ReplicateCountBelowOne_Throws
[Fact] Update_WithoutReason_Throws_AndNothingSaved
[Fact] Update_ReplacesComponents_AndRecordsReasonEvent               // AuditLogs has ActionCode "SolutionMaster.Updated" with Reason
[Fact] Update_KeepsComponentWhoseEntryWasDeactivated                 // Review Focus 1
[Fact] Update_Titrant_ReferenceToItself_Throws                       // Review Focus 2
[Fact] SetActive_RequiresReason
```

- [ ] **Step 2: Run, expect FAIL** (compile).

- [ ] **Step 3: Implement.** Mirror `MaterialMasterService` for scope/section/version. Validation split into two private methods:
```csharp
private async Task ValidateCommonAsync(SaveSolutionMasterRequest r, int sectionId, IReadOnlySet<int> alreadyUsedEntryIds, CancellationToken ct)
// Name required; ShelfLifeValue > 0; FinalVolumeMl > 0; StorageCondition + Instructions required;
// Components.Count >= 1; every Quantity > 0; no duplicate MaterialMasterEntryId ("listed twice");
// each entry exists, same section ("another laboratory"), active unless in alreadyUsedEntryIds ("inactive");
// PhTolerance/PhAdjustingEntryId require PhTarget ("pH target"); PhTarget 0..14; PhTolerance > 0.

private async Task ValidateTitrantAsync(SaveSolutionMasterRequest r, int sectionId, int? selfId, CancellationToken ct)
// Non-titrant: any titrant field set (NominalStrength, StrengthUnit, StandardizationMode, StandardEntryId,
//   EquivalenceMgPerMl, ReferenceSolutionId, BlankRequired, ReplicateCount, FactorMin, FactorMax, MaxRsdPercent, ValidityDays)
//   -> "Standardization settings are only allowed for titrants."
// Titrant: NominalStrength > 0 + StrengthUnit; StandardizationMode required; ReplicateCount >= 1;
//   FactorMin/FactorMax both > 0 and Min <= Max; MaxRsdPercent > 0; ValidityDays >= 0.
//   PrimaryStandard: StandardEntryId (same section, category ReferenceStandard or Reagent) + EquivalenceMgPerMl > 0; ReferenceSolutionId must be null.
//   AgainstVolumetricSolution: ReferenceSolutionId (same section, Type Titrant, active, != selfId) ; StandardEntryId/Equivalence must be null.
```
Update: load with `Components`, `RecordVersion.EnsureCurrent`, require `Reason` (trimmed, 1–500), validate (alreadyUsed = current component entry ids + current PhAdjusting/Standard entry ids), replace `Components` (clear + add with `Order = index + 1`), stamp modified, `SaveChangesAsync`, then `RecordUserEventAsync("SolutionMaster.Updated", AuditActionCategory.Configuration, "SolutionMaster", reason: reason, entityId: id.ToString())` (check `MediaProductService` for whether that call needs its own `SaveChangesAsync`). `SetActiveAsync` same pattern with action codes `"SolutionMaster.Deactivated"` / `"SolutionMaster.Activated"`.

- [ ] **Step 4: Run, expect PASS.**
- [ ] **Step 5: Commit** `feat(masters): solution master service`

---

### Task 3: API

**Files:** Create `backend/MicroLIMS.API/Controllers/SolutionMasterController.cs`; modify `ServiceCollectionExtensions.cs` (`AddScoped<SolutionMasterService>()`); regenerate `backend/MicroLIMS.Tests/ArchitectureTests/authorization-matrix.txt` with `MICROLIMS_WRITE_AUTH_MATRIX=1` (as S1 did) and check the new rows only.

```csharp
[ApiController]
[Route("api/masterdata/solution-masters")]
[Authorize]
public class SolutionMasterController : ControllerBase
{
    // GET  ?type=&activeOnly=          -> GetAllAsync
    // GET  {id:int}                    -> GetByIdAsync
    // POST                              [MasterDataManage] -> CreateAsync
    // PUT  {id:int}                     [MasterDataManage] -> UpdateAsync
    // PUT  {id:int}/active?value=       [MasterDataManage] body { reason } -> SetActiveAsync
}
public record SetActiveRequest(string Reason);
```
Same `CurrentUserId` / `ApiResponse<object>.Ok` style and version handling as `MaterialMasterController`.

- [ ] Build; run `SolutionMasterServiceTests` + `ArchitectureTests`; **Commit** `feat(api): solution master endpoints`

---

### Task 4: Frontend (agy, ui-ux-pro-max skill)

**Files:**
- Create: `frontend/src/modules/laboratoryConfiguration/masterDataSimple/SolutionMasterPage.tsx`, `services/SolutionMasterService.ts`
- Modify: `AppRoutes.tsx`, `routes.ts`, `menuConfig.ts` ("Solutions" beside "Reagents & Standards", path `/laboratory-configuration/solution-master`)

- [ ] **Step 1:** List page modelled on `MaterialMasterPage.tsx`: name, type chip (text + icon), shelf life, final volume, component count, status; filters by type/status/section.
- [ ] **Step 2:** Add/Edit dialog, sections: General (name, type, shelf life value + unit, storage, final volume, instructions multiline); pH (target, tolerance, adjusting reagent picker = Reagent entries); Components (ordered rows: entry picker from active Material master entries of the section, quantity, unit; add/remove/move up/down); Titrant (only when type = Titrant: strength + unit, mode radio, then either standard entry picker + equivalence mg/mL, or reference titrant picker; blank required switch; replicates; factor min/max; max RSD; validity days with helper "0 = restandardize before each use"). On save of an existing record open the shared reason dialog; send `reason`. Activate/Deactivate also asks for a reason.
- [ ] **Step 3:** `npm run build` + `npx eslint <changed files> --max-warnings=0` clean.
- [ ] **Step 4: Commit** `feat(ui): solution master page`

---

### Task 5: Verify and hand back
- [ ] Backup LIMSV2 (`LIMSV2_before_solution_master_20260929.dump`), `dotnet ef database update … --configuration Release`.
- [ ] Full Postgres suite `bash .claude/scripts/run-postgres-tests.sh <art> -c Release` → 0 failures.
- [ ] Opus reviews whole diff; frontend build re-run. Update memory. **Stop — report before S3.**
