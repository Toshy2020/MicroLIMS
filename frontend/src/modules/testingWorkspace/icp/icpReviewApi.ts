import { apiClient } from "../../../services/apiClient";

export type IcpEvidenceContext = "Run" | "Calibration" | "Sample" | string;
export type IcpEvidenceKind = "CalibrationReport" | "SampleReport" | "Other" | string;

export interface IcpEvidenceDto {
  id: number;
  icpRunId: number;
  icpRunSampleId?: number | null;
  context: IcpEvidenceContext;
  kind: IcpEvidenceKind;
  fileName: string;
  contentType: string;
  uploadedByUserId: number;
  uploadedByUserName?: string | null;
  uploadedAt: string;
  isCurrent?: boolean;
  supersedeReason?: string | null;
}

interface ApiResponse<T> {
  success: boolean;
  data: T;
  message?: string;
}

export const IcpReviewApi = {
  getTestOrderEvidence: async (testOrderId: number): Promise<IcpEvidenceDto[]> => {
    const res = await apiClient.get<ApiResponse<IcpEvidenceDto[]>>(
      `/icp-workspace/test-orders/${testOrderId}/evidence`
    );
    return Array.isArray(res.data?.data) ? res.data.data : [];
  },

  downloadEvidence: async (id: number): Promise<{ blob: Blob; contentType: string; fileName: string }> => {
    const res = await apiClient.get(`/icp-workspace/evidence/${id}/file`, {
      responseType: "blob"
    });
    const contentType = (res.headers["content-type"] as string) || "application/pdf";
    const disposition = res.headers["content-disposition"] as string | undefined;
    let fileName = "evidence-file";
    if (disposition) {
      const match = disposition.match(/filename\*?=(?:UTF-8'')?["']?([^"';]+)["']?/i);
      if (match?.[1]) {
        fileName = decodeURIComponent(match[1]);
      }
    }
    const blob = new Blob([res.data], { type: contentType });
    return { blob, contentType, fileName };
  },

  openEvidenceInNewTab: async (id: number): Promise<void> => {
    const { blob } = await IcpReviewApi.downloadEvidence(id);
    const url = window.URL.createObjectURL(blob);
    window.open(url, "_blank");
  }
};
