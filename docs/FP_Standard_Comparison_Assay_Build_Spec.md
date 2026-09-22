# Standard-Comparison Assay (PeakArea / Titration) + AAS — Draft Build Spec

Source: `E:\files\files\prompt-fp-standard-comparison-assay-aas-titration.md` (STM-PC-023 read-through,
2026-09-21). Recon: `docs/FP_Standard_Comparison_Assay_Phase0_Recon.md`. Local branch `feat/fp-hplc-foundation`
(never pushed). Style follows `docs/FP_Calibration_Curve_Build_Spec.md` and
`docs/FP_Multi_Analyte_HPLC_Build_Spec.md`.

> **Build status, 2026-09-22 (later than the body of this spec).** The gate is answered and building has
> started, so read this header before trusting a "blocked" note below.
> - **Q9 answered: fold both.** The single-analyte `HplcAssay` and the multi-vitamin `HplcMultiAnalyte` types
>   both become `StandardComparison`, and `HplcAssayResult` is retired. Local data is disposable; nothing is in
>   production.
> - **Q10 answered: both.** The analyst enters the standard replicate responses and the LIMS computes the RSD
>   (sample SD), which drives the gate; a transcribed instrument RSD is kept alongside for comparison only.
> - **Built and committed on `feat/fp-hplc-foundation`:** the Stage model, back and front (`94ae9c1`,
>   `644af51`); SC-1, the suitability run carrying `Th.Wt.std`, `MC`, the replicate responses, the computed RSD
>   and the ±5% weigh-in warning (`0ea44ce`); SC-2, the `StandardComparison` type and calculator in PeakArea
>   mode, with results on `TestAnalysis`/`ParameterResult` (`ad7c06e`). Postgres suite 1839/0/0.
> - **SC-3 built** (`45b18b8` backend, `fa3af0b` frontend): `HplcAssay`/`HplcMultiAnalyte` retired (enum values
>   kept, refused by Test Master), `HplcAssayResults` table dropped, the passed-run approval gate generalised to
>   every test with `RequiresSystemSuitability` (Q12 done). Old local test data deleted by user decision.
> - **SC-4 (titration) decisions, 2026-09-22:** `EP_blank` is titrated **once with the standard** and entered on
>   the suitability run (`SystemSuitabilityRunAnalyte.BlankTitreMl`); every sample linked to that run uses it for
>   both `EP_test` and `EP_std`. Titration **reuses the suitability run** for the standard titres: titrator
>   instead of HPLC, no column (`ChromatographyColumnId` nullable), RSD is the only criterion. `ResponseMode` lives
>   on `TestDefinition` and cannot change once runs exist. Entry readings use `ReadingKind.Titration`.
> - **Next:** the screens (SC-5) and the AAS calculation.
> - Still genuinely open: Q8 (a real worked example to validate against), Q14 (rounding convention), and the
>   deferred AAS calibration curve.

**This spec ends at a decision gate (Q1-Q14 in the recon doc). No code, migration or test is written from it
until the gate is answered. Every `ThWtStd`, `ThWtTest`, `TheoWt`, `TheoCs` value below is written
`<TBD, see Q#>` — none is a real number.** Replicate counts, the RSD ≤ 2% gate, the ±5%/±10% weigh-in windows,
and the AAS 0/2/4/6 ppm calibration levels *are* real values, quoted directly by the prompt from the SOP, and
are used as such (cited each time).

**Second gate, 2026-09-22 (D-W1-D-W5).** The user answered further gate questions after the first Stage-model
pass (D-S1-D-S5, same date). Q1, Q2 and Q3 are now closed/answered and no longer block the build; titration
(originally Slice 3, renumbered Slice 4 below) is unblocked. D-S2 (`ItemTestPortionWeights`) and D-S4 (`ThWtStd`
on `TestAnalyte`) are **reversed** and removed from the design — see "Weight & moisture model (D-W1-D-W5)" below,
which supersedes those two decisions. Q4, Q5 (AAS) and Q9-Q12, Q14 are untouched and still gate their respective
parts.

**Third gate, 2026-09-22 (D-A1-D-A3).** The user answered the weigh-in-window gate mode and the AAS term/scope
questions. Q11 is answered (D-A1): warning, not hard block, on both windows — see the rewritten "Weigh-in window
gate" section below; only whether the justification note is mandatory is still open. Q4 is reworded
closed-by-design (D-A2/D-A3), the same way Q1 was reworded by D-W5: AAS's four inputs are typed at result entry,
so there is no constant table to collect; only "which minerals run on AAS vs. ICP-OES" stays open, as a
master-data fact rather than a build blocker. Q5's calibration-run/RSD-scope decision is explicitly **deferred**
by the user, not decided — see the rewritten "AAS" section below. The AAS slice is **unblocked and ready to
build**; it is renumbered Slice 1 below so the slice order reflects that. Q9, Q10, Q12 and Q14 are untouched.

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

**Does not exist and has no anchor**: a titration entry model (recon F11 — the titration *formula* itself is now
confirmed, D-W2, 2026-09-22; only the entry model remains to be built), a weigh-in-window gate (recon F14).
`SamplePurpose`/tablet-vs-bulk-powder sample type (recon F6) is **not** in this category — resolved 2026-09-22
by user decision onto the existing `ProductionStage` lookup (recon F15, D-S3), see "Stage model" below.
**Not reused, explicitly** (recon F8): `ResultBasis` /
`ConversionFactor` — the Standard-Comparison formula already outputs %LC directly; there is no separate
concentration→amount→%LC conversion step the way elemental/`HplcMultiAnalyte` have one. Standard-Comparison
`ParameterResult` rows leave `ResultBasis` null and do not set `ConversionFactor`-driven fields.

## Enums (append-only; `Domain/Enums`)

- `EquationType` += `StandardComparisonAssay`, `Aas`. `WorkflowType` += `StandardComparisonAssay`, `Aas`.
  (`HplcAssay` and `HplcMultiAnalyte` stay in the enums for existing rows unless Q9 says to migrate and remove
  them — removal is out of scope for a "never invent, never guess" spec; migration is a separate, later slice.)
- New `ResponseMode` {`PeakArea`, `TitrationVolume`} — on `TestDefinition`, required when `EquationType =
  StandardComparisonAssay`.
- ~~New `SampleType` {`Tablet`, `BulkPowder`}~~ and ~~new `SamplePurpose` {`Bulk`, `FinishedProduct`, `Stability`}
  on `Sample`~~ — **superseded 2026-09-22 by user decision (D-S3).** Neither is added. Both axes turned out to be
  the same thing the lab already tracks via `ProductionStage` (recon F6/F15, corrected — it is not free text with
  no anchor). See "Stage model" below. New `ProductionStageRole` {`Bulk`, `Finished`, `Stability`, `InProcess`,
  `Other`} is added instead, on the existing `ProductionStage` entity, not on `Sample` or `Specification`.
- New `WeighInWindowMode` {`HardBlock`, `WarningWithJustification`} — Test Master config. **Decided 2026-09-22
  (D-A1, answers Q11): defaults to `WarningWithJustification`.** `HardBlock` stays in the enum (append-only, no
  removal) for a possible future stricter product, but nothing in this build sets it — the SOP-driven default is
  `WarningWithJustification` for every Standard-Comparison test. See "Weigh-in window gate" below for the
  concrete backend/storage/display behavior this now requires. The one remaining open point is narrower than
  before Q11 was answered: whether the justification note is *mandatory* — see that section.
- `CalibrationEntryMode` unchanged — reused as-is for AAS if Q5 confirms it (append nothing new unless AAS
  software genuinely needs `LimsFitted`, still reserved/unbuilt).

## Entities and fields

All new/changed numeric columns are C# `decimal` (never `double`), matching the Calibration Curve and
Multi-Analyte HPLC conventions. Precision follows the closest existing precedent: weights/concentrations
`numeric(18,6)`, percentages/RSD `numeric(28,10)` (computed, compared unrounded) or `numeric(10,6)` (configured
threshold, matching `CalMinCorrelation`).

### `TestDefinition` additions (nullable; required when `EquationType = StandardComparisonAssay`)

- `ResponseMode` (`ResponseMode`, required).
- `StandardReplicatesBulk` / `StandardReplicatesFinished` / `StandardReplicatesStability` (int, default
  6/6/3 — quoted by the prompt from the SOP, recon F9, not a TBD). Renamed from `...FinishedProduct` to
  `...Finished` (2026-09-22) to match `ProductionStageRole.Finished` exactly — see "Stage model" below: these are
  now keyed by the sample's resolved `ProductionStage.Role` (D-S5), not by a `Sample.SamplePurpose` field.
- `SampleReplicatesBulk` / `SampleReplicatesFinished` / `SampleReplicatesStability` (int, default 1/2/3 —
  same source, same rename, same D-S5 keying).
- `StandardRsdMaxPercent` (numeric(10,6), default 2 — quoted by the prompt, recon F9; nullable override per
  analyte follows the same "null = not checked" convention as the existing `TestAnalyte` SST criteria if a
  product needs a tighter limit than the SOP default).
- `StandardWeighInTolerancePercent` (numeric(10,6), default 5 — SOP-quoted). `SampleWeighInTolerancePercent`
  (numeric(10,6), default 10 — SOP-quoted).
- `WeighInWindowMode` (`WeighInWindowMode`, required, **default `WarningWithJustification`** — decided
  2026-09-22, D-A1, answers Q11; see "Weigh-in window gate" below for whether the justification note itself is
  mandatory, still open).
- Validation when `EquationType = StandardComparisonAssay`: `WorkflowType` must be `StandardComparisonAssay`,
  `ResponseMode` required, all replicate counts ≥ 1, `StandardRsdMaxPercent` and both tolerance percents > 0,
  `WeighInWindowMode` required (no longer a build blocker — defaults per D-A1).

### `TestAnalyte` additions

- **`ThWtStdMg` does NOT live here — superseded 2026-09-22 (D-W3), reverses D-S4.** The original draft of this
  section put a per-analyte `ThWtStdMg` column on `TestAnalyte` as a method constant. The user's second-gate
  answer (D-W3) moves `Th.Wt.std` onto `SystemSuitabilityRun`/`SystemSuitabilityRunAnalyte` instead, entered
  once per standard (per analyte for multi-analyte tests) rather than configured once per method. See "Weight &
  moisture model (D-W1-D-W5)" below for the new field and its column mapping. No `TestAnalyte` schema change is
  needed for `ThWtStd` at all.
- Reused as-is for Standard-Comparison analytes: `SstMaxRsdPercent`, `SstMinResolution`, `SstMaxTailingFactor`,
  `SstMinTheoreticalPlates` (all nullable — "not stated in this SOP... enforced only when configured", prompt
  §1 "Suitability"). `LoqMgPerL`/`View` stay nullable/unused for this type, same pattern as `HplcMultiAnalyte`.

### Stage model (D-S1–D-S5, decided by the user 2026-09-22)

Resolves recon F6/F15 and Q6/Q13, and **replaces** the `StandardComparisonWeightTarget` (per-analyte,
`SampleType`-keyed) and `SamplePurpose`/`SampleType` design that appeared in this position in earlier drafts of
this spec — both are contradicted by the decision below and are not built. There is one axis, not two: the
sample's **production stage**, which already exists in MicroLIMS as the `ProductionStage` lookup (recon F15) and
now gets a fixed role. *As originally decided:* it drove both the test-portion target weight and the replicate
count. **Reworded 2026-09-22 (D-W4) — one-line reversal note: it now drives only the replicate count** — see
"Weight & moisture model (D-W1-D-W5)" below for why `ThWtTest` no longer keys off the stage at all.

**D-S1 — scope.** *As originally decided 2026-09-22:* the stage changed *both* the test-portion target weight
(`ThWtTest`, via `ItemTestPortionWeights` below) and the replicate counts (`TestDefinition`, D-S5).
**Reworded the same day (D-W4) — one-line reversal note: the `ThWtTest` half of this scope is dropped.** Since
`ThWtTest` is now typed by the analyst per preparation (D-W4) rather than configured, the stage changes *only*
the replicate counts (`TestDefinition`, D-S5) — that is its sole remaining purpose. The %Assay equation itself
is identical for bulk, finished-product and stability samples — no stage-specific formula, and, for now, no
stage-specific spec limits (a later change to that is a new decision, not implied here).

**D-S3 — `ProductionStage` gets a fixed role.**
- New enum `ProductionStageRole` {`Bulk`, `Finished`, `Stability`, `InProcess`, `Other`} — named generically, not
  after any one lab's stage labels, so a renamed stage keeps working (TC — renamed-stage case, see Test cases).
- `ProductionStage` (existing entity, `backend/MicroLIMS.Domain/Entities/ProductionStage.cs`, currently
  `{Id, Name, IsActive}`) gains `Role` (`ProductionStageRole`, required; existing rows default to `Other` until
  reclassified — see migration below). `Name`/`IsActive` are unchanged; the lab keeps its own labels.
- Migration: add the `Role` column; data-migrate the six seeded rows (`20260906174124_AddSamplersAndProductionStages`)
  by name — `B` → `Bulk`, `IP` → `InProcess`, `F.P`/`S.F`/`Coating`/`Compressed Tab` → `Finished` (all read as
  finished-product-stage variants, consistent with the `F.P` illustration used in the original D-S2 worked
  example, still quoted for shape in "Weight & moisture model" above though the table itself is dropped) — and
  insert a new `Stability` row (`Role = Stability`; no existing name maps to it, recon F15 confirmed no such row
  exists today). Any stage a lab has since renamed away from these six names is left `Role = Other` and listed in
  the migration's own output for an admin to reclassify manually — the migration never guesses a role for a name
  it doesn't recognize.
- `Sample` linkage: `Sample.ProductionStage` (`Sample.cs:29`) is today a bare name string with no FK (recon
  F15) — not reliable enough to key replicate-count lookups on, since the entity's own
  comment says renaming/removing a stage "never affects historical records." Add `Sample.ProductionStageId`
  (`int?`, FK to `ProductionStage`) alongside the existing string, which is kept unchanged for display/reports
  (`ReportDocumentMapper.cs:105`) and not removed. Backfill for existing rows: match the stored `ProductionStage`
  name against current `ProductionStage.Name` rows (case-insensitive); a sample whose stored name no longer
  matches any current row (a stage since renamed or deleted) is left `ProductionStageId = null`, flagged for
  manual reconciliation, and cannot be used to enter a Standard-Comparison/AAS result until reconciled — it is
  never silently assigned `Other`.
- Capture scope: `Sample.ProductionStage` is populated only when `Sample.Category == SampleCategory.FinishedProduct`
  (recon F15, `CurrentStepViewService.cs:90-96`). Bulk and in-process pulls for this SOP are already received
  under that same `FinishedProduct` category with a `B`/`IP` stage today, so FP-only capture already covers
  every stage this rework needs — it does not need to widen to other `SampleCategory` values. What does change:
  the receiving UI (`NewSampleDialog.tsx`, `MultiSampleEntryGrid.tsx`, `EditSampleDetailsDialog.tsx`) must offer
  the new `Stability` stage as a pickable option, and `ProductWorkflowEngine.ReceiveAsync` must set
  `ProductionStageId` alongside the existing `ProductionStage` string at receiving time.

**D-S2 — `ItemTestPortionWeights`: `ThWtTest` keyed by (Item, TestDefinition, stage role), not by analyte.**
**DROPPED 2026-09-22 (D-W4) — reversed, not built.** *As originally decided:* a new `ItemTestPortionWeights`
table (`Id`, `ItemId`, `TestDefinitionId`, `StageRole`, `ThWtTestMg`, unique on the first three) configured one
`ThWtTest` value per (Item, TestDefinition, stage role), shared by every analyte of that test prep. **One-line
reversal note:** the user's second-gate answer (D-W4) establishes that Th.Wt.test is typed by the analyst at
result entry, next to Act.Wt.test, per preparation — it is not configured master data at all, so no lookup
table is needed. `ItemTestPortionWeights` is removed from the design entirely, along with its slice, its gate
rule ("no configured weight blocks entry") and its test cases (former TC11/TC12, see Test cases below). The
worked SOP-quoted numbers that illustrated this table's shape (Multivitamin tablets: WSV-HPLC `F.P` 1099.08 mg /
`B` 1040.08 mg, VitE-HPLC `F.P` 549.54 mg / `B` 520.04 mg) still stand as quoted SOP illustrations of what an
analyst would type for those preparations — they are no longer configured rows anywhere.

**D-S4 — `ThWtStd` is unaffected.** **REVERSED 2026-09-22 (D-W3) — do not leave this design standing.**
*As originally decided:* `ThWtStd` stayed exactly as specified under "TestAnalyte additions" above:
`TestAnalyte.ThWtStdMg`, a method constant per analyte, product- and stage-independent. **One-line reversal
note:** the user's second-gate answer (D-W3) moves `Th.Wt.std` onto `SystemSuitabilityRun`/
`SystemSuitabilityRunAnalyte` instead — entered once per standard (per analyte for multi-analyte tests), not
configured once per method on `TestAnalyte`. `TestAnalyte.ThWtStdMg` is removed from the design; see "Weight &
moisture model (D-W1-D-W5)" immediately below for the replacement fields and their column mapping.

**D-S5 — replicate counts, keyed by stage role.** (Unaffected by D-W1-D-W5 — this is the stage role's one
remaining purpose per D-S1/D-W4 above.) The `TestDefinition` replicate fields under "TestDefinition
additions" above (`StandardReplicatesBulk/Finished/Stability`, `SampleReplicatesBulk/Finished/Stability`) are
resolved from the sample's `ProductionStage.Role` (via `Sample.ProductionStageId`, D-S3), not from a
`Sample.SamplePurpose` field — that field is dropped, superseded by the stage role.

### Weight & moisture model (D-W1–D-W5, decided by the user 2026-09-22, second gate)

Resolves recon F7/F11/F14, closes/answers Q1, Q2, Q3, and reshapes Q13 — **supersedes and reverses D-S2 and
D-S4 above** (both are removed from the design; see the one-line reversal notes on each). Every weight, potency
and moisture value in the SOP formula is entered on a run or an entry screen, never configured as master data.
There is no lookup table for `ThWtStd` or `ThWtTest` anywhere in this design.

**D-W1 — Vitamin C's `ThWtStd` "(50)" is a typo, not a constant to chase (supersedes/closes Q2).** The SOP's
"(50)" next to Vitamin C's Th.Wt.std is a typing error in the source document. The design follows the equation;
the product name does not matter. Q2 is answered/closed — there is no constant to chase, and since `ThWtStd` is
typed per run (D-W3) rather than configured, this was never going to block the build regardless.

**D-W2 — titration formula confirmed (answers Q3).** Titration uses exactly this equation, from SOP STM-PC-013
(Perfectil Original Film Coated Tablets), §6.9.2.5 — the same formula as the peak-area mode, with a titrimetric
response:

```
Assay % = ((EP_test − EP_blank) / (EP_std − EP_blank)) × (ActWtStd / ThWtStd) × (ThWtTest / ActWtTest)
           × ((100 − MC) / 100) × P(%)
```

Definitions from the same SOP page: `EP_test` = volume of titrant to the colour change in the sample solution;
`EP_blank` = same for the blank; `EP_std` = same for the standard; `ActWtStd` = weight taken from the working
standard; `ThWtStd` = theoretical weight to be taken from the working standard; `ActWtTest` = weight taken from
the powdered tablets; `ThWtTest` = theoretical weight to be taken from the powdered tablets; `P(%)` = assay % of
the working standard; `MC` = working standard moisture content. Normality (`N`) and equivalence factor (`F`) do
**not** appear and are **not** inputs — the prompt's §1 working assumption is confirmed. Note: this confirming
screenshot is from STM-PC-013, while the original prompt's citation was STM-PC-023 — the formula is identical in
both documents (recon F11).

**D-W3 — `ThWtStd`, `ActWtStd`, `P` and `MC` all live on the System Suitability Run, not on `TestAnalyte`
(supersedes part of D-S2, reverses D-S4, reshapes Q1).** `Th.Wt.std`, `Act.Wt.std`, `P(%)` and `MC` are ALL
entered on the **System Suitability Run**, once per standard, and for a multi-analyte test once per analyte row
(one set per vitamin) — not on `TestDefinition`/`TestAnalyte` as Test Master constants.

- `SystemSuitabilityRun` (`backend/MicroLIMS.Domain/Entities/SystemSuitabilityRun.cs`) and
  `SystemSuitabilityRunAnalyte` (same folder) each gain two new columns: `TheoreticalWeightMg` (numeric(18,6))
  and `MoisturePercent` (numeric(10,6)), alongside their existing `StandardWeightMg` and `StandardPurityPercent`.
  Column-to-symbol mapping, explicit: existing `StandardWeightMg` = `Act.Wt.std`; new `TheoreticalWeightMg` =
  `Th.Wt.std` (**value always `<TBD, see Q1>`** until an analyst types it on a run — there is no table to
  pre-fill from, D-W5); existing `StandardPurityPercent` = `P`; new `MoisturePercent` = `MC`.
- `P` (`StandardPurityPercent`) still defaults from `Material.Purity` of the chosen reference standard lot
  (unchanged from today), but remains editable per run with an audit note (unchanged behavior, just confirmed
  explicitly here). `MC` (`MoisturePercent`) is measured per run and is **never** defaulted or carried over from
  a prior run of the same standard — there is no field to copy from, by construction (TC14, kept).
- The standard weigh-in window (±5%, `StandardWeighInTolerancePercent` on `TestDefinition`) is now checkable
  **on the run itself**, since both `TheoreticalWeightMg` and `StandardWeightMg` live there — not at
  Standard-Comparison result entry. See "Weigh-in window gate" below.
- **D-S4 ("`ThWtStd` stays a method constant on Test Master, per analyte") is REVERSED.** That design — a
  `TestAnalyte.ThWtStdMg` column — is removed; it does not stand alongside this one.

**D-W4 — `ThWtTest` is typed by the analyst at result entry, not configured (supersedes D-S2).** `Th.Wt.test` is
typed by the analyst at result entry, next to `Act.Wt.test`, per preparation. It is **not** configured master
data.

- The `ItemTestPortionWeights(ItemId, TestDefinitionId, StageRole, ThWtTestMg)` table is **dropped from the
  design entirely**, along with its slice, its gate rule ("no configured weight blocks entry") and its test
  cases (former TC11/TC12) — removed, not kept as an option (see D-S2 above and Slices/Test cases below).
- The sample weigh-in window (±10%, `SampleWeighInTolerancePercent` on `TestDefinition`) is then a check of the
  typed `ActWtTest` against the typed `ThWtTest` in the same entry — still subject to Q11 (hard block or
  warning). See "Weigh-in window gate" below.
- The stage role (D-S3) is **kept** — it is still needed, but now **only** for the replicate counts (D-S5). Its
  other purpose (keying `ThWtTest`) is gone; see D-S1's reworded scope above.

**D-W5 — Q1 is closed-by-design, not blocking (reshapes Q1).** Because every weight is now entered per run
(`ThWtStd`, D-W3) or per preparation (`ThWtTest`, D-W4), there is **no** table of `ThWtStd`/`ThWtTest` constants
to collect from the lab, and nothing about these two symbols blocks the build. Q1 is reworded: it is no longer a
blocking data-collection question; what remains open is only whether the lab wants any of these typed values
pre-filled or cross-checked against a configured reference later — a **future enhancement**, not part of this
build. Marked closed-by-design. **Q4/Q5 (AAS constants `TheoWt`/`TheoCs` and curve mode) stay OPEN and
untouched** — the AAS formula is a different formula and none of D-W1-D-W5 applies to it.

### Gate rule — no replicate count configured for the sample's stage

**Reworded 2026-09-22 (D-W4) — the `ThWtTest`-not-configured branch below is dropped**; since `ThWtTest` is
typed at entry (D-W4), there is nothing to look up or block on for it. Only the replicate-count branch remains.

Server-side, at entry validation (`TestWorkflowEngine`), worded the same way MicroLIMS already blocks other
unconfigured FP setups in Test Master — e.g. `"Step \"{stepName}\" has no media configured in Test Master."`
(`TestWorkflowEngine.cs:925`) and `"...the incubation window for \"{mediumName}\" ... is not configured - set
its incubation hours and temperature in Test Master."` (`TestWorkflowEngine.cs:1215`). Standard-Comparison/AAS
entry follows the same shape:
- `Sample.ProductionStageId` is null (unmigrated/unreconciled stage, D-S3): block —
  `"Sample {ReferenceNumber} has no reconciled production stage - it cannot be resolved to a stage role. Contact
  a Section Head to set its stage."` (still needed — the stage role still resolves the replicate count, D-S5).
- The resolved `TestDefinition` replicate field for that role is unset/zero: block with the
  "replicate count not configured for this stage" message rather than accept an arbitrary replicate count.
- (Superseded, no longer applies: "no `ItemTestPortionWeights` row configured for this Item/TestDefinition/
  StageRole" — that table and its gate branch are dropped, D-W4. Former TC12 is dropped with it.)

### Standard-Comparison entry — built on `TestAnalysis` / `ParameterResult` (no new result entity)

Following the `HplcMultiAnalyte` pattern (recon F5), not the retired `HplcAssayResult` shape:

- One `TestAnalysis` per signed entry: `AnalysisType = WorkflowType.StandardComparisonAssay`, `EquipmentId`
  (HPLC or titrator, per `ResponseMode`), `AnalysedAt`, `SampleMatrix` unused (solids only per this SOP; kept
  null), `ConditionsJson` unused, `ValidityRecordType`/`Id` → the linked `SystemSuitabilityRun` (mirrors today's
  `HplcAssayResult.SystemSuitabilityRunId`), signature, `Comment`.
- One `ParameterResult` per analyte spec: `SpecificationId`, `ParameterName`/analyte snapshot, `ReportedValue`
  (the %Assay/%LC, unrounded, `decimal`), `ReportedDisplay` (rounded per F13's existing convention — 1 dp
  AwayFromZero unless Q14 says otherwise), `Unit` = `"%"`, `ComparisonStatus`, `ResultBasis` **left null**
  (recon F8 — this formula has no basis conversion), `CalculationJson` carrying: `ActWtStd` and `ThWtStd`
  (echoing the values entered on the linked `SystemSuitabilityRun`/`SystemSuitabilityRunAnalyte`, D-W3 — **not**
  a Test Master lookup), `ActWtTest` and `ThWtTest` (both typed directly on this entry, D-W4 — **superseded
  2026-09-22, one-line reversal note:** the earlier design here read `ThWtTest` from an `ItemTestPortionWeights`
  lookup keyed by `ProductionStage.Role`, D-S2; that lookup is dropped), the resolved `ProductionStage.Role`
  (`StageRole`) — recorded for audit/traceability of which replicate-count configuration applied (D-S5), **not**
  used to resolve any weight, `P` (potency used, and whether it was the Material default or an override, with
  the override note — sourced from the run, D-W3), `MC` (sourced from the run, D-W3), `ResponseValue` (the
  PeakArea ratio or the titration blank-corrected ratio), `SystemSuitabilityRunAnalyteId` (since
  `ValidityRecordItemId`'s FK is scoped to `CalibrationRunAnalyte` — same workaround `HplcMultiAnalyte` already
  uses, recon "Status of the shared per-parameter result foundation").
- `ResultReading` rows, `Kind = Replicate`: one per standard replicate (`Stage` = "Standard", `Index` = replicate
  number, `Value1` = area or titration volume) and one per sample replicate (`Stage` = "Sample"), so the raw
  replicate data — and the computed standard RSD, whichever way Q10 resolves — is always auditable from the
  readings, the same way `HplcMultiAnalyte` stores injection-level data today.

### Response calculation per `ResponseMode`

- `PeakArea`: `Response = PA_test / PA_std` (today's HPLC input, unchanged source — from the CDS, transcribed).
- `TitrationVolume`: **confirmed 2026-09-22 (D-W2), answers Q3 — no longer gated.**
  `Response = (EP_test − EP_blank) / (EP_std − EP_blank)`, mL, where `EP_test`/`EP_blank`/`EP_std` are the
  titrant volumes to the colour change for the sample/blank/standard solutions respectively (SOP terminology,
  STM-PC-013 §6.9.2.5). Normality and equivalence factor do **not** appear — no `NormalityFactor` fields are
  added anywhere in the entry or the engine. (Superseded: the earlier draft of this line was gated "built only
  if Q3 confirms N/F cancellation" and modeled an unbuilt `NormalityFactor` branch — both are removed now that
  Q3 is answered.)

### Full formula (both modes, once `Response` is computed)

```
%Assay = Response × (ActWtStd / ThWtStd) × (ThWtTest / ActWtTest) × ((100 − MC) / 100) × P
```

Confirmed by D-W2 to be the same formula for titration too (with `Response` computed as above), not a separate
equation. All operands `decimal`. `ActWtStd`/`ThWtStd`/`P`/`MC` come from the linked
`SystemSuitabilityRun`/`SystemSuitabilityRunAnalyte` (D-W3); `ActWtTest`/`ThWtTest` are typed directly on this
entry (D-W4). Compared **unrounded** against the spec limit (`SpecificationEvaluator.Evaluate`), displayed
rounded to 1 dp AwayFromZero — same convention as every other FP equation type (recon F13; not reopened unless
Q14 says otherwise).

### Weigh-in window gate (decided 2026-09-22, D-A1 — answers Q11)

**Warning, not a hard block, on both sides of the gate.** `WeighInWindowMode` (`TestDefinition`) defaults to
`WarningWithJustification` (see "TestDefinition additions" above) — an out-of-window `ActWt*` never rejects the
run or the entry outright in this build. `HardBlock` stays in the enum (append-only, no removal) for a possible
future stricter product, but nothing here sets it. The two sides of this gate still live in different places,
since `ThWtStd` lives on the System Suitability Run and `ThWtTest` is typed at entry (D-W3/D-W4):

- **Standard side (±5%)** — checked **on the System Suitability Run itself** (`SystemSuitabilityRun`/
  `SystemSuitabilityRunAnalyte`, at run save/sign), comparing the entered `StandardWeightMg` (`ActWtStd`) against
  `TheoreticalWeightMg × (1 ± StandardWeighInTolerancePercent/100)` (`TestDefinition`, D-W3). Out-of-window: the
  **backend** raises the warning — `SystemSuitabilityService` computes the deviation and returns a warning result
  on save/sign rather than throwing, the same call site the hard gates already live in, just a non-throwing path;
  this is not a client-side-only validation.
- **Sample side (±10%)** — checked **at Standard-Comparison result entry** (`TestWorkflowEngine`), comparing the
  typed `ActWtTest` against the typed `ThWtTest × (1 ± SampleWeighInTolerancePercent/100)` (`TestDefinition`,
  D-W4) — both values entered in the same screen, same entry. Same non-throwing warning path, same "backend
  raises it" rule.
- **What is stored, concretely (D-A1: "the out-of-window state and the deviation percent are stored with the
  record").** `SystemSuitabilityRun`/`SystemSuitabilityRunAnalyte` and the Standard-Comparison `ParameterResult`
  (in `CalculationJson`, alongside the other weight/potency/moisture values already specified there) each gain a
  `WeighInWindowBreached` (`bool`) and `WeighInDeviationPercent` (`numeric(10,6)`, signed — positive over target,
  negative under target) pair, computed and persisted whenever the run/entry is saved, independent of whether
  anyone later reviews it — the deviation is on the record even if it's never looked at again.
- **Where it appears (D-A1: "run/result views and the CoA-facing summary").** The System Suitability Run screen
  and the Standard-Comparison result entry screen both surface the warning inline at save time (same visibility
  pattern as the elemental-assay `RequiresReview` surfacing); the reviewer sees it on the run/result review
  views; and the CoA-facing summary path (`SampleSummaryService`, `ReportDocumentMapper`) prints the deviation
  alongside the reported result rather than dropping it once the order is approved.
- **Still open (D-A1's one remaining sub-question, narrower than the original Q11):** whether the justification
  note is *mandatory* before the analyst can proceed past the warning. **Not yet decided by the user.**
  Recommendation: require it, matching the existing `WithdrawAsync` "reason required (min 10 non-blank
  characters)" convention — this is a GMP deviation from a validated method's weigh-in target, and an
  unexplained out-of-window entry with no note is a data-integrity gap. Until this is answered, the note field is
  specified as present-and-recorded-when-given, with no minimum-length enforcement — making it mandatory later is
  a small, additive follow-up (one validation rule), not a rework of the storage/display design above.

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

## AAS — new, separate equation type (unblocked 2026-09-22, D-A2/D-A3 — calculation only, ready to build)

```
%Assay = (Act.CS × Theo.Wt × 100) / (Theo.CS × Act.Wt)
```

**Term definitions, in the user's own words (D-A2, 2026-09-22):**

- `Act.CS` — actual concentration of the test solution, estimated by AAS.
- `Theo.CS` — theoretical concentration of the test solution.
- `Theo.Wt` — theoretical weight of test.
- `Act.Wt` — actual weight of the test sample, from the powdered homogeneous sample, taken during preparation of
  the Mineral Stock Solution for AAS.

The formula **deliberately has no standard weight, no `P` and no `MC`** — confirmed by the user (D-A2), not this
spec's inference; do not add them, matching the prompt's own instruction that the omission is a real property of
the AAS method in this SOP, not something to "fix" by analogy with §1's formula.

**All four inputs are typed at result entry (D-A3, 2026-09-22) — not configured master data.** `Act.CS`,
`Theo.CS`, `Theo.Wt` and `Act.Wt` are all typed on the AAS result entry screen, per mineral, the same way
`ActWtTest`/`ThWtTest` are typed on the Standard-Comparison entry rather than resolved from a `TestAnalyte`/Item
lookup (D-W4). `Act.CS` is transcribed by the analyst from the instrument readout, matching the Calibration
Curve "instrument-reported" decision (recon F12) — it is not fitted by the LIMS. Consequence: Q4's "constants"
are no longer a data-collection blocker (reworded the same shape as Q1's D-W5 rewording) — nothing needs to be
gathered from the lab before this equation type can be built. Pre-filling or cross-checking these typed values
against a configured reference later remains a future enhancement, out of scope here.

**The one part of Q4 that stays genuinely open:** which minerals in this product are run on AAS versus
ICP-OES — a master-data/test-setup fact (which `TestDefinition`/`Specification` rows point at
`EquipmentType.Aas` vs. `EquipmentType.IcpOes`), not a code blocker. Nothing about the calculation itself needs
this answered first.

- Built on `TestAnalysis`/`ParameterResult` the same way as Standard-Comparison, and on the same shared
  foundation (`TestAnalysis`/`ParameterResult`/`ResultReading`, recon F5) — **one `ParameterResult` per mineral
  spec**, the same "one row per analyte spec" convention every other multi-result FP type uses
  (`HplcMultiAnalyte`, elemental assay): one `TestAnalysis` (`AnalysisType = WorkflowType.Aas`) per digest, one
  `ParameterResult` per mineral (5 results per digest per the prompt).
- `ParameterResult.CalculationJson` carries the four typed inputs (`ActCs`, `TheoCs`, `TheoWt`, `ActWt`) for
  audit/traceability, the same way Standard-Comparison's `CalculationJson` carries its typed weights (D-W4).
- The existing unused `EquipmentType.Aas` value (recon F12) is wired into `TestAnalysis.EquipmentId` and FP
  Instruments the same way `Hplc`/`IcpOes` are already wired — no new enum member needed.
- `ActWt`: no weigh-in-window requirement stated for AAS (the prompt's weigh-in-window rule is scoped to §1
  Standard-Comparison only) — unaffected by D-A1.
- **No calibration-run entity in this build (D-A3).** See "AAS calibration curve — deferred" below.
- **Dependencies: none on the Stage model (Slice 2) or the Standard-Comparison/titration slices (Slices 3-4).**
  AAS shares only Slice 0's foundation (`TestAnalysis`/`ParameterResult`/`ResultReading`) — it does not use
  `ProductionStage.Role`, replicate-count-by-stage config, `SystemSuitabilityRun`/`SystemSuitabilityRunAnalyte`,
  or any Standard-Comparison entity. It can be built and shipped independently of whether Slices 2-4 exist yet —
  see "Slices, with dependencies" below, where it is now Slice 1.

### AAS calibration curve — deferred 2026-09-22 (D-A3), not cancelled

**Not part of this build.** The calculation-only AAS slice above needs no calibration-run entity: `Act.CS` is
typed at entry, transcribed off the instrument (D-A2/D-A3). The calibration curve itself (0/2/4/6 ppm, triplicate
reads, recon F9) and the RSD ≤ 2% scope question (per calibration level vs. whole curve, Q5) are follow-up work,
explicitly deferred by the user on 2026-09-22 — not decided, not built now, kept in the open list. The two-option
design below is kept as the design to return to when that follow-up is picked up; **neither option is
implemented in this build.**

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

### Slice 1 — AAS (depends on Slice 0 only, independent of Slices 2-4; UNBLOCKED 2026-09-22, D-A2/D-A3 — ready to build)

**Renumbered and moved 2026-09-22 (D-A2/D-A3).** This was Slice 4, listed last and blocked on Q4/Q5. It is moved
to Slice 1 because it is now the slice with the fewest open dependencies: Q4's constants are closed-by-design
(D-A2/D-A3), Q5's calibration-run decision is deferred rather than blocking (the calculation ships without it),
and AAS depends on nothing from Slice 0's siblings below — not the Stage model, not `SystemSuitabilityRun`, not
any Standard-Comparison entity. Ordering it first reflects that it can ship independently and immediately; it
does **not** imply the other slices are lower priority for the lab, only that AAS has no gate left to clear.

Backend: new `EquationType`/`WorkflowType = Aas`, `TestAnalysis`/`ParameterResult` wiring (`AnalysisType =
WorkflowType.Aas`, one `ParameterResult` per mineral spec — D-A3), the AAS result entry endpoint with typed
`ActCs`/`TheoCs`/`TheoWt`/`ActWt` fields (D-A2/D-A3, no `TestAnalyte`/Item lookup), the formula
`%Assay = (ActCs × TheoWt × 100) / (TheoCs × ActWt)` with no `P`/`MC`/standard-weight terms (D-A2, confirmed
deliberate), `EquipmentType.Aas` wired into `TestAnalysis.EquipmentId` and FP Instruments (recon F12, no new
enum value needed), review/projection/summary/CoA branches (same generic mechanism as every other
`TestAnalysis`-based type, recon F2/F5). **No calibration-run entity, no calibration-curve entry, no RSD-scope
decision in this slice** — see "AAS calibration curve — deferred" above; that remains a separate, later,
explicitly-deferred piece of work (Q5).
Frontend: Test Master / FP Instruments wiring for `EquipmentType.Aas`, AAS result entry screen (typed `Act.CS`,
`Theo.CS`, `Theo.Wt`, `Act.Wt` inputs per mineral, 5 minerals per digest per the prompt), summary/CoA labels.
**Still open before this slice starts:** which minerals in this product run on AAS vs. ICP-OES (the remaining
half of Q4) — a master-data/test-setup fact to confirm with the lab, not a code blocker; the endpoint and screen
themselves need no further gate.

### Slice 2 — Stage model: `ProductionStage.Role` migration (replicate counts only; `ItemTestPortionWeights` dropped) (depends on Slice 0 only; added 2026-09-22 as D-S1-D-S5, reworked same day per D-W4; renumbered from Slice 1 on 2026-09-22 per D-A2/D-A3)

**Reworded 2026-09-22 (D-W4) — one-line reversal note: this slice loses the item weight table.** *As originally
scoped,* it also built `ItemTestPortionWeights`. That table, its schema, its seeding and its gate branch are
dropped (D-W4); this slice now covers only the `ProductionStage.Role` migration, the `Sample` stage link, and
the replicate-count configuration.

Backend: `ProductionStageRole` enum, `ProductionStage.Role` column + data migration (seeded-row role backfill,
new `Stability` row, unrecognized-name rows flagged `Other` for manual reclassification — see "Stage model"
above), `Sample.ProductionStageId` FK + backfill-by-name migration (unmatched rows left null and flagged),
receiving-UI change to offer `Stability` as a pickable stage and to set `ProductionStageId` alongside the
existing string (`NewSampleDialog.tsx`, `MultiSampleEntryGrid.tsx`, `EditSampleDetailsDialog.tsx`,
`ProductWorkflowEngine.ReceiveAsync`), `TestDefinition` replicate-count fields keyed by stage role (D-S5), the
"no configured replicate count for this stage" gate rule. **This slice is schema/plumbing only and does not
require any of Q1-Q3 to be answered** (more true than before, now that Q1-Q3 are all closed/answered anyway) —
it can ship immediately. It does need Q6/Q13 confirmed (done, by D-S3 itself) before it starts.
Frontend: `ProductionStage` admin screen gains a `Role` picker; Test Master gains the per-stage-role replicate
count inputs (built in this slice or deferred to Slice 3, since nothing consumes them until then).

### Slice 3 — Standard-Comparison Assay, `PeakArea` mode (depends on Slice 2; blocked on Q9, Q10, Q12 confirmation — no longer blocked on Q1/Q2/Q11, all closed/answered 2026-09-22; renumbered from Slice 2 on 2026-09-22 per D-A2/D-A3)

Backend: enums, `TestDefinition`/`TestAnalyte` additions, `SystemSuitabilityRun`/`SystemSuitabilityRunAnalyte`
additions (`TheoreticalWeightMg`, `MoisturePercent` — D-W3), the entry endpoint
(`record-standard-comparison-result`, `TestWorkflow.Execute`, signed, section-scoped) with typed
`ActWtTest`/`ThWtTest` fields (D-W4), the formula (PeakArea response only in this slice), the weigh-in gate
(warning-mode per D-A1/Q11, split across the run and the entry screen — D-W3/D-W4), the suitability gate (per
Q10's answer), the generalized approval gate, review/projection/summary/CoA branches. Includes the `HplcAssay` →
`StandardComparisonAssay` and `HplcMultiAnalyte` → `StandardComparisonAssay` migration **only if Q9 says to
migrate** — otherwise `HplcAssay`/`HplcMultiAnalyte` stay as separate types and Standard-Comparison is additive
(new products only).
Frontend: Test Master (equation type, response mode, replicate/tolerance/RSD config keyed by stage role — no
item-weight grid, dropped D-W4), System Suitability Run entry screen gains `Th.Wt.std` and `MC` inputs per
standard (per analyte row for multi-analyte tests, D-W3), Standard-Comparison result entry screen (standard +
sample replicate grid, typed `Th.Wt.test` input next to `Act.Wt.test` per preparation — D-W4 — weigh-in warning
feedback per D-A1, not a blocking validation), summary/CoA labels.

### Slice 4 — Titration mode (depends on Slice 3; unblocked 2026-09-22 — Q3 answered by D-W2; renumbered from Slice 3 on 2026-09-22 per D-A2/D-A3)

**Reworded 2026-09-22 (D-W2) — one-line reversal note: no longer blocked.** *As originally scoped,* this slice
could not start until Q3 was answered. Q3 is now answered — titration uses the confirmed formula, with N and F
absent as inputs. This slice's contract:

- `ResponseMode.TitrationVolume` on the entry screen and engine, computing
  `Response = (EP_test − EP_blank) / (EP_std − EP_blank)` (D-W2) and feeding it into the same "Full formula"
  used by `PeakArea` mode.
- Blank titre input (`EP_blank`), alongside `EP_test`/`EP_std`, entered per replicate.
- Per-replicate titrant volumes: `ResultReading.Kind = Titration` rows (matching the already-reserved
  `ReadingKind.Titration = 6`), one per standard replicate and one per sample replicate, mirroring the
  `PeakArea` mode's replicate-reading pattern (`ResultReading.Kind = Replicate`) but with `Titration` kind and
  volume (mL) in `Value1`.
- No `Normality`/`EquivalenceFactor` fields anywhere — confirmed absent by D-W2, not modeled "to be safe."
- Equipment: `EquipmentType.Titrator`/`KarlFischer` (already exist, unused — recon F11) wired into FP
  Instruments the same way `Hplc`/`IcpOes` are.

## Test cases (structure only — no real numbers until Q8)

Every test below is written symbolically; the prompt requires a real, hand-verified historical result (Q8)
before any of these carry actual figures. Listed here so the shape of coverage is agreed at the gate, not so it
can be implemented yet.

| # | Case | Expected |
|---|---|---|
| TC1 | Worked PeakArea example using Q8's real historical HPLC-vitamin result | `%Assay` matches the historical value exactly (hand-verified acceptance test, prompt's closing instruction) |
| TC2 | Worked TitrationVolume example using Q8's real historical Vitamin C result | Same — formula confirmed 2026-09-22 (D-W2), no branch left to settle |
| TC3 | Worked AAS example using Q8's real historical mineral result | Same |
| TC4 | `ActWtStd` at exactly the tolerance boundary (`ThWtStd × 1.05` / `× 0.95`), checked on the System Suitability Run (D-W3) | Boundary itself passes (inclusive), one unit beyond fails — behavior depends on Q11 (block vs warning) |
| TC5 | `ActWtTest` at exactly the ±10% boundary, checked at Standard-Comparison result entry against the typed `ThWtTest` (D-W4) | Same shape as TC4 |
| TC6 | Standard RSD exactly at the configured max (2% default) | Passes (inclusive), matching the `>=`/`<=` inclusive-bound convention used by Calibration Curve TC13 |
| TC7 | Resolution/tailing/plates criteria left null on `TestAnalyte` | Not evaluated (existing nullable-criteria convention, unchanged) |
| TC8 | Multi-analyte Standard-Comparison: one prep, one injection set, 4 analytes (water-soluble vitamins, prompt §1 "Multi-result tests") | 4 `ParameterResult` rows from one `TestAnalysis`; `ThWtStd`/`P`/`MC` come from that vitamin's own `SystemSuitabilityRunAnalyte` row (D-W3), `ThWtTest` is typed once per preparation and shared by the 4 rows of that prep (D-W4) |
| TC9 | Sample's resolved `ProductionStage.Role = Stability` (D-S3): 3 standard replicates, 3 sample replicates required | Fewer/more than the stage-role-configured count (D-S5) rejected |
| TC10 | Sample's resolved `ProductionStage.Role = Bulk`: 6 standard / 1 sample required | Same shape as TC9, confirms replicate count is keyed by stage role, not a dropped `SamplePurpose` field |
| TC11 | **DROPPED 2026-09-22 (D-W4).** *Was:* `VitE-HPLC` on an Item with separate `ItemTestPortionWeights` rows per stage role, confirming the (Item, TestDefinition, StageRole) keying (D-S2). | Removed — `ItemTestPortionWeights` no longer exists; there is no keyed lookup to test. Replaced in spirit by TC23 below (typed values, no lookup). |
| TC12 | **DROPPED 2026-09-22 (D-W4).** *Was:* missing `ItemTestPortionWeights` row for the sample's (Item, TestDefinition, StageRole), expected rejected. | Removed — there is no configured row to be missing; `ThWtTest` is always typed at entry (D-W4). |
| TC13 | `P` overridden from the Material default, with an audit note | Override stored, audited, distinct from the default path; entered/overridden on the `SystemSuitabilityRun`/`SystemSuitabilityRunAnalyte` (D-W3) |
| TC14 | `MC` never defaulted from a prior run of the same standard | Each `SystemSuitabilityRun`/`SystemSuitabilityRunAnalyte` requires its own `MC` (D-W3); reusing a stale value is not possible by construction (no field to copy from) |
| TC15 | Approval blocked: `StandardComparisonAssay` order with no linked passed run | Same error shape as today's HPLC gate, now driven by the generalized check |
| TC16 | Approval blocked: `HplcMultiAnalyte` order with no linked passed run (regression test for the F3/Q12 gap this rework closes) | Now blocked, where it previously was not |
| TC17 | AAS: RSD gate at a calibration level vs across the whole curve | Only one enforced, per Q5's answer; the other is not silently checked too |
| TC18 | AAS formula explicitly does not accept `P` or `MC` inputs | Request DTO has no such fields; a client sending them is ignored/rejected, not silently multiplied in |
| TC19 | `ResultBasis`/`ConversionFactor` are null/default(1.0) on every Standard-Comparison `ParameterResult` | Confirms recon F8 — no basis-conversion path is silently active |
| TC20 | Rounding/comparison: unrounded value compared to limit, 1 dp AwayFromZero displayed | Matches F13's existing convention, unless Q14 changes it |
| TC21 | Stage model, D-S3: a Section Head renames a `ProductionStage` (e.g. `F.P` → `Finished Product (Tablet)`) after `Sample.ProductionStageId` links already exist for samples at that stage | Entry still resolves the correct replicate count, because the lookup keys on `Role`/`ProductionStageId`, never on `Name` — confirms the Stage model closes the renameable-name risk (recon F15). (Reworded 2026-09-22: `ThWtTest` dropped from this case's scope — D-W4 — since it is no longer looked up at all.) |
| TC22 | Stage model, D-S3: a sample's stored `Sample.ProductionStage` name string does not match any current `ProductionStage.Name` at migration time (stage was renamed/deleted before this rework), leaving `Sample.ProductionStageId = null` | Standard-Comparison/AAS entry is blocked with the "no reconciled production stage" message (Stage model gate rule) rather than silently defaulting to `Other` or a guessed role |
| TC23 | **New 2026-09-22 (D-W4).** Two different preparations of the same (Item, TestDefinition, stage role) are entered with two different typed `Th.Wt.test` values (e.g. an analyst re-weighs and types a slightly different theoretical target the second time) | Both entries are accepted and computed independently — confirms `ThWtTest` is a per-preparation typed value, not resolved from any shared configuration (replaces the intent of dropped TC11) |
| TC24 | **New 2026-09-22 (D-W2).** Titration blank subtraction: `EP_test`, `EP_std` and a non-zero `EP_blank` entered; separately, `EP_blank = 0` entered | `Response = (EP_test − EP_blank)/(EP_std − EP_blank)` in both cases; the `EP_blank = 0` case reduces to the plain ratio (regression guard on the subtraction step itself, not skipped when blank is negligible) |
| TC25 | **New 2026-09-22 (D-W3).** Standard weigh-in window breach on the System Suitability Run: `StandardWeightMg` (`ActWtStd`) outside `TheoreticalWeightMg × (1 ± 5%)` | Rejected/warned per Q11's mode, checked **at run save/sign**, not at Standard-Comparison result entry |
| TC26 | **New 2026-09-22 (D-W3).** `MC = 0` entered on a `SystemSuitabilityRun`/`SystemSuitabilityRunAnalyte` (a dry standard) | Accepted as a valid value, not treated as "missing"; `(100 − 0)/100 = 1` applies normally in the formula |
| TC27 | **New 2026-09-22 (D-W3).** `MC` left unentered (null) on a `SystemSuitabilityRun`/`SystemSuitabilityRunAnalyte` | Entry/run is blocked — `MC` is measured per run and never defaulted (D-W3); a null `MC` cannot compute `%Assay` |
| TC28 | **New 2026-09-22 (D-W3).** Multi-analyte run: two vitamins on the same `SystemSuitabilityRun`, each with its own `SystemSuitabilityRunAnalyte` row carrying different `Th.Wt.std`/`P`/`MC` values (e.g. one passes its weigh-in window, the other fails) | Each analyte's weigh-in/RSD outcome is independent, confirming per-analyte fields (D-W3), not one shared set for the whole run (mirrors TC8's per-prep note for `ThWtTest`, and the existing "analytes are independent" suitability-gate rule) |

InMemory + one Postgres round-trip per slice, following the existing FP test convention (`dotnet test
backend/MicroLIMS.Tests --artifacts-path <temp dir>` while the API is running, per repo `CLAUDE.md`).

## Notes carried forward from the recon

- **`ResultBasis`/`ConversionFactor` do not apply to Standard-Comparison Assay** (recon F8) — the formula's
  output is %LC directly; these two `Specification` fields, built for elemental/`HplcMultiAnalyte`, are left
  unused for this type rather than repurposed.
- **AAS deliberately excludes standard weight, `P`, and `MC`** — not an oversight to "fix" later; the prompt is
  explicit this is a real property of the AAS method in this SOP.
- **No numeric constant is guessed anywhere in this document.** Every `ThWtStd`, `ThWtTest`, `TheoWt`, `TheoCs`
  field above is a schema placeholder (`<TBD, see Q#>`), never a plausible-looking default. (`ThWtStd`/`ThWtTest`
  are no longer even config placeholders after D-W3/D-W4 — they're typed per run/preparation with no default.)
- **Vitamin C's SOP "(50)" is a source-document typing error, not a constant** (D-W1, 2026-09-22, closes Q2) —
  confirmed by the user, not guessed by this spec.
- **Titration is not a separate formula** (D-W2, 2026-09-22, answers Q3) — it uses the same "Full formula" as
  `PeakArea` mode, with `Response` computed from blank-corrected titrant volumes; no `Normality`/
  `EquivalenceFactor` fields exist anywhere in the design.
