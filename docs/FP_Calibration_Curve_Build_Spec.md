# Calibration Curve (ICP-OES) - Build Spec

Scoping: `E:\files\files\microlims-finished-product-calibration-curve-scoping.md`. Recon + gate:
`docs/FP_Calibration_Curve_Phase0_Recon.md`. Local branch `feat/fp-hplc-foundation`, never pushed.

## Revision 3 (Syngistix basis confirmed, 2026-09-19)

The user confirmed scoping Q2: **the Syngistix method template already applies sample weight, digest volume and
dilution, and reports ppm in the sample** - mg/kg for solids, **mg/L for liquids**. S3 is unblocked and changes:
- `ReportedConcentrationBasis` gains `SamplePpm` (required for CalibrationCurve tests; `SolutionMgPerL` is reserved
  and refused). The LIMS never applies weight, volume or dilution.
- Over-range and below-LOQ can no longer be computed (the standards are in solution mg/L): the analyst ticks the
  flags shown on the Syngistix report.
- Per sample the analyst enters only each element's ppm, the average unit weight (solids, g) or dose volume
  (liquids, mL), and the analysis time. No sample weight / volume / DF entry.
- The calibration run (S1) is unchanged: its standards, blanks and checks are in solution mg/L, as reported.

## Revision 2 (review fixes, 2026-09-19)

From `E:\files\files\prompt-fp-calibration-curve-s1-hardening.md`:

| # | Change | Why |
|---|---|---|
| A1 | One-way run **withdrawal** (status, reason, signature), `IsUsable` on views | a sign-once run needs a way to be retired without editing it |
| A2 | `decimal` everywhere, recovery = `(Measured*100)/Nominal`, **no square root** in r/r² comparison | exact inclusive-bound comparisons (TC13, TC14) |
| A3 | **Lab-local time** (`Lab:TimeZoneId`, TimeProvider) for run codes, yearly sequence and expiry; new `CalibrationAt` | UTC put runs near midnight in the wrong month/year; expiry must be judged on the calibration date |
| A4 | Required checks are Test Master booleans | the validated method decides which checks run |
| A5 | **Preview** endpoint (same gate code, nothing persisted) | analyst reviews computed values before signing |
| A6 | `ReportedConcentrationBasis` on the test; S3 blocked on scoping Q2 | the LIMS must apply DF/weight/volume exactly once |
| A7 | `CalMaxRunAgeHours` | a sample may only use a recent calibration |
| A8 | Reviewer sees the run and report at result review | transcription verification without a run-approval step |
| A9 | Notes on rounding and per-analyte overrides | open lab decision / design rule |
| A10 | TC12-TC19 | cover the above |

## Gate 0 decisions (user, 2026-09-19)

- G1 **Sign once, like the System Suitability Run**: created in one signed action with the Syngistix
  report attached, immutable afterwards, gate computed at creation. No draft/approve. A failed run is kept;
  the analyst creates a new run. After signing the only permitted change is a one-way **withdrawal** (A1).
  TC11 becomes: there is no edit endpoint.
- G2 Code `{MethodAbbreviation} CAL {seq:00}/{MM}{yyyy}` (e.g. `ICP-MIN CAL 01/092026`), sequence per
  method per calendar year - reuse/generalise `Helpers/SystemSuitabilityRunCode.cs` (infix parameter).
  Month, year and the per-year sequence use the **lab-local** date (A3).
- G4 A calibration / ICV standard with no ExpiryDate cannot be used (analyte gate fails).
- G3 Syngistix specifics are configuration on the Test Master (r vs r², which checks, acceptance values).

## Enums (append-only; Domain/Enums)

- `EquationType` + `CalibrationCurve`. `WorkflowType` + `ElementalAssay`. `EquipmentType` + `IcpOes`.
  `CdsSoftware` + `PerkinElmerSyngistix`.
- New: `CalibrationEntryMode` {InstrumentReported, LimsFitted (reserved, not built)},
  `CorrelationType` {R, RSquared}, `CalibrationCheckType` {Blank, Icv, Ccv, InternalStandard},
  `AnalyteView` {Axial, Radial}, `ResultBasis` {MgPerKg, MgPerUnit, PercentLabelClaim},
  `CalibrationRunStatus` {Active, Withdrawn}, `ReportedConcentrationBasis` {SolutionMgPerL}.

## Lab-local time (A3)

- Configuration `Lab:TimeZoneId`, default `Africa/Cairo`; if `TimeZoneInfo.FindSystemTimeZoneById` fails, fall
  back to `Egypt Standard Time`. Egypt observes daylight saving - never hard-code an offset.
- A small Application service (e.g. `ILabClock`: `DateTimeOffset UtcNow`, `DateTime ToLabLocal(DateTime utc)`,
  `DateOnly LabToday`) built on the registered `TimeProvider` (`TimeProvider.System` in production, a fake in tests).
  No such abstraction existed before this revision.
- `SystemSuitabilityRunCode` is shared: SST codes also switch to the lab-local month/year and yearly sequence.
  Existing SST codes are not rewritten.

## Slice S1 - Test Master analytes + criteria, ICP-OES equipment, Calibration Run (backend)

TestDefinition additions (nullable columns; required when EquationType = CalibrationCurve):
`CalibrationEntryMode`, `CalMinCorrelation` numeric(10,6), `CalCorrelationType`, `CalMinStandards` int,
`CalCheckRecoveryLowPercent` / `CalCheckRecoveryHighPercent` (ICV+CCV window), `CalBlankMax` (mg/L; null = analyte
LOQ), `CalIsRecoveryLowPercent` / `CalIsRecoveryHighPercent`,
**`CalRequireBlank` / `CalRequireIcv` / `CalRequireCcv` / `CalRequireInternalStandard`** (bool, A4),
**`ReportedConcentrationBasis`** (A6; v1 only `SolutionMgPerL`, shown read-only in the Test Master),
**`CalMaxRunAgeHours`** (int >= 1, default 24, A7).
Validation when EquationType = CalibrationCurve: WorkflowType must be ElementalAssay, MethodAbbreviation required,
CalMinCorrelation in (0, 1], CalCorrelationType, CalMinStandards >= 1, both check-window bounds (low <= high),
ReportedConcentrationBasis and CalMaxRunAgeHours >= 1 required; CalRequireInternalStandard requires the IS window.
Any combination of required checks is allowed.

`TestAnalyte` (child of TestDefinition): Id, TestDefinitionId, Element (string 20, e.g. "Zn"),
WavelengthNm numeric(10,4), View (AnalyteView), LoqMgPerL numeric(18,6), DisplayOrder, IsActive.
Unique (TestDefinitionId, Element, WavelengthNm). CRUD endpoints under masterdata/test-definitions/{id}/analytes
(SectionHead/Admin), deactivate instead of delete once used by a run.

Equipment: IcpOes requires a CdsSoftware exactly like Hplc.

`CalibrationRun`: Id, Code (unique), TestDefinitionId, SectionId (= test's section), EquipmentId (must be IcpOes,
same section, active), CalibrationStandardMaterialId, IcvStandardMaterialId (nullable), **`CalibrationAt`**
(entered from the Syngistix report; rejected if more than 5 minutes in the future; audited), PerformedByUserId,
PerformedAt (record creation time), SignatureId (sign against "TestDefinition"/test.Id like SST - ElectronicSignatures
is append-only, set the navigation not the id), Comment, `AnalytesPassed` int, `AnalytesTotal` int,
**`Status` (Active/Withdrawn), `WithdrawnAt`, `WithdrawnByUserId`, `WithdrawalReason`, `WithdrawalSignatureId`** (A1).
`CalibrationRunDocument` (one per run, mandatory): StorageKey via IFileStorageService
(`calibration-runs/{runId}/{guid}{ext}`), OriginalFileName, ContentType, SizeBytes, ContentSha256 (hex),
UploadedByUserId, UploadedAt - follow EquipmentDocument/MaterialDocument exactly.
`CalibrationRunAnalyte`: Id, CalibrationRunId, TestAnalyteId, Element / WavelengthNm / View (snapshots),
CorrelationValue numeric(10,6), CorrelationType, NumberOfStandards, LowestStandardMgPerL / HighestStandardMgPerL
numeric(18,6), Passed, FailureReasons (string).
`CalibrationRunCheck`: Id, CalibrationRunAnalyteId, CheckType, SequencePosition int, NominalMgPerL numeric(18,6)
(null for Blank), MeasuredMgPerL numeric(18,6), RecoveryPercent **numeric(28,10)** (computed; null for Blank), Passed.

### Numeric rules (A2)
- Every concentration, correlation, threshold and recovery is C# `decimal` in entities **and request DTOs**
  (never `double`) and `numeric` in Postgres.
- Recovery % = `(Measured * 100m) / Nominal`; Nominal must be `> 0`. The gate compares the full-precision value with
  the inclusive bounds; the stored column is for display/audit.
- Correlation must be in (0, 1]. **No square root**: run reports r, criterion r² -> pass if `r * r >= min`;
  run reports r², criterion r -> pass if `r2 >= min * min`; same type -> `value >= min`.

### Gate per analyte (one function, used by create and preview)
Server-side, never typed:
1. correlation rule above;
2. NumberOfStandards >= CalMinStandards;
3. every ICV/CCV recovery within [low, high] inclusive;
4. every Blank measured <= (CalBlankMax ?? analyte LOQ);
5. every InternalStandard recovery within the IS window (IS rows with no configured window -> request rejected);
6. **each required check type (A4) has at least one row for the analyte**, else the analyte fails with a reason;
7. calibration standard and ICV standard (if given) have an ExpiryDate and `ExpiryDate.Date >= lab-local date of
   CalibrationAt` - otherwise every analyte fails with "standard expired / no expiry date".
Analytes are independent (P2): a failing Zn does not fail Ca.

### Service and API
`CalibrationRunService` (Application) + `ICalibrationRunService`:
- `PreviewAsync(request, userId)` (A5): same validation, section scope and gate function as create; returns
  per-analyte/per-check recovery, pass/fail and reasons, and the standards-expiry outcome. No file, no signature,
  no persistence, no audit row, no sequence consumed.
- `CreateAsync(request, file stream, userId, ip)`: validate, recompute the gate (never trusts a preview), sign,
  generate the code (lab-local, unique-index retry), save the document (hash) - one save.
- `WithdrawAsync(id, reason, password, userId, ip)` (A1): reason required (min 10 non-blank characters), signed,
  one-way; a second withdraw is rejected. Nothing else on the run changes.
- `GetAll(filter: test, passed, status)`, `GetById`, `GetReportDetails` (as SST report; the report prints which
  checks were required, CalibrationAt, and the withdrawal block when withdrawn).
- Section scope via `EnsureCalibrationRunAccessAsync`.
Controller `api/calibration-runs`: `POST` multipart (`payload` JSON + `report` file; refuse missing file - TC10),
`POST preview` (JSON only), `POST {id}/withdraw`, `GET` list, `GET {id}`, `GET {id}/report`,
`GET {id}/document` (download, hash verified). Create/preview use `TestWorkflow.Execute` (same as SST create);
**withdraw uses `Samples.Approve`** (the Section Head approval permission - a supervisory retirement of a signed
record; no new permission added). View records carry `Status`, withdrawal fields and computed
**`IsUsable` = analyte.Passed && run.Status == Active**. Never return entities with User navigations.

### Tests
InMemory + Postgres: TC2, TC3, TC4, TC5, TC9, TC10, r vs r² conversion, per-analyte independence, code format +
sequence, missing expiry, IS without window rejected, equipment not ICP-OES rejected, **TC12-TC19** (below), and one
SST code-boundary test. Existing tests that assumed "at least one ICV or CCV" follow the configurable rule.

## Slice S2 - frontend for S1

FP Test Master: CalibrationCurve equation reveals the analyte list editor, the acceptance block, the four
required-check switches (warning when neither ICV nor CCV is required), the max run age, and the reported
concentration basis read-only ("Requires calibration run" locked on). Equation Types page entry. FP Instruments:
ICP-OES type + Syngistix CDS. Calibration Runs page (LABORATORY menu): enter header (incl. CalibrationAt),
standards, analyte grid with expandable check rows (paste-from-spreadsheet optional) -> **"Review computed results"**
screen from the preview endpoint (entered values beside computed recovery/pass/fail) -> confirm -> sign -> create
with the report file. Values are sent exactly as typed - no client-side arithmetic. List with per-analyte chips and
a Withdrawn badge; withdraw action (Section Head) with reason + signature; printable report page.

## Slice S3 - element results (backend) / S4 - result entry + summary/CoA (frontend)

Unblocked by Revision 3 (Syngistix reports ppm in the sample).

- `Specification` += ResultBasis (MgPerKg / MgPerUnit / PercentLabelClaim), LabelClaim, LabelClaimUnit,
  ConversionFactor (default 1.0), TestAnalyteId, **SampleMatrix** (Solid / Liquid - per item assignment; Solid =
  ppm is mg/kg, Liquid = ppm is mg/L).
- Per test order (one entry for all elements): **unit amount** - average unit weight Wu (g) for Solid, dose volume
  Vd (mL) for Liquid - and **`AnalysedAt`** (from the report). No sample weight, digest volume or dilution.
- Element result per (TestOrder, Specification): reported ppm `C`, flags **OverRange** and **BelowLoq** ticked by
  the analyst from the report, the linked `CalibrationRunAnalyteId`, and a calculation snapshot:
  - Solid: `mg per unit = C × Wu / 1000` (C in mg/kg, Wu in g)
  - Liquid: `mg per dose = C × Vd / 1000` (C in mg/L, Vd in mL)
  - `Result (claim) = mg per unit × CF`; `%LC = Result (claim) / LC × 100`
  - Reported value depends on ResultBasis: MgPerKg -> C (for liquids the spec's unit is mg/L), MgPerUnit -> claim,
    PercentLabelClaim -> %LC. All decimal, compared unrounded (A9).
  - **TC1 (revised):** C = 8500 ppm (mg/kg), Wu = 1.2500 g, CF = 1.0, LC = 10.0 mg -> 10.625 mg/unit,
    106.25 %LC. Liquid case: C = 200 mg/L, Vd = 5 mL, LC = 1.0 mg -> 1.0 mg/dose, 100 %LC.
- Entry label: "Concentration in the sample as reported by Syngistix (ppm: mg/kg for solids, mg/L for liquids)."
- Flags: OverRange -> RequiresReview (never extrapolated; the analyst dilutes and reruns - TC6). BelowLoq -> reported
  as "<LOQ"; against an NMT limit it conforms, against Range/NLT/Target it goes to RequiresReview (TC7).
- Only analytes with **`IsUsable`** (passed, run Active) of the order's method and section are selectable (TC8),
  and `AnalysedAt` must be within `[CalibrationAt, CalibrationAt + CalMaxRunAgeHours]` of the linked run (A7).
- **Withdrawal consequence (A1):** withdrawing a run sets `RequiresReview` on every linked element result not yet
  released, and produces a list of already-released results for QA follow-up.
- Approval gate, review return, projection, summary and CoA per element.
- **Reviewer view (A8):** the element-result review screen shows the run code, analyte gate status and failure
  reasons, the entered checks, a Withdrawn banner if applicable, and a link to download the attached report
  (`GET api/calibration-runs/{id}/document`, hash verified). The reviewer's approve action for elemental results
  records the audit statement "values checked against the attached report". (Design only.)

### S3 implementation contract (2026-09-19)

Split: **S3a** (data + recording + calculation) then **S3b** (downstream). Mirror the HPLC assay result wherever
possible: `Domain/Entities/HplcAssayResult.cs`, `TestWorkflowEngine.RecordHplcAssayResultAsync`,
`TestWorkflowController` `record-hplc-result`, `SampleApprovalService` (~line 150-170 HPLC gate),
`ReviewService` (~line 110-170 HPLC return), `ResultProjectionService.UpsertFromHplcAssayResultAsync`,
`SampleSummaryService` HPLC detail.

**S3a**
- Enum `SampleMatrix` {Solid, Liquid}.
- `Specification` += `TestAnalyteId` (FK TestAnalyte, nullable), `ResultBasis` (nullable), `SampleMatrix` (nullable),
  `LabelClaim` numeric(18,6), `LabelClaimUnit` string(20), `ConversionFactor` numeric(18,6) NOT NULL default 1.
  `SpecificationService.ValidateAsync`: when the spec's test has EquationType CalibrationCurve -> TestAnalyteId
  required and must belong to that test, one parameter per (item, analyte), ResultBasis + SampleMatrix required,
  LimitType must be Range / NotMoreThan / NotLessThan / TargetWithTolerance, ConversionFactor > 0, LabelClaim > 0
  required when ResultBasis = PercentLabelClaim. Other tests: these fields must be null (ConversionFactor 1).
  Extend the specifications create/update requests in `MasterDataController`.
- `ElementalAssayEntry` (one signed entry per test order, all elements together): Id, TestOrderId, SampleMatrix
  (snapshot), UnitAmount numeric(18,6) (g for Solid, mL for Liquid), AnalysedAt (UTC), IsActive, EnteredByUserId,
  EnteredAt, SignatureId (+ navigation; sign against "TestOrder"/order.Id, meaning ResultRecorded), Comment.
- `ElementalAssayResult` (one per element): Id, EntryId, TestOrderId, SpecificationId, CalibrationRunAnalyteId,
  ParameterName / Element snapshots, ReportedPpm numeric(18,6), OverRange bool, BelowLoq bool, MgPerUnit, ResultClaim,
  PercentLabelClaim (numeric(28,10), null when not computable), ReportedValue numeric(28,10) (null when over range /
  below LOQ), ReportedDisplay string (e.g. "10.6 mg", "106.3 %", "<LOQ", "Over range"), ResultBasis snapshot,
  SpecLimit snapshot (canonical text), Unit snapshot, ComparisonStatus, IsActive.
- `POST api/test-workflow/{testOrderId}/record-elemental-result` (TestWorkflow.Execute) body: unitAmount,
  analysedAt, elements[{specificationId, calibrationRunAnalyteId, reportedPpm, overRange, belowLoq}], password,
  comment. Engine method `RecordElementalAssayResultAsync`:
  - order's test WorkflowType ElementalAssay; section scope; not finalized/superseded; no active entry.
  - the order's sample has an item; **every** CalibrationCurve parameter of (item, test) supplied exactly once;
    all parameters share one SampleMatrix (else configuration error).
  - per element: run analyte exists, analyte `Passed`, run `Status == Active`, run's TestDefinition == order's
    test, run section == test section, run analyte's TestAnalyteId == spec.TestAnalyteId,
    `CalibrationAt <= AnalysedAt <= CalibrationAt + CalMaxRunAgeHours`; AnalysedAt not in the future (5 min).
  - UnitAmount > 0; ReportedPpm >= 0; OverRange and BelowLoq not both.
  - calculation (decimal, unrounded): Solid `MgPerUnit = C * Wu / 1000`, Liquid `MgPerUnit = C * Vd / 1000`;
    `ResultClaim = MgPerUnit * CF`; `%LC = ResultClaim / LC * 100` when LC set.
    ReportedValue by basis: MgPerKg -> C, MgPerUnit -> ResultClaim, PercentLabelClaim -> %LC.
  - status: OverRange -> "RequiresReview" (no value, never extrapolated); BelowLoq -> "<LOQ": NotMoreThan ->
    "WithinLimits", other types -> "RequiresReview"; otherwise `SpecificationEvaluator.Evaluate(spec, value)`.
  - order status/current step move exactly as HPLC does after its result; the order's overall outcome is the worst
    element (OutOfSpecification > RequiresReview > WithinLimits).
- Tests: TC1 solid (8500 ppm, 1.25 g, LC 10 mg -> 10.625 mg, 106.25 %), TC1 liquid (200 mg/L, 5 mL, LC 1 mg ->
  1.0 mg, 100 %), TC6 over range, TC7 <LOQ vs NMT and vs Range, TC8 failing analyte / withdrawn run refused,
  run-age window (before CalibrationAt and after max age refused), missing element refused, wrong-method run
  refused, CF applied, spec validation rules. InMemory + one Postgres end-to-end.

**S3b**
- Approval gate: an ElementalAssay order cannot be approved without an active entry; the section head who entered
  it cannot approve it (same rule as HPLC).
- Return to analyst: deactivate the active entry and its results (like HPLC).
- Projection: one ResultRecord per element result (SourceTable "ElementalAssayResult").
- Summary DTO: `ElementalAssayDetailDto` per order (entry: matrix, unit amount, AnalysedAt, entered by/at; per
  element: parameter, element, run code, run analyte pass, ppm, flags, mg/unit, claim, %LC, reported display,
  spec, status) + export text block; CoA uses each element's reported display and status.
- Withdrawal consequence: `CalibrationRunService.WithdrawAsync` sets ComparisonStatus "RequiresReview" on active
  element results linked to the run whose order is not Approved, and returns the list of already-approved orders
  (sample ref, test, element) in the withdraw response for QA follow-up.

## Notes (A9)

- **Rounding (Q4):** today both HPLC and elemental assays compare the **unrounded** value with the limit. This is an
  open lab decision (USP General Notices 7.20 rounds to the limit's decimals before comparing). No code change now.
- **No per-analyte acceptance overrides:** criteria are test-level. Minerals and trace contaminants must be
  configured as **separate tests**, because their acceptance windows usually differ.

## Test cases added in revision 2 (A10)

| # | Case | Expected |
|---|---|---|
| TC12 | Withdraw a run with reason and signature | Status Withdrawn, data unchanged, not `IsUsable`, second withdraw rejected, audited |
| TC13 | Nominal 5.0, measured 5.5, window 90-110 | Recovery exactly 110, Pass |
| TC14 | Run r = 0.999, criterion r² >= 0.998001 | Pass (exact decimal, no sqrt) |
| TC15 | Lab-local boundaries | Created 2026-09-30 22:00 UTC -> month 10 in the code; created 2026-12-31 22:30 UTC -> year 2027, sequence 01 (offsets verified through TimeZoneInfo, not hard-coded) |
| TC16 | Standard expiring 2026-09-30, CalibrationAt 2026-10-01 01:00 lab-local | Every analyte fails with the expiry reason |
| TC17 | Required-check configuration | Only Blank required: analyte with a blank row passes. CalRequireCcv and no CCV row: analyte fails with reason |
| TC18 | Preview | Same results as create; no rows written, no sequence consumed, no file needed |
| TC19 | CalibrationAt in the future | Rejected |
