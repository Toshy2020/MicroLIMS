import { useCallback, useEffect, useState } from "react";
import { workflowProfileService } from "../services/workflowProfileService";
import {
  profileErrorMessage,
  type WorkflowProfileCoverage,
  type WorkflowProfileOverview
} from "../types/workflowProfileTypes";

export function useWorkflowProfiles() {
  const [overview, setOverview] = useState<WorkflowProfileOverview | null>(null);
  const [coverage, setCoverage] = useState<WorkflowProfileCoverage | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const reload = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [o, c] = await Promise.all([workflowProfileService.overview(), workflowProfileService.coverage()]);
      setOverview(o);
      setCoverage(c);
    } catch (e) {
      setError(profileErrorMessage(e, "Could not load workflow profiles."));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void reload();
  }, [reload]);

  return { overview, coverage, loading, error, reload };
}
