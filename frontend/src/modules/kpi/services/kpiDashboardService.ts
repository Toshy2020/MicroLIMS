import { apiClient } from "../../../services/apiClient";
import type { KpiDashboard, KpiDefinitions, KpiRecordPage } from "../types/kpiTypes";

export interface KpiQueryParams {
  from: string; // yyyy-MM-dd, inclusive
  to: string;   // yyyy-MM-dd, inclusive
  category?: string;
  location?: string;
  testCode?: string;
  analystId?: number;
}

export interface KpiRecordParams extends KpiQueryParams {
  page?: number;
  pageSize?: number;
}

interface Envelope<T> { success: boolean; data: T }

// Send only what the user set: the server reads a missing analystId as "lab level".
function clean(params: object): Record<string, unknown> {
  return Object.fromEntries(Object.entries(params).filter(([, v]) => v !== undefined && v !== null && v !== ""));
}

export const kpiDashboardService = {
  dashboard: (params: KpiQueryParams) =>
    apiClient.get<Envelope<KpiDashboard>>("/kpi/dashboard", { params: clean(params) }).then((r) => r.data.data),

  records: (key: string, params: KpiRecordParams) =>
    apiClient.get<Envelope<KpiRecordPage>>(`/kpi/${encodeURIComponent(key)}/records`, { params: clean(params) }).then((r) => r.data.data),

  definitions: (from: string, to: string) =>
    apiClient.get<Envelope<KpiDefinitions>>("/kpi/definitions", { params: { from, to } }).then((r) => r.data.data),
};
