# Universal Specifications - Build Spec

Source: `E:\files\files\change-request-universal-specifications.md` + `Universal Specifications Screen.pdf` (mockup).
Branch: local `feat/fp-hplc-foundation`. LOCAL ONLY - never push.

## Recon (2026-09-19, verified against code + LIMSV2)

- Limits live on `Specification` (Domain/Entities/Specification.cs), keyed by `ItemId` + `TestCode`
  (not on the Item<->Test link `SampleTest`). String columns AlertLimit/ActionLimit/SpecLimit/Unit,
  `decimal? DilutionFactor`. No unique index; no duplicate (ItemId, TestCode) rows today.
- LIMSV2: 29 rows - 14 CountTest (e.g. 100/1000), 15 Observation with SpecLimit "Absent" (one "Abent").
  No Finished Product specs yet.
- CR §1 "ranges are ignored" is stale: `SpecLimitParser` already parses `a-b`, `NLT x`, `NMT x`.
- No TPH/discriminator anywhere in the codebase -> nullable typed columns on the existing table.
- Readers of a spec: `Specifications.FirstOrDefault(ItemId, TestCode)` in CurrentStepViewService:58,
  PathogenSessionService:559, ResultProjectionService:132, TestWorkflowEngine:1711/1800/1992;
  SampleSummaryService.FormatSpecificationText; MasterDataController specifications CRUD (~line 509).

## Decisions (user, 2026-09-19)

1. **Extend `Specification` in place** - one row per parameter. No new parameter table.
2. **CountTiered reuses the existing AlertLimit/ActionLimit/SpecLimit/DilutionFactor columns** - nothing
   copied, micro logic untouched.
3. Existing "Absent" pathogen rows -> `PresenceAbsence`, ExpectedState Absence. The "Abent" row is
   corrected to "Absent" and listed in the migration report.
4. Limit Type lives per item-assignment (on Specification), never on Test Master.

## Data model

`LimitType` enum (Domain/Enums, append-only order): Range, NotMoreThan, NotLessThan,
TargetWithTolerance, CountTiered, Qualitative, PresenceAbsence, MultiStage.
`ToleranceMode` enum: Absolute, Percent. `ExpectedPresence` enum: Presence, Absence.

New columns on `Specification` (all nullable unless stated):
- `ParameterName` string(150) NOT NULL - backfilled from TestDefinition.DisplayName, else TestCode.
- `DisplayOrder` int NOT NULL default 0.
- `LimitType` int NOT NULL (backfilled; see below).
- `ReferenceStandard` string(100).
- `LowerLimit`, `UpperLimit` decimal(18,6); `LowerInclusive`, `UpperInclusive` bool NOT NULL default true.
- `Target`, `Tolerance` decimal(18,6); `ToleranceMode` int.
- `ExpectedResultText` string(1000) (Qualitative).
- `ExpectedState` int, `SampleQuantity` decimal(18,6), `SampleQuantityUnit` string(20) (PresenceAbsence).
- Child `SpecificationStage` (Id, SpecificationId FK cascade, StageNumber int, StageLabel string(100),
  AcceptanceCriteriaText string(1000)) for MultiStage.
- Unique index (ItemId, TestCode, ParameterName).

Backfill (migration SQL):
- Tests whose TestDefinition.WorkflowType = CountTest -> CountTiered.
- SpecLimit ILIKE 'Absent' or 'Abent' -> PresenceAbsence, ExpectedState = Absence, and 'Abent' -> 'Absent'.
- Everything else -> CountTiered (legacy behaviour; flagged in report). Report = docs file listing
  rows touched/flagged, produced from a SQL query after migration.

## Canonical SpecLimit text (keeps every existing reader working)

For non-CountTiered types the server writes `SpecLimit` from the structured values; Alert/Action are
emptied. Readers that snapshot SpecLimit text (results, CoA, summary) keep working unchanged.
- Range: `"{Lower}-{Upper}"` (parser already understands).
- NotMoreThan: `"NMT {Upper}"`; NotLessThan: `"NLT {Lower}"`.
- TargetWithTolerance: `"{Target} ± {Tolerance}"` (+ `"%"` suffix after tolerance when Percent mode) -
  **extend SpecLimitParser** to parse `a ± b` / `a +/- b` into Min=a-b, Max=a+b (Percent: a*b/100).
- Qualitative: ExpectedResultText. PresenceAbsence: "Absent"/"Present". MultiStage: "" (stages carry text).
- Numbers written with InvariantCulture, no trailing-zero trimming beyond what the user typed (store as entered).

## Evaluation

`SpecificationEvaluator` (Application/Services): `Evaluate(Specification spec, decimal value)` ->
"WithinLimits" / "OutOfSpecification" for Range (inclusive flags respected), NMT, NLT, Target±Tol;
CountTiered -> existing `SpecLimitParser.CompareAgainstLimits(value, Alert, Action, Spec)`;
Qualitative / PresenceAbsence / MultiStage -> throw InvalidOperationException (not numeric).
`SpecificationLookup.PrimaryAsync(db, itemId, testCode)` = lowest DisplayOrder then Id; every
FirstOrDefault(ItemId, TestCode) reader switches to it. HPLC assay (TestWorkflowEngine ~1992) evaluates
with the evaluator instead of the parser.

## API (MasterDataController specifications endpoints, SectionHead/Admin as today)

Create/Update requests gain: ParameterName, DisplayOrder, LimitType, ReferenceStandard, LowerLimit,
UpperLimit, LowerInclusive, UpperInclusive, Target, Tolerance, ToleranceMode, ExpectedResultText,
ExpectedState, SampleQuantity, SampleQuantityUnit, Stages[]. Validation in a
`SpecificationService.Validate` (Application layer): required fields per type, Lower <= Upper,
Tolerance > 0, DilutionFactor only allowed for CountTiered, TestCode must be assigned to the item,
duplicate ParameterName per (item, test) refused. Existing CountTiered payloads (no LimitType) keep
working: missing LimitType = CountTiered. GET returns stages.

## UI (Item workspace -> Specifications tab, `ItemSpecificationsSection.tsx`)

Per mockup page 1/2: flat table - Assigned Test / Parameter | Limit Type chip | Limit | Unit |
Reference Standard | Actions. Tests with >1 parameter grouped under the test name with "↳" rows and
"+ Add parameter to <Test>". MultiStage rows show stages always expanded. Add/Edit dialog: Assigned Test,
Parameter Name (defaults to test name), Limit Type select, dynamic limit-definition box, Unit,
Reference Standard, Dilution Factor (enabled only for Count-Tiered). No client-side pass/fail logic.

## Out of scope

MultiStage auto-evaluation, Unit master list, Test Master changes, dropping legacy columns,
result-entry workflows for pH/LOD/appearance/related substances (no such workflows exist yet).

## Slices

S1 backend (agy): enums, entity, config, migration + backfill, parser ±, evaluator, lookup, service
validation, endpoints, tests. S2 frontend (agy): Specifications tab + dialog. S3 (Claude): wire HPLC
evaluation, summary/CoA formatting, migration report, full-suite + Postgres verification.
