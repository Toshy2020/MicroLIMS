import { apiClient } from "../../../services/apiClient";
import type {
  AuditLogItem,
  AuditTraceabilityResult
} from "../types/auditTypes";

export const AuditSearchService = {
  search: (payload: Record<string, any>): Promise<AuditLogItem[]> =>
    apiClient.post("/admin/audit/search", payload).then((r) => r.data.data),

  getForEntity: (entityName: string, entityId: number | string): Promise<AuditLogItem[]> =>
    apiClient.get("/admin/audit", { params: { entityName, entityId } }).then((r) => r.data.data),

  getTraceability: (auditLogId: number): Promise<AuditTraceabilityResult> =>
    apiClient.get(`/admin/audit/${auditLogId}/traceability`).then((r) => r.data.data),

  getTraceabilityForEntity: (entityName: string, entityId: number | string): Promise<AuditTraceabilityResult> =>
    apiClient.get("/admin/audit/traceability", { params: { entityName, entityId } }).then((r) => r.data.data)
};
