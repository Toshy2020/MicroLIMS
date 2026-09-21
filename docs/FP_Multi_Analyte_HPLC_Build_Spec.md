# Multi-Vitamin (Multi-Analyte) HPLC Assay - Recon and Draft Build Spec

Branch `feat/fp-hplc-foundation` (local, never pushed). Status: **Gate 0 answered 2026-09-21** - D1 (a), D2 (a), D3 configurable, D4 (b); D5/D6 are Test Master setup (lab input, not blocking).
Follows the rules in `docs/FP_Other_Equation_Types_Build_Spec.md` (decimal only, server computes, compare unrounded,
signed + audited + section-scoped, `ILabClock`, FP tests have no preparation stage).

## Why
Most products are multivitamins. Their assays are all HPLC: the water-soluble vitamins plus A, C, D and E. The
current `HplcAssay` cannot report them:
- **One analyte per test.** `SystemSuitabilityRun` holds one reference standard (one weight, purity, dilution, mean
  area) and `HplcAssayResult` one mean %. A run that quantifies B1, B2, B6 and niacinamide in one injection needs one
  standard and one result per vitamin.
- **Wrong basis.** It reports `% = (A/A_std)(W_std/W)(P/100)(D/D_std)100`, i.e. % of the standard's concentration (an
  API assay). Supplements are released on **amount per unit** and **% of label claim**, often in µg or IU, with a
  salt/ester conversion (e.g. thiamine HCl -> thiamine, retinyl acetate -> IU vitamin A).

## What already exists (recon)
| Need | Existing piece | Reuse |
|---|---|---|
| Several analytes per test | `TestAnalyte` (Element, WavelengthNm, View, LoqMgPerL; used by ICP-OES) | Yes - add HPLC use (see D2) |
| One spec row per analyte with label claim | `Specification.TestAnalyteId`, `ResultBasis` {MgPerKg, MgPerUnit, PercentLabelClaim}, `LabelClaim`, `LabelClaimUnit`, `ConversionFactor` | Yes, unchanged |
| mg/unit and %LC conversion | Elemental path (`TestWorkflowEngine` ~2339): `mpu = c x unitAmount / 1000`, `rc = mpu x ConversionFactor`, `%LC = rc / LC x 100` | Same shape |
| One result per analyte + downstream (review, approval, summary, CoA, projection) | Shared foundation `TestAnalysis` / `ParameterResult` / `ResultReading` | Yes - no downstream work |
| Standard + suitability gate, signed, relink guard | `SystemSuitabilityRun` (single standard) + `SystemSuitabilityService` | Extend with per-analyte rows (D1) |
| Per-analyte run rows | `CalibrationRunAnalyte` pattern (run child row per TestAnalyte, snapshot, pass/fail) | Pattern to copy |

`HplcAssay` (single analyte, API % of standard) stays as it is for the pharmaceuticals.

## Proposed design

- **New workflow/equation type** `HplcMultiAnalyte` (routing like the other FP types). No steps; section FP.
- **Analytes** (Test Master): each vitamin is a `TestAnalyte` row of the test: name (e.g. "Vitamin B1 (thiamine)"),
  detection wavelength, display order; the ICP-only `View` becomes optional for HPLC analytes.
- **Suitability run with one row per analyte** (`SystemSuitabilityRunAnalyte`): reference standard material + purity
  snapshot, standard weight (mg), standard dilution (mL), standard mean area, and the CDS criteria for that peak (RSD,
  tailing, plates, resolution to the critical neighbour). The run passes only if **every** analyte row passes its
  criteria (criteria per analyte on the Test Master, null = not checked, as today). Code, signature, immutability,
  failed runs kept, relink guard: unchanged.
- **Specifications**: one row per analyte (`TestAnalyteId`), `ResultBasis` MgPerUnit or PercentLabelClaim (usually a
  Range, e.g. 90-150 % LC), `LabelClaim` + `LabelClaimUnit` (mg / µg / IU), `ConversionFactor`.
- **Calculation per analyte** (C_s = standard concentration in mg/mL):
  - `C_s = W_std x (P / 100) / D_std`
  - per injection: `amount per unit (mg) = (A_u / A_s) x C_s x D_sample / W_sample x unit weight` (solids; W_sample mg,
    D_sample mL, unit weight = average unit weight mg); liquids: `x unit volume / V_sample` instead.
  - `result = amount x ConversionFactor` (the factor also converts mg to µg or IU, e.g. 1000 for µg);
    `%LC = result / LabelClaim x 100`; reported per `ResultBasis`; evaluated by `SpecificationEvaluator`.
  - Mean over preparations/injections (D3); RSD between preparations shown.
- **Entry**: one signed action for the whole test: analysed at, instrument, the linked passed run, sample weight(s)
  and dilution(s), average unit weight (D4), and a grid of areas: rows = analytes, columns = injections/preparations
  (paste from the CDS export). One `TestAnalysis`, one `ParameterResult` per analyte, `ResultReading` per injection
  (Value1 = area, ComputedValue = amount).

## Gate 0 - decisions

| # | Decision | Options | Recommendation |
|---|---|---|---|
| D1 | Standards | (a) one suitability run carries a row per vitamin; (b) one run per vitamin, several runs linked to an order | **(a)** - one run per injection sequence, matches how the CDS reports |
| D2 | Analytes | (a) reuse `TestAnalyte` (optional ICP fields); (b) new HPLC analyte table | **(a)** |
| D3 | Replicates | how many sample preparations and injections per preparation, and is there an RSD/agreement limit between preparations? | lab input - build configurable (preparations n, injections m, optional max RSD) |
| D4 | Unit weight | (a) typed with the result; (b) taken from the sample's weight variation mean when present, else typed | **(b)**, with the source recorded |
| D5 | Method grouping | which vitamins are run together (e.g. water-soluble B + C in one method, A/D/E in another)? | lab input - does not change the build, only the Test Master setup |
| D6 | Units | label claims in mg, µg and IU; conversion via `ConversionFactor` per vitamin | as proposed; lab to confirm the IU factors they use |

## Gate 0 answers (user, 2026-09-21)
D1 one run with a row per vitamin; D2 reuse `TestAnalyte`; D3 configurable preparations/injections/RSD; D4 unit weight
from the sample's weight variation when there is one, otherwise typed.

## Slice contract

### M1 - analytes and suitability run per analyte (backend)
- Enums (append): `WorkflowType.HplcMultiAnalyte`, `EquationType.HplcMultiAnalyte` (pair rule as the other FP types).
  `RequiresSystemSuitability` must be true. No steps.
- `TestAnalyte`: `View` becomes nullable (still required for CalibrationCurve analytes, not allowed for
  HplcMultiAnalyte); `WavelengthNm` = detection wavelength for HPLC (> 0); `LoqMgPerL` optional for HPLC (nullable).
  New nullable per-analyte SST criteria: `SstMaxRsdPercent`, `SstMinResolution`, `SstMaxTailingFactor`,
  `SstMinTheoreticalPlates` (null = not checked). The analyte CRUD endpoints used by Calibration Curve accept
  HplcMultiAnalyte tests too, with these rules.
- Test Master (HplcMultiAnalyte, nullable, defaulted on save): `HplcPreparations` (default 2, 1-10),
  `HplcInjectionsPerPreparation` (default 2, 1-10), `HplcMaxPreparationRsdPercent` (null = not checked, > 0).
- New `SystemSuitabilityRunAnalyte` (table `SystemSuitabilityRunAnalytes`): Id, SystemSuitabilityRunId (FK cascade),
  TestAnalyteId (FK), snapshot `AnalyteName` + `WavelengthNm`, `ReferenceStandardMaterialId` (FK Material),
  `StandardPurityPercent` (snapshot from Material.Purity, required > 0), `StandardWeightMg`, `StandardDilution`,
  `StandardMeanArea` (all > 0), `RsdPercent?`, `Resolution?`, `TailingFactor?`, `TheoreticalPlates?`, `Passed`,
  `FailureReasons`. numeric(28,10) for purity/area/criteria, (18,6) for weight/dilution.
- `SystemSuitabilityService.CreateAsync` for an HplcMultiAnalyte test: the request carries one analyte row per
  **active** TestAnalyte of the test (exactly once each; unknown/inactive/duplicate rejected); the run-level single
  standard fields are not used (store the first analyte's values or zero - keep the columns non-null as today, do not
  migrate them); each row is evaluated against its analyte's criteria with the same comparison rules as the run-level
  criteria today; run `Passed` = every row passed; `FailureReasons` lists them prefixed by analyte name. Same code,
  signature, section, equipment, column and standard-expiry checks as today. Single-analyte tests are unchanged.
  Get/list/report DTOs include the analyte rows.
- Tests: create pass/fail per analyte; missing/duplicate/inactive analyte rejected; criteria null = not checked;
  single-analyte HplcAssay runs unchanged; Postgres round trip.

### M2 - multi-analyte result (backend)
- Endpoint `record-hplc-multi-analyte-result` (signed, `TestWorkflowExecute`, section check): {AnalysedAt,
  EquipmentId?, SampleMatrix (Solid/Liquid), Preparations: [{SampleAmount (mg or mL), SampleDilutionMl}],
  UnitAmount? (mg per unit, or mL per dose for liquids), Areas: [{TestAnalyteId, PreparationIndex, InjectionIndex,
  Area}], Password, Comment?}. Exactly `HplcPreparations` preparations and, for every spec'd analyte,
  preparations x injections areas (> 0).
- Standard: the order's linked passed run (same checks as `HplcAssay`, run analyte row for each analyte).
  `C_s = W_std x (P/100) / D_std` (mg/mL) per analyte.
- Unit amount (D4): for Solid, if the sample has a finished weight variation result (active ParameterResult, not
  NextStageRequired) use its `ReportedValue` (mean tablet weight / mean net content, mg) and reject a typed
  UnitAmount that is supplied; otherwise UnitAmount is required. Liquid: UnitAmount (mL per dose) always typed.
  Record `UnitAmountSource` ("WeightVariation #{testOrderId}" or "Typed") in CalculationJson; UnitAmount on
  TestAnalysis.
- Per analyte, per injection: `amount (mg per unit) = (A_u / A_s) x C_s x D_sample x UnitAmount / SampleAmount`;
  per preparation = mean of its injections; `mpu` = mean of preparations; `result = mpu x ConversionFactor`;
  `%LC = result / LabelClaim x 100`; reported per ResultBasis (MgPerUnit or PercentLabelClaim; MgPerKg not allowed);
  status by `SpecificationEvaluator`. If `HplcMaxPreparationRsdPercent` is set and preparations >= 2 and the RSD of
  the preparation means exceeds it -> that analyte's status is `RequiresReview` with a reason. Display 1 dp.
- Storage: one TestAnalysis (AnalysisType HplcMultiAnalyte, SampleMatrix, UnitAmount, EquipmentId); one
  ParameterResult per analyte spec (ValidityRecordItemId = run analyte row id); readings Kind Replicate, Stage =
  preparation, Index = injection, Value1 = area, ComputedValue = amount per unit (after conversion). Completion path
  as the other FP types. Relink guard: the run cannot be relinked once an active result exists (as dissolution).
- Tests: worked example checked by hand; 1 vs 2 preparations; RSD exactly at limit passes; WV unit weight used and a
  typed one rejected when WV exists; typed required when not; liquid path; IU/µg via ConversionFactor; missing area
  rejected; run for another test/section/failed rejected; Postgres round trip.

### M3 - screens
Test Master (type, preparations/injections/RSD, analyte rows with wavelength and SST criteria), System Suitability
page (one row per vitamin for multi-analyte tests), result entry (preparations, unit weight shown with its source,
area grid analytes x injections with paste), specification dialog (analyte + result basis + label claim for
HplcMultiAnalyte tests), summary/cards labels by analysis type.
