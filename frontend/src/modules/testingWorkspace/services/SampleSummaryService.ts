import { apiClient } from "../../../services/apiClient";
import { SampleSummary } from "../types/sampleSummaryTypes";

// Only 4 of the backend's 6 ApprovalDecision values are reachable from
// the sample-level flow - Investigation/OOSInvestigation stay
// TestOrder-level-only (the old ApprovalService/DecisionDialog).
export type SampleApprovalDecision = "Approve" | "Reject" | "NewSampleRequest" | "RetestRetainedSample";

// Downloads a blob response as a file - same pattern as ReportsPage's
// existing report downloads (apiClient with responseType "blob" so the
// Authorization header still attaches, then an object URL + temp <a>).
async function downloadBlob(url: string, fileName: string): Promise<void> {
  const res = await apiClient.get(url, { responseType: "blob" });
  const objectUrl = window.URL.createObjectURL(res.data);
  const a = document.createElement("a");
  a.href = objectUrl;
  a.download = fileName;
  a.click();
  window.URL.revokeObjectURL(objectUrl);
}

export const SampleSummaryService = {
  async getSummary(sampleId: number): Promise<SampleSummary> {
    return (await apiClient.get(`/samples/${sampleId}/summary`)).data.data;
  },
  exportPdf(sampleId: number, referenceNumber: string): Promise<void> {
    return downloadBlob(`/samples/${sampleId}/summary/pdf`, `SampleSummary_${referenceNumber}.pdf`);
  },
  exportWord(sampleId: number, referenceNumber: string): Promise<void> {
    return downloadBlob(`/samples/${sampleId}/summary/word`, `SampleSummary_${referenceNumber}.docx`);
  },
  async completeReview(sampleId: number, password: string, comment: string | undefined): Promise<void> {
    await apiClient.post(`/samples/${sampleId}/review/complete`, { password, comment });
  },
  async decideApproval(sampleId: number, password: string, decision: SampleApprovalDecision, comment: string | undefined): Promise<void> {
    await apiClient.post(`/samples/${sampleId}/approval/decide`, { password, decision, comment });
  }
};
