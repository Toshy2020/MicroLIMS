import { apiClient } from "../../../services/apiClient";
import type {
  IcpInstrumentDto,
  IcpMethodOptionDto,
  IcpRunListItem,
  IcpRunDto,
  EligibleTestDto,
  IcpSampleEntryDto,
  IcpEvidenceDto,
  IcpEvidenceContext,
  IcpEvidenceKind,
  StartIcpRunRequest,
  SaveIcpCalibrationRequest,
  ConfirmIcpCalibrationRequest,
  AddIcpCcvRequest,
  SaveIcpReplicatesRequest,
  SubmitIcpSampleRequest
} from "../types";

interface ApiResponse<T> {
  success: boolean;
  data: T;
  message?: string;
}

export const IcpWorkspaceService = {
  // ---- Instruments / Methods ----

  getInstruments: async (): Promise<IcpInstrumentDto[]> => {
    const res = await apiClient.get<ApiResponse<IcpInstrumentDto[]>>("/icp-workspace/instruments");
    return Array.isArray(res.data?.data) ? res.data.data : [];
  },

  getMethodOptions: async (): Promise<IcpMethodOptionDto[]> => {
    const res = await apiClient.get<ApiResponse<IcpMethodOptionDto[]>>("/icp-workspace/method-options");
    return Array.isArray(res.data?.data) ? res.data.data : [];
  },

  getRunHistory: async (equipmentId: number): Promise<IcpRunListItem[]> => {
    const res = await apiClient.get<ApiResponse<IcpRunListItem[]>>(
      `/icp-workspace/instruments/${equipmentId}/runs`
    );
    return Array.isArray(res.data?.data) ? res.data.data : [];
  },

  // ---- Run Detail ----

  getRun: async (id: number): Promise<IcpRunDto> => {
    const res = await apiClient.get<ApiResponse<IcpRunDto>>(`/icp-workspace/runs/${id}`);
    return res.data?.data;
  },

  getEligibleTests: async (id: number, search?: string): Promise<EligibleTestDto[]> => {
    const params: Record<string, string> = {};
    if (search) params.search = search;
    const res = await apiClient.get<ApiResponse<EligibleTestDto[]>>(
      `/icp-workspace/runs/${id}/eligible-tests`,
      { params }
    );
    return Array.isArray(res.data?.data) ? res.data.data : [];
  },

  getSampleEntry: async (runSampleId: number): Promise<IcpSampleEntryDto> => {
    const res = await apiClient.get<ApiResponse<IcpSampleEntryDto>>(
      `/icp-workspace/samples/${runSampleId}`
    );
    return res.data?.data;
  },

  getTestOrderEvidence: async (testOrderId: number): Promise<IcpEvidenceDto[]> => {
    const res = await apiClient.get<ApiResponse<IcpEvidenceDto[]>>(
      `/icp-workspace/test-orders/${testOrderId}/evidence`
    );
    return Array.isArray(res.data?.data) ? res.data.data : [];
  },

  // ---- Evidence Download & Open ----

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
    const { blob } = await IcpWorkspaceService.downloadEvidence(id);
    const url = window.URL.createObjectURL(blob);
    window.open(url, "_blank");
  },

  // ---- Run Life Cycle & Calibration ----

  startRun: async (req: StartIcpRunRequest): Promise<IcpRunDto> => {
    const res = await apiClient.post<ApiResponse<IcpRunDto>>("/icp-workspace/runs", req);
    return res.data?.data;
  },

  saveCalibration: async (id: number, req: SaveIcpCalibrationRequest): Promise<IcpRunDto> => {
    const res = await apiClient.put<ApiResponse<IcpRunDto>>(`/icp-workspace/runs/${id}/calibration`, req);
    return res.data?.data;
  },

  confirmCalibration: async (id: number, req: ConfirmIcpCalibrationRequest): Promise<IcpRunDto> => {
    const res = await apiClient.post<ApiResponse<IcpRunDto>>(
      `/icp-workspace/runs/${id}/calibration/confirm`,
      req
    );
    return res.data?.data;
  },

  addCcv: async (id: number, req: AddIcpCcvRequest): Promise<IcpRunDto> => {
    const res = await apiClient.post<ApiResponse<IcpRunDto>>(`/icp-workspace/runs/${id}/ccv`, req);
    return res.data?.data;
  },

  abandonRun: async (id: number, reason: string): Promise<IcpRunDto> => {
    const res = await apiClient.post<ApiResponse<IcpRunDto>>(`/icp-workspace/runs/${id}/abandon`, {
      reason
    });
    return res.data?.data;
  },

  completeRun: async (id: number): Promise<IcpRunDto> => {
    const res = await apiClient.post<ApiResponse<IcpRunDto>>(`/icp-workspace/runs/${id}/complete`);
    return res.data?.data;
  },

  // ---- Samples & Entry (slice 1 client methods, full usage in slice 2) ----

  assignSamples: async (id: number, testOrderIds: number[]): Promise<IcpRunDto> => {
    const res = await apiClient.post<ApiResponse<IcpRunDto>>(`/icp-workspace/runs/${id}/samples`, {
      testOrderIds
    });
    return res.data?.data;
  },

  removeSample: async (id: number, runSampleId: number, reason: string): Promise<IcpRunDto> => {
    const res = await apiClient.post<ApiResponse<IcpRunDto>>(
      `/icp-workspace/runs/${id}/samples/${runSampleId}/remove`,
      { reason }
    );
    return res.data?.data;
  },

  saveReplicates: async (runSampleId: number, req: SaveIcpReplicatesRequest): Promise<IcpSampleEntryDto> => {
    const res = await apiClient.put<ApiResponse<IcpSampleEntryDto>>(
      `/icp-workspace/samples/${runSampleId}/replicates`,
      req
    );
    return res.data?.data;
  },

  submitSample: async (runSampleId: number, req: SubmitIcpSampleRequest): Promise<unknown> => {
    const res = await apiClient.post<ApiResponse<unknown>>(
      `/icp-workspace/samples/${runSampleId}/submit`,
      req
    );
    return res.data?.data;
  },

  // ---- Evidence Upload & Supersede ----

  uploadEvidence: async (
    runId: number,
    file: File,
    context: IcpEvidenceContext,
    kind: IcpEvidenceKind,
    runSampleId?: number
  ): Promise<IcpEvidenceDto> => {
    const fd = new FormData();
    fd.append("file", file);
    fd.append("context", context);
    fd.append("kind", kind);
    if (runSampleId !== undefined && runSampleId !== null) {
      fd.append("runSampleId", String(runSampleId));
    }

    const res = await apiClient.post<ApiResponse<IcpEvidenceDto>>(
      `/icp-workspace/runs/${runId}/evidence`,
      fd,
      {
        headers: { "Content-Type": "multipart/form-data" }
      }
    );
    return res.data?.data;
  },

  supersedeEvidence: async (
    evidenceId: number,
    file: File,
    reason: string
  ): Promise<IcpEvidenceDto> => {
    const fd = new FormData();
    fd.append("file", file);
    fd.append("reason", reason);

    const res = await apiClient.post<ApiResponse<IcpEvidenceDto>>(
      `/icp-workspace/evidence/${evidenceId}/supersede`,
      fd,
      {
        headers: { "Content-Type": "multipart/form-data" }
      }
    );
    return res.data?.data;
  }
};
