import { apiClient, ifMatch } from "../../../services/apiClient";
import type { KpiRecordPage } from "../types/kpiTypes";
import type {
  AnalystAvailability,
  AnalystWorkload,
  KpiSettings,
  SaveAvailabilityRequest,
  UpdateKpiSettingsRequest,
} from "../types/workloadTypes";

interface Envelope<T> {
  success: boolean;
  data: T;
}

export interface WorkloadParams {
  from: string;
  to: string;
  category?: string | null;
  location?: string | null;
  testCode?: string | null;
  analystId?: number | null;
  page?: number;
  pageSize?: number;
}

// Null, undefined and blank values are not sent. In particular analystId is sent only when set: without
// Kpi.ViewTeam the server answers with the caller's own row either way, and with it, no analystId means the team.
const clean = (params: object): Record<string, unknown> =>
  Object.fromEntries(Object.entries(params).filter(([, v]) => v !== null && v !== undefined && v !== ""));

export const kpiWorkloadService = {
  async workload(params: WorkloadParams): Promise<AnalystWorkload> {
    const res = await apiClient.get<Envelope<AnalystWorkload>>("/kpi/workload", { params: clean(params) });
    return res.data.data;
  },

  // page and pageSize are sent only when set; the server defaults to page 1, size 50.
  async workloadTests(params: WorkloadParams): Promise<KpiRecordPage> {
    const res = await apiClient.get<Envelope<KpiRecordPage>>("/kpi/workload/tests", { params: clean(params) });
    return res.data.data;
  },

  async getSettings(sectionId: number): Promise<KpiSettings> {
    const res = await apiClient.get<Envelope<KpiSettings>>(`/kpi/settings/${sectionId}`);
    return res.data.data;
  },

  async updateSettings(sectionId: number, version: number, req: UpdateKpiSettingsRequest): Promise<KpiSettings> {
    const res = await apiClient.put<Envelope<KpiSettings>>(`/kpi/settings/${sectionId}`, req, ifMatch(version));
    return res.data.data;
  },

  async listAvailability(params: { userId?: number | null; from?: string; to?: string } = {}): Promise<AnalystAvailability[]> {
    const res = await apiClient.get<Envelope<AnalystAvailability[]>>("/kpi/availability", { params: clean(params) });
    return res.data.data;
  },

  async createAvailability(req: SaveAvailabilityRequest): Promise<AnalystAvailability> {
    const res = await apiClient.post<Envelope<AnalystAvailability>>("/kpi/availability", req);
    return res.data.data;
  },

  async updateAvailability(id: number, req: SaveAvailabilityRequest): Promise<AnalystAvailability> {
    const res = await apiClient.put<Envelope<AnalystAvailability>>(`/kpi/availability/${id}`, req);
    return res.data.data;
  },
};
