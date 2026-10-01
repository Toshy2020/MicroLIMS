import { useApi } from "../../../hooks/useApi";
import { DashboardSummary } from "../types/dashboard";
import { DashboardLabCode, labQuery } from "../DashboardLabContext";

export function useDashboardSummary(lab: DashboardLabCode | null = null) {
  return useApi<DashboardSummary>(`/dashboard${labQuery(lab)}`);
}
