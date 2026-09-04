import axios from "axios";
import type {
  PeriodicReviewTaskDto,
  PeriodicReviewFindingDto,
  CreatePeriodicReviewFindingRequest,
  CompletePeriodicReviewRequest,
  AssignPeriodicReviewerRequest,
  PeriodicReviewWorkspaceDto,
  PeriodicReviewGenerationResultDto
} from "../types/documentControlTypes";

const API_BASE = "/api/document-control/periodic-reviews";

export const periodicReviewService = {
  async getTasks(params?: {
    masterId?: number;
    revisionId?: number;
    status?: string;
    reviewerUserId?: number;
  }): Promise<PeriodicReviewTaskDto[]> {
    const res = await axios.get<PeriodicReviewTaskDto[]>(`${API_BASE}/tasks`, { params });
    return res.data;
  },

  async getTaskById(id: number): Promise<PeriodicReviewTaskDto> {
    const res = await axios.get<PeriodicReviewTaskDto>(`${API_BASE}/tasks/${id}`);
    return res.data;
  },

  async getWorkspace(id: number): Promise<PeriodicReviewWorkspaceDto> {
    const res = await axios.get<PeriodicReviewWorkspaceDto>(`${API_BASE}/tasks/${id}/workspace`);
    return res.data;
  },

  async addFinding(id: number, req: CreatePeriodicReviewFindingRequest): Promise<PeriodicReviewFindingDto> {
    const res = await axios.post<PeriodicReviewFindingDto>(`${API_BASE}/tasks/${id}/findings`, req);
    return res.data;
  },

  async resolveFinding(id: number, findingId: number): Promise<PeriodicReviewFindingDto> {
    const res = await axios.put<PeriodicReviewFindingDto>(`${API_BASE}/tasks/${id}/findings/${findingId}/resolve`);
    return res.data;
  },

  async assignReviewer(id: number, req: AssignPeriodicReviewerRequest): Promise<PeriodicReviewTaskDto> {
    const res = await axios.post<PeriodicReviewTaskDto>(`${API_BASE}/tasks/${id}/assign`, req);
    return res.data;
  },

  async completeReview(id: number, req: CompletePeriodicReviewRequest): Promise<PeriodicReviewTaskDto> {
    const res = await axios.post<PeriodicReviewTaskDto>(`${API_BASE}/tasks/${id}/complete`, req);
    return res.data;
  },

  async getMasterHistory(masterId: number): Promise<PeriodicReviewTaskDto[]> {
    const res = await axios.get<PeriodicReviewTaskDto[]>(`${API_BASE}/masters/${masterId}/history`);
    return res.data;
  },

  async triggerGeneration(): Promise<PeriodicReviewGenerationResultDto> {
    const res = await axios.post<PeriodicReviewGenerationResultDto>(`${API_BASE}/trigger-worker`);
    return res.data;
  }
};
