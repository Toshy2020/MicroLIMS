# HPLC Chain — Masters, Solution Preparation and HPLC Workspace (Design)

Date: 2026-09-29 · Branch: `feat/hplc-chain` (from `main` 7b513b5) · Status: draft for user review

Sources: Laboratory Masters and Solution Preparation URS/FS v1.0, HPLC Workspace Frontend URS/FS v1.1 (Downloads, 2026-09-29), plus the user decisions below. Where this spec and the source docs differ, this spec wins.

## 1. Goal

A chain of connected masters that ends in one workspace where the analyst does HPLC testing:

HPLC Method master → Test Master → Item → Specification · Material master + Solution master → Preparation area → HPLC Workspace → existing review/approval.

Masters define what things are, the Preparation area makes usable solutions against real Material Stock lots, and the workspace consumes them. The QC engine does every calculation. The frontend collects data and shows results.

## 2. Decisions (2026-09-29)

| # | Decision |
|---|---|
| D1 | Th.Wt.std and Th.Wt.test are read-only **method constants**, one value per method analyte, the same for every item and stage. Analyst enters only actual weights and responses. |
| D2 | Amount per unit (= assay % × label claim ÷ 100) is **judged** against its own spec limit, with its own status plus an overall status, **only for finished-product stages** (stage role Finished). Other stages report and judge % only. |
| D3 | Reporting basis comes from the sample's **production stage role**: Bulk → Individual (each replicate reported and judged); Finished, Stability, InProcess, Other → Mean. No configuration. |
| D4 | Moisture content (MC) of a reference standard is held on the **stock lot**, like purity. Not typed per SST. |
| D5 | Analytes live on the **method** (name, wavelength, standard, Th.Wt, SST criteria). Single-analyte method = one analyte. |
| D6 | **New parallel module.** New tables and screens. The existing StandardComparison HPLC path, SST runs page and titration/AAS stay untouched; the old HPLC path is retired in a later, separate change. |
| D7 | Solution master and HPLC method are **edited in place** with a required reason and audit trail; no versions. Preparations and runs store a JSON **snapshot**. |
| D8 | Titrant code prefix **VS**. Standardization rules configurable per titrant (Section 3.2). |
| D9 | Material master link is required only for new **Chemical, Indicator, ReferenceStandard** lots. Existing rows stay unlinked. Micro types unaffected. |
| D10 | Indicator attributes: working concentration, solvent, transition range (pH from–to), colour change (from → to), use. |
| D11 | **One SST per run.** All samples in a run link to it. |
| D12 | The run records the **physical column** (required pick from the Column master, compatible with the instrument). |
| D13 | Model routing: Opus orchestrates and reviews; Sonnet subagents write backend + tests; agy writes frontend with the ui-ux-pro-max skill. |

## 3. Masters

### 3.1 Material master — `MaterialMasterEntry` (`/admin/materials`)
- Code (unique per section), name, category (`Reagent | Indicator | ReferenceStandard`), grade/specification, source (free supplier text, or `USP | EP | BP | InHouse` for standards), base unit (`MaterialUnit`), section, `IsActive`.
- Indicator-only optional fields (D10).
- `Material` (stock lot) gains nullable `MaterialMasterEntryId`; required on create when `MaterialType` is Chemical, Indicator or ReferenceStandard, and category must match (Chemical ↔ Reagent). Reference-standard lots gain `MoisturePercent` (0 ≤ x < 100, nullable, required before the lot can be used in an SST).
- Lot usability (backend only): `LotUsability.Check(lot, requiredQuantity)` → usable or a reason (`Expired`, `Insufficient`, `Unlinked`, `MissingPurity`, `MissingMoisture`). Pickers show blocked lots with the reason.
- Deactivate, never delete. Deactivated entries can't be picked for new lots, recipes or methods.

### 3.2 Solution master — `SolutionMaster`, `SolutionComponent` (`/admin/solutions[/:id]`)
- Name, type (`MobilePhase | Diluent | Titrant`), shelf life (value + hours/days), storage condition, final volume basis (mL), optional pH target (± tolerance) and adjusting reagent (Material entry), instructions, section, `IsActive`.
- Components: ordered; each = Material master entry + quantity or ratio + unit. No free text.
- **Titrant extras** (checked against USP *Volumetric Solutions*):
  - Nominal strength: value + unit (`N | M`).
  - Standardization mode: `PrimaryStandard` (standard Material entry, equivalence E = mg of standard per mL of nominal titrant) or `AgainstVolumetricSolution` (reference titrant Solution entry, nominal strength of the reference).
  - Blank required (bool). Replicate count (≥1), factor range (min–max, e.g. 0.95–1.05), max RSD %, standardization validity in days (0 = restandardize before each use).
- Edit in place with reason; audited. Deactivate, never delete.

### 3.3 HPLC Method master — `HplcMethod` + children (`/admin/hplc-methods[/:id]`)
- Header: name, abbreviation (unique, upper-case, used in MP/SST/run codes), section, `IsActive`, effective date.
- Parameters (URS Appendix A): column designation (L-number), length mm, ID mm, particle size µm, optional brand/part no., column temperature °C; elution mode (`Isocratic | Gradient`); gradient table (`HplcMethodGradientStep`: time min, %A, %B, %C, %D); equilibration min; flow mL/min; detector type (`UV | PDA | FLD | RI | ELSD | Other`); injection volume µL; run time min.
- Solutions: required mobile phases (`HplcMethodMobilePhase`: channel label A–D, Solution entry of type MobilePhase, optional ratio %); one Diluent Solution entry.
- Analytes (`HplcMethodAnalyte`, ≥1, ordered): name, wavelength nm, reference-standard Material entry, **Th.Wt.std mg, Th.Wt.test mg**, standard injections (n), SST criteria (max %RSD, min resolution, max tailing, min plates, optional min retention factor, min S/N, min peak-to-valley; null = not checked).
- Edit in place with reason; audit history viewable on the method screen. Deactivate, never delete. Inactive methods can't start new runs.

### 3.4 Test Master, Item, Specification
- New `WorkflowType`/`EquationType` value **`HplcMethodAssay`** with required `TestDefinition.HplcMethodId`. Such tests have no analytes, SST criteria or replicate config of their own; everything comes from the method. The existing types are unchanged.
- Specification rows for an `HplcMethodAssay` test are keyed by item + test code + `HplcMethodAnalyteId` (new nullable FK) + quantity (new enum `HplcSpecQuantity`: `AssayPercent | AmountPerUnit`). Assay % row: lower/upper limits in %. Amount-per-unit row: limits in the claim unit, plus `LabelClaim` + `LabelClaimUnit` (existing fields; unit follows dosage form, e.g. mg/tab, mg/5 mL, µg, IU).
- Replicate counts per stage keep using the existing `TestDefinitionStageReplicates`.

## 4. Preparation area (`/preparation`, `/preparation/new`, `/preparation/:id`)

Code name `SolutionPreparation` (the name "SamplePreparation" is already taken by micro).

- **Entities:** `SolutionPreparation` (code, solution master id + JSON recipe snapshot, type, method id for MP only, status, preparedBy/At, expiresAt, final volume, measured pH, signature id, section); `SolutionPreparationComponent` (component snapshot, stock lot id, actual quantity, unit); `SolutionPreparationStatusHistory` (from, to, by, at, reason).
- **Flow:** select an active Solution entry (+ method for MP; the method must list that mobile phase) → per component pick a usable lot and enter the actual quantity → final volume, measured pH if required (out of tolerance blocks) → review, showing the expiry that will apply → sign (`ElectronicSignature`) and complete.
- **Completion (one DB transaction):** re-check each lot's usability, deduct via `MaterialService.ConsumeAsync`, allocate the code, set `expiresAt = preparedAt + shelf life` (lab-local clock via `ILabClock`), status Prepared. Any failure rolls back everything; the preparation stays In Progress.
- **Codes:** MP `MP-{ABBR} {nn}/{MM}/{yyyy}` (sequence per abbreviation), DL `DL-{nn}/{MM}/{yyyy}`, VS `VS-{nn}/{MM}/{yyyy}` (sequence per prefix). The sequence runs through the calendar year and resets on 1 January (lab-local). Allocation through a new `CodeSequences` table (scope key + year + last value), incremented with a row-locking `UPDATE … RETURNING` inside the transaction.
- **Status:** In Progress → Prepared | Cancelled (reason, no stock); Prepared → Expired (automatic) | Discarded (reason, stock not restored). Expired, Discarded and Cancelled are final. Expiry: a hosted service flips due rows every 5 min and writes history. Every read also treats `Prepared && expiresAt ≤ now` as Expired, so there is no window in which it is usable.
- **Titrant standardization** (`TitrantStandardization` + `TitrantStandardizationReplicate`): on a Prepared VS. Per replicate: primary-standard lot + weight mg, or reference VS preparation + its volume mL; titrant volume mL; blank mL when required. The engine (`TitrationEngine.StandardizationFactor`) computes:
  - PrimaryStandard: `f = (W × P/100) / ((V − V_blank) × E)` (P from the lot; 100 when the lot has no purity).
  - AgainstVolumetricSolution: `f = (V_ref × f_ref × N_ref) / ((V − V_blank) × N_nominal)` (f_ref = the reference's current factor, which must be valid).
  - Reported factor = mean; pass when every factor is in range and RSD ≤ max. Stored with all inputs, the analyst and a signature. Re-standardization adds a new record; earlier ones are kept. A titrant shows **Not standardized** or **Standardization due** (older than validity days) and can't be consumed by tests. Anything that used a factor stores the factor it used.
- **Traceability:** the preparation shows its snapshot, lots, quantities, analyst, times, expiry, standardizations, and the runs/SSTs that used it. The Material lot view gains a "Consumed by preparations" list.
- **Permissions:** `Solutions.Prepare`, `Solutions.Standardize`; masters `MaterialMaster.Manage`, `SolutionMaster.Manage`, `HplcMethods.Manage`. Section-scoped as elsewhere.

## 5. HPLC Workspace (`/hplc-workspace/...`, routes per FS §3)

- **Instruments:** `Equipment` with type Hplc in the user's sections. State: **Running** (has an open run), **Unavailable** (inventory OutOfService/Retired, or calibration overdue, with the reason), else **Available**.
- **Run** — `HplcRun`: instrument, method id + **method snapshot JSON**, column id (D12), analyst, started at, status (`Open | Completed | Abandoned`), code `{ABBR} RUN {nn}/{MM}{yyyy}`. `HplcRunMobilePhase`: per method channel, the selected preparation (must be Prepared, unexpired, the required Solution entry, prepared for this method). Only one open run per instrument.
- **SST** — `HplcSstRecord` (1:1 with the run, D11): code `{ABBR} S.S {nn}/{MM}{yyyy}`. The sequence is shared with the existing `SystemSuitabilityRuns` codes of the same abbreviation, so codes never repeat. Per analyte (`HplcSstAnalyte`): standard lot (usable ReferenceStandard lot of the method's standard entry; purity and MC **snapshotted from the lot**), actual std weight mg, standard responses (n = method injections; engine computes mean + RSD), transcribed values from the report (RSD, resolution, tailing, plates, and the optional ones). The engine evaluates them against the snapshot criteria. Confirmation requires the **standard report upload**, then a signature. Status Pending → Passed | Failed. A failed SST keeps the run locked; the analyst abandons the run (with a reason) and starts a new one.
- **Samples** — `HplcRunSample`: TestOrder (test type `HplcMethodAssay` for this method, open, in the user's sections, not in another open run). Assignment only after the SST has passed. Removal needs a reason; rows are soft-removed and audited.
- **Entry** — `HplcSampleReplicate`: replicate no., actual test weight mg, response per analyte. Replicate count comes from the stage replicate config. Rows are editable until submission.
- **Engine** — reuse `StandardComparisonCalculator.CalculatePreparationAssay` per replicate per analyte: `A% = (R_test / R̄_std) × (W_std / Th.Wt.std) × (Th.Wt.test / W_test) × ((100 − MC)/100) × P`. Reported value per D3 (mean, or each replicate). Amount per unit per D2. Spec evaluation through the existing `SpecificationEvaluator`. Results are written to the generic `TestAnalysis` / `ParameterResult` (new equation type) with a calculation JSON (inputs, snapshot ids, basis), so the existing review, approval, sample summary and CoA work unchanged.
- **Evidence** — `HplcEvidence` (context `Run | Sst | Sample`, kind, file via `IFileStorageService`, PDF/images, uploader, time). No delete or replace; a new upload with a reason supersedes and the old file stays visible. Gates: standard report before SST confirm; sample result report before Send for Review.
- **Send for Review:** enabled when the backend reports the sample complete (SST passed, replicates valid, result calculated, report uploaded). Signed; uses the same submission path as the other FP analyses. Entries go read-only; Return to Analyst reopens them through the existing return flow (the prior result is superseded, as today).
- **Run completion:** the analyst completes the run when every assigned sample is submitted or removed. Run History lists completed and abandoned runs.
- **Review view:** reports beside the entered weights/responses and the method snapshot parameters (URS-HPLC-UI-057).
- **Permission:** `Hplc.Operate` (run, SST, entry, submit); section-scoped.

## 6. Cross-cutting

- Backend owns every rule (usability, expiry, codes, gates, calculations). The frontend never decides them; disabled actions show the backend's reason.
- Nothing is hard-deleted. All entities are audited through `MicroLimsDbContext.SaveChanges`. Signed actions use `ElectronicSignature`.
- Optimistic concurrency (`IVersionedEntity`) on masters and open records. Duplicate-submit guards on commands.
- UI: MUI + theme tokens, shared dialogs, no `alert/confirm`, status = text + icon.
- DTOs only (no entity serialization); dates shown in lab-local time.

## 7. Delivery (one slice at a time, stop after each)

| Slice | Content | Backend | Frontend |
|---|---|---|---|
| S1 | Material master + lot link + MC on lot | Sonnet | agy |
| S2 | Solution master (incl. titrant config) | Sonnet | agy |
| S3 | HPLC Method master + `HplcMethodAssay` test type + spec keys | Sonnet | agy |
| S4 | Preparation area (MP/DL/VS, CodeSequences, deduction, expiry job) | Sonnet | agy |
| S5 | Titrant standardization + TitrationEngine | Sonnet | agy |
| S6 | Workspace backend: run, SST, samples, entry, engine → ParameterResult, evidence, send for review | Sonnet | — |
| S7 | Workspace frontend (overview, run wizard, SST, samples, entry/result, history, review view) | — | agy |
| S8 | Browser end-to-end: master → prepare → run → SST → samples → result → review → approve → CoA; lot → preparation → run trace | Opus | — |

Each slice: migration applied to LIMSV2 after a backup; full Postgres suite green (`bash .claude/scripts/run-postgres-tests.sh`); `npm run build` + eslint clean; Opus reviews the whole diff (not just the feature) before committing. Local only; no push until the user asks. Before a push, Neon pre-merge checks for the new migrations.

## 8. Testing
- Engine unit tests with hand-checked numbers: assay (incl. MC/P), amount per unit, mean vs individual, titrant factor (both modes, blank, range/RSD).
- Postgres integration: atomic deduction + rollback, code sequence under concurrency and year reset, expiry job, SST gate, one open run per instrument, spec evaluation for both quantities by stage.
- Frontend: component and interaction tests for the lot picker, wizard, SST gate, replicate table, upload gates, send for review.

## 9. Open items
- O1: Confirm Appendix A parameter list and USP <621> text against the current official USP–NF (source docs OI-1).
- O2: Purity correction and validity period in titrant standardization are lab-SOP additions, not USP text; confirm against the site SOP.
- O3: Run code format `{ABBR} RUN nn/MMyyyy` is this spec's choice (the docs name a run code but give no format).
- O4: Working-standard preparation area: separate URS/FS (out of scope).
