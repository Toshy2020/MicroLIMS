import { apiClient } from "../../../services/apiClient";
import {
  DiscussionCategory,
  DiscussionPostSummary,
  DiscussionPostDetail,
  DiscussionVersion,
  DiscussionComment,
  PagedResult
} from "../types/discussionTypes";

export const discussionService = {
  async getFeed(
    categoryId?: DiscussionCategory,
    search?: string,
    importantOnly?: boolean,
    page: number = 1,
    pageSize: number = 20
  ): Promise<PagedResult<DiscussionPostSummary>> {
    const params: Record<string, any> = { page, pageSize };
    if (categoryId) params.categoryId = categoryId;
    if (search?.trim()) params.search = search.trim();
    if (importantOnly) params.importantOnly = true;

    const res = await apiClient.get("/discussions", { params });
    return res.data.data;
  },

  async getById(id: number): Promise<DiscussionPostDetail> {
    const res = await apiClient.get(`/discussions/${id}`);
    return res.data.data;
  },

  async createPost(formData: FormData): Promise<DiscussionPostDetail> {
    const res = await apiClient.post("/discussions", formData, {
      headers: { "Content-Type": "multipart/form-data" }
    });
    return res.data.data;
  },

  async updatePost(
    id: number,
    data: { title: string; content: string; category: number; isImportant: boolean }
  ): Promise<DiscussionPostDetail> {
    const res = await apiClient.put(`/discussions/${id}`, data);
    return res.data.data;
  },

  async toggleImportant(id: number): Promise<boolean> {
    const res = await apiClient.patch(`/discussions/${id}/important`);
    return res.data.data.isImportant;
  },

  async deletePost(id: number): Promise<void> {
    await apiClient.delete(`/discussions/${id}`);
  },

  async getHistory(id: number): Promise<DiscussionVersion[]> {
    const res = await apiClient.get(`/discussions/${id}/history`);
    return res.data.data;
  },

  async downloadAttachment(postId: number, attachmentId: number, fileName: string): Promise<void> {
    const response = await apiClient.get(`/discussions/${postId}/attachments/${attachmentId}/download`, {
      responseType: "blob"
    });
    const url = window.URL.createObjectURL(new Blob([response.data]));
    const link = document.createElement("a");
    link.href = url;
    link.setAttribute("download", fileName);
    document.body.appendChild(link);
    link.click();
    link.remove();
    window.URL.revokeObjectURL(url);
  },

  async addComment(postId: number, content: string): Promise<DiscussionComment> {
    const res = await apiClient.post(`/discussions/${postId}/comments`, { content });
    return res.data.data;
  },

  async updateComment(postId: number, commentId: number, content: string): Promise<DiscussionComment> {
    const res = await apiClient.put(`/discussions/${postId}/comments/${commentId}`, { content });
    return res.data.data;
  },

  async deleteComment(postId: number, commentId: number): Promise<void> {
    await apiClient.delete(`/discussions/${postId}/comments/${commentId}`);
  }
};
