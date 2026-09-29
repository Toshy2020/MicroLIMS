# HPLC Chain S4 — Solution Preparation Area Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Analysts prepare mobile phases, diluents and titrants from the Solution master against real stock lots; on signed completion the stock is deducted, a code (MP/DL/VS) and expiry are set, all in one save.

**Architecture:** Aggregate `SolutionPreparation` (+ components, status history) in Domain. `SolutionPreparationService` (Application) owns start → edit → complete/cancel/discard; stock goes through the existing `MaterialService.ConsumeAsync`; codes through a small `SolutionPreparationCode` helper (same "highest + 1 over issued codes" approach as `SystemSuitabilityRunCode`, protected by a unique index). A `LotUsability` helper decides which lots can be picked. An API-layer `BackgroundService` calls `ExpireDueAsync` every 5 minutes. New permission `Solutions.Prepare`.

**Tech Stack:** ASP.NET Core, EF Core + Npgsql, xUnit, React + TS + MUI.

**Spec:** `docs/superpowers/specs/2026-09-29-hplc-chain-design.md` §4 (titrant standardization is S5, not here)

## Global Constraints
- Clean Architecture; keep it simple; only what this plan says. Frontend files ≤ ~350 lines.
- Backend owns usability, expiry, codes, status. Nothing hard-deleted.
- **Simplifications vs spec (approved direction "keep it simple"):** no `CodeSequences` table — codes use max+1 over issued codes with a unique index on `Code`; a concurrent clash fails the completion with "Another preparation took that code at the same moment — sign again." and nothing is saved. Quantity used is entered **in the lot's own unit** (no unit conversion).
- Codes (lab-local date via `ILabClock`): Mobile Phase `MP-{ABBR} {nn}/{MM}/{yyyy}` (series per method abbreviation), Diluent `DL-{nn}/{MM}/{yyyy}`, Titrant `VS-{nn}/{MM}/{yyyy}`. `nn` two digits, restarts each lab-local calendar year.
- Expiry `ExpiresAt = PreparedAt + shelf life` (UTC). Any read treats `Prepared && ExpiresAt <= now` as `Expired`.
- Completion is one `SaveChangesAsync` (consume + code + status + signature link); any exception → nothing persisted, preparation stays `InProgress`.
- Signature: `ElectronicSignatureService.SignAsync(userId, password, SignatureMeaning.PreparationConfirmed, "SolutionPreparation", prep.Id, comment, ip)`.
- Permission `Solutions.Prepare` (new) for all preparation writes; reads `[Authorize]`. Seeded like migration `20260924192621_LabSeparationPrivileges` + `DbSeeder`, granted to SystemAdministrator, SectionHead, Analyst.

## Review Focus
- Two analysts completing preparations of the **same series at the same moment** → one succeeds, the other gets the clash message and can sign again; no duplicate code, no stock deducted for the failed one.
- A lot that **expires between picking and signing** → completion refused naming the component and lot.
- **Year boundary**: first preparation on lab-local 1 January gets `01` even if the last one of December was `37`; UTC midnight ≠ lab midnight (lab is UTC+3), so use `ILabClock.ToLabLocal`.
- A Mobile Phase started for a method that **doesn't list** that mobile phase → refused.
- Opening a Prepared record **after its expiry time but before the worker ran** → shows Expired and is not offered as available.

---

### Task 1: Domain + persistence + permission + migration

**Files:**
- Create enum `backend/MicroLIMS.Domain/Enums/SolutionPreparationStatus.cs`: `{ InProgress, Prepared, Expired, Discarded, Cancelled }`
- Create `backend/MicroLIMS.Domain/Entities/SolutionPreparation.cs`, `SolutionPreparationComponent.cs`, `SolutionPreparationStatusHistory.cs`
- Configs + DbSets (`SolutionPreparations`, `SolutionPreparationComponents`, `SolutionPreparationStatusHistory`); `UserReferenceRegistry` entries
- `PermissionConstants.SolutionsPrepare = "Solutions.Prepare"` (+ its lists/seeder, following how `LabSeparation` permissions were added)
- Migration `AddSolutionPreparation` (schema + the permission/grant SQL block copied from `LabSeparationPrivileges`, roles SystemAdministrator, SectionHead, Analyst — check the RoleType int values in `RoleType.cs`)

```csharp
public class SolutionPreparation : IVersionedEntity
{
    public int Id { get; set; }
    public uint Version { get; set; }
    public int SectionId { get; set; }
    public DocumentSection? Section { get; set; }
    public string? Code { get; set; }                     // set at completion
    public int SolutionMasterId { get; set; }
    public SolutionMaster? SolutionMaster { get; set; }
    public SolutionType Type { get; set; }                // copied from the master at start
    public string RecipeSnapshotJson { get; set; } = "{}";// SolutionMasterResponse at start (D7)
    public int? HplcMethodId { get; set; }                // Mobile Phase only
    public HplcMethod? HplcMethod { get; set; }
    public SolutionPreparationStatus Status { get; set; }
    public int StartedByUserId { get; set; }
    public DateTime StartedAt { get; set; }
    public int? PreparedByUserId { get; set; }
    public DateTime? PreparedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public decimal? FinalVolumeMl { get; set; }
    public decimal? MeasuredPh { get; set; }
    public int? SignatureId { get; set; }
    public ElectronicSignature? Signature { get; set; }
    public List<SolutionPreparationComponent> Components { get; set; } = new();
    public List<SolutionPreparationStatusHistory> StatusHistory { get; set; } = new();
}

public class SolutionPreparationComponent
{
    public int Id { get; set; }
    public int SolutionPreparationId { get; set; }
    public SolutionPreparation? SolutionPreparation { get; set; }
    public int Order { get; set; }
    public int MaterialMasterEntryId { get; set; }
    public string EntryCode { get; set; } = string.Empty;   // snapshot
    public string EntryName { get; set; } = string.Empty;   // snapshot
    public decimal RecipeQuantity { get; set; }
    public SolutionComponentUnit RecipeUnit { get; set; }
    public int? MaterialId { get; set; }                    // chosen lot
    public Material? Material { get; set; }
    public decimal? QuantityUsed { get; set; }              // in the lot's own unit
}

public class SolutionPreparationStatusHistory
{
    public int Id { get; set; }
    public int SolutionPreparationId { get; set; }
    public SolutionPreparationStatus? FromStatus { get; set; }
    public SolutionPreparationStatus ToStatus { get; set; }
    public int? ChangedByUserId { get; set; }               // null = automatic expiry
    public DateTime ChangedAt { get; set; }
    public string? Reason { get; set; }
}
```
Config: `Code` max 50, **unique filtered index** (`HasFilter("\"Code\" IS NOT NULL")`); `RecipeSnapshotJson` `jsonb` if other snapshot columns use jsonb (check `CalculationJson` mapping), else text; decimals `decimal(12,4)`, pH `decimal(4,2)`; index `(Status, ExpiresAt)`; FKs Restrict except children Cascade.
- [ ] Build, add migration, read it. **Commit** `feat(preparation): solution preparation entities and permission`

---

### Task 2: Helpers — `LotUsability` and `SolutionPreparationCode` (TDD, pure)

**Files:** `backend/MicroLIMS.Application/Helpers/LotUsability.cs`, `SolutionPreparationCode.cs`; tests `backend/MicroLIMS.Tests/UnitTests/LotUsabilityTests.cs`, `SolutionPreparationCodeTests.cs`.

```csharp
public record LotCheck(bool Usable, string? Reason);

public static class LotUsability
{
    // today = lab-local date. requiredQuantity null = only expiry/link/stock > 0 are checked.
    public static LotCheck Check(Material lot, int masterEntryId, decimal? requiredQuantity, DateOnly today)
    {
        if (lot.MaterialMasterEntryId != masterEntryId) return new(false, "Lot is not linked to this reagent.");
        if (lot.ExpiryDate.HasValue && DateOnly.FromDateTime(lot.ExpiryDate.Value) < today) return new(false, $"Expired on {lot.ExpiryDate:yyyy-MM-dd}.");
        if (lot.QuantityRemaining <= 0) return new(false, "No stock left.");
        if (requiredQuantity.HasValue && lot.QuantityRemaining < requiredQuantity.Value)
            return new(false, $"Only {lot.QuantityRemaining} {lot.Unit} left.");
        return new(true, null);
    }
}

public static class SolutionPreparationCode
{
    // head: "MP-VIT-C " | "DL-" | "VS-" ; labLocal: lab-local time of completion
    public static string Head(SolutionType type, string? methodAbbreviation);
    public static Task<string> NextAsync(IQueryable<string> issuedCodes, string head, DateTime labLocal, CancellationToken ct = default);
    // parse "{head}{nn}/{MM}/{yyyy}" for codes ending "/{yyyy}", take max nn, return next as D2.
    // Same in-memory fallback (try ToListAsync, catch InvalidOperationException) as SystemSuitabilityRunCode.
}
```
- [ ] Tests: Check → linked/expired (yesterday vs today boundary)/zero/insufficient/ok; Head for each type (MP needs abbreviation, throws without); NextAsync → first of year `01`, continues `03` after `02`, ignores other year, ignores other head (`MP-VIT-C ` vs `MP-VIT-B `), `DL-` series separate from `VS-`, formats `MP-VIT-C 01/09/2026`.
- [ ] FAIL → implement → PASS. **Commit** `feat(preparation): lot usability and preparation code helpers`

---

### Task 3: SolutionPreparationService (TDD)

**Files:** `backend/MicroLIMS.Application/Services/SolutionPreparationService.cs`, `DTOs/Responses/SolutionPreparationResponse.cs`; `TestServiceFactory.SolutionPreparation(db, clock?)`; tests `backend/MicroLIMS.Tests/UnitTests/SolutionPreparationServiceTests.cs`.

```csharp
public record StartPreparationRequest(int SolutionMasterId, int? HplcMethodId);
public record PreparationComponentInput(int ComponentId, int? MaterialId, decimal? QuantityUsed);
public record SavePreparationRequest(List<PreparationComponentInput> Components, decimal? FinalVolumeMl, decimal? MeasuredPh);
public record CompletePreparationRequest(string Password, string? Comment);
public record ReasonRequest(string Reason);
public record LotOption(int MaterialId, string BatchNumber, DateTime? ExpiryDate, decimal QuantityRemaining, MaterialUnit Unit, bool Usable, string? Reason);

public class SolutionPreparationService
{
    Task<List<SolutionPreparationListItem>> GetAllAsync(int userId, SolutionPreparationStatus? status, SolutionType? type, CancellationToken ct = default);
    Task<SolutionPreparationResponse> GetByIdAsync(int id, int userId, CancellationToken ct = default);
    Task<SolutionPreparationResponse> StartAsync(StartPreparationRequest r, int userId, CancellationToken ct = default);
    Task<SolutionPreparationResponse> SaveAsync(int id, SavePreparationRequest r, int userId, CancellationToken ct = default);
    Task<List<LotOption>> GetLotOptionsAsync(int id, int componentId, int userId, CancellationToken ct = default);
    Task<SolutionPreparationResponse> CompleteAsync(int id, CompletePreparationRequest r, int userId, string? ip, CancellationToken ct = default);
    Task<SolutionPreparationResponse> CancelAsync(int id, string reason, int userId, CancellationToken ct = default);
    Task<SolutionPreparationResponse> DiscardAsync(int id, string reason, int userId, CancellationToken ct = default);
    Task<List<SolutionPreparationListItem>> GetAvailableAsync(int userId, int solutionMasterId, int? hplcMethodId, CancellationToken ct = default); // Prepared + unexpired; S6 uses it
    Task<List<SolutionPreparationListItem>> GetByLotAsync(int materialId, int userId, CancellationToken ct = default);          // lot -> preparations trace
    Task<int> ExpireDueAsync(CancellationToken ct = default);   // worker; returns rows expired
}
```
Response: all fields + `EffectiveStatus` (computed: Prepared past expiry → Expired), solution name, method abbreviation, user names, components (with lot batch + unit), status history. List item: id, code, type, solution name, method abbr, effective status, preparedAt, expiresAt, preparedBy.

Rules:
- Start: solution active, in caller's sections; MobilePhase → `HplcMethodId` required, method active, and the method's `MobilePhases` contains this solution ("That method does not use this mobile phase."); Diluent/Titrant → `HplcMethodId` must be null. Section = solution's section. Snapshot = serialized `SolutionMasterResponse` (camelCase). Components copied from the master with `Order`. Status InProgress + history row.
- Save: InProgress only; only started-by user or users with section access (keep: section access); each `ComponentId` must belong to this prep; `MaterialId` lot must pass `LotUsability.Check(lot, component.MaterialMasterEntryId, quantityUsed, today)` and belong to the same section; `QuantityUsed > 0`; `FinalVolumeMl > 0`; `MeasuredPh` 0–14.
- Complete (one save): InProgress; every component has lot + quantity; final volume set; if snapshot has PhTarget: MeasuredPh required and within ± tolerance (tolerance null = must be given, no range check) — else "Measured pH {x} is outside {t} ± {tol}."; re-check every lot with `LotUsability` (message names component + batch); for each component `await _materials.ConsumeAsync(lot.Id, lot.MaterialType, quantityUsed, userId)` (it only mutates tracked entities — confirm it does not call `SaveChangesAsync`; if it does, stop and report instead of working around it); signature via `SignAsync` (check whether it saves; if it saves on its own, sign FIRST — a failed password then leaves everything untouched — and link `SignatureId`); code = `SolutionPreparationCode.NextAsync(_db.SolutionPreparations.Where(p => p.Code != null).Select(p => p.Code!), head, clock.ToLabLocal(now))`; PreparedBy/At, ExpiresAt, Status Prepared, history; `SaveChangesAsync`. Catch `DbUpdateException` whose inner is a unique violation on Code → throw the clash message (after detaching nothing — the request scope is discarded).
- Cancel: InProgress, reason 1–500 → Cancelled, no stock. Discard: effective status Prepared, reason → Discarded, stock not restored. Expired/Discarded/Cancelled are final ("This preparation is closed.").
- ExpireDueAsync: all `Prepared && ExpiresAt <= now` → Expired, history with `ChangedByUserId = null`, `Reason = "Expired automatically"`.

- [ ] **Tests** (seed section, analyst, entries, lots, a MobilePhase solution used by a method, a Diluent, a fixed-time `ILabClock` fake — check if a test fake already exists):
```csharp
Start_MobilePhase_WithoutMethod_Throws; Start_MobilePhase_MethodNotUsingSolution_Throws; Start_Diluent_WithMethod_Throws
Start_CopiesComponentsAndSnapshot
Save_LotOfOtherEntry_Throws; Save_ExpiredLot_Throws; Save_InsufficientLot_Throws
Complete_DeductsStock_SetsCodeExpiryAndStatus          // MP-VIT-C 01/09/2026, stock 100 -> 95
Complete_SecondInSeries_Gets02; Complete_NewLabYear_RestartsAt01 (clock 2026-12-31 21:30Z = 2027-01-01 00:30 lab -> "…/01/2027")
Complete_DiluentCodeHasNoAbbreviation                  // DL-01/09/2026
Complete_MissingLot_Throws_NoStockChange
Complete_PhOutOfTolerance_Throws
Complete_LotExpiredSincePicked_Throws_NamesComponent    // Review Focus 2
Complete_WrongPassword_LeavesInProgress_NoStockChange
Cancel_RequiresReason_NoStockChange; Discard_AfterPrepared_DoesNotRestoreStock
Closed_CannotBeCancelledOrDiscarded
GetById_PastExpiry_ShowsExpired_AndNotInAvailable       // Review Focus 5
ExpireDueAsync_FlipsDueRows_WritesHistory
GetByLot_ListsPreparationsThatConsumedIt
```
Plus one **Postgres** integration test (`backend/MicroLIMS.Tests/IntegrationTests/SolutionPreparationPostgresIntegrationTests.cs`, same skip/env pattern as other Postgres tests): two preparations of the same series completed on two DbContexts concurrently → exactly one code `…01…`, the other throws the clash message, stock deducted once.
- [ ] FAIL → implement → PASS. **Commit** `feat(preparation): solution preparation service`

---

### Task 4: API + expiry worker

**Files:** `backend/MicroLIMS.API/Controllers/SolutionPreparationController.cs` (`api/solution-preparations`): GET list `?status&type`, GET `{id}`, GET `{id}/components/{componentId}/lots`, GET `available?solutionMasterId&hplcMethodId`, GET `by-lot/{materialId}`, POST (start), PUT `{id}`, POST `{id}/complete`, POST `{id}/cancel`, POST `{id}/discard` — writes `[Authorize(Policy = PermissionConstants.SolutionsPrepare)]` (add the policy where other policies are registered). `backend/MicroLIMS.API/BackgroundServices/SolutionPreparationExpiryWorker.cs` — same shape as `DocumentEffectiveDateWorker` (startup cycle + `PeriodicTimer` 5 min + non-overlap lock), calls `ExpireDueAsync`; registered with `AddHostedService`. DI for the service; authorization matrix regenerated (only new rows). **Commit** `feat(api): solution preparation endpoints and expiry worker`

---

### Task 5: Frontend (agy, ui-ux-pro-max; files ≤ ~350 lines)

Module `frontend/src/modules/solutionPreparation/`: `PreparationListPage.tsx` (route `/preparation`: code, type, solution, method, status badge text+icon, prepared, expiry, preparer; filters; "Start preparation"), `PreparationWizardPage.tsx` (route `/preparation/new?type=&solutionId=&methodId=`; steps: Select → Lots & quantities → Review → Sign; resumes an InProgress record at `/preparation/:id/edit`), `components/ComponentLotPicker.tsx` (usable lots selectable, blocked lots shown disabled with reason), `components/PreparationStatusBadge.tsx`, `PreparationRecordPage.tsx` (route `/preparation/:id`: snapshot recipe, components with lot + quantity, times, expiry, history; Cancel/Discard with reason dialog), `services/SolutionPreparationService.ts`, `types.ts`. Menu: "Solution Preparation" in the Physicochemical lab area, visible with `Solutions.Prepare`. Signing uses the app's existing password-signature dialog. Material lot details dialog: add a small "Consumed by preparations" list (`GET by-lot/{materialId}`). Build + eslint clean. **Commit** `feat(ui): solution preparation area`

---

### Task 6: Verify
- [ ] Backup LIMSV2 (`LIMSV2_before_solution_preparation_20260929.dump`), apply migration, confirm `Solutions.Prepare` rows exist and are granted, full Postgres suite → 0 failures, frontend build, whole-diff review, memory. **Stop — report before S5.**
