# MicroLIMS Pathogen Workflow — Backend Refactor Report

Branch: `pathogen-workflow-refactor`. Report generated 2026-08-11 against commit `e5bf857` (HEAD at the time of writing, before this report's own commit). All claims below are grounded in the code as it stands in this worktree; where the plan or spec differed from what was actually built, the deviation is called out explicitly rather than papered over.

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
| `INCUBATION_NOT_COMPLETE` | Submission attempted before `IncubationEndUtc` has elapsed. Carries `remainingSeconds` as `Errors[1]`. Enforced in `SubmitBrothAsync` (against the request's own `incubationEndUtc`) and in `SubmitConfirmatoryObservationsAsync` (against the persisted `Incubation.IncubationEndUtc`). **Not enforced in `SubmitSelectivePlatingAsync`** — see below. |
| `MEDIA_NOT_IN_PERMITTED_LIST` | A media lot's `MaterialId` doesn't match the step's permitted medium, in `LoadReleasedLotAsync`; also thrown directly in `SubmitConfirmatorySetupAsync` if the submitted `StepMediaId` isn't one of the step's configured media. |
| `NO_MEDIA_SELECTED` | `SubmitConfirmatorySetupAsync` called with zero selections. |
| `INCOMPLETE_CONFIRMATORY_SETUP` | `SubmitConfirmatoryObservationsAsync` called with no prior setup row, no recorded incubation window, or an observation set that doesn't exactly match the selected media's material IDs. |
| `INCUBATOR_TEMP_OUT_OF_RANGE` | Chosen incubator's `SetPointTemperature` falls outside the step medium's `[TempMin, TempMax]`, checked server-side in `RequireEligibleIncubatorAsync` regardless of what the client's incubator picker showed. |
| `BIOCHEMICAL_RESULT_REQUIRED` | `SubmitBiochemicalAsync` called with a blank/whitespace `BiochemicalResultText`. |
| `SEGREGATION_OF_DUTIES_VIOLATION` | `RecordBiochemicalReviewDecisionAsync` called by the same user who performed the test (`_sodGuard.DidUserPerformTestAsync`). |
| `TEMPLATE_VALIDATION_FAILED` | **Declared but never thrown** — see Section 6. |

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
    - `WorkflowErrorCodes.TemplateValidationFailed` (`"TEMPLATE_VALIDATION_FAILED"`) is declared in `backend/MicroLIMS.Shared/Constants/WorkflowErrorCodes.cs` but never thrown anywhere in the codebase — Test Master's structural-validation failures raise a plain `InvalidOperationException` instead of a `WorkflowStepException`, so this code never reaches a client's `Errors[0]` in practice. Not fixed (see task constraints); flagged here as a genuine, if minor, defect.
    - `TestWorkflowStep.RequiresTargetOrganism` and `TestWorkflowStep.RequiresIncubationLock` are computed properties, explicitly `Ignore`d by EF (`TestWorkflowStepConfiguration.cs`), but are not referenced anywhere in `TestWorkflowEngine` or `WorkflowTemplateValidator` — the equivalent logic is duplicated inline instead (the `StepType` switch in `WorkflowTemplateValidator.Validate`, and per-method `RequireIncubationComplete` calls in `SubmitBrothAsync`/`SubmitConfirmatoryObservationsAsync`). The two properties are effectively unused documentation, not wired-in behavior. Confirmed by inspection, not a functional bug — `SubmitSelectivePlatingAsync` correctly does *not* call `RequireIncubationComplete`, consistent with what `RequiresIncubationLock` says (`SelectivePlating` reads plates the *previous* step already incubated) — but the property itself plays no role in enforcing that; the omission is achieved only by that method simply not calling the check.

```
7. READY FOR PROMPT 2
```

**Yes**, with the following caveats the frontend work depends on:

- The frontend has **not** been updated in this branch and **will be broken** against these API changes (removed `select-media`/`record-result` fields, new `Submit*` endpoints, `stepResultType` → `StepType`, `growthObserved` → `GrowthObservation`, `plate1DefaultLabel`/`plate2DefaultLabel` gone). This is expected — it is Prompt 2's entire scope — but it means the application is not currently usable end-to-end from the UI, only from the API directly, until that work lands.
- Every pre-existing pathogen `TestDefinition` needs to be re-saved through Test Master (once its UI exists) before it will pass `WorkflowTemplateValidator` — the migration cannot backfill `TargetOrganismId` or `StepMedia` because those concepts did not exist before this refactor (Section 6, item 12).
- `WorkflowErrorCodes.TemplateValidationFailed` is dead code (Section 6, item 14) — Prompt 2 should not build frontend error-code branching around it; template validation failures currently surface only as free text in `message`.
- `BiochemicalAttachmentId` has no backing entity or upload path (Section 6, item 5) — any frontend attachment UI for the biochemical step has nothing to call yet.

```
--- Verification sweep (real output, captured at report time) ---
```

**Build:**

```
$ dotnet build backend/MicroLIMS.sln --no-incremental
...
Build succeeded.
    0 Warning(s)
    0 Error(s)

Time Elapsed 00:00:07.23
```

**Tests:**

```
$ dotnet test backend/MicroLIMS.Tests/MicroLIMS.Tests.csproj
...
Passed!  - Failed:     0, Passed:   151, Skipped:     0, Total:   151, Duration: 9 s - MicroLIMS.Tests.dll (net8.0)
```

Matches the expected 151 passed / 0 failed.

**Frontend untouched:**

```
$ git diff --stat 9f76583..HEAD -- frontend/
(empty)
```

No file under `frontend/` was modified anywhere on this branch, from its base commit (`9f76583`) through HEAD.

**`ExpectedAppearanceSnapshot` immutability:**

```
$ grep -rn "ExpectedAppearanceSnapshot" backend --include=*.cs
```

Every occurrence is one of: the property declaration (`WorkflowStepResult.cs`, `ConfirmatoryPlateObservation.cs`), an EF migration/model-snapshot column definition, an object-initializer write at entity creation (`TestWorkflowEngine.cs` lines ~899 and ~1023, both inside `new WorkflowStepResult { ... }` / `new ConfirmatoryPlateObservation { ... }` expressions building a brand-new row, never touching an already-tracked/persisted entity), a read (`TestWorkflowController.cs`, `MediaAppearanceSnapshotService.cs`), or a test assertion. No reassignment of an already-persisted entity's `ExpectedAppearanceSnapshot` exists anywhere in the codebase. Constraint holds.
