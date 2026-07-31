import { apiClient } from "../../../../services/apiClient";

export const MediaService = {
  async getAll() {
    return (await apiClient.get("/admin/media")).data.data;
  }
};
