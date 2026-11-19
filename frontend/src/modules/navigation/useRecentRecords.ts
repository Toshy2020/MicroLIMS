import { useCallback, useEffect, useState } from "react";
import { toast } from "sonner";
import { getErrorMessage } from "../../utils/errorMessage";
import { recentRecordsService, type RecentRecordDto } from "./services/recentRecordsService";

export const MAX_SIDEBAR_RECENTS = 5;

// Lets a page that just recorded an opening refresh any list on screen.
const touchListeners = new Set<() => void>();
export function notifyRecentTouched() {
  touchListeners.forEach((l) => l());
}

export interface UseRecentRecords {
  items: RecentRecordDto[];
  loaded: boolean;
  // True when the list could not be loaded; callers hide their section.
  failed: boolean;
  // Resolves true when the list was cleared.
  clear(): Promise<boolean>;
}

export function useRecentRecords(): UseRecentRecords {
  const [items, setItems] = useState<RecentRecordDto[]>([]);
  const [loaded, setLoaded] = useState(false);
  const [failed, setFailed] = useState(false);
  const [version, setVersion] = useState(0);

  useEffect(() => {
    const listener = () => setVersion((v) => v + 1);
    touchListeners.add(listener);
    return () => {
      touchListeners.delete(listener);
    };
  }, []);

  useEffect(() => {
    const controller = new AbortController();
    recentRecordsService
      .list(controller.signal)
      .then((list) => {
        if (controller.signal.aborted) return;
        setItems(list);
        setFailed(false);
        setLoaded(true);
      })
      .catch(() => {
        if (controller.signal.aborted) return;
        setFailed(true);
        setLoaded(true);
      });
    return () => controller.abort();
  }, [version]);

  const clear = useCallback(async (): Promise<boolean> => {
    try {
      await recentRecordsService.clear();
      setItems([]);
      notifyRecentTouched();
      return true;
    } catch (err) {
      toast.error(getErrorMessage(err, "Could not clear your recent records."));
      return false;
    }
  }, []);

  return { items, loaded, failed, clear };
}
