# HPLC Chain S3 — HPLC Method Master Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** An HPLC Method master (parameters, gradient, mobile phases + diluent, analytes with Th.Wt and SST criteria), a new Test Master type `HplcMethodAssay` that points to a method, and specification rows keyed by method analyte for assay % and amount per unit.

**Architecture:** New aggregate `HplcMethod` with children `HplcMethodAnalyte`, `HplcMethodMobilePhase`, `HplcMethodGradientStep` (Domain + EF configs). `HplcMethodService` in Application, modelled on `SolutionMasterService` (S2): edit in place with a required reason. Each edit writes one audit event whose single change is the whole method as JSON before → after, so the history screen reads one list. `TestDefinitionMasterDataService` and `SpecificationService` each get one new branch for the new type. Controller `HplcMethodController` under `api/masterdata/hplc-methods`.

**Tech Stack:** ASP.NET Core, EF Core + Npgsql, xUnit, React + TS + MUI.

**Spec:** `docs/superpowers/specs/2026-09-29-hplc-chain-design.md` §2 (D1, D2, D5, D7), §3.3, §3.4

## Global Constraints
- Clean Architecture; keep it simple; only what this plan says.
- No versions (D7): edit in place, reason 1–500 chars required on update and on activate/deactivate.
- Writes `PermissionConstants.MasterDataManage`; reads `[Authorize]`. Section-scoped like S1/S2.
- Analytes live on the method (D5). Th.Wt.std and Th.Wt.test are method constants (D1).
- Spec rows reuse the existing `ResultBasis` enum as the quantity: `PercentLabelClaim` = assay %, `MgPerUnit` = amount per unit. **No new enum.**
- The existing StandardComparison / TestAnalyte path is untouched (D6).
- Frontend files stay small (target ≤ 350 lines each) — split page, dialog, sections, form mapping.

## Review Focus
- An `HplcMethodAssay` test pointing at an **inactive** method → refused on create/update of the test (existing tests keep working; the method is only checked when the method id changes).
- Two analytes with the **same name** on one method → refused.
- Removing an analyte that a **specification row already references** → refused with a clear message (never orphan a spec).
- Gradient mode with **no steps**, or isocratic mode **with** steps → refused.
- Mobile phase channel pointing at a **Diluent** solution (or diluent pointing at a Mobile Phase) → refused.

---

### Task 1: Domain + persistence + migration

**Files:**
- Create enums (namespace `MicroLIMS.Domain.Enums`): `ElutionMode.cs` `{ Isocratic, Gradient }`, `HplcDetectorType.cs` `{ UV, PDA, FLD, RI, ELSD, Other }`
- Create: `backend/MicroLIMS.Domain/Entities/HplcMethod.cs` (all 4 classes may share one file only if the codebase already does that elsewhere; otherwise one file each)
- Modify: `backend/MicroLIMS.Domain/Enums/WorkflowType.cs` — append `HplcMethodAssay` (last, keep int values); `EquationType.cs` — append `HplcMethodAssay`
- Modify: `TestDefinition.cs` — `public int? HplcMethodId { get; set; } public HplcMethod? HplcMethod { get; set; }`
- Modify: `Specification.cs` — `public int? HplcMethodAnalyteId { get; set; } [JsonIgnore] public HplcMethodAnalyte? HplcMethodAnalyte { get; set; }`
- Create configs; modify `IMicroLimsDbContext`/`MicroLimsDbContext` (DbSets `HplcMethods`, `HplcMethodAnalytes`, `HplcMethodMobilePhases`, `HplcMethodGradientSteps`); `TestDefinitionConfiguration` + `SpecificationConfiguration` FKs (Restrict); `UserReferenceRegistry` entries.
- Migration `AddHplcMethodMaster`.

- [ ] **Step 1: Entities**
```csharp
// HPLC method (HPLC chain S3). One record referenced by tests; edited in place
// with a reason; runs store a snapshot (D7). Deactivated, never deleted.
public class HplcMethod : IVersionedEntity
{
    public int Id { get; set; }
    public uint Version { get; set; }
    public int SectionId { get; set; }
    public DocumentSection? Section { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Abbreviation { get; set; } = string.Empty;   // upper-case, used in MP/SST/run codes
    public DateTime EffectiveDate { get; set; }
    public bool IsActive { get; set; } = true;

    // Column (USP <621> Appendix A)
    public string ColumnDesignation { get; set; } = string.Empty; // e.g. "L1"
    public decimal ColumnLengthMm { get; set; }
    public decimal ColumnInternalDiameterMm { get; set; }
    public decimal ParticleSizeUm { get; set; }
    public string? ColumnBrand { get; set; }
    public string? ColumnPartNumber { get; set; }
    public decimal ColumnTemperatureC { get; set; }

    // Elution
    public ElutionMode ElutionMode { get; set; }
    public decimal? EquilibrationMin { get; set; }
    public decimal FlowRateMlPerMin { get; set; }

    // Detection / injection
    public HplcDetectorType DetectorType { get; set; }
    public decimal InjectionVolumeUl { get; set; }
    public decimal RunTimeMin { get; set; }

    public int DiluentSolutionId { get; set; }
    public SolutionMaster? DiluentSolution { get; set; }

    public List<HplcMethodMobilePhase> MobilePhases { get; set; } = new();
    public List<HplcMethodGradientStep> GradientSteps { get; set; } = new();
    public List<HplcMethodAnalyte> Analytes { get; set; } = new();

    public int CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public int LastModifiedByUserId { get; set; }
    public DateTime LastModifiedAt { get; set; }
}

public class HplcMethodMobilePhase
{
    public int Id { get; set; }
    public int HplcMethodId { get; set; }
    public HplcMethod? HplcMethod { get; set; }
    public string Channel { get; set; } = string.Empty;   // "A".."D"
    public int SolutionMasterId { get; set; }
    public SolutionMaster? SolutionMaster { get; set; }
    public decimal? RatioPercent { get; set; }            // isocratic composition
}

public class HplcMethodGradientStep
{
    public int Id { get; set; }
    public int HplcMethodId { get; set; }
    public HplcMethod? HplcMethod { get; set; }
    public decimal TimeMin { get; set; }
    public decimal PercentA { get; set; }
    public decimal PercentB { get; set; }
    public decimal PercentC { get; set; }
    public decimal PercentD { get; set; }
}

// Kept on update when unchanged (matched by Id) so specification rows stay linked.
public class HplcMethodAnalyte
{
    public int Id { get; set; }
    public int HplcMethodId { get; set; }
    public HplcMethod? HplcMethod { get; set; }
    public int DisplayOrder { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal WavelengthNm { get; set; }
    public int StandardEntryId { get; set; }              // ReferenceStandard master entry
    public MaterialMasterEntry? StandardEntry { get; set; }
    public decimal TheoreticalWeightStdMg { get; set; }
    public decimal TheoreticalWeightTestMg { get; set; }
    public int StandardInjections { get; set; }
    public decimal? SstMaxRsdPercent { get; set; }
    public decimal? SstMinResolution { get; set; }
    public decimal? SstMaxTailingFactor { get; set; }
    public decimal? SstMinTheoreticalPlates { get; set; }
    public decimal? SstMinRetentionFactor { get; set; }
    public decimal? SstMinSignalToNoise { get; set; }
    public decimal? SstMinPeakToValley { get; set; }
}
```
- [ ] **Step 2: Configs** — strings: Name 200, Abbreviation 20 (unique index `(SectionId, Abbreviation)`), ColumnDesignation 20, Brand/PartNumber 100, Channel 1, analyte Name 100. Decimals `decimal(10,3)` (Th.Wt `decimal(12,4)`). Children `OnDelete(Cascade)` from method; FKs to SolutionMaster/MaterialMasterEntry `Restrict`. Unique `(HplcMethodId, Channel)` on mobile phases; unique `(HplcMethodId, Name)` on analytes. `TestDefinition.HplcMethodId` and `Specification.HplcMethodAnalyteId` → `Restrict`.
- [ ] **Step 3:** build (Release + artifacts path); `dotnet ef migrations add AddHplcMethodMaster …`; read it: only new tables, the two new nullable FK columns + indexes.
- [ ] **Step 4: Commit** `feat(masters): hplc method entities and test/spec links`

---

### Task 2: HplcMethodService (TDD)

**Files:** Create `backend/MicroLIMS.Application/Services/HplcMethodService.cs`, `DTOs/Responses/HplcMethodResponse.cs`; modify `TestServiceFactory.cs`; test `backend/MicroLIMS.Tests/UnitTests/HplcMethodServiceTests.cs`.

**Produces:**
```csharp
public record HplcMobilePhaseInput(string Channel, int SolutionMasterId, decimal? RatioPercent);
public record HplcGradientStepInput(decimal TimeMin, decimal PercentA, decimal PercentB, decimal PercentC, decimal PercentD);
public record HplcAnalyteInput(int? Id, string Name, decimal WavelengthNm, int StandardEntryId,
    decimal TheoreticalWeightStdMg, decimal TheoreticalWeightTestMg, int StandardInjections,
    decimal? SstMaxRsdPercent = null, decimal? SstMinResolution = null, decimal? SstMaxTailingFactor = null,
    decimal? SstMinTheoreticalPlates = null, decimal? SstMinRetentionFactor = null,
    decimal? SstMinSignalToNoise = null, decimal? SstMinPeakToValley = null);

public record SaveHplcMethodRequest(
    string Name, string Abbreviation, DateTime EffectiveDate,
    string ColumnDesignation, decimal ColumnLengthMm, decimal ColumnInternalDiameterMm, decimal ParticleSizeUm,
    decimal ColumnTemperatureC, ElutionMode ElutionMode, decimal FlowRateMlPerMin,
    HplcDetectorType DetectorType, decimal InjectionVolumeUl, decimal RunTimeMin, int DiluentSolutionId,
    List<HplcMobilePhaseInput> MobilePhases, List<HplcGradientStepInput> GradientSteps, List<HplcAnalyteInput> Analytes,
    string? ColumnBrand = null, string? ColumnPartNumber = null, decimal? EquilibrationMin = null,
    int? SectionId = null, string? Reason = null);

public class HplcMethodService
{
    Task<List<HplcMethodListItem>> GetAllAsync(int currentUserId, bool activeOnly = false, CancellationToken ct = default);
    Task<HplcMethodResponse> GetByIdAsync(int id, int currentUserId, CancellationToken ct = default);
    Task<HplcMethodResponse> CreateAsync(SaveHplcMethodRequest r, int currentUserId, CancellationToken ct = default);
    Task<HplcMethodResponse> UpdateAsync(int id, SaveHplcMethodRequest r, int currentUserId, CancellationToken ct = default);
    Task<HplcMethodResponse> SetActiveAsync(int id, bool isActive, string reason, int currentUserId, CancellationToken ct = default);
    Task<List<HplcMethodHistoryEntry>> GetHistoryAsync(int id, int currentUserId, CancellationToken ct = default);
}
public record HplcMethodListItem(int Id, string Name, string Abbreviation, bool IsActive, int AnalyteCount, string SectionName, DateTime LastModifiedAt);
public record HplcMethodHistoryEntry(DateTime At, string UserName, string Action, string? Reason, string? BeforeJson, string? AfterJson);
```
`HplcMethodResponse`: all scalars + `DiluentSolutionName`, children lists (mobile phases with solution name, gradient steps ordered by time, analytes ordered by `DisplayOrder` with `StandardEntryCode`).

- [ ] **Step 1: Failing tests** (seed section, member user, a ReferenceStandard entry, one MobilePhase and one Diluent solution):
```csharp
[Fact] Create_Isocratic_SavesChildren_AndUppercasesAbbreviation
[Fact] Create_DuplicateAbbreviationInSection_Throws                 // "already exists"
[Fact] Create_NoAnalytes_Throws; Create_NoMobilePhases_Throws
[Fact] Create_DuplicateAnalyteName_Throws                           // Review Focus 2
[Fact] Create_AnalyteStandardNotReferenceStandard_Throws
[Fact] Create_ThWtZero_Throws; Create_StandardInjectionsZero_Throws
[Fact] Create_ChannelNotAtoD_Throws; Create_DuplicateChannel_Throws
[Fact] Create_MobilePhaseChannelUsesDiluentSolution_Throws           // Review Focus 5
[Fact] Create_DiluentIsMobilePhaseSolution_Throws                    // Review Focus 5
[Fact] Create_GradientWithoutSteps_Throws; Create_IsocraticWithSteps_Throws   // Review Focus 4
[Fact] Create_GradientStepPercentsNot100_Throws                      // A+B+C+D must equal 100
[Fact] Create_IsocraticRatiosNot100_Throws                           // when any RatioPercent given, sum must be 100
[Fact] Update_WithoutReason_Throws
[Fact] Update_KeepsAnalyteIds_ForUnchangedAnalytes                   // analyte with Id kept, same Id after update
[Fact] Update_RemovingAnalyteUsedBySpecification_Throws              // Review Focus 3
[Fact] Update_RecordsHistoryWithBeforeAndAfterJson                   // GetHistoryAsync returns 1 entry with reason + both JSON
[Fact] SetActive_False_RequiresReason_AndAppearsInHistory
```

- [ ] **Step 2:** run → FAIL (compile).
- [ ] **Step 3: Implement.** Mirror `SolutionMasterService` for scope, section, version, reason validation. Validation rules:
  - Name, Abbreviation (2–20 chars, `^[A-Z0-9-]+$` after upper-casing), ColumnDesignation required; all numeric parameters > 0 (EquilibrationMin ≥ 0 when given).
  - Diluent: exists, same section, `Type == Diluent`, active unless it is the method's current diluent.
  - Mobile phases: ≥ 1; Channel in A–D, unique; solution exists, same section, `Type == MobilePhase`, active unless already on the method. Ratios: all null, or all given and summing to 100 (isocratic only; must be null for gradient).
  - Gradient: `Gradient` requires ≥ 2 steps, times strictly increasing from 0, each step's A+B+C+D = 100, and percentages for channels not defined must be 0; `Isocratic` requires 0 steps.
  - Analytes: ≥ 1; names unique (case-insensitive); WavelengthNm 190–900; standard entry same section, category `ReferenceStandard`, active unless already on the analyte; Th.Wt.std/test > 0; StandardInjections ≥ 1; SST criteria > 0 when given.
  - Update analytes: match by `Id`; update matched rows in place; add new rows (Id null); for removed rows, refuse if `_db.Specifications.AnyAsync(s => s.HplcMethodAnalyteId == removedId)` → `"Analyte \"{name}\" is used by specifications; it can't be removed."`. `DisplayOrder` = list index + 1. Mobile phases and gradient steps: clear and re-add.
  - History: before saving an update/activation, serialize the current `HplcMethodResponse` (camelCase) to `before`; after saving, serialize again to `after`; call `RecordUserEventAsync("HplcMethod.Updated" | "HplcMethod.Activated" | "HplcMethod.Deactivated", AuditActionCategory.Configuration, "HplcMethod", reason: reason, changes: new[] { new AuditFieldChange("Method", before, after) }, entityId: id.ToString())` — check `AuditFieldChange`'s real constructor and adapt. Create also records `"HplcMethod.Created"` with after only. `GetHistoryAsync` reads those audit rows (by recordType/entityId, newest first) with their change row and user name.
- [ ] **Step 4:** run → PASS. **Step 5: Commit** `feat(masters): hplc method service with reasoned edits and history`

---

### Task 3: Test Master + Specification rules (TDD)

**Files:** Modify `backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs` (request record + validation + mapping), `backend/MicroLIMS.Application/Services/SpecificationService.cs` (one new branch before the `CalibrationCurve` branch), the Test Master / Specification HTTP request records in `backend/MicroLIMS.API` (pass the new fields through). Tests: `backend/MicroLIMS.Tests/UnitTests/HplcMethodAssayTestMasterTests.cs`.

- [ ] **Step 1: Failing tests**
```csharp
[Fact] TestDef_HplcMethodAssay_WithoutMethod_Throws                 // "HPLC method is required"
[Fact] TestDef_HplcMethodAssay_InactiveMethod_Throws                // Review Focus 1
[Fact] TestDef_HplcMethodAssay_MethodFromOtherSection_Throws
[Fact] TestDef_HplcMethodAssay_EquationMustMatchWorkflow_Throws
[Fact] TestDef_OtherType_WithMethodId_Throws                        // "only allowed for HPLC method assay tests"
[Fact] TestDef_HplcMethodAssay_Update_KeepsInactiveMethodIfUnchanged
[Fact] Spec_HplcMethodAssay_RequiresAnalyteOfThatMethod
[Fact] Spec_HplcMethodAssay_RequiresResultBasisPercentOrMgPerUnit    // other ResultBasis values refused
[Fact] Spec_HplcMethodAssay_MgPerUnit_RequiresLabelClaimAndUnit
[Fact] Spec_HplcMethodAssay_Percent_RejectsLabelClaim
[Fact] Spec_HplcMethodAssay_DuplicateAnalyteAndBasis_Throws
[Fact] Spec_OtherTestType_WithHplcMethodAnalyteId_Throws
```
- [ ] **Step 2:** run → FAIL.
- [ ] **Step 3: Implement.**
  - Test Master: add `int? HplcMethodId` to the create/update request. If `WorkflowType == HplcMethodAssay` or `EquationType == HplcMethodAssay`: both must be `HplcMethodAssay`; `HplcMethodId` required; method exists in the test's section; active unless unchanged on update; `RequiresSystemSuitability` forced/required `true`; `ResponseMode` must be `PeakArea`. Otherwise `HplcMethodId` must be null. Follow the existing if/else style of the file.
  - Specification: branch `testDef?.WorkflowType == WorkflowType.HplcMethodAssay`:
    ```csharp
    if (!spec.HplcMethodAnalyteId.HasValue) throw new InvalidOperationException("Method analyte is required for HPLC method assay specifications.");
    var analyte = await _db.HplcMethodAnalytes.FirstOrDefaultAsync(a => a.Id == spec.HplcMethodAnalyteId.Value, cancellationToken);
    if (analyte == null || analyte.HplcMethodId != testDef.HplcMethodId) throw new InvalidOperationException($"That analyte does not belong to the method of test '{spec.TestCode}'.");
    if (spec.ResultBasis is not (ResultBasis.PercentLabelClaim or ResultBasis.MgPerUnit)) throw new InvalidOperationException("Result basis must be assay % (PercentLabelClaim) or amount per unit (MgPerUnit).");
    if (spec.TestAnalyteId.HasValue || spec.SampleMatrix.HasValue || spec.ConversionFactor != 1.0m) throw new InvalidOperationException("Test analyte, sample matrix and conversion factor are not used for HPLC method assay specifications.");
    if (spec.ResultBasis == ResultBasis.MgPerUnit && (!spec.LabelClaim.HasValue || spec.LabelClaim <= 0 || string.IsNullOrWhiteSpace(spec.LabelClaimUnit))) throw new InvalidOperationException("Amount per unit needs a label claim and its unit.");
    if (spec.ResultBasis == ResultBasis.PercentLabelClaim && (spec.LabelClaim.HasValue || !string.IsNullOrWhiteSpace(spec.LabelClaimUnit))) throw new InvalidOperationException("Label claim belongs on the amount-per-unit row.");
    duplicate: same ItemId + TestCode + HplcMethodAnalyteId + ResultBasis, other Id -> "A specification for this analyte and basis already exists."
    limit type: Range | NotMoreThan | NotLessThan | TargetWithTolerance (same message style as the StandardComparison branch)
    ```
    In every other branch, add `if (spec.HplcMethodAnalyteId.HasValue) throw …("Method analyte is only allowed for HPLC method assay specifications.")`.
- [ ] **Step 4:** run new tests + all existing `Specification*` and `TestDefinition*` tests → PASS. **Step 5: Commit** `feat(masters): hplc method assay test type and specification rows`

---

### Task 4: API

**Files:** Create `backend/MicroLIMS.API/Controllers/HplcMethodController.cs` (`api/masterdata/hplc-methods`: GET list `?activeOnly=`, GET `{id}`, GET `{id}/history`, POST, PUT `{id}`, PUT `{id}/active?value=` body `{ reason }`; writes `[Authorize(Policy = MasterDataManage)]`), DI registration, authorization matrix regenerated (`MICROLIMS_WRITE_AUTH_MATRIX=1`, only new rows). Build + ArchitectureTests pass. **Commit** `feat(api): hplc method endpoints`

---

### Task 5: Frontend (agy, ui-ux-pro-max skill; every file ≤ ~350 lines)

**Files:** `frontend/src/modules/laboratoryConfiguration/masterDataSimple/HplcMethodsPage.tsx` (list only) + folder `hplcMethod/`: `HplcMethodDialog.tsx`, `HplcColumnSection.tsx`, `HplcElutionSection.tsx` (mode, flow, equilibration, gradient table), `HplcDetectionSection.tsx`, `HplcSolutionsSection.tsx` (channels A–D → Mobile Phase solutions, ratio; diluent picker), `HplcAnalytesSection.tsx` (rows: name, λ, standard entry, Th.Wt.std, Th.Wt.test, injections, SST criteria), `HplcMethodHistoryDialog.tsx` (user, time, action, reason, expandable before/after), `hplcMethodForm.ts`, `services/HplcMethodService.ts`; route `/laboratory-configuration/hplc-methods`, menu "HPLC Methods" after "Solutions". Test Master screen: add `HplcMethodAssay` type with a required active-method picker (hide analyte/SST/response-mode fields for this type). Specification dialog: for an `HplcMethodAssay` test, analyte picker from the test's method + basis choice "Assay %" / "Amount per unit" (label claim + unit shown only for amount per unit). Build + eslint clean. **Commit** `feat(ui): hplc methods master, test type and specification rows`

---

### Task 6: Verify
- [ ] Backup LIMSV2 (`LIMSV2_before_hplc_method_master_20260929.dump`), apply migration, full Postgres suite → 0 failures, frontend build, Opus whole-diff review, memory update. **Stop — report before S4.**
