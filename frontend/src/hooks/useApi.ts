import { useEffect, useState } from "react";
import { apiClient } from "../services/apiClient";

// Small shared data-fetching hook used across module pages.
export function useApi<T>(url: string) {
  const [data, setData] = useState<T | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    setLoading(true);
    apiClient.get(url)
      .then((res) => setData(res.data.data))
      .catch(() => setError("Failed to load data."))
      .finally(() => setLoading(false));
  }, [url]);

  return { data, loading, error };
}
