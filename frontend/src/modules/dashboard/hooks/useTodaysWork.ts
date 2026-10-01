import { useApi } from "../../../hooks/useApi";
import { TodaysWorkItem } from "../types/dashboard";
import { DashboardLabCode, labQuery } from "../DashboardLabContext";

export function useTodaysWork(lab: DashboardLabCode | null = null) {
  return useApi<TodaysWorkItem[]>(`/dashboard/todays-work${labQuery(lab)}`);
}
