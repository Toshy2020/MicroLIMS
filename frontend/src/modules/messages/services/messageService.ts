import { apiClient } from "../../../services/apiClient";
import {
  ConversationSummary,
  DirectMessage,
  CreateConversationRequest,
  UserDirectoryItem
} from "../types/messageTypes";

export const messageService = {
  async getConversations(): Promise<ConversationSummary[]> {
    const res = await apiClient.get("/messages/conversations");
    return res.data.data;
  },

  async getConversationById(id: number): Promise<ConversationSummary> {
    const res = await apiClient.get(`/messages/conversations/${id}`);
    return res.data.data;
  },

  async createConversation(req: CreateConversationRequest): Promise<ConversationSummary> {
    const res = await apiClient.post("/messages/conversations", req);
    return res.data.data;
  },

  async getMessages(conversationId: number, take: number = 50): Promise<DirectMessage[]> {
    const res = await apiClient.get(`/messages/conversations/${conversationId}/messages`, {
      params: { take }
    });
    return res.data.data;
  },

  async sendMessage(conversationId: number, content: string): Promise<DirectMessage> {
    const res = await apiClient.post(`/messages/conversations/${conversationId}/messages`, {
      content
    });
    return res.data.data;
  },

  async markAsRead(conversationId: number): Promise<void> {
    await apiClient.post(`/messages/conversations/${conversationId}/read`);
  },

  async getUnreadCount(): Promise<number> {
    const res = await apiClient.get("/messages/unread-count");
    return res.data.data.unreadCount;
  },

  async getUserDirectory(): Promise<UserDirectoryItem[]> {
    const res = await apiClient.get("/users/directory");
    return res.data.data;
  }
};
