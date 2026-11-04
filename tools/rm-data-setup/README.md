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
