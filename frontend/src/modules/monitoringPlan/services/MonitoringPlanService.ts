import { apiClient } from "../../../services/apiClient";
import type {
  CoverageReport,
  SpacingReport,
  DueResult,
  MonitoringCategory,
  MonitoringChecklist,
  OccurrenceDetail,
  OccurrenceSummary,
  SignedChange,
  YearSummary
} from "../types";

const BASE = "/monitoring-occurrences";
const REPORTS = "/monitoring-reports";

// Row versions travel in the If-Match header like the other editable records.
const ifMatch = (version: number) => ({ headers: { "If-Match": String(version) } });

export const MonitoringPlanService = {
  getCoverage: (year: number, category?: MonitoringCategory) =>
    apiClient.get(`${REPORTS}/coverage`, { params: { year, category } }).then((r) => r.data.data as CoverageReport),

  getSpacing: (year: number, category: MonitoringCategory | undefined, flaggedOnly: boolean) =>
    apiClient.get(`${REPORTS}/spacing`, { params: { year, category, flaggedOnly } }).then((r) => r.data.data as SpacingReport),

  getYear: (year: number, category?: MonitoringCategory) =>
    apiClient.get(`${BASE}/calendar/year`, { params: { year, category } }).then((r) => r.data.data as YearSummary),

  getCalendar: (from: string, to: string, category?: MonitoringCategory) =>
    apiClient.get(`${BASE}/calendar`, { params: { from, to, category } }).then((r) => r.data.data as OccurrenceSummary[]),

  getDue: (category?: MonitoringCategory) =>
    apiClient.get(`${BASE}/due`, { params: { category } }).then((r) => r.data.data as DueResult),

  getDetail: (id: number) =>
    apiClient.get(`${BASE}/${id}`).then((r) => r.data.data as OccurrenceDetail),

  getChecklist: (sampleId: number) =>
    apiClient.get(`${BASE}/for-session`, { params: { sampleId } }).then((r) => r.data.data as MonitoringChecklist),

  createManual: (body: { machineId: number; targetDate: string; machinePartConfigurationIds: number[]; reason?: string }) =>
    apiClient.post(`${BASE}/manual`, body).then((r) => r.data.data as OccurrenceSummary),

  skip: (id: number, version: number, body: SignedChange) =>
    apiClient.post(`${BASE}/${id}/skip`, body, ifMatch(version)).then((r) => r.data.data as OccurrenceSummary),

  changeWindow: (id: number, version: number, body: SignedChange & { newTargetDate: string | null; newWindowEnd: string }) =>
    apiClient.post(`${BASE}/${id}/change-window`, body, ifMatch(version)).then((r) => r.data.data as OccurrenceSummary)
};
