import { apiClient } from "../../../services/apiClient";

export interface BackgroundJobRun {
  id: number;
  jobName: string;
  startedAt: string;
  finishedAt: string | null;
  outcome: "Running" | "Succeeded" | "Failed" | "Skipped";
  itemsProcessed: number | null;
  error: string | null;
  triggeredBy: "Schedule" | "Manual";
  triggeredByUserId: number | null;
  reason: string | null;
}

export interface BackgroundJobStatus {
  name: string;
  description: string;
  classification: "Housekeeping" | "Gxp";
  canRunManually: boolean;
  classificationReason: string;
  schedule: string;
  lastRun: BackgroundJobRun | null;
  lastSuccessAt: string | null;
  lastFailureAt: string | null;
  lastPassFinishedAt: string | null;
  runningNow: boolean;
}

export interface BackgroundJobRunPage {
  items: BackgroundJobRun[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export const BackgroundJobService = {
  async list(): Promise<BackgroundJobStatus[]> {
    return (await apiClient.get("/admin/jobs")).data.data;
  },
  async runs(name: string, page = 1, pageSize = 20): Promise<BackgroundJobRunPage> {
    return (await apiClient.get(`/admin/jobs/${encodeURIComponent(name)}/runs`, { params: { page, pageSize } })).data.data;
  },
  async run(name: string, reason: string): Promise<BackgroundJobRun> {
    return (await apiClient.post(`/admin/jobs/${encodeURIComponent(name)}/run`, { reason })).data.data;
  }
};
