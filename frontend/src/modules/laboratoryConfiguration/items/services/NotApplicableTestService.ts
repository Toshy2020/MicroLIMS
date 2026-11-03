import { apiClient } from "../../../../services/apiClient";

export type NotApplicableStatus = "Draft" | "HeadApproved" | "Approved" | "Withdrawn";

export interface NotApplicableTestDto {
  id: number;
  itemId: number;
  grade: string | null;
  testCode: string;
  justification: string;
  criticality: string;
  immediateAction: string;
  longTermAction: string;
  reference: string | null;
  status: NotApplicableStatus;
  headSignatureId: number | null;
  finalSignatureId: number | null;
  createdByUserId: number;
  createdAt: string;
  testDisplayName: string;
  headSignedBy: string | null;
  headSignedAt: string | null;
  finalSignedBy: string | null;
  finalSignedAt: string | null;
}

export interface CreateNotApplicableTestRequest {
  itemId?: number;
  grade?: string | null;
  testCode: string;
  justification: string;
  criticality: string;
  immediateAction: string;
  longTermAction: string;
  reference?: string | null;
}

export interface NotApplicableSignRequest {
  password: string;
  comment?: string | null;
}

export interface NotApplicableWithdrawRequest {
  password: string;
  reason: string;
}

export const NotApplicableTestService = {
  async listForItem(itemId: number): Promise<NotApplicableTestDto[]> {
    const res = await apiClient.get<{ data: NotApplicableTestDto[] }>(`/items/${itemId}/not-applicable-tests`);
    return res.data.data;
  },

  async create(itemId: number, req: Omit<CreateNotApplicableTestRequest, "itemId">): Promise<NotApplicableTestDto> {
    const res = await apiClient.post<{ data: NotApplicableTestDto }>(`/items/${itemId}/not-applicable-tests`, {
      ...req,
      itemId,
    });
    return res.data.data;
  },

  async headApprove(id: number, password: string, comment?: string | null): Promise<NotApplicableTestDto> {
    const res = await apiClient.post<{ data: NotApplicableTestDto }>(`/not-applicable-tests/${id}/head-approve`, {
      password,
      comment: comment || null,
    });
    return res.data.data;
  },

  async finalApprove(id: number, password: string, comment?: string | null): Promise<NotApplicableTestDto> {
    const res = await apiClient.post<{ data: NotApplicableTestDto }>(`/not-applicable-tests/${id}/final-approve`, {
      password,
      comment: comment || null,
    });
    return res.data.data;
  },

  async withdraw(id: number, password: string, reason: string): Promise<NotApplicableTestDto> {
    const res = await apiClient.post<{ data: NotApplicableTestDto }>(`/not-applicable-tests/${id}/withdraw`, {
      password,
      reason,
    });
    return res.data.data;
  },
};
