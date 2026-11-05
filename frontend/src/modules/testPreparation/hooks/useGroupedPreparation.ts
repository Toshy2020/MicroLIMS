import { useCallback, useEffect, useState } from "react";
import {
  SamplePreparationService,
  type ExcludedPreparationSample,
  type GroupedPreparation,
  type PreparationLotOptions
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

// The lots a group's one diluent / neutralizer pair is picked from (D10: one
// pair per group, signed once for every sample in it). Fetched once, the
// first time the group is opened.
export function useGroupLotOptions(configurationId: number, enabled: boolean) {
  const [lots, setLots] = useState<PreparationLotOptions | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!enabled || lots) return;
    let cancelled = false;
    setError(null);
    SamplePreparationService.getLotOptions({ configurationId })
      .then((o) => { if (!cancelled) setLots(o); })
      .catch((e: any) => {
        if (!cancelled) setError(e?.response?.data?.message ?? "Could not load the available lots.");
      });
    return () => { cancelled = true; };
  }, [configurationId, enabled, lots]);

  return { lots, error };
}
