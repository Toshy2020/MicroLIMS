import { apiClient } from "../../../services/apiClient";
import type {
  DocumentApprovalTaskDto,
  CreateApprovalTaskRequest,
  ApprovalReadinessDto,
  ApprovalDossierDto,
  ExecuteApprovalDecisionRequest,
  DocumentApprovalTaskStatus
} from "../types/documentControlTypes";

export const documentApprovalService = {
  createApprovalTask: async (
    revisionId: number,
    request: CreateApprovalTaskRequest
  ): Promise<DocumentApprovalTaskDto> => {
    const res = await apiClient.post(
      `/document-control/revisions/${revisionId}/approvals`,
      request
    );
    return res.data.data;
  },

  getActiveApprovalTask: async (
    revisionId: number
  ): Promise<DocumentApprovalTaskDto | null> => {
    const res = await apiClient.get(
      `/document-control/revisions/${revisionId}/active-approval`
    );
    return res.data.data;
  },

  getApprovalTask: async (
    approvalTaskId: number
  ): Promise<DocumentApprovalTaskDto> => {
    const res = await apiClient.get(
      `/document-control/approvals/${approvalTaskId}`
    );
    return res.data.data;
  },

  getApprovalTasks: async (params?: {
    revisionId?: number;
    approverUserId?: number;
    status?: DocumentApprovalTaskStatus;
  }): Promise<DocumentApprovalTaskDto[]> => {
    const res = await apiClient.get(`/document-control/approvals`, { params });
    return res.data.data;
  },

  getApprovalDossier: async (
    approvalTaskId: number
  ): Promise<ApprovalDossierDto> => {
    const res = await apiClient.get(
      `/document-control/approvals/${approvalTaskId}/dossier`
    );
    return res.data.data;
  },

  validateApprovalReadiness: async (
    approvalTaskId: number
  ): Promise<ApprovalReadinessDto> => {
    const res = await apiClient.get(
      `/document-control/approvals/${approvalTaskId}/readiness`
    );
    return res.data.data;
  },

  executeApprovalDecision: async (
    approvalTaskId: number,
    request: ExecuteApprovalDecisionRequest
  ): Promise<DocumentApprovalTaskDto> => {
    const res = await apiClient.post(
      `/document-control/approvals/${approvalTaskId}/decide`,
      request
    );
    return res.data.data;
  },

  signApproval: async (
    approvalTaskId: number,
    request: { password: string; decisionNotes?: string; effectiveDate?: string }
  ): Promise<DocumentApprovalTaskDto> => {
    const res = await apiClient.post(
      `/document-control/approvals/${approvalTaskId}/sign`,
      request
    );
    return res.data.data;
  },

  getApprovalSignature: async (
    approvalTaskId: number
  ): Promise<import("../types/documentControlTypes").ElectronicSignatureDto | null> => {
    const res = await apiClient.get(
      `/document-control/approvals/${approvalTaskId}/signature`
    );
    return res.data.data;
  }
};
