# HPLC Chain S5 — Titrant Standardization Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A prepared titrant (VS) can be standardized — replicate titrations against a primary-standard lot or another standardized VS — and the QC engine computes the factor; the preparation shows its current factor, date and validity.

**Architecture:** Task 1 is a pure calculator `TitrationEngine` (Application/Helpers, next to `StandardComparisonCalculator`) — no DB, can be built any time. Tasks 2–4 add `TitrantStandardization` (+ replicates) on top of S4's `SolutionPreparation`, a service, API and a dialog on the preparation record page.

**Tech Stack:** ASP.NET Core, EF Core + Npgsql, xUnit, React + TS + MUI.

**Spec:** `docs/superpowers/specs/2026-09-29-hplc-chain-design.md` §3.2 (titrant settings), §4 (titrant standardization); USP *Volumetric Solutions* check in the brainstorm (factor = W ÷ (V × E); blank correction; standardization against another VS; replicates/limits are lab SOP).

## Global Constraints
- Clean Architecture; keep it simple; only what this plan says. Frontend files ≤ ~350 lines.
- Formulas (full `decimal` precision stored; UI shows 4 dp):
  - PrimaryStandard: `f = (W_mg × P/100) / ((V_mL − Vblank_mL) × E_mgPerMl)`; P = lot purity, 100 when the lot has none.
  - AgainstVolumetricSolution: `f = (Vref_mL × f_ref × N_ref) / ((V_mL − Vblank_mL) × N_nominal)`; reference and titrant must use the same strength unit (N or M).
  - Blank: when the titrant's `BlankRequired` is false, `Vblank = 0` and none may be entered.
  - Reported factor = mean; RSD = sample SD (n−1) / mean × 100, null when n = 1. Passed when every replicate factor is within [FactorMin, FactorMax] and (RSD null or RSD ≤ MaxRsdPercent).
- Standardization does **not** deduct the standard's stock (open item: small weighed amounts; lot traceability is recorded).
- Validity: `ValidUntil = StandardizedAt + ValidityDays` days; `ValidityDays = 0` → `ValidUntil = null` and status "Restandardize before each use".
- Re-standardization adds a new record; earlier records are kept. Current factor = latest **passed** record.
- Signature meaning: append `TitrantStandardized` to `SignatureMeaning` (keep existing int values).
- Permission: writes use S4's `Solutions.Prepare`.

## Review Focus
- Titrant volume **equal to or below the blank** → refused per replicate (division by ≤ 0).
- Standardizing against a reference VS whose own standardization is **expired / failed / missing** → refused.
- Replicate count **different from** the titrant's configured count → refused.
- A **failed** standardization must not become the current factor; the previous passed factor (if still valid) stays current.
- Titrant preparation that is **expired or discarded** → cannot be standardized.

---

### Task 1: TitrationEngine (pure, TDD) — can run before S4 finishes

**Files:** Create `backend/MicroLIMS.Application/Helpers/TitrationEngine.cs`; test `backend/MicroLIMS.Tests/UnitTests/TitrationEngineTests.cs`.

```csharp
namespace MicroLIMS.Application.Helpers;

public record FactorEvaluation(decimal MeanFactor, decimal? RsdPercent, bool Passed, IReadOnlyList<string> FailureReasons);

// QC Analytical Engine - titration (USP Volumetric Solutions). Pure functions, no I/O.
public static class TitrationEngine
{
    public static decimal PrimaryStandardFactor(decimal standardWeightMg, decimal? purityPercent,
        decimal titrantVolumeMl, decimal blankMl, decimal equivalenceMgPerMl);

    public static decimal AgainstVolumetricSolutionFactor(decimal referenceVolumeMl, decimal referenceFactor,
        decimal referenceNominalStrength, decimal titrantVolumeMl, decimal blankMl, decimal nominalStrength);

    public static FactorEvaluation Evaluate(IReadOnlyList<decimal> factors, decimal factorMin, decimal factorMax, decimal maxRsdPercent);
}
```
Guards (throw `InvalidOperationException` with these texts): weight/equivalence/reference volume/reference factor/strengths ≤ 0 → "{name} must be greater than zero."; purity outside (0,100] → "Purity must be greater than 0 and at most 100."; blank < 0 → "Blank must not be negative."; `titrantVolumeMl - blankMl <= 0` → "Titrant volume must be greater than the blank."; `Evaluate` with no factors → "At least one replicate is required.".
Failure reasons: `"Replicate {n}: factor {f:0.0000} outside {min:0.0000}–{max:0.0000}."`, `"RSD {r:0.00}% exceeds {max:0.00}%."`.

- [ ] **Step 1: Failing tests (hand-checked numbers)**
```csharp
[Fact] PrimaryStandard_KhpAgainstNaoh_WithBlankAndPurity()
{   // 0.1 N NaOH, KHP: E = 20.42 mg/mL; (408.4 x 0.999) / ((20.05 - 0.05) x 20.42) = 407.9916 / 408.4 = 0.999
    Assert.Equal(0.999m, decimal.Round(TitrationEngine.PrimaryStandardFactor(408.4m, 99.9m, 20.05m, 0.05m, 20.42m), 6));
}
[Fact] PrimaryStandard_NoPurity_Uses100()
{   // 204.2 / (10.00 x 20.42) = 1.0
    Assert.Equal(1.0m, decimal.Round(TitrationEngine.PrimaryStandardFactor(204.2m, null, 10.00m, 0m, 20.42m), 6));
}
[Fact] AgainstVs_Example()
{   // (25.00 x 1.002 x 0.1) / ((24.90 - 0) x 0.1) = 2.505 / 2.49 = 1.0060241 (7 dp)
    Assert.Equal(1.0060241m, decimal.Round(TitrationEngine.AgainstVolumetricSolutionFactor(25.00m, 1.002m, 0.1m, 24.90m, 0m, 0.1m), 7));
}
[Fact] VolumeNotAboveBlank_Throws()        // V 0.05, blank 0.05
[Fact] Purity_Zero_Throws(); Weight_Zero_Throws(); Blank_Negative_Throws()
[Fact] Evaluate_ThreeInRange_Passes()
{   // 0.998, 1.000, 1.002 -> mean 1.000, SD 0.002, RSD 0.2 %
    var e = TitrationEngine.Evaluate(new[] { 0.998m, 1.000m, 1.002m }, 0.95m, 1.05m, 0.5m);
    Assert.True(e.Passed); Assert.Equal(1.000m, decimal.Round(e.MeanFactor, 4)); Assert.Equal(0.20m, decimal.Round(e.RsdPercent!.Value, 2));
}
[Fact] Evaluate_OneOutOfRange_Fails_NamesReplicate()   // 0.94 -> "Replicate 1: factor 0.9400 outside 0.9500–1.0500."
[Fact] Evaluate_RsdTooHigh_Fails()                      // 0.96, 1.04 with max RSD 0.5
[Fact] Evaluate_SingleReplicate_RsdNull_Passes()
```
For RSD use `Math.Sqrt` on `double` for the variance then convert back to `decimal` (same approach as `StandardComparisonCalculator` — check how it computes RSD and reuse its helper if one exists).
- [ ] **Step 2:** run → FAIL. **Step 3:** implement. **Step 4:** run → PASS.
- [ ] **Step 5: Commit** `feat(engine): titration factor calculator`

---

### Task 2: Standardization records + service (after S4 is merged into the branch)

**Files:** `backend/MicroLIMS.Domain/Entities/TitrantStandardization.cs`, `TitrantStandardizationReplicate.cs`; `SignatureMeaning.cs` (append `TitrantStandardized`); configs + DbSets; `UserReferenceRegistry`; migration `AddTitrantStandardization`; `backend/MicroLIMS.Application/Services/TitrantStandardizationService.cs`; response DTOs; `SolutionPreparationResponse` gains `CurrentFactor` block; tests `backend/MicroLIMS.Tests/UnitTests/TitrantStandardizationServiceTests.cs`.

```csharp
public class TitrantStandardization
{
    public int Id { get; set; }
    public int SolutionPreparationId { get; set; }
    public SolutionPreparation? SolutionPreparation { get; set; }
    public StandardizationMode Mode { get; set; }
    public string SettingsSnapshotJson { get; set; } = "{}";   // strength, unit, E, range, max RSD, replicates, validity at the time
    public decimal MeanFactor { get; set; }
    public decimal? RsdPercent { get; set; }
    public bool Passed { get; set; }
    public string? FailureReasons { get; set; }
    public int StandardizedByUserId { get; set; }
    public DateTime StandardizedAt { get; set; }
    public DateTime? ValidUntil { get; set; }
    public int SignatureId { get; set; }
    public ElectronicSignature? Signature { get; set; }
    public List<TitrantStandardizationReplicate> Replicates { get; set; } = new();
}

public class TitrantStandardizationReplicate
{
    public int Id { get; set; }
    public int TitrantStandardizationId { get; set; }
    public int ReplicateNo { get; set; }
    public int? StandardMaterialId { get; set; }          // PrimaryStandard: lot
    public Material? StandardMaterial { get; set; }
    public decimal? StandardWeightMg { get; set; }
    public decimal? StandardPurityPercent { get; set; }   // snapshot from the lot
    public int? ReferencePreparationId { get; set; }      // AgainstVolumetricSolution
    public SolutionPreparation? ReferencePreparation { get; set; }
    public decimal? ReferenceVolumeMl { get; set; }
    public decimal? ReferenceFactor { get; set; }         // snapshot of the reference's current factor
    public decimal TitrantVolumeMl { get; set; }
    public decimal? BlankMl { get; set; }
    public decimal Factor { get; set; }
}
```
Service:
```csharp
public record StandardizationReplicateInput(int? StandardMaterialId, decimal? StandardWeightMg,
    int? ReferencePreparationId, decimal? ReferenceVolumeMl, decimal TitrantVolumeMl, decimal? BlankMl);
public record StandardizeRequest(List<StandardizationReplicateInput> Replicates, string Password, string? Comment);
public record CurrentFactorDto(decimal? Factor, DateTime? StandardizedAt, DateTime? ValidUntil, string State); // "NotStandardized" | "Valid" | "Due" | "BeforeEachUse"

public class TitrantStandardizationService
{
    Task<TitrantStandardizationResponse> StandardizeAsync(int preparationId, StandardizeRequest r, int userId, string? ip, CancellationToken ct = default);
    Task<List<TitrantStandardizationResponse>> GetForPreparationAsync(int preparationId, int userId, CancellationToken ct = default);
    Task<CurrentFactorDto> GetCurrentFactorAsync(int preparationId, CancellationToken ct = default);
    Task<List<LotOption>> GetStandardLotOptionsAsync(int preparationId, int userId, CancellationToken ct = default);          // lots of the titrant's StandardEntryId, LotUsability(required null)
    Task<List<SolutionPreparationListItem>> GetReferenceOptionsAsync(int preparationId, int userId, CancellationToken ct = default); // preparations of ReferenceSolutionId, Prepared, unexpired, with State Valid
}
```
Rules: preparation type Titrant, effective status Prepared (not expired/discarded); settings read from the preparation's `RecipeSnapshotJson` (S4 snapshot — the titrant settings as they were when it was made); replicate count must equal `ReplicateCount`; mode-specific fields required/forbidden per mode; blank required iff `BlankRequired`; primary lot must be usable, same section, linked to `StandardEntryId`; reference preparation must be of `ReferenceSolutionId`, effective Prepared, and `GetCurrentFactorAsync(ref).State == "Valid"` (or "BeforeEachUse" standardized today — keep: only "Valid" accepted, and "BeforeEachUse" is accepted when standardized within the same lab-local day); same strength unit; compute each factor with `TitrationEngine`, `Evaluate`, sign (`SignatureMeaning.TitrantStandardized`, entity "TitrantStandardization" — check S4 for sign-before-save order), `ValidUntil` per Global Constraints, save. Failed records are saved too (Passed = false) and never become current.
Tests: one per Review Focus line + happy path primary (3 replicates, mean + RSD stored) + happy path against VS + current factor states (NotStandardized, Valid, Due after validity, failed-after-passed keeps passed).
- [ ] FAIL → implement → PASS. **Commit** `feat(preparation): titrant standardization records and service`

### Task 3: API
`SolutionPreparationController` (or a small `TitrantStandardizationController` under `api/solution-preparations/{id}/standardizations`): GET list, POST (writes `Solutions.Prepare`), GET `standard-lots`, GET `reference-options`. Authorization matrix regenerated (new rows only). **Commit** `feat(api): titrant standardization endpoints`

### Task 4: Frontend (agy; files ≤ ~350 lines)
On `PreparationRecordPage` for a Titrant: "Current factor" card (factor 4 dp, date, valid until / state text + icon: Not standardized, Valid, Due, Before each use) and a history table of standardizations (mean, RSD, passed/failed + reasons, analyst, date). `components/TitrantStandardizationDialog.tsx`: replicate rows per the titrant's mode (lot picker + weight mg, or reference picker + volume mL; titrant volume; blank when required), password signature, shows the engine's result returned by the API. **Commit** `feat(ui): titrant standardization`

### Task 5: Verify
Backup LIMSV2 (`LIMSV2_before_titrant_standardization_20260929.dump`), apply migration, full Postgres suite, frontend build, whole-diff review, memory. **Stop — report before S6.**
