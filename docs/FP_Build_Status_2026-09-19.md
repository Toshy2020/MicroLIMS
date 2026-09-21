# MicroLIMS — Build Status (2026-09-19)

Branch: `feat/fp-hplc-foundation` (local only, **never pushed**; nothing applied to production/Neon).
Local database: **LIMSV2** — all migrations up to `20260921182420_Tier2Disintegration` are applied.
Last full backend test run (including Postgres): **1692 passed / 0 failed / 0 skipped** (2026-09-21).
Frontend: type check and production build are clean. The project has no frontend unit tests.

---

## 1. What has been achieved

### 1.1 Section segregation (QC Microbiology / Finished Product)
The Microbiology and Finished Product labs are handled as separate sections: each one sees, tests, reviews and approves only its own work.

| Commit | What |
|---|---|
| cf2a081 | Section-based segregation foundation (phase 1) |
| bdd38b3, 18417ac, 4f093f0 | Testing workspace / my tasks, cross-section action blocking, materials stock scoped by section |
| 6f14975, b1383f4 | Per-section review and approval; media lots and cryovials by section |
| 85474b1, c248ee9, 2b736c7 | Dashboards, queues, notifications, KPIs, OOS tracking and reports by section |
| e575eb0, 8d3cb37, 2b11db0, ab12fbe | Section APIs and screens; combined and single-section Certificates of Analysis |
| eb1bb47, 149dc23 | Separate Microbiology / Finished Product Test Masters; configuration menu grouped by lab |

### 1.2 Finished Product — HPLC assay (Phase 3, manual entry)
| Commit | What |
|---|---|
| da5b622 | Specification limits: numeric range, NLT, NMT |
| d4f0bb9, 1cea2e0 | HPLC instruments, chromatography column master, reference standard purity |
| 80f6c83, 17804a9 | System Suitability Runs (signed, criteria from Test Master) and sample linking; System Suitability page |
| ee9f008 | HPLC fields in Test Master; Equation Types page |
| d2b4c77 | HPLC assay result entry, suitability gate, review flow |
| 6712080, 21764d3 | HPLC in sample summary, exports and CoA; raw data and calculation shown |
| b206b3e | Suitability run standard values in the list; printable run report |
| 7d3bef2 | Finished Product tests have **no preparation stage** (FP samples are Ready at receipt) |
| 4fa7fff, 2ccf13b | FP Instruments page (its own page, separate from the Micro equipment page); "Configure for Lab" pre-fills the form |

### 1.3 Universal specifications
| Commit | What |
|---|---|
| 98b3d8d, 703673e, 8181af4 | One specification model for every limit type (range, NLT, NMT, target ± tolerance, count tiers, qualitative, present/absent, multi-stage); several parameters per test; Specifications tab and parameter dialog |

### 1.4 Finished Product — calibration curve / elemental assay (ICP-OES)
| Commit | What |
|---|---|
| 0742797 | Calibration curve runs (signed once, one-way withdrawal, code `{ABBR} CAL nn/MMyyyy`, standards with no expiry refused, lab-local time) |
| a857f01 | Syngistix reports ppm **in the sample** (mg/kg solids, mg/L liquids) |
| 7b29a55 | Calibration curve screens |
| 1bd6bd0, 37756aa, b12c88d | Elemental assay results per element: downstream processing (review, approval, summary, CoA) and entry screens |

### 1.5 Finished Product — other test types
Decisions and designs: `docs/FP_Other_Equation_Types_Phase0_Recon.md` and `docs/FP_Other_Equation_Types_Build_Spec.md`.

| Commit | What |
|---|---|
| 6afa03d | **Shared result foundation**: TestAnalysis / ParameterResult / ResultReading. Elemental assay moved onto it; one shared path for approval, review, summary and CoA |
| f270979 | Generic result display in the summary, test cards and CoA; reusable unit entry grid (paste from Excel, keyboard navigation) |
| 27cccdb | **Measurement** (pH, density, viscosity, …): replicates, mean/min/max/SD/RSD, spec checked on mean / each value / min / max. **16 new FP equipment types** (UV-Vis, dissolution tester, disintegration tester, KF, titrator, viscometer, refractometer, polarimeter, conductivity meter, melting point, oven, furnace, GC, AAS, digestion microwave, caliper) |
| 2c196ab | **Loss on drying / ash** (% loss or % residue, optional container tare, required test conditions). **Appearance / identification** (complies / does not comply, observation required when it does not comply, expected text saved with the result) |
| 7058bca | Result-entry screens for all three types, new Test Master fields, new types on the FP Instruments page |
| 1a80a19 | Dissolution design (HPLC finish, stages S1/S2/S3) recorded in the spec |
| 9f758db | **Dissolution backend**: standard from the linked passed suitability run, % dissolved per vessel against the label claim, Q per product, stages S1 (6) / S2 (12) / S3 (24) with configurable offsets; S1/S2 that do not conform **always continue** to the next stage (fail only at S3); a pending stage cannot be submitted or approved; suitability run cannot be relinked once results exist (ae65c89) |
| e226772 | **Dissolution screens**: stage-by-stage vessel entry, Test Master stage settings, Dissolution Q in the specification dialog |
| bb6f1b7, 7039aea | **Disintegration backend** (2026-09-21): time limit per product (NMT n min on the specification), time per unit or "not disintegrated"; 6 units, then 12 more if 1-2 fail, at least 16 of 18 (counts set on the Test Master); 3+ failures at stage 1 fail at once; stage 2 judged by the rules captured at stage 1 |

Every new result is electronically signed, audited and limited to its section. Calculations use exact decimals. Values are compared unrounded; only the display is rounded.

### 1.6 Lab answers recorded (2026-09-19)
- Most products are **multivitamins** sharing one test set. The pharmaceutical products are **sildenafil, glimepiride and fluconazole**.
- Multivitamin assays cover the **water-soluble vitamins plus vitamins A, C, D and E**, all by **HPLC** (vitamin C included).
- All three pharmaceutical products have **dissolution**, finished by **HPLC**.
- **Content uniformity is deferred**; weight variation stays.
- Consequences: the **UV assay and titration are dropped** (no confirmed test needs them). Karl Fischer is only built if a water-content test is confirmed.

### 1.7 Database safety
Before each migration LIMSV2 was backed up to `E:\MicroLIMS\db-backups\`, and the migration was tested forward and back on a scratch copy.

Backups taken:
- `LIMSV2_before_section_segregation_20260918.dump`
- `LIMSV2_before_fp_hplc_20260918.dump`
- `LIMSV2_before_fp_prep_20260919.dump`
- `LIMSV2_before_universal_specs_20260919.dump`
- `LIMSV2_before_calibration_curve_20260919.dump`
- `LIMSV2_before_elemental_results_20260919.dump`
- `LIMSV2_before_shared_results_20260919.dump`
- `LIMSV2_before_tier1_measurement_20260919.dump`
- `LIMSV2_before_tier1_grav_qual_20260919.dump`
- `LIMSV2_before_tier2_dissolution_20260919.dump`
- `LIMSV2_before_tier2_disintegration_20260921.dump`

---

## 2. In progress

- Disintegration: backend done; screens next.

---

## 3. Still open

### 3.1 Decisions needed from you / the lab
| # | Question | Why it matters |
|---|---|---|
| 1 | **Multivitamin HPLC**: one test per vitamin on the current HPLC assay, or build a multi-vitamin HPLC assay (several vitamins per test, per-vitamin standards, result in mg/unit and % of label claim)? Recommended: build it. | The current HPLC assay handles one analyte per test and reports % of the standard, not % of label claim. This affects most products. |
| 2 | Full common multivitamin test list beyond the assays (appearance, LOD, disintegration, weight variation, others?) | Decides which remaining types are needed |
| 3 | Is a water-content (Karl Fischer) test needed on any product? | Decides whether Karl Fischer is built |
| 4 | Softgel/oil tests: acid value, peroxide value, omega-3 by GC — in or out? | Tier 3 scope |
| 5 | Admin backup/restore feature: design questions still unanswered | Requested earlier, not started |

### 3.2 Remaining build plan (in order)
1. **Disintegration screens** (backend done 2026-09-21).
2. **Weight variation** (EP 2.9.5 bands). Content uniformity stays deferred.
3. **Multi-vitamin HPLC assay**, if question 1 is answered "build it".
4. Tier 3, only if confirmed: related substances, omega-3 by GC, and acid/peroxide value.

### 3.3 Testing not yet done
- **No browser end-to-end test yet** of the HPLC, ICP-OES (calibration curve / elemental), measurement, loss on drying / ash, appearance/ID, dissolution or disintegration flows.
- You need to **restart the API** so it runs this branch's code first.
- Your HPLC (`HPC-F-IL-F-08-028`, Agilent, OpenLab ChemStation C.01.07) is in Equipment Inventory but not yet set up on the FP Instruments page.

### 3.4 Known follow-ups / small issues
- Test Master cannot **clear** a system suitability criterion once set (an empty value means "keep").
- Other controllers may return full User records including **PasswordHash**. This was found and fixed only for suitability runs; the rest needs an audit.
- The role rename question from the recon: likely not a bug, not checked.
- HPLC label-claim conversion (REQ-FP-021) was not built; question 1 above covers it.
- Reports OOS counts still include stale readings from returned tests (older, microbiology side).

### 3.5 Before anything goes to production
- The branch is local only. Pushing, merging and running the migrations on Neon all need your explicit go-ahead and a pre-merge data check.
