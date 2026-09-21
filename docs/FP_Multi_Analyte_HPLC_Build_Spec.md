# Multi-Vitamin (Multi-Analyte) HPLC Assay - Recon and Draft Build Spec

Branch `feat/fp-hplc-foundation` (local, never pushed). Status: **draft, waiting for Gate 0 answers.**
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
