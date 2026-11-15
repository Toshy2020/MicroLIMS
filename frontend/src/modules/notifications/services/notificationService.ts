import { apiClient } from "../../../services/apiClient";

interface ApiEnvelope<T> {
  data: T;
}

export type NotificationStateFilter = "unread" | "read" | "done" | "all";

export type EmailMode = "Off" | "Immediate" | "Digest";

export interface NotificationTargetDto {
  type: string | null;
  id: number | null;
  sampleId: number | null;
  testOrderId: number | null;
}

export interface NotificationDto {
  id: number;
  type: string;
  title: string;
  message: string;
  severity: string;
  createdAt: string;
  readAt: string | null;
  resolvedAt: string | null;
  target: NotificationTargetDto;
  sectionName: string | null;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageNumber: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface NotificationListParams {
  state?: NotificationStateFilter;
  type?: string;
  sectionId?: number;
  q?: string;
  page?: number;
  pageSize?: number;
}

export interface NotificationSummaryDto {
  unread: number;
}

export interface NotificationPreferenceRowDto {
  type: string;
  title: string;
  emailAllowed: boolean;
  emailMode: EmailMode;
  mandatory: boolean;
  mandatoryReason: string | null;
}

export interface UpdateNotificationPreferenceItem {
  type: string;
  emailMode: EmailMode;
}

export interface MandatoryNotificationTypeDto {
  type: string;
  title: string;
  emailAllowed: boolean;
  mandatory: boolean;
  setByUserId: number | null;
  setAt: string | null;
}

export interface UserWithoutEmailDto {
  id: number;
  username: string;
  fullName: string;
  roleName: string;
}

export interface EmailOutboxSummaryDto {
  waiting: number;
  failed: number;
  lastSentAt: string | null;
  usersWithoutEmail: UserWithoutEmailDto[];
}

export const notificationService = {
  async list(params: NotificationListParams = {}, signal?: AbortSignal): Promise<PagedResult<NotificationDto>> {
    const query: Record<string, string | number> = {};
    if (params.state) query.state = params.state;
    if (params.type) query.type = params.type;
    if (params.sectionId !== undefined) query.sectionId = params.sectionId;
    if (params.q) query.q = params.q;
    if (params.page !== undefined) query.page = params.page;
    if (params.pageSize !== undefined) query.pageSize = params.pageSize;

    const res = await apiClient.get<ApiEnvelope<PagedResult<NotificationDto>>>("/notifications", {
      params: query,
      signal
    });
    return res.data.data;
  },

  async summary(signal?: AbortSignal): Promise<NotificationSummaryDto> {
    const res = await apiClient.get<ApiEnvelope<NotificationSummaryDto>>("/notifications/summary", { signal });
    return res.data.data;
  },

  async markRead(id: number): Promise<void> {
    await apiClient.post(`/notifications/${id}/read`);
  },

  async markAllRead(): Promise<void> {
    await apiClient.post("/notifications/read-all");
  },

  async getPreferences(signal?: AbortSignal): Promise<NotificationPreferenceRowDto[]> {
    const res = await apiClient.get<ApiEnvelope<NotificationPreferenceRowDto[]>>("/notification-preferences", {
      signal
    });
    return res.data.data;
  },

  async putPreferences(preferences: UpdateNotificationPreferenceItem[]): Promise<NotificationPreferenceRowDto[]> {
    const res = await apiClient.put<ApiEnvelope<NotificationPreferenceRowDto[]>>("/notification-preferences", {
      preferences
    });
    return res.data.data;
  },

  async getMandatory(sectionId: number, signal?: AbortSignal): Promise<MandatoryNotificationTypeDto[]> {
    const res = await apiClient.get<ApiEnvelope<MandatoryNotificationTypeDto[]>>(
      "/notification-preferences/mandatory",
      {
        params: { sectionId },
        signal
      }
    );
    return res.data.data;
  },

  async putMandatory(sectionId: number, types: string[]): Promise<MandatoryNotificationTypeDto[]> {
    const res = await apiClient.put<ApiEnvelope<MandatoryNotificationTypeDto[]>>(
      "/notification-preferences/mandatory",
      { types },
      { params: { sectionId } }
    );
    return res.data.data;
  },

  async getEmailOutboxSummary(signal?: AbortSignal): Promise<EmailOutboxSummaryDto> {
    const res = await apiClient.get<ApiEnvelope<EmailOutboxSummaryDto>>("/admin/email-outbox/summary", { signal });
    return res.data.data;
  }
};
