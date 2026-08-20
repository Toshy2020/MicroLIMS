import { apiClient } from "../../../services/apiClient";
import {
  PathogenTestingSessionDto,
  SharedTsbStateDto,
  StartSharedTsbRequest,
  SaveResultMatrixRequest,
  SavePrimaryObservationsRequest,
  EligibleLocationForConfirmationDto,
  BatchConfirmatorySetupRequest,
  SaveBatchConfirmatoryPlateReadingsRequest
} from "../types/pathogenSessionTypes";

export const PathogenSessionService = {
  getSession: (sampleId: number): Promise<PathogenTestingSessionDto> =>
    apiClient.get(`/pathogen-session/${sampleId}`).then((r) => r.data.data),

  startSharedTsb: (sampleId: number, request: StartSharedTsbRequest): Promise<SharedTsbStateDto> =>
    apiClient.post(`/pathogen-session/${sampleId}/start-tsb`, request).then((r) => r.data.data),

  savePrimaryObservations: (sampleId: number, request: SavePrimaryObservationsRequest): Promise<PathogenTestingSessionDto> =>
    apiClient.post(`/pathogen-session/${sampleId}/save-primary-observations`, request).then((r) => r.data.data),

  getEligibleConfirmations: (sampleId: number, testOrderId?: number): Promise<EligibleLocationForConfirmationDto[]> => {
    const url = testOrderId
      ? `/pathogen-session/${sampleId}/eligible-confirmations?testOrderId=${testOrderId}`
      : `/pathogen-session/${sampleId}/eligible-confirmations`;
    return apiClient.get(url).then((r) => r.data.data);
  },

  startConfirmatorySetup: (sampleId: number, request: BatchConfirmatorySetupRequest): Promise<PathogenTestingSessionDto> =>
    apiClient.post(`/pathogen-session/${sampleId}/start-confirmatory-setup`, request).then((r) => r.data.data),

  saveConfirmatoryReadings: (sampleId: number, request: SaveBatchConfirmatoryPlateReadingsRequest): Promise<PathogenTestingSessionDto> =>
    apiClient.post(`/pathogen-session/${sampleId}/save-confirmatory-readings`, request).then((r) => r.data.data),

  saveResultMatrix: (sampleId: number, request: SaveResultMatrixRequest): Promise<PathogenTestingSessionDto> =>
    apiClient.post(`/pathogen-session/${sampleId}/save-matrix`, request).then((r) => r.data.data),

  completeSession: (sampleId: number): Promise<PathogenTestingSessionDto> =>
    apiClient.post(`/pathogen-session/${sampleId}/complete`).then((r) => r.data.data),

  resetSession: (sampleId: number, reason?: string): Promise<PathogenTestingSessionDto> =>
    apiClient.post(`/pathogen-session/${sampleId}/reset`, { reason }).then((r) => r.data.data)
};
