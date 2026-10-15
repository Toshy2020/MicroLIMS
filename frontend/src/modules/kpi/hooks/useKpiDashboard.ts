import { useCallback } from "react";
import { kpiDashboardService } from "../services/kpiDashboardService";
import { toQueryParams } from "../utils/kpiFilters";
import type { KpiFiltersValue } from "../utils/kpiFilters";
import { useGuardedFetch } from "./useGuardedFetch";

export function useKpiDashboard(filters: KpiFiltersValue) {
  const { from, to, category, location, testCode } = filters;
  const fetcher = useCallback(
    () => kpiDashboardService.dashboard(toQueryParams({ from, to, category, location, testCode })),
    [from, to, category, location, testCode]
  );
  return useGuardedFetch(fetcher, "The KPI figures could not be loaded.");
}
