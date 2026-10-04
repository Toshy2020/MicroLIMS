# I2 — ICP Run Backend Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** ICP-OES runs: start a run of an `IcpMethod` on an ICP-OES instrument, confirm a per-element calibration (r, optional blank / ICV), record CCV checks, assign `IcpMethodAssay` test orders, enter solution mg/L per element, calculate content server-side and send results for review through the existing review/approval flow.

**Architecture:** New `Icp*` run tables copying the `HplcRun` pattern (snapshot at start, signed confirmation, evidence with supersede, remove-with-reason, Send for Review via a `TestWorkflowSupport` recorder). Two pure helpers (`IcpContentCalculator`, `IcpCalibrationEvaluator`) hold all maths and verdicts; one `IcpSampleEntryContext` is shared by entry preview and submission so they can't disagree (same as `HplcSampleEntryContext`).

**Tech Stack:** ASP.NET Core, EF Core (PostgreSQL), xUnit (InMemory unit tests, `MICROLIMS_TEST_POSTGRES` integration tests).

**Spec:** `docs/superpowers/specs/2026-10-03-icp-gc-workspaces-design.md` §4.2 (D3, D4, D8, D10). I1 (method master) is done: `IcpMethod`, `IcpMethodElement`, `IcpMethodService` (+ `IcpMethodResponse` used as the run snapshot), `TestDefinition.IcpMethodId`, `Specification.IcpMethodElementId`, `WorkflowType.IcpMethodAssay`.

## Global Constraints

- Clean Architecture: entities/enums in Domain, services/helpers/recorder in Application, EF config + migration in Persistence, controller in API.
- Copy the HPLC run idioms (`HplcRunService*.cs`, `HplcSampleEntryContext.cs`, `HplcMethodAssayRecorder.cs`, `HplcWorkspaceController.cs`); do not refactor HPLC code.
- Builds/tests while the API runs: `--artifacts-path E:/MicroLIMS/rls-tmp/i2-build`. All output/temp on `E:`.
- Migration: `dotnet ef migrations add AddIcpRuns --project backend/MicroLIMS.Persistence --startup-project backend/MicroLIMS.API --configuration Release`. Apply to LIMSV2 only in the last task, after a backup.
- Permission: every write needs `PermissionConstants.HplcOperate` (D10); reads any authenticated user scoped by section, like HPLC.
- Nothing is hard-deleted: removal = status + reason; evidence is superseded, never replaced or deleted.
- Every sensitive action is audited (the HPLC equivalents' audit calls are the model); calibration confirmation and Send for Review are e-signed (`IElectronicSignatureService`, password).
- Frontend never calculates: preview values, verdicts and lock reasons all come from the server.
- Every user-facing error is an `InvalidOperationException` with the exact message given in the task (tests assert the text).
- Enum values appended, never inserted.

## User rulings (2026-10-04, binding)

- **Blocked element:** a sample whose test has a spec for an element that is not valid on the run (calibration failed, CCV failed, or CCV required and not yet passed) cannot be sent for review on this run. One result per test; no partial results. The analyst removes it (with a reason) and re-runs it elsewhere.
- **CCV gate:** when the method requires CCV, each element of the sample needs ≥ 1 passing CCV on the run and no failing CCV; a failing CCV locks the element for every not-yet-submitted sample (already-submitted samples are not pulled back — `// ponytail:` ceiling from the spec).
- (I1) Label claim on both mineral spec rows; conversion factor on the method element only; impurities specs are µg/g NotMoreThan.

## Controller rulings (this plan)

- **Amount unit:** `IcpRunSample.AmountUnit` (`Gram` | `Milliliter`, default Gram) applies to both the replicate sample amount and the unit amount, so liquids work without a density: content is µg per g (or per mL). `ElementalImpurities` requires `Gram` (results are µg/g).
- **<LOQ mixed replicates:** if every replicate of an element is below the lowest standard → reported `<LOQ` (passes NotMoreThan, otherwise RequiresReview — the existing ElementalAssay rule). If only some are → mean of the measured values, status `RequiresReview`, display suffixed ` (some replicates <LOQ)`.
- **Over range:** any replicate above the top standard blocks preview verdict and submission for that element: `"{Symbol}: replicate {n} is above the top standard - dilute and re-measure."`

## Review Focus

1. A sample that needs Zn on a run where Zn failed calibration → Send for Review refused with the element named — Task 7 `Submit_BlockedElement_Throws`.
2. A CCV fails after one sample was already submitted → that sample stays submitted, the next sample needing the element is refused — Task 5 `FailingCcv_LocksElement_OnlyForUnsubmitted`.
3. Calibration older than `MaxCalibrationAgeHours` → no replicate save, no submit — Task 6 `SaveReplicates_CalibrationExpired_Throws`, Task 7 `Submit_CalibrationExpired_Throws`.
4. Confirming calibration without the Syngistix report uploaded → refused, nothing signed — Task 4 `ConfirmCalibration_WithoutReport_Throws`.
5. Every element failing calibration → run cannot assign samples (no element valid) — Task 4 `ConfirmCalibration_AllFail_RunBlocksAssignment`.

---

### Task 1: Domain, EF configuration and migration

**Files:** Create `backend/MicroLIMS.Domain/Entities/IcpRun.cs`; create enums `IcpRunStatus`, `IcpCalibrationStatus`, `IcpRunSampleStatus`, `IcpEvidenceContext`, `IcpEvidenceKind`, `IcpAmountUnit` in `backend/MicroLIMS.Domain/Enums/`; create `backend/MicroLIMS.Persistence/Configurations/IcpRunConfiguration.cs`; DbSets in `IMicroLimsDbContext` + `MicroLimsDbContext`; `UserReferenceRegistry` entries for every new `*UserId` (copy the HplcRun entries' dispositions); migration `AddIcpRuns`.

**Produces (exact names):**

```csharp
public enum IcpRunStatus { Open, Completed, Abandoned }
public enum IcpCalibrationStatus { Pending, Confirmed }
public enum IcpRunSampleStatus { Assigned, Removed }
public enum IcpEvidenceContext { Run, Calibration, Sample }
public enum IcpEvidenceKind { CalibrationReport, SampleReport, Other }
public enum IcpAmountUnit { Gram, Milliliter }

public class IcpRun : IVersionedEntity
{
    public int Id { get; set; }  public uint Version { get; set; }
    public int SectionId { get; set; }  public DocumentSection? Section { get; set; }
    public string Code { get; set; } = string.Empty;            // "{ABBR} RUN {nn}/{MM}{yyyy}"
    public int EquipmentId { get; set; }  public Equipment? Equipment { get; set; }
    public int IcpMethodId { get; set; }  public IcpMethod? IcpMethod { get; set; }
    public string MethodSnapshotJson { get; set; } = "{}";      // IcpMethodResponse at start
    public int AnalystUserId { get; set; }
    public DateTime StartedAt { get; set; }
    public IcpRunStatus Status { get; set; }
    public DateTime? ClosedAt { get; set; }
    public string? CloseReason { get; set; }                    // required for Abandoned
    public IcpCalibration? Calibration { get; set; }
    public List<IcpCcvReading> CcvReadings { get; set; } = new();
    public List<IcpRunSample> Samples { get; set; } = new();
    public List<IcpEvidence> Evidence { get; set; } = new();
}

public class IcpCalibration   // 1:1 with run, created Pending at start
{
    public int Id { get; set; }  public int IcpRunId { get; set; }  public IcpRun? IcpRun { get; set; }
    public string Code { get; set; } = string.Empty;            // "{ABBR} CAL {nn}/{MM}{yyyy}"
    public IcpCalibrationStatus Status { get; set; }
    public int? CalibrationStandardMaterialId { get; set; }  public Material? CalibrationStandardMaterial { get; set; }
    public int? IcvStandardMaterialId { get; set; }  public Material? IcvStandardMaterial { get; set; }
    public int? ConfirmedByUserId { get; set; }  public DateTime? ConfirmedAt { get; set; }  // starts the MaxCalibrationAgeHours clock
    public int? SignatureId { get; set; }  public ElectronicSignature? Signature { get; set; }
    public List<IcpCalibrationElement> Elements { get; set; } = new();
}

public class IcpCalibrationElement   // one per method element, created at start
{
    public int Id { get; set; }  public int IcpCalibrationId { get; set; }  public IcpCalibration? IcpCalibration { get; set; }
    public int IcpMethodElementId { get; set; }  public string Symbol { get; set; } = string.Empty;
    public decimal? CorrelationR { get; set; }
    public decimal? BlankMgPerL { get; set; }
    public decimal? IcvMeasuredMgPerL { get; set; }
    public decimal? IcvRecoveryPercent { get; set; }   // server-computed
    public bool Passed { get; set; }
    public string? FailureReasons { get; set; }
}

public class IcpCcvReading
{
    public int Id { get; set; }  public int IcpRunId { get; set; }  public IcpRun? IcpRun { get; set; }
    public int IcpMethodElementId { get; set; }  public string Symbol { get; set; } = string.Empty;
    public decimal MeasuredMgPerL { get; set; }
    public decimal RecoveryPercent { get; set; }       // server-computed
    public bool Passed { get; set; }
    public int EnteredByUserId { get; set; }  public DateTime EnteredAt { get; set; }
}

public class IcpRunSample
{
    public int Id { get; set; }  public int IcpRunId { get; set; }  public IcpRun? IcpRun { get; set; }
    public int TestOrderId { get; set; }  public TestOrder? TestOrder { get; set; }
    public IcpRunSampleStatus Status { get; set; }
    public IcpAmountUnit AmountUnit { get; set; }      // g or mL, for sample amount and unit amount
    public decimal? UnitAmount { get; set; }           // average unit weight (g) or dose (mL); mineral mg/unit only
    public DateTime AssignedAt { get; set; }  public int AssignedByUserId { get; set; }
    public string? RemovedReason { get; set; }  public DateTime? RemovedAt { get; set; }  public int? RemovedByUserId { get; set; }
    public List<IcpSampleReplicate> Replicates { get; set; } = new();
}

public class IcpSampleReplicate
{
    public int Id { get; set; }  public int IcpRunSampleId { get; set; }  public IcpRunSample? IcpRunSample { get; set; }
    public int ReplicateNo { get; set; }
    public decimal SampleAmount { get; set; }          // g or mL (AmountUnit)
    public decimal VolumeMl { get; set; }
    public decimal DilutionFactor { get; set; }
    public List<IcpReplicateConcentration> Concentrations { get; set; } = new();
}

public class IcpReplicateConcentration
{
    public int Id { get; set; }  public int IcpSampleReplicateId { get; set; }  public IcpSampleReplicate? IcpSampleReplicate { get; set; }
    public int IcpMethodElementId { get; set; }
    public decimal SolutionMgPerL { get; set; }        // "Conc. in Calib. Units, mg/L" (D8)
}

public class IcpEvidence   // same shape as HplcEvidence
{
    public int Id { get; set; }  public int IcpRunId { get; set; }  public IcpRun? IcpRun { get; set; }
    public int? IcpRunSampleId { get; set; }  public IcpRunSample? IcpRunSample { get; set; }
    public IcpEvidenceContext Context { get; set; }  public IcpEvidenceKind Kind { get; set; }
    public string FilePath { get; set; } = string.Empty;  public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public int UploadedByUserId { get; set; }  public DateTime UploadedAt { get; set; }
    public int? SupersededByEvidenceId { get; set; }  public IcpEvidence? SupersededByEvidence { get; set; }
    public string? SupersedeReason { get; set; }
}
```

Configuration (copy `HplcRun*` configuration conventions): unique `Code` on runs and calibrations; precision (18,6) everywhere except `CorrelationR` (8,6); FKs to Equipment/IcpMethod/Material/TestOrder/ElectronicSignature `Restrict`; children cascade from their parent; unique (IcpRunSampleId, ReplicateNo); unique (IcpSampleReplicateId, IcpMethodElementId); index (IcpRunId, IcpMethodElementId) on CCV readings.

- [ ] Write entities/enums/config, add DbSets + registry entries, create migration, build `backend/MicroLIMS.Tests` (0 errors), run unit suite (Postgres skips expected).
- [ ] Commit `feat(icp): ICP run schema`.

---

### Task 2: IcpContentCalculator (pure, TDD)

**Files:** Create `backend/MicroLIMS.Application/Helpers/IcpContentCalculator.cs`; test `backend/MicroLIMS.Tests/UnitTests/IcpContentCalculatorTests.cs`.

```csharp
public record IcpReplicateInput(int ReplicateNo, decimal SampleAmount, decimal VolumeMl, decimal DilutionFactor, decimal SolutionMgPerL);
public record IcpReplicateContent(int ReplicateNo, decimal ContentPerAmount, bool BelowLoq, bool OverRange);
public record IcpElementContent(
    IReadOnlyList<IcpReplicateContent> Replicates,
    decimal? MeanContentPerAmount,      // µg per g (or per mL); null when every replicate is <LOQ
    bool AllBelowLoq, bool SomeBelowLoq, bool AnyOverRange,
    decimal? MgPerUnit, decimal? PercentLabelClaim);

public static class IcpContentCalculator
{
    // content = C × V × DF / W   (mg/L × mL = µg; / g -> µg/g)
    // mg/unit = mean content × unitAmount / 1000 × conversionFactor ; %LC = mg/unit / labelClaim × 100
    // C < lowest level -> BelowLoq ; C > highest level -> OverRange (C equal to a level is in range)
    public static IcpElementContent Calculate(IReadOnlyList<IcpReplicateInput> reps, decimal lowestLevel, decimal highestLevel,
        decimal conversionFactor, decimal? unitAmount, decimal? labelClaim);
}
```

Rules: `SampleAmount`, `VolumeMl` > 0 and `DilutionFactor` ≥ 1 else `Sample amount, volume and dilution must be positive (dilution at least 1).`; at least one replicate else `At least one replicate is required.` Mean = mean of replicates that are not BelowLoq (null if all BelowLoq). `MgPerUnit` only when `unitAmount` > 0 and mean not null; `PercentLabelClaim` only when also `labelClaim` > 0. No rounding inside (display rounding is the recorder's job).

- [ ] **Step 1: failing tests** (hand-checked):

```csharp
[Fact]
public void Calculate_MineralExample_HandChecked()
{
    // Zn: C 0.80 mg/L, V 50 mL, DF 10, W 0.5000 g -> 0.80*50*10/0.5 = 800 µg/g
    // rep2: C 0.82, W 0.5100 -> 0.82*50*10/0.51 = 803.921569 µg/g ; mean 801.960784
    // unit 1.2500 g -> 801.960784*1.25/1000 = 1.002451 mg/unit ; claim 1 mg -> 100.2451 %
    var r = IcpContentCalculator.Calculate(new[] {
        new IcpReplicateInput(1, 0.5000m, 50m, 10m, 0.80m),
        new IcpReplicateInput(2, 0.5100m, 50m, 10m, 0.82m) }, 0.1m, 6m, 1m, 1.25m, 1m);
    Assert.Equal(800m, r.Replicates[0].ContentPerAmount);
    Assert.Equal(801.960784m, Math.Round(r.MeanContentPerAmount!.Value, 6));
    Assert.Equal(1.002451m, Math.Round(r.MgPerUnit!.Value, 6));
    Assert.Equal(100.2451m, Math.Round(r.PercentLabelClaim!.Value, 4));
}
```

plus `Calculate_ConversionFactorApplied` (Ca as CaCO3 factor 2.4973 → mg/unit × 2.4973), `Calculate_AllBelowLoq_MeanNull`, `Calculate_SomeBelowLoq_MeanOfMeasured`, `Calculate_AboveTopStandard_OverRange`, `Calculate_EqualToLevels_InRange`, `Calculate_NoUnitAmount_NoMgPerUnit`, `Calculate_ZeroWeight_Throws`.
- [ ] Run → fail; implement; run → pass. Commit `feat(icp): ICP content calculator`.

---

### Task 3: IcpCalibrationEvaluator (pure, TDD)

**Files:** Create `backend/MicroLIMS.Application/Helpers/IcpCalibrationEvaluator.cs`; test `backend/MicroLIMS.Tests/UnitTests/IcpCalibrationEvaluatorTests.cs`.

```csharp
public record IcpCalibrationElementInput(string Symbol, decimal? CorrelationR, decimal? BlankMgPerL, decimal? IcvMeasuredMgPerL);
public record IcpCalibrationElementVerdict(string Symbol, bool Passed, decimal? IcvRecoveryPercent, IReadOnlyList<string> FailureReasons);
public record IcpCheckLimits(decimal MinCorrelation, bool RequireBlank, decimal? BlankMaxMgPerL,
    bool RequireIcv, decimal? IcvNominalMgPerL, decimal? IcvLowPercent, decimal? IcvHighPercent);

public static class IcpCalibrationEvaluator
{
    public static IcpCalibrationElementVerdict Evaluate(IcpCalibrationElementInput e, IcpCheckLimits l);
    // CCV: recovery = measured / nominal × 100 ; pass when low ≤ recovery ≤ high
    public static (decimal RecoveryPercent, bool Passed) EvaluateCcv(decimal measuredMgPerL, decimal nominal, decimal low, decimal high);
}
```

Failure reasons (exact): `r {r} is below the minimum {min}.`; `Blank {b} mg/L is above the limit {max} mg/L.`; `ICV recovery {x}% is outside {low}-{high}%.` (numbers `InvariantCulture`, recovery rounded to 1 dp in the text only). Missing required value → reason `r is required.` / `Blank is required.` / `ICV result is required.` (r is always required; blank/ICV only when their check is on). Boundaries inclusive (r == min passes; recovery == low/high passes; blank == max passes).

- [ ] Tests: `Evaluate_AllChecksPass`, `Evaluate_RBelowMin_Fails` (spec I4 data: min 0.999, Zn r 0.996383 → fails, Ca 0.999386 → passes), `Evaluate_RExactlyMin_Passes`, `Evaluate_BlankOverLimit_Fails`, `Evaluate_IcvOutOfRange_Fails`, `Evaluate_ChecksOff_IgnoresBlankAndIcv`, `Evaluate_MissingR_Fails`, `EvaluateCcv_Boundaries`.
- [ ] Run → fail; implement; run → pass. Commit `feat(icp): ICP calibration evaluator`.

---

### Task 4: IcpRunService — instruments, start, calibration, evidence, abandon, history (TDD)

**Files:** Create `backend/MicroLIMS.Application/Services/IcpRunService.cs` (partial class; keep each file < ~450 lines — split `IcpRunService.Calibration.cs`, `IcpRunService.Evidence.cs` as needed); DTOs in the same files like HPLC; `TestServiceFactory.IcpRun(db, …)`; tests `backend/MicroLIMS.Tests/UnitTests/IcpRunServiceTests.cs`.

**Produces:**

```csharp
public record IcpInstrumentDto(int EquipmentId, string Code, string Name, string State, string? Reason, IcpActiveRunSummaryDto? ActiveRun);
public record IcpActiveRunSummaryDto(int RunId, string Code, string MethodAbbreviation, string AnalystName, int SampleCount, IcpCalibrationStatus CalibrationStatus);
public record IcpMethodOptionDto(int Id, string Abbreviation, string Name, IcpMethodMode Mode, int EligibleTestOrderCount);
public record StartIcpRunRequest(int EquipmentId, int IcpMethodId);
public record SaveIcpCalibrationElementInput(int IcpCalibrationElementId, decimal? CorrelationR, decimal? BlankMgPerL, decimal? IcvMeasuredMgPerL);
public record SaveIcpCalibrationRequest(int? CalibrationStandardMaterialId, int? IcvStandardMaterialId, List<SaveIcpCalibrationElementInput> Elements);
public record ConfirmIcpCalibrationRequest(string Password, string? Comment);
public record IcpRunDto(...);   // run header, method snapshot (IcpMethodResponse), calibration (+ elements with verdicts, expiresAt),
                                // ccv readings, element availability (Task 5), samples summary, evidence — mirror HplcRunDto
public record IcpRunListItem(int Id, string Code, string MethodAbbreviation, string AnalystUserName, DateTime StartedAt, IcpRunStatus Status, IcpCalibrationStatus CalibrationStatus, int SampleCount);

public partial class IcpRunService
{
    Task<List<IcpInstrumentDto>> GetInstrumentsAsync(int userId, CancellationToken ct = default);          // EquipmentType.IcpOes in user's sections
    Task<List<IcpMethodOptionDto>> GetMethodOptionsAsync(int userId, CancellationToken ct = default);      // active IcpMethods in user's sections
    Task<IcpRunDto> StartRunAsync(StartIcpRunRequest r, int userId, CancellationToken ct = default);
    Task<IcpRunDto> GetRunAsync(int runId, int userId, CancellationToken ct = default);
    Task<IcpRunDto> SaveCalibrationAsync(int runId, SaveIcpCalibrationRequest r, int userId, CancellationToken ct = default);
    Task<IcpRunDto> ConfirmCalibrationAsync(int runId, ConfirmIcpCalibrationRequest r, int userId, string? ip, CancellationToken ct = default);
    Task<IcpEvidenceDto> UploadEvidenceAsync(int runId, int? runSampleId, IcpEvidenceContext context, IcpEvidenceKind kind, string fileName, string contentType, Stream content, int userId, CancellationToken ct = default);
    Task<IcpEvidenceDto> SupersedeEvidenceAsync(int evidenceId, string reason, string fileName, string contentType, Stream content, int userId, CancellationToken ct = default);
    Task<(byte[] Content, string ContentType, string FileName)> DownloadEvidenceAsync(int evidenceId, int userId, CancellationToken ct = default);
    Task<IcpRunDto> AbandonRunAsync(int runId, string reason, int userId, CancellationToken ct = default);
    Task<List<IcpRunListItem>> GetRunHistoryAsync(int equipmentId, int userId, CancellationToken ct = default);
}
```

Rules (copy the HPLC equivalents; exact new messages):
- Start: instrument must be `EquipmentType.IcpOes` → `"{Name}" is not an ICP-OES instrument.`; same availability/section rules and messages as `HplcRunService.StartRunAsync`; method active + same section; one open run per instrument (HPLC message wording with "ICP"); snapshot `IcpMethodResponse` as JSON (`SnapshotJson.Options`); codes via `SystemSuitabilityRunCode.NextAsync(_db.IcpRuns.Select(x => x.Code), abbr, labLocal, "RUN", ct)` and `..._db.IcpCalibrations.Select(x => x.Code)..., "CAL"`; create calibration Pending + one `IcpCalibrationElement` per snapshot element.
- Save calibration (Pending only, run Open): standard lots must be usable ReferenceStandard `Material` lots of the snapshot's calibration (resp. ICV) standard entry → `The calibration standard lot must be a usable lot of {Code}.` / `The ICV standard lot must be a usable lot of {Code}.`; ICV lot only when the method requires ICV; element ids must belong to this calibration; values stored as given, verdicts recomputed with `IcpCalibrationEvaluator` on every save.
- Confirm (Pending only): calibration standard lot required → `Choose the calibration standard lot.`; ICV lot required when ICV on → `Choose the ICV standard lot.`; a current (not superseded) evidence with `Context = Calibration` required → `Upload the Syngistix calibration report before confirming.`; signature via `IElectronicSignatureService` (same call shape as `ConfirmSstAsync`); verdicts recomputed and frozen; `Status = Confirmed`, `ConfirmedAt = now`. Confirmed is final.
- Evidence: same rules/messages as HPLC, `Context = Sample` requires `runSampleId` of this run.
- Abandon: reason required (HPLC message); Open only.
- Audit every start/save/confirm/abandon/evidence action like the HPLC service.

- [ ] Tests (arrange: IcpOes `Equipment`, ICP method via `TestServiceFactory.IcpMethod`, reference-standard `Material` lots): `StartRun_CreatesPendingCalibrationWithElements`, `StartRun_NonIcpInstrument_Throws`, `StartRun_SecondOpenRun_Throws`, `SaveCalibration_ComputesVerdicts` (I4 r values: Zn fails at 0.999), `ConfirmCalibration_WithoutReport_Throws`, `ConfirmCalibration_WithoutStandardLot_Throws`, `ConfirmCalibration_AllFail_RunBlocksAssignment` (assert via Task 6 after it exists — here assert all elements `Passed = false` and the run DTO reports no valid element), `ConfirmCalibration_Twice_Throws`, `AbandonRun_RequiresReason`, `Evidence_SupersedeKeepsOld`.
- [ ] Run → fail; implement; run → pass. Commit `feat(icp): ICP runs and calibration`.

---

### Task 5: CCV readings and element availability (TDD)

**Files:** `backend/MicroLIMS.Application/Services/IcpRunService.Ccv.cs`, `backend/MicroLIMS.Application/Services/IcpElementAvailability.cs`; tests `IcpRunCcvTests.cs`.

```csharp
public record AddIcpCcvRequest(int IcpMethodElementId, decimal MeasuredMgPerL);
public record IcpElementStateDto(int IcpMethodElementId, string Symbol, bool Valid, string? Reason);
public partial class IcpRunService { Task<IcpRunDto> AddCcvReadingAsync(int runId, AddIcpCcvRequest r, int userId, CancellationToken ct = default); }

// Pure: one rule used by run DTO, entry preview and submission.
public static class IcpElementAvailability
{
    public static List<IcpElementStateDto> Evaluate(IcpMethodResponse snapshot, IcpCalibration calibration,
        IReadOnlyList<IcpCcvReading> ccv, DateTime nowUtc);
}
```

Reasons (first that applies, exact): calibration not confirmed → `Calibration is not confirmed.`; expired (`ConfirmedAt + MaxCalibrationAgeHours < now`) → `Calibration expired at {local time 'yyyy-MM-dd HH:mm'}.` (use the lab clock time zone like HPLC); element failed calibration → `Failed calibration: {FailureReasons}`; any failing CCV → `CCV failed ({recovery:0.0}%).`; CCV required and none passed → `Needs a passing CCV.`; else Valid.
CCV add: method must require CCV → `This method does not use CCV.`; calibration Confirmed and not expired → `Confirm a valid calibration before entering CCV.`; element of this method; measured ≥ 0; recovery/pass via `IcpCalibrationEvaluator.EvaluateCcv`; audited; readings are never edited or deleted.

- [ ] Tests: `AddCcv_Passing_MakesElementValid`, `AddCcv_Failing_LocksElement`, `FailingCcv_LocksElement_OnlyForUnsubmitted` (complete in Task 7: a submitted sample keeps its TestAnalysis; a later submit for the element is refused), `Availability_Expired`, `Availability_CcvNotRequired_ValidAfterCalibration`, `AddCcv_MethodWithoutCcv_Throws`.
- [ ] Run → fail; implement; run → pass. Commit `feat(icp): CCV checks and element availability`.

---

### Task 6: Samples — eligible, assign, remove, entry preview, replicates (TDD)

**Files:** `backend/MicroLIMS.Application/Services/IcpRunService.Samples.cs`, `backend/MicroLIMS.Application/Services/IcpSampleEntryContext.cs`, helper `backend/MicroLIMS.Application/Helpers/IcpSampleEvaluator.cs`; tests `IcpRunSampleTests.cs`.

```csharp
public record IcpConcentrationInput(int IcpMethodElementId, decimal SolutionMgPerL);
public record IcpReplicateInputDto(decimal SampleAmount, decimal VolumeMl, decimal DilutionFactor, List<IcpConcentrationInput> Concentrations);
public record SaveIcpReplicatesRequest(IcpAmountUnit AmountUnit, decimal? UnitAmount, List<IcpReplicateInputDto> Replicates);
public record AssignIcpSamplesRequest(List<int> TestOrderIds);
public record IcpPreviewResultDto(int IcpMethodElementId, string ParameterName, string Quantity, decimal? Value, string Display, string Unit, ResultStatus? Status, string? SpecLimit, string? Problem);
public record IcpSampleEntryDto(/* mirror HplcSampleEntryDto: ids, run/cal codes, sample info, AmountUnit, UnitAmount,
    method defaults (SampleVolumeMl, DilutionFactor), elements needed by the specs with their IcpElementStateDto,
    replicates, preview, official, evidence, Editable/EditableReason, Submitted, CanSubmit/CanSubmitReason */);

public partial class IcpRunService
{
    Task<List<EligibleTestDto>> GetEligibleTestsAsync(int runId, string? search, int userId, CancellationToken ct = default);
    Task<IcpRunDto> AssignSamplesAsync(int runId, List<int> testOrderIds, int userId, CancellationToken ct = default);
    Task<IcpRunDto> RemoveSampleAsync(int runId, int runSampleId, string reason, int userId, CancellationToken ct = default);
    Task<IcpSampleEntryDto> GetSampleEntryAsync(int runSampleId, int userId, CancellationToken ct = default);
    Task<IcpSampleEntryDto> SaveReplicatesAsync(int runSampleId, SaveIcpReplicatesRequest r, int userId, CancellationToken ct = default);
    Task<IcpRunDto> CompleteRunAsync(int runId, int userId, CancellationToken ct = default);
}
```

Rules:
- Eligible = open `IcpMethodAssay` test orders whose definition's `IcpMethodId` = run method, in the run's section, not finalized/cancelled/voided, not on another Open run (copy `HplcRunService.EligibleOrdersQuery`).
- Assign: calibration Confirmed → `Confirm the calibration before assigning samples.`; at least one element valid → `No element is valid on this run.`; each id eligible (HPLC message). Remove: reason required, not after submission (HPLC messages); stays as Removed.
- `IcpSampleEntryContext.LoadAsync(db, runSampleId)` loads run, snapshot, calibration, CCV, sample, order, specs (rows with `IcpMethodElementId` for this test, stage-aware like HPLC), and exposes `EntryProblem()`, `SubmitProblem()`, `EvaluateRows()`.
- Needed elements = elements referenced by the test's specs. Entry `Problem` per element when not valid on the run (availability reason).
- Save replicates: run Open, sample Assigned, not submitted (HPLC editable rules); calibration valid (not expired) → `Calibration expired at {time}.`; every replicate has every needed element exactly once; method defaults are only defaults (values sent are used); `AmountUnit` must be Gram for ElementalImpurities → `Elemental impurities are reported per gram; enter the sample amount in g.`; `UnitAmount` > 0 required when any spec basis is MgPerUnit or PercentLabelClaim → `Enter the unit amount (average unit weight or dose).`
- `IcpSampleEvaluator.Evaluate(...)` per needed element and per spec row (MgPerUnit / PercentLabelClaim / MgPerKg) using `IcpContentCalculator` (levels from the snapshot's `StandardLevelsMgPerL`, lowest/highest), the element conversion factor, the sample unit amount and the spec label claim; OverRange → row Problem with the ruling's message and no Status; all <LOQ → Display `<LOQ`, Status by the existing rule (NotMoreThan → WithinLimits else RequiresReview); some <LOQ → mean, Status RequiresReview, Display suffix; otherwise `SpecificationEvaluator.Evaluate(spec, value)`. Display: MgPerKg `"{v:0.00} µg/g"` (Unit "µg/g"), MgPerUnit `"{v:0.000} {LabelClaimUnit}"`, PercentLabelClaim `"{v:0.0} %"`; round half away from zero.
- Complete run: every Assigned sample submitted (or Removed) → HPLC rule/message.

- [ ] Tests: `Assign_BeforeCalibration_Throws`, `Assign_NoValidElement_Throws`, `Assign_IneligibleOrder_Throws`, `Remove_RequiresReason`, `SaveReplicates_MissingElement_Throws`, `SaveReplicates_CalibrationExpired_Throws`, `SaveReplicates_ImpuritiesInMl_Throws`, `Preview_MineralRows_HandChecked` (Task 2 numbers through the spec rows), `Preview_OverRange_ShowsProblem`, `Preview_AllBelowLoq_NotMoreThanPasses`, `Preview_BlockedElement_ShowsProblem`.
- [ ] Run → fail; implement; run → pass. Commit `feat(icp): ICP sample assignment and entry`.

---

### Task 7: Send for Review — IcpMethodAssayRecorder (TDD)

**Files:** Create `backend/MicroLIMS.Application/Workflows/TestWorkflow/IcpMethodAssayRecorder.cs`; modify `TestWorkflowEngine.cs` (+ `TestWorkflowContracts.cs`: `Task<TestWorkflowResult> SubmitIcpMethodAssayAsync(int runSampleId, string password, string? comment, int userId, string? ipAddress = null)`), `TestWorkflowSupport.cs` (~line 563: add `WorkflowType.IcpMethodAssay` to the "no supplied spec ids" list; ~line 648: `else if (expectedWorkflowType == WorkflowType.IcpMethodAssay)` → every spec must have `IcpMethodElementId` → `Every specification for an ICP method assay must be linked to a method element.`); tests `IcpMethodAssayRecorderTests.cs`.

Rules (copy `HplcMethodAssayRecorder`):
- `IcpSampleEntryContext.SubmitProblem()` first; refusals (exact): blocked needed element → `{Symbol} is not valid on this run: {reason}`; current sample-report evidence missing → `Upload the sample result report before sending for review.`; any row with a Problem → that problem text; replicates missing → HPLC message.
- `ValidateTestAnalysisOrderAsync(order, now, null, null, password, WorkflowType.IcpMethodAssay, userId)` then `PersistTestAnalysisAndFinalizeAsync` exactly as HPLC; one `ParameterResult` per spec row with `ReportedValue`, `ReportedDisplay`, `Status`, `Unit`, `SpecificationId`, `ParameterName`; overall status = worst row (OOS > RequiresReview > WithinLimits) like ElementalAssayRecorder.
- `CalculationJson` per row: `{ element, icpMethodElementId, quantity: "IcpMgPerKg" | "IcpMgPerUnit" | "IcpPercentLabelClaim", amountUnit, unitAmount, conversionFactor, labelClaim, labelClaimUnit, standardLevelsMgPerL, replicates: [{ replicateNo, sampleAmount, volumeMl, dilutionFactor, solutionMgPerL, contentPerAmount, belowLoq }], meanContentPerAmount, reportedValue, display, runCode, calibrationCode, calibrationConfirmedAt, correlationR, ccv: [{ measuredMgPerL, recoveryPercent, passed, enteredAt }], icpMethodId, icpRunId, icpRunSampleId }`.
- Return-for-retest/re-entry: same behaviour HPLC gets from the shared workflow (the test order leaves Ready; the run sample becomes editable again because "submitted" is derived from the test order state) — confirm by test, no new code if the shared flow already does it.

- [ ] Tests: `Submit_Mineral_PersistsRowsAndCalculationJson`, `Submit_BlockedElement_Throws`, `Submit_CalibrationExpired_Throws`, `Submit_WithoutSampleReport_Throws`, `Submit_OverRange_Throws`, `Submit_WrongPassword_NothingPersisted`, `FailingCcv_LocksElement_OnlyForUnsubmitted` (finish the Task 5 test here), `Return_ThenResubmit_Works`.
- [ ] Run → fail; implement; run → pass; rerun `HplcMethodAssayRecorder` tests unchanged. Commit `feat(icp): send ICP results for review`.

---

### Task 8: Controller, DI, authorization matrix

**Files:** Create `backend/MicroLIMS.API/Controllers/IcpWorkspaceController.cs`, route `api/icp-workspace`, copying `HplcWorkspaceController` (reads: `instruments`, `method-options`, `instruments/{equipmentId}/runs`, `runs/{id}`, `runs/{id}/eligible-tests`, `samples/{runSampleId}`, `test-orders/{testOrderId}/evidence`, `evidence/{id}/file`; writes under `[Authorize(Policy = PermissionConstants.HplcOperate)]`: `POST runs`, `PUT runs/{id}/calibration`, `POST runs/{id}/calibration/confirm`, `POST runs/{id}/ccv`, `POST runs/{id}/abandon`, `POST runs/{id}/complete`, `POST runs/{id}/samples`, `POST runs/{id}/samples/{runSampleId}/remove`, `PUT samples/{runSampleId}/replicates`, `POST samples/{runSampleId}/submit` (engine), `POST runs/{id}/evidence` (multipart, same limits as HPLC), `POST evidence/{id}/supersede`). DI registration next to `HplcRunService`; regenerate the authorization matrix (`MICROLIMS_WRITE_AUTH_MATRIX=1`) and check only the new IcpWorkspace lines appear.
- [ ] Commit `feat(icp): ICP workspace API`.

---

### Task 9: Postgres round trip, migration applied, full suite

- [ ] Backup: `E:/MicroLIMS/backups/LIMSV2_before_icp_runs_<yyyymmdd>.dump` (`pg_dump -Fc`, password from `appsettings.Development.json`, never printed).
- [ ] `IcpRunPostgresIntegrationTests.FullRun_RoundTrip`: method (Zn, Ca; CCV on) → start → save calibration (Ca r 0.999386 pass, Zn 0.996383 fail) → upload calibration report → confirm → CCV Ca pass → assign a Ca-only test order → replicates → sample report → submit → TestAnalysis + ParameterResults persisted; a Zn test order cannot be submitted.
- [ ] Full suite: build then `bash .claude/scripts/run-postgres-tests.sh E:/MicroLIMS/rls-tmp/i2-build` → 0 failed, 0 skipped.
- [ ] Apply `AddIcpRuns` to LIMSV2; verify tables + `__EFMigrationsHistory`.
- [ ] Commit `test(icp): ICP run Postgres round trip`.

---

## Frontend (I3) and E2E (I4)

Not in this plan. I3 = ICP workspace screens via agy from a contract written from these DTOs; I4 = seed (ICP-OES instrument, multi-element calibration standard + lot, ICV standard) + browser E2E with the real report values.
