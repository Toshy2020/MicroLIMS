import { useCallback, useEffect, useState } from "react";
import {
  SamplePreparationService,
  type ExcludedPreparationSample,
  type GroupedPreparation
} from "../services/SamplePreparationService";

interface UseGroupedPreparationProps {
  sampleIds: number[];
  enabled?: boolean;
}

// Mirrors useActionableGroups so the Grouped Actions panel can hold both
// lists side by side: preparation first, then whatever incubation setup the
// samples become eligible for once it is signed.
export function useGroupedPreparation({ sampleIds, enabled = true }: UseGroupedPreparationProps) {
  const [groups, setGroups] = useState<GroupedPreparation[]>([]);
  const [excluded, setExcluded] = useState<ExcludedPreparationSample[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const sampleIdsKey = sampleIds.slice().sort((a, b) => a - b).join(",");

  const reload = useCallback(async (silent = false) => {
    if (!enabled || !sampleIdsKey) {
      setGroups([]);
      setExcluded([]);
      return;
    }

    if (!silent) setLoading(true);
    setError(null);

    try {
      const resp = await SamplePreparationService.getGroups(
        sampleIdsKey.split(",").map(Number)
      );
      setGroups(resp.groups || []);
      setExcluded(resp.excluded || []);
    } catch (err: any) {
      setError(err?.response?.data?.message || err?.message || "Failed to load grouped preparation.");
    } finally {
      if (!silent) setLoading(false);
    }
  }, [enabled, sampleIdsKey]);

  useEffect(() => {
    if (enabled) {
      reload(false);
    } else {
      setGroups([]);
      setExcluded([]);
    }
  }, [enabled, reload]);

  return { groups, excluded, loading, error, reload };
}
