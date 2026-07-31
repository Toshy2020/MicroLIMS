import { apiClient } from "../../../services/apiClient";

// Mirrors backend Domain.Enums.ApprovalDecision (serialized as strings).
export type ApprovalDecision =
  | "Approve"
  | "Reject"
  | "RetestRetainedSample"
  | "NewSampleRequest"
  | "Investigation"
  | "OOSInvestigation";

export const ApprovalService = {
  async decide(testOrderId: number, decision: ApprovalDecision, comment?: string) {
    return (await apiClient.post("/approval", { testOrderId, decision, comment })).data.data;
  }
};
