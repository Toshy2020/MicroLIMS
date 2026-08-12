# Pathogen Workflow Frontend Update Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Update the MicroLIMS React/TypeScript frontend to speak the refactored pathogen-detection backend API (five-stage confirmatory workflow) on branch `pathogen-workflow-refactor`, replacing every reference to the removed dual-plate model.

**Architecture:** A new `PathogenStepDialog` owns the whole analyst-facing lifecycle for a non-CountTest, non-EM/AfterCleaning step (fetch `current-step`, dispatch on `stepType` via an exhaustive switch with an explicit "unsupported" fallback, own the incubation-lock countdown). `TestWorkflowDialog` becomes a thin router: CountTest keeps its existing UI (dual-plate fields stripped), EM/AfterCleaning keeps its existing batch-grid UI (dual-plate stripped), everything else renders `PathogenStepDialog`. `TestMasterPage`'s step editor is rebuilt around `StepType`/`TargetOrganismId`/`StepMedia`. Three downstream read-only surfaces are updated to the new three-state observation.

**Tech Stack:** React 18, TypeScript, MUI v5.16 (`@mui/material`, `@mui/icons-material`), axios (via the existing shared `apiClient`), Vite. No new dependencies.

## Global Constraints

- **FRONTEND ONLY.** Do not modify any file under `backend/`. If a task's implementer believes a backend change is required, it must stop and report — never work around it with an unsound frontend guess.
- `stepType` is sent/received as the enum's **string name**: `"PlateCount" | "BrothEnrichment" | "SelectiveBroth" | "SelectivePlating" | "ConfirmatoryPlating" | "BiochemicalTest"`. Never an ordinal.
- `observation` (the pathogen 3-state enum) is a string union: `"NoGrowth" | "GrowthNonConforming" | "GrowthConforming"`.
- `WorkflowType` is a string union: `"CountTest" | "Observation"`. `DualPlate` no longer exists anywhere.
- Every response is wrapped in `ApiResponse<T>` (`{ success, message, data, errors }`). On error, `errors[0]` is a machine error code; `errors[1]` is `remainingSeconds` (as a string) when the code is `INCUBATION_NOT_COMPLETE`. Never string-match on `message` to detect a specific failure — always branch on `errors[0]` through the shared parser built in Task 1.
- Never hardcode expected-appearance text. It is only ever a string that came from the API (`expectedAppearance` field); if the API returns `null`, show an error state, not an empty box or invented text.
- Never build attachment/file-upload UI for the biochemical step. `BiochemicalAttachmentId` has no backing entity or upload endpoint.
- No default/fallthrough case may render a growth-observation form. Every step-type switch must have an explicit branch per known `StepType` value plus an explicit "unsupported step type" branch that renders neither a form nor a silent blank screen.
- Match existing conventions: `FloatingDialog` component for any new modal surface (most new UI here is inline content inside the already-modal-hosted `TestWorkflowDialog`, not a new dialog); plain `useState` forms (no form library); manual field-level `catch (e: any) { setError(e?.response?.data?.message ?? "...") }` for *unstructured* errors, upgraded to the shared error-code parser for structured `WorkflowStepException` errors; MUI components only, no new UI library.
- Every task's completion bar: `cd frontend && npm run build` (runs `tsc -b && vite build` — a real typecheck, not just a dev-server smoke test) must succeed with zero TypeScript errors. `npm run lint` must report no new errors introduced by the task's files (pre-existing warnings elsewhere are not this task's responsibility to fix). State the exact commands run and their real output in every task's report — never claim a pass without having run it.
- Comments explain *why*, not *what*, matching the file's existing voice (see `TestWorkflowEngine.cs`-style reasoning comments already used in `TestWorkflowDialog.tsx`/`TestMasterPage.tsx`).

---

## Backend surface this plan is built against (verified directly, current code, not the spec)

**Route prefix `api/test-workflow`, `{testOrderId}` in every path, `stepName` in the body — there is no step-instance route.**

- `GET /{testOrderId}/current-step` → response includes `step` (with `stepType` as a string, `targetOrganismId`, but **no `stepMedia` array**), `workflowType`, `sampleContext`, `incubationLock` (`{ isLocked, incubationEndUtc, remainingSeconds } | null`), `previousSteps[]`, `allStepsComplete`, `finalResult`, `allSteps[]`, `completedSteps[]`, `totalSteps`, `testName`, `stepNumber`.
- `GET /{testOrderId}/eligible-incubators/{stepMediaId}` → `{ stepMediaId, tempMin, tempMax, eligibleIncubators: [{ id, name, code, setTemperature, calibrationStatus }] }`.
- `GET /{testOrderId}/permitted-confirmatory-media?stepName=` → `{ testOrderId, stepName, organism, permittedMedia: [{ stepMediaId, materialId, mediaName, expectedAppearance, tempMin, tempMax, availableLots: [{ id, lotNumber, expiryDate }] }] }`. **No step-type restriction server-side** — this is the only endpoint that returns a step's `stepMediaId`/`materialId`/permitted-lot list, so it must also be called for `BrothEnrichment`/`SelectiveBroth`/`SelectivePlating` steps (which each have exactly one entry in `permittedMedia`), not only `ConfirmatoryPlating`. This is how the frontend learns the `stepMediaId` needed for `eligible-incubators` and the `materialId`-filtered lot list for any pathogen step — `current-step` alone cannot answer this.
- `POST /{testOrderId}/select-media` — body `{ stepName, mediaLotId, incubatorId }`. **CountTest only now.**
- `POST /{testOrderId}/record-result` — body `{ stepName, plateReadings, dilutionFactor }`. **CountTest only now**; the engine throws if the step is not `PlateCount`.
- `GET /{testOrderId}/locations`, `POST /{testOrderId}/batch-results`, `POST /{testOrderId}/close-incubation-window` — EM/AfterCleaning, unchanged.
- `POST /{testOrderId}/batch-pathogen-results` — body `{ locations: [{ sampleLocationId, growthObserved }] }`. **Still a plain boolean**, not the 3-state enum — EM/AfterCleaning batch pathogen results never adopted the confirmatory model; this endpoint has no dual-plate variant anymore (it was deleted, not migrated — there is no batch-scoped confirmatory concept). Only the `isDualPlate` branching needs to be stripped from `PathogenLocationResultGridDialog`; its single-observation payload shape is unchanged.
- `POST /{testOrderId}/submit-broth` — body `{ stepName, mediaLotId, equipmentId, incubationStartUtc, incubationEndUtc, observation }` where `observation` is an **optional free-text string**, not the 3-state enum.
- `POST /{testOrderId}/submit-selective-plating` — body `{ stepName, mediaLotId, equipmentId, incubationStartUtc, incubationEndUtc, observation }` where `observation` **is** the 3-state `GrowthObservation` string.
- `POST /{testOrderId}/submit-confirmatory-setup` — body `{ stepName, selections: [{ stepMediaId, mediaLotId, equipmentId }], incubationStartUtc, incubationEndUtc }`.
- `POST /{testOrderId}/submit-confirmatory-observations` — body `{ stepName, observations: [{ materialId, observation }] }`. Note: keyed by **`materialId`**, not `stepMediaId`, matching the selected media by material.
- `POST /{testOrderId}/analyst-decision` — body `{ decision: "SubmitAsDetected" | "ProceedToBiochemical" }`.
- `POST /{testOrderId}/submit-biochemical` — body `{ stepName, biochemicalResultText, attachmentId }`. Always send `attachmentId: null` — there is no picker for it.
- `POST /results/{workflowStepResultId}/biochemical-decision` — body `{ approve: boolean, comment: string }`. Reviewer/SectionHead/SystemAdministrator only. **No frontend surface currently calls or displays anything related to this** (confirmed: zero hits for "biochemical" anywhere in `frontend/src`, and the existing `review`/`SampleReview*` frontend modules are stubs or do not exist). Building a reviewer screen for this is out of scope for this plan — Task 2 adds the service method only, per the spec's own Part 1 scoping ("service layer"). Report this gap in Task 11 rather than silently building a new screen nobody asked for in Parts 2-7.

**Response shapes returned by the `Submit*`/`analyst-decision`/`submit-biochemical`/`biochemical-decision` endpoints — `StepResultDto`:**
```
{ stepInstanceId: number, stepType: string, status: string, submittedByUserId: number,
  submittedAtUtc: string, nextStepUnlocked: boolean, workflowFinalResult: string | null, flags: string[] }
```
**`submit-confirmatory-observations` returns a different shape, `ConfirmatoryOutcomeDto`:**
```
{ stepInstanceId: number, confirmatoryResult: "AllConforming" | "Inconclusive", analystDecisionRequired: boolean, flags: string[] }
```

**`MasterDataController`, route prefix `api/masterdata` — Test Master step CRUD (verified current):**
- `GET test-definitions/{id}/steps` → each step includes `stepType` (string), `targetOrganismId`, `targetOrganism: { id, name } | null`, and **`stepMedia: [{ stepMediaId, materialId, materialName, tempMin, tempMax, isRequired, displayOrder }]`**.
- `POST` / `PUT test-definitions/{id}/steps` (create) / `test-definitions/steps/{stepId}` (update) — body:
```
{ stepName, mediaTypeId, incubationMinHours, incubationMaxHours, temperatureMin, temperatureMax,
  isFinalStep, stepType, targetOrganismId: number | null,
  stepMedia: [{ materialId, tempMin, tempMax, isRequired, displayOrder }] }
```
  **`mediaTypeId` is still a required, non-nullable `int` on every step, including every pathogen step type.** This was not called out in the task prompt and is a real, verified quirk of the current backend (`TestWorkflowStep.MediaTypeId` is non-nullable in the domain model) — it is semantically vestigial for anything but `PlateCount` but the API rejects a missing value. **Do not drop the Media Type selector from the step editor form for any step type** — keep it exactly as it exists today, for every `StepType`, or every pathogen step create/update will 400. Report this to the human partner in Task 11; it is not something a frontend-only task can fix.
- `PUT test-definitions/steps/{stepId}/move` — body `{ direction: "up" | "down" }`, unchanged.
- `DELETE test-definitions/steps/{stepId}`, unchanged.
- `GET masterdata/organisms` — already exists, already used nowhere yet in `TestMasterPage`; returns `[{ id, scientificName, atccNumber, commonName }]`.
- `GET api/inventory/materials?type=DehydratedMedia` — **new endpoint to add to the service layer.** Returns Material rows (`{ id, materialName, ... }`) for the `StepMedia.MaterialId` picker. No existing frontend call site for this route.

---

## Scope correction, verified directly — do NOT touch these three files

The original task list named `MediaEvaluationPage.tsx`, `mediaSummaryTypes.ts`, and `MediaReportPage.tsx` as broken. A full grep of every `growthObserved`/`GrowthObserved` occurrence in the frontend, cross-checked against the backend's own domain boundaries, confirms **all three files' `growthObserved` usage belongs entirely to the Media Evaluation (GPT / Indication-Inhibition challenge testing) bounded context**, which the backend refactor explicitly left untouched (`MediaEvaluation.GrowthObserved` is still `bool?`, unchanged, correct as-is). None of the three files reference `PathogenObservation`, `TestWorkflowStep`, or anything else this refactor touched. **Task 10 must not modify these three files.** Editing working, unrelated code here would be a pure regression risk for zero benefit. Report this correction in Task 11.

---

## Task 1: Shared pathogen workflow types and error-code parser

**Files:**
- Create: `frontend/src/modules/testingWorkspace/types/testWorkflowTypes.ts`
- Create: `frontend/src/modules/testingWorkspace/utils/workflowErrors.ts`
- Test: no automated test framework is configured for this frontend (confirmed: no `test`/`vitest`/`jest` script in `package.json`) — verification for every frontend task in this plan is `npm run build` (real typecheck) plus `npm run lint`, not a unit-test run. State this explicitly in each task's report rather than fabricating a test command that does not exist.

**Interfaces:**
- Produces: every type/union other tasks import. Get these exactly right; later tasks depend on the literal names.

- [ ] **Step 1: Write `testWorkflowTypes.ts`**

```typescript
// Mirrors backend/MicroLIMS.API/Controllers/TestWorkflowController.cs and
// TestWorkflowEngine.cs record types. StepType/GrowthObservation/WorkflowType
// are sent and received as their C# enum's string name, never an ordinal.

export type StepType =
  | "PlateCount"
  | "BrothEnrichment"
  | "SelectiveBroth"
  | "SelectivePlating"
  | "ConfirmatoryPlating"
  | "BiochemicalTest";

export type GrowthObservation = "NoGrowth" | "GrowthNonConforming" | "GrowthConforming";

export type WorkflowType = "CountTest" | "Observation";

export type ConfirmatoryResult = "AllConforming" | "Inconclusive";

export type AnalystDecision = "SubmitAsDetected" | "ProceedToBiochemical";

export interface TestWorkflowStepDto {
  id: number;
  stepOrder: number;
  stepName: string;
  mediaTypeId: number;
  stepType: StepType;
  targetOrganismId: number | null;
  mediaType: { id: number; class: string } | null;
  incubationMinHours: number;
  incubationMaxHours: number;
  temperatureMin: number;
  temperatureMax: number;
  isFinalStep: boolean;
}

export interface IncubationLock {
  isLocked: boolean;
  incubationEndUtc: string;
  remainingSeconds: number;
}

export interface SampleContext {
  sampleName: string;
  batchNumber: string | null;
  controlNumber: string | null;
  reason: string | null;
  systemReferenceNumber: string | null;
  sampleType: string;
  stage?: string; // present only when sampleType === "FinishedProduct" - omitted entirely otherwise, never null
}

export interface PreviousStepDetail {
  stepNumber: number;
  stepName: string;
  status: "Incubating" | "Complete";
  mediaName: string | null;
  lotNumber: string | null;
  incubatorName: string | null;
  incubatorSetTemp: number | null;
  incubationStartUtc: string | null;
  incubationEndUtc: string | null;
  observation: string | null;
}

export interface CompletedStepSummary {
  stepOrder: number;
  stepName: string;
  stepType: StepType;
  isFinalStep: boolean;
  outcome: string | null;
  observedAt: string | null;
  reportedResult: string | null;
  calculatedResult: number | null;
  status: string | null;
}

export interface CurrentStepResponse {
  testOrderId: number;
  step: TestWorkflowStepDto | null;
  stepNumber: number | null;
  totalSteps: number;
  testName: string;
  workflowType: WorkflowType;
  sampleContext: SampleContext;
  incubationLock: IncubationLock | null;
  previousSteps: PreviousStepDetail[];
  allStepsComplete: boolean;
  finalResult: string | null;
  allSteps: { stepOrder: number; stepName: string }[];
  completedSteps: CompletedStepSummary[];
  // Legacy CountTest/EM fields, still present on the same response for those workflow types:
  incubation?: {
    id: number; mediaId?: number; equipmentId?: number;
    temperature: string; duration: string; startedAt: string; expectedReadingAt: string;
  } | null;
}

export interface StepResultDto {
  stepInstanceId: number;
  stepType: string;
  status: string;
  submittedByUserId: number;
  submittedAtUtc: string;
  nextStepUnlocked: boolean;
  workflowFinalResult: string | null;
  flags: string[];
}

export interface ConfirmatoryOutcomeDto {
  stepInstanceId: number;
  confirmatoryResult: ConfirmatoryResult;
  analystDecisionRequired: boolean;
  flags: string[];
}

export interface PermittedConfirmatoryMediaEntry {
  stepMediaId: number;
  materialId: number;
  mediaName: string;
  expectedAppearance: string | null;
  tempMin: number;
  tempMax: number;
  availableLots: { id: number; lotNumber: string; expiryDate: string }[];
}

export interface PermittedConfirmatoryMediaResponse {
  testOrderId: number;
  stepName: string;
  organism: { id: number; name: string } | null;
  permittedMedia: PermittedConfirmatoryMediaEntry[];
}

export interface EligibleIncubatorsResponse {
  stepMediaId: number;
  tempMin: number;
  tempMax: number;
  eligibleIncubators: { id: number; name: string; code: string; setTemperature: number; calibrationStatus: string }[];
}
```

- [ ] **Step 2: Write `workflowErrors.ts`**

```typescript
// Every workflow error the backend can throw as a WorkflowStepException
// (backend/MicroLIMS.Shared/Constants/WorkflowErrorCodes.cs). ApiResponse
// has no error-code field, so the code travels as errors[0] and, for an
// incubation-lock failure, remainingSeconds travels as errors[1] - see
// TestWorkflowController.RunAsync. TEMPLATE_VALIDATION_FAILED is declared
// backend-side but never thrown (a plain-text InvalidOperationException is
// used instead for Test Master validation) - deliberately absent here.
export type WorkflowErrorCode =
  | "INCUBATION_NOT_COMPLETE"
  | "INCUBATION_WINDOW_INVALID"
  | "INCUBATION_WINDOW_TOO_SHORT"
  | "CONFIRMATORY_ALREADY_RECORDED"
  | "CONFIRMATORY_SETUP_ALREADY_SUBMITTED"
  | "MEDIA_NOT_IN_PERMITTED_LIST"
  | "NO_MEDIA_SELECTED"
  | "INCOMPLETE_CONFIRMATORY_SETUP"
  | "INCUBATOR_TEMP_OUT_OF_RANGE"
  | "BIOCHEMICAL_RESULT_REQUIRED"
  | "SEGREGATION_OF_DUTIES_VIOLATION";

export interface ParsedWorkflowError {
  code: WorkflowErrorCode | null;
  message: string;
  remainingSeconds: number | null;
}

// Read the structured code (and remainingSeconds, for an incubation lock)
// off an ApiResponse.Fail envelope. Falls back to the free-text message
// with code: null for anything the server didn't throw as a
// WorkflowStepException (e.g. a plain InvalidOperationException, or a
// network error with no response at all).
export function parseWorkflowError(e: any): ParsedWorkflowError {
  const data = e?.response?.data;
  const message: string = data?.message ?? "Something went wrong. Please try again.";
  const errors: string[] | undefined = data?.errors;
  const rawCode = errors?.[0];
  const knownCodes: WorkflowErrorCode[] = [
    "INCUBATION_NOT_COMPLETE", "INCUBATION_WINDOW_INVALID", "INCUBATION_WINDOW_TOO_SHORT",
    "CONFIRMATORY_ALREADY_RECORDED", "CONFIRMATORY_SETUP_ALREADY_SUBMITTED", "MEDIA_NOT_IN_PERMITTED_LIST",
    "NO_MEDIA_SELECTED", "INCOMPLETE_CONFIRMATORY_SETUP", "INCUBATOR_TEMP_OUT_OF_RANGE",
    "BIOCHEMICAL_RESULT_REQUIRED", "SEGREGATION_OF_DUTIES_VIOLATION"
  ];
  const code = knownCodes.includes(rawCode as WorkflowErrorCode) ? (rawCode as WorkflowErrorCode) : null;
  const remainingSeconds = code === "INCUBATION_NOT_COMPLETE" && errors?.[1] ? Number(errors[1]) : null;
  return { code, message, remainingSeconds };
}

// Per-code display text for the cases the plan calls out specifically.
// Falls back to the server's own message for every other code, which is
// already written to be analyst-readable (see WorkflowStepException
// call sites in TestWorkflowEngine.cs).
export function workflowErrorDisplayMessage(parsed: ParsedWorkflowError): string {
  switch (parsed.code) {
    case "CONFIRMATORY_ALREADY_RECORDED":
      return "This confirmatory plating has already been read out. Showing the recorded result.";
    case "CONFIRMATORY_SETUP_ALREADY_SUBMITTED":
      return "Confirmatory media have already been selected for this step and are incubating - go read the plates instead of selecting again.";
    default:
      return parsed.message;
  }
}
```

- [ ] **Step 3: Verify**

```bash
cd frontend && npm run build
```

Expected: succeeds, 0 TypeScript errors (these are new, unimported files — this only proves they are syntactically/type-valid in isolation; nothing imports them yet).

- [ ] **Step 4: Commit**

```bash
git add frontend/src/modules/testingWorkspace/types/testWorkflowTypes.ts frontend/src/modules/testingWorkspace/utils/workflowErrors.ts
git commit -m "feat: add pathogen workflow types and error-code parser"
```

---

## Task 2: Service layer — TestWorkflowService.ts rewrite, masterDataOptions.ts updates

**Files:**
- Modify: `frontend/src/modules/testingWorkspace/services/TestWorkflowService.ts`
- Modify: `frontend/src/services/masterDataOptions.ts`

**Interfaces:**
- Consumes: every type from `testWorkflowTypes.ts` (Task 1).
- Produces: every service method Tasks 4-9 call. Signatures below are final — do not deviate.

- [ ] **Step 1: Rewrite `TestWorkflowService.ts` in full**

```typescript
import { apiClient } from "../../../services/apiClient";
import {
  CurrentStepResponse, StepResultDto, ConfirmatoryOutcomeDto,
  PermittedConfirmatoryMediaResponse, EligibleIncubatorsResponse, AnalystDecision
} from "../types/testWorkflowTypes";

export const TestWorkflowService = {
  getCurrentStep: (testOrderId: number): Promise<CurrentStepResponse> =>
    apiClient.get(`/test-workflow/${testOrderId}/current-step`).then((r) => r.data.data),

  getEligibleIncubators: (testOrderId: number, stepMediaId: number): Promise<EligibleIncubatorsResponse> =>
    apiClient.get(`/test-workflow/${testOrderId}/eligible-incubators/${stepMediaId}`).then((r) => r.data.data),

  getPermittedConfirmatoryMedia: (testOrderId: number, stepName: string): Promise<PermittedConfirmatoryMediaResponse> =>
    apiClient.get(`/test-workflow/${testOrderId}/permitted-confirmatory-media`, { params: { stepName } }).then((r) => r.data.data),

  // CountTest only - the pathogen dual-plate fields this used to carry
  // (plate2MediaId, plate1Label, plate2Label) no longer exist server-side.
  selectMedia: (testOrderId: number, stepName: string, mediaLotId: number, incubatorId: number) =>
    apiClient.post(`/test-workflow/${testOrderId}/select-media`, { stepName, mediaLotId, incubatorId }).then((r) => r.data.data),

  // CountTest only - record-result now rejects any non-PlateCount step
  // server-side. Every pathogen step goes through the Submit* methods below.
  recordResult: (testOrderId: number, payload: { stepName: string; plateReadings: number[]; dilutionFactor: number }) =>
    apiClient.post(`/test-workflow/${testOrderId}/record-result`, payload).then((r) => r.data.data),

  getLocations: (testOrderId: number) =>
    apiClient.get(`/test-workflow/${testOrderId}/locations`).then((r) => r.data.data),

  closeIncubationWindow: (testOrderId: number) =>
    apiClient.post(`/test-workflow/${testOrderId}/close-incubation-window`).then((r) => r.data.data),

  // Still a single boolean per location - EM/AfterCleaning batch pathogen
  // results never adopted the confirmatory model; there is no dual-plate
  // variant of this endpoint anymore.
  recordBatchPathogenResults: (testOrderId: number, locations: { sampleLocationId: number; growthObserved: boolean }[]) =>
    apiClient.post(`/test-workflow/${testOrderId}/batch-pathogen-results`, { locations }).then((r) => r.data.data),

  recordBatchResults: (testOrderId: number, dilutionFactor: number, locations: { sampleLocationId: number; cfuResult: number }[]) =>
    apiClient.post(`/test-workflow/${testOrderId}/batch-results`, { dilutionFactor, locations }).then((r) => r.data.data),

  // ---- Pathogen five-stage workflow ----

  submitBroth: (
    testOrderId: number, stepName: string, mediaLotId: number, equipmentId: number,
    incubationStartUtc: string, incubationEndUtc: string, observation: string | null
  ): Promise<StepResultDto> =>
    apiClient.post(`/test-workflow/${testOrderId}/submit-broth`, {
      stepName, mediaLotId, equipmentId, incubationStartUtc, incubationEndUtc, observation
    }).then((r) => r.data.data),

  submitSelectivePlating: (
    testOrderId: number, stepName: string, mediaLotId: number, equipmentId: number,
    incubationStartUtc: string, incubationEndUtc: string, observation: string
  ): Promise<StepResultDto> =>
    apiClient.post(`/test-workflow/${testOrderId}/submit-selective-plating`, {
      stepName, mediaLotId, equipmentId, incubationStartUtc, incubationEndUtc, observation
    }).then((r) => r.data.data),

  submitConfirmatorySetup: (
    testOrderId: number, stepName: string,
    selections: { stepMediaId: number; mediaLotId: number; equipmentId: number }[],
    incubationStartUtc: string, incubationEndUtc: string
  ): Promise<StepResultDto> =>
    apiClient.post(`/test-workflow/${testOrderId}/submit-confirmatory-setup`, {
      stepName, selections, incubationStartUtc, incubationEndUtc
    }).then((r) => r.data.data),

  submitConfirmatoryObservations: (
    testOrderId: number, stepName: string,
    observations: { materialId: number; observation: string }[]
  ): Promise<ConfirmatoryOutcomeDto> =>
    apiClient.post(`/test-workflow/${testOrderId}/submit-confirmatory-observations`, { stepName, observations })
      .then((r) => r.data.data),

  recordAnalystDecision: (testOrderId: number, decision: AnalystDecision): Promise<StepResultDto> =>
    apiClient.post(`/test-workflow/${testOrderId}/analyst-decision`, { decision }).then((r) => r.data.data),

  submitBiochemical: (testOrderId: number, stepName: string, biochemicalResultText: string): Promise<StepResultDto> =>
    apiClient.post(`/test-workflow/${testOrderId}/submit-biochemical`, {
      stepName, biochemicalResultText, attachmentId: null
    }).then((r) => r.data.data),

  // Reviewer-only. No frontend UI calls this yet (see plan header) - the
  // method exists so a future review screen has something to call.
  recordBiochemicalDecision: (workflowStepResultId: number, approve: boolean, comment: string): Promise<StepResultDto> =>
    apiClient.post(`/test-workflow/results/${workflowStepResultId}/biochemical-decision`, { approve, comment })
      .then((r) => r.data.data)
};
```

- [ ] **Step 2: Update `masterDataOptions.ts`'s step-CRUD payload types and add the materials lookup**

Replace the `createTestWorkflowStep`/`updateTestWorkflowStep` entries (current lines ~56-65) with:

```typescript
  getMaterials: (type?: string) =>
    apiClient.get("/inventory/materials", { params: type ? { type } : {} }).then((r) => r.data.data),
  createTestWorkflowStep: (testDefinitionId: number, payload: {
    stepName: string; mediaTypeId: number; incubationMinHours: number; incubationMaxHours: number;
    temperatureMin: number; temperatureMax: number; isFinalStep: boolean; stepType: string;
    targetOrganismId: number | null;
    stepMedia: { materialId: number; tempMin: number; tempMax: number; isRequired: boolean; displayOrder: number }[];
  }) => apiClient.post(`/masterdata/test-definitions/${testDefinitionId}/steps`, payload).then((r) => r.data.data),
  updateTestWorkflowStep: (stepId: number, payload: {
    stepName: string; mediaTypeId: number; incubationMinHours: number; incubationMaxHours: number;
    temperatureMin: number; temperatureMax: number; isFinalStep: boolean; stepType: string;
    targetOrganismId: number | null;
    stepMedia: { materialId: number; tempMin: number; tempMax: number; isRequired: boolean; displayOrder: number }[];
  }) => apiClient.put(`/masterdata/test-definitions/steps/${stepId}`, payload).then((r) => r.data.data),
```

Leave `moveTestWorkflowStep`, `deleteTestWorkflowStep`, `getTestWorkflowSteps`, and every other export in this file untouched.

- [ ] **Step 3: Verify**

```bash
cd frontend && npm run build
```

Expected: fails with TypeScript errors in `TestWorkflowDialog.tsx`, `TestMasterPage.tsx`, and `PathogenLocationResultGridDialog.tsx` — every one of them still calls the removed `TestWorkflowService.selectMedia(..., plate2MediaId, ...)` 5-arg overload, references `.stepResultType`/`.isDualPlate`/`.plate1DefaultLabel` on values now typed as `CurrentStepResponse`/`TestWorkflowStepDto` (which have no such fields), or passes the old `stepResultType`/`plate1DefaultLabel` fields into `masterDataOptions.createTestWorkflowStep`'s now-incompatible payload type. **This is expected** — later tasks fix each file. Confirm the error list is confined to those three files and does not include any file outside this plan's known-broken list; if it does, stop and report rather than guessing why.

- [ ] **Step 4: Commit**

```bash
git add frontend/src/modules/testingWorkspace/services/TestWorkflowService.ts frontend/src/services/masterDataOptions.ts
git commit -m "feat: rewrite TestWorkflowService for the five-stage pathogen API"
```

---

## Task 3: Incubation countdown hook

**Files:**
- Create: `frontend/src/modules/testingWorkspace/hooks/useIncubationCountdown.ts`

**Interfaces:**
- Consumes: `IncubationLock` type (Task 1).
- Produces: `useIncubationCountdown(lock, onUnlocked?)` hook, used by Tasks 5-7.

- [ ] **Step 1: Write the hook, mirroring the existing interval/cleanup pattern in `frontend/src/hooks/useIdleTimeout.ts`**

```typescript
import { useEffect, useRef, useState } from "react";
import { IncubationLock } from "../types/testWorkflowTypes";

// Option B countdown: the submit control stays visible but disabled,
// showing a live remaining-time readout, unlocking itself the instant
// the window ends - never hidden, mirroring the existing
// useIdleTimeout ref-held-interval pattern. The server is still the
// source of truth (every Submit* call is re-validated server-side);
// this only avoids the analyst needing to reload the page to see the
// button re-enable.
export function useIncubationCountdown(lock: IncubationLock | null, onUnlocked?: () => void) {
  const [remainingSeconds, setRemainingSeconds] = useState(lock?.remainingSeconds ?? 0);
  const intervalRef = useRef<ReturnType<typeof setInterval> | null>(null);
  const firedRef = useRef(false);
  // Kept fresh every render (not a dependency) so the interval always
  // calls the caller's current onUnlocked, never a stale closure from
  // whenever the effect last ran - same fix useIdleTimeout.ts already
  // applies to its own onTimeout callback via onTimeoutRef.
  const onUnlockedRef = useRef(onUnlocked);
  onUnlockedRef.current = onUnlocked;

  useEffect(() => {
    setRemainingSeconds(lock?.remainingSeconds ?? 0);
    firedRef.current = false;
    if (intervalRef.current) clearInterval(intervalRef.current);

    if (!lock || !lock.isLocked) return;

    const endTime = new Date(lock.incubationEndUtc).getTime();
    intervalRef.current = setInterval(() => {
      const remaining = Math.max(0, Math.ceil((endTime - Date.now()) / 1000));
      setRemainingSeconds(remaining);
      if (remaining === 0 && !firedRef.current) {
        firedRef.current = true;
        onUnlockedRef.current?.();
        if (intervalRef.current) clearInterval(intervalRef.current);
      }
    }, 1000);

    return () => { if (intervalRef.current) clearInterval(intervalRef.current); };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [lock?.incubationEndUtc, lock?.isLocked]);

  const isLocked = !!lock?.isLocked && remainingSeconds > 0;
  const formatted = formatCountdown(remainingSeconds);
  return { isLocked, remainingSeconds, formatted };
}

function formatCountdown(totalSeconds: number): string {
  const h = Math.floor(totalSeconds / 3600);
  const m = Math.floor((totalSeconds % 3600) / 60);
  const s = totalSeconds % 60;
  if (h > 0) return `${h}h ${m}m ${s}s`;
  if (m > 0) return `${m}m ${s}s`;
  return `${s}s`;
}
```

- [ ] **Step 2: Verify**

```bash
cd frontend && npm run build
```

Expected: same confined error list as Task 2 (this new file is unimported so far and type-valid on its own).

- [ ] **Step 3: Commit**

```bash
git add frontend/src/modules/testingWorkspace/hooks/useIncubationCountdown.ts
git commit -m "feat: add incubation-lock countdown hook"
```

---

## Task 4: Strip dual-plate from the CountTest/EM shell and wire in the pathogen router

**Files:**
- Modify: `frontend/src/modules/testingWorkspace/TestWorkflowDialog.tsx`
- Modify: `frontend/src/modules/testingWorkspace/PathogenLocationResultGridDialog.tsx`

**Interfaces:**
- Consumes: `TestWorkflowService` (Task 2), `CurrentStepResponse`/`WorkflowType` types (Task 1).
- Produces: `TestWorkflowDialog` renders `<PathogenStepDialog testOrderId={...} testCode={...} displayName={...} />` for any step whose `current.workflowType !== "CountTest"` and the sample is not EM/AfterCleaning. `PathogenStepDialog` itself does not exist until Task 5 — this task creates the call site and a temporary placeholder component so the build stays green in between; Task 5 replaces the placeholder file wholesale.

- [ ] **Step 1: Strip `isDualPlate` from `PathogenLocationResultGridDialog.tsx`**

Read the current file in full first (163 lines). Remove the `isDualPlate: boolean` prop entirely and every branch keyed on it (the allEntered check, inconclusive count, `liveStatus`, submit payload shape, table headers, table cells — six sites, all previously located at lines 22, 32, 53, 56, 59, 76, 104, 119). The component becomes single-observation-only: one `growthObserved` yes/no per location, submitted as `{ sampleLocationId, growthObserved }[]` via `TestWorkflowService.recordBatchPathogenResults`. This matches the backend exactly — there is no dual-plate variant of `batch-pathogen-results` to preserve.

- [ ] **Step 2: In `TestWorkflowDialog.tsx`, remove every dual-plate/pathogen-Observation-specific piece of state and JSX**

Remove: `plate2MediaId`, `plate1Label`, `plate2Label` state (lines 76-78); `growthObserved`, `plate1Growth`, `plate2Growth` state (lines 82-84); the `isDualGrowth`/`samePlateLot`/`materialMedia`/`matchingMedia` derived values that only exist to serve the old Observation branch (lines 112-123) — but **keep** `classMedia`/`matchingIncubators` computation logic if `CountTest` still needs it for its own select-media phase (verify by reading the CountTest branch's JSX before deleting anything it depends on); remove the entire `isDualGrowth` branch of the "select-media" phase JSX (lines 239-261's ternary, keep only the non-dual `<Select>`); remove `isDualGrowth`/`plate1Growth`/`plate2Growth`/`growthObserved` JSX in "enter-result" phase (lines 353-395); remove `isDualPlate={!!step?.isDualPlate}` prop passed to `PathogenLocationResultGridDialog` (line 329, now deleted by Step 1 anyway); remove the `plate1DefaultLabel`/`plate2DefaultLabel` seeding in `load()` (line 94).

Change `submitResult`'s branching (currently `if (current.workflowType === "CountTest") {...} else if (isDualGrowth) {...} else {...}`) down to just the CountTest branch — the entire non-CountTest `record-result` path is now handled by `PathogenStepDialog`, not here.

Change the top-level render logic so that when `current.workflowType !== "CountTest"` and `!isEmOrAfterCleaning`, the component renders:

```tsx
<PathogenStepDialog testOrderId={testOrderId} testCode={testCode} displayName={displayName} />
```

instead of any of the existing "select-media"/"awaiting-result"/"enter-result" phase JSX for that case. `current.workflowType === "CountTest"` keeps its existing phases exactly as they are today (minus the dual-plate branches, which never applied to CountTest anyway — CountTest's `isDualGrowth` was always `false` since `stepResultType` on a `PlateCount` step was never `"DualGrowth"`, so this removal is a no-op for CountTest specifically; verify this is true by re-reading the original file, don't just assume). `isEmOrAfterCleaning` keeps its existing `LocationResultGridDialog`/`PathogenLocationResultGridDialog` routing exactly as today.

- [ ] **Step 2b: Create the temporary placeholder so the build passes**

```typescript
// frontend/src/modules/testingWorkspace/PathogenStepDialog.tsx
// TEMPORARY placeholder - Task 5 replaces this file wholesale with the
// real step-type-dispatching implementation.
import { Alert } from "@mui/material";
interface Props { testOrderId: number; testCode: string; displayName: string; }
export function PathogenStepDialog({ testOrderId }: Props) {
  return <Alert severity="info">Pathogen step UI for test order #{testOrderId} - under construction.</Alert>;
}
```

- [ ] **Step 3: Verify**

```bash
cd frontend && npm run build
cd frontend && npm run lint
```

Expected: build succeeds (0 TypeScript errors) for these two files plus the placeholder. `TestMasterPage.tsx` will still fail — untouched until Task 9; confirm the error list is confined to that one remaining file.

- [ ] **Step 4: Commit**

```bash
git add frontend/src/modules/testingWorkspace/TestWorkflowDialog.tsx frontend/src/modules/testingWorkspace/PathogenLocationResultGridDialog.tsx frontend/src/modules/testingWorkspace/PathogenStepDialog.tsx
git commit -m "refactor: strip dual-plate model from the CountTest/EM shell"
```

---

## Task 5: PathogenStepDialog orchestrator + BrothStepPanel

**Files:**
- Modify (wholesale replace): `frontend/src/modules/testingWorkspace/PathogenStepDialog.tsx`
- Create: `frontend/src/modules/testingWorkspace/pathogenSteps/BrothStepPanel.tsx`
- Create: `frontend/src/modules/testingWorkspace/pathogenSteps/UnsupportedStepPanel.tsx`
- Create: `frontend/src/modules/testingWorkspace/pathogenSteps/InconclusiveTerminalPanel.tsx`

**Interfaces:**
- Consumes: `TestWorkflowService` (Task 2), `useIncubationCountdown` (Task 3), all types (Task 1).
- Produces: `PathogenStepDialog` component (real implementation); `BrothStepPanel({ testOrderId, step, incubationLock, onSubmitted }: BrothStepPanelProps)`; the step-chain strip, incubation lock display, and the discriminated `stepType` switch that Tasks 6-8 plug their panels into.

**A step is `Inconclusive`-terminal (Task 4/Part 4 of the original prompt) when:** the current `step.stepType === "BiochemicalTest"` (i.e. the chain nominally advanced past confirmatory plating) **but** the most recently completed step's `outcome`/status indicates an `Inconclusive` confirmatory result. The backend does not return a single dedicated boolean for this — infer it from `current.completedSteps`: find the entry with `stepType === "ConfirmatoryPlating"`; if its `outcome` (a free-text string written by `FinalizeWorkflowAsync`-style summaries, e.g. containing "Inconclusive") indicates a non-conforming/inconclusive result, treat the workflow as terminal regardless of what `current.step` says. Read the actual `outcome` strings the backend writes (`TestWorkflowEngine.SubmitConfirmatoryObservationsAsync`, search for the exact string it stores) before hardcoding a match — verify precisely rather than guessing at the substring.

- [ ] **Step 1: Read `TestWorkflowEngine.cs`'s `SubmitConfirmatoryObservationsAsync` to find the exact outcome/history text written for an Inconclusive result**, so the detection in Step 3 below matches it exactly rather than guessing.

- [ ] **Step 2: Write `UnsupportedStepPanel.tsx`**

```tsx
import { Alert } from "@mui/material";
import { StepType } from "../types/testWorkflowTypes";

// The explicit fallback every step-type switch in this module must have.
// Rendering a growth-observation form here by accident is the exact bug
// class this whole refactor exists to eliminate - this panel is what
// stands in its place instead of a silent fallthrough.
export function UnsupportedStepPanel({ stepType }: { stepType: string }) {
  return (
    <Alert severity="error">
      This workflow step's type ("{stepType}") is not supported by this dialog. Contact a System Administrator -
      the Test Master template for this step may be misconfigured.
    </Alert>
  );
}
```

- [ ] **Step 3: Write `InconclusiveTerminalPanel.tsx`**

```tsx
import { Box, Alert, Typography } from "@mui/material";

// Per current-step's own advertised next step, GetCurrentStepAsync
// reports the chain as having advanced past confirmatory plating after
// an Inconclusive result - but the biochemical step can never actually
// be submitted for that order (SubmitBiochemicalAsync always refuses a
// non-AllConforming confirmatory result). Do not follow the advertised
// next step literally: render a terminal state instead of a biochemical
// form the analyst cannot submit.
export function InconclusiveTerminalPanel() {
  return (
    <Box>
      <Alert severity="warning" sx={{ mb: 1 }}>
        Confirmatory plating result: <strong>Inconclusive</strong>
      </Alert>
      <Typography variant="body2">
        This result has been flagged for investigation. A retest is required. No further action is available
        to the analyst on this test order until the investigation is resolved.
      </Typography>
    </Box>
  );
}
```

- [ ] **Step 4: Write `BrothStepPanel.tsx`**

Fetches its own permitted-media entry (single item) via `TestWorkflowService.getPermittedConfirmatoryMedia(testOrderId, step.stepName)` — per the plan header, this endpoint is not confirmatory-only despite its name, and it's the only way to learn the step's `stepMediaId`/`materialId`/permitted lots. Then fetches eligible incubators via `TestWorkflowService.getEligibleIncubators(testOrderId, stepMediaId)`.

```tsx
import { useEffect, useState } from "react";
import { Box, Typography, Select, MenuItem, TextField, Button, Stack, Alert } from "@mui/material";
import { TestWorkflowService } from "../services/TestWorkflowService";
import { TestWorkflowStepDto, PermittedConfirmatoryMediaEntry, StepResultDto } from "../types/testWorkflowTypes";
import { parseWorkflowError, workflowErrorDisplayMessage } from "../utils/workflowErrors";

interface Props {
  testOrderId: number;
  step: TestWorkflowStepDto;
  onSubmitted: (result: StepResultDto) => void;
}

// BrothEnrichment/SelectiveBroth: preparation only. There is deliberately
// no result-interpretation UI here - the chain runs to completion
// regardless of what the analyst observes here (the method requires it),
// so this must never be framed as a pass/fail decision point.
export function BrothStepPanel({ testOrderId, step, onSubmitted }: Props) {
  const [medium, setMedium] = useState<PermittedConfirmatoryMediaEntry | null>(null);
  const [incubators, setIncubators] = useState<{ id: number; name: string; code: string; setTemperature: number }[]>([]);
  const [mediaLotId, setMediaLotId] = useState<number | "">("");
  const [equipmentId, setEquipmentId] = useState<number | "">("");
  const [durationHours, setDurationHours] = useState(String(step.incubationMinHours));
  const [observation, setObservation] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    setLoading(true);
    setError(null);
    TestWorkflowService.getPermittedConfirmatoryMedia(testOrderId, step.stepName)
      .then(async (res) => {
        const only = res.permittedMedia[0] ?? null;
        setMedium(only);
        if (only) {
          const eligible = await TestWorkflowService.getEligibleIncubators(testOrderId, only.stepMediaId);
          setIncubators(eligible.eligibleIncubators);
        }
      })
      .catch((e) => setError(workflowErrorDisplayMessage(parseWorkflowError(e))))
      .finally(() => setLoading(false));
  }, [testOrderId, step.stepName]);

  const submit = async () => {
    setError(null);
    if (!medium || !mediaLotId || !equipmentId) { setError("Select a media lot and an incubator."); return; }
    const startUtc = new Date().toISOString();
    const endUtc = new Date(Date.now() + Number(durationHours) * 3600 * 1000).toISOString();
    try {
      const result = await TestWorkflowService.submitBroth(
        testOrderId, step.stepName, Number(mediaLotId), Number(equipmentId),
        startUtc, endUtc, observation.trim() || null
      );
      onSubmitted(result);
    } catch (e) {
      setError(workflowErrorDisplayMessage(parseWorkflowError(e)));
    }
  };

  if (loading) return <Typography variant="body2">Loading step configuration…</Typography>;
  if (!medium) return <Alert severity="error">This step has no assigned medium configured in Test Master.</Alert>;

  return (
    <Stack spacing={1.5}>
      {error && <Alert severity="error">{error}</Alert>}
      <Alert severity="info">
        This step is preparation only - it is not a pass/fail result. The observation below (if any) is recorded
        for the record; the workflow proceeds to the next step regardless of what is observed here.
      </Alert>
      <Typography variant="body2">Medium: <strong>{medium.mediaName}</strong></Typography>
      <Select displayEmpty size="small" value={mediaLotId} onChange={(e) => setMediaLotId(Number(e.target.value))}>
        <MenuItem value=""><em>Media Lot</em></MenuItem>
        {medium.availableLots.map((l) => (
          <MenuItem key={l.id} value={l.id}>{l.lotNumber} — expires {new Date(l.expiryDate).toLocaleDateString()}</MenuItem>
        ))}
      </Select>
      <Select displayEmpty size="small" value={equipmentId} onChange={(e) => setEquipmentId(Number(e.target.value))}>
        <MenuItem value=""><em>Incubator ({medium.tempMin}-{medium.tempMax} °C)</em></MenuItem>
        {incubators.map((i) => <MenuItem key={i.id} value={i.id}>{i.name} ({i.code}) — {i.setTemperature}°C</MenuItem>)}
      </Select>
      <TextField
        size="small" type="number" label="Incubation Duration (hours)" value={durationHours}
        onChange={(e) => setDurationHours(e.target.value)} sx={{ maxWidth: 220 }}
        helperText={`Template range: ${step.incubationMinHours}-${step.incubationMaxHours}h`}
      />
      <TextField
        size="small" multiline minRows={2} label="Observation (optional)" value={observation}
        onChange={(e) => setObservation(e.target.value)}
      />
      <Stack direction="row" justifyContent="flex-end">
        <Button variant="contained" onClick={submit}>Start Incubation</Button>
      </Stack>
    </Stack>
  );
}
```

- [ ] **Step 5: Write the real `PathogenStepDialog.tsx`, replacing the Task 4 placeholder wholesale**

```tsx
import { useEffect, useState } from "react";
import { Box, Typography, Stack, Alert } from "@mui/material";
import { LoadingSpinner } from "../../components/LoadingSpinner";
import { TestWorkflowService } from "./services/TestWorkflowService";
import { CurrentStepResponse, StepResultDto } from "./types/testWorkflowTypes";
import { parseWorkflowError, workflowErrorDisplayMessage } from "./utils/workflowErrors";
import { useIncubationCountdown } from "./hooks/useIncubationCountdown";
import { BrothStepPanel } from "./pathogenSteps/BrothStepPanel";
import { UnsupportedStepPanel } from "./pathogenSteps/UnsupportedStepPanel";
import { InconclusiveTerminalPanel } from "./pathogenSteps/InconclusiveTerminalPanel";
// Tasks 6-8 add these imports and switch branches:
// import { SelectivePlatingPanel } from "./pathogenSteps/SelectivePlatingPanel";
// import { ConfirmatoryPlatingPanel } from "./pathogenSteps/ConfirmatoryPlatingPanel";
// import { BiochemicalTestPanel } from "./pathogenSteps/BiochemicalTestPanel";

interface Props { testOrderId: number; testCode: string; displayName: string; }

// TODO(Step 1 of this task, verify against real backend text): the exact
// substring TestWorkflowEngine writes into a completed ConfirmatoryPlating
// step's outcome/history note for an Inconclusive result. Do not guess -
// read TestWorkflowEngine.SubmitConfirmatoryObservationsAsync first.
const INCONCLUSIVE_OUTCOME_MARKER = "Inconclusive";

function isInconclusiveTerminal(current: CurrentStepResponse): boolean {
  if (current.step?.stepType !== "BiochemicalTest") return false;
  const confirmatoryStep = current.completedSteps.find((s) => s.stepType === "ConfirmatoryPlating");
  return !!confirmatoryStep?.outcome?.includes(INCONCLUSIVE_OUTCOME_MARKER);
}

function StepChainStrip({ current }: { current: CurrentStepResponse }) {
  const completedByOrder = new Map(current.completedSteps.map((s) => [s.stepOrder, s]));
  const currentOrder = current.step?.stepOrder ?? null;
  return (
    <Stack direction="row" spacing={1} sx={{ mb: 2, flexWrap: "wrap" }}>
      {current.allSteps.map((s) => {
        const done = completedByOrder.get(s.stepOrder);
        const isCurrent = s.stepOrder === currentOrder;
        const isInconclusive = done?.outcome?.includes(INCONCLUSIVE_OUTCOME_MARKER);
        let bg = "#eef0f4", color = "#6b7280", border = "1px solid #d9dce3", label = s.stepName;
        if (done) {
          label = `${s.stepName}: ${done.outcome}`;
          bg = isInconclusive ? "#fdecea" : "#e8f6ec";
          color = isInconclusive ? "#b3261e" : "#1e7a34";
          border = `1px solid ${isInconclusive ? "#f3b7b2" : "#a8ddb5"}`;
        } else if (isCurrent) {
          label = `${s.stepName}: In progress`; bg = "#eaf1fd"; color = "#1a56db"; border = "1px solid #a9c6f5";
        }
        return (
          <Box key={s.stepOrder} sx={{ px: 1.25, py: 0.5, borderRadius: 999, fontSize: 12, fontWeight: 600, bgcolor: bg, color, border }}>
            {done ? (isInconclusive ? "✗ " : "✓ ") : ""}{label}
          </Box>
        );
      })}
    </Stack>
  );
}

export function PathogenStepDialog({ testOrderId }: Props) {
  const [current, setCurrent] = useState<CurrentStepResponse | null>(null);
  const [error, setError] = useState<string | null>(null);

  const load = async () => {
    setError(null);
    try {
      const data = await TestWorkflowService.getCurrentStep(testOrderId);
      setCurrent(data);
    } catch (e) {
      setError(workflowErrorDisplayMessage(parseWorkflowError(e)));
    }
  };

  useEffect(() => { load(); /* eslint-disable-next-line react-hooks/exhaustive-deps */ }, [testOrderId]);

  const countdown = useIncubationCountdown(current?.incubationLock ?? null, load);

  const handleSubmitted = (_result: StepResultDto) => load();

  if (error && !current) return <Alert severity="error">{error}</Alert>;
  if (!current) return <Box sx={{ py: 4 }}><LoadingSpinner /></Box>;

  if (current.allStepsComplete) {
    return (
      <Box>
        <StepChainStrip current={current} />
        <Alert severity={current.finalResult === "Detected" ? "error" : "success"}>
          Final result: <strong>{current.finalResult}</strong>
        </Alert>
      </Box>
    );
  }

  if (isInconclusiveTerminal(current)) {
    return (
      <Box>
        <StepChainStrip current={current} />
        <InconclusiveTerminalPanel />
      </Box>
    );
  }

  const step = current.step;
  if (!step) return <Alert severity="error">No current step is available for this test order.</Alert>;

  return (
    <Box>
      <StepChainStrip current={current} />
      {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}
      <Typography sx={{ fontWeight: 700, mb: 0.5 }}>
        Step {step.stepOrder}: {step.stepName}
        {step.isFinalStep && <Typography component="span" variant="caption" color="text.secondary"> — determines the final result</Typography>}
      </Typography>
      {current.incubationLock?.isLocked && (
        <Alert severity="warning" sx={{ mb: 1.5 }}>
          Incubation in progress — {countdown.formatted} remaining. Submission unlocks automatically.
        </Alert>
      )}

      {step.stepType === "BrothEnrichment" || step.stepType === "SelectiveBroth" ? (
        <BrothStepPanel testOrderId={testOrderId} step={step} onSubmitted={handleSubmitted} />
      ) : step.stepType === "SelectivePlating" ? (
        <UnsupportedStepPanel stepType={step.stepType} /> /* Task 6 replaces this branch */
      ) : step.stepType === "ConfirmatoryPlating" ? (
        <UnsupportedStepPanel stepType={step.stepType} /> /* Task 7 replaces this branch */
      ) : step.stepType === "BiochemicalTest" ? (
        <UnsupportedStepPanel stepType={step.stepType} /> /* Task 8 replaces this branch */
      ) : (
        <UnsupportedStepPanel stepType={step.stepType} />
      )}
    </Box>
  );
}
```

Note the deliberate structure: every branch is explicit, and even the two not-yet-implemented step types (Tasks 6-8) render `UnsupportedStepPanel` rather than nothing or a growth form — the switch is exhaustive-shaped from this task onward, and Tasks 6-8 each replace exactly one ternary arm, never touching the overall shape.

- [ ] **Step 6: Verify**

```bash
cd frontend && npm run build
cd frontend && npm run lint
```

Expected: succeeds for every file this task touches (0 TypeScript errors). `TestMasterPage.tsx` remains the only outstanding broken file until Task 9.

- [ ] **Step 7: Commit**

```bash
git add frontend/src/modules/testingWorkspace/PathogenStepDialog.tsx frontend/src/modules/testingWorkspace/pathogenSteps/
git commit -m "feat: add pathogen step dialog orchestrator and broth step panel"
```

---

## Task 6: SelectivePlatingPanel

**Files:**
- Create: `frontend/src/modules/testingWorkspace/pathogenSteps/SelectivePlatingPanel.tsx`
- Modify: `frontend/src/modules/testingWorkspace/PathogenStepDialog.tsx` (one ternary arm)

**Interfaces:**
- Consumes: `TestWorkflowService.submitSelectivePlating`, `getPermittedConfirmatoryMedia`, `getEligibleIncubators` (Task 2); `GrowthObservation` type (Task 1).
- Produces: `SelectivePlatingPanel({ testOrderId, step, onSubmitted })`.

- [ ] **Step 1: Write `SelectivePlatingPanel.tsx`**

Same media-lot/incubator/window fetch pattern as `BrothStepPanel` (reuse `getPermittedConfirmatoryMedia` for the single permitted medium — do not duplicate that fetch differently; copy the established pattern). The differences: **show `medium.expectedAppearance` prominently above the observation selector** (never invented, never hardcoded — if `expectedAppearance` is `null`, render an error state instead of an empty box, per the plan's global constraint); a **three-state radio group** mapping to the exact `GrowthObservation` values:

```tsx
<RadioGroup value={observation} onChange={(e) => setObservation(e.target.value as GrowthObservation)}>
  <FormControlLabel value="NoGrowth" control={<Radio />} label="No growth — target absent" />
  <FormControlLabel value="GrowthNonConforming" control={<Radio />} label="Growth present, does not match expected appearance — target absent" />
  <FormControlLabel value="GrowthConforming" control={<Radio />} label="Growth matching expected appearance — presumptive positive" />
</RadioGroup>
```

and a required observed-appearance free-text note whenever `observation !== "NoGrowth"` (validate before submit: if `observation` is `"GrowthNonConforming"` or `"GrowthConforming"` and the note is blank, block submission with a clear inline error — do not silently submit without it; there is no dedicated backend field for this note in `submit-selective-plating`'s request body, so send it appended into... **stop and check**: the backend's `SubmitSelectivePlatingRequest` has no free-text field at all besides the enum `observation`. There is nowhere to send an observed-appearance note for this endpoint. **Do not invent a field the API doesn't accept.** Keep the required-note validation as a client-side UX prompt that ensures the analyst has actually looked and typed something, but only submit the `observation` enum value itself — do not attempt to smuggle the note text into any request field. State this discrepancy plainly in the task report: the spec asked for an observed-appearance note to be required and presumably recorded, but the backend has no field to receive it for this endpoint, so the note is captured as a client-side confirmation step only and is not persisted. This is a real gap between the spec and the deployed API — report it, do not paper over it with a guess.

Submit via `TestWorkflowService.submitSelectivePlating(testOrderId, step.stepName, mediaLotId, equipmentId, startUtc, endUtc, observation)`. On success, call `onSubmitted(result)`.

- [ ] **Step 2: In `PathogenStepDialog.tsx`, replace the `SelectivePlating` ternary arm**

```tsx
) : step.stepType === "SelectivePlating" ? (
  <SelectivePlatingPanel testOrderId={testOrderId} step={step} onSubmitted={handleSubmitted} />
```

and add the import.

- [ ] **Step 3: Verify**

```bash
cd frontend && npm run build
cd frontend && npm run lint
```

- [ ] **Step 4: Commit**

```bash
git add frontend/src/modules/testingWorkspace/pathogenSteps/SelectivePlatingPanel.tsx frontend/src/modules/testingWorkspace/PathogenStepDialog.tsx
git commit -m "feat: add selective plating panel with three-state observation"
```

---

## Task 7: ConfirmatoryPlatingPanel — setup, read-out, analyst decision

**Files:**
- Create: `frontend/src/modules/testingWorkspace/pathogenSteps/ConfirmatoryPlatingPanel.tsx`
- Modify: `frontend/src/modules/testingWorkspace/PathogenStepDialog.tsx` (one ternary arm)

**Interfaces:**
- Consumes: `TestWorkflowService.getPermittedConfirmatoryMedia`, `getEligibleIncubators`, `submitConfirmatorySetup`, `submitConfirmatoryObservations`, `recordAnalystDecision` (Task 2); `PermittedConfirmatoryMediaEntry`, `ConfirmatoryOutcomeDto`, `GrowthObservation`, `AnalystDecision` (Task 1).
- Produces: `ConfirmatoryPlatingPanel({ testOrderId, step, onSubmitted })`. Internally two-phase: this component owns its own local phase state (`"setup" | "readout" | "decision"`), independent of anything in `PathogenStepDialog` — `PathogenStepDialog` only knows "current step is ConfirmatoryPlating" and re-fetches `current-step` (calling `onSubmitted`) whenever this panel completes an action that should refresh the whole dialog (setup submission does NOT complete the step, so do not call `onSubmitted` there — only after observations are recorded or a decision is made).

**This is the most complex panel. Design it as three internal sub-renders inside one component, gated by local state, not three separate exported components** — they share the fetched `permittedMedia` list and the `testOrderId`/`step` props, and splitting them into separate files would just require prop-drilling all of it back out.

- [ ] **Step 1: Fetch permitted media on mount**

```tsx
const [permitted, setPermitted] = useState<PermittedConfirmatoryMediaEntry[]>([]);
const [organism, setOrganism] = useState<{ id: number; name: string } | null>(null);
// ... loading/error state, same pattern as BrothStepPanel
useEffect(() => {
  TestWorkflowService.getPermittedConfirmatoryMedia(testOrderId, step.stepName)
    .then((res) => { setPermitted(res.permittedMedia); setOrganism(res.organism); })
    .catch(/* same error-parsing pattern */);
}, [testOrderId, step.stepName]);
```

Determine initial phase: if `current` step chain shows this step already has a `WorkflowStepResult` awaiting read-out (there is no direct signal for "setup submitted, awaiting observations" in `current-step`'s response — the only way to find out is to attempt the setup call and read the error). **Handle this via the error-code branch, not a guess:** default `phase` to `"setup"`. If `submitConfirmatorySetup` fails with `errors[0] === "CONFIRMATORY_SETUP_ALREADY_SUBMITTED"`, do not show it as a generic error — instead transition `phase` to `"readout"` directly (per Part 3 of the original prompt: "route to phase B, NOT a generic error"). Similarly, if it fails with `CONFIRMATORY_ALREADY_RECORDED`, the step has already been read out — show the recorded result rather than any form; since `current-step`'s `completedSteps` entry for this step (if present) carries the `outcome` summary, use that to render a read-only "already recorded" state instead of re-attempting anything.

Since the analyst may open this step fresh (never having attempted setup this session), also proactively check `current.completedSteps` for an entry with `stepType === "ConfirmatoryPlating"` before ever calling setup — if one exists and the step isn't finalized, that's the read-out-already-recorded case; if the step is the *current* step but not yet in `completedSteps`, it may still be mid-setup from an earlier session — there is genuinely no way to know without attempting an action, so default to `"setup"` and let the `CONFIRMATORY_SETUP_ALREADY_SUBMITTED` error redirect to `"readout"` the first time the analyst tries to act. Document this reasoning in the task report as a real UX limitation of the current API (no "get my confirmatory setup state" read endpoint exists) rather than silently working around it with a guess.

- [ ] **Step 2: Setup phase JSX**

For each entry in `permitted`, render a lot picker (from `entry.availableLots`) and an incubator picker (fetch `getEligibleIncubators(testOrderId, entry.stepMediaId)` per entry, in parallel, on mount — not lazily per-row-click). Show `entry.expectedAppearance` next to each medium's name (never invented; if null, show an inline warning for that specific medium, not a blocking error for the whole panel — other media on the panel may still have valid appearance data). **The analyst may only select from `permitted` entries — there is no "add another medium" control; the whole panel enforces "only what the step template permits" simply by never offering anything else.** Let the analyst check which media they actually plated (a checkbox per entry — not all permitted media are necessarily used every run) — only checked entries with a completed lot+incubator selection go into the `selections` array sent to `submitConfirmatorySetup`. One shared incubation-duration field for the whole setup (all selections share `incubationStartUtc`/`incubationEndUtc`).

On submit: call `TestWorkflowService.submitConfirmatorySetup(testOrderId, step.stepName, selections, startUtc, endUtc)`. On success, set `phase = "readout"` locally (do NOT call the parent `onSubmitted` yet — the step is not complete). On `CONFIRMATORY_SETUP_ALREADY_SUBMITTED`, also set `phase = "readout"` per Step 1's handling.

- [ ] **Step 3: Read-out phase JSX**

Only for the media the analyst actually selected in setup — track which `stepMediaId`s were submitted (from the setup response or from local state kept across the phase transition; the `submitConfirmatorySetup` response is a `StepResultDto`, which does not echo back the selections, so **keep the selections list in local component state** across the phase transition rather than trying to re-derive it from the API). For each selected medium, show its `expectedAppearance` again (same never-invented rule) beside a three-state observation radio group, keyed by `materialId` (not `stepMediaId` — the backend's `submit-confirmatory-observations` request is keyed by `materialId`, verify this matches what you send).

On submit: call `TestWorkflowService.submitConfirmatoryObservations(testOrderId, step.stepName, observations)`, which returns `ConfirmatoryOutcomeDto`. If `analystDecisionRequired`, set `phase = "decision"`. Otherwise (an `Inconclusive` result never has `analystDecisionRequired: true` — verify this against the actual `RecordAnalystDecisionAsync`/`SubmitConfirmatoryObservationsAsync` logic before assuming it, don't just infer it from the DTO shape), call the parent `onSubmitted(...)` to let `PathogenStepDialog` re-fetch and land on whatever state (including the Inconclusive-terminal state Task 5 built) the refreshed `current-step` response now implies.

- [ ] **Step 4: Decision phase JSX**

Two buttons: "Submit as Detected (skip biochemical)" and "Proceed to Biochemical Test". Above them, a plain-language warning (per the original prompt's explicit instruction): *"Submitting as Detected without biochemical confirmation will be flagged for the reviewer."* On click, call `TestWorkflowService.recordAnalystDecision(testOrderId, "SubmitAsDetected" | "ProceedToBiochemical")`, then call the parent `onSubmitted(result)` — both decisions complete this panel's involvement (either the workflow finalizes, or the biochemical step unlocks and `PathogenStepDialog`'s next render will show `BiochemicalTestPanel` from Task 8).

- [ ] **Step 5: In `PathogenStepDialog.tsx`, replace the `ConfirmatoryPlating` ternary arm**

```tsx
) : step.stepType === "ConfirmatoryPlating" ? (
  <ConfirmatoryPlatingPanel testOrderId={testOrderId} step={step} onSubmitted={handleSubmitted} />
```

- [ ] **Step 6: Verify**

```bash
cd frontend && npm run build
cd frontend && npm run lint
```

- [ ] **Step 7: Commit**

```bash
git add frontend/src/modules/testingWorkspace/pathogenSteps/ConfirmatoryPlatingPanel.tsx frontend/src/modules/testingWorkspace/PathogenStepDialog.tsx
git commit -m "feat: add confirmatory plating panel with setup, read-out, and analyst decision"
```

---

## Task 8: BiochemicalTestPanel

**Files:**
- Create: `frontend/src/modules/testingWorkspace/pathogenSteps/BiochemicalTestPanel.tsx`
- Modify: `frontend/src/modules/testingWorkspace/PathogenStepDialog.tsx` (final ternary arm)

**Interfaces:**
- Consumes: `TestWorkflowService.submitBiochemical` (Task 2).
- Produces: `BiochemicalTestPanel({ testOrderId, step, onSubmitted })`.

- [ ] **Step 1: Write `BiochemicalTestPanel.tsx`**

A single required multiline free-text field (`biochemicalResultText`) and a submit button. **No attachment UI of any kind** — per the plan's global constraint, `BiochemicalAttachmentId` has no backing entity or upload endpoint; do not build a file picker with nothing to call. Validate non-blank before enabling submit (matches the backend's `BIOCHEMICAL_RESULT_REQUIRED` error, which this client-side check should make unreachable in the common case, but still handle the error code via the shared parser if the server rejects it for any reason).

```tsx
import { useState } from "react";
import { TextField, Button, Stack, Alert, Typography } from "@mui/material";
import { TestWorkflowService } from "../services/TestWorkflowService";
import { TestWorkflowStepDto, StepResultDto } from "../types/testWorkflowTypes";
import { parseWorkflowError, workflowErrorDisplayMessage } from "../utils/workflowErrors";

interface Props { testOrderId: number; step: TestWorkflowStepDto; onSubmitted: (result: StepResultDto) => void; }

export function BiochemicalTestPanel({ testOrderId, step, onSubmitted }: Props) {
  const [text, setText] = useState("");
  const [error, setError] = useState<string | null>(null);

  const submit = async () => {
    setError(null);
    if (!text.trim()) { setError("Enter the biochemical confirmation result."); return; }
    try {
      const result = await TestWorkflowService.submitBiochemical(testOrderId, step.stepName, text.trim());
      onSubmitted(result);
    } catch (e) {
      setError(workflowErrorDisplayMessage(parseWorkflowError(e)));
    }
  };

  return (
    <Stack spacing={1.5}>
      {error && <Alert severity="error">{error}</Alert>}
      <Typography variant="body2" color="text.secondary">
        Record the biochemical confirmation result (e.g. IMViC pattern, API strip result).
      </Typography>
      <TextField multiline minRows={4} label="Biochemical Result" value={text} onChange={(e) => setText(e.target.value)} />
      <Stack direction="row" justifyContent="flex-end">
        <Button variant="contained" disabled={!text.trim()} onClick={submit}>Submit</Button>
      </Stack>
    </Stack>
  );
}
```

- [ ] **Step 2: In `PathogenStepDialog.tsx`, replace the `BiochemicalTest` ternary arm and remove the trailing generic-fallback arm's redundancy** (the final `: (<UnsupportedStepPanel .../>)` else-arm stays as the true catch-all for any `StepType` not explicitly handled — e.g. `PlateCount`, which should never reach this dialog at all since `workflowType === "CountTest"` routes elsewhere in `TestWorkflowDialog`, but the fallback must still exist defensively per the plan's global constraint against silent fallthrough).

```tsx
) : step.stepType === "BiochemicalTest" ? (
  <BiochemicalTestPanel testOrderId={testOrderId} step={step} onSubmitted={handleSubmitted} />
) : (
  <UnsupportedStepPanel stepType={step.stepType} />
)}
```

- [ ] **Step 3: Verify**

```bash
cd frontend && npm run build
cd frontend && npm run lint
```

Expected: succeeds. `PathogenStepDialog`'s step-type switch is now fully real (no remaining placeholder arms). Confirm by reading the final file that every `StepType` union member is reachable through an explicit branch or the final fallback — do this by literally checking off each of the six `StepType` values against the switch, not by assuming.

- [ ] **Step 4: Commit**

```bash
git add frontend/src/modules/testingWorkspace/pathogenSteps/BiochemicalTestPanel.tsx frontend/src/modules/testingWorkspace/PathogenStepDialog.tsx
git commit -m "feat: add biochemical test panel, completing the pathogen step dispatch"
```

---

## Task 9: Test Master step editor rewrite

**Files:**
- Modify: `frontend/src/modules/laboratoryConfiguration/masterDataSimple/TestMasterPage.tsx`

**Interfaces:**
- Consumes: `masterDataOptions.createTestWorkflowStep`/`updateTestWorkflowStep`/`getMaterials`/`getOrganisms` (Task 2, `getOrganisms` already existed).

**Read the full current file (400 lines) before editing anything** — this task changes the `WorkflowStepsSection` component's form/state/validation/table substantially; do not attempt line-level patches against stale line numbers, re-locate every reference by content.

- [ ] **Step 1: Replace the module-level constants**

Remove `STEP_RESULT_TYPES = ["PlateCount", "Growth", "DualGrowth"]`. Add:
```typescript
const STEP_TYPES = ["PlateCount", "BrothEnrichment", "SelectiveBroth", "SelectivePlating", "ConfirmatoryPlating", "BiochemicalTest"];
const STEP_TYPES_REQUIRING_ORGANISM = ["SelectivePlating", "ConfirmatoryPlating"];
const STEP_TYPES_WITH_NO_MEDIA = ["BiochemicalTest"];
```
Keep `WORKFLOW_TYPES` but drop `"DualPlate"`: `const WORKFLOW_TYPES = ["CountTest", "Observation"];`.

- [ ] **Step 2: Rework the step form state**

Replace the `stepResultType`/`plate1DefaultLabel`/`plate2DefaultLabel` fields in the form state (`useState<Record<string, any>>`) with `stepType: "PlateCount"`, `targetOrganismId: null as number | null`, and `stepMedia: [] as { materialId: number | ""; tempMin: string; tempMax: string; isRequired: boolean; displayOrder: number }[]`.

Fetch organisms and materials on mount alongside the existing media-type fetch: `masterDataOptions.getOrganisms()` and `masterDataOptions.getMaterials("DehydratedMedia")`.

**Keep the Media Type selector exactly as it exists today, for every step type** — per the plan header, `mediaTypeId` is still a required field server-side for every `StepType`, including pathogen types. Do not remove or hide it.

- [ ] **Step 3: Rebuild the add/edit form JSX**

Add a `StepType` `Select` (populated from `STEP_TYPES`). Below it, conditionally:
- If `STEP_TYPES_REQUIRING_ORGANISM.includes(form.stepType)`: show a required Organism `Select` (populated from fetched organisms, `option.scientificName`), bound to `form.targetOrganismId`.
- If `STEP_TYPES_WITH_NO_MEDIA.includes(form.stepType)`: hide the `StepMedia` editor entirely (send `stepMedia: []`).
- Otherwise: show a `StepMedia` list editor — a small repeatable-row UI (Material `Select` from fetched materials, TempMin/TempMax number fields, IsRequired checkbox, DisplayOrder number field, a delete-row icon button, an "Add Medium" button). For `BrothEnrichment`/`SelectiveBroth`/`SelectivePlating`, cap the list at exactly one row client-side (add a note: "This step type allows exactly one medium" and disable "Add Medium" once one row exists) — the server enforces this too, but give the analyst immediate feedback per the plan's instruction to mirror backend rules client-side while treating the server as authoritative. For `ConfirmatoryPlating`, allow multiple rows, and force `isRequired: false` on every row (don't show the checkbox at all for this step type — every confirmatory medium is analyst-selectable by definition; sending `isRequired: true` for any row would fail the server's validator).

- [ ] **Step 4: Client-side validation mirroring the six backend rules — call this before every save, treat the server's response as authoritative on any mismatch**

```typescript
function validateStepForm(form: StepFormState): string | null {
  const isBroth = form.stepType === "BrothEnrichment" || form.stepType === "SelectiveBroth";
  const isSelectivePlating = form.stepType === "SelectivePlating";
  const isConfirmatory = form.stepType === "ConfirmatoryPlating";
  const isBiochemical = form.stepType === "BiochemicalTest";

  if (isBroth && (form.stepMedia.length !== 1 || !form.stepMedia[0].isRequired))
    return "A broth step must have exactly one assigned medium, marked as required.";
  if (isBroth && form.targetOrganismId)
    return "A broth step must not target an organism.";
  if (isSelectivePlating && (form.stepMedia.length !== 1 || !form.stepMedia[0].isRequired))
    return "A selective plating step must have exactly one assigned medium, marked as required.";
  if (isSelectivePlating && !form.targetOrganismId)
    return "A selective plating step must target an organism.";
  if (isConfirmatory && form.stepMedia.length === 0)
    return "A confirmatory plating step must have at least one permitted medium.";
  if (isConfirmatory && !form.targetOrganismId)
    return "A confirmatory plating step must target an organism.";
  if (isBiochemical && form.stepMedia.length > 0)
    return "A biochemical test step must have no assigned media.";
  if (isBiochemical && form.targetOrganismId)
    return "A biochemical test step must not target an organism.";
  for (const m of form.stepMedia) {
    if (m.materialId === "" ) return "Every medium row needs a selected material.";
    if (Number(m.tempMin) >= Number(m.tempMax)) return "Every medium's minimum temperature must be below its maximum.";
  }
  const materialIds = form.stepMedia.map((m) => m.materialId);
  if (new Set(materialIds).size !== materialIds.length) return "The same medium cannot be assigned to this step more than once.";
  return null;
}
```

Call this in `saveStep` before the API call; on a non-null result, `setError(result); return;` without calling the server.

- [ ] **Step 5: Update the save payload**

```typescript
const payload = {
  stepName: form.stepName, mediaTypeId: Number(form.mediaTypeId),
  incubationMinHours: Number(form.incubationMinHours) || 0, incubationMaxHours: Number(form.incubationMaxHours) || 0,
  temperatureMin: Number(form.temperatureMin) || 0, temperatureMax: Number(form.temperatureMax) || 0,
  isFinalStep: !!form.isFinalStep, stepType: form.stepType, targetOrganismId: form.targetOrganismId,
  stepMedia: form.stepMedia.map((m, i) => ({
    materialId: Number(m.materialId), tempMin: Number(m.tempMin), tempMax: Number(m.tempMax),
    isRequired: form.stepType === "ConfirmatoryPlating" ? false : !!m.isRequired, displayOrder: i
  }))
};
```

- [ ] **Step 6: Update the step list table**

Replace the "Result Type"/"Plate Labels" columns with "Step Type" and "Media" (rendering `stepMedia.map(m => m.materialName).join(", ")` or an em-dash if empty) and "Organism" (`targetOrganism?.name ?? "—"`).

**Add the missing-configuration indicator required by the original prompt:** for any step whose `stepType` is one of `STEP_TYPES_REQUIRING_ORGANISM` and `targetOrganismId` is null, OR whose `stepType` is not `BiochemicalTest`/`PlateCount` and `stepMedia.length === 0`, render a visible warning chip/icon in that row (e.g. a small MUI `Chip` reading "Needs configuration" in a warning color, or a `WarningAmberIcon` with a tooltip) — this is specifically for the migration-inherited templates that have no `TargetOrganismId`/`StepMedia` yet (per the plan header, the migration could not backfill these) so they are visibly flagged rather than silently failing the first time an analyst tries to run them.

- [ ] **Step 7: Verify**

```bash
cd frontend && npm run build
cd frontend && npm run lint
```

Expected: **succeeds with zero TypeScript errors across the whole project** — this is the last file with any known-broken reference from the original list, so a clean build here means every file in the original "known to be broken" list plus every file this plan added compiles.

- [ ] **Step 8: Commit**

```bash
git add frontend/src/modules/laboratoryConfiguration/masterDataSimple/TestMasterPage.tsx
git commit -m "feat: rebuild Test Master step editor for StepType/organism/StepMedia"
```

---

## Task 10: Downstream readers — sampleSummaryTypes, SampleSummaryDialog, SampleReportPage

**Files:**
- Modify: `frontend/src/modules/testingWorkspace/types/sampleSummaryTypes.ts`
- Modify: `frontend/src/modules/testingWorkspace/SampleSummaryDialog.tsx`
- Modify: `frontend/src/modules/testingWorkspace/SampleReportPage.tsx`

**Do NOT modify** `MediaEvaluationPage.tsx`, `mediaSummaryTypes.ts`, or `MediaReportPage.tsx` — see the plan header's scope correction. Their `growthObserved` usage belongs to the unrelated, untouched Media Evaluation domain.

**Backend context (already shipped, verified, reviewed on this branch):** `PathogenObservationDetailDto.GrowthObserved` (bool) has been replaced with `PathogenObservationDetailDto.Observation` (string — one of `"NoGrowth"`, `"GrowthNonConforming"`, `"GrowthConforming"`), serialized as `observation` (camelCase) in JSON. This task is the frontend half of that already-completed backend fix — until this task lands, the frontend's stale `growthObserved: boolean` type reads `undefined` at runtime and silently renders every pathogen observation as "not detected", which is a live regression on this branch right now. Prioritize this task's correctness accordingly.

- [ ] **Step 1: `sampleSummaryTypes.ts`** — find `PathogenObservationDetail` (around line 42) and change `growthObserved: boolean;` to `observation: "NoGrowth" | "GrowthNonConforming" | "GrowthConforming";`.

- [ ] **Step 2: `SampleSummaryDialog.tsx`** (around line 297) — the `<Field label="Growth Observed" value={p.growthObserved ? "Yes" : "No"} />` must become a three-state-aware render. Do not just swap in the raw enum string for a lab analyst to read — write a small local label map (same pattern as `masterDataOptions.ts`'s `mediaClassLabel`) mirroring the wording used in the backend's `ReportDocumentMapper.ObservationText` helper (already shipped — read it for the exact wording you should match, so the on-screen dialog and the archived PDF describe the same observation the same way): `"NoGrowth"` → "No growth", `"GrowthNonConforming"` → "Growth observed — does not match target organism", `"GrowthConforming"` → "Growth observed — matches target organism".

- [ ] **Step 3: `SampleReportPage.tsx`** — two sites:
  - (~line 310) `const detected = test.pathogenObservations.some((p) => p.growthObserved);` → `const detected = test.pathogenObservations.some((p) => p.observation === "GrowthConforming");`. This is the exact fix that closes the false-positive/false-negative bug on the frontend side — only `GrowthConforming` may contribute to a Detected/danger-tone determination, matching the already-shipped backend `ReportDocumentMapper.cs` fix exactly. Get this line right; a `!== "NoGrowth"` inversion here would silently reintroduce the original backend bug on the frontend.
  - (~line 421) `p.growthObserved ? "Growth observed" : "No growth"` → use the same three-state label mapping as Step 2 (extract it to a small shared helper in this task if both files need it, rather than duplicating the map inline in both places — a shared `pathogenObservationLabel(observation)` function in `sampleSummaryTypes.ts` or a small new `frontend/src/modules/testingWorkspace/utils/pathogenObservationLabel.ts` is appropriate; pick whichever fits the existing file organization better after reading both files).

- [ ] **Step 4: Verify**

```bash
cd frontend && npm run build
cd frontend && npm run lint
```

- [ ] **Step 5: Commit**

```bash
git add frontend/src/modules/testingWorkspace/types/sampleSummaryTypes.ts frontend/src/modules/testingWorkspace/SampleSummaryDialog.tsx frontend/src/modules/testingWorkspace/SampleReportPage.tsx
git commit -m "fix: consume the three-state pathogen observation in the summary/report readers"
```

---

## Task 11: Verification sweep and report

**Files:** none modified — this task only verifies and reports.

- [ ] **Step 1: Full clean build**

```bash
cd frontend && rm -rf node_modules/.vite dist && npm run build
```

Capture the real, complete output. Expected: 0 TypeScript errors, successful Vite build.

- [ ] **Step 2: Lint**

```bash
cd frontend && npm run lint
```

Capture the real, complete output. Any pre-existing warning in a file this plan did not touch is not this task's responsibility; any new warning/error in a file this plan modified or created must be fixed before this task closes.

- [ ] **Step 3: Grep sweep for anything the plan's own file list might have missed**

```bash
cd frontend && grep -rn "stepResultType\|plate1DefaultLabel\|plate2DefaultLabel\|growthObserved\|isDualPlate\|DualPlate" src/ --include=*.ts --include=*.tsx
```

Every remaining hit must be inside `MediaEvaluationPage.tsx`, `mediaSummaryTypes.ts`, or `MediaReportPage.tsx` (the unrelated Media Evaluation domain, deliberately untouched) — confirm this explicitly, file by file. If any hit appears anywhere else, that is a real gap this plan did not close; report it, do not silently patch it outside a reviewed task.

- [ ] **Step 4: Manual smoke check of the dev server (best-effort — no test framework exists to automate this)**

```bash
cd frontend && npm run dev
```

Confirm the dev server starts without a runtime error overlay. A full manual click-through against a live backend/database is out of scope for this task (no seeded pathogen test order is guaranteed to exist in the environment this task runs in) — note this limitation explicitly rather than fabricating a manual-test result.

- [ ] **Step 5: Write the report**

Produce a short report (in the task's return message, not a new file, unless the plan's execution skill requires one) covering:
- Every file changed across all 10 tasks.
- The three endpoint/behavior discrepancies already identified in this plan's header and inline notes — restate them plainly so they're not lost in a 400-line diff: (a) `mediaTypeId` is still required for every `StepType` including pathogen types — a backend quirk, not fixable from the frontend; (b) `submit-selective-plating` has no field to receive the required observed-appearance note the original spec asked for — the note is a client-side confirmation gate only, not persisted; (c) `permitted-confirmatory-media` is reused for single-medium broth/selective-plating steps despite its confirmatory-sounding name, because `current-step` never returns `stepMedia`.
- The scope correction: `MediaEvaluationPage.tsx`/`mediaSummaryTypes.ts`/`MediaReportPage.tsx` were confirmed NOT broken by this refactor and were not touched.
- The reviewer biochemical-decision gap: the service method exists (Task 2) but no frontend screen calls it — there is no existing reviewer UI in this codebase to wire it into (confirmed: no `SampleReview*`/`Review*` module beyond a 5-line stub), and building one was not described anywhere in Parts 2-7 of the original task. Flag this as a follow-up decision for the human partner, not a silent omission.
- Confirmation that the already-shipped backend fix (`PathogenObservationDetailDto.Observation`) and this plan's Task 10 are a matched pair and must ship together — the branch was in a regressed state (false-negative on the frontend) between the backend fix landing and Task 10 landing.

---

## Self-Review Notes (for the plan author, before dispatch)

- **Spec coverage:** Part 1 (types/service) → Tasks 1-2. Part 2 (step-type dispatch, all 5 sub-cases) → Tasks 4-8. Part 3 (error codes) → Task 1's parser, consumed throughout Tasks 5-9. Part 4 (Inconclusive terminal) → Task 5. Part 5 (incubation lock, Option B) → Task 3, consumed in Task 5. Part 6 (Test Master editor) → Task 9. Part 7 (downstream readers) → Task 10, correctly narrowed to the 3 files actually broken.
- **Placeholder scan:** every task has real code, not prose-only instructions, except where a genuine backend limitation makes a described feature (the selective-plating note field; the reviewer decision screen) impossible to build honestly — those are called out explicitly as reported gaps, not silently implemented as broken/fake features.
- **Type consistency:** `TestWorkflowStepDto`, `CurrentStepResponse`, `StepResultDto`, `ConfirmatoryOutcomeDto`, `PermittedConfirmatoryMediaEntry`/`Response`, `EligibleIncubatorsResponse`, `GrowthObservation`, `StepType`, `AnalystDecision`, `WorkflowErrorCode` are each defined exactly once (Task 1) and referenced by the same name in every later task — verified by re-reading each task's code blocks against Task 1's definitions before finalizing this document.
