# Titration assay test - design

Date: 2026-10-03. Branch: `feat/titration-test` (from main after PR #75).
Plan and user decisions: `E:\MicroLIMS\rls-tmp\workflow-diagrams\titration-test-plan.md` (section 4).

## 1. Scope (user decisions)

- New `WorkflowType.Titration` (append to enum; never renumber) and `EquationType.Titration`.
- Reaction types: acid-base (aqueous or nonaqueous), redox, complexometric, precipitation,
  Karl Fischer (volumetric). Modes: Direct, Residual (back titration).
- Two calculation methods chosen per test: `UspFactor` and `Relative` (site SOP STM-PC-013 6.9.2.5).
- Replicates and max RSD % per test. Blank optional per test; when on, ONE blank per series.
- LOD / water % and average unit weight entered on the titration screen.
- Relative method standard: reference standard lot OR approved working standard lot (in stock,
  in date); its weight is deducted from the lot.
- Titrant factor `Due` in Relative mode: warning shown and stored, not a block.
  UspFactor mode blocks `NotStandardized` and `Due`.
- Every result stores the titrant preparation, factor used and standardization record id.

## 2. References

- USP <541> Titrimetry; USP monograph form `Result = [(V - B) x N x F x 100] / W`
  (e.g. Ascorbic Acid: 0.1 N iodine VS, F = 88.06 mg/mEq; Calcium Glycerophosphate:
  0.1 M edetate disodium VS, F = 40.08 mg/mmol). Residual: `[(B - V) x N x F x 100] / W`.
- Ph. Eur. nonaqueous (perchloric acid in glacial acetic acid) volume correction:
  `V_corr = V x (1 + (T_std - T_titration) x 0.0011)`; coefficient configurable per test.
- USP <921> water determination, volumetric KF: water % = `(V - B) x F_KF / W x 100`,
  F_KF = mg water per mL of reagent.

## 3. Domain

### 3.1 Enums (new files in MicroLIMS.Domain/Enums, stored as int like the other config enums)
- `TitrationType { AcidBase, Redox, Complexometric, Precipitation, KarlFischer }`
- `TitrationMode { Direct, Residual }`
- `TitrationCalculation { UspFactor, Relative }`
- `TitrationEndpoint { Visual, Potentiometric }`
- `TitrantStrengthUnit` += `MgWaterPerMl` (append). KF reagents are Solution masters of type
  Titrant with this unit; standardization works unchanged (PrimaryStandard, E = mg standard
  per mL of nominal reagent; water standard E = nominal, sodium tartrate dihydrate
  E = nominal / 0.1566 entered by the user).
- `ResultBasis` += `PercentAsIs, PercentDriedBasis, PercentAnhydrousBasis` (append).

### 3.2 TestDefinition columns (all nullable, titration only; same plain-column style)
| Column | Type | Notes |
|---|---|---|
| TitrationType | TitrationType? | required for Titration |
| TitrationNonAqueous | bool? | acid-base only |
| TitrationMode | TitrationMode? | required |
| TitrationCalculation | TitrationCalculation? | required |
| TitrantSolutionMasterId | int? FK SolutionMaster (Restrict) | must be Type=Titrant, same section; KF => unit MgWaterPerMl and vice versa |
| TitrationEquivalencyFactor | decimal(18,6)? | F in mg per mEq/mmol; required for UspFactor non-KF; not used for KF (F = 1) or Relative |
| TitrationBlankRequired | bool? | default false. Residual + UspFactor without blank requires excess master + volume |
| TitrationExcessSolutionMasterId | int? FK SolutionMaster | Residual only (the VS added in excess) |
| TitrationExcessVolumeMl | decimal(10,3)? | Residual only |
| ReplicateCount | (existing column) | 1-10 for titration |
| TitrationMaxRsdPercent | decimal(6,3)? | optional; RSD over replicate results |
| TitrationEndpoint | TitrationEndpoint? | required |
| TitrationIndicator | string(200)? | required when Visual |
| TitrationTempCorrection | bool? | only AcidBase + NonAqueous |
| TitrationExpansionCoefficient | decimal(8,6)? | default 0.0011 when TempCorrection |
| TitrationStandardEntryId | int? FK MaterialMasterEntry | Relative only: the reference standard master entry |
| TitrationStdTheoreticalWeightMg | decimal(18,4)? | Relative only (ThW_std) |
| TitrationSampleTheoreticalWeightMg | decimal(18,4)? | Relative only (ThW_test) |

Validation in TestDefinitionMasterDataService (create + update), clear messages:
type/mode/calculation/endpoint required; titrant master exists, is Titrant, same section;
KF <-> MgWaterPerMl consistency; KF => UspFactor only, Direct only; F > 0 for UspFactor
non-KF; Residual => excess master + volume > 0 unless blank required; Relative =>
standard entry + both theoretical weights > 0; temp correction only AcidBase+NonAqueous;
ReplicateCount 1-10; MaxRsd > 0 when set; titration fields must be null for other workflows
(clear them). Titration tests have no workflow steps (like other analysis workflows).

### 3.3 Specification
Titration specs use limit types Range / NotLessThan / NotMoreThan / TargetWithTolerance and
`ResultBasis` in { PercentAsIs, PercentDriedBasis, PercentAnhydrousBasis,
PercentLabelClaim, MgPerUnit } (required for titration). PercentLabelClaim / MgPerUnit
require `LabelClaim` (> 0, mg per unit) - finished products only. One spec parameter per
titration test is the normal case; recorder handles exactly the specs supplied (one
ParameterResult each, same raw data).

### 3.4 Snapshot of the analysis
Stored on `TestAnalysis.ConditionsJson` (existing column) as a JSON object
`TitrationSnapshot`: type, mode, calculation, endpoint, indicator, titrant preparation id +
code + master name, nominal strength + unit, factor used, factor state, standardization id +
date, F, blank volume, excess preparation id/code/volume/factor (residual), temperatures +
coefficient (if corrected), LOD/water %, average unit weight, standard lot id/label/purity/
moisture/weight/titre (relative), warnings[]. Readings: one `ResultReading` per replicate
with `Kind = ReadingKind.Titration`, `Value1` = sample weight mg, `Value2` = titrant volume
mL (as entered), `Value3` = corrected volume mL (after temp correction; = Value2 otherwise),
`ComputedValue` = replicate result in the spec unit.

## 4. Calculation engine (pure, `TitrationEngine`, unit tested)

Inputs per replicate: W (mg), V (mL). Series: B (mL, 0 when no blank), N_nom, factor f.
1. Temperature correction (optional): `V' = V x (1 + (T_std - T_t) x k)`; also applied to B
   and to the standard titre. T_std = titrant standardization temperature, T_t = titration
   temperature, both entered on screen.
2. UspFactor, Direct: `mg = (V' - B') x N_nom x f x F` (KF: `x F_KF` where F_KF = N_nom x f, F = 1).
3. UspFactor, Residual with blank: `mg = (B' - V') x N_back x f_back x F`.
   Residual without blank: `mg = (V_ex x N_ex x f_ex - V' x N_back x f_back) x F`.
   (N in mEq/mL or mmol/mL; both titrants must share the unit kind.)
4. Relative: `pct_raw = [(V' - B') / (V_std' - B')] x (W_std / ThW_std) x (ThW_test / W)
   x (100 - MC_std)/100 x P_std` (P = lot purity %, MC = lot moisture %, default 0).
   UspFactor: `pct_raw = mg / W x 100`.
5. Basis (from spec):
   - PercentAsIs: pct_raw
   - PercentDriedBasis / PercentAnhydrousBasis: `pct_raw x 100 / (100 - LOD)` (LOD or water %
     entered, 0 <= LOD < 100, required)
   - PercentLabelClaim: UspFactor `mg / W x AvgUnitWt / LabelClaim x 100`; Relative: pct_raw
     (theoretical weights already express label claim)
   - MgPerUnit: UspFactor `mg / W x AvgUnitWt`; Relative `pct_raw x LabelClaim / 100`
   AvgUnitWt (mg) required for UspFactor PercentLabelClaim / MgPerUnit.
6. Mean of replicate results; RSD % (sample SD, n-1) when n >= 2; RSD > max => status
   `RequiresReview` with reason (not OOS). Mean compared with the spec via the existing
   specification evaluator (SpecLimitParser / same helper the measurement recorder uses).
Guards: V' > B' (direct), B' > V' (residual with blank), V_std' > B', W > 0, factor > 0,
results finite. Unit tests with hand-checked numbers: ascorbic acid (iodine, F 88.06),
calcium (EDTA, F 40.08), residual, KF, relative, temp correction, each basis, RSD.

## 5. API (fixed contract - backend and frontend build against this)

Permission: `PermissionConstants.TestWorkflowExecute` (same as other recorders).

### GET `/test-workflow/{testOrderId}/titration-context` -> `ApiResponse<TitrationContextDto>`
```
TitrationContextDto {
  testOrderId: int, testCode: string, displayName: string,
  titrationType: "AcidBase"|"Redox"|"Complexometric"|"Precipitation"|"KarlFischer",
  nonAqueous: bool, mode: "Direct"|"Residual", calculation: "UspFactor"|"Relative",
  endpoint: "Visual"|"Potentiometric", indicator: string|null,
  equivalencyFactor: decimal|null, blankRequired: bool, replicateCount: int,
  maxRsdPercent: decimal|null, tempCorrection: bool, expansionCoefficient: decimal|null,
  stdTheoreticalWeightMg: decimal|null, sampleTheoreticalWeightMg: decimal|null,
  excessVolumeMl: decimal|null,
  titrant: { solutionMasterId, name, nominalStrength, strengthUnit: "Normal"|"Molar"|"MgWaterPerMl" },
  excessTitrant: same shape | null,
  titrantPreparations: TitrantPreparationOption[],   // for the titrant master
  excessPreparations: TitrantPreparationOption[],    // residual only, else []
  standardLots: StandardLotOption[],                 // relative only, else []
  specifications: [{ specificationId, parameterName, resultBasis, unit, specLimit, labelClaim, labelClaimUnit }]
}
TitrantPreparationOption {
  preparationId, code, expiresAt, factor: decimal|null, factorState: "Valid"|"BeforeEachUse"|"Due"|"NotStandardized",
  standardizedAt|null, validUntil|null, standardizationId|null,
  usable: bool, warning: string|null, blockReason: string|null
}
StandardLotOption { materialId, lotLabel, kind: "ReferenceStandard"|"WorkingStandard",
  purityPercent, moisturePercent|null, expiryDate, quantityRemaining, unit }
```
Preparation options: Prepared, not expired, same section, from the configured master.
`usable=false` + blockReason for NotStandardized always, and for Due/BeforeEachUse-not-today
in UspFactor; Relative: Due => usable, warning "Titrant standardization is due - factor not
used in the relative calculation" (exact wording free).

### POST `/test-workflow/{testOrderId}/record-titration-result` -> same `TestWorkflowResult` as other recorders
```
RecordTitrationResultRequest {
  analysedAt: DateTime, equipmentId: int|null,
  titrantPreparationId: int, excessPreparationId: int|null,
  blankVolumeMl: decimal|null,
  standardizationTemperatureC: decimal|null, titrationTemperatureC: decimal|null,
  lossOnDryingPercent: decimal|null, averageUnitWeightMg: decimal|null,
  standard: { materialId: int, weightMg: decimal, titreMl: decimal } | null,   // relative only
  specificationIds: int[],
  replicates: [{ sampleWeightMg: decimal, titrantVolumeMl: decimal }],
  password: string, comment: string|null
}
```
Server rules: everything in section 4 + replicate count == configured; blank given iff
required; excess prep iff residual; standard iff relative (lot = configured standard entry,
ReferenceStandard or approved WorkingStandard via the same usability check as HPLC SST
(`LotUsability.Check`), weight deducted mg -> g/kg like `TitrantStandardizationService`);
temperatures iff temp correction; LOD iff dried/anhydrous basis; avg unit weight iff
UspFactor label claim / mg per unit; equipment (if given) InService, FP section (existing
check) and, when its type is known, Titrator. Recording goes through
`ValidateTestAnalysisOrderAsync(..., WorkflowType.Titration, ...)` and
`PersistTestAnalysisAndFinalizeAsync(..., conditionsJson: snapshot, resultType: Numeric,
workflowDisplayName: "Titration", ...)` so e-sig, Result row, review hand-off and the
review-return supersede path are reused. Stock deduction happens after signing in the same
SaveChanges (mirror TitrantStandardizationService). Status: worst of spec comparison and
RSD rule.

Also: test definition request/response DTOs carry the section 3.2 fields; the test
definition option list used by the frontend (`masterDataOptions.getTestDefinitions`) exposes
`workflowType` so the workspace can route to the TitrationPanel.

## 6. Frontend

- Test master (TestMasterPage, physicochemical): WorkflowType "Titration" option; Titration
  section with the 3.2 fields, conditional visibility (excess only for Residual, standard +
  theoretical weights only for Relative, temp correction only AcidBase+NonAqueous,
  indicator only Visual, F hidden for KF/Relative). Titrant pickers list Solution masters of
  type Titrant (KF filters MgWaterPerMl).
- Solution master titrant section: strength unit option "mg H2O/mL (Karl Fischer)".
- Specification dialog: for Titration tests offer the five ResultBasis values (labels:
  "% as is", "% dried basis", "% anhydrous basis", "% of label claim", "mg per unit") and
  LabelClaim when label claim / mg per unit.
- Workspace: `TitrationPanel` (new, same structure as GravimetricPanel) rendered by
  TestWorkflowDialog for workflowType "Titration". Loads titration-context; shows method
  card (type, mode, method, endpoint/indicator, F, titrant nominal strength); titrant
  preparation picker with factor + state chip (Valid green, Due amber + warning text,
  blocked options disabled with reason); equipment picker (existing pattern); optional
  blank row; residual excess prep picker; relative standard lot picker + weight + titre;
  temperature inputs; LOD / avg unit weight when needed; replicate grid (weight mg, volume
  mL) with live preview computed client-side for display only (server is authoritative);
  RSD; SignatureDialog; outcome view like Gravimetric. Results card / sample summary show
  titrant code + factor used and replicate table (readings Kind "Titration").

## 7. Out of scope
Coulometric KF; automatic LOD lookup from another test; titrator instrument data import.

## 8. Amendments after adversarial review (2026-10-03) - these OVERRIDE sections 3-6

A1. Relative method uses an empirical standard factor, no theoretical weights.
    Drop `TitrationStdTheoreticalWeightMg` and `TitrationSampleTheoreticalWeightMg` (do not add
    them; remove if already added). For each standard titration s:
    `K_s = W_std,s x P/100 x (100 - MC)/100 / (V_std,s' - B')`  [mg analyte per mL titrant]
    `K = mean(K_s)`. Then `mg = (V' - B') x K` and every basis is computed exactly as the
    UspFactor path from `mg` (PercentAsIs = mg / W x 100; dried/anhydrous from that;
    PercentLabelClaim = mg / W x AvgUnitWt / LabelClaim x 100; MgPerUnit = mg / W x AvgUnitWt).
    AvgUnitWt is therefore required for label claim / mg per unit in BOTH methods.
    Relative is Direct mode only (validation).
A2. Several standard titrations per series: request field `standards: [{ materialId, weightMg,
    titreMl }]` (1-3 items, all the same lot) REPLACES `standard`. Context DTO drops
    `stdTheoreticalWeightMg` / `sampleTheoreticalWeightMg`. Snapshot stores every standard
    entry, each K_s and K. Stock deduction = sum of standard weights.
A3. Standardization temperature comes from the record, never typed at titration time.
    Add nullable `TemperatureC decimal(5,2)` to `TitrantStandardization` (migration in T1) and
    an optional input on the standardization request/dialog. Request field
    `standardizationTemperatureC` is REMOVED; only `titrationTemperatureC` is entered.
    Context `TitrantPreparationOption` gains `standardizationTemperatureC: decimal|null`.
    When the test has temperature correction and the current standardization has no
    temperature, the option is `usable=false` with blockReason "Restandardize this titrant
    and record the temperature - temperature correction needs it".
A4. Residual mode always requires the excess Solution master + volume in the test master and
    always requires `excessPreparationId` in the request (traceability), blank or not.
A5. Residual: excess and back titrant masters must have the same `StrengthUnit` (validated in
    the test master; KF not allowed in residual).
A6. Snapshot also stores the K inputs/outputs and the excess volume used.
