# MicroLIMS Pathogen Workflow — Backend Refactor Report

Branch: `pathogen-workflow-refactor`. Report originally generated 2026-08-11 against commit `e5bf857`; **revised 2026-08-11 against commit `e06f0fe`** (current HEAD) after a final whole-branch code review found six blocking correctness/data-integrity findings, all fixed, reviewed, and verified across six commits (`7e067c8`, `05edffb`, `319ae2f`, `e1090f4`, `79a2053`, `e06f0fe` — `git log --oneline e5bf857..HEAD`), including two rounds of scoped re-review, one of which itself surfaced and closed one more Important finding. See Section 6, item 15 for the full list; this revision touches only Sections 1, 4, 6, 7, and the verification sweep. All claims below are grounded in the code as it stands in this worktree; where the plan or spec differed from what was actually built, the deviation is called out explicitly rather than papered over.

```
=== MicroLIMS Pathogen Workflow - Backend Refactor Report ===

1. MIGRATION STATUS
```

- **Migration name:** `AddPathogenWorkflowRefactor` (file `backend/MicroLIMS.Persistence/Migrations/20260810225125_AddPathogenWorkflowRefactor.cs`)
- **Tables created:** `TestWorkflowStepMedias`, `WorkflowStepResults`, `ConfirmatoryMediaSelections`, `ConfirmatoryPlateObservations`
- **Columns added:** `TestWorkflowSteps.StepType`, `TestWorkflowSteps.TargetOrganismId`; `Incubations.IncubationStartUtc`, `Incubations.IncubationEndUtc`; `PathogenObservations.Observation`
- **Columns removed:** `TestWorkflowSteps.StepResultType`, `TestWorkflowSteps.IsDualPlate`, `TestWorkflowSteps.Plate1DefaultLabel`, `TestWorkflowSteps.Plate2DefaultLabel`; `Incubations.Plate2MediaId`; `PathogenObservations.GrowthObserved`, `PathogenObservations.PlateLabel`
- **Enum changes:** `WorkflowType.DualPlate` (ordinal 2) removed — the enum now stops at `Observation` (1). `StepResultType` (3 values: `PlateCount`/`Growth`/`DualGrowth`) is dropped entirely and replaced by a new `StepType` (6 values: `PlateCount`, `BrothEnrichment`, `SelectiveBroth`, `SelectivePlating`, `ConfirmatoryPlating`, `BiochemicalTest`). New enums `GrowthObservation` (`NoGrowth`/`GrowthNonConforming`/`GrowthConforming`), `ConfirmatoryResult` (`AllConforming`/`Inconclusive`), and `AnalystDecision` (`SubmitAsDetected`/`ProceedToBiochemical`) were added. All enums persist as integers.
- **Data remap performed in `Up()`:**
  - `TestDefinitions.WorkflowType = 2 (DualPlate)` → `1 (Observation)`.
  - `TestWorkflowSteps.StepResultType` → `StepType`, by raw SQL: `PlateCount` (0) unchanged via the new column's default; `DualGrowth` (2) → `ConfirmatoryPlating` (4); `Growth` (1) is split three ways using context the old enum didn't carry — `IsFinalStep = TRUE` → `SelectivePlating` (3); `IsFinalStep = FALSE` and the step's `MediaType.Class = SelectiveBroth (3)` → `SelectiveBroth` (2); otherwise → `BrothEnrichment` (1).
  - `PathogenObservations.GrowthObserved` (bool) → `Observation` (enum): `true` → `GrowthConforming` (2), `false` → `NoGrowth` (0). `GrowthNonConforming` (1) has no historical rows — the old boolean could not express "growth of something other than the target organism."
- **`Down()` is deliberately lossy** and documents each loss inline in the migration: `BrothEnrichment`/`SelectiveBroth`/`SelectivePlating` all collapse back onto `Growth` (1); `BiochemicalTest` has no pre-refactor equivalent and also folds to `Growth` (1) (see Section 6, item 7 for the verification-coverage caveat on this and the `DualGrowth` branch); `WorkflowType.DualPlate` is **not** restored (left as `Observation` — the migration's own comment explains that guessing which rows used to be `DualPlate` would produce "a plausible-looking but unverifiable value in a GMP record"); the `Plate1DefaultLabel`/`Plate2DefaultLabel`/`PlateLabel`/`Plate2MediaId` columns are restored empty, with no data.
- **Separate fix on this branch, not part of this migration:** commit `e5bf857` restored a `CreateTable` for `TestWorkflowSteps` (plus `TestDefinitions.WorkflowType` and `CountTestReadings.StepName`) into the earlier migration `20260806083337_TestWorkflowTemplates`, where it had been lost in a prior squash. See Section 6, item 8.
- **Second migration, added by the final-review fix wave (Section 6, item 15):** `20260811164833_AddIncubationReceivedAtAndAnalystDecision` (file `backend/MicroLIMS.Persistence/Migrations/20260811164833_AddIncubationReceivedAtAndAnalystDecision.cs`), landed in commit `7e067c8`. Four nullable columns added, no data migration and a clean `Down()` that drops all four:
  - `Incubations.WindowReceivedAtUtc` (`timestamp with time zone`) — a server clock reading taken when the analyst-declared incubation window is received, independent of the analyst-supplied `IncubationStartUtc`/`IncubationEndUtc`, so a reviewer can see what was claimed versus when it was actually submitted.
  - `WorkflowStepResults.AnalystDecision` (`integer`, nullable enum), `AnalystDecisionAtUtc` (`timestamp with time zone`), `AnalystDecisionByUserId` (`integer`) — records the post-confirmatory analyst decision (`SubmitAsDetected` vs `ProceedToBiochemical`) for **both** branches; previously the `ProceedToBiochemical` branch left no trace at all.

```
2. ENDPOINTS IMPLEMENTED
```

**`TestWorkflowController`, route prefix `api/test-workflow`** (`[Authorize(Roles = Analyst,Reviewer,SectionHead,SystemAdministrator)]` at the controller level unless noted otherwise):

| Method | Path | Notes |
|---|---|---|
| GET | `/{testOrderId}/current-step` | Full workflow state for the dialog: current template step, open-incubation lock, sample header context, completed-step list, full step outline, final result. |
| GET | `/{testOrderId}/eligible-incubators/{stepMediaId}` | Server-filtered incubator list for one step medium's temperature window. |
| GET | `/{testOrderId}/permitted-confirmatory-media?stepName=` | Permitted media panel for a confirmatory step, each with its expected appearance and available released lots. |
| POST | `/{testOrderId}/select-media` | **CountTest-only** legacy path (see Section 6, item 6). |
| POST | `/{testOrderId}/record-result` | **CountTest-only** legacy path; rejects any non-`PlateCount` step with a message pointing at the dedicated endpoints. |
| GET | `/{testOrderId}/locations` | EM / After Cleaning per-location result grid. |
| POST | `/{testOrderId}/batch-results` | EM / After Cleaning CFU batch entry. |
| POST | `/{testOrderId}/close-incubation-window` | Closes the currently open (non-final) EM/After Cleaning incubation window. |
| POST | `/{testOrderId}/batch-pathogen-results` | EM / After Cleaning batch growth observations (final step). |
| POST | `/{testOrderId}/submit-broth` | Broth enrichment / selective broth step submission. |
| POST | `/{testOrderId}/submit-selective-plating` | Selective plating single-observation submission. |
| POST | `/{testOrderId}/submit-confirmatory-setup` | Analyst-selected media/lot/incubator panel for confirmatory plating. |
| POST | `/{testOrderId}/submit-confirmatory-observations` | One observation per selected confirmatory medium. |
| POST | `/{testOrderId}/analyst-decision` | `SubmitAsDetected` or `ProceedToBiochemical`, offered only after an all-conforming confirmatory result. |
| POST | `/{testOrderId}/submit-biochemical` | Free-text biochemical result + optional attachment id. |
| POST | `results/{workflowStepResultId}/biochemical-decision` | `[Authorize(Roles = Reviewer,SectionHead,SystemAdministrator)]`. Reviewer approve / return-for-biochemical decision. |

**`MasterDataController`, route prefix `api/masterdata`** (Test Master workflow-step CRUD; base `[Authorize]`, writes further restricted to `[Authorize(Roles = SectionHead,SystemAdministrator)]`):

| Method | Path | Notes |
|---|---|---|
| PUT | `test-definitions/{id}/workflow-type` | Sets `TestDefinition.WorkflowType` (`CountTest`/`Observation`). Write-restricted. |
| GET | `test-definitions/{id}/steps` | Lists a test's configured steps with step media, no write-role restriction. |
| POST | `test-definitions/{id}/steps` | Creates a step; runs `WorkflowTemplateValidator` + final-step-uniqueness + contiguous-step-order checks. Write-restricted. |
| PUT | `test-definitions/steps/{stepId}` | Updates a step; `StepMedia` is replaced wholesale, not merged. Write-restricted. |
| PUT | `test-definitions/steps/{stepId}/move` | Swaps `StepOrder` with an adjacent step, via a two-phase `SaveChanges` (a single-batch swap trips EF's circular-dependency detection against the unique `(TestDefinitionId, StepOrder)` index). Write-restricted. |
| DELETE | `test-definitions/steps/{stepId}` | Blocked if any `Incubation` already references the step by name; closes the `StepOrder` gap on delete. Write-restricted. |

```
3. FINAL REQUEST/RESPONSE SHAPES
```

Taken verbatim from `backend/MicroLIMS.API/Controllers/TestWorkflowController.cs` and `MasterDataController.cs` as they stand — not from the spec or the plan.

**Request records (`TestWorkflowController.cs`):**

```csharp
public record SubmitBrothRequest(string StepName, int MediaLotId, int EquipmentId,
    DateTime IncubationStartUtc, DateTime IncubationEndUtc, string? Observation);

public record SubmitSelectivePlatingRequest(string StepName, int MediaLotId, int EquipmentId,
    DateTime IncubationStartUtc, DateTime IncubationEndUtc, GrowthObservation Observation);

public record ConfirmatorySelectionRequest(int StepMediaId, int MediaLotId, int EquipmentId);
public record SubmitConfirmatorySetupRequest(string StepName, List<ConfirmatorySelectionRequest> Selections,
    DateTime IncubationStartUtc, DateTime IncubationEndUtc);

public record ConfirmatoryObservationRequest(int MaterialId, GrowthObservation Observation);
public record SubmitConfirmatoryObservationsRequest(string StepName, List<ConfirmatoryObservationRequest> Observations);

public record AnalystDecisionRequest(AnalystDecision Decision);
public record SubmitBiochemicalRequest(string StepName, string BiochemicalResultText, int? AttachmentId);
public record BiochemicalReviewRequest(bool Approve, string Comment);

// Legacy CountTest-only requests, dual-plate fields stripped:
public record SelectMediaRequest(string StepName, int MediaLotId, int IncubatorId);
public record RecordTestResultRequest(string StepName, List<decimal>? PlateReadings, decimal? DilutionFactor);
```

**Every workflow-action request is shaped `{testOrderId}` in the URL + `StepName` in the body** — there is no separate `stepInstanceId`/step-scoped resource. See Section 6, item 1 for why.

**`GET /{testOrderId}/current-step` response** (anonymous object, shape as actually returned):

```json
{
  "testOrderId": 123,
  "step": {
    "id": 5, "stepOrder": 3, "stepName": "Selective Plating", "mediaTypeId": 7,
    "stepType": "SelectivePlating",
    "targetOrganismId": 2,
    "mediaType": { "id": 7, "class": "Selective" },
    "incubationMinHours": 24, "incubationMaxHours": 48,
    "temperatureMin": 35.0, "temperatureMax": 37.0,
    "isFinalStep": false
  },
  "stepNumber": 3,
  "totalSteps": 5,
  "testName": "SALM",
  "workflowType": "Observation",
  "sampleContext": {
    "sampleName": "...", "batchNumber": "...", "controlNumber": "...",
    "reason": "...", "systemReferenceNumber": "...", "sampleType": "FinishedProduct",
    "stage": "..."
  },
  "incubationLock": {
    "isLocked": true, "incubationEndUtc": "2026-08-12T10:00:00Z", "remainingSeconds": 3600
  },
  "previousSteps": [ { "stepNumber": 1, "stepName": "...", "status": "Complete", "mediaName": "...", "lotNumber": "...", "incubatorName": "...", "incubatorSetTemp": 35.0, "incubationStartUtc": "...", "incubationEndUtc": "...", "observation": "..." } ],
  "allStepsComplete": false,
  "finalResult": null,
  "allSteps": [ { "stepOrder": 1, "stepName": "..." } ],
  "completedSteps": [ { "stepOrder": 1, "stepName": "...", "stepType": "BrothEnrichment", "isFinalStep": false, "outcome": "...", "observedAt": "...", "reportedResult": null, "calculatedResult": null, "status": null } ]
}
```

Deviations from a naive reading of the spec, all intentional:

- **`sampleContext.stage`** is included only when `Sample.Category == FinishedProduct` — it is a Finished-Product-only concept (`Sample.ProductionStage`), and the key is omitted entirely (not sent as `null`) for every other category.
- **`sampleContext.sampleName`** has no single backing column; it is resolved with the same precedence `SampleSummaryService` uses: `Item.Name ?? WaterSamplingPoint.Code ?? Department.Name ?? Machine.Name ?? ""`.
- **`incubationLock`** is `null` unless there is an open incubation with a non-null `IncubationEndUtc`; `isLocked` is the negation of `Incubation.IsIncubationComplete`, and `remainingSeconds` is clamped to `>= 0`.

**Every `StepResultDto`** (the common return shape from `submit-broth`, `submit-selective-plating`, `submit-confirmatory-setup`, `analyst-decision`, `submit-biochemical`, `biochemical-decision`):

```csharp
public record StepResultDto(
    int StepInstanceId, string StepType, string Status,
    int SubmittedByUserId, DateTime SubmittedAtUtc,
    bool NextStepUnlocked, string? WorkflowFinalResult, List<string> Flags);
```

`StepInstanceId` is the underlying `Incubation.Id`, not a dedicated step-instance identifier — there is no separate step-instance table; every pathogen step's run is anchored to an `Incubation` row (even `BiochemicalTest`, which has no incubation window of its own and reuses the confirmatory step's `IncubationId`, per code comment in `SubmitBiochemicalAsync`). `StepType` is sent as the enum's string name, not its ordinal, since the frontend has no reason to know the C# ordinal. `Flags` carries free-form string markers (`"BiochemicalNotPerformed"`, `"ReturnedForBiochemical"`, `"InconclusiveResult"`) — there is no separate flags enum.

**`submit-confirmatory-observations` returns a distinct shape**, not `StepResultDto`:

```csharp
public record ConfirmatoryOutcomeDto(int StepInstanceId, string ConfirmatoryResult, bool AnalystDecisionRequired, List<string> Flags);
```

**Envelope:** every response is wrapped in `ApiResponse<object>` (`{ success, message, data, errors }`, from `backend/MicroLIMS.Shared/Responses/ApiResponse.cs`). There is no dedicated error-code field on `ApiResponse` — see Section 4.

**`CreateTestWorkflowStepRequest` / `UpdateTestWorkflowStepRequest`** (Test Master, `MasterDataController.cs`; identical shape for create and update):

```csharp
public record StepMediaRequest(int MaterialId, decimal TempMin, decimal TempMax, bool IsRequired, int DisplayOrder);
public record CreateTestWorkflowStepRequest(string StepName, int MediaTypeId, int IncubationMinHours, int IncubationMaxHours,
    decimal TemperatureMin, decimal TemperatureMax, bool IsFinalStep, StepType StepType, int? TargetOrganismId,
    List<StepMediaRequest> StepMedia);
```

`StepMediaRequest.MaterialId` is the spec's `MediaId` field under this codebase's terminology (see Section 6, item 3) — it points at `Material` (the catalogue medium), not at a released lot.

```
4. VALIDATION RULES IMPLEMENTED
```

**Template structural rules** (`WorkflowTemplateValidator.Validate`, run from `MasterDataController.ValidateStepRulesAsync` on step create/update — collects every violation rather than stopping at the first):

| # | Rule | Failure message |
|---|---|---|
| 1 | A broth step (`BrothEnrichment`/`SelectiveBroth`) must have exactly one assigned medium, marked required, and must not target an organism. | "A broth step must have exactly one assigned medium, marked as required." / "A broth step must not target an organism." |
| 2 | A `SelectivePlating` step must have exactly one required medium and must target an organism. | "A selective plating step must have exactly one assigned medium, marked as required." / "A selective plating step must target an organism." |
| 3 | A `ConfirmatoryPlating` step must have at least one analyst-selectable (not required) medium and must target an organism. | "A confirmatory plating step must have at least one permitted medium, all analyst-selectable." / "A confirmatory plating step must target an organism." |
| 4 | A `BiochemicalTest` step must have no assigned media and no target organism. | "A biochemical test step must have no assigned media." / "A biochemical test step must not target an organism." |
| 5 | Every medium's `TempMin` must be below its `TempMax`. | "Medium {MaterialId}: the minimum temperature must be below the maximum." |
| 6 | No medium may be assigned to the same step more than once. | "Medium {MaterialId} is assigned to this step more than once." |

Plus, outside `WorkflowTemplateValidator` but in the same `ValidateStepRulesAsync` call: only one step per test may be `IsFinalStep`, and `ValidateContiguousStepOrderAsync` enforces "no gaps, no duplicates" in `StepOrder` after every create/move/delete. All of these throw a plain `InvalidOperationException` with the violations joined by spaces (`"Rule {n} ({StepName}): {Message}"`) — see Section 6 for the discovered gap that this never surfaces the `TEMPLATE_VALIDATION_FAILED` code.

**Runtime engine rules** (`WorkflowErrorCodes`, `backend/MicroLIMS.Shared/Constants/WorkflowErrorCodes.cs`), each thrown as a `WorkflowStepException` and returned as `Errors[0]` on a 400:

| Error code | Trigger |
|---|---|
| `INCUBATION_NOT_COMPLETE` | Submission attempted before `IncubationEndUtc` has elapsed. Carries `remainingSeconds` as `Errors[1]`. Enforced in `SubmitBrothAsync` (against the request's own `incubationEndUtc`) and in `SubmitConfirmatoryObservationsAsync` (against the persisted `Incubation.IncubationEndUtc`). **Not enforced in `SubmitSelectivePlatingAsync`** — still true after the fix wave below; see the code comment at `TestWorkflowEngine.cs` ~line 988 (`RequiresIncubationLock`: selective plating is read off plates the *previous* step incubated, so it carries no elapsed-window lock of its own — but see Section 6, item 14 for why the property documenting this is itself dead code). |
| `INCUBATION_WINDOW_INVALID` | **Added by the final-review fix wave (commit `05edffb`).** `RequireValidIncubationWindow` (called from `SubmitBrothAsync`, `SubmitSelectivePlatingAsync`, `SubmitConfirmatorySetupAsync`) rejects a declared window where `incubationEndUtc < incubationStartUtc` — previously a `start > end` window passed `INCUBATION_NOT_COMPLETE`'s only check (has the declared end passed) without complaint. |
| `INCUBATION_WINDOW_TOO_SHORT` | **Added by the final-review fix wave (commit `05edffb`).** Same `RequireValidIncubationWindow` call, rejects a declared window shorter than the step template's `IncubationMinHours` — previously a 1-second window passed. Deliberately no upper-bound check: over-incubation is a documented deviation, not a falsification risk. |
| `CONFIRMATORY_ALREADY_RECORDED` | **Added by the final-review fix wave (commit `05edffb`).** `SubmitConfirmatorySetupAsync` rejects a second setup call for a step that already has a read-out `WorkflowStepResult` (`ConfirmatoryResult != null`) — previously a re-run silently minted a fresh row that every downstream reader preferred over the original, with no audit trail explaining the disappearance (this is how an `Inconclusive` result could end up reported as `Detected`). |
| `CONFIRMATORY_SETUP_ALREADY_SUBMITTED` | **Added by the final-review fix wave; the post-read-out case landed in commit `05edffb`, then a second scoped re-review found it didn't cover a second setup submitted *before* read-out — closed in commit `e06f0fe`.** `SubmitConfirmatorySetupAsync` now also rejects a second setup while the first is still awaiting its plate readings (`ConfirmatoryResult == null` but a `WorkflowStepResult` row exists). Kept as a distinct code from `CONFIRMATORY_ALREADY_RECORDED` so the frontend can tell "read out, done" apart from "incubating, go and read it." |
| `MEDIA_NOT_IN_PERMITTED_LIST` | A media lot's `MaterialId` doesn't match the step's permitted medium, in `LoadReleasedLotAsync`; also thrown directly in `SubmitConfirmatorySetupAsync` if the submitted `StepMediaId` isn't one of the step's configured media. |
| `NO_MEDIA_SELECTED` | `SubmitConfirmatorySetupAsync` called with zero selections. |
| `INCOMPLETE_CONFIRMATORY_SETUP` | `SubmitConfirmatoryObservationsAsync` called with no prior setup row, no recorded incubation window, a duplicate observation for the same material id (rejected in code as of the fix wave, commit `05edffb` — previously relied solely on a DB unique index the EF InMemory test provider doesn't enforce), or an observation set that doesn't exactly match the selected media's material IDs. |
| `INCUBATOR_TEMP_OUT_OF_RANGE` | Chosen incubator's `SetPointTemperature` falls outside the step medium's `[TempMin, TempMax]`, checked server-side in `RequireEligibleIncubatorAsync` regardless of what the client's incubator picker showed. |
| `BIOCHEMICAL_RESULT_REQUIRED` | `SubmitBiochemicalAsync` called with a blank/whitespace `BiochemicalResultText`. |
| `SEGREGATION_OF_DUTIES_VIOLATION` | `RecordBiochemicalReviewDecisionAsync` called by the same user who performed the test (`_sodGuard.DidUserPerformTestAsync`). **Broadened by the fix wave (commit `319ae2f`):** the guard now also checks `ConfirmatoryPlateObservation.RecordedByUserId`, joined through the parent `WorkflowStepResult` — confirmatory plate readings are appended to a result row someone else set up, so an analyst who only read the plates was previously invisible to this check and could review/approve their own reading. Note: this check runs *before* `RecordBiochemicalReviewDecisionAsync`'s result-type/eligibility check, so a caller passing a bogus or unrelated result id gets this error rather than the more specific one — cosmetic ordering only (Section 6, item 16). |
| `TEMPLATE_VALIDATION_FAILED` | **Declared but never thrown** — confirmed still true after the fix wave (it was out of that wave's scope); see Section 6. |

Non-coded validation (plain `InvalidOperationException`, 400 with message only, no `Errors[0]` code): step/test-order not found, wrong step type for the endpoint called, released-lot checks (not released, wrong media class), `record-result` called against a non-`PlateCount` step, missing incubation window, a returning reviewer's comment left blank, and the various Test Master structural failures above.

```
5. AUDIT EVENTS REGISTERED
```

No new event-type registry was created — per the plan's explicit constraint, this reuses the two existing mechanisms:

- **`AuditLog` (automatic, via `MicroLimsDbContext.CaptureAuditEntries`, called from both `SaveChanges` and `SaveChangesAsync`):** every insert/update/delete on every tracked entity is captured with User, UTC timestamp, entity name, entity id, action (`Create`/`Update`/`Delete`), and previous/new value JSON. This applies for free to every new entity introduced by this refactor — `TestWorkflowStepMedia`, `WorkflowStepResult`, `ConfirmatoryMediaSelection`, `ConfirmatoryPlateObservation`, `PathogenObservation`, `Incubation`, and `TestWorkflowStep` — with no bespoke wiring needed.
- **`WorkflowHistory` (per-`TestOrder`, via `WorkflowStateMachine.TransitionAsync`, which every workflow engine is required to call instead of writing `TestOrder.CurrentStep` directly):**
  - Every `FinalizeWorkflowAsync` call (`NotDetected` off non-conforming selective plating; `Detected` off `SubmitAsDetected`; `Detected` off a completed biochemical test) transitions the order to `WorkflowStep.Ready` with note `"Workflow complete: {finalResult}"`.
  - `SubmitConfirmatoryObservationsAsync`, when the result is not all-conforming, adds a standalone `WorkflowHistory` row (`Incubating` → `Incubating`) noting `"Confirmatory plating inconclusive - flagged for investigation."` — this is a direct `_db.WorkflowHistories.Add`, not a `TransitionAsync` call, since the order doesn't actually change step.
  - `RecordBiochemicalReviewDecisionAsync`, on a reviewer return, transitions the order back to `WorkflowStep.Incubating` with note `"Returned for biochemical confirmation: {comment}"`.
- **`ReviewWorkflowEvent` (per gated record, via `ReviewGateService.LogEventAsync`, shared with Sample/Media/Cryovial gates):** `RecordBiochemicalReviewDecisionAsync` logs `ReviewWorkflowEventType.ReviewCompleted` against the sample, with `ApprovalDecision.Approve` on approval or `ApprovalDecision.Investigation` on a return — there is no dedicated pathogen review-event type; the biochemical send-back rides the same `Sample`-scoped review timeline used elsewhere.
- **Notification:** on a reviewer return, `RecordBiochemicalReviewDecisionAsync` also sends the assigned analyst an in-app notification (`INotificationService.NotifyAsync`) — not an audit record per se, but the only user-facing signal that a result was sent back.

The spec's nine `PathoWorkflow.*` event names are not implemented as a parallel event-type enum — deliberately, per the plan's constraint against creating a second audit mechanism. The mapping above is the closest equivalent grounded in the actual code; no literal `PathoWorkflow.*` identifiers exist anywhere in this codebase.

```
6. KNOWN GAPS OR DEVIATIONS
```

1. **Routing.** Endpoints are `api/test-workflow/{testOrderId}/...`, not `/api/workflow-steps/{stepInstanceId}/...`. This codebase addresses workflow actions by test order with the step named in the request body; a second addressing scheme would have been a new pattern alongside the existing one.
2. **`WorkflowType` scope.** The spec's Part 1.1 list of five values (`BrothEnrichment`, `SelectiveBroth`, `SelectivePlating`, `ConfirmatoryPlating`, `BiochemicalTest`) taken literally would have deleted `CountTest` and `Observation` from `WorkflowType`, breaking every TAMC/TYMC and non-pathogen test. Resolved with the human partner: those five became a new **per-step** enum `StepType` (replacing the old `StepResultType`), while the per-test `WorkflowType` kept `CountTest` and `Observation` and only lost `DualPlate`.
3. **Media terminology.** `MediaId` in the spec maps to `MaterialId` here (the medium as a catalogue item); `MediaLotId` maps to `MediaId` (the released lot). `Media` *is* the released lot in this codebase; `Material` is the medium. `TestWorkflowStepMedia.MaterialId` therefore points at `Material`, not at `Media` as the spec's field name implied. There is no `MediaLot` entity.
4. **`MediaEvaluationCriteria` does not exist.** The expected-appearance snapshot reads `MediaChallengeSpec.ExpectedDescription`, matched by material *name* (not id) and organism id, because `MediaChallengeSpec` keys on `MaterialName`.
5. **No `Attachment` entity exists.** `WorkflowStepResult.BiochemicalAttachmentId` is an unmapped `int?` with no foreign key — a hook for a future attachments feature, not a working reference today.
6. **No `ReviewDecision` entity exists.** The biochemical send-back fields (`RequiresBiochemical`, `ReturnReason`, `ReturnedAtUtc`, `ReturnedByUserId`) were added directly to `WorkflowStepResult`, and the review event itself reuses the existing `ReviewGateService` / `ReviewWorkflowEvent` infrastructure shared by Sample/Media/Cryovial gates, rather than a new mechanism.
7. **`ApiResponse` has no error-code field.** Structured workflow errors travel as `Errors[0] = error code` (a `WorkflowErrorCodes` constant), with `Errors[1] = remainingSeconds` appended when the failure is an incubation lock (`RunAsync` in `TestWorkflowController`).
8. **CountTest endpoints retained.** The plan called for deleting `SelectMediaRequest`/`RecordTestResultRequest` and their routes; the human partner overrode this because they are CountTest's only HTTP entry points. They were kept, stripped of dual-plate fields (`Plate2MediaId`, `Plate1Label`, `Plate2Label`, `GrowthObserved`, `Plate1/2GrowthObserved`). A guard in `RecordResultAsync` now rejects the legacy `record-result` path for any non-`PlateCount` step type with a message pointing at the dedicated pathogen endpoints.
9. **Equipment has no `IsActive`.** Incubator eligibility (`IncubatorEligibilityService`) is `Type == Incubator`, a non-null `SetPointTemperature` within the step medium's range, and `CalibrationDueDate` not past due; `CalibrationStatus` in the response DTO is hardcoded to `"Current"` rather than derived from anything richer, since eligibility already filters out anything overdue.
10. **Migration verification gap.** The `DualPlate`/`DualGrowth` remap branches (`WorkflowType = 2 → 1`, `StepResultType = 2 → StepType 4`) had no matching rows in the verification dataset used when the migration was built, so those specific branches are confirmed by enum-ordinal inspection of the SQL rather than exercised against live data.
11. **Two pre-existing repo defects were found and fixed on this branch, unrelated to the refactor itself:**
    - (a) A `.gitignore` rule `storage/` matched `backend/MicroLIMS.Infrastructure/Storage/` on a case-insensitive filesystem, so `IFileStorageService.cs` and `LocalFileStorageService.cs` had never been committed and a clean clone could not build (fixed in commit `cd536e7`).
    - (b) No migration contained a `CreateTable` for `TestWorkflowSteps` (nor the `CountTestReadings.StepName` and `TestDefinitions.WorkflowType` columns) — lost when two earlier migrations were squashed — so a genuinely empty database could not be provisioned via `dotnet ef database update`. Fixed in commit `e5bf857`, verified end-to-end against a scratch PostgreSQL database per that commit's message.
12. **Existing DualPlate data needs manual attention.** The migration re-types `Growth` → `SelectivePlating` and `DualGrowth` → `ConfirmatoryPlating`, but no migrated step gets a `TargetOrganismId` or `StepMedia` rows out of the migration alone — those didn't exist as concepts before. Every pre-existing pathogen template must be re-saved through Test Master before it will pass `WorkflowTemplateValidator`. Salmonella's XLD+TSI panel in particular must be re-entered.
13. **Frontend is broken by design and has not been touched.** `TestWorkflowDialog.tsx`, `TestMasterPage.tsx`, `TestWorkflowService.ts`, and `masterDataOptions.ts` (plus `sampleSummaryTypes.ts`, `SampleSummaryDialog.tsx`, `SampleReportPage.tsx`, `PathogenLocationResultGridDialog.tsx`, `MediaEvaluationPage.tsx`, `mediaSummaryTypes.ts`, `MediaReportPage.tsx`) still reference `stepResultType`, `plate1DefaultLabel`, `growthObserved`, and the routes this branch removed. This is expected and is Prompt 2's scope, not an oversight in this one — see Section 7.
14. **Minor deferred items, discovered during this task's verification pass, not present in the original plan's list:**
    - The `SubmitConfirmatoryObservationsAsync` `Inconclusive`-branch `WorkflowHistory` note ("Confirmatory plating inconclusive...") has no test asserting it exists.
    - A few tests assert against the method's returned DTO rather than re-querying persisted state.
    - Multiple `WorkflowTemplateValidator` violations on one step are joined into a single run-together `InvalidOperationException` message (`"Rule 1 (...): ... Rule 5 (...): ..."`), not returned as a structured list.
    - `Down()` folds `BiochemicalTest` to the old `Growth` value because the old `StepResultType` NOT NULL enum column has no sentinel for "did not exist before."
    - `WorkflowErrorCodes.TemplateValidationFailed` (`"TEMPLATE_VALIDATION_FAILED"`) is declared in `backend/MicroLIMS.Shared/Constants/WorkflowErrorCodes.cs` but never thrown anywhere in the codebase — Test Master's structural-validation failures raise a plain `InvalidOperationException` instead of a `WorkflowStepException`, so this code never reaches a client's `Errors[0]` in practice. Not fixed (see task constraints); flagged here as a genuine, if minor, defect. **Re-verified after the final-review fix wave (Section 6, item 15): still true.** This was not in that wave's scope; `grep -rn "WorkflowErrorCodes.TemplateValidationFailed" backend` still finds only the declaration, no throw site.
    - `TestWorkflowStep.RequiresTargetOrganism` and `TestWorkflowStep.RequiresIncubationLock` are computed properties, explicitly `Ignore`d by EF (`TestWorkflowStepConfiguration.cs`), but are not referenced anywhere in `TestWorkflowEngine` or `WorkflowTemplateValidator` — the equivalent logic is duplicated inline instead (the `StepType` switch in `WorkflowTemplateValidator.Validate`, and per-method incubation-window/lock calls in the pathogen `Submit*` methods). The two properties are effectively unused documentation, not wired-in behavior. Confirmed by inspection, not a functional bug — `SubmitSelectivePlatingAsync` correctly does *not* call `RequireIncubationComplete`, consistent with what `RequiresIncubationLock` says (`SelectivePlating` reads plates the *previous* step already incubated) — but the property itself plays no role in enforcing that; the omission is achieved only by that method simply not calling the check. **Re-verified after the final-review fix wave: still true.** This was also out of that wave's scope; the wave added a new check (`RequireValidIncubationWindow`, thrown from three `Submit*` methods including `SubmitSelectivePlatingAsync`) but it validates the analyst-declared window's plausibility, not whether a lock applies — `RequiresIncubationLock` remains unreferenced everywhere except its own doc comment.
15. **Final whole-branch code review found six blocking correctness/data-integrity findings; all six were fixed, tested, and verified**, across commits `7e067c8`, `05edffb`, `319ae2f`, `e1090f4`, `79a2053`, `e06f0fe` (`git log --oneline e5bf857..HEAD`), including two rounds of scoped re-review — one of which itself found and closed one more Important finding (the second bullet below). The suite grew from 151 to 177 passing tests across this wave.
    - **B1 — a fabricated incubation window was accepted.** `RequireIncubationComplete` only asked "has the declared end passed?", which a 1-second window satisfies as readily as a real one, and a window with `start > end` satisfied it too. Fixed (`05edffb`, `7e067c8`): a new `RequireValidIncubationWindow` (called from `SubmitBrothAsync`, `SubmitSelectivePlatingAsync`, `SubmitConfirmatorySetupAsync`) rejects unless `start <= end` and the declared duration is at least the step's `IncubationMinHours` — deliberately no upper bound, since over-incubation is a documented lab deviation, not a falsification risk. A server-stamped `Incubation.WindowReceivedAtUtc` was added alongside the analyst-declared window so a reviewer can see what was claimed versus when it was actually submitted. New codes: `INCUBATION_WINDOW_INVALID`, `INCUBATION_WINDOW_TOO_SHORT`.
    - **B2 — a re-run of confirmatory setup could bury an `Inconclusive` result under a silent `Detected`.** `SubmitConfirmatorySetupAsync` had no guard against a result already existing for the step, so a second call minted a fresh `Incubation`/`WorkflowStepResult` that every downstream reader preferred, with no audit trail explaining the first run's disappearance. Fixed in two passes: commit `05edffb` rejected a second setup once the step had been read out (`CONFIRMATORY_ALREADY_RECORDED`); a second, scoped re-review then found the guard didn't cover a second setup submitted *before* read-out — nothing else caught that case, since `IsStepDoneAsync` deliberately treats an un-read-out setup as "not done" (so the analyst still sees the step as current) and `ConfirmatoryResult` is still null at that point — so commit `e06f0fe` extended the same check, in place, to reject that case too (`CONFIRMATORY_SETUP_ALREADY_SUBMITTED`).
    - **B3 — no chain-order enforcement for pathogen steps.** `LoadStepAsync` resolved a step by name with no check that earlier steps were complete, so a later step could be submitted first, or any step re-submitted on top of itself. Fixed (`05edffb`): `LoadOrderAndStepAsync` now applies the same first-incomplete-step guard the legacy `SelectMediaAsync` path already used, plus a new `RequireOrderNotFinalized` check rejecting any submission against a `TestOrder` already at `Ready`/`Reviewed`/`Approved`.
    - **B4 — reviewer send-back accepted any result row.** `RecordBiochemicalReviewDecisionAsync` had no check on which `WorkflowStepResult` it was deciding; returning, e.g., a selective-plating result moved the order to `Incubating` while `SubmitBiochemicalAsync` still refused it, permanently stranding the order with no path forward. Fixed (`05edffb`): restricted to a confirmatory-plating result actually submitted as Detected without biochemical confirmation (`StepType == ConfirmatoryPlating && SkippedBiochemical`).
    - **B5 — `GetCurrentStepAsync` never advanced for a pathogen chain.** `IsStepDoneAsync` decided completion for non-CountTest workflows purely from `PathogenObservations`, which only `SubmitSelectivePlatingAsync` ever writes — so the current-step view kept reporting the first step complete after the whole chain had already run. Fixed (`05edffb`): pathogen step completion is now read from `WorkflowStepResults`, with confirmatory plating specifically requiring `ConfirmatoryResult != null` (plates actually read out, not merely set up). CountTest and legacy Observation-step completion logic is untouched.
    - **B6 — a segregation-of-duties gap on confirmatory plate readings.** `SegregationOfDutiesGuard.DidUserPerformTestAsync` checked `Results`, `CountTestReadings`, `PathogenObservations`, and `WorkflowStepResult.SubmittedByUserId`, but confirmatory plate readings are appended to a result row someone else already set up — an analyst who only read the plates was invisible to the guard and could review and approve their own reading. Fixed (`319ae2f`): the guard now also checks `ConfirmatoryPlateObservation.RecordedByUserId`, joined through the parent `WorkflowStepResult` since the observation carries no `TestOrderId` of its own.
    - Plus non-blocking fixes landed in the same wave: `RecordAnalystDecisionAsync` is now single-shot with both branches leaving a contemporaneous `WorkflowHistory` record — previously the "proceed to biochemical" branch persisted nothing at all; `ResultProjectionService.UpsertFromPathogenResultAsync` no longer creates a duplicate `ResultRecord` on a re-run (commit `e1090f4`) — it is now keyed on `TestOrder`+`Round` instead of the concluding row's id, since which `WorkflowStepResult` concludes a chain can change after a reviewer send-back and the biochemical submission that answers it; duplicate confirmatory plate readings are now rejected in application code (`SubmitConfirmatoryObservationsAsync`) rather than relied on solely a DB unique index, since the EF InMemory test provider does not enforce it; and a migrated template missing its `TargetOrganismId` or its `StepMedia` now throws a clear `InvalidOperationException` naming the step and pointing at Test Master, instead of an unhandled null-dereference 500.
16. **Two genuine gaps remain after the fix wave, discovered during it and not present in the original plan's list:**
    - A submitted confirmatory media panel is now **permanently immutable** — `SubmitConfirmatorySetupAsync` rejects any second call for the step (item 15/B2 above), and there is no supported endpoint to correct a wrong media selection once submitted. This matches the existing, equally-immutable `SelectMediaAsync` behavior elsewhere in the codebase, so it is consistent with the rest of the system — but it is a real gap an analyst will eventually hit, and "changing a panel after submission needs a documented, reason-bearing edit path" (per the code comment on `SubmitConfirmatorySetupAsync`) is explicitly deferred as a separate feature.
    - After an `Inconclusive` confirmatory read-out, `GetCurrentStepAsync` now (correctly, per fix B5 above) reports the chain as having advanced past confirmatory plating — but the biochemical step can never actually be submitted for an `Inconclusive` result (`SubmitBiochemicalAsync` and `RecordAnalystDecisionAsync` both require `ConfirmatoryResult == AllConforming`). The order is left sitting at `Incubating`, with the current-step view pointing at a step that structurally can never be completed. This is by design — `Inconclusive` means "flagged for investigation, no path to `Detected`" — but the current-step view now advertises a next step that isn't reachable, which Prompt 2's UI needs to account for rather than following literally.
    - Any `ResultRecord` rows that were **already duplicated in the database before the B2/`e1090f4` fix landed** (by the old, broken `SourceId`-based keying) are not backfilled or deduplicated by that fix — only new projections from this point forward key correctly on `TestOrder`+`Round`.
    - `RecordBiochemicalReviewDecisionAsync` runs its segregation-of-duties check (`_sodGuard.DidUserPerformTestAsync`) before its result-type/eligibility check (`StepType == ConfirmatoryPlating` / `SkippedBiochemical`), so a caller passing a bogus or unrelated `workflowStepResultId` gets a `SEGREGATION_OF_DUTIES_VIOLATION` rather than the more specific "not a decidable result" message. Cosmetic ordering only, not a security issue — the SOD check is unconditionally correct regardless of which branch reports first.

```
7. READY FOR PROMPT 2
```

**Yes**, with the following caveats the frontend work depends on:

- The frontend has **not** been updated in this branch and **will be broken** against these API changes (removed `select-media`/`record-result` fields, new `Submit*` endpoints, `stepResultType` → `StepType`, `growthObserved` → `GrowthObservation`, `plate1DefaultLabel`/`plate2DefaultLabel` gone). This is expected — it is Prompt 2's entire scope — but it means the application is not currently usable end-to-end from the UI, only from the API directly, until that work lands.
- Every pre-existing pathogen `TestDefinition` needs to be re-saved through Test Master (once its UI exists) before it will pass `WorkflowTemplateValidator` — the migration cannot backfill `TargetOrganismId` or `StepMedia` because those concepts did not exist before this refactor (Section 6, item 12).
- `WorkflowErrorCodes.TemplateValidationFailed` is dead code (Section 6, item 14) — Prompt 2 should not build frontend error-code branching around it; template validation failures currently surface only as free text in `message`.
- `BiochemicalAttachmentId` has no backing entity or upload path (Section 6, item 5) — any frontend attachment UI for the biochemical step has nothing to call yet.
- Four new machine-readable error codes landed with the final-review fix wave (Section 6, item 15; Section 4): `INCUBATION_WINDOW_INVALID`, `INCUBATION_WINDOW_TOO_SHORT`, `CONFIRMATORY_ALREADY_RECORDED`, `CONFIRMATORY_SETUP_ALREADY_SUBMITTED`. Prompt 2 should branch on these the same way it does on the original set — in particular, the confirmatory-setup screen should distinguish "already read out" from "awaiting readings" using the latter two codes rather than showing a generic error.
- A submitted confirmatory media panel cannot be corrected once submitted, and after an `Inconclusive` confirmatory result the current-step view reports the chain as past confirmatory plating even though the biochemical step can never be completed for that order (Section 6, item 16) — Prompt 2's UI should treat an `Inconclusive` result as terminal for navigation purposes rather than literally following the advertised next step.

```
--- Verification sweep (real output, re-captured 2026-08-11 against commit `e06f0fe`, after the final-review fix wave) ---
```

**Build:**

```
$ dotnet build backend/MicroLIMS.sln --no-incremental
...
Build succeeded.
    0 Warning(s)
    0 Error(s)

Time Elapsed 00:00:06.49
```

**Tests:**

```
$ dotnet test backend/MicroLIMS.Tests/MicroLIMS.Tests.csproj
...
Passed!  - Failed:     0, Passed:   177, Skipped:     0, Total:   177, Duration: 8 s - MicroLIMS.Tests.dll (net8.0)
```

151 passed at the original `e5bf857` snapshot; the fix wave added 26 tests (`PathogenChainInvariantTests.cs`, `IncubationLockTests.cs`, `BiochemicalReviewTests.cs`, `SegregationOfDutiesTests.cs`, `ResultProjectionTests.cs`) covering the six blocking findings and the analyst-decision idempotency follow-up. Current: 177 passed / 0 failed.

**Frontend untouched:**

```
$ git diff --stat 9f76583..HEAD -- frontend/
(empty)
```

No file under `frontend/` was modified anywhere on this branch, from its base commit (`9f76583`) through HEAD (`e06f0fe`) — the fix wave, like the rest of this branch, is backend-only.

**`ExpectedAppearanceSnapshot` immutability (re-verified against the fix wave's changes to `TestWorkflowEngine.cs`, including the new duplicate-observation guard in `SubmitConfirmatoryObservationsAsync`):**

```
$ grep -rn "ExpectedAppearanceSnapshot" backend --include=*.cs
```

Every occurrence is one of: the property declaration (`WorkflowStepResult.cs`, `ConfirmatoryPlateObservation.cs`), an EF migration/model-snapshot column definition (now in both `20260810225125_AddPathogenWorkflowRefactor` and the newer `20260811164833_AddIncubationReceivedAtAndAnalystDecision`, plus `MicroLimsDbContextModelSnapshot.cs`), an object-initializer write at entity creation (`TestWorkflowEngine.cs` line 1017, inside `new WorkflowStepResult { ... }` in `SubmitSelectivePlatingAsync`, and line 1201, inside `new ConfirmatoryPlateObservation { ... }` in `SubmitConfirmatoryObservationsAsync` — both building a brand-new row, never touching an already-tracked/persisted entity), a read (`TestWorkflowController.cs`, `MediaAppearanceSnapshotService.cs`), or a test assertion (`ConfirmatoryPlatingTests.cs`, `MediaAppearanceSnapshotTests.cs`, `SelectivePlatingTests.cs`). No reassignment of an already-persisted entity's `ExpectedAppearanceSnapshot` exists anywhere in the codebase, including the code the fix wave touched. Constraint holds.
