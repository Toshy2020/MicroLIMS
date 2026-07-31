import { apiClient } from "../../../services/apiClient";
import { SampleCard } from "../types/workspaceTypes";

export const WorkspaceService = {
  async getActiveSamples(): Promise<SampleCard[]> {
    const res = await apiClient.get("/testorders");
    return res.data.data;
  }
};
