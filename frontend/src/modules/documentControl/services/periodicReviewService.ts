import { apiClient } from "../../../services/apiClient";
import type {
  PeriodicReviewTaskDto,
  PeriodicReviewFindingDto,
  CreatePeriodicReviewFindingRequest,
  CompletePeriodicReviewRequest,
  AssignPeriodicReviewerRequest,
  PeriodicReviewWorkspaceDto,
  PeriodicReviewGenerationResultDto
} from "../types/documentControlTypes";

const API_BASE = "/document-control/periodic-reviews";

export const periodicReviewService = {
  async getTasks(params?: {
    masterId?: number;
    revisionId?: number;
    status?: string;
    reviewerUserId?: number;
  }): Promise<PeriodicReviewTaskDto[]> {
    const res = await apiClient.get(`${API_BASE}/tasks`, { params });
    const data = res.data;
    if (Array.isArray(data)) return data;
    if (data && Array.isArray(data.data)) return data.data;
    return [];
  },

  async getTaskById(id: number): Promise<PeriodicReviewTaskDto> {
    const res = await apiClient.get(`${API_BASE}/tasks/${id}`);
    const data = res.data;
    return data && data.data ? data.data : data;
  },

  async getWorkspace(id: number): Promise<PeriodicReviewWorkspaceDto> {
    const res = await apiClient.get(`${API_BASE}/tasks/${id}/workspace`);
    const data = res.data;
    return data && data.data ? data.data : data;
  },

  async addFinding(id: number, req: CreatePeriodicReviewFindingRequest): Promise<PeriodicReviewFindingDto> {
    const res = await apiClient.post(`${API_BASE}/tasks/${id}/findings`, req);
    const data = res.data;
    return data && data.data ? data.data : data;
  },

  async resolveFinding(id: number, findingId: number): Promise<PeriodicReviewFindingDto> {
    const res = await apiClient.put(`${API_BASE}/tasks/${id}/findings/${findingId}/resolve`);
    const data = res.data;
    return data && data.data ? data.data : data;
  },

  async assignReviewer(id: number, req: AssignPeriodicReviewerRequest): Promise<PeriodicReviewTaskDto> {
    const res = await apiClient.post(`${API_BASE}/tasks/${id}/assign`, req);
    const data = res.data;
    return data && data.data ? data.data : data;
  },

  async completeReview(id: number, req: CompletePeriodicReviewRequest): Promise<PeriodicReviewTaskDto> {
    const res = await apiClient.post(`${API_BASE}/tasks/${id}/complete`, req);
    const data = res.data;
    return data && data.data ? data.data : data;
  },

  async getMasterHistory(masterId: number): Promise<PeriodicReviewTaskDto[]> {
    const res = await apiClient.get(`${API_BASE}/masters/${masterId}/history`);
    const data = res.data;
    if (Array.isArray(data)) return data;
    if (data && Array.isArray(data.data)) return data.data;
    return [];
  },

  async triggerGeneration(): Promise<PeriodicReviewGenerationResultDto> {
    const res = await apiClient.post(`${API_BASE}/trigger-worker`);
    const data = res.data;
    return data && data.data ? data.data : data;
  }
};
