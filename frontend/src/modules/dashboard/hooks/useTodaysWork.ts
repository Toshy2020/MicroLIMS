import { useApi } from "../../../hooks/useApi";
import { TodaysWorkItem } from "../types/dashboard";

export function useTodaysWork() {
  return useApi<TodaysWorkItem[]>("/dashboard/todays-work");
}
