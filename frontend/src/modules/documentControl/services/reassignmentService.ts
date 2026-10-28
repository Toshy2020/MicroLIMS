import { apiClient } from "../../../services/apiClient";
import type {
  ReassignmentCandidate,
  ReassignmentCount,
  ReassignmentItemType,
  ReassignmentQueue,
  ReassignRequest
} from "../types/reassignmentTypes";

export const reassignmentService = {
  async getQueue(departmentId?: number): Promise<ReassignmentQueue> {
    const res = await apiClient.get("/document-control/reassignments", { params: departmentId ? { departmentId } : {} });
    return res.data.data;
  },

  async getCount(): Promise<ReassignmentCount> {
    return (await apiClient.get("/document-control/reassignments/count")).data.data;
  },

  // Exactly the users the server would accept as the new holder.
  async getCandidates(itemType: ReassignmentItemType, itemId: number): Promise<ReassignmentCandidate[]> {
    const res = await apiClient.get("/document-control/reassignments/candidates", { params: { itemType, itemId } });
    return res.data.data;
  },

  async reassign(request: ReassignRequest): Promise<void> {
    await apiClient.post("/document-control/reassignments", request);
  }
};
