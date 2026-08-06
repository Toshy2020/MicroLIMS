import { apiClient } from "../../../services/apiClient";

export const ReviewService = {
  async submitReview(testOrderId: number, comment: string | undefined, password: string) {
    return (await apiClient.post("/review", { testOrderId, comment, password })).data.data;
  }
};
