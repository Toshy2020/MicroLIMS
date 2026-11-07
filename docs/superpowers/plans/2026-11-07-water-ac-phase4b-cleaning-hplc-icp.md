# Phase 4b — After-cleaning HPLC (MACO) and ICP (ppm limit)

Spec decisions 6, 9, 10, 12. User rulings 2026-11-07:
- **One HPLC / ICP run sample (test order) per swab / machine part.**
- Residue calculation like residual solvents, with the **extraction volume and swabbed area on the method**:
  µg/mL = sample response ÷ mean standard response × standard concentration (µg/mL);
  µg/swab = µg/mL × extraction volume (mL); µg/cm² = µg/swab ÷ swabbed area (cm²).
Depends on Phase 4a (Sample.PreviousProductItemId, CleaningTechniques).

## Backend (job A)

### B1. Method and element master data
- `HplcResultMode.CleaningResidue = 3` (append-only).
- `HplcMethod`: `int? ProductItemId` (+ nav Item, FK Restrict) and `decimal? SwabAreaCm2`.
  Reuse `SampleSolutionVolumeMl` as the extraction volume (mL).
- `HplcMethodAnalyte`: `decimal? MacoLimit`, `MacoUnit? MacoUnit` (new enum `MacoUnit { UgPerSwab = 0, UgPerCm2 = 1 }`).
  Reuse `StandardConcentrationUgPerMl`.
- `TestDefinitionIcpElement`: `decimal? CleaningLimitPpm`.
- `ParameterResult.SpecificationId` → `int?` (FK stays, nullable). Update `ActiveParameterResultRow.SpecificationId`
  and `ParameterResultDetailDto.SpecificationId` to `int?`. Nothing else reads it (checked).
- One migration `CleaningResidueHplcAndIcp`.
- `HplcMethodService` create/update (and `HplcMethodResponse`, so the run snapshot `MethodSnapshotJson` carries the
  new fields): CleaningResidue mode requires a Finished Product `ProductItemId`, `SampleSolutionVolumeMl` > 0, every
  analyte `StandardConcentrationUgPerMl` > 0, `MacoLimit` > 0 and `MacoUnit`; `SwabAreaCm2` > 0 when any analyte uses
  UgPerCm2. Fields are rejected (or ignored and nulled) in other modes. HPLC technique only (not GC).
- ICP test definition save: `CleaningLimitPpm` ≥ 0 when given.

### B2. Calculation
New `Helpers/CleaningResidueCalculator.cs` (pure, tested): per replicate response → µg/mL → µg/swab →
(÷ area) µg/cm² in the analyte's MACO unit; mean over replicates; all responses 0 → "Not detected"
(same rule as `HplcResidualSolventEvaluator.IsNotDetected`). Evaluate NMT MACO with `SpecificationEvaluator`.
Display with 2 decimals plus the unit ("µg/swab" / "µg/cm²").

### B3. Synthetic specifications for item-less AC samples (one place each)
- `HplcSampleEntryContext.LoadAsync` (~line 220): when the sample is After Cleaning and has no Item, build in-memory
  `Specification` objects per method analyte: `Id = 0`, `HplcMethodAnalyteId = analyte.Id`, `LimitType = NotMoreThan`,
  `UpperLimit = MacoLimit`, `Unit` = unit text, `ParameterName = analyte.Name`. `EntryProblem` / `EvaluateRows` get a
  CleaningResidue branch using B2 (no sample weight needed). A non-CleaningResidue method on an AC sample →
  problem "Use a cleaning-residue method for after-cleaning samples."
- `IcpSampleEntryContext.LoadAsync` (~line 125): same for AC samples: one NMT spec per element with a `CleaningLimitPpm`
  (`IcpTestElementId = element.Id`, unit ppm). No element with a limit → problem "No cleaning limits are set on this
  ICP test's elements."
- Recorders (`HplcMethodAssayRecorder`, `IcpMethodAssayRecorder`): write `SpecificationId = spec.Id == 0 ? null : spec.Id`.

### B4. Preparation: one order per swab
`AfterCleaningWorkflowEngine` prepare, physicochemical lab:
- HPLC and ICP part configurations → **one TestOrder per configuration** (SectionId = lab, one SampleLocation linked to
  the config). UV configurations keep one order per test with a location per part (Phase 3 recorder).
- HPLC: the test's `HplcMethod` must be CleaningResidue and its `ProductItemId` must equal the sample's
  `PreviousProductItemId`; otherwise refuse "No cleaning HPLC method for {product} on test {code}."
- Micro lab unchanged.

### B5. Part name where the order is shown
- HPLC / ICP eligible-sample and run-sample DTOs: add `subjectName` (machine – part from the order's single location).
- `SampleSummaryTestOrderBuilder`: an order with parameter results and exactly one location shows the part name with
  the test name and does not render an empty location row.

### B6. Tests
Calculator (µg/swab, µg/cm², not detected, pass/fail at the MACO), method validation, HPLC and ICP entry on an AC
sample end to end with null SpecificationId, prepare creating one order per HPLC/ICP config and refusing a method for
another product, summary label.

## Frontend (job B)
- HPLC method editor: "Cleaning residue" result mode (HPLC only); product picker (Finished Product items);
  extraction volume (the existing sample-solution volume field, relabelled in this mode); swabbed area (cm²);
  per analyte: standard concentration (existing field), MACO and unit (µg/swab | µg/cm²).
- ICP test elements editor: "Cleaning limit (ppm)" column.
- After-cleaning part config: HPLC / ICP tests need no limit editor (caption "Limit from the method" /
  "Limit from the ICP test"); UV keeps the qualitative editor from 4a.
- HPLC / ICP workspace sample rows: show `subjectName` under the sample reference.
- Component tests.

## Verification (stop point)
Postgres suite, tsc, vitest, build; backup + migrate LIMSV2; browser: method → part config → receive AC in FP with
HPLC → prepare (N orders) → HPLC run → results vs MACO → review.
