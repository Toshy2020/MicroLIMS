import { apiClient } from "../../../services/apiClient";

export const ReviewService = {
  async submitReview(testOrderId: number, comment?: string) {
    return (await apiClient.post("/review", { testOrderId, comment })).data.data;
  }
};
