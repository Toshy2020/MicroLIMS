import { useCallback, useEffect, useState } from "react";
import { apiClient } from "../services/apiClient";
import { getErrorMessage } from "../utils/errorMessage";

// Small shared data-fetching hook used across module pages.
//
// `reload` lets a caller retry after a failure - callers were previously left
// with an `error` they could report but no way to recover from except a full
// page reload.
//
// Each request carries an AbortController signal (CLAUDE.md 6.2). Changing the
// URL, reloading, or unmounting cancels the request still in flight, so a slow
// answer for an old URL can never overwrite the current one, and a cancelled
// request is never shown as an error.
export function useApi<T>(url: string) {
  const [data, setData] = useState<T | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [reloadKey, setReloadKey] = useState(0);

  const reload = useCallback(() => setReloadKey((k) => k + 1), []);

  useEffect(() => {
    const controller = new AbortController();
    setLoading(true);
    setError(null);

    apiClient.get(url, { signal: controller.signal })
      .then((res) => { if (!controller.signal.aborted) setData(res.data.data); })
      .catch((err) => {
        if (!controller.signal.aborted) setError(getErrorMessage(err, "The data could not be loaded. Try again."));
      })
      .finally(() => { if (!controller.signal.aborted) setLoading(false); });

    return () => controller.abort();
  }, [url, reloadKey]);

  return { data, loading, error, reload };
}
