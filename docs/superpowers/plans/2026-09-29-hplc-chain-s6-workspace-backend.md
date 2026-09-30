# HPLC Chain S6 — HPLC Workspace Backend Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The backend of the HPLC Workspace: instruments, runs with a method snapshot, prepared mobile phases, one SST record per run with standard entry and a report gate, sample assignment after SST passes, replicate entry with calculated assay % and amount per unit, Send for Review into the existing review/approval flow.

**Architecture:** New aggregate `HplcRun` (+ mobile phases, SST record with analyte rows and injections, samples with replicates, evidence) in Domain. Two pure helpers in Application/Helpers: `HplcSstEvaluator` and `HplcAssayCalculator` (the latter reuses `StandardComparisonCalculator.CalculatePreparationAssay`). `HplcRunService` owns runs/SST/samples/entry/evidence. Submission is a new `HplcMethodAssayRecorder : TestWorkflowSupport` that writes `ParameterResult`s and calls the existing `PersistTestAnalysisAndFinalizeAsync(...)` — exactly like `StandardComparisonRecorder` — so review, approval, summary and CoA work unchanged. `SampleApprovalService`'s passed-SST gate gets one branch for `HplcMethodAssay`.

**Tech Stack:** ASP.NET Core, EF Core + Npgsql, xUnit.

**Spec:** `docs/superpowers/specs/2026-09-29-hplc-chain-design.md` §2 (D1–D5, D11, D12), §5

## Global Constraints
- Clean Architecture; keep it simple; only what this plan says.
- Backend owns every gate; every command re-checks state.
- Formula per replicate per analyte: `A% = (R_test / R̄_std) × (W_std / Th.Wt.std) × (Th.Wt.test / W_test) × ((100 − MC)/100) × P` = `StandardComparisonCalculator.CalculatePreparationAssay(responseTest, meanStd, actWtStd, thWtStd, thWtTest, actWtTest, moisturePercent, purityPercent)`; Th.Wt from the **run's method snapshot**, P and MC from the **SST analyte row snapshot** (copied from the lot).
- Reporting basis (D3) from the sample's `ProductionStage.Role`: `Bulk` → Individual (one reported value and judgement per replicate); anything else (incl. null) → Mean.
- Amount per unit (D2): only when the stage role is `Finished` **and** the item has an `MgPerUnit` spec row for that analyte: `amount = reported A% × LabelClaim / 100`, unit = `LabelClaimUnit`, judged against that row. Other stages: no amount row is evaluated.
- Spec evaluation: the existing `SpecificationEvaluator` (check its API; the same one `StandardComparisonCalculator.Calculate` uses).
- Codes (lab-local via `ILabClock`): run `{ABBR} RUN {nn}/{MM}{yyyy}` and SST `{ABBR} S.S {nn}/{MM}{yyyy}` via `SystemSuitabilityRunCode.NextAsync(issued, abbr, labLocal, infix)`; the SST's `issued` query = existing `SystemSuitabilityRuns.Code` **concatenated** with `HplcSstRecords.Code`, so old and new codes never repeat. Unique indexes on both new `Code` columns.
- Nothing hard-deleted: removing a sample = status `Removed` + reason; evidence is superseded, never replaced.
- New permission `Hplc.Operate` (SystemAdministrator, SectionHead, Analyst) for all workspace writes, seeded like S4's `Solutions.Prepare`.
- Files: evidence through `IFileStorageService.SaveAsync(fileName, bytes)` / `ReadAsync(path)`; allow `application/pdf`, `image/png`, `image/jpeg`; max size = the limit other upload endpoints use (find it; else 20 MB).

## Review Focus
- Assigning a sample **before the SST has passed** (including by calling the API directly) → refused.
- Selecting a mobile-phase preparation that **expires after it was selected** → SST confirmation re-checks and refuses.
- The **same test order** assigned to two open runs → refused.
- A test **returned to analyst** after submission → entry becomes editable again and re-submission supersedes the old result (existing return flow); nothing else to add.
- Replicate count per stage (existing `StageReplicateResolver` for sample preparations) **not met** → submission refused naming the count.

---

## Part A — runs, mobile phases, SST, evidence

### Task A1: Domain + persistence + permission + migration

**Files:** enums `HplcRunStatus { Open, Completed, Abandoned }`, `HplcSstStatus { Pending, Passed, Failed }`, `HplcEvidenceContext { Run, Sst, Sample }`, `HplcEvidenceKind { StandardReport, SampleReport, Other }`, `HplcRunSampleStatus { Assigned, Removed }`; entities below; configs; DbSets; `UserReferenceRegistry`; `PermissionConstants.HplcOperate = "Hplc.Operate"` + seeding; `WorkflowType.HplcMethodAssay` already exists (S3). Migration `AddHplcWorkspace` (schema + permission SQL block like S4).

```csharp
public class HplcRun : IVersionedEntity
{
    public int Id { get; set; }  public uint Version { get; set; }
    public int SectionId { get; set; }
    public string Code { get; set; } = string.Empty;
    public int EquipmentId { get; set; }            public Equipment? Equipment { get; set; }
    public int ChromatographyColumnId { get; set; } public ChromatographyColumn? ChromatographyColumn { get; set; }
    public int HplcMethodId { get; set; }           public HplcMethod? HplcMethod { get; set; }
    public string MethodSnapshotJson { get; set; } = "{}";   // HplcMethodResponse at start (D7)
    public int AnalystUserId { get; set; }
    public DateTime StartedAt { get; set; }
    public HplcRunStatus Status { get; set; }
    public DateTime? ClosedAt { get; set; }
    public string? CloseReason { get; set; }        // required for Abandoned
    public List<HplcRunMobilePhase> MobilePhases { get; set; } = new();
    public HplcSstRecord? Sst { get; set; }
    public List<HplcRunSample> Samples { get; set; } = new();
}
public class HplcRunMobilePhase { public int Id { get; set; } public int HplcRunId { get; set; } public string Channel { get; set; } = ""; public int SolutionPreparationId { get; set; } public SolutionPreparation? SolutionPreparation { get; set; } }

public class HplcSstRecord
{
    public int Id { get; set; }  public int HplcRunId { get; set; }  public HplcRun? HplcRun { get; set; }
    public string Code { get; set; } = string.Empty;
    public HplcSstStatus Status { get; set; }
    public string? FailureReasons { get; set; }
    public int? ConfirmedByUserId { get; set; }  public DateTime? ConfirmedAt { get; set; }
    public int? SignatureId { get; set; }
    public List<HplcSstAnalyte> Analytes { get; set; } = new();
}
public class HplcSstAnalyte
{
    public int Id { get; set; }  public int HplcSstRecordId { get; set; }
    public int HplcMethodAnalyteId { get; set; }  public string AnalyteName { get; set; } = "";
    public int? StandardMaterialId { get; set; }   public Material? StandardMaterial { get; set; }
    public decimal? StandardPurityPercent { get; set; }  public decimal? StandardMoisturePercent { get; set; }  // snapshots from the lot
    public decimal? StandardWeightMg { get; set; }
    public decimal? MeanResponse { get; set; }  public decimal? ComputedRsdPercent { get; set; }
    public decimal? ReportedRsdPercent { get; set; }  public decimal? Resolution { get; set; }  public decimal? TailingFactor { get; set; }
    public decimal? TheoreticalPlates { get; set; }  public decimal? RetentionFactor { get; set; }  public decimal? SignalToNoise { get; set; }  public decimal? PeakToValley { get; set; }
    public bool Passed { get; set; }  public string? FailureReasons { get; set; }
    public List<HplcSstInjection> Injections { get; set; } = new();
}
public class HplcSstInjection { public int Id { get; set; } public int HplcSstAnalyteId { get; set; } public int InjectionNo { get; set; } public decimal Response { get; set; } }

public class HplcRunSample
{
    public int Id { get; set; }  public int HplcRunId { get; set; }  public HplcRun? HplcRun { get; set; }
    public int TestOrderId { get; set; }  public TestOrder? TestOrder { get; set; }
    public HplcRunSampleStatus Status { get; set; }
    public DateTime AssignedAt { get; set; }  public int AssignedByUserId { get; set; }
    public string? RemovedReason { get; set; }  public DateTime? RemovedAt { get; set; }  public int? RemovedByUserId { get; set; }
    public List<HplcSampleReplicate> Replicates { get; set; } = new();
}
public class HplcSampleReplicate { public int Id { get; set; } public int HplcRunSampleId { get; set; } public int ReplicateNo { get; set; } public decimal ActualWeightMg { get; set; } public List<HplcReplicateResponse> Responses { get; set; } = new(); }
public class HplcReplicateResponse { public int Id { get; set; } public int HplcSampleReplicateId { get; set; } public int HplcMethodAnalyteId { get; set; } public decimal Response { get; set; } }

public class HplcEvidence
{
    public int Id { get; set; }  public int HplcRunId { get; set; }  public int? HplcRunSampleId { get; set; }
    public HplcEvidenceContext Context { get; set; }  public HplcEvidenceKind Kind { get; set; }
    public string FilePath { get; set; } = "";  public string FileName { get; set; } = "";  public string ContentType { get; set; } = "";
    public int UploadedByUserId { get; set; }  public DateTime UploadedAt { get; set; }
    public int? SupersededByEvidenceId { get; set; }  public string? SupersedeReason { get; set; }
}
```
Indexes: unique `HplcRun.Code`, unique `HplcSstRecord.Code`, unique `HplcSstRecord.HplcRunId`, filtered unique `HplcRunSample (TestOrderId)` where `Status = Assigned` **is not enough** (a completed run keeps Assigned rows) — so no DB uniqueness for samples; the service checks "not in another **open** run". Index `HplcRun (EquipmentId, Status)`.
- [ ] Build, migration, read it. **Commit** `feat(hplc): workspace entities and permission`

### Task A2: `HplcSstEvaluator` (pure, TDD)
`backend/MicroLIMS.Application/Helpers/HplcSstEvaluator.cs`:
```csharp
public record SstCriteria(decimal? MaxRsdPercent, decimal? MinResolution, decimal? MaxTailingFactor, decimal? MinTheoreticalPlates,
    decimal? MinRetentionFactor, decimal? MinSignalToNoise, decimal? MinPeakToValley, int StandardInjections);
public record SstEntered(IReadOnlyList<decimal> Responses, decimal? ReportedRsdPercent, decimal? Resolution, decimal? TailingFactor,
    decimal? TheoreticalPlates, decimal? RetentionFactor, decimal? SignalToNoise, decimal? PeakToValley);
public record SstOutcome(decimal MeanResponse, decimal? ComputedRsdPercent, bool Passed, IReadOnlyList<string> FailureReasons);
public static class HplcSstEvaluator { public static SstOutcome Evaluate(string analyteName, SstCriteria c, SstEntered e); }
```
Rules: responses count must equal `StandardInjections` ("{analyte}: {n} injections required, {m} entered."); each > 0; mean; computed RSD (sample SD, n−1; null when n = 1 — reuse the RSD helper `TitrationEngine`/`StandardComparisonCalculator` use); **RSD gate uses the computed RSD** (reported kept for comparison, as today); each criterion that is non-null must have its entered value present ("{analyte}: resolution is required.") and within limit ("{analyte}: resolution 1.8 is below 2.0."). Tests: pass, each criterion fail, missing value, wrong injection count, RSD gate uses computed not reported.
**Commit** `feat(engine): hplc system suitability evaluator`

### Task A3: `HplcRunService` part A (TDD)
`backend/MicroLIMS.Application/Services/HplcRunService.cs` + DTOs; tests `HplcRunServiceTests.cs` (seed method via entities, solutions, preparations Prepared with `ExpiresAt` in future, equipment Hplc, column compatible, reference-standard lot with purity + MC).
```csharp
Task<List<HplcInstrumentDto>> GetInstrumentsAsync(int userId, CancellationToken ct = default);
  // Equipment.Type == Hplc in user's sections. State: Running (has Open run) | Unavailable (CalibrationDueDate < lab today, or the linked EquipmentInventory — if Equipment links to one by Code or FK, check — is OutOfService/Retired; give Reason) | Available. Includes active run summary (code, method abbr, analyst, sample count, SST status).
Task<List<HplcMethodOptionDto>> GetMethodOptionsAsync(int userId, CancellationToken ct = default);   // active methods + count of eligible test orders
Task<HplcRunDto> StartRunAsync(StartHplcRunRequest r, int userId, CancellationToken ct = default);
  // r: EquipmentId, HplcMethodId, ChromatographyColumnId, MobilePhases[{Channel, SolutionPreparationId}]
  // Equipment Hplc, Available, no Open run; method active, same section; column active, compatible with equipment (CompatibleEquipment contains it);
  // one preparation per method channel, each: effective Prepared (not expired), Type MobilePhase, SolutionMasterId == the channel's solution, HplcMethodId == this method.
  // Snapshot = serialized HplcMethodResponse; run code; creates HplcSstRecord (Pending, code allocated now) with one HplcSstAnalyte per method analyte.
Task<HplcRunDto> GetRunAsync(int runId, int userId, CancellationToken ct = default);   // everything the screens need incl. gate reasons: CanConfirmSst + reason, CanAssignSamples + reason
Task<HplcRunDto> SaveSstAsync(int runId, SaveSstRequest r, int userId, CancellationToken ct = default);
  // per analyte: StandardMaterialId (usable ReferenceStandard lot linked to the analyte's StandardEntryId, with purity and MC present - else "Lot {batch} has no moisture content recorded."), StandardWeightMg > 0, Responses, reported values. Snapshots P and MC. Status stays Pending. Only while Pending and run Open.
Task<HplcRunDto> ConfirmSstAsync(int runId, ConfirmRequest r, int userId, string? ip, CancellationToken ct = default);
  // requires a current (not superseded) StandardReport evidence on the SST; re-checks mobile-phase preparations still unexpired; evaluates every analyte with HplcSstEvaluator; signs (SignatureMeaning.SuitabilityRunPerformed, entity "HplcSstRecord"); Passed/Failed with reasons. Final.
Task<HplcEvidenceDto> UploadEvidenceAsync(int runId, int? runSampleId, HplcEvidenceContext ctx, HplcEvidenceKind kind, string fileName, string contentType, byte[] content, int userId, CancellationToken ct = default);
Task<HplcEvidenceDto> SupersedeEvidenceAsync(int evidenceId, string reason, string fileName, string contentType, byte[] content, int userId, CancellationToken ct = default);
Task<(byte[] Content, string ContentType, string FileName)> DownloadEvidenceAsync(int evidenceId, int userId, CancellationToken ct = default);
Task<HplcRunDto> AbandonRunAsync(int runId, string reason, int userId, CancellationToken ct = default);   // Open only; reason required; samples keep their rows
Task<List<HplcRunListItem>> GetRunHistoryAsync(int equipmentId, int userId, CancellationToken ct = default);
```
Tests: every rule above, plus Review Focus line 2 (preparation expires between selection and SST confirm).
**Commit** `feat(hplc): runs, mobile phases, system suitability and evidence`

---

## Part B — samples, entry, submission

### Task B1: `HplcAssayCalculator` (pure, TDD)
```csharp
public record AssayReplicateInput(int ReplicateNo, decimal ActualWeightMg, decimal Response);
public record AssayStandard(decimal MeanResponse, decimal StandardWeightMg, decimal PurityPercent, decimal MoisturePercent);
public record AssayReplicateResult(int ReplicateNo, decimal AssayPercent);
public record AssayAnalyteResult(int HplcMethodAnalyteId, string AnalyteName, IReadOnlyList<AssayReplicateResult> Replicates, decimal? MeanAssayPercent);
public static class HplcAssayCalculator
{
    public static AssayAnalyteResult Calculate(int analyteId, string name, decimal thWtStdMg, decimal thWtTestMg, AssayStandard std, IReadOnlyList<AssayReplicateInput> reps);
    public static decimal AmountPerUnit(decimal assayPercent, decimal labelClaim) => assayPercent * labelClaim / 100m;
    public static bool IsIndividualBasis(ProductionStageRole? role) => role == ProductionStageRole.Bulk;
    public static bool JudgesAmountPerUnit(ProductionStageRole? role) => role == ProductionStageRole.Finished;
}
```
Test with hand-checked numbers: R_test 1000, R̄_std 1000, W_std 50.5, Th.std 50, Th.test 200, W_test 202, MC 0.5, P 99.5 → `(1)(1.01)(0.990099…)(0.995)(99.5)` = **99.0025** (4 dp). Amount per unit with LC 500 → 495.0125. Mean of replicates. Basis helpers per role.
**Commit** `feat(engine): hplc assay calculator`

### Task B2: `HplcRunService` part B (TDD)
```csharp
Task<List<EligibleTestDto>> GetEligibleTestsAsync(int runId, string? search, int userId, CancellationToken ct = default);
  // TestOrders whose TestDefinition.WorkflowType == HplcMethodAssay && HplcMethodId == run.HplcMethodId, in user's sections, not finalized (find the field PersistTestAnalysisAndFinalizeAsync sets - CurrentStep Ready - and the cancelled/voided states), not Assigned in another Open run. Row: testOrderId, sample number, batch, product, test code, received at, stage name.
Task<HplcRunDto> AssignSamplesAsync(int runId, List<int> testOrderIds, int userId, CancellationToken ct = default);   // run Open + SST Passed ("Sample assignment is locked until system suitability passes.")
Task<HplcRunDto> RemoveSampleAsync(int runId, int runSampleId, string reason, int userId, CancellationToken ct = default); // not yet submitted; Status Removed + reason
Task<HplcSampleEntryDto> GetSampleEntryAsync(int runSampleId, int userId, CancellationToken ct = default);
  // identity, linked SST code + status, method Th.Wt per analyte (read-only), required replicate count (StageReplicateResolver), replicates, basis (Mean/Individual), preview results (calculator + spec evaluation, marked preview), evidence, Editable flag (false once submitted and not returned), CanSubmit + reason.
Task<HplcSampleEntryDto> SaveReplicatesAsync(int runSampleId, SaveReplicatesRequest r, int userId, CancellationToken ct = default);
  // replace-all replicate rows while editable; weights > 0; one response > 0 per method analyte per replicate.
```
**Commit** `feat(hplc): sample assignment and replicate entry`

### Task B3: Submission recorder + approval gate + run completion (TDD)
- `backend/MicroLIMS.Application/Workflows/TestWorkflow/HplcMethodAssayRecorder.cs` (`: TestWorkflowSupport`, wired the way `StandardComparisonRecorder` is wired — find where it is constructed/registered and the public service method that exposes it):
  `Task<TestWorkflowResult> SubmitAsync(int runSampleId, string password, string? comment, int userId, string? ip)`.
  Checks: run Open, SST Passed, sample Assigned, replicate count == required, every analyte response present, a current SampleReport evidence on this sample. Builds, per method analyte with spec rows for the item + test code:
  - Mean basis: one `ParameterResult` for the `PercentLabelClaim` row (ReportedValue = mean A%, Unit "%", readings = one `ResultReading` per replicate `Kind = Replicate, Stage = replicateNo, Value1 = response, ComputedValue = A%`), plus — only when `JudgesAmountPerUnit` and an `MgPerUnit` row exists — one `ParameterResult` for that row (ReportedValue = amount, Unit = LabelClaimUnit).
  - Individual basis: one `ParameterResult` per replicate for the `PercentLabelClaim` row (`ParameterName = "{name} – replicate {n}"`).
  - `ComparisonStatus` from the spec evaluator; `CalculationJson` holds inputs (Th.Wt, W_std, P, MC, R̄_std, weights, responses, basis, run code, SST code, method id).
  - No spec row for an analyte → refuse ("No assay specification for {analyte} on this item.").
  Then `PersistTestAnalysisAndFinalizeAsync(order, WorkflowType.HplcMethodAssay, run.EquipmentId, now, null, results, ResultType.Numeric, password, comment, "hplc method assay", …)` — copy the remaining arguments from `StandardComparisonRecorder`.
- `SampleApprovalService`: in the passed-SST gate, tests with `WorkflowType.HplcMethodAssay` pass when an `HplcRunSample` for the order (Status Assigned) belongs to a run whose `Sst.Status == Passed`; the old `SystemSuitabilityRunId` check stays for other types. Add a test for both.
- `HplcRunService.CompleteRunAsync(runId, userId)`: Open; every Assigned sample finalized (submitted) → Completed.
- Tests: submit happy path (mean), bulk individual, finished with amount per unit (two statuses), stability no amount row, missing report refused, wrong replicate count refused, return-to-analyst then resubmit supersedes (use the existing return service in the test), approval gate.
**Commit** `feat(hplc): send for review, approval gate and run completion`

### Task B4: API
`backend/MicroLIMS.API/Controllers/HplcWorkspaceController.cs` (`api/hplc-workspace`): GET `instruments`, GET `method-options`, POST `runs`, GET `runs/{id}`, GET `instruments/{equipmentId}/runs`, PUT `runs/{id}/sst`, POST `runs/{id}/sst/confirm`, POST `runs/{id}/abandon`, POST `runs/{id}/complete`, GET `runs/{id}/eligible-tests?search=`, POST `runs/{id}/samples`, POST `runs/{id}/samples/{runSampleId}/remove`, GET `samples/{runSampleId}`, PUT `samples/{runSampleId}/replicates`, POST `samples/{runSampleId}/submit`, POST `runs/{id}/evidence` (multipart: file, context, kind, runSampleId?), POST `evidence/{id}/supersede` (multipart + reason), GET `evidence/{id}/file`. Writes `[Authorize(Policy = PermissionConstants.HplcOperate)]`; reads `[Authorize]`. Follow an existing multipart upload endpoint (git grep "IFormFile" -- backend/MicroLIMS.API) for size/type handling. Authorization matrix regenerated (new rows only). **Commit** `feat(api): hplc workspace endpoints`

### Task B5: Verify
Backup LIMSV2 (`LIMSV2_before_hplc_workspace_20260929.dump`), apply migration, full Postgres suite, whole-diff review, memory. **Stop — report before S7 (frontend).**
