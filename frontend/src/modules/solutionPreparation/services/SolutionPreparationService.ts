import { apiClient } from "../../../services/apiClient";
import type {
  SolutionType,
  SolutionPreparationStatus,
  SolutionPreparationListItem,
  SolutionPreparationResponse,
  LotOption,
  StartPreparationRequest,
  SavePreparationRequest,
  CompletePreparationRequest
} from "../types";

interface ApiResponse<T> {
  success: boolean;
  data: T;
  message?: string;
}

export const SolutionPreparationService = {
  getAll: async (
    status?: SolutionPreparationStatus,
    type?: SolutionType
  ): Promise<SolutionPreparationListItem[]> => {
    const params: Record<string, string> = {};
    if (status) params.status = status;
    if (type) params.type = type;
    const res = await apiClient.get<ApiResponse<SolutionPreparationListItem[]>>(
      "/solution-preparations",
      { params }
    );
    const data = res.data?.data;
    return Array.isArray(data) ? data : [];
  },

  getById: async (id: number): Promise<SolutionPreparationResponse> => {
    const res = await apiClient.get<ApiResponse<SolutionPreparationResponse>>(
      `/solution-preparations/${id}`
    );
    return res.data?.data;
  },

  getAvailable: async (
    solutionMasterId: number,
    hplcMethodId?: number | null
  ): Promise<SolutionPreparationListItem[]> => {
    const params: Record<string, number> = { solutionMasterId };
    if (hplcMethodId) params.hplcMethodId = hplcMethodId;
    const res = await apiClient.get<ApiResponse<SolutionPreparationListItem[]>>(
      "/solution-preparations/available",
      { params }
    );
    const data = res.data?.data;
    return Array.isArray(data) ? data : [];
  },

  getByLot: async (materialId: number): Promise<SolutionPreparationListItem[]> => {
    const res = await apiClient.get<ApiResponse<SolutionPreparationListItem[]>>(
      `/solution-preparations/by-lot/${materialId}`
    );
    const data = res.data?.data;
    return Array.isArray(data) ? data : [];
  },

  getLotOptions: async (
    id: number,
    componentId: number
  ): Promise<LotOption[]> => {
    const res = await apiClient.get<ApiResponse<LotOption[]>>(
      `/solution-preparations/${id}/components/${componentId}/lots`
    );
    const data = res.data?.data;
    return Array.isArray(data) ? data : [];
  },

  start: async (
    req: StartPreparationRequest
  ): Promise<SolutionPreparationResponse> => {
    const res = await apiClient.post<ApiResponse<SolutionPreparationResponse>>(
      "/solution-preparations",
      req
    );
    return res.data?.data;
  },

  save: async (
    id: number,
    req: SavePreparationRequest
  ): Promise<SolutionPreparationResponse> => {
    const res = await apiClient.put<ApiResponse<SolutionPreparationResponse>>(
      `/solution-preparations/${id}`,
      req
    );
    return res.data?.data;
  },

  complete: async (
    id: number,
    req: CompletePreparationRequest
  ): Promise<SolutionPreparationResponse> => {
    const res = await apiClient.post<ApiResponse<SolutionPreparationResponse>>(
      `/solution-preparations/${id}/complete`,
      req
    );
    return res.data?.data;
  },

  cancel: async (
    id: number,
    reason: string
  ): Promise<SolutionPreparationResponse> => {
    const res = await apiClient.post<ApiResponse<SolutionPreparationResponse>>(
      `/solution-preparations/${id}/cancel`,
      { reason }
    );
    return res.data?.data;
  },

  discard: async (
    id: number,
    reason: string
  ): Promise<SolutionPreparationResponse> => {
    const res = await apiClient.post<ApiResponse<SolutionPreparationResponse>>(
      `/solution-preparations/${id}/discard`,
      { reason }
    );
    return res.data?.data;
  }
};
