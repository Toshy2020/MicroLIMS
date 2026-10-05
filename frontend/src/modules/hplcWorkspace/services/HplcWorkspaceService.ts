import { apiClient } from "../../../services/apiClient";
import type {
  HplcTechnique,
  HplcInstrumentDto,
  HplcMethodOptionDto,
  HplcRunListItem,
  HplcRunDto,
  EligibleTestDto,
  HplcSampleEntryDto,
  HplcEvidenceDto,
  HplcDissolutionStandardResult,
  HplcEvidenceContext,
  HplcEvidenceKind,
  StartHplcRunRequest,
  SaveSstRequest,
  ConfirmSstRequest,
  HplcSstRecordDto,
  SaveReplicatesRequest,
  SubmitHplcSampleRequest,
  EligibleQualificationDto,
  HplcQualificationEntryDto
} from "../types";

interface ApiResponse<T> {
  success: boolean;
  data: T;
  message?: string;
}

export const HplcWorkspaceService = {
  // ---- Instruments / Methods ----

  getInstruments: async (technique?: HplcTechnique): Promise<HplcInstrumentDto[]> => {
    const params = technique ? { technique } : undefined;
    const res = await apiClient.get<ApiResponse<HplcInstrumentDto[]>>("/hplc-workspace/instruments", { params });
    return Array.isArray(res.data?.data) ? res.data.data : [];
  },

  getMethodOptions: async (technique?: HplcTechnique): Promise<HplcMethodOptionDto[]> => {
    const params = technique ? { technique } : undefined;
    const res = await apiClient.get<ApiResponse<HplcMethodOptionDto[]>>("/hplc-workspace/method-options", { params });
    return Array.isArray(res.data?.data) ? res.data.data : [];
  },

  getRunHistory: async (equipmentId: number): Promise<HplcRunListItem[]> => {
    const res = await apiClient.get<ApiResponse<HplcRunListItem[]>>(
      `/hplc-workspace/instruments/${equipmentId}/runs`
    );
    return Array.isArray(res.data?.data) ? res.data.data : [];
  },

  // ---- Run Detail ----

  getRun: async (id: number): Promise<HplcRunDto> => {
    const res = await apiClient.get<ApiResponse<HplcRunDto>>(`/hplc-workspace/runs/${id}`);
    return res.data?.data;
  },

  getEligibleTests: async (id: number, search?: string): Promise<EligibleTestDto[]> => {
    const params: Record<string, string> = {};
    if (search) params.search = search;
    const res = await apiClient.get<ApiResponse<EligibleTestDto[]>>(
      `/hplc-workspace/runs/${id}/eligible-tests`,
      { params }
    );
    return Array.isArray(res.data?.data) ? res.data.data : [];
  },

  getSampleEntry: async (runSampleId: number): Promise<HplcSampleEntryDto> => {
    const res = await apiClient.get<ApiResponse<HplcSampleEntryDto>>(
      `/hplc-workspace/samples/${runSampleId}`
    );
    return res.data?.data;
  },

  // ---- Evidence Download & Open ----

  // Current reports behind a test order's HPLC result (sample reports and the
  // SST standard report of each run), for the reviewer.
  getTestOrderEvidence: async (testOrderId: number): Promise<HplcEvidenceDto[]> => {
    const res = await apiClient.get<ApiResponse<HplcEvidenceDto[]>>(
      `/hplc-workspace/test-orders/${testOrderId}/evidence`
    );
    return res.data?.data ?? [];
  },

  // The standard (and Cs) from the HPLC run a dissolution test is assigned to.
  getDissolutionStandard: async (testOrderId: number): Promise<HplcDissolutionStandardResult> => {
    const res = await apiClient.get<ApiResponse<HplcDissolutionStandardResult>>(
      `/hplc-workspace/test-orders/${testOrderId}/dissolution-standard`
    );
    return res.data?.data ?? { standard: null, problem: null };
  },

  downloadEvidence: async (id: number): Promise<{ blob: Blob; contentType: string; fileName: string }> => {
    const res = await apiClient.get(`/hplc-workspace/evidence/${id}/file`, {
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
    const { blob } = await HplcWorkspaceService.downloadEvidence(id);
    const url = window.URL.createObjectURL(blob);
    window.open(url, "_blank");
  },

  // ---- Run Life Cycle & SST ----

  startRun: async (req: StartHplcRunRequest): Promise<HplcRunDto> => {
    const res = await apiClient.post<ApiResponse<HplcRunDto>>("/hplc-workspace/runs", req);
    return res.data?.data;
  },

  saveSst: async (id: number, req: SaveSstRequest): Promise<HplcSstRecordDto> => {
    const res = await apiClient.put<ApiResponse<HplcSstRecordDto>>(`/hplc-workspace/runs/${id}/sst`, req);
    return res.data?.data;
  },

  confirmSst: async (id: number, req: ConfirmSstRequest): Promise<HplcSstRecordDto> => {
    const res = await apiClient.post<ApiResponse<HplcSstRecordDto>>(
      `/hplc-workspace/runs/${id}/sst/confirm`,
      req
    );
    return res.data?.data;
  },

  abandonRun: async (id: number, reason: string): Promise<HplcRunDto> => {
    const res = await apiClient.post<ApiResponse<HplcRunDto>>(`/hplc-workspace/runs/${id}/abandon`, {
      reason
    });
    return res.data?.data;
  },

  completeRun: async (id: number): Promise<HplcRunDto> => {
    const res = await apiClient.post<ApiResponse<HplcRunDto>>(`/hplc-workspace/runs/${id}/complete`);
    return res.data?.data;
  },

  // ---- Samples & Entry (Part B / S7b) ----

  assignSamples: async (id: number, testOrderIds: number[]): Promise<HplcRunDto> => {
    const res = await apiClient.post<ApiResponse<HplcRunDto>>(`/hplc-workspace/runs/${id}/samples`, {
      testOrderIds
    });
    return res.data?.data;
  },

  removeSample: async (id: number, runSampleId: number, reason: string): Promise<HplcRunDto> => {
    const res = await apiClient.post<ApiResponse<HplcRunDto>>(
      `/hplc-workspace/runs/${id}/samples/${runSampleId}/remove`,
      { reason }
    );
    return res.data?.data;
  },

  saveReplicates: async (runSampleId: number, req: SaveReplicatesRequest): Promise<HplcSampleEntryDto> => {
    const res = await apiClient.put<ApiResponse<HplcSampleEntryDto>>(
      `/hplc-workspace/samples/${runSampleId}/replicates`,
      req
    );
    return res.data?.data;
  },

  submitSample: async (runSampleId: number, req: SubmitHplcSampleRequest): Promise<unknown> => {
    const res = await apiClient.post<ApiResponse<unknown>>(
      `/hplc-workspace/samples/${runSampleId}/submit`,
      req
    );
    return res.data?.data;
  },

  // ---- Working Standard Qualifications (Task 10) ----

  getEligibleQualifications: async (runId: number): Promise<EligibleQualificationDto[]> => {
    const res = await apiClient.get<ApiResponse<EligibleQualificationDto[]>>(`/hplc-workspace/runs/${runId}/eligible-qualifications`);
    return Array.isArray(res.data?.data) ? res.data.data : [];
  },

  assignQualifications: async (runId: number, qualificationIds: number[]): Promise<HplcRunDto> => {
    const res = await apiClient.post<ApiResponse<HplcRunDto>>(`/hplc-workspace/runs/${runId}/qualifications`, { qualificationIds });
    return res.data?.data;
  },

  getQualificationEntry: async (runSampleId: number): Promise<HplcQualificationEntryDto> => {
    const res = await apiClient.get<ApiResponse<HplcQualificationEntryDto>>(`/hplc-workspace/qualification-samples/${runSampleId}`);
    return res.data?.data;
  },

  saveQualificationReplicates: async (runSampleId: number, req: SaveReplicatesRequest): Promise<HplcQualificationEntryDto> => {
    const res = await apiClient.put<ApiResponse<HplcQualificationEntryDto>>(`/hplc-workspace/qualification-samples/${runSampleId}/replicates`, req);
    return res.data?.data;
  },

  submitQualification: async (runSampleId: number, req: SubmitHplcSampleRequest): Promise<HplcQualificationEntryDto> => {
    const res = await apiClient.post<ApiResponse<HplcQualificationEntryDto>>(`/hplc-workspace/qualification-samples/${runSampleId}/submit`, req);
    return res.data?.data;
  },

  // ---- Evidence Upload & Supersede ----

  uploadEvidence: async (
    runId: number,
    file: File,
    context: HplcEvidenceContext,
    kind: HplcEvidenceKind,
    runSampleId?: number
  ): Promise<HplcEvidenceDto> => {
    const fd = new FormData();
    fd.append("file", file);
    fd.append("context", context);
    fd.append("kind", kind);
    if (runSampleId !== undefined && runSampleId !== null) {
      fd.append("runSampleId", String(runSampleId));
    }

    const res = await apiClient.post<ApiResponse<HplcEvidenceDto>>(
      `/hplc-workspace/runs/${runId}/evidence`,
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
  ): Promise<HplcEvidenceDto> => {
    const fd = new FormData();
    fd.append("file", file);
    fd.append("reason", reason);

    const res = await apiClient.post<ApiResponse<HplcEvidenceDto>>(
      `/hplc-workspace/evidence/${evidenceId}/supersede`,
      fd,
      {
        headers: { "Content-Type": "multipart/form-data" }
      }
    );
    return res.data?.data;
  }
};
