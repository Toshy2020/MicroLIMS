import { useCallback, useEffect, useRef, useState } from "react";

export interface GuardedFetch<T> {
  data: T | null;
  loading: boolean;
  error: string | null;
  reload(): Promise<void>;
}

// Only the newest request may write state. Without that, a slow response for
// an older filter lands last and the page shows figures for the wrong period.
// An error drops the data too: stale figures must not sit under an error banner.
// `fetcher` must be memoised by the caller (useCallback on the filter values).
export function useGuardedFetch<T>(fetcher: () => Promise<T>, failureMessage: string): GuardedFetch<T> {
  const [data, setData] = useState<T | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const latest = useRef(0);

  const reload = useCallback(async () => {
    const mine = ++latest.current;
    setLoading(true);
    setError(null);
    try {
      const res = await fetcher();
      if (mine === latest.current) setData(res);
    } catch (e: unknown) {
      if (mine === latest.current) {
        setData(null);
        const err = e as { response?: { data?: { message?: string } } } | undefined;
        setError(err?.response?.data?.message ?? failureMessage);
      }
    } finally {
      if (mine === latest.current) setLoading(false);
    }
  }, [fetcher, failureMessage]);

  useEffect(() => { void reload(); }, [reload]);

  return { data, loading, error, reload };
}
