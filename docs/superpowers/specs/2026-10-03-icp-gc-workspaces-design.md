# ICP and GC Workspaces — remodel on the HPLC workspace (Design)

Date: 2026-10-03. Branch: `feat/instrument-workspaces` (from `origin/main` 9836de7).
Builds on: `2026-09-29-hplc-chain-design.md` (method master → run → qualification gate → samples → evidence → review gate).

## 1. Goal

Analysts work the same way on every instrument. GC gets a workspace (it has none today);
ICP-OES moves from the test-configured Calibration Runs page to a method master and a
run-based workspace. No URS/FS exists — the HPLC workspace is the reference model.

## 2. Decisions (2026-10-03)

| # | Decision |
|---|---|
| D1 | GC covers **both** assay (% label claim vs reference standard) and **residual solvents** (USP <467>, ppm). |
| D2 | ICP covers **both** mineral assay (mg/unit, % label claim) and **elemental impurities** (µg/g vs limit). |
| D3 | ICP calibration entry = **summary only**: one correlation coefficient r per element, typed from the Syngistix Calibration Display. The Syngistix report upload is required before the calibration is confirmed. |
| D4 | Blank, ICV and CCV are **optional** checks per ICP method, all off by default. Only r is mandatory. |
| D5 | **AAS is retired** — not part of the new workspace; removed with the old path. |
| D6 | Old ICP/AAS data (`CalibrationRun*`, ElementalAssay results) is **deleted**, gated on a read-only Neon count (§7 slice R). Local LIMSV2 has 2 runs, both AAS. |
| D7 | GC residual solvents: **LIMS calculates ppm** from peak areas. |
| D8 | ICP samples: analyst enters **solution mg/L** from the Syngistix *Conc. in Calib. Units* tab; LIMS applies weight, volume and dilution. Supersedes the 2026-09-19 "Syngistix reports sample ppm" decision (`ReportedConcentrationBasis.SamplePpm`). |
| D9 | Architecture **Approach 1**: GC extends the existing `Hplc*` tables with a `Technique`; ICP gets its own `Icp*` tables copying the HPLC run pattern. Class/table names stay `Hplc*` (UI says "Chromatography"); renaming is out of scope. |
| D10 | One permission `Hplc.Operate` for HPLC, GC and ICP, shown as "Instrument workspaces – operate". Method masters stay under `MasterData.Manage`. |
| D11 | Every agy frontend slice must load and apply the **ui-ux-pro-max** skill (and frontend-design) before writing UI (§6). |

## 3. GC on the chromatography module

### 3.1 Method master (`HplcMethod`)

- `Technique` (`Hplc | Gc`), chosen at creation, then read-only. Existing rows = `Hplc`.
- `ResultMode` (`Assay | ResidualSolvents`). HPLC methods are always `Assay`.
- Column: designation (L-codes on HPLC, G-codes on GC, e.g. G43); length (HPLC mm, GC entered in m, stored mm);
  internal diameter mm; `ParticleSizeUm` becomes nullable (HPLC only, still required there);
  new `FilmThicknessUm` (GC only, required there).
- GC conditions (new nullable columns, required when `Technique = Gc`): `CarrierGas` (`Helium | Nitrogen | Hydrogen`),
  `CarrierFlowMlPerMin`, `SplitRatio` (null = splitless), `InletTemperatureC`, `DetectorTemperatureC`.
- Oven program: new child `HplcMethodOvenStep` (`StepNo`, `RateCPerMin` — null on the first step, `TemperatureC`, `HoldMin`).
  At least one step on GC. Mobile phases, gradient, flow rate, column temperature and equilibration stay HPLC-only.
- Headspace (optional, GC): `HeadspaceEnabled`, `HeadspaceEquilibrationTemperatureC`, `HeadspaceEquilibrationMin`, `HeadspaceTransferLineTemperatureC`.
- `HplcDetectorType` gains `Fid, Tcd, Ecd, Ms`. GC methods accept only those; HPLC only the existing ones.
- Analyte `WavelengthNm` becomes nullable; required on HPLC, not used on GC.
- Residual-solvents analytes: name (solvent), standard master entry, `StandardConcentrationUgPerMl`, standard injections,
  SST criteria (same columns as today). Th.Wt fields are not used in this mode.
  The method gains `SampleSolutionVolumeMl` (required in this mode).
- Validation lives in `HplcMethodService` (backend); the dialog only mirrors it.

### 3.2 Specification

- New `ResultBasis.Ppm`. Residual-solvent spec rows link to a method analyte (solvent) and use `NotMoreThan` with the
  limit in ppm (e.g. methanol NMT 3000).

### 3.3 Run and calculation

- Run, SST, sample assignment, evidence, send-for-review and review gate: unchanged HPLC behaviour.
- The run snapshot carries the technique. On GC: instrument must be `EquipmentType.Gc`; the column must list the
  instrument as compatible and match the G-designation; **no mobile-phase step** (carrier gas is a utility).
- `ChromatographyColumn.CompatibleEquipment` accepts `Gc` as well as `Hplc`.
- Assay mode: existing `HplcAssayCalculator`, unchanged.
- Residual-solvents mode: new `ResidualSolventCalculator` (Application/Helpers):
  `ppm = (C_std [µg/mL] × V [mL] / W [g]) × (r_u / r̄_std)`, where `W` = replicate `ActualWeightMg / 1000`,
  `r̄_std` = mean SST standard response for that solvent, `V` = method `SampleSolutionVolumeMl`.
  Reported value = mean of replicates; judged by `SpecificationEvaluator`; calculation JSON stores every input.
- Recorder: `HplcMethodAssayRecorder` branches on `ResultMode`; the workflow type stays `HplcMethodAssay`.

### 3.4 UI

- Menu: **HPLC Workspace** and **GC Workspace** → the same pages with a `technique` route parameter
  (instrument list, run history and eligible samples filtered by it).
- Method master page: technique filter; the dialog shows HPLC or GC sections by technique.
- Run wizard: GC skips the mobile-phase step.
- Sample entry in residual-solvents mode: one area per solvent per replicate; result card shows ppm vs limit.

### 3.5 Out of scope

GC pressure programs, internal-standard calculation, USP <467> Procedure A/B identification flow, renaming `Hplc*`.

## 4. ICP module (new `Icp*` tables)

### 4.1 Method master (`IcpMethod`)

- Section, name, abbreviation (upper-case, used in codes), effective date, active; edited in place with a reason; one audit
  event per edit with before/after JSON (same as `HplcMethod`).
- `Mode` (`MineralAssay | ElementalImpurities`).
- `IcpMethodElement` rows: display order, symbol (e.g. "Zn"), wavelength nm, view (`AnalyteView` radial/axial),
  conversion factor (default 1). Kept by Id on update so spec rows stay linked.
- Calibration: `StandardLevelsMgPerL` (comma list, parsed by `CalibrationStandardLevelsHelper`, ≥ 2 levels),
  calibration standard master entry, `MinCorrelation` (e.g. 0.999).
- Optional checks: `RequireBlank` + `BlankMaxMgPerL`; `RequireIcv` + ICV standard master entry (second source) +
  `IcvNominalMgPerL` + recovery low/high %; `RequireCcv` + `CcvNominalMgPerL` + recovery low/high %.
- `MaxCalibrationAgeHours` (default 24).
- Sample-prep defaults: `SampleVolumeMl`, `DilutionFactor` (default 1) — prefilled on entry, editable.
- Test Master: a new workflow type `IcpMethodAssay` references an `IcpMethod` (like `HplcMethodAssay` → `HplcMethod`).
  Spec rows link to an `IcpMethodElement`; bases: `MgPerUnit`, `PercentLabelClaim` (mineral assay), `MgPerKg` = µg/g
  (impurities), with the existing `LabelClaim` / `ConversionFactor` columns.

### 4.2 Run (`IcpRun`)

- Instrument `EquipmentType.IcpOes`, in the user's sections, available (same rules as HPLC); one open run per instrument.
- Code `{ABBR} RUN {nn}/{MM}{yyyy}`; method snapshot JSON at start; status `Open | Completed | Abandoned` (abandon needs a reason).
- **Calibration** — `IcpCalibration` 1:1 with the run; per element `IcpCalibrationElement`: r (required); blank mg/L
  (if required); ICV measured mg/L → recovery % (if required). Standard lots: usable lots of the method's calibration
  (and ICV) standard entries. Each element passes or fails on its own with failure reasons. Confirmation needs the
  Syngistix report upload, then a signature. The run continues if ≥ 1 element passed; failed elements are blocked on
  this run. Calibration time starts the `MaxCalibrationAgeHours` clock: no sample entry/submit after it expires.
- **CCV** — `IcpCcvReading` (element, measured mg/L, recovery %, pass, entered by/at), only when `RequireCcv`.
  A failing CCV **locks** that element on the run for every sample not yet submitted.
  `// ponytail:` ceiling — already-submitted samples are not pulled back; the reviewer sees the failed CCV on the run.
- **Samples** — `IcpRunSample`: open TestOrder of type `IcpMethodAssay` for this method, not in another open run;
  assigned only after calibration is confirmed; removal with a reason (soft, audited).
- **Entry** — per sample: unit amount (average unit weight g or dose mL). `IcpSampleReplicate`: weight g, volume mL,
  dilution, and per element the solution mg/L (field labelled "Conc. in Calib. Units, mg/L").
- **Engine** — `IcpContentCalculator` (Application/Helpers), per replicate per element:
  - `content µg/g = C × V × DF / W`
  - `mg/unit = content × unitAmount / 1000 × conversionFactor`
  - `% LC = mg/unit / labelClaim × 100`
  - `C > top standard` → "Over range – dilute and re-measure", blocks submit; `C < lowest standard` → reported `<LOQ`
    (passes a `NotMoreThan` limit, otherwise needs review — same rule as today).
  Reported value = mean of replicates; results through `SpecificationEvaluator` into `TestAnalysis`/`ParameterResult`
  with a calculation JSON, so review, approval, sample summary and CoA work unchanged.
- **Evidence** — same model as `HplcEvidence` (context `Run | Calibration | Sample`, supersede with reason, never delete).
  Gates: Syngistix calibration report before confirm; sample result report before Send for Review.
- **Send for Review / Return / run completion / run history** — as HPLC.

### 4.3 UI

- Menu **ICP Workspace**: instrument cards → run workspace (header, calibration panel with per-element r / blank / ICV
  table and status chips, CCV panel, sample assignment, entry, history). Review panel mounted where `HplcReviewPanel` is.
- Method master page **ICP Methods** under laboratory configuration.
- The old "Calibration Runs (ICP-OES / AAS)" menu entry is removed in slice R.

### 4.4 Retirement (slice R, gated)

Run the read-only Neon count (`CalibrationRuns`, `CalibrationRunAnalytes`, ElementalAssay `TestAnalyses`, tests with
`WorkflowType = ElementalAssay`). Only if prod holds no real records: delete `CalibrationRun*` entities/tables,
`ElementalAssayRecorder`, `ElementalAssayPanel`, `CalibrationRunsPage`/`ReportPage`, the `Cal*` fields and
`CalInstrumentType`/`ReportedConcentrationBasis` on `TestDefinition`, and the AAS test option. `WorkflowType.ElementalAssay`
stays as a retired int value. `EquipmentType.Aas` stays. If prod holds real records: stop and decide with the user.

## 5. Cross-cutting

- Clean Architecture as today: entities in Domain, calculators/validation in Application, EF configs + migrations in Persistence.
- Frontend never calculates results or verdicts; verdict banners come from server status only.
- Every sensitive action (method edit, calibration confirm, CCV entry, sample submit, abandon, removal, supersede) is
  signed and/or audited exactly like the HPLC equivalent.
- Local DB backup (`E:/MicroLIMS/backups`) before applying each migration. All build output and temp files on `E:`.

## 6. Frontend rule for agy (D11)

Every agy frontend prompt starts with:
1. "Before writing any UI code, load and follow the **ui-ux-pro-max** skill, then the **frontend-design** skill.
   State in your first output which guidelines you are applying."
2. Reuse the lab kit `frontend/src/components/lab` (LabPage, KpiStrip, FilterBar, RegisterTable, FormDialog,
   ResultSection, CriteriaCard, VerdictBanner, NumericCell) and the existing `modules/hplcWorkspace` components.
3. Keep files small (no file > ~400 lines; split by panel).
4. No client-side result or verdict math.
I verify every slice with `tsc`, eslint and a browser check before committing.

## 7. Delivery (stop after each slice)

| # | Slice | Owner |
|---|---|---|
| G1 | GC backend: technique, GC method fields, oven steps, detectors, residual-solvents mode, `ResidualSolventCalculator`, `ResultBasis.Ppm`, run technique checks, migration | Sonnet subagent |
| G2 | GC frontend: method dialog GC sections, HPLC/GC workspace menus with technique filter, wizard without mobile phases, solvent entry | agy (§6) |
| G3 | GC seed (one USP <467> Class 2 method, one GC assay method, columns, GC instrument) + browser E2E | Opus |
| I1 | ICP method master: backend (Sonnet) + frontend (agy, §6) | Sonnet / agy |
| I2 | ICP run backend: run, calibration, CCV lock, samples, `IcpContentCalculator`, recorder, evidence, controller | Sonnet subagent |
| I3 | ICP workspace frontend | agy (§6) |
| I4 | ICP browser E2E with the real report: Ca/Cu/Fe/Mg/Mn/Zn, levels 0.1/0.5/1/3/6 mg/L, r = 0.999386 / 0.999227 / 0.999721 / 0.999463 / 0.999391 / 0.996383 at min r 0.999 → Zn blocked, others pass | Opus |
| R | Neon count → delete old calibration-run / elemental / AAS path | Opus, gated |

## 8. Testing

- Unit: `ResidualSolventCalculator` (hand-checked example), `IcpContentCalculator` (content, mg/unit, %LC, over-range,
  <LOQ), per-element calibration evaluation (r, blank, ICV), CCV lock, GC method validation (technique-specific required
  fields), run technique checks (instrument type, column designation, no mobile phase on GC).
- Full Postgres suite after every backend slice (`dotnet build` first — the script uses `--no-build`).
- One browser E2E per technique (G3, I4) before the module is called done.

## 9. Open items

- Neon count result decides slice R.
- GC test data: which real GC methods the lab runs (seed uses USP <467> Class 2 + one assay until told otherwise).
