import { apiClient } from "../../../services/apiClient";
import {
  CurrentStepResponse, StepResultDto, ConfirmatoryOutcomeDto,
  PermittedConfirmatoryMediaResponse, EligibleIncubatorsResponse, AnalystDecision,
  GrowthObservation, SiblingPathogenOrder,
  ActionableGroupsResponse, BatchSelectMediaRequest, BatchSelectMediaResponse,
  TestWorkflowResult
} from "../types/testWorkflowTypes";

export const TestWorkflowService = {
  getCurrentStep: (testOrderId: number): Promise<CurrentStepResponse> =>
    apiClient.get(`/test-workflow/${testOrderId}/current-step`).then((r) => r.data.data),

  getSiblingPathogenOrders: (testOrderId: number): Promise<SiblingPathogenOrder[]> =>
    apiClient.get(`/test-workflow/${testOrderId}/sibling-pathogen-orders`).then((r) => r.data.data),

  getEligibleIncubators: (testOrderId: number, stepMediaId: number): Promise<EligibleIncubatorsResponse> =>
    apiClient.get(`/test-workflow/${testOrderId}/eligible-incubators/${stepMediaId}`).then((r) => r.data.data),

  getPermittedConfirmatoryMedia: (testOrderId: number, stepName: string): Promise<PermittedConfirmatoryMediaResponse> =>
    apiClient.get(`/test-workflow/${testOrderId}/permitted-confirmatory-media`, { params: { stepName } }).then((r) => r.data.data),

  // CountTest only - the pathogen dual-plate fields this used to carry
  // (plate2MediaId, plate1Label, plate2Label) no longer exist server-side.
  selectMedia: (testOrderId: number, stepName: string, mediaLotId: number, incubatorId: number) =>
    apiClient.post(`/test-workflow/${testOrderId}/select-media`, { stepName, mediaLotId, incubatorId }).then((r) => r.data.data),

  startStage2Incubation: (testOrderId: number, stepName: string, incubatorId: number) =>
    apiClient.post(`/test-workflow/${testOrderId}/start-stage-2-incubation`, { stepName, incubatorId }).then((r) => r.data.data),

  // CountTest only - record-result now rejects any non-PlateCount step
  // server-side. Every pathogen step goes through the Submit* methods below.
  recordResult: (testOrderId: number, payload: { stepName: string; plateReadings?: number[]; rawPlateReadings?: string[]; dilutionFactor: number }) =>
    apiClient.post(`/test-workflow/${testOrderId}/record-result`, payload).then((r) => r.data.data),

  recordCountResult: (testOrderId: number, payload: { stepName: string; rawPlateReadings: string[]; dilutionFactor: number; dilutionFactorOverrideNote?: string }) =>
    apiClient.post(`/test-workflow/${testOrderId}/record-result`, payload).then((r) => r.data.data),

  // HPLC Assay only. Signed; the server calculates % assay per replicate and
  // the mean, and rejects the call unless a passed suitability run is linked.
  recordHplcResult: (testOrderId: number, payload: { sampleWeightMg: number; sampleDilution: number; sampleAreas: number[]; password: string; comment?: string | null }) =>
    apiClient.post(`/test-workflow/${testOrderId}/record-hplc-result`, payload).then((r) => r.data.data),

  // Elemental Assay (ICP-OES Calibration Curve). Signed; the server calculates
  // per-element recovery / claim / %LC and comparison status.
  recordElementalResult: (
    testOrderId: number,
    payload: {
      unitAmount: number;
      analysedAt: string;
      elements: {
        specificationId: number;
        calibrationRunAnalyteId: number;
        reportedPpm: number;
        overRange: boolean;
        belowLoq: boolean;
      }[];
      password: string;
      comment?: string | null;
    }
  ) =>
    apiClient.post(`/test-workflow/${testOrderId}/record-elemental-result`, payload).then((r) => r.data.data),

  // Numeric measurement (pH, density, viscosity, etc.). Signed.
  recordMeasurementResult: (
    testOrderId: number,
    payload: {
      analysedAt: string;
      equipmentId?: number | null;
      parameters: {
        specificationId: number;
        readings: number[];
      }[];
      password: string;
      comment?: string | null;
    }
  ) =>
    apiClient.post(`/test-workflow/${testOrderId}/record-measurement-result`, payload).then((r) => r.data.data),

  // Gravimetric analysis (loss on drying, ash/residue). Signed.
  recordGravimetricResult: (
    testOrderId: number,
    payload: {
      analysedAt: string;
      equipmentId?: number | null;
      conditions: Record<string, string>;
      parameters: {
        specificationId: number;
        replicates: {
          container?: number | null;
          initial: number;
          final: number;
        }[];
      }[];
      password: string;
      comment?: string | null;
    }
  ) =>
    apiClient.post(`/test-workflow/${testOrderId}/record-gravimetric-result`, payload).then((r) => r.data.data),

  // Qualitative analysis (appearance, ID, odor, etc.). Signed.
  recordQualitativeResult: (
    testOrderId: number,
    payload: {
      analysedAt: string;
      equipmentId?: number | null;
      parameters: {
        specificationId: number;
        conforms: boolean;
        observation?: string | null;
      }[];
      password: string;
      comment?: string | null;
    }
  ) =>
    apiClient.post(`/test-workflow/${testOrderId}/record-qualitative-result`, payload).then((r) => r.data.data),

  // Dissolution analysis (Stage 1). Signed. Returns TestWorkflowResult.
  recordDissolutionResult: (
    testOrderId: number,
    payload: {
      analysedAt: string;
      equipmentId?: number | null;
      conditions: Record<string, string>;
      mediumVolumeMl: number;
      dilutionFactor?: number | null;
      vesselAreas: number[];
      password: string;
      comment?: string | null;
    }
  ): Promise<TestWorkflowResult> =>
    apiClient.post(`/test-workflow/${testOrderId}/record-dissolution-result`, payload).then((r) => r.data.data),

  // Dissolution stage progression (Stage 2 or 3). Signed. Returns TestWorkflowResult.
  recordDissolutionStage: (
    testOrderId: number,
    payload: {
      vesselAreas: number[];
      password: string;
      comment?: string | null;
    }
  ): Promise<TestWorkflowResult> =>
    apiClient.post(`/test-workflow/${testOrderId}/record-dissolution-stage`, payload).then((r) => r.data.data),

  // Disintegration analysis (Stage 1). Signed. Returns TestWorkflowResult.
  recordDisintegrationResult: (
    testOrderId: number,
    payload: {
      analysedAt: string;
      equipmentId?: number | null;
      conditions: Record<string, string>;
      unitMinutes: (number | null)[];
      password: string;
      comment?: string | null;
    }
  ): Promise<TestWorkflowResult> =>
    apiClient.post(`/test-workflow/${testOrderId}/record-disintegration-result`, payload).then((r) => r.data.data),

  // Disintegration stage progression (Stage 2). Signed. Returns TestWorkflowResult.
  recordDisintegrationStage: (
    testOrderId: number,
    payload: {
      unitMinutes: (number | null)[];
      password: string;
      comment?: string | null;
    }
  ): Promise<TestWorkflowResult> =>
    apiClient.post(`/test-workflow/${testOrderId}/record-disintegration-stage`, payload).then((r) => r.data.data),

  getLocations: (testOrderId: number) =>
    apiClient.get(`/test-workflow/${testOrderId}/locations`).then((r) => r.data.data),

  closeIncubationWindow: (testOrderId: number) =>
    apiClient.post(`/test-workflow/${testOrderId}/close-incubation-window`).then((r) => r.data.data),

  // Section Head/System Administrator only (enforced server-side too) -
  // bypasses the minimum-duration wait for the currently open incubation.
  overrideMinimumDuration: (testOrderId: number) =>
    apiClient.post(`/test-workflow/${testOrderId}/override-minimum-duration`).then((r) => r.data.data),

  // Still a single boolean per location - EM/AfterCleaning batch pathogen
  // results never adopted the confirmatory model; there is no dual-plate
  // variant of this endpoint anymore.
  recordBatchPathogenResults: (testOrderId: number, locations: { sampleLocationId: number; growthObserved: boolean }[]) =>
    apiClient.post(`/test-workflow/${testOrderId}/batch-pathogen-results`, { locations }).then((r) => r.data.data),

  // Dilution factor is always 1 server-side (EM/After Cleaning are
  // direct-count categories) - each location submits its own set of raw
  // plate readings, averaged, same shape as recordWaterBatchReadings.
  recordBatchResults: (testOrderId: number, locations: { sampleLocationId: number; readings: number[] }[]) =>
    apiClient.post(`/test-workflow/${testOrderId}/batch-results`, { locations }).then((r) => r.data.data),

  recordWaterBatchReadings: (testOrderId: number, locations: { sampleLocationId: number; readings: number[] }[]) =>
    apiClient.post(`/test-workflow/${testOrderId}/water-batch-readings`, { locations }).then((r) => r.data.data),

  // ---- Pathogen five-stage workflow ----

  submitBroth: (
    testOrderId: number, stepName: string, observation: string | null
  ): Promise<StepResultDto> =>
    apiClient.post(`/test-workflow/${testOrderId}/submit-broth`, {
      stepName, observation
    }).then((r) => r.data.data),

  startSelectivePlatingIncubation: (
    testOrderId: number, stepName: string, mediaLotId: number, equipmentId: number, incubationStartUtc?: string
  ) =>
    apiClient.post(`/test-workflow/${testOrderId}/start-selective-plating-incubation`, {
      stepName, mediaLotId, equipmentId, incubationStartUtc
    }).then((r) => r.data.data),

  submitSelectivePlatingObservation: (
    testOrderId: number, stepName: string, observation: GrowthObservation, observedAppearanceNote?: string
  ): Promise<StepResultDto> =>
    apiClient.post(`/test-workflow/${testOrderId}/submit-selective-plating-observation`, {
      stepName, observation, observedAppearanceNote
    }).then((r) => r.data.data),

  /** @deprecated Use startSelectivePlatingIncubation followed by submitSelectivePlatingObservation */
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

  submitBiochemical: (testOrderId: number, stepName: string, biochemicalResultText: string, organismDetected: boolean): Promise<StepResultDto> =>
    apiClient.post(`/test-workflow/${testOrderId}/submit-biochemical`, {
      stepName, biochemicalResultText, attachmentId: null, organismDetected
    }).then((r) => r.data.data),

  // Reviewer-only. No frontend UI calls this yet (see plan header) - the
  // method exists so a future review screen has something to call.
  recordBiochemicalDecision: (workflowStepResultId: number, approve: boolean, comment: string): Promise<StepResultDto> =>
    apiClient.post(`/test-workflow/results/${workflowStepResultId}/biochemical-decision`, { approve, comment })
      .then((r) => r.data.data),

  // Grouped Test Actions
  getActionableGroups: (params?: { scope?: string; actionType?: string; sampleIds?: string }): Promise<ActionableGroupsResponse> =>
    apiClient.get("/test-workflow/actionable-groups", { params }).then((r) => r.data.data),

  batchSelectMedia: (payload: BatchSelectMediaRequest): Promise<BatchSelectMediaResponse> =>
    apiClient.post("/test-workflow/batch-select-media", payload).then((r) => r.data.data)
};

