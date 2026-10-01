import { useApi } from "../../../hooks/useApi";
import { MyTask } from "../types/dashboard";
import { DashboardLabCode, labQuery } from "../DashboardLabContext";

// Backend rejects this for non-Analyst roles (403) - only call it from
// Analyst-gated panels.
export function useMyTasks(lab: DashboardLabCode | null = null) {
  return useApi<MyTask[]>(`/dashboard/my-tasks${labQuery(lab)}`);
}
