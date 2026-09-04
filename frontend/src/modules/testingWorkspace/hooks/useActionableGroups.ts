import { useState, useEffect, useCallback } from "react";
import { ActionableGroup } from "../types/testWorkflowTypes";
import { TestWorkflowService } from "../services/TestWorkflowService";

interface UseActionableGroupsProps {
  scope?: string;
  actionType?: string;
  sampleIds?: number[];
  enabled?: boolean;
}

export function useActionableGroups({
  scope = "mine",
  actionType,
  sampleIds,
  enabled = true
}: UseActionableGroupsProps = {}) {
  const [groups, setGroups] = useState<ActionableGroup[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const sampleIdsKey = sampleIds?.slice().sort().join(",") ?? "";

  const reload = useCallback(async (silent = false) => {
    if (!enabled) return;
    if (!silent) setLoading(true);
    setError(null);

    try {
      const resp = await TestWorkflowService.getActionableGroups({
        scope,
        actionType,
        sampleIds: sampleIdsKey || undefined
      });
      setGroups(resp.groups || []);
    } catch (err: any) {
      setError(err?.response?.data?.message || err?.message || "Failed to load actionable groups.");
    } finally {
      if (!silent) setLoading(false);
    }
  }, [enabled, scope, actionType, sampleIdsKey]);

  useEffect(() => {
    if (enabled) {
      reload(false);
    } else {
      setGroups([]);
    }
  }, [enabled, reload]);

  return {
    groups,
    loading,
    error,
    reload
  };
}
