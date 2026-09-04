import { apiClient } from "../../../services/apiClient";
import type {
  DocumentReviewTaskDto,
  DocumentReviewFindingDto,
  SubmitForReviewRequest,
  AddReviewFindingRequest,
  RespondToFindingRequest,
  VerifyFindingRequest,
  ReviewDecisionRequest
} from "../types/documentControlTypes";

export const documentReviewService = {
  submitForReview: async (
    revisionId: number,
    request: SubmitForReviewRequest
  ): Promise<DocumentReviewTaskDto> => {
    const res = await apiClient.post(
      `/document-control/revisions/${revisionId}/submit-review`,
      request
    );
    return res.data.data;
  },

  getReviewTaskById: async (id: number): Promise<DocumentReviewTaskDto> => {
    const res = await apiClient.get(`/document-control/reviews/${id}`);
    return res.data.data;
  },

  getReviewTasksByRevisionId: async (
    revisionId: number
  ): Promise<DocumentReviewTaskDto[]> => {
    const res = await apiClient.get(
      `/document-control/revisions/${revisionId}/review-tasks`
    );
    return res.data.data;
  },

  getMyReviewTasks: async (): Promise<DocumentReviewTaskDto[]> => {
    const res = await apiClient.get("/document-control/reviews/my-tasks");
    return res.data.data;
  },

  addReviewFinding: async (
    taskId: number,
    request: AddReviewFindingRequest
  ): Promise<DocumentReviewFindingDto> => {
    const res = await apiClient.post(
      `/document-control/reviews/${taskId}/findings`,
      request
    );
    return res.data.data;
  },

  respondToFinding: async (
    findingId: number,
    request: RespondToFindingRequest
  ): Promise<DocumentReviewFindingDto> => {
    const res = await apiClient.post(
      `/document-control/findings/${findingId}/respond`,
      request
    );
    return res.data.data;
  },

  verifyFinding: async (
    findingId: number,
    request: VerifyFindingRequest
  ): Promise<DocumentReviewFindingDto> => {
    const res = await apiClient.post(
      `/document-control/findings/${findingId}/verify`,
      request
    );
    return res.data.data;
  },

  resolveFinding: async (findingId: number): Promise<DocumentReviewFindingDto> => {
    const res = await apiClient.post(
      `/document-control/findings/${findingId}/resolve`
    );
    return res.data.data;
  },

  decideReview: async (
    taskId: number,
    request: ReviewDecisionRequest
  ): Promise<DocumentReviewTaskDto> => {
    const res = await apiClient.post(
      `/document-control/reviews/${taskId}/decide`,
      request
    );
    return res.data.data;
  }
};
