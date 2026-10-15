import { apiClient, ifMatch } from "../../../services/apiClient";
import type {
  CreateWorkflowProfileVersionRequest,
  LiveWorkflowType,
  SignWorkflowProfileDraftRequest,
  WorkflowProfileCoverage,
  WorkflowProfileOverview,
  WorkflowProfileStatus,
  WorkflowProfileVersion
} from "../types/workflowProfileTypes";

interface Envelope<T> {
  success: boolean;
  data: T;
}

const BASE = "/kpi/workflow-profiles";

export interface WorkflowProfileListParams {
  workflowType?: LiveWorkflowType;
  testCode?: string;
  defaultOnly?: boolean;
  status?: WorkflowProfileStatus;
}

export const workflowProfileService = {
  async list(params: WorkflowProfileListParams = {}): Promise<WorkflowProfileVersion[]> {
    const res = await apiClient.get<Envelope<WorkflowProfileVersion[]>>(BASE, { params });
    return res.data.data;
  },

  async overview(): Promise<WorkflowProfileOverview> {
    const res = await apiClient.get<Envelope<WorkflowProfileOverview>>(`${BASE}/overview`);
    return res.data.data;
  },

  async coverage(): Promise<WorkflowProfileCoverage> {
    const res = await apiClient.get<Envelope<WorkflowProfileCoverage>>(`${BASE}/coverage`);
    return res.data.data;
  },

  async create(req: CreateWorkflowProfileVersionRequest): Promise<WorkflowProfileVersion> {
    const res = await apiClient.post<Envelope<WorkflowProfileVersion>>(BASE, req);
    return res.data.data;
  },

  // `version` is the Draft's version as loaded; the server refuses a stale form with 409.
  async signDraft(id: number, version: number, req: SignWorkflowProfileDraftRequest): Promise<WorkflowProfileVersion> {
    const res = await apiClient.post<Envelope<WorkflowProfileVersion>>(`${BASE}/${id}/sign`, req, ifMatch(version));
    return res.data.data;
  }
};
