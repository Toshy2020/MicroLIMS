import { apiClient } from "../../../services/apiClient";

export const DashboardService = {
  async getAll() {
    return (await apiClient.get("/dashboard")).data.data;
  }
};
