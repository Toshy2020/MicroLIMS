# Standard-Comparison Assay (PeakArea / Titration) + AAS — Draft Build Spec

Source: `E:\files\files\prompt-fp-standard-comparison-assay-aas-titration.md` (STM-PC-023 read-through,
2026-09-21). Recon: `docs/FP_Standard_Comparison_Assay_Phase0_Recon.md`. Local branch `feat/fp-hplc-foundation`
(never pushed). Style follows `docs/FP_Calibration_Curve_Build_Spec.md` and
`docs/FP_Multi_Analyte_HPLC_Build_Spec.md`.

**This spec ends at a decision gate (Q1-Q14 in the recon doc). No code, migration or test is written from it
until the gate is answered. Every `ThWtStd`, `ThWtTest`, `TheoWt`, `TheoCs` value below is written
`<TBD, see Q#>` — none is a real number.** Replicate counts, the RSD ≤ 2% gate, the ±5%/±10% weigh-in windows,
and the AAS 0/2/4/6 ppm calibration levels *are* real values, quoted directly by the prompt from the SOP, and
are used as such (cited each time).

## Why

`HplcAssayResult`'s formula (`% = (A/A_std)×(W_std/W)×(P/100)×(D/D_std)×100`) reports % of the standard's
concentration using explicit dilution ratios and no moisture correction. The validated method's formula is
`% Assay (of label claim) = (Response_test/Response_std) × (ActWtStd/ThWtStd) × (ThWtTest/ActWtTest) ×
((100−MC)/100) × P`, which replaces dilution ratios with fixed target weights and adds a per-run moisture term.
This is a correctness fix (prompt, "Decisions already made"), and `HplcMultiAnalyte` (built this week) inherits
the same gap (recon F4) — both are replaced by one generalized equation type.

## What already exists (recon summary — see recon doc for line references)

| Need | Existing piece | Reuse |
|---|---|---|
| One result per (TestOrder, analyte/Specification) | `TestAnalysis` / `ParameterResult` / `ResultReading` (built, 8 equation types incl. `HplcMultiAnalyte`) | Yes — no new result-storage pattern needed (recon F5) |
| Per-analyte standard + optional SST criteria | `TestAnalyte` (+ `SstMaxRsdPercent`/`SstMinResolution`/`SstMaxTailingFactor`/`SstMinTheoreticalPlates`, all nullable), `SystemSuitabilityRunAnalyte` | Yes, unchanged shape (recon F4/F10) |
| Standard potency default | `Material.Purity` (`decimal?(6,3)`, required for `ReferenceStandard`) | Yes, default `P`, editable + audited override (recon F7) |
| RSD calculation | `HplcMultiAnalyteCalculator.CalculatePreparationRsd` / `MeasurementCalculator` (both sample SD, n−1, `decimal`) | Reusable helper if standard RSD is LIMS-computed (Q10) |
| Review/projection/summary/CoA generic paths | Already branch on `WorkflowType`/`AnalysisType` generically for the 8 `TestAnalysis`-based types | Yes, once Standard-Comparison is on `TestAnalysis` (recon F2) |
| Sign-once immutable run pattern | `SystemSuitabilityRun`, `CalibrationRun` | Reusable shape for a lighter AAS run (recon F12) |
| Instrument-reported calibration entry | `CalibrationEntryMode.InstrumentReported` | Reusable for AAS if confirmed (Q5) |

**Does not exist and has no anchor** (recon F6, F11, F14): `SamplePurpose`, a bulk-powder sample type, a
titration entry model, a weigh-in-window gate. **Not reused, explicitly** (recon F8): `ResultBasis` /
`ConversionFactor` — the Standard-Comparison formula already outputs %LC directly; there is no separate
concentration→amount→%LC conversion step the way elemental/`HplcMultiAnalyte` have one. Standard-Comparison
`ParameterResult` rows leave `ResultBasis` null and do not set `ConversionFactor`-driven fields.

## Enums (append-only; `Domain/Enums`)

- `EquationType` += `StandardComparisonAssay`, `Aas`. `WorkflowType` += `StandardComparisonAssay`, `Aas`.
  (`HplcAssay` and `HplcMultiAnalyte` stay in the enums for existing rows unless Q9 says to migrate and remove
  them — removal is out of scope for a "never invent, never guess" spec; migration is a separate, later slice.)
- New `ResponseMode` {`PeakArea`, `TitrationVolume`} — on `TestDefinition`, required when `EquationType =
  StandardComparisonAssay`.
- New `SampleType` {`Tablet`, `BulkPowder`} — see "Sample type and SamplePurpose" below. Alternative: extend the
  existing `DosageForm` enum with `BulkPowder` instead of adding a new enum (recommendation, not a decision —
  Q13).
- New `SamplePurpose` {`Bulk`, `FinishedProduct`, `Stability`} — on `Sample`, alongside `Category` (recon F6: no
  existing field covers this axis).
- New `WeighInWindowMode` {`HardBlock`, `WarningWithJustification`} — Test Master config; **gate choice, not
  decided** (Q11). The spec below builds both branches; the build stops after Q11 is answered rather than
  picking a default silently.
- `CalibrationEntryMode` unchanged — reused as-is for AAS if Q5 confirms it (append nothing new unless AAS
  software genuinely needs `LimsFitted`, still reserved/unbuilt).

## Entities and fields

All new/changed numeric columns are C# `decimal` (never `double`), matching the Calibration Curve and
Multi-Analyte HPLC conventions. Precision follows the closest existing precedent: weights/concentrations
`numeric(18,6)`, percentages/RSD `numeric(28,10)` (computed, compared unrounded) or `numeric(10,6)` (configured
threshold, matching `CalMinCorrelation`).

### `TestDefinition` additions (nullable; required when `EquationType = StandardComparisonAssay`)

- `ResponseMode` (`ResponseMode`, required).
- `StandardReplicatesBulk` / `StandardReplicatesFinishedProduct` / `StandardReplicatesStability` (int, default
  6/6/3 — quoted by the prompt from the SOP, recon F9, not a TBD).
- `SampleReplicatesBulk` / `SampleReplicatesFinishedProduct` / `SampleReplicatesStability` (int, default 1/2/3 —
  same source).
- `StandardRsdMaxPercent` (numeric(10,6), default 2 — quoted by the prompt, recon F9; nullable override per
  analyte follows the same "null = not checked" convention as the existing `TestAnalyte` SST criteria if a
  product needs a tighter limit than the SOP default).
- `StandardWeighInTolerancePercent` (numeric(10,6), default 5 — SOP-quoted). `SampleWeighInTolerancePercent`
  (numeric(10,6), default 10 — SOP-quoted).
- `WeighInWindowMode` (`WeighInWindowMode`) — **left without a default; the field exists, but no value is
  written until Q11 is answered.**
- Validation when `EquationType = StandardComparisonAssay`: `WorkflowType` must be `StandardComparisonAssay`,
  `ResponseMode` required, all replicate counts ≥ 1, `StandardRsdMaxPercent` and both tolerance percents > 0,
  `WeighInWindowMode` required (build cannot ship without Q11).

### `TestAnalyte` additions

- `ThWtStdMg` (numeric(18,6), nullable) — **value always `<TBD, see Q1/Q2>`**; the column exists so the
  per-analyte target can be configured once the SOP table (Q1) and the Vitamin C ambiguity (Q2) are resolved.
  Not sample-type-dependent per the prompt ("fixed target weight of the working standard, per analyte").
- Reused as-is for Standard-Comparison analytes: `SstMaxRsdPercent`, `SstMinResolution`, `SstMaxTailingFactor`,
  `SstMinTheoreticalPlates` (all nullable — "not stated in this SOP... enforced only when configured", prompt
  §1 "Suitability"). `LoqMgPerL`/`View` stay nullable/unused for this type, same pattern as `HplcMultiAnalyte`.

### New `StandardComparisonWeightTarget` (child of `TestAnalyte`) — `ThWtTest` keyed by (analyte, sample type)

Per the prompt: *"`ThWtTest` varies by sample type, not just by analyte... Model this as a value keyed by (Test
or Analyte, SampleType) — a single per-analyte constant is not sufficient."*

- `Id`, `TestAnalyteId` (FK), `SampleType` (`SampleType`, or the `DosageForm`-derived equivalent per Q13),
  `ThWtTestMg` (numeric(18,6)) — **value always `<TBD, see Q1>`**.
- Unique (`TestAnalyteId`, `SampleType`).
- One row per analyte × sample type actually used by that test (e.g. a Vitamin E or mineral analyte gets a
  Tablet row and a BulkPowder row, each independently `<TBD>` — the SOP's "half of each" relationship between
  the tablet and bulk-powder values, and between the finished-product and mineral halves, is **not** encoded as
  a computed factor; each is its own configured number, matching how the prompt presents them as two separate
  known quantities (1099.08 / 1040.08 mg finished-product/bulk, 549.54 / 520.04 mg for Vitamin E and the
  minerals) rather than one value and a ×0.5 rule).

### Sample type and `SamplePurpose` — where they live

Two independent axes (recon F6, Q13):

- **`SamplePurpose`** (Bulk / FinishedProduct / Stability) drives replicate counts only. New field on `Sample`
  (`Sample.SamplePurpose`, nullable enum, alongside `Category`), set at receiving like `Category` is. Required
  when the sample's assigned tests include any `StandardComparisonAssay` or `Aas` test; null otherwise (no
  behavior change for every other equation type).
- **Sample type** (Tablet / BulkPowder) drives `ThWtTest` lookup. Recommended: extend `Specification.DosageForm`
  with `BulkPowder` and read it per (item, test) the same way Weight Variation already does, rather than adding
  a parallel field — `DosageForm` is already item-scoped, which is the right granularity (a product is either a
  tablet product or a bulk-powder product, not chosen per sample). Alternative, not recommended: add a
  `SampleType` field directly on `Item` (also item-scoped, functionally equivalent, but duplicates what
  `DosageForm` almost already models — the choice is presented at the gate, Q13, not decided here).
- These are not the same value and are not derived from one another in code: a `Bulk`-purpose sample is
  expected in practice to always be a `BulkPowder` sample type, but nothing enforces that pairing, matching how
  the prompt treats the Vitamin E/mineral halving as a per-analyte fact rather than a `SamplePurpose`-driven
  rule (the halving applies to specific analytes on a `FinishedProduct`-purpose, tablet-sample-type product).

### Standard-Comparison entry — built on `TestAnalysis` / `ParameterResult` (no new result entity)

Following the `HplcMultiAnalyte` pattern (recon F5), not the retired `HplcAssayResult` shape:

- One `TestAnalysis` per signed entry: `AnalysisType = WorkflowType.StandardComparisonAssay`, `EquipmentId`
  (HPLC or titrator, per `ResponseMode`), `AnalysedAt`, `SampleMatrix` unused (solids only per this SOP; kept
  null), `ConditionsJson` unused, `ValidityRecordType`/`Id` → the linked `SystemSuitabilityRun` (mirrors today's
  `HplcAssayResult.SystemSuitabilityRunId`), signature, `Comment`.
- One `ParameterResult` per analyte spec: `SpecificationId`, `ParameterName`/analyte snapshot, `ReportedValue`
  (the %Assay/%LC, unrounded, `decimal`), `ReportedDisplay` (rounded per F13's existing convention — 1 dp
  AwayFromZero unless Q14 says otherwise), `Unit` = `"%"`, `ComparisonStatus`, `ResultBasis` **left null**
  (recon F8 — this formula has no basis conversion), `CalculationJson` carrying: `ActWtStd`, `ThWtStd` (echoing
  the configured value used, for audit), `ActWtTest`, `ThWtTest`, `SampleType` used to look it up, `P`
  (potency used, and whether it was the Material default or an override, with the override note),
  `MC`, `ResponseValue` (the PeakArea ratio or the titration-volume ratio), `SystemSuitabilityRunAnalyteId`
  (since `ValidityRecordItemId`'s FK is scoped to `CalibrationRunAnalyte` — same workaround `HplcMultiAnalyte`
  already uses, recon "Status of the shared per-parameter result foundation").
- `ResultReading` rows, `Kind = Replicate`: one per standard replicate (`Stage` = "Standard", `Index` = replicate
  number, `Value1` = area or titration volume) and one per sample replicate (`Stage` = "Sample"), so the raw
  replicate data — and the computed standard RSD, whichever way Q10 resolves — is always auditable from the
  readings, the same way `HplcMultiAnalyte` stores injection-level data today.

### Response calculation per `ResponseMode`

- `PeakArea`: `Response = PA_test / PA_std` (today's HPLC input, unchanged source — from the CDS, transcribed).
- `TitrationVolume`: `Response = (V_test − V_blank) / (V_std − V_blank)`, mL. **Built only if Q3 confirms N/F
  cancellation** (see "Slice — Titration mode" below); if N/F do not cancel, `NormalityFactor` fields are added
  to the entry and multiplied into the ratio — the spec does not guess which branch applies.

### Full formula (both modes, once `Response` is computed)

```
%Assay = Response × (ActWtStd / ThWtStd) × (ThWtTest / ActWtTest) × ((100 − MC) / 100) × P
```

All operands `decimal`; compared **unrounded** against the spec limit (`SpecificationEvaluator.Evaluate`),
displayed rounded to 1 dp AwayFromZero — same convention as every other FP equation type (recon F13; not
reopened unless Q14 says otherwise).

### Weigh-in window gate (structure only — behavior depends on Q11)

Server-side, at entry validation, for both `ActWtStd` (vs `ThWtStd × (1 ± StandardWeighInTolerancePercent/100)`)
and `ActWtTest` (vs `ThWtTest × (1 ± SampleWeighInTolerancePercent/100)`):

- `WeighInWindowMode = HardBlock`: out-of-window `ActWt*` rejects the entry outright (`InvalidOperationException`,
  same pattern as every other hard gate in `TestWorkflowEngine`).
- `WeighInWindowMode = WarningWithJustification`: out-of-window `ActWt*` is accepted only with a non-blank
  justification note (min length, matching the existing `WithdrawAsync` "reason required (min 10 non-blank
  characters)" convention), stored alongside the entry and surfaced to the reviewer (same visibility pattern as
  the elemental-assay `RequiresReview` surfacing).
- Both branches are fully specified; **the build does not start until Q11 picks one** (or a hybrid — e.g. hard
  block beyond some wider secondary tolerance — but no such secondary number exists in the prompt, so a hybrid
  is not assumed).

### Suitability gate (standard RSD ≤ configured %, plus optional resolution/tailing/plates)

Reuses the existing per-analyte criteria pattern on `TestAnalyte` / `SystemSuitabilityRunAnalyte` (recon F4/F10)
unchanged in shape:

1. Standard RSD ≤ `TestDefinition.StandardRsdMaxPercent` (or the SOP default 2%, recon F9) — **source depends on
   Q10**: if computed, use the existing sample-SD `decimal` helper (`HplcMultiAnalyteCalculator`-style) over the
   entered standard replicate responses stored as `ResultReading` rows; if transcribed, it is a typed field on
   the `SystemSuitabilityRunAnalyte` row exactly as today.
2. Resolution / tailing factor / theoretical plates: evaluated only when the corresponding `TestAnalyte` column
   is non-null (already-built nullable-criteria pattern, no new field needed).
3. Analytes are independent (matches the elemental/`HplcMultiAnalyte` precedent) — one failing analyte does not
   fail the whole run.

### Approval gate — generalized (fixes recon F3/Q12 as part of this work)

Replace `SampleApprovalService.cs`'s `WorkflowType == WorkflowType.HplcAssay`-only filter (lines 169-192) with a
check driven by `TestDefinition.RequiresSystemSuitability == true` for **every** `WorkflowType` that uses it —
`HplcAssay` (legacy, until retired), `HplcMultiAnalyte` (currently ungated — a pre-existing gap this closes),
`StandardComparisonAssay`, and `Aas` if it also requires suitability (Q5). The generic check reads the linked
run from `TestAnalysis.ValidityRecordType`/`Id` for `TestAnalysis`-based types and from
`TestOrder.SystemSuitabilityRunId` for the legacy `HplcAssay` path, and requires `run.Passed == true` in both
cases. This is the one piece of "small remaining gaps" flagged against the otherwise-reused foundation (recon,
"Status of the shared per-parameter result foundation").

## AAS — new, separate equation type

```
%Assay = (ActCs × TheoWt × 100) / (TheoCs × ActWt)
```

- `TheoWt`, `TheoCs`: **`<TBD, see Q4>`** — constants, configured on `TestAnalyte` (`TheoWtMg` numeric(18,6),
  `TheoCsMgPerL` numeric(18,6), or equivalent units once Q4 confirms which minerals and units apply). Not
  sample-type-keyed (the prompt gives no indication AAS needs the tablet/bulk-powder split that HPLC does).
- `ActCs`: transcribed by the analyst off the AAS readout against its calibration curve (`InstrumentReported`
  mode, recon F12) — not fitted by the LIMS, mirroring Calibration Curve's `InstrumentReported` default;
  **confirm this is right for AAS too** (Q5 — AAS software may differ from Syngistix).
- `ActWt`: measured per run, `decimal`, no weigh-in-window requirement stated for AAS (the prompt's weigh-in
  window rule is scoped to §1 Standard-Comparison only).
- **Deliberately excludes** standard weight, `P` and `MC` — not modeled, not added, matching the prompt's
  explicit instruction that this is a property of the AAS method, not an omission.
- Built on `TestAnalysis`/`ParameterResult` the same way as Standard-Comparison: one `TestAnalysis`
  (`AnalysisType = WorkflowType.Aas`) per digest, one `ParameterResult` per mineral spec (5 results per digest
  per the prompt, "same shared-foundation dependency as §1" — already satisfied, recon F5).

### AAS calibration run — two options, presented, not decided (Q5)

**Option A (recommended): a light standalone "AAS Calibration Run" record**, reusing the sign-once/immutable
shape of `CalibrationRun`/`SystemSuitabilityRun` but dropping everything ICP-specific:

- `AasCalibrationRun`: `Id`, `Code` (reuse `SystemSuitabilityRunCode` generator with a new infix, e.g.
  `{ABBR} AAS-CAL {seq:00}/{MM}{yyyy}` — recon precedent, `FP_Calibration_Curve_Phase0_Recon.md` F2), 
  `TestDefinitionId`, `SectionId`, `EquipmentId` (must be `EquipmentType.Aas`), `CalibrationStandardMaterialId`,
  `CalibrationAt`, `PerformedByUserId`/`At`, `SignatureId`, `Comment`, `Passed`.
- `AasCalibrationLevel` (child, one per level — 0/2/4/6 ppm, prompt-quoted, recon F9): `NominalPpm`
  (numeric(18,6)), three `ReadingMg...` values (triplicate, prompt-quoted) or a `ResultReading`-style child if
  reusing the shared foundation is preferred over a bespoke child table.
- Gate: RSD ≤ 2% (prompt-quoted, same single criterion as §1) — **per level or across the whole curve is
  explicitly left open (Q5)**, so both a per-`AasCalibrationLevel` RSD column and a whole-curve RSD column are
  modeled; only one is enforced once Q5 is answered.
- No ICV/CCV/blank/internal-standard/correlation gate — none are stated for AAS (prompt §2), and none are added
  "to be safe," per the prompt's explicit instruction not to add fields the SOP doesn't call for.

**Option B: no reusable run record** — the curve is entered per sample batch, inline with the AAS result entry,
with no separate signed artifact and no cross-batch reuse. Simpler to build, but loses the traceability and
report-attachment pattern every other FP calibration/suitability record has, and breaks the "confirm `ActCs`
against a specific calibration event" audit trail the reviewer would otherwise get (mirroring `CalibrationRun`'s
A8 "reviewer sees the run and report" rule). Not recommended, but buildable if the lab says AAS curves are
re-run per batch and never reused.

## Slices, with dependencies

### Slice 0 — shared foundation (already built; reused as-is)

`TestAnalysis` / `ParameterResult` / `ResultReading`, `TestAnalyte`, `SystemSuitabilityRun(Analyte)`,
`SpecificationEvaluator`, section scoping, `ILabClock`, signature/audit conventions. **Small gaps to close as
part of Slice 1, not separately:**
- Approval gate generalization (recon F3/Q12) — currently `HplcAssay`-only, and `HplcMultiAnalyte` is
  ungated today.
- `SampleSummaryService`/CoA paths still branch by `WorkflowType` per equation type for their detail DTOs
  (working as designed for the 8 existing types) — Standard-Comparison and AAS each need their own detail-DTO
  branch added the same way `HplcMultiAnalyte`'s was, not a new mechanism.
- `ParameterResult.ValidityRecordItemId`'s FK is scoped to `CalibrationRunAnalyte` only — Standard-Comparison
  and AAS both store their run-analyte link in `CalculationJson` instead (workaround already in production use
  by `HplcMultiAnalyte`).

### Slice 1 — Standard-Comparison Assay, `PeakArea` mode (depends on Slice 0; blocked on Q1, Q2, Q6/Q13 answer, Q9, Q10, Q11, Q12 confirmation)

Backend: enums, `TestDefinition`/`TestAnalyte`/`StandardComparisonWeightTarget` additions, `Sample.SamplePurpose`
(+ migration of `Category`-only samples to have it nullable/backfilled), `DosageForm.BulkPowder` (or the Q13
alternative), the entry endpoint (`record-standard-comparison-result`, `TestWorkflow.Execute`, signed,
section-scoped), the formula (PeakArea response only in this slice), the weigh-in gate (per Q11's answer), the
suitability gate (per Q10's answer), the generalized approval gate, review/projection/summary/CoA branches.
Includes the `HplcAssay` → `StandardComparisonAssay` and `HplcMultiAnalyte` → `StandardComparisonAssay`
migration **only if Q9 says to migrate** — otherwise `HplcAssay`/`HplcMultiAnalyte` stay as separate types and
Standard-Comparison is additive (new products only). Frontend: Test Master (equation type, response mode,
replicate/tolerance/RSD config, weight-target grid keyed by analyte × sample type — all inputs blank/`<TBD>`
until Q1/Q2 are answered and a lab admin fills them in), result entry screen (standard + sample replicate grid,
MC and P inputs with override/audit note, weigh-in validation feedback per Q11's mode), summary/CoA labels.

### Slice 2 — Titration mode (depends on Slice 1; blocked on Q3)

Adds `ResponseMode.TitrationVolume` to the entry screen and engine: blank titre input, `V_test`/`V_std`/`V_blank`
readings (`ResultReading.Kind = Titration`, matching the already-reserved `ReadingKind.Titration = 6`), and —
only if Q3 says N/F do not cancel — explicit `Normality`/`EquivalenceFactor` inputs multiplied into the
response ratio. Equipment: `EquipmentType.Titrator`/`KarlFischer` (already exist, unused — recon F11) wired into
FP Instruments the same way `Hplc`/`IcpOes` are. **Do not build any part of this slice before Q3 is answered** —
the prompt is explicit that the wrong assumption here silently produces a wrong %Assay, not an error.

### Slice 3 — AAS (depends on Slice 0 only, independent of Slices 1/2; blocked on Q4, Q5)

New equation type end to end: `TestAnalyte.TheoWtMg`/`TheoCsMgPerL` (values `<TBD, see Q4>`), the AAS
calibration run (Option A or B per Q5), the result entry (`ActCs`, `ActWt`, 5 minerals per digest), Test Master
and FP Instruments wiring for `EquipmentType.Aas`, review/projection/summary/CoA branches. Fully independent of
whether Slices 1/2 have shipped — AAS shares only Slice 0's foundation, not the Standard-Comparison entities.

## Test cases (structure only — no real numbers until Q8)

Every test below is written symbolically; the prompt requires a real, hand-verified historical result (Q8)
before any of these carry actual figures. Listed here so the shape of coverage is agreed at the gate, not so it
can be implemented yet.

| # | Case | Expected |
|---|---|---|
| TC1 | Worked PeakArea example using Q8's real historical HPLC-vitamin result | `%Assay` matches the historical value exactly (hand-verified acceptance test, prompt's closing instruction) |
| TC2 | Worked TitrationVolume example using Q8's real historical Vitamin C result | Same, once Q3's N/F branch is settled |
| TC3 | Worked AAS example using Q8's real historical mineral result | Same |
| TC4 | `ActWtStd` at exactly the tolerance boundary (`ThWtStd × 1.05` / `× 0.95`) | Boundary itself passes (inclusive), one unit beyond fails — behavior depends on Q11 (block vs warning) |
| TC5 | `ActWtTest` at exactly the ±10% boundary | Same shape as TC4 |
| TC6 | Standard RSD exactly at the configured max (2% default) | Passes (inclusive), matching the `>=`/`<=` inclusive-bound convention used by Calibration Curve TC13 |
| TC7 | Resolution/tailing/plates criteria left null on `TestAnalyte` | Not evaluated (existing nullable-criteria convention, unchanged) |
| TC8 | Multi-analyte Standard-Comparison: one prep, one injection set, 4 analytes (water-soluble vitamins, prompt §1 "Multi-result tests") | 4 `ParameterResult` rows from one `TestAnalysis`, each with its own `ThWtStd`/`P`/`MC`/label claim/spec |
| TC9 | `SamplePurpose = Stability`: 3 standard replicates, 3 sample replicates required | Fewer/more than configured count rejected |
| TC10 | `SamplePurpose = Bulk`: 6 standard / 1 sample | Same shape as TC9 |
| TC11 | Vitamin E / mineral analyte on a `FinishedProduct`, `Tablet` sample: `ThWtTest` resolves to the halved `StandardComparisonWeightTarget` row, not the full tablet value | Confirms the (analyte, sample type) keying, not a (test, sample type) keying |
| TC12 | Missing `StandardComparisonWeightTarget` row for the sample's (analyte, sample type) | Rejected — "not configured" error, not a silent fallback or a zero |
| TC13 | `P` overridden from the Material default, with an audit note | Override stored, audited, distinct from the default path |
| TC14 | `MC` never defaulted from a prior run of the same standard | Each entry requires its own `MC`; reusing a stale value is not possible by construction (no field to copy from) |
| TC15 | Approval blocked: `StandardComparisonAssay` order with no linked passed run | Same error shape as today's HPLC gate, now driven by the generalized check |
| TC16 | Approval blocked: `HplcMultiAnalyte` order with no linked passed run (regression test for the F3/Q12 gap this rework closes) | Now blocked, where it previously was not |
| TC17 | AAS: RSD gate at a calibration level vs across the whole curve | Only one enforced, per Q5's answer; the other is not silently checked too |
| TC18 | AAS formula explicitly does not accept `P` or `MC` inputs | Request DTO has no such fields; a client sending them is ignored/rejected, not silently multiplied in |
| TC19 | `ResultBasis`/`ConversionFactor` are null/default(1.0) on every Standard-Comparison `ParameterResult` | Confirms recon F8 — no basis-conversion path is silently active |
| TC20 | Rounding/comparison: unrounded value compared to limit, 1 dp AwayFromZero displayed | Matches F13's existing convention, unless Q14 changes it |

InMemory + one Postgres round-trip per slice, following the existing FP test convention (`dotnet test
backend/MicroLIMS.Tests --artifacts-path <temp dir>` while the API is running, per repo `CLAUDE.md`).

## Notes carried forward from the recon

- **`ResultBasis`/`ConversionFactor` do not apply to Standard-Comparison Assay** (recon F8) — the formula's
  output is %LC directly; these two `Specification` fields, built for elemental/`HplcMultiAnalyte`, are left
  unused for this type rather than repurposed.
- **AAS deliberately excludes standard weight, `P`, and `MC`** — not an oversight to "fix" later; the prompt is
  explicit this is a real property of the AAS method in this SOP.
- **No numeric constant is guessed anywhere in this document.** Every `ThWtStd`, `ThWtTest`, `TheoWt`, `TheoCs`
  field above is a schema placeholder (`<TBD, see Q#>`), never a plausible-looking default.
