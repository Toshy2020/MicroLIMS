import { useCallback, useRef, useState } from "react";
import { toast } from "sonner";

/**
 * Holds the copy of a record that an edit dialog works from. The caller's `record` is the copy it
 * was opened with; after a user-requested reload from the "changed by someone else" banner,
 * `current` is the freshly fetched copy (including its row version, used as If-Match on the next
 * save) until the caller passes a different record.
 */
export function useReloadedRecord<T>(record: T | null, fetchFresh: (record: T) => Promise<T | null | undefined>) {
  const [reloaded, setReloaded] = useState<{ source: T; value: T } | null>(null);
  const fetchRef = useRef(fetchFresh);
  fetchRef.current = fetchFresh;

  const current: T | null = reloaded && reloaded.source === record ? reloaded.value : record;

  const reload = useCallback(async (): Promise<T | null> => {
    if (!record) return null;
    try {
      const fresh = await fetchRef.current(record);
      if (!fresh) {
        toast.error("Could not reload the latest record.");
        return null;
      }
      setReloaded({ source: record, value: fresh });
      return fresh;
    } catch {
      toast.error("Could not reload the latest record.");
      return null;
    }
  }, [record]);

  return { current, reload };
}
