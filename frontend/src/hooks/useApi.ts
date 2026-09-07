import { useCallback, useEffect, useState } from "react";
import { apiClient } from "../services/apiClient";

// Small shared data-fetching hook used across module pages.
//
// `reload` lets a caller retry after a failure - callers were previously left
// with an `error` they could report but no way to recover from except a full
// page reload.
export function useApi<T>(url: string) {
  const [data, setData] = useState<T | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [reloadKey, setReloadKey] = useState(0);

  const reload = useCallback(() => setReloadKey((k) => k + 1), []);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError(null);

    apiClient.get(url)
      .then((res) => { if (!cancelled) setData(res.data.data); })
      .catch(() => { if (!cancelled) setError("Failed to load data."); })
      .finally(() => { if (!cancelled) setLoading(false); });

    return () => { cancelled = true; };
  }, [url, reloadKey]);

  return { data, loading, error, reload };
}
