import { useCallback } from "react";
import { kpiWorkloadService } from "../services/kpiWorkloadService";
import { toQueryParams } from "../utils/kpiFilters";
import type { KpiFiltersValue } from "../utils/kpiFilters";
import { useGuardedFetch } from "./useGuardedFetch";

export function useKpiWorkload(filters: KpiFiltersValue, analystId?: number) {
  const { from, to, category, location, testCode } = filters;
  const fetcher = useCallback(
    () => kpiWorkloadService.workload({ ...toQueryParams({ from, to, category, location, testCode }), analystId }),
    [from, to, category, location, testCode, analystId]
  );
  return useGuardedFetch(fetcher, "The workload figures could not be loaded.");
}
