import { apiClient } from "../../../services/apiClient";

export const AuditSearchService = {
  search: (payload: Record<string, any>) => apiClient.post("/admin/audit/search", payload).then((r) => r.data.data),
  getForEntity: (entityName: string, entityId: number | string) =>
    apiClient.get("/admin/audit", { params: { entityName, entityId } }).then((r) => r.data.data)
};
