import { apiClient } from "../../../services/apiClient";
import type {
  ErrorSeverity,
  IncidentDetail,
  IncidentFilterState,
  IncidentListResult
} from "../types/errorMonitoringTypes";

// Shared by the list, the export and every refresh, so a filtered CSV is
// guaranteed to contain exactly what the screen is showing.
function toQueryParams(filters: IncidentFilterState): Record<string, string> {
  const params: Record<string, string> = {};

  if (filters.severity) params.severity = filters.severity;
  if (filters.source) params.source = filters.source;
  if (filters.status) params.status = filters.status;
  if (filters.search.trim()) params.search = filters.search.trim();
  if (filters.fromDate) params.fromUtc = new Date(filters.fromDate).toISOString();

  if (filters.toDate) {
    // Inclusive end date: a user picking "to 9 Sep" means the whole day.
    const to = new Date(filters.toDate);
    to.setHours(23, 59, 59, 999);
    params.toUtc = to.toISOString();
  }

  return params;
}

export const ErrorMonitoringService = {
  async search(
    filters: IncidentFilterState,
    page: number,
    pageSize: number
  ): Promise<IncidentListResult> {
    const res = await apiClient.get("/error-monitoring/incidents", {
      // Backend pages from 1; MUI's TablePagination is zero-based.
      params: { ...toQueryParams(filters), page: page + 1, pageSize }
    });
    return res.data.data as IncidentListResult;
  },

  async get(incidentId: string): Promise<IncidentDetail> {
    const res = await apiClient.get(`/error-monitoring/incidents/${incidentId}`);
    return res.data.data as IncidentDetail;
  },

  async resolve(incidentId: string, resolutionNotes: string | null): Promise<void> {
    await apiClient.post(`/error-monitoring/incidents/${incidentId}/resolve`, { resolutionNotes });
  },

  async reopen(incidentId: string): Promise<void> {
    await apiClient.post(`/error-monitoring/incidents/${incidentId}/reopen`);
  },

  async overrideSeverity(errorLogId: string, severity: ErrorSeverity | null): Promise<void> {
    await apiClient.post(`/error-monitoring/error-logs/${errorLogId}/severity-override`, { severity });
  },

  async exportCsv(filters: IncidentFilterState): Promise<void> {
    const res = await apiClient.get("/error-monitoring/incidents/export", {
      params: toQueryParams(filters),
      responseType: "blob"
    });

    const url = window.URL.createObjectURL(new Blob([res.data], { type: "text/csv" }));
    const link = document.createElement("a");
    link.href = url;
    link.download = `microlims-incidents-${new Date().toISOString().slice(0, 10)}.csv`;
    document.body.appendChild(link);
    link.click();
    link.remove();
    window.URL.revokeObjectURL(url);
  }
};
