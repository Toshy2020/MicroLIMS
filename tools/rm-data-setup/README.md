# RM data setup (Plan 6)

Loads the 14 raw materials of `docs/superpowers/specs/2026-10-25-raw-material-stm-support-design.md`
(goal 10) into MicroLIMS through its API, repeatably: local LIMSV2 first, then Neon.

- `materials/<STM code>.json`: one transcribed material each (format: `schema.md`).
- `catalog.json`: test definitions, solutions/titrants, HPLC and ICP methods shared across materials.
- `gaps.md`: what the documents ask for that the system or the master data cannot hold.
- `load.py`: idempotent loader (`--dry-run` / `--apply`), see its `--help`.
- `reports/`: loader and end-to-end run reports.

## Transcription rules

1. Source text: `E:/MicroLIMS/rls-tmp/rm-stms-txt`. Use the STM and spec sheet named in the spec's table (the latest-dated ones).
2. Main method only. "Alternative" methods are not transcribed.
3. Nothing is invented. Every value carries `source` (file:line). Unreadable, cut-off or contradictory text goes to `openQuestions`, with no row.
4. Micro tests follow the spec sheet, even if it cites USP while physchem cites BP: one spec set.
5. Not created: disinfectant efficacy (Sodium Hypochlorite).
6. AAS tests are ICP methods (calibration curve from the stated standard levels).

User answers (2026-11-04), apply as stated:

- Soya isoflavone total isoflavones (UV): skip. Siberian Ginseng assay: skip.
- Pyridoxine USP content of chloride: titration.
- Methyl/Propyl Paraben identification: HPLC retention time.
- Sodium Starch Glycolate E. coli: keep "Absence / gm".
- Riboflavin phosphate minimum sample quantity: 200 g.
- Sodium Lauryl Sulfate micro limits: not added.

User decisions on the gaps (2026-11-04): "use your recommendations for A, load everything in B as written".
Applied as:

- Visual/comparison limit tests with a number (heavy metals, chlorides, sulfates, iron, impurity E):
  Qualitative rows, limit in the text, e.g. "Complies (NMT 20 ppm)".
- Results the workflows cannot calculate (two-indicator titrations, SLS alkalinity in mL, SLS NaCl+Na2SO4,
  SLS non-esterified alcohols, optical rotation on dried basis): Measurement tests, the analyst enters the
  final value, the system judges it.
- NaCl tablets assay: % value, limit text "% of stated amount".
- Titrants: house standardization defaults (3 replicates, factor 0.97-1.03, max RSD 0.2 %, valid 30 days,
  shelf life 30 days, "Tightly closed, room temperature"), taken from the existing local titrants.
- Missing reference standards/indicators: created as material master entries (name only, no stock).
- HPLC/ICP methods missing required details: held back with their tests and spec rows (list in
  reports/held-back.md) until the lab supplies the method details. The % w/w ICP row change waits for them.
- Document questions loaded as written: TAMC/TYMC 1000/100 CFU/g; typos kept; Pyridoxine BP UV identification
  as rows of one UV test; NaOH identification pH per each grade's own STM.
- Minimum quantities: routine from the spec sheet, retest from the STM when it states one, otherwise none.
- Karl Fischer water (Sildenafil, parabens): Measurement test with MoistureRole Water, analyst enters water %.
- Paraben identification by HPLC retention time: Qualitative row ("complies"), recorded by the analyst.
- Assays whose written formula cannot produce the result (Hypochlorite no dilution factor, Metabisulfite no
  back-titration subtraction, SLS no formula): Measurement tests, analyst enters the final %.
