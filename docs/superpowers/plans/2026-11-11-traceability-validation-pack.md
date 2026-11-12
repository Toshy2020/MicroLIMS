# Traceability Matrix and Validation Pack (A2) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Risk-assess every requirement, tag tests with the requirements they prove, generate the traceability matrix in CI, and deliver OQ, IQ, PQ and summary-report documents.

**Architecture:** Tests carry FS IDs (`[Trait("Requirement", …)]` in xUnit, `[FS-…]` in Vitest titles). A reflection test exports backend tags to JSON; a Node tool joins tags, `.trx` and Vitest JUnit results with the A1 documents and writes `RTM.md` and `Gaps.md`. The A1 document checker is extended for risk rows. No product code changes.

**Tech Stack:** Markdown; Node 22 (`node:test`, no new packages); xUnit traits and reflection; Vitest `junit` reporter (built in); GitHub Actions.

**Spec:** `docs/superpowers/specs/2026-11-11-traceability-validation-pack-design.md`

## Global Constraints

- Requires A1 complete: `documents/URS`, `documents/FunctionalSpecification`, `scripts/docs/check-documents.mjs`.
- Risk scales exactly: S 1–3, P 1–3, D 1–3 (3 = hard to detect); RPN = S×P×D; High ≥ 12, Medium 6–11, Low < 6.
- Tags name **FS** IDs only. A tag is added only to a test that fails when the behaviour is broken (CLAUDE.md Rule 4.7).
- First pass tags existing tests; it writes no new tests and changes no test logic.
- A skipped test is not passed.
- High-risk requirement is Covered only with ≥ 1 passed backend test **and** an OQ script ID. Medium / low: ≥ 1 passed automated test.
- `RTM.md` and `Gaps.md` are generated; never hand-edited.
- All risk scores are marked "Proposed — pending QA review". All documents status "Draft".
- No product code changes: only `documents/`, `scripts/docs/`, test attributes and titles, one new test class, `frontend` test reporter config, `.github/workflows/ci.yml`.
- Central package versions; no new NuGet or npm packages.

## Review Focus

1. A skipped `[PostgresFact]` is counted as proof → `skipped_test_is_not_passed` (Task 3).
2. A risk class written by hand disagrees with its numbers (as in Release 1c RA-1C-07) → `risk_class_must_match_rpn` (Task 1).
3. A tag with a typo silently proves nothing → `Every_requirement_tag_names_an_existing_fs_entry` (Task 2) and `unknown_frontend_tag_fails` (Task 3).
4. A high-risk rule "proved" only by a frontend test → `high_risk_needs_backend_test_and_oq` (Task 3).
5. Coverage drops unnoticed when a test is deleted or renamed → `covered_requirement_becoming_gap_fails` (Task 3).

## File Structure

| File | Responsibility |
|---|---|
| `scripts/docs/check-documents.mjs` (modify) | Also validate `RA-<AREA>.md` rows and OQ IDs |
| `scripts/docs/build-rtm.mjs` | Build `RTM.md`, `Gaps.md`; `--check` mode |
| `scripts/docs/build-rtm.test.mjs`, `scripts/docs/fixtures/rtm/*` | Tool tests |
| `scripts/docs/rtm-baseline.json` | Covered IDs (ratchet) and `highRiskGapsBlock: false` |
| `backend/MicroLIMS.Tests/Architecture/RequirementTagTests.cs` | Validate tags; export `requirement-tags.json` |
| `frontend/vite.config.ts` (modify) | JUnit reporter when `CI` is set |
| `documents/Validation/**` | Documents of spec 5.1 |
| `.github/workflows/ci.yml` (modify) | `traceability` job; evidence artifact |

---

### Task 1: Risk assessment format and check

**Files:**
- Create: `documents/Validation/RiskAssessment/_template.md`
- Modify: `scripts/docs/check-documents.mjs`, `scripts/docs/check-documents.test.mjs`

**Interfaces:**
- Row format: `| URS ID | Failure mode | S | P | D | RPN | Class | Existing controls | Verification |`; non-GxP: `| URS-XXX-nnn | Not GxP — not assessed | | | | | | | |`.
- Produces: `parseRiskRows(rootDir): Map<string, { s:number, p:number, d:number, rpn:number, riskClass:"High"|"Medium"|"Low"|"NotGxP" }>` exported from `check-documents.mjs`.
- New errors: unknown URS ID; S/P/D outside 1–3; RPN ≠ S×P×D; class not matching RPN; GxP URS with no risk row (reported only for areas whose `RA-<AREA>.md` exists); duplicate row.

- [ ] **Step 1:** Failing tests: `risk_class_must_match_rpn` (2×2×1 labelled Medium fails; 3×2×2 High passes; 11 → Medium; 12 → High; 5 → Low), `rpn_must_equal_product`, `score_out_of_range_fails`, `gxp_urs_without_risk_row_fails`, `non_gxp_row_needs_no_scores`, `risk_row_for_unknown_urs_fails`.
- [ ] **Step 2:** Run `node --test scripts/docs` → FAIL.
- [ ] **Step 3:** Implement.
- [ ] **Step 4:** Run → PASS. Commit `ci: validate risk assessment rows`.

### Task 2: Backend requirement tags

**Files:**
- Create: `backend/MicroLIMS.Tests/Architecture/RequirementTagTests.cs`

**Interfaces:**
- Tag form: `[Trait("Requirement", "FS-MIC-004")]` on a test method or class (class-level applies to all its tests).
- `Every_requirement_tag_names_an_existing_fs_entry` — reflects over the test assembly for `TraitAttribute` data with name `Requirement`; reads FS IDs from `documents/FunctionalSpecification/*.md` and `documents/DocumentControl/**/*.md` (repo root found by walking up to `improvement-roadmap.md`); fails listing each unknown ID with the test name.
- `Export_requirement_tags` — when env `MICROLIMS_RTM_TAGS_PATH` is set, writes JSON `[{ "test": "<Namespace.Class.Method>", "kind": "postgres" | "unit", "ids": ["FS-…"] }]`; `kind` is `postgres` when the method has `PostgresFactAttribute` (or a Postgres theory attribute), else `unit`. When unset it passes without writing.
- Malformed value (not matching `^(FS-[A-Z]{3}-\d{3}|FS-1[a-d]-\d{3}|DC-URS-\d{3})$`) fails `Requirement_tag_values_are_well_formed`.

- [ ] **Step 1:** Write the three tests plus a self-test: a private nested fixture class tagged `FS-ZZZ-999` proves the unknown-ID detection (assert the collector returns it and the validator rejects it), excluded from the real scan by name.
- [ ] **Step 2:** `dotnet build backend/MicroLIMS.sln -c Release` then `dotnet test backend/MicroLIMS.Tests -c Release --no-build --filter RequirementTagTests` → PASS (no tags yet), and with `MICROLIMS_RTM_TAGS_PATH=/tmp/tags.json` the file contains `[]`.
- [ ] **Step 3:** Commit `test: requirement tag validation and export`.

### Task 3: Matrix builder

**Files:**
- Create: `scripts/docs/build-rtm.mjs`, `scripts/docs/build-rtm.test.mjs`, `scripts/docs/fixtures/rtm/*`, `scripts/docs/rtm-baseline.json`
- Modify: `frontend/vite.config.ts` (in `test`: when `process.env.CI`, `reporters: ["default", "junit"]`, `outputFile: { junit: "test-results/vitest-junit.xml" }`)

**Interfaces:**
- CLI: `node scripts/docs/build-rtm.mjs --documents documents --tags <requirement-tags.json> --trx <file> [--trx <file>…] --junit <file> --frontend-src frontend/src [--check] [--update-baseline]`
- `buildRtm(input): { rows: RtmRow[], gaps: RtmRow[], errors: string[] }`; `RtmRow = { ursId, riskClass, fsIds: string[], tests: { name, kind: "postgres"|"unit"|"frontend", outcome: "Passed"|"Failed"|"Skipped"|"NotRun" }[], oqIds: string[], status: "Covered"|"Covered — manual only"|"Gap"|"Not GxP" }`.
- Frontend tags: regex `\[(FS-[A-Z]{3}-\d{3}|FS-1[a-d]-\d{3})\]` in `it(`/`test(` titles in `*.test.ts(x)`; matched to JUnit `testcase` by title.
- OQ IDs: from `documents/Validation/OQ/OQ-<AREA>.md` headings `### OQ-<AREA>-nn` with a `**Requirements:**` line of URS IDs.
- Output `documents/Validation/Traceability/RTM.md` (one table per area, columns of spec 5.4, sorted by ID; header line with counts by status and class; **no timestamps or run numbers**, so output is stable) and `Gaps.md`. Test outcomes appear in the CI evidence copy only (`--with-results` flag); the committed file omits the outcome column so it does not change on every run.
- `--check`: regenerate in memory and exit 1 if committed files differ; exit 1 if any ID in `rtm-baseline.json.covered` is no longer Covered; exit 1 on High gaps only when `highRiskGapsBlock` is true. `--update-baseline` adds newly Covered IDs; never removes.

- [ ] **Step 1:** Failing tests with fixtures: `covered_when_medium_has_one_passed_test`, `skipped_test_is_not_passed`, `failed_test_is_not_passed`, `high_risk_needs_backend_test_and_oq` (frontend-only → Gap; backend without OQ → Gap; both → Covered), `oq_only_is_manual_only`, `non_gxp_status`, `unknown_frontend_tag_fails`, `urs_reached_through_any_of_its_fs_entries`, `output_is_stable_across_runs`, `check_fails_when_committed_file_differs`, `covered_requirement_becoming_gap_fails`, `update_baseline_never_removes`, `high_gap_blocks_only_when_switched_on`, `tag_in_trx_but_test_missing_is_notrun`.
- [ ] **Step 2:** Run → FAIL. **Step 3:** Implement (TRX and JUnit parsed with a minimal regex/XML reader; no package). **Step 4:** Run → PASS.
- [ ] **Step 5:** Generate the first (empty-coverage) `RTM.md` and `Gaps.md`; commit `ci: traceability matrix builder`.

### Task 4: CI job and evidence artifact

**Files:** Modify `.github/workflows/ci.yml`

- In job `backend`, set `MICROLIMS_RTM_TAGS_PATH=backend/TestResults/requirement-tags.json` on the PostgreSQL test step; the existing upload step already takes `backend/TestResults/*` — widen its path to include the JSON.
- In job `frontend`, upload `frontend/test-results/vitest-junit.xml` as `frontend-test-results`.
- New job `traceability` (`needs: [backend, frontend]`): download both artifacts; run `node --test scripts/docs`; `node scripts/docs/check-documents.mjs documents`; `build-rtm.mjs … --trx backend/TestResults/integration.trx --check`; then `--with-results` into `evidence/` and upload artifact `validation-evidence-${{ github.sha }}` containing the matrix with results, both result sets and the tags file. Only the PostgreSQL-run `.trx` is used as evidence (the no-database run reports integration tests as skipped by design).

- [ ] **Step 1:** Edit the workflow. **Step 2:** Push and confirm the three jobs pass and the artifact holds the four files; report the run honestly if it cannot be observed. **Step 3:** Commit `ci: traceability job and evidence pack`.

### Tasks 5–10: Risk rows, tags and OQ scripts per area group

Same groups and order as A1 Tasks 3–8 (5: MIC, PAT, REV · 6: SIG, AUD, SEC, ADM · 7: RCV, WAT, ENV, ACL, MON · 8: PHY, INS, WST, SOL · 9: MED, CRY, INV, EQP · 10: MDM, OOS, RPT, NTF). Document Control is handled in Task 6: its comment IDs become tags; its risk and OQ documents are not rewritten.

For each area:

- [ ] **Step 1:** Write `documents/Validation/RiskAssessment/RA-<AREA>.md`: one row per URS entry, with a one-sentence justification of S, P and D in the "Failure mode" and "Existing controls" columns. Header: "Scores proposed — pending QA review".
- [ ] **Step 2:** For each FS entry, find the tests that prove it (start from the `**Code:**` location; search tests that construct or call that service). Read each candidate; tag it only if its assertions would fail when the FS behaviour is broken. Add `[Trait("Requirement", "<FS ID>")]` or the `[FS ID]` title suffix. Change nothing else in the test.
- [ ] **Step 3:** Write `documents/Validation/OQ/OQ-<AREA>.md` with one script per High URS entry (or a small group), using `Templates/OQ-test-case.md` fields: ID, Requirements, Objective, Preconditions and test data, Steps with expected result each, Actual result, Pass / Fail, Evidence reference, Executed by / date, Reviewed by / date. Steps use real screen names read from the frontend module; no invented screens.
- [ ] **Step 4:** Run backend build and `RequirementTagTests`; frontend `npm run test` and `npm run debt:check`; `build-rtm.mjs` locally against local results; `--update-baseline`. Commit regenerated `RTM.md`, `Gaps.md`, baseline.
- [ ] **Step 5:** Commit `docs(<area>): risk assessment, test tags, OQ scripts`; pull request body lists counts: URS by class, Covered, Gap, and the gap list for the user.

### Task 11: IQ, PQ, templates, plan update

**Files:**
- Move: `documents/DocumentControl/ML-SYS-VP-001_Validation_Plan.md` (placed there by A1) → `documents/Validation/ML-SYS-VP-001_Validation_Plan.md`, with a pointer
- Create: `documents/Validation/IQ/ML-SYS-IQ-001_Protocol.md`, `PQ/ML-SYS-PQ-001_Protocol.md`, `Templates/OQ-test-case.md` (created in Task 5 if needed earlier), `Templates/Deviation-form.md`, `Templates/Validation-summary-report.md`, `Evidence/README.md`

- [ ] **Step 1:** IQ: one table line per check of spec 5.7 with columns Check | How (command or query) | Expected | Actual | Evidence | Pass/Fail. Commands taken from the repository: migrations from `scripts/Migration/apply-migrations.sh`; trigger list from `AppendOnlyTriggerCatalogPostgresTests.ProtectedTables` (read it; give the `pg_trigger` query it uses); roles and grants from `docs/operations/database-roles-runbook.md` section 5; settings names from `appsettings.json` and `Program.cs` (names only, never values); backup and restore from `scripts/DatabaseBackup/backup.sh` and `scripts/Restore/restore.sh`. Anything not found in the repository is written as "Site to define", not invented.
- [ ] **Step 2:** PQ: the scripts of spec 5.8 in the OQ test-case format, with acceptance criteria and a period field left for the site.
- [ ] **Step 3:** Templates and `Evidence/README.md` (the four items of spec 5.9 and where CI puts them).
- [ ] **Step 4:** Update the validation plan: replace test counts with a pointer to `Traceability/RTM.md`; name IQ, OQ, PQ files; state the risk method once; keep role names `_TBD_`.
- [ ] **Step 5:** Document check passes. Commit `docs: IQ, PQ, templates and validation plan update`.

### Task 12: Close-out

- [ ] **Step 1:** Run the full verification of `CLAUDE.md` section 2 (backend with PostgreSQL, frontend) because test files were edited; report exact pass / fail / skip counts.
- [ ] **Step 2:** Run all `scripts/docs` checks and the builder in `--check` mode.
- [ ] **Step 3:** Update `documents/README.md` with the Validation documents and their status.
- [ ] **Step 4:** Report to the user: totals by status and risk class; the High gaps list; that risk scores are proposals; that no OQ, IQ or PQ has been executed; and ask when to switch on `highRiskGapsBlock`.

## Cross-plan corrections (whole-programme review, 2026-11-11)

These corrections come from reviewing all plans together against the code. Where they differ from the text above, they win. Shared names are defined once in `docs/superpowers/plans/2026-11-11-build-order.md` (section "Programme conventions").

1. Tags may name FS IDs **and** the existing Document Control IDs (`DC-URS-nnn`, `FS-1a-nnn` … `FS-1d-nnn`); the Global Constraint saying "FS IDs only" applies to the new areas only.
2. `documents/Validation/Templates/OQ-test-case.md` is created in Task 5 (first use), not Task 11.
3. Task 2 Step 2 writes the tags file to `backend/TestResults/requirement-tags.json`, not `/tmp`.
