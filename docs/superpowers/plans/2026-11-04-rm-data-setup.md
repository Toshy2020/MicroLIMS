# RM data setup for 14 materials (Plan 6) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Load the 14 raw materials (goal 10, plus goal 9 "AAS runs as ICP") so each can be received and tested as routine work: first on local LIMSV2, verified end to end, then on Neon.

**Architecture:** Each material's STM and spec sheet are transcribed into one reviewed JSON data file with a source line for every value. A shared catalog dedupes test definitions, titrants/solutions, methods and reagents across materials. One idempotent Python loader reads the files and creates or updates master data through the existing API (no direct SQL, so every change is audited). It has a dry-run diff mode and is rerun unchanged against Neon.

**Tech Stack:** Python 3 (stdlib `urllib`, `json`, `getpass`), the MicroLIMS REST API, existing helpers in `E:/MicroLIMS/rls-tmp` for local driving.

**Spec:** `docs/superpowers/specs/2026-10-25-raw-material-stm-support-design.md` (goals 9-10, "Known document issues", "Calls made after the builder check").

## Global Constraints

- Inventing a method or limit the documents do not state is an anti-goal. A value with no source line is not loaded; gaps go to the user.
- Use the **latest-dated** STM and spec sheet per material (table in the spec). "Alternative" methods are not set up (main method only).
- Items are matched by STM code (`Items.Code`); a missing or differing prod item is reported, never created.
- Micro tests follow each spec sheet even when it cites USP and physchem cites BP: one spec set, not two grades.
- Disinfectant efficacy (Sodium Hypochlorite) is not created.
- AAS tests (Lead, Sodium, Potassium, Selenium total/selenate/selenite) are ICP methods with a calibration curve from standard levels.
- Changes go through the API only, as an authenticated user, so they are audited. No raw SQL writes.
- Not-applicable records are created as drafts by the loader; the two signatures (Section Head, then QC Manager) are given in the app by the people holding those roles.
- Local first. Neon writes happen only after the user approves the dry-run diff, and are run by the user with their own credentials (the loader prompts with `getpass`; Claude never handles production passwords).
- Builds, scratch and dumps on `E:/MicroLIMS/rls-tmp` / `E:/MicroLIMS/backups`. Back up LIMSV2 before the first local load.
- Branch `feat/rm-data-setup`, stacked on `feat/rm-stm-support` (PR #130). Commit locally; never push unasked.

## Review Focus

1. **A limit transcribed wrong** (NMT vs NLT, % vs ppm, inclusive bound): every spec row carries `source: "<file>:<line>"`; Task 2's fidelity review re-reads each row against that line.
2. **Rerunning the loader** must change nothing: Task 4's test runs it twice and asserts the second run reports 0 creates and 0 updates.
3. **A prod item whose code or category differs** from local: the dry-run reports it and the loader skips the item rather than guessing.
4. **A dried/anhydrous assay with no LOD/KF row in its grade's spec set**: the API refuses it at spec save; the loader reports it as a data error, not a crash.
5. **A grade-specific test on a single-grade item** (Pyridoxine/NaOH BP vs USP rows): Task 7 receives one sample per grade and checks the ordered tests differ as the sheets say.

---

### Task 1: Data file format and reader rules

**Files:**
- Create: `tools/rm-data-setup/README.md` (format, rules, how to run)
- Create: `tools/rm-data-setup/schema.md` (one JSON shape per material)

**Interfaces:**
- Produces: material file `tools/rm-data-setup/materials/<STM code>.json` with keys `itemCode`, `stmFile`, `specFile`, `grades[]` (each: `grade`, `rows[]`), `notApplicable[]`, `supplierAttributes[]`, `minQuantity` (`routine`, `retest`, `unit`), `openQuestions[]`. A spec row: `testCode`, `parameterName`, `limitType`, `lower`, `upper`, `lowerInclusive`, `unit`, `expectedResultText`, `resultBasis`, `referenceStandard`, `perContainer`, `source`. A test reference: `testCode` resolves to the catalog (Task 3).

- [ ] Write both files. Rules in README: latest-dated file; main method only; every value has `source`; anything unreadable or contradictory goes to `openQuestions` with its source and no row.
- [ ] Commit.

### Task 2: Transcribe the 14 materials

**Files:** Create `tools/rm-data-setup/materials/<code>.json` x14 (from `E:/MicroLIMS/rls-tmp/rm-stms-txt`).

- [ ] Transcribe each material (parallel readers; one material per reader).
- [ ] Fidelity review: a second reader checks every row's values against its `source` line and lists mismatches. Fix and re-review until clean.
- [ ] Hold the rows tied to the spec's "Known document issues" in `openQuestions` until the user answers.
- [ ] Commit.

### Task 3: Shared catalog

**Files:** Create `tools/rm-data-setup/catalog.json`, `tools/rm-data-setup/gaps.md`.

**Interfaces:**
- Produces: `testDefinitions[]` (code, displayName, workflowType, equationType, physchemArea, moistureRole, perContainer, replicateCount, UV fields, titration constants), `solutions[]` (titrants/VS with standardization), `hplcMethods[]` (incl. related-substances impurities, internal standard), `icpMethods[]` (AAS as ICP, standard levels), `reagentLinks[]` (STM reagent name to an existing material master entry).

- [ ] Build the catalog from the 14 files: reuse general definitions (description per container, solubility, LOD, sulfated ash/ROI, pH, limit tests, TAMC, TYMC, E. coli, Salmonella) and existing local ones by code where they match; material-specific ones for each titration/HPLC/ICP/UV procedure.
- [ ] Map reagents and AAS standards to the master entries loaded on 2026-10-25; unmatched ones go to `gaps.md`.
- [ ] Any STM requirement the system cannot hold goes to `gaps.md` and is reported; no workaround is invented.
- [ ] Commit.

### Task 4: Idempotent loader

**Files:** Create `tools/rm-data-setup/load.py`, `tools/rm-data-setup/test_load.py`.

**Interfaces:**
- `py -I load.py --target local|prod --dry-run|--apply [--only <code>]`. Target URL from a flag; user/password from `getpass` (local may read `frontend/.env.e2e.local`). Order: catalog (test definitions, solutions, methods) -> item minimum quantities -> specs per grade -> supplier attributes -> not-applicable drafts. Upsert by natural key (test code; solution code; method code; item code + test code + parameter + grade). Output: per-object `create | update(field diffs) | same | skip(reason)` and totals.
- `--apply` on `prod` also requires `--i-approve-prod`.

- [ ] Test: dry-run on an empty selection reports 0 changes; apply twice on one material, second run reports 0 creates/updates (local Postgres, `--only`).
- [ ] Implement; API errors are reported per object and do not stop the run.
- [ ] Commit.

### Task 5: Local load

- [ ] Back up LIMSV2 to `E:/MicroLIMS/backups/LIMSV2-before-rm-data-setup-<date>.dump`.
- [ ] Dry-run, review the diff, apply, rerun (0 changes).
- [ ] Commit the run report under `tools/rm-data-setup/reports/`.

### Task 6: Not-applicable signatures, supplier attributes, minimums

- [ ] In the app, Section Head then QC Manager sign the loaded not-applicable drafts (Riboflavin related substances, composition).
- [ ] Check supplier attributes and minimum quantities on each item screen.

### Task 7: End-to-end check per material (local)

**Files:** Create `tools/rm-data-setup/e2e.py` (local only).

- [ ] Per material and grade: receive one sample (grade from the supplier CoA), confirm the ordered tests equal the grade's spec rows minus not-applicable ones, enter conforming physchem results through the API, review with retention quantity, approve, and check the CoA rows.
- [ ] Micro tests: confirm orders and limits for every material; run one material through incubation to approval.
- [ ] Browser check: Pyridoxine (BP and USP samples) and Riboflavin (not-applicable row on the CoA).
- [ ] Record results in `tools/rm-data-setup/reports/e2e-local.md`; commit.

### Task 8: Neon

- [ ] Read-only check: the 14 item codes exist on prod with the same category; prod has the release this depends on (PR #130 migrations).
- [ ] The user runs the dry-run against prod with their own credentials (`--dry-run` only reads); its report goes to `reports/prod-dry-run.md`. Stop and review it with the user.
- [ ] After the user approves: the user runs `--apply --i-approve-prod` with their own credentials; then a second dry-run must show 0 changes.
- [ ] Not-applicable drafts on prod are signed by the real Section Head and QC Manager.
