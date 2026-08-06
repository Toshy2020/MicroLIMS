import { useApi } from "../../../hooks/useApi";
import { DashboardSummary } from "../types/dashboard";

export function useDashboardSummary() {
  return useApi<DashboardSummary>("/dashboard");
}
