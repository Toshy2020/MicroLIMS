import { useApi } from "../../../hooks/useApi";
import { IncubationOverviewRow } from "../types/dashboard";

export function useIncubationOverview() {
  return useApi<IncubationOverviewRow[]>("/dashboard/incubation-overview");
}
