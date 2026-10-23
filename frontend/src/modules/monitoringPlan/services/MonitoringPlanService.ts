import { apiClient } from "../../../services/apiClient";
import type {
  CoverageReport,
  Deviation,
  DeviationClosure,
  DeviationKind,
  DeviationList,
  DeviationScan,
  DeviationStatus,
  LabCalendarDate,
  MonitoringSettings,
  SpacingReport,
  DueResult,
  MonitoringCategory,
  MonitoringChecklist,
  OccurrenceDetail,
  OccurrenceSummary,
  PlanPreview,
  PlanRevision,
  SignedChange,
  YearSummary
} from "../types";

const BASE = "/monitoring-occurrences";
const REPORTS = "/monitoring-reports";

// Row versions travel in the If-Match header like the other editable records.
const ifMatch = (version: number) => ({ headers: { "If-Match": String(version) } });

export const MonitoringSettingsApi = {
  get: () => apiClient.get("/monitoring/settings").then((r) => r.data.data as MonitoringSettings),
  save: (s: MonitoringSettings) => {
    const { id: _id, version, ...body } = s;
    return apiClient.put("/monitoring/settings", body, ifMatch(version)).then((r) => r.data.data as MonitoringSettings);
  },
  getDates: (year: number) =>
    apiClient.get("/monitoring/calendar", { params: { year } }).then((r) => r.data.data as LabCalendarDate[]),
  addDate: (date: string, description: string) =>
    apiClient.post("/monitoring/calendar", { date, description }).then((r) => r.data.data as LabCalendarDate),
  removeDate: (id: number) => apiClient.delete(`/monitoring/calendar/${id}`)
};

export const MonitoringPlanService = {
  getRevisions: (year: number) =>
    apiClient.get("/monitoring-plan/revisions", { params: { year } }).then((r) => r.data.data as PlanRevision[]),

  // goLiveDate: when the plan starts. Omitted means today (or 1 January of a future year).
  generateDraft: (year: number, goLiveDate?: string) =>
    apiClient.post("/monitoring-plan/revisions", { year, goLiveDate: goLiveDate || null }).then((r) => r.data.data as PlanPreview),

  getPreview: (id: number) =>
    apiClient.get(`/monitoring-plan/revisions/${id}/preview`).then((r) => r.data.data as PlanPreview),

  approve: (id: number, password: string, reason?: string) =>
    apiClient.post(`/monitoring-plan/revisions/${id}/approve`, { password, reason: reason || null }).then((r) => r.data.data as PlanRevision),

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

  createManual: (body: { machineId: number; targetDate: string; machinePartConfigurationIds?: number[]; reason?: string; kind?: "Day" | "Week" | "Month" }) =>
    apiClient.post(`${BASE}/manual`, body).then((r) => r.data.data as OccurrenceSummary),

  skip: (id: number, version: number, body: SignedChange) =>
    apiClient.post(`${BASE}/${id}/skip`, body, ifMatch(version)).then((r) => r.data.data as OccurrenceSummary),

  changeWindow: (id: number, version: number, body: SignedChange & { newTargetDate: string | null; newWindowEnd: string }) =>
    apiClient.post(`${BASE}/${id}/change-window`, body, ifMatch(version)).then((r) => r.data.data as OccurrenceSummary)
};

export const MonitoringDeviationApi = {
  list: (status?: DeviationStatus, kind?: DeviationKind) =>
    apiClient.get("/monitoring-deviations", { params: { status, kind } }).then((r) => r.data.data as DeviationList),

  scan: () => apiClient.post("/monitoring-deviations/scan").then((r) => r.data.data as DeviationScan),

  close: (deviationIds: number[], closure: DeviationClosure, body: SignedChange) =>
    apiClient.post("/monitoring-deviations/close", { deviationIds, closure, ...body }).then((r) => r.data.data as number)
};

export type { Deviation };
