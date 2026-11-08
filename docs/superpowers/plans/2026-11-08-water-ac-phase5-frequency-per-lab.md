# Phase 5 — Water frequency per lab and Monitoring Plan per lab

Spec decision 4 and §4. Water points only: machines have no frequency (after cleaning is event-driven,
machine occurrences stay manual). Rooms (EM) unchanged.

A "lab" is a lab section (`Sections`: MICRO, FP; RM chemistry lives in the FP section's RawPackaging area),
the same id as `Sample.LabSectionId` (Phase 2).

## Backend (job A)

### B1. Override table
New entity `WaterPointLabFrequency : IVersionedEntity, IHasFrequency`: `Id`, `Version`,
`WaterSamplingPointId` (FK cascade), `SectionId` (FK Restrict), the `IHasFrequency` columns,
unique (`WaterSamplingPointId`, `SectionId`). Nav `WaterSamplingPoint.LabFrequencies`.
`MonitoringOccurrence.LabSectionId int?` (FK Restrict, index).
Migration `WaterFrequencyPerLab`: backfill existing water occurrences (`TargetType = WaterSamplingPoint`)
`LabSectionId` = the MICRO section id (look it up by `Code = 'MICRO'` in SQL), like `SampleLabSection` did.

### B2. Master data
`WaterMasterDataService`: get / set / clear a point's override for one section
(`GET/PUT/DELETE api/masterdata/water/points/{id}/lab-frequencies/{sectionId}`, body = the frequency rule
fields the point uses today; validate with `FrequencyRules` exactly like the point's own frequency).
Permission: the caller must be able to configure that section (`GetConfigurableSectionIdsAsync` contains it).
Point response gains `labFrequencies: [{ sectionId, sectionCode, rule..., description }]`.
Signed/audited the same way the point's frequency edit is (follow the existing point update path).

### B3. Plan generation per point × lab
`MonitoringPlanService.WaterTargetsAsync`: per active point, group `AssignedTestCodes` by the test's
`TestDefinition.SectionId`; one `PlanTarget` per section with ≥1 code; rule = that section's override ??
the point's rule; `RuleSource` = "lab" or "own".
`PlanTarget`, `GeneratedOccurrence` gain `int? LabSectionId`; `Key` and both `KeyOf` become
(TargetType, TargetId, LabSectionId, PeriodKey, Sequence); `ToEntity` writes it. Rooms pass null.
Because the backfill sets MICRO on existing water occurrences, republishing creates no churn for micro.
Manual water occurrences (`CreateManualAsync`) take a `labSectionId` (required for water).

### B4. Linking
`MonitoringLinkService.GroupOccurrencesQuery` (+ any other water candidate query there): water occurrences
match only when `o.LabSectionId == sample.LabSectionId`.

### B5. Lists per lab
`MonitoringOccurrenceService` calendar / year summary / due / detail: water occurrences are limited to the
caller's readable sections (`GetReadableSectionIdsAsync`); non-water unchanged. Inject
`IUserSectionScopeService` (required ctor parameter, no userId==0 bypass). Responses carry
`labSectionId` + `labSectionCode`. Optional query filter `labSectionId`.

### B6. Tests
Override CRUD + permission (other lab refused); generation: point with MICRO + FP codes → two occurrence
streams, FP override changes only FP's dates; key/no churn for backfilled MICRO rows; link only to the
sample's lab; due list for an FP-only user shows no MICRO water occurrences.

## Frontend (job B)
- Water point panel (`laboratoryConfiguration/water/components/`): a "Frequency per lab" block listing each
  lab that has tests on the point: "Point default" or its own rule, with Edit / Reset to default
  (reuse the existing frequency editor component). Read-only for labs the user cannot configure.
- Monitoring Plan screens (calendar, due list, occurrence detail): show the lab chip on water occurrences;
  a lab filter; manual water occurrence dialog asks for the lab.
- Types + component tests.

## Verification (stop point)
Postgres suite on the worktree, tsc, vitest, build; back up LIMSV2, apply `WaterFrequencyPerLab`,
check the backfill count; regenerate a draft plan and confirm micro water occurrences show no adds/retires.
