import { apiClient } from "../../../services/apiClient";
import type {
  WorkingStandardLotDto,
  WorkingStandardQualificationDto,
  EligibleSourceSampleDto,
  CreateQualificationRequest,
  UpdateQualificationRequest,
  WorkingStandardDocumentDto,
  WorkingStandardDocumentKind,
  WorkingStandardSignRequest,
  WorkingStandardReasonRequest,
  WorkingStandardReturnRequest
} from "../types";

interface ApiResponse<T> {
  success: boolean;
  data: T;
  message?: string;
}

export const WorkingStandardService = {
  getLots: async (): Promise<WorkingStandardLotDto[]> => {
    const res = await apiClient.get<ApiResponse<WorkingStandardLotDto[]>>("/working-standards/lots");
    return Array.isArray(res.data?.data) ? res.data.data : [];
  },

  getQualifications: async (): Promise<WorkingStandardQualificationDto[]> => {
    const res = await apiClient.get<ApiResponse<WorkingStandardQualificationDto[]>>("/working-standards/qualifications");
    return Array.isArray(res.data?.data) ? res.data.data : [];
  },

  getQualification: async (id: number): Promise<WorkingStandardQualificationDto> => {
    const res = await apiClient.get<ApiResponse<WorkingStandardQualificationDto>>(`/working-standards/qualifications/${id}`);
    return res.data?.data;
  },

  getEligibleSourceSamples: async (search?: string): Promise<EligibleSourceSampleDto[]> => {
    const params: Record<string, string> = {};
    if (search) params.search = search;
    const res = await apiClient.get<ApiResponse<EligibleSourceSampleDto[]>>(
      "/working-standards/source-samples",
      { params }
    );
    return Array.isArray(res.data?.data) ? res.data.data : [];
  },

  createQualification: async (req: CreateQualificationRequest): Promise<WorkingStandardQualificationDto> => {
    const res = await apiClient.post<ApiResponse<WorkingStandardQualificationDto>>(
      "/working-standards/qualifications",
      req
    );
    return res.data?.data;
  },

  updateDraftQualification: async (
    id: number,
    req: UpdateQualificationRequest
  ): Promise<WorkingStandardQualificationDto> => {
    const res = await apiClient.put<ApiResponse<WorkingStandardQualificationDto>>(
      `/working-standards/qualifications/${id}`,
      req
    );
    return res.data?.data;
  },

  uploadDocument: async (
    id: number,
    file: File,
    kind: WorkingStandardDocumentKind
  ): Promise<WorkingStandardDocumentDto> => {
    const fd = new FormData();
    fd.append("file", file);
    fd.append("kind", kind);

    const res = await apiClient.post<ApiResponse<WorkingStandardDocumentDto>>(
      `/working-standards/qualifications/${id}/documents`,
      fd,
      {
        headers: { "Content-Type": "multipart/form-data" }
      }
    );
    return res.data?.data;
  },

  downloadDocument: async (documentId: number): Promise<{ blob: Blob; contentType: string; fileName: string }> => {
    const res = await apiClient.get(`/working-standards/documents/${documentId}/file`, {
      responseType: "blob"
    });
    const contentType = (res.headers["content-type"] as string) || "application/pdf";
    const disposition = res.headers["content-disposition"] as string | undefined;
    let fileName = "document";
    if (disposition) {
      const match = disposition.match(/filename\*?=(?:UTF-8'')?["']?([^"';]+)["']?/i);
      if (match?.[1]) {
        fileName = decodeURIComponent(match[1]);
      }
    }
    const blob = new Blob([res.data], { type: contentType });
    return { blob, contentType, fileName };
  },

  openDocumentInNewTab: async (documentId: number): Promise<void> => {
    const { blob } = await WorkingStandardService.downloadDocument(documentId);
    const url = window.URL.createObjectURL(blob);
    window.open(url, "_blank");
  },

  review: async (
    id: number,
    req: WorkingStandardSignRequest
  ): Promise<WorkingStandardQualificationDto> => {
    const res = await apiClient.post<ApiResponse<WorkingStandardQualificationDto>>(
      `/working-standards/qualifications/${id}/review`,
      req
    );
    return res.data?.data;
  },

  returnQualification: async (
    id: number,
    req: WorkingStandardReturnRequest
  ): Promise<WorkingStandardQualificationDto> => {
    const res = await apiClient.post<ApiResponse<WorkingStandardQualificationDto>>(
      `/working-standards/qualifications/${id}/return`,
      req
    );
    return res.data?.data;
  },

  rejectAtReview: async (
    id: number,
    req: WorkingStandardReasonRequest
  ): Promise<WorkingStandardQualificationDto> => {
    const res = await apiClient.post<ApiResponse<WorkingStandardQualificationDto>>(
      `/working-standards/qualifications/${id}/reject-review`,
      req
    );
    return res.data?.data;
  },

  approve: async (
    id: number,
    req: WorkingStandardSignRequest
  ): Promise<WorkingStandardQualificationDto> => {
    const res = await apiClient.post<ApiResponse<WorkingStandardQualificationDto>>(
      `/working-standards/qualifications/${id}/approve`,
      req
    );
    return res.data?.data;
  },

  rejectAtApproval: async (
    id: number,
    req: WorkingStandardReasonRequest
  ): Promise<WorkingStandardQualificationDto> => {
    const res = await apiClient.post<ApiResponse<WorkingStandardQualificationDto>>(
      `/working-standards/qualifications/${id}/reject-approval`,
      req
    );
    return res.data?.data;
  }
};
