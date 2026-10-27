import { apiClient } from "../../../services/apiClient";
import type {
  AuditLogItem,
  AuditSearchPageResult,
  AuditTraceabilityResult,
  CursorPage,
  LoginHistoryItem,
  SecurityEventItem
} from "../types/auditTypes";

export const AuditSearchService = {
  // One keyset page (newest first). Pass the previous page's nextCursor as BeforeId for the next one.
  searchPage: (payload: Record<string, unknown>): Promise<AuditSearchPageResult> =>
    apiClient.post("/admin/audit/search", payload).then((r) => r.data.data),

  // First page only, as a plain list.
  search: (payload: Record<string, unknown>): Promise<AuditLogItem[]> =>
    apiClient.post("/admin/audit/search", payload).then((r) => (r.data.data as AuditSearchPageResult).items),

  // CSV for the current filter (the server caps it at 50,000 rows and records the export as an audit event).
  exportCsv: async (payload: Record<string, unknown>): Promise<void> => {
    const res = await apiClient.post("/admin/audit/export", payload, { responseType: "blob" });
    const url = window.URL.createObjectURL(new Blob([res.data], { type: "text/csv" }));
    const link = document.createElement("a");
    link.href = url;
    link.download = `microlims-audit-trail-${new Date().toISOString().slice(0, 10)}.csv`;
    document.body.appendChild(link);
    link.click();
    link.remove();
    window.URL.revokeObjectURL(url);
  },

  getSecurityEvents: (params: { take: number; beforeId?: number | null }): Promise<CursorPage<SecurityEventItem>> =>
    apiClient.get("/admin/audit/security-events", { params }).then((r) => r.data.data),

  getLoginHistory: (params: { take: number; beforeId?: number | null }): Promise<CursorPage<LoginHistoryItem>> =>
    apiClient.get("/admin/audit/login-history", { params }).then((r) => r.data.data),

  getForEntity: (entityName: string, entityId: number | string): Promise<AuditLogItem[]> =>
    apiClient.get("/admin/audit", { params: { entityName, entityId } }).then((r) => r.data.data),

  getTraceability: (auditLogId: number): Promise<AuditTraceabilityResult> =>
    apiClient.get(`/admin/audit/${auditLogId}/traceability`).then((r) => r.data.data),

  getTraceabilityForEntity: (entityName: string, entityId: number | string): Promise<AuditTraceabilityResult> =>
    apiClient.get("/admin/audit/traceability", { params: { entityName, entityId } }).then((r) => r.data.data)
};
