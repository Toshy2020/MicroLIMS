# Phase 3 — Water chemistry results per sampling point

Spec §2 (limits per point / part). User ruling 2026-11-07: reuse micro's per-location model
("the micro lab already does this for water, EM and AC").

## Model (reuse, no new result tables)
Micro stores each location's result **on `SampleLocation`** (`CalculatedResult`, `ReportedResult`,
`Status`, `SpecLimit` snapshot, `Unit`, `EnteredAt`, `EnteredByUserId`). From there it already flows
into the shared review / approval (`WorkflowStateMachine.TransitionAsync(.., Ready, ..)` +
`SampleReviewService.AutoSubmitForReviewIfReadyAsync`), the sample summary / CoA
(`SampleSummaryTestOrderBuilder.BuildLocations`) and trends (`ResultProjectionService.UpsertFromSampleLocationAsync`).
Chemistry on a water point (pH, conductivity, TOC …) writes the same fields. One test order
per test across the batch, as today; one result per point, checked against **that point's** limits.
The physchem `ParameterResult` path is not used (it needs an Item `Specification` row).

## Backend (job A)

### A1. Limit columns
Add to `SamplingConfiguration` **and** `MachinePartConfiguration` (Phase 4 uses the part side):
`LimitType? LimitType`, `decimal? LowerLimit`, `decimal? UpperLimit`, `bool LowerInclusive = true`,
`bool UpperInclusive = true`, `string? ExpectedResultText`. Null `LimitType` = today's micro rows
(alert / action / spec text), unchanged.
Migration `LocationChemistryLimits` (`dotnet ef migrations add ... --configuration Release`), no data change.
Request / response DTOs and master-data create/update for both configs carry the new fields.
Validation (one static helper, used by both services): Range needs lower and/or upper;
NotMoreThan needs upper; NotLessThan needs lower; Qualitative needs ExpectedResultText; lower ≤ upper.
Only `Range`, `NotMoreThan`, `NotLessThan`, `Qualitative` are accepted here.

### A2. Prepare links every configured test
`WaterWorkflowEngine` prepare: set `location.SamplingConfigurationId` whenever a config exists
for (point, code), not only for count tests.

### A3. `LocationResultRecorder`
New `Application/Workflows/TestWorkflow/LocationResultRecorder.cs` (follow `IncubationStepRecorder.RecordBatchResultsAsync`
for the tail: projection upsert, `TransitionAsync(.., WorkflowStep.Ready, ..)`, `AutoSubmitForReviewIfReadyAsync`, a summary `Result` row, `UnitOfWork.RunAsync`).
`RecordLocationResultsAsync(int testOrderId, List<LocationResultInput> inputs, int userId)`,
`record LocationResultInput(int SampleLocationId, decimal? Value, string? Text)`.
- `EnsureTestOrderAccessAsync`. Order must be open (not Ready / closed). Every location of the order
  must be in `inputs` exactly once.
- Limit source per location: its `SamplingConfiguration` (water) or `MachinePartConfiguration` (AC), and it must have
  a non-null `LimitType`; otherwise refuse: "Limits for {test} at {point} are not configured."
- Numeric types: `Value` required; build an in-memory `Specification { LimitType, LowerLimit, UpperLimit,
  LowerInclusive, UpperInclusive }` and call `SpecificationEvaluator.Evaluate(spec, value)`.
  `SpecLimit` snapshot = `SpecificationService.BuildCanonicalSpecLimit(spec)`.
- Qualitative: `Text` required; WithinLimits when it equals `ExpectedResultText` (trimmed, case-insensitive),
  else OOS (use the same ResultStatus the existing qualitative evaluation uses). `SpecLimit` = expected text.
- Write `CalculatedResult` (numeric) / `ReportedResult` (display text), `Status`, `SpecLimit`, `Unit` from the config,
  `EnteredAt`, `EnteredByUserId`.
- Endpoint `POST api/test-workflow/{testOrderId}/location-results` on `TestWorkflowController`
  (same permission attribute as `batch-results`).

### A4. Current step tells the UI
`CurrentStepViewService` / `CurrentStepResponse`: when the order has locations whose config has a non-null
`LimitType`, add `locationEntry: true` and `locations: [{ sampleLocationId, name, limitType, lowerLimit,
upperLimit, expectedResultText, unit, specLimit, value?, text? }]` (name = water point code + location,
or machine – part). Otherwise unchanged.

### A5. Tests
Recorder: Range pass / fail, NMT, NLT, qualitative pass / fail, missing input refused, unconfigured limit refused,
order moves to Ready and the sample to review. Prepare links configs for non-count tests.
Config validation cases. Full suite (Postgres run is done by the controller).

## Frontend (job B)
- Water limits editor (`water/components/WaterLocationPanel.tsx`, `LimitFields` or a sibling): for an assigned test
  that is **not** a count test, show Limit type (Range / NMT / NLT / Qualitative), Lower, Upper, Expected text, Unit
  instead of alert / action / spec. Save through the existing sampling-configuration calls with the new fields.
  Count tests keep today's editor. Read-only rules from Phase 1 still apply.
- `LimitPills` (`WaterLocationList.tsx`) shows the chemistry limit (e.g. "6.5 – 8.5", "NMT 1.3 µS/cm", "Complies").
- `TestWorkflowDialog.tsx`: when `current.locationEntry`, render a new `LocationResultsPanel` instead of the
  workflow-type panel: one row per location (name, limit, unit, a number input or a text input for Qualitative),
  live pass/fail hint is optional, submit → `POST test-workflow/{id}/location-results`.
- Types for the new fields; component tests for the panel and the limit editor.

## Verification (stop point)
Postgres suite on the worktree, `tsc`, `vitest`, `build`; back up LIMSV2, apply `LocationChemistryLimits`;
browser: configure pH (Range) on a point for an RM test, receive water in RM & PM, prepare, enter results, see review.
