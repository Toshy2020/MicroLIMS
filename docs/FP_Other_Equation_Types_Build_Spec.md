# FP Equation Types beyond HPLC Assay and Calibration Curve - Build Spec

Recon: `docs/FP_Other_Equation_Types_Phase0_Recon.md`. Style and rules follow
`docs/FP_Calibration_Curve_Build_Spec.md`. Local branch `feat/fp-hplc-foundation`, never pushed.
## Gate 0 decisions (user, 2026-09-19)

- G1, G2, G3, G4, G8: **accepted as recommended.**
- Product forms: **tablet, capsule, soft capsule, liquid** (no gummies, no effervescent-specific tests).
- Pharmacopoeias: **USP (online)** and **BP (online)** - the two presets for G2.
- G6: **compare unrounded** (current behaviour); only the displayed value is rounded.
- G7: **omega-3 by GC is in scope** (Tier 3 GC internal-standard ratio moves up); acid value / peroxide value
  **not sure - left out** until confirmed.
- One disintegration tester (the duplicate line on the instrument list is the same instrument).
- Portfolio (2026-09-19): **most products are multivitamins and share one common test set**; the exceptions are
  the pharmaceutical products **sildenafil, glimepiride, fluconazole**. Consequences for G5:
  - Tier 1 (measurement, LOD/ash, appearance/ID), disintegration, weight variation and the HPLC/ICP assays cover the
    multivitamin portfolio - build them first and make them reusable across all items.
  - Dissolution and content uniformity are expected only on the three pharmaceutical products (confirm per product
    against its monograph); G2 presets therefore need both the USP dietary-supplement chapters (multivitamins) and the
    USP/BP medicinal chapters (pharmaceuticals).
- Lab answers (2026-09-19): multivitamin assays are the **water-soluble vitamins, vitamin A, C, D and E**;
  **all three pharmaceuticals have dissolution**; **content uniformity is deferred** (drop CU from T7 for now;
  weight variation stays).
- Lab answers (2026-09-19, second round): **dissolution is finished by HPLC**; **all vitamin assays are HPLC**
  (including vitamin C). Consequences: T5 UV Assay is **dropped** (no UV test confirmed); T8 titration is **dropped**
  for now (KF only if a water-content test is confirmed); dissolution takes its standard response from the linked
  passing `SystemSuitabilityRun`, exactly like the HPLC assay.
- Gap found (2026-09-19): the current `HplcAssay` is **single-analyte** (one SST standard, one primary spec) and
  reports `% = (A/A_std)(W_std/W)(P/100)(D/D_std)100`, not mg/unit or %LC. Multivitamin HPLC runs usually quantify
  several vitamins per injection against per-vitamin standards and report against the label claim. Open decision:
  configure one test per vitamin on the current HPLC assay, or build a multi-analyte HPLC assay with label claim.
- Still open: the full common multivitamin test list beyond the assays (appearance, LOD, disintegration, weight
  variation?).

## Global rules (all types)

- All quantities are C# `decimal` (entities and request DTOs) and Postgres `numeric`; weights/volumes numeric(18,6),
  percentages and computed values numeric(28,10). Multiply before divide. No `double`, no `Math.Sqrt` on decimals in
  comparisons; SD uses a decimal Newton-Raphson square root to 10 dp (display/RSD only).
- The server computes everything; the frontend sends values exactly as typed (as in S2/S4).
- Compare unrounded; display rounded to `ReportDecimals` (G6) with `MidpointRounding.AwayFromZero`.
- Every result is signed (`IElectronicSignatureService`, sign against the TestOrder, set the navigation), audited
  automatically, section-scoped (`EnsureTestOrderAccessAsync`), and "now" comes from `ILabClock`.
- FP tests have no preparation stage: weights, dilutions and conditions are entered with the result.
- Instruments are picked from FP Instruments (Equipment in the FP section); consumables/standards from Material Stock.

## G1 - Shared result foundation (recommended)

Generalise the elemental pair (recon F1) into one foundation used by every new type **and** the Calibration Curve:

- `TestAnalysis` (was `ElementalAssayEntry`): Id, TestOrderId, AnalysisType (= WorkflowType), EquipmentId?,
  AnalysedAt, common inputs as typed nullable columns (UnitAmount, SampleMatrix) + `ConditionsJson` (jsonb: time,
  temperature, rpm, medium, apparatus...), IsActive, EnteredByUserId, EnteredAt, Signature, Comment,
  ValidityRecordId? (link to G4 record).
- `ParameterResult` (was `ElementalAssayResult`): Id, TestAnalysisId, TestOrderId, SpecificationId, ParameterName
  snapshot, ReportedValue numeric(28,10)?, ReportedDisplay, Unit, SpecLimit snapshot, ComparisonStatus, Flags
  (OverRange, BelowLoq, ...), `CalculationJson` (jsonb snapshot of every intermediate), StageReached?, IsActive.
- `ResultReading` (new): Id, ParameterResultId, Kind (Replicate, Unit, Vessel, TimePoint, Weight, Titration...),
  Index, Stage?, TimePointMinutes?, Value1/Value2/Value3 numeric(28,10), Text?, computed per-reading value, Passed?.
- One downstream implementation for all types: approval gate (active analysis required; enterer cannot approve),
  return-to-analyst (deactivate analysis + results), projection (one ResultRecord per ParameterResult), summary DTO
  (`AnalysisDetailDto` with parameter results + readings), export text, CoA (one row per ParameterResult).
- **Impact on Calibration Curve S3:** a mechanical rename/move - `ElementalAssayEntry` -> `TestAnalysis`
  (UnitAmount, SampleMatrix, AnalysedAt kept as typed columns), `ElementalAssayResult` -> `ParameterResult`
  (MgPerUnit/ResultClaim/PercentLabelClaim/ReportedPpm move into `CalculationJson` + ReportedValue;
  CalibrationRunAnalyteId becomes `ValidityRecordItemId`). Endpoint, calculation and tests unchanged in behaviour.
  LIMSV2 has no elemental results yet, so the data migration is trivial (copy rows if any). HPLC stays on
  `HplcAssayResult` this round (migrate later if wanted).
- **Size estimate for the foundation:** one backend slice of roughly the size of S3a+S3b together (entities +
  migration + generic downstream in ~8 places + move of elemental + tests; ~2.5-3.5 k lines incl. tests) and one
  frontend slice (generic summary/CoA rendering + the reusable `UnitEntryGrid`, recon F10).

Alternative (not recommended): a new result table per type - ~8 downstream edits per type, eight times.

## G4 - Validity records (recommended: common base)

`ValidityRecord` base pattern (shared code, not necessarily one table): Code `{ABBR} {INFIX} nn/MMyyyy` via the shared
generator, method (TestDefinition), section, equipment, `PerformedAt` (lab-local, not future), signature, status
Active/Withdrawn (one-way signed withdrawal, `Samples.Approve`), optional attachment (hash), `ValidUntil`, computed
`IsUsable` = passed && Active && now <= ValidUntil, preview endpoint using the same gate function. Concrete records:
- **UV suitability** (`UVS`): blank absorbance, standard preparations (2) with weight/absorbance, gate below.
- **Titrant standardization** (`STD`): titrant lot (Material), primary standard lot (Material, ReferenceStandard with
  Purity), n replicate titrations, computed N, RSD limit, ValidUntil = PerformedAt + configured days.
- **KF titer** (`KFT`): n water-standard titrations, computed F, RSD limit, ValidUntil.
- **Dissolution apparatus check** (`DIS`): apparatus type, medium text, volume, temperature, rpm, equipment - no pass/fail
  math beyond temperature/rpm within configured tolerance.
`SystemSuitabilityRun` and `CalibrationRun` keep their tables; the base is shared services/helpers + UI shell.

## Equation types

New `WorkflowType` values (routing by entry shape): `Measurement`, `Gravimetric`, `Qualitative`, `UnitTimed`,
`ExternalStandardAssay` (UV now; HPLC may move later), `Dissolution`, `DosageUniformity`, `Titration`.
New `EquationType` values: `Measurement`, `GravimetricLoss`, `GravimetricResidue`, `Qualitative`, `Disintegration`,
`UvAssay`, `Dissolution`, `ContentUniformity`, `WeightVariation`, `TitrationAssay`, `KarlFischer`
(+ `AcidValue`, `PeroxideValue` only if G7 = yes). Pairs validated in MasterDataController like HPLC/CC.

### T1 Numeric Measurement (pH, density, viscosity, refractive index, thickness, diameter, fill volume)
- Test Master: `ReplicateCount` n (1-30), unit, `EvaluationBasis` {Mean, EachValue, Min, Max}, equipment type filter.
- Inputs: n readings. Outputs: mean, min, max, SD (n-1), RSD = SD/mean × 100 (null when n < 2 or mean = 0).
- Evaluation: Range / NMT / NLT / Target via `SpecificationEvaluator` on the basis value; EachValue = every reading
  must be within (worst status wins).
- **Worked example:** pH 6.02, 6.05, 5.98 -> mean 6.0166666667, min 5.98, max 6.05, SD 0.0351188458, RSD 0.5837 %.
  Spec Target 6.0 ± 0.5 (5.5-6.5) on Mean -> WithinLimits, display "6.02".
- Tests: basis Mean/Each/Min/Max; boundary 6.5 exactly WithinLimits; 6.5000001 OOS; n = 1 has no SD.

### T2 Gravimetric % Loss / Residue (LOD, sulfated ash, friability*)
- Modes: `PercentLoss = (W1 - W2) / W1 × 100`, `PercentResidue = W2 / W1 × 100`. Optional tare: container, container +
  sample, container + dried/residue -> W1, W2 derived. Conditions (time, temperature, rpm/revolutions) as configured
  fields in ConditionsJson. Replicates optional (mean reported).
- **Worked example (LOD):** container 25.1234 g, + sample 27.1234 g (W1 = 2.0000), after drying 27.0334 g
  (W2 = 1.9100) -> loss 4.50 %. Spec NMT 5.0 % -> WithinLimits. **Residue:** W1 1.0000 g, residue 0.0020 g -> 0.20 %.
- Tests: W2 > W1 rejected for loss; W1 = 0 rejected; boundary 5.000 % NMT 5.0 WithinLimits.
- *Friability only if the lab confirms a friability tester (recon open question 4).

### T3 Qualitative / Identification (appearance, colour, odour, ID by IR/TLC)
- Result: `Conforms` / `DoesNotConform` + observation text (required when DoesNotConform), evaluated against
  Qualitative (expected text snapshot shown to the analyst and stored) or PresenceAbsence. Optional attachment (IR
  overlay, TLC photo) with hash. Not the Observation (pathogen) workflow (recon F4).
- **Example:** expected "White to off-white, round biconvex effervescent tablets"; analyst selects Conforms and types
  "White round biconvex tablets, citrus odour" -> WithinLimits, CoA shows "Complies".
- Tests: DoesNotConform -> OutOfSpecification; DoesNotConform without text rejected; expected text snapshot unchanged
  after the spec is edited.

### T4 Timed Multi-Unit (disintegration)
- Test Master: time limit (min), medium, temperature; staged rule (G3): stage 1 = 6 units all disintegrate within the
  limit; if 1-2 fail, stage 2 = 12 more units and at least 16 of 18 must pass; 3+ failures at stage 1 -> fail.
- Inputs per unit: time (min) or "not disintegrated"; conditions.
- **Example:** 12, 14, 15, 13, 16, 18 min, limit 30 -> stage 1 Complies. Stage 2 case: one unit 32 min -> 12 more,
  all pass -> 17/18 >= 16 -> Complies.
- Tests: exactly 30.0 min passes; 2 fails at S1 -> S2; 3 fails at S1 -> fail; 16/18 passes, 15/18 fails.

### T5 UV Assay (external standard)
- Same input model and formula as HPLC (recon F5), response = absorbance:
  `% = (A_sample / A_std_mean) × (W_std / W_sample) × (P / 100) × (D_sample / D_std) × 100`.
- Validity record `UVS` gate: blank absorbance <= limit; standard absorbance within [low, high]; agreement of two
  standard preparations `|RF1 - RF2| / mean(RF) × 100 <= limit` where RF = A / W.
- **Worked example:** W_std 50.0 mg, D_std 100, P 99.5 %, A_std_mean 0.5000; W_sample 250.0 mg, D_sample 500,
  A_sample 0.4950 -> 98.505 % -> display "98.5 %". Standard agreement: 0.5000/50.0 = 0.0100000, 0.4925/49.5 =
  0.0099494949; difference 0.5063 % <= 2.0 -> pass.
- Tests: std absorbance at the exact bounds passes; agreement exactly 2.0 % passes; withdrawn/expired UVS not usable.

### T6 Dissolution
- Apparatus record (`DIS`) + per vessel `% dissolved = (A_u / A_s) × C_s × V × DF / LC × 100` (C_s mg/mL, V mL, LC mg
  per unit). Multi-time-point with media replacement: `mg_n = C_n × V + V_s × Σ_{i<n} C_i`.
  Finish UV (absorbance) or HPLC (area) - same formula with the response ratio.
- Acceptance (typed criteria on the spec, G2/G3): Q and the profile S1: 6 units each >= Q + 5; S2: 12 units, mean >= Q
  and none < Q - 15; S3: 24 units, mean >= Q, at most 2 units < Q - 15, none < Q - 25.
- **Worked example:** A_s 0.500, C_s 0.0200 mg/mL, V 900 mL, DF 1, LC 18 mg -> % = A_u × 200. Vessels A_u 0.4500,
  0.4400, 0.4600, 0.4450, 0.4550, 0.4350 -> 90.0, 88.0, 92.0, 89.0, 91.0, 87.0 %; Q = 80 -> every unit >= 85 ->
  S1 Complies. Media replacement: C_1 0.0100, C_2 0.0180 mg/mL, V 900, V_s 10 mL -> mg_2 = 16.2 + 0.1 = 16.3 mg ->
  90.5556 %.
- Tests: unit at exactly Q + 5 passes S1; S1 fail -> S2 needed; S2 mean exactly Q passes; S3 two units < Q - 15 pass,
  three fail; any unit < Q - 25 fails.

### T7 Uniformity of Dosage Units and Weight Variation
- **Content uniformity (USP <905> / EP 2.9.40):** `AV = |M - X̄| + k × s`; M = X̄ when 98.5 <= X̄ <= 101.5, else 98.5 or
  101.5; k = 2.4 (n = 10), 2.0 (n = 30); S1 passes if AV <= L1; S2 (30 units) passes if AV <= L1 and every unit within
  [(1 - 0.01 L2) M, (1 + 0.01 L2) M]. L1, L2, k are configuration (defaults 15.0, 25.0, 2.4/2.0), per pharmacopoeia (G2).
  Unit values are %LC per unit from 10/30 unit preparations through HPLC or UV (lab input needed - recon inputs list).
- **Weight variation (EP 2.9.5):** n (default 20); bands are configuration rows (form, mean-weight range, % limit), e.g.
  tablets >= 250 mg ±5 %; pass if at most 2 units outside the limit and none outside twice the limit.
- **Worked example (CU):** 98, 99, 100, 101, 102, 97, 103, 99, 100, 101 %LC -> X̄ 100.0, s = √(30/9) = 1.8257418584,
  M = 100.0, AV = 2.4 × 1.8257 = 4.3818 <= 15.0 -> Complies. Boundary: X̄ 97.0, s 2.0 -> M 98.5, AV = 1.5 + 4.8 = 6.3.
- **Worked example (WV):** mean 500 mg, ±5 % band 475-525, twice 450-550; two units at 470 and 530, none outside
  450-550 -> Complies; a third unit outside ±5 % -> fails.
- Tests: X̄ exactly 98.5 and 101.5 (M = X̄); AV exactly 15.0 passes; S2 unit at exactly (1 - 0.25)M passes; WV band
  edges.

### T8 Titration and Karl Fischer
- **Standardization** (`STD` record): `N = (W_std × P / 100) / (V × Eq)` per replicate (W mg, Eq mg/meq), mean N,
  RSD <= limit, ValidUntil. **Assay:** `% = (V_s - V_b) × N × F × 100 / W` (F mg per meq, W mg); mg/unit and %LC via
  ResultBasis / LabelClaim / ConversionFactor (as elemental).
- **KF (volumetric):** titer `F = W_water(mg) / V(mL)` (`KFT` record); `% water = V × F × 100 / W(mg)`.
- Optional (G7): acid value `V × N × 56.11 / W(g)`, peroxide value `(V_s - V_b) × N × 1000 / W(g)`.
- **Worked examples:** KHP 408.44 mg, P 100 %, V 20.00 mL, Eq 204.22 -> N = 0.1000. Assay (ascorbic acid, F 8.806
  mg/meq): V_s - V_b = 20.00 mL, N 0.1000, W 200.0 mg -> 17.612 mg -> 8.806 %. KF: 50.0 mg water uses 10.00 mL ->
  F 5.000 mg/mL; sample 500.0 mg uses 0.80 mL -> 0.80 % water.
- Tests: expired/withdrawn standardization not usable; RSD exactly at limit passes; V_b > V_s rejected.

### Tier 3 (later, only if confirmed)
Related substances (area normalisation / vs standard with RRF, reporting threshold, totals); GC internal-standard ratio
(omega-3) - the lab has a GC (recon open question 1).

### T6 slice contract (2026-09-19, HPLC finish, single time point)
- Enums (append): `WorkflowType.Dissolution`, `EquationType.Dissolution`, `LimitType.DissolutionQ`.
- Specification (per item): `LimitType.DissolutionQ`, Q stored in `LowerLimit` (0 < Q <= 100, %), `LabelClaim` (> 0)
  with `LabelClaimUnit` "mg" = LC per unit; SpecLimit text "Q = {Q} %". Exactly one dissolution spec per test/item.
- Test Master (TestDefinition, nullable, required for Dissolution): stage table offsets with USP <711> / EP 2.9.3
  immediate-release defaults - `DissolutionS1Offset` 5, `DissolutionS2MinOffset` 15, `DissolutionS3MinOffset` 25,
  `DissolutionS3MaxBelowS2Min` 2; `ConditionFields` reused for apparatus/rpm/medium/temperature/time;
  `RequiresSystemSuitability` must be true (standard comes from the SST run). No steps.
- Standard: order must be linked to a PASSED SST run of the same test/section (same checks as the HPLC assay).
  `C_s = W_std x (P/100) / D_std` (mg/mL).
- Per vessel: `% = (A_u / A_std_mean) x C_s x V x DF x 100 / LC` (V medium volume mL, DF sample dilution, default 1).
  Multiply before divide. Media replacement / multi-point profile: out of scope now.
- Stages: S1 6 vessels; S2 +6 (12 total); S3 +12 (24 total). Lab decision 2026-09-19: **always continue** to the
  next stage when S1/S2 do not conform, even when S3 can no longer be met (USP <711> wording); the only fail is at S3. Pure `DissolutionStageEvaluator` returns
  {Complies, NextStageRequired, DoesNotComply} + stage reached + reasons, from the rules in T6.
- Flow: `record-dissolution-result` creates the signed TestAnalysis with the stage-1 vessels. If the outcome is
  NextStageRequired the order is NOT finalized (stays Running, StageReached = 1). `record-dissolution-stage` (signed,
  new vessels only, conditions/volume unchanged) appends the next stage's readings to the same active analysis,
  re-evaluates all units and finalizes when Complies/DoesNotComply or after S3. Reported value = mean % of all units
  (0 dp display), status WithinLimits / OutOfSpecification. Readings: Kind Vessel, Stage, Value1 = area,
  ComputedValue = %, Passed = per-unit check of the stage it was entered in.

### T4 slice contract (2026-09-21)
User decisions: the time limit is set **per product on the Specification** (like Dissolution Q); the analyst records
the **time of each unit** in minutes, or marks it "not disintegrated".
- Enums (append): `WorkflowType.Disintegration`, `EquationType.Disintegration`, `LimitType.DisintegrationTime = 9`.
  The two types go together (MasterDataController pair rule, create and update), as for Dissolution.
- Specification (per item): `LimitType.DisintegrationTime`, limit in `UpperLimit` (minutes, > 0), Unit "min";
  SpecLimit text "NMT {T} min". LabelClaim, TestAnalyteId, ResultBasis and SampleMatrix must be empty; ConversionFactor
  1.0. Exactly one specification per test/item. DisintegrationTime is allowed only on Disintegration tests, and
  Disintegration tests allow only DisintegrationTime. `SpecificationEvaluator` treats it like DissolutionQ (not
  evaluated generically).
- Test Master (TestDefinition, nullable int, defaulted on save for Disintegration tests; USP <701> / EP 2.9.1):
  `DisintegrationStage1Units` 6, `DisintegrationStage2Units` 12, `DisintegrationMaxStage1Failures` 2,
  `DisintegrationMinPassTotal` 16. Rules: units >= 1; 0 <= max failures < stage-1 units;
  1 <= min pass total <= stage-1 + stage-2 units. `RequiresSystemSuitability` must be false. `ConditionFields`
  reused (medium, temperature, discs...). No steps. Equipment optional, FP section (same check as the other types).
- Pure `DisintegrationStageEvaluator.Evaluate(IReadOnlyList<decimal?> unitMinutes, decimal limitMinutes, config)`:
  a unit passes when its time is not null and <= limit (compared unrounded; exactly at the limit passes).
  Stage 1 (count = stage-1 units): 0 failures -> Complies; 1..max failures -> NextStageRequired; more ->
  DoesNotComply (no stage 2: the pharmacopoeia only repeats on 1-2 failures; the dissolution "always continue"
  rule does not apply here). Stage 2 (count = stage-1 + stage-2 units): passed >= min pass total -> Complies, else
  DoesNotComply. Any other count throws. Reuses `DissolutionStageOutcome`. Returns stage reached, outcome, passed
  count, longest time among disintegrated units, reasons.
- Endpoints (`TestWorkflowExecute`, section check, signed with password): `record-disintegration-result`
  {AnalysedAt, EquipmentId?, Conditions, UnitMinutes (list of decimal?, null = not disintegrated), Password, Comment?}
  -> exactly stage-1 units; `record-disintegration-stage` {UnitMinutes, Password, Comment?} -> exactly stage-2
  units, only while the active analysis is NextStageRequired. Present times must be > 0.
- Stored as in Dissolution: one TestAnalysis (AnalysisType Disintegration, ConditionsJson) with one ParameterResult;
  readings Kind `Unit`, Index, Stage, Value1 = minutes (null when not disintegrated), Text "Not disintegrated" when
  null, Passed. ParameterResult: StageReached; ComparisonStatus WithinLimits / OutOfSpecification /
  NextStageRequired; ReportedDisplay "Complies" / "Does not comply" / "Stage 2 required"; ReportedValue = longest
  time among disintegrated units (null if none); CalculationJson = limit, config, units, passed count, longest,
  outcome, reasons. While NextStageRequired the order is not finalized; the approval gate already blocks a pending
  stage. The completion path (Result row, projection, Ready, auto-submit for review) matches Dissolution.
- Tests: exactly 30.0 min passes, 30.0000001 fails; 1 and 2 failures at S1 -> S2; 3 -> DoesNotComply at S1;
  16/18 Complies, 15/18 DoesNotComply; not-disintegrated counts as a failure; custom Test Master config honoured;
  wrong unit counts rejected; stage call rejected when not pending; spec rules; Postgres round trip of both calls.

### T7 weight variation slice contract (2026-09-21, USP <2091>)
User decisions: **USP <2091>** (Weight Variation of Dietary Supplements) for **every** product, the three
pharmaceuticals included until content uniformity is built (<905> not built); capsules and softgels record the
**gross and empty-shell weight** of every unit. Limits are presets on the Test Master (editable), never hard-coded in
the evaluator. The chapter text below is from memory of USP <2091> - check it against the lab's current USP.
- Enums (append): `WorkflowType.WeightVariation`, `EquationType.WeightVariation`, `LimitType.WeightVariation = 10`,
  new `DosageForm { Tablet, HardCapsule, SoftCapsule }`.
- Specification (per item): `LimitType.WeightVariation`, new nullable column `Specification.DosageForm` (required for
  this limit type, not allowed on others), Unit "mg"; no limits/label claim on the spec (they come from the Test
  Master preset). Exactly one per test/item; allowed only on WeightVariation tests and vice versa. SpecLimit text:
  tablet "USP <2091>: tablets, limit by average weight"; capsules "USP <2091>: net content 90-110 % of average".
- Test Master (TestDefinition, nullable, defaulted on save for WeightVariation tests; USP <2091> preset):
  `WvUnitCount` 20; tablets: `WvTabletBand1MaxMg` 130, `WvTabletBand1Percent` 10, `WvTabletBand2MaxMg` 324,
  `WvTabletBand2Percent` 7.5, `WvTabletBand3Percent` 5, `WvTabletMaxOutside` 2; capsules: `WvCapsuleInnerPercent` 10,
  `WvCapsuleOuterPercent` 25, `WvCapsuleS1MaxOutside` 2, `WvCapsuleS1MaxForRetest` 6, `WvCapsuleS2ExtraUnits` 40,
  `WvCapsuleS2MaxOutside` 6. Percentages numeric(28,10), mg numeric(18,6). Validation: counts >= 1, 0 < percents,
  Band1MaxMg < Band2MaxMg, inner < outer, S1MaxOutside < S1MaxForRetest <= unit count, S2MaxOutside < unit count +
  extra units. RequiresSystemSuitability false; ConditionFields reused (balance ID etc.); no steps.
- Pure `WeightVariationEvaluator` (decimal, compare unrounded, deviation % = |x - mean| x 100 / mean):
  - **Tablet** (inputs: unit weights mg): mean of all units; band = mean <= Band1MaxMg -> Band1Percent; <= Band2MaxMg
    -> Band2Percent; else Band3Percent (P). Complies if count(dev > P) <= TabletMaxOutside and no dev > 2P; else
    DoesNotComply. Single stage. Exactly P passes.
  - **Hard capsule** (inputs: gross + shell per unit; net = gross - shell, shell > 0, shell < gross): step A - if every
    gross weight is within InnerPercent of the mean gross weight -> Complies (USP shortcut on intact capsules).
    Otherwise step B on net contents: any dev > OuterPercent -> DoesNotComply; count(dev > InnerPercent) <=
    S1MaxOutside -> Complies; <= S1MaxForRetest -> NextStageRequired (ExtraUnits more); else DoesNotComply.
  - **Soft capsule**: step B directly (no intact shortcut).
  - **Stage 2** (capsules, unit count + extra units): new mean of all net contents; Complies if count(dev > Inner) <=
    S2MaxOutside and no dev > Outer; else DoesNotComply.
  - Returns stage reached, outcome (reuse `DissolutionStageOutcome`), mean, band percent used, step A result,
    per-unit deviation and pass flags, reasons. Wrong unit counts throw.
- Endpoints (`TestWorkflowExecute`, section check, signed): `record-weight-variation-result` {AnalysedAt,
  EquipmentId? (balance), Conditions, Units: [{WeightMg} | {GrossMg, ShellMg}], Password, Comment?} -> exactly unit
  count; `record-weight-variation-stage` {Units, Password, Comment?} -> exactly extra units, only while
  NextStageRequired. Shape must match the spec's dosage form (tablet: weight only; capsules: gross + shell).
  Stage 2 uses the criteria and dosage form snapshotted at stage 1 (as disintegration).
- Stored as disintegration: one TestAnalysis (AnalysisType WeightVariation) + one ParameterResult; readings Kind
  `Unit`, Index, Stage, Value1 = weight or gross mg, Value2 = shell mg (capsules), ComputedValue = weight or net mg,
  Value3 = deviation % from the final mean, Passed = within the inner / band limit. ReportedValue = final mean
  (tablet weight or net content, mg); ReportedDisplay "Complies" / "Does not comply" / "Stage 2 required";
  CalculationJson = dosage form, criteria snapshot, step A result, means, band, units, outcome, reasons.
- Frontend display fix: the summary/result-card reading labels must key on the **analysis type**
  (Disintegration -> "Time (min)"; WeightVariation -> "Weight (mg)" / "Gross (mg)", "Shell (mg)", "Net (mg)",
  "Deviation %"), not on reading kind `Unit`, which both types use.
- Tests: tablet band edges (mean exactly 130 -> 10 %, 130.000001 -> 7.5 %, exactly 324 -> 7.5 %); dev exactly P
  passes; 2 outside P pass, 3 fail; one beyond 2P fails; capsule step A all within 10 % of gross -> Complies without
  net check; step B 2 outside pass, 3-6 -> stage 2, 7 fail, one > 25 % fails; stage 2 on the 60-unit mean, 6/60 pass,
  7/60 fail; softgel skips step A; shell >= gross rejected; wrong shape for the dosage form rejected; stage-1
  snapshot used at stage 2; spec rules; Postgres round trip.

## G3 - Staged evaluation engine (recommended now, in the Dissolution slice)
One pure engine: input = typed criteria + unit values per stage; output = stage reached, outcome, reasons. Stage state
on `ParameterResult.StageReached`; units in `ResultReading` with `Stage`. The analyst adds the next stage's units only
when the engine says so. Used by T4, T6, T7. Keeping Multi-Stage manual would leave dissolution/UDU unevaluated.

## Slices (dependencies in brackets)

1. **F0 foundation backend** - TestAnalysis / ParameterResult / ResultReading, generic downstream, move Calibration
   Curve S3 onto it (contract below). Validity-record helpers come with the first record that needs them (UV, slice 4). [G1]
2. **F0 frontend** - generic summary/CoA rendering, reusable `UnitEntryGrid` (paste + keyboard). [1]
3. **Tier 1** - T1 Measurement, T2 Gravimetric, T3 Qualitative (+ equipment types G8). [1, 2]
4. **T5 UV Assay** + UVS record. [1, 2]
5. **T6 Dissolution** + DIS record + staged engine (G3); T4 Disintegration on the same engine. [1, 2, 4]
6. **T7 CU / Weight Variation** (staged engine, G2 configuration). [5]
7. **T8 Titration / KF** + STD / KFT records. [1, 2]
8. Tier 3 if confirmed. [1]
Optional: instrument calibration-due gate on analysis date (recon F7).

## Decisions for Gate 0

| # | Decision | Options | Recommendation |
|---|---|---|---|
| G1 | Result storage | (a) shared TestAnalysis/ParameterResult/ResultReading, Calibration Curve moved onto it; (b) one table per type | **(a)** - one downstream implementation; CC move is mechanical and LIMSV2 has no elemental results yet |
| G2 | Pharmacopoeia for UDU / WV / dissolution / disintegration | (a) configuration per item assignment (chapter + edition + limits); (b) one global default | **(a)**, with USP and EP presets the Section Head selects; limits never hard-coded |
| G3 | Staged evaluation | (a) build the engine now; (b) keep Multi-Stage manual | **(a)**, delivered with Dissolution |
| G4 | Validity records | (a) common base (helpers, code, withdrawal, preview, UI shell); (b) separate records | **(a)** |
| G5 | Order | foundation -> Tier 1 -> UV -> Dissolution/Disintegration -> CU/WV -> Titration/KF -> Tier 3 | as listed, **trimmed to the tests the lab confirms** (inputs list) |
| G6 | Rounding | (a) compare unrounded (today); (b) round to the limit's decimals before comparing (USP GN 7.20) | lab decision; build `ReportDecimals` + `RoundBeforeCompare` per spec so either can be chosen |
| G7 | Softgel/oil tests (acid value, peroxide value, omega-3 GC) | in / out | depends on products; a GC exists on the instrument list |
| G8 | Equipment types to add | from the instrument list: UvVis, DissolutionTester, DisintegrationTester, KarlFischer, Titrator, Viscometer, Refractometer, Polarimeter, ConductivityMeter, MeltingPoint, Oven, Furnace, Gc, Aas, DigestionMicrowave, Caliper/Micrometer | add all present on the list; **not** hardness/friability (no instrument listed) |

## F0 contract - shared result foundation (backend)

Goal: the elemental assay runs on the generic tables with **no API or DTO change** (frontend untouched), and the
generic downstream is ready for the next types.

- `TestAnalysis` (table `TestAnalyses`): Id, TestOrderId (FK), AnalysisType (WorkflowType, int), EquipmentId?,
  AnalysedAt (UTC), UnitAmount numeric(18,6)?, SampleMatrix?, ConditionsJson jsonb?, ValidityRecordType string(40)?,
  IsActive, EnteredByUserId, EnteredAt, SignatureId (+ navigation), Comment. Index (TestOrderId, IsActive).
- `ParameterResult` (table `ParameterResults`): Id, TestAnalysisId (FK cascade), TestOrderId, SpecificationId (FK),
  ParameterName, ReportedValue numeric(28,10)?, ReportedDisplay, Unit, SpecLimit, ResultBasis?, ComparisonStatus,
  OverRange bool, BelowLoq bool, ValidityRecordItemId int? (elemental: CalibrationRunAnalyteId, FK to
  CalibrationRunAnalytes nullable), CalculationJson jsonb (elemental: reportedPpm, mgPerUnit, resultClaim,
  percentLabelClaim, element, runCode...), StageReached int?, IsActive.
- `ResultReading` (table `ResultReadings`): Id, ParameterResultId (FK cascade), Kind (enum ReadingKind {Replicate, Unit,
  Vessel, TimePoint, Weight, Titration}), Index, Stage?, TimePointMinutes numeric(18,6)?, Value1/Value2/Value3
  numeric(28,10)?, Text string(500)?, ComputedValue numeric(28,10)?, Passed bool?. (Empty for elemental.)
- Migration `SharedResultFoundation`: create the three tables; LIMSV2 has 0 elemental rows, but copy any existing
  ElementalAssayEntries/Results rows (preserving values; update ResultRecords SourceTable "ElementalAssayResult" ->
  "ParameterResult" with the new ids), then drop `ElementalAssayResults` and `ElementalAssayEntries`. Down reverses.
- Code moves: `TestWorkflowEngine.RecordElementalAssayResultAsync` writes TestAnalysis (AnalysisType ElementalAssay)
  + ParameterResults; `CalibrationRunService.WithdrawAsync` flags ParameterResults by ValidityRecordItemId;
  SampleApprovalService / ReviewService / ResultProjectionService (`UpsertFromParameterResultAsync`, SourceTable
  "ParameterResult", backfill) / SampleSummaryService use a **generic helper** keyed on "workflow types that use
  TestAnalysis" (today: ElementalAssay) - not per-type branches. `ElementalAssayDetailDto` keeps its exact fields,
  mapped from TestAnalysis + ParameterResult + CalculationJson. Add a generic `AnalysisDetailDto` (analysis fields +
  parameter results + readings) on the test order detail for future types.
- Tests: every existing elemental test (unit + Postgres) passes unchanged in behaviour (update only storage-level
  assertions); new tests for the generic helpers (approval gate, return, projection) using a non-elemental
  AnalysisType fixture; migration Up/Down verified on a copy of LIMSV2.
