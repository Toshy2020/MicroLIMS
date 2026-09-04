import { apiClient } from "../../../services/apiClient";
import type {
  AcknowledgementPresentationDto,
  AcknowledgementSubmissionRequest,
  AcknowledgementResultDto,
  ReadingProgressUpdateRequest,
  ReadingProgressResultDto
} from "../types/acknowledgementTypes";

export const documentAcknowledgementService = {
  getAcknowledgementContext: async (
    assignmentId: number
  ): Promise<AcknowledgementPresentationDto> => {
    const res = await apiClient.get(
      `/document-control/acknowledgements/assignments/${assignmentId}/context`
    );
    return res.data.data;
  },

  submitAcknowledgement: async (
    assignmentId: number,
    request: AcknowledgementSubmissionRequest
  ): Promise<AcknowledgementResultDto> => {
    const res = await apiClient.post(
      `/document-control/acknowledgements/assignments/${assignmentId}/submit`,
      request
    );
    return res.data.data;
  },

  recordReadingProgress: async (
    assignmentId: number,
    request: ReadingProgressUpdateRequest
  ): Promise<ReadingProgressResultDto> => {
    const res = await apiClient.post(
      `/document-control/acknowledgements/assignments/${assignmentId}/reading-progress`,
      request
    );
    return res.data.data;
  },

  getAcknowledgementStatus: async (
    assignmentId: number
  ): Promise<AcknowledgementResultDto | null> => {
    const res = await apiClient.get(
      `/document-control/acknowledgements/assignments/${assignmentId}/status`
    );
    return res.data.data;
  }
};
