import { apiClient } from "../../../services/apiClient";

export const TestWorkflowService = {
  getCurrentStep: (testOrderId: number) =>
    apiClient.get(`/test-workflow/${testOrderId}/current-step`).then((r) => r.data.data),
  selectMedia: (testOrderId: number, stepName: string, mediaLotId: number, incubatorId: number) =>
    apiClient.post(`/test-workflow/${testOrderId}/select-media`, { stepName, mediaLotId, incubatorId }).then((r) => r.data.data),
  recordResult: (testOrderId: number, payload: Record<string, unknown>) =>
    apiClient.post(`/test-workflow/${testOrderId}/record-result`, payload).then((r) => r.data.data),
  getLocations: (testOrderId: number) =>
    apiClient.get(`/test-workflow/${testOrderId}/locations`).then((r) => r.data.data),
  closeIncubationWindow: (testOrderId: number) =>
    apiClient.post(`/test-workflow/${testOrderId}/close-incubation-window`).then((r) => r.data.data),
  recordBatchPathogenResults: (
    testOrderId: number,
    locations: { sampleLocationId: number; growthObserved?: boolean; plate1GrowthObserved?: boolean; plate2GrowthObserved?: boolean }[]
  ) => apiClient.post(`/test-workflow/${testOrderId}/batch-pathogen-results`, { locations }).then((r) => r.data.data),
  recordBatchResults: (testOrderId: number, dilutionFactor: number, locations: { sampleLocationId: number; cfuResult: number }[]) =>
    apiClient.post(`/test-workflow/${testOrderId}/batch-results`, { dilutionFactor, locations }).then((r) => r.data.data)
};
