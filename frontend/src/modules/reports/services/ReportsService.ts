import { apiClient } from "../../../services/apiClient";

export const ReportsService = {
  async getAll() {
    return (await apiClient.get("/reports")).data.data;
  }
};
