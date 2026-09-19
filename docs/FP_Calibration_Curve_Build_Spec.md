# Calibration Curve (ICP-OES) - Build Spec

Scoping: `E:\files\files\microlims-finished-product-calibration-curve-scoping.md`. Recon + gate:
`docs/FP_Calibration_Curve_Phase0_Recon.md`. Local branch `feat/fp-hplc-foundation`, never pushed.

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

**S3 is blocked until the user confirms from a sample Syngistix report that the method template reports the
concentration in the measured solution before dilution, weight or volume correction (scoping Q2).**

- `Specification` += ResultBasis, LabelClaim, LabelClaimUnit, ConversionFactor (default 1.0), TestAnalyteId.
- Digest record per test order: W (g), V (mL), Wu (g), **`AnalysedAt`** (from the report).
- Element result per (TestOrder, Specification) with calculation snapshot (scoping §6, TC1). Entry label:
  "Reported concentration in measured solution (mg/L), before dilution, weight or volume correction."
- Only analytes with **`IsUsable`** (passed, run Active) of the order's method and section are selectable (TC8),
  and `AnalysedAt` must be within `[CalibrationAt, CalibrationAt + CalMaxRunAgeHours]` of the linked run (A7).
- OverRange / <LOQ flags -> RequiresReview (TC6/TC7).
- **Withdrawal consequence (A1):** withdrawing a run sets `RequiresReview` on every linked element result not yet
  released, and produces a list of already-released results for QA follow-up.
- Approval gate, review return, projection, summary and CoA per element.
- **Reviewer view (A8):** the element-result review screen shows the run code, analyte gate status and failure
  reasons, the entered checks, a Withdrawn banner if applicable, and a link to download the attached report
  (`GET api/calibration-runs/{id}/document`, hash verified). The reviewer's approve action for elemental results
  records the audit statement "values checked against the attached report". (Design only.)

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
